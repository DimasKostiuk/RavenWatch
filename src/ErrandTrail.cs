#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Odin's golden «Хугінів слід до скарбу»: the raven leads you leg by leg (follow, route … from:<slot>), at the end of
    // each a sign someone left (clue), at the last a hidden chest (cache), and when it is opened its keeper rises
    // (kill_champion … guard). Both things are seen only here (RuneRing.WantProp), E on them is the step's deed.

    // clue <slot> <fire|bones|feathers|stone>: a sign at that place; look at it — the raven says what it means
    internal sealed class ClueStep : ErrandStep
    {
        private const float Reach = 5f, Seen = 80f;
        private string _slot = "", _kind = "fire";
        protected override void Read() { _slot = Arg(1, ""); _kind = Arg(2, "any"); }
        private static readonly string[] Kinds = { "fire", "bones", "feathers", "stone" };
        // its kind; «any»: one of them, the same for this errand every time (by its stamp)
        private string K(ErrandRun r, int i) { return _kind != "any" ? _kind : Kinds[(int)((uint)ChampionStep.Stamp(r, i).GetStableHashCode() % (uint)Kinds.Length)]; }
        internal override int Need { get { return 1; } }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = Reach; at = Vector3.zero;
            string v;
            return r.Slot.TryGetValue(_slot, out v) && ErrandSteps.Point(v, out at);
        }

        private float Dist(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p == null || !Where(r, i, out at, out rad)) return float.MaxValue;
            Vector3 d = p.transform.position - at; d.y = 0f;
            return d.magnitude;
        }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p != null && r.Count[i] < 1 && Where(r, i, out at, out rad) && Dist(r, i, p) < Seen)
                RuneRing.WantProp(at, r.Id + "." + i, Props(K(r, i)), Name(K(r, i)), K(r, i) == "feathers" || K(r, i) == "bones" ? 2.5f : 1f);
            return false;
        }

        internal override string ActLabel(ErrandRun r, int i, Player p, out string why)
        {
            why = null;
            if (r.Count[i] >= 1 || p == null) return "";
            if (Dist(r, i, p) > Reach) why = Lang.T("біля прикмети");
            return Lang.T("Роздивитися");
        }

        internal override string Act(ErrandRun r, int i, Player p)
        {
            r.Count[i] = 1;
            Trail.Say(p, r, Hugin(K(r, i)), Munin(K(r, i)));
            return null;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= 1) return Lang.T("Прикмету знайдено");
            return Lang.F("Роздивися прикмету: {0} — ще {1} м", Lang.LowerFirst(Name(K(r, i))), Mathf.RoundToInt(Mathf.Max(0f, Dist(r, i, p) - Reach)));
        }

        private static string[] Props(string kind)
        {
            switch (kind)
            {
                case "bones": return new[] { "BoneFragments", "TrophySkeleton" };
                case "feathers": return new[] { "Feathers" };
                case "stone": return new[] { "stone_pile", "Stone" };
                default: return new[] { "fire_pit", "fire_pit_iron" };
            }
        }

        private static string Name(string kind)
        {
            switch (kind)
            {
                case "bones": return Lang.T("Обгризені кістки");
                case "feathers": return Lang.T("Чуже пір'я");
                case "stone": return Lang.T("Купка каміння з руною");
                default: return Lang.T("Старе кострище");
            }
        }

        private static string[] Hugin(string kind)
        {
            switch (kind)
            {
                case "bones": return Lang.Keys("Кістки. Не бійся, вони вже нікуди не йдуть. А от той, хто їх обгриз, — міг піти. Далі.", "Хтось тут обідав, і не сам. Слід веде далі — і він свіжіший за ці кістки.");
                case "feathers": return Lang.Keys("Пір'я. Не моє, якщо тобі цікаво. Хтось ніс тут щось важке і зачепився. Далі — сюди.", "Чуже пір'я, і його багато. Той, хто ніс скарб, мав крила — або дуже поспішав.");
                case "stone": return Lang.Keys("Руна на камені, подряпана поспіхом. Каже: «не тут». Значить, далі.", "Хтось склав каміння й вирізав знак. Так позначають дорогу ті, хто збирається повернутися.");
                default: return Lang.Keys("Старе кострище. Хтось ночував тут і дуже поспішав далі — бачиш, недогарки? Туди.", "Вогонь давно згас, але попіл не розвіяло. Тут сиділи — і звідси пішли туди, куди веду я.");
            }
        }

        private static string[] Munin(string kind)
        {
            switch (kind)
            {
                case "bones": return Lang.Keys("О, кістки! Ну хоч хтось тут дійшов до кінця. Далі, далі.", "Обгризене. Не я, якщо що. Пішли, поки той, хто гриз, не повернувся.");
                case "feathers": return Lang.Keys("Чуже пір'я. Тут якась курка пройшла. Я її вже ненавиджу заочно.", "Пір'я! Хтось тут линяв від страху. Іди далі, не нюхай.");
                case "stone": return Lang.Keys("Каміння з дряпулькою. Якийсь грамотій позначив. Далі, бо я засну.", "Руна. Пише, що далі. Я б і без руни сказав.");
                default: return Lang.Keys("Кострище. Хтось тут жер і не поділився. Йдемо далі, поки я злий.", "Попіл холодний. Значить, той скупердяй давно пішов. Наздоженемо.");
            }
        }
    }

    // cache <slot>: the hidden chest at that place; opened — what is in it falls out (valuables of its land, by your stage)
    internal sealed class CacheStep : ErrandStep
    {
        private const float Reach = 4f, Seen = 80f;
        private string _slot = "";
        protected override void Read() { _slot = Arg(1, ""); }
        internal override int Need { get { return 1; } }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            radius = Reach; at = Vector3.zero;
            string v;
            return r.Slot.TryGetValue(_slot, out v) && ErrandSteps.Point(v, out at);
        }

        private float Dist(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p == null || !Where(r, i, out at, out rad)) return float.MaxValue;
            Vector3 d = p.transform.position - at; d.y = 0f;
            return d.magnitude;
        }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (p != null && r.Count[i] < 1 && Where(r, i, out at, out rad) && Dist(r, i, p) < Seen)
                RuneRing.WantProp(at, r.Id + "." + i, new[] { "TreasureChest_meadows", "TreasureChest_blackforest", "piece_chest_wood" }, Lang.T("Схованка"), 1f);
            return false;
        }

        internal override string ActLabel(ErrandRun r, int i, Player p, out string why)
        {
            why = null;
            if (r.Count[i] >= 1 || p == null) return "";
            if (Dist(r, i, p) > Reach) why = Lang.T("біля схованки");
            return Lang.T("Відкрити схованку");
        }

        internal override string Act(ErrandRun r, int i, Player p)
        {
            Vector3 at; float rad;
            if (!Where(r, i, out at, out rad)) return Lang.T("Схованки не видно.");
            if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetSolidHeight(at);
            r.Count[i] = 1;
            int n = Trail.Loot(p, at);
            Errands.Log("cache of " + r.Id + " opened: " + n + " kinds of things");
            RavenFx.Play("sfx_chest_open", at, Quaternion.identity);
            RavenFx.Play("vfx_offering", at, Quaternion.identity);
            Trail.Say(p, r, Lang.Keys("Ось воно. Хтось ховав це довго й старанно. А тепер — обернись.", "Скарб. Тільки не радій завчасно: такі речі рідко лежать без сторожа."),
                            Lang.Keys("О-о, блищить! Хапай, поки... ой. Чуєш? Земля гуде.", "Ну, нарешті щось путнє! Тільки хтось це стеріг, і він не радий."));
            return null;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= 1) return Lang.T("Схованку відкрито");
            return Lang.F("Відкрий схованку — ще {0} м", Mathf.RoundToInt(Mathf.Max(0f, Dist(r, i, p) - Reach)));
        }
    }

    internal static class Trail
    {
        // a word from the errand's raven (the other when that one is not on you)
        internal static void Say(Player p, ErrandRun r, string[] hugin, string[] munin)
        {
            if (p == null) return;
            bool m = r != null && r.Munin;
            if (!Shoulder.Present(p, m)) m = !m;
            if (!Shoulder.Present(p, m)) return;
            string[] from = m ? munin : hugin;
            RavenSkills.SayFromShoulder(p, m, Lang.T(from[Random.Range(0, from.Length)]));
        }

        // what lies in a cache: coins and valuables, more and better by the land (its tier, no further than your stage)
        private struct Find { public string Item; public int Min, Max, From; public Find(string i, int min, int max, int from) { Item = i; Min = min; Max = max; From = from; } }
        private static readonly Find[] Finds =
        {
            new Find("Coins", 40, 80, 0), new Find("Amber", 1, 3, 0), new Find("AmberPearl", 1, 2, 1), new Find("Ruby", 1, 2, 2),
            new Find("SilverNecklace", 1, 1, 3),
        };

        // drops it all out of the chest; how many kinds
        internal static int Loot(Player p, Vector3 at)
        {
            if (ObjectDB.instance == null) return 0;
            int land = WorldGenerator.instance != null ? Champions.TierOf(WorldGenerator.instance.GetBiome(at)) : 0;
            int tier = Mathf.Min(land, Mathf.Min(Stage.Of(p), 7));
            float more = 1f + 0.35f * tier; // (deeper lands, fuller chests)
            int kinds = 0;
            foreach (Find f in Finds)
            {
                if (tier < f.From) continue;
                GameObject prefab = ObjectDB.instance.GetItemPrefab(f.Item);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(Random.Range(f.Min, f.Max + 1) * (f.Item == "Coins" ? more * 1.5f : more)));
                ItemDrop.ItemData item = drop.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                int max = Mathf.Max(1, item.m_shared.m_maxStackSize);
                while (n > 0)
                {
                    int k = Mathf.Min(n, max);
                    ItemDrop.ItemData one = item.Clone();
                    one.m_stack = k;
                    ItemDrop.DropItem(one, k, at + Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    n -= k;
                }
                kinds++;
            }
            return kinds;
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What a cleaner (and a servant, off its house's floor) picks up (the user, 2026-10-30):
    //   by kinds of things, each on or off on its round menu («Що прибирати»): wood, stone, ore and metal, fuel and resin,
    //   hides and cloth, beasts' spoils, plants and seeds, food, trophies, gear, valuables, the rest — all on at first.
    //   A thing is sorted once by its prefab (the game's own: by its name; a mod's: by its type and a word in its name).
    //   What a station's worker takes out lies by that station — the bars by a smelter (the kiln's coal, the mill's
    //   flour…), the mead by a barrel, the honey by a hive — it is left to that worker when the base has one; with nobody
    //   on that job the cleaner takes it (else it would lie there for good).
    // Its choice: one int in its ZDO (the kinds it leaves), written when changed.
    internal static class NpcTidy
    {
        internal static readonly int KOff = "RavenWatch_npcTidyOff".GetStableHashCode(); // int: a bit per kind it leaves

        internal sealed class Kind { public string Id, Title, Icon, Desc; }
        internal static readonly Kind[] Kinds =
        {
            new Kind { Id = "wood", Title = Lang.K("Деревина"), Icon = "n-t-wood", Desc = Lang.K("Дерево, тонке дерево, колоди, кора, чорне й іггдрасилеве дерево.") },
            new Kind { Id = "stone", Title = Lang.K("Камінь"), Icon = "n-t-stone", Desc = Lang.K("Камінь, кремінь, обсидіан, кришталь, мармур, граустен.") },
            new Kind { Id = "ore", Title = Lang.K("Руда й метал"), Icon = "n-t-ore", Desc = Lang.K("Руда, брухт, злитки, цвяхи.") },
            new Kind { Id = "fuel", Title = Lang.K("Паливо й смола"), Icon = "n-t-fuel", Desc = Lang.K("Смола, вугілля, дьоготь, сік.") },
            new Kind { Id = "hide", Title = Lang.K("Шкури й тканини"), Icon = "n-t-hide", Desc = Lang.K("Шкіра, шкури, льон, джут, тканини.") },
            new Kind { Id = "beast", Title = Lang.K("Здобич зі звірів"), Icon = "n-t-beast", Desc = Lang.K("Пір'я, кістки, ікла, хітин, слиз, нутрощі й таке інше з тварин і потвор.") },
            new Kind { Id = "plant", Title = Lang.K("Рослини й насіння"), Icon = "n-t-plant", Desc = Lang.K("Насіння, будяк, кульбаба, льон, ячмінь, коренеплоди.") },
            new Kind { Id = "food", Title = Lang.K("Їжа"), Icon = "n-t-food", Desc = Lang.K("Страви, медовухи, ягоди, гриби, сире м'ясо й риба.") },
            new Kind { Id = "trophy", Title = Lang.K("Трофеї"), Icon = "n-t-trophy", Desc = Lang.K("Трофеї звірів і потвор.") },
            new Kind { Id = "gear", Title = Lang.K("Спорядження"), Icon = "n-t-gear", Desc = Lang.K("Зброя, обладунки, щити, інструменти, стріли.") },
            new Kind { Id = "value", Title = Lang.K("Цінності"), Icon = "n-t-value", Desc = Lang.K("Монети, бурштин, перли, рубіни, намиста.") },
            new Kind { Id = "other", Title = Lang.K("Інше"), Icon = "n-t-other", Desc = Lang.K("Усе, що не підпадає під інші: ключові речі, ядра сурлінгів тощо.") },
        };

        // the game's own things by name (a mod's thing falls through to its type and the words in its name)
        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>();
        private static void N(string kind, params string[] names) { foreach (string n in names) Named[n] = kind; }
        static NpcTidy()
        {
            N("wood", "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Ashwood");
            N("stone", "Stone", "Flint", "Obsidian", "Crystal", "BlackMarble", "Grausten");
            N("ore", "CopperOre", "TinOre", "IronOre", "SilverOre", "BlackMetalScrap", "IronScrap", "CopperScrap", "BronzeScrap", "FlametalOre", "FlametalOreNew",
                "Copper", "Tin", "Bronze", "Iron", "Silver", "BlackMetal", "Flametal", "FlametalNew", "BronzeNails", "IronNails");
            N("fuel", "Resin", "Coal", "Tar", "Sap", "Eitr", "Bitumen");
            N("hide", "LeatherScraps", "DeerHide", "TrollHide", "WolfPelt", "LoxPelt", "ScaleHide", "AskHide", "BjornHide", "Linen", "JuteRed", "JuteBlue", "CeramicPlate");
            N("beast", "Feathers", "BoneFragments", "Guck", "Ooze", "Chitin", "Entrails", "Bloodbag", "WolfFang", "WolfClaw", "Needle", "Carapace", "Mandible", "HardAntler",
                "WitheredBone", "FreezeGland", "Wishbone", "Royaljelly", "MorgenSinew", "MorgenHeart", "CharredBone", "AsksvinCarrionNeck", "AsksvinCarrionPelvic", "AsksvinCarrionRibcage", "AsksvinCarrionSkull", "VoltureEgg", "Bilebag", "ProustitePowder");
            N("plant", "Thistle", "Dandelion", "Flax", "Barley", "CarrotSeeds", "TurnipSeeds", "OnionSeeds", "BeechSeeds", "FirCone", "PineCone", "BirchSeeds", "AncientSeed",
                "Carrot", "Turnip", "Onion", "SeedCarrot", "SeedTurnip", "SeedOnion", "Fiddleheadfern", "SmokePuff", "MushroomJotunPuffs", "MushroomMagecap", "VineberrySeeds");
            N("food", "RawMeat", "DeerMeat", "NeckTail", "FishRaw", "LoxMeat", "SerpentMeat", "WolfMeat", "BugMeat", "HareMeat", "ChickenMeat", "ChickenEgg", "BoneMawSerpentMeat", "AsksvinMeat", "VoltureMeat");
            N("value", "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "GoldRubyRing");
        }

        private static readonly Dictionary<string, int> Sorted = new Dictionary<string, int>(); // (prefab → kind index, once)

        internal static int KindOf(ItemDrop d)
        {
            string name = d.gameObject.name.Replace("(Clone)", "").Trim();
            int k;
            if (Sorted.TryGetValue(name, out k)) return k;
            k = Index(Sort(name, d.m_itemData != null ? d.m_itemData.m_shared : null));
            if (Sorted.Count > 2000) Sorted.Clear();
            Sorted[name] = k;
            return k;
        }

        private static int Index(string id) { for (int i = 0; i < Kinds.Length; i++) if (Kinds[i].Id == id) return i; return Kinds.Length - 1; }

        private static string Sort(string name, ItemDrop.ItemData.SharedData s)
        {
            string kind;
            if (Named.TryGetValue(name, out kind)) return kind;
            if (s != null)
            {
                switch (s.m_itemType)
                {
                    case ItemDrop.ItemData.ItemType.Trophy: return "trophy";
                    case ItemDrop.ItemData.ItemType.Consumable: case ItemDrop.ItemData.ItemType.Fish: return "food";
                    case ItemDrop.ItemData.ItemType.OneHandedWeapon: case ItemDrop.ItemData.ItemType.TwoHandedWeapon: case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    case ItemDrop.ItemData.ItemType.Bow: case ItemDrop.ItemData.ItemType.Shield: case ItemDrop.ItemData.ItemType.Helmet: case ItemDrop.ItemData.ItemType.Chest:
                    case ItemDrop.ItemData.ItemType.Legs: case ItemDrop.ItemData.ItemType.Shoulder: case ItemDrop.ItemData.ItemType.Hands: case ItemDrop.ItemData.ItemType.Tool:
                    case ItemDrop.ItemData.ItemType.Torch: case ItemDrop.ItemData.ItemType.Ammo: case ItemDrop.ItemData.ItemType.AmmoNonEquipable: case ItemDrop.ItemData.ItemType.Utility:
                    case ItemDrop.ItemData.ItemType.Trinket:
                        return "gear";
                }
            }
            // a mod's material: a word in its name
            string n = name.ToLowerInvariant();
            if (n.Contains("wood") || n.Contains("log") || n.Contains("bark")) return "wood";
            if (n.EndsWith("ore") || n.Contains("scrap") || n.Contains("ingot") || n.Contains("bar") || n.Contains("nails")) return "ore";
            if (n.Contains("stone") || n.Contains("marble") || n.Contains("crystal")) return "stone";
            if (n.Contains("resin") || n.Contains("coal") || n.Contains("tar")) return "fuel";
            if (n.Contains("hide") || n.Contains("pelt") || n.Contains("leather") || n.Contains("linen") || n.Contains("cloth")) return "hide";
            if (n.Contains("seed") || n.Contains("cone")) return "plant";
            if (n.Contains("meat") || n.Contains("fish")) return "food";
            if (n.Contains("trophy")) return "trophy";
            if (n.Contains("bone") || n.Contains("feather") || n.Contains("fang") || n.Contains("claw") || n.Contains("tooth")) return "beast";
            if (n.Contains("coin") || n.Contains("gem") || n.Contains("ruby") || n.Contains("amber")) return "value";
            return "other";
        }

        // ---------------------------------------------------------------- its choice
        internal static int Off(NpcActor a) { ZDO z = a != null ? a.Zdo : null; return z != null ? NpcLocker.Int(z, KOff, 0) : 0; }
        internal static bool Takes(NpcActor a, int kind) { return (Off(a) & (1 << kind)) == 0; }

        // its menu's deed, done where it is run (NpcMenu.OnOwner)
        internal static void Deeds() { NpcMenu.Deeds["tidy"] = (a, at, arg) => { int k; return int.TryParse(arg, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out k) && k >= 0 && k < Kinds.Length ? Toggle(a, k) : null; }; }

        private static string Toggle(NpcActor a, int kind)
        {
            string busy = NpcKit.Busy(a); if (busy != null) return busy;
            int off = Off(a) ^ (1 << kind);
            NpcLocker.Put(a.Zdo, KOff, off);
            if (a.Worker != null) a.Worker.Reset();
            return Lang.T(Kinds[kind].Title) + ": " + ((off & (1 << kind)) == 0 ? Lang.T("прибирає") : Lang.T("не чіпає"));
        }

        // its button on the round menu and the ring of kinds under it (each pressed: on or off — the ring stays)
        internal static NpcMenu.Button Button()
        {
            return new NpcMenu.Button { Order = 95, Title = Lang.K("Що прибирати"), Icon = "n-tidy", Shown = a => a.Worker != null && (a.Worker.JobId == "cleaner" || a.Worker.JobId == "servant"),
                Desc = a => { int off = 0; for (int i = 0; i < Kinds.Length; i++) if (!Takes(a, i)) off++; return off == 0 ? Lang.T("Прибирає все. Відкрий, щоб вибрати, чого не чіпати.") : Lang.F("Не чіпає {0} з {1} видів речей.", off, Kinds.Length); },
                Now = a => Off(a) != 0, Sub = a => Ring() };
        }

        private static List<NpcMenu.Button> Ring()
        {
            List<NpcMenu.Button> l = new List<NpcMenu.Button>();
            for (int i = 0; i < Kinds.Length; i++)
            {
                int k = i;
                l.Add(new NpcMenu.Button { Order = i, Title = Kinds[k].Title, Icon = Kinds[k].Icon, Stay = true,
                    Desc = a => Lang.T(Kinds[k].Desc) + "\n" + (Takes(a, k) ? Lang.T("Прибирає. Натисни — не чіпатиме.") : Lang.T("Не чіпає. Натисни — прибиратиме.")),
                    Now = a => Takes(a, k), Do = (a, p) => NpcMenu.OnOwner(a, "tidy", p.transform.position, k.ToString(System.Globalization.CultureInfo.InvariantCulture)) });
            }
            return l;
        }

        // ---------------------------------------------------------------- a station's own make: its worker's
        private const float ByStation = 3.5f, LookAgain = 10f;
        private sealed class Spot { public Vector3 At; public HashSet<string> Made; }
        private static readonly Dictionary<string, KeyValuePair<float, List<Spot>>> Spots = new Dictionary<string, KeyValuePair<float, List<Spot>>>();

        // lying by a station whose worker the base has, and of that station's make
        internal static bool Claimed(NpcBase b, Vector3 me, ItemDrop d)
        {
            if (b == null || d == null) return false;
            List<Spot> spots = SpotsOf(b, me);
            if (spots.Count == 0) return false;
            string name = d.gameObject.name.Replace("(Clone)", "").Trim();
            Vector3 p = d.transform.position;
            foreach (Spot s in spots) if (s.Made.Contains(name) && (p - s.At).sqrMagnitude < ByStation * ByStation) return true;
            return false;
        }

        private static List<Spot> SpotsOf(NpcBase b, Vector3 me)
        {
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<Spot>> had;
            if (Spots.TryGetValue(key, out had) && Time.time - had.Key < LookAgain) return had.Value;
            List<Spot> l = new List<Spot>();
            bool smelter = false, keeper = false;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o.Worker == null || !NpcResident.Is(o) || Utils.DistanceXZ(NpcRules.Home(o), b.Centre) > NpcBase.Reach) continue;
                string j = o.Worker.JobId;
                if (j == "smelter") smelter = true; else if (j == "keeper") keeper = true;
            }
            if (smelter)
                foreach (Smelter s in NpcWorker.InBaseOf<Smelter>(b, me))
                {
                    HashSet<string> made = new HashSet<string>();
                    foreach (Smelter.ItemConversion c in s.m_conversion) if (c != null && c.m_to != null) made.Add(c.m_to.gameObject.name);
                    l.Add(new Spot { At = s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position, Made = made });
                }
            if (keeper)
            {
                foreach (Fermenter f in NpcWorker.InBaseOf<Fermenter>(b, me))
                {
                    HashSet<string> made = new HashSet<string>();
                    foreach (Fermenter.ItemConversion c in f.m_conversion) if (c != null && c.m_to != null) made.Add(c.m_to.gameObject.name);
                    l.Add(new Spot { At = f.m_outputPoint != null ? f.m_outputPoint.position : f.transform.position, Made = made });
                }
                foreach (Beehive h in NpcWorker.InBaseOf<Beehive>(b, me))
                    if (h.m_honeyItem != null) l.Add(new Spot { At = h.m_spawnPoint != null ? h.m_spawnPoint.position : h.transform.position, Made = new HashSet<string> { h.m_honeyItem.gameObject.name } });
            }
            if (Spots.Count > 50) Spots.Clear();
            Spots[key] = new KeyValuePair<float, List<Spot>>(Time.time, l);
            return l;
        }
    }
}
#endif

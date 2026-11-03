#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The thing a person in need lost (NpcRescue, «lost»): not a tool of the game but a keepsake of its own — a necklace,
    // an amber charm, a pearl — one to a cell (never in a stack with another: each is someone's), worth nothing to a
    // trader, its owner's name on it (the game's «made by» line: the person's name). Built as the ravens' items are
    // (RavenItem: a copy of a game item, registered with the game and the network).
    internal static class NpcKeepsake
    {
        internal const string NecklacePrefab = "RavenWatch_LostNecklace", CharmPrefab = "RavenWatch_LostCharm", PearlPrefab = "RavenWatch_LostPearl";
        internal static GameObject Necklace, Charm, Pearl;

        // ...or a weapon of its land: its own copy («Загублена сокира»), a weapon still, never sold, its owner on it
        private static readonly KeyValuePair<Heightmap.Biome, string>[] WeaponOf =
        {
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Meadows, "AxeFlint"), new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.BlackForest, "KnifeCopper"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Swamp, "AxeBronze"), new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Mountain, "KnifeSilver"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Plains, "KnifeBlackMetal"), new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.Mistlands, "KnifeSkollAndHati"),
            new KeyValuePair<Heightmap.Biome, string>(Heightmap.Biome.AshLands, "KnifeSkollAndHati"),
        };
        private static readonly Dictionary<string, GameObject> Weapons = new Dictionary<string, GameObject>(); // (the game's prefab name → our copy)

        internal static IEnumerable<GameObject> All()
        {
            foreach (GameObject g in new GameObject[] { Necklace, Charm, Pearl }) yield return g;
            foreach (GameObject g in Weapons.Values) yield return g;
        }

        internal static void Build(Func<string, GameObject> find, Func<GameObject, string, GameObject> clone)
        {
            if (Necklace == null) Necklace = Make(clone(find("SilverNecklace"), NecklacePrefab), "rw_item_lost_necklace", "Загублене намисто",
                "Чиєсь срібне намисто — тонка робота, потерта від того, що його довго носили. Хтось за ним дуже сумує. Ім'я власника — у підказці до речі.");
            if (Charm == null) Charm = Make(clone(find("Amber"), CharmPrefab), "rw_item_lost_charm", "Загублений оберіг",
                "Бурштиновий оберіг на шкіряному шнурку. Усередині застигла комашка — кажуть, на щастя. Ім'я власника — у підказці до речі.");
            if (Pearl == null) Pearl = Make(clone(find("AmberPearl"), PearlPrefab), "rw_item_lost_pearl", "Загублена перлина",
                "Перлина з дірочкою для нитки: колись її носили на шиї. Для когось вона дорожча за все золото. Ім'я власника — у підказці до речі.");
            foreach (KeyValuePair<Heightmap.Biome, string> w in WeaponOf)
            {
                if (Weapons.ContainsKey(w.Value)) continue;
                bool axe = w.Value.StartsWith("Axe");
                GameObject g = Make(clone(find(w.Value), "RavenWatch_Lost" + w.Value), "rw_item_lost_" + w.Value.ToLowerInvariant(), axe ? "Загублена сокира" : "Загублений ніж",
                    axe ? "Чиясь сокира — руків'я витерте до блиску чужою долонею. Рубати нею можна, але власник її шукає. Ім'я власника — у підказці до речі."
                        : "Чийсь ніж — на руків'ї зарубки, ніби хтось рахував дні. Різати ним можна, але власник його шукає. Ім'я власника — у підказці до речі.", true);
                if (g != null) Weapons[w.Value] = g;
            }
        }

        private static GameObject Make(GameObject go, string key, string name, string desc, bool weapon = false)
        {
            if (go == null) return null;
            ItemDrop.ItemData.SharedData sh = go.GetComponent<ItemDrop>().m_itemData.m_shared;
            sh.m_name = Lang.Word(key, name);
            sh.m_description = Lang.Word(key + "_desc", desc);
            if (!weapon) sh.m_itemType = ItemDrop.ItemData.ItemType.Misc; // (a weapon stays a weapon)
            sh.m_maxStackSize = 1;   // (each is someone's own: never one stack)
            sh.m_value = 0;          // (not for a trader)
            if (!weapon) sh.m_weight = 0.1f; // (a weapon weighs what it weighs)
            sh.m_teleportable = true;
            sh.m_questItem = false;
            return go;
        }

        // one for a person to lose: half the time a keepsake, else the weapon of the land it is in
        internal static GameObject Any(Heightmap.Biome land)
        {
            if (UnityEngine.Random.value < 0.5f)
                foreach (KeyValuePair<Heightmap.Biome, string> w in WeaponOf) { GameObject g; if (w.Key == land && Weapons.TryGetValue(w.Value, out g) && g != null) return g; }
            List<GameObject> l = new List<GameObject>();
            foreach (GameObject g in new GameObject[] { Necklace, Charm, Pearl }) if (g != null) l.Add(g);
            return l.Count > 0 ? l[UnityEngine.Random.Range(0, l.Count)] : null;
        }
    }
}
#endif

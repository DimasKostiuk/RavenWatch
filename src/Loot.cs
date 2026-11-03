#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // «Здобич» (the user, 2026-10-22): a raven back from its own outing (Outing, run its time) or visit (WorldVisit, run
    // its time) now and then brings you something to eat, from the land you stand in — Hugin what grows (berries,
    // mushrooms, honey, roots: only what can be eaten), Munin meat and a small fish (fish no heavier than FishMax). Rarely
    // either brings a treasure (coins, amber). It comes in the beak (RavenFetch.Hold) and goes into your bag once the
    // raven sits on you again.
    //   - Chance a time (× its character, «Сорока»/«Злодюжка», the boons «Підбирач»/«Стерв'ятник»/«Мисливець»), at most
    //     PerDay a game day a raven (kept in the raven). Trust 5 and up: one more piece.
    //   - Not hungry, not in a fight, not underground or under a roof; «Сонько» brings nothing at night.
    //   - Munin may eat the meat himself on the way (MeatEaten): you get only his word on it.
    // Cheap by design: no clock of its own, no looking about — one roll when an outing or a visit is over anyway, a short
    // table by the land; the network is the beak's one string RavenFetch has already; the words ask the director.
    internal static class RavenLoot
    {
        internal static float Chance = 0.25f, MeatEaten = 0.15f, FishMax = 2.5f;
        internal static int PerDay = 3;
        internal const string Key = "RavenWatch_loot"; // the raven's: "day;n" brought this game day

        private sealed class Find { public string Prefab; public int Min, Max; public bool Fish; public ItemDrop.ItemData.SharedData Shared; } // Shared: the game's (Ready)
        private static Find F(string prefab, int min, int max) { return new Find { Prefab = prefab, Min = min, Max = max, Fish = prefab.StartsWith("Fish") }; }

        // what grows and runs in each land (made good once the game's items are there: Ready)
        private static readonly Dictionary<Heightmap.Biome, Find[]> HuginSrc = new Dictionary<Heightmap.Biome, Find[]>
        {
            { Heightmap.Biome.Meadows,    new[] { F("Raspberry", 1, 3), F("Mushroom", 1, 2), F("Honey", 1, 1) } },
            { Heightmap.Biome.BlackForest, new[] { F("Blueberries", 1, 3), F("MushroomYellow", 1, 2), F("Carrot", 1, 2), F("Raspberry", 1, 2) } },
            { Heightmap.Biome.Swamp,      new[] { F("MushroomYellow", 1, 2), F("Blueberries", 1, 2), F("Turnip", 1, 2) } },
            { Heightmap.Biome.Mountain,   new[] { F("Onion", 1, 2), F("MushroomBlue", 1, 1) } },
            { Heightmap.Biome.Plains,     new[] { F("Cloudberry", 1, 3), F("Honey", 1, 1) } },
            { Heightmap.Biome.Mistlands,  new[] { F("MushroomJotunPuffs", 1, 2), F("MushroomMagecap", 1, 2), F("RoyalJelly", 1, 2) } },
            { Heightmap.Biome.AshLands,   new[] { F("Vineberry", 1, 3), F("MushroomSmokePuff", 1, 2), F("Fiddleheadfern", 1, 2) } },
        };
        private static readonly Dictionary<Heightmap.Biome, Find[]> MuninSrc = new Dictionary<Heightmap.Biome, Find[]>
        {
            { Heightmap.Biome.Meadows,    new[] { F("RawMeat", 1, 1), F("DeerMeat", 1, 1), F("NeckTail", 1, 1), F("Fish1", 1, 1) } },
            { Heightmap.Biome.BlackForest, new[] { F("RawMeat", 1, 1), F("DeerMeat", 1, 1), F("Fish2", 1, 1) } },
            { Heightmap.Biome.Swamp,      new[] { F("NeckTail", 1, 1), F("Fish5", 1, 1) } },
            { Heightmap.Biome.Mountain,   new[] { F("WolfMeat", 1, 1), F("Fish4_cave", 1, 1) } },
            { Heightmap.Biome.Plains,     new[] { F("LoxMeat", 1, 1), F("DeerMeat", 1, 1), F("Fish7", 1, 1) } },
            { Heightmap.Biome.Mistlands,  new[] { F("HareMeat", 1, 1), F("BugMeat", 1, 1), F("Fish9", 1, 1) } },
            { Heightmap.Biome.AshLands,   new[] { F("AsksvinMeat", 1, 1), F("VoltureMeat", 1, 1), F("Fish11", 1, 1) } },
        };
        // a treasure instead, rarely (either raven, anywhere): the chance each
        private static readonly Find[] Rare = { F("Coins", 3, 15), F("Amber", 1, 1), F("AmberPearl", 1, 1) };
        private static readonly float[] RareChance = { 0.03f, 0.01f, 0.005f };

        private static Dictionary<Heightmap.Biome, List<Find>> _hugin, _munin;

        // the tables as the game has them: Hugin's only what can be eaten, Munin's fish only the small ones; an item the
        // game does not know is left out (said once in the log)
        private static bool Ready()
        {
            if (_hugin != null) return true;
            if (ObjectDB.instance == null || ObjectDB.instance.m_items == null || ObjectDB.instance.m_items.Count == 0) return false;
            _hugin = Make(HuginSrc, false);
            _munin = Make(MuninSrc, true);
            foreach (Find t in Rare) t.Shared = Shared(t.Prefab); // (one the game does not know stays null: not brought)
            return true;
        }

        private static Dictionary<Heightmap.Biome, List<Find>> Make(Dictionary<Heightmap.Biome, Find[]> src, bool munin)
        {
            Dictionary<Heightmap.Biome, List<Find>> made = new Dictionary<Heightmap.Biome, List<Find>>();
            List<string> left = new List<string>();
            foreach (KeyValuePair<Heightmap.Biome, Find[]> kv in src)
            {
                List<Find> l = new List<Find>();
                foreach (Find f in kv.Value)
                {
                    ItemDrop.ItemData.SharedData s = Shared(f.Prefab);
                    bool ok = s != null && (munin ? (!f.Fish || s.m_weight <= FishMax) : s.m_food > 0f);
                    if (ok) { f.Shared = s; l.Add(f); } else if (!left.Contains(f.Prefab)) left.Add(f.Prefab);
                }
                made[kv.Key] = l;
            }
            if (left.Count > 0 && Plugin.Instance != null) Plugin.Instance.Log("Raven loot: " + (munin ? "Munin" : "Hugin") + " leaves out " + string.Join(", ", left.ToArray()) + " (unknown, not food, or a fish too heavy)");
            return made;
        }

        private static ItemDrop.ItemData.SharedData Shared(string prefab)
        {
            GameObject go = ObjectDB.instance.GetItemPrefab(prefab);
            ItemDrop d = go != null ? go.GetComponent<ItemDrop>() : null;
            return d != null ? d.m_itemData.m_shared : null;
        }

        // From Outing.Tick and WorldVisit.Tick: an outing or a visit ran its time. True when it brought (or ate) something
        internal static bool After(Player p, bool munin) { return After(p, munin, false); }

        private static bool After(Player p, bool munin, bool sure)
        {
            if (p == null || p.IsDead() || !ReferenceEquals(p, Player.m_localPlayer) || !Ready()) return false;
            if (!sure && (Senses.Fight || Senses.Underground || Senses.Indoors)) return false;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null || Satiety.Zone(item) <= Satiety.Hungry || RavenFetch.Carrying(munin)) return false;
            if (!sure && Quirks.AsleepForScout(p, munin)) return false; // («Сонько» at night: asleep on the wing)
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0, n = Count(item, day);
            if (!sure && (n >= PerDay || Random.value >= Chance * Mul(item, munin))) return false;
            // what: a treasure now and then, else what the land gives
            bool magpie = Norov.HasQuirk(item, "magpie");
            Find f = null;
            float r = Random.value;
            for (int i = 0; i < Rare.Length; i++) { float c = RareChance[i] * (magpie ? 2f : 1f) * BreedTraits.RareMulOf(item); if (r < c) { f = Rare[i]; break; } r -= c; }
            if (f != null && f.Shared == null) f = null; // (a treasure the game does not know: the land's find instead)
            bool rare = f != null;
            if (f == null)
            {
                List<Find> l;
                if (!(munin ? _munin : _hugin).TryGetValue(Senses.Biome, out l) || l.Count == 0) return false;
                f = l[Random.Range(0, l.Count)];
            }
            int count = Random.Range(f.Min, f.Max + 1) + (!rare && f.Max > 1 && Trust.LevelWorn(munin) >= 5 ? 1 : 0);
            string thing = Lang.Game(f.Shared.m_name);
            bool eaten = munin && !rare && !f.Fish && Random.value < MeatEaten; // (on the way: gone)
            if (!eaten && !RavenFetch.Hold(p, munin, f.Prefab, count)) return false;
            if (!sure) item.m_customData[Key] = day.ToString(CultureInfo.InvariantCulture) + ";" + (n + 1).ToString(CultureInfo.InvariantCulture); // (the console's own does not count)
            Speak(p, munin, eaten ? "loot_eaten" : rare ? "loot_rare" : munin ? (f.Fish ? "loot_fish" : "loot_meat") : "loot_food", thing);
            return true;
        }

        private static int Count(ItemDrop.ItemData item, int day)
        {
            string raw; int d, n;
            if (!item.m_customData.TryGetValue(Key, out raw) || string.IsNullOrEmpty(raw)) return 0;
            string[] f = raw.Split(';');
            return f.Length == 2 && int.TryParse(f[0], out d) && d == day && int.TryParse(f[1], out n) ? n : 0;
        }

        // how keen it is on bringing things: its character, its quirk, the boons for it
        private static float Mul(ItemDrop.ItemData item, bool munin)
        {
            float m = 1f;
            Norov.Character c = Norov.CharOf(item);
            if (c != null) m *= c.Id == "curious" ? 1.4f : c.Id == "wanderer" ? 1.3f : c.Id == "fidget" || c.Id == "huntsman" ? 1.2f : c.Id == "sweettooth" ? 1.1f : c.Id == "tender" ? 0.9f : c.Id == "homebody" ? 0.7f : c.Id == "calm" ? 0.8f : 1f;
            if (Norov.HasQuirk(item, "magpie")) m *= 1.2f;
            if (Norov.HasQuirk(item, "thief")) m *= 1.3f;
            if (!munin && Perks.Has("picker")) m *= 1.5f;
            if (munin && (Perks.Has("scav") || Perks.Has("hunter"))) m *= 1.5f;
            return m * BreedTraits.LootMulOf(item) * Habits.LootMul(item); // (the breed «Строкатий»; «Худорба», «Мисливець»)
        }

        // its word on it, if the director lets it (else only the corner line when it is given)
        private static void Speak(Player p, bool munin, string tag, string thing)
        {
            if (!Director.May(Director.Voice.Event)) return;
            string line = Satiety.Say(p, munin, tag, "thing", thing);
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, munin, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }

        // the console (raven loot [munin]): a find now, as if an outing just ended (no chance, no day's limit)
        internal static string Force(Player p, bool munin)
        {
            if (!Ready()) return Lang.T("Предмети гри ще не готові.");
            return After(p, munin, true) ? Lang.F("{0} щось несе (біом: {1}).", Plugin.RavenName(munin), Senses.Biome) : Lang.T("Нічого: ворон не на плечі, голодний, уже несе щось, або в цьому біомі нема здобичі.");
        }
    }
}
#endif

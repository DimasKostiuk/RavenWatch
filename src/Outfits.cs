#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Things a raven can wear: the game's head pieces (on its head). Necklaces are off until they are made anew:
    // the neck slot is still in the data ("head|neck") and in the dressing code, but no piece is listed for it.
    // A head piece is the item's own "attach" model, the one the game hangs on a hero's head bone; the ones the game
    // stretches over the hero's skin instead ("attach_skin") cannot fit a raven and are left out. What each raven wears
    // is kept in the raven item, and told to every game through the player's net data, so everyone sees the same.
    // How a piece sits (size, place, turn) comes from config/RavenWatch/outfits.txt: one line for all head pieces,
    // and a line for any single piece tuned in the book.
    //
    // The things for a raven made in the Workshop (RavenGear: its back, its neck, its legs) are worn the same way, in
    // the slots Neck, Back and Legs: listed, put on, fitted in the book and told to the others alike.
    internal static class Outfits
    {
        internal const int Head = 0, Neck = 1, Back = 2, Legs = 3, Slots = 4;
        internal const string KeyH = "RavenWatch_OutfitH", KeyM = "RavenWatch_OutfitM"; // on the player's ZDO: "head|neck|back|legs"
        private static readonly string[] DataKey = { "RavenWatch_OutfitHead", "RavenWatch_OutfitNeck", "RavenWatch_OutfitBack", "RavenWatch_OutfitLegs" }; // in the raven item
        internal static string[] SlotTitles { get { return Lang.Arr(SlotTitlesSrc); } } // (translated on every read)
        private static readonly string[] SlotTitlesSrc = Lang.Keys("На голову", "На шию", "На спину", "На лапи");
        internal static string[] SlotWhere { get { return Lang.Arr(SlotWhereSrc); } } // (translated on every read)
        private static readonly string[] SlotWhereSrc = Lang.Keys("голова", "шия", "спина", "лапи");
        internal static bool VanillaOnly = true;
        internal static int Version = 1; // bumped when a fit changes: every raven dresses again

        internal sealed class Entry
        {
            public string Prefab, NameSrc; // the game's word ($item_…) or our source text: shown in the current language
            public string Name { get { return string.IsNullOrEmpty(NameSrc) ? NameSrc : NameSrc[0] == '$' ? (Localization.instance != null ? Lang.Game(NameSrc) : NameSrc) : Lang.T(NameSrc); } set { NameSrc = value; } }
            public int Slot;
            public Sprite Icon;
            public GameObject Source; // the "attach" child, or the item itself for a necklace
            public RavenGear.Item Gear; // a thing from the Workshop (then no Source)
        }

        internal static Sprite IconOf(Entry e) { return e == null ? null : e.Gear != null ? RavenGear.IconOf(e.Gear) : e.Icon; }

        internal static int SlotOfGear(string slot) { return slot == RavenGear.Legs ? Legs : slot == RavenGear.Neck ? Neck : slot == RavenGear.Head ? Head : Back; }

        // a hat of our own (made in the Workshop): only for looks, opened like the game's hats (top level, Odin's gifts)
        internal static bool OwnHat(Entry e) { return e != null && e.Gear != null && e.Slot == Head; }

        // A head piece: size in the raven's head, place in head lengths. A thing from the Workshop: size × its own,
        // place in metres and turns in degrees in the Workshop's frame, and its size along each axis (SX, SY, SZ).
        internal sealed class Fit
        {
            public float S = 1f, X, Y, Z, RX, RY, RZ, SX = 1f, SY = 1f, SZ = 1f;
            public bool Ok; // "Готово" in the book: tuned and approved, to be taken into the mod
            public Fit Copy() { return (Fit)MemberwiseClone(); }
            public Fit(float s, float x, float y, float z, float rx, float ry, float rz) { S = s; X = x; Y = y; Z = z; RX = rx; RY = ry; RZ = rz; }
            public Fit() { }
        }

        // How the pieces sit in the released mod: tuned in game, approved ("ok" in outfits.txt), then written here.
        // A line in outfits.txt still wins over these, so a piece can be tuned again.
        private static readonly Dictionary<string, Fit> Builtin = new Dictionary<string, Fit>
        {
            { "head", new Fit(1f, 0f, 0.25f, 0f, 0f, 0f, 0f) }, // every head piece without its own line
            { "neck", new Fit(0.6f, 0f, 0f, 0f, 0f, 0f, 0f) },  // every necklace without its own line
            // tuned in game 2026-10-07 (size, x, y, z, tilt, turn, roll)
            { "HelmetBerserkerUndead", new Fit(1.464f, 0f, 0.75f, -0.05f, -20f, 0f, 0f) },
            { "HelmetBronze",          new Fit(1.21f, 0f, 0.4f, 0.1f, -30f, 0f, 0f) },
            { "HelmetIron",            new Fit(1.21f, 0f, 0.4f, 0.1f, -30f, 0f, 0f) },   // as the bronze one
            { "HelmetCelebration",     new Fit(1.464f, 0f, 0.25f, 0f, -10f, 0f, 0f) },
            { "HelmetCrownofValheim",  new Fit(1.331f, 0f, 0.4f, 0.1f, -30f, 0f, 0f) },
            { "HelmetDNMage",          new Fit(1.464f, 0f, 0.4f, 0.05f, -50f, 0f, 0f) },
            { "HelmetDrake",           new Fit(1.464f, 0f, 0.3f, 0.1f, -30f, 0f, 0f) },
            { "HelmetDverger",         new Fit(1.1f, 0f, 0.45f, 0.15f, 0f, 0f, 0f) },
            { "HelmetFishingHat",      new Fit(1.21f, 0f, 0.35f, 0.05f, -30f, 0f, 0f) },
            { "HelmetHat4",            new Fit(1.772f, 0f, 0.3f, 0f, -10f, 0f, 0f) },    // extravagant green cap
            { "HelmetHat9",            new Fit(1.772f, 0f, 0.3f, 0f, -10f, 0f, 0f) },    // extravagant orange cap, as the green one
            { "HelmetHat7",            new Fit(1.464f, 0f, 0.3f, 0.05f, -10f, 0f, 0f) },
            { "HelmetMidsummerCrown",  new Fit(1f, 0f, 0.35f, 0.05f, -10f, 0f, 0f) },
            { "HelmetPointyHat",       new Fit(1f, 0f, 0.35f, 0f, -40f, 0f, 0f) },
            { "HelmetRootCrown",       new Fit(1.464f, 0f, 0.3f, 0.15f, -30f, 0f, 0f) },
            { "HelmetStrawHat",        new Fit(1.21f, 0f, 0.45f, 0.1f, -10f, 0f, 0f) },
            { "HelmetSweatBand",       new Fit(1.331f, 0f, 0.3f, 0.1f, -20f, 0f, 0f) },
            { "HelmetYule",            new Fit(1.331f, 0f, 0.4f, 0.05f, -20f, 0f, 0f) },
            { "HelmetHat3",            new Fit(1.331f, 0f, 0.4f, 0.1f, 0f, 0f, 0f) },    // brown fur cap
            { "HelmetHat8",            new Fit(1.331f, 0f, 0.35f, 0.1f, -10f, 0f, 0f) }, // grey fur cap
            { "HelmetHat10",           new Fit(1.331f, 0f, 0.35f, 0.1f, 0f, 0f, 0f) },   // simple purple cap
            { "HelmetCarapace",        new Fit(1.772f, 0f, 0.45f, -0.05f, -30f, 0f, 0f) },
            { "SilverNecklace",        new Fit(0.878f, 0f, 0f, 0.3f, 60f, 0f, 0f) },     // the game's silver necklace, on the neck
            // the first things from the raven tables and our own hats, tuned in game 2026-10-13/14 (metres, degrees, size per axis)
            { "gear_moneta_na_udachu", new Fit(1f, 0f, 0.005f, 0.004f, 5f, 0f, 0f) { SX = 1f, SY = 1f, SZ = 0.587f } },
            { "gear_sumka_dlia_hoduvannia", new Fit(1f, 0f, -0.105f, -0.042f, 5f, 0f, 1f) { SX = 1.093f, SY = 0.571f, SZ = 1.061f } },
            { "gear_holova_valkirii", new Fit(0.623f, 0f, -0.02f, 0.005f, 35f, 0f, 0f) },
            { "gear_kihti_komara", new Fit(1f, -0.01f, 0.002f, -0.02f, 0f, 0f, 0f) { SX = 1f, SY = 1f, SZ = 0.971f } },
            { "gear_spiral_zakhystu", new Fit(1f, 0f, 0.03f, -0.003f, -13f, 0f, 0f) { SX = 1f, SY = 1.081f, SZ = 0.701f } },
            { "gear_holova_iahluta", new Fit(0.642f, 0.008f, -0.001f, -0.03f, -26f, 0f, -4f) },
            { "gear_kaptur_avanhardu", new Fit(0.396f, 0.02f, 0.01f, 0.01f, 29f, 0f, 0f) { SX = 0.837f, SY = 1.974f, SZ = 0.863f } },
            { "gear_kaptur_odina", new Fit(0.388f, -0.057f, 0f, -0.01f, 10f, 1f, 15f) { SX = 1.23f, SY = 1.093f, SZ = 1.305f } },
            { "gear_dzvinkyi_oshyinyk", new Fit(0.699f, 0f, 0.035f, 0.028f, 16f, 0f, -2f) },
            { "gear_zakhysnyi_osheinyk", new Fit(0.811f, 0f, 0.026f, 0.004f, 6f, 0f, 0f) },
            { "gear_kihtiaste_namysto", new Fit(0.388f, 0f, -0.005f, 0.045f, -30f, 0f, 0f) { SX = 1f, SY = 1.194f, SZ = 1f } },
            { "gear_kholodnyi_medalion", new Fit(0.751f, 0f, -0.39f, 0.1f, -8f, 0f, 0f) { SX = 1.513f, SY = 1.093f, SZ = 1.126f } },
            { "gear_korona_vidom", new Fit(0.407f, 0.003f, 0.007f, 0f, -2f, -1f, -6f) { SX = 0.78f, SY = 1.161f, SZ = 0.836f } },
        };

        // The wardrobe grows with trust: the raven lets itself be dressed from level 3, first in plain caps, then in
        // helmets, the crown of Valheim when it trusts you almost fully. Every other piece is Odin's gift: coming back
        // from him after a ritual (to levels 3-6) the raven wears one, picked at random from that level's pool
        // (GiftPool), and keeps it. Nothing opens all at once at the top: each raven ends with its own few gifts, and
        // the book never shows what it could have had. All of it is kept in the raven item.
        internal const int WardrobeLevel = 3;
        internal const int TopLevel = 7;
        private static readonly Dictionary<string, int> UnlockAt = new Dictionary<string, int>
        {
            { "HelmetHat4", 3 }, { "HelmetHat9", 3 }, { "HelmetHat10", 3 }, { "HelmetHat7", 3 }, { "HelmetSweatBand", 3 }, // everyday caps
            { "HelmetBronze", 4 }, { "HelmetIron", 4 },                                                                    // battle helmets
            { "HelmetDrake", 5 }, { "HelmetCarapace", 5 },
            { "HelmetCrownofValheim", 6 },                                                                                  // it trusts you almost fully
        };

        // What Odin may give on reaching each level (one piece a ritual, one it has not got). A level whose pool this
        // raven has had all of takes from the levels below. The top level has none for now (more later).
        private static readonly Dictionary<string, int> GiftPool = new Dictionary<string, int>
        {
            // plain and festive
            { "HelmetStrawHat", 3 }, { "HelmetFishingHat", 3 }, { "HelmetHat3", 3 }, { "HelmetHat8", 3 },
            { "HelmetCelebration", 3 }, { "HelmetMidsummerCrown", 3 }, { "HelmetPointyHat", 3 }, { "HelmetYule", 3 },
            // battle
            { "HelmetDverger", 4 }, { "HelmetBerserkerUndead", 4 }, { "HelmetRootCrown", 4 },
            // trophies: the heads (no matter whom you have killed)
            { RavenGear.KeyPrefix + "holova_iahluta", 5 }, { RavenGear.KeyPrefix + "holova_valkirii", 5 },
            // magic
            { "HelmetDNMage", 6 }, { RavenGear.KeyPrefix + "korona_vidom", 6 }, { RavenGear.KeyPrefix + "kaptur_odina", 6 },
        };
        private const string GiftsKey = "RavenWatch_OutfitGifts"; // Odin's gifts this raven got, comma-separated
        private const string NewGiftKey = "RavenWatch_OutfitNew";  // the gift it brings on this trip, told when it is back

        // Tuning (every piece open, the fitting buttons) is for admins only: a player's own config cannot open the wardrobe.
        internal static bool TuningWanted; // config "Outfits all for tuning"
        internal static bool Tuning { get { return TuningWanted && !PlayerView && Plugin.IsLocalAdmin(); } }
        internal static bool TuningAllowed { get { return TuningWanted && Plugin.IsLocalAdmin(); } }
        // the book's «Спорядження» tab shows (and works) as a player sees it, tuning or not; the fitting has its own tab
        internal static bool PlayerView;

        // in the book at all: a finished piece (or any piece while tuning)
        internal static bool Listed(Entry e) { return e != null && (Tuning || Builtin.ContainsKey(e.Prefab) || (e.Gear != null && (e.Gear.BuiltIn || Perches.FromFiles))); }

        // This raven may wear this piece now. The level counts once the raven is back from Odin (a ritual's new
        // level opens its pieces on its return); Odin's gifts are kept in the raven.
        internal static bool IsOpen(ItemDrop.ItemData item, Entry e)
        {
            if (e == null) return false;
            if (Tuning) return true;
            // a thing made at the raven table: worn from the wardrobe's level on, once this raven has been given it
            if (OwnHat(e))
            {
                if (!e.Gear.BuiltIn) return false;
                int lv = Trust.SettledLevel(item), at;
                if (UnlockAt.TryGetValue(e.Prefab, out at)) return lv >= at;
                return Gifts(item).Contains(e.Prefab);
            }
            if (e.Gear != null) return (e.Gear.BuiltIn || Perches.FromFiles) && Trust.SettledLevel(item) >= GearFx.WearLevel(e.Prefab) && RavenGear.Owned(item, e.Gear);
            if (!Builtin.ContainsKey(e.Prefab)) return false; // not (yet) found to suit a raven
            int level = Trust.SettledLevel(item);
            int l;
            if (UnlockAt.TryGetValue(e.Prefab, out l)) return level >= l;
            return Gifts(item).Contains(e.Prefab);
        }

        private static List<string> Gifts(ItemDrop.ItemData item)
        {
            string v;
            List<string> l = new List<string>();
            if (item != null && item.m_customData != null && item.m_customData.TryGetValue(GiftsKey, out v))
                foreach (string s in v.Split(',')) if (s.Trim().Length > 0) l.Add(s.Trim());
            return l;
        }

        // The raven flies off to Odin after a ritual (Trust): at levels 3-6 Odin picks a piece it does not have yet,
        // from the new level's pool (GiftPool; that pool all had: the nearest level below with something left).
        // Only picked here: it is given and put on when the raven is back (TakeNewGift), so the book shows nothing early.
        internal static void OdinGift(bool munin, ItemDrop.ItemData item)
        {
            if (item == null) return;
            int level = Trust.Level(item); // the new level: the gift is for reaching it
            if (level < WardrobeLevel || level >= TopLevel) return;
            List<string> have = Gifts(item);
            List<string> pool = new List<string>();
            for (int lv = level; lv >= WardrobeLevel && pool.Count == 0; lv--)
                foreach (KeyValuePair<string, int> kv in GiftPool)
                {
                    if (kv.Value != lv || have.Contains(kv.Key)) continue;
                    Entry e = Find(kv.Key);
                    if (e == null || !(Builtin.ContainsKey(kv.Key) || (OwnHat(e) && e.Gear.BuiltIn))) continue; // (released pieces only)
                    pool.Add(kv.Key);
                }
            if (pool.Count == 0) return;
            string gift = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[NewGiftKey] = gift;
            if (Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(munin) + " will get " + gift + " from Odin (level " + level + ")");
        }

        // the trust level a piece opens on; 0: it comes only as Odin's gift (or with the top level)
        internal static int UnlockLevel(Entry e)
        {
            int l;
            return e != null && UnlockAt.TryGetValue(e.Prefab, out l) ? l : 0;
        }

        // The book's "new" mark: pieces open to this raven that the book has not shown yet.
        private const string SeenKey = "RavenWatch_OutfitSeen";

        private static List<string> Seen(ItemDrop.ItemData item)
        {
            string v;
            List<string> l = new List<string>();
            if (item != null && item.m_customData != null && item.m_customData.TryGetValue(SeenKey, out v))
                foreach (string s in v.Split(',')) if (s.Trim().Length > 0) l.Add(s.Trim());
            return l;
        }

        internal static bool IsNew(ItemDrop.ItemData item, Entry e)
        {
            return item != null && !Tuning && IsOpen(item, e) && !Seen(item).Contains(e.Prefab);
        }

        // as the book's page lists them: the head pieces it may wear; a thing from the raven table only while worn or in
        // your bag (one left in a chest is not on the page, so it must not light the tab's dot either)
        internal static bool OnPage(Player p, ItemDrop.ItemData item, Entry e)
        {
            if (!Listed(e)) return false;
            if (e.Slot != Head && e.Gear != null && !OwnHat(e)) return Tuning || Worn(item, e.Slot) == e.Prefab || (p != null && CountInBag(p, e) > 0);
            return IsOpen(item, e);
        }

        // the new pieces the page shows now (for the dots and the tab's hover)
        internal static List<Entry> NewOnPage(Player p, ItemDrop.ItemData item)
        {
            List<Entry> l = new List<Entry>();
            if (item == null || Tuning || Trust.SettledLevel(item) < WardrobeLevel) return l;
            List<string> seen = Seen(item);
            foreach (Entry e in Catalog()) if (!seen.Contains(e.Prefab) && IsOpen(item, e) && OnPage(p, item, e)) l.Add(e);
            return l;
        }

        internal static bool AnyNew(Player p, ItemDrop.ItemData item) { return NewOnPage(p, item).Count > 0; }

        // A raven from before the "new" marks existed: what it could already wear is not news (no dot on every piece
        // at once); a raven still below the wardrobe level gets an empty list, so its first pieces do come as new.
        internal static void SeenStart(ItemDrop.ItemData item)
        {
            if (item == null || Tuning) return;
            if (item.m_customData != null && item.m_customData.ContainsKey(SeenKey)) return;
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[SeenKey] = "";
            if (Trust.SettledLevel(item) >= WardrobeLevel) MarkSeen(item);
        }

        internal static void MarkSeen(ItemDrop.ItemData item)
        {
            if (item == null || Tuning) return;
            List<string> seen = Seen(item);
            bool changed = false;
            foreach (Entry e in Catalog()) if (Listed(e) && IsOpen(item, e) && !seen.Contains(e.Prefab)) { seen.Add(e.Prefab); changed = true; }
            if (!changed) return;
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            item.m_customData[SeenKey] = string.Join(",", seen.ToArray());
        }

        // back from Odin: the gift is now its own and worn; its name for the message (once)
        internal static string TakeNewGift(ItemDrop.ItemData item)
        {
            string v;
            if (item == null || item.m_customData == null || !item.m_customData.TryGetValue(NewGiftKey, out v)) return null;
            item.m_customData.Remove(NewGiftKey);
            Entry e = Find(v);
            if (e == null) return null;
            List<string> have = Gifts(item);
            if (!have.Contains(v)) have.Add(v);
            item.m_customData[GiftsKey] = string.Join(",", have.ToArray());
            item.m_customData[DataKey[e.Slot]] = v; // worn from now on
            return e.Name;
        }
        private static List<Entry> _catalog;
        private static ObjectDB _catalogDb; // the item list it was made from (a new one with every world)
        private static readonly Dictionary<string, Entry> ByPrefab = new Dictionary<string, Entry>();
        private static readonly HashSet<string> Vanilla = new HashSet<string>();
        private static Dictionary<string, Fit> _fits;
        private static int _gearSeen = -1;

        // ---------------------------------------------------------------- what there is

        // the game's own items, before any mod adds its own (ObjectDB.Awake, first thing)
        internal static void Snapshot(ObjectDB db)
        {
            if (Vanilla.Count > 0 || db == null || db.m_items == null) return;
            foreach (GameObject g in db.m_items) if (g != null) Vanilla.Add(g.name);
        }

        internal static List<Entry> Catalog()
        {
            ObjectDB db = ObjectDB.instance;
            List<RavenGear.Item> gear = RavenGear.Items(); // (kept: read anew only when the Workshop saved or tuning changed)
            if (_catalog != null && (db == null || db == _catalogDb) && _gearSeen == RavenGear.Version) return _catalog; // made once (Find asks twice a second)
            _gearSeen = RavenGear.Version;
            _catalog = new List<Entry>();
            ByPrefab.Clear();
            _catalogDb = db;
            if (db == null) return _catalog;
            List<string> skinned = new List<string>();
            foreach (GameObject g in ObjectDB.instance.m_items)
            {
                if (g == null) continue;
                if (VanillaOnly && Vanilla.Count > 0 && !Vanilla.Contains(g.name)) continue;
                ItemDrop drop = g.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                ItemDrop.ItemData.SharedData sh = drop.m_itemData.m_shared;
                Entry e = null;
                if (sh.m_itemType == ItemDrop.ItemData.ItemType.Helmet)
                {
                    Transform attach = null, skin = null;
                    for (int i = 0; i < g.transform.childCount; i++)
                    {
                        Transform c = g.transform.GetChild(i);
                        if (c.name == "attach") attach = c; else if (c.name == "attach_skin") skin = c;
                    }
                    if (attach == null) { if (skin != null) skinned.Add(g.name); continue; }
                    e = new Entry { Slot = Head, Source = attach.gameObject };
                }
                if (e == null) continue;
                e.Prefab = g.name;
                e.Name = sh.m_name; // (the word: localized when shown, so a new language shows at once)
                e.Icon = sh.m_icons != null && sh.m_icons.Length > 0 ? sh.m_icons[0] : null;
                _catalog.Add(e);
                ByPrefab[e.Prefab] = e;
            }
            // the game's own copies of a piece under a prefix (FW_HelmetBronze, SP_HelmetBronze: the same bronze helmet)
            List<string> copies = new List<string>();
            _catalog.RemoveAll(e =>
            {
                int u = e.Prefab.IndexOf('_');
                if (u <= 0 || u >= e.Prefab.Length - 1 || !ByPrefab.ContainsKey(e.Prefab.Substring(u + 1))) return false;
                copies.Add(e.Prefab);
                ByPrefab.Remove(e.Prefab);
                return true;
            });
            // the things from the Workshop, each in its slot
            foreach (RavenGear.Item it in gear)
            {
                Entry g = new Entry { Prefab = it.Key, NameSrc = it.NameSrc, Slot = SlotOfGear(it.Slot), Gear = it };
                _catalog.Add(g);
                ByPrefab[g.Prefab] = g;
            }
            Version++; // a model may have changed: every raven dresses again
            _catalog.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            if (Plugin.Instance != null)
            {
                StringBuilder sb = new StringBuilder("Raven outfits (" + (VanillaOnly ? "game's own items only" : "all items") + ", " + Vanilla.Count + " game items known):");
                foreach (Entry e in _catalog) sb.Append(' ').Append(e.Prefab).Append(e.Slot == Neck ? "(neck)" : "");
                if (copies.Count > 0) sb.Append(". Left out, copies of other pieces: ").Append(string.Join(" ", copies.ToArray()));
                if (skinned.Count > 0) sb.Append(". Left out, worn over the skin: ").Append(string.Join(" ", skinned.ToArray()));
                Plugin.Instance.Log(sb.ToString());
            }
            return _catalog;
        }

        internal static Entry Find(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            Catalog();
            Entry e;
            return ByPrefab.TryGetValue(prefab, out e) ? e : null;
        }

        // ---------------------------------------------------------------- what a raven wears

        internal static string Worn(ItemDrop.ItemData item, int slot)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(DataKey[slot], out v) ? v : "";
        }

        // put on (or, with null, take off) a piece on the worn raven; seen at once here, and by the others on the next sync
        internal static void Choose(bool munin, int slot, string prefab)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            if (item == null) return;
            if (!string.IsNullOrEmpty(prefab) && !IsOpen(item, Find(prefab))) return; // not yet trusted with that
            Player p = Player.m_localPlayer;
            string off = Shoulder.NotOnShoulder(p, munin);
            if (off != null) { if (p != null) p.Message(MessageHud.MessageType.Center, off); return; }
            if (Paid(item, slot)) // a thing given from the bag goes back there first
            {
                string why = TakeOff(p, munin, slot);
                if (why != null) { if (p != null) p.Message(MessageHud.MessageType.Center, why); return; }
            }
            if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
            if (Worn(item, slot) != (prefab ?? "")) RavenPouch.Spill(p, item, slot); // a tried-on pouch's food back to the bag
            if (string.IsNullOrEmpty(prefab)) item.m_customData.Remove(DataKey[slot]);
            else item.m_customData[DataKey[slot]] = prefab;
            Refresh(munin, item);
        }

        private static void Refresh(bool munin, ItemDrop.ItemData item)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            ItemDrop.ItemData hugin, mun;
            RavenItem.Worn(p, out hugin, out mun);
            Publish(p, hugin, mun);
            Dress(Shoulder.LocalModel(munin), Code(item));
        }

        // ---------------------------------------------------------------- things from the raven table
        // A thing made at the raven table is an item in the bag (WsItems: RavenWatch_gear_<id>). Given to a raven it
        // leaves the bag and is kept in the raven (its slot, and "paid": it came from the bag); taken off, it goes back
        // into the bag as the same item. The pouch's food is the raven's while it is worn; taken off in any way, the food
        // goes back to the bag (RavenPouch.Spill), so a pouch always comes to a raven empty (no food of the other raven's
        // stuck in it). Admins tuning may still try a thing on without having it (not paid: nothing goes back).
        private const string PaidKey = "RavenWatch_OutfitPaid"; // + slot

        // A raven brought back from its copy (Backup): the copy may be older than taking a thing off it, so the things
        // given from the bag and the pouch's food are not brought back (they would be there twice). Hats stay: they are free.
        internal static void StripCrafted(ItemDrop.ItemData raven)
        {
            if (raven == null || raven.m_customData == null) return;
            for (int s = Neck; s < Slots; s++)
                if (Paid(raven, s)) { raven.m_customData.Remove(DataKey[s]); raven.m_customData.Remove(PaidKey + s); }
            RavenPouch.SetInRaven(raven, null);
        }

        internal static bool Paid(ItemDrop.ItemData raven, int slot)
        {
            string v;
            return raven != null && raven.m_customData != null && raven.m_customData.TryGetValue(PaidKey + slot, out v) && v == "1";
        }

        // the thing's item in the player's bag (the first one)
        internal static ItemDrop.ItemData InBag(Player p, Entry e)
        {
            if (p == null || e == null || e.Gear == null) return null;
            string prefab = RavenGear.PrefabOf(e.Prefab);
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) return it;
            return null;
        }

        internal static int CountInBag(Player p, Entry e)
        {
            if (p == null || e == null || e.Gear == null) return 0;
            string prefab = RavenGear.PrefabOf(e.Prefab);
            int n = 0;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
                if (it.m_dropPrefab != null && it.m_dropPrefab.name == prefab) n += it.m_stack;
            return n;
        }

        // may this thing be given to that raven now? null: yes, else why not (asked before the hero's move, and again after)
        internal static string CanPutOn(Player p, bool munin, Entry e)
        {
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (p == null || e == null || e.Gear == null || e.Slot == Head) return Lang.T("Це не надягають.");
            if (raven == null) return Lang.F("{0} не сидить у тебе на плечі.", Plugin.RavenName(munin));
            if (Trust.Stranger(raven, p)) return Lang.F("{0} не твій: не дасться вдягти.", Plugin.RavenName(munin));
            string off = Shoulder.NotOnShoulder(p, munin);
            if (off != null) return off;
            int need = GearFx.WearLevel(e.Prefab);
            if (!Tuning && Trust.SettledLevel(raven) < need) return Lang.F("{0}-го рівня довіри.", (Plugin.RavenName(munin) + (need > WardrobeLevel ? Lang.T(" ще не дасться вдягти таку річ: вона — з ") : Lang.T(" ще не дається вбирати: спорядження — з ")) + need));
            return null;
        }

        // the thing (this one from the bag, or the first of its kind) given to the raven; null when done, else why not.
        // What it wore in that slot goes back to the bag, into the place the new thing leaves.
        internal static string PutOn(Player p, bool munin, Entry e, ItemDrop.ItemData thing = null)
        {
            string no = CanPutOn(p, munin, e);
            if (no != null) return no;
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (thing == null || !p.GetInventory().ContainsItem(thing)) thing = InBag(p, e);
            if (thing == null) return Lang.F("У сумці нема: {0}.", e.Name);
            // one of them (another mod may let them stack): its copy kept to put back if the old thing cannot come off
            ItemDrop.ItemData one = thing.Clone();
            one.m_stack = 1;
            p.GetInventory().RemoveItem(thing, 1);
            if (Worn(raven, e.Slot).Length > 0)
            {
                string why = TakeOff(p, munin, e.Slot);
                if (why != null) { p.GetInventory().AddItem(one); return why; }
            }
            if (raven.m_customData == null) raven.m_customData = new Dictionary<string, string>();
            raven.m_customData[DataKey[e.Slot]] = e.Prefab;
            raven.m_customData[PaidKey + e.Slot] = "1";
            if (RavenGear.IsPouch(e.Prefab))
            {
                RavenPouch.SpillItem(p, one); // an old pouch item may still hold food: to the bag; the raven's pouch starts empty
                if (p.GetInventory().ContainsItem(thing)) RavenPouch.SpillItem(null, thing); // (the rest of a stack: emptied, not paid out again)
            }
            Refresh(munin, raven);
            GearFx.PutOnSay(p, munin, e.Prefab); // a word about it, now and then
            return null;
        }

        // take what the raven wears in this slot off; a thing given from the bag goes back there. null when done.
        internal static string TakeOff(Player p, bool munin, int slot)
        {
            ItemDrop.ItemData raven = Trust.Worn(munin);
            string on = Worn(raven, slot);
            if (raven == null || on.Length == 0) return null;
            if (p != null && Trust.Stranger(raven, p)) return Lang.F("{0} не твій: не дасться роздягти.", Plugin.RavenName(munin));
            string off = Shoulder.NotOnShoulder(p, munin);
            if (off != null) return off;
            if (Paid(raven, slot))
            {
                ObjectDB db = ObjectDB.instance;
                GameObject prefab = db != null ? db.GetItemPrefab(RavenGear.PrefabOf(on)) : null;
                if (prefab == null) return Lang.T("Цю річ зараз не зняти: її нема в цій грі."); // its model is gone: kept on the raven, not lost
                if (p == null || !p.GetInventory().HaveEmptySlot()) return Lang.T("Нема місця в сумці, щоб зняти річ із ворона.");
                ItemDrop.ItemData back = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                back.m_dropPrefab = prefab;
                back.m_stack = 1;
                back.m_worldLevel = (byte)Game.m_worldLevel;
                p.GetInventory().AddItem(back);
                RavenPouch.Spill(p, raven, slot); // its food to the bag (after the pouch: that took the free place)
            }
            else RavenPouch.Spill(p, raven, slot);
            raven.m_customData.Remove(DataKey[slot]);
            raven.m_customData.Remove(PaidKey + slot);
            Refresh(munin, raven);
            return null;
        }

        // what the raven shows (here and to everyone): only what it may wear now. A piece it can no longer wear
        // (tuning switched off, a level set lower by a command, data edited by hand) stays in the item but is not drawn.
        internal static string Code(ItemDrop.ItemData item)
        {
            if (item == null) return "";
            string c = WornOpen(item, Head);
            for (int s = Neck; s < Slots; s++) c += "|" + WornOpen(item, s);
            return c;
        }

        internal static string WornOpen(ItemDrop.ItemData item, int slot)
        {
            string p = Worn(item, slot);
            return p.Length > 0 && IsOpen(item, Find(p)) ? p : "";
        }

        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            string h = Code(hugin), m = Code(munin);
            if (zdo.GetString(KeyH, "") != h) zdo.Set(KeyH, h);
            if (zdo.GetString(KeyM, "") != m) zdo.Set(KeyM, m);
        }

        // ---------------------------------------------------------------- dressing a raven model

        // marks what we put on a raven (the eyes' glow and the next dressing leave it alone / take it off)
        internal class Piece : MonoBehaviour { }
        private class State : MonoBehaviour { public string Code = ""; public int Version; }

        // dress the raven model as the code says ("head|neck|back|legs", an older "head|neck" too); nothing happens when it already is
        internal static void Dress(GameObject raven, string code)
        {
            if (raven == null) return;
            if (code == null) code = "";
            State st = raven.GetComponent<State>();
            if (st == null) { if (code.Replace("|", "").Length == 0) return; st = raven.AddComponent<State>(); }
            if (st.Code == code && st.Version == Version) return;
            st.Code = code; st.Version = Version;
            Undress(raven);
            string[] parts = code.Split('|');
            for (int slot = Head; slot < Slots; slot++)
            {
                Entry e = parts.Length > slot ? Find(parts[slot]) : null;
                if (e != null) PutOn(raven, e);
            }
        }

        internal static void Undress(GameObject raven)
        {
            foreach (Piece p in raven.GetComponentsInChildren<Piece>(true)) if (p != null) UnityEngine.Object.DestroyImmediate(p.gameObject);
        }

        internal static void Forget(GameObject raven)
        {
            if (raven == null) return;
            Undress(raven);
            State st = raven.GetComponent<State>();
            if (st != null) UnityEngine.Object.DestroyImmediate(st);
        }

        private static void PutOn(GameObject raven, Entry e)
        {
            try
            {
                if (e.Gear != null) { RavenGear.PutOn(raven, e.Gear, FitFor(e, RavenGear.IsMunin(raven))); return; }
                // A head piece rides the head itself (Jaw: the skull with the upper beak), so it turns with every look
                // the game's own animation gives; the place is still counted from the neck, as the fits were tuned.
                Transform anchor = FindBone(raven, e.Slot == Head ? "Neck" : "Spine1");
                Transform bone = e.Slot == Head ? FindBone(raven, "Jaw") : anchor;
                if (bone == null) bone = anchor;
                if (bone == null) return;
                GameObject g = e.Slot == Head ? CopyAttach(e.Source) : CopyMeshes(e.Source);
                if (g == null) return;
                g.name = "RavenWatch_Outfit_" + e.Prefab;
                g.AddComponent<Piece>();
                g.transform.SetParent(bone, false);
                Place(raven, bone, anchor, g.transform, FitFor(e));
                foreach (Transform t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = raven.layer;
            }
            catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven outfit " + e.Prefab + " failed: " + ex.Message); }
        }

        // the worn model, as the game makes it for a hero: no colliders, no physics
        private static GameObject CopyAttach(GameObject source)
        {
            GameObject g = UnityEngine.Object.Instantiate(source);
            g.SetActive(true);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (Rigidbody r in g.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(r);
            foreach (ZNetView nv in g.GetComponentsInChildren<ZNetView>(true)) UnityEngine.Object.Destroy(nv);
            // no lamp on a raven's head (the dverger circlet lights up everything around): the piece itself still glows.
            // The game's scripts that switch a light on by distance or make it flicker go first, or they would turn it back on.
            foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && (mb.GetType().Name == "LightLod" || mb.GetType().Name == "LightFlicker")) UnityEngine.Object.DestroyImmediate(mb);
            foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
            return g;
        }

        // an item lying on the ground has physics and a net id: only its meshes are copied, where they are in it
        private static GameObject CopyMeshes(GameObject source)
        {
            GameObject root = new GameObject("Meshes");
            int n = 0;
            HashSet<Renderer> farLooks = new HashSet<Renderer>(); // the simpler models for far away: only the closest one is copied
            foreach (LODGroup lg in source.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (Renderer r in lods[i].renderers) if (r != null) farLooks.Add(r);
            }
            foreach (MeshRenderer mr in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (farLooks.Contains(mr)) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject part = new GameObject(mr.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.InverseTransformPoint(mr.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.transform.rotation) * mr.transform.rotation;
                part.transform.localScale = Divide(mr.transform.lossyScale, source.transform.lossyScale);
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                n++;
            }
            if (n == 0) { UnityEngine.Object.Destroy(root); return null; }
            return root;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z));
        }

        private static Transform FindBone(GameObject raven, string name)
        {
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // Size and place are counted in the raven's own measure (its head with the beak), so a fit holds at any size.
        // The turn starts from the raven facing forward, upright, whatever pose the bone is in right now: the bone's
        // rest pose is taken from the model's bind pose when the game lets us read it.
        private const float HumanHead = 0.22f; // a hero's head the pieces are made for, meters across
        internal static float HeadShare = 0.4f; // a raven's head across, against its head with the beak

        private static void Place(GameObject raven, Transform bone, Transform anchor, Transform piece, Fit f)
        {
            Transform root = raven.transform;
            float unit = HeadLength(raven, root);
            Quaternion rest = RestInRoot(raven, bone);
            Vector3 from = anchor != null && anchor != bone ? RestPosInRoot(raven, anchor) - RestPosInRoot(raven, bone) : Vector3.zero;
            Quaternion want = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(f.RX, f.RY, f.RZ); // the raven looks along its back axis
            float boneScale = Mathf.Max(0.0001f, bone.lossyScale.x / Mathf.Max(0.0001f, root.lossyScale.x));
            Vector3 pos = from + Quaternion.Euler(0f, 180f, 0f) * new Vector3(f.X, f.Y, f.Z) * unit; // forward = towards the beak
            piece.localPosition = Quaternion.Inverse(rest) * pos / boneScale;
            piece.localRotation = Quaternion.Inverse(rest) * want;
            piece.localScale = Vector3.one * (f.S * unit * HeadShare / HumanHead) / boneScale;
        }

        // the raven's head with the beak, in its own (root) measure; measured once
        // Hugin and Munin are different models: each is measured on its own (a hat tuned on one sits the same on the other)
        private static readonly Dictionary<string, float> Units = new Dictionary<string, float>();
        private const float KnownHead = 6.646f; // the game's raven head with the beak, measured 2026-10-07 (until this raven is measured)
        private static float HeadLength(GameObject raven, Transform root)
        {
            SkinnedMeshRenderer body = raven.GetComponentInChildren<SkinnedMeshRenderer>(true);
            string model = body != null && body.sharedMesh != null ? body.sharedMesh.name : raven.name;
            float d;
            if (Units.TryGetValue(model, out d)) return d;
            Transform neck = FindBone(raven, "Neck"), tip = FindBone(raven, "Jaw1_end");
            if (neck == null || tip == null) return 0.3f;
            // Only a raven fully there is measured: one still appearing (the game grows its bones out of nothing) gave
            // a head 22% short, kept for the whole game, and every hat sat smaller and lower. Not now: the known size
            // is used meanwhile, and the next raven is measured.
            for (Transform t = tip; t != null && t != root; t = t.parent)
            {
                Vector3 s = t.localScale;
                if (Mathf.Abs(s.x - 1f) > 0.02f || Mathf.Abs(s.y - 1f) > 0.02f || Mathf.Abs(s.z - 1f) > 0.02f) return KnownHead;
            }
            d = Vector3.Distance(root.InverseTransformPoint(neck.position), root.InverseTransformPoint(tip.position));
            if (d < 0.001f) return KnownHead; // a squashed (not yet landed) model: try again on the next raven
            Units[model] = d;
            Version++; // pieces put on with the stand-in size are put on again with the measured one
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven head length for outfits (" + model + "): " + d.ToString("0.###", CultureInfo.InvariantCulture));
            return d;
        }

        private static Quaternion RestInRoot(GameObject raven, Transform bone)
        {
            try
            {
                foreach (SkinnedMeshRenderer smr in raven.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null || smr.bones == null) continue;
                    int i = Array.IndexOf(smr.bones, bone);
                    if (i < 0) continue;
                    Matrix4x4[] bind = smr.sharedMesh.bindposes;
                    if (bind == null || i >= bind.Length) continue;
                    Quaternion inSmr = bind[i].inverse.rotation;
                    Quaternion smrInRoot = Quaternion.Inverse(raven.transform.rotation) * smr.transform.rotation;
                    return smrInRoot * inSmr;
                }
            }
            catch (Exception) { }
            return Quaternion.Inverse(raven.transform.rotation) * bone.rotation; // the pose it is in now
        }

        // where a bone is in the rest pose, in the raven's own (root) measure
        private static Vector3 RestPosInRoot(GameObject raven, Transform bone)
        {
            try
            {
                foreach (SkinnedMeshRenderer smr in raven.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh == null || smr.bones == null) continue;
                    int i = Array.IndexOf(smr.bones, bone);
                    if (i < 0) continue;
                    Matrix4x4[] bind = smr.sharedMesh.bindposes;
                    if (bind == null || i >= bind.Length) continue;
                    Vector3 inSmr = bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
                    return (raven.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix).MultiplyPoint3x4(inSmr);
                }
            }
            catch (Exception) { }
            return raven.transform.InverseTransformPoint(bone.position); // where it is now
        }

        // ---------------------------------------------------------------- fits

        private static string FitFile { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "outfits.txt"); } }

        // a fit for one raven only: the piece's name + "@h" (Hugin) or "@m" (Munin); without it both share one
        internal static string RavenKey(Entry e, bool munin) { return e.Prefab + (munin ? "@m" : "@h"); }
        private static readonly Fit GearDefault = new Fit(); // a thing from the Workshop without a fit: as it was made there

        private static Dictionary<string, Fit> Fits()
        {
            if (_fits != null) return _fits;
            _fits = new Dictionary<string, Fit>();
            try
            {
                if (File.Exists(FitFile))
                    foreach (string raw in File.ReadAllLines(FitFile))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string[] v = line.Substring(eq + 1).Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        float[] n = new float[10];
                        int k = 0;
                        for (; k < v.Length && k < 10; k++) if (!float.TryParse(v[k], NumberStyles.Float, CultureInfo.InvariantCulture, out n[k])) break;
                        if (k < 7) continue;
                        Fit fit = new Fit(n[0], n[1], n[2], n[3], n[4], n[5], n[6]);
                        if (k >= 10) { fit.SX = n[7]; fit.SY = n[8]; fit.SZ = n[9]; } // a thing's size along each axis
                        fit.Ok = v.Length > k && v[k].ToLowerInvariant() == "ok";
                        _fits[line.Substring(0, eq).Trim()] = fit;
                    }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Could not read outfits.txt: " + e.Message); }
            return _fits;
        }

        // the piece's own line in the file, its released fit, the file's line for all such pieces, the released one
        internal static Fit FitFor(Entry e)
        {
            Fit f;
            if (Fits().TryGetValue(e.Prefab, out f)) return f;
            if (Builtin.TryGetValue(e.Prefab, out f)) return f;
            if (e.Gear != null) return GearDefault;
            string slot = e.Slot == Head ? "head" : "neck";
            if (Fits().TryGetValue(slot, out f)) return f;
            return Builtin[slot];
        }

        // for this raven: its own fit when it has one, else the shared one
        internal static Fit FitFor(Entry e, bool munin)
        {
            Fit f;
            string k = RavenKey(e, munin);
            if (Fits().TryGetValue(k, out f)) return f;
            if (Builtin.TryGetValue(k, out f)) return f;
            return FitFor(e);
        }

        // this raven sits it its own way (not as the other one)
        internal static bool Separate(Entry e, bool munin)
        {
            string k = RavenKey(e, munin);
            return Fits().ContainsKey(k) || Builtin.ContainsKey(k);
        }

        private static string EditKey(Entry e, bool munin) { return Separate(e, munin) ? RavenKey(e, munin) : e.Prefab; }

        internal static bool Released(Entry e) { return Builtin.ContainsKey(e.Prefab); }

        // done: approved in the file, or already in the mod (and not being tuned again)
        internal static bool Ready(Entry e)
        {
            Fit f;
            if (Fits().TryGetValue(e.Prefab, out f)) return f.Ok;
            return Released(e);
        }

        internal static bool Ready(Entry e, bool munin)
        {
            if (!Separate(e, munin)) return Ready(e);
            Fit f;
            if (Fits().TryGetValue(RavenKey(e, munin), out f)) return f.Ok;
            return true; // its own fit is in the mod already
        }

        // "Готово": this fit is the one to keep
        internal static void Approve(Entry e) { Approve(e, false); }
        internal static void Approve(Entry e, bool munin)
        {
            if (e == null) return;
            Fit f = FitFor(e, munin).Copy();
            f.Ok = true;
            Fits()[EditKey(e, munin)] = f;
            Save();
        }

        internal static string Status(Entry e) { return Status(e, false); }
        internal static string Status(Entry e, bool munin)
        {
            Fit f;
            string own = Separate(e, munin) ? Lang.F(", лише для {0}", (munin ? Lang.T("Муніна") : Lang.T("Хугіна"))) : "";
            if (Fits().TryGetValue(EditKey(e, munin), out f)) return (f.Ok ? Lang.T("готова, чекає в мод") : Lang.T("підганяється, ще не «Готово»")) + own;
            if (Builtin.ContainsKey(EditKey(e, munin))) return Lang.F("готова, вже в моді{0}", own);
            if (e.Gear != null) return Lang.T("як зроблено в Майстерні");
            return Lang.F("спільна для всіх {0}", (e.Slot == Head ? Lang.T("шапок") : Lang.T("намист")));
        }

        // the book's buttons: a piece gets its own line, written to the file at once
        internal static void Nudge(Entry e, Action<Fit> change) { Nudge(e, change, false); }
        internal static void Nudge(Entry e, Action<Fit> change, bool munin)
        {
            if (e == null) return;
            Fit f = FitFor(e, munin).Copy();
            change(f);
            f.Ok = false; // moved: to be approved again
            Fits()[EditKey(e, munin)] = f;
            Save();
            Version++;
        }

        internal static void ResetFit(Entry e) { ResetFit(e, false); }
        internal static void ResetFit(Entry e, bool munin)
        {
            if (e == null || !Fits().Remove(EditKey(e, munin))) return;
            Save();
            Version++;
        }

        // this raven gets a fit of its own (from the shared one), or goes back to the shared one
        internal static void SetSeparate(Entry e, bool munin, bool own)
        {
            if (e == null || own == Separate(e, munin)) return;
            string k = RavenKey(e, munin);
            if (own) { Fit f = FitFor(e).Copy(); f.Ok = false; Fits()[k] = f; }
            else Fits().Remove(k); // (a fit of its own already in the mod stays: it is changed there)
            Save();
            Version++;
        }

        // the other raven sits it as this one does (its own line, to be approved there too)
        internal static void CopyToOther(Entry e, bool munin)
        {
            if (e == null) return;
            Fit f = FitFor(e, munin).Copy();
            f.Ok = false;
            Fits()[RavenKey(e, !munin)] = f;
            Save();
            Version++;
        }

        private static void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# How the ravens' outfits sit: name = size x y z rx ry rz [sx sy sz]");
                sb.AppendLine("# A head piece: size 1 = a raven's head; x right, y up, z towards the beak, in the raven's head lengths; turns in degrees.");
                sb.AppendLine("# A thing from the Workshop (gear_…): size × its own, x y z in metres, turns in degrees, in the Workshop's frame; sx sy sz its size along each axis.");
                sb.AppendLine("# name@h / name@m: for Hugin / Munin only. 'head' and 'neck' are for every piece without its own line. Tuned in the book (tab Вбрання); 'ok' at the end = approved with Готово.");
                foreach (KeyValuePair<string, Fit> kv in Fits())
                {
                    Fit f = kv.Value;
                    bool axes = Mathf.Abs(f.SX - 1f) > 0.0001f || Mathf.Abs(f.SY - 1f) > 0.0001f || Mathf.Abs(f.SZ - 1f) > 0.0001f;
                    sb.AppendLine(kv.Key + " = " + F(f.S) + " " + F(f.X) + " " + F(f.Y) + " " + F(f.Z) + " " + F(f.RX) + " " + F(f.RY) + " " + F(f.RZ)
                        + (axes ? " " + F(f.SX) + " " + F(f.SY) + " " + F(f.SZ) : "") + (f.Ok ? " ok" : ""));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(FitFile));
                File.WriteAllText(FitFile, sb.ToString());
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Could not write outfits.txt: " + e.Message); }
        }

        internal static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        internal static string Describe(Fit f)
        {
            return Lang.F("розмір {0} · вбік {1} · вгору {2} · вперед {3} · нахил {4}° · поворот {5}° · крен {6}°", F(f.S), F(f.X), F(f.Y), F(f.Z), F(f.RX), F(f.RY), F(f.RZ));
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    internal static class OutfitVanillaPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ObjectDB __instance) { Outfits.Snapshot(__instance); }
    }

    // ---------------------------------------------------------------- the book's picture of the raven

    // A copy of your raven far above the world, on a layer of its own, slowly turning, filmed into a picture
    // for the book while the outfit page is open.
    internal static class OutfitStage
    {
        private const int Size = 512;
        private static GameObject _stage, _model;
        private static Camera _cam;
        private static Light[] _lamps;
        private static RenderTexture _rt;
        private static bool _munin;
        private static string _code;
        private static int _version;
        private static float _angle = 135f; // three-quarter from the front, the beak to the left (the raven looks along its back axis)

        internal static RenderTexture Texture { get { return _rt; } }

        // the book's picture is dragged with the mouse: it turns by hand, and stands still a while after
        private static float _heldUntil;
        private static bool _shot; // a picture taken since it was built (else nothing shows)
        private static float _shotAngle, _shotPitch, _shotZoom;
        internal static void Turn(float degrees) { _angle += degrees; _heldUntil = Time.unscaledTime + 3f; }

        // the fitting's view: turning by itself or held, how high the camera looks from, sitting or in flight
        internal static bool Spin = true;
        internal static float Pitch = 0.25f;
        // how the raven is shown: 0 standing (the game's landed idle), 1 sitting (crouched as on a perch, breathing), 2 flying
        internal const int Standing = 0, Sitting = 1, Flying = 2;
        internal static int Mode;
        internal static string[] ModeNames { get { return Lang.Arr(ModeNamesSrc); } } // (translated on every read)
        private static readonly string[] ModeNamesSrc = Lang.Keys("Стоїть", "Сидить", "Летить");
        private static int _modeWas;
        private static PerchPose _sit;
        private static int _sitShots;
        internal static void View(float angle, float pitch) { _angle = angle; Pitch = pitch; Spin = false; }
        internal static void ZoomBy(float k) { Zoom = Mathf.Clamp(Zoom * k, 0.8f, 10f); }

        // «Стоп»: it sits, held still in an even pose, in the middle of the picture; the camera stops where it is shown
        // from three-quarters at the front
        internal static bool Still;
        internal static void Stop() { Still = true; Spin = false; Mode = Sitting; _angle = 135f; Pitch = 0.15f; _sitShots = 0; }
        internal static void Go() { Still = false; Spin = true; }

        // the sitting pose, kept to come back to from the flight; the flying clip and the bones it must not carry off
        private static Transform[] _bones;
        private static Vector3[] _bp, _bs;
        private static Quaternion[] _br;
        private static Animator _anim;
        private static AnimationClip _fly;
        private static Transform[] _roots;
        private static Vector3[] _rootPos;
        private static Transform _hip;
        private static bool _wasFlying;
        private static float _flyT;

        private static int _level;
        private static string _wear = "";
        private static int _wearStamp = -1;

        // the picture of this raven dressed so; made again when the raven or its level changes
        internal static bool Show(bool munin, int level)
        {
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null) { Close(); return false; }
            if (_stage == null || _munin != munin || _level != level || _model == null) Build(src, munin, level);
            return _stage != null;
        }

        internal static void Close()
        {
            // off at once: destroyed only at the frame's end, the next raven's first shot (Hugin ↔ Munin) took both
            if (_stage != null) { _stage.SetActive(false); UnityEngine.Object.Destroy(_stage); }
            _stage = null; _model = null; _cam = null;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; }
        }

        // A fresh raven from the game's own, landed and then held still in its quiet sitting pose: always the same
        // picture, never caught mid-flap or with its head turned, as the raven on the shoulder may be.
        private static void Build(Raven src, bool munin, int level)
        {
            Close();
            int layer = RavenFx.FreeLayer();
            if (layer < 0) return;
            _munin = munin;
            _level = level;
            _stage = new GameObject("RavenWatch_OutfitStage");
            _stage.transform.position = new Vector3(0f, 9100f, 0f);
            _model = UnityEngine.Object.Instantiate(src.m_visual, _stage.transform, false);
            AppearMute.Quiet(_model);
            _model.SetActive(true);
            Shared.RemoveExclamation(src, _model);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            _model.transform.localScale = src.m_visual.transform.localScale;
            foreach (Collider c in _model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (Animator a in _model.GetComponentsInChildren<Animator>(true))
            {
                // land it (as on the shoulder) and run the animation on by hand until it sits quietly, then hold it there
                a.enabled = true;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                a.Rebind();
                a.Update(0f);
                a.SetTrigger("teleportin");
                for (int i = 0; i < 60; i++) a.Update(0.1f);
                a.enabled = false;
            }
            // a pose still squashed (the game keeps a raven that is away squashed to nothing): opened up
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                if (t != _model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
            EyeGlow.Apply(_model, level); // its eyes as on your shoulder
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
                if (sk != null) sk.updateWhenOffscreen = true;
            }
            _model.layer = layer;
            // the sitting pose as it is now, and what the flight needs
            List<Transform> bones = new List<Transform>(), roots = new List<Transform>();
            _hip = null;
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
            {
                if (t == _model.transform) continue;
                bones.Add(t);
                string n = t.name.ToLowerInvariant();
                if (n == "armature" || n == "hip" || n == "hips" || n == "root" || n == "body") roots.Add(t);
                if (n == "hip" || n == "hips") _hip = t;
            }
            _bones = bones.ToArray();
            _bp = new Vector3[_bones.Length]; _bs = new Vector3[_bones.Length]; _br = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++) { _bp[i] = _bones[i].localPosition; _br[i] = _bones[i].localRotation; _bs[i] = _bones[i].localScale; }
            _roots = roots.ToArray();
            _rootPos = new Vector3[_roots.Length];
            for (int i = 0; i < _roots.Length; i++) _rootPos[i] = _roots[i].localPosition;
            _anim = _model.GetComponentInChildren<Animator>(true);
            _fly = _anim != null ? ShoulderLife.FlyClipOf(_anim) : null;
            _wasFlying = false;
            _modeWas = Standing; _sit = null;

            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.enabled = false;
            _cam.cullingMask = 1 << layer;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.082f, 0.09f, 0.106f, 1f); // the book's dark slot (#15171B)
            _cam.fieldOfView = 25f;
            _cam.renderingPath = RenderingPath.Forward;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            Light key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(_stage.transform, false);
            key.type = LightType.Directional; key.intensity = 1.2f; key.color = new Color(1f, 0.96f, 0.9f); key.cullingMask = 1 << layer;
            key.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            Light fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(_stage.transform, false);
            fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.8f, 0.85f, 1f); fill.cullingMask = 1 << layer;
            fill.transform.rotation = Quaternion.Euler(20f, -60f, 0f);
            key.enabled = fill.enabled = false; // lit only while the picture is taken (see Tick)
            _lamps = new Light[] { key, fill };
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            _rt.Create();
            _cam.targetTexture = _rt;
            _code = null;
            _shot = false;
        }

        // every frame while the page is open: dressed as chosen, turned a little, filmed (only when something in it moved)
        internal static void Tick(string code) { Tick(code, ""); }

        // wear: what its runes give it (RavenWear), seen here too
        internal static void Tick(string code, string wear)
        {
            if (_stage == null || _model == null || _cam == null) return;
            bool dressed = false;
            if (wear == null) wear = "";
            if (code != _code || _version != Outfits.Version || wear != _wear || _wearStamp != WearCatalog.Stamp)
            {
                dressed = true;
                _code = code; _version = Outfits.Version; _wear = wear; _wearStamp = WearCatalog.Stamp;
                Outfits.Dress(_model, code);
                RavenWear.Dress(_model, _munin, wear);
                EyeGlow.Apply(_model, _level); // the paint put the game's materials back under it: the eyes made again on what it wears
                foreach (Transform t in _model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _model.layer;
                Measure();
            }
            if (Spin && Time.unscaledTime >= _heldUntil) _angle += Time.unscaledDeltaTime * 25f; // the camera goes slowly round the raven, from the three-quarter view
            // Shot every frame while anything in it moves (the turning, a drag, the breathing on a perch, the flight):
            // not capped at 30 a second, which goes in uneven steps whenever the game is not at 60. Nothing moving: no shot.
            bool moving = Mode != Standing || _angle != _shotAngle || Pitch != _shotPitch || Zoom != _shotZoom || _modeWas != Mode || WearMoves();
            if (!dressed && !moving && _shot) return;
            _shot = true; _shotAngle = _angle; _shotPitch = Pitch; _shotZoom = Zoom;
            Pose();
            Frame();
            // The world's light settings are left alone: switched for the picture and back every frame, the game's
            // ambient light came back different and the grass went pale. The raven is close to the camera (no fog to speak of)
            // and has its own lamps, on only for this one shot: left on, they lit the world too (the layer the game draws
            // its grass with has no name either, so a "free" layer is not surely free).
            if (_lamps != null) foreach (Light l in _lamps) if (l != null) l.enabled = true;
            try { _cam.Render(); }
            finally { if (_lamps != null) foreach (Light l in _lamps) if (l != null) l.enabled = false; }
        }

        // what its runes give it plays on it (embers, sparks): kept moving in the picture too
        private static bool WearMoves()
        {
            WearState st = _model != null ? _model.GetComponent<WearState>() : null;
            return st != null && st.Fx.Count > 0;
        }

        // the raven with its outfit: where its middle is and how big, measured once for each dressing
        private static Vector3 _center;
        private static float _radius = 0.3f;
        internal static float Zoom = 2f; // the raven twice as big as "all of it surely in the picture" (that left too much room)

        private static void Measure()
        {
            Bounds b = new Bounds(_model.transform.position, Vector3.zero);
            bool any = false;
            // the body itself (its skinned mesh): what it wears, a crown, the eyes' glow do not pull it off the middle
            foreach (SkinnedMeshRenderer sk in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!sk.enabled || sk.GetComponentInParent<Outfits.Piece>() != null) continue;
                if (any) b.Encapsulate(sk.bounds); else { b = sk.bounds; any = true; }
            }
            if (any && b.size.magnitude > 0.05f && b.size.magnitude < 10f) { _center = b.center; _radius = Mathf.Max(0.05f, b.extents.magnitude); return; }
            any = false;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<Outfits.Piece>() != null) continue; // the raven alone: what it wears (moved while fitting) does not move the camera
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            if (!any || b.size.magnitude < 0.05f || b.size.magnitude > 10f) b = new Bounds(_model.transform.position + Vector3.up * 0.3f, Vector3.one * 0.6f);
            _center = b.center;
            _radius = Mathf.Max(0.05f, b.extents.magnitude);
        }

        // In flight: the game's flying clip, the wings beating, the body held where it sits and levelled as the
        // flight levels it; back to sitting: the pose it had. The things worn ride their bones either way.
        private static void Pose()
        {
            int mode = Mode == Flying && (_fly == null || _anim == null) ? Standing : Mode;
            if (mode != _modeWas)
            {
                // out of the last pose first: the sitting one goes, the bones as they stood
                if (_sit != null) { UnityEngine.Object.DestroyImmediate(_sit); _sit = null; }
                if (_modeWas != Standing) Restore();
                if (mode == Sitting)
                {
                    // the perch's own sitting (PerchPose: legs folded, toes round, the body sunk, the idle breathing)
                    _sit = _model.AddComponent<PerchPose>();
                    _sit.Init(PerchPose.Squat);
                    _sitShots = 0;
                }
                _modeWas = mode;
            }
            if (_sit != null) _sit.Neutral = Still;
            if (mode == Sitting && _sitShots < 3) { _sitShots++; Measure(); } // it sinks into its legs: framed where it is then (not every breath)
            bool fly = mode == Flying;
            if (fly)
            {
                _flyT += Time.unscaledDeltaTime;
                float len = ShoulderLife.BeatKnown ? ShoulderLife.LoopLength : _fly.length;
                float time = (ShoulderLife.BeatKnown ? ShoulderLife.BeatStart : 0f) + Mathf.Repeat(_flyT, Mathf.Max(0.1f, len));
                Transform root = _anim.transform;
                Vector3 lp = root.localPosition; Quaternion lr = root.localRotation; Vector3 ls = root.localScale;
                _fly.SampleAnimation(_anim.gameObject, time);
                root.localPosition = lp; root.localRotation = lr; root.localScale = ls;
                for (int i = 0; i < _roots.Length; i++) if (_roots[i] != null) _roots[i].localPosition = _rootPos[i];
                foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer) && !r.gameObject.activeSelf) r.gameObject.SetActive(true); // the clip may hide a part
                _model.transform.localRotation = ShoulderLife.LevelFix;
                if (_hip != null) _center = _hip.position;
            }
            _wasFlying = fly;
        }

        // the standing pose it was made in, as it was
        private static void Restore()
        {
            for (int i = 0; i < _bones.Length; i++) if (_bones[i] != null) { _bones[i].localPosition = _bp[i]; _bones[i].localRotation = _br[i]; _bones[i].localScale = _bs[i]; }
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            Measure();
        }

        // the camera round the raven, a little from above (or from where the fitting asked), keeping all of it in the picture
        private static void Frame()
        {
            float r = _radius * (_wasFlying ? 1.6f : 1f); // the wings spread
            float dist = r / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f / Zoom;
            Vector3 dir = Quaternion.Euler(0f, _angle, 0f) * new Vector3(0f, Pitch, 1f).normalized;
            _cam.nearClipPlane = Mathf.Max(0.01f, dist - r * 2f);
            _cam.farClipPlane = dist + r * 2f;
            _cam.transform.position = _center + dir * dist;
            _cam.transform.LookAt(_center);
        }
    }
}
#endif

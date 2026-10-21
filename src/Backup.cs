#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A lost raven can come back. Every awake raven of yours has its own id and a generation; a copy of it is kept in
    // your character (every half hour, and after a ritual or a return), and at the Sacrificial stones, for two trophies
    // of the boss of its last ritual, the raven is brought back from that copy: same trust level, training and bosses,
    // an empty trust bar, and part of its memory lost on the way. Once in 5 game days per raven.
    // No two of one raven: a return raises the generation, written in your character and in the world (a global key
    // every game sees). An older copy, wherever it was hidden (a chest, the ground, a friend's bag, a grave), crumbles
    // into feathers as soon as anyone with the mod has it in a bag or opens the chest it lies in; so does a second copy
    // of one raven found in the same bag or chest.
    internal static class Backup
    {
        private const string IdKey = "RavenWatch_id", GenKey = "RavenWatch_gen";
        private const string CopyPrefix = "RavenWatch_copy_", GenPrefix = "RavenWatch_gen_", BackPrefix = "RavenWatch_back_";
        internal static float SaveMinutes = 30f;
        internal static int RestoreDays = 5;
        private static float _nextSave = -1f, _nextScan;
        private static readonly FieldInfo OpenContainer = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        private static string Get(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        internal static string Id(ItemDrop.ItemData item) { return Get(item, IdKey); }
        private static int Gen(ItemDrop.ItemData item) { int g; return int.TryParse(Get(item, GenKey), out g) ? g : 1; }

        // the copy's text: every key and value of the raven, base64, "key:value,key:value"
        private static string Pack(ItemDrop.ItemData item)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> kv in item.m_customData)
                parts.Add(B64(kv.Key) + ":" + B64(kv.Value ?? ""));
            return string.Join(",", parts.ToArray());
        }

        private static Dictionary<string, string> Unpack(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return d;
            foreach (string part in text.Split(','))
            {
                int c = part.IndexOf(':');
                if (c <= 0) continue;
                try { d[UnB64(part.Substring(0, c))] = UnB64(part.Substring(c + 1)); } catch { }
            }
            return d;
        }

        private static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        private static string UnB64(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }

        // ---------------------------------------------------------------- generations

        private static int ProfileGen(Player p, string id)
        {
            string v; int g;
            return p != null && p.m_customData.TryGetValue(GenPrefix + id, out v) && int.TryParse(v, out g) ? g : 0;
        }

        private static string WorldKey(string id, int gen) { return ("rw_" + id + "_g" + gen).ToLowerInvariant(); }

        private static int WorldGen(string id)
        {
            if (ZoneSystem.instance == null) return 0;
            string head = ("rw_" + id + "_g").ToLowerInvariant();
            int best = 0;
            foreach (string key in ZoneSystem.instance.GetGlobalKeys())
            {
                if (key == null || !key.StartsWith(head)) continue;
                int g;
                if (int.TryParse(key.Substring(head.Length).Split(' ')[0], out g) && g > best) best = g;
            }
            return best;
        }

        // an older copy of a raven that has been brought back since
        private static bool Stale(ItemDrop.ItemData item, Player p)
        {
            string id = Id(item);
            if (string.IsNullOrEmpty(id)) return false;
            int gen = Gen(item);
            return gen < WorldGen(id) || (Mine(item, p) && gen < ProfileGen(p, id));
        }

        private static bool Mine(ItemDrop.ItemData item, Player p) { return Trust.Active(item) && Trust.IsOwner(item, p); }

        // ---------------------------------------------------------------- every half a second, from the sync

        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead() || Time.time < _nextScan) return;
            _nextScan = Time.time + 2f;
            Inventory bag = p.GetInventory();
            Crumble(p, bag, true);
            Container open = OpenContainer != null && InventoryGui.instance != null ? OpenContainer.GetValue(InventoryGui.instance) as Container : null;
            if (open != null && open.GetInventory() != null) Crumble(p, open.GetInventory(), false);

            // your awake ravens: an id the first time, and their copy now and then
            if (_nextSave < 0f) _nextSave = Time.time + 60f;
            bool save = Time.time >= _nextSave;
            if (save) _nextSave = Time.time + Mathf.Max(1f, SaveMinutes) * 60f;
            List<ItemDrop.ItemData> mine = new List<ItemDrop.ItemData>(bag.GetAllItems());
            mine.AddRange(RavenSeat.Shelved(p)); // on SeneaL UI's shoulder cells, off the bag
            foreach (ItemDrop.ItemData item in mine)
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin) || !Mine(item, p)) continue;
                if (string.IsNullOrEmpty(Id(item))) { item.m_customData[IdKey] = Guid.NewGuid().ToString("N").Substring(0, 12); item.m_customData[GenKey] = "1"; Save(p, item); }
                else if (save) Save(p, item);
            }
        }

        private static void Crumble(Player p, Inventory inv, bool own)
        {
            List<ItemDrop.ItemData> gone = null;
            HashSet<string> ids = new HashSet<string>();
            List<ItemDrop.ItemData> all = new List<ItemDrop.ItemData>(inv.GetAllItems());
            if (own) all.AddRange(RavenSeat.Shelved(p)); // the shoulder cells count as yours too
            foreach (ItemDrop.ItemData item in all)
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin)) continue;
                string id = Id(item);
                bool twin = id != null && !ids.Add(id); // the same raven twice in one place (a glitch or a mod copied it): one is kept
                if (!twin && !Stale(item, p)) continue;
                if (gone == null) gone = new List<ItemDrop.ItemData>();
                gone.Add(item);
            }
            if (gone == null) return;
            foreach (ItemDrop.ItemData item in gone)
            {
                if (own && item.m_equipped)
                {
                    RavenSeat.Crumbling = true;
                    try { p.UnequipItem(item, false); } finally { RavenSeat.Crumbling = false; }
                }
                if (RavenSeat.OnShelf(item)) RavenSeat.Forget(item); else inv.RemoveItem(item);
                GameObject feathers = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Feathers") : null;
                ItemDrop f = feathers != null ? feathers.GetComponent<ItemDrop>() : null;
                if (f != null) { ItemDrop.ItemData pile = f.m_itemData.Clone(); pile.m_dropPrefab = feathers; pile.m_stack = 3; inv.AddItem(pile); }
            }
            RavenMsg.Center(p, Lang.T("Цей ворон був лише відлунням:\nсправжнього повернули біля каменів. Лишилось пір'я."));
        }

        // the copy of this raven in your character
        internal static void Save(Player p, ItemDrop.ItemData item)
        {
            string id = Id(item);
            if (p == null || string.IsNullOrEmpty(id) || !Mine(item, p)) return;
            bool munin;
            RavenItem.IsRaven(item, out munin);
            p.m_customData[CopyPrefix + id] = (munin ? "m" : "h") + ";" + Pack(item);
            if (ProfileGen(p, id) < Gen(item)) p.m_customData[GenPrefix + id] = Gen(item).ToString();
        }

        // a copy of a raven of this kind kept in your character
        internal static bool HasCopy(Player p, bool munin)
        {
            if (p == null) return false;
            string kind = munin ? "m;" : "h;";
            foreach (KeyValuePair<string, string> kv in p.m_customData)
                if (kv.Key.StartsWith(CopyPrefix) && kv.Value.StartsWith(kind)) return true;
            return false;
        }

        // ---------------------------------------------------------------- the book's view

        internal sealed class Lost
        {
            public string Id; public bool Munin; public int Level; public bool WithYou; public Trust.Step Price; public int DaysLeft;
            public Dictionary<string, string> Data;
            private ItemDrop.ItemData _copy;
            // the copy's data on a bare item, only to be read (its name, norov, training) — never put in a bag; made when
            // first asked (the book's once-a-second look asks only for the ids)
            public ItemDrop.ItemData Copy { get { if (_copy == null) _copy = new ItemDrop.ItemData { m_customData = Data }; return _copy; } }
        }

        // every raven of yours with a copy: whether it is with you, and what its return would take
        internal static List<Lost> Ravens(Player p)
        {
            List<Lost> list = new List<Lost>();
            if (p == null) return list;
            HashSet<string> here = new HashSet<string>();
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) { string id = Id(item); if (id != null) here.Add(id); }
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) { string id = Id(item); if (id != null) here.Add(id); }
            int today = Memory.Day();
            foreach (KeyValuePair<string, string> kv in p.m_customData)
            {
                if (!kv.Key.StartsWith(CopyPrefix)) continue;
                Lost l = new Lost();
                l.Id = kv.Key.Substring(CopyPrefix.Length);
                l.Munin = kv.Value.StartsWith("m");
                Dictionary<string, string> data = Unpack(kv.Value.Substring(Math.Min(2, kv.Value.Length)));
                string lv; int level;
                l.Level = data.TryGetValue("RavenWatch_trustLevel", out lv) && int.TryParse(lv, out level) ? level : 1;
                l.Data = data;
                l.WithYou = here.Contains(l.Id);
                List<Trust.Step> steps = Trust.Steps();
                l.Price = steps.Count > 0 ? steps[Mathf.Clamp(l.Level - 1, 0, steps.Count - 1)] : null;
                string back; int day;
                // days of this world: a return made in a world further on in time does not lock this one for ages
                l.DaysLeft = p.m_customData.TryGetValue(BackPrefix + l.Id, out back) && int.TryParse(back, out day) && day <= today ? Mathf.Max(0, day + RestoreDays - today) : 0;
                list.Add(l);
            }
            list.Sort((a, b) => a.Munin.CompareTo(b.Munin));
            return list;
        }

        // ---------------------------------------------------------------- the return

        internal static string Restore(Player p, string id)
        {
            Lost l = Ravens(p).Find(x => x.Id == id);
            if (l == null) return Lang.T("Копії цього ворона нема.");
            string name = Plugin.RavenName(l.Munin);
            if (l.WithYou) return Lang.F("{0} і так з тобою.", name);
            if (l.DaysLeft > 0) return Lang.F("{0} ще не відійшов від минулого повернення.\nСпробуй через {1} дн.", name, l.DaysLeft);
            if (!Trust.AtStones(p)) return Lang.T("Повернути ворона можна лише біля Жертовних каменів.");
            if (l.Price == null || Trust.Have(p, l.Price.Prefab) < l.Price.Count)
                return Lang.F("Потрібно: {0}.", (l.Price != null ? Lang.F("{0} ×{1} (є {2})", l.Price.Name, l.Price.Count, Trust.Have(p, l.Price.Prefab)) : Lang.T("трофеї боса")));
            if (!p.GetInventory().HaveEmptySlot()) return Lang.T("У сумці нема місця для ворона.");
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(l.Munin ? RavenItem.MuninPrefab : RavenItem.HuginPrefab) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            string copy;
            if (drop == null || !p.m_customData.TryGetValue(CopyPrefix + id, out copy)) return Lang.T("Не вдалося повернути ворона.");

            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = 1;
            item.m_equipped = false;
            item.m_customData = Unpack(copy.Substring(Math.Min(2, copy.Length)));
            int gen = Mathf.Max(Mathf.Max(Gen(item), ProfileGen(p, id)), WorldGen(id)) + 1;
            item.m_customData[IdKey] = id;
            item.m_customData[GenKey] = gen.ToString();
            item.m_customData.Remove("RavenWatch_worn"); // the second belt's mark: it is in the bag, not worn
            Outfits.StripCrafted(item); // the things from the bag and the pouch's food: not twice
            Trust.Returned(item);  // the level stays, the bar is empty, it is home and not on its way anywhere
            Memory.Forget(item);   // some of what it remembered is lost on the way
            if (!p.GetInventory().AddItem(item)) return Lang.T("У сумці нема місця для ворона.");

            p.GetInventory().RemoveItem(Trust.FindName(p, l.Price.Prefab), l.Price.Count, -1, false);
            p.m_customData[GenPrefix + id] = gen.ToString();
            p.m_customData[BackPrefix + id] = Memory.Day().ToString();
            if (ZoneSystem.instance != null) ZoneSystem.instance.SetGlobalKey(WorldKey(id, gen)); // every other copy is now an echo
            Save(p, item);
            RitualFx.Play(p, l.Munin, l.Price.Prefab);
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            LastChance.Ring(chest, new Color(1f, 0.9f, 0.6f, 1f), l.Munin ? new Color(0.7f, 0.1f, 0.1f, 1f) : new Color(0.3f, 0.5f, 0.9f, 1f));
            return Lang.F("{0} повернувся.\nВін у сумці: посади його на плече.", name);
        }
    }
}
#endif

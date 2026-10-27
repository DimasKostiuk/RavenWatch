#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Оглядає обнову» (the user, 2026-10-27): you put on a thing you never wore before (armour, a weapon, a shield) and a
    // raven on your shoulder looks you over — at you first, then askance at the new thing — and has a word on it (Munin
    // finds fault, Hugin weighs it up). Not on every change: only a thing this character never wore (kept in the player,
    // the gear in the bag when this first runs counts as worn), a whole set put on is one look (Settle after the last),
    // at most once every PauseMinutes, never in a fight.
    internal static class NewGear
    {
        internal static float Settle = 4f, PauseMinutes = 8f, WordWait = 20f;
        internal const string Key = "RavenWatch_wornGear"; // the player's: the things worn ever ("prefab|prefab|…", the last Kept)
        private const int Kept = 400;

        private static readonly HashSet<string> Worn = new HashSet<string>();
        private static readonly List<string> Order = new List<string>(); // (the same, oldest first: the oldest go past Kept)
        private static long _wornOf = -1;          // whose list is read
        private static long _liveOf = -1;          // the character the tick has seen in the world (before: the load's own re-equipping, not counted)
        private static string _thing;              // the newest new thing waiting for its look (its name)
        private static bool _weapon;
        private static float _lookAt = -1f, _nextLook, _wordUntil, _askanceAt = -1f;
        private static bool _askanceMunin;         // the second look (a raven has one act waiting: the next is asked for later)
        private static string _word;               // the word waiting for the director
        private static bool _wordMunin;

        // the slots that make a look: armour, weapons, a shield (not tools, torches, a cape's trinkets)
        private static bool Counts(ItemDrop.ItemData item, out bool weapon)
        {
            weapon = false;
            if (item == null || item.m_shared == null) return false;
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet: case ItemDrop.ItemData.ItemType.Chest: case ItemDrop.ItemData.ItemType.Legs: case ItemDrop.ItemData.ItemType.Shoulder: return true;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon: case ItemDrop.ItemData.ItemType.TwoHandedWeapon: case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow: case ItemDrop.ItemData.ItemType.Shield: weapon = true; return true;
            }
            return false;
        }

        private static string Id(ItemDrop.ItemData item) { return item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name; }

        // the character's list, read once; the first time ever: everything in the bag counts as worn already
        private static void Load(Player p)
        {
            long id = p.GetPlayerID();
            if (_wornOf == id) return;
            _wornOf = id;
            Worn.Clear(); Order.Clear();
            string raw;
            if (p.m_customData.TryGetValue(Key, out raw)) { foreach (string w in raw.Split('|')) if (w.Length > 0 && Worn.Add(w)) Order.Add(w); return; }
            bool weapon;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (Counts(it, out weapon) && Worn.Add(Id(it))) Order.Add(Id(it));
            Save(p);
        }

        private static void Save(Player p)
        {
            while (Order.Count > Kept) { Worn.Remove(Order[0]); Order.RemoveAt(0); }
            p.m_customData[Key] = string.Join("|", Order.ToArray());
        }

        // a thing put on (the equip patch): new to this character → a look soon
        internal static void Equipped(Player p, ItemDrop.ItemData item)
        {
            bool weapon;
            if (p == null || p != Player.m_localPlayer || _liveOf != p.GetPlayerID() || !Counts(item, out weapon)) return;
            Load(p);
            string id = Id(item);
            if (!Worn.Add(id)) return; // (worn before: no news)
            Order.Add(id);
            Save(p);
            _thing = Lang.Game(item.m_shared.m_name);
            _weapon = weapon;
            _lookAt = Time.time + Settle; // (a whole set: one look after the last piece)
        }

        // twice a second (RavenSkills)
        internal static void Tick(Player p)
        {
            if (p == null) return;
            _liveOf = p.GetPlayerID();
            float now = Time.time;
            if (_askanceAt > 0f && now >= _askanceAt)
            {
                _askanceAt = -1f;
                ShoulderLife l2 = Shoulder.LocalLife(_askanceMunin);
                if (l2 != null && l2.SitNew && !l2.Hovering) l2.SitDirectorNow.React(l2, _askanceMunin ? "Косить оком" : "Розглядає знахідку", 0f); // …then askance at the thing
            }
            if (_word != null)
            {
                if (now > _wordUntil) _word = null;
                else if (!Senses.Fight && PerkFx.Say(_wordMunin, _word, 1f, 0f, "thing", _thing ?? "")) _word = null;
            }
            if (_lookAt < 0f || now < _lookAt) return;
            _lookAt = -1f;
            if (now < _nextLook || p.IsDead() || Senses.Fight || p.IsSleeping()) return; // (too soon after the last look: this one is let go)
            bool munin;
            if (!Pick(p, out munin)) return;
            _nextLook = now + PauseMinutes * 60f;
            ShoulderLife life = Shoulder.LocalLife(munin);
            if (life != null && life.SitNew && !life.Hovering)
            {
                life.SitDirectorNow.React(life, "Досліджує тебе", 0.2f); // at you…
                _askanceAt = now + 2.4f; _askanceMunin = munin;
            }
            _word = _weapon ? "gear_look_weapon" : "gear_look_armor";
            _wordMunin = munin;
            _wordUntil = now + WordWait;
            if (Plugin.Instance != null) Plugin.Instance.Log("New gear: " + Plugin.RavenName(munin) + " looks you over (" + _thing + ")");
        }

        // a raven awake on your shoulder; of two, either
        private static bool Pick(Player p, out bool munin)
        {
            bool h = Ready(p, false), m = Ready(p, true);
            munin = h && m ? UnityEngine.Random.value < 0.5f : m;
            return h || m;
        }

        private static bool Ready(Player p, bool munin)
        {
            ItemDrop.ItemData item = Trust.Worn(munin);
            return item != null && Trust.Active(item) && !Trust.Away(item) && !Trust.Stranger(item, p);
        }
    }

    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class NewGearEquipPatch
    {
        private static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        { try {
            if (__result && __instance == Player.m_localPlayer) NewGear.Equipped((Player)__instance, item);
        } catch (Exception e) { PatchGuard.Fail("NewGearEquipPatch", e); } }
    }
}
#endif

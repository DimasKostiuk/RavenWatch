#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Поклик Одіна»: a stone Haldor sells (assets/items/poklyk_odina.txt). Used, it asks which raven goes (the card:
    // RavenBook.ShowRavenPick); that one flies off to Odin for a few minutes and comes back in one of his gifts it missed
    // after the rituals (Outfits.CallPool). One trip on each trust level from the wardrobe's on (3 to 7: five a raven),
    // so every level has its chance at its own better hats; the stone is spent only when a raven flies. The take-off
    // is kept in the raven (TakeOffKey, world time) and done by Tick, so a logout during the rite still sends it.
    internal static class OdinCall
    {
        internal const string Why = "odincall"; // Trust's away reason
        internal const string StoneId = "poklyk_odina";
        internal static string StonePrefab { get { return WsItems.PrefabPrefix + StoneId; } }
        internal static float AwaySeconds = 300f;
        private const string LevelsKey = "RavenWatch_odinCallLevels"; // in the raven item: the trust levels it flew at, comma-separated
        private const string TakeOffKey = "RavenWatch_odinCallAt";     // in the raven item: world time it takes off (the rite shown till then)

        internal static bool IsStone(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null && item.m_dropPrefab.name == StonePrefab;
        }

        private static List<int> Flown(ItemDrop.ItemData raven)
        {
            List<int> l = new List<int>();
            string v;
            if (raven != null && raven.m_customData != null && raven.m_customData.TryGetValue(LevelsKey, out v))
                foreach (string s in v.Split(',')) { int n; if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) l.Add(n); }
            return l;
        }

        internal static bool FlownAt(ItemDrop.ItemData raven, int level) { return Flown(raven).Contains(level); }

        // why this raven cannot go now; null: it can
        internal static string WhyNot(Player p, bool munin)
        {
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (raven == null) return Lang.T("не на плечі");
            if (Trust.Stranger(raven, p)) return Lang.T("не твій");
            if (!Trust.Active(raven)) return Lang.T("спить");
            if (Trust.SettledLevel(raven) < Outfits.WardrobeLevel) return Lang.F("з {0}-го рівня довіри", Outfits.WardrobeLevel);
            if (Trust.Away(raven) || Trust.Leaving(raven) || (raven.m_customData != null && raven.m_customData.ContainsKey(TakeOffKey))) return Lang.T("зараз не з тобою");
            if (FlownAt(raven, Trust.SettledLevel(raven))) return Lang.T("на цьому рівні вже літав");
            if (Outfits.CallPool(raven).Count == 0) return Lang.T("Одін уже все віддав");
            return null;
        }

        // the stone used: the card, if a raven can take off from here at all
        internal static void Use(Player p)
        {
            if (p == null || p != Player.m_localPlayer || FateRite.Busy) return; // (a stone still at work: this one waits)
            if (Trust.Worn(false) == null && Trust.Worn(true) == null) { RavenMsg.Center(p, Lang.T("Посади ворона на плече —\nдо Одіна він летить звідти.")); return; }
            if (!Trust.FreeToLeave(p, false)) { RavenMsg.Center(p, Lang.T("Під дахом, під землею чи в бою\nворон до Одіна не злетить.")); return; }
            if (InventoryGui.instance != null && InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            RavenBook.ShowRavenPick(new RavenPick
            {
                Title = Lang.T("Поклик Одіна"),
                Say = Lang.T("Стисни камінь — і ворон полетить до Одіна. Повернеться за кілька хвилин у новій шапці."),
                Sub = Lang.T("Раз на кожному рівні довіри, з 3-го по 7-й. Обери, хто полетить:"),
                WhyNot = munin => WhyNot(p, munin),
                Note = munin => Lang.F("можна летіти · рівень {0}", Trust.SettledLevel(Trust.Worn(munin))),
                Act = munin => Send(p, munin),
            });
        }

        // the raven picked on the card: the stone spent, the hat chosen, off it flies; null, or why not
        internal static string Send(Player p, bool munin)
        {
            if (p == null) return null;
            string why = WhyNot(p, munin);
            if (why != null) return Plugin.RavenName(munin) + ": " + why + ".";
            if (!Trust.FreeToLeave(p, false)) return Lang.T("Під дахом, під землею чи в бою\nворон до Одіна не злетить.");
            Inventory inv = p.GetInventory();
            ItemDrop.ItemData stone = null;
            foreach (ItemDrop.ItemData it in inv.GetAllItems()) if (IsStone(it)) { stone = it; break; }
            if (stone == null) return Lang.T("Поклику Одіна в сумці нема.");
            ItemDrop.ItemData raven = Trust.Worn(munin);
            if (Outfits.CallGift(munin, raven) == null) return Plugin.RavenName(munin) + ": " + Lang.T("Одін уже все віддав") + ".";
            inv.RemoveOneItem(stone);
            List<int> flown = Flown(raven);
            flown.Add(Trust.SettledLevel(raven));
            raven.m_customData[LevelsKey] = string.Join(",", flown.ConvertAll(n => n.ToString(CultureInfo.InvariantCulture)).ToArray());
            raven.m_customData[TakeOffKey] = (WorldTime.Now + FateRite.CallOffAt).ToString("R", CultureInfo.InvariantCulture);
            Backup.Save(p, raven); // the copy knows this level's trip is used
            if (Plugin.Instance != null) Plugin.Instance.Log("Odin's call: " + Plugin.RavenName(munin) + " flies to Odin (trust level " + Trust.SettledLevel(raven) + ")");
            FateRite.PlayCall(p, munin); // shown as the Fate stone is, in Odin's gold; it takes off once it has shaken itself (Tick)
            return null;
        }

        // twice a second for each worn raven (Trust.Tick): the rite over, off it flies
        internal static void Tick(Player p, bool munin, ItemDrop.ItemData raven)
        {
            string v; double at;
            if (raven == null || raven.m_customData == null || !raven.m_customData.TryGetValue(TakeOffKey, out v)) return;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out at) && WorldTime.Now < at && at - WorldTime.Now <= WorldTime.MaxAhead) return;
            raven.m_customData.Remove(TakeOffKey);
            Trust.SendAway(raven, AwaySeconds, Why);
            string said = Lang.F("{0} полетів до Одіна.\nПовернеться за {1} — у новій шапці.", Plugin.RavenName(munin), RavenAbilities.Span(AwaySeconds));
            if (Perches.On(raven)) Perches.ToOdin(p, raven, munin, said); // sat down on a perch during the rite: off from there
            else if (p != null) RavenMsg.Center(p, said, 2);
        }

        // back (Trust.Wander): the hat is on it already
        internal static void Back(Player p, bool munin, string gift)
        {
            if (p != null)
                RavenMsg.Center(p, Lang.F("{0} повернувся від Одіна.", Plugin.RavenName(munin)) + (gift != null ? Lang.F("\nОдін передав йому: {0}.", gift) : ""));
            RavenBook.Again();
        }
    }

    // a right click on the stone (in the bag or on the hotbar): the card instead of the game's "can't use"
    [HarmonyPatch(typeof(Humanoid), "UseItem")]
    internal static class OdinCallUsePatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData __1)
        { try {
            if (!OdinCall.IsStone(__1)) return true;
            OdinCall.Use(__instance as Player);
            return false;
        } catch (Exception e) { PatchGuard.Fail("OdinCallUsePatch", e); return true; } }
    }
}
#endif

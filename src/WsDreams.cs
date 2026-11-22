#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A worn thing's own dreams (an item's «dream: …» lines; the user 2026-11-21: Sverre's ring, the copy of Draupnir «Брехливий» — «завжди якщо вдітий
    // перстень то мають бути лише ці сни»). The game dreams through DreamTexts.GetRandomDreamText (SleepText asks it 4 s into a sleep, shows the text 6.5 s):
    // one of its own, now and then. While the local player wears an item with dreams, every sleep dreams — one of that item's lines, never the game's.
    // The lines go round in a shuffled order, none twice before all were dreamt (session: a new game starts a new round). Translated as shown (Lang.T).
    internal static class WsDreams
    {
        private static readonly Dictionary<string, List<int>> Round = new Dictionary<string, List<int>>(); // session: the item's lines not dreamt yet this round
        private static int _last = -1;

        // a dream of what the player wears now (null: nothing worn dreams — the game's own)
        internal static string Now()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetEquippedItems())
            {
                WsModel m = it.m_dropPrefab != null ? WsItems.ModelOf(it.m_dropPrefab.name) : null;
                if (m == null || m.Dreams.Count == 0) continue;
                List<int> left;
                if (!Round.TryGetValue(m.Id, out left) || left.Count == 0)
                {
                    left = new List<int>();
                    for (int i = 0; i < m.Dreams.Count; i++) left.Add(i);
                    for (int i = left.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); int t = left[i]; left[i] = left[j]; left[j] = t; }
                    if (left.Count > 1 && left[left.Count - 1] == _last) { int t = left[0]; left[0] = left[left.Count - 1]; left[left.Count - 1] = t; } // (the last of a round never opens the next)
                    Round[m.Id] = left;
                }
                int k = left[left.Count - 1];
                left.RemoveAt(left.Count - 1);
                _last = k;
                return Lang.T(m.Dreams[k]);
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(DreamTexts), "GetRandomDreamText")]
    internal static class WsDreamsPatch
    {
        private static bool Prefix(ref DreamTexts.DreamText __result)
        {
            try
            {
                string text = WsDreams.Now();
                if (text == null) return true;
                __result = new DreamTexts.DreamText { m_text = text, m_chanceToDream = 1f };
                return false;
            }
            catch (Exception e) { PatchGuard.Fail("WsDreamsPatch", e); return true; }
        }
    }
}
#endif

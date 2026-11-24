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

    // What a worn thing leaves in the morning (an item's «morning: stamina 20»; the user 2026-11-23: Sverre's ring — after a sleep with it, stamina
    // comes back 20% faster, nothing else). The player woke with it worn (the game's own waking, Player.SetSleeping: SleepPatch in Diary.cs, right after
    // the game gave «Rested») — the effect is on him while «Rested» lasts and the thing stays worn (WsMorningSE.IsDone, asked by the game's SEMan each
    // frame: two cheap looks, no clock of ours). No sleep — no effect; put on again after taking off — none till the next morning. The game saves no
    // status effects: after a new login it is gone together with «Rested». The number is the game's own kind («Stamina regen +20%», added to the
    // multiplier as every SE_Stats does), and the game writes that line in the tooltip itself.
    internal static class WsMorning
    {
        private const string EffectName = "RavenWatch_Morning";
        private static readonly int Hash = EffectName.GetStableHashCode();
        private static WsMorningSE _template;

        internal static void Woke(Player p)
        {
            foreach (ItemDrop.ItemData it in p.GetInventory().GetEquippedItems())
            {
                WsModel m = it.m_dropPrefab != null ? WsItems.ModelOf(it.m_dropPrefab.name) : null;
                if (m == null || m.MorningStamina <= 0f) continue; // (a line not read was told in the log with its file)
                SEMan sm = p.GetSEMan();
                sm.RemoveStatusEffect(Hash, true); // (yesterday's still on: this morning's anew, with this thing)
                WsMorningSE se = RavenFx.Template(ref _template, EffectName);
                se.m_name = it.m_shared.m_name;
                se.m_tooltip = Lang.T("Виспався весело. Тримається, поки ти відпочилий і не зняв цю річ.");
                se.m_icon = it.GetIcon();
                se.m_staminaRegenMultiplier = 1f + m.MorningStamina / 100f;
                WsMorningSE on = sm.AddStatusEffect(se, true, 0, 0f, 0) as WsMorningSE;
                if (on != null) on.Worn = it;
                return; // (one morning effect: the first worn thing that has one)
            }
        }
    }

    // the morning effect itself: over when «Rested» is over or its thing is no longer worn; shows the time «Rested» has left
    internal class WsMorningSE : SE_Stats
    {
        internal ItemDrop.ItemData Worn; // session: the worn item this morning's effect came from (a status effect is not saved)

        private StatusEffect Rested { get { return m_character != null ? m_character.GetSEMan().GetStatusEffect(SEMan.s_statusEffectRested) : null; } }

        public override bool IsDone() { return Worn == null || !Worn.m_equipped || Rested == null; }

        public override string GetIconText() { StatusEffect r = Rested; return r != null ? r.GetIconText() : ""; }
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

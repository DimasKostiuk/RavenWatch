#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The world's news for the people (npc-plan.md, «новини світу»), kept as their memories (NpcMemory) — so they fade by
    // themselves in a few days and the story can put its own in the same way:
    //   a boss fallen      news_boss_<eikthyr|elder|bonemass|moder|yagluth|queen|fader> — every resident, from the world's
    //                      keys (only those fallen since it came: a newcomer is no news of old bosses)
    //   your first steps   you_land_<blackforest|swamp|mountain|plains|mistlands|ashlands|deepnorth|ocean> — told to the
    //   in a land          first resident you meet after (and its heart's people), about you only
    //   a newcomer         news_newcomer (what: the name) — to those already living at the heart (NpcResident.Settle)
    //   a raid now         raid_now — not kept: said while one is on round its base (NpcVoice)
    //   the story          !news <kind> [what] in a talk (to this person and its heart's) · NpcNews.Tell(a, kind, what)
    // A kind news_* fades in five days (NpcMemory.KindOf); its words: mem_<kind> themes (NpcPhrases).
    internal static class NpcNews
    {
        internal static readonly int KKeys = "RavenWatch_npcNewsKeys".GetStableHashCode(); // int on the person: the bosses' keys it knows of (bits)
        private static readonly string[,] Bosses =
        {
            { "defeated_eikthyr", "eikthyr" }, { "defeated_gdking", "elder" }, { "defeated_bonemass", "bonemass" }, { "defeated_dragon", "moder" },
            { "defeated_goblinking", "yagluth" }, { "defeated_queen", "queen" }, { "defeated_fader", "fader" },
        };

        // ---------------------------------------------------------------- a boss fallen (where the person is owned, once a minute)
        private static readonly Dictionary<NpcActor, float> NextLook = new Dictionary<NpcActor, float>();
        internal static void Tick(NpcActor a)
        {
            float at;
            if (a == null || !a.Owned || a.Zdo == null || ZoneSystem.instance == null || (NextLook.TryGetValue(a, out at) && Time.time < at)) return;
            if (NextLook.Count > 128) NextLook.Clear();
            NextLook[a] = Time.time + 60f;
            int now = 0;
            for (int i = 0; i < Bosses.GetLength(0); i++) if (ZoneSystem.instance.GetGlobalKey(Bosses[i, 0])) now |= 1 << i;
            int known = NpcLocker.Int(a.Zdo, KKeys, -1);
            if (known == now) return;
            if (!NpcResident.Is(a) || known == -1) { NpcLocker.Put(a.Zdo, KKeys, now); return; } // (not living anywhere, or new to it all: what fell meanwhile is no news — no burst when it settles again)
            if (NpcLocker.Get(a, NpcMemory.KMemory) == null) return; // (its memory not here yet: the news waits, not lost)
            NpcLocker.Put(a.Zdo, KKeys, now);
            for (int i = 0; i < Bosses.GetLength(0); i++) if ((now & ~known & (1 << i)) != 0) NpcMemory.Remember(a, "news_boss_" + Bosses[i, 1], 0L, "");
        }

        // ---------------------------------------------------------------- your first steps in a land (your game, now and then)
        private static readonly FieldInfo KnownBiome = AccessTools.Field(typeof(Player), "m_knownBiome");
        private const string KLands = "RavenWatch_newsLands2", KPending = "RavenWatch_newsLandsNew"; // the player's own: the lands told or known before (the game's biome names, comma) · seen, not told yet
        private static float _nextLands;

        internal static void Lands()
        {
            if (Time.time < _nextLands) return;
            _nextLands = Time.time + 10f;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            // the game keeps the lands you know in your own language («Чорний ліс»): ours by its biome names (BlackForest)
            string kept;
            if (!p.m_customData.TryGetValue(KLands, out kept)) // (the first look, at once: what you knew before is no news)
            {
                HashSet<string> known = KnownBiome != null ? KnownBiome.GetValue(p) as HashSet<string> : null;
                List<string> seed = new List<string>();
                foreach (Heightmap.Biome b in Enum.GetValues(typeof(Heightmap.Biome))) if (Knows(known, b)) seed.Add(b.ToString());
                string here0 = p.GetCurrentBiome().ToString();
                if (!seed.Contains(here0)) seed.Add(here0);
                p.m_customData[KLands] = string.Join(",", seed.ToArray());
                return;
            }
            List<string> told = Split(kept);
            string pend;
            List<string> pending = Split(p.m_customData.TryGetValue(KPending, out pend) ? pend : "");
            Heightmap.Biome now = p.GetCurrentBiome();
            string here = now.ToString();
            if (now != Heightmap.Biome.None && !told.Contains(here) && !pending.Contains(here)) { pending.Add(here); p.m_customData[KPending] = string.Join(",", pending.ToArray()); } // (a land new to you: kept till you meet your people — a restart too)
            if (pending.Count == 0) return;
            NpcActor near = null; float best = 15f * 15f; // (told to the first of your people you meet: till then it waits)
            foreach (NpcActor o in NpcActor.Live) { if (o == null || !NpcResident.Settled(o)) continue; float d = (o.transform.position - p.transform.position).sqrMagnitude; if (d < best) { best = d; near = o; } }
            if (near == null) return;
            foreach (string land in pending)
            {
                string kind = "you_land_" + land.ToLowerInvariant();
                NpcMemory.Tell(near, kind, p.GetPlayerID(), "");
                NpcMemory.TellHome(near, kind, p.GetPlayerID(), "");
                told.Add(land);
            }
            p.m_customData[KLands] = string.Join(",", told.ToArray());
            p.m_customData[KPending] = "";
        }

        private static List<string> Split(string s) { return new List<string>(s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)); }

        // a land the game says you know: its name in your language among them («$biome_blackforest» → «Чорний ліс»)
        private static bool Knows(HashSet<string> known, Heightmap.Biome b)
        {
            if (known == null || b == Heightmap.Biome.None) return false;
            string id = b.ToString();
            string local = Localization.instance != null ? Localization.instance.Localize("$biome_" + id.ToLowerInvariant()) : id;
            return known.Contains(local) || known.Contains(id);
        }

        // ---------------------------------------------------------------- a raid round its base now (NpcVoice.Sources)
        internal static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            if (RaidNear(a)) themes.Add(new KeyValuePair<string, float>("raid_now", 75f));
        }

        internal static bool RaidNear(NpcActor a)
        {
            RandomEvent ev = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            return ev != null && a != null && NpcResident.Is(a) && Utils.DistanceXZ(ev.m_pos, NpcRules.Home(a)) < ev.m_eventRange + 30f;
        }

        // ---------------------------------------------------------------- the story's news: this person and its heart's
        internal static void Tell(NpcActor a, string kind, string what)
        {
            if (a == null || string.IsNullOrEmpty(kind)) return;
            if (!kind.StartsWith("news_", StringComparison.Ordinal)) kind = "news_" + kind;
            NpcMemory.Tell(a, kind, 0L, what);
            NpcMemory.TellHome(a, kind, 0L, what);
        }
    }
}
#endif

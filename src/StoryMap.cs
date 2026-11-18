#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's marks on the map (the user 2026-11-01): the mod's main tasks — the Hall of the Watch, and the story's
    // quests after it — wear a golden «!» with a golden ring round the place, not the blue of Odin's errands (ErrandMap).
    // Whoever has a main task asks for its mark by a key (Set) and takes it away (Clear); nothing is saved in the game's
    // map: the marks are drawn again on a new map (entering the world again).
    internal static class StoryMap
    {
        internal static readonly Color Mark = new Color(1f, 0.8f, 0.3f, 1f);     // the «!» and the name
        internal static readonly Color Ring = new Color(1f, 0.78f, 0.3f, 0.8f);  // the ring round the place

        private sealed class Entry { internal Vector3 At; internal string Label; internal float Radius; internal bool Bare; internal Minimap.PinData Mark, Ring; }
        private static readonly Dictionary<string, Entry> Marks = new Dictionary<string, Entry>();
        private static Minimap _map;

        internal static int Count { get { return Marks.Count; } }

        // a main task's mark here (the same key again: moved or renamed, drawn anew only when it changed). bare: its ring alone, no «!» (one is in the
        // search's ring already — the user 2026-11-15: «коли заходжу у зону, прибирати знак оклику, повертати, коли покинув зону»)
        internal static void Set(string key, Vector3 at, string label, float radius, bool bare = false)
        {
            if (Minimap.instance == null) return;
            Fresh();
            Entry e;
            if (Marks.TryGetValue(key, out e))
            {
                if ((e.At - at).sqrMagnitude < 0.25f && e.Label == label && Mathf.Approximately(e.Radius, radius) && e.Bare == bare) return;
                Remove(e);
            }
            e = new Entry { At = at, Label = label, Radius = radius, Bare = bare };
            if (radius > 0f)
            {
                e.Ring = Minimap.instance.AddPin(at, Minimap.PinType.EventArea, "", false, false, 0L, default(Splatform.PlatformUserID));
                e.Ring.m_worldSize = radius * 2f;
                e.Ring.m_icon = ErrandMap.GreyOf(e.Ring.m_icon); // (coloured gold in Tint)
            }
            if (!bare || radius <= 0f)
            {
                e.Mark = Minimap.instance.AddPin(at, Minimap.PinType.RandomEvent, label, false, false, 0L, default(Splatform.PlatformUserID));
                e.Mark.m_animate = true;
                e.Mark.m_icon = ErrandMap.GreyOf(e.Mark.m_icon);
            }
            Marks[key] = e;
        }

        // where a mark stands now (false: none under that key) — the book's «на мапі»
        internal static bool Where(string key, out Vector3 at)
        {
            Entry e;
            at = Vector3.zero;
            if (_map != Minimap.instance || !Marks.TryGetValue(key, out e)) return false;
            at = e.At;
            return true;
        }

        internal static void Clear(string key)
        {
            Entry e;
            if (!Marks.TryGetValue(key, out e)) return;
            Marks.Remove(key);
            if (_map == Minimap.instance) Remove(e);
        }

        // after the map's redraw (ErrandMapTintPatch): ours in gold
        internal static void Tint()
        {
            foreach (Entry e in Marks.Values)
            {
                Paint(e.Ring, Ring);
                Paint(e.Mark, Mark);
            }
        }

        private static void Paint(Minimap.PinData p, Color c)
        {
            if (p == null) return;
            if (p.m_iconElement != null && p.m_iconElement.color != c) p.m_iconElement.color = c;
            if (p.m_NamePinData != null && p.m_NamePinData.PinNameText != null && p.m_NamePinData.PinNameText.color != c) p.m_NamePinData.PinNameText.color = c;
        }

        // a new map (entered the world again): the old marks went with the old one
        private static void Fresh()
        {
            if (_map == Minimap.instance) return;
            _map = Minimap.instance;
            Marks.Clear();
        }

        private static void Remove(Entry e)
        {
            if (Minimap.instance == null) return;
            if (e.Ring != null) Minimap.instance.RemovePin(e.Ring);
            if (e.Mark != null) Minimap.instance.RemovePin(e.Mark);
        }
    }
}
#endif

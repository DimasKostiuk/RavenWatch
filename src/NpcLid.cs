#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A chest opened by a person as a player opens one (npc-plan.md): its lid up and its sound (the game's own SetInUse —
    // every game sees the lid through its ZDO), for the moment it takes or puts something, then down with its sound. The
    // game that runs the chest writes «open» from its own flag every second, so a lid can only stay up while this game
    // holds it: here every lid is shut when its moment is over (cut short — a fight, an order — too). While it is up the
    // chest is «in use» for players and for other people (they wait), not for the one who opened it.
    internal static class NpcLid
    {
        private sealed class Up { public NpcActor By; public float Until; }
        private static readonly Dictionary<Container, Up> Open = new Dictionary<Container, Up>();
        private static readonly List<Container> Shut = new List<Container>();

        // up for a moment (where the chest is run here; the same one opening it again: a longer moment)
        internal static void Peek(Container c, NpcActor by, float seconds)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            Up up;
            if (Open.TryGetValue(c, out up)) { if (up.By == by) up.Until = Mathf.Max(up.Until, Time.time + seconds); return; }
            if (c.IsInUse()) return; // (a player has it open)
            Open[c] = new Up { By = by, Until = Time.time + seconds };
            c.SetInUse(true);
        }

        // down now, with its sound
        internal static void Close(Container c)
        {
            if (c == null || !Open.Remove(c)) return;
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid() && nv.IsOwner()) c.SetInUse(false);
        }

        // opened by this one (its own moment: not a reason to wait)
        internal static bool Mine(Container c, NpcActor by) { Up up; return c != null && Open.TryGetValue(c, out up) && up.By == by; }

        // every lid whose moment is over: down (a chest gone meanwhile: nothing to do — loaded again, it is shut)
        internal static void Tick()
        {
            if (Open.Count == 0) return;
            Shut.Clear();
            foreach (KeyValuePair<Container, Up> kv in Open) if (kv.Key == null || Time.time >= kv.Value.Until || kv.Value.By == null) Shut.Add(kv.Key);
            foreach (Container c in Shut) { if (c == null) Open.Remove(c); else Close(c); }
        }
    }
}
#endif

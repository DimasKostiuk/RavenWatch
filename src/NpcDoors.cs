#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Doors for a person (npc-plan.md): the game's path finding takes a closed door for a wall, so a person behind a
    // shut gate stood and was stepped through at last. Now, when it has no way to where it goes, it looks for a door
    // that leads there (one it can reach on its own side, the shortest way round), walks to it, opens it as a player
    // does (the door's own message: every game sees it open), goes through and shuts it behind itself — only a door it
    // opened. A door with a key, or under a ward the player of this game may not use, is left shut. (NpcMind.ThroughDoor)
    internal static class NpcDoors
    {
        private const float Look = 45f, Again = 5f;
        private static readonly Dictionary<string, bool> IsDoor = new Dictionary<string, bool>(); // a kind of piece → has a door
        private static readonly Dictionary<long, KeyValuePair<float, List<Door>>> Seen = new Dictionary<long, KeyValuePair<float, List<Door>>>();
        private static object _world;

        // the doors round a spot (one look per 32 m square every few seconds)
        internal static List<Door> Around(Vector3 at) { return Near(at); } // (a servant's house: its doors)

        private static readonly int PerfDoors = NpcPerf.Id("door search (пошук дверей)");
        private static List<Door> Near(Vector3 at)
        {
            long perf = NpcPerf.Now();
            try { return Near0(at); } finally { NpcPerf.Add(PerfDoors, perf); }
        }

        private static List<Door> Near0(Vector3 at)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { Seen.Clear(); _world = ZNet.instance; }
            long key = ((long)Mathf.FloorToInt(at.x / 32f) << 32) ^ (uint)Mathf.FloorToInt(at.z / 32f);
            KeyValuePair<float, List<Door>> had;
            if (Seen.TryGetValue(key, out had) && Time.time - had.Key < Again) return had.Value;
            List<Door> l = new List<Door>();
            NpcBase b = NpcBase.Covering(at); // (a base's own look round it: its doors, the pieces near not gone over)
            if (b != null) { foreach (Component c in b.Round(typeof(Door))) { Door d = c as Door; if (d != null && Utils.DistanceXZ(d.transform.position, at) <= Look) l.Add(d); } }
            else
            {
                List<Piece> pieces = new List<Piece>();
                Piece.GetAllPiecesInRadius(at, Look, pieces); // heavy: only where no base looks round, once a 32 m square every Again s
                foreach (Piece pc in pieces)
                {
                    if (pc == null) continue;
                    bool door;
                    if (!IsDoor.TryGetValue(pc.m_name ?? "", out door)) { door = pc.GetComponentInChildren<Door>() != null; IsDoor[pc.m_name ?? ""] = door; } // (asked once a kind: a big base has thousands of pieces)
                    if (door) { Door d = pc.GetComponentInChildren<Door>(); if (d != null) l.Add(d); }
                }
            }
            if (Seen.Count > 64) Seen.Clear();
            Seen[key] = new KeyValuePair<float, List<Door>>(Time.time, l);
            return l;
        }

        // a door that leads from here towards there: reachable on this side, the shortest way round first (m: who goes: a door shut to it is none)
        internal static Door Toward(Vector3 from, Vector3 to, Pathfinding.AgentType agent, HashSet<Door> tried = null, NpcMind m = null)
        {
            if (Pathfinding.instance == null) return null;
            List<Door> doors = new List<Door>(Near(from));
            doors.RemoveAll(d => d == null || (tried != null && tried.Contains(d)) || !Usable(d, m));
            doors.Sort((a, b) => (Utils.DistanceXZ(from, a.transform.position) + Utils.DistanceXZ(a.transform.position, to)).CompareTo(Utils.DistanceXZ(from, b.transform.position) + Utils.DistanceXZ(b.transform.position, to)));
            for (int i = 0; i < doors.Count && i < 6; i++)
            {
                Vector3 near, far;
                Sides(doors[i], from, out near, out far);
                if (Utils.DistanceXZ(far, to) > Utils.DistanceXZ(from, to) + 2f) continue; // (it would lead away)
                if (NpcWalk.Have(from, near)) return doors[i];
            }
            return null;
        }

        // the spot before it on this side, and the one past it
        internal static void Sides(Door d, Vector3 from, out Vector3 near, out Vector3 far)
        {
            Vector3 n = d.transform.forward; n.y = 0f; n.Normalize();
            Vector3 c = d.transform.position;
            float side = Vector3.Dot(from - c, n) >= 0f ? 1f : -1f;
            near = c + n * side * 1.4f; far = c - n * side * 1.8f;
        }

        // a door a person may open: no key to it, not under a ward this game's player may not use, not a player's house
        // shut to it (NpcPrivate), and of the story's places only one the story has let be anyone's (LocState.OpenToPeople:
        // the Hall's gate once it is open: a player shut it behind him and Heid stood outside for good, 2026-11-04)
        internal static bool Usable(Door d, NpcMind m)
        {
            if (d == null || d.m_keyItem != null) return false;
            if (Player.m_localPlayer != null && !PrivateArea.CheckAccess(d.transform.position, 0f, false, false)) return false;
            if (m != null && NpcPrivate.DoorOff(m, d)) return false;
            Piece pc = d.GetComponentInParent<Piece>();
            return pc == null || !Blueprint.Marked(pc.GetComponent<ZNetView>()) || LocState.OpenToPeople(d);
        }

        internal static bool Closed(Door d)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() && nv.GetZDO().GetInt(ZDOVars.s_state, 0) == 0;
        }

        // The game's «use door» is a switch (shut → open, open → shut), and the door's state comes back from whoever holds
        // it a moment later: told twice in that moment, a door opened and shut again (2026-11-02: «they opened them and back
        // several times»). So a door told anything rests Settle s — nobody tells it again till its state has come back.
        internal static float Settle = 1.5f;
        private static readonly Dictionary<Door, float> _told = new Dictionary<Door, float>();
        private static bool Resting(Door d) { float at; return _told.TryGetValue(d, out at) && Time.time - at < Settle; }
        private static void Told(Door d, string what, Vector3 by)
        {
            if (_told.Count > 128) _told.Clear();
            _told[d] = Time.time;
            NpcWalk.DoorUsed(d, what, by);
        }

        // opened as a player opens it, from where it stands (true: it opened it)
        internal static bool Open(Door d, Vector3 by)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || !Closed(d) || Resting(d)) return false;
            Vector3 dir = (by - d.transform.position).normalized;
            nv.InvokeRPC("UseDoor", Vector3.Dot(d.transform.forward, dir) < 0f);
            Told(d, "open", by);
            return true;
        }

        // shut behind it — not while someone stands in the doorway (a person, a player: it would shut in their face and
        // be opened again); true: shut (or shut already)
        internal static bool Shut(Door d, Component by = null)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || Closed(d)) return true;
            if (Resting(d) || InDoorway(d, by != null ? by.gameObject : null)) return false;
            nv.InvokeRPC("UseDoor", false); // (an open door told again: shut)
            Told(d, "shut", d.transform.position);
            return true;
        }

        private static bool InDoorway(Door d, GameObject self)
        {
            Vector3 at = d.transform.position;
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.gameObject != self && Utils.DistanceXZ(o.transform.position, at) < 1.8f && Mathf.Abs(o.transform.position.y - at.y) < 2f) return true;
            foreach (Player p in Player.GetAllPlayers()) if (p != null && Utils.DistanceXZ(p.transform.position, at) < 1.8f && Mathf.Abs(p.transform.position.y - at.y) < 2f) return true;
            return false;
        }

        // which side of a door a point is on (its own forward: +1 / −1)
        internal static float Side(Door d, Vector3 p) { return Mathf.Sign(Vector3.Dot(d.transform.forward, p - d.transform.position)); }
    }
}
#endif

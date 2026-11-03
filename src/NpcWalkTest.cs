#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A walk test (raven npc walk test [minutes]): every resident near you goes round its base's places in turn — each
    // chest, each seat, each bed, each fire, a few far spots — a whim at a time, so the black box fills up while you only
    // stand by (god mode helps). Over after its minutes, or raven npc walk stop.
    internal static class NpcWalkTest
    {
        private static float _until;
        private static readonly Dictionary<NpcActor, int> Turn = new Dictionary<NpcActor, int>();
        private static readonly Dictionary<NpcActor, float> There = new Dictionary<NpcActor, float>();
        private static List<Vector3> _spots = new List<Vector3>();
        private static List<NpcMind.Task> _kinds = new List<NpcMind.Task>();

        internal static bool On { get { return Time.time < _until; } }

        internal static string Start(Player p, string minutes)
        {
            float m;
            if (!float.TryParse(minutes, NumberStyles.Float, CultureInfo.InvariantCulture, out m) || m <= 0f) m = 10f;
            if (p == null) return "Ні.";
            _spots.Clear(); _kinds.Clear(); Turn.Clear(); There.Clear();
            Vector3 at = p.transform.position;
            foreach (Collider c in Physics.OverlapSphere(at, 45f, LayerMask.GetMask("piece", "piece_nonsolid"), QueryTriggerInteraction.Collide))
            {
                Piece piece = c.GetComponentInParent<Piece>();
                if (piece == null) continue;
                Vector3 pos = piece.transform.position;
                bool seen = false; foreach (Vector3 s in _spots) if ((s - pos).sqrMagnitude < 0.25f) seen = true;
                if (seen) continue;
                if (Mathf.Abs(pos.y - at.y) > 3f) continue; // (on a roof, up a tower, upstairs: not this test)
                if (piece.GetComponentInChildren<Chair>() != null) { _spots.Add(pos); _kinds.Add(NpcMind.Task.Sit); }
                else if (piece.GetComponentInChildren<Container>() != null || piece.GetComponentInChildren<Bed>() != null || piece.GetComponentInChildren<Fireplace>() != null) { _spots.Add(pos); _kinds.Add(NpcMind.Task.Go); }
            }
            if (_spots.Count == 0) return "Поруч (45 м) нема ні скринь, ні сидінь, ні ліжок, ні вогнищ.";
            _until = Time.time + m * 60f;
            NpcWalk.Watch = true;
            NpcWalk.ClearTotals();
            NpcWalk.Write("walks.txt", "---- test " + m + " min, " + _spots.Count + " places ----");
            NpcWalk.Write("halts.txt", "---- test " + m + " min, " + _spots.Count + " places ----");
            return "Тест ходи: " + m + " хв, місць " + _spots.Count + ". Стій поруч; raven npc walk stop — кінець.";
        }

        internal static string Stop() { _until = 0f; Turn.Clear(); There.Clear(); return "Тест ходи закінчено."; }

        // where it is owned, now and then (NpcActor.Ticks): its whim done — the next place
        internal static void Tick(NpcActor a)
        {
            if (!On || a == null || !a.Owned || a.Mind == null || !NpcResident.Is(a) || _spots.Count == 0) return;
            NpcMind.Plan now = a.Mind.Of(NpcMind.Layer.Whim);
            if (now != null) // (there — stood a moment, sat a while: the next place)
            {
                bool there = now.Kind == NpcMind.Task.Idle || (now.Kind == NpcMind.Task.Sit && now.Seated);
                float since;
                if (!there) { There.Remove(a); return; }
                if (!There.TryGetValue(a, out since)) { There[a] = Time.time; return; }
                if (Time.time - since < (now.Kind == NpcMind.Task.Sit ? 6f : 2f)) return;
                There.Remove(a);
            }
            int i;
            Turn.TryGetValue(a, out i);
            Turn[a] = i + 1;
            int k = (i + (a.GetInstanceID() & 0xff)) % _spots.Count; // (each its own order through the places)
            NpcMind.Plan plan = _kinds[k] == NpcMind.Task.Sit
                ? new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = _spots[k] }
                : new NpcMind.Plan { Kind = NpcMind.Task.Go, To = _spots[k], Near = 1.2f };
            a.Mind.TakeWhim(plan, 60f, "walktest", true);
        }
    }
}
#endif

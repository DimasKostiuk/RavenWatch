#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A player's own house in a base (the user, 2026-10-31): a house (NpcHouse) with a bed a player sleeps in (the game's
    // owner of the bed — a resident is never given such a bed). Residents keep out of it at their ease: no walk, rest,
    // fire or wash spot in it, and its doors stay shut to them. Let in:
    //   one whose own bed is in it, a servant who keeps it, a worker whose chest stands in it
    //   its work and what it is told (the Job, Order, Fight, Scene layers), a meal, its sleep, a chore, a trip
    //   the heart's dining table when the player picked one in there (the plan is a guest's)
    //   now and then a guest of its own accord (the whim «guest», Visit): warms itself by the house's fire for a minute
    //   or two, once in a few game days, and only while no player is home
    // Looked over on the game that asks (physics only, nothing sent), its beds once a minute per base.
    internal static class NpcPrivate
    {
        private const float Again = 60f, HouseAgain = 900f, VisitDays = 3f; // (its beds once a minute; the house itself once in a quarter of an hour)
        private static readonly List<NpcHouse> None = new List<NpcHouse>();
        private static readonly Dictionary<string, KeyValuePair<float, List<NpcHouse>>> Seen = new Dictionary<string, KeyValuePair<float, List<NpcHouse>>>();
        private static readonly Dictionary<ZDOID, double> LastVisit = new Dictionary<ZDOID, double>();
        private static object _world;

        // the players' houses of a base (a house still being looked over counts once it is ready)
        internal static List<NpcHouse> Houses(NpcBase b)
        {
            if (b == null || !b.Ready) return None;
            if (!ReferenceEquals(_world, ZNet.instance)) { Seen.Clear(); LastVisit.Clear(); _world = ZNet.instance; }
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            KeyValuePair<float, List<NpcHouse>> had;
            if (Seen.TryGetValue(key, out had) && Time.time - had.Key < Again) return had.Value;
            List<NpcHouse> l = new List<NpcHouse>();
            foreach (Bed bed in NpcWorker.InBaseOf<Bed>(b, b.Centre))
            {
                ZNetView nv = bed != null ? bed.GetComponent<ZNetView>() : null;
                if (nv == null || !nv.IsValid() || nv.GetZDO().GetLong(ZDOVars.s_owner, 0L) == 0L) continue;
                bool known = false;
                foreach (NpcHouse h in l) if (h.Ready && h.Inside(bed.transform.position)) { known = true; break; }
                if (!known) { NpcHouse h = NpcHouse.For(bed.transform.position, HouseAgain); if (!l.Contains(h)) l.Add(h); }
            }
            if (Seen.Count > 50) Seen.Clear();
            Seen[key] = new KeyValuePair<float, List<NpcHouse>>(Time.time, l);
            return l;
        }

        // the player's house a spot is in (null: none)
        internal static NpcHouse At(NpcBase b, Vector3 p)
        {
            foreach (NpcHouse h in Houses(b)) if (h.Ready && !h.TooBig && h.Inside(p)) return h;
            return null;
        }

        // not for this one at its ease: in a player's house it is not let into
        internal static bool Off(NpcActor a, NpcBase b, Vector3 p)
        {
            if (NpcKit.UnderSky(p)) return false; // (nothing built over it: a yard, a fire in the open — no one's house, whatever the map of rooms says)
            NpcHouse h = At(b, p);
            return h != null && !LetIn(a, h);
        }

        private static bool LetIn(NpcActor a, NpcHouse h)
        {
            if (a == null) return false;
            GameObject bed = NpcKit.Find(NpcLocker.Ref(a.Zdo, NpcResident.KBed));
            if (bed != null && h.Inside(bed.transform.position)) return true;
            NpcWorker w = a.Worker;
            if (w == null) return false;
            Vector3 at;
            if (w.HouseAt(out at) && h.Inside(at)) return true;
            Container c = w.Chest;
            return c != null && h.Inside(c.transform.position);
        }

        // a spot in its base for a walk or a rest — not in a player's house (a few tries, else the base's middle)
        internal static Vector3 Spot(NpcActor a, NpcBase b)
        {
            for (int i = 0; i < 6; i++) { Vector3 s = b.Spot(); if (!Off(a, b, s)) return s; }
            return b.Centre;
        }

        // a shut door it may not open now: one into a player's house, it outside it, about something of its ease
        internal static bool DoorOff(NpcMind m, Door d)
        {
            NpcActor a = m.Actor;
            NpcResident r = a != null ? a.Resident : null;
            if (r == null || !NpcResident.Is(a)) return false; // (a traveller, a story figure: not ours to keep out)
            NpcMind.Layer layer = m.NowLayer;
            if (layer == NpcMind.Layer.Job || layer == NpcMind.Layer.Order || layer == NpcMind.Layer.Fight || layer == NpcMind.Layer.Scene) return false;
            NpcMind.Plan p = m.Current;
            if (p != null && (p.Guest || p.Kind == NpcMind.Task.Eat || p.Kind == NpcMind.Task.Sleep || p.Kind == NpcMind.Task.Chore || p.Kind == NpcMind.Task.Travel)) return false;
            NpcBase b = r.Base;
            foreach (NpcHouse h in Houses(b))
                if (h.Ready && !h.TooBig && h.Near(d.transform.position) && !h.Inside(m.transform.position) && !LetIn(a, h)) return true; // (inside already: it may always go out)
            return false;
        }

        // ---------------------------------------------------------------- a guest now and then (NpcWhims «guest»)
        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0d; } }
        private static double Day { get { return EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800d; } }

        internal static bool CanVisit(NpcResident r) { NpcHouse h; Vector3 at; return Due(r.Actor) && Warm(r, out h, out at); }

        internal static NpcMind.Plan Visit(NpcResident r)
        {
            NpcHouse h; Vector3 at;
            if (!Warm(r, out h, out at)) return null;
            LastVisit[r.Actor.Zdo.m_uid] = Now;
            return new NpcMind.Plan { Kind = NpcMind.Task.Sit, To = at, Guest = true };
        }

        // its last visit long enough ago (first seen: some time in the last few days, so not all of them go on day one)
        private static bool Due(NpcActor a)
        {
            if (a == null || a.Zdo == null) return false;
            double last;
            if (!LastVisit.TryGetValue(a.Zdo.m_uid, out last)) { last = Now - Random.Range(0f, (float)(VisitDays * Day)); LastVisit[a.Zdo.m_uid] = last; }
            return Now - last >= VisitDays * Day;
        }

        // a burning fire in a player's house of its base with nobody home, and a spot by it it can walk to
        private static bool Warm(NpcResident r, out NpcHouse house, out Vector3 at)
        {
            house = null; at = Vector3.zero;
            NpcBase b = r.Base;
            List<NpcHouse> houses = Houses(b);
            if (houses.Count == 0) return false;
            foreach (Fireplace f in NpcWorker.InBaseOf<Fireplace>(b, r.transform.position))
            {
                if (f == null || !f.IsBurning() || !NpcKit.Warms(f)) continue;
                NpcHouse h = At(b, f.transform.position);
                if (h == null || Home(h)) continue;
                Vector3? s = NpcKit.FireSide(f, r.transform.position);
                if (s == null) continue;
                house = h; at = s.Value;
                return true;
            }
            return false;
        }

        private static bool Home(NpcHouse h)
        {
            foreach (Player p in Player.GetAllPlayers()) if (p != null && h.Near(p.transform.position)) return true;
            return false;
        }
    }
}
#endif

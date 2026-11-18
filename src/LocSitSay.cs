#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A seat of a place your raven has its own word about (the user 2026-11-16, the throne with two carved ravens by the barrow's chest: «якщо сісти на
    // трон … ворон, якщо він на плечі, підколить … 5 різних підколів ситуативних тільки для цього предмету»). The place's lines «sitsay: <place> <tag>
    // [conditions]» (locations.list; the place a story word — a named piece, an inside's point «in.<layout>.<point>»): you sit down on a seat of the game
    // (attached, not on a ship) within Near m of it, and the first of its lines whose conditions hold gives the tag; a raven of yours on the shoulder,
    // awake, says a line of it in the tone of its trust (the tags: SeatPatterns). Both there: one of the two. None: nothing is said. Once a sitting, and
    // not at that seat again within Again s (up and down again: no nagging). Others talking, a scene playing: it waits while you sit.
    // Nothing kept: a seat's last word is this game's run only. Cost: twice a second (StoryQuests.Each) one look whether he sits; on sitting down, once,
    // a pass over the places' «sitsay» lines (one now) with a lookup and a distance each.
    internal static class LocSitSay
    {
        internal static float Near = 1.5f, Again = 120f;
        private static int _sat;      // how many looks in a row he has sat (the first: his body not yet on the seat — the game puts it there after)
        private static bool _done;    // this sitting's word said, or none to say
        private static readonly Dictionary<string, float> SaidAt = new Dictionary<string, float>(); // a seat's place → when its word was said

        internal static void Look(Player p)
        {
            if (p == null || p.IsDead() || !p.IsAttached() || p.IsAttachedToShip()) { _sat = 0; return; }
            if (++_sat == 1) { _done = false; return; }
            if (_done || StoryScenes.Playing.Length > 0 || RavenSkills.Hushed(p)) return; // (waits while he sits)
            _done = true;
            LocDef.SitSay s = Seat(p);
            if (s == null) return;
            float at;
            if (SaidAt.TryGetValue(s.Place, out at) && Time.time - at < Again) return;
            bool munin;
            if (!Speaker(p, out munin)) return;
            SaidAt[s.Place] = Time.time;
            RavenSkills.StrikeSay(munin, s.Tag); // (a tag in the tone of its trust, said from the shoulder: no line of the tag — nothing)
        }

        // the seat he sits at: the first «sitsay» line within Near m whose conditions hold for him
        private static LocDef.SitSay Seat(Player p)
        {
            foreach (Loc l in Locs.All)
                foreach (LocDef.SitSay s in l.SitSays)
                {
                    Vector3 at;
                    if (!StoryWhere.Find(s.Place, out at) || StoryWhere.Apart(at, p.transform.position) > Near) continue;
                    Loc of = l;
                    if (s.When.Length == 0 || StoryCond.Holds(p, s.When, e => of.Log("sitsay: " + e))) return s;
                }
            return null;
        }

        // a raven of his on the shoulder, awake (another's worn says nothing: its words would be a stranger's); both — one of the two
        private static bool Speaker(Player p, out bool munin)
        {
            bool h = Ready(p, false), m = Ready(p, true);
            munin = m && (!h || Random.value < 0.5f);
            return h || m;
        }

        // that raven of his is on the shoulder, awake and his own: one that may speak a story's word to him there (StoryStranger asks it too)
        internal static bool Ready(Player p, bool munin)
        {
            if (!Shoulder.Present(p, munin)) return false;
            ItemDrop.ItemData r = Trust.Worn(munin);
            return r != null && Trust.Active(r) && !Trust.Stranger(r, p);
        }
    }
}
#endif

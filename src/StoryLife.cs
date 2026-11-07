#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story's person between its commands: its own life among the places the author gave it (the user 2026-11-04). Not a third
    // way of moving: the same choice and the same visit as anyone's (NpcPlaces), only the places are these, the lines «spot:» of
    // its [person] (assets/story/people):
    //   spot: <set> <place[.piece]> [by <place[.piece]>] <kind> [sit] [clip <clip> [from <clip>] [out <clip>]] [hours <a>-<b>] [stay <a>-<b>] [want <n>]
    //     set     a named set of places; the quest's word «npc <who> live <set>» says which is its now (the world's key, every game
    //             reads it, nothing to send; none said: Default)
    //     kind    NpcPlaces.Kinds: what it is worth to it and what it is said to be doing (fire, seat, think, tend…)
    //     sit     sits there; clip: its clip there (from: its way in, out: its way out, in time for the end of its stay)
    //     hours   only in these game hours (b below a: across midnight)   stay: seconds there   want: added to its worth
    // A spot counts while it stands within Reach of its home now (its home moved with the story: StoryPeople «kindled»: the spots there
    // count then). No spot of the set fits now: its base's own places. «npc <who> hold» goes over all this (the Order layer), and its
    // «free» leaves it to this again. The load check: the game at Parse, storycheck at the build.
    internal static class StoryLife
    {
        internal const string Default = "home";
        internal const string Usage = "npc <хто> live <набір місць>";
        internal static float Reach = NpcBase.Reach; // m from its home
        private static readonly char[] Spaces = { ' ', '\t' };

        internal sealed class Spot
        {
            public string Set = "", Thing = "", By = "", Kind = "", Clip = "", From = "", Out = "";
            public bool Sit;
            public float HourFrom = -1f, HourTo = -1f, Min, Max, Want;
        }

        private static string Key(string who) { return "live_" + who; }
        internal static string SetOf(string who) { string s = StoryWorld.Text(Key(who)); return string.IsNullOrEmpty(s) ? Default : s; }
        internal static void Set(string who, string set) { StoryWorld.SetText(Key(who), set); }

        private static StoryPeople.Person Of(string npc)
        {
            foreach (StoryPeople.Person x in StoryPeople.All) if (x.Npc == npc) return x;
            return null;
        }

        // what is wrong with the word (the load check), null: fine
        internal static string Why(string who, string set)
        {
            StoryPeople.Person x = Of(who);
            if (x == null) return "npc live: «" + who + "» не людина сюжету (assets/story/people)";
            foreach (Spot s in x.Spots) if (s.Set == set) return null;
            return "npc live: у «" + who + "» нема місць «" + set + "»";
        }

        // «<set> <place> [by <place>] <kind> [sit] [clip c [from c] [out c]] [hours a-b] [stay a-b] [want n]»; why: what is wrong, null fine
        internal static Spot Parse(string v, out string why)
        {
            why = null;
            string[] w = v.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length < 3) { why = "spot: <набір> <місце> <вид> …"; return null; }
            Spot s = new Spot { Set = w[0], Thing = w[1] };
            int i = 2;
            if (w[i] == "by" && i + 2 < w.Length) { s.By = w[i + 1]; i += 2; }
            s.Kind = w[i++];
            if (!NpcPlaces.Kinds.ContainsKey(s.Kind)) { why = "нема виду «" + s.Kind + "»"; return null; }
            for (; i < w.Length; i++)
            {
                if (w[i] == "sit") { s.Sit = true; continue; }
                if (i + 1 >= w.Length) { why = "«" + w[i] + "» без значення"; return null; }
                string val = w[++i];
                switch (w[i - 1])
                {
                    case "clip": s.Clip = val; break;
                    case "from": s.From = val; break;
                    case "out": s.Out = val; break;
                    case "hours": if (!Range(val, out s.HourFrom, out s.HourTo)) why = "погані години «" + val + "»"; break;
                    case "stay": if (!Range(val, out s.Min, out s.Max)) why = "поганий час «" + val + "»"; break;
                    case "want": if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Want)) why = "погане число «" + val + "»"; break;
                    default: why = "невідоме слово «" + w[i - 1] + "»"; break;
                }
                if (why != null) return null;
            }
            if (s.Sit && s.Clip.Length > 0) { why = "або sit, або clip"; return null; }
            return s;
        }

        private static bool Range(string v, out float a, out float b)
        {
            a = b = 0f;
            int dash = v.IndexOf('-');
            return dash > 0 && float.TryParse(v.Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && float.TryParse(v.Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b);
        }

        private static bool InHours(Spot s, float hour)
        {
            if (s.HourFrom < 0f) return true;
            return s.HourFrom <= s.HourTo ? hour >= s.HourFrom && hour < s.HourTo : hour >= s.HourFrom || hour < s.HourTo;
        }

        // where it stands at a spot: by the thing, a step from its edge, never its middle (the thing's pieces are looked over: only for the one it goes to)
        private static Vector3 Stand(Spot s, Vector3 at, Vector3 me, bool clip)
        {
            Vector3 side = me, by;
            if (s.By.Length > 0 && StoryWhere.Find(s.By, out by)) side = by;
            return StoryWhere.StandBy(s.Thing, side, at, clip, out by) ? by : NpcPlaces.Side(at, side, 1.5f);
        }

        // its places now (NpcPlaces.Authored): null, no spots at all or none fits now: its base's own. Asked at each of its choices
        internal static List<NpcPlaces.Place> Places(NpcActor a)
        {
            StoryPeople.Person x = Of(a.Id);
            if (x == null || x.Spots.Count == 0) return null;
            string set = SetOf(a.Id);
            float hour = EnvMan.instance != null ? Mathf.Repeat(EnvMan.instance.GetDayFraction() * 24f - NpcPersona.Of(a).Chrono, 24f) : 12f;
            Vector3 home = NpcRules.Home(a), me = a.transform.position;
            List<NpcPlaces.Place> all = NpcPlaces.Begin();
            foreach (Spot s in x.Spots)
            {
                Vector3 at;
                if (s.Set != set || !InHours(s, hour) || !StoryWhere.Standing(s.Thing, out at) || Utils.DistanceXZ(at, home) > Reach || LocState.Absent(StoryWhere.Piece(s.Thing))) continue;
                bool clip = s.Clip.Length > 0;
                Spot spot = s; Vector3 thing = at;
                NpcPlaces.Place p = NpcPlaces.Spot(s.Kind, NpcPlaces.Side(at, me, 1.5f), at + Vector3.up * 0.5f, at);
                p.Stand = () => Stand(spot, thing, a.transform.position, clip);
                p.Sit = s.Sit; p.Extra = s.Want; p.Min = s.Min; p.Max = s.Max; p.Clip = s.Clip; p.From = s.From; p.Out = s.Out; p.Exact = clip;
            }
            return all;
        }
    }
}
#endif

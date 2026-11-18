#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The story's person between its commands: its own life among the places the author gave it (the user 2026-11-04), and its day by the
    // hour (the user 2026-11-16: «кожен мав свій більш-менш розклад… на кожному етапі сюжету цей розклад можна було легко змінювати»). Not a
    // third way of moving: the same choice and the same visit as anyone's (NpcPlaces), only the places are these, the lines of its [person]
    // (assets/story/people):
    //   day: <set> [when <world conditions>]
    //     which set of places is its now: the first line that holds, as its «live:» (the last may have no «when»); none holds, or no such
    //     line: Default. The world's conditions only (a stage of a server's quest: «reached ch1 home_hall»), read on every game from what the
    //     world already keeps: nothing to send, nothing a quest has to say, right after a reload and after the author's jump
    //   at: <set> <a>-<b> <place[.piece]> … (the rest as «spot:», without «hours»)
    //     its day by the hour: in these game hours (its own clock: NpcPersona.Chrono) it is there — while one such line of its set fits, the
    //     places of «spot:» are not weighed at all, and no whim of its pleasure takes it off (NpcWhims.Held). Several lines over the same hours:
    //     it goes between them. Now and then a break (Break of the time, in turns of BreakEvery s): its «spot:» places are weighed too.
    //     Its place past Reach of its home: a way out and back (Far — its word in those hours, as a story's: StoryHold), no break; the
    //     story's own word over it
    //   at: <set> <a>-<b> sleep
    //     its night. A hook only (the user 2026-11-16: «поки сюжетні нехай не сплять… задай просто гачок»): read and checked, and nothing is done
    //     with it — those hours are its free ones. Where sleep comes in: Places, the line marked «sleep»
    //   spot: <set> <place[.piece]> [by <place[.piece]>] <kind> [sit] [clip <clip> [from <clip>] [out <clip>]] [tool] [moves <a|b>] [hours <a>-<b>] [days <n>] [stay <a>-<b>] [want <n>]
    //     its free hours (none of its «at:» fits now): weighed against each other as anyone's places
    //     set     a named set of places: its «day:» says which is its now
    //     kind    NpcPlaces.Kinds: what it is worth to it and what it is said to be doing (fire, seat, think, tend, post — a watch, its back to the thing…)
    //     sit     sits there; clip: its clip there (from: its way in, out: its way out, in time for the end of its stay); moves: its short moves there, most of the
    //             time (NpcPlaces.SpotMoves) over the kind's — Heid's hand on her heart by the stones of the fallen (through the gate: NpcMoves); tool: its
    //             own thing (NpcTool) in its hand for the clip there — Lit's hammer at the wall (NpcPlaces.ClipBegins / ClipEnds)
    //     hours   only in these game hours (b below a: across midnight)   stay: seconds there   want: added to its worth
    //     chance  «іноді» (the user 2026-11-16: «щоб іноді Літ міг відійти від шаблону і піти до каменів Гейд, постояти, щось сказати й піти назад»):
    //             the line holds on that part of the days, at random — the same day on every game (Lucky: the day and the line). An outing:
    //             while one is on, the other lines of its set in those hours do not fit (Fits) — it goes there, then back to its day
    //     say     the theme of its own words it says there (assets/story/words «[lit:stones] …»; none: «at_<kind>»): at an outing, once a visit
    //             at its next look round (Tick → NpcActor.DueRemark)
    //     when    (last, as in «day:»: «… when <world conditions>») only while these hold (the user 2026-11-16: one line of a phase — Torvald's watch
    //             from the crypt on, Heid tidying the Hall after the first heart, Lit's work on the walls in chapter 2 — not a set of its own each). The
    //             world's conditions only; a stage of a server's quest — «step ch2 work1» — holds while it is on
    //     days    only every n-th day of the game (the user 2026-11-16: Torvald goes to change the watch once in three days, not every evening):
    //             the world's day (EnvMan.GetDay) divisible by n — the same day on every game; hours across midnight count to the day they began
    //     (none)  an «at:» line with none of days, chance, daily acts at random intervals (the user 2026-11-16: «не кожен день… всі дії мають мати
    //             інтервали рандомні в ядрі і лише якщо цього потребуватиму я тоді зробимо певні дії кожнодневними»): a day it acts, then the next
    //             one GapMin…GapMax days on (Acts: from the line's seed and the world's day — the same days on every game, nothing kept)
    //     every   «every <a>-<b>»: that gap in days for this line, not GapMin…GapMax
    //     daily   every day — only where the user asks for it
    //     group   «group <name>»: lines of different people with the same name act on the same days (Heid and Torvald by the stones of the
    //             fallen at dawn); lines of one gathering («meet») share their days by its name by themselves
    //     meet    a gathering's name: the people whose days name the same one in the same hours are there together, and talk their own
    //             talks of it (StoryMeets; the talks: assets/story/meets, «[meet <name>]»)
    // A spot counts while it stands within Reach of its home now (an «at:» past it: Far) (its home moved with the story: StoryPeople «kindled»: the spots there
    // count then). No spot of the set fits now: its base's own places. «npc <who> hold» goes over all this (the Order layer), and its
    // «free» leaves it to this again. The load check: the game at Parse, storycheck at the build. What its day says now: «raven npc day <who>».
    internal static class StoryLife
    {
        internal const string Default = "home";
        internal const string Sleep = "sleep"; // («at: <set> <a>-<b> sleep»)
        internal static float Reach = NpcBase.Reach; // m from its home
        internal static float Break = 0.15f, BreakEvery = 120f; // of its planned hours it is off to a place of its free ones · s a turn of that
        internal const int GapMin = NpcKit.GapFrom, GapMax = NpcKit.GapTo; // days from a day an «at:» line acts to its next one, at random (none of days, chance, daily: NpcKit.Days)
        private static readonly char[] Spaces = { ' ', '\t' };

        internal sealed class Spot
        {
            public string Set = "", Thing = "", By = "", Kind = "", Clip = "", From = "", Out = "", Moves = "";
            public string Meet = ""; // «meet <name>»: a gathering — those whose days name it at the same hours are there together (StoryMeets)
            public string Word = ""; // a line «at:» as a story's word (StoryHold), for when its place stands past its home's reach: Far
            public bool Sit, Tool;
            public bool Planned;   // a line «at:»: its day by the hour
            public bool Sleeps;    // «at: … sleep»: the hook of its night
            public float HourFrom = -1f, HourTo = -1f, Min, Max, Want;
            public int Days = 1; // «days <n>»: only every n-th day of the game (OnDay)
            public bool Daily;   // «daily»: every day (the user's own word for a line; none of days, chance, daily — at random intervals: Acts)
            public int GapFrom = GapMin, GapTo = GapMax; // «every <a>-<b>»: days from one day it acts to the next (Acts)
            public string Group = ""; // «group <name>»: the lines of that name act on the same days, whoever's they are (Acts)
            public NpcKit.DayRun Days0; // (Acts: its run of days — made once, shared by the lines of its group or gathering)
            public float Chance; // «chance <p>»: an outing — on that part of the days (Lucky); 0: every day
            public string When = ""; // «… when <world conditions>»: the line only while they hold (Live)
            public string Say = "", Who = ""; // «say <theme>»: its words there (Themes) · whose line (the seed of its days: StoryPeople.Parse)
            public uint Seed; public bool Seeded; // (Lucky: from its owner, place and hour — made once, not a string a check)
            public Action<string> WhenLog; // (Live: made once — a check asks it every few seconds a person)
        }
        internal sealed class Day { public string Set = "", When = ""; }

        // the set of places that is its now: the first «day:» that holds
        internal static string SetOf(StoryPeople.Person x)
        {
            if (x.DayLog == null) x.DayLog = e => Story.Log("people: " + x.Id + ": day … when " + e); // (once a person: asked every few seconds)
            foreach (Day d in x.Days) if (d.When.Length == 0 || StoryCond.Holds(null, d.When, x.DayLog, true)) return d.Set;
            return Default;
        }

        private static StoryPeople.Person Of(string npc)
        {
            foreach (StoryPeople.Person x in StoryPeople.All) if (x.Npc == npc) return x;
            return null;
        }

        // «<set> [when <world conditions>]»
        internal static Day ParseDay(string v, out string why)
        {
            why = null;
            int when = v.IndexOf(" when ", StringComparison.Ordinal);
            Day d = new Day();
            if (when >= 0) { d.When = v.Substring(when + 6).Trim(); v = v.Substring(0, when); }
            d.Set = v.Trim();
            if (d.Set.Length == 0 || d.Set.IndexOfAny(Spaces) >= 0) { why = "day: <набір> [when <умови світу>]"; return null; }
            return d;
        }

        // «<set> <a>-<b> sleep» | «<set> <a>-<b> <place> …» (the rest as a spot's, its hours said here)
        internal static Spot ParseAt(string v, out string why)
        {
            why = null;
            string when = CutWhen(ref v);
            string[] w = v.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            float a, b;
            if (w.Length < 3 || !Range(w[1], out a, out b)) { why = "at: <набір> <від>-<до> <місце> <вид> … | at: <набір> <від>-<до> sleep"; return null; }
            if (a < 0f || a > 24f || b < 0f || b > 24f || a == b) { why = "погані години «" + w[1] + "» (0–24, різні)"; return null; }
            Spot s;
            if (w[2] == Sleep)
            {
                if (w.Length > 3) { why = "at … sleep: без слів після нього"; return null; }
                s = new Spot { Set = w[0], Sleeps = true };
            }
            else
            {
                if (Array.IndexOf(w, "hours") >= 0) { why = "at: години вже сказано, «hours» зайве"; return null; }
                s = Parse(w[0] + " " + string.Join(" ", w, 2, w.Length - 2), out why);
                if (s == null) return null;
                s.Word = s.Thing + (s.By.Length > 0 ? " by " + s.By : "") + (s.Sit ? " sit" : s.Clip.Length > 0 ? " clip " + s.Clip + (s.From.Length > 0 ? " from " + s.From : "") + (s.Out.Length > 0 ? " out " + s.Out : "") : " stand"); // (far: Far)
            }
            s.Planned = true; s.HourFrom = a; s.HourTo = b; s.When = when;
            return s;
        }

        // «<set> <place> [by <place>] <kind> [sit] [clip c [from c] [out c]] [hours a-b] [stay a-b] [want n]»; why: what is wrong, null fine
        internal static Spot Parse(string v, out string why)
        {
            why = null;
            string when = CutWhen(ref v);
            string[] w = v.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length < 3) { why = "spot: <набір> <місце> <вид> …"; return null; }
            Spot s = new Spot { Set = w[0], Thing = w[1], When = when };
            int i = 2;
            if (w[i] == "by" && i + 2 < w.Length) { s.By = w[i + 1]; i += 2; }
            s.Kind = w[i++];
            if (!NpcPlaces.Kinds.ContainsKey(s.Kind)) { why = "нема виду «" + s.Kind + "»"; return null; }
            for (; i < w.Length; i++)
            {
                if (w[i] == "sit") { s.Sit = true; continue; }
                if (w[i] == "tool") { s.Tool = true; continue; }
                if (w[i] == "daily") { s.Daily = true; continue; }
                if (i + 1 >= w.Length) { why = "«" + w[i] + "» без значення"; return null; }
                string val = w[++i];
                switch (w[i - 1])
                {
                    case "clip": s.Clip = val; break;
                    case "from": s.From = val; break;
                    case "out": s.Out = val; break;
                    case "moves": s.Moves = val; break;
                    case "meet": s.Meet = val; break;
                    case "hours": if (!Range(val, out s.HourFrom, out s.HourTo)) why = "погані години «" + val + "»"; break;
                    case "stay": if (!Range(val, out s.Min, out s.Max)) why = "поганий час «" + val + "»"; break;
                    case "want": if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Want)) why = "погане число «" + val + "»"; break;
                    case "chance": if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Chance) || s.Chance <= 0f || s.Chance >= 1f) why = "chance: від 0 до 1 (не включно), «" + val + "»"; break;
                    case "say": s.Say = val; break;
                    case "days": if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Days) || s.Days < 1) why = "days: ціле число від 1, «" + val + "»"; break;
                    case "every": { float a, b; if (!Range(val, out a, out b) || a < 1f || b < a || a != Mathf.Floor(a) || b != Mathf.Floor(b)) why = "every <a>-<b>: цілі дні від 1, a ≤ b, «" + val + "»"; else { s.GapFrom = (int)a; s.GapTo = (int)b; } break; }
                    case "group": s.Group = val; break;
                    default: why = "невідоме слово «" + w[i - 1] + "»"; break;
                }
                if (why != null) return null;
            }
            if (s.Sit && s.Clip.Length > 0) { why = "або sit, або clip"; return null; }
            if ((s.Daily || s.Group.Length > 0 || s.GapFrom != GapMin || s.GapTo != GapMax) && (s.Days > 1 || s.Chance > 0f)) { why = "daily, every, group — для рядка з випадковими проміжками: не разом із days чи chance"; return null; }
            return s;
        }

        // «… when <conditions>»: cut off the line, the conditions given back ("" none)
        private static string CutWhen(ref string v)
        {
            int at = v.IndexOf(" when ", StringComparison.Ordinal);
            if (at < 0) return "";
            string c = v.Substring(at + 6).Trim();
            v = v.Substring(0, at);
            return c;
        }

        // its «when» holds now (the world's conditions: asked with no player, cached as read — StoryCond.Holds)
        private static bool Live(Spot s)
        {
            if (s.When.Length == 0) return true;
            if (s.WhenLog == null) { Spot me = s; s.WhenLog = e => Story.Log("people: " + me.Who + ": at/spot … when " + e); }
            return StoryCond.Holds(null, s.When, s.WhenLog, true);
        }

        private static bool Range(string v, out float a, out float b)
        {
            a = b = 0f;
            int dash = v.IndexOf('-');
            return dash > 0 && float.TryParse(v.Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out a) && float.TryParse(v.Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out b);
        }

        private static bool InHours(Spot s, float hour)
        {
            if (s.HourFrom < 0f) return OnDay(s, hour);
            bool late = s.HourFrom > s.HourTo && hour < s.HourTo; // (across midnight, after it: the day it began)
            return (s.HourFrom <= s.HourTo ? hour >= s.HourFrom && hour < s.HourTo : hour >= s.HourFrom || late) && OnDay(s, late ? -1f : hour);
        }

        // its day of the n (Days): the world's day — the person's own clock shifts the hour, not the day (an hour or so at most: NpcPersona.Chrono)
        private static bool OnDay(Spot s, float hour)
        {
            bool gaps = Gaps(s);
            if (s.Days <= 1 && s.Chance <= 0f && !gaps) return true;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (hour < 0f) day--;
            if (gaps) return Acts(s, day);
            return (s.Days <= 1 || ((day % s.Days) + s.Days) % s.Days == 0) && (s.Chance <= 0f || Lucky(s, day));
        }

        // a line of its day by the hour that acts at random intervals: none of days, chance, daily (a free-time «spot:» is weighed, not planned)
        private static bool Gaps(Spot s) { return s.Planned && !s.Sleeps && !s.Daily && s.Days <= 1 && s.Chance <= 0f; }

        // its days at random intervals (NpcKit.Days): its group's, its gathering's, else its own — made once a line
        private static bool Acts(Spot s, int day)
        {
            if (s.Days0 == null) s.Days0 = NpcKit.Days(s.Group.Length > 0 ? "group|" + s.Group : s.Meet.Length > 0 ? "meet|" + s.Set + "|" + s.Meet : s.Who + "|" + s.Thing + "|" + s.HourFrom.ToString(CultureInfo.InvariantCulture), s.GapFrom, s.GapTo);
            return NpcKit.OnDay(s.Days0, day);
        }

        // an outing's day: from the day and the line alone — every game, a reload, the server agree (nothing kept)
        private static bool Lucky(Spot s, int day)
        {
            if (!s.Seeded) { s.Seed = (uint)(s.Who + "|" + s.Thing + "|" + s.HourFrom.ToString(CultureInfo.InvariantCulture)).GetStableHashCode(); s.Seeded = true; }
            uint h = s.Seed ^ (uint)day * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFF) / 65535f < s.Chance;
        }

        // the line fits now: of its set, in its hours and on its day, and no outing of its set on now over it (an outing's own lines fit)
        private static bool Fits(StoryPeople.Person x, Spot s, string set, float hour)
        {
            if (s.Set != set || !InHours(s, hour) || !Live(s)) return false;
            if (s.Chance > 0f || !x.Outings) return true; // (no «chance» line of its own at all: nothing to look over)
            foreach (Spot o in x.Spots) if (o.Chance > 0f && !o.Sleeps && o.Set == set && InHours(o, hour) && Live(o)) return false;
            return true;
        }

        // where it stands at a spot: by the thing, a step from its edge, never its middle (the thing's pieces are looked over: only for the one it goes to)
        private static Vector3 Stand(Spot s, Vector3 at, Vector3 me, bool clip)
        {
            Vector3 side = me, by;
            if (s.By.Length > 0 && StoryWhere.Find(s.By, out by)) side = by;
            return StoryWhere.StandBy(s.Thing, side, at, clip, out by) ? by : NpcPlaces.Side(at, side, 1.5f);
        }

        // the hour of its own day (an early bird's sooner: the same shift as a resident's day)
        private static float Hour(NpcActor a) { return Clock(NpcPersona.Of(a).Chrono); }
        private static float Clock(float shift) { return EnvMan.instance != null ? Mathf.Repeat(EnvMan.instance.GetDayFraction() * 24f - shift, 24f) : 12f; }

        // the same hour with no body (the server keeps it where nobody runs it: StoryLife.Far): its ways as its body has them, made once a change of them
        private sealed class Shift { public uint Seed; public string Traits; public float Chrono; }
        private static readonly Dictionary<string, Shift> _shift = new Dictionary<string, Shift>(); // session: npc → its seed and the story's changes as last read, its day's shift
        private static float Hour(ZDO z, string npc)
        {
            uint seed = NpcKit.Seed(z);
            string traits = NpcLocker.Text(z, NpcLevers.KTraits);
            Shift k;
            if (!_shift.TryGetValue(npc, out k)) _shift[npc] = k = new Shift { Traits = null };
            if (k.Traits == null || k.Seed != seed || !string.Equals(k.Traits, traits, StringComparison.Ordinal)) { k.Seed = seed; k.Traits = traits; k.Chrono = NpcPersona.OfZdo(z, NpcDefs.Find(npc)).Chrono; } // (a persona made only when its ways changed)
            return Clock(k.Chrono);
        }

        // a far line of its day: an «at:» whose place stands past Reach of its home — not a place of its life round its home but a way out and
        // back (the user 2026-11-16, «справжній похід»: Torvald «goes to change the watch» on the edge of the Meadows, 150–450 m off the Hall). In
        // those hours it is its word as a story's is (StoryHold.Now: «<place> [by <place>] stand|sit|clip …»), walked as a long way, brought there
        // by the server where nobody sees it; the hours over, the word is gone, and it goes home as anyone far off its base (NpcResident «back»,
        // NpcStray, StoryPeople). No breaks on a far line (a break would walk it 300 m home and back). Its own lines only, never a scene's copy
        // of it, never while it is away. "": none now. Asked by StoryHold.Tick (every HoldEvery s a story person), StoryPeople.Keep, NpcStray:
        // a few lines of its file and a world key each
        internal static float FarEvery = 1f; // s an answer of Far is kept (its three askers come within one: StoryHold.Tick, StoryPeople.Keep, NpcStray)
        private sealed class FarKept { public float Until; public string Word = ""; public ZDO Of; }
        private static readonly Dictionary<string, FarKept> _far = new Dictionary<string, FarKept>(); // session: npc → its far word, a second
        internal static string Far(ZDO z)
        {
            string npc = z.GetString(Npcs.KId, "");
            StoryPeople.Person x = Of(npc);
            if (x == null || !x.Planned || !StoryPeople.Of(z, npc)) return ""; // (not a story person with a day by the hour: nothing kept for it)
            FarKept k;
            if (_far.TryGetValue(npc, out k) && k.Of == z && Time.time < k.Until) return k.Word;
            if (k == null) _far[npc] = k = new FarKept();
            k.Of = z; k.Until = Time.time + FarEvery;
            return k.Word = FarNow(z, npc, x);
        }
        private static string FarNow(ZDO z, string npc, StoryPeople.Person x)
        {
            Vector3 home = z.GetVec3(NpcRules.KHome, z.GetPosition()), at;
            if ((home - StoryPeople.AwayAt).sqrMagnitude < StoryPeople.Moved * StoryPeople.Moved) return ""; // (away, or back from it with its home not written anew yet: every line would be «far» from the world's corner — the review of 2026-11-16)
            StoryPeople.Home h = x.Now;
            if (h == null || h.At == StoryPeople.Away) return ""; // (out of the world for a while: its «home» is the world's far corner)
            string set = SetOf(x);
            float hour = -1f;
            foreach (Spot s in x.Spots)
            {
                if (!s.Planned || s.Sleeps || s.Set != set) continue;
                if (hour < 0f) hour = Hour(z, npc);
                if (!Fits(x, s, set, hour) || !StoryWhere.Standing(s.Thing, out at) || Utils.DistanceXZ(at, home) <= Reach) continue; // flat: as Stands — its home and the place on the same ground
                return s.Word;
            }
            return "";
        }

        // a turn of its break now: the same answer all through the turn (its choice and «raven npc why» agree), another person's turns are its own
        private static bool OnBreak(NpcActor a)
        {
            uint turn = NpcKit.Seed(a.Zdo) + (uint)Mathf.FloorToInt(Time.time / BreakEvery);
            return (turn * 2654435761u >> 16 & 0xFFFF) / 65535f < Break;
        }

        // the spot is there to go to: its thing stands in the world, within reach of the person's home now, and its place's state has it
        private static bool Stands(Spot s, Vector3 home, out Vector3 at)
        {
            at = Vector3.zero;
            return !s.Sleeps && StoryWhere.Standing(s.Thing, out at) && Utils.DistanceXZ(at, home) <= Reach && !LocState.Absent(StoryWhere.Piece(s.Thing)); // flat: a spot of its life round its home, both on the same ground
        }

        // its day says where it is now: a line «at:» of its set in these hours whose place stands, and no break (NpcWhims.Held: no whim of its
        // pleasure then). Asked as often as a whim is tried (a minute or more a person): a few lines of its file
        internal static bool Planned(NpcActor a)
        {
            StoryPeople.Person x = a != null ? Of(a.Id) : null;
            if (x == null || x.Spots.Count == 0) return false;
            string set = null; float hour = 0f; Vector3 at;
            foreach (Spot s in x.Spots)
            {
                if (!s.Planned || s.Sleeps) continue;
                if (set == null) { set = SetOf(x); hour = Hour(a); }
                if (Fits(x, s, set, hour) && Stands(s, NpcRules.Home(a), out at)) return s.Chance > 0f || !OnBreak(a); // (an outing: no break from it)
            }
            return false;
        }

        // the gathering its day has it at now ("" none), and where: a line of its set with «meet», in its hours (asked for anyone, on any game)
        internal static string MeetNow(NpcActor a, out Vector3 at)
        {
            at = Vector3.zero;
            StoryPeople.Person x = a != null ? Of(a.Id) : null;
            if (x == null) return "";
            string set = null; float hour = 0f;
            foreach (Spot s in x.Spots)
            {
                if (s.Meet.Length == 0) continue;
                if (set == null) { set = SetOf(x); hour = Hour(a); }
                if (Fits(x, s, set, hour) && Stands(s, NpcRules.Home(a), out at)) return s.Meet;
            }
            return "";
        }

        // the spots of its set that fit this hour and stand within reach of its home now
        private static readonly List<KeyValuePair<Spot, Vector3>> _fit = new List<KeyValuePair<Spot, Vector3>>();

        // its places now (NpcPlaces.Authored): null, no spots at all or none fits now: its base's own. Asked at each of its choices
        internal static List<NpcPlaces.Place> Places(NpcActor a)
        {
            StoryPeople.Person x = Of(a.Id);
            if (x == null || (x.Spots.Count == 0 && x.Carry == null)) return null;
            string set = SetOf(x);
            float hour = Hour(a);
            Vector3 home = NpcRules.Home(a), me = a.transform.position;
            List<NpcPlaces.Place> all = NpcPlaces.Begin();
            _fit.Clear();
            bool planned = false;
            foreach (Spot s in x.Spots)
            {
                Vector3 at;
                if (!Fits(x, s, set, hour)) continue;
                if (s.Sleeps) continue; // sleep: the hook of its night — here it would lie down (its bed: StoryPeople «live … bed», NpcMind.Task.Sleep); till then these hours are its free ones
                if (!Stands(s, home, out at)) continue;
                _fit.Add(new KeyValuePair<Spot, Vector3>(s, at));
                planned |= s.Planned;
            }
            bool free = !planned || OnBreak(a); // (its day says where it is: its free places only in a break)
            foreach (KeyValuePair<Spot, Vector3> f in _fit)
            {
                Spot s = f.Key; Vector3 at = f.Value;
                if (!s.Planned && !free) continue;
                bool clip = s.Clip.Length > 0;
                Spot spot = s; Vector3 thing = at;
                NpcPlaces.Place p = NpcPlaces.Spot(s.Kind, NpcPlaces.Side(at, me, 1.5f), at + Vector3.up * 0.5f, at);
                p.Stand = () => Stand(spot, thing, a.transform.position, clip);
                if (p.K.Outward) // (a watch: its back to the thing, its eyes out from it — on the side of its «by», else the side it comes from)
                {
                    Vector3 side = me, by;
                    if (s.By.Length > 0 && StoryWhere.Find(s.By, out by)) side = by;
                    Vector3 off = side - at; off.y = 0f;
                    if (off.sqrMagnitude > 0.01f) p.Look = at + off.normalized * 20f + Vector3.up * 1.6f;
                }
                p.Shared = s.Meet.Length > 0; // (a gathering: those there are its company, not a crowd)
                p.Sit = s.Sit; p.Extra = s.Want; p.Min = s.Min; p.Max = s.Max; p.Clip = s.Clip; p.From = s.From; p.Out = s.Out; p.Exact = clip; p.Moves = s.Moves; p.Tool = s.Tool;
            }
            // its own thing (StoryProps «carry:») set down somewhere near its home by its own doing — it goes back for it first of all (set down by a
            // word of the story: it lies till «npc <who> prop fetch»). Nothing of its own anywhere: StoryProps.Keep puts it in her hand
            if (x.Carry != null)
            {
                Vector3 lie; Quaternion turn; bool left;
                if (StoryProps.Lying(a, out lie, out turn, out left) && (!left || StoryProps.MayFetch(a)) && Utils.DistanceXZ(lie, home) <= Reach)
                {
                    NpcPlaces.Place p = NpcPlaces.Spot("fetch", NpcPlaces.Side(lie, me, 0.6f), lie + Vector3.up * 0.3f, lie);
                    StoryProps.Ask carry = x.Carry;
                    p.Stand = () => NpcPlaces.Side(lie, a.transform.position, 0.6f);
                    p.Exact = true; p.Extra = StoryProps.FetchWant;
                    p.There = who => StoryProps.PickUp(who, carry, true);
                }
            }
            return all;
        }

        // what it is doing at a place of its day, for its words unasked (NpcVoice.Sources; the user 2026-11-16: «Літ щось робить і може про себе щось
        // сказати»): «at_<kind>» of the first line of its set now whose place it stands by (within SpotNear m) — Lit at the wall «at_tend», Torvald on
        // his watch «at_post» (a far line too). Said only with its own words of it (assets/story/words: «[lit:at_tend] …»; NpcVoice: a story person
        // speaks unasked only with «voice: own» in its template). Asked as its remark is (every two to five minutes a person, a player near): its lines
        internal static float SpotNear = 6f; // m from its place's thing (a wall's middle: it stands by its edge)
        internal static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            StoryPeople.Person x = Of(a.Id);
            if (x == null || x.Spots.Count == 0) return;
            string set = SetOf(x);
            float hour = Hour(a);
            Vector3 me = a.transform.position, at;
            foreach (Spot s in x.Spots)
            {
                if (s.Sleeps || !Fits(x, s, set, hour) || !StoryWhere.Standing(s.Thing, out at) || Utils.DistanceXZ(at, me) > SpotNear) continue; // flat: by its place, on the same ground
                themes.Add(new KeyValuePair<string, float>(ThemeOf(s), 30f)); // (over its idle thought: what it does now is what it speaks of)
                return;
            }
        }
        private static string ThemeOf(Spot s) { return s.Say.Length > 0 ? s.Say : "at_" + s.Kind; }

        // an outing reached: what it came to say is due at its next look round (NpcActor.DueRemark — NpcVoice says it, a player near), once a visit.
        // Where it is run, every TickEvery s a story person: its lines (a few), a place looked up for an outing on now
        internal static float TickEvery = 5f;
        private sealed class Visit { public float Next; public int Day = int.MinValue; public Spot Line; public string Due = ""; }
        private static readonly Dictionary<NpcActor, Visit> Visits = new Dictionary<NpcActor, Visit>(); // session: what its outing said, this day
        internal static void Forget(NpcActor a) { Visits.Remove(a); }
        internal static void Tick(NpcActor a)
        {
            Visit v;
            if (!Visits.TryGetValue(a, out v)) Visits[a] = v = new Visit();
            if (Time.time < v.Next) return;
            v.Next = Time.time + TickEvery;
            StoryPeople.Person x = Of(a.Id);
            if (x == null || !x.Outings) { v.Next = Time.time + 600f; return; } // (no outing of its own: nothing to look for — its file read anew makes a new body's look)
            string set = null; float hour = 0f; Vector3 at;
            foreach (Spot s in x.Spots)
            {
                if (s.Chance <= 0f || s.Sleeps) continue;
                if (set == null) { set = SetOf(x); hour = Hour(a); }
                if (!Fits(x, s, set, hour) || !StoryWhere.Standing(s.Thing, out at) || Utils.DistanceXZ(at, a.transform.position) > SpotNear) continue; // flat: by its place, on the same ground
                int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
                if (v.Day == day && v.Line == s) return; // (said this visit already, or due: no string made to tell)
                v.Day = day; v.Line = s;
                a.DueRemark = v.Due = ThemeOf(s);
                a.NextRemark = Time.time; // (at its next look round: NpcVoice.Tick, every ten seconds)
                return;
            }
            if (v.Due.Length > 0 && a.DueRemark == v.Due) a.DueRemark = ""; // (it left its outing before a player came near: not said later, somewhere else)
            v.Due = "";
        }

        // «raven npc day <who>»: the set that is its now, its hour, each line of its day and whether it fits
        internal static string Tell(NpcActor a)
        {
            StoryPeople.Person x = a != null ? Of(a.Id) : null;
            if (x == null) return (a != null ? a.Name : "?") + ": не людина сюжету (assets/story/people) — свого дня за годинами не має.";
            string set = SetOf(x);
            float hour = Hour(a);
            CultureInfo ci = CultureInfo.InvariantCulture;
            System.Text.StringBuilder t = new System.Text.StringBuilder();
            string far = a.Zdo != null ? Far(a.Zdo) : "", said = StoryHold.Of(a.Id);
            t.Append(a.Name).Append(": набір «").Append(set).Append("», година ").Append(hour.ToString("0.0", ci)).Append(said.Length > 0 ? ", слово сюжету над днем («" + said + "»)" : far.Length > 0 ? ", у поході («" + far + "»)" : Planned(a) ? ", за розкладом" : OnBreak(a) ? ", перерва" : ", вільний час");
            foreach (Spot s in x.Spots)
            {
                if (s.Set != set) continue;
                t.Append("\n").Append(s.Planned ? "at " : "spot ").Append(s.HourFrom < 0f ? "весь день" : s.HourFrom.ToString("0.#", ci) + "-" + s.HourTo.ToString("0.#", ci)).Append(" ").Append(s.Sleeps ? Sleep : s.Thing + " " + s.Kind + (s.Meet.Length > 0 ? " (збори «" + s.Meet + "»)" : ""));
                Vector3 at;
                if (!InHours(s, hour)) t.Append((s.Days > 1 || s.Chance > 0f || Gaps(s)) && InHours(new Spot { HourFrom = s.HourFrom, HourTo = s.HourTo }, hour) ? ": не цей день (" + (Gaps(s) ? "раз на " + s.GapFrom + "–" + s.GapTo + " дні навмання" + (s.Group.Length > 0 ? ", разом з «" + s.Group + "»" : "") : s.Chance > 0f ? "іноді, " + Mathf.RoundToInt(s.Chance * 100f) + "% днів" : "раз на " + s.Days + " дні") + ")" : ": не ця година");
                else if (!Live(s)) t.Append(": не тепер (when " + s.When + ")");
                else if (!Fits(x, s, set, hour)) t.Append(": сьогодні відступ (рядок з chance) замість нього");
                else if (s.Sleeps) t.Append(": сон ще не зроблено (вільний час)");
                else if (!StoryWhere.Standing(s.Thing, out at)) t.Append(": місця нема у світі");
                else if (Utils.DistanceXZ(at, NpcRules.Home(a)) > Reach) t.Append(s.Planned ? ": зараз, похід (далі " + Mathf.RoundToInt(Reach) + " м від дому: Far)" : ": далі " + Mathf.RoundToInt(Reach) + " м від дому"); // flat: as Places
                else if (LocState.Absent(StoryWhere.Piece(s.Thing))) t.Append(": деталі нема в цьому стані місця");
                else t.Append(": зараз");
            }
            Npcs.Log(t.ToString());
            return t.ToString();
        }
    }
}
#endif

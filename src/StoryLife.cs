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
    //     (kind)  or a doing of its own instead of the kind: «do: <name> = <kind> <words…>» in its [person] above the line — the name stands
    //             where the kind does and is read as those words (Lit's «hammer» = tend clip dv_hammer.loop from … tool): Doing
    //     when    (last, as in «day:»: «… when <world conditions>») only while these hold (the user 2026-11-16: one line of a phase — Torvald's watch
    //             from the crypt on, Heid tidying the Hall after the first heart, Lit's work on the walls in chapter 2 — not a set of its own each). The
    //             world's conditions only; a stage of a server's quest — «step ch2 work1» — holds while it is on. A phase with a name of its own:
    //             «phase <name>» ([phase <name>] with its «when:» lines — «or» — in assets/story/people: StoryPeople.Phase); the hour's
    //             own: «night», «day»; «weather rain|storm|snow|fog|clear» — judged by the game that runs it (its player's sky), so never
    //             on a far line (Local: a way out and back is not put off by rain, and the server has no sky to ask)
    //     days    only every n-th day of the game (the user 2026-11-16: Torvald goes to change the watch once in three days, not every evening):
    //             the world's day (EnvMan.GetDay) divisible by n — the same day on every game; hours across midnight count to the day they began
    //     (none)  an «at:» line with none of days, chance, daily acts at random intervals (the user 2026-11-16: «не кожен день… всі дії мають мати
    //             інтервали рандомні в ядрі і лише якщо цього потребуватиму я тоді зробимо певні дії кожнодневними»): a day it acts, then the next
    //             one GapMin…GapMax days on (Acts: from the line's seed and the world's day — the same days on every game, nothing kept)
    //     every   «every <a>-<b>»: that gap in days for this line, not GapMin…GapMax
    //     daily   every day — only where the user asks for it
    //     group   «group <name>»: lines of different people with the same name act on the same days (Heid and Torvald by the stones of the
    //             fallen at dawn); lines of one gathering («meet») share their days by its name by themselves
    //     one     «one <name>» (the user 2026-11-22: «сьогодні коло біля вогню, завтра всі за столом, якщо він є в цей момент»): of the lines of
    //             that name one place a day. The places are said once for everyone, in a block of assets/story/people:
    //               [one vechir]
    //               place: hall.vohnyshche
    //               place: hall.stil when state zala >= 2      (a place counts while its «when» holds and it stands in the place's state now)
    //             each day one of those that count, at random, the same on every game and for every person (Picked: the name and the world's
    //             day); the line's own «when» only says whether this person comes. Every day, no gaps: the day's choice is its variety.
    //             With «meet» the same gathering's name on each line: its talks go where it is that day
    //     over    a line of a phase that takes the whole of its hours (the user 2026-11-22; Lit's «не заважай» days): while it fits, no other
    //             line of its set fits in those hours — no outing («chance»), no usual line — and no break is taken from it
    //     haul    «haul <place[.piece]>» (the user 2026-11-22: Torvald «бере біля скрині, несе до вогню, дошка зникає»): an errand of two places —
    //             it goes to that place first, takes a plank on its shoulder there (HaulCarry, the thing HaulItem: StoryProps, a scene's kind of
    //             thing — with a term), carries it to the line's own place and puts it down there (HaulPut; at its mark HaulMark the plank is gone:
    //             into the fire). One plank a visit (NpcPlaces: Place.Then, AtMark, Ends — an order, a scene, a talk, no way on: the plank is taken
    //             off; the game that ran it gone mid-way, a reload: the next look of its day does it — HaulLeft, within TickEvery s);
    //             not with sit, clip or tool, not for one who carries a thing of its own («carry:»). The pile not there: the line is a plain place.
    //             «lays» after it (the user 2026-11-22: Eyvind's planks by the wall — «лежить купою біля стіни і зникає згодом»): at the mark the
    //             plank is let fall, not gone, and stays there on a pile for HaulLies s (StoryPile: a look only, a few at most)
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
            public string Haul = ""; // «haul <place>»: it takes a plank there first and carries it here (Places: an errand of two places)
            public bool Lays;        // «lays» (with «haul»): the plank put down stays on a pile for a while (StoryPile), not gone at once
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
            public bool Over;    // «over»: while it fits, no other line of its set in those hours does (Fits), and no break from it
            public string One = ""; // «one <name>»: one place a day of the lines of that name (Picked)
            public Choice Pick;     // (its block «[one <name>]», found once the people are read: Bind; null — no such block, the line never fits)
            public bool Local;   // its «when» asks the sky of the game that runs it («weather»): never a far line (FarNow)
            public string Say = "", Who = ""; // «say <theme>»: its words there (Themes) · whose line (the seed of its days: StoryPeople.Parse)
            public uint Seed; public bool Seeded; // (Lucky: from its owner, place and hour — made once, not a string a check)
            public Action<string> WhenLog; // (Live: made once — a check asks it every few seconds a person)
        }
        internal sealed class Day { public string Set = "", When = ""; }
        // «[one <name>]»: the places one of which is the day's (Picked)
        internal sealed class Choice
        {
            public string Name = "";
            public readonly List<KeyValuePair<string, string>> Places = new List<KeyValuePair<string, string>>(); // a place → its «when» ("" always), in the file's order
            public uint Seed; public int Day = int.MinValue; public float Until; public string Now = ""; // (the day's place, kept OneEvery s)
            public Action<string> Log;
        }

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
                s = Read(w[0] + " " + string.Join(" ", w, 2, w.Length - 2), out why);
                if (s == null) return null;
                s.Word = s.Thing + (s.By.Length > 0 ? " by " + s.By : "") + (s.Sit ? " sit" : s.Clip.Length > 0 ? " clip " + s.Clip + (s.From.Length > 0 ? " from " + s.From : "") + (s.Out.Length > 0 ? " out " + s.Out : "") : " stand"); // (far: Far)
            }
            s.Planned = true; s.HourFrom = a; s.HourTo = b; s.When = when; s.Local = AsksSky(when);
            return s;
        }

        // «<set> <place> [by <place>] <kind> [sit] [clip c [from c] [out c]] [hours a-b] [stay a-b] [want n]»; why: what is wrong, null fine
        internal static Spot Parse(string v, out string why)
        {
            Spot s = Read(v, out why);
            if (s != null && (s.Over || s.One.Length > 0)) { why = "over, one — слова рядка «at:» (день за годинами), не «spot:»"; return null; }
            return s;
        }
        private static Spot Read(string v, out string why)
        {
            why = null;
            string when = CutWhen(ref v);
            string[] w = v.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length < 3) { why = "spot: <набір> <місце> <вид> …"; return null; }
            Spot s = new Spot { Set = w[0], Thing = w[1], When = when, Local = AsksSky(when) };
            int i = 2;
            if (w[i] == "by" && i + 2 < w.Length) { s.By = w[i + 1]; i += 2; }
            s.Kind = w[i++];
            if (!NpcPlaces.Kinds.ContainsKey(s.Kind)) { why = "нема виду «" + s.Kind + "»"; return null; }
            for (; i < w.Length; i++)
            {
                if (w[i] == "sit") { s.Sit = true; continue; }
                if (w[i] == "tool") { s.Tool = true; continue; }
                if (w[i] == "daily") { s.Daily = true; continue; }
                if (w[i] == "over") { s.Over = true; continue; }
                if (w[i] == "lays") { s.Lays = true; continue; }
                if (i + 1 >= w.Length) { why = "«" + w[i] + "» без значення"; return null; }
                string val = w[++i];
                switch (w[i - 1])
                {
                    case "clip": s.Clip = val; break;
                    case "from": s.From = val; break;
                    case "out": s.Out = val; break;
                    case "moves": s.Moves = val; break;
                    case "meet": s.Meet = val; break;
                    case "haul": s.Haul = val; break;
                    case "hours": if (!Range(val, out s.HourFrom, out s.HourTo)) why = "погані години «" + val + "»"; break;
                    case "stay": if (!Range(val, out s.Min, out s.Max)) why = "поганий час «" + val + "»"; break;
                    case "want": if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Want)) why = "погане число «" + val + "»"; break;
                    case "chance": if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out s.Chance) || s.Chance <= 0f || s.Chance >= 1f) why = "chance: від 0 до 1 (не включно), «" + val + "»"; break;
                    case "say": s.Say = val; break;
                    case "days": if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out s.Days) || s.Days < 1) why = "days: ціле число від 1, «" + val + "»"; break;
                    case "every": { float a, b; if (!Range(val, out a, out b) || a < 1f || b < a || a != Mathf.Floor(a) || b != Mathf.Floor(b)) why = "every <a>-<b>: цілі дні від 1, a ≤ b, «" + val + "»"; else { s.GapFrom = (int)a; s.GapTo = (int)b; } break; }
                    case "group": s.Group = val; break;
                    case "one": s.One = val; break;
                    default: why = "невідоме слово «" + w[i - 1] + "»"; break;
                }
                if (why != null) return null;
            }
            if (s.Sit && s.Clip.Length > 0) { why = "або sit, або clip"; return null; }
            if (s.Haul.Length > 0 && (s.Sit || s.Clip.Length > 0 || s.Tool)) { why = "haul <місце> — несе дошку звідти сюди: не разом із sit, clip чи tool"; return null; }
            if (s.Lays && s.Haul.Length == 0) { why = "lays — слово до haul <місце>: принесена дошка лишається купою"; return null; }
            if ((s.Daily || s.Group.Length > 0 || s.GapFrom != GapMin || s.GapTo != GapMax) && (s.Days > 1 || s.Chance > 0f)) { why = "daily, every, group — для рядка з випадковими проміжками: не разом із days чи chance"; return null; }
            if (s.One.Length > 0 && (s.Daily || s.Group.Length > 0 || s.GapFrom != GapMin || s.GapTo != GapMax || s.Days > 1 || s.Chance > 0f)) { why = "one <назва> — одне з місць на день, щодня: не разом із daily, every, group, days чи chance"; return null; }
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

        // its «when» asks the sky («weather …»): only a game with a player knows it (NpcVoice.Weather: the sky over this game's player)
        private const string SkyWord = "weather";
        private static bool AsksSky(string when)
        {
            if (when.Length == 0) return false;
            foreach (string c in when.Split(',')) { string t = c.Trim().TrimStart('!').TrimStart(); if (t == SkyWord || t.StartsWith(SkyWord + " ", StringComparison.Ordinal)) return true; }
            return false;
        }

        // «do: <name> = <kind> <words…>»: a doing of its own, for the lines below it (name → its words); why: what is wrong, null fine
        internal static string ParseDo(string v, Dictionary<string, string> into)
        {
            int eq = v.IndexOf('=');
            string name = eq > 0 ? v.Substring(0, eq).Trim() : "", words = eq > 0 ? v.Substring(eq + 1).Trim() : "";
            if (name.Length == 0 || words.Length == 0 || name.IndexOfAny(Spaces) >= 0) return "do: <назва> = <вид> <слова…>";
            if (NpcPlaces.Kinds.ContainsKey(name) || name == Sleep || name == "by") return "do: «" + name + "» — уже слово рядка (вид місця чи sleep)";
            if (!NpcPlaces.Kinds.ContainsKey(words.Split(Spaces, StringSplitOptions.RemoveEmptyEntries)[0])) return "do: «" + name + "» має починатися з виду місця (NpcPlaces.Kinds)";
            if (words.IndexOf(" when ", StringComparison.Ordinal) >= 0) return "do: без «when» — умови каже сам рядок";
            into[name] = words;
            return null;
        }

        // a line «at:» / «spot:» with its doing's name put as its words: the word where the kind stands (lead words before the place: 1 a spot's set,
        // 2 an «at:»'s set and hours; after the place and its «by <place>»)
        internal static string Doing(string v, Dictionary<string, string> doings, int lead)
        {
            if (doings.Count == 0) return v;
            string when = "";
            int at = v.IndexOf(" when ", StringComparison.Ordinal);
            if (at >= 0) { when = v.Substring(at); v = v.Substring(0, at); }
            string[] w = v.Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
            int i = lead + 1;
            if (i + 2 < w.Length && w[i] == "by") i += 2;
            string words;
            if (i < w.Length && doings.TryGetValue(w[i], out words)) w[i] = words;
            return string.Join(" ", w) + when;
        }

        // «place: <place[.piece]> [when <world conditions>]» of a block «[one <name>]»; why: what is wrong, null fine
        internal static string ParsePlace(string v, Choice into)
        {
            string when = CutWhen(ref v);
            v = v.Trim();
            if (v.Length == 0 || v.IndexOfAny(Spaces) >= 0) return "place: <місце[.деталь]> [when <умови світу>]";
            if (AsksSky(when)) return "place: без «weather» — місце дня одне для всіх ігор";
            foreach (KeyValuePair<string, string> p in into.Places) if (p.Key == v) return "place: «" + v + "» уже є в «one " + into.Name + "»";
            into.Places.Add(new KeyValuePair<string, string>(v, when));
            return null;
        }

        // the people read: what a person's lines know — its «one» lines their blocks, whether any line is «over»
        internal static void Bind(StoryPeople.Person x, Dictionary<string, Choice> ones)
        {
            x.Overs = false; x.Hauls = false;
            foreach (Spot s in x.Spots)
            {
                x.Overs |= s.Over; x.Hauls |= s.Haul.Length > 0;
                if (s.One.Length == 0) continue;
                if (!ones.TryGetValue(s.One, out s.Pick)) { Story.Log("people: " + x.Id + ": «one " + s.One + "» — нема блоку [one " + s.One + "] (assets/story/people): рядок не діє ніколи"); continue; }
                bool named = false;
                foreach (KeyValuePair<string, string> p in s.Pick.Places) named |= p.Key == s.Thing;
                if (!named) Story.Log("people: " + x.Id + ": «one " + s.One + "» — місця «" + s.Thing + "» нема в блоці [one " + s.One + "]: рядок не діє ніколи");
            }
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
            if (s.Days <= 1 && s.Chance <= 0f && !gaps && s.One.Length == 0) return true;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (hour < 0f) day--;
            if (s.One.Length > 0) return Picked(s, day);
            if (gaps) return Acts(s, day);
            return (s.Days <= 1 || ((day % s.Days) + s.Days) % s.Days == 0) && (s.Chance <= 0f || Lucky(s, day));
        }

        // a line of its day by the hour that acts at random intervals: none of days, chance, daily (a free-time «spot:» is weighed, not planned)
        private static bool Gaps(Spot s) { return s.Planned && !s.Sleeps && !s.Daily && s.Days <= 1 && s.Chance <= 0f && s.One.Length == 0; }

        // «one <name>»: the day's place of that name is this line's — one of the block's places that count now (its «when» holds, the place's
        // state has the piece: a table only where there is one), from the name and the day alone: every game and every person of the name
        // agree. The answer kept OneEvery s a block (a state may change under it; asked at each look of each of its people)
        internal static float OneEvery = 5f;
        private static readonly List<string> _ones = new List<string>();
        private static bool Picked(Spot s, int day)
        {
            Choice c = s.Pick;
            if (c == null) return false; // (no such block: said at Bind)
            if (c.Day == day && Time.time < c.Until) return c.Now == s.Thing;
            c.Day = day; c.Until = Time.time + OneEvery; c.Now = "";
            if (c.Log == null) { c.Seed = (uint)("one|" + c.Name).GetStableHashCode(); c.Log = e => Story.Log("people: one " + c.Name + ": place … when " + e); }
            _ones.Clear();
            Vector3 at;
            foreach (KeyValuePair<string, string> p in c.Places)
                if ((p.Value.Length == 0 || StoryCond.Holds(null, p.Value, c.Log, true)) && StoryWhere.Standing(p.Key, out at) && !LocState.Absent(StoryWhere.Piece(p.Key))) _ones.Add(p.Key);
            if (_ones.Count == 0) return false;
            uint h = c.Seed ^ (uint)day * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            c.Now = _ones[(int)(h % (uint)_ones.Count)];
            return c.Now == s.Thing;
        }

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

        // the line fits now: of its set, in its hours and on its day, and no «over» line nor outing of its set on now over it (their own lines fit)
        private static bool Fits(StoryPeople.Person x, Spot s, string set, float hour)
        {
            if (s.Set != set || !InHours(s, hour) || !Live(s)) return false;
            if (s.Over) return true;
            if (!x.Overs && !x.Outings) return true; // (no «over» nor «chance» line of its own at all: nothing to look over)
            Above(x, set, hour);
            return !x.OverOn && (s.Chance > 0f || !x.OutingOn);
        }

        // what stands over its usual lines now: an «over» line, an outing («chance») of its set on in this hour. Fits asks it for every line of
        // every look (its places, its far word, its gathering, its words): reckoned once a frame a person — for that set and hour — not once a line
        private static void Above(StoryPeople.Person x, string set, float hour)
        {
            if (x.AboveFrame == Time.frameCount && x.AboveHour == hour && x.AboveSet == set) return;
            x.AboveFrame = Time.frameCount; x.AboveHour = hour; x.AboveSet = set; x.OverOn = false; x.OutingOn = false;
            foreach (Spot o in x.Spots)
            {
                if (o.Sleeps || (!o.Over && o.Chance <= 0f) || o.Set != set || !InHours(o, hour) || !Live(o)) continue;
                if (o.Over) x.OverOn = true; else x.OutingOn = true;
            }
        }

        // where it stands at a spot: by the thing, a step from its edge, never its middle (the thing's pieces are looked over: only for the one it goes to)
        private static Vector3 Stand(Spot s, Vector3 at, Vector3 me, bool clip)
        {
            Vector3 side = me, by;
            if (s.By.Length > 0 && StoryWhere.Find(s.By, out by)) side = by;
            return StoryWhere.StandBy(s.Thing, side, at, clip, out by, s.Clip) ? by : NpcPlaces.Side(at, side, 1.5f);
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
                if (!s.Planned || s.Sleeps || s.Local || s.Set != set) continue; // (Local: its «when» asks this game's sky — never a way out and back)
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
                if (Fits(x, s, set, hour) && Stands(s, NpcRules.Home(a), out at)) return s.Chance > 0f || s.Over || !OnBreak(a); // (an outing, a line «over»: no break from it)
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
            bool planned = false, over = false;
            foreach (Spot s in x.Spots)
            {
                Vector3 at;
                if (!Fits(x, s, set, hour)) continue;
                if (s.Sleeps) continue; // sleep: the hook of its night — here it would lie down (its bed: StoryPeople «live … bed», NpcMind.Task.Sleep); till then these hours are its free ones
                if (!Stands(s, home, out at)) continue;
                _fit.Add(new KeyValuePair<Spot, Vector3>(s, at));
                planned |= s.Planned; over |= s.Over;
            }
            bool free = !planned || (!over && OnBreak(a)); // (its day says where it is: its free places only in a break)
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
                Vector3 pile;
                if (s.Haul.Length > 0 && x.Carry == null && StoryWhere.Standing(s.Haul, out pile) && !LocState.Absent(StoryWhere.Piece(s.Haul)) && Utils.DistanceXZ(pile, home) <= Reach) // flat: a place of its life round its home, as Stands
                    Hauls(a, p, spot, thing, pile, me);
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

        // «haul»: the line's place made an errand (NpcPlaces: Place.Then). The place of the list becomes the pile — it goes there, stands a moment,
        // the plank comes onto its shoulder (the clip first, then the thing: the clip leads the plank) — and its Then is the line's own place,
        // an object of its own: there the put-down clip, the plank gone at its mark, and at the visit's end however it came (nobody near to
        // see the clip begin, a talk, an order) the plank and the carrying clip are taken off. The plank has its own term besides (StoryProps.NpcFor)
        internal const string HaulItem = "FineWood", HaulCarry = "plank_carry", HaulPut = "plank_put", HaulMark = "down";
        private static readonly int HaulKey = "haul".GetStableHashCode(); // (the pile's place is not the line's own: its own key — where it was lately, what it found no way to)
        private static readonly Action<NpcActor> HaulGone = who => StoryProps.Put(who), HaulFalls = who => { StoryProps.Drop(who); }, HaulTaken = HaulTakes, HaulEnds = HaulOver, HaulEndsLaid = HaulLaid; // (made once: a place is made at every choice)
        internal static float HaulSize = 1.1f, HaulTake = 1.5f, HaulFor = 180f, HaulLies = 900f; // m the plank is long (as the scene's) · s it stands by the pile · s the carrying clip is asked for at most · s a plank laid («lays») stays on its pile (some twelve hours of the game)
        private static void Hauls(NpcActor a, NpcPlaces.Place p, Spot s, Vector3 thing, Vector3 pile, Vector3 me)
        {
            PAnimClip put = BodyClips.Find(HaulPut);
            if (put == null || BodyClips.Find(HaulCarry) == null) return; // (no such clips: a plain place)
            NpcPlaces.Place to = NpcPlaces.Apart(s.Kind, NpcPlaces.Side(thing, pile, 1.5f), thing + Vector3.up * 0.5f, thing);
            to.Stand = () => Stand(s, thing, a.transform.position, true);
            to.Clip = HaulPut; to.From = ""; to.Out = ""; to.Exact = true; to.Extra = s.Want;
            to.Min = to.Max = put.Length * 1.6f + 1f; // (its stay is scaled by its ways — NpcPlaces.Go: never shorter than the clip)
            to.MarkAt = put.Length * 0.7f;
            foreach (PAnimEvent e in put.Marks) if (e.Sound == HaulMark) to.MarkAt = e.T;
            if (s.Lays) { to.AtMark = HaulFalls; to.Ends = HaulEndsLaid; } // (let fall where the hands are: it lies — and at the visit's end goes onto the pile)
            else { to.AtMark = HaulGone; to.Ends = HaulEnds; }
            string from = s.Haul;
            p.At = NpcPlaces.Side(pile, me, 1.5f); p.Look = pile + Vector3.up * 0.5f; p.Key ^= HaulKey;
            p.Stand = () => { Vector3 by; return StoryWhere.StandBy(from, a.transform.position, pile, false, out by, "") ? by : NpcPlaces.Side(pile, a.transform.position, 1.5f); };
            p.Clip = ""; p.From = ""; p.Out = ""; p.Exact = false; p.Moves = ""; p.Sit = false; p.Tool = false;
            p.Min = p.Max = HaulTake;
            p.There = HaulTaken;
            p.Then = to;
        }
        private static void HaulTakes(NpcActor a)
        {
            if (a.Body == null || !a.Body.Clip(HaulCarry, HaulFor)) return; // (its body would not: no plank in the air)
            string why = StoryProps.Take(a, new StoryProps.Ask { Item = HaulItem, Size = HaulSize, Free = true });
            if (why != null) Story.Log("people: " + a.Id + ": haul: " + why);
        }
        private static void HaulLaid(NpcActor a)
        {
            string item; float size; Vector3 at; Quaternion turn;
            if (StoryProps.Lain(a, out item, out size, out at, out turn)) StoryPile.Add(a, item, size, at, turn, HaulLies); // (still in its hands — the clip never came to its mark: no pile, HaulOver takes it off)
            HaulOver(a);
        }
        private static void HaulOver(NpcActor a)
        {
            StoryProps.Put(a); // (a scene's kind of thing: off; nothing held — nothing done)
            BodyAnimator b = BodyAnimator.Of(a.GetComponent<Character>());
            string now = b != null ? b.ClipIn(NpcBody.ClipSlot) : null;
            if (a.Body != null && now != null && (now == HaulCarry || now == HaulPut || now == NpcMoves.Its(a.Def, HaulCarry) || now == NpcMoves.Its(a.Def, HaulPut))) a.Body.Clip(""); // (the carrying clip still on with no plank: off; another body's own of the name — NpcBody.Clip)
        }

        // a plank on it with no errand to end it (the game that ran it changed mid-way, it came back into sight, the files were read anew): taken
        // off. Not a scene's (the same thing and clips are a scene's too — StoryScenes.Holder), not under an order (NpcPlaces cuts its own errand
        // there). Looked at with its outings, every TickEvery s a person that hauls at all
        private static void HaulLeft(NpcActor a)
        {
            if (!a.Owned || !StoryProps.HasOf(a, HaulItem) || NpcPlaces.OnErrand(a) || StoryScenes.Holder(a) != 0L || (a.Mind != null && a.Mind.NowLayer > NpcMind.Layer.Whim)) return;
            Story.Log("people: " + a.Id + ": a plank on it and no errand to end it — taken off");
            HaulOver(a);
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
            if (x != null && x.Hauls) HaulLeft(a);
            if (x == null || !x.Outings) { if (x == null || !x.Hauls) v.Next = Time.time + 600f; return; } // (no outing of its own: nothing to look for — its file read anew makes a new body's look)
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
                t.Append("\n").Append(s.Planned ? "at " : "spot ").Append(s.HourFrom < 0f ? "весь день" : s.HourFrom.ToString("0.#", ci) + "-" + s.HourTo.ToString("0.#", ci)).Append(" ").Append(s.Sleeps ? Sleep : s.Thing + " " + s.Kind + (s.Meet.Length > 0 ? " (збори «" + s.Meet + "»)" : "") + (s.Haul.Length > 0 ? " (несе дошку від " + s.Haul + (s.Lays ? ", кладе купою" : "") + ")" : "") + (s.Over ? " (over)" : ""));
                Vector3 at;
                if (!InHours(s, hour)) t.Append((s.Days > 1 || s.Chance > 0f || Gaps(s) || s.One.Length > 0) && InHours(new Spot { HourFrom = s.HourFrom, HourTo = s.HourTo }, hour) ? ": не цей день (" + (s.One.Length > 0 ? "сьогодні інше місце з «one " + s.One + "»" : Gaps(s) ? "раз на " + s.GapFrom + "–" + s.GapTo + " дні навмання" + (s.Group.Length > 0 ? ", разом з «" + s.Group + "»" : "") : s.Chance > 0f ? "іноді, " + Mathf.RoundToInt(s.Chance * 100f) + "% днів" : "раз на " + s.Days + " дні") + ")" : ": не ця година");
                else if (!Live(s)) t.Append(": не тепер (when " + s.When + ")");
                else if (!Fits(x, s, set, hour)) t.Append(": зараз замість нього рядок з over чи відступ (chance)");
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

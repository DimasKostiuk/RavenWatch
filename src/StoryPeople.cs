#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's own people who live in the world (story-lore.md «Пролог»; the user 2026-11-01). The server keeps each
    // of them once in a world — never two, never none while their place stands — and says where they live; their day
    // is a resident's like anyone's (NpcResident: its duties about its home, the fire in the evening, its bed at night),
    // the story holding them (NpcLevers «protect»: nobody sends them off, hunger does not take them away).
    // Who they are and where they live is data (the user 2026-11-02: no names of the story in the code), StoryFiles kind
    // «people» (assets/story/people/*.txt):
    //   [person heid]
    //   npc: heid                                               its NPC template (NpcDefs)
    //   live: hall.vohnyshche when state zala >= 1.2             home (where it appears, its bed: «spawn», «bed») while the world's conditions
    //   live: torvald.vohnyshche spawn torvald.lava bed torvald.lizhko   hold — the first line that holds; the last, with no «when», otherwise
    //   after: world heid_called                                 not in the world at all till these hold (the world's
    //                                                            conditions only: the server keeps them, it has no player);
    //                                                            came, and they no longer hold (a jump back, a night begun anew) —
    //                                                            out of the world as by «live: away» (Person.OutNow)
    //   until: world ketil_gone                                  gone for good once these hold (its story's block is over: chapter 1 — Ketil killed
    //                                                            in the ambush): taken away where nobody sees it (a scene that faded it
    //                                                            out took it already), never made again. The world's conditions only
    //   live: away when world ch1_home, !world ch1_torv_home   out of the world for a while (its ZDO and memories kept, parked where nobody
    //                                                            comes; it goes when nobody sees it, and comes back to its next home out of sight)
    // Going (away, or for good by «until»): nobody near and its body loaded nowhere — the server takes it at once. Someone near (the user 2026-11-16:
    // Torvald «on watch» sat on by his camp 77 m from the Hall, where the player lives — the server waited for nobody within 150 m, and that never
    // came): the server says so in the world's key «leave_<npc>» — and the game that runs its body reads its «live:» itself too (AwayNow: no wait for
    // the server's look) — and that game takes it the first moment nobody sees it (LeaveTick:
    // no player within SeenNear m, none with a clear line to it within SeenFar m, not on this game's own screen; this game's own player sees nothing
    // while a scene holds his screen black — the user 2026-11-18: Torvald walks out of the Hall, the picture goes black, «він просто має різко
    // зникнути»: StoryScreen.Black, BlackHides) — till then it walks off from them
    // at its ease (OffWalk m, the farthest way from them); one who keeps it in sight keeps it LeaveCap s at most, then it goes in sight all the same. Never in a scene, a talk, or against a story's word that holds it (that leads it; it only goes).
// A scene's «npc <who> leave» (LeaveUnseen): out of the world the first moment it is out of the scene's shot — the shot is the eyes then (SeenNow).
    //   spot: home torvald.kamin think hours 6-11                a place of its own life between commands (StoryLife, its head)
    //   day: hall when state zala >= 1.3                         which set of its places is its now · at: hall 6-9 hall.rizba think — its day by the hour (StoryLife)
    //   do: hammer = tend clip dv_hammer.loop from dv_hammer.in out dv_hammer.out tool   a doing of its own: the name stands for these words in its lines below (StoryLife.Doing)
    //   [phase lit_works] + when: <world conditions>             a phase of the story with a name («or»: a line a way) — the condition «phase <name>» (Phase)
    //   stranger: !world eyvind_met [:: Чужинець]               not met yet while these world conditions hold: no name, no talk, quiet (StoryStranger)
    //   strangersay: hugin Хто це? / munin Мабуть, Літів.        what your ravens say the first time you come near it then (StoryStranger)
    //   notalk: reached ch2 tower_night, !reached ch2 dawn       no talk with it while these world conditions hold: E and Q open nothing, its name only —
    //                                                            its own words, scenes and road talks go on (StoryStranger: Torvald on his watch)
    //   fight: defend when reached ch2 sverre_fight              its way of fighting while these world conditions hold (the first line that holds; none:
    //                                                            its template's «fight:»), and while it fights (defend, hunt, wary) monsters see it — a
    //                                                            story person is unseen by them otherwise (Hidden, NpcVitals.StoryOwn). The user
    //                                                            2026-11-23: «щоб Ейвінд теж міг битись, але лише в кінці 2 глави… просто нпс можуть або
    //                                                            не можуть відбиватись». Put on its mind where it is run (FightTick), each FightEvery s
    //   notalksay: hurry                                         …and on E it says a line of its own theme «[<who>:hurry]» instead (StoryStranger.Hurry:
    //                                                            Eyvind leaving the Hall, the user 2026-11-23 — «просто має сказати якусь фразу, що спішить»)
    // A spot is «<place>.<named piece>» (the place's names: LocParts — loaded or not, Loc.NamedAt) or «<place>». It is born never on a piece itself
    // (a bench's seat, a fire): a step off its spawn towards its home, or — home there too, or no «spawn» — Aside, towards the place's middle.
    // Made as soon as its time and its home are there (looked at every Soon s while someone waits: the story's word must not leave the
    // player at an empty camp, the user 2026-11-05: Heid came a minute late, before his eyes): nobody within Born m — at its home; someone there —
    // out of their sight, and it walks in (NpcTravel «arrive», ComeFrom: the user 2026-11-15, Ketil popped up by the heart before his eyes); no
    // such spot (players all round) — at its home after BornWait s all the same. A new home (its «live:» changed): put there only where nobody sees
    // it (no player within Unseen m of it or of where it goes, its body not loaded here); else only its home changes and it walks there itself. One held by a word of the story (StoryHold) is
    // brought the same way to that word's place, not its home: unloaded far from it on its way, it would stand there for ever. Made straight as an object of the world (the
    // server's own, as NpcRescue does): its body comes when someone comes near.
    internal static class StoryPeople
    {
        internal const string Kind = "people";
        internal static float Every = 15f, Soon = 2f, Unseen = 150f, Born = 40f, BornWait = 8f, Moved = 6f, HomeNear = 60f; // (HomeNear: m from its home it is at home — its spots of its own life are round it)
        internal const string Away = "away"; // («live: away when …»: out of the world for a while — parked at AwayAt)
        internal static readonly Vector3 AwayAt = new Vector3(10300f, 0f, 10300f); // (the world's far corner, in the sea past every land: nobody comes, nothing of it is drawn)
        internal static float SeenNear = 15f, SeenFar = 110f, OffWalk = 50f, LeaveEvery = 1f; // (going while someone is near — LeaveTick: m always seen · m a clear line still sees it · m it walks off · s between its looks)
        private const string LeaveWhose = "storyleave";
        private static string LeaveKey(string npc) { return "leave_" + npc; }
        private static float _next;

        internal sealed class Home { public string At = "", Spawn = "", Bed = "", When = ""; }
        internal sealed class Person
        {
            public string Id = "", Npc = "", File = "";
            public readonly List<Home> Homes = new List<Home>(); // (in the file's order: the first whose «when» holds)
            public string After = ""; // (world conditions; «»: always)
            public string Until = ""; // (world conditions: once they hold it is gone for good — taken away where nobody sees it, never made again; «»: never)
            public readonly List<StoryLife.Spot> Spots = new List<StoryLife.Spot>(); // its own life's places and its day by the hour («spot:», «at:»: StoryLife)
            public readonly List<StoryLife.Day> Days = new List<StoryLife.Day>();   // which set of them is its now («day:», in the file's order: the first that holds)
            public bool Planned; // it has a day by the hour (an «at:» other than sleep): only then StoryLife.Far looks for a far line
            public bool Outings; // it has a line «chance»: only then StoryLife looks over its outings (Fits, Tick)
            public bool Overs;   // it has a line «over»: only then StoryLife looks over them (Fits; set by StoryLife.Bind)
            public bool Hauls;   // it has a line «haul»: only then StoryLife looks for a plank left on it (Tick; set by StoryLife.Bind)
            public int AboveFrame = -1; public float AboveHour; public string AboveSet; public bool OverOn, OutingOn; // (StoryLife.Above: what stands over its usual lines, reckoned once a frame)
            public readonly Dictionary<string, string> Doings = new Dictionary<string, string>(); // («do: <name> = <kind> <words…>»: its own doings, for the lines below — StoryLife.Doing)
            public Action<string> DayLog, HomeLog, AfterLog; // (its conditions' log, made once: they are asked every few seconds)
            // its time has come: its «after:» holds (none: always). Once it came and this no longer holds it is out of the world as by «live: away» (OutNow)
            internal bool Came
            {
                get
                {
                    if (After.Length == 0) return true;
                    if (AfterLog == null) { string id = Id; AfterLog = e => Story.Log("people: " + id + ": after " + e); }
                    return StoryCond.Holds(null, After, AfterLog, true);
                }
            }
            // out of the world now: its «live:» says away, or its «after:» no longer holds though it came — the author's jump back before its coming
            // (the gap of 2026-11-18: Eyvind stayed in a Hall jumped back to before him), and a story that goes back past it in play (Baldr: a night on
            // the tower begun anew, its quest back before «baldr_come»). Parked, not taken for good: its next home brings it back out of sight
            internal bool OutNow(Home h) { return !Came || h != null && h.At == Away; }
            public StoryProps.Ask Carry; // a thing of its own it always carries (StoryProps «carry:»); null: none
            public string Stranger = "", StrangerName = ""; // («stranger: <world conditions> [:: <name>]»: not met yet while they hold — StoryStranger)
            public string NoTalk = ""; // («notalk: <world conditions>»: no talk with it while they hold — its own words, scenes, road talks go on; StoryStranger)
            public string NoTalkSay = ""; // («notalksay: <theme>»: what it says on E meanwhile — a line of «[<who>:<theme>]»; none: silent)
            public readonly List<KeyValuePair<string, string>> Fights = new List<KeyValuePair<string, string>>(); // («fight: <way> [when <world conditions>]»: its way, the conditions; in the file's order)
            public float FightAt = -1f; public string FightWay = ""; // (FightNow reckoned at most every FightEvery s: IsEnemy asks it of every pair a monster looks at)
            public readonly List<KeyValuePair<bool, string>> StrangerSay = new List<KeyValuePair<bool, string>>(); // («strangersay:»: munin?, the line — in its order)
            public float BornWait = -1f;
            public string Why = "";
            internal Home Now
            {
                get
                {
                    if (HomeLog == null) { string id = Id; HomeLog = e => Story.Log("people: " + id + ": live … when " + e); }
                    foreach (Home h in Homes) if (h.When.Length == 0 || StoryCond.Holds(null, h.When, HomeLog, true)) return h;
                    return null;
                }
            }
        }

        private static List<Person> _all;
        internal static List<Person> All { get { if (_all == null) Load(); return _all; } }

        internal static void Reload() { _all = null; _npcs = null; _afterOf = null; _byNpc = null; }
        // a template one of them wears (NpcVitals.StoryOwn: no monster sees them unless the template says «unseen: no»)
        private static HashSet<string> _npcs;
        internal static bool Wears(string npc) { if (_all == null) Load(); return _npcs != null && _npcs.Contains(npc); }

        // the one whose talk this is has come into this world: its «after:» holds (gone since by its «until:» — it came all the same). A talk of nobody of the
        // story's (the settlers'): yes. Asked for what was learnt from it in another world (NpcTalk.Heard: the book shows it here only once it has come)
        internal static bool Came(string talk)
        {
            if (_all == null) Load(); // (first: a reading anew empties the talks' map too)
            if (_afterOf == null) // (once a reading: whose talk is whose — the book asks it for each line a second)
            {
                _afterOf = new Dictionary<string, string>();
                foreach (Person x in _all)
                {
                    NpcDef d = NpcDefs.Find(x.Npc);
                    if (d != null && d.TalkId.Length > 0 && !_afterOf.ContainsKey(d.TalkId)) _afterOf[d.TalkId] = x.After;
                }
            }
            string after;
            return !_afterOf.TryGetValue(talk ?? "", out after) || after.Length == 0 || StoryCond.Holds(null, after, e => Story.Log("people: talk " + talk + ": after " + e), true);
        }
        private static Dictionary<string, string> _afterOf;

        // the one whose talk this is has gone from this world for good: its «until:» holds (the book: what was not asked of it nobody can be asked now — NpcTalkTime.cs).
        // A few people, each once; asked only as the book's open entry is drawn
        internal static bool Left(string talk)
        {
            if (_all == null) Load();
            foreach (Person x in _all)
            {
                if (x.Until.Length == 0) continue;
                NpcDef d = NpcDefs.Find(x.Npc);
                if (d != null && d.TalkId == talk) return StoryCond.Holds(null, x.Until, e => Story.Log("people: talk " + talk + ": until " + e), true);
            }
            return false;
        }

        // the world's own one of that template: not the animation stage's mannequin in its body, not one a scene made for itself (it goes with the
        // scene — the server took a scene's Ketil for a second one and would have taken one of the two away)
        internal static bool Of(ZDO z, string npc)
        {
            if (z == null || z.GetString(Npcs.KId, "") != npc) return false;
            string tag = z.GetString(Npcs.KTag, "");
            return tag != AnimStage.TagName && tag != StoryScenes.MadeTag;
        }

        // the world's word about its people: «npcbed <who>» — that person has a bed of its own (a player gave it one: its round menu «Оселя» → «Ліжко», for a story person
        // whose template opens that ring: «menu: home talk»). The world's (the server's census sees everyone; a client, those loaded near it)
        internal static void Conds()
        {
            // a phase of the story by its name (Phase below). No Dry line on purpose: it is for people's days, words and talks — a quest that waited
            // on one would be «not judged» by the dry walk and the author's jump, said aloud
            StoryCond.Register("phase", new StoryCond.Kind { Usage = "phase <назва>   (фаза сюжету: блок [phase <назва>] у assets/story/people — поки справджується хоч один його рядок when:)", Min = 1, World = true,
                Test = (p, a) => Phase(a[1]) });
            StoryCond.Register("npcbed", new StoryCond.Kind { Dry = "world npcbed:{1} : a player gives that person a bed (its round menu: «Оселя» → «Ліжко»)", Usage = "npcbed <хто>   (у людини сюжету є своє ліжко)", Min = 1, World = true,
                Test = (p, a) =>
                {
                    foreach (ZDO z in NpcCensus.People())
                        if (Of(z, a[1]) && NpcLocker.Text(z, NpcResident.KBed).Length > 0) return true; // (its link: the truth — NpcResident)
                    return false;
                } });
            // the person of the world (never a scene's copy) within m of a place, wherever it is loaded or not (the census): a scene that needs it at home
            // (the user's chapter 1, 2026-11-14: «Відвар» by the fire with the old man back from his stash — not out on the road; «Торвальда нема» not while he
            // is still in sight). Not in the world at all: false
            StoryCond.Register("npcnear", new StoryCond.Kind { Dry = "free", Usage = "npcnear <хто> <місце> <м>   (людина сюжету в межах стількох метрів від місця)", Min = 3, World = true,
                Check = a => Errands.F(a[3], -1f) <= 0f ? "npcnear: м — число" : StoryWhere.Why(a[2]),
                Test = (p, a) =>
                {
                    float m = Errands.F(a[3], 10f), off;
                    foreach (ZDO z in NpcCensus.People())
                        if (Of(z, a[1]) && StoryWhere.Off(a[2], z.GetPosition(), out off) && off <= m) return true; // (a named piece: from its edge, where a word «hold» stands one — StoryWhere.Off)
                    return false;
                } });
            // the person of the world is where its standing word has it, as that word has it: stood at its spot, sat, in its clip (StoryHold.There —
            // written by the game that runs it); no word: yes. A scene waits for this of its own people by itself (StoryScenes.Unset): the condition is
            // for a stage's «wait:» or a talk that needs someone in place. Not in the world at all: false
            StoryCond.Register("npcset", new StoryCond.Kind { Dry = "free", Usage = "npcset <хто>   (людина сюжету стала там, де її тримає слово: стоїть на місці, сидить, у своєму кліпі; слова нема — так)", Min = 1, World = true,
                Test = (p, a) =>
                {
                    foreach (ZDO z in NpcCensus.People())
                        if (Of(z, a[1])) return StoryHold.There(z);
                    return false;
                } });
        }

        private static void Load()
        {
            StoryTrackConds.Register(); // (their «after:» is conditions: known before they are read — the server reads its people first)
            _all = new List<Person>();
            _phases = new Dictionary<string, List<string>>();
            _noPhase.Clear(); _phaseKept.Clear();
            _ones = new Dictionary<string, StoryLife.Choice>();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false)) Parse(f.Key, f.Value);
            _npcs = new HashSet<string>();
            foreach (Person x in _all) { _npcs.Add(x.Npc); StoryLife.Bind(x, _ones); }
            Story.Log("people: " + _all.Count + " read, phases: " + _phases.Count + ", choices of a day: " + _ones.Count);
        }

        // A phase of the story with a name of its own (the user 2026-11-22: «щоб в наступних актах ми просто додавали якісь умови легко»):
        //   [phase lit_works]
        //   when: reached ch2 work1, !reached ch2 seen1      it holds while any of its lines does («or»: a line a way; a new act adds a line here,
        //   when: step ch2 work3                             and no person's day, word or talk that asks «phase lit_works» is touched)
        // The world's conditions only, and no «phase» nor «weather» in them (storycheck). Asked as the condition «phase <name>» anywhere a
        // condition is: a day's line «… when phase x», a word's theme «[lit:idle ?phase x]», a gathering's talk «? phase x»
        private static Dictionary<string, List<string>> _phases;
        private static Dictionary<string, StoryLife.Choice> _ones; // («[one <name>]» with its «place:» lines: the places one of which is the day's — StoryLife, the word «one»)
        private static readonly HashSet<string> _noPhase = new HashSet<string>(); // session: names asked and not found, said once a reading
        private static readonly Action<string> PhaseLog = e => Story.Log("people: phase … when " + e);
        private static int _phaseDeep;
        // its answer is kept PhaseEvery s (a phase stands on stages and facts of the world: it turns once in an hour of play, and a person's day asks
        // it for every line of every look — a few lists of conditions each time otherwise)
        internal static float PhaseEvery = 1f;
        private sealed class PhaseKept { public float Until; public bool Yes; }
        private static readonly Dictionary<string, PhaseKept> _phaseKept = new Dictionary<string, PhaseKept>(); // session: a phase → its answer, a second
        internal static bool Phase(string name)
        {
            if (_all == null) Load();
            PhaseKept k;
            if (_phaseKept.TryGetValue(name, out k) && Time.time < k.Until) return k.Yes;
            if (k == null) _phaseKept[name] = k = new PhaseKept();
            k.Until = Time.time + PhaseEvery;
            return k.Yes = PhaseNow(name);
        }
        private static bool PhaseNow(string name)
        {
            List<string> ways;
            if (!_phases.TryGetValue(name, out ways)) { if (_noPhase.Add(name)) Story.Log("people: no phase «" + name + "» ([phase " + name + "] in assets/story/people): it never holds"); return false; }
            if (_phaseDeep > 0) return false; // (a phase asking a phase: storycheck stops it; here it only never holds)
            _phaseDeep++;
            try { foreach (string w in ways) if (StoryCond.Holds(null, w, PhaseLog, true)) return true; }
            finally { _phaseDeep--; }
            return false;
        }

        private static void Parse(string file, string text)
        {
            Person cur = null;
            List<string> phase = null;
            StoryLife.Choice one = null;
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value; int n = ln.Key;
                if (line.StartsWith("[person ") && line.EndsWith("]")) { phase = null; one = null; cur = new Person { Id = line.Substring(8, line.Length - 9).Trim(), File = file }; cur.Npc = cur.Id; _all.Add(cur); continue; }
                if (line.StartsWith("[phase ") && line.EndsWith("]"))
                {
                    cur = null; one = null;
                    string name = line.Substring(7, line.Length - 8).Trim();
                    if (!_phases.TryGetValue(name, out phase)) _phases[name] = phase = new List<string>();
                    else Story.Log("people: " + file + ":" + n + ": фаза «" + name + "» уже є — рядки додано до неї");
                    continue;
                }
                if (line.StartsWith("[one ") && line.EndsWith("]"))
                {
                    cur = null; phase = null;
                    string name = line.Substring(5, line.Length - 6).Trim();
                    if (!_ones.TryGetValue(name, out one)) _ones[name] = one = new StoryLife.Choice { Name = name };
                    else Story.Log("people: " + file + ":" + n + ": «one " + name + "» уже є — місця додано до нього");
                    continue;
                }
                int c = line.IndexOf(':');
                if (one != null)
                {
                    string bad = c > 0 && line.Substring(0, c).Trim() == "place" ? StoryLife.ParsePlace(line.Substring(c + 1), one) : "у блоці [one …] лише рядки «place: <місце> [when <умови світу>]»";
                    if (bad != null) Story.Log("people: " + file + ":" + n + ": " + bad);
                    continue;
                }
                if (phase != null)
                {
                    if (c > 0 && line.Substring(0, c).Trim() == "when" && line.Substring(c + 1).Trim().Length > 0) phase.Add(line.Substring(c + 1).Trim());
                    else Story.Log("people: " + file + ":" + n + ": у фазі лише рядки «when: <умови світу>»");
                    continue;
                }
                if (cur == null || c <= 0) { Story.Log("people: " + file + ":" + n + ": не розумію «" + line + "»"); continue; }
                string k = line.Substring(0, c).Trim(), v = line.Substring(c + 1).Trim();
                if (k == "spot") v = StoryLife.Doing(v, cur.Doings, 1); else if (k == "at") v = StoryLife.Doing(v, cur.Doings, 2);
                switch (k)
                {
                    case "do": { string bad = StoryLife.ParseDo(v, cur.Doings); if (bad != null) Story.Log("people: " + file + ":" + n + ": " + bad); break; }
                    case "npc": cur.Npc = v; break;
                    case "live": cur.Homes.Add(HomeOf(v)); break;
                    case "after": cur.After = v; break;
                    case "until": cur.Until = v; break;
                    case "spot": { string bad; StoryLife.Spot sp = StoryLife.Parse(v, out bad); if (sp != null) { sp.Who = cur.Id; cur.Spots.Add(sp); cur.Outings |= sp.Chance > 0f; } else Story.Log("people: " + file + ":" + n + ": " + bad); break; }
                    case "at": { string bad; StoryLife.Spot sp = StoryLife.ParseAt(v, out bad); if (sp != null) { sp.Who = cur.Id; cur.Spots.Add(sp); cur.Planned |= !sp.Sleeps; cur.Outings |= sp.Chance > 0f; } else Story.Log("people: " + file + ":" + n + ": " + bad); break; }
                    case "day": { string bad; StoryLife.Day d = StoryLife.ParseDay(v, out bad); if (d != null) cur.Days.Add(d); else Story.Log("people: " + file + ":" + n + ": " + bad); break; }
                    case "carry": { string bad; cur.Carry = StoryProps.ReadCarry(v, out bad); if (cur.Carry == null) Story.Log("people: " + file + ":" + n + ": " + bad); break; }
                    case "stranger": { int sep = v.IndexOf("::", StringComparison.Ordinal); cur.Stranger = (sep >= 0 ? v.Substring(0, sep) : v).Trim(); cur.StrangerName = sep >= 0 ? v.Substring(sep + 2).Trim() : ""; break; }
                    case "notalk": cur.NoTalk = v.Trim(); break;
                    case "notalksay": cur.NoTalkSay = v.Trim(); break;
                    case "fight":
                        {
                            int wi = v.IndexOf(" when ", StringComparison.Ordinal);
                            string way = (wi >= 0 ? v.Substring(0, wi) : v).Trim().ToLowerInvariant();
                            if (Array.IndexOf(FightWays, way) < 0) { Story.Log("people: " + file + ":" + n + ": fight: off|flee|wary|defend|hunt [when <умови світу>]"); break; }
                            cur.Fights.Add(new KeyValuePair<string, string>(way, wi >= 0 ? v.Substring(wi + 6).Trim() : ""));
                            break;
                        }
                    case "strangersay":
                        foreach (string part in v.Split('/'))
                        {
                            string t = part.Trim(); int sp = t.IndexOf(' ');
                            string who = sp > 0 ? t.Substring(0, sp) : "";
                            if (who != "hugin" && who != "munin") { Story.Log("people: " + file + ":" + n + ": strangersay: hugin|munin <слова> [/ …]"); continue; }
                            cur.StrangerSay.Add(new KeyValuePair<bool, string>(who == "munin", t.Substring(sp + 1).Trim()));
                        }
                        break;
                    default: Story.Log("people: " + file + ":" + n + ": невідоме поле «" + k + "»"); break;
                }
            }
        }

        // «<spot> [spawn <spot>] [bed <spot>] [when <world conditions>]»
        private static Home HomeOf(string v)
        {
            Home h = new Home();
            int when = v.IndexOf(" when ", StringComparison.Ordinal);
            if (when >= 0) { h.When = v.Substring(when + 6).Trim(); v = v.Substring(0, when); }
            string[] w = v.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            h.At = w.Length > 0 ? w[0] : "";
            for (int i = 1; i + 1 < w.Length; i += 2)
            {
                if (w[i] == "spawn") h.Spawn = w[i + 1];
                else if (w[i] == "bed") h.Bed = w[i + 1];
            }
            return h;
        }

        // ---------------------------------------------------------------- the server keeps them
        internal static void ServerTick()
        {
            if (Time.time < _next || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null || ZoneSystem.instance == null) return;
            bool waits = false;
            foreach (Person x in All)
            {
                try { Keep(x); }
                catch (Exception e) { Story.Log("people: " + x.Id + ": " + e.Message); }
                waits |= x.Why.Length > 0;
            }
            _next = Time.time + (waits ? Soon : Every); // (someone waits: one list of people and a few world keys, every Soon s)
        }

        // why it is not (yet) where it should be — said in the log once, when it changes
        private static void Why(Person x, string why)
        {
            if (why == x.Why) return;
            x.Why = why;
            if (why.Length > 0) Story.Log("people: " + x.Id + " waits — " + why);
        }

        private static void Keep(Person x)
        {
            ZDO npc = null;
            foreach (ZDO z in NpcCensus.People()) // (the shared census: NpcCensus)
            {
                if (!Of(z, x.Npc)) continue;
                if (npc == null) { npc = z; continue; }
                ZDO gone = z;
                if (!npc.GetBool(NpcResident.KResident, false) && z.GetBool(NpcResident.KResident, false)) { gone = npc; npc = z; } // (the one living there stays: a second one made by hand goes)
                Own(gone); NpcLocker.RemoveOf(gone); ZDOMan.instance.DestroyZDO(gone); // (never two of one; its locker with it)
                Story.Log("people: a second " + x.Id + " taken away");
            }
            if (x.Until.Length > 0 && StoryCond.Holds(null, x.Until, e => Story.Log("people: " + x.Id + ": until " + e), true)) // (its time is over: never kept again)
            {
                if (npc == null) { Leaving(x, false); Why(x, "gone for good: until " + x.Until); return; }
                if (!Parked(npc) && Watched(npc)) { Leaving(x, true); Why(x, "its time is over (until " + x.Until + ") — someone near: it goes the first moment nobody sees it"); return; } // (parked by the game that ran it: whoever owns it still, it is out of every sight)
                Own(npc); NpcLocker.RemoveOf(npc); ZDOMan.instance.DestroyZDO(npc);
                Leaving(x, false);
                Story.Log("people: " + x.Id + " taken away for good (until " + x.Until + ")");
                Why(x, "gone for good: until " + x.Until);
                return;
            }
            bool came = x.Came;
            if (npc == null && !came) { Leaving(x, false); Why(x, "not yet in the world: after " + x.After); return; } // (its time not come: the world's conditions — the server has no player)

            Home h = came ? x.Now : null;
            // «live: away when …» (the user 2026-11-15: Torvald, gone «to keep watch», stood by the road the player took to look for him): out of the
            // world for a while, whole — its ZDO, its locker, its memories kept — parked at AwayAt, where nobody ever comes; it goes when nobody sees it
            // (someone near: the game that runs it takes it out of their sight — LeaveTick), and its next home takes it back as a move from there (below:
            // never a walk from the world's edge). The same for one that came and whose «after:» no longer holds (Person.OutNow)
            if (x.OutNow(h))
            {
                string gone = came ? "away" : "out of the world: its «after: " + x.After + "» no longer holds";
                if (npc == null) { Leaving(x, false); Why(x, gone + ": not in the world now"); return; }
                if (Parked(npc)) { Leaving(x, false); Why(x, gone); return; }
                if (Watched(npc)) { Leaving(x, true); Why(x, gone + " — someone near: it goes the first moment nobody sees it"); return; }
                Own(npc);
                npc.SetPosition(AwayAt);
                npc.Set(NpcRules.KHome, AwayAt);
                Leaving(x, false);
                Story.Log("people: " + x.Id + " " + gone + " — out of the world till its next home");
                Why(x, gone);
                return;
            }
            Leaving(x, false); // (here to live: no word to go left behind — its next home came before it got out of sight)
            if (h == null || h.At.Length == 0) { Why(x, "no «live:» in " + x.File); return; }
            Vector3 home, spawn; ZDO dummy, bedZ = null; Loc loc, other;
            string why = StoryWhere.Standing(h.At, out home, out dummy, out loc);
            if (why != null) { Why(x, why); return; }
            if (h.Spawn.Length == 0 || StoryWhere.Standing(h.Spawn, out spawn, out dummy, out other) != null) spawn = home + Aside(loc, home);
            else { Vector3 to = home - spawn; to.y = 0f; spawn += to.sqrMagnitude > 1f ? to.normalized * 1.2f : Aside(other ?? loc, spawn); } // (a step off it, towards home: not inside it; home on the same piece — Torvald's bench, 2026-11-15: he was born standing on its seat — the step towards the place's middle, as with no «spawn»)
            Vector3 bedAt;
            if (h.Bed.Length > 0 && StoryWhere.Standing(h.Bed, out bedAt, out bedZ, out other) != null) bedZ = null;

            if (npc == null)
            {
                // it comes as its home does, before anyone is close; someone there (came, or was put there) — made out of their sight, it walks in;
                // nowhere out of sight — after a little while at its home all the same: never a home with nobody in it for good
                Vector3 born = spawn; bool walks = false;
                if (!Unseen0(spawn, Born))
                {
                    walks = NpcTravel.ComeFrom(spawn, out born);
                    if (!walks)
                    {
                        born = spawn;
                        if (x.BornWait < 0f) x.BornWait = Time.time;
                        if (Time.time - x.BornWait < BornWait) { Why(x, "someone within " + Born + " m of its home, nowhere out of sight to walk in from"); return; }
                    }
                }
                x.BornWait = -1f;
                NpcDef body = NpcDefs.Find(x.Npc);
                int hash = (body != null ? Npcs.PrefabOf(body) : Npcs.HumanPrefab).GetStableHashCode(); // (the body its template asks for)
                npc = ZDOMan.instance.CreateNewZDO(born + Vector3.up * 0.5f, hash);
                npc.Persistent = true;
                npc.SetPrefab(hash);
                npc.Set(Npcs.KId, x.Npc);
                NpcLocker.Put(npc, NpcLevers.KVars, NpcLevers.Protected + "=1");
                NpcKit.Mark(npc);
                Live(npc, home, bedZ);
                if (walks && StoryHold.Now(npc).Length == 0) NpcTravel.Give(npc, spawn, NpcTravel.ToArrive); // (its way in: the game that takes it over walks it there; a story's word already on it — Ketil's «hold heart.first» — walks it instead, and no way is left behind it)
                NpcCensus.Added(npc);
                Why(x, "");
                Story.Log("people: " + x.Id + " made at " + h.At + " (" + Mathf.RoundToInt(spawn.x) + "," + Mathf.RoundToInt(spawn.z) + ")" + (walks ? ", out of sight " + Mathf.RoundToInt(Utils.DistanceXZ(born, spawn)) + " m off: walks in" : "") + (h.Bed.Length > 0 ? (bedZ != null ? ", its bed found" : ", its bed not found") : ""));
                return;
            }

            bool homeSet = Utils.DistanceXZ(npc.GetVec3(NpcRules.KHome, npc.GetPosition()), home) < Moved; // (its home is that one already)
            // where it is to be: its home — or the place a word of the story holds it at (StoryHold), by the same rule: there or on its way, or
            // brought there once nobody sees it. Its home set but it not there yet (the user 2026-11-15: Lit sent to the Hall, the player gone the
            // other way — its body unloaded on the road, it stood there for ever and the stage waited); and held: a held one was never brought at
            // all (the review of 2026-11-15: Lit held at the Hall's gate, the player run ahead — the same stand on the meadow road)
            string word = StoryHold.Now(npc); // (the story's word, or its day's far line: StoryLife.Far)
            bool held = word.Length > 0;
            Vector3 goal = home, way = spawn; // (way: where it is put or walks in from — its home's way in, or a step off its word's thing)
            if (held)
            {
                Loc heldLoc;
                if (!StoryHold.PlaceNow(npc, out goal, out heldLoc)) { Why(x, "held: its word's place is not standing yet"); return; }
                way = goal + Aside(heldLoc, goal); // (not in the fire, not in the gate: its word walks it the last steps)
            }
            if ((homeSet || held) && StoryWhere.Apart(goal, npc.GetPosition()) < HomeNear) { if (!homeSet) Live(npc, home, bedZ); Why(x, ""); return; } // (held there: its new home written, it stays by its word)
            // someone near it or its new home, or its body loaded (its owner writes where it stands each frame: a move would be undone): not put
            // there — only its home changed, and it walks there itself (NpcResident: far from its base — home the long way). Before, it waited
            // for nobody to be near, and a player waiting at the new home kept it away for ever (the user 2026-11-15: Torvald roamed by his
            // hiding place, Heid stood by the fire, the brew never came)
            if ((npc.GetPosition() - AwayAt).sqrMagnitude < Moved * Moved) // (back from away: never a walk from the world's edge — out of sight of its home and in, or there at once)
            {
                Vector3 back = way;
                bool walks = !Unseen0(way, Born) && NpcTravel.ComeFrom(way, out back);
                if (!walks) back = way; // (ComeFrom found no spot out of sight: what it gave back is not a place)
                Put(npc, back); // (back with nothing kept from before it went: a walk off out of sight, cut by its going, is not walked again here)
                Live(npc, home, bedZ);
                if (walks && !held) NpcTravel.Give(npc, spawn, NpcTravel.ToArrive);
                Why(x, "");
                Story.Log("people: " + x.Id + " back from away to " + h.At + (walks ? ", out of sight: it walks in" : ""));
                return;
            }
            // it walks only where it is run: someone near IT (its zone loaded) or its body loaded on a game. Someone only at its new home walks
            // nothing — its body lies unloaded where it stood (the review of 2026-11-15: Heid called to the Hall's door from a far camp, the player
            // waiting in the Hall — she would never have come): then out of their sight of its home and in, as one born there
            bool nearIt = Run(npc.GetPosition()); // (not «within Unseen m»: 100–150 m off its body is not loaded, nobody walked it, and it was not moved either — it stood)
            bool loaded = NpcStray.Walked(npc); // (walked by a game — a body only made, in the ring no game owns, stands: NpcStray.Walked)
            if (nearIt || loaded)
            {
                if (homeSet) { Why(x, "on its way home"); return; } // (walking there: nothing new to write or say)
                Live(npc, home, bedZ);
                Why(x, "");
                Story.Log("people: " + x.Id + " lives at " + h.At + " now — " + (nearIt ? "someone near" : "a game runs its body") + ": it walks there itself");
                return;
            }
            Vector3 from;
            if (!Unseen0(way, Unseen) && NpcTravel.ComeFrom(way, out from))
            {
                Put(npc, from);
                Live(npc, home, bedZ);
                if (!held) NpcTravel.Give(npc, spawn, NpcTravel.ToArrive); // (its way in: the game that takes it over walks it there)
                Why(x, "");
                Story.Log("people: " + x.Id + " to " + (held ? "its word's place («" + word + "»)" : h.At) + " — someone there, nobody near it: out of their sight, it walks in");
                return;
            }
            Put(npc, way);
            Live(npc, home, bedZ);
            Why(x, "");
            Story.Log("people: " + x.Id + (held ? " put by its word's place («" + word + "») — nobody sees it" : homeSet ? " put at its home " + h.At + " — it had not got there, and nobody sees it" : " moved to " + h.At));
        }


        // the server puts it there (nobody runs it now): with nothing kept of its plans from before — a way it was on leads elsewhere now, and a
        // way kept would be walked again, or read by NpcStray as where it belongs (the review of 2026-11-16: Torvald put home after his watch,
        // his way to the watch kept — the strays' look took him back there in ten minutes, and this home again, round and round with nobody near)
        // A body made here but walked by nobody (the ring past every active area: NpcStray.Walked) is moved with it: owned here a moment, this
        // game's own sync would write where the body stands back into its ZDO
        private static void Put(ZDO npc, Vector3 at)
        {
            Own(npc);
            Vector3 p = at + Vector3.up * 0.5f;
            npc.SetPosition(p);
            GameObject body = NpcKit.Find(npc.m_uid);
            if (body != null)
            {
                body.transform.position = p;
                Rigidbody rb = body.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = p; rb.linearVelocity = Vector3.zero; }
            }
            NpcLocker.Put(npc, NpcMind.KPlans, "");
        }

        // a resident there (the server's write: nobody runs it now — nobody is near)
        private static void Live(ZDO z, Vector3 home, ZDO bed)
        {
            z.Set(NpcResident.KResident, true);
            z.Set(NpcRules.KHome, home);
            NpcLocker.Put(z, NpcResident.KSettledAt, (long)NpcRescue.Now);
            NpcResident.SetBed(z, bed != null ? bed.m_uid : ZDOID.None);
            NpcKit.SetRef(z, NpcResident.KHeart, ZDOID.None);
        }

        // a step off its home towards the middle of the place (not in a fire)
        private static Vector3 Aside(Loc l, Vector3 home)
        {
            Vector3 at; Quaternion rot;
            Vector3 d = l != null && l.Anchor(out at, out rot) ? at - home : Vector3.zero;
            d.y = 0f;
            return (d.sqrMagnitude > 1f ? d.normalized : Vector3.forward) * 2.5f;
        }

        // a game runs the land it stands on: some player's active area holds it (its body there, or coming) — it walks itself, nothing is moved
        private static bool Run(Vector3 p)
        {
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) if (c != null && ZNetScene.InActiveArea(p, c.GetPosition())) return true;
            return false;
        }

        private static bool Unseen0(Vector3 p, float far)
        {
            foreach (ZDO c in ZNet.instance.GetAllCharacterZDOS()) if (c != null && Utils.DistanceXZ(c.GetPosition(), p) < far) return false;
            return true;
        }

        // ---------------------------------------------------------------- going out of sight (away, or for good)
        // at AwayAt already (the server put it there, or the game that ran it did — LeaveTick)
        private static bool Parked(ZDO npc) { return (npc.GetPosition() - AwayAt).sqrMagnitude < Moved * Moved; }

        // the server cannot take it now: someone within Unseen m, or its body run by a game (that game writes where it stands: a move from here is undone)
        private static bool Watched(ZDO npc)
        {
            return !Unseen0(npc.GetPosition(), Unseen) || NpcStray.Walked(npc);
        }

        // the server's word to the game that runs it: go the first moment nobody sees you (the world's key: every game reads it, written only when it changes)
        private static void Leaving(Person x, bool on) { StoryWorld.SetText(LeaveKey(x.Npc), on ? "1" : ""); }

        // a scene's «npc <who> leave» (StoryScenesCast): this person goes out of the world the first moment nobody sees it — the shot counts as the eyes
        // (SeenNow); kept till then, the scene over too (session: a world left, a game closed — it stays where it is, its life brings it on)
        internal static readonly HashSet<string> SceneLeaves = new HashSet<string>();
        internal static void LeaveUnseen(NpcActor a) { if (a != null && a.Def != null && !a.Def.Generated) SceneLeaves.Add(a.Id); }

        // ---------------------------------------------------------------- its way of fighting by the story («fight: … when …»)
        internal static readonly string[] FightWays = { "off", "flee", "wary", "defend", "hunt" };
        internal static float FightEvery = 1f;
        // its way now by its lines ("" — none holds, or it has none: its template's). The world's conditions: the same on every game
        internal static string FightNow(Person x)
        {
            if (x.Fights.Count == 0) return "";
            if (Time.time < x.FightAt) return x.FightWay;
            x.FightAt = Time.time + FightEvery;
            x.FightWay = "";
            foreach (KeyValuePair<string, string> f in x.Fights)
                if (f.Value.Length == 0 || StoryCond.Holds(null, f.Value, e => Story.Log("people: " + x.Id + ": fight when " + e), true)) { x.FightWay = f.Key; break; }
            return x.FightWay;
        }
        private static Dictionary<string, Person> _byNpc; // (template → its person: made with the list)
        private static Person ByNpc(string npc) { if (_all == null) Load(); if (_byNpc == null) { _byNpc = new Dictionary<string, Person>(); foreach (Person x in _all) if (!_byNpc.ContainsKey(x.Npc)) _byNpc[x.Npc] = x; } Person p; return _byNpc.TryGetValue(npc ?? "", out p) ? p : null; }
        // no monster sees it (NpcVitals.StoryOwn): one of the story's people — while it does not fight by its «fight:» lines
        internal static bool Hidden(string npc)
        {
            if (!Wears(npc)) return false;
            Person x = ByNpc(npc);
            string way = x != null ? FightNow(x) : "";
            return way.Length == 0 || way == "off" || way == "flee";
        }
        // its story's way now, for NpcNature.Manner (null: no «fight:» line of its own — its template's; "": none holds now — its template's too)
        internal static string FightOf(NpcActor a) { Person x = a != null ? ByNpc(a.Id) : null; return x == null || x.Fights.Count == 0 ? null : FightNow(x); }
        // where it is run (NpcActor.Ticks), each FightEvery s, once its kept plans are read (Ready): its own way on its mind made what Manner says now
        // — compared with the mind itself, not with what was sent (the review of 2026-11-23: a word before its plans were read was lost to the kept one)
        private static readonly Dictionary<NpcActor, float> _fightLook = new Dictionary<NpcActor, float>(); // session: its next look (FightForget as it is unloaded)
        internal static void FightTick(NpcActor a)
        {
            if (a == null || a.Def == null || a.Def.Generated || !a.Owned || a.Mind == null) return;
            Person x = ByNpc(a.Id);
            if (x == null || x.Fights.Count == 0) return;
            float at;
            if (_fightLook.TryGetValue(a, out at) && Time.time < at) return;
            _fightLook[a] = Time.time + FightEvery;
            if (!a.Mind.Ready) return;
            string way = NpcNature.Manner(a);
            if (a.Mind.OwnWay == way) return;
            a.Mind.Do("fight " + way, NpcMind.Layer.Routine);
            Story.Log("people: " + x.Id + " fights now: " + way);
        }
        internal static void FightForget(NpcActor a) { _fightLook.Remove(a); }

        // the safeguard (the user 2026-11-23: «автоматично зникнути через якийсь час як запобіжник»): one who keeps it in sight — follows it, stands
        // and watches — does not keep it in the world for ever: LeaveCap s after its word to go it is gone all the same, in sight
        internal static float LeaveCap = 90f;
        private sealed class Going { public float Next, Since = -1f; public bool Walks; }
        private static readonly Dictionary<NpcActor, Going> Goers = new Dictionary<NpcActor, Going>(); // (session: the story's people run here — a handful; Forget as each is unloaded)
        internal static void Forget(NpcActor a) { Goers.Remove(a); }

        // where its body is run (NpcActor.Ticks: StoryModules — each frame), for the story's people only (Wears: a set's look; anyone else stops
        // there), each LeaveEvery s: the server's word «leave_<npc>» read; it stands — nobody sees it: out of the world at once (no puff: nobody
        // sees it); someone does — it walks off from them at its ease, and goes the moment they lose it
        internal static void LeaveTick(NpcActor a)
        {
            if (a.Def == null || a.Def.Generated || !Wears(a.Id)) return;
            Going g;
            if (!Goers.TryGetValue(a, out g)) Goers[a] = g = new Going();
            if (Time.time < g.Next) return;
            g.Next = Time.time + LeaveEvery;
            if (SceneLeaves.Contains(a.Id)) // a scene's «npc <who> leave»: gone the first moment it is out of the shot — the scene walks it, nothing else moves it
            {
                ZDO sz = a.Zdo;
                if (sz == null || a.Body == null || SeenNow(a)) return;
                SceneLeaves.Remove(a.Id);
                a.Body.Put(AwayAt, "сцена: іде зі світу поза кадром", false);
                sz.SetPosition(AwayAt);
                sz.Set(NpcRules.KHome, AwayAt);
                Story.Log("people: " + a.Id + " out of the world in a scene — out of the shot"); // (its next home brings it back out of sight, as any away)
                return;
            }
            if (StoryWorld.Text(LeaveKey(a.Id)).Length == 0 && !AwayNow(a.Id)) // (the server's word, or its own «live: away» holding now — read here from the same world: the server looks every Every s)
            {
                g.Since = -1f;
                if (g.Walks) { g.Walks = false; a.Order("free", NpcMind.Layer.Order, LeaveWhose); } // (its next home came first: its walk off ends here)
                return;
            }
            NpcMind m = a.Mind;
            ZDO z = a.Zdo;
            if (m == null || z == null || a.Body == null || !Of(z, a.Id) || m.Of(NpcMind.Layer.Scene) != null || m.InTalk) { g.Since = -1f; return; } // (a scene's copy is the scene's; a scene or a talk has it now: it waits — and the safeguard's count with it)
            if (g.Since < 0f) g.Since = Time.time;
            bool capped = Time.time - g.Since >= LeaveCap;
            if (!SeenNow(a) || capped)
            {
                if (g.Walks) { g.Walks = false; a.Order("free", NpcMind.Layer.Order, LeaveWhose); } // (its walk off is not kept for its coming back)
                a.Body.Put(AwayAt, capped ? "сюжет: іде зі світу — запобіжник" : "сюжет: іде зі світу поза очима", false);
                z.SetPosition(AwayAt);
                z.Set(NpcRules.KHome, AwayAt);
                g.Since = -1f;
                Story.Log("people: " + a.Id + " out of the world — " + (capped ? "still seen after " + LeaveCap + " s: gone all the same (the safeguard)" : "nobody saw it go"));
                return;
            }
            if (StoryHold.Now(z).Length > 0) return; // (a story's word leads it: it goes from wherever that has it, unseen)
            NpcMind.Plan held = m.Of(NpcMind.Layer.Order);
            if (held != null) return; // (on its way off — or another's order leads it: never taken from it, it goes from wherever that has it, unseen)
            Vector3 off;
            if (!OffSight(a.transform.position, out off)) return;
            a.Order("go " + off.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + off.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + off.z.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ease", NpcMind.Layer.Order, LeaveWhose);
            g.Walks = true;
            Story.Log("people: " + a.Id + " to go — someone sees it: walks off " + Mathf.RoundToInt(Utils.DistanceXZ(a.transform.position, off)) + " m"); // flat: a log's number
        }

        // its «live:» says away now (the world's conditions: the same on every game) — the game that runs it need not wait for the server's word
        // (the user 2026-11-18: Torvald must be gone while the scene's screen is black, and the server looks at its people only every Every s)
        private static bool AwayNow(string npc)
        {
            foreach (Person x in All) if (x.Npc == npc) return x.OutNow(x.Came ? x.Now : null);
            return false;
        }

        // someone sees it now: a player this game has loaded within SeenNear m; or within SeenFar m with a clear line to its head or chest — this game's
        // own player only with it on the screen (a remote player's eyes are not known here: a clear line is enough). Two lines a near player: light.
        // A scene's shot on this game's screen (StoryCamera.Active) is his eyes then — and the eyes of those watching it with him (within StoryWatch.Radius:
        // they see that very shot): near is not «seen», only in the shot is (the user 2026-11-20: Eyvind left the bench in heid_tells and stood in the
        // shot «лава порожня» — «мав зникнути в сцені, і щоб ми цього не бачили»)
        internal static float BlackHides = 0.95f; // (how black a scene's screen hides all from this game's own player: StoryScreen.Black, 0…1)
        private static bool SeenNow(NpcActor a)
        {
            Vector3 head = a.transform.position + Vector3.up * 1.6f, mid = a.transform.position + Vector3.up * 1f;
            Camera cam = Utils.GetMainCamera();
            bool black = StoryScreen.Black >= BlackHides, shot = StoryCamera.Active && cam != null;
            Player me = Player.m_localPlayer;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || (black && p == me)) continue; // (a scene's black: he sees nothing, near or far)
                bool byShot = shot && (p == me || (me != null && Vector3.Distance(p.transform.position, me.transform.position) <= StoryWatch.Radius)); // (in the scene's circle: its shot is what they see)
                float d = Vector3.Distance(p.transform.position, mid);
                if (d < SeenNear && !byShot) return true;
                if (d > SeenFar) continue;
                Vector3 eye = p.GetEyePoint();
                if ((p == me || byShot) && cam != null)
                {
                    Vector3 v = cam.WorldToViewportPoint(mid);
                    if (v.z <= 0f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) continue; // (not on its screen)
                    eye = cam.transform.position;
                }
                if (!Physics.Linecast(eye, head, RavenMotor.Mask, QueryTriggerInteraction.Ignore) || !Physics.Linecast(eye, mid, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // where it walks off to: OffWalk m from it on land (12 sides), the farthest from every player this game has loaded. False: none on land
        private static bool OffSight(Vector3 from, out Vector3 to)
        {
            to = from;
            if (WorldGenerator.instance == null || ZoneSystem.instance == null) return false;
            float best = -1f;
            for (int i = 0; i < 12; i++)
            {
                float r = i * Mathf.PI / 6f;
                Vector3 p = from + new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r)) * OffWalk;
                if (WorldGenerator.instance.GetHeight(p.x, p.z) < ZoneSystem.instance.m_waterLevel + 0.3f) continue; // (not into the water)
                float least = float.MaxValue;
                foreach (Player pl in Player.GetAllPlayers()) if (pl != null) least = Mathf.Min(least, StoryWhere.Apart(p, pl.transform.position)); // (one in our crypt overhead sees nothing here)
                if (least > best) { best = least; to = p; }
            }
            if (best < 0f) return false;
            float h;
            to.y = ZoneSystem.instance.GetGroundHeight(to, out h) ? h : WorldGenerator.instance.GetHeight(to.x, to.z);
            return true;
        }

        // the author's look: `raven story people [id]` (and `raven story heid`) — where each is, where it lives, what it waits for
        internal static string Describe(Player p, string only)
        {
            if (ZDOMan.instance == null) return "Нема світу.";
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Люди сюжету (" + All.Count + "):");
            foreach (Person x in All)
            {
                if (only.Length > 0 && x.Id != only) continue;
                Home h = x.Now;
                sb.Append("\n").Append(x.Id).Append(" (").Append(x.File).Append("): ").Append(!x.Came ? "поза світом (after: " + x.After + " не справджується)" : "живе " + (h != null ? h.At + (h.When.Length > 0 ? " (бо " + h.When + ")" : "") : "—"));
                int n = 0;
                foreach (ZDO z in NpcCensus.People())
                {
                    if (!Of(z, x.Npc)) continue;
                    n++;
                    Vector3 at = z.GetPosition(), home = z.GetVec3(NpcRules.KHome, at);
                    sb.Append("\n  стоїть ").Append(Mathf.RoundToInt(at.x)).Append(",").Append(Mathf.RoundToInt(at.y)).Append(",").Append(Mathf.RoundToInt(at.z))
                      .Append(" — ").Append(Mathf.RoundToInt(Vector3.Distance(at, p.transform.position))).Append(" м від тебе; дім ")
                      .Append(Mathf.RoundToInt(home.x)).Append(",").Append(Mathf.RoundToInt(home.z)).Append(" (").Append(Mathf.RoundToInt(Utils.DistanceXZ(at, home))).Append(" м від нього)")
                      .Append("; тіло ").Append(ZNetScene.instance != null && ZNetScene.instance.FindInstance(z.m_uid) != null ? "завантажене тут" : "не завантажене тут")
                      .Append("; мешканець ").Append(z.GetBool(NpcResident.KResident, false) ? "так" : "ні");
                }
                if (n == 0) sb.Append("\n  у світі нема").Append(ZNet.instance != null && !ZNet.instance.IsServer() ? " (ти не сервер — видно лише завантажене)" : "");
                sb.Append("\n  чекає: ").Append(x.Why.Length > 0 ? x.Why : "нічого");
            }
            foreach (Loc l in Locs.All) if (l.Follow != null && l.Built) sb.Append("\n").Append(l.Title).Append(": ").Append(l.WordOf(LocState.StateOf(l))).Append(LocState.ShutOf(l) ? " (зміна чекає за кадром)" : "");
            return sb.ToString();
        }

        private static void Own(ZDO z) { if (z.GetOwner() != ZDOMan.GetSessionID()) z.SetOwner(ZDOMan.GetSessionID()); }
    }
}
#endif

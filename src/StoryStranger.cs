#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person of the story not met yet (the user 2026-11-16, chapter 2: Eyvind sits in the Hall's corner from the start of the chest's first round —
    // «наведення — „Чужинець“, E нічого; ворони: Гугін „Хто це?“, Мунін „Мабуть, Літів“»; Baldr, «Незнайомець» till his name is known, is the
    // same kind). Its people file (StoryPeople):
    //   stranger: <world conditions> [:: <name>]   while they hold: its name over it is that one («Чужинець» if none), the hover says nothing
    //                                              of a talk or a ring, E does nothing, Q opens nothing, and it says nothing of its own (it
    //                                              sits there unasked: «Ніхто не питав, я й не набивався»)
    //   strangersay: hugin <words> / munin <words> … what your ravens say the first time you come within Near m of it while it is so: in
    //                                              that order, each one only if that raven is on your shoulder, awake and yours — and the
    //                                              row stops at the first that is not (an answer is never said to a question nobody asked).
    //                                              Once a character (its flag «stranger_<who>»); none of the two there — said later, when one is
    //   notalk: <world conditions>                 while they hold: no talk with it — E and Q open nothing, the hover is its name only; its own
    //                                              words, a scene's, a road talk's (StoryRoad) go on as ever (the user 2026-11-20: Torvald on his
    //                                              watch at the tower is not to be talked to)
    // The world's conditions: every game reads the same, the server keeps nothing of it. A scene that takes it says its lines (its quiet is only
    // for words of its own: StoryAside.InScene), under the name it is shown by — a scene where it gives its name sets the fact there («do world …»).
    // E on it that a stage waits for («on: npc_use <who>») is the stage's, as on anyone (StoryModules). Cost: its hover, E, Q and its own words ask one map lookup and a kept answer (its conditions asked once Every s);
    // twice a second (StoryQuests.Each) one pass over the people with «strangersay» not said yet, and over the live people only then.
    internal static class StoryStranger
    {
        internal static float Near = 12f, Every = 0.25f, Gap = 0.6f; // (Near: one who walks in by the gate passes him on his bench — at 7 m one going straight to Lit by the chest never came so near · Every: his name over the line right after the scene's «do world …» — at 1 s the next line still showed «Чужинець»; a few world keys a look)
        private const string Flag = "stranger_";
        private static readonly string DefaultName = Lang.K("Чужинець");

        private static List<StoryPeople.Person> _of; // (the list the map was made from: StoryPeople reads anew → a new map)
        private static readonly Dictionary<string, StoryPeople.Person> _byNpc = new Dictionary<string, StoryPeople.Person>(); // template → its person, only those with «stranger:»
        private static readonly Dictionary<string, float> _askedAt = new Dictionary<string, float>(); // session: a person → when its conditions were last asked
        private static readonly Dictionary<string, bool> _is = new Dictionary<string, bool>();       // session: … and what they said

        // ravens' row now being said: whose, the next line's index, when
        private static StoryPeople.Person _row; private static int _next; private static float _nextAt;

        internal static void Wire()
        {
            NpcActor.NameInstead.Add(a => { StoryPeople.Person x = Of(a); return x == null ? null : Lang.T(x.StrangerName.Length > 0 ? x.StrangerName : DefaultName); });
            NpcActor.HoverInstead.Add(a => { StoryPeople.Person x = Of(a); return x == null ? null : a.GetHoverName(); }); // (only its name: no «Говорити», no «Дії»)
            NpcActor.UseInstead.Add((a, p) => Of(a) != null && (p == null || !StoryQuests.Awaits(p, "npc_use", a.Def.Id)) ? (bool?)false : null); // (E: nothing — unless a stage waits for it)
            NpcMenu.KeyInstead.Add((a, p) => Of(a) != null ? (bool?)true : null);  // (Q: no ring)
            NpcActor.HoverInstead.Add(a => NoTalkOf(a) != null ? a.GetHoverName() : null); // («notalk:» — its name, nothing to press)
            NpcActor.UseInstead.Add((a, p) => NoTalkOf(a) != null && (p == null || !StoryQuests.Awaits(p, "npc_use", a.Def.Id)) ? (bool?)false : null);
            NpcMenu.KeyInstead.Add((a, p) => NoTalkOf(a) != null ? (bool?)true : null);
            StoryQuests.Each.Add("strangers", Look);
        }

        // the person of the story this one is, while its «notalk:» holds (a scene's own copy of it is not)
        internal static StoryPeople.Person NoTalkOf(NpcActor a)
        {
            if (a == null || a.Def == null) return null;
            Map();
            StoryPeople.Person x;
            if (!_noTalk.TryGetValue(a.Def.Id, out x) || a.Tag == StoryScenes.MadeTag) return null;
            float at; bool was;
            if (_ntAt.TryGetValue(x.Id, out at) && Time.time - at < Every && _nt.TryGetValue(x.Id, out was)) return was ? x : null;
            bool now = StoryCond.Holds(null, x.NoTalk, e => Story.Log("people: " + x.Id + ": notalk " + e), true);
            _ntAt[x.Id] = Time.time; _nt[x.Id] = now;
            return now ? x : null;
        }
        private static readonly Dictionary<string, StoryPeople.Person> _noTalk = new Dictionary<string, StoryPeople.Person>(); // template → its person, only those with «notalk:»
        private static readonly Dictionary<string, float> _ntAt = new Dictionary<string, float>(); // session: when its conditions were last asked
        private static readonly Dictionary<string, bool> _nt = new Dictionary<string, bool>();      // session: … and what they said

        // the person of the story this one is, while it is still a stranger (a scene's own copy of it is not: StoryPeople.Of)
        internal static StoryPeople.Person Of(NpcActor a)
        {
            if (a == null || a.Def == null) return null;
            Map();
            StoryPeople.Person x;
            if (!_byNpc.TryGetValue(a.Def.Id, out x) || a.Tag == StoryScenes.MadeTag) return null;
            return Is(x) ? x : null;
        }

        // no word of its own while a stranger (NpcActor.Hushed, with StoryAside's); a scene's lines are the scene's
        internal static bool Hushed(NpcActor a) { return Of(a) != null && !StoryAside.InScene(a); }

        private static void Map()
        {
            List<StoryPeople.Person> all = StoryPeople.All;
            if (ReferenceEquals(all, _of)) return;
            _of = all; _byNpc.Clear(); _askedAt.Clear(); _is.Clear(); _row = null; _noTalk.Clear(); _ntAt.Clear(); _nt.Clear();
            foreach (StoryPeople.Person x in all)
            {
                if (x.Stranger.Length > 0 && !_byNpc.ContainsKey(x.Npc)) _byNpc[x.Npc] = x;
                if (x.NoTalk.Length > 0 && !_noTalk.ContainsKey(x.Npc)) _noTalk[x.Npc] = x;
            }
        }

        private static bool Is(StoryPeople.Person x)
        {
            float at; bool was;
            if (_askedAt.TryGetValue(x.Id, out at) && Time.time - at < Every && _is.TryGetValue(x.Id, out was)) return was;
            bool now = StoryCond.Holds(null, x.Stranger, e => Story.Log("people: " + x.Id + ": stranger " + e), true);
            _askedAt[x.Id] = Time.time; _is[x.Id] = now;
            return now;
        }

        // twice a second: the ravens' row going on, or one to begin by a stranger near
        private static void Look(Player p)
        {
            if (p == null || p.IsDead()) return;
            Map();
            if (_row != null) { Go(p); return; }
            if (_byNpc.Count == 0 || StoryScenes.Playing.Length > 0 || RavenSkills.Hushed(p)) return;
            foreach (StoryPeople.Person x in _byNpc.Values)
            {
                if (x.StrangerSay.Count == 0 || Story.Flag(p, Flag + x.Id) || !Is(x)) continue;
                if (!LocSitSay.Ready(p, x.StrangerSay[0].Key)) continue; // (the first one's raven not there: later)
                if (!NearOne(p, x.Npc)) continue;
                Story.SetFlag(p, Flag + x.Id, true);
                _row = x; _next = 0; _nextAt = 0f;
                Go(p);
                return;
            }
        }

        private static bool NearOne(Player p, string npc)
        {
            foreach (NpcActor a in NpcActor.Live)
                if (a != null && a.Def != null && a.Def.Id == npc && a.Tag != StoryScenes.MadeTag && StoryWhere.Apart(a.transform.position, p.transform.position) <= Near) return true;
            return false;
        }

        // the row's next line, when its time has come; a scene begun, the raven gone: the row ends there
        private static void Go(Player p)
        {
            if (Time.time < _nextAt) return;
            if (_next >= _row.StrangerSay.Count || StoryScenes.Playing.Length > 0) { _row = null; return; }
            KeyValuePair<bool, string> line = _row.StrangerSay[_next];
            if (!LocSitSay.Ready(p, line.Key)) { _row = null; return; }
            string said = Lang.T(line.Value);
            RavenSkills.SayFromShoulder(p, line.Key, said);
            _next++;
            _nextAt = Time.time + RavenSkills.SaySeconds(said) + Gap;
        }
    }
}
#endif

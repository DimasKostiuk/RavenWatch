#if RAVEN_ITEM && RAVEN_NPC && RAVEN_STORY
using System.Collections.Generic;

namespace HuginWhispers
{
    // Whether a place of a story person's talk can still be heard (the user 2026-11-16: each player has his own clues, and what he did not ask in its
    // time he loses — the book says who may tell more, and of what is lost, that nobody can be asked now). Asked by the book's «Сага» for the lines of
    // its open entry that wait for «asked <talk> <place>» (StorySaga.MoreOf); the talk itself is not touched — the same answers and conditions, read
    // as time instead of «holds now»:
    //   Now      some way of answers leads there from the first screen, and none of them is out of its time
    //   Later    not yet (its chapter, its stage, a fact of the world still to come; the person not in this world yet)
    //   Gone     never again: every way there is past its time (a stage passed, «!reached» reached, a lasting fact come, its chapter over), or the
    //            person has gone from the world for good (its «until:»)
    //   Settled  heard already — or another way of the same row was («>|»: read «I have the horn», «what horn?» is not owed)
    // A condition's time (TimeOf): what the story moves one way only is judged — «chnow», «chfrom», «step», «reached», «quest», a lasting fact
    // («world», «knows», «lived», «flag», «chose», «been», «read», «boss», «key») and «asked» (the place it names: can it still be heard). Everything of
    // the moment — where the person stands, what you carry, the hour, its patience — counts as «now»: the book says who may tell more, not that it
    // is at hand this minute. A settler's talk (its places open by what weighs on it) is never judged: Later.
    // A gathering by the fire asks it too (Untold: a true tale not heard yet and still in its time — StoryMeets).
    // Cost: nothing kept and no pass of its own. One asking walks a talk's answers once (a story talk: a few dozen), a condition a flag or a key
    // looked up; the book asks only for the entry open on its right page, as it is drawn.
    internal static partial class NpcTalk
    {
        internal enum Ask { Gone, Later, Now, Settled }

        // a place of that talk starting so (Torvald's «story_») that this player may be told now and has not heard: one of them at random — a true tale
        // the person tells by the fire (StoryMeets «{told …}», the condition «untold»: the user 2026-11-16 — heard there, it is no longer to be lost);
        // null: none. Asked when a gathering's talk is picked with a guest seated: the talk's places that start so, an Askable each
        internal static string Untold(Player p, string talk, string prefix)
        {
            Talk t = p != null ? Find(talk) : null;
            if (t == null) return null;
            List<string> fresh = null;
            foreach (string place in t.Nodes.Keys)
                if (place.StartsWith(prefix, System.StringComparison.Ordinal) && t.Nodes[place].Says.Length > 0 && Askable(p, talk, place) == Ask.Now) (fresh ?? (fresh = new List<string>())).Add(place);
            return fresh != null ? fresh[UnityEngine.Random.Range(0, fresh.Count)] : null;
        }

        // place «»: that person at all (the condition «asked <talk>»)
        internal static Ask Askable(Player p, string talk, string place)
        {
            Talk t = Find(talk);
            if (p == null || t == null) return Ask.Later;
            if (place.Length == 0 ? HeardAny(p, talk) : Heard(p, talk, place) || RowHeard(p, t, place)) return Ask.Settled;
            if (StoryPeople.Left(talk)) return Ask.Gone;
            if (!StoryPeople.Came(talk) || t.Nodes.ContainsKey("c_any")) return Ask.Later;
            return place.Length == 0 ? Ask.Now : Reach(p, t, place, new Dictionary<string, Ask>());
        }

        // an answer above this place's own in its row of ways («>|») has its place read: that row has had its answer and this way is never offered again —
        // as the talk itself hides it (AboveHolds: only the answers above a way, «I have the horn» over «what horn?»; one below hides nothing). Only when
        // every answer that leads to this place is so
        private static bool RowHeard(Player p, Talk t, string place)
        {
            bool any = false;
            foreach (Node n in t.Nodes.Values)
                foreach (Answer a in n.Answers)
                {
                    if (!LeadsTo(a, place)) continue;
                    any = true;
                    bool shut = false;
                    for (Answer x = a.Else; x != null && !shut; x = x.Else)
                        shut = x.Pool == null && x.To != "end" && x.To != place && (Story.Flag(p, ReadKey(t.Id, x.To)) || Story.Flag(p, HereKey(t.Id, x.To)));
                    if (!shut) return false;
                }
            return any;
        }

        // the best way to a place from the first screen: each answer that leads there, no better than the place it stands in. seen: this asking's own
        // (a ring of places is no way in by itself: «ask» → «heart_what» → «ask»)
        private static Ask Reach(Player p, Talk t, string place, Dictionary<string, Ask> seen)
        {
            if (place == "start") return Ask.Now;
            Ask best;
            if (seen.TryGetValue(place, out best)) return best;
            seen[place] = best = Ask.Gone;
            foreach (KeyValuePair<string, Node> n in t.Nodes)
                foreach (Answer a in n.Value.Answers)
                {
                    if (!LeadsTo(a, place)) continue;
                    Ask way = Ask.Now;
                    foreach (Cond c in a.When) { Ask one = TimeOf(p, c); if (one < way) way = one; }
                    if (way <= best) continue;
                    Ask from = Reach(p, t, n.Key, seen);
                    if (from < way) way = from;
                    if (way > best) best = way;
                    if (best == Ask.Now) return seen[place] = best;
                }
            return seen[place] = best;
        }

        // a condition's time (the header). Never Settled
        private static int _asking; // («asked» inside an answer's conditions asks another place: not round in a ring)
        internal static Ask TimeOf(Player p, Cond c)
        {
            if (p == null || c == null || c.Kind == null || c.A.Length == 0) return Ask.Now;
            switch (c.A[0])
            {
                case "chnow": { int n = Errands.I(c.A[1], 0), now = StoryQuests.ChapterNow(p); return now < n ? Ask.Later : now == n ? Ask.Now : Ask.Gone; }
                case "chfrom": return StoryQuests.ChapterNow(p) >= Errands.I(c.A[1], 0) ? Ask.Now : Ask.Later;
                case "step":
                    if (c.Not || c.A.Length < 3) return Ask.Now; // («!step»: of the moment)
                    return StoryQuests.IsAt(p, c.A[1], c.A[2]) ? Ask.Now : StoryQuests.Reached(p, c.A[1], c.A[2]) ? Ask.Gone : Ask.Later;
                case "quest":
                    if (c.A.Length > 2 && c.A[2] == Story.Active)
                    {
                        if (c.Not) return Ask.Now;
                        string st = StoryQuests.StateOf(p, c.A[1]);
                        return st == Story.Active ? Ask.Now : st.Length == 0 ? Ask.Later : Ask.Gone;
                    }
                    break;
                case "asked":
                    if (c.Not || c.Test(p)) break;
                    if (_asking > 3) return Ask.Later;
                    _asking++;
                    try { Ask a = Askable(p, c.A[1], c.A.Length > 2 ? c.A[2] : ""); return a == Ask.Settled ? Ask.Gone : a; } // (Settled while not heard: another way of its row was — this one never comes)
                    finally { _asking--; }
                case "chose": // (a choice is made once: another way chosen — this one never; Choose takes the others' flags away)
                    if (c.A.Length > 2 && !c.Test(p) && !c.Not) { string had = StoryQuests.Chosen(p, c.A[1]); if (had.Length > 0 && had != c.A[2]) return Ask.Gone; }
                    break;
                case "reached": case "world": case "knows": case "lived": case "flag": case "been": case "read": case "boss": case "key": break;
                default: return Ask.Now; // (of the moment)
            }
            bool yes = c.Test(p) != c.Not; // (the fact itself, whichever way the condition asks it)
            return c.Not ? (yes ? Ask.Gone : Ask.Now) : (yes ? Ask.Now : Ask.Later);
        }
    }
}
#endif

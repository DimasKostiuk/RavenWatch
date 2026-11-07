#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;

namespace HuginWhispers
{
    // The saga: the story's progress as the book's «Сага» shows it (the user 2026-11-05). One place that puts it together;
    // the book only draws what Pages gives. Two halves, from the owners that already keep them:
    //   the world's — StoryTales: a tale with «saga:» that has happened; known (seen, told by a person, «known:») — its entry and
    //                 the names of the players who saw it; not known yet — its «rumor:» (a person telling it lifts it);
    //   one's own   — StoryQuests: a stage (or way) of a personal quest with «saga:», passed, kept in the character in the order lived (Story.Saga),
    //                 so what came before the book (it is given in Eirik's house, a few stages in) is written in it already. A quest of the server
    //                 keeps nothing in the character: what happened on this server is the world's half, seen or told by a person here.
    // Chapters: assets/story/saga/*.txt (StoryFiles), in the files' order:
    //   [chapter prologue]
    //   name: Пролог
    //   done: world heart_first           conditions (StoryCond): the chapter closed for this player
    // A quest and a tale name theirs («chapter:»); a chapter is shown once it has an entry (none at all: the first one).
    // Not shown: what has not happened yet, the chapters ahead (spoilers).
    // Cost: nothing while the book is shut; while it is open, Stamp and Fresh once a second (the book's refresh): the tales'
    // world keys and conditions, the character's list — a few dozen strings. Pages (with the witnesses: one pass over the
    // world's keys) only when the page is drawn.
    internal static class StorySaga
    {
        internal const string Kind = "saga";

        internal sealed class Chapter { public string Id = "", Name = ""; public List<Cond> Done; }

        internal sealed class Page
        {
            public Chapter Chapter;
            public bool Closed;                                                                  // its «done:» holds
            public readonly List<StoryTales.Told> World = new List<StoryTales.Told>();          // happened, by moment
            public int Happened, Of;                                                             // the world's moments of it: happened · all
            public readonly List<string> Own = new List<string>();                              // one's own entries (untranslated), in the order lived
        }

        private static List<Chapter> _all;
        internal static readonly List<string> Errors = new List<string>();

        private static List<Chapter> All { get { if (_all == null) Load(); return _all; } }
        internal static void Reload() { _all = null; }
        internal static bool HasChapter(string id) { return Find(id) != null; }
        private static Chapter Find(string id) { foreach (Chapter c in All) if (c.Id == id) return c; return null; }

        private static void Load()
        {
            StoryTrackConds.Register(); // (its «done:» is conditions)
            _all = new List<Chapter>(); Errors.Clear();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false))
            {
                Chapter c = null;
                foreach (KeyValuePair<int, string> l in StoryFiles.Lines(f.Value))
                {
                    string line = l.Value, at = f.Key + ":" + l.Key + ": ";
                    if (line.StartsWith("[chapter ") && line.EndsWith("]"))
                    {
                        string id = line.Substring(9, line.Length - 10).Trim();
                        if (!Story.GoodName(id) || Find(id) != null) { Errors.Add(at + "погана або вже вжита назва розділу «" + id + "»"); c = null; continue; }
                        c = new Chapter { Id = id }; _all.Add(c);
                        continue;
                    }
                    if (c == null) { Errors.Add(at + "рядок поза розділом"); continue; }
                    int k = line.IndexOf(':');
                    if (k <= 0) { Errors.Add(at + "рядок без «:»"); continue; }
                    string key = line.Substring(0, k).Trim(), val = line.Substring(k + 1).Trim();
                    switch (key)
                    {
                        case "name": c.Name = val; break;
                        case "done": c.Done = StoryCond.Parse(val, Errors); break;
                        default: Errors.Add(at + "невідоме «" + key + "»"); break;
                    }
                }
            }
            foreach (Chapter c in _all) if (c.Name.Length == 0) Errors.Add(c.Id + ": без name:");
            foreach (string e in Errors) Story.Log("saga: " + e);
            Story.Log("saga: " + _all.Count + " chapters" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
        }

        // ---------------------------------------------------------------- the pages
        // the chapters this player has something in (none: the first), each with its entries
        internal static List<Page> Pages(Player p)
        {
            List<Page> pages = new List<Page>();
            if (p == null || All.Count == 0) return pages;
            Dictionary<string, Page> by = new Dictionary<string, Page>();
            foreach (Chapter c in All) by[c.Id] = new Page { Chapter = c, Closed = c.Done != null && c.Done.Count > 0 && StoryCond.All(p, c.Done) };
            foreach (StoryTales.Told t in StoryTales.Saga(p, true))
            {
                Page pg;
                if (!by.TryGetValue(t.Chapter, out pg)) continue;
                pg.Of++;
                if (t.Moment == 0) continue;
                pg.Happened++;
                pg.World.Add(t);
            }
            foreach (string e in Story.Of(p).Saga)
            {
                StoryQuests.QuestDef q;
                StoryQuests.StageDef s = StoryQuests.Entry(e, out q);
                Page pg;
                if (s != null && s.Saga.Length > 0 && by.TryGetValue(q.Chapter, out pg)) pg.Own.Add(s.Saga);
            }
            foreach (Chapter c in All)
            {
                Page pg = by[c.Id];
                pg.World.Sort((a, b) => a.Moment.CompareTo(b.Moment));
                if (pg.World.Count > 0 || pg.Own.Count > 0) pages.Add(pg);
            }
            if (pages.Count == 0) pages.Add(by[All[0].Id]);
            return pages;
        }

        // the console's «raven story saga»: the pages as words (what the book would show now)
        internal static string Describe(Player p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Сага " + (p != null ? p.GetPlayerName() : "") + " (нового: " + (Fresh(p) ? "так" : "ні") + "):");
            foreach (string e in Errors) sb.Append("\n  ! ").Append(e);
            foreach (Page pg in Pages(p))
            {
                sb.Append("\n[").Append(pg.Chapter.Id).Append("] ").Append(pg.Chapter.Name).Append(" — світ ").Append(pg.Happened).Append(" з ").Append(pg.Of).Append(pg.Closed ? ", закінчено" : "");
                foreach (StoryTales.Told t in pg.World)
                    sb.Append("\n  ").Append(t.Known ? t.Saga : "(чутка) " + (t.Rumor.Length > 0 ? t.Rumor : "—")).Append(t.Known && (t.You || t.Witnesses.Count > 0) ? "  · свідки: " + (t.You ? "ти " : "") + string.Join(", ", t.Witnesses.ToArray()) : "");
                foreach (string e in pg.Own) sb.Append("\n  я: ").Append(e);
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- new on the page (the tab's gold dot)
        private static string SeenVar { get { return "saga_seen_" + (ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L); } } // (a server's part of it differs: what is new is counted per world)

        // how much there is to read: one's own entries, a rumour 1, a known world entry 2 (a rumour lifted is new again)
        private static int Weight(Player p)
        {
            int n = 0;
            foreach (string e in Story.Of(p).Saga) { StoryQuests.QuestDef q; StoryQuests.StageDef s = StoryQuests.Entry(e, out q); if (s != null && s.Saga.Length > 0) n++; }
            foreach (StoryTales.Told t in StoryTales.Saga(p, false)) if (t.Moment != 0) n += t.Known ? 2 : 1;
            return n;
        }

        internal static bool Fresh(Player p) { return p != null && Weight(p) > Story.Var(p, SeenVar); }

        // the page seen: what is on it is no longer new
        internal static void Seen(Player p) { if (p != null) Story.SetVar(p, SeenVar, Weight(p), true); }

        // what the page is drawn from, without the witnesses' names (the book draws it again when this changes)
        internal static string Stamp(Player p)
        {
            if (p == null) return "";
            string s = Story.Of(p).Saga.Count + ":"; // (the played minutes must not move it every 30 s)
            foreach (StoryTales.Told t in StoryTales.Saga(p, false)) s += t.Moment + (t.Known ? "k" : "") + ",";
            StoryQuests.QuestDef q; StoryQuests.StageDef st; UnityEngine.Vector3 at;
            if (StoryQuests.Now(p, out q, out st)) s += "|" + q.Id + "." + st.Id + (StoryQuests.MarkAt(q, st, out at) ? "m" : "");
            return s + "|" + StoryWorld.Texts("tale_").Count; // (a witness more)
        }
    }
}
#endif

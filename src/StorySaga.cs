#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Text;

namespace HuginWhispers
{
    // The saga: the story's progress as the book's «Сага» shows it (the user 2026-11-05). One place that puts it together;
    // the book only draws what Pages and Known give. All of it is data: assets/story/saga/*.txt (StoryFiles), read with the quests.
    //
    // The chronicle — two halves, from the owners that already keep them:
    //   the world's — StoryTales: a tale with «saga:» that has happened; known (seen, told by a person, «known:») — its entry and
    //                 the names of the players who saw it; not known yet — its «rumor:» (a person telling it lifts it);
    //   one's own   — StoryQuests: a stage (or way) with «saga:», passed, kept in the character in the order lived (Story.Saga), so what came
    //                 before the book (it is given in Eirik's house, a few stages in) is written in it already. A quest of the server's: only to those
    //                 who were there when it passed (StoryQuestsServer.cs, Witnessed); what happened on this server for all is the world's half.
    // The story is three acts, chapters in them (the user 2026-11-17: act 1 — chapters 1 and 2 in 3.0, its chapter 3 later); the prologue stands
    // before them, alone. A chapter is the story's block of files (StoryFiles.Block: ch1, ch2 … through the whole story); an act is only its group:
    //   [act 1]
    //   label: Акт перший                  the small word over the name, on the screen and the page; the book's upper row names the act by it
    //   name: …                             the act's own name (none yet: the chapter's name stands big in its place, and nothing under it)
    // Chapters — all in saga/chapters.txt, in its order (a chapter in another file would stand where its file happens to be read):
    //   [chapter ch1]
    //   act: 1                              its act (none: it stands alone, as the prologue)
    //   name: Ті, що йдуть на південь      its name; with an act named: under the act's name, 4 px smaller (the user 2026-11-17), never «Глава N»
    //   when: reached ch2 lit_plan, near npc.lit 15   conditions (StoryCond) over its quest begun: its title waits for them (none: the quest alone)
    //   after: 30                           its title comes up so many seconds after its quest began and «when:» held (never under MinAfter)
    //   lands: Луки                         where it goes on, under its name
    //   epigraph: …                         a line under its name, in italics
    //   closing: …                          a line under its entries once it is closed
    //   done: world heart_first             conditions (StoryCond): the chapter closed for this player
    // A quest and a tale name theirs («chapter:»); a chapter is shown once it has an entry, or once its title came up on this player's screen and it has begun in this world (Opened: its flag
    // «chapter_seen.<id>»: the user 2026-11-17 — chapter 2 had begun, «Зала встає» on the screen, and the book had no page for it, nothing of it passed yet);
    // none at all: the first one.
    // Not shown: what has not happened yet, the chapters ahead (spoilers).
    // Its start (the user 2026-11-14): once a quest of a chapter has begun for this player (a quest of the server: once it leads him), its label and
    // name come up on the screen — the game's «new biome» title and sound, golden (RavenMsg.Title) — once a character (flag «chapter_seen.<id>»);
    // not while a scene plays, one a pass; «when:» — and only once these hold too (chapter 2: the next day, come to Lit — the user 2026-11-16: it came up
    // the moment the talk that ended chapter 1 closed); «after:» — so many seconds after this game first saw it begun and «when:» holding, never fewer
    // than MinAfter (the user 2026-11-16: «завжди текст має мати затримку»); put up only when the game's messages are quiet (RavenMsg.Quiet), else the next pass;
    // taken off the screen before its time (the HUD hidden, left the world: RavenMsg.Title's «lost») — its flag goes, and it comes again. A character that played before this was made (the var «chapter_titles» not set, played > OldPlayed s) is
    // not shown the chapters it began then. Cost: twice a second, each chapter not shown yet against the quests (a few, a flag or a key each).
    //
    // What one knows (the user 2026-11-12: people, places and lore that fill in with time — a name not known yet, a thing learnt in a talk).
    // An entry is a block of its section's word (Sections: person, place, lore — a new section is one row there):
    //   [person echo]
    //   chapter: prologue                   the chapter it first belongs to (the book groups by it)
    //   shown: lived prologue.echo          conditions: it is in the book (none: once any of its lines or quotes is there; several lines: any)
    //   name: flag know_eirik :: Ейрік      its name as known now: the first line whose conditions hold («<conditions> :: <words>»;
    //   name: Відлуння                      the last one without conditions — what it is called till more is known)
    //   about: Згасла постать у хаті        a word or two under the name: the first line whose conditions hold (none: nothing)
    //   line: asked heid stones :: …        a thing known of it: there once its conditions hold, in the file's order. Without conditions: with the entry.
    //                                       A line that later words replace says so itself («!flag …»).
    //   quote: zala.rizba Різьба Ейріка     what the player has read on that piece of a place (LocTouch «read:»: of the lines he has read, the
    //                                       first as written — the fullest; word for word), under this caption
    //   map: been zala :: hall              «Показати на мапі»: the first line whose conditions hold names the place (StoryWhere)
    //   [person heid +]                     more for an entry there already, from another file (a later chapter's own: saga/ch2.txt): its lines
    //                                       after the entry's, its names and abouts before them — files read in name order, so ch2's stand before ch1's
    //                                       (a later name is the truer one); no chapter of its own
    // A riddle (the user 2026-11-16: its own ribbon «Загадки») is an entry of a section that closes (Section.Closes): its name is what is not understood, its
    // lines the clues in the author's order, with no dates, and it has one key more:
    //   [riddle r_south]
    //   answer: reached ch2 x :: …         its answer: once the conditions of one hold (the first such), the riddle is closed — the words stand under its clues,
    //                                       and it goes below the open ones in the list. Always with conditions, and never by a talk alone (storycheck):
    //                                       a main clue — a scene, a stage, an event of the world — cannot be lost, a side one (a talk's place) can
    // Who may tell more (MoreOf, the user 2026-11-16): the open entry's lines not there yet that wait for a talk's place («asked») — one that can be heard
    // now names its person («{0} може розповісти більше»: as that person's own entry of the book names it now; not in the book yet — not named); one
    // whose every way is past (NpcTalk.Askable: Gone) leaves the quiet «Про це вже нікого спитати». Nobody retells what was lost. A line that waits
    // for anything else says nothing (no word of what is ahead). Asked only for the entry open on the right page, as it is drawn; not in the stamp.
    // Conditions here may say «or»: «a, b | c» — a and b, or c (StoryCond itself is «and» only).
    // Whatever a condition can ask opens a line: one's own entry of the chronicle («lived <quest>.<stage>[.<way>]»), a moment of the world
    // one saw or was told («knows <tale>»), a place stood on («been»), a talk's place heard («asked <talk> <place>»: the talk keeps it
    // once someone asks — NpcTalk.Note), a piece read («read <place>.<piece>»), a flag a talk or a scene set, the world's own words.
    // Nothing is kept for it: what is known is asked anew from what the story already keeps.
    // New on the page: an entry is new till it has been opened as it is now — its Stamp (the words it shows: a line put in above another
    // in the file lights nothing, a line reworded is new) against the one read, kept in the character (Story.Text «saga_read»:
    // «id:stamp/id:stamp»). One for all worlds: in another world only what differs there is new, not the whole book again.
    // What is learnt is each character's own, and a world's book has only what that world's story has come to (the user 2026-11-16): «asked» holds for
    // a place heard in this world, or heard in another one once here the one who says it has come and an answer leading there has had its time
    // (NpcTalk.Heard: Opened) — the talk here does not offer it again, the book opens it as this world goes on. «been», «knows» are this world's anyway;
    // an entry of a server's story may still ask the world itself in «shown:» («world …»).
    // Learnt just now in a talk: a line under the person's words, «Записано в книгу — …» (Learnt).
    // Cost: nothing while the book is shut; while it is open, Stamp and Fresh once a second (the book's refresh): the tales'
    // world keys, the character's list and every entry's conditions — a few dozen now, each a flag or a key looked up. Pages (with the
    // witnesses: one pass over the world's keys) only when the page is drawn.
    internal static class StorySaga
    {
        internal const string Kind = "saga";

        internal sealed class Act { public string Id = "", Label = "", Name = ""; }
        internal sealed class Chapter { public string Id = "", Name = "", Lands = "", Epigraph = "", Closing = "", ActId = ""; public Act Act; public float After; public List<Cond> Done, When; } // (Act null: it stands alone — the prologue)

        internal sealed class Page
        {
            public Chapter Chapter;
            public bool Closed;                                                                  // its «done:» holds
            public readonly List<StoryTales.Told> World = new List<StoryTales.Told>();          // happened, by moment
            public int Happened, Of;                                                             // the world's moments of it: happened · all
            public readonly List<string> Own = new List<string>();                              // one's own entries (untranslated), in the order lived
        }

        // the sections of what one knows: the word of its blocks, its name in the book, what an empty one says
        internal sealed class Section { public string Word = "", Name = "", None = "", Rune = ""; public bool Closes; } // (Closes: its entries have an «answer:» — riddles; Rune: its mark in the book, the user's design 2026-11-16)
        internal const string ChronicleRune = "ᛚ";
        internal static readonly Section[] Sections =
        {
            new Section { Word = "person", Name = Lang.K("Люди"), None = Lang.K("Ти ще нікого тут не знаєш."), Rune = "ᛗ" },
            new Section { Word = "place", Name = Lang.K("Місця"), None = Lang.K("Ти ще не знаєш тут жодного місця."), Rune = "ᛟ" },
            new Section { Word = "lore", Name = Lang.K("Знання"), None = Lang.K("Про цей світ ти ще нічого не знаєш."), Rune = "ᚨ" },
            new Section { Word = "riddle", Name = Lang.K("Загадки"), None = Lang.K("Загадок у тебе ще нема."), Closes = true, Rune = "ᛈ" },
        };
        private static bool IsSection(string word) { foreach (Section s in Sections) if (s.Word == word) return true; return false; }

        internal sealed class Line { public List<List<Cond>> When; public string Text = ""; } // (When null: always; else any of its groups, each with all of its conditions)

        internal sealed class Entry
        {
            public string Id = "", Section = "", Chapter = "";
            public bool Adding; public int NamesAdded, AboutsAdded; // (read now from a «[person x +]» block: its names and abouts before the entry's own)
            public List<List<Cond>> Shown;
            public readonly List<Line> Names = new List<Line>(), Abouts = new List<Line>(), Lines = new List<Line>(), Maps = new List<Line>(), Answers = new List<Line>();
            public readonly List<KeyValuePair<string, string>> Quotes = new List<KeyValuePair<string, string>>(); // «place.piece» → its caption
        }

        // an entry as this player knows it now (the words untranslated)
        internal sealed class Known
        {
            public Entry Entry;
            public string Name = "", About = "", Map = "";                // Map: a place's word («»: no way to the map)
            public string Answer = ""; public bool Closed;                // a riddle: its answer once known (Closed)
            public readonly List<string> Lines = new List<string>();
            public readonly List<KeyValuePair<string, string>> Quotes = new List<KeyValuePair<string, string>>(); // caption → the words read
            public string Stamp = "";                                      // the words it shows now, as a few letters: read as it is now, it is not new
            public bool Fresh;
        }

        private static List<Chapter> _all;
        private static readonly List<Act> Acts = new List<Act>(); // (read with the chapters: Load)
        private static readonly List<Entry> Entries = new List<Entry>();
        internal static readonly List<string> Errors = new List<string>();

        private static List<Chapter> All { get { if (_all == null) Load(); return _all; } }
        internal static void Reload() { _all = null; Load(); } // (at once, with the quests: a talk's places the entries ask for are noted before anyone talks)
        internal static bool HasChapter(string id) { return Find(id) != null; }

        // ---- a chapter's start: its title on the screen (StoryModules: Each)
        private const string SeenFlag = "chapter_seen.", TitlesVar = "chapter_titles";
        private static readonly Dictionary<string, float> BegunAt = new Dictionary<string, float>(); // (session: when this game first saw a chapter begun — its «after:» counts from here; a relog counts again)
        private const int OldPlayed = 120; // (seconds of play: a character older than this had its chapters begun before the titles were)
        private const float MinAfter = 5f; // (a title never comes up the moment its chapter begins — the step, the talk, the scene that began it: the user 2026-11-16)

        internal static void Look(Player p)
        {
            if (p == null || StoryScenes.Playing.Length > 0 || RavenMsg.TitleWaiting) return;
            bool first = Story.Var(p, TitlesVar) == 0, old = first && StoryTrackConds.Played(p) > OldPlayed;
            if (first) Story.SetVar(p, TitlesVar, 1, true); // (judged once, now: a title that cannot come up yet must not make a new character «old» two minutes on)
            foreach (Chapter c in All)
            {
                if (Story.Flag(p, SeenFlag + c.Id) || !Begun(p, c)) continue;
                if (old) { Story.SetFlag(p, SeenFlag + c.Id, true); continue; } // (begun before the titles were: not now, out of the blue)
                if (c.When != null && !BegunAt.ContainsKey(c.Id) && !StoryCond.All(p, c.When)) continue; // (once they held, the wait counts on: walked off meanwhile — it still comes)
                float t;
                if (!BegunAt.TryGetValue(c.Id, out t)) BegunAt[c.Id] = t = UnityEngine.Time.time;
                if (UnityEngine.Time.time - t < Math.Max(c.After, MinAfter)) continue; // (the user 2026-11-16: chapter 1 came up the moment the first heart stood, chapter 2 the moment chapter 1's last talk closed)
                Player me = p; string flag = SeenFlag + c.Id;
                if (Title(c, () => { if (me != null) Story.SetFlag(me, flag, false); })) Story.SetFlag(p, flag, true); // (not up: not quiet yet — the next pass; lost before its time: again)
                return; // (one a pass)
            }
        }

        // a quest of it has begun for this player
        private static bool Begun(Player p, Chapter c)
        {
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
                if (q.Chapter == c.Id && StoryQuests.Leads(p, q) && StoryQuests.State(p, q).Length > 0) return true;
            return false;
        }

        // its title has come up for this character, and here it has begun: its page is in the book before anything of it is passed. The flag is the
        // character's, one for all worlds — in a world whose story has not come to it yet it is not shown (the user 2026-11-16: another world's book has
        // only what that world's story has come to). Cost: the quests once (a few) — the page drawn, the book's stamp once a second while it is open
        private static bool Opened(Player p, Chapter c) { return Story.Flag(p, SeenFlag + c.Id) && Begun(p, c); }

        // the act's label small over it; big — the act's name, the chapter's name under it 4 px smaller (no «Глава N»: the user 2026-11-17);
        // an act with no name yet — the chapter's name big, nothing under it; the prologue — its name alone
        private static bool Title(Chapter c, Action lost = null)
        {
            Act a = c.Act;
            if (a == null) return RavenMsg.Title("", Lang.T(c.Name), "", lost);
            string label = a.Label.Length > 0 ? Lang.T(a.Label) : "";
            return a.Name.Length > 0 ? RavenMsg.Title(label, Lang.T(a.Name), Lang.T(c.Name), lost) : RavenMsg.Title(label, Lang.T(c.Name), "", lost);
        }

        // the console's «story title <chapter>»: shown now, nothing kept
        internal static string ShowTitle(string id)
        {
            Chapter c = Find(id);
            if (c == null) { List<string> ids = new List<string>(); foreach (Chapter x in All) ids.Add(x.Id); return "raven story title <глава> — нема глави «" + id + "» (є: " + string.Join(", ", ids.ToArray()) + ")"; }
            return Title(c) ? "Напис глави «" + c.Id + "» на екрані (у лозі — «title …», коли зійде)." : "Не зараз: інший напис ще на екрані, ідуть повідомлення гри, HUD сховано чи екрана гри нема.";
        }
        private static Chapter Find(string id) { foreach (Chapter c in All) if (c.Id == id) return c; return null; }

        private static bool GoodId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id) if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        // «<conditions> :: <words>», or the words alone
        private static Line ReadLine(string val, string at)
        {
            Line l = new Line();
            int k = val.IndexOf(" :: ", StringComparison.Ordinal);
            if (k < 0) { l.Text = val; return l; }
            l.When = new List<List<Cond>>();
            ReadWhen(val.Substring(0, k), at, l.When);
            l.Text = val.Substring(k + 4).Trim();
            if (l.Text.Length == 0) Errors.Add(at + "після «::» нема слів");
            return l;
        }

        // «a, b | c» into its groups. A wrong condition never holds, an empty group is left out: a line stays shut, never open by mistake
        private static void ReadWhen(string text, string at, List<List<Cond>> into)
        {
            foreach (string part in text.Split('|'))
            {
                List<string> wrong = new List<string>();
                List<Cond> all = StoryCond.Parse(part.Trim(), wrong);
                foreach (string w in wrong) Errors.Add(at + w);
                if (all.Count == 0) Errors.Add(at + "порожня умова біля «|» чи перед «::»"); else into.Add(all);
            }
        }

        private static bool Holds(Player p, List<List<Cond>> any)
        {
            if (any == null) return true;
            foreach (List<Cond> all in any) if (StoryCond.All(p, all)) return true;
            return false;
        }

        private static void Load()
        {
            StoryTrackConds.Register(); // (its conditions: known before they are read)
            _all = new List<Chapter>(); Acts.Clear(); Entries.Clear(); Errors.Clear();
            Dictionary<string, string> all = StoryFiles.All(Kind, null, false);
            List<string> order = new List<string>(all.Keys);
            order.Sort(StringComparer.Ordinal); // (ch1.txt before ch2.txt: the later chapter's adding block is read last and its names stand first)
            string[] heads = new string[Sections.Length + 2];
            heads[0] = "chapter"; heads[1] = "act"; for (int i = 0; i < Sections.Length; i++) heads[i + 2] = Sections[i].Word;
            for (int pass = 0; pass < 2; pass++) // (every file's own blocks first, then the adding ones «[person heid +]»: a chapter's file adds to an entry of another — StoryFiles.Part)
            foreach (string file in order)
            {
                KeyValuePair<string, string> f = new KeyValuePair<string, string>(file, all[file]);
                Chapter c = null; Entry e = null; Act act = null;
                foreach (KeyValuePair<int, string> l in StoryFiles.Lines(StoryFiles.Part(f.Value, pass == 1, heads)))
                {
                    string line = l.Value, at = f.Key + ":" + l.Key + ": ";
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        c = null; e = null; act = null;
                        string[] head = line.Substring(1, line.Length - 2).Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        string word = head.Length > 0 ? head[0] : "", id = head.Length > 1 ? head[1].Trim() : "";
                        if (pass == 1) // («[person heid +]»: more lines for an entry there already — a later chapter's; its names and abouts go first, as a later name is the truer one)
                        {
                            string have = id.EndsWith("+") ? id.Substring(0, id.Length - 1).Trim() : id;
                            e = Entries.Find(x => x.Id == have && x.Section == word);
                            if (e == null) Errors.Add(at + "[" + word + " " + have + " +]: нема запису «" + have + "» у розділі «" + word + "»");
                            else { e.Adding = true; e.NamesAdded = e.AboutsAdded = 0; } // (this block's names before all read so far: the later chapter's first)
                            continue;
                        }
                        if (word == "chapter")
                        {
                            if (!Story.GoodName(id) || _all.Exists(x => x.Id == id)) { Errors.Add(at + "погана або вже вжита назва глави «" + id + "»"); continue; }
                            c = new Chapter { Id = id }; _all.Add(c);
                        }
                        else if (word == "act")
                        {
                            if (!GoodId(id) || Acts.Exists(x => x.Id == id)) { Errors.Add(at + "погана або вже вжита назва акту «" + id + "»"); continue; }
                            act = new Act { Id = id }; Acts.Add(act);
                        }
                        else if (IsSection(word))
                        {
                            if (!GoodId(id) || Entries.Exists(x => x.Id == id)) { Errors.Add(at + "погана або вже вжита назва запису «" + id + "» (літери, цифри, _; одна на всі розділи книги)"); continue; }
                            e = new Entry { Id = id, Section = word }; Entries.Add(e);
                        }
                        else Errors.Add(at + "невідомий блок «" + word + "»");
                        continue;
                    }
                    if (c == null && e == null && act == null) { Errors.Add(at + "рядок поза блоком"); continue; }
                    int k = line.IndexOf(':');
                    if (k <= 0) { Errors.Add(at + "рядок без «:»"); continue; }
                    string key = line.Substring(0, k).Trim(), val = line.Substring(k + 1).Trim();
                    if (act != null)
                        switch (key)
                        {
                            case "label": act.Label = val; break;
                            case "name": act.Name = val; break;
                            default: Errors.Add(at + "невідоме «" + key + "» в акті (label:, name:)"); break;
                        }
                    else if (c != null)
                        switch (key)
                        {
                            case "name": c.Name = val; break;
                            case "act": c.ActId = val; break;
                            case "label": Errors.Add(at + "label: — тепер в акті: «[act <n>]» з label:, а в главі «act: <n>»"); break;
                            case "lands": c.Lands = val; break;
                            case "after": if (!float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out c.After) || c.After < 0f) Errors.Add(at + "after: секунди"); break;
                            case "epigraph": c.Epigraph = val; break;
                            case "closing": c.Closing = val; break;
                            case "done": c.Done = StoryCond.Parse(val, Errors); break;
                            case "when": c.When = StoryCond.Parse(val, Errors); break;
                            default: Errors.Add(at + "невідоме «" + key + "»"); break;
                        }
                    else
                        switch (key)
                        {
                            case "chapter": if (e.Adding) Errors.Add(at + "chapter: — лише в самому записі, не в доповненні"); else e.Chapter = val; break;
                            case "shown": if (e.Shown == null) e.Shown = new List<List<Cond>>(); ReadWhen(val, at, e.Shown); break;
                            case "name": if (e.Adding) e.Names.Insert(e.NamesAdded++, ReadLine(val, at)); else e.Names.Add(ReadLine(val, at)); break;
                            case "about": if (e.Adding) e.Abouts.Insert(e.AboutsAdded++, ReadLine(val, at)); else e.Abouts.Add(ReadLine(val, at)); break;
                            case "line": e.Lines.Add(ReadLine(val, at)); break;
                            case "map": e.Maps.Add(ReadLine(val, at)); break;
                            case "answer":
                                {
                                    Section sec = Array.Find(Sections, x => x.Word == e.Section);
                                    Line a = ReadLine(val, at);
                                    if (sec == null || !sec.Closes) Errors.Add(at + "answer: — лише в загадці");
                                    else if (a.When == null) Errors.Add(at + "answer: <умови> :: <слова> — без умов загадка була б розгадана одразу");
                                    else e.Answers.Add(a);
                                    break;
                                }
                            case "quote":
                                {
                                    string[] w = val.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                    if (w.Length == 0 || w[0].IndexOf('.') <= 0) Errors.Add(at + "quote: <місце>.<деталь> [підпис]");
                                    else e.Quotes.Add(new KeyValuePair<string, string>(w[0], w.Length > 1 ? w[1].Trim() : ""));
                                    break;
                                }
                            default: Errors.Add(at + "невідоме «" + key + "»"); break;
                        }
                }
            }
            foreach (Chapter c in _all)
            {
                if (c.Name.Length == 0) Errors.Add(c.Id + ": без name:");
                if (c.ActId.Length > 0 && (c.Act = Acts.Find(x => x.Id == c.ActId)) == null) Errors.Add(c.Id + ": act: нема акту «" + c.ActId + "» (блок «[act " + c.ActId + "]» у saga/chapters.txt)");
            }
            foreach (Entry e in Entries)
            {
                if (e.Names.Count == 0 || e.Names[e.Names.Count - 1].When != null) Errors.Add(e.Id + ": останній name: має бути без умов (як його звати, поки більше не відомо)");
                if (!_all.Exists(x => x.Id == e.Chapter)) Errors.Add(e.Id + ": chapter: нема глави «" + e.Chapter + "»");
                if (e.Shown == null && e.Lines.Count == 0 && e.Quotes.Count == 0) Errors.Add(e.Id + ": ні shown:, ні line:, ні quote: — запису ніколи не буде в книзі");
            }
            Entries.RemoveAll(e => e.Names.Count == 0 || !_all.Exists(x => x.Id == e.Chapter));
            Asking.Clear();
            foreach (Entry e in Entries)
                foreach (List<Line> list in new[] { e.Names, e.Abouts, e.Lines, e.Answers })
                    foreach (Line l in list)
                        foreach (string key in Asks(l))
                        {
                            List<Entry> by;
                            if (!Asking.TryGetValue(key, out by)) Asking[key] = by = new List<Entry>();
                            if (!by.Contains(e)) by.Add(e);
                        }
            foreach (string e in Errors) Story.Log("saga: " + e);
            Story.Log("saga: " + Acts.Count + " acts, " + _all.Count + " chapters, " + Entries.Count + " entries" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
        }

        // ---------------------------------------------------------------- the chronicle's pages
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
                if (pg.World.Count > 0 || pg.Own.Count > 0 || Opened(p, c)) pages.Add(pg); // (its title shown and begun here: in the book before anything of it is passed)
            }
            if (pages.Count == 0) pages.Add(by[All[0].Id]);
            return pages;
        }

        // ---------------------------------------------------------------- what one knows
        private static int First(Player p, List<Line> lines) { for (int i = 0; i < lines.Count; i++) if (Holds(p, lines[i].When)) return i; return -1; }

        // an entry as this player knows it now; null: not in his book yet
        private static Known Of(Player p, Entry e)
        {
            if (!Holds(p, e.Shown)) return null;
            int name = First(p, e.Names);
            if (name < 0) return null; // (a file with no plain name: said at the load)
            Known k = new Known { Entry = e, Name = e.Names[name].Text };
            StringBuilder st = new StringBuilder(k.Name);
            int about = First(p, e.Abouts);
            if (about >= 0) { k.About = e.Abouts[about].Text; st.Append('|').Append(k.About); }
            foreach (Line l in e.Lines)
                if (Holds(p, l.When)) { k.Lines.Add(l.Text); st.Append('|').Append(l.Text); }
            foreach (KeyValuePair<string, string> q in e.Quotes)
            {
                string read = LocTouch.ReadBy(p, q.Key);
                if (read.Length == 0) continue;
                k.Quotes.Add(new KeyValuePair<string, string>(q.Value, read));
                st.Append('|').Append(read);
            }
            int answer = First(p, e.Answers);
            if (answer >= 0) { k.Closed = true; k.Answer = e.Answers[answer].Text; st.Append("|=").Append(k.Answer); }
            if (e.Shown == null && k.Lines.Count == 0 && k.Quotes.Count == 0 && !k.Closed) return null; // (nothing known of it yet)
            int map = First(p, e.Maps);
            if (map >= 0) k.Map = e.Maps[map].Text; // (a way to the map is no news: not in the stamp)
            k.Stamp = ((uint)st.ToString().GetStableHashCode() & 0xFFFFFFu).ToString("x");
            return k;
        }

        // a section's entries this player has, by chapter, then as written; each new or read (Fresh). A section that closes: the open ones first, the closed below
        internal static List<Known> KnownOf(Player p, string section)
        {
            List<Known> l = new List<Known>();
            if (p == null) return l;
            Dictionary<string, string> read = Read(p);
            foreach (Chapter c in All)
                foreach (Entry e in Entries)
                {
                    if (e.Section != section || e.Chapter != c.Id) continue;
                    Known k = Of(p, e);
                    if (k == null) continue;
                    string had;
                    k.Fresh = !read.TryGetValue(e.Id, out had) || had != k.Stamp;
                    l.Add(k);
                }
            if (l.Exists(k => k.Closed))
            {
                List<Known> closed = l.FindAll(k => k.Closed);
                l.RemoveAll(k => k.Closed);
                l.AddRange(closed);
            }
            return l;
        }

        internal static string ChapterName(string id) { Chapter c = Find(id); return c != null ? c.Name : ""; }

        // ---------------------------------------------------------------- learnt in a talk: said under the person's words
        // A talk's place just heard (NpcTalk.Pick) that opened something of the book (the user 2026-11-16: «підсвічувати в діалозі, що знання пішло у книгу»):
        // a line under its words (RavenBook.TalkNote — the window's own line; an answer that closes the talk: in the corner), one an entry, its section and
        // its name as it is now. Only entries whose name, word or line asks that place and holds now. Cost: on a pick of such a place, its few entries
        private static readonly Dictionary<string, List<Entry>> Asking = new Dictionary<string, List<Entry>>(); // «talk place» → the entries a line of which asks it

        private static IEnumerable<string> Asks(Line l)
        {
            if (l.When == null) yield break;
            foreach (List<Cond> all in l.When)
                foreach (Cond c in all)
                    if (!c.Not && c.A.Length > 2 && c.A[0] == "asked") yield return c.A[1] + " " + c.A[2];
        }

        internal static void Learnt(Player p, string talk, string place)
        {
            List<Entry> by;
            string key = talk + " " + place;
            if (p == null || All.Count == 0 || !Asking.TryGetValue(key, out by)) return; // (All: read first)
            foreach (Entry e in by)
            {
                Known k = Of(p, e);
                if (k == null || !Opens(p, e, key)) continue;
                Section s = Array.Find(Sections, x => x.Word == e.Section);
                RavenBook.TalkNote(Lang.F("Записано в книгу — {0}: {1}", s != null ? Lang.T(s.Name) : "", Lang.T(k.Name)), true);
            }
        }

        // a name, word or line of it that asks that place holds now
        private static bool Opens(Player p, Entry e, string key)
        {
            foreach (List<Line> list in new[] { e.Names, e.Abouts, e.Lines, e.Answers })
                foreach (Line l in list)
                    foreach (string asked in Asks(l))
                        if (asked == key) { if (Holds(p, l.When)) return true; break; } // (the words first: its conditions only for a line that asks it)
            return false;
        }

        // ---------------------------------------------------------------- who may tell more (the header)
        internal sealed class More { public readonly List<string> Who = new List<string>(); public bool Lost; } // (Who: their names as the book knows them now, untranslated)

        // the open entry's lines that are not there yet and wait for a talk. A way of a line («a, b | c»: each group) is: lost — a place it asks can
        // never be heard again; dead — it can never hold for another reason (another way of that row was heard, later words replaced it: no loss);
        // askable — every place it still asks can be heard now; else it waits (not yet, or for something that is no talk). A line is lost when it has a
        // lost way and none that waits or can be asked. Cost: the entry's lines not there yet, each place asked through NpcTalk.Askable — as it is drawn
        internal static More MoreOf(Player p, Known k)
        {
            More m = new More();
            if (p == null || k == null || k.Closed) return m; // (a riddle answered: nothing to chase)
            List<string> talks = new List<string>();
            foreach (Line l in k.Entry.Lines)
            {
                if (l.When == null || Holds(p, l.When)) continue;
                bool lost = false, waits = false;
                foreach (List<Cond> all in l.When)
                {
                    bool gone = false, dead = false, later = false;
                    List<string> now = null;
                    foreach (Cond c in all)
                    {
                        if (c.Test(p)) continue;
                        if (c.Not || c.A.Length < 2 || c.A[0] != "asked") // (not a talk: is its time past, or still to come?)
                        {
                            NpcTalk.Ask t = NpcTalk.TimeOf(p, c);
                            if (t == NpcTalk.Ask.Gone) dead = true; else later = true;
                            continue;
                        }
                        switch (NpcTalk.Askable(p, c.A[1], c.A.Length > 2 ? c.A[2] : ""))
                        {
                            case NpcTalk.Ask.Gone: gone = true; break;
                            case NpcTalk.Ask.Settled: dead = true; break;
                            case NpcTalk.Ask.Later: later = true; break;
                            default: if (now == null) now = new List<string>(); now.Add(c.A[1]); break;
                        }
                    }
                    if (dead) continue; // (first: words that later ones replaced are no loss, whoever has left since)
                    if (gone) { lost = true; continue; }
                    waits = true;
                    if (now != null) foreach (string t in now) if (!talks.Contains(t)) talks.Add(t); // (also beside a «later»: that one can be asked already)
                }
                if (lost && !waits) m.Lost = true;
            }
            foreach (string t in talks)
            {
                Entry who = Entries.Find(x => x.Id == t); // (the person's own entry: named as its talk — storycheck)
                Known named = who != null ? Of(p, who) : null;
                if (named != null && !m.Who.Contains(named.Name)) m.Who.Add(named.Name); // (not in the book yet: not named)
            }
            return m;
        }

        // what of it was read, as it then was: the character's, one for all worlds
        private const string ReadText = "saga_read";
        private static Dictionary<string, string> Read(Player p)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string part in Story.Text(p, ReadText).Split('/'))
            {
                int c = part.IndexOf(':');
                if (c > 0) d[part.Substring(0, c)] = part.Substring(c + 1);
            }
            return d;
        }

        // an entry opened in the book: as it is now, it is no longer new (entries gone from the files go from the marks too)
        internal static void SeenEntry(Player p, Known k)
        {
            if (p == null || k == null) return;
            Dictionary<string, string> read = Read(p);
            string had;
            if (read.TryGetValue(k.Entry.Id, out had) && had == k.Stamp) return;
            read[k.Entry.Id] = k.Stamp;
            StringBuilder sb = new StringBuilder();
            foreach (Entry e in Entries)
                if (read.TryGetValue(e.Id, out had)) sb.Append(sb.Length > 0 ? "/" : "").Append(e.Id).Append(':').Append(had);
            Story.SetText(p, ReadText, sb.ToString());
        }

        internal static bool FreshIn(Player p, string section) { foreach (Known k in KnownOf(p, section)) if (k.Fresh) return true; return false; }

        // the console's «raven story saga»: the pages as words (what the book would show now)
        internal static string Describe(Player p)
        {
            StringBuilder sb = new StringBuilder("Сага " + (p != null ? p.GetPlayerName() : "") + " (нового: " + (Fresh(p) ? "так" : "ні") + "):");
            foreach (string e in Errors) sb.Append("\n  ! ").Append(e);
            foreach (Page pg in Pages(p))
            {
                sb.Append("\n[").Append(pg.Chapter.Id).Append("] ").Append(pg.Chapter.Name).Append(" — світ ").Append(pg.Happened).Append(" з ").Append(pg.Of).Append(pg.Closed ? ", закінчено" : "");
                foreach (StoryTales.Told t in pg.World)
                    sb.Append("\n  ").Append(t.Known ? t.Saga : "(чутка) " + (t.Rumor.Length > 0 ? t.Rumor : "—")).Append(t.Known && (t.You || t.Witnesses.Count > 0) ? "  · свідки: " + (t.You ? "ти " : "") + string.Join(", ", t.Witnesses.ToArray()) : "");
                foreach (string e in pg.Own) sb.Append("\n  я: ").Append(e);
            }
            foreach (Section s in Sections)
            {
                List<Known> known = KnownOf(p, s.Word);
                sb.Append("\n").Append(s.Name).Append(" (").Append(known.Count).Append(" з ").Append(Entries.FindAll(e => e.Section == s.Word).Count).Append("):");
                foreach (Known k in known)
                {
                    sb.Append("\n  [").Append(k.Entry.Id).Append("] ").Append(k.Name).Append(k.About.Length > 0 ? " — " + k.About : "").Append(k.Fresh ? "  (нове)" : "").Append(k.Map.Length > 0 ? "  · мапа: " + k.Map : "");
                    foreach (string l in k.Lines) sb.Append("\n    ").Append(l);
                    if (k.Closed) sb.Append("\n    = ").Append(k.Answer);
                    More more = MoreOf(p, k);
                    if (more.Who.Count > 0) sb.Append("\n    (може розповісти більше: ").Append(string.Join(", ", more.Who.ToArray())).Append(")");
                    if (more.Lost) sb.Append("\n    (про це вже нікого спитати)");
                    foreach (KeyValuePair<string, string> q in k.Quotes) sb.Append("\n    «").Append(q.Key).Append("»: ").Append(q.Value.Replace("\n", " / "));
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- new on the page (the tab's gold dot)
        // The chronicle is read a chapter at a time (till 2026-11-16 one number for all of it: a new entry of a chapter not shown went read unseen):
        // each chapter's weight against the one it had when its page was last drawn, kept in the character per world (a server's part of it differs)
        private static string SeenVar(string chapter) { return OldSeenVar + "_" + chapter; }
        private static string OldSeenVar { get { return "saga_seen_" + (ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L); } }

        // how much of each chapter there is to read: one's own entries, a rumour 1, a known world entry 2 (a rumour lifted is new again)
        private static Dictionary<string, int> Weights(Player p)
        {
            Dictionary<string, int> w = new Dictionary<string, int>();
            foreach (Chapter c in All) w[c.Id] = 0;
            foreach (string e in Story.Of(p).Saga) { StoryQuests.QuestDef q; StoryQuests.StageDef s = StoryQuests.Entry(e, out q); if (s != null && s.Saga.Length > 0 && w.ContainsKey(q.Chapter)) w[q.Chapter]++; }
            foreach (StoryTales.Told t in StoryTales.Saga(p, false)) if (t.Moment != 0 && w.ContainsKey(t.Chapter)) w[t.Chapter] += t.Known ? 2 : 1;
            // a character from before the chapters were read apart: all of it was read — each chapter as it is now; something was new — all new once
            int old = Story.Var(p, OldSeenVar);
            if (old > 0)
            {
                int all = 0;
                foreach (int n in w.Values) all += n;
                if (old >= all) foreach (KeyValuePair<string, int> e in w) Story.SetVar(p, SeenVar(e.Key), e.Value, true);
                Story.SetVar(p, OldSeenVar, 0, true);
            }
            return w;
        }

        internal static bool FreshChapter(Player p, string chapter)
        {
            int n;
            return p != null && Weights(p).TryGetValue(chapter, out n) && n > Story.Var(p, SeenVar(chapter));
        }

        internal static bool FreshChronicle(Player p)
        {
            if (p == null) return false;
            foreach (KeyValuePair<string, int> e in Weights(p)) if (e.Value > Story.Var(p, SeenVar(e.Key))) return true;
            return false;
        }

        // anything new in the saga: the chronicle, or an entry of what one knows
        internal static bool Fresh(Player p)
        {
            if (p == null) return false;
            if (FreshChronicle(p)) return true;
            foreach (Section s in Sections) if (FreshIn(p, s.Word)) return true;
            return false;
        }

        // a chapter of the chronicle seen (its page drawn): what is on it is no longer new
        internal static void Seen(Player p, string chapter)
        {
            int n;
            if (p != null && Weights(p).TryGetValue(chapter, out n) && n != Story.Var(p, SeenVar(chapter))) Story.SetVar(p, SeenVar(chapter), n, true);
        }

        // what the page is drawn from, without the witnesses' names (the book draws it again when this changes)
        internal static string Stamp(Player p)
        {
            if (p == null) return "";
            StringBuilder s = new StringBuilder().Append(Story.Of(p).Saga.Count).Append(':'); // (the played minutes must not move it every 30 s)
            foreach (StoryTales.Told t in StoryTales.Saga(p, false)) s.Append(t.Moment).Append(t.Known ? "k" : "").Append(',');
            StoryQuests.QuestDef q; StoryQuests.StageDef st; UnityEngine.Vector3 at;
            foreach (StoryQuests.Standing a in StoryQuests.NowAll(p)) s.Append('|').Append(a.Quest.Id).Append('.').Append(a.Stage.Id).Append(StoryQuests.MarkAt(a.Quest, a.Stage, out at) ? "m" : ""); // (every quest he stands in: the book shows a card each)
            s.Append('|').Append(StoryWorld.Texts("tale_").Count); // (a witness more)
            s.Append('|');
            foreach (Chapter c in All) s.Append(c.Done != null && c.Done.Count > 0 && StoryCond.All(p, c.Done) ? 'c' : Opened(p, c) ? 's' : 'o'); // (a chapter closed: its page says so at once, not at the next other change; its title shown: its page is there at once)
            foreach (Section sec in Sections) foreach (Known k in KnownOf(p, sec.Word)) s.Append('|').Append(k.Entry.Id).Append(k.Stamp).Append(k.Fresh ? "!" : "").Append(k.Map.Length > 0 ? "m" : "");
            return s.ToString();
        }
    }
}
#endif

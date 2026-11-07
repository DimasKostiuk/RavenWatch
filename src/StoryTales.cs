#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // What happened in the world, as the people saw it (the user 2026-11-05: people remember what happened round them, and so the
    // story reaches the one who missed it). A tale is a moment of the world's story, written in assets/story/tales:
    //   [tale gate]
    //   when: gate hall open                 the world's conditions that make it happen (the first time they hold is its moment)
    //   at: hall.dveri_holovni 30           where it happened and how near one had to be to see it (m, 30 if not said)
    //   known: quest prologue_main done     a player who knows it anyway (besides one who saw it or was told: the world's text knows_<his id>, Knows)
    //   saw: Я сам|сама бачив|бачила…      what a witness tells (the speaker's forms «а|б», the listener's «{а|б}»); several lines of saw, heard or hearsay: variants, each person always tells its own (WordsOf)
    //   heard: Кажуть, …                    what one who only heard of it tells
    //   as heid: Я відчинила…               what this one person says of it, however she knows it: a person of the story was part of it, «they say» is no word for her
    //   chapter: prologue                   the book's «Сага» (StorySaga): its chapter, and
    //   saga: Браму Зали Варти відчинено.   its entry there for one who knows it (with the names of the players who saw it)
    //   rumor: Кажуть, біля Зали щось сталося.   … and for one who does not yet: a rumour, till someone tells it (none: a plain one)
    // Its moment: the world's key «tale_<id>» (StoryWorld.Var: the second it first held; any game may set it, once), and where it was
    // then: «tale_<id>_at» (StoryWorld.Text), so a place that depends on who asks («heart.near»: the heart nearest to you) is the same
    // spot for every game and every person afterwards. Already true when this game first looks (a world from before the tale, or its
    // keys loaded with the world): an old moment, nobody saw it — only what turns true while the game watches is seen. A person within
    // reach in the first Witness s remembers it as seen (NpcMemory «tale_<id>», what «saw»; one who had only heard of it and is there
    // too: seen now); the people of its heart who do not know it yet and were not there, as heard; a player within reach knows it
    // (this world's text about him: Knows) and his name goes into the world — «tale_<id>_w<his id>» (StoryWorld.Text), a key of his own, so two seeing it at
    // once never write over each other: the saga's witnesses (the first MaxWitnesses of a tale). In a talk (NpcTalk): a settler tells it first when asked what happened while you were away (NpcAway.Sources); a talk of its own (Heid): the condition «taleuntold» (it remembers one you do not know), the words
    // «{tale}» (that one, as seen or heard) and the deed «!taletold» (now you know). Kept for good, as the story's own memories.
    // How it spreads (the user 2026-11-05: «як вірус між NPC»), each step a level lower, never over a better one:
    //   saw       a witness (above)
    //   heard     a witness's own people; one a player who knows it comes within CarryNear of (Carry: «you brought it»)
    //   hearsay   «hearsay:» (none: heard) — the people of one you brought it to; and anyone whose home lies inside its ring: from
    //             its spot a ring grows SpreadPerDay m a game day, up to SpreadMost (a person far off is not loaded — it learns
    //             when it next looks, so nothing is sent anywhere). An old moment (no spot) spreads nowhere.
    // Cost: a look at the tales twice a second on each game (StoryQuests.Each: world keys only), the people near you every CarryEvery s,
    // and, where a person is run, every LookEvery s a person (NpcActor.Ticks: a few tales, a distance each; its memory read only
    // inside a ring).
    internal static class StoryTales
    {
        internal const string Kind = "tales";
        internal static float Witness = 60f, LookEvery = 5f; // s after its moment one near counts as having seen it · s between a person's looks

        internal static float CarryNear = 4f, CarryEvery = 3f, SpreadPerDay = 300f, SpreadMost = 2000f; // m from you a person hears it · s between those looks · m its ring grows a game day · m at most
        internal const string Saw = "saw", Heard = "heard", Hearsay = "hearsay"; // (what a person keeps: how it knows it)
        private static int Rank(string what) { return what == Saw ? 3 : what == Heard ? 2 : what == Hearsay ? 1 : 0; }

        private sealed class Tale { public string Id = "", At = "", Saw = "", Heard = "", Hearsay = "", Chapter = "", Saga = "", Rumor = ""; public readonly Dictionary<string, string> As = new Dictionary<string, string>(); public float Near = 30f; public List<Cond> When, Known; }
        private static List<Tale> _all;
        internal static readonly List<string> Errors = new List<string>();

        private static List<Tale> All { get { if (_all == null) Load(); return _all; } }
        internal static void Reload() { _all = null; }

        private static void Load()
        {
            StoryTrackConds.Register(); // (its «when:» is conditions: known before they are read — a person's tick may ask first)
            _all = new List<Tale>(); Errors.Clear();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false))
            {
                Tale t = null;
                foreach (KeyValuePair<int, string> l in StoryFiles.Lines(f.Value))
                {
                    string line = l.Value, at = f.Key + ":" + l.Key + ": ";
                    if (line.StartsWith("[tale ") && line.EndsWith("]"))
                    {
                        string id = line.Substring(6, line.Length - 7).Trim();
                        t = Story.GoodName(id) ? new Tale { Id = id } : null;
                        if (t == null) Errors.Add(at + "погана назва"); else _all.Add(t);
                        continue;
                    }
                    if (t == null) continue;
                    int c = line.IndexOf(':');
                    if (c <= 0) { Errors.Add(at + "рядок без «:»"); continue; }
                    string key = line.Substring(0, c).Trim(), val = line.Substring(c + 1).Trim();
                    if (key.StartsWith("as ", StringComparison.Ordinal)) { t.As[key.Substring(3).Trim()] = val; continue; }
                    switch (key)
                    {
                        case "when": t.When = StoryCond.Parse(val, Errors); break;
                        case "known": t.Known = StoryCond.Parse(val, Errors); break;
                        case "at":
                            {
                                string[] w = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                t.At = w.Length > 0 ? w[0] : "";
                                float m; if (w.Length > 1 && float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m)) t.Near = m;
                                string why = t.At.Length > 0 ? StoryWhere.Why(t.At) : "нема місця";
                                if (why != null) Errors.Add(at + why);
                                break;
                            }
                        case "saw": t.Saw = More(t.Saw, val); break;
                        case "heard": t.Heard = More(t.Heard, val); break;
                        case "hearsay": t.Hearsay = More(t.Hearsay, val); break;
                        case "chapter": t.Chapter = val; break;
                        case "saga": t.Saga = val; break;
                        case "rumor": t.Rumor = val; break;
                        default: Errors.Add(at + "невідоме «" + key + "»"); break;
                    }
                }
            }
            foreach (Tale t in _all) if (t.When == null || t.At.Length == 0 || t.Saw.Length == 0) Errors.Add(t.Id + ": треба when, at, saw");
            _all.RemoveAll(t => t.When == null || t.At.Length == 0 || t.Saw.Length == 0);
            foreach (Tale t in _all)
            {
                if ((t.Saga.Length > 0 || t.Rumor.Length > 0) && t.Chapter.Length == 0) Errors.Add(t.Id + ": є saga:/rumor:, а chapter: нема");
                else if (t.Chapter.Length > 0 && !StorySaga.HasChapter(t.Chapter)) Errors.Add(t.Id + ": chapter: нема розділу «" + t.Chapter + "» (assets/story/saga)");
                if (t.Rumor.Length > 0 && t.Saga.Length == 0) Errors.Add(t.Id + ": rumor: без saga: (чутку ніхто не розвіє)");
            }
            foreach (string e in Errors) Story.Log("tales: " + e);
            Story.Log("tales: " + _all.Count + " loaded" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
        }

        private static string Key(Tale t) { return "tale_" + t.Id; }

        // several «saw:» (heard, hearsay) lines: its variants, kept one a line
        private static string More(string had, string line) { return had.Length > 0 ? had + "\n" + line : line; }

        // what this person tells of it: its own level's words, else the next better ones; of several, always the same one for this person and tale
        private static string WordsOf(NpcActor a, Tale t, NpcMemory.Entry e)
        {
            string own;
            if (a != null && a.Def != null && t.As.TryGetValue(a.Def.Id, out own)) return Lang.T(own);
            string[] said = (e.What == Hearsay && t.Hearsay.Length > 0 ? t.Hearsay : e.What != Saw && t.Heard.Length > 0 ? t.Heard : t.Saw).Split('\n');
            uint seed = a != null && a.Zdo != null ? NpcKit.Seed(a.Zdo) : 0u;
            return Lang.T(said[(int)((seed ^ (uint)t.Id.GetStableHashCode()) % (uint)said.Length)]);
        }

        // ---------------------------------------------------------------- the moment, and the players who saw it (every game)
        internal static float Settle = 5f; // s after a world is entered before «not yet» counts (its keys and the character's flags come in with it)
        private static object _world;      // session: the world these looks are in
        private static float _since;
        private static readonly HashSet<string> Watched = new HashSet<string>(); // session: tales seen not yet happened in this world — what turns true now is new
        private static readonly HashSet<string> Written = new HashSet<string>(); // session: tales whose moment this game wrote (once)
        private const string Old = "old";  // (its place's text: it was so before anyone watched — nobody saw it)

        internal static void Look(Player p)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { _world = ZNet.instance; _since = Time.time; Watched.Clear(); Written.Clear(); _knownHere.Clear(); }
            double now = NpcRescue.Now;
            foreach (Tale t in All)
            {
                int at = StoryWorld.Var(Key(t));
                if (at == 0)
                {
                    if (!StoryCond.All(p, t.When)) { Written.Remove(t.Id); _knownHere.Remove(t.Id); if (Time.time - _since >= Settle) Watched.Add(t.Id); continue; } // (not so now: a test's run or the author's wipe took its keys, it may come again)
                    // every game that watches may write the moment, so a real spot always beats «old»: a game just come in (its keys not
                    // read yet) never writes over a spot another game saw; each game writes once (the key comes back from the server later)
                    if (!Written.Add(t.Id)) continue;
                    StoryWorld.SetVar(Key(t), Mathf.Max(1, (int)now));
                    string had = StoryWorld.Text(Key(t) + "_at");
                    if (!Watched.Contains(t.Id))
                    {
                        if (had.Length == 0) { StoryWorld.SetText(Key(t) + "_at", Old); Story.Log("tales: «" + t.Id + "» was so before this game watched — an old moment, nobody saw it"); }
                        continue;
                    }
                    Vector3 where;
                    if ((had.Length == 0 || had == Old) && StoryWhere.Find(t.At, out where)) StoryWorld.SetText(Key(t) + "_at", where.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + where.y.ToString("0.0", CultureInfo.InvariantCulture) + "," + where.z.ToString("0.0", CultureInfo.InvariantCulture)); // (the spot as it was for this game: the same for all after)
                    at = (int)now;
                }
                if (now - at > Witness || Knows(p, t.Id)) continue;
                if (!Near(t, p.transform.position)) continue;
                Learned(p, t.Id); // (he was there: nothing to be told)
                if (t.Saga.Length > 0 && Witnesses(t, StoryWorld.Texts(Key(t) + WitnessKey), null, null) < MaxWitnesses)
                    StoryWorld.SetText(Key(t) + WitnessKey + p.GetPlayerID().ToString(CultureInfo.InvariantCulture), NameKept(p.GetPlayerName())); // (the saga's witness: once, his own key; the first MaxWitnesses only)
            }
            Carry(p);
        }

        // what this player knows of this world's story is the world's, never the character's (the user 2026-11-07: on another server he knows that one's): one
        // text a player, «knows_<his id>» = its tales. Written where it was learnt; held here too, the key comes back from the server a moment later
        private static readonly HashSet<string> _knownHere = new HashSet<string>(); // session: learnt on this game in this world
        private static string KnowsKey(Player p) { return "knows_" + p.GetPlayerID().ToString(CultureInfo.InvariantCulture); }
        private static bool Knows(Player p, string tale)
        {
            if (_knownHere.Contains(tale)) return true;
            string v = StoryWorld.Text(KnowsKey(p));
            return v.Length > 0 && Array.IndexOf(v.Split(','), tale.ToLowerInvariant()) >= 0;
        }
        private static void Learned(Player p, string tale)
        {
            if (Knows(p, tale)) return;
            _knownHere.Add(tale);
            string v = StoryWorld.Text(KnowsKey(p));
            foreach (string k in _knownHere) if (Array.IndexOf(v.Split(','), k.ToLowerInvariant()) < 0) v = v.Length > 0 ? v + "," + k : k;
            StoryWorld.SetText(KnowsKey(p), v);
        }

        // what you know, brought to the people you come near: one within CarryNear who does not know it has heard it from you, and its people
        // have it from it (hearsay). Your game asks (your story's flags are yours); what they keep is written where each is run (NpcMemory.Tell)
        private static float _nextCarry;
        private static readonly HashSet<string> Brought = new HashSet<string>(); // session: «tale person» already settled here — not asked again
        private static void Carry(Player p)
        {
            if (Time.time < _nextCarry) return;
            _nextCarry = Time.time + CarryEvery;
            Vector3 me = p.transform.position;
            foreach (Tale t in All)
            {
                Vector3 spot;
                if (StoryWorld.Var(Key(t)) == 0 || !Where(t, out spot) || !(Knows(p, t.Id) || (t.Known != null && StoryCond.All(p, t.Known)))) continue; // (not yet, an old moment, or you do not know it)
                foreach (NpcActor o in NpcActor.Live)
                {
                    if (o == null || o.Zdo == null || (o.transform.position - me).sqrMagnitude > CarryNear * CarryNear || !NpcResident.Is(o)) continue;
                    string seen = t.Id + " " + NpcKit.Seed(o.Zdo);
                    if (Brought.Contains(seen)) continue;
                    if (Brought.Count > 512) Brought.Clear();
                    Brought.Add(seen);
                    if (NpcMemory.Find(o, Key(t), 0L) != null) continue;
                    NpcMemory.Tell(o, Key(t), 0L, Heard);
                    Npcs.Log(o.Id + ": hears «" + t.Id + "» from " + p.GetPlayerName());
                    foreach (NpcActor h in NpcHeart.Residents(NpcKit.GetRef(o.Zdo, NpcResident.KHeart)))
                        if (h != null && h != o && NpcMemory.Find(h, Key(t), 0L) == null) NpcMemory.Tell(h, Key(t), 0L, Hearsay);
                }
            }
        }

        // where it happened: the spot kept at its moment (a place asked anew may be another one now: «heart.near»); false: not known, or an old moment
        private static bool Where(Tale t, out Vector3 at)
        {
            at = Vector3.zero;
            string s = StoryWorld.Text(Key(t) + "_at");
            if (s == Old) return false;
            string[] w = s.Split(',');
            float x, y, z;
            if (w.Length == 3 && float.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && float.TryParse(w[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) { at = new Vector3(x, y, z); return true; }
            return StoryWhere.Find(t.At, out at); // (its spot not in yet: the place itself)
        }

        private static bool Near(Tale t, Vector3 pos) { Vector3 at; return Where(t, out at) && Vector3.Distance(at, pos) <= t.Near; }

        // ---------------------------------------------------------------- the saga (StorySaga reads these)
        private const string WitnessKey = "_w";
        internal static int MaxWitnesses = 8; // names kept a tale: the world's keys stay few however many players (the book shows 4 and «і ще N»)

        // a tale's witnesses among the world's texts: how many; their names into «names» (but «me», the asking player: «you» then)
        private static int Witnesses(Tale t, List<KeyValuePair<string, string>> texts, string me, Told into)
        {
            string pre = (Key(t) + WitnessKey).ToLowerInvariant(); // (the world keeps its keys in lower case)
            int n = 0;
            foreach (KeyValuePair<string, string> w in texts)
            {
                if (!w.Key.StartsWith(pre, StringComparison.Ordinal) || w.Key.Length == pre.Length || !(char.IsDigit(w.Key[pre.Length]) || w.Key[pre.Length] == '-')) continue; // («gate_w…» of a tale «gate», not of «gate_wall»)
                n++;
                if (into == null) continue;
                if (me != null && w.Key == pre + me) into.You = true; else into.Witnesses.Add(NameBack(w.Value));
            }
            return n;
        }

        // a player's name in a world key: the game lowers the whole key, its text too («Олег» came back «олег»), so a capital is kept as «^» before
        // its small letter (and «^» itself as «^^»)
        private const char Cap = '^';
        private static string NameKept(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in name ?? "")
            {
                if (c == Cap) sb.Append(Cap).Append(Cap);
                else if (char.IsUpper(c) && char.ToLowerInvariant(c) != c) sb.Append(Cap).Append(char.ToLowerInvariant(c));
                else sb.Append(c);
            }
            return sb.ToString();
        }
        private static string NameBack(string kept)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < kept.Length; i++)
            {
                if (kept[i] == Cap && i + 1 < kept.Length) { i++; sb.Append(kept[i] == Cap ? Cap : char.ToUpperInvariant(kept[i])); }
                else sb.Append(kept[i]);
            }
            return sb.ToString();
        }

        internal sealed class Told
        {
            public string Chapter = "", Saga = "", Rumor = "";
            public int Moment;                               // the world's second it happened (0: not yet)
            public bool Known;                               // this player saw it, was told, or knows it anyway («known:»)
            public readonly List<string> Witnesses = new List<string>(); // the other players who saw it (their names, as the world keeps them)
            public bool You;                                 // this player is one of them
        }

        // every tale with a saga entry, happened or not, in the files' order (the caller sorts by moment); its witnesses only when asked
        internal static List<Told> Saga(Player p, bool witnesses)
        {
            List<Told> l = new List<Told>();
            List<KeyValuePair<string, string>> seen = witnesses ? StoryWorld.Texts("tale_") : null;
            foreach (Tale t in All)
            {
                if (t.Saga.Length == 0) continue;
                Told e = new Told { Chapter = t.Chapter, Saga = t.Saga, Rumor = t.Rumor, Moment = StoryWorld.Var(Key(t)) };
                e.Known = e.Moment != 0 && (Knows(p, t.Id) || (t.Known != null && StoryCond.All(p, t.Known)));
                if (seen != null && e.Moment != 0) Witnesses(t, seen, p.GetPlayerID().ToString(CultureInfo.InvariantCulture), e);
                l.Add(e);
            }
            return l;
        }

        // ---------------------------------------------------------------- the people who saw it (where a person is run)
        private static readonly Dictionary<NpcActor, float> Next = new Dictionary<NpcActor, float>(); // session: its next look
        internal static void Tick(NpcActor a)
        {
            float next;
            if (Next.TryGetValue(a, out next) && Time.time < next) return;
            Next[a] = Time.time + LookEvery;
            if (All.Count == 0) return;
            double now = NpcRescue.Now;
            foreach (Tale t in All)
            {
                int at = StoryWorld.Var(Key(t));
                if (at == 0) continue;
                if (now - at > Witness) { Ring(a, t, now - at); continue; }
                NpcMemory.Entry had = NpcMemory.Find(a, Key(t), 0L);
                if ((had != null && had.What == "saw") || !Near(t, a.transform.position)) continue; // (one told by a neighbour first and there itself: seen, not heard)
                NpcMemory.Remember(a, Key(t), 0L, "saw");
                Vector3 where; if (Where(t, out where)) NpcFeel.Feel(a, NpcFeel.Awe, 60f, where + Vector3.up, true); // (it saw it: its eyes on it, its head up a while)
                foreach (NpcActor o in NpcHeart.Residents(NpcKit.GetRef(a.Zdo, NpcResident.KHeart))) // (those it lives with, from its mouth: not one who knows it already, nor one there that sees it itself)
                    if (o != null && o != a && NpcMemory.Find(o, Key(t), 0L) == null && !Near(t, o.transform.position)) NpcMemory.Tell(o, Key(t), 0L, "heard");
            }
        }

        // its ring: a resident whose home lies inside it has it as hearsay (once; its memory read only inside the ring)
        private static readonly HashSet<string> Ringed = new HashSet<string>(); // session: «tale person» that knows it already — not read again
        private static void Ring(NpcActor a, Tale t, double since)
        {
            if (!NpcResident.Is(a)) return;
            string seen = t.Id + " " + NpcKit.Seed(a.Zdo);
            if (Ringed.Contains(seen)) return;
            Vector3 spot;
            if (!Where(t, out spot)) return; // (an old moment: it went nowhere)
            float day = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 1800f;
            float reach = Mathf.Min(SpreadMost, SpreadPerDay * (float)(since / day));
            if (Utils.DistanceXZ(NpcRules.Home(a), spot) > reach) return;
            if (Ringed.Count > 512) Ringed.Clear();
            Ringed.Add(seen);
            if (NpcMemory.Find(a, Key(t), 0L) != null) return;
            NpcMemory.Remember(a, Key(t), 0L, Hearsay);
            Npcs.Log(a.Id + ": the rumour of «" + t.Id + "» has reached it (" + Mathf.RoundToInt(Utils.DistanceXZ(NpcRules.Home(a), spot)) + " m off)");
        }

        internal static void Forget(NpcActor a) { Next.Remove(a); }

        // ---------------------------------------------------------------- in a talk
        // a tale this person remembers and this player does not know: seen ones first
        private static Tale Untold(NpcActor a, Player p, out NpcMemory.Entry e)
        {
            e = null;
            if (a == null || p == null) return null;
            Tale best = null;
            foreach (Tale t in All)
            {
                NpcMemory.Entry m = NpcMemory.Find(a, Key(t), 0L);
                if (m == null || Knows(p, t.Id) || (t.Known != null && StoryCond.All(p, t.Known))) continue;
                if (best == null || Rank(m.What) > Rank(e.What)) { best = t; e = m; } // (the best known first: seen, heard, hearsay)
            }
            return best;
        }

        private static string _shown = ""; // session: the tale the window shows now (its deed marks it known)

        internal static void Register()
        {
            StoryCond.Register("taleuntold", new StoryCond.Kind { Usage = "taleuntold   (людина, з якою говориш, пам'ятає подію, якої ти не знаєш)",
                Test = (p, a) => { NpcMemory.Entry e; return Untold(NpcTalk.Him, p, out e) != null; } });
            NpcTalk.Words["{tale}"] = (a, p) =>
            {
                NpcMemory.Entry e;
                Tale t = Untold(a, p, out e);
                _shown = t != null ? t.Id : "";
                if (t == null) return Lang.T("Та нічого особливого.");
                return WordsOf(a, t, e);
            };
            NpcTalk.Deeds["taletold"] = (a, p, w) => { if (_shown.Length > 0) Learned(p, _shown); };
            // the settlers' «Що тут сталося, поки мене не було?» (NpcAway): a tale first, then what else it remembers of your absence
            NpcAway.Sources.Add(new NpcAway.Source
            {
                Line = (a, p) => { NpcMemory.Entry e; Tale t = Untold(a, p, out e); _shown = t != null ? t.Id : ""; return t == null ? null : WordsOf(a, t, e); },
                Told = (a, p) => { if (_shown.Length > 0) Learned(p, _shown); }
            });
        }
    }
}
#endif

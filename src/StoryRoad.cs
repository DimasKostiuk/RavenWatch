#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // A person who talks to you on the way (the user 2026-11-18, chapter 2: after the night on the tower Torvald goes home to the Hall, and
    // «якщо ми в певному радіусі від нпс тоді він буде з нами говорити» — who walks with him hears a few talks, who does not hears nothing).
    // A stage's line:
    //   road: <who> <m> <talks>    while the stage stands and this game's player is within m of that person (a story person's template id),
    //                              the talks of «[road <talks>]» (assets/story/meets, NpcChatter's format, read there: NpcChatter.Road) are
    //                              said one after another in the order written, each once a character (his flag «road_<talks>_<first line's
    //                              hash>»: a talk written in between later is new, the heard ones stay heard; its first line rewritten — new;
    //                              two talks of a block never begin alike — storycheck):
    //                                <who>: <words>   the person, over its head — «{@theme}» a made line of its own words, as in a gathering's talk;
    //                                                 another person of the story may have lines in a talk too, one a talk (the user 2026-11-18, the
    //                                                 night on the tower: the stranger «говорить якісь свої фрази, а не просто… мовчки з нами б'ється») —
    //                                                 such a talk is begun only while that one is within m of him and free (With), and dropped unheard if
    //                                                 that one is gone half way (it comes again from its first line)
    //                                hero: <words>    the player himself, as a scene's «player say» (StoryScenes.HeroLine: his subtitle and his hum)
    //                                ? <conditions>   (before a talk) only while they hold (StoryCond: the player's and the world's, «he» the person)
    // Each player hears his own: run on his game, for him alone — the person's lines over its head here only (NpcActor.SayHere: its bubble
    // and its head turned to him, no message, no gesture — a gesture would go to every game), the hero's on his screen — two walking with it
    // each hear theirs, and nothing is sent or kept in the world. The person is free to talk by what every game knows (Free: no talk or fight
    // holds it on any game, no scene holds it, no other line of its own or of someone near on the floor), and he: no scene plays here, his talk
    // window is shut, he is calm (StoryCond «calm»: no foe at him, not fighting — the night on the tower: it talks between the waves). He drops
    // back beyond m (or it is not free, or a fight is on): the talk stops after the line being said; within m again — first a word of
    // the person's own theme «road_back» («Так от, про що я…», assets/story/words — storycheck asks it of every person a road names), then on
    // from the line it stopped at. The first talk FirstAfter s after he comes near, the next GapMin…GapMax s after one ends; a talk is heard
    // the moment its last line is said. Stage after stage with the same line is one road (the night on the tower: its waits and its fights) —
    // a talk goes on over them; another line, character or world: all anew; a relog mid-talk says that talk again from its start.
    // Cost: twice a second (StoryQuests.Each) the current stages' few words; while a stage has the line, the person looked for among those
    // loaded (StoryScenes.Actor), one distance; nothing to say (all heard, none fits) — asked again only Idle s later.
    internal static class StoryRoad
    {
        internal const string Hero = "hero", BackTheme = "road_back";
        internal static float FirstAfter = 3f, GapMin = 8f, GapMax = 16f, Idle = 5f;

        private static string _key; // session: whose (character, world), the quest and its line the talks are of now (not the stage: the same line on the next stage goes on)
        private static List<Cond> _calm; // (the condition «calm», read once)
        private static string _who = "", _talks = ""; private static float _m; // session: the line's words, read once a key
        private static int _talk = -1, _line; // session: the talk being said (-1: none) · its next line
        private static float _next, _came = -1f, _ownUntil; // session: when a line may come · when he came within reach (-1: he is not) · the floor its own line here holds till
        private static bool _broke; // session: the talk stopped half way — a word of «road_back» first

        internal const string Usage = "road: <хто> <м> <розмови>";

        // the stage line's own check (StoryQuestsRead): null — good
        internal static string Why(string val)
        {
            string[] w = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float m;
            if (w.Length != 3 || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out m) || m <= 0f) return Usage;
            if (!Story.GoodName(w[0]) || !Story.GoodName(w[2])) return Usage + ": погана назва";
            return null;
        }

        internal static string Flag(string talks, NpcChatter.Chat c) { return "road_" + talks + "_" + ((uint)c.Said[0].GetStableHashCode()).ToString("x8"); }

        internal static void Look(Player p)
        {
            if (p == null) return;
            string road = null, key = null;
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                StoryQuests.StageDef s = StoryQuests.StageNow(p, q);
                if (s != null && s.Road.Length > 0) { road = s.Road; key = p.GetPlayerID() + " " + (ZNet.instance != null ? ZNet.instance.GetInstanceID() : 0) + " " + q.Id + " " + s.Road; break; }
            }
            if (key != _key) // (another character, world or road, or none: whatever was being said is over)
            {
                _key = key; _talk = -1; _line = 0; _broke = false; _came = -1f; _next = _ownUntil = 0f;
                if (road != null) { string[] w = road.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); _who = w[0]; _m = float.Parse(w[1], CultureInfo.InvariantCulture); _talks = w[2]; } // (checked as it was read: Why)
            }
            if (road == null) return;
            NpcActor a = StoryScenes.Actor(_who, p.transform.position);
            if (_calm == null) _calm = StoryCond.Parse("calm", new List<string>());
            List<NpcChatter.Chat> talks = NpcChatter.Road(_talks);
            string other = _talk >= 0 && _talk < talks.Count ? Other(talks[_talk]) : null;
            NpcActor withNow = other != null ? StoryScenes.Actor(other, p.transform.position) : null; // (the other one of the talk being said: its floor is this talk's own)
            if (a == null || StoryWhere.Apart(a.transform.position, p.transform.position) > _m || !Free(a, withNow) || !StoryCond.All(p, _calm))
            {
                if (_talk >= 0 && _line > 0) _broke = true; // (gone off half way: back — «Так от, про що я…»)
                _came = -1f;
                return;
            }
            if (_came < 0f) _came = Time.time;
            if (Time.time < _next || Time.time - _came < FirstAfter) return;
            if (_talk < 0)
            {
                _talk = Next(p, a, talks);
                if (_talk < 0) { _next = Time.time + Idle; return; } // (all heard, or none fits now: not asked again at once)
                _line = 0; _broke = false;
            }
            NpcChatter.Chat c = talks[_talk];
            if (_broke)
            {
                string back = Say(a, "{@" + BackTheme + "}", p);
                if (back != null) { _broke = false; _next = Time.time + Seconds(a, back, back) + NpcSpeech.Think(back); return; }
                if (!NpcPhrases.Has(a, BackTheme)) _broke = false; // (no such words of its own: on without them; held quiet this moment — the word at the next look)
                else return;
            }
            string by = c.By != null && _line < c.By.Length ? c.By[_line] : _who, line = c.Said[_line];
            float pause;
            if (by == Hero) pause = StoryScenes.HeroLine(p, line, true, out line);
            else
            {
                NpcActor s = by == _who ? a : Here(by, p);
                if (s == null) { _talk = -1; _next = Time.time + Idle; return; } // (the other one gone half way: the talk dropped unheard — again from its first line)
                if (s != a && !Free(s, a)) return; // (not free this moment: the same line at the next look)
                string said = Say(s, line, p);
                if (said == null) return; // (held quiet this moment: the same line at the next look)
                pause = Seconds(s, line, said);
                line = said;
            }
            _line++;
            if (_line < c.Said.Length) { _next = Time.time + pause + NpcSpeech.Think(line); return; } // (the next one thinks a moment before it answers, as in any talk)
            Story.SetFlag(p, Flag(_talks, c), true); // (heard the moment its last line is said: run off now, it is not said again)
            _talk = -1;
            _next = Time.time + pause + UnityEngine.Random.Range(GapMin, GapMax);
        }

        // the first talk not heard yet whose conditions hold now («he» the person: NpcTalk.Speaker); -1 none
        private static int Next(Player p, NpcActor a, List<NpcChatter.Chat> all)
        {
            NpcActor was = NpcTalk.Speaker;
            NpcTalk.Speaker = a;
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (Story.Flag(p, Flag(_talks, all[i])) || !StoryCond.All(p, all[i].When)) continue;
                    string o = Other(all[i]);
                    NpcActor with = o != null ? Here(o, p) : null;
                    if (o != null && (with == null || !Free(with, a))) continue; // (its other one not here, or not free: a talk of its own first)
                    return i;
                }
            }
            finally { NpcTalk.Speaker = was; }
            return -1;
        }

        // the other person of a talk (not the road's own, not the hero): null — none
        private static string Other(NpcChatter.Chat c)
        {
            if (c.By == null) return null;
            foreach (string b in c.By) if (b != _who && b != Hero) return b;
            return null;
        }

        // that person, loaded and within the road's m of him (null: not here)
        private static NpcActor Here(string who, Player p)
        {
            NpcActor o = StoryScenes.Actor(who, p.transform.position);
            return o != null && StoryWhere.Apart(o.transform.position, p.transform.position) <= _m ? o : null;
        }

        // its line, made of its own words, for this player alone (null: nothing said)
        private static string Say(NpcActor a, string line, Player p)
        {
            string said = a.SayHere(a.Form(NpcPhrases.Inline(a, Lang.TP(line), p)), p);
            if (said != null) _ownUntil = a.FloorUntil; // (the floor its line holds here: its own, not someone else's word)
            return said;
        }

        private static float Seconds(NpcActor a, string line, string said)
        {
            return Mathf.Max(RavenSpeech.Seconds(line, RavenSpeech.Pace.Chatter), NpcSpeech.Seconds(a, said)); // (as a gathering's talk: not over its own words)
        }

        // may it talk to him now, by what every game knows (the review 2026-11-18: its mind's sleep, its scene plan, its talk of two are known only
        // where it is run): no talk or fight holds it, not down or in its bed (NpcVoice.FreeAnywhere — its own words), no scene holds it on any
        // game (StoryScenes.Holder), no line on the floor near it but this one's own here (another person's, or its own that came from another
        // game: a gathering's talk); and here no scene plays and his talk window is shut
        // with: the other one of the talk being said — its line on the floor is this talk's own, not someone else's
        private static bool Free(NpcActor a, NpcActor with)
        {
            if (!NpcVoice.FreeAnywhere(a, true) || StoryScenes.Holder(a) != 0L || NpcChatter.IsTalking(a)) return false;
            if (!NpcActor.FloorFree(a.transform.position, a, with) || a.FloorUntil > _ownUntil + 0.05f) return false;
            return StoryScenes.Playing.Length == 0 && NpcTalk.With == null;
        }
    }
}
#endif

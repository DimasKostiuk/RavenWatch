#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A settler's nature (the user 2026-11-07: «характерів більше»): one of a few kinds of person, so that people differ in what they do, not only
    // in how soon they do it. One row of All below; a new nature is a new row (and its words: themes nature_<id>…, NpcMoreNaturePatterns).
    //   who         from its seed, the same on every game after any restart: nothing is sent, nothing is kept (Born; a written «persona: nature=<id>» or
    //               the story's «trait nature <id>» wins). Only settlers get one by birth; a person written by hand has none (NpcPersona.Nature null)
    //   its traits  ranges over the ones it rolled (Shape), so all that already reads NpcPersona (flight, whims, work, words, poses, places) follows by itself
    //   its fight   Manner: the one answer to «how does this person fight», a settler's by its nature (plain: by its bravery), one written by hand by its
    //               template — or its story's «fight: … when …» line now (StoryFight: Eyvind from the fight for Sverre); Fighter: it goes out to
    //               a raid. Guard.Is stays what it was: a guard by its job, or by its template
    //   its work    WontTake, asked through NpcWorker.WhyNot by the job's button and by SetJob: a nature's own refusals (Wont), then what is true of
    //               anyone: its nerve, its age, a lame leg, its pride; good terms with the player change nothing. What its hands know (a knack of its
    //               story) outweighs a mere dislike, not a fear or a body that cannot. Words steady for one person and job: the menu asks each frame
    //   its day     × a whim's weight (Whim), + a place's worth (Place), × a pose's worth (Pose), × a group of words (Lean), its tones (Tones), its gait
    //               (Gait), its own short moves at a place (Moves: NpcPlaces.Now), the evening's work of a hard worker (WorksLate). Small tables made once a nature: no search, nothing dear
    //   its people  how well two natures get on (Match, in NpcAttitude.Fit); a bully teasing the weak with its fist in its palm (NpcAttitude.Tick,
    //               quarrel_teased); a caring one soothing one frightened or grieving near it (Comfort, from NpcFeel.React); a nature's own move as
    //               a feeling comes (Reacts: the coward shivers instead of a start back)
    //   its words   themes nature_<id> (and _fought _fled _work _evening _build when they fit: Themes, a source of NpcVoice), «?nature <id>» in any theme
    //   its talk    the gifts it likes (Likes: NpcTalkGift), how a way of talking it round works on it (Sway, SwayBy: NpcTalkSway; the best one a chip on its card),
    //               how it keeps its talk — its door, its own first, its grudge (Talk, TalkBy: NpcTalkDoor; a chip on its card),
    //               how it takes a kind word by its air — soft, blunt, a joke (Takes, TakesBy: NpcTalkTone; the best one a chip on its card)
    // Tune: the rows. Test: raven npc lever <who> trait nature <id>; raven npc self shows it; its card says it.
    internal sealed class NpcNature
    {
        internal string Id = "", Word = "", Tip = "", Fight = "", Traits = "", Whims = "", Places = "", Poses = "", Leans = "", Tone = "", Gait = "", Wont = "";
        internal string Moves = ""; // its own short moves standing at a place («a|b»: clips that end by themselves), now and then over the place's own (NpcPlaces.Now)
        internal string Tasks = ""; // × how like it to ask a small task of this kind (NpcTasks): «word=0.3», none: as anyone
        internal string Reacts = ""; // its own move the moment a feeling comes («fright:shiver»), over NpcFeel.Moves
        internal string Likes = ""; // × how much it likes a kind of gift (NpcTalkGift: drink meat fish bread sweet mushroom flower shiny trophy), none: as anyone
        internal string Sway = "";  // × how a way of talking it round works on it (NpcTalkSway: pride gain fear friend duty), none: by its traits (SwayBy)
        internal string Talk = "";  // × how it keeps its talk (NpcTalkDoor): door — how soon it shuts it to you, insist — how soon its own comes first, grudge — how long it holds an unheard worry; none: by its ways (TalkBy)
        internal string Takes = ""; // × how it takes a kind word said in one air (NpcTalkTone: soft blunt joke), none: by its tones (TakesBy). At or below NpcTalk.TakeMiss the word misses: no warmth, and it says so
        internal float Share;
        internal bool Late; // (works its evening too)

        private Dictionary<string, float> _whims, _places, _poses, _leans, _tasks, _likes, _sway, _talk, _takes;
        private Dictionary<string, string> _wont, _reacts;
        private string[] _themes; private int _index = -1; // (its words' theme names, made once · its place in All, for the pairs table)
        internal bool Plain { get { return Id == "plain"; } }

        internal static readonly List<NpcNature> All = new List<NpcNature>
        {
            new NpcNature { Id = "plain", Share = 0.35f },
            new NpcNature { Id = "bully", Takes = "blunt=1.4 joke=1.2 soft=0.5", Talk = "door=1.2 insist=1.3 grudge=1.3", Likes = "trophy=3 meat=2 drink=1.5 sweet=0.5 flower=0.2", Sway = "pride=1.8 gain=1 friend=0.7 duty=0.6 fear=0.4", Tasks = "word=0.2 sit=0.2 calm=0.1 look=0.1 beast=0.2 feed=0.4 greet=0.4 mend=0.6 listen=0.5 fire=0.5 light=0.4 hearth=0.7 raven=1.2 trophy=2", Moves = "fire_proud_l|fire_proud_r|dust_off|knuckle_crack", Share = 0.07f, Word = Lang.K("задира"), Tip = Lang.K("Лізе в бій першим|першою і чіпляється до слабших."), Fight = "defend", Gait = "soldier",
                Traits = "bravery=0.75-0.95 chatty=0.5-0.85 laziness=0.3-0.6",
                Whims = "stretch=3 gaze=1.5 tend=0.4 mend=0.5", Places = "edge=0.45 person=0.3 scar=0.4 trophy=0.35 stores=-0.2", Poses = "hands_belt=3 hand_belt=2 arms_cross=1.5", Leans = "fear=0.4 others=1.5",
                Wont = "cook:pride gatherer:pride cleaner:pride servant:house keeper:pride" },
            new NpcNature { Id = "caring", Takes = "soft=1.5 joke=1 blunt=0.7", Talk = "door=0.6 insist=0.7 grudge=0.6", Likes = "flower=2.5 sweet=2 bread=1.5 trophy=0.3", Sway = "friend=1.8 duty=1.5 pride=0.6 gain=0.5 fear=0.2", Tasks = "word=1.6 feed=2.5 greet=2 sit=1.3 calm=0.8", Moves = "fire_nod_l|fire_nod_r", Share = 0.08f, Word = Lang.K("турботливий|турботлива"), Tip = Lang.K("Захищає своїх і дбає про кожного в оселі."), Fight = "defend",
                Traits = "bravery=0.5-0.8 chatty=0.6-0.9 diligence=0.5-0.8",
                Whims = "tend=3 mend=1.6 guest=0.5", Places = "person=0.6 table=0.25 heart=0.25 edge=-0.2", Poses = "hands_back=1.5 sit_hands=1.5", Leans = "people=1.5 complaint=0.7",
                Wont = "miner:dislike" },
            new NpcNature { Id = "coward", Takes = "soft=1.5 joke=0.8 blunt=0.5", Talk = "door=0.8 insist=0.6 grudge=0.8", Likes = "sweet=2 bread=1.5 trophy=0.2", Sway = "fear=1.7 friend=1.3 gain=1 duty=0.8 pride=0.3", Tasks = "word=1.6 calm=2.5 look=2.5 beast=2.5 sit=1.3 light=1.5 hearth=1.5 greet=0.6 fire=0.8", Reacts = "fright:shiver", Moves = "look_search|fire_sheepish_l|fire_sheepish_r", Share = 0.08f, Word = Lang.K("боягуз|боягузка"), Tip = Lang.K("Тікає першим|першою, а потім соромиться цього."), Fight = "flee", Gait = "meek",
                Traits = "bravery=0.05-0.2 curiosity=0.4-0.8",
                Whims = "gaze=0.5 guest=0.3 wash=0.6", Places = "edge=-0.6 scar=-0.6 heart=0.35 table=0.2 person=0.2", Poses = "hug_self=2 hands_belt=0.2 hand_belt=0.2 ground_knee=1.5", Leans = "fear=1.8",
                Wont = "guard:fear lumber:fear miner:fear keeper:fear" },
            new NpcNature { Id = "lazy", Takes = "joke=1.4 soft=1.1 blunt=0.6", Talk = "door=1.1 insist=0.8 grudge=0.7", Likes = "sweet=2.5 drink=2 bread=1.3 shiny=0.6", Sway = "gain=1.8 friend=1 fear=0.8 pride=0.7 duty=0.3", Tasks = "fire=2 hearth=1.6 light=1.2 fireside=1.5 beast=1.3 look=0.5 feed=0.3 greet=0.3", Moves = "yawn|scratch_head|stretch_up|nose_rub", Share = 0.08f, Word = Lang.K("ледар|ледарка"), Tip = Lang.K("За будь-якої нагоди кидає роботу й лягає відпочити."), Fight = "flee", Gait = "waddle",
                Traits = "laziness=0.75-0.95 diligence=0.1-0.3 bravery=0.25-0.5",
                Whims = "rest=2.5 nap=3 fire=1.5 mend=0.15 tend=0.3 break=2", Places = "seat=0.6 fire=0.35 table=0.25 stores=-0.4 craft=-0.4 roam=-0.2", Poses = "ground_back=2.5 sit_elbows=1.5 hands_back=0.4", Leans = "complaint=1.4 work=0.5",
                Wont = "guard:lazy smelter:lazy lumber:lazy miner:lazy cleaner:lazy servant:lazy cook:lazy" },
            new NpcNature { Id = "loner", Takes = "blunt=1.4 soft=0.6 joke=0.5", Talk = "door=1.5 insist=0.7 grudge=1", Likes = "fish=2.5 mushroom=2 shiny=0.6 drink=0.5", Sway = "duty=1.4 gain=1.2 pride=0.9 fear=0.5 friend=0.4", Tasks = "word=0.3 fireside=0.1 greet=0.1 news=0.2 feed=0.3 listen=0.3 sit=0.4 look=0.6 stars=1.5", Moves = "turn_away|turn_away|shrug_small|nose_rub", Share = 0.07f, Word = Lang.K("самітник|самітниця"), Tip = Lang.K("Тримається осторонь, говорить мало."), Fight = "flee", Gait = "meek",
                Traits = "chatty=0.05-0.2 curiosity=0.2-0.5 bravery=0.3-0.6",
                Whims = "gaze=2.5 guest=0.1 wash=1.5", Places = "edge=0.6 roam=0.35 table=-0.7 person=-0.7 heart=-0.3 fire=-0.25", Poses = "arms_cross=2.5 hands_back=1.5", Leans = "people=0.3 others=0.5 news=0.5",
                Wont = "servant:house cook:dislike" },
            new NpcNature { Id = "nosy", Takes = "joke=1.4 soft=1 blunt=0.8", Talk = "door=0.6 insist=0.9 grudge=0.8", Likes = "shiny=3 trophy=1.5 mushroom=1.2", Sway = "gain=1.6 friend=1.3 pride=0.8 duty=0.7 fear=0.6", Tasks = "news=2.5 raven=2.5 look=1.5 trophy=1.5 greet=1.5", Moves = "listen_in|listen_in|point_at|scratch_head|shade_eyes", Share = 0.07f, Word = Lang.K("пронира"), Tip = Lang.K("Усюди заглядає і все про всіх знає."), Fight = "flee",
                Traits = "curiosity=0.85-1 chatty=0.6-0.9 bravery=0.25-0.5",
                Whims = "gaze=1.5 guest=1.8", Places = "watch=0.7 trophy=0.5 stores=0.45 ravens=0.45 craft=0.3 person=0.35", Poses = "hand_belt=1.5", Leans = "news=1.8 others=1.5",
                Wont = "miner:dislike lumber:dislike smelter:dislike" },
            new NpcNature { Id = "worker", Takes = "blunt=1.5 joke=0.8 soft=0.7", Talk = "door=1 insist=1.1 grudge=1", Likes = "meat=2 bread=2 fish=1.3 flower=0.4 shiny=0.4", Sway = "duty=1.8 pride=1.2 friend=0.9 gain=0.8 fear=0.4", Tasks = "fireside=0.3 listen=0.4 sit=0.5 fire=0.5 news=0.5 light=0.8 trophy=0.8", Moves = "wipe_brow|back_ache|neck_stretch|knuckle_crack", Share = 0.08f, Word = Lang.K("трудяга"), Tip = Lang.K("Працює довше за інших і не терпить ледарів."), Fight = "wary", Late = true,
                Traits = "diligence=0.85-1 laziness=0.02-0.1 bravery=0.4-0.7 chrono=-1.5--0.5",
                Whims = "rest=0.3 fire=0.5 guest=0.2 gaze=0.5 mend=2.5 tend=2", Places = "craft=0.5 stores=0.45 heart=0.15 seat=-0.3 table=-0.1 roam=-0.3", Poses = "hands_belt=1.5 ground_back=0.3", Leans = "work=1.8 complaint=0.7",
                Wont = "servant:house" },
            new NpcNature { Id = "reveller", Takes = "joke=1.6 soft=1 blunt=0.7", Talk = "door=0.7 insist=1 grudge=0.6", Likes = "drink=3 meat=1.5 sweet=1.3 mushroom=0.5", Sway = "friend=1.8 gain=1.2 pride=1 fear=0.5 duty=0.4", Tasks = "word=1.3 fireside=2.5 trophy=1.8 greet=1.6 listen=1.5 news=1.5 sit=1.2", Moves = "toast|toast|drink|talk_hands|fire_laugh_l|fire_laugh_r", Share = 0.07f, Word = Lang.K("гуляка"), Tip = Lang.K("Шукає компанію, вечори проводить біля вогню."), Fight = "wary", Gait = "spry",
                Traits = "chatty=0.85-1 laziness=0.45-0.7 diligence=0.25-0.5 bravery=0.45-0.7 chrono=0.5-1.5",
                Whims = "fire=2.5 guest=1.6 mend=0.4", Places = "table=0.6 fire=0.55 person=0.55 edge=-0.4 stores=-0.3", Poses = "ground_back=1.5 hand_belt=1.5", Leans = "joy=1.6 people=1.4 others=1.3",
                Wont = "guard:dislike miner:dislike smelter:dislike servant:house cleaner:lazy" },
            new NpcNature { Id = "grumbler", Takes = "blunt=1.5 soft=0.5 joke=0.6", Talk = "door=1.3 insist=1.6 grudge=1.5", Likes = "bread=2 meat=1.5 shiny=0.3 flower=0.3", Sway = "duty=1.4 gain=1.3 pride=0.9 friend=0.6 fear=0.5", Tasks = "word=0.6 sit=0.6 fireside=0.7 fire=1.3 light=1.3 hearth=1.4", Moves = "wave_off|shrug_small|arms_open|nose_rub", Share = 0.05f, Word = Lang.K("буркун|буркотуха"), Tip = Lang.K("Завжди чимось невдоволений|невдоволена, але робить, що треба."), Fight = "wary", Tone = "бурч",
                Traits = "chatty=0.45-0.7 diligence=0.5-0.75 bravery=0.4-0.7 laziness=0.2-0.45",
                Whims = "mend=1.3 gaze=0.7", Places = "stores=0.25 person=0.1", Poses = "arms_cross=2 sit_elbows=1.5", Leans = "complaint=2 joy=0.5",
                Wont = "servant:house cleaner:pride" },
        };

        // how well two natures get on: a share of the whole way from the worst-matched pair to the best (either order)
        private static readonly Dictionary<string, float> Pairs = new Dictionary<string, float>
        {
            { "bully+coward", -0.35f }, { "bully+loner", -0.2f }, { "bully+bully", -0.2f }, { "bully+caring", -0.15f }, { "bully+reveller", 0.15f },
            { "worker+lazy", -0.35f }, { "worker+worker", 0.15f }, { "worker+grumbler", 0.1f }, { "worker+reveller", -0.15f },
            { "caring+coward", 0.2f }, { "caring+caring", 0.2f }, { "caring+loner", 0.05f }, { "caring+reveller", 0.15f },
            { "reveller+reveller", 0.25f }, { "reveller+loner", -0.25f }, { "reveller+grumbler", -0.2f }, { "reveller+lazy", 0.15f },
            { "nosy+loner", -0.3f }, { "nosy+nosy", 0.1f }, { "nosy+reveller", 0.15f }, { "grumbler+grumbler", 0.15f }, { "grumbler+lazy", -0.15f },
        };

        // ---------------------------------------------------------------- who it is
        internal static NpcNature Find(string id) { foreach (NpcNature n in All) if (n.Id == id) return n; return null; }

        internal static float H(uint own, string what) // (a steady roll from a person's number: NpcTalkGift's taste too)
        {
            uint h = own ^ (uint)what.GetStableHashCode(); h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
            return (h & 0xFFFFFu) / 1048575f;
        }

        // by its lasting number, apart from the row of its traits: a body rolled before stays the body it was
        internal static NpcNature Born(uint own)
        {
            float roll = H(own, "nature"), sum = 0f;
            foreach (NpcNature n in All) { sum += n.Share; if (roll < sum) return n; }
            return All[0];
        }

        private static bool Number(string s, out float v) { return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v); }

        // its traits within this nature's ranges: «bravery=0.75-0.95», a low end below zero «chrono=-1.5--0.5»
        internal void Shape(uint own, NpcPersona p)
        {
            foreach (string pair in Traits.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('='), dash = pair.IndexOf('-', eq + 2);
                float lo, hi;
                if (eq <= 0 || dash < 0 || !Number(pair.Substring(eq + 1, dash - eq - 1), out lo) || !Number(pair.Substring(dash + 1), out hi)) { Npcs.Log("nature " + Id + ": no range in «" + pair + "»"); continue; }
                string name = pair.Substring(0, eq);
                p.Put(name, Mathf.Lerp(lo, hi, H(own, Id + name)));
            }
        }

        internal static NpcNature Of(NpcActor a) { return a != null ? NpcPersona.Of(a).Nature : null; }
        internal static bool Is(NpcActor a, string id) { NpcNature n = Of(a); return n != null && n.Id == id; }

        // ---------------------------------------------------------------- its fight
        internal static float FightFrom = 0.72f, RunBelow = 0.4f; // a plain one's bravery: from this it stands by its own, below this it runs
        internal static string Manner(NpcActor a)
        {
            if (a == null || a.Def == null) return "flee";
            if (!a.Def.Generated) { string s = StoryFight != null ? StoryFight(a) : null; return string.IsNullOrEmpty(s) ? a.Def.Fight : s; } // (one written by hand: as written — or as its story says now, StoryFight)
            NpcPersona p = NpcPersona.Of(a);
            if (p.Nature == null) return a.Def.Fight; // (no body to read yet: NpcPersona.Average)
            if (p.Nature.Fight.Length > 0) return p.Nature.Fight;
            return p.Bravery >= FightFrom ? "defend" : p.Bravery < RunBelow ? "flee" : "wary";
        }
        internal static bool Fighter(NpcActor a) { string m = Manner(a); return m == "defend" || m == "hunt"; }
        // the story's word on how one of its people fights now (StoryPeople.FightOf: its «fight: … when …» line that holds; null or "" — its template's).
        // Through Manner everything follows it: its weapons taken up (NpcGear.CanFight, its re-dress), its mind's way (StoryPeople.FightTick), its foes (NpcVitals.StoryOwn)
        internal static System.Func<NpcActor, string> StoryFight;

        // what its mind should hold now: on guard it fights, else its manner
        internal static string FightNow(NpcActor a) { return Guard.Hired(a) ? "defend" : Manner(a); }

        // a settler's mind put right where it is owned, at its load and when its ways change: a kept «fight=» or a trait moved under it must not leave
        // the mind saying one thing and its card, its gear and a raid another. Once at a load, once a lever: written only when it differs
        internal static void SyncFight(NpcActor a)
        {
            if (a == null || a.Mind == null || a.Def == null || !a.Def.Generated) return;
            string want = FightNow(a);
            if (a.Mind.Style.ToString().ToLowerInvariant() != want) a.Mind.Do("fight " + want, NpcMind.Layer.Routine);
        }

        // a settler by its nature, a template by its own word: nobody who only runs ever takes up a weapon
        internal static bool MayFight(NpcActor a) { return a != null && a.Def != null && (a.Def.Generated ? Manner(a) != "flee" : Fighter(a)); }

        internal static float BullyLooks = 18f;
        private static float LooksOut(NpcActor a) { return a != null && a.Def != null && a.Def.Generated && Is(a, "bully") ? BullyLooks : 0f; }

        // ---------------------------------------------------------------- its work
        internal static float LazyNoWatch = 0.7f, BraveNoWork = 0.3f, ProudFrom = 0.72f, HouseShare = 0.5f, DislikeShare = 0.12f, SlackFrom = 0.75f;

        // why this settler will not take this job (null: it will). What is true of it, not of how it stands with you
        internal static string WontTake(NpcActor a, string job)
        {
            if (a == null || a.Def == null || !a.Def.Generated || string.IsNullOrEmpty(job)) return null;
            NpcPersona p = NpcPersona.Of(a);
            string said;
            if (p.RefusalsAt == _langNow && p.Refusals != null && p.Refusals.TryGetValue(job, out said)) return said; // (the menu asks every frame)
            string why = null;
            if (p.Nature != null) { if (p.Nature._wont == null) p.Nature._wont = WontOf(p.Nature.Wont); p.Nature._wont.TryGetValue(job, out why); }
            if (why == "dislike" && NpcBackstory.Knack(a, job) > 0f) why = null; // (its hands know this work)
            if (why == null) why = ByWhoItIs(a, p, job);
            said = why != null ? Say(a, job, why) : null;
            if (NpcBackstory.Has(a)) // (its story says what it can, a lame leg, a knack: kept only once that is here)
            {
                if (p.Refusals == null || p.RefusalsAt != _langNow) { p.Refusals = new Dictionary<string, string>(); p.RefusalsAt = _langNow; }
                p.Refusals[job] = said;
            }
            return said;
        }
        private static int _langNow;

        private static string ByWhoItIs(NpcActor a, NpcPersona p, string job)
        {
            NpcSettlers.Look look = NpcSettlers.Of(a);
            bool old = look != null && look.Age == 2;
            uint own = a.Zdo != null ? NpcKit.Seed(a.Zdo) : 1u;
            bool heavy = job == "lumber" || job == "miner", knack = NpcBackstory.Knack(a, job) > 0f; // (a knack from its story: it knows this work)
            if ((job == "guard" || heavy) && (old || NpcBackstory.Fact(a, "кульгає"))) return "weak";
            if (job == "guard" && Manner(a) == "flee") return "fear";
            if (job == "guard" && p.Laziness >= LazyNoWatch) return "lazy";
            if (heavy && p.Bravery < BraveNoWork) return "fear";
            if (job == "smelter" && p.Laziness >= SlackFrom) return "lazy";
            if (job == "servant" && p.Bravery >= ProudFrom) return "house"; // (the bold one bows to nobody)
            if (knack) return null;
            if (job == "servant" && (p.Nature == null || p.Nature.Plain) && H(own, "wont_servant") < HouseShare) return "house"; // (not everyone would keep another's house)
            if ((p.Nature == null || p.Nature.Plain) && job != "servant" && H(own, "wont_" + job) < DislikeShare) return "dislike"; // (each its own)
            return null;
        }

        private static Dictionary<string, string> WontOf(string row)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string pair in row.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) { int c = pair.IndexOf(':'); if (c > 0) d[pair.Substring(0, c)] = pair.Substring(c + 1); }
            return d;
        }

        private static readonly Dictionary<string, string[]> Refuse = new Dictionary<string, string[]>
        {
            { "pride", new[] { Lang.K("Знайди для цього когось іншого. Я до такого не нахилюся."), Lang.K("Я не для того тут вижив|вижила, щоб таким займатися."), Lang.K("Сміятимуться ж усі. Ні."), Lang.K("Дай мені справжню роботу, а це хай роблять інші."), Lang.K("Не моє це діло, і не проси."), Lang.K("Таке нехай роблять ті, хто нічого кращого не вміє.") } },
            { "house", new[] { Lang.K("У чужому домі порядкувати не стану, хоч ми й ладнаємо."), Lang.K("Я не прислуга. Навіть у тебе."), Lang.K("Працювати на оселю — так. Бігати по чужих кімнатах — ні."), Lang.K("Добре, що ми ладнаємо, але слугою я не буду."), Lang.K("Хай твій дім доглядає той, кому це до душі."), Lang.K("Я сюди прийшов|прийшла жити, а не прислуговувати.") } },
            { "fear", new[] { Lang.K("Туди? Ні. Я ще хочу побачити завтрашній день."), Lang.K("Від самої думки ноги підкошуються. Не можу."), Lang.K("Я тебе підведу, щойно щось трапиться. Краще не треба."), Lang.K("Страшно мені. Соромно казати, але страшно."), Lang.K("Хай це робить хтось сміливіший."), Lang.K("Ні, ні й ні. Шукай іншого.") } },
            { "lazy", new[] { Lang.K("Це ж цілий день спину гнути. Ні, дякую."), Lang.K("Надто багато роботи. Дай щось простіше."), Lang.K("А коли ж відпочивати? Ні, це не для мене."), Lang.K("Від самої думки втомлююся."), Lang.K("Я за таке не візьмуся: ні сьогодні, ні завтра."), Lang.K("Хай старається той, кому більше за всіх треба.") } },
            { "weak", new[] { Lang.K("Не ті в мене сили для такого."), Lang.K("Спина й ноги цього вже не витримають."), Lang.K("Хотів|Хотіла б, та тіло не дозволить."), Lang.K("Я б і радий|рада, та це вже не для мого тіла."), Lang.K("Дай роботу, яку я справді подужаю."), Lang.K("Упаду на першому ж дні. Ні.") } },
            { "dislike", new[] { Lang.K("Не лежить душа до цього."), Lang.K("Що завгодно, тільки не це."), Lang.K("Пробував|Пробувала колись. Більше не хочу."), Lang.K("Ні, це не моє."), Lang.K("Візьмуся за щось інше, а це — ні."), Lang.K("Нудьга смертна. Пошукай когось іншого.") } },
        };

        // steady for one person and job (the menu asks every frame), in its forms
        private static string Say(NpcActor a, string job, string why)
        {
            string[] l;
            if (!Refuse.TryGetValue(why, out l)) { Npcs.Log("nature: no words for a refusal «" + why + "»"); return Lang.T("Цієї роботи не візьме."); }
            uint own = a.Zdo != null ? NpcKit.Seed(a.Zdo) : 1u;
            return a.Form(Lang.T(l[(int)(H(own, "say_" + job) * l.Length) % l.Length]));
        }

        // ---------------------------------------------------------------- its day: read from small tables, made once a nature
        private static Dictionary<string, float> Table(ref Dictionary<string, float> kept, string row)
        {
            if (kept != null) return kept;
            kept = new Dictionary<string, float>();
            foreach (string pair in row.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                float v;
                if (eq > 0 && Number(pair.Substring(eq + 1), out v)) kept[pair.Substring(0, eq)] = v;
                else Npcs.Log("nature: no number in «" + pair + "»");
            }
            return kept;
        }

        private static float Read(Dictionary<string, float> t, string key, float none) { float v; return t.TryGetValue(key, out v) ? v : none; }

        // (n: read once by the caller, a choice weighs many: NpcPersona.Of builds strings each time. Null, one with no nature: no change)
        internal static float Whim(NpcNature n, string id) { return n == null ? 1f : Read(Table(ref n._whims, n.Whims), id, 1f); }
        internal static float Place(NpcNature n, string kind) { return n == null ? 0f : Read(Table(ref n._places, n.Places), kind, 0f); }
        internal static float Pose(NpcNature n, string pose) { return n == null ? 1f : Read(Table(ref n._poses, n.Poses), pose, 1f); }
        internal static float Lean(NpcNature n, string group) { return n == null ? 1f : Read(Table(ref n._leans, n.Leans), group, 1f); }
        internal static float Task(NpcNature n, string kind) { return n == null ? 1f : Read(Table(ref n._tasks, n.Tasks), kind, 1f); }
        internal static float Like(NpcNature n, string kind) { return n == null ? 1f : Read(Table(ref n._likes, n.Likes), kind, 1f); }

        // how a way of talking it round works on it (×, 1 as anyone): its nature's row; a plain one, or one written by hand, by its traits
        internal static readonly string[] Ways = { "pride", "gain", "fear", "friend", "duty" };
        internal static float SwayBy(NpcActor a, string way)
        {
            NpcPersona p = NpcPersona.Of(a);
            NpcNature n = p.Nature;
            if (n != null && !n.Plain && n.Sway.Length > 0) return Read(Table(ref n._sway, n.Sway), way, 1f);
            switch (way)
            {
                case "pride": return Mathf.Lerp(0.5f, 1.6f, p.Bravery);
                case "fear": return Mathf.Lerp(1.5f, 0.4f, p.Bravery);
                case "duty": return Mathf.Lerp(0.5f, 1.6f, p.Diligence);
                case "gain": return Mathf.Lerp(0.6f, 1.5f, p.Laziness);
                case "friend": return Mathf.Lerp(0.6f, 1.5f, p.Chatty);
            }
            return 1f;
        }

        // how it keeps its talk (×, 1 as anyone): its nature's row; a plain one by its ways (a chatty one keeps its door open, a grumpy one insists, a timid one hardly)
        internal static float TalkBy(NpcActor a, string key)
        {
            NpcPersona p = NpcPersona.Of(a);
            NpcNature n = p.Nature;
            if (n != null && !n.Plain && n.Talk.Length > 0) return Read(Table(ref n._talk, n.Talk), key, 1f);
            HashSet<string> t = NpcPhrases.Tones(a);
            switch (key)
            {
                case "door": return Mathf.Lerp(1.3f, 0.7f, p.Chatty) * (t.Contains("бурч") ? 1.2f : 1f);
                case "insist": return t.Contains("бурч") ? 1.4f : t.Contains("боязк") ? 0.7f : 1f;
                case "grudge": return t.Contains("бурч") ? 1.3f : t.Contains("весел") ? 0.7f : 1f;
            }
            return 1f;
        }

        // how it takes a kind word by the air it is said in (×, 1 as anyone; the user 2026-11-12: the right answer is by the person, not by the row it stands in): its
        // nature's row; a plain one, or one written by hand, by its tones — a grumpy one wants it short, a timid one gentle, a cheerful one with a laugh
        internal static readonly string[] Airs = { "soft", "blunt", "joke" };
        internal static float TakesBy(NpcActor a, string air)
        {
            NpcPersona p = NpcPersona.Of(a);
            NpcNature n = p.Nature;
            if (n != null && !n.Plain && n.Takes.Length > 0) return Read(Table(ref n._takes, n.Takes), air, 1f);
            HashSet<string> t = NpcPhrases.Tones(a);
            float k = 1f;
            switch (air)
            {
                case "soft": if (t.Contains("бурч")) k *= 0.6f; if (t.Contains("боязк")) k *= 1.4f; if (t.Contains("смілив")) k *= 0.75f; if (t.Contains("стар")) k *= 1.15f; break;
                case "blunt": if (t.Contains("бурч")) k *= 1.3f; if (t.Contains("боязк")) k *= 0.6f; if (t.Contains("весел")) k *= 0.85f; if (t.Contains("смілив")) k *= 1.25f; break;
                case "joke": if (t.Contains("бурч")) k *= 0.7f; if (t.Contains("весел")) k *= 1.4f; if (t.Contains("стар")) k *= 0.85f; if (t.Contains("мол")) k *= 1.2f; break;
            }
            return Mathf.Clamp(k, 0.4f, 1.8f);
        }

        // the air it takes best, for the card («Вдача»: a hint) — null: none stands out
        internal static string BestAir(NpcActor a) { string best = null; float most = 1.2f; foreach (string w in Airs) { float v = TakesBy(a, w); if (v >= most) { most = v; best = w; } } return best; }
        internal static string AirWord(string air) { return air == "blunt" ? Lang.T("пряме слово") : air == "joke" ? Lang.T("жарт") : Lang.T("лагідне слово"); }

        // the way that works best on it, and its word for the card («Вдача»: a hint, not the table)
        internal static string BestWay(NpcActor a) { string best = Ways[0]; float most = -1f; foreach (string w in Ways) { float v = SwayBy(a, w); if (v > most) { most = v; best = w; } } return best; }
        internal static string WayWord(string way)
        {
            switch (way)
            {
                case "pride": return Lang.T("гордістю");
                case "gain": return Lang.T("вигодою");
                case "fear": return Lang.T("страхом");
                case "friend": return Lang.T("дружбою");
                default: return Lang.T("обов'язком");
            }
        }
        // its own move as a feeling comes (null: the common one)
        internal static string Reaction(NpcActor a, string kind)
        {
            NpcNature n = Of(a);
            if (n == null || n.Reacts.Length == 0) return null;
            if (n._reacts == null) n._reacts = WontOf(n.Reacts); // (the same «kind:clip» shape as its refusals, parsed once)
            string clip; return n._reacts.TryGetValue(kind, out clip) ? clip : null;
        }

        // a caring one near sees another frightened or grieving: it turns to it and soothes it (calm_down) — the event's own moment, not a poll
        internal static float ComfortNear = 8f;
        internal static void Comfort(NpcActor hurt)
        {
            if (hurt == null) return;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == hurt || !o.Owned || o.Body == null || o.Mind == null) continue;
                if (Utils.DistanceXZ(o.transform.position, hurt.transform.position) > ComfortNear) continue; // (near first: a raid frightens many at once)
                if (!Is(o, "caring") || !o.Mind.AtLeisure || o.Mind.SeatedNow || !o.Body.ClipFree || !NpcClock.Seen(o)) continue;
                o.Mind.Face(hurt.transform.position);
                if (o.Body.Move("calm_down")) { Npcs.Log(o.Who + ": comforts " + hurt.Who); return; } // (one is enough)
            }
        }

        internal static bool WorksLate(NpcActor a) { NpcNature n = Of(a); return n != null && n.Late; }

        // ---------------------------------------------------------------- its people
        private static float[,] _match;
        internal static float Match(NpcActor a, NpcActor o)
        {
            NpcNature x = Of(a), y = Of(o);
            if (x == null || y == null) return 0f;
            if (_match == null) // (the pairs table made once into a grid: no strings joined on each ask)
            {
                _match = new float[All.Count, All.Count];
                for (int i = 0; i < All.Count; i++) All[i]._index = i;
                for (int i = 0; i < All.Count; i++) for (int j = 0; j < All.Count; j++) { float v; if (Pairs.TryGetValue(All[i].Id + "+" + All[j].Id, out v) || Pairs.TryGetValue(All[j].Id + "+" + All[i].Id, out v)) _match[i, j] = v; }
            }
            return x._index >= 0 && y._index >= 0 ? _match[x._index, y._index] : 0f;
        }

        // ---------------------------------------------------------------- its words
        private static void Themes(NpcActor a, Player p, List<KeyValuePair<string, float>> themes)
        {
            NpcNature n = Of(a);
            if (n == null || n.Plain || a.Mind == null) return;
            if (n._themes == null) { string me = "nature_" + n.Id; n._themes = new[] { me, me + "_fled", me + "_fought", me + "_work", me + "_evening", me + "_build" }; } // (made once a nature)
            themes.Add(new KeyValuePair<string, float>(n._themes[0], 28f));
            bool ran, won;
            if (NpcMind.FoughtOf(a, out ran, out won) < 90f) themes.Add(new KeyValuePair<string, float>(ran ? n._themes[1] : n._themes[2], 75f)); // (on any game: a talk asks this on yours)
            if (a.Worker != null && a.Worker.Works && NpcMind.LayerNow(a) == NpcMind.Layer.Job) themes.Add(new KeyValuePair<string, float>(n._themes[3], 32f));
            if (NpcVoice.DayPart() == "evening") themes.Add(new KeyValuePair<string, float>(n._themes[4], 30f));
            if (p != null && p.InPlaceMode()) themes.Add(new KeyValuePair<string, float>(n._themes[5], 42f));
        }

        internal static void Wire()
        {
            NpcVoice.Sources.Add(Themes);
            Lang.Changed += () => _langNow++; // (a refusal kept is in the old language)
            NpcMind.LookOuts.Add(LooksOut);
            StoryCond.Register("nature", new StoryCond.Kind { Usage = "nature <id>   (його вдача: plain bully caring coward lazy loner nosy worker reveller grumbler)", Test = (p, w) => NpcTalk.Him != null && w.Length > 1 && Is(NpcTalk.Him, w[1]) });
        }
    }
}
#endif

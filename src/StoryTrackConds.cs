#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The track's own conditions (registered into StoryCond on the first load):
    //   world <x> · wvar <x> [>=|<…] N      the world's story (StoryWorld)
    //   chose <choice> [way]                 the way the player went at a quest's choice (StoryQuests ways); no way — chose at all
    //   step <quest> <stage>                 standing at that stage of the quest now (a place's touch glows only then); a stage already passed that only
    //                                        waits to move on — its event just raised, the server's answer not come — is not stood at (StoryQuests.IsAt)
    //   reached <quest> <stage>              the story got to that stage or past it, by the file's order (StoryQuests.Reached): «!reached» is the end
    //                                        of what is no more in its time — a story person's question gone for good
    //   chnow N                             the story's block going on now is N (StoryQuests.ChapterNow): put by NpcTalk.Parse on a story person's questions —
    //                                        never written in data (storycheck)
    //   wholechapter · always                    hold always: a story person's question meant to live its whole chapter / never to end (storycheck asks each
    //                                        question of a story talk's first screen for its end — these say «on purpose»; «always» lifts the chapter's
    //                                        bounds too, and only beside «taleuntold»)
    //   through [N]                          holds always: a story person's question that lives past its chapter till the story ends it (NpcTalk's header:
    //                                        its own lasting «?!world …», or a later chapter's «?- <place> : <condition>»); N — no end yet, judged open at chapter N
    //   chfrom N                            the story's block going on now is N or a later one: put by NpcTalk.Parse on a «through» question — never written in data
    //   played N                             minutes of real play in this character (idle minutes do not count)
    //   near <place> [metres=15]             standing that near (StoryWhere)
    //   before <thing> [metres=6] [half=2]   straight out before it, on its outer side, within half m of its middle line (a scene that walks you
    //                                        to a gate: from there nothing stands in the way; from the side, the stairs did)
    //   calm                                 a good moment for something to happen: alive, outdoors, awake, not
    //                                        fighting, not on a ship, not in a cutscene
    //   sky                                  open sky overhead (no roof, not underground: a raven may circle over you)
    //   since <stamp> N                      N minutes of real play since the action «stamp <stamp>» (a pause in a story:
    //                                        «10 minutes after the first raven»; an idle game does not count)
    //   quota <name> N                       fewer than N actions «spend <name> <hours>» in their round open now (a few at a time, then a rest of
    //                                        the game's hours: Torvald's tall tales); no round open — holds
    //   wsince <mark> N                      N minutes of the world's clock since the action «wclock <mark>» (a server's quest's short pause: Eirik's
    //                                        raven five minutes after the brew — a server's quest keeps no character's minutes)
    //   days <mark> N                        N of the game's days since the action «wstamp <mark>» (the world's: a server's quest, a person's
    //                                        «after:»; 1 — the next day, whatever the hour)
    //   gate <place> open|shut               a place's great door (locations.list «gate:»; its own world key: the one fact — never a story flag)
    //   placed <place> · hearth <place> N    the world chose its spot (it is built when someone first comes near) · its hearth burns at least N
    //   state <place> [>=…] N[.K]            its state and sub-state (1.2: abandoned · kindled)
    //   been <place>                         the player has stood on its ground (in its no-build ring) — written once (Been)
    //   lived <quest>.<stage>[.<way>]        that entry of one's own chronicle is written: a stage (or way) with «saga:» passed (Story.Saga)
    //   read <place>.<piece> [full]          the player has read that piece's words («full»: its first «read:» line, LocTouch.ReadFull) (a place's «read:», E on it: LocTouch.ReadBy)
    //   asked <talk> [place]                 the player has heard that place of a talk (an answer led there: NpcTalk.Heard — the talk keeps it
    //                                        for a place someone asks about, and for every «once» answer as before); no place: any of them.
    //                                        In this world: heard here, or heard in another one and this world's story has come to it (NpcTalk.Opened)
    //   (world, wvar, placed, gate, hearth, state ask only the world: a place's server rules may use them — StoryCond.Kind.World; step, reached, quest
    //   too when the quest is the server's — Kind.WorldIf, OfServer: the lasting way to say «since that stage» in a person's «after:», a place's «becomes:»)
    internal static class StoryTrackConds
    {
        internal const string PlayedVar = "played_s";
        private static bool _done;
        private static float _lastInput = -999f, _unsaved, _saveAt;

        internal static void Register()
        {
            if (_done) return;
            _done = true;
            StoryModules.Conds(); // (the modules' own conditions)
            StoryCond.Register("world", new StoryCond.Kind { Dry = "world world:{1}", Usage = "world <назва>   (світовий прапорець сюжету)", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»", Test = (p, a) => StoryWorld.Flag(a[1]), World = true });
            StoryCond.Register("wvar", new StoryCond.Kind { Dry = "world wvar:{1}", Usage = "wvar <назва> [>=|<|…] N   (світове число)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryCond.Compare(StoryWorld.Var(a[1]), a, 2), World = true });
            StoryCond.Register("chose", new StoryCond.Kind { Dry = "me chose:{1}", Usage = "chose <вибір> [шлях]   (яким шляхом гравець пішов; без шляху — чи вибрав узагалі)", Min = 1,
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Test = (p, a) => { string c = StoryQuests.Chosen(p, a[1]); return a.Length > 2 ? c == a[2] : c.Length > 0; } });
            StoryCond.Register("step", new StoryCond.Kind { Dry = "step", Usage = "step <квест> <етап>   (гравець зараз на цьому етапі квесту)", Min = 2, WorldIf = OfServer,
                Check = a => Story.GoodName(a[1]) && Story.GoodName(a[2]) ? null : "погана назва «" + a[1] + " " + a[2] + "»",
                Test = (p, a) => StoryQuests.IsAt(p, a[1], a[2]) });
            StoryCond.Register("reached", new StoryCond.Kind { Dry = "reached", Usage = "reached <квест> <етап>   (сюжет дійшов до цього етапу чи далі, за порядком у файлі; квест пройдено — до всіх)", Min = 2, WorldIf = OfServer,
                Check = a => Story.GoodName(a[1]) && Story.GoodName(a[2]) ? null : "погана назва «" + a[1] + " " + a[2] + "»",
                Test = (p, a) => StoryQuests.Reached(p, a[1], a[2]) });
            // (no Dry: never written in data, a quest never waits on it — NpcTalk.Parse puts it on a story person's questions)
            StoryCond.Register("chnow", new StoryCond.Kind { Usage = "chnow N   (зараз іде блок сюжету N: найпізніший, чий квест почався; 0 — пролог)", Min = 1,
                Check = a => Errands.I(a[1], -1) < 0 ? "chnow N: N — число від 0" : null, Test = (p, a) => StoryQuests.ChapterNow(p) == Errands.I(a[1], 0) });
            StoryCond.Register("chfrom", new StoryCond.Kind { Usage = "chfrom N   (іде блок сюжету N чи пізніший: гра ставить його наскрізному питанню сама)", Min = 1,
                Check = a => Errands.I(a[1], -1) < 0 ? "chfrom N: N — число від 0" : null, Test = (p, a) => StoryQuests.ChapterNow(p) >= Errands.I(a[1], 0) });
            StoryCond.Register("through", new StoryCond.Kind { Dry = "free", Usage = "through [N]   (наскрізне питання людини сюжету: живе й після своєї глави, доки сюжет його не закриє; N — кінця ще нема, переглянуто на главі N)",
                Check = a => a.Length > 1 && Errands.I(a[1], -1) < 0 ? "through [N]: N — число від 0" : null, Test = (p, a) => true });
            StoryCond.Register("wholechapter", new StoryCond.Kind { Dry = "free", Usage = "wholechapter   (питання людини сюжету живе всю свою главу: так задумано)", Test = (p, a) => true });
            StoryCond.Register("always", new StoryCond.Kind { Dry = "free", Usage = "always   (питання людини сюжету поза межами глав: лише переказ подій, taleuntold)", Test = (p, a) => true });
            StoryCond.Register("quest", new StoryCond.Kind { Dry = "quest", Usage = "quest <id> [done|active|failed|started]   (без стану: done; квест сервера читає світ)", Min = 1, WorldIf = OfServer,
                Check = a => a.Length > 2 && System.Array.IndexOf(new[] { Story.Done, Story.Active, Story.Failed, "started" }, a[2]) < 0 ? "стан квесту: done | active | failed | started" : null,
                Test = (p, a) =>
                {
                    string want = a.Length > 2 ? a[2] : Story.Done, has = StoryQuests.StateOf(p, a[1]);
                    return want == "started" ? has.Length > 0 : has == want;
                } });
            StoryCond.Register("played",new StoryCond.Kind { Dry = "free", Usage = "played [>=|<|…] N   (хвилин живої гри цим персонажем)", Min = 1,
                Check = a => StoryCond.CmpCheck(a, 1), Test = (p, a) => StoryCond.Compare(Played(p) / 60, a, 1) });
            StoryCond.Register("before", new StoryCond.Kind { Dry = "at {1}", Usage = "before <річ> [метрів=6] [пів-ширини=2]   (прямо перед нею, із зовнішнього боку)", Min = 2,
                Check = a => (a.Length > 2 && Errands.I(a[2], -1) < 1) || (a.Length > 3 && Errands.I(a[3], -1) < 1) ? "before: метри — числа від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at, mid, way;
                    float m = a.Length > 2 ? Errands.I(a[2], 6) : 6f, half = a.Length > 3 ? Errands.I(a[3], 2) : 2f;
                    if (!StoryWhere.Find(a[1], out at) || StoryWhere.Apart(at, p.transform.position) > m + half + 2f || !BeforeOf(a[1], out mid, out way)) return false; // (far off: only the distance; its colliders only within a few m of it)
                    Vector3 d = p.transform.position - mid; d.y = 0f;
                    float ahead = Vector3.Dot(d, way);
                    return ahead >= 0f && ahead <= m && Mathf.Abs(Vector3.Dot(d, Vector3.Cross(Vector3.up, way))) <= half;
                } });
            StoryCond.Register("near", new StoryCond.Kind { Dry = "at {1}", Usage = "near <місце> [метрів=15]   (hall, hall.<назва>, місце автора)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "near: метри — число від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at;
                    float r = a.Length > 2 ? Errands.I(a[2], 15) : 15f;
                    return StoryWhere.Find(a[1], out at) && StoryWhere.Apart(at, p.transform.position) <= r;
                } });
            StoryCond.Register("npcclip", new StoryCond.Kind { Dry = "free", Usage = "npcclip <хто> <кліп>   (цей кліп чи його частина зараз на людині)", Min = 2, Test = (p, a) => StoryHold.InClip(Npcs.Find(a[1]), a[2]) });
            StoryCond.Register("npcprop", new StoryCond.Kind { Dry = "free", Usage = "npcprop <хто>   (людина зараз тримає річ у руці чи на руці: своя річ або річ сцени; та, що лежить на землі, — ні)", Min = 1, Test = (p, a) => StoryProps.InHand(Npcs.Find(a[1])) });
            StoryCond.Register("npcat", new StoryCond.Kind { Dry = "free", Usage = "npcat <місце> [метрів]   (людина, з якою говориш, зараз у цьому місці: без метрів — на його землі за кресленням; річ чи мітка без метрів — 15 м)", Min = 1,
                Check = a => a.Length > 2 && Errands.I(a[2], -1) < 1 ? "npcat: метри — число від 1" : null,
                Test = (p, a) =>
                {
                    Vector3 at;
                    NpcActor him = NpcTalk.Him;
                    if (him == null) return false;
                    string piece;
                    Loc loc = StoryWhere.Place(a[1], out piece);
                    if (loc != null && piece.Length == 0 && a.Length <= 2) return loc.Holds(him.transform.position); // (the Hall's walls are no circle: a radius took in the yard outside its gate)
                    return StoryWhere.Find(a[1], out at) && StoryWhere.Apart(at, him.transform.position) <= (a.Length > 2 ? Errands.I(a[2], 15) : 15f);
                } });
            StoryCond.Register("calm", new StoryCond.Kind { Dry = "free", Usage = "calm   (жива, не в підземеллі, не спить, не б'ється, не на кораблі, не будує, не біля станка, не сидить, вікна закриті)", Test = (p, a) => Calm(p), Why = (p, a) => WhyNotCalm(p) });
            StoryCond.Register("inside", new StoryCond.Kind { Dry = "at {1}", Usage = "inside <місце>   (гравець у будівлі цього місця: стоїть на її підлозі й під її дахом; поруч під виступом даху — ні)", Min = 1,
                Test = (p, a) => Inside(p, a[1]) });
            StoryCond.Register("touched", new StoryCond.Kind { Dry = "moment", Usage = "touched <місце.назва>   (гравець щойно натиснув E на цій сюжетній речі: сцена, що чекає дотику, «until touched …»)", Min = 1,
                Test = (p, a) => LocTouch.UsedKey == a[1] && Time.time - LocTouch.UsedAt < 2f });
            StoryCond.Register("sky", new StoryCond.Kind { Dry = "free", Usage = "sky   (над головою небо: не під дахом, не в підземеллі)", Test = (p, a) => !Senses.Indoors && !Senses.Underground, Why = (p, a) => Senses.Underground ? "у підземеллі" : "під дахом" });
            StoryCond.Register("since", new StoryCond.Kind { Dry = "after stamp:{1}", Usage = "since <мітка> [>=|<|…] N   (хвилин живої гри від дії «stamp <мітка>»; без мітки — ні)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => Story.Var(p, StampVar(a[1])) > 0 && StoryCond.Compare((Played(p) - Story.Var(p, StampVar(a[1]))) / 60, a, 2),
                Why = (p, a) => Story.Var(p, StampVar(a[1])) > 0 ? "минуло " + (Played(p) - Story.Var(p, StampVar(a[1]))) / 60 + " хв" : "мітки «" + a[1] + "» нема" });
            StoryActions.Register("stamp", new StoryActions.Kind { Dry = "set me stamp:{1}", Min = 1, Usage = "stamp <мітка>   (запам'ятати хвилину живої гри: умова «since <мітка> N»)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { Story.SetVar(p, StampVar(w[1]), Mathf.Max(1, Played(p))); return null; } });
            // a few of a thing, then a rest (Torvald's tall tales, the user 2026-11-15: «three at a time, then a couple of the game's hours»): «spend <name> <hours>»
            // counts one; the first of a round opens it for that many of the game's hours (the world's clock: a night slept through goes by too). «quota <name> N»
            // holds while fewer than N were spent in the round open now, or none is open. The character's own: each player hears his own three
            StoryCond.Register("quota", new StoryCond.Kind { Dry = "free", Usage = "quota <назва> N   (менше N дій «spend <назва>» за їхнє теперішнє коло; кола нема чи минуло — так)", Min = 2,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : Errands.I(a[2], 0) < 1 ? "quota <назва> N: N від 1" : null,
                Test = (p, a) => !RoundOpen(p, a[1]) || Story.Var(p, QuotaVar(a[1])) < Errands.I(a[2], 1),
                Why = (p, a) => RoundOpen(p, a[1]) ? "уже " + Story.Var(p, QuotaVar(a[1])) + ", коло скінчиться за " + Mathf.CeilToInt((Story.Var(p, QuotaEndVar(a[1])) - WorldSec()) / NpcLevers.GameHour()) + " год гри" : "коло не почато" });
            StoryActions.Register("spend", new StoryActions.Kind { Dry = "none", Min = 2, Usage = "spend <назва> <годин гри>   (ще одна дія для «quota <назва> N»; перша в колі відкриває його на стільки годин гри, до " + RoundMost + ")",
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : Errands.F(a[2], 0f) <= 0f || Errands.F(a[2], 0f) > RoundMost ? "spend <назва> <годин гри>: від 0 до " + RoundMost : null,
                Do = (p, q, w, rest) =>
                {
                    if (!RoundOpen(p, w[1])) { Story.SetVar(p, QuotaVar(w[1]), 0); Story.SetVar(p, QuotaEndVar(w[1]), WorldSec() + Mathf.Max(1, Mathf.RoundToInt(Errands.F(w[2], 1f) * NpcLevers.GameHour()))); }
                    Story.AddVar(p, QuotaVar(w[1]), 1);
                    return null;
                } });
            // the world's own pause, in the game's days (chapter 1: Ketil comes two days after the first heart; a day after his bed): the server's quest has no
            // character to keep minutes in, and a night slept through is a day gone. Kept as the world's number «t_<mark>» = the day + 1 (0 is «never»)
            StoryCond.Register("days", new StoryCond.Kind { Dry = "world wstamp:{1}", Usage = "days <мітка> [>=|<|…] N   (днів гри від дії «wstamp <мітка>»: 1 — уже наступного дня; без мітки — ні)", Min = 2, World = true,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryWorld.Var(DayVar(a[1])) > 0 && StoryCond.Compare(Today() - (StoryWorld.Var(DayVar(a[1])) - 1), a, 2),
                Why = (p, a) => StoryWorld.Var(DayVar(a[1])) > 0 ? "минуло днів: " + (Today() - (StoryWorld.Var(DayVar(a[1])) - 1)) : "мітки «" + a[1] + "» нема" });
            StoryWorld.ScratchKinds.Add(DayVar("")); // («t_ch1_heart»: a block's «scratch:» takes its marks away with it)
            // the world's own short pause, in minutes of its clock (the user 2026-11-15: Eirik's raven five minutes after the brew): kept as the world's number
            // «tc_<mark>» = the world's second + 1 (0 is «never»); the clock runs while the server does, a night slept through goes by in it too
            StoryCond.Register("wsince", new StoryCond.Kind { Dry = "world wclock:{1}", Usage = "wsince <мітка> [>=|<|…] N   (хвилин годинника світу від дії «wclock <мітка>»; без мітки — ні)", Min = 2, World = true,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryWorld.Var(ClockVar(a[1])) > 0 && StoryCond.Compare((WorldSec() - (StoryWorld.Var(ClockVar(a[1])) - 1)) / 60, a, 2),
                Why = (p, a) => StoryWorld.Var(ClockVar(a[1])) > 0 ? "минуло хвилин світу: " + (WorldSec() - (StoryWorld.Var(ClockVar(a[1])) - 1)) / 60 : "мітки «" + a[1] + "» нема" });
            StoryWorld.ScratchKinds.Add(ClockVar(""));
            // the same mark in seconds (a short wait inside a stage's moment: 20 s after the first mushroom)
            StoryCond.Register("wsecs", new StoryCond.Kind { Dry = "world wclock:{1}", Usage = "wsecs <мітка> [>=|<|…] N   (секунд годинника світу від дії «wclock <мітка>»; без мітки — ні)", Min = 2, World = true,
                Check = a => !Story.GoodName(a[1]) ? "погана назва «" + a[1] + "»" : StoryCond.CmpCheck(a, 2),
                Test = (p, a) => StoryWorld.Var(ClockVar(a[1])) > 0 && StoryCond.Compare(WorldSec() - (StoryWorld.Var(ClockVar(a[1])) - 1), a, 2),
                Why = (p, a) => StoryWorld.Var(ClockVar(a[1])) > 0 ? "минуло секунд світу: " + (WorldSec() - (StoryWorld.Var(ClockVar(a[1])) - 1)) : "мітки «" + a[1] + "» нема" });
            StoryActions.Register("wclock", new StoryActions.Kind { Dry = "set world wclock:{1}", Min = 1, Usage = "wclock <мітка>   (запам'ятати мить годинника світу, раз: умова «wsince <мітка> N» у хвилинах, «wsecs <мітка> N» у секундах)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { if (StoryWorld.Var(ClockVar(w[1])) == 0) StoryWorld.SetVar(ClockVar(w[1]), WorldSec() + 1); return null; } }); // (once: a stage entered again keeps the first moment)
            StoryActions.Register("wforget", new StoryActions.Kind { Dry = "none", Min = 1, Usage = "wforget <префікс>   (ключі блоку геть, як у кінці його квесту: мітки днів, знайдені місця, скрині — StoryWorld.Forget; автору: почати блок наново)",
                Check = a => Story.GoodName(a[1].TrimEnd('_')) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { StoryWorld.Forget(w[1].ToLowerInvariant()); return null; } });
            StoryActions.Register("wstamp", new StoryActions.Kind { Dry = "set world wstamp:{1}", Min = 1, Usage = "wstamp <мітка>   (запам'ятати сьогоднішній день світу, раз: умова «days <мітка> N»)",
                Check = a => Story.GoodName(a[1]) ? null : "погана назва «" + a[1] + "»",
                Do = (p, q, w, rest) => { if (StoryWorld.Var(DayVar(w[1])) == 0) StoryWorld.SetVar(DayVar(w[1]), Today() + 1); return null; } }); // (once: a stage entered again keeps the first day)
            // a place of the story, by its id or its alias (locations.list): what the world says of it — the same for everyone
            StoryCond.Register("placed", new StoryCond.Kind { Dry = "world placed:{1} : the world places it by itself", Usage = "placed <місце>   (світ уже вибрав, де воно стоїть — збудоване чи ще ні)", Min = 1, World = true,
                Check = a => PlaceCheck(a[1]), Test = (p, a) => { Vector3 at; Quaternion r; Loc l = Locs.Get(a[1]); return l != null && l.Where(out at, out r); } });
            StoryCond.Register("gate", new StoryCond.Kind { Dry = "world gate:{1}={2}", Usage = "gate <місце> open|shut   (головна брама місця, рядок gate: — її власний ключ світу)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? (a[2] != "open" && a[2] != "shut" ? "gate <місце> open|shut" : null),
                Test = (p, a) => LocState.GateOpenOf(Locs.Get(a[1])) == (a[2] == "open") });
            StoryCond.Register("hearth", new StoryCond.Kind { Dry = "world hearth:{1} : a player lights the place's hearth (its «hearthwait:» may forbid it)", Usage = "hearth <місце> [>=|<|…] N   (вогнище місця: 0 холодне, 1 Хель, 2 Варти)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? StoryCond.CmpCheck(a, 2), Test = (p, a) => StoryCond.Compare(LocHearth.LevelOf(Locs.Get(a[1])), a, 2) });
            StoryCond.Register("state", new StoryCond.Kind { Dry = "world state:{1} : the place's own «becomes:» rules", Usage = "state <місце> [>=|<|…] N[.K]   (стан місця й субстан: 1.2 — «закинута · розпалена»)", Min = 2, World = true,
                Check = a => PlaceCheck(a[1]) ?? StateCheck(a), Test = (p, a) => StateTest(Locs.Get(a[1]), a) });
            StoryCond.Register("been", new StoryCond.Kind { Dry = "me been:{1} : he walks in", Usage = "been <місце>   (гравець уже стояв на землі цього місця)", Min = 1,
                Check = a => PlaceCheck(a[1]), Test = (p, a) => { Loc l = Locs.Get(a[1]); return l != null && Story.Flag(p, BeenFlag(l)); } });
            StoryCond.Register("lived", new StoryCond.Kind { Dry = "lived", Usage = "lived <квест>.<етап>[.<шлях>]   (цей запис уже є у власному літописі: етап чи шлях із saga: пройдено)", Min = 1,
                Check = a => Story.GoodName(a[1]) && a[1].IndexOf('.') > 0 ? null : "lived <квест>.<етап>[.<шлях>]",
                Test = (p, a) => p != null && Story.Of(p).Saga.Contains(a[1]) });
            StoryCond.Register("read", new StoryCond.Kind { Dry = "me read:{1} : he reads it (E)", Usage = "read <місце>.<деталь> [full]   (гравець прочитав слова цієї деталі: E на ній; full — саме її перший рядок read:, повний текст)", Min = 1,
                Check = a => a.Length > 2 && a[2] != "full" ? "read <місце>.<деталь> [full]" : LocTouch.ReadCheck(a[1]),
                Test = (p, a) => a.Length > 2 ? LocTouch.ReadFull(p, a[1]) : LocTouch.ReadBy(p, a[1]).Length > 0 });
            StoryCond.Register("asked", new StoryCond.Kind { Dry = "me asked:{1} : he asks it in the talk", Usage = "asked <розмова> [місце розмови]   (гравець уже чув це місце розмови: до нього привела відповідь; без місця — будь-яке)", Min = 1,
                Check = a => { if (!Story.GoodName(a[1]) || (a.Length > 2 && !Story.GoodName(a[2]))) return "asked <розмова> [місце розмови]"; if (a.Length > 2) NpcTalk.Note(a[1], a[2]); return null; }, // (asked about: the talk keeps it from now on)
                Test = (p, a) => a.Length > 2 ? NpcTalk.Heard(p, a[1], a[2]) : NpcTalk.HeardAny(p, a[1]) });
        }

        // «step|reached|quest <quest> …» of a quest of the server reads the world, no player (StoryQuests.State): a server's rule may ask it — its stage stays reached
        // when the quest is over, as a key of its «scratch:» does not (Lit's home, the Hall's state: the user 2026-11-16)
        private static bool OfServer(string[] a) { StoryQuests.QuestDef q = a.Length > 1 ? StoryQuests.Find(a[1]) : null; return q != null && q.Server; }

        internal static bool Calm(Player p) { return WhyNotCalm(p) == null; }

        private static string PlaceCheck(string id) { return Locs.Get(id) == null ? "нема місця «" + id + "» (locations.list)" : null; }

        // «state zala >= 1.2»: its state ×100 + sub-state, compared as one number (1.2 → 102)
        private static string StateCheck(string[] a)
        {
            string[] b = Hundreds(a);
            return b != null && StoryCond.CmpCheck(b, 2) == null ? null : "state <місце> [>=|<|…] N[.K]";
        }

        private static bool StateTest(Loc l, string[] a)
        {
            string[] b = Hundreds(a);
            return l != null && b != null && StoryCond.Compare(LocState.StateOf(l) * 100 + LocState.SubOf(l), b, 2);
        }

        private static string[] Hundreds(string[] a)
        {
            string[] b = (string[])a.Clone();
            int i = b.Length - 1, s, k;
            string num = b[i], sign = "";
            while (num.Length > 0 && (num[0] == '<' || num[0] == '>' || num[0] == '=')) { sign += num[0]; num = num.Substring(1); }
            if (!LocState.SubNumber(num, out s, out k)) return null;
            b[i] = sign + (s * 100 + k);
            return b;
        }

        // been on a place's ground (the personal flag, written once: Been — the places' look twice a second)
        private static string BeenFlag(Loc l) { return "been." + l.Id + "." + Story.WorldId; } // (a character's, in this world: a place stands elsewhere in another)
        internal static void Been(Player p)
        {
            foreach (Loc l in Locs.All)
            {
                if (!l.Built || Story.Flag(p, BeenFlag(l)) || !l.InNoBuild(p.transform.position)) continue;
                Story.SetFlag(p, BeenFlag(l), true);
            }
        }

        // on a floor of that place and under a roof of it, by the pieces themselves (the game's shelter is true under the eaves outside too)
        internal static bool Inside(Player p, string place)
        {
            return LocState.InsideOf(p, place);
        }

        // a thing's front, kept BeforeEvery s (StoryWhere.Before reads its colliders; a gate swinging moves it, so not for good)
        internal static float BeforeEvery = 2f;
        private static string _beforeOf;
        private static float _beforeAt = -99f;
        private static bool _beforeOk;
        private static Vector3 _beforeMid, _beforeWay;
        private static bool BeforeOf(string thing, out Vector3 mid, out Vector3 way)
        {
            if (thing != _beforeOf || Time.time - _beforeAt > BeforeEvery) { _beforeOf = thing; _beforeAt = Time.time; _beforeOk = StoryWhere.Before(thing, out _beforeMid, out _beforeWay); }
            mid = _beforeMid; way = _beforeWay;
            return _beforeOk;
        }

        private static string StampVar(string stamp) { return "at_" + stamp; } // (the second of real play «stamp» was put at: the character's var)
        // «quota»/«spend»: how many in the round open now, and the world's second it ends at (the character's vars). A round longer than RoundMost of the
        // game's hours ahead is no round of this world (the character came from another world whose clock ran further): not open
        internal const int RoundMost = 24;
        private static string QuotaVar(string name) { return "qn_" + name; }
        private static string QuotaEndVar(string name) { return "qe_" + name; }
        private static int WorldSec() { return ZNet.instance != null ? (int)ZNet.instance.GetTimeSeconds() : 0; }
        private static bool RoundOpen(Player p, string name) { int end = Story.Var(p, QuotaEndVar(name)), now = WorldSec(); return now < end && end - now <= RoundMost * NpcLevers.GameHour(); }
        // the author's jump (StoryQuests.Rebuild): a mark put as if so long ago — «since <mark> N» / «days <mark> N» then hold; below zero — taken away
        internal static void StampAgo(Player p, string stamp, int minutes) { Story.SetVar(p, StampVar(stamp), minutes < 0 ? 0 : Mathf.Max(1, Played(p) - minutes * 60 - 1), true); }
        internal static void ClockAgo(string mark, int minutes) { StoryWorld.SetVar(ClockVar(mark), minutes < 0 ? 0 : Mathf.Max(1, WorldSec() + 1 - minutes * 60 - 1)); }
        private static string ClockVar(string mark) { return "tc_" + mark; } // (the world's second «wclock» was put at, + 1)
        internal static void DayAgo(string mark, int days) { StoryWorld.SetVar(DayVar(mark), days < 0 ? 0 : Mathf.Max(1, Today() + 1 - days)); }
        private static string DayVar(string mark) { return "t_" + mark; } // (the world's day «wstamp» was put at, + 1)
        // the game's day now — the server too (no EnvMan there: the world's clock by the game's day length)
        internal static int Today()
        {
            if (EnvMan.instance != null) return EnvMan.instance.GetDay();
            return ZNet.instance != null ? (int)(ZNet.instance.GetTimeSeconds() / 1800.0) : 0;
        }

        // what keeps the moment from being calm (null: calm) — the panel says it
        internal static string WhyNotCalm(Player p)
        {
            if (p == null) return "нема гравця";
            if (p.IsDead()) return "мертвий";
            if (p.InInterior() && !LocInside.In(p.transform.position)) return "у підземеллі"; // (a dungeon of the game; one of ours is built for the story's scenes — the user 2026-11-15: crypt_scroll waited by its chest for ever)
            if (p.IsSleeping()) return "спить";
            if (p.IsTeleporting()) return "телепорт";
            if (p.GetStandingOnShip() != null || p.IsAttachedToShip()) return "на кораблі";
            if (p.InCutscene()) return "сцена гри";
            if (p.InPlaceMode()) return "будує";
            if (p.GetCurrentCraftingStation() != null) return "біля станка";
            if (p.IsAttached() || p.IsSitting()) return "сидить";
            if (InventoryGui.IsVisible() || StoreGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || TextInput.IsVisible() || Console.IsVisible() || (Chat.instance != null && Chat.instance.HasFocus())) return "відкрите вікно";
            if (Senses.Pressed(p))
            {
                Character c = Senses.NearestFoe != null ? Senses.NearestFoe : Senses.Boss;
                return "бій або ворог поруч" + (c != null ? ": " + c.name.Replace("(Clone)", "") + " " + Localization.instance.Localize(c.m_name) + " за " + Mathf.RoundToInt(Vector3.Distance(c.transform.position, p.transform.position)) + " м" : " (хто — не видно)");
            }
            return null;
        }

        // seconds of real play (kept in the character: StoryState n.played_s)
        internal static int Played(Player p) { return Story.Var(p, PlayedVar) + Mathf.FloorToInt(_unsaved); }

        // every frame: a minute counts while someone touches the keys or the mouse within it (an idle game does not)
        internal static void Count(Player p)
        {
            if (Input.anyKey || Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.01f) _lastInput = Time.time;
            if (Time.time - _lastInput < 60f && !Game.IsPaused()) _unsaved += Time.deltaTime;
            if (Time.time < _saveAt || _unsaved < 1f) return;
            _saveAt = Time.time + 30f;
            int whole = Mathf.FloorToInt(_unsaved);
            _unsaved -= whole;
            Story.SetVar(p, PlayedVar, Story.Var(p, PlayedVar) + whole, true); // (every 30 s: not into the log)
        }

        // the author's «skip the wait»
        internal static void AddMinutes(Player p, int min) { Story.SetVar(p, PlayedVar, Story.Var(p, PlayedVar) + min * 60); }
    }
}
#endif

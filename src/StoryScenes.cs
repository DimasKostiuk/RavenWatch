#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (story-plan.md part 7): a scene is a text file of steps played one after another; one scene at
    // a time. A quest's stage «scene: <id>» plays it and passes on «scene_done <id>». Played live: the player keeps
    // the keys unless a shot, the bars or the black hold him (StoryCamera.TakesPlayer). All it took is given back in one place (Release). The world never pauses — a scene that goes wrong stops:
    //   death · a fight (hit, or a foe at you; one that roams waits it out instead: FightWait) · going off more than 60 m from where it plays
    // and its actors vanish; the stage plays it again later (after Retry seconds, when «needs» hold again).
    // Every step has its own time limit: a scene never hangs.
    //
    // Files: assets/story/scenes/*.txt — read where StoryFiles says (built in; on the author's machine the mod's own folder,
    // edited in F9 «Сцени»). This file: playing; reading: StoryScenesRead; the kinds of steps: StoryScenesKinds; the player's, the bird's, the people's
    // verbs: StoryScenesPlayer / Bird / Cast; the sisters' falcons: StoryScenesFalcon; the camera: StoryScenesCamera.
    //   [scene raven_call]
    //   needs: calm                         conditions to begin (StoryCond)
    //   place: prologue_house               where it is filmed (the author's shots of that place: StoryShots)
    //   roams: yes                          it goes with the player wherever he walks (a raven over him): not stopped when he is
    //                                       FarStop m from where it began (2026-11-08: the prologue's nudge died three times as the
    //                                       player walked on while Munin flew 60 m to him); a fight neither stops it nor skips his
    //                                       steps: it waits, its bird circling over him, and goes on when calm (FightWait; FightMost
    //                                       s at most — then stopped, and played again); death still stops it
    //   alone: yes                          a scene of one's own: the other players are not drawn in this game while
    //                                       it plays (StoryAlone — nobody walks into the shot)
    //   guard: 30 | off                     the quiet round the event (StoryHush): its full size in m; none — by the
    //                                       place's size; off — none. It grows round the place as the player comes on
    //                                       the stage that leads here; no place — round the player while it plays
    //   aside: away                         the people it does not hold leave the frame (StoryAside: all within Quiet m go off
    //                                       to Gone m) — the user 2026-11-17, Eyvind in the Hall: «нехай на момент сцени нпс уходять,
    //                                       але не завжди: в деяких сценах ок, що на фоні хтось ходить»; none (or «step») — they only
    //                                       step off the player (Clear → To m) and may walk on behind
    //   audience: 55 | seen | 55 seen       those who watch it (StoryWatch): every player within so many m of its place (none: of its director)
    //                                       sees it as he does — his shots, bars, the black, what is heard; none: StoryWatch.Radius, at most
    //                                       RadiusMost. «seen»: their bodies stay in the picture (else the players who watch are not drawn
    //                                       while a shot holds — the user 2026-11-20). «alone: yes» has no audience at all
    //   camera: still                       full stillness (the user 2026-11-18: Heid walking past swung the shoulder shot of eyvind_meet —
    //                                       «тут потрібна повна статика»; the falcons' scene may jump as it does): every shot told in words
    //                                       picks its spot once as it begins and keeps it, nobody passing moves it (one shot alone: the
    //                                       word «still» in its line — StoryScenesCamera)
    // PARTS (the user 2026-11-21, the Hall's last scene: «головна проблема — щоб усі постійно були у русі… треба чіткого режисера, який водить
    // всіх по сцені і не зупиняє їх»): one line of steps moved one person at a time, the rest stood. A scene may have parts — lines of steps
    // of their own, each played beside the scene's line at its own pace, with its own step's clock (Part, Swap):
    //   part lit                            the lines from here on are the part «lit» (any name: a person's, «camera») — till the next «part»
    //   part main                           … back to the scene's own line
    //   parts go                            (the scene's line) every part begins, each from its first step; a part that runs out just ends;
    //                                       the scene ends when its own line does, whatever the parts still do
    //   cue <name> · wait cue <name> [s=60] a sign given once for the whole scene · waits till it is given (one part tells another: «he has
    //                                       come level with me» → the hand points)
    //   wait music <s>                      waits till the scene's music is that far into its track (Audio.MusicAt): a cut, a line lands on
    //                                       the music whatever the steps before it took. No music of the scene: the seconds since it began
    //   npc <who> busy <clip>|none          that person's work: whenever nothing of the scene plays on its body and it does not walk, this
    //                                       clip begins again by itself (BusyTick) — nobody of a scene stands like a post between its orders
    // A part's steps are ordinary steps, written whole («npc lit clip …» in the part «lit» too): every tool reads them as ever. Not in a
    // part: a lasting step (do, drop, door, own — what a stop finishes is counted along the scene's own line) and «parts». The author's
    // step-by-step holds the scene's own line only. All the steps stay ONE list in the file's order (SceneDef.Steps): a step's number is
    // the same for the window «Режисер», those who watch (StoryWatch.Step) and the check's rows, whichever line plays it.
    // It never begins in a fight at you (FoeNear: what would stop it at once), whatever its «needs».
    // It begins only when every person it takes from the world (npc <who> …, not its own «spawn») is near and free. It never waits for
    // a raven of the game (the user 2026-11-07: the heart built, the hammer gone — and 23 s of nothing while it waited for one): a raven
    // still waiting to be heard when it begins flies off, as it does when nobody comes (Plugin.RunOutVisit), and none comes while it plays.
    // Once is once: after a lasting step (do, own, drop, door) it is never played again — stopped then, or the game
    // left, its lasting steps still ahead run quietly and it counts as played.
    //   bird come 60 18 land 5              the story's raven: from 60 m behind you, 18 m up, lands 5 m before you
    //   bird land hall.dveri_holovni        … or flies on to a place and sits on top of it
    //   bird look player                    turns to you
    //   bird say …ти ж обіцяв.             a line over it
    //   bird circle 30 [4.5] [3.5]          round over you for 30 s (radius, metres over your head)
    //   bird over [10]                      waits till it is really over you, within so many m (it set off from afar and circles)
    //   bird leave hall                     off that way and gone (the step does not wait for it)
    //   bird come from hall hover           a place's own raven (LocRaven; the Hall's postman): out of its place to hang in the air before you
    //   bird home hall                      … and back home, in through its roof
    //   letter Літ :: Приходь. Поміряв.     a letter on his screen (the post's window); the step waits till he folds it
    //   letter Варта, що лишилась seal bruni :: …   signed in words with that sender's own seal (one who writes but is no person of the world)
    //   bird gone                           a puff of feathers
    //   bird wear <look> [4]                its colours go over into that look of assets/story/looks, slowly (StoryBird.Wear: a few repaints)
    //   bird come … nowait                  the scene goes on as it flies in (then «bird circle»: it never sits)
    //   bird come … as hugin|munin          your own raven's picture (plain black, its size asleep); … look <id> another look of assets/story/looks
    //   bird come … at <place> side -0.5    on top of a thing, this far right (below zero: left) of its middle as you see it
    //   bird shoulder [hugin|munin]         onto your shoulder (its kind's); then «own <kind> here» makes it yours
    //   falcon <name> come [from] [up] land [<place>] [dive] [nowait] · … circle   a sister's falcon (Falcon), several by name:
    //                                       flies in and lands (waits till it stands), or circles over you
    //   falcon <name> land [<place>] [dive] · circle · leave [<place>|up] · gone
    //   falcon <name> skim <from> <to> [run 20] [up 35]   the low run: made high over «from», straight to «to», the last «run» m low over the
    //                                       ground, never braking (does not wait); over «to»: «falcon <name> become <who> stride go <place>» —
    //                                       gone behind its feathers at that speed, the person walks on to the place
    //   falcon <name> become <who> · falcon <name> from <who>   on its feet it turns into a person (made there for the scene) ·
    //                                       a person turns into a falcon standing there; falcon.<name>: where it is (StoryScenesFalcon)
    //   lane <name> <from> <toward> [clear 45]   a straight clear line for the scene; lane.<name>@<m>[@<side>]: a place on it (StoryScenesLane)
    //   say hugin|munin <текст>             your own raven says it (only if it is on your shoulder)
    //   msg <текст>                         a line in the middle of the screen
    //   camera <from> [up <m>] [look <what>] [blend 2] [fov 50]   a shot (StoryCamera): from a place (up: the lens that high over its point; none: 1.6), «behind», «over» or «face» (close up);
    //                                       looking at player (default), bird, sky (up before you) or a place; the player stands still
    //   camera on <who> [close|medium|full|wide] [front|left|right|back] [low|high] [track]   one in the picture, in words: how near, from
    //                                       which side of it, lower or higher; track: it goes with it (who: player, bird, npc.<who>, a place)
    //   camera shoulder <a> <b> [left|right] over a's shoulder at b · camera two <a> <b> [left|right]   both from the side; a scene keeps to
    //                                       one side of the line between two (the 180° rule) unless a line says left|right
    //   … push <m> <s> [wait]               on these three: the lens goes m nearer over s seconds (all the camera's words: StoryScenesCamera)
    //   camera eyes npc.<who> [look <what>] [cut|blend s]   through that person's eyes: from its head at what it looks at (none: the player)
    //   camera eyes [cut|blend s] [live n]  through his own eyes: from his head, where his head looks (turned by «player look <place> [s]» — s: how slow
    //                                       the turn, 2–3 for a look round a room); he and the ravens on his shoulder are not drawn while it is on
    //   camera off [blend 1.5]              the camera back to the player (also when the scene ends or stops)
    //   bars on|off · sub <текст>           the black bars; a subtitle (waits while it is read)
    //   weather <env> [s] [all] · weather off [all]     forced weather (ThunderStorm, Rain, Misty, Clear …); off when the scene ends or stops
    //   bolt near|ahead [n] [all] · bolt <place> [n] [spread] [all]   bolts from the sky (StoryAir; ahead = before the eyes)
    //   thor [all] · shake <strength> [all]
    //                                       «all»: in every game of the world, round each player
    //   fade out|in [1.5]                   the whole picture to black and back (waits)
    //   glow <0..1> [1.5]                   the light of a memory from the picture's edges, breathing (StoryScreen.Glow; does not wait; the end takes it off)
    //   dim <0..1> [1.5]                    the picture darkened, not black: under a vision (StoryScreen.Dim; does not wait; the end takes it off)
    //   memory <picture> [x y] [w] [4]      a memory over the picture: a see-through picture of assets/story/memories comes, drifts, goes (StoryScreen.Memory;
    //                                       x y its middle and w its width as shares of the screen — 0.5 0.45 0.5; seconds it holds; does not wait)
    //   player face <place> · player emote <gesture> [loop]|stop    the player himself turns softly on the spot and looks there (does not wait) / a
    //                                       gesture of the game; «loop» — it lasts (sit: he sits) till «stop» or the scene's end
    //   player stop [0.8]                   the walk on now ends where he is, slowing over the seconds (waits)
    //   player back <m>                     he steps back that far, slowly, facing as he faces (waits; a clip of ours fades first)
    //   player face <place> slow            the same turn, slower
    //   player look <place|bird|npc.x>|forward    where his head looks till the scene ends (StoryLook; a shot on and no word: straight before him)
    //   player go <place> <m> [front] [run]  he walks to <m> from its middle, on the side he is on, and turns to it (front:
    //                                       the side its face looks to, turned as it is; goes through what is between). Waits till he
    //                                       stands exactly so, at most StoryWalk.Most seconds (a long walk: as long as its way needs); how near he got, in the log
    //   player go <place> <m> before        square before the thing's middle, on its outer side, wherever he comes from (a gate)
    //   player go … keep nowait             he goes on at the pace he came at, and the scene does not wait (it takes up his walk)
    //   player go … soft                    he slows softly over the last metres and stops near it (no exact spot: nothing is taken there)
    //   player gait tired|careful|…|none    how he carries himself till the scene ends (StoryGait, BodyGait)
    //   player pace walk|jog|run · player arrive <place> [m]   the pace of the walk on now · the scene waits till he has got there,
    //                                       or till only m are left (the next shot catches him still walking)
    //   player clip <clip[.part]>|none [nowait]  one of our clips on him, through Gestures (waits for it unless it loops,
    //                                       or «nowait»); the next one crossfades; let go when the scene ends or stops
    //   player mark <name> [seconds]        waits till the clip on him passes that «mark» (BodyClips), at most 30 s
    //   prop take <item> [from <place.piece>] [left|right] [pose <clip>|still] [size <m>] [free] · prop put
    //                                       a thing in the player's hand (its look; the piece it came from hidden; he looks
    //                                       at it — prop_look_<hand>); «free»: it stays where it lay and the clip moves it
    //                                       (@Prop, BodyClips); «npc <who> prop take|put …» the same for a person.
    //                                       All put back when the scene ends or stops (StoryProps)
    //   prop drop · npc <who> prop drop     what is held (in a hand, on the arm; a person's own «carry:» thing too) falls from where it is,
    //                                       straight down, and lies there (waits its fall)
    //   prop throw <place|player|npc.x> · npc <who> prop throw …   it flies an arc there, tumbling, and lies (at a body: just short of it;
    //                                       nothing catches it; waits its flight). A first sketch: no physics, nothing in its way
    //   npc <who> prop fetch                its own thing the story had it leave: it may go back for it
    //   npc.<who>.thing · player.thing      a place's word: the thing that one holds, wherever it is — in hand, on the arm, lying, in the
    //                                       air («camera on npc.heid.thing close», «… look npc.heid.thing», «player look npc.heid.thing»)
    //   wait 1.5                            seconds
    //   until near bird 3 | 20              until the conditions hold, at most 20 s
    //   if raven hugin: say hugin Це не наш. a step only when the conditions hold
    //   do <action>                         a quest's action now (StoryActions: gate hall open, world x, flag x,
    //                                       msg …; not goto/end) — the world changes at the scene's moment, not after it
    //   door <place> open|close             a door of the story's places opened or shut for everyone (hall.dveri_holovni)
    //   drop <item> <place|player|npc.x>    a thing left on the ground there (networked, anyone may pick it up)
    //   npc <who> take                      a person of the story into the scene (who: its template id or its tag; the
    //                                       nearest loaded one): it stops what it does — its mind's Scene layer, over all
    //   npc <who> go <place|player|bird|npc.x> [run|walk] [near 1.6]   walks there — runs when farther than RunFrom m, never with «walk» (waits till it is near: NpcMind.Reach)
    //   npc <who> go <place.piece> before <m> [run]   to that spot exactly, square before the thing's middle on its outer side: a clip that follows
    //                                       touches it. Not come to it: said (the log, the scene check's VERDICT)
    //   npc <who> go <spot.…> exact         to that very point (StoryWhere «spot.<place>@<x>@<z>»), no slack
    //   npc <who> go … nowait               the scene goes on as it walks (it speaks walking, the camera looks elsewhere)
    //   npc <who> arrive [seconds=12]       waits till that person's walk is over — one begun without waiting («go … nowait», a falcon's «become … stride go <place>»)
    //   npc <who> face player|bird|<place>  turns and keeps looking (does not wait)
    //   npc <who> say <текст>               a line over it, as it says it to you (waits while it is read)
    //   npc <who> says <текст>              the same, and the scene goes on while it is said; «wait said» — till it is over
    //   npc <who> emote <gesture> [loop]    a gesture (wave, bow, kneel, cry…; loop: held)
    //   npc <who> clip <clip>|none [on npc.<x>|player [m=0.8]]   one of our clips on that person; «on»: a clip of two — written for one who stands m
    //                                       before it face to face on level ground, its hands carried to where the other really stands (BodyAnimator.Mate)
    //   npc <who> mark <name> [seconds]     waits till the clip on that person passes that «mark» (BodyClips), at most 30 s: never a «wait» that counts a clip's seconds
    //   npc <who> sit <place> · stay · free sits by a place · stands · back to its own day (free: «вільно», a standing word ends too)
    //   do npc <who> hold <place> …         a word that outlives the scene (StoryHold): what it does when the scene lets it go
    //   do npc <who> free                   the standing word gone; one the scene holds stays the scene's till its end
    //   npc <who> spawn <place> [whole]     made for this scene at a place (its template id), facing you, coming in slowly; «whole»: there at once, never
    //                                       see-through (behind a black screen: «fade out» before it)
    //   npc <who> sparks [seconds]          it stands whole while dark yellow sparks rise out of it, and is gone behind their last burst (NpcSparks)
    //   npc <who> spawn <place> behind <m>  … that far behind the place as the camera sees it: out of the picture, at your back (chapter 1, the one who
    //                                       watches from the forest's edge: «npc stranger spawn player behind 24 whole»); on dry ground, not in a trunk
    //                                       or a rock (StoryScenesCast.SpawnBehind); nowhere to stand: skipped, a note
    //   npc <who> spawn <place> at x y z    … always at that spot of the place (metres in its blueprint's frame), on its floor
    //                                       (StoryScenesCast.SpawnIn s); not kept in the
    //                                       world — fades away when the scene ends or stops (NpcFade)
    //   npc <who> fade [3] [sound]          fades away slowly (waits the seconds) and is gone; a sound with it (AudioBank)
    //   music · bed · mix · mute · unmute · ear · sound   what is heard, all of it held by the scene: StoryAudio (its header), Audio
    //   Every one taken is let go when the scene ends or stops (back to its own day where it is). A person is a network
    //   object: everyone near sees it walk and hears its lines; its words go to whichever game runs it.
    //   Places also «npc.<who>» (where that person stands now): for the camera, the bird, «until near npc.heid 3 | 20».
    // Those near (within Seen m) see the bird too: each order goes to them with its places worked out (RPC), their
    // game flies a picture of its own.
    internal static partial class StoryScenes
    {
        internal sealed class Step { private StepKind _kind; internal StepKind Kind { get { return _kind ?? (_kind = KindOf(W)); } } /* (its kind or verb, found once: the director asks every frame) */ public string Text = "", Line = ""; public int Num; public List<Cond> If, Until; public float UntilSec = 10f; public string[] W = new string[0]; public string Part; } // (Num: its line in the file — the window «Режисер» puts lines by it; Part: the part it is a step of, null — the scene's own line)
        internal sealed class SceneDef
        {
            public string Id = "", From = "", NeedsText = "", Place = "", Check = "", Test = "";
            public bool Alone; // («alone: yes»: the other players not drawn here while it plays — StoryAlone)
            public bool Roams; // («roams: yes»: it goes with the player — no stop for walking off from where it began)
            public bool Away;  // («aside: away»: the people it does not hold leave the frame, not only step off the player — StoryAside)
            public bool Still; // («camera: still»: every shot told in words keeps the spot it took as it began — StoryScenesCamera, «still»)
            public float Audience = -1f; // («audience: <m>»: how far from it the players who watch stand; none (-1): StoryWatch.Radius)
            public bool AudienceSeen;    // («audience: seen»: the players who watch stay in the picture)
            public bool Cinema;          // (it takes the picture — a shot, the bars, the black: then it takes that of those who watch too; a letter's raven does not)
            public List<Cond> Needs;
            public float Guard = -1f; // (the quiet round's full size: «guard:»; none (-1) — by the place's size, StoryHush.MaxFor; 0 — off)
            public readonly List<Step> Steps = new List<Step>();
            public readonly List<string> Cast = new List<string>(); // (people it takes from the world — not its own «spawn»: all of them near before it begins)
        }

        internal static readonly List<SceneDef> Scenes = new List<SceneDef>();
        internal static readonly List<string> Errors = new List<string>();
        internal static readonly List<string> Recent = new List<string>();
        internal static float Retry = 20f, FarStop = 60f, Seen = 100f, StepMax = 30f, LetterMost = 300f; // (LetterMost: s a letter may lie open before the scene goes on: «letter»)
        private const string Rpc = "RavenWatch_StoryAct";

        private static bool _loaded;
        private static SceneDef _now;
        private static int _at = -1;
        private static float _stepAt, _until, _waitFor;
        private static Vector3 _anchor;
        // the scene itself put him elsewhere (StoryWalk.Put: under the black, a held walk): «walked off from where it plays» is measured from there
        // (2026-11-14, the ambush: «player go» put him 90 m off under the black, and the next frame stopped the scene as if he had walked away)
        internal static void MovedTo(Vector3 at) { if (_now != null) _anchor = at; }
        private static float _hurtAt = -100f;
        private static StoryBird _bird;
        private static readonly Dictionary<string, float> _stopped = new Dictionary<string, float>(); // a scene stopped: not before Retry
        private static readonly Dictionary<long, StoryBird> _mirrors = new Dictionary<long, StoryBird>(); // others' scenes' birds

        internal static string Folder { get { return Path.Combine(StoryQuests.Folder, "scenes"); } }
        internal static string Playing { get { return _now != null ? _now.Id : ""; } }
        internal static SceneDef Current { get { return _now; } }
        internal static int At { get { return MainAt; } } // (the step of the scene's own line: what the windows mark, what a watcher takes it up from)
        internal static Vector3 BirdAt { get { return _bird != null ? _bird.transform.position : Vector3.zero; } }
        internal static bool HasBird { get { return _bird != null; } }

        internal static SceneDef Find(string id) { Load(); foreach (SceneDef s in Scenes) if (s.Id == id) return s; return null; }

        // ---------------------------------------------------------------- playing
        // asked by a quest's stage each pass: plays it when it may (one at a time, its needs, not just stopped)
        internal static void TryPlay(string id)
        {
            Load();
            if (_now != null || StoryWatch.InPicture) return; // (another's scene is on this screen: none of one's own begins under it)
            Player p = Player.m_localPlayer;
            if (p == null) return;
            SceneDef s = Find(id);
            int resume;
            if (s != null && StoryWatch.Resume(id, out resume) && StoryQuests.OwnerOf(id) != null) // (a scene of the server, its director near me left it unfinished: taken up at the step it got to, without his own camera and body)
            {
                if (p.IsDead() || !Near(p, s) || Missing(p, s) != null) return; // (its people: till the old lock runs out, or the first of them would stop it)
                resume = Mathf.Max(resume, StoryQuests.Committed(p, id)); // (its pulse may be two seconds old: a lasting step never twice)
                if (StoryQuests.Mine(id)) { _handing = true; if (Play(s, resume)) { StoryWatch.Resumed(); Track("сцену «" + id + "» перейнято з кроку " + (resume + 1)); return; } _handing = false; }
                return;
            }
            float stoppedAt;
            if (s != null && StoryQuests.Committed(p, id) > 0) { if (StoryQuests.OwnerOf(id) != null && _stopped.TryGetValue(id, out stoppedAt) && Time.time - stoppedAt < Retry) return; /* (he left it: one who watches takes it up first) */ if (StoryQuests.OwnerOf(id) != null && !Near(p, s)) return; /* (nor one far off: whoever comes near finishes it) */ if (!p.IsDead() && StoryQuests.Mine(id)) Finish(p, s, StoryQuests.Committed(p, id)); return; } // (stopped past its lasting step, or a relog: the rest done quietly, never played again)
            float when;
            if (_stopped.TryGetValue(id, out when) && Time.time - when < Retry) return;
            if (s == null) // (no file for it: «played» at once, said so — the load check names it too: StoryQuests.Validate)
            {
                Note("сцени «" + id + "» нема у файлах — вважаю зіграною");
                StoryBus.Raise("scene_done", id, 0);
                return;
            }
            string why, needs = StoryCond.Failing(p, s.Needs, out why);
            if (needs != null) // (said when what it waits for changes, not each pass: a why's detail after «:», a foe's metres, not counted)
            {
                if (_toldNeeds == null || !_toldNeeds.StartsWith(id + ":")) _needsSince = Time.time;
                string key = id + ":" + needs + ":" + (why != null && why.IndexOf(':') > 0 ? why.Substring(0, why.IndexOf(':')) : why);
                if (_toldNeeds != key)
                {
                    _toldNeeds = key; Track("сцена «" + id + "» чекає: " + needs + (why != null ? " — " + why : ""));
                    if (Time.time - LocTouch.UsedAt < 5f && Senses.Pressed(p)) RavenMsg.Center(p, Lang.T("Не зараз: поруч ворог.")); // (you touched it and nothing happened: why — the scene starts once it is quiet)
                }
                return;
            }
            Character foe = FoeNear(p);
            if (foe != null) // (it would stop at once: it waits for the fight's end as for its needs)
            {
                if (_toldNeeds == null || !_toldNeeds.StartsWith(id + ":")) _needsSince = Time.time;
                if (_toldNeeds != id + ":fight") { _toldNeeds = id + ":fight"; Track("сцена «" + id + "» чекає: бій — " + Localization.instance.Localize(foe.m_name) + " поруч"); }
                return;
            }
            if (_toldNeeds != null && _toldNeeds.StartsWith(id + ":")) { _toldNeeds = null; Track("сцена «" + id + "» дочекалась за " + (Time.time - _needsSince).ToString("0") + " с"); }
            string missing = Missing(p, s);
            if (missing != null)
            {
                Vector3 place;
                if (s.Place.Length > 0 && StoryWhere.Find(s.Place, out place) && StoryWhere.Apart(place, p.transform.position) > FarStop) _missingSince = Time.time; // (counted only while the player is there)
                if (_toldMissing != id + missing) { _toldMissing = id + missing; _missingSince = Time.time; Note("сцена «" + id + "» чекає: «" + missing + "» нема поруч або зайнята"); }
                else if (Time.time - _missingSince > CastWait && StoryQuests.Mine(id)) { _toldMissing = null; Note("«" + missing + "» так і не прийшла — сцена без неї"); Finish(p, s, 0); } // (never stuck for good: its lasting steps done, the story goes on)
                return;
            }
            _toldMissing = null;
            // its people in their places first (the user 2026-11-20: the Hall's last scene began with them on their way): each one a standing
            // word holds is THERE by that word's own rule (StoryHold.There) — or SetWait s have passed since the scene could first begin: one
            // who cannot get there (a way shut, a seat taken) never keeps a scene from playing, it begins with him where he is, and says so
            string unset = Unset(p, s);
            if (unset == null) UnsetSince.Remove(id);
            else
            {
                float since;
                if (!UnsetSince.TryGetValue(id, out since)) UnsetSince[id] = since = Time.time; // (the clock is this scene's own: begun once, whoever of its people is the late one this pass, whatever other scene is asked between its passes)
                bool waits = Time.time - since < SetWait;
                string told = id + (waits ? ":" : "!") + unset;
                if (_toldUnset != told) // (said when it changes, not each pass)
                {
                    _toldUnset = told;
                    if (waits) Track("сцена «" + id + "» чекає: «" + unset + "» ще не на своєму місці");
                    else Note("сцена «" + id + "» починається, хоч «" + unset + "» не на своєму місці (чекала " + SetWait.ToString("0") + " с)");
                }
                if (waits) return;
            }
            if (StoryQuests.OwnerOf(id) != null && !Near(p, s)) return; // (one far off must not hold the claim of a scene he cannot begin)
            if (!StoryQuests.Mine(id)) return; // (a scene of the server: one player at a time, asked last, when all else lets it begin)
            if (Play(s)) UnsetSince.Remove(id); // (played: tried again later, it waits for its people anew)
        }

        internal static bool Play(SceneDef s) { return Play(s, 0); }

        // near enough to where it plays (no place: anywhere)
        internal static bool Near(Player p, SceneDef s)
        {
            Vector3 place;
            return s.Place.Length == 0 || !StoryWhere.Find(s.Place, out place) || StoryWhere.Apart(place, p.transform.position) <= FarStop;
        }

        // ---------------------------------------------------------------- once is once
        // A scene that ran a lasting step (a thing given, a gate opened, a raven owned) is kept as begun in the character
        // (var scene.<id> = the step after it; a scene of the server: the world's, StoryQuests.Committed); stopped then, or the game left, it is not played again:
        // the lasting steps still ahead run quietly and it counts as played.
        private static string _toldMissing, _toldNeeds; // session: what the log was last told a scene waits for
        private static float _missingSince, _needsSince;
        internal static float CastWait = 180f; // (s a scene waits for its people before it is done without them)
        private static readonly Dictionary<string, float> UnsetSince = new Dictionary<string, float>(); // session: a scene that waits for its people to take their places → since when
        private static string _toldUnset; // session: what the log was last told of such a wait
        internal static float SetWait = 25f; // (s a scene waits for a held person to take its place before it begins all the same)
        // the first person it takes from the world that its standing word has not got to its place yet, else null
        internal static string Unset(Player p, SceneDef s)
        {
            foreach (string who in s.Cast)
            {
                NpcActor a = Actor(who, p.transform.position);
                if (a != null && !StoryHold.There(a.Zdo)) return who;
            }
            return null;
        }

        private static void Finish(Player p, SceneDef s, int from)
        {
            int n = 0;
            for (int i = Mathf.Max(0, from); i < s.Steps.Count; i++)
            {
                Step st = s.Steps[i];
                StepKind k;
                k = st.Kind; if (k == null || !k.Lasting || k.Begin == null) continue;
                if (st.If != null && !StoryCond.All(p, st.If)) continue;
                try { k.Begin(p, st); n++; } catch (Exception e) { Story.Log("scene finish: " + e.Message); }
            }
            StoryQuests.Commit(p, s.Id, 0);
            Note("сцена «" + s.Id + "» дограна тихо (почата раніше; кроків: " + n + ")");
            StoryBus.Raise("scene_done", s.Id, 0);
        }

        // the first person it takes from the world that is not near (or another player's scene holds), else null
        private static string Missing(Player p, SceneDef s)
        {
            foreach (string who in s.Cast)
            {
                NpcActor a = Actor(who, p.transform.position);
                if (a == null || HeldByOther(a)) return who;
            }
            return null;
        }

        // from a step of it (the author's window: «▶» by a step) — the steps before it not played
        // not begun while he is still in a portal or far from its place (a teleport lands seconds later: the scene played 150 m off
        // its place, its walk lost in the black — the studio's check 2026-11-04); false then, and the log says why
        internal static bool Play(SceneDef s, int from)
        {
            Player p = Player.m_localPlayer;
            if (p == null || s == null) return false;
            Vector3 place;
            if (p.IsTeleporting()) { Note("сцена «" + s.Id + "» не почалась: гравець ще в порталі"); return false; }
            if (s.Place.Length > 0 && StoryWhere.Find(s.Place, out place) && StoryWhere.Apart(place, p.transform.position) > FarStop)
            { NoteOnce(s.Id + ":far", "сцена «" + s.Id + "» не почалась: гравець за " + Mathf.RoundToInt(Utils.DistanceXZ(place, p.transform.position)) + " м від «" + s.Place + "»"); return false; }
            if (_now != null) Stop("інша сцена");
            _toldOnce = null;
            if (from > 0 && from < s.Steps.Count && s.Steps[from].Part != null) // (the author's «▶» on a row of a part: a part cannot be begun in its middle — from where the parts begin)
            {
                int go = s.Steps.FindIndex(x => x.Part == null && x.W.Length > 0 && x.W[0] == "parts");
                Note("сцена «" + s.Id + "»: крок " + (from + 1) + " — крок партії «" + s.Steps[from].Part + "»; граю від «parts go»" + (go < 0 ? " (нема такого кроку: з початку)" : ""));
                from = Mathf.Max(0, go);
            }
            _now = s; Lines(s, from); _anchor = p.transform.position; _hurtAt = Senses.HurtByFoeAt;
            _fightAt = -1f; _fightAll = 0f; _calmSince = -1f; _birdRinged = false; _saidUntil = 0f; // (FightWait: none waited yet; «wait said»: no line of this scene yet)
            Outcomes.Clear(); _endedAt = _at; // (played from a later step: the one before it never began here — nothing came of it to say)
            _ownOff = _handing; _handing = false; _server = StoryQuests.OwnerOf(s.Id) != null;
            StoryWatch.Begin(s);
            if (s.Alone || !s.AudienceSeen) StoryAlone.Begin(); else StoryAlone.End(); // (the players who watch are not in its shots unless it says «audience: seen»)
            StoryGait.Turns(p, true);
            if (!s.Roams) Gestures.Stand(p); // (one sitting on a bench or the ground is «calm» too — the user 2026-11-20 sat by the fire, the evening scene never came: a scene that takes him stands him up first; one that roams leaves him where he sits)
            StoryAudio.Prepare(s); // (its files read now: its steps find them there)
            if (from > 0) SetUp(p, s, from);
            _go = false; _held = false;
            Track("сцена «" + s.Id + "» почалась" + (from > 0 ? " з кроку " + (from + 1) : ""));
            if (Plugin.Waiting) { Plugin.RunOutVisit(); Track("сцена «" + s.Id + "»: ворон гри зі словами, яких ніхто не вислухав, відлітає — сцена не чекає"); } // (the scene's ordinary course, not a step done otherwise: as a Note it stood in a check's VERDICT beside the real one, 2026-11-12)
            StoryBus.Raise("scene_start", s.Id, 0);
            Next();
            return true;
        }

        // played from a later step (the author's window): what the steps before it leave behind, put at once (StepKind.Setup) —
        // he stands where his walks took him, the bars, the last shot, the thing in his hand, the bird on its seat; nothing
        // lasting (a thing given, a raven owned), no clip, no line, no pause
        private static bool _setup;
        private static void SetUp(Player p, SceneDef s, int from)
        {
            Gestures.SceneEnd(p, 0f); // (a clip left from before: off first — what the steps put on stays)
            for (int i = 0; i < from && i < s.Steps.Count; i++)
            {
                Step st = s.Steps[i];
                StepKind k;
                k = st.Kind; if (k == null || k.Setup == null || k.Lasting || NotHis(k)) continue;
                if (st.Part != null) continue; // (a part's step: the parts begin from their own first steps — «parts go» —, nothing of theirs is put beforehand)
                if (st.If != null && !StoryCond.All(p, st.If)) continue;
                try { k.Setup(p, st); if (k.Seen == StepSeen.Screen) StoryWatch.Step(i, false); } catch (Exception e) { Story.Log("scene set-up: " + e.Message); }
            }
            _waitFor = 0f;
        }

        private static void PropStep(Player p, Step st)
        {
            if (st.W[1] == "put") { StoryProps.Put(p); Hold(0.5f); return; }
            if (st.W[1] == "drop") { Hold(Mathf.Max(0.6f, StoryProps.Drop(p))); return; } // (what he holds falls from where it is and lies there till the scene's end)
            if (st.W[1] == "throw")
            {
                Vector3 to; bool body;
                if (!ThrowAt(st.W[2], out to, out body)) { Note("prop throw: нема місця «" + st.W[2] + "» — крок пропущено"); return; }
                Hold(StoryProps.Throw(p, to, body)); // (the step waits its flight)
                return;
            }
            string why;
            StoryProps.Ask ask = StoryProps.Read(st.W, 2, out why);
            why = ask != null ? StoryProps.Take(p, ask) : why;
            if (why != null) Note("prop: " + why);
            Hold(0.6f); // (the hand comes up to it)
        }

        internal static void Stop(string why)
        {
            if (_now == null) return;
            string id = _now.Id;
            Watching = false;
            EndStep("the scene stopped: " + why);
            _stopped[id] = Time.time;
            _reached = MainAt; // (stopped in a part's turn too: where its own line got to)
            _line = _main;
            _now = null; _at = -1;
            Let("its claim", () => StoryQuests.Unclaim(id));
            Release(true);
            Count(StopsOf, id); LastStopOf[id] = why;
            Track("сцена «" + id + "» перервана: " + why);
            StoryBus.Raise("scene_stop", id, 0);
        }

        private static void Done()
        {
            string id = _now.Id;
            Watching = false;
            _now = null; _at = -1;
            StoryQuests.Commit(Player.m_localPlayer, id, 0); // (played to the end: nothing left to finish)
            _reached = -1;
            Release(false);
            Track("сцена «" + id + "» скінчилась");
            StoryBus.Raise("scene_done", id, 0);
        }

        // ALL a scene holds, given back in ONE place — the review 2026-11-18: three lists of it (stopped, played out, the director given back)
        // that a new thing held had to be written into three times. Each a line of Kept: what it is, whether it is the director's own (his
        // camera, his walk, the thing in his hand — given back too when a scene of the server goes on without him: OwnOff), and how it is given
        // back (broken: stopped — at once; else played out — softly). A module adds its own (Holding: what is heard, a dome, a raven on a seat).
        private sealed class Kept { public string What; public bool His; public Action<Player, bool> Give; }
        private static readonly List<Kept> _kept = new List<Kept>(), _modules = new List<Kept>(); // (the core's, then the modules' — last, as what is heard always was)
        internal static void Holding(string what, bool his, Action<bool> give) { _modules.Add(new Kept { What = what, His = his, Give = (p, broken) => give(broken) }); }
        private static int _reached = -1; // (the step a stopped scene got to: its watchers' end)
        private static void Keeps()
        {
            if (_kept.Count > 0) return;
            Action<string, bool, Action<Player, bool>> k = (what, his, give) => _kept.Add(new Kept { What = what, His = his, Give = give });
            k("its watchers", false, (p, broken) => StoryWatch.End(broken, broken ? _reached : -1));
            k("the bird", false, (p, broken) => { HandOver(true); if (_bird != null && !BirdHomeAtEnd()) Act(broken ? "gone" : "free", ""); _bird = null; }); // (played out: left where it is, nobody's now — it flies off by itself a little later, StoryBird.Release; a place's raven: home)
            k("the falcons", false, (p, broken) => FalconsEnd(broken));
            k("its people", false, (p, broken) => FreeCast());
            k("its lanes", false, (p, broken) => _lanes.Clear());
            k("the thing in his hand", true, (p, broken) => StoryProps.Put(p));
            k("his gesture", true, (p, broken) => Gestures.SceneEnd(p, PoseFade));
            k("his sitting", true, (p, broken) => StopEmote(p));
            k("his walk", true, (p, broken) => StoryWalk.Stop());
            k("his look", true, (p, broken) => StoryLook.End());
            k("his turns", true, (p, broken) => StoryGait.Turns(p, false));
            k("his gait", true, (p, broken) => StoryGait.Set(p, "none"));
            k("the others hidden", false, (p, broken) => StoryAlone.End());
            k("the camera", true, (p, broken) => { if (broken) StoryCamera.Reset(); else StoryCamera.Off(1f); }); // (stopped: the player gets it back at once)
            k("the screen", true, (p, broken) => { if (broken) StoryScreen.Reset(); else { StoryScreen.Bars(false); StoryScreen.Fade(false, 1f); StoryScreen.GlowOff(); StoryScreen.DimOff(); } }); // (a scene that forgot «camera off», «fade in» or «glow 0»: given back softly)
            k("the line of its shots", false, (p, broken) => _lines.Clear()); // (which side of two in talk its camera worked from: StoryScenesCamera)
            k("its weather", false, (p, broken) => { bool weather = _weather, all = _weatherAll; _weather = _weatherAll = false; if (weather) StoryAir.Play("weather off", all); });
        }
        // a creature the scene itself plays (a module's: StoryBeasts — the skeleton that walks at you, the boar): no fight of yours, it neither
        // stops the scene nor keeps it waiting (FoeNear, FightWait); the user 2026-11-18
        internal static readonly List<Func<Character, bool>> Ours = new List<Func<Character, bool>>();
        private static bool OursOne(Character c) { foreach (Func<Character, bool> f in Ours) if (f(c)) return true; return false; }
        private static bool _weather, _weatherAll; // (this scene forced the weather: off with it — here, or in every game)

        // Each part alone (Let): the scene is already over and nobody calls this again, so a fault in one must not leave the camera, the black or the keys taken.
        // hisOnly: the director's own alone, at once (OwnOff: a scene of the server goes on without him)
        private static void Release(bool broken, bool hisOnly = false)
        {
            Player p = Player.m_localPlayer;
            Keeps();
            foreach (List<Kept> l in new[] { _kept, _modules })
                foreach (Kept k in l) { if (hisOnly && !k.His) continue; Kept one = k; Let(one.What, () => one.Give(p, broken)); }
            if (!hisOnly) _bird = null; // (whatever its line came to)
        }

        private static void Let(string what, Action give)
        {
            try { give(); }
            catch (Exception e) { Story.Log("scene release: " + what + ": " + e); }
        }

        // a fight at you: what stops a scene playing, and so what a scene never begins in, «needs: calm» or not (the user 2026-11-06:
        // heart_lantern began three times by a greyling at his heart and broke off each time in a second and a half)
        internal static float FoeStop = 12f;
        private static Character FoeNear(Player p)
        {
            Character foe = Senses.NearestFoe; // (the same foe «calm» asks of: Senses.Pressed; a boss too)
            return foe != null && !foe.IsDead() && !OursOne(foe) && Vector3.Distance(foe.transform.position, p.transform.position) < FoeStop ? foe : null; // (the scene's own creature: none)
        }

        // A scene that goes with him (roams: the Hall's letter, a raven's call) is not lost to a fight (the user 2026-11-17: the letter's raven
        // never came and its letter was skipped as the greydwarfs came, the stage passed with nothing read): it stops on the step it is at, its
        // bird circles over him (as the post's raven waits by one in a fight), and calm again for CalmFor s it goes on from that very step,
        // its clocks moved by the wait, its bird back to what it was told. A fight over FightMost s in all: stopped — the bird home (a place's
        // raven) or gone, the stage stands and plays it again after Retry s, to whoever its server gives it then (maybe another).
        // Only while it leaves him his keys (no shot, bars, black: StoryCamera.TakesPlayer; no walk: StoryWalk.On) — one that holds them stops as any scene (a server's: goes
        // on without his steps, and then never waits). Death: stopped (Tick). A fight here: a foe within FoeStop m (once waiting, within FoeStop +
        // CalmMore: one stepping on the edge does not toss the bird between its ring and him), or a blow in the last HurtCalm s. Calm again, the
        // step has at least ResumeRoom s more (its bird must get back before him). Cost: a distance and a few numbers a frame while it plays.
        internal static float CalmFor = 2f, FightMost = 300f, HurtCalm = 4f, CalmMore = 4f, ResumeRoom = 12f, FightRingR = 6f, FightRingUp = 5f;
        private static float _fightAt = -1f, _fightAll, _calmSince = -1f;
        private static bool FightWait(Player p)
        {
            Character foe = Senses.NearestFoe;
            bool fight = (foe != null && !foe.IsDead() && !OursOne(foe) && Vector3.Distance(foe.transform.position, p.transform.position) < FoeStop + (_fightAt >= 0f ? CalmMore : 0f)) || (Senses.HurtByFoeAt > _hurtAt + 0.01f && Time.time - Senses.HurtByFoeAt < HurtCalm); // (a blow since the scene began or went on: one just before it is no fight of its)
            if (fight)
            {
                _calmSince = -1f;
                if (_fightAt < 0f && (StoryCamera.TakesPlayer || StoryWalk.On)) return false; // (a shot, the bars, the black or a walk of the scene hold his keys: he could not fight — stopped as any scene)
                if (_fightAt < 0f) { _fightAt = Time.time; Note("сцена «" + _now.Id + "» чекає, поки скінчиться бій" + (foe != null && !foe.IsDead() ? " — " + foe.name.Replace("(Clone)", "") + " за " + Mathf.RoundToInt(Vector3.Distance(foe.transform.position, p.transform.position)) + " м" : " — удар") + (_bird != null ? "; птах кружляє над гравцем" : "")); } // (who holds it, said: the user 2026-11-20 thought the trail had hung)
                BirdWaits(true, p); // (asked each frame: a bird still on its way through a roof circles once it is out)
            }
            else if (_fightAt >= 0f && _calmSince < 0f) _calmSince = Time.time;
            if (_fightAt < 0f) return false;
            if (_fightAll + Time.time - _fightAt > FightMost) { Stop("бій не скінчився за " + Mathf.RoundToInt(FightMost) + " с: етап спробує знову"); return true; }
            if (_calmSince < 0f || Time.time - _calmSince < CalmFor) return true;
            float lost = Time.time - _fightAt; // (calm again: on from that step)
            _fightAll += lost; _fightAt = -1f; _calmSince = -1f;
            _stepAt = Mathf.Max(_stepAt + lost, Time.time - StepMax + ResumeRoom); if (_limit > 0f) _limit = Mathf.Max(_limit + lost, Time.time + ResumeRoom);
            if (_waitFor > 0f) _waitFor += lost; if (_until > 0f) _until += lost;
            _sceneAt += lost;
            foreach (Part x in _parts) // (the parts stood still with it: their steps' clocks too, by the same rule)
            {
                if (!x.On || x.Over || x.Pos < 0) continue;
                x.StepAt = Mathf.Max(x.StepAt + lost, Time.time - StepMax + ResumeRoom); if (x.Limit > 0f) x.Limit = Mathf.Max(x.Limit + lost, Time.time + ResumeRoom);
                if (x.WaitFor > 0f) x.WaitFor += lost; if (x.Until > 0f) x.Until += lost; if (x.SaidUntil > 0f) x.SaidUntil += lost;
            }
            _hurtAt = Senses.HurtByFoeAt; // (that fight's blows are over: not a «удар» for the old stop below)
            BirdWaits(false, p);
            Note("бій скінчився: сцена «" + _now.Id + "» далі");
            return false;
        }

        // the people it holds: still its — the ones made beforehand too (unrenewed, the sisters' locks ran out while they waited and the scene let
        // them go the moment they came, 2026-11-14); every LockRenew s
        private static void RenewLocks()
        {
            if ((_cast.Count > 0 || _ready.Count > 0) && Time.time >= _renewAt) { _renewAt = Time.time + LockRenew; foreach (NpcActor a in _cast.Values) Lock(a, true); foreach (NpcActor a in _ready.Values) Lock(a, true); }
        }

        // every frame (StoryQuests.Tick)
        internal static void Tick()
        {
            if (_now == null) return;
            Player p = Player.m_localPlayer;
            if (p == null) { Stop("гравця нема"); return; } // (the same full stop as any other: its people let go, the camera given back)
            StoryAlone.Tick();
            if (_now.Roams && !_ownOff) // (a scene that goes with him — a letter: it waits a fight out, its bird over him; one already given back to him goes its old way)
            {
                if (p.IsDead()) { Stop("смерть"); return; } // (death stops it whole, server's or not: never on through his steps unseen — the letter skipped)
                if (FightWait(p)) { if (_now != null) { StoryQuests.Renew(_now.Id); StoryWatch.Tick(); RenewLocks(); } return; } // (still his: its claim and its people renewed)
            }
            if (!_ownOff) // (one's own scene ends with these; a scene of the server only gives him back what it held of him, and goes on)
            {
                string hit = p.IsDead() ? "смерть" : Senses.HurtByFoeAt > _hurtAt + 0.01f ? "удар" : null;
                Character foe = hit == null ? FoeNear(p) : null;
                if (foe != null) hit = "ворог поруч: " + Localization.instance.Localize(foe.m_name);
                if (hit != null) { if (_server) OwnOff(p, hit); else { Stop(hit); return; } }
            }
            if (StoryWalk.On) _anchor = p.transform.position; // (the scene itself walks him: not his walking off, however far — the review 2026-11-14)
            if (!_now.Roams && Utils.DistanceXZ(_anchor, p.transform.position) > FarStop) { Stop("гравець відійшов від місця сцени"); return; } // (from where it began, or where the scene itself put him: MovedTo — the bird comes from afar; a scene that roams goes with him)
            if (!_ownOff) StoryLook.Tick(p);
            HandOver(false);
            StoryQuests.Renew(_now.Id);
            StoryWatch.Tick();
            RenewLocks();
            BusyTick();
            if (_held) { if (_go) { _go = false; _held = false; Next(); } } // (step by step: the author's «Далі»)
            else if (_at < 0 || _at >= _now.Steps.Count) { Stop("сцена не почалась як слід: нема кроку " + (_at + 1)); return; } // (its beginning broke off before its first step — Play threw: stopped whole here, not thrown at every frame with every stage of the story standing behind it)
            else Turn(p);
            if (_now != null) PartsTurn(p);
        }

        // the line whose turn it is, a frame of it: its step cut off at its limit, still going, or the next one
        private static void Turn(Player p)
        {
            Step cur = _now.Steps[_at];
            if (Time.time > (_limit > 0f ? _limit : _stepAt + Mathf.Max(StepMax, _waitFor - _stepAt + 5f))) // (its own limit, else the general one on its clock)
            {
                if (cur.Kind != null && cur.Kind.Late != null) cur.Kind.Late(cur);
                Track("крок «" + cur.Line + "» задовгий — далі"); Count(LatesOf, _now.Id); LastLateOf[_now.Id] = cur.Line;
                EndStep("CUT OFF at its time limit");
                Next();
                return;
            }
            if (Waiting(p)) return;
            if (_line == _main && StepHolds) { _held = true; return; } // (this step done: the next one when the author says)
            Next();
        }

        // ---------------------------------------------------------------- the lines of a scene: its own, and its parts
        // A line: which of the scene's steps are its, where it is in them, and — while another line has its turn — its step's own clock and
        // walk. The director's statics (_at, _stepAt, _waitFor, _until, _limit, the walker of «npc go», «mark», «says») are those of the line
        // whose turn it is: Swap puts the one's away and the other's in, so every kind of step plays in a part as it does in the scene's line,
        // unchanged. The scene's own line (_main) is the one in turn whenever nothing else is said: every frame ends on it.
        private sealed class Part
        {
            public string Name = ""; public readonly List<int> Idx = new List<int>(); public int Pos = -1; public bool On, Over;
            public float StepAt, Until, WaitFor, Limit, WalkNear, WalkBegan, WalkWalkFrom, MarkFrom, SaidUntil; public int EndedAt = -1;
            public NpcActor Walker; public Vector3 WalkTo, WalkFrom; public bool WalkRanAny; public string WalkSaid;
        }
        private static Part _main = new Part(), _line = _main;
        private static readonly List<Part> _parts = new List<Part>();
        private static readonly HashSet<string> _cues = new HashSet<string>(); // (the signs given in this scene: «cue», «wait cue»)
        internal static readonly List<int> PartBegun = new List<int>(); // (the steps the parts of the scene playing began, in their order: the studio's check gives each a row — its own line it reads off At)
        private static float _sceneAt; // (when it began: the clock of «wait music» in a scene with no music of its own)
        private static int MainAt { get { Part m = _line == _main ? null : _main; return m == null ? _at : m.Pos >= 0 && m.Pos < m.Idx.Count ? m.Idx[m.Pos] : -1; } }

        // a scene begins: its steps into its lines; its own line set to play from that step of the scene (0: its first)
        private static void Lines(SceneDef s, int from)
        {
            _main = _line = new Part { Name = "main", On = true };
            _parts.Clear(); _cues.Clear(); PartBegun.Clear(); _sceneAt = Time.time;
            for (int i = 0; i < s.Steps.Count; i++)
            {
                string name = s.Steps[i].Part;
                if (name == null) { _main.Idx.Add(i); continue; }
                Part part = null;
                foreach (Part x in _parts) if (x.Name == name) { part = x; break; }
                if (part == null) _parts.Add(part = new Part { Name = name });
                part.Idx.Add(i);
            }
            int k = 0;
            while (k < _main.Idx.Count && _main.Idx[k] < from) k++;
            _main.Pos = k - 1;
            _at = _main.Pos >= 0 ? _main.Idx[_main.Pos] : -1;
            _walker = null; _stepAt = Time.time; _waitFor = _until = _limit = 0f;
        }

        // «parts go»: every part begins (once: asked again, those going go on)
        private static void PartsGo()
        {
            foreach (Part x in _parts) if (!x.On) { x.On = true; Track("  партія «" + x.Name + "» пішла (" + x.Idx.Count + " кроків)"); }
        }

        // the turn passes to another line: this one's step put away, that one's in
        private static void Swap(Part to)
        {
            Part f = _line;
            if (f == to) return;
            f.StepAt = _stepAt; f.Until = _until; f.WaitFor = _waitFor; f.Limit = _limit; f.EndedAt = _endedAt; f.MarkFrom = _markFrom; f.SaidUntil = _saidUntil;
            f.Walker = _walker; f.WalkTo = _walkTo; f.WalkFrom = _walkFrom; f.WalkNear = _walkNear; f.WalkBegan = _walkBegan; f.WalkWalkFrom = _walkWalkFrom; f.WalkRanAny = _walkRanAny; f.WalkSaid = _walkSaid;
            _line = to;
            _stepAt = to.StepAt; _until = to.Until; _waitFor = to.WaitFor; _limit = to.Limit; _endedAt = to.EndedAt; _markFrom = to.MarkFrom; _saidUntil = to.SaidUntil;
            _walker = to.Walker; _walkTo = to.WalkTo; _walkFrom = to.WalkFrom; _walkNear = to.WalkNear; _walkBegan = to.WalkBegan; _walkWalkFrom = to.WalkWalkFrom; _walkRanAny = to.WalkRanAny; _walkSaid = to.WalkSaid;
            _at = to.Pos >= 0 && to.Pos < to.Idx.Count ? to.Idx[to.Pos] : -1;
        }

        // each part going, a frame of it, after the scene's own line. A step of a part may stop the scene (its person gone), and the stop may
        // begin another (a stage's next): then nothing of this one is touched any more
        private static void PartsTurn(Player p)
        {
            SceneDef was = _now;
            for (int i = 0; i < _parts.Count && _now == was; i++)
            {
                Part part = _parts[i];
                if (!part.On || part.Over) continue;
                Swap(part);
                try { if (part.Pos < 0) Next(); else Turn(p); }
                catch (Exception e) { part.Over = true; Story.Log("scene part «" + part.Name + "»: " + e); } // (a part that broke ends alone: the scene and its other parts go on)
                if (_now == was) Swap(_main);
            }
        }

        // a person's work («npc <who> busy <clip>»): whenever no clip of ours plays on its body and it does not walk, its work begins again.
        // Looked at BusyEvery s apart, a person asked not sooner than BusyAgain s after its last asking (a body run by another game shows the
        // clip a moment later). Cost: a few people, a few numbers each, four times a second, only while a scene with work plays
        internal static float BusyEvery = 0.1f, BusyAgain = 0.25f, /* (the user 2026-11-21: a second between two rounds of a work read as «затримки»: the next round follows the last at once) */ BusyAfar = 1.5f, BusyLoop = 600f; // (BusyAfar: s more for a body run by another game — its clip shows here later; BusyLoop: the term of a work that loops, else NpcBody.LoopTerm eases it off each minute)
        internal static int BusyTries = 3; // (asked so many times with no clip of ours ever seen on the body: its body has no such clip — said once, the work taken off)
        private static readonly Dictionary<string, int> _busyMiss = new Dictionary<string, int>(); // session: how many askings of each came to nothing
        // any order of the scene to a person: its work does not come over it at once (StoryScenesCast.OnPerson)
        private static void BusyLater(string who, NpcActor a) { if (_busy.ContainsKey(who)) _busyAsked[who] = Time.time + (a != null && !a.Owned ? BusyAfar : 0f); }
        private static readonly Dictionary<string, string> _busy = new Dictionary<string, string>();   // (its word in the scene → its work's clip)
        private static readonly Dictionary<string, float> _busyAsked = new Dictionary<string, float>(); // session: when each was last told to
        private static float _busyAt;
        private static void BusyTick()
        {
            if (_busy.Count == 0 || Time.time < _busyAt) return;
            _busyAt = Time.time + BusyEvery;
            List<string> gone = null;
            foreach (KeyValuePair<string, string> kv in _busy)
            {
                NpcActor a; float asked;
                if (!_cast.TryGetValue(kv.Key, out a) || a == null) continue;
                if (_busyAsked.TryGetValue(kv.Key, out asked) && Time.time - asked < BusyAgain) continue;
                NpcMind.Plan plan = a.Owned && a.Mind != null ? a.Mind.Of(NpcMind.Layer.Scene) : null;
                Character c = a.GetComponent<Character>();
                if (c == null || (plan != null && plan.Kind == NpcMind.Task.Go) || c.GetVelocity().sqrMagnitude > 0.04f) continue; // (on its way: its work waits)
                BodyAnimator b = BodyAnimator.Of(c);
                float m; int miss;
                if (b != null && b.CodeClip(out m) != null) { _busyMiss.Remove(kv.Key); continue; } // (something of ours plays on it; none of ours ever played: no animator yet — free)
                _busyMiss.TryGetValue(kv.Key, out miss);
                if (miss >= BusyTries) { (gone ?? (gone = new List<string>())).Add(kv.Key); continue; }
                _busyMiss[kv.Key] = miss + 1;
                _busyAsked[kv.Key] = Time.time + (a.Owned ? 0f : BusyAfar);
                PAnimClip work = BodyClips.Find(kv.Value);
                a.Order("clip " + kv.Value + (work != null && work.Loop ? " " + F(BusyLoop) : ""), NpcMind.Layer.Scene);
            }
            if (gone != null) foreach (string who in gone) { Note("npc busy: «" + _busy[who] + "» так і не заграв на «" + who + "» за " + BusyTries + " спроби (нема такого кліпу для її тіла?) — справу знято"); _busy.Remove(who); _busyMiss.Remove(who); }
        }

        // the director of a scene of the server is hit, or dies: what it held of him given back at once, it goes on without its Own steps (StoryWatch)
        private static bool _ownOff, _handing, _server;
        // a step of the director's own — his body, his camera, his screen and ears — while he is given back: left out
        private static bool NotHis(StepKind k) { return _ownOff && (k.Seen == StepSeen.Own || k.Seen == StepSeen.Screen); }
        // it holds the picture now, the director's and that of those who watch: a scene that takes it at all, he not given back, not looked at from outside
        internal static bool Cinema { get { return _now != null && _now.Cinema && !_ownOff && !Watching; } }
        private static void OwnOff(Player p, string why)
        {
            _ownOff = true;
            Note("сцена «" + _now.Id + "» йде далі без кроків гравця: " + why);
            StoryWatch.Now(); // (those who watch are given back with him: said at once)
            Release(true, true); // (his own alone, at once: its music, sounds and weather are everyone's, and it goes on. A «mix», «mute» of the scene stays till its end: no scene of the server uses them)
        }

        // the author's step-by-step (F9 «Сцени»): each step played, then held till «Далі» (no time limit while held). Never on a
        // black screen: there is nothing to look at, so what happens under a fade goes on by itself
        internal static bool StepMode;
        private static bool _held, _go;
        private static bool StepHolds { get { return StepMode && !StoryScreen.Dark; } }
        internal static bool Held { get { return _held; } }
        // each scene: how many times it stopped, how many of its steps were cut off at their time limit, and the last of each (a check compares
        // its own scene's, before and after: the same reason twice read «OK», and another scene's stop, after, hid this one's)
        internal static readonly Dictionary<string, int> StopsOf = new Dictionary<string, int>(), LatesOf = new Dictionary<string, int>();
        internal static readonly Dictionary<string, string> LastStopOf = new Dictionary<string, string>(), LastLateOf = new Dictionary<string, string>();
        internal static int CountOf(Dictionary<string, int> d, string id) { int c; return d.TryGetValue(id, out c) ? c : 0; }
        // the steps that threw as they began and so did nothing, each with what it threw (Begin). The scene goes on without them, and it looked
        // whole: on 2026-11-11 every camera step of a scene threw, it played with no shot at all, and its check's VERDICT said nothing of it —
        // the log's line was an ordinary one. The check counts these before and after its run (StudioScene); kept short: the last 40
        internal static readonly List<string> Broke = new List<string>();
        // what each step of the scene played last came to (its index → «4.1 s: ran 14 m, stopped 0.3 m off»), said as the step ends: its kind's Done,
        // how long it took, cut off or not (EndStep). Read by the studio's scene check (a row a step, the VERDICT's lines under their step) and written to
        // the log — review 2 2026-11-10: «walk HELD» was known, but whether she ran, how far, how near she stopped had to be read off the frames
        internal static readonly Dictionary<int, string> Outcomes = new Dictionary<int, string>();
        private static int _endedAt = -1;
        private static void EndStep(string how)
        {
            if (_now == null || _at < 0 || _at >= _now.Steps.Count || _endedAt == _at) return;
            _endedAt = _at;
            Step st = _now.Steps[_at];
            string said = null;
            if (st.Kind != null && st.Kind.Done != null && !NotHis(st.Kind))
            {
                try { said = st.Kind.Done(Player.m_localPlayer, st); }
                catch (Exception e) { said = "(what came of it could not be read: " + e.Message + ")"; Story.Log("scene step outcome: " + e); }
            }
            string text = F(Time.time - _stepAt) + " s" + (!string.IsNullOrEmpty(said) ? ": " + said : "") + (how != null ? " — " + how : "");
            Outcomes[_at] = text;
            Track("     → " + text);
        }

        // what a clip step came to: the clip of ours on that body now and how far it is in (one read)
        private static string ClipOn(Character c, string clip)
        {
            BodyAnimator a = c != null ? BodyAnimator.Of(c) : null;
            float m = 0f; PAnimClip on = a != null ? a.CodeClip(out m) : null;
            if (on == null) return "NO clip of ours on the body now (" + clip + " asked)";
            return (on.Name == clip ? "" : "another clip on it, " + on.Name + " (" + clip + " asked): ") + "played " + F(m) + " of " + F(on.Length) + " s" + (on.Loop ? ", loops" : "");
        }

        // what a mark step came to
        private static string MarkSaid(Character c, string mark)
        {
            if (c == null) return "nobody to read the mark on";
            float t = MarkTime(c, mark);
            return t < 0f ? "no «mark " + mark + "» on the clip on it" : MarkAhead(c, mark) ? "cut off before the mark (" + F(t) + " s into the clip)" : "the mark passed";
        }
        private static void Count(Dictionary<string, int> d, string id) { d[id] = CountOf(d, id) + 1; }

        // the author's look from outside (the window «Режисер»): the scene plays on his own body as ever, but its shots, bars and
        // fades are not taken: his free camera stays his, and he sees it as the others near see it. Till the scene ends or stops
        internal static bool Watching;
        internal static void Go() { if (_held) _go = true; }


        // the current step still going? its kind says (none: its clock)
        private static bool Waiting(Player p)
        {
            Step st = _now.Steps[_at];
            StepKind k = st.Kind;
            if (k == null || NotHis(k)) return false; // (his own step, and he is given back: nothing to wait for)
            return k.Busy != null ? k.Busy(p, st) : Time.time < _waitFor;
        }

        // a person's walk over, and it is not there: never in silence — what follows (a clip that touches the thing) plays in the air
        private static void WalkerShort()
        {
            float far = Utils.DistanceXZ(_walker.transform.position, _walkTo);
            _walkSaid = (_walkRanAny ? "ran " : "walked ") + F(Utils.DistanceXZ(_walkFrom, _walker.transform.position)) + " m in " + F(Time.time - _walkBegan) + " s, stopped " + F(far) + " m off its spot (asked within " + F(_walkNear) + ")"
                + (_walkRanAny && _walkWalkFrom > 0.3f ? "; walked the last " + F(_walkWalkFrom) + " m" : "") + (_walker.Owned ? "" : " (its body run by another game)"); // (the step's outcome: Done)
            if (far <= _walkNear + 0.3f) return;
            _walkSaid += " — HELD short";
            StoryWalk.Helds++;
            Story.Log("npc go: HELD, " + _walker.Id + " stopped " + far.ToString("0.00") + " m from its spot (it was to come within " + _walkNear.ToString("0.00") + "): the scene goes on");
        }

        private static bool Walking()
        {
            if (_walker == null) return false;
            { Character c = _walker.GetComponent<Character>(); bool running = c != null && c.IsRunning(); if (running) _walkRanAny = true; else if (_walkRanAny && _walkWalkFrom < 0f) _walkWalkFrom = Utils.DistanceXZ(_walker.transform.position, _walkTo); } // (a run that falls to a walk near the end: how far out — the outcome says)
            bool walking;
            if (_walker.Owned) { NpcMind.Plan walk = _walker.Mind != null ? _walker.Mind.Of(NpcMind.Layer.Scene) : null; walking = walk != null && walk.Kind == NpcMind.Task.Go; }
            else walking = Utils.DistanceXZ(_walker.transform.position, _walkTo) > _walkNear + 1f;
            if (walking) return true;
            WalkerShort();
            _walker.Order("stay", NpcMind.Layer.Scene);
            _walker = null;
            return false;
        }

        private static void Next()
        {
            Player p = Player.m_localPlayer;
            while (_now != null)
            {
                SceneDef was = _now;
                EndStep(null);
                _line.Pos++;
                if (_line.Pos >= _line.Idx.Count)
                {
                    if (_line == _main) Done(); // (the scene's own line: the scene is over, whatever its parts still do)
                    else { _line.Over = true; _at = -1; Track("  партія «" + _line.Name + "» скінчилась"); }
                    return;
                }
                _at = _line.Idx[_line.Pos];
                Step st = _now.Steps[_at];
                string of = _line == _main ? "" : "[" + _line.Name + "] ";
                _stepAt = Time.time; _limit = 0f; _waitFor = 0f; _until = 0f; // (the step's own clock, clean: a step that knows how long it may take sets its own)
                if (st.If != null && !StoryCond.All(p, st.If)) { Track("  " + (_at + 1) + ". " + of + "пропущено (умова): " + st.Line); _endedAt = _at; continue; } // (never begun: nothing came of it)
                Track("  " + (_at + 1) + ". " + of + st.Line);
                if (_line != _main) PartBegun.Add(_at);
                Begin(p, st);
                if (_now != was) return; // (the step stopped the scene — and the stop may have begun another: not this line's any more)
                if (st.Kind != null && st.Kind.Lasting) StoryQuests.Commit(p, _now.Id, _at + 1); // (begun for good: the steps from the next one on are what a stop leaves)
                if (Waiting(p) || (_line == _main && StepHolds)) return; // (step by step: one at a time, even the ones that do not wait)
            }
        }

        // ---------------------------------------------------------------- the author's editing (F9 «Сцени»)
        // a scene file's text as it is used now (the author's copy, else the built-in one); saved as his copy and read again
        internal static string FileText(string file) { return StoryFiles.Text(StoryFiles.Scenes, Folder, Blueprint.On, file); }
        // where a step's line stands in the file's lines now, -1: it is another line there — the file changed since it was read (by hand, by
        // another chat), so its numbers are old: read again, nothing written (2026-11-04: a shot written over the scene's «needs»)
        internal static int FileLine(Step st, List<string> lines)
        {
            int i = st.Num - 1;
            if (i >= 0 && i < lines.Count && StoryFiles.Bare(lines[i]) == st.Line) return i;
            Reload();
            return -1;
        }
        internal const string FileMoved = "Файл сцени змінився: перечитав його, спробуй ще раз.";

        internal static string SaveFile(string file, string text) { string why = StoryFiles.Save(StoryFiles.Scenes, Folder, file, text); if (why == null) Reload(); return why; }

        private static string Rest(string text, int from)
        {
            string[] w = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length <= from) return "";
            int i = 0, pos = 0;
            for (; i < from; i++) pos = text.IndexOf(w[i], pos) + w[i].Length;
            return text.Substring(pos).Trim();
        }

        // «letter <від кого> [seal <печатка>] :: <текст>» → { from, text, seal (null: none named) }; null: not so written
        private static string[] LetterParts(string text)
        {
            string rest = Rest(text, 1);
            int sep = rest.IndexOf("::", StringComparison.Ordinal);
            if (sep <= 0) return null;
            string from = rest.Substring(0, sep).Trim(), words = rest.Substring(sep + 2).Trim(), seal = null;
            int si = from.LastIndexOf(" seal ", StringComparison.Ordinal);
            if (si > 0) { seal = from.Substring(si + 6).Trim(); from = from.Substring(0, si).Trim(); }
            return from.Length > 0 && words.Length > 0 && (seal == null || seal.Length > 0) ? new[] { from, words, seal } : null;
        }

        // a story person's or a seal's id: Latin letters, digits, «_» (a name in words is not)
        private static bool LatinId(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char ch in s) if (ch > 127 || !(char.IsLetterOrDigit(ch) || ch == '_')) return false;
            return true;
        }

        private static void Begin(Player p, Step st)
        {
            StepKind k = st.Kind; // (its verb's own entry, where it has verbs)
            if (k == null || k.Begin == null || NotHis(k)) return;
            try { k.Begin(p, st); if (k.Seen == StepSeen.Screen) StoryWatch.Step(_at, true); } // (the picture and what is heard: the same on the game of each one who watches)
            catch (Exception e) { Track("крок «" + st.Line + "» зламався: " + e.Message + " — далі"); Story.Log("scene step: " + e); Broke.Add("«" + st.Line + "»: " + e.Message); if (Broke.Count > 40) Broke.RemoveAt(0); } // (the scene goes on: a step's own limit never comes for one that never began)
        }

        // the player himself in a scene: turned to look at something, a gesture (his own game: he is the owner)
        private static readonly System.Reflection.FieldInfo FLookYaw = HarmonyLib.AccessTools.Field(typeof(Character), "m_lookYaw");
        internal static float PoseFade = 0.4f;

        // when the clip on him has that mark; below zero: it has none
        private static float MarkTime(Character p, string name)
        {
            BodyAnimator a = BodyAnimator.Of(p);
            PAnimClip c = a != null ? BodyClips.Find(a.ClipIn(2)) : null;
            if (c != null) foreach (PAnimEvent m in c.Marks) if (m.Sound == name) return m.T;
            return -1f;
        }

        private static bool MarkAhead(Character p, string name)
        {
            float t = MarkTime(p, name);
            return t >= 0f && BodyAnimator.Of(p).TimeIn(2) < t;
        }

        // «npc <who> mark»: till the clip on it passes it; the clip may take a moment to come (it is run on another game)
        internal static float MarkComes = 1f;
        private static float _markFrom;
        private static bool NpcMarkBusy(Player p, string[] w)
        {
            if (Time.time >= _waitFor) return false;
            NpcActor a = Actor(w[1], p.transform.position);
            Character c = a != null ? a.GetComponent<Character>() : null;
            if (c == null) return false;
            if (MarkTime(c, w[3]) >= 0f) return MarkAhead(c, w[3]);
            if (Time.time < _markFrom + MarkComes) return true;
            Note("npc mark: у кліпі на «" + w[1] + "» нема мітки «" + w[3] + "» — крок пропущено");
            return false;
        }

        // a line said in the world, while a shot holds the camera (the game's own screen, where it shows, is hidden then): as a subtitle too
        internal static void Spoken(string who, string line)
        {
            Player me = Player.m_localPlayer;
            line = Grammar.Fix(line, me); // (the forms for the director; the game's own lines do this where they are shown)
            if (me != null && line.IndexOf("{name}", StringComparison.Ordinal) >= 0) line = line.Replace("{name}", me.GetPlayerName()); // (a person greets him by name — Eyvind running past, chapter 2: the bubble over it says the same, NpcActor.Say)
            if (StoryCamera.Active) StoryScreen.Sub(who, line, RavenSpeech.Seconds(line, RavenSpeech.Pace.Scene) + 0.4f);
            StoryWatch.Line(who, line); // (those near: as a subtitle, wherever they stand)
        }


        // ---------------------------------------------------------------- small things
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }
        private static Vector3 PV(string[] a, int i) { return new Vector3(F(a[i], 0f), F(a[i + 1], 0f), F(a[i + 2], 0f)); }

        // what a sound follows: the bird, a person of the story, else the player (StoryAudio)
        internal static Transform FollowOf(string place)
        {
            Player p = Player.m_localPlayer;
            if (place == "bird") return _bird != null ? _bird.transform : null;
            if (place != null && place.StartsWith("npc.") && p != null) { NpcActor a = Actor(place.Substring(4), p.transform.position); return a != null ? a.transform : null; }
            if (place == null || place == "player") return p != null ? p.transform : null;
            return null;
        }

        // said once till a scene begins (asked twice a second by a stage that waits)
        private static string _toldOnce;
        private static void NoteOnce(string key, string s) { if (_toldOnce == key) return; _toldOnce = key; Note(s); }

        // Two kinds of a director's line. Track: the scene's ordinary course — it began, a step, what the step came to, it ended. Note: the scene did
        // NOT do as its file says — a step skipped for want of a place, a thing taken from a piece that is not there, a shot from another side.
        // A note is also kept in Amiss with the step it fell in (the last 40): the studio's scene check counts them before and after its run and says
        // each in its VERDICT. Till 2026-11-12 both were one line of the log: «prop: «prologue_house.knyha» нема поруч» stood among the track's lines
        // while two days of grip runs measured a book taken from before the holder, and the VERDICT said nothing of it. A new line that is the
        // ordinary course is a Track; anything else a Note — so what is forgotten shows in a check rather than hides.
        internal static readonly List<string> Amiss = new List<string>();
        internal static int AmissSaid; // (how many were ever said: the list keeps only the last 40, so a check counts by this)
        internal static void Note(string s)
        {
            AmissSaid++;
            Amiss.Add((_now != null ? "«" + _now.Id + "» " + (_at >= 0 && _at < _now.Steps.Count ? (_at + 1) + ". " + _now.Steps[_at].Line + " — " : "") : "") + s); // (whose step: the story may begin the next scene while a check still measures the tail of its own)
            if (Amiss.Count > 40) Amiss.RemoveAt(0);
            Track(s);
        }
        internal static void Track(string s)
        {
            Recent.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (Recent.Count > 60) Recent.RemoveAt(0);
            StoryQuests.Note(s); // (the track's log and the game's log)
        }
    }
}
#endif

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
    //   death · a fight (hit, or a foe at you) · going off more than 60 m from where it plays
    // and its actors vanish; the stage plays it again later (after Retry seconds, when «needs» hold again).
    // Every step has its own time limit: a scene never hangs.
    //
    // Files: assets/story/scenes/*.txt — read where StoryFiles says (built in; on the author's machine the mod's own folder,
    // edited in F9 «Сцени»). This file: playing; reading: StoryScenesRead; the kinds of steps: StoryScenesKinds; the player's, the bird's, the people's
    // verbs: StoryScenesPlayer / Bird / Cast; the camera: StoryScenesCamera.
    //   [scene raven_call]
    //   needs: calm                         conditions to begin (StoryCond)
    //   place: prologue_house               where it is filmed (the author's shots of that place: StoryShots)
    //   roams: yes                          it goes with the player wherever he walks (a raven over him): not stopped when he is
    //                                       FarStop m from where it began — a foe, a blow, death still stop it (2026-11-08: the prologue's
    //                                       nudge died three times as the player walked on while Munin flew 60 m to him)
    //   alone: yes                          a scene of one's own: the other players are not drawn in this game while
    //                                       it plays (StoryAlone — nobody walks into the shot)
    //   guard: 30 | off                     the quiet round the event (StoryHush): its full size in m; none — by the
    //                                       place's size; off — none. It grows round the place as the player comes on
    //                                       the stage that leads here; no place — round the player while it plays
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
    //   bird gone                           a puff of feathers
    //   bird come … nowait                  the scene goes on as it flies in (then «bird circle»: it never sits)
    //   bird come … as hugin|munin          your own raven's picture (plain black, its size asleep); … look <id> another look of assets/story/looks
    //   bird come … at <place> side -0.5    on top of a thing, this far right (below zero: left) of its middle as you see it
    //   bird shoulder [hugin|munin]         onto your shoulder (its kind's); then «own <kind> here» makes it yours
    //   say hugin|munin <текст>             your own raven says it (only if it is on your shoulder)
    //   msg <текст>                         a line in the middle of the screen
    //   camera <from> [look <what>] [blend 2] [fov 50]   a shot (StoryCamera): from a place, «behind», «over» or «face» (close up);
    //                                       looking at player (default), bird, sky (up before you) or a place; the player stands still
    //   camera on <who> [close|medium|full|wide] [front|left|right|back] [low|high] [track]   one in the picture, in words: how near, from
    //                                       which side of it, lower or higher; track: it goes with it (who: player, bird, npc.<who>, a place)
    //   camera shoulder <a> <b> [left|right] over a's shoulder at b · camera two <a> <b> [left|right]   both from the side; a scene keeps to
    //                                       one side of the line between two (the 180° rule) unless a line says left|right
    //   … push <m> <s> [wait]               on these three: the lens goes m nearer over s seconds (all the camera's words: StoryScenesCamera)
    //   camera off [blend 1.5]              the camera back to the player (also when the scene ends or stops)
    //   bars on|off · sub <текст>           the black bars; a subtitle (waits while it is read)
    //   weather <env> [s] [all] · weather off [all]     forced weather (ThunderStorm, Rain, Misty, Clear …); off when the scene ends or stops
    //   bolt near|ahead [n] [all] · bolt <place> [n] [spread] [all]   bolts from the sky (StoryAir; ahead = before the eyes)
    //   thor [all] · shake <strength> [all]
    //                                       «all»: in every game of the world, round each player
    //   fade out|in [1.5]                   the whole picture to black and back (waits)
    //   player face <place> · player emote <gesture>|stop    the player himself turns softly on the spot and looks there (does not wait) / a gesture
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
    //   npc <who> go <place|player|bird|npc.x> [run] [near 1.6]   walks there — runs when farther than RunFrom m (waits till it is near: NpcMind.Reach)
    //   npc <who> go <place.piece> before <m> [run]   to that spot exactly, square before the thing's middle on its outer side: a clip that follows
    //                                       touches it. Not come to it: said (the log, the scene check's VERDICT)
    //   npc <who> face player|bird|<place>  turns and keeps looking (does not wait)
    //   npc <who> say <текст>               a line over it, as it says it to you (waits while it is read)
    //   npc <who> emote <gesture> [loop]    a gesture (wave, bow, kneel, cry…; loop: held)
    //   npc <who> mark <name> [seconds]     waits till the clip on that person passes that «mark» (BodyClips), at most 30 s: never a «wait» that counts a clip's seconds
    //   npc <who> sit <place> · stay · free sits by a place · stands · back to its own day (free: «вільно», a standing word ends too)
    //   do npc <who> hold <place> …         a word that outlives the scene (StoryHold): what it does when the scene lets it go
    //   do npc <who> free                   the standing word gone; one the scene holds stays the scene's till its end
    //   npc <who> spawn <place>             made for this scene at a place (its template id), facing you, coming in slowly
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
        internal sealed class Step { private StepKind _kind; internal StepKind Kind { get { return _kind ?? (_kind = KindOf(W)); } } /* (its kind or verb, found once: the director asks every frame) */ public string Text = "", Line = ""; public int Num; public List<Cond> If, Until; public float UntilSec = 10f; public string[] W = new string[0]; } // (Num: its line in the file — the window «Режисер» puts lines by it)
        internal sealed class SceneDef
        {
            public string Id = "", From = "", NeedsText = "", Place = "", Check = "", Test = "";
            public bool Alone; // («alone: yes»: the other players not drawn here while it plays — StoryAlone)
            public bool Roams; // («roams: yes»: it goes with the player — no stop for walking off from where it began)
            public List<Cond> Needs;
            public float Guard = -1f; // (the quiet round's full size: «guard:»; none (-1) — by the place's size, StoryHush.MaxFor; 0 — off)
            public readonly List<Step> Steps = new List<Step>();
            public readonly List<string> Cast = new List<string>(); // (people it takes from the world — not its own «spawn»: all of them near before it begins)
        }

        internal static readonly List<SceneDef> Scenes = new List<SceneDef>();
        internal static readonly List<string> Errors = new List<string>();
        internal static readonly List<string> Recent = new List<string>();
        internal static float Retry = 20f, FarStop = 60f, Seen = 100f, StepMax = 30f;
        private const string Rpc = "RavenWatch_StoryAct";

        private static bool _loaded;
        private static SceneDef _now;
        private static int _at = -1;
        private static float _stepAt, _until, _waitFor;
        private static Vector3 _anchor;
        private static float _hurtAt = -100f;
        private static StoryBird _bird;
        private static readonly Dictionary<string, float> _stopped = new Dictionary<string, float>(); // a scene stopped: not before Retry
        private static readonly Dictionary<long, StoryBird> _mirrors = new Dictionary<long, StoryBird>(); // others' scenes' birds

        internal static string Folder { get { return Path.Combine(StoryQuests.Folder, "scenes"); } }
        internal static string Playing { get { return _now != null ? _now.Id : ""; } }
        internal static SceneDef Current { get { return _now; } }
        internal static int At { get { return _at; } }
        internal static Vector3 BirdAt { get { return _bird != null ? _bird.transform.position : Vector3.zero; } }
        internal static bool HasBird { get { return _bird != null; } }

        internal static SceneDef Find(string id) { Load(); foreach (SceneDef s in Scenes) if (s.Id == id) return s; return null; }

        // ---------------------------------------------------------------- playing
        // asked by a quest's stage each pass: plays it when it may (one at a time, its needs, not just stopped)
        internal static void TryPlay(string id)
        {
            Load();
            if (_now != null) return;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            SceneDef s = Find(id);
            int resume;
            if (s != null && StoryWatch.Resume(id, out resume) && StoryQuests.OwnerOf(id) != null) // (a scene of the server, its director near me left it unfinished: taken up at the step it got to, without his own camera and body)
            {
                if (p.IsDead() || !Near(p, s) || Missing(p, s) != null) return; // (its people: till the old lock runs out, or the first of them would stop it)
                resume = Mathf.Max(resume, StoryQuests.Committed(p, id)); // (its pulse may be two seconds old: a lasting step never twice)
                if (StoryQuests.Mine(id)) { _handing = true; if (Play(s, resume)) { StoryWatch.Resumed(); Note("сцену «" + id + "» перейнято з кроку " + (resume + 1)); return; } _handing = false; }
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
                    _toldNeeds = key; Note("сцена «" + id + "» чекає: " + needs + (why != null ? " — " + why : ""));
                    if (Time.time - LocTouch.UsedAt < 5f && Senses.Pressed(p)) RavenMsg.Center(p, Lang.T("Не зараз: поруч ворог.")); // (you touched it and nothing happened: why — the scene starts once it is quiet)
                }
                return;
            }
            Character foe = FoeNear(p);
            if (foe != null) // (it would stop at once: it waits for the fight's end as for its needs)
            {
                if (_toldNeeds == null || !_toldNeeds.StartsWith(id + ":")) _needsSince = Time.time;
                if (_toldNeeds != id + ":fight") { _toldNeeds = id + ":fight"; Note("сцена «" + id + "» чекає: бій — " + Localization.instance.Localize(foe.m_name) + " поруч"); }
                return;
            }
            if (_toldNeeds != null && _toldNeeds.StartsWith(id + ":")) { _toldNeeds = null; Note("сцена «" + id + "» дочекалась за " + (Time.time - _needsSince).ToString("0") + " с"); }
            string missing = Missing(p, s);
            if (missing != null)
            {
                Vector3 place;
                if (s.Place.Length > 0 && StoryWhere.Find(s.Place, out place) && Utils.DistanceXZ(place, p.transform.position) > FarStop) _missingSince = Time.time; // (counted only while the player is there)
                if (_toldMissing != id + missing) { _toldMissing = id + missing; _missingSince = Time.time; Note("сцена «" + id + "» чекає: «" + missing + "» нема поруч або зайнята"); }
                else if (Time.time - _missingSince > CastWait && StoryQuests.Mine(id)) { _toldMissing = null; Note("«" + missing + "» так і не прийшла — сцена без неї"); Finish(p, s, 0); } // (never stuck for good: its lasting steps done, the story goes on)
                return;
            }
            _toldMissing = null;
            if (StoryQuests.OwnerOf(id) != null && !Near(p, s)) return; // (one far off must not hold the claim of a scene he cannot begin)
            if (!StoryQuests.Mine(id)) return; // (a scene of the server: one player at a time, asked last, when all else lets it begin)
            Play(s);
        }

        internal static bool Play(SceneDef s) { return Play(s, 0); }

        // near enough to where it plays (no place: anywhere)
        internal static bool Near(Player p, SceneDef s)
        {
            Vector3 place;
            return s.Place.Length == 0 || !StoryWhere.Find(s.Place, out place) || Utils.DistanceXZ(place, p.transform.position) <= FarStop;
        }

        // ---------------------------------------------------------------- once is once
        // A scene that ran a lasting step (a thing given, a gate opened, a raven owned) is kept as begun in the character
        // (var scene.<id> = the step after it; a scene of the server: the world's, StoryQuests.Committed); stopped then, or the game left, it is not played again:
        // the lasting steps still ahead run quietly and it counts as played.
        private static string _toldMissing, _toldNeeds; // session: what the log was last told a scene waits for
        private static float _missingSince, _needsSince;
        internal static float CastWait = 180f; // (s a scene waits for its people before it is done without them)

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
            if (s.Place.Length > 0 && StoryWhere.Find(s.Place, out place) && Utils.DistanceXZ(place, p.transform.position) > FarStop)
            { NoteOnce(s.Id + ":far", "сцена «" + s.Id + "» не почалась: гравець за " + Mathf.RoundToInt(Utils.DistanceXZ(place, p.transform.position)) + " м від «" + s.Place + "»"); return false; }
            if (_now != null) Stop("інша сцена");
            _toldOnce = null;
            _now = s; _at = Mathf.Clamp(from, 0, s.Steps.Count) - 1; _anchor = p.transform.position; _hurtAt = Senses.HurtByFoeAt;
            Outcomes.Clear(); _endedAt = -1;
            _ownOff = _handing; _handing = false; _server = StoryQuests.OwnerOf(s.Id) != null;
            StoryWatch.Begin(s);
            if (s.Alone) StoryAlone.Begin(); else StoryAlone.End();
            StoryGait.Turns(p, true);
            StoryAudio.Prepare(s); // (its files read now: its steps find them there)
            if (from > 0) SetUp(p, s, from);
            _go = false; _held = false;
            Note("сцена «" + s.Id + "» почалась" + (from > 0 ? " з кроку " + (from + 1) : ""));
            if (Plugin.Waiting) { Plugin.RunOutVisit(); Note("сцена «" + s.Id + "»: ворон гри зі словами, яких ніхто не вислухав, відлітає — сцена не чекає"); }
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
                k = st.Kind; if (k == null || k.Setup == null || k.Lasting || (_ownOff && k.Seen == StepSeen.Own)) continue;
                if (st.If != null && !StoryCond.All(p, st.If)) continue;
                try { k.Setup(p, st); } catch (Exception e) { Story.Log("scene set-up: " + e.Message); }
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
            int reached = _at;
            _now = null; _at = -1;
            Let("its claim", () => StoryQuests.Unclaim(id));
            Let("its watchers", () => StoryWatch.End(true, reached));
            Let("the bird", () => { HandOver(true); if (_bird != null) Act("gone", ""); });
            _bird = null;
            Release(true);
            Count(StopsOf, id); LastStopOf[id] = why;
            Note("сцена «" + id + "» перервана: " + why);
            StoryBus.Raise("scene_stop", id, 0);
        }

        private static void Done()
        {
            string id = _now.Id;
            Watching = false;
            _now = null; _at = -1;
            StoryQuests.Commit(Player.m_localPlayer, id, 0); // (played to the end: nothing left to finish)
            Let("its watchers", () => StoryWatch.End(false, -1));
            Let("the bird", () => { HandOver(true); if (_bird != null) Act("free", ""); }); // (left where it is, nobody's now: it flies off by itself a little later — StoryBird.Release)
            _bird = null;
            Release(false);
            Note("сцена «" + id + "» скінчилась");
            StoryBus.Raise("scene_done", id, 0);
        }

        // all a scene took, given back in one place (stopped: at once; played out: softly) — its people, the thing in his hand, his
        // walk, look, gait, gesture, the others hidden, the camera and the screen, its weather; then the modules' own (Ends: what is heard)
        internal static readonly List<Action<bool>> Ends = new List<Action<bool>>();
        internal static readonly List<Action<bool>> OwnEnds = new List<Action<bool>>(); // (what a module holds of the director himself: given back too when a scene of the server goes on without him)
        private static bool _weather, _weatherAll; // (this scene forced the weather: off with it — here, or in every game)

        // Each part alone (Let): the scene is already over and nobody calls this again, so a fault in one must not leave the camera, the black or the keys taken
        private static void Release(bool broken)
        {
            Player p = Player.m_localPlayer;
            Let("its people", FreeCast);
            Let("the thing in his hand", () => StoryProps.Put(p));
            Let("his gesture", () => Gestures.SceneEnd(p, PoseFade));
            Let("his walk", StoryWalk.Stop);
            Let("his look", StoryLook.End);
            Let("his turns", () => StoryGait.Turns(p, false));
            Let("the others hidden", StoryAlone.End);
            Let("his gait", () => StoryGait.Set(p, "none"));
            if (broken) { Let("the camera", StoryCamera.Reset); Let("the screen", StoryScreen.Reset); } // (the player gets the camera back at once)
            else { Let("the camera", () => StoryCamera.Off(1f)); Let("the screen", () => { StoryScreen.Bars(false); StoryScreen.Fade(false, 1f); }); } // (a scene that forgot «camera off» or «fade in»: given back softly)
            Let("the line of its shots", _lines.Clear); // (which side of two in talk its camera worked from: StoryScenesCamera)
            bool weather = _weather, all = _weatherAll;
            _weather = _weatherAll = false;
            if (weather) Let("its weather", () => StoryAir.Play("weather off", all));
            foreach (Action<bool> e in Ends) { Action<bool> end = e; Let("a module's end", () => end(broken)); }
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
            return foe != null && !foe.IsDead() && Vector3.Distance(foe.transform.position, p.transform.position) < FoeStop ? foe : null;
        }

        // every frame (StoryQuests.Tick)
        internal static void Tick()
        {
            if (_now == null) return;
            Player p = Player.m_localPlayer;
            if (p == null) { Stop("гравця нема"); return; } // (the same full stop as any other: its people let go, the camera given back)
            StoryAlone.Tick();
            if (!_ownOff) // (one's own scene ends with these; a scene of the server only gives him back what it held of him, and goes on)
            {
                string hit = p.IsDead() ? "смерть" : Senses.HurtByFoeAt > _hurtAt + 0.01f ? "удар" : null;
                Character foe = hit == null ? FoeNear(p) : null;
                if (foe != null) hit = "ворог поруч: " + Localization.instance.Localize(foe.m_name);
                if (hit != null) { if (_server) OwnOff(p, hit); else { Stop(hit); return; } }
            }
            if (!_now.Roams && Utils.DistanceXZ(_anchor, p.transform.position) > FarStop) { Stop("гравець відійшов від місця сцени"); return; } // (from where it began: the bird comes from afar; a scene that roams goes with him)
            if (!_ownOff) StoryLook.Tick(p);
            HandOver(false);
            StoryQuests.Renew(_now.Id);
            StoryWatch.Tick();
            if (_cast.Count > 0 && Time.time >= _renewAt) { _renewAt = Time.time + LockRenew; foreach (NpcActor a in _cast.Values) Lock(a, true); } // (the people it holds: still its)
            if (_held) { if (!_go) return; _go = false; _held = false; Next(); return; } // (step by step: the author's «Далі»)
            Step cur = _now.Steps[_at];
            if (Time.time > (_limit > 0f ? _limit : _stepAt + Mathf.Max(StepMax, _waitFor - _stepAt + 5f))) // (its own limit, else the general one on its clock)
            {
                if (cur.Kind != null && cur.Kind.Late != null) cur.Kind.Late(cur);
                Note("крок «" + cur.Line + "» задовгий — далі"); Count(LatesOf, _now.Id); LastLateOf[_now.Id] = cur.Line;
                EndStep("CUT OFF at its time limit");
                Next();
                return;
            }
            if (Waiting(p)) return;
            if (StepHolds) { _held = true; return; } // (this step done: the next one when the author says)
            Next();
        }

        // the director of a scene of the server is hit, or dies: what it held of him given back at once, it goes on without its Own steps (StoryWatch)
        private static bool _ownOff, _handing, _server;
        private static void OwnOff(Player p, string why)
        {
            _ownOff = true;
            Note("сцена «" + _now.Id + "» йде далі без кроків гравця: " + why);
            Let("the camera", StoryCamera.Reset);
            Let("the screen", StoryScreen.Reset);
            Let("the thing in his hand", () => StoryProps.Put(p));
            Let("his gesture", () => Gestures.SceneEnd(p, PoseFade));
            Let("his walk", StoryWalk.Stop);
            Let("his look", StoryLook.End);
            Let("his gait", () => StoryGait.Set(p, "none"));
            Let("his turns", () => StoryGait.Turns(p, false));
            foreach (Action<bool> e in OwnEnds) { Action<bool> end = e; Let("a module's own end", () => end(true)); } // (not Ends: its music, sounds and weather are everyone's, and it goes on. A «mix», «mute» of the scene stays till its end: no scene of the server uses them)
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
            if (st.Kind != null && st.Kind.Done != null && !(_ownOff && st.Kind.Seen == StepSeen.Own))
            {
                try { said = st.Kind.Done(Player.m_localPlayer, st); }
                catch (Exception e) { said = "(what came of it could not be read: " + e.Message + ")"; Story.Log("scene step outcome: " + e); }
            }
            string text = F(Time.time - _stepAt) + " s" + (!string.IsNullOrEmpty(said) ? ": " + said : "") + (how != null ? " — " + how : "");
            Outcomes[_at] = text;
            Note("     → " + text);
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
            if (k == null || (_ownOff && k.Seen == StepSeen.Own)) return false; // (his own step, and he is given back: nothing to wait for)
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
                EndStep(null);
                _at++;
                if (_at >= _now.Steps.Count) { Done(); return; }
                Step st = _now.Steps[_at];
                _stepAt = Time.time; _limit = 0f; _waitFor = 0f; _until = 0f; // (the step's own clock, clean: a step that knows how long it may take sets its own)
                if (st.If != null && !StoryCond.All(p, st.If)) { Note("  " + (_at + 1) + ". пропущено (умова): " + st.Line); _endedAt = _at; continue; } // (never begun: nothing came of it)
                Note("  " + (_at + 1) + ". " + st.Line);
                Begin(p, st);
                if (_now != null && st.Kind != null && st.Kind.Lasting) StoryQuests.Commit(p, _now.Id, _at + 1); // (begun for good: the steps from the next one on are what a stop leaves)
                if (_now != null && (Waiting(p) || StepHolds)) return; // (step by step: one at a time, even the ones that do not wait)
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

        private static void Begin(Player p, Step st)
        {
            StepKind k = st.Kind; // (its verb's own entry, where it has verbs)
            if (k == null || k.Begin == null || (_ownOff && k.Seen == StepSeen.Own)) return;
            try { k.Begin(p, st); }
            catch (Exception e) { Note("крок «" + st.Line + "» зламався: " + e.Message + " — далі"); Story.Log("scene step: " + e); } // (the scene goes on: a step's own limit never comes for one that never began)
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
            line = Grammar.Fix(line, Player.m_localPlayer); // (the forms for the director; the game's own lines do this where they are shown)
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

        internal static void Note(string s)
        {
            Recent.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (Recent.Count > 60) Recent.RemoveAt(0);
            StoryQuests.Note(s); // (the track's log and the game's log)
        }
    }
}
#endif

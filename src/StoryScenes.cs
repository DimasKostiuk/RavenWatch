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
    // the keys unless a shot holds the camera (StoryCamera). The world never pauses — a scene that goes wrong stops:
    //   death · a fight (hit, or a foe at you) · going off more than 60 m from where it plays
    // and its actors vanish; the stage plays it again later (after Retry seconds, when «needs» hold again).
    // Every step has its own time limit: a scene never hangs.
    //
    // Files: assets/story/scenes/*.txt — read where StoryFiles says (built in; on the author's machine the mod's own folder,
    // edited in F9 «Сцени»). This file: reading, the kinds of steps, playing; the camera, the bird, the people of a
    // scene in StoryScenesCamera / StoryScenesBird / StoryScenesCast.
    //   [scene raven_call]
    //   needs: calm                         conditions to begin (StoryCond)
    //   place: prologue_house               where it is filmed (the author's shots of that place: StoryShots)
    //   guard: 30 | off                     the quiet round the event (StoryHush): its full size in m; none — by the
    //                                       place's size; off — none. It grows round the place as the player comes on
    //                                       the stage that leads here; no place — round the player while it plays
    // It begins only when every person it takes from the world (npc <who> …, not its own «spawn») is near and free.
    // Once is once: after a lasting step (do, own, drop, door) it is never played again — stopped then, or the game
    // left, its lasting steps still ahead run quietly and it counts as played.
    //   bird come 60 18 land 5              the story's raven: from 60 m behind you, 18 m up, lands 5 m before you
    //   bird land hall.dveri_holovni        … or flies on to a place and sits on top of it
    //   bird look player                    turns to you
    //   bird say …ти ж обіцяв.             a line over it
    //   bird circle 30 [4.5] [3.5]          round over you for 30 s (radius, metres over your head)
    //   bird leave hall                     off that way and gone (the step does not wait for it)
    //   bird gone                           a puff of feathers
    //   say hugin|munin <текст>             your own raven says it (only if it is on your shoulder)
    //   msg <текст>                         a line in the middle of the screen
    //   camera <from> [look <what>] [blend 2] [fov 50]   a shot (StoryCamera): from a place, «behind» or «over» you;
    //                                       looking at player (default), bird, sky (up before you) or a place; the player stands still
    //   camera off [blend 1.5]              the camera back to the player (also when the scene ends or stops)
    //   bars on|off · sub <текст>           the black bars; a subtitle (waits while it is read)
    //   weather <env> [s] [all] · weather off [all]     forced weather (ThunderStorm, Rain, Misty, Clear …)
    //   bolt near|ahead [n] [all] · bolt <place> [n] [spread] [all]   bolts from the sky (StoryAir; ahead = before the eyes)
    //   thor [all] · shake <strength> [all] · music <name>|off [all]
    //                                       «all»: in every game of the world, round each player
    //   fade out|in [1.5]                   the whole picture to black and back (waits)
    //   player face <place> · player emote <gesture>|stop    the player himself turned / a gesture
    //   player go <place> <m> [front] [run]  he walks to <m> from its middle, on the side he is on, and turns to it (front:
    //                                       the side its face looks to, turned as it is; goes through what is between). Waits till he
    //                                       stands exactly so, at most StoryWalk.Most seconds; how near he got, in the log
    //   player clip <clip[.part]>|none [nowait]  one of our clips on him, through Gestures (waits for it unless it loops,
    //                                       or «nowait»); the next one crossfades; let go when the scene ends or stops
    //   player mark <name> [seconds]        waits till the clip on him passes that «mark» (BodyClips), at most 30 s
    //   prop take <item> [from <place.piece>] [left|right] [pose <clip>|still] [size <m>] [free] · prop put
    //                                       a thing in the player's hand (its look; the piece it came from hidden; he looks
    //                                       at it — prop_look_<hand>); «free»: it stays where it lay and the clip moves it
    //                                       (@Prop, BodyClips); «npc <who> prop take|put …» the same for a person.
    //                                       All put back when the scene ends or stops (StoryProps)
    //   wait 1.5                            seconds
    //   until near bird 3 | 20              until the conditions hold, at most 20 s
    //   if raven hugin: say hugin Це не наш. a step only when the conditions hold
    //   do <action>                         a quest's action now (StoryActions: hallgate open, world x, flag x,
    //                                       msg …; not goto/end) — the world changes at the scene's moment, not after it
    //   door <place> open|close             a door of the story's places opened or shut for everyone (hall.dveri_holovni)
    //   drop <item> <place|player|npc.x>    a thing left on the ground there (networked, anyone may pick it up)
    //   npc <who> take                      a person of the story into the scene (who: its template id or its tag; the
    //                                       nearest loaded one): it stops what it does — its mind's Scene layer, over all
    //   npc <who> go <place|player|bird|npc.x> [run] [near 1.6]   walks there — runs when farther than RunFrom m (waits till it is near: NpcMind.Reach)
    //   npc <who> face player|bird|<place>  turns and keeps looking (does not wait)
    //   npc <who> say <текст>               a line over it, as it says it to you (waits while it is read)
    //   npc <who> emote <gesture> [loop]    a gesture (wave, bow, kneel, cry…; loop: held)
    //   npc <who> sit <place> · stay · free sits by a place · stands · back to its own day
    //   npc <who> spawn <place>             made for this scene at a place (its template id), facing you; not kept in the
    //                                       world — fades away when the scene ends or stops (NpcFade)
    //   npc <who> fade [3] [sound]          fades away slowly (waits the seconds) and is gone; a story sound with it (StorySounds)
    //   sound <name|sfx_…> [place|player|npc.x]   a story sound (StorySounds: whisper …) or the game's, heard there (this game)
    //   Every one taken is let go when the scene ends or stops (back to its own day where it is). A person is a network
    //   object: everyone near sees it walk and hears its lines; its words go to whichever game runs it.
    //   Places also «npc.<who>» (where that person stands now): for the camera, the bird, «until near npc.heid 3 | 20».
    // Those near (within Seen m) see the bird too: each order goes to them with its places worked out (RPC), their
    // game flies a picture of its own.
    internal static partial class StoryScenes
    {
        internal sealed class Step { public string Text = "", Line = ""; public int Num; public List<Cond> If, Until; public float UntilSec = 10f; public string[] W = new string[0]; } // (Num: its line in the file — the window «Режисер» puts lines by it)
        internal sealed class SceneDef
        {
            public string Id = "", From = "", NeedsText = "", Place = "";
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

        // ---------------------------------------------------------------- reading
        internal static void Load() { Wire(); if (!_loaded) Reload(); }

        internal static void Reload()
        {
            _loaded = true;
            StoryTrackConds.Register(); // (the conditions and the modules' actions known before any «if»/«do» line is checked)
            Scenes.Clear(); Errors.Clear();
            foreach (KeyValuePair<string, string> kv in StoryFiles.All(StoryFiles.Scenes, Folder, Blueprint.On)) Parse(kv.Key, kv.Value); // (built in; the mod's own folder on the author's machine — StoryFiles)
            foreach (SceneDef sc in Scenes) CheckPlaces(sc);
            foreach (string e in Errors) Story.Log("scenes: " + e);
            StoryShots.Again(); StorySounds.Again(); // (the author's shots and the story's sounds: from their files again too)
            Story.Log("scenes: " + Scenes.Count + " loaded" + (Errors.Count > 0 ? ", " + Errors.Count + " problems" : ""));
        }

        private static readonly string[] NpcVerbs = { "take", "go", "face", "say", "emote", "sit", "stay", "free", "spawn", "fade", "prop", "clip" };

        private static void Parse(string file, string text)
        {
            SceneDef s = null;
            foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(text))
            {
                string line = ln.Value, at = file + ":" + ln.Key + ": ";
                if (line.StartsWith("[scene ") && line.EndsWith("]"))
                {
                    string id = line.Substring(7, line.Length - 8).Trim();
                    if (!Story.GoodName(id)) { Errors.Add(at + "погана назва сцени"); s = null; continue; }
                    if (Find0(id) != null) { Errors.Add(at + "сцена «" + id + "» уже є — ця пропущена"); s = null; continue; }
                    s = new SceneDef { Id = id, From = file }; Scenes.Add(s);
                    continue;
                }
                if (s == null) { Errors.Add(at + "рядок поза сценою"); continue; }
                if (line.StartsWith("needs:")) { s.NeedsText = line.Substring(6).Trim(); s.Needs = Conds(s.NeedsText, at); continue; }
                if (line.StartsWith("place:")) { s.Place = line.Substring(6).Trim(); continue; } // (where it is filmed: its shots are that place's — the window «Режисер»)
                if (line.StartsWith("guard:"))
                {
                    string g = line.Substring(6).Trim();
                    if (g == "off") s.Guard = 0f;
                    else { float r = F(g, -1f); if (r > 0f && r <= 80f) s.Guard = r; else Errors.Add(at + "guard: <метрів 1–80> | off"); }
                    continue;
                }
                Step st = new Step { Line = line, Text = line, Num = ln.Key };
                if (line.StartsWith("if "))
                {
                    int c = line.IndexOf(':');
                    if (c < 0) { Errors.Add(at + "if <умови>: <крок>"); continue; }
                    st.If = Conds(line.Substring(3, c - 3), at);
                    st.Text = line.Substring(c + 1).Trim();
                }
                st.W = st.Text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string why = Check(st);
                if (why != null) { Errors.Add(at + why); continue; }
                if (st.W[0] == "npc" && st.W.Length > 2 && st.W[2] != "free" && !s.Cast.Contains(st.W[1]) && !Spawned(s, st.W[1])) s.Cast.Add(st.W[1]); // (wanted from the world)
                if (st.W[0] == "until") { int bar = st.Text.LastIndexOf('|'); st.Until = StoryCond.Parse(st.Text.Substring(5, bar - 5), null); st.UntilSec = F(st.Text.Substring(bar + 1).Trim(), 10f); }
                s.Steps.Add(st);
            }
        }

        // the places a scene's steps name (a typo: the step skipped in the game, said only in the log)
        private static void CheckPlaces(SceneDef s)
        {
            string at = s.From + " [" + s.Id + "]: ";
            if (s.Place.Length > 0 && StoryWhere.Why(s.Place) != null) Errors.Add(at + "place — " + StoryWhere.Why(s.Place));
            PAnimClip was = null; // (two clips one after the other: is the join seen? said in the log only)
            foreach (Step st in s.Steps)
            {
                string[] w = st.W;
                if (w[0] == "player" && w.Length > 2 && w[1] == "clip")
                {
                    PAnimClip now = BodyClips.Find(w[2]);
                    string ch;
                    if (was != null && now != null && BodyClips.JoinGap(was, now, out ch) > BodyClips.JoinNear) Story.Log("scene " + s.Id + ":" + st.Num + ": the join " + was.Name + " > " + now.Name + " shows (" + ch + ")");
                    was = now;
                }
                string place = null;
                if (w[0] == "bird" && w.Length > 2 && (w[1] == "land" || w[1] == "leave")) place = w[2];
                else if (w[0] == "bird" && w[1] == "come" && Array.IndexOf(w, "at") > 0 && Array.IndexOf(w, "at") + 1 < w.Length) place = w[Array.IndexOf(w, "at") + 1];
                else if (w[0] == "npc" && w.Length > 3 && (w[2] == "go" || w[2] == "sit" || w[2] == "spawn" || w[2] == "face")) place = w[3];
                else if ((w[0] == "door" || w[0] == "bolt") && w.Length > 1 && w[1] != "near" && w[1] != "ahead") place = w[1];
                else if ((w[0] == "drop" || w[0] == "sound") && w.Length > 2) place = w[2];
                else if ((w[0] == "prop" || (w[0] == "npc" && w.Length > 2 && w[2] == "prop")) && Array.IndexOf(w, "from") > 0 && Array.IndexOf(w, "from") + 1 < w.Length) place = w[Array.IndexOf(w, "from") + 1];
                else if (w[0] == "camera" && Array.IndexOf(w, "look") > 0 && Array.IndexOf(w, "look") + 1 < w.Length) place = w[Array.IndexOf(w, "look") + 1];
                if (place == null) continue;
                string why = StoryWhere.Why(place);
                if (why != null) Errors.Add(s.From + ":" + st.Num + ": " + why);
            }
        }

        private static bool Spawned(SceneDef s, string who)
        {
            foreach (Step o in s.Steps) if (o.W.Length > 2 && o.W[0] == "npc" && o.W[1] == who && o.W[2] == "spawn") return true;
            return false;
        }

        private static SceneDef Find0(string id) { foreach (SceneDef s in Scenes) if (s.Id == id) return s; return null; }

        private static List<Cond> Conds(string val, string at)
        {
            List<string> errs = new List<string>();
            List<Cond> l = StoryCond.Parse(val, errs);
            foreach (string e in errs) Errors.Add(at + e);
            return l;
        }

        // ---------------------------------------------------------------- the kinds of steps
        // A registry, as StoryCond's conditions and StoryActions' actions (the user 2026-11-02: predictable, nothing patched
        // on): a step's first word picks its kind, a new kind is one entry — the director itself never changes for it.
        //   Min / Usage   words it needs after its own; how it is written (the errors)
        //   Check(st)     load time: null — fine, else what is wrong (after Min)
        //   Begin(p, st)  it starts; it may set its clock: Hold(s) — busy till then; _limit — its own time limit
        //   Busy(p, st)   still going? (none: its clock — Hold — says)
        //   Late(st)      it ran past its limit: what it began is put right (a walker stands where it is)
        // The clock (_waitFor, _until, _limit) is the step's own: cleared as each step starts.
        internal sealed class StepKind
        {
            public int Min;
            public string Usage = "";
            public Func<Step, string> Check;
            public Action<Player, Step> Begin;
            public Func<Player, Step, bool> Busy;
            public Action<Step> Late;
            public bool Lasting; // (it changes the world or the bag for good: once it ran, the scene is never played again — a stop finishes it quietly)
        }

        private static Dictionary<string, StepKind> _kinds;
        private static Dictionary<string, StepKind> Kinds { get { if (_kinds == null) { _kinds = Builtin(); StoryModules.Steps(); } return _kinds; } } // (and the modules' own)

        // a module's own step (the same way as the ones below)
        internal static void Register(string word, StepKind k) { Kinds[word] = k; }

        private static void Hold(float seconds) { _waitFor = Time.time + seconds; }

        private static Dictionary<string, StepKind> Builtin()
        {
            StepKind air = new StepKind // (each game's own air, or everyone's with «all»: StoryAir)
            {
                Usage = "weather <env> [s] [all] · weather off · thor [all] · shake <сила> [all] · music <назва>|off [all]",
                Check = st => st.W[0] != "thor" && st.W.Length < 2 ? st.W[0] + ": мало слів" : null,
                Begin = (p, st) =>
                {
                    bool all = st.W[st.W.Length - 1] == "all";
                    StoryAir.Play(all ? st.Text.Substring(0, st.Text.LastIndexOf("all")).Trim() : st.Text, all);
                }
            };
            return new Dictionary<string, StepKind>
            {
                { "wait", new StepKind { Min = 1, Usage = "wait <секунд>", Begin = (p, st) => Hold(F(st.W[1], 1f)) } },
                { "until", new StepKind { Usage = "until <умови> | <секунд>",
                    Check = st =>
                    {
                        int bar = st.Text.LastIndexOf('|');
                        if (bar < 0) return "until <умови> | <секунд>";
                        List<string> errs = new List<string>();
                        StoryCond.Parse(st.Text.Substring(5, bar - 5), errs);
                        return errs.Count > 0 ? string.Join("; ", errs.ToArray()) : null;
                    },
                    Begin = (p, st) => { _until = Time.time + st.UntilSec; _limit = _until + 1f; },
                    Busy = (p, st) => Time.time < _until && !StoryCond.All(p, st.Until) } },
                { "msg", new StepKind { Min = 1, Usage = "msg <текст>",
                    Begin = (p, st) => { if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, Lang.T(Rest(st.Text, 1))); } } },
                { "say", new StepKind { Min = 2, Usage = "say hugin|munin <текст>",
                    Check = st => st.W[1] != "hugin" && st.W[1] != "munin" ? "say hugin|munin <текст>" : null,
                    Begin = (p, st) =>
                    {
                        string line = Lang.T(Rest(st.Text, 2));
                        bool munin = st.W[1] == "munin";
                        if (!Shoulder.Present(p, munin)) return; // (not on the shoulder: nothing said, nothing waited)
                        Hold(ReadTime(line));
                        RavenSkills.StoryLine(munin, line);
                    } } },
                { "sub", new StepKind { Min = 1, Usage = "sub <текст>",
                    Begin = (p, st) => { string line = Lang.T(Rest(st.Text, 1)); float t = ReadTime(line); Hold(t); StoryScreen.Sub(line, t + 0.4f); } } },
                { "bars", new StepKind { Min = 1, Usage = "bars on|off",
                    Check = st => st.W[1] != "on" && st.W[1] != "off" ? "bars on|off" : null,
                    Begin = (p, st) => StoryScreen.Bars(st.W[1] == "on") } },
                { "camera", new StepKind { Min = 1, Usage = "camera <місце|behind|over|off> [look …] [blend N|cut] [fov N] · camera shot <кадр> · camera move <кадр> <кадр> <с> [ease …] [wait] · camera orbit <що> <радіус> <висота> <градусів> <с> [wait]",
                    Busy = (p, st) => Time.time < _waitFor, // (a move or an orbit with «wait»)
                    Begin = (p, st) => CameraStep(p, st.W) } },
                { "fade", new StepKind { Min = 1, Usage = "fade out|in [секунд]   (увесь кадр у чорне й назад; крок чекає, поки скінчиться)",
                    Check = st => st.W[1] != "out" && st.W[1] != "in" ? "fade out|in [секунд]" : null,
                    Begin = (p, st) => { float s = st.W.Length > 2 ? F(st.W[2], 1.5f) : 1.5f; StoryScreen.Fade(st.W[1] == "out", s); Hold(s); } } },
                { "player", new StepKind { Min = 2, Usage = PlayerUsage,
                    Check = st => st.W[1] != "face" && st.W[1] != "emote" && st.W[1] != "clip" && st.W[1] != "go" && st.W[1] != "mark" ? PlayerUsage : st.W[1] == "go" && st.W.Length < 4 ? PlayerUsage : null,
                    Begin = (p, st) => PlayerStep(p, st.W),
                    Busy = (p, st) => st.W[1] == "go" ? StoryWalk.On : st.W[1] == "mark" ? Time.time < _waitFor && MarkAhead(p, st.W[2]) : Time.time < _waitFor,
                    Late = st => { if (st.W[1] == "go") StoryWalk.Stop(); } } },
                { "prop", new StepKind { Min = 1, Usage = StoryProps.Usage,
                    Check = st => { if (st.W[1] == "put") return null; string why; return st.W[1] != "take" ? StoryProps.Usage : StoryProps.Read(st.W, 2, out why) == null ? why : null; },
                    Begin = (p, st) =>
                    {
                        if (st.W[1] == "put") { StoryProps.Put(p); Hold(0.5f); return; }
                        string why;
                        StoryProps.Ask ask = StoryProps.Read(st.W, 2, out why);
                        why = ask != null ? StoryProps.Take(p, ask) : why;
                        if (why != null) Note("prop: " + why);
                        Hold(0.6f); // (the hand comes up to it)
                    } } },
                { "weather", air }, { "thor", air }, { "shake", air }, { "music", air },
                { "bolt", new StepKind { Min = 1, Usage = "bolt near|ahead [n] [all] · bolt <місце> [n] [розкид] [all]",
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        bool all = w[w.Length - 1] == "all";
                        int n = all ? w.Length - 1 : w.Length;
                        string line;
                        if (w[1] == "near" || w[1] == "ahead") line = "bolt " + w[1] + " " + (n > 2 ? w[2] : "1");
                        else
                        {
                            Vector3 at;
                            if (!StoryWhere.Find(w[1], out at)) { Note("bolt: нема місця «" + w[1] + "» — крок пропущено"); return; }
                            line = "bolt " + V(at) + " " + (n > 2 ? w[2] : "1") + " " + (n > 3 ? w[3] : "0");
                        }
                        StoryAir.Play(line, all);
                    } } },
                { "do", new StepKind { Min = 1, Lasting = true, Usage = "do <дія квесту>",
                    Check = st => { string why = StoryActions.Check(st.Text.Substring(2).Trim(), true); return why != null ? "do: " + why : null; },
                    Begin = (p, st) => StoryActions.Do(p, null, st.Text.Substring(2).Trim()) } },
                { "sound", new StepKind { Min = 1, Usage = "sound <назва|sfx_…> [місце|player|npc.x]",
                    Begin = (p, st) =>
                    {
                        Vector3 at = p.transform.position + Vector3.up * 1.4f;
                        if (st.W.Length > 2 && st.W[2] != "player" && !StoryWhere.Find(st.W[2], out at)) { Note("sound: нема місця «" + st.W[2] + "» — грає біля гравця"); at = p.transform.position + Vector3.up * 1.4f; }
                        if (!StorySounds.Play(st.W[1], at)) Note("sound: «" + st.W[1] + "» — нема такого звуку в грі");
                    } } },
                { "drop", new StepKind { Min = 2, Lasting = true, Usage = "drop <предмет> <місце|player|npc.x>   (річ лишається на землі там)",
                    Begin = (p, st) => Drop(p, st.W[1], st.W[2]) } },
                { "door", new StepKind { Min = 2, Lasting = true, Usage = "door <місце> open|close",
                    Check = st => st.W[2] != "open" && st.W[2] != "close" ? "door <місце> open|close" : null,
                    Begin = (p, st) => Door(st.W[1], st.W[2] == "open") } },
                { "bird", new StepKind { Min = 1, Usage = "bird come|land|look|say|circle|leave|gone …",
                    Check = st =>
                    {
                        string v = st.W[1];
                        if (Array.IndexOf(new[] { "come", "land", "look", "say", "circle", "leave", "gone" }, v) < 0) return "bird: невідоме «" + v + "»";
                        return (v == "land" || v == "say" || v == "leave" || v == "look" || v == "circle") && st.W.Length < 3 ? "bird " + v + ": мало слів" : null;
                    },
                    Begin = (p, st) => BirdStep(p, st, st.W),
                    Busy = (p, st) =>
                    {
                        string v = st.W[1];
                        if (v == "come" || v == "land") return _bird != null && !_bird.Settled;
                        return Time.time < _waitFor; // (say, circle: its clock)
                    } } },
                { "npc", new StepKind { Min = 2, Usage = "npc <хто> " + string.Join("|", NpcVerbs) + " …",
                    Check = st =>
                    {
                        string v = st.W[2];
                        if (Array.IndexOf(NpcVerbs, v) < 0) return "npc <хто> " + string.Join("|", NpcVerbs) + " …";
                        if (v == "clip") return st.W.Length < 4 ? "npc <хто> clip <кліп>|none" : st.W[3] != "none" && BodyClips.Find(st.W[3]) == null ? "npc <хто> clip: нема кліпу «" + st.W[3] + "»" : null; // (our clips: BodyClips)
                        if (v == "prop") { string pw; return st.W.Length < 4 || (st.W[3] != "put" && (st.W[3] != "take" || StoryProps.Read(st.W, 4, out pw) == null)) ? "npc <хто> " + StoryProps.Usage : null; }
                    return (v == "go" || v == "face" || v == "say" || v == "emote" || v == "sit" || v == "spawn") && st.W.Length < 4 ? "npc <хто> " + v + ": мало слів" : null;
                    },
                    Begin = (p, st) => NpcStep(p, st, st.W),
                    Busy = (p, st) => st.W[2] == "go" ? Walking() : Time.time < _waitFor,
                    Late = st => { if (_walker != null) { _walker.Order("stay", NpcMind.Layer.Scene); _walker = null; } } } },
            };
        }

        private static string Check(Step st)
        {
            string[] w = st.W;
            StepKind k;
            if (w.Length == 0) return "порожній крок";
            if (!Kinds.TryGetValue(w[0], out k)) return "невідомий крок «" + w[0] + "» (є: " + string.Join(", ", new List<string>(Kinds.Keys).ToArray()) + ")";
            if (w.Length - 1 < k.Min) return k.Usage.Length > 0 ? k.Usage : w[0] + ": мало слів";
            return k.Check != null ? k.Check(st) : null;
        }

        // ---------------------------------------------------------------- playing
        // asked by a quest's stage each pass: plays it when it may (one at a time, its needs, not just stopped)
        internal static void TryPlay(string id)
        {
            Load();
            if (_now != null) return;
            Player p = Player.m_localPlayer;
            if (p == null) return;
            SceneDef s = Find(id);
            if (s != null && Story.Var(p, Committed(id)) > 0) { if (!p.IsDead()) Finish(p, s, Story.Var(p, Committed(id))); return; } // (stopped past its lasting step, or a relog: the rest done quietly, never played again)
            float when;
            if (_stopped.TryGetValue(id, out when) && Time.time - when < Retry) return;
            if (s == null) // (no file for it: «played» at once, said so — the load check names it too: StoryQuests.Validate)
            {
                Note("сцени «" + id + "» нема у файлах — вважаю зіграною");
                StoryBus.Raise("scene_done", id, 0);
                return;
            }
            if (s.Needs != null && !StoryCond.All(p, s.Needs)) return;
            string missing = Missing(p, s);
            if (missing != null)
            {
                Vector3 place;
                if (s.Place.Length > 0 && StoryWhere.Find(s.Place, out place) && Utils.DistanceXZ(place, p.transform.position) > FarStop) _missingSince = Time.time; // (counted only while the player is there)
                if (_toldMissing != id + missing) { _toldMissing = id + missing; _missingSince = Time.time; Note("сцена «" + id + "» чекає: «" + missing + "» нема поруч або зайнята"); }
                else if (Time.time - _missingSince > CastWait) { _toldMissing = null; Note("«" + missing + "» так і не прийшла — сцена без неї"); Finish(p, s, 0); } // (never stuck for good: its lasting steps done, the story goes on)
                return;
            }
            _toldMissing = null;
            Play(s);
        }

        internal static void Play(SceneDef s) { Play(s, 0); }

        // ---------------------------------------------------------------- once is once
        // A scene that ran a lasting step (a thing given, a gate opened, a raven owned) is kept as begun in the character
        // (var scene.<id> = the step after it); stopped then, or the game left, it is not played again: the lasting steps
        // still ahead run quietly and it counts as played.
        private static string Committed(string id) { return "scene." + id; }
        private static string _toldMissing;
        private static float _missingSince;
        internal static float CastWait = 180f; // (s a scene waits for its people before it is done without them)

        private static void Finish(Player p, SceneDef s, int from)
        {
            int n = 0;
            for (int i = Mathf.Max(0, from); i < s.Steps.Count; i++)
            {
                Step st = s.Steps[i];
                StepKind k;
                if (!Kinds.TryGetValue(st.W[0], out k) || !k.Lasting || k.Begin == null) continue;
                if (st.If != null && !StoryCond.All(p, st.If)) continue;
                try { k.Begin(p, st); n++; } catch (Exception e) { Story.Log("scene finish: " + e.Message); }
            }
            Story.SetVar(p, Committed(s.Id), 0);
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
        internal static void Play(SceneDef s, int from)
        {
            Player p = Player.m_localPlayer;
            if (p == null || s == null) return;
            if (_now != null) Stop("інша сцена");
            _now = s; _at = Mathf.Clamp(from, 0, s.Steps.Count) - 1; _anchor = p.transform.position; _hurtAt = Senses.HurtByFoeAt;
            _go = false; _held = false;
            Note("сцена «" + s.Id + "» почалась" + (from > 0 ? " з кроку " + (from + 1) : ""));
            StoryBus.Raise("scene_start", s.Id, 0);
            Next();
        }

        internal static void Stop(string why)
        {
            if (_now == null) return;
            string id = _now.Id;
            _stopped[id] = Time.time;
            _now = null; _at = -1;
            if (_bird != null) { Act("gone", ""); _bird = null; }
            FreeCast(); StoryProps.Put(Player.m_localPlayer); Gestures.SceneEnd(Player.m_localPlayer, PoseFade); StoryWalk.Stop(); // (whatever was taken: back)
            StoryCamera.Reset(); StoryScreen.Reset(); // (the player gets the camera back at once)
            Note("сцена «" + id + "» перервана: " + why);
            StoryBus.Raise("scene_stop", id, 0);
        }

        private static void Done()
        {
            string id = _now.Id;
            _now = null; _at = -1;
            Story.SetVar(Player.m_localPlayer, Committed(id), 0); // (played to the end: nothing left to finish)
            if (_bird != null) Act("free", ""); // (left where it is, nobody's now: it flies off by itself a little later — StoryBird.Release)
            _bird = null;
            FreeCast(); StoryProps.Put(Player.m_localPlayer); Gestures.SceneEnd(Player.m_localPlayer, PoseFade); StoryWalk.Stop();
            StoryCamera.Off(1f); StoryScreen.Bars(false); StoryScreen.Fade(false, 1f); // (a scene that forgot «camera off» or «fade in»: given back softly)
            Note("сцена «" + id + "» скінчилась");
            StoryBus.Raise("scene_done", id, 0);
        }

        // every frame (StoryQuests.Tick)
        internal static void Tick()
        {
            if (_now == null) return;
            Player p = Player.m_localPlayer;
            if (p == null) { Stop("гравця нема"); return; } // (the same full stop as any other: its people let go, the camera given back)
            if (p.IsDead()) { Stop("смерть"); return; }
            if (Senses.HurtByFoeAt > _hurtAt + 0.01f) { Stop("удар"); return; }
            if (Senses.Fight && Senses.Foe != null && Vector3.Distance(Senses.Foe.transform.position, p.transform.position) < 12f) { Stop("ворог поруч: " + Localization.instance.Localize(Senses.Foe.m_name)); return; }
            if (Utils.DistanceXZ(_anchor, p.transform.position) > FarStop) { Stop("гравець відійшов від місця сцени"); return; } // (from where it began: the bird comes from afar)
            if (_cast.Count > 0 && Time.time >= _renewAt) { _renewAt = Time.time + LockRenew; foreach (NpcActor a in _cast.Values) Lock(a, true); } // (the people it holds: still its)
            if (_held) { if (!_go) return; _go = false; _held = false; Next(); return; } // (step by step: the author's «Далі»)
            Step cur = _now.Steps[_at];
            if (Time.time > (_limit > 0f ? _limit : _stepAt + Mathf.Max(StepMax, _waitFor - _stepAt + 5f))) // (its own limit, else the general one on its clock)
            {
                StepKind lk;
                if (Kinds.TryGetValue(cur.W[0], out lk) && lk.Late != null) lk.Late(cur);
                Note("крок «" + cur.Line + "» задовгий — далі");
                Next();
                return;
            }
            if (Waiting(p)) return;
            if (StepMode) { _held = true; return; } // (this step done: the next one when the author says)
            Next();
        }

        // the author's step-by-step (F9 «Сцени»): each step played, then held till «Далі» (no time limit while held)
        internal static bool StepMode;
        private static bool _held, _go;
        internal static bool Held { get { return _held; } }
        internal static void Go() { if (_held) _go = true; }

        // the current step still going? its kind says (none: its clock)
        private static bool Waiting(Player p)
        {
            Step st = _now.Steps[_at];
            StepKind k;
            if (!Kinds.TryGetValue(st.W[0], out k)) return false;
            return k.Busy != null ? k.Busy(p, st) : Time.time < _waitFor;
        }

        // a person of the scene on its way (npc go): run here — its mind says (its walk turns into standing there when it
        // arrives: NpcMind.Arrived); run on another game — where it stands, by the same rule and a step more (what we see
        // lags). There: it stands for the scene (its day would take it off)
        private static bool Walking()
        {
            if (_walker == null) return false;
            bool walking;
            if (_walker.Owned) { NpcMind.Plan walk = _walker.Mind != null ? _walker.Mind.Of(NpcMind.Layer.Scene) : null; walking = walk != null && walk.Kind == NpcMind.Task.Go; }
            else walking = Utils.DistanceXZ(_walker.transform.position, _walkTo) > _walkNear + 1f;
            if (walking) return true;
            _walker.Order("stay", NpcMind.Layer.Scene);
            _walker = null;
            return false;
        }

        private static void Next()
        {
            Player p = Player.m_localPlayer;
            while (_now != null)
            {
                _at++;
                if (_at >= _now.Steps.Count) { Done(); return; }
                Step st = _now.Steps[_at];
                _stepAt = Time.time; _limit = 0f; _waitFor = 0f; _until = 0f; // (the step's own clock, clean: a step that knows how long it may take sets its own)
                if (st.If != null && !StoryCond.All(p, st.If)) { Note("  " + (_at + 1) + ". пропущено (умова): " + st.Line); continue; }
                Note("  " + (_at + 1) + ". " + st.Line);
                Begin(p, st);
                StepKind lk;
                if (_now != null && Kinds.TryGetValue(st.W[0], out lk) && lk.Lasting) Story.SetVar(p, Committed(_now.Id), _at + 1, true); // (begun for good: the steps from the next one on are what a stop leaves)
                if (_now != null && (Waiting(p) || StepMode)) return; // (step by step: one at a time, even the ones that do not wait)
            }
        }

        // ---------------------------------------------------------------- the author's editing (F9 «Сцени»)
        // a scene file's text as it is used now (the author's copy, else the built-in one); saved as his copy and read again
        internal static string FileText(string file) { return StoryFiles.Text(StoryFiles.Scenes, Folder, Blueprint.On, file); }
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
            StepKind k;
            if (Kinds.TryGetValue(st.W[0], out k) && k.Begin != null) k.Begin(p, st);
        }

        // the player himself in a scene: turned to look at something, a gesture (his own game: he is the owner)
        private static readonly System.Reflection.FieldInfo FLookYaw = HarmonyLib.AccessTools.Field(typeof(Character), "m_lookYaw");
        private const string PlayerUsage = "player face <місце|npc.x|bird> · player emote <жест>|stop · player go <місце> <м> [front] [run] · player clip <кліп[.частина]>|none [nowait] · player mark <назва> [секунд]";
        internal static float PoseFade = 0.4f;

        // the clip on him has that mark still before it
        private static bool MarkAhead(Player p, string name)
        {
            BodyAnimator a = BodyAnimator.Of(p);
            PAnimClip c = a != null ? BodyClips.Find(a.ClipIn(2)) : null;
            if (c != null) foreach (PAnimEvent m in c.Marks) if (m.Sound == name) return a.TimeIn(2) < m.T;
            return false;
        }

        private static void PlayerStep(Player p, string[] w)
        {
            if (w[1] == "mark")
            {
                if (!MarkAhead(p, w[2])) { Note("player mark: у кліпі на гравці нема попереду мітки «" + w[2] + "» — крок пропущено"); return; }
                Hold(w.Length > 3 ? F(w[3], 30f) : 30f);
                return;
            }
            if (w[1] == "clip")
            {
                if (w[2] == "none") { Gestures.SceneEnd(p, PoseFade); return; }
                PAnimClip c = BodyClips.Find(w[2]);
                if (c == null || !Gestures.Scene(p, w[2], PoseFade)) { Note("player clip: нема кліпу «" + w[2] + "» — крок пропущено"); return; }
                if (!c.Loop && Array.IndexOf(w, "nowait") < 0) Hold(c.Length);
                return;
            }
            if (w[1] == "go")
            {
                string why = StoryWalk.Go(p, w[2], F(w[3], 1f), Array.IndexOf(w, "front") > 0, Array.IndexOf(w, "run") > 0);
                if (why != null) Note("player go: " + why + " — крок пропущено");
                return;
            }
            if (w[1] == "emote")
            {
                if (w[2] == "stop") { Gestures.StopEmote(p); return; }
                EmoteTalk.OursUntil = Time.time + 1f; // (the scene's gesture: the ravens do not talk about it)
                p.StartEmote(w[2], Array.IndexOf(w, "loop") < 0);
                return;
            }
            Vector3 at;
            if (!StoryWhere.Find(w[2], out at)) { Note("player face: нема місця «" + w[2] + "» — крок пропущено"); return; }
            Vector3 d = at - p.transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f || FLookYaw == null) return;
            FLookYaw.SetValue(p, Quaternion.LookRotation(d.normalized));
            p.FaceLookDirection();
        }

        private static float ReadTime(string line) { return Mathf.Clamp(1.2f + line.Length * 0.06f, 2f, 8f); }

        // ---------------------------------------------------------------- small things
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }
        private static Vector3 PV(string[] a, int i) { return new Vector3(F(a[i], 0f), F(a[i + 1], 0f), F(a[i + 2], 0f)); }

        internal static void Note(string s)
        {
            Recent.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
            if (Recent.Count > 60) Recent.RemoveAt(0);
            StoryQuests.Note(s); // (the track's log and the game's log)
        }
    }
}
#endif

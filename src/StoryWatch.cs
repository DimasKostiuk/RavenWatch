#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A scene seen by everyone near, as its director sees it (the user 2026-11-07: «всі бачать це важливо»; 2026-11-20: «всі в певному радіусі бачать її
    // так само», one who was late joins it). «alone: yes» never. One director: the game that plays it.
    // The circle: round the scene's place (no place, or one that roams: round its director), Radius m — a scene's own: «audience: <m>» (RadiusMost at
    //   most: the game draws for certain only what is in the zones next to the player, 64 m). In at its radius, out Edge m farther: one on its rim does
    //   not flip. The director's pulse every PulseEvery s says the scene, its step, the circle: nobody has to be there when it begins.
    // What each step shows to whom it says itself (StoryScenes.StepSeen, a word every step must give):
    //   World  — people, doors, things: the network shows them;
    //   Shared — reaches every game near by its own way: the bird, the falcons, a sound with a place, the lines (subtitles here);
    //   Screen — the picture and what is heard (bars, the black, the glow, a vision, music, beds, the mix, the ear): the director's own AND, by the
    //            same word, on the game of each one who watches (StepKind.Watch); one who comes in the middle gets all that ran so far, put at once;
    //   Own    — the director's alone: his body, his letter. His camera too — its shots are found on his game only, and its pose goes to those who
    //            watch PoseEvery s apart (a cut said, never eased over), shown PoseLag s late and eased between two poses.
    // So one who watches has the keys exactly when the director has not: the bars, the black or a shot hold them (StoryCamera.TakesPlayer), nothing else.
    // Only a scene that takes the picture (a shot, the bars, the black: SceneDef.Cinema) takes anyone's: a letter's raven gives those near its lines only.
    // He is out of the picture — his camera, keys and sounds his at once, the lines still come — while he fights or is dead, has a window open, or said
    // so (Esc: for this scene); calm again for CalmFor s, he is in again from where it is now. Bodies: the players who watch are not drawn while a shot
    // holds, on the director's game and on theirs (StoryAlone), unless the scene says «audience: seen».
    // The scene's end or the director gone (nothing heard for Silence s) gives everything back.
    // A scene of the server does not end with one player (StoryScenes.OwnOff): the director hit or dead gets his camera and keys back, those who watch
    // theirs, and it goes on without his Own and Screen steps. He leaves the game, or walks off: one it leads near takes it up at the step it got to
    // (Resume: StoryScenes.TryPlay, its claim as ever).
    // Cost: nothing with no scene; while one plays a pulse every PulseEvery s to every game, a few words a step, and while a shot holds its pose
    // PoseEvery s apart to those who watch alone.
    internal static class StoryWatch
    {
        internal static float PulseEvery = 2f, Silence = 6f, Radius = 40f, RadiusMost = 60f, Edge = 10f, ResumeFor = 30f;
        internal static float PoseEvery = 0.05f, PoseLost = 1f, PoseLag = 0.15f, CamBlend = 1.2f, CamFar = 80f; // (CamFar: a shot farther from his body than the game draws — not taken)
        internal static float HereEvery = 4f, Gone = 12f, CalmFor = 4f;
        private const string Rpc = "RavenWatch_StoryWatch", RpcPose = "RavenWatch_StoryWatchPose", RpcHere = "RavenWatch_StoryWatchHere";
        private const int Out = 0, Hello = 1, In = 2, Still = 3; // (what one who watches says to the director: out of the picture · near: the bird · in: all so far · still in)
        private const int FCinema = 1, FSeen = 2;
        private const char Apart = '\u001f';

        // ---------------------------------------------------------------- the director
        private static string _scene = "";
        private static bool _shared, _seen, _fixed, _streaming;
        private static float _pulseAt, _poseAt, _radius;
        private static double _began;
        private static Vector3 _lastAt, _centre; // (_lastAt: a stop comes when he has no body any more — death, leaving: it must still reach them)
        private static readonly List<int> _ran = new List<int>(); // (the Screen steps that ran, in their order: all one who comes in the middle needs)
        private static readonly Dictionary<long, float> _audience = new Dictionary<long, float>(); // session: who takes its picture → last heard of
        private static readonly List<long> _lost = new List<long>();

        internal static void Begin(StoryScenes.SceneDef s)
        {
            Leave(true); // (a scene of one's own begins: another's, watched till now, lets go of all it held here first)
            _scene = s.Id; _shared = !s.Alone; _seen = s.AudienceSeen;
            _radius = Mathf.Min(s.Audience > 0f ? s.Audience : Radius, RadiusMost);
            _fixed = !s.Roams && s.Place.Length > 0 && StoryWhere.Find(s.Place, out _centre);
            _ran.Clear(); _audience.Clear(); _streaming = false;
            _began = Time.realtimeSinceStartupAsDouble;
            _pulseAt = 0f;
        }

        // every frame of a scene playing
        internal static void Tick()
        {
            if (!_shared || ZRoutedRpc.instance == null) return;
            if (Player.m_localPlayer != null) _lastAt = Player.m_localPlayer.transform.position;
            if (Time.time >= _pulseAt)
            {
                _pulseAt = Time.time + PulseEvery;
                Send(ZRoutedRpc.Everybody, "on", "", StoryScenes.At);
                foreach (KeyValuePair<long, float> e in _audience) if (Time.time - e.Value > Gone) _lost.Add(e.Key); // (its game left, or its «out» never came)
                foreach (long id in _lost) _audience.Remove(id);
                _lost.Clear();
            }
            if (_audience.Count == 0) { _streaming = false; return; }
            bool on = StoryScenes.Cinema && StoryCamera.Active && !StoryCamera.Leaving; // (on its way back the camera is his own again: theirs goes back to them)
            if (!on && !_streaming) return;
            if (on && Time.time < _poseAt) return;
            _poseAt = Time.time + PoseEvery;
            _streaming = on;
            StoryCamera.Pose pose = StoryCamera.Now();
            ZPackage pkg = new ZPackage();
            pkg.Write((float)(Time.realtimeSinceStartupAsDouble - _began)); pkg.Write(pose.At); pkg.Write(pose.Rot); pkg.Write(pose.Fov);
            pkg.Write(StoryCamera.Cuts); pkg.Write(on); pkg.Write(StoryCamera.Eyes);
            foreach (long id in _audience.Keys) ZRoutedRpc.instance.InvokeRoutedRPC(id, RpcPose, pkg);
        }

        // a Screen step ran on the director's game (StoryScenes.Begin; live false: put at once as a scene is played from a later step): the same on theirs
        internal static void Step(int at, bool live)
        {
            if (!_shared) return;
            _ran.Add(at);
            if (!live) return;
            foreach (long id in _audience.Keys) Send(id, "step", "", at);
        }

        // a black the scene itself puts over the picture outside any step (a held walk put on its spot: StoryWalk): theirs too
        internal static void Black(bool on, float seconds)
        {
            if (!_shared || !StoryScenes.Cinema) return;
            foreach (long id in _audience.Keys) Send(id, "black", (on ? "1 " : "0 ") + seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), StoryScenes.At);
        }

        // what the pulse says has changed (the director given back: OwnOff): said now, not in two seconds
        internal static void Now() { _pulseAt = 0f; }

        // a line of the scene, for those near (the director's own subtitle is his)
        internal static void Line(string text) { Line("", text); }
        internal static void Line(string who, string text) { if (_shared) Send(ZRoutedRpc.Everybody, "line", (who ?? "") + Apart + text, StoryScenes.At); } // (who apart: its name gold on their screen too)

        // over; stopped before its end: at its step, for one who takes it up
        internal static void End(bool broken, int at)
        {
            if (_shared) Send(ZRoutedRpc.Everybody, broken ? "stop" : "end", "", at);
            _shared = false; _streaming = false; _audience.Clear(); _ran.Clear();
        }

        private static void Send(long to, string what, string text, int at)
        {
            Player p = Player.m_localPlayer;
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(what); pkg.Write(_scene); pkg.Write(at);
            pkg.Write(_fixed ? _centre : p != null ? p.transform.position : _lastAt); pkg.Write(_radius);
            pkg.Write((StoryScenes.Cinema ? FCinema : 0) | (_seen ? FSeen : 0));
            pkg.Write(text);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, Rpc, pkg);
        }

        // one who watches says where he is with it
        private static void OnHere(long sender, int what, string scene)
        {
            if (StoryScenes.Playing.Length == 0 || !_shared || scene != _scene) return; // (said of the scene before this one: its answer would be this one's)
            if (what == Out) { _audience.Remove(sender); return; }
            if (what == Hello) { StoryScenes.BirdSync(sender); return; } // (the bird and the falcons as they are now)
            _audience[sender] = Time.time;
            if (what != In) return;
            StringBuilder sb = new StringBuilder();
            foreach (int i in _ran) { if (sb.Length > 0) sb.Append(','); sb.Append(i); }
            Send(sender, "state", sb.ToString(), StoryScenes.At); // (all that ran till this moment; what runs after it comes after it, step by step)
            _poseAt = 0f;
        }

        // ---------------------------------------------------------------- one who watches
        private static long _dir;
        private static string _watching = "", _resume = "", _quitOf = ""; // (_quitOf: the scene he chose to see with his own eyes — till it ends, wherever he walks meanwhile)
        private static int _at, _resumeAt;
        private static float _heard, _resumeUntil, _hereAt, _calmAt;
        private static bool _cinema, _bodies, _in, _synced, _camOn, _camMine, _mirrored, _eyes; // (_camMine: the game's camera is held by this — a shot of the director's on it, or easing back from one)

        // another player's scene plays near this one now (the Hall's raven keeps off heads then: LocRaven)
        internal static bool Watching { get { return _dir != 0L && Time.time - _heard < Silence; } }
        // its picture is on this screen (no scene of one's own begins under it: StoryScenes.TryPlay)
        internal static bool InPicture { get { return _in; } }

        private static void OnMsg(long sender, ZPackage pkg)
        {
            string what = pkg.ReadString(), scene = pkg.ReadString();
            int at = pkg.ReadInt();
            Vector3 centre = pkg.ReadVector3();
            float radius = pkg.ReadSingle();
            int flags = pkg.ReadInt();
            string text = pkg.ReadString();
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID() || StoryScenes.Playing.Length > 0) return;
            if (what == "on")
            {
                bool mine = _dir == sender && _watching == scene;
                if (StoryWhere.Apart(centre, me.transform.position) > radius + (mine ? Edge : 0f)) { if (_dir == sender) Leave(false); return; }
                if (_dir != 0L && _dir != sender && Time.time - _heard < Silence) return; // (two scenes near: the first one, not a flip between them every pulse)
                if (!mine)
                {
                    Leave(false);
                    _dir = sender; _watching = scene; _calmAt = 0f;
                    Story.Log("watch: joined «" + scene + "» at its step " + (at + 1));
                    Here(Hello);
                }
                _at = at; _heard = Time.time; _cinema = (flags & FCinema) != 0; _bodies = (flags & FSeen) != 0;
                return;
            }
            if (_dir != sender || scene != _watching) return;
            if (what == "line")
            {
                int cut = text.IndexOf(Apart);
                string who = cut >= 0 ? text.Substring(0, cut) : "", said = cut >= 0 ? text.Substring(cut + 1) : text;
                StoryScreen.Sub(who, said, RavenSpeech.Seconds(said, RavenSpeech.Pace.Scene) + 0.4f);
                return;
            }
            if (what == "state")
            {
                if (!_in) return; // (asked, and out of it again before the answer came)
                foreach (string n in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int i; if (int.TryParse(n, out i)) Mirror(i, true); }
                _synced = true;
                return;
            }
            if (what == "step") { if (_in && _synced) Mirror(at, false); return; } // (before the answer: it is in the answer)
            if (what == "black")
            {
                string[] w = text.Split(' ');
                float s;
                if (_in && _synced && w.Length > 1 && float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s)) { _mirrored = true; StoryScreen.Fade(w[0] == "1", s); }
                return;
            }
            if (what == "stop") { _resume = scene; _resumeAt = From(scene, at); _resumeUntil = Time.time + ResumeFor; }
            _quitOf = "";
            Leave(what == "stop", what == "end");
        }

        // a Screen step of the scene watched, on this game: as it begins, or put at once
        private static void Mirror(int at, bool atOnce)
        {
            StoryScenes.SceneDef s = StoryScenes.Find(_watching);
            if (s == null || at < 0 || at >= s.Steps.Count) return; // (another build's scene file: left out)
            StoryScenes.Step st = s.Steps[at];
            if (st.Kind == null || st.Kind.Watch == null) return;
            _mirrored = true;
            try { st.Kind.Watch(st, atOnce); }
            catch (Exception e) { Story.Log("watch: «" + st.Line + "» of «" + _watching + "» failed here: " + e.Message); }
        }

        // ---- the director's camera here: its poses kept a moment, the picture a little behind them and eased between two (a cut is a cut)
        private struct Sample { public double T; public StoryCamera.Pose Pose; public int Cuts; }
        private static readonly List<Sample> _poses = new List<Sample>(); // session: the last few poses of the shot watched
        private static double _off, _poseHeard;

        private static void OnPose(long sender, ZPackage pkg)
        {
            double sent = pkg.ReadSingle();
            StoryCamera.Pose pose = new StoryCamera.Pose { At = pkg.ReadVector3(), Rot = pkg.ReadQuaternion(), Fov = pkg.ReadSingle() };
            int cuts = pkg.ReadInt();
            bool on = pkg.ReadBool(), eyes = pkg.ReadBool();
            Player me = Player.m_localPlayer;
            if (_dir != sender || !_in || me == null || GameCamera.instance == null) return;
            if (!on || Vector3.Distance(pose.At, me.transform.position) > CamFar) { CamOff(false); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            _off = _poses.Count == 0 ? now - sent : Math.Min(_off + 0.0005, now - sent); // (the shortest way a pose ever took: the clock of the two games, one against the other)
            if (_poses.Count >= 8) _poses.RemoveAt(0);
            _poses.Add(new Sample { T = sent, Pose = pose, Cuts = cuts });
            _poseHeard = now;
            if (eyes != _eyes) { _eyes = eyes; StoryAlone.Again(); } // (a shot through his eyes: he himself is not drawn here either)
            if (_camOn) return;
            _camOn = _camMine = true;
            StoryCamera.Shot(PoseNow, CamBlend);
        }

        private static StoryCamera.Pose PoseNow()
        {
            int n = _poses.Count;
            if (n == 0) return StoryCamera.Now();
            double t = Time.realtimeSinceStartupAsDouble - _off - PoseLag;
            int i = n - 1;
            while (i > 0 && _poses[i].T > t) i--;
            Sample a = _poses[i];
            if (i == n - 1 || a.T > t) return a.Pose;
            Sample b = _poses[i + 1];
            if (b.Cuts != a.Cuts || b.T - a.T < 0.0001) return a.Pose; // (a cut between them: the old shot to its last moment, then the new one)
            float k = (float)((t - a.T) / (b.T - a.T));
            return new StoryCamera.Pose { At = Vector3.Lerp(a.Pose.At, b.Pose.At, k), Rot = Quaternion.Slerp(a.Pose.Rot, b.Pose.Rot, k), Fov = Mathf.Lerp(a.Pose.Fov, b.Pose.Fov, k) };
        }

        private static void CamOff(bool atOnce)
        {
            _poses.Clear();
            if (_eyes) { _eyes = false; StoryAlone.Again(); }
            if (atOnce) { if (_camMine && StoryCamera.Active) StoryCamera.Reset(); _camOn = _camMine = false; return; } // (one already easing back too: his at once)
            if (!_camOn) return;
            _camOn = false;
            StoryCamera.Off(CamBlend);
        }

        private static void Here(int what) { if (_dir != 0L && ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(_dir, RpcHere, what, _watching); }

        // each part alone: one that fails must not leave the rest undone — the camera, the black or the keys taken (as StoryScenes.Release)
        private static void Let(string what, Action act)
        {
            try { act(); }
            catch (Exception e) { Story.Log("watch: " + what + ": " + e); }
        }

        // into its picture: all that ran so far asked for, the players who watch put out of the shot
        private static void Enter()
        {
            _in = true; _synced = false; _hereAt = Time.time + HereEvery;
            StoryScenes.SceneDef s = StoryScenes.Find(_watching);
            if (s != null) Let("its sounds read", () => StoryAudio.Prepare(s)); // (its files read now: its steps find them there)
            long dir = _dir;
            Let("the players out of its shots", () => StoryAlone.Begin(o => o.GetZDOID().UserID == dir ? _eyes : !_bodies)); // (the director is the one in the picture; a shot through his eyes: not he either)
            Here(In);
            StoryScreen.Sub(Lang.T("Поруч іде сцена. Esc — дивитись своїми очима."), 4f); // (this makes the screen's object too: it is what sees him leave the world — NoBody)
            Story.Log("watch: in the picture of «" + _watching + "»");
        }

        // out of its picture: all it put on this screen and into these ears given back (atOnce: a fight, a stop; else softly).
        // ended: the scene played out — only then its music may ring on («keep N»), as on its director's game
        private static void Drop(bool atOnce, bool ended = false)
        {
            if (!_in) return;
            _in = false; _synced = false;
            Let("its director told", () => Here(Out));
            Let("the camera", () => CamOff(atOnce));
            Let("the players shown", StoryAlone.End);
            if (!_mirrored) return;
            _mirrored = false;
            Let("the screen", () => { if (atOnce) StoryScreen.Reset(); else { StoryScreen.Bars(false); StoryScreen.Fade(false, 1f); StoryScreen.GlowOff(); StoryScreen.DimOff(); } });
            Let("what is heard", () => Audio.End(Audio.Scene, atOnce ? Audio.BrokenFade : Audio.EndFade, !ended));
        }

        // gone from it
        private static void Leave(bool atOnce, bool ended = false)
        {
            Drop(atOnce, ended);
            _dir = 0L; _watching = ""; _cinema = false; _camMine = false;
        }

        // he has no body in a world any more (the game left, the server gone; dead and not yet back): nothing of another's scene stays on his
        // screen. Asked by the screen's own object every frame (StoryScreen.Update): the story's frame does not run then
        internal static void NoBody() { if (_dir != 0L) Leave(true); }

        // a window of his is open: its picture is not put over it
        private static bool Busy() { return InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible() || TextInput.IsVisible() || global::Console.IsVisible(); } // (StoreGui: the book's windows too — RavenBookAsWindowPatch)

        // every frame (StoryQuests.Frame)
        internal static void Look(Player p)
        {
            if (_dir == 0L) return;
            if (Time.time - _heard > Silence)
            {
                Story.Log("watch: nothing heard of «" + _watching + "» for " + Silence + " s: its director is gone");
                _resume = _watching; _resumeAt = From(_watching, _at); _resumeUntil = Time.time + ResumeFor;
                Leave(true);
                return;
            }
            bool pressed = p.IsDead() || Senses.Pressed(p); // (hit, or dead: his camera and keys are his, whatever the scene shows)
            if (pressed || Busy()) _calmAt = Time.time + CalmFor;
            if (!_in)
            {
                if (_cinema && _quitOf != _watching && Time.time >= _calmAt) Enter();
                return;
            }
            if (RavenKeys.Down(KeyCode.Escape)) { _quitOf = _watching; Story.Log("watch: he looks with his own eyes till «" + _watching + "» ends"); Drop(false); return; } // (asked first: the game's menu opens on the same key, and a window alone lets him in again)
            if (!_cinema || Time.time < _calmAt) { Drop(pressed || !_cinema); return; } // (the director given back: so are they)
            if (Time.time >= _hereAt) { _hereAt = Time.time + HereEvery; Here(Still); }
            if (_camOn && !StoryCamera.Active) _camOn = _camMine = false; // (the camera gave itself back — a shot that failed: the next pose takes it again)
            if (_camOn && Time.realtimeSinceStartupAsDouble - _poseHeard > PoseLost) CamOff(false);
            StoryAlone.Tick();
        }

        // where one who takes it up begins: the step it got to, cut off half way (a line, a walk: played again, not lost); past it only when that
        // step is lasting (it ran at its start, and its «played» key in the world may not be here yet: never twice — StoryQuests.Committed)
        private static int From(string scene, int at)
        {
            StoryScenes.SceneDef s = StoryScenes.Find(scene);
            if (at < 0 || s == null || at >= s.Steps.Count) return Mathf.Max(0, at + 1);
            StoryScenes.StepKind k = s.Steps[at].Kind;
            return k != null && k.Lasting ? at + 1 : at;
        }

        // this scene was left unfinished by its director near me: the step to take it up at
        internal static bool Resume(string scene, out int at)
        {
            at = _resumeAt;
            return _resume == scene && Time.time < _resumeUntil;
        }

        internal static void Resumed() { _resume = ""; }

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnMsg);
            ZRoutedRpc.instance.Register<ZPackage>(RpcPose, OnPose);
            ZRoutedRpc.instance.Register<int, string>(RpcHere, OnHere);
        }
    }
}
#endif

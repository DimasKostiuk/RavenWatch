#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A scene seen by everyone near (docs/plans/story-server-core.md; the user 2026-11-07: «всі бачать це важливо»), «alone: yes» never. One director: the game that plays it.
    // What each step shows to whom it says itself (StoryScenes.StepSeen, a word every step must give): World — people, doors, things, the network shows them;
    // Shared — reaches every game near: the bird, a sound, the words here; Own — the director's: his camera, bars, body.
    // Whoever comes near joins it at that moment: the director's pulse every PulseEvery s says what plays and how far it got, so nobody has to be there when it begins.
    //   On his game the scene's lines come as subtitles, the bird comes as it is now. He keeps his eyes and his keys, unless the scene says:
    //   watchers take · watchers take camera · watchers free   a step: the keys taken from all near (TakesPlayer) · and the director's camera too · given back.
    //   The scene's end or the director gone (nothing heard for Silence s) gives everything back.
    // A scene of the server does not end with one player (StoryScenes.OwnOff): the director hit or dead gets his camera and keys back and it goes on without
    // his Own steps. He leaves the game, or walks off: one it leads near takes it up at the step it got to (Resume: StoryScenes.TryPlay, its claim as ever).
    // Cost: nothing with no scene; while one plays a pulse every PulseEvery s to every game, and with «take camera» its pose PoseEvery s: only in those moments.
    internal static class StoryWatch
    {
        internal static float PulseEvery = 2f, Silence = 6f, Range = 100f, PoseEvery = 0.1f, PoseLost = 1f, ResumeFor = 30f;
        private const string Rpc = "RavenWatch_StoryWatch", RpcPose = "RavenWatch_StoryWatchPose", RpcSync = "RavenWatch_StoryWatchSync";

        // ---------------------------------------------------------------- the director
        private static string _scene = "";
        private static bool _shared, _take, _takeCam;
        private static float _pulseAt, _poseAt;
        private static Vector3 _lastAt; // (a stop comes when he has no body any more: death, leaving: it must still reach them)

        internal static void Begin(StoryScenes.SceneDef s)
        {
            _scene = s.Id; _shared = !s.Alone; _take = _takeCam = false;
            Leave();
            _pulseAt = 0f;
        }

        // every frame of a scene playing
        internal static void Tick()
        {
            if (!_shared || ZRoutedRpc.instance == null) return;
            if (Player.m_localPlayer != null) _lastAt = Player.m_localPlayer.transform.position;
            if (Time.time >= _pulseAt) { _pulseAt = Time.time + PulseEvery; Send("on", ""); }
            if (!_takeCam || !StoryCamera.Active || Time.time < _poseAt) return;
            _poseAt = Time.time + PoseEvery;
            StoryCamera.Pose pose = StoryCamera.Now();
            ZPackage pkg = new ZPackage();
            pkg.Write(pose.At); pkg.Write(pose.Rot); pkg.Write(pose.Fov);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcPose, pkg);
        }

        internal static void Take(bool on, bool camera) { _take = on; _takeCam = on && camera; _pulseAt = 0f; }

        // a line of the scene, for those near (the director's own subtitle is his)
        internal static void Line(string text) { Line("", text); }
        internal static void Line(string who, string text) { if (_shared) Send("line", (who ?? "") + Apart + text); } // (who apart: its name gold on their screen too)
        private const char Apart = '\u001f';

        // over; stopped before its end: at its step, for one who takes it up
        internal static void End(bool broken, int at)
        {
            if (_shared) Send(broken ? "stop" : "end", "", at);
            _shared = false; _take = _takeCam = false;
        }

        private static void Send(string what, string text) { Send(what, text, StoryScenes.At); }

        private static void Send(string what, string text, int at)
        {
            Player p = Player.m_localPlayer;
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(what); pkg.Write(_scene); pkg.Write(at); pkg.Write(p != null ? p.transform.position : _lastAt); pkg.Write(_take); pkg.Write(_takeCam); pkg.Write(text);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        // ---------------------------------------------------------------- one who watches
        private static long _dir;
        private static string _watching = "", _resume = "";
        private static int _at, _resumeAt;
        private static float _heard, _resumeUntil, _poseHeard;
        private static bool _holds, _holdsCam, _camOn;
        private static StoryCamera.Pose _pose;

        // the keys are the scene's (StoryCamera.TakesPlayer)
        internal static bool Holds { get { return _dir != 0L && _holds && !_fighting; } }
        private static bool _fighting; // (hit, or dead: his keys are his, whatever the scene says)

        private static void OnMsg(long sender, ZPackage pkg)
        {
            string what = pkg.ReadString(), scene = pkg.ReadString();
            int at = pkg.ReadInt();
            Vector3 where = pkg.ReadVector3();
            bool take = pkg.ReadBool(), cam = pkg.ReadBool();
            string text = pkg.ReadString();
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID() || StoryScenes.Playing.Length > 0) return;
            if (what == "on")
            {
                if (Vector3.Distance(where, me.transform.position) > Range) { if (_dir == sender) Leave(); return; }
                if (_dir != 0L && _dir != sender && Time.time - _heard < Silence) return; // (two scenes near: the first one, not a flip between them every pulse)
                if (_dir != sender || _watching != scene)
                {
                    Leave();
                    _dir = sender; _watching = scene;
                    Story.Log("watch: joined «" + scene + "» at its step " + (at + 1));
                    ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcSync); // (the bird as it is now)
                }
                _at = at; _heard = Time.time; _holds = take; _holdsCam = cam;
                return;
            }
            if (_dir != sender) return;
            if (what == "line")
            {
                int cut = text.IndexOf(Apart);
                string who = cut >= 0 ? text.Substring(0, cut) : "", said = cut >= 0 ? text.Substring(cut + 1) : text;
                StoryScreen.Sub(who, said, RavenSpeech.Seconds(said, RavenSpeech.Pace.Scene) + 0.4f);
                return;
            }
            if (what == "stop") { _resume = scene; _resumeAt = From(scene, at); _resumeUntil = Time.time + ResumeFor; }
            Leave();
        }

        private static void OnPose(long sender, ZPackage pkg)
        {
            StoryCamera.Pose pose = new StoryCamera.Pose { At = pkg.ReadVector3(), Rot = pkg.ReadQuaternion(), Fov = pkg.ReadSingle() };
            if (_dir != sender || !_holdsCam || _fighting) return;
            _pose = pose; _poseHeard = Time.time;
            if (_camOn) return;
            _camOn = true;
            StoryCamera.Shot(() => _pose, 0.5f);
            StoryScreen.Bars(true);
        }

        private static void OnSync(long sender) { if (StoryScenes.Playing.Length > 0) StoryScenes.BirdSync(sender); }

        private static void CamOff()
        {
            if (!_camOn) return;
            _camOn = false;
            StoryCamera.Off(1f);
            StoryScreen.Bars(false);
        }

        // gone from it: all it held given back
        private static void Leave()
        {
            CamOff();
            _dir = 0L; _watching = ""; _holds = _holdsCam = false;
        }

        // every frame (StoryQuests.Frame)
        internal static void Look(Player p)
        {
            if (_dir == 0L) return;
            _fighting = p.IsDead() || Senses.Pressed(p);
            if (Time.time - _heard > Silence)
            {
                Story.Log("watch: nothing heard of «" + _watching + "» for " + Silence + " s: its director is gone");
                _resume = _watching; _resumeAt = From(_watching, _at); _resumeUntil = Time.time + ResumeFor;
                Leave();
                return;
            }
            if (_camOn && (!_holdsCam || _fighting || Time.time - _poseHeard > PoseLost)) CamOff();
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
            ZRoutedRpc.instance.Register(RpcSync, OnSync);
        }
    }
}
#endif

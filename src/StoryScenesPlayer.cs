#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), the step «player <verb> …»: each verb one entry (StepVerb) — what it needs, where it names a place, how it
    // begins, how long it goes on, what it leaves when the scene is played from a later step. A new verb is a new entry, the director never changes.
    internal static partial class StoryScenes
    {
        private const string PlayerUsage = "player say <текст> · player face <місце|npc.x|bird> · player emote <жест>|stop · player go <місце> <м> [front] [before] [run] [keep] [soft] [nowait] · player back <м> · player stop [с] · player look <місце|bird|npc.x>|forward · player gait <хода>|none · player pace walk|jog|run · player arrive [місце] [м] · player clip <кліп>|none [nowait] · player mark <мітка> [с]";

        // a walk as long as the walk itself may take, not the general StepMax (a long «player go» stopped halfway at 30 s)
        private static void WalkClock() { if (StoryWalk.On) _limit = StoryWalk.Until + 2f; }

        // a lasting gesture of the scene's («player emote sit loop»): the game keeps it till he moves — the scene's end or stop takes it off (Release, OwnOff)
        private static bool _emoteLoop;
        private static void StopEmote(Player p) { if (!_emoteLoop) return; _emoteLoop = false; if (p != null) Gestures.StopEmote(p); }

        // The hero says a line aloud — the ONE way for a scene's «player say» and a talk on the way (StoryRoad, «hero:»): his words in his language
        // and forms, a subtitle under his name on his screen (sub false: none — he has no bubble over his head), hummed in his own voice, and the
        // people of his scene turn to him (NpcSpeech.HeroSays; out of a scene, those within NpcSpeech.ListenNear). The seconds it is said; said: the words
        internal static float HeroLine(Player p, string words, bool sub, out string said)
        {
            said = Grammar.Fix(Lang.T(words), p);
            float t = RavenSpeech.Seconds(said, RavenSpeech.Pace.Scene);
            if (sub) StoryScreen.Sub(p.GetPlayerName(), said, t + 0.4f);
#if RAVEN_NPC
            NpcSpeech.HeroSays(p, said, t, "babble", 0x53434E45u);
#endif
            return t;
        }

        private static StepKind PlayerKind()
        {
            return new StepKind(StepSeen.Own) { Min = 1, VerbAt = 1, Usage = PlayerUsage, Verbs = new Dictionary<string, StepVerb>
            {
                { "face", new StepVerb(StepSeen.Own) { Min = 1, PlaceAt = 2,
                    Begin = (p, st) =>
                    {
                        Vector3 at;
                        if (!StoryWhere.Find(st.W[2], out at)) { Note("player face: нема місця «" + st.W[2] + "» — крок пропущено"); return; }
                        StoryWalk.Face(p, at - p.transform.position, Array.IndexOf(st.W, "slow") > 2);
                        StoryLook.At(st.W[2]);
                    },
                    Setup = (p, st) =>
                    {
                        Vector3 at;
                        StoryLook.At(st.W[2]);
                        if (FLookYaw == null || !StoryWhere.Find(st.W[2], out at)) return;
                        FLookYaw.SetValue(p, Quaternion.LookRotation(Flat(at - p.transform.position)));
                        p.FaceLookDirection();
                    } } },
                { "look", new StepVerb(StepSeen.Own) { Min = 1, PlaceAt = 2, PlaceNot = new[] { "forward" }, Begin = PlayerLook, Setup = PlayerLook } },
                // «player say <текст>»: the hero says a line himself (the user 2026-11-16: a few short lines in the scenes, where silence looks odd; no choice
                // in a scene) — a subtitle under his own name, on his screen and those near (StoryWatch), and hummed in his own voice, the «hero» timbre of
                // his sex (NpcTimbre.OfHero, NpcBabble: the same hum as his answers in a talk; the voice package, when it has his voice, takes over there)
                { "say", new StepVerb(StepSeen.Shared) { Min = 1, // (Shared, as «sub»: those near get it even once the director has his own back — a server's scene, he was hit)
                    Begin = (p, st) =>
                    {
                        string line;
                        float t = HeroLine(p, Rest(st.Text, 2), !_ownOff, out line); // (the director's screen taken back — a server's scene, he was hit: no subtitle of his own)
                        Hold(t);
                        StoryWatch.Line(p.GetPlayerName(), line);
                    } } },
                { "gait", new StepVerb(StepSeen.Own) { Min = 1, Begin = PlayerGait, Setup = PlayerGait, Busy = (p, st) => false } },
                { "pace", new StepVerb(StepSeen.Own) { Min = 1, Begin = (p, st) => StoryWalk.Pace(st.W[2]), Busy = (p, st) => false } },
                { "stop", new StepVerb(StepSeen.Own) { Begin = (p, st) => StoryWalk.Halt(st.W.Length > 2 ? F(st.W[2], 0.8f) : 0.8f), Busy = (p, st) => StoryWalk.Halting, Done = (p, st) => StoryWalk.Outcome } },
                { "emote", new StepVerb(StepSeen.Own) { Min = 1,
                    Begin = (p, st) =>
                    {
                        if (st.W[2] == "stop") { Gestures.StopEmote(p); _emoteLoop = false; return; }
                        EmoteTalk.OursUntil = Time.time + 1f; // (the scene's gesture: the ravens do not talk about it)
                        bool loop = Array.IndexOf(st.W, "loop") >= 0;
                        if (!p.StartEmote(st.W[2], !loop)) { Note("player emote: гра не почала жест «" + st.W[2] + "» (сидить на чомусь, тягне лук, нема сил…)"); return; }
                        if (loop) _emoteLoop = true; // (a gesture that lasts — sitting: the scene's end takes it off, Release)
                    } } },
                { "clip", new StepVerb(StepSeen.Own) { Min = 1,
                    Begin = (p, st) =>
                    {
                        if (st.W[2] == "none") { Gestures.SceneEnd(p, PoseFade); return; }
                        PAnimClip c = BodyClips.Find(st.W[2]);
                        if (c == null || !Gestures.Scene(p, st.W[2], PoseFade)) { Note("player clip: нема кліпу «" + st.W[2] + "» — крок пропущено"); return; }
                        if (!c.Loop && Array.IndexOf(st.W, "nowait") < 0) Hold(c.Length);
                    },
                    Done = (p, st) => st.W[2] == "none" ? null : ClipOn(p, st.W[2]) } },
                { "mark", new StepVerb(StepSeen.Own) { Min = 1,
                    Begin = (p, st) =>
                    {
                        if (!MarkAhead(p, st.W[2])) { if (MarkTime(p, st.W[2]) < 0f) Note("player mark: у кліпі на гравці нема мітки «" + st.W[2] + "» — крок пропущено"); return; } // (already passed, a step before it took long: nothing to wait for)
                        Hold(st.W.Length > 3 ? F(st.W[3], 30f) : 30f);
                    },
                    Busy = (p, st) => Time.time < _waitFor && MarkAhead(p, st.W[2]),
                    Done = (p, st) => MarkSaid(p, st.W[2]) } },
                { "go", new StepVerb(StepSeen.Own) { Min = 2, PlaceAt = 2,
                    Begin = (p, st) =>
                    {
                        string[] w = st.W;
                        string why = StoryWalk.Go(p, w[2], F(w[3], 1f), Array.IndexOf(w, "front") > 0, Array.IndexOf(w, "run") > 0, Array.IndexOf(w, "keep") > 0, Array.IndexOf(w, "soft") > 0, Array.IndexOf(w, "before") > 0);
                        if (why != null) { Note("player go: " + why + " — крок пропущено"); return; }
                        WalkClock();
                    },
                    Setup = (p, st) =>
                    {
                        string[] w = st.W;
                        if (StoryWalk.Go(p, w[2], F(w[3], 1f), Array.IndexOf(w, "front") > 0, false, false, false, Array.IndexOf(w, "before") > 0) != null) return;
                        Vector3 to = StoryWalk.Target, way = StoryWalk.Way;
                        StoryWalk.Stop();
                        StoryWalk.Put(p, to, way); // (on the floor under the spot: in a building, not the land beneath it)
                    },
                    Busy = (p, st) => StoryWalk.On && Array.IndexOf(st.W, "nowait") < 0,
                    Late = st => StoryWalk.Stop(),
                    Done = (p, st) => StoryWalk.Outcome } },
                { "back", new StepVerb(StepSeen.Own) { Min = 1,
                    Begin = (p, st) => { StoryWalk.Back(p, F(st.W[2], 1.2f)); WalkClock(); },
                    Busy = (p, st) => StoryWalk.On && Array.IndexOf(st.W, "nowait") < 0,
                    Late = st => StoryWalk.Stop(),
                    Done = (p, st) => StoryWalk.Outcome } },
                { "arrive", new StepVerb(StepSeen.Own) { // (its waiting: Busy; [м] — till only that much of the walk is left)
                    Begin = (p, st) => WalkClock(),
                    Busy = (p, st) => StoryWalk.On && StoryWalk.Left(p) > (st.W.Length > 3 ? F(st.W[3], 0f) : 0f),
                    Late = st => { if (st.W.Length <= 3) StoryWalk.Stop(); },
                    Done = (p, st) => StoryWalk.On ? "still walking, " + F(StoryWalk.Left(p)) + " m left" : StoryWalk.Outcome } },
            } };
        }

        private static void PlayerLook(Player p, Step st) { float s; string no = StoryLook.At(st.W[2], st.W.Length > 3 && float.TryParse(st.W[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s) ? s : 0f); if (no != null) Note(no); } // («player look <place> <s>»: that slow a turn — 2–3 s, a look round a room
        private static void PlayerGait(Player p, Step st) { string no = StoryGait.Set(p, st.W[2]); if (no != null) Note(no); }
    }
}
#endif

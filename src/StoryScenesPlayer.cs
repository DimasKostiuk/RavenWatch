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
        private const string PlayerUsage = "player face <місце|npc.x|bird> · player emote <жест>|stop · player go <місце> <м> [front] [before] [run] [keep] [soft] [nowait] · player back <м> · player stop [с] · player look <місце|bird|npc.x>|forward · player gait <хода>|none · player pace walk|jog|run · player arrive [місце] [м] · player clip <кліп>|none [nowait] · player mark <мітка> [с]";

        // a walk as long as the walk itself may take, not the general StepMax (a long «player go» stopped halfway at 30 s)
        private static void WalkClock() { if (StoryWalk.On) _limit = StoryWalk.Until + 2f; }

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
                { "gait", new StepVerb(StepSeen.Own) { Min = 1, Begin = PlayerGait, Setup = PlayerGait, Busy = (p, st) => false } },
                { "pace", new StepVerb(StepSeen.Own) { Min = 1, Begin = (p, st) => StoryWalk.Pace(st.W[2]), Busy = (p, st) => false } },
                { "stop", new StepVerb(StepSeen.Own) { Begin = (p, st) => StoryWalk.Halt(st.W.Length > 2 ? F(st.W[2], 0.8f) : 0.8f), Busy = (p, st) => StoryWalk.Halting, Done = (p, st) => StoryWalk.Outcome } },
                { "emote", new StepVerb(StepSeen.Own) { Min = 1,
                    Begin = (p, st) =>
                    {
                        if (st.W[2] == "stop") { Gestures.StopEmote(p); return; }
                        EmoteTalk.OursUntil = Time.time + 1f; // (the scene's gesture: the ravens do not talk about it)
                        p.StartEmote(st.W[2], Array.IndexOf(st.W, "loop") < 0);
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

        private static void PlayerLook(Player p, Step st) { string no = StoryLook.At(st.W[2]); if (no != null) Note(no); }
        private static void PlayerGait(Player p, Step st) { string no = StoryGait.Set(p, st.W[2]); if (no != null) Note(no); }
    }
}
#endif

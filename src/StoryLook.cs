#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Where the player's head looks in a scene (the user 2026-11-04) and in a talk with a person (the user 2026-11-09: its eyes, while the
    // talk window is open: Talk/TalkEnd from NpcTalk, ticked by it each frame; a scene closes a talk, and its own word wins). Left alone, the game turns it after his look: the turn
    // of his body and the mouse's pitch as it was when the scene took him, so with the camera elsewhere it stared off to one side.
    // While a scene plays the head looks at what it said (scene step «player look <place|bird|npc.x|npc.x.thing>», «player face» too), or,
    // while a cutscene holds him (a shot, the bars, the black: StoryCamera.TakesPlayer) or the author watches from outside (StoryScenes.Watching: the mouse is his, not the player's),
    // straight before him. With no shot and no word he keeps his own head: he still has the keys.
    // Measured, not trusted: on the author's machine the log says, as a scene first takes the head and whenever the game turns it more
    // than OffSaid off the aim (looked at every LogEvery s), where the game turns it, how far that is from the aim, and the weights; «raven story look» says the same at once.
    // A clip that moves the head («@Look», Head, Neck) goes over all of this (HeadLookPatch).
    // Done with the game's own CharacterAnimEvent.m_lookAt, which nothing else in the game writes: the game turns the head after
    // it softly and sends it to the other players itself. The point eases to a new aim, so no turn starts with a jerk, and stands still once
    // there (Settle): the game sends it again each time it moves (its ZDO every 0.2 s), so a point that only crept on was sent for ever;
    // a person's eyes are followed only when they moved more than FollowStep (its head bobs as it speaks: NpcSpeech).
    internal static class StoryLook
    {
        internal static float Ease = 0.35f, Far = 10f; // seconds to turn to a new aim · m off, the point the game is given
        internal static float Settle = 0.4f, FollowStep = 0.15f; // degrees off the aim: there, it stands · m a person's eyes move before the head follows

        private static Func<Vector3> _aim, _talk; // (a scene's word · a talk's person)
        private static float _ease = Ease;      // (this aim's turn: «player look <place> <s>» — a slow look round a room, the camera in his eyes)
        // where his head looks now, as it turns (a shot through his eyes: «camera eyes» — the picture turns as his head does)
        internal static Vector3 Dir(Player p) { return _on != null ? _dir : p.GetLookDir(); }

        // a talk opened with this person (NpcTalk.Open): the head on its eyes till TalkEnd
        internal static void Talk(Character who)
        {
            _ease = Ease; // (a talk turns the head at the usual pace, whatever slow look a scene left)
            if (who == null) { _talk = null; return; }
            Vector3 last = who.GetEyePoint();
            _talk = () => { if (who != null) { Vector3 now = who.GetEyePoint(); if ((now - last).sqrMagnitude > FollowStep * FollowStep) last = now; } return last; };
        }
        internal static void TalkEnd() { if (_talk == null) return; _talk = null; if (_aim == null) Free(); }
        private static CharacterAnimEvent _on;
        private static Transform _point;
        private static Vector3 _dir;

        // a note when there is no such place, else null. forward: straight before him
        internal static string At(string what, float ease = 0f)
        {
            _ease = ease > 0f ? ease : Ease;
            if (what == "forward") { _aim = null; return null; }
            Vector3 at;
            Character body = StoryWhere.BodyOf(what); // (a creature of the scene: its eyes, as it moves)
            if (body != null) { Vector3 seen = body.GetEyePoint(); _aim = () => { if (body != null) seen = body.GetEyePoint(); return seen; }; return null; }
            bool thing = StoryWhere.IsLow(what); // («npc.<who>.thing», «player.thing»: what that one holds — followed wherever it goes, in a hand, falling, lying; «kept.<name>»: a thing lying low)
            if (what == "bird" || what.StartsWith("npc.") || thing)
            {
                if (!StoryWhere.Find(what, out at)) return "player look: нема «" + what + "»";
                float up = what == "bird" || thing ? 0f : 1.5f; // (a person's place is at the feet; a thing's word is the thing itself)
                Vector3 last = at + Vector3.up * up;
                _aim = () => { Vector3 now; if (StoryWhere.Find(what, out now)) last = now + Vector3.up * up; return last; }; // (it moves)
                return null;
            }
            if (!StoryWhere.Top(what, out at)) return "player look: нема місця «" + what + "»";
            _aim = () => at;
            return null;
        }

        // every frame of a scene (StoryScenes.Tick) and of an open talk (NpcTalk.Tick: a scene closes a talk, so never both)
        internal static void Tick(Player p)
        {
            Func<Vector3> aim = _aim ?? _talk;
            if (aim == null && !StoryCamera.TakesPlayer && !StoryScenes.Watching) { Free(); return; }
            if (_on == null)
            {
                _on = p.GetComponentInChildren<CharacterAnimEvent>(); // heavy: once a scene, when it first takes the head
                if (_on == null) return;
                if (_point == null) _point = new GameObject("RavenWatch_look").transform;
                _dir = p.GetLookDir();
                _on.m_lookAt = _point;
            }
            Vector3 head = Body.Head(p);
            Vector3 want = aim != null ? aim() - head : p.transform.forward;
            if (want.sqrMagnitude < 0.01f) want = p.transform.forward;
            want.Normalize();
            _dir = Vector3.Angle(_dir, want) < Settle ? want : Vector3.Slerp(_dir, want, 1f - Mathf.Exp(-Time.deltaTime / _ease)); // (there: it stands, not creeping on)
            Vector3 at = head + _dir * Far;
            if ((at - _point.position).sqrMagnitude > 1e-4f) _point.position = at; // (the head bobs a breath: the point stays where it is)
            if (Blueprint.On && Time.time >= _logAt) // (said when it goes wrong, not every LogEvery s: the log is read)
            {
                _logAt = Time.time + LogEvery;
                Vector3 game = FHeadDir != null ? (Vector3)FHeadDir.GetValue(_on) : _dir;
                bool off = Vector3.Angle(game, _dir) > OffSaid;
                if (off || !_told) { _told = true; Story.Log(Describe(p)); }
            }
        }

        internal static float LogEvery = 2f, OffSaid = 15f; // s · deg the game turns the head off our aim before it is said
        private static float _logAt;
        private static bool _told; // (once a scene: where it starts)
        private static readonly FieldInfo FHeadDir = AccessTools.Field(typeof(CharacterAnimEvent), "m_headLookDir"), FWeight = AccessTools.Field(typeof(CharacterAnimEvent), "m_lookAtWeight");

        // the head as the game has it now: the numbers, one line
        internal static string Describe(Player p)
        {
            if (p == null) return "head: no player";
            string aim = _aim != null ? "aim" : _talk != null ? "talk" : StoryCamera.TakesPlayer || StoryScenes.Watching ? "forward" : "his own";
            if (_on == null) return "head: " + aim + ", not taken (camera " + StoryCamera.Active + ", watching " + StoryScenes.Watching + ")";
            Vector3 game = FHeadDir != null ? (Vector3)FHeadDir.GetValue(_on) : Vector3.zero;
            BodyAnimator a = BodyAnimator.Of(p);
            return "head: " + aim + ", the game turns it " + Mathf.RoundToInt(Vector3.Angle(game, _dir)) + " deg off our aim, " + Mathf.RoundToInt(Vector3.SignedAngle(p.transform.forward, game, Vector3.up)) + " deg from his body; weights: look " + _on.m_lookWeight.ToString("0.00") + ", at " + (FWeight != null ? ((float)FWeight.GetValue(_on)).ToString("0.00") : "?") + ", head " + _on.m_headLookWeight.ToString("0.00") + ", clamp " + _on.m_lookClamp.ToString("0.00") + "; a clip holds it " + (a != null ? a.HeadFree.ToString("0.00") : "-");
        }

        // the scene is over, however: the head his own again
        internal static void End() { _aim = null; _told = false; _ease = Ease; if (_talk == null) Free(); } // (a scene's slow look is its own: the next talk turns the head at the usual pace)

        private static void Free()
        {
            if (_on != null) _on.m_lookAt = null;
            if (_point != null) UnityEngine.Object.Destroy(_point.gameObject);
            _on = null; _point = null;
        }
    }
}
#endif

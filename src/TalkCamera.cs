#if RAVEN_ITEM && RAVEN_NPC && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // The camera of a talk (the user 2026-11-05): when you talk to a person the camera comes in over your right shoulder — you on the left,
    // half behind the scroll (RavenBook.ShowTalk), the person on the right third, a little closer (a narrower view). The same for settlers
    // and the story's people. Driven by StoryCamera (its shot is a pose asked each frame), so a scene's camera is never fought: a talk
    // opened while a shot is on takes none, and a scene starting during a talk takes the camera from it (End then leaves it alone).
    //   Begin(a)   at a talk's opening (NpcTalk.Open): you turn to it on your feet, softly (StoryWalk.Face), the camera eases in (In s)
    //   End()      at its close: back to the game's camera (Out s) — only if the camera is still the talk's
    // Walls and roofs: the camera kept clear of them by the game's own rule (a sphere from your head back to it); too tight a room for a
    // shot over the shoulder (Tight m): no shot, the game's camera stays. On only where its switch is (config «Talk camera»).
    // Nothing it follows is taken rigidly: your head, its head and the place the walls leave are each eased after (Follow), so a turn, a breath
    // or a wall coming between never jolts the picture; a wall pulling it in is followed faster (Pull), or the lens would pass through it.
    internal static class TalkCamera
    {
        internal static bool On = true;
        internal static float In = 1.4f, Out = 1f, Fov = 52f, Back = 1.05f, BackPerM = 0.12f, Side = 0.6f, Rise = 0.18f, Turn = 13f, Follow = 5f, Pull = 14f, Tight = 0.55f;
        private static NpcActor _with;
        private static Vector3 _look, _head, _at;
        private static bool _fresh; // (the first frame of a shot: nothing to ease from)
        private static bool _turning; // (your turn to it is ours: a talk closed before it ends stops it)
        private static StoryCamera.Pose _last;
        private static System.Func<StoryCamera.Pose> _pose;
        private static bool _gave; // (the camera easing back now is the one a talk gave back)

        internal static void Begin(NpcActor a)
        {
            Player p = Player.m_localPlayer;
            bool ours = _gave && StoryCamera.Leaving;
            if (!On || a == null || p == null || StoryScreen.Held || (StoryCamera.Active && !ours)) return; // (a scene's camera, the bars: never fought)
            Character c = a.GetComponent<Character>();
            if (c == null) return;
            Vector3 d = c.GetEyePoint() - p.GetEyePoint(); d.y = 0f;
            if (d.sqrMagnitude < 0.04f) return;
            StoryWalk.Face(p, d); _turning = true; // (you turn to it)
            _with = a; _look = c.GetEyePoint(); _head = p.GetEyePoint(); _fresh = true;
            if (!Frame(out _last)) { _with = null; return; } // (too tight a room: the game's camera stays)
            _pose = Pose; _gave = false;
            StoryCamera.Shot(_pose, In); // (from wherever the camera is: the game's, or a talk's on its way back)
        }

        internal static void End()
        {
            if (_turning && StoryScenes.Playing.Length == 0) StoryWalk.Stop(); // (a scene's own walk is the scene's)
            _turning = false;
            if (_pose == null) return; // (no shot of a talk now — NpcTalk.Open closes what was before it opens: what the last End gave back stays known, Begin takes it on from there)
            _gave = StoryCamera.Driving(_pose);
            if (_gave) StoryCamera.Off(Out); // (a scene took it meanwhile: not ours to give back)
            _with = null; _pose = null;
        }

        // each frame: where it stands and looks — its target eased after the person's head (no jolt when it shifts its weight)
        private static StoryCamera.Pose Pose()
        {
            StoryCamera.Pose now;
            if (_with != null && Frame(out now)) _last = now;
            return _last;
        }

        private static bool Frame(out StoryCamera.Pose pose)
        {
            pose = _last;
            Player p = Player.m_localPlayer;
            Character c = _with != null ? _with.GetComponent<Character>() : null;
            GameCamera gc = GameCamera.instance;
            if (p == null || c == null || gc == null) return false;
            float dt = Time.unscaledDeltaTime, ease = 1f - Mathf.Exp(-Follow * dt);
            _look = Vector3.Lerp(_look, c.GetEyePoint(), ease);
            _head = Vector3.Lerp(_head, p.GetEyePoint(), ease);
            Vector3 head = _head, d = _look - head; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.2f) return false;
            d /= dist;
            Vector3 right = Vector3.Cross(Vector3.up, d).normalized;
            Vector3 want = head - d * (Back + BackPerM * dist) + right * Side + Vector3.up * Rise;
            Vector3 at = want, way = want - head;
            bool walled = false;
            RaycastHit hit;
            if (Physics.SphereCast(head, gc.m_raycastWidth, way.normalized, out hit, way.magnitude, gc.m_blockCameraMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < Tight) return false;
                at = head + way.normalized * (hit.distance - 0.1f);
                walled = true;
            }
            _at = _fresh ? at : Vector3.Lerp(_at, at, walled ? 1f - Mathf.Exp(-Pull * dt) : ease);
            _fresh = false;
            at = _at;
            Vector3 toLook = _look - at;
            Quaternion rot = Quaternion.AngleAxis(-Turn, Vector3.up) * Quaternion.LookRotation(toLook.normalized, Vector3.up); // (turned a little left: the person sits on the right third)
            pose = new StoryCamera.Pose { At = at, Rot = rot, Fov = Fov };
            return true;
        }
    }
}
#endif

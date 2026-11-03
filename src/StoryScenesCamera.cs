#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene director (StoryScenes), its shots: from where, at what (StoryCamera does the filming).
    internal static partial class StoryScenes
    {
        // camera <from> [look <what>] [blend N | cut] [fov N] · camera off [blend N | cut]
        //   behind — over the player's shoulder towards what it looks at (both in the picture; the angle with nothing in
        //            between picked from a few, picked again as they move); over — high above; <place> — from there
        //   look   — player (default), sky, bird, npc.<who>, or a place; whatever moves is followed every frame
        // The author's shots (StoryShots, the window «Режисер»), each «<place>.<name>»:
        //   camera shot <a> [look <what>]                         stands there, as it was taken (or looking at something)
        //   camera move <a> <b> <s> [ease none|in|out|both] [look <what>]   from one to the other in s seconds
        //   camera orbit <what> <radius> <height> <degrees> <s> [ease …]   round it, from where the camera is now
        //   cut — at once (= blend 0); blend N — eased in from wherever the camera was, a moving one too (default 1.5)
        //   wait — the scene waits till the move or the orbit is over (else it goes on while the camera moves)
        private static void CameraStep(Player p, string[] w)
        {
            float blend = 1.5f, fov = 0f;
            int bi = Array.IndexOf(w, "blend"), fi = Array.IndexOf(w, "fov"), li = Array.IndexOf(w, "look"), ei = Array.IndexOf(w, "ease");
            if (bi >= 0 && w.Length > bi + 1) blend = F(w[bi + 1], blend);
            if (Array.IndexOf(w, "cut") > 0) blend = 0f;
            if (fi >= 0 && w.Length > fi + 1) fov = F(w[fi + 1], 0f);
            string ease = ei >= 0 && w.Length > ei + 1 ? w[ei + 1] : "both";
            bool hold = Array.IndexOf(w, "wait") > 0;
            if (w[1] == "off") { StoryCamera.Off(blend); return; }
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            string what = li >= 0 && w.Length > li + 1 ? w[li + 1] : "player";
            Func<Vector3> look = Looker(p, what, fwd);
            if (w[1] == "shot" || w[1] == "move")
            {
                StoryCamera.Pose a, b;
                if (w.Length < 3 || !StoryShots.Find(w[2], out a)) { Note("camera " + w[1] + ": нема кадру «" + (w.Length > 2 ? w[2] : "") + "» — крок пропущено"); return; }
                if (fov > 1f) a.Fov = fov;
                Func<Vector3> aim = li >= 0 ? look : null; // (no «look»: the shot's own turn)
                if (w[1] == "shot") { StoryCamera.Shot(StoryShots.Still(a, aim), blend); return; }
                if (w.Length < 5 || !StoryShots.Find(w[3], out b)) { Note("camera move: нема кадру «" + (w.Length > 3 ? w[3] : "") + "» — крок пропущено"); return; }
                float secs = F(w[4], 3f);
                if (fov > 1f) b.Fov = fov;
                StoryCamera.Shot(StoryShots.Move(a, b, secs, ease, aim), blend);
                if (hold) { Hold(secs); _limit = Time.time + secs + 2f; }
                return;
            }
            if (w[1] == "orbit")
            {
                if (w.Length < 7) { Note("camera orbit <що> <радіус> <висота> <градусів> <секунд> — мало слів"); return; }
                Func<Vector3> centre = Looker(p, w[2], fwd);
                float secs = F(w[6], 6f);
                StoryCamera.Shot(StoryShots.Orbit(centre, F(w[3], 4f), F(w[4], 2f), F(w[5], 90f), secs, ease, fov), blend);
                if (hold) { Hold(secs); _limit = Time.time + secs + 2f; }
                return;
            }
            Func<Vector3> from;
            if (w[1] == "behind") from = what == "player" ? Fixed(p.transform.position - fwd * 6f + Vector3.up * 3f) : OverShoulder(look);
            else if (w[1] == "over") from = Fixed(p.transform.position - fwd * 3f + Vector3.up * 14f);
            else
            {
                Vector3 start;
                if (!StoryWhere.Find(w[1], out start)) { Note("camera: нема місця «" + w[1] + "» — крок пропущено"); return; }
                from = Fixed(start + Vector3.up * 1.6f); // (a place is a spot on the ground: the camera at eye height over it)
            }
            StoryCamera.Shot(from, look, blend, fov);
        }

        // the author's window (F9 «Камера»): a camera line played now, no scene round it
        internal static void PreviewCamera(string line)
        {
            Player p = Player.m_localPlayer;
            List<string> l = new List<string>(line.Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries));
            l.Remove("wait"); // (no scene round it: a playing scene's step clock is not this preview's)
            string[] w = l.ToArray();
            if (p == null || w.Length < 2 || w[0] != "camera") return;
            CameraStep(p, w);
        }

        private static Func<Vector3> Fixed(Vector3 v) { return () => v; }

        // what the camera looks at, followed each frame: the player, the sky before you, the bird, a person, a place
        private static Func<Vector3> Looker(Player p, string what, Vector3 fwd)
        {
            if (what == "player") return () => Player.m_localPlayer != null ? Player.m_localPlayer.GetHeadPoint() : Vector3.zero;
            if (what == "sky") { Vector3 sky = p.GetHeadPoint() + fwd * 30f + Vector3.up * 22f; return () => sky; } // (up before you: the storm, Thor)
            Vector3 last = p.GetHeadPoint();
            Vector3 first;
            if (!StoryWhere.Find(what, out first)) Note("camera look: нема місця «" + what + "» — дивлюсь на гравця");
            else last = first + Vector3.up * 1.2f;
            return () => { Vector3 at; if (StoryWhere.Find(what, out at)) last = at + Vector3.up * 1.2f; return last; }; // (it moves: where it is now)
        }

        // Over the player's shoulder towards a target: a few angles tried (behind it a little to either side, then wider),
        // the first from which both heads are seen with nothing between (the game's own camera-blocking layers) and that
        // is not inside anything; tried again twice a second, the camera easing there — never a rock across the picture
        internal static float ShoulderBack = 3f, ShoulderUp = 1.1f;
        private static readonly float[] Angles = { 25f, -25f, 45f, -45f, 70f, -70f, 0f, 110f, -110f };
        private static Func<Vector3> OverShoulder(Func<Vector3> look)
        {
            Vector3 at = Vector3.zero, want = Vector3.zero; float next = 0f; bool first = true;
            return () =>
            {
                Player p = Player.m_localPlayer;
                if (p == null) return at;
                if (first || Time.time >= next)
                {
                    next = Time.time + 0.5f;
                    want = ShoulderSpot(p.GetHeadPoint(), look());
                    if (first) { at = want; first = false; }
                }
                at = Vector3.Lerp(at, want, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                return at;
            };
        }

        private static Vector3 ShoulderSpot(Vector3 head, Vector3 target)
        {
            Vector3 dir = target - head; dir.y = 0f;
            float dist = dir.magnitude;
            dir = dist > 0.01f ? dir / dist : Vector3.forward;
            float back = Mathf.Clamp(ShoulderBack + dist * 0.12f, ShoulderBack, 6f); // (farther apart: a step farther back, both still in it)
            int mask = GameCamera.instance != null ? (int)GameCamera.instance.m_blockCameraMask : (int)RavenMotor.Mask;
            foreach (float ang in Angles)
            {
                Vector3 c = head + Quaternion.Euler(0f, ang, 0f) * -dir * back + Vector3.up * ShoulderUp;
                if (Physics.CheckSphere(c, 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;                   // (inside a rock, a wall)
                if (Physics.Linecast(c, head, mask, QueryTriggerInteraction.Ignore)) continue;                      // (the player hidden)
                if (Physics.Linecast(c, target, mask, QueryTriggerInteraction.Ignore)) continue;                    // (what it looks at hidden)
                return c;
            }
            // none clear (under a roof, in a shed): higher and a step back, what it looks at seen even if the player is not
            foreach (float ang in Angles)
            {
                Vector3 c = head + Quaternion.Euler(0f, ang, 0f) * -dir * back * 1.3f + Vector3.up * (ShoulderUp + 2.5f);
                if (Physics.CheckSphere(c, 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(c, target, mask, QueryTriggerInteraction.Ignore)) continue;
                return c;
            }
            // still none: close behind the head, never inside a wall (a short step back from what blocks it)
            Vector3 aim = head - dir * back + Vector3.up * ShoulderUp;
            RaycastHit hit;
            if (Physics.SphereCast(head, 0.3f, (aim - head).normalized, out hit, Vector3.Distance(head, aim), mask, QueryTriggerInteraction.Ignore)) return head + (aim - head).normalized * Mathf.Max(0.4f, hit.distance - 0.2f);
            return aim;
        }
    }
}
#endif

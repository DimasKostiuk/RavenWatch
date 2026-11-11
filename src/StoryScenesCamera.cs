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
        //            between picked from a few, picked again as they move); over — high above; <place> — from there;
        //            face — close before his face, a little to the side, where his body faces when it begins (it stays
        //            there: his head turning is seen), looking at his head (default fov FaceFov)
        //   look   — player (default), sky, bird, npc.<who>, or a place; whatever moves is followed every frame
        // The author's shots (StoryShots, the window «Режисер»), each «<place>.<name>»:
        //   camera shot <a> [look <what>]                         stands there, as it was taken (or looking at something)
        //   camera move <a> <b> <s> [ease none|in|out|both] [look <what>]   from one to the other in s seconds
        //   camera orbit <what> <radius> <height> <degrees> <s> [ease …]   round it, from where the camera is now
        //   camera follow [side] [up] [back]                      goes with him: behind his shoulder (m from his head), lagging a
        //                                                         little, looking the way he faces (his nape in the picture)
        //   camera face [degrees] [m]                             round his face: at that angle from where he faces, that far
        //   live N — a living camera on any of these (StoryCamera.Live): sways in step with him, breathes when he stands
        //   cut — at once (= blend 0); blend N — eased in from wherever the camera was, a moving one too (default 1.5)
        //   wait — the scene waits till the move or the orbit is over (else it goes on while the camera moves)
        // Shots told in words, no coordinates (who: player, bird, npc.<who>, a place — a place's front is its side towards the player):
        //   camera on <who> [close|medium|full|wide] [front|left|right|back|<degrees>] [low|high] [track] [look <what>]
        //                                                         one in the picture: how near (medium; full and wide aim at the middle of its
        //                                                         body), at which angle from where its body faces (left, right: its own; none: the
        //                                                         first clear, as face), the lens lower or higher. It stays where it was put (its
        //                                                         head turning is seen); track: it goes with it as it walks. look: at something else
        //   camera shoulder <a> <b> [left|right]                  over a's shoulder at b, both in the picture («behind look X»: a is the player)
        //   camera two <a> <b> [left|right]                       both from the side: square to the line between them, at their heads' height
        //                                                         (farther apart than TwoFar m: made all the same, and a note says so — it is a shot of a talk close by)
        //   who may be a thing someone holds too: npc.<who>.thing, player.thing (StoryWhere) — the shot aims at the thing itself, wherever it is
        //                                                         (on the arm, falling, lying, in flight); to film a fall, the shot's line goes before «prop drop»
        //   the line — the scene's first shoulder or two of a pair fixes the side of the line a–b the camera works from (left|right of the
        //              way a looks at b, else the first clear); every later one of the same two, «shoulder b a» too, stays on that side of
        //              the world unless it says left|right itself (the 180° rule); forgotten with the scene (Release)
        //   push <m> <s> — on these three: over s seconds the lens goes m nearer to what it looks at (below zero: away); wait — the step waits s
        //   Each of the three tries its spots once as the shot begins: not inside anything, nothing between the lens and the heads, and
        //   what the shot looks at seen by the very cast the frame makes (StoryCamera.Sees: picked as it is filmed). None clear: an angle
        //   beside it, then nearer, at last another side or from above — and the log says which. on: «look bird|npc.x» aims at its real
        //   head; medium aims a little under the head (OnMediumDown), two a little under the middle (TwoDown)
        //   Read at load (CameraCheck): an unknown word, a word without its value, the angle twice, one named twice, wait with no push — an error
        private static void CameraStep(Player p, string[] w)
        {
            if (Watching) return; // (the author looks from outside: the camera is his)
            float blend = 1.5f, fov = 0f;
            int bi = Array.IndexOf(w, "blend"), fi = Array.IndexOf(w, "fov"), li = Array.IndexOf(w, "look"), ei = Array.IndexOf(w, "ease");
            if (bi >= 0 && w.Length > bi + 1) blend = F(w[bi + 1], blend);
            if (Array.IndexOf(w, "cut") > 0) blend = 0f;
            if (fi >= 0 && w.Length > fi + 1) fov = F(w[fi + 1], 0f);
            string ease = ei >= 0 && w.Length > ei + 1 ? w[ei + 1] : "both";
            bool hold = Array.IndexOf(w, "wait") > 0;
            if (w[1] == "off") { StoryCamera.Off(blend); return; }
            int vi = Array.IndexOf(w, "live");
            StoryCamera.Live = vi >= 0 && w.Length > vi + 1 ? F(w[vi + 1], 1f) : 0f;
            if (w[1] == "follow")
            {
                StoryCamera.Shot(Follow(Num(w, 2, FollowSide), Num(w, 3, FollowUp), Num(w, 4, FollowBack), fov), blend);
                return;
            }
            Vector3 fwd = Flat(GameCamera.instance != null ? GameCamera.instance.transform.forward : p.transform.forward);
            string what = li >= 0 && w.Length > li + 1 ? w[li + 1] : "player";
            Func<Vector3> look = Looker(p, what, fwd);
            if (w[1] == "on" || w[1] == "shoulder" || w[1] == "two")
            {
                Framing f = ReadFraming(w);
                Subject a, b = null;
                if (!Who(p, f.A, out a) || (w[1] != "on" && !Who(p, f.B, out b))) { Note("camera " + w[1] + ": нема «" + (a == null ? f.A : f.B) + "» — крок пропущено"); return; }
                Func<Vector3> spot, aim;
                if (w[1] == "on")
                {
                    string size = f.Size;
                    Subject at = null;
                    if (li >= 0) aim = what != "sky" && Who(p, what, out at) ? at.Head : look; // (its real head, as «shoulder» aims at one; the sky, and a word not found: as «look» has them)
                    else if (!a.Tall || size == "close") aim = a.Head;
                    else aim = () => a.Head() - Vector3.up * (size == "medium" ? OnMediumDown : OnBodyDown); // (a little under the head: less air over it; full, wide — the middle of the body, all of it in)
                    spot = On(a, f, aim);
                    if (fov < 1f) fov = size == "close" ? FaceFov : size == "full" ? OnFullFov : size == "wide" ? OnWideFov : OnMediumFov;
                }
                else if (w[1] == "shoulder") { spot = OverShoulder(a.Head, b.Head, f.A, f.B, f.Side); aim = b.Head; }
                else
                {
                    bool tall = a.Tall || b.Tall;
                    float apart = Vector3.Distance(a.Head(), b.Head());
                    if (apart > TwoFar) Note("camera two: між ними " + apart.ToString("0", CultureInfo.InvariantCulture) + " м — кадр двох для розмови зблизька; зведи їх, або camera on <хто> wide"); // (the shot is still made: at 12 m it was a landscape with two specks, 2026-11-11)
                    aim = () => (a.Head() + b.Head()) * 0.5f - Vector3.up * (tall ? TwoDown : 0f); // (the middle between their heads, a little under it, wherever they go)
                    spot = Fixed(TwoSpot(a.Head(), b.Head(), aim(), f.A, f.B, f.Side));
                    if (fov < 1f) fov = TwoFov;
                }
                StoryCamera.Shot(Push(spot, aim, f.PushM, f.PushS), aim, blend, fov);
                if (hold && f.PushM != 0f) { Hold(f.PushS); _limit = Time.time + f.PushS + 2f; }
                return;
            }
            if (w[1] == "shot" || w[1] == "move")
            {
                StoryCamera.Pose a, b;
                if (w.Length < 3 || !StoryShots.Find(w[2], out a)) { Note("camera " + w[1] + ": нема кадру «" + (w.Length > 2 ? w[2] : "") + "» — крок пропущено"); return; }
                if (fov > 1f) a.Fov = fov;
                Func<Vector3> aim = li >= 0 ? look : null; // (no «look»: the shot's own turn)
                bool round = w[2].StartsWith(StoryShots.PlayerFrame + "."); // (a shot round the player: the land there is not known)
                if (w[1] == "shot") { StoryCamera.Shot(round ? Clearing(StoryShots.Still(a, aim)) : StoryShots.Still(a, aim), blend); return; }
                if (w.Length < 5 || !StoryShots.Find(w[3], out b)) { Note("camera move: нема кадру «" + (w.Length > 3 ? w[3] : "") + "» — крок пропущено"); return; }
                float secs = F(w[4], 3f);
                if (fov > 1f) b.Fov = fov;
                StoryCamera.Shot(round ? Clearing(StoryShots.Move(a, b, secs, ease, aim)) : StoryShots.Move(a, b, secs, ease, aim), blend);
                if (hold) { Hold(secs); _limit = Time.time + secs + 2f; }
                return;
            }
            if (w[1] == "orbit")
            {
                if (w.Length < 7) { Note("camera orbit <що> <радіус> <висота> <градусів> <секунд> — мало слів"); return; }
                Func<Vector3> centre = Looker(p, w[2], fwd);
                float secs = F(w[6], 6f);
                StoryCamera.Shot(Clearing(StoryShots.Orbit(centre, F(w[3], 4f), F(w[4], 2f), F(w[5], 90f), secs, ease, fov)), blend); // (it goes round: never through a rock)
                if (hold) { Hold(secs); _limit = Time.time + secs + 2f; }
                return;
            }
            Func<Vector3> from;
            if (w[1] == "behind") from = what == "player" ? Fixed(p.transform.position - fwd * 6f + Vector3.up * 3f) : OverShoulder(Looker(p, "player", fwd), look, null, null, 0); // (the player's shoulder, no line kept: as ever)
            else if (w[1] == "over") from = Fixed(p.transform.position - fwd * 3f + Vector3.up * 14f);
            else if (w[1] == "face")
            {
                float want = Num(w, 2, float.NaN);
                Vector3 before;
                if (!RoundSpot(p.GetHeadPoint(), Flat(p.transform.forward), float.IsNaN(want) ? FaceAngles : First(want, FaceAngles), Num(w, 3, FaceDist), FaceUp, null, out before)) Note("camera face: довкола голови тісно — кадр згори"); // (the angle asked for first)
                from = Fixed(before);
                if (fov < 1f) fov = FaceFov;
            }
            else
            {
                Vector3 start;
                if (!StoryWhere.Find(w[1], out start)) { Note("camera: нема місця «" + w[1] + "» — крок пропущено"); return; }
                from = Fixed(start + Vector3.up * 1.6f); // (a place is a spot on the ground: the camera at eye height over it)
            }
            StoryCamera.Shot(from, look, blend, fov);
        }

        // the author's window (F9 «Камера»): a camera line played now, no scene round it
        private static bool _preview; // (a shot of the author's window, not the scene's step: it reads the scene's line and never writes it — LineTook)
        internal static void PreviewCamera(string line)
        {
            Player p = Player.m_localPlayer;
            List<string> l = new List<string>(line.Split(new[] { ' ', '	' }, StringSplitOptions.RemoveEmptyEntries));
            l.Remove("wait"); // (no scene round it: a playing scene's step clock is not this preview's)
            string[] w = l.ToArray();
            if (p == null || w.Length < 2 || w[0] != "camera") return;
            _preview = true;
            try { CameraStep(p, w); }
            finally { _preview = false; }
        }

        private static Func<Vector3> Fixed(Vector3 v) { return () => v; }

        // a camera that keeps nothing between it and his head, each frame (nearer when something stands there)
        private static Func<StoryCamera.Pose> Clearing(Func<StoryCamera.Pose> pose)
        {
            return () => { StoryCamera.Pose q = pose(); q.At = StoryShots.ClearOf(q.At); return q; }; // heavy: one ray a frame, only while this shot is on
        }

        // a shot's own number: the i-th word of the line, only if every word from the shot's name up to it is a number (so
        // «live 1» further on is never read as one); else the default
        private static float Num(string[] w, int i, float def)
        {
            float v = def;
            for (int k = 2; k <= i; k++) if (k >= w.Length || !float.TryParse(w[k], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return def;
            return v;
        }

        // going with him, behind his shoulder: lags a little (it has weight), looks the way he faces; nearer than whatever
        // stands between it and his head
        internal static float FollowSide = 0.32f, FollowUp = 0.08f, FollowBack = 0.95f, FollowEase = 5f, FollowAhead = 6f;
        private static Func<StoryCamera.Pose> Follow(float side, float up, float back, float fov)
        {
            Vector3 at = Vector3.zero; bool first = true;
            return () =>
            {
                Player p = Player.m_localPlayer;
                if (p == null) return new StoryCamera.Pose { At = at, Rot = Quaternion.identity, Fov = fov };
                Quaternion turn = Quaternion.Euler(0f, p.transform.eulerAngles.y, 0f);
                Vector3 head = p.GetHeadPoint(), want = head + turn * new Vector3(side, up, -back);
                at = first ? want : Vector3.Lerp(at, want, 1f - Mathf.Exp(-FollowEase * Time.deltaTime));
                first = false;
                Vector3 c = StoryShots.ClearOf(at), look = head + turn * Vector3.forward * FollowAhead - c; // heavy: one ray a frame, only while this shot is on
                return new StoryCamera.Pose { At = c, Rot = Quaternion.LookRotation(look.normalized, Vector3.up), Fov = fov };
            };
        }
        internal static float FaceDist = 1.15f, FaceUp = 0.05f, FaceFov = 38f; // (camera face, and «on … close»: m before the head, m up)
        internal static float FaceNearer = 0.7f, FaceOver = 1.2f, LensRoom = 0.25f; // (the nearer ring's share of the distance; no spot round the head: m over it; m clear round the lens)
        private static readonly float[] FaceAngles = { 28f, -28f, 60f, -60f, 95f, -95f, 130f, -130f, 180f };
        private static int LensMask { get { return GameCamera.instance != null ? (int)GameCamera.instance.m_blockCameraMask : (int)RavenMotor.Mask; } } // (what the game's own camera stops at)
        private static float[] First(float want, float[] rest) { float[] all = new float[rest.Length + 1]; all[0] = want; rest.CopyTo(all, 1); return all; }

        // a lens there: in no rock or wall, nothing between it and the head — and, «aim» given (on), left there by the frame's own cast from
        // what the shot really looks at (StoryCamera.Sees: the middle of a body behind a fence, a hearth round a corner — a head seen is
        // not enough, the frame would pull the lens up to what stands between)
        private static bool LensClear(Vector3 c, Vector3 head, Vector3? aim, int mask)
        {
            if (Physics.CheckSphere(c, LensRoom, mask, QueryTriggerInteraction.Ignore)) return false; // heavy: once as a shot begins (track: when it is picked again), a spot tried — the first clear one ends it (inside a rock, a wall)
            if (Physics.Linecast(c, head, mask, QueryTriggerInteraction.Ignore)) return false;        // heavy: the same spot, one line (the head hidden)
            return !aim.HasValue || StoryCamera.Sees(aim.Value, c);                                   // (the same spot, one cast: what it looks at hidden, or the lens under the land)
        }

        // Round a head («face», «on»): the first of these angles — degrees round it from where its body faces, 0 straight before it, below zero
        // its left — where the lens is clear (LensClear; «face» gives no aim: as it ever was); none: the same on a nearer ring. «up»: the
        // lens that far over the head (below zero under it, and then never nearer than OnGround to the ground under the lens). «low»: the
        // «head» is a thing that may lie on the ground (Subject.Low) — the lens is kept OnGround over the ground by it, or every spot round a
        // lying thing would be «inside the ground». False: no spot clear (it is put over the head then — the caller says so)
        internal static float LowOver = 1f; // (m over a spot by a low thing the ray to the ground under the lens begins)
        private static bool RoundSpot(Vector3 head, Vector3 body, float[] angles, float far, float up, Vector3? aim, out Vector3 at, bool low = false)
        {
            int mask = LensMask;
            foreach (float dist in new[] { far, far * FaceNearer })
                foreach (float ang in angles)
                {
                    Vector3 c = head + Quaternion.Euler(0f, ang, 0f) * body * dist + Vector3.up * up;
                    RaycastHit floor;
                    if (up < 0f && Physics.Raycast(new Vector3(c.x, head.y, c.z), Vector3.down, out floor, OnGround - up, mask, QueryTriggerInteraction.Ignore)) c.y = floor.point.y + OnGround; // heavy: a low lens only, one ray a spot tried (the ground under it: the lens stays over it)
                    if (low && Physics.Raycast(c + Vector3.up * LowOver, Vector3.down, out floor, LowOver + OnGround, mask, QueryTriggerInteraction.Ignore)) c.y = Mathf.Max(c.y, floor.point.y + OnGround); // heavy: a shot of a held thing only, one ray a spot tried (it lies or falls to the ground: the lens stays over the ground there)
                    if (!LensClear(c, head, aim, mask)) continue;
                    at = c;
                    return true;
                }
            at = head + Vector3.up * FaceOver;
            return false;
        }

        // ---------------------------------------------------------------- shots told in words: on, shoulder, two
        // One in a shot: where its head is and where its body faces — each asked every frame; Tall: it has a body to fit in the picture
        // (a bird and a place have none: a shot of them aims at the head). A body has its real head (the player's head point, a person's
        // eyes, the bird's neck bone); a place has none: PlaceUp m over its point, as «look» takes it, and its front is its side towards
        // the player. A thing someone holds («npc.<who>.thing», «player.thing») is its own middle — a basket 0.4 m tall would be out of the
        // picture 1.2 m under the aim — and may lie on the ground (Low: the lens stays over the ground by it). The one place that knows
        // this: no «+ up» beside it
        private sealed class Subject { public Func<Vector3> Head, Front; public bool Tall, Low; }
        internal static float PlaceUp = 1.2f, BirdUp = 0.12f; // (m over a place's point — what «look» aims at too; over the bird's feet when its neck bone is not found)
        private static float UpOf(string word) { return StoryWhere.IsThing(word) ? 0f : PlaceUp; } // (m over a word's point a shot or a look aims at: none over a thing)

        internal static float SkullUp = 0.08f; // m from a person's head bone up to its eyes (a first guess: tuned in game by a close shot)
        private static bool Who(Player p, string word, out Subject s)
        {
            s = null;
            Vector3 at;
            if (!StoryWhere.Find(word, out at)) return false; // (the one table of the story's words: what it does not find is nobody)
            Func<Vector3> head, front;
            Vector3 last = at + Vector3.up * PlaceUp, way = Vector3.forward;
            bool thing = StoryWhere.IsThing(word);
            Character c = word == "player" ? p : word.StartsWith("npc.") && !thing ? CharOf(word.Substring(4), p) : null;
            if (word == "bird")
            {
                StoryBird bird = _bird;
                Transform neck = RavenGear.Bone(bird.gameObject, LookDiag.HeadBone); // heavy: once as a shot of the bird begins (its bones gone through for the one named; kept for the shot)
                head = () => { if (bird != null) last = neck != null ? neck.position : bird.transform.position + Vector3.up * BirdUp; return last; };
                front = () => { if (bird != null) way = Flat(bird.transform.forward); return way; };
            }
            else if (c != null)
            {
                bool me = c == p;
                // (a person's head: its own bone — the game's eye point stands at standing height over the feet, and Heid's close shots were framed by the top of
                //  her hood, her face in the lower half, zz_heid_meet 2026-11-11; the player's GetHeadPoint is his bone already)
                Animator an = me ? null : c.GetComponentInChildren<Animator>(); // heavy: once as a shot of a person begins
                Transform skull = an != null ? BodyClips.SkeletonBone(c, an.transform, "Head") : null;
                head = () => { if (c != null) last = me ? c.GetHeadPoint() : skull != null ? skull.position + Vector3.up * SkullUp : Body.Eye(c); return last; }; // (gone in the middle of a shot: where it was)
                front = () => { if (c != null) way = Flat(c.transform.forward); return way; };
            }
            else
            {
                bool moves = StoryWhere.Moves(word);
                float up = UpOf(word);
                Vector3 point = at;
                head = () => { Vector3 now; if (moves && StoryWhere.Find(word, out now)) point = now; return point + Vector3.up * up; }; // (a place stands still: found once, not each frame; a thing held or in the air: where it is now — let go of for good, where it was)
                front = () => { Player you = Player.m_localPlayer; return you != null ? Flat(you.transform.position - point) : way; };
            }
            s = new Subject { Head = head, Front = front, Tall = c != null, Low = thing };
            return true;
        }

        // The words of «camera on|shoulder|two» after the shot's name, read in one place: the step, its load check, and the author's window
        // (it shows them on its buttons). Wrong: the first thing in the line that cannot be so, in the words the load check says («» — none):
        // a word none of them knows, a word left without its value, the angle said twice («on player 30 1.5», written as «face [°] [m]»),
        // one and the same named twice, «wait» with no push to wait for — each would pass in silence and film another shot
        internal sealed class Framing
        {
            public string A = "", B = "", Size = "medium", Tilt = "", Wrong = ""; // (on: A alone · close|medium|full|wide · low|high|«»)
            public float Angle = float.NaN; // (on: degrees round it from where its body faces; none asked — the first clear)
            public int Side;                // (shoulder, two: 1 — left of the way A looks at B, -1 — right, 0 — none asked)
            public bool Track;
            public float PushM, PushS = PushSecs;
        }
        internal static readonly string[] OnSizes = { "close", "medium", "full", "wide" }, OnAngles = { "front", "right", "back", "left" };
        internal static readonly float[] OnAngleDeg = { 0f, 90f, 180f, -90f }; // (OnAngles in degrees: its own right is +90, as «face» counts them)
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }

        internal static Framing ReadFraming(string[] w)
        {
            Framing f = new Framing();
            bool on = w.Length > 1 && w[1] == "on";
            if (w.Length > 2) f.A = w[2];
            if (!on && w.Length > 3) f.B = w[3];
            string wrong = null; // (the first one: what the load check says)
            bool waits = false;
            for (int i = on ? 3 : 4; i < w.Length; i++)
            {
                string x = w[i]; float v = 0f; int k = -1;
                if (x == "cut") continue;
                if (x == "wait") { waits = true; continue; }
                if (x == "blend" || x == "fov" || x == "live") // (a word with its number after it: CameraStep reads those)
                {
                    if (i + 1 >= w.Length || !TryF(w[i + 1], out v)) wrong = wrong ?? "після «" + x + "» має бути число";
                    i++;
                }
                else if (on && x == "look")
                {
                    if (i + 1 >= w.Length) wrong = wrong ?? "після «look» нема на що дивитись";
                    i++;
                }
                else if (x == "push") // (metres, then seconds; the seconds left out: PushSecs)
                {
                    if (i + 1 < w.Length && TryF(w[i + 1], out v)) { f.PushM = v; i++; if (i + 1 < w.Length && TryF(w[i + 1], out v)) { f.PushS = Mathf.Max(0.05f, v); i++; } }
                    else wrong = wrong ?? "після «push» мають бути метри (і секунди)";
                }
                else if (!on) { if (x == "left") f.Side = 1; else if (x == "right") f.Side = -1; else wrong = wrong ?? "невідоме слово «" + x + "»"; }
                else if (Array.IndexOf(OnSizes, x) >= 0) f.Size = x;
                else if ((k = Array.IndexOf(OnAngles, x)) >= 0 || TryF(x, out v))
                {
                    if (!float.IsNaN(f.Angle)) wrong = wrong ?? "кут сказано двічі («" + x + "»): він один — front|left|right|back чи градуси; як близько, кажуть close|medium|full|wide";
                    f.Angle = k >= 0 ? OnAngleDeg[k] : v;
                }
                else if (x == "low" || x == "high") f.Tilt = x;
                else if (x == "track") f.Track = true;
                else wrong = wrong ?? "невідоме слово «" + x + "»";
            }
            if (!on && f.A.Length > 0 && f.A == f.B) wrong = wrong ?? "«" + f.A + "» названо двічі: у кадрі двоє різних";
            if (waits && f.PushM == 0f) wrong = wrong ?? "«wait» без «push» нічого не чекає";
            f.Wrong = wrong ?? "";
            return f;
        }

        // load time: the three shots told in words — who they are of is a word of the story, and nothing in the line is wrong (Framing.Wrong:
        // a typo would pass in silence and the shot be another); the camera's other lines as ever
        private static string CameraCheck(Step st)
        {
            string[] w = st.W;
            if (w[1] != "on" && w[1] != "shoulder" && w[1] != "two") return null;
            bool on = w[1] == "on";
            if (w.Length < (on ? 3 : 4)) return on ? "camera on <хто> [close|medium|full|wide] [front|left|right|back|<градусів>] [low|high] [track] …" : "camera " + w[1] + " <хто> <на кого> [left|right] …";
            Framing f = ReadFraming(w);
            string why = StoryWhere.Why(f.A) ?? (on ? null : StoryWhere.Why(f.B));
            if (why != null) return "camera " + w[1] + ": " + why;
            return f.Wrong.Length > 0 ? "camera " + w[1] + ": " + f.Wrong : null;
        }

        // «camera on»: how far the lens is from the head and how wide it sees (close: FaceDist, FaceFov), m from the head down to the middle
        // of a body (full, wide aim there), the lens under or over the head (low, high), m a low lens keeps over the ground under it (more
        // than LensRoom: the lens needs that clear round it, or every low spot by a sitting one would count as «inside the ground»)
        internal static float OnMedium = 2.2f, OnMediumFov = 42f, OnFull = 4.2f, OnFullFov = 48f, OnWide = 8f, OnWideFov = 58f;
        internal static float OnBodyDown = 0.6f, OnLow = -0.9f, OnHigh = 1.6f, OnGround = 0.4f;
        internal static float OnMediumDown = 0.25f; // (medium: m under the head the shot aims at, so the head is not in the middle of the picture with air over it. Unmeasured: a first guess, to tune in game)
        internal static float TrackStep = 0.2f, TrackTurn = 10f; // (track: picked again only when it has gone this many m or turned this many degrees)
        private static readonly float[] OnBeside = { 0f, 20f, -20f, 40f, -40f }; // (the angle asked, then a little to either side of it)

        // one in the picture: the lens that far from its head at the angle asked, lower or higher, where what the shot looks at (aim) is seen.
        // It stays where it was put (the head turning is seen); «track»: it keeps its place by it as it walks — eased as Follow, looked at
        // again at most every RePick s and only when it has moved or turned: the place it has (in the body's own frame, so it turns with
        // it) is kept while it is clear, and only then another is picked — walking past posts never swings it from side to side
        private static Func<Vector3> On(Subject s, Framing f, Func<Vector3> aim)
        {
            float far = f.Size == "close" ? FaceDist : f.Size == "full" ? OnFull : f.Size == "wide" ? OnWide : OnMedium;
            float up = f.Tilt == "low" ? OnLow : f.Tilt == "high" ? OnHigh : FaceUp;
            float[] asked = null;
            if (!float.IsNaN(f.Angle)) { asked = new float[OnBeside.Length]; for (int i = 0; i < asked.Length; i++) asked[i] = f.Angle + OnBeside[i]; }
            Vector3 at;
            bool have = OnSpot(s, asked, far, up, aim(), true, out at); // (false: nowhere clear, it hangs over the head — no place to keep)
            if (!f.Track) return Fixed(at);
            Vector3 was = s.Head(), faced = s.Front(), off = at - was, held = Quaternion.Inverse(Quaternion.LookRotation(faced)) * off; float next = Time.time + RePick;
            return () =>
            {
                Vector3 head = s.Head();
                if (Time.time >= next)
                {
                    next = Time.time + RePick;
                    Vector3 front = s.Front();
                    if (!have || (head - was).sqrMagnitude > TrackStep * TrackStep || Vector3.Angle(front, faced) > TrackTurn)
                    {
                        Vector3 to = aim(), c = head + Quaternion.LookRotation(front) * held;
                        if ((have && LensClear(c, head, to, LensMask)) || OnSpot(s, asked, far, up, to, false, out c))
                        {
                            off = c - head; held = Quaternion.Inverse(Quaternion.LookRotation(front)) * off;
                            was = head; faced = front; have = true;
                        }
                        else next = Time.time + RePick * 4f; // (nowhere clear round it: not asked again at once — it keeps the place it has)
                    }
                }
                at = Vector3.Lerp(at, head + off, 1f - Mathf.Exp(-FollowEase * Time.deltaTime));
                return at;
            };
        }

        // where the lens of «on» stands: the angle asked first, then a little to either side of it, then the same nearer; none clear there
        // (or none asked): the usual order round it (FaceAngles); none at all: over its head. Said in the log as the shot begins
        private static bool OnSpot(Subject s, float[] asked, float far, float up, Vector3 aim, bool say, out Vector3 at)
        {
            Vector3 head = s.Head(), body = s.Front();
            if (asked != null)
            {
                if (RoundSpot(head, body, asked, far, up, aim, out at, s.Low)) return true;
                if (say) Note("camera on: з того боку тісно — інший кут");
            }
            if (RoundSpot(head, body, FaceAngles, far, up, aim, out at, s.Low)) return true;
            if (say) Note("camera on: довкола тісно (або того, на що дивиться кадр, звідти не видно) — кадр згори");
            return false;
        }

        // «push m s»: over s seconds the lens goes m nearer to what it looks at (below zero: away), eased both ways; never nearer than PushStop
        internal static float PushSecs = 3f, PushStop = 0.6f;
        private static Func<Vector3> Push(Func<Vector3> from, Func<Vector3> look, float m, float seconds)
        {
            if (m == 0f) return from;
            float start = Time.unscaledTime, dur = Mathf.Max(0.05f, seconds);
            return () =>
            {
                Vector3 at = from(), d = look() - at;
                float len = d.magnitude;
                if (len < 0.01f) return at;
                return at + d / len * Mathf.Min(m * StoryShots.Ease("both", (Time.unscaledTime - start) / dur), Mathf.Max(0f, len - PushStop));
            };
        }

        // ---------------------------------------------------------------- the line (the 180° rule)
        // Two in a talk: the camera works from one side of the line between them, or from shot to shot they seem to swap places. The scene's
        // first «shoulder» or «two» of a pair fixes that side, kept as a side of the world square to their line, so the reverse shot
        // (shoulder b a) and every later one of the same two ask it here; a line that says left|right itself fixes it anew, and so does a
        // shoulder shot that had to cross as they moved. Forgotten with the scene (Release). Only a step of the scene writes it: a shot
        // of the author's window (PreviewCamera) reads it and leaves it as it is, scene playing or not
        private static readonly Dictionary<string, Vector3> _lines = new Dictionary<string, Vector3>(); // (the pair, whichever is named first → its side)
        private static string Pair(string a, string b) { return string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a; }
        private static Vector3 LeftOf(Vector3 way) { return Vector3.Cross(way, Vector3.up); } // (way: flat — the left of one who looks that way)
        // a side of the world as a side of the way a looks at b: 1 left, -1 right, 0 — none kept
        private static int SideOf(Vector3 world, Vector3 way) { return world == Vector3.zero ? 0 : Vector3.Dot(world, LeftOf(way)) >= 0f ? 1 : -1; }
        private static int LineOf(string a, string b, Vector3 way) { Vector3 world; return _lines.TryGetValue(Pair(a, b), out world) ? SideOf(world, way) : 0; }
        // the side a shot took (0: straight behind, on the line itself — it fixes nothing), as a side of the world; kept for the scene when
        // «keep» (the scene's own step: asked as the shot is made, !_preview — a shoulder shot picks again long after)
        private static Vector3 LineTook(string a, string b, Vector3 way, int side, bool keep)
        {
            Vector3 world = LeftOf(way) * side;
            if (side != 0 && keep && _now != null) _lines[Pair(a, b)] = world;
            return world;
        }

        // both from the side: the lens on the perpendicular of the line between their heads through its middle, far enough for both, at
        // their heads' height, where both heads and what the shot looks at (aim: a little under the middle) are seen. The side of the line
        // asked (else the scene's, else the left of the way a looks at b) first — square to it, then a little off, then nearer; the other
        // side only when that one has no clear spot (said when a side was asked or kept)
        internal static float TwoMin = 2.5f, TwoPer = 0.9f, TwoAdd = 1.5f, TwoFov = 50f; // (m from the middle: at least, per m between them, and added; how wide it sees)
        internal static float TwoDown = 0.3f; // (m under the middle between their heads the shot aims at: less air over them. Unmeasured: a first guess, to tune in game)
        internal static float TwoFar = 7f; // (m between the two: farther than this the shot is made all the same, and a note says it is a shot for a talk close by — CameraStep)
        private static readonly float[] TwoOff = { 0f, 15f, -15f, 30f, -30f };
        private static Vector3 TwoSpot(Vector3 ha, Vector3 hb, Vector3 aim, string a, string b, int asked)
        {
            Vector3 mid = (ha + hb) * 0.5f, way = Flat(hb - ha), left = LeftOf(way);
            float far = Mathf.Max(TwoMin, Utils.DistanceXZ(ha, hb) * TwoPer + TwoAdd);
            int side = asked != 0 ? asked : LineOf(a, b, way), mask = LensMask;
            for (int pass = 0; pass < 2; pass++)
            {
                int now = (side != 0 ? side : 1) * (pass == 0 ? 1 : -1);
                foreach (float dist in new[] { far, far * FaceNearer })
                    foreach (float off in TwoOff)
                    {
                        Vector3 c = mid + Quaternion.Euler(0f, off, 0f) * left * now * dist;
                        if (Physics.CheckSphere(c, LensRoom, mask, QueryTriggerInteraction.Ignore)) continue; // heavy: once as the shot begins, a spot tried — the first clear one ends it (inside a rock, a wall)
                        if (Physics.Linecast(c, ha, mask, QueryTriggerInteraction.Ignore)) continue;          // heavy: the same spot, one line (the first one hidden)
                        if (Physics.Linecast(c, hb, mask, QueryTriggerInteraction.Ignore)) continue;          // heavy: the same spot, one line (the other hidden)
                        if (!StoryCamera.Sees(aim, c)) continue;                                              // (the same spot, the frame's own cast from what it looks at: picked as it is filmed)
                        if (pass == 1 && side != 0) Note("camera two: з того боку лінії тісно — кадр з іншого боку");
                        LineTook(a, b, way, now, !_preview);
                        return c;
                    }
            }
            Note("camera two: довкола тісно — камера ближче до них");
            LineTook(a, b, way, side != 0 ? side : 1, !_preview);
            return mid + left * (side != 0 ? side : 1) * far; // (StoryCamera's own ray pulls it in towards them: never inside a wall)
        }

        // what the camera looks at, followed each frame: the player, the sky before you, the bird, a person, a place
        private static Func<Vector3> Looker(Player p, string what, Vector3 fwd)
        {
            if (what == "player") return () => Player.m_localPlayer != null ? Player.m_localPlayer.GetHeadPoint() : Vector3.zero;
            if (what == "sky") { Vector3 sky = p.GetHeadPoint() + fwd * 30f + Vector3.up * 22f; return () => sky; } // (up before you: the storm, Thor)
            Vector3 last = p.GetHeadPoint();
            Vector3 first;
            float up = UpOf(what); // (a thing someone holds: the thing itself)
            if (!StoryWhere.Find(what, out first)) Note("camera look: нема місця «" + what + "» — дивлюсь на гравця");
            else last = first + Vector3.up * up;
            if (!StoryWhere.Moves(what)) return () => last; // (a place stands still: found once, not each frame)
            return () => { Vector3 at; if (StoryWhere.Find(what, out at)) last = at + Vector3.up * up; return last; }; // (it moves: where it is now)
        }

        // Over one's shoulder towards a target («behind look X»: the player's; «shoulder a b»: anyone's): a few angles tried (behind
        // it a little to either side, then wider), the first from which both heads are seen with nothing between (the game's own
        // camera-blocking layers) and that is not inside anything; tried again every RePick s, the camera easing there — never a rock
        // across the picture. a, b named («shoulder»): the side of their line — the one asked, else the scene's (LineOf), else the first
        // clear — is what this shot keeps to as they move, and what the scene keeps after it (LineTook); null («behind»): no line, as ever
        internal static float ShoulderBack = 3f, ShoulderUp = 1.1f, RePick = 0.5f;
        private static readonly float[] Angles = { 25f, -25f, 45f, -45f, 70f, -70f, 0f, 110f, -110f }; // (over zero: the left shoulder)
        private static Func<Vector3> OverShoulder(Func<Vector3> head, Func<Vector3> look, string a, string b, int asked)
        {
            Vector3 at = Vector3.zero, want = Vector3.zero, world = Vector3.zero; float next = 0f; bool first = true, line = a != null, keep = !_preview; // (keep: the scene's own step — it may write the scene's line, now and when it picks again)
            Action pick = () =>
            {
                next = Time.time + RePick;
                Vector3 h = head(), t = look(), way = Flat(t - h);
                int side = !line ? 0 : !first ? SideOf(world, way) : asked != 0 ? asked : LineOf(a, b, way), took;
                want = ShoulderSpot(h, t, side, line, out took);
                if (!first)
                {
                    if (line && took != 0 && took != side) world = LineTook(a, b, way, took, keep); // (it had to cross as they moved, or had no side yet: the camera is there now, and the next shot of these two keeps to that)
                    return;
                }
                at = want; first = false;
                if (line && side != 0 && took == -side) Note("camera shoulder: з того боку лінії тісно — кадр з іншого боку");
                if (line) world = LineTook(a, b, way, took != 0 ? took : side, keep); // (where the camera really is: what the next shot of these two keeps to)
            };
            if (line) pick(); // (a line to keep: picked as the step begins, so the scene's next shot of these two finds it — played, or set up from a later step)
            return () =>
            {
                if (first || Time.time >= next) pick();
                at = Vector3.Lerp(at, want, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                return at;
            };
        }

        // side: 0 — any (the angles in their order); 1 | -1 — the left | right of the way from the head to the target: that side's angles
        // (and straight behind) first, low then high, the other side's only when none of those is clear. took: the side of the spot found.
        // film («shoulder»): a spot must also be left there by the frame's own cast from the target (StoryCamera.Sees: picked as it is
        // filmed); «behind» asks without it, as it ever did
        private static Vector3 ShoulderSpot(Vector3 head, Vector3 target, int side, bool film, out int took)
        {
            Vector3 dir = target - head; dir.y = 0f;
            float dist = dir.magnitude;
            dir = dist > 0.01f ? dir / dist : Vector3.forward;
            float back = Mathf.Clamp(ShoulderBack + dist * 0.12f, ShoulderBack, 6f); // (farther apart: a step farther back, both still in it)
            int mask = LensMask;
            for (int pass = 0; pass < (side == 0 ? 1 : 2); pass++)
                for (int high = 0; high < 2; high++) // (none clear low — under a roof, in a shed: higher and a step back, what it looks at seen even if the one before it is not)
                    foreach (float ang in Angles)
                    {
                        if (side != 0 && (ang * side < 0f) != (pass == 1)) continue; // (the other side of the line: the second pass only)
                        Vector3 c = head + Quaternion.Euler(0f, ang, 0f) * -dir * back * (high == 1 ? 1.3f : 1f) + Vector3.up * (ShoulderUp + (high == 1 ? 2.5f : 0f));
                        if (Physics.CheckSphere(c, 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;              // heavy: every RePick s while the shot is on, a spot tried — the first clear one ends it (inside a rock, a wall)
                        if (high == 0 && Physics.Linecast(c, head, mask, QueryTriggerInteraction.Ignore)) continue;    // heavy: the same spot, one line (the one before the lens hidden)
                        if (Physics.Linecast(c, target, mask, QueryTriggerInteraction.Ignore)) continue;               // heavy: the same spot, one line (what it looks at hidden)
                        if (film && !StoryCamera.Sees(target, c)) continue;                                            // (the same spot, the frame's own cast — wider than a line)
                        took = ang > 0f ? 1 : ang < 0f ? -1 : 0;
                        return c;
                    }
            took = 0;
            // still none: close behind the head, never inside a wall (a short step back from what blocks it)
            Vector3 aim = head - dir * back + Vector3.up * ShoulderUp;
            RaycastHit hit;
            if (Physics.SphereCast(head, 0.3f, (aim - head).normalized, out hit, Vector3.Distance(head, aim), mask, QueryTriggerInteraction.Ignore)) return head + (aim - head).normalized * Mathf.Max(0.4f, hit.distance - 0.2f); // heavy: only when no spot was clear, one cast
            return aim;
        }
    }
}
#endif

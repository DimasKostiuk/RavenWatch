#if RAVEN_ITEM && RAVEN_STORY
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HuginWhispers
{
    // A scene checked as the player will see it (the user 2026-11-04: «how can you not see the crooked arm, the flicker»): the studio
    // plays a scene and, every frame, measures every body in it — the player and each person of its cast — against the mistakes the
    // eye catches, and goes on SceneTail s after its end (the body let go, the head its own again: where a seam shows). It keeps two
    // kinds of pictures:
    //   <name>_step_<k>.png        the frame each step begins on, big (StepFrame px, four a sheet), the time and the step's words in the picture:
    //                              the key frames of the scene (review 2 2026-11-10: 24 frames of 480 px and nobody could tell which step was which)
    //   <name>_<n>.png             the screen, the scene's own camera, as he sees it: every 1/fps s and as each step begins, 24 frames a
    //                              sheet, each labelled; a frame no different from the last one kept is left out (FrameSame)
    //   <name>_<who>_<view>.png    each body close (front, side), as each step begins and when it jolts (a limb flies, a bone jerks: the
    //                              moment to look at): its pose can be judged in them, in a wide shot it cannot
    // A clip of ours played on a body in the scene brings its «want» lines (the user's words as measures): judged there, on the living body.
    // Out: studio/out/<name>.txt, a VERDICT on top, then the steps: a row a step with when it began, how long it took and what came of it
    // (StoryScenes.Outcomes: «ran 14 m in 4.1 s, stopped 0.3 m off»; the VERDICT's lines that fall in its time under it — review 2 2026-11-10, В9), then
    // each body's numbers, each body's pose in words as each step begins.
    //   scene <name> <scene id> [fps=2] [from=0]
    // What it looks for (each a mistake already made):
    //   pose         BodyCheck: the one set of rules, the stage's own (palm, fist, hands, elbows, knees, shoulder, neck, feet sliding,
    //                limbs flying, targets missed, a thing in the hands; here also the world's real surface a clip's «touch» means)
    //   flicker      a renderer's material changed again and again (the game put its own back: the echo's hair; once is a fade, no flicker)
    //   off screen   a shot on, and the one it is about not in the picture (a person; for the player only the number: a shot may be of the raven)
    //   blocked      a shot on, the body in the picture and something between it and the camera
    //   camera in    the camera inside something
    //   HUD          the game's screen on while a cutscene has the player
    //   raven out    the scene's raven out of the picture most of the time a shot is on (it came, it spoke: unseen — gate_shut 2026-11-04)
    //   held long    one step holding the shot longer than StepLong s (a bird flying in 50 m while he stared up — gate_shut 2026-11-04)
    // and of the scene as a whole:
    //   black        the picture black longer than SceneBlack s in one go (a walk hidden in a fade that went on: the sign 2026-11-04)
    //   walk held    a «player go» that gained no way and was given up (StoryWalk.Helds)
    //   stopped      the scene broken off before its end (StoryScenes.LastStopOf), or held for the author's «Далі»
    //   errors       an exception or an error of ours in the log while it played
    //   dropped      a line of its file the game would not read (StoryScenes.Errors: that step is simply not played)
    //   far          the player not at its place when it began (it does not begin then: StoryScenes.Play)
    // The world it plays in: its lines «test: <the story's actions; …>» (and «npc <who> at <place>»: that person put there at once) run
    // first, so the check never depends on where the player's own game happens to be (Heid in the camp, the gate shut: runs spoilt
    // 2026-11-04). What the run changes is given back after it (TestState): the world's story keys, the character's story, where he
    // stood — the player's own game goes on as it was. A scene a personal quest's stage plays is played on that stage (OnItsStage: the quest
    // set there as the author's jump does, its start: run): what glows, what its lines ask, is what that stage gives — a new world's player
    // on the quest's first stage left the book dark, 2026-11-07. A server quest's stage is the world's: its «test:» lines set it.
    //   keys         the player pressed keys or moved the mouse while the scene left him the keys: his numbers are his, not the scene's
    // It plays it as he would: from where the scene says («check: <place> <m> [in]»: CheckSpot), else far from its place at its place (Story.Tp), and waits till he stands; a step waiting for
    // his touch («until touched <place.piece>») is touched for him after SceneTouchAfter s (LocTouch.Touch, as E).
    internal static partial class Studio
    {
        internal static float SceneBlack = 3f, SceneTpFrom = 15f, SceneTouchAfter = 2f, SceneTpNear = 3f, StepShotAfter = 0.4f, SceneTail = 2.5f, FrameSame = 0.012f, FrameKeepEvery = 4f, BlockedMost = 1f, CamInMost = 0.5f, HudMost = 0.5f, StepLong = 8f, BirdOffMost = 2f; // s · m from its place: taken there first · s · m · s · s after its end · how alike two frames are left out as one · s: one kept however alike · s · s · s
        internal const int BodyFrame = 400, BodyFrames = 24;
        internal const string BodyViews = "front,side,holdr,holdl"; // (the hands from their palms' side too: a grab, a hold)
        internal static float JoltShotGap = 0.6f; // s between two close frames of a jolt (one each, not a burst)

        private sealed class Body
        {
            public string Who;
            public Character C;
            public Transform Head, FootL;
            public readonly Dictionary<Renderer, int> Mats = new Dictionary<Renderer, int>();
            public readonly Dictionary<Renderer, int> Changed = new Dictionary<Renderer, int>();
            public bool Seen;
            public int Swaps;
            public float OffScreen, OnScreen, Blocked, SizeSum, SizeN;
            public string SwapStep = "";
            public BodyCheck Pose;
            // close pictures of it
            public Rig Cams;
            public Texture2D[] Sheets;
            public int Cols, Rows, Shot;
            public string ClipWas = ""; public float MomentWas;
            public float JoltShotAt = -9f, IdleShotAt = -99f;
            public readonly List<float> ShotAt = new List<float>(); // (the moments a clip on it has: its keys' middle, its marks, its want times)
            public int Dropped; // (close shots asked past BodyFrames: said, not lost in silence)
        }

        private static Body BodyOf(string who, Character c)
        {
            Animator anim = c.GetComponentInChildren<Animator>(); // heavy: once per body of a checked scene
            if (anim == null) return null;
            Transform v = anim.transform;
            Body b = new Body { Who = who, C = c };
            b.Head = BodyClips.SkeletonBone(c, v, "Head"); b.FootL = BodyClips.SkeletonBone(c, v, "LeftFoot");
            if (c is Humanoid) b.Pose = new BodyCheck((Humanoid)c) { Real = true };
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) b.Mats[r] = r.sharedMaterial != null ? r.sharedMaterial.GetInstanceID() : 0; // heavy: once per body
            b.Cams = Make(BodyViews, BodyFrame);
            b.Sheets = HeroSheets(b.Cams, BodyFrames, out b.Cols, out b.Rows);
            return b;
        }

        private static int _sceneMask;
        private static void Measure(Body b, string step, float t, float dt, bool walked)
        {
            if (b.C == null) return;
            // flicker: a material that keeps changing (one change is a fade, a new coat)
            List<Renderer> rs = new List<Renderer>(b.Mats.Keys);
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                int now = r.sharedMaterial != null ? r.sharedMaterial.GetInstanceID() : 0;
                if (now == b.Mats[r]) continue;
                b.Mats[r] = now;
                if (!b.Seen) continue;
                int times; b.Changed.TryGetValue(r, out times);
                b.Changed[r] = ++times;
                if (times >= 2) { b.Swaps++; if (b.SwapStep.Length == 0) b.SwapStep = r.name + " at " + t.ToString("0.0") + " s, " + step; }
            }
            if (b.Pose != null) b.Pose.Measure(t, walked);
            // in the picture while a shot is on
            Camera cam = Camera.main;
            if (StoryCamera.Active && cam != null && b.Head != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(b.Head.position);
                bool on = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                if (on)
                {
                    b.OnScreen += dt;
                    if (_sceneMask == 0) _sceneMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "terrain", "vehicle");
                    if (Physics.Linecast(cam.transform.position, b.Head.position, _sceneMask, QueryTriggerInteraction.Ignore)) b.Blocked += dt; // (one ray a frame a body, only in a check)
                    if (b.FootL != null) { b.SizeSum += Mathf.Abs(vp.y - cam.WorldToViewportPoint(b.FootL.position).y); b.SizeN++; }
                }
                else b.OffScreen += dt;
            }
            b.Seen = true;
        }

        // where the scene begins as he would come to it, its line «check: <place[.piece]> <m> [in]»: that far from the thing, outside
        // (away from its place's middle: a gate of a shut Hall from without — the user 2026-11-04 was put inside) or «in» (towards it);
        // no way to tell (no place, or the middle itself): on the side he is on. Turned to the thing
        private static bool CheckSpot(StoryScenes.SceneDef s, Player p, bool hasMiddle, Vector3 middle, out Vector3 spot, out Quaternion turn)
        {
            spot = Vector3.zero; turn = Quaternion.identity;
            string[] w = s.Check.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            Vector3 at;
            if (w.Length == 0 || !StoryWhere.Find(w[0], out at)) return false;
            float off; if (w.Length < 2 || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out off)) off = 4f;
            Vector3 way = hasMiddle ? at - middle : Vector3.zero; way.y = 0f;
            if (way.sqrMagnitude < 1f) { way = p.transform.position - at; way.y = 0f; }
            if (way.sqrMagnitude < 0.01f) way = Vector3.forward;
            way.Normalize();
            if (w.Length > 2 && w[2] == "in") way = -way;
            spot = at + way * off;
            turn = Quaternion.LookRotation(-way);
            return true;
        }

        // what the log says while a scene is checked: exceptions and our errors (not the game's own noise)
        private static readonly List<string> _sceneErrors = new List<string>();
        private static void SceneLog(string text, string trace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (type == LogType.Error && text.IndexOf("RavenWatch", System.StringComparison.Ordinal) < 0 && text.IndexOf("HuginWhispers", System.StringComparison.Ordinal) < 0) return;
            if (_sceneErrors.Count < 8) _sceneErrors.Add(type + ": " + (text.Length > 160 ? text.Substring(0, 160) : text));
        }

        // a frame's look, a sparse grid of its pixels (what two frames are compared by)
        private static List<float> FrameLook(Texture2D a)
        {
            List<float> l = new List<float>();
            for (int y = 8; y < a.height; y += 16)
                for (int x = 8; x < a.width; x += 16) { Color p = a.GetPixel(x, y); l.Add(p.r); l.Add(p.g); l.Add(p.b); }
            return l;
        }

        // how alike two looks are: 0 the same
        private static float FrameOff(List<float> a, List<float> b)
        {
            if (a == null || b == null || a.Count != b.Count || a.Count == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < a.Count; i++) sum += Mathf.Abs(a[i] - b[i]);
            return sum / a.Count;
        }

        // the moments of a clip worth a frame: a moment in, its keys where its targets come full (a hand on a thing), its marks, its wants, near its end
        private static List<float> ClipMoments(PAnimClip c)
        {
            List<float> at = new List<float> { Mathf.Min(0.4f, c.Length * 0.5f) };
            foreach (PAnimEvent m in c.Marks) at.Add(m.T);
            foreach (string w in c.Wants) { float v; string t0 = w.Split(' ')[0].Split('-')[0]; if (float.TryParse(t0, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) at.Add(v); }
            foreach (string ch in new[] { "@LeftHand.on.w", "@RightHand.on.w", "@Prop.w", "@LeftHand.w", "@RightHand.w" })
            {
                if (!c.Bones.Contains(ch)) continue;
                float was = c.Value(ch, 0f).x;
                foreach (PAnimKey k in c.Keys) { Vector3 v; if (!k.V.TryGetValue(ch, out v)) continue; if (v.x >= 0.95f && was < 0.95f) at.Add(k.T); was = v.x; } // (the moment it comes full: the grab)
            }
            if (c.Length > 1.5f) at.Add(c.Length - 0.3f);
            at.Sort();
            List<float> kept = new List<float>();
            foreach (float v in at) if (v >= 0f && v <= c.Length && (kept.Count == 0 || v - kept[kept.Count - 1] > 0.3f)) kept.Add(v);
            return kept;
        }

        private static void ShootBody(Body b, Player p, StringBuilder rows, string step, float t)
        {
            if (b.C == null || b.Cams == null) return;
            if (b.Shot >= BodyFrames) { b.Dropped++; return; }
            PlaceBody(b.Cams, b.C); // (one placing for the player and a person: the same views)
            Shoot(b.Cams, b.Sheets, b.Shot, b.Cols, b.Rows);
            for (int v = 0; v < b.Sheets.Length; v++) Label(b.Sheets[v], b.Shot, b.Cols, b.Rows, BodyFrame, t.ToString("0.0", CultureInfo.InvariantCulture) + " s  " + b.Cams.Views[v], step); // (in the picture: when, which step — review 2 2026-11-10)
            b.Shot++;
            rows.Append("    ").Append(b.Who).Append(" close ").Append(b.Shot).Append(": ").Append(b.Pose != null ? b.Pose.Words() : "").Append('\n');
            BodyAnimator an = b.C.GetComponent<BodyAnimator>(); // (a shot's, not a frame's: at most BodyFrames a body)
            if (an != null) rows.Append("      layers: ").Append(an.LayersSaid()).Append('\n'); // (what is on the body in that frame: which slot plays what, how strongly — the stage's slot is not the scene's)
        }

        // what a check's run may change, and is given back after it
        private sealed class TestState
        {
            private readonly List<string> _keys = new List<string>();
            private readonly string _story;
            private readonly Vector3 _at; private readonly Quaternion _turn;
            private readonly bool _hadHugin, _hadMunin; // (a raven a run gave is given back too: left on a new character, its live quest started by itself, 2026-11-07; the book is one of the RavenWatch_ keys)
            private readonly Dictionary<string, string> _ours = new Dictionary<string, string>(); // (every key of ours on the character: a raven's marks, the book seen, talks remembered — review 2026-11-07)
            private readonly ZPackage _bag = new ZPackage();
            internal readonly List<string> Changed = new List<string>(); // (what it had to give back: said in the report)
            private static bool Ours(string k) { if (k.StartsWith(StoryWorld.Prefix)) return true; string n = Name(k); foreach (Loc l in Locs.All) if (System.Array.IndexOf(l.StoryKeys, n) >= 0) return true; return false; } // (never where a place stands or that it is built: given back, it would be built twice)
            internal TestState(Player p)
            {
                foreach (string k in ZoneSystem.instance.GetGlobalKeys()) if (Ours(k)) _keys.Add(k);
                p.m_customData.TryGetValue(Story.Key, out _story);
                foreach (KeyValuePair<string, string> kv in p.m_customData) if (kv.Key.StartsWith("RavenWatch_", System.StringComparison.Ordinal)) _ours[kv.Key] = kv.Value;
                p.GetInventory().Save(_bag);
                _hadHugin = Trial.HadKind(p, false); _hadMunin = Trial.HadKind(p, true);
                _at = p.transform.position; _turn = p.transform.rotation;
            }
            private static string Name(string k) { int sp = k.IndexOf(' '); return sp < 0 ? k : k.Substring(0, sp); }
            internal void Back(Player p)
            {
                HashSet<string> had = new HashSet<string>(_keys);
                foreach (string k in new List<string>(ZoneSystem.instance.GetGlobalKeys())) if (Ours(k) && !had.Contains(k)) ZoneSystem.instance.RemoveGlobalKey(Name(k));
                foreach (string k in _keys) ZoneSystem.instance.SetGlobalKey(k);
                if (p == null) return;
                if (_story == null) p.m_customData.Remove(Story.Key); else p.m_customData[Story.Key] = _story;
                Story.Reset();
                if (!_hadHugin && Trial.HadKind(p, false)) { StoryOwn.Unown(p, false); Changed.Add("Hugin it was given"); }
                if (!_hadMunin && Trial.HadKind(p, true)) { StoryOwn.Unown(p, true); Changed.Add("Munin it was given"); }
                foreach (string k in new List<string>(p.m_customData.Keys))
                {
                    if (!k.StartsWith("RavenWatch_", System.StringComparison.Ordinal)) continue;
                    string was;
                    if (!_ours.TryGetValue(k, out was)) { p.m_customData.Remove(k); if (k != Story.Key) Changed.Add(k + " (new)"); }
                    else if (p.m_customData[k] != was) { p.m_customData[k] = was; if (k != Story.Key) Changed.Add(k); }
                }
                foreach (KeyValuePair<string, string> kv in _ours) if (!p.m_customData.ContainsKey(kv.Key)) { p.m_customData[kv.Key] = kv.Value; Changed.Add(kv.Key + " (gone)"); }
                ZPackage now = new ZPackage(); p.GetInventory().Save(now);
                if (System.Convert.ToBase64String(now.GetArray()) != System.Convert.ToBase64String(_bag.GetArray()))
                {
                    p.UnequipAllItems(); // (Load makes new items: the body would still wear the old ones, gone from the bag)
                    _bag.SetPos(0); p.GetInventory().Load(_bag);
                    foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(p.GetInventory().GetAllItems())) if (it.m_equipped) { it.m_equipped = false; p.EquipItem(it, false); }
                    Changed.Add("the bag");
                }
                if (Utils.DistanceXZ(p.transform.position, _at) > 3f && !Story.Tp(p, _at, _turn)) Changed.Add("NOT taken back where he stood: the game refused the portal"); // (he never left: no portal for nothing)
            }
        }

        // who «npc <who> at» was to put where: asked after the run has settled (land beyond what this game runs freezes a person, and the order goes nowhere)
        private static readonly Dictionary<NpcActor, Vector3> _putAt = new Dictionary<NpcActor, Vector3>();

        // the scene's «test:» lines: the story's own actions, and «npc <who> at <place>» (put there at once); what went wrong, null none
        private static readonly HashSet<string> _prepared = new HashSet<string>(); // (lines done in this run: asked again only the ones that failed — a «give» was done eight times)
        private static string Prepare(StoryScenes.SceneDef s, Player p)
        {
            List<string> bad = new List<string>();
            foreach (string raw in s.Test.Split(';'))
            {
                string a = raw.Trim();
                if (a.Length == 0 || _prepared.Contains(a)) continue;
                string[] w = a.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (w.Length == 4 && w[0] == "npc" && w[2] == "at")
                {
                    Vector3 at; NpcActor who = Npcs.Find(w[1]);
                    if (who == null || !StoryWhere.Approach(w[3], p.transform.position, out at)) { bad.Add(a); continue; } // (beside it, not in it: a person put into the hearth burns)
                    who.Order("tp " + Npcs.V(at), NpcMind.Layer.Scene); who.Order("free", NpcMind.Layer.Scene);
                    _putAt[who] = at; _prepared.Add(a);
                    continue;
                }
                string why = StoryActions.Check(a, true);
                if (why != null) { bad.Add(a + ": " + why); continue; }
                StoryActions.Do(p, null, a);
                _prepared.Add(a);
            }
            return bad.Count > 0 ? "test: " + string.Join(" · ", bad.ToArray()) : null;
        }

        private static IEnumerator SceneCheck(string name, string id, float fps, int from)
        {
            StoryScenes.SceneDef s = StoryScenes.Find(id);
            Player p = Player.m_localPlayer;
            if (s == null || p == null) { Note(name, s == null ? "no scene «" + id + "»" : "no player"); yield break; }
            TestState was = new TestState(p);
            string before = StoryScenes.Playing; // (what played before the check: not left to this finally)
            IEnumerator run = SceneRun(name, s, fps, from);
            try { while (run.MoveNext()) yield return run.Current; }
            finally
            {
                if (StoryScenes.Playing.Length > 0 && StoryScenes.Playing != before) StoryScenes.Stop("перевірка студії скінчилась"); // (never left playing under the world given back: its own, or the next one its quest went on to — echo_door played on after echo, 2026-11-07)
                was.Back(Player.m_localPlayer);
                if (was.Changed.Count > 0) NoteMore(name, "given back after the run: " + string.Join(", ", was.Changed.ToArray()));
            }
        }

        private static IEnumerator SceneRun(string name, StoryScenes.SceneDef s, float fps, int from)
        {
            string id = s.Id;
            Player p = Player.m_localPlayer;
            if (p == null) yield break;
            // at its place first: far from it, the check takes him there itself and waits till the portal lets him go and the
            // ground is under him (a teleport asked by a line before lands seconds later: two scenes once played 150 m off)
            // (a portal still going — the last run's way back — makes the game refuse a new one without a word: he stayed where he was and the scene «did not begin», 2026-11-06)
            float portal = Time.time;
            while (p.IsTeleporting() && Time.time - portal < 30f) yield return null;
            portal = Time.time; // (from its landing: counted from before it, a long portal left no pause and the next was refused, 2026-11-07)
            while (Time.time - portal < 2.5f) yield return null; // (the game's own pause between two portals)
            Vector3 place = Vector3.zero; float far = -1f;
            bool known = s.Place.Length > 0 && StoryWhere.Find(s.Place, out place);
            Vector3 spot; Quaternion turn;
            if (CheckSpot(s, p, known, place, out spot, out turn)) { if (Utils.DistanceXZ(spot, p.transform.position) > SceneTpNear) { Story.Tp(p, spot, turn); yield return null; } }
            else if (known && Utils.DistanceXZ(place, p.transform.position) > SceneTpFrom) { Story.Tp(p, place); yield return null; }
            float wait = Time.time;
            while ((p.IsTeleporting() || !p.IsOnGround()) && Time.time - wait < 30f) yield return null;
            if (Time.time - wait > 0.1f) { float settle = Time.time; while (Time.time - settle < 2f) yield return null; } // (the world round him built)
            if (known) far = Utils.DistanceXZ(place, p.transform.position);
            // its world as written, once he stands there: a place far off is not loaded before (Heid «at hall.vohnyshche» was asked of a Hall
            // 500 m away and never put there, 2026-11-06); then people put there stand, the world's keys reach everyone
            string prepared = null;
            if (s.Test.Length > 0)
            {
                _putAt.Clear(); _prepared.Clear();
                float tryUntil = Time.time + 8f; // (a person made by the same line's key comes a moment later: the server makes it)
                prepared = Prepare(s, p);
                while (prepared != null && Time.time < tryUntil) { float pause = Time.time; while (Time.time - pause < 1f) yield return null; prepared = Prepare(s, p); }
                float settle = Time.time; while (Time.time - settle < 1.5f) yield return null;
                foreach (KeyValuePair<NpcActor, Vector3> put in _putAt)
                    if (put.Key == null || Utils.DistanceXZ(put.Key.transform.position, put.Value) > 3f) prepared = (prepared != null ? prepared + " · " : "") + (put.Key != null ? put.Key.Id : "?") + " was not put there (its land is not run by this game: it stands frozen)";
            }
            List<string> touched = new List<string>(); string touchWalk = null; int touchWalkAt = -1;
            int stopsWas = StoryScenes.CountOf(StoryScenes.StopsOf, id), latesWas = StoryScenes.CountOf(StoryScenes.LatesOf, id); // (before it begins: a person missing stops it in its very first step, and that read «OK», 2026-11-07)
            string onStage = OnItsStage(p, id);
            if (from > 0 && StoryScenes.Playing == id) { StoryScenes.Stop("перевірка почне її з кроку " + (from + 1)); stopsWas = StoryScenes.CountOf(StoryScenes.StopsOf, id); } // (its quest began it from the start: this stop is the check's own)
            bool begun = from == 0 && StoryScenes.Playing == id; // (its quest began it itself, as in the game — set on its stage now, or standing there all along: begun again it read «STOPPED: інша сцена»)
            if (!begun && !StoryScenes.Play(s, from)) { Note(name, "scene «" + id + "» did not begin: " + (p.IsTeleporting() ? "still in a portal" : far >= 0f ? Mathf.RoundToInt(far) + " m from its place «" + s.Place + "»" : "see the log")); yield break; }
            int helds = StoryWalk.Helds;
            float keys = 0f;
            float black = 0f, blackMost = 0f, heldFor = 0f, touchWait = -1f, hudOn = 0f, camIn = 0f, birdOn = 0f, birdOff = 0f, stepAt = 0f, stepLong = 0f; string blackStep = "", longStep = ""; int touchedAt = -1;
            _sceneErrors.Clear();
            Application.logMessageReceived += SceneLog;
            Dictionary<Character, Body> bodies = new Dictionary<Character, Body>();
            StringBuilder rows = new StringBuilder();
            if (onStage != null) rows.Append("0.0 s  (on its stage: ").Append(onStage).Append(")\n");
            List<Texture2D> frames = new List<Texture2D>(), stepFrames = new List<Texture2D>();
            List<StepRow> steps = new List<StepRow>(); // (each step as it began: the table on top of the report)
            List<float> lastKept = null;
            int sheet = 0, stepSheet = 0;
            bool stepShotDue = false;
            float t0 = Time.time, nextShot = 0f, nextClose = -1f, keptAt = -99f, tailUntil = -1f, every = 1f / Mathf.Clamp(fps, 0.5f, 8f), most = 300f;
            string lastStep = "";
            bool ended = false;
            try
            {
                while ((StoryScenes.Playing == s.Id || tailUntil < 0f || Time.time < tailUntil) && Time.time - t0 < most + SceneTail)
                {
                    yield return new WaitForEndOfFrame();
                    float t = Time.time - t0;
                    bool playing = StoryScenes.Playing == s.Id;
                    if (!playing && tailUntil < 0f) { if (steps.Count > 0) steps[steps.Count - 1].T1 = t; tailUntil = Time.time + SceneTail; rows.Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s  (its end: measured on for ").Append(SceneTail.ToString("0.#", CultureInfo.InvariantCulture)).Append(" s)\n"); nextClose = t + StepShotAfter; nextShot = t; }
                    else if (playing && Time.time - t0 > most) break;
                    string step = !playing ? "after its end" : StoryScenes.At >= 0 && StoryScenes.At < s.Steps.Count ? (StoryScenes.At + 1) + ". " + s.Steps[StoryScenes.At].Line : "-";
                    if (StoryScreen.Black >= 0.99f) { black += Time.unscaledDeltaTime; if (black > blackMost) { blackMost = black; blackStep = step; } } else black = 0f;
                    if (playing && StoryScenes.Held) heldFor += Time.deltaTime;
                    if (!StoryCamera.TakesPlayer && (Input.anyKey || Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.05f)) keys += Time.deltaTime; // (his hands on it: what his body does is his)
                    if (StoryCamera.TakesPlayer && Hud.instance != null && Hud.instance.IsVisible()) hudOn += Time.deltaTime;
                    Camera main = Camera.main;
                    if (StoryCamera.Active && main != null)
                    {
                        if (_sceneMask == 0) _sceneMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "terrain", "vehicle");
                        if (Physics.CheckSphere(main.transform.position, 0.08f, _sceneMask, QueryTriggerInteraction.Ignore)) camIn += Time.deltaTime;
                        if (StoryScenes.HasBird)
                        {
                            Vector3 vp = main.WorldToViewportPoint(StoryScenes.BirdAt);
                            if (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) birdOn += Time.deltaTime; else birdOff += Time.deltaTime;
                        }
                    }
                    // a step waiting for his touch (until touched <place.piece>): touched for him after SceneTouchAfter s, as E would
                    if (playing && StoryScenes.At >= 0 && StoryScenes.At < s.Steps.Count && StoryScenes.At != touchedAt)
                    {
                        StoryScenes.Step w = s.Steps[StoryScenes.At];
                        if (touchWalkAt != StoryScenes.At) { touchWalk = null; touchWalkAt = StoryScenes.At; } // (a walk belongs to its own step)
                        int k = w.Text.IndexOf("touched ", System.StringComparison.Ordinal);
                        if (w.W.Length > 0 && w.W[0] == "until" && k >= 0)
                        {
                            if (touchWait < 0f) touchWait = t;
                            else if (t - touchWait > SceneTouchAfter)
                            {
                                // as a player could: the piece glows for him, and he is within reach of it (else walked there first, by the core's walk) — 2026-11-07 it was
                                // touched from 5.6 m and the whole reach measured from where no player stands
                                string key = w.Text.Substring(k + 8).Split(' ', '|', ',')[0].Trim();
                                ZNetView piece = StoryWhere.Piece(key);
                                LocTouch lt = piece != null ? piece.GetComponent<LocTouch>() : null;
                                Vector3 at = piece != null ? StoryProps.Middle(piece) : p.transform.position;
                                float reach = Vector3.Distance(p.GetEyePoint(), at);
                                if (lt == null || !lt.On) { touched.Add(key + ": NOT touchable (it does not glow for him: " + WhyNoTouch(key, piece, p) + "): the check stops here, the rest of the scene unseen"); touchedAt = StoryScenes.At; touchWait = -1f; most = t; } // (no 300 s of waiting for what nobody will press, 2026-11-07)
                                else if (reach > p.m_maxInteractDistance * TouchReach && touchWalk == null) { string no = StoryWalk.Go(p, key, TouchStand, false, false, false, false, false); touchWalk = no == null ? key : ""; rows.Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s  (the check walks to ").Append(key).Append(", ").Append(reach.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m off)\n"); if (no != null) touched.Add(key + ": could not walk there (" + no + ")"); }
                                else if (!StoryWalk.On)
                                {
                                    LocTouch.Touch(key, at); touchedAt = StoryScenes.At; touchWait = -1f;
                                    bool within = reach <= p.m_maxInteractDistance * TouchReach;
                                    touched.Add(key + " from " + reach.ToString("0.0", CultureInfo.InvariantCulture) + " m" + (touchWalk == null ? "" : touchWalk.Length > 0 ? " (walked there first)" : " (no walk there)") + (within ? "" : ": NOT within a player's reach, the rest measured from where no player stands"));
                                    touchWalk = null;
                                    rows.Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s  (the check touched ").Append(key).Append(" from ").Append(reach.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m)\n");
                                }
                            }
                        }
                        else touchWait = -1f;
                    }
                    bool stepBegun = step != lastStep;
                    if (stepBegun) stepAt = t;
                    else if (playing && StoryCamera.Active && !StoryScenes.Held && t - stepAt > stepLong) { stepLong = t - stepAt; longStep = step; }
                    if (stepBegun && playing) { if (steps.Count > 0) steps[steps.Count - 1].T1 = t; steps.Add(new StepRow { Index = StoryScenes.At, Line = step, T0 = t }); rows.Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s  ").Append(step).Append('\n'); nextShot = Mathf.Min(nextShot, t + StepShotAfter); nextClose = t + StepShotAfter; stepShotDue = true; } // (a frame as each step begins, a moment in: what it does shows)
                    lastStep = step;
                    if (!bodies.ContainsKey(p)) { Body me = BodyOf("player", p); if (me != null) bodies[p] = me; }
                    foreach (KeyValuePair<string, NpcActor> c in StoryScenes.CastNow)
                    {
                        Character ch = c.Value != null ? c.Value.GetComponent<Character>() : null;
                        if (ch == null || bodies.ContainsKey(ch)) continue;
                        Body nb = BodyOf(c.Key, ch);
                        if (nb != null) bodies[ch] = nb;
                    }
                    foreach (Body b in bodies.Values)
                    {
                        Measure(b, step, t, Time.deltaTime, b.C == (Character)p && StoryWalk.On);
                        BodyAnimator ba = BodyAnimator.Of(b.C);
                        float moment = 0f; PAnimClip clip = ba != null ? ba.CodeClip(out moment) : null;
                        string clipNow = clip != null ? clip.Name : "";
                        bool again = clip != null && !clip.Loop && clipNow == b.ClipWas && moment < b.MomentWas - 0.1f; // (the same clip played again; a loop's next round is no new clip: its moments would eat the frames, its wants judged anew)
                        if (clipNow != b.ClipWas || again)
                        {
                            b.ClipWas = clipNow;
                            b.ShotAt.Clear(); // (a clip cut off: its moments are not the next one's)
                            if (clip != null && b.Pose != null) b.Pose.AddWants(clip, t - moment); // (its own words: judged where it plays)
                            if (clip != null) foreach (float at in ClipMoments(clip)) if (at > moment) b.ShotAt.Add(t - moment + at); // (found mid-way: only the moments still ahead, never a burst of the past ones)
                        }
                        b.MomentWas = moment;
                        for (int i = b.ShotAt.Count - 1; i >= 0; i--) if (t >= b.ShotAt[i]) { b.ShotAt.RemoveAt(i); rows.Append("    ").Append(b.Who).Append(" at a moment of its clip, ").Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s:\n"); ShootBody(b, p, rows, step, t); }
                        if (b.Pose != null && b.Pose.Jolt && t - b.JoltShotAt > JoltShotGap && b.Shot < BodyFrames) { b.JoltShotAt = t; rows.Append("    ").Append(b.Who).Append(" jolted at ").Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s:\n"); ShootBody(b, p, rows, step, t); }
                    }
                    if (t >= nextShot)
                    {
                        nextShot = t + every;
                        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                        string when = t.ToString("0.0", CultureInfo.InvariantCulture) + " s  " + step;
                        if (stepShotDue) // (the frame a step begins on, big: 960 px, four a sheet — the key frames of a scene, not 24 small ones; review 2 2026-11-10)
                        {
                            stepShotDue = false;
                            Texture2D big = Shrink(shot, StepFrame);
                            if (big == shot) { big = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false); big.SetPixels(shot.GetPixels()); }
                            Text(big, 4, big.height - 8 * TextScale - 4, when.Length > StepFrame / (6 * TextScale) - 1 ? when.Substring(0, StepFrame / (6 * TextScale) - 1) : when, new Color32(255, 230, 120, 255));
                            big.Apply(false);
                            stepFrames.Add(big);
                            if (stepFrames.Count == 4) { SaveSheet(name + "_step_" + stepSheet, stepFrames, 2); stepSheet++; }
                        }
                        Texture2D small = Shrink(shot, 480);
                        if (small != shot) Object.Destroy(shot);
                        List<float> look = FrameLook(small);
                        bool same = !stepBegun && t - keptAt < FrameKeepEvery && FrameOff(look, lastKept) < FrameSame;
                        if (same) Object.Destroy(small);
                        else
                        {
                            lastKept = look;
                            keptAt = t;
                            Text(small, 4, small.height - 8 * TextScale - 4, when.Length > 39 ? when.Substring(0, 39) : when, new Color32(255, 230, 120, 255)); // (which step, in the picture)
                            small.Apply(false);
                            frames.Add(small);
                            rows.Append("    frame ").Append(sheet).Append('.').Append(frames.Count).Append(" at ").Append(t.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s\n");
                            if (frames.Count == 24) { SaveSheet(name + "_" + sheet, frames); sheet++; }
                        }
                    }
                    if (nextClose >= 0f && t >= nextClose)
                    {
                        nextClose = -1f;
                        foreach (Body b in bodies.Values)
                        {
                            BodyAnimator ba = BodyAnimator.Of(b.C);
                            if ((ba == null || !ba.CodeBusy) && t - b.IdleShotAt < IdleShotGap) continue; // (a body none of ours holds: a frame now and then, the rest kept for the clips)
                            if (ba == null || !ba.CodeBusy) b.IdleShotAt = t;
                            ShootBody(b, p, rows, step, t);
                        }
                    }
                }
                ended = true;
            }
            finally
            {
                Application.logMessageReceived -= SceneLog;
                try
                {
                    if (frames.Count > 0) { SaveSheet(name + "_" + sheet, frames); sheet++; }
                    if (stepFrames.Count > 0) { SaveSheet(name + "_step_" + stepSheet, stepFrames, 2); stepSheet++; }
                    foreach (Body b in bodies.Values)
                    {
                        if (b.Shot > 0) HeroSave(name + "_" + b.Who, b.Cams, b.Sheets);
                        HeroFree(b.Sheets, b.Cams);
                    }
                }
                finally { SceneReport(name, id, s, from, far, t0, sheet, stepSheet, ended, lastStep, heldFor, blackMost, blackStep, helds, hudOn, camIn, most, bodies, rows, birdOn, birdOff, stepLong, longStep, keys, prepared, stopsWas, latesWas, touched, steps); } // (last: who reads it finds the sheets it names)
            }
        }

        // why a piece does not glow: no piece, or which of its «touch:» conditions does not hold now
        private static string WhyNoTouch(string key, ZNetView piece, Player p)
        {
            if (piece == null) return "no piece of that name loaded";
            int dot = key.IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(key.Substring(0, dot)) : null;
            string name = dot > 0 ? key.Substring(dot + 1) : key;
            if (l == null) return "no place";
            List<string> no = new List<string>();
            foreach (KeyValuePair<string, string> t in l.Touches)
            {
                if (t.Key != name) continue;
                foreach (Cond c in StoryCond.Parse(t.Value, null)) if (!StoryCond.All(p, new List<Cond> { c })) no.Add(c.Text);
                return no.Count > 0 ? "fails " + string.Join(", ", no.ToArray()) : "its conditions hold now, it has not been looked at yet";
            }
            return "no «touch:» line for it";
        }

        // the personal quest whose stage plays this scene, set on that stage (null: none, or it stands there already)
        private static string OnItsStage(Player p, string scene)
        {
            foreach (StoryQuests.QuestDef q in StoryQuests.Quests)
            {
                if (q.Server) continue; // (the world's: «test:» lines)
                foreach (StoryQuests.StageDef st in q.Stages)
                {
                    if (st.Scene != scene) continue;
                    if (Story.StageOf(p, q.Id) == st.Id && Story.Quest(p, q.Id) == Story.Active) return null;
                    StoryQuests.Jump(p, q, st.Id);
                    return q.Id + "." + st.Id;
                }
            }
            return null;
        }

        internal static float IdleShotGap = 5f; // s between the step frames of a body none of our clips holds (24 frames went on a walk and arms at rest, 3 were left for the book: echo 2026-11-07)
        internal static float TouchReach = 0.9f, TouchStand = 1.0f; // (of the game's own reach for E: within it as a player stands; m from the piece the check walks him to)
        internal const int StepFrame = 960; // (px wide: the frame each step begins on, four a sheet, two a row)
        private static void SceneReport(string name, string id, StoryScenes.SceneDef s, int from, float far, float t0, int sheets, int stepSheets, bool ended, string lastStep, float heldFor, float blackMost, string blackStep, int helds, float hudOn, float camIn, float most, Dictionary<Character, Body> bodies, StringBuilder rows, float birdOn, float birdOff, float stepLong, string longStep, float keys, string prepared, int stopsWas, int latesWas, List<string> touched, List<StepRow> steps)
        {
            StringBuilder o = new StringBuilder();
            o.Append("scene ").Append(id).Append(" from step ").Append(from + 1).Append(far >= 0f ? ", begun " + far.ToString("0.0", CultureInfo.InvariantCulture) + " m from «" + s.Place + "»" : "").Append(", ").Append((Time.time - t0).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, ").Append(stepSheets).Append(" step sheet(s) ").Append(name).Append("_step_0… (the frame each step begins on, big, labelled), ").Append(sheets).Append(" screen sheet(s) ").Append(name).Append("_0…, close: ").Append(name).Append("_<who>_front|side|holdr|holdl (files: below)\n");
            List<string> bad = new List<string>();
            if (!ended) bad.Add("THE CHECK ITSELF BROKE at «" + lastStep + "» (studio/out/log.txt): this report stops there");
            if (prepared != null) bad.Add("its world not set up as written: " + prepared);
            if (keys > 0.3f) bad.Add("the player was at the keys " + keys.ToString("0.0", CultureInfo.InvariantCulture) + " s while the scene left them to him: the player's own numbers are his, not the scene's — run it again hands off");
            foreach (string e in StoryScenes.Errors) if (e.IndexOf(s.From, System.StringComparison.Ordinal) >= 0) bad.Add("a line of its file not read, so not played: " + e);
            foreach (string e in StoryQuests.Errors) bad.Add("a line of the quests not read: " + e);
            int stops = StoryScenes.CountOf(StoryScenes.StopsOf, id) - stopsWas, lates = StoryScenes.CountOf(StoryScenes.LatesOf, id) - latesWas;
            if (stops > 0) bad.Add("STOPPED before its end: " + StoryScenes.LastStopOf[id]);
            if (lates > 0) bad.Add(lates + " step(s) cut off at their time limit, so what they waited for never came (last: «" + StoryScenes.LastLateOf[id] + "»)");
            foreach (string tch in touched) if (tch.Contains("NOT") || tch.Contains("could not")) bad.Add("touch: " + tch);
            if (StoryScenes.Playing == s.Id && ended) bad.Add("NOT DONE in " + most.ToString("0") + " s (at " + lastStep + ")");
            if (heldFor > 1f) bad.Add("held " + heldFor.ToString("0") + " s for the author's «Далі» (step by step is on in the panel)");
            if (blackMost > SceneBlack) bad.Add("the picture black " + blackMost.ToString("0.0") + " s in one go (to " + blackStep + ")");
            if (StoryWalk.Helds > helds) bad.Add("a walk gained no way and was given up " + (StoryWalk.Helds - helds) + " time(s): see «player go: HELD» in the log");
            if (hudOn > HudMost) bad.Add("the game's screen (map, status) on for " + hudOn.ToString("0.0") + " s while a cutscene has the player");
            if (birdOff > BirdOffMost && birdOff > birdOn) bad.Add("the scene's raven out of the picture " + birdOff.ToString("0.0") + " s of the shots (in it " + birdOn.ToString("0.0") + " s)");
            if (stepLong > StepLong) bad.Add("one step held the shot " + stepLong.ToString("0.0") + " s («" + longStep + "»): is anything happening on screen?");
            if (camIn > CamInMost) bad.Add("the camera inside something for " + camIn.ToString("0.0") + " s");
            foreach (string e in _sceneErrors) bad.Add("log " + e);
            Loc at = s.Place.Length > 0 ? Locs.Get(s.Place) : null;
            if (at != null && at.Built && at.FrameOffBy < 0f) bad.Add("its place «" + at.Id + "»: its frame not checked in this run (no piece of it loaded, or its origin not written yet): shots and named spots unconfirmed");
            if (at != null && at.FrameOffBy > Loc.FrameOff) bad.Add("its place «" + at.Id + "» stands off its own frame by " + at.FrameOffBy.ToString("0.00", CultureInfo.InvariantCulture) + " m: every shot and named spot of it is off by that (Loc.CheckFrame) — mend the place, not the scene");
            foreach (Body b in bodies.Values)
            {
                if (b.Swaps > 0) bad.Add(b.Who + ": FLICKER, its look keeps changing, " + b.Swaps + " times (first: " + b.SwapStep + ")");
                if (b.Who != "player" && b.OffScreen > 1f && b.OffScreen > b.OnScreen * 0.5f) bad.Add(b.Who + ": out of the picture " + b.OffScreen.ToString("0.0") + " s of the shots (in it " + b.OnScreen.ToString("0.0") + " s)");
                if (b.Blocked > BlockedMost && b.Blocked > b.OnScreen * 0.3f) bad.Add(b.Who + ": something stands between it and the camera for " + b.Blocked.ToString("0.0") + " s of the " + b.OnScreen.ToString("0.0") + " s it is in the picture");
                if (b.Pose != null) b.Pose.Verdict(bad, b.Who);
                if (b.Dropped > 0) bad.Add(b.Who + ": " + b.Dropped + " close frame(s) not taken, past " + BodyFrames + ": the moments after them are not filmed");
            }
            StringBuilder asked = new StringBuilder();
            foreach (Body b in bodies.Values) { string w = b.Pose != null ? b.Pose.WantsSaid(bad) : ""; if (w.Length > 0) asked.Append(b.Who).Append(", ").Append(w); }
            List<string> wrote = new List<string>(); // (the files of this run, by name: anim.js shows these, never an older one left with a like name)
            for (int i = 0; i < stepSheets; i++) wrote.Add(name + "_step_" + i + ".png");
            for (int i = 0; i < sheets; i++) wrote.Add(name + "_" + i + ".png");
            foreach (Body b in bodies.Values) if (b.Shot > 0 && b.Cams != null) foreach (string v in b.Cams.Views) wrote.Add(name + "_" + b.Who + "_" + v + ".png");
            o.Append("files: ").Append(string.Join(" ", wrote.ToArray())).Append('\n');
            o.Append("VERDICT:").Append(bad.Count == 0 ? " OK (no mistake made before; the frames still judge it)\n" : "\n");
            foreach (string l in bad) o.Append("  - ").Append(l).Append('\n');
            // the steps: when each began, how long it took, what came of it (the director's own word as it ended), the VERDICT's lines that fall in its time
            o.Append("steps (began → ended, what came of it; the VERDICT's lines of that time under each):\n");
            foreach (StepRow r in steps)
            {
                string came; StoryScenes.Outcomes.TryGetValue(r.Index, out came);
                o.Append("  ").Append(r.T0.ToString("0.0", CultureInfo.InvariantCulture)).Append(" → ").Append(r.T1 >= 0f ? r.T1.ToString("0.0", CultureInfo.InvariantCulture) : "end").Append(" s  ").Append(r.Line).Append(came != null ? "  — " + came : "  — (not ended in the run)").Append('\n');
                foreach (string l in bad) { float when = AtOf(l); if (when >= r.T0 && (r.T1 < 0f || when < r.T1)) o.Append("      - ").Append(l).Append('\n'); }
            }
            o.Append(asked);
            foreach (Body b in bodies.Values)
            {
                o.Append(b.Who).Append(": in shots on screen ").Append(b.OnScreen.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, off ").Append(b.OffScreen.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, blocked ").Append(b.Blocked.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s, about ").Append(b.SizeN > 0f ? Mathf.RoundToInt(100f * b.SizeSum / b.SizeN).ToString() : "-").Append("% of the picture's height, look kept changing ").Append(b.Swaps).Append(" times");
                if (b.Pose != null) o.Append("; ").Append(b.Pose.Numbers()).Append('\n').Append("  ").Append(b.Pose.Stance).Append('\n').Append(b.Pose.TempoSaid());
                else o.Append('\n');
            }
            o.Append("\nsteps and frames:\n").Append(rows);
            File.WriteAllText(Path.Combine(Out, Clean(name) + ".txt"), o.ToString(), new UTF8Encoding(false));
        }

        private sealed class StepRow { public int Index; public string Line; public float T0, T1 = -1f; } // (a step of the run: its index in the scene, its words, when it began and ended)
        private static readonly Regex AtRe = new Regex(@"\bat (\d+(?:\.\d+)?) s\b", RegexOptions.Compiled);
        // the moment a VERDICT line names («… at 12.3 s …»), below zero none
        private static float AtOf(string line) { Match m = AtRe.Match(line); float v; return m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : -1f; }

        private static void SaveSheet(string name, List<Texture2D> frames, int cols = 4)
        {
            int n = frames.Count, w = frames[0].width, h = frames[0].height, rows = (n + cols - 1) / cols;
            Texture2D sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
            Fill(sheet, new Color32(20, 20, 20, 255));
            for (int i = 0; i < n; i++) { Blit(frames[i], sheet, (i % cols) * w, (rows - 1 - i / cols) * h); Object.Destroy(frames[i]); }
            Save(name, sheet);
            Object.Destroy(sheet);
            frames.Clear();
        }
    }
}
#endif

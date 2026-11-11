#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine;

namespace HuginWhispers
{
    // «Хто смикнув» (the user 2026-11-06: people and the player jerk, on a bench, walking, standing; 2.8.1 had none of it): the author's
    // tool that says WHICH PART OF A FRAME put a jerk in. Every body near the camera (you, other players, people) is read at seven points
    // of one frame, only read, nothing is changed:
    //   start   before any Update          the place physics left it in (and the rigidbody's smoothing)
    //   update  first in LateUpdate        + the game's Update, ZSync, the animator
    //   clips   after BodyAnimator (-1000) + a player's own clips
    //   gait    after StoryGait (-900)     + a player's scene gait
    //   late    before NpcPose (10000)     + the game's LateUpdate (attach, look-at)
    //   pose    after NpcPose              + a person's clips, gait, turn, fall, meal
    //   end     last                       + whatever comes after the pose
    // A motion that goes on evenly gives each part the same change frame after frame, so a jerk is the frame whose motion breaks from the
    // one before, and its part is the one whose change broke most. Kinds: root (the body's place), yaw (its turn), pose (head, hips and hands
    // in the body's own frame), view (your head as the camera sees it: with no root or pose jerk that frame it is the camera).
    // Each jerk comes with the frame's own state, because a long frame, a garbage collection or a frame with no physics step moves
    // everybody at once and is no fault of a body's: dt, GC, physics steps, who runs the body, sitting, what it was doing.
    //   raven jolt on | off            start and stop; while off nothing of this exists in the game (no cost, nothing to distort)
    //   raven jolt report              the counts, into the log and jolt/report.txt; the jerks one by one in jolt/log.txt
    //   raven jolt mark                (or Num1) «I saw it now»: the last seconds of every body, frame by frame, jolt/mark_<time>.txt
    //   raven jolt clear               the counts from zero
    //   raven jolt mute clips|gait|turn|none   a person's layer off, to compare (this game, till a restart)
    internal static class JoltWatch
    {
        internal const int Root = 0, Yaw = 1, Pose = 2, View = 3, Kinds = 4, Points = 7, RingLength = 480, MostBodies = 16, MostLines = 2000;
        internal static float Near = 40f, RootBreak = 3f, RootLeast = 0.03f, YawBreak = 200f, YawLeast = 4f, PoseLeast = 0.04f, PoseBreak = 3f, ViewLeast = 0.03f, ViewBreak = 3f, HitchAt = 0.033f, MarkSeconds = 5f;
        internal static KeyCode MarkKey = KeyCode.Keypad1;
        private static readonly string[] Kind = { "root", "yaw", "pose", "view" };
        private static readonly string[] Parts = { "physics, before Update", "game Update + animator", "player's clips", "scene gait", "game LateUpdate", "NpcPose", "after the pose" };

        private static readonly string[] Named = { "Head", "Hips", "LeftHand", "RightHand" };
        private struct Sample { public Vector3 Root, Head, Hips, HandL, HandR; public float Yaw; }
        private struct Rec { public float T, Dt, Speed, RootStep, YawStep, HeadStep, ViewStep, Fwd, Side, Up, RbStep, RbSpeed, Gap, Nearest; public byte Physics, Mask; public sbyte PartRoot, PartPose, Bone; public bool Gc; }

        private sealed class Body
        {
            public Character C; public Transform T, Head, Hips, HandL, HandR; public Rigidbody Rb; public NpcActor Actor; public Animator Anim; public string Name = "";
            public readonly Sample[] Now = new Sample[Points];
            public Sample End;
            public readonly Vector3[] DRoot = new Vector3[Points], DRootWas = new Vector3[Points];
            public readonly Vector3[][] DBone = { new Vector3[Points], new Vector3[Points], new Vector3[Points], new Vector3[Points] }, DBoneWas = { new Vector3[Points], new Vector3[Points], new Vector3[Points], new Vector3[Points] };
            public Vector3 VWas, ViewWas;
            public float YawRateWas, PaceHead, PaceView, Gap; public Vector3 RbWas;
            public readonly Vector3[] FixPos = new Vector3[FixKept], FixVel = new Vector3[FixKept], FixWant = new Vector3[FixKept];
            public readonly bool[] FixGround = new bool[FixKept], FixAttached = new bool[FixKept], FixHeld = new bool[FixKept], FixMove = new bool[FixKept];
            public readonly Collider[] FixOn = new Collider[FixKept];
            public int FixAt; public float FixLogged;
            public bool Set, SetTwice, Seen;
            public readonly Rec[] Ring = new Rec[RingLength];
            public int RingAt, RingCount;
            public readonly int[,] Count = new int[Kinds, Points];
            public readonly int[,] ByPhysics = new int[Kinds, 3];
            public readonly int[] Hitch = new int[Kinds], WithGc = new int[Kinds];
        }

        private static GameObject _host;
        private static readonly List<Body> Bodies = new List<Body>();
        private static readonly Dictionary<Character, Body> Known = new Dictionary<Character, Body>();
        private static readonly List<Character> Around = new List<Character>();
        private static readonly StringBuilder Lines = new StringBuilder();
        private static int _lines, _physics, _physicsNow, _gcSeen, _frames, _sharedFrames, _sharedHitch, _gcFrames;
        private static bool _gcNow, _asked;
        private const int FixKept = 8; // physics steps kept per body: shown under a whole-body jerk
        private static float _since, _nextLook, _worst, _worstGc;
        private static readonly int[] DtBins = new int[5], PhysicsBins = new int[4];

        internal static bool On { get { return _host != null; } }
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "jolt"); } }

        internal static string Cmd(string[] a)
        {
            string w = a.Length > 1 ? a[1] : "";
            switch (w)
            {
                case "on":
                    if (On) return "Уже міряю.";
                    Clear();
                    _host = new GameObject("RavenWatch_JoltWatch");
                    UnityEngine.Object.DontDestroyOnLoad(_host);
                    _host.AddComponent<JoltStart>(); _host.AddComponent<JoltClips>(); _host.AddComponent<JoltGait>(); _host.AddComponent<JoltLate>(); _host.AddComponent<JoltPose>(); _host.AddComponent<JoltEnd>();
                    return "Міряю. Побачив ривок: Num1 або raven jolt mark. Підсумок: raven jolt report.";
                case "off":
                    if (!On) return "Не міряю.";
                    string last = Report();
                    UnityEngine.Object.Destroy(_host); _host = null; Bodies.Clear(); Known.Clear();
                    NpcPose.MuteClips = NpcPose.MuteGait = NpcPose.MuteTurn = NpcPose.MuteSpeech = false; // (a layer muted for the test comes back with the tool off)
                    return last;
                case "report": return On ? Report() : "Не міряю: raven jolt on.";
                case "clear": Clear(); return "З нуля.";
                case "mark": _asked = true; return On ? "Зберігаю останні секунди." : "Не міряю: raven jolt on.";
                case "mute":
                    string m = a.Length > 2 ? a[2] : "";
                    NpcPose.MuteClips = m == "clips"; NpcPose.MuteGait = m == "gait"; NpcPose.MuteTurn = m == "turn"; NpcPose.MuteSpeech = m == "speech";
                    return "Вимкнено в людей: " + (m == "clips" || m == "gait" || m == "turn" || m == "speech" ? m : "нічого") + ".";
                default: return "raven jolt on|off|report|mark|clear|mute clips|gait|turn|speech|none";
            }
        }

        private static void Clear()
        {
            foreach (Body b in Bodies) { Array.Clear(b.Count, 0, b.Count.Length); Array.Clear(b.ByPhysics, 0, b.ByPhysics.Length); Array.Clear(b.Hitch, 0, Kinds); Array.Clear(b.WithGc, 0, Kinds); b.RingCount = 0; }
            Lines.Length = 0; _lines = 0; _frames = _sharedFrames = _sharedHitch = _gcFrames = 0; _worst = _worstGc = 0f; _since = Time.time;
            Array.Clear(DtBins, 0, DtBins.Length); Array.Clear(PhysicsBins, 0, PhysicsBins.Length);
            _gcSeen = GC.CollectionCount(0);
        }

        // ---------------------------------------------------------------- who is looked at
        private static void LookRound()
        {
            _nextLook = Time.time + 1f;
            Camera cam = Utils.GetMainCamera();
            Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
            Around.Clear();
            foreach (Player p in Player.GetAllPlayers()) if (p != null) Around.Add(p);
            foreach (NpcActor x in NpcActor.Live) if (x != null) { Character c = x.GetComponent<Character>(); if (c != null) Around.Add(c); } // (once a second, a handful)
            Around.Sort((p, q) => (p.transform.position - from).sqrMagnitude.CompareTo((q.transform.position - from).sqrMagnitude));
            foreach (Body b in Bodies) b.Seen = false;
            int n = 0;
            foreach (Character c in Around)
            {
                if (n >= MostBodies || (c.transform.position - from).sqrMagnitude > Near * Near) break;
                Body b;
                if (!Known.TryGetValue(c, out b)) { b = Make(c); if (b == null) continue; Known[c] = b; Bodies.Add(b); }
                b.Seen = true; n++;
            }
            for (int i = Bodies.Count - 1; i >= 0; i--) if (Bodies[i].C == null) { Bodies.RemoveAt(i); }
            List<Character> gone = null;
            foreach (KeyValuePair<Character, Body> k in Known) if (k.Key == null) { if (gone == null) gone = new List<Character>(); gone.Add(k.Key); }
            if (gone != null) foreach (Character c in gone) Known.Remove(c);
        }

        private static Body Make(Character c)
        {
            Animator anim = c.GetComponentInChildren<Animator>();
            if (anim == null) return null;
            Body b = new Body { C = c, T = c.transform, Anim = anim, Rb = c.GetComponent<Rigidbody>(), Actor = c.GetComponent<NpcActor>() };
            b.Head = BodyClips.SkeletonBone(c, anim.transform, "Head");
            b.Hips = BodyClips.SkeletonBone(c, anim.transform, "Hips");
            b.HandL = BodyClips.SkeletonBone(c, anim.transform, "LeftHand");
            b.HandR = BodyClips.SkeletonBone(c, anim.transform, "RightHand");
            b.Name = b.Actor != null ? b.Actor.Name : c is Player ? ((Player)c).GetPlayerName() + (c == Player.m_localPlayer ? " (ти)" : "") : c.name;
            Say(b.Name + ": body rb interpolation " + (b.Rb != null ? b.Rb.interpolation.ToString() : "-") + ", animator " + anim.updateMode + "/" + anim.cullingMode + ", root motion " + anim.applyRootMotion);
            return b;
        }

        // ---------------------------------------------------------------- the frame
        // after each physics step, each body: where it is, how fast, what it wants, what it stands on (a jerk shows the last steps: only while the tool is on)
        internal static void FixedDone()
        {
            _physicsNow++;
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.C == null || b.Rb == null || !b.Seen) continue;
                int n = b.FixAt; b.FixAt = (n + 1) % FixKept;
                b.FixPos[n] = b.Rb.position; b.FixVel[n] = b.Rb.linearVelocity; b.FixWant[n] = b.C.GetMoveDir(); b.FixGround[n] = b.C.IsOnGround(); b.FixAttached[n] = b.C.IsAttached(); b.FixHeld[n] = NpcBody.Held.ContainsKey(b.C); b.FixMove[n] = b.C.CanMove(); b.FixOn[n] = b.C.GetLastGroundCollider(); // (the game's own getters: no boxing, no garbage that would show in the GC count)
            }
        }

        private static void Steps(Body b)
        {
            if (Time.time < b.FixLogged || _lines >= MostLines) return;
            b.FixLogged = Time.time + 0.5f;
            for (int k = 1; k < FixKept; k++) // (from the second oldest: the oldest has no step before it in the ring)
            {
                int n = (b.FixAt + k) % FixKept, was = (n + FixKept - 1) % FixKept;
                Collider on = b.FixOn[n];
                Lines.Append("\t\tstep ").Append(((b.FixPos[n] - b.FixPos[was]).magnitude).ToString("0.000", CultureInfo.InvariantCulture)).Append(" · rb ").Append(b.FixVel[n].ToString("F2")).Append(" · want ").Append(b.FixWant[n].ToString("F2")).Append(b.FixGround[n] ? " · ground " : " · AIR ").Append(b.FixAttached[n] ? " · ATTACHED" : "").Append(b.FixHeld[n] ? " · HELD" : "").Append(b.FixMove[n] ? "" : " · CANT MOVE").Append(" · ").Append(on != null ? on.name + " [" + LayerMask.LayerToName(on.gameObject.layer) + "]" : "-").Append('\n');
                _lines++;
            }
        }

        internal static void FrameStart()
        {
            _physics = _physicsNow; _physicsNow = 0;
            int gc = GC.CollectionCount(0);
            _gcNow = gc != _gcSeen; _gcSeen = gc;
            if (Time.time >= _nextLook) LookRound();
            Read(0);
            for (int i = 0; i < Bodies.Count; i++) { Body b = Bodies[i]; if (b.C != null && b.Rb != null) b.Gap = (b.T.position - b.Rb.position).magnitude; } // (what the rigidbody's smoothing has between its steps: none while it moves means the smoothing is off)
        }

        internal static void Read(int point)
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                Body b = Bodies[i];
                if (b.C == null || b.Head == null || b.Hips == null || b.HandL == null || b.HandR == null) continue;
                Vector3 p = b.T.position; Quaternion r = Quaternion.Inverse(b.T.rotation);
                b.Now[point] = new Sample { Root = p, Yaw = b.T.eulerAngles.y, Head = r * (b.Head.position - p), Hips = r * (b.Hips.position - p), HandL = r * (b.HandL.position - p), HandR = r * (b.HandR.position - p) };
            }
        }

        internal static void FrameEnd()
        {
            Read(Points - 1);
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _frames++;
            DtBins[dt < 0.02f ? 0 : dt < HitchAt ? 1 : dt < 0.05f ? 2 : dt < 0.1f ? 3 : 4]++;
            PhysicsBins[Mathf.Min(_physics, 3)]++;
            bool hitch = dt >= HitchAt;
            
            if (dt > _worst) _worst = dt;
            if (_gcNow) { _gcFrames++; if (dt > _worstGc) _worstGc = dt; }
            Camera cam = Utils.GetMainCamera();
            int jolted = 0;
            for (int i = 0; i < Bodies.Count; i++) if (Measure(Bodies[i], dt, hitch, cam)) jolted++;
            if (jolted >= 2) { _sharedFrames++; if (hitch || _gcNow) _sharedHitch++; }
            if (_asked || Input.GetKeyDown(MarkKey)) { _asked = false; Mark(); }
        }

        private static bool Measure(Body b, float dt, bool hitch, Camera cam)
        {
            if (b.C == null || b.Head == null || b.Hips == null || b.HandL == null || b.HandR == null || !b.Seen) { b.Set = false; b.SetTwice = false; return false; }
            Sample[] s = b.Now;
            Sample last = s[Points - 1];
            if (!b.Set) { b.End = last; b.Set = true; b.SetTwice = false; return false; }
            for (int i = 0; i < Points; i++)
            {
                b.DRoot[i] = s[i].Root - (i == 0 ? b.End.Root : s[i - 1].Root);
                b.DBone[0][i] = s[i].Head - (i == 0 ? b.End.Head : s[i - 1].Head);
                b.DBone[1][i] = s[i].Hips - (i == 0 ? b.End.Hips : s[i - 1].Hips);
                b.DBone[2][i] = s[i].HandL - (i == 0 ? b.End.HandL : s[i - 1].HandL);
                b.DBone[3][i] = s[i].HandR - (i == 0 ? b.End.HandR : s[i - 1].HandR);
            }
            Vector3 step = last.Root - b.End.Root, v = step / dt;
            float yawStep = Mathf.DeltaAngle(b.End.Yaw, last.Yaw), yawRate = yawStep / dt;
            // the bone that moved most; hands swing fast in a walk, so each is judged by the same break from the pace of late
            float poseStep = (last.Head - b.End.Head).magnitude; int bone = 0;
            float step1 = (last.Hips - b.End.Hips).magnitude; if (step1 > poseStep) { poseStep = step1; bone = 1; }
            step1 = (last.HandL - b.End.HandL).magnitude; if (step1 > poseStep) { poseStep = step1; bone = 2; }
            step1 = (last.HandR - b.End.HandR).magnitude; if (step1 > poseStep) { poseStep = step1; bone = 3; }
            float frames = Mathf.Max(1f, dt * 60f);
            byte mask = 0; sbyte partRoot = -1, partPose = -1;
            if (b.SetTwice)
            {
                if ((v - b.VWas).magnitude > RootBreak && (step - b.VWas * dt).magnitude > RootLeast) { mask |= 1; partRoot = Worst(b.DRoot, b.DRootWas); }
                if (Mathf.Abs(yawRate - b.YawRateWas) > YawBreak && Mathf.Abs(yawStep - b.YawRateWas * dt) > YawLeast) mask |= 2;
                if (poseStep > PoseLeast * frames && poseStep > b.PaceHead * PoseBreak) { mask |= 4; partPose = Worst(b.DBone[bone], b.DBoneWas[bone]); }
            }
            float viewStep = 0f;
            if (b.C == Player.m_localPlayer && cam != null)
            {
                Vector3 view = cam.transform.InverseTransformPoint(b.Head.position);
                viewStep = (view - b.ViewWas).magnitude;
                if (b.SetTwice && viewStep > ViewLeast * frames && viewStep > b.PaceView * ViewBreak && (mask & 5) == 0) mask |= 8;
                b.ViewWas = view;
                b.PaceView = Mathf.Lerp(b.PaceView, viewStep, 0.2f);
            }
            b.PaceHead = Mathf.Lerp(b.PaceHead, poseStep, 0.2f);
            Vector3 local = Quaternion.Inverse(b.T.rotation) * v;
            Vector3 rbAt = b.Rb != null ? b.Rb.position : b.T.position;
            Rec r = new Rec { Fwd = local.z, Side = local.x, Up = local.y, RbStep = (rbAt - b.RbWas).magnitude, RbSpeed = b.Rb != null ? b.Rb.linearVelocity.magnitude : 0f, Gap = b.Gap, Nearest = mask != 0 ? Nearest(b) : -1f, Bone = (sbyte)bone, T = Time.time, Dt = dt, Speed = v.magnitude, RootStep = step.magnitude, YawStep = yawStep, HeadStep = poseStep, ViewStep = viewStep, Physics = (byte)Mathf.Min(_physics, 255), Mask = mask, PartRoot = partRoot, PartPose = partPose, Gc = _gcNow };
            b.RbWas = rbAt; // (the body itself between two frames: it jumps too = physics; only the picture jumps = something wrote the transform)
            b.Ring[b.RingAt] = r;
            b.RingAt = (b.RingAt + 1) % RingLength; if (b.RingCount < RingLength) b.RingCount++;
            if (mask != 0)
            {
                if ((mask & 1) != 0) { Count(b, Root, partRoot, hitch, (step - b.VWas * dt).magnitude, r); Steps(b); }
                if ((mask & 2) != 0) Count(b, Yaw, -1, hitch, yawStep, r);
                if ((mask & 4) != 0) Count(b, Pose, partPose, hitch, poseStep, r);
                if ((mask & 8) != 0) Count(b, View, -1, hitch, viewStep, r);
            }
            Array.Copy(b.DRoot, b.DRootWas, Points);
            for (int k = 0; k < Named.Length; k++) Array.Copy(b.DBone[k], b.DBoneWas[k], Points);
            b.End = last; b.VWas = v; b.YawRateWas = yawRate; b.SetTwice = true;
            return mask != 0;
        }

        // the nearest other body, when a jerk asks: pushing against someone
        private static float Nearest(Body b)
        {
            float best = -1f;
            foreach (Body o in Bodies) { if (o == b || o.C == null) continue; float d = (o.T.position - b.T.position).magnitude; if (best < 0f || d < best) best = d; }
            return best;
        }

        private static sbyte Worst(Vector3[] now, Vector3[] was)
        {
            int best = 0; float most = -1f;
            for (int i = 0; i < Points; i++) { float d = (now[i] - was[i]).sqrMagnitude; if (d > most) { most = d; best = i; } }
            return (sbyte)best;
        }

        private static void Count(Body b, int kind, int part, bool hitch, float size, Rec r)
        {
            if (part >= 0) b.Count[kind, part]++;
            b.ByPhysics[kind, Mathf.Min(_physics, 2)]++;
            if (hitch) b.Hitch[kind]++;
            if (_gcNow) b.WithGc[kind]++;
            if (_lines >= MostLines) return;
            _lines++;
            BodyAnimator clips = BodyAnimator.Of(b.C);
            string clip = clips != null && b.Actor != null ? clips.ClipIn(NpcBody.ClipSlot) : null;
            Lines.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append('\t').Append(b.Name).Append('\t').Append(Kind[kind]).Append(kind == Pose ? " " + Named[r.Bone] : "").Append('\t').Append(size.ToString("0.000", CultureInfo.InvariantCulture))
                .Append('\t').Append(part >= 0 ? Parts[part] : "-")
                .Append("\tdt ").Append((r.Dt * 1000f).ToString("0", CultureInfo.InvariantCulture)).Append(" ms · physics ").Append(_physics).Append(_gcNow ? " · GC" : "")
                .Append(" · speed ").Append(b.VWas.magnitude.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(b.Actor != null ? (b.Actor.Owned ? " · run here" : " · run elsewhere") : "").Append(b.C.IsAttached() ? " · attached" : "").Append(b.C.InEmote() ? " · emote" : "").Append(b.C.IsOnGround() ? "" : " · off ground")
                .Append(b.Rb != null && b.Rb.isKinematic ? " · kinematic" : "").Append(" · step fwd ").Append(r.Fwd.ToString("0.0", CultureInfo.InvariantCulture)).Append(" side ").Append(r.Side.ToString("0.0", CultureInfo.InvariantCulture)).Append(" up ").Append(r.Up.ToString("0.0", CultureInfo.InvariantCulture)).Append(" · rb step ").Append(r.RbStep.ToString("0.000", CultureInfo.InvariantCulture)).Append(" · rb ").Append(r.RbSpeed.ToString("0.0", CultureInfo.InvariantCulture)).Append(" · gap ").Append(r.Gap.ToString("0.000", CultureInfo.InvariantCulture)).Append(" · nearest ").Append(r.Nearest.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(clip != null ? " · clip " + clip : "").Append(b.Actor != null ? " · " + b.Actor.StatusNow : "").Append('\n');
        }

        // ---------------------------------------------------------------- what was found
        private static void Say(string line) { if (_lines < MostLines) { _lines++; Lines.Append(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append('\t').Append(line).Append('\n'); } }

        private static void Save(string file, string text)
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, file), text); }
            catch (Exception e) { Npcs.Log("jolt: could not write " + file + ": " + e.Message); }
        }

        private static string Report()
        {
            float span = Mathf.Max(0.01f, Time.time - _since);
            StringBuilder t = new StringBuilder();
            t.Append("Хто смикнув: ").Append(span.ToString("0", CultureInfo.InvariantCulture)).Append(" с, кадрів ").Append(_frames).Append(", FPS ").Append((_frames / span).ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            t.Append("Кадри, мс: <20 ").Append(DtBins[0]).Append(" · <33 ").Append(DtBins[1]).Append(" · <50 ").Append(DtBins[2]).Append(" · <100 ").Append(DtBins[3]).Append(" · довші ").Append(DtBins[4]).Append(" · найдовший ").Append((_worst * 1000f).ToString("0", CultureInfo.InvariantCulture)).Append('\n');
            t.Append("Прибирання пам'яті (GC): кадрів ").Append(_gcFrames).Append(", найдовший із них ").Append((_worstGc * 1000f).ToString("0", CultureInfo.InvariantCulture)).Append(" мс\n");
            t.Append("Кроків фізики за кадр: 0 — ").Append(PhysicsBins[0]).Append(" · 1 — ").Append(PhysicsBins[1]).Append(" · 2 — ").Append(PhysicsBins[2]).Append(" · 3+ — ").Append(PhysicsBins[3]).Append('\n');
            if (_lines >= MostLines) t.Append("Журнал ривків повний (").Append(MostLines).Append(" рядків): raven jolt clear\n");
            t.Append("Кадрів, де смикнуло двох і більше: ").Append(_sharedFrames).Append(", з них у довгому кадрі чи GC ").Append(_sharedHitch).Append('\n');
            foreach (Body b in Bodies)
            {
                t.Append(b.Name).Append(':');
                bool any = false;
                for (int k = 0; k < Kinds; k++)
                {
                    int n = b.ByPhysics[k, 0] + b.ByPhysics[k, 1] + b.ByPhysics[k, 2];
                    if (n == 0) continue;
                    any = true;
                    t.Append("\n  ").Append(Kind[k]).Append(' ').Append(n).Append(" (").Append((n * 60f / span).ToString("0.0", CultureInfo.InvariantCulture)).Append("/хв): фізики 0/1/2+ ").Append(b.ByPhysics[k, 0]).Append('/').Append(b.ByPhysics[k, 1]).Append('/').Append(b.ByPhysics[k, 2]).Append(", довгий кадр ").Append(b.Hitch[k]).Append(", GC ").Append(b.WithGc[k]);
                    for (int p = 0; p < Points; p++) if (b.Count[k, p] > 0) t.Append(" · ").Append(Parts[p]).Append(' ').Append(b.Count[k, p]);
                }
                if (!any) t.Append(" чисто");
                t.Append('\n');
            }
            string text = t.ToString();
            Npcs.Log("[jolt]\n" + text);
            Save("report.txt", text);
            Save("log.txt", Lines.ToString());
            return text;
        }

        private static void Mark()
        {
            StringBuilder t = new StringBuilder("Побачено о ").Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append("; час: секунд до позначки\n");
            float now = Time.time;
            foreach (Body b in Bodies)
            {
                t.Append("\n== ").Append(b.Name).Append("\nсек\tмс\tфіз\tшвидк\tвперед\tвбік\tвгору\tкрок rb\trb\tрозрив\tкрок\tповорот\tголова\tкамера\tщо\n");
                for (int i = 0; i < b.RingCount; i++)
                {
                    Rec r = b.Ring[(b.RingAt - b.RingCount + i + RingLength) % RingLength];
                    if (now - r.T > MarkSeconds) continue;
                    t.Append((r.T - now).ToString("0.000", CultureInfo.InvariantCulture)).Append('\t').Append((r.Dt * 1000f).ToString("0", CultureInfo.InvariantCulture)).Append('\t').Append(r.Physics).Append('\t').Append(r.Speed.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t').Append(r.Fwd.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t').Append(r.Side.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t').Append(r.Up.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t').Append(r.RbStep.ToString("0.000", CultureInfo.InvariantCulture)).Append('\t').Append(r.RbSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append('\t').Append(r.Gap.ToString("0.000", CultureInfo.InvariantCulture)).Append('\t').Append(r.RootStep.ToString("0.000", CultureInfo.InvariantCulture)).Append('\t').Append(r.YawStep.ToString("0.0", CultureInfo.InvariantCulture)).Append('\t').Append(r.HeadStep.ToString("0.000", CultureInfo.InvariantCulture)).Append('\t').Append(r.ViewStep.ToString("0.000", CultureInfo.InvariantCulture)).Append('\t');
                    if (r.Gc) t.Append("GC ");
                    if ((r.Mask & 1) != 0) t.Append("ROOT[").Append(r.PartRoot >= 0 ? Parts[r.PartRoot] : "-").Append("] ");
                    if ((r.Mask & 2) != 0) t.Append("YAW ");
                    if ((r.Mask & 4) != 0) t.Append("POSE[").Append(r.PartPose >= 0 ? Parts[r.PartPose] : "-").Append("] ");
                    if ((r.Mask & 8) != 0) t.Append("VIEW ");
                    t.Append('\n');
                }
            }
            string file = "mark_" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".txt";
            Save(file, t.ToString());
            Save("log.txt", Lines.ToString());
            Npcs.Log("[jolt] saw it: " + file);
        }
    }

    // the seven points: only readers, each in its place in the frame; none exists while the tool is off
    [DefaultExecutionOrder(-32000)]
    internal sealed class JoltStart : MonoBehaviour
    {
        private static readonly WaitForFixedUpdate AfterPhysics = new WaitForFixedUpdate();
        private void OnEnable() { StartCoroutine(Steps()); }
        private IEnumerator Steps() { while (true) { yield return AfterPhysics; JoltWatch.FixedDone(); } }
        private void Update() { JoltWatch.FrameStart(); }
        private void LateUpdate() { JoltWatch.Read(1); }
    }
    [DefaultExecutionOrder(-950)] internal sealed class JoltClips : MonoBehaviour { private void LateUpdate() { JoltWatch.Read(2); } }
    [DefaultExecutionOrder(-850)] internal sealed class JoltGait : MonoBehaviour { private void LateUpdate() { JoltWatch.Read(3); } }
    [DefaultExecutionOrder(9000)] internal sealed class JoltLate : MonoBehaviour { private void LateUpdate() { JoltWatch.Read(4); } }
    [DefaultExecutionOrder(10500)] internal sealed class JoltPose : MonoBehaviour { private void LateUpdate() { JoltWatch.Read(5); } }
    [DefaultExecutionOrder(31500)] internal sealed class JoltEnd : MonoBehaviour { private void LateUpdate() { JoltWatch.FrameEnd(); } }
}
#endif

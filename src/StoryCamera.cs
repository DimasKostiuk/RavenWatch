#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The scene's camera (story-tech: GameCamera.UpdateCamera is the one place the game sets its camera each frame —
    // a prefix there owns it while a shot is on). Only this player's camera: the others near see the scene live, theirs
    // is never taken. While a shot is on, or a scene holds the bars or the black over the picture (a cutscene with no shot of its
    // own yet: TakesPlayer), the player does not move, look round or fight (PlayerController.TakeInput says no) and the
    // HUD is out of sight, and so are the names and bars over heads (StoryCameraNamesPatch); the world never pauses, and a scene that stops gives the camera back at once.
    //   Shot(from, look, blend, fov)   from a point (fixed) looking at a point that may move (followed each frame)
    //   Shot(pose, blend)              any camera that moves by itself: a shot of the author's, a move between two of
    //                                  them, an orbit (StoryShots), the author's free camera — its pose asked each frame
    //   Off(blend)                     back to the game's camera, blended from where the shot was
    //   Live                           a living camera on whatever shot is on (a scene's word «live N»; 0: still): it sways
    //                                  in step with the player's own steps and, when he stands, only breathes
    // blend 0 = a cut (at once); else eased in from wherever the camera was (the game's, or the last shot — even moving).
    // The bars and the subtitles are StoryScreen's.
    // Close up nothing vanishes while a shot is on: the near plane is ours (the game moves it only in the method a shot replaces,
    // so it stayed wherever the game's own camera had left it), and the game's rule «the camera within 2 m: don't draw him»
    // (Player.FixedUpdate, set again before each frame) is undone each frame before the picture is made.
    internal static class StoryCamera
    {
        internal static float Fov = 65f;
        // Eyes (a shot through the player's own eyes: StoryScenesCamera «camera eyes»): he is not drawn — the game's own way, as when its camera is
        // within 2 m of him (Character.SetVisible) — and neither are the ravens on his shoulder (their renderers off: they would fill a corner of
        // the picture), till another shot or the camera given back. A raven that flies off meanwhile is shown again at the shot's end.
        internal static bool Eyes;
        private static readonly object[] HiddenArgs = { false };
        private static readonly List<Renderer> _eyesHid = new List<Renderer>();
        private static void EyesHide(Player p, bool on)
        {
            if (!on) { foreach (Renderer r in _eyesHid) if (r != null) r.enabled = true; _eyesHid.Clear(); return; }
            if (_eyesHid.Count > 0 || p == null) return;
            for (int i = 0; i < 2; i++)
            {
                GameObject raven = Shoulder.ModelOf(p, i == 1);
                if (raven == null || !raven.transform.IsChildOf(p.transform)) continue; // (on his shoulder only: one in the air is in the picture by right)
                foreach (Renderer r in raven.GetComponentsInChildren<Renderer>()) if (r != null && r.enabled) { r.enabled = false; _eyesHid.Add(r); } // heavy: once a shot through his eyes
            }
        }
        internal static float NearClip = 0.05f, HideWithin = 2.5f; // m: what is nearer the lens is cut · the game's rule reaches 2 m, a little more for his steps
        private static readonly MethodInfo SetVisibleM = AccessTools.Method(typeof(Character), "SetVisible");
        private static readonly object[] VisibleArgs = { true }; // (every frame of a close shot: nothing made each time)
        private static bool _on, _leaving;
        private static Func<Pose> _pose;
        private static float _blendFrom, _blendFor;
        private static Vector3 _wasPos;
        private static Quaternion _wasRot;
        private static float _wasFov;

        internal static bool Active { get { return _on; } }
        internal static bool Leaving { get { return _on && _leaving; } } // (on its way back to the game's camera: a new shot may take it from there)
        internal static bool TakesPlayer { get { return (_on && !_leaving) || StoryScreen.Held || StoryWatch.Holds; } } // (StoryWatch: another's scene says «watchers take») (the keys are the scene's: the user 2026-11-04 fought and turned the camera in a scene with bars and no shot; easing back they are yours again: the way back follows you)

        // ---------------------------------------------------------------- the living camera
        internal static float Live; // (how much: 1 — a hand holding it)
        internal static float SwaySide = 0.045f, SwayUp = 0.03f, SwayRoll = 0.9f, StrideM = 1.5f, BreathM = 0.008f; // (m and degrees at Live 1; a stride's metres; the breath's m)
        private static float _phase, _stepping;

        // this frame's sway: its steps measured by how far he goes, so it keeps time with his feet at any pace
        private static void Sway(Player p, float dt, ref Vector3 pos, ref Quaternion rot)
        {
            if (Live <= 0f) return;
            float speed = p.GetVelocity().magnitude;
            _phase += speed * dt / StrideM * Mathf.PI; // (a stride: half a side-to-side swing)
            _stepping = Mathf.Lerp(_stepping, Mathf.Clamp01(speed / 1.2f), 1f - Mathf.Exp(-4f * dt)); // (it eases in and out as he starts and stops)
            float t = StoryClock.Now, side = Mathf.Sin(_phase) * _stepping, up = Mathf.Abs(Mathf.Sin(_phase)) * _stepping;
            Vector3 breath = new Vector3(Mathf.PerlinNoise(t * 0.5f, 3.1f) - 0.5f, Mathf.PerlinNoise(t * 0.4f, 7.7f) - 0.5f, 0f) * 2f * BreathM;
            pos += rot * (new Vector3(side * SwaySide, up * SwayUp, 0f) + breath) * Live;
            rot *= Quaternion.Euler(0f, 0f, -side * SwayRoll * Live);
        }

        private static readonly FieldInfo CamF = AccessTools.Field(typeof(GameCamera), "m_camera");
        private static readonly MethodInfo GamePose = AccessTools.Method(typeof(GameCamera), "GetCameraPosition");
        private static readonly object[] _shakeArgs = new object[1]; // (one array: called each frame of a shot)
        private static MethodInfo ShakeM = AccessTools.Method(typeof(GameCamera), "UpdateCameraShake");

        // where the camera is, where it looks, how wide (fov 0: the shot's default)
        internal struct Pose { public Vector3 At; public Quaternion Rot; public float Fov; }

        internal static void Shot(Func<Vector3> from, Func<Vector3> look, float blend, float fov)
        {
            if (from == null || look == null) return;
            float f = fov;
            Shot(() =>
            {
                GameCamera gc = GameCamera.instance;
                Vector3 to = look(), at = gc != null ? Clear(gc, to, from()) : from();
                Vector3 d = to - at;
                return new Pose { At = at, Rot = d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity, Fov = f };
            }, blend);
        }

        internal static void Shot(Func<Pose> pose, float blend)
        {
            GameCamera gc = GameCamera.instance;
            if (gc == null || pose == null) return;
            Note(gc); // (the blend starts from wherever the camera is now: the game's or the last shot)
            _pose = pose;
            _blendFrom = StoryClock.Now; _blendFor = Mathf.Max(0f, blend);
            _on = true; _leaving = false;
            Eyes = false; // (a shot through his eyes says so after this: StoryScenesCamera)
        }

        // the camera is driven by this pose now (the author's free camera: flying or not)
        internal static bool Driving(Func<Pose> pose) { return _on && !_leaving && _pose != null && pose != null && _pose.Equals(pose); }

        // where the camera is now, as a pose (the author's window: a shot taken from it)
        internal static Pose Now()
        {
            GameCamera gc = GameCamera.instance;
            if (gc == null) return new Pose { Rot = Quaternion.identity, Fov = Fov };
            Camera c = CamF != null ? CamF.GetValue(gc) as Camera : null;
            return new Pose { At = gc.transform.position, Rot = gc.transform.rotation, Fov = c != null ? c.fieldOfView : Fov };
        }

        internal static void Off(float blend)
        {
            if (!_on) return;
            if (blend <= 0f || GameCamera.instance == null) { Reset(); return; }
            Note(GameCamera.instance);
            _leaving = true; _blendFrom = StoryClock.Now; _blendFor = blend;
            Eyes = false;
        }

        // at once (a scene stopped, the player died, the world changed)
        internal static void Reset() { _on = false; _leaving = false; _pose = null; Live = 0f; Eyes = false; EyesHide(Player.m_localPlayer, false); }

        private static void Note(GameCamera gc)
        {
            _wasPos = gc.transform.position; _wasRot = gc.transform.rotation;
            Camera c = CamF != null ? CamF.GetValue(gc) as Camera : null;
            _wasFov = c != null ? c.fieldOfView : Fov;
        }

        // the game's own rule: nothing between the camera and what it looks at — a roof, a wall, a tree in the way pulls
        // the camera in towards it (so a shot «from above» under a roof ends under the roof, never inside it)
        private static Vector3 Clear(GameCamera gc, Vector3 target, Vector3 want)
        {
            Vector3 d = want - target;
            float len = d.magnitude;
            if (len < 0.3f) return want;
            RaycastHit hit;
            if (Physics.SphereCast(target, gc.m_raycastWidth, d / len, out hit, len, gc.m_blockCameraMask, QueryTriggerInteraction.Ignore))
                return target + d / len * Mathf.Max(0.6f, hit.distance - 0.15f);
            return want;
        }

        // would a shot looking at «target» leave the lens there (Clear would not pull it in)? Asked as a spot is picked (StoryScenes: on,
        // shoulder, two), by the very cast the frame makes — so what is picked is what is filmed
        internal static bool Sees(Vector3 target, Vector3 lens)
        {
            GameCamera gc = GameCamera.instance;
            return gc == null || Clear(gc, target, lens) == lens; // heavy: one cast a spot tried, as its shot begins or is picked again
        }

        // the patch's work: true = it set the camera this frame (the game's own is skipped)
        internal static bool Drive(GameCamera gc, float dt)
        {
            if (!_on) return false;
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) { Reset(); return false; }
            Vector3 pos; Quaternion rot; float fov;
            float k = _blendFor <= 0f ? 1f : Mathf.Clamp01((StoryClock.Now - _blendFrom) / _blendFor);
            k = k * k * (3f - 2f * k);
            if (_leaving)
            {
                object[] args = { dt, Vector3.zero, Quaternion.identity };
                try { GamePose.Invoke(gc, args); } catch (Exception e) { Story.Log("camera: the game's own pose failed, the camera given back: " + e.Message); Reset(); return false; }
                pos = Vector3.Lerp(_wasPos, (Vector3)args[1], k); rot = Quaternion.Slerp(_wasRot, (Quaternion)args[2], k); fov = Mathf.Lerp(_wasFov, gc.m_fov, k);
                if (k >= 1f) Reset();
            }
            else
            {
                Pose w = _pose();
                pos = Vector3.Lerp(_wasPos, w.At, k); rot = Quaternion.Slerp(_wasRot, w.Rot, k); fov = Mathf.Lerp(_wasFov, w.Fov > 1f ? w.Fov : Fov, k);
                Sway(p, StoryClock.Delta, ref pos, ref rot); // (the game's dt runs on through its pause: the sway stands with the scene)
            }
            gc.transform.position = pos;
            gc.transform.rotation = rot;
            Camera c = CamF != null ? CamF.GetValue(gc) as Camera : null;
            if (c != null) { c.fieldOfView = fov; c.nearClipPlane = NearClip; }
            if (gc.m_skyCamera != null) gc.m_skyCamera.fieldOfView = fov;
            EyesHide(p, Eyes);
            if (SetVisibleM != null) { if (Eyes) SetVisibleM.Invoke(p, HiddenArgs); else if (Vector3.Distance(pos, p.transform.position) < HideWithin) SetVisibleM.Invoke(p, VisibleArgs); }
            try { if (ShakeM != null) { _shakeArgs[0] = dt; ShakeM.Invoke(gc, _shakeArgs); } } catch (Exception e) { ShakeM = null; Story.Log("camera: the game's shake failed, off for this session: " + e.Message); } // (said once: off after)
            return true;
        }
    }

    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class StoryCameraPatch
    {
        private static bool Prefix(GameCamera __instance, float dt)
        {
            try { return !StoryCamera.Drive(__instance, dt); } catch (Exception e) { PatchGuard.Fail("StoryCameraPatch", e); StoryCamera.Reset(); return true; } // (a shot that throws: the camera back, and said)
        }
    }

    // a cutscene on (a shot, the bars, the black): the player stands still, no blows
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class StoryCameraInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            try { if (StoryCamera.TakesPlayer) __result = false; }
            catch (Exception e) { PatchGuard.Fail("StoryCameraInputPatch", e); }
        }
    }

    // a cutscene on: the HUD out of sight (the game shows it again each frame by itself; this puts it away after)
    [HarmonyPatch(typeof(Hud), "Update")]
    internal static class StoryCameraHudPatch
    {
        private static readonly Action<Hud, bool> SetVisible = Hide(); // (made once: called each frame of a shot)
        private static Action<Hud, bool> Hide()
        {
            MethodInfo m = AccessTools.Method(typeof(Hud), "SetVisible");
            try { return m != null ? (Action<Hud, bool>)Delegate.CreateDelegate(typeof(Action<Hud, bool>), m) : null; } catch (Exception e) { Story.Log("camera: Hud.SetVisible not usable, the HUD stays in shots: " + e.Message); return null; }
        }
        private static void Postfix(Hud __instance)
        {
            if (!StoryCamera.TakesPlayer || SetVisible == null) return;
            try { SetVisible(__instance, false); } catch (Exception e) { PatchGuard.Fail("StoryCameraHudPatch", e); }
        }
    }

    // a cutscene on: no name and health bar over anyone (the game's EnemyHud — its own, not the HUD's): a person, a monster, a player. Said no here,
    // the game takes the bar off in the same frame (EnemyHud.UpdateHuds); the user 2026-11-14 saw «КЕТІЛЬ» and his bar over the ambush's close shots
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    internal static class StoryCameraNamesPatch
    {
        private static void Postfix(ref bool __result)
        {
            try { if (__result && StoryCamera.TakesPlayer) __result = false; } catch (Exception e) { PatchGuard.Fail("StoryCameraNamesPatch", e); }
        }
    }

    // A scene's own clock (seconds) for what it shows and how its camera moves: not the game's scaled time (a scene's picture keeps its pace),
    // but standing still while the game is paused — single play's Esc menu sets Time.timeScale 0 and a scene's steps, on Time.time, wait; so its
    // subtitle, its black, its glow, its blends and its live sway wait with them. A voice the game plays is not paused by it.
    // Read at least once a frame while anything runs on it (the screen's OnGUI, a shot's camera): a frame nobody read does not count.
    internal static class StoryClock
    {
        private static double _now; // (a double: a float summed each frame drifts over hours of play)
        private static int _frame = -1;
        internal static float Delta { get { return Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f; } }
        internal static float Now { get { if (Time.frameCount != _frame) { _frame = Time.frameCount; _now += Delta; } return (float)_now; } }
    }

    // The bars over and under the picture and the subtitles: drawn over the game while a scene wants them. The black (fade) and the
    // glow of a memory (Glow: a warm light coming in from the edges, breathing slowly — the user 2026-11-14, Munin speaks the player's own memory) too.
    internal class StoryScreen : MonoBehaviour
    {
        private static StoryScreen _inst;
        internal static float BarShare = 0.11f, BarSeconds = 0.8f;
        private static bool _bars;
        private static float _barNow;
        private static string _sub = "";
        private static float _subUntil;
        private Texture2D _black;
        private GUIStyle _style, _shadow;

        private static void Need()
        {
            if (_inst != null) return;
            GameObject g = new GameObject("RavenWatch_StoryScreen");
            DontDestroyOnLoad(g);
            _inst = g.AddComponent<StoryScreen>();
        }

        internal static bool Held { get { return _bars || _fadeTo > 0.5f; } } // (bars on, or the picture going or gone black: StoryCamera.TakesPlayer)
        internal static void Bars(bool on) { if (StoryScenes.Watching) return; if (on) Need(); _bars = on; }
        internal static void Sub(string text, float seconds) { Sub("", text, seconds); }
        // who says it: its name a little gold before the line (the user 2026-11-07: «трохи виділялось, не кидалось в очі»)
        internal static void Sub(string who, string text, float seconds)
        {
            Need();
            who = who ?? ""; text = text ?? "";
            _sub = who.Length > 0 ? who + ": " + text : text;
            _subRich = who.Length > 0 ? "<color=#" + ColorUtility.ToHtmlStringRGB(NameGold) + ">" + who + ":</color> " + text : text;
            _subUntil = StoryClock.Now + seconds;
        }
        internal static Color NameGold = new Color(0.86f, 0.74f, 0.5f);
        private static string _subRich = "";
        internal static void Reset() { _bars = false; _sub = ""; _subRich = ""; _subUntil = 0f; _fadeTo = 0f; _glowTo = 0f; _glowNow = 0f; _dimTo = 0f; _dimNow = 0f; _mems.Clear(); } // (a stop: the camera is back at once — the glow and a vision too)

        // the black over the whole picture: «fade out» to black, «fade in» back (seconds)
        private static float _fadeNow, _fadeTo, _fadeSec = 1f;
        internal static bool Dark { get { return _fadeTo > 0.5f; } } // (the picture is going or gone black)
        internal static float Black { get { return _fadeNow; } } // (how black it is now, 0…1: the studio's scene check)
        internal static void Fade(bool black, float seconds) { if (StoryScenes.Watching) return; if (black) Need(); _fadeTo = black ? 1f : 0f; _fadeSec = Mathf.Max(0.05f, seconds); }

        // the glow of a memory over the picture: how strong (0 none … 1 the edges white-gold, the middle washed), reached in seconds; it breathes
        // GlowBreath of itself every GlowPeriod s. A scene's end takes it off (StoryScenes.Release: GlowOff, over GlowOffSeconds; a stop — Reset, at once)
        private static float _glowNow, _glowTo, _glowSec = 1f;
        internal static float GlowBreath = 0.12f, GlowPeriod = 3.2f, GlowWash = 0.35f; // (× its strength: the breath; the light over the middle)
        internal static Color GlowColour = new Color(1f, 0.93f, 0.74f);
        private Texture2D _glowTex, _white;
        internal static void Glow(float to, float seconds) { if (StoryScenes.Watching) return; if (to > 0f) Need(); _glowTo = Mathf.Clamp01(to); _glowSec = Mathf.Max(0.05f, seconds); }
        internal static float Glowing { get { return _glowNow; } } // (how strong it is now: the studio's scene check)
        internal static float GlowOffSeconds = 1.5f;
        internal static void GlowOff() { _glowTo = 0f; _glowSec = GlowOffSeconds; }

        // ---- a vision (the user 2026-11-15: «екран темнішає і з'являються скріншоти прозорі поверх екрана як уривки пам'яті»)
        // «dim <0..1>»: the picture darkened — not the black that hides a scene's work (Fade, Dark), a scene's own dark under its memories.
        // «memory <picture> …»: a picture of assets/story/memories/<name>.png|.jpg (the author's machine reads it there first — live; else the
        // build's RavenWatch.memories.<name>.*) over the picture, see-through: in over MemIn s, holding, out over MemOut s, drifting and nearing a
        // little the while; several at once. Made a memory once as it is read (MakeMemory: its colours drawn toward the memory's gold, its edges
        // gone soft into nothing). A scene's end takes all of it off (DimOff, StoryScenes.Release); a stop at once (Reset)
        private static float _dimNow, _dimTo, _dimSec = 1f;
        internal static float DimOffSeconds = 1.5f;
        internal static void Dim(float to, float seconds) { if (StoryScenes.Watching) return; if (to > 0f) Need(); _dimTo = Mathf.Clamp01(to); _dimSec = Mathf.Max(0.05f, seconds); }
        internal static void DimOff() { _dimTo = 0f; _dimSec = DimOffSeconds; float now = StoryClock.Now; foreach (Mem m in _mems) if (m.Until > now) m.Until = now; }
        internal static float MemIn = 1.4f, MemOut = 1.8f, MemAlpha = 0.6f, MemDrift = 0.025f, MemGrow = 0.06f, MemGold = 0.55f, MemEdge = 0.22f; // (live: raven npc tune StoryScreen.<field>; the last two when a picture is read)
        private sealed class Mem { public Texture2D Tex; public float X, Y, W, From, Until; public Vector2 Drift; }
        private static readonly List<Mem> _mems = new List<Mem>(); // session: the memories on the screen now
        private static readonly Dictionary<string, Texture2D> _memTex = new Dictionary<string, Texture2D>(); // session: each picture made a memory once (null: none of that name)
        internal static string Memory(string name, float x, float y, float width, float seconds)
        {
            if (StoryScenes.Watching) return null;
            Texture2D t = MemoryPicture(name);
            if (t == null) return "no memory picture «" + name + "» (assets/story/memories/<name>.png|.jpg)";
            Need();
            float now = StoryClock.Now;
            _mems.Add(new Mem { Tex = t, X = x, Y = y, W = Mathf.Clamp(width, 0.1f, 1f), From = now, Until = now + MemIn + Mathf.Max(0.5f, seconds), Drift = UnityEngine.Random.insideUnitCircle.normalized * MemDrift });
            return null;
        }
        // is there such a picture (the load's check: nothing read, nothing decoded — that waits for its first play)
        internal static bool HasMemory(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string dir = StoryFiles.Source != null ? System.IO.Path.Combine(StoryFiles.Source, "memories") : null;
            foreach (string ext in new[] { ".png", ".jpg" })
            {
                if (dir != null && System.IO.File.Exists(System.IO.Path.Combine(dir, name + ext))) return true;
                if (typeof(StoryScreen).Assembly.GetManifestResourceInfo("RavenWatch.memories." + name + ext) != null) return true;
            }
            return false;
        }

        private static Texture2D MemoryPicture(string name)
        {
            Texture2D t;
            if (string.IsNullOrEmpty(name)) return null;
            if (_memTex.TryGetValue(name, out t)) return t;
            byte[] data = null;
            string dir = StoryFiles.Source != null ? System.IO.Path.Combine(StoryFiles.Source, "memories") : null;
            foreach (string ext in new[] { ".png", ".jpg" })
            {
                if (data != null) break;
                string f = dir != null ? System.IO.Path.Combine(dir, name + ext) : null;
                if (f != null && System.IO.File.Exists(f)) { data = System.IO.File.ReadAllBytes(f); break; }
                using (System.IO.Stream st = typeof(StoryScreen).Assembly.GetManifestResourceStream("RavenWatch.memories." + name + ext))
                    if (st != null) { data = new byte[st.Length]; int read = 0; while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; } }
            }
            t = null;
            if (data != null)
            {
                Texture2D read = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (AbilityIcons.Decode(read, data))
                {
                    t = new Texture2D(read.width, read.height, TextureFormat.RGBA32, false); // (a JPG decodes without alpha: its soft edges are alpha — made in a copy that has it)
                    t.SetPixels32(read.GetPixels32());
                    MakeMemory(t); t.wrapMode = TextureWrapMode.Clamp; t.hideFlags = HideFlags.HideAndDontSave;
                }
                UnityEngine.Object.Destroy(read);
            }
            if (t == null) { Story.Log("memory: no picture «" + name + "»"); return null; } // (not kept: one added later is read when asked again)
            _memTex[name] = t;
            return t;
        }

        // a picture made a memory: its colours drawn toward the memory's gold (MemGold of the way), its edges dissolving into nothing (MemEdge of
        // each side). heavy: once a picture, every pixel (a screenshot of ~1 MB: once in a scene)
        private static void MakeMemory(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            int w = t.width, h = t.height;
            Color gold = GlowColour;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h, ey = Mathf.Clamp01(Mathf.Min(v, 1f - v) / Mathf.Max(0.01f, MemEdge)); ey = ey * ey * (3f - 2f * ey);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, ex = Mathf.Clamp01(Mathf.Min(u, 1f - u) / Mathf.Max(0.01f, MemEdge)); ex = ex * ex * (3f - 2f * ex);
                    Color32 c = px[y * w + x];
                    float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f, l = r * 0.3f + g * 0.59f + b * 0.11f;
                    r = Mathf.Lerp(r, l * gold.r, MemGold); g = Mathf.Lerp(g, l * gold.g, MemGold); b = Mathf.Lerp(b, l * gold.b, MemGold);
                    px[y * w + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)(c.a * ex * ey));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true); // (no longer read: its pixels let go)
        }

        private void DrawVision()
        {
            if (_white == null) _white = Plugin.Solid(Color.white);
            if (_black == null) _black = Plugin.Solid(Color.black);
            Color was = GUI.color; GUI.depth = -1001;
            if (_dimNow > 0.001f) { GUI.color = new Color(1f, 1f, 1f, _dimNow); GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black); }
            float now = StoryClock.Now;
            for (int i = _mems.Count - 1; i >= 0; i--) if (_mems[i].Tex == null || now > _mems[i].Until + MemOut) _mems.RemoveAt(i);
            foreach (Mem m in _mems)
            {
                float life = m.Until + MemOut - m.From, t = now - m.From;
                float a = MemAlpha * Mathf.Min(Mathf.Clamp01(t / MemIn), Mathf.Clamp01((m.Until + MemOut - now) / MemOut));
                float k = Mathf.Clamp01(t / Mathf.Max(0.1f, life)), grow = 1f + MemGrow * k;
                float w = Screen.width * m.W * grow, h = w * m.Tex.height / Mathf.Max(1, m.Tex.width);
                float cx = Screen.width * (m.X + m.Drift.x * k), cy = Screen.height * (m.Y + m.Drift.y * k);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), m.Tex, ScaleMode.StretchToFill, true);
            }
            GUI.color = was;
        }

        // the light from the edges: clear in the middle, strong at the rim (made once, small: the screen stretches it soft)
        private static Texture2D GlowTexture()
        {
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.35f) / 0.85f); // (nothing within a third of the middle)
                px[y * n + x] = new Color(1f, 1f, 1f, r * r * (3f - 2f * r));
            }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // the story's goal under the map (StoryQuests: the active quest's name and what to do now; «» — none) and, under it and paler,
        // the quest beside it (the user 2026-11-17: two at once — a second line, the same letters, dimmer); a new one of either is bright a while
        private static string _goalTitle = "", _goal = "", _sideTitle = "", _side = "";
        private static float _goalShown, _goalAt, _sideAt;
        internal static void Goal(string title, string goal, string sideTitle = "", string side = "")
        {
            goal = goal ?? ""; side = side ?? ""; title = title ?? ""; sideTitle = sideTitle ?? "";
            bool main = goal != _goal || title != _goalTitle, beside = side != _side || sideTitle != _sideTitle;
            if (!main && !beside) return;
            if (goal.Length > 0) Need();
            if (main) { _goalTitle = title; _goal = goal; _goalAt = Time.unscaledTime; _goalShown = 0f; } // (the first one changed: it comes in softly anew, as ever)
            if (beside) { _sideTitle = sideTitle; _side = side; _sideAt = Time.unscaledTime; }
        }
        private GUIStyle _goalStyle, _goalHead;
        private string _goalHFor; private float _goalHW, _goalH; // (the first goal's height as last measured: the second stands under it)

        private void Update()
        {
            _barNow = Mathf.MoveTowards(_barNow, _bars ? 1f : 0f, StoryClock.Delta / Mathf.Max(0.05f, BarSeconds));
            _fadeNow = Mathf.MoveTowards(_fadeNow, _fadeTo, StoryClock.Delta / _fadeSec);
            _glowNow = Mathf.MoveTowards(_glowNow, _glowTo, StoryClock.Delta / _glowSec);
            _dimNow = Mathf.MoveTowards(_dimNow, _dimTo, StoryClock.Delta / _dimSec);
            bool show = _goal.Length > 0 && _barNow <= 0.001f && Hud.instance != null && Hud.instance.IsVisible() && !InventoryGui.IsVisible() && !Minimap.IsOpen() && !Menu.IsVisible(); // (only with the game's HUD: not over the intro's titles, the valkyrie's flight, sleep, a shot, the user's F3)
            _goalShown = Mathf.MoveTowards(_goalShown, show ? 1f : 0f, Time.unscaledDeltaTime / 0.6f); // (comes and goes softly)
        }

        private void OnGUI()
        {
            if (_goalShown > 0.001f) DrawGoal();
            if (_glowNow > 0.001f) DrawGlow(); // (under the black and the bars)
            if (_dimNow > 0.001f || _mems.Count > 0) DrawVision(); // (over the glow, under the black and the bars)
            if (_fadeNow > 0.001f)
            {
                if (_black == null) _black = Plugin.Solid(Color.black);
                Color fw = GUI.color; GUI.color = new Color(1f, 1f, 1f, _fadeNow); GUI.depth = -1001;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
                GUI.color = fw;
            }
            if (_barNow <= 0.001f && (_sub.Length == 0 || StoryClock.Now > _subUntil)) return;
            if (_black == null) _black = Plugin.Solid(Color.black);
            GUI.depth = -1000;
            float h = Screen.height * BarShare * _barNow;
            if (h > 0.5f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), _black);
                GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), _black);
            }
            if (_sub.Length == 0 || StoryClock.Now > _subUntil) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.RoundToInt(Screen.height * 0.028f), richText = true };
                _style.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
                _shadow = new GUIStyle(_style) { richText = false }; _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f); // (the shadow plain: a colour tag in it would turn the name's shadow gold)
            }
            float a = Mathf.Clamp01((_subUntil - StoryClock.Now) / 0.4f);
            float bh = Mathf.Max(h, Screen.height * 0.09f);
            Rect r = new Rect(Screen.width * 0.12f, Screen.height - bh, Screen.width * 0.76f, bh);
            Color was = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), _sub, _shadow);
            GUI.Label(r, _subRich, _style);
            GUI.color = was;
        }

        private void DrawGlow()
        {
            if (_glowTex == null) _glowTex = GlowTexture();
            if (_white == null) _white = Plugin.Solid(Color.white);
            float g = _glowNow * (1f - GlowBreath + GlowBreath * Mathf.Sin(StoryClock.Now * Mathf.PI * 2f / GlowPeriod));
            Color was = GUI.color; GUI.depth = -1001;
            Rect all = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = new Color(GlowColour.r, GlowColour.g, GlowColour.b, g * GlowWash); GUI.DrawTexture(all, _white);
            GUI.color = new Color(GlowColour.r, GlowColour.g, GlowColour.b, g); GUI.DrawTexture(all, _glowTex);
            GUI.color = was;
        }

        // under the minimap, right: the quest's name small and gold, the goal under it; a new goal glows for a moment
        private void DrawGoal()
        {
            if (_goalStyle == null)
            {
                int fs = Mathf.RoundToInt(Screen.height * 0.019f);
                _goalStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, wordWrap = true, fontSize = fs };
                _goalHead = new GUIStyle(_goalStyle) { fontSize = Mathf.RoundToInt(fs * 0.85f), fontStyle = FontStyle.Bold };
            }
            float w = Screen.width * 0.2f, x = Screen.width - w - Screen.width * 0.012f, y = Mathf.Max(Screen.height * 0.34f, BelowEffects());
            float fresh = Mathf.Clamp01(1f - (Time.unscaledTime - _goalAt) / 4f); // (new: brighter for a few seconds)
            Color was = GUI.color;
            float a = _goalShown;
            Shadowed(new Rect(x, y, w, _goalHead.fontSize * 1.6f), _goalTitle, _goalHead, new Color(0.86f, 0.7f, 0.36f, a * (0.75f + 0.25f * fresh)));
            Shadowed(new Rect(x, y + _goalHead.fontSize * 1.5f, w, Screen.height * 0.12f), _goal, _goalStyle, Color.Lerp(new Color(0.9f, 0.88f, 0.82f, a * 0.85f), new Color(1f, 0.93f, 0.7f, a), fresh));
            if (_side.Length > 0)
            {
                if (_goalHFor != _goal || _goalHW != w) { _goalHFor = _goal; _goalHW = w; _goalH = Mathf.Min(_goalStyle.CalcHeight(new GUIContent(_goal), w), Screen.height * 0.12f); } // (measured when it or the screen changes, not each frame)
                float y2 = y + _goalHead.fontSize * 1.5f + _goalH + _goalHead.fontSize * 0.9f; // (under the first, as tall as it wraps)
                float freshSide = Mathf.Clamp01(1f - (Time.unscaledTime - _sideAt) / 4f), dim = 0.6f; // (beside the chapter's own: dimmer, the same letters)
                Shadowed(new Rect(x, y2, w, _goalHead.fontSize * 1.6f), _sideTitle, _goalHead, new Color(0.86f, 0.7f, 0.36f, a * dim * (0.75f + 0.25f * freshSide)));
                Shadowed(new Rect(x, y2 + _goalHead.fontSize * 1.5f, w, Screen.height * 0.12f), _side, _goalStyle, Color.Lerp(new Color(0.9f, 0.88f, 0.82f, a * dim * 0.85f), new Color(1f, 0.93f, 0.7f, a * dim), freshSide));
            }
            GUI.color = was;
        }

        // where the game's list of status effects ends now, from the top of the screen (it grows down from under the map)
        private static readonly Vector3[] Corners = new Vector3[4];
        private static float BelowEffects()
        {
            RectTransform list = Hud.instance != null ? Hud.instance.m_statusEffectListRoot : null;
            if (list == null) return 0f;
            float low = 0f;
            for (int i = 0; i < list.childCount; i++)
            {
                RectTransform c = list.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                c.GetWorldCorners(Corners);
                low = Mathf.Max(low, Screen.height - Corners[0].y); // (a screen-space list: its corners are in pixels)
            }
            return low > 0f ? low + Screen.height * 0.008f : 0f;
        }

        private static void Shadowed(Rect r, string text, GUIStyle st, Color c)
        {
            if (string.IsNullOrEmpty(text)) return;
            Color keep = st.normal.textColor;
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
            st.normal.textColor = keep;
        }
    }
}
#endif

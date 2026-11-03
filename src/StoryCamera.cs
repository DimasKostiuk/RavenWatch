#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The scene's camera (story-tech: GameCamera.UpdateCamera is the one place the game sets its camera each frame —
    // a prefix there owns it while a shot is on). Only this player's camera: the others near see the scene live, theirs
    // is never taken. While a shot is on the player does not move or fight (PlayerController.TakeInput says no) and the
    // HUD is out of sight; the world never pauses, and a scene that stops gives the camera back at once.
    //   Shot(from, look, blend, fov)   from a point (fixed) looking at a point that may move (followed each frame)
    //   Shot(pose, blend)              any camera that moves by itself: a shot of the author's, a move between two of
    //                                  them, an orbit (StoryShots), the author's free camera — its pose asked each frame
    //   Off(blend)                     back to the game's camera, blended from where the shot was
    // blend 0 = a cut (at once); else eased in from wherever the camera was (the game's, or the last shot — even moving).
    // The bars and the subtitles are StoryScreen's.
    internal static class StoryCamera
    {
        internal static float Fov = 65f;
        private static bool _on, _leaving;
        private static Func<Pose> _pose;
        private static float _blendFrom, _blendFor;
        private static Vector3 _wasPos;
        private static Quaternion _wasRot;
        private static float _wasFov;

        internal static bool Active { get { return _on; } }

        private static readonly FieldInfo CamF = AccessTools.Field(typeof(GameCamera), "m_camera");
        private static readonly MethodInfo GamePose = AccessTools.Method(typeof(GameCamera), "GetCameraPosition");
        private static readonly MethodInfo ShakeM = AccessTools.Method(typeof(GameCamera), "UpdateCameraShake");

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
            _blendFrom = Time.unscaledTime; _blendFor = Mathf.Max(0f, blend);
            _on = true; _leaving = false;
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
            _leaving = true; _blendFrom = Time.unscaledTime; _blendFor = blend;
        }

        // at once (a scene stopped, the player died, the world changed)
        internal static void Reset() { _on = false; _leaving = false; _pose = null; }

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

        // the patch's work: true = it set the camera this frame (the game's own is skipped)
        internal static bool Drive(GameCamera gc, float dt)
        {
            if (!_on) return false;
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) { Reset(); return false; }
            Vector3 pos; Quaternion rot; float fov;
            float k = _blendFor <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - _blendFrom) / _blendFor);
            k = k * k * (3f - 2f * k);
            if (_leaving)
            {
                object[] args = { dt, Vector3.zero, Quaternion.identity };
                try { GamePose.Invoke(gc, args); } catch { Reset(); return false; }
                pos = Vector3.Lerp(_wasPos, (Vector3)args[1], k); rot = Quaternion.Slerp(_wasRot, (Quaternion)args[2], k); fov = Mathf.Lerp(_wasFov, gc.m_fov, k);
                if (k >= 1f) Reset();
            }
            else
            {
                Pose w = _pose();
                pos = Vector3.Lerp(_wasPos, w.At, k); rot = Quaternion.Slerp(_wasRot, w.Rot, k); fov = Mathf.Lerp(_wasFov, w.Fov > 1f ? w.Fov : Fov, k);
            }
            gc.transform.position = pos;
            gc.transform.rotation = rot;
            Camera c = CamF != null ? CamF.GetValue(gc) as Camera : null;
            if (c != null) c.fieldOfView = fov;
            if (gc.m_skyCamera != null) gc.m_skyCamera.fieldOfView = fov;
            try { if (ShakeM != null) ShakeM.Invoke(gc, new object[] { dt }); } catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class StoryCameraPatch
    {
        private static bool Prefix(GameCamera __instance, float dt)
        {
            try { return !StoryCamera.Drive(__instance, dt); } catch { StoryCamera.Reset(); return true; }
        }
    }

    // a shot on: the player stands still, no blows
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class StoryCameraInputPatch
    {
        private static void Postfix(ref bool __result) { if (StoryCamera.Active) __result = false; }
    }

    // a shot on: the HUD out of sight (the game shows it again each frame by itself; this puts it away after)
    [HarmonyPatch(typeof(Hud), "Update")]
    internal static class StoryCameraHudPatch
    {
        private static readonly Action<Hud, bool> SetVisible = Hide(); // (made once: called each frame of a shot)
        private static Action<Hud, bool> Hide()
        {
            MethodInfo m = AccessTools.Method(typeof(Hud), "SetVisible");
            try { return m != null ? (Action<Hud, bool>)Delegate.CreateDelegate(typeof(Action<Hud, bool>), m) : null; } catch { return null; }
        }
        private static void Postfix(Hud __instance)
        {
            if (!StoryCamera.Active || SetVisible == null) return;
            try { SetVisible(__instance, false); } catch { }
        }
    }

    // The bars over and under the picture and the subtitles: drawn over the game while a scene wants them.
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

        internal static void Bars(bool on) { if (on) Need(); _bars = on; }
        internal static void Sub(string text, float seconds) { Need(); _sub = text ?? ""; _subUntil = Time.unscaledTime + seconds; }
        internal static void Reset() { _bars = false; _sub = ""; _subUntil = 0f; _fadeTo = 0f; }

        // the black over the whole picture: «fade out» to black, «fade in» back (seconds)
        private static float _fadeNow, _fadeTo, _fadeSec = 1f;
        internal static void Fade(bool black, float seconds) { if (black) Need(); _fadeTo = black ? 1f : 0f; _fadeSec = Mathf.Max(0.05f, seconds); }

        // the story's goal under the map (StoryQuests: the active quest's name and what to do now; «» — none)
        private static string _goalTitle = "", _goal = "";
        private static float _goalShown, _goalAt;
        internal static void Goal(string title, string goal)
        {
            goal = goal ?? "";
            if (goal == _goal && title == _goalTitle) return;
            if (goal.Length > 0) Need();
            _goalTitle = title ?? ""; _goal = goal; _goalAt = Time.unscaledTime; _goalShown = 0f;
        }
        private GUIStyle _goalStyle, _goalHead;

        private void Update()
        {
            _barNow = Mathf.MoveTowards(_barNow, _bars ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, BarSeconds));
            _fadeNow = Mathf.MoveTowards(_fadeNow, _fadeTo, Time.unscaledDeltaTime / _fadeSec);
            bool show = _goal.Length > 0 && !StoryCamera.Active && _barNow <= 0.001f && Hud.instance != null && !Hud.IsUserHidden() && !InventoryGui.IsVisible() && !Minimap.IsOpen() && !Menu.IsVisible();
            _goalShown = Mathf.MoveTowards(_goalShown, show ? 1f : 0f, Time.unscaledDeltaTime / 0.6f); // (comes and goes softly)
        }

        private void OnGUI()
        {
            if (_goalShown > 0.001f) DrawGoal();
            if (_fadeNow > 0.001f)
            {
                if (_black == null) _black = Plugin.Solid(Color.black);
                Color fw = GUI.color; GUI.color = new Color(1f, 1f, 1f, _fadeNow); GUI.depth = -1001;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _black);
                GUI.color = fw;
            }
            if (_barNow <= 0.001f && (_sub.Length == 0 || Time.unscaledTime > _subUntil)) return;
            if (_black == null) _black = Plugin.Solid(Color.black);
            GUI.depth = -1000;
            float h = Screen.height * BarShare * _barNow;
            if (h > 0.5f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), _black);
                GUI.DrawTexture(new Rect(0f, Screen.height - h, Screen.width, h), _black);
            }
            if (_sub.Length == 0 || Time.unscaledTime > _subUntil) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.RoundToInt(Screen.height * 0.028f) };
                _style.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
                _shadow = new GUIStyle(_style); _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            float a = Mathf.Clamp01((_subUntil - Time.unscaledTime) / 0.4f);
            float bh = Mathf.Max(h, Screen.height * 0.09f);
            Rect r = new Rect(Screen.width * 0.12f, Screen.height - bh, Screen.width * 0.76f, bh);
            Color was = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), _sub, _shadow);
            GUI.Label(r, _sub, _style);
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

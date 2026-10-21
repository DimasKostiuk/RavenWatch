#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The picture in the «Політ» tab: a raven of your kind and trust flying a test route far above the world, by the
    // very same motor (RavenMotor) and wings (ShoulderLife: the game's clip, your wing keys, the adaptive strokes) as
    // in the game, so every slider shows at once in the beats, glides, banks and turns. A camera of its own renders
    // it into a picture (its own layer and lights, nothing of the world). Gone a moment after the tab is not drawn.
    internal static class FlightStage
    {
        internal const int Size = 360;
        internal enum Route { Circle, Eight, Waves, Hover, Straight }
        internal enum View { Behind, Orbit, Wide }
        internal static readonly string[] RouteNames = { "Коло", "Вісімка", "Хвилі", "Зависання", "Прямо" };
        internal static readonly string[] ViewNames = { "За вороном", "Навколо", "Загальний" };
        internal static Route Path = Route.Straight; // level, no turns: the pose seen as it is (a circle banks it into the turn all the time)
        internal static View Camera_ = View.Behind;
        internal static float Yaw = 0f, Pitch = 12f, Zoom = 1f;
        internal static float RouteSize = 7f;
        internal static float Slow = 1f;       // the picture's own speed (0: held still); the game runs on as it is
        internal static float StepOnce;        // held still: one step of this many seconds, once
        internal static bool Beating = true;   // the picture's bird beats all the time, without slower glide beats
        internal static bool TakeoffPreview;
        internal static float TakeoffPreviewPhase;
        private static float _clock;
        internal static string Problem = "";

        private static GameObject _stage, _model;
        private static bool _munin;
        private static Camera _cam;
        private static Animator _anim;
        private static RenderTexture _rt;
        private static ShoulderLife _life;
        private static RavenMotor _motor;
        private static float _drawnAt, _radius = 0.5f, _camYaw;
        private static readonly Vector3 Origin = new Vector3(0f, 9400f, 0f);

        internal static RenderTexture Texture { get { return _rt; } }
        internal static RavenMotor Motor { get { return _model != null ? _motor : null; } }

        // ---------------------------------------------------------------- the black box played back (BlackBox)
        // a recorded raven shown frame by frame over a marker for the seat (your shoulder): where it was from it,
        // how it was turned, every bone as it was; the motor waits meanwhile
        internal static BlackBox.Track Replay;
        internal static float ReplayT, ReplaySpeed = 1f;
        internal static bool ReplayPlay = true;
        private static GameObject _seat;
        private static Transform[] _repBones;
        private static BlackBox.Track _repFor;
        private static GameObject _repModel;
        internal static BlackBox.Frame ReplayFrame { get { if (Replay == null) return null; int i = Replay.IndexAt(ReplayT); return i >= 0 ? Replay.Frames[i] : null; } }

        private static void PoseReplay()
        {
            float len = Replay.Length;
            if (ReplayPlay) { ReplayT += Time.unscaledDeltaTime * ReplaySpeed; if (ReplayT > len) ReplayT = 0f; }
            ReplayT = Mathf.Clamp(ReplayT, 0f, len);
            BlackBox.Frame f = ReplayFrame;
            if (f == null) return;
            if (_repFor != Replay || _repModel != _model)
            {
                // its bones by name on this model (the recorded one had its own: made at another time, other pieces on)
                _repFor = Replay; _repModel = _model;
                Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
                foreach (Transform b in _model.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(b.name)) byName[b.name] = b;
                _repBones = new Transform[Replay.Bones.Length];
                for (int i = 0; i < _repBones.Length; i++) { Transform b; if (byName.TryGetValue(Replay.Bones[i], out b)) _repBones[i] = b; }
            }
            Vector3 seat = Origin + Vector3.up * 2f;
            if (_seat != null) { _seat.SetActive(true); _seat.transform.position = seat; }
            _model.transform.position = seat + f.Pos;
            _model.transform.rotation = f.Rot;
            for (int i = 0; i < _repBones.Length; i++) if (_repBones[i] != null) { _repBones[i].localRotation = f.BoneRot[i]; _repBones[i].localPosition = f.BonePos[i]; }
        }
        internal static ShoulderLife Life { get { return _model != null ? _life : null; } }

        // the panel draws the picture this frame: the stage is (made and) kept
        internal static bool Show(bool munin)
        {
            _drawnAt = Time.unscaledTime;
            if (_stage != null && _model != null && _munin == munin) return true;
            bool takeoffPreview = TakeoffPreview;
            Close();
            TakeoffPreview = takeoffPreview;
            Player p = Player.m_localPlayer;
            Raven src = Shoulder.Template(munin);
            if (p == null || src == null || src.m_visual == null) { Problem = "Не знайдено модель ворона."; return false; }
            int layer = RavenFx.FreeLayer();
            if (layer < 0) { Problem = "Немає вільного шару для картинки."; return false; }
            Problem = "";
            _munin = munin;
            _stage = new GameObject("RavenWatch_FlightStage");
            _stage.transform.position = Origin;
            _stage.AddComponent<FlightStageRun>();

            // the raven: your own size and trust (as on your shoulder), the game's model
            ShoulderLife yours = Shoulder.LifeOf(p, munin);
            int level = yours != null ? Mathf.Max(1, yours.Level) : Mathf.Max(1, Trust.LevelWorn(munin));
            _model = Object.Instantiate(src.m_visual, Origin + Vector3.up * 2f, Quaternion.identity);
            AppearMute.Quiet(_model);
            _model.name = "RavenWatch_FlightStageRaven";
            _model.SetActive(true);
            Shared.RemoveExclamation(src, _model);
            _model.transform.localScale = src.m_visual.transform.lossyScale * Shoulder.Scale * Shoulder.SizeFor(level);
            foreach (Collider c in _model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            Animator anim = _model.GetComponentInChildren<Animator>(true);
            // its body where it sits (the prefab's own pose holds it far off): the game's idle clip at its start
            if (anim != null && anim.runtimeAnimatorController != null)
                foreach (AnimationClip c in anim.runtimeAnimatorController.animationClips)
                    if (c != null && c.name.ToLowerInvariant().Contains("idle") && c.isLooping) { c.SampleAnimation(anim.gameObject, 0f); break; }
            _anim = anim;
            if (TakeoffPreview && anim != null) anim.enabled = false;
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.enabled = true;
                for (Transform t = r.transform; t != null && t != _model.transform; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
                if (sk != null) { sk.updateWhenOffscreen = true; sk.skinnedMotionVectors = false; }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            _life = _model.AddComponent<ShoulderLife>();
            _life.Init(p, munin, level);
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            // in flight, as a strike flies it: the game's flying clip, our beats on it
            _life.Soaring = true; _life.Still = true; _life.VanillaFly = true;
            if (TakeoffPreview && _anim != null) _anim.enabled = false;
            _motor = RavenMotor.Of(_model, munin, p);
            _motor.Launch(Vector3.forward * 6f, "перегляд", 1f);
            _motor.Body.Airborne();
            _radius = Mathf.Clamp(0.55f * Shoulder.SizeFor(level) / 0.6f, 0.25f, 2f);

            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.cullingMask = 1 << layer; _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.36f, 0.45f, 0.56f, 1f); _cam.fieldOfView = 40f;
            _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 200f;
            _cam.renderingPath = RenderingPath.Forward; _cam.allowHDR = false; _cam.allowMSAA = false;
            _cam.depth = -50f;
            Lamp("Key", 1.2f, Color.white, Quaternion.Euler(50f, 150f, 0f), layer);
            Lamp("Fill", 0.6f, new Color(0.8f, 0.85f, 1f), Quaternion.Euler(20f, -40f, 0f), layer);
            Lamp("Back", 0.4f, Color.white, Quaternion.Euler(-30f, 0f, 0f), layer);
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); _rt.Create();
            _cam.targetTexture = _rt;
            // the seat's marker for the black box (your shoulder): a small light ball
            _seat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _seat.name = "Seat";
            Object.Destroy(_seat.GetComponent<Collider>());
            _seat.transform.SetParent(_stage.transform, false);
            _seat.transform.localScale = Vector3.one * 0.07f;
            _seat.layer = layer;
            Renderer sr = _seat.GetComponent<Renderer>();
            if (sr != null) { sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false; }
            _seat.SetActive(false);
            _repModel = null;
            return true;
        }

        private static void Lamp(string name, float power, Color c, Quaternion turn, int layer)
        {
            Light l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(_stage.transform, false); l.type = LightType.Directional; l.intensity = power; l.color = c;
            l.cullingMask = 1 << layer; l.shadows = LightShadows.None; l.transform.rotation = turn;
        }

        internal static void Close()
        {
            if (_cam != null) { _cam.enabled = false; _cam.targetTexture = null; }
            if (_model != null) Object.Destroy(_model);
            if (_stage != null) Object.Destroy(_stage);
            _model = null; _stage = null; _cam = null; _anim = null; _life = null; _motor = null; _seat = null; _repModel = null;
            _repFor = null; _repBones = null;
            TakeoffPreview = false;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
        }

        // the route as a curve (u 0..1 round), around the stage's middle
        private static Vector3 At(float u)
        {
            float a = u * Mathf.PI * 2f, r = RouteSize;
            switch (Path)
            {
                case Route.Eight: return Origin + new Vector3(Mathf.Sin(a) * r, 2f + Mathf.Sin(a * 2f) * 0.6f, Mathf.Sin(a) * Mathf.Cos(a) * r);
                case Route.Waves: return Origin + new Vector3(Mathf.Sin(a) * r * 1.4f, 2f + Mathf.Sin(a * 3f) * r * 0.45f, Mathf.Cos(a) * r * 0.8f);
                default: return Origin + new Vector3(Mathf.Sin(a) * r, 2f + Mathf.Sin(a * 2f) * 0.8f, Mathf.Cos(a) * r);
            }
        }

        private static float Nearest(Vector3 p)
        {
            float best = 0f, d = float.MaxValue;
            for (int i = 0; i < 64; i++) { float u = i / 64f, x = (At(u) - p).sqrMagnitude; if (x < d) { d = x; best = u; } }
            return best;
        }

        // each frame after the game's: one step of the flight, the wings, the camera
        internal static void Step()
        {
            if (_model == null || _motor == null || _motor.Body == null) return;
            if (Time.unscaledTime - _drawnAt > 1f || Player.m_localPlayer == null || _life == null || !ReferenceEquals(_life.Player, Player.m_localPlayer)) { Close(); return; } // the tab is not shown any more (or you died: made again for the new you)
            if (_seat != null && _seat.activeSelf) _seat.SetActive(false);
            if (TakeoffPreview)
            {
                if (_anim != null && _anim.enabled) _anim.enabled = false;
                _model.transform.position = Origin + Vector3.up * 2f;
                _model.transform.rotation = Quaternion.identity;
                _life.ApplyTakeoffPose(TakeoffPreviewPhase, true, 1f, true);
                Aim();
                return;
            }
            if (Replay != null) { PoseReplay(); Aim(); return; }
            float dt = Time.deltaTime * Slow;
            if (Slow <= 0f && StepOnce > 0f) { dt = StepOnce; StepOnce = 0f; }
            _life.TimeScale = Time.deltaTime > 0f ? dt / Time.deltaTime : 0f;
            _clock += dt;
            _motor.Body.NoGlide = Beating;
            Transform t = _model.transform;
            if ((t.position - Origin).sqrMagnitude > 2500f) { t.position = Origin + Vector3.up * 2f; _motor.Body.Vel = Vector3.forward * 4f; }
            FlightStyle s = _motor.Body.Style;
            if (Path == Route.Straight)
            {
                // straight on, level, at its cruise; far out it is put back (the camera follows it, nothing else to see)
                Vector3 p0 = t.position;
                if (p0.z - Origin.z > 40f) { t.position = new Vector3(Origin.x, p0.y, Origin.z - 30f); }
                Vector3 aim = new Vector3(Origin.x, Origin.y + 2f, t.position.z + 12f);
                _motor.Steer(aim, s.Cruise, false, 0f, dt, Vector3.zero, 0f);
                _motor.Finish(dt, 0f);
            }
            else if (Path == Route.Hover)
            {
                // hanging on one spot, turning slowly round, as over the shoulder before it lands
                Vector3 spot = Origin + Vector3.up * 2f + new Vector3(Mathf.Sin(_clock * 0.4f) * 0.4f, Mathf.Sin(_clock * 0.7f) * 0.25f, 0f);
                _motor.Steer(spot, 3f, true, 0f, dt, Vector3.zero, 0f);
                _motor.Body.FaceHint(Quaternion.Euler(0f, _clock * 20f, 0f) * Vector3.forward, 1f);
                _motor.Finish(dt, 0.7f);
            }
            else
            {
                float u = Nearest(t.position);
                float lead = Mathf.Clamp(s.Cruise * 0.45f / (RouteSize * 6.3f), 0.03f, 0.2f);
                _motor.Steer(At(u + lead), s.Cruise, false, 0f, dt, Vector3.zero, 0f);
                _motor.Finish(dt, 0f);
            }
            _life.Apply();
            Aim();
        }

        private static void Aim()
        {
            if (_cam == null || _model == null) return;
            Vector3 bird = _model.transform.position;
            if (Camera_ == View.Wide)
            {
                // the whole route in sight, from aside and above
                Quaternion look = Quaternion.Euler(Mathf.Max(Pitch, 20f), Yaw, 0f);
                Vector3 mid = Origin + Vector3.up * 2f;
                _cam.transform.position = mid - look * Vector3.forward * (RouteSize * 3.2f / Zoom);
                _cam.transform.rotation = look;
                return;
            }
            float yaw = Yaw;
            if (Camera_ == View.Behind && Replay == null) // (a replay has no flight to follow: round the bird)
            {
                Vector3 v = _motor.Body.Vel; v.y = 0f;
                if (v.sqrMagnitude > 0.25f) _camYaw = Mathf.LerpAngle(_camYaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, Mathf.Clamp01(Time.deltaTime * 2.5f));
                yaw = _camYaw + Yaw;
            }
            Quaternion q = Quaternion.Euler(Pitch, yaw, 0f);
            _cam.transform.position = bird - q * Vector3.forward * (_radius * 6f / Zoom);
            _cam.transform.rotation = q;
        }

        // the picture's speed, under it (both tabs): slower to look closely, held still, one step at a time
        internal static void SpeedRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Швидкість перегляду", GUILayout.Width(140f));
            float s = GUILayout.HorizontalSlider(Slow, 0f, 1f, GUILayout.Width(120f));
            if (Mathf.Abs(s - Slow) > 1e-4f) Slow = Mathf.Round(s * 20f) / 20f;
            GUILayout.Label(Slow <= 0f ? "стоп" : "×" + Slow.ToString("0.00"), GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Slow <= 0f ? "▶ Грати" : "Пауза", GUILayout.Width(70f))) Slow = Slow <= 0f ? 1f : 0f;
            GUI.enabled = Slow <= 0f;
            if (GUILayout.Button("кадр ▶", GUILayout.Width(64f))) StepOnce = 1f / 30f;
            GUI.enabled = true;
            if (GUILayout.Button("×0.1")) Slow = 0.1f;
            if (GUILayout.Button("×0.25")) Slow = 0.25f;
            if (GUILayout.Button("×0.5")) Slow = 0.5f;
            if (GUILayout.Button("×1")) Slow = 1f;
            GUILayout.EndHorizontal();
            Beating = GUILayout.Toggle(Beating, Beating ? " Змахи без зупинок (лише перегляд)" : " Змахи як у грі (з планеруванням)", "Button");
        }

        // what the picture's bird is doing now
        internal static string Status()
        {
            if (_motor == null || _motor.Body == null || _life == null) return "";
            FlightBody b = _motor.Body;
            return b.Speed.ToString("0.0") + " м/с · змахи ×" + _life.FlapRate.ToString("0.00") + (_life.FlapRate < 0.05f ? " (планерує)" : "")
                + " · крен " + _life.Flight.Bank.ToString("0") + "° · зусилля " + _life.Flight.Effort.ToString("0.00");
        }
    }

    // the stage's clock: steps the picture after the game's own animation each frame
    internal sealed class FlightStageRun : MonoBehaviour
    {
        private void LateUpdate() { try { FlightStage.Step(); } catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Flight picture failed: " + e.Message); FlightStage.Close(); } }
    }
}
#endif

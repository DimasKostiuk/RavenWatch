#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The «Розворот» tab's picture: a bar and a raven on it far up in the sky (y 9500), filmed by its own camera into a
    // texture. The raven is a perch's sitting copy (Perches.Sitter: PerchPose + PerchLife in stage mode) and plays the
    // turn round and round, with the tab's pace, side and hold. Drag turns the view, the wheel brings it closer.
    internal static class PerchTurnStage
    {
        private const int Size = 480;
        private static GameObject _stage, _seat, _bird;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static Light _key, _fill;
        private static int _layer = -1;
        private static PerchLife _life;
        private static float _again;
        internal static float Yaw = 140f, Pitch = 12f, Zoom = 1f;
        internal static bool Left, MuninShown;
        internal static bool Playing;          // ▶: the turn round and round; paused: the pose at PerchTurn.Hold
        internal static int PoseFrom = -1; // which perch's leg pose the raven sits with (-1: none, the game's own)
        internal static string Problem = "";
        internal static RenderTexture Texture { get { return _rt; } }

        private static bool Open()
        {
            if (_stage != null) return true;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; } // the world was unloaded under it
            _layer = RavenFx.FreeLayer();
            if (_layer < 0) { Problem = "Немає вільного шару для картинки."; return false; }
            _stage = new GameObject("RavenWatch_TurnStage");
            _stage.transform.position = new Vector3(0f, 9500f, 0f);
            // the bar: a plain dark rod across, the seat on its top
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(_stage.transform, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bar.transform.localScale = new Vector3(0.035f, 0.45f, 0.035f);
            bar.GetComponent<MeshRenderer>().sharedMaterial = WsCatalog.Mark;
            MaterialPropertyBlock pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", new Color(0.36f, 0.26f, 0.18f, 1f));
            bar.GetComponent<MeshRenderer>().SetPropertyBlock(pb);
            bar.layer = _layer;
            _seat = new GameObject("Seat");
            _seat.transform.SetParent(_stage.transform, false);
            _seat.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.cullingMask = 1 << _layer; _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.17f, 0.19f, 0.22f, 1f); _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.01f; _cam.farClipPlane = 50f; _cam.enabled = false;
            _cam.renderingPath = RenderingPath.Forward; _cam.allowHDR = false; _cam.allowMSAA = false;
            _key = Lamp(1.15f, Quaternion.Euler(45f, 150f, 0f));
            _fill = Lamp(0.55f, Quaternion.Euler(25f, -40f, 0f));
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); _rt.antiAliasing = 2; _rt.Create();
            _cam.targetTexture = _rt;
            Problem = "";
            return true;
        }

        private static Light Lamp(float power, Quaternion turn)
        {
            Light l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(_stage.transform, false);
            l.type = LightType.Directional; l.intensity = power; l.cullingMask = 1 << _layer; l.shadows = LightShadows.None;
            l.transform.rotation = turn; l.enabled = false;
            return l;
        }

        // ▶ pressed or the side changed: the turn from its start
        internal static void Restart() { if (_life != null) _life.StartPreview(Left); _again = 0f; }

        // the raven made again (another kind or another pose)
        internal static void Again() { if (_bird != null) Object.DestroyImmediate(_bird); _bird = null; _life = null; }

        private static void Bird()
        {
            if (_bird != null) return;
            float[] pose = null, stand = null;
            if (PoseFrom >= 0 && PoseFrom < Perches.Defs.Count) { pose = Perches.Defs[PoseFrom].Pose; stand = Perches.Defs[PoseFrom].Stand; }
            _bird = Perches.Sitter(_seat.transform, MuninShown, Shoulder.Scale * Shoulder.SizeFor(7), _layer, pose, stand);
            if (_bird == null) { Problem = "Ворона ще нема: увійди у світ."; return; }
            _life = _bird.AddComponent<PerchLife>();
            _life.Munin = MuninShown; _life.Level = 7; _life.Stage = true;
            PerchPose pp = _bird.GetComponent<PerchPose>();
            if (pp != null) { pp.Life = _life; _life.Pose = pp; }
            _again = 0f;
        }

        // each frame the tab is drawn: the turn kept going round, the picture taken
        internal static void Render()
        {
            if (!Open()) return;
            Bird();
            if (_life != null)
            {
                if (!Playing) { if (!_life.Turning) _life.StartPreview(Left); } // paused: held at the chosen moment (PerchTurn.Hold)
                else if (!_life.Turning && Time.time >= _again)
                {
                    if (_again <= 0f) _again = Time.time + 0.8f; // a short pause between two turns
                    else { _life.StartPreview(Left); _again = 0f; }
                }
            }
            Quaternion look = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 c = _stage.transform.position + Vector3.up * 0.18f;
            _cam.transform.position = c - look * Vector3.forward * (1.5f / Zoom);
            _cam.transform.rotation = look;
            _key.enabled = true; _fill.enabled = true;
            try { _cam.Render(); }
            finally { _key.enabled = false; _fill.enabled = false; }
        }

        internal static void Close()
        {
            if (_stage != null) Object.Destroy(_stage);
            _stage = null; _seat = null; _bird = null; _life = null; _cam = null;
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); _rt = null; }
        }
    }
}
#endif

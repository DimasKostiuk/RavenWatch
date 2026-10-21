#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // An item icon photographed from the raven's own model, the way the game's icons look.
    // The model is set up far above the world on a layer nothing else uses, lit by its own lights
    // and shot twice (on black and on white): the difference gives clean transparent edges
    // whatever the game's shaders write into the alpha channel.
    internal static class IconRender
    {
        private const int Size = 256; // the shot
        private const int Out = 128;  // the icon, the raven cut out of the shot to fill it

        internal static string Why = ""; // what happened on the last try, for the log and the console

        internal static Sprite Render(bool munin)
        {
            Raven src = Shoulder.Template(munin);
            if (src == null || src.m_visual == null || src.m_isMunin != munin) { Why = "the raven model is not loaded yet"; return null; }
            int layer = RavenFx.FreeLayer();
            if (layer < 0) { Why = "no free layer"; return null; }

            GameObject stage = new GameObject("RavenWatch_IconStage");
            RenderTexture rt = null;
            bool fog = RenderSettings.fog;
            UnityEngine.Rendering.AmbientMode ambMode = RenderSettings.ambientMode;
            Color amb = RenderSettings.ambientLight;
            try
            {
                stage.transform.position = new Vector3(0f, 9000f, 0f);
                // The raven on your shoulder is the best model: landed and posed. The game's own template is kept
                // "away" (its bones squashed to nothing until it lands), so from there the squashed bones are opened up.
                GameObject perched = Shoulder.LocalModel(munin);
                GameObject model = UnityEngine.Object.Instantiate(perched != null ? perched : src.m_visual, stage.transform, false);
                AppearMute.Quiet(model);
                foreach (ShoulderPin pin in model.GetComponentsInChildren<ShoulderPin>(true)) UnityEngine.Object.DestroyImmediate(pin);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = src.m_visual.transform.localScale;
                model.SetActive(true);
                Shared.RemoveExclamation(src, model);
                foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false; // hold the pose
                if (perched == null)
                    foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                        if (t != model.transform && t.localScale.sqrMagnitude < 1e-6f) t.localScale = Vector3.one;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);

                Bounds b = new Bounds(model.transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                    r.enabled = true;
                    SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
                    if (sk != null) sk.updateWhenOffscreen = true;
                    if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
                }
                if (!any) { Why = "the model has no visible parts"; return null; }
                // a skinned model may not know its size before it was ever drawn: then a raven-sized frame
                if (b.size.magnitude < 0.05f || b.size.magnitude > 10f) b = new Bounds(model.transform.position + Vector3.up * 0.3f, Vector3.one * 0.6f);
                foreach (Transform t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

                // a three-quarter view from the front, the way the game shows creatures' trophies
                Vector3 front = -model.transform.forward; // the raven model looks along its back axis
                Vector3 dir = (Quaternion.AngleAxis(-35f, Vector3.up) * front + Vector3.up * 0.25f).normalized;
                Camera cam = new GameObject("Cam").AddComponent<Camera>();
                cam.transform.SetParent(stage.transform, false);
                cam.enabled = false;
                cam.cullingMask = 1 << layer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.fieldOfView = 25f;
                cam.renderingPath = RenderingPath.Forward;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                float radius = Mathf.Max(0.05f, b.extents.magnitude);
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
                cam.farClipPlane = dist + radius * 2f;
                cam.transform.position = b.center + dir * dist;
                cam.transform.LookAt(b.center);

                Light key = new GameObject("Key").AddComponent<Light>();
                key.transform.SetParent(stage.transform, false);
                key.type = LightType.Directional; key.intensity = 1.2f; key.color = new Color(1f, 0.96f, 0.9f); key.cullingMask = 1 << layer;
                key.transform.rotation = Quaternion.LookRotation(-(dir + Vector3.up * 0.6f + cam.transform.right * 0.5f).normalized);
                Light fill = new GameObject("Fill").AddComponent<Light>();
                fill.transform.SetParent(stage.transform, false);
                fill.type = LightType.Directional; fill.intensity = 0.5f; fill.color = new Color(0.8f, 0.85f, 1f); fill.cullingMask = 1 << layer;
                fill.transform.rotation = Quaternion.LookRotation(-(dir - cam.transform.right * 0.8f).normalized);

                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.48f);

                rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                Color32[] onBlack = Shot(cam, rt, Color.black);
                Color32[] onWhite = Shot(cam, rt, Color.white);
                cam.targetTexture = null;

                Color32[] px = new Color32[onBlack.Length];
                int solid = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    // seen on black = c·a, seen on white = c·a + (1 − a)
                    float a = 1f - ((onWhite[i].r - onBlack[i].r) + (onWhite[i].g - onBlack[i].g) + (onWhite[i].b - onBlack[i].b)) / (3f * 255f);
                    a = Mathf.Clamp01(a);
                    if (a < 0.01f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                    solid++;
                    px[i] = new Color32((byte)Mathf.Min(255f, onBlack[i].r / a), (byte)Mathf.Min(255f, onBlack[i].g / a), (byte)Mathf.Min(255f, onBlack[i].b / a), (byte)(a * 255f));
                }
                if (solid < Size * Size / 400) { Why = "the picture came out empty (" + solid + " pixels, model from " + (perched != null ? "the shoulder" : "the template") + ")"; return null; } // keep the old icon

                px = Fit(px);
                Texture2D tex = new Texture2D(Out, Out, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.SetPixels32(px);
                tex.Apply();
                Sprite sp = Sprite.Create(tex, new Rect(0, 0, Out, Out), new Vector2(0.5f, 0.5f), 100f);
                sp.name = munin ? "RavenWatch_MuninModelIcon" : "RavenWatch_HuginModelIcon";
                sp.hideFlags = HideFlags.HideAndDontSave;
                Why = "ok (" + solid + " pixels, model from " + (perched != null ? "the shoulder" : "the template") + ")";
                return sp;
            }
            catch (Exception e)
            {
                Why = "error: " + e.Message;
                return null;
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambMode;
                RenderSettings.ambientLight = amb;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(stage);
            }
        }

        // the smallest square around the raven, with a thin margin, scaled to fill the icon
        private static Color32[] Fit(Color32[] px)
        {
            int minX = Size, minY = Size, maxX = -1, maxY = -1;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (px[y * Size + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) return Scale(px, 0f, 0f, Size);
            float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 1.08f;
            float cx = (minX + maxX + 1) * 0.5f, cy = (minY + maxY + 1) * 0.5f;
            return Scale(px, cx - side * 0.5f, cy - side * 0.5f, side);
        }

        // the square (x0, y0, side) of the shot into the icon, each icon pixel averaging what it covers
        private static Color32[] Scale(Color32[] px, float x0, float y0, float side)
        {
            Color32[] o = new Color32[Out * Out];
            float step = side / Out;
            int sub = Mathf.Clamp(Mathf.CeilToInt(step), 1, 4);
            for (int y = 0; y < Out; y++)
                for (int x = 0; x < Out; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0; int n = 0;
                    for (int sy = 0; sy < sub; sy++)
                        for (int sx = 0; sx < sub; sx++)
                        {
                            int ix = Mathf.FloorToInt(x0 + (x + (sx + 0.5f) / sub) * step), iy = Mathf.FloorToInt(y0 + (y + (sy + 0.5f) / sub) * step);
                            n++;
                            if (ix < 0 || iy < 0 || ix >= Size || iy >= Size) continue;
                            Color32 c = px[iy * Size + ix];
                            float ca = c.a / 255f;
                            r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca; // colours weighted by cover, no dark fringe
                        }
                    if (a <= 0f) { o[y * Out + x] = new Color32(0, 0, 0, 0); continue; }
                    o[y * Out + x] = new Color32((byte)Mathf.Min(255f, r / a), (byte)Mathf.Min(255f, g / a), (byte)Mathf.Min(255f, b / a), (byte)Mathf.Min(255f, a / n * 255f));
                }
            return o;
        }

        private static Color32[] Shot(Camera cam, RenderTexture rt, Color background)
        {
            cam.backgroundColor = background;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            Color32[] px = read.GetPixels32();
            UnityEngine.Object.Destroy(read);
            return px;
        }

    }
}
#endif

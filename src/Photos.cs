#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // The pictures of the Workshop's models (a perch and a raven table in the hammer, a thing for a raven in the book,
    // an item in the inventory). Taken by hand in the test window's tab «Фото» (PhotoPanel) and kept as
    // config/RavenWatch/photos/<id>.png (+ <id>.txt, the camera it was taken with); the mod's own ones are inside the
    // DLL (assets/photos/<id>.png). A model without a picture is photographed by itself (Auto), the same way.
    //
    // A shot is taken twice, on black and on white: the difference gives clean transparent edges whatever the game's
    // shaders write into the alpha channel (some write 0 there, and such a model came out invisible).
    internal sealed class PhotoSet
    {
        internal float Yaw = 200f, Pitch = 18f, Roll = 0f;  // the camera's turn round the model
        internal float Zoom = 1f, PanX, PanY;                 // 1: the whole model fits; the shift in frames
        internal float Fov = 25f;                             // perspective (degrees); 0: flat, no perspective
        internal float Light = 1f, LightTurn = 0f, Ambient = 0.45f;
        internal bool Trim = true;                            // the empty margins cut off, the model filling the icon

        internal PhotoSet Clone() { return (PhotoSet)MemberwiseClone(); }

        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static string F(float v) { return v.ToString("0.###", C); }

        internal string ToLine()
        {
            return "photo: " + F(Yaw) + " " + F(Pitch) + " " + F(Roll) + " " + F(Zoom) + " " + F(PanX) + " " + F(PanY) + " " + F(Fov)
                + " " + F(Light) + " " + F(LightTurn) + " " + F(Ambient) + " " + (Trim ? "trim" : "full");
        }

        internal static PhotoSet Parse(string text)
        {
            PhotoSet s = new PhotoSet();
            if (string.IsNullOrEmpty(text)) return s;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("photo:")) continue;
                string[] w = line.Substring(6).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                float[] v = { s.Yaw, s.Pitch, s.Roll, s.Zoom, s.PanX, s.PanY, s.Fov, s.Light, s.LightTurn, s.Ambient };
                for (int i = 0; i < w.Length && i < v.Length; i++) float.TryParse(w[i], NumberStyles.Float, C, out v[i]);
                s.Yaw = v[0]; s.Pitch = v[1]; s.Roll = v[2]; s.Zoom = Mathf.Max(0.05f, v[3]); s.PanX = v[4]; s.PanY = v[5]; s.Fov = v[6];
                s.Light = v[7]; s.LightTurn = v[8]; s.Ambient = v[9];
                if (w.Length > 10) s.Trim = w[10] != "full";
            }
            return s;
        }
    }

    // A small studio far above the world: the subject on a layer nothing else uses, its own camera and lamps.
    // The lamps are on only while it shoots (the free layer is not surely unused: the game draws grass on such).
    internal sealed class PhotoStudio
    {
        internal const int Size = 512; // the shot; the icon is cut out of it
        internal readonly GameObject Root;
        internal readonly Transform Subject;
        internal readonly Camera Cam;
        internal readonly RenderTexture Rt;
        internal readonly int Layer;
        internal Vector3 Center;   // the subject's middle (in the studio)
        internal float Radius = 1f;
        private readonly Light _key, _fill, _rim;

        internal PhotoStudio(string name, Vector3 at, int layer)
        {
            Layer = layer;
            Root = new GameObject(name);
            Root.transform.position = at;
            Subject = new GameObject("Subject").transform;
            Subject.SetParent(Root.transform, false);
            Cam = new GameObject("Cam").AddComponent<Camera>();
            Cam.transform.SetParent(Root.transform, false);
            Cam.enabled = false; Cam.cullingMask = 1 << layer; Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.renderingPath = RenderingPath.Forward; Cam.allowHDR = false; Cam.allowMSAA = true;
            _key = Lamp("Key", new Color(1f, 0.96f, 0.9f));
            _fill = Lamp("Fill", new Color(0.8f, 0.85f, 1f));
            _rim = Lamp("Rim", Color.white);
            Rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            Rt.antiAliasing = 4;
            Rt.Create();
            Cam.targetTexture = Rt;
        }

        private Light Lamp(string name, Color c)
        {
            Light l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(Root.transform, false);
            l.type = LightType.Directional; l.color = c; l.cullingMask = 1 << Layer; l.shadows = LightShadows.None; l.enabled = false;
            return l;
        }

        // everything under the subject on the studio's layer, no colliders, no particles
        internal void Prepare()
        {
            foreach (Transform t in Subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            foreach (Collider c in Subject.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (ParticleSystemRenderer r in Subject.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.enabled = false;
        }

        // the subject's size: false when nothing of it is seen
        internal bool Measure()
        {
            Bounds? b = null;
            foreach (Renderer r in Subject.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (b == null) b = r.bounds; else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
            }
            if (b == null || b.Value.size.sqrMagnitude < 1e-8f) { Center = Vector3.zero; Radius = 0.5f; return false; }
            Center = Root.transform.InverseTransformPoint(b.Value.center);
            Radius = Mathf.Max(0.02f, b.Value.extents.magnitude);
            return true;
        }

        // the camera and the lamps for these settings
        internal void Aim(PhotoSet s)
        {
            Quaternion look = Quaternion.Euler(s.Pitch, s.Yaw, s.Roll);
            float half = Radius / Mathf.Max(0.05f, s.Zoom);
            Vector3 c = Root.transform.TransformPoint(Center) - look * new Vector3(s.PanX, s.PanY, 0f) * half;
            float dist;
            if (s.Fov < 1f)
            {
                Cam.orthographic = true;
                Cam.orthographicSize = half;
                dist = Radius * 3f + 1f;
            }
            else
            {
                Cam.orthographic = false;
                Cam.fieldOfView = Mathf.Clamp(s.Fov, 1f, 90f);
                dist = half / Mathf.Sin(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            }
            Cam.nearClipPlane = Mathf.Max(0.005f, dist - Radius * 3f);
            Cam.farClipPlane = dist + Radius * 3f + 1f;
            Cam.transform.position = c - look * Vector3.forward * dist;
            Cam.transform.rotation = look;
            _key.transform.rotation = look * Quaternion.Euler(30f, -40f + s.LightTurn, 0f);
            _fill.transform.rotation = look * Quaternion.Euler(10f, 55f + s.LightTurn, 0f);
            _rim.transform.rotation = look * Quaternion.Euler(25f, 180f + s.LightTurn * 0.5f, 0f);
            _key.intensity = 1.2f * s.Light; _fill.intensity = 0.45f * s.Light; _rim.intensity = 0.5f * s.Light;
        }

        // one picture into Rt, on this background, no fog, the studio's own soft light around
        internal void Shoot(PhotoSet s, Color background)
        {
            bool fog = RenderSettings.fog;
            UnityEngine.Rendering.AmbientMode mode = RenderSettings.ambientMode;
            Color amb = RenderSettings.ambientLight;
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(s.Ambient, s.Ambient, s.Ambient * 1.05f);
                _key.enabled = _fill.enabled = _rim.enabled = true;
                Cam.backgroundColor = background;
                Cam.Render();
            }
            finally
            {
                _key.enabled = _fill.enabled = _rim.enabled = false;
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = mode;
                RenderSettings.ambientLight = amb;
            }
        }

        // the clean picture: on black and on white, the colour and the cover worked out from both
        internal Color32[] Clean(PhotoSet s, out int solid)
        {
            Aim(s);
            Color32[] black = Read(s, Color.black), white = Read(s, Color.white);
            Color32[] px = new Color32[black.Length];
            solid = 0;
            for (int i = 0; i < px.Length; i++)
            {
                // seen on black = c·a, seen on white = c·a + (1 − a)
                float a = 1f - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / (3f * 255f);
                a = Mathf.Clamp01(a);
                if (a < 0.01f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                solid++;
                px[i] = new Color32((byte)Mathf.Min(255f, black[i].r / a), (byte)Mathf.Min(255f, black[i].g / a), (byte)Mathf.Min(255f, black[i].b / a), (byte)(a * 255f));
            }
            return px;
        }

        private Color32[] Read(PhotoSet s, Color background)
        {
            Shoot(s, background);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = Rt;
            Texture2D read = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            Color32[] px = read.GetPixels32();
            UnityEngine.Object.Destroy(read);
            return px;
        }

        internal void Dispose()
        {
            if (Cam != null) Cam.targetTexture = null;
            if (Rt != null) { Rt.Release(); UnityEngine.Object.Destroy(Rt); }
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }
    }

    internal static class IconPhotos
    {
        internal const int Out = 128; // the icon (mipmapped: the inventory draws it smaller, smoothly)
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "photos"); } }
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Photos: " + s); }

        // the id a model's picture goes by: the same as the piece's / thing's / item's id
        internal static string KeyOf(WsModel m)
        {
            if (m == null) return "";
            if (string.IsNullOrEmpty(m.Id)) return Perches.IdFor(m.Name);
            if (m.Id == Perches.IdFor("") && (m.Use == RavenGear.Use || m.Use == WsItems.Use)) return Perches.IdFor(m.Name); // the Workshop's stand-in
            return m.Id;
        }

        internal static string FileOf(string id) { return Path.Combine(Dir, id + ".png"); }
        internal static bool HasOwn(string id) { return !string.IsNullOrEmpty(id) && File.Exists(FileOf(id)); }

        // the picture taken by hand (the folder's first, then the mod's own); null: none, photographed by itself
        internal static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Sprite s;
            if (Cache.TryGetValue(id, out s)) return s;
            s = null;
            try
            {
                byte[] data = null;
                if (File.Exists(FileOf(id))) data = File.ReadAllBytes(FileOf(id));
                else
                    using (Stream st = typeof(IconPhotos).Assembly.GetManifestResourceStream("RavenWatch.photos." + id + ".png"))
                        if (st != null) { data = new byte[st.Length]; int read = 0; while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; } }
                if (data != null)
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (AbilityIcons.Decode(tex, data)) { tex.filterMode = FilterMode.Trilinear; s = SpriteOf(tex, id); }
                    else UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception e) { Log(id + ": " + e.Message); }
            Cache[id] = s;
            return s;
        }

        internal static Sprite SpriteOf(Texture2D tex, string id)
        {
            tex.hideFlags = HideFlags.HideAndDontSave; // the game's asset cleanup on world load must not take it
            Sprite s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "RavenWatch_photo_" + id;
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        // the settings it was taken with (default ones when none)
        internal static PhotoSet SettingsOf(string id)
        {
            try { string f = Path.Combine(Dir, id + ".txt"); if (File.Exists(f)) return PhotoSet.Parse(File.ReadAllText(f)); }
            catch (Exception e) { Log(id + " settings: " + e.Message); }
            return new PhotoSet();
        }

        // a picture taken by hand: into the folder, and at once into the hammer / the book / the inventory
        internal static bool Save(string id, Texture2D icon, PhotoSet s, string name)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(FileOf(id), ImageConversion.EncodeToPNG(icon));
                File.WriteAllText(Path.Combine(Dir, id + ".txt"), "# RavenWatch: як знято фото «" + name + "»\n" + s.ToLine() + "\n", new System.Text.UTF8Encoding(false));
            }
            catch (Exception e) { Log("save " + id + ": " + e.Message); return false; }
            Texture2D copy = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, true);
            copy.SetPixels32(icon.GetPixels32());
            copy.Apply(true);
            copy.filterMode = FilterMode.Trilinear;
            Cache[id] = SpriteOf(copy, id);
            Changed(id);
            return true;
        }

        // the own picture thrown away: the mod's one, or photographed by itself again
        internal static void Remove(string id)
        {
            try
            {
                if (File.Exists(FileOf(id))) File.Delete(FileOf(id));
                string t = Path.Combine(Dir, id + ".txt");
                if (File.Exists(t)) File.Delete(t);
            }
            catch (Exception e) { Log("remove " + id + ": " + e.Message); }
            Cache.Remove(id);
            Changed(id);
        }

        private static void Changed(string id)
        {
            try { Perches.IconAgain(id); } catch (Exception e) { Log("perch icon: " + e.Message); }
            try { RavenGear.IconAgain(id); } catch (Exception e) { Log("gear icon: " + e.Message); }
            try { WsItems.IconAgain(id); } catch (Exception e) { Log("item icon: " + e.Message); }
        }

        // ---------------------------------------------------------------- the picture made of the shot

        // the shot into the icon: the smallest square round the model (trim) or the whole frame, each icon pixel
        // averaging what it covers
        internal static Texture2D Finish(Color32[] px, bool trim)
        {
            const int S = PhotoStudio.Size;
            float x0 = 0f, y0 = 0f, side = S;
            if (trim)
            {
                int minX = S, minY = S, maxX = -1, maxY = -1;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                        if (px[y * S + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
                if (maxX >= 0)
                {
                    side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * 1.06f;
                    x0 = (minX + maxX + 1) * 0.5f - side * 0.5f; y0 = (minY + maxY + 1) * 0.5f - side * 0.5f;
                }
            }
            Color32[] o = new Color32[Out * Out];
            float step = side / Out;
            int sub = Mathf.Clamp(Mathf.CeilToInt(step), 1, 6);
            for (int y = 0; y < Out; y++)
                for (int x = 0; x < Out; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0; int n = 0;
                    for (int sy = 0; sy < sub; sy++)
                        for (int sx = 0; sx < sub; sx++)
                        {
                            int ix = Mathf.FloorToInt(x0 + (x + (sx + 0.5f) / sub) * step), iy = Mathf.FloorToInt(y0 + (y + (sy + 0.5f) / sub) * step);
                            n++;
                            if (ix < 0 || iy < 0 || ix >= S || iy >= S) continue;
                            Color32 c = px[iy * S + ix];
                            float ca = c.a / 255f;
                            r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca; // colours weighted by cover: no dark fringe
                        }
                    if (a <= 0f) { o[y * Out + x] = new Color32(0, 0, 0, 0); continue; }
                    o[y * Out + x] = new Color32((byte)Mathf.Min(255f, r / a), (byte)Mathf.Min(255f, g / a), (byte)Mathf.Min(255f, b / a), (byte)Mathf.Min(255f, a / n * 255f));
                }
            Texture2D tex = new Texture2D(Out, Out, TextureFormat.RGBA32, true);
            tex.SetPixels32(o);
            tex.Apply(true);
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        // ---------------------------------------------------------------- photographed by itself

        // a model with no picture taken: a three-quarter view from the front and a little above, filling the icon
        internal static Sprite Auto(Transform look)
        {
            if (look == null) return null;
            int layer = RavenFx.FreeLayer();
            if (layer < 0) return null;
            PhotoStudio st = null;
            try
            {
                st = new PhotoStudio("RavenWatch_AutoPhoto", new Vector3(0f, 9600f, 0f), layer);
                GameObject copy = UnityEngine.Object.Instantiate(look.gameObject, st.Subject, false);
                copy.SetActive(true);
                st.Prepare();
                if (!st.Measure()) return null;
                PhotoSet s = new PhotoSet();
                int solid;
                Color32[] px = st.Clean(s, out solid);
                if (solid < PhotoStudio.Size * PhotoStudio.Size / 2000) { Log(look.name + ": the picture came out empty (" + solid + " pixels)"); return null; }
                Texture2D tex = Finish(px, true);
                return SpriteOf(tex, look.name);
            }
            catch (Exception e) { Log("auto " + look.name + ": " + e.Message); return null; }
            finally
            {
                if (st != null)
                {
                    // gone at once: the next one is set up in the same place
                    st.Cam.targetTexture = null;
                    st.Rt.Release(); UnityEngine.Object.Destroy(st.Rt);
                    UnityEngine.Object.DestroyImmediate(st.Root);
                }
            }
        }
    }
}
#endif

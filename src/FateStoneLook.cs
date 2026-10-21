#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // The Fate stone is the game's Thunderstone, made violet (Hugin's blue and Munin's red together): its glow, its own
    // particles and its light, plus slow violet sparks rising from it while it lies on the ground. Done once on the
    // stone's template, so every stone in the world and in hands looks the same; the real Thunderstone is untouched.
    internal static class FateStoneLook
    {
        internal static Color Violet = new Color(0.62f, 0.25f, 1f);
        private static Material _spark;

        internal static void Dress(GameObject go)
        {
            if (go == null) return;
            try
            {
                int mats = 0, glows = 0, parts = 0, lights = 0;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    Material[] list = r.sharedMaterials;
                    Material[] made = new Material[list.Length];
                    for (int i = 0; i < list.Length; i++) { made[i] = Recolor(list[i], ref glows); if (made[i] != null) mats++; }
                    r.sharedMaterials = made;
                }
                foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(Violet, Color.Lerp(Violet, Color.white, 0.4f));
                    parts++;
                }
                foreach (Light l in go.GetComponentsInChildren<Light>(true)) { l.color = Violet; lights++; }
                if (lights == 0) AddLight(go);
                AddSparks(go);
                if (Plugin.Instance != null) Plugin.Instance.Log("Fate stone: violet, materials " + mats + " (glowing " + glows + "), particles " + parts + ", lights " + (lights > 0 ? lights.ToString() : "added"));
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Fate stone look failed: " + e.Message); }
        }

        // a copy with a violet glow: the glow picture turned grey first, or its own colour would mix with the violet
        private static Material Recolor(Material m, ref int glows)
        {
            if (m == null) return null;
            Material c = new Material(m);
            c.name = m.name + " (fate)";
            c.hideFlags = HideFlags.HideAndDontSave;
            if (c.HasProperty("_EmissionColor"))
            {
                Color e = m.GetColor("_EmissionColor");
                float k = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                if (k > 0.01f) { c.SetColor("_EmissionColor", Violet * k); glows++; }
            }
            if (c.HasProperty("_EmissionMap"))
            {
                Texture t = m.GetTexture("_EmissionMap");
                Texture2D grey = t != null ? Grey(t) : null;
                if (grey != null) c.SetTexture("_EmissionMap", grey);
            }
            if (c.HasProperty("_Color"))
            {
                Color col = m.color;
                Color tinted = Color.Lerp(col, Violet, 0.35f);
                tinted.a = col.a;
                c.color = tinted;
            }
            return c;
        }

        // the glow picture as greys (by its brightest channel, so the strength stays)
        private static Texture2D Grey(Texture src)
        {
            RenderTexture rt = null, prev = RenderTexture.active;
            try
            {
                int w = Mathf.Clamp(src.width, 4, 512), h = Mathf.Clamp(src.height, 4, 512);
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                Color32[] px = t.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    byte v = (byte)Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b));
                    px[i] = new Color32(v, v, v, px[i].a);
                }
                t.SetPixels32(px);
                t.Apply(true);
                t.hideFlags = HideFlags.HideAndDontSave;
                return t;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Fate stone glow picture: " + e.Message); return null; }
            finally { RenderTexture.active = prev; if (rt != null) RenderTexture.ReleaseTemporary(rt); }
        }

        private static void AddLight(GameObject go)
        {
            GameObject g = new GameObject("RavenWatch_FateLight");
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Violet;
            l.range = 2f;
            l.intensity = 1.2f;
            l.shadows = LightShadows.None;
        }

        // slow violet sparks drifting up from the stone
        private static void AddSparks(GameObject go)
        {
            GameObject g = new GameObject("RavenWatch_FateSparks");
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(Violet, Color.Lerp(Violet, Color.white, 0.5f));
            main.gravityModifier = -0.04f; // they float up
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 6f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f;
            Gradient fade = new Gradient();
            fade.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(fade);
            Material m = Spark();
            if (m != null) g.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
        }

        // a soft round dot that adds light (a glowing spark)
        private static Material Spark()
        {
            if (_spark != null) return _spark;
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            const int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - n / 2f) / (n / 2f), dy = (y + 0.5f - n / 2f) / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            _spark = new Material(sh);
            _spark.mainTexture = t;
            _spark.hideFlags = HideFlags.HideAndDontSave;
            return _spark;
        }
    }
}
#endif

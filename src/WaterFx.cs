#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // The water stirred by a raven (the user, 2026-10-28: «хвилі і тряска води»): rings spreading over the water's face
    // and drops thrown up by its wings, while it bathes (PerchBath), washes in your hands, or dives for a fish
    // (FishPlan). Our own two particle systems in the world, shared by every raven; each game draws its own.
    internal static class WaterFx
    {
        internal static float RingSize = 0.55f, RingLife = 1.4f, DropSpeed = 1.6f;
        private static ParticleSystem _rings, _drops;
        private static bool _failed;

        // the water's face by a bathing raven (it sits Bath.Sink in it; on a shallow bottom by the sea, the sea's level)
        internal static float FaceAt(Vector3 raven)
        {
            float y = raven.y + Bath.Sink;
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : y;
            return Mathf.Abs(y - sea) < 0.3f ? sea : y;
        }

        // the water's face at that point (with its waves): the water volume over it, else the sea's level
        internal static float SurfaceAt(Vector3 at)
        {
            bool calm;
            WaterVolume w = VolumeAt(at, out calm);
            return w != null ? w.GetWaterSurface(at, calm ? 0f : 1f) : ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : at.y; // (a tub's water: no sea waves)
        }

        // the water there: a building's own (the hot tub: calm) before the sea's tile under the same spot
        internal static WaterVolume VolumeAt(Vector3 at, out bool calm)
        {
            calm = false;
            WaterVolume sea = null;
            foreach (WaterVolume w in WaterVolume.Instances)
            {
                Vol v = Of(w);
                if (v == null) continue;
                Bounds b = v.Box.bounds;
                if (at.x < b.min.x || at.x > b.max.x || at.z < b.min.z || at.z > b.max.z) continue;
                if (!v.Built) { if (sea == null) sea = w; continue; }
                b.Expand(0.6f);
                if (b.Contains(at)) { calm = true; return w; }
            }
            return sea;
        }

        // each water's collider and whether a building holds it, looked up once (gone with it)
        private sealed class Vol { internal Collider Box; internal bool Built; }
        private static readonly System.Collections.Generic.Dictionary<WaterVolume, Vol> Vols = new System.Collections.Generic.Dictionary<WaterVolume, Vol>();
        private static Vol Of(WaterVolume w)
        {
            if (w == null) return null;
            Vol v;
            if (Vols.TryGetValue(w, out v)) return v != null && v.Box != null ? v : null;
            if (Vols.Count > 256) { var dead = new System.Collections.Generic.List<WaterVolume>(); foreach (WaterVolume k in Vols.Keys) if (k == null) dead.Add(k); foreach (WaterVolume k in dead) Vols.Remove(k); }
            Collider c = w.GetComponent<Collider>();
            v = c != null ? new Vol { Box = c, Built = w.GetComponentInParent<Piece>() != null } : null;
            Vols[w] = v;
            return v;
        }

        // something solid lies on the water there: ice (a frozen lake: the mod Seasons), a pier, a floor, a ship's deck — no
        // bath and no dive through it (the ground itself is no cover: a shore is looked at by its own height)
        private static int _solidMask;
        internal static bool Covered(Vector3 at, float water)
        {
            if (_solidMask == 0) _solidMask = LayerMask.GetMask("Default", "static_solid", "piece", "Default_small", "vehicle");
            RaycastHit h;
            return Physics.Raycast(new Vector3(at.x, water + 3f, at.z), Vector3.down, out h, 3.2f, _solidMask, QueryTriggerInteraction.Ignore) && h.point.y > water - 0.1f;
        }

        // a ring spreading out from there (big: a dive)
        internal static void Ring(Vector3 at, float size)
        {
            if (!Make()) return;
            ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
            e.position = at + Vector3.up * 0.02f; e.applyShapeToPosition = false;
            e.startSize = RingSize * size; e.startLifetime = RingLife * Mathf.Lerp(1f, 1.4f, size - 1f);
            e.startColor = new Color(0.85f, 0.93f, 1f, 0.55f);
            e.velocity = Vector3.zero;
            _rings.Emit(e, 1);
        }

        // drops thrown up off the water round it
        internal static void Spray(Vector3 at, int n, float power)
        {
            if (!Make()) return;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle;
                ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
                e.position = at + new Vector3(c.x, 0.03f, c.y) * 0.15f; e.applyShapeToPosition = false;
                e.velocity = (new Vector3(c.x, 0f, c.y) * 0.7f + Vector3.up * UnityEngine.Random.Range(0.6f, 1f)) * DropSpeed * power;
                e.startSize = UnityEngine.Random.Range(0.014f, 0.03f);
                e.startLifetime = UnityEngine.Random.Range(0.45f, 0.75f);
                e.startColor = new Color(0.86f, 0.94f, 1f, UnityEngine.Random.Range(0.6f, 0.9f));
                _drops.Emit(e, 1);
            }
        }

        private static bool Make()
        {
            if (_failed) return false;
            if (_rings != null && _drops != null) return true;
            try
            {
                GameObject g = new GameObject("RavenWatch_WaterFx");
                UnityEngine.Object.DontDestroyOnLoad(g);

                // the rings: flat on the water, growing and fading
                GameObject rg = new GameObject("Rings");
                rg.transform.SetParent(g.transform, false);
                _rings = Sys(rg, 60, 0f);
                ParticleSystem.SizeOverLifetimeModule sz = _rings.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1f, 0.6f, 0f)));
                ParticleSystem.ColorOverLifetimeModule rc = _rings.colorOverLifetime; rc.enabled = true;
                rc.color = new ParticleSystem.MinMaxGradient(Fade(0.1f));
                ParticleSystemRenderer rr = rg.GetComponent<ParticleSystemRenderer>();
                rr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                Material ring = RingMat();
                if (ring != null) rr.sharedMaterial = ring;

                // the drops: up and down again
                GameObject dg = new GameObject("Drops");
                dg.transform.SetParent(g.transform, false);
                _drops = Sys(dg, 150, 1f);
                ParticleSystem.ColorOverLifetimeModule dc = _drops.colorOverLifetime; dc.enabled = true;
                dc.color = new ParticleSystem.MinMaxGradient(Fade(0.6f));
                ParticleSystemRenderer dr = dg.GetComponent<ParticleSystemRenderer>();
                Material dot = RavenAura.Dot();
                if (dot != null) dr.sharedMaterial = dot;
                return true;
            }
            catch (Exception e) { _failed = true; if (Plugin.Instance != null) Plugin.Instance.Log("Raven water fx failed: " + e.Message); return false; }
        }

        private static ParticleSystem Sys(GameObject g, int most, float gravity)
        {
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule m = ps.main;
            m.loop = true; m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Shape;
            m.maxParticles = most;
            m.gravityModifier = gravity;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
            ps.Play();
            return ps;
        }

        // seen at once, gone toward its end
        private static Gradient Fade(float outFrom)
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // a thin soft ring on a see-through material
        private static Material _ringMat;
        private static Material RingMat()
        {
            if (_ringMat != null) return _ringMat;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f - n / 2f) / (n / 2f), dy = (y + 0.5f - n / 2f) / (n / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.09f); // a band near the edge
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            _ringMat = new Material(sh);
            _ringMat.mainTexture = t;
            _ringMat.hideFlags = HideFlags.HideAndDontSave;
            return _ringMat;
        }
    }
}
#endif

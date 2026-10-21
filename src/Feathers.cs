#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Our feather effects, all on our own feather sheet (assets/feathers.png). The shadow's:
    //   In    — they fly in from every side and wrap you (the feather burst backwards); every game sees it
    //   Orbit — while it lasts, a few circle slowly round you; only you see them (others must not find you by them)
    //   the end keeps the burst outwards (Shadow.Spawn), and the circling ones fly off with it
    internal static class Feathers
    {
        internal static int InCount = 36;
        internal static float InSeconds = 1.7f, InSize = 2f; // how long they take to land on you; their size against the rest
        internal static float OrbitPerSecond = 4f;
        private static Material _mat;
        private static int _tilesX = 1, _tilesY = 1;
        private static bool _looked;

        // Our own feathers (assets/feathers.png, drawn by tools/Feathers.cs): four light grey feathers on a 2×2 sheet,
        // tinted in each effect, on an alpha-blended material (dark feathers must not glow). Every feather effect
        // takes them from here, as the runes come from DomeLook.
        internal static Material Feather()
        {
            if (!_looked) { _looked = true; _mat = Sheet("RavenWatch.feathers.png", "RavenWatch feathers"); _tilesX = 2; _tilesY = 2; }
            return _mat;
        }

        // our soft smoke puffs (assets/smoke.png, drawn by tools/Smoke.cs): four on a 2×2 sheet, white, tinted per effect
        private static Material _smoke;
        private static bool _smokeLooked;
        internal static Material Smoke()
        {
            if (!_smokeLooked) { _smokeLooked = true; _smoke = Sheet("RavenWatch.smoke.png", "RavenWatch smoke"); }
            return _smoke;
        }

        // a picture built into the mod, on an alpha-blended material
        private static Material Sheet(string resource, string name)
        {
            try
            {
                byte[] data = null;
                using (System.IO.Stream st = typeof(Feathers).Assembly.GetManifestResourceStream(resource))
                    if (st != null) { data = new byte[st.Length]; int read = 0; while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; } }
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                Shader sh = Shader.Find("Sprites/Default");
                if (data == null || sh == null || !AbilityIcons.Decode(tex, data)) { if (Plugin.Instance != null) Plugin.Instance.Log(name + ": the sheet could not be loaded"); return null; }
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Trilinear;
                tex.hideFlags = HideFlags.HideAndDontSave; // kept through the game's asset cleanup on world load, as its material is
                Material m = new Material(sh);
                m.name = name;
                m.mainTexture = tex;
                m.hideFlags = HideFlags.HideAndDontSave;
                return m;
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log(name + " failed: " + e.Message); return null; }
        }

        // a feather system of our own on that material; built switched off, started by the caller
        internal static ParticleSystem Make(Transform parent, string name, Vector3 local)
        {
            Material m = Feather();
            if (m == null || parent == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // they stay with you as you move
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Palette(); // dark feathers, each its own shade, light enough to be seen at night
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f); // feathers turn as they drift
            if (_tilesX * _tilesY > 1)
            {
                ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = _tilesX; sheet.numTilesY = _tilesY;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, _tilesX * _tilesY - 0.01f);
            }
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        // the shadow's feathers: each picks its own shade along this (near black, dark grey, a cold blue-black, a
        // brownish black, a lighter grey), so no two look alike
        private static ParticleSystem.MinMaxGradient Palette()
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] {
                    new GradientColorKey(new Color(0.22f, 0.22f, 0.25f), 0f),
                    new GradientColorKey(new Color(0.42f, 0.43f, 0.48f), 0.25f),
                    new GradientColorKey(new Color(0.24f, 0.28f, 0.42f), 0.5f),
                    new GradientColorKey(new Color(0.38f, 0.3f, 0.26f), 0.75f),
                    new GradientColorKey(new Color(0.62f, 0.63f, 0.68f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            ParticleSystem.MinMaxGradient m = new ParticleSystem.MinMaxGradient(g);
            m.mode = ParticleSystemGradientMode.RandomColor; // a random point of the gradient for each feather
            return m;
        }

        private static Gradient Fade(float inAt, float outFrom)
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // the burst backwards: from a ring of air round you they rush in and vanish on your body
        internal static void In(Player p)
        {
            if (p == null) return;
            VanishSoundBack(p, Mathf.Max(0.2f, InSeconds)); // the sound of Hugin's feather cloud, backwards: it ends as they land on you
            if (Feather() == null) return; // no sheet: no holder left hanging either
            ParticleSystem ps = Make(Holder(p, "RavenWatch_ShadowInRoot"), "RavenWatch_ShadowIn", Vector3.up * 1.1f);
            if (ps == null) return;
            float life = Mathf.Max(0.2f, InSeconds);
            const float from = 3.3f;
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f * InSize, 0.28f * InSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.9f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(-from / life * 1.05f, -from / life * 0.85f); // inwards, ending at the body
            main.maxParticles = InCount;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = from;
            sh.radiusThickness = 0f; // on the shell
            ParticleSystem.EmissionModule em = ps.emission;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)InCount) });
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.25f, 0.7f));
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)));
            ps.Play();
            UnityEngine.Object.Destroy(ps.transform.parent.gameObject, life + 1f);
        }

        // ---------------------------------------------------------------- the feather cloud's sound, backwards
        private static List<AudioClip> _vanishClips;
        private static AudioSource _vanishLike;

        // the clips of Hugin's vanishing (every variant of its sound) and a player to copy the settings from
        private static void FindVanish()
        {
            if (_vanishClips != null) return;
            _vanishClips = new List<AudioClip>();
            Raven hugin = Shoulder.Template(false);
            if (hugin == null || hugin.m_despawnEffect == null || hugin.m_despawnEffect.m_effectPrefabs == null) { _vanishClips = null; return; }
            foreach (EffectList.EffectData d in hugin.m_despawnEffect.m_effectPrefabs)
            {
                if (d == null || d.m_prefab == null) continue;
                AudioSource a = d.m_prefab.GetComponentInChildren<AudioSource>(true);
                if (a == null) continue;
                if (_vanishLike == null) _vanishLike = a;
                ZSFX z = d.m_prefab.GetComponentInChildren<ZSFX>(true);
                if (z != null && z.m_audioClips != null) { foreach (AudioClip c in z.m_audioClips) if (c != null && !_vanishClips.Contains(c)) _vanishClips.Add(c); }
                else if (a.clip != null && !_vanishClips.Contains(a.clip)) _vanishClips.Add(a.clip);
            }
            if (Plugin.Instance != null) Plugin.Instance.Log("Shadow sound: " + _vanishClips.Count + " variant(s) of Hugin's vanishing to play backwards");
        }

        // when your character appears: every variant is caught backwards now, so the first shadow already has it
        internal static void PrepareVanish()
        {
            try { FindVanish(); if (_vanishClips != null) foreach (AudioClip c in _vanishClips) SoundBack.Prepare(c); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shadow sound prepare failed: " + e.Message); }
        }

        // a ready variant backwards, ending as the feathers land; none ready yet: the sound as it is
        private static void VanishSoundBack(Player p, float endIn)
        {
            FindVanish();
            List<AudioClip> ready = new List<AudioClip>();
            if (_vanishClips != null) foreach (AudioClip c in _vanishClips) if (SoundBack.Get(c) != null) ready.Add(c);
            Vector3 at = p.transform.position + Vector3.up * 1.1f;
            if (ready.Count == 0) { PrepareVanish(); VanishSound(at); return; }
            SoundBack.PlayEndingAt(SoundBack.Get(ready[UnityEngine.Random.Range(0, ready.Count)]), _vanishLike, at, p.transform, endIn);
        }

        // only the sound of Hugin's vanishing (the feather cloud): its pieces with a sound player, their own puff
        // switched off, as the feathers here fly in, not out
        internal static void VanishSound(Vector3 at)
        {
            try
            {
                Raven hugin = Shoulder.Template(false);
                if (hugin == null || hugin.m_despawnEffect == null || hugin.m_despawnEffect.m_effectPrefabs == null) return;
                foreach (EffectList.EffectData d in hugin.m_despawnEffect.m_effectPrefabs)
                {
                    if (d == null || d.m_prefab == null || d.m_prefab.GetComponentInChildren<AudioSource>(true) == null) continue;
                    GameObject go = UnityEngine.Object.Instantiate(d.m_prefab, at, Quaternion.identity);
                    float len = 2f;
                    foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.gameObject.SetActive(false); }
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) if (a.clip != null) len = Mathf.Max(len, a.clip.length + 0.5f);
                    if (go.GetComponent<TimedDestruction>() == null) UnityEngine.Object.Destroy(go, len);
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shadow sound failed: " + e.Message); }
        }

        // while it lasts: a few circling round your middle, rising and sinking a little
        internal static ParticleSystem Orbit(Player p)
        {
            ParticleSystem ps = p != null && Feather() != null ? Make(Holder(p, "RavenWatch_ShadowOrbitRoot"), "RavenWatch_ShadowOrbit", Vector3.up * 0.9f) : null;
            if (ps == null) return null;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.maxParticles = 16;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 1.05f;
            sh.radiusThickness = 0.55f; // 0.5 .. 1.05 m off you
            sh.rotation = new Vector3(90f, 0f, 0f); // the circle lies flat round you
            sh.position = new Vector3(0f, 0f, 0f);
            sh.randomPositionAmount = 0.35f;       // up and down a little too
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = OrbitPerSecond;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            // every curve here in the same mode (two constants): each feather picks its own between them
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.22f); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(-0.9f, 2.2f); // round you, each at its own pace, a few the other way
            vel.radial = new ParticleSystem.MinMaxCurve(-0.08f, 0.1f);  // drifting in and out
            Breeze(ps, 0.35f, 0.45f, 0.25f);               // a breeze pushing them about
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.2f, 0.75f));
            ps.Play();
            return ps;
        }

        // ---------------------------------------------------------------- Hugin's leap
        // Hugin's light feathers: greys from silver to slate, each its own
        private static ParticleSystem.MinMaxGradient HuginPalette()
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] {
                    new GradientColorKey(new Color(0.42f, 0.45f, 0.55f), 0f),
                    new GradientColorKey(new Color(0.6f, 0.62f, 0.68f), 0.35f),
                    new GradientColorKey(new Color(0.5f, 0.52f, 0.58f), 0.65f),
                    new GradientColorKey(new Color(0.82f, 0.84f, 0.88f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            ParticleSystem.MinMaxGradient m = new ParticleSystem.MinMaxGradient(g);
            m.mode = ParticleSystemGradientMode.RandomColor;
            return m;
        }

        internal static int RingCount = 120, LandCount = 45;
        internal static float RingRadius = 4f, LandRadius = 2.2f, RingHeight = 0.25f;
        internal static float LeapFeatherSize = 1.5f; // the leap's feathers against the first ones

        // The leap (and its landings): feathers burst out from under your feet, from all over a small patch, some
        // farther, some nearer, a little upwards too — they fill the circle up to the radius instead of drawing its
        // rim — slowing down and fluttering. Left in the world where it happened.
        internal static void Ring(Vector3 feet, int count, float radius)
        {
            GameObject holder = new GameObject("RavenWatch_LeapRingRoot");
            holder.transform.position = feet + Vector3.up * RingHeight;
            ParticleSystem ps = Make(holder.transform, "RavenWatch_LeapRing", Vector3.zero);
            if (ps == null) { UnityEngine.Object.Destroy(holder); return; }
            const float life = 1.6f, drag = 1.5f;
            float reach = (1f - Mathf.Exp(-drag * life)) / drag; // how far a unit of speed carries against the drag
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.85f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.4f / reach, radius / reach); // end anywhere 0.4 .. 1 of the radius off
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * LeapFeatherSize, 0.24f * LeapFeatherSize);
            main.startColor = HuginPalette();
            main.gravityModifier = 0.06f;
            main.maxParticles = count;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.8f;
            sh.radiusThickness = 1f; // from all over the patch under you
            sh.rotation = new Vector3(90f, 0f, 0f); // flat on the ground, out every way
            ParticleSystem.VelocityOverLifetimeModule up = ps.velocityOverLifetime; // and some upwards (every curve two constants)
            up.enabled = true;
            up.space = ParticleSystemSimulationSpace.World;
            up.x = new ParticleSystem.MinMaxCurve(0f, 0f); up.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            up.y = new ParticleSystem.MinMaxCurve(0.1f, 1.6f);
            ParticleSystem.EmissionModule em = ps.emission;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
            Drag(ps, drag);
            Breeze(ps, 0.6f, 0.7f); // they flutter as they slow
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.6f));
            ps.Play();
            UnityEngine.Object.Destroy(holder, life + 1f);
        }

        // A thing put on a raven: a few of its own feathers shaken loose, drifting down off your shoulder
        internal static void Shed(Vector3 at, bool munin, int count)
        {
            GameObject holder = new GameObject("RavenWatch_ShedRoot");
            holder.transform.position = at;
            ParticleSystem ps = Make(holder.transform, "RavenWatch_Shed", Vector3.zero);
            if (ps == null) { UnityEngine.Object.Destroy(holder); return; }
            const float life = 1.8f;
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startColor = munin ? MuninPalette() : HuginPalette();
            main.gravityModifier = 0.05f;
            main.maxParticles = count;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.08f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
            Drag(ps, 2f);
            Breeze(ps, 0.4f, 0.6f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.7f));
            ps.Play();
            UnityEngine.Object.Destroy(holder, life + 1f);
        }

        // A low puff of smoke rolling out from under your feet: a handful of soft clouds, growing and thinning
        internal static void Dust(Vector3 feet, int count, float radius)
        {
            Material m = Smoke();
            if (m == null) return;
            GameObject go = new GameObject("RavenWatch_Dust");
            go.transform.position = feet + Vector3.up * 0.3f;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            const float life = 2f, drag = 2f;
            float reach = (1f - Mathf.Exp(-drag * life)) / drag;
            ParticleSystem.MainModule main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.3f / reach, radius / reach);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.78f, 0.8f, 0.4f), new Color(0.62f, 0.63f, 0.66f, 0.3f));
            main.gravityModifier = -0.01f; // rising a little as it thins
            main.maxParticles = count;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.5f; sh.radiusThickness = 1f;
            sh.rotation = new Vector3(90f, 0f, 0f);
            Drag(ps, drag);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.12f, 0.35f));
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            UnityEngine.Object.Destroy(go, life + 1f);
        }

        // While the leap holds: feathers come off your feet like a raven's trail; in a jump they carry some of your
        // speed (they rise with you a moment), then lose it and hang, leaving a trace in the air.
        internal static ParticleSystem FeetTrail(Player p)
        {
            if (p == null) return null;
            GameObject holder = new GameObject("RavenWatch_LeapTrailRoot");
            holder.transform.position = p.transform.position;
            ParticleSystem ps = Make(holder.transform, "RavenWatch_LeapTrail", Vector3.up * 0.15f);
            if (ps == null) { UnityEngine.Object.Destroy(holder); return null; }
            holder.AddComponent<Follow>().Target = p.transform;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * LeapFeatherSize, 0.15f * LeapFeatherSize);
            main.startColor = HuginPalette();
            main.gravityModifier = 0.05f;
            main.maxParticles = 90;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 2f;         // a few even standing still
            em.rateOverDistance = 3f;     // more the faster you go
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.25f;
            sh.rotation = new Vector3(90f, 0f, 0f); // round your feet
            ParticleSystem.InheritVelocityModule inherit = ps.inheritVelocity; // some of your speed: they go along a moment
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new ParticleSystem.MinMaxCurve(0.35f);
            Drag(ps, 1.2f); // then lose it and hang
            Breeze(ps, 0.25f, 0.8f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.1f, 0.55f));
            ps.Play();
            return ps;
        }

        // ---------------------------------------------------------------- Munin's wings
        // Munin's dark feathers: near black, soot grey, a dark rust, a cold blue-black, each its own
        private static ParticleSystem.MinMaxGradient MuninPalette()
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] {
                    new GradientColorKey(new Color(0.12f, 0.11f, 0.13f), 0f),
                    new GradientColorKey(new Color(0.3f, 0.3f, 0.33f), 0.35f),
                    new GradientColorKey(new Color(0.32f, 0.2f, 0.18f), 0.65f),
                    new GradientColorKey(new Color(0.18f, 0.2f, 0.28f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            ParticleSystem.MinMaxGradient m = new ParticleSystem.MinMaxGradient(g);
            m.mode = ParticleSystemGradientMode.RandomColor;
            return m;
        }

        internal static readonly Color MuninRune = new Color(0.55f, 0.57f, 0.62f, 1f); // the runes grey, as Munin

        // our dome runes in the same kind of system as the feathers (a 4×4 sheet, glowing), or null without the sheet
        internal static ParticleSystem MakeRunes(Transform parent, string name, Vector3 local)
        {
            Material m = DomeLook.RuneMaterialPublic();
            if (m == null || parent == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(MuninRune, Color.Lerp(MuninRune, Color.white, 0.3f));
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4; sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            return ps;
        }

        // drag that eases them to a hang, the same for every stream here
        private static void Drag(ParticleSystem ps, float drag)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(100f);
            lim.drag = new ParticleSystem.MinMaxCurve(drag);
            lim.multiplyDragByParticleSize = false;
            lim.multiplyDragByParticleVelocity = false;
        }

        private static void Breeze(ParticleSystem ps, float strength, float frequency, float scroll = 0f)
        {
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(strength);
            noise.frequency = frequency;
            if (scroll > 0f) noise.scrollSpeed = scroll;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        internal static int GustFeathers = 110; // and a third as many runes

        // The wings' start: a gust blows feathers and runes back off you, never forward — it reads as speed. Three
        // waves a moment apart, each its own size, pace and life.
        internal static void Gust(Player p)
        {
            if (p == null || Feather() == null) return;
            GameObject holder = new GameObject("RavenWatch_WingsGust");
            holder.transform.position = p.transform.position + Vector3.up * 1f;
            holder.transform.rotation = Quaternion.LookRotation(-Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized + Vector3.up * 0.15f, Vector3.up);
            int feathers = GustFeathers, runes = Mathf.Max(1, GustFeathers / 3);
            ParticleSystem f = Make(holder.transform, "RavenWatch_WingsGustFeathers", Vector3.zero);
            if (f != null) GustShape(f, feathers, 0.14f, 0.32f, MuninPalette());
            ParticleSystem r = MakeRunes(holder.transform, "RavenWatch_WingsGustRunes", Vector3.zero);
            if (r != null) GustShape(r, runes, 0.12f, 0.22f, new ParticleSystem.MinMaxGradient(MuninRune, Color.Lerp(MuninRune, Color.white, 0.3f)));
            UnityEngine.Object.Destroy(holder, 3.6f); // the longest live 2.8 s, the last wave starts at 0.2 s
        }

        private static void GustShape(ParticleSystem ps, int count, float sizeMin, float sizeMax, ParticleSystem.MinMaxGradient color)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.duration = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.8f); // seen longer: they get about 4 m back before they go
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.75f, 4.5f);  // half the push of the first try
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.gravityModifier = 0.05f;
            main.maxParticles = count;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone; // backwards, spreading a little
            sh.angle = 28f;
            sh.radius = 0.55f;
            sh.radiusThickness = 1f;
            ParticleSystem.EmissionModule em = ps.emission;
            int a = Mathf.RoundToInt(count * 0.5f), b = Mathf.RoundToInt(count * 0.3f), c = Mathf.Max(0, count - a - b);
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)a), new ParticleSystem.Burst(0.09f, (short)b), new ParticleSystem.Burst(0.2f, (short)c) });
            Drag(ps, 0.6f);
            Breeze(ps, 0.5f, 0.8f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.7f));
            ps.Play();
        }

        internal static float FootFeathersPerMetre = 7.2f, FootRunesPerMetre = 2.4f; // six times a raven's trail, a rune to three feathers

        // One foot's stream while the wings last: Munin's feathers and grey runes left behind as you run, carrying a
        // little of your speed, then easing to a hang and fluttering down
        internal static void FootStream(Transform foot)
        {
            ParticleSystem f = Make(foot, "RavenWatch_WingsFootFeathers", Vector3.zero);
            if (f != null)
            {
                Stream(f, FootFeathersPerMetre, 1f, 240);
                ParticleSystem.MainModule main = f.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
                main.startColor = MuninPalette();
            }
            ParticleSystem r = MakeRunes(foot, "RavenWatch_WingsFootRunes", Vector3.up * 0.1f);
            if (r != null)
            {
                Stream(r, FootRunesPerMetre, 0.3f, 80);
                ParticleSystem.MainModule main = r.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            }
        }

        private static void Stream(ParticleSystem ps, float perMetre, float perSecond, int max)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 1.2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.02f, 0.1f); // some sink fast, some hang
            main.maxParticles = max;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = perSecond;
            em.rateOverDistance = perMetre;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.2f;
            ParticleSystem.InheritVelocityModule inherit = ps.inheritVelocity; // some of your speed, each its own share: they go along a moment
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new ParticleSystem.MinMaxCurve(0.05f, 0.45f);
            ParticleSystem.VelocityOverLifetimeModule drift = ps.velocityOverLifetime; // and a drift of its own, aside, up or down (all two constants)
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f); drift.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            drift.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.55f);
            Drag(ps, 1.3f);
            Breeze(ps, 0.55f, 0.9f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.08f, 0.55f));
            ps.Play();
        }

        // ---------------------------------------------------------------- the strike's trail
        internal static float TrailPerMetre = 1.2f;
        internal static readonly Color HuginTrailA = new Color(0.55f, 0.57f, 0.62f, 1f), HuginTrailB = new Color(0.72f, 0.74f, 0.8f, 1f); // grey
        internal static readonly Color MuninTrailA = new Color(0.08f, 0.07f, 0.09f, 1f), MuninTrailB = new Color(0.2f, 0.18f, 0.21f, 1f);  // dark

        // A raven flying to strike sheds a few feathers behind it: they are left in the air where it passed (by the
        // metre flown, not by time), drift a little, sink and fade. Hugin's grey, Munin's dark.
        // Not a child of the raven: its life (ShoulderLife) keeps every transform under the model as a bone and turns
        // them each frame, so anything of ours put there and later removed breaks its flight. It follows instead.
        internal static ParticleSystem Trail(Transform raven, bool munin)
        {
            if (raven == null) return null;
            GameObject holder = new GameObject("RavenWatch_StrikeTrailRoot");
            holder.transform.position = raven.position;
            ParticleSystem ps = Make(holder.transform, "RavenWatch_StrikeTrail", Vector3.zero);
            if (ps == null) { UnityEngine.Object.Destroy(holder); return null; }
            holder.AddComponent<Follow>().Target = raven;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // left behind where it flew
            main.scalingMode = ParticleSystemScalingMode.Shape;          // their own size, whatever the raven's scale
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            main.startColor = munin ? new ParticleSystem.MinMaxGradient(MuninTrailA, MuninTrailB) : new ParticleSystem.MinMaxGradient(HuginTrailA, HuginTrailB);
            main.gravityModifier = 0.08f; // they sink slowly
            main.maxParticles = 40;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.rateOverDistance = TrailPerMetre;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.06f;
            Drag(ps, 1.5f); // they lose the push at once and hang
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.1f, 0.5f));
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.7f)));
            ps.Play();
            return ps;
        }

        // the strike is over: no new feathers; it lets go of the raven, the ones in the air finish and it goes
        internal static void EndTrail(ParticleSystem ps)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            Transform holder = ps.transform.parent;
            Follow f = holder != null ? holder.GetComponent<Follow>() : null;
            if (f != null) f.Target = null; // stays where the raven landed while the last ones fade
            UnityEngine.Object.Destroy(holder != null ? holder.gameObject : ps.gameObject, 2f);
        }

        // A spot that goes where you go but never turns with you: the shadow's feathers hang on it, so turning round
        // leaves them circling where they were (on your own transform they swung round with you)
        private static Transform Holder(Player p, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.position = p.transform.position;
            go.AddComponent<Follow>().Target = p.transform;
            return go.transform;
        }

        // keeps an effect where a moving thing is, without being part of it
        internal class Follow : MonoBehaviour
        {
            internal Transform Target;
            private void LateUpdate()
            {
                if (Target != null) { transform.position = Target.position; return; }
                // what it followed is gone (a player left the world) without the effect being ended: it goes too
                if (!ReferenceEquals(Target, null)) { Target = null; Destroy(gameObject, 2f); }
            }
        }

        // the shadow is over: no new ones, the circling ones fly off outwards, then it goes
        internal static void Release(ParticleSystem ps)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.radial = new ParticleSystem.MinMaxCurve(3.2f, 4.8f); // two constants, as the rest of the curves
            vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            UnityEngine.Object.Destroy(ps.transform.parent != null ? ps.transform.parent.gameObject : ps.gameObject, 1.5f);
        }
    }
}
#endif

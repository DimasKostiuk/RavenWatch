#if RAVEN_ITEM && RAVEN_STORY
using System;
using UnityEngine;

namespace HuginWhispers
{
    // A person of a scene gone in sparks (the user 2026-11-14, the ambush: «Кетіль декілька разів був за сцену півпрозорим. Треба під час його
    // зникнення додати якийсь ефект іскор темно-жовтих»). The body is never see-through: it stands whole, in its clothes, while dark yellow sparks
    // rise out of it — few at first, thick at its end, a warm light growing with them — and at the end it is gone at once behind their last burst.
    // (NpcFade's way — see-through copies of the body — showed him pale and bare, his body over his shirt: kept for what must come in or go out with no effect.)
    //   npc <who> sparks [seconds=3.5]     the scene's step (StoryScenesCast): the step waits; the person is gone with it
    // Seen on every game near: the step tells the others by the scene's own RPC («n:sparks», StoryScenes.PersonMirror); each draws its own sparks on the
    // body it has. The body itself goes where it is run (Then).
    // Cost: one particle system and a light for the seconds it lasts; nothing otherwise. Numbers below: to tune live (NOT seen in the game yet).
    internal sealed class NpcSparks : MonoBehaviour
    {
        internal static float Rate0 = 8f, Rate1 = 75f;   // sparks a second as it begins · as it ends
        internal static int Last = 110;                  // its last burst, as the body goes
        internal static float Glow = 1.7f, GlowAfter = 1.3f; // the light at its end · s it dies away after
        internal static float SizeMin = 0.03f, SizeMax = 0.08f, Rise = 0.9f;
        internal static readonly Color Bright = new Color(0.88f, 0.63f, 0.12f), Deep = new Color(0.56f, 0.34f, 0.05f); // dark yellow · deep amber

        private ParticleSystem _ps; private Light _light; private float _from, _for; private Action _then;

        // from now on, for so long; then: what the game that runs the scene does as it ends (the body taken away)
        internal static void Begin(GameObject body, float seconds, Action then)
        {
            if (body == null) return;
            if (body.GetComponent<NpcSparks>() != null) return; // (in sparks already)
            NpcSparks s = body.AddComponent<NpcSparks>();
            s._from = Time.time; s._for = Mathf.Max(0.3f, seconds); s._then = then;
            s.Make();
        }

        private void Make()
        {
            Material m = Feathers.Smoke(); // (our soft round puffs: small and bright they read as sparks)
            if (m == null) return;
            GameObject go = new GameObject("RavenWatch_Sparks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.9f;
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // (they stay in the air where they rose: he does not carry them)
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(SizeMin, SizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(Bright, Deep);
            main.gravityModifier = 0f;
            main.maxParticles = 700;
            ParticleSystem.EmissionModule em = _ps.emission;
            em.rateOverTime = Rate0;
            ParticleSystem.ShapeModule shape = _ps.shape; // (a body's box: head to knees)
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.5f, 1.6f, 0.38f);
            ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f); vel.y = new ParticleSystem.MinMaxCurve(Rise * 0.4f, Rise * 1.3f); vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime; // (a spark: bright at once, out at its end)
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.enabled = true; col.color = g;
            ParticleSystem.SizeOverLifetimeModule size = _ps.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            ParticleSystem.TextureSheetAnimationModule sheet = _ps.textureSheetAnimation; // (the puffs' sheet: four on 2 × 2)
            sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m; pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
            _ps.Play();
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point; _light.color = Bright; _light.range = 5f; _light.intensity = 0f; _light.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (_ps == null) { Action none = _then; _then = null; Destroy(this); if (none != null) none(); return; } // (no picture to draw them with: the body goes all the same)
            float k = Mathf.Clamp01((Time.time - _from) / _for);
            ParticleSystem.EmissionModule em = _ps.emission;
            em.rateOverTime = Mathf.Lerp(Rate0, Rate1, k * k);
            if (_light != null) _light.intensity = Glow * k;
            if (k < 1f) return;
            _ps.Emit(Last);
            em.rateOverTime = 0f;
            Transform t = _ps.transform;
            t.SetParent(null, true); // (they stay where he stood, as he goes)
            if (_light != null) t.gameObject.AddComponent<BeastLook.FadeLight>().Begin(_light, Glow, GlowAfter);
            Destroy(t.gameObject, 3.5f);
            Action then = _then; _then = null; _ps = null;
            Destroy(this);
            if (then != null) then();
        }
    }

    internal static partial class StoryScenes
    {
        // a person's act of a scene drawn on the other games too («n:<act>» over the bird's RPC: StoryScenesBird.OnAct)
        private static void PersonMirror(string what, string args)
        {
            string[] a = args.Split(' ');
            if (what != "sparks" || a.Length < 2 || ZNetScene.instance == null) return;
            GameObject body = ZNetScene.instance.FindInstance(NpcKit.Resolve(a[0]));
            if (body != null) NpcSparks.Begin(body, F(a[1], 3.5f), null);
        }
    }
}
#endif

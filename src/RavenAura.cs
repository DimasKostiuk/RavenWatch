#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // Small things that show how far a raven has come with you:
    // a ringing caw when it is back from Odin, a faint haze of motes on a raven of trust 6 and 7 (Hugin's dark,
    // Munin's reddish), and a hungry raven asking to be fed now and then.
    internal static class RavenAura
    {
        internal const int FromLevel = 6;
        private const string RpcBackHugin = "RavenWatch_BackFromOdinH", RpcBackMunin = "RavenWatch_BackFromOdinM";
        private static Material _mat;

        internal static void Register()
        {
            RavenFx.Register(RpcBackHugin, p => BackCaw(p, false));
            RavenFx.Register(RpcBackMunin, p => BackCaw(p, true));
        }

        // ---------------------------------------------------------------- back from Odin

        // here and for everyone nearby
        internal static void BackFromOdin(Player p, bool munin)
        {
            if (p == null) return;
            RavenFx.Broadcast(munin ? RpcBackMunin : RpcBackHugin, p, x => BackCaw(x, munin));
        }

        // a loud, clear caw: higher and ringing for Hugin, lower for Munin, carried far
        private static void BackCaw(Player p, bool munin)
        {
            try { ScreamFx.Caw(p, munin, munin ? 0.95f : 1.45f, 1f); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven return caw failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- the motes

        // A few motes drifting up off the raven's body and fading: just enough to be seen up close.
        internal static void Attach(GameObject raven, bool munin, int level)
        {
            if (raven == null || level < FromLevel) return;
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == "RavenWatch_Aura") return; // it has one already (the same bird back from a perch)
            try
            {
                Transform body = null;
                foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == "Spine1") { body = t; break; }
                if (body == null) body = raven.transform;
                GameObject g = new GameObject("RavenWatch_Aura");
                g.transform.SetParent(body, false);
                ParticleSystem ps = g.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                bool top = level >= 7;

                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.scalingMode = ParticleSystemScalingMode.Shape; // sizes in meters, whatever the bone's scale
                main.simulationSpace = ParticleSystemSimulationSpace.World; // a faint trail when you move
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, top ? 0.03f : 0.024f);
                main.gravityModifier = -0.015f; // drifting up
                main.maxParticles = 30;
                main.startColor = munin
                    ? new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.07f, 0.05f, 0.75f), new Color(0.8f, 0.18f, 0.08f, 0.6f))
                    : new ParticleSystem.MinMaxGradient(new Color(0.05f, 0.06f, 0.1f, 0.75f), new Color(0.16f, 0.2f, 0.32f, 0.6f));

                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = top ? 3f : 1.5f;

                ParticleSystem.ShapeModule sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                float scale = Mathf.Max(0.0001f, body.lossyScale.x);
                sh.radius = 0.07f / scale; // the shape follows the bone's scale: about the bird's body, in meters

                Gradient fade = new Gradient();
                fade.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(fade);
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.03f;
                noise.frequency = 0.8f;

                ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
                Material m = Mat();
                if (m != null) r.sharedMaterial = m;
                ps.Play();
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven aura failed: " + e.Message); }
        }

        // a soft round mote on a plain see-through material (dark motes need it: a glow-only one would not show them)
        internal static Material Dot() { return Mat(); } // the soft mote, for other effects too

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
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
            _mat = new Material(sh);
            _mat.mainTexture = t;
            _mat.hideFlags = HideFlags.HideAndDontSave;
            return _mat;
        }

        // ---------------------------------------------------------------- hungry

        // A raven at satiety 40 or less asks for food now and then, from the shoulder (not when asleep or away).
        // Now and then, instead, it lets slip a hint about its favourite food (Satiety.Hint).
        internal static float FirstAskMin = 120f, FirstAskMax = 300f, AskMin = 360f, AskMax = 540f; // real seconds: the first ask 2–5 min after it gets hungry, then every 6–9
        internal static float HintChance = 0.35f;
        private static readonly float[] NextAsk = { -1f, -1f };
        private static readonly int[] LastLine = { -1, -1 };

        internal static void Reset() { NextAsk[0] = NextAsk[1] = -1f; LastLine[0] = LastLine[1] = -1; }

        private static string[] HuginAsks { get { return Lang.Pool(HuginAsksSrc); } } // (translated on every read)
        private static readonly string[] HuginAsksSrc = Lang.Keys("Я тут подумав... а подумав я про їжу. Нагодуй мене.", "На голодний шлунок і думки не думаються. Де мій обід?", "Ти сьогодні вже ї{в|ла}. А я — ні. Помічаєш несправедливість?", "Я не скаржуся. Я просто дуже голосно голодний.", "Нагодуй мене — і я розповім тобі щось розумне. Можливо.");

        private static string[] MuninAsks { get { return Lang.Pool(MuninAsksSrc); } } // (translated on every read)
        private static readonly string[] MuninAsksSrc = Lang.Keys("ЖРАТИ ДАЙ, СУКА.", "Ще годину без їжі — і я почну з твого вуха.", "Ти що, забу{в|ла}, що я теж хочу жерти?", "Нагодуй мене, або я сам візьму. З твоєї пики.", "Порожній шлунок — злий Мунін. А злий Мунін — це твої проблеми.");

        // from Trust.Tick, twice a second, for each worn raven
        internal static void Hungry(Player p, bool munin, ItemDrop.ItemData item)
        {
            int i = munin ? 1 : 0;
            bool asks = p != null && item != null && Trust.Active(item) && !Trust.Hostile(item) && !Trust.FedToday(item) && !Trust.Away(item)
                && Shoulder.LocalModel(munin) != null;
            if (!asks) { NextAsk[i] = -1f; return; }
            if (NextAsk[i] < 0f) { NextAsk[i] = Time.time + UnityEngine.Random.Range(FirstAskMin, FirstAskMax); return; }
            if (Time.time < NextAsk[i]) return;
            if (!Director.May(Director.Voice.Hunger)) { NextAsk[i] = Time.time + UnityEngine.Random.Range(30f, 60f); return; } // the director: later
            NextAsk[i] = Time.time + UnityEngine.Random.Range(AskMin, AskMax);
            Director.Spoke(Director.Voice.Hunger, 8f);
            string hint = UnityEngine.Random.value < HintChance ? Satiety.Hint(p, item, munin) : null; // its favourite, a bit at a time
            if (!string.IsNullOrEmpty(hint)) { RavenSkills.SayFromShoulder(p, munin, hint); return; }
            string made = Satiety.Zone(item) <= Satiety.Hungry ? Satiety.Say(p, munin, "hungry_bad") : null; // really hungry: its own lines
            if (string.IsNullOrEmpty(made) && Plugin.Instance != null) made = Plugin.Instance.ComposeTagged(p, munin, "hungry_ask"); // phrase file, by trust tone
            if (!string.IsNullOrEmpty(made)) { RavenSkills.SayFromShoulder(p, munin, made); return; }
            string[] lines = munin ? MuninAsks : HuginAsks;
            int k = UnityEngine.Random.Range(0, lines.Length);
            if (k == LastLine[i] && lines.Length > 1) k = (k + 1) % lines.Length; // not the same twice in a row
            LastLine[i] = k;
            RavenSkills.SayFromShoulder(p, munin, lines[k]);
        }
    }
}
#endif

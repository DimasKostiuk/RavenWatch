#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuginWhispers
{
    // A person seen through (Fades: smooth by default):
    //   an echo      its template's «echo: 0.4» (how much of it shows) — always half there (the prologue's «Відлуння»)
    //   fading away  anyone: «npc <who> fade [s] [sound]» in a scene, or a person made for a scene when it ends
    //                (NpcFade.Out) — see-through, then gone; its owner takes it away at the end
    // Every game paints its own picture: the body's and its clothes' renderers get see-through copies of their materials
    // (Sprites/Default — the game has no legacy particle shaders; unlit: an echo pale blue, anyone else in the light of
    // the hour; its meshes with white vertex colours, which that shader multiplies by), no shadows; clothes the game puts on later are caught on the next look. The fading is written once by
    // the owner (KFade: when it began, how long, its sound) — every game fades it alike and plays its sound itself.
    [DefaultExecutionOrder(31000)] // (after the game's own dressing, which puts its hair material back each update)
    internal sealed class NpcFade : MonoBehaviour
    {
        internal static readonly int KFade = "RavenWatch_echoFade".GetStableHashCode(); // string "<world seconds begun> <seconds> [sound]"
        private static readonly Color Pale = new Color(0.7f, 0.8f, 0.95f, 1f); // (an echo: its own colours, cooled a little)
        private static Shader _shader;
        private NpcActor _a;
        private readonly Dictionary<Material, Material> _made = new Dictionary<Material, Material>(); // (the game's → ours)
        private readonly Dictionary<Material, Color> _mine = new Dictionary<Material, Color>(); // (ours → the colour of what it copies: skin, hair, cloth)
        private float _nextLook, _alpha = -1f;
        private string _heard = "";
        private bool _gone;

        // every game, each of its turns (NpcModules): an echo, or one fading, gets its picture once
        internal static void Keep(NpcActor a)
        {
            if (a == null || a.Def == null || Given.Contains(a)) return; // (one given its picture already: a look into a set, not a component search each frame)
            if (a.Def.Echo <= 0f && (Time.frameCount % 15 != 0 || Fading(a.Zdo).Length == 0)) return; // (anyone else: its fading looked for now and then)
            a.gameObject.AddComponent<NpcFade>()._a = a;
            Given.Add(a);
        }
        private static readonly HashSet<NpcActor> Given = new HashSet<NpcActor>();

        private static string Fading(ZDO z) { return z != null ? z.GetString(KFade, "") : ""; }

        // a person goes away slowly (where it is run: a scene's owner) — seconds, and a story sound or «-»
        internal static void Out(NpcActor a, float seconds, string sound)
        {
            if (a != null) a.Act("fade " + Mathf.Max(0.1f, seconds).ToString("0.##", CultureInfo.InvariantCulture) + " " + (string.IsNullOrEmpty(sound) ? "-" : sound));
        }

        // the act «fade <s> [sound]» where it is run: from now on it fades, on every game
        internal static void Act(NpcActor a, string[] w)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || ZNet.instance == null || (Fading(z).Length > 0 && !Coming(Fading(z)))) return; // (fading already; coming in: it may go)
            float s = w.Length > 1 ? Errands.F(w[1], Fades.Person) : Fades.Person;
            z.Set(KFade, ZNet.instance.GetTimeSeconds().ToString("0.##", CultureInfo.InvariantCulture) + " " + Mathf.Max(0.1f, s).ToString("0.##", CultureInfo.InvariantCulture) + " " + (w.Length > 2 ? w[2] : "-"));
        }

        // a person made for a scene comes in slowly (StoryScenes' spawn): «<world seconds> <seconds> - in»
        internal static void In(NpcActor a, float seconds)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || ZNet.instance == null) return;
            z.Set(KFade, ZNet.instance.GetTimeSeconds().ToString("0.##", CultureInfo.InvariantCulture) + " " + Mathf.Max(0.1f, seconds).ToString("0.##", CultureInfo.InvariantCulture) + " - in");
        }
        private static bool Coming(string v) { return v.EndsWith(" in"); }

        // how much of it is left (1 whole … 0 gone); its sound once, as it begins (a late look stays quiet)
        private float Left()
        {
            string v = Fading(_a.Zdo);
            if (v.Length == 0 || ZNet.instance == null) return 1f;
            string[] w = v.Split(' ');
            double from; float dur;
            if (w.Length < 2 || !double.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out from) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dur)) return 1f;
            double since = ZNet.instance.GetTimeSeconds() - from;
            if (_heard != v) { _heard = v; if (w.Length > 2 && w[2] != "-" && since < 1.5) Audio.Sound(w[2], _a.transform.position + Vector3.up * 1.4f, new SoundAsk()); }
            if (Coming(v)) return Mathf.Clamp01((float)since / dur); // (coming in: from nothing to whole)
            return 1f - Mathf.Clamp01((float)since / dur);
        }

        // an echo's own light grey smoke: slow wisps round it, rising a little, as much as it is there (each game its own)
        internal static float SmokeRate = 7f;
        private ParticleSystem _smoke;
        private static ParticleSystem Smoke(Transform on)
        {
            GameObject o = new GameObject("RavenWatch_echoSmoke");
            o.transform.SetParent(on, false);
            o.transform.localPosition = Vector3.up * 0.9f;
            ParticleSystem ps = o.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.66f, 0.72f, 0.16f), new Color(0.75f, 0.78f, 0.82f, 0.22f));
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(0.6f, 1.6f, 0.4f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
            ParticleSystemRenderer pr = o.GetComponent<ParticleSystemRenderer>();
            pr.material = FxLib.Glow(true);
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // what each renderer wears of ours: put back every frame if the game swapped it (a look of a reference each, nothing made)
        private readonly Dictionary<Renderer, Material[]> _ours = new Dictionary<Renderer, Material[]>();
        private void Keep()
        {
            foreach (KeyValuePair<Renderer, Material[]> o in _ours)
            {
                Renderer r = o.Key;
                if (r == null || o.Value.Length == 0) continue;
                if (r.sharedMaterial != o.Value[0]) r.sharedMaterials = o.Value;
                if (_a.Def.Echo > 0f && r.HasPropertyBlock()) r.SetPropertyBlock(null); // (the game's tint block, set again with its hair)
            }
        }

        private void LateUpdate()
        {
            if (_a == null) { Destroy(this); return; }
            Keep();
            if (_a.Def.Echo > 0f && _smoke == null) _smoke = Smoke(transform);
            if (_smoke != null) { ParticleSystem.EmissionModule em = _smoke.emission; em.rateOverTime = SmokeRate * Mathf.Clamp01(_alpha < 0f ? 0f : _alpha / Mathf.Max(0.01f, _a.Def.Echo)); }
            if (Time.time >= _nextLook) { _nextLook = Time.time + 0.5f; Look(); }
            float left = Left(), alpha = (_a.Def.Echo > 0f ? _a.Def.Echo : 1f) * left;
            if (Mathf.Abs(alpha - _alpha) > 0.002f)
            {
                _alpha = alpha;
                Color c = Tint(alpha);
                foreach (KeyValuePair<Material, Color> m in _mine) if (m.Key != null) m.Key.SetColor("_Color", new Color(c.r * m.Value.r, c.g * m.Value.g, c.b * m.Value.b, c.a));
            }
            if (left <= 0f && !_gone && _a.Owned && !Coming(Fading(_a.Zdo))) { _gone = true; _a.Remove(); }
        }

        // an echo pale blue; anyone else as bright as the hour (the copies are unlit)
        private Color Tint(float alpha)
        {
            if (_a.Def.Echo > 0f) return new Color(Pale.r, Pale.g, Pale.b, alpha);
            Color amb = RenderSettings.ambientLight;
            float l = Mathf.Clamp(Mathf.Max(amb.r, Mathf.Max(amb.g, amb.b)) * 1.6f, 0.35f, 1f);
            return new Color(l, l, l, alpha);
        }

        // the renderers as they are now: any of the game's materials swapped for our see-through copy
        private void Look()
        {
            if (_shader == null) { _shader = Shader.Find("Sprites/Default"); Npcs.Log("fade shader: " + (_shader != null ? _shader.name : "none")); }
            if (_shader == null) return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                Vector4 block = Vector4.zero; // (the game may tint by the renderer's own block instead of the material)
                if (r.HasPropertyBlock()) { if (_block == null) _block = new MaterialPropertyBlock(); r.GetPropertyBlock(_block); block = _block.GetVector("_SkinColor"); }
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || _mine.ContainsKey(m)) continue;
                    Material e;
                    if (!_made.TryGetValue(m, out e))
                    {
                        e = new Material(_shader) { name = m.name + " (fade)", renderQueue = 3000 };
                        bool echo = _a.Def.Echo > 0f;
                        if (m.HasProperty("_MainTex") && !echo) e.mainTexture = m.mainTexture;
                        Color own = echo ? EchoTone(m) : Own(m, block);
                        Color t0 = Tint(_alpha < 0f ? 1f : _alpha);
                        e.SetColor("_Color", new Color(t0.r * own.r, t0.g * own.g, t0.b * own.b, t0.a));
                        _made[m] = e; _mine[e] = own;
                        _alpha = -1f; // (the new one painted on the next frame)
                    }
                    mats[i] = e; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
                if (!_ours.ContainsKey(r)) _ours[r] = (Material[])mats.Clone();
                if (changed && _a.Def.Echo > 0f && r.HasPropertyBlock()) { if (Blueprint.On) Npcs.Log("echo look: " + r.name + " had its own block (its _Color " + (_block != null ? _block.GetColor("_Color").ToString() : "?") + "): taken off, our colour shows"); r.SetPropertyBlock(null); } // (the game tints by a block over the material: its colour, not ours, was drawn)
                if (r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
                WhiteVertices(r);
                if (Blueprint.On && changed) Tell(r, mats);
            }
        }

        // a texture's mean colour (read through a small render target: the game's are not readable), for the log
        private static string Average(Texture tex)
        {
            RenderTexture rt = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32), was = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            Texture2D px = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            px.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); px.Apply();
            RenderTexture.active = was; RenderTexture.ReleaseTemporary(rt);
            Color sum = Color.clear; Color[] all = px.GetPixels();
            foreach (Color c in all) sum += c;
            Object.Destroy(px);
            sum /= all.Length;
            return sum.r.ToString("0.00") + "/" + sum.g.ToString("0.00") + "/" + sum.b.ToString("0.00") + "/" + sum.a.ToString("0.00");
        }

        // the author's log, once per renderer: what was copied and what it shows (the echo came out black: the numbers first)
        private readonly HashSet<Renderer> _told = new HashSet<Renderer>();
        private void Tell(Renderer r, Material[] mats)
        {
            if (!_told.Add(r)) return;
            SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
            Mesh mesh = sk != null ? sk.sharedMesh : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
            string s = "echo look: " + r.name + " mesh " + (mesh != null ? mesh.name + (mesh.isReadable ? " readable" : " NOT readable") + (_white.Contains(mesh) ? " (white copy)" : "") : "none");
            foreach (Material e in mats)
            {
                if (e == null) continue;
                Color c = e.HasProperty("_Color") ? e.GetColor("_Color") : Color.clear;
                s += " | " + e.name + " tex " + (e.mainTexture != null ? e.mainTexture.name + " avg " + Average(e.mainTexture) : "none") + " colour " + c.r.ToString("0.00") + "/" + c.g.ToString("0.00") + "/" + c.b.ToString("0.00") + "/" + c.a.ToString("0.00");
            }
            Npcs.Log(s);
            if (_told.Count == 1)
            {
                string have = "";
                foreach (string n in new[] { "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "Unlit/Transparent", "Sprites/Default", "UI/Default", "Custom/Creature", "Custom/Player" }) { Shader sh = Shader.Find(n); have += n + (sh != null ? (sh.isSupported ? " yes" : " unsupported") : " no") + "; "; }
                Npcs.Log("echo look: shaders here: " + have);
            }
        }

        // Sprites/Default multiplies by the mesh's vertex colours, and the game's bodies and clothes keep their own in them
        // (masks, dark for the most part): the echo was a black figure (2026-11-02). Its meshes get copies with white vertex
        // colours — only for the one seen through; a mesh the game keeps unreadable stays as it is
        private readonly HashSet<Mesh> _white = new HashSet<Mesh>();
        private void WhiteVertices(Renderer r)
        {
            SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
            MeshFilter mf = sk == null ? r.GetComponent<MeshFilter>() : null;
            Mesh m = sk != null ? sk.sharedMesh : mf != null ? mf.sharedMesh : null;
            if (m == null || _white.Contains(m) || !m.isReadable) return;
            try
            {
                Mesh copy = Instantiate(m);
                copy.name = m.name + " (echo)";
                Color32[] c = new Color32[copy.vertexCount];
                for (int i = 0; i < c.Length; i++) c[i] = new Color32(255, 255, 255, 255);
                copy.colors32 = c;
                _white.Add(copy);
                if (sk != null) sk.sharedMesh = copy; else mf.sharedMesh = copy;
            }
            catch (System.Exception e) { Npcs.Log("echo mesh " + m.name + ": " + e.Message); }
        }

        // an echo is one even colour, its hair and beard a shade darker than its body (the game's pictures came out black or white
        // on the see-through shader: measured 2026-11-04)
        internal static float EchoHair = 0.32f, EchoBody = 0.6f;
        private static Color EchoTone(Material m)
        {
            string n = m.name.ToLowerInvariant();
            float k = n.Contains("hair") || n.Contains("beard") ? EchoHair : EchoBody;
            return new Color(k, k, k, 1f);
        }

        // the colour the game lays over a material's picture: skin and hair are tinted by «_SkinColor» (a grey picture —
        // without it hair goes white), cloth by its «_Color»
        private static MaterialPropertyBlock _block;
        private static Color Own(Material m, Vector4 block)
        {
            Color c = m.HasProperty("_Color") ? m.color : Color.white;
            Vector4 s = block.x + block.y + block.z > 0.01f ? block : m.HasProperty("_SkinColor") ? m.GetVector("_SkinColor") : Vector4.zero;
            if (s.x + s.y + s.z > 0.01f) c = new Color(c.r * s.x, c.g * s.y, c.b * s.z, 1f);
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
        }

        private void OnDestroy()
        {
            foreach (Material m in _mine.Keys) if (m != null) Destroy(m);
            foreach (Mesh m in _white) if (m != null) Destroy(m);
            _mine.Clear(); _made.Clear(); _white.Clear();
            if (_a != null) Given.Remove(_a);
        }
    }
}
#endif

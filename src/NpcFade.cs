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
            if (z == null || ZNet.instance == null || Fading(z).Length > 0) return; // (fading already)
            float s = w.Length > 1 ? Errands.F(w[1], Fades.Person) : Fades.Person;
            z.Set(KFade, ZNet.instance.GetTimeSeconds().ToString("0.##", CultureInfo.InvariantCulture) + " " + Mathf.Max(0.1f, s).ToString("0.##", CultureInfo.InvariantCulture) + " " + (w.Length > 2 ? w[2] : "-"));
        }

        // how much of it is left (1 whole … 0 gone); its sound once, as it begins (a late look stays quiet)
        private float Left()
        {
            string v = Fading(_a.Zdo);
            if (v.Length == 0 || ZNet.instance == null) return 1f;
            string[] w = v.Split(' ');
            double from; float dur;
            if (w.Length < 2 || !double.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out from) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dur)) return 1f;
            double since = ZNet.instance.GetTimeSeconds() - from;
            if (_heard != v) { _heard = v; if (w.Length > 2 && since < 1.5) StorySounds.Play(w[2], _a.transform.position + Vector3.up * 1.4f); }
            return 1f - Mathf.Clamp01((float)since / dur);
        }

        private void LateUpdate()
        {
            if (_a == null) { Destroy(this); return; }
            if (Time.time >= _nextLook) { _nextLook = Time.time + 0.5f; Look(); }
            float left = Left(), alpha = (_a.Def.Echo > 0f ? _a.Def.Echo : 1f) * left;
            if (Mathf.Abs(alpha - _alpha) > 0.002f)
            {
                _alpha = alpha;
                Color c = Tint(alpha);
                foreach (KeyValuePair<Material, Color> m in _mine) if (m.Key != null) m.Key.SetColor("_Color", new Color(c.r * m.Value.r, c.g * m.Value.g, c.b * m.Value.b, c.a));
            }
            if (left <= 0f && !_gone && _a.Owned) { _gone = true; _a.Remove(); }
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
                        if (m.HasProperty("_MainTex")) e.mainTexture = m.mainTexture;
                        Color own = Own(m, block);
                        Color t0 = Tint(_alpha < 0f ? 1f : _alpha);
                        e.SetColor("_Color", new Color(t0.r * own.r, t0.g * own.g, t0.b * own.b, t0.a));
                        _made[m] = e; _mine[e] = own;
                        _alpha = -1f; // (the new one painted on the next frame)
                    }
                    mats[i] = e; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
                if (r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
                WhiteVertices(r);
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

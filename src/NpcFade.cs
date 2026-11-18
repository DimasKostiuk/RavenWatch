#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuginWhispers
{
    // A person — or any creature of the game — seen through (Fades: smooth by default):
    //   an echo      its template's «echo: 0.4» (how much of it shows) — always half there (the prologue's «Відлуння»)
    //   fading away  anyone: «npc <who> fade [s] [sound]» in a scene, or a person made for a scene when it ends
    //                (NpcFade.Out) — see-through, then gone; its owner takes it away at the end
    //   coming in    a person made for a scene (NpcFade.In): from nothing to whole, then the game's own picture back (Restore) —
    //                a see-through copy is never a picture to stay in
    //   a ghost      a creature of the game marked in its ZDO (KGhost: how much of it shows — the story's ghosts, the user 2026-11-15:
    //                «привидів … ті що в склепі літають з ектоплазмою» — the game's Ghost, made see-through like the echo): every game paints
    //                it as it comes into that game (Creature, from the game's Character.Awake: ChampionAwakePatch), the echo's way, smoke too;
    //                the author's test: raven npc ghost [prefab] [how much]
    // Every game paints its own picture: the body's and its clothes' renderers get see-through copies of their materials, no shadows;
    // clothes the game puts on later are caught on the next look. How the copy is made (Mode, live: raven npc tune NpcFade.Mode 0|1|2;
    // the user 2026-11-15: «поправити вигляд відлуння … далі ми можемо використовувати його в інших ситуаціях»):
    //   1, 2  the game's own shader kept (SeeThrough: Custom/Player, Custom/Creature — lit, its picture, its bumps; read from the game's bundle
    //         2026-11-15: both carry the standard _SrcBlend/_DstBlend/_ZWrite/_Mode, and the game's own Ghost is Custom/Creature so set:
    //         One/OneMinusSrcAlpha, no depth, both sides) — 1 blended by its alpha, 2 the ghost's way (its light added, what is behind dimmed
    //         by alpha), 3 on the game's own Ghost's material; its depth (Depth): 0 none, 1 written (the body drawn after its clothes and hair), 2 the
    //         whole body's depth first, nothing drawn (Prepass) — only its nearest skin shows: one shell, no hair seen over its head, no beard
    //         twice (the user 2026-11-15: «борода його з волоссям явно виділяються»)
    //   0     the old flat copy (Sprites/Default, unlit: an echo pale blue, anyone else in the light of the hour; meshes with white vertex
    //         colours, which that shader multiplies by) — and any material whose shader has no blend of its own (CanBlend)
    // The fading is written once by the owner (KFade: when it began, how long, its sound) — every game fades it alike and plays its sound itself.
    [DefaultExecutionOrder(31000)] // (after the game's own dressing, which puts its hair material back each update)
    internal sealed class NpcFade : MonoBehaviour
    {
        internal static readonly int KFade = "RavenWatch_echoFade".GetStableHashCode(); // string "<world seconds begun> <seconds> [sound]"
        private static readonly Color Pale = new Color(0.7f, 0.8f, 0.95f, 1f); // (an echo: its own colours, cooled a little)
        internal static int Mode = 1, Depth = 2; // (live: raven npc tune NpcFade.Mode 0|1|2|3, NpcFade.Depth 0|1|2 — every one seen through is painted anew)
        private static Shader _shader;
        private NpcActor _a;
        private ZNetView _nv; private float _ghostShows; // (a creature of the game: its view, how much of it shows — KGhost)
        internal static readonly int KGhost = "RavenWatch_ghost".GetStableHashCode(); // float on a creature of the game: how much of it shows (0 < it ≤ 1)
        private float Echo { get { return _a != null ? _a.Def.Echo : _ghostShows; } } // (how much of it shows always: an echo, a ghost; 0 — whole unless fading)
        private ZDO Zdo { get { return _a != null ? _a.Zdo : _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; } }
        private int _mode = -1, _depth = -1; private float _glow = -1f, _gloss = -1f, _shine = -1f; // (what its copies were made with: a change makes them anew)
        private readonly Dictionary<Material, Material> _made = new Dictionary<Material, Material>(); // (the game's → ours)
        private readonly Dictionary<Material, Color> _mine = new Dictionary<Material, Color>(); // (ours → the colour of what it copies: skin, hair, cloth)
        private readonly HashSet<Material> _lit = new HashSet<Material>(); // (ours that keep the game's shader: SeeThrough)
        private float _nextLook, _alpha = -1f;
        private string _heard = "";
        private bool _gone;

        // every game, each of its turns (NpcModules): an echo, or one fading, gets its picture once
        internal static void Keep(NpcActor a)
        {
            if (a == null || a.Def == null || Given.Contains(a)) return; // (one given its picture already: a look into a set, not a component search each frame)
            if (a.Def.Echo <= 0f && (Time.frameCount % 15 != 0 || Fading(a.Zdo).Length == 0 || CameIn(Fading(a.Zdo)))) return; // (anyone else: its fading looked for now and then; come in whole — its own picture)
            a.gameObject.AddComponent<NpcFade>()._a = a;
            Given.Add(a);
        }
        private static readonly HashSet<NpcActor> Given = new HashSet<NpcActor>();

        // a creature of the game as it comes into this game (ChampionAwakePatch: the game's Character.Awake, every game): marked a ghost — painted so
        internal static void Creature(Character c)
        {
            if (c == null || c.GetComponent<NpcActor>() != null) return; // (a person of ours: Keep, by its template)
            ZNetView nv = c.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : ZNetView.m_initZDO; // (as the game makes it from its ZDO, the view may not have it yet)
            float shows = z != null ? z.GetFloat(KGhost, 0f) : 0f;
            if (shows <= 0f || c.GetComponent<NpcFade>() != null) return;
            NpcFade f = c.gameObject.AddComponent<NpcFade>();
            f._nv = nv; f._ghostShows = Mathf.Clamp01(shows);
        }

        // the author's test: a creature of the game made a ghost before you (4 m, facing you) — raven npc ghost [prefab=Ghost] [how much=0.6].
        // Its mark is in its ZDO: every game paints it; this one at once (its Awake came before the mark)
        internal static string Spawn(Player p, string[] a)
        {
            string name = a.Length > 2 ? a[2] : "Ghost";
            float shows = a.Length > 3 ? Mathf.Clamp(Errands.F(a[3], 0.6f), 0.05f, 1f) : 0.6f;
            GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (pf == null || pf.GetComponent<Character>() == null) return "Нема істоти «" + name + "» у грі.";
            Vector3 at = p.transform.position + p.transform.forward * 4f + Vector3.up * 0.5f;
            GameObject g = Object.Instantiate(pf, at, Quaternion.LookRotation(-p.transform.forward));
            ZNetView nv = g.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return "Не вийшло: у «" + name + "» нема свого місця в мережі.";
            nv.GetZDO().Set(KGhost, shows);
            Creature(g.GetComponent<Character>());
            return "«" + name + "» — привид (видно " + Mathf.RoundToInt(shows * 100f) + "%), за 4 м перед тобою.";
        }

        private static string Fading(ZDO z) { return z != null ? z.GetString(KFade, "") : ""; }

        // ---------------------------------------------------------------- a see-through copy that is lit (anyone's to use)
        // Seen in game 2026-11-15: Custom/Player at run time has no blend of its own (its material keeps the saved _SrcBlend… of an older
        // shader; the shader reads none — the echo stayed the flat copy). So the lit copy is made on:
        //   1, 2  a shader that blends by its own words keeps itself (CanBlend); any other, the standard one (Standard: the mod's own models
        //         wear it here) with the picture, bumps and colour of what it copies — 1 its fade way, 2 the ghost's (One/OneMinusSrcAlpha)
        //   3     the game's own Ghost's material (see-through in this game), with the picture of what it copies (GhostGlow: how much it shines)
        internal static float GhostGlow = 0.25f, Gloss = 0.1f; // (live: raven npc tune NpcFade.GhostGlow|Gloss — made anew with Mode)
        internal static float Shine = 1f; // (a creature's own light kept on its copy, × this — live: raven npc tune NpcFade.Shine)
        private static Shader _standard;
        private static Material _ghost;
        private static bool _ghostLooked;

        // the depth of a whole body first (Depth 2): written, nothing drawn (the standard shader blended Zero/One), a queue before the colour —
        // so only its nearest skin shows. One for the game, on renderers of one mesh part (a second material on such a mesh draws it again)
        private static Material _prepass;
        private static Material Prepass()
        {
            if (_prepass != null) return _prepass;
            if (_standard == null) _standard = FbxLook.Standard; // (Shader.Find does not see it here)
            if (_standard == null) return null;
            _prepass = new Material(_standard) { name = "RavenWatch see-through depth", renderQueue = (int)RenderQueue.Transparent - 1, hideFlags = HideFlags.HideAndDontSave };
            _prepass.SetFloat("_SrcBlend", (float)BlendMode.Zero);
            _prepass.SetFloat("_DstBlend", (float)BlendMode.One);
            _prepass.SetFloat("_ZWrite", 1f);
            return _prepass;
        }
        private static bool IsPrepass(Material m) { return m != null && _prepass != null && (m == _prepass || m.name.StartsWith(_prepass.name, System.StringComparison.Ordinal)); }
        // a renderer of cut-out cards (hair, a beard): by its own materials' names or their alpha test
        private static bool Cards(Material[] mats)
        {
            if (mats == null) return false;
            foreach (Material m in mats)
            {
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                if (n.Contains("hair") || n.Contains("beard") || m.IsKeywordEnabled("_ALPHATEST_ON")) return true;
            }
            return false;
        }
        private static int Parts(Renderer r)
        {
            SkinnedMeshRenderer sk = r as SkinnedMeshRenderer;
            MeshFilter mf = sk == null ? r.GetComponent<MeshFilter>() : null;
            Mesh m = sk != null ? sk.sharedMesh : mf != null ? mf.sharedMesh : null;
            return m != null ? m.subMeshCount : 0;
        }

        // whether a material's shader blends by its own words (_SrcBlend, _DstBlend, _ZWrite declared by the shader itself)
        internal static bool CanBlend(Material m) { return m != null && m.HasProperty("_SrcBlend") && m.HasProperty("_DstBlend") && m.HasProperty("_ZWrite"); }

        // the game's Ghost's body material, looked for once (heavy: one prefab's renderers, once a game)
        private static Material GhostMaterial()
        {
            if (_ghost != null || _ghostLooked || ZNetScene.instance == null) return _ghost;
            _ghostLooked = true;
            GameObject g = ZNetScene.instance.GetPrefab("Ghost");
            Renderer r = g != null ? g.GetComponentInChildren<SkinnedMeshRenderer>(true) : null; // heavy: once a game
            _ghost = r != null ? r.sharedMaterial : null;
            Npcs.Log("see-through: the game's ghost material " + (_ghost != null ? "«" + _ghost.name + "» [" + (_ghost.shader != null ? _ghost.shader.name : "?") + "] queue " + _ghost.renderQueue : "not found"));
            TellShader(_ghost);
            return _ghost;
        }

        // the author's log, once a shader: what it really reads (its properties) and the keywords of a material on it
        private static readonly HashSet<string> _shadersTold = new HashSet<string>();
        internal static void TellShader(Material m)
        {
            if (!Blueprint.On || m == null || m.shader == null || !_shadersTold.Add(m.shader.name)) return;
            Shader s = m.shader;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("see-through: shader «" + s.name + "» queue " + s.renderQueue + ", passes " + s.passCount + ", reads:");
            for (int i = 0; i < s.GetPropertyCount(); i++) sb.Append(' ').Append(s.GetPropertyName(i));
            sb.Append(" | keywords of «").Append(m.name).Append("»: ").Append(string.Join(" ", m.shaderKeywords));
            Npcs.Log(sb.ToString());
        }

        // the copy (null: none can be made — the caller keeps the flat one): mode 1 blended by its alpha, 2 the ghost's way (its light added,
        // what is behind dimmed), 3 on the game's ghost; depth written or not; last: drawn after the rest of its body (a body under its clothes
        // and hair: only its shell shows). Its _Color (alpha too) is the caller's: Paint. Keeps: whether the copy reads the game's skin tint
        // (_SkinColor): only a copy on the game's own shader does — on any other the caller lays that tint into its colour (Own)
        internal static Material SeeThrough(Material m, int mode, bool depth, bool last, out bool keeps)
        {
            keeps = false;
            if (m == null) return null;
            TellShader(m);
            Material e;
            if (mode == 3)
            {
                Material g = GhostMaterial();
                if (g == null) return null;
                e = new Material(g) { name = m.name + " (ghost)" };
                Texture tex = m.HasProperty("_MainTex") ? m.mainTexture : null;
                if (tex != null) { e.mainTexture = tex; if (e.HasProperty("_EmissionMap")) e.SetTexture("_EmissionMap", tex); }
                if (e.HasProperty("_EmissionColor")) e.SetColor("_EmissionColor", Color.white * GhostGlow);
                if (m.HasProperty("_BumpMap") && e.HasProperty("_BumpMap")) e.SetTexture("_BumpMap", m.GetTexture("_BumpMap"));
                e.renderQueue = (int)RenderQueue.Transparent + (last ? 1 : 0);
                return e;
            }
            if (CanBlend(m)) { e = new Material(m) { name = m.name + " (see-through)" }; keeps = true; }
            else
            {
                if (_standard == null) _standard = FbxLook.Standard; // (Shader.Find does not see it here)
                if (_standard == null) return null;
                e = new Material(_standard) { name = m.name + " (see-through)" };
                if (m.HasProperty("_MainTex")) e.mainTexture = m.mainTexture;
                Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                if (bump != null) { e.SetTexture("_BumpMap", bump); e.EnableKeyword("_NORMALMAP"); }
                e.SetFloat("_Glossiness", Gloss);
                e.SetFloat("_Metallic", 0f);
                Color glow = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black; // (its own light: the game's Ghost shines 2.83 of its picture — without it, a dark bone in its own glow: seen 2026-11-15)
                if (glow.maxColorComponent > 0.01f && Shine > 0f)
                {
                    e.EnableKeyword("_EMISSION");
                    e.SetColor("_EmissionColor", glow * Shine);
                    Texture map = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                    if (map != null) e.SetTexture("_EmissionMap", map);
                    e.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                TellShader(e);
            }
            e.SetFloat("_Mode", mode == 2 ? 3f : 2f);
            e.SetFloat("_SrcBlend", (float)(mode == 2 ? BlendMode.One : BlendMode.SrcAlpha));
            e.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            e.SetFloat("_ZWrite", depth ? 1f : 0f);
            e.DisableKeyword("_ALPHATEST_ON");
            e.EnableKeyword(mode == 2 ? "_ALPHAPREMULTIPLY_ON" : "_ALPHABLEND_ON");
            e.DisableKeyword(mode == 2 ? "_ALPHABLEND_ON" : "_ALPHAPREMULTIPLY_ON");
            e.SetOverrideTag("RenderType", "Transparent");
            e.renderQueue = (int)RenderQueue.Transparent + (last ? 1 : 0);
            return e;
        }

        // ---------------------------------------------------------------- fading

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

        // come in whole: the see-through copies were only for the coming (the flat ones unlit and drawn in no order — Ketil 2026-11-14 stood
        // flat and bright, his body over his shirt, at times bare); from then on it wears the game's own picture again
        private static bool CameIn(string v)
        {
            if (!Coming(v) || ZNet.instance == null) return false;
            string[] w = v.Split(' ');
            double from; float dur;
            return w.Length >= 2 && double.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out from) && float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dur)
                && ZNet.instance.GetTimeSeconds() - from >= dur;
        }

        // the game's own materials, meshes, shadows and tint blocks back on every renderer we painted (its copies go with the component: OnDestroy)
        private readonly Dictionary<Renderer, Mesh> _meshWas = new Dictionary<Renderer, Mesh>();
        private readonly Dictionary<Renderer, ShadowCastingMode> _shadowWas = new Dictionary<Renderer, ShadowCastingMode>();
        private readonly Dictionary<Renderer, Material[]> _origin = new Dictionary<Renderer, Material[]>(); // (the game's materials of a renderer as we first found it)
        private readonly Dictionary<Renderer, Color> _blockWas = new Dictionary<Renderer, Color>(); // (a renderer whose own block carries _Color: what it was)
        private void Restore()
        {
            Dictionary<Material, Material> back = new Dictionary<Material, Material>();
            foreach (KeyValuePair<Material, Material> m in _made) back[m.Value] = m.Key;
            foreach (Renderer r in _ours.Keys)
            {
                if (r == null) continue;
                Material[] orig;
                if (_origin.TryGetValue(r, out orig)) { r.sharedMaterials = orig; continue; } // (as it was before we touched it: the game may have made an instance of our copy since — 2026-11-15, a mode change copied our flat copy)
                Material[] mats = r.sharedMaterials;
                List<Material> keep = new List<Material>(mats.Length);
                Material was;
                foreach (Material m in mats) { if (IsPrepass(m)) continue; keep.Add(m != null && back.TryGetValue(m, out was) ? was : m); } // (our depth pass off)
                r.sharedMaterials = keep.ToArray();
            }
            _origin.Clear();
            foreach (KeyValuePair<Renderer, Mesh> m in _meshWas)
            {
                if (m.Key == null) continue;
                SkinnedMeshRenderer sk = m.Key as SkinnedMeshRenderer;
                MeshFilter mf = sk == null ? m.Key.GetComponent<MeshFilter>() : null;
                if (sk != null && _white.Contains(sk.sharedMesh)) sk.sharedMesh = m.Value; else if (mf != null && _white.Contains(mf.sharedMesh)) mf.sharedMesh = m.Value; // (only where our white copy still is)
            }
            foreach (KeyValuePair<Renderer, ShadowCastingMode> s in _shadowWas) if (s.Key != null) s.Key.shadowCastingMode = s.Value;
            foreach (KeyValuePair<Renderer, Color> b in _blockWas)
            {
                if (b.Key == null) continue;
                if (_block == null) _block = new MaterialPropertyBlock();
                b.Key.GetPropertyBlock(_block); _block.SetColor("_Color", b.Value); b.Key.SetPropertyBlock(_block);
            }
            _ours.Clear(); _meshWas.Clear(); _shadowWas.Clear(); _blockWas.Clear();
        }

        // Mode or Depth changed (the author trying them live): the game's picture back, ours made again on the next look
        private void Anew()
        {
            Restore();
            foreach (Material m in _mine.Keys) if (m != null) Destroy(m);
            foreach (Mesh m in _white) if (m != null) Destroy(m);
            _mine.Clear(); _made.Clear(); _white.Clear(); _lit.Clear(); _told.Clear();
            _mode = Mode; _depth = Depth; _glow = GhostGlow; _gloss = Gloss; _shine = Shine; _alpha = -1f; _nextLook = 0f;
        }

        // how much of it is left (1 whole … 0 gone); its sound once, as it begins (a late look stays quiet)
        private float Left()
        {
            string v = Fading(Zdo);
            if (v.Length == 0 || ZNet.instance == null) return 1f;
            string[] w = v.Split(' ');
            double from; float dur;
            if (w.Length < 2 || !double.TryParse(w[0], NumberStyles.Float, CultureInfo.InvariantCulture, out from) || !float.TryParse(w[1], NumberStyles.Float, CultureInfo.InvariantCulture, out dur)) return 1f;
            double since = ZNet.instance.GetTimeSeconds() - from;
            if (_heard != v) { _heard = v; if (w.Length > 2 && w[2] != "-" && since < 1.5) Audio.Sound(w[2], transform.position + Vector3.up * 1.4f, new SoundAsk()); }
            if (Coming(v)) return Mathf.Clamp01((float)since / dur); // (coming in: from nothing to whole)
            return 1f - Mathf.Clamp01((float)since / dur);
        }

        // an echo's own light grey smoke: slow wisps round it, rising a little, as much as it is there (each game its own)
        internal static float SmokeRate = 8.05f; // (wisps a second at full: 7, then 15% more — the user 2026-11-15)
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

        // what each renderer wears of ours: put back every frame if the game swapped it (a look of a reference each, nothing made); its tint block:
        // the flat copy of an echo shows only its own colour (the block off), a lit copy keeps the block (skin, hair) with our colour in it
        private readonly Dictionary<Renderer, Material[]> _ours = new Dictionary<Renderer, Material[]>();
        private void Keep()
        {
            _adopted.Clear();
            foreach (KeyValuePair<Renderer, Material[]> o in _ours)
            {
                Renderer r = o.Key;
                if (r == null || o.Value.Length == 0) continue;
                if (r.sharedMaterial != o.Value[0])
                {
                    Material[] now = Adopt(r.sharedMaterials, o.Value);
                    if (now != null) _adopted[r] = now; // (the game cloned ours: the clone is ours now — not put back)
                    else r.sharedMaterials = o.Value;
                }
                if (!r.HasPropertyBlock()) continue;
                Material first = o.Value[0];
                if (first != null && _lit.Contains(first))
                {
                    if (_block == null) _block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(_block);
                    if (!_block.HasColor("_Color")) continue; // (the game's block tints by _SkinColor only: ours shows)
                    if (!_blockWas.ContainsKey(r)) _blockWas[r] = _block.GetColor("_Color");
                    Color want = first.GetColor("_Color");
                    if (_block.GetColor("_Color") != want) { _block.SetColor("_Color", want); r.SetPropertyBlock(_block); }
                }
                else if (Echo > 0f) r.SetPropertyBlock(null); // (the game's tint block, set again with its hair)
            }
            foreach (KeyValuePair<Renderer, Material[]> a in _adopted) _ours[a.Key] = a.Value;
        }

        // The game's dressing of a player's body (VisEquipment.UpdateColors, every frame) reads «renderer.materials», which clones any
        // material the renderer does not own — ours too: put back each frame, cloned each frame, two lost materials a frame on every person
        // seen through (the reviewer of 2026-11-15, read in the game's code; the old flat copy did the same since 2026-11-02). A clone of ours
        // is taken as ours instead (its colour from ours, kept by Paint from now on): the renderer owns it, the game clones nothing more.
        // The new array when every difference is such a clone; null: something else is there (the game's own again) — put ours back
        private readonly Dictionary<Renderer, Material[]> _adopted = new Dictionary<Renderer, Material[]>();
        private Material[] Adopt(Material[] now, Material[] ours)
        {
            if (now == null || now.Length != ours.Length) return null;
            for (int i = 0; i < now.Length; i++)
            {
                if (now[i] == ours[i]) continue;
                if (now[i] == null || ours[i] == null || now[i].shader != ours[i].shader || now[i].name != ours[i].name + " (Instance)") return null;
            }
            for (int i = 0; i < now.Length; i++)
            {
                if (now[i] == ours[i]) continue;
                Color own; if (_mine.TryGetValue(ours[i], out own)) _mine[now[i]] = own;
                if (_lit.Contains(ours[i])) _lit.Add(now[i]);
                if (Blueprint.On) Npcs.Log("see-through: «" + ours[i].name + "» cloned by the game on " + (_a != null ? _a.Id : name) + " — the clone taken as ours");
            }
            return (Material[])now.Clone();
        }

        private void LateUpdate()
        {
            if (_a == null && (_nv == null || !_nv.IsValid())) { Destroy(this); return; } // (its person, or its creature, gone)
            if (_mode != Mode || _depth != Depth || _glow != GhostGlow || _gloss != Gloss || _shine != Shine) Anew();
            Keep();
            if (Echo > 0f && _smoke == null) _smoke = Smoke(transform);
            if (_smoke != null) { ParticleSystem.EmissionModule em = _smoke.emission; em.rateOverTime = SmokeRate * Mathf.Clamp01(_alpha < 0f ? 0f : _alpha / Mathf.Max(0.01f, Echo)); }
            if (Time.time >= _nextLook) { _nextLook = Time.time + 0.5f; Look(); }
            float left = Left(), alpha = (Echo > 0f ? Echo : 1f) * left;
            if (Echo <= 0f && left >= 1f && Coming(Fading(Zdo))) { Restore(); Destroy(this); return; } // (come in whole: its own picture again — Keep gives none while it stays so)
            if (Mathf.Abs(alpha - _alpha) > 0.002f)
            {
                _alpha = alpha;
                foreach (KeyValuePair<Material, Color> m in _mine) if (m.Key != null) m.Key.SetColor("_Color", Paint(m.Key, m.Value, alpha));
            }
            if (left <= 0f && !_gone && _a != null && _a.Owned && !Coming(Fading(Zdo))) { _gone = true; _a.Remove(); }
        }

        // the colour of one of ours: an echo pale blue; anyone else white when lit, as bright as the hour when flat (the flat copy is unlit);
        // the ghost's way (Mode 2) gives its light by alpha too (One/OneMinusSrcAlpha: nothing of it at 0)
        private Color Paint(Material ours, Color own, float alpha)
        {
            bool lit = _lit.Contains(ours);
            Color t;
            if (Echo > 0f) t = new Color(Pale.r, Pale.g, Pale.b, alpha);
            else if (lit) t = new Color(1f, 1f, 1f, alpha);
            else
            {
                Color amb = RenderSettings.ambientLight;
                float l = Mathf.Clamp(Mathf.Max(amb.r, Mathf.Max(amb.g, amb.b)) * 1.6f, 0.35f, 1f);
                t = new Color(l, l, l, alpha);
            }
            Color c = new Color(t.r * own.r, t.g * own.g, t.b * own.b, t.a);
            if (lit && _mode >= 2) { c.r *= c.a; c.g *= c.a; c.b *= c.a; } // (the ghost's ways: its light by alpha too)
            return c;
        }

        // the renderers as they are now: any of the game's materials swapped for our see-through copy
        private void Look()
        {
            VisEquipment vis = GetComponent<VisEquipment>();
            Renderer body = vis != null ? vis.m_bodyModel : null; // (drawn after its clothes and hair: SeeThrough «last»)
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) // heavy: every 0.5 s per one seen through (clothes the game puts on later are caught so)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material[] mats = r.sharedMaterials;
                if (!_origin.ContainsKey(r)) _origin[r] = (Material[])mats.Clone();
                bool changed = false, flat = false;
                bool cards = Cards(_origin[r]); // (hair, a beard: cut-out cards — their whole quads would write depth and cut the body behind them)
                Vector4 block = Vector4.zero; // (the game may tint by the renderer's own block instead of the material)
                if (r.HasPropertyBlock()) { if (_block == null) _block = new MaterialPropertyBlock(); r.GetPropertyBlock(_block); block = _block.GetVector("_SkinColor"); }
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (IsPrepass(m)) continue; // (our depth pass — or the game's clone of it: not the game's)
                    if (m == null || _mine.ContainsKey(m)) { if (m != null && !_lit.Contains(m)) flat = true; continue; }
                    Material e;
                    if (!_made.TryGetValue(m, out e))
                    {
                        bool echo = Echo > 0f;
                        Color own;
                        bool keeps = false;
                        e = _mode > 0 ? SeeThrough(m, _mode, _depth > 0 && !cards, r == body, out keeps) : null;
                        if (e != null)
                        {
                            own = keeps ? (m.HasProperty("_Color") ? m.color : Color.white) : Own(m, block); // (on the game's own shader its block tints skin and hair still; on any other the tint goes into our colour)
                            if (echo) { float g = own.r * 0.3f + own.g * 0.59f + own.b * 0.11f; own = Color.Lerp(own, new Color(g, g, g), EchoGrey); } // (an echo: its colours dimmed to one grey, the picture's light and shade kept)
                            own.a = 1f;
                            _lit.Add(e);
                        }
                        else
                        {
                            if (_shader == null) { _shader = Shader.Find("Sprites/Default"); Npcs.Log("fade shader: " + (_shader != null ? _shader.name : "none")); }
                            if (_shader == null) continue;
                            e = new Material(_shader) { name = m.name + " (fade)", renderQueue = 3000 };
                            if (m.HasProperty("_MainTex") && !echo) e.mainTexture = m.mainTexture;
                            own = echo ? EchoTone(m) : Own(m, block);
                            if (_mode > 0) Npcs.Log("see-through: «" + m.name + "» — no lit copy (mode " + _mode + "): the flat one");
                        }
                        e.SetColor("_Color", Paint(e, own, _alpha < 0f ? 1f : _alpha));
                        _made[m] = e; _mine[e] = own;
                        _alpha = -1f; // (the new one painted on the next frame)
                    }
                    if (!_lit.Contains(e)) flat = true;
                    mats[i] = e; changed = true;
                }
                if (changed && _depth >= 2 && !cards && mats.Length == 1 && Parts(r) == 1 && Prepass() != null) mats = new[] { mats[0], _prepass }; // (a second material on a one-part mesh draws it again: its depth, a queue earlier)
                if (changed) r.sharedMaterials = mats;
                if (!_ours.ContainsKey(r)) _ours[r] = (Material[])mats.Clone();
                if (changed && flat && Echo > 0f && r.HasPropertyBlock()) { if (Blueprint.On) Npcs.Log("echo look: " + r.name + " had its own block (its _Color " + (_block != null ? _block.GetColor("_Color").ToString() : "?") + "): taken off, our colour shows"); r.SetPropertyBlock(null); } // (the flat copy: the game tints by a block over the material — its colour, not ours, was drawn)
                if (!_shadowWas.ContainsKey(r)) _shadowWas[r] = r.shadowCastingMode;
                if (r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.Off;
                if (flat && r != body) WhiteVertices(r); // (only Sprites/Default multiplies by the vertex colours; the body: the game puts its own mesh back every frame — VisEquipment.UpdateBaseModel — a copy would be made anew each look)
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
            string s = "echo look (mode " + _mode + ", depth " + _depth + "): " + r.name + " mesh " + (mesh != null ? mesh.name + (mesh.isReadable ? " readable" : " NOT readable") + (_white.Contains(mesh) ? " (white copy)" : "") : "none");
            foreach (Material e in mats)
            {
                if (e == null) continue;
                Color c = e.HasProperty("_Color") ? e.GetColor("_Color") : Color.clear;
                s += " | " + e.name + " [" + (e.shader != null ? e.shader.name : "?") + " q" + e.renderQueue + "] tex " + (e.mainTexture != null ? e.mainTexture.name + " avg " + Average(e.mainTexture) : "none") + " colour " + c.r.ToString("0.00") + "/" + c.g.ToString("0.00") + "/" + c.b.ToString("0.00") + "/" + c.a.ToString("0.00");
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
        // colours — only for the flat copy; a mesh the game keeps unreadable stays as it is
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
                if (!_meshWas.ContainsKey(r)) _meshWas[r] = m;
                if (sk != null) sk.sharedMesh = copy; else mf.sharedMesh = copy;
            }
            catch (System.Exception e) { Npcs.Log("echo mesh " + m.name + ": " + e.Message); }
        }

        // the flat copy of an echo is one even colour, its hair and beard a shade darker than its body (the game's pictures came out black or
        // white on the see-through shader: measured 2026-11-04)
        internal static float EchoHair = 0.6f, EchoBody = 0.6f; // (hair as the body: the user 2026-11-15 «борода його з волоссям явно виділяються» — once 0.32, a shade darker)
        internal static float EchoGrey = 0.75f; // (the lit copy of an echo: how far its own colours go to grey — live: raven npc tune NpcFade.EchoGrey)
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
            _mine.Clear(); _made.Clear(); _white.Clear(); _lit.Clear();
            if (_a != null) Given.Remove(_a);
        }
    }
}
#endif

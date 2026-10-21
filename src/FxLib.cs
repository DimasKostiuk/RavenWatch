#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Our effects library (tab «Фарби» → «Ефекти»): an effect of ours is a base — any of the game's VFX (whatever has
    // particles or a light: a fire's flame, a staff's sparks, a blow's flash, a boss's smoke) or one of our own sheets
    // (feathers, runes, smoke) — and how we use it on a raven:
    //   - «Аура»: always on, at a bone;
    //   - «Слід»: left behind in the air as the raven flies (particles in the world, made by the metre flown).
    // Colour, size, amount, pace and life are ours; everything else is the game's own effect.
    // A look names the effects it wears (Look.Fx); the Guide wears them. An effect never goes under the raven's model
    // (the shoulder's life takes every transform there for a bone, see Feathers): it follows its bone from outside.
    //
    // Files: config/RavenWatch/fx/<id>.txt:
    //   name: Жар-пір'я
    //   base: game:vfx_Burning      game:<prefab> | ours:feathers | ours:runes | ours:smoke
    //   kind: aura                  aura | trail
    //   bone: back                  LookThings.BoneIds
    //   at 0 0.02 0                 the place on the bone (the Workshop's frame there)
    //   tint FF6020                 (left out: the game's colours)
    //   size 1  amount 1  speed 1  life 1
    internal class RavenFxDef
    {
        public string Id = "", Name = "Новий ефект", Base = "", Kind = "aura", Bone = "back";
        public string Desc = "";  // a short line for the book (a rune's effect)
        public string Element = ""; // «element: fire» (Elements): its own, else the catalog's table
        public bool Rune;         // a rune may give it (WearCatalog): an aura → Бальдр, a trail → Геймдаль
        public Vector3 At;
        public bool Tinted; public Color Tint = new Color(1f, 0.5f, 0.2f);
        public float Size = 1f, Amount = 1f, Speed = 1f, Life = 1f;
        public System.DateTime SavedAt;
        internal bool NoLight; // (not in the file) its lights left out: a mix keeps one light for all its parts (FxMix)
        internal bool Trail { get { return Kind == "trail"; } }

        internal string Code()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("name: ").Append(Name).Append('\n');
            sb.Append("base: ").Append(Base).Append('\n');
            sb.Append("kind: ").Append(Kind).Append('\n');
            sb.Append("bone: ").Append(Bone).Append('\n');
            if (Rune) sb.Append("rune: yes").Append('\n');
            if (Desc.Length > 0) sb.Append("desc: ").Append(Desc).Append('\n');
            if (Element.Length > 0) sb.Append("element: ").Append(Element).Append('\n');
            sb.Append("at ").Append(F(At.x)).Append(' ').Append(F(At.y)).Append(' ').Append(F(At.z)).Append('\n');
            if (Tinted) sb.Append("tint ").Append(ColorUtility.ToHtmlStringRGB(Tint)).Append('\n');
            sb.Append("size ").Append(F(Size)).Append('\n').Append("amount ").Append(F(Amount)).Append('\n')
              .Append("speed ").Append(F(Speed)).Append('\n').Append("life ").Append(F(Life)).Append('\n');
            return sb.ToString();
        }

        // what makes it be built anew (the rest is tuned on the living one)
        internal string Shape() { return Base + "|" + Kind + "|" + Bone; }

        internal static RavenFxDef Parse(string text, string id)
        {
            RavenFxDef d = new RavenFxDef { Id = id ?? "" };
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int c = line.IndexOf(':');
                if (c > 0 && line.IndexOf(' ') > c)
                {
                    string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
                    if (k == "name") d.Name = v; else if (k == "base") d.Base = v; else if (k == "kind") d.Kind = v == "trail" ? "trail" : "aura"; else if (k == "bone") d.Bone = v;
                    else if (k == "desc") d.Desc = v; else if (k == "element") d.Element = v.ToLowerInvariant(); else if (k == "rune") d.Rune = v == "yes" || v == "on" || v == "1" || v == "true";
                    continue;
                }
                string[] a = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                switch (a[0].ToLowerInvariant())
                {
                    case "at": if (a.Length > 3) d.At = new Vector3(N(a[1], 0f), N(a[2], 0f), N(a[3], 0f)); break;
                    case "tint": if (a.Length > 1) { Color t; if (ColorUtility.TryParseHtmlString("#" + a[1], out t)) { d.Tinted = true; d.Tint = t; } } break;
                    case "size": if (a.Length > 1) d.Size = N(a[1], 1f); break;
                    case "amount": if (a.Length > 1) d.Amount = N(a[1], 1f); break;
                    case "speed": if (a.Length > 1) d.Speed = N(a[1], 1f); break;
                    case "life": if (a.Length > 1) d.Life = N(a[1], 1f); break;
                }
            }
            if (System.Array.IndexOf(LookThings.BoneIds, d.Bone) < 0) d.Bone = "back";
            return d;
        }

        internal RavenFxDef Copy() { RavenFxDef c = Parse(Code(), Id); c.SavedAt = SavedAt; return c; }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static float N(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
    }

    internal static class FxLib
    {
        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "fx"); } }
        internal static int Version; // goes up on every save: the Guide dresses again

        // the effects that ship in the mod (the user's approved files get baked here)
        // the rune effects (Бальдр: auras, Геймдаль: trails): the first draft, to be tuned in «Фарби» → «Ефекти»
        private static readonly string[] Builtin =
        {
            "id: b_zharyny\nname: Жарини\nbase: game:bow_projectile_fire\nkind: aura\nbone: back\nrune: yes\ndesc: довкола ворона кружляють жарини\nat 0 0 0\ntint FF7A20\nsize 0.6\namount 0.8\nspeed 1\nlife 1\n",
            "id: b_iskry\nname: Іскри Тора\nbase: game:staff_lightning_projectile\nkind: aura\nbone: back\nrune: yes\ndesc: по пір'ю бігають криваво-червоні іскри\nat 0 0 0\ntint 720007\nsize 1.281\namount 0.564\nspeed 0.37\nlife 1.762\n",
            "id: b_inii\nname: Подих інею\nbase: game:dragon_ice_projectile\nkind: aura\nbone: back\nrune: yes\ndesc: від ворона тягне морозом\nat 0 0 0\ntint C8E8FF\nsize 0.5\namount 0.7\nspeed 1\nlife 1\n",
            "id: b_svitliachky\nname: Світлячки\nbase: game:FireFlies\nkind: aura\nbone: back\nrune: yes\ndesc: довкола літають світлячки\nat 0 0 0\ntint D8FF80\nsize 0.387\namount 0.455\nspeed 0.391\nlife 0.2\n",
            "id: b_dym\nname: Дим\nbase: ours:smoke\nkind: aura\nbone: back\nrune: yes\ndesc: темний дим тягнеться від крил\nat 0 0 0\ntint 3B474A\nsize 1.071\namount 1.87\nspeed 0.664\nlife 1\n",
            "id: b_runy\nname: Руни Бальдра\nbase: ours:runes\nkind: aura\nbone: back\nrune: yes\ndesc: довкола ворона повільно пливуть блакитні руни\nat 0 0 0\ntint 85C9F2\nsize 0.8\namount 0.6\nspeed 1\nlife 1\n",
            "id: b_tuman\nname: Кривавий туман\nbase: ours:smoke\nkind: aura\nbone: back\nrune: yes\ndesc: ворона огортає червоний туман\nat 0 0 0\ntint A40D13\nsize 1.497\namount 3.119\nspeed 0.159\nlife 0.919\n",
            "id: b_spory\nname: Спори\nbase: game:MushroomSmokePuff\nkind: aura\nbone: back\nrune: yes\ndesc: з пір'я сиплються світлі спори\nat 0 0 0\ntint 80E0C0\nsize 0.749\namount 0.935\nspeed 0.958\nlife 1\n",
            "id: h_pirya\nname: Пір'я\nbase: ours:feathers\nkind: trail\nbone: back\nrune: yes\ndesc: за вороном у польоті сиплеться пір'я\nat 0 0 0\nsize 1.355\namount 1.652\nspeed 1.273\nlife 1.467\n",
            "id: h_bile_pirya\nname: Біле пір'я\nbase: ours:feathers\nkind: trail\nbone: back\nrune: yes\ndesc: за вороном летить біле пір'я\nat 0 0 0\ntint F0F0F0\nsize 1\namount 2.087\nspeed 1\nlife 1\n",
            "id: h_runy\nname: Руни в небі\nbase: ours:runes\nkind: trail\nbone: back\nrune: yes\ndesc: за вороном лишаються сині руни\nat 0 0 0\ntint 80B8FF\nsize 0.978\namount 1.017\nspeed 0.874\nlife 1\n",
            "id: h_vohon\nname: Вогняний слід\nbase: game:bow_projectile_fire\nkind: trail\nbone: back\nrune: yes\ndesc: за вороном тягнеться вогонь\nat 0 0 0\ntint FF8030\nsize 0.7\namount 1\nspeed 1\nlife 1\n",
            "id: h_blyskavka\nname: Блискавка\nbase: game:staff_lightning_projectile\nkind: trail\nbone: back\nrune: yes\ndesc: ворон летить, як блискавка\nat 0 0 0\ntint B0E0FF\nsize 0.955\namount 1.145\nspeed 0.517\nlife 1\n",
            "id: h_snih\nname: Сніговий слід\nbase: game:dragon_ice_projectile\nkind: trail\nbone: back\nrune: yes\ndesc: за вороном кружляє сніг\nat 0 0 0\ntint F0F8FF\nsize 0.138\namount 0.203\nspeed 0.243\nlife 1\n",
            "id: h_dym\nname: Димний слід\nbase: ours:smoke\nkind: trail\nbone: back\nrune: yes\ndesc: за крилами стелиться дим\nat 0 0 0\ntint 5A5A5A\nsize 1.284\namount 1\nspeed 1\nlife 1\n",
            "id: h_svitliachky\nname: Слід світлячків\nbase: ours:streak\nkind: trail\nbone: tail\nrune: yes\ndesc: за вороном тягнеться світна стрічка світлячків\nat 0 0 0\ntint E0FF90\nsize 1\namount 1\nspeed 1\nlife 1.5\n",
        };

        private static List<RavenFxDef> _all;
        private static int _allAt = -1;

        // ours: built in, and (while files are read) the saved ones over them
        internal static List<RavenFxDef> All()
        {
            if (_all != null && _allAt == Version) return _all;
            _allAt = Version;
            Dictionary<string, RavenFxDef> by = new Dictionary<string, RavenFxDef>();
            foreach (string s in Builtin)
            {
                string id = "";
                foreach (string line in s.Split('\n')) if (line.StartsWith("id:")) id = line.Substring(3).Trim();
                by[id] = RavenFxDef.Parse(s, id);
            }
            if (RavenLooks.FromFiles && Directory.Exists(Dir))
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { RavenFxDef d = RavenFxDef.Parse(File.ReadAllText(f, Encoding.UTF8), Path.GetFileNameWithoutExtension(f)); d.SavedAt = File.GetLastWriteTimeUtc(f); by[d.Id] = d; }
                    catch (System.Exception e) { Perches.Log("effect " + f + ": " + e.Message); }
                }
            if (_live != null) by[_live.Id] = _live;
            _all = new List<RavenFxDef>(by.Values);
            _all.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.CurrentCulture));
            return _all;
        }

        // one effect being edited in the test window (tab «Руни»): worn as it is now by everything wearing its id, until
        // it is saved or dropped (null)
        private static RavenFxDef _live;
        internal static RavenFxDef Live { get { return _live; } set { _live = value; Version++; } }

        // its own file in fx/ (over the built-in one of the same id)
        internal static bool HasFile(string id) { return !string.IsNullOrEmpty(id) && File.Exists(Path.Combine(Dir, id + ".txt")); }
        internal static bool IsBuiltin(string id) { foreach (string s in Builtin) if (s.StartsWith("id: " + id + "\n", System.StringComparison.Ordinal)) return true; return false; }

        internal static RavenFxDef Find(string id) { foreach (RavenFxDef d in All()) if (d.Id == id) return d; return null; }

        internal static void Save(RavenFxDef d)
        {
            Directory.CreateDirectory(Dir);
            if (string.IsNullOrEmpty(d.Id))
            {
                string id = Perches.IdFor(d.Name), at = id;
                for (int n = 2; File.Exists(Path.Combine(Dir, at + ".txt")); n++) at = id + "_" + n;
                d.Id = at;
            }
            File.WriteAllText(Path.Combine(Dir, d.Id + ".txt"), d.Code(), Encoding.UTF8);
            Version++;
            RavenLooks.Version++; // the Guide wears it anew
        }

        internal static void Delete(RavenFxDef d)
        {
            string f = Path.Combine(Dir, d.Id + ".txt");
            if (File.Exists(f)) File.Delete(f);
            Version++;
            RavenLooks.Version++;
        }

        // ---------------------------------------------------------------- the bases there are
        internal const string GamePrefix = "game:", OursPrefix = "ours:";
        internal static readonly string[] Ours = { "feathers", "runes", "smoke", "streak" };
        internal static readonly string[] OursNames = { "Наші пір'я", "Наші руни", "Наш дим", "Наша світна стрічка" };

        internal class Base { public string Src, Label; public bool Loop, Flash; public int Systems, Lights; }
        private static List<Base> _bases;
        private static ZNetScene _basesFor;

        // every prefab of the game with particles or a light (once per world: the mods add theirs as it loads)
        internal static List<Base> Bases()
        {
            if (_bases != null && _basesFor == ZNetScene.instance) return _bases;
            _bases = new List<Base>();
            _basesFor = ZNetScene.instance;
            for (int i = 0; i < Ours.Length; i++) _bases.Add(new Base { Src = OursPrefix + Ours[i], Label = OursNames[i], Loop = true });
            if (ZNetScene.instance == null) return _bases;
            List<GameObject> prefabs = new List<GameObject>(ZNetScene.instance.m_prefabs);
            prefabs.AddRange(ZNetScene.instance.m_nonNetViewPrefabs);
            HashSet<string> seen = new HashSet<string>();
            foreach (GameObject g in prefabs)
            {
                if (g == null || !seen.Add(g.name) || g.GetComponent<Player>() != null) continue;
                ParticleSystem[] ps = g.GetComponentsInChildren<ParticleSystem>(true);
                Light[] ls = g.GetComponentsInChildren<Light>(true);
                if (ps.Length == 0 && ls.Length == 0) continue;
                bool loop = false;
                foreach (ParticleSystem p in ps) if (p.main.loop) { loop = true; break; }
                string n = g.name.ToLowerInvariant();
                _bases.Add(new Base { Src = GamePrefix + g.name, Label = g.name, Loop = loop || ls.Length > 0, Flash = n.StartsWith("vfx_") || n.StartsWith("fx_"), Systems = ps.Length, Lights = ls.Length });
            }
            Perches.Log("effects library: " + (_bases.Count - Ours.Length) + " game prefabs with particles or lights");
            return _bases;
        }

        // the base brings a light of its own
        internal static bool HasLight(string src) { GameObject t = Template(src); return t != null && t.GetComponentInChildren<Light>(true) != null; }

        internal static string LabelOf(string src)
        {
            if (src.StartsWith(OursPrefix, System.StringComparison.Ordinal)) { int i = System.Array.IndexOf(Ours, src.Substring(OursPrefix.Length)); return i >= 0 ? OursNames[i] : src; }
            return src.StartsWith(GamePrefix, System.StringComparison.Ordinal) ? src.Substring(GamePrefix.Length) : src;
        }

        // ---------------------------------------------------------------- a base's still copy (switched off, cloned on use)
        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();

        internal static GameObject Template(string src)
        {
            GameObject t;
            if (string.IsNullOrEmpty(src)) return null;
            // a base that gave nothing (kept as a real null) is not tried again; a copy the game destroyed is made anew
            if (Templates.TryGetValue(src, out t) && (ReferenceEquals(t, null) || t != null)) return t;
            t = null;
            try { t = src.StartsWith(OursPrefix, System.StringComparison.Ordinal) ? MakeOurs(src.Substring(OursPrefix.Length)) : CopyGame(src.StartsWith(GamePrefix, System.StringComparison.Ordinal) ? src.Substring(GamePrefix.Length) : src); }
            catch (System.Exception e) { Perches.Log("effect base " + src + ": " + e.Message); }
            if (t != null || ZNetScene.instance != null) Templates[src] = t; // (before the world is up: asked again later)
            return t;
        }

        private static GameObject NewRoot(string name)
        {
            GameObject root = new GameObject("RWFX_" + name);
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(root);
            return root;
        }

        // the game's effect: its particle systems and lights with their children, nothing else (no damage, sound, logic)
        private static GameObject CopyGame(string name)
        {
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (g == null) return null;
            GameObject root = null;
            foreach (Transform n in g.GetComponentsInChildren<Transform>(true))
            {
                if (!IsFx(n)) continue;
                bool under = false;
                for (Transform up = n.parent; up != null; up = up.parent) if (IsFx(up)) { under = true; break; }
                if (under) continue;
                if (root == null) root = NewRoot(name);
                GameObject c = Object.Instantiate(n.gameObject, root.transform, false);
                c.SetActive(true);
                c.transform.localPosition = g.transform.InverseTransformPoint(n.position);
                c.transform.localRotation = Quaternion.Inverse(g.transform.rotation) * n.rotation;
                Vector3 a = n.lossyScale, b = g.transform.lossyScale;
                c.transform.localScale = new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z));
                Strip(c.transform);
                // (copied under a switched-off root, nothing of it woke; whatever could not be taken off — a piece of the
                // game's logic, a network object — must never wake later on a raven: the whole copy goes)
                if (!OnlyLooks(c.transform)) { Perches.Log("effect base " + name + ": " + c.name + " keeps game logic, left out"); Object.DestroyImmediate(c); }
            }
            if (root != null && root.transform.childCount == 0) { Object.DestroyImmediate(root); root = null; }
            return root;
        }

        private static bool OnlyLooks(Transform copy)
        {
            foreach (Component k in copy.GetComponentsInChildren<Component>(true))
                if (!(k is Transform || k is ParticleSystem || k is ParticleSystemRenderer || k is Light || k is LightFlicker || k is LightLod || k is MeshRenderer || k is MeshFilter)) return false;
            return true;
        }

        private static bool IsFx(Transform t) { return t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null; }

        private static void Strip(Transform copy)
        {
            foreach (Transform x in copy.GetComponentsInChildren<Transform>(true))
            {
                for (int pass = 0; pass < 3; pass++)
                    foreach (Component k in x.GetComponents<Component>())
                    {
                        if (k == null || k is Transform || k is ParticleSystem || k is ParticleSystemRenderer || k is Light || k is LightFlicker || k is LightLod) continue;
                        if ((k is MeshRenderer || k is MeshFilter) && x.GetComponent<ParticleSystem>() != null) continue; // (a system's own mesh)
                        try { Object.DestroyImmediate(k); } catch { }
                    }
                ParticleSystem ps = x.GetComponent<ParticleSystem>();
                if (ps != null) { ParticleSystem.MainModule m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; m.playOnAwake = true; }
                Light l = x.GetComponent<Light>();
                if (l != null) l.shadows = LightShadows.None; // a light on every raven that wears it: no shadows of its own (their cost)
            }
        }

        // our sheets as a steady effect: feathers drifting off, runes rising, soft smoke
        private static GameObject MakeOurs(string which)
        {
            GameObject root = NewRoot(which);
            if (which == "streak") return MakeStreak(root);
            ParticleSystem ps;
            if (which == "runes") ps = Feathers.MakeRunes(root.transform, "runes", Vector3.zero);
            else ps = Feathers.Make(root.transform, which, Vector3.zero);
            if (ps == null) { Object.DestroyImmediate(root); return null; }
            if (which == "smoke") { Material sm = Feathers.Smoke(); if (sm != null) { ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = sm; ParticleSystem.TextureSheetAnimationModule ts = ps.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 2; ts.numTilesY = 2; } }
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = which == "smoke" ? new ParticleSystem.MinMaxCurve(0.15f, 0.3f) : which == "runes" ? new ParticleSystem.MinMaxCurve(0.05f, 0.09f) : new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.startColor = which == "runes" ? new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.8f, 1f), Color.white) : which == "smoke" ? new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.3f, 0.32f, 0.6f), new Color(0.5f, 0.5f, 0.52f, 0.5f)) : new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.11f, 0.13f), new Color(0.35f, 0.34f, 0.37f));
            main.gravityModifier = which == "feathers" ? 0.05f : which == "runes" ? -0.03f : -0.01f;
            main.maxParticles = 60;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = which == "smoke" ? 6f : 4f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.08f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            return root;
        }

        // A glowing streak left in the air: points dropped by the metre flown (they stay where they were left), joined
        // into one ribbon that thins and fades from the raven back, with a soft spark at each point. Life = its length,
        // size = its width, amount = how dense the sparks, tint = its colour (white: takes the tint as it is).
        private static GameObject MakeStreak(GameObject root)
        {
            GameObject g = new GameObject("streak");
            g.transform.SetParent(root.transform, false);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; main.playOnAwake = true; main.duration = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 0.9f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.maxParticles = 300;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.rateOverDistance = 14f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = false; // (from the point itself: one line)
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
            ParticleSystem.TrailModule tr = ps.trails;
            tr.enabled = true;
            tr.mode = ParticleSystemTrailMode.Ribbon;
            tr.ribbonCount = 1;
            tr.lifetime = 1f;
            tr.dieWithParticles = true;
            tr.inheritParticleColor = true;
            tr.sizeAffectsWidth = true;
            tr.sizeAffectsLifetime = false;
            tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.6f);
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = GlowMat(ref _dotMat, true);
            r.trailMaterial = GlowMat(ref _ribbonMat, false);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return root;
        }

        private static Material _dotMat, _ribbonMat;

        // a soft glow picture on the sprite shader: a round spark, or a band soft on both edges (across the ribbon)
        private static Material GlowMat(ref Material m, bool dot)
        {
            if (m != null) return m;
            const int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a;
                    if (dot) { float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f; a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)); a *= a; }
                    else { float v = (y + 0.5f) / n; a = Mathf.Sin(v * Mathf.PI); a *= a; }
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply();
            Shader sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.mainTexture = t;
            return m;
        }

        // ---------------------------------------------------------------- on a raven
        // what the look names, on this raven (what it wore before from a look goes first)
        internal static void Hang(GameObject raven, IList<string> ids, bool munin, int layer)
        {
            Unhang(raven);
            if (raven == null || ids == null) return;
            foreach (string id in ids) { RavenFxDef d = Find(id); if (d != null) Put(raven, d, munin, layer); }
        }

        internal static void Unhang(GameObject raven)
        {
            for (int i = FxOn.All.Count - 1; i >= 0; i--) { FxOn f = FxOn.All[i]; if (f == null) { FxOn.All.RemoveAt(i); continue; } if (f.Raven == raven && !f.Worn) f.Off(); }
        }

        // one effect on one raven; null when its base or bone is not there
        internal static FxOn Put(GameObject raven, RavenFxDef d, bool munin, int layer)
        {
            string boneName = LookThings.BoneOf(d.Bone);
            Transform bone = boneName != null ? RavenGear.Bone(raven, boneName) : null;
            GameObject tpl = Template(d.Base);
            Quaternion rot; float scale;
            if (bone == null || tpl == null || !RavenGear.FrameOf(munin, boneName, out rot, out scale)) return null;
            GameObject root = new GameObject("RavenWatch_Fx " + d.Name);
            GameObject body = Object.Instantiate(tpl, root.transform, false);
            body.name = "body"; body.hideFlags = HideFlags.None;
            if (d.NoLight) foreach (Light l in body.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            FxOn on = root.AddComponent<FxOn>();
            on.Raven = raven; on.Live = true; on.Bone = bone; on.Rot = rot; on.Scale = scale; on.Body = body; on.Layer = layer >= 0 ? layer : raven.layer;
            on.Init(body.transform);
            on.Tune(d, true);
            body.SetActive(true);
            on.LateUpdate();
            return on;
        }
    }

    // One effect on a raven: follows its bone from outside the raven's model (place, turn, size of the raven), hides
    // with the raven, and takes our tuning over the game's own values (kept once, so tuning again starts from them).
    internal sealed class FxOn : MonoBehaviour
    {
        internal static readonly List<FxOn> All = new List<FxOn>();
        internal GameObject Raven, Body;
        internal Transform Bone;
        internal bool Live; // on a raven (when that raven is gone, the effect goes too)
        internal bool OnlyFlying; // a trail on a raven of ours: left only in the air, not while it sits
        internal bool Worn;       // put on by the runes (RavenWear): only RavenWear takes it off, never a look's Unhang
        private bool _emitting = true;
        internal Quaternion Rot;
        internal float Scale = 1f;
        internal int Layer;
        private Vector3 _at;
        private float _size = 1f;
        private bool _trail;
        private string _shape = "";

        private ParticleSystem[] _ps;
        private float[] _rate, _dist, _life, _speedK;
        private int[] _max;
        private short[][] _burst;
        private WsFx _tint;

        // Seen only with its raven: not when the raven's own body is not drawn (another player in Hugin's shadow — the
        // game draws nothing of them, so an aura must not hang there in the air), not while your own ravens wear the
        // shadow's dark copies, not far off. Its lights only close by (each wearer's raven brings its own).
        internal static float FarSeen = 80f, LightFar = 35f;
        private Renderer _skin;   // the raven's own body
        private Light[] _lights;
        private bool _world;      // in the world (a stage of the book or the panel draws on a layer of its own: always seen)
        private bool _hidden, _lit = true;
        private float _nextLook;

        // the raven's own drawn body: not what it wears or holds
        private static Renderer SkinOf(GameObject raven)
        {
            Renderer any = null;
            foreach (Renderer r in raven.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<Outfits.Piece>() != null || r.GetComponentInParent<RavenFetch.Carried>() != null) continue;
                if (r is SkinnedMeshRenderer) return r;
                if (any == null) any = r;
            }
            return any;
        }

        private void Look()
        {
            float d = 0f;
            if (_world && GameCamera.instance != null) d = Vector3.Distance(GameCamera.instance.transform.position, Bone.position);
            _hidden = (_skin != null && (!_skin.enabled || !_skin.gameObject.activeInHierarchy)) || Shadow.Fading(Raven) || d > FarSeen;
            bool lit = d <= LightFar;
            if (lit != _lit) { _lit = lit; foreach (Light l in _lights) if (l != null) l.enabled = lit; }
        }

        private void Awake() { All.Add(this); }
        private void OnDestroy() { All.Remove(this); }

        internal string Shape { get { return _shape; } }

        internal void Init(Transform body)
        {
            _ps = body.GetComponentsInChildren<ParticleSystem>(true);
            int n = _ps.Length;
            _rate = new float[n]; _dist = new float[n]; _life = new float[n]; _speedK = new float[n]; _max = new int[n]; _burst = new short[n][];
            for (int i = 0; i < n; i++)
            {
                ParticleSystem.MainModule m = _ps[i].main;
                ParticleSystem.EmissionModule e = _ps[i].emission;
                _rate[i] = e.rateOverTimeMultiplier; _dist[i] = e.rateOverDistanceMultiplier;
                _life[i] = m.startLifetimeMultiplier; _speedK[i] = m.simulationSpeed; _max[i] = m.maxParticles;
                ParticleSystem.Burst[] b = new ParticleSystem.Burst[e.burstCount];
                e.GetBursts(b);
                _burst[i] = new short[b.Length];
                for (int k = 0; k < b.Length; k++) _burst[i][k] = (short)Mathf.Clamp(b[k].maxCount, 0, short.MaxValue);
            }
            _tint = body.gameObject.AddComponent<WsFx>();
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            _lights = body.GetComponentsInChildren<Light>(true);
            _world = !string.IsNullOrEmpty(LayerMask.LayerToName(Layer));
            if (Raven != null) _skin = SkinOf(Raven);
        }

        // our tuning on the game's values; first: loops set and the trail's way of emitting
        internal void Tune(RavenFxDef d, bool first)
        {
            _at = d.At; _size = Mathf.Max(0.01f, d.Size); _trail = d.Trail; _shape = d.Shape();
            for (int i = 0; i < _ps.Length; i++)
            {
                ParticleSystem p = _ps[i];
                if (p == null) continue;
                ParticleSystem.MainModule m = p.main;
                ParticleSystem.EmissionModule e = p.emission;
                if (first)
                {
                    if (!m.loop) { p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); m.loop = true; } // a flash plays again and again
                    if (_trail) m.simulationSpace = ParticleSystemSimulationSpace.World; // left behind where it flew
                    m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                float amount = Mathf.Max(0f, d.Amount);
                if (_trail)
                {
                    // by the metre flown: its own distance rate, else made from its time rate (and a little by time still)
                    float perM = _dist[i] > 0f ? _dist[i] : Mathf.Max(3f, _rate[i] * 0.5f);
                    e.rateOverDistanceMultiplier = perM * amount;
                    e.rateOverTimeMultiplier = _rate[i] * amount * 0.15f;
                }
                else { e.rateOverTimeMultiplier = _rate[i] * amount; e.rateOverDistanceMultiplier = _dist[i] * amount; }
                if (_burst[i].Length > 0)
                {
                    ParticleSystem.Burst[] b = new ParticleSystem.Burst[_burst[i].Length];
                    e.GetBursts(b);
                    for (int k = 0; k < b.Length; k++) { short c = (short)Mathf.Max(amount > 0f ? 1 : 0, Mathf.RoundToInt(_burst[i][k] * amount)); b[k].minCount = c; b[k].maxCount = c; }
                    e.SetBursts(b);
                }
                m.startLifetimeMultiplier = _life[i] * Mathf.Max(0.05f, d.Life);
                m.simulationSpeed = _speedK[i] * Mathf.Max(0.05f, d.Speed);
                m.maxParticles = Mathf.Max(_max[i], Mathf.CeilToInt(_max[i] * Mathf.Max(1f, amount * d.Life)));
                if (first && !p.isPlaying) p.Play(false);
            }
            if (_tint != null) _tint.Recolor(d.Tinted, d.Tint);
        }

        internal void Again() { foreach (ParticleSystem p in _ps) if (p != null) { p.Clear(true); p.Play(true); } }

        // off: no new ones; the ones in the air finish (a trail), then it goes
        internal void Off()
        {
            Raven = null; Bone = null; Live = false;
            foreach (ParticleSystem p in _ps) if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (Light l in GetComponentsInChildren<Light>(true)) l.enabled = false;
            Destroy(gameObject, _trail ? 3f : 0f);
        }

        internal void LateUpdate()
        {
            if (Raven == null) { if (Live) Off(); return; } // (its raven destroyed with nobody taking it off)
            if (Bone == null) { Off(); return; }
            if (OnlyFlying)
            {
                RavenAct act = AnimDirector.Of(Raven);
                bool fly = act != RavenAct.Sit && act != RavenAct.Hover;
                if (fly != _emitting)
                {
                    _emitting = fly;
                    foreach (ParticleSystem p in _ps) if (p != null) { ParticleSystem.EmissionModule e = p.emission; e.enabled = fly; }
                }
            }
            bool seen = Bone.gameObject.activeInHierarchy; // the raven away (the game's own hides it)
            if (seen && Time.time >= _nextLook) { _nextLook = Time.time + 0.25f; Look(); }
            seen &= !_hidden;
            if (Body.activeSelf != seen) Body.SetActive(seen);
            float k = Mathf.Abs(Bone.lossyScale.x) / Mathf.Max(0.0001f, Scale);
            Quaternion frame = Bone.rotation * Quaternion.Inverse(Rot);
            transform.position = Bone.position + frame * (_at * k);
            transform.rotation = frame;
            transform.localScale = Vector3.one * (k * _size);
        }
    }
}
#endif

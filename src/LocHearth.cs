#if RAVEN_ITEM && RAVEN_NPC
// (the Hearth of the Watch's fires: from 2.9 — the user 2026-11-01)
using UnityEngine;

namespace HuginWhispers
{
    // The Hearth of the Watch (a story piece whose file says «script: hearth»; the Hall's, in its middle), and its three fires — one
    // for the world, kept by the place it stands in (its world key Loc.KeyFire: «ravenwatch_hall_fire <n>» for the Hall, so everyone sees
    // the same; the story asks «hearth <place> N», the place's own steps follow it — locations.list «becomes:»):
    //   0 — cold: ash. A piece of wood in the bag lights it (E; wood, not coal: a newcomer has no coal — the user 2026-11-02) — but not with the Watch's own fire:
    //   1 — Hel's fire (2.9): she still holds the Hall, lest the ravens' Watch wake again. A cold, pale sea-green flame
    //       with a deep violet smoke, low and slow, no sparks; it lights but does not warm. It burns on for ever, till:
    //   2 — the Watch's fire (3.0: kindled with the Feather of the Watch, the story's): thick gold, sparks and now and
    //       then a black feather rising; it warms as any fire. Till the story changes it again.
    // Hel's flame is ours (our smoke puffs, our glow); the Watch's is the game's own fire pit's, with our feathers.
    internal sealed class LocHearth : MonoBehaviour, Hoverable, Interactable
    {
        internal const int Cold = 0, Hel = 1, Watch = 2;
        private const string WoodItem = "$item_wood";
        private static readonly Vector3 FireAt = new Vector3(0f, 0.5f, 0f); // (the hearth's bowl, scale 2)
        private const float Near = 30f; // seen being lit: the burst and a raven's word

        // a place's fire (cold: no place, or none lit)
        internal static int LevelOf(Loc l)
        {
            string v; int n;
            if (l == null || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeyFire, out v)) return Cold;
            return int.TryParse(v, out n) ? Mathf.Clamp(n, Cold, Watch) : Watch; // (a bare key from an older test: the Watch's)
        }

        internal static void SetLevel(Loc l, int n)
        {
            if (l == null || ZoneSystem.instance == null) return;
            if (n <= Cold) ZoneSystem.instance.RemoveGlobalKey(l.KeyFire);
            else ZoneSystem.instance.SetGlobalKey(l.KeyFire + " " + Mathf.Clamp(n, Hel, Watch));
        }

        // the place this hearth stands in: stamped by its blueprint, or the author's own building by its frame — looked up once it is
        // in the world (never again: a piece does not move between places)
        private Loc _loc;
        private bool _looked;
        private Loc Place { get { if (!_looked && _nv != null && _nv.IsValid()) { _loc = Locs.Of(_nv); _looked = _loc != null; /* (no place yet: the author's frame set later, asked again) */ } return _loc; } }
        private int Level { get { return LevelOf(Place); } }
        private void SetLevel(int n) { SetLevel(Place, n); }

        internal static string WordOf(int n) { return n == Hel ? "вогонь Хели" : n == Watch ? "вогонь Варти" : "холодне"; }

        private int _shown = -1;
        private GameObject _fire;
        private Light _light;
        private float _next, _base;
        private ZNetView _nv;

        private void Awake() { _nv = GetComponent<ZNetView>(); }

        private void Update()
        {
            if (_nv == null || !_nv.IsValid()) return; // (the hammer's ghost, a blueprint's: no fire on a shadow)
            if (_light != null) _light.intensity = _base * (_shown == Hel ? 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 0.6f, 3.1f) : 1f);
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            int n = Level;
            if (n == _shown) return;
            bool seen = _shown >= 0 && n > _shown && Player.m_localPlayer != null && (Player.m_localPlayer.transform.position - transform.position).magnitude < Near;
            _shown = n;
            Make(n);
            if (seen) Lit(n);
        }

        private void OnDestroy() { if (_fire != null) Destroy(_fire); }

        // ---------------------------------------------------------------- E
        public string GetHoverName() { return Lang.T("Вогнище Варти"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string text = Hover();
            return Localization.instance != null ? Localization.instance.Localize(text) : text; // ($KEY_Use → the key, as every hover text of the game)
        }

        private string Hover()
        {
            string t = Lang.T("Вогнище Варти") + "\n";
            switch (Level)
            {
                case Hel:
                    return t + Lang.T("Горить чужим, холодним вогнем. Воно світить, та не гріє.")
                        + (HasFeather(Player.m_localPlayer) ? "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Торкнутися вогню Пером Варти") : "");
                case Watch: return t + Lang.T("Горить вогнем Варти.");
            }
            Player p = Player.m_localPlayer;
            bool wood = p != null && p.GetInventory().CountItems(WoodItem) > 0;
            if (!Opened) return t + Lang.T("Холодне. Попіл ще пам'ятає вогонь.") + "\n<color=#a0a0a0>" + Lang.T(Shut) + "</color>";
            return t + Lang.T("Холодне. Попіл ще пам'ятає вогонь.") + "\n"
                + (wood ? "[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Розпалити (дрова)") : "<color=#a0a0a0>" + Lang.T("Щоб розпалити, потрібні дрова.") + "</color>");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || Place == null) return false;
            // Hel's fire touched with the Feather of the Watch: the Watch's own (the feather stays with its bearer)
            if (Level == Hel && HasFeather(p)) { SetLevel(Watch); return true; }
            if (Level != Cold) return false;
            if (!Opened) { p.Message(MessageHud.MessageType.Center, Lang.T(Shut)); return false; }
            if (p.GetInventory().CountItems(WoodItem) <= 0) { p.Message(MessageHud.MessageType.Center, Lang.T("Щоб розпалити, потрібні дрова.")); return false; }
            p.GetInventory().RemoveItem(WoodItem, 1);
            SetLevel(Hel);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        // not lit while its building's great door is locked: Heid opens the Hall's with her key (the world key <place>_gate; the user
        // 2026-11-01) — in through a hole, the cold hearth still waits for her. A place with no great door: always open
        private string Shut { get { Loc l = Place; return l != null ? l.HearthShut : ""; } } // (the place's own words: «hearthshut:»)
        private bool Opened { get { Loc l = Place; return l == null || l.Gate.Length == 0 || LocState.GateOpenOf(l); } }

        // the Feather of the Watch in the bag (the story's: from its prologue; the author — `spawn RavenWatch_item_pero_varty`)
        private const string FeatherPrefab = "RavenWatch_item_pero_varty";
        private static string _featherName;
        private static bool HasFeather(Player p)
        {
            if (p == null) return false;
            if (_featherName == null)
            {
                GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(FeatherPrefab) : null;
                ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
                if (d == null) return false;
                _featherName = d.m_itemData.m_shared.m_name;
            }
            return p.GetInventory().CountItems(_featherName) > 0;
        }

        // ---------------------------------------------------------------- the fire's look
        private void Make(int n)
        {
            if (_fire != null) Destroy(_fire);
            _fire = null; _light = null;
            if (n == Cold) return;
            _fire = new GameObject("RavenWatch_HearthFire");
            _fire.transform.SetParent(transform, false);
            _fire.transform.localPosition = FireAt;
            if (n == Hel) MakeHel(_fire.transform); else MakeWatch(_fire.transform);
        }

        private void MakeHel(Transform at)
        {
            Material puff = Feathers.Smoke(), glow = FxLib.Glow(true), flame = FlameMat();
            // the flame: tongues of light (a soft spark stretched along its rise, adding up where they cross), a pale
            // cold heart going sea-green, their tips dying violet — fire, not mist (the user 2026-11-01)
            if (flame != null)
            {
                ParticleSystem f = Ps(at, "flame", flame, 1, 1);
                Main(f, new Vector2(0.55f, 0.95f), new Vector2(0.8f, 1.4f), new Vector2(0.28f, 0.48f), 55, 90, new Color(0.7f, 1f, 0.95f, 0.85f), new Color(0.35f, 0.95f, 0.8f, 0.75f));
                Cone(f, 0.34f, 5f);
                Grade(f, new Color(0.85f, 1f, 0.98f), new Color(0.3f, 0.95f, 0.75f), new Color(0.45f, 0.25f, 0.85f), 1f);
                Shrink(f, 1f, 0.15f);
                Tongues(f);
                // its glowing bed: low, wide, slow
                ParticleSystem b = Ps(at, "bed", flame, 1, 1);
                Main(b, new Vector2(0.9f, 1.4f), new Vector2(0.02f, 0.1f), new Vector2(0.7f, 1.1f), 10, 20, new Color(0.3f, 0.9f, 0.8f, 0.5f), new Color(0.25f, 0.7f, 0.75f, 0.4f));
                Cone(b, 0.3f, 2f);
                Grade(b, Color.white, Color.white, Color.white, 1f);
            }
            // the smoke over it: thin, deep violet, slow
            if (puff != null)
            {
                ParticleSystem s = Ps(at, "smoke", puff, 2, 2);
                s.transform.localPosition = Vector3.up * 1.1f;
                Main(s, new Vector2(4f, 6f), new Vector2(0.3f, 0.5f), new Vector2(0.6f, 1.1f), 3, 20, new Color(0.24f, 0.13f, 0.34f, 0.22f), new Color(0.16f, 0.1f, 0.26f, 0.18f));
                Cone(s, 0.25f, 10f);
                Grade(s, Color.white, Color.white, Color.white, 1f);
                Shrink(s, 0.6f, 1.6f);
            }
            // a few cold motes drifting up (no sparks: this fire does not crackle)
            if (glow != null)
            {
                ParticleSystem m = Ps(at, "motes", glow, 1, 1);
                Main(m, new Vector2(2f, 3.5f), new Vector2(0.15f, 0.35f), new Vector2(0.03f, 0.06f), 3, 20, new Color(0.55f, 1f, 0.9f, 0.9f), new Color(0.6f, 0.5f, 1f, 0.8f));
                Cone(m, 0.4f, 15f);
                Grade(m, Color.white, Color.white, Color.white, 1f);
            }
            _light = Lamp(at, new Color(0.38f, 0.92f, 0.82f), 1.1f, 7f);
        }

        private void MakeWatch(Transform at)
        {
            GameObject tpl = FxLib.Template("game:fire_pit");
            if (tpl != null)
            {
                GameObject body = Instantiate(tpl, at, false);
                body.hideFlags = HideFlags.None;
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = Vector3.one * 1.3f;
                body.SetActive(true);
                foreach (ParticleSystem ps in body.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name.ToLowerInvariant().Contains("smoke")) continue;
                    ParticleSystem.MainModule mm = ps.main;
                    mm.startColor = Tint(mm.startColor, new Color(1f, 0.86f, 0.55f)); // (a little more gold)
                }
                foreach (Light l in body.GetComponentsInChildren<Light>(true)) { l.color = new Color(1f, 0.72f, 0.38f); l.intensity *= 1.3f; l.range *= 1.2f; }
            }
            else _light = Lamp(at, new Color(1f, 0.72f, 0.38f), 1.6f, 9f);
            // now and then a black feather rising out of it
            Material feather = Feathers.Feather();
            if (feather != null)
            {
                ParticleSystem f = Ps(at, "feathers", feather, 2, 2);
                Main(f, new Vector2(2.5f, 4f), new Vector2(0.6f, 1.1f), new Vector2(0.2f, 0.3f), 0.35f, 6, new Color(0.08f, 0.08f, 0.1f, 1f), new Color(0.14f, 0.12f, 0.12f, 1f));
                Cone(f, 0.3f, 20f);
                Grade(f, Color.white, Color.white, Color.white, 1f);
                ParticleSystem.RotationOverLifetimeModule r = f.rotationOverLifetime;
                r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            }
            // it warms (the game's own heat round a fire: warm, dry, rest)
            GameObject warm = new GameObject("warmth");
            warm.layer = LayerMask.NameToLayer("character_trigger");
            warm.transform.SetParent(at, false);
            SphereCollider c = warm.AddComponent<SphereCollider>();
            c.isTrigger = true; c.radius = 3.5f;
            EffectArea area = warm.AddComponent<EffectArea>();
            area.m_type = EffectArea.Type.Heat | EffectArea.Type.Fire;
        }

        // the first moment it catches, seen: a burst, and a raven's word
        private void Lit(int n)
        {
            if (_fire == null) return;
            if (n == Hel)
            {
                Material puff = Feathers.Smoke();
                if (puff != null)
                {
                    ParticleSystem b = Ps(_fire.transform, "burst", puff, 2, 2);
                    ParticleSystem.EmissionModule em = b.emission;
                    em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
                    Main(b, new Vector2(2.5f, 3.5f), new Vector2(1.5f, 3f), new Vector2(1.2f, 2.2f), 0f, 60, new Color(0.3f, 0.75f, 0.7f, 0.45f), new Color(0.28f, 0.16f, 0.4f, 0.4f), false);
                    ParticleSystem.ShapeModule sh = b.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f;
                    Grade(b, Color.white, Color.white, Color.white, 1f);
                    Shrink(b, 0.5f, 1.8f);
                }
                // the wood catching: a spurt of cold sparks up, a hiss of fire and a cold breath
                Material flame = FlameMat();
                if (flame != null)
                {
                    ParticleSystem sp = Ps(_fire.transform, "sparks", flame, 1, 1);
                    ParticleSystem.EmissionModule se = sp.emission;
                    se.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
                    Main(sp, new Vector2(0.6f, 1.2f), new Vector2(2.5f, 5f), new Vector2(0.06f, 0.14f), 0f, 50, new Color(0.75f, 1f, 0.95f, 1f), new Color(0.4f, 0.9f, 0.85f, 1f), false);
                    Cone(sp, 0.25f, 25f);
                    Grade(sp, Color.white, new Color(0.5f, 1f, 0.9f), new Color(0.5f, 0.3f, 0.9f), 1f);
                    ParticleSystem.MainModule spm = sp.main; spm.gravityModifier = 0.35f;
                }
                Vector3 at = _fire.transform.position;
                RavenFx.Play("sfx_FireAddFuel", at, Quaternion.identity);
                RavenFx.Play("sfx_frozenking_charge_whoosh", at, Quaternion.identity);
                RavenSkills.GiftSay(true, "hearth_hel", Lang.T("Не наш це вогонь… Холодний, як її руки."));
            }
            else
            {
                // the feather touches it: the rite's chime, the fire taking, a raven's cry — the Watch's fire is back
                Vector3 at = _fire.transform.position;
                RavenFx.Play("sfx_bossstone_attach_done", at, Quaternion.identity);
                RavenFx.Play("sfx_FireAddFuel", at, Quaternion.identity);
                RavenFx.Play("vfx_FireAddFuel", at, Quaternion.identity);
                RavenFx.Play("sfx_raven_kaw", at + Vector3.up * 2f, Quaternion.identity);
                RavenSkills.GiftSay(false, "hearth_watch", Lang.T("Ось він. Вогонь Варти. Довго ж ми чекали."));
            }
        }

        // the flame's own picture: our soft spark on an adding shader (light on light, as fire), the first the game has;
        // none of them — the plain one (it still reads as a flame, only softer)
        private static Material _flame;
        private static bool _flameLooked;
        private static Material FlameMat()
        {
            if (_flameLooked) return _flame;
            _flameLooked = true;
            Material dot = FxLib.Glow(true);
            foreach (string name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive", "Legacy Shaders/Particles/Additive (Soft)" })
            {
                Shader sh = Shader.Find(name);
                if (sh == null) continue;
                _flame = new Material(sh);
                _flame.hideFlags = HideFlags.HideAndDontSave;
                if (dot != null) _flame.mainTexture = dot.mainTexture;
                if (_flame.HasProperty("_TintColor")) _flame.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                if (Plugin.Instance != null) Plugin.Instance.Log("Hearth: flame shader " + name);
                return _flame;
            }
            if (Plugin.Instance != null) Plugin.Instance.Log("Hearth: no adding shader in this game, the plain glow for the flame");
            return _flame = dot;
        }

        // stretched along its rise: a tongue, not a ball
        private static void Tongues(ParticleSystem ps)
        {
            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.18f;
            r.lengthScale = 1.6f;
            ParticleSystem.NoiseModule n = ps.noise; // (a little flicker sideways)
            n.enabled = true; n.strength = 0.25f; n.frequency = 1.6f; n.scrollSpeed = 0.8f;
        }

        // ---------------------------------------------------------------- particle helpers
        private static ParticleSystem Ps(Transform parent, string name, Material mat, int tilesX, int tilesY)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // (the cone points up)
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (tilesX * tilesY > 1)
            {
                ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
                sheet.enabled = true; sheet.numTilesX = tilesX; sheet.numTilesY = tilesY;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, tilesX * tilesY - 0.01f);
            }
            return ps;
        }

        // (once: a single burst, gone when done)
        private static void Main(ParticleSystem ps, Vector2 life, Vector2 speed, Vector2 size, float rate, int max, Color a, Color b, bool loop = true)
        {
            ParticleSystem.MainModule m = ps.main;
            m.loop = loop; m.prewarm = loop; m.playOnAwake = true;
            if (!loop) { m.duration = 1f; m.stopAction = ParticleSystemStopAction.Destroy; }
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(a, b);
            m.maxParticles = max;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;
            ps.Play();
        }

        private static void Cone(ParticleSystem ps, float radius, float angle)
        {
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.radius = radius; sh.angle = angle;
        }

        // colour over its life (from, middle, to) and its fade in and out
        private static void Grade(ParticleSystem ps, Color from, Color mid, Color to, float peak)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(mid, 0.45f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.2f), new GradientAlphaKey(peak * 0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Shrink(ParticleSystem ps, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
        }

        private static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color t)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(g.color * t);
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(g.colorMin * t, g.colorMax * t);
            }
            return g;
        }

        private Light Lamp(Transform at, Color c, float intensity, float range)
        {
            GameObject g = new GameObject("light");
            g.transform.SetParent(at, false);
            g.transform.localPosition = Vector3.up * 0.5f;
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            _base = intensity;
            return l;
        }
    }
}
#endif

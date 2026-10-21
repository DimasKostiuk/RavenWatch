#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's light: a Mistlands wisp floats over your head and lights the place up like a torch, only wider.
    // The wearer writes into the player's own network data that the light is on; every game reads it for
    // every player nearby and draws the wisp itself, the same way as the shoulder ravens.
    internal static class RavenLight
    {
        internal const string EffectName = "RavenWatch_Light";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        private const string Key = "RavenWatch_Light";
        private const string WispPrefab = "demister_ball";

        internal static float Seconds = 300f;
        internal static float RangeMultiplier = 1.25f; // against a torch
        internal static float Height = 2.4f;           // meters over the feet
        internal static Color WispColor = new Color(1f, 0.93f, 0.7f); // the Wisplight ball is blue; Hugin's is yellow-white
        internal static float WispOpacity = 0.7f; // 30% see-through
        private static float _alpha = 1f;

        private static readonly Dictionary<Player, WispLife> Drawn = new Dictionary<Player, WispLife>();
        private static float _next;
        private static bool _missingLogged;
        private static SE_Stats _template;

        // a little longer with trust (+5% a level)
        internal static float Duration() { return Mathf.Max(1f, Seconds * Trust.Mul(false, 0.05f) * GiftUp.Power("light")); }

        internal static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Світло Хугіна");
            se.m_tooltip = Lang.F("Світляк над головою світить на {0}% ширше за смолоскип", Mathf.RoundToInt(RangeMultiplier * 100f - 100f));
            se.m_icon = AbilityIcons.ForEffect("light", false);
            se.m_ttl = Duration();
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        internal static bool On(Player p) { return p != null && p.GetSEMan().HaveStatusEffect(Hash); }

        // the gift pressed again while it shines: the wisp goes out (the cooldown still runs from the start)
        internal static void Stop(Player p)
        {
            if (p == null) return;
            p.GetSEMan().RemoveStatusEffect(Hash, true);
            Publish(p);
            p.Message(MessageHud.MessageType.TopLeft, Lang.T("Світляк Хугіна згас"));
        }

        internal static void Reset()
        {
            foreach (WispLife w in Drawn.Values) if (w != null) Object.Destroy(w.gameObject);
            Drawn.Clear();
        }

        // the local player tells everyone whether the wisp is on
        internal static void Publish(Player p)
        {
            NetFlag.Set(p, Key, On(p));
        }

        // twice a second (your own every frame, so it answers the press at once): who has a wisp. One that is no
        // longer wanted is not destroyed but sent home to Hugin (WispLife fades it there and goes)
        private static readonly List<Player> Gone = new List<Player>();

        internal static void Tick()
        {
            Player me = Player.m_localPlayer;
            if (me != null) Check(me, !me.IsDead() && On(me));
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;

            // the ones whose player or wisp is gone (a player who left takes the wisp along)
            Gone.Clear();
            foreach (KeyValuePair<Player, WispLife> kv in Drawn) if (kv.Key == null || kv.Value == null) Gone.Add(kv.Key);
            foreach (Player pl in Gone) { WispLife w; if (Drawn.TryGetValue(pl, out w) && w != null) Object.Destroy(w.gameObject); Drawn.Remove(pl); }
            // everyone else nearby, from what their game writes (Check may add to the list: not while going through it)
            foreach (Player pl in Player.GetAllPlayers())
                if (pl != null && pl != me) Check(pl, Wants(pl));
        }

        private static void Check(Player pl, bool wants)
        {
            WispLife w;
            Drawn.TryGetValue(pl, out w);
            if (w != null && !w.Leaving)
            {
                if (!wants) w.Leave();
                return;
            }
            if (!wants) return;
            // none, or one still on its way home: a new one comes out (the old one finishes going)
            Drawn[pl] = Make(pl);
        }

        private static bool Wants(Player pl)
        {
            return !pl.IsDead() && NetFlag.Get(pl, Key);
        }

        // Built switched off, so nothing in the copies wakes up before it is stripped down to the looks
        private static WispLife Make(Player pl)
        {
            GameObject root = new GameObject(EffectName);
            root.SetActive(false);
            root.transform.SetParent(pl.transform, false);
            root.transform.localPosition = new Vector3(0f, Height, 0f);

            Light torch = TorchLight();
            if (torch != null)
            {
                GameObject lg = Object.Instantiate(torch.gameObject, root.transform, false);
                lg.transform.localPosition = Vector3.zero;
                Strip(lg);
                foreach (Light l in lg.GetComponentsInChildren<Light>(true)) l.range *= RangeMultiplier;
            }
            else
            {
                Light l = new GameObject("light").AddComponent<Light>();
                l.transform.SetParent(root.transform, false);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.7f, 0.4f);
                l.intensity = 1.4f;
                l.range = 10f * RangeMultiplier;
                l.shadows = LightShadows.Soft;
            }

            GameObject wisp = Wisp();
            if (wisp != null)
            {
                GameObject w = Object.Instantiate(wisp, root.transform, false);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
                Strip(w);
                NoSparks(w);
                Tint(w, WispColor, WispOpacity);
            }
            else if (!_missingLogged && Plugin.Instance != null)
            {
                _missingLogged = true;
                Plugin.Instance.Log("Hugin's light: no Wisplight ball (" + WispPrefab + "), the light goes without a wisp.");
            }

            Sounds(root);
            Runes(root.transform);
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true)) { ParticleSystem.MainModule mm = ps.main; mm.scalingMode = ParticleSystemScalingMode.Hierarchy; } // they grow and shrink with it
            WispLife life = root.AddComponent<WispLife>();
            life.Init(pl);
            root.SetActive(true);
            return life;
        }

        internal static float RunesPerSecond = 1.2f;
        internal static bool GameSounds = true; // the torch's and the Wisplight ball's own sounds that come along with the copies
        internal static float SoundVolume = 0.7f; // those sounds, against the Wisplight's own loudness
        private static bool _soundsLogged;

        // The copies keep their sound players (only the scripts are stripped): told once in the log, and silenced
        // when "Hugin light game sounds" is off
        private static void Sounds(GameObject root)
        {
            AudioSource[] all = root.GetComponentsInChildren<AudioSource>(true);
            if (!_soundsLogged && Plugin.Instance != null)
            {
                _soundsLogged = true;
                System.Text.StringBuilder log = new System.Text.StringBuilder("Hugin's light, sounds in the wisp: " + all.Length);
                foreach (AudioSource a in all)
                {
                    string path = a.gameObject.name;
                    for (Transform t = a.transform.parent; t != null && t != root.transform; t = t.parent) path = t.name + "/" + path;
                    log.Append("\n  ").Append(path).Append(" clip ").Append(a.clip != null ? a.clip.name : "-").Append(" loop ").Append(a.loop)
                        .Append(" playOnAwake ").Append(a.playOnAwake).Append(" volume ").Append(a.volume.ToString("0.##")).Append(GameSounds ? " -> kept" : " -> off");
                }
                Plugin.Instance.Log(log.ToString());
            }
            if (GameSounds) return;
            foreach (AudioSource a in all) { a.playOnAwake = false; a.Stop(); a.enabled = false; }
        }
        private static bool _sparksLogged;

        // The ball's sparks fall like embers; Hugin's light has runes instead. A spark is a particle system that moves
        // (speed or gravity); the glow of the ball itself stands still and stays. Told once in the log.
        private static void NoSparks(GameObject w)
        {
            System.Text.StringBuilder log = _sparksLogged ? null : new System.Text.StringBuilder("Hugin's light, the wisp's particles:");
            foreach (ParticleSystem ps in w.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                float speed = Mathf.Max(Mathf.Abs(main.startSpeed.constantMax), Mathf.Abs(main.startSpeed.constant));
                float gravity = Mathf.Max(Mathf.Abs(main.gravityModifier.constantMax), Mathf.Abs(main.gravityModifier.constant));
                string n = ps.gameObject.name.ToLowerInvariant();
                bool spark = n.Contains("spark") || n.Contains("sparc") || speed > 0.05f || gravity > 0.001f;
                if (log != null) log.Append("\n  ").Append(ps.gameObject.name).Append(" speed ").Append(speed.ToString("0.##")).Append(" gravity ").Append(gravity.ToString("0.###"))
                    .Append(" max ").Append(main.maxParticles).Append(spark ? " -> off" : " -> kept");
                if (!spark) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.EmissionModule em = ps.emission;
                em.enabled = false;
                Renderer r = ps.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
            if (log != null && Plugin.Instance != null) { _sparksLogged = true; Plugin.Instance.Log(log.ToString()); }
        }

        // the dome's runes, a few at a time: they come out of the wisp softly, drift off every way and fade
        private static void Runes(Transform root)
        {
            Material m = DomeLook.RuneMaterialPublic();
            if (m == null || RunesPerSecond <= 0f) return;
            GameObject go = new GameObject("runes");
            go.transform.SetParent(root, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // they go along with you
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.24f, 0.5f); // with the drag: about 0.6 m off the wisp
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(WispColor, Color.Lerp(WispColor, Color.white, 0.4f));
            main.maxParticles = 10;
            main.gravityModifier = 0f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = RunesPerSecond;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; // out from the centre, every way
            sh.radius = 0.12f;
            sh.radiusThickness = 1f;
            ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.limit = new ParticleSystem.MinMaxCurve(100f); // no cap: only the drag
            lim.drag = new ParticleSystem.MinMaxCurve(0.5f); // slowing down as they go
            lim.multiplyDragByParticleSize = false;
            lim.multiplyDragByParticleVelocity = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.3f), new GradientAlphaKey(0.4f, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.1f)));
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            sheet.cycleCount = 1;
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }

        // only the looks stay: no network object, no physics, and the wisp does not clear the Mistlands fog
        private static void Strip(GameObject g)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (MonoBehaviour mb in g.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb is LightFlicker || mb is LightLod) continue;
                    Object.DestroyImmediate(mb);
                }
            }
            foreach (Component c in g.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (c is Collider || c.GetType().Name == "ParticleSystemForceField") Object.DestroyImmediate(c);
            }
            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        }

        // Repaint the wisp: every color keeps its own brightness and transparency, only the hue and saturation change,
        // so the glow and the fading of the particles stay as they were
        internal static void Tint(GameObject g, Color to, float opacity)
        {
            float th, ts, tv;
            Color.RGBToHSV(to, out th, out ts, out tv);
            _alpha = Mathf.Clamp01(opacity);
            foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.color = Recolor(l.color, th, ts);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in RavenFx.OwnMaterials(r))
                {
                    if (m == null) continue;
                    foreach (string prop in new string[] { "_Color", "_TintColor", "_EmissionColor", "_BaseColor" })
                        if (m.HasProperty(prop)) m.SetColor(prop, Recolor(m.GetColor(prop), th, ts));
                }
            foreach (ParticleSystem ps in g.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.startColor = Recolor(main.startColor, th, ts);
                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                if (col.enabled) col.color = Recolor(col.color, th, ts);
                ParticleSystem.ColorBySpeedModule spd = ps.colorBySpeed;
                if (spd.enabled) spd.color = Recolor(spd.color, th, ts);
                ParticleSystem.TrailModule trail = ps.trails;
                if (trail.enabled) trail.colorOverLifetime = Recolor(trail.colorOverLifetime, th, ts);
            }
        }

        private static Color Recolor(Color c, float h, float s)
        {
            float ch, cs, cv;
            Color.RGBToHSV(c, out ch, out cs, out cv);
            Color r = Color.HSVToRGB(h, s, cv, true);
            r.a = c.a * _alpha; // a light ignores it
            return r;
        }

        private static Gradient Recolor(Gradient g, float h, float s)
        {
            if (g == null) return null;
            GradientColorKey[] keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Recolor(keys[i].color, h, s);
            Gradient n = new Gradient();
            n.mode = g.mode;
            GradientAlphaKey[] alpha = g.alphaKeys;
            for (int i = 0; i < alpha.Length; i++) alpha[i].alpha *= _alpha;
            n.SetKeys(keys, alpha);
            return n;
        }

        private static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient m, float h, float s)
        {
            switch (m.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Recolor(m.color, h, s));
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Recolor(m.colorMin, h, s), Recolor(m.colorMax, h, s));
                case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(Recolor(m.gradient, h, s));
                case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Recolor(m.gradientMin, h, s), Recolor(m.gradientMax, h, s));
                default: { ParticleSystem.MinMaxGradient r = new ParticleSystem.MinMaxGradient(Recolor(m.gradient, h, s)); r.mode = ParticleSystemGradientMode.RandomColor; return r; }
            }
        }

        // the ball the Wisplight sends over your head; the network prefab of that name as a fallback
        internal static GameObject Wisp()
        {
            if (ObjectDB.instance != null)
                foreach (StatusEffect se in ObjectDB.instance.m_StatusEffects)
                {
                    SE_Demister d = se as SE_Demister;
                    if (d != null && d.m_ballPrefab != null) return d.m_ballPrefab;
                }
            return ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(WispPrefab) : null;
        }

        // the brightest light in the torch, the model for ours
        private static Light TorchLight()
        {
            GameObject torch = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Torch") : null;
            if (torch == null) return null;
            Light best = null;
            foreach (Light l in torch.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Point && (best == null || l.range > best.range)) best = l;
            return best;
        }
    }

    // The wisp's life: it comes out of Hugin small and dim, grows and brightens on the way up to its place over your
    // head (a slight arc), bobs there, and when it is put out flies back into Hugin, shrinking and dimming till it is
    // gone. Hugin not drawn (too far, flying off): it grows and fades on the spot over your head.
    internal class WispLife : MonoBehaviour
    {
        internal static float InSeconds = 1.6f, OutSeconds = 1.3f;
        internal static float FollowLag = 0.6f; // s: how slowly it catches up with its spot over your head (0: fixed to it)
        private Vector3 _vel;      // its own speed while it follows
        private Vector3 _at;       // where it is in the world: its own, not dragged along by the player it hangs under
        private Player _pl;
        private Light[] _lights;
        private float[] _intensity;
        private ParticleSystem _runes;
        private float _start, _leaveStart = -1f;
        private Vector3 _leaveFrom;
        private bool _whole;
        private AudioSource _on;      // the switch-on sound: played backwards when it goes out
        private AudioSource[] _hum;   // the hum while it shines
        private float[] _humVolume;
        private float _k, _leaveK = 1f; // how whole it is now; how whole it was when it started home

        internal bool Leaving { get { return _leaveStart >= 0f; } }

        internal void Init(Player pl)
        {
            _pl = pl;
            _lights = GetComponentsInChildren<Light>(true);
            _intensity = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _intensity[i] = _lights[i].intensity;
            Transform r = transform.Find("runes");
            _runes = r != null ? r.GetComponent<ParticleSystem>() : null;
            // the ball's sounds, quieter: the switch-on plays once as it comes; the hum swells and fades with the wisp
            List<AudioSource> loops = new List<AudioSource>();
            foreach (AudioSource a in GetComponentsInChildren<AudioSource>(true))
            {
                if (!a.enabled) continue; // silenced in the settings
                a.volume *= RavenLight.SoundVolume;
                if (a.loop) loops.Add(a);
                else if (_on == null && a.clip != null) { _on = a; SoundBack.Prepare(a.clip); } // its backwards copy, ready long before it goes out
            }
            _hum = loops.ToArray();
            _humVolume = new float[_hum.Length];
            for (int i = 0; i < _hum.Length; i++) _humVolume[i] = _hum[i].volume;
            _start = Time.time;
            // small and silent from the start, but the lights left alone until it is switched on: the torch's
            // LightFlicker reads its base brightness as it wakes, and a 0 set now would keep the light dark for good
            _k = 0f;
            transform.localScale = Vector3.one * 0.03f;
            for (int i = 0; i < _hum.Length; i++) if (_hum[i] != null) _hum[i].volume = 0f;
            transform.localPosition = Home();
        }

        internal void Leave()
        {
            if (Leaving) return;
            _leaveStart = Time.time;
            _leaveFrom = transform.localPosition;
            _leaveK = _k; // put out while still coming: it goes home from the size it had
            OffSound();
            if (_runes != null) { ParticleSystem.EmissionModule em = _runes.emission; em.enabled = false; } // the runes in the air finish their way
        }

        // over the head, bobbing (in the player's own frame)
        private Vector3 Spot()
        {
            float t = Time.time;
            return new Vector3(Mathf.Sin(t * 0.7f) * 0.25f, RavenLight.Height + Mathf.Sin(t * 1.9f) * 0.12f, Mathf.Cos(t * 0.6f) * 0.2f);
        }

        // Hugin's head (on your shoulder, or on a perch near enough to fly to and from), or the spot itself when he is
        // not there to come from
        internal static float PerchReach = 40f;
        private Vector3 Home()
        {
            if (_pl == null) return Spot();
            GameObject hugin = Shoulder.ModelOf(_pl, false);
            if (hugin == null && Time.time >= _perchLook) { _perchLook = Time.time + 0.5f; _perched = Perches.SittingBird(_pl, false, PerchReach); }
            if (hugin == null) hugin = _perched;
            if (hugin == null) return Spot();
            return _pl.transform.InverseTransformPoint(hugin.transform.position + Vector3.up * 0.2f);
        }
        private float _perchLook;
        private GameObject _perched; // Hugin on a perch (looked up twice a second, not every frame)

        // 0: nothing, 1: the whole wisp
        private void Apply(float k)
        {
            _k = k;
            transform.localScale = Vector3.one * Mathf.Lerp(0.03f, 1f, k);
            for (int i = 0; i < _lights.Length; i++) if (_lights[i] != null) _lights[i].intensity = _intensity[i] * k;
            if (_hum != null) for (int i = 0; i < _hum.Length; i++) if (_hum[i] != null) _hum[i].volume = _humVolume[i] * k;
        }

        // The switch-on, backwards: its own player beside the wisp (the wisp is gone before the sound ends), with the
        // same mixer and distance settings, so it sits in the game's sound volume like the original
        // Backwards the switch-on ends on its click: timed so the click falls as the wisp is gone (OutSeconds home).
        // Not caught yet (put out within a second or two of the very first light): the switch-on as it is.
        private void OffSound()
        {
            if (_on == null || _on.clip == null || _pl == null) return;
            AudioClip back = SoundBack.Get(_on.clip);
            if (back != null) SoundBack.PlayEndingAt(back, _on, transform.position, _pl.transform, OutSeconds + 0.15f);
            else SoundBack.PlayEndingAt(_on.clip, _on, transform.position, _pl.transform, _on.clip.length);
        }

        // It has a little weight: it flies after its spot over your head (a spring that does not overshoot), so a
        // sudden step leaves it hanging a moment before it catches up. Too far behind (a portal, a teleport): it is there.
        private void Follow()
        {
            Vector3 want = _pl.transform.TransformPoint(Spot());
            // the player carries it along as its child before this runs: so it keeps its own place (_at) and is put back there
            if (FollowLag <= 0.01f || (_at - want).sqrMagnitude > 15f * 15f) { _at = want; _vel = Vector3.zero; }
            else _at = Vector3.SmoothDamp(_at, want, ref _vel, FollowLag, 40f, Time.deltaTime);
            transform.position = _at;
        }

        private static float Ease(float u) { return u * u * (3f - 2f * u); }

        private void LateUpdate()
        {
            if (_pl == null) { Destroy(gameObject); return; }
            if (Leaving)
            {
                float u = Mathf.Clamp01((Time.time - _leaveStart) / Mathf.Max(0.1f, OutSeconds));
                float e = Ease(u);
                transform.localPosition = Vector3.Lerp(_leaveFrom, Home(), e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.35f);
                Apply(_leaveK * (1f - e));
                if (u >= 1f) Destroy(gameObject);
                return;
            }
            float v = Mathf.Clamp01((Time.time - _start) / Mathf.Max(0.1f, InSeconds));
            if (v >= 1f)
            {
                if (!_whole) { _whole = true; Apply(1f); _vel = Vector3.zero; _at = transform.position; } // whole now: from here the torch light flickers on its own
                Follow();
                return;
            }
            float k = Ease(v);
            transform.localPosition = Vector3.Lerp(Home(), Spot(), k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.35f);
            Apply(k);
        }
    }

    // Munin's wings: you run faster for a while
    internal static class Wings
    {
        internal const string EffectName = "RavenWatch_Wings";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        internal static float Seconds = 20f, SecondsTop = 40f; // at trust level 1 and at the top level
        internal static float Duration() { return Mathf.Floor(Trust.Lerp(true, Seconds, SecondsTop) * GiftUp.Power("wings") + 0.5f); }
        internal static float SpeedPercent = 50f;
        internal static float SpeedNow() { return Mathf.Max(0f, SpeedPercent) * Trust.Mul(true, 0.05f); } // as fast as it carries you now (trust)
        internal static float RunStaminaPercent = 30f; // the wings carry you: running costs less
        private static SE_Stats _template;

        internal static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Попутний вітер");
            se.m_tooltip = Lang.F("+{0}% до швидкості руху, біг коштує на {1}% менше витривалості", Mathf.RoundToInt(SpeedNow()), Mathf.RoundToInt(RunStaminaPercent));
            se.m_icon = AbilityIcons.ForEffect("wings", true);
            se.m_ttl = Mathf.Max(1f, Duration());
            se.m_speedModifier = SpeedNow() / 100f;
            se.m_runStaminaDrainModifier = -Mathf.Clamp(RunStaminaPercent, 0f, 100f) / 100f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }
    }
}
#endif

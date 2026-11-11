#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What the paired gifts look and sound like (the user, 2026-10-24), on every game near: each reads the caster's
    // flags (its ZDO) and makes its own copy; nothing here touches the game.
    //  «Подвійна ноша»   — two thin ropes from the ravens' feet to your shoulders, as if they lifted you, sagging and
    //                      lagging a little; their flight is heavy (FollowPlan.Heavy: lower, slower, hard beats, a sag and a
    //                      jerk now and then — Dip); heavy wing beats, the rope creaking, a strained cry at the jerk, a feather lost.
    //  «Розігнати хмари» — a warm light from above, the edge of the circle in pale haze and our blue runes on a ring
    //                      that follows the caster with inertia, each raven a light trail of its own colour; a gust and
    //                      a far thunder at the start, the wind easing off at the end.
    //  «Вихор»           — a ring of feathers whirling round the caster, lagging after them on a spring, Hugin's blue on
    //                      one side and Munin's red on the other, dust swept round on the ground, the wind howling;
    //                      gathering at the start, dying down at the end; a whistle on every dive (StrikeFlight).
    //                      (No raven trails here: their runes colour them.)
    internal static class PairFx
    {
        internal const string LoadKey = "RavenWatch_LoadOn", WhirlKey = "RavenWatch_WhirlOn";
        internal static float Near = 100f;
        internal static readonly Color HuginGlow = new Color(0.55f, 0.78f, 1f, 1f), MuninGlow = new Color(1f, 0.45f, 0.35f, 1f);

        private static readonly Dictionary<Player, LoadLook> Loads = new Dictionary<Player, LoadLook>();
        private static readonly Dictionary<Player, SkyLook> Skies = new Dictionary<Player, SkyLook>();
        private static readonly Dictionary<Player, WhirlLook> Whirls = new Dictionary<Player, WhirlLook>();
        private static float _lookAt;

        internal static bool Loaded(Player pl) { return pl != null && NetFlag.Get(pl, LoadKey); }

        internal static void Reset()
        {
            foreach (LoadLook l in Loads.Values) if (l != null) Object.Destroy(l.gameObject);
            foreach (SkyLook s in Skies.Values) if (s != null) Object.Destroy(s.gameObject);
            foreach (WhirlLook w in Whirls.Values) if (w != null) Object.Destroy(w.gameObject);
            Loads.Clear(); Skies.Clear(); Whirls.Clear();
        }

        // twice a second: who near has which of them on
        internal static void Tick()
        {
            if (Time.time < _lookAt) return;
            _lookAt = Time.time + 0.5f;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null) continue;
                bool near = !pl.IsDead() && Vector3.Distance(me.transform.position, pl.transform.position) <= Near, whirl = NetFlag.Get(pl, WhirlKey);
                Keep(Loads, pl, near, Loaded(pl));
                Keep(Skies, pl, near, ClearSky.Room(pl) > 0f);
                Keep(Whirls, pl, near, whirl);
                Whirl.Watch(pl, near && whirl); // (another's whirl: our copies of their birds)
            }
            Prune(Loads); Prune(Skies); Prune(Whirls);
        }

        // the gift ended: the look ends its own way (the wind dies down, the feathers fly out); only gone out of sight: just gone
        private static void Keep<T>(Dictionary<Player, T> on, Player pl, bool near, bool flag) where T : PairLook
        {
            T had;
            on.TryGetValue(pl, out had);
            if (near && flag && had == null) { T made = new GameObject("RavenWatch_" + typeof(T).Name).AddComponent<T>(); made.Init(pl); on[pl] = made; }
            else if (!(near && flag) && had != null) { if (flag) Object.Destroy(had.gameObject); else had.Finish(); on.Remove(pl); }
        }

        private static void Prune<T>(Dictionary<Player, T> on) where T : PairLook
        {
            List<Player> gone = null;
            foreach (KeyValuePair<Player, T> kv in on) if (kv.Key == null || kv.Value == null) (gone ?? (gone = new List<Player>())).Add(kv.Key);
            if (gone == null) return;
            foreach (Player g in gone) { T l; if (on.TryGetValue(g, out l) && l != null) l.Finish(); on.Remove(g); }
        }

        // the load's sag, every few seconds for each raven: down under it, a jerk back up a little past, settled (m)
        internal static float DipEvery = 7f, DipDepth = 0.55f;
        internal static float Dip(Player pl, bool munin) { return DipAt(Phase(pl, munin)); }
        internal static float Phase(Player pl, bool munin)
        {
            float off = (munin ? 3.3f : 0f) + (pl != null ? (pl.GetPlayerID() % 97) * 0.13f : 0f);
            return (Time.time + off) % DipEvery;
        }
        private static float DipAt(float ph)
        {
            if (ph < 0.9f) return -DipDepth * Mathf.Sin(ph / 0.9f * Mathf.PI * 0.5f);
            if (ph < 1.2f) return Mathf.Lerp(-DipDepth, 0.15f, (ph - 0.9f) / 0.3f);
            if (ph < 1.7f) return Mathf.Lerp(0.15f, 0f, (ph - 1.2f) / 0.5f);
            return 0f;
        }

        // ---------------------------------------------------------------- sounds
        // A sound of the catalogue (assets/story/sounds/gifts.txt: which game sounds, how loud), played here through the audio core,
        // as part of the world: slower by stretch, at this share of its loudness, heard at least 30 m.
        internal static void Sound(string name, Vector3 at, float stretch, float volume)
        {
            Audio.Sound(name, at, new SoundAsk { World = true, Stretch = stretch, Volume = volume, Reach = 30f, Owner = Audio.Gift });
        }
        internal const string HeavyFlap = "pair_flap", Creak = "pair_creak", Cry = "pair_cry", Gust = "pair_gust", Thunder = "pair_thunder", Whoosh = "pair_whoosh";

        internal static void Whistle(Vector3 at) { Sound(Whoosh, at, 1.15f, 0.55f); }

        // ---------------------------------------------------------------- particles
        // a particle system of ours on a sheet (feathers or smoke), built off, the caller sets it up and plays it
        internal static ParticleSystem Particles(Transform parent, string name, Material m, bool world)
        {
            if (m == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            if (m == Feathers.Feather())
            {
                ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = 2; sheet.numTilesY = 2;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            }
            return ps;
        }

        // fade in, hold, fade out over a particle's life, in this colour
        internal static void Fade(ParticleSystem ps, Color c, float alpha)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.2f), new GradientAlphaKey(alpha, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        // stop making new ones; the rest live out and the holder goes
        internal static void Ease(GameObject holder, float life)
        {
            if (holder == null) return;
            foreach (ParticleSystem ps in holder.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(holder, life);
        }

        // the raven models of this player now flying (not sitting on the shoulder): null when not out
        internal static Transform Flying(Player pl, bool munin)
        {
            GameObject m = Shoulder.ModelOf(pl, munin);
            if (m == null || !m.activeInHierarchy) return null;
            ShoulderPin pin = m.GetComponent<ShoulderPin>();
            if (pin != null && pin.enabled && Orders.Of(pl, munin) == Orders.None) return null;
            return Vector3.Distance(m.transform.position, pl.transform.position + Vector3.up * 1.5f) > 0.6f ? m.transform : null;
        }
    }

    // a look that follows one player while their gift lasts; Finish lets it end its own way
    internal abstract class PairLook : MonoBehaviour
    {
        protected Player Pl;
        internal virtual void Init(Player pl) { Pl = pl; transform.position = pl.transform.position; Shadow.Outside(gameObject, pl); } // (hidden with you in Hugin's shadow; its own rings too)
        internal virtual void Finish() { Destroy(gameObject); }
    }

    // ---------------------------------------------------------------- «Подвійна ноша»
    internal sealed class LoadLook : PairLook
    {
        // two thin ropes from the ravens' claws (between its two feet, Leg3: knotted round them, a loose end hanging) to your shoulders (Hugin's to the right, Munin's to the left), as if they
        // were lifting you: each sags a little and lags behind as you move (its middle a weight on a spring, Rope)
        internal static float RopeWidth = 0.014f, Sag = 0.06f, Spring = 26f, Damp = 5.5f, ShoulderBack = 0.12f;
        private const int Points = 12;
        private readonly Rope[] _ropes = new Rope[2];
        private float _flapAt, _creakAt, _featherAt;
        private readonly float[] _lastPhase = { 0f, 0f };
        private readonly Transform[] _bones = new Transform[2]; // (looked up once: Shoulder.Bone searches the whole body)
        private readonly GameObject[] _birdOf = new GameObject[2];
        private readonly Transform[] _footL = new Transform[2], _footR = new Transform[2]; // (each bird's feet, looked up once per bird)
        private static Material _ropeMat;
        private static bool _matLooked, _tile;

        private sealed class Rope
        {
            internal LineRenderer Line, Knot;   // the rope; its knot at the claws with a loose end
            internal Vector3 Mid, Vel, Tail, TailVel;
            internal bool Placed;
        }

        // the knot: the rope wound round the bird's legs (KnotTurns turns of KnotR, a little along the rope) and a short
        // loose end hanging from it, swinging on its own spring
        internal static float KnotR = 0.035f, KnotTurns = 1.75f, KnotWind = 0.03f, TailLen = 0.12f, TailSpring = 30f, TailDamp = 4f;
        private const int KnotPoints = 16, TailPoints = 5;

        internal override void Init(Player pl)
        {
            base.Init(pl);
            for (int i = 0; i < 2; i++)
            {
                _ropes[i] = new Rope { Line = Line(i == 1 ? "rope M" : "rope H", Points), Knot = Line(i == 1 ? "knot M" : "knot H", KnotPoints + TailPoints) };
                _ropes[i].Knot.widthMultiplier = 1.15f; // (a knot is a little thicker than the rope)
            }
            Show(false);
            _flapAt = Time.time + 0.5f; _creakAt = Time.time + Random.Range(2f, 4f); _featherAt = Time.time + Random.Range(4f, 8f);
        }

        // the game's own rope if it has one (the ships' rigging), else the grain of a wooden beam; tiled along the rope
        // when the picture repeats, stretched when it does not
        private static Material RopeMaterial()
        {
            if (_matLooked) return _ropeMat;
            _matLooked = true;
            Texture tex = null;
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
                if (m != null && m.name.ToLowerInvariant().Contains("rope") && m.HasProperty("_MainTex") && m.mainTexture != null) { tex = m.mainTexture; break; }
            if (tex == null)
            {
                GameObject beam = RavenFx.Prefab("wood_beam");
                Renderer r = beam != null ? beam.GetComponentInChildren<Renderer>(true) : null;
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_MainTex")) tex = r.sharedMaterial.mainTexture;
            }
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            _ropeMat = new Material(sh) { name = "RavenWatch rope", hideFlags = HideFlags.HideAndDontSave };
            if (tex != null) { _ropeMat.mainTexture = tex; _tile = tex.wrapMode == TextureWrapMode.Repeat; _ropeMat.mainTextureScale = new Vector2(_tile ? 1.5f : 1f, 0.15f); }
            return _ropeMat;
        }

        private LineRenderer Line(string name, int points)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = RopeMaterial();
            lr.positionCount = points;
            lr.useWorldSpace = true;
            lr.startWidth = lr.endWidth = RopeWidth;
            lr.textureMode = _tile ? LineTextureMode.Tile : LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            Color c = _ropeMat != null && _ropeMat.mainTexture != null ? new Color(0.78f, 0.66f, 0.5f, 1f) : new Color(0.45f, 0.36f, 0.24f, 1f); // (a texture: lightly tinted; none: a hemp colour)
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private void Show(bool on) { foreach (Rope r in _ropes) if (r != null && r.Line != null) { r.Line.enabled = on; if (r.Knot != null) r.Knot.enabled = on; if (!on) r.Placed = false; } }

        private void LateUpdate()
        {
            if (Pl == null) { Destroy(gameObject); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Transform h = PairFx.Flying(Pl, false), m = PairFx.Flying(Pl, true);
            if (h == null || m == null) { Show(false); return; }
            for (int i = 0; i < 2; i++)
            {
                bool munin = i == 1;
                Transform bird = munin ? m : h;
                Transform bone = _bones[i] != null ? _bones[i] : (_bones[i] = Shoulder.Bone(Pl, munin));
                Vector3 to = (bone != null ? bone.position : Pl.transform.position + Vector3.up * 1.45f) - Pl.transform.forward * ShoulderBack;
                Draw(_ropes[i], Grip(i, bird), to, dt);
            }
            Sounds(h, m);
        }

        // where the rope goes into the bird: between its feet, held in the claws (no feet found: its body)
        private Vector3 Grip(int i, Transform bird)
        {
            if (_birdOf[i] != bird.gameObject) { _birdOf[i] = bird.gameObject; _footL[i] = RavenGear.Bone(bird.gameObject, "Leg3.L"); _footR[i] = RavenGear.Bone(bird.gameObject, "Leg3.R"); }
            if (_footL[i] != null && _footR[i] != null) return (_footL[i].position + _footR[i].position) * 0.5f;
            Transform one = _footL[i] != null ? _footL[i] : _footR[i];
            return one != null ? one.position : bird.position;
        }

        // the rope's middle chases where a slightly sagging rope's middle would be: a quick spring, so it bends as the
        // ends move and settles; drawn as a curve through it
        private static void Draw(Rope r, Vector3 a, Vector3 b, float dt)
        {
            float len = Vector3.Distance(a, b);
            Vector3 rest = (a + b) * 0.5f + Vector3.down * (Sag * len);
            if (!r.Placed) { r.Mid = rest; r.Vel = Vector3.zero; r.Tail = a + Vector3.down * TailLen; r.TailVel = Vector3.zero; r.Placed = true; }
            r.Vel += ((rest - r.Mid) * Spring - r.Vel * Damp) * dt;
            r.Mid += r.Vel * dt;
            Vector3 off = r.Mid - rest; // (never a loop: a short leash on how far it may lag)
            if (off.magnitude > len * 0.25f) r.Mid = rest + off.normalized * len * 0.25f;
            Vector3 ctrl = r.Mid * 2f - (a + b) * 0.5f; // (the curve passes through the middle)
            r.Line.enabled = true;
            if (r.Knot != null) r.Knot.enabled = true;
            for (int k = 0; k < Points; k++)
            {
                float t = k / (float)(Points - 1), u = 1f - t;
                r.Line.SetPosition(k, u * u * a + 2f * u * t * ctrl + t * t * b);
            }
            Knot(r, a, (b - a).normalized, dt);
        }

        // round the legs a turn and three quarters, winding a little along the rope, then the loose end down from the knot
        private static void Knot(Rope r, Vector3 at, Vector3 along, float dt)
        {
            if (r.Knot == null) return;
            Vector3 u = Vector3.Cross(along, Vector3.up);
            if (u.sqrMagnitude < 0.001f) u = Vector3.Cross(along, Vector3.forward);
            u.Normalize();
            Vector3 v = Vector3.Cross(along, u);
            for (int k = 0; k < KnotPoints; k++)
            {
                float t = k / (float)(KnotPoints - 1), ang = t * KnotTurns * Mathf.PI * 2f;
                r.Knot.SetPosition(k, at + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * KnotR + along * (KnotWind * (t - 0.5f)));
            }
            Vector3 from = r.Knot.GetPosition(KnotPoints - 1);
            Vector3 hang = from + Vector3.down * TailLen;
            r.TailVel += ((hang - r.Tail) * TailSpring - r.TailVel * TailDamp) * dt;
            r.Tail += r.TailVel * dt;
            Vector3 off = r.Tail - from; // (it keeps its length: only its swing lags)
            r.Tail = from + (off.sqrMagnitude > 0.0001f ? off.normalized : Vector3.down) * TailLen;
            Vector3 bend = (from + r.Tail) * 0.5f + Vector3.down * 0.015f;
            for (int k = 0; k < TailPoints; k++)
            {
                float t = (k + 1) / (float)TailPoints, w = 1f - t;
                r.Knot.SetPosition(KnotPoints + k, w * w * from + 2f * w * t * bend + t * t * r.Tail);
            }
        }

        private void Sounds(Transform h, Transform m)
        {
            if (Time.time >= _flapAt) { _flapAt = Time.time + Random.Range(1.1f, 1.8f); bool mu = Random.value < 0.5f; PairFx.Sound(PairFx.HeavyFlap, (mu ? m : h).position, 1.25f, 0.45f); }
            if (Time.time >= _creakAt) { _creakAt = Time.time + Random.Range(3f, 6f); PairFx.Sound(PairFx.Creak, Pl.transform.position + Vector3.up * 1.6f, 1.6f, 0.3f); }
            if (Time.time >= _featherAt) { _featherAt = Time.time + Random.Range(5f, 10f); bool mu = Random.value < 0.5f; Feathers.Shed((mu ? m : h).position, mu, 1); }
            for (int i = 0; i < 2; i++)
            {
                float ph = PairFx.Phase(Pl, i == 1);
                if (_lastPhase[i] < 0.9f && ph >= 0.9f) PairFx.Sound(PairFx.Cry, (i == 1 ? m : h).position, 1.35f, 0.7f); // the jerk back up: a strained cry
                _lastPhase[i] = ph;
            }
        }
    }

    // ---------------------------------------------------------------- «Розігнати хмари»
    internal sealed class SkyLook : PairLook
    {
        // the edge of the clear circle — pale haze and our blue runes rising off the ground — on a ring of its own that
        // follows you on a spring: it lags as you go, swings a little past and settles (RingSpring, RingDamp)
        internal static float RingSpring = 4f, RingDamp = 3.2f;
        private GameObject _fx;
        private Transform _ring;
        private Vector3 _ringVel;
        private ParticleSystem _edge, _runes;
        private Light _light;
        private ParticleSystem[] _trails = new ParticleSystem[2];
        private float _r;

        internal override void Init(Player pl)
        {
            base.Init(pl);
            _fx = new GameObject("sky fx");
            _fx.transform.SetParent(transform, false);
            _ring = new GameObject("sky ring").transform;
            Shadow.Outside(_ring.gameObject, Pl);
            _ring.position = pl.transform.position;
            // the haze: low on the ground round the circle
            _edge = PairFx.Particles(_ring, "edge", Feathers.Smoke(), false);
            if (_edge != null)
            {
                ParticleSystem.MainModule main = _edge.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                ParticleSystem.ShapeModule sh = _edge.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radiusThickness = 0f;
                _edge.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                _edge.transform.localPosition = Vector3.up * 0.4f;
                PairFx.Fade(_edge, new Color(0.92f, 0.95f, 1f), 0.22f);
            }
            // the runes: ours (the dome's), Hugin's blue, rising slowly off the ground along the edge
            _runes = Feathers.MakeRunes(_ring, "runes", Vector3.up * 0.5f);
            if (_runes != null)
            {
                ParticleSystem.MainModule main = _runes.main;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local; // (they go with the ring)
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
                main.startSpeed = 0f;
                main.maxParticles = 400;
                main.startColor = new ParticleSystem.MinMaxGradient(PairFx.HuginGlow, Color.Lerp(PairFx.HuginGlow, Color.white, 0.35f));
                ParticleSystem.ShapeModule sh = _runes.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radiusThickness = 0f;
                _runes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                ParticleSystem.VelocityOverLifetimeModule vel = _runes.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                vel.z = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); // (the shape is turned: its z is up)
                ParticleSystem.ColorOverLifetimeModule col = _runes.colorOverLifetime;
                col.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
            Radius();
            if (_edge != null) _edge.Play();
            if (_runes != null) _runes.Play();
            // a light from above: the ground under the clear sky warmer
            if (pl != null && Player.m_localPlayer != null && Vector3.Distance(pl.transform.position, Player.m_localPlayer.transform.position) < 35f)
            {
                GameObject lg = new GameObject("sun");
                lg.transform.SetParent(_fx.transform, false);
                lg.transform.localPosition = Vector3.up * 14f;
                lg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _light = lg.AddComponent<Light>();
                _light.type = LightType.Spot; _light.spotAngle = 70f; _light.range = 22f; _light.intensity = 0f;
                _light.color = new Color(1f, 0.92f, 0.75f); _light.shadows = LightShadows.None;
            }
            Vector3 at = pl.transform.position;
            PairFx.Sound(PairFx.Gust, at, 2f, 0.7f);
            PairFx.Sound(PairFx.Thunder, at + Vector3.up * 20f, 3f, 0.35f); // a far rumble, rolling away
        }

        // the circle's width as the caster's game wrote it (golden runes III: twice as wide); the edge as dense at any width
        private void Radius()
        {
            ZDO z = Pl != null ? NetFlag.Zdo(Pl, false) : null;
            float r = z != null ? z.GetFloat("RavenWatch_ClearR", ClearSky.Radius) : ClearSky.Radius;
            if (Mathf.Abs(r - _r) <= 0.1f) return;
            _r = r;
            if (_edge != null) { ParticleSystem.ShapeModule sh = _edge.shape; sh.radius = r; ParticleSystem.EmissionModule em = _edge.emission; em.rateOverTime = 1.8f * r; }
            if (_runes != null) { ParticleSystem.ShapeModule sh = _runes.shape; sh.radius = r; ParticleSystem.EmissionModule em = _runes.emission; em.rateOverTime = 1.6f * r; }
        }

        internal override void Finish()
        {
            if (Pl != null) PairFx.Sound(PairFx.Gust, Pl.transform.position, 2.6f, 0.4f); // the wind easing off
            for (int i = 0; i < 2; i++) if (_trails[i] != null) { _trails[i].transform.SetParent(null, true); PairFx.Ease(_trails[i].gameObject, 2f); }
            _ended = true;
            if (_ring != null) PairFx.Ease(_ring.gameObject, 6f);
            if (_fx != null) Destroy(_fx);
            Destroy(gameObject);
        }

        private bool _ended;
        private void OnDestroy() { if (!_ended && _ring != null) Destroy(_ring.gameObject); } // (gone out of sight: the ring at once too)

        private void LateUpdate()
        {
            if (Pl == null) { Finish(); return; }
            transform.position = Pl.transform.position;
            Radius();
            // the ring on its spring
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _ringVel += ((Pl.transform.position - _ring.position) * RingSpring - _ringVel * RingDamp) * dt;
            _ring.position += _ringVel * dt;
            if (_light != null) _light.intensity = Mathf.MoveTowards(_light.intensity, 1.1f, Time.deltaTime * 0.5f);
            // each raven its own light trail while it is up there
            for (int i = 0; i < 2; i++)
            {
                Transform bird = PairFx.Flying(Pl, i == 1);
                if (bird == null) { if (_trails[i] != null) { ParticleSystem.EmissionModule e0 = _trails[i].emission; e0.enabled = false; } continue; }
                if (_trails[i] == null) _trails[i] = Trail(i == 1);
                if (_trails[i] == null) continue;
                _trails[i].transform.position = bird.position;
                ParticleSystem.EmissionModule em = _trails[i].emission; em.enabled = true;
            }
        }

        // a raven's trail: soft light of its colour left in the air behind it (it follows the bird from outside its model)
        private ParticleSystem Trail(bool munin)
        {
            ParticleSystem ps = PairFx.Particles(transform, munin ? "trail M" : "trail H", Feathers.Smoke(), true);
            if (ps == null) return null;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f; em.rateOverDistance = 7f;
            ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            PairFx.Fade(ps, munin ? PairFx.MuninGlow : PairFx.HuginGlow, 0.55f);
            ps.Play();
            return ps;
        }
    }

    // ---------------------------------------------------------------- «Вихор»
    internal sealed class WhirlLook : PairLook
    {
        // the whirl on a ring of its own, left where it is and drawn after you on a spring (as Hugin's light follows):
        // it lags as you go and settles round you again; it gathers over GrowSeconds and dies down as the feathers fly out
        internal static float RingSpring = 2.5f, RingDamp = 2.6f, GrowSeconds = 1.5f, FeatherRate = 42f, DustRate = 40f;
        private Transform _ring;
        private Vector3 _ringVel;
        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
        private readonly List<float> _rates = new List<float>();
        private float _born, _howlAt;
        private bool _ended, _grown;

        internal override void Init(Player pl)
        {
            base.Init(pl);
            _ring = new GameObject("RavenWatch_WhirlRing").transform;
            Shadow.Outside(_ring.gameObject, Pl);
            _ring.position = pl.transform.position;
            Ring("blue", Feathers.Feather(), PairFx.HuginGlow, 0f, 0.3f, 3.5f, 0.95f, FeatherRate);
            Ring("red", Feathers.Feather(), PairFx.MuninGlow, 180f, 0.3f, 3.5f, 0.95f, FeatherRate);
            Ring("dust", Feathers.Smoke(), new Color(0.55f, 0.48f, 0.38f), 0f, 0.15f, 0.6f, 0.3f, DustRate, 360f);
            _born = Time.time;
            _howlAt = Time.time;
        }

        // a half ring (arc) of the whirl turning round you: feathers (or dust) rise as they go round
        private void Ring(string name, Material m, Color c, float startAngle, float up0, float up1, float alpha, float rate, float arc = 180f)
        {
            ParticleSystem ps = PairFx.Particles(_ring, name, m, false);
            if (ps == null) return;
            bool dust = m == Feathers.Smoke();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSize = dust ? new ParticleSystem.MinMaxCurve(1.2f, 2.2f) : new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f; // (it gathers: LateUpdate)
            ParticleSystem.ShapeModule sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Whirl.Radius * 0.85f; sh.radiusThickness = dust ? 0.15f : 0.7f; sh.arc = arc;
            ps.transform.localRotation = Quaternion.Euler(-90f, startAngle, 0f); // (turned: its z is the world's up)
            ps.transform.localPosition = Vector3.up * 0.3f;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            float turn = dust ? 1.4f : 1.9f, pull = dust ? 0f : -0.4f; // round, and drawn in a little (every curve of a group in one mode)
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); vel.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(turn, turn);
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(up0, up1); // rising as they go round
            vel.radial = new ParticleSystem.MinMaxCurve(pull, pull);
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            PairFx.Fade(ps, c, alpha);
            ps.Play();
            _systems.Add(ps); _rates.Add(rate);
        }

        internal override void Finish()
        {
            _ended = true;
            if (_ring != null) PairFx.Ease(_ring.gameObject, 3.5f);
            Destroy(gameObject);
        }

        private void OnDestroy() { if (!_ended && _ring != null) Destroy(_ring.gameObject); } // (gone out of sight: the ring at once too)

        private void LateUpdate()
        {
            if (Pl == null) { Finish(); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            transform.position = Pl.transform.position;
            _ringVel += ((Pl.transform.position - _ring.position) * RingSpring - _ringVel * RingDamp) * dt;
            _ring.position += _ringVel * dt;
            if (!_grown)
            {
                float grow = Mathf.Clamp01((Time.time - _born) / GrowSeconds);
                _grown = grow >= 1f;
                for (int i = 0; i < _systems.Count; i++) if (_systems[i] != null) { ParticleSystem.EmissionModule em = _systems[i].emission; em.rateOverTime = _rates[i] * grow * grow; }
            }
            if (Time.time >= _howlAt) { _howlAt = Time.time + Random.Range(1.4f, 2f); PairFx.Sound(PairFx.Gust, Pl.transform.position + Vector3.up, 1.5f, 0.35f); } // the wind howling round
        }
    }
}
#endif

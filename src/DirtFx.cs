#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A dirty raven shows it (Bath, 2026-10-28): now and then a bit of it falls off — a clump of mud, flakes of ash, a
    // drop of blood, the dust of the bones — and flies buzz round the filthiest (three; one round a dirty one of the blood
    // or the bones). On every game, for every raven drawn on a shoulder near you, from its owner's ZDO (Bath.Seen). Our
    // own small particles on the soft mote (RavenAura.Dot); the flies steered by hand (DirtFlies), each darting from one
    // point about the raven to the next with a little inertia, never a clean circle.
    internal static class DirtFx
    {
        internal static float DirtyEveryMin = 8f, DirtyEveryMax = 14f, FilthyEveryMin = 4f, FilthyEveryMax = 8f, Near = 40f, DropScale = 1f;
        internal static float FlySize = 0.035f, FlyRadius = 0.22f, FlyAccel = 9f, FlySpeed = 1.6f, FlyDrag = 2.2f;
        internal static int FlyMost = 3;

        private sealed class Fx { internal Transform Body; internal ParticleSystem Drops; internal DirtFlies Flies; internal float NextDrop; internal int Seen; }
        private static int _tick;
        private static readonly Dictionary<ShoulderLife, Fx> On = new Dictionary<ShoulderLife, Fx>();
        private static readonly List<ShoulderLife> Gone = new List<ShoulderLife>();
        private static readonly List<Player> Players = new List<Player>();
        private static bool _failed;

        // twice a second (RavenSkills)
        internal static void Tick()
        {
            Player me = Player.m_localPlayer;
            if (me == null || _failed) return;
            try
            {
                Players.Clear();
                _tick++;
                Player.GetPlayersInRange(me.transform.position, Near, Players);
                foreach (Player pl in Players)
                    foreach (bool m in new[] { false, true })
                    {
                        ShoulderLife life = Shoulder.LifeOf(pl, m);
                        if (life == null) continue;
                        int kind, stage = Bath.Seen(pl, m, out kind);
                        Fx f;
                        On.TryGetValue(life, out f);
                        if (stage < 2 && f == null) continue;
                        if (f == null) { f = Make(life); if (f == null) continue; On[life] = f; }
                        f.Seen = _tick;
                        Show(f, stage, kind);
                    }
                Players.Clear();
                foreach (KeyValuePair<ShoulderLife, Fx> kv in On)
                {
                    if (kv.Key == null || kv.Value.Body == null) { Gone.Add(kv.Key); continue; }
                    if (kv.Value.Seen != _tick && kv.Value.Flies != null) kv.Value.Flies.Want = 0; // (no longer the one on a shoulder, or far: its flies off — never left buzzing round a stale bird)
                }
                foreach (ShoulderLife l in Gone) On.Remove(l);
                Gone.Clear();
            }
            catch (Exception e) { _failed = true; if (Plugin.Instance != null) Plugin.Instance.Log("Raven dirt fx failed: " + e); }
        }

        // how many flies: three round the filthiest (not the ash: none would stay), one round a dirty one of the blood or the bones
        private static int FliesFor(int stage, int kind) { return stage >= 3 ? (kind != Bath.Ash ? FlyMost : 0) : stage >= 2 && (kind == Bath.Blood || kind == Bath.Bones) ? 1 : 0; }

        private static void Show(Fx f, int stage, int kind)
        {
            if (f.Flies != null) f.Flies.Want = FliesFor(stage, kind);
            if (stage < 2 || !f.Body.gameObject.activeInHierarchy) { f.NextDrop = 0f; return; }
            if (f.NextDrop <= 0f) { f.NextDrop = Time.time + UnityEngine.Random.Range(1f, 4f); return; }
            if (Time.time < f.NextDrop) return;
            f.NextDrop = Time.time + (stage >= 3 ? UnityEngine.Random.Range(FilthyEveryMin, FilthyEveryMax) : UnityEngine.Random.Range(DirtyEveryMin, DirtyEveryMax));
            Drop(f, kind);
        }

        // a bit of it off the raven's belly
        private static void Drop(Fx f, int kind)
        {
            if (f == null || f.Drops == null) return;
            ParticleSystem.MainModule main = f.Drops.main;
            main.gravityModifier = kind == Bath.Ash ? 0.12f : kind == Bath.Bones ? 0.05f : kind == Bath.Blood ? 1.5f : 1.2f;
            int n = kind == Bath.Ash ? UnityEngine.Random.Range(4, 8) : kind == Bath.Bones ? UnityEngine.Random.Range(2, 5) : kind == Bath.Blood ? UnityEngine.Random.Range(2, 5) : UnityEngine.Random.Range(2, 4);
            Vector3 at = f.Body.position - Vector3.up * 0.04f;
            float k = DropScale;
            for (int i = 0; i < n; i++)
            {
                ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
                e.position = at + UnityEngine.Random.insideUnitSphere * 0.05f;
                e.applyShapeToPosition = false;
                Vector3 side = UnityEngine.Random.insideUnitSphere; side.y = 0f;
                switch (kind)
                {
                    case Bath.Ash:
                        e.velocity = side * 0.15f + Vector3.up * 0.05f;
                        e.startColor = Color.Lerp(new Color(0.3f, 0.29f, 0.28f, 0.95f), new Color(0.55f, 0.53f, 0.5f, 0.9f), UnityEngine.Random.value);
                        e.startSize = UnityEngine.Random.Range(0.025f, 0.045f) * k; e.startLifetime = UnityEngine.Random.Range(2.2f, 3.2f);
                        break;
                    case Bath.Bones:
                        e.velocity = side * 0.18f;
                        e.startColor = Color.Lerp(new Color(0.42f, 0.38f, 0.31f, 0.7f), new Color(0.58f, 0.52f, 0.43f, 0.6f), UnityEngine.Random.value);
                        e.startSize = UnityEngine.Random.Range(0.06f, 0.1f) * k; e.startLifetime = UnityEngine.Random.Range(1.5f, 2.2f);
                        break;
                    case Bath.Blood:
                        e.velocity = side * 0.06f - Vector3.up * 0.1f;
                        e.startColor = Color.Lerp(new Color(0.35f, 0.01f, 0.01f, 1f), new Color(0.55f, 0.04f, 0.03f, 1f), UnityEngine.Random.value);
                        e.startSize = UnityEngine.Random.Range(0.022f, 0.032f) * k; e.startLifetime = 1.2f;
                        break;
                    default:
                        e.velocity = side * 0.14f - Vector3.up * 0.15f;
                        e.startColor = Color.Lerp(new Color(0.18f, 0.12f, 0.05f, 1f), new Color(0.32f, 0.22f, 0.1f, 1f), UnityEngine.Random.value);
                        e.startSize = UnityEngine.Random.Range(0.035f, 0.055f) * k; e.startLifetime = 1.4f;
                        break;
                }
                f.Drops.Emit(e, 1);
            }
        }

        private static Fx Make(ShoulderLife life)
        {
            Transform body = RavenBones.Find(life.transform, "Spine1");
            if (body == null) body = life.transform;
            Material dot = RavenAura.Dot();
            Fx f = new Fx { Body = body };

            // the bits that fall: in the world, each shot one by one (Drop)
            GameObject g = new GameObject("RavenWatch_Dirt");
            g.transform.SetParent(body, false);
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            ParticleSystem.EmissionModule em = ps.emission; em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape; sh.enabled = false;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(0.05f, 0.7f));
            ParticleSystem.NoiseModule noise = ps.noise; noise.enabled = true; noise.strength = 0.04f; noise.frequency = 1.2f; // (ash and dust drift; a clump hardly)
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            if (dot != null) r.sharedMaterial = dot;
            ps.Play();
            f.Drops = ps;

            // the flies: in the world, moved by hand every frame (DirtFlies)
            GameObject fg = new GameObject("RavenWatch_Flies");
            fg.transform.SetParent(body, false);
            ParticleSystem fly = fg.AddComponent<ParticleSystem>();
            fly.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule fm = fly.main;
            fm.loop = true; fm.playOnAwake = false;
            fm.scalingMode = ParticleSystemScalingMode.Shape;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.maxParticles = FlyMost;
            ParticleSystem.EmissionModule fe = fly.emission; fe.rateOverTime = 0f;
            ParticleSystem.ShapeModule fs = fly.shape; fs.enabled = false;
            ParticleSystemRenderer frr = fg.GetComponent<ParticleSystemRenderer>();
            if (dot != null) frr.sharedMaterial = dot;
            fly.Play();
            f.Flies = fg.AddComponent<DirtFlies>();
            f.Flies.Init(body, fly);
            return f;
        }

        // seen from a little in to a little before its end
        private static Gradient Fade(float inTo, float outFrom)
        {
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inTo), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // the console (raven bath fx [h|m]): a bit off your own raven now, to look at
        internal static string DropNow(bool munin)
        {
            Player me = Player.m_localPlayer;
            ShoulderLife life = Shoulder.LifeOf(me, munin);
            if (life == null) return Plugin.RavenName(munin) + " не на плечі.";
            int kind, stage = Bath.Seen(me, munin, out kind);
            Fx f;
            if (!On.TryGetValue(life, out f)) { f = Make(life); if (f == null) return "no fx"; On[life] = f; }
            if (f.Flies != null) f.Flies.Want = FliesFor(stage, kind);
            Drop(f, kind);
            return Plugin.RavenName(munin) + ": a bit of " + Bath.KindIds[kind] + " off (stage " + stage + ", flies " + (f.Flies != null ? f.Flies.Want : 0) + ")";
        }
    }

    // The flies round a dirty raven, every frame: each heads for a point about the raven's back, speeding up toward it
    // with a little inertia (overshooting, turning), and picks the next point every fraction of a second; carried along
    // with the raven as you move, so they never trail behind.
    internal sealed class DirtFlies : MonoBehaviour
    {
        internal int Want;
        private Transform _body;
        private ParticleSystem _ps;
        private readonly ParticleSystem.Particle[] _p = new ParticleSystem.Particle[3];
        private readonly Vector3[] _pos = new Vector3[3], _vel = new Vector3[3], _aim = new Vector3[3];
        private readonly float[] _next = new float[3], _phase = new float[3];
        private Vector3 _was;
        private int _have;
        private readonly object[] _args = new object[3];
        private static System.Reflection.MethodInfo Set;

        internal void Init(Transform body, ParticleSystem ps) { _body = body; _ps = ps; _was = Center(); }

        private Vector3 Center() { return _body.position + Vector3.up * 0.06f; }

        // a point about the raven, mostly above and beside it (not inside the bird)
        private static Vector3 Aim()
        {
            Vector3 v = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.6f, 1f) * DirtFx.FlyRadius;
            v.y = Mathf.Abs(v.y) * 0.6f + 0.02f;
            return v;
        }

        private void LateUpdate()
        {
            if (_body == null || _ps == null) return;
            int want = Mathf.Clamp(Want, 0, _p.Length);
            Vector3 c = Center();
            Vector3 carried = c - _was; // (you ran on: they come along)
            _was = c;
            if (carried.sqrMagnitude > 4f) carried = Vector3.zero; // (a jump of the model: placed anew below)
            float dt = Mathf.Min(Time.deltaTime, 0.05f), now = Time.time;
            while (_have < want)
            {
                _pos[_have] = c + Aim(); _vel[_have] = UnityEngine.Random.onUnitSphere * 0.3f; _aim[_have] = Aim(); _next[_have] = now; _phase[_have] = UnityEngine.Random.value * 10f;
                _have++;
            }
            if (_have > want) _have = want;
            if (_have == 0) { if (_ps.particleCount > 0) _ps.Clear(); return; } // (clean again: gone — a zero to SetParticles would leave the old ones flying)
            for (int i = 0; i < _have; i++)
            {
                if (now >= _next[i]) { _aim[i] = Aim(); _next[i] = now + UnityEngine.Random.Range(0.12f, 0.5f); }
                _pos[i] += carried;
                Vector3 to = c + _aim[i] - _pos[i];
                if (to.sqrMagnitude > 1f) { _pos[i] = c + Aim(); _vel[i] = Vector3.zero; continue; } // (left far behind: back about the raven)
                _vel[i] += to.normalized * Mathf.Min(DirtFx.FlyAccel, to.magnitude * 60f) * dt;
                _vel[i] *= Mathf.Clamp01(1f - DirtFx.FlyDrag * dt);
                if (_vel[i].sqrMagnitude > DirtFx.FlySpeed * DirtFx.FlySpeed) _vel[i] = _vel[i].normalized * DirtFx.FlySpeed;
                _pos[i] += _vel[i] * dt;
                ParticleSystem.Particle q = _p[i];
                q.position = _pos[i];
                q.startSize = DirtFx.FlySize * (0.85f + 0.15f * Mathf.Sin((now + _phase[i]) * 70f)); // (the wings' buzz: a flicker)
                q.startColor = Color32.Lerp(new Color32(18, 38, 14, 255), new Color32(52, 92, 30, 255), 0.5f + 0.5f * Mathf.Sin((now + _phase[i]) * 9f)); // (a green fly: its back glints as it turns)
                q.startLifetime = 10f; q.remainingLifetime = 10f;
                q.velocity = Vector3.zero;
                _p[i] = q;
            }
            if (Set == null) Set = typeof(ParticleSystem).GetMethod("SetParticles", new[] { typeof(ParticleSystem.Particle[]), typeof(int), typeof(int) });
            if (Set == null) return;
            _args[0] = _p; _args[1] = _have; _args[2] = 0;
            Set.Invoke(_ps, _args); // (by reflection: the direct call drags in the Span overloads this build cannot see)
        }
    }
}
#endif

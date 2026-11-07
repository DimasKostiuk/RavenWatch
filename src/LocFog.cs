#if RAVEN_ITEM && RAVEN_NPC
// (the abandoned Hall's mist: from 2.9 — the user 2026-11-01)
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // While the Hall is abandoned its fire is out and Hel holds it: a bluish mist hangs over the whole building, and a
    // low one creeps over its floor. Only seen (each game makes its own, nothing sent or kept), only while the Hall is
    // a ruin (LocState) and its «fog:» conditions hold (locations.list: the Hall's — till the Watch's fire is lit) — and on the author's own Hall while he looks at that state (LocPreview). Made of our
    // smoke sheet (Feathers.Smoke), already full when it appears (prewarmed): it is simply there, as if always.
    // Its place comes from the Hall's own pieces: their places in the blueprint give its middle and its size; the low
    // one lies only over the Hall's own floors, each at its height (the grass round it stays clear).
    internal static class LocFog
    {
        private const float Every = 2f, Reach = 250f;
        private const int Enough = 5; // pieces of the author's own place found before its mist is laid
        private static readonly Color MistA = new Color(0.55f, 0.68f, 0.95f, 0.22f), MistB = new Color(0.38f, 0.5f, 0.85f, 0.16f);
        private static readonly Color FloorA = new Color(0.55f, 0.66f, 0.92f, 0.11f), FloorB = new Color(0.42f, 0.52f, 0.82f, 0.07f);

        internal static bool IsFloor(string prefab) { return prefab.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0; }
        internal static bool IsRoof(string prefab) { return prefab.IndexOf("roof", System.StringComparison.OrdinalIgnoreCase) >= 0; }
        private static readonly Dictionary<string, GameObject> _real = new Dictionary<string, GameObject>(); // over each place in the world (with its mist)
        private static GameObject _look; // over the author's own, the place he looks at
        private static Loc _lookOf;
        private static float _next;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Player me = Player.m_localPlayer;
            bool can = me != null && Feathers.Smoke() != null;
            foreach (Loc l in Locs.All)
            {
                GameObject g;
                _real.TryGetValue(l.Id, out g);
                Vector3 at; Quaternion rot;
                bool near = can && l.Where(out at, out rot) && Flat(me.transform.position - at) < Reach;
                bool real = near && l.Fog && l.Built && LocState.Ruined(l, LocState.StateOf(l))
                    && (l.FogWhile.Length == 0 || StoryCond.Holds(null, l.FogWhile, e => l.Log("fog: " + e), true)); // (its «fog:» conditions: the Hall's — till the Watch's fire breaks Hel's hold)
                if (!real) { if (near && g != null) Disperse(g); else Drop(ref g); g = null; }
                else if (g == null) g = FromWorld(l);
                _real[l.Id] = g;
            }

            Loc of = LocPreview.Of;
            if (!can || of == null || !of.Fog || LocPreview.State == 0 || !LocState.Ruined(of, LocPreview.State) || of != _lookOf) Drop(ref _look);
            if (can && of != null && of.Fog && LocPreview.State != 0 && LocState.Ruined(of, LocPreview.State) && _look == null) { _look = FromFrame(of); _lookOf = of; }
        }

        private static float Flat(Vector3 d) { d.y = 0f; return d.magnitude; }

        // the mist going, seen: it stops coming and drifts off to all sides, thinning (FogDisperse)
        private static void Disperse(GameObject g)
        {
            if (g == null) return;
            foreach (FloorMist m in g.GetComponentsInChildren<FloorMist>(true)) m.enabled = false;
            foreach (RoofMist m in g.GetComponentsInChildren<RoofMist>(true)) m.enabled = false;
            g.AddComponent<FogDisperse>();
        }

        private static void Drop(ref GameObject g)
        {
            if (g != null) Object.Destroy(g);
            g = null;
        }

        // the Hall a blueprint put into the world: its size from the blueprint (from afar only part of it is loaded),
        // its middle from any piece of it loaded (each carries its place in the blueprint)
        private static GameObject FromWorld(Loc l)
        {
            Vector3 at; Quaternion rot;
            List<Vector3> box, floors, roofs;
            if (!l.Where(out at, out rot) || (box = l.Corners()) == null || (floors = l.Floors()) == null || (roofs = l.Roofs()) == null) return null;
            List<ZNetView> all = LocParts.All(l.Id);
            if (all.Count == 0) return null;
            ZNetView one = all[0];
            Vector3 c = one.transform.position - rot * one.GetZDO().GetVec3(LocParts.KAt, Vector3.zero);
            return Make(c, rot, box, floors, roofs);
        }

        // the author's own Hall: by its frame
        private static GameObject FromFrame(Loc of)
        {
            Vector3 c; Quaternion rot;
            if (!BlueprintFrame.Get(of.Id, out c, out rot)) return null;
            List<Vector3> local = new List<Vector3>(), floors = new List<Vector3>(), roofs = new List<Vector3>();
            foreach (Piece pc in Blueprint.Around(c, LocParts.AuthorReach))
            {
                ZNetView nv = pc.GetComponent<ZNetView>();
                string which; Vector3 l;
                if (!LocParts.Place(nv, out which, out l) || which != of.Id || nv.GetZDO().GetString(LocParts.KOf, "").Length > 0) continue; // (a placed copy, or another place's)
                local.Add(l);
                string name = Utils.GetPrefabName(pc.gameObject);
                if (IsFloor(name)) floors.Add(l);
                if (IsRoof(name)) roofs.Add(l);
            }
            return local.Count < Enough ? null : Make(c, rot, local, floors, roofs);
        }

        private static GameObject Make(Vector3 c, Quaternion rot, List<Vector3> local, List<Vector3> floors, List<Vector3> roofs)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue, top = float.MinValue;
            foreach (Vector3 v in local)
            {
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x);
                z0 = Mathf.Min(z0, v.z); z1 = Mathf.Max(z1, v.z);
                top = Mathf.Max(top, v.y);
            }
            float w = x1 - x0, l = z1 - z0;
            GameObject root = new GameObject("RavenWatch_LocFog");
            root.transform.SetPositionAndRotation(c, rot);

            // over the roof: big slow clouds laid over its own roof pieces (RoofMist) — its shape, not a box; a place
            // without a roof: a box over it as before
            if (roofs.Count > 0)
            {
                ParticleSystem ps = Layer(root.transform, "mist", Vector3.zero, Vector3.zero,
                    new Vector2(5f, 9f), new Vector2(14f, 20f), 0f, 160, MistA, MistB, ParticleSystemRenderMode.Billboard);
                ps.gameObject.AddComponent<RoofMist>().Begin(ps, roofs);
            }
            else Layer(root.transform, "mist", new Vector3((x0 + x1) / 2f, top - 1f, (z0 + z1) / 2f), new Vector3(w + 8f, 4f, l + 8f),
                  new Vector2(6f, 11f), new Vector2(14f, 20f), 7f, 160, MistA, MistB, ParticleSystemRenderMode.Billboard);

            // the floors: wide thin sheets lying flat, laid one by one over the Hall's own floor pieces (FloorMist)
            if (floors.Count > 0)
            {
                ParticleSystem ps = Layer(root.transform, "floor", Vector3.zero, Vector3.zero,
                    new Vector2(3f, 6f), new Vector2(12f, 18f), 0f, 300, FloorA, FloorB, ParticleSystemRenderMode.HorizontalBillboard);
                ps.gameObject.AddComponent<FloorMist>().Begin(ps, floors);
            }
            return root;
        }

        private static ParticleSystem Layer(Transform parent, string name, Vector3 at, Vector3 box, Vector2 size, Vector2 life, float rate, int max,
                                  Color a, Color b, ParticleSystemRenderMode mode)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true; main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = 0f;
            main.maxParticles = max;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime; // (a slow drift, no wind of its own)
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f); vel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.03f); vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            ParticleSystem.RotationOverLifetimeModule turn = ps.rotationOverLifetime;
            turn.enabled = true; turn.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation; // (our smoke: four puffs, one each)
            sheet.enabled = true; sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            ParticleSystemRenderer r = g.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Feathers.Smoke();
            r.renderMode = mode;
            r.maxParticleSize = 4f; // (inside it, close up: not shrunk to half the screen)
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }

    // the mist dispersing: no more of it, and what there is drifts out from its middle to all sides, faster as it goes,
    // its life run quicker so it thins away by its own fade (the particle system's own modules: nothing read back)
    internal sealed class FogDisperse : MonoBehaviour
    {
        private const float Span = 16f, Push = 0.6f, Quicker = 1.25f; // (the user 2026-11-01: twice slower)
        private float _t;
        private ParticleSystem[] _ps;

        private void Start()
        {
            _ps = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem p in _ps)
            {
                ParticleSystem.EmissionModule em = p.emission; em.enabled = false;
                ParticleSystem.MainModule m = p.main; m.simulationSpeed = Quicker;
                ParticleSystem.VelocityOverLifetimeModule v = p.velocityOverLifetime;
                v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.x = v.y = v.z = new ParticleSystem.MinMaxCurve(0f);
                v.radial = new ParticleSystem.MinMaxCurve(Push); // (away from its own middle, every way)
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t > Span) { Destroy(gameObject); return; }
            foreach (ParticleSystem p in _ps)
            {
                if (p == null) continue;
                ParticleSystem.VelocityOverLifetimeModule v = p.velocityOverLifetime;
                v.radial = new ParticleSystem.MinMaxCurve(Push * (1f + _t)); // (faster as it goes)
            }
        }
    }

    // the high mist's own hand: each cloud over a roof piece of the place, a little aside and above it (the roof's own
    // outline, soft at its edges)
    internal sealed class RoofMist : MonoBehaviour
    {
        private const float Rate = 7f, Aside = 2.5f;
        private ParticleSystem _ps;
        private List<Vector3> _roofs;
        private float _owed;

        internal void Begin(ParticleSystem ps, List<Vector3> roofs)
        {
            _ps = ps; _roofs = roofs;
            for (int i = 0; i < 20; i++) { Lay(Mathf.RoundToInt(Rate)); _ps.Simulate(1f, true, false, false); } // (already there when it appears)
            _ps.Play();
        }

        private void Update()
        {
            if (_ps == null || _roofs == null) return;
            _owed += Rate * Time.deltaTime;
            int n = Mathf.FloorToInt(_owed);
            if (n > 0) { _owed -= n; Lay(n); }
        }

        private void Lay(int n)
        {
            Transform root = transform.parent;
            ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
            e.applyShapeToPosition = false;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = root.TransformPoint(_roofs[Random.Range(0, _roofs.Count)]);
                at.x += Random.Range(-Aside, Aside); at.z += Random.Range(-Aside, Aside); at.y += Random.Range(0.4f, 2.2f);
                e.position = at;
                _ps.Emit(e, 1);
            }
        }
    }

    // the low mist's own hand: each puff over a floor piece of the Hall, a little aside, on that floor's top (a ray
    // down finds it; off the floor's edge it finds nothing and no puff is laid: the mist keeps to the floors)
    internal sealed class FloorMist : MonoBehaviour
    {
        private const float Rate = 18f, Aside = 1.2f;
        private static readonly int Mask = LayerMask.GetMask("piece", "piece_nonsolid");
        private ParticleSystem _ps;
        private List<Vector3> _floors;
        private float _owed;

        internal void Begin(ParticleSystem ps, List<Vector3> floors)
        {
            _ps = ps; _floors = floors;
            for (int i = 0; i < 16; i++) { Lay(Mathf.RoundToInt(Rate)); _ps.Simulate(1f, true, false, false); } // (already full when it appears)
            _ps.Play();
        }

        private void Update()
        {
            if (_ps == null || _floors == null) return;
            _owed += Rate * Time.deltaTime;
            int n = Mathf.FloorToInt(_owed);
            if (n > 0) { _owed -= n; Lay(n); }
        }

        private void Lay(int n)
        {
            Transform root = transform.parent;
            ParticleSystem.EmitParams e = new ParticleSystem.EmitParams();
            RaycastHit hit;
            for (int i = 0; i < n; i++)
            {
                Vector3 at = root.TransformPoint(_floors[Random.Range(0, _floors.Count)]);
                at.x += Random.Range(-Aside, Aside); at.z += Random.Range(-Aside, Aside);
                if (!Physics.Raycast(at + Vector3.up * 0.8f, Vector3.down, out hit, 1.6f, Mask)) continue;
                e.position = hit.point + Vector3.up * 0.15f;
                e.applyShapeToPosition = false;
                _ps.Emit(e, 1);
            }
        }
    }
}
#endif

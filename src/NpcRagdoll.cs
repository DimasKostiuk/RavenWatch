#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own body gone limp for the moment it falls senseless (the user 2026-11-23: «коли нпс ранять і він падає, то зробити йому
    // регдол, але щоб його не відшвирювало надто далеко. А коли він впаде, тоді плавно його в позу лежачу переводити»). NpcFall owns the
    // fall — when it begins, how it will lie, when it has landed, the blend into the lying pose; this is only the physics of the seconds between.
    // No second body: the game's own ragdoll is another object (dressed anew, living in the net, gone in a puff), and a swap of two bodies
    // shows. The person's OWN bones get what makes a ragdoll of them — a body, a collider and a joint each — copied part by part from the
    // ragdoll the game would make of this very body (its death effects' prefab: the same skeleton, the game's own weights and limits),
    // and lose them again when it has landed. What it wears and holds hangs on those bones and falls with them.
    // Not thrown far (the game flings its ragdolls with the blow's whole push):
    //   · the push is ours and small (NpcFall: MaxPush m/s at most, hardly any of it upward), and most of it on its upper body: it topples
    //     over its feet the way it is to lie, it does not slide off whole
    //   · a part pressed into the ground or into another is let out slowly (Depen) — that, not the blow, is what shoots a ragdoll off
    //   · past Far m from where it stood its sideways speed is taken away (the leash), and no part ever goes faster than Fastest
    // Landed (Step says so): every part slower than Still for StillFor s — or Longest s gone, a longer fall (off a wall) given twice that.
    // Each game drops its own copy (nothing is sent: its ZDO says it fell, as before); its standing body is not moved meanwhile on any game (NpcFall).
    // Cost: once a fall — the prefab's parts read and some fifteen bodies, colliders and joints made (heavy: said at the call), a list a fall;
    // a frame of the fall — one pass over those bodies; nothing at all before or after.
    internal sealed class NpcRagdoll
    {
        // ---------------------------------------------------------------- numbers (raven npc tune NpcRagdoll.<name>)
        internal static float MaxPush = 1.6f, MaxUp = 0.4f;      // m/s the fall's push may be, sideways · upward
        internal static float Far = 2f, FarDamp = 8f;            // m from where it stood its sideways speed begins to die · how fast (a second: e^-FarDamp)
        internal static float Fastest = 5f;                      // m/s no part goes faster than, whatever struck it
        internal static float Depen = 1.5f;                      // m/s a part pressed into something is pushed out at most
        internal static float Drag = 0.5f, Spin = 1.5f;          // the least damping of a part's motion · of its turning (the prefab's own, if more)
        internal static float Still = 0.25f, StillFor = 0.35f;   // m/s under which a part is at rest · s all of them must be
        internal static float Longest = 3.5f, Falling = 2f;      // s a fall may last · m/s downward at which it is still falling (then twice Longest)
        internal static bool Trace = false;

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private readonly List<Joint> _joints = new List<Joint>();
        private readonly List<GameObject> _made = new List<GameObject>(); // (the colliders' own objects under the bones)
        private Rigidbody _hips;
        private Vector3 _from;
        private float _since, _stillSince = -1f;

        internal int Parts { get { return _bodies.Count; } }

        // the ragdoll the game would make of this body (null: its death leaves none)
        private static GameObject Layout(Character c)
        {
            if (c == null || c.m_deathEffects == null || c.m_deathEffects.m_effectPrefabs == null) return null;
            foreach (EffectList.EffectData e in c.m_deathEffects.m_effectPrefabs)
                if (e != null && e.m_prefab != null && e.m_prefab.GetComponent<Ragdoll>() != null) return e.m_prefab;
            return null;
        }

        // the body's own bones by name (the bones its mesh is skinned to: what it wears may carry bones of the same names)
        private static Dictionary<string, Transform> Bones(Character c)
        {
            Dictionary<string, Transform> map = new Dictionary<string, Transform>();
            VisEquipment ve = c.GetComponent<VisEquipment>();
            if (ve == null || ve.m_bodyModel == null || ve.m_bodyModel.bones == null) return map;
            foreach (Transform b in ve.m_bodyModel.bones) { Transform t = BodyBuild.Real(b); if (t != null && !map.ContainsKey(t.name)) map[t.name] = t; }
            return map;
        }

        // The body goes limp, pushed so (null: this body has no ragdoll to copy, or too few of its parts were found — it lies down by the blend alone).
        // Its animator must be off before this and stay off till End: it would write the bones back every frame.
        internal static NpcRagdoll Begin(Character c, Vector3 push, uint seed)
        {
            GameObject prefab = Layout(c);
            if (prefab == null) return null;
            Dictionary<string, Transform> bones = Bones(c);
            if (bones.Count == 0) return null;
            NpcRagdoll r = new NpcRagdoll();
            // The joints are made with the skeleton in the prefab's own pose and the bones put back after: a joint's limits are counted from how
            // its two bones stand when it is made, and the game's limits are written for that pose (made on a raised arm they would hold it raised).
            List<KeyValuePair<Transform, Quaternion>> posed = new List<KeyValuePair<Transform, Quaternion>>();
            HashSet<Transform> seen = new HashSet<Transform>();
            foreach (Transform pt in prefab.GetComponentsInChildren<Transform>(true)) // heavy: once a fall, the ragdoll prefab's own transforms (some seventy)
            {
                Transform b;
                if (!bones.TryGetValue(pt.name, out b) || seen.Contains(b)) continue; // (a name twice in the prefab: its first, else the second would keep the first's pose as «how it stood»)
                seen.Add(b);
                posed.Add(new KeyValuePair<Transform, Quaternion>(b, b.localRotation));
                b.localRotation = pt.localRotation;
            }
            Dictionary<Rigidbody, Rigidbody> made = new Dictionary<Rigidbody, Rigidbody>();
            Collider[] own = c.GetComponents<Collider>(); // (its standing capsule: the limp body does not lean on it)
            int skipped = 0;
            foreach (Rigidbody prb in prefab.GetComponentsInChildren<Rigidbody>(true)) // heavy: once a fall, the prefab's bodies (some fifteen), parents first
            {
                Transform bone;
                if (prb.transform == prefab.transform || !bones.TryGetValue(prb.name, out bone) || bone.GetComponent<Rigidbody>() != null) { skipped++; continue; }
                Joint pj = prb.GetComponent<Joint>();
                CharacterJoint pcj = pj as CharacterJoint;
                Rigidbody to = null;
                if (pj != null && (pcj == null || pj.connectedBody == null || !made.TryGetValue(pj.connectedBody, out to))) { skipped++; continue; } // (a joint of another kind, or to a part not made: this bone stays stiff on its parent)
                Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = prb.mass;
                rb.linearDamping = Mathf.Max(prb.linearDamping, Drag);
                rb.angularDamping = Mathf.Max(prb.angularDamping, Spin);
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.maxDepenetrationVelocity = Depen;
                rb.solverIterations = 12;
                foreach (Collider pc in prb.GetComponentsInChildren<Collider>(true)) // heavy: once a fall, a part's own colliders (one or two)
                {
                    if (pc.isTrigger || pc.GetComponentInParent<Rigidbody>(true) != prb) continue; // (true: the prefab is no object in the world — every one of it counts as inactive, and a plain lookup finds nothing)
                    GameObject g = new GameObject("RwLimp");
                    g.layer = pc.gameObject.layer;
                    g.transform.SetParent(bone, false);
                    g.transform.localPosition = prb.transform.InverseTransformPoint(pc.transform.position);
                    g.transform.localRotation = Quaternion.Inverse(prb.transform.rotation) * pc.transform.rotation;
                    Vector3 ps = prb.transform.lossyScale, cs = pc.transform.lossyScale; // (a collider on a child of its part with a scale of its own: ours as large against the bone)
                    g.transform.localScale = new Vector3(ps.x != 0f ? cs.x / ps.x : 1f, ps.y != 0f ? cs.y / ps.y : 1f, ps.z != 0f ? cs.z / ps.z : 1f);
                    Collider col = Copy(pc, g);
                    if (col == null) { Object.DestroyImmediate(g); continue; }
                    col.sharedMaterial = pc.sharedMaterial;
                    foreach (Collider o in own) if (o != null) Physics.IgnoreCollision(o, col, true);
                    r._made.Add(g);
                }
                if (pcj != null)
                {
                    CharacterJoint j = bone.gameObject.AddComponent<CharacterJoint>();
                    j.connectedBody = to;
                    j.anchor = pcj.anchor; j.axis = pcj.axis; j.swingAxis = pcj.swingAxis;
                    j.lowTwistLimit = pcj.lowTwistLimit; j.highTwistLimit = pcj.highTwistLimit;
                    j.swing1Limit = pcj.swing1Limit; j.swing2Limit = pcj.swing2Limit;
                    j.twistLimitSpring = pcj.twistLimitSpring; j.swingLimitSpring = pcj.swingLimitSpring;
                    j.enableProjection = true; // (a limb never comes off its joint, however it is struck)
                    j.enableCollision = pcj.enableCollision;
                    r._joints.Add(j);
                }
                else r._hips = rb; // (the one part with no joint: what the rest hangs on)
                made[prb] = rb;
                r._bodies.Add(rb);
            }
            foreach (KeyValuePair<Transform, Quaternion> kv in posed) kv.Key.localRotation = kv.Value; // (as it stood when it fell)
            if (r._bodies.Count < 6 || r._hips == null || r._made.Count == 0) // (not this skeleton's ragdoll, or no collider to stand on: no half-limp body, none falling through the ground)
            {
                Npcs.Log("limp body: only " + r._bodies.Count + " part(s) and " + r._made.Count + " collider(s) of «" + prefab.name + "» fit " + c.name + " (" + skipped + " not): it lies down without falling");
                r.End();
                return null;
            }
            uint s = seed == 0u ? 0x6D2B79F5u : seed;
            float feet = c.transform.position.y;
            foreach (Rigidbody rb in r._bodies)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                float high = Mathf.Clamp01((rb.position.y - feet) / 1.5f); // (its feet hardly pushed, its head the most: it topples over them)
                rb.linearVelocity = push * (0.15f + 0.85f * high) * (0.85f + 0.15f * ((s & 0xFFFF) / 65535f));
            }
            r._from = r._hips.position; r._since = Time.time;
            if (Trace) Npcs.Log("limp body: " + c.name + " — " + r._bodies.Count + " parts and " + r._made.Count + " colliders of «" + prefab.name + "» (" + skipped + " not made), pushed " + push.magnitude.ToString("0.0") + " m/s");
            return r;
        }

        // the collider as it is in the prefab, number for number: our object stands under the same bone at the same scale (Begin)
        private static Collider Copy(Collider from, GameObject onto)
        {
            CapsuleCollider cap = from as CapsuleCollider;
            if (cap != null) { CapsuleCollider c = onto.AddComponent<CapsuleCollider>(); c.direction = cap.direction; c.radius = cap.radius; c.height = cap.height; c.center = cap.center; return c; }
            BoxCollider box = from as BoxCollider;
            if (box != null) { BoxCollider c = onto.AddComponent<BoxCollider>(); c.size = box.size; c.center = box.center; return c; }
            SphereCollider sph = from as SphereCollider;
            if (sph != null) { SphereCollider c = onto.AddComponent<SphereCollider>(); c.radius = sph.radius; c.center = sph.center; return c; }
            return null; // (a mesh collider: not copied — a ragdoll's parts are capsules, boxes and balls)
        }

        // One frame of the fall: the leash and the speed limit. True: it has landed (or its time is over)
        internal bool Step(float dt)
        {
            if (_hips == null) return true;
            Vector3 off = _hips.position - _from; off.y = 0f;
            float keep = off.magnitude > Far ? Mathf.Exp(-FarDamp * dt) : 1f;
            float most = 0f;
            foreach (Rigidbody rb in _bodies)
            {
                if (rb == null) continue;
                Vector3 v = rb.linearVelocity;
                if (keep < 1f) { v.x *= keep; v.z *= keep; }
                float sp = v.magnitude;
                if (sp > Fastest) { v *= Fastest / sp; sp = Fastest; }
                if (keep < 1f || sp >= Fastest) rb.linearVelocity = v;
                if (sp > most) most = sp;
            }
            float t = Time.time - _since;
            if (most < Still) { if (_stillSince < 0f) _stillSince = Time.time; if (Time.time - _stillSince >= StillFor && t > 0.4f) return true; }
            else _stillSince = -1f;
            bool falling = _hips.linearVelocity.y < -Falling;
            return t > (falling ? Longest * 2f : Longest);
        }

        // The body is itself again: joints first (a body cannot go while a joint holds it), then the colliders (left without their part's
        // body for a moment they would join its standing body's), then the bodies. now: this moment (landed — the ray that finds the ground
        // under it must not find its own parts: NpcFall.Anchor); else with the frame's end (from OnDisable: the object may be going whole,
        // and taking a part of it away at once there is an error)
        internal void End(bool now = true)
        {
            for (int i = _joints.Count - 1; i >= 0; i--) Gone(_joints[i], now);
            foreach (GameObject g in _made) Gone(g, now);
            for (int i = _bodies.Count - 1; i >= 0; i--) Gone(_bodies[i], now);
            _joints.Clear(); _bodies.Clear(); _made.Clear(); _hips = null;
        }
        private static void Gone(Object o, bool now) { if (o == null) return; if (now) Object.DestroyImmediate(o); else Object.Destroy(o); }
    }
}
#endif

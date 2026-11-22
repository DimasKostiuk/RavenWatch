#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The raven sits on what is worn, not on a fixed spot by the bone: naked it would float, in heavy armour it would sink in.
    // For a player's raven (ShoulderPin) and a person's (NpcRaven) alike — any body with the game's VisEquipment.
    // When the gear changes (or a raven lands), the body and everything worn are read once as they are posed now — one reading for
    // both shoulders (Snapshot) — and for each shoulder two things are found (Measure):
    //   Lift  how far up or down from the seat by the bone the bird sits: the surface under its feet (three points) and under its
    //         body (breast, tail, outer wing: fur and a hood stand higher round the feet than under them — 2026-11-19, the wolf and
    //         lox capes had it in to its belly), the body's share less the height of its legs. A little into the armour is right
    //         (ArmourSink: the user, «головне щоб у повітрі не сидів»); never over MaxLift, never under MinLift.
    //   Side  how far along the shoulder from that seat: inwards when the seat hangs past the shoulder's edge (a narrow body, a
    //         hooded cloak: nothing under its feet there), outwards when the helmet comes into the bird (horns, antlers, a wide
    //         brim: the least of it within OutMost, and only while there is still shoulder under its feet).
    // Gear whose meshes cannot be read (any mod's may be odd) falls back on a middle value by the kind of gear: naked, tunic,
    // light or heavy armour, a cape (ByGear).
    // The reading is of the body as it is posed at that moment, and a cape lies otherwise on one who sits on the ground or bends low than on one
    // who stands: so the body's posture is part of what «changed» means (Posture: how high its hips are, how far its trunk leans, in a few
    // wide steps) — another one held for PostChecks looks, and it is read again. The user 2026-11-20: Torvald's raven stood in the air over his
    // fur — read while he sat by the fire, kept when he rose (seen: «raven fit again» alone put it back on the fur).
    // Cost: whether the gear changed is asked from the game's own equipment numbers (SigEvery s a body, a few ints), with a slow
    // look over its renderers as a safety (SafeEvery s); the reading itself (heavy: every worn mesh baked, their triangles gone
    // over once) only on a change or a landing, and once for both ravens of the same frame.
    internal static class ShoulderFit
    {
        internal static bool On = true;
        internal static float Margin = 0.01f;   // feet this far above the top found
        internal static float MinLift = -0.2f, MaxLift = 0.16f; // room to sit down onto a bare shoulder (~13 cm under the seat); up onto a fur cape (13 cm over it)
        internal static float Below = 0.25f, Above = 0.25f; // the height band around the seat that is searched
        internal static float BellyUp = 0.04f;  // m its belly is over its feet: what stands this high round the feet does not lift it
        internal static float InFrom = 0.10f, InStep = 0.02f, InMost = 0.06f;   // nothing under its feet within this: inwards by steps, this far at most
        internal static float OutStep = 0.015f, OutMost = 0.06f, OutDrop = 0.06f; // off a helmet: outwards by steps, this far at most, while the shoulder under it is no lower than this
        internal static float SigEvery = 0.25f, SigEveryFar = 1.5f, SafeEvery = 5f;
        // its feet and its body over the seat, m at a full-grown body: x outwards (from the head), z forwards
        private static readonly Vector2[] Feet = { new Vector2(0f, 0f), new Vector2(0.025f, 0f), new Vector2(-0.025f, 0f) };
        private static readonly Vector2[] BodyPts = { new Vector2(0f, 0.05f), new Vector2(0f, -0.05f), new Vector2(0f, -0.1f), new Vector2(0.05f, 0.03f), new Vector2(0.05f, -0.03f) };
        // the bird itself for the helmet's check, as two balls over its feet: its body and its head (a full-grown raven on a shoulder)
        internal static float BodyUp = 0.07f, BodyR = 0.06f, HeadUp = 0.14f, HeadFwd = 0.05f, HeadR = 0.04f;

        private sealed class Fit
        {
            public int Sig, Safe;
            public int Post = -1, PostSeen = -1, PostFor; // (the posture it was read in · the one seen last · for how many looks)
            public Animator Anim;
            public float Next, NextSafe;
            public bool[] Dirty = { true, true };
            public float[] Lift = { 0f, 0f }, Side = { 0f, 0f }, Now = { 0f, 0f }, NowAt = { 0f, 0f }; // (Now: where it is on its way to Lift)
            public bool[] Eased = { false, false };
            public string[] How = { "", "" };
        }

        private static readonly Dictionary<Character, Fit> Fits = new Dictionary<Character, Fit>();

        private static Fit Of(Character c, Vector3 seat, bool munin)
        {
            Fit f;
            if (!Fits.TryGetValue(c, out f))
            {
                if (Fits.Count > 40) Prune();
                f = new Fit(); Fits[c] = f;
            }
            if (Time.time >= f.Next)
            {
                f.Next = Time.time + (ReferenceEquals(c, Player.m_localPlayer) ? SigEvery : SigEveryFar);
                int sig = Signature(c);
                if (sig != f.Sig) { f.Sig = sig; f.Dirty[0] = f.Dirty[1] = true; }
                int post = Posture(c, f);
                if (post != f.PostSeen) { f.PostSeen = post; f.PostFor = 0; }
                else if (post != f.Post && ++f.PostFor >= PostChecks) { f.Dirty[0] = f.Dirty[1] = true; } // (it sat down, rose, bent low — and stays so: read as it is now)
                if (Time.time >= f.NextSafe)
                {
                    f.NextSafe = Time.time + SafeEvery;
                    int safe = SafeSignature(c);
                    if (safe != f.Safe) { f.Safe = safe; f.Dirty[0] = f.Dirty[1] = true; }
                }
            }
            int i = munin ? 1 : 0;
            if (f.Dirty[i])
            {
                f.Dirty[i] = false;
                f.Post = Posture(c, f); f.PostFor = 0;
                string how; float side;
                long t0 = Slow.Start();
                f.Lift[i] = Measure(c, seat, munin, out side, out how);
                Slow.End(t0, "shoulder fit (armour measured)");
                f.Side[i] = side; f.How[i] = Mathf.Abs(side) > 0.004f ? Lang.F("{0}; уздовж плеча {1}", how, Cm(side)) : how;
                if (ShoulderLife.LogLandings && Plugin.Instance != null) Plugin.Instance.Log("Shoulder fit: " + (munin ? "Munin" : "Hugin") + " on " + Name(c) + " " + Cm(f.Lift[i]) + ", along the shoulder " + Cm(side) + " (" + how + "); read " + _soupN + " meshes, " + NearHelm.Count / 3 + " helmet triangles by the bird, closed " + _unread);
            }
            return f;
        }

        // how a body holds itself, in wide steps: its hips' height over its feet (on the ground · on a seat or crouched · standing) and its trunk's
        // lean from upright (under PostLean1° · under PostLean2° · over). From its animator's own bones; none to read (no human skeleton): one posture
        internal static int PostChecks = 2;
        internal static float EaseRate = 0.12f; // m a second its height goes to a new reading
        internal static float PostLow = 0.45f, PostMid = 0.75f, PostLean1 = 25f, PostLean2 = 50f;
        private static int Posture(Character c, Fit f)
        {
            if (f.Anim == null) f.Anim = c.GetComponentInChildren<Animator>(); // heavy: once a body (again only when its animator is gone)
            if (f.Anim == null || !f.Anim.isHuman) return 0;
            Transform hips = f.Anim.GetBoneTransform(HumanBodyBones.Hips), head = f.Anim.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || head == null) return 0;
            float size = Mathf.Max(0.1f, c.transform.localScale.x), up = (hips.position.y - c.transform.position.y) / size;
            float lean = Vector3.Angle(head.position - hips.position, Vector3.up);
            return (up < PostLow ? 0 : up < PostMid ? 1 : 2) * 3 + (lean < PostLean1 ? 0 : lean < PostLean2 ? 1 : 2);
        }

        // how far up (or down) from its seat by the bone the raven sits on this body's shoulder now (seat: the seat by the bone, with
        // no Side on it — the reading starts from there)
        internal static float Lift(Character c, bool munin, Vector3 seat)
        {
            if (!On || c == null) return 0f;
            Fit f = Of(c, seat, munin);
            int i = munin ? 1 : 0;
            // read again in another posture, the height may be centimetres off the last: it goes there softly, never in a jump under a sitting bird
            if (!f.Eased[i]) { f.Now[i] = f.Lift[i]; f.Eased[i] = true; }
            else f.Now[i] = Mathf.MoveTowards(f.Now[i], f.Lift[i], Mathf.Max(0f, Time.time - f.NowAt[i]) * EaseRate);
            f.NowAt[i] = Time.time;
            return f.Now[i];
        }

        // … and how far along the shoulder from that seat, as a step in the world (outwards from the head, or inwards): as last read
        internal static Vector3 Side(Character c, bool munin)
        {
            Fit f;
            if (!On || c == null || !Fits.TryGetValue(c, out f)) return Vector3.zero;
            return Out(c, munin) * f.Side[munin ? 1 : 0];
        }

        // outwards along that shoulder, level (from the head: Hugin's to the right, Munin's to the left)
        private static Vector3 Out(Character c, bool munin)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) return Vector3.zero;
            return Vector3.Cross(Vector3.up, fwd.normalized) * (munin ? -1f : 1f);
        }

        private static string Name(Character c) { Player p = c as Player; return p != null ? p.GetPlayerName() : c.name; }

        internal static string Info(Character c)
        {
            Fit f;
            if (c == null || !Fits.TryGetValue(c, out f)) return Lang.T("ще не міряно");
            return Lang.F("Хугін {0} ({1}), Мунін {2} ({3})", Cm(f.Lift[0]), f.How[0], Cm(f.Lift[1]), f.How[1]);
        }

        // this body again (a raven just landed: its pose now)
        internal static void Again(Character c) { Fit f; if (c != null && Fits.TryGetValue(c, out f)) { f.Dirty[0] = f.Dirty[1] = true; } }

        internal static void Remeasure() { foreach (Fit f in Fits.Values) { f.Sig = 0; f.Next = 0f; f.Dirty[0] = f.Dirty[1] = true; } }

        private static string Cm(float v) { return Lang.F("{0} см", ((v >= 0f ? "+" : "") + Mathf.RoundToInt(v * 100f))); }

        private static void Prune()
        {
            List<Character> gone = new List<Character>();
            foreach (Character k in Fits.Keys) if (k == null) gone.Add(k);
            foreach (Character k in gone) Fits.Remove(k);
        }

        // ---------------------------------------------------------------- has the gear changed
        // the game's own numbers of what is drawn on the body (VisEquipment: the hash of each worn thing, the body's model): a few
        // ints, no look over the body. Found once; a game that names them otherwise — the renderers' look alone, at its slow pace
        private static System.Reflection.FieldInfo[] _hashes;
        private static int Signature(Character c)
        {
            if (_hashes == null)
            {
                List<System.Reflection.FieldInfo> l = new List<System.Reflection.FieldInfo>();
                foreach (string n in new[] { "m_currentChestItemHash", "m_currentShoulderItemHash", "m_currentHelmetItemHash", "m_currentLegItemHash", "m_currentUtilityItemHash", "m_currentModelIndex" })
                {
                    System.Reflection.FieldInfo fi = AccessTools.Field(typeof(VisEquipment), n);
                    if (fi != null) l.Add(fi);
                }
                _hashes = l.ToArray();
            }
            VisEquipment vis = c.GetComponent<VisEquipment>();
            int h = Mathf.RoundToInt(c.transform.localScale.x * 100f); // (a grown body: «Звір Муніна»)
            if (vis == null) return h;
            foreach (System.Reflection.FieldInfo fi in _hashes) { object v = fi.GetValue(vis); if (v is int) h = unchecked(h * 31 + (int)v); }
            return h;
        }

        // what is really drawn on it (a thing put on past the game's numbers: a person's clothes, another mod): the safety look
        private static int SafeSignature(Character c)
        {
            int h = 17;
            foreach (Renderer r in Worn(c, null)) h = unchecked(h * 31 + r.GetInstanceID()); // heavy: SafeEvery s a body with a raven on its shoulder
            return h;
        }

        // the body and what is worn on it: not the ravens, not weapons or tools in the hands or on the back; what is on the head (a
        // helmet, a hood on the head's bone) goes into «helmet» when asked for — it never lifts the bird, it only moves it aside
        private static readonly List<Renderer> _worn = new List<Renderer>();
        private static readonly HashSet<Transform> _skip = new HashSet<Transform>();
        private static List<Renderer> Worn(Character c, List<Renderer> helmet)
        {
            _worn.Clear(); _skip.Clear();
            if (helmet != null) helmet.Clear();
            VisEquipment vis = c.GetComponent<VisEquipment>();
            Transform helm = null;
            if (vis != null)
                foreach (System.Reflection.FieldInfo fi in Attach())
                {
                    Transform t = fi.GetValue(vis) as Transform;
                    if (t == null) continue;
                    if (fi.Name == "m_helmet") helm = t; else _skip.Add(t);
                }
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(false)) // heavy: on a change of gear, a landing, the safety look
            {
                if (r == null || !r.enabled) continue;
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                string rn = r.name.ToLowerInvariant();
                if (rn.Contains("hair") || rn.Contains("beard")) continue; // long hair lifted Munin onto it
                if (r.GetComponentInParent<ShoulderPin>() != null || r.GetComponentInParent<WispLife>() != null) continue; // the ravens and Hugin's wisp are not armour
                bool out_ = false, head = false;
                for (Transform t = r.transform; t != null && t != c.transform; t = t.parent)
                {
                    if (_skip.Contains(t)) { out_ = true; break; }
                    if (t == helm || t.name == "Head") { head = true; break; }
                }
                if (out_) continue;
                if (head) { if (helmet != null) helmet.Add(r); }
                else _worn.Add(r);
            }
            return _worn;
        }

        // the hands, the back and the helmet points (found once)
        private static List<System.Reflection.FieldInfo> _attach;
        private static List<System.Reflection.FieldInfo> Attach()
        {
            if (_attach != null) return _attach;
            _attach = new List<System.Reflection.FieldInfo>();
            foreach (string n in new[] { "m_leftHand", "m_rightHand", "m_helmet", "m_backShield", "m_backMelee", "m_backTwohandedMelee", "m_backBow", "m_backTool", "m_backAtgeir" })
            {
                System.Reflection.FieldInfo fi = AccessTools.Field(typeof(VisEquipment), n);
                if (fi != null) _attach.Add(fi);
            }
            return _attach;
        }

        // ---------------------------------------------------------------- the reading: every worn mesh as it is posed now
        // One for both shoulders: asked again in the same frame for the same body, it stands. A skinned mesh is baked as posed now;
        // Unity's bake may or may not carry the scale, so both readings are tried and the one that lands around the body is kept.
        private sealed class Soup
        {
            public string Name = "";
            public bool Helmet;
            public Vector3[] V = new Vector3[0];
            public int Vn;
            public readonly List<int> T = new List<int>();
        }
        private static readonly List<Soup> Soups = new List<Soup>();
        private static readonly List<Renderer> Helmets = new List<Renderer>();
        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<int> Tris = new List<int>();
        private static int _soupN, _unread, _snapFrame = -1;
        private static Character _snapFor;
        private static Mesh _baked;

        // A mesh the game keeps closed (not skinned: most helmets, plates hung on a bone), read all the same: its points and its
        // triangles taken back from the graphics card and picked out of its records by hand. Kept by mesh — a helmet is opened once, not at every change of
        // gear — a few dozen at most; a mesh that would not open is remembered too and not tried again. (A bake through a skinned
        // renderer of our own gave nothing: 2026-11-19, «closed 1», no helmet seen.)
        private sealed class Closed { public Vector3[] V; public int[] T; }
        private static readonly Dictionary<Mesh, Closed> Opened = new Dictionary<Mesh, Closed>();
        private static int Bytes(UnityEngine.Rendering.VertexAttributeFormat f)
        {
            switch (f)
            {
                case UnityEngine.Rendering.VertexAttributeFormat.Float32: case UnityEngine.Rendering.VertexAttributeFormat.UInt32: case UnityEngine.Rendering.VertexAttributeFormat.SInt32: return 4;
                case UnityEngine.Rendering.VertexAttributeFormat.Float16: case UnityEngine.Rendering.VertexAttributeFormat.UNorm16: case UnityEngine.Rendering.VertexAttributeFormat.SNorm16:
                case UnityEngine.Rendering.VertexAttributeFormat.UInt16: case UnityEngine.Rendering.VertexAttributeFormat.SInt16: return 2;
                default: return 1;
            }
        }
        private static Closed Open(Mesh src)
        {
            Closed m;
            if (Opened.TryGetValue(src, out m)) return m; // (null: it would not open)
            if (Opened.Count > 32) Opened.Clear();
            m = null;
            GraphicsBuffer vb = null, ib = null;
            try
            {
                // where a point lies in the first stream's record: plain floats, or this mesh is not read
                int at = -1, off = 0;
                foreach (UnityEngine.Rendering.VertexAttributeDescriptor d in src.GetVertexAttributes())
                {
                    if (d.stream != 0) continue;
                    if (d.attribute == UnityEngine.Rendering.VertexAttribute.Position) { if (d.format == UnityEngine.Rendering.VertexAttributeFormat.Float32 && d.dimension >= 3) at = off; break; }
                    off += Bytes(d.format) * d.dimension;
                }
                if (at >= 0)
                {
                    // heavy: once a mesh — two reads back from the graphics card
                    vb = src.GetVertexBuffer(0);
                    ib = src.GetIndexBuffer();
                    int vs = vb.stride, vn = vb.count, isz = ib.stride;
                    byte[] vd = new byte[vs * vn], id = new byte[isz * ib.count];
                    vb.GetData(vd);
                    ib.GetData(id);
                    Closed c = new Closed { V = new Vector3[vn] };
                    for (int i = 0; i < vn; i++)
                    {
                        int o = i * vs + at;
                        c.V[i] = new Vector3(BitConverter.ToSingle(vd, o), BitConverter.ToSingle(vd, o + 4), BitConverter.ToSingle(vd, o + 8));
                    }
                    List<int> tris = new List<int>();
                    for (int s = 0; s < src.subMeshCount; s++)
                    {
                        UnityEngine.Rendering.SubMeshDescriptor sd = src.GetSubMesh(s);
                        if (sd.topology != MeshTopology.Triangles) continue;
                        for (int k = 0; k < sd.indexCount; k++)
                        {
                            int o = (sd.indexStart + k) * isz;
                            tris.Add((isz == 2 ? BitConverter.ToUInt16(id, o) : (int)BitConverter.ToUInt32(id, o)) + sd.baseVertex);
                        }
                    }
                    c.T = tris.ToArray();
                    if (vn > 0 && c.T.Length >= 3) m = c;
                }
            }
            catch (Exception e)
            {
                m = null;
                if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder fit: the closed mesh " + src.name + " could not be opened: " + e.Message);
            }
            finally { if (vb != null) vb.Dispose(); if (ib != null) ib.Dispose(); }
            Opened[src] = m;
            return m;
        }

        private static void Snapshot(Character c, Vector3 about)
        {
            if (ReferenceEquals(_snapFor, c) && _snapFrame == Time.frameCount) return;
            _snapFor = c; _snapFrame = Time.frameCount; _soupN = 0; _unread = 0;
            if (_baked == null) { _baked = new Mesh(); _baked.hideFlags = HideFlags.HideAndDontSave; }
            List<Renderer> worn = Worn(c, Helmets);
            for (int k = 0; k < worn.Count + Helmets.Count; k++)
            {
                bool helmet = k >= worn.Count;
                Renderer r = helmet ? Helmets[k - worn.Count] : worn[k];
                try
                {
                    SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                    MeshFilter mf = smr == null ? r.GetComponent<MeshFilter>() : null;
                    if (smr != null ? smr.sharedMesh == null : (mf == null || mf.sharedMesh == null)) continue;
                    // a mesh that is not skinned and that the game keeps closed (most helmets: the first run of this reading, 2026-11-19, saw no
                    // horned helmet at all): a copy of ours that can be read (Open)
                    Closed open = smr == null && !mf.sharedMesh.isReadable ? Open(mf.sharedMesh) : null;
                    if (smr == null && !mf.sharedMesh.isReadable && open == null) { _unread++; continue; }
                    for (int pass = 0; pass < (smr != null ? 3 : 1); pass++)
                    {
                        Matrix4x4 m;
                        Mesh mesh;
                        if (smr != null)
                        {
                            _baked.Clear();
                            smr.BakeMesh(_baked, pass == 1); // works for a mesh we may not read ourselves (the bake is ours)
                            if (_baked.vertexCount == 0) { _unread++; break; }
                            mesh = _baked;
                            m = pass < 2 ? Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one) : smr.transform.localToWorldMatrix;
                        }
                        else { mesh = mf.sharedMesh; m = r.transform.localToWorldMatrix; }
                        Verts.Clear();
                        if (open != null) Verts.AddRange(open.V); else mesh.GetVertices(Verts);
                        if (_soupN == Soups.Count) Soups.Add(new Soup());
                        Soup s = Soups[_soupN];
                        if (s.V.Length < Verts.Count) s.V = new Vector3[Verts.Count + Verts.Count / 4];
                        Vector3 lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
                        for (int i = 0; i < Verts.Count; i++) { Vector3 v = m.MultiplyPoint3x4(Verts[i]); s.V[i] = v; lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
                        float tall = hi.y - lo.y;
                        bool around = tall > 0.03f && tall < 3f && about.y > lo.y - 0.3f && about.y < hi.y + 0.5f && about.x > lo.x - 0.5f && about.x < hi.x + 0.5f && about.z > lo.z - 0.5f && about.z < hi.z + 0.5f;
                        if (!around && smr != null && pass < 2) continue; // not where the body is (or not body-sized): the next reading
                        s.Name = r.name; s.Helmet = helmet; s.Vn = Verts.Count; s.T.Clear();
                        if (open != null) s.T.AddRange(open.T);
                        else for (int sm = 0; sm < mesh.subMeshCount; sm++) { Tris.Clear(); mesh.GetTriangles(Tris, sm); s.T.AddRange(Tris); }
                        _soupN++;
                        break;
                    }
                }
                catch (Exception e) { _unread++; if (ShoulderLife.LogLandings && Plugin.Instance != null) Plugin.Instance.Log("Shoulder fit: " + r.name + " could not be read: " + e.Message); }
            }
            Verts.Clear(); Tris.Clear();
        }

        // the triangles by one shoulder, picked out of the reading once a measure: what the feet and the body may stand on (within
        // NearFlat m of the seat, level), and the helmet's by the bird
        private const float NearFlat = 0.22f, NearHelmet = 0.3f;
        private static readonly List<Vector3> Near = new List<Vector3>(), NearHelm = new List<Vector3>();
        private static readonly List<int> NearOf = new List<int>();
        private static void Pick(Vector3 seat, float size)
        {
            Near.Clear(); NearOf.Clear(); NearHelm.Clear();
            float rf = NearFlat * size, rh = NearHelmet * size;
            Vector3 bird = seat + Vector3.up * (BodyUp * size);
            for (int k = 0; k < _soupN; k++)
            {
                Soup s = Soups[k];
                for (int t = 0; t + 2 < s.T.Count; t += 3)
                {
                    int ia = s.T[t], ib = s.T[t + 1], ic = s.T[t + 2];
                    if (ia >= s.Vn || ib >= s.Vn || ic >= s.Vn) continue;
                    Vector3 a = s.V[ia], b = s.V[ib], c = s.V[ic];
                    if (s.Helmet)
                    {
                        if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > bird.x + rh || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < bird.x - rh) continue;
                        if (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > bird.z + rh || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < bird.z - rh) continue;
                        if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > bird.y + rh || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < bird.y - rh) continue;
                        NearHelm.Add(a); NearHelm.Add(b); NearHelm.Add(c);
                        continue;
                    }
                    if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > seat.x + rf || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < seat.x - rf) continue;
                    if (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > seat.z + rf || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < seat.z - rf) continue;
                    Near.Add(a); Near.Add(b); Near.Add(c); NearOf.Add(k);
                }
            }
        }

        // straight down through a point: does this triangle lie under it, and at what height (in the ground plane)
        private static bool Under(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out float y)
        {
            y = 0f;
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-10f) return false;
            float w1 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / d;
            float w2 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / d;
            float w3 = 1f - w1 - w2;
            if (w1 < 0f || w2 < 0f || w3 < 0f) return false;
            y = w1 * a.y + w2 * b.y + w3 * c.y;
            return true;
        }

        // the highest surface under a point, within the band round the seat's height; false: none
        private static bool Top(Vector3 p, float seatY, float size, out float top, out int soup)
        {
            top = float.MinValue; soup = -1;
            float lo = seatY - Below * size, hi = seatY + Above * size;
            for (int i = 0, n = 0; i + 2 < Near.Count; i += 3, n++)
            {
                float y;
                if (!Under(p, Near[i], Near[i + 1], Near[i + 2], out y) || y < lo || y > hi || y <= top) continue;
                top = y; soup = NearOf[n];
            }
            return soup >= 0;
        }

        // the highest under any of these points of the bird (its feet, or its body) standing at «seat»
        private static bool TopOf(Vector2[] pts, Vector3 seat, Vector3 outw, Vector3 fwd, float size, out float top, out int soup)
        {
            top = float.MinValue; soup = -1;
            foreach (Vector2 o in pts)
            {
                float y; int s;
                if (Top(seat + (outw * o.x + fwd * o.y) * size, seat.y, size, out y, out s) && y > top) { top = y; soup = s; }
            }
            return soup >= 0;
        }

        // the bird standing at «seat»: how far up or down from it — on what is under its feet, and no lower than what stands round
        // them lets its body be (less its legs' height); NaN: nothing there to stand on
        private static float Height(Character c, Vector3 seat, Vector3 outw, Vector3 fwd, float size, out string how)
        {
            float feet, body; int fs, bs;
            bool f = TopOf(Feet, seat, outw, fwd, size, out feet, out fs), b = TopOf(BodyPts, seat, outw, fwd, size, out body, out bs);
            how = "";
            if (!f && !b) return float.NaN;
            float y = f ? feet : float.MinValue; int on = fs; bool byBody = false;
            if (b && body - BellyUp * size > y) { y = body - BellyUp * size; on = bs; byBody = true; }
            string name = Soups[on].Name;
            how = Lang.F("поверхня під вороном: {0} {1} см{2}", name, Mathf.RoundToInt((y - seat.y) * 100f), byBody ? Lang.T(" (під тілом)") : "");
            return Mathf.Clamp(y + Margin - seat.y - Sink(c, name), MinLift * size, MaxLift * size);
        }

        // how much of the helmet is in the bird standing there (its triangles that come into its body's or its head's ball)
        private static int HelmetIn(Vector3 feet, Vector3 fwd, float size)
        {
            if (NearHelm.Count == 0) return 0;
            Vector3 body = feet + Vector3.up * (BodyUp * size), head = feet + Vector3.up * (HeadUp * size) + fwd * (HeadFwd * size);
            float br = BodyR * size, hr = HeadR * size;
            int n = 0;
            for (int i = 0; i + 2 < NearHelm.Count; i += 3)
                if (Within(body, br, NearHelm[i], NearHelm[i + 1], NearHelm[i + 2]) || Within(head, hr, NearHelm[i], NearHelm[i + 1], NearHelm[i + 2])) n++;
            return n;
        }

        // a triangle comes within r of a point (the nearest point of the triangle to it: a horn is a few long triangles, their
        // corners far outside the bird while their middle goes through it)
        private static bool Within(Vector3 p, float r, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            Vector3 q;
            if (d1 <= 0f && d2 <= 0f) q = a;
            else
            {
                Vector3 bp = p - b;
                float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3) q = b;
                else
                {
                    float vc = d1 * d4 - d3 * d2;
                    if (vc <= 0f && d1 >= 0f && d3 <= 0f) q = a + ab * (d1 / (d1 - d3));
                    else
                    {
                        Vector3 cp = p - c;
                        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                        if (d6 >= 0f && d5 <= d6) q = c;
                        else
                        {
                            float vb = d5 * d2 - d1 * d6;
                            if (vb <= 0f && d2 >= 0f && d6 <= 0f) q = a + ac * (d2 / (d2 - d6));
                            else
                            {
                                float va = d3 * d6 - d5 * d4;
                                if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) q = b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
                                else { float den = 1f / (va + vb + vc); q = a + ab * (vb * den) + ac * (vc * den); }
                            }
                        }
                    }
                }
            }
            return (p - q).sqrMagnitude < r * r;
        }

        private static float Measure(Character c, Vector3 seat, bool munin, out float side, out string how)
        {
            side = 0f;
            float size = Mathf.Max(0.1f, c.transform.localScale.x);
            Vector3 outw = Out(c, munin), fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
            Snapshot(c, seat);
            Pick(seat, size);
            float lift = Height(c, seat, outw, fwd, size, out how);
            // past the shoulder's edge (nothing under its feet, or only far below: a narrow body, a cloak hanging off it): inwards
            float feet; int fs;
            bool on = TopOf(Feet, seat, outw, fwd, size, out feet, out fs) && feet - seat.y >= -InFrom * size;
            if (!on)
                for (float s = InStep; s <= InMost + 0.001f; s += InStep)
                {
                    Vector3 at = seat - outw * (s * size);
                    if (!TopOf(Feet, at, outw, fwd, size, out feet, out fs) || feet - seat.y < -InFrom * size) continue;
                    side = -s * size; lift = Height(c, at, outw, fwd, size, out how); on = true;
                    break;
                }
            if (float.IsNaN(lift))
            {
                how = Lang.F("за типом одягу{0}", (_unread > 0 ? Lang.F(", моделі закриті: {0}", _unread) : ""));
                side = 0f;
                return ByGear(c) * size;
            }
            // the helmet in the bird (horns, antlers, a brim): outwards to where the least of it is, while the shoulder still holds it
            if (NearHelm.Count > 0 && on)
            {
                Vector3 at = seat + outw * side;
                int least = HelmetIn(at + Vector3.up * (lift - Shoulder.PerchDrop), fwd, size);
                float feet0 = feet;
                if (least > 0)
                {
                    int was = least;
                    for (float s = OutStep; s <= OutMost + 0.001f && least > 0; s += OutStep)
                    {
                        Vector3 p = at + outw * (s * size);
                        float f2; int s2; string h2;
                        if (!TopOf(Feet, p, outw, fwd, size, out f2, out s2) || f2 < feet0 - OutDrop * size) break; // (the shoulder ends here)
                        float l2 = Height(c, p, outw, fwd, size, out h2);
                        if (float.IsNaN(l2)) break;
                        int n = HelmetIn(p + Vector3.up * (l2 - Shoulder.PerchDrop), fwd, size);
                        if (n >= least) continue;
                        least = n; side = (p - seat).magnitude * Mathf.Sign(Vector3.Dot(p - seat, outw)); lift = l2; how = h2;
                    }
                    how += Lang.F("; шолом у птасі: {0} → {1}", was, least);
                }
            }
            return lift;
        }

        // on armour the feet sink in a little: a bare shoulder is one known surface, armour is any of hundreds (other
        // mods' too) with collars and plates the reading may land on — a bird a touch into the leather looks right, one
        // hanging over it does not (the user, 2026-10-24; again 2026-11-19)
        internal static float ArmourSink = 0.03f;
        private static float Sink(Character c, string on)
        {
            VisEquipment vis = c != null ? c.GetComponent<VisEquipment>() : null;
            return vis != null && vis.m_bodyModel != null && on == vis.m_bodyModel.name ? 0f : ArmourSink;
        }

        // no surface could be read (a mod's armour with closed meshes): a middle value by what is worn
        internal static float NakedLift = 0f, TunicLift = 0.015f, LightLift = 0.025f, HeavyLift = 0.04f, CapeLift = 0.01f;
        internal static float HeavyArmor = 20f; // chest armour from this on counts as heavy
        private static readonly System.Reflection.FieldInfo ChestField = AccessTools.Field(typeof(Humanoid), "m_chestItem");
        private static readonly System.Reflection.FieldInfo CapeField = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");
        private static float ByGear(Character c)
        {
            ItemDrop.ItemData chest = null, cape = null;
            if (c is Humanoid)
            {
                if (ChestField != null) chest = ChestField.GetValue(c) as ItemDrop.ItemData;
                if (CapeField != null) cape = CapeField.GetValue(c) as ItemDrop.ItemData;
            }
            float lift = chest == null ? NakedLift : chest.m_shared.m_armor < 8f ? TunicLift : chest.m_shared.m_armor < HeavyArmor ? LightLift : HeavyLift;
            if (cape != null) lift += CapeLift;
            if (chest != null || cape != null) lift -= ArmourSink;
            return Mathf.Clamp(lift, MinLift, MaxLift);
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // The things a look hangs on the raven (Look.Thing, tab «Фарби» → «Речі»): anything the Workshop's catalog knows
    // (a game piece, its living effect, an asset) or a whole Workshop model. Each sits on one bone, in the Workshop's
    // frame there (RavenGear.Holder: the raven's own axes as it sits, sizes of a shoulder raven), so it goes with that
    // bone: the head's turns, the wings' beats, the legs. «Пара»: the same mirrored on the bone of the other side.
    internal static class LookThings
    {
        internal static readonly string[] BoneIds = { "head", "beak", "neck", "back", "hip", "tail", "wingl", "wingr", "tipl", "tipr", "legl", "legr" };
        internal static readonly string[] BoneNames = { "Голова", "Дзьоб", "Шия", "Спина", "Таз", "Хвіст", "Ліве крило", "Праве крило", "Лівий кінчик", "Правий кінчик", "Ліва лапа", "Права лапа" };
        private static readonly string[] Bones = { "Neck", "Jaw", "Spine1", "Spine", "Hip", "Tail", "Wing1.l", "Wing1.r", "wing3.l", "wing3.r", "Leg3.l", "Leg3.r" };
        internal const string HolderName = "RavenWatch_LookThing";
        internal const string ModelPrefix = "model:";

        internal static string BoneOf(string id) { int i = System.Array.IndexOf(BoneIds, id); return i >= 0 ? Bones[i] : null; }
        internal static string NameOf(string id) { int i = System.Array.IndexOf(BoneIds, id); return i >= 0 ? BoneNames[i] : id; }

        // the bone of the other side (wings, wing tips, legs); null when it has none
        internal static string PairOf(string id)
        {
            if (id.EndsWith("l")) { string o = id.Substring(0, id.Length - 1) + "r"; if (System.Array.IndexOf(BoneIds, o) >= 0) return o; }
            if (id.EndsWith("r")) { string o = id.Substring(0, id.Length - 1) + "l"; if (System.Array.IndexOf(BoneIds, o) >= 0) return o; }
            return null;
        }

        // marks one hung copy: which thing of the look it is (the test window moves it without building it again)
        internal sealed class Tag : MonoBehaviour { internal int Index; internal bool Mirror; }

        // hangs every thing of the look on this raven (what it had from a look before goes first)
        internal static void Hang(GameObject raven, Look look, bool munin, int layer)
        {
            Unhang(raven);
            if (raven == null || look == null) return;
            for (int i = 0; i < look.Things.Count; i++)
            {
                Look.Thing t = look.Things[i];
                One(raven, t, i, munin, layer, t.Bone, false);
                string other = t.Pair ? PairOf(t.Bone) : null;
                if (other != null) One(raven, t, i, munin, layer, other, true);
            }
        }

        internal static void Unhang(GameObject raven)
        {
            if (raven == null) return;
            List<GameObject> gone = new List<GameObject>();
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == HolderName) gone.Add(t.gameObject);
            foreach (GameObject g in gone) { g.transform.SetParent(null, false); Object.Destroy(g); }
        }

        private static void One(GameObject raven, Look.Thing t, int index, bool munin, int layer, string boneId, bool mirror)
        {
            string bone = BoneOf(boneId);
            Transform holder = bone != null ? RavenGear.Holder(raven, munin, bone, HolderName) : null;
            if (holder == null) { Perches.Log("look thing " + t.Src + ": no bone " + boneId); return; }
            Transform at = holder;
            if (mirror)
            {
                GameObject flip = new GameObject("mirror");
                flip.transform.SetParent(holder, false);
                flip.transform.localScale = new Vector3(-1f, 1f, 1f);
                at = flip.transform;
            }
            GameObject inner = new GameObject("thing");
            inner.transform.SetParent(at, false);
            Place(inner.transform, t);
            Tag tag = inner.AddComponent<Tag>();
            tag.Index = index; tag.Mirror = mirror;
            Build(inner.transform, t);
            foreach (Transform x in holder.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = layer >= 0 ? layer : raven.layer;
            foreach (Renderer rd in holder.GetComponentsInChildren<Renderer>(true)) { if (rd is ParticleSystemRenderer) continue; rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.receiveShadows = false; }
        }

        internal static void Place(Transform inner, Look.Thing t)
        {
            inner.localPosition = t.Pos;
            inner.localRotation = Quaternion.Euler(t.Rot);
            inner.localScale = t.Scale;
        }

        // what it is, built under its place: one catalog piece, or every part of a Workshop model (its tint over them all)
        private static void Build(Transform inner, Look.Thing t)
        {
            if (t.Src.StartsWith(ModelPrefix, System.StringComparison.Ordinal))
            {
                WsModel m = Model(t.Src.Substring(ModelPrefix.Length));
                if (m == null) { Perches.Log("look thing: no Workshop model " + t.Src); return; }
                foreach (WsPart p in m.Parts)
                {
                    if (WsFiles.Special(p.Src)) continue;
                    WsPart q = p;
                    if (t.Tinted) { q = p.Clone(); q.Tinted = true; q.Tint = t.Tint; }
                    Perches.Part(inner, q, false);
                }
                return;
            }
            WsPart one = new WsPart { Src = t.Src, Tinted = t.Tinted, Tint = t.Tint, Fx = t.Fx };
            Perches.Part(inner, one, false);
        }

        // a Workshop model by its file's name, read again when the file changes
        private sealed class Read { public System.DateTime At; public WsModel Model; }
        private static readonly Dictionary<string, Read> Models = new Dictionary<string, Read>();

        internal static WsModel Model(string name)
        {
            string file = WsFiles.FileFor(name);
            if (!File.Exists(file)) return null;
            System.DateTime at = File.GetLastWriteTimeUtc(file);
            Read r;
            if (Models.TryGetValue(file, out r) && r.At == at) return r.Model;
            WsModel m = WsFiles.Load(file);
            Models[file] = new Read { At = at, Model = m };
            return m;
        }

        // a new thing's first size: a game piece made about as big as the raven's head; a Workshop model as it was made
        // (it was built round the raven already)
        private static readonly Dictionary<string, float> Firsts = new Dictionary<string, float>();

        internal static float FirstScale(string src)
        {
            float k;
            if (Firsts.TryGetValue(src, out k)) return k;
            k = Measure(src);
            Firsts[src] = k;
            return k;
        }

        private static float Measure(string src)
        {
            if (src.StartsWith(ModelPrefix, System.StringComparison.Ordinal)) return 1f;
            GameObject tpl = WsCatalog.Template(src);
            if (tpl == null) return 1f;
            Bounds b = new Bounds();
            bool any = false;
            foreach (MeshFilter mf in tpl.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 to = tpl.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Vector3 c = to.MultiplyPoint3x4(mb.center), e = mb.extents;
                Vector3 ext = new Vector3(
                    Mathf.Abs(to.m00) * e.x + Mathf.Abs(to.m01) * e.y + Mathf.Abs(to.m02) * e.z,
                    Mathf.Abs(to.m10) * e.x + Mathf.Abs(to.m11) * e.y + Mathf.Abs(to.m12) * e.z,
                    Mathf.Abs(to.m20) * e.x + Mathf.Abs(to.m21) * e.y + Mathf.Abs(to.m22) * e.z);
                Bounds one = new Bounds(c, ext * 2f);
                if (any) b.Encapsulate(one); else { b = one; any = true; }
            }
            float size = any ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : 0f;
            return size > 0.0001f ? Mathf.Clamp(HeadSize / size, 0.005f, 20f) : 1f;
        }

        internal static float HeadSize = 0.08f;
    }
}
#endif

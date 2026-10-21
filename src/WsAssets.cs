#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Ассети» of the Workshop: outside models (FBX files) to try out next to the game's pieces.
    // Every folder in config/RavenWatch/assets/ is one set (its own button in the tab «Ассети»); the files in it are read
    // by the mod itself (binary FBX 7.x: meshes, material colours, a diffuse texture beside the file), only when a part
    // asks for them. Parts name them "asset/<set>/<file>" (spaces turned into '_'), so a model file stays one word per part.
    // The files the mod's own models use are built into the DLL (assets/fbx/<file>.fbx → RavenWatch.fbx.<file>.fbx,
    // found by the file's name); any other shows nothing where the files are missing. A file in the folder wins.
    internal static class WsAssets
    {
        internal const string Prefix = "asset/";

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "assets"); } }

        internal static readonly List<string> Sets = new List<string>();
        private static readonly Dictionary<string, string> FileOf = new Dictionary<string, string>(); // src → the .fbx path
        private static readonly Dictionary<string, GameObject> Looks = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static bool _scanned; // the folder looked through once (a release has no folder: the built-in files then)
        internal static readonly List<string> NoTexture = new List<string>(); // textures named by the files but not found

        internal static bool Is(string src) { return src != null && src.StartsWith(Prefix, StringComparison.Ordinal); }

        private static string Word(string s) { return s.Replace(' ', '_'); }

        // the folder looked through again: the sets and their files as catalog entries (nothing is read yet)
        internal static List<WsCatalog.Entry> Scan()
        {
            List<WsCatalog.Entry> list = new List<WsCatalog.Entry>();
            Sets.Clear(); FileOf.Clear(); Failed.Clear();
            _scanned = true;
            try
            {
                if (!Directory.Exists(Dir)) return list;
                string[] dirs = Directory.GetDirectories(Dir);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                foreach (string d in dirs)
                {
                    string set = Path.GetFileName(d);
                    string[] files = Directory.GetFiles(d, "*.fbx", SearchOption.AllDirectories);
                    if (files.Length == 0) continue;
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    Sets.Add(set);
                    foreach (string f in files)
                    {
                        string rel = f.Substring(d.Length).TrimStart('\\', '/');
                        rel = rel.Substring(0, rel.Length - 4).Replace('\\', '/');
                        WsCatalog.Entry e = new WsCatalog.Entry();
                        e.Name = Prefix + Word(set) + "/" + Word(rel);
                        e.Label = Path.GetFileNameWithoutExtension(f).Replace('_', ' ');
                        e.Mod = set;
                        e.Cat = WsCatalog.CatAsset;
                        e.Search = (e.Name + " " + e.Label + " " + set).ToLowerInvariant();
                        FileOf[e.Name] = f;
                        list.Add(e);
                    }
                }
            }
            catch (Exception ex) { WsFiles.Log("assets: " + ex.Message); }
            WsFiles.Log("assets: " + list.Count + " files in " + Sets.Count + " sets (" + Dir + ")");
            return list;
        }

        // the look of one file, made once and kept switched off (parts are clones of it, as with the game's pieces)
        internal static GameObject Template(string src)
        {
            GameObject g;
            if (Looks.TryGetValue(src, out g) && g != null) return g;
            if (Failed.Contains(src)) return null;
            string file;
            if (!FileOf.TryGetValue(src, out file) && !_scanned) Scan();
            byte[] data = null;
            if (!FileOf.TryGetValue(src, out file)) { data = BuiltIn(src); if (data == null) { Failed.Add(src); return null; } }
            try
            {
                long t0 = Slow.Start();
                g = data != null ? FbxLook.Make(data, null, src) : FbxLook.Make(File.ReadAllBytes(file), Path.GetDirectoryName(file), src);
                Slow.End(t0, "asset " + src);
            }
            catch (Exception ex) { WsFiles.Log("asset " + file + ": " + ex.Message); g = null; }
            if (g == null) { Failed.Add(src); return null; }
            try { GlowSheet(g, src, file); } catch (Exception ex) { WsFiles.Log("asset " + src + " glow: " + ex.Message); }
            Looks[src] = g;
            return g;
        }

        // «<Name>_glow.png» beside the model (tools/glow_map.js): what shines in it — painted runes, a lantern's glass —
        // laid on as the glow of every material that has none of its own (the file's own glow map wins)
        internal static float GlowPower = 2f;
        private static void GlowSheet(GameObject g, string src, string file)
        {
            byte[] png = null;
            if (file != null)
            {
                string side = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "_glow.png");
                if (File.Exists(side)) png = File.ReadAllBytes(side);
            }
            if (png == null)
            {
                int slash = src.LastIndexOf('/');
                using (Stream st = typeof(WsAssets).Assembly.GetManifestResourceStream("RavenWatch.fbx." + src.Substring(slash + 1) + "_glow.png"))
                    if (st != null) using (MemoryStream ms = new MemoryStream()) { st.CopyTo(ms); png = ms.ToArray(); }
            }
            if (png == null) return;
            Texture2D t = FbxLook.Picture(png, src + "_glow");
            if (t == null) return;
            int n = 0;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null || !m.HasProperty("_EmissionMap") || m.GetTexture("_EmissionMap") != null) continue;
                    m.SetTexture("_EmissionMap", t);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white * GlowPower); // (over 1: it shines, not just shows)
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    n++;
                }
            WsFiles.Log("asset " + src + ": glow sheet on " + n + " material(s)");
        }

        // a file built into the mod, by the part's last word (asset/<set>/Bag → RavenWatch.fbx.Bag.fbx); null: none
        private static byte[] BuiltIn(string src)
        {
            try
            {
                int slash = src.LastIndexOf('/');
                string res = "RavenWatch.fbx." + src.Substring(slash + 1) + ".fbx";
                using (Stream st = typeof(WsAssets).Assembly.GetManifestResourceStream(res))
                {
                    if (st == null) return null;
                    using (MemoryStream ms = new MemoryStream()) { st.CopyTo(ms); return ms.ToArray(); }
                }
            }
            catch (Exception ex) { WsFiles.Log("asset " + src + " (built in): " + ex.Message); return null; }
        }

        // the main material: the biggest mesh's first one
        internal static Material MainMaterial(string src)
        {
            GameObject g = Template(src);
            if (g == null) return null;
            Material m = null; float best = -1f;
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                float size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.sqrMagnitude : 0f;
                if (size > best && r.sharedMaterials.Length > 0 && r.sharedMaterials[0] != null) { best = size; m = r.sharedMaterials[0]; }
            }
            return m;
        }

        // which assets the saved models use (so the unused ones can go later)
        internal static Dictionary<string, int> Used()
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            try
            {
                foreach (string f in WsFiles.List())
                    foreach (string l in File.ReadAllLines(f))
                    {
                        string t = l.Trim();
                        if (!t.StartsWith("part " + Prefix, StringComparison.Ordinal)) continue;
                        int sp = t.IndexOf(' ', 5);
                        string src = sp > 0 ? t.Substring(5, sp - 5) : t.Substring(5);
                        int n; d.TryGetValue(src, out n); d[src] = n + 1;
                    }
            }
            catch (Exception ex) { WsFiles.Log("assets used: " + ex.Message); }
            return d;
        }
    }

    // ---------------------------------------------------------------- binary FBX → meshes

    internal class FbxNode
    {
        internal string Name;
        internal readonly List<object> Props = new List<object>();
        internal readonly List<FbxNode> Kids = new List<FbxNode>();

        internal FbxNode Kid(string name) { foreach (FbxNode k in Kids) if (k.Name == name) return k; return null; }
        internal object P(int i) { return i < Props.Count ? Props[i] : null; }
        internal string S(int i) { string s = P(i) as string; if (s == null) return ""; int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s; }
        internal long L(int i) { object o = P(i); return o is long ? (long)o : o is int ? (int)o : 0L; }
        internal double D(int i) { object o = P(i); return o is double ? (double)o : o is float ? (float)o : o is int ? (int)o : o is long ? (long)o : 0.0; }
        internal string KidS(string name) { FbxNode k = Kid(name); return k != null ? k.S(0) : ""; }

        // Properties70 → name → the node (values from Props[4])
        internal Dictionary<string, FbxNode> Props70()
        {
            Dictionary<string, FbxNode> d = new Dictionary<string, FbxNode>();
            FbxNode p = Kid("Properties70");
            if (p != null) foreach (FbxNode k in p.Kids) if (k.Name == "P") d[k.S(0)] = k;
            return d;
        }
    }

    internal static class FbxReader
    {
        internal static List<FbxNode> Read(byte[] b)
        {
            if (b.Length < 27 || Encoding.ASCII.GetString(b, 0, 18) != "Kaydara FBX Binary") throw new Exception("not a binary FBX (ASCII FBX is not read)");
            uint ver = BitConverter.ToUInt32(b, 23);
            int pos = 27;
            List<FbxNode> top = new List<FbxNode>();
            while (pos < b.Length - 32)
            {
                FbxNode n = Node(b, ref pos, ver >= 7500);
                if (n == null) break;
                top.Add(n);
            }
            return top;
        }

        private static FbxNode Node(byte[] b, ref int pos, bool big)
        {
            long end, count;
            if (big) { end = BitConverter.ToInt64(b, pos); count = BitConverter.ToInt64(b, pos + 8); pos += 24; }
            else { end = BitConverter.ToUInt32(b, pos); count = BitConverter.ToUInt32(b, pos + 4); pos += 12; }
            int nameLen = b[pos++];
            if (end == 0) return null;
            FbxNode n = new FbxNode();
            n.Name = Encoding.ASCII.GetString(b, pos, nameLen); pos += nameLen;
            for (long i = 0; i < count; i++) n.Props.Add(Prop(b, ref pos));
            while (pos < end)
            {
                FbxNode k = Node(b, ref pos, big);
                if (k == null) break;
                n.Kids.Add(k);
            }
            pos = (int)end;
            return n;
        }

        private static object Prop(byte[] b, ref int pos)
        {
            char t = (char)b[pos++];
            switch (t)
            {
                case 'Y': pos += 2; return (int)BitConverter.ToInt16(b, pos - 2);
                case 'C': pos += 1; return b[pos - 1] != 0;
                case 'I': pos += 4; return BitConverter.ToInt32(b, pos - 4);
                case 'F': pos += 4; return (double)BitConverter.ToSingle(b, pos - 4);
                case 'D': pos += 8; return BitConverter.ToDouble(b, pos - 8);
                case 'L': pos += 8; return BitConverter.ToInt64(b, pos - 8);
                case 'S':
                case 'R':
                {
                    int len = BitConverter.ToInt32(b, pos); pos += 4;
                    object o;
                    if (t == 'S') o = Encoding.UTF8.GetString(b, pos, len);
                    else { byte[] raw = new byte[len]; Buffer.BlockCopy(b, pos, raw, 0, len); o = raw; }
                    pos += len;
                    return o;
                }
                case 'f': case 'd': case 'i': case 'l': case 'b':
                {
                    int n = BitConverter.ToInt32(b, pos), enc = BitConverter.ToInt32(b, pos + 4), len = BitConverter.ToInt32(b, pos + 8);
                    pos += 12;
                    byte[] data;
                    int at = 0;
                    if (enc == 1) { data = Inflate(b, pos, len); }
                    else { data = b; at = pos; }
                    pos += len;
                    if (t == 'f') { double[] a = new double[n]; for (int k = 0; k < n; k++) a[k] = BitConverter.ToSingle(data, at + k * 4); return a; }
                    if (t == 'd') { double[] a = new double[n]; for (int k = 0; k < n; k++) a[k] = BitConverter.ToDouble(data, at + k * 8); return a; }
                    if (t == 'i') { int[] a = new int[n]; for (int k = 0; k < n; k++) a[k] = BitConverter.ToInt32(data, at + k * 4); return a; }
                    if (t == 'l') { long[] a = new long[n]; for (int k = 0; k < n; k++) a[k] = BitConverter.ToInt64(data, at + k * 8); return a; }
                    { byte[] a = new byte[n]; Buffer.BlockCopy(data, at, a, 0, n); return a; }
                }
                default: throw new Exception("unknown property type '" + t + "'");
            }
        }

        // zlib: 2 header bytes, then deflate
        private static byte[] Inflate(byte[] b, int pos, int len)
        {
            using (MemoryStream src = new MemoryStream(b, pos + 2, len - 2))
            using (DeflateStream z = new DeflateStream(src, CompressionMode.Decompress))
            using (MemoryStream dst = new MemoryStream())
            {
                byte[] buf = new byte[65536];
                int r;
                while ((r = z.Read(buf, 0, buf.Length)) > 0) dst.Write(buf, 0, r);
                return dst.ToArray();
            }
        }
    }

    internal static class FbxLook
    {
        private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
        private static Shader _shader;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        private class Obj { internal FbxNode N; internal string Kind; }

        // dir: the file's folder, where its texture may lie (null: a file built into the mod, its texture only inside it)
        internal static GameObject Make(byte[] bytes, string dir, string src)
        {
            List<FbxNode> top = FbxReader.Read(bytes);
            FbxNode objects = null, conns = null, global = null;
            foreach (FbxNode n in top)
            {
                if (n.Name == "Objects") objects = n;
                else if (n.Name == "Connections") conns = n;
                else if (n.Name == "GlobalSettings") global = n;
            }
            if (objects == null) throw new Exception("no Objects");

            Dictionary<long, Obj> byId = new Dictionary<long, Obj>();
            foreach (FbxNode k in objects.Kids) { Obj o = new Obj(); o.N = k; o.Kind = k.Name; byId[k.L(0)] = o; }

            // connections: who hangs on whom, in file order (a model's materials keep their slot order)
            Dictionary<long, long> parentModel = new Dictionary<long, long>();
            Dictionary<long, FbxNode> geomOf = new Dictionary<long, FbxNode>();
            Dictionary<long, List<FbxNode>> matsOf = new Dictionary<long, List<FbxNode>>();
            Dictionary<long, FbxNode> texOf = new Dictionary<long, FbxNode>(); // material → its diffuse texture
            Dictionary<long, FbxNode> videoOf = new Dictionary<long, FbxNode>(); // texture → its video (file, maybe embedded)
            Dictionary<long, FbxNode> normOf = new Dictionary<long, FbxNode>(); // material → its normal map
            Dictionary<long, FbxNode> emitOf = new Dictionary<long, FbxNode>(); // material → its glow map (runes, eyes that shine)
            if (conns != null)
                foreach (FbxNode c in conns.Kids)
                {
                    if (c.Name != "C") continue;
                    long child = c.L(1), parent = c.L(2);
                    Obj co, po;
                    if (!byId.TryGetValue(child, out co)) continue;
                    byId.TryGetValue(parent, out po);
                    string pk = po != null ? po.Kind : "";
                    if (co.Kind == "Model" && pk == "Model") parentModel[child] = parent;
                    else if (co.Kind == "Geometry" && pk == "Model" && co.N.S(2) == "Mesh") geomOf[parent] = co.N;
                    else if (co.Kind == "Material" && pk == "Model") { List<FbxNode> l; if (!matsOf.TryGetValue(parent, out l)) matsOf[parent] = l = new List<FbxNode>(); l.Add(co.N); }
                    else if (co.Kind == "Texture" && pk == "Material")
                    {
                        string prop = c.Props.Count > 3 ? c.S(3) : "";
                        if (prop == "NormalMap" || prop == "Bump" || prop == "Maya|normalCamera") { normOf[parent] = co.N; continue; }
                        if (prop == "EmissiveColor" || prop == "Maya|emissionColor" || prop == "3dsMax|Parameters|emit_color_map") { emitOf[parent] = co.N; continue; }
                        if (prop == "DiffuseColor" || prop == "Maya|baseColor" || prop == "3dsMax|Parameters|base_color_map" || !texOf.ContainsKey(parent)) texOf[parent] = co.N;
                    }
                    else if (co.Kind == "Video" && pk == "Texture") videoOf[parent] = co.N;
                }

            // the file's axes and unit: to Unity (Y up, X mirrored as Unity's own importer does, metres)
            int up = 1, upS = 1, front = 2, frontS = 1, coord = 0, coordS = 1;
            double unit = 1.0;
            if (global != null)
            {
                Dictionary<string, FbxNode> g = global.Props70();
                up = GetInt(g, "UpAxis", 1); upS = GetInt(g, "UpAxisSign", 1);
                front = GetInt(g, "FrontAxis", 2); frontS = GetInt(g, "FrontAxisSign", 1);
                coord = GetInt(g, "CoordAxis", 0); coordS = GetInt(g, "CoordAxisSign", 1);
                FbxNode u; if (g.TryGetValue("UnitScaleFactor", out u)) unit = u.D(4);
            }
            float toMetres = (float)(unit / 100.0);

            Dictionary<long, Matrix4x4> world = new Dictionary<long, Matrix4x4>();
            GameObject root = new GameObject("WS_" + src);
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            int made = 0;
            foreach (KeyValuePair<long, Obj> kv in byId)
            {
                if (kv.Value.Kind != "Model") continue;
                FbxNode geo;
                if (!geomOf.TryGetValue(kv.Key, out geo)) continue;
                Matrix4x4 m = World(kv.Key, byId, parentModel, world) * GeoOffset(kv.Value.N);
                List<FbxNode> mats;
                matsOf.TryGetValue(kv.Key, out mats);
                Mesh mesh = BuildMesh(geo, m, up, upS, front, frontS, coord, coordS, toMetres, mats != null ? mats.Count : 1);
                if (mesh == null) continue;
                mesh.name = kv.Value.N.S(1);
                GameObject part = new GameObject(kv.Value.N.S(1));
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                Material[] ms = new Material[mesh.subMeshCount];
                for (int i = 0; i < ms.Length; i++)
                {
                    FbxNode mat = mats != null && i < mats.Count ? mats[i] : null;
                    FbxNode tex = null, video = null, norm = null, normVideo = null, emit = null, emitVideo = null;
                    if (mat != null) { texOf.TryGetValue(mat.L(0), out tex); normOf.TryGetValue(mat.L(0), out norm); emitOf.TryGetValue(mat.L(0), out emit); }
                    if (tex != null) videoOf.TryGetValue(tex.L(0), out video);
                    if (norm != null) videoOf.TryGetValue(norm.L(0), out normVideo);
                    if (emit != null) videoOf.TryGetValue(emit.L(0), out emitVideo);
                    ms[i] = MakeMaterial(mat, tex, video, dir, src);
                    if (norm != null) Normal(ms[i], norm, normVideo, dir, src);
                    if (emit != null) Glow(ms[i], emit, emitVideo, dir, src);
                }
                mr.sharedMaterials = ms;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                made++;
            }
            if (made == 0) { UnityEngine.Object.Destroy(root); throw new Exception("no meshes"); }
            return root;
        }

        private static int GetInt(Dictionary<string, FbxNode> d, string k, int def) { FbxNode n; return d.TryGetValue(k, out n) ? (int)n.D(4) : def; }

        private static Vector3 Vec(Dictionary<string, FbxNode> d, string k, Vector3 def)
        {
            FbxNode n;
            if (!d.TryGetValue(k, out n) || n.Props.Count < 7) return def;
            return new Vector3((float)n.D(4), (float)n.D(5), (float)n.D(6));
        }

        // FBX euler XYZ (X first): Rz * Ry * Rx, in the file's own right-handed frame
        private static Matrix4x4 Euler(Vector3 deg)
        {
            float x = deg.x * Mathf.Deg2Rad, y = deg.y * Mathf.Deg2Rad, z = deg.z * Mathf.Deg2Rad;
            Matrix4x4 rx = Matrix4x4.identity, ry = Matrix4x4.identity, rz = Matrix4x4.identity;
            rx.m11 = Mathf.Cos(x); rx.m12 = -Mathf.Sin(x); rx.m21 = Mathf.Sin(x); rx.m22 = Mathf.Cos(x);
            ry.m00 = Mathf.Cos(y); ry.m02 = Mathf.Sin(y); ry.m20 = -Mathf.Sin(y); ry.m22 = Mathf.Cos(y);
            rz.m00 = Mathf.Cos(z); rz.m01 = -Mathf.Sin(z); rz.m10 = Mathf.Sin(z); rz.m11 = Mathf.Cos(z);
            return rz * ry * rx;
        }

        private static Matrix4x4 Move(Vector3 t) { Matrix4x4 m = Matrix4x4.identity; m.m03 = t.x; m.m13 = t.y; m.m23 = t.z; return m; }
        private static Matrix4x4 Size(Vector3 s) { Matrix4x4 m = Matrix4x4.identity; m.m00 = s.x; m.m11 = s.y; m.m22 = s.z; return m; }

        // T * Roff * Rp * Rpre * R * Rpost⁻¹ * Rp⁻¹ * Soff * Sp * S * Sp⁻¹
        private static Matrix4x4 Local(FbxNode model)
        {
            Dictionary<string, FbxNode> p = model.Props70();
            Vector3 rp = Vec(p, "RotationPivot", Vector3.zero), sp = Vec(p, "ScalingPivot", Vector3.zero);
            return Move(Vec(p, "Lcl Translation", Vector3.zero)) * Move(Vec(p, "RotationOffset", Vector3.zero)) * Move(rp)
                * Euler(Vec(p, "PreRotation", Vector3.zero)) * Euler(Vec(p, "Lcl Rotation", Vector3.zero)) * Euler(Vec(p, "PostRotation", Vector3.zero)).inverse
                * Move(-rp) * Move(Vec(p, "ScalingOffset", Vector3.zero)) * Move(sp) * Size(Vec(p, "Lcl Scaling", Vector3.one)) * Move(-sp);
        }

        private static Matrix4x4 GeoOffset(FbxNode model)
        {
            Dictionary<string, FbxNode> p = model.Props70();
            return Move(Vec(p, "GeometricTranslation", Vector3.zero)) * Euler(Vec(p, "GeometricRotation", Vector3.zero)) * Size(Vec(p, "GeometricScaling", Vector3.one));
        }

        private static Matrix4x4 World(long id, Dictionary<long, Obj> byId, Dictionary<long, long> parent, Dictionary<long, Matrix4x4> done)
        {
            Matrix4x4 m;
            if (done.TryGetValue(id, out m)) return m;
            m = Local(byId[id].N);
            long p;
            if (parent.TryGetValue(id, out p) && byId.ContainsKey(p) && p != id) m = World(p, byId, parent, done) * m;
            done[id] = m;
            return m;
        }

        // a direction of the file's frame → Unity's: right/up/front from the file's axes, then X mirrored
        private static Vector3 Axes(Vector3 v, int up, int upS, int front, int frontS, int coord, int coordS)
        {
            float r = v[coord] * coordS, u = v[up] * upS, f = v[front] * frontS;
            return new Vector3(-r, u, f);
        }

        private static Mesh BuildMesh(FbxNode geo, Matrix4x4 m, int up, int upS, int front, int frontS, int coord, int coordS, float scale, int slots)
        {
            FbxNode vn = geo.Kid("Vertices"), pn = geo.Kid("PolygonVertexIndex");
            if (vn == null || pn == null) return null;
            double[] cp = vn.P(0) as double[];
            int[] pvi = pn.P(0) as int[];
            if (cp == null || pvi == null || pvi.Length == 0) return null;

            FbxNode normals = geo.Kid("LayerElementNormal"), uvs = geo.Kid("LayerElementUV"), mats = geo.Kid("LayerElementMaterial");
            Matrix4x4 nm = m.inverse.transpose;
            int subs = Mathf.Max(1, slots);
            List<Vector3> pos = new List<Vector3>(pvi.Length), nor = new List<Vector3>(pvi.Length);
            List<Vector2> uv = new List<Vector2>(pvi.Length);
            List<int>[] tris = new List<int>[subs];
            for (int i = 0; i < subs; i++) tris[i] = new List<int>();
            bool hasN = normals != null, hasUV = uvs != null;

            int poly = 0, start = 0;
            for (int i = 0; i < pvi.Length; i++)
            {
                if (pvi[i] >= 0) continue;
                // one polygon: pvi[start..i], the last stored as ~index
                int count = i - start + 1;
                int baseV = pos.Count;
                for (int k = 0; k < count; k++)
                {
                    int pv = start + k;
                    int c = pvi[pv] < 0 ? ~pvi[pv] : pvi[pv];
                    Vector3 p = new Vector3((float)cp[c * 3], (float)cp[c * 3 + 1], (float)cp[c * 3 + 2]);
                    pos.Add(Axes(m.MultiplyPoint3x4(p), up, upS, front, frontS, coord, coordS) * scale);
                    if (hasN)
                    {
                        Vector3 n = Attr3(normals, "Normals", "NormalsIndex", pv, c, poly);
                        nor.Add(Axes(nm.MultiplyVector(n), up, upS, front, frontS, coord, coordS).normalized);
                    }
                    if (hasUV) uv.Add(Attr2(uvs, "UV", "UVIndex", pv, c, poly));
                }
                int slot = 0;
                if (mats != null) slot = Mathf.Clamp(Index(mats, "Materials", null, pv: start, cp: 0, poly: poly), 0, subs - 1);
                // a fan, wound the other way (the X mirror turns the faces)
                for (int k = 1; k + 1 < count; k++) { tris[slot].Add(baseV); tris[slot].Add(baseV + k + 1); tris[slot].Add(baseV + k); }
                poly++;
                start = i + 1;
            }

            Mesh mesh = new Mesh();
            if (pos.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(pos);
            if (hasN && nor.Count == pos.Count) mesh.SetNormals(nor);
            if (hasUV && uv.Count == pos.Count) mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs;
            for (int i = 0; i < subs; i++) mesh.SetTriangles(tris[i], i);
            if (!hasN) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // the index of an attribute for one polygon corner, after its mapping and reference
        private static int Index(FbxNode layer, string data, string index, int pv, int cp, int poly)
        {
            string map = layer.KidS("MappingInformationType"), refType = layer.KidS("ReferenceInformationType");
            int i;
            if (map == "ByPolygonVertex") i = pv;
            else if (map == "ByVertice" || map == "ByVertex" || map == "ByControlPoint") i = cp;
            else if (map == "ByPolygon") i = poly;
            else i = 0; // AllSame
            if (data == "Materials") { int[] a = layer.Kid("Materials") != null ? layer.Kid("Materials").P(0) as int[] : null; return a != null && a.Length > 0 ? a[Mathf.Min(i, a.Length - 1)] : 0; }
            if (refType == "IndexToDirect" && index != null)
            {
                FbxNode ix = layer.Kid(index);
                int[] a = ix != null ? ix.P(0) as int[] : null;
                if (a != null && i < a.Length) i = a[i];
            }
            return i;
        }

        private static Vector3 Attr3(FbxNode layer, string data, string index, int pv, int cp, int poly)
        {
            double[] a = layer.Kid(data) != null ? layer.Kid(data).P(0) as double[] : null;
            int i = Index(layer, data, index, pv, cp, poly);
            if (a == null || i < 0 || i * 3 + 2 >= a.Length) return Vector3.up;
            return new Vector3((float)a[i * 3], (float)a[i * 3 + 1], (float)a[i * 3 + 2]);
        }

        private static Vector2 Attr2(FbxNode layer, string data, string index, int pv, int cp, int poly)
        {
            double[] a = layer.Kid(data) != null ? layer.Kid(data).P(0) as double[] : null;
            int i = Index(layer, data, index, pv, cp, poly);
            if (a == null || i < 0 || i * 2 + 1 >= a.Length) return Vector2.zero;
            return new Vector2((float)a[i * 2], (float)a[i * 2 + 1]);
        }

        // ---- materials: the file's diffuse colour, its texture when found beside the file

        private static Shader Shader
        {
            get
            {
                if (_shader != null) return _shader;
                _shader = Shader.Find("Standard");
                if (_shader == null) foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>()) if (s != null && s.name == "Standard") { _shader = s; break; }
                if (_shader == null) _shader = Shader.Find("Custom/Piece");
                if (_shader == null) _shader = Shader.Find("Legacy Shaders/Diffuse");
                WsFiles.Log("assets: shader " + (_shader != null ? _shader.name : "none"));
                return _shader;
            }
        }

        private static Material MakeMaterial(FbxNode mat, FbxNode tex, FbxNode video, string dir, string src)
        {
            Material m = new Material(Shader);
            m.hideFlags = HideFlags.HideAndDontSave;
            Color c = Color.white;
            if (mat != null)
            {
                m.name = mat.S(1);
                Dictionary<string, FbxNode> p = mat.Props70();
                FbxNode d;
                if (p.TryGetValue("DiffuseColor", out d) && d.Props.Count >= 7) c = new Color((float)d.D(4), (float)d.D(5), (float)d.D(6), 1f);
                if (p.TryGetValue("Opacity", out d) && d.Props.Count >= 5) c.a = Mathf.Clamp01((float)d.D(4));
            }
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (tex != null)
            {
                Texture2D t = FindTexture(tex, video, dir, src, false);
                if (t != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
                else if (t == null)
                {
                    string want = TextureName(tex, video);
                    if (want.Length > 0 && !WsAssets.NoTexture.Contains(want)) { WsAssets.NoTexture.Add(want); WsFiles.Log("asset " + src + ": texture " + want + " not found beside the file"); }
                }
            }
            return m;
        }

        // the file's normal map on the material (the game's light shows the carving)
        private static void Normal(Material m, FbxNode tex, FbxNode video, string dir, string src)
        {
            if (m == null || !m.HasProperty("_BumpMap")) return;
            Texture2D t = FindTexture(tex, video, dir, src, true);
            if (t == null) return;
            m.SetTexture("_BumpMap", t);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }

        // a picture from its bytes (a glow sheet beside a model): mipmapped, packed, kept on the card
        internal static Texture2D Picture(byte[] data, string name)
        {
            if (data == null || LoadImage == null) return null;
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            if (!(bool)LoadImage.Invoke(null, new object[] { t, data })) { UnityEngine.Object.Destroy(t); return null; }
            t.name = name;
            t.filterMode = FilterMode.Trilinear;
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply(true, true);
            return t;
        }

        // the file's glow map on the material: what is bright in it shines in the dark (a table's runes, a carved raven's eyes)
        private static void Glow(Material m, FbxNode tex, FbxNode video, string dir, string src)
        {
            if (m == null || !m.HasProperty("_EmissionMap")) return;
            Texture2D t = FindTexture(tex, video, dir, src, false);
            if (t == null) return;
            m.SetTexture("_EmissionMap", t);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static string TextureName(FbxNode tex, FbxNode video)
        {
            foreach (FbxNode n in new FbxNode[] { video, tex })
            {
                if (n == null) continue;
                foreach (string k in new string[] { "RelativeFilename", "FileName", "Filename" })
                {
                    string s = n.KidS(k);
                    if (s.Length > 0) return Path.GetFileName(s.Replace('\\', '/'));
                }
            }
            return "";
        }

        // The picture inside the file first; else a file of that name near it (a set whose textures lie beside).
        // An embedded one is kept per model: exporters (Meshy) give every file the same ids and names ("Image_0"),
        // so by its id alone one model would wear another's picture. linear: a normal map (directions, not colours)
        private static Texture2D FindTexture(FbxNode tex, FbxNode video, string dir, string src, bool linear)
        {
            string name = TextureName(tex, video);
            byte[] data = null;
            string key = null;
            if (video != null)
            {
                FbxNode content = video.Kid("Content");
                byte[] raw = content != null ? content.P(0) as byte[] : null;
                if (raw != null && raw.Length > 0) { data = raw; key = "embedded:" + src + ":" + video.L(0); }
            }
            if (data == null && name.Length > 0 && dir != null)
            {
                string setDir = dir;
                while (setDir != null && Path.GetDirectoryName(setDir) != null && !string.Equals(Path.GetFullPath(Path.GetDirectoryName(setDir)).TrimEnd('\\', '/'), Path.GetFullPath(WsAssets.Dir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    setDir = Path.GetDirectoryName(setDir);
                if (setDir == null) setDir = dir;
                string found = File.Exists(Path.Combine(dir, name)) ? Path.Combine(dir, name) : null;
                if (found == null) { string[] all = Directory.GetFiles(setDir, name, SearchOption.AllDirectories); if (all.Length > 0) found = all[0]; }
                if (found != null)
                {
                    key = found.ToLowerInvariant();
                    Texture2D had;
                    if (Textures.TryGetValue(key, out had) && had != null) return had;
                    data = File.ReadAllBytes(found);
                }
            }
            if (data == null || LoadImage == null) return null;
            if (linear) key += "#linear";
            Texture2D had2;
            if (Textures.TryGetValue(key, out had2) && had2 != null) return had2;
            Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            t.hideFlags = HideFlags.HideAndDontSave;
            if (!(bool)LoadImage.Invoke(null, new object[] { t, data })) { UnityEngine.Object.Destroy(t); return null; }
            bool small = t.width <= 512 && t.height <= 512;
            if (!small)
            {
                // a big painted picture: smaller copies for far away (no shimmer) and packed on the card (a 2048 one
                // takes 16 MB as it is, 4 MB packed)
                Texture2D big = new Texture2D(t.width, t.height, TextureFormat.RGBA32, true, linear);
                big.hideFlags = HideFlags.HideAndDontSave;
                big.SetPixels32(t.GetPixels32());
                big.Apply(true);
                UnityEngine.Object.Destroy(t);
                t = big;
                try { t.Compress(true); } catch (Exception ex) { WsFiles.Log("texture " + name + " not packed: " + ex.Message); }
                t.anisoLevel = 4;
            }
            // small palettes (one colour per cell) stay sharp
            t.filterMode = small ? FilterMode.Point : FilterMode.Trilinear;
            t.wrapMode = TextureWrapMode.Repeat;
            t.name = name;
            if (!small) t.Apply(false, true); // kept on the card only
            Textures[key] = t;
            return t;
        }
    }
}
#endif

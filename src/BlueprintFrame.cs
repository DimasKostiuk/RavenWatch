#if RAVEN_ITEM && RAVEN_NPC
// (a blueprint's fixed frame in the author's world: from 2.9 — the user 2026-11-01)
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A blueprint's frame — its middle and its front — is fixed once in the author's world, and every later save uses
    // the same one, wherever the author stands then. So every piece keeps its place in the blueprint from save to save,
    // and with it every name and layer (HallParts) and every piece of a Hall already standing in players' worlds.
    //   config/RavenWatch/blueprints/<name>.frame: «world x y z yaw» (tabs: a world's name may have spaces)
    // A blueprint saved before frames gets its frame from the building itself (Find): the pieces that are the only one
    // of their kind, in the saved file and round the author, paired; each pair of pairs gives a turn and a middle, and
    // the one that puts the most saved pieces onto pieces of their kind standing there wins.
    internal static class BlueprintFrame
    {
        private const float Near = 0.05f;   // m: the same place
        private const float Agree = 0.5f;   // of the saved pieces found again: below it, no frame (another building)
        private const int Sample = 200;     // pieces tried first for each guess (all of them only for a good one)

        private static string FileOf(string name) { return Path.Combine(Blueprint.Root, name + ".frame"); }
        private static string World { get { return ZNet.instance != null ? ZNet.instance.GetWorldName() : ""; } }

        // read once a world (and again after Set)
        private static readonly Dictionary<string, KeyValuePair<Vector3, float>> _known = new Dictionary<string, KeyValuePair<Vector3, float>>();
        private static readonly HashSet<string> _none = new HashSet<string>();
        private static string _knownWorld;

        internal static bool Get(string name, out Vector3 c, out Quaternion rot)
        {
            if (_knownWorld != World) { _knownWorld = World; _known.Clear(); _none.Clear(); }
            KeyValuePair<Vector3, float> k;
            if (_known.TryGetValue(name, out k)) { c = k.Key; rot = Quaternion.Euler(0f, k.Value, 0f); return true; }
            c = Vector3.zero; rot = Quaternion.identity;
            if (_none.Contains(name)) return false;
            bool ok = Read(name, out c, out rot);
            if (ok) _known[name] = new KeyValuePair<Vector3, float>(c, rot.eulerAngles.y); else _none.Add(name);
            return ok;
        }

        private static bool Read(string name, out Vector3 c, out Quaternion rot)
        {
            c = Vector3.zero; rot = Quaternion.identity;
            string f = FileOf(name);
            if (!Blueprint.On || !File.Exists(f)) return false;
            string[] w = File.ReadAllText(f, Encoding.UTF8).Trim().Split('\t');
            float x, y, z, yaw;
            if (w.Length < 5 || w[0] != World || !TryF(w[1], out x) || !TryF(w[2], out y) || !TryF(w[3], out z) || !TryF(w[4], out yaw)) return false;
            c = new Vector3(x, y, z);
            rot = Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        // the world it was fixed in («» — none)
        internal static string WorldOf(string name)
        {
            string f = FileOf(name);
            if (!Blueprint.On || !File.Exists(f)) return "";
            string[] w = File.ReadAllText(f, Encoding.UTF8).Trim().Split('\t');
            return w.Length >= 5 ? w[0] : "";
        }

        internal static void Set(string name, Vector3 c, float yaw)
        {
            File.WriteAllText(FileOf(name), World + "\t" + F(c.x) + "\t" + F(c.y) + "\t" + F(c.z) + "\t" + F(Mathf.Repeat(yaw, 360f)), new UTF8Encoding(false));
            _known.Remove(name); _none.Remove(name);
            BlueprintWearPatch.ForgetAll();
        }

        // a piece's place in the blueprint (by the frame of this world)
        internal static Vector3 Local(Vector3 c, Quaternion rot, Vector3 world) { return Quaternion.Inverse(rot) * (world - c); }

        // the frame of a saved blueprint found in the building round `around` (null: found and fixed; else why not)
        internal static string Find(string name, Vector3 around, float radius)
        {
            string file = Blueprint.FileOf(name);
            if (!File.Exists(file)) return "креслення «" + name + "» ще нема";
            List<Blueprint.Part> parts = Blueprint.Parse(File.ReadAllLines(file, Encoding.UTF8), new List<Blueprint.Spot>());
            if (parts.Count == 0) return "креслення порожнє";
            Dictionary<string, List<Vector3>> stand = new Dictionary<string, List<Vector3>>(), saved = new Dictionary<string, List<Vector3>>();
            HashSet<long> here = new HashSet<long>();
            foreach (Piece pc in Blueprint.Around(around, radius))
            {
                string k = Utils.GetPrefabName(pc.gameObject);
                Add(stand, k, pc.transform.position);
                here.Add(Key(k, pc.transform.position - around));
            }
            foreach (Blueprint.Part pt in parts) Add(saved, pt.Prefab, pt.Pos);
            List<Vector3[]> pairs = new List<Vector3[]>(); // (saved, standing)
            foreach (KeyValuePair<string, List<Vector3>> kv in saved)
            {
                List<Vector3> w;
                if (kv.Value.Count == 1 && stand.TryGetValue(kv.Key, out w) && w.Count == 1) pairs.Add(new[] { kv.Value[0], w[0] });
            }
            if (pairs.Count < 2) return "замало деталей, які є по одній (" + pairs.Count + "): стань ближче до середини будівлі";
            int best = -1, tries = 0; Vector3 bestC = Vector3.zero; float bestYaw = 0f;
            for (int i = 0; i < pairs.Count && tries < 600; i++)
                for (int j = i + 1; j < pairs.Count && tries < 600; j++)
                {
                    Vector3 la = pairs[i][0], lb = pairs[j][0], wa = pairs[i][1], wb = pairs[j][1];
                    Vector2 dl = new Vector2(lb.x - la.x, lb.z - la.z), dw = new Vector2(wb.x - wa.x, wb.z - wa.z);
                    if (dl.magnitude < 2f || Mathf.Abs(dl.magnitude - dw.magnitude) > Near || Mathf.Abs((lb.y - la.y) - (wb.y - wa.y)) > Near) continue;
                    tries++;
                    float yaw = (Mathf.Atan2(dw.x, dw.y) - Mathf.Atan2(dl.x, dl.y)) * Mathf.Rad2Deg; // (a turn round Y moves a heading by itself)
                    Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                    Vector3 c = wa - q * la;
                    if (Score(parts, here, c, q, around, Sample) < Mathf.Min(Sample, parts.Count) / 2) continue;
                    int s = Score(parts, here, c, q, around, parts.Count);
                    if (s > best) { best = s; bestC = c; bestYaw = yaw; }
                }
            if (best < parts.Count * Agree) return "будівля тут не схожа на збережене креслення (збіглося " + Mathf.Max(best, 0) + " з " + parts.Count + ")";
            Refine(parts, stand, ref bestC, ref bestYaw);
            Set(name, bestC, bestYaw);
            return null;
        }

        // the guess from two pieces, made exact by all the pieces it found: each saved piece paired with the standing one
        // of its kind nearest to where the guess puts it; then the turn and the middle that lay them on each other best
        // (least squares; a turn round Y only — the frame never tilts)
        private static void Refine(List<Blueprint.Part> parts, Dictionary<string, List<Vector3>> stand, ref Vector3 c, ref float yaw)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            List<Vector3> ls = new List<Vector3>(), ws = new List<Vector3>();
            foreach (Blueprint.Part pt in parts)
            {
                List<Vector3> kind;
                if (!stand.TryGetValue(pt.Prefab, out kind)) continue;
                Vector3 guess = c + q * pt.Pos, hit = Vector3.zero;
                float bestD = 0.2f * 0.2f;
                bool found = false;
                foreach (Vector3 w in kind) { float d = (w - guess).sqrMagnitude; if (d < bestD) { bestD = d; hit = w; found = true; } }
                if (found) { ls.Add(pt.Pos); ws.Add(hit); }
            }
            if (ls.Count < 3) return;
            Vector3 lc = Vector3.zero, wc = Vector3.zero;
            for (int i = 0; i < ls.Count; i++) { lc += ls[i]; wc += ws[i]; }
            lc /= ls.Count; wc /= ls.Count;
            double dot = 0, cross = 0;
            for (int i = 0; i < ls.Count; i++)
            {
                Vector3 l = ls[i] - lc, w = ws[i] - wc;
                dot += w.x * l.x + w.z * l.z;
                cross += w.x * l.z - w.z * l.x; // (Euler(0, a, 0) turns (x, z) into (x cos a + z sin a, −x sin a + z cos a))
            }
            yaw = (float)(System.Math.Atan2(cross, dot) * 180.0 / System.Math.PI);
            c = wc - Quaternion.Euler(0f, yaw, 0f) * lc;
        }

        // how many of the first n saved pieces stand (their kind, their place) where this frame puts them
        private static int Score(List<Blueprint.Part> parts, HashSet<long> here, Vector3 c, Quaternion q, Vector3 around, int n)
        {
            int hit = 0, step = Mathf.Max(1, parts.Count / Mathf.Max(1, n));
            for (int i = 0, seen = 0; i < parts.Count && seen < n; i += step, seen++)
            {
                Vector3 w = c + q * parts[i].Pos - around;
                if (Near3(here, parts[i].Prefab, w)) hit++;
            }
            return hit;
        }

        private static bool Near3(HashSet<long> here, string prefab, Vector3 w)
        {
            int x = Mathf.RoundToInt(w.x * 10f), y = Mathf.RoundToInt(w.y * 10f), z = Mathf.RoundToInt(w.z * 10f), h = prefab.GetStableHashCode();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (here.Contains(Pack(h, x + dx, y + dy, z + dz))) return true;
            return false;
        }

        private static long Key(string prefab, Vector3 w) { return Pack(prefab.GetStableHashCode(), Mathf.RoundToInt(w.x * 10f), Mathf.RoundToInt(w.y * 10f), Mathf.RoundToInt(w.z * 10f)); }
        private static long Pack(int h, int x, int y, int z) { return ((long)(h & 0xFFFF) << 48) | ((long)(x & 0xFFFF) << 32) | ((long)(y & 0xFFFF) << 16) | (long)(z & 0xFFFF); }

        private static void Add(Dictionary<string, List<Vector3>> d, string k, Vector3 v)
        {
            List<Vector3> l;
            if (!d.TryGetValue(k, out l)) d[k] = l = new List<Vector3>();
            l.Add(v);
        }

        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }
}
#endif

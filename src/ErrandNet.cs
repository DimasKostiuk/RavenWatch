#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The places of the world (crypts, caves, runestones…) are known in full only to the server (ZoneSystem's location
    // list), so an errand that sends you to one asks the server for it before it is given. On a host or alone the answer
    // is right here. A dedicated server without the mod never answers: after a few seconds the errand gives way to one
    // that needs no such place.
    internal static class ErrandNet
    {
        private const string RpcFind = "RavenWatch_ErrandFind", RpcFound = "RavenWatch_ErrandFound";
        internal static float Wait = 8f;
        private const int MaxAsks = 4;          // places in one errand, at most
        private const float MaxRing = 6000f;    // m: never further than this from the player

        internal sealed class Ask { public string Slot = "", Names = "", Lands = "|", Near = ""; public float X, Z, Min, Max, Within; public bool Fresh, Nearest; }
        // Within: only this near the world's middle (0 anywhere); Fresh: not where someone has already killed (PlaceClear);
        // Near: the ring is round that slot's place (answered before it in the same ask), not round you; Nearest: the
        // closest such place to it, not one at random. One place is never given to two slots.
        private sealed class Pending { public Action<Dictionary<string, string>> Done; public float Until; }
        private static readonly Dictionary<int, Pending> Waiting = new Dictionary<int, Pending>();
        private static int _next = 1;

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcFind, new Action<long, ZPackage>(OnFind));
            ZRoutedRpc.instance.Register<ZPackage>(RpcFound, new Action<long, ZPackage>(OnFound));
        }

        // the asks answered (slot → "x/z", "" when none such) or null when nobody answered
        internal static void Find(List<Ask> asks, Action<Dictionary<string, string>> done)
        {
            if (asks.Count == 0) { done(new Dictionary<string, string>()); return; }
            if (ZNet.instance != null && ZNet.instance.IsServer()) { done(Answer(asks)); return; }
            if (ZRoutedRpc.instance == null) { done(null); return; }
            int id = _next++;
            Waiting[id] = new Pending { Done = done, Until = Time.time + Wait };
            ZPackage pkg = new ZPackage();
            pkg.Write(id);
            pkg.Write(asks.Count);
            foreach (Ask a in asks) { pkg.Write(a.Slot); pkg.Write(a.Names); pkg.Write(a.Lands); pkg.Write(a.X); pkg.Write(a.Z); pkg.Write(a.Min); pkg.Write(a.Max); pkg.Write(a.Within); pkg.Write(a.Fresh); pkg.Write(a.Near); pkg.Write(a.Nearest); }
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (server == null) { Waiting.Remove(id); done(null); return; }
            ZRoutedRpc.instance.InvokeRoutedRPC(server.m_uid, RpcFind, pkg);
        }

        // nobody answered in time
        internal static void Tick()
        {
            if (Waiting.Count == 0) return;
            List<int> late = null;
            foreach (KeyValuePair<int, Pending> kv in Waiting) if (Time.time > kv.Value.Until) { if (late == null) late = new List<int>(); late.Add(kv.Key); }
            if (late == null) return;
            foreach (int id in late)
            {
                Pending p = Waiting[id];
                Waiting.Remove(id);
                Errands.Log("the server did not answer where to send (no RavenWatch on it?)");
                try { p.Done(null); } catch (Exception e) { Errands.Log("find (late): " + e); }
            }
        }

        private static readonly Dictionary<long, float> LastAsk = new Dictionary<long, float>();
        private const float AskGap = 2f; // s between one player's asks (each goes through every place of the world)

        private static void OnFind(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            float was;
            if (LastAsk.TryGetValue(sender, out was) && Time.time - was < AskGap) return; // (no answer: the asker gives up after Wait)
            LastAsk[sender] = Time.time;
            if (LastAsk.Count > 64) LastAsk.Clear();
            int id = pkg.ReadInt(), n = Mathf.Min(pkg.ReadInt(), MaxAsks);
            List<Ask> asks = new List<Ask>();
            for (int i = 0; i < n; i++) asks.Add(new Ask { Slot = pkg.ReadString(), Names = pkg.ReadString(), Lands = pkg.ReadString(), X = pkg.ReadSingle(), Z = pkg.ReadSingle(), Min = pkg.ReadSingle(), Max = pkg.ReadSingle(), Within = pkg.ReadSingle(), Fresh = pkg.ReadBool(), Near = pkg.ReadString(), Nearest = pkg.ReadBool() });
            Dictionary<string, string> found = Answer(asks);
            ZPackage back = new ZPackage();
            back.Write(id);
            back.Write(found.Count);
            foreach (KeyValuePair<string, string> kv in found) { back.Write(kv.Key); back.Write(kv.Value); }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcFound, back);
        }

        private static void OnFound(long sender, ZPackage pkg)
        {
            ZNetPeer server = ZNet.instance != null ? ZNet.instance.GetServerPeer() : null;
            if (server == null || sender != server.m_uid) return; // only the server says where
            int id = pkg.ReadInt(), n = pkg.ReadInt();
            Dictionary<string, string> found = new Dictionary<string, string>();
            for (int i = 0; i < n; i++) found[pkg.ReadString()] = pkg.ReadString();
            Pending p;
            if (!Waiting.TryGetValue(id, out p)) return;
            Waiting.Remove(id);
            try { p.Done(found); } catch (Exception e) { Errands.Log("find: " + e); }
        }

        // on the server: one place of those names in that ring, in a land the player has walked (at random)
        private static Dictionary<string, string> Answer(List<Ask> asks)
        {
            Dictionary<string, string> found = new Dictionary<string, string>();
            List<Vector3> used = new List<Vector3>();
            foreach (Ask a in asks)
            {
                string from;
                if (a.Near.Length > 0)
                {
                    if (!found.TryGetValue(a.Near, out from) || from.Length == 0) { found[a.Slot] = ""; continue; } // (its place not found: neither is this)
                    string[] xz = from.Split('/');
                    a.X = Errands.F(xz[0], a.X); a.Z = xz.Length > 1 ? Errands.F(xz[1], a.Z) : a.Z;
                }
                string v = One(a, used);
                found[a.Slot] = v;
                if (v.Length > 0) { string[] xz = v.Split('/'); used.Add(new Vector3(Errands.F(xz[0], 0f), 0f, Errands.F(xz[1], 0f))); }
            }
            return found;
        }

        private static bool Taken(List<Vector3> used, Vector3 p)
        {
            foreach (Vector3 u in used) if ((u.x - p.x) * (u.x - p.x) + (u.z - p.z) * (u.z - p.z) < 4f) return true;
            return false;
        }

        private static string One(Ask a, List<Vector3> used)
        {
            if (ZoneSystem.instance == null) return "";
            string[] names = a.Names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<Vector3> can = new List<Vector3>();
            if (Array.IndexOf(names, "RavenTree") >= 0 && ZDOMan.instance != null) // our own trees: their ZDOs
            {
                List<ZDO> trees = new List<ZDO>();
                trees.AddRange(RavenTree.AllZdos());
                foreach (ZDO z in trees)
                {
                    Vector3 t = z.GetPosition();
                    float dt = Mathf.Sqrt((t.x - a.X) * (t.x - a.X) + (t.z - a.Z) * (t.z - a.Z));
                    if (dt < a.Min || dt > Mathf.Min(a.Max, MaxRing)) continue;
                    if (a.Within > 0f && t.x * t.x + t.z * t.z > a.Within * a.Within) continue;
                    if (WorldGenerator.instance != null)
                    {
                        Heightmap.Biome tb = WorldGenerator.instance.GetBiome(t.x, t.z);
                        if (tb != Heightmap.Biome.Meadows && !a.Lands.Contains("|" + tb + "|")) continue; // (not to a land you have never been)
                    }
                    can.Add(t);
                }
            }
            foreach (ZoneSystem.LocationInstance li in ZoneSystem.instance.m_locationInstances.Values)
            {
                if (li.m_location == null) continue;
                string pf = li.m_location.m_prefabName ?? "";
                bool named = false;
                foreach (string n in names) if (pf.StartsWith(n, StringComparison.OrdinalIgnoreCase)) { named = true; break; }
                if (!named) continue;
                float dx = li.m_position.x - a.X, dz = li.m_position.z - a.Z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < a.Min || d > Mathf.Min(a.Max, MaxRing)) continue;
                if (a.Within > 0f && li.m_position.x * li.m_position.x + li.m_position.z * li.m_position.z > a.Within * a.Within) continue;
                if (WorldGenerator.instance != null)
                {
                    Heightmap.Biome b = WorldGenerator.instance.GetBiome(li.m_position.x, li.m_position.z);
                    if (b != Heightmap.Biome.Meadows && !a.Lands.Contains("|" + b + "|")) continue;
                }
                if (Taken(used, li.m_position)) continue;
                can.Add(li.m_position);
            }
            if (a.Nearest && can.Count > 1) can.Sort((p1, p2) => ((p1.x - a.X) * (p1.x - a.X) + (p1.z - a.Z) * (p1.z - a.Z)).CompareTo((p2.x - a.X) * (p2.x - a.X) + (p2.z - a.Z) * (p2.z - a.Z)));
            // a fresh one only: not where someone has already fought (shuffled; the first fresh one found)
            if (a.Fresh && can.Count > 0)
            {
                List<Vector3> fresh = new List<Vector3>();
                if (!a.Nearest) for (int i = can.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); Vector3 t = can[i]; can[i] = can[j]; can[j] = t; }
                foreach (Vector3 c in can) { if (!PlaceClear.Fought(c)) { fresh.Add(c); break; } }
                if (fresh.Count == 0) Errands.Log("every place of «" + a.Names + "» in that ring has been fought in already");
                can = fresh;
            }
            if (can.Count == 0) return "";
            Vector3 at = a.Nearest ? can[0] : can[UnityEngine.Random.Range(0, can.Count)];
            return Mathf.RoundToInt(at.x) + "/" + Mathf.RoundToInt(at.z);
        }
    }
}
#endif

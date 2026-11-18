#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HuginWhispers
{
    // The census of our things in the world (the user 2026-11-01: predictable, small checks): the people, their lockers,
    // the hearts. ONE running pass over the world's objects for every kind asked for — a slice a frame, at most PerFrame
    // objects looked at — begun again Every s after it ends; whoever asks gets the last whole list of its kind. NpcStray,
    // NpcRescue, StoryPeople, NpcHeart, NpcKit, NpcLocker read the same lists instead of each going over the whole world
    // in one frame. The server sees the whole world this way; a client, what it has loaded.
    //   Of(prefab)    the last whole list (things gone since left out); a kind asked for the first time — at once, whole
    //   Added(z)      one just made: in the list now (no second one made before the next pass sees it)
    // A kind nobody asked for in Forget s is let go.
    internal static class NpcCensus
    {
        internal static float Every = 20f, Forget = 120f;
        internal static int PerFrame = 3000;
        private sealed class Kind { public string Prefab; public float Asked; public List<ZDO> Building = new List<ZDO>(), Ready; }
        private static readonly Dictionary<int, Kind> Kinds = new Dictionary<int, Kind>(); // by the prefab's hash
        private static readonly List<int> Gone = new List<int>();
        private static readonly List<ZDO> Out = new List<ZDO>();
        private static readonly FieldInfo BySector = HarmonyLib.AccessTools.Field(typeof(ZDOMan), "m_objectsBySector");
        private static object _world;
        private static bool _running;
        private static int _sector, _inSector;
        private static float _next;
        private static readonly int PerfSlice = NpcPerf.Id("census (перепис світу, шматок)"), PerfWhole = NpcPerf.Id("census whole (перепис одразу)");

        internal static List<ZDO> Of(string prefab)
        {
            Fresh();
            int hash = prefab.GetStableHashCode();
            Kind k;
            if (!Kinds.TryGetValue(hash, out k)) { k = new Kind { Prefab = prefab }; Kinds[hash] = k; }
            k.Asked = Time.time;
            if (k.Ready == null && ZDOMan.instance != null) // (the first ask: whole, at once — as it always was)
            {
                long perf = NpcPerf.Now();
                List<ZDO> all = new List<ZDO>(); int idx = 0;
                while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, all, ref idx)) { }
                k.Ready = all;
                NpcPerf.Add(PerfWhole, perf);
            }
            Out.Clear();
            if (k.Ready != null && ZDOMan.instance != null) foreach (ZDO z in k.Ready) if (z != null && z.IsValid() && z.GetPrefab() == hash) Out.Add(z); // (gone since: left out — and one the game's pool has given since to another thing: «valid» is only «in use», the review 2026-11-17)
            return new List<ZDO>(Out);
        }

        // every person of ours, whatever its body (Npcs.PeoplePrefabs): the one ask for «the people» — a body kind named by itself misses the others
        // (the dvergr body, 2026-11-13: the story's people, the strays, the hearts counted the human one alone)
        internal static List<ZDO> People()
        {
            List<ZDO> all = new List<ZDO>();
            foreach (string p in Npcs.PeoplePrefabs) all.AddRange(Of(p));
            return all;
        }

        internal static void Added(ZDO z)
        {
            Kind k;
            if (z == null || !Kinds.TryGetValue(z.GetPrefab(), out k)) return;
            if (k.Ready != null && !k.Ready.Contains(z)) k.Ready.Add(z);
            if (_running && !k.Building.Contains(z)) k.Building.Add(z);
        }

        // every frame on every game: one slice of the pass while it runs
        internal static void Tick()
        {
            if (ZDOMan.instance == null || Kinds.Count == 0 || BySector == null) return;
            Fresh();
            if (!_running)
            {
                if (Time.time < _next) return;
                Gone.Clear();
                foreach (KeyValuePair<int, Kind> kv in Kinds) if (Time.time - kv.Value.Asked > Forget) Gone.Add(kv.Key);
                foreach (int g in Gone) Kinds.Remove(g);
                if (Kinds.Count == 0) return;
                foreach (Kind k in Kinds.Values) k.Building = new List<ZDO>();
                _running = true; _sector = 0; _inSector = 0;
            }
            List<ZDO>[] sectors = BySector.GetValue(ZDOMan.instance) as List<ZDO>[];
            if (sectors == null) return;
            long perf = NpcPerf.Now();
            int looked = 0;
            while (_sector < sectors.Length && looked < PerFrame)
            {
                List<ZDO> l = sectors[_sector];
                if (l == null || _inSector >= l.Count) { _sector++; _inSector = 0; continue; }
                for (; _inSector < l.Count && looked < PerFrame; _inSector++, looked++)
                {
                    ZDO z = l[_inSector];
                    Kind k;
                    if (z != null && Kinds.TryGetValue(z.GetPrefab(), out k)) k.Building.Add(z);
                }
            }
            if (_sector >= sectors.Length) // (the pass is through: its lists are the census now)
            {
                // one that walked into a sector already passed is not lost, nor one met twice counted twice: the last
                // list's still there are kept with this one's — a thing leaves the census only when it is gone
                foreach (KeyValuePair<int, Kind> kv in Kinds)
                {
                    Kind k = kv.Value;
                    HashSet<ZDO> seen = new HashSet<ZDO>();
                    List<ZDO> now = new List<ZDO>();
                    foreach (ZDO z in k.Building) if (z != null && z.IsValid() && z.GetPrefab() == kv.Key && seen.Add(z)) now.Add(z);
                    if (k.Ready != null) foreach (ZDO z in k.Ready) if (z != null && z.IsValid() && z.GetPrefab() == kv.Key && seen.Add(z)) now.Add(z); // (one the pool gave to another thing since: off the list, as Of leaves it out)
                    k.Ready = now;
                }
                _running = false; _next = Time.time + Every;
            }
            NpcPerf.Add(PerfSlice, perf);
        }

        private static void Fresh()
        {
            if (ReferenceEquals(_world, ZNet.instance)) return;
            _world = ZNet.instance;
            Kinds.Clear(); _running = false; _next = 0f; // (another world: nothing of the old one)
        }
    }
}
#endif

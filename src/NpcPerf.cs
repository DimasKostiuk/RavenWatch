#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What the people cost (the user 2026-11-01: «оптимізація — щоб гра не перераховувала NPC, щоб перевірки були
    // прогнозовані та невеликі»). Each costly part says when it begins and ends (Now … Add); kept per part since the last
    // reset: how often, how long in all, the longest once, and the worst frame of all of them together. Two stopwatch
    // reads a part, no allocation: always on. `raven npc perf` [reset] shows it (and the clock's: what waited its turn).
    internal static class NpcPerf
    {
        private static readonly Dictionary<string, int> Ids = new Dictionary<string, int>();
        private static readonly List<string> Names = new List<string>();
        private static long[] _calls = new long[32], _ticks = new long[32], _max = new long[32];
        private static float _since = -1f;
        private static int _frame = -1;
        private static long _frameTicks, _worst;

        // a part's number (once, kept in a static field by its caller)
        internal static int Id(string name)
        {
            int id;
            if (Ids.TryGetValue(name, out id)) return id;
            id = Names.Count;
            Ids[name] = id; Names.Add(name);
            if (id >= _calls.Length) { System.Array.Resize(ref _calls, id * 2); System.Array.Resize(ref _ticks, id * 2); System.Array.Resize(ref _max, id * 2); }
            return id;
        }

        internal static long Now() { return Stopwatch.GetTimestamp(); }

        internal static void Add(int id, long from)
        {
            long d = Stopwatch.GetTimestamp() - from;
            if (_since < 0f) _since = Time.realtimeSinceStartup;
            _calls[id]++; _ticks[id] += d; if (d > _max[id]) _max[id] = d;
            if (Time.frameCount != _frame) { if (_frameTicks > _worst) _worst = _frameTicks; _frame = Time.frameCount; _frameTicks = 0; }
            _frameTicks += d; // (parts inside parts count twice here: the worst frame is an upper bound)
        }

        internal static void Reset()
        {
            for (int i = 0; i < _calls.Length; i++) { _calls[i] = 0; _ticks[i] = 0; _max[i] = 0; }
            _worst = 0; _frameTicks = 0; _since = Time.realtimeSinceStartup;
            NpcClock.Reset();
        }

        private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

        internal static string Report()
        {
            float secs = _since < 0f ? 0f : Mathf.Max(0.001f, Time.realtimeSinceStartup - _since);
            StringBuilder sb = new StringBuilder("Вартість NPC за " + secs.ToString("0") + " с (живих тут: " + NpcActor.Live.Count + "):\n");
            sb.Append("  частина: разів/с · мс/с · найдовше мс\n");
            List<int> order = new List<int>();
            for (int i = 0; i < Names.Count; i++) if (_calls[i] > 0) order.Add(i);
            order.Sort((a, b) => _ticks[b].CompareTo(_ticks[a]));
            foreach (int i in order)
                sb.Append("  ").Append(Names[i]).Append(": ").Append((_calls[i] / secs).ToString("0.#")).Append(" · ")
                  .Append((Ms(_ticks[i]) / secs).ToString("0.00")).Append(" · ").Append(Ms(_max[i]).ToString("0.00")).Append('\n');
            if (order.Count == 0) sb.Append("  (нічого ще не рахувалось)\n");
            sb.Append("Найгірший кадр NPC: ").Append(Ms(System.Math.Max(_worst, _frameTicks)).ToString("0.00")).Append(" мс\n");
            sb.Append(NpcClock.Report());
            return sb.ToString();
        }

        // `raven npc perf [reset]`
        internal static string Command(string[] a)
        {
            if (a.Length > 2 && a[2] == "reset") { Reset(); return "Лічильники NPC з нуля."; }
            string r = Report();
            Npcs.Log("[npc perf]\n" + r);
            return r;
        }
    }

    // When the people think (the clock of all of them on this game): their checks spread over time, not all in one
    // frame — and fewer of them where nobody is near.
    //   Phase(a)   each person's own offset (0..1): its timers start out of step with everyone else's
    //   Slow(a)    × its checks' intervals: 1 with a player near (Near m), Mid m — 2.5, farther — 5; asleep × 3
    //   Take()     a costly check may run this frame (at most PerFrame of them on a game): else it waits for the next
    internal static class NpcClock
    {
        internal static float Near = 40f, Mid = 100f;
        internal static int PerFrame = 3;
        private static int _frame = -1, _used;
        private static long _waited, _taken;

        internal static float Phase(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            uint h = z != null ? z.m_uid.ID * 2654435761u : (uint)Random.Range(0, int.MaxValue);
            return (h % 1000u) / 1000f;
        }

        internal static float Slow(NpcActor a)
        {
            if (a == null) return 1f;
            float near = float.MaxValue;
            Vector3 at = a.transform.position;
            foreach (Player p in Player.GetAllPlayers()) if (p != null) near = Mathf.Min(near, Utils.DistanceXZ(p.transform.position, at));
            float k = near <= Near ? 1f : near <= Mid ? 2.5f : 5f;
            if (a.Mind != null && a.Mind.Asleep) k *= 3f;
            return k;
        }

        internal static bool Take()
        {
            if (Time.frameCount != _frame) { _frame = Time.frameCount; _used = 0; }
            if (_used >= PerFrame) { _waited++; return false; }
            _used++; _taken++;
            return true;
        }

        internal static void Reset() { _waited = 0; _taken = 0; }

        internal static string Report()
        {
            long all = _waited + _taken;
            return "Черга важких перевірок: виконано " + _taken + ", чекали на наступний кадр " + _waited + (all > 0 ? " (" + (100.0 * _waited / all).ToString("0") + "%)" : "") + ", ліміт " + PerFrame + " на кадр.";
        }
    }
}
#endif

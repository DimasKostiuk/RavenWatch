#if RAVEN_ITEM && RAVEN_NPC
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A load test of the people (the user 2026-11-04: «20 NPCs in one place, I want to see the load and the FPS, before and after»). The
    // author's tool, nothing a player meets:
    //   raven npc stress fps [s=30]     the whole frame for so many seconds: average FPS, 1% low, the worst frame, and what the people cost
    //                                   in it (NpcPerf from zero) — into the log, «[npc stress]»
    //   raven npc stress add [n=20]     n settlers round you, a few a frame, each living at the base you stand in (its cap of PerBase
    //                                   people not asked: a test), tagged «stress»
    //   raven npc stress remove         every person tagged «stress» gone from the world (its locker, its things with it: NpcActor.Remove)
    internal static class NpcStress
    {
        internal const string Tag = "stress";
        private static bool _measuring;

        internal static string Cmd(Player p, string[] a)
        {
            string what = a.Length > 2 ? a[2] : "";
            switch (what)
            {
                case "fps":
                    if (_measuring) return "Уже міряю.";
                    float s = a.Length > 3 ? Mathf.Clamp(Errands.F(a[3], 30f), 5f, 300f) : 30f;
                    Plugin.Instance.StartCoroutine(Measure(s));
                    return "Міряю " + s.ToString("0", CultureInfo.InvariantCulture) + " с (у лог: [npc stress]).";
                case "add":
                    int n = a.Length > 3 ? Mathf.Clamp(Errands.I(a[3], 20), 1, 60) : 20;
                    Plugin.Instance.StartCoroutine(Add(p, n));
                    return "Додаю " + n + " (у лог: [npc stress]).";
                case "remove":
                    int gone = 0;
                    foreach (NpcActor x in NpcActor.Live.ToArray()) if (x != null && x.Zdo != null && x.Zdo.GetString(Npcs.KTag, "") == Tag) { x.Remove(); gone++; }
                    Npcs.Log("[npc stress] removed " + gone + " (loaded here; the rest when their zone loads: run it again there)");
                    return "Прибрано: " + gone + ".";
                default: return "raven npc stress fps [с] | add [скільки] | remove";
            }
        }

        private static IEnumerator Add(Player p, int n)
        {
            Vector3 home = p.transform.position;
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                float ang = i * Mathf.PI * 2f / n, r = 4f + (i % 3) * 3f;
                Vector3 at = home + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * r;
                NpcActor x = Npcs.Spawn("settler", at, Quaternion.Euler(0f, ang * Mathf.Rad2Deg + 180f, 0f), p.GetPlayerID(), Tag);
                if (x != null) { made++; yield return null; NpcResident.Settle(x, home, false, false, false); /* (nobody told of them: they leave no gossip behind) */ }
                if (i % 3 == 2) yield return null; // (a few a frame)
            }
            Npcs.Log("[npc stress] added " + made + " of " + n + " round " + Mathf.RoundToInt(home.x) + "," + Mathf.RoundToInt(home.z) + "; live now " + NpcActor.Live.Count);
        }

        private static IEnumerator Measure(float seconds)
        {
            _measuring = true;
            NpcPerf.Reset();
            List<float> frames = new List<float>(4096);
            float end = Time.unscaledTime + seconds;
            yield return null;
            while (Time.unscaledTime < end) { frames.Add(Time.unscaledDeltaTime); yield return null; }
            _measuring = false;
            if (frames.Count == 0) yield break;
            float sum = 0f; foreach (float f in frames) sum += f;
            frames.Sort();
            float low1 = frames[Mathf.Clamp(Mathf.FloorToInt(frames.Count * 0.99f), 0, frames.Count - 1)];
            float low01 = frames[Mathf.Clamp(Mathf.FloorToInt(frames.Count * 0.999f), 0, frames.Count - 1)];
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder t = new StringBuilder("[npc stress] ");
            t.Append(seconds.ToString("0", ci)).Append(" s, people loaded ").Append(NpcActor.Live.Count).Append(", frames ").Append(frames.Count)
             .Append(": FPS ").Append((frames.Count / sum).ToString("0.0", ci))
             .Append(" · 1% low ").Append((1f / low1).ToString("0.0", ci)).Append(" FPS (").Append((low1 * 1000f).ToString("0.0", ci)).Append(" ms)")
             .Append(" · 0.1% low ").Append((low01 * 1000f).ToString("0.0", ci)).Append(" ms")
             .Append(" · worst ").Append((frames[frames.Count - 1] * 1000f).ToString("0.0", ci)).Append(" ms\n").Append(NpcPerf.Report());
            Npcs.Log(t.ToString());
        }
    }
}
#endif

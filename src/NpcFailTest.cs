#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;

namespace HuginWhispers
{
    // A failure on purpose (raven npc fail <who> tick|mind|act|off, for the author): to see what the core does when a module
    // throws, on one person — a part of its life that throws (NpcActor.Run: told, left alone a while), a plan whose thought
    // throws (NpcMind.ThinkFailed: told, let go), an act that throws (NpcActor.DoAct: told). Nothing costs anything unless used.
    internal static class NpcFailTest
    {
        private const string Whose = "failtest";
        private static readonly HashSet<NpcActor> Ticking = new HashSet<NpcActor>();

        internal static void Wire()
        {
            NpcActor.Ticks.Add(a => { if (Ticking.Count > 0 && Ticking.Contains(a)) throw new InvalidOperationException("raven npc fail: a part that throws"); });
            NpcActor.Acts[Whose] = (a, w) => { throw new InvalidOperationException("raven npc fail: an act that throws"); };
            NpcMind.TaskOf chore = NpcMind.Tasks[NpcMind.Task.Chore];
            Func<NpcMind, NpcMind.Plan, float, bool> step = chore.Step;
            chore.Step = (m, p, dt) => { if (p.Whose == Whose) throw new InvalidOperationException("raven npc fail: a thought that throws"); return step(m, p, dt); };
        }

        internal static string Cmd(NpcActor who, string[] a)
        {
            if (who == null) return "raven npc fail <хто> tick|mind|act|off";
            string busy = NpcKit.Busy(who); if (busy != null) return busy;
            switch (a.Length > 3 ? a[3] : "")
            {
                case "tick": Ticking.Add(who); return "Частина ламається на ньому щоразу, як їй дозволено (off — припинити).";
                case "mind": return who.Mind != null && who.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Chore }, 30f, Whose, true) ? "Думка зламається на цьому плані." : "Ні.";
                case "act": who.Act(Whose); return "Дія зламалась (дивись лог).";
                case "off": Ticking.Remove(who); return "Більше не ламається.";
            }
            return "raven npc fail <хто> tick|mind|act|off";
        }
    }
}
#endif

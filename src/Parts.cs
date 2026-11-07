#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A row of named parts run in turn by a clock (the places' modules, the story's modules): a part that throws is said in the log with its
    // name and rests PauseFor s, the others go on. Without this one part's fault each frame stops all that comes after it, and the
    // step's own guard (RavenSkills.Fault) says so once in five minutes. A part that needs no player: its Action overload.
    internal sealed class Parts
    {
        private sealed class Part { internal string Name; internal Action<Player> Run; internal float RestUntil; }
        internal const float PauseFor = 30f;

        private readonly string _who;
        private readonly List<Part> _parts = new List<Part>();

        internal Parts(string who) { _who = who; }

        internal void Add(string name, Action<Player> run) { _parts.Add(new Part { Name = name, Run = run }); }
        internal void Add(string name, Action run) { _parts.Add(new Part { Name = name, Run = p => run() }); }

        internal void Run(Player p)
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part part = _parts[i];
                if (Time.time < part.RestUntil) continue;
                try { part.Run(p); }
                catch (Exception e)
                {
                    part.RestUntil = Time.time + PauseFor;
                    if (Plugin.Instance != null) Plugin.Instance.Log(_who + ": " + part.Name + " failed, rests " + PauseFor + " s: " + e);
                }
            }
        }
    }
}
#endif

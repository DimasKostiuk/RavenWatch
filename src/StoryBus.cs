#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // One road for everything that happens to the player (story-plan.md): the story's «wait for …», its triggers, later
    // the errands too all listen here instead of each being called by name. A thing that happened is raised once where
    // it happens; whoever cares subscribes.
    //
    // Predictable: listeners are called in the order they subscribed; an event raised while another is being handed out
    // waits in line and goes after it (never inside it); a listener that throws is logged and the others still hear it.
    //
    // The names (lower case), arg, value:
    //   any Diary event by its Ev name: death, biome <Biome>, dungeon, sleep, craft, cook, fish, parry, trophyhang …
    //   kill <Prefab> stars(1–) · boss <Prefab> · read (at the stone) · loot <Prefab> n · destroyed <Prefab>
    //   champion <stamp> · trust hugin|munin level · notches "" n · gold "" n
    //   errand_given|errand_done|errand_failed|errand_dropped <id>
    // From: who did it (a player id). Now always you; kept for the shared triggers to come (several players, one quest).
    internal struct StoryEvent
    {
        public string Name, Arg;
        public int Value;
        public Vector3 At;
        public bool HasAt;
        public long From;
        public float Time;

        public override string ToString()
        {
            return Name + (Arg.Length > 0 ? " " + Arg : "") + (Value != 0 ? " " + Value.ToString(CultureInfo.InvariantCulture) : "")
                + (HasAt ? " @" + Mathf.RoundToInt(At.x) + "," + Mathf.RoundToInt(At.z) : "");
        }
    }

    internal static class StoryBus
    {
        private static readonly List<KeyValuePair<string, Action<StoryEvent>>> Listeners = new List<KeyValuePair<string, Action<StoryEvent>>>();
        private static readonly Queue<StoryEvent> Waiting = new Queue<StoryEvent>();
        private static bool _handing;
        internal static readonly List<string> Recent = new List<string>(); // the last ones, for the panel and `raven story events`
        internal static bool Trace;                                         // every event into the BepInEx log too

        // who: a name for the log («story», «errands»…); subscribing twice under one name replaces the first
        internal static void Listen(string who, Action<StoryEvent> f)
        {
            Listeners.RemoveAll(kv => kv.Key == who);
            if (f != null) Listeners.Add(new KeyValuePair<string, Action<StoryEvent>>(who, f));
        }

        internal static void Raise(string name, string arg, int value) { Raise(name, arg, value, Vector3.zero, false); }
        internal static void Raise(string name, string arg, int value, Vector3 at) { Raise(name, arg, value, at, true); }

        private static void Raise(string name, string arg, int value, Vector3 at, bool hasAt)
        {
            if (string.IsNullOrEmpty(name)) return;
            Player p = Player.m_localPlayer;
            StoryEvent e = new StoryEvent
            {
                Name = name.ToLowerInvariant(), Arg = arg ?? "", Value = value, At = at, HasAt = hasAt,
                From = p != null ? p.GetPlayerID() : 0L, Time = UnityEngine.Time.time
            };
            Waiting.Enqueue(e);
            if (_handing) return; // (handed out after the one going now)
            _handing = true;
            try
            {
                int guard = 0;
                while (Waiting.Count > 0 && guard++ < 64) Hand(Waiting.Dequeue());
                if (Waiting.Count > 0) { Story.Log("bus: more than 64 events raised from each other — the rest dropped (a loop?)"); Waiting.Clear(); }
            }
            finally { _handing = false; }
        }

        private static void Hand(StoryEvent e)
        {
            string line = e.ToString();
            Recent.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + line);
            if (Recent.Count > 60) Recent.RemoveAt(0);
            if (Trace) Story.Log("event " + line);
            KeyValuePair<string, Action<StoryEvent>>[] now = Listeners.ToArray(); // (one may unsubscribe while hearing)
            foreach (KeyValuePair<string, Action<StoryEvent>> kv in now)
            {
                try { kv.Value(e); }
                catch (Exception ex) { Story.Log("bus: " + kv.Key + " on «" + line + "»: " + ex); }
            }
        }

        // a script's pattern: "kill Troll,Troll_Elite" · "boss *" · "biome Swamp" · "death" (any arg)
        internal static bool Matches(StoryEvent e, string name, string args)
        {
            if (!string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(args) || args == "*") return true;
            foreach (string a in args.Split(','))
                if (string.Equals(a.Trim(), e.Arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---------------------------------------------------------------- the hooks (one line at each place)
        internal static void FromDiary(Ev type, string arg, int value) { if (type != Ev.Kill) Raise(type.ToString(), arg, value); } // (a kill: Kill below, by prefab)

        internal static void Kill(string prefab, int stars, bool boss)
        {
            Raise("kill", prefab, stars);
            if (boss) Raise("boss", prefab, 0);
        }

        internal static string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Події (останні " + Recent.Count + "), слухачі: ");
            for (int i = 0; i < Listeners.Count; i++) sb.Append(i > 0 ? ", " : "").Append(Listeners[i].Key);
            if (Listeners.Count == 0) sb.Append("—");
            int from = Mathf.Max(0, Recent.Count - 20);
            for (int i = from; i < Recent.Count; i++) sb.Append('\n').Append(Recent[i]);
            return sb.ToString();
        }
    }
}
#endif

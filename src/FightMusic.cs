#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A fight's music while a champion lord stands (the game's own boss music, else its battle music); stopped when it
    // falls or you go.
    internal static class FightMusic
    {
        private static string _name;
        private static bool _on;
        private static readonly FieldInfo Triggered = AccessTools.Field(typeof(MusicMan), "m_triggeredMusic");
        private static readonly MethodInfo StopIt = AccessTools.Method(typeof(MusicMan), "StopMusic");

        private static string Name()
        {
            if (_name != null) return _name;
            _name = "combat";
            if (MusicMan.instance == null) return _name;
            List<string> names = new List<string>();
            foreach (MusicMan.NamedMusic m in MusicMan.instance.m_music)
            {
                if (m == null || string.IsNullOrEmpty(m.m_name)) continue;
                names.Add(m.m_name);
                if (_name == "combat" && m.m_enabled && m.m_clips != null && m.m_clips.Length > 0 && m.m_name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0) _name = m.m_name;
            }
            Errands.Log("fight music: «" + _name + "» (the game has: " + string.Join(", ", names.ToArray()) + ")");
            return _name;
        }

        // every couple of seconds while the fight goes on
        internal static void Keep()
        {
            if (MusicMan.instance == null) return;
            MusicMan.instance.TriggerMusic(Name());
            _on = true;
        }

        internal static void Stop()
        {
            if (!_on) return;
            _on = false;
            try
            {
                if (MusicMan.instance == null) return;
                if (Triggered != null) Triggered.SetValue(MusicMan.instance, null);
                if (StopIt != null) StopIt.Invoke(MusicMan.instance, null);
            }
            catch (Exception e) { Errands.Log("fight music stop: " + e.Message); }
        }
    }
}
#endif

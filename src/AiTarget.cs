#if RAVEN_ITEM
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A monster's target and alert (private in the game): one place for every gift that makes a monster lose someone,
    // take someone, or stay alert (Shadow, the в'язь's murk, Marks' blindness, the dome, the decoy, the arena).
    internal static class AiTarget
    {
        private static readonly FieldInfo Target = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly FieldInfo Static = AccessTools.Field(typeof(MonsterAI), "m_targetStatic");
        private static readonly MethodInfo Alerted = AccessTools.Method(typeof(BaseAI), "SetAlerted", new[] { typeof(bool) });
        private static readonly object[] Off = { false }, On = { true };
        private static readonly Dictionary<int, float> Calmed = new Dictionary<int, float>();

        internal static bool Known { get { return Target != null; } }

        internal static Character Of(MonsterAI ai)
        {
            try { return Target != null && ai != null ? Target.GetValue(ai) as Character : null; }
            catch { return null; }
        }

        internal static void Set(MonsterAI ai, Character c) { if (Target != null && ai != null) Target.SetValue(ai, c); }

        // no target at all (a creature and a thing it went for)
        internal static void Drop(MonsterAI ai)
        {
            Set(ai, null);
            if (Static != null && ai != null) Static.SetValue(ai, null);
        }

        internal static void Alert(MonsterAI ai) { if (Alerted != null && ai != null) Alerted.Invoke(ai, On); }

        // its alert down — at most once in 5 s for one monster: dropped again and again, the game raises it again at
        // once, and every raise plays the creature's alert sound
        internal static void Calm(MonsterAI ai)
        {
            if (Alerted == null || ai == null) return;
            int key = ai.GetInstanceID();
            float last;
            if (Calmed.TryGetValue(key, out last) && Time.time - last < 5f) return;
            if (Calmed.Count > 200) Calmed.Clear();
            Calmed[key] = Time.time;
            Alerted.Invoke(ai, Off);
        }
    }
}
#endif

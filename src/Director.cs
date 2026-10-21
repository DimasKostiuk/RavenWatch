#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // One director over everything the ravens say on their own: the reactions to events (a quick raven, or a line from
    // the shoulder), the talks with each other, the fireside stories and the asking for food. Each of them still keeps
    // its own timer; before speaking it asks the director, and the director says no while
    //   - someone is talking right now (yours or another player's raven, a gift's words too: Cozy.Quieted),
    //   - too little time has passed since the last line said on their own (Gap; an event may come sooner: EventGap),
    //   - the ravens have already said enough lately (PerTenMinutes; events keep a little reserve).
    // A "no" is not lost: the asker simply tries again a little later. What answers your own doing (gifts, strikes, the
    // thought and its answer, feeding, the ritual) never asks: it is spoken at once, and the rest waits for it.
    internal static class Director
    {
        internal enum Voice { Hunger = 0, Shoulder = 1, Fireside = 2, Talk = 2, Event = 3 }

        internal static float Gap = 90f;          // quiet after a line said on their own, from when it has been read
        internal static float EventGap = 20f;     // an event may follow sooner
        internal static int PerTenMinutes = 4;    // lines said on their own in 10 minutes, both ravens together (a talk is one)
        private const int EventReserve = 2;       // events may go a little over that

        private static float _freeAt = -999f;     // when the last line said on their own has been read
        private static readonly List<float> Said = new List<float>();

        // may a raven start talking on its own now?
        internal static bool May(Voice v)
        {
            float now = Time.time;
            if (Cozy.Quieted) return false;
            if (now < _freeAt + (v == Voice.Event ? EventGap : Gap)) return false;
            while (Said.Count > 0 && now - Said[0] > 600f) Said.RemoveAt(0);
            return Said.Count < PerTenMinutes + (v == Voice.Event ? EventReserve : 0);
        }

        // a raven did talk on its own (seconds: how long it takes to read, the whole talk for a talk)
        internal static void Spoke(Voice v, float seconds)
        {
            float now = Time.time;
            Said.Add(now);
            _freeAt = Mathf.Max(_freeAt, now + Mathf.Max(0f, seconds));
            Cozy.Quiet(seconds); // and everything else waits while it is said
        }

        internal static string Describe()
        {
            float now = Time.time;
            while (Said.Count > 0 && now - Said[0] > 600f) Said.RemoveAt(0);
            float left = Mathf.Max(0f, _freeAt + Gap - now);
            return Lang.F("Режисер: за 10 хв сказано {0} з {1}, пауза ще {2} с{3}", Said.Count, PerTenMinutes, Mathf.CeilToInt(left), (Cozy.Quieted ? Lang.T(", зараз хтось говорить") : ""));
        }

        internal static void Reset() { Said.Clear(); _freeAt = -999f; }
    }
}
#endif

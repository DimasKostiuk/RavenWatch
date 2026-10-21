#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The ravens about Odin's errand, from the shoulder (ErrandPatterns): it brought one, a step moved on, a step done,
    // you are in its zone, it is done, you gave it up. The raven who brought it speaks (the other when that one is not
    // on you). One line waits a moment (a raven just back from Odin lands first), and a newer one takes its place.
    // The counts speak only now and then (the director's gate and their own pause); the rest always, if a raven is there.
    internal static class ErrandTalk
    {
        internal static float CountChance = 0.5f, CountPause = 25f;
        private static string _tag;
        private static bool _munin, _important;
        private static float _at, _lastCount = -100f;

        internal static void Given(bool munin) { Queue("errand_given", munin, 5f, true); }
        internal static void Step(bool munin) { Queue("errand_step", munin, 1.5f, true); }
        internal static void Near(bool munin) { Queue("errand_near", munin, 1f, true); }
        internal static void Done(bool munin) { Queue("errand_done", munin, 2f, true); }
        internal static void Refuse(bool munin) { Queue("errand_refuse", munin, 1f, true); }

        internal static void Count(bool munin)
        {
            if (Time.time - _lastCount < CountPause || Random.value >= CountChance) return;
            _lastCount = Time.time;
            Queue("errand_count", munin, 1f, false);
        }

        private static void Queue(string tag, bool munin, float delay, bool important)
        {
            if (_tag != null && _important && !important) return; // (a count never pushes out what matters)
            _tag = tag; _munin = munin; _important = important; _at = Time.time + delay;
        }

        // twice a second (Errands.Tick)
        internal static void Tick()
        {
            if (_tag == null || Time.time < _at) return;
            Player p = Player.m_localPlayer;
            if (p == null || Plugin.Instance == null) { _tag = null; return; }
            if (Time.time > _at + 20f) { _tag = null; return; } // (no raven came to say it in time: the moment is gone)
            if (p.IsDead() || p.IsSleeping() || RavenSpeech.Has(p.gameObject)) return; // someone talking: a little later
            if (!_important && (Senses.Fight || !Director.May(Director.Voice.Event))) { _tag = null; return; }
            bool who = _munin;
            if (!Shoulder.Present(p, who)) who = !who;
            if (!Shoulder.Present(p, who)) return; // nobody on the shoulder yet (still landing): wait
            if (!Patterns.Has(Plugin.Instance.PhraseDir, who, _tag)) { _tag = null; return; }
            string line = Plugin.Instance.ComposeTagged(p, who, _tag);
            _tag = null;
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, who, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }
    }
}
#endif

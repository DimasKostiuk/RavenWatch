#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // The ravens about Odin's errand, from the shoulder (ErrandPatterns): it brought one, a step moved on, a step done,
    // you are in its zone, it is done, you gave it up, it was lost. The raven who brought it speaks (the other when that one is not
    // on you). One line waits a moment (a raven just back from Odin lands first), and a newer one takes its place.
    // The counts speak only now and then (the director's gate and their own pause); the rest always, if a raven is there.
    // (B3, 2026-10-25) A count of things you can count says the numbers ({have} {need} {left}: errand_count_n), Munin
    // more often than Hugin; the last one to go is always said (errand_count_last); coming up to a champion has its own
    // word (errand_champ_near).
    internal static class ErrandTalk
    {
        internal static float CountChanceM = 0.7f, CountChanceH = 0.35f, CountPause = 25f;
        private static string _tag;
        private static bool _munin, _important;
        private static float _at, _wait, _lastCount = -100f;
        private static int _have, _need;

        internal static void Given(bool munin) { Queue("errand_given", munin, 5f, true); }
        internal static void Step(bool munin) { Queue("errand_step", munin, 1.5f, true); }
        internal static void Near(bool munin) { Queue("errand_near", munin, 1f, true); }
        internal static void NearChampion(bool munin) { Queue("errand_champ_near", munin, 1f, true); }
        internal static void Done(bool munin) { Queue("errand_done", munin, 2f, true); }
        internal static void Refuse(bool munin) { Queue("errand_refuse", munin, 1f, true); }
        // lost with a death: the ravens are in the grave, so the word waits until one is back on the shoulder
        internal static void Failed(bool munin) { Queue("errand_failed", munin, 3f, true, 600f); }

        // a count moved on: have of need; numbers: a count of things (kills, finds), said with its numbers
        internal static void Count(bool munin, int have, int need, bool numbers)
        {
            _have = have; _need = need;
            if (numbers && need - have == 1) { _lastCount = Time.time; Queue("errand_count_last", munin, 0.5f, true); return; } // («Останній!»: always, a fight or not)
            if (Time.time - _lastCount < CountPause || Random.value >= (munin ? CountChanceM : CountChanceH)) return;
            Queue(numbers ? "errand_count_n" : "errand_count", munin, 1f, false); // (the pause counts from when it is said)
        }

        private static void Queue(string tag, bool munin, float delay, bool important, float wait = 20f)
        {
            if (_tag != null && _important && !important) return; // (a count never pushes out what matters)
            _tag = tag; _munin = munin; _important = important; _at = Time.time + delay; _wait = wait;
        }

        // twice a second (Errands.Tick)
        internal static void Tick()
        {
            if (_tag == null || Time.time < _at) return;
            Player p = Player.m_localPlayer;
            if (p == null || Plugin.Instance == null) { _tag = null; return; }
            if (Time.time > _at + _wait) { _tag = null; return; } // (no raven came to say it in time: the moment is gone)
            if (p.IsDead() || p.IsSleeping() || RavenSpeech.Has(p.gameObject)) return; // someone talking: a little later
            if (!_important && (Senses.Fight || !Director.May(Director.Voice.Event))) { if (!_tag.StartsWith("errand_count")) _tag = null; return; } // (a count waits the fight out, its numbers kept fresh)
            bool who = _munin;
            if (!Shoulder.Present(p, who)) who = !who;
            if (!Shoulder.Present(p, who)) return; // nobody on the shoulder yet (still landing): wait
            if (!Patterns.Has(Plugin.Instance.PhraseDir, who, _tag) && _tag.StartsWith("errand_count_")) _tag = "errand_count"; // (a file from before the numbers)
            if (!Patterns.Has(Plugin.Instance.PhraseDir, who, _tag)) { _tag = null; return; }
            string line;
            Composer.Extra.Clear();
            try
            {
                Composer.Extra["have"] = _have.ToString();
                Composer.Extra["need"] = _need.ToString();
                Composer.Extra["left"] = Mathf.Max(0, _need - _have).ToString();
                line = Plugin.Instance.ComposeTagged(p, who, _tag);
            }
            finally { Composer.Extra.Clear(); }
            if (_tag.StartsWith("errand_count")) _lastCount = Time.time;
            _tag = null;
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, who, line);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
        }
    }
}
#endif

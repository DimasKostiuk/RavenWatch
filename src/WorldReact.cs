#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // «Світ», the ravens on your shoulder feel the weather and the fire (the user, 2026-10-15):
    //   - by a fire or out in the cold: their feathers puffed up (Fluffed: ShoulderLife asks it once a second, on every
    //     game alike — the heat and the cold are where the player stands);
    //   - the rain stopped (or you came in out of it): now and then a raven shakes the water off (ShoulderLife.ShakeNow,
    //     the sitting system's shake) and may grumble about it;
    //   - the cold comes on: now and then a word about it.
    // Each kind keeps its own pause; the words ask the dialogue director (as the shoulder's own).
    internal static class WorldReact
    {
        internal static float ShakeChance = 0.6f, ShakeSay = 0.3f, ColdSay = 0.3f;
        internal static float PauseMinutes = 10f;
        private static bool _wasWet, _wasCold;
        private static float _shakeFree, _coldFree, _next;

        // puffed up now: warm by a fire, or the cold where they stand
        internal static bool Fluffed(Player pl)
        {
            if (pl == null) return false;
            if (EffectArea.IsPointInsideArea(pl.transform.position, EffectArea.Type.Heat, 1f) != null) return true;
            return EnvMan.instance != null && (EnvMan.IsCold() || EnvMan.IsFreezing()) && !pl.InShelter();
        }

        // From WorldVisit.Schedule (the owner's game, twice a second)
        internal static void Tick(Player p)
        {
            if (p == null || EnvMan.instance == null || Time.time < _next) return;
            _next = Time.time + 1f;
            bool wet = EnvMan.IsWet() && !Senses.Indoors && !Senses.Underground;
            bool cold = (EnvMan.IsCold() || EnvMan.IsFreezing()) && !p.InShelter();
            if (_wasWet && !wet && Time.time >= _shakeFree) Shake(p);
            if (cold && !_wasCold && Time.time >= _coldFree) { _coldFree = Time.time + PauseMinutes * 60f; if (Random.value < ColdSay) Say(p, "world_cold"); }
            _wasWet = wet; _wasCold = cold;
        }

        // out of the rain: one raven (or both) shakes the water off
        private static void Shake(Player p)
        {
            _shakeFree = Time.time + PauseMinutes * 60f;
            bool any = false;
            for (int k = 0; k < 2; k++)
            {
                GameObject model = Shoulder.ModelOf(p, k == 1);
                ShoulderLife life = model != null ? model.GetComponent<ShoulderLife>() : null;
                if (life == null || Random.value >= ShakeChance) continue;
                life.ShakeNow();
                any = true;
            }
            if (any && Random.value < ShakeSay) Say(p, "world_shake");
        }

        private static void Say(Player p, string tag)
        {
            bool h = Shoulder.ModelOf(p, false) != null, m = Shoulder.ModelOf(p, true) != null;
            if ((!h && !m) || !Director.May(Director.Voice.Shoulder)) return;
            bool munin = h && m ? Random.value < 0.5f : m;
            string line = Satiety.Say(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return;
            RavenSkills.SayFromShoulder(p, munin, line);
            Director.Spoke(Director.Voice.Shoulder, RavenSpeech.ReadSeconds(line));
        }
    }
}
#endif

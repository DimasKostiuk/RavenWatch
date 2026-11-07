#if RAVEN_ITEM
using System;
using System.Collections.Generic;

namespace HuginWhispers
{
    // A fight's music while a champion lord stands (the game's own boss music, else its battle music): the fight layer of
    // the audio core (Audio); a scene plays over it, and it is let go when the lord falls or you go. Each Keep renews a
    // short lease, so a fight whose Stop never comes still ends.
    internal static class FightMusic
    {
        internal static float Lease = 10f;
        private static string _name;

        private static string Name()
        {
            if (_name != null) return _name;
            List<string> names = Audio.GameTracks();
            _name = "combat";
            foreach (string n in names) if (n.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0) { _name = n; break; }
            Errands.Log("fight music: «" + _name + "» (the game has: " + string.Join(", ", names.ToArray()) + ")");
            return _name;
        }

        // every couple of seconds while the fight goes on
        internal static void Keep() { Audio.Music(Audio.Fight, AudioLayer.Fight, Name(), Audio.FightFade, Lease, 1f, false, 0f); }

        internal static void Stop() { Audio.MusicOff(Audio.Fight, Audio.FightFade); }
    }
}
#endif

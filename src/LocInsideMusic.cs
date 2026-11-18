#if RAVEN_ITEM && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // A place's own music while the player is in its inside (the user 2026-11-15, the Watch's crypt: «тихо грає на фоні, доки ми у тому
    // склепі; коли вийдемо — звичайні мелодії гри»). Its line «insidemusic: <sound> [vol V]» (locations.list; the sound a track of the
    // catalogue, played in a loop). The audio core's layer Place: over the game's music, under a fight's and a scene's (Audio) — a fight in
    // the crypt brings its own music, a scene its own, and this comes back after them. Out of the inside (or dead): let go, the game's again.
    // Cost: once every Every s — a look whether he is in an inside of ours (LocInside.PlaceOf: our places with insides, a box test each).
    internal static class LocInsideMusic
    {
        internal static float Every = 0.5f, Fade = 3f;
        private static float _next;
        private static string _on = ""; // session: the place whose music is asked now ("": none)
        private static ZNet _world;     // (the world left, the audio core let everything go: asked anew here)

        internal static void Tick(Player p)
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            if (ZNet.instance != _world) { _world = ZNet.instance; _on = ""; }
            Loc l = p != null && !p.IsDead() ? LocInside.PlaceOf(p.transform.position) : null;
            string want = l != null && l.InsideMusic.Length > 0 ? l.Id : "";
            if (want == _on) return;
            if (_on.Length > 0) Audio.MusicOff(Audio.Place, Fade);
            _on = want;
            if (want.Length > 0) Audio.Music(Audio.Place, AudioLayer.Place, l.InsideMusic, Fade, 0f, l.InsideMusicVol, false, 0f);
        }
    }
}
#endif

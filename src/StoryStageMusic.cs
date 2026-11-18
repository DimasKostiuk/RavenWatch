#if RAVEN_ITEM && RAVEN_STORY
using System;
using UnityEngine;

namespace HuginWhispers
{
    // A quest stage's own music while it stands for this player (the user 2026-11-15, the rite: the track of the oath fades with its scene,
    // «далі не починається новий трек — тиша», through the fight, till the golden fire's scene). The stage's line:
    //   music: off                  real silence: the game puts nothing in, nor does a fight's music (AudioLayer.Stage is over Fight)
    //   music: <track> [vol V]      a track of the catalogue, in a loop
    // A scene's music is over it, and what the scene leaves (its end, its fade) falls back to this. The stage left (the next one has none,
    // the quest done, the player dead): let go, the game's again. Two stages of one word in a row: the same, nothing changes between them.
    // Cost: once every Every s — the active quests' stages (StoryQuests.StageNow), a word compared.
    internal static class StoryStageMusic
    {
        internal static float Every = 0.5f, Fade = 3f;
        private static float _next;
        private static string _on = ""; // session: the words asked now ("": none)
        private static ZNet _world;     // (the world left, the audio core let everything go: asked anew here)

        internal static void Tick(Player p)
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            if (ZNet.instance != _world) { _world = ZNet.instance; _on = ""; }
            string want = "";
            if (p != null && !p.IsDead())
                foreach (StoryQuests.QuestDef d in StoryQuests.Quests)
                {
                    StoryQuests.StageDef s = StoryQuests.StageNow(p, d);
                    if (s != null && s.Music.Length > 0) { want = s.Music; break; }
                }
            if (want == _on) return;
            _on = want;
            if (want.Length == 0) { Audio.MusicOff(Audio.Stage, Fade); return; }
            string[] w = want.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int vi = Array.IndexOf(w, "vol");
            float vol = vi > 0 && vi + 1 < w.Length ? Mathf.Clamp01(Errands.F(w[vi + 1], 1f)) : 1f;
            Audio.Music(Audio.Stage, AudioLayer.Stage, w[0] == "off" ? null : w[0], Fade, 0f, vol, false, 0f);
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_STORY
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's dome turned inside out, for a scene (chapter 1, the ambush: «над гравцем замикається купол — купол Гугіна навиворіт: тримає
    // всередині», story-lore.md). The dome itself is Hugin's own (Dome: its look, its list sent to everyone near, its wall) — a scene only
    // raises it inside out (Dome.HoldOver: whoever stood inside stays inside, whoever stood outside stays out) and lowers it:
    //   dome on [<place>|player] [m=Radius]   over that spot (none, «player»: over the player who plays the scene); the step does not wait
    //   dome off                              it crumbles (its runes burst)
    // A scene's end lowers it whatever happened (SceneEnd: StoryScenes.Ends). Every game near sees it by the dome's own message (StepSeen.World).
    internal static class StoryDome
    {
        internal static float Radius = 3.5f; // m: round the player, the one killed outside it a few steps off

        internal static void Steps()
        {
            StoryScenes.Register("dome", new StoryScenes.StepKind(StoryScenes.StepSeen.World) { Min = 1, PlaceAt = 2, PlaceNot = new[] { "player" },
                Usage = "dome on [<місце>|player] [м=3.5] · dome off   (купол Гугіна навиворіт: хто всередині, лишається всередині)",
                Check = st => st.W[1] != "on" && st.W[1] != "off" ? "dome on [<місце>|player] [м] · dome off" : st.W[1] == "on" && st.W.Length > 3 && F(st.W[3], -1f) <= 0f ? "dome on: метри — число" : null,
                Begin = Step, Setup = Step,
                Done = (p, st) => st.W[1] == "on" ? "dome inside out over " + (st.W.Length > 2 ? st.W[2] : "player") : null });
        }

        private static void Step(Player p, StoryScenes.Step st)
        {
            if (st.W[1] == "off") { Dome.HoldOff(); return; }
            Vector3 at = p.transform.position;
            if (st.W.Length > 2 && st.W[2] != "player" && !StoryWhere.Find(st.W[2], out at)) { StoryScenes.Note("dome: нема місця «" + st.W[2] + "» — крок пропущено"); return; }
            Dome.HoldOver(at, st.W.Length > 3 ? F(st.W[3], Radius) : Radius);
        }

        // a scene over (played to its end or stopped): no wall left behind
        internal static void SceneEnd(bool broken) { Dome.HoldOff(); }

        private static float F(string s, float or) { float v; return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : or; }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
// (the author's game without the game's intro video: from 2.9 — the user 2026-11-01)
using HarmonyLib;

namespace HuginWhispers
{
    // The game's intro film (Odin and the tree) is not played by itself — at the start nor in a new world — in the
    // author's game only (his blueprints folder: Blueprint.On); players' games as the game made them. Still there to
    // watch from the main menu's cinematics.
    [HarmonyPatch(typeof(CinematicsManager), "Awake")]
    internal static class AuthorNoIntroPatch
    {
        private static void Postfix(CinematicsManager __instance)
        {
            if (!Blueprint.On) return;
            __instance.m_introOnStartup = false;
            __instance.m_introOnNewWorld = false;
        }
    }
}
#endif

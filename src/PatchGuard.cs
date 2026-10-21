using System;
using System.Collections.Generic;

namespace HuginWhispers
{
    // Our patches on the game's own creature, damage, AI and equipment code run for every creature, modded ones too.
    // One of them throwing must never break the game's own method: the patch gives up (the game goes on as without
    // the mod) and the error is written to the log once per patch, with where it came from.
    internal static class PatchGuard
    {
        private static readonly HashSet<string> Told = new HashSet<string>();

        internal static void Fail(string patch, Exception e)
        {
            if (!Told.Add(patch) || Plugin.Instance == null) return;
            Plugin.Instance.LogError("Patch " + patch + " failed and stepped aside (the game goes on as without it; said once): " + e);
        }
    }
}

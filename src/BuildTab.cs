#if RAVEN_ITEM
using System;
using System.Reflection;
using HarmonyLib;

namespace HuginWhispers
{
    // The hammer's own tab «RavenWatch» (the build menu sorted by use): every piece of ours (perches, the nest, the
    // tables) goes there and nowhere else. The game's tabs are the flags of Piece.UsageTagFlags, copied once into each
    // by-use list with their names; one more flag, past the game's own, is added to that copy. A game without such
    // lists (older) simply has no tab. UI mods that redraw the game's tag buttons (SeneaL UI) show it as well.
    internal static class BuildTab
    {
        internal const Piece.UsageTagFlags Flag = (Piece.UsageTagFlags)(1 << 30); // (the game's own end at 1 << 19)
        internal const string Name = "RavenWatch";
    }

    [HarmonyPatch]
    internal static class BuildTabPatch
    {
        private static Type ListType { get { return AccessTools.TypeByName("ByUsagePieceList"); } }

        private static bool Prepare() { return ListType != null; }

        private static MethodBase TargetMethod() { return AccessTools.Constructor(ListType, new[] { typeof(string) }); }

        private static void Postfix(object __instance)
        {
            try
            {
                FieldInfo tags = AccessTools.Field(ListType, "m_usageTags"), names = AccessTools.Field(ListType, "m_usageTagDisplayNames");
                Piece.UsageTagFlags[] t = tags != null ? tags.GetValue(__instance) as Piece.UsageTagFlags[] : null;
                string[] n = names != null ? names.GetValue(__instance) as string[] : null;
                if (t == null || n == null || t.Length != n.Length || Array.IndexOf(t, BuildTab.Flag) >= 0) return;
                Piece.UsageTagFlags[] t2 = new Piece.UsageTagFlags[t.Length + 1];
                string[] n2 = new string[n.Length + 1];
                Array.Copy(t, t2, t.Length); Array.Copy(n, n2, n.Length);
                t2[t.Length] = BuildTab.Flag; n2[n.Length] = BuildTab.Name;
                tags.SetValue(__instance, t2); names.SetValue(__instance, n2);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Build tab: " + e.Message); }
        }
    }
}
#endif

using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A mod that binds worn cloth to the body by bone NAMES (Jotunn for every mod's armour, AdventureBackpacks) searches the whole
    // player, and the raven on the shoulder has bones of the same names (Neck...): a hood put on while a raven sits got
    // tied to the bird and stretched after it when it flew off. Such bones are set back on the body's own: after every
    // piece put on (below), and once more as a raven leaves the shoulder (Shoulder.FlyOff, Unhooked), whoever bound it.
    internal static class SkinGuard
    {
        private static readonly List<Transform> Ravens = new List<Transform>();

        internal static void Fix(VisEquipment vis)
        {
            if (vis == null || vis.m_bodyModel == null) return;
            Transform top = vis.transform;
            Ravens.Clear();
            foreach (ShoulderPin pin in top.GetComponentsInChildren<ShoulderPin>(true)) Ravens.Add(pin.transform);
            if (Ravens.Count == 0) return; // (no raven on this body: nothing to be tied to — most calls end here)
            Dictionary<string, Transform> body = null;
            foreach (SkinnedMeshRenderer smr in top.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr == vis.m_bodyModel || InRaven(smr.transform)) continue; // (the ravens' own skins are theirs)
                Transform[] bones = smr.bones;
                if (bones == null) continue;
                bool changed = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null || !InRaven(bones[i])) continue;
                    if (body == null) body = BodyBones(vis.m_bodyModel);
                    Transform own;
                    if (!body.TryGetValue(bones[i].name, out own)) continue;
                    bones[i] = own; changed = true;
                }
                if (!changed) continue;
                smr.bones = bones;
                if (smr.rootBone != null && InRaven(smr.rootBone)) smr.rootBone = vis.m_bodyModel.rootBone;
                Plugin.Instance.Log("Worn skin " + smr.name + " was tied to a raven's bones: set back on the body");
            }
        }

        private static bool InRaven(Transform t)
        {
            for (int i = 0; i < Ravens.Count; i++) if (Ravens[i] != null && t.IsChildOf(Ravens[i])) return true;
            return false;
        }

        private static Dictionary<string, Transform> BodyBones(SkinnedMeshRenderer body)
        {
            Dictionary<string, Transform> d = new Dictionary<string, Transform>();
            foreach (Transform t in body.bones)
            {
                Transform b = BodyBuild.Real(t); // (its build child stands in its place in the list)
                if (b != null && !d.ContainsKey(b.name)) d[b.name] = b;
            }
            return d;
        }
    }

    // after every other mod's own binding of the piece (theirs are postfixes too)
    [HarmonyPatch]
    internal static class SkinGuardPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (string n in new[] { "SetShoulderEquipped", "SetUtilityEquipped", "SetChestEquipped", "SetLegEquipped", "SetHelmetEquipped", "SetTrinketEquipped" })
            {
                System.Reflection.MethodInfo m = AccessTools.Method(typeof(VisEquipment), n);
                if (m != null) yield return m;
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(VisEquipment __instance, bool __result) { if (!__result) return; try { SkinGuard.Fix(__instance); BodyBuild.Bind(__instance); BodyTint.Bind(__instance); } catch (System.Exception e) { PatchGuard.Fail("SkinGuardPatch", e); } } // (a built body: the new piece onto its build children too)
    }
}

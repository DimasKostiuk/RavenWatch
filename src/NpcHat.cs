#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A raven's hat of ours on a person (the user 2026-11-13: «всі шапочки які ми робили для воронів нехай теж там будуть»): its template's
    // «hat: <id> up forward size tilt» (NpcDef.Hat, HatFit). The hat is the Workshop model made for a raven's head (RavenGear, slot head), built
    // part by part (Perches.Part) on the point the game hangs a helmet on (VisEquipment.m_helmet), so it goes with the head. It fits by itself:
    // as wide as a head (HeadWide), its bottom on the top of the head (HeadTop over that point), the person's own way ahead and up — measured in
    // the skeleton's rest pose (its skin's bind poses), not in whatever pose the head has as the hat goes on: the same on a sleeper, a bow, the
    // look tab's mannequin. The template's fit moves it from there. A look only: every game puts it on its own picture (the template is the
    // mod's on every game). Asked often (the look tab's picture each frame, a person a second): nothing is made unless the hat or its fit changed.
    internal static class NpcHat
    {
        internal static float HeadWide = 0.26f, HeadTop = 0.13f; // m: the hat's width at size 1; its bottom over the helmet's point
        private const string HolderName = "RavenWatch_Hat";
        private static readonly FieldInfo HelmetPoint = AccessTools.Field(typeof(VisEquipment), "m_helmet"), BodyModel = AccessTools.Field(typeof(VisEquipment), "m_bodyModel");
        private static readonly HashSet<string> Missing = new HashSet<string>(); // (a hat not in the mod, or one that failed: said once, not tried again)

        private sealed class Worn : MonoBehaviour { internal string Id = ""; internal Vector4 Fit; }

        // the template's hat on this body (none: off); body: the person's root (its way ahead and up)
        internal static void Wear(VisEquipment vis, Transform body, NpcDef d)
        {
            Transform at = vis != null && HelmetPoint != null ? HelmetPoint.GetValue(vis) as Transform : null;
            if (at == null || body == null) return;
            Transform old = at.Find(HolderName);
            string id = d != null && !d.Dvergr && !d.Odin ? d.Hat : "";
            Worn w = old != null ? old.GetComponent<Worn>() : null;
            if (id.Length == 0 || Missing.Contains(id)) { if (old != null) Object.Destroy(old.gameObject); return; }
            if (w != null && w.Id == id && w.Fit == d.HatFit) return; // (as it is)
            if (old != null) { old.name = "RavenWatch_HatOld"; Object.Destroy(old.gameObject); }
            RavenGear.Item it = null;
            foreach (RavenGear.Item x in RavenGear.Items()) if (x.Id == id) { it = x; break; }
            if (it == null || it.Model == null) { Missing.Add(id); Npcs.Log(d.Id + ": no raven's hat «" + id + "» (assets/gear)"); return; }

            GameObject holder = new GameObject(HolderName);
            try
            {
                holder.transform.SetParent(at, false);
                Place(vis, at, body, holder.transform, d.HatFit);
                Transform inner = new GameObject("model").transform;
                inner.SetParent(holder.transform, false);
                foreach (WsPart p in it.Model.Parts) if (!WsFiles.Special(p.Src)) Perches.Part(inner, p, false); // heavy: once per hat put on or changed
                foreach (Collider c in inner.GetComponentsInChildren<Collider>(true)) Object.Destroy(c); // heavy: once per hat put on (a game piece's own colliders come with it: a hat stops nothing)
                // its box in the holder's frame: as wide as a head, its bottom's middle on the holder
                bool any = false; Bounds b = new Bounds();
                foreach (Renderer r in inner.GetComponentsInChildren<Renderer>(true)) // heavy: once per hat put on
                {
                    if (r == null || r is ParticleSystemRenderer) continue;
                    Bounds wb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 c = holder.transform.InverseTransformPoint(new Vector3(i % 2 == 0 ? wb.min.x : wb.max.x, (i / 2) % 2 == 0 ? wb.min.y : wb.max.y, i / 4 == 0 ? wb.min.z : wb.max.z));
                        if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                    }
                }
                if (any)
                {
                    float wide = Mathf.Max(b.size.x, b.size.z, 0.001f), k = HeadWide * Mathf.Max(0.2f, d.HatFit.z) / wide;
                    inner.localScale = Vector3.one * k;
                    inner.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z) * k;
                }
                int layer = vis.gameObject.layer; // (the look tab's picture draws one layer only)
                foreach (Transform t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; // heavy: once per hat put on
                Worn mark = holder.AddComponent<Worn>(); mark.Id = id; mark.Fit = d.HatFit; // (marked once whole: a half-made one is never kept)
            }
            catch (System.Exception e)
            {
                Object.Destroy(holder);
                Missing.Add(id);
                Npcs.Log(d.Id + ": the raven's hat «" + id + "» not put on: " + e.Message);
            }
        }

        // the holder in the helmet point's frame as it is in the skeleton's rest pose: the person's way ahead (+Z) and up (+Y), HeadTop over the
        // point, then the fit (up, forward, tilt). The rest pose from the body skin's bind poses (the point's bone; the point itself hangs on it
        // unmoved); no skin found: the pose it has now
        private static void Place(VisEquipment vis, Transform at, Transform body, Transform holder, Vector4 fit)
        {
            Matrix4x4 pointRest; // the point in the body's frame (its root's), at rest
            if (!RestOf(vis, at, body, out pointRest)) pointRest = body.worldToLocalMatrix * at.localToWorldMatrix;
            Vector3 pos = (Vector3)pointRest.GetColumn(3) + Vector3.up * (HeadTop + fit.x) + Vector3.forward * fit.y;
            Matrix4x4 local = pointRest.inverse * Matrix4x4.TRS(pos, Quaternion.Euler(fit.w, 0f, 0f), Vector3.one);
            holder.localPosition = local.GetColumn(3);
            holder.localRotation = local.rotation;
            holder.localScale = Vector3.one;
        }

        private static bool RestOf(VisEquipment vis, Transform at, Transform body, out Matrix4x4 pointRest)
        {
            pointRest = Matrix4x4.identity;
            SkinnedMeshRenderer skin = BodyModel != null ? BodyModel.GetValue(vis) as SkinnedMeshRenderer : null;
            if (skin == null || skin.sharedMesh == null) return false;
            Transform[] bones = skin.bones;
            Matrix4x4[] bind = skin.sharedMesh.bindposes;
            if (bones == null || bind == null || bind.Length != bones.Length) return false;
            Matrix4x4 chain = Matrix4x4.identity; // from the nearest bone down to the point, as they hang
            for (Transform t = at; t != null; t = t.parent)
            {
                int i = System.Array.IndexOf(bones, t);
                if (i >= 0)
                {
                    Matrix4x4 meshToBody = body.worldToLocalMatrix * skin.transform.localToWorldMatrix; // (the mesh's frame in the body's: fixed, not animated)
                    pointRest = meshToBody * bind[i].inverse * chain;
                    return true;
                }
                chain = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * chain;
            }
            return false;
        }
    }
}
#endif

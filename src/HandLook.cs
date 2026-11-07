#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A thing's look for a hand — never the thing itself (nothing of it lives, falls, is picked up or saved):
    //   Grip(item)        what the game itself puts in a hand when the item is equipped (its «attach» child and its
    //                     «equipoffset», VisEquipment's way): a shield sits on the forearm, a sword in the fist
    //   Copy(item, size)  its meshes only, centred, no bigger than size m (0 — as it is): for things never held in the
    //                     game (a letter, a charm, a bite of food), and for a thing a clip moves (shadow: it casts one)
    //   Make(item, size)  the game's grip when it has one (and no size is asked), else the copy
    // Used by a person at a meal (NpcEat) and the story's props (StoryProps).
    internal static class HandLook
    {
        internal static GameObject Make(string item, float size)
        {
            GameObject g = size <= 0f ? Grip(item) : null;
            return g ?? Copy(item, size);
        }

        internal static GameObject Grip(string item)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
            if (prefab == null) return null;
            Transform attach = null;
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                Transform c = prefab.transform.GetChild(i);
                if (c.name == "attach") { attach = c; break; } // (not «attach_skin»: worn on the body, not held)
            }
            if (attach == null) return null;
            GameObject top = new GameObject("RavenWatch_handLook");
            GameObject g = Object.Instantiate(attach.gameObject);
            g.SetActive(true);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) c.enabled = false; // heavy: once per look made
            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>(true)) Object.Destroy(rb); // heavy: once per look made
            g.transform.SetParent(top.transform, false);
            g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
            Transform off = prefab.transform.Find("equipoffset");
            if (off != null) { g.transform.localPosition += off.position; g.transform.localRotation *= off.rotation; } // (as VisEquipment.AttachItem)
            return top;
        }

        internal static GameObject Copy(string item, float size, bool shadow = false)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item) : null;
            if (prefab == null) return null;
            List<Renderer> use = new List<Renderer>();
            LODGroup lod = prefab.GetComponentInChildren<LODGroup>(true);
            if (lod != null && lod.enabled && lod.GetLODs().Length > 0) use.AddRange(lod.GetLODs()[0].renderers); // (an item of ours has the game's shape switched off and its own in place: WsItems.Dress)
            else use.AddRange(prefab.GetComponentsInChildren<MeshRenderer>(true)); // heavy: once per look made
            GameObject root = new GameObject("look");
            Matrix4x4 inv = prefab.transform.worldToLocalMatrix;
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer rd in use)
            {
                MeshRenderer mr = rd as MeshRenderer;
                MeshFilter mf = mr != null && mr.enabled ? mr.GetComponent<MeshFilter>() : null;
                if (mf == null || mf.sharedMesh == null) continue;
                GameObject part = new GameObject("part");
                part.transform.SetParent(root.transform, false);
                Matrix4x4 m = inv * mr.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                part.transform.localScale = new Vector3(m.GetColumn(0).magnitude, m.GetColumn(1).magnitude, m.GetColumn(2).magnitude);
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer r = part.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                Bounds pb = mf.sharedMesh.bounds;
                Vector3 c = m.MultiplyPoint3x4(pb.center), e = Vector3.Scale(pb.extents, part.transform.localScale);
                Bounds w = new Bounds(c, e * 2f);
                if (!any) { b = w; any = true; } else b.Encapsulate(w);
            }
            if (!any) { Object.Destroy(root); return null; }
            // centred on the hand; no bigger than asked (a whole fish, a pie: no bigger than the hand holds)
            float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float s = size > 0f && big > 0.001f ? Mathf.Min(1f, size / big) : 1f;
            GameObject top = new GameObject("RavenWatch_handLook");
            root.transform.SetParent(top.transform, false);
            root.transform.localScale = Vector3.one * s;
            root.transform.localPosition = -b.center * s;
            return top;
        }
    }
}
#endif

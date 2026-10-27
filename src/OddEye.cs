#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // One eye of another colour («oddeye» of a look, set in «Фарби»; no breed has one). Both eyes share one picture, so no
    // paint can tell them apart: a small glowing bead sits over one eye instead, a child of the head bone, where LookDiag
    // measured that eye. Known limit: the eye's own vertices bend with more than that one bone, so on a turned head the
    // bead drifts off the eye; a true odd eye needs the eye's triangles skinned on the raven's own bones.
    internal static class OddEye
    {
        internal const string Name = "RavenWatch_OddEye";

        // puts it on (or takes it off: on false); cheap when it is already there in that colour and size
        internal static void Set(GameObject raven, bool munin, bool on, Color color, float size)
        {
            if (raven == null) return;
            Transform old = Find(raven);
            if (!on) { if (old != null) Object.Destroy(old.gameObject); return; }
            LookDiag.EyeSpot[] eyes = LookDiag.Eyes(munin);
            Transform head = RavenGear.Bone(raven, LookDiag.HeadBone);
            if (eyes == null || head == null) { if (old != null) Object.Destroy(old.gameObject); return; }
            LookDiag.EyeSpot e = eyes[0];
            float d = e.Size * size;
            if (old != null)
            {
                Renderer r0 = old.GetComponent<Renderer>();
                if (old.parent == head && r0 != null && r0.sharedMaterial != null && r0.sharedMaterial.color == color && Mathf.Abs(old.localScale.x - d) < 1e-5f) return;
                Object.Destroy(old.gameObject);
            }
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = Name;
            Collider c = g.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            g.transform.SetParent(head, false);
            g.transform.localPosition = e.At;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one * d;
            g.layer = raven.layer;
            Renderer r = g.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = MatOf(color);
        }

        internal static Transform Find(GameObject raven)
        {
            foreach (Transform t in raven.GetComponentsInChildren<Transform>(true)) if (t.name == Name) return t;
            return null;
        }

        // one unlit material per colour (a bright bead: the eye seems to glow), kept for the game's life
        private static readonly System.Collections.Generic.Dictionary<Color, Material> Mats = new System.Collections.Generic.Dictionary<Color, Material>();
        private static Material MatOf(Color color)
        {
            Material m;
            if (Mats.TryGetValue(color, out m) && m != null) return m;
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            m = new Material(sh);
            m.color = color;
            m.renderQueue = 3100;
            Mats[color] = m;
            return m;
        }
    }
}
#endif

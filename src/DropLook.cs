#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A raven dropped on the ground looks like a raven: the vanilla feathers the item is built from are hidden
    // and a small copy of the real Hugin or Munin sits in their place. No model of our own is needed.
    internal static class DropLook
    {
        internal static float Scale = 0.3f;
        private const string Name = "RavenWatch_DropRaven";
        private static readonly List<ItemDrop> Waiting = new List<ItemDrop>();
        private static float _next;

        internal static void Dress(ItemDrop drop)
        {
            if (drop == null || drop.transform.Find(Name) != null) return;
            bool munin;
            if (!RavenItem.IsRaven(drop.m_itemData, out munin))
            {
                // a fresh drop may not know its prefab yet: go by the object's name
                string n = drop.gameObject.name;
                if (n.StartsWith(RavenItem.MuninPrefab)) munin = true;
                else if (!n.StartsWith(RavenItem.HuginPrefab)) return;
            }
            Raven source = Shoulder.Template(munin);
            if (source == null || source.m_visual == null) { if (!Waiting.Contains(drop)) Waiting.Add(drop); return; }

            foreach (Renderer r in drop.GetComponentsInChildren<Renderer>(true)) r.enabled = false; // the feathers
            GameObject go = Object.Instantiate(source.m_visual, drop.transform, false);
            go.name = Name;
            go.SetActive(true);
            AppearMute.Quiet(go); // (a raven lying as a figure: no appearing each time it is shown)
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c); // the feathers keep theirs for picking up
            Shared.RemoveExclamation(source, go);
            Vector3 want = source.m_visual.transform.lossyScale * Scale;
            Vector3 parent = drop.transform.lossyScale;
            go.transform.localScale = new Vector3(want.x / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), want.y / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), want.z / Mathf.Max(0.0001f, Mathf.Abs(parent.z)));
            go.transform.rotation = Quaternion.Euler(0f, drop.transform.eulerAngles.y, 0f); // upright, whatever way the drop landed
            go.transform.position = drop.transform.position;
            go.AddComponent<RavenUpright>();
            Shoulder.Perch(go.GetComponentInChildren<Animator>());
        }

        // ravens dropped before any raven was loaded get dressed a moment later
        internal static void Tick()
        {
            if (Waiting.Count == 0 || Time.time < _next) return;
            _next = Time.time + 2f;
            for (int i = Waiting.Count - 1; i >= 0; i--)
            {
                ItemDrop d = Waiting[i];
                Waiting.RemoveAt(i);
                if (d != null) Dress(d);
            }
        }
    }

    // the dropped item rolls and tumbles with its physics: its raven keeps sitting upright on top of it
    internal class RavenUpright : MonoBehaviour
    {
        private void LateUpdate()
        {
            Transform p = transform.parent;
            if (p == null) return;
            transform.rotation = Quaternion.Euler(0f, p.eulerAngles.y, 0f);
            transform.position = p.position;
        }
    }

    [HarmonyPatch(typeof(ItemDrop), "Start")]
    internal static class DropLookPatch
    {
        private static void Postfix(ItemDrop __instance)
        {
            if (!RavenItem.Enabled) return;
            try { DropLook.Dress(__instance); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Dropped raven look failed: " + e.Message); }
        }
    }
}
#endif

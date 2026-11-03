#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The cloth a person wears (a cape: the game's MagicaCloth — Unity's Cloth for any other — simulated on its own
    // against the pose the game gave the bones).
    // When our own code poses the body far from the game's — it falls and lies (NpcFall), later a scene, a mocap clip — the
    // cloth keeps to the old pose and stands up stiff out of a lying body (the user saw Torvald's cape, 2026-11-01). So
    // while any such pose holds the body, its cloth is let go (a plain skinned mesh then: it follows our bones), and taken
    // up again, its motion cleared (no whip), once the last one lets the body go. Any code posing a body: Hold(body, itself,
    // true) every frame it poses, Hold(body, itself, false) when it stops. Where it is seen only: nothing is sent.
    internal static class NpcCloth
    {
        private sealed class Held { public readonly HashSet<object> By = new HashSet<object>(); public readonly List<Component> Off = new List<Component>(); public float Look; }
        private static readonly Dictionary<GameObject, Held> Bodies = new Dictionary<GameObject, Held>();

        internal static void Hold(GameObject body, object by, bool on)
        {
            if (body == null || by == null) return;
            Held h;
            if (on)
            {
                if (!Bodies.TryGetValue(body, out h)) { if (Bodies.Count > 64) Tidy(); Bodies[body] = h = new Held(); }
                h.By.Add(by);
                if (Time.time >= h.Look) { h.Look = Time.time + 1f; LetGo(body, h); } // (new clothes put on meanwhile: theirs too)
                return;
            }
            if (!Bodies.TryGetValue(body, out h) || !h.By.Remove(by) || h.By.Count > 0) return;
            Bodies.Remove(body);
            foreach (Component c in h.Off)
            {
                if (c == null) continue;
                Cloth u = c as Cloth;
                if (u != null) { u.enabled = true; u.ClearTransformMotion(); }
                Behaviour b = c as Behaviour;
                if (b != null) b.enabled = true;
            }
        }

        private static void LetGo(GameObject body, Held h)
        {
            foreach (Cloth c in body.GetComponentsInChildren<Cloth>())
                if (c != null && c.enabled) { c.enabled = false; h.Off.Add(c); }
            foreach (Behaviour b in body.GetComponentsInChildren<Behaviour>()) // (the game's capes are MagicaCloth — found by its name: no reference to its library)
                if (b != null && b.enabled && b.GetType().Name == "MagicaCloth") { b.enabled = false; h.Off.Add(b); }
        }

        // bodies gone from the world: forgotten
        private static void Tidy()
        {
            List<GameObject> gone = new List<GameObject>();
            foreach (GameObject g in Bodies.Keys) if (g == null) gone.Add(g);
            foreach (GameObject g in gone) Bodies.Remove(g);
        }
    }
}
#endif

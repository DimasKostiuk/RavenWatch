#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person is not drawn by another mod's camera that stands right by it, while the bag is open.
    // Path of Valheiman draws the world behind the doll on its character sheet with a camera of its own: 0.6 m before the
    // player's eyes, looking ahead (PassiveTree.Gear.Portrait.Scenery). A person's gear is opened from arm's length, so that
    // camera stood almost in the person's face and a giant filled the sheet (the user's picture, 2026-11-08). Any mod's
    // picture of the world made this way is the same case, so no mod is named here: the rule is the camera, not who made it.
    // The game's own camera, and any camera while the bag is shut (our stage, the look window's preview, a scene), are left alone.
    // Only for the one render: hidden before that camera culls, back straight after it; nothing is written, nothing goes to the net.
    // Our own cameras may name one person they must not see (Away): the stage's key sheet films a mannequin while the take before plays
    // on a second one 1.4 m aside, and that one stood between the side camera and the body measured (2026-11-10, basket_set).
    internal static class NpcForeignCamera
    {
        private static NpcActor _away; private static List<Camera> _awayBy; // (one person unseen by these cameras, till Away(null, null))
        internal static void Away(NpcActor a, List<Camera> by) { _away = a; _awayBy = a != null ? by : null; }
        internal const float Near = 4.5f; // m from that camera to the person's chest: as far as a person can be used from (the game's reach, 5, less PoV's 0.6)
        private const float ChestHeight = 1f;

        private sealed class Body { internal int Count = -1; internal Renderer[] Parts = new Renderer[0]; }
        private static readonly Dictionary<NpcActor, Body> Bodies = new Dictionary<NpcActor, Body>();
        private static readonly List<Renderer> Off = new List<Renderer>();

        internal static void Wire()
        {
            Camera.onPreCull += Before;
            Camera.onPostRender += After;
            NpcActor.Unloaded.Add(a => Bodies.Remove(a));
        }

        private static void Before(Camera cam)
        {
            try
            {
                Back(); // (a render that never reached its end gave nothing back: no camera after it may find the person hidden)
                if (_away != null && _awayBy != null && cam != null && _awayBy.Contains(cam)) // (one of ours: only the person it was told of)
                {
                    foreach (Renderer r in PartsOf(_away)) if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; Off.Add(r); }
                    return;
                }
                if (NpcActor.Live.Count == 0 || cam == null || cam.cameraType != CameraType.Game || !InventoryGui.IsVisible()) return; // (each camera each frame: this line is all it costs with the bag shut)
                if (GameCamera.instance == null || cam.gameObject == GameCamera.instance.gameObject) return;
                Vector3 at = cam.transform.position;
                float most = Near * Near;
                foreach (NpcActor a in NpcActor.Live)
                {
                    if (a == null || (a.transform.position + Vector3.up * ChestHeight - at).sqrMagnitude > most) continue;
                    foreach (Renderer r in PartsOf(a))
                        if (r != null && !r.forceRenderingOff) { r.forceRenderingOff = true; Off.Add(r); }
                }
            }
            catch (System.Exception e) { Failed(e); }
        }

        private static void After(Camera cam)
        {
            try { Back(); }
            catch (System.Exception e) { Failed(e); }
        }

        private static void Back()
        {
            if (Off.Count == 0) return;
            foreach (Renderer r in Off) if (r != null) r.forceRenderingOff = false;
            Off.Clear();
        }

        // what the body is made of now: looked for again only when it changed (dressed, or a piece gone)
        private static Renderer[] PartsOf(NpcActor a)
        {
            Body b;
            if (!Bodies.TryGetValue(a, out b)) { b = new Body(); Bodies[a] = b; }
            int count = a.transform.hierarchyCount;
            bool gone = false;
            foreach (Renderer r in b.Parts) if (r == null) { gone = true; break; }
            if (count != b.Count || gone)
            {
                b.Parts = a.GetComponentsInChildren<Renderer>(true); // heavy: only for a person within Near of another mod's camera with the bag open, and only when its body changed
                b.Count = count;
            }
            return b.Parts;
        }

        // never a line for every camera of every frame, and never a person left hidden
        private static void Failed(System.Exception e)
        {
            Camera.onPreCull -= Before;
            Camera.onPostRender -= After;
            foreach (Renderer r in Off) { try { if (r != null) r.forceRenderingOff = false; } catch { } } // quiet: this is the last way out, the reason is written below
            Off.Clear();
            Npcs.Log("hiding people from other mods' cameras stopped: " + e);
        }
    }
}
#endif

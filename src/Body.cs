using System;
using UnityEngine;

namespace HuginWhispers
{
    // Points on a creature's body, safe for any creature. The game's own (GetHeadPoint, GetEyePoint, GetCenterPoint,
    // GetRadius) read the head, eye or collider straight from the prefab: a modded creature (or a few odd ones in
    // dungeons) may lack one, and the call throws. Once that stopped every strike of Hugin; in a per-frame look it
    // would throw every frame. Each falls back to something sensible over the creature's feet.
    internal static class Body
    {
        internal static Vector3 Center(Character c)
        {
            if (c == null) return Vector3.zero;
            try { return c.GetCenterPoint(); }
            catch (Exception) { return c.transform.position + Vector3.up; }
        }

        internal static Vector3 Head(Character c)
        {
            if (c == null) return Vector3.zero;
            try { return c.GetHeadPoint(); }
            catch (Exception) { return Center(c) + Vector3.up * 0.5f; }
        }

        internal static Vector3 Eye(Character c)
        {
            if (c == null) return Vector3.zero;
            try { return c.GetEyePoint(); }
            catch (Exception) { return Head(c); }
        }

        internal static float Radius(Character c)
        {
            if (c == null) return 0.5f;
            try { return c.GetRadius(); }
            catch (Exception) { return 0.5f; }
        }
    }
}

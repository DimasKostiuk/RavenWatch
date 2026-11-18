using System;
using UnityEngine;

namespace HuginWhispers
{
    // Points on a creature's body, safe for any creature. The game's own (GetHeadPoint, GetEyePoint, GetCenterPoint,
    // GetRadius) read the head, eye or collider straight from the prefab: a modded creature (or a few odd ones in
    // dungeons) may lack one, and the call throws. Once that stopped every strike of Hugin; in a per-frame look it
    // would throw every frame. Each falls back to something sensible over the creature's feet. The head is asked of the game's field
    // (m_head), not of its call: a body without one asked every frame would throw every frame (HeadBone: the skeleton's, to keep).
    internal static class Body
    {
        internal static Vector3 Center(Character c)
        {
            if (c == null) return Vector3.zero;
            try { return c.GetCenterPoint(); }
            catch (Exception) { return c.transform.position + Vector3.up; }
        }

        // its head: the game's own (m_head, from a humanoid animator); a body with none the game knows — over its middle. Asked, not thrown and
        // caught: a body without a head asked each frame (a mark, a raven on its head) would throw each frame (2026-11-17: Lit, the dvergr — his
        // animator is not humanoid; the Hall's raven landing on his head threw, and its place's raven stopped for 30 s each time)
        private static readonly System.Reflection.FieldInfo HeadField = HarmonyLib.AccessTools.Field(typeof(Character), "m_head");
        internal static Vector3 Head(Character c)
        {
            if (c == null) return Vector3.zero;
            if (HeadField == null) { try { return c.GetHeadPoint(); } catch (Exception) { return Center(c) + Vector3.up * 0.5f; } } // quiet: the game renamed its field — its own call, as before, its throw caught as before
            Transform h = HeadField.GetValue(c) as Transform;
            return h != null ? h.position : Center(c) + Vector3.up * 0.5f;
        }

        // its head's bone, to keep and follow: the game's own, else its skeleton's (a player's or a person's body: a dvergr's too), else null.
        // Heavy for a body without the game's: its bones looked through — ask it once (a raven landing on it), not each frame
        internal static Transform HeadBone(Character c)
        {
            if (c == null) return null;
            Transform h = HeadField != null ? HeadField.GetValue(c) as Transform : null;
#if RAVEN_ITEM
            if (h == null) h = BodyClips.SkeletonBone(c, c.transform, "Head");
#endif
            return h;
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

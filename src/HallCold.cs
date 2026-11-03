#if RAVEN_ITEM && RAVEN_NPC
// (the abandoned Hall's cold: from 2.9 — the user 2026-11-02)
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Hel holds the Hall till it is kindled (HallState: abandoned, its step «гейд» too; her blue fire warms nobody): inside it you are cold,
    // the game's own Cold. Inside = over one of its floors and under its roof, by the blueprint (the roof has holes in it:
    // the game's own shelter check would not do). The game decides the rest: a real fire by you, frost resistance.
    internal static class HallCold
    {
        private const float Every = 1f, FloorNear = 1.5f, RoofNear = 3f;
        private static float _next;
        private static Vector3 _spot, _middle; // the world's Hall: where the world says it is, and its real middle (from a piece of it)
        private static bool _hasMiddle;
        private static Loc _shapeOf; // (its floors and roofs from the blueprint, read once per place)
        private static List<Vector3> _floors, _roofs;
        internal static bool Inside;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Player p = Player.m_localPlayer;
            Inside = p != null && !HallState.Kindled && In(p.transform.position);
        }

        private static bool In(Vector3 p)
        {
            Loc l = Locs.Hall;
            if (l == null) return false;
            Vector3 c; Quaternion rot; string of;
            if (HallParts.AuthorFrame(p, out of, out c, out rot) && of == l.Id && Under(l, Quaternion.Inverse(rot) * (p - c))) return true; // (the author's own)
            return l.From(p) <= 0f && Middle(l, out c, out rot) && Under(l, Quaternion.Inverse(rot) * (p - c));
        }

        private static bool Middle(Loc l, out Vector3 c, out Quaternion rot)
        {
            c = Vector3.zero;
            Vector3 at;
            if (!l.Where(out at, out rot)) return false;
            if (!_hasMiddle || at != _spot)
            {
                List<ZNetView> all = HallParts.All(l.Id);
                if (all.Count == 0) return false;
                _middle = all[0].transform.position - rot * all[0].GetZDO().GetVec3(HallParts.KAt, Vector3.zero);
                _spot = at; _hasMiddle = true;
            }
            c = _middle;
            return true;
        }

        private static bool Under(Loc l, Vector3 lp)
        {
            if (_shapeOf != l || _floors == null || _roofs == null) { _shapeOf = l; _floors = l.Floors(); _roofs = l.Roofs(); } // (no blueprint yet: asked again)
            return Near(_floors, lp, FloorNear, false) && Near(_roofs, lp, RoofNear, true);
        }

        private static bool Near(List<Vector3> parts, Vector3 lp, float near, bool above)
        {
            if (parts == null) return false;
            foreach (Vector3 q in parts)
            {
                float x = q.x - lp.x, z = q.z - lp.z;
                if (x * x + z * z <= near * near && (above ? q.y > lp.y + 1f : q.y < lp.y + 1f)) return true;
            }
            return false;
        }
    }

    // the game reads its cold flag only here for the player's own effects; the flag itself (EnvMan.IsCold is a bare read, it
    // may be inlined), and only for this call: the sky and everything else stay as they are
    [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
    internal static class HallColdPatch
    {
        private static readonly FieldInfo Flag = AccessTools.Field(typeof(EnvMan), "s_isCold");
        private static bool _was;

        private static void Prefix(Player __instance, out bool __state)
        {
            __state = HallCold.Inside && __instance == Player.m_localPlayer && Flag != null;
            if (!__state) return;
            _was = (bool)Flag.GetValue(null);
            Flag.SetValue(null, true);
        }

        private static void Finalizer(bool __state) { if (__state) Flag.SetValue(null, _was); } // (a finalizer: put back even if the game threw)
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
// (the abandoned Hall's cold: from 2.9 — the user 2026-11-02; any place's «cold:» since 2026-11-06)
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A place whose «cold:» conditions hold (locations.list — the Hall: Hel holds it till it is kindled, her blue fire warms nobody): inside
    // its building you are cold, the game's own Cold. Inside = over one of its floors and under its roof, by the blueprint (the roof has
    // holes in it: the game's own shelter check would not do). The game decides the rest: a real fire by you, frost resistance.
    // Once a second, only the places with the line and only when you stand within their walls.
    internal static class LocCold
    {
        private const float Every = 1f, FloorNear = 1.5f, RoofNear = 3f;
        private static float _next;
        private sealed class Shape { internal Vector3 Spot, Middle; internal bool HasMiddle; internal List<Vector3> Floors, Roofs; }
        private static readonly Dictionary<string, Shape> _shapes = new Dictionary<string, Shape>(); // (each place's floors and roofs from its blueprint, read once)
        internal static bool Inside;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            Player p = Player.m_localPlayer;
            Inside = false;
            if (p == null) return;
            foreach (Loc l in Locs.All)
                if (l.ColdWhile.Length > 0 && In(l, p.transform.position) && StoryCond.Holds(null, l.ColdWhile, e => l.Log("cold: " + e), true)) { Inside = true; return; }
        }

        private static bool In(Loc l, Vector3 p)
        {
            Vector3 c; Quaternion rot; string of;
            if (LocParts.AuthorFrame(p, out of, out c, out rot) && of == l.Id && Under(l, Quaternion.Inverse(rot) * (p - c))) return true; // (the author's own)
            return l.From(p) <= 0f && Middle(l, out c, out rot) && Under(l, Quaternion.Inverse(rot) * (p - c));
        }

        private static Shape Of(Loc l)
        {
            Shape sh;
            if (!_shapes.TryGetValue(l.Id, out sh)) _shapes[l.Id] = sh = new Shape();
            return sh;
        }

        private static bool Middle(Loc l, out Vector3 c, out Quaternion rot)
        {
            c = Vector3.zero;
            Vector3 at;
            if (!l.Where(out at, out rot)) return false;
            Shape sh = Of(l);
            if (!sh.HasMiddle || at != sh.Spot)
            {
                List<ZNetView> all = LocParts.All(l.Id);
                if (all.Count == 0) return false;
                sh.Middle = all[0].transform.position - rot * all[0].GetZDO().GetVec3(LocParts.KAt, Vector3.zero);
                sh.Spot = at; sh.HasMiddle = true;
            }
            c = sh.Middle;
            return true;
        }

        private static bool Under(Loc l, Vector3 lp)
        {
            Shape sh = Of(l);
            if (sh.Floors == null || sh.Roofs == null) { sh.Floors = l.Floors(); sh.Roofs = l.Roofs(); } // (no blueprint yet: asked again)
            return Near(sh.Floors, lp, FloorNear, false) && Near(sh.Roofs, lp, RoofNear, true);
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
    internal static class LocColdPatch
    {
        private static readonly FieldInfo Flag = AccessTools.Field(typeof(EnvMan), "s_isCold");
        private static bool _was;

        private static void Prefix(Player __instance, out bool __state)
        {
            __state = LocCold.Inside && __instance == Player.m_localPlayer && Flag != null;
            if (!__state) return;
            _was = (bool)Flag.GetValue(null);
            Flag.SetValue(null, true);
        }

        private static void Finalizer(bool __state) { if (__state) Flag.SetValue(null, _was); } // (a finalizer: put back even if the game threw)
    }
}
#endif

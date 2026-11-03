#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A scene walks the player to a spot (scene step «player go»): he goes on his own feet, as if by his keys, so the game's
    // walking, slopes and sounds are the game's. Nothing is carried: a thing in the way stops him, and at the end of its
    // time the step gives up and the scene goes on from where he stands, and says how far off that is.
    // What it does to his walking is only for the walk: he goes as he went before it, whichever way it ends (the game keeps
    // the walk switch on until something turns it off).
    // He comes from the side he is on (never round or through the thing): keep the ground round a thing to be taken clear, at
    // least a body's width past where he would stand. «front» makes him go to the side its face looks to:
    // only for a thing whose side matters, and only where nothing stands in between.
    // A clip that follows needs him exactly so before the thing (its body frame is its map), so the step ends only when he
    // stands within Near of the spot and turned within Turned of the way it says: he slows into it, no overshoot, and
    // turns as he comes. The spot is reckoned from what the eye sees, the thing's meshes' middle (StoryProps.Middle): the
    // same point its look is laid on, not its object's root.
    internal static class StoryWalk
    {
        internal static float Most = 7f, Near = 0.06f, Turned = 3f, Slow = 0.45f, TurnSpeed = 150f, Least = 0.25f, Body = 0.3f; // (Near, Slow, Least: as tuned in game on the shield; Body: his width)
        private static readonly FieldInfo FLookYaw = AccessTools.Field(typeof(Character), "m_lookYaw");

        private static Vector3 _to, _way;
        private static float _until;
        private static bool _run, _walked;

        internal static bool On { get; private set; }

        // a note when it cannot, else null. front: on the side the thing's face looks to, turned as it is turned, so what
        // lies before him lies as the clip has it; else from where he comes
        internal static string Go(Player p, string place, float within, bool front, bool run)
        {
            Vector3 at;
            ZNetView piece = StoryWhere.Piece(place);
            if (piece != null) at = StoryProps.Middle(piece);
            else if (!StoryWhere.Find(place, out at)) return "нема місця «" + place + "»";
            Vector3 from = at - p.transform.position; from.y = 0f;
            Vector3 dir = front && piece != null ? piece.transform.forward : from.sqrMagnitude > 0.0001f ? from.normalized : p.transform.forward;
            dir.y = 0f;
            dir.Normalize();
            _way = dir;
            _to = at - dir * Mathf.Max(0.1f, within);
            if (piece != null) CanStand(piece, at, within);
            _until = Time.time + Most;
            _run = run;
            if (!On) _walked = p.GetWalk(); // (a walk after a walk: still the one from before them)
            Gestures.Stand(p); // (not sneaking up to it: the clip after is made standing)
            On = true;
            return null;
        }

        internal static void Stop() { if (On) Done(Player.m_localPlayer); }

        private static void Done(Player p)
        {
            On = false;
            if (p == null) return;
            p.SetMoveDir(Vector3.zero);
            p.SetWalk(_walked);
        }

        // said once a walk: a thing's own touch reaching past where he must stand (a square's corner reaches farther than its side): he would stop against it
        private static void CanStand(ZNetView piece, Vector3 at, float within)
        {
            float most = 0f;
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk starts
            {
                if (!c.enabled || c.isTrigger) continue;
                Vector3 m = c.bounds.center - at; m.y = 0f;
                BoxCollider box = c as BoxCollider;
                Vector3 e = box != null ? Vector3.Scale(box.size, c.transform.lossyScale) * 0.5f : c.bounds.extents; // (a box's own sides: the world's bounds of a turned box overstate it)
                most = Mathf.Max(most, m.magnitude + new Vector2(e.x, e.z).magnitude);
            }
            if (most + Body > within) Story.Log("player go: the thing's touch reaches " + most.ToString("0.00") + " m and his body " + Body + ": he cannot stand at " + within + " on every side — make its collider smaller");
        }

        // right after the player's own keys were read (the patch below): this frame's keys are ours
        internal static void Steer(Player p)
        {
            if (!On || p == null || !ReferenceEquals(p, Player.m_localPlayer)) return;
            Vector3 d = _to - p.transform.position; d.y = 0f;
            float far = d.magnitude;
            Vector3 face = far > Slow ? d / far : _way; // (along the way, then turned as the spot wants)
            Vector3 now = p.transform.forward; now.y = 0f;
            Vector3 turned = Vector3.RotateTowards(now.normalized, face, TurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            if (FLookYaw != null) { FLookYaw.SetValue(p, Quaternion.LookRotation(turned)); p.FaceLookDirection(); }
            float off = Vector3.Angle(now, _way);
            if ((far <= Near && off <= Turned) || Time.time >= _until || p.IsDead())
            {
                Done(p);
                Story.Log("player go: stands " + far.ToString("0.00") + " m from the spot, turned off by " + off.ToString("0") + " deg" + (far > 0.1f || off > 10f ? " — A CLIP THAT FOLLOWS WILL MISS" : ""));
                return;
            }
            p.SetMoveDir(far <= Near ? Vector3.zero : d / far * Mathf.Clamp(far / Slow, Least, 1f)); // (slows into it: the game goes as far as this says)
            p.SetRun(_run && far > 2f);
            p.SetWalk(!_run || far <= 2f);
        }
    }

    [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
    internal static class StoryWalkPatch
    {
        private static void Postfix(Player ___m_character)
        {
            try { if (StoryWalk.On) StoryWalk.Steer(___m_character); }
            catch (Exception e) { PatchGuard.Fail("StoryWalkPatch", e); }
        }
    }
}
#endif

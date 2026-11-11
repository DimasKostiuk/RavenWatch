#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet: the ladder when it gets nowhere (NpcWalk: its head says the order).
    internal sealed partial class NpcWalk
    {
        // a long while meaning to walk and not two metres from where it was: the last safety — home (NpcMind.HomeFromStuck)
        private Vector3 _anchor; private float _anchorAt, _stepAt;
        private bool Stranded(Vector3 me)
        {
            float now = Time.time;
            if (Done) { _anchor = me; _anchorAt = now; _stepAt = now; return false; } // (there — standing by its goal is no being stranded)
            if (now - _stepAt > 2f || (me - _anchor).sqrMagnitude > 4f) { _anchor = me; _anchorAt = now; } // (it stood of its own will meanwhile, or got on: counted afresh)
            _stepAt = now;
            float held = now - _anchorAt;
            if (held < Home) return false;
            if (held < HomeSeen && Watched(me)) return false; // (seen: a while longer — it may still find its way)
            if (_trip != null) End("home from stuck (" + Mathf.RoundToInt(held) + " s)");
            _goal = new Vector3(float.MaxValue, 0f, 0f); _anchorAt = now;
            _m.HomeFromStuck("got nowhere " + Mathf.RoundToInt(held) + " s");
            return true;
        }


        // A step a body does not climb by itself (2026-11-06, measured: the first step of a stone stair, 0.58 m — the person pushed at its foot
        // with no speed till the ladder eased it up six seconds later; a player jumps or is lifted, a person is not): it means to walk, has
        // not moved for StepAfter s, and just ahead the ground stands StepMin…StepMost m higher with room for a body over it — it steps up
        // onto it in a quarter second (the slide every ease-out uses). Counted with the jumps as «ступив на сходинку» (under 0.25 m: none)
        internal static float StepAfter = 0.3f, StepMin = 0.25f, StepMost = 0.75f, StepFor = 0.25f; // … · s the step up takes
        private static readonly float[] Aheads = { 0.35f, 0.5f, 0.65f, 0.8f };
        private float _stepUpNext;
        private bool StepUp(Vector3 me, Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return false;
            dir.Normalize();
            RaycastHit h = default(RaycastHit); float rise = 0f; bool found = false;
            foreach (float ahead in Aheads) // (the edge stands a body's half width or more ahead: one look at 0.45 m saw the floor before it, measured 2026-11-06)
            {
                Vector3 probe = me + dir * ahead + Vector3.up * (StepMost + 0.3f);
                if (!Physics.Raycast(probe, Vector3.down, out h, StepMost + 0.3f - StepMin + 0.05f, Ground, QueryTriggerInteraction.Ignore)) continue; // (nothing that high there)
                rise = h.point.y - me.y;
                if (rise < StepMin || rise > StepMost || h.normal.y < 0.5f) continue; // (no step: flat, a wall. A stair's collider may be a ramp: up to 60° is its tread)
                if (Physics.CheckCapsule(h.point + Vector3.up * 0.45f, h.point + Vector3.up * 1.6f, 0.28f, Solids, QueryTriggerInteraction.Ignore)) continue; // (no room for a body up there)
                if (SomeoneAt(h.point, _m)) continue; // (a person or a player stands there: not into them)
                found = true; break;
            }
            if (!found) return false;
            // (no look for «a wall over the step»: a stair goes on rising there — it refused every stair, measured 2026-11-06; the room for a body up there says enough)
            Vector3 to = h.point + Vector3.up * 0.05f;
            NpcBody body = _m.Body;
            if (body == null) return false;
            _m.StopMoving(); _m.Face(to + dir);
            body.Slide(to, StepFor, "ступив на сходинку"); // (the body's one way: held and drawn every frame, its time real — NpcBody.Slide)
            _askAt = Time.time + StepFor;
            if (_trip != null) Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\tstepped up {3:0.00} m\tat {4}", DateTime.Now, _m.Name(), _trip.What, rise, Npcs.V(me)));
            return true;
        }

        private static bool SomeoneAt(Vector3 p, NpcMind me)
        {
            foreach (NpcActor o in NpcActor.Live) if (o != null && o.gameObject != me.gameObject && Utils.DistanceXZ(o.transform.position, p) < 0.6f && Mathf.Abs(o.transform.position.y - p.y) < 1.5f) return true;
            foreach (Player pl in Player.GetAllPlayers()) if (pl != null && Utils.DistanceXZ(pl.transform.position, p) < 0.7f && Mathf.Abs(pl.transform.position.y - p.y) < 1.5f) return true;
            return false;
        }

        // a step round what holds it: the free side nearest its goal, for a second
        private void StepRound(Vector3 me, Vector3 to)
        {
            Vector3 fwd = to - me; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 best = Vector3.zero; float bd = float.MaxValue;
            foreach (Vector3 t in new[] { (fwd + right).normalized, (fwd - right).normalized, right, -right, -fwd })
            {
                Vector3 p = NpcKit.Floor(me + t * 1.2f);
                if (Mathf.Abs(p.y - me.y) > 0.6f || Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.3f, Solids, QueryTriggerInteraction.Ignore) || Physics.Linecast(me + Vector3.up * 0.9f, p + Vector3.up * 0.9f, Solids, QueryTriggerInteraction.Ignore)) continue; // (no step off a ledge, down a stairwell, through a wall)
                float d = Utils.DistanceXZ(p, to);
                if (d < bd) { bd = d; best = t; }
            }
            if (bd == float.MaxValue) { Halt("held, no room round it", me, fwd); return; }
            _stepDir = best; _stepUntil = Time.time + 1f; _askAt = Time.time + 1f;
            if (_trip != null) _trip.Rounds++;
            Halt("held — steps round", me, fwd);
        }


        // held fast (a nook by a wall, a slope against a house: the body cannot move whichever way it is turned): the
        // nearest free spot a step or so away — room for a body, the map under it, ground at its height — towards its goal
        // first; the body slides there in half a second (a stumbling step, not a jump: NpcBody.Slide)
        private void Unwedge(Vector3 me, Vector3 dir, float reach)
        {
            Vector3 best = me; float bd = float.MaxValue;
            foreach (float r in Steps)
                if (r <= reach) for (int i = 0; i < 8; i++)
                {
                    Vector3 p = NpcKit.Floor(me + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * r);
                    if (Mathf.Abs(p.y - me.y) > 1f || Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.35f, Solids, QueryTriggerInteraction.Ignore) || Physics.Linecast(me + Vector3.up * 0.9f, p + Vector3.up * 0.9f, Solids, QueryTriggerInteraction.Ignore)) continue; // (not through a wall)
                    Vector3 v;
                    if (!NpcKit.SameFloor(out v, p, 0.4f, NpcKit.Walker) || Utils.DistanceXZ(v, p) > 0.3f) continue;
                    float d = r * 2f + Utils.DistanceXZ(p, _goal); // (near first, then towards its goal)
                    if (d < bd) { bd = d; best = p; }
                    if (i == 7 && bd < float.MaxValue) goto found; // (the nearest ring with room: the farther ones not gone over)
                }
            found:
            if (bd == float.MaxValue && reach >= 3f) { Vector3 v0; if (NpcKit.SameFloor(out v0, me, 3f, NpcKit.Walker) && Utils.DistanceXZ(v0, me) > 0.2f) { bd = 0f; best = v0; } } // (not the floor above or below) // (no room for a body anywhere near — wedged among chairs: the nearest spot on the map all the same)
            if (bd == float.MaxValue && Inside(me)) { Free(me); return; } // (in a wall, in a thing, no room round it to slide to: the safety)
            if (bd == float.MaxValue) { if (_trip != null) Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\theld fast, no free spot within reach\tat {3}", DateTime.Now, _m.Name(), _trip.What, Npcs.V(me))); return; }
            float far = Utils.DistanceXZ(me, best), takes = 0.3f + far / 3f; // (a step's slide: a metre in a third of a second)
            NpcBody body = _m.Body;
            if (body == null) return;
            _m.StopMoving();
            Vector3 face = best - me; face.y = 0f;
            if (face.sqrMagnitude > 0.0001f) _m.Face(best + face * 3f);
            body.Slide(best + Vector3.up * 0.05f, takes, "протиснувся");
            _askAt = Time.time + takes; // (its way asked afresh from there)
            if (_trip != null) { _trip.Slides++; Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\theld fast — eased out {3:0.0} m\tat {4} → {5}\tin front: {6}", DateTime.Now, _m.Name(), _trip.What, Utils.DistanceXZ(me, best), Npcs.V(me), Npcs.V(best), Things(me, dir))); }
        }
        private static readonly float[] Steps = { 0.6f, 1f, 1.5f, 2f, 2.5f, 3f }; // (a crowded dining hall: the nearest free spot may be a few steps off)

        // a safe spot by a goal for the safety (NpcMind.Unstick): on the way finder's map within SafeReach m of it, room for a body (nothing built in
        // it, nobody standing on it), no fire (NpcBody.Burns), the floor at its height (not the floor above or below) — the nearest such; none: false
        internal static float SafeReach = 2.5f;
        internal static bool SafeBy(Vector3 to, out Vector3 safe)
        {
            safe = to;
            for (float r = 0.6f; r <= SafeReach; r += 0.6f)
            {
                Vector3 v;
                if (!NpcKit.SameFloor(out v, to, r, NpcKit.Walker)) continue;
                if (Physics.CheckCapsule(v + Vector3.up * 0.55f, v + Vector3.up * 1.45f, 0.3f, Solids, QueryTriggerInteraction.Ignore)) continue;
                if (NpcBody.Burns(v)) continue; // (not into a hearth: room for a body and the map say nothing of fire)
                bool taken = false;
                foreach (NpcActor o in NpcActor.Live) if (o != null && Utils.DistanceXZ(o.transform.position, v) < 0.6f) { taken = true; break; }
                foreach (Player pl in Player.GetAllPlayers()) if (pl != null && Utils.DistanceXZ(pl.transform.position, v) < 0.8f) { taken = true; break; } // (not put into a player either)
                if (taken) continue;
                safe = v + Vector3.up * 0.05f;
                return true;
            }
            return false;
        }

        // its body in something solid (a wall, a rock, a thing: the capsule overlaps it where it stands)
        private static bool Inside(Vector3 me) { return Physics.CheckCapsule(me + Vector3.up * 0.55f, me + Vector3.up * 1.45f, 0.25f, Solids, QueryTriggerInteraction.Ignore); }

        // the safety for a body held inside a thing: to the nearest spot on the way finder's map within FreeReach m that has
        // room for a body — seen, it goes and comes in a puff of smoke; not even that: home (NpcMind.HomeFromStuck)
        internal static float FreeReach = 6f;
        private void Free(Vector3 me)
        {
            Vector3 v;
            bool found = false;
            for (float r = 1.5f; r <= FreeReach && !found; r += 1.5f)
                if (NpcKit.SameFloor(out v, me, r, NpcKit.Walker) && Utils.DistanceXZ(v, me) > 0.3f && !Physics.CheckCapsule(v + Vector3.up * 0.55f, v + Vector3.up * 1.45f, 0.25f, Solids, QueryTriggerInteraction.Ignore)) { found = true; Put(me, v + Vector3.up * 0.05f); }
            if (!found) _m.HomeFromStuck("held inside a thing, nowhere free near");
        }

        private void Put(Vector3 me, Vector3 to)
        {
            if (_m.Body != null) _m.Body.Put(to, "з предмета");
            Forget();
        }
    }
}
#endif

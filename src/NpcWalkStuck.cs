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
        // first; the body slides there in half a second (a stumbling step, not a jump)
        private Vector3 _slideTo;
        private float _slideUntil;
        private void Unwedge(Vector3 me, Vector3 dir, float reach)
        {
            Vector3 best = me; float bd = float.MaxValue;
            foreach (float r in Steps)
                if (r <= reach) for (int i = 0; i < 8; i++)
                {
                    Vector3 p = NpcKit.Floor(me + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * r);
                    if (Mathf.Abs(p.y - me.y) > 1f || Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.35f, Solids, QueryTriggerInteraction.Ignore) || Physics.Linecast(me + Vector3.up * 0.9f, p + Vector3.up * 0.9f, Solids, QueryTriggerInteraction.Ignore)) continue; // (not through a wall)
                    Vector3 v;
                    if (!NpcKit.Valid(out v, p, 0.4f, _m.m_pathAgentType) || Utils.DistanceXZ(v, p) > 0.3f) continue;
                    float d = r * 2f + Utils.DistanceXZ(p, _goal); // (near first, then towards its goal)
                    if (d < bd) { bd = d; best = p; }
                }
            if (bd == float.MaxValue && reach >= 3f) { Vector3 v0; if (NpcKit.Valid(out v0, me, 3f, _m.m_pathAgentType) && Utils.DistanceXZ(v0, me) > 0.2f && Mathf.Abs(v0.y - me.y) < 1f) { bd = 0f; best = v0; } } // (not the floor above or below) // (no room for a body anywhere near — wedged among chairs: the nearest spot on the map all the same)
            if (bd == float.MaxValue && Inside(me)) { Free(me); return; } // (in a wall, in a thing, no room round it to slide to: the safety)
            if (bd == float.MaxValue) { if (_trip != null) Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\theld fast, no free spot within reach\tat {3}", DateTime.Now, _m.Name(), _trip.What, Npcs.V(me))); return; }
            float far = Utils.DistanceXZ(me, best);
            Jumped(_m, me, best, "протиснувся");
            _slideTo = best + Vector3.up * 0.05f; _slideUntil = Time.time + 0.3f + far / 3f; // (a step's slide: a metre in a third of a second)
            _askAt = _slideUntil; // (its way asked afresh from there)
            if (_trip != null) { _trip.Slides++; Write("halts.txt", string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss}\t{1}\t{2}\theld fast — eased out {3:0.0} m\tat {4} → {5}\tin front: {6}", DateTime.Now, _m.Name(), _trip.What, Utils.DistanceXZ(me, best), Npcs.V(me), Npcs.V(best), Things(me, dir))); }
        }
        private static readonly float[] Steps = { 0.6f, 1f, 1.5f, 2f, 2.5f, 3f }; // (a crowded dining hall: the nearest free spot may be a few steps off)

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
                if (NpcKit.Valid(out v, me, r, _m.m_pathAgentType) && Utils.DistanceXZ(v, me) > 0.3f && !Physics.CheckCapsule(v + Vector3.up * 0.55f, v + Vector3.up * 1.45f, 0.25f, Solids, QueryTriggerInteraction.Ignore)) { found = true; Put(me, v + Vector3.up * 0.05f); }
            if (!found) _m.HomeFromStuck("held inside a thing, nowhere free near");
        }

        private void Put(Vector3 me, Vector3 to)
        {
            NpcActor a = _m.Actor;
            bool seen = Watched(me) || Watched(to);
            if (seen && a != null) a.Appear();
            Jumped(_m, me, to, seen ? "з предмета з димком" : "з предмета");
            Rigidbody rb = _m.GetComponent<Rigidbody>();
            _m.transform.position = to;
            if (rb != null) { rb.position = to; rb.linearVelocity = Vector3.zero; }
            if (seen && a != null) a.Appear();
            Forget();
        }

        private void Slide()
        {
            Rigidbody rb = _m.GetComponent<Rigidbody>();
            Vector3 at = Vector3.MoveTowards(_m.transform.position, _slideTo, 3f * Time.deltaTime);
            _m.transform.position = at;
            if (rb != null) { rb.position = at; rb.linearVelocity = Vector3.zero; }
            Vector3 d = _slideTo - at; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) _m.Face(at + d * 3f);
            if ((_slideTo - at).sqrMagnitude < 0.0004f) _slideUntil = 0f;
            _pushFrom = at; _pushAt = Time.time;
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet: its way kept clear (NpcWalk).
    internal sealed partial class NpcWalk
    {
        // the way finder's way runs tight round every corner, along every wall and the edge of every slope (the shortest one):
        // each corner where it turns is moved off what it wraps — outward on the bend — as far as there is room, floor at
        // the same height, the map under it and ground not too steep; half as far when the whole is not; else it stays
        private void Widen()
        {
            if (Margin <= 0f) return;
            float walked = 0f; // (ShapeFar m ahead: its way is asked afresh long before it gets past them, and a long way had every corner looked over each time)
            for (int i = Mathf.Max(1, _next); i < _path.Count - 1 && walked < ShapeFar; i++)
            {
                Vector3 a = _path[i - 1], c = _path[i], b = _path[i + 1];
                walked += Utils.DistanceXZ(a, c);
                Vector3 v1 = c - a, v2 = b - c; v1.y = 0f; v2.y = 0f;
                if (v1.sqrMagnitude < 0.01f || v2.sqrMagnitude < 0.01f) continue;
                v1.Normalize(); v2.Normalize();
                Vector3 outward = v1 - v2; // (away from the inside of the bend: from the corner it wraps)
                if (outward.sqrMagnitude < 0.02f) continue; // (nearly straight on)
                outward.Normalize();
                foreach (float k in Halves)
                {
                    Vector3 p = NpcKit.Floor(c + outward * Margin * k);
                    if (Mathf.Abs(p.y - c.y) > 0.5f || Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.5f, 0.35f, Solids, QueryTriggerInteraction.Ignore) || TooSteep(p)) continue;
                    Vector3 v;
                    if (!NpcKit.Valid(out v, p, 0.4f, NpcKit.Walker) || Utils.DistanceXZ(v, p) > 0.3f) continue; // (on the map: a body can stand there)
                    if (!Clear(a, p) || !Clear(p, b)) continue; // (the way to it and on free: no diagonal into a door frame)
                    _path[i] = p;
                    if (_trip != null) _trip.Widened++;
                    break;
                }
            }
        }
        private static readonly float[] Halves = { 1f, 0.5f };

        // ---------------------------------------------------------------- room round its way (the user 2026-11-02: they
        // walked into slopes and the edges of things). The way finder's way runs along every edge — a wall, a thing, the lip
        // of a drop, the foot of a steep bank — and its corners alone were moved off them (Widen). Here every point of its
        // way near ahead (one each Dense m, the first ShapeFar m) looks to both sides square to its way: how far it is to a
        // wall or a thing (a ray at knee and chest height) and to an edge underfoot (a drop over StepDrop m, ground steeper
        // than Steep); short of Berth on a side, the point is moved off it, into the middle of a narrow way — on the map,
        // at the same height, the way to it and on from it free. Asked with its way (every Repath s), not every frame.
        internal static float StepDrop = 0.6f;
        private static readonly int Walls = LayerMask.GetMask("piece", "Default", "static_solid");
        private readonly List<Vector3> _dense = new List<Vector3>();
        private void Shape()
        {
            if (Berth <= 0f || _path.Count < 2) return;
            // denser: a point each Dense m along its way near ahead (its corners kept)
            List<Vector3> d = _dense; d.Clear();
            float walked = 0f;
            for (int i = 0; i < _path.Count; i++)
            {
                if (i > _next && walked < ShapeFar)
                {
                    Vector3 a = _path[i - 1], b = _path[i];
                    float l = Utils.DistanceXZ(a, b);
                    for (float s = Dense; s < l - Dense * 0.5f; s += Dense) d.Add(NpcKit.Floor(Vector3.Lerp(a, b, s / l)));
                    walked += l;
                }
                d.Add(_path[i]);
            }
            _path.Clear(); _path.AddRange(d);
            walked = 0f;
            for (int i = Mathf.Max(1, _next); i < _path.Count - 1 && walked < ShapeFar; i++)
            {
                walked += Utils.DistanceXZ(_path[i - 1], _path[i]);
                Vector3 p = _path[i], along = _path[i + 1] - _path[i - 1]; along.y = 0f;
                if (along.sqrMagnitude < 0.01f) continue;
                along.Normalize();
                Vector3 right = new Vector3(along.z, 0f, -along.x);
                float r = SideRoom(p, right), l = SideRoom(p, -right);
                if (r >= Berth && l >= Berth) continue;
                float shift = r < l ? -Mathf.Min(Berth - r, (l - r) * 0.5f) : Mathf.Min(Berth - l, (r - l) * 0.5f); // (off the nearer edge; in a narrow way to its middle)
                if (Mathf.Abs(shift) < 0.08f) continue;
                foreach (float k in Halves)
                {
                    Vector3 q = NpcKit.Floor(p + right * shift * k);
                    if (Mathf.Abs(q.y - p.y) > 0.4f || TooSteep(q)) continue;
                    Vector3 v;
                    if (!NpcKit.Valid(out v, q, 0.4f, NpcKit.Walker) || Utils.DistanceXZ(v, q) > 0.3f) continue;
                    if (!Clear(_path[i - 1], q) || !Clear(q, _path[i + 1])) continue;
                    _path[i] = q;
                    if (_trip != null) _trip.Shaped++;
                    break;
                }
            }
        }

        // free room from a point of its way to one side: to a wall or a thing, or to an edge underfoot (a drop, a steep bank)
        private static float SideRoom(Vector3 p, Vector3 side)
        {
            float far = Berth + 0.5f, room = far;
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 0.4f, side, out h, far, Walls, QueryTriggerInteraction.Ignore) && h.normal.y < 0.5f) room = Mathf.Min(room, h.distance);
            if (Physics.Raycast(p + Vector3.up * 1.2f, side, out h, far, Walls, QueryTriggerInteraction.Ignore)) room = Mathf.Min(room, h.distance);
            for (float s = 0.3f; s < room; s += 0.3f) // (underfoot: where the ground falls away or rises steep)
            {
                Vector3 q = p + side * s;
                if (!Physics.Raycast(q + Vector3.up * 1f, Vector3.down, out h, 1f + StepDrop + 0.5f, Ground, QueryTriggerInteraction.Ignore)) { room = s; break; } // (nothing under it: a drop)
                if (p.y - h.point.y > StepDrop || Vector3.Angle(h.normal, Vector3.up) > Steep) { room = s; break; }
            }
            return room;
        }

        // the point it aims at, a little ahead on its way — but never one past a corner it would cut through what stands
        // by it: the body's way there checked (five times a second); not free, it aims at the corner itself
        private float _carrotAt; private bool _carrotClear = true;
        private Vector3 SafeCarrot(Vector3 me, float ahead, float now)
        {
            Vector3 aim = Carrot(me, ahead);
            if (_next >= _path.Count) return aim;
            Vector3 corner = _path[_next];
            if (Utils.DistanceXZ(me, corner) >= ahead) return aim; // (the corner farther than its aim: on a straight bit)
            if (now >= _carrotAt) { _carrotAt = now + 0.2f * _slow; _carrotClear = Body(me, aim); }
            return _carrotClear ? aim : corner;
        }

        // a body's width free in a straight line (knee to chest)
        private static bool Body(Vector3 me, Vector3 to)
        {
            Vector3 d = to - me; d.y = 0f;
            float l = d.magnitude;
            if (l < 0.2f) return true;
            return !Physics.CapsuleCast(me + Vector3.up * 0.55f, me + Vector3.up * 1.4f, 0.32f, d / l, l, Walls, QueryTriggerInteraction.Ignore);
        }

        private static readonly int Ground = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
        private static bool TooSteep(Vector3 p)
        {
            RaycastHit h;
            return Physics.Raycast(p + Vector3.up * 0.8f, Vector3.down, out h, 1.6f, Ground, QueryTriggerInteraction.Ignore) && Vector3.Angle(h.normal, Vector3.up) > Steep;
        }

        // its way ended short of its goal: the way back from its goal ends short of it too, on the far side of the gap; a body walks the line
        // between them: a link of the map is made there (NpcWalkLinks) and it walks across; else, when
        // the two ends are near, nothing stands between and the step is one a body takes (down a little, up a step), the
        // far end and the way on from it are put after its own: it walks across (the map has a hole there — the body does not)
        internal static float Gap = 4f, GapDown = 0.6f, GapUp = 0.6f; // (a step a body takes, up or down: 2026-11-06 it was 3 m down, and people walked off raised floors and fell — measured, JoltWatch)
        private void Bridge(Pathfinding pf, Vector3 to)
        {
            Vector3 end = _path[_path.Count - 1];
            _ask.Clear();
            _asksNow++;
            long perf = NpcPerf.Now();
            bool ok = pf.GetPath(to, end, _ask, NpcKit.Walker, false, true, false);
            NpcPerf.Add(PerfPath, perf);
            if (!ok || _ask.Count == 0) return;
            Vector3 far = _ask[_ask.Count - 1]; // (the nearest it gets to us from its goal's side)
            if ((far - end).sqrMagnitude < 0.04f) { if (_trip != null) _trip.NoBridge++; return; } // (the way back from its goal ends where its own does: no gap between them to cross — measured 2026-11-11, a chest behind a half wall: a link of no length was made and 41 «bridges» in one test led nowhere)
            float dy = far.y - end.y;
            bool linked = LinkAcross(end, far); // (a body walks it: a link of the map from now on — NpcWalkLinks)
            if (!linked && (Utils.DistanceXZ(end, far) > Gap || dy > GapUp || dy < -GapDown || !Clear(end, far))) { if (_trip != null) _trip.NoBridge++; return; }
            for (int i = _ask.Count - 1; i >= 0; i--) _path.Add(_ask[i]); // (from the far end on to its goal)
            _partial = Utils.DistanceXZ(_path[_path.Count - 1], to) > _near + 0.5f;
            if (_trip != null) _trip.Bridges++;
        }


        // eyes ahead (ten times a second): the way finder's map knows the walls, not what stands in a room — chairs, tables,
        // chests, a forge: its way runs through them and the body walked into them and stood. A body's worth looked ahead;
        // something there: the nearest free turn off its way, the same one kept a moment (no wobbling), else straight on.
        // What is walked on is no obstacle: stairs, floors, a ramp, ground sloping up
        internal static float Look = 0.8f;
        private float _turn, _turnAt, _turnKeep;
        private static readonly float[] Turns = { 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };
        private static readonly int Things2 = LayerMask.GetMask("piece", "static_solid", "Default");
        private Vector3 RoundThings(Vector3 me, Vector3 want)
        {
            float now = Time.time;
            if (now < _turnAt) return _turn == 0f ? want : Quaternion.Euler(0f, _turn, 0f) * want;
            _turnAt = now + 0.1f * _slow;
            if (!Blocked(me, want)) { if (now > _turnKeep) _turn = 0f; return _turn == 0f || Blocked(me, Quaternion.Euler(0f, _turn, 0f) * want) ? Clean(want) : Quaternion.Euler(0f, _turn, 0f) * want; }
            if (_turn != 0f && !Blocked(me, Quaternion.Euler(0f, _turn, 0f) * want)) { _turnKeep = now + 0.4f; return Quaternion.Euler(0f, _turn, 0f) * want; } // (the turn it took: kept while it still frees it)
            foreach (float t in Turns)
            {
                Vector3 d = Quaternion.Euler(0f, t, 0f) * want;
                if (Blocked(me, d)) continue;
                _turn = t; _turnKeep = now + 0.4f;
                if (_trip != null) _trip.Turns++;
                return d;
            }
            return want; // (all round blocked: on as it is — held fast, it is eased out)
        }
        private Vector3 Clean(Vector3 want) { _turn = 0f; return want; }

        private bool Blocked(Vector3 me, Vector3 dir)
        {
            RaycastHit h;
            if (!Physics.CapsuleCast(me + Vector3.up * 0.65f, me + Vector3.up * 1.4f, 0.3f, dir, out h, Look, Things2, QueryTriggerInteraction.Ignore)) return false;
            if (h.normal.y > 0.4f && !NpcKit.Furniture(h.collider)) return false; // (ground or a step it walks up; the top edge of a bench is neither: low furniture is walked round, not climbed)
            Piece p = h.collider.GetComponentInParent<Piece>();
            if (p != null) { string n = p.gameObject.name; if (n.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("ramp", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0) return false; } // (walked on, not round)
            Door door = p != null ? p.GetComponentInChildren<Door>() : null;
            if (door != null) return NpcDoors.Closed(door) && !NpcDoors.Usable(door, _m); // (a door: through it, DoorOnWay opens it — but one shut to it, a story's gate, a player's house, stands as a wall)
            return true;
        }
    }
}
#endif

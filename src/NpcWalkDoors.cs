#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A person's feet: doors on its way (NpcWalk; the rule of which it may use, and the telling: NpcDoors).
    internal sealed partial class NpcWalk
    {
        // A shut door right on its way. The way finder's map is built a piece at a time and not again at once when a door
        // shuts (a person shuts the ones it went through): its way may run through a door shut since — it walked into it,
        // stood six seconds and was eased past it (2026-11-02: every slide of a test was at a door). Seen ahead, a door it
        // may use is opened as a player opens it, a moment to swing, through, and shut behind — only one it opened
        private Door _door;
        private float _doorLook, _doorSwing, _doorSince;
        private float _doorSideFrom;
        // a door opened by its way through a door (NpcMind.ThroughDoor) and not shut yet (someone in the doorway): watched
        // here and shut once the doorway is free
        internal void Track(Door d, Vector3 from) { if (d == null) return; _door = d; _doorSince = Time.time; _doorSwing = 0f; _doorSideFrom = NpcDoors.Side(d, from); }

        // Every thought of its mind, walking or not (its walk may end right behind the door, it may sit down there): through
        // — on its other side and a step past it — shut behind it; never went through but walked off, or long gone: the door
        // it opened shut all the same, not left open; shut waits while someone stands in the doorway (NpcDoors.Shut)
        internal void DoorTick()
        {
            if ((object)_door == null) return;
            if (_door == null) { _door = null; return; } // (taken down)
            float now = Time.time;
            if (now < _doorSwing) return;
            Vector3 me = _m.transform.position;
            float far = Utils.DistanceXZ(me, _door.transform.position);
            bool through = NpcDoors.Side(_door, me) != _doorSideFrom && far > 1.2f;
            bool off = far > 4f || now - _doorSince > 12f;
            if ((through || off) && NpcDoors.Shut(_door, _m)) _door = null;
            else if (now - _doorSince > 30f) _door = null; // (someone keeps standing in it: left open)
        }

        private bool DoorOnWay(Vector3 me, Vector3 want, float now)
        {
            if (_door != null)
            {
                if (now < _doorSwing) { _m.StopMoving(); _m.Face(_door.transform.position + Vector3.up); Doing = "opens a door"; return true; }
                return false; // (the door it opened: DoorTick)
            }
            if (now < _doorLook) return false;
            _doorLook = now + 0.2f * _slow;
            RaycastHit h;
            if (!Physics.CapsuleCast(me + Vector3.up * 0.65f, me + Vector3.up * 1.4f, 0.3f, want, out h, 1.2f, Things2, QueryTriggerInteraction.Ignore)) return false;
            Door d = h.collider.GetComponentInParent<Door>();
            if (d == null || !NpcDoors.Closed(d) || !NpcDoors.Usable(d, _m) || !NpcDoors.Open(d, me)) return false;
            _door = d; _doorSince = now; _doorSwing = now + 0.6f; _doorSideFrom = NpcDoors.Side(d, me);
            _m.StopMoving();
            if (_trip != null) _trip.Doors++;
            return true;
        }
    }
}
#endif

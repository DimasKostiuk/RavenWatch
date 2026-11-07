#if RAVEN_ITEM && RAVEN_NPC
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A door of a story place as THIS player's story has it (the user 2026-11-04): the game keeps one open/shut for the whole world,
    // so a player past the prologue opened the ghost's house for one who had not come to it yet (and a character wiped kept the door
    // open). While the place's «lockdoors» hold for the player here, the door is drawn and stands shut in his game whatever the world
    // says (its collider moves with it: he cannot pass, the other one walks through what is shut for him); once they let him in, it
    // shows the world's state again. Each game its own picture.
    //   «shutdoors»  not locked, but it stands shut till this player opens it himself (the user: at the stage «look into the house» you
    //                must not see what is behind it, whatever happened to the door before): E opens it, here and now, whatever the
    //                world's state was. A reload while they hold: shut again
    //   the world    held shut and open in the world, with no other player near (OthersNear): shut in the world too, one write — so its
    //                hover and the rest of the game tell the truth. Another player near keeps his open door; the picture only, then
    // Put on a place's doors by LocState when it shows them (only places with «lockdoors»); a look at the conditions LookEvery s.
    internal sealed class LocDoorView : MonoBehaviour
    {
        internal static float LookEvery = 0.25f, OthersNear = 30f;
        private static readonly FieldInfo FAnim = AccessTools.Field(typeof(Door), "m_animator"), FRev = AccessTools.Field(typeof(Door), "m_lastDataRevision");
        private Door _door;
        private ZNetView _nv;
        private Animator _anim;
        private bool _locked, _shut, _opened, _held;
        private float _next;

        private void Awake() { _door = GetComponent<Door>(); _nv = GetComponent<ZNetView>(); _anim = _door != null && FAnim != null ? FAnim.GetValue(_door) as Animator : null; }

        // E on it (LocDoorPatch, its lock already asked). True: that was all (open in the world, only drawn shut: now it shows that);
        // false: the game's own E goes on and opens it
        internal bool Open()
        {
            if (!_shut) return false;
            _opened = true; _shut = false;
            return _nv != null && _nv.IsValid() && _nv.GetZDO().GetInt(ZDOVars.s_state, 0) != 0;
        }

        private void LateUpdate()
        {
            if (_door == null || _anim == null) return;
            if (Time.time >= _next) Look();
            if (_locked || _shut)
            {
                if (_anim.GetInteger("state") != 0) _anim.SetInteger("state", 0);
                if (_door.m_openEnable != null && _door.m_openEnable.activeSelf) _door.m_openEnable.SetActive(false);
                _held = true;
            }
            else if (_held)
            {
                _held = false;
                if (FRev != null) FRev.SetValue(_door, uint.MaxValue); // (the game shows the world's state again at its next look)
            }
        }

        private void Look()
        {
            _next = Time.time + LookEvery;
            _locked = LocState.LockedForMe(_door);
            bool want = !_locked && LocState.ShutForMe(_door);
            if (!want) _opened = false; // (the next time they hold it starts shut again)
            _shut = want && !_opened;
            if (!(_locked || _shut) || _nv == null || !_nv.IsValid() || _nv.GetZDO().GetInt(ZDOVars.s_state, 0) == 0 || OtherNear()) return;
            if (!_nv.IsOwner()) _nv.ClaimOwnership();
            _nv.GetZDO().Set(ZDOVars.s_state, 0); // (only when it was open, and it stays so: not a write a look)
            Story.Log("door " + Utils.GetPrefabName(gameObject) + ": was open in the world, shut (" + (_locked ? "locked" : "shutdoors") + ")");
        }

        private bool OtherNear()
        {
            Player me = Player.m_localPlayer;
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && p != me && Vector3.Distance(p.transform.position, transform.position) < OthersNear) return true;
            return false;
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A place's doors: its great door (locations.list «gate:»), the doors locked or shut for each player.
    internal static partial class LocState
    {
        // ---------------------------------------------------------------- the doors
        // The great door in front (its «gate:» in locations.list, a name of <place>.names.txt): shut and locked till the story opens it — an event of
        // the server's (the world key <place>_gate) — and then it swings open once, INTO the building (Inward), and from then it is anyone's
        // door: opened and shut by hand either way, as any door (the user 2026-11-04, later the same day: before, nobody could close it).
        // The story takes it back («gate <place> shut»): shut and locked again. The other doors are anyone's, unless «lockdoors» says.
        internal static bool GateOpenOf(Loc l) { return l != null && ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(l.KeyGate); }

        // the story opens it for the whole world, or locks it again (quest and scene: «gate <place> open|shut»)
        internal static void SetGate(Loc l, bool open)
        {
            if (l == null || ZoneSystem.instance == null || GateOpenOf(l) == open) return;
            if (open) ZoneSystem.instance.SetGlobalKey(l.KeyGate); else ZoneSystem.instance.RemoveGlobalKey(l.KeyGate);
        }

        private static readonly List<ZNetView> _doors = new List<ZNetView>();
        private static readonly int KSwung = "RavenWatch_gateSwung".GetStableHashCode(); // (the great door swung open once since the story opened it: after that, its state is the players')

        // the door's state that swings it into its building (the game's: one opened by someone outside swings away from him)
        internal static int Inward(ZNetView nv)
        {
            Loc l = nv != null && nv.IsValid() ? Locs.Get(nv.GetZDO().GetString(LocParts.KOf, "")) : null;
            Vector3 at; Quaternion r;
            if (l == null || !l.Anchor(out at, out r)) return 1;
            Vector3 outward = nv.transform.position - at; outward.y = 0f;
            return Vector3.Dot(nv.transform.forward, outward) < 0f ? 1 : -1;
        }

        // a player stands in the building of this place: on its floor and under its roof (not by it under the eaves). Rays straight
        // up and down: two, asked of one player
        internal static bool InsideOf(Player p, string place)
        {
            if (p == null) return false;
            return OfPlace(new Ray(p.transform.position + Vector3.up * 0.5f, Vector3.down), 1.6f, place) && OfPlace(new Ray(p.GetHeadPoint(), Vector3.up), 8f, place);
        }

        private static readonly int PieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid");
        private static bool OfPlace(Ray ray, float far, string place)
        {
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, far, PieceMask, QueryTriggerInteraction.Ignore) || hit.collider == null) return false;
            ZNetView nv = hit.collider.GetComponentInParent<ZNetView>();
            string of; Vector3 local;
            return nv != null && nv.IsValid() && LocParts.Place(nv, out of, out local) && of == place;
        }

        private static void CloseDoors()
        {
            for (int i = _doors.Count - 1; i >= 0; i--)
            {
                ZNetView nv = _doors[i];
                if (nv == null || !nv.IsValid()) { _doors.RemoveAt(i); continue; }
                Loc l = Locs.Get(nv.GetZDO().GetString(LocParts.KOf, ""));
                if (l == null || !IsMain(l, nv) || !nv.IsOwner()) continue;
                int state = nv.GetZDO().GetInt(ZDOVars.s_state);
                bool swung = nv.GetZDO().GetBool(KSwung);
                if (GateOpenOf(l)) { if (!swung) { nv.GetZDO().Set(ZDOVars.s_state, Inward(nv)); nv.GetZDO().Set(KSwung, true); } } // (once, inward; then the players')
                else { if (state != 0) nv.GetZDO().Set(ZDOVars.s_state, 0); if (swung) nv.GetZDO().Set(KSwung, false); }
            }
        }

        // the place a door is of (a blueprint put it there), null — none
        private static Loc DoorOf(Door d)
        {
            ZNetView nv = d != null ? d.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() ? Locs.Get(nv.GetZDO().GetString(LocParts.KOf, "")) : null;
        }

        private static bool IsMain(Loc l, ZNetView nv) { return l.Gate.Length > 0 && LocParts.NameOf(l.Id, nv) == l.Gate; }

        // why this door does not open now (null — it opens)
        internal static string Locked(Door d)
        {
            Loc l = DoorOf(d);
            if (l == null) return null;
            if (!GateOpenOf(l) && IsMain(l, d.GetComponent<ZNetView>())) return "Великі двері замкнені.";
            if (l.LockDoors.Length > 0 && System.Array.IndexOf(l.OpenDoors, Utils.GetPrefabName(d.gameObject)) < 0 && System.Array.IndexOf(l.OpenDoors, LocParts.NameOf(l.Id, d.GetComponent<ZNetView>())) < 0 && DoorsLocked(l, Player.m_localPlayer)) return "Двері замкнені.";
            return null;
        }

        // a door of a place a person may open by itself: its great door once the story has opened it, any other door; not while it is
        // the story's to open, and not where each player has his own picture of them (lockdoors: a person would go through what is shut for him)
        internal static bool OpenToPeople(Door d)
        {
            Loc l = DoorOf(d);
            if (l == null || l.LockDoors.Length > 0) return false;
            return GateOpenOf(l) || !IsMain(l, d.GetComponent<ZNetView>());
        }

        // locked for the player here by the place's «lockdoors» (LocDoorView draws it shut for him whatever the world says)
        internal static bool LockedForMe(Door d)
        {
            Loc l = DoorOf(d);
            if (l == null || l.LockDoors.Length == 0) return false;
            return !NeverLocked(l, d) && DoorsLocked(l, Player.m_localPlayer);
        }

        // to stand shut for the player here till he opens it himself (Loc.ShutDoors; LocDoorView keeps it so)
        internal static bool ShutForMe(Door d)
        {
            Loc l = DoorOf(d);
            if (l == null || l.ShutDoors.Length == 0 || NeverLocked(l, d)) return false;
            return DoorConds(l, l.ShutDoors, "shutdoors", Player.m_localPlayer);
        }

        private static bool NeverLocked(Loc l, Door d)
        {
            return System.Array.IndexOf(l.OpenDoors, Utils.GetPrefabName(d.gameObject)) >= 0 || System.Array.IndexOf(l.OpenDoors, LocParts.NameOf(l.Id, d.GetComponent<ZNetView>())) >= 0;
        }

        // a place's doors locked for this player while its conditions hold for him (Loc.LockDoors: till the story leads him in)
        private static bool DoorsLocked(Loc l, Player p) { return DoorConds(l, l.LockDoors, "lockdoors", p); }
        private static bool DoorConds(Loc l, string text, string word, Player p) { return p != null && StoryCond.Holds(p, text, e => l.Log(word + ": " + e)); }

    }
}
#endif

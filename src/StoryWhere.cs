#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's places by name (a stage's «mark:», the condition «near», a scene's steps):
    //   <place>[.<piece>]    a place of the story by its id (locations.list.txt: zala, torvald) — «hall» is the Hall's other
    //                        word — and a named piece of it (its names file: hall.dveri_holovni, torvald.vohnyshche):
    //                        the piece where it stands when loaded here, else where its name says, else the place itself
    //                        (where it stands: Loc.Anchor — one rule for every caller)
    //   <name>               the author's place in this world (StoryPlaces)
    //   bird                 the story's raven of the scene playing now
    //   npc.<who>            a person of the story where it stands now (its template id or tag; the nearest loaded)
    internal static class StoryWhere
    {
        internal const string HallWord = "hall";

        // a place's word into its place and the piece named in it («» — the place itself)
        internal static Loc Place(string name, out string piece)
        {
            int dot = name.IndexOf('.');
            string id = dot > 0 ? name.Substring(0, dot) : name;
            piece = dot > 0 ? name.Substring(dot + 1) : "";
            return Locs.Get(id == HallWord ? HallSpawn.Name : id);
        }

        // the load check (quests, scenes): what is wrong with a place's word, null — fine. A place of the story and its
        // named piece are checked against their files; the author's own marks only when this world's list is read.
        internal static string Why(string name)
        {
            if (string.IsNullOrEmpty(name)) return "порожнє місце";
            if (name == "bird" || name == "player" || name == "sky" || name.StartsWith("npc.")) return null;
            string piece;
            Loc loc = Place(name, out piece);
            if (loc != null)
            {
                string prefab; Vector3 local;
                return piece.Length == 0 || HallParts.NameSpot(loc.Id, piece, out prefab, out local) ? null : "у «" + loc.Id + "» нема названої частини «" + piece + "» (" + loc.Id + ".names.txt)";
            }
            if (name.IndexOf('.') > 0) return "нема місця сюжету «" + name.Substring(0, name.IndexOf('.')) + "» (locations.list.txt)";
            if (ZNet.instance == null || StoryPlaces.Find(name) != null) return null;
            return "місце «" + name + "» — ні місце сюжету, ні мітка автора в цьому світі";
        }

        internal static bool Find(string name, out Vector3 at)
        {
            at = Vector3.zero;
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "bird") { at = StoryScenes.BirdAt; return StoryScenes.HasBird; }
            if (name.StartsWith("npc.")) // (a person of the story where it stands now: StoryScenes.Actor)
            {
                NpcActor a = StoryScenes.Actor(name.Substring(4), Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero);
                if (a == null) return false;
                at = a.transform.position;
                return true;
            }
            string piece;
            Loc loc = Place(name, out piece);
            if (loc != null)
            {
                if (piece.Length > 0)
                {
                    foreach (ZNetView nv in HallParts.Named(loc.Id, piece)) if (nv != null) { at = nv.transform.position; return true; } // (loaded here)
                    if (loc.NamedSpot(piece, out at)) return true; // (not loaded: where its name says)
                }
                Quaternion rot;
                return loc.Anchor(out at, out rot);
            }
            StoryPlaces.Place pl = StoryPlaces.Find(name);
            if (pl == null) return false;
            at = pl.Pos;
            return true;
        }

        // where a bird sits on it: the top of a named piece's colliders (a gate's pivot is at its foot), else the place
        internal static bool Top(string name, out Vector3 at)
        {
            Bounds b;
            if (Body(name, out b)) { at = new Vector3(b.center.x, b.max.y, b.center.z); return true; }
            return Find(name, out at);
        }

        // where one walks up to it from «from»: a named piece — the nearest point of its body and a step out towards the
        // one coming (a door's pivot is inside its wall: nobody reaches that); anything else — the place itself
        internal static bool Approach(string name, Vector3 from, out Vector3 at)
        {
            Bounds b;
            if (!Body(name, out b)) return Find(name, out at);
            Vector3 p = b.ClosestPoint(new Vector3(from.x, b.center.y, from.z));
            Vector3 d = from - p; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) { d = from - b.center; d.y = 0f; }
            at = p + (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * 1.2f;
            at.y = b.min.y;
            return true;
        }

        // the named pieces of a place («hall.dveri_holovni» — one name may be on several: a gate's two halves), the loaded
        // ones; Piece — the first
        internal static List<ZNetView> Pieces(string name)
        {
            string piece;
            Loc loc = Place(name, out piece);
            List<ZNetView> l = new List<ZNetView>();
            if (loc != null && piece.Length > 0) foreach (ZNetView nv in HallParts.Named(loc.Id, piece)) if (nv != null) l.Add(nv);
            return l;
        }

        internal static ZNetView Piece(string name) { List<ZNetView> l = Pieces(name); return l.Count > 0 ? l[0] : null; }

        private static bool Body(string name, out Bounds b)
        {
            ZNetView nv = Piece(name);
            b = new Bounds();
            if (nv == null) return false;
            b = new Bounds(nv.transform.position, Vector3.zero);
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) if (c != null && !c.isTrigger) b.Encapsulate(c.bounds);
            return true;
        }
    }
}
#endif

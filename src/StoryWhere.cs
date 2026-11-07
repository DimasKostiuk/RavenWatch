#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The story's places by name (a stage's «mark:», the condition «near», a scene's steps):
    //   <place>[.<piece>]    a place of the story by its id (locations.list.txt: zala, torvald) — «hall» is the Hall's other
    //                        word — and a named piece of it (its names file: hall.dveri_holovni, torvald.vohnyshche):
    //                        the piece where it stands when loaded here — of several parts, the middle of their roots (a gate's two leaves:
    //                        between them, not one leaf — Heid faced the gate 45° off, 2026-11-07; NamedAt) — else where its name says, else the place itself
    //                        (where it stands: Loc.Anchor — one rule for every caller)
    //   <name>               the author's place in this world (StoryPlaces)
    //   bird · player · sky  words of their own (Words): the story's raven of the scene playing now · where you stand · high in the air ahead of you
    //                        and to your left (a sound from above)
    //   npc.<who>            a person of the story where it stands now (its template id or tag; the nearest loaded)
    //   <prefix><name>       a module's own places (Prefixed: «me.<name>» — the player's own spot, StorySpot)
    // One table for the load check (Why) and the finding (Find): a word that passes the check is a word that can be found.
    internal static class StoryWhere
    {
        // a module's places by their prefix: the rest of the word → where (false: not there, or not yet)
        internal delegate bool FindFn(string rest, out Vector3 at);
        internal static readonly Dictionary<string, FindFn> Prefixed = new Dictionary<string, FindFn>
        {
            { "npc.", (string rest, out Vector3 at) => // (a person of the story where it stands now: StoryScenes.Actor)
                {
                    NpcActor a = StoryScenes.Actor(rest, Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero);
                    at = a != null ? a.transform.position : Vector3.zero;
                    return a != null;
                } },
        };

        // words of their own: a new one is a line here
        private static readonly Dictionary<string, FindFn> Words = new Dictionary<string, FindFn>
        {
            { "bird", (string rest, out Vector3 at) => { at = StoryScenes.BirdAt; return StoryScenes.HasBird; } },
            { "player", (string rest, out Vector3 at) => { Player me = Player.m_localPlayer; at = me != null ? me.transform.position : Vector3.zero; return me != null; } },
            { "sky", (string rest, out Vector3 at) => // (up in the air ahead of you and to your left: a caw from above, a sound to look for)
                {
                    Player me = Player.m_localPlayer;
                    at = me != null ? me.GetHeadPoint() + me.transform.forward * 10f - me.transform.right * 8f + Vector3.up * 14f : Vector3.zero;
                    return me != null;
                } },
        };

        // a word that may name something on the move (asked again each time); a place of the story stands still
        internal static bool Moves(string name) { string piece; return Words.ContainsKey(name) || name.IndexOf('.') > 0 && Place(name, out piece) == null; }
        private static FindFn PrefixOf(string name, out string rest)
        {
            rest = "";
            foreach (KeyValuePair<string, FindFn> kv in Prefixed) if (name.StartsWith(kv.Key)) { rest = name.Substring(kv.Key.Length); return kv.Value; }
            return null;
        }

        // a place's word into its place and the piece named in it («» — the place itself)
        internal static Loc Place(string name, out string piece)
        {
            int dot = name.IndexOf('.');
            string id = dot > 0 ? name.Substring(0, dot) : name;
            piece = dot > 0 ? name.Substring(dot + 1) : "";
            return Locs.Get(id); // (by its id or an alias of it: «hall»)
        }

        // the load check (quests, scenes): what is wrong with a place's word, null — fine. A place of the story and its
        // named piece are checked against their files; the author's own marks only when this world's list is read.
        internal static string Why(string name)
        {
            if (string.IsNullOrEmpty(name)) return "порожнє місце";
            if (Words.ContainsKey(name)) return null;
            string rest;
            if (PrefixOf(name, out rest) != null) return Story.GoodName(rest) ? null : "погана назва місця «" + name + "»";
            string piece;
            Loc loc = Place(name, out piece);
            if (loc != null)
            {
                string prefab; Vector3 local;
                return piece.Length == 0 || LocParts.NameSpot(loc.Id, piece, out prefab, out local) ? null : "у «" + loc.Id + "» нема названої частини «" + piece + "» (" + loc.Id + ".names.txt)";
            }
            if (name.IndexOf('.') > 0) return "нема місця сюжету «" + name.Substring(0, name.IndexOf('.')) + "» (locations.list.txt)";
            if (ZNet.instance == null || StoryPlaces.Find(name) != null) return null;
            return "місце «" + name + "» — ні місце сюжету, ні мітка автора в цьому світі";
        }

        // a named piece loaded here: the middle of its parts' roots, at the height of the lowest. A gate's two leaves: between them, whichever way
        // they swing (a door's root stays where it is); one part: its root, as ever
        private static bool NamedAt(Loc loc, string piece, out Vector3 at)
        {
            at = Vector3.zero;
            Vector3 sum = Vector3.zero; float low = float.MaxValue; int n = 0;
            foreach (ZNetView nv in LocParts.Named(loc.Id, piece))
            {
                if (nv == null || !nv.IsValid()) continue;
                sum += nv.transform.position; low = Mathf.Min(low, nv.transform.position.y); n++;
            }
            if (n == 0) return false;
            at = new Vector3(sum.x / n, low, sum.z / n);
            return true;
        }

        internal static bool Find(string name, out Vector3 at)
        {
            at = Vector3.zero;
            if (string.IsNullOrEmpty(name)) return false;
            FindFn own;
            if (Words.TryGetValue(name, out own)) return own("", out at);
            string rest;
            own = PrefixOf(name, out rest);
            if (own != null) return own(rest, out at);
            string piece;
            Loc loc = Place(name, out piece);
            if (loc != null)
            {
                if (piece.Length > 0)
                {
                    if (NamedAt(loc, piece, out at)) return true; // (loaded here)
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

        // «<place>[.<name>]» where no player and nothing loaded may be (the server keeping the story's people): where it is, its object in the world's own
        // list (null — none, or a whole place), its place; why not (null — found)
        internal static string Standing(string spot, out Vector3 at, out ZDO zdo, out Loc loc)
        {
            at = Vector3.zero; zdo = null;
            string piece;
            loc = Place(spot, out piece);
            if (loc == null) return "no place «" + spot + "»";
            if (!loc.Stands) return loc.Id + " does not stand in this world yet";
            if (piece.Length == 0) { Quaternion r; return loc.Anchor(out at, out r) ? null : loc.Id + " has no place"; }
            return loc.NamedAt(piece, out at, out zdo) ? null : "no piece named «" + piece + "» in " + loc.Id + " (its names file)";
        }
        internal static bool Standing(string spot, out Vector3 at) { ZDO z; Loc l; return Standing(spot, out at, out z, out l) == null; }

        // where a person stands by a thing: a step from its edge on the side «side» it comes from, never its middle (a fire: she walked into it and burned; a bed: knelt on
        // it); hands: a clip that works on the thing, ClipIn m nearer. False: nothing of it found. One rule for a word of the story (StoryHold) and its own life (StoryLife)
        internal static float ClipIn = 0.7f;
        internal static bool StandBy(string thing, Vector3 side, Vector3 thingAt, bool hands, out Vector3 stand)
        {
            if (!Approach(thing, side, out stand)) return false;
            if (hands) { Vector3 to = thingAt - stand; to.y = 0f; if (to.magnitude > ClipIn) stand += to.normalized * ClipIn; }
            return true;
        }

        // where a bird sits on it: on the named piece's own surface straight down over its middle (a gate's pivot is at its
        // foot; a leaning pillar's box top is in the air over it), else its box top, else the place
        internal static bool Top(string name, out Vector3 at) { return Top(name, Vector3.zero, 0f, out at); }

        // the same, side m to the right (below zero: to the left) of its middle as seen from «from»: a seat on one end of a
        // wide thing. Nothing of it there: its middle
        internal static bool Top(string name, Vector3 from, float side, out Vector3 at)
        {
            Bounds b;
            if (!Body(name, out b)) return Find(name, out at);
            at = new Vector3(b.center.x, b.max.y, b.center.z);
            if (side != 0f)
            {
                Vector3 way = b.center - from; way.y = 0f;
                Vector3 right = new Vector3(way.z, 0f, -way.x).normalized;
                at += right * side;
            }
            ZNetView nv = Piece(name);
            Ray down = new Ray(at + Vector3.up, Vector3.down);
            float best = float.MinValue;
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: once per scene step that seats a bird on it
            {
                RaycastHit hit;
                if (c != null && !c.isTrigger && c.Raycast(down, out hit, b.size.y + 2f) && hit.point.y > best) best = hit.point.y;
            }
            if (best > float.MinValue) at.y = best;
            else if (side != 0f) return Top(name, out at);
            return true;
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

        // before a thing (a scene's «before»: the user 2026-11-04 — Heid stopped 2.4 m aside of the gate she came to from the side, and
        // opened it from there): the middle of all its parts, and the way out of its face: square to the line of its parts (a gate's
        // two halves; one part: its own right), on the side away from its place's middle — the side one comes to it from
        internal static bool Before(string name, out Vector3 mid, out Vector3 way)
        {
            mid = way = Vector3.zero;
            string piece;
            Loc loc = Place(name, out piece);
            List<ZNetView> l = Pieces(name);
            if (l.Count == 0) return false;
            Bounds b = new Bounds(l[0].transform.position, Vector3.zero);
            foreach (ZNetView nv in l) foreach (Collider c in nv.GetComponentsInChildren<Collider>()) if (c != null && !c.isTrigger) b.Encapsulate(c.bounds); // heavy: once a step that asks
            mid = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 along = l.Count > 1 ? l[1].transform.position - l[0].transform.position : l[0].transform.right;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) along = l[0].transform.right;
            way = Vector3.Cross(Vector3.up, along).normalized;
            Vector3 at; Quaternion rot;
            if (loc != null && loc.Anchor(out at, out rot)) { Vector3 outward = mid - at; outward.y = 0f; if (Vector3.Dot(way, outward) < 0f) way = -way; }
            return true;
        }

        // the named pieces of a place («hall.dveri_holovni» — one name may be on several: a gate's two halves), the loaded
        // ones; Piece — the first
        internal static List<ZNetView> Pieces(string name)
        {
            string piece;
            Loc loc = Place(name, out piece);
            List<ZNetView> l = new List<ZNetView>();
            if (loc != null && piece.Length > 0) foreach (ZNetView nv in LocParts.Named(loc.Id, piece)) if (nv != null) l.Add(nv);
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

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
    //   falcon.<name>        a sister's falcon of the scene playing now, where it is — its own middle; turned into a person: hers (StoryScenes.FalconAt)
    //   beast.<name>         a creature of the game the scene playing now made (StoryBeasts: the skeleton, the boar), its middle
    //   lane.<name>@<m>[@<side>]   so many m along a lane of the scene playing now, so many to its right (StoryScenes.LaneAt)
    //   spot.<place>@<x>@<z>[@<y>] a point of a place of the story in its blueprint's own frame (the numbers of its .txt and .names.txt), on the floor
    //                        there (SpotAt): where a person of a scene stands exactly — a named piece gives only its edge, and which edge depends on
    //                        where one comes from (the user 2026-11-21, the Hall's last scene: Heid 0.7 m off the cauldron, Torvald 8 m from her)
    //   npc.<who>.thing · player.thing   the thing that one holds (StoryProps; a person holding none — its own thing, NpcTool), where its look is now — in a hand, on the arm, lying, falling,
    //                        in flight: its own middle, not a spot on the ground (IsLow: what films or looks at it adds no height over it).
    //                        Not found while it holds nothing. Moves while it is held or on its way; lying still, it stands
    //   <prefix><name>       a module's own places (Prefixed: «me.<name>» — the player's own spot, StorySpot; «kept.<name>» — the bed the story keeps,
    //                        where one lies on it, StoryKeep: a thing lying low, Lying)
    // One table for the load check (Why) and the finding (Find): a word that passes the check is a word that can be found.
    internal static class StoryWhere
    {
        // a module's places by their prefix: the rest of the word → where (false: not there, or not yet)
        internal delegate bool FindFn(string rest, out Vector3 at);
        internal static readonly Dictionary<string, FindFn> Prefixed = new Dictionary<string, FindFn>
        {
            { "falcon.", (string rest, out Vector3 at) => StoryScenes.FalconAt(rest, out at) }, // (a sister's falcon of the scene: StoryScenesFalcon)
            { "beast.", (string rest, out Vector3 at) => StoryBeasts.At(rest, out at) },        // (a creature of the game the scene made: StoryBeasts)
            { "lane.", (string rest, out Vector3 at) => StoryScenes.LaneAt(rest, out at) },     // (so far along a lane of the scene: StoryScenesLane)
            { "spot.", (string rest, out Vector3 at) => SpotAt(rest, out at) },                                                                // (a point of a place in its blueprint's frame, on its floor)
            { "npc.", (string rest, out Vector3 at) => // (a person of the story where it stands now: StoryScenes.Actor; «npc.<who>.thing»: what it holds)
                {
                    bool thing = rest.EndsWith(Thing);
                    NpcActor a = Person(thing ? rest.Substring(0, rest.Length - Thing.Length) : rest);
                    at = a != null ? a.transform.position : Vector3.zero;
                    return a != null && (!thing || StoryProps.ThingAt(a.gameObject, out at) || NpcTool.LookAt(a, out at)); // (nothing of the story in its hands: its own thing — the dwarf's hammer, NpcTool)
                } },
        };

        // «spot.<place>@<x>@<z>[@<y>]»: that point of the place's blueprint, set on the floor under it (y: where to look for the floor from — none: 0,
        // the ground floor of a place built round its own middle; a gallery: its own height). False: no such place here, or its numbers are not numbers
        private static bool SpotAt(string rest, out Vector3 at)
        {
            at = Vector3.zero;
            string[] w = rest.Split('@');
            float x, z, y = 0f;
            Loc loc = w.Length >= 3 ? Locs.Get(w[0]) : null;
            if (loc == null || !float.TryParse(w[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) || !float.TryParse(w[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z)) return false;
            if (w.Length > 3 && !float.TryParse(w[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)) return false;
            if (!loc.ToWorld(new Vector3(x, y, z), out at)) return false;
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out hit, 4f, LayerMask.GetMask("piece", "Default", "static_solid", "terrain"), QueryTriggerInteraction.Ignore)) at.y = hit.point.y; // (asked when a step wants it: one ray)
            return true;
        }

        // a module's place the WORLD keeps (its prefix here: «heart.» — StoryHeart, «found.» — StoryFound), known with no player and no place of the
        // story: a person may live there (Standing), stand by it (StoryHold). Not «npc.», «falcon.», «me.»: those are a scene's or a player's
        internal static readonly HashSet<string> WorldKept = new HashSet<string>();
        internal static bool OfModule(string name)
        {
            if (name != null) foreach (string k in WorldKept) if (name.StartsWith(k, System.StringComparison.Ordinal)) return true;
            return false;
        }

        // The thing a holder has (StoryProps: in a hand, on the arm, lying, in the air) as a word of place: «npc.<who>.thing», «player.thing»
        private const string Thing = ".thing";
        internal static bool IsThing(string name) { return name != null && name.EndsWith(Thing) && (name == "player" + Thing || (name.StartsWith("npc.") && name.Length > 4 + Thing.Length)); }

        // a module's place that names a thing lying low, not a spot to stand on (its prefix here: «kept.» — StoryKeep, the bed the story keeps): it
        // stands still, and a shot, a look, a turn aim at it itself — a bed's mattress is a metre under the aim a place gets (PlaceUp)
        internal static readonly HashSet<string> Lying = new HashSet<string>();
        private static bool OfLying(string name)
        {
            if (name != null) foreach (string k in Lying) if (name.StartsWith(k, System.StringComparison.Ordinal)) return true;
            return false;
        }
        // what a shot, a look, a turn aims at itself, with no height over it: a thing someone holds, a thing lying low. The one rule of it
        // (a falcon's word is its own middle in the air — and the middle of the person it turned into: a shot of it aimed 1.2 m over the bird till 2026-11-14)
        // a word that names a body — a creature the scene made (StoryBeasts: «beast.<name>»): a shot frames it whole and follows its head, a look
        // follows it as it moves, as a person's (StoryScenesCamera.Who, StoryLook.At; the review 2026-11-18: taken for a place, a shot aimed 1.2 m over its middle)
        internal static Character BodyOf(string name) { return name != null && name.StartsWith("beast.", System.StringComparison.Ordinal) ? StoryBeasts.CharOf(name.Substring(6)) : null; }
        internal static bool IsLow(string name) { return IsThing(name) || OfLying(name) || (name != null && name.StartsWith("falcon.")); }
        private static NpcActor Person(string who) { return StoryScenes.Actor(who, Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero); }
        // whose thing a thing's word names, on this game (null: nobody here)
        private static GameObject HolderOf(string thing)
        {
            if (thing == "player" + Thing) return Player.m_localPlayer != null ? Player.m_localPlayer.gameObject : null;
            NpcActor a = Person(thing.Substring(4, thing.Length - 4 - Thing.Length));
            return a != null ? a.gameObject : null;
        }

        // words of their own: a new one is a line here
        private static readonly Dictionary<string, FindFn> Words = new Dictionary<string, FindFn>
        {
            { "bird", (string rest, out Vector3 at) => { at = StoryScenes.BirdAt; return StoryScenes.HasBird; } },
            { "player", (string rest, out Vector3 at) => { Player me = Player.m_localPlayer; at = me != null ? me.transform.position : Vector3.zero; return me != null; } },
            { "player" + Thing, (string rest, out Vector3 at) => { Player me = Player.m_localPlayer; at = Vector3.zero; return me != null && StoryProps.ThingAt(me.gameObject, out at); } }, // (what you hold, where it is now)
            { "sky", (string rest, out Vector3 at) => // (up in the air ahead of you and to your left: a caw from above, a sound to look for)
                {
                    Player me = Player.m_localPlayer;
                    at = me != null ? HuginWhispers.Body.Head(me) + me.transform.forward * 10f - me.transform.right * 8f + Vector3.up * 14f : Vector3.zero;
                    return me != null;
                } },
        };

        // a word that may name something on the move (asked again each time); a place of the story stands still — and a thing that lies still
        // (held, falling, in flight, or not here yet: it moves — StoryProps.ThingMoves)
        internal static bool Moves(string name)
        {
            if (IsThing(name)) return StoryProps.ThingMoves(HolderOf(name));
            if (OfLying(name)) return false;
            string piece;
            return Words.ContainsKey(name) || name.IndexOf('.') > 0 && Place(name, out piece) == null;
        }
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
        // A name may be pieces of different states (the Hall's chest: the abandoned Hall's and the repaired one's): those its state has now; none of them
        // there — all of them, as before (a scene may film a piece hidden this moment)
        private static bool NamedAt(Loc loc, string piece, out Vector3 at)
        {
            at = Vector3.zero;
            Vector3 sum = Vector3.zero; float low = float.MaxValue; int n = 0;
            List<ZNetView> named = LocParts.Named(loc.Id, piece);
            for (int pass = 0; pass < 2 && n == 0; pass++)
                foreach (ZNetView nv in named)
                {
                    if (nv == null || !nv.IsValid() || (pass == 0 && LocState.Absent(nv))) continue;
                    sum += nv.transform.position; low = Mathf.Min(low, nv.transform.position.y); n++;
                }
            if (n == 0) return false;
            at = new Vector3(sum.x / n, low, sum.z / n);
            return true;
        }

        // how far one is from a place by the map — and never near across the game's line between the land and an inside (3000 m): an inside of
        // ours hangs over its own door (LocInside), so by the map its grave and the grass over it are one spot
        internal static float Apart(Vector3 place, Vector3 who) { return Character.InInterior(place) != Character.InInterior(who) ? float.MaxValue : Utils.DistanceXZ(place, who); } // flat: the one measure itself, once the line is checked

        // how far one is from a place by its word (the conditions near, npcat, npcnear): a named piece loaded here — from the nearest edge of its body
        // (of several parts, the nearest part), as StandBy stands a person a step off that edge; a root is often at a big thing's far end (the user
        // 2026-11-19: torv_back never began — Heid stood by the kitchen hearth as told, 6.4 m from its root, the scene asked 6). Anything else, or
        // the piece not loaded here: from where its word says (Find). Each collider its own box (a rotated piece's one box took in its corners), the
        // pieces its state has not (LocState.Absent — the Hall's chest of 1.4 beside the chest of 2: their hidden colliders are off, and an off one's
        // box sat at the world's middle) left out. A door, a gate: from its word too — its leaves swing, so its body is no place (the prologue's
        // «!near prologue_house.dveri 2», «not on the threshold», was set from its middle). A dedicated server has no pieces loaded: from its word
        // there — its waits use wide rounds (10–16 m), the tight ones are a scene's needs, on the game that plays it. Its colliders read at most every
        // EdgeEvery s a word. False: no such place here
        internal static float EdgeEvery = 3f;
        private sealed class Edges { public float At; public readonly List<Bounds> Bodies = new List<Bounds>(); }
        private static readonly Dictionary<string, Edges> EdgesOf = new Dictionary<string, Edges>();
        internal static bool Off(string name, Vector3 who, out float m)
        {
            m = float.MaxValue;
            string piece;
            if (Place(name, out piece) != null && piece.Length > 0)
            {
                Edges e;
                if (!EdgesOf.TryGetValue(name, out e)) EdgesOf[name] = e = new Edges { At = float.MinValue };
                if (Time.time - e.At >= EdgeEvery)
                {
                    e.At = Time.time; e.Bodies.Clear();
                    foreach (ZNetView nv in Pieces(name))
                    {
                        if (nv == null || !nv.IsValid() || LocState.Absent(nv)) continue;
                        if (nv.GetComponentInChildren<Door>() != null) { e.Bodies.Clear(); break; }
                        foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: every EdgeEvery s a word that a condition asks
                            if (On(c)) e.Bodies.Add(c.bounds);
                    }
                }
                if (e.Bodies.Count > 0)
                {
                    foreach (Bounds b in e.Bodies) m = Mathf.Min(m, Apart(b.ClosestPoint(new Vector3(who.x, b.center.y, who.z)), who));
                    return true;
                }
            }
            Vector3 at;
            if (!Find(name, out at)) return false;
            m = Apart(at, who);
            return true;
        }

        // where a place stands on the map: a module's own word for it (OnMap: a point inside one of our dungeons — the way into it on the land; by its
        // own spot the map showed «Скриня кургану» over the shore, 120 m off the barrow, the user 2026-11-15), else where it is
        internal static readonly Dictionary<string, FindFn> OnMap = new Dictionary<string, FindFn>();
        internal static bool MapAt(string name, out Vector3 at)
        {
            if (!string.IsNullOrEmpty(name)) foreach (KeyValuePair<string, FindFn> kv in OnMap) if (name.StartsWith(kv.Key)) return kv.Value(name.Substring(kv.Key.Length), out at);
            return Find(name, out at);
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
        // list (null — none, or a whole place), its place (null too for a module's own word: «heart.first»); why not (null — found)
        internal static string Standing(string spot, out Vector3 at, out ZDO zdo, out Loc loc)
        {
            at = Vector3.zero; zdo = null; loc = null;
            if (OfModule(spot)) return Find(spot, out at) ? null : "«" + spot + "» is not in this world yet"; // (a module's place the world keeps: «heart.first», «found.<name>»)
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
        // A clip that touches what it works on says itself how far its face stands («touch wall 0.65», BodyClips): then the person stands square
        // before the thing's middle, that far from its face, on the side it comes from — as a scene's «before» does (Before, FaceEdge). The user
        // 2026-11-20: Lit «не підходить до стін іноді і б'є молотом просто в повітрі» — a step out of the nearest point of the wall's box put him
        // by its end when he came along it, and never at his clip's own distance
        // The spot is a place to STAND (the user 2026-11-20: «do people take their places before a scene rightly?» — read in the code: a step
        // out of a thing's box was taken as it came, whatever stood there; the Hall's later state put a table and four benches by the pillar
        // Sverre is told to stand at, and that morning the story had put Eyvind up on the workbench, 2026-11-20 11:57): a step
        // out of a thing's edge is only where to look from — Free takes the floor nearest it that is no furniture, has room for a body and
        // is not taken (free: the asker's own word — StoryHold: no other held person's spot). A clip that touches the thing keeps its own
        // exact spot: its hands are reckoned from there
        internal static bool StandBy(string thing, Vector3 side, Vector3 thingAt, bool hands, out Vector3 stand, string clip = null, System.Func<Vector3, bool> free = null)
        {
            float touch = hands ? Touches(clip) : 0f;
            Vector3 mid, way;
            if (touch > 0f && Before(thing, side, out mid, out way))
            {
                float face;
                stand = mid + way * ((FaceEdge(thing, mid, -way, out face) ? face : 0f) + touch);
                return true;
            }
            if (!Approach(thing, side, out stand)) return false;
            stand = Free(stand, free);
            if (hands) { Vector3 to = thingAt - stand; to.y = 0f; if (to.magnitude > ClipIn) stand += to.normalized * ClipIn; }
            return true;
        }

        // the floor a body may stand on, nearest a spot (within FreeReach m): NpcKit.FloorBeside — the one way to find where a body may be put —
        // with room for it and what the asker wants; none found, or no map here (a server with no pieces): the spot as it is
        internal static float FreeReach = 1.6f;
        internal static Vector3 Free(Vector3 spot, System.Func<Vector3, bool> free = null)
        {
            Vector3 v;
            return NpcKit.FloorBeside(out v, spot, FreeReach, s => NpcKit.Room(s) && (free == null || free(s))) ? v : spot;
        }

        // how far before a body the face of what its clip touches stands (its «touch <what> … <m>»; a part of a clip: its whole clip's); 0: it touches nothing
        internal static float Touches(string clip)
        {
            PAnimClip c = string.IsNullOrEmpty(clip) ? null : BodyClips.Find(clip);
            if (c == null || c.Touch.Length == 0) return 0f;
            string[] w = c.Touch.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float m;
            return w.Length > 1 && float.TryParse(w[w.Length - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out m) && m > 0f && m < 3f ? m : 0f;
        }

        // how far out of its middle, towards one who comes along «dir», the face of a thing is (all its pieces of that name: a gate's two halves):
        // rays along that way at the height of hands, a little to each side (the middle may be the gap between two halves) — none hit: at the shins
        // (a low thing: the kitchen hearth); the nearest face. The m of «before» is from it, for the player (StoryWalk) and a person (StoryScenes
        // NpcGo) alike — the user 2026-11-04: hands 0.4 m short of a gate set back in its frame; 2026-11-19: Heid «before 0.3» of the kitchen hearth
        // stood on it (a person's m was from the middle)
        internal static float FaceHigh = 1.3f, FaceAside = 0.35f, FaceLow = 0.3f;
        internal static bool FaceEdge(string place, Vector3 at, Vector3 dir, out float edge)
        {
            edge = 0f; bool any = false;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            List<ZNetView> pieces = Pieces(place);
            foreach (float[] ups in new[] { new[] { FaceHigh - 0.3f, FaceHigh }, new[] { FaceLow } })
            {
                foreach (ZNetView nv in pieces)
                    foreach (Collider c in nv.GetComponentsInChildren<Collider>()) // heavy: once when a scene's walk «before» starts
                    {
                        if (!c.enabled || c.isTrigger) continue;
                        foreach (float s in new[] { -FaceAside, 0f, FaceAside })
                            foreach (float up in ups)
                            {
                                Vector3 o = new Vector3(at.x, at.y + up, at.z) + side * s - dir * 3f;
                                RaycastHit hit;
                                if (c.Raycast(new Ray(o, dir), out hit, 6f)) { float e = 3f - hit.distance; if (!any || e > edge) edge = e; any = true; }
                            }
                    }
                if (any) return true;
            }
            return false;
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
            foreach (ZNetView nv in l) foreach (Collider c in nv.GetComponentsInChildren<Collider>()) if (On(c)) b.Encapsulate(c.bounds); // heavy: once a step that asks
            mid = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 along = l.Count > 1 ? l[1].transform.position - l[0].transform.position : l[0].transform.right;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) along = l[0].transform.right;
            way = Vector3.Cross(Vector3.up, along).normalized;
            Vector3 at; Quaternion rot;
            if (loc != null && loc.Anchor(out at, out rot)) { Vector3 outward = mid - at; outward.y = 0f; if (Vector3.Dot(way, outward) < 0f) way = -way; }
            return true;
        }

        // the same for one who walks up to it from «from» (a scene's «npc go … before», «player go … before»): on the side it comes from — the outer side
        // only says which side a gate is opened from by one who stands outside; a thing inside by a wall (the kitchen hearth, the user 2026-11-19:
        // Heid was sent between it and the wall, «stuck 2 m from its goal») is walked up to from the room. The condition «before» keeps the outer
        // side: it asks where one stands before a gate, whoever comes
        internal static bool Before(string name, Vector3 from, out Vector3 mid, out Vector3 way)
        {
            if (!Before(name, out mid, out way)) return false;
            Vector3 to = from - mid; to.y = 0f;
            if (Vector3.Dot(way, to) < 0f) way = -way;
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

        // the first of them its world's state has (LocState.Absent: the Hall's chest of 1.4 and the chest of 2 share a name), else the first
        internal static ZNetView Piece(string name)
        {
            List<ZNetView> l = Pieces(name);
            foreach (ZNetView nv in l) if (nv.IsValid() && !LocState.Absent(nv)) return nv;
            return l.Count > 0 ? l[0] : null;
        }

        // its body: the box of its colliders that are on (a hidden piece's are off, and an off one's box sits at the world's middle — the review 2026-11-19)
        private static bool Body(string name, out Bounds b)
        {
            ZNetView nv = Piece(name);
            b = new Bounds();
            if (nv == null) return false;
            b = new Bounds(nv.transform.position, Vector3.zero);
            foreach (Collider c in nv.GetComponentsInChildren<Collider>()) if (On(c)) b.Encapsulate(c.bounds);
            return true;
        }
        private static bool On(Collider c) { return c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy; }
    }
}
#endif

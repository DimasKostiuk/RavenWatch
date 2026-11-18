#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // An inside of ours made of the game's own rooms (the crypt of chapter 1 first; any theme of the game the same way): a layout written
    // beforehand is put into the world as it is, the same in every world. Three layers, each a file of assets/story/rooms:
    //   <name>.txt       the rooms, the game's things in them (doors, torches, chests, spawners) and the named points — made by
    //                    «node tools/rooms.js build <name>» (its head says the words; the lines read here: theme, air, room, door, thing, point)
    //   <name>.own.txt   the author's own pieces inside (his stones, his models of the Workshop), in the dungeon's measure, a Blueprint's line a
    //                    piece — written by «raven story rooms keep» from what he built there with the hammer
    // and its place in the story: the line of a place «inside: <name> [door <piece>]» (locations.list.txt): the inside belongs to that place.
    //
    // How it gets into a world, with no patch of the game (DungeonGenerator, Teleport, EnvZone, read 2026-11-14/15):
    //   the rooms   a dungeon of the game is one object (for a forest crypt DG_ForestCrypt) whose ZDO keeps «roomData»: the count, then for each
    //               room the hash of its name, its place and its turn IN THE WORLD (a turn is written as three angles). An object that comes
    //               with that data is never generated: each game sets the rooms' walls itself (Awake → Load → Spawn), and none of the rooms'
    //               own things (chests, spawners, doors). So Put writes a ZDO of the game's generator with our list, and the game does the rest —
    //               one generator for each zone the rooms lie in (the game keeps an object only near a player: Put says why).
    //   the things  doors, whatever the layout says and the author's own pieces are ZDOs put by us, each once; his pieces carry Blueprint's
    //               mark too: no wear, no fall, no damage, no hammer but his (the same patches that keep a place of the story) — and so do
    //               the layout's things that are pieces (its chests, the story's models, torches: 2026-11-15, the scroll's chest could be broken).
    //   the height  an inside hangs far over the land (the game holds «inside» to be anything over 3000 m, and its own dungeons at 5000 m
    //               over their door): Lift over its place, clear of a crypt of the game that may hang over the same spot.
    //   when        the server, once a world, as soon as the place stands (ServerTick): where it hangs is the world's key «<place's key>_inside
    //               x y z yaw». A world built before this gets it the same way.
    //   the doors, the air   what the game's own dungeons get from their Location and ours has to bring: made in each game for itself while
    //               that game is near (Tick → Make; nothing of the world, so a relog, another player, a new owner need nothing):
    //                 the way in    the game's Teleport («[E] Увійти», its own carrying and its stats) in a box round the place's named piece
    //                               «door <piece>» — any piece will do, a door of the game too: the box stands a little before it;
    //                 the way out   the same at the inside's own point, across the way in (a hole into nothing behind it: the layout shuts it
    //                               with «cap <entrance>.<n> <kind>»), back to before the door;
    //                 the air       the game's EnvZone: a box round the rooms that turns the layout's environment on (dark, fog, sound).
    //   the spaces  «space» of the layout: an empty box beyond a way, nothing of the game in it — the author builds its floor and walls himself.
    //               The layout's box (its air, where he may build, what «keep» takes) is stretched over it; on his machine each space's edges
    //               are drawn blue and the whole box's red (Edges).
    //   the look outside   «shell: <location of the game>» of the place: the game's own mound over the door (Shell), this game's own object too.
    //   the seal    «insideshut: <conditions> :: <words>» of the place: while these hold for a player the way in is a thing that says the words.
    //   the author builds inside   the game refuses most pieces over 3000 m unless the environment is one of EnvMan's own list of
    //               «building allowed here» — the layout's air joins that list on the author's machine only (Perches.StoryAuthor).
    // The story's words: the place «in.<name>» (where one comes in) and «in.<name>.<point>» (a point of the layout): a scene's place, a
    // camera's aim, where «stash» finds its chest. They are met only by one who is inside (StoryWhere.Apart): by the map an inside is the
    // spot of its own door.
    // What a room's small decor is (roots, rubble) the game draws by the room's place in the world, so it differs from world to world until
    // our own generator prefab says otherwise (m_useCustomInteriorTransform): not done.
    //
    // The author's walk through a layout (asleep without the author's folder, Blueprint.On):
    //   raven story rooms try <name>     the place's own inside if this world has one; else one put over you (or found, if one of that name
    //                                    is loaded) — and you are taken to its way in
    //   raven story rooms out            back to where you stood
    //   raven story rooms here [words]   where you stand, as a line of the layout says a place (room x y z turn), with your words: into the log
    //   raven story rooms keep [force]   every piece you built in the inside you stand in → <name>.own.txt (and they become the layout's own)
    //   raven story rooms clear          every tried layout loaded now taken out of the world, with its things and its own pieces
    //   raven story rooms clear world    the places' own insides too, and their keys: the server puts them anew from the files as they are now
    // A tried layout is a thing of the world (it stays in the save) till cleared while it is loaded; which one was tried is this session's.
    // a chest of the author's inside opens to everyone: a «private» one has no builder there (the game asks who built it)
    [HarmonyLib.HarmonyPatch(typeof(Container), "CheckAccess")]
    internal static class LocInsideChestPatch
    {
        private static void Postfix(Container __instance, ref bool __result)
        {
            try { if (!__result && LocInside.Own(__instance.GetComponentInParent<ZNetView>())) __result = true; }
            catch (Exception e) { PatchGuard.Fail("LocInsideChestPatch", e); }
        }
    }

    internal static class LocInside
    {
        internal const string Kind = "rooms";            // assets/story/rooms
        internal const string Place = "in.";             // the story's word of a place: «in.<layout>[.<point>]»
        private const string Mark = "RavenWatch_inside"; // on the generator's ZDO: the layout's name; on a thing of the layout: «<name>|thing»; on a piece of the author's: «<name>|own»
        private const string MarkAs = "RavenWatch_insideAs"; // on the generator's ZDO: what of the files it was put from (Version)
        private const string MarkAt = "RavenWatch_insideAt"; // on a generator's ZDO: the layout's own point in the world (a layout has a generator a zone: Put)
        private const string OfThing = "|thing", OfOwn = "|own";
        private const string OwnFile = ".own";           // «<name>.own.txt»: the author's own pieces
        private const float Lift = 5120f;                // m over its place (the game's own: 5000 over the door, some 12 m high)
        private const float Edge = 14f;                  // m: the air's box reaches this far beyond the rooms' middles (the biggest room is 20 m long)
        private const float RigNear = 90f;               // m by the map from its point (and its rooms' reach): this near a game makes its doors and its air
        private const float InZ = 1.5f;                  // m in from the way in: where one arrives
        private const float OutStep = 2f;                // m out from the door on the land: where one comes back
        private static readonly Vector3 ExitBox = new Vector3(2.6f, 3.2f, 0.6f);  // the way out: across the way in, this far into the room
        private static readonly Vector3 EntryBox = new Vector3(2.4f, 2.8f, 0.9f); // the way in: round the named piece, a little before it on both sides

        internal sealed class Part { public string Id = "", Name = ""; public Vector3 At; public float Turn; }
        internal sealed class Layout
        {
            public string Name = "", Theme = "ForestCrypt", Air = "Crypt", Print = "";
            public List<Part> Rooms = new List<Part>(), Things = new List<Part>(), Points = new List<Part>();
            public List<Blueprint.Part> Own = new List<Blueprint.Part>();
            public List<Space> Spaces = new List<Space>();
            public Vector3 Lo, Hi; // the rooms' middles, from the least to the greatest
            public Vector3 Middle { get { return (Lo + Hi) * 0.5f; } }
            public float Reach { get { return (Hi - Lo).magnitude * 0.5f + Edge; } }
            public string Version { get { return Print + "/" + Things.Count + "/" + Own.Count; } }
        }

        // an empty box of the layout the author builds in («space»): its middle and turn in the dungeon's measure, wide high long
        internal sealed class Space { public string Id = ""; public Vector3 At, Size; public float Turn; }

        // where a layout stands in this world: of a place (its key) or the author's try
        internal struct Stand
        {
            public Layout L; public Loc Of; public Vector3 At; public Quaternion Rot; public string As; // (As: what of the files a world's inside was put from; null — not a world's, the author's try)
            public Vector3 P(Vector3 local) { return At + Rot * local; }
        }

        private static Dictionary<string, Layout> _all;
        internal static void Reload() { _all = null; } // (StoryFiles.ReloadAll: the files read anew by whoever asks next)

        internal static Layout Get(string name)
        {
            if (_all == null) Read();
            Layout l;
            return _all.TryGetValue(name ?? "", out l) ? l : null;
        }

        private static void Read()
        {
            _all = new Dictionary<string, Layout>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> files = StoryFiles.All(Kind, null, false);
            foreach (KeyValuePair<string, string> f in files)
            {
                Layout l = new Layout { Name = Path.GetFileNameWithoutExtension(f.Key) };
                if (l.Name.EndsWith(OwnFile, StringComparison.OrdinalIgnoreCase)) continue; // (the author's pieces of a layout: below)
                bool good = true;
                foreach (string raw in f.Value.Replace("\r", "").Split('\n'))
                {
                    string line = StoryFiles.Bare(raw);
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w[0] == "theme:" && w.Length > 1) l.Theme = w[1];
                    else if (w[0] == "air:" && w.Length > 1) l.Air = w[1];
                    else if (w[0] == "print:" && w.Length > 1) l.Print = w[1];
                    else if (w[0] == "room" && w.Length >= 3) good &= Add(l.Rooms, w[1], w[2], w, 3, f.Key, line);                    // room <id> <kind> x y z turn
                    else if (w[0] == "space" && w.Length >= 9)                                                                          // space <id> x y z turn wide high long
                    {
                        Space sp = new Space { Id = w[1] }; float sx, sy, sz;
                        if (Num(w[2], out sp.At.x) && Num(w[3], out sp.At.y) && Num(w[4], out sp.At.z) && Num(w[5], out sp.Turn) && Num(w[6], out sx) && Num(w[7], out sy) && Num(w[8], out sz)) { sp.Size = new Vector3(sx, sy, sz); l.Spaces.Add(sp); }
                        else { Story.Log("rooms: " + f.Key + ": a space is «id x y z turn wide high long»: «" + line + "»"); good = false; }
                    }
                    else if (w[0] == "point" && w.Length >= 2) good &= Add(l.Points, w[1], w[1], w, 2, f.Key, line);                  // point <name> x y z turn <room>
                    else if ((w[0] == "door" || w[0] == "thing") && w.Length >= 2) good &= Add(l.Things, w[0], w[1], w, 2, f.Key, line); // door|thing <prefab> x y z turn [room]
                    else { Story.Log("rooms: " + f.Key + ": a line not understood: «" + line + "»"); good = false; }
                }
                if (!good || l.Rooms.Count == 0)
                {
                    Story.Log("rooms: " + f.Key + " is not taken (" + (good ? "no room in it" : "see the lines above") + "): build it again, node tools/rooms.js build " + l.Name);
                    continue;
                }
                l.Lo = l.Hi = l.Rooms[0].At;
                foreach (Part r in l.Rooms) { l.Lo = Vector3.Min(l.Lo, r.At); l.Hi = Vector3.Max(l.Hi, r.At); }
                foreach (Space sp in l.Spaces) // (a space is all of it inside the layout's box: the box is the rooms' middles and Edge beyond)
                {
                    bool q = Mathf.RoundToInt(sp.Turn) % 180 != 0;
                    Vector3 half = new Vector3(q ? sp.Size.z : sp.Size.x, sp.Size.y, q ? sp.Size.x : sp.Size.z) * 0.5f, edge = new Vector3(Edge, Edge, Edge);
                    l.Lo = Vector3.Min(l.Lo, sp.At - half + edge); l.Hi = Vector3.Max(l.Hi, sp.At + half - edge);
                }
                _all[l.Name] = l;
            }
            foreach (KeyValuePair<string, string> f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f.Key);
                if (!name.EndsWith(OwnFile, StringComparison.OrdinalIgnoreCase)) continue;
                Layout l;
                if (_all.TryGetValue(name.Substring(0, name.Length - OwnFile.Length), out l)) l.Own = Blueprint.Parse(f.Value.Replace("\r", "").Split('\n'), new List<Blueprint.Spot>());
                else Story.Log("rooms: " + f.Key + ": the author's pieces of a layout that is not there: left out");
            }
        }

        // «… x y z turn» from word i on
        private static bool Add(List<Part> to, string id, string name, string[] w, int i, string file, string line)
        {
            float x, y, z, t;
            if (w.Length < i + 4 || !Num(w[i], out x) || !Num(w[i + 1], out y) || !Num(w[i + 2], out z) || !Num(w[i + 3], out t))
            { Story.Log("rooms: " + file + ": a place is «x y z turn»: «" + line + "»"); return false; }
            to.Add(new Part { Id = id, Name = name, At = new Vector3(x, y, z), Turn = t });
            return true;
        }
        private static bool Num(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string Q(float v) { return v.ToString("0.#####", CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- where it stands
        private static readonly Dictionary<string, Stand> _tried = new Dictionary<string, Stand>(StringComparer.OrdinalIgnoreCase); // session: the layouts the author tried, by name (the world keeps the objects, not which of them he walks)
        private static object _world; // session: the world the tried ones and the rigs are of
        private sealed class Kept { public string Key, As = ""; public bool Ok; public Vector3 At; public float Yaw; } // (As: the layout's Version it was put from — the key's fifth word; none: put before it was kept)
        private static readonly Dictionary<string, Kept> _kept = new Dictionary<string, Kept>(); // (a place's key read once per change: the story's words ask often)

        // a place's own inside, by the world's key (false: it has none, or the server has not put it yet)
        internal static bool Where(Loc l, out Stand s)
        {
            s = new Stand();
            string v;
            if (l.Inside.Length == 0 || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(l.KeyInside, out v)) return false;
            Kept k;
            if (!_kept.TryGetValue(l.Id, out k)) _kept[l.Id] = k = new Kept();
            if (!ReferenceEquals(v, k.Key))
            {
                k.Key = v;
                string[] w = (v ?? "").Split(' ');
                float x = 0f, y = 0f, z = 0f;
                k.Ok = w.Length >= 4 && Num(w[0], out x) && Num(w[1], out y) && Num(w[2], out z) && Num(w[3], out k.Yaw);
                k.At = new Vector3(x, y, z);
                k.As = w.Length > 4 ? w[4] : "";
                if (!k.Ok) l.Log("inside: its key is not «x y z yaw»: «" + v + "»");
            }
            s.L = Get(l.Inside); s.Of = l; s.At = k.At; s.Rot = Quaternion.Euler(0f, k.Yaw, 0f); s.As = k.As;
            return k.Ok && s.L != null;
        }

        // a layout by its name: the one the author tried, else the one of the place that has it
        internal static bool Standing(string layout, out Stand s)
        {
            if (_tried.TryGetValue(layout ?? "", out s) && ReferenceEquals(_world, ZNet.instance)) { s.L = Get(layout) ?? s.L; return true; }
            foreach (Loc l in Locs.All)
                if (string.Equals(l.Inside, layout, StringComparison.OrdinalIgnoreCase) && Where(l, out s)) return true;
            s = new Stand();
            return false;
        }

        // «in.<layout>» — where one comes in; «in.<layout>.<point>» — a point of the layout (StoryWhere.Prefixed)
        internal static bool PlaceAt(string rest, out Vector3 at)
        {
            at = Vector3.zero;
            int dot = rest.IndexOf('.');
            string point = dot < 0 ? "" : rest.Substring(dot + 1);
            Stand s;
            if (!Standing(dot < 0 ? rest : rest.Substring(0, dot), out s)) return false;
            if (point.Length == 0) { at = s.P(new Vector3(0f, 0f, InZ)); return true; }
            foreach (Part p in s.L.Points) if (p.Name == point) { at = s.P(p.At); return true; }
            return false;
        }

        // a point of the inside on the map: the way into it on the land (the inside hangs Lift m over its door; the map draws by x and z, so its own
        // spot would show over whatever lies 100 m off on the land — the user 2026-11-15 saw «Скриня кургану» over the shore)
        internal static bool DoorOnMap(string rest, out Vector3 at)
        {
            at = Vector3.zero;
            int dot = rest.IndexOf('.');
            Stand s;
            if (!Standing(dot < 0 ? rest : rest.Substring(0, dot), out s)) return false;
            at = s.Of != null ? s.At - Vector3.up * Lift : s.At;
            return true;
        }

        // the story's part (StoryModules.Conds): its word of a place
        internal static void Places()
        {
            StoryWhere.Prefixed[Place] = PlaceAt;
            StoryWhere.OnMap[Place] = DoorOnMap;
            StoryWhere.WorldKept.Add(Place); // (the world's: known with no player)
        }

        // ---------------------------------------------------------------- into the world
        // the layout put with its own point at <origin>, looking <yaw>: the generator's ZDO with the rooms, a ZDO a thing, a ZDO a piece of the
        // author's. Null: done, else why not. Nothing is checked against what stands there already: who calls picks the place.
        internal static string Put(Layout l, Vector3 origin, float yaw, out int things)
        {
            things = 0;
            if (ZNetScene.instance == null || ZDOMan.instance == null || DungeonDB.instance == null) return "not in a world";
            Room.Theme theme = Room.Theme.None;
            foreach (Room.Theme t in Enum.GetValues(typeof(Room.Theme))) if (string.Equals(t.ToString(), l.Theme, StringComparison.OrdinalIgnoreCase)) theme = t;
            if (theme == Room.Theme.None) return "no theme «" + l.Theme + "» in the game";
            GameObject gen = ZNetScene.instance.GetPrefab("DG_" + theme); // (the game's own names: DG_ForestCrypt, DG_SunkenCrypt, DG_Cave…)
            if (gen != null && gen.GetComponent<DungeonGenerator>() == null) gen = null;
            if (gen == null)
                foreach (GameObject g in ZNetScene.instance.m_prefabs) // (only for a theme named otherwise, once a layout put: every prefab asked for its generator part)
                {
                    DungeonGenerator d = g != null ? g.GetComponent<DungeonGenerator>() : null;
                    if (d != null && d.m_themes == theme) { gen = g; break; }
                }
            if (gen == null) return "the game has no dungeon generator of the theme " + theme;

            Dictionary<string, int> hashOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DungeonDB.RoomData rd in DungeonDB.GetRooms()) hashOf[rd.m_prefab.Name] = rd.Hash;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            // A generator a zone: the rooms are children of the generator's object, and the game keeps an object only while its own zone is
            // within one zone of a player — so the rooms of one generator would go from under a player two zones (64 m each) along. Each zone's
            // rooms hang on a generator of their own, standing in that zone (at its first room); a layout may be as long as it likes.
            foreach (Part r in l.Rooms) if (!hashOf.ContainsKey(r.Name)) return "the game has no room «" + r.Name + "» (a new version of the game? take the kinds again: tools/rooms.js)";
            Dictionary<Vector2i, List<Part>> byZone = new Dictionary<Vector2i, List<Part>>();
            foreach (Part r in l.Rooms)
            {
                Vector2i zone = ZoneSystem.GetZone(origin + rot * r.At).ToVector2i();
                List<Part> of;
                if (!byZone.TryGetValue(zone, out of)) byZone[zone] = of = new List<Part>();
                of.Add(r);
            }
            foreach (List<Part> of in byZone.Values)
            {
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(of.Count);
                    foreach (Part r in of)
                    {
                        Vector3 at = origin + rot * r.At;
                        bw.Write(hashOf[r.Name]);
                        bw.Write(at.x); bw.Write(at.y); bw.Write(at.z);                              // (as the game's BinaryWriter.Write(Vector3))
                        bw.Write(0f); bw.Write(Mathf.Repeat(yaw + r.Turn, 360f)); bw.Write(0f);      // (its Write(Quaternion): the three angles)
                    }
                    bw.Flush();
                    data = ms.ToArray();
                }
                ZDO z = New(gen.name, origin + rot * of[0].At, rot, l.Name);
                z.Set(ZDOVars.s_roomData, data);
                z.Set(MarkAs, l.Version);
                z.Set(MarkAt, origin); // (the layout's own point: this generator stands at a room of its zone)
            }
            int own = 0;
            foreach (Part t in l.Things)
            {
                GameObject pf = ZNetScene.instance.GetPrefab(t.Name);
                if (pf == null) { Story.Log("rooms: " + l.Name + ": the game has no «" + t.Name + "»: left out"); continue; }
                ZDO tz = New(t.Name, origin + rot * t.At, Quaternion.Euler(0f, yaw + t.Turn, 0f), l.Name + OfThing);
                if (pf.GetComponent<Piece>() != null && pf.GetComponent<Door>() == null) tz.Set(Blueprint.Mark, 1); // (a piece the layout puts — the scroll's chest, the story's models, a ground torch: the place's own, as the author's; spawners, bone piles, what is picked stay the game's; a door not: people pass no marked door outside a place's own — NpcDoors)
                things++;
            }
            foreach (Blueprint.Part o in l.Own)
            {
                if (ZNetScene.instance.GetPrefab(o.Prefab) == null) { Story.Log("rooms: " + l.Name + ": no «" + o.Prefab + "» in this game (a piece of the author's): left out"); continue; }
                ZDO p = New(o.Prefab, origin + rot * o.Pos, rot * o.Rot, l.Name + OfOwn);
                p.Set(Blueprint.Mark, 1); // (the place's own: no wear, no fall, no damage, no hammer but the author's)
                if (!string.IsNullOrEmpty(o.Item)) Blueprint.HangOn(p, o);
                own++;
            }
            Story.Log("rooms: «" + l.Name + "» put at " + Mathf.RoundToInt(origin.x) + " " + Mathf.RoundToInt(origin.y) + " " + Mathf.RoundToInt(origin.z) + ": " + l.Rooms.Count + " rooms, " + things + " things, " + own + " pieces of the author's");
            things += own;
            return null;
        }

        private static ZDO New(string prefab, Vector3 at, Quaternion rot, string mark)
        {
            int hash = prefab.GetStableHashCode();
            ZDO z = ZDOMan.instance.CreateNewZDO(at, hash);
            z.Persistent = true;
            z.SetPrefab(hash);
            z.SetRotation(rot);
            z.Set(Mark, mark);
            return z;
        }

        // a thing of that layout: its rooms, a thing of its lines, a piece of the author's (a place built again takes its inside away with its
        // own pieces: LocBuild) — and how far from its own point the layout reaches
        internal static bool Marked(ZDO z, string layout)
        {
            string m = z.GetString(Mark, "");
            return m.Length > 0 && (string.Equals(m, layout, StringComparison.OrdinalIgnoreCase) || m.StartsWith(layout + "|", StringComparison.OrdinalIgnoreCase));
        }
        internal static float Span(string layout) { Layout l = Get(layout); return l == null ? 0f : Mathf.Max(l.Lo.magnitude, l.Hi.magnitude) + Edge; }

        // ---------------------------------------------------------------- the author's pieces, for whoever comes in
        // A piece of the author's inside (kept: Mark «…|own») is a thing to look at (the user 2026-11-15): E does nothing on it — but a seat (a bench, a
        // throne: Chair) and a chest (Container: the chest of the story's thing, open to everyone even when it is a «private» one: nobody built it, so
        // the game would open it to nobody). Its workbenches and forges are no stations: nothing made, built or learnt by them (a player may not know
        // of them yet). What hangs on its stands stays. The author himself dresses the stands; the rest is so for him too, so he sees what a player gets.
        //   E            LocInteractPatch (the one guard of E) asks Usable
        //   the stations PieceIn, as the piece comes into a game (NpcBasePieceIn), and Keep for the ones standing: LocState.DeadStations — and cold (Cold:
        //                its fire, light, sparks and sound out: the user 2026-11-15, the black forge in the barrow burned on)
        //   the chest    LocInsideChestPatch
        //   the stands   the patches of a place's stands (Blueprint.cs) ask StandKept
        internal static bool Own(ZNetView nv) { return nv != null && nv.IsValid() && IsOwn(nv.GetZDO()); }
        // any piece of an inside of ours — its rooms, a thing of its lines, the author's own (Blueprint.StoryPiece)
        internal static bool Ours(ZNetView nv) { return nv != null && nv.IsValid() && nv.GetZDO().GetString(Mark, "").Length > 0; }
        private static bool IsOwn(ZDO z) { return z != null && z.GetString(Mark, "").EndsWith(OfOwn, StringComparison.Ordinal); }
        internal static bool StandKept(ZNetView nv) { return !Perches.StoryAuthor && Own(nv); }
        internal static bool Usable(GameObject go) { return go.GetComponentInParent<Chair>() != null || go.GetComponentInParent<Container>() != null || (Perches.StoryAuthor && go.GetComponentInParent<ItemStand>() != null); }
        internal static void PieceIn(Piece pc)
        {
            ZNetView nv = pc.GetComponent<ZNetView>();
            if (nv == null || !IsOwn(ZNetView.m_initZDO ?? (nv.IsValid() ? nv.GetZDO() : null))) return; // (as the game makes it from its ZDO, the view may not have it yet)
            LocState.DeadStations(nv);
            Cold(nv);
            NoBase(nv);
        }

        // no piece of the author's inside makes a player's base there (a workbench, a forge, a bed, a fire: the game's «PlayerBase» area round them) —
        // in a base the game's spawners never spawn (CreatureSpawner.m_spawnInPlayerBase), and the crypt stood with its spawners and no foes (the user
        // 2026-11-15). The area is left, only that kind taken off it (the game asks every area by its kinds each time: EffectArea.IsPointInsideArea)
        private static void NoBase(ZNetView nv)
        {
            foreach (EffectArea a in nv.GetComponentsInChildren<EffectArea>(true)) a.m_type &= ~EffectArea.Type.PlayerBase; // heavy: once as the piece comes in
        }

        // a station of the author's inside is scenery: no fire on it, no light, no sparks, no sound (its own fire and in-use looks too)
        private static void Cold(ZNetView nv)
        {
            foreach (CraftingStation s in nv.GetComponentsInChildren<CraftingStation>(true)) // heavy: once as the piece comes in
            {
                if (s.m_haveFireObject != null) s.m_haveFireObject.SetActive(false);
                if (s.m_inUseObject != null) s.m_inUseObject.SetActive(false);
                foreach (ParticleSystem ps in s.GetComponentsInChildren<ParticleSystem>(true)) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ParticleSystem.EmissionModule em = ps.emission; em.enabled = false; } // heavy: once as the piece comes in
                foreach (Light l in s.GetComponentsInChildren<Light>(true)) l.enabled = false; // heavy: once as the piece comes in
                foreach (AudioSource a in s.GetComponentsInChildren<AudioSource>(true)) { a.Stop(); a.enabled = false; } // heavy: once as the piece comes in
            }
        }

        // ---------------------------------------------------------------- the server: a place's inside, once a world
        private static float _nextServer;
        private static readonly Dictionary<string, float> _rest = new Dictionary<string, float>(); // session: a place whose inside could not be put: tried again after a rest

        internal static void ServerTick()
        {
            if (Time.time < _nextServer) return;
            _nextServer = Time.time + 2f;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null) return;
            foreach (Loc l in Locs.All)
            {
                string has; float rest;
                if (l.Inside.Length == 0 || !l.Built || ZoneSystem.instance.GetGlobalKey(l.KeyInside, out has)) continue;
                if (_rest.TryGetValue(l.Id, out rest) && Time.time < rest) continue;
                Layout lay = Get(l.Inside);
                Vector3 at = Vector3.zero; Quaternion rot = Quaternion.identity; int things = 0;
                string why = lay == null ? "no layout «" + l.Inside + "» in assets/story/rooms" : !l.Where(out at, out rot) ? "the place has no spot" : null;
                Vector3 origin = at + Vector3.up * Lift;
                if (why == null) why = Put(lay, origin, rot.eulerAngles.y, out things);
                if (why != null) { _rest[l.Id] = Time.time + 60f; l.Log("inside: not put: " + why + " (tried again in a minute)"); continue; }
                ZoneSystem.instance.SetGlobalKey(l.KeyInside + " " + F(origin.x) + " " + F(origin.y) + " " + F(origin.z) + " " + F(rot.eulerAngles.y) + " " + lay.Version);
                l.Log("inside: «" + lay.Name + "» hangs " + Mathf.RoundToInt(Lift) + " m over it: " + lay.Rooms.Count + " rooms, " + things + " things");
            }
        }

        // ---------------------------------------------------------------- every game: its doors and its air while it is near
        private sealed class Rig { public GameObject Root, Open, Shut; public string Air = "", As = ""; public float SweepAt; }
        private static readonly Dictionary<string, Rig> _rigs = new Dictionary<string, Rig>(StringComparer.OrdinalIgnoreCase); // session: this game's own objects, by layout
        private static readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _gone = new List<string>();
        private static readonly HashSet<string> _told = new HashSet<string>(); // (said once a game)
        private static readonly HashSet<string> _staleTold = new HashSet<string>(); // (said once a game: the author's note of an inside put from other files)
        private static float _next;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            Player me = Player.m_localPlayer;
            if (!ReferenceEquals(_world, ZNet.instance)) { DropAll(); _tried.Clear(); _fitted.Clear(); _fromSet = false; _world = ZNet.instance; } // (another world: nothing of the last one)
            if (me == null || ZoneSystem.instance == null) { if (_rigs.Count > 0) DropAll(); return; }
            _seen.Clear();
            foreach (KeyValuePair<string, Stand> t in _tried) { Stand s = t.Value; s.L = Get(t.Key) ?? s.L; Look(me, s); }
            foreach (Loc l in Locs.All)
            {
                Stand s;
                if (l.Inside.Length > 0 && !_tried.ContainsKey(l.Inside) && Where(l, out s)) Look(me, s);
            }
            _gone.Clear();
            foreach (string name in _rigs.Keys) if (!_seen.Contains(name)) _gone.Add(name);
            foreach (string name in _gone) Drop(name);
        }

        private static void Look(Player me, Stand s)
        {
            if (Utils.DistanceXZ(me.transform.position, s.At) > RigNear + s.L.Reach) return;
            _seen.Add(s.L.Name);
            Vector3 door, outward;
            bool way = DoorOf(s, out door, out outward);
            string made = s.At + "|" + s.Rot.eulerAngles.y + "|" + s.L.Print + "|" + s.L.Air + "|" + s.L.Lo + s.L.Hi + s.L.Spaces.Count + "|" + (way ? door.ToString("F2") + outward.ToString("F2") : _fromSet && s.Of == null ? _from.ToString("F1") : "");
            if (s.Of != null) made += "|" + s.Of.Shell + s.Of.ShellShift + s.Of.ShellTurn + "|" + s.Of.InsideShutWords;
            Rig r;
            if (!_rigs.TryGetValue(s.L.Name, out r) || r.Root == null || r.As != made)
            {
                if (r != null) Drop(s.L.Name);
                _rigs[s.L.Name] = r = Make(s, way, door, outward, made);
            }
            if (Time.time >= r.SweepAt) { r.SweepAt = Time.time + SweepEvery; Sweep(s); }
            // an inside stands in a world once, as its files were then: the author is told when they have changed since (the user 2026-11-15: no foes in the
            // crypt — it stood from the layout before its spawners); a world of players is never rebuilt by itself (their chests would go)
            if (Perches.StoryAuthor && s.Of != null && (s.As ?? "") != s.L.Version && _staleTold.Add(s.L.Name))
            {
                s.Of.Log("inside: «" + s.L.Name + "» stands as it was put " + ((s.As ?? "").Length > 0 ? "from other files (" + s.As + ", now " + s.L.Version + ")" : "before its version was kept") + ": raven story rooms clear world");
                if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "Нутро «" + s.L.Name + "» у цьому світі — зі старого розкладу: raven story rooms clear world");
            }
            // the seal («insideshut:»): the way in is this player's while the story lets him in, else a thing that says its words
            if (r.Open == null || r.Shut == null) return;
            bool shut = s.Of != null && s.Of.InsideShut.Length > 0 && StoryCond.Holds(me, s.Of.InsideShut, e => s.Of.Log("insideshut: " + e));
            if (r.Shut.activeSelf != shut) r.Shut.SetActive(shut);
            if (r.Open.activeSelf == shut) r.Open.SetActive(!shut);
        }

        // the game's own signs a room carries as part of its walls (a Vegvisir to a boss in a burial chamber, a runestone): not in an inside of the story — it
        // is our crypt, not the game's way to the Elder (the user 2026-11-15). A part of the room, no thing of its own (no ZNetView): every game hides it
        // itself, every SweepEvery s while it is near (rooms come in as their zones load). Cost: two looks for objects of those kinds — few in any scene
        internal static float SweepEvery = 5f;
        private static void Sweep(Stand s)
        {
            Vector3 mid = s.P(s.L.Middle); float reach = s.L.Reach;
            foreach (Vegvisir v in UnityEngine.Object.FindObjectsByType<Vegvisir>(FindObjectsSortMode.None)) // heavy: every SweepEvery s near an inside (a few in a scene)
                if (v != null && Vector3.Distance(v.transform.position, mid) <= reach) v.gameObject.SetActive(false);
            foreach (RuneStone rs in UnityEngine.Object.FindObjectsByType<RuneStone>(FindObjectsSortMode.None)) // heavy: the same
                if (rs != null && Vector3.Distance(rs.transform.position, mid) <= reach) rs.gameObject.SetActive(false);
            Fit(s);
        }

        // the author's check of a layout's things (the user 2026-11-15: the archivist's table stood in the wall, the note's chest inside the stone altar — the
        // layout's numbers were guessed from a room's size and never seen): each thing once a game, as it comes in with its room round it — the room's stone
        // or another thing in its box, or a surface low over its top (a thing wholly in the stone touches none of its faces) — is said in the log with the
        // layout's own numbers, to move it in tools/rooms/<name>.rooms.txt. The author's machine only; a pickable grows from the stone as the game puts it
        private static readonly HashSet<ZDOID> _fitted = new HashSet<ZDOID>();
        private static readonly List<ZDO> _fitNear = new List<ZDO>();
        private static readonly Collider[] _fitHits = new Collider[16];
        private static int _solid;
        private const float FitSlack = 0.15f, FitRoof = 1f, FitFloor = 2f;
        private static void Fit(Stand s)
        {
            if (!Perches.StoryAuthor || ZNetScene.instance == null) return;
            if (_solid == 0) _solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
            string mine = s.L.Name + OfThing;
            _fitNear.Clear();
            BlueprintGround.ObjectsAround(s.P(s.L.Middle), s.L.Reach, _fitNear); // heavy: every SweepEvery s near an inside, the author only
            foreach (ZDO z in _fitNear)
            {
                if (_fitted.Contains(z.m_uid) || z.GetString(Mark, "") != mine) continue;
                ZNetView nv = ZNetScene.instance.FindInstance(z);
                if (nv == null) continue; // (not in the game yet: the next sweep)
                GameObject go = nv.gameObject;
                // ours only: a piece or a story model; the game's own things («fill») stand where the game keeps them (roots grow from the walls)
                if (go.GetComponent<Piece>() == null && !Utils.GetPrefabName(go).StartsWith("RavenWatch_", StringComparison.Ordinal)) { _fitted.Add(z.m_uid); continue; }
                Bounds b;
                if (!Seen(go, out b)) { _fitted.Add(z.m_uid); continue; }
                RaycastHit h;
                if (!Other(go, b.center + Vector3.up * 0.1f, Vector3.down, b.extents.y + FitFloor, out h)) continue; // (its room not in yet: the next sweep)
                _fitted.Add(z.m_uid);
                string why = Clash(go, b, Vector3.zero);
                if (why == null) continue;
                Quaternion back = Quaternion.Inverse(s.Rot);
                Vector3 local = back * (go.transform.position - s.At);
                Vector3 free;
                string to = "; no free spot within " + F(FitSeek) + " m";
                if (Free(go, b, out free))
                {
                    to = "; free at " + Three(local + back * free) + " — moved there in this world now, write that in the layout";
                    nv.ClaimOwnership(); // (the author sees it right at once; the layout file is what every other world is put from)
                    go.transform.position += free;
                    z.SetPosition(go.transform.position);
                }
                Story.Log("rooms: «" + s.L.Name + "»: " + Utils.GetPrefabName(go) + " at " + Three(local) + " (the dungeon's measure) " + why + to + " (tools/rooms/" + s.L.Name + ".rooms.txt)");
            }
        }

        private static string Three(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }

        // a thing's whole look (what the eye sees in the wall: a model of parts may carry no body at all) — its meshes, not its smoke
        private static bool Seen(GameObject go, out Bounds b)
        {
            b = default(Bounds); bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) // heavy: once a thing, the author only
            {
                if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
            }
            return any;
        }

        // what the thing's look, moved by «shift», stands in or under: null — nothing
        private static string Clash(GameObject go, Bounds b, Vector3 shift)
        {
            Vector3 half = b.extents - Vector3.one * FitSlack, mid = b.center + shift;
            if (half.x > 0f && half.y > 0f && half.z > 0f)
            {
                int n = Physics.OverlapBoxNonAlloc(mid, half, _fitHits, Quaternion.identity, _solid, QueryTriggerInteraction.Ignore); // heavy: once a thing, the author only
                for (int i = 0; i < n; i++) if (!_fitHits[i].transform.IsChildOf(go.transform)) return "stands in " + Named(_fitHits[i]);
            }
            RaycastHit h;
            Vector3 top = new Vector3(mid.x, b.max.y + shift.y, mid.z);
            if (Other(go, top + Vector3.up * FitRoof, Vector3.down, FitRoof - FitSlack, out h)) return "is under " + Named(h.collider) + " (" + F(h.point.y - top.y) + " m over its top)";
            return null;
        }

        // the nearest spot round it, on the same floor, where its look stands in nothing (rings of FitStep m out to FitSeek m, FitDirs ways each)
        internal static float FitSeek = 4f, FitStep = 0.25f;
        private const int FitDirs = 16;
        private static bool Free(GameObject go, Bounds b, out Vector3 shift)
        {
            for (float r = FitStep; r <= FitSeek + 0.001f; r += FitStep)
                for (int i = 0; i < FitDirs; i++)
                {
                    float a = i * Mathf.PI * 2f / FitDirs;
                    shift = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    RaycastHit h;
                    if (!Other(go, b.center + shift + Vector3.up * 0.1f, Vector3.down, b.extents.y + 0.5f, out h) || Mathf.Abs(h.point.y - b.min.y) > 0.3f) continue; // (the same floor under it)
                    if (Clash(go, b, shift) == null) return true;
                }
            shift = Vector3.zero;
            return false;
        }

        private static bool Other(GameObject go, Vector3 from, Vector3 dir, float far, out RaycastHit hit)
        {
            hit = default(RaycastHit); float best = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, far, _solid, QueryTriggerInteraction.Ignore)) // heavy: once a thing, the author only
                if (!h.collider.transform.IsChildOf(go.transform) && h.distance < best) { best = h.distance; hit = h; }
            return best < float.MaxValue;
        }

        private static string Named(Collider c) { Transform p = c.transform.parent; return "«" + (p != null ? p.name + "/" : "") + c.name + "»"; }

        // where the place's way in stands and which way is out of it: its named piece («door <piece>»), away from the place's middle
        private static bool DoorOf(Stand s, out Vector3 door, out Vector3 outward)
        {
            door = Vector3.zero; outward = Vector3.forward;
            Loc l = s.Of;
            if (l == null) return false;
            Vector3 mid; Quaternion rot;
            if (l.InsideDoor.Length == 0 || !l.NamedSpot(l.InsideDoor, out door) || !l.Anchor(out mid, out rot))
            {
                if (_told.Add(l.Id)) l.Log("inside: no way in — " + (l.InsideDoor.Length == 0 ? "its line names no piece («inside: " + l.Inside + " door <piece>»)" : "no piece named «" + l.InsideDoor + "» (its names file)") + ": only a scene or the author gets there");
                return false;
            }
            outward = door - mid; outward.y = 0f;
            outward = outward.sqrMagnitude < 0.25f ? rot * Vector3.forward : outward.normalized; // (a door in the very middle: the place's own front)
            return true;
        }

        private static GameObject Child(GameObject root, string name, Vector3 at, Quaternion rot)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            g.transform.SetPositionAndRotation(at, rot);
            return g;
        }

        // a point the game's Teleport carries one to: it puts him a step before the point and a step under it (Teleport.GetTeleportPoint)
        private static Teleport Arrive(GameObject root, string name, Vector3 feet, Quaternion looking)
        {
            return Child(root, name, feet - looking * Vector3.forward + Vector3.up * 1.2f, looking).AddComponent<Teleport>();
        }

        private static Rig Make(Stand s, bool way, Vector3 door, Vector3 outward, string made)
        {
            Rig r = new Rig { Root = new GameObject("RavenWatch_inside_" + s.L.Name), Air = s.L.Air, As = made };
            // the air: a box round the rooms that turns the game's environment on for the one who stands in it
            GameObject air = Child(r.Root, "air", s.P(s.L.Middle), s.Rot);
            BoxCollider box = air.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = s.L.Hi - s.L.Lo + new Vector3(Edge, Edge, Edge) * 2f;
            air.AddComponent<EnvZone>().m_environment = s.L.Air; // (m_force is the game's own default: the environment is forced while inside)
            // its layer is the game's own for such boxes: on the default one the game's «does this piece go through anything» test (Player.TestGhostClipping,
            // asked for a workbench, a chair, a bed…) met this box round everything and refused them all (the user 2026-11-15)
            int trig = LayerMask.NameToLayer("character_trigger");
            if (trig >= 0) air.layer = trig; else if (_told.Add("layer")) Story.Log("rooms: the game has no layer «character_trigger»: the air's box stays on the default one, and pieces that may not clip are refused inside");
            // the two ways: each a solid box under E that carries to the other's point
            Teleport comeIn = Arrive(r.Root, "in", s.P(new Vector3(0f, 0.2f, InZ)), s.Rot);
            GameObject exit = Child(r.Root, "exit", s.P(new Vector3(0f, ExitBox.y * 0.5f, ExitBox.z * 0.5f)), s.Rot);
            exit.AddComponent<BoxCollider>().size = ExitBox;
            Teleport wayOut = exit.AddComponent<Teleport>();
            wayOut.m_hoverText = "$location_exit";
            if (way)
            {
                Quaternion face = Quaternion.LookRotation(outward, Vector3.up);
                wayOut.m_targetPoint = Arrive(r.Root, "out", door + outward * OutStep + Vector3.up * 0.2f, face);
                GameObject entry = Child(r.Root, "entry", door + Vector3.up * (EntryBox.y * 0.5f), face);
                entry.AddComponent<BoxCollider>().size = EntryBox;
                Teleport wayIn = entry.AddComponent<Teleport>();
                wayIn.m_targetPoint = comeIn;
                wayIn.m_enterText = Lang.T(s.Of.Title); // (the game's own golden line on coming in, as its dungeons have)
                r.Open = entry;
                // sealed: the same box with the game's own «words when looked at» and no way through (Look turns one of the two on)
                r.Shut = Child(r.Root, "sealed", entry.transform.position, face);
                r.Shut.AddComponent<BoxCollider>().size = EntryBox;
                r.Shut.AddComponent<HoverText>().m_text = Lang.T(s.Of.InsideShutWords);
                r.Shut.SetActive(false);
                if (s.Of.Shell.Length > 0) Shell(r.Root, s.Of, door, face);
            }
            else if (s.Of != null) // (a place with no door named: nobody walks in, but one a scene brought in must get out — before the place's front)
            {
                Vector3 mid; Quaternion front;
                if (s.Of.Anchor(out mid, out front)) wayOut.m_targetPoint = Arrive(r.Root, "out", s.Of.BeforeFront(mid, front) + Vector3.up * 0.2f, front);
            }
            else if (_fromSet) wayOut.m_targetPoint = Arrive(r.Root, "out", _from + Vector3.up * 0.2f, Quaternion.identity); // (the author's try: back to where he stood)
            // the author builds inside: the layout's air is one the game lets him build in (EnvMan's own list; the game asks it for every piece but a few)
            if (Perches.StoryAuthor && EnvMan.instance != null && !EnvMan.instance.m_interiorBuildingOverrideEnvironments.Contains(s.L.Air))
                EnvMan.instance.m_interiorBuildingOverrideEnvironments.Add(s.L.Air);
            // … and sees where he may: each space's edges (blue), and the edges of the whole box beyond which nothing is built or kept (red)
            if (Perches.StoryAuthor)
            {
                foreach (Space sp in s.L.Spaces) Edges(r.Root, "space " + sp.Id, s.P(sp.At), s.Rot * Quaternion.Euler(0f, sp.Turn, 0f), sp.Size, new Color(0.3f, 0.8f, 1f));
                Edges(r.Root, "box", s.P(s.L.Middle), s.Rot, box.size, new Color(1f, 0.25f, 0.2f));
            }
            return r;
        }

        // The look of a location of the game over the place («shell: Crypt2»): its mound and stones as the game has them, with none of what makes it
        // a dungeon of the game — its networked things (its own generator, its spawners: the game itself leaves those out of a location it only draws,
        // ZoneSystem.SpawnLocation in its «client» way, done here the same), its inside, its own ways in and out. This game's own object, as the
        // rest of the rig; it stands so that the game's own way in — the Teleport on the land — is where the place's door is, turned the same,
        // then moved by the place's own numbers. Its Location part stays: no building round it, as round any crypt
        private static void Shell(GameObject root, Loc l, Vector3 door, Quaternion face)
        {
            ZoneSystem.ZoneLocation zl = null;
            foreach (ZoneSystem.ZoneLocation z in ZoneSystem.instance.m_locations) // (once a rig: the game's list of its locations, some hundreds)
                if (z != null && z.m_prefab.IsValid && string.Equals(z.m_prefab.Name, l.Shell, StringComparison.OrdinalIgnoreCase)) { zl = z; break; }
            if (zl == null) { if (_told.Add("shell" + l.Id)) l.Log("shell: the game has no location «" + l.Shell + "»: nothing stands over the door"); return; }
            zl.m_prefab.Load();
            GameObject made;
            ZNetView[] nets = global::Utils.GetEnabledComponentsInChildren<ZNetView>(zl.m_prefab.Asset); // heavy: once a rig
            Vector3 at = Vector3.zero; float yaw = 0f;
            try
            {
                // its own way in on the land, read off the game's own copy before ours is made: ours is made where it is to stand (made elsewhere
                // and moved, its Location part and its ground-shapers would have woken at the wrong spot)
                Transform top = zl.m_prefab.Asset.transform;
                Teleport way = null;
                foreach (Teleport t in zl.m_prefab.Asset.GetComponentsInChildren<Teleport>(true)) // heavy: once a rig
                    if (top.InverseTransformPoint(t.transform.position).y < 1000f && (way == null || t.m_targetPoint != null)) way = t;
                if (way != null) { at = top.InverseTransformPoint(way.transform.position); yaw = way.transform.eulerAngles.y - top.eulerAngles.y; }
                else if (_told.Add("way" + l.Id)) l.Log("shell: «" + l.Shell + "» has no way in on the land: it stands with its middle at the door");
                Quaternion rot = Quaternion.Euler(0f, face.eulerAngles.y - yaw + l.ShellTurn, 0f);
                Vector3 pos = door - rot * new Vector3(at.x, 0f, at.z) + face * l.ShellShift; // (its own ground at the door's foot: a way in hangs over its sill)
                foreach (ZNetView v in nets) v.gameObject.SetActive(false);
                made = SoftReferenceableAssets.Utils.Instantiate(zl.m_prefab, pos, rot);
            }
            finally { foreach (ZNetView v in nets) v.gameObject.SetActive(true); zl.m_prefab.Release(); }
            made.name = "shell " + l.Shell;
            foreach (ZNetView v in made.GetComponentsInChildren<ZNetView>(true)) UnityEngine.Object.Destroy(v.gameObject); // heavy: once a rig (the switched-off copies of its networked things)
            foreach (Teleport t in made.GetComponentsInChildren<Teleport>(true)) UnityEngine.Object.Destroy(t.gameObject); // heavy: once a rig (its own ways: ours are the entry and the exit)
            Location part = made.GetComponent<Location>();
            if (part != null && part.m_interiorTransform != null) UnityEngine.Object.Destroy(part.m_interiorTransform.gameObject);
            if (part != null) part.m_noBuild = false; // (the place keeps its own ring, «nobuild:»: the game's is measured by the map alone and would forbid the author his own inside over it)
            foreach (EnvZone e in made.GetComponentsInChildren<EnvZone>(true)) UnityEngine.Object.Destroy(e.gameObject); // heavy: once a rig (the air its Location part made for an inside of the game's: ours brings its own)
            made.transform.SetParent(root.transform, true);
            made.SetActive(true);
            l.Log("shell: «" + l.Shell + "» stands over the door (its own way in was at " + at.x.ToString("0.0") + " " + at.y.ToString("0.0") + " " + at.z.ToString("0.0") + ", looking " + Mathf.RoundToInt(yaw) + ")");
        }

        // the twelve edges of a box as lines of light (the author's only: seen in the dark, through nothing)
        private static Material _edge; // session: one material for every line
        private static void Edges(GameObject root, string name, Vector3 at, Quaternion rot, Vector3 size, Color c)
        {
            if (_edge == null) { Shader sh = Shader.Find("Sprites/Default"); if (sh == null) { Story.Log("rooms: no shader for the edges of a space: not drawn"); return; } _edge = new Material(sh); }
            GameObject g = Child(root, name, at, rot);
            Vector3 h = size * 0.5f;
            for (int axis = 0; axis < 3; axis++)
                for (int k = 0; k < 4; k++)
                {
                    Vector3 a = new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, 0f), b;
                    if (axis == 0) { a = new Vector3(-h.x, (k & 1) == 0 ? -h.y : h.y, (k & 2) == 0 ? -h.z : h.z); b = a; b.x = h.x; }
                    else if (axis == 1) { a = new Vector3((k & 1) == 0 ? -h.x : h.x, -h.y, (k & 2) == 0 ? -h.z : h.z); b = a; b.y = h.y; }
                    else { a.z = -h.z; b = a; b.z = h.z; }
                    GameObject e = new GameObject("edge");
                    e.transform.SetParent(g.transform, false);
                    LineRenderer lr = e.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false; lr.sharedMaterial = _edge; lr.positionCount = 2;
                    lr.SetPosition(0, a); lr.SetPosition(1, b);
                    lr.startWidth = lr.endWidth = 0.12f; lr.startColor = lr.endColor = c;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                }
        }

        private static void Drop(string name)
        {
            Rig r;
            if (!_rigs.TryGetValue(name, out r)) return;
            _rigs.Remove(name);
            if (r.Root != null) UnityEngine.Object.Destroy(r.Root);
            // (a box taken away with the player in it never says «left»: the environment it forced is let go — only if it is still the one forced)
            if (EnvMan.instance != null && Player.m_localPlayer != null && Player.m_localPlayer.InInterior() && EnvMan.instance.GetCurrentEnvironment() != null && EnvMan.instance.GetCurrentEnvironment().m_name == r.Air)
                EnvMan.instance.SetForceEnvironment("");
        }
        private static void DropAll() { foreach (string name in new List<string>(_rigs.Keys)) Drop(name); }

        // a point is in an inside of ours, by its rooms' box (the author's pieces there neither wear nor fall while he builds: BlueprintWearPatch; the story's «calm» holds there as on the land: StoryTrackConds.WhyNotCalm)
        internal static bool In(Vector3 point) { Stand s; return Character.InInterior(point) && Around(point, out s); }

        // the place whose inside a point is in (null: none — out in the world, or in a dungeon of the game's own)
        internal static Loc PlaceOf(Vector3 point)
        {
            if (!Character.InInterior(point)) return null;
            Stand s;
            foreach (Loc l in Locs.All) if (l.Inside.Length > 0 && Where(l, out s) && Holds(s, point)) return l;
            return null;
        }

        // the inside a point is in (its rooms' box)
        private static bool Around(Vector3 point, out Stand s)
        {
            foreach (KeyValuePair<string, Stand> t in _tried) { s = t.Value; s.L = Get(t.Key) ?? s.L; if (Holds(s, point)) return true; }
            foreach (Loc l in Locs.All) if (l.Inside.Length > 0 && Where(l, out s) && Holds(s, point)) return true;
            s = new Stand();
            return false;
        }
        private static bool Holds(Stand s, Vector3 point)
        {
            Vector3 d = Quaternion.Inverse(s.Rot) * (point - s.At) - s.L.Middle, half = (s.L.Hi - s.L.Lo) * 0.5f + new Vector3(Edge, Edge, Edge);
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && Mathf.Abs(d.z) <= half.z;
        }

        // ---------------------------------------------------------------- the author's walk
        private static Vector3 _from; private static bool _fromSet; // session: where the author stood before «try»

        internal static string Cmd(Player p, string[] a)
        {
            string what = a[2].ToLowerInvariant();
            if (what == "try") return Try(p, a.Length > 3 ? a[3] : "");
            if (what == "out")
            {
                if (!_fromSet) return "rooms: no place to go back to (you did not go in by «try» in this session)";
                return Story.Tp(p, _from) ? "rooms: back to where you stood" : "rooms: the game refused to carry you: say it again";
            }
            if (what == "keep") return Keep(p, a.Length > 3 && a[3].ToLowerInvariant() == "force");
            if (what == "here") return Here(p, a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "");
            if (what == "clear") return Clear(p, a.Length > 3 && a[3].ToLowerInvariant() == "world");
            if (what == "spawners") return Spawners(p);
            return "raven story rooms try <name> · out · here [words] · keep [force] · clear [world] · spawners";
        }

        // the author's look at the game's spawners near him («raven story rooms spawners»: the user 2026-11-15, a crypt with spawners and no foes): for each,
        // which of the game's own rules holds it now (CreatureSpawner.UpdateSpawner, SpawnArea.UpdateSpawn — read 2026-11-15) — another game runs it, a
        // player's base round it, a place of the game forbids its group, too far, a noise it waits for, day or night, its creature alive; in the log, a line each
        internal static float SpawnersNear = 80f;
        private static string Spawners(Player p)
        {
            Vector3 me = p.transform.position;
            int n = 0, ready = 0;
            foreach (CreatureSpawner c in UnityEngine.Object.FindObjectsByType<CreatureSpawner>(FindObjectsSortMode.None)) // heavy: once a command, the author's
            {
                float d = Vector3.Distance(me, c.transform.position);
                if (d > SpawnersNear) continue;
                n++;
                List<string> why = new List<string>();
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) why.Add("no ZDO");
                else
                {
                    if (!nv.IsOwner()) why.Add("another game runs it");
                    ZDOID sp = nv.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned);
                    if (!sp.IsNone() && ZDOMan.instance.GetZDO(sp) != null) why.Add("its creature is alive");
                }
                if (!c.m_spawnInPlayerBase && EffectArea.IsPointInsideArea(c.transform.position, EffectArea.Type.PlayerBase)) why.Add("a player's base round it");
                Location loc = Location.GetLocation(c.transform.position);
                if (loc != null && loc.m_blockSpawnGroups.Contains(c.m_spawnGroupID)) why.Add("the place " + loc.name + " forbids its group");
                if (d > c.m_triggerDistance) why.Add("too far (" + Mathf.RoundToInt(d) + " of " + Mathf.RoundToInt(c.m_triggerDistance) + " m)");
                if (c.m_triggerNoise > 0f) why.Add("waits for a noise " + c.m_triggerNoise.ToString("0.#", CultureInfo.InvariantCulture));
                if ((!c.m_spawnAtDay && EnvMan.IsDay()) || (!c.m_spawnAtNight && EnvMan.IsNight())) why.Add("not at this time of day");
                if (SpawnSystem.m_nospawn) why.Add("spawning is off (nospawn)");
                if (StoryHush.NoBirth(c.transform.position, true)) why.Add("the story's quiet round (StoryHush) round a scene's place");
                // the game's own last words, asked of it as they are (its private checks): a world key it waits for or one that stops it, its group full, its time
                if (Ask(c, "CheckGlobalKeys")) why.Add("a world key (needs «" + c.m_requiredGlobalKey + "», stopped by «" + c.m_blockingGlobalKey + "»)");
                if (Ask(c, "CheckGroupSpawnBlocked")) why.Add("its group has spawned enough");
                if (nv != null && nv.IsValid() && !Ask(c, "CanRespawnNow", nv.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned))) why.Add("its respawn time not come (" + c.m_respawnTimeMinuts + " min)");
                if (nv != null && nv.IsValid() && c.m_respawnTimeMinuts <= 0f && nv.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned) != ZDOID.None) why.Add("spawns once, and has");
                if (c.m_creaturePrefab == null) why.Add("no creature set on it");
                if (why.Count == 0) ready++;
                Story.Log("rooms: spawner " + Utils.GetPrefabName(c.gameObject) + " " + Mathf.RoundToInt(d) + " m: " + (why.Count == 0 ? "ready" : string.Join(", ", why.ToArray())));
            }
            foreach (SpawnArea a in UnityEngine.Object.FindObjectsByType<SpawnArea>(FindObjectsSortMode.None)) // heavy: once a command, the author's
            {
                float d = Vector3.Distance(me, a.transform.position);
                if (d > SpawnersNear) continue;
                n++;
                List<string> why = new List<string>();
                ZNetView nv = a.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) why.Add("another game runs it");
                if (ZNetScene.instance.OutsideActiveArea(a.transform.position)) why.Add("outside the active area");
                if (d > a.m_triggerDistance) why.Add("too far");
                if (SpawnSystem.m_nospawn) why.Add("spawning is off (nospawn)");
                if (StoryHush.NoBirth(a.transform.position, true)) why.Add("the story's quiet round (StoryHush) round a scene's place");
                object[] counts = { 0, 0 }; // (its own count of its creatures: near it, and all round)
                System.Reflection.MethodInfo gi = HarmonyLib.AccessTools.Method(typeof(SpawnArea), "GetInstances");
                try { if (gi != null) { gi.Invoke(a, counts); if ((int)counts[0] >= a.m_maxNear || (int)counts[1] >= a.m_maxTotal) why.Add("full: " + counts[0] + " near (of " + a.m_maxNear + "), " + counts[1] + " round (of " + a.m_maxTotal + ")"); } } catch (Exception e) { Story.Log("rooms: spawn area count failed: " + e.Message); }
                if (a.m_prefabs == null || a.m_prefabs.Count == 0) why.Add("nothing to spawn set on it");
                if (why.Count == 0) ready++;
                Story.Log("rooms: spawn area " + Utils.GetPrefabName(a.gameObject) + " " + Mathf.RoundToInt(d) + " m: " + (why.Count == 0 ? "ready (one every " + Mathf.RoundToInt(a.m_spawnIntervalSec) + " s)" : string.Join(", ", why.ToArray())));
            }
            return "rooms: " + n + " spawner(s) within " + Mathf.RoundToInt(SpawnersNear) + " m, " + ready + " ready by the game's rules; each one's reason in the log («rooms: spawner …»)";
        }

        // a private check of the game's spawner, as the game asks it (the author's look only)
        private static bool Ask(CreatureSpawner c, string method, params object[] args)
        {
            System.Reflection.MethodInfo m = HarmonyLib.AccessTools.Method(typeof(CreatureSpawner), method);
            try { return m != null && (bool)m.Invoke(c, args); } catch (Exception e) { Story.Log("rooms: spawner check " + method + " failed: " + e.Message); return false; }
        }

        private static string Try(Player p, string name)
        {
            if (_all == null) Read();
            Layout l = Get(name);
            if (l == null) return "raven story rooms try <name>   (the layouts of assets/story/rooms: " + (_all.Count > 0 ? string.Join(", ", new List<string>(_all.Keys).ToArray()) : "none") + ")";
            if (!ReferenceEquals(_world, ZNet.instance)) { DropAll(); _tried.Clear(); _fromSet = false; _world = ZNet.instance; }
            Stand s; int things = 0; string how;
            DungeonGenerator there;
            if (!_tried.ContainsKey(l.Name) && Standing(l.Name, out s)) how = "is the inside of «" + s.Of.Id + "» in this world"; // (the place's own: no second one)
            else if ((there = Loaded(l.Name)) != null)
            {
                ZDO z = there.GetComponent<ZNetView>().GetZDO();
                if (z.GetString(MarkAs, "") != l.Version) return "rooms: «" + l.Name + "» stands here as it was put before its files changed: raven story rooms clear, then try again";
                s = new Stand { L = l, At = z.GetVec3(MarkAt, there.transform.position), Rot = there.transform.rotation };
                _tried[l.Name] = s;
                how = "stands already";
            }
            else
            {
                if (p.InInterior()) return "rooms: you are inside already: «raven story rooms out» first";
                s = new Stand { L = l, At = p.transform.position + Vector3.up * Lift, Rot = Quaternion.Euler(0f, p.transform.eulerAngles.y, 0f) };
                string why = Put(l, s.At, s.Rot.eulerAngles.y, out things);
                if (why != null) return "rooms: " + why;
                _tried[l.Name] = s;
                how = "put: " + l.Rooms.Count + " rooms, " + things + " things";
            }
            if (!p.InInterior()) { _from = p.transform.position; _fromSet = true; }
            _next = 0f; // (its doors and its air at once)
            bool went = Story.Tp(p, s.P(new Vector3(0f, 0f, InZ)), s.Rot); // (the game holds the one it carries till there is a floor under: the rooms load meanwhile)
            return "rooms: «" + l.Name + "» " + how + ", print " + l.Print
                + (went ? "; you go to its way in" : "; the game refused to carry you (a portal still going?): say it again") + "   (back: raven story rooms out, or E on its way out)";
        }

        // where the author stands, as a line of the layout's own file would say it: the room (the nearest middle) and his place and turn in its
        // measure — with his words, into the log too («here a pile of bones»: what he wants there is then put by a «thing» line)
        private static string Here(Player p, string note)
        {
            Stand s;
            if (!Around(p.transform.position, out s)) return "rooms: you stand in no inside of ours (raven story rooms try <name> first)";
            Vector3 local = Quaternion.Inverse(s.Rot) * (p.transform.position - s.At);
            Part best = null;
            foreach (Part r in s.L.Rooms) if (!r.Id.StartsWith("cap", StringComparison.Ordinal) && (best == null || (r.At - local).sqrMagnitude < (best.At - local).sqrMagnitude)) best = r;
            if (best == null) return "rooms: no room here";
            Vector3 inRoom = Quaternion.Euler(0f, -best.Turn, 0f) * (local - best.At);
            float turn = Mathf.Repeat(p.transform.eulerAngles.y - s.Rot.eulerAngles.y - best.Turn, 360f);
            string line = best.Id + " " + F(Mathf.Round(inRoom.x * 10f) / 10f) + " " + F(Mathf.Round(inRoom.y * 10f) / 10f) + " " + F(Mathf.Round(inRoom.z * 10f) / 10f) + " turn " + Mathf.RoundToInt(turn);
            Story.Log("rooms: here «" + s.L.Name + "»: " + line + (note.Length > 0 ? "   — " + note : ""));
            return "rooms: " + line + (note.Length > 0 ? "   — " + note : "") + "   (your feet, in the room's measure; written into the log)";
        }

        // every piece the author built in the inside he stands in → <name>.own.txt, in the dungeon's measure; the pieces become the layout's own
        private static string Keep(Player p, bool force)
        {
            Stand s;
            if (!Around(p.transform.position, out s)) return "rooms: you stand in no inside of ours (raven story rooms try <name> first)";
            if (!StoryFiles.IsSource) return "rooms: keep writes into the mod's own assets/story/rooms, and config/RavenWatch/story/source.txt names no such folder";
            List<Piece> near = new List<Piece>();
            Piece.GetAllPiecesInRadius(s.P(s.L.Middle), s.L.Reach, near); // heavy: once a command, the author's
            Quaternion inv = Quaternion.Inverse(s.Rot);
            List<Blueprint.Part> parts = new List<Blueprint.Part>();
            List<ZNetView> views = new List<ZNetView>();
            foreach (Piece pc in near)
            {
                ZNetView nv = pc != null ? pc.GetComponent<ZNetView>() : null;
                if (nv == null || !nv.IsValid() || !Holds(s, pc.transform.position)) continue;
                if (nv.GetZDO().GetString(Mark, "").EndsWith(OfThing, StringComparison.Ordinal)) continue; // (a thing of the layout's own lines: the layout puts it)
                Blueprint.Part part = new Blueprint.Part { Prefab = Utils.GetPrefabName(pc.gameObject), Pos = inv * (pc.transform.position - s.At), Rot = inv * pc.transform.rotation };
                Blueprint.Hung(pc, ref part);
                parts.Add(part); views.Add(nv);
            }
            // far fewer than the last time: not loaded, or taken down by mistake — asked first (as a blueprint's save)
            if (!force && parts.Count < s.L.Own.Count * 0.8f)
                return "rooms: NOT KEPT: " + parts.Count + " pieces here, and " + s.L.Own.Count + " in the last keep. If that is meant: raven story rooms keep force";
            StringBuilder sb = new StringBuilder();
            sb.Append("# ").Append(s.L.Name).Append(OwnFile).Append(" — the author's own pieces inside «").Append(s.L.Name).Append("», in the dungeon's measure: written by «raven story rooms keep», read by LocInside.\n");
            sb.Append("pieces ").Append(parts.Count).Append('\n');
            foreach (Blueprint.Part pt in parts)
                sb.Append(pt.Prefab).Append(' ').Append(F(pt.Pos.x)).Append(' ').Append(F(pt.Pos.y)).Append(' ').Append(F(pt.Pos.z)).Append(' ')
                  .Append(Q(pt.Rot.x)).Append(' ').Append(Q(pt.Rot.y)).Append(' ').Append(Q(pt.Rot.z)).Append(' ').Append(Q(pt.Rot.w))
                  .Append(string.IsNullOrEmpty(pt.Item) ? "" : " item:" + pt.Item + ":" + pt.Variant + ":" + pt.Quality + ":" + pt.Turn).Append('\n');
            string file = Path.Combine(Path.Combine(StoryFiles.Source, Kind), s.L.Name + OwnFile + ".txt");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            foreach (ZNetView nv in views) // (now they are what Put would have made: the layout's own, kept whole)
            {
                nv.ClaimOwnership();
                nv.GetZDO().Set(Mark, s.L.Name + OfOwn);
                nv.GetZDO().Set(Blueprint.Mark, 1);
                BlueprintWearPatch.Forget(nv.gameObject);
                LocState.DeadStations(nv); // (as PieceIn does for one that comes in marked)
                Cold(nv);
                NoBase(nv);
            }
            Reload();
            Layout now = Get(s.L.Name);
            if (now != null) // (the one standing here is the files as they are now: «try» finds it again)
            {
                foreach (DungeonGenerator g in UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None)) // heavy: once a command, the author's (every generator of the layout loaded now: one a zone)
                {
                    ZNetView gv = g.GetComponent<ZNetView>();
                    if (gv == null || !gv.IsValid() || !string.Equals(gv.GetZDO().GetString(Mark, ""), s.L.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    gv.ClaimOwnership(); gv.GetZDO().Set(MarkAs, now.Version);
                }
            }
            return "rooms: " + parts.Count + " pieces of yours kept for «" + s.L.Name + "» → " + file;
        }

        // the tried layouts loaded now taken out of the world; «world»: the places' own too, and their keys (the server puts them anew)
        private static string Clear(Player p, bool world)
        {
            if (p.InInterior()) return "rooms: you are inside: «raven story rooms out» first (the floor would go from under you)";
            int gone = 0, left = 0;
            // the server holds every object of the world: a place's inside goes whole, loaded or not — cleared from afar it stood twice, the old one
            // where nobody had it loaded and the new one over it (the user 2026-11-15). With it the chests the stash put itself there (StoryFound)
            bool all = world && ZNet.instance != null && ZNet.instance.IsServer();
            HashSet<ZDOID> done = new HashSet<ZDOID>();
            if (all) foreach (Loc l in Locs.All) gone += TakeWhole(l, done);
            foreach (ZNetView v in UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)) // heavy: once a command, the author's (the tries; what a client has loaded)
            {
                ZDO z = v != null ? v.GetZDO() : null;
                if (z != null && done.Contains(z.m_uid)) continue; // (taken whole above)
                string mark = z != null ? z.GetString(Mark, "") : "";
                if (mark.Length == 0) continue;
                if (!world && OfPlace(mark, z.GetPosition())) { left++; continue; }
                v.ClaimOwnership();
                ZNetScene.instance.Destroy(v.gameObject);
                gone++;
            }
            _tried.Clear();
            DropAll();
            int keys = 0;
            if (world && ZoneSystem.instance != null)
                foreach (Loc l in Locs.All)
                {
                    string has;
                    if (l.Inside.Length == 0 || !ZoneSystem.instance.GetGlobalKey(l.KeyInside, out has)) continue;
                    ZoneSystem.instance.RemoveGlobalKey(l.KeyInside);
                    keys++;
                }
            return "rooms: " + gone + " thing(s) of " + (world ? "every layout" : "tried layouts") + " taken out of the world" + (all ? " (the places' insides whole, loaded or not)" : " (those loaded now; one elsewhere stays till cleared there)")
                + (left > 0 ? "; " + left + " of the places' own insides left (clear world takes them too)" : "") + (keys > 0 ? "; " + keys + " place(s) get theirs anew from the server" : "");
        }

        // a place's inside taken out of the world whole, loaded or not (the server holds every object), with the chests the stash put there itself
        // (StoryFound): «clear world», and a place moved or built again (LocBuild.Rebuild — a move once left the old crypt's rooms and spawners
        // hanging over the old spot, the user 2026-11-15). The server only; done: what went (the ones loaded here are skipped after). The count
        internal static int TakeWhole(Loc l, HashSet<ZDOID> done)
        {
            Stand s;
            if (ZNet.instance == null || !ZNet.instance.IsServer() || l.Inside.Length == 0 || !Where(l, out s)) return 0;
            int chest = StoryFound.ChestPrefab.GetStableHashCode(), gone = 0;
            List<ZDO> zs = new List<ZDO>();
            BlueprintGround.ObjectsAround(s.P(s.L.Middle), s.L.Reach, zs);
            foreach (ZDO z in zs)
            {
                if (z.GetString(Mark, "").Length == 0 && !(z.GetPrefab() == chest && Holds(s, z.GetPosition()))) continue;
                if (done != null) done.Add(z.m_uid);
                z.SetOwner(ZDOMan.GetSessionID());
                ZDOMan.instance.DestroyZDO(z); // (a body loaded here goes with it: the game's own OnZDODestroyed)
                gone++;
            }
            return gone;
        }

        // a marked thing is of a place's own inside (not of a try): its layout stands by a place's key, and the thing is within its reach
        private static bool OfPlace(string mark, Vector3 at)
        {
            int bar = mark.IndexOf('|');
            string name = bar < 0 ? mark : mark.Substring(0, bar);
            foreach (Loc l in Locs.All)
            {
                Stand s;
                if (string.Equals(l.Inside, name, StringComparison.OrdinalIgnoreCase) && Where(l, out s) && Vector3.Distance(at, s.P(s.L.Middle)) <= s.L.Reach + Edge) return true;
            }
            return false;
        }

        // a layout of that name loaded now (its generator)
        private static DungeonGenerator Loaded(string name)
        {
            foreach (DungeonGenerator g in UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None)) // heavy: once a command, the author's
            {
                ZNetView v = g.GetComponent<ZNetView>();
                ZDO z = v != null ? v.GetZDO() : null;
                if (z != null && string.Equals(z.GetString(Mark, ""), name, StringComparison.OrdinalIgnoreCase)) return g;
            }
            return null;
        }
    }
}
#endif

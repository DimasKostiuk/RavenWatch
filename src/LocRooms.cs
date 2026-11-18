#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The author's look at a dungeon of the game (the crypt of chapter 1 is to be a place of ours built on the game's crypt: story-lore.md,
    // «Склеп Чорного лісу»). Nothing here changes the world; it sleeps without the author's folder (Blueprint.On).
    //   raven story rooms                  every dungeon loaded now (stand at its door or inside: the game loads the inside with its zone),
    //                                      nearest first: how it stands, its rooms, what joins them, the things in each
    //   raven story rooms kinds            the game's room kinds, a count for each theme
    //   raven story rooms kinds <theme>    every room of that theme as the game made it: size, doors, the things it may hold and their chance
    //                                      (the chance of RandomSpawn only: a RandomObject, one of several by weight, is not reckoned)
    //   raven story rooms kinds all        the same for every theme that has rooms, a file each (the sunken crypts, the caves, the mines…)
    // Each writes config/RavenWatch/blueprints/rooms/<name>.txt and answers with a line and the file; the kinds go into the mod's own
    // tools/rooms as well when the author's source folder is known (StoryFiles.Source): what tools/rooms.js builds from.
    //   raven story rooms try <name> · out · keep · clear   a layout of ours put over the author, walked through, built in: LocInside (these do change the world)
    //
    // How the game builds one (DungeonGenerator, read 2026-11-12), which is what the file's head lines are for:
    //   the seed     the world's seed + the zone + the generator's place — or DungeonGenerator.m_forceSeed, a static taken once by the next
    //                dungeon generated. Only the layout is kept (the ZDO's «roomData»: each room's hash, place, turn), the seed is not: «seed»
    //                here is what the game would reckon for this spot of this world, right for a dungeon the game generated itself.
    //   the box      a room is placed only if all of it lies in a box of 64 m round the ZONE's centre, set straight to the world, not to the
    //                dungeon. So one seed gives one layout only where the dungeon stands at the same offset from its zone's centre with the
    //                same turn: «stands» says both.
    //   the things   what a room holds by chance (RandomSpawn) is drawn from the room's place — in the world, or in the dungeon when
    //                «things drawn» says so — so the same layout elsewhere may hold other things.
    //   a loaded dungeon is not generated again: its rooms are put where the ZDO says. A layout written there is the layout, whatever the box.
    // Places are in the dungeon's own measure (x right, y up, z forward of the generator; the turn is from its own), so two worlds compare;
    // «print» is one number of the whole layout: the same print, the same rooms in the same places.
    internal static class LocRooms
    {
        private const float InRoom = 0.5f;   // m: a thing this far outside a room's box is still its own (a door stands in the wall)
        private const float Joined = 0.1f;   // m: two doors of rooms this near are one way through (RoomConnection.TestContact)
        private const float BoxOf = 64f;     // m: the box of a dungeon (DungeonGenerator.m_zoneSize as the game ships it; read off each one)

        private static string Dir { get { return Path.Combine(Blueprint.Root, "rooms"); } }
        private static string F(float v) { float r = Mathf.Round(v * 10f) / 10f; if (r == 0f) r = 0f; return r.ToString("0.#", CultureInfo.InvariantCulture); } // (no «-0»)
        private static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }
        private static string Deg(float yaw) { int d = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)); return (d == 360 ? 0 : d).ToString(CultureInfo.InvariantCulture); }

        internal static string Cmd(Player p, string[] a)
        {
            if (!Blueprint.On) return "rooms: the author's folder is not there (" + Blueprint.Root + ")";
            string what = a.Length > 2 ? a[2].ToLowerInvariant() : "";
            try
            {
                if (what == "kinds") return Kinds(a.Length > 3 ? a[3] : "");
                if (what == "try" || what == "out" || what == "here" || what == "keep" || what == "clear" || what == "spawners") return LocInside.Cmd(p, a); // (a layout of ours walked through: LocInside)
                if (what.Length > 0) return "raven story rooms · raven story rooms kinds [theme|all] · raven story rooms try <name> · out · here [words] · keep [force] · clear [world]";
                return Loaded(p);
            }
            catch (Exception e) { Story.Log("rooms: " + e); return "rooms: " + e.Message + " (the whole of it is in the log)"; }
        }

        private static string Save(string name, StringBuilder sb)
        {
            Directory.CreateDirectory(Dir);
            StringBuilder n = new StringBuilder();
            foreach (char c in name) n.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            string file = Path.Combine(Dir, n + ".txt");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            // the kinds are what tools/rooms.js builds from: on the author's machine they go straight into its folder too (no copying by hand)
            string tool = StoryFiles.IsSource && name.StartsWith("kinds_") ? Path.Combine(Path.Combine(Path.Combine(Path.Combine(StoryFiles.Source, ".."), ".."), "tools"), "rooms") : null;
            if (tool != null && Directory.Exists(tool)) { file = Path.GetFullPath(Path.Combine(tool, n + ".txt")); File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false)); }
            return file;
        }

        private static string KindOf(Room r) { return r.m_entrance ? "entrance" : r.m_endCap ? "endcap" : r.m_divider ? "divider" : "room"; }

        // ---------------------------------------------------------------- the dungeons loaded now
        private sealed class Thing { public string Prefab, Is; public Vector3 At; }

        private static string Loaded(Player p)
        {
            DungeonGenerator[] gens = UnityEngine.Object.FindObjectsByType<DungeonGenerator>(FindObjectsSortMode.None); // heavy: once a command, the author's
            if (gens.Length == 0) return "rooms: no dungeon is loaded here (stand at a crypt's door or inside it)";
            Vector3 me = p.transform.position;
            Array.Sort(gens, (x, y) => Vector3.Distance(x.transform.position, me).CompareTo(Vector3.Distance(y.transform.position, me)));
            ZNetView[] views = UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None); // heavy: once a command, the author's (the rooms' own copies of their things are switched off and not among these)
            string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : "world";
            StringBuilder sb = new StringBuilder(), said = new StringBuilder();
            sb.Append("# raven story rooms — world «").Append(world).Append("», seed ").Append(WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed().ToString(CultureInfo.InvariantCulture) : "?").Append("; what each head line means: src/LocRooms.cs\n");
            foreach (DungeonGenerator g in gens)
            {
                string print; int rooms;
                One(g, me, views, sb, out print, out rooms);
                said.Append(said.Length > 0 ? "; " : "").Append(Utils.GetPrefabName(g.gameObject)).Append(' ').Append(rooms).Append(" rooms, print ").Append(print);
            }
            return "rooms: " + said + " → " + Save(world + "_loaded", sb);
        }

        private static void One(DungeonGenerator g, Vector3 me, ZNetView[] views, StringBuilder sb, out string print, out int count)
        {
            Transform gt = g.transform;
            Room[] rooms = g.GetComponentsInChildren<Room>(); // heavy: once a dungeon a command
            count = rooms.Length;
            var zone = ZoneSystem.GetZone(gt.position);
            Vector3 centre = ZoneSystem.GetZonePos(zone);
            Location loc = Location.GetZoneLocation(gt.position);
            sb.Append("\n[dungeon ").Append(Utils.GetPrefabName(g.gameObject)).Append("]\n");
            sb.Append("at: ").Append(V(gt.position)).Append(" in the world, ").Append(Mathf.RoundToInt(Vector3.Distance(gt.position, me))).Append(" m from you; location ").Append(loc != null ? Utils.GetPrefabName(loc.gameObject) : "none found").Append('\n');
            sb.Append("themes: ").Append(g.m_themes).Append("; built as ").Append(g.m_algorithm).Append('\n');
            // (DungeonGenerator.GetSeed's own sum, not the call: the call would take a waiting m_forceSeed away from the dungeon it was set for)
            Vector2i z = zone.ToVector2i();
            int seed = (WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0) + z.x * 4271 + z.y * -7187 + (int)gt.position.x * -4271 + (int)gt.position.y * 9187 + (int)gt.position.z * -2134;
            sb.Append("seed: ").Append(seed.ToString(CultureInfo.InvariantCulture)).Append("   (reckoned as the game does for this spot; a forced seed is not kept anywhere)\n");
            sb.Append("asked: rooms ").Append(g.m_minRooms).Append(" … ").Append(g.m_maxRooms).Append("; needed ").Append(g.m_minRequiredRooms).Append(" of [").Append(string.Join(", ", g.m_requiredRooms.ToArray())).Append("]; a door at a way through: chance ").Append(F(g.m_doorChance)).Append('\n');
            sb.Append("stands: turned ").Append(Deg(gt.eulerAngles.y)).Append("°, ").Append(F(gt.position.x - centre.x)).Append(" east and ").Append(F(gt.position.z - centre.z)).Append(" north of its zone's centre; the box ").Append(V(g.m_zoneSize)).Append('\n');
            sb.Append("things drawn: by the room's place in ").Append(g.m_useCustomInteriorTransform ? "the dungeon" : "the world").Append(g.m_addBaseSeedToRandomSpawn ? ", and by the dungeon's seed" : "").Append('\n');

            // every door of every room, to say what it leads to
            List<RoomConnection> doors = new List<RoomConnection>(); List<int> doorOf = new List<int>();
            for (int i = 0; i < rooms.Length; i++) foreach (RoomConnection c in rooms[i].GetConnections()) { doors.Add(c); doorOf.Add(i); }

            // the things: each in the first room whose box holds it; those in the dungeon's box and in no room are said after the rooms
            List<Thing>[] inRoom = new List<Thing>[rooms.Length];
            Dictionary<string, int> loose = new Dictionary<string, int>();
            float half = Mathf.Max(BoxOf, Mathf.Max(g.m_zoneSize.x, g.m_zoneSize.z)) * 0.5f + 8f;
            foreach (ZNetView v in views)
            {
                if (v == null || v.GetZDO() == null || v.gameObject == g.gameObject) continue;
                Vector3 at = v.transform.position;
                if (Mathf.Abs(at.x - centre.x) > half || Mathf.Abs(at.z - centre.z) > half || Mathf.Abs(at.y - gt.position.y) > half) continue;
                if (v.GetComponent<Character>() != null) continue; // (who walks there now is not the dungeon's make)
                int home = -1;
                for (int i = 0; i < rooms.Length && home < 0; i++)
                {
                    Vector3 l = rooms[i].transform.InverseTransformPoint(at), s = rooms[i].m_size;
                    if (Mathf.Abs(l.x) <= s.x * 0.5f + InRoom && Mathf.Abs(l.y) <= s.y * 0.5f + InRoom && Mathf.Abs(l.z) <= s.z * 0.5f + InRoom) home = i;
                }
                string prefab = Utils.GetPrefabName(v.gameObject);
                if (home < 0) { int n; loose.TryGetValue(prefab, out n); loose[prefab] = n + 1; continue; }
                if (inRoom[home] == null) inRoom[home] = new List<Thing>();
                inRoom[home].Add(new Thing { Prefab = prefab, Is = IsWhat(v), At = rooms[home].transform.InverseTransformPoint(at) });
            }

            List<string> lines = new List<string>();
            sb.Append("rooms: ").Append(rooms.Length).Append("   (place and turn in the dungeon's own measure; a thing's place in its room's)\n");
            for (int i = 0; i < rooms.Length; i++)
            {
                Room r = rooms[i];
                string name = Utils.GetPrefabName(r.gameObject), at = V(gt.InverseTransformPoint(r.transform.position)), turn = Deg(r.transform.eulerAngles.y - gt.eulerAngles.y);
                lines.Add(name + "|" + at + "|" + turn);
                sb.Append("  ").Append(i).Append(' ').Append(name).Append("  ").Append(KindOf(r)).Append("  at ").Append(at).Append("  turned ").Append(turn).Append("°  size ").Append(r.m_size.x).Append('x').Append(r.m_size.y).Append('x').Append(r.m_size.z).Append('\n');
                StringBuilder ways = new StringBuilder();
                for (int d = 0; d < doors.Count; d++)
                {
                    if (doorOf[d] != i) continue;
                    int to = -1;
                    for (int o = 0; o < doors.Count && to < 0; o++) if (doorOf[o] != i && Vector3.Distance(doors[d].transform.position, doors[o].transform.position) < Joined) to = doorOf[o];
                    ways.Append(ways.Length > 0 ? ", " : "").Append(doors[d].m_type.Length > 0 ? doors[d].m_type : "way").Append(doors[d].m_entrance ? " (the way in)" : "").Append(to >= 0 ? " → " + to : " → nothing").Append(" at ").Append(V(r.transform.InverseTransformPoint(doors[d].transform.position)));
                }
                if (ways.Length > 0) sb.Append("      ways: ").Append(ways).Append('\n');
                int made = r.GetComponentsInChildren<ZNetView>(true).Length; // heavy: once a room a command (its own switched-off copies: what the room may hold at most)
                List<Thing> th = inRoom[i];
                sb.Append("      things: ").Append(th != null ? th.Count : 0).Append(" there, ").Append(made).Append(" made for it\n");
                if (th == null) continue;
                th.Sort((x, y) => string.CompareOrdinal(x.Prefab, y.Prefab));
                foreach (Thing t in th) sb.Append("        ").Append(t.Prefab).Append(t.Is.Length > 0 ? " (" + t.Is + ")" : "").Append(" at ").Append(V(t.At)).Append('\n');
            }
            if (loose.Count > 0)
            {
                List<string> l = new List<string>();
                foreach (KeyValuePair<string, int> kv in loose) l.Add(kv.Key + (kv.Value > 1 ? " ×" + kv.Value : ""));
                l.Sort(string.CompareOrdinal);
                sb.Append("in no room (within the box): ").Append(string.Join(", ", l.ToArray())).Append('\n');
            }
            lines.Sort(string.CompareOrdinal);
            print = ((uint)string.Join("\n", lines.ToArray()).GetStableHashCode()).ToString("x8", CultureInfo.InvariantCulture);
            sb.Append("print: ").Append(print).Append("   (of every room's name, place and turn: the same print in two worlds, the same layout)\n");
        }

        // what a thing of the dungeon is, where the game says so by its part
        private static string IsWhat(ZNetView v)
        {
            if (v.GetComponent<Container>() != null) return "chest";
            CreatureSpawner cs = v.GetComponent<CreatureSpawner>();
            if (cs != null) return "spawns " + (cs.m_creaturePrefab != null ? cs.m_creaturePrefab.name : "?") + (cs.m_respawnTimeMinuts > 0f ? ", again after " + F(cs.m_respawnTimeMinuts) + " min" : ", once");
            SpawnArea sa = v.GetComponent<SpawnArea>();
            if (sa != null)
            {
                List<string> who = new List<string>();
                foreach (SpawnArea.SpawnData d in sa.m_prefabs) if (d != null && d.m_prefab != null) who.Add(d.m_prefab.name);
                return "spawner of " + string.Join(", ", who.ToArray());
            }
            if (v.GetComponent<Door>() != null) return "door";
            if (v.GetComponent<Pickable>() != null || v.GetComponent<PickableItem>() != null) return "to pick";
            if (v.GetComponent<ItemDrop>() != null) return "item";
            if (v.GetComponent<Fireplace>() != null) return "fire";
            return "";
        }

        // ---------------------------------------------------------------- the game's room kinds
        private static string Kinds(string theme)
        {
            if (DungeonDB.instance == null) return "rooms: no DungeonDB (not in a world?)";
            List<DungeonDB.RoomData> all = DungeonDB.GetRooms();
            if (theme.Length == 0)
            {
                StringBuilder s = new StringBuilder("rooms: " + all.Count + " kinds —");
                foreach (Room.Theme t in Enum.GetValues(typeof(Room.Theme)))
                {
                    if (t == Room.Theme.None) continue;
                    int n = 0;
                    foreach (DungeonDB.RoomData rd in all) if ((rd.m_theme & t) != 0) n++;
                    if (n > 0) s.Append(' ').Append(t).Append(' ').Append(n).Append(',');
                }
                return s.ToString().TrimEnd(',') + "   (raven story rooms kinds <theme>)";
            }
            if (string.Equals(theme, "all", StringComparison.OrdinalIgnoreCase)) // (every theme that has rooms, a file each)
            {
                List<string> done = new List<string>();
                foreach (Room.Theme t in Enum.GetValues(typeof(Room.Theme)))
                {
                    if (t == Room.Theme.None || !all.Exists(rd => (rd.m_theme & t) != 0)) continue;
                    Kinds(t.ToString());
                    done.Add(t.ToString());
                }
                return "rooms: the kinds of " + done.Count + " themes written (" + string.Join(", ", done.ToArray()) + "): kinds_<theme>.txt";
            }
            Room.Theme want = Room.Theme.None;
            foreach (Room.Theme t in Enum.GetValues(typeof(Room.Theme))) if (string.Equals(t.ToString(), theme, StringComparison.OrdinalIgnoreCase)) want = t;
            if (want == Room.Theme.None) return "rooms: no theme «" + theme + "» (" + string.Join(", ", Enum.GetNames(typeof(Room.Theme))) + ")";
            StringBuilder sb = new StringBuilder("# raven story rooms kinds " + want + " — the rooms as the game made them; what each line means: src/LocRooms.cs\n");
            int count = 0;
            foreach (DungeonDB.RoomData rd in all)
            {
                if ((rd.m_theme & want) == 0) continue;
                count++;
                rd.m_prefab.Load(); // heavy: once a room kind a command (the game's own way to have the prefab at hand; given back below)
                try { Kind(rd, sb); }
                finally { rd.m_prefab.Release(); }
            }
            if (count == 0) return "rooms: the theme " + want + " has no rooms";
            return "rooms: " + count + " kinds of " + want + " → " + Save("kinds_" + want, sb);
        }

        private static void Kind(DungeonDB.RoomData rd, StringBuilder sb)
        {
            Room r = rd.RoomInPrefab;
            if (r == null) { sb.Append("\n[room ").Append(rd.m_prefab.Name).Append("]   not loaded\n"); return; }
            sb.Append("\n[room ").Append(rd.m_prefab.Name).Append("]  ").Append(KindOf(r)).Append(rd.m_enabled ? "" : "  OFF").Append("  size ").Append(r.m_size.x).Append('x').Append(r.m_size.y).Append('x').Append(r.m_size.z)
              .Append("  themes ").Append(rd.m_theme).Append("  weight ").Append(F(r.m_weight)).Append(r.m_minPlaceOrder > 0 ? "  not before the " + r.m_minPlaceOrder + "th from the way in" : "").Append(r.m_endCap ? "  endcap first " + r.m_endCapPrio : "").Append('\n');
            Transform rt = r.transform;
            foreach (RoomConnection c in r.GetComponentsInChildren<RoomConnection>(true)) // heavy: once a room kind a command
                sb.Append("  way: ").Append(c.m_type.Length > 0 ? c.m_type : "-").Append(c.m_entrance ? " (the way in)" : "").Append(c.m_allowDoor ? "" : " no door").Append(" at ").Append(V(rt.InverseTransformPoint(c.transform.position))).Append(" facing ").Append(Deg(c.transform.eulerAngles.y - rt.eulerAngles.y)).Append("°").Append(c.gameObject.activeSelf ? "" : " OFF").Append('\n');
            foreach (ZNetView v in r.GetComponentsInChildren<ZNetView>(true)) // heavy: once a room kind a command
            {
                // its chance: every RandomSpawn from it up to the room must fall its way
                float chance = 1f;
                for (Transform t = v.transform; t != null && t != rt; t = t.parent) { RandomSpawn rs = t.GetComponent<RandomSpawn>(); if (rs != null && rs.enabled) chance *= Mathf.Clamp01(rs.m_chanceToSpawn / 100f); }
                string what = IsWhat(v);
                sb.Append("  thing: ").Append(Utils.GetPrefabName(v.gameObject)).Append(what.Length > 0 ? " (" + what + ")" : "").Append(" at ").Append(V(rt.InverseTransformPoint(v.transform.position))).Append(" facing ").Append(Deg(v.transform.eulerAngles.y - rt.eulerAngles.y)).Append("°")
                  .Append(chance < 1f ? "  chance " + Mathf.RoundToInt(chance * 100f) + "%" : "").Append(v.gameObject.activeSelf ? "" : " OFF").Append('\n');
            }
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
// (the story's places in every world — the Hall of the Watch first, then any the author adds: from 2.9 — the user 2026-11-01)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // A place of the story: one in every world, a new one or one played for years. What it is — its blueprint
    // (assets/story/blueprints/<id>.txt, the author's build), its layers and names (LocParts) — and where it goes, its
    // states and their look, all from the list of places (Locs: locations.list.txt). How it comes into a world:
    //   1. the server picks the place once a world: from the world as generated (WorldGenerator — land not visited
    //      yet is known too), with the world's seed (the same world, the same place), a bounded number of tries spread
    //      over frames, the cheap asks first (the land, the height) and the dear ones (the world's places, the players'
    //      buildings in the server's own list) only for a place that passed them; land nobody has been to first. Round
    //      the start (as the Hall), or round another place of the story (the user 2026-11-01: Torvald's stone near the
    //      Hall) once that one has its place. The place goes into the world's global keys: «<key> x z yaw», and once built «… ground origin» (Where, Origin).
    //   2. the server builds it ahead of anyone: once a player comes within BuildFrom m (the user 2026-11-05: it rose before his eyes when
    //      it was built on arriving; many places to come, so only the ones someone is heading for), one place at a time, a few pieces a
    //      frame. Its zones are loaded for the while (the game's own PokeLocalZone); a player's building within Clear m or bad ground:
    //      refused, the server crosses the place out and picks another. Passed: the ground is levelled (BlueprintGround), the wild
    //      things on it go, the pieces go in, and «<key>_built» is set. A place someone teleports next to is built the same way.
    //      Its outer pieces and roofs are landmarks (ZNetView.m_distant, as the game's big trees): the game loads them from far (its
    //      far ring, about 290 m), so coming back it does not grow piece by piece at 160 m either.
    //   3. its pieces carry Blueprint's mark: no wear, no fall, no damage, no hammer but the author's (BlueprintGuard).
    // Here: its shape and where it stands. What it is: LocDef (the list's lines); how it gets into a world: LocBuild; the list: Locs; its parts: LocModules.
    internal sealed partial class Loc : LocDef
    {
        // what it is taken from a fresh read of the list (the place itself — its seeking, its builder — stays): all of LocDef, whatever is added to it
        private static readonly FieldInfo[] DefFields = typeof(LocDef).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal void Take(LocDef d) { foreach (FieldInfo f in DefFields) f.SetValue(this, f.GetValue(d)); }

        // ---------------------------------------------------------------- the blueprint built into the mod
        private bool _read;
        private List<Blueprint.Part> _parts;
        private readonly List<Blueprint.Spot> _marks = new List<Blueprint.Spot>();
        private float _ground = BlueprintGround.NoGround;
        private BlueprintGround.Rect _rect;

        internal bool Shape()
        {
            if (_read) return _parts != null;
            _read = true;
            using (Stream st = typeof(Loc).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + Id + ".txt"))
            {
                if (st == null) { Log("no blueprint in this build: it waits"); return false; }
                string[] lines;
                using (StreamReader r = new StreamReader(st, Encoding.UTF8)) lines = r.ReadToEnd().Replace("\r", "").Split('\n');
                List<Blueprint.Part> parts = Blueprint.Parse(lines, _marks);
                if (parts.Count == 0) { Log("its blueprint is empty"); return false; }
                _parts = parts;
                _ground = Blueprint.GroundIn(lines);
                _rect = BlueprintGround.Footprint(parts);
            }
            BlueprintGround.Rect room = Room;
            Log("its blueprint: " + _parts.Count + " pieces, " + (_rect.X1 - _rect.X0).ToString("0") + " × " + (_rect.Z1 - _rect.Z0).ToString("0") + " m, its ground " + (room.X1 - room.X0).ToString("0") + " × " + (room.Z1 - room.Z0).ToString("0") + " m, ground " + _ground.ToString("0.00"));
            return true;
        }

        // its own ground: the blueprint's walls (_rect) and its room round them (x across, z along: its front is +z, to the way people
        // come). Where it is sought, its neighbours kept off, its ground levelled and cleared — all by this; its walls by _rect
        // (inside it, its fog, its landmarks)
        // Tight: no place in this world with its room (the user 2026-11-06: a new world had none for the Hall's 59 × 35 m once its room
        // was added, and the story stopped) — sought again by its walls alone, and the world keeps it so (KeyTight): its build, its
        // ground and its neighbours then see its walls only
        private BlueprintGround.Rect Room
        {
            get
            {
                float across = Tight ? 0f : RoomAcross, along = Tight ? 0f : RoomAlong;
                return new BlueprintGround.Rect { X0 = _rect.X0 - across, X1 = _rect.X1 + across, Z0 = _rect.Z0 - along, Z1 = _rect.Z1 + along };
            }
        }
        private bool _seekTight; // session: the second search, by its walls alone, is going on
        private bool Tight { get { return _seekTight || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(KeyTight)); } }

        // ---------------------------------------------------------------- where it is (everyone: from the world's keys)
        // «x z yaw [ground origin]»: once built, the height of its levelled ground less Sink (where it stands: Where) and the height of
        // its blueprint's own origin as it was built (its frame: Origin), written by the server when it builds (LocBuild.BuildAhead; once
        // for a world built before — KeepHeight, from its own pieces). The origin is kept, not worked out from the blueprint's «ground»
        // and «sink»: a world keeps the building it got, and a later blueprint or sink must not move its frame. Without them — picked,
        // not built yet — the land as generated at its middle. The user 2026-11-06: the camera shots and named spots stood metres off
        // after a rebuild — the frame stood on the land's height, not the building's
        internal bool Where(out Vector3 at, out Quaternion rot)
        {
            at = Vector3.zero; rot = Quaternion.identity;
            string v;
            if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(KeySpot, out v)) return false;
            string[] w = v.Split(' ');
            float x, z, yaw, y;
            if (w.Length < 3 || !TryF(w[0], out x) || !TryF(w[1], out z) || !TryF(w[2], out yaw)) return false;
            if (w.Length < 4 || !TryF(w[3], out y)) y = WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(x, z) : 0f;
            at = new Vector3(x, y, z);
            rot = Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        // its blueprint's origin in this world (a piece stands at origin + rot * its place in the blueprint): the author's own building by its
        // frame; a built one by its key; one not built yet (or a key of the first day of this, 2026-11-06, with the ground only) worked out
        // by the builder's own rule (LocBuild.BuildAhead: origin = ground - blueprint ground - Sink)
        private bool Origin(out Vector3 at, out Quaternion rot)
        {
            if (ByFrame) return BlueprintFrame.Get(Id, out at, out rot);
            if (!Where(out at, out rot)) return false;
            string v; float o;
            ZoneSystem.instance.GetGlobalKey(KeySpot, out v);
            string[] w = v.Split(' ');
            if (w.Length >= 5 && TryF(w[4], out o)) at.y = o;
            else at.y -= _ground + (w.Length >= 4 ? 0f : Sink); // (a 4-word key holds the ground less Sink already)
            return true;
        }

        // its key holds where it was built: its ground and its origin
        internal bool HeightKnown { get { string v; return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(KeySpot, out v) && v.Split(' ').Length >= 5; } }

        // the server: its key with its ground and its origin (its first three words kept)
        private void SetHeight(float ground, float origin)
        {
            string v;
            if (!ZoneSystem.instance.GetGlobalKey(KeySpot, out v)) return;
            string[] w = v.Split(' ');
            if (w.Length < 3) return;
            ZoneSystem.instance.SetGlobalKey(KeySpot + " " + w[0] + " " + w[1] + " " + w[2] + " " + F(ground) + " " + F(origin));
        }

        // its size in its blueprint: the footprint's corners at the floor and one at its top (LocFog), null — no blueprint
        internal List<Vector3> Corners()
        {
            if (!Shape()) return null;
            float top = 0f;
            foreach (Blueprint.Part p in _parts) top = Mathf.Max(top, p.Pos.y);
            return new List<Vector3> { new Vector3(_rect.X0, 0f, _rect.Z0), new Vector3(_rect.X1, 0f, _rect.Z1), new Vector3(_rect.X0, top, _rect.Z0) };
        }

        // where its floors lie in its blueprint (LocFog: the low mist only over them)
        internal List<Vector3> Floors()
        {
            if (!Shape()) return null;
            List<Vector3> list = new List<Vector3>();
            foreach (Blueprint.Part p in _parts) if (LocFog.IsFloor(p.Prefab)) list.Add(p.Pos);
            return list;
        }

        // where its roof lies in its blueprint (LocFog: the high mist only over it)
        internal List<Vector3> Roofs()
        {
            if (!Shape()) return null;
            List<Vector3> list = new List<Vector3>();
            foreach (Blueprint.Part p in _parts) if (LocFog.IsRoof(p.Prefab)) list.Add(p.Pos);
            return list;
        }

        // how far a point is from its walls (0 inside); MaxValue while it has no place
        internal float From(Vector3 p)
        {
            Vector3 at; Quaternion rot;
            if (!Shape() || !Where(out at, out rot)) return float.MaxValue;
            return FromAt(p, at, rot);
        }

        // the same, were it standing at (at, rot)
        internal float FromAt(Vector3 p, Vector3 at, Quaternion rot)
        {
            if (!Shape()) return float.MaxValue;
            Vector3 d = p - at; d.y = 0f;
            return BlueprintGround.Outside(_rect, Quaternion.Inverse(rot) * d);
        }

        internal bool Built { get { return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(KeyBuilt); } }

        // the author's look (`raven blueprint hall`): where it is, how far, built or waiting
        internal string Tell(Player p)
        {
            Vector3 at; Quaternion rot;
            if (!Shape()) return Title + ": у цій збірці нема креслення «" + Id + "».";
            if (!Where(out at, out rot)) return Title + ": місця в цьому світі ще не обрано (сервер шукає; дивись лог).";
            Vector3 d = at - p.transform.position; d.y = 0f;
            return Title + ": " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + ", " + Mathf.RoundToInt(d.magnitude) + " м від тебе, "
                 + (Built ? "стоїть" : "чекає першого, хто підійде так близько, що вся ділянка завантажиться") + ".";
        }

        // ---------------------------------------------------------------- no building round it
        // The game asks Location.IsInsideNoBuildLocation for a piece placed, a piece taken down, a pickaxe or hoe on the
        // ground: a standing place answers «inside» within NoBuild m of its middle. Asked every frame while placing: the
        // place is read from the key only when the key changes
        private string _keyRead;
        private Vector3 _atKnown;
        private bool _known;

        internal bool InNoBuild(Vector3 point)
        {
            if (ZoneSystem.instance == null || NoBuild <= 0f || !Built) return false;
            string v;
            if (!ZoneSystem.instance.GetGlobalKey(KeySpot, out v)) return false;
            if (!ReferenceEquals(v, _keyRead)) { _keyRead = v; Quaternion r; _known = Where(out _atKnown, out r); }
            if (!_known) return false;
            float dx = point.x - _atKnown.x, dz = point.z - _atKnown.z;
            return dx * dx + dz * dz <= NoBuild * NoBuild;
        }

        // ---------------------------------------------------------------- where it stands: one rule for every caller
        // (the user 2026-11-02: the story's places, Heid, the marks, the scenes — never each its own way)
        //   1. the one the world built (its place in the world's keys)
        //   2. else, the author's world: his own building, by its frame (BlueprintFrame)
        //   3. else the spot the world chose for it, not built yet (nothing stands there)
        // ByFrame: it is the author's own building (2) — its frame is the building's own; a built one's height is in its key (Where, Origin)
        internal bool ByFrame { get { Vector3 c; Quaternion r; return !Built && Blueprint.On && BlueprintFrame.Get(Id, out c, out r); } }
        internal bool Stands { get { return Built || ByFrame; } }

        internal bool Anchor(out Vector3 at, out Quaternion rot)
        {
            if (!Built && Blueprint.On && BlueprintFrame.Get(Id, out at, out rot)) return true;
            return Where(out at, out rot);
        }

        // a point of its blueprint's own frame (a piece's place in it) in this world: where that piece stands (Origin)
        internal bool ToWorld(Vector3 local, out Vector3 world)
        {
            world = Vector3.zero;
            Vector3 at; Quaternion rot;
            if (!Shape() || !Origin(out at, out rot)) return false;
            world = at + rot * local;
            return true;
        }

        // the other way: a point of this world in its blueprint's own frame, and its turn (the author's camera shots: StoryShots)
        internal bool ToLocal(Vector3 world, out Vector3 local)
        {
            local = Vector3.zero;
            Vector3 at; Quaternion rot;
            if (!Shape() || !Origin(out at, out rot)) return false;
            local = Quaternion.Inverse(rot) * (world - at);
            return true;
        }

        internal bool Turn(out Quaternion rot) { Vector3 at; return Anchor(out at, out rot); }

        // a named piece of it (its names: LocParts — «vohnyshche», «dveri_holovni»), loaded or not: where it is (its object
        // when the server's list has it; else where its name says) and that object (null — not found)
        internal bool NamedAt(string name, out Vector3 world, out ZDO zdo)
        {
            zdo = null;
            string prefab;
            if (!NamedSpot(name, out world, out prefab)) return false;
            zdo = ZdoNear(prefab, world);
            if (zdo != null) world = zdo.GetPosition();
            return true;
        }

        // where a named piece of it stands by its name alone (no search: the story's marks ask often)
        internal bool NamedSpot(string name, out Vector3 world) { string prefab; return NamedSpot(name, out world, out prefab); }
        private bool NamedSpot(string name, out Vector3 world, out string prefab)
        {
            world = Vector3.zero;
            Vector3 local;
            return LocParts.NameSpot(Id, name, out prefab, out local) && ToWorld(local, out world);
        }

        // that prefab's object in the server's own list (loaded or not) within a step of a point
        private static ZDO ZdoNear(string prefab, Vector3 w)
        {
            if (FindObjects == null || ZDOMan.instance == null) return null;
            int hash = prefab.GetStableHashCode();
            List<ZDO> all = new List<ZDO>();
            foreach (Vector2s z in Zones(w, 2f))
            {
                all.Clear();
                FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() });
                foreach (ZDO d in all) if (d != null && d.GetPrefab() == hash && Utils.DistanceXZ(d.GetPosition(), w) < 1.5f) return d;
            }
            return null;
        }

        private static IEnumerable<Vector2s> Zones(Vector3 c, float radius)
        {
            Vector2s lo = ZoneSystem.GetZone(c - new Vector3(radius, 0f, radius)), hi = ZoneSystem.GetZone(c + new Vector3(radius, 0f, radius));
            for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                    yield return new Vector2s((short)x, (short)y);
        }

        // a point is in the author's own building of this place (the one its blueprint is saved from: BlueprintFrame, this world; only
        // where blueprints are on — the author's machine)
        internal bool InAuthorCopy(Vector3 point)
        {
            Vector3 at; Quaternion rot;
            return Blueprint.On && Shape() && BlueprintFrame.Get(Id, out at, out rot) && BlueprintGround.Outside(_rect, Quaternion.Inverse(rot) * (point - at)) <= 0f;
        }

        // what it keeps from people (lockchests, lockstations): a thing of its own nobody takes to (NpcBase)
        internal bool Keeps(System.Type t) { return (LockChests && t == typeof(Container)) || (LockStations && (t == typeof(CraftingStation) || t == typeof(Smelter) || t == typeof(CookingStation) || t == typeof(Fermenter) || t == typeof(Beehive))); }

        // where one waits to be let in: the middle of its yard before its front (its front is +z, the way people come)
        internal Vector3 BeforeFront(Vector3 at, Quaternion rot) { return at + rot * new Vector3(0f, 0f, _rect.Z1 + 3f); }

        // a point of the world is within this place's walls (its blueprint's footprint, turned as it stands here; not its room round them)
        internal bool Holds(Vector3 point)
        {
            Vector3 at; Quaternion rot;
            return Shape() && Anchor(out at, out rot) && BlueprintGround.Outside(_rect, Quaternion.Inverse(rot) * (point - at)) <= 0f;
        }

        // ---------------------------------------------------------------- everyone: its mark on the map
        private float _nextLook;

        internal void Tick()
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + 1f;
            ShowMapMark();
            CheckFrame();
        }

        // its frame against its own pieces, once a session as soon as some are loaded (the user 2026-11-06: the frame stood on the land's
        // height, not the building's, and every shot and named spot was metres off with nothing said): each piece carries where it stands
        // in the blueprint (LocParts.Place), so ToWorld of that must be where it is. Off — said in the log, with how far
        private string _frameFor; // session: the key (or frame) it was checked for — another world, a rebuild, a move: looked at again
        internal const float FrameOff = 0.05f;
        internal float FrameOffBy = -1f; // m its frame was found off its pieces (-1: not looked at for where it stands now) — the scene check's VERDICT (StudioScene)
        private void CheckFrame()
        {
            string now = "";
            if (ZNet.instance == null || ZNet.instance.IsDedicated() || ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(KeySpot, out now) || !Built) { _frameFor = null; FrameOffBy = -1f; return; } // (no world, none built: looked at again when it comes back. The author's own building has no stamped pieces. A dedicated server's look would search each time till one loads)
            if (now == _frameFor) return;
            FrameOffBy = -1f;
            if (!Shape() || !HeightKnown) return; // (an older world: the server writes its origin first — KeepHeight)
            List<ZNetView> pieces = LocParts.All(Id); // (the same look LocState makes each second: nothing new searched)
            if (pieces.Count == 0) return;
            _frameFor = now;
            float worst = 0f; int n = 0;
            foreach (ZNetView nv in pieces)
            {
                if (n >= 8) break;
                string of; Vector3 local, world;
                if (nv.GetComponent<Rigidbody>() != null) continue; // (a cart: it stands on the ground, not where the blueprint had it — Blueprint.CartOnGround)
                if (!LocParts.Place(nv, out of, out local) || of != Id || !ToWorld(local, out world)) continue;
                worst = Mathf.Max(worst, Vector3.Distance(world, nv.transform.position));
                n++;
            }
            if (n == 0) return;
            FrameOffBy = worst;
            if (worst > FrameOff) Log("its frame is off its own pieces by up to " + worst.ToString("0.00") + " m (" + n + " pieces): its shots, named spots, fog and cold are off by that — Where's height or turn is wrong");
            else Log("its frame fits its pieces (" + n + " pieces, at most " + worst.ToString("0.000") + " m)");
        }

        // its own golden mark with its no-build ring, «go there», for whoever its «mapmark:» conditions hold for (the Hall: once the world's
        // first heart stands, till you have been there — the user 2026-11-04: it burned on after his prologue had ended; with no story: always).
        // A place without the line is on the map only while a quest leads there (its stage's «mark:» — the user 2026-11-03: the raven leads
        // you to it, the map does not show it before)
        private void ShowMapMark()
        {
            Vector3 at; Quaternion rot;
            if (Minimap.instance == null) return;
            bool known = MapMark.Length > 0;
#if RAVEN_STORY
            Player me = Player.m_localPlayer;
            known = known && me != null && StoryCond.Holds(me, MapMark, e => Log("mapmark: " + e));
#endif
            if (known && Shape() && Where(out at, out rot)) StoryMap.Set(MarkKey, at, Lang.T(Title), NoBuild);
            else StoryMap.Clear(MarkKey);
        }

        private bool Again { get { return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(KeyAgain); } }

        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
        internal void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Loc " + Id + ": " + s); }
    }
}
#endif

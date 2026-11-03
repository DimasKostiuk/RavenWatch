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
    // (assets/story/blueprints/<id>.txt, the author's build), its layers and names (HallParts) — and where it goes, its
    // states and their look, all from the list of places (Locs: locations.list.txt). How it comes into a world:
    //   1. the server picks the place once a world: from the world as generated (WorldGenerator — land not visited
    //      yet is known too), with the world's seed (the same world, the same place), a bounded number of tries spread
    //      over frames, the cheap asks first (the land, the height) and the dear ones (the world's places, the players'
    //      buildings in the server's own list) only for a place that passed them; land nobody has been to first. Round
    //      the start (as the Hall), or round another place of the story (the user 2026-11-01: Torvald's stone near the
    //      Hall) once that one has its place. The place goes into the world's global keys: «<key> x z yaw».
    //   2. the first one whose near area (the game's own, where trees and stones are loaded) takes in all of its land
    //      builds it — before he can see it: its pieces show only there anyway. Once more, from what really stands
    //      there: the ground as it is now (the builder), any building of a player's within Clear m (the server: it has
    //      every building of the world, loaded or not) — refused: the server crosses the place out and picks another.
    //      Passed: the server lets ONE builder at a time (a claim that lapses), the ground is levelled
    //      (BlueprintGround), the pieces go in, and «<key>_built» is set.
    //   3. its pieces carry Blueprint's mark: no wear, no fall, no damage, no hammer but the author's (BlueprintGuard).
    internal sealed class Loc
    {
        // ---------------------------------------------------------------- what it is (Locs reads it)
        internal string Id, Title;
        internal Heightmap.Biome Biome = Heightmap.Biome.Meadows;
        internal string Near = "";                   // round another place of the story («» — round the start)
        internal bool Edge;                          // Min..Max counted between the walls of the two (not from the other's middle)
        internal float Min = 300f, Max = 1500f, Far = 3000f; // m from the start / from that place (the last ring only if the first has no place)
        internal float Clear = 40f;                  // m round the footprint free of any player's building
        internal float NoBuild = 30f;                // m from its middle: no building there (as round the start's stones)
        internal string[] Words = { "закинута" };    // its states' names (1, 2, …)
        internal string[][] Subs = { new string[0] }; // each state's sub-states' names (1.1, 1.2, …; 1.0 — the state itself)
        internal int RuinTo = 1;                     // states 1..RuinTo: a ruin (broken look, fires out)
        internal bool Fog;                           // Hel's mist over it while a ruin (HallFog)
        internal string[] RuinLit = new string[0];   // pieces (prefabs) still burning in its ruin — the rest of its fires are out
        internal readonly List<LitStep> LitFrom = new List<LitStep>(); // pieces (prefabs) burning in its ruin from a step on (Hall: hearth from 1.1)
        internal readonly List<KeyValuePair<string, string>> LitWhile = new List<KeyValuePair<string, string>>(); // «litwhile: fire_pit !flag echo_gone»: that fire burns (no fuel needed) for you while the story's conditions hold for you, else it is out (HallState.LitWhile)
        internal readonly List<KeyValuePair<string, string>> Touches = new List<KeyValuePair<string, string>>(); // «touch: shchyt step prologue munin_shield»: that named piece glows for you while the conditions hold, E on it — the story's event «use <place>.<name>» (LocTouch)
        internal float Sink;                         // m it stands lower in the ground than it was saved (its foundation half in the earth)
        internal bool LockChests;                    // its chests do not open (nobody's to take from)
        internal bool LockBeds;                      // nobody sleeps in its beds (nor makes them his spawn)
        internal bool Grass;                         // its ground keeps all its grass (else bare earth under its floors, grass round them)
        internal float BareRound;                    // >0: bare earth only within that many m of its middle, grass round it («grass: 5»)
        internal readonly Dictionary<string, Color> Tints = new Dictionary<string, Color>(); // pieces (prefabs) recoloured in it
        internal string KeyBase;                     // its world keys' stem
        internal string Follows = "";                // «follows: hall»: its state goes with the Hall's — 2 once the Hall is kindled, else 1 (HallState.SyncFollower)

        internal int Last { get { return Words.Length; } }
        internal bool IsHall { get { return Id == HallSpawn.Name; } }
        internal string KeySpot { get { return KeyBase; } }
        internal string KeyBuilt { get { return KeyBase + "_built"; } }
        internal string KeyNo { get { return KeyBase + "_no"; } }
        internal string KeyAgain { get { return KeyBase + "_again"; } } // (built again where it stood: no player-building check, it stood there already)
        internal string KeyState { get { return KeyBase + "_state"; } }
        internal string KeyNext { get { return KeyBase + "_next"; } }
        internal string KeySub { get { return KeyBase + "_sub"; } }
        internal string KeyGate { get { return KeyBase + "_gate"; } }
        internal string MarkKey { get { return IsHall ? "hall" : "loc_" + Id; } }

        private string Rpc(string what) { return IsHall ? "RavenWatch_Hall" + what : "RavenWatch_Loc_" + Id + "_" + what; }

        internal string WordOf(int s) { return s >= 1 && s <= Words.Length ? Words[s - 1] : s.ToString(); }
        internal int SubsOf(int s) { return s >= 1 && s <= Subs.Length ? Subs[s - 1].Length : 0; }
        internal string SubWord(int s, int sub) { return sub >= 1 && sub <= SubsOf(s) ? Subs[s - 1][sub - 1] : sub.ToString(); }
        internal string WordOf(int s, int sub) { return sub <= 0 ? WordOf(s) : WordOf(s) + " · " + SubWord(s, sub); }

        // «litfrom: 1.1 hearth»: these burn in its ruin from that step on (that state's later steps, and later states)
        internal struct LitStep { internal int State, Sub; internal string[] Prefabs; }
        internal bool LitAt(string prefab, int state, int sub)
        {
            foreach (LitStep f in LitFrom)
                if ((state > f.State || (state == f.State && sub >= f.Sub)) && Array.IndexOf(f.Prefabs, prefab) >= 0) return true;
            return false;
        }

        // what it is taken from a fresh read of the list (the place itself — its seeking, its builder — stays)
        internal void Take(Loc d)
        {
            Title = d.Title; Biome = d.Biome; Near = d.Near; Min = d.Min; Max = d.Max; Far = d.Far; Clear = d.Clear; NoBuild = d.NoBuild;
            Edge = d.Edge; Words = d.Words; Subs = d.Subs; RuinTo = d.RuinTo; Fog = d.Fog; RuinLit = d.RuinLit; Sink = d.Sink; KeyBase = d.KeyBase; Follows = d.Follows; LockChests = d.LockChests; LockBeds = d.LockBeds; Grass = d.Grass; BareRound = d.BareRound;
            LitFrom.Clear(); LitFrom.AddRange(d.LitFrom);
            LitWhile.Clear(); LitWhile.AddRange(d.LitWhile);
            Touches.Clear(); Touches.AddRange(d.Touches);
            Tints.Clear(); foreach (KeyValuePair<string, Color> t in d.Tints) Tints[t.Key] = t.Value;
        }

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
            Log("its blueprint: " + _parts.Count + " pieces, " + (_rect.X1 - _rect.X0).ToString("0") + " × " + (_rect.Z1 - _rect.Z0).ToString("0") + " m, ground " + _ground.ToString("0.00"));
            return true;
        }

        // ---------------------------------------------------------------- where it is (everyone: from the world's keys)
        internal bool Where(out Vector3 at, out Quaternion rot)
        {
            at = Vector3.zero; rot = Quaternion.identity;
            string v;
            if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(KeySpot, out v)) return false;
            string[] w = v.Split(' ');
            float x, z, yaw;
            if (w.Length < 3 || !TryF(w[0], out x) || !TryF(w[1], out z) || !TryF(w[2], out yaw)) return false;
            at = new Vector3(x, WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(x, z) : 0f, z);
            rot = Quaternion.Euler(0f, yaw, 0f);
            return true;
        }

        // its size in its blueprint: the footprint's corners at the floor and one at its top (HallFog), null — no blueprint
        internal List<Vector3> Corners()
        {
            if (!Shape()) return null;
            float top = 0f;
            foreach (Blueprint.Part p in _parts) top = Mathf.Max(top, p.Pos.y);
            return new List<Vector3> { new Vector3(_rect.X0, 0f, _rect.Z0), new Vector3(_rect.X1, 0f, _rect.Z1), new Vector3(_rect.X0, top, _rect.Z0) };
        }

        // where its floors lie in its blueprint (HallFog: the low mist only over them)
        internal List<Vector3> Floors()
        {
            if (!Shape()) return null;
            List<Vector3> list = new List<Vector3>();
            foreach (Blueprint.Part p in _parts) if (HallFog.IsFloor(p.Prefab)) list.Add(p.Pos);
            return list;
        }

        // where its roof lies in its blueprint (HallFog: the high mist only over it)
        internal List<Vector3> Roofs()
        {
            if (!Shape()) return null;
            List<Vector3> list = new List<Vector3>();
            foreach (Blueprint.Part p in _parts) if (HallFog.IsRoof(p.Prefab)) list.Add(p.Pos);
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
            return Outside(Quaternion.Inverse(rot) * d);
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

        // ---------------------------------------------------------------- the server: the place, once a world
        private const float FlatGen = 4f;          // m: the land as generated under it at most this far from its middle (the live check allows MaxDev 6)
        private const float AwayFromPlaces = 20f;  // m between it and a place of the world (beyond that place's own radius)
        private const float AwayFromStory = 10f;   // m between it and another place of the story (beyond both footprints)
        private const int Tries = 3000, PerFrame = 50;
        private const float ClaimLapse = 120f;
        private ZNet _world;
        private bool _seeking, _gaveUp;
        private long _claimBy;
        private float _claimAt, _nextSeek;

        private ZRoutedRpc _rpcOn; // (registered with this one: once a session)
        internal void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null || _rpcOn == ZRoutedRpc.instance) return;
            _rpcOn = ZRoutedRpc.instance;
            ZRoutedRpc.instance.Register<float, float, string>(Rpc("Blocked"), OnBlocked);
            ZRoutedRpc.instance.Register(Rpc("Claim"), OnClaim);
            ZRoutedRpc.instance.Register(Rpc("Go"), OnGo);
            ZRoutedRpc.instance.Register<int>(Rpc("Done"), OnDone);
            ZRoutedRpc.instance.Register(Rpc("Rebuild"), OnRebuild);
        }

        // ---------------------------------------------------------------- the author: built again from the blueprint now in the mod
        // A place stands as its blueprint was when it was built (a world keeps it; a later blueprint does not reach it).
        // The author's «перебудувати» (the user 2026-11-01): the server takes every piece of it away (those it stamped,
        // loaded or not) and forgets it was built — the next one who comes near builds it again, where it stood, from
        // the blueprint in this build. Its state, its fire, its doors stay as they were (world keys).
        internal void AskRebuild() { ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), Rpc("Rebuild")); }

        private void OnRebuild(long sender)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || !Plugin.IsAdminSender(sender)) return;
            Rebuild(sender, false);
        }

        // the author's jobs left for the server in config/RavenWatch/blueprints (each done once, its file gone):
        //   <id>.rebuild — built again where it stands, from the blueprint in this build;
        //   <id>.move    — taken away and its place sought anew (its rules changed)
        //   zala.unlight — the Hearth of the Watch cold again (for tests: its fire, and Hel's mist, as at the start)
        // A world's name inside: only in that world (empty: the first world the server opens)
        private void Jobs()
        {
            if (!Blueprint.On) return;
            if (IsHall && Job("unlight")) { HallHearth.SetLevel(HallHearth.Cold); ZoneSystem.instance.RemoveGlobalKey(KeyNext); ZoneSystem.instance.RemoveGlobalKey(KeySub); ZoneSystem.instance.SetGlobalKey(KeyState + " 1"); Log("the author's job: the hearth cold again, the Hall abandoned"); }
            string move = Path.Combine(Blueprint.Root, Id + ".move"), again = Path.Combine(Blueprint.Root, Id + ".rebuild");
            bool m = File.Exists(move), r = !m && File.Exists(again);
            if (!m && !r) return;
            string world = "";
            try { world = File.ReadAllText(m ? move : again, Encoding.UTF8).Trim(); } catch { }
            if (world.Length > 0 && world != ZNet.instance.GetWorldName()) return; // (meant for another world: left for it)
            try { File.Delete(m ? move : again); } catch { return; }
            Log("the author's job: " + (m ? "a new place" : "built again"));
            Rebuild(ZDOMan.GetSessionID(), m);
        }

        // a job file «<id>.<what>» for this world: taken (its file gone) — true; none, or another world's — false
        private bool Job(string what)
        {
            string f = Path.Combine(Blueprint.Root, Id + "." + what);
            if (!File.Exists(f)) return false;
            string world = "";
            try { world = File.ReadAllText(f, Encoding.UTF8).Trim(); } catch { }
            if (world.Length > 0 && world != ZNet.instance.GetWorldName()) return false;
            try { File.Delete(f); } catch { return false; }
            return true;
        }

        private void Rebuild(long sender, bool move)
        {
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot) || !Shape()) { if (sender != ZDOMan.GetSessionID()) Plugin.ReplyTo(sender, Title + ": місця ще нема або нема креслення."); return; }
            int gone = 0;
            List<ZDO> all = new List<ZDO>();
            foreach (Vector2s z in Zones(at, Reach() + 20f))
            {
                all.Clear();
                if (FindObjects != null) FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() });
                foreach (ZDO zdo in all)
                {
                    if (zdo == null) continue;
                    string of = zdo.GetString(HallParts.KOf, "");
                    // its own; or a blueprint's piece with no stamp standing in it (built before pieces were stamped)
                    if (of != Id && !(of.Length == 0 && zdo.GetInt(Blueprint.Mark) == 1 && From(zdo.GetPosition()) < 1f)) continue;
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                    gone++;
                }
            }
            ZoneSystem.instance.RemoveGlobalKey(KeyBuilt);
            if (move) ZoneSystem.instance.RemoveGlobalKey(KeyAgain); else ZoneSystem.instance.SetGlobalKey(KeyAgain);
            if (move)
            {
                ZoneSystem.instance.RemoveGlobalKey(KeySpot); _gaveUp = false; _nextSeek = Time.time + 2f;
                // the places beside it go along: built ones taken away too, all sought again with it (never one left alone)
                foreach (Loc k in Locs.All)
                {
                    if (k == this || k.Near != Id) continue;
                    if (k.Built) k.Rebuild(ZDOMan.GetSessionID(), true);
                    else ZoneSystem.instance.RemoveGlobalKey(k.KeySpot);
                }
            }
            _claimBy = 0L;
            Log("taken away for building again (" + gone + " pieces)" + (move ? ", its place sought anew" : ""));
            if (sender != ZDOMan.GetSessionID() || Player.m_localPlayer != null) Plugin.ReplyTo(sender, Title + ": прибрано " + gone + " деталей. Постане знову з нового креслення, щойно хтось підійде.");
        }

        internal void ServerTick()
        {
            if (ZNet.instance != _world) { _world = ZNet.instance; _seeking = _gaveUp = false; _nextSeek = 0f; _claimBy = 0L; _read = false; _parts = null; _marks.Clear(); _pending = false; _askAt = 0f; }
            if (_seeking || Time.time < _nextSeek || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || WorldGenerator.instance == null || ZDOMan.instance == null) return;
            _nextSeek = Time.time + 5f; // (a look every few seconds: one string asked of the world's keys)
            if (!ZoneSystem.instance.LocationsGenerated) return; // (a new world: its places first, then ours away from them)
            if (Shape()) Jobs();
            if (!Shape() || Built) return;
            Vector3 at; Quaternion rot;
            if (Where(out at, out rot)) return; // (picked: it waits for its builder)
            if (_gaveUp) return; // (no place in this world: told once in the log; a restart tries again)
            Vector3 centre;
            if (!Centre(out centre)) return; // (round a place of the story that has no place yet: later)
            _seeking = true;
            Plugin.Instance.StartCoroutine(Seek(centre));
        }

        // what it is sought round: the start, or another place of the story once that one has its place
        private bool Centre(out Vector3 c)
        {
            c = Vector3.zero;
            if (Near.Length == 0)
            {
                ZoneSystem.LocationInstance li;
                if (ZoneSystem.instance.FindClosestLocation("StartTemple", Vector3.zero, out li)) c = li.m_position;
                return true;
            }
            Loc other = Locs.Get(Near);
            Quaternion r;
            return other != null && other != this && other.Where(out c, out r);
        }

        private IEnumerator Seek(Vector3 centre)
        {
            HashSet<string> no = Crossed();
            System.Random rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ (IsHall ? 0x48616c6c : Id.GetStableHashCode())); // (the same world, the same tries)
            int land = 0, uneven = 0, places = 0, built = 0, story = 0, crossed = 0, kin = 0, tried = 0;
            Vector3 found = Vector3.zero; float yaw = 0f; bool ok = false;
            // the rings: the near one, land nobody has been to first; then the near one any; then the far one any
            float far = Mathf.Max(Far, Max * 2f);
            float[][] rings = { new[] { Min, Max, 1f }, new[] { Min, Max, 0f }, new[] { Max, far, 0f }, new[] { far, far * 2f, 0f } }; // (the last: a world with no room near — the story must still begin somewhere)
            Loc by = Edge ? Locs.Get(Near) : null; // (counted from its walls: the middle a little further off, by both sizes)
            float own = Reach() - GroundMargin;
            foreach (float[] ring in rings)
            {
                if (ring[2] > 0f && Near.Length > 0) continue; // (beside a place of the story: its land is visited — no «nobody's been there» ring)
                for (int i = 0; i < Tries && !ok; i++)
                {
                    if (++tried % PerFrame == 0) yield return null;
                    float lo = ring[0], hi = ring[1];
                    if (by != null) { lo += own; hi += own + by.Reach(); }
                    float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = lo + (float)rnd.NextDouble() * (hi - lo);
                    Vector3 c = new Vector3(centre.x + Mathf.Cos(ang) * r, 0f, centre.z + Mathf.Sin(ang) * r);
                    if (by != null) { float gap = by.From(c) - own; if (gap < ring[0] || gap > ring[1]) { story++; continue; } }
                    Vector3 face = centre - c; face.y = 0f;
                    float y = Quaternion.LookRotation(face.normalized, Vector3.up).eulerAngles.y; // (its front to the start, or to the place it stands by: the way people come)
                    Quaternion q = Quaternion.Euler(0f, y, 0f);
                    if (no.Contains(SpotId(c))) { crossed++; continue; }
                    if (!Land(c, q)) { land++; continue; }
                    if (!Even(c, q)) { uneven++; continue; }
                    if (ring[2] > 0f && !Fresh(c)) continue;
                    if (NearStory(c)) { story++; continue; }
                    if (NearPlace(c, q)) { places++; continue; }
                    if (NearBuilding(c, q, true)) { built++; continue; }
                    if (!KinFit(c, q, rnd)) { kin++; continue; }
                    found = c; yaw = y; ok = true;
                }
                if (ok) break;
            }
            _seeking = false;
            if (!ok)
            {
                // no room beside the place it stands by: that one, not built yet, moves (and this with it) — never one alone
                Loc o = Near.Length > 0 ? Locs.Get(Near) : null; Vector3 oat; Quaternion orot;
                if (o != null && !o.Built && o.Where(out oat, out orot)) { o.CrossOut(oat, "no room for " + Id + " beside it"); yield break; }
                _gaveUp = true;
            }
            if (!ok) { Log("no place (tries " + tried + ": not " + Biome + " " + land + ", uneven/water " + uneven + ", a place of the world near " + places + ", of the story " + story + ", a player's building near " + built + ", crossed out " + crossed + ", no room for the places beside it " + kin + ")"); yield break; }
            ZoneSystem.instance.SetGlobalKey(KeySpot + " " + F(found.x) + " " + F(found.z) + " " + F(yaw));
            foreach (KeyValuePair<Loc, Vector3> k in _kinSpots) // (the places that stand by it: their room kept with it, set at once)
            {
                ZoneSystem.instance.SetGlobalKey(k.Key.KeySpot + " " + F(k.Value.x) + " " + F(k.Value.z) + " " + F(_kinYaw[k.Key]));
                k.Key.Log("its place, with " + Id + ": " + Mathf.RoundToInt(k.Value.x) + " " + Mathf.RoundToInt(k.Value.z));
            }
            Log("its place: " + Mathf.RoundToInt(found.x) + " " + Mathf.RoundToInt(found.z) + ", " + Mathf.RoundToInt(Vector3.Distance(new Vector3(found.x, 0f, found.z), new Vector3(centre.x, 0f, centre.z))) + " m from " + (Near.Length > 0 ? Near : "the start") + " (tries " + tried + ")");
        }

        // ---------------------------------------------------------------- the places that stand by it, sought with it
        // A place round which others stand (the Hall: Torvald's stone) takes a spot only where there is room for each of
        // them too (the user 2026-11-01: never one without the other); their spots are set with its own.
        private readonly List<KeyValuePair<Loc, Vector3>> _kinSpots = new List<KeyValuePair<Loc, Vector3>>();
        private readonly Dictionary<Loc, float> _kinYaw = new Dictionary<Loc, float>();
        private const int KinTries = 150;

        private bool KinFit(Vector3 c, Quaternion q, System.Random rnd)
        {
            _kinSpots.Clear(); _kinYaw.Clear();
            foreach (Loc k in Locs.All)
            {
                Vector3 at; Quaternion r;
                if (k == this || k.Near != Id || !k.Shape() || k.Where(out at, out r)) continue; // (no blueprint yet, or placed already)
                Vector3 kc; float ky;
                if (!k.TryBeside(this, c, q, rnd, out kc, out ky)) return false;
                _kinSpots.Add(new KeyValuePair<Loc, Vector3>(k, kc));
                _kinYaw[k] = ky;
            }
            return true;
        }

        // a spot for this one beside «by», were «by» standing at (at, rot): its own rules, but no waiting for the other
        internal bool TryBeside(Loc by, Vector3 at, Quaternion rot, System.Random rnd, out Vector3 found, out float yaw)
        {
            found = Vector3.zero; yaw = 0f;
            HashSet<string> no = Crossed();
            float own = Reach() - GroundMargin;
            for (int i = 0; i < KinTries; i++)
            {
                float lo = Min, hi = Max;
                if (Edge) { lo += own; hi += own + by.Reach(); }
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = lo + (float)rnd.NextDouble() * (hi - lo);
                Vector3 c = new Vector3(at.x + Mathf.Cos(ang) * r, 0f, at.z + Mathf.Sin(ang) * r);
                if (Edge) { float gap = by.FromAt(c, at, rot) - own; if (gap < Min || gap > Max) continue; }
                else { Vector3 d = c - at; d.y = 0f; if (d.magnitude < Reach() + by.Reach() + AwayFromStory) continue; }
                Vector3 face = at - c; face.y = 0f;
                float y = Quaternion.LookRotation(face.normalized, Vector3.up).eulerAngles.y;
                Quaternion q = Quaternion.Euler(0f, y, 0f);
                if (no.Contains(SpotId(c)) || !Land(c, q) || !Even(c, q) || NearStory(c) || NearPlace(c, q) || NearBuilding(c, q, true)) continue;
                found = c; yaw = y;
                return true;
            }
            return false;
        }

        // the footprint's points, as generated: the corners, the middle, and a grid every Step m (cheap: a few dozen asks)
        private const float Step = 3f;
        private IEnumerable<Vector3> Points(Vector3 c, Quaternion q, float grow)
        {
            for (float x = _rect.X0 - grow; x <= _rect.X1 + grow + 0.01f; x += Step)
                for (float z = _rect.Z0 - grow; z <= _rect.Z1 + grow + 0.01f; z += Step)
                    yield return c + q * new Vector3(Mathf.Min(x, _rect.X1 + grow), 0f, Mathf.Min(z, _rect.Z1 + grow));
        }

        private const float GroundMargin = 6f; // (BlueprintGround eases the edge over 6 m: that land too)

        // all of it and its eased edge in its land (the corners first: most tries fail there)
        private bool Land(Vector3 c, Quaternion q)
        {
            float g = GroundMargin;
            Vector3[] corners = { new Vector3(_rect.X0 - g, 0f, _rect.Z0 - g), new Vector3(_rect.X1 + g, 0f, _rect.Z0 - g), new Vector3(_rect.X0 - g, 0f, _rect.Z1 + g), new Vector3(_rect.X1 + g, 0f, _rect.Z1 + g), Vector3.zero };
            foreach (Vector3 k in corners) { Vector3 p = c + q * k; if (WorldGenerator.instance.GetBiome(p.x, p.z) != Biome) return false; }
            foreach (Vector3 p in Points(c, q, g)) if (WorldGenerator.instance.GetBiome(p.x, p.z) != Biome) return false;
            return true;
        }

        // flat enough as generated, and dry with its edge
        private bool Even(Vector3 c, Quaternion q)
        {
            List<float> h = new List<float>();
            foreach (Vector3 p in Points(c, q, 0f)) h.Add(WorldGenerator.instance.GetHeight(p.x, p.z));
            h.Sort();
            float mid = h[h.Count / 2], sea = ZoneSystem.instance.m_waterLevel;
            if (mid - h[0] > FlatGen || h[h.Count - 1] - mid > FlatGen || mid < sea + 2f) return false;
            foreach (Vector3 p in Points(c, q, GroundMargin)) if (WorldGenerator.instance.GetHeight(p.x, p.z) < sea + 0.5f) return false;
            return true;
        }

        // the zones it covers: none made yet (nobody has been there — no building of anyone's can stand there)
        private static readonly FieldInfo GeneratedZones = HarmonyLib.AccessTools.Field(typeof(ZoneSystem), "m_generatedZones");
        private bool Fresh(Vector3 c)
        {
            HashSet<Vector2s> made = GeneratedZones != null ? GeneratedZones.GetValue(ZoneSystem.instance) as HashSet<Vector2s> : null;
            if (made == null) return false;
            foreach (Vector2s z in Zones(c, Reach() + Clear)) if (made.Contains(z)) return false;
            return true;
        }

        internal float Reach()
        {
            float w = Mathf.Max(Mathf.Abs(_rect.X0), Mathf.Abs(_rect.X1)), l = Mathf.Max(Mathf.Abs(_rect.Z0), Mathf.Abs(_rect.Z1));
            return Mathf.Sqrt(w * w + l * l) + GroundMargin;
        }

        // ---------------------------------------------------------------- where it stands: one rule for every caller
        // (the user 2026-11-02: the story's places, Heid, the marks, the scenes — never each its own way)
        //   1. the one the world built (its place in the world's keys)
        //   2. else, the author's world: his own building, by its frame (BlueprintFrame)
        //   3. else the spot the world chose for it, not built yet (nothing stands there)
        // ByFrame: it is the author's own building (2) — its frame's height is the building's own; the world's spot has
        // only the land's height as generated (the ground was levelled when it was built)
        internal bool ByFrame { get { Vector3 c; Quaternion r; return !Built && Blueprint.On && BlueprintFrame.Get(Id, out c, out r); } }
        internal bool Stands { get { return Built || ByFrame; } }

        internal bool Anchor(out Vector3 at, out Quaternion rot)
        {
            if (!Built && Blueprint.On && BlueprintFrame.Get(Id, out at, out rot)) return true;
            return Where(out at, out rot);
        }

        // a point of its blueprint's own frame (a piece's place in it) in this world, by Anchor. Its height only about on
        // the world's spot (the ground was levelled when it was built)
        internal bool ToWorld(Vector3 local, out Vector3 world)
        {
            world = Vector3.zero;
            Vector3 at; Quaternion rot;
            if (!Shape() || !Anchor(out at, out rot)) return false;
            world = at + rot * (ByFrame ? local : local - Vector3.up * (_ground == BlueprintGround.NoGround ? 0f : _ground));
            return true;
        }

        // the other way: a point of this world in its blueprint's own frame, and its turn (the author's camera shots: StoryShots)
        internal bool ToLocal(Vector3 world, out Vector3 local)
        {
            local = Vector3.zero;
            Vector3 at; Quaternion rot;
            if (!Shape() || !Anchor(out at, out rot)) return false;
            local = Quaternion.Inverse(rot) * (world - at) + (ByFrame ? Vector3.zero : Vector3.up * (_ground == BlueprintGround.NoGround ? 0f : _ground));
            return true;
        }

        internal bool Turn(out Quaternion rot) { Vector3 at; return Anchor(out at, out rot); }

        // a named piece of it (its names: HallParts — «vohnyshche», «dveri_holovni»), loaded or not: where it is (its object
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
            return HallParts.NameSpot(Id, name, out prefab, out local) && ToWorld(local, out world);
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

        // another place of the story (placed already, or its blueprint known) too near: their circles apart
        private bool NearStory(Vector3 c)
        {
            foreach (Loc o in Locs.All)
            {
                Vector3 at; Quaternion r;
                if (o == this || (Edge && o.Id == Near) || !o.Shape() || !o.Where(out at, out r)) continue; // (the one it stands by: kept by Edge's own count)
                Vector3 d = at - c; d.y = 0f;
                if (d.magnitude < Reach() + o.Reach() + AwayFromStory) return true;
            }
            return false;
        }

        // a place of the world (a ruin, a crypt, a village, the start…) too near: only the zones round it are asked
        private bool NearPlace(Vector3 c, Quaternion q)
        {
            Quaternion inv = Quaternion.Inverse(q);
            foreach (Vector2s z in Zones(c, Reach() + 64f))
            {
                ZoneSystem.LocationInstance li;
                if (!ZoneSystem.instance.m_locationInstances.TryGetValue(z, out li)) continue;
                float own = li.m_location != null ? Mathf.Max(li.m_location.m_exteriorRadius, li.m_location.m_interiorRadius) : 32f;
                Vector3 d = li.m_position - c; d.y = 0f;
                if (Outside(inv * d) < own + AwayFromPlaces) return true;
            }
            return false;
        }

        // a player's building within Clear m of the footprint, from the server's own list of the world's objects (only
        // the zones round it): a piece someone built has its creator. A blueprint's pieces (the story's) are not his
        private static readonly MethodInfo FindObjects = HarmonyLib.AccessTools.Method(typeof(ZDOMan), "FindObjects");
        private static readonly Dictionary<int, bool> _isPiece = new Dictionary<int, bool>();
        private bool NearBuilding(Vector3 c, Quaternion q, bool skipMarked)
        {
            if (FindObjects == null) return false;
            Quaternion inv = Quaternion.Inverse(q);
            List<ZDO> all = new List<ZDO>();
            foreach (Vector2s z in Zones(c, Reach() + Clear))
            {
                all.Clear();
                FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() });
                foreach (ZDO zdo in all)
                {
                    if (zdo == null || zdo.GetLong(ZDOVars.s_creator, 0L) == 0L || !Piece(zdo.GetPrefab())) continue;
                    if (skipMarked && zdo.GetInt(Blueprint.Mark) == 1) continue;
                    Vector3 d = zdo.GetPosition() - c; d.y = 0f;
                    if (Outside(inv * d) < Clear) return true;
                }
            }
            return false;
        }

        private static bool Piece(int hash)
        {
            bool p;
            if (_isPiece.TryGetValue(hash, out p)) return p;
            GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            p = pf == null || pf.GetComponent<global::Piece>() != null; // (unknown to this game: count it — better a wrong «no» than a hall on a house)
            _isPiece[hash] = p;
            return p;
        }

        private float Outside(Vector3 lp)
        {
            float dx = Mathf.Max(0f, Mathf.Max(_rect.X0 - lp.x, lp.x - _rect.X1));
            float dz = Mathf.Max(0f, Mathf.Max(_rect.Z0 - lp.z, lp.z - _rect.Z1));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // the crossed-out places («<key>_no x/z,x/z…»): a builder found a building or bad ground there
        private HashSet<string> Crossed()
        {
            HashSet<string> set = new HashSet<string>();
            string v;
            if (ZoneSystem.instance.GetGlobalKey(KeyNo, out v)) foreach (string s in v.Split(',')) if (s.Length > 0) set.Add(s);
            return set;
        }

        private static string SpotId(Vector3 c) { return Mathf.RoundToInt(c.x) + "/" + Mathf.RoundToInt(c.z); }

        private void OnBlocked(long sender, float x, float z, string why)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Built) return;
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot) || Mathf.Abs(at.x - x) > 1f || Mathf.Abs(at.z - z) > 1f) return; // (an old word: that place is gone already)
            CrossOut(at, why);
        }

        internal void CrossOut(Vector3 at, string why)
        {
            HashSet<string> no = Crossed();
            no.Add(SpotId(at));
            ZoneSystem.instance.SetGlobalKey(KeyNo + " " + string.Join(",", new List<string>(no).ToArray()));
            ZoneSystem.instance.RemoveGlobalKey(KeySpot);
            ZoneSystem.instance.RemoveGlobalKey(KeyAgain);
            foreach (Loc k in Locs.All) if (k != this && k.Near == Id && !k.Built) ZoneSystem.instance.RemoveGlobalKey(k.KeySpot); // (the places beside it go with it)
            Log("its place " + SpotId(at) + " crossed out (" + why + "): another is sought");
            _claimBy = 0L;
            _nextSeek = Time.time + 2f; // (ServerTick seeks again once the key is gone)
        }

        private void OnClaim(long sender)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Built) return;
            if (_claimBy != 0L && _claimBy != sender && Time.time - _claimAt < ClaimLapse) return; // (someone builds it now)
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot) || !Shape()) return;
            if (!Again && NearBuilding(at, rot, true)) { CrossOut(at, "a player's building near"); return; } // (the builder sees only his near area: the server, all)
            _claimBy = sender; _claimAt = Time.time;
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, Rpc("Go"));
        }

        private void OnDone(long sender, int made)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || Built) return;
            if (_claimBy != 0L && sender != _claimBy) return; // (only the one let build it says it stands)
            ZoneSystem.instance.SetGlobalKey(KeyBuilt);
            ZoneSystem.instance.RemoveGlobalKey(KeyAgain);
            _claimBy = 0L;
            Log("built (" + made + " pieces)");
        }

        // ---------------------------------------------------------------- everyone: the one who comes near builds it
        private bool _pending, _close;
        private float _askAt, _nextLook, _readyAt;

        internal void Tick()
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + (_close ? 0.25f : 1f); // (near its place: looked at often — it must stand the moment its land comes in)
            MapMark();
            Player me = Player.m_localPlayer;
            if (me == null || ZoneSystem.instance == null || ZNetScene.instance == null || Built) { _readyAt = 0f; _close = false; return; }
            if (_pending && Time.time - _askAt < ClaimLapse) return;
            _pending = false;
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot)) return;
            Vector3 d = me.transform.position - at; d.y = 0f;
            _close = d.magnitude < 300f;
            if (!Shape() || !InNearArea(at, rot)) { _readyAt = 0f; return; }
            // a moment for its trees and stones to come in (the zones are ready already: IsAreaReady)
            if (_readyAt == 0f) { _readyAt = Time.time + 0.5f; return; }
            if (Time.time < _readyAt) return;
            _readyAt = 0f;
            string why = Blocked(at, rot);
            if (why != null) { ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), Rpc("Blocked"), at.x, at.z, why); Log("its place refused here: " + why); _pending = true; _askAt = Time.time; return; }
            _pending = true; _askAt = Time.time;
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), Rpc("Claim"));
        }

        // the Hall on everyone's map from the moment its place is chosen: the story's golden «!» with its no-build ring;
        // the other places only while a quest leads there (its stage's «mark:» — the user 2026-11-01: Torvald's stone
        // is found by following the raven, not seen on the map beforehand)
        private void MapMark()
        {
            Vector3 at; Quaternion rot;
            if (Minimap.instance == null) return;
            if (IsHall && Shape() && Where(out at, out rot)) StoryMap.Set(MarkKey, at, Lang.T(Title), NoBuild);
            else StoryMap.Clear(MarkKey);
        }

        // all of its land (the footprint and the eased edge, where trees and stones are cleared) inside this game's near
        // area — the game's own test (ZNetScene.InActiveArea) — its zones loaded and their objects made
        private static readonly HashSet<Vector2s> _zones = new HashSet<Vector2s>();
        private bool InNearArea(Vector3 at, Quaternion rot)
        {
            if (ZNet.instance == null) return false;
            Vector2s me = ZoneSystem.GetZone(ZNet.instance.GetReferencePosition());
            _zones.Clear();
            foreach (Vector3 p in Points(at, rot, GroundMargin))
            {
                if (!ZNetScene.InActiveArea(p, me)) return false;
                _zones.Add(ZoneSystem.GetZone(p));
            }
            foreach (Vector2s z in _zones) if (!ZoneSystem.instance.IsZoneLoaded(z) || !ZNetScene.instance.IsAreaReady(ZoneSystem.GetZonePos(z))) return false;
            return true;
        }

        // what really stands there now: a player's building near (in this near area; the server asks the rest), or the
        // ground gone wrong (dug, raised)
        private static readonly List<global::Piece> _nearPieces = new List<global::Piece>();
        private bool Again { get { return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(KeyAgain); } }

        private string Blocked(Vector3 at, Quaternion rot)
        {
            Quaternion inv = Quaternion.Inverse(rot);
            _nearPieces.Clear();
            if (!Again) global::Piece.GetAllPiecesInRadius(at, Reach() + Clear, _nearPieces); // (built again where it stood: the buildings round it were there already)
            foreach (global::Piece pc in _nearPieces)
            {
                if (pc == null || pc.GetCreator() == 0L) continue;
                ZNetView nv = pc.GetComponent<ZNetView>();
                if (Blueprint.Marked(nv)) continue;
                Vector3 d = pc.transform.position - at; d.y = 0f;
                if (Outside(inv * d) < Clear) return "a player's " + Utils.GetPrefabName(pc.gameObject) + " " + Mathf.RoundToInt(Outside(inv * d)) + " m off";
            }
            BlueprintGround.Fit fit = BlueprintGround.Check(_parts, at, rot);
            return fit.Ok ? null : "ground: " + fit.Why;
        }

        private void OnGo(long sender)
        {
            if (Built || Player.m_localPlayer == null) return;
            if (Blueprint.Placing) { _pending = false; return; } // (another place going up here this moment: ask again in a second — the claim stays ours)
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot) || !Shape()) return;
            BlueprintGround.Fit fit = BlueprintGround.Check(_parts, at, rot);
            if (!fit.Ok) { ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), Rpc("Blocked"), at.x, at.z, "ground: " + fit.Why); return; }
            at.y = fit.Ground - _ground - Sink; // (the ground levelled where it was; the building into it by Sink)
            string ground = BlueprintGround.Level(_parts, at, rot, fit.Ground, !Grass, Floors(), BareRound); // (earth only under its floors, or within BareRound m of its middle: grass round it)
            BlueprintGround.Forget(); // (the world's own building: no undo of its ground)
            Log("building at " + SpotId(at) + ": " + ground);
            Blueprint.Spawn(_parts, _marks, at, rot, Id, (made, missing) =>
            {
                Log("it stands: " + made + " pieces" + (missing > 0 ? ", " + missing + " unknown to this game" : ""));
                ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), Rpc("Done"), made);
            });
        }

        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
        internal void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log((IsHall ? "Hall: " : "Loc " + Id + ": ") + s); }
    }

    // The list of the story's places: assets/story/blueprints/locations.list.txt (built in), or the author's own copy in
    // config/RavenWatch/blueprints (it wins wholly; «Нова локація» in the window writes there). A block a place:
    //   loc <id>                 — its blueprint's name (latin)
    //   name: <title>
    //   biome: Meadows           — the game's biome
    //   from: start 300 1500 3000 | from: <another id> 60 250 [edge — counted between their walls]
    //   clear: 40   nobuild: 30  — m free of players' buildings round it / no building from its middle
    //   states: закинута, …      — its states' names, 1, 2, …
    //   ruin: 2                  — states 1..2 a ruin (broken look, fires out)
    //   fog: yes                 — Hel's mist while a ruin
    //   ruinlit: Candle_resin    — pieces still burning in the ruin (the rest of its fires are out)
    //   sink: 0.5                — m lower in the ground than saved (the foundation half in the earth)
    //   lockchests: yes          — its chests do not open
    //   lockbeds: yes            — nobody sleeps in its beds
    //   litwhile: fire_pit !flag echo_gone — that fire burns (no fuel needed) for each player while the conditions hold for
    //                              him (StoryCond), else it is out: the ghost's fire in the house of the prologue
    //   touch: shchyt step prologue munin_shield — that named piece glows for each player while the conditions hold;
    //                              E on it then — the story's event «use <place>.<name>» (LocTouch: Munin's shield)
    //   follows: hall            — its state goes with the Hall's: 1 till it is kindled, 2 from then on (Heid's camp)
    //   grass: yes               — all the grass stays (else bare earth under its floors only)
    //   tint: <prefab> <RRGGBB>  — that piece recoloured in it (a line each)
    //   keys: ravenwatch_hall    — its world keys' stem (the Hall keeps its old ones; else ravenwatch_loc_<id>)
    // The Hall of the Watch is always there (from the list, else as it was built in 2.9).
    internal static class Locs
    {
        internal const string File = "locations.list";
        private static List<Loc> _all;

        internal static List<Loc> All { get { if (_all == null) Load(); return _all; } }
        internal static Loc Hall { get { return Get(HallSpawn.Name); } }

        internal static Loc Get(string id)
        {
            foreach (Loc l in All) if (l.Id == id) return l;
            return null;
        }

        // the place a piece is of: stamped by a blueprint, or — in the author's world — the one whose frame it stands in
        internal static Loc Of(ZNetView nv)
        {
            string of; Vector3 at;
            return nv != null && nv.IsValid() && HallParts.Place(nv, out of, out at) ? Get(of) : null;
        }

        internal static string AuthorFile { get { return Path.Combine(Blueprint.Root, File + ".txt"); } }

        internal static void Load()
        {
            List<Loc> list = new List<Loc>();
            string text = null;
            try
            {
                if (Blueprint.On && System.IO.File.Exists(AuthorFile)) text = System.IO.File.ReadAllText(AuthorFile, Encoding.UTF8);
                else
                    using (Stream st = typeof(Locs).Assembly.GetManifestResourceStream("RavenWatch.blueprints." + File + ".txt"))
                        if (st != null) using (StreamReader r = new StreamReader(st, Encoding.UTF8)) text = r.ReadToEnd();
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Locs: " + e.Message); }
            if (text != null) Parse(text, list);
            if (list.Find(l => l.Id == HallSpawn.Name) == null) list.Insert(0, HallDefault());
            if (_all == null) { _all = list; return; }
            // read again (a place added): the places already known stay the same objects, the new ones join
            List<Loc> merged = new List<Loc>();
            foreach (Loc d in list)
            {
                Loc known = _all.Find(l => l.Id == d.Id);
                if (known != null) { known.Take(d); merged.Add(known); }
                else { merged.Add(d); d.RegisterRpcs(); }
            }
            _all = merged;
        }

        // as the Hall was built in 2.9 before the list
        private static Loc HallDefault()
        {
            return new Loc { Id = HallSpawn.Name, Title = "Зала Варти", KeyBase = "ravenwatch_hall", Min = 300f, Max = 1500f, Far = 3000f, Clear = 40f, NoBuild = 30f,
                             Words = new[] { "закинута", "відремонтована", "обжита" }, Subs = new[] { new[] { "гейд", "розпалена" }, new string[0], new string[0] }, RuinTo = 1, Fog = true, RuinLit = new[] { "Candle_resin" }, Sink = 0.5f, LockBeds = true };
        }

        private static void Parse(string text, List<Loc> into)
        {
            Loc cur = null;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l.StartsWith("loc "))
                {
                    string id = l.Substring(4).Trim();
                    if (!Blueprint.GoodName(id)) { cur = null; continue; }
                    cur = id == HallSpawn.Name ? HallDefault() : new Loc { Id = id, Title = id, KeyBase = "ravenwatch_loc_" + id.ToLowerInvariant() };
                    into.Add(cur);
                    continue;
                }
                int colon = l.IndexOf(':');
                if (cur == null || colon < 0) continue;
                string k = l.Substring(0, colon).Trim().ToLowerInvariant(), v = l.Substring(colon + 1).Trim();
                string[] w = v.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                float f;
                switch (k)
                {
                    case "name": cur.Title = v; break;
                    case "biome": try { cur.Biome = (Heightmap.Biome)Enum.Parse(typeof(Heightmap.Biome), v, true); } catch { } break;
                    case "from":
                        if (w.Length == 0) break;
                        cur.Near = w[0] == "start" ? "" : w[0];
                        if (w.Length > 1 && TryF(w[1], out f)) cur.Min = f;
                        if (w.Length > 2 && TryF(w[2], out f)) cur.Max = f;
                        cur.Far = w.Length > 3 && TryF(w[3], out f) ? f : cur.Max * 2f;
                        cur.Edge = Array.IndexOf(w, "edge") > 0;
                        break;
                    case "clear": if (TryF(v, out f)) cur.Clear = f; break;
                    case "nobuild": if (TryF(v, out f)) cur.NoBuild = f; break;
                    case "states": States(v, cur); break;
                    case "ruin": int n; if (int.TryParse(v, out n)) cur.RuinTo = n; break;
                    case "fog": cur.Fog = v == "yes" || v == "так" || v == "1"; break;
                    case "ruinlit": cur.RuinLit = w; break;
                    case "litfrom":
                        int ls, lk;
                        if (w.Length >= 2 && HallState.SubNumber(w[0], out ls, out lk)) { string[] p = new string[w.Length - 1]; Array.Copy(w, 1, p, 0, p.Length); cur.LitFrom.Add(new Loc.LitStep { State = ls, Sub = lk, Prefabs = p }); }
                        break;
                    case "sink": if (TryF(v, out f)) cur.Sink = f; break;
                    case "lockchests": cur.LockChests = v == "yes" || v == "так" || v == "1"; break;
                    case "lockbeds": cur.LockBeds = v == "yes" || v == "так" || v == "1"; break;
                    case "litwhile": if (w.Length >= 2) cur.LitWhile.Add(new KeyValuePair<string, string>(w[0], v.Substring(v.IndexOf(w[0], StringComparison.Ordinal) + w[0].Length).Trim())); break;
                    case "touch": if (w.Length >= 2) cur.Touches.Add(new KeyValuePair<string, string>(w[0], v.Substring(v.IndexOf(w[0], StringComparison.Ordinal) + w[0].Length).Trim())); break;
                    case "grass": cur.Grass = v == "yes" || v == "так" || v == "1"; if (!cur.Grass && TryF(v, out f) && f > 1f) cur.BareRound = f; break;
                    case "tint":
                        Color col;
                        if (w.Length >= 2 && ColorUtility.TryParseHtmlString("#" + w[1].TrimStart('#'), out col)) cur.Tints[w[0]] = col;
                        break;
                    case "keys": if (v.Length > 0) cur.KeyBase = v.ToLowerInvariant(); break;
                    case "follows": cur.Follows = v.Trim().ToLowerInvariant(); break;
                }
            }
        }

        // «закинута (розпалена / відчинена), відремонтована, обжита»: the states, each with its sub-states in brackets
        private static void States(string v, Loc into)
        {
            List<string> words = new List<string>();
            List<string[]> subs = new List<string[]>();
            int depth = 0, from = 0;
            for (int i = 0; i <= v.Length; i++)
            {
                char c = i < v.Length ? v[i] : ',';
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                if (c != ',' || depth > 0) continue;
                string one = v.Substring(from, i - from).Trim();
                from = i + 1;
                List<string> mine = new List<string>();
                int open = one.IndexOf('(');
                if (open >= 0)
                {
                    int close = one.LastIndexOf(')');
                    string inner = close > open ? one.Substring(open + 1, close - open - 1) : one.Substring(open + 1);
                    foreach (string sub in inner.Split('/')) if (sub.Trim().Length > 0) mine.Add(sub.Trim());
                    one = one.Substring(0, open).Trim();
                }
                if (one.Length == 0) continue;
                words.Add(one);
                subs.Add(mine.ToArray());
            }
            if (words.Count == 0) return;
            into.Words = words.ToArray();
            into.Subs = subs.ToArray();
        }

        internal static string StatesText(Loc l)
        {
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < l.Words.Length; i++)
            {
                if (i > 0) b.Append(", ");
                b.Append(l.Words[i]);
                if (l.SubsOf(i + 1) > 0) b.Append(" (").Append(string.Join(" / ", l.Subs[i])).Append(')');
            }
            return b.ToString();
        }

        // a new sub-state of a state, from the author's window: its place's «states:» line in his own list (the built-in
        // one copied first); the rest of his list as he wrote it
        internal static string AddSub(Loc l, int state, string name)
        {
            if (!Blueprint.On) return "Немає теки " + Blueprint.Root + ".";
            name = name.Replace(",", " ").Replace("/", " ").Replace("(", " ").Replace(")", " ").Trim();
            if (l == null || state < 1 || state > l.Last || state > l.Subs.Length) return "Нема такого стану.";
            if (name.Length == 0) return "Дай субстану назву.";
            if (Array.IndexOf(l.Subs[state - 1], name) >= 0) return "Субстан «" + name + "» уже є.";
            if (!System.IO.File.Exists(AuthorFile)) System.IO.File.WriteAllText(AuthorFile, Text(All), new UTF8Encoding(false));
            List<string> subs = new List<string>(l.Subs[state - 1]) { name };
            string[][] all = (string[][])l.Subs.Clone();
            all[state - 1] = subs.ToArray();
            Loc shape = new Loc { Words = l.Words, Subs = all };
            List<string> lines = new List<string>(System.IO.File.ReadAllLines(AuthorFile, Encoding.UTF8));
            int at = -1, statesAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("loc ")) { if (at >= 0) break; if (t.Substring(4).Trim() == l.Id) at = i; continue; }
                if (at >= 0 && t.StartsWith("states:")) { statesAt = i; break; }
            }
            if (at < 0) return "У " + AuthorFile + " нема «loc " + l.Id + "».";
            string line = "states: " + StatesText(shape);
            if (statesAt >= 0) lines[statesAt] = line; else lines.Insert(at + 1, line);
            System.IO.File.WriteAllLines(AuthorFile, lines.ToArray(), new UTF8Encoding(false));
            Load();
            return "«" + l.Title + "»: новий субстан " + state + "." + subs.Count + " «" + name + "» (у стані «" + l.WordOf(state) + "»; записано в " + AuthorFile + ").";
        }

        // a new place from the author's window: a block in his own list (the built-in one copied first), round the Hall
        internal static string Add(string id, string title)
        {
            if (!Blueprint.On) return "Немає теки " + Blueprint.Root + ".";
            if (!Blueprint.GoodName(id)) return "Назва креслення: латиниця, цифри, _ чи - (наприклад torvald).";
            if (Get(id) != null) return "Локація «" + id + "» уже є.";
            if (!System.IO.File.Exists(AuthorFile)) System.IO.File.WriteAllText(AuthorFile, Text(All), new UTF8Encoding(false));
            string block = "\nloc " + id + "\nname: " + (title.Trim().Length > 0 ? title.Trim() : id) + "\nbiome: Meadows\nfrom: " + HallSpawn.Name + " 60 250\nclear: 20\nnobuild: 15\nstates: закинута\nruin: 1\nfog: no\n";
            System.IO.File.AppendAllText(AuthorFile, block, new UTF8Encoding(false));
            Load();
            return "Локацію «" + id + "» додано (поруч Зали, 60–250 м; правила — у " + AuthorFile + "). Тепер: рамка тут, будуй, зберігай.";
        }

        private static string Text(List<Loc> list)
        {
            StringBuilder b = new StringBuilder("# RavenWatch: місця сюжету (по одному в кожному світі). Див. Loc.cs — Locs.\n");
            foreach (Loc l in list)
            {
                b.Append("\nloc ").Append(l.Id).Append("\nname: ").Append(l.Title).Append("\nbiome: ").Append(l.Biome)
                 .Append("\nfrom: ").Append(l.Near.Length > 0 ? l.Near + " " + F(l.Min) + " " + F(l.Max) + (l.Edge ? " edge" : "") : "start " + F(l.Min) + " " + F(l.Max) + " " + F(l.Far))
                 .Append("\nclear: ").Append(F(l.Clear)).Append("\nnobuild: ").Append(F(l.NoBuild))
                 .Append("\nstates: ").Append(StatesText(l)).Append("\nruin: ").Append(l.RuinTo)
                 .Append("\nfog: ").Append(l.Fog ? "yes" : "no").Append(l.RuinLit.Length > 0 ? "\nruinlit: " + string.Join(" ", l.RuinLit) : "").Append(l.Sink != 0f ? "\nsink: " + F(l.Sink) : "").Append(l.LockChests ? "\nlockchests: yes" : "").Append(l.LockBeds ? "\nlockbeds: yes" : "").Append(l.Grass ? "\ngrass: yes" : l.BareRound > 0f ? "\ngrass: " + F(l.BareRound) : "").Append("\nkeys: ").Append(l.KeyBase).Append(l.Follows.Length > 0 ? "\nfollows: " + l.Follows : "").Append('\n');
                foreach (KeyValuePair<string, Color> t in l.Tints) b.Append("tint: ").Append(t.Key).Append(' ').Append(ColorUtility.ToHtmlStringRGB(t.Value)).Append('\n');
                foreach (Loc.LitStep f in l.LitFrom) b.Append("litfrom: ").Append(f.State).Append(f.Sub > 0 ? "." + f.Sub : "").Append(' ').Append(string.Join(" ", f.Prefabs)).Append('\n');
                foreach (KeyValuePair<string, string> f in l.LitWhile) b.Append("litwhile: ").Append(f.Key).Append(' ').Append(f.Value).Append('\n');
                foreach (KeyValuePair<string, string> f in l.Touches) b.Append("touch: ").Append(f.Key).Append(' ').Append(f.Value).Append('\n');
            }
            return b.ToString();
        }

        private static string F(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        private static bool TryF(string s, out float v) { return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v); }
    }
}
#endif

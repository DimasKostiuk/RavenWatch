#if RAVEN_ITEM && RAVEN_NPC
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
    // A place of the story coming into a world, on the server: its spot once a world (Seek), the places that stand by it (KinFit), built ahead of
    // anyone (BuildAhead), the author's rebuild and jobs. The whole story: the head of Loc.cs.
    internal sealed partial class Loc
    {
        // ---------------------------------------------------------------- the server: the place, once a world
        private const float FlatGen = 4f;          // m: the land as generated under it at most this far from its middle (the live check allows MaxDev 6)
        private const float AwayFromPlaces = 20f;  // m between it and a place of the world (beyond that place's own radius)
        private const float AwayFromStory = 10f;   // m between it and another place of the story (beyond both footprints)
        private const int Tries = 3000, PerFrame = 50;
        private ZNet _world;
        private bool _seeking, _gaveUp;
        private float _nextSeek;

        private ZRoutedRpc _rpcOn; // (registered with this one: once a session)
        internal void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null || _rpcOn == ZRoutedRpc.instance) return;
            _rpcOn = ZRoutedRpc.instance;
            ZRoutedRpc.instance.Register(Rpc("Rebuild"), OnRebuild);
        }

        // ---------------------------------------------------------------- the author: built again from the blueprint now in the mod
        // A place stands as its blueprint was when it was built (a world keeps it; a later blueprint does not reach it).
        // The author's «перебудувати» (the user 2026-11-01): the server takes every piece of it away (those it stamped,
        // loaded or not) and forgets it was built: it is built again where it stood (BuildAhead), from
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
        //   <id>.unlight — its hearth cold again and its first state (for tests: its fire, and Hel's mist, as at the start)
        // A world's name inside: only in that world (empty: the first world the server opens)
        private void Jobs()
        {
            if (!Blueprint.On) return;
            if (Job("unlight")) { LocHearth.SetLevel(this, LocHearth.Cold); ZoneSystem.instance.RemoveGlobalKey(KeyNext); ZoneSystem.instance.RemoveGlobalKey(KeySub); ZoneSystem.instance.SetGlobalKey(KeyState + " 1"); Log("the author's job: its hearth cold again, its first state"); }
            string move = Path.Combine(Blueprint.Root, Id + ".move"), again = Path.Combine(Blueprint.Root, Id + ".rebuild");
            bool m = File.Exists(move), r = !m && File.Exists(again);
            if (!m && !r) return;
            string world = "";
            try { world = File.ReadAllText(m ? move : again, Encoding.UTF8).Trim(); } catch (Exception e) { Log("job file: " + e.Message); }
            if (world.Length > 0 && world != ZNet.instance.GetWorldName()) return; // (meant for another world: left for it)
            try { File.Delete(m ? move : again); } catch (Exception e) { Log("job file not taken: " + e.Message); return; }
            Log("the author's job: " + (m ? "a new place" : "built again"));
            Rebuild(ZDOMan.GetSessionID(), m);
        }

        // a job file «<id>.<what>» for this world: taken (its file gone) — true; none, or another world's — false
        private bool Job(string what)
        {
            string f = Path.Combine(Blueprint.Root, Id + "." + what);
            if (!File.Exists(f)) return false;
            string world = "";
            try { world = File.ReadAllText(f, Encoding.UTF8).Trim(); } catch (Exception e) { Log("job file: " + e.Message); }
            if (world.Length > 0 && world != ZNet.instance.GetWorldName()) return false;
            try { File.Delete(f); } catch (Exception e) { Log("job file not taken: " + e.Message); return false; }
            return true;
        }

        private void Rebuild(long sender, bool move)
        {
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot) || !Shape()) { if (sender != ZDOMan.GetSessionID()) Plugin.ReplyTo(sender, Title + ": місця ще нема або нема креслення."); return; }
            int gone = RemovePieces(at);
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
            Log("taken away for building again (" + gone + " pieces)" + (move ? ", its place sought anew" : ""));
            if (sender != ZDOMan.GetSessionID() || Player.m_localPlayer != null) Plugin.ReplyTo(sender, Title + ": прибрано " + gone + " деталей. Постане знову з нового креслення.");
        }

        // every piece of it in the world, loaded or not: its own; or a blueprint's piece with no stamp standing in it (built before pieces were stamped)
        private int RemovePieces(Vector3 at)
        {
            int gone = 0;
            List<ZDO> all = new List<ZDO>();
            foreach (Vector2s z in Zones(at, Reach() + 20f))
            {
                all.Clear();
                if (FindObjects != null) FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() });
                foreach (ZDO zdo in all)
                {
                    if (zdo == null) continue;
                    string of = zdo.GetString(LocParts.KOf, "");
                    if (of != Id && !(of.Length == 0 && zdo.GetInt(Blueprint.Mark) == 1 && From(zdo.GetPosition()) < 1f)) continue;
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    ZDOMan.instance.DestroyZDO(zdo);
                    gone++;
                }
            }
            return gone;
        }

        internal void ServerTick()
        {
            if (ZNet.instance != _world) { _world = ZNet.instance; _seeking = _gaveUp = _building = _seekTight = false; _nextSeek = 0f; _read = false; _parts = null; _marks.Clear(); _kinSpots.Clear(); _kinYaw.Clear(); }
            if (_seeking || Time.time < _nextSeek || ZNet.instance == null || !ZNet.instance.IsServer() || ZoneSystem.instance == null || WorldGenerator.instance == null || ZDOMan.instance == null) return;
            _nextSeek = Time.time + 5f; // (a look every few seconds: one string asked of the world's keys)
            if (!ZoneSystem.instance.LocationsGenerated) return; // (a new world: its places first, then ours away from them)
            if (Shape()) Jobs();
            if (Shape() && Built) { KeepHeight(); return; }
            if (!Shape()) return;
            Vector3 at; Quaternion rot;
            if (Where(out at, out rot)) // (picked: it waits till someone is heading its way)
            {
                if (!_building && !_oneAtATime && SomeoneWithin(at, BuildFrom)) { _building = _oneAtATime = true; Plugin.Instance.StartCoroutine(BuildAhead(at, rot)); }
                return;
            }
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
            System.Random rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ Id.GetStableHashCode()); // (the same world, the same tries)
            int land = 0, uneven = 0, places = 0, built = 0, story = 0, crossed = 0, kin0 = 0, tried = 0;
            Vector3 found = Vector3.zero; float yaw = 0f; bool ok = false;
            string first = null; // (what the search with its room met, when it found nothing)
            ZNet world = ZNet.instance; // (the host may leave mid-search: nothing of this world written into the next)
            List<Loc> kin = new List<Loc>(); foreach (Loc k in Locs.All) { Vector3 kat; Quaternion kr; if (k != this && k.Near == Id && !k.Built && !k.Where(out kat, out kr)) kin.Add(k); } // (the places that stand by it, no spot yet: sought with it, tight with it)
            SetTight(kin, false); // (sought anew: with its room first)
            try
            {
                for (int pass = 0; pass < 2 && !ok; pass++)
                {
                    if (pass == 1)
                    {
                        if (RoomAcross <= 0f && RoomAlong <= 0f && !kin.Exists(k => k.RoomAcross > 0f || k.RoomAlong > 0f)) break; // (no room to leave out)
                        first = "with its room: tries " + tried + ", not " + Biome + " " + land + ", uneven/water " + uneven + ", a place of the world near " + places + ", of the story " + story + ", a player's building near " + built + ", crossed out " + crossed + ", no room for the places beside it " + kin0;
                        land = uneven = places = built = story = crossed = kin0 = tried = 0;
                        SetTight(kin, true);
                        rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ Id.GetStableHashCode() ^ 0x5eed); // (the same world, the same tries)
                    }
                    // the rings: the near one, land nobody has been to first; then the near one any; then the far one any
                    float far = Mathf.Max(Far, Max * 2f);
                    float[][] rings = { new[] { Min, Max, 1f }, new[] { Min, Max, 0f }, new[] { Max, far, 0f }, new[] { far, far * 2f, 0f } }; // (the last: a world with no room near — the story must still begin somewhere)
                    Loc by = Edge ? Locs.Get(Near) : null; // (counted from its walls: the middle a little further off, by both sizes)
                    float own = Reach() - GroundMargin; // (Room: its walls alone in the second pass)
                    foreach (float[] ring in rings)
                    {
                        if (ring[2] > 0f && Near.Length > 0) continue; // (beside a place of the story: its land is visited — no «nobody's been there» ring)
                        for (int i = 0; i < Tries && !ok; i++)
                        {
                            if (++tried % PerFrame == 0) { yield return null; if (ZNet.instance != world || ZoneSystem.instance == null) yield break; }
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
                            if (!KinFit(c, q, rnd)) { kin0++; continue; }
                            found = c; yaw = y; ok = true;
                        }
                        if (ok) break;
                    }
                }
                if (first != null) Log("no place with its room (" + first + ")" + (ok ? ": it stands by its walls alone in this world" : ""));
                if (ok && _seekTight) { ZoneSystem.instance.SetGlobalKey(KeyTight); foreach (Loc k in kin) ZoneSystem.instance.SetGlobalKey(k.KeyTight); }
                if (!ok)
                {
                    // no room beside the place it stands by: that one, not built yet, moves (and this with it) — never one alone
                    Loc o = Near.Length > 0 ? Locs.Get(Near) : null; Vector3 oat; Quaternion orot;
                    if (o != null && !o.Built && o.Where(out oat, out orot)) { o.CrossOut(oat, "no room for " + Id + " beside it"); yield break; }
                    _gaveUp = true;
                }
                if (!ok) { Log("no place (tries " + tried + ": not " + Biome + " " + land + ", uneven/water " + uneven + ", a place of the world near " + places + ", of the story " + story + ", a player's building near " + built + ", crossed out " + crossed + ", no room for the places beside it " + kin0 + ")"); yield break; }
                ZoneSystem.instance.SetGlobalKey(KeySpot + " " + F(found.x) + " " + F(found.z) + " " + F(yaw));
                foreach (KeyValuePair<Loc, Vector3> k in _kinSpots) // (the places that stand by it: their room kept with it, set at once)
                {
                    ZoneSystem.instance.SetGlobalKey(k.Key.KeySpot + " " + F(k.Value.x) + " " + F(k.Value.z) + " " + F(_kinYaw[k.Key]));
                    k.Key.Log("its place, with " + Id + ": " + Mathf.RoundToInt(k.Value.x) + " " + Mathf.RoundToInt(k.Value.z));
                }
                Log("its place: " + Mathf.RoundToInt(found.x) + " " + Mathf.RoundToInt(found.z) + ", " + Mathf.RoundToInt(Vector3.Distance(new Vector3(found.x, 0f, found.z), new Vector3(centre.x, 0f, centre.z))) + " m from " + (Near.Length > 0 ? Near : "the start") + " (tries " + tried + ")");
            }
            finally { _seeking = _seekTight = false; foreach (Loc k in kin) k._seekTight = false; } // (a world left, or a failure: nothing kept tight)
        }

        // ---------------------------------------------------------------- the places that stand by it, sought with it
        // A place round which others stand (the Hall: Torvald's stone) takes a spot only where there is room for each of
        // them too (the user 2026-11-01: never one without the other); their spots are set with its own.
        // the search by walls alone, for it and the places that stand by it (their room left out too, or they would not fit in its tight pass); false: none tight
        private void SetTight(List<Loc> kin, bool on)
        {
            _seekTight = on;
            if (!on) ZoneSystem.instance.RemoveGlobalKey(KeyTight);
            foreach (Loc k in kin) { k._seekTight = on; if (!on) ZoneSystem.instance.RemoveGlobalKey(k.KeyTight); }
        }

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

        // its ground's points, as generated: the corners, the middle, and a grid every Step m (cheap: a few dozen asks)
        private const float Step = 3f;
        private IEnumerable<Vector3> Points(Vector3 c, Quaternion q, float grow)
        {
            BlueprintGround.Rect r = Room;
            for (float x = r.X0 - grow; x <= r.X1 + grow + 0.01f; x += Step)
                for (float z = r.Z0 - grow; z <= r.Z1 + grow + 0.01f; z += Step)
                    yield return c + q * new Vector3(Mathf.Min(x, r.X1 + grow), 0f, Mathf.Min(z, r.Z1 + grow));
        }

        private const float GroundMargin = 6f; // (BlueprintGround eases the edge over 6 m: that land too)

        // all of it and its eased edge in its land (the corners first: most tries fail there)
        private bool Land(Vector3 c, Quaternion q)
        {
            float g = GroundMargin;
            BlueprintGround.Rect r = Room;
            Vector3[] corners = { new Vector3(r.X0 - g, 0f, r.Z0 - g), new Vector3(r.X1 + g, 0f, r.Z0 - g), new Vector3(r.X0 - g, 0f, r.Z1 + g), new Vector3(r.X1 + g, 0f, r.Z1 + g), Vector3.zero };
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
            BlueprintGround.Rect r = Room;
            float w = Mathf.Max(Mathf.Abs(r.X0), Mathf.Abs(r.X1)), l = Mathf.Max(Mathf.Abs(r.Z0), Mathf.Abs(r.Z1));
            return Mathf.Sqrt(w * w + l * l) + GroundMargin;
        }

        // another place of the story (placed already, or its blueprint known) too near: their circles apart
        private bool NearStory(Vector3 c)
        {
            foreach (Loc o in Locs.All)
            {
                Vector3 at; Quaternion r;
                if (o == this || (Edge && o.Id == Near) || !o.Shape() || !o.Where(out at, out r)) continue; // (the one it stands by: kept by Edge's own count)
                Vector3 d = at - c; d.y = 0f;
                float apart, need = Reach() + o.Reach() + AwayFromStory;
                if (Apart.TryGetValue(o.Id, out apart) || o.Apart.TryGetValue(Id, out apart)) need = Mathf.Max(need, apart); // (two the story keeps far apart: Munin's and Hugin's places)
                if (d.magnitude < need) return true;
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
                if (BlueprintGround.Outside(Room, inv * d) < own + AwayFromPlaces) return true;
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
            BlueprintGround.Rect room = Room;
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
                    if (BlueprintGround.Outside(room, inv * d) < Clear) return true;
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

        // the crossed-out places («<key>_no x/z,x/z…»): a builder found a building or bad ground there
        private HashSet<string> Crossed()
        {
            HashSet<string> set = new HashSet<string>();
            string v;
            if (ZoneSystem.instance.GetGlobalKey(KeyNo, out v)) foreach (string s in v.Split(',')) if (s.Length > 0) set.Add(s);
            return set;
        }

        private static string SpotId(Vector3 c) { return Mathf.RoundToInt(c.x) + "/" + Mathf.RoundToInt(c.z); }

        internal void CrossOut(Vector3 at, string why)
        {
            HashSet<string> no = Crossed();
            no.Add(SpotId(at));
            ZoneSystem.instance.SetGlobalKey(KeyNo + " " + string.Join(",", new List<string>(no).ToArray()));
            ZoneSystem.instance.RemoveGlobalKey(KeySpot);
            ZoneSystem.instance.RemoveGlobalKey(KeyAgain);
            foreach (Loc k in Locs.All) if (k != this && k.Near == Id && !k.Built) ZoneSystem.instance.RemoveGlobalKey(k.KeySpot); // (the places beside it go with it)
            Log("its place " + SpotId(at) + " crossed out (" + why + "): another is sought");
            _nextSeek = Time.time + 2f; // (ServerTick seeks again once the key is gone)
        }

        // ---------------------------------------------------------------- the server builds it ahead of anyone
        internal static float BuildFrom = 500f;        // m from a player: he runs about a minute to get here, the building takes seconds
        private const float ZonesWait = 60f, ZonePad = 4f, RetryIn = 30f, HeightsOff = 12f;
        private const int FramesAZone = 4;             // (frames between two zones made: the game itself makes one at a time)
        private static readonly MethodInfo PokeZone = HarmonyLib.AccessTools.Method(typeof(ZoneSystem), "PokeLocalZone");
        private static bool _oneAtATime;               // session: one place going up in the world at a time
        private bool _building;

        // the server's look every few seconds, one distance a player
        private static bool SomeoneWithin(Vector3 at, float m)
        {
            Player me = Player.m_localPlayer;
            if (me != null && Utils.DistanceXZ(me.transform.position, at) < m) return true;
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) if (peer.IsReady() && Utils.DistanceXZ(peer.GetRefPos(), at) < m) return true;
            return false;
        }

        // a place built before its key held its origin: the server writes it once, from its own pieces as soon as some are loaded (each
        // knows its place in the blueprint: LocParts.Place) — exact, no terrain asked; the middle of a few, so one piece moved by hand does not lead
        private void KeepHeight()
        {
            if (HeightKnown) return;
            Vector3 was; Quaternion rot;
            if (!Origin(out was, out rot) || !SomeoneWithin(was, Reach() + 40f)) return; // (its pieces load round a player: no search till one is there)
            List<float> o = new List<float>();
            foreach (ZNetView nv in LocParts.All(Id)) // (the shared look: nothing new searched)
            {
                string of; Vector3 local;
                if (o.Count >= 9) break;
                if (nv.GetComponent<Rigidbody>() == null && LocParts.Place(nv, out of, out local) && of == Id) o.Add(nv.transform.position.y - local.y); // (its turn is a yaw: height is untouched by it)
            }
            if (o.Count == 0) return;
            o.Sort();
            float origin = o[o.Count / 2];
            SetHeight(origin + _ground, origin); // (BuildAhead's two words: ground less Sink is its origin + its blueprint's ground)
            Log("its origin " + origin.ToString("0.00") + " from its own pieces (built before its key held it): its shots and spots were read " + (was.y - origin).ToString("0.00") + " m off till now");
        }

        private IEnumerator BuildAhead(Vector3 at, Quaternion rot)
        {
            ZNet world = ZNet.instance;
            try
            {
                if (PokeZone == null) { Log("the game changed (PokeLocalZone): it cannot be built"); _nextSeek = Time.time + RetryIn; yield break; }
                if (!Again && NearBuilding(at, rot, true)) { CrossOut(at, "a player's building near"); yield break; } // (the server has every building of the world, loaded or not)
                // its zones in, one every few frames, and kept in till the ground is levelled (the game lets a zone go 4 s after the last poke)
                List<Vector2s> zones = new List<Vector2s>(Zones(at, Reach() + ZonePad));
                float giveUp = Time.time + ZonesWait;
                while (true)
                {
                    if (ZNet.instance != world || ZoneSystem.instance == null) yield break;
                    bool made = false, ready = true;
                    foreach (Vector2s z in zones)
                    {
                        if ((bool)PokeZone.Invoke(ZoneSystem.instance, new object[] { z })) { made = true; ready = false; break; }
                        if (!ZoneSystem.instance.IsZoneLoaded(z)) ready = false;
                    }
                    if (ready) break;
                    if (Time.time > giveUp) { Log("its zones did not load in " + ZonesWait + " s: tried again later"); _nextSeek = Time.time + RetryIn; yield break; }
                    for (int f = made ? FramesAZone : 1; f > 0; f--) yield return null;
                }
                while (Blueprint.Placing) yield return null; // (the author's own going up)
                if (ZNet.instance != world || ZoneSystem.instance == null) yield break;
                foreach (Vector2s z in zones) PokeZone.Invoke(ZoneSystem.instance, new object[] { z });
                BlueprintGround.Fit fit = BlueprintGround.Check(Room, at, rot);
                if (!fit.Ok) { if (fit.Seen) CrossOut(at, "ground: " + fit.Why); else { Log("its ground not read yet: " + fit.Why); _nextSeek = Time.time + RetryIn; } yield break; }
                float land = WorldGenerator.instance.GetHeight(at.x, at.z);
                if (Mathf.Abs(fit.Ground - land) > HeightsOff) { Log("its ground reads " + fit.Ground.ToString("0.0") + ", the land says " + land.ToString("0.0") + ": tried again later"); _nextSeek = Time.time + RetryIn; yield break; }
                int old = RemovePieces(at); // (a build cut off before it was finished: never two of it)
                if (old > 0) Log("took away " + old + " pieces of a build that never finished");
                at.y = fit.Ground - _ground - Sink; // (the ground levelled where it was; the building into it by Sink)
                SetHeight(fit.Ground - Sink, at.y); // (Where, Origin: everyone reads where it was built, not the land)
                Log("its height " + (fit.Ground - Sink).ToString("0.00") + " (the land at its middle " + land.ToString("0.00") + ")");
                string ground = BlueprintGround.Level(Room, at, rot, fit.Ground, !Grass, Floors(), BareRound); // (all its ground flat, its room too; earth only under its floors, or within BareRound m of its middle: grass round it)
                BlueprintGround.Forget(); // (the world's own building: no undo of its ground)
                Log("building at " + SpotId(at) + ": " + ground);
                bool done = false;
                Blueprint.Spawn(_parts, _marks, at, rot, Id, IsLandmark, (made, missing) =>
                {
                    done = true;
                    Log("it stands: " + made + " pieces" + (missing > 0 ? ", " + missing + " unknown to this game" : ""));
                    if (ZNet.instance != world || ZoneSystem.instance == null) return;
                    ZoneSystem.instance.SetGlobalKey(KeyBuilt);
                    ZoneSystem.instance.RemoveGlobalKey(KeyAgain);
                    Log("built (" + made + " pieces)");
                });
                while (!done && Blueprint.Placing && ZNet.instance == world) yield return null;
                if (!done) { Log("its building broke off: taken away and tried again in " + RetryIn + " s"); _nextSeek = Time.time + RetryIn; }
            }
            finally { _building = false; _oneAtATime = false; }
        }

        // seen from far: what stands on its outside and its roofs; what is inside waits for you to come (each landmark costs a body in the far ring)
        private const float OuterWithin = 4f;
        private bool IsLandmark(Blueprint.Part pt)
        {
            float edge = Mathf.Min(Mathf.Min(pt.Pos.x - _rect.X0, _rect.X1 - pt.Pos.x), Mathf.Min(pt.Pos.z - _rect.Z0, _rect.Z1 - pt.Pos.z));
            return edge <= OuterWithin || LocFog.IsRoof(pt.Prefab);
        }

    }
}
#endif

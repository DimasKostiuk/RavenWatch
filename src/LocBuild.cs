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
    // A place of the story coming into a world, on the server: its spot once a world (Seek), the places that stand by its walls (KinFit), built ahead of
    // anyone (BuildAhead), the author's rebuild and jobs. The whole story: the head of Loc.cs.
    // Two kinds of «from: <another place>» (the user 2026-11-21): one by its walls («edge»: Torvald's camp by the Hall) is sought WITH that place — a
    // spot is taken only where both fit, never one without the other; any other (the post, Eyvind's hut: hundreds of m off, another land) is sought
    // AFTER it, by its own Seek with every pass and ring, and never moves it. One search at a time in a world (_seekingAny): the next one sees where
    // the last one stands, so «apart:» and the circles hold between places sought in the same minute.
    internal sealed partial class Loc
    {
        // ---------------------------------------------------------------- the server: the place, once a world
        private const float FlatGen = 4f;          // m: the land as generated under it at most this far from its middle (the live check allows MaxDev 6)
        private const float AwayFromPlaces = 20f;  // m between it and a place of the world (beyond that place's own radius)
        private const float AwayFromStory = 10f;   // m between it and another place of the story (beyond both footprints)
        private const int Tries = 3000, PerFrame = 50;
        private const long FrameMs = 4;            // ms of one frame a search takes at most (a try that asks the world's objects, or the places by its walls, is dear: the frame ends early)
        private ZNet _world;
        private bool _seeking, _gaveUp;
        private static bool _seekingAny; // session: one place sought at a time in a world — the next sees the last one's spot (apart, the circles)
        private float _nextSeek;

        private ZRoutedRpc _rpcOn; // (registered with this one: once a session)
        internal void RegisterRpcs()
        {
            if (ZRoutedRpc.instance == null || _rpcOn == ZRoutedRpc.instance) return;
            _rpcOn = ZRoutedRpc.instance;
            ZRoutedRpc.instance.Register(Rpc("Rebuild"), OnRebuild);
        }

        // ---------------------------------------------------------------- the author: built again from the blueprint now in the mod
        // A place stands as its blueprint was when it was built (a world keeps it); a later blueprint's new pieces come in once (FillOnce), nothing is taken away.
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
            int inside = 0;
#if RAVEN_STORY
            inside = LocInside.TakeWhole(this, null); // (its inside whole, before its key goes: the rooms and spawners over it are no pieces of its own)
#endif
            int gone = RemovePieces(at);
            ZoneSystem.instance.RemoveGlobalKey(KeyBuilt);
            ZoneSystem.instance.RemoveGlobalKey(KeyInside); // (its inside went with its pieces: put anew once it stands again — LocInside.ServerTick)
            if (move) ZoneSystem.instance.RemoveGlobalKey(KeyAgain); else ZoneSystem.instance.SetGlobalKey(KeyAgain);
            if (move)
            {
                CrossOut(at, "moved by the author"); // (the search goes by the world's seed: without this it found the very same spot again — the user 2026-11-15)
                _gaveUp = false; _nextSeek = Time.time + 2f;
                // the places beside it go along: built ones taken away too, all sought again with it (never one left alone)
                foreach (Loc k in Locs.All)
                {
                    if (k == this || k.Near != Id) continue;
                    if (k.Built) k.Rebuild(ZDOMan.GetSessionID(), true);
                    else ZoneSystem.instance.RemoveGlobalKey(k.KeySpot);
                }
            }
            Log("taken away for building again (" + gone + " pieces" + (inside > 0 ? ", its inside " + inside + " things" : "") + ")" + (move ? ", its place sought anew" : ""));
            if (sender != ZDOMan.GetSessionID() || Player.m_localPlayer != null) Plugin.ReplyTo(sender, Title + ": прибрано " + gone + " деталей. Постане знову з нового креслення.");
        }

        // every piece of it in the world, loaded or not: its own; or a blueprint's piece with no stamp standing in it (built before pieces were stamped)
        private int RemovePieces(Vector3 at)
        {
            int gone = 0;
            List<ZDO> all = new List<ZDO>();
            float reach = Reach() + 20f;
#if RAVEN_STORY
            if (Inside.Length > 0) reach = Mathf.Max(reach, LocInside.Span(Inside)); // (its inside hangs over it, in the same zones: taken away with it)
#endif
            foreach (Vector2s z in Zones(at, reach))
            {
                all.Clear();
                if (FindObjects != null) FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() });
                foreach (ZDO zdo in all)
                {
                    if (zdo == null) continue;
                    string of = zdo.GetString(LocParts.KOf, "");
                    bool mine = of == Id || (of.Length == 0 && zdo.GetInt(Blueprint.Mark) == 1 && From(zdo.GetPosition()) < 1f && !Character.InInterior(zdo.GetPosition())); // (not a marked piece of an inside that hangs over it: by the map it stands «in» the place)
#if RAVEN_STORY
                    mine |= Inside.Length > 0 && LocInside.Marked(zdo, Inside); // (its own inside: the rooms, the things, the author's pieces)
#endif
                    if (!mine) continue;
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
            if (Shape() && Built) { KeepHeight(); WildOnce(); FillOnce(); return; }
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
            if (_seekingAny) { _nextSeek = Time.time + 0.5f; return; } // (another place is being sought: this one right after it, knowing where that one stands — a new world's places by the start are wanted in its first minute)
            _seeking = _seekingAny = true;
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
            List<Vector3> no = Crossed();
            System.Random rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ Id.GetStableHashCode()); // (the same world, the same tries)
            int land = 0, uneven = 0, places = 0, built = 0, story = 0, crossed = 0, kin0 = 0, tried = 0;
            Vector3 found = Vector3.zero; float yaw = 0f; bool ok = false;
            string first = null; // (what the search with its room met, when it found nothing)
            ZNet world = ZNet.instance; // (the host may leave mid-search: nothing of this world written into the next)
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew(); long msAll = 0, msWorst = 0; int frames = 1, looks0 = WorldSpot.BuiltLooks; // (what the search cost: its log says it)
            List<Loc> kin = new List<Loc>(); foreach (Loc k in Locs.All) { Vector3 kat; Quaternion kr; if (k != this && k.Edge && k.Near == Id && !k.Built && !k.Where(out kat, out kr)) kin.Add(k); } // (the places that stand by its walls, no spot yet: sought with it, tight with it, in its land's pass)
            SetTight(kin, false); // (sought anew: with its room first)
            try
            {
                for (int pass = 0; pass < 4 && !ok; pass++)
                {
                    // the last two passes: none of its own land within reach, with its room or without — first the calm lands (the Meadows and the Black
                    // Forest, mixed too: the user 2026-11-21 — never a swamp or a mountain while those are there), then any dry, even land (the user
                    // 2026-11-15: a place the story leads to must stand in every world; a world with no Black Forest for three and a half km round the
                    // start still gets its barrow). The places by its walls go by the same pass (KinFit)
                    if (pass >= 2)
                    {
                        Log("no " + (pass == 2 ? Biome.ToString() : "calm land") + " for it within reach (tries " + tried + ", not that land " + land + ", uneven/water " + uneven + ", a place of the world near " + places + "): sought once more in " + (pass == 2 ? "the Meadows and the Black Forest" : "any land"));
                        land = uneven = places = built = story = crossed = kin0 = tried = 0;
                        _seekLand = pass - 1;
                        rnd = new System.Random(WorldGenerator.instance.GetSeed() ^ Id.GetStableHashCode() ^ (pass == 2 ? 0xca1 : 0xa11));
                    }
                    if (pass == 1)
                    {
                        if (RoomAcross <= 0f && RoomAlong <= 0f && !kin.Exists(k => k.RoomAcross > 0f || k.RoomAlong > 0f)) continue; // (no room to leave out: on to the last pass)
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
                        if (ring[2] > 0f && (Near.Length > 0 || Nearest)) continue; // (beside a place of the story: its land is visited — no «nobody's been there» ring; «nearest» is the nearest such land, been to or not)
                        for (int i = 0; i < Tries && !ok; i++)
                        {
                            if (++tried % PerFrame == 0 || sw.ElapsedMilliseconds >= FrameMs)
                            {
                                long ms = sw.ElapsedMilliseconds; msAll += ms; if (ms > msWorst) msWorst = ms; frames++;
                                yield return null;
                                sw.Reset(); sw.Start();
                                if (ZNet.instance != world || ZoneSystem.instance == null) yield break;
                            }
                            float lo = ring[0], hi = ring[1];
                            if (by != null) { lo += own; hi += own + by.Reach(); }
                            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = lo + (Nearest ? (float)i / Tries : (float)rnd.NextDouble()) * (hi - lo); // («nearest»: outward step by step, a random side each — the first fit is about the nearest)
                            Vector3 c = new Vector3(centre.x + Mathf.Cos(ang) * r, 0f, centre.z + Mathf.Sin(ang) * r);
                            if (by != null) { float gap = by.From(c) - own; if (gap < ring[0] || gap > ring[1]) { story++; continue; } }
                            Vector3 face = centre - c; face.y = 0f;
                            float y = Quaternion.LookRotation(face.normalized, Vector3.up).eulerAngles.y; // (its front to the start, or to the place it stands by: the way people come)
                            Quaternion q = Quaternion.Euler(0f, y, 0f);
                            if (IsCrossed(no, c)) { crossed++; continue; }
                            if (!Land(c, q)) { land++; continue; }
                            if (!Even(c, q)) { uneven++; continue; }
                            if (ring[2] > 0f && !Fresh(c)) continue;
                            if (NearStory(c)) { story++; continue; }
                            if (NearPlace(c, q)) { places++; continue; }
                            if (NearBuilding(c, q)) { built++; continue; }
                            if (!KinFit(c, q, rnd)) { kin0++; continue; }
                            found = c; yaw = y; ok = true;
                        }
                        if (ok) break;
                    }
                }
                msAll += sw.ElapsedMilliseconds; if (sw.ElapsedMilliseconds > msWorst) msWorst = sw.ElapsedMilliseconds;
                string cost = "; cost: " + frames + " frames, " + msAll + " ms in all, the longest frame " + msWorst + " ms, zones looked through for buildings " + (WorldSpot.BuiltLooks - looks0);
                if (first != null) Log("no place with its room (" + first + ")" + (ok ? ": it stands by its walls alone in this world" : ""));
                if (ok && _seekTight) { ZoneSystem.instance.SetGlobalKey(KeyTight); foreach (Loc k in kin) ZoneSystem.instance.SetGlobalKey(k.KeyTight); }
                if (!ok)
                {
                    // no room by the walls of the place it stands by: that one, not built yet, moves (and this with it) — never one alone. A place
                    // sought after another (no «edge») never moves it: its own passes and rings were its every chance
                    Loc o = Edge && Near.Length > 0 ? Locs.Get(Near) : null; Vector3 oat; Quaternion orot;
                    if (o != null && !o.Built && o.Where(out oat, out orot)) { o.CrossOut(oat, "no room for " + Id + " beside it"); yield break; }
                    _gaveUp = true;
                }
                if (!ok) { Log("no place (tries " + tried + ": not " + Biome + " " + land + ", uneven/water " + uneven + ", a place of the world near " + places + ", of the story " + story + ", a player's building near " + built + ", crossed out " + crossed + ", no room for the places beside it " + kin0 + ")" + cost); yield break; }
                ZoneSystem.instance.SetGlobalKey(KeySpot + " " + F(found.x) + " " + F(found.z) + " " + F(yaw));
                foreach (KeyValuePair<Loc, Vector3> k in _kinSpots) // (the places that stand by it: their room kept with it, set at once)
                {
                    ZoneSystem.instance.SetGlobalKey(k.Key.KeySpot + " " + F(k.Value.x) + " " + F(k.Value.z) + " " + F(_kinYaw[k.Key]));
                    k.Key.Log("its place, with " + Id + ": " + Mathf.RoundToInt(k.Value.x) + " " + Mathf.RoundToInt(k.Value.z));
                }
                Log("its place: " + Mathf.RoundToInt(found.x) + " " + Mathf.RoundToInt(found.z) + ", " + Mathf.RoundToInt(Vector3.Distance(new Vector3(found.x, 0f, found.z), new Vector3(centre.x, 0f, centre.z))) + " m from " + (Near.Length > 0 ? Near : "the start") + " (tries " + tried + ")" + cost);
            }
            finally { _seeking = _seekingAny = _seekTight = false; _seekLand = 0; foreach (Loc k in kin) { k._seekTight = false; k._seekLand = 0; } } // (a world left, or a failure: nothing kept tight)
        }

        // ---------------------------------------------------------------- the places that stand by its walls, sought with it
        // A place by whose walls others stand («edge» — the Hall: Torvald's stone) takes a spot only where there is room for each of
        // them too (the user 2026-11-01: never one without the other); their spots are set with its own. They keep off one another as any two
        // places do (Needs: the circles, «apart:»), and go by its pass of lands (_seekLand). A place «from» it with no «edge» is not of these:
        // it is sought after it, on its own (the head of this file).
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
                if (k == this || !k.Edge || k.Near != Id || !k.Shape() || k.Where(out at, out r)) continue; // (not by its walls, no blueprint yet, or placed already)
                Vector3 kc; float ky;
                k._seekLand = _seekLand; // (its pass of lands: a camp by a hall that stands in «any land» stands in it too)
                if (!k.TryBeside(this, c, q, rnd, _kinSpots, out kc, out ky)) return false;
                _kinSpots.Add(new KeyValuePair<Loc, Vector3>(k, kc));
                _kinYaw[k] = ky;
            }
            return true;
        }

        // a spot for this one by the walls of «by», were «by» standing at (at, rot): its own rules (its ring between the walls of the two, «nearest»
        // from the inner edge out, as Seek), but no waiting for the other; «beside»: those picked by «by» in the same try already — kept off as any place
        internal bool TryBeside(Loc by, Vector3 at, Quaternion rot, System.Random rnd, List<KeyValuePair<Loc, Vector3>> beside, out Vector3 found, out float yaw)
        {
            found = Vector3.zero; yaw = 0f;
            List<Vector3> no = Crossed();
            float own = Reach() - GroundMargin;
            for (int i = 0; i < KinTries; i++)
            {
                float lo = Min + own, hi = Max + own + by.Reach();
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, r = lo + (Nearest ? (float)i / KinTries : (float)rnd.NextDouble()) * (hi - lo);
                Vector3 c = new Vector3(at.x + Mathf.Cos(ang) * r, 0f, at.z + Mathf.Sin(ang) * r);
                float gap = by.FromAt(c, at, rot) - own;
                if (gap < Min || gap > Max) continue;
                bool taken = false;
                foreach (KeyValuePair<Loc, Vector3> b in beside) { Vector3 d = b.Value - c; d.y = 0f; if (d.magnitude < Needs(b.Key)) { taken = true; break; } } // flat: two places of the land, on the map
                if (taken) continue;
                Vector3 face = at - c; face.y = 0f;
                float y = Quaternion.LookRotation(face.normalized, Vector3.up).eulerAngles.y;
                Quaternion q = Quaternion.Euler(0f, y, 0f);
                if (IsCrossed(no, c) || !Land(c, q) || !Even(c, q) || NearStory(c) || NearPlace(c, q) || NearBuilding(c, q)) continue;
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
        private int _seekLand; // session: the search's pass of lands — 0 its own land; 1 the calm ones (the Meadows and the Black Forest, mixed too); 2 any
        private bool LandFits(Heightmap.Biome b) { return _seekLand == 0 ? b == Biome : b == Heightmap.Biome.Meadows || b == Heightmap.Biome.BlackForest; }
        private bool Land(Vector3 c, Quaternion q)
        {
            if (_seekLand >= 2) return true; // (dry and even is asked next: Even)
            float g = GroundMargin;
            BlueprintGround.Rect r = Room;
            Vector3[] corners = { new Vector3(r.X0 - g, 0f, r.Z0 - g), new Vector3(r.X1 + g, 0f, r.Z0 - g), new Vector3(r.X0 - g, 0f, r.Z1 + g), new Vector3(r.X1 + g, 0f, r.Z1 + g), Vector3.zero };
            foreach (Vector3 k in corners) { Vector3 p = c + q * k; if (!LandFits(WorldGenerator.instance.GetBiome(p.x, p.z))) return false; }
            foreach (Vector3 p in Points(c, q, g)) if (!LandFits(WorldGenerator.instance.GetBiome(p.x, p.z))) return false;
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
                if (d.magnitude < Needs(o)) return true;
            }
            if (_foundsAt != Time.frameCount) { _foundsAt = Time.frameCount; _founds.Clear(); StoryFound.AllFound(_founds); } // (once a frame: a seek asks it of many spots)
            foreach (Vector3 f in _founds) { Vector3 d = f - c; d.y = 0f; if (d.magnitude < Reach() + AwayFromFound) return true; }
            return false;
        }
        // how far its middle keeps from another place's: both circles and AwayFromStory, or what «apart:» says of the two, either way (two the
        // story keeps far apart: Munin's and Hugin's places) — one rule for a place that stands (NearStory) and one picked in the same try (TryBeside)
        private float Needs(Loc o)
        {
            float apart, need = Reach() + o.Reach() + AwayFromStory;
            if (Apart.TryGetValue(o.Id, out apart) || o.Apart.TryGetValue(Id, out apart)) need = Mathf.Max(need, apart);
            return need;
        }
        // a place the story found in the world already (a glade, a forest's edge: found.*) — a place placed later keeps off it too (the user 2026-11-18:
        // «щоб всі наші локації не були в одній точці»; the glades keep StoryFound.BaseClear off the places the other way round)
        private const float AwayFromFound = 60f;
        private static readonly List<Vector3> _founds = new List<Vector3>(); // session: this frame's found places
        private static int _foundsAt = -1;

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
        // the zones round it); what is «someone's» — the one answer for all who ask: WorldSpot.Someones
        private static readonly MethodInfo FindObjects = HarmonyLib.AccessTools.Method(typeof(ZDOMan), "FindObjects");
        private bool NearBuilding(Vector3 c, Quaternion q)
        {
            Quaternion inv = Quaternion.Inverse(q);
            BlueprintGround.Rect room = Room;
            foreach (Vector2s z in Zones(c, Reach() + Clear))
                foreach (Vector3 p in WorldSpot.BuiltIn(z)) // (kept for the frame: the tries of one frame share their zones)
                {
                    Vector3 d = p - c; d.y = 0f;
                    if (BlueprintGround.Outside(room, inv * d) < Clear) return true;
                }
            return false;
        }

        // the crossed-out places («<key>_no x/z,x/z…»): a builder found a building or bad ground there. Each takes the land round it too
        // (CrossedPart of its reach: the user 2026-11-21 — a spot a few m off stood on the same bad ground and was turned down the same way)
        private const float CrossedPart = 0.5f;
        private List<Vector3> Crossed()
        {
            List<Vector3> l = new List<Vector3>();
            string v;
            if (!ZoneSystem.instance.GetGlobalKey(KeyNo, out v) || v == null) return l;
            foreach (string s in v.Split(','))
            {
                string[] xz = s.Split('/');
                float x, z;
                if (xz.Length == 2 && TryF(xz[0], out x) && TryF(xz[1], out z)) l.Add(new Vector3(x, 0f, z));
            }
            return l;
        }
        private bool IsCrossed(List<Vector3> no, Vector3 c)
        {
            float r = Reach() * CrossedPart;
            foreach (Vector3 n in no) { float dx = n.x - c.x, dz = n.z - c.z; if (dx * dx + dz * dz < r * r) return true; }
            return false;
        }

        private static string SpotId(Vector3 c) { return Mathf.RoundToInt(c.x) + "/" + Mathf.RoundToInt(c.z); }

        internal void CrossOut(Vector3 at, string why)
        {
            string was;
            if (!ZoneSystem.instance.GetGlobalKey(KeyNo, out was) || was == null) was = "";
            was = was.Trim();
            if (Array.IndexOf(was.Split(','), SpotId(at)) < 0) was = was.Length > 0 ? was + "," + SpotId(at) : SpotId(at);
            ZoneSystem.instance.SetGlobalKey(KeyNo + " " + was);
            ZoneSystem.instance.RemoveGlobalKey(KeySpot);
            ZoneSystem.instance.RemoveGlobalKey(KeyAgain);
            foreach (Loc k in Locs.All) if (k != this && k.Near == Id && !k.Built) ZoneSystem.instance.RemoveGlobalKey(k.KeySpot); // (the places beside it go with it)
            Log("its place " + SpotId(at) + " crossed out (" + why + "): another is sought");
            _nextSeek = Time.time + 2f; // (ServerTick seeks again once the key is gone)
        }

        // ---------------------------------------------------------------- the server builds it ahead of anyone
        internal static float BuildFrom = 500f;        // m from a player: he runs about a minute to get here, the building takes seconds
        private const float ZonesWait = 60f, ZonePad = 4f, RetryIn = 30f, HeightsOff = 12f;
        private static bool _oneAtATime;               // session: one place going up in the world at a time
        private bool _building;

        // the server's look every few seconds, one distance a player (a place built ahead; a glade's land made ahead — StoryFound)
        internal static bool SomeoneWithin(Vector3 at, float m)
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

        // a place built before Wild: its clearing let pass the game's plain bushes and every bush another mod makes a piece (PlantEverything;
        // the user 2026-11-08: bushes inside the Hall and the ghost's house). The same clearing once more, once a world, from the server's
        // own list of the world's objects: no body loaded, no player near needed
        private void WildOnce()
        {
            if (ZoneSystem.instance.GetGlobalKey(KeyWild)) return;
            Vector3 at; Quaternion rot;
            if (!Where(out at, out rot)) return;
            int n = BlueprintGround.Clear(Room, at, rot); // heavy: once a world a place (the objects of its few zones)
            ZoneSystem.instance.SetGlobalKey(KeyWild);
            Log("its ground cleared of the wild by the new rule: " + n + " taken away");
        }

        // A place built from an older blueprint (the user 2026-11-15: «щоб зала оновлювалась один раз у версії 3.0 — одна перевірка і все»): the
        // pieces its blueprint has now and the world has not (the bench of «обжита_гейд», any later one) put in — once a world for each blueprint
        // (KeyFill: its parts' hash; a later blueprint, once more). Nothing taken away: what stands stays as it is (the Hall's chests with a
        // player's things in them, a player's own building — a rebuild would lose them). A piece is known by its stamp (LocParts: which place,
        // where in its blueprint) and its prefab, within FillNear m. One missing whose prefab stands within FillMoved m (in its blueprint's frame) as a
        // piece of it its blueprint no longer has: moved there, not doubled (a chest with a player's things goes with them). Fewer than FillKnown of its blueprint's pieces known (built before pieces
        // were stamped): nothing put in — it would be a second building over the first — said in the log (the author's «перебудувати» then).
        // While someone is near (its zones loaded, as KeepHeight); the pieces go up as at its building (Blueprint.Spawn: stamped, so a state's
        // piece shows only in its state). Cost: once a world a blueprint — the objects of its few zones, a look each (heavy: as RemovePieces).
        internal static float FillNear = 0.06f, FillKnown = 0.5f, FillMoved = 1.5f;
        private bool _filling;
        private int _partsHash;
        private void FillOnce()
        {
            if (_filling || _building || _oneAtATime || Blueprint.Placing || _parts == null || _parts.Count == 0) return;
            if (_partsHash == 0) { StringBuilder sb = new StringBuilder(); foreach (Blueprint.Part p in _parts) sb.Append(p.Prefab).Append(' ').Append(p.Pos.x.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ').Append(p.Pos.y.ToString("0.00", CultureInfo.InvariantCulture)).Append(' ').Append(p.Pos.z.ToString("0.00", CultureInfo.InvariantCulture)).Append(';'); _partsHash = sb.ToString().GetStableHashCode(); if (_partsHash == 0) _partsHash = 1; }
            string key = KeyFill(_partsHash);
            if (ZoneSystem.instance.GetGlobalKey(key)) return;
            Vector3 at; Quaternion rot;
            if (!Origin(out at, out rot) || !SomeoneWithin(at, Reach() + 40f)) return;
            Dictionary<int, List<KeyValuePair<Vector3, ZDO>>> have = new Dictionary<int, List<KeyValuePair<Vector3, ZDO>>>();
            List<ZDO> all = new List<ZDO>();
            foreach (Vector2s z in Zones(at, Reach() + 20f))
            {
                all.Clear();
                if (FindObjects != null) FindObjects.Invoke(ZDOMan.instance, new object[] { z, all, new HashSet<ZoneSystem.SectorIndex>() }); // heavy: once a world a blueprint
                foreach (ZDO zdo in all)
                {
                    if (zdo == null || zdo.GetString(LocParts.KOf, "") != Id) continue;
                    List<KeyValuePair<Vector3, ZDO>> l;
                    if (!have.TryGetValue(zdo.GetPrefab(), out l)) have[zdo.GetPrefab()] = l = new List<KeyValuePair<Vector3, ZDO>>();
                    l.Add(new KeyValuePair<Vector3, ZDO>(zdo.GetVec3(LocParts.KAt, new Vector3(float.MaxValue, 0f, 0f)), zdo));
                }
            }
            List<Blueprint.Part> missing = new List<Blueprint.Part>();
            HashSet<ZDO> matched = new HashSet<ZDO>();
            foreach (Blueprint.Part p in _parts)
            {
                List<KeyValuePair<Vector3, ZDO>> l;
                bool found = false;
                if (have.TryGetValue(p.Prefab.GetStableHashCode(), out l)) foreach (KeyValuePair<Vector3, ZDO> v in l) if ((v.Key - p.Pos).sqrMagnitude < FillNear * FillNear) { found = true; matched.Add(v.Value); break; }
                if (!found) missing.Add(p);
            }
            int known = _parts.Count - missing.Count;
            // a piece of it that its blueprint no longer has, by one missing of its prefab: moved there (the server owns it for the move)
            int moved = 0;
            for (int i = missing.Count - 1; i >= 0 && known >= _parts.Count * FillKnown; i--)
            {
                Blueprint.Part p = missing[i];
                List<KeyValuePair<Vector3, ZDO>> l;
                if (!have.TryGetValue(p.Prefab.GetStableHashCode(), out l)) continue;
                ZDO best = null; float bd = FillMoved * FillMoved;
                foreach (KeyValuePair<Vector3, ZDO> v in l) { float d = (v.Key - p.Pos).sqrMagnitude; if (d < bd && !matched.Contains(v.Value)) { bd = d; best = v.Value; } }
                if (best == null) continue;
                matched.Add(best);
                best.SetOwner(ZDOMan.GetSessionID());
                best.SetPosition(at + rot * p.Pos);
                best.SetRotation(rot * p.Rot);
                LocParts.Stamp(best, Id, p.Pos);
                missing.RemoveAt(i);
                moved++;
            }
            if (moved > 0) Log("built from an older blueprint: " + moved + " piece(s) moved where its blueprint has them now (seen so once they load anew)");
            if (known < _parts.Count * FillKnown)
            {
                ZoneSystem.instance.SetGlobalKey(key); // (said once a world a blueprint, not every few seconds)
                Log("its blueprint has " + _parts.Count + " pieces, " + known + " of them known here by their stamp: built before pieces were stamped — nothing put in (the author's «перебудувати» builds it anew)");
                return;
            }
            if (missing.Count == 0) { ZoneSystem.instance.SetGlobalKey(key); return; }
            ZNet world = ZNet.instance;
            _filling = _oneAtATime = true;
            Log("built from an older blueprint: " + missing.Count + " of its " + _parts.Count + " pieces missing here — put in");
            Blueprint.Spawn(missing, new List<Blueprint.Spot>(), at, rot, Id, IsLandmark, (made, unknown) =>
            {
                _filling = _oneAtATime = false;
                if (ZNet.instance != world || ZoneSystem.instance == null) return;
                ZoneSystem.instance.SetGlobalKey(key);
                Log("the pieces its blueprint has now put in: " + made + (unknown > 0 ? ", " + unknown + " unknown to this game" : ""));
            });
        }

        private IEnumerator BuildAhead(Vector3 at, Quaternion rot)
        {
            ZNet world = ZNet.instance;
            try
            {
                if (!Again && NearBuilding(at, rot)) { CrossOut(at, "a player's building near"); yield break; } // (the server has every building of the world, loaded or not)
                // its zones in, and kept in till the ground is levelled (BlueprintGround.ZonesIn: the one way land is made far from every player)
                List<Vector2s> zones = new List<Vector2s>(Zones(at, Reach() + ZonePad));
                int came = BlueprintGround.ZonesGone;
                yield return BlueprintGround.ZonesIn(zones, ZonesWait, v => came = v);
                if (came == BlueprintGround.ZonesNoGame) { Log("the game changed (PokeLocalZone): it cannot be built"); _nextSeek = Time.time + RetryIn; yield break; }
                if (came == BlueprintGround.ZonesLate) { Log("its zones did not load in " + ZonesWait + " s: tried again later"); _nextSeek = Time.time + RetryIn; yield break; }
                if (came != BlueprintGround.ZonesOk) yield break;
                while (Blueprint.Placing) yield return null; // (the author's own going up)
                if (ZNet.instance != world || ZoneSystem.instance == null) yield break;
                BlueprintGround.KeepIn(zones);
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
                ZoneSystem.instance.SetGlobalKey(KeyWild); // (cleared by Wild's answer: WildOnce has nothing to do)
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

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The small shared tools of every part of a person (npc-plan.md: one core, parts on it), written once:
    //   Ref / Parse                     a link for now (an order, whom it looks at): "user:id", good till the world is closed
    //   Save / Resolve / GetRef / SetRef a lasting link (kept in saves: its heart, bed, chest, locker) — see «lasting links»
    //   IdOf                            a thing's own link
    //   Nearest<T>                      the nearest built thing of a kind round a spot (the bed / chest / bench you point at)
    //   ByFire / FireSide               a spot two steps from the nearest burning fire it can walk up to, on its floor
    //   Busy                            why an NPC cannot be changed from this game now (another game runs it), or null
    //   Floor                           the floor under a spot (a building's floor, the ground) — never the roof over it
    internal static class NpcKit
    {
        internal static readonly int Built = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small");

        internal static string Ref(ZDOID id) { return id.UserID + ":" + id.ID; }

        internal static ZDOID Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return ZDOID.None;
            int c = s.IndexOf(':');
            long u; uint i;
            return c > 0 && long.TryParse(s.Substring(0, c), out u) && uint.TryParse(s.Substring(c + 1), out i) ? new ZDOID(u, i) : ZDOID.None;
        }

        internal static ZDOID GetRef(ZDO z, int key) { return z != null ? Resolve(z.GetString(key, "")) : ZDOID.None; }

        // it has a link there at all — no look-up (a bed not loaded on this game is its bed all the same)
        internal static bool HasRef(ZDO z, int key) { return z != null && z.GetString(key, "").Length > 0; }

        internal static void SetRef(ZDO z, int key, ZDOID id) { if (z != null) z.Set(key, Save(id)); }

        // ---------------------------------------------------------------- lasting links (kept in saves)
        // The game numbers every ZDO anew each time the world is loaded: a «user:id» lives only till the world is closed
        // (after a restart a person's heart, bed, chest and locker were all lost — 2026-10-31). So a saved link names its
        // thing by what does not change: its kind and where it stands (a bed, a chest, a heart, a table: built things do
        // not move), and a thing that moves (a person, its locker, a foe, a lost item) by a number of its own (KSid) as
        // well — given by its owner. Found again once a session and kept; the id it had when written is tried first
        // (good till the world is closed). A link of the old kind ("user:id") is read as before.
        //   "~prefab/x/y/z/sid/user:id"   (its fields by «/», so a link sits whole in any list kept by «|», «,» or «;» —
        //   a «|» link split the lists round it, 2026-11-03: a person's plan, the rescues known; one written so is read still)
        internal static readonly int KSid = "RavenWatch_sid".GetStableHashCode(); // long: its own number (a thing that moves)
        private static readonly Dictionary<string, ZDOID> Found = new Dictionary<string, ZDOID>();
        private static readonly Dictionary<string, float> NotFound = new Dictionary<string, float>();
        private static object _foundWorld;

        // a link to it for keeping (its own number given first when it moves and is ours)
        internal static string Save(ZDOID id)
        {
            if (id.IsNone()) return "";
            ZDO t = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            return t != null ? Save(t, false) : Ref(id); // (not here: as it is — good till the world is closed)
        }

        internal static string Save(ZDO t, bool moves)
        {
            if (t == null) return "";
            long sid = t.GetLong(KSid, 0L);
            if (sid == 0L && moves && t.IsOwner()) sid = Mark(t);
            Vector3 p = t.GetPosition();
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            return "~" + t.GetPrefab() + "/" + p.x.ToString("0.##", ci) + "/" + p.y.ToString("0.##", ci) + "/" + p.z.ToString("0.##", ci) + "/" + sid + "/" + Ref(t.m_uid);
        }

        // its own number (once, by its owner)
        internal static long Mark(ZDO t)
        {
            long sid = t.GetLong(KSid, 0L);
            if (sid != 0L || !t.IsOwner()) return sid;
            do sid = ((long)UnityEngine.Random.Range(int.MinValue, int.MaxValue) << 32) ^ (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue); while (sid == 0L);
            t.Set(KSid, sid);
            return sid;
        }

        // the thing a link names, as numbered now (none: gone, or not here on this game)
        internal static ZDOID Resolve(string s)
        {
            if (string.IsNullOrEmpty(s)) return ZDOID.None;
            if (s[0] != '~') return Parse(s);
            if (ZDOMan.instance == null) return ZDOID.None;
            if (!ReferenceEquals(_foundWorld, ZNet.instance)) { Found.Clear(); NotFound.Clear(); _foundWorld = ZNet.instance; }
            ZDOID had;
            if (Found.TryGetValue(s, out had) && ZDOMan.instance.GetZDO(had) != null) return had;
            float miss;
            if (NotFound.TryGetValue(s, out miss) && Time.time - miss < 30f) return ZDOID.None; // (looked for a little ago: the search over all of a kind is not cheap)
            string[] f = s.Substring(1).Split(s.IndexOf('/') > 0 ? '/' : '|'); // (an old link: by «|»)
            int prefab; float x, y, z; long sid;
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            if (f.Length < 6 || !int.TryParse(f[0], out prefab) || !float.TryParse(f[1], System.Globalization.NumberStyles.Float, ci, out x) || !float.TryParse(f[2], System.Globalization.NumberStyles.Float, ci, out y)
                || !float.TryParse(f[3], System.Globalization.NumberStyles.Float, ci, out z) || !long.TryParse(f[4], out sid)) return ZDOID.None;
            Vector3 at = new Vector3(x, y, z);
            ZDO hit = null;
            ZDO was = ZDOMan.instance.GetZDO(Parse(f[5])); // (this session's id)
            if (was != null && was.GetPrefab() == prefab && (sid == 0L || was.GetLong(KSid, 0L) == sid)) hit = was; // (numbered this session: the same thing, wherever it went since — ids are never given twice in a session)
            if (hit == null && sid != 0L) hit = BySid(prefab, sid);
            if (hit == null && sid == 0L) hit = ByPlace(prefab, at);
            if (hit == null) { if (NotFound.Count > 500) NotFound.Clear(); NotFound[s] = Time.time; return ZDOID.None; }
            if (Found.Count > 2000) Found.Clear();
            Found[s] = hit.m_uid;
            return hit.m_uid;
        }

        // a built thing: the one of its kind standing there — among what is loaded round the spot (where a person is run,
        // its heart, bed and chest are about it: loaded); not loaded here: not found now (asked again later)
        private static readonly Collider[] Hits = new Collider[64];
        private static ZDO ByPlace(int prefab, Vector3 at)
        {
            int n = Physics.OverlapSphereNonAlloc(at, 1.5f, Hits, Physics.AllLayers, QueryTriggerInteraction.Collide);
            ZDO best = null; float bd = 0.1f;
            for (int i = 0; i < n; i++)
            {
                ZNetView nv = Hits[i] != null ? Hits[i].GetComponentInParent<ZNetView>() : null;
                ZDO o = nv != null && nv.IsValid() ? nv.GetZDO() : null;
                if (o == null || o.GetPrefab() != prefab) continue;
                float d = Vector3.Distance(o.GetPosition(), at);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // a thing that moves: among all of its kind here, the one with its number
        private static ZDO BySid(int prefab, long sid)
        {
            foreach (NpcActor a in NpcActor.Live) { ZDO z = a != null ? a.Zdo : null; if (z != null && z.GetPrefab() == prefab && z.GetLong(KSid, 0L) == sid) return z; } // (a person loaded here: no search)
            GameObject pf = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (pf == null) return null;
            List<ZDO> all = NpcCensus.Of(pf.name); // (the shared census: NpcCensus)
            foreach (ZDO o in all) if (o.GetLong(KSid, 0L) == sid) return o;
            return null;
        }

        // a person's links of an old save («user:id», dead after a restart) made lasting (where it is owned, once a session):
        // one still good now is rewritten as lasting; a dead one — its heart found again where it lives (its home is the
        // heart's place), its locker taken back where it lies (NpcLocker.Adopt), its bed, chest and work let go of (pointed
        // at again). True: done (else asked again a little later — its heart or locker not here yet)
        internal static bool Heal(NpcActor a)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned || ZDOMan.instance == null) return false;
            Mark(z); // (its own number: links to it hold)
            bool done = true;
            foreach (int key in new[] { NpcResident.KHeart, NpcResident.KBed, NpcWorker.KChest, NpcWorker.KTarget, NpcLocker.KLocker })
            {
                string raw = z.GetString(key, "");
                if (raw.Length == 0 || raw[0] == '~') continue;
                ZDOID id = Parse(raw);
                if (ZDOMan.instance.GetZDO(id) != null) { SetRef(z, key, id); continue; } // (good this session: kept lasting)
                if (key == NpcResident.KHeart)
                {
                    NpcHeart h = NpcHeart.Near(NpcRules.Home(a), 3f);
                    if (h == null) { done = false; continue; } // (its heart not loaded yet: later)
                    SetRef(z, key, h.Id);
                    Npcs.Log(a.Id + ": found its heart again after a restart");
                }
                else if (key == NpcLocker.KLocker) { z.Set(key, ""); if (!NpcLocker.Adopt(a)) Npcs.Log(a.Id + ": its locker of before the restart not found near it"); }
                else { z.Set(key, ""); Npcs.Log(a.Id + ": its " + (key == NpcResident.KBed ? "bed" : key == NpcWorker.KChest ? "chest" : "work target") + " link was lost in a restart — point it out again"); }
            }
            if (done) NpcLocker.Adopt(a); // (a locker left behind by a restart: taken back, once a session — or a full one for an empty one)
            return done;
        }

        // the same thing named by two links (a link kept and one made now)
        internal static bool Same(string a, string b) { ZDOID x = Resolve(a); return !x.IsNone() && x == Resolve(b); }

        internal static ZDOID IdOf(Component c)
        {
            ZNetView nv = c != null ? c.GetComponentInParent<ZNetView>() : null;
            return nv != null && nv.IsValid() ? nv.GetZDO().m_uid : ZDOID.None;
        }

        internal static GameObject Find(ZDOID id) { return ZNetScene.instance != null && !id.IsNone() ? ZNetScene.instance.FindInstance(id) : null; }

        // where a linked thing is (loaded: its object; else as its ZDO knows), none: null
        internal static Vector3? WhereIs(ZDOID id)
        {
            if (id.IsNone()) return null;
            GameObject go = Find(id);
            if (go != null) return go.transform.position;
            ZDO z = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
            return z != null ? (Vector3?)z.GetPosition() : null;
        }

        // the nearest T within range of the spot (its own position), that ok lets through
        internal static T Nearest<T>(Vector3 at, float range, Func<T, bool> ok) where T : Component
        {
            T best = null;
            float bd = range;
            foreach (Collider col in Physics.OverlapSphere(at, range, Built))
            {
                T t = col.GetComponentInParent<T>();
                if (t == null || (ok != null && !ok(t))) continue;
                float d = Vector3.Distance(t.transform.position, at);
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        // two steps from the nearest burning fire round «around» (within range, and inside when given), on the side of
        // «me»; none: the fallback
        internal static Vector3 ByFire(Vector3 around, float range, Vector3 me, Vector3 fallback, Func<Vector3, bool> inside)
        {
            List<Fireplace> fires = new List<Fireplace>();
            foreach (Collider col in Physics.OverlapSphere(around, range, Built))
            {
                Fireplace f = col.GetComponentInParent<Fireplace>();
                if (f != null && !fires.Contains(f) && f.IsBurning() && (inside == null || inside(f.transform.position))) fires.Add(f);
            }
            fires.Sort((x, y) => Vector3.Distance(x.transform.position, around).CompareTo(Vector3.Distance(y.transform.position, around)));
            for (int i = 0; i < fires.Count && i < 4; i++) // (the nearest few: one up a tower out of reach gives way to the next)
            {
                Vector3? s = FireSide(fires[i], me);
                if (s != null) return s.Value;
            }
            return fallback;
        }

        // nothing built over this spot (a roof, a floor above): the open sky — the fire's own smoke and sparks not counted
        private static readonly int Roofs = LayerMask.GetMask("piece", "Default", "static_solid");
        internal static bool UnderSky(Vector3 p)
        {
            return !Physics.Raycast(p + Vector3.up * 2.2f, Vector3.up, 40f, Roofs, QueryTriggerInteraction.Ignore);
        }

        // test: why each fire round here was not taken (the evening's «no fire» log)
        internal static string FiresWhy(Vector3 around, float range, Vector3 me, Func<Vector3, bool> ours, Func<Vector3, bool> priv)
        {
            List<Fireplace> fires = new List<Fireplace>();
            foreach (Collider col in Physics.OverlapSphere(around, range, Built)) { Fireplace f = col.GetComponentInParent<Fireplace>(); if (f != null && !fires.Contains(f)) fires.Add(f); }
            if (fires.Count == 0) return "no fire within " + range + " m of its home";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(" (its home ").Append(Npcs.V(around)).Append(")");
            foreach (Fireplace f in fires)
            {
                Vector3 p = f.transform.position;
                if (!f.IsBurning()) continue; // (only the burning ones: what is out is out)
                sb.Append(" · ").Append(f.name.Replace("(Clone)", "")).Append(" at ").Append(Npcs.V(p)).Append(", ").Append(Mathf.RoundToInt(Utils.DistanceXZ(p, around))).Append(" m from its home: ");
                if (!ours(p) && !priv(p)) sb.Append("outside its base and its yard");
                else if (priv(p)) sb.Append("in a player's house");
                else sb.Append(FireSide(f, me) != null ? "fine" : "no spot round it walked to");
            }
            return sb.ToString();
        }

        // a place to sit by a fire: two steps (or one and a half) from it on a floor at its own height, on «me»'s side;
        // none (a fire up a tower with no way up, on a ledge): null. Its spots and whether they are reached are kept for
        // half a minute a fire (asked by every resident's mood, whims and evening: the paths are the dear part) — the
        // floor round it by a few rays, the way to it by a few path looks from the first who asks, a door only after them
        private sealed class FireSeats { public float At; public readonly List<Vector3> Spots = new List<Vector3>(); public bool Reach; }
        private static readonly Dictionary<Fireplace, FireSeats> Seats = new Dictionary<Fireplace, FireSeats>();
        private const float SeatsAgain = 30f, NotReachedAgain = 5f; // (a «no way» asked again soon: the way finder maps the land bit by bit, its first answer is often «none»)

        // how far it is on foot between two things (each by the walkable spot beside it): the game's own path, metres —
        // a thing behind a wall is as far as the way round (none: no way at all). «Near» a table, a fire, a pantry means near
        // on foot, never through a wall into the next building
        private static readonly List<Vector3> Way = new List<Vector3>();
        // the walkable spot nearest p within r, as the game's FindValidPoint means to give — which asks the navmesh with its
        // walker's kind number instead of the navmesh's own id for it, and so answers «none» everywhere (raven npc nav,
        // 2026-10-31: none within 6 m of people standing on a floor, a way home found all the same). Asked like GetPath
        // asks: the navmesh id of that walker and its areas; the game's own call first still, it keeps that tile mapped
        private static readonly System.Collections.Generic.Dictionary<Pathfinding.AgentType, UnityEngine.AI.NavMeshQueryFilter> Filters = new System.Collections.Generic.Dictionary<Pathfinding.AgentType, UnityEngine.AI.NavMeshQueryFilter>();
        internal static bool Valid(out Vector3 v, Vector3 p, float r, Pathfinding.AgentType t)
        {
            v = p;
            Pathfinding pf = Pathfinding.instance;
            if (pf == null) return false;
            Vector3 g;
            if (pf.FindValidPoint(out g, p, r, t)) { v = g; return true; } // (should the game mend its own one day)
            UnityEngine.AI.NavMeshQueryFilter f;
            if (!Filters.TryGetValue(t, out f))
            {
                try
                {
                    object s = HarmonyLib.AccessTools.Method(typeof(Pathfinding), "GetSettings").Invoke(pf, new object[] { t });
                    UnityEngine.AI.NavMeshBuildSettings b = (UnityEngine.AI.NavMeshBuildSettings)HarmonyLib.AccessTools.Field(s.GetType(), "m_build").GetValue(s);
                    f = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = b.agentTypeID, areaMask = (int)HarmonyLib.AccessTools.Field(s.GetType(), "m_areaMask").GetValue(s) };
                }
                catch (Exception e) { Npcs.Log("way finder: its walker settings not read — " + e.Message); f = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = 0, areaMask = -1 }; }
                Filters[t] = f;
            }
            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out hit, r, f)) return false;
            v = hit.position;
            return true;
        }

        // how people walk: on foot, never swimming — the way finder's «humanoid» swims, and their way to a hall across a
        // stream went through the water (a studio shot, 2026-10-31: down the steps, into the river, stuck at the bank)
        internal const Pathfinding.AgentType Walker = Pathfinding.AgentType.HumanoidNoSwim;

        internal static string FootWhy = ""; // (test: why the last OnFoot found no way)
        internal static float OnFoot(Vector3 a, Vector3 b, float max)
        {
            if (Pathfinding.instance == null) return Vector3.Distance(a, b);
            Vector3 pa, pb;
            if (!NpcKit.Valid(out pa, a, 2.5f, NpcKit.Walker)) { FootWhy = "no walkable spot by its start"; return float.MaxValue; }
            if (!NpcKit.Valid(out pb, b, 2.5f, NpcKit.Walker)) { FootWhy = "no walkable spot by its end"; return float.MaxValue; }
            if (Vector3.Distance(pa, pb) > max) { FootWhy = "too far straight"; return float.MaxValue; } // (too far even straight: no path asked)
            Way.Clear();
            try { if (!Pathfinding.instance.GetPath(pa, pb, Way, NpcKit.Walker) || Way.Count == 0) { FootWhy = "no path (" + Way.Count + " points)"; return float.MaxValue; } } catch (Exception e) { FootWhy = "path threw " + e.GetType().Name; return float.MaxValue; }
            if (Vector3.Distance(Way[Way.Count - 1], pb) > 1.5f) { FootWhy = "only part of the way (ends " + Mathf.Round(Vector3.Distance(Way[Way.Count - 1], pb) * 10f) / 10f + " m short)"; return float.MaxValue; } // (only part of the way: none)
            FootWhy = "";
            float len = Vector3.Distance(pa, Way[0]);
            for (int i = 1; i < Way.Count; i++) len += Vector3.Distance(Way[i - 1], Way[i]);
            return len;
        }

        internal static Vector3? FireSide(Fireplace f, Vector3 me)
        {
            if (f == null) return null;
            FireSeats s;
            if (!Seats.TryGetValue(f, out s) || Time.time - s.At > (s.Reach ? SeatsAgain : NotReachedAgain))
            {
                if (Seats.Count > 64) Seats.Clear();
                s = new FireSeats { At = Time.time };
                Vector3 at = f.transform.position;
                foreach (float dist in new[] { 2f, 1.5f })
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 p = Floor(at + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * dist);
                        Vector3 v;
                        if (Pathfinding.instance != null && NpcKit.Valid(out v, p, 1f, NpcKit.Walker) && Utils.DistanceXZ(v, at) >= 1.2f) p = v; // (onto the ground the way finder walks: a fire pit's stones round it are not a floor to it)
                        if (Mathf.Abs(p.y - at.y) <= 1f) s.Spots.Add(p); // (not the ground far under it, nor a roof over it)
                    }
                s.Reach = Reached(s.Spots, me);
                Seats[f] = s;
            }
            if (!s.Reach && (s.Spots.Count == 0 || !UnderSky(f.transform.position))) return null; // (a fire in the open: taken all the same — a «no way» there is mostly the way finder not ready yet; stuck, it sits where it got to)
            Vector3 best = s.Spots[0]; float bd = float.MaxValue;
            foreach (Vector3 p in s.Spots) { float d = Utils.DistanceXZ(p, me); if (d < bd) { bd = d; best = p; } }
            return best;
        }

        // any of the spots walked to from here: the three nearest by the game's paths, else through a door to the nearest
        private static bool Reached(List<Vector3> spots, Vector3 me)
        {
            if (spots.Count == 0) return false;
            if (Pathfinding.instance == null) return true;
            List<Vector3> l = new List<Vector3>(spots);
            l.Sort((a, b) => Utils.DistanceXZ(a, me).CompareTo(Utils.DistanceXZ(b, me)));
            Vector3 from;
            if (NpcKit.Valid(out from, me, 2.5f, NpcKit.Walker)) me = from; // (from the ground it walks: seated, in a corner, its own spot is off the way finder's floor)
            for (int i = 0; i < l.Count && i < 3; i++) if (Pathfinding.instance.HavePath(me, l[i], NpcKit.Walker)) return true;
            return NpcDoors.Toward(me, l[0], NpcKit.Walker) != null;
        }

        // the surface just under the spot, looked for from a little above it (a floor, a path, the ground) — not the
        // highest thing over it (the game's «solid height» is a roof when there is one); nothing within reach: the land
        private static readonly int Floors = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
        internal static Vector3 Floor(Vector3 p)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out hit, 6f, Floors, QueryTriggerInteraction.Ignore)) return hit.point;
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }

        // null when this game may change it now; else what to tell the player
        internal static string Busy(NpcActor a)
        {
            if (a == null || a.Zdo == null) return Lang.T("Його тут уже нема.");
            return a.Owned ? null : Lang.T("Його зараз веде інша гра — спробуй ще раз.");
        }
    }
}
#endif

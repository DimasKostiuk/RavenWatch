#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A person's locker (npc-plan.md, «шафка»): what is heavy and seldom changes — its gear cells (NpcGear) and what it
    // carries (NpcWorker) — kept in a ZDO of its own instead of the person's. The game sends a changed ZDO whole, and a
    // walking person's changes up to twenty times a second to every player near: with ten cells and a full bag in it,
    // that was kilobytes a send. The locker does not walk: it is sent only when its contents change, and moved after
    // the person now and then (when it is Follow m off), so it is always among the ZDOs the person's games have.
    // Made when first needed (where the person is owned); old saves moved over once (Migrate); taken away with it.
    internal static class NpcLocker
    {
        internal const string Prefab = "RavenWatch_npc_locker";
        internal static readonly int KLocker = "RavenWatch_npcLocker".GetStableHashCode(); // string "user:id" on the person
        private const float Follow = 24f;
        private static readonly int Hash = Prefab.GetStableHashCode();
        internal static readonly int KOf = "RavenWatch_lockerOf".GetStableHashCode(); // long on the locker: its person's own number (NpcKit.KSid)

        // the prefab: nothing to see, nothing to do — a place for the ZDO to be (an unknown prefab would be complained of)
        internal static GameObject Build(Transform holder)
        {
            GameObject go = new GameObject(Prefab);
            go.transform.SetParent(holder, false);
            ZNetView nv = go.AddComponent<ZNetView>();
            nv.m_persistent = true;
            return go;
        }

        // its locker's ZDO: null while it has none (made when create, where it is owned) or while it is not here yet
        internal static ZDO Of(NpcActor a, bool create)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || ZDOMan.instance == null) return null;
            ZDOID id = NpcKit.GetRef(z, KLocker);
            if (!id.IsNone()) return ZDOMan.instance.GetZDO(id); // (null: not synced here yet — never made again over it)
            if (!create || !a.Owned) return null;
            ZDO l = ZDOMan.instance.CreateNewZDO(a.transform.position, Hash);
            l.Persistent = true;
            l.SetPrefab(Hash);
            NpcKit.Mark(l); l.Set(KOf, NpcKit.Mark(z)); // (both found again after a restart: NpcKit «lasting links»)
            NpcKit.SetRef(z, KLocker, l.m_uid);
            NpcCensus.Added(l);
            return l;
        }

        // its locker can be read and written here now (it has none yet, or it is here): until then nothing of it is
        // written — an empty bag written over a locker not yet synced would lose what is in it
        internal static bool Ready(NpcActor a) { return !Has(a) || (ZDOMan.instance != null && ZDOMan.instance.GetZDO(NpcKit.GetRef(a.Zdo, KLocker)) != null); }

        internal static bool Has(NpcActor a) { return a != null && a.Zdo != null && !NpcKit.GetRef(a.Zdo, KLocker).IsNone(); }

        // a string from it: null when it cannot be read here now (not here yet); "" when it has none
        internal static string Get(NpcActor a, int key)
        {
            if (!Has(a)) return "";
            ZDO l = Of(a, false);
            return l != null ? l.GetString(key, "") : null;
        }

        // written into it (made when there is none and there is something to keep)
        internal static bool Set(NpcActor a, int key, string value)
        {
            ZDO l = Of(a, !string.IsNullOrEmpty(value));
            if (l == null) return string.IsNullOrEmpty(value) && !Has(a); // (nothing to keep and nowhere to keep it: fine)
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            if (l.GetString(key, "") != value) l.Set(key, value);
            return true;
        }

        // its locker named but not there: on a client maybe not synced yet — on the server (it holds every ZDO) it is gone
        // for good; after half a minute of that the person is let go of it (a new one is made when there is something to
        // keep; what was in the old one is lost) — else it would wait for its things for ever
        private static readonly System.Collections.Generic.Dictionary<NpcActor, float> Missing = new System.Collections.Generic.Dictionary<NpcActor, float>();
        internal static void Check(NpcActor a)
        {
            if (a == null || !a.Owned || !Has(a) || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Of(a, false) != null) { Missing.Remove(a); return; }
            float since;
            if (!Missing.TryGetValue(a, out since)) { Missing[a] = Time.time; return; }
            if (Time.time - since < 30f) return;
            Missing.Remove(a);
            Npcs.Log(a.Id + ": its locker " + a.Zdo.GetString(KLocker, "") + " is not found (the server has no such ZDO) — let go of");
            NpcKit.SetRef(a.Zdo, KLocker, ZDOID.None);
            if (Adopt(a)) return; // (one of its own lying by it, its link lost: taken back — else a new one when there is something to keep)
            Npcs.Log(a.Id + ": no locker of its own near it — a new one when needed; what was in the old one is lost");
        }

        // a locker of an old save left without its person (its link was a «user:id», gone with the restart): taken back
        // by the person it lies by — the nearest person here with no locker of its own (the user, 2026-10-31: a guard's
        // bow and arrows were lost so)
        internal static bool Adopt(NpcActor a)
        {
            if (a == null || !a.Owned || ZDOMan.instance == null) return false;
            ZDO mine = Has(a) ? Of(a, false) : null;
            if (Has(a) && (mine == null || !Empty(mine))) return false; // (one with its things in it: kept)
            List<ZDO> all = NpcCensus.Of(Prefab); // (the shared census: NpcCensus)
            ZDO best = null; float bd = 60f;
            long me = NpcKit.Mark(a.Zdo);
            foreach (ZDO l in all)
            {
                long of = l.GetLong(KOf, 0L);
                if (of != 0L && of != me) continue; // (another's)
                float d = Utils.DistanceXZ(l.GetPosition(), a.transform.position);
                if (d >= bd) continue;
                bool closer = false; // (a person nearer to it that has no locker either: its, more likely)
                foreach (NpcActor o in NpcActor.Live) if (o != null && o != a && !Has(o) && Utils.DistanceXZ(o.transform.position, l.GetPosition()) < d) { closer = true; break; }
                if (of == 0L && closer) continue;
                if (of == 0L && Taken(l.m_uid)) continue;
                if (mine != null && (l == mine || Empty(l))) continue; // (it has an empty one: only a full one is worth the change)
                if (Empty(l) && d > 10f) continue; // (an empty one far off: no use, and maybe another's)
                best = l; bd = d;
            }
            if (best == null) return false;
            if (!best.IsOwner()) best.SetOwner(ZDOMan.GetSessionID());
            if (mine != null) { if (!mine.IsOwner()) mine.SetOwner(ZDOMan.GetSessionID()); ZDOMan.instance.DestroyZDO(mine); } // (the empty one taken by mistake: gone)
            NpcKit.Mark(best); best.Set(KOf, me);
            NpcKit.SetRef(a.Zdo, KLocker, best.m_uid);
            Npcs.Log(a.Id + ": took back its locker " + NpcKit.Ref(best.m_uid) + " (" + Mathf.RoundToInt(bd) + " m off)");
            return true;
        }

        // nothing in it at all: its gear, what it carries, its request (only such a one is given up for a full one)
        private static bool Empty(ZDO l) { return l.GetString(ZDOVars.s_items, "").Length == 0 && l.GetString(NpcWorker.KCarry, "").Length == 0 && l.GetString(NpcRequests.KRequest, "").Length == 0 && l.GetString(NpcMemory.KMemory, "").Length == 0 && l.GetString(NpcAttitude.KAttitude, "").Length == 0; } // (what it remembers and feels is kept too)

        private static bool Taken(ZDOID l) { foreach (NpcActor o in NpcActor.Live) if (o != null && Has(o) && NpcKit.GetRef(o.Zdo, KLocker) == l) return true; return false; }

        // after the person (where it is owned, now and then)
        internal static void Keep(NpcActor a)
        {
            ZDO l = Has(a) ? Of(a, false) : null;
            if (l == null || Utils.DistanceXZ(l.GetPosition(), a.transform.position) < Follow) return;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            l.SetPosition(a.transform.position);
        }

        // what an old save kept on the person itself: into the locker once, then off the person
        internal static bool Migrate(NpcActor a) // (true: nothing left on the person — else asked again later)
        {
            ZDO z = a != null ? a.Zdo : null;
            if (z == null || !a.Owned) return false;
            if (!Ready(a)) return false;
            bool left = false;
            foreach (int key in new[] { ZDOVars.s_items, NpcWorker.KCarry, NpcRequests.KRequest })
            {
                string s = z.GetString(key, "");
                if (s.Length == 0) continue;
                if (Set(a, key, s)) { z.Set(key, ""); Npcs.Log(a.Id + ": moved its " + (key == NpcWorker.KCarry ? "carry" : key == NpcRequests.KRequest ? "request" : "gear") + " into its locker"); }
                else left = true;
            }
            return !left;
        }

        // the person taken away: its locker too
        internal static void Remove(NpcActor a)
        {
            ZDO l = Has(a) ? Of(a, false) : null;
            if (l == null) return;
            if (!l.IsOwner()) l.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(l);
        }
    }
}
#endif

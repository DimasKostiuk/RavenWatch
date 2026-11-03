#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Nobody lost in the world (npc-plan.md, «запобіжник»): a person runs only where a player has its zone loaded — one
    // who walked off with you and was left there (you logged out, its «follow me» does not outlive that) would stand
    // there for ever. The server looks over the people now and then (it has them all, loaded or not): one that is its
    // player's — living at a base, or hired — far from where it belongs, with no player near it for a while, is put
    // back where it belongs:
    //   a resident     at its home (its base's middle; its heart)
    //   on its way     at the end of its way (a heart: there it settles as it would on arriving — NpcTravel)
    //   hired, homeless at the ravens' home (NpcTravel.Hall), to be hired again from there
    // Its locker goes with it (its gear, what it carries). A story person not hired, one in need, Odin — left where
    // the story put them. Nothing runs on a client; nothing is sent but the moved ZDOs.
    internal static class NpcStray
    {
        private const float Every = 30f, Far = 120f, Watched = 200f, Alone = 600f;
        private static float _next;
        private static object _world;
        private static bool _first = true; // (the first look since this game started the world)
        private static readonly Dictionary<ZDOID, float> AloneSince = new Dictionary<ZDOID, float>(); // (server time it was first seen far and alone)
        private static readonly List<ZDO> Scan = new List<ZDO>();

        internal static void ServerTick()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null) return;
            if (!ReferenceEquals(_world, ZNet.instance)) { _world = ZNet.instance; _first = true; _next = 0f; AloneSince.Clear(); } // (another world: looked at afresh)
            if (Time.time < _next) return;
            _next = Time.time + Every;
            try
            {
                List<Vector3> players = Players();
                Scan.Clear();
                Scan.AddRange(NpcCensus.Of(Npcs.HumanPrefab)); // (the shared census: NpcCensus)
                HashSet<ZDOID> seen = new HashSet<ZDOID>();
                foreach (ZDO n in Scan)
                {
                    if (n == null) continue;
                    seen.Add(n.m_uid);
                    Vector3 to;
                    string where;
                    if (!Belongs(n, out to, out where) || Utils.DistanceXZ(n.GetPosition(), to) < Far || Near(players, n.GetPosition()) || RunByPeer(n)) { AloneSince.Remove(n.m_uid); continue; }
                    float since;
                    if (!AloneSince.TryGetValue(n.m_uid, out since)) { AloneSince[n.m_uid] = _first ? Time.time - Alone + 60f : Time.time; continue; } // (found so on the world's first look: left there before it was saved — back a minute from now)
                    if (Time.time - since < Alone) continue;
                    AloneSince.Remove(n.m_uid);
                    Bring(n, to, where);
                }
                _first = false;
                foreach (ZDOID id in new List<ZDOID>(AloneSince.Keys)) if (!seen.Contains(id)) AloneSince.Remove(id); // (gone meanwhile)
            }
            catch (System.Exception e) { Npcs.Log("strays: " + e.Message); }
        }

        // where it belongs (false: it is none of ours to bring back)
        private static bool Belongs(ZDO n, out Vector3 to, out string where)
        {
            to = Vector3.zero; where = "";
            bool resident = n.GetBool(NpcResident.KResident, false), hired = n.GetLong(NpcTalk.KHired, 0L) != 0L;
            if (!resident && !hired) return false; // (a story person not hired, one in need: where the story put it)
            NpcMind.Plan way = Way(n);
            if (way != null) { to = way.To; where = "the end of its way"; return true; }
            if (resident) { to = n.GetVec3(NpcRules.KHome, Vector3.zero); where = "its home"; return to != Vector3.zero; }
            to = NpcTravel.Hall; where = "the ravens' home";
            return to != Vector3.zero;
        }

        // its long way, as it kept it (on the order layer, or paused under an order: NpcMind «way»)
        private static NpcMind.Plan Way(ZDO n)
        {
            foreach (string part in n.GetString(NpcMind.KPlans, "").Split(';'))
            {
                if (!part.StartsWith("order=") && !part.StartsWith("way=")) continue;
                NpcMind.Plan p = NpcMind.Plan.Parse(part.Substring(part.IndexOf('=') + 1));
                if (p != null && p.Kind == NpcMind.Task.Travel) return p;
            }
            return null;
        }

        // put there, its locker with it (owned by the server for the move: whoever loads it next takes it over)
        private static void Bring(ZDO n, Vector3 to, string where)
        {
            Vector2 off = Random.insideUnitCircle * 1.5f;
            Vector3 at = to + new Vector3(off.x, 0.3f, off.y);
            Move(n, at);
            ZDO locker = ZDOMan.instance.GetZDO(NpcKit.GetRef(n, NpcLocker.KLocker));
            if (locker != null) Move(locker, at);
            Npcs.Log(n.GetString(Npcs.KId, "?") + " " + n.m_uid + ": far from where it belongs, nobody near it — the server put it at " + where);
        }

        private static void Move(ZDO z, Vector3 at)
        {
            if (z.GetOwner() != ZDOMan.GetSessionID()) z.SetOwner(ZDOMan.GetSessionID());
            z.SetPosition(at);
        }

        // where the players are: each peer's, and this game's own player (a host)
        private static List<Vector3> Players()
        {
            List<Vector3> l = new List<Vector3>();
            foreach (ZNetPeer peer in ZNet.instance.GetPeers()) if (peer != null) l.Add(peer.m_refPos);
            if (Player.m_localPlayer != null) l.Add(Player.m_localPlayer.transform.position);
            return l;
        }

        // run now by a connected player's game (its zone loaded there): not moved under it — that game would write it back
        private static bool RunByPeer(ZDO n)
        {
            long owner = n.GetOwner();
            return owner != 0L && owner != ZDOMan.GetSessionID() && ZNet.instance.GetPeer(owner) != null;
        }

        private static bool Near(List<Vector3> players, Vector3 at)
        {
            foreach (Vector3 p in players) if (Utils.DistanceXZ(p, at) < Watched) return true;
            return false;
        }
    }
}
#endif

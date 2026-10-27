#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // What changes seldom and matters only to the players near (the gossip facts, the bonds' scores) is not kept on the
    // player's ZDO: that goes to every peer whole, up to 20 times a second, with every move. It is sent to each player
    // within reach when it changes, and again now and then (one who came near since gets it too); every game keeps
    // what it last heard from each player, for a while.
    internal static class NearCast
    {
        internal const int Gossip = 0, Bonds = 1;
        private const string Rpc = "RavenWatch_NearCast";
        internal static float Again = 30f, Keep = 90f, Reach = 60f;

        private sealed class Note { internal string Raw; internal float At; }
        // by kind: player id → what was heard from that player / what was told to them
        private static readonly Dictionary<long, Note>[] Heard = { new Dictionary<long, Note>(), new Dictionary<long, Note>() };
        private static readonly Dictionary<long, Note>[] Told = { new Dictionary<long, Note>(), new Dictionary<long, Note>() };

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnNote)); }

        // mine of this kind, to each player within reach who has not had this lately
        internal static void Tell(Player me, int kind, string raw)
        {
            if (me == null || raw == null || ZRoutedRpc.instance == null) return;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || Vector3.Distance(o.transform.position, me.transform.position) > Reach) continue;
                ZNetView nv = o.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                long id = o.GetPlayerID();
                Note t;
                if (Told[kind].TryGetValue(id, out t) && t.Raw == raw && Time.time - t.At < Again) continue;
                ZPackage pkg = new ZPackage();
                pkg.Write(kind);
                pkg.Write(me.GetPlayerID());
                pkg.Write(raw);
                ZRoutedRpc.instance.InvokeRoutedRPC(nv.GetZDO().GetOwner(), Rpc, pkg);
                Told[kind][id] = new Note { Raw = raw, At = Time.time };
            }
        }

        // what that player last told of this kind ("" when nothing, or not for a while)
        internal static string Of(Player o, int kind)
        {
            Note n;
            if (o == null || !Heard[kind].TryGetValue(o.GetPlayerID(), out n) || Time.time - n.At > Keep) return "";
            return n.Raw;
        }

        // as if that player had told it, kept for good (the test dummies, raven dummy)
        internal static void Put(long playerId, int kind, string raw) { Heard[kind][playerId] = new Note { Raw = raw ?? "", At = float.MaxValue }; }

        private static void OnNote(long sender, ZPackage pkg)
        {
            int kind = pkg.ReadInt();
            long pid = pkg.ReadLong();
            string raw = pkg.ReadString();
            if (kind < 0 || kind >= Heard.Length) return;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o.GetPlayerID() != pid) continue;
                ZNetView nv = o.GetComponent<ZNetView>(); // (only that player's own game speaks for it)
                if (nv != null && nv.IsValid() && nv.GetZDO().GetOwner() == sender) Heard[kind][pid] = new Note { Raw = raw, At = Time.time };
                return;
            }
        }
    }
}
#endif

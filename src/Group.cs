using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A group fighting together should not all get a reactive Hugin after the same battle.
    // Before a reactive visit the player asks the server; the server lets at most a few players
    // in the same spot have one within a short time. No answer (an older server) means yes.
    internal static class Group
    {
        internal const string RpcAsk = "RavenWatch_ReactAsk";
        internal const string RpcAnswer = "RavenWatch_ReactAnswer";

        // server settings (read from the server's own config)
        internal static float Radius = 50f;
        internal static float WindowSeconds = 600f;
        internal static int MaxPerGroup = 2;

        // client state
        internal static bool Waiting;
        internal static float AskedAt;
        internal static bool HasAnswer;
        internal static bool Answer;
        private const float TimeoutSeconds = 5f;

        private struct Grant { public long Uid; public Vector3 Pos; public float Time; }
        private static readonly List<Grant> Grants = new List<Grant>();

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcAnswer, new Action<long, ZPackage>(OnAnswer));
        }

        internal static void ResetClient()
        {
            Waiting = false;
            HasAnswer = false;
        }

        // ---------------------------------------------------------------- client

        internal static void Ask(Vector3 pos)
        {
            Waiting = true;
            HasAnswer = false;
            AskedAt = Time.time;
            ZPackage p = new ZPackage();
            p.Write(pos);
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcAsk, p);
        }

        // true once there is a decision: the server's, or "yes" after the timeout
        internal static bool Decided(out bool allowed)
        {
            allowed = true;
            if (!Waiting) return false;
            if (HasAnswer) { allowed = Answer; Waiting = false; return true; }
            if (Time.time - AskedAt > TimeoutSeconds) { allowed = true; Waiting = false; return true; }
            return false;
        }

        private static void OnAnswer(long sender, ZPackage p)
        {
            bool allowed = p.ReadBool();
            if (sender != Plugin.ServerPeerId() && sender != ZDOMan.GetSessionID()) return;
            if (!Waiting) return;
            Answer = allowed;
            HasAnswer = true;
        }

        // ---------------------------------------------------------------- server

        private static void OnAsk(long sender, ZPackage p)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            Vector3 pos = p.ReadVector3();
            float now = Time.time;
            Grants.RemoveAll(g => now - g.Time > WindowSeconds);

            int near = 0;
            foreach (Grant g in Grants)
                if (g.Uid != sender && Vector3.Distance(g.Pos, pos) <= Radius) near++;

            bool allowed = near < Math.Max(1, MaxPerGroup);
            if (allowed)
            {
                Grant n;
                n.Uid = sender; n.Pos = pos; n.Time = now;
                Grants.Add(n);
                if (Grants.Count > 200) Grants.RemoveAt(0);
            }

            ZPackage answer = new ZPackage();
            answer.Write(allowed);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcAnswer, answer);
        }
    }
}

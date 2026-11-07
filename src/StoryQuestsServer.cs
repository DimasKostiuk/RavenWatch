#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A quest of the server (docs/plans/story-server-core.md; the user 2026-11-07): the global story is the world's, nothing of it is kept in a player.
    //   owner: server                the quest's one stage for the whole world: the world's text «q_<id>» = «<stage>», «-done» at its end; «<stage> ! <from> <0|1>» while its actions are owed
    //   who: quest prologue done     the players it leads: only their games look at its stages, show its goal and mark and may pass it; anyone may stand by and watch
    //   fork: world heart_first -> lantern   a quest's own fork: asked on every stage, before the stage's
    // Every game it leads looks at the stage as a personal quest's does, but moves nothing: the first whose stage is done asks the server («stage X
    // passed, on to Y») and the server takes it only while X still stands, so two never pass one stage. Who is granted it runs the stage's «done»
    // and the next one's «start» (RpcGo), so those actions run once in the world, on that game. So they ask nothing of a character: no flag, var,
    // stamp, spot, saga, ways, goto or end on such a quest (storycheck).
    // A stage's scene has one player at a time: a claim to the server (Mine), renewed while it plays, gone with its player after ClaimFor s. Who
    // passed the last stage has it first for FirstFor s (he is the one standing there); then any it leads, where the scene's own needs let him.
    // While a claim stands only its holder may pass the stage.
    // The actions must not be lost with the one who was granted them (the world's letter would be gone and the heart shut for all): while they are owed the key says so
    // and nothing moves. He says when they are run (RpcActed); he has no body now (between death and respawn): they wait for it (_owed); he is gone, or has not said in
    // ActWait s: any game it leads gets them (RpcActAsk).
    internal static partial class StoryQuests
    {
        internal const string DoneWord = "-done";
        private const string RpcAsk = "RavenWatch_QuestAsk", RpcGo = "RavenWatch_QuestGo", RpcClaim = "RavenWatch_QuestClaim", RpcClaimed = "RavenWatch_QuestClaimed", RpcUnclaim = "RavenWatch_QuestUnclaim", RpcActed = "RavenWatch_QuestActed", RpcActAsk = "RavenWatch_QuestActAsk";
        internal static float AskAgain = 3f, ClaimFor = 15f, ClaimRenew = 5f, ClaimAskAgain = 2f, FirstFor = 20f, ActWait = 20f;

        private static string KeyOf(QuestDef q) { return "q_" + q.Id; }

        // the world's word on it, read once a change (a stage is asked many times a pass)
        private static void ReadWorld(QuestDef q)
        {
            string raw = StoryWorld.Text(KeyOf(q));
            if (raw == q.Raw) return;
            q.Raw = raw;
            string[] w = raw.Split(' ');
            q.AtNow = raw.Length == 0 || raw == DoneWord ? "" : w[0];
            q.Owed = w.Length >= 4 && w[1] == "!";
            q.OwedFrom = q.Owed && w[2] != "-" ? w[2] : "";
            q.OwedRan = q.Owed && w[3] == "1";
            if (q.Latch != q.AtNow) q.Latch = "";
        }

        // its state and its stage, whoever keeps them: the one way to ask
        internal static string State(Player p, QuestDef q)
        {
            if (!q.Server) return Story.Quest(p, q.Id);
            ReadWorld(q);
            return q.Raw.Length == 0 ? "" : q.Raw == DoneWord ? Story.Done : Story.Active;
        }

        internal static string At(Player p, QuestDef q)
        {
            if (!q.Server) return Story.StageOf(p, q.Id);
            ReadWorld(q);
            return q.AtNow;
        }

        // by the quest's id (the conditions «quest», «step»): none of that name — what the character keeps
        internal static string StateOf(Player p, string id) { QuestDef q = Find(id); return q != null ? State(p, q) : Story.Quest(p, id); }

        internal static bool IsAt(Player p, string quest, string stage)
        {
            QuestDef q = Find(quest);
            StageDef s = q != null ? StageNow(p, q) : null;
            return s != null && s.Id == stage;
        }

        // does this quest lead this player (a personal one: always)
        internal static bool Leads(Player p, QuestDef q) { return !q.Server || (p != null && StoryCond.All(p, q.Who)); }

        // the quest of the server whose stage plays this scene (null: a scene of one's own)
        internal static QuestDef OwnerOf(string scene)
        {
            Load();
            foreach (QuestDef q in Quests)
                if (q.Server) foreach (StageDef s in q.Stages) if (s.Scene == scene) return q;
            return null;
        }

        // ---------------------------------------------------------------- a game it leads, twice a second
        private static void ServerStep(Player p, QuestDef q)
        {
            if (State(p, q) != Story.Active || !Leads(p, q)) return;
            StageDef s = q.Stage(At(p, q));
            if (s == null) { if (Time.time >= q.AskUntil) { Note(q.Id + ": нема етапу «" + q.AtNow + "» у файлі — з початку"); Ask(q, q.AtNow, q.First, false, false); } return; } // (a stage gone from the file: from the top, as a personal quest)
            if (q.Owed)
            {
                if (Time.time >= q.ActAskAt && ZRoutedRpc.instance != null) { q.ActAskAt = Time.time + AskAgain; ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcActAsk, q.Id); }
                return;
            }
            bool playing = s.Scene.Length > 0 && StoryScenes.Playing == s.Scene;
            if (q.Latch != s.Id && !playing && Passed(p, s)) q.Latch = s.Id; // (what passed it lives one pass, the scene's end above all: held till the stage moves, or a slow answer plays the scene again)
            if (Time.time < q.AskUntil) return;
            if (!playing)
            {
                foreach (Fork f in q.Forks) if (f.To != s.Id && StoryCond.All(p, f.When)) { Ask(q, s.Id, f.To, false, false); return; }
                foreach (Fork f in s.Forks) if (StoryCond.All(p, f.When)) { Ask(q, s.Id, f.To, false, false); return; }
            }
            if (q.Latch == s.Id) { Ask(q, s.Id, s.Next, true, false); return; }
            if (s.Scene.Length > 0 && !playing) StoryScenes.TryPlay(s.Scene);
        }

        private static void StartOnServer(QuestDef q) { Ask(q, "", q.First, false, false); }

        // to the server: «from» stands, on to «to» (ranDone: the stage's «done» is run when granted); force: the author's, whatever stands
        private static void Ask(QuestDef q, string from, string to, bool ranDone, bool force)
        {
            q.AskUntil = Time.time + AskAgain;
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(q.Id); pkg.Write(from); pkg.Write(to); pkg.Write(ranDone); pkg.Write(force);
            ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcAsk, pkg);
        }

        // ---------------------------------------------------------------- the server: one answer for the whole world
        private sealed class Claim { public long Holder, First, ActHolder; public float Until, EnteredAt, ActAt; }
        private static readonly Dictionary<string, Claim> _claims = new Dictionary<string, Claim>(); // session: a claim lives as long as its player, nothing to keep

        private static Claim ClaimOf(QuestDef q) { Claim c; if (!_claims.TryGetValue(q.Id, out c)) _claims[q.Id] = c = new Claim(); return c; }

        private static void OnAsk(long sender, ZPackage pkg)
        {
            try { Asked(sender, pkg); }
            catch (Exception e) { Failed("server ask", e); } // (its first read of the quests on a server may be here)
        }

        private static void Asked(long sender, ZPackage pkg)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            string id = pkg.ReadString(), from = pkg.ReadString(), to = pkg.ReadString();
            bool ranDone = pkg.ReadBool(), force = pkg.ReadBool();
            QuestDef q = Find(id);
            if (q == null || !q.Server) return;
            if (force ? !Plugin.IsAdminSender(sender) : q.Stage(to) == null && to != "end") return;
            ReadWorld(q);
            if (!force && (q.AtNow != from || (q.Owed && q.Stage(q.AtNow) != null))) return; // (another passed it first; or what was granted is not run yet, if it can be)
            if (!force && from.Length == 0 && q.Raw.Length > 0) return; // (begun by another)
            Claim c = ClaimOf(q);
            if (!force && Time.time < c.Until && c.Holder != sender) return; // (its scene plays: the stage is its player's)
            StageDef was = q.Stage(from), next = q.Stage(to);
            bool owed = to != "end" && ((ranDone && was != null && was.Done.Count > 0) || (next != null && next.Start.Count > 0));
            StoryWorld.SetText(KeyOf(q), to == "end" ? DoneWord : owed ? to + " ! " + (from.Length > 0 ? from : "-") + " " + (ranDone ? "1" : "0") : to);
            c.Holder = 0L; c.Until = 0f; c.First = sender; c.EnteredAt = Time.time; c.ActHolder = sender; c.ActAt = Time.time;
            Grant(sender, id, from, to, ranDone);
        }

        private static void Grant(long to, string id, string from, string stage, bool ranDone)
        {
            ZPackage go = new ZPackage();
            go.Write(id); go.Write(from); go.Write(stage); go.Write(ranDone);
            ZRoutedRpc.instance.InvokeRoutedRPC(to, RpcGo, go);
        }

        private static bool HereStill(long peer) { return peer == ZDOMan.GetSessionID() || ZNet.instance.GetPeer(peer) != null; }

        // he has run what was owed: the stage stands free
        private static void OnActed(long sender, string id, string stage)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            QuestDef q = Find(id);
            if (q == null || !q.Server) return;
            ReadWorld(q);
            if (q.Owed && q.AtNow == stage) StoryWorld.SetText(KeyOf(q), stage);
        }

        // a game it leads sees actions owed: to it, if the one they were granted to is gone or has been silent too long
        private static void OnActAsk(long sender, string id)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            QuestDef q = Find(id);
            if (q == null || !q.Server) return;
            ReadWorld(q);
            if (!q.Owed) return;
            Claim c = ClaimOf(q);
            if (c.ActHolder != 0L && Time.time - c.ActAt < ActWait && HereStill(c.ActHolder)) return;
            c.ActHolder = sender; c.ActAt = Time.time;
            Story.Log("track: " + q.Id + ": actions of «" + q.AtNow + "» owed, given to another game");
            Grant(sender, id, q.OwedFrom, q.AtNow, q.OwedRan);
        }

        private static void OnClaim(long sender, string id, string stage)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            QuestDef q = Find(id);
            if (q == null || !q.Server) return;
            ReadWorld(q);
            if (q.AtNow != stage || q.Owed) return;
            Claim c = ClaimOf(q);
            if (Time.time < c.Until && c.Holder != sender) return;
            if (c.Holder != sender && c.First != 0L && c.First != sender && Time.time - c.EnteredAt < FirstFor && HereStill(c.First)) return; // (who stands there has it first, while he is here at all; the host is no peer of his own)
            c.Holder = sender; c.Until = Time.time + ClaimFor;
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcClaimed, id);
        }

        private static void OnUnclaim(long sender, string id)
        {
            Claim c;
            if (ZNet.instance != null && ZNet.instance.IsServer() && _claims.TryGetValue(id, out c) && c.Holder == sender) { c.Holder = 0L; c.Until = 0f; }
        }

        // ---------------------------------------------------------------- the game that asked
        private static bool FromServer(long sender) { return sender == Plugin.ServerPeerId() || sender == ZDOMan.GetSessionID(); }

        private static void OnGo(long sender, ZPackage pkg)
        {
            if (!FromServer(sender)) return;
            string id = pkg.ReadString(), from = pkg.ReadString(), to = pkg.ReadString();
            bool ranDone = pkg.ReadBool();
            if (Player.m_localPlayer == null) { _owed.Add(new[] { id, from, to, ranDone ? "1" : "0" }); return; } // (no body now: when it is back)
            Went(id, from, to, ranDone);
        }

        private static readonly List<string[]> _owed = new List<string[]>(); // session: granted while this game had no player

        private static void RunOwed()
        {
            if (_owed.Count == 0) return;
            List<string[]> l = new List<string[]>(_owed);
            _owed.Clear();
            foreach (string[] g in l)
            {
                QuestDef q = Find(g[0]);
                if (q == null) continue;
                ReadWorld(q);
                if (q.Owed && q.AtNow == g[2]) Went(g[0], g[1], g[2], g[3] == "1"); // (another may have run them while he had no body, or the author moved it: not a second time)
            }
        }

        private static void Went(string id, string from, string to, bool ranDone)
        {
            Player p = Player.m_localPlayer;
            QuestDef q = Find(id);
            if (q == null || p == null) return;
            q.AskUntil = 0f; q.ClaimUntil = 0f; q.Latch = "";
            StageDef was = q.Stage(from);
            if (ranDone && was != null) Run(p, q, was.Done);
            if (to == "end")
            {
                Note(q.Id + ": пройдено на цьому сервері");
                StoryBus.Raise("quest_" + Story.Done, q.Id, 0);
                return;
            }
            StageDef s = q.Stage(to);
            if (s == null)
            {
                Note(q.Id + ": нема етапу «" + to + "»");
                if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcActed, q.Id, to); // (nothing to run here: the key not left owed for good)
                return;
            }
            Note(q.Id + " → " + s.Id + " (сервер)");
            StoryBus.Raise("stage", q.Id + "." + s.Id, 0);
            Run(p, q, s.Start);
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcActed, q.Id, s.Id);
        }

        private static void OnClaimed(long sender, string id)
        {
            QuestDef q = FromServer(sender) ? Find(id) : null;
            if (q != null) q.ClaimUntil = Time.time + ClaimFor;
        }

        // may this game play that scene now (a scene of one's own: always)? Asks the server when not; the next pass may have it
        internal static bool Mine(string scene)
        {
            QuestDef q = OwnerOf(scene);
            if (q == null || Time.time < q.ClaimUntil) return true;
            if (Time.time >= q.ClaimAskedAt && ZRoutedRpc.instance != null)
            {
                q.ClaimAskedAt = Time.time + ClaimAskAgain;
                ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcClaim, q.Id, At(Player.m_localPlayer, q));
            }
            return false;
        }

        // the scene plays on: its claim too (StoryScenes.Tick, every frame: its own clock)
        internal static void Renew(string scene)
        {
            if (Time.time < _renewAt) return;
            _renewAt = Time.time + ClaimRenew;
            QuestDef q = OwnerOf(scene);
            if (q != null && ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcClaim, q.Id, At(Player.m_localPlayer, q));
        }
        private static float _renewAt;

        // stopped before its end: the next one may have it
        internal static void Unclaim(string scene)
        {
            QuestDef q = OwnerOf(scene);
            if (q == null) return;
            q.ClaimUntil = 0f;
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(Plugin.ServerPeerId(), RpcUnclaim, q.Id);
        }

        internal static void RegisterRpcs()
        {
            _claims.Clear();
            _owed.Clear(); // (of the world before: never run in this one)
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, OnAsk);
            ZRoutedRpc.instance.Register<ZPackage>(RpcGo, OnGo);
            ZRoutedRpc.instance.Register<string, string>(RpcClaim, OnClaim);
            ZRoutedRpc.instance.Register<string>(RpcClaimed, OnClaimed);
            ZRoutedRpc.instance.Register<string>(RpcUnclaim, OnUnclaim);
            ZRoutedRpc.instance.Register<string, string>(RpcActed, OnActed);
            ZRoutedRpc.instance.Register<string>(RpcActAsk, OnActAsk);
        }

        // «a scene's "lasting" step ran» for this scene: in the world for a scene of the server, in the character for one's own
        internal static int Committed(Player p, string scene) { string n = "scene." + scene; return OwnerOf(scene) != null ? StoryWorld.Var(n) : Story.Var(p, n); }
        internal static void Commit(Player p, string scene, int step)
        {
            string n = "scene." + scene;
            if (OwnerOf(scene) == null) { Story.SetVar(p, n, step, true); return; }
            if (step == 0) StoryWorld.Remove(n); else StoryWorld.SetVar(n, step); // (taken off whatever this game reads now: the key of a step a moment ago may not be back yet)
        }
    }
}
#endif

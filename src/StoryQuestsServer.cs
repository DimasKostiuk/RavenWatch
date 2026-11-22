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
    // stamp, spot, ways, goto or end on such a quest (storycheck).
    // Its «saga:» (the user 2026-11-16: the book's «Мій шлях» of a chapter of the server was empty) goes to those who were there, each game for its own
    // player (Witnessed): the stage passed its own way (to its «next», not by a fork), and this player passed it himself (the server granted his game
    // that very pass: OnGo) or stood within SagaNear m of the place of its mark within SagaRecent s before. A late one, one far off, one not in the game
    // then: nothing. Nothing is sent: every game reads the world's word on the stage anyway (ReadWorld); the server's answer and that word may come in
    // either order, so a pass seen waits up to MineWait s for the answer. All of it is of one world (QuestDef.WorldOf): another world starts it anew.
    // Cost: twice a second, on a stage with «saga:» only, its place looked up (as its map mark is anyway) and one distance.
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
            string world = Story.WorldId;
            if (q.WorldOf != world) { q.WorldOf = world; q.Raw = null; q.Latch = ""; q.Left.Clear(); q.Mine = q.NearStage = ""; } // (another world: nothing of the last one's passes and places counts here)
            string raw = StoryWorld.Text(KeyOf(q));
            if (raw == q.Raw) return;
            bool first = q.Raw == null; // (the first word in this world this session: what stands now was not seen passing)
            string was = q.AtNow;
            q.Raw = raw;
            string[] w = raw.Split(' ');
            bool done = w[0] == DoneWord;
            q.AtNow = raw.Length == 0 || done ? "" : w[0];
            q.EndedAt = done && w.Length > 1 ? w[1] : ""; // («-done <stage>»: the stage it ended at by its own «next: end» — ServerResume)
            q.Owed = w.Length >= 4 && w[1] == "!";
            q.OwedFrom = q.Owed && w[2] != "-" ? w[2] : "";
            q.OwedRan = q.Owed && w[3] == "1";
            if (!first && was.Length > 0 && was != q.AtNow)
            {
                StageDef s = q.Stage(was);
                if (s != null && s.Saga.Length > 0 && (q.AtNow == s.Next || (done && q.EndedAt == was))) q.Left.Add(new KeyValuePair<string, float>(was, Time.time)); // (its own way on, not a fork: Witnessed, on the next pass)
            }
            if (q.Latch != q.AtNow) q.Latch = "";
        }

        // ---------------------------------------------------------------- «saga:» of a quest of the server: to those who were there (the header)
        internal static float SagaNear = 40f, SagaRecent = 60f, MineWait = 5f; // m from the place of the stage's mark · s before the stage passed · s a pass seen waits for the server's answer to this game

        // while a stage with «saga:» stands: this player by the place of its mark now — the place itself (StoryWhere.Find), not the map's mark: that one
        // is the way in for a point inside a dungeon of ours (by the barrow's chest nobody would be «near»), off the spot for a search, and set after this in a pass
        private static void Near(Player p, QuestDef q, StageDef s)
        {
            if (s.Saga.Length == 0) return;
            NearOne(p, q, s, s.MarkAt);
            foreach (StageDef m in s.Marks) NearOne(p, q, s, m.MarkAt); // (its other places to choose from: by any of them)
        }
        private static void NearOne(Player p, QuestDef q, StageDef s, string place)
        {
            Vector3 at;
            if (place.Length == 0 || !StoryWhere.Find(place, out at)) return;
            if (StoryWhere.Apart(at, p.transform.position) <= SagaNear) { q.NearStage = s.Id; q.NearAt = Time.time; } // (the place itself: a search's wide ring is no nearness)
        }

        // the world passed stages with «saga:»: this player's entry of each, if he was there
        private static void Witnessed(Player p, QuestDef q)
        {
            bool leads = Leads(p, q);
            for (int i = q.Left.Count - 1; i >= 0; i--)
            {
                string id = q.Left[i].Key;
                bool mine = q.Mine == id, near = q.NearStage == id && q.Left[i].Value - q.NearAt <= SagaRecent;
                if (leads && (mine || near)) Story.AddSaga(p, q.Id + "." + id);
                else if (leads && Time.time - q.Left[i].Value < MineWait) continue; // (the server's answer to this game may still come)
                if (mine) q.Mine = "";
                q.Left.RemoveAt(i);
            }
        }

        // its state and its stage, whoever keeps them: the one way to ask
        internal static string State(Player p, QuestDef q)
        {
            if (!q.Server) return Story.Quest(p, q.Id);
            ReadWorld(q);
            return q.Raw.Length == 0 ? "" : q.Raw.StartsWith(DoneWord, System.StringComparison.Ordinal) ? Story.Done : Story.Active;
        }

        internal static string At(Player p, QuestDef q)
        {
            if (!q.Server) return Story.StageOf(p, q.Id);
            ReadWorld(q);
            return q.AtNow;
        }

        // by the quest's id (the conditions «quest», «step»): none of that name — what the character keeps
        internal static string StateOf(Player p, string id) { QuestDef q = Find(id); return q != null ? State(p, q) : Story.Quest(p, id); }

        // The condition «step»: standing at that stage — and not one already passed that only waits to move on: its event heard and its waits holding, the
        // pass not run yet (up to half a second), or a stage of the server's this game has passed and the server not answered yet (Latch). The user 2026-11-16:
        // Heid's «Хто були ті жінки?» (?step ch1 heid_ask) came again on the screen right after «Принесу.» had raised the stage's event, and a click gave «…»
        // Asked by the world (no player: a person's day «when step ch2 work3», a place's rule — StoryCond.Holds(null, …)): a server's quest stands where
        // the world says, whoever it leads (the review 2026-11-18: StageNow asks Leads, false with no player — such a «step» never held)
        internal static bool IsAt(Player p, string quest, string stage)
        {
            QuestDef q = Find(quest);
            StageDef s = q == null ? null : p == null && q.Server ? (State(null, q) == Story.Active ? q.Stage(At(null, q)) : null) : StageNow(p, q);
            return s != null && s.Id == stage && !Passing(p, q, s);
        }

        private static bool _passingAsked; // (its waits may ask «step» themselves: not round in a ring)
        private static bool Passing(Player p, QuestDef q, StageDef s)
        {
            if (q.Server && q.Latch == s.Id) return true;
            if (s.OnName.Length == 0 || s.Scene.Length > 0 || _passingAsked || !Pending(s.OnName, s.OnArgs)) return false;
            _passingAsked = true;
            try { return StoryCond.All(p, s.Wait); } finally { _passingAsked = false; }
        }

        // an event a stage waits for, raised and not yet gone through a pass (this pass's or the next one's)
        private static bool Pending(string name, string args)
        {
            foreach (StoryEvent e in _heard) if (StoryBus.Matches(e, name, args)) return true;
            return HeardEvent(name, args);
        }

        // The story has got to that stage of the quest, or past it — the condition «reached»: a story person's question whose time is over, «?!reached ch1 rite_end»
        // (the user 2026-11-15: what was not asked in its time is gone for good). By the stages' order in the file; done — past them all, but one ended by its own
        // «next: end» only up to that stage (a later version tells on: Resume); failed — past them all. A stage the quest comes back from (scroll_raven →
        // scroll_heart) is no good end: name the one after it. Asks the world for a quest of the server, the character for its own
        internal static bool Reached(Player p, string quest, string stage)
        {
            QuestDef q = Find(quest);
            int want = q != null ? IndexOf(q, stage) : -1;
            string st = want >= 0 ? State(p, q) : "";
            if (st.Length == 0) return false;
            if (st == Story.Active) return IndexOf(q, At(p, q)) >= want;
            if (st != Story.Done) return true;
            int end = IndexOf(q, q.Server ? q.EndedAt : Story.StageOf(p, EndedKey(q)));
            return end < 0 || end >= want;
        }
        private static int IndexOf(QuestDef q, string stage) { for (int i = 0; i < q.Stages.Count; i++) if (q.Stages[i].Id == stage) return i; return -1; }

        // The block of the story that goes on now: the latest one any of whose quests has begun (QuestDef.Block; none begun — the prologue, 0) — the condition
        // «chnow N». A story person's question lives only in its own chapter: not before it (a chapter's «wholechapter», «!reached …» hold before its quest starts) and not
        // after it (NpcTalk.Parse puts it on each). A quest of the server is the world's: a late player still in his own prologue is already in its chapter.
        // A few quests, each asked from what is read already
        internal static int ChapterNow(Player p)
        {
            Load();
            int now = 0;
            foreach (QuestDef q in Quests) if (q.Block > now && State(p, q).Length > 0) now = q.Block;
            return now;
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
            string state = State(p, q);
            if (q.Left.Count > 0) Witnessed(p, q); // (first: the last stage ends the quest, and then it is no longer active)
            if (state != Story.Active || !Leads(p, q)) return;
            StageDef s = q.Stage(At(p, q));
            if (s == null) { if (Time.time >= q.AskUntil) { Note(q.Id + ": нема етапу «" + q.AtNow + "» у файлі — з початку"); Ask(q, q.AtNow, q.First, false, false); } return; } // (a stage gone from the file: from the top, as a personal quest)
            Near(p, q, s);
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
            bool begins = to != "end" && State(null, q) != Story.Active, toldOn = to == "end" && was != null && was.Next == "end";
            StoryWorld.SetText(KeyOf(q), to == "end" ? DoneWord + (toldOn ? " " + was.Id : "") : owed ? to + " ! " + (from.Length > 0 ? from : "-") + " " + (ranDone ? "1" : "0") : to);
            if (to == "end" && !toldOn && q.Scratch.Length > 0) StoryWorld.Forget(q.Scratch); // (ended for good: its block's own keys go — CloseBefore says when else)
            if (begins) CloseBefore(q, 0);
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

        // the story told on since a quest of the server ended by its stage's own «next: end» («-done <stage>»: Resume, the character's, says why): that
        // stage leads on in the files read now — the quest is at the new stage again, its «start:» owed to a game it leads as any step's. The server,
        // every ResumeEvery s (a pass over the quests: a few words each)
        internal static float ResumeEvery = 5f;
        private static float _resumeNext;
        internal static void ServerResume()
        {
            if (Time.time < _resumeNext || ZNet.instance == null || !ZNet.instance.IsServer()) return;
            _resumeNext = Time.time + ResumeEvery;
            Load();
            foreach (QuestDef q in Quests)
            {
                if (!q.Server) continue;
                ReadWorld(q);
                StageDef s = q.EndedAt.Length > 0 ? q.Stage(q.EndedAt) : null;
                StageDef next = s != null && s.Next.Length > 0 && s.Next != "end" ? q.Stage(s.Next) : null;
                if (next == null) continue; // (still its end, or that stage gone from the files)
                StoryWorld.SetText(KeyOf(q), next.Start.Count > 0 ? next.Id + " ! " + s.Id + " 1" : next.Id); // («1»: the old stage's «done» ran at its end)
                Story.Log("track: " + q.Id + ": told on in a new version — after «" + s.Id + "» now «" + next.Id + "»" + (next.Start.Count > 0 ? " (its start owed to a game it leads)" : ""));
            }
        }

        // A block's own world keys («scratch:») and the end of a quest of the server (the review 2026-11-18: a chapter told on in a later version —
        // act 2 after the release of act 1 — must find its marks still there; they went with its end). Ended by its last stage's own «next: end» it may be told on (ServerResume
        // above): its keys stay. Ended any other way (a fork to «end», the author's) — they go at once (Asked). The story past it — a quest whose
        // «starts:» asks it done begins (chapter 3 after chapter 2) — closes it for good: its keys go, it is told on no more («-done» without its
        // stage), and so each quest done that began from it (its «starts:» names it: Eyvind's beside chapter 2). Every key costs a send to every game.
        // The server, on a quest's beginning only
        private static void CloseBefore(QuestDef q, int depth)
        {
            if (q.Starts == null || depth > 8) return;
            foreach (Cond c in q.Starts)
                if (!c.Not && c.A.Length > 1 && c.A[0] == "quest") { QuestDef b = Find(c.A[1]); if (b != null && b != q) Close(b, q, depth + 1); }
        }

        private static void Close(QuestDef b, QuestDef by, int depth)
        {
            if (!b.Server || depth > 8 || State(null, b) != Story.Done) return;
            if (b.EndedAt.Length > 0) StoryWorld.SetText(KeyOf(b), DoneWord);
            if (b.Scratch.Length > 0) StoryWorld.Forget(b.Scratch);
            Story.Log("track: " + b.Id + ": closed — «" + by.Id + "» begins after it" + (b.Scratch.Length > 0 ? ", its keys «" + b.Scratch + "» gone" : ""));
            foreach (QuestDef d in Quests)
            {
                if (d == b || d == by || d.Starts == null) continue;
                foreach (Cond c in d.Starts)
                    if (!c.Not && c.A.Length > 1 && c.A[1] == b.Id && (c.A[0] == "quest" || c.A[0] == "reached" || c.A[0] == "step")) { Close(d, by, depth + 1); break; }
            }
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
            QuestDef mine = ranDone ? Find(id) : null;
            if (mine != null) mine.Mine = from; // (this game passed it itself — not a fork, not the author's: its «saga:» is this player's, Witnessed)
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

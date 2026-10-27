#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Спільні дії» (the user, 2026-10-22): what the ravens of two players do together, started by the players. The
    // frame for all of them; the first is the chase, one raven of each (1 on 1).
    //   - You aim at another player and press «Догонялки удвох» in the orders circle (J), pick your raven (OrdersMenu);
    //     that player gets a card (RavenBook.Invite): they pick their raven with the mouse, or decline (the button, Esc,
    //     or InviteSeconds pass). Ravens at odds with each other (Bonds: Enemy) will not play: the raven refuses.
    //   - The one who invited runs the game (the host): its length, who chases first, every catch, the end — and sends
    //     them to every game, so all see the same score. Each owner gives its own raven the chase (Orders.GiveFor) and
    //     every game flies the two of them round the middle between the players (ChasePlan with a Game).
    //   - They just fly after each other for the game's time; a catch is a point, and then the roles change and so does
    //     the shape of the flight (ChasePlan.Shapes: the ring, an eight, a snake, a hill) — as many rounds as there are
    //     catches. Each raven flies its own speed (SpeedOf: its kind's top × its «Крило» — the one place any later
    //     speed bonus goes); on a shape other than the ring both fly the slower one's speed. When the two differ much
    //     in speed, now and then (GiveChance) the faster one, fleeing, gives way («піддавки») — so the slower raven
    //     catches it too.
    //   - At each catch the catcher says a short line with the score; at the end both sides hear who won, the winner
    //     may get coins (CoinChance, CoinMin–CoinMax, at most CoinsPerDay a game day) and counts a win for its nickname
    //     («Ловець» at 50); friends get along a bit better; every game is kept in the raven (wins and losses against
    //     each raven: the thoughts speak of it).
    //   - The game ends early when the players are far apart, one dies or leaves, or a raven is called back.
    //   - Each raven is flown by its owner's game only; it sends where its raven is (FlyRpc, FlyEvery) to the games
    //     near, and they fly that raven after it (ChasePlan.PairFollow) — everyone sees the same flight, wherever the
    //     houses and the lag would have sent a raven of their own reckoning (one stuck in a wall for one player only).
    internal static class JointPlay
    {
        internal static float InviteSeconds = 20f, MaxApart = 25f, ChaseMin = 90f, ChaseMax = 150f, StartRange = 20f;
        internal static float CoinChance = 0.4f, FriendGain = 5f;
        internal static float GiveChance = 0.3f, GiveGap = 0.08f; // «піддавки»: the chance a round, the speed gap (share) from which it may
        internal static int CoinMin = 30, CoinMax = 100, CoinsPerDay = 3;
        private const string InviteRpc = "RavenWatch_JInvite", AnswerRpc = "RavenWatch_JAnswer", StartRpc = "RavenWatch_JStart",
            CatchRpc = "RavenWatch_JCatch", EndRpc = "RavenWatch_JEnd", LeftRpc = "RavenWatch_JLeft", FlyRpc = "RavenWatch_JFly";
        internal static float FlyEvery = 0.1f, FlyFresh = 1.5f, FlyRange = 100f; // s between sends of where my raven is; s a sent place stays good (a lag spike: no flip to its own reckoning); m to whom
        internal const string RecordKey = "RavenWatch_chases", CoinsKey = "RavenWatch_chaseCoins";
        private const int RecordMax = 40; // ravens kept in one raven's record

        // ---------------------------------------------------------------- a game (kept on every game that hears of it)

        internal sealed class Game
        {
            public string Id;
            public readonly long[] Pid = new long[2];     // 0: the host (who invited), 1: the guest
            public readonly bool[] Munin = new bool[2];
            public readonly string[] Name = new string[2];
            public float Seconds, StartedAt;
            public readonly int[] Score = new int[2];
            public int Chaser;                              // the slot that chases now
            public int Shape, Give = -1;                    // this round's shape (ChasePlan.Shapes); the slot that gives way (-1: none)
            public readonly float[] Speed = new float[2];   // m/s each one's own (SpeedOf), taken at the start
            public readonly float[] Phase = new float[2];   // where on a shape the runner is
            public readonly bool[] PhaseOn = new bool[2];
            public bool Ended;
            // the flight's own (each game flies it: ChasePlan)
            public long HostPeer;                           // who sent the start: only its points and end count
            public float SwappedAt, Dir = 1f, Ring = 7f, RingNow = 3.5f, LookAt;
            private int _breathed = -1;
            public readonly Vector3[] Pos = new Vector3[2], Vel = new Vector3[2];
            public readonly float[] Seen = { -100f, -100f };
            // where the other games' ravens are, as their owners sent it (FlyRpc), and when it came; when mine was sent
            public readonly Vector3[] NetPos = new Vector3[2], NetVel = new Vector3[2];
            public readonly float[] NetAt = { -100f, -100f };
            public float SentAt = -100f;
            public float Lift, LiftNow;                     // m the flight is raised over the roofs round the middle (ChasePlan.RoofLift)
            internal bool Heard(int slot) { return Time.time - NetAt[slot] < FlyFresh; }
            private readonly Player[] _p = new Player[2];
            internal Player P(int i)
            {
                if (_p[i] != null && _p[i].GetPlayerID() == Pid[i]) return _p[i];
                _p[i] = null;
                foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetPlayerID() == Pid[i]) { _p[i] = o; break; }
                return _p[i];
            }
            internal int SlotOf(long pid) { return Pid[0] == pid ? 0 : Pid[1] == pid ? 1 : -1; }
            internal bool Host { get { Player me = Player.m_localPlayer; return me != null && me.GetPlayerID() == Pid[0]; } }
            // the middle between the two players (where they circle), or null
            internal bool Middle(out Vector3 mid)
            {
                Player a = P(0), b = P(1);
                mid = Vector3.zero;
                if (a == null || b == null) return false;
                mid = (a.transform.position + b.transform.position) * 0.5f;
                return true;
            }
            internal void Round() { SwappedAt = Time.time; PhaseOn[0] = PhaseOn[1] = false; }
            // the shapes' long side: along the line between the two players
            internal Vector3 Axis()
            {
                Player a = P(0), b = P(1);
                Vector3 d = a != null && b != null ? b.transform.position - a.transform.position : Vector3.forward;
                d.y = 0f;
                return d.sqrMagnitude > 0.25f ? d.normalized : Vector3.forward;
            }
            internal bool Pending { get { return SwappedAt > Time.time + 100f; } } // (the host sent a point: waiting for it back)
            // the ring as wide as the place lets it (as ChaseGame.Breathe, round the middle)
            internal void Breathe(Vector3 mid, float dt)
            {
                if (_breathed == Time.frameCount) return; // (both ravens fly it: once a frame)
                _breathed = Time.frameCount;
                if (Time.time >= LookAt)
                {
                    LookAt = Time.time + 3f;
                    // the same ring in every game (the shape and the score are the same everywhere): no dice
                    float want = Mathf.Lerp(ChasePlan.RingMin, ChasePlan.RingMax, Mathf.Repeat(Shape * 0.37f + (Score[0] + Score[1]) * 0.21f, 1f)), room = want;
                    Lift = ChasePlan.RoofLift(mid, want);
                    Vector3 at = mid + Vector3.up * (ChasePlan.Height + Lift);
                    RaycastHit hit;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 d = Quaternion.AngleAxis(k * 45f, Vector3.up) * Vector3.forward;
                        if (Physics.Raycast(at, d, out hit, want + 1.5f, RavenMotor.Mask, QueryTriggerInteraction.Ignore)) room = Mathf.Min(room, hit.distance - 1.5f);
                    }
                    Ring = Mathf.Max(ChasePlan.RingTight, room);
                }
                RingNow = Mathf.MoveTowards(RingNow, Ring, dt * 1.2f);
                LiftNow = Mathf.MoveTowards(LiftNow, Lift, dt * ChasePlan.LiftSpeed);
            }
        }

        // ---------------------------------------------------------------- where each raven is (its owner's game says)

        // my raven in a game: where it is now, to the games near (a few times a second)
        internal static void Send(Game g, int slot, Vector3 pos, Vector3 vel)
        {
            if (g == null || ZRoutedRpc.instance == null || Time.time - g.SentAt < FlyEvery) return;
            g.SentAt = Time.time;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            ZPackage pkg = null;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || Vector3.Distance(o.transform.position, pos) > FlyRange) continue;
                long peer = PeerOf(o);
                if (peer == 0L) continue;
                if (pkg == null) { pkg = new ZPackage(); pkg.Write(g.Id); pkg.Write(slot); pkg.Write(pos); pkg.Write(vel); }
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, FlyRpc, pkg);
            }
        }

        private static void OnFly(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(); int slot = pkg.ReadInt(); Vector3 pos = pkg.ReadVector3(), vel = pkg.ReadVector3();
            Game g;
            if (slot < 0 || slot > 1 || !Games.TryGetValue(id, out g) || g.Ended) return;
            Player owner = g.P(slot);
            if (owner == null || sender != PeerOf(owner)) return; // (only its owner says where it is)
            g.NetPos[slot] = pos; g.NetVel[slot] = vel; g.NetAt[slot] = Time.time;
        }

        private static readonly Dictionary<string, Game> Games = new Dictionary<string, Game>();

        // the game this raven of that player flies now (null: none)
        internal static Game ChaseOf(Player owner, bool munin, out int slot)
        {
            slot = -1;
            if (owner == null) return null;
            long pid = owner.GetPlayerID();
            foreach (Game g in Games.Values)
            {
                if (g.Ended) continue;
                int i = g.SlotOf(pid);
                if (i >= 0 && g.Munin[i] == munin) { slot = i; return g; }
            }
            return null;
        }

        internal static bool InGame(Player owner, bool munin) { int s; return ChaseOf(owner, munin, out s) != null; }

        // my own game now (null: none)
        internal static Game Mine()
        {
            Player me = Player.m_localPlayer;
            if (me == null) return null;
            foreach (Game g in Games.Values) if (!g.Ended && g.SlotOf(me.GetPlayerID()) >= 0) return g;
            return null;
        }

        // ---------------------------------------------------------------- may they play

        // that player has a raven on the shoulder that can play (their mod talks: CrossTalk's flag)
        internal static bool CanPlayWith(Player t)
        {
            if (t == null || t == Player.m_localPlayer || t.IsDead()) return false;
            foreach (bool m in new[] { false, true }) if (Shoulder.ModelOf(t, m) != null && CrossTalk.CanTalk(t, m)) return true;
            return false;
        }

        // others are on the server (the hint of the pair chase is shown only then)
        internal static bool Others { get { return ZNet.instance != null && ZNet.instance.GetNrOfPlayers() > 1; } }

        // why my raven cannot play now (null: it can)
        internal static string WhyNot(Player me, bool munin)
        {
            if (Mine() != null) return Lang.T("уже грає");
            return Orders.Why(me, munin, Orders.Chase);
        }

        // a raven's own speed in the air (m/s): its kind's top × its «Крило». Anything that makes a raven faster later
        // (a thing it wears, a boon) goes into Wing.Of, and so here
        internal static float SpeedOf(Player owner, bool munin) { return RavenFlight.For(munin).Top * Wing.Of(owner, munin); }

        // the host: whether the runner of the next round gives way (only on the ring — a shape is flown at one speed —
        // and only when it is the faster of two far apart in speed, now and then)
        private static int Give(Player a, bool ma, Player b, bool mb, int shape, int runner)
        {
            if (shape != 0 || a == null || b == null) return -1;
            float sa = SpeedOf(a, ma), sb = SpeedOf(b, mb);
            if (Mathf.Abs(sa - sb) / Mathf.Max(0.1f, Mathf.Min(sa, sb)) < GiveGap || (sa > sb ? 0 : 1) != runner) return -1;
            return UnityEngine.Random.value < GiveChance ? runner : -1;
        }

        private static bool Enemies(Player me, bool myMunin, Player o, bool theirMunin) { return Bonds.StateOf(Bonds.PairScore(me, myMunin, o, theirMunin)) == Bonds.State.Enemy; }

        // ---------------------------------------------------------------- the invitation

        private sealed class Ask { public string Id; public long Pid; public string Name; public bool Munin; public float Until; }
        private static Ask _out, _in; // mine sent, one to me
        private static Ask _accepted;  // the one I said yes to: only a start from that player, with that raven of mine, is mine
        private static bool _acceptedMunin;
        private static long _quietPid; private static float _quietUntil; // (declined: no new card from that player for a while)
        internal static float QuietSeconds = 30f;

        internal static bool Waiting { get { return _out != null && Time.time < _out.Until; } }
        
        // I invite that player, with this raven of mine
        internal static string Invite(Player t, bool myMunin)
        {
            Player me = Player.m_localPlayer;
            if (me == null || t == null) return Lang.T("Нема кого кликати.");
            if (!CanPlayWith(t)) return Lang.F("{0}: нема ворона, що міг би грати.", t.GetPlayerName());
            string why = WhyNot(me, myMunin);
            if (why != null) return Plugin.RavenName(myMunin) + ": " + why;
            if (Vector3.Distance(me.transform.position, t.transform.position) > StartRange) return Lang.T("Підійди ближче.");
            if (Waiting) return Lang.F("Уже чекаєш відповіді від {0}.", _out.Name);
            // at odds with every raven of theirs on the shoulder: mine will not even fly
            bool any = false, all = true;
            bool foe = false;
            foreach (bool m in new[] { false, true }) if (Shoulder.ModelOf(t, m) != null) { any = true; if (!Enemies(me, myMunin, t, m)) all = false; else foe = m; }
            if (any && all) { Line(me, myMunin, t, foe, "joint_refuse", null); return Lang.F("{0} не хоче гратися з воронами гравця {1}.", Plugin.RavenName(myMunin), t.GetPlayerName()); }
            long peer = PeerOf(t);
            if (peer == 0L || ZRoutedRpc.instance == null) return Lang.T("Не вийшло надіслати.");
            _out = new Ask { Id = Guid.NewGuid().ToString("N").Substring(0, 10), Pid = t.GetPlayerID(), Name = t.GetPlayerName(), Munin = myMunin, Until = Time.time + InviteSeconds + 2f };
            ZPackage pkg = new ZPackage();
            pkg.Write(_out.Id); pkg.Write(me.GetPlayerID()); pkg.Write(me.GetPlayerName()); pkg.Write(myMunin);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, InviteRpc, pkg);
            return Lang.F("{0} кличе {1} в догонялки. Чекаємо відповіді…", Plugin.RavenName(myMunin), t.GetPlayerName());
        }

        private static void OnInvite(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(); long pid = pkg.ReadLong(); string name = pkg.ReadString(); bool munin = pkg.ReadBool();
            name = Clean(name);
            Player me = Player.m_localPlayer;
            if (me == null || pid == me.GetPlayerID() || sender != PeerOf(FindPid(pid))) return; // (only that player itself)
            if (pid == _quietPid && Time.time < _quietUntil) { Answer(sender, id, false, false, "no"); return; } // (just said no to them)
            // not now (a fight, asleep, dead, already playing or asked): no card in the middle of it
            string busy = me.IsDead() ? "dead" : (Senses.Ready && (Senses.Fight || Senses.Sleeping)) ? "busy" : Mine() != null || (_in != null && Time.time < _in.Until) ? "busy" : null;
            bool canAny = WhyNot(me, false) == null || WhyNot(me, true) == null;
            if (busy == null && !canAny) busy = "noraven";
            if (busy != null) { Answer(sender, id, false, false, busy); return; }
            _in = new Ask { Id = id, Pid = pid, Name = name, Munin = munin, Until = Time.time + InviteSeconds };
            _inPeer = sender;
            RavenBook.ShowInvite(name, munin, InviteSeconds);
            ScreamFx.CawAll(me, munin, munin ? 0.95f : 1.3f, 0.5f);
        }
        private static long _inPeer;

        // the card's answer (the raven picked, or no): RavenBook.Invite calls these
        internal static string Accept(bool myMunin)
        {
            Player me = Player.m_localPlayer;
            Ask a = _in;
            if (me == null || a == null || Time.time > a.Until) { RavenBook.HideInvite(); return Lang.T("Запрошення вже минуло."); }
            string why = WhyNot(me, myMunin);
            if (why != null) return Plugin.RavenName(myMunin) + ": " + why;
            Player o = FindPid(a.Pid);
            if (o == null || Vector3.Distance(o.transform.position, me.transform.position) > MaxApart) { Decline("far"); return Lang.T("Гравець надто далеко."); }
            _in = null;
            RavenBook.HideInvite();
            if (Enemies(me, myMunin, o, a.Munin)) // the raven itself will not
            {
                Line(me, myMunin, o, a.Munin, "joint_refuse", null);
                Answer(_inPeer, a.Id, false, myMunin, "enemy");
                return null;
            }
            _accepted = a; _accepted.Until = Time.time + 15f; _acceptedMunin = myMunin;
            Answer(_inPeer, a.Id, true, myMunin, "");
            return null;
        }

        internal static void Decline(string why)
        {
            Ask a = _in;
            _in = null;
            RavenBook.HideInvite();
            if (a == null) return;
            if (why == "no") { _quietPid = a.Pid; _quietUntil = Time.time + QuietSeconds; }
            Answer(_inPeer, a.Id, false, false, why ?? "no");
        }

        // the card is up: who asks, which raven, how long still
        internal static bool Invited(out string name, out bool munin, out float left)
        {
            name = _in != null ? _in.Name : ""; munin = _in != null && _in.Munin; left = _in != null ? _in.Until - Time.time : 0f;
            return _in != null && left > 0f;
        }

        private static void Answer(long peer, string id, bool yes, bool munin, string why)
        {
            if (ZRoutedRpc.instance == null || peer == 0L) return;
            Player me = Player.m_localPlayer;
            ZPackage pkg = new ZPackage();
            pkg.Write(id); pkg.Write(yes); pkg.Write(munin); pkg.Write(why ?? ""); pkg.Write(me != null ? me.GetPlayerID() : 0L);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, AnswerRpc, pkg);
        }

        // the answer to my invitation: a game, or why not
        private static void OnAnswer(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(); bool yes = pkg.ReadBool(), theirMunin = pkg.ReadBool(); string why = pkg.ReadString(); long pid = pkg.ReadLong();
            Player me = Player.m_localPlayer;
            Ask a = _out;
            if (me == null || a == null || a.Id != id || a.Pid != pid || sender != PeerOf(FindPid(pid))) return;
            _out = null;
            if (!yes)
            {
                string said = why == "enemy" ? Lang.F("Ворон гравця {0} не хоче гратися з твоїм.", a.Name)
                    : why == "busy" || why == "dead" ? Lang.F("{0} зараз не до догонялок.", a.Name)
                    : why == "noraven" ? Lang.F("У {0} нема вільного ворона.", a.Name)
                    : why == "far" ? Lang.F("{0} надто далеко.", a.Name)
                    : Lang.F("{0}: ні, без догонялок.", a.Name);
                me.Message(MessageHud.MessageType.Center, said);
                return;
            }
            Player o = FindPid(pid);
            string mineWhy = WhyNot(me, a.Munin);
            if (o == null || mineWhy != null) { me.Message(MessageHud.MessageType.Center, mineWhy != null ? Plugin.RavenName(a.Munin) + ": " + mineWhy : Lang.T("Гравця вже нема поруч.")); return; }
            // the game: its length and who chases first; to everyone (and here)
            ZPackage s = new ZPackage();
            s.Write(Guid.NewGuid().ToString("N").Substring(0, 10));
            s.Write(me.GetPlayerID()); s.Write(me.GetPlayerName()); s.Write(a.Munin);
            s.Write(pid); s.Write(a.Name); s.Write(theirMunin);
            s.Write(UnityEngine.Random.Range(ChaseMin, ChaseMax));
            int chaser = UnityEngine.Random.value < 0.5f ? 0 : 1, shape = UnityEngine.Random.Range(0, ChasePlan.Shapes);
            s.Write(chaser); s.Write(shape); s.Write(Give(me, a.Munin, o, theirMunin, shape, 1 - chaser));
            s.Write(UnityEngine.Random.value < 0.5f ? 1f : -1f); // the way round: the same in every game
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, StartRpc, s); // (everybody: this game too)
        }

        // ---------------------------------------------------------------- the game

        private static void OnStart(long sender, ZPackage pkg)
        {
            Game g = new Game();
            g.Id = pkg.ReadString();
            g.Pid[0] = pkg.ReadLong(); g.Name[0] = Clean(pkg.ReadString()); g.Munin[0] = pkg.ReadBool();
            g.Pid[1] = pkg.ReadLong(); g.Name[1] = Clean(pkg.ReadString()); g.Munin[1] = pkg.ReadBool();
            g.Seconds = Mathf.Clamp(pkg.ReadSingle(), 30f, 300f);
            g.Chaser = Mathf.Clamp(pkg.ReadInt(), 0, 1);
            g.Shape = Mathf.Clamp(pkg.ReadInt(), 0, ChasePlan.Shapes - 1); g.Give = Mathf.Clamp(pkg.ReadInt(), -1, 1);
            g.StartedAt = Time.time;
            g.Round(); g.SwappedAt += 1.5f; // (off the shoulders first)
            g.Dir = pkg.GetPos() < pkg.Size() ? (pkg.ReadSingle() < 0f ? -1f : 1f) : (UnityEngine.Random.value < 0.5f ? 1f : -1f); // (an older host sends none)
            g.HostPeer = sender;
            Player me = Player.m_localPlayer;
            if (me == null || Games.ContainsKey(g.Id)) return;
            Player host = g.P(0);
            if (host == null || sender != PeerOf(host) || Vector3.Distance(host.transform.position, me.transform.position) > 120f) return; // (sent by that player itself; far off: nothing to see)
            int mine = g.SlotOf(me.GetPlayerID());
            if (mine == 1) // the guest: only the start of the game I said yes to
            {
                Ask ok = _accepted;
                if (ok == null || ok.Pid != g.Pid[0] || Time.time > ok.Until || _acceptedMunin != g.Munin[1]) return;
                _accepted = null;
            }
            Prune();
            Games[g.Id] = g;
            for (int i = 0; i < 2; i++) g.Speed[i] = SpeedOf(g.P(i), g.Munin[i]);
            if (mine < 0) return;
            // my raven takes the chase (it pays as any chase); it cannot: the host is told and ends it
            if (!Orders.GiveFor(me, g.Munin[mine], Orders.Chase, g.Seconds + 10f)) { Left(g, "cannot"); return; }
            Player o = g.P(1 - mine);
            if (o != null) Line(me, g.Munin[mine], o, g.Munin[1 - mine], "joint_start", null);
        }

        private static readonly List<string> Gone = new List<string>();
        private static void Prune()
        {
            Gone.Clear();
            foreach (KeyValuePair<string, Game> kv in Games) if (kv.Value.Ended) Gone.Add(kv.Key);
            foreach (string k in Gone) Games.Remove(k);
        }

        // the host's game saw a catch (ChasePlan): the point and the next round (its shape; the catcher runs now) to everyone
        internal static void HostCaught(Game g, int catcher)
        {
            if (g == null || g.Ended || !g.Host) return;
            g.SwappedAt = Time.time + 1000f; // (once: the round comes back with the point)
            int shape = ChasePlan.NextShape(g.Shape);
            ZPackage pkg = new ZPackage();
            pkg.Write(g.Id); pkg.Write(catcher); pkg.Write(g.Score[0] + (catcher == 0 ? 1 : 0)); pkg.Write(g.Score[1] + (catcher == 1 ? 1 : 0));
            pkg.Write(shape); pkg.Write(Give(g.P(0), g.Munin[0], g.P(1), g.Munin[1], shape, catcher));
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, CatchRpc, pkg); // (everybody: this game too)
        }

        private static void OnCatch(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(); int catcher = Mathf.Clamp(pkg.ReadInt(), 0, 1); int s0 = Mathf.Clamp(pkg.ReadInt(), 0, 99), s1 = Mathf.Clamp(pkg.ReadInt(), 0, 99);
            int shape = Mathf.Clamp(pkg.ReadInt(), 0, ChasePlan.Shapes - 1), give = Mathf.Clamp(pkg.ReadInt(), -1, 1);
            Game g;
            if (!Games.TryGetValue(id, out g) || g.Ended || sender != g.HostPeer) return;
            g.Score[0] = s0; g.Score[1] = s1;
            g.Chaser = 1 - catcher; // the caught one is after it now, the other way round, on another shape
            g.Shape = shape; g.Give = give;
            g.Dir = -g.Dir;
            g.Round();
            Player me = Player.m_localPlayer;
            if (me == null) return;
            int mine = g.SlotOf(me.GetPlayerID());
            if (mine < 0) return;
            Player o = g.P(1 - mine);
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["score"] = g.Score[mine] + ":" + g.Score[1 - mine];
            if (mine == catcher) { if (o != null) Line(me, g.Munin[mine], o, g.Munin[1 - mine], "joint_catch", v); }
            else if (o != null && UnityEngine.Random.value < 0.35f) Line(me, g.Munin[mine], o, g.Munin[1 - mine], "joint_caught", v);
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("Догонялки: {0} {1} — {2} {3}", Plugin.RavenName(g.Munin[mine]), g.Score[mine], g.Score[1 - mine], g.Name[1 - mine]));
        }

        // my raven left the game (called back, an order, it could not start): the host ends it
        private static void Left(Game g, string why)
        {
            Player me = Player.m_localPlayer;
            if (g == null || me == null) return;
            if (g.Host) { End(g, why); return; }
            Player host = g.P(0);
            long peer = PeerOf(host);
            if (peer == 0L || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(g.Id); pkg.Write(why);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, LeftRpc, pkg);
        }

        private static void OnLeft(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(), why = pkg.ReadString();
            Game g;
            if (Games.TryGetValue(id, out g) && !g.Ended && g.Host && sender == PeerOf(g.P(1))) End(g, why == "cannot" ? why : "left"); // (from the guest only)
        }

        // the host ends the game: to everyone
        private static void End(Game g, string why)
        {
            if (g == null || g.Ended) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(g.Id); pkg.Write(g.Score[0]); pkg.Write(g.Score[1]); pkg.Write(why ?? "");
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, EndRpc, pkg); // (everybody: this game too)
        }

        private static void OnEnd(long sender, ZPackage pkg)
        {
            string id = pkg.ReadString(); int s0 = Mathf.Clamp(pkg.ReadInt(), 0, 99), s1 = Mathf.Clamp(pkg.ReadInt(), 0, 99); string why = pkg.ReadString();
            Game g;
            if (!Games.TryGetValue(id, out g) || g.Ended || sender != g.HostPeer) return;
            g.Ended = true;
            if (why.Length == 0 && Time.time - g.StartedAt < g.Seconds - 10f) why = "early"; // (a full game only: no win from an end sent early)
            g.Score[0] = s0; g.Score[1] = s1;
            Player me = Player.m_localPlayer;
            if (me == null) return;
            int mine = g.SlotOf(me.GetPlayerID());
            if (mine < 0) return;
            bool munin = g.Munin[mine];
            if (Orders.Of(me, munin) == Orders.Chase) Orders.ReturnQuiet(me, munin);
            Player o = g.P(1 - mine);
            int my = g.Score[mine], their = g.Score[1 - mine];
            if (why.Length > 0) // cut short: no winner
            {
                me.Message(MessageHud.MessageType.Center, Lang.F("Догонялки перервано: {0}", WhyWord(why)));
                return;
            }
            me.Message(MessageHud.MessageType.Center, Lang.F("Догонялки: твій {0} — {1}, {2} гравця {3} — {4}", Plugin.RavenName(munin), my, Plugin.RavenName(g.Munin[1 - mine]), g.Name[1 - mine], their));
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["score"] = my + ":" + their;
            if (o != null) Line(me, munin, o, g.Munin[1 - mine], my > their ? "joint_won" : my < their ? "joint_lost" : "joint_draw", v);
            // the record against that raven, the nickname, the bond, the coins
            string rid = o != null ? Bonds.RidOf(o, g.Munin[1 - mine]) : "";
            Record(RavenSeat.On(me, munin), rid, my > their, my < their);
            if (o != null) BondLog.Note(RavenSeat.On(me, munin), o, g.Munin[1 - mine], -1); // (met: the acquaintance's day; the score is in the record)
            if (o != null && Bonds.StateOf(Bonds.PairScore(me, munin, o, g.Munin[1 - mine])) != Bonds.State.Enemy) Bonds.Change(me, munin, o, g.Munin[1 - mine], FriendGain);
            ItemDrop.ItemData item = RavenSeat.On(me, munin);
            if (item != null) RavenJournal.Note(me, item, Lang.F("догонялки з вороном гравця {0}: {1}:{2}", g.Name[1 - mine], my, their));
            if (my > their)
            {
                Nicks.OnChaseWin(me, munin);
                Reward(me);
            }
        }

        private static string WhyWord(string why)
        {
            switch (why)
            {
                case "far": return Lang.T("гравці розійшлися");
                case "gone": return Lang.T("гравця вже нема");
                case "dead": return Lang.T("хтось загинув");
                case "left": return Lang.T("ворона покликали назад");
                case "cannot": return Lang.T("ворон не зміг злетіти");
                default: return Lang.T("гру зупинено");
            }
        }

        // the winner: now and then some coins, a few times a game day at most
        private static void Reward(Player me)
        {
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0, n = 0, d;
            string raw;
            if (me.m_customData.TryGetValue(CoinsKey, out raw))
            {
                string[] f = raw.Split(';');
                if (f.Length == 2 && int.TryParse(f[0], out d) && d == day) int.TryParse(f[1], out n);
            }
            if (n >= CoinsPerDay || UnityEngine.Random.value >= CoinChance) return;
            int coins = UnityEngine.Random.Range(CoinMin, CoinMax + 1);
            me.m_customData[CoinsKey] = day.ToString(CultureInfo.InvariantCulture) + ";" + (n + 1).ToString(CultureInfo.InvariantCulture);
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Coins") : null;
            if (go == null) return;
            if (!me.GetInventory().AddItem(go, coins))
            {
                GameObject drop = UnityEngine.Object.Instantiate(go, me.transform.position + me.transform.forward + Vector3.up, Quaternion.identity);
                ItemDrop it = drop.GetComponent<ItemDrop>();
                if (it != null) it.SetStack(coins);
            }
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("За перемогу в догонялках: {0}", Lang.N(coins, "монета|монети|монет")));
        }

        // ---------------------------------------------------------------- the record (in the raven): wins and losses against each raven

        private static void Record(ItemDrop.ItemData item, string rid, bool won, bool lost)
        {
            if (item == null || string.IsNullOrEmpty(rid)) return;
            Dictionary<string, int[]> r = Records(item);
            int[] wl;
            if (!r.TryGetValue(rid, out wl)) { wl = new int[2]; r[rid] = wl; }
            if (won) wl[0]++;
            if (lost) wl[1]++;
            StringBuilder sb = new StringBuilder();
            // the one just played first, then the rest as they were: the ones not played longest fall off the end
            sb.Append(rid).Append('=').Append(wl[0]).Append(':').Append(wl[1]);
            int n = 1;
            foreach (KeyValuePair<string, int[]> kv in r)
            {
                if (kv.Key == rid) continue;
                if (++n > RecordMax) break;
                sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value[0]).Append(':').Append(kv.Value[1]);
            }
            item.m_customData[RecordKey] = sb.ToString();
        }

        private static Dictionary<string, int[]> Records(ItemDrop.ItemData item)
        {
            Dictionary<string, int[]> r = new Dictionary<string, int[]>();
            string raw;
            if (item == null || !item.m_customData.TryGetValue(RecordKey, out raw) || string.IsNullOrEmpty(raw)) return r;
            foreach (string part in raw.Split(';'))
            {
                int eq = part.IndexOf('='), colon = part.LastIndexOf(':');
                int w, l;
                if (eq > 0 && colon > eq && int.TryParse(part.Substring(eq + 1, colon - eq - 1), out w) && int.TryParse(part.Substring(colon + 1), out l)) r[part.Substring(0, eq)] = new[] { w, l };
            }
            return r;
        }

        // my raven's wins and losses against the raven with this id (the book's acquaintances)
        internal static bool ChaseWith(ItemDrop.ItemData item, string rid, out int wins, out int losses)
        {
            wins = losses = 0;
            int[] wl;
            if (item == null || string.IsNullOrEmpty(rid) || !Records(item).TryGetValue(rid, out wl)) return false;
            wins = wl[0]; losses = wl[1];
            return true;
        }

        // my raven's record against that player's ravens (the thought speaks of it): the one with the most games
        internal static bool RecordWith(Player me, bool myMunin, Player o, out bool theirMunin, out int wins, out int losses)
        {
            theirMunin = false; wins = losses = 0;
            Dictionary<string, int[]> r = Records(RavenSeat.On(me, myMunin));
            if (r.Count == 0 || o == null) return false;
            int best = 0;
            foreach (bool m in new[] { false, true })
            {
                int[] wl;
                if (!r.TryGetValue(Bonds.RidOf(o, m), out wl) || wl[0] + wl[1] <= best) continue;
                best = wl[0] + wl[1]; theirMunin = m; wins = wl[0]; losses = wl[1];
            }
            return best > 0;
        }

        // ---------------------------------------------------------------- twice a second (RavenSkills)

        private static readonly List<Game> Ticking = new List<Game>();

        // a new character, or the world left: nothing carries over
        internal static void Reset()
        {
            Games.Clear(); _in = _out = _accepted = null; _quietPid = 0L;
            RavenBook.HideInvite();
        }

        internal static void Tick(Player me)
        {
            if (me == null || (Games.Count == 0 && _in == null && _out == null)) return;
            if (_in != null && Time.time > _in.Until) Decline("time");
            if (_out != null && Time.time > _out.Until) { me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0}: відповіді на догонялки нема.", _out.Name)); _out = null; }
            Prune(); // (the ended ones go: nothing asks for them any more)
            Ticking.Clear(); Ticking.AddRange(Games.Values); // (End changes the games)
            foreach (Game g in Ticking)
            {
                if (g.Ended) continue;
                int mine = g.SlotOf(me.GetPlayerID());
                // my raven no longer in it (called back, another order, the order ended): it has left the game
                if (mine >= 0 && Time.time - g.StartedAt > 4f && Orders.Of(me, g.Munin[mine]) != Orders.Chase) { Left(g, "left"); if (!g.Host) g.Ended = true; continue; }
                if (!g.Host) { if (Time.time - g.StartedAt > g.Seconds + 30f) g.Ended = true; continue; } // (the host never ended it: forgotten)
                Player a = g.P(0), b = g.P(1);
                string why = a == null || b == null ? "gone" : a.IsDead() || b.IsDead() ? "dead" : Vector3.Distance(a.transform.position, b.transform.position) > MaxApart ? "far"
                    : Time.time - g.StartedAt > 6f && Orders.Of(b, g.Munin[1]) != Orders.Chase ? "left" : null; // (the guest's raven never took off, or went home)
                if (why != null) { End(g, why); continue; }
                if (Time.time - g.StartedAt >= g.Seconds) End(g, "");
            }
        }

        // ---------------------------------------------------------------- the words

        // a line of my raven to theirs (over mine, everyone near sees it)
        private static void Line(Player me, bool munin, Player o, bool theirMunin, string tag, Dictionary<string, string> extra)
        {
            if (Plugin.Instance == null || me == null || o == null) return;
            Dictionary<string, string> v = extra != null ? new Dictionary<string, string>(extra) : new Dictionary<string, string>();
            v["name"] = me.GetPlayerName(); v["friend"] = o.GetPlayerName();
            v["raven"] = Nicks.Name(o, theirMunin); v["addr"] = Nicks.Call(o, theirMunin); v["me"] = Nicks.Name(me, munin);
            int level = Mathf.Max(1, Trust.LevelWorn(munin));
            string line = Composer.ComposeCross(Plugin.Instance.PhraseDir, munin, tag, level, Grammar.Female(me), Grammar.Female(o), v, Voice.CodeOn(me, munin));
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(me, munin, line);
        }

        // ---------------------------------------------------------------- the network

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(InviteRpc, new Action<long, ZPackage>(OnInvite));
            ZRoutedRpc.instance.Register<ZPackage>(AnswerRpc, new Action<long, ZPackage>(OnAnswer));
            ZRoutedRpc.instance.Register<ZPackage>(StartRpc, new Action<long, ZPackage>(OnStart));
            ZRoutedRpc.instance.Register<ZPackage>(CatchRpc, new Action<long, ZPackage>(OnCatch));
            ZRoutedRpc.instance.Register<ZPackage>(EndRpc, new Action<long, ZPackage>(OnEnd));
            ZRoutedRpc.instance.Register<ZPackage>(LeftRpc, new Action<long, ZPackage>(OnLeft));
            ZRoutedRpc.instance.Register<ZPackage>(FlyRpc, new Action<long, ZPackage>(OnFly));
        }

        internal static long PeerOf(Player o) { ZNetView nv = o != null ? o.GetComponent<ZNetView>() : null; return nv != null && nv.IsValid() ? nv.GetZDO().GetOwner() : 0L; }
        // a name from the network, shown in rich text: no tags, not endless
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            s = s.Replace("<", "").Replace(">", "");
            return s.Length > 32 ? s.Substring(0, 32) : s;
        }
        private static Player FindPid(long pid) { foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetPlayerID() == pid) return o; return null; }

        // ---------------------------------------------------------------- the console: raven joint [invite <player> [h|m]|accept [h|m]|no|games]
        internal static string Cmd(Player me, string[] a)
        {
            string w = a.Length > 1 ? a[1].ToLowerInvariant() : "games";
            if (w == "invite" && a.Length > 2)
            {
                Player t = null;
                foreach (Player o in Player.GetAllPlayers()) if (o != null && o != me && o.GetPlayerName().ToLowerInvariant() == a[2].ToLowerInvariant()) t = o;
                return t == null ? Lang.F("Нема гравця «{0}» поруч.", a[2]) : Invite(t, a.Length > 3 && a[3].ToLowerInvariant().StartsWith("m"));
            }
            if (w == "accept") return Accept(a.Length > 2 && a[2].ToLowerInvariant().StartsWith("m")) ?? Lang.T("Прийнято.");
            if (w == "no") { Decline("no"); return Lang.T("Відхилено."); }
            StringBuilder sb = new StringBuilder();
            foreach (Game g in Games.Values) sb.Append(g.Id).Append(": ").Append(g.Name[0]).Append(' ').Append(g.Score[0]).Append(" — ").Append(g.Score[1]).Append(' ').Append(g.Name[1]).Append(g.Ended ? " (кінець)" : "").Append('\n');
            return sb.Length > 0 ? sb.ToString().TrimEnd() : Lang.T("Ігор нема. raven joint invite [гравець] [h|m] · accept [h|m] · no");
        }
    }
}
#endif

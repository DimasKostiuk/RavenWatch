#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens of different players talking with each other: 2 to 4 ravens from 2 to 4 players (one raven each, or
    // two players and their pairs, or two and one...). A talk comes out of a moment, never out of a timer:
    //   - a meeting: another player comes near after a while apart (the first time ever: they get acquainted;
    //     after many talks together: old pals);
    //   - a boss just beaten with someone else near;
    //   - sitting by the same fire.
    // One game puts the whole talk together (who meets: the lower player id; a boss: whoever beat it; the fire: the
    // lowest id at it) from what every game publishes on its player (CrossTalk flag: which ravens may talk; Shoulder:
    // their trust level; the model: the owner's sex), and sends it to everybody as a script, so all see the same talk.
    // Each game keeps, in the character, how many talks its ravens had with each player: that is the whole "acquaintance".
    internal static class CrossTalk
    {
        internal const string FlagKey = "RavenWatch_Cross"; // bit 1: this game has the talks; 2: its Hugin may talk; 4: its Munin
        private const string Rpc = "RavenWatch_CrossTalk";
        private const string KinKey = "RavenWatch_kin";     // playerID:talks:day:Hugin's name:Munin's name;... (the players our ravens talked with, their ravens' names then)

        internal static bool Enabled = true;
        internal static float GapMinutes = 6f;    // between two such talks you are in (a boss: half)
        internal static float FireMinutes = 12f;  // by one fire: one talk this often
        internal const float MeetRange = 12f, ApartRange = 40f, GoneMinutes = 15f, OwedSeconds = 180f;
        internal const float FireRange = 8f, BossRange = 30f, HearRange = 30f;
        internal const int MaxLines = 6, MaxRavens = 4, PalsAt = 5;

        internal sealed class Who { public Player P; public bool Munin; public int Level; }
        internal struct Line { public int Who, To; public string Text; } // To: whom it speaks to

        private static readonly Dictionary<long, float> SeenAt = new Dictionary<long, float>();  // a player within ApartRange, last
        private static readonly Dictionary<long, float> Owed = new Dictionary<long, float>();    // came back after a while: a greeting owed since
        private static float _lookAt, _lastCross = -999f, _busyUntil, _fireNext, _bossUntil = -1f;
        private static string _boss;
        private static float _startedAt = -999f;                                         // when this game last put a talk together
        private static readonly List<Player> Started = new List<Player>();               // and whose ravens were in it
        private const int MaxText = 400;

        // ---------------------------------------------------------------- what this game tells the others

        // your raven may join a talk: yours, awake, here (not away at Odin's, not a stranger's, not sulking hostile)
        private static bool Ok(Player p, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(p, munin);
            return r != null && Trust.Active(r) && !Trust.Away(r) && !Trust.Stranger(r, p) && !Trust.Hostile(r);
        }

        private static void Publish(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            int bits = Enabled ? 1 | (Ok(p, false) ? 2 : 0) | (Ok(p, true) ? 4 : 0) : 0;
            if (zdo.GetInt(FlagKey, 0) != bits) zdo.Set(FlagKey, bits);
        }

        private static int Flags(Player o)
        {
            ZDO zdo = NetFlag.Zdo(o, false);
            return zdo != null ? zdo.GetInt(FlagKey, 0) : 0;
        }

        // this player's raven can talk right now: sitting on the shoulder a moment already, nobody of theirs talking
        private static bool Can(Player o, bool munin)
        {
            if (o == null || o.IsDead() || o.IsSleeping()) return false;
            if ((Flags(o) & (munin ? 4 : 2)) == 0) return false;
            if (o != Player.m_localPlayer && Shadow.Flagged(o)) return false;
            if (TalkBubble.Playing(o) || CrossBubble.Busy(o)) return false;
            return Shoulder.Settled(o, munin, o == Player.m_localPlayer ? 4f : 2f);
        }

        private static void Add(List<Who> list, Player o)
        {
            foreach (bool munin in new[] { false, true })
                if (Can(o, munin)) list.Add(new Who { P = o, Munin = munin, Level = Mathf.Max(1, Shoulder.LevelOn(o, munin)) });
        }

        // ---------------------------------------------------------------- the twice-a-second sync

        internal static void Tick(Player p)
        {
            if (p == null) return;
            Publish(p);
            float now = Time.time;
            if (!Enabled || now < _lookAt) return;
            _lookAt = now + 1f;
            if (_bossUntil > 0f && now > _bossUntil) { _bossUntil = -1f; _boss = null; }
            Player meet = null;
            bool anyone = false;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || (Flags(o) & 1) == 0) continue;
                long id = o.GetPlayerID();
                if (id == 0) continue; // just came: its id is not there yet
                float d = Vector3.Distance(o.transform.position, p.transform.position);
                if (d > ApartRange) continue;
                anyone = true;
                float last;
                if (!SeenAt.TryGetValue(id, out last) || now - last > GoneMinutes * 60f) Owed[id] = now; // back after a while (or the first time today)
                SeenAt[id] = now;
                float owedAt; PassTalk.Seen(p, o, id, d, now, Owed.TryGetValue(id, out owedAt) && now - owedAt <= OwedSeconds); // a word in passing: the same look, nothing new looked for
                float since;
                if (meet == null && d <= MeetRange && Owed.TryGetValue(id, out since))
                {
                    if (now - since > OwedSeconds) Owed.Remove(id);
                    else if (Decides(p, o)) meet = o; // of the two, the lower id puts the talk together
                }
            }
            if (!anyone) return; // nobody with ravens near: no talk can come (and the look for enemies below is not cheap)
            if (p.IsDead() || p.IsSleeping() || now < _busyUntil || TalkBubble.Playing(p) || CrossBubble.Busy(p)) return;
            if (Senses.Ready ? Senses.Fight || Senses.Attacking || Senses.Health01 < 0.35f // a foe within 25 m (looked for once a second), an archer afar, a swing, barely alive
                : BaseAI.HaveEnemyInRange(p, p.transform.position, 25f)) return;
            float gap = now - _lastCross;

            if (_boss != null && gap >= GapMinutes * 30f && Director.May(Director.Voice.Event))
            {
                Player o = Nearest(p, BossRange);
                if (o != null && Start(p, o, "boss", _boss, BossRange, true)) { _boss = null; _bossUntil = -1f; RavenTalk.Forget("boss"); return; }
            }
            if (meet != null && gap >= GapMinutes * 60f && Director.May(Director.Voice.Event))
            {
                string kind = Kind(p, meet);
                if (Start(p, meet, kind, null, MeetRange, UnityEngine.Random.value < 0.5f)) { Owed.Remove(meet.GetPlayerID()); return; }
            }
            if (Cozy.Cosy && now >= _fireNext && gap >= GapMinutes * 60f && Director.May(Director.Voice.Talk))
            {
                Player o = Nearest(p, FireRange);
                if (o == null) return;
                foreach (Player x in Player.GetAllPlayers()) // the lowest id at the fire with a raven that can talk tells it
                    if (x != null && x != p && (Flags(x) & 7) > 1 && !Decides(p, x) && Vector3.Distance(x.transform.position, p.transform.position) <= FireRange) return;
                if (Start(p, o, "fire", null, FireRange, UnityEngine.Random.value < 0.5f)) _fireNext = now + FireMinutes * 60f;
                else _fireNext = now + 30f;
            }
        }

        // a boss fell (the diary): a talk with whoever was near, for the next two minutes
        internal static void Boss(string token)
        {
            if (!Enabled) return;
            _boss = string.IsNullOrEmpty(token) ? "" : token;
            _bossUntil = Time.time + 120f;
            Player p = Player.m_localPlayer;
            if (p != null && AnyRavensNear(p, BossRange)) RavenTalk.Forget("boss"); // someone's ravens near (even out on a strike now): they talk of it together, not yours alone too
        }

        // another player with a raven that may talk, sitting or not
        private static bool AnyRavensNear(Player p, float range)
        {
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o != p && (Flags(o) & 7) > 1 && Vector3.Distance(o.transform.position, p.transform.position) <= range) return true;
            return false;
        }

        private static Player Nearest(Player p, float range)
        {
            Player best = null;
            float bd = range;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || (Flags(o) & 1) == 0 || (!Can(o, false) && !Can(o, true))) continue;
                float d = Vector3.Distance(o.transform.position, p.transform.position);
                if (d <= bd) { bd = d; best = o; }
            }
            return best;
        }

        // ---------------------------------------------------------------- acquaintance (in your character)

        private sealed class Rec { public int N, Day; public string H = "", M = ""; }

        private static Dictionary<long, Rec> Kin(Player p)
        {
            Dictionary<long, Rec> kin = new Dictionary<long, Rec>();
            string raw;
            if (p == null || !p.m_customData.TryGetValue(KinKey, out raw)) return kin;
            foreach (string e in raw.Split(';'))
            {
                string[] f = e.Split(':');
                long id; int n, day;
                if (f.Length >= 3 && long.TryParse(f[0], out id) && int.TryParse(f[1], out n) && int.TryParse(f[2], out day))
                    kin[id] = new Rec { N = n, Day = day, H = f.Length > 3 ? f[3] : "", M = f.Length > 4 ? f[4] : "" };
            }
            return kin;
        }

        // a talk with these players: one more each, and the names their ravens had
        private static void Met(Player p, List<Player> players)
        {
            Dictionary<long, Rec> kin = Kin(p);
            int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            foreach (Player o in players)
            {
                Rec k;
                if (!kin.TryGetValue(o.GetPlayerID(), out k)) k = new Rec();
                k.N = Mathf.Min(k.N + 1, 9999); k.Day = today;
                k.H = Known(o, false) ?? k.H; k.M = Known(o, true) ?? k.M;
                kin[o.GetPlayerID()] = k;
            }
            Keep(p, kin);
        }

        // the console's test dummy (raven dummy): your ravens already know its ravens this many talks
        internal static void Befriend(Player p, long id, int talks)
        {
            Dictionary<long, Rec> kin = Kin(p);
            Rec k;
            if (!kin.TryGetValue(id, out k)) k = new Rec();
            k.N = talks; k.Day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            kin[id] = k;
            Keep(p, kin);
        }

        private static void Keep(Player p, Dictionary<long, Rec> kin)
        {
            List<KeyValuePair<long, Rec>> all = new List<KeyValuePair<long, Rec>>(kin);
            all.Sort((a, b) => b.Value.Day.CompareTo(a.Value.Day)); // the most recent first: the oldest go when there are too many
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < all.Count && i < 60; i++)
            {
                if (sb.Length > 0) sb.Append(';');
                Rec r = all[i].Value;
                sb.Append(all[i].Key).Append(':').Append(r.N).Append(':').Append(r.Day).Append(':').Append(Safe(r.H)).Append(':').Append(Safe(r.M));
            }
            p.m_customData[KinKey] = sb.ToString();
        }

        private static string Safe(string s) { return string.IsNullOrEmpty(s) ? "" : s.Replace(":", "").Replace(";", ""); }

        // the nickname this player's raven goes by now, or null when it has none (an older mod)
        private static string Known(Player o, bool munin)
        {
            string name, call; Nicks.Kind k;
            return Nicks.Of(o, munin, out name, out call, out k) ? Safe(name) : null;
        }

        private static int Talks(Player p, Player o) { Rec k; return Kin(p).TryGetValue(o.GetPlayerID(), out k) ? k.N : 0; }

        // how the ravens know each other: never talked (get acquainted), one of theirs goes by a new name since the last
        // talk (yours notice it), old pals, or just know each other
        private static bool _renamedMunin;
        private static string Kind(Player p, Player o)
        {
            Rec k;
            if (!Kin(p).TryGetValue(o.GetPlayerID(), out k) || k.N <= 0) return "intro";
            foreach (bool m in new[] { false, true })
            {
                string was = m ? k.M : k.H, now = Known(o, m);
                if (!string.IsNullOrEmpty(was) && !string.IsNullOrEmpty(now) && was != now && Can(o, m)) { _renamedMunin = m; return "rename"; }
            }
            return k.N >= PalsAt ? "pals" : "greet";
        }

        // ---------------------------------------------------------------- a talk

        // Who talks: yours and theirs first, then anybody else near who can (4 ravens at most). A opens and B answers
        // (always of another player); C is anybody else who chimes in. mineFirst: your raven opens.
        private static bool Start(Player me, Player other, string kind, string boss, float range, bool mineFirst)
        {
            if (Plugin.Instance == null) return false;
            List<Who> mine = new List<Who>(), theirs = new List<Who>(), rest = new List<Who>();
            Add(mine, me);
            Add(theirs, other);
            if (kind == "rename")
            {
                List<Who> renamed = theirs.FindAll(w => w.Munin == _renamedMunin);
                if (renamed.Count > 0) { theirs = renamed; mineFirst = true; }
                else kind = Talks(me, other) >= PalsAt ? "pals" : "greet"; // the renamed one is off somewhere: a plain greeting
            }
            if (mine.Count == 0 || theirs.Count == 0) return false;
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o != me && o != other && (Flags(o) & 1) != 0 && Vector3.Distance(o.transform.position, me.transform.position) <= Mathf.Max(range, MeetRange)) Add(rest, o);
            Shuffle(mine); Shuffle(rest);
            if (kind != "rename") Shuffle(theirs);
            Who a = mineFirst ? mine[0] : theirs[0], b = mineFirst ? theirs[0] : mine[0];
            List<Who> whos = new List<Who> { a, b };
            List<Who> extra = new List<Who>();
            extra.AddRange(rest); // a third player first: a talk of three sides is livelier
            foreach (Who w in mine) if (w != a && w != b) extra.Add(w);
            foreach (Who w in theirs) if (w != a && w != b) extra.Add(w);
            foreach (Who w in extra) if (whos.Count < MaxRavens) whos.Add(w);
            List<Line> lines = null;
            for (int tries = 0; tries < 3 && lines == null; tries++) lines = Make(kind, whos, boss); // an answer off the thread is never taken: put together again
            if (lines == null) return false;
            Started.Clear();
            foreach (Who w in whos) Started.Add(w.P);
            _startedAt = Time.time;
            Send(kind, whos, lines);
            Play(kind, whos, lines);
            return true;
        }

        private static void Shuffle(List<Who> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); Who t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        // the kinds of talk (their steps: Plan)
        private static readonly HashSet<string> Scenes = new HashSet<string> { "intro", "greet", "pals", "boss", "fire", "rename", "about" }; // about: a thought about someone and their raven's answer (AboutTalk)

        private static float Chatty(string kind) { return kind == "fire" ? 0.75f : kind == "intro" || kind == "pals" ? 0.65f : 0.55f; }

        // A talk, longer with more ravens in it:
        //   two:   A opens, B answers, (A's last word)
        //   three: A opens, B answers, C joins in on the same thread, (A or B answers C), (A's last word)
        //   four:  ... and D joins in too (on C, or on the first two), and the answer goes to D
        // A step in () comes by a chance (by the fire they talk more); a join that does not come takes its answer with it.
        private struct Step { public int Who, To; public string Tag; public float Chance; }

        private static List<Step> Plan(string kind, int n, float chatty)
        {
            List<Step> plan = new List<Step>();
            plan.Add(new Step { Who = 0, To = 1, Tag = "x_" + kind + "_open", Chance = 1f });
            plan.Add(new Step { Who = 1, To = 0, Tag = "x_" + kind + "_reply", Chance = 1f });
            if (n >= 3)
            {
                plan.Add(new Step { Who = 2, To = UnityEngine.Random.Range(0, 2), Tag = "x_" + kind + "_join", Chance = 0.85f });
                if (n >= 4) plan.Add(new Step { Who = 3, To = UnityEngine.Random.value < 0.5f ? 2 : UnityEngine.Random.Range(0, 2), Tag = "x_" + kind + "_join", Chance = 0.75f });
                plan.Add(new Step { Who = UnityEngine.Random.Range(0, 2), To = -1, Tag = "x_join_reply", Chance = chatty }); // To: the last one who joined
            }
            plan.Add(new Step { Who = 0, To = 1, Tag = "x_" + kind + "_close", Chance = chatty });
            return plan;
        }

        private static List<Line> Make(string kind, List<Who> whos, string bossToken)
        {
            if (!Scenes.Contains(kind)) return null;
            string dir = Plugin.Instance.PhraseDir;
            Dictionary<string, string> boss = new Dictionary<string, string>();
            if (kind == "boss" && !string.IsNullOrEmpty(bossToken)) Composer.CreatureForms(dir, boss, "boss", bossToken);
            List<Line> lines = new List<Line>();
            List<string> said = new List<string>();
            string thread = null;
            int joined = -1; // the last raven that joined in
            _gossip = Gossip.ForceNext || UnityEngine.Random.value < Gossip.Chance; // this talk may speak of their people's real doings
            Gossip.ForceNext = false;
            try
            {
                foreach (Step st in Plan(kind, whos.Count, Chatty(kind)))
                {
                    if (lines.Count >= MaxLines) break;
                    bool join = st.Tag.EndsWith("_join"), answer = st.Tag == "x_join_reply";
                    if (answer && joined < 0) continue;                       // nobody joined: nobody to answer
                    // a question is always answered, and one who joined in is answered and the talk closed (no talk cut off mid-air)
                    bool must = !join && ((said.Count > 0 && RavenTalk.Asks(said[said.Count - 1])) || (joined >= 0 && (answer || st.Tag.EndsWith("_close"))));
                    if (!must && UnityEngine.Random.value > st.Chance) { if (join || answer) continue; break; }
                    int to = answer ? joined : st.To;
                    if (st.Who >= whos.Count || to < 0 || to >= whos.Count || to == st.Who) continue;
                    Patterns.ThreadWant = thread;
                    string line = null;
                    for (int tries = 0; tries < 4; tries++)
                    {
                        line = Compose(dir, whos[st.Who], whos[to], st.Tag, boss);
                        if (string.IsNullOrEmpty(line) || !Echoes(line, said)) break;
                    }
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (Patterns.ThreadNext != null) thread = Patterns.ThreadNext; // a chain: the next one picks up what was just said
                        else if (thread == null && Patterns.ThreadGot != null) thread = Patterns.ThreadGot;
                    }
                    if (string.IsNullOrEmpty(line))
                    {
                        if (st.Chance >= 1f) return null; // the opening or its answer cannot be said: no talk
                        continue;
                    }
                    lines.Add(new Line { Who = st.Who, To = to, Text = line });
                    said.Add(line);
                    if (join) joined = st.Who;
                }
            }
            finally { Patterns.ThreadWant = null; _gossip = false; }
            return lines.Count >= 2 ? lines : null;
        }

        // one line: {name} is the speaker's owner (their forms {він|вона}), {friend} the owner of the raven it speaks to
        // ({>він|вона}: that one's forms), {raven} that raven's nickname, {addr} how it is called («Кривдзьобе»),
        // {me} the speaker's own nickname, {kin} the other's kind (Хугін / Мунін)
        private static string Compose(string dir, Who s, Who to, string tag, Dictionary<string, string> boss)
        {
            Dictionary<string, string> v = new Dictionary<string, string>(boss);
            v["name"] = s.P.GetPlayerName();
            v["friend"] = to.P.GetPlayerName();
            v["raven"] = Nicks.Name(to.P, to.Munin);
            v["addr"] = Nicks.Call(to.P, to.Munin);
            v["me"] = Nicks.Name(s.P, s.Munin);
            v["kin"] = Plugin.RavenName(to.Munin);
            if (_gossip) { Gossip.Fill(v, s.P, s.Munin, "g_my_", dir); Gossip.Fill(v, to.P, to.Munin, "g_their_", dir); } // what really happened to them, now and then
            return Composer.ComposeCross(dir, s.Munin, tag, s.Level, Grammar.Female(s.P), Grammar.Female(to.P), v, Voice.CodeOn(s.P, s.Munin));
        }

        // three words in a row that an earlier line of the talk already said
        private static bool Echoes(string line, List<string> said)
        {
            string[] w = Words(line);
            for (int i = 0; i + 2 < w.Length; i++)
            {
                string three = " " + w[i] + " " + w[i + 1] + " " + w[i + 2] + " ";
                foreach (string l in said) if ((" " + string.Join(" ", Words(l)) + " ").Contains(three)) return true;
            }
            return false;
        }

        private static string[] Words(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant()) sb.Append(char.IsLetter(c) || c == '\'' || c == '’' ? c : ' ');
            return sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // ---------------------------------------------------------------- for AboutTalk

        internal static bool CanTalk(Player o, bool munin) { return Can(o, munin); }

        // of two players, whose game puts a talk (or a word in passing) together: the lower id; a test dummy has no game of
        // its own, so with one it is always yours
        internal static bool Decides(Player me, Player other)
        {
            if (Dummy.Is(other)) return true;
            return me.GetPlayerID() < other.GetPlayerID();
        }

        // one line of s to to (PassTalk); thread: what it answers, or null
        private static readonly Dictionary<string, string> NoBoss = new Dictionary<string, string>();
        private static bool _gossip; // Make rolled it for this talk
        internal static string Say(Who s, Who to, string tag, string thread)
        {
            if (Plugin.Instance == null) return null;
            Patterns.ThreadWant = thread;
            try { return Compose(Plugin.Instance.PhraseDir, s, to, tag, NoBoss); }
            finally { Patterns.ThreadWant = null; }
        }
        internal static int TalksWith(Player p, Player o) { return Talks(p, o); }

        // a talk put together elsewhere: sent to everybody and shown here
        internal static void Script(string kind, List<Who> whos, List<Line> lines)
        {
            if (whos == null || lines == null || lines.Count < 2) return;
            Send(kind, whos, lines);
            Play(kind, whos, lines);
        }

        // ---------------------------------------------------------------- everybody sees it (sent once per talk)

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnTalk));
        }

        private static void Send(string kind, List<Who> whos, List<Line> lines)
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(kind);
            pkg.Write(Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L);
            pkg.Write(whos.Count);
            foreach (Who w in whos) { pkg.Write(w.P.GetZDOID()); pkg.Write(w.Munin); }
            pkg.Write(lines.Count);
            foreach (Line l in lines) { pkg.Write(l.Who); pkg.Write(l.To); pkg.Write(l.Text); }
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnTalk(long sender, ZPackage pkg)
        {
            if (Player.m_localPlayer == null || sender == ZDOMan.GetSessionID()) return;
            try
            {
                string kind = pkg.ReadString();
                long by = pkg.ReadLong();
                if (!Scenes.Contains(kind)) return;
                int n = Mathf.Min(pkg.ReadInt(), MaxRavens);
                List<Who> whos = new List<Who>();
                for (int i = 0; i < n; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    bool munin = pkg.ReadBool();
                    Player found = null;
                    foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetZDOID() == id) { found = o; break; }
                    whos.Add(new Who { P = found, Munin = munin });
                }
                int m = Mathf.Min(pkg.ReadInt(), MaxLines);
                List<Line> lines = new List<Line>();
                for (int i = 0; i < m; i++) lines.Add(new Line { Who = pkg.ReadInt(), To = pkg.ReadInt(), Text = Clean(pkg.ReadString()) });
                foreach (Who w in whos) if (w.P == null) return; // somebody in it is not around here: nothing to see
                foreach (Line l in lines) if (l.Who < 0 || l.Who >= whos.Count || l.To < 0 || l.To >= whos.Count || l.Text.Length == 0) return;
                if (lines.Count < 2) return;
                // two games put a talk together at the same moment with some of the same ravens: the lower id's stays
                if (Time.time - _startedAt < 3f && by > Player.m_localPlayer.GetPlayerID())
                    foreach (Who w in whos) if (Started.Contains(w.P)) return;
                Play(kind, whos, lines);
            }
            catch (Exception e) { Log("Ravens' talk could not be read: " + e.Message); }
        }

        // a line from another game: no markup of its own (the bubble adds the names' colours), not endless
        internal static string Clean(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = t.Replace("<", "").Replace(">", "").Trim();
            return t.Length > MaxText ? t.Substring(0, MaxText) : t;
        }

        // on every game: the bubble, and for those in it the count of talks, the pause and the quiet around
        private static void Play(string kind, List<Who> whos, List<Line> lines)
        {
            Player me = Player.m_localPlayer;
            if (me == null || lines.Count == 0) return;
            Vector3 mid = Vector3.zero;
            foreach (Who w in whos) mid += w.P.transform.position;
            mid /= whos.Count;
            float dist = Vector3.Distance(me.transform.position, mid);
            if (dist > 100f) return;
            bool mine = false;
            List<long> others = new List<long>();
            List<Player> otherPlayers = new List<Player>();
            foreach (Who w in whos)
            {
                if (w.P == me) mine = true;
                else if (!others.Contains(w.P.GetPlayerID())) { others.Add(w.P.GetPlayerID()); otherPlayers.Add(w.P); }
            }
            float total = 2f;
            foreach (Line l in lines) total += TalkBubble.Pace(l.Text) * CrossBubble.Slower + 1.2f;
            if (mine)
            {
                Met(me, otherPlayers);
                List<long> famed = new List<long>(); // the players whose raven in it bore an earned name (gold or legend)
                foreach (Who w in whos)
                {
                    string nn, nc; Nicks.Kind nk;
                    if (w.P != me && Nicks.Of(w.P, w.Munin, out nn, out nc, out nk) && nk >= Nicks.Kind.Deed && !famed.Contains(w.P.GetPlayerID())) famed.Add(w.P.GetPlayerID());
                }
                foreach (Who w in whos) if (w.P == me && others.Count > 0) Nicks.OnTalk(me, w.Munin, others, famed); // a deed for its nickname
                _lastCross = Time.time;
                Director.Spoke(kind == "fire" ? Director.Voice.Fireside : Director.Voice.Talk, total);
                foreach (long id in others) { Owed.Remove(id); PassTalk.Shared(id); } // greeted: no second greeting for the same coming, nor a word in passing soon
                if (kind == "boss") { _boss = null; _bossUntil = -1f; RavenTalk.Forget("boss"); }
            }
            else if (dist < HearRange) Cozy.Quiet(total);
            if (dist < ApartRange) _busyUntil = Time.time + total;
            CrossBubble.Play(whos, lines, mine);
        }

        // ---------------------------------------------------------------- the console (raven cross [kind])

        internal static string Force(Player p, string kind)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (!Scenes.Contains(kind)) return Lang.T("Види: intro, greet, pals, boss, fire, rename.");
            string boss = kind == "boss" ? (string.IsNullOrEmpty(_boss) ? "$enemy_eikthyr" : _boss) : null;
            Player o = Nearest(p, 30f);
            if (o == null)
            {
                // nobody near: your own two play both sides, to see the bubble
                List<Who> solo = new List<Who>();
                Add(solo, p);
                if (solo.Count < 2) return Lang.T("Поруч нема гравця з воронами, а для показу самому потрібні обидва твої ворони на плечах.");
                List<Line> sl = null;
                for (int tries = 0; tries < 3 && sl == null; tries++) sl = Make(kind, solo, boss);
                if (sl == null) return Lang.F("Не склалося: нема фраз для «{0}».", kind);
                Send(kind, solo, sl);
                Play(kind, solo, sl);
                return Lang.F("Показ розмови «{0}» твоїми воронами (поруч нікого).", kind);
            }
            return Start(p, o, kind, boss, 30f, UnityEngine.Random.value < 0.5f) ? Lang.F("Розмова «{0}» з {1}.", kind, o.GetPlayerName()) : Lang.T("Не склалося: ворони не на плечах або нема фраз.");
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }
}
#endif

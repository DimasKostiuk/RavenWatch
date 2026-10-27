#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Стосунки» at the moments that matter (the user, 2026-10-23: «більше фраз контекстуальних для воронів, коли вони
    // одне одного люблять або ненавидять»): a friend or an enemy met again after days apart; a friend raven's person
    // in trouble or dead, an enemy's person brought down (a word from your shoulder); your two talking a foe over after
    // a quarrel; at the evening fire, a foe among the ravens. The people your raven likes or can't stand get the same
    // (PeopleMoments). The lines: BondMomentPatterns.
    internal static partial class Bonds
    {
        internal static float ReunionDays = 2f, ReunionChance = 0.8f, WatchRange = 25f, WatchGap = 120f;
        internal static float PairFoeChance = 0.4f, PairFoeAfter = 20f, FireFoeChance = 0.6f, FireFoeAfter = 30f;

        // ---------------------------------------------------------------- met again after a while

        // the pair's first look this session (Tick): a friend or an enemy not seen for days gets its own greeting
        private static bool Reunion(Player me, bool my, Player o, bool tm)
        {
            State st = StateOf(PairScore(me, my, o, tm));
            bool friend = st == State.Pals || st == State.Close;
            if (!friend && st != State.Enemy) return false;
            BondLog.Rec r = BondLog.Find(RavenSeat.On(me, my), RidOf(o, tm));
            int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (r == null || r.Last < 0 || today - r.Last < ReunionDays || UnityEngine.Random.value > ReunionChance) return false;
            CrossTalk.Who a = WhoOf(me, my), b = WhoOf(o, tm);
            string k = friend ? "bond_back_friend" : "bond_back_enemy";
            string l1 = Line(a, b, k + "_open"), l2 = l1 != null ? Line(b, a, k + "_reply") : null;
            if (l2 == null) return false;
            CrossTalk.Script(friend ? "bfriend" : "benemy", new List<CrossTalk.Who> { a, b },
                new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } });
            return true;
        }

        // ---------------------------------------------------------------- someone near brought low

        private sealed class Watched { public int State; public float Next; }
        private static readonly Dictionary<long, Watched> WatchOf = new Dictionary<long, Watched>();

        private static int Plight(Player o) { return o.IsDead() ? 2 : o.GetHealthPercentage() < 0.3f ? 1 : 0; }

        // every look (Tick): a player near goes down to nearly dead, or dies — my ravens have their word, by the bond with
        // that player's raven, else by what they think of the player
        private static void Watch(Player me)
        {
            if (me.IsDead()) return;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || Dummy.Is(o) || o.GetPlayerID() == 0L) continue;
                if (Vector3.Distance(o.transform.position, me.transform.position) > WatchRange) continue;
                long id = o.GetPlayerID();
                int now = Plight(o);
                Watched w;
                if (!WatchOf.TryGetValue(id, out w)) { if (WatchOf.Count > 32) WatchOf.Clear(); WatchOf[id] = new Watched { State = now }; continue; } // (first seen: as it is)
                int was = w.State;
                w.State = now;
                if (now <= was || Time.time < w.Next || Shadow.Flagged(o)) continue; // (only a fall: to nearly dead, to dead)
                if (Plight(me, o, now == 2)) w.Next = Time.time + WatchGap;
            }
        }

        private static bool Plight(Player me, Player o, bool dead)
        {
            if (RavenSkills.Hushed(me) || !Director.May(Director.Voice.Event)) return false;
            // the strongest bond of mine with one of their ravens
            bool bestMy = false, bestTm = false; float best = 0f; bool found = false;
            foreach (bool my in new[] { true, false })
            {
                if (!Free(me, my)) continue;
                foreach (bool tm in new[] { false, true })
                {
                    if (RidOf(o, tm).Length == 0) continue;
                    float s = PairScore(me, my, o, tm);
                    if (Mathf.Abs(s) > Mathf.Abs(best)) { best = s; bestMy = my; bestTm = tm; found = true; }
                }
            }
            State st = found ? StateOf(best) : State.Known;
            string tag = null;
            if (st == State.Pals || st == State.Close) tag = dead ? "bond_friend_died" : "bond_friend_hurt";
            else if (st == State.Enemy) tag = dead ? "bond_enemy_died" : "bond_enemy_hurt";
            if (tag != null)
            {
                string line = Line(WhoOf(me, bestMy), WhoOf(o, bestTm), tag);
                if (string.IsNullOrEmpty(line)) return false;
                RavenSkills.SayFromShoulder(me, bestMy, line);
                Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line));
                return true;
            }
            return People.OnPlight(me, o, dead); // no raven of theirs it cares about: what it thinks of them
        }

        // ---------------------------------------------------------------- your two of a foe, after a quarrel

        private sealed class FoeTalk { public bool Munin; public Player Of; public bool OfMunin; public float At, Until; }
        private static FoeTalk _foeTalk;

        private static void AfterQuarrel(bool my, Player o, bool tm)
        {
            if (UnityEngine.Random.value > PairFoeChance) return;
            _foeTalk = new FoeTalk { Munin = my, Of = o, OfMunin = tm, At = Time.time + PairFoeAfter, Until = Time.time + 120f };
        }

        private static void TalkFoe(Player me)
        {
            FoeTalk t = _foeTalk;
            if (t == null || Time.time < t.At) return;
            if (Time.time > t.Until || t.Of == null || !Free(me, t.Munin) || !Free(me, !t.Munin)) { _foeTalk = null; return; }
            if (RavenSkills.Hushed(me) || !Director.May(Director.Voice.Talk) || (Senses.Ready && Senses.Fight)) { t.At = Time.time + 5f; return; }
            _foeTalk = null;
            CrossTalk.Who a = WhoOf(me, t.Munin), b = WhoOf(me, !t.Munin), f = WhoOf(t.Of, t.OfMunin);
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["raven"] = Nicks.Name(f.P, f.Munin); v["friend"] = f.P.GetPlayerName();
            string l1 = PairLine(a, "bond_pair_enemy_open", v), l2 = l1 != null ? PairLine(b, "bond_pair_enemy_reply", v) : null;
            if (l2 == null) return;
            List<TalkBubble.Line> lines = new List<TalkBubble.Line> { new TalkBubble.Line { Munin = a.Munin, Text = l1 }, new TalkBubble.Line { Munin = b.Munin, Text = l2 } };
            TalkBubble.Play(me, lines, true);
            RavenTalk.Send(me, lines);
            Director.Spoke(Director.Voice.Talk, TalkBubble.Pace(l1) + TalkBubble.Pace(l2) + 2f);
        }

        // ---------------------------------------------------------------- a foe at the evening fire

        private sealed class FireFoe { public CrossTalk.Who A, B; public float At; }
        private static FireFoe _fireFoe;

        // the gathering's talk began (Gathering, the deciding game): one of mine sits there with an enemy of its
        internal static void AtFire(Player me, List<CrossTalk.Who> there)
        {
            if (!Enabled || there == null || UnityEngine.Random.value > FireFoeChance) return;
            foreach (CrossTalk.Who mine in there)
            {
                if (mine.P != me) continue;
                foreach (CrossTalk.Who o in there)
                {
                    if (o.P == null || o.P == me) continue;
                    if (StateOf(PairScore(me, mine.Munin, o.P, o.Munin)) != State.Enemy) continue;
                    _fireFoe = new FireFoe { A = mine, B = o, At = Time.time + FireFoeAfter };
                    return;
                }
            }
        }

        private static void TalkFire()
        {
            FireFoe f = _fireFoe;
            if (f == null || Time.time < f.At) return;
            _fireFoe = null;
            if (!Gathering.Active || f.A.P == null || f.B.P == null || f.B.P.IsDead()) return;
            string l1 = Line(f.A, f.B, "bond_fire_enemy_open"), l2 = l1 != null ? Line(f.B, f.A, "bond_fire_enemy_reply") : null;
            if (l2 == null) return;
            CrossTalk.Script("benemy", new List<CrossTalk.Who> { f.A, f.B },
                new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } });
        }

        // from Tick, every look
        private static void Moments(Player me)
        {
            TalkFoe(me);
            TalkFire();
            if (Enabled) Watch(me);
        }

        // «Зграя»: the double strike's word — mine, then theirs (Pack)
        internal static bool PackTalk(Player me, bool my, Player o, bool tm)
        {
            CrossTalk.Who a = WhoOf(me, my), b = WhoOf(o, tm);
            string l1 = Line(a, b, "bond_pack_open"), l2 = l1 != null ? Line(b, a, "bond_pack_reply") : null;
            if (l2 == null) return false;
            CrossTalk.Script("bfriend", new List<CrossTalk.Who> { a, b },
                new List<CrossTalk.Line> { new CrossTalk.Line { Who = 0, To = 1, Text = l1 }, new CrossTalk.Line { Who = 1, To = 0, Text = l2 } });
            return true;
        }

        internal static void ResetMoments() { WatchOf.Clear(); _foeTalk = null; _fireFoe = null; }
    }
}
#endif

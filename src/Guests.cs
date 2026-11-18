#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Guests: now and then a raven of yours flies over to a friend — another player's raven sitting on a perch near
    // (at home there, on guard or just resting) that yours counts as a friend (Bonds, FriendAt and up). It sits by it a
    // minute or two, the two say a word each (seen by everyone near), the friendship grows a little, and it comes back
    // with a word of its own. Your game drives it all: your raven, an ordinary sit on that perch (Perches.Sit, the
    // visit's mode only in your game), the host's answer in the host's own tone from its seat.
    internal static class Guests
    {
        internal const string VisitMode = "v";
        internal static float EveryMin = 480f, EveryMax = 900f; // s between two tries of one raven
        internal static float Chance = 0.5f, Reach = 40f, StayMin = 60f, StayMax = 120f, BackLine = 4f, CalmNear = 30f;

        private sealed class Visit { internal bool Munin; internal PerchView View; internal int Seat = -1, HostSeat; internal string Token, HostRid = "", Owner = ""; internal bool HostMunin, Talked; internal float Until, Since; internal long HostPid; }
        private static readonly Visit[] _visit = new Visit[2];
        private static readonly float[] _next = { -1f, -1f };

        internal static void Tick(Player p)
        {
            for (int k = 0; k < 2; k++)
            {
                bool munin = k == 1;
                if (_visit[k] != null) { Run(p, k); continue; }
                if (_next[k] < 0f) _next[k] = Time.time + Random.Range(EveryMin * 0.5f, EveryMax);
                if (Time.time < _next[k]) continue;
                _next[k] = Time.time + Random.Range(EveryMin, EveryMax);
                if (Random.value <= Chance) Try(p, munin);
            }
        }

        // a fight near you: nobody goes visiting now
        private static bool Calm(Player p)
        {
            foreach (Character c in Character.GetAllCharacters())
                if (Home.Hostile(c) && Home.Angry(c) && Vector3.Distance(c.transform.position, p.transform.position) < CalmNear) return false;
            return true;
        }

        private static void Try(Player p, bool munin)
        {
            ItemDrop.ItemData item, it;
            if (p == null || p.IsDead() || (item = RavenSeat.On(p, munin)) == null || Perches.WhyNot(p, munin, out it) != null || !Calm(p)) return;
            if (!Outing.ReadyRaven(p, munin) || Victim.Busy || Bonds.Visiting) return; // (sitting quietly on you: never pulled off your order, an outing, a chase or another visit)
            long me = p.GetPlayerID();
            PerchView best = null; int hostSeat = -1; float bestScore = Bonds.FriendAt - 0.01f;
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.Valid || v.IsSpot || v.Gone || v.FreeSeat() < 0 || Vector3.Distance(v.transform.position, p.transform.position) > Reach) continue;
                for (int i = 0; i < v.Count; i++)
                {
                    Perches.Occupant o = v.At(i);
                    float score, start;
                    if (o == null || o.Pid == me || o.Rid.Length == 0 || Home.Hunting(v, i) || !v.Settled(i)) continue;
                    if (!Bonds.Stored(item, o.Rid, out score, out start) || score <= bestScore) continue; // (only one it has met and counts a friend)
                    best = v; hostSeat = i; bestScore = score;
                }
            }
            if (best == null) return;
            Perches.Occupant host = best.At(hostSeat);
            int seat = best.FreeSeat();
            _visit[munin ? 1 : 0] = new Visit { Munin = munin, View = best, HostSeat = hostSeat, HostRid = host.Rid, HostMunin = host.Munin, Owner = host.Name, Since = Time.time, HostPid = host.Pid };
            Perches.Sit(p, best, munin, seat, VisitMode);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} полетів у гості до {1} гравця {2}.", Plugin.RavenName(munin), Plugin.RavenNameGen(host.Munin), host.Name), 0, RavenItem.Icon(munin));
        }

        // the perch took it (Perches.Confirm): it is on its way there
        internal static void Landed(bool munin, PerchView v, int seat, string token)
        {
            Visit vi = _visit[munin ? 1 : 0];
            if (vi == null || vi.View != v) return;
            vi.Seat = seat; vi.Token = token; vi.Until = Time.time + Random.Range(StayMin, StayMax);
        }

        private static void Run(Player p, int k)
        {
            Visit vi = _visit[k];
            ItemDrop.ItemData item = p != null ? RavenSeat.On(p, vi.Munin) : null;
            if (vi.Token == null) { if (Time.time - vi.Since > 8f) _visit[k] = null; return; } // the perch never took it
            if (item == null || !Perches.On(item) || vi.View == null || !vi.View.Valid) { _visit[k] = null; return; } // called back, taken off, gone
            Perches.Occupant host = vi.View.At(vi.HostSeat);
            bool hostHere = host != null && host.Rid == vi.HostRid;
            if (hostHere && !vi.Talked && vi.View.Settled(vi.Seat)) { vi.Talked = true; Talk(p, vi, item, host); }
            if (Time.time < vi.Until && hostHere) return;
            _visit[k] = null;
            Perches.CallQuiet(p, item, vi.Munin); // back home to you
            if (vi.Talked && Plugin.Instance != null) Plugin.Instance.StartCoroutine(Back(p, vi.Munin, vi.Owner, vi.HostPid, vi.HostMunin));
        }

        // The friend's name as everyone sees it: its nickname (its owner here; or from its own data at home), else its kind
        private static string HostName(Visit vi, Perches.Occupant host, Player owner)
        {
            if (owner != null) return Nicks.Name(owner, host.Munin);
            ItemDrop.ItemData data = Home.Cache(vi.View, vi.HostSeat);
            Nicks.Nick nick = data != null ? Nicks.Current(data) : null;
            return nick != null ? nick.Name : Plugin.RavenName(host.Munin);
        }

        // a word each (everyone near sees the two, by their names): its owner here — the visit scene of the bonds,
        // with that player in it; else yours to its friend and the friend's answer in its own tone
        private static void Talk(Player p, Visit vi, ItemDrop.ItemData item, Perches.Occupant host)
        {
            Player owner = PerchView.PlayerOf(host.Pid);
            string mine = Nicks.Name(p, vi.Munin), theirs = HostName(vi, host, owner);
            string hi = null, reply = null;
            if (owner != null) Bonds.PerchVisit(p, vi.Munin, owner, host.Munin, host.Level, out hi, out reply);
            else Bonds.Bump(item, host.Rid, Bonds.TalkGain); // (its owner away: the friendship grows all the same)
            if (string.IsNullOrEmpty(hi) || string.IsNullOrEmpty(reply))
            {
                hi = Satiety.Say(p, vi.Munin, "guest_hello", "host", theirs, "friend", host.Name);
                Dictionary<string, string> extra = new Dictionary<string, string>();
                extra["friend"] = p.GetPlayerName();
                reply = Plugin.Instance != null ? Composer.ComposePerch(Plugin.Instance.PhraseDir, host.Munin, "guest_reply", host.Level, host.Name, host.Female, extra, host.Voice) : null;
            }
            List<TalkBubble.Line> lines = new List<TalkBubble.Line>();
            if (!string.IsNullOrEmpty(hi)) lines.Add(new TalkBubble.Line { Munin = vi.Munin, Text = hi, Name = mine, To = theirs });
            if (!string.IsNullOrEmpty(reply)) lines.Add(new TalkBubble.Line { Munin = host.Munin, Text = reply, Name = theirs, To = mine });
            if (lines.Count > 0) { TalkBubble.PlayAt(vi.View, lines, true); Perches.SendTalk(vi.View.Id, lines); }
        }

        // home again: the gossip of the friend's owner (here: their real doings), else its own word of the visit
        private static System.Collections.IEnumerator Back(Player p, bool munin, string owner, long hostPid, bool hostMunin)
        {
            yield return new WaitForSeconds(BackLine + 3f);
            if (p == null || p.IsDead() || RavenSeat.On(p, munin) == null) yield break;
            Player of = PerchView.PlayerOf(hostPid);
            string line = of != null && Random.value < 0.6f ? Bonds.PerchTale(p, munin, of, hostMunin) : null;
            if (string.IsNullOrEmpty(line)) line = Satiety.Say(p, munin, "guest_back", "friend", owner);
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
        }

        // a new life or world: no visit under way
        internal static void Reset() { _visit[0] = _visit[1] = null; _next[0] = _next[1] = -1f; }
    }
}
#endif

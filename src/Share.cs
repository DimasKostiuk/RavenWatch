#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Діляться їжею» (the user, 2026-10-22): a fed raven gives a bite of its own to a hungry one.
    //   - Your own two: one hungry (Satiety.Hungry or less), the other well fed (it keeps enough for itself after): now
    //     and then the fed one digs a bite out and passes it over (ShareFill from one to the other), a word each in
    //     their bubble. Your game alone: both are yours.
    //   - A friend of another player (Bonds: Pals or more): your fed raven flies over to the hungry one and sits by it a
    //     moment (the friend's visit, Bonds.StartShare); landed, it hands the bite over: its own satiety here, theirs on
    //     their game (ShareRpc: that game checks its raven really is hungry and not at odds with yours).
    // Cheap: a look every LookEvery seconds from the sync, no physics; whether a raven is hungry is told to the others as
    // two bits on the player (HungryKey), written only when it changes.
    internal static class Share
    {
        internal static bool Enabled = true;
        internal static float ShareFill = 12f, LookEvery = 20f;
        internal static float OwnChance = 0.5f, OwnGapMinutes = 15f;      // your two: this often at most
        internal static float FriendChance = 0.4f, FriendGapMinutes = 25f; // a friend's raven: this often at most (per raven of yours)
        internal const string HungryKey = "RavenWatch_Hungry";            // bit 1: Hugin hungry, 2: Munin
        private const string Rpc = "RavenWatch_ShareFood";

        private static float _lookAt, _ownNext = -1f;
        private static readonly float[] FriendNext = { -1f, -1f };

        // a raven of mine that can give: on the shoulder a moment already, awake, mine, here, fed enough to give and keep
        private static bool CanGive(Player me, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(me, munin);
            return r != null && Trust.Active(r) && !Trust.Stranger(r, me) && !Trust.Away(r) && !Perches.On(r)
                && Satiety.Value(r) >= Satiety.AskAt + ShareFill && Quirks.Sulk(me, munin) == null && Shoulder.Settled(me, munin, 3f);
        }

        private static bool IsHungry(ItemDrop.ItemData r) { return r != null && Trust.Active(r) && !Trust.Away(r) && Satiety.Zone(r) <= Satiety.Hungry; }

        // From the sync (RavenSkills, twice a second): the bits told, and a look now and then
        internal static void Tick(Player me)
        {
            if (me == null || Time.time < _lookAt) return;
            _lookAt = Time.time + LookEvery;
            Publish(me);
            if (!Enabled || me.IsDead() || (Senses.Ready && (Senses.Fight || Senses.Sleeping || Senses.Swimming))) return;
            if (Own(me)) return;
            Friend(me);
        }

        private static void Publish(Player me)
        {
            ZDO z = NetFlag.Zdo(me, true);
            if (z == null) return;
            int bits = (IsHungry(RavenSeat.On(me, false)) ? 1 : 0) | (IsHungry(RavenSeat.On(me, true)) ? 2 : 0);
            if (z.GetInt(HungryKey, 0) != bits) z.Set(HungryKey, bits);
        }

        internal static bool HungryOn(Player o, bool munin)
        {
            ZDO z = NetFlag.Zdo(o, false);
            return z != null && (z.GetInt(HungryKey, 0) & (munin ? 2 : 1)) != 0;
        }

        // ---------------------------------------------------------------- your own two

        private static bool Own(Player me)
        {
            if (_ownNext < 0f) { _ownNext = Time.time + OwnGapMinutes * 30f; return false; } // (not right after loading in)
            if (Time.time < _ownNext) return false;
            foreach (bool giver in new[] { false, true })
            {
                ItemDrop.ItemData g = RavenSeat.On(me, giver), r = RavenSeat.On(me, !giver);
                if (!IsHungry(r) || !Shoulder.Settled(me, !giver, 3f) || !CanGive(me, giver)) continue;
                _ownNext = Time.time + OwnGapMinutes * 60f; // (looked at: a roll, then a rest either way)
                if (Random.value > OwnChance || RavenSkills.Hushed(me)) return false;
                Satiety.Change(g, -ShareFill);
                Satiety.Change(r, ShareFill);
                Shown(me, giver);
                me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} поділився їжею з {1} · ситість +{2}", Plugin.RavenName(giver), Plugin.RavenNameIns(!giver), Mathf.RoundToInt(ShareFill)), 0, RavenItem.Icon(giver));
                if (Director.May(Director.Voice.Talk)) Words(me, giver);
                return true;
            }
            return false;
        }

        // what is seen: the giver digs a bite out (in your bag), the other is glad of it a moment later
        private static void Shown(Player me, bool giver)
        {
            ShoulderLife g = Shoulder.LifeOf(me, giver), r = Shoulder.LifeOf(me, !giver);
            if (g != null) g.Act("Зазирає в торбу");
            if (r != null) r.Act("Радіє здобичі", 1.4f);
        }

        // a word each in their bubble (seen by the others near, as their talks)
        private static void Words(Player me, bool giver)
        {
            string l1 = Satiety.Say(me, giver, "pair_share_give");
            string l2 = !string.IsNullOrEmpty(l1) ? Satiety.Say(me, !giver, "pair_share_thanks") : null;
            if (string.IsNullOrEmpty(l2)) return;
            List<TalkBubble.Line> lines = new List<TalkBubble.Line> { new TalkBubble.Line { Munin = giver, Text = l1 }, new TalkBubble.Line { Munin = !giver, Text = l2 } };
            TalkBubble.Play(me, lines, true);
            RavenTalk.Send(me, lines);
            Director.Spoke(Director.Voice.Talk, TalkBubble.Pace(l1) + TalkBubble.Pace(l2) + 2f);
        }

        // ---------------------------------------------------------------- a friend's raven

        private static void Friend(Player me)
        {
            foreach (bool my in new[] { false, true })
            {
                int i = my ? 1 : 0;
                if (FriendNext[i] < 0f) { FriendNext[i] = Time.time + FriendGapMinutes * 20f; continue; }
                if (Time.time < FriendNext[i] || !CanGive(me, my)) continue;
                Player best = null; bool bestM = false; float bestS = Bonds.FriendAt;
                foreach (Player o in Player.GetAllPlayers())
                {
                    if (o == null || o == me || o.IsDead() || Shadow.Flagged(o) || Vector3.Distance(o.transform.position, me.transform.position) > Bonds.VisitRange) continue;
                    foreach (bool tm in new[] { false, true })
                    {
                        if (!HungryOn(o, tm) || Shoulder.ModelOf(o, tm) == null) continue;
                        float s = Bonds.PairScore(me, my, o, tm);
                        if (s >= bestS) { bestS = s; best = o; bestM = tm; }
                    }
                }
                if (best == null) continue;
                FriendNext[i] = Time.time + FriendGapMinutes * 60f;
                if (Random.value > FriendChance) continue;
                if (Bonds.StartShare(me, my, best, bestM)) return;
            }
        }

        // the visit landed by the hungry friend (Bonds): the bite handed over — here my raven's share, there theirs
        internal static void Hand(Player me, bool my, Player o, bool tm)
        {
            ItemDrop.ItemData g = RavenSeat.On(me, my);
            long peer = JointPlay.PeerOf(o);
            if (g == null || peer == 0L || ZRoutedRpc.instance == null) return;
            Satiety.Change(g, -ShareFill);
            ZPackage pkg = new ZPackage();
            pkg.Write(me.GetPlayerName());
            pkg.Write(my);
            pkg.Write(tm);
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, Rpc, pkg);
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} відніс поїсти {1} гравця {2}", Plugin.RavenName(my), Plugin.RavenNameDat(tm), o.GetPlayerName()), 0, RavenItem.Icon(my));
        }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new System.Action<long, ZPackage>(OnHand));
        }

        private static readonly Dictionary<long, float> GotAt = new Dictionary<long, float>();

        // a friend's raven brought my raven a bite (their game asks): only when mine really is hungry, near, not at odds
        private static void OnHand(long sender, ZPackage pkg)
        {
            pkg.ReadString(); // (the sender's name: shown by older games, not trusted here)
            bool giver = pkg.ReadBool(), mine = pkg.ReadBool();
            Player me = Player.m_localPlayer, o = null;
            foreach (Player x in Player.GetAllPlayers()) if (x != null && x != me && JointPlay.PeerOf(x) == sender) { o = x; break; } // (by the game that sent it, not by a name: names can be alike or made up)
            ItemDrop.ItemData r = me != null ? RavenSeat.On(me, mine) : null;
            if (o == null || r == null || Vector3.Distance(o.transform.position, me.transform.position) > Bonds.VisitRange + 10f) return;
            float last;
            if (GotAt.TryGetValue(sender, out last) && Time.time - last < 300f) return; // (one game: once in five minutes at most)
            GotAt[sender] = Time.time;
            if (GotAt.Count > 16) GotAt.Clear();
            if (Satiety.Zone(r) > Satiety.Empty || Bonds.Score(me, mine, o, giver) <= Bonds.EnemyAt) return;
            Satiety.Change(r, ShareFill);
            Bonds.Change(me, mine, o, giver, Bonds.ShareGain);
            BondLog.Note(r, o, giver, BondLog.FedUs);
            People.OnFed(r, mine, o);
            ShoulderLife life = Shoulder.LifeOf(me, mine);
            if (life != null) life.Act("Радіє здобичі", 0.5f);
            string said = Lang.F("{0} гравця {1} приніс поїсти твоєму {2} · ситість +{3}", Plugin.RavenName(giver), o.GetPlayerName(), Plugin.RavenNameDat(mine), Mathf.RoundToInt(ShareFill));
            me.Message(MessageHud.MessageType.TopLeft, said, 0, RavenItem.Icon(mine));
        }

        // the console: raven share — the gaps forgotten and a look now (own two first, then a friend)
        internal static string Cmd(Player p)
        {
            if (p == null) return "No player.";
            _ownNext = 0f; FriendNext[0] = FriendNext[1] = 0f; _lookAt = 0f;
            float oc = OwnChance, fc = FriendChance;
            OwnChance = FriendChance = 1f;
            try { Publish(p); if (Own(p)) return "Share: your two."; Friend(p); return Bonds.Visiting ? "Share: off to a friend." : "Share: nobody to give to (one hungry, the other fed above 52?)."; }
            finally { OwnChance = oc; FriendChance = fc; }
        }

        internal static void Reset() { _ownNext = -1f; FriendNext[0] = FriendNext[1] = -1f; _lookAt = 0f; }
    }
}
#endif

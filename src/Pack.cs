#if RAVEN_ITEM
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Зграя» (the user, 2026-10-23): friend ravens of two players fight as one.
    //  - A double strike: a raven's blow on a foe that another player's raven struck a moment before (PackSeconds), the
    //    two of them friends — the later one deals a blow more (PackBonus of its own), both ravens' colours burst there,
    //    they have their word (bond_pack_*), and the book's «Знайомі» counts it (fought as a pack).
    //  - Unbidden: an angry foe close to one of two players whose ravens are friends — now and then (Pals sometimes,
    //    Close more often) the deciding game sends its raven at it and asks the other game to send theirs; each game flies
    //    its own (a free strike: no strength, no pause; only where the strike itself could fly).
    internal static class Pack
    {
        internal static float PackSeconds = 3f, PackBonus = 0.4f, BossShare = 0.01f, Near = 30f, ThreatNear = 10f;
        internal static float GoChancePals = 0.2f, GoChanceClose = 0.45f, GoGapMin = 180f, GoGapMax = 360f, GoRetry = 20f;
        internal static float PairBond = 2f, TalkGap = 60f, FollowMin = 0.3f, FollowMax = 0.9f, DoubleGap = 6f, AskGap = 10f;
        internal static float FightGap = 60f, TalkWait = 14f; // one fight together counts once a minute for a pair (the bond, the book); the word waits for both back on the shoulders
        private const string RpcHit = "RavenWatch_PackHit", RpcGo = "RavenWatch_PackGo";

        private sealed class Blow { public ZDOID Target; public Player By; public bool Munin, Mine, Used; public float At; }
        private static readonly List<Blow> Recent = new List<Blow>();
        private static readonly Dictionary<string, float> GoNext = new Dictionary<string, float>();
        private static readonly Dictionary<long, float> HeardAt = new Dictionary<long, float>(), AskedAt = new Dictionary<long, float>();
        private static readonly Dictionary<ZDOID, float> DoubledAt = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<string, float> FoughtAt = new Dictionary<string, float>();
        private static readonly List<Character> Foes = new List<Character>();
        private static float _talkAt, _lookAt;
        private static bool _foesLooked; // (the foes near looked up this look: once, not once for every player)

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcHit, OnHit);
            ZRoutedRpc.instance.Register<ZPackage>(RpcGo, OnGo);
        }

        internal static void Reset() { Recent.Clear(); GoNext.Clear(); HeardAt.Clear(); AskedAt.Clear(); DoubledAt.Clear(); FoughtAt.Clear(); Foes.Clear(); }

        // a fight of the pair, counted once a FightGap (true: count it now)
        private static bool NewFight(Player me, bool my, Player o, bool om)
        {
            string key = Bonds.RidOf(me, my) + "|" + Bonds.RidOf(o, om);
            float at;
            if (FoughtAt.TryGetValue(key, out at) && Time.time - at < FightGap) return false;
            if (FoughtAt.Count > 64) FoughtAt.Clear();
            FoughtAt[key] = Time.time;
            return true;
        }

        private static void Prune() { Recent.RemoveAll(b => Time.time - b.At > PackSeconds || b.By == null); }

        private static bool Friends(Player me, bool my, Player o, bool tm)
        {
            Bonds.State st = Bonds.StateOf(Bonds.PairScore(me, my, o, tm));
            return st == Bonds.State.Pals || st == Bonds.State.Close;
        }

        // ---------------------------------------------------------------- the double strike

        // a blow of my raven landed (Strike.Hit, my game)
        internal static void OwnHit(Player p, bool munin, Character t, float v, Vector3 from)
        {
            if (p == null || t == null || p != Player.m_localPlayer || !Bonds.Enabled || ZRoutedRpc.instance == null) return;
            Prune();
            ZDOID id = t.GetZDOID();
            Player with = null; bool withMunin = false;
            float dAt;
            bool rested = !DoubledAt.TryGetValue(id, out dAt) || Time.time - dAt >= DoubleGap;
            foreach (Blow b in Recent)
            {
                if (!rested) break;
                if (b.Mine || b.Used || b.Target != id || !Friends(p, munin, b.By, b.Munin)) continue;
                b.Used = true; // (one double strike for one blow of theirs: a series of mine does not make three)
                DoubledAt[id] = Time.time;
                if (DoubledAt.Count > 32) DoubledAt.Clear();
                with = b.By; withMunin = b.Munin;
                Double(p, munin, b.By, b.Munin, t, v, from);
                break;
            }
            Recent.Add(new Blow { Target = id, By = p, Munin = munin, Mine = true, At = Time.time });
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID()); pkg.Write(id); pkg.Write(munin);
            pkg.Write(with != null ? with.GetZDOID() : ZDOID.None); pkg.Write(withMunin); // (a double strike on that one's blow, or none)
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcHit, pkg);
        }

        // another player's raven struck: kept a moment; a double strike on my raven's blow: my book counts the fight too
        private static void OnHit(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID()) return;
            ZDOID pid = pkg.ReadZDOID(), id = pkg.ReadZDOID();
            bool munin = pkg.ReadBool();
            ZDOID with = pkg.ReadZDOID();
            bool withMunin = pkg.ReadBool();
            Player who = Find(pid, sender);
            if (who == null || Vector3.Distance(who.transform.position, me.transform.position) > Near * 2f) return;
            float at;
            if (HeardAt.TryGetValue(sender, out at) && Time.time - at < 0.1f) return; // (a flood of forged blows: no)
            HeardAt[sender] = Time.time;
            if (HeardAt.Count > 64) HeardAt.Clear();
            Prune();
            if (with == me.GetZDOID() && Friends(me, withMunin, who, munin) && NewFight(me, withMunin, who, munin))
            {
                BondLog.Note(RavenSeat.On(me, withMunin), who, munin, BondLog.Pack);
                Nicks.OnPack(me, RavenSeat.On(me, withMunin));
            }
            Recent.Add(new Blow { Target = id, By = who, Munin = munin, At = Time.time });
        }

        private static Player Find(ZDOID pid, long sender)
        {
            if (pid.UserID != sender) return null; // (only a player's own game speaks of its raven)
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetZDOID() == pid) return o;
            return null;
        }

        // the later of the two blows: a blow more of its own, the burst, the word, the book
        private static void Double(Player p, bool munin, Player o, bool om, Character t, float v, Vector3 from)
        {
            if (t == null || t.IsDead()) return;
            float bonus = v * PackBonus;
            if (t.IsBoss()) bonus = Mathf.Min(bonus, t.GetMaxHealth() * BossShare);
            if (bonus >= 0.5f)
            {
                HitData hit = new HitData();
                if (munin) hit.m_damage.m_slash = bonus; else hit.m_damage.m_pierce = bonus;
                hit.m_point = Body.Center(t);
                Vector3 d = Body.Center(t) - from;
                hit.m_dir = d.sqrMagnitude > 0.001f ? d.normalized : t.transform.forward;
                hit.m_blockable = false; hit.m_dodgeable = false; hit.m_ranged = true;
                hit.m_skill = Skills.SkillType.None;
                hit.m_skillRaiseAmount = Strike.Marker - bonus; // (cut back to its own size in the target's game, as every raven blow)
                hit.SetAttacker(p);
                t.Damage(hit);
                if (DamageText.instance != null) DamageText.instance.ShowText(DamageText.TextType.Weak, hit.m_point + Vector3.up * 0.9f, Lang.T("ЗГРАЯ!"));
            }
            BindFx.Nova(p, "chain", Body.Center(t), 1.4f, 0.5f);
            if (!NewFight(p, munin, o, om)) return; // (the same fight: the blow more, not another count)
            BondLog.Note(RavenSeat.On(p, munin), o, om, BondLog.Pack);
            Nicks.OnPack(p, RavenSeat.On(p, munin)); // a deed for its nickname
            Bonds.Change(p, munin, o, om, PairBond);
            if (Time.time >= _talkAt && Plugin.Instance != null) { _talkAt = Time.time + TalkGap; Plugin.Instance.StartCoroutine(TalkLater(p, munin, o, om)); }
        }

        // their word once both are back on the shoulders (a bubble needs a bird to sit over), or not at all
        private static IEnumerator TalkLater(Player p, bool munin, Player o, bool om)
        {
            float until = Time.time + TalkWait;
            while (Time.time < until)
            {
                yield return new WaitForSeconds(0.5f);
                if (p == null || o == null || p.IsDead() || o.IsDead()) yield break;
                if (!CrossTalk.CanTalk(p, munin) || !CrossTalk.CanTalk(o, om)) continue;
                if (!RavenSkills.Hushed(p) && Director.May(Director.Voice.Event)) Bonds.PackTalk(p, munin, o, om);
                yield break;
            }
        }

        // ---------------------------------------------------------------- unbidden: together at a foe near one of the two

        // from the ravens' sync (RavenSkills), once a second
        internal static void Tick(Player me)
        {
            if (me == null || me.IsDead() || !Bonds.Enabled || Time.time < _lookAt) return;
            _lookAt = Time.time + 1f;
            if (Shadow.On(me)) return; // (hidden in Hugin's shadow: a raven flying out unbidden would give you away)
            Foes.Clear(); _foesLooked = false;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || o.IsDead() || Dummy.Is(o) || Shadow.Flagged(o) || Vector3.Distance(o.transform.position, me.transform.position) > Near) continue;
                if (!CrossTalk.Decides(me, o)) continue;
                Character foe = Threat(me, o);
                if (foe == null) continue;
                // the warmest friendship between their ravens and mine
                bool my = false, tm = false; float best = Bonds.FriendAt - 0.01f; bool any = false;
                foreach (bool a in new[] { false, true })
                    foreach (bool b in new[] { false, true })
                    {
                        if (Bonds.RidOf(o, b).Length == 0 || RavenSeat.On(me, a) == null) continue;
                        float s = Bonds.PairScore(me, a, o, b);
                        if (s > best) { best = s; my = a; tm = b; any = true; }
                    }
                if (!any) continue;
                string key = Bonds.RidOf(me, my) + "|" + Bonds.RidOf(o, tm);
                float next;
                if (GoNext.TryGetValue(key, out next) && Time.time < next) continue;
                bool close = Bonds.StateOf(best) == Bonds.State.Close;
                if (UnityEngine.Random.value > (close ? GoChanceClose : GoChancePals)) { GoNext[key] = Time.time + GoRetry; continue; }
                GoNext[key] = Time.time + UnityEngine.Random.Range(GoGapMin, GoGapMax);
                if (GoNext.Count > 64) GoNext.Clear();
                bool went = RavenSkills.FreeStrike(me, my, foe);
                Ask(me, my, o, tm, foe);
                if (went) Told(me, my, o, tm);
                return; // (one at a time)
            }
        }

        // an angry foe close to one of the two (its target is known only where it is driven: near and angry will do)
        private static Character Threat(Player me, Player o)
        {
            if (!_foesLooked) { _foesLooked = true; foreach (Character c in Character.GetAllCharacters()) if (Home.Hostile(c) && Home.Angry(c) && (c.transform.position - me.transform.position).sqrMagnitude < (Near + ThreatNear) * (Near + ThreatNear)) Foes.Add(c); }
            Character best = null; float bd = ThreatNear * ThreatNear;
            foreach (Character c in Foes)
            {
                if (c == null || c.IsDead()) continue;
                float d = Mathf.Min((c.transform.position - me.transform.position).sqrMagnitude, (c.transform.position - o.transform.position).sqrMagnitude);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        // the other game: send your raven too
        private static void Ask(Player me, bool my, Player o, bool tm, Character foe)
        {
            long peer = JointPlay.PeerOf(o);
            if (peer == 0L || ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(me.GetZDOID()); pkg.Write(my); pkg.Write(tm); pkg.Write(foe.GetZDOID());
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, RpcGo, pkg);
        }

        private static void OnGo(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            if (me == null || me.IsDead() || !Bonds.Enabled || Shadow.On(me)) return;
            float asked;
            if (AskedAt.TryGetValue(sender, out asked) && Time.time - asked < AskGap) return; // (one call to arms at a time from a game)
            AskedAt[sender] = Time.time;
            if (AskedAt.Count > 64) AskedAt.Clear();
            ZDOID pid = pkg.ReadZDOID();
            bool theirs = pkg.ReadBool(), mine = pkg.ReadBool();
            ZDOID fid = pkg.ReadZDOID();
            Player who = Find(pid, sender);
            if (who == null || Vector3.Distance(who.transform.position, me.transform.position) > Near) return;
            if (!Friends(me, mine, who, theirs)) return; // (my side of it must agree)
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(fid) : null;
            Character foe = g != null ? g.GetComponent<Character>() : null;
            if (foe == null || foe.IsDead() || !Home.Hostile(foe) || Plugin.Instance == null) return; // (a real foe, not whatever was named)
            Plugin.Instance.StartCoroutine(Follow(me, mine, who, theirs, foe, UnityEngine.Random.Range(FollowMin, FollowMax)));
        }

        private static IEnumerator Follow(Player me, bool mine, Player who, bool theirs, Character foe, float wait)
        {
            yield return new WaitForSeconds(wait);
            if (me == null || foe == null || foe.IsDead()) yield break;
            if (RavenSkills.FreeStrike(me, mine, foe) && who != null) Told(me, mine, who, theirs);
        }

        // a line in the corner: who flew at it together
        private static void Told(Player me, bool my, Player o, bool tm)
        {
            me.Message(MessageHud.MessageType.TopLeft, Lang.F("Зграя: {0} і {1} — на ворога!", Plugin.RavenName(my), Nicks.Name(o, tm)), 0, RavenItem.Icon(my));
        }
    }
}
#endif

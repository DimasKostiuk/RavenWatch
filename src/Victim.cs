#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HuginWhispers
{
    // «Жертва» (the user, 2026-10-22): now and then a raven of yours picks someone near and mocks them for a few minutes —
    // another player, a tamed animal, a trader, the other raven, or you yourself.
    //   - A small chance a game day (Chance), and the same again for mocking you (OwnChance); a raven of trust 2 and up,
    //     awake, fed, sitting on your shoulder. It starts at a random moment of that day when someone is there.
    //   - A spell of mockery lasts MinMinutes–MaxMinutes, a jeer every EveryMin–EveryMax s (3 min: two or three, 10 min:
    //     eight or so). It is felt apart from the usual talk: the card has a venom-green throbbing rim and «глузує», it
    //     shakes as it comes, the raven laughs (two quick caws), does its «Дражнить» and keeps its eyes on the victim; the
    //     other chatter waits (each jeer counts with the director).
    //   - It has a reason (struck you, no raven, a legend's raven, half dead, no reason at all) and sees what the victim
    //     does: hurt, a fall, a death, running away, a fight, sleeping, swimming, building, an emote, standing about; the
    //     fresh thing comes into the next jeer.
    //   - Out of reach (another player gone off): it tells you what it saw instead (gossip), and once more after the spell.
    //   - Once a spell (PrankChance) it flies onto the victim's head (an animal's back) and stays a whole minute
    //     (HeadSeconds): pecking (PeckDamage a peck, never to death: another player's own game takes it off, PvP or not)
    //     and mocking non-stop — a line as soon as the
    //     last one is read and nobody near (30 m) is talking: no raven, no player's words over a head, no NPC; its words
    //     there are of their own (victim_head, and victim_head_move while the victim runs or jumps). A roll shakes it off.
    //   - A player with ravens of their own: one of theirs may snap back, and yours answers — a quarrel of the ravens in
    //     the bridge bubble (CrossTalk «victim»). The other raven as the victim answers too (your two's own bubble).
    //   - Hugin's prophecy (ProphecyChance, he must be on): he foretells the victim a fall, a blow or a death within the
    //     spell; come true: his triumph and a little trust; not: Munin laughs at him.
    // Others can say no: «Other players ravens may mock me» (a flag on their player). The owner's game decides all and
    // writes on its player which raven mocks whom (every game turns that raven's head to the victim: ShoulderLife).
    internal static class Victim
    {
        internal static bool Enabled = true;
        internal static float Chance = 0.08f, OwnChance = 0.08f;   // a game day: someone else / you
        internal static float MinMinutes = 3f, MaxMinutes = 10f;    // how long a spell lasts
        internal static float EveryMin = 55f, EveryMax = 85f;       // s between two jeers
        internal static float Range = 30f;                          // m: within this it mocks them to their face
        internal static int MinLevel = 2;
        internal static float PrankChance = 0.6f, HeadSeconds = 60f, ReachSeconds = 20f, PeckEvery = 2.2f, PeckDamage = 1f;
        internal static float QuietRange = 30f; // m: nobody talking this near before a line on the head
        internal static float QuarrelChance = 0.35f, RavenAnswer = 0.6f, ProphecyChance = 0.35f, ProphecyTrust = 1f;
        internal static float GossipAfter = 0.5f;                   // after a spell: one more word about them, this often
        internal static ConfigEntry<bool> LetOthers;
        internal const string NoMockKey = "RavenWatch_noMock", MockKey = "RavenWatch_mock", MockTKey = "RavenWatch_mockT";
        private static readonly int NoMockHash = NoMockKey.GetStableHashCode(), MockHash = MockKey.GetStableHashCode(); // (every raven looks twice a second: hashed once)
        private static readonly KeyValuePair<int, int> MockTHash = ZDO.GetHashZDOID(MockTKey);
        private const string DayKey = "RavenWatch_victimDay";
        private const string Rpc = "RavenWatch_Victim", PeckRpc = "RavenWatch_VictimPeck";
        internal static float PeckMinGap = 1.5f; // s: the victim's game takes a peck from one player at most this often

        internal enum Kind { Player, Own, Beast, Trader, Raven }

        private sealed class Session
        {
            public Kind Kind; public bool Munin; public Character Char; public string Name, Reason; public bool Female;
            public float Until, NextLine, LastLine = -100f;
            public int Lines, Gossips;
            public Transform At; // a trader: where it stands (no character of its own)
            // what it sees
            public bool Present = true; public float Hp = 1f, Y0, Y1, Dist; public Vector3 StillAt, Prev; public float StillSince;
            public string Ev; public float EvAt = -100f; public readonly Dictionary<string, float> EvLast = new Dictionary<string, float>();
            public readonly List<string> Seen = new List<string>();
            public int Emote = -1;
            // the prank: onto them
            public float PrankAt = -1f, PrankFrom, PrankUntil, NextPeck, NextHeadLine; public PerchView Spot; public bool OnHead; public int HeadLines;
            public Vector3 HeadPrev; public float MovedAt = -100f; // the victim under it: where it was last tick, when it last ran or jumped
            // Hugin's prophecy
            public float ProphecyAt = -1f; public string Prophecy; public bool ProphecyTrue;
            public float OutSince = -1f; // since when they are out of reach
        }

        private static Session _s;
        private static Player _for;
        internal static bool Busy { get { return _s != null; } } // a spell of mockery goes on (Bonds waits)
        private static int _day = -1;
        private static float _otherAt = -1f, _otherUntil, _ownAt = -1f, _ownUntil;
        private static float _gossipAt = -1f, _gossipUntil; private static string _gossipName, _gossipEv; private static bool _gossipMunin, _gossipFemale; private static Kind _gossipKind;

        // ---------------------------------------------------------------- from the twice-a-second sync (RavenSkills)

        internal static void Tick(Player p)
        {
            if (p == null) return;
            if (p != _for) { Reset(); _for = p; } // another character or world: nothing of the last one goes on
            Publish(p);
            if (_s != null) { try { Run(p); } catch { Stop(p, false); throw; } return; }
            if (!Enabled) return;
            Roll();
            float now = Time.time;
            if (_gossipAt > 0f && now >= _gossipAt) _gossipAt = LateGossip(p) || now > _gossipUntil ? -1f : now + 45f; // (not now: again in a while, for ten minutes)
            if (_ownAt > 0f && now >= _ownAt)
            {
                if (Start(p, true, null)) _ownAt = -1f;
                else _ownAt = now < _ownUntil ? now + 30f : -1f; // not now: in half a minute, until the day's chance is gone
            }
            else if (_otherAt > 0f && now >= _otherAt)
            {
                if (Start(p, false, null)) _otherAt = -1f;
                else _otherAt = now < _otherUntil ? now + 30f : -1f;
            }
        }

        // a new game day: its two dice (once a day for this character, a reload does not roll again)
        private static void Roll()
        {
            Player p = Player.m_localPlayer;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (p == null || day == _day) return;
            _day = day;
            string was;
            if (p.m_customData != null && p.m_customData.TryGetValue(DayKey, out was) && was == day.ToString()) return;
            if (p.m_customData != null) p.m_customData[DayKey] = day.ToString();
            float now = Time.time;
            if (UnityEngine.Random.value < Chance) { _otherAt = now + UnityEngine.Random.Range(60f, 900f); _otherUntil = _otherAt + 900f; }
            if (UnityEngine.Random.value < OwnChance) { _ownAt = now + UnityEngine.Random.Range(60f, 900f); _ownUntil = _ownAt + 900f; }
        }

        // your «not me» told to everyone; which raven of yours mocks whom (the others' games turn its head there)
        private static void Publish(Player p)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            bool let = LetOthers == null || LetOthers.Value;
            if (z.GetBool(NoMockHash, false) == let) z.Set(NoMockHash, !let);
            int mock = _s == null ? 0 : _s.Munin ? 2 : 1;
            if (z.GetInt(MockHash, 0) != mock) z.Set(MockHash, mock);
            ZDOID t = _s != null && _s.Char != null ? _s.Char.GetZDOID() : ZDOID.None;
            if (z.GetZDOID(MockTHash) != t) z.Set(MockTHash, t);
        }

        // ShoulderLife: whom this raven of that player stares at while it mocks (null: nobody)
        internal static Character LookFor(Player pl, bool munin)
        {
            if (pl == null) return null;
            ZDO z = NetFlag.Zdo(pl, false);
            if (z == null || z.GetInt(MockHash, 0) != (munin ? 2 : 1)) return null;
            ZDOID t = z.GetZDOID(MockTHash);
            if (t == ZDOID.None || ZNetScene.instance == null) return null;
            GameObject g = ZNetScene.instance.FindInstance(t);
            Character c = g != null ? g.GetComponent<Character>() : null;
            return c != null && c != pl && !c.IsDead() ? c : null;
        }

        // ---------------------------------------------------------------- the start

        // a raven of yours that may mock now: worn, awake, trust 2 and up, fed, on the shoulder
        private static bool Free(Player p, bool munin)
        {
            ItemDrop.ItemData r = RavenSeat.On(p, munin);
            return r != null && Trust.Active(r) && Trust.Level(r) >= MinLevel && !Trust.Stranger(r, p) && !Trust.Away(r)
                && !Trust.Starving(r) && Satiety.Zone(r) > Satiety.Hungry && !WorldVisit.Active(munin);
        }

        private static bool _picking; // the raven that picks now (Reason asks how it takes to them)
        private sealed class Cand { public Kind K; public Character C; public Transform At; public string Name; public bool Female; public float W; }

        // own: you are the victim; who: the console's choice (a name, "beast", "trader", "raven"), or null
        private static bool Start(Player p, bool own, string who)
        {
            if (p.IsDead() || p.InBed() || (Senses.Ready && (Senses.Fight || Senses.Sleeping))) return false;
            bool h = Free(p, false), m = Free(p, true);
            if (!h && !m) return false;
            bool munin = h && m ? UnityEngine.Random.value < 0.6f : m;
            _picking = munin;
            List<Cand> c = new List<Cand>();
            if (own) c.Add(new Cand { K = Kind.Own, C = p, Name = p.GetPlayerName(), Female = Grammar.Female(p), W = 1f });
            else Candidates(p, munin, h && m, who, c);
            if (c.Count == 0) return false;
            float sum = 0f;
            foreach (Cand x in c) sum += x.W;
            float pick = UnityEngine.Random.value * sum;
            Cand v = c[c.Count - 1];
            foreach (Cand x in c) { pick -= x.W; if (pick <= 0f) { v = x; break; } }
            if (v.K == Kind.Raven) munin = v.Name != Plugin.RavenName(true); // (the other one mocks this one)
            Begin(p, munin, v);
            return true;
        }

        private static void Candidates(Player p, bool munin, bool both, string who, List<Cand> c)
        {
            Vector3 me = p.transform.position;
            string w = who != null ? who.ToLowerInvariant() : null;
            bool any = w == null;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || o.IsDead() || Shadow.Flagged(o) || Vector3.Distance(me, o.transform.position) > Range) continue;
                if (NetFlag.Get(o, NoMockKey)) continue; // «not me»
                if (!any && o.GetPlayerName().ToLowerInvariant() != w) continue;
                c.Add(new Cand { K = Kind.Player, C = o, Name = o.GetPlayerName(), Female = Grammar.Female(o), W = 4f * GrudgeWeight(p, munin, o) });
            }
            if (any || w == "beast")
                foreach (Character b in Character.GetAllCharacters())
                {
                    if (b == null || b.IsPlayer() || !b.IsTamed() || b.IsDead() || Vector3.Distance(me, b.transform.position) > Range) continue;
                    c.Add(new Cand { K = Kind.Beast, C = b, Name = SitOn.NameOf(b), Female = BeastFemale(b), W = 1.5f });
                }
            if (any || w == "trader")
                foreach (Trader t in UnityEngine.Object.FindObjectsByType<Trader>(FindObjectsSortMode.None))
                {
                    if (t == null || Vector3.Distance(me, t.transform.position) > Range) continue;
                    string token = t.m_name ?? "";
                    c.Add(new Cand { K = Kind.Trader, At = t.transform, Name = Lang.Game(token), Female = token.Contains("hildir") || token.Contains("bogwitch"), W = 2f }); // (a trader is no character: only where it stands)
                }
            if ((any || w == "raven") && both)
                c.Add(new Cand { K = Kind.Raven, Name = Plugin.RavenName(!munin), Female = false, W = 1f }); // (alone with nobody else: most often this)
        }

        // a hen is «вона»; the other tame ones of the game are «він» (вовк, кабан, лось, асксвін)
        private static bool BeastFemale(Character b)
        {
            string n = Utils.GetPrefabName(b.gameObject).ToLowerInvariant();
            return n.Contains("hen") || n.Contains("chicken");
        }

        private static void Begin(Player p, bool munin, Cand v)
        {
            float now = Time.time;
            Session s = new Session { Kind = v.K, Munin = munin, Char = v.C, At = v.At, Name = v.Name, Female = v.Female };
            s.Until = now + UnityEngine.Random.Range(MinMinutes, Mathf.Max(MinMinutes, MaxMinutes)) * 60f;
            s.NextLine = now + 1.5f;
            if (v.C != null) { s.Hp = v.C.GetHealthPercentage(); s.Y0 = s.Y1 = v.C.transform.position.y; s.StillAt = v.C.transform.position; s.StillSince = now; }
            s.Reason = Reason(p, v);
            if ((v.K == Kind.Player || v.K == Kind.Beast || v.K == Kind.Own) && UnityEngine.Random.value < PrankChance)
                s.PrankAt = now + (s.Until - now) * UnityEngine.Random.Range(0.3f, 0.7f);
            if ((v.K == Kind.Player || v.K == Kind.Own || v.K == Kind.Beast) && Free(p, false) && UnityEngine.Random.value < ProphecyChance)
            {
                s.ProphecyAt = now + UnityEngine.Random.Range(40f, Mathf.Max(45f, (s.Until - now) * 0.4f));
                string[] kinds = v.K == Kind.Beast ? new[] { "hurt", "death" } : v.K == Kind.Own ? new[] { "hurt", "fall" } : new[] { "hurt", "fall", "death" }; // (your death ends the spell: never foretold to you)
                s.Prophecy = kinds[UnityEngine.Random.Range(0, kinds.Length)];
            }
            _s = s;
            string raven = Plugin.RavenName(munin);
            RavenMsg.Center(p, v.K == Kind.Own ? Lang.F("{0} обрав жертвою… тебе", raven) : Lang.F("{0} обрав собі жертву: {1}", raven, v.Name));
            if (v.K == Kind.Player) Tell(v.C as Player, p, munin, true);
            Log("Raven victim: " + raven + " mocks " + v.K + " «" + v.Name + "» (" + s.Reason + ") for " + Mathf.RoundToInt(s.Until - now) + " s" + (s.PrankAt > 0f ? ", prank" : "") + (s.Prophecy != null && s.ProphecyAt > 0f ? ", prophecy " + s.Prophecy : ""));
        }

        // how this raven takes to that player (People): one it can't stand is picked far more often, one it likes seldom
        private static float GrudgeWeight(Player p, bool munin, Player o)
        {
            switch (People.StateTo(p, munin, o))
            {
                case People.State.Hate: return 5f;
                case People.State.Dislike: return 2.5f;
                case People.State.Respect: return 0.4f;
                case People.State.Adore: return 0.2f;
                default: return 1f;
            }
        }

        // People: a raven that hates this player picks them now (false: not now — busy, nobody free, «not me»)
        internal static bool Grudge(Player p, Player o)
        {
            if (_s != null || p == null || o == null || NetFlag.Get(o, NoMockKey)) return false;
            return Start(p, false, o.GetPlayerName());
        }

        // why this one (a player: what is plain to see; else none)
        private static string Reason(Player p, Cand v)
        {
            if (v.K != Kind.Player) return "any";
            Player t = (Player)v.C;
            if (People.StateTo(p, _picking, t) == People.State.Hate) return "grudge"; // it simply can't stand them
            if (AboutTalk.StruckLately(t)) return "hit";
            if (t.GetHealthPercentage() < 0.4f) return "weak";
            bool raven = Shoulder.ModelOf(t, false) != null || Shoulder.ModelOf(t, true) != null;
            if (!raven) return UnityEngine.Random.value < 0.6f ? "noraven" : "random";
            foreach (bool m in new[] { false, true })
            {
                string n, call; Nicks.Kind k;
                if (Nicks.Of(t, m, out n, out call, out k) && k == Nicks.Kind.Legend) return "envy";
            }
            return "random";
        }

        // ---------------------------------------------------------------- the spell

        private static void Run(Player p)
        {
            Session s = _s;
            float now = Time.time;
            if (p.IsDead()) { Stop(p, false); return; }
            bool mineFree = RavenSeat.On(p, s.Munin) != null && Trust.Active(RavenSeat.On(p, s.Munin));
            if (!mineFree) { Stop(p, false); return; } // taken off, gone to Odin
            if (now >= s.Until && s.Spot == null) { Stop(p, true); return; }
            if (s.Kind == Kind.Beast && s.Char == null) { Stop(p, true); return; } // (gone: unloaded, dead and taken away)
            if (s.Kind == Kind.Raven && RavenSeat.On(p, !s.Munin) == null) { Stop(p, false); return; } // the other one taken off
            Watch(p, s);
            if (_s == null) return;
            Prank(p, s);
            if (s.Spot != null) return; // on their head: it talks from there (Prank)
            if (s.ProphecyAt > 0f && now >= s.ProphecyAt && Free(p, false) && Near(p, s) && !Hushed(p)) { s.ProphecyAt = -1f; Foretell(p, s); return; }
            if (now < s.NextLine) return;
            if (Hushed(p) || (Senses.Ready && Senses.Fight && s.Kind != Kind.Own)) { s.NextLine = now + 6f; return; }
            if (!Free(p, s.Munin)) { s.NextLine = now + 10f; return; }
            s.NextLine = now + UnityEngine.Random.Range(EveryMin, EveryMax);
            Jeer(p, s);
        }

        private static bool Hushed(Player p) { return RavenSkills.Hushed(p) || RavenSpeech.Has(p.gameObject); }

        private static bool Near(Player p, Session s)
        {
            if (s.Kind == Kind.Own || s.Kind == Kind.Raven) return true;
            Vector3 at = s.Char != null ? s.Char.transform.position : s.At != null ? s.At.position : p.transform.position + Vector3.up * 999f;
            s.Dist = Vector3.Distance(p.transform.position, at);
            return s.Dist <= Range;
        }

        // one jeer: what it sees fresh, or just a jab; out of reach: gossip to you
        private static void Jeer(Player p, Session s)
        {
            float now = Time.time;
            bool near = Near(p, s);
            string fam = Fam(s.Kind);
            string line;
            if (!near)
            {
                if (s.OutSince < 0f) s.OutSince = now;
                if (s.Kind != Kind.Player && s.Kind != Kind.Beast) { if (now - s.OutSince > 60f) Stop(p, true); return; } // (a trader left behind: no gossip, just over)
                if (s.Gossips >= 2 || now - s.OutSince > 150f) { Stop(p, true); return; } // gone for good: the spell is over (and maybe a word later)
                string ev = s.Seen.Count > 0 ? s.Seen[s.Seen.Count - 1] : null;
                line = Make(p, s.Munin, s, ev != null ? "victim_gossip_" + ev : null, "victim_gossip");
                if (line == null) return;
                s.Gossips++;
                Say(p, s, line, false);
                return;
            }
            s.OutSince = -1f;
            string tag;
            if (s.Lines == 0) tag = s.Kind == Kind.Player ? "victim_start_" + s.Reason : fam + "_start";
            else if (s.Ev != null && now - s.EvAt < 12f) tag = fam + "_ev_" + s.Ev;
            else tag = fam + "_tease";
            line = Make(p, s.Munin, s, tag, s.Lines == 0 ? (s.Kind == Kind.Player ? "victim_start_random" : fam + "_tease") : fam + "_tease");
            if (line == null) return;
            if (s.Ev != null && tag.Contains("_ev_")) s.Ev = null; // (said: not again)
            s.Lines++;
            s.LastLine = now;
            // a player with ravens: one of theirs may snap back (a quarrel of the ravens); the other raven of yours answers
            if (s.Kind == Kind.Player && Quarrel(p, s, line)) return;
            if (s.Kind == Kind.Raven && UnityEngine.Random.value < RavenAnswer && Bicker(p, s, line)) return;
            Say(p, s, line, true);
        }

        private static string Fam(Kind k)
        {
            switch (k)
            {
                case Kind.Own: return "victim_own";
                case Kind.Beast: return "victim_beast";
                case Kind.Trader: return "victim_trader";
                case Kind.Raven: return "victim_raven";
                default: return "victim";
            }
        }

        // the words: {name} you (your forms), {friend} the victim (their forms {>він|вона}), {raven} the other raven
        private static string Make(Player p, bool munin, Session s, string tag, string spare)
        {
            if (Plugin.Instance == null) return null;
            string dir = Plugin.Instance.PhraseDir;
            ItemDrop.ItemData mine = RavenSeat.On(p, munin);
            int level = mine != null ? Mathf.Max(1, Trust.Level(mine)) : 1;
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["name"] = p.GetPlayerName();
            v["friend"] = s.Name;
            v["raven"] = Plugin.RavenName(!munin);
            foreach (string t in new[] { tag, spare })
            {
                if (t == null) continue;
                string line = Composer.ComposeCross(dir, munin, t, level, Grammar.Female(p), s.Female, v, Voice.Code(mine), true);
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return null;
        }

        // said from the shoulder (or from their head), as mockery: the green card, a laugh, its teasing act
        private static void Say(Player p, Session s, string line, bool act) { Say(p, s, s.Munin, line, act); }
        private static void Say(Player p, Session s, bool munin, string line, bool act)
        {
            string marked = RavenSpeech.Mock + line;
            if (s.Spot != null && munin == s.Munin) s.Spot.SayHere(0, Grammar.Fix(marked, p));
            else RavenSkills.SayFromShoulder(p, munin, marked);
            Director.Spoke(Director.Voice.Event, RavenSpeech.ReadSeconds(line)); // the other chatter waits: this is the show now
            Laugh(p, munin);
            if (act && s.Spot == null)
            {
                ShoulderLife life = Shoulder.LocalLife(munin);
                if (life != null) life.Act(UnityEngine.Random.value < 0.75f ? "Дражнить" : "Штурхає");
            }
        }

        // two quick caws, the second higher: a raven's laugh
        private static void Laugh(Player p, bool munin)
        {
            if (Plugin.Instance == null) return;
            Plugin.Instance.StartCoroutine(LaughRun(p, munin));
        }
        private static IEnumerator LaughRun(Player p, bool munin)
        {
            float pitch = munin ? 0.95f : 1.35f;
            ScreamFx.CawAll(p, munin, pitch, 0.45f);
            yield return new WaitForSeconds(0.28f);
            if (p != null) ScreamFx.CawAll(p, munin, pitch * 1.12f, 0.4f);
            if (UnityEngine.Random.value < 0.4f) { yield return new WaitForSeconds(0.26f); if (p != null) ScreamFx.CawAll(p, munin, pitch * 1.2f, 0.35f); }
        }

        // their raven snaps back, yours has the last word now and then (the bridge bubble, everyone sees it)
        private static bool Quarrel(Player p, Session s, string line)
        {
            Player t = s.Char as Player;
            if (t == null || UnityEngine.Random.value >= QuarrelChance) return false;
            List<int> can = new List<int>();
            foreach (bool m in new[] { false, true }) if (CrossTalk.CanTalk(t, m)) can.Add(m ? 1 : 0);
            if (can.Count == 0) return false;
            bool theirs = can.Count == 1 ? can[0] == 1 : UnityEngine.Random.value < 0.55f;
            ItemDrop.ItemData mine = RavenSeat.On(p, s.Munin);
            CrossTalk.Who a = new CrossTalk.Who { P = p, Munin = s.Munin, Level = mine != null ? Mathf.Max(1, Trust.Level(mine)) : 1 };
            CrossTalk.Who b = new CrossTalk.Who { P = t, Munin = theirs, Level = Mathf.Max(1, Shoulder.LevelOn(t, theirs)) };
            string dir = Plugin.Instance.PhraseDir;
            Dictionary<string, string> v = new Dictionary<string, string>();
            v["name"] = t.GetPlayerName(); v["friend"] = p.GetPlayerName();
            v["raven"] = Nicks.Name(p, s.Munin); v["addr"] = Nicks.Call(p, s.Munin); v["me"] = Nicks.Name(t, theirs);
            string back = Composer.ComposeCross(dir, theirs, "victim_back", b.Level, Grammar.Female(t), Grammar.Female(p), v, Voice.CodeOn(t, theirs));
            if (string.IsNullOrEmpty(back)) return false;
            List<CrossTalk.Line> lines = new List<CrossTalk.Line>
            {
                new CrossTalk.Line { Who = 0, To = 1, Text = RavenSpeech.Mock + line },
                new CrossTalk.Line { Who = 1, To = 0, Text = back },
            };
            if (UnityEngine.Random.value < 0.6f)
            {
                Dictionary<string, string> w = new Dictionary<string, string>();
                w["name"] = p.GetPlayerName(); w["friend"] = t.GetPlayerName();
                w["raven"] = Nicks.Name(t, theirs); w["addr"] = Nicks.Call(t, theirs); w["me"] = Nicks.Name(p, s.Munin);
                string again = Composer.ComposeCross(dir, s.Munin, "victim_retort", a.Level, Grammar.Female(p), Grammar.Female(t), w, Voice.Code(mine));
                if (!string.IsNullOrEmpty(again)) lines.Add(new CrossTalk.Line { Who = 0, To = 1, Text = RavenSpeech.Mock + again });
            }
            CrossTalk.Script("victim", new List<CrossTalk.Who> { a, b }, lines); // (counted with the director there, as every talk)
            Laugh(p, s.Munin);
            return true;
        }

        // the other raven of yours, mocked, answers it (your two's bubble, sent to the others near)
        private static bool Bicker(Player p, Session s, string line)
        {
            bool other = !s.Munin;
            ItemDrop.ItemData r = RavenSeat.On(p, other);
            if (r == null || !Trust.Active(r)) return false;
            string back = Make(p, other, new Session { Kind = Kind.Raven, Name = Plugin.RavenName(s.Munin), Female = false }, "victim_raven_back", null);
            if (back == null) return false;
            List<TalkBubble.Line> lines = new List<TalkBubble.Line>
            {
                new TalkBubble.Line { Munin = s.Munin, Text = line },
                new TalkBubble.Line { Munin = other, Text = back },
            };
            TalkBubble.Play(p, lines, true);
            RavenTalk.Send(p, lines);
            float total = 0f;
            foreach (TalkBubble.Line l in lines) total += TalkBubble.Pace(l.Text);
            Director.Spoke(Director.Voice.Event, total + 2f);
            Laugh(p, s.Munin);
            ShoulderLife life = Shoulder.LocalLife(s.Munin);
            if (life != null) life.Act("Дражнить");
            return true;
        }

        // ---------------------------------------------------------------- what it sees them do

        private static void Watch(Player p, Session s)
        {
            if (s.Kind == Kind.Raven || s.Kind == Kind.Trader) return;
            float now = Time.time;
            Character c = s.Char;
            if (s.Kind == Kind.Player && c == null)
            {
                // gone: died near you (the game takes a dead player away), or left; back again when they come back
                if (s.Present && s.Hp < 0.4f && s.Dist < Range + 15f) Event(p, s, "death");
                s.Present = false;
                foreach (Player o in Player.GetAllPlayers()) if (o != null && o != p && o.GetPlayerName() == s.Name) { s.Char = o; break; }
                if (s.Char != null) { s.Present = true; s.Hp = s.Char.GetHealthPercentage(); s.Y0 = s.Y1 = s.Char.transform.position.y; s.StillAt = s.Char.transform.position; s.StillSince = now; }
                return;
            }
            if (c == null) return;
            Vector3 pos = c.transform.position;
            s.Dist = Vector3.Distance(p.transform.position, pos);
            if (c.IsDead()) { if (s.Present) Event(p, s, "death"); s.Present = false; return; }
            float hp = c.GetHealthPercentage();
            if (s.Hp - hp >= 0.12f) Event(p, s, "hurt");
            float speed = s.Prev == Vector3.zero ? 0f : Vector3.Distance(s.Prev, pos) / 0.5f; // (another game's body has no speed of its own here: by the steps)
            s.Prev = pos;
            bool foes = BaseAI.HaveEnemyInRange(c, pos, 20f); // (one look a tick: near 8 m is a fight, within 20 m a flight)
            if (hp < 0.35f && speed > 5f && foes) Event(p, s, "flee");
            s.Hp = hp;
            float y = pos.y;
            if (s.Y0 - y > 5f && y < 3000f) Event(p, s, "fall"); // five metres down within a second
            s.Y0 = s.Y1; s.Y1 = y;
            if (foes && BaseAI.HaveEnemyInRange(c, pos, 8f)) Event(p, s, "fight");
            if ((pos - s.StillAt).sqrMagnitude > 1.5f) { s.StillAt = pos; s.StillSince = now; }
            else if (now - s.StillSince > 40f) { Event(p, s, "idle"); s.StillSince = now; }
            Player t = c as Player;
            if (t == null) return;
            if (t.InBed()) Event(p, s, "sleep");
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (y < sea - 1.1f && y > sea - 30f && (t == p ? t.IsSwimming() : true)) Event(p, s, "swim");
            ZNetView nv = t.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (z != null)
            {
                int emote = z.GetInt(ZDOVars.s_emoteID, 0);
                if (s.Emote >= 0 && emote != s.Emote && z.GetString(ZDOVars.s_emote, "").Length > 0) Event(p, s, "emote");
                s.Emote = emote;
                if (z.GetInt(ZDOVars.s_rightItem, 0) == HammerHash) Event(p, s, "build");
            }
        }
        private static readonly int HammerHash = "Hammer".GetStableHashCode();

        // a thing seen: fresh for the next jeer (a big one brings it sooner); remembered for the gossip; the prophecy
        private static void Event(Player p, Session s, string ev)
        {
            float now = Time.time, last;
            if (s.EvLast.TryGetValue(ev, out last) && now - last < (ev == "death" ? 5f : 40f)) return;
            s.EvLast[ev] = now;
            int had = s.Ev == null ? -1 : Weight(s.Ev);
            if (s.Ev == null || now - s.EvAt > 12f || Weight(ev) >= had) { s.Ev = ev; s.EvAt = now; }
            s.Seen.Remove(ev); s.Seen.Add(ev);
            if (Weight(ev) >= 2) s.NextLine = Mathf.Min(s.NextLine, Mathf.Max(now + 2.5f, s.LastLine + 14f));
            if (s.Prophecy != null && s.ProphecyAt < 0f && !s.ProphecyTrue && (ev == s.Prophecy || (s.Prophecy == "hurt" && (ev == "fall" || ev == "death"))))
            {
                s.ProphecyTrue = true;
                if (Free(p, false))
                {
                    string line = Make(p, false, s, "victim_prophecy_true", null);
                    if (line != null) { Say(p, s, false, line, false); Trust.Deed(false, ProphecyTrust, Lang.T("пророцтво")); }
                }
            }
        }

        private static int Weight(string ev) { return ev == "death" ? 3 : ev == "fall" || ev == "hurt" || ev == "flee" ? 2 : 1; }

        // ---------------------------------------------------------------- Hugin's prophecy

        private static void Foretell(Player p, Session s)
        {
            string line = Make(p, false, s, "victim_prophecy_" + s.Prophecy, null);
            if (line == null) { s.Prophecy = null; return; }
            Say(p, s, false, line, false);
            s.NextLine = Mathf.Max(s.NextLine, Time.time + 25f);
        }

        // ---------------------------------------------------------------- the prank: onto their head, pecking

        private static void Prank(Player p, Session s)
        {
            float now = Time.time;
            ItemDrop.ItemData item = RavenSeat.On(p, s.Munin);
            if (s.Spot != null)
            {
                bool lost = item == null || !s.Spot.Valid || (!Perches.OnSpot(item) && now - s.PrankFrom > 6f);
                if (lost || now >= s.PrankUntil || p.IsDead() || (Senses.Ready && Senses.Fight)) { Back(p, s); return; }
                if (!Perches.OnSpot(item) || !s.Spot.Settled(0)) return; // (still on its way)
                if (!s.OnHead) { s.OnHead = true; s.PrankUntil = now + HeadSeconds; s.NextHeadLine = now + 0.8f; } // landed: its minute starts now
                Character under = s.Kind == Kind.Own ? (Character)p : s.Char;
                if (under != null) // running or jumping (every tick, half a second: 2.5 m is about 5 m/s)
                {
                    Vector3 pos = under.transform.position;
                    if (s.HeadPrev != Vector3.zero && (pos - s.HeadPrev).magnitude > 2.5f) s.MovedAt = now;
                    s.HeadPrev = pos;
                }
                if (now >= s.NextPeck)
                {
                    s.NextPeck = now + PeckEvery + UnityEngine.Random.Range(0f, 1.5f);
                    ZDO z = s.Spot.Zdo;
                    if (z != null && s.Spot.Owner) z.Set(SpotFollow.PeckKey, z.GetInt(SpotFollow.PeckKey, 0) + 1);
                    if (s.Kind == Kind.Own) Pecked(p, PeckDamage); // you: for real, a little
                    else if (s.Kind == Kind.Player) SendPeck(s.Char as Player); // another player: their own game takes it off
                }
                if (now >= s.NextHeadLine) HeadLine(p, s, now);
                return;
            }
            if (s.PrankAt < 0f || now < s.PrankAt) return;
            if (item == null || !Free(p, s.Munin) || !Shoulder.Settled(p, s.Munin, 2f) || s.Char == null || s.Char.IsDead() || s.Dist > 20f || (Senses.Ready && (Senses.Fight || Senses.Underground || Senses.Swimming)))
            {
                if (now > s.PrankAt + 90f) s.PrankAt = -1f; // the moment never came
                return;
            }
            s.PrankAt = -1f;
            PerchView v;
            string why = SitOn.Go(p, s.Munin, s.Char, false, s.Kind == Kind.Beast ? SitOn.Back : SitOn.Head, out v);
            if (why != null || v == null) { Log("Raven victim: no prank (" + (why ?? "no spot") + ")"); return; }
            s.Spot = v;
            s.PrankFrom = now;
            s.PrankUntil = now + ReachSeconds; // (to get there; landed, its minute: OnHead)
            s.NextPeck = now;
            s.OnHead = false; s.HeadLines = 0;
            Laugh(p, s.Munin);
        }

        // one line from their head: as soon as the last is read and nobody near talks; while they run or jump, about that
        private static void HeadLine(Player p, Session s, float now)
        {
            Vector3 at = s.Spot != null ? s.Spot.transform.position : p.transform.position;
            bool voiceM;
            if (Cozy.Quieted || RavenVoice.Busy(p, out voiceM) || TalkNear(at, QuietRange)) { s.NextHeadLine = now + 1.2f; return; }
            bool moving = now - s.MovedAt < 2.5f; // (ran or jumped just now: Prank watches it every tick)
            string fam = Fam(s.Kind);
            string line = Make(p, s.Munin, s, moving && s.HeadLines > 0 ? fam + "_head_move" : fam + "_head", fam + "_prank");
            if (line == null) { s.NextHeadLine = now + 5f; return; }
            s.HeadLines++;
            Say(p, s, line, false);
            if (s.HeadLines % 2 == 1) Laugh(p, s.Munin);
            s.NextHeadLine = now + RavenSpeech.ReadSeconds(line) * 0.85f + UnityEngine.Random.Range(0.4f, 1.4f);
        }

        // someone near is talking: words over a player's head (the chat), an NPC's, a raven's card (the cozy quiet, set
        // by any raven line near, already covers the ravens); read from the chat's own lists
        private static System.Reflection.FieldInfo _worldTexts, _npcTexts;
        private static bool _textsLooked;
        private static bool TalkNear(Vector3 at, float r)
        {
            if (Chat.instance == null) return false;
            if (!_textsLooked) { _textsLooked = true; _worldTexts = HarmonyLib.AccessTools.Field(typeof(Chat), "m_worldTexts"); _npcTexts = HarmonyLib.AccessTools.Field(typeof(Chat), "m_npcTexts"); }
            try
            {
                if (Near(_worldTexts, "m_position", at, r)) return true;
                if (Near(_npcTexts, "m_go", at, r)) return true;
            }
            catch { }
            return false;
        }

        private static readonly Dictionary<string, System.Reflection.FieldInfo> Fields = new Dictionary<string, System.Reflection.FieldInfo>();
        private static System.Reflection.FieldInfo FieldOf(Type t, string field)
        {
            string key = t.FullName + "." + field;
            System.Reflection.FieldInfo f;
            if (!Fields.TryGetValue(key, out f)) { f = HarmonyLib.AccessTools.Field(t, field); Fields[key] = f; }
            return f;
        }

        private static bool Near(System.Reflection.FieldInfo list, string field, Vector3 at, float r)
        {
            System.Collections.IList l = list != null ? list.GetValue(Chat.instance) as System.Collections.IList : null;
            if (l == null) return false;
            foreach (object o in l)
            {
                if (o == null) continue;
                System.Reflection.FieldInfo f = FieldOf(o.GetType(), field);
                object v = f != null ? f.GetValue(o) : null;
                Vector3 pos;
                if (v is Vector3) pos = (Vector3)v;
                else { GameObject g = v as GameObject; if (g == null) continue; pos = g.transform.position; }
                if ((pos - at).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        private static void Back(Player p, Session s)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, s.Munin);
            s.Spot = null;
            if (item != null) Perches.CallQuiet(p, item, s.Munin);
            s.NextLine = Mathf.Max(s.NextLine, Time.time + 8f);
        }

        // ---------------------------------------------------------------- the end

        private static void Stop(Player p, bool spoken)
        {
            Session s = _s;
            _s = null;
            if (s == null) return;
            if (s.Spot != null) Back(p, s);
            if (p != null && spoken && !p.IsDead())
            {
                float after = 2f;
                if (Free(p, s.Munin) && Near(p, s))
                {
                    string line = Make(p, s.Munin, s, Fam(s.Kind) + "_end", null);
                    if (line != null) { Say(p, s, line, false); after = RavenSpeech.ReadSeconds(line) + 2f; }
                }
                // the prophecy not come true: Munin laughs at Hugin (or Hugin grumbles alone)
                if (s.Prophecy != null && s.ProphecyAt < 0f && !s.ProphecyTrue)
                {
                    bool m = Free(p, true);
                    string line = Make(p, m, s, m ? "victim_prophecy_false" : "victim_prophecy_miss", null);
                    if (line != null && (m || Free(p, false))) Plugin.Instance.StartCoroutine(Later(p, m, line, after)); // (once the end line is read)
                }
                if ((s.Kind == Kind.Player || s.Kind == Kind.Beast) && s.Seen.Count > 0 && UnityEngine.Random.value < GossipAfter)
                {
                    _gossipAt = Time.time + UnityEngine.Random.Range(120f, 360f);
                    _gossipUntil = _gossipAt + 600f;
                    _gossipName = s.Name; _gossipFemale = s.Female; _gossipMunin = s.Munin; _gossipKind = s.Kind;
                    _gossipEv = s.Seen[UnityEngine.Random.Range(0, s.Seen.Count)];
                }
            }
            if (s.Kind == Kind.Player) Tell(s.Char as Player, p, s.Munin, false);
            if (p != null) Publish(p);
            Log("Raven victim: the spell over (" + s.Lines + " jeers)");
        }

        private static IEnumerator Later(Player p, bool munin, string line, float after)
        {
            yield return new WaitForSeconds(after);
            if (p == null || p.IsDead() || Hushed(p)) yield break;
            RavenSkills.SayFromShoulder(p, munin, RavenSpeech.Mock + line);
            Laugh(p, munin);
        }

        // a while after the spell, about someone not here: what it saw (if they are not right here)
        // true: done (said, or nothing to say: they are right here); false: not now
        private static bool LateGossip(Player p)
        {
            if (!Free(p, _gossipMunin) || Hushed(p) || (Senses.Ready && Senses.Fight) || !Director.May(Director.Voice.Shoulder)) return false;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && o.GetPlayerName() == _gossipName && Vector3.Distance(o.transform.position, p.transform.position) < Range) return true;
            Session s = new Session { Kind = _gossipKind, Munin = _gossipMunin, Name = _gossipName, Female = _gossipFemale };
            string line = Make(p, s.Munin, s, "victim_gossip_" + _gossipEv, "victim_gossip");
            if (line != null) Say(p, s, line, false);
            return true;
        }

        // ---------------------------------------------------------------- the victim told (their game: a line in the middle)

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnTold));
            ZRoutedRpc.instance.Register<float>(PeckRpc, new Action<long, float>(OnPeck));
        }

        // ---------------------------------------------------------------- a peck on a head: a little, never to death

        // the health a peck takes, on this game's own player (never below 1 HP over the peck: it cannot kill)
        private static void Pecked(Player me, float dmg)
        {
            if (me == null || me.IsDead() || dmg <= 0f || me.GetHealth() <= dmg + 1f) return;
            me.SetHealth(me.GetHealth() - dmg);
            if (DamageText.instance != null) DamageText.instance.ShowText(HitData.DamageModifier.Normal, me.GetTopPoint(), dmg, true);
        }

        private static void SendPeck(Player victim)
        {
            if (victim == null || PeckDamage <= 0f || ZRoutedRpc.instance == null || Dummy.Is(victim)) return;
            ZNetView nv = victim.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(nv.GetZDO().GetOwner(), PeckRpc, PeckDamage);
        }

        // a peck from another player's raven: taken only while a raven of theirs really sits on you (its seat is theirs
        // and follows you), at most every PeckMinGap from each, never more than PeckDamage, and never if «not me»
        private static readonly Dictionary<long, float> PeckedAt = new Dictionary<long, float>();
        private static void OnPeck(long sender, float dmg)
        {
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID() || (LetOthers != null && !LetOthers.Value)) return;
            float last;
            if (PeckedAt.TryGetValue(sender, out last) && Time.time - last < PeckMinGap) return;
            bool sits = false;
            ZDOID mine = me.GetZDOID();
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.IsSpot || v.Zdo == null || v.Zdo.GetOwner() != sender) continue;
                SpotFollow f = v.GetComponent<SpotFollow>();
                if (f != null && f.Target == mine) { sits = true; break; }
            }
            if (!sits) return;
            PeckedAt[sender] = Time.time;
            if (PeckedAt.Count > 16) PeckedAt.Clear();
            Pecked(me, Mathf.Clamp(dmg, 0f, PeckDamage));
        }

        private static void Tell(Player victim, Player p, bool munin, bool start)
        {
            if (victim == null || p == null || ZRoutedRpc.instance == null || Dummy.Is(victim)) return;
            ZNetView nv = victim.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetPlayerName());
            pkg.Write(munin);
            pkg.Write(start);
            ZRoutedRpc.instance.InvokeRoutedRPC(nv.GetZDO().GetOwner(), Rpc, pkg);
        }

        private static void OnTold(long sender, ZPackage pkg)
        {
            string who = pkg.ReadString();
            bool munin = pkg.ReadBool(), start = pkg.ReadBool();
            Player me = Player.m_localPlayer;
            if (me == null) return;
            string raven = Plugin.RavenName(munin);
            if (start) Bonds.OnMocked(who, munin); // (my ravens remember that raven)
            RavenMsg.Center(me, start ? Lang.F("{0} гравця {1} обрав тебе жертвою. Тримайся.", raven, who) : Lang.F("{0} гравця {1} відчепився від тебе.", raven, who));
        }

        // ---------------------------------------------------------------- the console: raven victim [me|<name>|beast|trader|raven|stop]

        internal static string Cmd(Player p, string[] a)
        {
            if (p == null) return Lang.T("Нема гравця.");
            string arg = a.Length > 1 ? a[1] : "";
            if (arg.ToLowerInvariant() == "stop") { if (_s == null) return Lang.T("Ніхто нікого не дражнить."); Stop(p, true); return Lang.T("Глузування скінчилося."); }
            if (_s != null)
                return Lang.F("{0} дражнить {1} ще {2} с ({3} разів). raven victim stop — кінець.", Plugin.RavenName(_s.Munin), _s.Name, Mathf.CeilToInt(_s.Until - Time.time), _s.Lines);
            if (arg.Length == 0 && a.Length == 1)
                return Lang.F("Сьогодні: інших {0}, тебе {1}. raven victim me|[ім'я]|beast|trader|raven|any — почати зараз.", _otherAt > 0f ? Lang.F("за {0} с", Mathf.CeilToInt(_otherAt - Time.time)) : "—", _ownAt > 0f ? Lang.F("за {0} с", Mathf.CeilToInt(_ownAt - Time.time)) : "—");
            bool own = arg.ToLowerInvariant() == "me";
            string who = own || arg.ToLowerInvariant() == "any" ? null : string.Join(" ", a, 1, a.Length - 1);
            if (!Free(p, false) && !Free(p, true)) return Lang.F("Немає вільного ворона на плечі (довіра {0}+, ситий).", MinLevel);
            if (!Start(p, own, who)) return Lang.T("Нема кого дражнити поруч (або зараз не час: бій, сон).");
            return Lang.F("{0} дражнить {1}.", Plugin.RavenName(_s.Munin), _s.Name);
        }

        private static void Reset() { _s = null; _day = -1; _otherAt = _ownAt = _gossipAt = -1f; }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log(s); }
    }
}
#endif

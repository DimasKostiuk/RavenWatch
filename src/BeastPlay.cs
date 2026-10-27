#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // «Гра зі звіром» (the user, 2026-10-27): a raven plays tag with your tamed beast — on your word (J: the perch's button,
    // looking at your beast) or by itself now and then (about every half hour, by its character). Four parts:
    //   0 Tease  — it flies to the beast's tail, tugs at it;
    //   1 Run    — it runs off low round the place, zigzagging, and the beast is after it;
    //   2 Hunt   — the other way round: it dives at the beast, the beast runs;
    //   3 Final  — a win (it lands on the beast's back) or caught (down on the grass by its nose), decided at the start.
    // Its words at each turn (by the beast's kind: a wolf, a boar, a lox, an askvin, a hen; else any). After: a little
    // trust for the raven, a good share of a meal for the beast (its «fed» clock).
    // It is an order (Orders.Beast) flown by BeastPlan on every game: the owner writes the start, the seed (the win in
    // it) and the beast on the player's ZDO, so every game flies the bird the same; the beast runs by its own AI, steered
    // by the owner's game (it takes the beast over for the game), and is seen moving as any creature is.
    internal static class BeastPlay
    {
        internal static float Tease = 4f, Run = 14f, Hunt = 13f, Final = 5f;
        internal static float Seconds { get { return Tease + Run + Hunt + Final; } }
        internal static float CheckEvery = 60f, Chance = 0.35f, PauseMin = 25f, PauseMax = 35f; // by itself: a roll a minute, then 25–35 min
        internal static float Near = 15f, OrderNear = 25f, FedShare = 0.5f, LureAhead = 8f, LureAway = 12f, Roam = 18f;
        internal const string PlayKey = "RavenWatch_BeastA", BeastKey = "RavenWatch_BeastZ", CenterKey = "RavenWatch_BeastC"; // (+H/M)

        internal static int Phase(float t) { return t < Tease ? 0 : t < Tease + Run ? 1 : t < Tease + Run + Hunt ? 2 : 3; }
        internal static bool Wins(int seed) { return (seed & 2) != 0; }

        private sealed class Game
        {
            public bool Munin, Ordered;
            public Character Beast;
            public MonsterAI Ai;
            public GameObject Prev;
            public float Start, TeaseAt;
            public int Phase = -1, Seed;
            public string Name, Kind;
        }
        private static readonly Game[] Games = new Game[2];
        private static readonly GameObject[] Lures = new GameObject[2]; // (each raven's game its own: two at once must not pull one beast's lure)
        private static float _nextCheck, _allowedAt = -1f;

        // ---------------------------------------------------------------- the record, for every game
        internal static bool Rec(Player p, bool munin, out float start, out int seed, out string beast, out Vector3 center)
        {
            start = 0f; seed = 0; beast = null; center = Vector3.zero;
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            if (z == null) return false;
            string s = munin ? "M" : "H";
            long play = z.GetLong(PlayKey + s, -1L);
            if (play < 0L) return false;
            bool unused;
            ShipPlay.Unpack(play, out start, out seed, out unused);
            beast = z.GetString(BeastKey + s, "");
            center = z.GetVec3(CenterKey + s, p.transform.position);
            return beast.Length > 0;
        }

        internal static Character Find(string id)
        {
            if (string.IsNullOrEmpty(id) || ZNetScene.instance == null) return null;
            string[] w = id.Split(':');
            long user; uint n;
            if (w.Length != 2 || !long.TryParse(w[0], out user) || !uint.TryParse(w[1], out n)) return null;
            GameObject g = ZNetScene.instance.FindInstance(new ZDOID(user, n));
            return g != null ? g.GetComponent<Character>() : null;
        }

        // how high its back is over its feet (a lox: high; a hen: hardly)
        internal static float BackHeight(Character c) { return c != null ? Mathf.Clamp(c.GetTopPoint().y - c.transform.position.y, 0.3f, 4f) * 0.85f + 0.1f : 1f; }

        // ---------------------------------------------------------------- may it play with this one
        internal static string WhyNot(Player p, Character beast)
        {
            if (beast == null || beast.IsDead() || !beast.IsTamed()) return Lang.T("це не приручений звір");
            if (Vector3.Distance(beast.transform.position, p.transform.position) > OrderNear) return Lang.T("звір задалеко");
            Sadle sadle = beast.GetComponentInChildren<Sadle>();
            if (sadle != null && sadle.HaveValidUser()) return Lang.T("на ньому хтось їде");
            MonsterAI ai = beast.GetBaseAI() as MonsterAI;
            if (ai == null) return Lang.T("цей звір не грається");
            if (ai.GetTargetCreature() != null || ai.IsAlerted()) return Lang.T("звірові зараз не до ігор");
            foreach (Game g in Games) if (g != null && g.Beast == beast) return Lang.T("уже грається");
            return null;
        }

        // the J button / the perch key, looking at your tamed beast
        internal static bool Order(Player p, bool munin, Character beast)
        {
            if (p == null) return false;
            string why = WhyNot(p, beast) ?? Orders.Why(p, munin, Orders.Beast); // (the order's own reasons first: nothing taken over for nothing)
            if (why != null) { p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0}: гратися — {1}", Plugin.RavenName(munin), why), 0, RavenItem.Icon(munin)); return false; }
            Game g = Begin(p, munin, beast, true);
            if (g == null) return false;
            if (!Orders.GiveFor(p, munin, Orders.Beast, Seconds + 1f)) { Clear(p, munin); Games[munin ? 1 : 0] = null; return false; }
            return true;
        }

        // aimed at a tamed beast (and not called back from a perch): the perch's button plays instead
        internal static Character Aimed(Player p, bool munin)
        {
            SitHere.Target t = p != null ? SitHere.Look(p, munin) : null;
            return t != null && t.Animal != null && t.Animal.IsTamed() ? t.Animal : null;
        }

        // the perch key (RavenKeys): your beast in sight → a game; else the perch as ever
        internal static void PerchOrPlay(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            Character beast = item != null && !Perches.On(item) ? Aimed(p, munin) : null;
            if (beast != null) Order(p, munin, beast); else Perches.Order(p, munin);
        }

        private static Game Begin(Player p, bool munin, Character beast, bool ordered)
        {
            ZDO z = NetFlag.Zdo(p, true);
            ZNetView bnv = beast.GetComponent<ZNetView>();
            MonsterAI ai = beast.GetBaseAI() as MonsterAI;
            if (z == null || bnv == null || !bnv.IsValid() || ai == null) return null;
            if (!bnv.IsOwner()) bnv.ClaimOwnership(); // (its AI runs where it is owned: here, for the game)
            string kind = KindOf(beast);
            bool win = UnityEngine.Random.value < WinChance(RavenSeat.On(p, munin), kind);
            int seed = (UnityEngine.Random.Range(0, 256) & ~2) | (win ? 2 : 0);
            float start = ShipPlay.Fold(ShipPlay.NetNow + 0.5);
            string s = munin ? "M" : "H";
            ZDOID id = bnv.GetZDO().m_uid;
            z.Set(PlayKey + s, ShipPlay.Pack(start, seed, false));
            z.Set(BeastKey + s, id.UserID + ":" + id.ID);
            z.Set(CenterKey + s, beast.transform.position);
            Game g = new Game { Munin = munin, Ordered = ordered, Beast = beast, Ai = ai, Prev = ai.GetFollowTarget(), Start = Time.time + 0.5f, Seed = seed, Name = SitOn.NameOf(beast), Kind = kind };
            g.TeaseAt = g.Start + Tease + UnityEngine.Random.Range(4f, 8f);
            Games[munin ? 1 : 0] = g;
            _allowedAt = Time.time + UnityEngine.Random.Range(PauseMin, PauseMax) * 60f;
            Outing.LastAt = Time.time;
            Errands.Log("beast play: " + Plugin.RavenName(munin) + " with " + Utils.GetPrefabName(beast.gameObject) + (ordered ? " (ordered)" : " (by itself)") + (win ? ", wins" : ", caught"));
            return g;
        }

        private static void Clear(Player p, bool munin)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, true) : null;
            if (z != null) z.Set(PlayKey + (munin ? "M" : "H"), -1L);
        }

        // the game's own kinds (their words); else any
        private static string KindOf(Character c)
        {
            string n = Utils.GetPrefabName(c.gameObject).ToLowerInvariant();
            return n.Contains("wolf") ? "wolf" : n.Contains("boar") ? "boar" : n.Contains("lox") ? "lox" : n.Contains("asksvin") ? "asksvin" : n.Contains("hen") || n.Contains("chicken") ? "hen" : "";
        }

        // who wins: the bold and the quick more often; the slow lox and the hen seldom catch it, a wolf often
        private static float WinChance(ItemDrop.ItemData raven, string kind)
        {
            Norov.Character c = Norov.CharOf(raven);
            string id = c != null ? c.Id : "";
            float w = id == "bold" ? 0.7f : id == "fidget" ? 0.6f : id == "calm" ? 0.4f : 0.5f;
            if (kind == "lox" || kind == "hen") w += 0.25f;
            else if (kind == "wolf") w -= 0.15f;
            return Mathf.Clamp01(w);
        }

        // ---------------------------------------------------------------- twice a second (the owner's game)
        internal static void Tick(Player p)
        {
            if (p == null) return;
            if (_allowedAt < 0f) _allowedAt = Time.time + UnityEngine.Random.Range(5f, 15f) * 60f; // (not the moment you come in)
            for (int k = 0; k < 2; k++) if (Games[k] != null) Steer(p, k);
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckEvery;
            if (Games[0] != null || Games[1] != null || Time.time < _allowedAt || p.IsDead() || Senses.Fight || Senses.OnShip || p.IsRiding() || !Outing.CanGo(p) || UnityEngine.Random.value >= Chance) return;
            Character beast = NearBeast(p);
            if (beast == null) return;
            bool h = Ready(p, false), m = Ready(p, true);
            if (!h && !m) return;
            float wh = h ? Playful(RavenSeat.On(p, false)) : 0f, wm = m ? Playful(RavenSeat.On(p, true)) : 0f;
            bool munin = h && m ? UnityEngine.Random.value * (wh + wm) < wm : m;
            if (Begin(p, munin, beast, false) == null) return;
            Satiety.Flew(munin, Orders.HungerOf(Orders.Beast));
            Orders.StartBeast(p, munin, Seconds + 1f);
        }

        private static bool Ready(Player p, bool munin)
        {
            ItemDrop.ItemData it = RavenSeat.On(p, munin);
            return it != null && Trust.Active(it) && !Trust.Stranger(it, p) && !Trust.Away(it) && Satiety.Zone(it) > Satiety.Hungry
                && Orders.Of(p, munin) == Orders.None && Outing.ReadyRaven(p, munin);
        }

        // by its character: the fidget and the bold love a game, the calm one less
        private static float Playful(ItemDrop.ItemData item)
        {
            Norov.Character c = Norov.CharOf(item);
            string id = c != null ? c.Id : "";
            return id == "fidget" || id == "bold" ? 1.5f : id == "calm" ? 0.6f : 1f;
        }

        private static Character NearBeast(Player p)
        {
            Character best = null; float bd = Near * Near;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || !c.IsTamed()) continue;
                float d = (c.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd && WhyNot(p, c) == null) { bd = d; best = c; }
            }
            return best;
        }

        // the beast's part: where its AI is to go now
        private static void Steer(Player p, int k)
        {
            Game g = Games[k];
            bool munin = k == 1;
            float t = Time.time - g.Start;
            bool gone = g.Beast == null || g.Beast.IsDead() || g.Ai == null;
            if (gone || t > Seconds || p.IsDead() || (t > 1f && Orders.Of(p, munin) != Orders.Beast))
            {
                End(p, g, !gone && !p.IsDead() && t > Seconds - 2f);
                Games[k] = null;
                return;
            }
            if (t < 0f) return;
            int ph = Phase(t);
            if (ph != g.Phase) { g.Phase = ph; Turn(p, g, ph); }
            GameObject raven = Shoulder.ModelOf(p, munin);
            Vector3 b = g.Beast.transform.position, r = raven != null ? raven.transform.position : b;
            Vector3 center;
            float start; int seed; string id;
            if (!Rec(p, munin, out start, out seed, out id, out center)) center = b;
            switch (ph)
            {
                case 0: g.Ai.SetFollowTarget(null); break; // (it stands: the raven comes for its tail)
                case 1: // after the raven: a lure past it, so the beast runs (its AI walks to a target close by)
                {
                    Vector3 way = Flat(r - b);
                    Lure(k, r + way * LureAhead, g.Ai);
                    if (Time.time >= g.TeaseAt) { g.TeaseAt = float.MaxValue; Say(p, g, "beast_play_tease", 0.7f); }
                    break;
                }
                case 2: // away from the raven, weaving, never far from where it began
                {
                    Vector3 way = Quaternion.Euler(0f, Mathf.Sin(t * 0.8f + g.Seed) * 50f, 0f) * Flat(b - r);
                    if (Vector3.Distance(b, center) > Roam) way = Flat(Vector3.Lerp(way, Flat(center - b), 0.7f));
                    Lure(k, b + way * LureAway, g.Ai);
                    break;
                }
                default: // the end: a win, it stands for the raven on its back; caught, it goes to where the raven lies
                    if (Wins(g.Seed)) g.Ai.SetFollowTarget(null);
                    else Lure(k, r, g.Ai);
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

        private static void Lure(int k, Vector3 at, MonsterAI ai)
        {
            if (Lures[k] == null) { Lures[k] = new GameObject("RavenWatch_BeastLure" + k); UnityEngine.Object.DontDestroyOnLoad(Lures[k]); }
            if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetGroundHeight(at);
            Lures[k].transform.position = at;
            if (ai.GetFollowTarget() != Lures[k]) ai.SetFollowTarget(Lures[k]);
        }

        // a turn of the game: its word
        private static void Turn(Player p, Game g, int ph)
        {
            if (ph == 0) { if (!Say(p, g, "beast_play_start_" + g.Kind, 1f)) Say(p, g, "beast_play_start", 1f); }
            else if (ph == 2) Say(p, g, "beast_play_swap", 0.8f);
            else if (ph == 3) Say(p, g, Wins(g.Seed) ? "beast_play_win" : "beast_play_caught", 1f);
        }

        // the game over: the beast back to what it did; played through — a little trust, the beast half fed
        private static void End(Player p, Game g, bool through)
        {
            if (g.Ai != null) g.Ai.SetFollowTarget(g.Prev);
            Clear(p, g.Munin);
            if (!through || g.Beast == null) return;
            Trust.Played(g.Munin, Lang.T("гра зі звіром"));
            Tameable tm = g.Beast.GetComponent<Tameable>();
            ZNetView nv = g.Beast.GetComponent<ZNetView>();
            if (tm == null || nv == null || !nv.IsValid() || !nv.IsOwner() || ZNet.instance == null) return;
            long now = ZNet.instance.GetTime().Ticks, was = nv.GetZDO().GetLong(ZDOVars.s_tameLastFeeding, 0L);
            long want = now - (long)(tm.m_fedDuration * (1f - FedShare) * TimeSpan.TicksPerSecond);
            if (want > was) nv.GetZDO().Set(ZDOVars.s_tameLastFeeding, want);
        }

        private static bool Say(Player p, Game g, string tag, float chance)
        {
            if (UnityEngine.Random.value >= chance || RavenSkills.Hushed(p)) return false;
            string line = Satiety.Say(p, g.Munin, tag, "beast", g.Name ?? "");
            if (string.IsNullOrEmpty(line)) return false;
            RavenSkills.SayFromShoulder(p, g.Munin, line);
            return true;
        }

        // a new world or character: a game cut off gives its beast back to what it did (never left after a lure)
        internal static void Reset()
        {
            for (int k = 0; k < 2; k++) { Game g = Games[k]; if (g != null && g.Ai != null) g.Ai.SetFollowTarget(g.Prev); Games[k] = null; }
            _allowedAt = -1f; _nextCheck = 0f;
        }

        // the console: raven beastplay [h|m] — a game now with the nearest tamed beast, as by itself (free)
        internal static string Cmd(Player p, string[] a)
        {
            bool munin = a.Length > 1 && (a[1] == "m" || a[1] == "munin");
            if (p == null) return "no player";
            if (!Ready(p, munin)) return Plugin.RavenName(munin) + " не готовий (не на плечі, спить, голодний, зайнятий).";
            Character beast = NearBeast(p);
            if (beast == null) return "Нема приручених звірів поруч (" + Mathf.RoundToInt(Near) + " м), вільних для гри.";
            if (Begin(p, munin, beast, false) == null) return "Не вийшло.";
            Orders.StartBeast(p, munin, Seconds + 1f);
            return Plugin.RavenName(munin) + " грається з: " + SitOn.NameOf(beast);
        }
    }

    // the flight itself, on every game the same from the record: to the tail, off round the place with the beast after it,
    // after the beast, and the end on its back or down by its nose
    internal sealed class BeastPlan : FlightPlan
    {
        private string _id;
        private Character _beast;
        private float _lookAt;
        internal override string Name { get { return Orders.Plans[Orders.Beast]; } }
        internal override void Begin() { _id = null; _beast = null; }
        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing)
        {
            float start; int seed; string id; Vector3 c;
            return BeastPlay.Rec(pin.Player, pin.Munin, out start, out seed, out id, out c) ? c + Vector3.up * 2f : base.TakeoffAim(pin, facing);
        }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            float start; int seed; string id; Vector3 center;
            float cruise = m.Body != null ? m.Body.Style.Cruise : 9f, top = m.Body != null ? m.Body.Style.Top : 13f;
            if (!BeastPlay.Rec(pin.Player, pin.Munin, out start, out seed, out id, out center))
            {
                m.Steer(pin.Player.transform.position + Vector3.up * 5f, cruise, false, 0f, dt, carry, 1f);
                m.Finish(dt, 0f);
                return;
            }
            if (id != _id || (_beast == null && Time.time >= _lookAt)) { _id = id; _beast = BeastPlay.Find(id); _lookAt = Time.time + 1f; }
            float t = Mathf.Max(0f, ShipPlay.Since(start));
            Vector3 b = _beast != null ? _beast.transform.position : center;
            Vector3 aim;
            float speed, flare = 0f;
            bool arrive = false, pursue = false;
            switch (BeastPlay.Phase(t))
            {
                case 0: // at its tail, low, tugging
                {
                    Vector3 back = _beast != null ? -_beast.transform.forward : Vector3.back;
                    aim = b + back * 0.9f + Vector3.up * (0.7f + 0.25f * Mathf.Sin(t * 9f));
                    speed = cruise * 0.6f; arrive = true; flare = 0.4f;
                    break;
                }
                case 1: // off round the place, low and weaving — the beast after it
                {
                    float u = t - BeastPlay.Tease, dir = (seed & 1) == 0 ? 1f : -1f;
                    float a = seed * 0.1f + dir * u * 0.6f, r = 7f + 2.5f * Mathf.Sin(u * 0.9f + seed);
                    Vector3 pt = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    pt.y = Ground(pt) + 1.3f + 0.6f * Mathf.Abs(Mathf.Sin(u * 2.1f));
                    aim = pt; speed = cruise * 0.8f;
                    break;
                }
                case 2: // after the beast: swooping down at its back and up again
                {
                    float u = t - BeastPlay.Tease - BeastPlay.Run;
                    aim = b + Vector3.up * (1f + 1.8f * Mathf.Abs(Mathf.Sin(u * 1.4f)));
                    speed = top; pursue = true;
                    break;
                }
                default:
                    if (BeastPlay.Wins(seed)) { aim = b + Vector3.up * BeastPlay.BackHeight(_beast); speed = cruise * 0.5f; arrive = true; flare = 0.6f; } // on its back, the winner
                    else
                    {
                        Vector3 f = _beast != null ? _beast.transform.forward : Vector3.forward;
                        Vector3 pt = b + f * 1.2f; pt.y = Ground(pt) + 0.3f;
                        aim = pt; speed = 3f; arrive = true; flare = 0.7f; // caught: down on the grass by its nose
                    }
                    break;
            }
            m.Steer(aim, speed, arrive, 0f, dt, Vector3.zero, 1f, pursue);
            m.Finish(dt, flare);
        }
    }
}
#endif

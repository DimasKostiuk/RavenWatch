#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «За кормою» (the user, 2026-10-26): under sail, now and then, two ravens play behind the ship as a pair — your own
    // two, or one of yours with a friend raven (Pals or closer) of another player on board. Three figures one after
    // another (of seven), then home. Their own idea: free; your own two gain a little trust, a pair of two players'
    // ravens a little friendship. It is an order (Orders.Ship) flown by ShipPlan on every game: the owner writes the
    // start (network time), the figures' seed and the mate on the player's ZDO, and every game puts each bird at the same
    // point of the same figure round the same ship.
    internal static class ShipPlay
    {
        internal static float CheckEvery = 60f, Chance = 0.3f, PauseMin = 8f, PauseMax = 12f; // a roll a minute under sail; after a play 8–12 min
        internal static float FigureSeconds = 14f;
        internal const int Figures = 3;
        internal static float Seconds { get { return FigureSeconds * Figures + 2f; } }
        internal static float ForeignShare = 0.5f, Near = 25f, TrustGain = 1f, BondGain = 3f;
        private const string RpcAsk = "RavenWatch_ShipAsk", RpcYes = "RavenWatch_ShipYes";
        internal const string PlayKey = "RavenWatch_ShipA", MateKey = "RavenWatch_ShipP"; // (+H/M) the play: start | seed | the mate's raven; the mate's player id (0: your own two)
        internal static long Pack(float start, int seed, bool mateMunin) { return ((long)Mathf.RoundToInt(start * 100f) << 9) | ((long)(seed & 0xFF) << 1) | (mateMunin ? 1L : 0L); }
        internal static void Unpack(long v, out float start, out int seed, out bool mateMunin) { mateMunin = (v & 1L) != 0; seed = (int)((v >> 1) & 0xFF); start = (v >> 9) / 100f; }

        private static float _next = -1f, _allowedAt;
        private static readonly Dictionary<long, float> Asked = new Dictionary<long, float>(); // (a partner asked: when; one ask at a time)

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcAsk, new Action<long, ZPackage>(OnAsk));
            ZRoutedRpc.instance.Register<ZPackage>(RpcYes, new Action<long, ZPackage>(OnYes));
        }

        internal static double NetNow { get { return SitNet.Seconds; } }
        // the network time folded into a day (a float keeps it to a hundredth); a difference of two folded back
        internal static float Fold(double t) { return (float)(t % 86400.0); }
        internal static float Since(float start) { float d = Fold(NetNow) - start; if (d < -43200f) d += 86400f; return d; }

        // twice a second (the owner's game)
        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead()) return;
            if (!Senses.Sailing || Senses.Fight || Outing.Indoors()) { _next = -1f; return; }
            if (_next < 0f) { _next = Time.time + CheckEvery; return; } // (under sail a while first)
            if (Time.time < _next) return;
            _next = Time.time + CheckEvery;
            if (Time.time < _allowedAt || UnityEngine.Random.value >= Chance || !Outing.CanGo(p)) return;
            Ship ship = ShipOf(p);
            if (ship == null) return;
            // a friend raven of another player on board, sometimes; else your own two
            if (UnityEngine.Random.value < ForeignShare && AskForeign(p, ship)) return;
            Own(p);
        }

        private static bool Ready(Player p, bool munin)
        {
            ItemDrop.ItemData it = RavenSeat.On(p, munin);
            return it != null && Trust.Active(it) && !Trust.Stranger(it, p) && !Trust.Away(it) && Satiety.Zone(Satiety.Value(it)) > Satiety.Hungry
                && Orders.Of(p, munin) == Orders.None && Outing.ReadyRaven(p, munin);
        }

        private static void Own(Player p)
        {
            if (!Ready(p, false) || !Ready(p, true)) return;
            float start = Fold(NetNow + 1.0);
            int seed = UnityEngine.Random.Range(0, 210);
            for (int i = 0; i < 2; i++) Begin(p, i == 1, start, 0L, false, seed);
            _allowedAt = Time.time + UnityEngine.Random.Range(PauseMin, PauseMax) * 60f;
            bool who = UnityEngine.Random.value < 0.5f;
            Say(p, who, who ? OwnMunin : OwnHugin);
            for (int i = 0; i < 2; i++) Trust.Ordered(i == 1); // (something done together)
            Errands.Log("ship play: own pair, seed " + seed);
        }

        // this raven goes out to play: its record on the player's ZDO (every game flies it), the order itself
        private static void Begin(Player p, bool munin, float start, long mate, bool mateMunin, int seed)
        {
            ZDO z = NetFlag.Zdo(p, true);
            if (z == null) return;
            string s = munin ? "M" : "H";
            z.Set(PlayKey + s, Pack(start, seed, mateMunin));
            z.Set(MateKey + s, mate);
            Satiety.Flew(munin, Orders.HungerTrick);
            Orders.StartShip(p, munin, Seconds + 1f);
            Nicks.OnShipPlay(p, munin); // a deed for its nickname
        }

        // ---- with another player's raven: my game (the lower id decides, as for the talks) asks theirs
        private static bool AskForeign(Player me, Ship ship)
        {
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me || o.IsDead() || Shadow.Flagged(o) || !CrossTalk.Decides(me, o)) continue;
                if (Vector3.Distance(o.transform.position, me.transform.position) > Near || ShipOf(o) != ship) continue;
                float asked;
                if (Asked.TryGetValue(o.GetPlayerID(), out asked) && Time.time - asked < 30f) continue;
                foreach (bool my in new[] { UnityEngine.Random.value < 0.5f, false, true })
                {
                    if (!Ready(me, my)) continue;
                    foreach (bool tm in new[] { false, true })
                    {
                        if (!CrossTalk.CanTalk(o, tm) || Bonds.StateOf(Bonds.PairScore(me, my, o, tm)) < Bonds.State.Pals) continue;
                        ZPackage pkg = new ZPackage();
                        pkg.Write(o.GetPlayerID()); pkg.Write(tm); pkg.Write(me.GetPlayerID()); pkg.Write(my);
                        float start = Fold(NetNow + 2.5); // (time for the answer to come back)
                        int seed = UnityEngine.Random.Range(0, 210);
                        pkg.Write(start); pkg.Write(seed);
                        if (ZRoutedRpc.instance == null) return false;
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcAsk, pkg);
                        Asked[o.GetPlayerID()] = Time.time;
                        _allowedAt = Time.time + 30f; // (till the answer: nothing else)
                        Errands.Log("ship play: asked " + o.GetPlayerName() + "'s " + (tm ? "Munin" : "Hugin"));
                        return true;
                    }
                }
            }
            return false;
        }

        // their game: its raven comes if it may, and says so back
        private static void OnAsk(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            long to = pkg.ReadLong(); bool mine = pkg.ReadBool(); long from = pkg.ReadLong(); bool theirs = pkg.ReadBool();
            float start = pkg.ReadSingle(); int seed = pkg.ReadInt();
            if (me == null || me.GetPlayerID() != to || sender == ZDOMan.GetSessionID()) return;
            Player o = PlayerOf(from);
            if (o == null || !Ready(me, mine) || !Outing.CanGo(me) || !Senses.OnShip) return;
            Begin(me, mine, start, from, theirs, seed);
            Bonds.Change(me, mine, o, theirs, BondGain);
            ZPackage back = new ZPackage();
            back.Write(from); back.Write(theirs); back.Write(to); back.Write(mine); back.Write(start); back.Write(seed);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, RpcYes, back);
            _allowedAt = Time.time + UnityEngine.Random.Range(PauseMin, PauseMax) * 60f;
            Later(me, mine, ForeignAnswer, 3f);
        }

        // my game: theirs said yes — mine goes out too, the same figures from the same moment
        private static void OnYes(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            long to = pkg.ReadLong(); bool mine = pkg.ReadBool(); long from = pkg.ReadLong(); bool theirs = pkg.ReadBool();
            float start = pkg.ReadSingle(); int seed = pkg.ReadInt();
            if (me == null || me.GetPlayerID() != to) return;
            Player o = PlayerOf(from);
            if (o == null || !Ready(me, mine)) return;
            Begin(me, mine, start, from, theirs, seed);
            Bonds.Change(me, mine, o, theirs, BondGain);
            _allowedAt = Time.time + UnityEngine.Random.Range(PauseMin, PauseMax) * 60f;
            Say(me, mine, ForeignCall);
            Errands.Log("ship play: with " + o.GetPlayerName() + "'s " + (theirs ? "Munin" : "Hugin") + ", seed " + seed);
        }

        private static Player PlayerOf(long id) { foreach (Player pl in Player.GetAllPlayers()) if (pl != null && pl.GetPlayerID() == id) return pl; return null; }

        // ---- the ship under a player (any game: the nearest ship, the player on or right by it)
        private static readonly List<Ship> Ships = new List<Ship>();
        private static float _shipsAt = -10f;
        internal static Ship ShipOf(Player p)
        {
            if (p == null) return null;
            if (ReferenceEquals(p, Player.m_localPlayer)) { Ship own = p.GetStandingOnShip() ?? p.GetControlledShip(); if (own != null) return own; } // (at the helm too)
            if (Time.time - _shipsAt > 2f) { _shipsAt = Time.time; Ships.Clear(); Ships.AddRange(UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None)); }
            Ship best = null; float bd = 400f;
            foreach (Ship s in Ships)
            {
                if (s == null) continue;
                float d = (s.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // a ship's size in its own frame (read once): x half its width (the sail's yard and the shields count), y how high
        // its mast stands over its middle, z half its length — whatever the ship, a raft or a longship; turned any way
        private static readonly Dictionary<Ship, Vector3> Size = new Dictionary<Ship, Vector3>();
        private static readonly Vector3[] Corner = new Vector3[8];
        internal static Vector3 SizeOf(Ship s)
        {
            Vector3 v;
            if (Size.TryGetValue(s, out v)) return v;
            Transform st = s.transform;
            Vector3 lo = new Vector3(-1.5f, 0f, -3f), hi = new Vector3(1.5f, 4f, 3f);
            bool any = false;
            foreach (Renderer r in s.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || r is ParticleSystemRenderer || !r.enabled) continue;
                Bounds b = r.localBounds;
                Vector3 c = b.center, e = b.extents;
                for (int k = 0; k < 8; k++)
                    Corner[k] = st.InverseTransformPoint(r.transform.TransformPoint(c + new Vector3((k & 1) != 0 ? e.x : -e.x, (k & 2) != 0 ? e.y : -e.y, (k & 4) != 0 ? e.z : -e.z)));
                foreach (Vector3 q in Corner)
                {
                    if (q.sqrMagnitude > 40f * 40f) continue; // (nothing of a ship is that far: something else hung on it)
                    if (!any) { lo = hi = q; any = true; } else { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                }
            }
            v = new Vector3(Mathf.Clamp(Mathf.Max(-lo.x, hi.x), 1.5f, 8f), Mathf.Clamp(hi.y, 4f, 18f), Mathf.Clamp(Mathf.Max(-lo.z, hi.z), 3f, 18f));
            if (Size.Count > 16) Size.Clear();
            Size[s] = v;
            Errands.Log("ship play: " + Utils.GetPrefabName(s.gameObject) + " is " + (v.z * 2f).ToString("0.#") + " m long, " + (v.x * 2f).ToString("0.#") + " m wide, mast " + v.y.ToString("0.#") + " m");
            return v;
        }

        // ---- words
        private static void Say(Player p, bool munin, string[] from)
        {
            if (p == null || !Shoulder.Present(p, munin) && Orders.Of(p, munin) != Orders.Ship) return;
            if (RavenSkills.Hushed(p)) return;
            RavenSkills.SayFromShoulder(p, munin, Lang.T(from[UnityEngine.Random.Range(0, from.Length)]));
        }

        private static readonly List<KeyValuePair<float, Action>> Queue = new List<KeyValuePair<float, Action>>();
        private static void Later(Player p, bool munin, string[] from, float seconds) { Queue.Add(new KeyValuePair<float, Action>(Time.time + seconds, () => Say(p, munin, from))); }
        internal static void Frame()
        {
            for (int i = Queue.Count - 1; i >= 0; i--)
                if (Time.time >= Queue[i].Key) { Action a = Queue[i].Value; Queue.RemoveAt(i); try { a(); } catch { } }
        }

        internal static void Reset() { _next = -1f; _allowedAt = 0f; Asked.Clear(); Queue.Clear(); Size.Clear(); Ships.Clear(); }

        private static readonly string[] OwnHugin = Lang.Keys("Муніне, за корму! Покажемо рибам, як літають.", "Вітер попутний. Гріх не політати.", "Тримайся мого крила, Муніне. Якщо зможеш.");
        private static readonly string[] OwnMunin = Lang.Keys("Хугіне, наввипередки до хвилі! Хто останній — той курка!", "Досить сидіти! За корму, поки не заснув!", "Ану, розімнемо крила, поки цей стерновий не втопив нас.");
        private static readonly string[] ForeignCall = Lang.Keys("Гей, друже! За корму — політаймо!", "Ходімо за корму, там вітер добрий.", "Ану, за мною! Покажемо їм, як літають ворони.");
        private static readonly string[] ForeignAnswer = Lang.Keys("Лечу! Тільки не відставай.", "А давай! Хто перший до хвилі?", "О, нарешті хтось кличе політати.");
    }

    // the flight itself: three figures behind (and round) the ship, the two birds each in their own part of it
    internal sealed class ShipPlan : FlightPlan
    {
        internal override string Name { get { return Orders.Plans[Orders.Ship]; } }
        private static readonly int[][] Perms = MakePerms();
        private static int[][] MakePerms()
        {
            // every ordered choice of 3 of the 7 figures (210), the seed picks one
            List<int[]> l = new List<int[]>();
            for (int a = 0; a < 7; a++) for (int b = 0; b < 7; b++) for (int c = 0; c < 7; c++) if (a != b && b != c && a != c) l.Add(new[] { a, b, c });
            return l.ToArray();
        }

        internal override Vector3 TakeoffAim(ShoulderPin pin, Quaternion facing) { Vector3 a; float sp; return Aim(pin, out a, out sp) ? a : base.TakeoffAim(pin, facing); }

        internal override void Step(RavenMotor m, ShoulderPin pin, Quaternion facing, Vector3 carry, float dt)
        {
            Vector3 aim; float shipSpeed;
            if (!Aim(pin, out aim, out shipSpeed)) { aim = pin.Player.transform.position + Vector3.up * 5f; shipSpeed = carry.magnitude; }
            float behind = Vector3.Distance(pin.transform.position, aim);
            float speed = shipSpeed + Mathf.Clamp(behind * 1.3f, 2f, 10f);
            m.Steer(aim, speed, false, 0f, dt, carry, 1f);
            m.Finish(dt, 0f);
        }

        // where this bird is in its figure now (false: no ship, or no record yet)
        private static bool Aim(ShoulderPin pin, out Vector3 aim, out float shipSpeed)
        {
            aim = Vector3.zero; shipSpeed = 0f;
            Player p = pin.Player;
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            Ship ship = ShipPlay.ShipOf(p);
            if (z == null || ship == null) return false;
            string s = pin.Munin ? "M" : "H";
            long play = z.GetLong(ShipPlay.PlayKey + s, -1L);
            if (play < 0L) return false;
            float start; int seed; bool mateMunin;
            ShipPlay.Unpack(play, out start, out seed, out mateMunin);
            long mate = z.GetLong(ShipPlay.MateKey + s, 0L);
            // the two parts: your own two — Hugin and Munin; two players' — by their ids
            int role = mate == 0L ? (pin.Munin ? 1 : 0) : (p.GetPlayerID() < mate || (p.GetPlayerID() == mate && !pin.Munin && mateMunin)) ? 0 : 1;
            float t = Mathf.Max(0f, ShipPlay.Since(start));
            int k = Mathf.Clamp((int)(t / ShipPlay.FigureSeconds), 0, ShipPlay.Figures - 1);
            int fig = Perms[((seed % Perms.Length) + Perms.Length) % Perms.Length][k];
            float u = t - k * ShipPlay.FigureSeconds;
            Rigidbody body = ship.GetComponent<Rigidbody>();
            shipSpeed = body != null ? new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude : 0f;
            aim = Way(pin.transform.position, Figure(fig, role, u, ship, p), role, ship, p);
            return true;
        }

        // The way into the figure's point never through the ship (the shoulder, the last figure: anywhere): when the
        // straight way from the bird to it would cross the ship's box (the hull, the mast, the sail and its yard), out
        // past the side first at the height it flies, then along that side to the height and the end the point is at,
        // and only then in. Every figure's own points stay out of that box (checked for a raft up to the Ashlands' ship).
        private static Vector3 Way(Vector3 bird, Vector3 target, int role, Ship ship, Player p)
        {
            Vector3 size = ShipPlay.SizeOf(ship);
            Vector3 S = ship.transform.position;
            Vector3 F = ship.transform.forward; F.y = 0f; F = F.sqrMagnitude > 0.001f ? F.normalized : Vector3.forward;
            Vector3 R = Vector3.Cross(Vector3.up, F);
            float deck = Mathf.Max(p.transform.position.y, Water + 1f);
            Vector3 b = Local(bird, S, R, F), t = Local(target, S, R, F);
            if (!Crosses(b, t, size, deck, S.y)) return target;
            float side = Mathf.Abs(b.x) > 0.5f ? Mathf.Sign(b.x) : (role == 0 ? 1f : -1f);
            float lane = side * (size.x + 3f);
            if (Mathf.Abs(b.x) < size.x + 2.5f) // (over the ship or right by it: straight out first, at its own height)
                return S + F * Mathf.Clamp(b.z, -(size.z + 4f), size.z + 4f) + R * lane + Vector3.up * (Mathf.Max(bird.y, deck + 1f) - S.y);
            // out past the side: along it to the point's end and height, then in
            float z = Mathf.Abs(t.z) > size.z + 1.5f ? Mathf.Sign(t.z) * (size.z + 3f) : t.z;
            return S + F * z + R * lane + Vector3.up * (Mathf.Max(target.y, Water + 1f) - S.y);
        }

        // in the ship's frame: x to its right, y the world's height, z ahead
        private static Vector3 Local(Vector3 w, Vector3 S, Vector3 R, Vector3 F) { Vector3 d = w - S; return new Vector3(Vector3.Dot(d, R), w.y, Vector3.Dot(d, F)); }

        // the ship as two boxes (its frame): the hull with its rails, posts and people — the whole length, up to a little
        // over the deck; the mast with the sail and its yard — the middle, the whole width, up past the top
        internal static bool InShip(Vector3 q, Vector3 size, float deck, float shipY)
        {
            if (Mathf.Abs(q.x) >= size.x + 1f || q.y <= Water - 1f) return false;
            if (Mathf.Abs(q.z) < size.z + 1.5f && q.y < deck + 2.5f) return true;
            return Mathf.Abs(q.z) < 2.5f && q.y < shipY + size.y + 1.5f;
        }

        // does the straight way a → b (both in the ship's frame) pass through it
        private static bool Crosses(Vector3 a, Vector3 b, Vector3 size, float deck, float shipY)
        {
            for (int k = 1; k <= 12; k++) if (InShip(Vector3.Lerp(a, b, k / 12f), size, deck, shipY)) return true;
            return false;
        }

        internal static float Pace = 6.5f; // m/s at most a figure's point moves over the ship (the bird has the ship's own speed on top)

        internal static float Water { get { return ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f; } }

        // the figures (ship's frame: F ahead, R to the right, the deck where its owner stands)
        private static Vector3 Figure(int fig, int role, float u, Ship ship, Player p)
        {
            Vector3 size = ShipPlay.SizeOf(ship);
            float half = size.z, beam = size.x, top = size.y;
            Vector3 S = ship.transform.position;
            Vector3 F = ship.transform.forward; F.y = 0f; F = F.sqrMagnitude > 0.001f ? F.normalized : Vector3.forward;
            Vector3 R = Vector3.Cross(Vector3.up, F);
            float deck = Mathf.Max(p.transform.position.y, Water + 1f), water = Water, back = half + 4f;
            float s = role == 0 ? 1f : -1f, ph = role * 1.3f;
            Vector3 at;
            switch (fig)
            {
                case 0: // «Кільватер»: wing by wing low over the wake, almost on the water
                    at = S - F * (back + 1.5f * Mathf.Sin(0.5f * u + ph)) + R * (s * 0.8f);
                    at.y = water + 1.1f + 0.25f * Mathf.Sin(2f * u + ph);
                    break;
                case 1: // «Коса»: they cross behind the stern, one over the other at every crossing
                    at = S - F * (back + 2f) + R * (s * 2.4f * Mathf.Sin(1.6f * u));
                    at.y = deck + 2.6f + s * 0.75f * Mathf.Cos(1.6f * u);
                    break;
                case 2: // «Подвійна спіраль»: round each other, falling back and catching up
                {
                    Vector3 c = S - F * (back + 3f + 2.5f * Mathf.Sin(0.35f * u)) + Vector3.up * (deck - S.y + 3.5f);
                    at = c + (R * Mathf.Cos(2f * u) + Vector3.up * Mathf.Sin(2f * u)) * (1.8f * s);
                    break;
                }
                case 3: // «Дзеркальні хвилі»: one up while the other is down
                    at = S - F * (back + 1f) + R * (s * 1.6f);
                    at.y = deck + 3f + s * 2f * Mathf.Sin(1.3f * u);
                    break;
                case 4: // «За рибою»: one dives to the water again and again, the other circles over it
                    if (role == 0)
                    {
                        float q = Mathf.Repeat(u / 7f, 1f);
                        at = S - F * (back + 3f + 3f * Mathf.Sin(q * Mathf.PI * 2f));
                        at.y = water + 0.7f + 6f * (0.5f + 0.5f * Mathf.Cos(q * Mathf.PI * 2f));
                    }
                    else
                    {
                        Vector3 c = S - F * (back + 3f);
                        at = c + (R * Mathf.Cos(0.9f * u) + F * Mathf.Sin(0.9f * u)) * 4f;
                        at.y = deck + 5f;
                    }
                    break;
                case 5: // «Наввипередки»: stern to bow and back along the side, out past the hull and the sail's yard
                {
                    float run = Mathf.Max(5f, (2f * half + 9f) / Pace); // (one way in this many s: no faster than a raven keeps up, whatever the ship)
                    float q = Mathf.PingPong(u / run + role * 0.08f * Mathf.Sin(u * 0.7f), 1f);
                    at = S + F * Mathf.Lerp(-back, half + 5f, q) + R * (s * (beam + 2.5f));
                    at.y = deck + 1.5f + 0.3f * Mathf.Sin(3f * u + ph);
                    break;
                }
                default: // «Через вітрило»: up past the sail, over the mast, down behind the stern and along the hull
                {
                    float h = top * 0.6f + 3f;
                    float a = Pace / Mathf.Max(half + 3f, h) * u + role * Mathf.PI; // (round at a raven's pace whatever the ship; they come in out of it: one before the bow, one behind the stern)
                    Vector3 mid = new Vector3(S.x, deck + top * 0.55f, S.z);
                    at = mid + F * (Mathf.Cos(a) * (half + 3f)) + Vector3.up * (Mathf.Sin(a) * h) + R * (s * (1.5f + (beam + 2f) * (0.5f - 0.5f * Mathf.Sin(a)))); // (over the mast at the top, out past the side at the bottom, smoothly all round)
                    at.y = Mathf.Max(at.y, water + 0.9f);
                    break;
                }
            }
            return at;
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Вечірнє віче» (the user, 2026-10-22): at night, by a fire, when three or more ravens of two or more players are
    // there, they now and then fly off the shoulders and sit down side by side — on a roof or a beam near the fire if
    // there is one, else in a row on the ground by it, facing the flames — and talk (the fireside talk of the players'
    // ravens, in the bridge); after a minute or two each flies back to its own shoulder.
    //   - One game decides (the lowest player id with a talking raven at that fire, as the fireside talks do): it finds
    //     the row once (a few rays, only then), and tells everybody where each raven sits (Rpc).
    //   - Each owner's game sends its own ravens there (an unseen spot of «Сісти тут», free) and calls them back; every
    //     game sees the flights and the sitting as any raven on a spot.
    //   - Cheap: the look is once every LookEvery seconds and only when you sit by a fire at night with others' ravens
    //     near (no physics until it really gathers).
    internal static class Gathering
    {
        internal static bool Enabled = true;
        internal static int MinRavens = 3, MaxSeats = 8;
        internal static float Range = 10f, LookEvery = 5f, Chance = 0.5f, AgainMinutes = 20f, MissMinutes = 2f;
        internal static float StayMin = 70f, StayMax = 110f, TalkAfter = 8f, SecondTalk = 0.5f, Apart = 0.32f;
        internal static float RoofMin = 1.2f, RoofMax = 6f, RingMin = 2.5f, RingMax = 7f, GroundFrom = 2.2f;
        private const string Rpc = "RavenWatch_Gather";

        private static float _lookAt, _next, _talkAt = -1f, _talk2At = -1f, _until = -1f, _activeUntil = -1f, _acceptedAt = -999f;
        private static Vector3 _fire;
        private static readonly List<CrossTalk.Who> _whos = new List<CrossTalk.Who>(); // the decider's: who talks there
        private static readonly bool[] _sent = new bool[2]; // my ravens sent there this time
        private static int _waits; // the first talk put off while they still fly in

        // a gathering seen lately (the bridge asks it before it looks for birds on the spots)
        internal static bool Active { get { return Time.time < _activeUntil; } }

        private static int _mask;
        private static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece"); return _mask; } }

        // ---------------------------------------------------------------- from the sync (twice a second)
        internal static void Tick(Player me)
        {
            if (me == null) return;
            Stay(me);
            Talk(me);
            if (!Enabled || Time.time < _lookAt) return;
            _lookAt = Time.time + LookEvery;
            if (Time.time < _next || Active || !Cozy.Cosy || EnvMan.instance == null || !EnvMan.IsNight()) return;
            if (me.IsDead() || (Senses.Ready && (Senses.Fight || Senses.Underground))) return;
            Look(me);
        }

        // the deciding game: enough ravens here, the dice, the row, everybody told
        private static void Look(Player me)
        {
            List<CrossTalk.Who> whos = new List<CrossTalk.Who>();
            int players = 0;
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o.IsDead() || (o != me && Vector3.Distance(o.transform.position, me.transform.position) > Range)) continue;
                int had = whos.Count;
                foreach (bool m in new[] { false, true }) if (CrossTalk.CanTalk(o, m)) whos.Add(new CrossTalk.Who { P = o, Munin = m, Level = Mathf.Max(1, Shoulder.LevelOn(o, m)) });
                if (whos.Count == had) continue;
                players++;
                if (o != me && !CrossTalk.Decides(me, o)) return; // the lowest id there decides
            }
            if (players < 2 || whos.Count < MinRavens) return;
            _next = Time.time + MissMinutes * 60f;
            if (Random.value > Chance || !Director.May(Director.Voice.Talk)) return;
            EffectArea heat = EffectArea.IsPointInsideArea(me.transform.position, EffectArea.Type.Heat, 1f);
            if (heat == null) return;
            Vector3 fire = heat.transform.position;
            // alternate the players along the row (not all of one side together), at most MaxSeats
            whos.Sort((a, b) => a.P.GetPlayerID() == b.P.GetPlayerID() ? a.Munin.CompareTo(b.Munin) : a.P.GetPlayerID().CompareTo(b.P.GetPlayerID()));
            List<CrossTalk.Who> row = Interleave(whos);
            if (row.Count > MaxSeats) row.RemoveRange(MaxSeats, row.Count - MaxSeats);
            Vector3[] at; Vector3 face;
            if (!Roof(fire, row.Count, out at, out face) && !Ground(fire, me, row.Count, out at, out face)) return;
            _next = Time.time + AgainMinutes * 60f;
            float stay = Random.Range(StayMin, StayMax);
            ZPackage pkg = new ZPackage();
            pkg.Write(fire);
            pkg.Write(stay);
            pkg.Write(row.Count);
            for (int i = 0; i < row.Count; i++) { pkg.Write(row[i].P.GetPlayerID()); pkg.Write(row[i].Munin); pkg.Write(at[i]); pkg.Write(face); }
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg); // (everybody: this game too)
            _whos.Clear();
            for (int i = 0; i < row.Count && _whos.Count < CrossTalk.MaxRavens; i++) _whos.Add(row[i]);
            _talkAt = Time.time + TalkAfter; _waits = 0;
            _talk2At = Random.value < SecondTalk ? Time.time + stay * 0.55f : -1f;
        }

        private static List<CrossTalk.Who> Interleave(List<CrossTalk.Who> whos)
        {
            List<List<CrossTalk.Who>> by = new List<List<CrossTalk.Who>>();
            foreach (CrossTalk.Who w in whos)
            {
                List<CrossTalk.Who> l = by.Find(x => x[0].P == w.P);
                if (l == null) { l = new List<CrossTalk.Who>(); by.Add(l); }
                l.Add(w);
            }
            List<CrossTalk.Who> row = new List<CrossTalk.Who>();
            for (int k = 0; row.Count < whos.Count; k++) foreach (List<CrossTalk.Who> l in by) if (k < l.Count) row.Add(l[k]);
            return row;
        }

        // a roof's ridge, a beam, a wall top near the fire, long enough for the row: rays down round the fire (a ring of
        // 12, two widths), then along the found top for each seat — all of them on something built, at about one height
        private static bool Roof(Vector3 fire, int n, out Vector3[] at, out Vector3 face)
        {
            at = new Vector3[n]; face = Vector3.forward;
            RaycastHit h;
            for (float r = RingMin; r <= RingMax; r += (RingMax - RingMin))
                for (int k = 0; k < 12; k++)
                {
                    Vector3 d = Quaternion.AngleAxis(k * 30f, Vector3.up) * Vector3.forward, c = fire + d * r;
                    if (!Physics.Raycast(c + Vector3.up * (RoofMax + 3f), Vector3.down, out h, RoofMax + 4f, Mask, QueryTriggerInteraction.Ignore)) continue;
                    float up = h.point.y - fire.y;
                    if (up < RoofMin || up > RoofMax || h.normal.y < 0.5f || h.collider.GetComponentInParent<Piece>() == null) continue;
                    Vector3 side = Vector3.Cross(Vector3.up, d); // the row runs round the fire
                    bool ok = true;
                    for (int i = 0; i < n && ok; i++)
                    {
                        Vector3 p = h.point + side * ((i - (n - 1) * 0.5f) * Apart);
                        RaycastHit s;
                        ok = Physics.Raycast(new Vector3(p.x, h.point.y + 1f, p.z), Vector3.down, out s, 1.6f, Mask, QueryTriggerInteraction.Ignore)
                            && Mathf.Abs(s.point.y - h.point.y) < 0.3f && s.normal.y > 0.45f && s.collider.GetComponentInParent<Piece>() != null && SitHere.Free(s.point);
                        if (ok) at[i] = s.point;
                    }
                    if (!ok) continue;
                    face = -d;
                    return true;
                }
            return false;
        }

        // else on the ground by the fire, on your side of it, in a row round it, facing the flames
        private static bool Ground(Vector3 fire, Player me, int n, out Vector3[] at, out Vector3 face)
        {
            at = new Vector3[n];
            Vector3 d = me.transform.position - fire; d.y = 0f;
            d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
            face = -d;
            Vector3 side = Vector3.Cross(Vector3.up, d);
            RaycastHit h;
            for (int i = 0; i < n; i++)
            {
                // a slight arc: the ends come round the fire a little
                float off = (i - (n - 1) * 0.5f) * Apart;
                Vector3 p = fire + d * (GroundFrom + off * off * 0.15f) + side * off;
                if (!Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out h, 4f, Mask, QueryTriggerInteraction.Ignore) || h.normal.y < 0.6f || !SitHere.Free(h.point)) return false;
                at[i] = h.point;
            }
            return true;
        }

        // ---------------------------------------------------------------- every game: told where they sit

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new System.Action<long, ZPackage>(OnGather));
        }

        private static void OnGather(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            if (me == null) return;
            Vector3 fire = pkg.ReadVector3();
            float stay = Mathf.Clamp(pkg.ReadSingle(), 20f, 180f);
            int n = Mathf.Min(pkg.ReadInt(), MaxSeats);
            if (Vector3.Distance(fire, me.transform.position) > 60f) return; // (far: not ours to see or do)
            // the one who called it is by that fire (a game far off, or one sending false fires, cannot pull my ravens away)
            Player from = null;
            foreach (Player o in Player.GetAllPlayers()) if (o != null && (o == me ? sender == ZDOMan.GetSessionID() : JointPlay.PeerOf(o) == sender)) { from = o; break; }
            if (from == null || Vector3.Distance(from.transform.position, fire) > Range + 5f) return;
            _activeUntil = Time.time + stay + 20f;
            // mine fly only to a real fire I am at, at night, and to one gathering at a time (two deciders at the edge of
            // each other's reach: the first one heard)
            bool go = Enabled && Vector3.Distance(me.transform.position, fire) <= Range + 5f && EnvMan.instance != null && EnvMan.IsNight()
                && Time.time - _acceptedAt >= AgainMinutes * 30f && EffectArea.IsPointInsideArea(fire, EffectArea.Type.Heat, 1f) != null;
            long id = me.GetPlayerID();
            for (int i = 0; i < n; i++)
            {
                long pid = pkg.ReadLong(); bool munin = pkg.ReadBool(); Vector3 at = pkg.ReadVector3(), face = pkg.ReadVector3();
                if (!go || pid != id || Vector3.Distance(at, fire) > RingMax + 3f) continue;
                Go(me, munin, at, face);
            }
            if (_sent[0] || _sent[1]) _acceptedAt = Time.time;
            if (_sent[0] || _sent[1]) { _fire = fire; _until = Time.time + stay; }
        }

        // my raven to its place in the row (free: no strength, no food — their own doing), if it is free for it
        private static void Go(Player me, bool munin, Vector3 at, Vector3 face)
        {
            ItemDrop.ItemData item;
            if (Perches.WhyNot(me, munin, out item) != null || Orders.Of(me, munin) != Orders.None || Outing.Of(me, munin) != Outing.None) return;
            face.y = 0f;
            Quaternion rot = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : Vector3.forward, Vector3.up);
            if (SitHere.Place(me, munin, at, rot, null, false) == null) _sent[munin ? 1 : 0] = true;
        }

        // my ravens there: home when the time is up, or sooner (a fight, you went off, the night is over)
        private static void Stay(Player me)
        {
            if (_until < 0f) return;
            bool over = Time.time >= _until || me.IsDead() || (Senses.Ready && Senses.Fight) || Vector3.Distance(me.transform.position, _fire) > 25f
                || (EnvMan.instance != null && !EnvMan.IsNight());
            if (!over) return;
            _until = -1f;
            for (int i = 0; i < 2; i++)
            {
                if (!_sent[i]) continue;
                _sent[i] = false;
                ItemDrop.ItemData item = RavenSeat.On(me, i == 1);
                if (item != null && Perches.OnSpot(item)) Perches.CallQuiet(me, item, i == 1); // (called back by you already: nothing)
            }
        }

        // the deciding game: the talk once they sit (and maybe a second one later)
        private static void Talk(Player me)
        {
            float due = _talkAt >= 0f ? _talkAt : _talk2At;
            if (due < 0f || Time.time < due) return;
            bool first = _talkAt >= 0f;
            // only the ones really sitting there (one busy elsewhere stayed home; one still flying in is waited for a little)
            List<CrossTalk.Who> there = _whos.FindAll(w => w.P != null && !w.P.IsDead() && Bird(w.P, w.Munin) != null);
            if ((there.Count < 2 || RavenSkills.Hushed(me)) && first && _waits++ < 3) { _talkAt = Time.time + 5f; return; }
            if (first) _talkAt = -1f; else _talk2At = -1f;
            if (there.Count >= 2 && !RavenSkills.Hushed(me) && CrossTalk.Gather(there) && first) Bonds.AtFire(me, there); // (a foe by the fire: a jab a little later)
        }

        // a raven sitting in the row (for the bridge: its words over the bird, not over its player); null when none
        internal static GameObject Bird(Player p, bool munin)
        {
            if (!Active || p == null) return null;
            long id = p.GetPlayerID();
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.IsSpot) continue;
                Perches.Occupant o = v.At(0);
                if (o != null && o.Pid == id && o.Munin == munin) return v.Bird(munin);
            }
            return null;
        }

        // the console: raven gather — a look now, whatever the dice (still needs the fire, the night, the ravens)
        internal static string Cmd(Player p)
        {
            if (p == null) return "No player.";
            if (!Cozy.Cosy) return "Gather: sit by a fire first.";
            if (EnvMan.instance == null || !EnvMan.IsNight()) return "Gather: only at night.";
            float c = Chance; Chance = 1f; _next = 0f; _activeUntil = -1f;
            try { Look(p); } finally { Chance = c; }
            return Active ? "Gather: they fly." : "Gather: not enough ravens (3+ of 2+ players, settled on the shoulders) or no place for the row.";
        }

        internal static void Reset()
        {
            _next = 0f; _talkAt = _talk2At = _until = _activeUntil = -1f;
            _sent[0] = _sent[1] = false;
            _whos.Clear();
        }
    }
}
#endif

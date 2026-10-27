#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // follow <slot>: the raven leads you along the route of that slot («route» below). It begins with the step's deed
    // (E on the rune stake at the route's start, the J circle, the book): the raven who brought the errand (the other one
    // when that one is not on you) flies to the next point and sits there on an unseen spot (Perches.Lead: free, quiet);
    // you come near — it flies on; you lag — it caws for you; a fight near you — it waits. You go farther than the spots
    // let it sit (SitHere.Far) — it comes back to you (Perches.Tick), and the step waits: «Веди далі» on the route goes on
    // from the point nearest you. On foot or mounted. (mark follow<i>: "<point>:h|m", the point it flies to or sits on)
    internal sealed class FollowStep : ErrandStep
    {
        private const float StartRing = 12f, Reach = 12f, Lag = 30f, CallGap = 10f, Seen = 80f, Fight = 8f;
        private const float Back = 25f; // m from the route that «Веди далі» still takes (its next point within SitHere.Far of you)
        internal static float Step = 30f; // m between the route's points
        private string _slot = "", _ptsFor;
        private List<Vector3> _pts = new List<Vector3>();
        private float _callAt;
        private bool _backTold, _stuckTold;

        private bool _auto; // follow <slot> auto: it begins by itself once you stand at its start (a trail goes on from a sign)
        protected override void Read() { _slot = Arg(1, ""); _auto = Arg(2, "") == "auto"; }
        internal override int Need { get { return 1; } }

        // the walk itself (a timer leaves room for it: ErrandMods), from the points as last read (Where reads them first)
        internal override float Wait
        {
            get
            {
                float m = 0f;
                for (int k = 1; k < _pts.Count; k++) m += Flat(_pts[k - 1], _pts[k]);
                return m / ErrandMods.TrialPace;
            }
        }

        private static string Key(int i) { return "follow" + i; }

        // the route's points, start to end (the slot's value: its end first, for the slots «near» it; then the route)
        private List<Vector3> Points(ErrandRun r)
        {
            string v;
            if (!r.Slot.TryGetValue(_slot, out v)) v = "";
            if (v == _ptsFor) return _pts;
            _ptsFor = v; _pts = new List<Vector3>();
            string[] parts = v.Split('|');
            for (int k = 1; k < parts.Length; k++) { Vector3 at; if (ErrandSteps.Point(parts[k], out at)) _pts.Add(at); }
            return _pts;
        }

        private static bool State(ErrandRun r, int i, out int at, out bool munin)
        {
            string m = r.Mark(Key(i));
            int colon = m.IndexOf(':');
            at = colon > 0 ? Errands.I(m.Substring(0, colon), 0) : 0;
            munin = colon > 0 && m.Substring(colon + 1) == "m";
            return colon > 0;
        }

        // the guide: the errand's own raven, or the other one when that one is not free on you (not worn, or sitting
        // somewhere) and the other is
        private static bool Guide(Player p, ErrandRun r)
        {
            if (!Free(p, r.Munin) && Free(p, !r.Munin)) return !r.Munin;
            return r.Munin;
        }
        private static bool Free(Player p, bool munin) { ItemDrop.ItemData it = RavenSeat.On(p, munin); return it != null && !Perches.On(it); }

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        // where it sits: on what is there now (a rock, a dug pit), not on the world as made
        private static Vector3 Ground(Vector3 v) { if (ZoneSystem.instance != null) v.y = ZoneSystem.instance.GetSolidHeight(v); return v; }

        private int Nearest(Player p, List<Vector3> pts)
        {
            int best = 0; float bd = float.MaxValue;
            for (int k = 0; k < pts.Count; k++) { float d = Flat(p.transform.position, pts[k]); if (d < bd) { bd = d; best = k; } }
            return best;
        }

        // sits on the route's point it was sent to (out: where)
        private static bool Sits(ItemDrop.ItemData item, Vector3 point)
        {
            Vector3 at;
            return item != null && Perches.On(item) && Perches.SpotPos(item, out at) && Flat(at, point) < 4f;
        }

        private static Quaternion Facing(List<Vector3> pts, int k)
        {
            Vector3 d = (k + 1 < pts.Count ? pts[k + 1] : pts[k]) - (k > 0 ? pts[k - 1] : pts[k]);
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
        }

        // led right now: sent on and sitting there, or on its way
        private bool Leading(ErrandRun r, int i, Player p)
        {
            int at; bool munin;
            List<Vector3> pts = Points(r);
            if (!State(r, i, out at, out munin) || at >= pts.Count) return false;
            return Perches.Busy(munin) || Sits(RavenSeat.On(p, munin), pts[at]);
        }

        internal override bool Where(ErrandRun r, int i, out Vector3 at, out float radius)
        {
            List<Vector3> pts = Points(r);
            at = Vector3.zero; radius = Reach;
            if (pts.Count == 0) return false;
            int k; bool m;
            if (!State(r, i, out k, out m)) { at = pts[0]; radius = StartRing; return true; }
            at = pts[Mathf.Clamp(k, 0, pts.Count - 1)];
            return true;
        }

        internal override string ActLabel(ErrandRun r, int i, Player p, out string why)
        {
            why = null;
            List<Vector3> pts = Points(r);
            int at; bool m;
            bool begun = State(r, i, out at, out m);
            if (r.Count[i] >= 1 || p == null || pts.Count < 2 || begun && Leading(r, i, p)) return "";
            bool guide = Guide(p, r);
            ItemDrop.ItemData item = RavenSeat.On(p, guide);
            if (item == null) why = Lang.T("з вороном на плечі");
            else if (Perches.On(item) || Perches.Busy(guide)) why = Lang.T("коли ворон на плечі");
            else if (!begun && Flat(p.transform.position, pts[0]) > StartRing) why = Lang.T("на місці старту");
            else if (begun && Flat(p.transform.position, pts[Nearest(p, pts)]) > Back) why = Lang.T("на маршруті");
            return begun ? Lang.T("Веди далі") : Lang.T("Веди мене");
        }

        internal override string Act(ErrandRun r, int i, Player p)
        {
            List<Vector3> pts = Points(r);
            int at; bool m;
            bool begun = State(r, i, out at, out m);
            bool guide = Guide(p, r);
            int near = Nearest(p, pts);
            int next = !begun ? 1 : Flat(p.transform.position, pts[near]) <= Reach ? Mathf.Min(pts.Count - 1, near + 1) : near; // (off the route a little: back onto it first)
            string why = Perches.Lead(p, guide, Ground(pts[next]), Facing(pts, next));
            if (why != null) return why;
            r.SetMark(Key(i), next.ToString(CultureInfo.InvariantCulture) + ":" + (guide ? "m" : "h"));
            _backTold = false;
            ScreamFx.CawAll(p, guide, 1f, 1f);
            RavenMsg.Center(p, Lang.F("{0} злетів уперед. Іди за ним.", Plugin.RavenName(guide)));
            return null;
        }

        internal override bool Tick(ErrandRun r, int i, Player p)
        {
            List<Vector3> pts = Points(r);
            if (p == null || r.Count[i] >= 1) return false;
            if (pts.Count < 2) { Errands.Log("follow: no route in slot «" + _slot + "» — the step passes"); r.Count[i] = 1; return true; } // (a broken template: never stuck)
            int at; bool munin;
            if (!State(r, i, out at, out munin))
            {
                if (Flat(p.transform.position, pts[0]) < Seen) { RuneRing.Want(pts[0], StartRing); RuneRing.WantStake(pts[0]); }
                string no;
                if (_auto && Time.time > _callAt && ActLabel(r, i, p, out no).Length > 0 && no == null) { _callAt = Time.time + 3f; Act(r, i, p); Errands.Keep(p, r); }
                return false;
            }
            at = Mathf.Clamp(at, 0, pts.Count - 1);
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            bool sits = Sits(item, pts[at]);
            if (!sits && !Perches.Busy(munin))
            {
                if (!_backTold) { _backTold = true; RavenMsg.Center(p, Lang.F("{0} повернувся до тебе. Повернись на маршрут і скажи «Веди далі» — у колі J чи в книзі.", Plugin.RavenName(munin))); }
                return false;
            }
            _backTold = false;
            if (!sits) return false; // (on its way there)
            float d = Flat(p.transform.position, pts[at]);
            bool fight = Time.time - Senses.HitFoeAt < Fight || Time.time - Senses.HurtByFoeAt < Fight; // (it waits the fight out)
            if (d <= Reach && !fight)
            {
                if (at >= pts.Count - 1) { r.Count[i] = 1; Perches.CallSilent(p, item, munin); return true; }
                at++;
                string why = Perches.Lead(p, munin, Ground(pts[at]), Facing(pts, at));
                if (why != null) // (sulking, gone to sleep…: said once; it tries again while you stand by)
                {
                    if (!_stuckTold) { _stuckTold = true; RavenMsg.Center(p, why); Errands.Log("follow: the guide did not fly on — " + why); }
                    return false;
                }
                _stuckTold = false;
                r.SetMark(Key(i), at.ToString(CultureInfo.InvariantCulture) + ":" + (munin ? "m" : "h"));
                Errands.Keep(p, r);
                return false;
            }
            if (d > Lag && Time.time > _callAt) // you lag: it calls you from where it sits
            {
                _callAt = Time.time + CallGap;
                Transform t = Perches.SpotOf(item);
                if (t != null) ScreamFx.CawAt(t, t.position + Vector3.up * 0.3f, 1f, 1f, false, Voice.CodeOn(p, munin));
            }
            return false;
        }

        internal override string Describe(ErrandRun r, int i, Player p)
        {
            if (r.Count[i] >= 1) return Lang.T("Дійш{ов|ла} туди, куди вів ворон");
            List<Vector3> pts = Points(r);
            if (pts.Count < 2 || p == null) return Lang.T("Іди за вороном");
            int at; bool munin;
            if (!State(r, i, out at, out munin))
                return Lang.F("Дійди до рунного кілка на місці старту й скажи ворону «Веди мене» — ще {0} м", Mathf.RoundToInt(Mathf.Max(0f, Flat(p.transform.position, pts[0]) - StartRing)));
            string line = Lang.F("Іди за вороном: {0} сидить попереду — точка {1} з {2}", Plugin.RavenName(munin), Mathf.Clamp(at, 1, pts.Count - 1), pts.Count - 1);
            return Leading(r, i, p) ? line : line + Lang.T(" (він повернувся до тебе: «Веди далі» на маршруті)");
        }

        // ---------------------------------------------------------------- the route (a slot, filled when given)
        // route <slot>: from a start 100–300 m from you to that slot's place; route <min> <max>: from such a start, that
        // many m on. Points every Step m on dry land, never too steep between two (WorldGenerator: the world as made, not as
        // dug); turning aside where the way is blocked. Kept as "end|start|…|end" (the end first: Point and «near» read it).
        internal static string Route(Player p, string[] slot, Dictionary<string, string> filled, float within, out string why)
        {
            why = null;
            if (p == null || WorldGenerator.instance == null) { why = Lang.T("Світ ще не готовий."); return null; }
            Vector3 target = Vector3.zero;
            bool toPlace = false;
            float length = 0f;
            string tv;
            if (slot.Length > 2 && filled != null && filled.TryGetValue(slot[2], out tv) && ErrandSteps.ParsePoint(tv, out target)) toPlace = true;
            else length = Random.Range(slot.Length > 2 ? Errands.F(slot[2], 300f) : 300f, slot.Length > 3 ? Errands.F(slot[3], 500f) : 500f);
            // from:<slot>: the leg begins where another route ends, and goes on the way that one went
            string fromV = null;
            float heading = float.NaN;
            foreach (string w in slot) if (w.StartsWith("from:") && filled != null) filled.TryGetValue(w.Substring(5), out fromV);
            if (fromV != null)
            {
                string[] parts = fromV.Split('|');
                Vector3 a, b;
                if (parts.Length > 2 && ErrandSteps.ParsePoint(parts[parts.Length - 2], out a) && ErrandSteps.ParsePoint(parts[parts.Length - 1], out b) && Flat(a, b) > 1f) heading = Angle(b - a);
            }
            for (int tries = 0; tries < 6; tries++)
            {
                string startV = fromV ?? ErrandSteps.Around(p, 100f, 300f, within, out why);
                Vector3 start;
                if (startV == null || !ErrandSteps.ParsePoint(startV, out start)) continue;
                string lands;
                if (!p.m_customData.TryGetValue(Diary.LandsKey, out lands)) lands = "|";
                List<Vector3> pts = Walk(start, toPlace, target, length, lands, float.IsNaN(heading) ? heading : heading + Random.Range(-40f, 40f));
                if (pts == null) continue;
                StringBuilder sb = new StringBuilder(ErrandSteps.Pt(pts[pts.Count - 1].x)).Append('/').Append(ErrandSteps.Pt(pts[pts.Count - 1].z));
                foreach (Vector3 v in pts) sb.Append('|').Append(ErrandSteps.Pt(v.x)).Append('/').Append(ErrandSteps.Pt(v.z));
                why = null;
                return sb.ToString();
            }
            why = why ?? Lang.T("Ворон не знайшов дороги, якою тебе повести.");
            return null;
        }

        private const int MaxPoints = 60;
        private static readonly float[] Turns = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f };

        private static List<Vector3> Walk(Vector3 start, bool toPlace, Vector3 target, float length, string lands, float heading0)
        {
            List<Vector3> pts = new List<Vector3> { start };
            Vector3 pos = start;
            float heading = toPlace ? Angle(target - start) : float.IsNaN(heading0) ? Random.Range(0f, 360f) : heading0, gone = 0f;
            while (pts.Count < MaxPoints)
            {
                if (toPlace && Flat(pos, target) <= Step * 1.5f) { pts.Add(target); return pts; }
                if (!toPlace && gone >= length) return pts;
                float want = toPlace ? Angle(target - pos) : heading + Random.Range(-15f, 15f);
                bool moved = false;
                foreach (float turn in Turns)
                {
                    float a = (want + turn) * Mathf.Deg2Rad;
                    Vector3 next = pos + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Step;
                    if (!Walkable(pos, next, lands)) continue;
                    if (pts.Count > 1 && Flat(next, pts[pts.Count - 2]) < Step * 0.9f) continue; // (no going back on itself)
                    pos = next; heading = want + turn; gone += Step; pts.Add(next); moved = true;
                    break;
                }
                if (!moved) return null;
            }
            return toPlace ? null : pts; // (to a place: too far to lead on foot)
        }

        private static float Angle(Vector3 d) { return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        // dry land all the way (the middle too), no cliff between, not out of the world, not under the sea, in a land you
        // have walked (the Meadows always)
        private static bool Walkable(Vector3 from, Vector3 to, string lands)
        {
            if (to.x * to.x + to.z * to.z > 9800f * 9800f) return false;
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            WorldGenerator w = WorldGenerator.instance;
            Vector3 mid = (from + to) * 0.5f;
            float h0 = w.GetHeight(from.x, from.z), h1 = w.GetHeight(to.x, to.z), hm = w.GetHeight(mid.x, mid.z);
            if (h1 < sea + 1f || hm < sea + 0.5f) return false;
            if (Mathf.Abs(h1 - h0) > 9f || Mathf.Abs(hm - h0) > 6f || Mathf.Abs(h1 - hm) > 6f) return false;
            Heightmap.Biome b = w.GetBiome(to.x, to.z);
            return b != Heightmap.Biome.Ocean && (b == Heightmap.Biome.Meadows || lands.Contains("|" + b + "|"));
        }
    }
}
#endif

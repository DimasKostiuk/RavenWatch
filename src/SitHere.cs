#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Сісти тут»: the raven sits on top of whatever building you look at (a sign, a fence, a roof, a portal, a wall...)
    // until you call it back. Not a new way of sitting: an unseen perch of one seat is put there (Perches.BuildSpot:
    // RavenWatch_Spot, no look, no colliders, not saved with the world, gone with its maker), and the perches do the
    // rest — the flight there and back, the sitting, its life there, what the others see, «Повернути». It gathers no
    // strength there (its day norm 0); the order costs as the others do (Orders.Cost, and food for the flight).
    // It is the context order «Туди»: the orders circle's piece over «Жердина» (and its own keys) does what fits where you
    // look (Look), in this order: another player or a tamed animal → onto them (SitOn); a small thing on the ground → it
    // is fetched (RavenFetch); a perch → onto that perch; a building → here (Perches.OrderHere). «Жердина» under it
    // stays the nearest perch. A spot nobody sits on or flies to is taken away (Tick); you going farther than Far calls the raven after
    // you (Perches.Tick).
    internal static class SitHere
    {
        internal const string Prefab = "RavenWatch_Spot", DefId = "spot";
        private static readonly string NothingSrc = Lang.K("Наведи приціл на гравця, ручну тварину, дрібницю на землі, будову чи жердину.");
        internal static string Nothing { get { return Lang.T(NothingSrc); } }
        internal static float Range = 40f;    // m from you: how far it may be sent
        internal static float Far = 60f;      // m: you went this far from it → it follows you
        internal static float Hunger = 1.25f;  // satiety for the flight (as a figure)
        internal static float Room = 0.45f;   // m of free air it needs over the spot
        internal static float Apart = 0.35f;  // m beside the other raven, if it already sits there

        internal sealed class Target
        {
            internal Player Player;     // looking at another player: onto their head or a free shoulder (SitOn)
            internal Character Animal;  // looking at a creature: onto a tamed one's back or head (SitOn)
            internal ItemDrop Item;     // looking at a thing on the ground: «Принести» (RavenFetch)
            internal PerchView Perch;   // looking at a perch: that one
            internal bool Building;     // looking at a building (Why: why not there)
            internal string Why, Name, Follow; // Follow: a ship or a cart it rides on (SitOn.OnThing)
            internal Vector3 At, Fwd;
            internal float Dist;
        }

        private static int _mask;
        private static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small", "vehicle"); return _mask; } }
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Target None = new Target();

        // where you look now, for this raven (asked by the orders menu several times a second: kept a moment)
        private static readonly Target[] _last = new Target[2];
        private static readonly float[] _lastAt = { -9f, -9f };
        internal static Target Look(Player p, bool munin)
        {
            int k = munin ? 1 : 0, o = 1 - k;
            if (_last[k] != null && Time.unscaledTime - _lastAt[k] < 0.25f) return _last[k];
            // the other raven looked a moment ago at someone or something (not a building's top): the same for this one
            if (_last[o] != null && Time.unscaledTime - _lastAt[o] < 0.25f && !_last[o].Building) { _last[k] = _last[o]; _lastAt[k] = _lastAt[o]; return _last[k]; }
            Target t = Find(p, munin);
            _last[k] = t; _lastAt[k] = Time.unscaledTime;
            return t;
        }

        private static Target Find(Player p, bool munin)
        {
            if (p == null || GameCamera.instance == null) return None;
            Transform cam = GameCamera.instance.transform;
            int n = Physics.RaycastNonAlloc(cam.position, cam.forward, Hits, Range + 15f, Mask, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || c.transform.IsChildOf(p.transform)) continue;
                if (best < 0 || Hits[i].distance < Hits[best].distance) best = i;
            }
            // another player, before what the plain ray hit (the first in the order: a player, a thing, a perch, a building)
            Character other = SitOn.CharacterAt(cam, p, best >= 0 ? Hits[best].distance + 0.4f : Range + 15f);
            if (other != null)
            {
                Target pt = new Target { Player = other as Player, Animal = other is Player ? null : other, Name = SitOn.NameOf(other) };
                pt.Dist = Vector3.Distance(other.transform.position, p.transform.position);
                pt.Why = pt.Dist > Range ? Lang.F("Задалеко: {0} м (до {1}).", Mathf.RoundToInt(pt.Dist), Mathf.RoundToInt(Range)) : SitOn.WhyNot(other);
                return pt;
            }
            // a thing lying there, before what the plain ray hit (a fatter ray: small things are hard to hit exactly)
            ItemDrop thing = ItemAt(cam, best >= 0 ? Hits[best].distance + 0.4f : Range + 15f);
            if (thing != null)
            {
                Target it = new Target { Item = thing, Name = RavenFetch.NameOf(thing) };
                it.Dist = Vector3.Distance(thing.transform.position, p.transform.position);
                it.Why = it.Dist > RavenFetch.Range ? Lang.F("Задалеко: {0} м (до {1}).", Mathf.RoundToInt(it.Dist), Mathf.RoundToInt(RavenFetch.Range)) : RavenFetch.WhyNot(thing);
                return it;
            }
            if (best < 0) return None;
            RaycastHit hit = Hits[best];
            PerchView pv = hit.collider.GetComponentInParent<PerchView>();
            if (pv != null && pv.Valid && !pv.IsSpot) return new Target { Perch = pv };
            Piece piece = hit.collider.GetComponentInParent<Piece>();
            Transform root = piece != null ? piece.transform : hit.collider.transform;
            Target t = new Target { Building = true, Name = piece != null ? NameOf(piece) : NatureName(hit.collider) };
            if (piece == null && (hit.collider.GetComponentInParent<TreeBase>() != null || hit.collider.GetComponentInParent<TreeLog>() != null)) { t.Why = Lang.T("На дерево ворон поки не сідає."); return t; }
            // a ship or a cart: it rides on it (the spot follows: SitOn.Thing)
            Ship ship = piece != null ? piece.GetComponentInParent<Ship>() : null;
            Vagon cart = ship == null && piece != null ? piece.GetComponentInParent<Vagon>() : null;
            ZNetView mover = ship != null ? ship.GetComponent<ZNetView>() : cart != null ? cart.GetComponent<ZNetView>() : null;
            Vector3 at;
            if (!Top(hit, out at)) { t.Why = Lang.T("Там нема де сісти зверху."); return t; }
            Vector3 way = at - p.transform.position;
            t.Dist = way.magnitude;
            if (t.Dist > Range) { t.Why = Lang.F("Задалеко: {0} м (до {1}).", Mathf.RoundToInt(t.Dist), Mathf.RoundToInt(Range)); return t; }
            way.y = 0f;
            Vector3 fwd = way.sqrMagnitude > 0.01f ? way.normalized : Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            // the other raven sits there already: beside it, along the same top
            if (Taken(at))
            {
                Vector3 side = Vector3.Cross(Vector3.up, fwd) * (munin ? -1f : 1f), moved;
                if (Beside(hit.collider, at + side * Apart, out moved) || Beside(hit.collider, at - side * Apart, out moved)) at = moved;
                else { t.Why = Lang.T("Тут місце лише на одного."); return t; }
            }
            if (!Free(at)) { t.Why = Lang.T("Над цим місцем щось є — нема де сісти."); return t; }
            if (!Seen(p, at, root)) { t.Why = Lang.T("Звідси туди не видно."); return t; }
            t.At = at; t.Fwd = fwd;
            if (mover != null) t.Follow = SitOn.OnThing(mover, at);
            return t;
        }

        private static int _itemMask;
        private static ItemDrop ItemAt(Transform cam, float far)
        {
            if (_itemMask == 0) _itemMask = LayerMask.GetMask("item");
            RaycastHit h;
            if (!Physics.SphereCast(cam.position, 0.2f, cam.forward, out h, far, _itemMask, QueryTriggerInteraction.Ignore)) return null;
            return h.collider != null ? h.collider.GetComponentInParent<ItemDrop>() : null;
        }

        // the ground or a rock (whatever the crosshair is on, not built)
        private static string NatureName(Collider c)
        {
            if (c.GetComponentInParent<Heightmap>() != null) return Lang.T("земля");
            return Lang.T("камінь");
        }

        private static string NameOf(Piece piece)
        {
            string n = piece.m_name ?? "";
            if (Localization.instance != null) n = Lang.Game(n);
            return n.Length > 0 ? n : Lang.T("будова");
        }

        // the top of the part you look at, right over (or under) where the crosshair is: a sign's edge, a post's head, a
        // wall's top, the roof's slope; only what is facing up enough to stand on
        private static bool Top(RaycastHit hit, out Vector3 at)
        {
            Collider c = hit.collider;
            Vector3 inside = hit.point - hit.normal * 0.03f;   // a thin board: just into it, so the ray down finds its edge
            float top = c.bounds.max.y + 0.3f;
            RaycastHit h;
            if (c.Raycast(new Ray(new Vector3(inside.x, top, inside.z), Vector3.down), out h, top - c.bounds.min.y + 0.5f) && h.normal.y > 0.35f) { at = h.point; return true; }
            if (hit.normal.y > 0.5f) { at = hit.point; return true; }
            at = Vector3.zero;
            return false;
        }

        // the same part's top a little aside (for the second raven)
        private static bool Beside(Collider c, Vector3 near, out Vector3 at)
        {
            float top = c.bounds.max.y + 0.3f;
            RaycastHit h;
            if (c.Raycast(new Ray(new Vector3(near.x, top, near.z), Vector3.down), out h, top - c.bounds.min.y + 0.5f) && h.normal.y > 0.35f && !Taken(h.point)) { at = h.point; return true; }
            at = Vector3.zero;
            return false;
        }

        // the top of a part right over its middle (a sign's edge, a bush's crown): what faces up enough to stand on
        internal static bool TopOf(Collider c, out Vector3 at)
        {
            Bounds b = c.bounds;
            float top = b.max.y + 0.3f;
            RaycastHit h;
            if (c.Raycast(new Ray(new Vector3(b.center.x, top, b.center.z), Vector3.down), out h, top - b.min.y + 0.5f) && h.normal.y > 0.35f) { at = h.point; return true; }
            at = Vector3.zero;
            return false;
        }

        // a spot there already, someone on it or flying to it
        internal static bool Taken(Vector3 at)
        {
            foreach (PerchView v in PerchView.All)
                if (v != null && v.IsSpot && !v.Idle && (v.transform.position - at).sqrMagnitude < 0.3f * 0.3f) return true;
            return false;
        }

        // the thing lies still: a spot is measured once where it is, not while it still flies, falls or rolls
        internal static bool Resting(Component c)
        {
            Rigidbody rb = c != null ? c.GetComponentInParent<Rigidbody>() : null;
            return rb == null || rb.isKinematic || rb.IsSleeping() || rb.linearVelocity.sqrMagnitude < 0.04f;
        }

        // free air over the spot (not under the eaves, not inside a shelf)
        internal static bool Free(Vector3 at)
        {
            return !Physics.CheckCapsule(at + Vector3.up * 0.17f, at + Vector3.up * Room, 0.12f, Mask, QueryTriggerInteraction.Ignore);
        }

        // seen from your eyes: nothing but that thing itself in between (root: the building piece, the sign, the bush)
        internal static bool Seen(Player p, Vector3 at, Transform root)
        {
            Vector3 eye = p.GetEyePoint(), to = at + Vector3.up * 0.2f, dir = to - eye;
            float len = dir.magnitude;
            if (len < 0.3f) return true;
            int n = Physics.RaycastNonAlloc(eye, dir / len, Hits, len - 0.15f, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || c.transform.IsChildOf(p.transform)) continue;
                if (root != null && c.transform.IsChildOf(root)) continue;
                return false;
            }
            return true;
        }

        // what it costs: as an order (strength, and food for the flight)
        internal static float CostOf(bool munin) { return Orders.Cost * Norov.CostMul(Trust.Worn(munin)); }
        internal static string PriceText(bool munin)
        {
            float cost = CostOf(munin), food = Hunger * Satiety.HungerMul(Trust.Worn(munin)) + cost * Satiety.GiftShare;
            return Lang.F("{0} сили · ситість −{1}", Mathf.RoundToInt(Power.Price(munin, cost)), Satiety.Num(Mathf.Round(food * 10f) / 10f));
        }

        // why it would not go now (beyond Perches.WhyNot): hungry, too weak
        internal static string WhyNotNow(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            if (item == null) return Lang.F("{0} нема на плечі.", Plugin.RavenNameGen(munin));
            if (Satiety.Zone(item) <= Satiety.Hungry) return Plugin.RavenName(munin) + (Trust.Starving(item) ? Lang.T(" голодує — не полетить.") : Lang.T(" голодний — не полетить."));
            if (Power.Get(item) < Power.Price(munin, CostOf(munin))) return Lang.F("Мало сили: треба {0}.", Mathf.RoundToInt(Power.Price(munin, CostOf(munin))));
            return null;
        }

        // the order (Perches.Go, after its own checks): the spot put there, the raven sent onto it
        internal static string Go(Player p, bool munin, Target t)
        {
            string why = WhyNotNow(p, munin);
            if (why != null) return why;
            return Place(p, munin, t.At, Quaternion.LookRotation(t.Fwd, Vector3.up), t.Follow, true);
        }

        // a spot put there (follow: whom it rides on, SitOn), paid for (an order; a visit of its own is free: WorldVisit),
        // the raven sent onto it; the spot made, or null
        internal static string Place(Player p, bool munin, Vector3 at, Quaternion rot, string follow, bool paid) { PerchView v; return Place(p, munin, at, rot, follow, paid, out v); }
        internal static string Place(Player p, bool munin, Vector3 at, Quaternion rot, string follow, bool paid, out PerchView v)
        {
            v = null;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Prefab) : null;
            if (prefab == null) return Lang.T("Не вийшло: місця для ворона нема в грі.");
            if (paid && Power.Get(RavenSeat.On(p, munin)) < Power.Price(munin, CostOf(munin))) { Power.Spend(p, munin, CostOf(munin)); return null; } // (it says why itself)
            GameObject g = Object.Instantiate(prefab, at, rot);
            v = g.GetComponent<PerchView>();
            if (v == null || !v.Valid) { Object.Destroy(g); v = null; return Lang.T("Не вийшло: місце не з'явилося."); }
            if (paid) Power.Spend(p, munin, CostOf(munin)); // (paid once it is there)
            if (follow != null) v.Zdo.Set(SitOn.FollowKey, follow);
            if (paid)
            {
                Satiety.Flew(munin, Hunger); // the flying makes it hungry
                Trust.Ordered(munin);        // and it is something done together
            }
            _last[0] = _last[1] = null;  // (the other raven sees this one's spot at once)
            Perches.Sit(p, v, munin, 0);
            return null;
        }

        // From Perches.Tick: spots nobody sits on, flies to or holds a bird of, taken away after a few seconds (by
        // whoever holds them now: also one left by a player who went away)
        private static float _next;
        private static readonly List<PerchView> _gone = new List<PerchView>();
        internal static void Tick()
        {
            SitOn.Tick(); // «not on me» said (and kept)
            if (Time.time < _next || ZNetScene.instance == null) return;
            _next = Time.time + 1f;
            _gone.Clear();
            foreach (PerchView v in PerchView.All)
            {
                if (v == null || !v.IsSpot || !v.Owner) continue;
                if (!v.Idle) { v.IdleSince = -1f; continue; }
                if (v.IdleSince < 0f) { v.IdleSince = Time.time; continue; }
                if (Time.time - v.IdleSince >= 3f) _gone.Add(v);
            }
            foreach (PerchView v in _gone) ZNetScene.instance.Destroy(v.gameObject);
            _gone.Clear();
        }
    }
}
#endif

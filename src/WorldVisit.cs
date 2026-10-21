#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HuginWhispers
{
    // «Світ», the visits (the user, 2026-10-15): now and then a raven of yours, sitting idle on your shoulder, flies by
    // itself to something near and sits on it a while:
    //   - a sign: it reads it to you (once: a sign read is remembered until its words change);
    //   - a berry bush: it pecks at it on and off (a little food for it, once a day a bush; the bush keeps its berries);
    //   - a fence, a pole, a roof's ridge, a portal: it just sits there and looks about (a perch of its own choosing);
    //   - something shiny on the ground (coins, amber, gems): it stands by it and guards it — and grumbles if you take it;
    //   - the water's edge: it drinks (pecks at the water);
    //   - a beehive: it pokes its beak in, gets stung, and is back on you at once, cross;
    //   - a tamed animal (a wolf, a boar, a lox): onto its back, a peck or two at it.
    // And three of their own, not on the director's clock: your grave, when you come back to it (it sits on it while you
    // gather your things); your bed while you sleep in it; and at sea, the top of the mast — a lookout that cries «Земля!»
    // when land comes up ahead (a land you have never set foot on: by its name).
    // Its own look of the animation director (AnimDirector.Schedule calls Schedule), not the outings': about every
    // MinMinutes–MaxMinutes, × the menu's «Ravens touch the world», × its character (Norov.OutChance). Never in a fight,
    // underground, on a ship, swimming, asleep (but the bed), hungry, hurt, or just after an order; under a roof only
    // close by. It is a free «sit here» spot (SitHere.Place unpaid): the perches fly it there and back and show it to
    // everyone; the stay ends by itself (StayMin–StayMax), or at once when something happens. What it says asks the
    // dialogue director — a visit may stay silent.
    internal static class WorldVisit
    {
        internal static float Mul = 1f;                        // the menu: 0 never, 1 as below
        internal static float MinMinutes = 10f, MaxMinutes = 20f;
        internal static float Radius = 15f, IndoorRadius = 12f; // m from you it looks round (under a roof: closer)
        internal static float StayMin = 25f, StayMax = 45f;    // s it sits there
        internal static float AfterOrder = 60f;                // s after an order or an outing of yours: not yet
        internal static float BerrySatiety = 3f, PeckSeconds = 2.6f, BerrySay = 0.5f;
        internal static float PeckEveryMin = 8f, PeckEveryMax = 12f; // s between two peckings while it sits on a bush
        internal static float PerchSay = 0.3f, ShinySay = 0.7f;
        internal static float GraveNear = 20f;                 // m from your grave: it flies to it
        internal const string SignsKey = "RavenWatch_signs";   // the player's: the signs read ("x,y,z,hash;…", the last 80)
        private const int SignsKept = 80;
        internal const int Sign = 1, Berry = 2, Perch = 3, Shiny = 4, Grave = 5, Bed = 6, Mast = 7, Water = 8, Bees = 9, Beast = 10;
        internal static float WaterSay = 0.3f, BeastSay = 0.5f, BeesStay = 2.5f;
        internal static float MastMin = 60f, MastMax = 150f;          // s on the mast
        internal static float MastAgainMin = 240f, MastAgainMax = 480f; // s before it goes up again
        internal static float LandFar = 200f, LandNear = 140f, ShoreNear = 60f, MastSay = 0.3f;
        internal static float MastAfterSail = 40f; // s under sail before it first goes up

        // the things on the ground it likes to guard (their prefab names)
        private static readonly HashSet<string> ShinyNames = new HashSet<string> { "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "GemstoneRed", "GemstoneGreen", "GemstoneBlue", "Crystal", "Thunderstone" };
        // building pieces that make a perch (in its prefab name): a fence, a pole, a post, a roof's ridge, a portal
        private static readonly string[] PerchWords = { "fence", "pole", "post", "roof_top", "portal", "stake" };

        private sealed class Thing { internal int Kind; internal Vector3 At; internal Transform Root; internal string Text; internal float Weight; internal ItemDrop Item; internal string Follow; internal Ship Ship; }
        // Of: where the thing itself stands (what is remembered); Item: the shiny thing it guards
        // Ship, LandSeen, CalmFrom, NextLook: the mast (its ship; land cried already; since when the ship is still; the next look ahead)
        private sealed class Stay { internal int Kind; internal PerchView View; internal float Started, Until, NextPeck, CalmFrom = -1f, NextLook; internal bool Landed, Done, LandSeen; internal string Text; internal Vector3 At, Of; internal ItemDrop Item; internal Ship Ship; }
        private static readonly Stay[] Now = new Stay[2];

        private static float _next = -1f, _retry, _graveAt, _mastNext, _sailFrom = -1f;

        internal static bool Active(bool munin) { return Now[munin ? 1 : 0] != null; }

        // the corner line when it sets off (Perches.Confirm), or null when it is not a visit
        internal static string Note(bool munin)
        {
            Stay s = Now[munin ? 1 : 0];
            if (s == null) return null;
            string what = s.Kind == Water ? Lang.T("до води напитися") : s.Kind == Bees ? Lang.T("до вулика — ой") : s.Kind == Beast ? Lang.T("подражнити звіра") : s.Kind == Sign ? Lang.T("роздивитися табличку") : s.Kind == Berry ? Lang.T("до куща з ягодами") : s.Kind == Shiny ? Lang.T("до чогось блискучого") : s.Kind == Grave ? Lang.T("до твоєї могили") : s.Kind == Bed ? Lang.T("до тебе на ліжко") : s.Kind == Mast ? Lang.T("на щоглу — дозорцем") : Lang.T("посидіти поруч");
            return Lang.F("{0} полетів {1}. (Повернути — J)", Plugin.RavenName(munin), what);
        }

        // From AnimDirector.Schedule (the owner's game, twice a second)
        internal static void Schedule(Player p)
        {
            if (p == null || Mul <= 0f) return;
            WorldNotice.Tick(p); // what they notice from your shoulder (the game's raven, Odin)
            WorldReact.Tick(p);  // the weather on them (shaking the rain off, the cold)
            if (Time.time >= _graveAt) { _graveAt = Time.time + 3f; if (ToGrave(p) || ToBed(p) || ToMast(p)) return; } // their own moments, not on the clock
            if (_next < 0f) { NextLook(); return; }
            if (Time.time < _next || Time.time < _retry) return;
            _retry = Time.time + 30f;
            if (!CanGo(p)) return; // not now: again in half a minute
            bool munin;
            if (!Pick(p, out munin)) return;
            Thing t = Find(p, munin);
            if (t == null) { _next = Time.time + 120f; return; } // nothing near worth it: look again in a while
            if (Go(p, munin, t)) NextLook();
        }

        // which raven goes: the more eager by its character, of those ready
        private static bool Pick(Player p, out bool munin)
        {
            bool h = Ready(p, false), m = Ready(p, true);
            munin = m;
            if (!h && !m) return false;
            float wh = h ? Norov.OutChance(RavenSeat.On(p, false)) : 0f, wm = m ? Norov.OutChance(RavenSeat.On(p, true)) : 0f;
            munin = h && m ? Random.value * (wh + wm) < wm : m;
            return true;
        }

        // the console (raven world [munin]): a visit now, without waiting for the director (what it finds near, and why not)
        internal static string Force(Player p, bool munin)
        {
            if (p == null) return Lang.T("Нема гравця.");
            if (!CanGo(p)) return Lang.T("Зараз не можна (бій, під землею, корабель, сон, мало здоров'я або щойно був наказ).");
            if (!Ready(p, munin)) return Lang.F("{0} не готовий (не на плечі, спить, голодний, зайнятий).", Plugin.RavenName(munin));
            Thing t = Find(p, munin);
            if (t == null) return Lang.T("Поруч нема нічого, що його цікавить і що видно (табличка, кущ з ягодами, паркан чи стовп, блискуче, вода, вулик, ручна тварина).");
            return Go(p, munin, t) ? Lang.F("{0} летить: {1}.", Plugin.RavenName(munin), KindName(t)) : Lang.T("Не вийшло сісти туди.");
        }

        private static string KindName(Thing t)
        {
            switch (t.Kind)
            {
                case Sign: return Lang.F("табличка «{0}»", t.Text);
                case Berry: return Lang.T("кущ з ягодами");
                case Shiny: return Lang.T("щось блискуче");
                case Grave: return Lang.T("твоя могила");
                case Bed: return Lang.T("ліжко");
                case Mast: return Lang.T("щогла");
                case Water: return Lang.T("вода");
                case Bees: return Lang.T("вулик");
                case Beast: return Lang.T("звір");
                default: return Lang.T("посидіти");
            }
        }

        private static void NextLook()
        {
            float wanted = Random.Range(MinMinutes, MaxMinutes) * 60f;
            _next = Time.time + wanted / Mathf.Max(0.05f, Mul);
        }

        private static bool CanGo(Player p)
        {
            return !p.IsDead() && !Senses.Underground && !Senses.Fight && !Senses.OnShip && !Senses.Swimming && !Senses.Sleeping
                && Senses.Health01 >= 0.5f && !Shoulder.Hovering(p) && Time.time - Outing.LastAt > AfterOrder;
        }

        // sitting quietly on you, awake, not hungry, not already away
        private static bool Ready(Player p, bool munin)
        {
            if (Now[munin ? 1 : 0] != null || !Outing.ReadyRaven(p, munin)) return false;
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            ItemDrop.ItemData it;
            return item != null && Satiety.Zone(item) > Satiety.Hungry && Perches.WhyNot(p, munin, out it) == null;
        }

        // ---------------------------------------------------------------- your grave, your bed: their own moments

        private static readonly HashSet<Vector3Int> GravesSat = new HashSet<Vector3Int>();
        private static readonly Collider[] Near = new Collider[32];

        // back at your own grave (within GraveNear): one raven flies to it and sits on it, once a grave
        private static bool ToGrave(Player p)
        {
            if (Now[0] != null || Now[1] != null || p.IsDead() || Senses.Fight || Senses.Underground || Game.instance == null) return false;
            PlayerProfile prof = Game.instance.GetPlayerProfile();
            if (prof == null || !prof.HaveDeathPoint()) return false;
            Vector3 dp = prof.GetDeathPoint();
            if ((dp - p.transform.position).sqrMagnitude > GraveNear * GraveNear || GravesSat.Contains(Key(dp))) return false;
            int n = Physics.OverlapSphereNonAlloc(dp, 4f, Near, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                TombStone grave = Near[i] != null ? Near[i].GetComponentInParent<TombStone>() : null;
                ZNetView gnv = grave != null ? grave.GetComponent<ZNetView>() : null;
                if (gnv == null || !gnv.IsValid() || gnv.GetZDO().GetLong("owner", 0L) != p.GetPlayerID()) continue; // (the game keeps whose grave it is there)
                if (GraveRavens.HasBird(grave)) return false; // the other raven lies in it and keeps it: the stone is its
                Vector3 at;
                if (!SitHere.Resting(grave) || !SitHere.TopOf(Near[i], out at) || !SitHere.Free(at)) continue; // (a new grave still flying up or falling: not yet)
                bool munin;
                if (!Pick(p, out munin)) return false;
                GravesSat.Add(Key(dp)); // (once a grave, whether it went or not)
                return Go(p, munin, new Thing { Kind = Grave, At = at, Root = grave.transform });
            }
            return false;
        }

        // you lie down in a bed: one raven sits on it by you while you sleep, and is back on you when you get up
        private static bool ToBed(Player p)
        {
            if (Now[0] != null || Now[1] != null || !p.InBed() || Senses.Fight) return false;
            int n = Physics.OverlapSphereNonAlloc(p.transform.position, 2.5f, Near, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                global::Bed bed = Near[i] != null ? Near[i].GetComponentInParent<global::Bed>() : null;
                if (bed == null) continue;
                Vector3 at;
                if (!Highest(Near[i], out at) || !SitHere.Free(at)) continue;
                bool h = OnYouStill(p, false), m = OnYouStill(p, true); // (at night it may be sleepy: it comes all the same)
                if (!h && !m) return false;
                bool munin = h && m ? Random.value < 0.5f : m;
                return Go(p, munin, new Thing { Kind = Bed, At = at, Root = bed.transform });
            }
            return false;
        }

        // sitting on you, nothing else to do (the bed: sleepy or not)
        private static bool OnYouStill(Player p, bool munin)
        {
            GameObject model = Shoulder.ModelOf(p, munin);
            ItemDrop.ItemData it;
            return model != null && Outing.Of(p, munin) == Outing.None && AnimDirector.Of(model) == RavenAct.Sit && Perches.WhyNot(p, munin, out it) == null;
        }

        // at sea under sail: one raven goes up to the top of the mast for a while (now and then, not all the time)
        private static bool ToMast(Player p)
        {
            _sailFrom = Senses.Sailing ? (_sailFrom < 0f ? Time.time : _sailFrom) : -1f;
            if (Now[0] != null || Now[1] != null || !Senses.Sailing || Senses.Fight || Time.time < _mastNext || Time.time - _sailFrom < MastAfterSail) return false;
            Ship ship = p.GetStandingOnShip() ?? p.GetControlledShip();
            ZNetView snv = ship != null ? ship.GetComponent<ZNetView>() : null;
            if (ship == null || ship.m_mastObject == null || snv == null || !snv.IsValid()) return false;
            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in ship.m_mastObject.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any) return false;
            bool h = OnYouStill(p, false) && Fed(p, false), m = OnYouStill(p, true) && Fed(p, true);
            if (!h && !m) return false;
            bool munin = h && m ? Random.value < 0.5f : m;
            _mastNext = Time.time + Random.Range(MastAgainMin, MastAgainMax) / Mathf.Max(0.05f, Mul);
            Vector3 top = new Vector3(b.center.x, b.max.y + 0.02f, b.center.z);
            return Go(p, munin, new Thing { Kind = Mast, At = top, Root = ship.transform, Ship = ship, Follow = SitOn.OnThing(snv, top) });
        }

        private static bool Fed(Player p, bool munin) { ItemDrop.ItemData item = RavenSeat.On(p, munin); return item != null && Satiety.Zone(item) > Satiety.Hungry; }

        // the lookout: land ahead of the ship (a little to either side too), not the shore it sails along. Returns the
        // land seen (its biome), or None
        private static Heightmap.Biome LandAhead(Ship ship)
        {
            if (WorldGenerator.instance == null || ZoneSystem.instance == null) return Heightmap.Biome.None;
            float sea = ZoneSystem.instance.m_waterLevel + 1.5f;
            Vector3 at = ship.transform.position, fwd = Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up).normalized;
            Vector3 near = at + fwd * ShoreNear;
            if (WorldGenerator.instance.GetHeight(near.x, near.z) > sea) return Heightmap.Biome.None; // (along the shore: nothing to cry about)
            foreach (float angle in new[] { 0f, -25f, 25f })
                foreach (float d in new[] { LandNear, LandFar })
                {
                    Vector3 pt = at + Quaternion.Euler(0f, angle, 0f) * fwd * d;
                    if (WorldGenerator.instance.GetHeight(pt.x, pt.z) > sea) return WorldGenerator.instance.GetBiome(pt);
                }
            return Heightmap.Biome.None;
        }

        // a land this character has never stood in (the diary's list: Diary.LandsKey)
        private static bool NewLand(Player p, Heightmap.Biome b)
        {
            string raw;
            return b != Heightmap.Biome.Ocean && p.m_customData != null && p.m_customData.TryGetValue(Diary.LandsKey, out raw) && raw != null && !raw.Contains("|" + b + "|");
        }

        // the highest point of a part's top (a bed's headboard, not its blanket): a few rays down across it
        private static bool Highest(Collider c, out Vector3 at)
        {
            Bounds b = c.bounds;
            float top = b.max.y + 0.3f;
            bool any = false;
            at = Vector3.zero;
            for (int ix = 0; ix < 3; ix++)
                for (int iz = 0; iz < 3; iz++)
                {
                    Vector3 from = new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.2f + 0.3f * ix), top, Mathf.Lerp(b.min.z, b.max.z, 0.2f + 0.3f * iz));
                    RaycastHit h;
                    if (!c.Raycast(new Ray(from, Vector3.down), out h, top - b.min.y + 0.5f) || h.normal.y < 0.35f) continue;
                    if (!any || h.point.y > at.y) { at = h.point; any = true; }
                }
            return any;
        }

        // ---------------------------------------------------------------- what is near

        private static int _mask, _itemMask;
        private static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "piece", "piece_nonsolid", "Default_small"); return _mask; } }
        private static int ItemMask { get { if (_itemMask == 0) _itemMask = LayerMask.GetMask("item"); return _itemMask; } }
        private static readonly Collider[] Cols = new Collider[128];
        private static readonly HashSet<Component> _seen = new HashSet<Component>();
        private static readonly List<Thing> _things = new List<Thing>();
        private static readonly HashSet<ZDOID> ShinySeen = new HashSet<ZDOID>(); // guarded once (this session)

        // what is near and worth a visit, picked by weight (Hugin likes the signs, Munin the berries and the shiny);
        // only when it is looked for (here), never every frame
        private static Thing Find(Player p, bool munin)
        {
            Vector3 me = p.transform.position;
            float r = Senses.Indoors ? IndoorRadius : Radius;
            int n = Physics.OverlapSphereNonAlloc(me, r, Cols, Mask, QueryTriggerInteraction.Ignore);
            _seen.Clear(); _things.Clear();
            ReadSigns(p);
            for (int i = 0; i < n; i++)
            {
                Collider c = Cols[i];
                if (c == null) continue;
                Sign sign = c.GetComponentInParent<Sign>();
                if (sign != null)
                {
                    if (!_seen.Add(sign)) continue;
                    string text = Clean(sign.GetText());
                    if (text.Length == 0 || _read.Contains(Spot(sign.transform.position, text))) continue;
                    Add(p, c, sign.transform, Sign, text, munin ? 2f : 4f);
                    continue;
                }
                Pickable bush = c.GetComponentInParent<Pickable>();
                if (bush != null)
                {
                    if (_seen.Add(bush) && IsBerryBush(bush) && !PeckedToday(bush.transform.position)) Add(p, c, bush.transform, Berry, null, munin ? 3f : 2f);
                    continue;
                }
                Piece piece = c.GetComponentInParent<Piece>();
                if (piece == null || !_seen.Add(piece) || piece.GetComponentInParent<Ship>() != null) continue;
                if (Utils.GetPrefabName(piece.gameObject).ToLowerInvariant().Contains("beehive")) Add(p, c, piece.transform, Bees, null, 0.6f);
                else if (IsPerch(piece)) Add(p, c, piece.transform, Perch, null, 1f);
            }
            // the shiny things on the ground (their own layer)
            int ni = Physics.OverlapSphereNonAlloc(me, r, Cols, ItemMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < ni; i++)
            {
                ItemDrop d = Cols[i] != null ? Cols[i].GetComponentInParent<ItemDrop>() : null;
                if (d == null || !_seen.Add(d) || !ShinyNames.Contains(Utils.GetPrefabName(d.gameObject))) continue;
                ZNetView nv = d.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || ShinySeen.Contains(nv.GetZDO().m_uid)) continue;
                Vector3 at;
                if (!SitHere.Resting(d) || !Ground(d.transform.position, p, out at) || SitHere.Taken(at) || !SitHere.Free(at) || !SitHere.Seen(p, at, d.transform)) continue;
                _things.Add(new Thing { Kind = Shiny, At = at, Root = d.transform, Weight = munin ? 4f : 2f, Item = d });
            }
            // a tamed animal near (not ridden, its back free)
            foreach (Character beast in Character.GetAllCharacters())
            {
                if (beast == null || beast.IsPlayer() || !beast.IsTamed() || (beast.transform.position - me).sqrMagnitude > r * r) continue;
                Vector3 at; Quaternion rot; string follow;
                if (!SitOn.BackOf(beast, out at, out rot, out follow) || !SitHere.Seen(p, at, beast.transform)) continue;
                _things.Add(new Thing { Kind = Beast, At = at, Root = beast.transform, Text = SitOn.NameOf(beast), Follow = follow, Weight = munin ? 2f : 1f });
            }
            // the water's edge (out in the open)
            Vector3 edge;
            if (!Senses.Indoors && Shore(p, out edge)) _things.Add(new Thing { Kind = Water, At = edge, Root = p.transform, Weight = 1f });
            float sum = 0f;
            foreach (Thing t in _things) sum += t.Weight;
            float pick = Random.value * sum;
            foreach (Thing t in _things) { pick -= t.Weight; if (pick <= 0f) return t; }
            return null;
        }

        private static void Add(Player p, Collider c, Transform root, int kind, string text, float weight)
        {
            Vector3 at;
            if (!SitHere.TopOf(c, out at) || SitHere.Taken(at) || !SitHere.Free(at) || !SitHere.Seen(p, at, root)) return;
            _things.Add(new Thing { Kind = kind, At = at, Root = root, Text = text, Weight = weight });
        }

        private static bool IsPerch(Piece piece)
        {
            string name = Utils.GetPrefabName(piece.gameObject).ToLowerInvariant();
            if (name.Contains("sharp")) return false; // (not on the spikes)
            foreach (string w in PerchWords) if (name.Contains(w)) return true;
            return false;
        }

        // the water's edge near you: a ring round you where the ground just meets the water, the nearest one seen
        private static bool Shore(Player p, out Vector3 at)
        {
            at = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            float sea = ZoneSystem.instance.m_waterLevel;
            Vector3 me = p.transform.position;
            foreach (float d in new[] { 5f, 9f, 13f })
                for (int a = 0; a < 8; a++)
                {
                    Vector3 pt = me + Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward * d;
                    float g = ZoneSystem.instance.GetGroundHeight(pt);
                    if (g < sea - 0.15f || g > sea + 0.3f) continue;
                    pt.y = Mathf.Max(g, sea);
                    if (SitHere.Taken(pt) || !SitHere.Free(pt) || !SitHere.Seen(p, pt, null)) continue;
                    at = pt;
                    return true;
                }
            return false;
        }

        // a place on the ground beside a shiny thing (it stands by it, not on it)
        private static int _groundMask;
        private static bool Ground(Vector3 thing, Player p, out Vector3 at)
        {
            if (_groundMask == 0) _groundMask = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
            Vector3 side = Vector3.ProjectOnPlane(thing - p.transform.position, Vector3.up);
            side = side.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, side.normalized) : Vector3.right;
            RaycastHit h;
            if (Physics.Raycast(thing + side * 0.3f + Vector3.up * 1f, Vector3.down, out h, 2.5f, _groundMask, QueryTriggerInteraction.Ignore) && h.normal.y > 0.5f) { at = h.point; return true; }
            at = Vector3.zero;
            return false;
        }

        // a berry bush with berries on it now (raspberries, blueberries, cloudberries: the game's own and the like)
        private static bool IsBerryBush(Pickable b)
        {
            string name = Utils.GetPrefabName(b.gameObject);
            if (name.IndexOf("berry", System.StringComparison.OrdinalIgnoreCase) < 0 || name.IndexOf("bush", System.StringComparison.OrdinalIgnoreCase) < 0) return false;
            ZNetView nv = b.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() && !nv.GetZDO().GetBool(ZDOVars.s_picked, false);
        }

        // the sign's words as read out: no tags, one line, not too long
        private static readonly Regex Tags = new Regex("<[^>]*>");
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = Tags.Replace(text, "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length > 48 ? s.Substring(0, 46).TrimEnd() + "…" : s;
        }

        // ---------------------------------------------------------------- off it goes, it sits, it comes back

        private static bool Go(Player p, bool munin, Thing t)
        {
            Vector3 way = t.At - p.transform.position; way.y = 0f;
            Vector3 fwd = way.sqrMagnitude > 0.01f ? way.normalized : Vector3.forward;
            int k = munin ? 1 : 0;
            // the bed: while you sleep; the grave: while you gather your things; the beehive: cut short once it lands
            float stay = t.Kind == Bed ? 9999f : t.Kind == Grave ? 120f : t.Kind == Mast ? Random.Range(MastMin, MastMax) : t.Kind == Bees ? 60f : Random.Range(StayMin, StayMax);
            Stay s = new Stay { Kind = t.Kind, Text = t.Text, At = t.At, Of = t.Root.position, Item = t.Item, Ship = t.Ship, Started = Time.time, Until = Time.time + stay };
            Now[k] = s; // (before the spot: Perches.Confirm asks Note for its corner line)
            PerchView v;
            string why = SitHere.Place(p, munin, t.At, Quaternion.LookRotation(fwd, Vector3.up), t.Follow, false, out v);
            if (why != null || v == null) { Now[k] = null; return false; }
            s.View = v;
            if (t.Kind == Shiny && t.Item != null) { ZNetView nv = t.Item.GetComponent<ZNetView>(); if (nv != null && nv.IsValid()) ShinySeen.Add(nv.GetZDO().m_uid); }
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven world: " + Plugin.RavenName(munin) + " visits: " + KindName(t) + ", " + Mathf.RoundToInt(way.magnitude) + " m off");
            return true;
        }

        // From Perches.Tick (the owner's game, twice a second): the visits go on — landed: it does its thing; its time
        // up, or something happened: back to you, quietly
        internal static void Tick(Player p)
        {
            for (int k = 0; k < 2; k++)
            {
                Stay s = Now[k];
                if (s == null) continue;
                bool munin = k == 1;
                ItemDrop.ItemData item = p != null ? RavenSeat.On(p, munin) : null;
                // called back by you, sent elsewhere, its spot gone: the visit is over
                if (p == null || item == null || s.View == null || (!Perches.OnSpot(item) && Time.time - s.Started > 6f)) { Now[k] = null; continue; }
                if (!Perches.OnSpot(item)) continue; // (still being seated)
                if (s.Kind == Mast) s.CalmFrom = Senses.Sailing ? -1f : s.CalmFrom < 0f ? Time.time : s.CalmFrom;
                bool end = Time.time >= s.Until || p.IsDead() || Senses.Fight || Senses.Underground || Senses.Swimming
                    || (s.Kind == Bed ? !p.InBed() : Senses.Sleeping)
                    || (s.Kind == Mast && (!Senses.OnShip || s.Ship == null || (s.CalmFrom >= 0f && Time.time - s.CalmFrom > 20f)));
                // the shiny thing gone (taken by you or anyone near: a grumble; out of this game's sight: just back)
                if (!end && s.Kind == Shiny && s.Landed && s.Item == null)
                {
                    if ((p.transform.position - s.Of).sqrMagnitude < Radius * Radius && Director.May(Director.Voice.Event)) Speak(p, munin, s, "world_shiny_taken", Director.Voice.Event, null);
                    end = true;
                }
                if (!end && !s.Landed && s.View.Settled(0)) { s.Landed = true; Arrived(p, munin, s, item); }
                else if (!end && s.Landed && !s.Done) Act(p, munin, s, item); // (a word not let through yet: tried again)
                if (!end && s.Landed && (s.Kind == Berry || s.Kind == Water || s.Kind == Beast) && Time.time >= s.NextPeck) PeckAgain(s);
                if (!end && s.Landed && s.Kind == Mast && Time.time >= s.NextLook) Lookout(p, munin, s);
                if (!end) continue;
                Now[k] = null;
                Perches.CallQuiet(p, item, munin);
            }
        }

        private static void Arrived(Player p, bool munin, Stay s, ItemDrop.ItemData item)
        {
            if (s.Kind == Water || s.Kind == Beast) PeckAgain(s);
            if (s.Kind == Bees) { PeckAgain(s); s.Until = Time.time + BeesStay; } // a poke, and the bees are out
            if (s.Kind == Berry)
            {
                PeckedNow(s.Of);
                PeckAgain(s);
                if (Satiety.Value(item) <= Satiety.StuffedAt) Satiety.Change(item, BerrySatiety); // (the food once a visit)
            }
            Act(p, munin, s, item);
        }

        // the lookout on the mast, every 2 s: land ahead → «Земля!» (by its name if you never set foot there), once until
        // the sea is clear ahead again
        private static void Lookout(Player p, bool munin, Stay s)
        {
            s.NextLook = Time.time + 2f;
            Heightmap.Biome land = LandAhead(s.Ship);
            if (land == Heightmap.Biome.None) { s.LandSeen = false; return; }
            if (s.LandSeen || !Director.May(Director.Voice.Event)) return;
            s.LandSeen = true;
            if (NewLand(p, land)) Speak(p, munin, s, "world_newland", Director.Voice.Event, null, "land", Composer.BiomeName(land, false));
            else Speak(p, munin, s, "world_land", Director.Voice.Event, null);
        }

        // a pecking at the bush: one more on the spot's count, every game sees it (SpotFollow)
        private static void PeckAgain(Stay s)
        {
            s.NextPeck = Time.time + PeckSeconds + Random.Range(PeckEveryMin, PeckEveryMax);
            ZDO z = s.View != null ? s.View.Zdo : null;
            if (z != null && s.View.Owner) z.Set(SpotFollow.PeckKey, z.GetInt(SpotFollow.PeckKey, 0) + 1);
        }

        // its words there: a sign read out (remembered only once it was said); the rest now and then, or not at all
        private static void Act(Player p, bool munin, Stay s, ItemDrop.ItemData item)
        {
            switch (s.Kind)
            {
                case Sign:
                    if (!Director.May(Director.Voice.Event)) return; // (not now: it may still read it while it sits)
                    if (Speak(p, munin, s, "world_sign", Director.Voice.Event, s.Text)) { s.Done = true; Remember(p, s.Of, s.Text); }
                    return;
                case Grave:
                    if (!Director.May(Director.Voice.Event)) return;
                    s.Done = Speak(p, munin, s, "world_grave", Director.Voice.Event, null);
                    return;
                case Bed:
                    s.Done = true; // (you sleep: it keeps quiet)
                    return;
                case Mast:
                    s.Done = true;
                    if (Random.value < MastSay && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_mast", Director.Voice.Shoulder, null);
                    return;
                case Bees:
                    s.Done = true;
                    if (Director.May(Director.Voice.Event)) Speak(p, munin, s, "world_bees", Director.Voice.Event, null);
                    return;
                case Beast:
                    s.Done = true;
                    if (Random.value < BeastSay && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_beast", Director.Voice.Shoulder, null, "beast", s.Text);
                    return;
                default:
                    s.Done = true;
                    float chance = s.Kind == Berry ? BerrySay : s.Kind == Shiny ? ShinySay : s.Kind == Water ? WaterSay : PerchSay;
                    if (Random.value >= chance || !Director.May(Director.Voice.Shoulder)) return;
                    Speak(p, munin, s, s.Kind == Berry ? "world_berry" : s.Kind == Shiny ? "world_shiny" : s.Kind == Water ? "world_water" : "world_perch", Director.Voice.Shoulder, null);
                    return;
            }
        }

        private static bool Speak(Player p, bool munin, Stay s, string tag, Director.Voice voice, string sign, string key = null, string value = null)
        {
            string line = sign != null ? Satiety.Say(p, munin, tag, "sign", sign) : key != null ? Satiety.Say(p, munin, tag, key, value) : Satiety.Say(p, munin, tag);
            if (string.IsNullOrEmpty(line)) return false;
            s.View.SayHere(0, line);
            Director.Spoke(voice, RavenSpeech.ReadSeconds(line));
            return true;
        }

        // ---------------------------------------------------------------- what it remembers

        private static string Spot(Vector3 at, string text) { return Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.y) + "," + Mathf.RoundToInt(at.z) + "," + text.GetStableHashCode(); }

        // a sign is read once: the same words at the same place (about a metre) are not news; new words are. The list is
        // read into a set once a look (Find), not once a sign
        private static readonly HashSet<string> _read = new HashSet<string>();
        private static void ReadSigns(Player p)
        {
            _read.Clear();
            string raw;
            if (p.m_customData == null || !p.m_customData.TryGetValue(SignsKey, out raw) || raw.Length == 0) return;
            foreach (string e in raw.Split(';')) if (e.Length > 0) _read.Add(e);
        }

        private static void Remember(Player p, Vector3 at, string text)
        {
            if (p.m_customData == null) return;
            string raw;
            p.m_customData.TryGetValue(SignsKey, out raw);
            List<string> list = new List<string>(string.IsNullOrEmpty(raw) ? new string[0] : raw.Split(';'));
            list.Add(Spot(at, text));
            while (list.Count > SignsKept) list.RemoveAt(0);
            p.m_customData[SignsKey] = string.Join(";", list.ToArray());
        }

        // a bush pecked at today (this session; a bush's spot rounded to a metre)
        private static readonly Dictionary<Vector3Int, int> Pecked = new Dictionary<Vector3Int, int>();
        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }
        private static Vector3Int Key(Vector3 at) { return new Vector3Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y), Mathf.RoundToInt(at.z)); }
        private static bool PeckedToday(Vector3 at) { int d; return Pecked.TryGetValue(Key(at), out d) && d == Today; }
        private static void PeckedNow(Vector3 at) { if (Pecked.Count > 200) Pecked.Clear(); Pecked[Key(at)] = Today; }
    }
}
#endif

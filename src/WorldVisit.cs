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
    //   - a tamed animal (a wolf, a boar, a lox): onto its back, a peck or two at it;
    // and (the user, 2026-10-22):
    //   - a trader (Haldor, Hildir, the bog witch): onto its head — and the trader grumbles (its own words, over it);
    //   - a fire burning: it sits by it warming itself, its feathers puffed up (PerchLife: by the heat where it sits);
    //   - a rune stone (Hugin): he «reads» it out — a bit of what it says — and has his say on the old tale;
    //   - the game's gulls and crows: after one of them for a few seconds (Outing.Hunt), both ravens now;
    //   - another player's ship or cart: it sits on it (and rides along, if it goes);
    //   - bones (Munin): he digs about in them and now and then pulls something out — in his beak home to you
    //     (RavenFetch.Hold): bone fragments mostly, a few coins or a bit of amber rarely; once a day a pile.
    // And four of their own, not on the director's clock: your grave, when you come back to it (it sits on it while you
    // gather your things); your bed while you sleep in it; at sea, the top of the mast — a lookout that cries «Земля!»
    // when land comes up ahead (a land you have never set foot on: by its name); and a fish (the user, 2026-10-27): by
    // deep water or on a ship, now and then it flies out over the water, hangs there and dives for a fish of that land
    // (flown as an outing: FishPlan) — a hungry Munin eats it, Hugin (and a fed Munin) brings it to you (RavenFetch.Hold).
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
        internal const int Sign = 1, Berry = 2, Perch = 3, Shiny = 4, Grave = 5, Bed = 6, Mast = 7, Water = 8, Bees = 9, Beast = 10,
            TraderHead = 11, Fire = 12, Rune = 13, Birds = 14, Ride = 15, Bones = 16, Bathe = 18; // (the fish is no visit: an outing, Outing.Fish)
        // the dive for a fish: how far out it looks for deep water, how deep, the splash, the pause after one (a hungry
        // Munin sooner), the satiety a fish gives the one that eats it
        internal static float FishNear = 14f, FishDeep = 0.8f, FishHangMin = 2.5f, FishHangMax = 4f, FishAgainMin = 600f, FishAgainMax = 1200f, FishHungryMul = 0.4f, FishSatiety = 25f, FishSay = 0.7f;
        // a fish of each land (Munin's loot has the same); the sea's own; none known: the meadows' one
        private static readonly Dictionary<Heightmap.Biome, string> FishOf = new Dictionary<Heightmap.Biome, string>
        {
            { Heightmap.Biome.Meadows, "Fish1" }, { Heightmap.Biome.BlackForest, "Fish2" }, { Heightmap.Biome.Ocean, "Fish3" }, { Heightmap.Biome.Mountain, "Fish4_cave" },
            { Heightmap.Biome.Swamp, "Fish5" }, { Heightmap.Biome.DeepNorth, "Fish6" }, { Heightmap.Biome.Plains, "Fish7" }, { Heightmap.Biome.Mistlands, "Fish9" }, { Heightmap.Biome.AshLands, "Fish11" },
        };
        internal static float FireStayMin = 50f, FireStayMax = 80f, FireSay = 0.6f, RideSay = 0.5f, RideFar = 35f;
        internal static float GrumbleFirst = 2.5f, GrumbleEveryMin = 12f, GrumbleEveryMax = 18f; internal static int Grumbles = 2;
        internal static float BonesStayMin = 18f, BonesStayMax = 26f, BonesEmptySay = 0.4f;
        // what Munin pulls out of the bones (a prefab, the most of it, the chance: the rest of the time nothing)
        private static readonly string[] BoneFinds = { "BoneFragments", "Coins", "Amber" };
        private static readonly int[] BoneMost = { 2, 12, 1 };
        private static readonly float[] BoneChance = { 0.3f, 0.1f, 0.04f };
        internal static float WaterSay = 0.3f, BeastSay = 0.5f, BeesStay = 2.5f;
        internal static float MastMin = 60f, MastMax = 150f;          // s on the mast
        internal static float MastAgainMin = 240f, MastAgainMax = 480f; // s before it goes up again
        internal static float LandFar = 200f, LandNear = 140f, ShoreNear = 60f, MastSay = 0.3f;
        internal static float MastAfterSail = 40f; // s under sail before it first goes up

        // the things on the ground it likes to guard (their prefab names)
        private static readonly HashSet<string> ShinyNames = new HashSet<string> { "Coins", "Amber", "AmberPearl", "Ruby", "SilverNecklace", "GemstoneRed", "GemstoneGreen", "GemstoneBlue", "Crystal", "Thunderstone" };
        // building pieces that make a perch (in its prefab name): a fence, a pole, a post, a roof's ridge, a portal
        private static readonly string[] PerchWords = { "fence", "pole", "post", "roof_top", "portal", "stake" };

        private sealed class Thing { internal int Kind; internal Vector3 At; internal Transform Root; internal string Text; internal float Weight; internal ItemDrop Item; internal string Follow; internal Ship Ship; internal Trader Npc; internal ZDOID Bird; }
        // Of: where the thing itself stands (what is remembered); Item: the shiny thing it guards
        // Ship, LandSeen, CalmFrom, NextLook: the mast (its ship; land cried already; since when the ship is still; the next look ahead)
        // Npc, NextGrumble, Grumbled: the trader under it (how often it has grumbled, the next time)
        private sealed class Stay { internal int Kind; internal PerchView View; internal float Started, Until, NextPeck, CalmFrom = -1f, NextLook, NextGrumble; internal bool Landed, Done, LandSeen; internal string Text; internal Vector3 At, Of; internal ItemDrop Item; internal Ship Ship; internal Trader Npc; internal int Grumbled; }
        private static readonly Stay[] Now = new Stay[2];

        private static float _next = -1f, _retry, _graveAt, _mastNext, _sailFrom = -1f;

        internal static bool Active(bool munin) { return Now[munin ? 1 : 0] != null; }

        // what it is doing out there, for its state line (null: no visit)
        internal static string Word(bool munin)
        {
            Stay s = Now[munin ? 1 : 0];
            if (s == null) return null;
            switch (s.Kind)
            {
                case Sign: return Lang.T("читає табличку");
                case Berry: return Lang.T("зриває ягоди");
                case Perch: return Lang.T("сів на жердину");
                case Shiny: return Lang.T("летить по блискітку");
                case Grave: return Lang.T("сидить на могилі");
                case Bed: return Lang.T("вмостився на ліжку");
                case Mast: return Lang.T("на щоглі");
                case Water: return Lang.T("п'є воду");
                case Bees: return Lang.T("дражнить бджіл");
                case Beast: return Lang.T("сидить на звірі");
                case TraderHead: return Lang.T("сидить на торговці");
                case Fire: return Lang.T("гріється біля вогню");
                case Rune: return Lang.T("роздивляється руни");
                case Birds: return Lang.T("ганяє птахів");
                case Ride: return Lang.T("катається");
                case Bones: return Lang.T("порпається в кістках");
                case Bathe: return Lang.T("купається");
                default: return Lang.T("роздивляється");
            }
        }

        // the corner line when it sets off (Perches.Confirm), or null when it is not a visit
        internal static string Note(bool munin)
        {
            Stay s = Now[munin ? 1 : 0];
            if (s == null) return null;
            string what = s.Kind == TraderHead ? Lang.F("сісти на голову: {0}", s.Text) : s.Kind == Fire ? Lang.T("погрітися біля вогню") : s.Kind == Rune ? Lang.T("почитати рунічний камінь") : s.Kind == Ride ? Lang.T("на чужий корабель чи віз") : s.Kind == Bones ? Lang.T("порпатися в кістках") : s.Kind == Water ? Lang.T("до води напитися") : s.Kind == Bees ? Lang.T("до вулика — ой") : s.Kind == Beast ? Lang.T("подражнити звіра") : s.Kind == Sign ? Lang.T("роздивитися табличку") : s.Kind == Berry ? Lang.T("до куща з ягодами") : s.Kind == Shiny ? Lang.T("до чогось блискучого") : s.Kind == Grave ? Lang.T("до твоєї могили") : s.Kind == Bed ? Lang.T("до тебе на ліжко") : s.Kind == Mast ? Lang.T("на щоглу — дозорцем") : s.Kind == Bathe ? (s.Text == Bath.Hand ? Lang.T("до тебе у воду") : s.Text == Bath.Tub ? Lang.T("до тебе в купіль") : Lang.T("помитися")) : Lang.T("посидіти поруч");
            return Lang.F("{0} полетів {1}. (Повернути — J)", Plugin.RavenName(munin), what);
        }

        // From AnimDirector.Schedule (the owner's game, twice a second)
        internal static void Schedule(Player p)
        {
            if (p == null || Mul <= 0f) return;
            WorldNotice.Tick(p); // what they notice from your shoulder (the game's raven, Odin)
            WorldReact.Tick(p);  // the weather on them (shaking the rain off, the cold)
            if (Time.time >= _graveAt) { _graveAt = Time.time + 3f; if (ToGrave(p) || ToBed(p) || ToMast(p) || ToFish(p) || ToBath(p)) return; } // their own moments, not on the clock
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
            float wh = h ? Norov.OutChance(RavenSeat.On(p, false)) * Norov.VisitW(RavenSeat.On(p, false)) : 0f, wm = m ? Norov.OutChance(RavenSeat.On(p, true)) * Norov.VisitW(RavenSeat.On(p, true)) : 0f; // («Цікавий»: goes more)
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
            if (t == null) return Lang.T("Поруч нема нічого, що його цікавить і що видно (табличка, кущ з ягодами, паркан чи стовп, блискуче, вода, вулик, ручна тварина, торговець, вогонь, рунічний камінь, птахи, чужий корабель чи віз, кістки).");
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
                case Bathe: return Lang.T("купання");
                case Bees: return Lang.T("вулик");
                case Beast: return Lang.T("звір");
                case TraderHead: return Lang.F("голова: {0}", t.Text);
                case Fire: return Lang.T("вогонь");
                case Rune: return Lang.F("рунічний камінь «{0}»", t.Text);
                case Birds: return Lang.T("птахи гри");
                case Ride: return Lang.T("чужий корабель чи віз");
                case Bones: return Lang.T("кістки");
                default: return Lang.T("посидіти");
            }
        }

        private static void NextLook()
        {
            float wanted = Random.Range(MinMinutes, MaxMinutes) * 60f;
            Player p = Player.m_localPlayer; // («Цікавий» on you: sooner)
            float keen = p != null ? Mathf.Max(Norov.VisitW(RavenSeat.On(p, false)), Norov.VisitW(RavenSeat.On(p, true))) : 1f;
            _next = Time.time + wanted / Mathf.Max(0.05f, Mul) / Mathf.Max(1f, keen);
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
            Vector3 top;
            if (!MastTop(ship, out top)) return false;
            bool h = OnYouStill(p, false) && Fed(p, false), m = OnYouStill(p, true) && Fed(p, true);
            if (!h && !m) return false;
            bool munin = h && m ? Random.value < 0.5f : m;
            _mastNext = Time.time + Random.Range(MastAgainMin, MastAgainMax) * Habits.SeaMul(RavenSeat.On(p, munin)) / Mathf.Max(0.05f, Mul); // («Моряк»: sooner)
            return Go(p, munin, new Thing { Kind = Mast, At = top, Root = ship.transform, Ship = ship, Follow = SitOn.OnThing(snv, top) });
        }

        // a dive for a fish: by deep water or on a ship, its own pause between; a hungry Munin goes first (and sooner). One that
        // may not bring one now (FishBrings: a fish in your bag, or its day's FishPerDay brought) goes only to eat it: a hungry Munin
        // (the user 2026-11-05: Cubariossa's raven brought fish all the time)
        private static float _fishNext = -1f;
        private static bool ToFish(Player p)
        {
            if (_fishNext < 0f) { _fishNext = Time.time + Random.Range(FishAgainMin, FishAgainMax); return false; }
            if (Time.time < _fishNext || Mul <= 0f || Now[0] != null || Now[1] != null || p.IsDead() || Senses.Fight || Senses.Underground || Senses.Indoors || Senses.Swimming || Senses.Sleeping || Shoulder.Hovering(p)) return false;
            bool h = Fisher(p, false), m = Fisher(p, true);
            if (!h && !m) return false;
            ItemDrop.ItemData mi = m ? RavenSeat.On(p, true) : null;
            bool hungry = mi != null && Satiety.Zone(mi) <= Satiety.Hungry;
            bool inBag = FishInBag(p);
            h = h && FishBrings(RavenSeat.On(p, false), inBag);
            m = m && (hungry || FishBrings(mi, inBag));
            if (!h && !m) { _fishNext = Time.time + 60f; return false; } // (nothing to go for now: a look again in a minute)
            Vector3 at;
            if (!DeepWater(p, out at)) { _fishNext = Time.time + 30f; return false; } // (no water here: a look again soon)
            float fh = h ? Norov.FishW(RavenSeat.On(p, false)) : 0f, fm = m ? Norov.FishW(mi) : 0f;
            bool munin = hungry || (h && m ? Random.value * (fh + fm) < fm : m); // («Ловчий»: goes more)
            _fishNext = Time.time + Random.Range(FishAgainMin, FishAgainMax) * (hungry ? FishHungryMul : 1f) * Habits.FishMul(RavenSeat.On(p, munin)) / Mathf.Max(0.05f, Mul) / Mathf.Max(1f, Mathf.Max(fh, fm)); // («Моряк», «Ловчий»: sooner)
            return Outing.StartFish(p, munin, at, Random.Range(FishHangMin, FishHangMax)); // (flown: FishPlan; the fish when it is up again: FishCaught)
        }

        // a bath (Bath): by the water's edge, its own pause after each (by its character and its dirt); the dirtier, the
        // more eager of the two goes. A hot tub near is better than the shore (heated: night or cold, no matter); you sitting
        // in it, a raven joins you a while after you get in, and again now and then while you stay
        private static float _bathNext = -1f, _bathLast = -999f, _bathShoreAt, _tubNext;
        private static bool _inTub;
        internal static bool NoShore { get { return Time.time < _bathShoreAt; } } // (no water's edge near when last looked: Bath's hints)
        private static bool ToBath(Player p) { return ToBath(p, -1); }
        private static bool ToBath(Player p, int force)
        {
            if (force < 0 && _bathNext < 0f) { _bathNext = Time.time + Random.Range(Bath.AgainMin, Bath.AgainMax) * 30f; return false; } // (half a pause after you come in)
            Bath.TubAt tub = Bath.TubNear(p);
            bool inTub = Bath.InTub(p, tub), warm = tub != null && tub.Warm;
            if (inTub && !_inTub) _tubNext = Mathf.Max(_tubNext, Time.time + Random.Range(8f, 20f)); // (just got in: it comes after you soon)
            _inTub = inTub;
            ItemDrop.ItemData hi = RavenSeat.On(p, false), mi = RavenSeat.On(p, true);
            bool early = Time.time - _bathLast > Bath.MinGap && (Bath.Dirty(hi) || Bath.Dirty(mi)); // (filthy: sooner than its pause)
            bool withYou = inTub && Time.time >= _tubNext;
            if ((force < 0 && ((Time.time < _bathNext && !early && !withYou) || (Time.time < _bathShoreAt && tub == null) || Mul <= 0f)) || Now[0] != null || Now[1] != null || p.IsDead() || Senses.Fight || Senses.Underground
                || (Senses.Indoors && tub == null) || (Senses.Swimming && !inTub) || Senses.Sleeping || Senses.OnShip || Shoulder.Hovering(p)) return false;
            bool h = (force < 0 || force == 0) && Bather(p, false, hi, warm), m = (force < 0 || force == 1) && Bather(p, true, mi, warm);
            if (!h && !m) return false;
            Vector3 at;
            string how = "";
            if (tub != null && Bath.TubSpot(p, tub, inTub, out at) && (inTub || SitHere.Seen(p, at, tub.Piece.transform))) how = inTub ? Bath.Tub : "";
            else if (inTub) { _tubNext = Time.time + 20f; return false; } // (no room in it now: a look again soon)
            else if (Senses.Indoors || !Shore(p, out at)) { _bathShoreAt = Time.time + 45f; return false; } // (no water's edge here: a look again in a while, dirty or not)
            float wh = h ? Bath.Eager(hi) : 0f, wm = m ? Bath.Eager(mi) : 0f;
            bool munin = h && m ? Random.value * (wh + wm) < wm : m;
            _bathNext = Time.time + Bath.Pause(munin ? mi : hi) / Mathf.Max(0.05f, Mul);
            if (inTub) _tubNext = Time.time + Random.Range(180f, 300f);
            _bathLast = Time.time;
            return Go(p, munin, new Thing { Kind = Bathe, At = at, Root = p.transform, Weight = 1f, Text = how });
        }
        private static bool Bather(Player p, bool munin, ItemDrop.ItemData item, bool warm) { return item != null && OnYouStill(p, munin) && Outing.ReadyRaven(p, munin) && Fed(p, munin) && (warm || Bath.Weather(item)); }

        // the perch's button while you stand in the water (Bath.Wading): it hops off into the water by you and you wash it;
        // null when it went, else why not
        internal static string HandWash(Player p, bool munin)
        {
            if (p == null) return Lang.T("Нема гравця.");
            string why = Bath.HandWhyNot(p, munin);
            if (why != null) return why;
            if (!Bath.Wading(p)) return Lang.T("Стань у воду — не глибше, ніж по шию.");
            Bath.TubAt tub = Bath.TubNear(p);
            bool inTub = Bath.InTub(p, tub);
            Vector3 at;
            if (!(inTub ? Bath.TubSpot(p, tub, true, out at) : WaterBeside(p, out at))) return Lang.T("Нема вільного місця у воді поруч.");
            if (!Go(p, munin, new Thing { Kind = Bathe, At = at, Root = p.transform, Weight = 1f, Text = Bath.Hand })) return Lang.T("Не вийшло: місця для ворона нема.");
            _bathLast = Time.time;
            if (!inTub && Bath.Depth(p) < Bath.KneelTo) { EmoteTalk.OursUntil = Time.time + 1f; p.StartEmote("kneel", true); Bath.Knelt(); } // (shallow: down on a knee to it, up again after; deeper you stand)
            Bath.Scrub(p, munin); // (your hands at it)
            return null;
        }

        // a free spot on the water right in front of you (not deeper than the raven may stand in)
        private static bool WaterBeside(Player p, out Vector3 at)
        {
            at = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            float water = p.GetLiquidLevel();
            Vector3 me = p.transform.position, fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f })
            {
                Vector3 pt = me + Quaternion.Euler(0f, turn, 0f) * fwd * 0.65f;
                if (water - ZoneSystem.instance.GetGroundHeight(pt) < 0.03f || WaterFx.Covered(pt, water)) continue; // (dry ground there; ice, a pier over it)
                pt.y = Mathf.Max(ZoneSystem.instance.GetGroundHeight(pt), water - Bath.Sink); // (in the water, not on it)
                if (SitHere.Taken(pt) || !SitHere.Free(pt)) continue;
                at = pt;
                return true;
            }
            return false;
        }

        // the console (raven bath fish): a fishing now, if there is deep water near
        internal static string FishNow(Player p)
        {
            if (p == null) return Lang.T("Нема гравця.");
            _fishNext = 0f;
            return ToFish(p) ? "off fishing" : "no fishing now (no deep water within 14 m, a visit on, a fight, or not ready)";
        }

        // the console (raven bath now): a bath now, if there is water near and it may
        internal static string BathNow(Player p, bool munin)
        {
            if (p == null) return Lang.T("Нема гравця.");
            return ToBath(p, munin ? 1 : 0) ? Lang.F("{0} летить купатися.", Plugin.RavenName(munin)) : Lang.T("Зараз не можна: нема берега поруч, ніч, холод, бій, голодний чи зайнятий.") + " (" + BathWhy(p, munin) + ")";
        }

        // which of the bars it was (the console's: for the tests)
        private static string BathWhy(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            Vector3 at;
            if (item == null) return "not on you";
            if (Now[0] != null || Now[1] != null) return "a visit going on";
            if (Senses.Fight) return "fight";
            if (Senses.Underground) return "underground";
            if (Senses.Indoors) return "indoors";
            if (Senses.Swimming) return "swimming";
            if (Senses.Sleeping) return "sleeping";
            if (Senses.OnShip) return "on a ship";
            if (Shoulder.Hovering(p)) return "hovering";
            if (!OnYouStill(p, munin)) return "not settled on you";
            if (!Outing.ReadyRaven(p, munin)) return "not ready (asleep, sulking, stuffed, busy)";
            if (!Fed(p, munin)) return "hungry";
            if (EnvMan.IsNight()) return "night";
            if (EnvMan.IsCold() || EnvMan.IsFreezing()) return "cold";
            if (!Shore(p, out at)) return "no shore within 13 m";
            return "?";
        }

        // a raven that may go for a fish: on you, free to fly, awake and not sleepy, not sulking, not stuffed, Hugin not
        // off scouting (Outing.ReadyRaven) — hunger no bar here: a hungry Munin fishes for himself
        private static bool Fisher(Player p, bool munin) { return OnYouStill(p, munin) && Outing.ReadyRaven(p, munin); }

        // a small splash, each game its own: the sound now and then; the game's fish splash only for a dive into deep
        // water (fish: in the shallows of a bath, the bottom right under it, it shows as a big white square)
        internal static void SplashSmall(Vector3 at, bool sound, bool fish = false)
        {
            Player me = Player.m_localPlayer;
            if (me == null || Vector3.Distance(me.transform.position, at) > RavenFx.HearRange) return;
            if (fish) RavenFx.Play("vfx_water_surface_fish", at, Quaternion.identity);
            if (sound) RavenFx.Play("fx_land_water", at, Quaternion.identity);
        }

        // a spot on deep water near you (off a ship's side at sea), in sight, free
        private static bool DeepWater(Player p, out Vector3 at)
        {
            at = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            float sea = ZoneSystem.instance.m_waterLevel;
            Vector3 me = p.transform.position;
            float turn = Random.Range(0f, 45f);
            foreach (float d in new[] { 6f, 10f, FishNear })
                for (int a = 0; a < 8; a++)
                {
                    Vector3 pt = me + Quaternion.Euler(0f, turn + a * 45f, 0f) * Vector3.forward * d;
                    if (ZoneSystem.instance.GetGroundHeight(pt) > sea - FishDeep) continue;
                    pt.y = sea;
                    if (WaterFx.Covered(pt, sea) || SitHere.Taken(pt) || !SitHere.Seen(p, pt, null)) continue; // (ice, a pier: no dive through it)
                    at = pt;
                    return true;
                }
            return false;
        }

        // how many fish a raven brings you a game day (in the raven: «day:count»), and none while a fish is in your bag
        internal static int FishPerDay = 3;
        private const string FishDayKey = "RavenWatch_fishDay";
        private static int FishToday(ItemDrop.ItemData raven)
        {
            string v; int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            if (raven == null || !raven.m_customData.TryGetValue(FishDayKey, out v)) return 0;
            string[] w = v.Split(':');
            return w.Length == 2 && Errands.I(w[0], -1) == day ? Errands.I(w[1], 0) : 0;
        }
        private static void Brought(ItemDrop.ItemData raven)
        {
            if (raven == null) return;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            raven.m_customData[FishDayKey] = day + ":" + (FishToday(raven) + 1);
        }
        private static bool FishBrings(ItemDrop.ItemData raven, bool inBag) { return raven != null && !inBag && FishToday(raven) < FishPerDay; }

        // a fish of any land in your bag (the one it brought, or yours: either way you have fish)
        private static bool FishInBag(Player p)
        {
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) // (a bag's few dozen things, once a dive is asked)
            {
                string n = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
                foreach (string f in FishOf.Values) if (n == f) return true;
            }
            return false;
        }

        // up out of the water: a fish of the land it dived in — a hungry Munin eats it, else home in the beak
        internal static void FishCaught(Player p, bool munin)
        {
            ItemDrop.ItemData item = RavenSeat.On(p, munin);
            Vector3 at; float hang;
            if (item == null || p.IsDead() || !Outing.FishSpot(p, munin, out at, out hang)) return;
            string prefab;
            if (WorldGenerator.instance == null || !FishOf.TryGetValue(WorldGenerator.instance.GetBiome(at), out prefab)) prefab = "Fish1";
            if (ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(prefab) == null) prefab = "Fish1";
            GameObject go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (go == null) return;
            Nicks.OnFishDive(p, munin); // a deed for its nickname (eaten or brought)
            string thing = Lang.Game(go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
            if (munin && Satiety.Zone(item) <= Satiety.Hungry)
            {
                Satiety.Change(item, FishSatiety);
                PerkFx.Say(munin, "world_fish_eaten", 1f, 0f, "thing", Lang.Lower(thing));
                if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven world: Munin ate a fish he caught (" + prefab + ")");
                return;
            }
            if (!FishBrings(item, FishInBag(p)) || !RavenFetch.Hold(p, munin, prefab, 1)) return;
            Brought(item);
            PerkFx.Say(munin, "world_fish", FishSay, 0f, "thing", Lang.Lower(thing));
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Raven world: " + Plugin.RavenName(munin) + " brings a fish it caught (" + prefab + ")");
        }

        // The top of the pole itself, in the ship's own axes, for any ship (a mod's too). The game turns the mast object
        // round to the wind on its pivot, so the pole stands on that pivot: the seat is the highest point of the mast's
        // parts right on that axis. Measured in the ship's axes from each mesh's own points or its own box — never the
        // renderers' world boxes, which swell as the ship rolls and put the seat metres up in the air beside the pole.
        //   1. a mesh that can be read: its very points within MastAxisNear of the axis (exact, even in one mesh with
        //      the yard and the ropes in it);
        //   2. else the meshes' own boxes the axis runs through (the top of the pole's box; a pole too thick for 1. too);
        //   3. no part on the axis (a mast object pivoted off its pole): the tallest thin part, else all of them.
        // Sails (cloth, skinned), lines and particles are never a seat. Kept per ship prefab: the mast is always the same.
        internal static float MastAxisNear = 0.5f; // m off the mast's axis that still counts as the pole
        private static readonly Dictionary<string, Vector3> MastTops = new Dictionary<string, Vector3>(); // ship prefab → top, in its axes
        private static bool MastTop(Ship ship, out Vector3 top)
        {
            top = Vector3.zero;
            Transform st = ship.transform;
            string key = Utils.GetPrefabName(ship.gameObject);
            Vector3 local;
            if (!MastTops.TryGetValue(key, out local))
            {
                string how;
                if (!MeasureMast(ship, out local, out how)) return false;
                MastTops[key] = local;
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven world: mast of " + key + ": " + how + ", top " + local.ToString("F2"));
            }
            top = st.TransformPoint(local);
            return true;
        }

        private static bool MeasureMast(Ship ship, out Vector3 top, out string how)
        {
            top = Vector3.zero; how = null;
            Transform st = ship.transform;
            Vector3 pivot = st.InverseTransformPoint(ship.m_mastObject.transform.position);
            float exact = float.MinValue, boxed = float.MinValue;
            string exactBy = null, boxBy = null;
            // the tallest thin part and all the parts, for a mast object pivoted off its pole
            float thinTall = 0f; Vector3 thinTop = Vector3.zero; string thinBy = null;
            bool any = false; Vector3 min = Vector3.zero, max = Vector3.zero;
            Vector3[] c = new Vector3[8];
            foreach (MeshFilter f in ship.m_mastObject.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                Mesh mesh = f.sharedMesh;
                if (mesh == null || r == null || !r.enabled || !f.gameObject.activeInHierarchy) continue;
                Transform t = f.transform;
                Bounds mb = mesh.bounds;
                Vector3 lo = Vector3.zero, hi = Vector3.zero;
                for (int i = 0; i < 8; i++)
                {
                    c[i] = st.InverseTransformPoint(t.TransformPoint(new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z)));
                    if (i == 0) { lo = hi = c[i]; } else { lo = Vector3.Min(lo, c[i]); hi = Vector3.Max(hi, c[i]); }
                }
                if (!any) { min = lo; max = hi; any = true; } else { min = Vector3.Min(min, lo); max = Vector3.Max(max, hi); }
                if (mesh.isReadable)
                {
                    Vector3[] v = mesh.vertices;
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 q = st.InverseTransformPoint(t.TransformPoint(v[i]));
                        if (q.y > exact && new Vector2(q.x - pivot.x, q.z - pivot.z).sqrMagnitude <= MastAxisNear * MastAxisNear) { exact = q.y; exactBy = f.name; }
                    }
                }
                // (its own box too, read or not: a pole thicker than the margin has no points near the axis, only its box round it)
                if (pivot.x >= lo.x - MastAxisNear && pivot.x <= hi.x + MastAxisNear && pivot.z >= lo.z - MastAxisNear && pivot.z <= hi.z + MastAxisNear && hi.y > boxed) { boxed = hi.y; boxBy = f.name; }
                float tall = hi.y - lo.y, wide = Mathf.Max(hi.x - lo.x, hi.z - lo.z);
                if (tall >= 1.5f && tall >= wide * 3f && tall > thinTall) { thinTall = tall; thinBy = f.name; thinTop = new Vector3((lo.x + hi.x) * 0.5f, hi.y, (lo.z + hi.z) * 0.5f); }
            }
            float rise = pivot.y + 1.5f; // (a top must stand well over the mast's foot)
            if (exact > rise) { top = new Vector3(pivot.x, exact, pivot.z); how = "on the axis, points of " + exactBy; }
            else if (boxed > rise) { top = new Vector3(pivot.x, boxed, pivot.z); how = "on the axis, box of " + boxBy; }
            else if (thinBy != null) { top = thinTop; how = "off the axis, thin part " + thinBy; }
            else if (any) { top = new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f); how = "off the axis, all parts"; }
            else return false;
            top += Vector3.up * 0.02f;
            return true;
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
        private static int Mask { get { if (_mask == 0) _mask = LayerMask.GetMask("Default", "static_solid", "piece", "piece_nonsolid", "Default_small", "vehicle"); return _mask; } }
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
                // a rune stone: Hugin's, read once (as a sign)
                RuneStone rune = c.GetComponentInParent<RuneStone>();
                if (rune != null)
                {
                    if (!_seen.Add(rune) || munin) continue;
                    string words = Clean(RuneText(rune));
                    if (words.Length > 0 && !_read.Contains(Spot(rune.transform.position, words))) Add(p, c, rune.transform, Rune, words, 3f);
                    continue;
                }
                // a fire burning: on the ground by it, in its warmth (more wanted in the cold)
                Fireplace fire = c.GetComponentInParent<Fireplace>();
                if (fire != null)
                {
                    Vector3 warm;
                    if (_seen.Add(fire) && Burning(fire) && BesideFire(fire, p, out warm)) _things.Add(new Thing { Kind = Fire, At = warm, Root = fire.transform, Weight = WorldReact.Fluffed(p) ? 2.5f : 1f });
                    continue;
                }
                // another player's ship or cart: on its hull or its box (not up the mast)
                Ship ship = c.GetComponentInParent<Ship>();
                Vagon cart = ship == null ? c.GetComponentInParent<Vagon>() : null;
                Component ride = ship != null ? (Component)ship : cart;
                if (ride != null)
                {
                    if (_seen.Contains(ride) || c.bounds.size.y > 4f) continue; // (a mast is no seat: another part of it may be)
                    if (!NotMine(ride, p)) { _seen.Add(ride); continue; }
                    ZNetView rnv = ride.GetComponent<ZNetView>();
                    Vector3 on;
                    if (rnv == null || !rnv.IsValid() || !SitHere.TopOf(c, out on) || SitHere.Taken(on) || !SitHere.Free(on) || !SitHere.Seen(p, on, ride.transform)) continue;
                    _seen.Add(ride);
                    _things.Add(new Thing { Kind = Ride, At = on, Root = ride.transform, Follow = SitOn.OnThing(rnv, on), Weight = 1f });
                    continue;
                }
                // bones (Munin): a pile of them, a skull, the giants' remains
                if (munin && c.GetComponentInParent<Character>() == null)
                {
                    ZNetView bnv = c.GetComponentInParent<ZNetView>();
                    if (bnv != null && IsBones(bnv.gameObject) && !Built(bnv))
                    {
                        if (_seen.Add(bnv) && !PeckedToday(bnv.transform.position)) Add(p, c, bnv.transform, Bones, null, 2.5f);
                        continue;
                    }
                }
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
            // a trader: onto its head (a trader is no character: found where it stands, only when looked for)
            foreach (Trader npc in Object.FindObjectsByType<Trader>(FindObjectsSortMode.None))
            {
                if (npc == null || (npc.transform.position - me).sqrMagnitude > r * r) continue;
                Vector3 head;
                if (!HeadOf(npc, out head) || SitHere.Taken(head) || !SitHere.Free(head) || !SitHere.Seen(p, head, npc.transform)) continue;
                ZNetView tnv = npc.GetComponent<ZNetView>();
                _things.Add(new Thing { Kind = TraderHead, At = head, Root = npc.transform, Npc = npc, Text = Lang.Game(npc.m_name), Follow = tnv != null && tnv.IsValid() ? SitOn.OnThing(tnv, head) : null, Weight = munin ? 3f : 2f });
            }
            // the game's gulls and crows: after one of them
            ZDOID bird;
            if (!Senses.Indoors && WorldBirds.Near(p, out bird)) _things.Add(new Thing { Kind = Birds, At = me, Root = p.transform, Bird = bird, Weight = munin ? 2f : 1f });
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

        // the words on a rune stone (its own, or one of its tales: picked by where it stands, the same each time)
        private static string RuneText(RuneStone rs)
        {
            string text = rs.m_text;
            if (rs.m_randomTexts != null && rs.m_randomTexts.Count > 0)
            {
                Vector3 at = rs.transform.position;
                int i = (((Mathf.RoundToInt(at.x) * 73856093) ^ (Mathf.RoundToInt(at.z) * 19349663)) & 0x7fffffff) % rs.m_randomTexts.Count;
                if (rs.m_randomTexts[i] != null && !string.IsNullOrEmpty(rs.m_randomTexts[i].m_text)) text = rs.m_randomTexts[i].m_text;
            }
            if (string.IsNullOrEmpty(text)) return "";
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
        }

        // a fire alight now (its flames shown)
        private static bool Burning(Fireplace f) { return f.m_enabledObject != null && f.m_enabledObject.activeInHierarchy; }

        // the ground by a fire, on your side of it, in its warmth
        private static bool BesideFire(Fireplace f, Player p, out Vector3 at)
        {
            at = Vector3.zero;
            if (_groundMask == 0) _groundMask = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
            Vector3 c = f.transform.position, toYou = Vector3.ProjectOnPlane(p.transform.position - c, Vector3.up);
            toYou = toYou.sqrMagnitude > 0.01f ? toYou.normalized : Vector3.forward;
            foreach (float turn in new[] { 0f, 50f, -50f, 100f, -100f })
            {
                Vector3 pt = c + Quaternion.Euler(0f, turn, 0f) * toYou * 1.3f;
                RaycastHit h;
                if (!Physics.Raycast(pt + Vector3.up * 1.2f, Vector3.down, out h, 2.5f, _groundMask, QueryTriggerInteraction.Ignore) || h.normal.y < 0.6f) continue;
                if (SitHere.Taken(h.point) || !SitHere.Free(h.point) || !SitHere.Seen(p, h.point, f.transform)) continue;
                if (EffectArea.IsPointInsideArea(h.point, EffectArea.Type.Heat, 0.5f) == null) continue; // (in its warmth, or why sit there)
                at = h.point;
                return true;
            }
            return false;
        }

        // a ship or a cart someone else made (not the one you stand on)
        private static bool NotMine(Component ride, Player p)
        {
            Piece pc = ride.GetComponent<Piece>();
            if (pc == null || pc.GetCreator() == p.GetPlayerID()) return false;
            Ship on = p.GetStandingOnShip();
            return on == null || on.gameObject != ride.gameObject;
        }

        // a building piece someone put there (not the world's own)
        private static bool Built(Component c) { Piece pc = c.GetComponent<Piece>(); return pc != null && pc.GetCreator() != 0L; }

        // bones lying about: a pile, a skull, a giant's ribs (by the prefab's name)
        private static readonly string[] BoneWords = { "bonepile", "bone_pile", "bones", "skull", "skeleton", "giant_ribs", "ribcage" };
        private static bool IsBones(GameObject go)
        {
            string name = Utils.GetPrefabName(go).ToLowerInvariant();
            if (name.Contains("bonemass") || name.Contains("trophy") || name.Contains("altar")) return false;
            foreach (string w in BoneWords) if (name.Contains(w)) return true;
            return false;
        }

        // the top of a trader's head. A person (the model's top just over its head bone: a hat, hair): that top. Else —
        // a trader of another mod may be a mounted skull with antlers, whose tips would have it sit in the air beside
        // it — over the head (or the middle) down onto the trader's own collider, where it is flat enough to sit.
        // Nothing to sit on: no visit.
        internal static float HeadTopGap = 0.8f;                    // m: the model's top over the head bone, at most, for a person
        internal static float HeadFlat = 0.6f;                      // the surface's normal.y at least
        private static readonly RaycastHit[] HeadHits = new RaycastHit[16];

        // a trader's head bone by its name (no Character: no m_head); null — a figure without one (a mod's skull)
        internal static Transform HeadBone(Transform trader)
        {
            foreach (Transform tr in trader.GetComponentsInChildren<Transform>()) { string n = tr.name.ToLowerInvariant(); if (n == "head" || n.EndsWith("_head") || n.EndsWith(":head")) return tr; }
            return null;
        }

        private static bool HeadOf(Trader t, out Vector3 at)
        {
            at = Vector3.zero;
            Transform head = HeadBone(t.transform);
            bool any = false;
            Bounds b = new Bounds();
            foreach (SkinnedMeshRenderer r in t.GetComponentsInChildren<SkinnedMeshRenderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            if (!any) return false;
            Vector3 x = head != null ? head.position : b.center;
            if (head != null && b.max.y - head.position.y < HeadTopGap) { at = new Vector3(x.x, b.max.y + 0.02f, x.z); return true; } // a person: the top of the model is the top of the head (a hat, hair)
            float from = b.max.y + 0.5f;
            int hits = Physics.RaycastNonAlloc(new Vector3(x.x, from, x.z), Vector3.down, HeadHits, from - b.min.y + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            int top = -1;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit h = HeadHits[i];
                if (h.collider == null || !h.collider.transform.IsChildOf(t.transform)) continue; // (the trader's own)
                if (top < 0 || h.point.y > HeadHits[top].point.y) top = i; // (unordered: the highest)
            }
            if (top < 0 || HeadHits[top].normal.y < HeadFlat) return false; // (its highest point there is a slope: no seat)
            at = HeadHits[top].point;
            return true;
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
                    if (g < sea && WaterFx.Covered(pt, sea)) continue; // (frozen over, under a pier: no bath there)
                    pt.y = Mathf.Max(g, sea - Bath.Sink); // (in the shallows: on the bottom, the water up its legs — never standing on the water)
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
            if (t.Kind == Birds) return Outing.StartHunt(p, munin, t.Bird); // (after a bird: an outing of its own)
            Vector3 way = t.At - p.transform.position; way.y = 0f;
            Vector3 fwd = way.sqrMagnitude > 0.01f ? way.normalized : Vector3.forward;
            int k = munin ? 1 : 0;
            // the bed: while you sleep; the grave: while you gather your things; the beehive: cut short once it lands
            float stay = t.Kind == Bed ? 9999f : t.Kind == Grave ? 120f : t.Kind == Mast ? Random.Range(MastMin, MastMax) : t.Kind == Bees ? 60f : t.Kind == Bathe ? Bath.StayOf(RavenSeat.On(p, munin), t.Text) + 1.5f
                : t.Kind == Fire ? Random.Range(FireStayMin, FireStayMax) : t.Kind == Bones ? Random.Range(BonesStayMin, BonesStayMax) : Random.Range(StayMin, StayMax);
            Stay s = new Stay { Kind = t.Kind, Text = t.Text, At = t.At, Of = t.Root.position, Item = t.Item, Ship = t.Ship, Npc = t.Npc, Started = Time.time, Until = Time.time + stay };
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
                bool end = Time.time >= s.Until || p.IsDead() || Senses.Fight || Senses.Underground || (Senses.Swimming && !(s.Kind == Bathe && !string.IsNullOrEmpty(s.Text))) // (in the tub or the water with it: deep, maybe)
                    || (s.Kind == Bed ? !p.InBed() : Senses.Sleeping)
                    || (s.Kind == Mast && (!Senses.OnShip || s.Ship == null || (s.CalmFrom >= 0f && Time.time - s.CalmFrom > 20f)))
                    || (s.Kind == Ride && (s.View.transform.position - p.transform.position).sqrMagnitude > RideFar * RideFar) // (carried off too far: home)
                    || (s.Kind == TraderHead && s.Npc == null);
                // the shiny thing gone (taken by you or anyone near: a grumble; out of this game's sight: just back)
                if (!end && s.Kind == Shiny && s.Landed && s.Item == null)
                {
                    if ((p.transform.position - s.Of).sqrMagnitude < Radius * Radius && Director.May(Director.Voice.Event)) Speak(p, munin, s, "world_shiny_taken", Director.Voice.Event, null);
                    end = true;
                }
                if (!end && !s.Landed && s.View.Settled(0)) { s.Landed = true; Arrived(p, munin, s, item); }
                else if (!end && s.Landed && !s.Done) Act(p, munin, s, item); // (a word not let through yet: tried again)
                if (!end && s.Landed && (s.Kind == Berry || s.Kind == Water || s.Kind == Beast || s.Kind == Bones) && Time.time >= s.NextPeck) PeckAgain(s);
                if (!end && s.Landed && s.Kind == TraderHead && s.Grumbled < Grumbles && Time.time >= s.NextGrumble) Grumble(s);
                if (!end && s.Landed && s.Kind == Mast && Time.time >= s.NextLook) Lookout(p, munin, s);
                if (!end) continue;
                Now[k] = null;
                if (s.Kind == Bones && s.Landed && !p.IsDead()) Dig(p, munin, s); // (what he pulled out flies home in his beak)
                else if (s.Kind == Bathe) { if (s.Landed && Time.time >= s.Until && !p.IsDead()) Bath.Done(p, munin, item, s.Text ?? ""); } // (washed through: clean, and the shake on you)
                else if (s.Landed && Time.time >= s.Until && s.Kind != Bed && s.Kind != Grave && s.Kind != Mast) RavenLoot.After(p, munin); // (a visit that ran its time: now and then a find)
                Perches.CallQuiet(p, item, munin);
            }
        }

        private static void Arrived(Player p, bool munin, Stay s, ItemDrop.ItemData item)
        {
            if (s.Kind == Water || s.Kind == Beast || s.Kind == Bones) PeckAgain(s);
            if (s.Kind == Bones) PeckedNow(s.Of); // (a pile once a day)
            if (s.Kind == TraderHead) s.NextGrumble = Time.time + GrumbleFirst;
            if (s.Kind == Bees) { PeckAgain(s); s.Until = Time.time + BeesStay; } // a poke, and the bees are out
            if (s.Kind == Bathe) { s.Until = Time.time + Bath.StayOf(item, s.Text) + 0.6f; if (!Bath.InTubAt(p, s.At)) { SplashSmall(s.At, true); WaterFx.Ring(s.At, 1.2f); WaterFx.Spray(s.At, 10, 1f); } BatheOn(s, s.Until - Time.time - 0.3f); } // (its time counted from the landing: a long flight ate it) // into the water, and the bath on every game
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

        // the bath on the spot: one more on its count, with how long (SpotFollow → PerchLife.Bathe)
        private static void BatheOn(Stay s, float seconds)
        {
            PerchLife here = s.View != null ? s.View.LifeAt(0) : null;
            if (here != null) here.Bathe(seconds); // (here at once, the moment it lands: no wait for the count)
            ZDO z = s.View != null ? s.View.Zdo : null;
            if (z == null || !s.View.Owner) return;
            int n = (z.GetInt(SpotFollow.BatheKey, 0) >> 8) + 1;
            z.Set(SpotFollow.BatheKey, (n << 8) | Mathf.Clamp(Mathf.RoundToInt(seconds), 2, 255));
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
                case Rune:
                    if (!Director.May(Director.Voice.Event)) return; // (as a sign: tried again while it sits)
                    if (Speak(p, munin, s, "world_rune", Director.Voice.Event, null, "rune", s.Text)) { s.Done = true; Remember(p, s.Of, s.Text); }
                    return;
                case TraderHead:
                    s.Done = true;
                    if (Director.May(Director.Voice.Event)) Speak(p, munin, s, "world_trader", Director.Voice.Event, null, "trader", s.Text);
                    return;
                case Fire:
                    s.Done = true;
                    if (Random.value < FireSay && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_fire", Director.Voice.Shoulder, null);
                    return;
                case Ride:
                    s.Done = true;
                    if (Random.value < RideSay && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_ride", Director.Voice.Shoulder, null);
                    return;
                case Bones:
                    s.Done = true;
                    if (Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_bones", Director.Voice.Shoulder, null);
                    return;
                case Bathe:
                    s.Done = true;
                    if (Random.value < (s.Text == "" || s.Text == null ? 0.6f : 0.85f) && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, s.Text == Bath.Hand ? "bath_hand" : s.Text == Bath.Tub ? "bath_tub" : "world_bath", Director.Voice.Shoulder, null); // (by your hands, in the tub with you: their own words)
                    return;
                default:
                    s.Done = true;
                    float chance = s.Kind == Berry ? BerrySay : s.Kind == Shiny ? ShinySay : s.Kind == Water ? WaterSay : PerchSay;
                    if (Random.value >= chance || !Director.May(Director.Voice.Shoulder)) return;
                    Speak(p, munin, s, s.Kind == Berry ? "world_berry" : s.Kind == Shiny ? "world_shiny" : s.Kind == Water ? "world_water" : "world_perch", Director.Voice.Shoulder, null);
                    return;
            }
        }

        // the trader under it grumbles (its own words over its head, as it talks to you)
        private static void Grumble(Stay s)
        {
            s.Grumbled++;
            s.NextGrumble = Time.time + Random.Range(GrumbleEveryMin, GrumbleEveryMax);
            if (s.Npc == null || Chat.instance == null) return;
            string token = (s.Npc.m_name ?? "").ToLowerInvariant();
            string[] lines = token.Contains("haldor") ? HaldorLines : token.Contains("hildir") ? HildirLines : token.Contains("bogwitch") ? WitchLines : AnyTraderLines;
            string key = lines[Random.Range(0, lines.Length)];
            TraderSays(s.Npc, key);
            // the others nearby hear it too, each in its own language (the key goes, not the words)
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(s.Npc.transform.position);
            pkg.Write(key);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, GrumbleRpc, pkg);
        }

        private const string GrumbleRpc = "RavenWatch_TraderGrumble";
        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(GrumbleRpc, new System.Action<long, ZPackage>(OnGrumble));
        }

        private static void OnGrumble(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return; // said here already
            Vector3 at = pkg.ReadVector3();
            string key = pkg.ReadString();
            Player me = Player.m_localPlayer;
            if (me == null || Vector3.Distance(me.transform.position, at) > 40f) return;
            if (System.Array.IndexOf(HaldorLines, key) < 0 && System.Array.IndexOf(HildirLines, key) < 0
                && System.Array.IndexOf(WitchLines, key) < 0 && System.Array.IndexOf(AnyTraderLines, key) < 0) return; // only our own lines
            Trader best = null; float bestD = 4f; // rare (twice a visit): a look over the loaded traders is cheap enough
            foreach (Trader t in Object.FindObjectsByType<Trader>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(t.transform.position, at);
                if (d < bestD) { bestD = d; best = t; }
            }
            if (best != null) TraderSays(best, key);
        }

        private static void TraderSays(Trader npc, string key)
        {
            if (npc == null || Chat.instance == null) return;
            Chat.instance.SetNpcText(npc.gameObject, Vector3.up * npc.m_dialogHeight, 20f, RavenSpeech.Hold(Mathf.Max(4f, npc.m_hideDialogDelay)), "", Lang.T(key), false);
        }

        private static readonly string[] HaldorLines = Lang.Keys("Гей! Злазь з моєї голови, пташко!", "Я тут торгую, а не гніздо тримаю!", "Ще раз нагадиш на капелюх — продам тебе на пір'я.", "Чий це ворон? Плати за постій!", "Кшш! Ох, ці воїни зі своїми птахами…", "Тільки не дзьобай. Тільки не дзьобай…");
        private static readonly string[] HildirLines = Lang.Keys("Моя зачіска! Ану геть!", "Пташко, я тобі не жердина.", "Подряпаєш капелюшок — заплатиш золотом.", "Ох, ворони… Тільки не на мій товар, благаю.", "Ну й манери в цього птаха. Весь у хазяїна.");
        private static readonly string[] WitchLines = Lang.Keys("Хі-хі… Ще трохи посидиш — підеш у казан.", "Сиди-сиди. Мені якраз бракує пір'я для зілля.", "Ворон на голові — на дощ. Або на біду.", "Важкий який. Ти що, жабами годуєшся?");
        private static readonly string[] AnyTraderLines = Lang.Keys("Гей! Злазь!", "Я тобі не жердина, птахо.", "Кшш! Іди звідси, бо продам.");

        // Munin dug about in the bones: now and then something comes out — in his beak home to you (RavenFetch.Hold)
        private static void Dig(Player p, bool munin, Stay s)
        {
            Bath.Soil(RavenSeat.On(p, munin), Bath.BonesDirt, Bath.Bones); // (the dirt of the bones on it)
            float roll = Random.value;
            for (int i = 0; i < BoneFinds.Length; i++)
            {
                if (roll >= BoneChance[i]) { roll -= BoneChance[i]; continue; }
                int n = Random.Range(1, BoneMost[i] + 1);
                if (!RavenFetch.Hold(p, munin, BoneFinds[i], n)) return;
                GameObject go = ObjectDB.instance.GetItemPrefab(BoneFinds[i]); // (Hold found it: it is there)
                string thing = Lang.Game(go.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
                if (Director.May(Director.Voice.Event)) Speak(p, munin, s, "world_bones_find", Director.Voice.Event, null, "thing", thing);
                return;
            }
            if (Random.value < BonesEmptySay && Director.May(Director.Voice.Shoulder)) Speak(p, munin, s, "world_bones_empty", Director.Voice.Shoulder, null);
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

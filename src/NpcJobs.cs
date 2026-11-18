#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The jobs (npc-plan.md, stage 2): each one entry — its rule of what to do next (an errand for NpcWorker), its word,
    // its picture, its button. NpcWorker carries the errands out; a new job is a class here and a line in Jobs.All.
    // ---------------------------------------------------------------- the jobs
    internal abstract class Job
    {
        internal string Id, Word, Desc, Icon = "n-job"; // (its name, a line about it and its picture for the menu)
        internal bool AllDay;                // works day and night (a guard), not only in the day
        internal bool Evening;               // works the evening too (a servant: its fires and lights burn then)
        internal string Place;               // taken by pointing at a spot (a servant: its house) — the menu's «наведи на …»
        internal virtual string Assign(NpcActor a, Vector3 at) { return NpcWorker.SetJob(a, Id, NpcMenu.By); } // (only the menu's pick of a place: the one who pressed it)
        internal virtual string Can(NpcActor a) { return null; } // why this one cannot take it (null: it can)
        internal bool StoreAfterEach = true; // (gatherer/cleaner: only when its hands are full or nothing is left)
        internal bool NeedsChest = true;     // cannot work without a chest of its own: takes from it or brings to it (a guard does neither; a servant mends and shuts doors without one)
        internal bool HasChest = true;       // may be given one («Ящик» in its ring): all but a guard. Not NeedsChest: a servant needs none and still has one — its button asked
                                             // NeedsChest, so a servant could be given no chest at all and its fuel, its floor and its refill never ran (read 2026-11-12)
        internal bool HasSupply;             // may be given a second chest to take from («Інгредієнти»: a cook) — its own chest then only receives what it keeps (Keeps); none set: one chest for both
        internal virtual bool Keeps(ItemDrop.ItemData it) { return true; } // …what of its hands goes to its own chest then (a cook: its dishes); the rest back to where it takes from
        // what it lacks for its work now, in the player's words, a line each (its card «Побут», the heart's board) — asked on any game, only while that is shown
        internal virtual void Lacks(NpcWorker w, List<string> into) { }
        internal string Move = "reach_in";   // its body's move at each errand (a clip: NpcWorker, Stage.Use); an errand's own Move wins; "" none (its tool's swing is its own)
        internal abstract NpcWorker.Errand Find(NpcWorker w);
        // the fuel its chest should hold (the game's names of the things: «$item_wood»…) — a servant keeps it topped up
        internal virtual void Fuels(NpcWorker w, HashSet<string> into) { }

        // the game's name of a fuel (or nothing)
        internal static void Add(HashSet<string> into, ItemDrop fuel) { if (fuel != null && fuel.m_itemData != null) into.Add(fuel.m_itemData.m_shared.m_name); }
    }

    internal static class Jobs
    {
        internal static readonly List<Job> All = new List<Job> { new Stoker(), new SmelterWork(), new Cook(), new Keeper(), new Gatherer(), new Cleaner(), new Guard(), new Servant(), new Lumber(), new Miner() };

        internal static Job Find(string id) { foreach (Job j in All) if (j.Id == id) return j; return null; }

        // its word for a player, in the reader's language (the phrases' own language: Lang.W(Find(id).Word))
        internal static string Kept(string id) { Job j = Find(id); return j != null ? j.Word : Lang.K("без роботи"); } // (the source: what is kept — a goal's memory)
        internal static string Word(string id) { return Lang.T(Kept(id)); }
    }

    // fires and torches: fuel from the chest when they burn low. The base's own first; a player's house (NpcPrivate) only
    // when they want nothing; a fire a servant keeps (its house, and the servant has its fuel) never: that is the
    // servant's work (the user, 2026-11-14)
    internal sealed class Stoker : Job
    {
        internal Stoker() { Move = "feed_fire"; Id = "stoker"; Word = Lang.K("палій"); Icon = "n-stoker"; Desc = Lang.K("Вогнища й факели в базі: підкладає пальне з ящика, коли догорають. У хату гравця йде в останню чергу, а в дім, який доглядає слуга, не ходить."); }

        internal override NpcWorker.Errand Find(NpcWorker w) { Sort(w); return Fuel(w, Open) ?? Fuel(w, InHouse); }

        internal override void Fuels(NpcWorker w, HashSet<string> into)
        {
            Sort(w);
            foreach (Fireplace f in Open) if (!f.m_infiniteFuel && f.m_canRefill) Add(into, f.m_fuelItem);
            foreach (Fireplace f in InHouse) if (!f.m_infiniteFuel && f.m_canRefill) Add(into, f.m_fuelItem);
        }

        // its base's fires (nearest first, InBase) in two lists, filled anew each time and read at once (nothing kept from
        // them: an errand holds its own fire); the servants here with a chest and their houses, looked up once a time
        private static readonly List<Fireplace> Open = new List<Fireplace>(), InHouse = new List<Fireplace>();
        private static readonly List<KeyValuePair<NpcWorker, NpcHouse>> Keepers = new List<KeyValuePair<NpcWorker, NpcHouse>>();
        private static void Sort(NpcWorker w)
        {
            Open.Clear(); InHouse.Clear();
            Servant.Keepers(Keepers);
            NpcBase b = w.Base;
            foreach (Fireplace f in w.InBase<Fireplace>())
            {
                if (f == null || Kept(f)) continue;
                Vector3 p = f.transform.position;
                NpcHouse h = NpcPrivate.At(b, p);
                if (h == null || NpcKit.UnderSky(p)) Open.Add(f); // (nothing built over it: a yard, no one's house — as NpcPrivate.Off)
                else InHouse.Add(f);
            }
            Keepers.Clear(); // (no worker held past this look)
        }

        // a servant fuels this one: its house's (as Servant.Fuels: on a floor of it or its edge), and it has the fuel at hand
        // (none in its chest: the stoker may go, last of all if it is a player's house)
        private static bool Kept(Fireplace f)
        {
            if (Keepers.Count == 0 || f.m_fuelItem == null) return false;
            string name = f.m_fuelItem.m_itemData.m_shared.m_name;
            foreach (KeyValuePair<NpcWorker, NpcHouse> k in Keepers)
                if (k.Value.Near(f.transform.position) && k.Key.Carrying(name) + k.Key.InChest(name) > 0) return true;
            return false;
        }

        // none of its fires' fuel in its chest
        internal override void Lacks(NpcWorker w, List<string> into)
        {
            Container c = w.Chest;
            if (c == null || c.GetInventory() == null) return; // (not to be seen from this game)
            HashSet<string> fuels = new HashSet<string>(); Fuels(w, fuels);
            if (fuels.Count == 0) { into.Add(Lang.T("на базі нема вогнищ, які можна підживити")); return; }
            foreach (string f in fuels) if (c.GetInventory().CountItems(f) > 0) return;
            into.Add(Lang.T("у ящику нема пального для вогнищ"));
        }

        // a fire or a light of these that burns low: fuel from the chest (the servant's too, for its house). One about to
        // go out first (Urgent), else the first of them (the lists come nearest first: InBase)
        internal static NpcWorker.Errand Fuel(NpcWorker w, IEnumerable<Fireplace> fires)
        {
            Fireplace low = null; float lowFuel = 0f;
            foreach (Fireplace f in fires)
            {
                if (f == null || f.m_infiniteFuel || !f.m_canRefill || f.m_fuelItem == null) continue;
                ZNetView nv = f.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                if (f.m_canTurnOff && nv.GetZDO().GetInt(ZDOVars.s_state, 1) == 0) continue; // (put out on purpose)
                float fuel = nv.GetZDO().GetFloat(ZDOVars.s_fuel);
                if (fuel > f.m_maxFuel - Mathf.Min(2f, f.m_maxFuel * 0.5f)) continue; // (two short; a small one, a mod's: half of it)
                if (!Reserve.IsFree(nv.GetZDO().m_uid, w.Me)) continue;
                string fuelName = f.m_fuelItem.m_itemData.m_shared.m_name;
                if (w.Carrying(fuelName) + w.InChest(fuelName) <= 0) continue;
                if (fuel <= 1f) return FuelErrand(f, nv, fuel);
                if (low == null) { low = f; lowFuel = fuel; }
            }
            return low != null ? FuelErrand(low, low.GetComponent<ZNetView>(), lowFuel) : null;
        }

        private static NpcWorker.Errand FuelErrand(Fireplace f, ZNetView nv, float fuel)
        {
            string fuelName = f.m_fuelItem.m_itemData.m_shared.m_name;
            return new NpcWorker.Errand
            {
                Target = nv.GetZDO().m_uid, At = f.transform.position, Near = 1.8f, Take = fuelName, Count = Mathf.Max(1, Mathf.FloorToInt(f.m_maxFuel - fuel)),
                Word = Lang.K("підкладає дрова"), Move = "feed_fire", Urgent = fuel <= 1f, // (about to go out: first among a servant's errands)
                Use = (wk, e) =>
                {
                    // all it brings in one go: one message to the fire, not one a log (the game's own «add this much»)
                    if (f == null || nv == null || !nv.IsValid()) return true;
                    int put = Mathf.Min(wk.Carrying(fuelName), Mathf.FloorToInt(f.m_maxFuel - nv.GetZDO().GetFloat(ZDOVars.s_fuel)));
                    for (int k = 0; k < put; k++) wk.Spend(fuelName);
                    if (put > 0) nv.InvokeRPC("RPC_AddFuelAmount", (float)put);
                    return true;
                }
            };
        }
    }

    // smelters, kilns, furnaces: what is done out (and to the chest), fuel and ore in from the chest
    internal sealed class SmelterWork : Job
    {
        internal SmelterWork() { Move = "feed_fire"; Id = "smelter"; Word = Lang.K("плавильник"); Icon = "n-smelter"; Desc = Lang.K("Плавильні й печі: виймає готове в ящик, підсипає вугілля й руду з ящика."); }

        internal override void Fuels(NpcWorker w, HashSet<string> into) { foreach (Smelter s in w.InBase<Smelter>()) Add(into, s.m_fuelItem); }

        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            foreach (Smelter s in w.InBase<Smelter>())
            {
                ZNetView nv = s.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                ZDOID id = nv.GetZDO().m_uid;
                if (!Reserve.IsFree(id, w.Me)) continue;
                ZDO z = nv.GetZDO();
                Vector3 outAt = s.m_outputPoint != null ? s.m_outputPoint.position : s.transform.position;
                HashSet<string> made = new HashSet<string>();
                foreach (Smelter.ItemConversion c in s.m_conversion) if (c != null && c.m_to != null) made.Add(c.m_to.gameObject.name);
                // 1) done: out and to the chest
                bool lying = false;
                foreach (ItemDrop d in NpcWorker.Drops(outAt, 3.5f)) if (made.Contains(d.gameObject.name.Replace("(Clone)", "").Trim())) { lying = true; break; }
                if (z.GetInt(ZDOVars.s_spawnAmount) > 0 || lying)
                {
                    bool asked = false;
                    return new NpcWorker.Errand
                    {
                        Target = id, At = outAt, Near = 1.8f, Collect = made, Word = Lang.K("виймає готове"), Move = "reach_in",
                        Use = (wk, e) => { if (!asked && z.GetInt(ZDOVars.s_spawnAmount) > 0) { asked = true; nv.InvokeRPC("RPC_EmptyProcessed"); } return true; }
                    };
                }
                // 2) fuel
                if (s.m_maxFuel > 0 && s.m_fuelItem != null && z.GetFloat(ZDOVars.s_fuel) <= s.m_maxFuel - 3)
                {
                    string fuelName = s.m_fuelItem.m_itemData.m_shared.m_name;
                    if (w.Carrying(fuelName) + w.InChest(fuelName) > 0)
                        return new NpcWorker.Errand
                        {
                            Target = id, At = s.transform.position, Near = 2.2f, Take = fuelName, Count = s.m_maxFuel - Mathf.CeilToInt(z.GetFloat(ZDOVars.s_fuel)), Word = Lang.K("підкладає вугілля"),
                            Use = (wk, e) =>
                            {
                                if (!nv.IsValid() || z.GetFloat(ZDOVars.s_fuel) > s.m_maxFuel - 1) return true;
                                if (!wk.Spend(fuelName)) return true;
                                nv.InvokeRPC("RPC_AddFuel");
                                return false;
                            }
                        };
                }
                // 3) ore (any the chest has that this one takes)
                if (z.GetInt(ZDOVars.s_queued) < s.m_maxOre)
                {
                    foreach (Smelter.ItemConversion c in s.m_conversion)
                    {
                        if (c == null || c.m_from == null) continue;
                        string ore = c.m_from.m_itemData.m_shared.m_name, prefab = c.m_from.gameObject.name;
                        if (w.Carrying(ore) + w.InChest(ore) <= 0) continue;
                        return new NpcWorker.Errand
                        {
                            Target = id, At = s.transform.position, Near = 2.2f, Take = ore, Count = s.m_maxOre - z.GetInt(ZDOVars.s_queued), Word = Lang.K("засипає руду"),
                            Use = (wk, e) =>
                            {
                                if (!nv.IsValid() || z.GetInt(ZDOVars.s_queued) >= s.m_maxOre) return true;
                                if (!wk.Spend(ore)) return true;
                                nv.InvokeRPC("RPC_AddOre", prefab, false);
                                return false;
                            }
                        };
                    }
                }
            }
            return null;
        }
    }

    // bushes and plants that grow back: picked, all of it to the chest
    internal sealed class Gatherer : Job
    {
        internal Gatherer() { Move = "pick_low"; Id = "gatherer"; Word = Lang.K("збирач"); Icon = "n-gatherer"; Desc = Lang.K("Кущі й рослини, що відростають (ягоди, гриби): збирає в ящик."); StoreAfterEach = false; }

        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            if (!w.Room) return null;
            foreach (Pickable p in w.InBase<Pickable>())
            {
                if (p.m_respawnTimeMinutes <= 0f || p.m_itemPrefab == null || !p.CanBePicked()) continue; // (crops that do not grow back: the player's)
                ZNetView nv = p.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                ZDOID id = nv.GetZDO().m_uid;
                if (!Reserve.IsFree(id, w.Me)) continue;
                HashSet<string> what = new HashSet<string> { p.m_itemPrefab.name };
                bool done = false;
                return new NpcWorker.Errand
                {
                    Target = id, At = p.transform.position, Near = 1.4f, Collect = what, Word = Lang.K("збирає"),
                    Use = (wk, e) => { if (!done && p != null && p.CanBePicked()) nv.InvokeRPC("RPC_Pick", 0); done = true; return true; }
                };
            }
            return null;
        }
    }

    // things lying about in the base: picked up, to the chest
    internal sealed class Cleaner : Job
    {
        internal Cleaner() { Move = "pick_low"; Id = "cleaner"; Word = Lang.K("прибиральник"); Icon = "n-cleaner"; Desc = Lang.K("Речі, що валяються в базі: підбирає й несе в ящик (які саме — «Що прибирати»). Готове біля плавилень, бочок і вуликів лишає тим, хто там працює."); StoreAfterEach = false; }

        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            return Tidy(w, w.InBase<ItemDrop>());
        }

        // a thing lying about among these: picked up, to the chest (the servant's too, off its house's floor)
        internal static NpcWorker.Errand Tidy(NpcWorker w, IEnumerable<ItemDrop> drops)
        {
            if (!w.Room) return null;
            foreach (ItemDrop d in drops)
            {
                if (d == null || d.GetComponent("EggGrow") != null) continue; // (an egg lying warm to hatch: left where it is)
                if (NpcWorker.Untouchable(d)) continue; // (the story's, the ravens', a keepsake, a quest thing: never moved about — a raven laid down in the base went into the cleaner's chest; the servant's putting away asked this already)
                if (!NpcTidy.Takes(w.Actor, NpcTidy.KindOf(d))) continue; // (a kind it was told to leave)
                if (NpcTidy.Claimed(w.Base, w.Pos, d)) continue; // (a station's make by it: its worker's)
                ZDOID id = NpcKit.IdOf(d);
                if (id.IsNone() || !Reserve.IsFree(id, w.Me)) continue;
                HashSet<string> what = new HashSet<string> { d.gameObject.name.Replace("(Clone)", "").Trim() };
                return new NpcWorker.Errand { Target = id, At = d.transform.position, Near = 1.2f, Collect = what, Word = Lang.K("прибирає"), Use = (wk, e) => true };
            }
            return null;
        }
    }

    // A servant (npc-plan.md, «слуга»): one house of its base, pointed at (NpcHouse: its rooms under one roof) — its
    // fires and lights fuelled from its chest, its damaged walls mended with a hammer, what lies on its floor picked up,
    // its doors shut when nobody is by them; its chest kept in fuel from its storeroom (the chests round a spot you show,
    // «Склад»), and what you put in its chest or its inbox («Розбір») put away there (else in the base — never into
    // another player's house; nowhere: left, and named). The most pressing first. Works the day and the evening (its
    // lights burn then). With nothing to do it lives its own day like any resident (a walk about the base, a rest, a whim).
    internal sealed class Servant : Job
    {
        internal Servant() { Id = "servant"; Word = Lang.K("слуга"); Icon = "n-servant"; Evening = true; Place = Lang.K("на дім, який доглядатиме"); StoreAfterEach = false; NeedsChest = false;
            Desc = Lang.K("Доглядає один дім: лагодить побите, зачиняє двері; з ящиком запасів — ще й підкладає пальне вогнищам і світильникам, прибирає підлогу, бере пальне зі складу у свою скриню, коли воно закінчується, а те, що ти кладеш у його скриню чи скриню для розбору, розкладає на складі (нема місця — по скринях бази, де лежить те саме, не в доми інших гравців). Сюжетні й особливі речі не чіпає."); }

        private const int Low = 10, Fill = 30; // (its chest with less of its house's fuel than this: brought up to that)

        // the servants on this game that fuel their house's fires (a chest to hand, the house looked over), with that house
        // (Stoker.Sort: the stoker stays out of those fires)
        internal static void Keepers(List<KeyValuePair<NpcWorker, NpcHouse>> into)
        {
            into.Clear();
            foreach (NpcActor o in NpcActor.Live)
            {
                NpcWorker k = o != null ? o.Worker : null;
                if (k == null || !(k.Job is Servant) || k.Chest == null) continue;
                NpcHouse h = k.House;
                if (h != null && h.Good) into.Add(new KeyValuePair<NpcWorker, NpcHouse>(k, h));
            }
        }

        // the fuel of its house's fires and lights
        internal override void Fuels(NpcWorker w, HashSet<string> into)
        {
            NpcHouse h = w.House;
            if (h == null || !h.Good) return;
            foreach (Fireplace f in w.InBase<Fireplace>()) if (h.Near(f.transform.position) && !f.m_infiniteFuel && f.m_canRefill) Add(into, f.m_fuelItem);
        }

        // a chest it may take from or put into: built (not a ship's hold, not a person's cells, not the heart's coin box), not
        // private, not open now
        private static bool Open(Container c, NpcActor me)
        {
            if (c == null || c.GetComponent<Piece>() == null || c.m_privacy == Container.PrivacySetting.Private) return false;
            ZNetView nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() && (nv.GetZDO().GetInt(ZDOVars.s_inUse, 0) == 0 || NpcLid.Mine(c, me)); // (its own lid up a moment ago: still for it)
        }

        // the storeroom's chests (none pointed at: none)
        private static List<Container> Store(NpcWorker w, List<Container> chests)
        {
            List<Container> l = new List<Container>();
            Vector3 at;
            if (!w.StoreAt(out at)) return l;
            foreach (Container c in chests) if (Open(c, w.Actor) && Utils.DistanceXZ(c.transform.position, at) <= NpcWorker.StoreReach) l.Add(c);
            return l;
        }

        // its own chest short of its house's fuel: fetched from the storeroom (else from another chest of the base), into it
        private static NpcWorker.Errand Refill(NpcWorker w, HashSet<string> fuels)
        {
            Container own = w.Chest;
            if (own == null || fuels.Count == 0) return null;
            ZDOID ownId = NpcKit.IdOf(own);
            if (ownId.IsNone() || !Reserve.IsFree(ownId, w.Me)) return null;
            List<Container> chests = w.InBase<Container>(), store = Store(w, chests);
            HashSet<ZDOID> work = NpcWorker.WorkChests();
            foreach (string name in fuels)
            {
                int have = own.GetInventory().CountItems(name) + w.Carrying(name); // (what it carries already counts: not fetched again)
                if (have >= Low) continue;
                Container from = Nearest(store, own, name, w, work) ?? Nearest(chests, own, name, w, work);
                if (from == null) continue;
                string what = name; int want = Fill - have; Container src = from;
                return new NpcWorker.Errand { Target = ownId, At = src.transform.position, Near = 2f, Word = store.Contains(src) ? Lang.K("бере пальне зі складу") : Lang.K("шукає пальне по базі"),
                    Use = (wk, e) => { wk.TakeFrom(src, what, want); return true; } }; // (carried: to its own chest next — its carry goes there)
            }
            return null;
        }

        // the nearest chest of these with some of it (not its own, not a worker's, not private, not open)
        private static Container Nearest(List<Container> chests, Container own, string name, NpcWorker w, HashSet<ZDOID> work)
        {
            Container best = null; float bd = float.MaxValue;
            foreach (Container c in chests)
            {
                if (c == own || !Open(c, w.Actor) || work.Contains(NpcKit.IdOf(c)) || c.GetInventory().CountItems(name) == 0) continue;
                float d = (c.transform.position - w.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        // things put away (its inbox, its own chest — all but its house's fuel; never the untouchable ones): what it carries
        // first (nowhere for it now: back where it came from), then the inbox, then its own chest. What has no place is
        // left where it lies and named (its menu, the inbox's hover) — nothing is dropped, nothing changed
        private static NpcWorker.Errand PutAway(NpcWorker w, HashSet<string> fuels)
        {
            Container own = w.Chest, inbox = w.Inbox;
            List<Container> chests = w.InBase<Container>(), store = Store(w, chests);
            HashSet<string> nowhere = new HashSet<string>();
            List<Container> inStore = null, inBase = null; // (the chests a thing may go to: looked over once, when there is a thing)
            foreach (ItemDrop.ItemData it in w.Carried())
            {
                string name = it.m_shared.m_name;
                if (fuels.Contains(name) || NpcWorker.Untouchable(it)) continue; // (its fuel: to its own chest as ever)
                Container dst = Spot(w, it, store, chests, own, inbox, ref inStore, ref inBase);
                if (dst == null) { nowhere.Add(name); dst = inbox != null && inbox.GetInventory().CanAddItem(it, 1) ? inbox : own != null && own.GetInventory().CanAddItem(it, 1) ? own : null; } // (no place now: back where it came from — if it fits there; else it keeps it, named)
                if (dst == null) continue;
                return PutErrand(w, dst, name, null, 0, store.Contains(dst) ? Lang.K("розкладає на складі") : dst == inbox ? Lang.K("повертає речі на розбір") : Lang.K("несе речі на місце"));
            }
            if (inbox != null && Open(inbox, w.Actor))
                foreach (ItemDrop.ItemData it in inbox.GetInventory().GetAllItems())
                {
                    if (NpcWorker.Untouchable(it)) continue;
                    string name = it.m_shared.m_name;
                    if (nowhere.Contains(name)) continue;
                    if (Spot(w, it, store, chests, own, inbox, ref inStore, ref inBase) == null) { nowhere.Add(name); continue; }
                    ZDOID inId = NpcKit.IdOf(inbox);
                    if (inId.IsNone() || !Reserve.IsFree(inId, w.Me)) break;
                    string what = name; Container src = inbox; int n = inbox.GetInventory().CountItems(name);
                    return new NpcWorker.Errand { Target = inId, At = src.transform.position, Near = 2f, Word = Lang.K("забирає речі на розбір"),
                        Use = (wk, e) => { wk.TakeFrom(src, what, n); return true; } }; // (carried: put away next)
                }
            if (own != null)
                foreach (ItemDrop.ItemData it in own.GetInventory().GetAllItems())
                {
                    string name = it.m_shared.m_name;
                    if (fuels.Contains(name) || nowhere.Contains(name) || NpcWorker.Untouchable(it)) continue;
                    Container dst = Spot(w, it, store, chests, own, inbox, ref inStore, ref inBase);
                    if (dst == null) { nowhere.Add(name); continue; }
                    NpcWorker.Errand e = PutErrand(w, dst, name, name, own.GetInventory().CountItems(name), store.Contains(dst) ? Lang.K("розкладає на складі") : Lang.K("несе речі на місце"));
                    if (e != null) return e;
                }
            w.SetNowhere(nowhere);
            return null;
        }

        // to that chest with it (take: from its own chest first), all it has of it in
        private static NpcWorker.Errand PutErrand(NpcWorker w, Container dst, string what, string take, int count, string word)
        {
            ZDOID id = NpcKit.IdOf(dst);
            if (id.IsNone() || !Reserve.IsFree(id, w.Me)) return null;
            Container to = dst;
            return new NpcWorker.Errand { Target = id, At = to.transform.position, Near = 2f, Take = take, Count = count, Word = word,
                Use = (wk, e) => { wk.PutInto(to, what); return true; } };
        }

        // where a thing goes: the storeroom where the same lies › anywhere in the storeroom with room › a chest of the base
        // where the same lies, not in a house of another player (NpcPrivate); never its own chest, its inbox, a worker's
        // chest, a private or open one
        private static Container Spot(NpcWorker w, ItemDrop.ItemData it, List<Container> store, List<Container> chests, Container own, Container inbox, ref List<Container> inStore, ref List<Container> inBase)
        {
            if (inStore == null) { inStore = Free(w, store, own, inbox, false); inBase = Free(w, chests, own, inbox, true); } // (the storeroom is yours to point at, wherever; the base's chests — not in another player's house)
            Vector3 from = own != null ? own.transform.position : w.Pos;
            return Room(inStore, it, true, from) ?? Room(inStore, it, false, from) ?? Room(inBase, it, true, from);
        }

        // of these, the chests a thing may be put into: asked once for all its things, not again for each
        private static List<Container> Free(NpcWorker w, List<Container> chests, Container own, Container inbox, bool notStrangers)
        {
            HashSet<ZDOID> work = NpcWorker.WorkChests();
            List<Container> l = new List<Container>();
            foreach (Container c in chests)
            {
                if (c == own || c == inbox || !Open(c, w.Actor) || work.Contains(NpcKit.IdOf(c))) continue;
                if (notStrangers && NpcPrivate.Off(w.Actor, w.Base, c.transform.position)) continue; // (a house of another player: not ours to fill)
                l.Add(c);
            }
            return l;
        }

        private static Container Room(List<Container> chests, ItemDrop.ItemData it, bool same, Vector3 from)
        {
            Container best = null; float bd = float.MaxValue;
            foreach (Container c in chests)
            {
                Inventory inv = c.GetInventory();
                if ((same && inv.CountItems(it.m_shared.m_name) == 0) || !inv.CanAddItem(it, 1)) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        internal override string Assign(NpcActor a, Vector3 at)
        {
            string s = NpcWorker.SetJob(a, Id, NpcMenu.By);
            if (a.Worker == null || a.Worker.JobId != Id) return s; // (could not take the job)
            return NpcWorker.SetHouse(a, at);
        }

        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            NpcHouse h = w.House;
            if (h == null || !h.Good) { w.Doing = h == null ? Lang.K("чекає, який дім доглядати") : h.Describe(); return null; }
            bool chest = w.Chest != null; // (without one: only what needs nothing — mending, the doors)
            List<Fireplace> fires = new List<Fireplace>();
            if (chest) foreach (Fireplace f in w.InBase<Fireplace>()) if (h.Near(f.transform.position)) fires.Add(f);
            // 1) a fire about to go out — then mending — then fuel, the floor, the doors
            NpcWorker.Errand e = chest ? Stoker.Fuel(w, fires) : null;
            if (e != null && e.Urgent) return e;
            NpcWorker.Errand mend = Mend(w, h);
            if (mend != null) return mend;
            if (e != null) return e;
            if (chest)
            {
                List<ItemDrop> drops = new List<ItemDrop>();
                foreach (ItemDrop d in w.InBase<ItemDrop>()) if (h.Inside(d.transform.position)) drops.Add(d);
                e = Cleaner.Tidy(w, drops);
                if (e != null) return e;
            }
            // 2) its own chest topped up with its house's fuel (from the storeroom), things put away (its inbox, its own chest), the doors
            HashSet<string> fuels = new HashSet<string>(); Fuels(w, fuels);
            if (chest) { e = Refill(w, fuels); if (e != null) return e; }
            e = PutAway(w, fuels);
            if (e != null) return e;
            return Shut(w, h);
        }

        // a damaged wall of its house: the hammer, a few blows, whole (as a player mends: free)
        private static NpcWorker.Errand Mend(NpcWorker w, NpcHouse h)
        {
            foreach (WearNTear wnt in NpcChores.Broken(w.Base)) // (the base's damaged pieces, looked for once in half a minute: NpcChores)
            {
                Piece pc = wnt != null ? wnt.GetComponent<Piece>() : null;
                if (pc == null || wnt.GetHealthPercentage() >= 0.9f || !h.Covers(pc.transform.position)) continue; // (all of its house: walls, roof to the ridge, eaves — mended from below)
                ZDOID id = NpcKit.IdOf(pc);
                if (id.IsNone() || !Reserve.IsFree(id, w.Me)) continue;
                int blows = 0;
                NpcActor a = w.Actor;
                return new NpcWorker.Errand
                {
                    Target = id, At = pc.transform.position, Near = 2.2f, Word = Lang.K("лагодить дім"), Move = "",
                    Use = (wk, er) =>
                    {
                        if (wnt == null) { NpcChores.Hammer(a, false); return true; }
                        if (blows == 0) NpcChores.Hammer(a, true);
                        Humanoid body = a.GetComponent<Humanoid>();
                        ItemDrop.ItemData inHand = body != null ? body.GetCurrentWeapon() : null;
                        ZSyncAnimation za = a.GetComponent<ZSyncAnimation>();
                        if (za != null && inHand != null && !string.IsNullOrEmpty(inHand.m_shared.m_attack.m_attackAnimation)) za.SetTrigger(inHand.m_shared.m_attack.m_attackAnimation);
                        if (++blows < 4) return false; // (a blow a step)
                        if (wnt.Repair()) pc.m_placeEffect.Create(pc.transform.position, pc.transform.rotation);
                        NpcChores.Hammer(a, false);
                        return true;
                    }
                };
            }
            return null;
        }

        // a door of its house left open, nobody by it: shut
        private static NpcWorker.Errand Shut(NpcWorker w, NpcHouse h)
        {
            foreach (Door d in NpcDoors.Around(h.At))
            {
                if (d == null || NpcDoors.Closed(d) || !h.Near(d.transform.position)) continue;
                if (Player.GetClosestPlayer(d.transform.position, 6f) != null) continue; // (someone by it: left as it is)
                ZDOID id = NpcKit.IdOf(d);
                if (id.IsNone() || !Reserve.IsFree(id, w.Me)) continue;
                return new NpcWorker.Errand { Target = id, At = d.transform.position, Near = 1.8f, Word = Lang.K("зачиняє двері"), Move = "", Use = (wk, er) => { if (d != null) NpcDoors.Shut(d); return true; } };
            }
            return null;
        }
    }

    // a cook: its kitchen is the cooking tools round its pantry, its work the dishes they make of what lies in its ingredients' chest (or its pantry,
    // with no second chest) — all of it NpcKitchen: what next, the tools, the dishes, what it lacks. Here only what a job says of itself
    internal sealed class Cook : Job
    {
        internal Cook() { Id = "cook"; Word = Lang.K("кухар"); Icon = "n-cook"; HasSupply = true;
            Desc = Lang.K("Кухня біля комори: решітки, печі, казан та інше знаряддя. Бере зі скрині інгредієнтів (або з комори), готує все, що з цього виходить, знімає готове, поки не пригоріло, тримає вогонь і несе страви в комору."); }

        internal override void Fuels(NpcWorker w, HashSet<string> into) { foreach (CookingStation s in w.InBase<CookingStation>()) if (s.m_useFuel) Add(into, s.m_fuelItem); }

        internal override bool Keeps(ItemDrop.ItemData it) { return it != null && NpcKitchen.IsDish(it.m_shared.m_name); } // (its dishes to the pantry; a dough, a leftover, an overcooked coal back to its ingredients)

        internal override void Lacks(NpcWorker w, List<string> into) { NpcKitchen.Lacks(w, into); }

        internal override NpcWorker.Errand Find(NpcWorker w) { return NpcKitchen.Next(w); }
    }

    // beehives (honey to the chest) and fermenting barrels (the ready mead tapped, an empty one filled from the chest)
    internal sealed class Keeper : Job
    {
        internal Keeper() { Id = "keeper"; Word = Lang.K("пасічник-винар"); Icon = "n-keeper"; Desc = Lang.K("Вулики: мед у ящик. Бочки: зливає готову медовуху в ящик, порожні заливає основою з ящика."); }

        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            foreach (Beehive b in w.InBase<Beehive>())
            {
                ZNetView nv = b.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || b.m_honeyItem == null) continue;
                ZDOID id = nv.GetZDO().m_uid;
                if (!Reserve.IsFree(id, w.Me) || nv.GetZDO().GetInt(ZDOVars.s_level) < Mathf.Max(1, b.m_maxHoney / 2)) continue; // (half full at least: fewer trips)
                bool asked = false;
                return new NpcWorker.Errand
                {
                    Target = id, At = b.m_spawnPoint != null ? b.m_spawnPoint.position : b.transform.position, Near = 1.6f, Collect = new HashSet<string> { b.m_honeyItem.name }, Word = Lang.K("збирає мед"),
                    Use = (wk, e) => { if (!asked) { asked = true; nv.InvokeRPC("RPC_Extract"); } return true; }
                };
            }
            foreach (Fermenter f in w.InBase<Fermenter>())
            {
                ZNetView nv = f.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                ZDOID id = nv.GetZDO().m_uid;
                if (!Reserve.IsFree(id, w.Me)) continue;
                ZDO z = nv.GetZDO();
                int content = z.GetInt(ZDOVars.s_content);
                Vector3 outAt = f.m_outputPoint != null ? f.m_outputPoint.position : f.transform.position;
                if (content != 0)
                {
                    long start = z.GetLong(ZDOVars.s_startTime, 0L);
                    if (start == 0L || ZNet.instance == null || (ZNet.instance.GetTime() - new DateTime(start)).TotalSeconds <= f.m_fermentationDuration) continue; // (still working)
                    HashSet<string> made = new HashSet<string>();
                    foreach (Fermenter.ItemConversion c in f.m_conversion) if (c != null && c.m_to != null) made.Add(c.m_to.name);
                    bool asked = false; int wait = 4; // (the game pours it a moment after the tap)
                    return new NpcWorker.Errand
                    {
                        Target = id, At = outAt, Near = 1.6f, Collect = made, Word = Lang.K("зливає медовуху"),
                        Use = (wk, e) => { if (!asked) { asked = true; nv.InvokeRPC("RPC_Tap"); } return --wait <= 0; }
                    };
                }
                foreach (Fermenter.ItemConversion c in f.m_conversion)
                {
                    if (c == null || c.m_from == null) continue;
                    string bas = c.m_from.m_itemData.m_shared.m_name; int hash = c.m_from.name.GetStableHashCode();
                    if (w.Carrying(bas) + w.InChest(bas) <= 0) continue;
                    return new NpcWorker.Errand
                    {
                        Target = id, At = f.transform.position, Near = 1.8f, Take = bas, Count = 1, Word = Lang.K("заливає бочку"),
                        Use = (wk, e) => { if (nv.IsValid() && z.GetInt(ZDOVars.s_content) == 0 && wk.Spend(bas)) nv.InvokeRPC("RPC_AddItem", hash, false); return true; }
                    };
                }
            }
            return null;
        }
    }

    // a fighter walking the edge of its base, looking out at each stop; its own way in a fight does the rest (day and night)
    internal sealed class Guard : Job
    {
        // one who stands guard: a guard by its job, or a fighter by its template (its map mark, its armour and health on hover)
        // hired to keep watch (the user 2026-11-04: Knud went off mending, «he should guard»): it never mends. With a post (OnWatch) it does not
        // leave it for its ease either, only for what it must (a meal, its pay, a rest worn out), an order, a fight, a scene. With no post it
        // walks the base's edge, and now and then does what anyone does («if he patrols he could»). A fighter with no such job (Is below): as anyone
        internal static bool Hired(NpcActor a) { return a != null && a.Worker != null && a.Worker.JobId == "guard"; }
        internal static bool OnWatch(NpcActor a) { Vector3 post; return Hired(a) && a.Worker.Post(out post); }
        internal static bool Is(NpcActor a) { return a != null && ((a.Worker != null && a.Worker.JobId == "guard") || (a.Def != null && (a.Def.Fight == "defend" || a.Def.Fight == "hunt"))); }

        // a guard's health and armour under its name, small and coloured by how hurt it is
        internal static string Line(NpcActor a)
        {
            if (!Is(a)) return "";
            Character c = a.GetComponent<Character>();
            ZDO z = a.Zdo;
            if (c == null || z == null) return "";
            float hp = c.GetHealth(), max = Mathf.Max(1f, c.GetMaxHealth());
            string hpCol = hp / max < 0.35f ? "#e8705a" : hp / max < 0.7f ? "#e3c060" : "#9fd27a";
            return "\n<size=85%><color=" + hpCol + ">" + Lang.T("Здоров'я") + " " + Mathf.CeilToInt(hp) + "/" + Mathf.RoundToInt(max) + "</color>   <color=#a9c3dd>" + Lang.T("Броня") + " " + z.GetInt(NpcActor.KArmor, 0) + "</color></size>";
        }

        // where it keeps watch with a post: by its post (null: no post — not its word)
        internal static bool? Watch(NpcActor a, Vector3 at)
        {
            NpcWorker w = a.Worker;
            Vector3 post;
            if (!NpcResident.Is(a) || w == null || w.JobId != "guard" || !w.Post(out post)) return null;
            if (Utils.DistanceXZ(at, post) <= NpcWorker.PostReach) return true;
            return NpcMind.LayerNow(a) == NpcMind.Layer.Job ? false : (bool?)null; // (on its watch: its post only; off it — a meal, its sleep — its base as any resident's: 2026-11-08 Knud, starving, walked past a greydwarf in his own base)
        }

        // on its watch (its job's hours): it looks out as far as it sees — a born guard further
        internal static float LookOut(NpcActor a)
        {
            return a.Worker != null && a.Worker.JobId == "guard" && a.Mind != null && a.Mind.NowLayer == NpcMind.Layer.Job ? a.Mind.m_viewRange * NpcKnack.Of(a, "guard") : 0f;
        }

        private const int RoundAsks = 3;        // ways asked for one round's spot at most (none fits: it stands at its post)
        private const float RoundDetour = 1.8f; // a spot is near when its way on foot is at most this × the straight line (+3 m)

        internal Guard() { Move = ""; Id = "guard"; Word = Lang.K("вартовий"); Icon = "n-guard"; Desc = Lang.K("Стоїть на посту (кнопка «Пост») і обходить довкола, без посту — межу бази; б’є лише ворогів біля посту чи в базі, мирних звірів не чіпає (лише для бійців). Вартує вдень і вночі."); AllDay = true; NeedsChest = false; HasChest = false; }

        internal override string Can(NpcActor a) { return (a.Mind != null && (a.Mind.Style == NpcMind.Fight.Defend || a.Mind.Style == NpcMind.Fight.Hunt)) || (a.Def != null && a.Def.Generated) ? null : Lang.T("Він не боєць — вартувати не візьметься."); } // (a settler: its nature's word, NpcNature.WontTake)

        // with a post: stands on it, now and then a round near it (inside its base); without one: the base's edge
        internal override NpcWorker.Errand Find(NpcWorker w)
        {
            Vector3 post;
            if (w.Post(out post))
            {
                if (UnityEngine.Random.value < 0.5f) { int wait = UnityEngine.Random.Range(12, 25); return new NpcWorker.Errand { At = post, Near = 1f, Word = Lang.K("стоїть на посту"), Use = (wk, e) => --wait <= 0 }; }
                // a spot round it near on foot, not only as the crow flies: one behind a low wall was 5 m off and 50 m round
                // (2026-11-03: a guard went round and round its post); on the floor there, not the land under a paved yard
                Vector3 round = post;
                int asked = 0;
                for (int i = 0; i < 10 && asked < RoundAsks; i++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(3f, NpcWorker.PostRound);
                    Vector3 p = NpcKit.Floor(post + new Vector3(r.x, 0f, r.y));
                    if (!w.Base.Inside(p)) continue;
                    asked++;
                    float straight = Utils.DistanceXZ(post, p);
                    if (NpcKit.OnFoot(post, p, straight * RoundDetour + 3f) > straight * RoundDetour + 3f) continue; // heavy: a way asked, at most RoundAsks a round (one each 10–20 s a guard)
                    round = p; break;
                }
                int stop = UnityEngine.Random.Range(4, 9);
                return new NpcWorker.Errand { At = round, Near = 1.2f, Word = Lang.K("обходить пост"), Use = (wk, e) => --stop <= 0 };
            }
            bool all = NpcWorker.Patrols(w.Actor);
            Vector3 at = all ? w.Base.Spot() : w.Base.EdgeSpot(); // (the whole base, or its edge)
            int look = UnityEngine.Random.Range(4, 9); // (a few breaths at each stop)
            return new NpcWorker.Errand { At = at, Near = 1.5f, Word = all ? Lang.K("обходить базу") : Lang.K("обходить межу"), Use = (wk, e) => --look <= 0 };
        }


    }

    // a servant's inbox chest says so on hover, and what it found no place for
    [HarmonyLib.HarmonyPatch(typeof(Container), "GetHoverText")]
    internal static class NpcInboxHoverPatch
    {
        private static void Postfix(Container __instance, ref string __result)
        {
            if (NpcActor.Live.Count == 0 || string.IsNullOrEmpty(__result)) return;
            ZDOID id = NpcKit.IdOf(__instance);
            if (id.IsNone()) return;
            foreach (NpcActor a in NpcActor.Live)
            {
                if (a == null || a.Worker == null || a.Worker.JobId != "servant" || NpcLocker.Ref(a.Zdo, NpcWorker.KInbox) != id) continue;
                string n = NpcWorker.Nowhere(a);
                __result += "\n<color=#e0b860>" + Lang.F("Розбір: {0}", a.Name) + "</color>" + (n.Length > 0 ? "\n<size=80%><color=#e8705a>" + Lang.F("Не знайшов місця: {0}", n) + "</color></size>" : "");
                return;
            }
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens' own place: the shoulders, not a slot of the game. Hugin always sits on the right shoulder and
    // Munin on the left, as in the myths; each has its own place, so both are worn at once and the belt stays free.
    // A raven on the shoulder is a raven in the bag with the game's own "worn" mark (the frame in the bag and on the
    // hotbar; kept when the game saves, and left alone by mods that sort or stack the bag), but in no slot of the game.
    // Right click or a hotbar key puts it on and takes it off. Only the ravens this game sat down itself count (Seated):
    // a copy brought in with the mark (from a chest, dropped and picked up) loses it. A raven that leaves the bag
    // (dropped, put in a chest) is taken off. Dying takes them off quietly, as the game does with worn gear.
    //
    // A raven taken off the shoulder takes offence: for a while no raven of that kind (that one or another)
    // sits on your shoulder again, and one cannot be swapped for another while it sits there.
    // Each kind on its own: taking Munin off does not stop Hugin. The wait is kept in the character
    // (world time), so leaving the game does not end it. Dying and loading the character do not count
    // as taking it off. "raven ready" ends the waits.
    internal static class RavenSeat
    {
        internal static bool Enabled = true;
        internal static float Minutes = 30f;
        private static string Key(bool munin) { return munin ? "RavenWatch_seatMunin" : "RavenWatch_seatHugin"; }
        private const string OldWornKey = "RavenWatch_worn"; // the old second belt's mark (Path of Valheiman): it was on
        internal static bool Restoring; // the game puts the saved gear back on while loading the character
        internal static bool Crumbling; // an old copy of a raven brought back falls apart: nobody took it off, no offence

        // the ravens sitting on a shoulder now, and on whom (a character loaded again is a new player with new items)
        private static readonly Dictionary<ItemDrop.ItemData, Player> Seated = new Dictionary<ItemDrop.ItemData, Player>();

        // With SeneaL UI's shoulder cells (SeneaLShoulders) a raven on the shoulder is not in the bag at all: it is
        // kept here, and in the character's own data when the game saves (ShelfKey). Without them, or once SeneaL UI's
        // inventory is gone or switched off, every raven kept here goes back into the bag, still on the shoulder.
        private static readonly HashSet<ItemDrop.ItemData> Shelf = new HashSet<ItemDrop.ItemData>();
        private const string ShelfKey = "RavenWatch_shoulderItems";
        private static float _roomToldAt = -999f;
        private static readonly List<ItemDrop.ItemData> Gone = new List<ItemDrop.ItemData>(4), Move = new List<ItemDrop.ItemData>(4); // Tick's own, reused

        internal static bool IsSeated(ItemDrop.ItemData item)
        {
            return item != null && item.m_equipped && Seated.ContainsKey(item);
        }

        internal static bool OnShelf(ItemDrop.ItemData item) { return item != null && Shelf.Contains(item); }

        // the ravens of this player kept off the bag (on the shoulder)
        internal static List<ItemDrop.ItemData> Shelved(Player p)
        {
            List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(2);
            foreach (ItemDrop.ItemData item in Shelf) if (item != null && Owner(item) == p) list.Add(item);
            return list;
        }

        private static Player Owner(ItemDrop.ItemData item)
        {
            Player p;
            return Seated.TryGetValue(item, out p) ? p : null;
        }

        // the ravens on this player's shoulders: in the bag with the mark, or kept off the bag
        internal static void SeatedOf(Player p, out ItemDrop.ItemData hugin, out ItemDrop.ItemData munin)
        {
            hugin = null; munin = null;
            if (p == null) return;
            Inventory bag = p.GetInventory();
            foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated)
            {
                bool m;
                if (kv.Value != p || !kv.Key.m_equipped || !RavenItem.IsRaven(kv.Key, out m)) continue;
                if (!Shelf.Contains(kv.Key) && !bag.ContainsItem(kv.Key)) continue;
                if (m) munin = kv.Key; else hugin = kv.Key;
            }
        }

        // out of the bag onto the shoulder cell
        private static void ToShelf(Player p, ItemDrop.ItemData item)
        {
            if (Shelf.Contains(item)) return;
            Shelf.Add(item);
            p.GetInventory().RemoveItem(item);
        }

        // from the shoulder cell back into the bag, still worn; false when the bag is full
        private static bool FromShelf(Player p, ItemDrop.ItemData item)
        {
            if (!Shelf.Contains(item)) return true;
            if (!p.GetInventory().AddItem(item)) return false;
            Shelf.Remove(item);
            return true;
        }

        private static void NoRoom(Player p, bool munin)
        {
            if (Time.time - _roomToldAt < 60f) return;
            _roomToldAt = Time.time;
            RavenMsg.Center(p, Lang.F("У сумці нема місця для {0}.\nЗвільни клітинку.", Plugin.RavenNameGen(munin)));
        }

        // the game saves the character: the ravens kept off the bag go into its own data
        internal static void Saving(Player p)
        {
            if (p == null) return;
            List<ItemDrop.ItemData> list = Shelved(p);
            if (list.Count == 0) { p.m_customData.Remove(ShelfKey); return; }
            try
            {
                Inventory tmp = new Inventory(ShelfKey, null, 4, 1);
                for (int i = 0; i < list.Count && i < 4; i++) tmp.AddItem(list[i], new Vector2i(i, 0));
                ZPackage pkg = new ZPackage();
                tmp.Save(pkg);
                p.m_customData[ShelfKey] = pkg.GetBase64();
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder ravens were not saved: " + e.Message); }
        }

        // and back when it is loaded: on the shoulder again (into the bag at the next check if SeneaL UI is not there)
        private static void ReadShelf(Player p)
        {
            string raw;
            if (!p.m_customData.TryGetValue(ShelfKey, out raw)) return;
            p.m_customData.Remove(ShelfKey);
            Inventory tmp = new Inventory(ShelfKey, null, 4, 1);
            try { tmp.Load(new ZPackage(raw)); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shoulder ravens could not be read: " + e.Message); return; }
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(tmp.GetAllItems()))
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin)) continue;
                Shelf.Add(item);
                if (Sit(p, item, true)) continue;
                // that shoulder is taken already (should not happen): into the bag, or on the ground beside you
                Shelf.Remove(item);
                item.m_equipped = false;
                if (!p.GetInventory().AddItem(item)) ItemDrop.DropItem(item, 1, p.transform.position + Vector3.up, Quaternion.identity);
            }
        }

        private static double Until(Player p, bool munin)
        {
            string raw;
            double v;
            if (p != null && p.m_customData.TryGetValue(Key(munin), out raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return 0;
        }

        internal static void Reset(Player p)
        {
            if (p == null) return;
            p.m_customData.Remove(Key(false));
            p.m_customData.Remove(Key(true));
        }

        internal static string Left(Player p, bool munin)
        {
            float left = WorldTime.Sane(Until(p, munin) - WorldTime.Now);
            return left > 0 ? RavenAbilities.Span(left) : "";
        }

        // may this raven go on the shoulder now? Says why not.
        internal static bool MaySit(Player p, ItemDrop.ItemData item, bool munin)
        {
            if (p == null || p != Player.m_localPlayer || IsSeated(item)) return true;
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m);
            if ((munin ? m : h) != null)
            {
                RavenMsg.Center(p, munin
                    ? Lang.T("На лівому плечі вже сидить Мунін.\nДругого він заклює.")
                    : Lang.T("На правому плечі вже сидить Хугін.\nДвом там тісно."));
                return false;
            }
            if (!Enabled) return true;
            string left = Left(p, munin);
            if (left.Length == 0) return true;
            RavenMsg.Center(p, munin
                ? Lang.F("Мунін ще ображений, дебіле.\nЧекай {0}.", left)
                : Lang.F("Хугін ще сумує.\nСяде через {0}.", left));
            return false;
        }

        // onto its own shoulder; quiet = put back by the game (loading), no checks and no words
        internal static bool Sit(Player p, ItemDrop.ItemData item, bool quiet)
        {
            bool munin;
            if (p == null || !RavenItem.IsRaven(item, out munin)) return false;
            if (IsSeated(item)) return true;
            if (!quiet && !MaySit(p, item, munin)) return false;
            foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated) // one shoulder, one raven: a second one stays in the bag
            {
                bool m;
                if (kv.Value == p && kv.Key != item && kv.Key.m_equipped && RavenItem.IsRaven(kv.Key, out m) && m == munin
                    && (Shelf.Contains(kv.Key) || p.GetInventory().ContainsItem(kv.Key)))
                {
                    item.m_equipped = false;
                    return false;
                }
            }
            item.m_equipped = true;
            Seated[item] = p;
            if (!quiet) RavenJournal.Note(p, item, "on the shoulder"); // (restored on loading: not news)
            if (!quiet && p == Player.m_localPlayer && SeneaLShoulders.Holds && p.GetInventory().ContainsItem(item)) ToShelf(p, item);
            RavenItem.ForgetWorn();
            return true;
        }

        // off the shoulder, back into the bag; offence = taken off by the player. False when the bag has no room for it.
        internal static bool TakeOff(Player p, ItemDrop.ItemData item, bool offence)
        {
            if (item == null) return false;
            // «Ревнивий», sulking: you cannot take it off (any way you try: the bag, the ring, a key); death still does
            bool mun;
            if (offence && !Crumbling && p != null && p == Player.m_localPlayer && !p.IsDead() && RavenItem.IsRaven(item, out mun) && ReferenceEquals(On(p, mun), item))
            {
                string sulk = Quirks.Sulk(p, mun);
                if (sulk != null) { RavenMsg.Center(p, Lang.F("{0}: і з плеча не злізе.", sulk)); return false; }
            }
            // asked before anything changes: the shoulder cell's way below clears the mark first (that hid every
            // take-off from the shoulder cells of SeneaL UI: no offence, no wait, no line in the journal)
            bool was = IsSeated(item);
            if (Shelf.Contains(item))
            {
                item.m_equipped = false;
                if (p == null || !FromShelf(p, item))
                {
                    item.m_equipped = true;
                    bool munin;
                    if (p != null && !p.IsDead() && RavenItem.IsRaven(item, out munin)) NoRoom(p, munin); // (at the death it just stays)
                    RavenJournal.Note(p, item, "take-off failed: no room in the bag");
                    return false;
                }
            }
            Seated.Remove(item);
            item.m_equipped = false;
            OffPerch(p, item);
            RavenItem.ForgetWorn();
            if (was) RavenJournal.Note(p, item, offence ? "off the shoulder into the bag" : "off the shoulder into the bag (no offence)");
            if (was && offence) { TakenOff(p, item); Nicks.OnTakenOff(p, item); }
            return true;
        }

        // a raven sitting on a perch when it is taken off: it leaves the perch (the seat let go, the stay counted)
        private static void OffPerch(Player p, ItemDrop.ItemData item)
        {
            bool munin;
            if (p == null || p != Player.m_localPlayer || !Perches.On(item) || !RavenItem.IsRaven(item, out munin)) return;
            try { Perches.CallFar(p, item, munin); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven off its perch: " + e.Message); }
        }

        // gone for good (an old copy crumbled to feathers): nothing comes back to the bag
        internal static void Forget(ItemDrop.ItemData item)
        {
            if (item == null) return;
            RavenJournal.Note(Player.m_localPlayer, item, "forgotten (an old copy crumbled)");
            Seated.Remove(item);
            Shelf.Remove(item);
            item.m_equipped = false;
            RavenItem.ForgetWorn();
        }

        // right click in the bag, its hotbar key, a click on its SeneaL shoulder cell: as with any gear, the hero takes
        // a moment for it — the game's own "equipping" move and bar (its action queue), then the game puts it on or
        // takes it off (EquipItem / UnequipItem, which bring it to its shoulder: the patches below). Pressed again
        // meanwhile: called off, as with gear. What would refuse anyway refuses at once, before the move.
        internal static float EquipSeconds = 1f; // config "Shoulder on/off seconds"; 0: at once
        private static readonly System.Reflection.MethodInfo QueueOn = AccessTools.Method(typeof(Player), "QueueEquipAction");
        private static readonly System.Reflection.MethodInfo QueueOff = AccessTools.Method(typeof(Player), "QueueUnequipAction");

        internal static void Toggle(Player p, ItemDrop.ItemData item)
        {
            bool munin;
            if (p == null || p != Player.m_localPlayer || !RavenItem.IsRaven(item, out munin)) return;
            if (IsSeated(item))
            {
                string sulk = ReferenceEquals(On(p, munin), item) ? Quirks.Sulk(p, munin) : null;
                if (sulk != null) { RavenMsg.Center(p, Lang.F("{0}: і з плеча не злізе.", sulk)); return; }
                if (!Queue(p, item, false)) TakeOff(p, item, true);
            }
            else
            {
                if (!MaySit(p, item, munin)) return;
                if (!Queue(p, item, true)) Sit(p, item, false);
            }
        }

        private static bool Queue(Player p, ItemDrop.ItemData item, bool on)
        {
            System.Reflection.MethodInfo m = on ? QueueOn : QueueOff;
            if (EquipSeconds <= 0f || m == null || p.InAttack()) return false;
            item.m_shared.m_equipDuration = EquipSeconds; // (the ravens' shared data: one setting for all of them)
            try { m.Invoke(p, new object[] { item }); return true; }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven equip move: " + e.Message); return false; }
        }

        // the raven of that kind on this player's shoulder, or null
        internal static ItemDrop.ItemData On(Player p, bool munin)
        {
            ItemDrop.ItemData h, m;
            RavenItem.Worn(p, out h, out m); // SeatedOf, kept for the frame
            return munin ? m : h;
        }

        // the same with the move (the empty SeneaL shoulder cell clicked)
        internal static void ToggleFromBag(Player p, bool munin)
        {
            if (p == null) return;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
            {
                bool m;
                if (!RavenItem.IsRaven(item, out m) || m != munin || IsSeated(item)) continue;
                Toggle(p, item);
                return;
            }
            RavenMsg.Center(p, munin ? Lang.T("У сумці нема Муніна.") : Lang.T("У сумці нема Хугіна."));
        }

        // twice a second: a raven that left the bag is off the shoulder; one that came in with the mark is not on it
        internal static void Tick(Player p)
        {
            if (p == null) return;
            Inventory bag = p.GetInventory();
            bool changed = false;
            Gone.Clear();
            foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated)
                if (kv.Value != p || (!Shelf.Contains(kv.Key) && !bag.ContainsItem(kv.Key))) Gone.Add(kv.Key);
            if (Gone.Count > 0)
                foreach (ItemDrop.ItemData item in Gone)
                {
                    Player owner = Seated[item];
                    Seated.Remove(item);
                    Shelf.Remove(item);
                    changed = true;
                    if (owner != p) continue; // an earlier life of this character, or the menu's picture of it: let go quietly
                    item.m_equipped = false;  // dropped or put away
                    if (p.IsDead()) continue;
                    RavenJournal.Note(p, item, "left the bag while on the shoulder (dropped or put away)");
                    OffPerch(p, item);
                    TakenOff(p, item);
                }
            foreach (ItemDrop.ItemData item in bag.GetAllItems())
            {
                bool munin;
                if (item.m_equipped && !Seated.ContainsKey(item) && RavenItem.IsRaven(item, out munin)) item.m_equipped = false;
            }

            // the shoulder cells: the ravens on the shoulders leave the bag, or come back to it when the cells are gone
            if (!p.IsDead())
            {
                Reseat(p, bag);
                bool holds = SeneaLShoulders.Holds;
                bool back = !holds && SeneaLShoulders.Released; // not a moment of SeneaL UI starting up
                Move.Clear();
                foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated)
                    if (kv.Value == p && kv.Key.m_equipped && (holds ? bag.ContainsItem(kv.Key) : back && Shelf.Contains(kv.Key))) Move.Add(kv.Key);
                foreach (ItemDrop.ItemData item in Move)
                {
                    if (holds) { ToShelf(p, item); continue; }
                    bool munin;
                    if (!FromShelf(p, item) && RavenItem.IsRaven(item, out munin)) NoRoom(p, munin);
                }
                if (Move.Count > 0) changed = true;
            }
            if (changed) RavenItem.ForgetWorn();
            TickDeathWatch(p);
        }

        // after the game put the saved gear back on: a raven the old second belt had lost while loading sits again
        internal static void Loaded(Player p)
        {
            if (p == null) return;
            ReadShelf(p);
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin) || item.m_customData == null || !item.m_customData.Remove(OldWornKey)) continue;
                if (!item.m_equipped) Sit(p, item, true);
            }
        }

        // A raven the death took off the shoulder carries the owner's id; back in that player's bag (the grave looted,
        // by hand, by SeneaL UI or picked up from the ground) it sits on its shoulder again by itself. SeneaL UI's own
        // re-equip never sees the ravens: they leave the shoulders before it marks the worn gear, or sit off the bag.
        private const string ReseatKey = "RavenWatch_reseat";

        // it sat on a shoulder when its owner died (a raven only carried in the bag has no such mark)
        internal static bool WasWorn(ItemDrop.ItemData item) { return item != null && item.m_customData != null && item.m_customData.ContainsKey(ReseatKey); }

        private static void Reseat(Player p, Inventory bag)
        {
            string me = null;
            List<ItemDrop.ItemData> back = null;
            foreach (ItemDrop.ItemData item in bag.GetAllItems())
            {
                string who;
                if (item.m_customData == null || !item.m_customData.TryGetValue(ReseatKey, out who)) continue;
                if (me == null) me = p.GetPlayerID().ToString();
                if (who != me) continue; // somebody else's: it stays in this bag, marked for its own
                if (back == null) back = new List<ItemDrop.ItemData>();
                back.Add(item);
            }
            if (back == null) return;
            List<bool> home = new List<bool>(2);
            foreach (ItemDrop.ItemData item in back)
            {
                if (item.m_customData != null) item.m_customData.Remove(ReseatKey);
                bool munin;
                if (!RavenItem.IsRaven(item, out munin)) continue;
                Trust.BackFromGrave(p, item, munin); // came back for it: a share of the death's loss returns (once, in time)
                if (IsSeated(item) || On(p, munin) != null) continue; // that shoulder is taken: it waits in the bag
                GraveRavens.HandOff(p, item); // the bird seen at the grave flies to the shoulder (before the shoulder draws it)
                if (!Sit(p, item, true)) continue;
                if (Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(munin) + " is back on the shoulder from the grave");
                if (Trust.Active(item) && !Trust.Stranger(item, p)) home.Add(munin);
            }
            // out of the grave: one of them says what it thinks of lying there (Munin, with his temper, more often)
            if (home.Count == 0 || Plugin.Instance == null) return;
            bool speaker = home.Count == 1 ? home[0] : UnityEngine.Random.value < 0.6f;
            string line = Plugin.Instance.ComposeTagged(p, speaker, "grave_back");
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, speaker, line);
        }

        // Out of your own grave straight onto its shoulder cell (SeneaL UI), past the bag: a full bag never keeps your
        // ravens in the grave. One that sat on the shoulder at the death always goes so; one carried in the bag only when
        // the bag has no room, or when dropped on its shoulder cell (wanted: and it may sit, no offence wait). Without the
        // shoulder cells a seated raven lives in the bag, so a full bag still has no room. True: taken out and seated.
        internal static bool FromGrave(Player p, Inventory grave, ItemDrop.ItemData item, bool wanted)
        {
            bool munin;
            if (p == null || p != Player.m_localPlayer || p.IsDead() || grave == null || item == null || !RavenItem.IsRaven(item, out munin)) return false;
            if (!SeneaLShoulders.Holds || On(p, munin) != null || !grave.ContainsItem(item)) return false;
            string who = null;
            bool worn = item.m_customData != null && item.m_customData.TryGetValue(ReseatKey, out who) && who == p.GetPlayerID().ToString();
            if (!worn && !wanted && p.GetInventory().HaveEmptySlot()) return false; // into the bag as ever
            if (!worn && !MaySit(p, item, munin)) return false;         // (says why)
            if (worn) item.m_customData.Remove(ReseatKey);
            GraveRavens.HandOff(p, item); // the bird seen at the grave flies to the shoulder
            item.m_equipped = false;
            Shelf.Add(item);
            if (!Sit(p, item, worn))
            {
                Shelf.Remove(item);
                item.m_equipped = false;
                if (worn) item.m_customData[ReseatKey] = who;
                return false;
            }
            grave.RemoveItem(item);
            if (worn) Trust.BackFromGrave(p, item, munin);
            InvRedraw.Changed(grave);
            RavenItem.ForgetWorn();
            RavenJournal.Note(p, item, "grave: straight onto the shoulder (past the bag)");
            if (Plugin.Instance != null) Plugin.Instance.Log(Plugin.RavenName(munin) + " is back on the shoulder from the grave, past the bag");
            if (worn && Trust.Active(item) && !Trust.Stranger(item, p))
            {
                string line = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, munin, "grave_back") : null;
                if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
            }
            return true;
        }

        // At the death the game puts a raven taken off the shoulder (a Misc thing) into the first free cell of the hotbar.
        // Path of Valheiman's «Bound gear» keeps the hotbar on the body: the raven would then go neither to the grave nor
        // on the shoulder. Into the first free cell under the hotbar instead, from the top (rows that inventory mods add
        // at the bottom, for gear or quick slots, last); none free: it stays where it is
        private static void OffHotbar(Player p, ItemDrop.ItemData item)
        {
            Inventory inv = p != null ? p.GetInventory() : null;
            if (inv == null || item == null || item.m_gridPos.y != 0 || !inv.ContainsItem(item)) return;
            for (int y = 1; y < inv.GetHeight(); y++)
                for (int x = 0; x < inv.GetWidth(); x++)
                    if (inv.GetItemAt(x, y) == null)
                    {
                        item.m_gridPos = new Vector2i(x, y);
                        InvRedraw.Changed(inv);
                        RavenJournal.Note(p, item, "death: out of the hotbar into the bag (so it goes to the grave)");
                        return;
                    }
        }

        // the tombstone takes the bag: the ravens leave the shoulders with the rest of the worn gear
        // (unless this world lets you keep what you wear)
        internal static void Died(Player p)
        {
            if (p == null || p == _diedPlayer) return; // a mod that makes a second grave calls it again: once per death
            _diedPlayer = p;
            Perches.Died(p); // ravens on perches leave them (they go to the grave with the bag)
            Loyal.Forget(p);
            List<ItemDrop.ItemData> mine = new List<ItemDrop.ItemData>();
            foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated) if (kv.Value == p) mine.Add(kv.Key);
            foreach (ItemDrop.ItemData item in mine) Loyal.TakeStayed(item); // the ones on the shoulder cells are not in the bag
            WatchFrom(p);
            if (Loyal.WorldKeeps()) { foreach (ItemDrop.ItemData item in mine) { Loyal.Mark(item); Watch(item, true); } return; } // they stay, and they know they were not in the grave
            foreach (ItemDrop.ItemData item in mine)
            {
                if (Loyal.Stays(p, item)) { RavenJournal.Note(p, item, "death: stays on the shoulder (loyal)"); Watch(item, true); continue; } // its gift: still worn, so the grave leaves it where it sits
                RavenJournal.Note(p, item, "death: off the shoulder with the gear");
                if (item.m_customData != null) item.m_customData[ReseatKey] = p.GetPlayerID().ToString(); // back on the shoulder once you pick it up
                if (TakeOff(p, item, false)) { OffHotbar(p, item); Watch(item, false); continue; }
                if (GraveRow(p, item)) { RavenJournal.Note(p, item, "death: the bag full, into the grave in a row of its own"); Watch(item, false); continue; }
                // a full bag: the raven from the shoulder cell never goes on the ground, it stays where it sits
                // (still worn and on the shelf: saved with the character, back on the shoulder after the respawn)
                if (item.m_customData != null) item.m_customData.Remove(ReseatKey);
                item.m_equipped = true;
                Seated[item] = p;
                Loyal.Mark(item); // it did not lie in the grave
                RavenJournal.Note(p, item, "death: the bag full, stays on the shoulder");
                Watch(item, true);
            }
            RavenItem.ForgetWorn();
        }

        // A full bag at the death: the bag gets one row more for the grave's sake only — the game makes the grave as big as
        // the bag (Inventory.MoveInventoryToGrave) — so a raven from a shoulder cell lies in the grave with the rest
        // instead of staying on you (that would make the loyal gifts pointless). The bag is its own size again once the
        // grave is made (RavenSeatDeathPatch's finalizer). Inventory mods may look for free cells only in their own rows
        // (SeneaL UI: its main rows): then the raven is laid straight into the new row, past the search for a cell.
        private static readonly System.Reflection.FieldInfo InvHeight = AccessTools.Field(typeof(Inventory), "m_height");
        private static readonly System.Reflection.FieldInfo InvList = AccessTools.Field(typeof(Inventory), "m_inventory");
        private static Inventory _grown;
        private static Player _grownFor;
        private static int _grownFrom = -1, _rowX;

        private static bool GraveRow(Player p, ItemDrop.ItemData item)
        {
            Inventory bag = p.GetInventory();
            if (InvHeight == null || bag == null) return false;
            if (_grown != bag) { _grown = bag; _grownFor = p; _grownFrom = (int)InvHeight.GetValue(bag); InvHeight.SetValue(bag, _grownFrom + 1); _rowX = 0; }
            if (TakeOff(p, item, false)) return true; // the game found the new row by itself
            List<ItemDrop.ItemData> list = InvList != null ? InvList.GetValue(bag) as List<ItemDrop.ItemData> : null;
            if (list == null || _rowX >= bag.GetWidth()) return false;
            item.m_gridPos = new Vector2i(_rowX++, _grownFrom);
            if (!list.Contains(item)) list.Add(item);
            Shelf.Remove(item);
            if (TakeOff(p, item, false)) { RavenJournal.Note(p, item, "death: laid straight into the grave row (an inventory mod saw no free cell)"); return true; }
            list.Remove(item);
            return false;
        }

        // The bag is its own size again. A raven still in the extra row (no grave was made after all: another mod's
        // way) would be out of the bag's grid, unseen: it goes back on its shoulder, as with a full bag before.
        internal static void GraveMade()
        {
            if (_grown != null && _grownFrom > 0 && InvHeight != null)
            {
                InvHeight.SetValue(_grown, _grownFrom);
                List<ItemDrop.ItemData> list = InvList != null ? InvList.GetValue(_grown) as List<ItemDrop.ItemData> : null;
                if (list != null && _grownFor != null)
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        ItemDrop.ItemData item = list[i];
                        bool munin;
                        if (item == null || item.m_gridPos.y < _grownFrom || !RavenItem.IsRaven(item, out munin)) continue;
                        list.RemoveAt(i);
                        if (item.m_customData != null) item.m_customData.Remove(ReseatKey);
                        Shelf.Add(item);
                        item.m_equipped = true;
                        Seated[item] = _grownFor;
                        Loyal.Mark(item);
                        RavenJournal.Note(_grownFor, item, "death: no grave was made, back on the shoulder from the extra row");
                        Watch(item, true);
                    }
                InvRedraw.Changed(_grown);
            }
            _grown = null; _grownFor = null; _grownFrom = -1;
        }

        // A grave being opened: a raven in it outside the grid you see (its own row from GraveRow, read again from the
        // save with the grave's usual size) is moved into a free cell, so you see it and can take it.
        internal static void GraveOpening(TombStone grave, Player p)
        {
            if (grave == null || p == null || p != Player.m_localPlayer) return;
            Container c = grave.GetComponent<Container>();
            ZNetView nv = grave.GetComponent<ZNetView>();
            if (c == null || nv == null || !nv.IsValid() || !nv.IsOwner()) return; // not ours to rearrange
            Inventory inv = c.GetInventory();
            int w = inv.GetWidth(), h = inv.GetHeight();
            bool moved = false;
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                bool munin;
                if (!RavenItem.IsRaven(item, out munin) || (item.m_gridPos.x < w && item.m_gridPos.y < h)) continue;
                for (int y = 0; y < h && !moved; y++)
                    for (int x = 0; x < w; x++)
                        if (inv.GetItemAt(x, y) == null) { item.m_gridPos = new Vector2i(x, y); moved = true; break; }
                if (moved) { RavenJournal.Note(p, item, "grave: moved into a free cell you can see"); moved = false; InvRedraw.Changed(inv); }
            }
        }

        // The death watch. Other inventory mods (extra slots, equipment slots, their own or a second grave, other
        // versions of them) do their own things with the gear when you die, and some may throw a raven on the ground
        // or put one that should stay into a grave. So from the death to the respawn, twice a second, each raven of
        // the shoulders is looked for by its id: in the bag, on the shoulder cells, in a grave nearby, on the ground.
        // One on the ground (or one that should have stayed, found in a grave) goes back onto its shoulder: saved with
        // the character when the game respawns it, so it is on you again after the respawn. One in a grave is fine.
        private sealed class Watched { internal string Id; internal bool Munin, Stay; internal string Where = ""; internal Container Grave; } // Grave: where it was last seen
        private static readonly List<Watched> DeathWatch = new List<Watched>(2);
        private static Player _diedPlayer, _watchFor;
        private static Vector3 _diedAt;
        private const float WatchRadius = 20f;

        private static void WatchFrom(Player p)
        {
            DeathWatch.Clear();
            _watchFor = p;
            _diedAt = p.transform.position;
        }

        private static void Watch(ItemDrop.ItemData item, bool stay)
        {
            bool munin;
            string id = RavenJournal.IdOf(item);
            if (id == "?" || !RavenItem.IsRaven(item, out munin)) return; // without an id it cannot be told from a copy
            DeathWatch.Add(new Watched { Id = id, Munin = munin, Stay = stay });
        }

        private static bool HasId(ItemDrop.ItemData item, string id)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue("RavenWatch_id", out v) && v == id;
        }

        private static ItemDrop.ItemData FindIn(Inventory inv, string id)
        {
            if (inv == null) return null;
            foreach (ItemDrop.ItemData item in inv.GetAllItems()) if (HasId(item, id)) return item;
            return null;
        }

        // from RavenSeat.Tick
        private static void TickDeathWatch(Player p)
        {
            if (p.IsDead() && p != _diedPlayer)
            {
                // a mod made the grave without the game's CreateTombStone: whatever is still worn stays worn
                _diedPlayer = p;
                WatchFrom(p);
                foreach (KeyValuePair<ItemDrop.ItemData, Player> kv in Seated) if (kv.Value == p) { Loyal.Mark(kv.Key); Watch(kv.Key, true); }
                if (Plugin.Instance != null) Plugin.Instance.Log("Death without the game's grave: the ravens on the shoulders are watched");
            }
            if (DeathWatch.Count == 0) return;
            if (p != _watchFor || !p.IsDead()) { DeathWatch.Clear(); _watchFor = null; return; } // respawned: over
            List<Container> graves = null;
            List<ItemDrop> drops = null;
            foreach (Watched w in DeathWatch)
            {
                // on you: in the bag or on the shoulder cells
                if (FindIn(p.GetInventory(), w.Id) != null) { Seen(w, "bag"); continue; }
                bool onShelf = false;
                foreach (ItemDrop.ItemData item in Shelf) if (HasId(item, w.Id)) { onShelf = true; break; }
                if (onShelf) { Seen(w, "shoulder"); continue; }

                // the grave it was seen in last time first: the scene is searched only while it is not found
                ItemDrop.ItemData found = null;
                Container grave = null;
                if (w.Grave != null) { found = FindIn(w.Grave.GetInventory(), w.Id); if (found != null) grave = w.Grave; else w.Grave = null; }
                if (grave == null && graves == null) graves = Near<Container>(true);
                if (grave == null) foreach (Container c in graves)
                {
                    found = FindIn(c.GetInventory(), w.Id);
                    if (found != null) { grave = c; break; }
                }
                if (grave != null)
                {
                    w.Grave = grave;
                    if (!w.Stay) { Seen(w, "grave"); continue; } // where it should be
                    ZNetView gv = grave.GetComponent<ZNetView>();
                    if (gv == null || !gv.IsValid() || !gv.IsOwner()) { Seen(w, "grave"); continue; } // not ours to take from
                    if (!BackFromDeath(p, found, "a grave (it should have stayed)")) { Seen(w, "grave"); continue; }
                    grave.GetInventory().RemoveItem(found);
                    continue;
                }

                if (drops == null) drops = Near<ItemDrop>(false);
                ItemDrop drop = null;
                foreach (ItemDrop d in drops) if (d != null && HasId(d.m_itemData, w.Id)) { drop = d; break; }
                if (drop != null)
                {
                    ZNetView nv = drop.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid()) continue;
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    if (!BackFromDeath(p, drop.m_itemData, "the ground")) { Seen(w, "ground"); continue; }
                    drops.Remove(drop);
                    ZNetScene.instance.Destroy(drop.gameObject);
                    continue;
                }
                Seen(w, "nowhere near");
            }
        }

        private static void Seen(Watched w, string where)
        {
            if (w.Where == where) return;
            w.Where = where;
            if (Plugin.Instance != null) Plugin.Instance.Log("Death watch: " + Plugin.RavenName(w.Munin) + " " + w.Id + " is in the " + where);
        }

        // back onto its shoulder from where another mod left it at the death
        private static bool BackFromDeath(Player p, ItemDrop.ItemData item, string from)
        {
            bool munin;
            if (!RavenItem.IsRaven(item, out munin) || On(p, munin) != null) return false; // that shoulder is taken: it stays where it is
            item.m_equipped = false;
            Shelf.Add(item);
            if (!Sit(p, item, true)) { Shelf.Remove(item); item.m_equipped = false; return false; }
            if (item.m_customData != null) item.m_customData.Remove(ReseatKey);
            Loyal.Mark(item);
            RavenItem.ForgetWorn();
            RavenJournal.Note(p, item, "death watch: back on the shoulder from " + from);
            return true;
        }

        private static List<T> Near<T>(bool graves) where T : Component
        {
            List<T> list = new List<T>();
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (c == null || (c.transform.position - _diedAt).sqrMagnitude > WatchRadius * WatchRadius) continue;
                if (graves && c.GetComponent<TombStone>() == null) continue; // a grave (the game's or a mod's), not a chest
                list.Add(c);
            }
            return list;
        }

        // taken off by the player: the wait begins
        internal static void TakenOff(Player p, ItemDrop.ItemData item)
        {
            bool munin;
            if (!Enabled || Crumbling || p == null || p != Player.m_localPlayer || p.IsDead() || !RavenItem.IsRaven(item, out munin)) return;
            if (Minutes <= 0f) return;
            // (away on a perch, on a shy outing or at Odin's it is still taken off: the offence and the wait as ever —
            // else taking it off while it was away swapped ravens and kept the trust for free)
            WorldTime.Set(p.m_customData, Key(munin), Minutes * 60f);
            Trust.OnTakenOff(item);
            string wait = RavenAbilities.Span(Minutes * 60f);
            RavenMsg.Center(p, munin
                ? Lang.F("Мунін образився на такого дебіла.\nНе сяде на плече ще {0}.", wait)
                : Lang.F("Хугін сумно злетів з плеча.\nНе сяде ще {0}.", wait));
        }
    }

    // right click in the bag and the hotbar keys: a raven sits down or gets up instead of going into a slot
    [HarmonyPatch(typeof(Player), "ToggleEquipped")]
    internal static class RavenSeatTogglePatch
    {
        private static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return true;
            RavenSeat.Toggle(__instance, item);
            __result = true;
            return false;
        }
    }

    // the game (loading the character) or another mod putting a raven on: it goes to its shoulder, never into a slot
    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    internal static class RavenSeatEquipPatch
    {
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, ref bool __result)
        { try {
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return true;
            Player p = __instance as Player;
            if (p == null) __result = false;
            else if (RavenSeat.Restoring) __result = RavenSeat.Sit(p, item, true);
            else if (p != Player.m_localPlayer) __result = item.m_equipped;
            else __result = RavenSeat.Sit(p, item, false);
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("RavenSeatEquipPatch", e); return true; } }
    }

    // The game or another mod taking a raven "off": only what you do (the bag's and the inventory's own actions, a
    // drop, the death's taking of the gear, the SeneaL doll) takes it off. Anything else (some other code unequipping
    // everything it finds) is refused: the raven stays on its shoulder, and the journal says who tried.
    [HarmonyPatch(typeof(Humanoid), "UnequipItem")]
    internal static class RavenSeatUnequipPatch
    {
        private static readonly string[] Allowed = { "InventoryGui", "InventoryGrid", "UpdateEquipQueue", "UpdateActionQueue", "QueueUnequipAction", "ToggleEquipped", "UseItem", "UnequipAllItems", "DropItem", "OnDeath", "CreateTombStone", "SeneaL" };

        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
        { try {
            bool munin;
            if (!RavenItem.IsRaven(item, out munin)) return true;
            Player p = __instance as Player;
            // your seated raven: our own crumbling of an old copy may; anyone else only through your own actions (the
            // call chain is looked at only here, for a raven, which is rare)
            if (p != null && p == Player.m_localPlayer && !RavenSeat.Crumbling && RavenSeat.IsSeated(item) && !RavenJournal.CalledThrough(Allowed))
            {
                RavenJournal.Note(p, item, "REFUSED: something tried to take it off");
                return false;
            }
            RavenSeat.TakeOff(p, item, true); // (a sulking raven refuses there)
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("RavenSeatUnequipPatch", e); return true; } }
    }

    [HarmonyPatch(typeof(Player), "EquipInventoryItems")]
    internal static class RavenSeatLoadPatch
    {
        private static void Prefix() { RavenSeat.Restoring = true; }
        private static void Postfix(Player __instance)
        {
            RavenSeat.Restoring = false;
            RavenSeat.Loaded(__instance);
        }
        private static System.Exception Finalizer(System.Exception __exception) { RavenSeat.Restoring = false; return __exception; }
    }

    [HarmonyPatch(typeof(Player), "Save")]
    internal static class RavenSeatSavePatch
    {
        private static void Prefix(Player __instance) { try { RavenFetch.BeforeSave(__instance); } catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Saving (fetch): " + e.Message); } try { RavenSeat.Saving(__instance); } catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Saving: " + e.Message); } } // never stop the game saving the character
    }

    [HarmonyPatch(typeof(Player), "CreateTombStone")]
    internal static class RavenSeatDeathPatch
    {
        [HarmonyPriority(Priority.First)] // before other inventory mods move the worn gear around
        private static void Prefix(Player __instance) { try { RavenSeat.Died(__instance); } catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Death: " + e); } } // never stop the grave being made

        // the bag back to its own size once the grave is made (whatever happened in between)
        [HarmonyPriority(Priority.Last)]
        private static System.Exception Finalizer(System.Exception __exception) { RavenSeat.GraveMade(); return __exception; }
    }

    [HarmonyPatch(typeof(TombStone), "Interact")]
    internal static class RavenGraveOpenPatch
    {
        private static void Prefix(TombStone __instance, Humanoid character, bool hold)
        {
            try { if (!hold) GraveRavens.Touched(__instance, character as Player); } catch { } // someone else's grave with a raven in it: a peck
            try { RavenSeat.GraveOpening(__instance, character as Player); } catch { }
        }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class RavenSpawnPatch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            RavenFx.Warm();
            Feathers.PrepareVanish(); // the shadow's backwards sound, caught silently now
            SoundBack.PrepareNamed(RavenSkills.ScoutSound); // and the scouting's
            SoundBack.PrepareNamed(Fury.Sound);              // and the fury's
            SoundBack.PrepareNamed(ScreamFx.CrowSound);      // and the scream's
        }
    }
}
#endif

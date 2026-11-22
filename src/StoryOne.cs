#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // One in the world (story-lore.md «Персні: тільки 9 на сервер»; the user 2026-11-21: the copy of Draupnir Eyvind gives is an item as any — a chest,
    // a grave, a drop — but there is one of it in the world). An item's file says «one: yes» (WsItems.IsOne); nothing of it names a ring.
    //   in the item   RavenWatch_one = «<world> <generation>»: the world it was born in (Story.WorldId — a copy of the world, moved from a game of one's own to
    //                 a server, keeps it) and its generation
    //   in the world  the keys of StoryWorld: one_<id> — the live generation (0: never found here), oneseen_<id> — the game day it was last seen anywhere
    //   born          one with no data in a bag (the story's «do give», the admin's console; one at the feet — when picked up): the next generation, so any
    //                 copy there was is an older one now
    //   taken         one come into a bag since the last look (picked up, out of a chest or a grave) is given the next generation too: a copy left behind
    //                 (a chest glitch, a world rolled back) is older now and crumbles where it is met. Not at the first look after a login or a respawn (the
    //                 review 2026-11-21: the character file keeps a new one only when saved — a crash would have brought back the one before, and the only
    //                 copy crumbled): what it carries then is taken as held. A new generation in the bag saves the character at once (the same crash). So a
    //                 copied character file is caught only when one of the two copies is next taken. A generation above the world's (the world rolled back
    //                 behind the bag) is taken as the live one
    //   seen          in a bag, the open chest, lying about — and, once a minute while unseen today, in a loaded chest or on an item stand near (Around):
    //                 a stored one is not a lost one
    //   crumbles      older than the live one, or of another world (or one not found in this world): to ash, in a bag, in the open chest (Tick) or lying
    //                 about (StoryOneDrop, the game that owns it)
    //   the altar     the Altar of the Watch («script: altar», StoryOneAltar) gives back one the world has not seen for RecallDays game days (destroyed, lost,
    //                 its keeper not in the game): a new generation into the bag — the old one, wherever it is, crumbles when met
    // A world key written by a client comes back from the server a moment later: till it does, the generation just given is the live one (Pending), so
    // the ring is not taken for an older copy of itself; not back after Wait s — written again.
    // Cost: the bag and the open chest looked through every Every s (a few dozen items, a lookup each); the loaded chests and stands once a minute only
    // while a found one is unseen today; a world key written only when one is born, taken or given back, and once a game day while it is seen (each write
    // sends the whole key list to everyone, and each new text of a key stays in every player's stats: StoryWorld — so never on a login)
    internal static class StoryOne
    {
        internal const string DataKey = "RavenWatch_one";
        internal static int RecallDays = 9;
        internal static float Every = 2f, Wait = 20f, Settle = 10f;
        private static float _next, _since;
        private static readonly FieldInfo OpenContainer = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        private sealed class Pend { internal int Gen; internal float At; }
        private static readonly Dictionary<string, Pend> Pending = new Dictionary<string, Pend>(); // session: a generation written, not back from the server yet
        private static HashSet<string> _held; // session: «<id> <generation>» in the bag at the last look (null: no look yet for this character — all it carries is held)
        private static Player _whose;
        private static string _world = ""; // session: the world the two below are of
        private static readonly Dictionary<string, int> SeenDay = new Dictionary<string, int>(); // session: the day this game last wrote it seen

        // this game has its world's keys by now (a player of its own in the world Settle s): before, a live one would read as never found
        internal static bool Settled { get { return _whose != null && _whose == Player.m_localPlayer && Time.time - _since >= Settle; } }

        internal static string IdOf(string prefab) { return prefab.StartsWith(WsItems.PrefabPrefix, System.StringComparison.Ordinal) ? prefab.Substring(WsItems.PrefabPrefix.Length) : prefab; }
        internal static bool Is(ItemDrop.ItemData it) { return it != null && it.m_dropPrefab != null && WsItems.IsOne(it.m_dropPrefab.name); }
        private static int Today { get { return EnvMan.instance != null ? EnvMan.instance.GetDay() : 0; } }

        // its world and generation (false: none yet — not born)
        private static bool Read(ItemDrop.ItemData it, out string world, out int gen)
        {
            world = ""; gen = 0;
            string v;
            if (it.m_customData == null || !it.m_customData.TryGetValue(DataKey, out v)) return false;
            string[] w = v.Split(' ');
            if (w.Length < 2 || !int.TryParse(w[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out gen)) return false;
            world = w[0];
            return true;
        }
        private static void Write(ItemDrop.ItemData it, int gen)
        {
            if (it.m_customData == null) it.m_customData = new Dictionary<string, string>();
            it.m_customData[DataKey] = Story.WorldId + " " + gen.ToString(CultureInfo.InvariantCulture);
        }

        // the live generation in this world (one just written by this game counts till the server's word comes back)
        internal static int Live(string id)
        {
            int v = StoryWorld.Var("one_" + id);
            Pend p;
            if (!Pending.TryGetValue(id, out p)) return v;
            if (v >= p.Gen) { Pending.Remove(id); return v; } // (back from the server — or another game gave a newer one: then ours is the older)
            if (Time.time - p.At < Wait) return p.Gen;
            StoryWorld.SetVar("one_" + id, p.Gen); p.At = Time.time; // (not back: written again)
            Story.Log("one: " + id + " generation " + p.Gen + " not back from the server in " + Wait + " s — written again");
            return p.Gen;
        }
        private static void SetLive(string id, int gen)
        {
            StoryWorld.SetVar("one_" + id, gen);
            Pending[id] = new Pend { Gen = gen, At = Time.time };
        }
        internal static int LastSeen(string id) { return StoryWorld.Var("oneseen_" + id); }

        // seen in the world today (once a day per game)
        internal static void Seen(string id)
        {
            int today = Today, was;
            if (today <= 0 || (SeenDay.TryGetValue(id, out was) && was == today)) return;
            SeenDay[id] = today;
            if (LastSeen(id) < today) StoryWorld.SetVar("oneseen_" + id, today);
        }

        // why this copy is no longer one (null: it is the live one)
        internal static string Echo(ItemDrop.ItemData it, string id)
        {
            string world; int gen;
            if (!Read(it, out world, out gen)) return null; // (not born: nothing to compare)
            if (world != Story.WorldId) return "of another world";
            int live = Live(id);
            if (live <= 0) return "never found in this world";
            return gen < live ? "older (" + gen + " < " + live + ")" : null;
        }

        // the next generation into this copy: every other one is older now
        private static int Next(ItemDrop.ItemData it, string id, string why)
        {
            string world; int gen;
            if (!Read(it, out world, out gen)) gen = 0;
            int next = Mathf.Max(gen, Live(id)) + 1;
            Write(it, next);
            SetLive(id, next);
            Seen(id);
            Story.Log("one: " + id + " " + why + " — generation " + next);
            return next;
        }

        // ---------------------------------------------------------------- the bag and the open chest (the story's pass, twice a second: its own clock)
        internal static void Tick(Player p)
        {
            if (p == null || p.IsDead() || Time.time < _next || ZoneSystem.instance == null || EnvMan.instance == null) return;
            _next = Time.time + Every;
            if (!WsItems.AnyOne()) return;
            if (Story.WorldId != _world) { _world = Story.WorldId; Pending.Clear(); SeenDay.Clear(); } // (another world, or this one rolled back: nothing of the last one's carried over)
            if (p != _whose) { _whose = p; _held = null; _since = Time.time; } // (another character, a respawn: what it carries is taken as held — no new generation for it)
            if (Time.time - _since < Settle) return; // (the world's keys may not have come from the server yet: a live one would read as never found)
            HashSet<string> now = new HashSet<string>();
            List<ItemDrop.ItemData> ash = null;
            bool raised = false;
            foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems())
            {
                if (!Is(it)) continue;
                string id = IdOf(it.m_dropPrefab.name), world; int gen;
                if (!Read(it, out world, out gen)) { now.Add(id + " " + Next(it, id, "born in the bag")); raised = true; continue; }
                string why = Echo(it, id);
                if (why != null) { (ash ?? (ash = new List<ItemDrop.ItemData>())).Add(it); Story.Log("one: " + id + " generation " + gen + " in the bag crumbles: " + why); continue; }
                bool came = _held != null && !_held.Contains(id + " " + gen);
                if (came || gen > Live(id)) { gen = Next(it, id, came ? "came into the bag" : "above the world's (rolled back?)"); raised = true; } // (any copy left behind is older now)
                if (!now.Add(id + " " + gen)) { (ash ?? (ash = new List<ItemDrop.ItemData>())).Add(it); continue; } // (the same one twice in one bag: one stays)
                Seen(id);
            }
            _held = now;
            if (ash != null) Crumble(p, p.GetInventory(), ash);
            // a new generation is in the world's keys at once, the character keeps it only when saved (by the game: at logout, every half hour): saved
            // now, or a crash would bring back the one before, an older copy of itself
            if (raised && Game.instance != null) { try { Game.instance.SavePlayerProfile(false); } catch (System.Exception e) { Story.Log("one: the character not saved after a new generation: " + e.Message); } } // heavy: a file, only when one is born or taken into the bag
            Around(p);

            Container open = OpenContainer != null && InventoryGui.instance != null ? OpenContainer.GetValue(InventoryGui.instance) as Container : null;
            if (open == null || open.GetInventory() == null) return;
            ash = null;
            foreach (ItemDrop.ItemData it in open.GetInventory().GetAllItems())
            {
                if (!Is(it)) continue;
                string id = IdOf(it.m_dropPrefab.name), why = Echo(it, id);
                if (why == null) { Seen(id); continue; }
                (ash ?? (ash = new List<ItemDrop.ItemData>())).Add(it);
                Story.Log("one: " + id + " in a chest crumbles: " + why);
            }
            if (ash != null) Crumble(p, open.GetInventory(), ash);
        }

        // kept in a chest that is not opened, hung on a stand: seen too while it is loaded near you (a stored one is not a lost one). Once in AroundEvery s, and
        // only while one found in this world has not been written seen today by this game; a stale copy there is not counted (it crumbles when met)
        internal static float AroundEvery = 60f;
        private static float _aroundNext;
        private static void Around(Player p)
        {
            if (Time.time < _aroundNext) return;
            _aroundNext = Time.time + AroundEvery;
            int today = Today, was;
            List<string> want = null;
            foreach (string pf in WsItems.Ones())
            {
                string id = IdOf(pf);
                if (Live(id) <= 0 || (SeenDay.TryGetValue(id, out was) && was == today)) continue;
                (want ?? (want = new List<string>())).Add(pf);
            }
            if (want == null) return;
            foreach (Container k in Object.FindObjectsOfType<Container>()) // heavy: once a minute, only while a found one is unseen today
            {
                if (k == null || k.GetInventory() == null) continue;
                foreach (ItemDrop.ItemData it in k.GetInventory().GetAllItems())
                    if (Is(it) && want.Contains(it.m_dropPrefab.name) && Echo(it, IdOf(it.m_dropPrefab.name)) == null) Seen(IdOf(it.m_dropPrefab.name));
            }
            foreach (ItemStand s in Object.FindObjectsOfType<ItemStand>()) // heavy: the same once a minute
            {
                if (s == null || !s.HaveAttachment()) continue;
                foreach (string pf in want) if (s.GetAttachedItem() == pf.GetStableHashCode()) Seen(IdOf(pf)); // (a stand keeps the item's data in its own: its generation is not read here)
            }
        }

        private static void Crumble(Player p, Inventory inv, List<ItemDrop.ItemData> ash)
        {
            foreach (ItemDrop.ItemData it in ash) inv.RemoveItem(it);
            p.Message(MessageHud.MessageType.Center, Lang.F("{0} розсипається попелом: справжній — деінде.", Localization.instance != null ? Localization.instance.Localize(ash[0].m_shared.m_name) : ash[0].m_shared.m_name));
        }

        // ---------------------------------------------------------------- the altar's: what it may give back
        // the first one of a kind found in this world that nobody has seen for RecallDays game days (null: none)
        internal static string Lost(out bool anyFound)
        {
            anyFound = false;
            int today = Today;
            foreach (string pf in WsItems.Ones())
            {
                string id = IdOf(pf);
                if (Live(id) <= 0) continue;
                anyFound = true;
                if (today - LastSeen(id) >= RecallDays) return pf;
            }
            return null;
        }

        // given back: a new generation into the bag (false: no room, no such item)
        internal static bool GiveBack(Player p, string prefab)
        {
            GameObject g = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop d = g != null ? g.GetComponent<ItemDrop>() : null;
            if (p == null || d == null || !p.GetInventory().HaveEmptySlot()) return false;
            ItemDrop.ItemData it = d.m_itemData.Clone();
            it.m_dropPrefab = g; it.m_stack = 1; it.m_customData = new Dictionary<string, string>();
            string id = IdOf(prefab);
            if (!p.GetInventory().AddItem(it)) return false;
            int gen = Next(it, id, "given back by the altar (unseen since day " + LastSeen(id) + ")"); // (into the one in the bag now: the live one is never one that was not given)
            if (_held != null) _held.Add(id + " " + gen); // (in the bag already: not «come in» again at the next look)
            if (Game.instance != null) { try { Game.instance.SavePlayerProfile(false); } catch (System.Exception e) { Story.Log("one: the character not saved after the altar: " + e.Message); } } // heavy: a file, once a giving back
            return true;
        }
    }

    // a copy lying on the ground (its prefab's own: WsItems.Make): the game that owns it lets an older or foreign one crumble; any game that has it
    // loaded says it is seen. Every Every s, from a moment after it is loaded (its data read from the world)
    internal sealed class StoryOneDrop : MonoBehaviour
    {
        private ZNetView _nv;
        private ItemDrop _drop;
        private float _next;

        private void Start() { _nv = GetComponent<ZNetView>(); _drop = GetComponent<ItemDrop>(); _next = Time.time + 1f; }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + StoryOne.Every * 1.5f;
            if (_nv == null || !_nv.IsValid() || _drop == null || !StoryOne.Is(_drop.m_itemData) || ZNetScene.instance == null || !StoryOne.Settled) return; // (an icon's photo, a ghost: no copy of the world; a game just come into it: its keys first)
            string id = StoryOne.IdOf(_drop.m_itemData.m_dropPrefab.name), why = StoryOne.Echo(_drop.m_itemData, id);
            if (why == null) { if (_drop.m_itemData.m_customData != null && _drop.m_itemData.m_customData.ContainsKey(StoryOne.DataKey)) StoryOne.Seen(id); return; }
            if (!_nv.IsOwner()) return;
            Story.Log("one: " + id + " lying at " + transform.position.ToString("F0") + " crumbles: " + why);
            ZNetScene.instance.Destroy(gameObject);
        }
    }

    // The Altar of the Watch (a story piece whose file says «script: altar»: the Hall's; Torvald in chapter 2: «До нього йшли, коли губили щось золоте.
    // Він вертав»). E gives back one of a kind the world has lost (StoryOne.Lost). No price yet (story-lore.md: «ціна виклику — потім»)
    internal sealed class StoryOneAltar : MonoBehaviour, Hoverable, Interactable
    {
        private ZNetView _nv;
        private void Awake() { _nv = GetComponentInParent<ZNetView>(); }

        public string GetHoverName() { return Lang.T("Вівтар Варти"); }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            string t = Lang.T("Вівтар Варти") + "\n" + Lang.T("Сюди йшли, коли губили щось золоте.");
            if (_nv == null || !_nv.IsValid()) return t; // (the hammer's ghost)
            bool found;
            string pf = StoryOne.Lost(out found);
            if (pf != null) t += "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.F("Покликати: {0}", LocHearth.ItemName(pf) ?? pf);
            else if (found) t += "\n<color=#a0a0a0>" + Lang.T("Вівтар мовчить: те, що він знає, ще ходить світом.") + "</color>";
            return Localization.instance != null ? Localization.instance.Localize(t) : t;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (hold || p == null || _nv == null || !_nv.IsValid()) return false;
            bool found;
            string pf = StoryOne.Lost(out found);
            if (pf == null) { p.Message(MessageHud.MessageType.Center, Lang.T(found ? "Вівтар мовчить: те, що він знає, ще ходить світом." : "Вівтар мовчить.")); return false; }
            if (!StoryOne.GiveBack(p, pf)) { p.Message(MessageHud.MessageType.Center, Lang.T("У торбі нема місця.")); return false; }
            string name = LocHearth.ItemName(pf) ?? pf;
            p.Message(MessageHud.MessageType.Center, Lang.F("{0} вертається до тебе.", Localization.instance != null ? Localization.instance.Localize(name) : name));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif

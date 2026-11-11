#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Chores (npc-plan.md): small useful things a resident does by itself, outside any job — a whim (NpcWhims) that
    // walks somewhere and does a thing there, step by step (NpcMind Task.Chore, the chore's id in the plan's Pose):
    //   mend   a damaged piece of its base (after a raid, the weather): to it, the hammer out, a few blows — whole again
    //          (as a player mends: free), then the next one, up to three
    //   tend   a hungry tamed beast of its base: food it eats from the base's chests, put down before it (the beast eats
    //          it as it eats what lies about); a ravens' feeder short of meat or of plants: topped up from the chests
    // Light: the base's damaged pieces and needs are looked for once in half a minute per base; nothing runs between.
    // New chores: an id here, its Find and its steps; a whim for it in NpcWhims.
    internal static class NpcChores
    {
        private const float Again = 30f, MendLow = 0.9f, FeederLow = 3f;
        internal const string Mend = "mend", Tend = "tend";

        internal static NpcMind.Plan Plan(string chore) { return new NpcMind.Plan { Kind = NpcMind.Task.Chore, Pose = chore }; }

        // ---------------------------------------------------------------- what needs doing at a base (cached)
        private sealed class Needs { public float At; public readonly List<WearNTear> Broken = new List<WearNTear>(); public readonly List<Character> Hungry = new List<Character>(); public readonly List<FeederView> Feeders = new List<FeederView>(); }
        private static readonly Dictionary<string, Needs> Seen = new Dictionary<string, Needs>();
        private static object _world;

        private static Needs Of(NpcBase b)
        {
            if (!ReferenceEquals(_world, ZNet.instance)) { Seen.Clear(); _world = ZNet.instance; }
            string key = Mathf.RoundToInt(b.Centre.x) + "," + Mathf.RoundToInt(b.Centre.z);
            Needs n;
            if (Seen.TryGetValue(key, out n) && Time.time - n.At < Again) return n;
            n = new Needs { At = Time.time };
            if (b.Wears != null) foreach (WearNTear w in b.Wears) if (w != null && w.GetHealthPercentage() < MendLow) n.Broken.Add(w); // (the base's own look: NpcBase; not made yet — nothing to mend till it is)
            foreach (Character c in Character.GetAllCharacters())
                if (c != null && !c.IsDead() && c.IsTamed() && b.Inside(c.transform.position)) { Tameable t = c.GetComponent<Tameable>(); if (t != null && t.IsHungry()) n.Hungry.Add(c); }
            foreach (FeederView f in FeederView.All) if (f != null && f.Box != null && b.Holds(f.transform.position)) n.Feeders.Add(f);
            if (Seen.Count > 50) Seen.Clear();
            Seen[key] = n;
            return n;
        }

        internal static List<WearNTear> Broken(NpcBase b) { return b != null && b.Ready ? Of(b).Broken : new List<WearNTear>(); } // (a servant: its house's among them)

        internal static bool CanMend(NpcResident r) { NpcBase b = r.Base; return b != null && b.Ready && Of(b).Broken.Exists(w => w != null && w.GetHealthPercentage() < MendLow); }
        internal static bool CanTend(NpcResident r)
        {
            NpcBase b = r.Base;
            if (b == null || !b.Ready) return false;
            Needs n = Of(b);
            ItemDrop.ItemData it; Container c;
            foreach (Character a in n.Hungry) if (a != null && Food(b, r.transform.position, a, out it, out c)) return true;
            bool meat;
            foreach (FeederView f in n.Feeders) if (f != null && FeederShort(f, out meat) && RavenFood(b, r.transform.position, f, meat, out it, out c)) return true;
            return false;
        }

        // ---------------------------------------------------------------- the chests of a base (not feeders, not private)
        private static IEnumerable<Container> Chests(NpcBase b, Vector3 me)
        {
            foreach (Container c in NpcWorker.InBaseOf<Container>(b, me))
                if (c != null && c.GetComponent<Piece>() != null && c.m_privacy != Container.PrivacySetting.Private && c.GetComponent<FeederView>() == null && !c.IsInUse()) yield return c;
        }

        // food a tamed beast eats, in a chest of the base
        private static bool Food(NpcBase b, Vector3 me, Character beast, out ItemDrop.ItemData item, out Container from)
        {
            item = null; from = null;
            MonsterAI ai = beast.GetComponent<MonsterAI>(); // (the game's own taming — other mods' beasts too, when they are made with it)
            if (ai == null || ai.m_consumeItems == null || ai.m_consumeItems.Count == 0) return false;
            foreach (Container c in Chests(b, me))
                foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
                    if (Eats(ai, it)) { item = it; from = c; return true; }
            return false;
        }

        private static bool Eats(MonsterAI ai, ItemDrop.ItemData it)
        {
            foreach (ItemDrop ci in ai.m_consumeItems) if (ci != null && ci.m_itemData.m_shared.m_name == it.m_shared.m_name) return true;
            return false;
        }

        // a feeder short of one kind (meat for Munin, the rest for Hugin)
        private static bool FeederShort(FeederView f, out bool meat)
        {
            float m = 0f, p = 0f;
            foreach (ItemDrop.ItemData it in f.Box.GetInventory().GetAllItems()) { if (Feeder.FillOf(true, it) > 0f) m += it.m_stack; else if (Feeder.FillOf(false, it) > 0f) p += it.m_stack; }
            meat = m < p; // (the shorter kind first)
            return m < FeederLow || p < FeederLow;
        }

        private static bool RavenFood(NpcBase b, Vector3 me, FeederView f, bool meat, out ItemDrop.ItemData item, out Container from)
        {
            item = null; from = null;
            for (int kind = 0; kind < 2; kind++, meat = !meat) // (the short kind first, the other if none of it)
                foreach (Container c in Chests(b, me))
                    foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
                        if (Feeder.FillOf(meat, it) > 0f) { item = it; from = c; return true; }
            return false;
        }

        // ---------------------------------------------------------------- the steps (true: over)
        internal static bool Step(NpcMind m, NpcMind.Plan p, float dt)
        {
            NpcActor a = m.Actor;
            NpcResident r = a != null ? a.Resident : null;
            if (r == null || !NpcResident.Is(a) || !r.Base.Ready) return true;
            return p.Pose == Mend ? Mending(m, a, r, p, dt) : p.Pose == Tend ? Tending(m, a, r, p, dt) : true;
        }

        //   0 the nearest damaged piece it can walk to · 1 to it · 2 blows, then whole · back to 0 (three at most)
        private static bool Mending(NpcMind m, NpcActor a, NpcResident r, NpcMind.Plan p, float dt)
        {
            if (p.Stage == 0)
            {
                if (p.Tries >= 3f) return true; // (three done)
                WearNTear best = null; float bd = float.MaxValue;
                foreach (WearNTear w in Of(r.Base).Broken)
                {
                    if (w == null || w.GetHealthPercentage() >= MendLow) continue;
                    float d = (w.transform.position - m.transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; best = w; }
                }
                if (best == null) return true;
                Of(r.Base).Broken.Remove(best); // (taken: another resident mends another one)
                Vector3 stand = Stand(best.transform.position, r.Base.Centre);
                if (best.transform.position.y - stand.y > 4f) return false; // (a roof out of a hammer's reach from the ground: another)
                if (Pathfinding.instance != null && !NpcWalk.Have(m.transform.position, stand) && NpcDoors.Toward(m.transform.position, stand, NpcKit.Walker) == null) return false; // (out of its reach, not even through a door: another)
                p.Who = NpcKit.IdOf(best); p.To = stand; p.Look = best.transform.position; p.Stage = 1;
                m.NewGoal();
                return false;
            }
            GameObject go = NpcKit.Find(p.Who);
            WearNTear piece = go != null ? go.GetComponent<WearNTear>() : null;
            if (piece == null || piece.GetHealthPercentage() >= 0.999f) { Hammer(a, false); p.Stage = 0; return false; }
            if (p.Stage == 1)
            {
                m.Status = Lang.K("іде лагодити");
                if (!m.Walk(dt, p.To, 1.2f)) return false;
                p.Stage = 2; p.Since = Time.time; p.Clock = 0f;
                Hammer(a, true);
            }
            m.Halt(); m.Face(p.Look);
            m.Status = Lang.K("лагодить");
            if (Time.time - p.Since < p.Clock) return false;
            p.Clock += 1.3f; // (a blow every 1.3 s)
            Humanoid body = a.GetComponent<Humanoid>(); // (the hammer's own blow: the game's animation for it)
            ItemDrop.ItemData inHand = body != null ? body.GetCurrentWeapon() : null;
            ZSyncAnimation za = a.GetComponent<ZSyncAnimation>();
            if (za != null && inHand != null && !string.IsNullOrEmpty(inHand.m_shared.m_attack.m_attackAnimation)) za.SetTrigger(inHand.m_shared.m_attack.m_attackAnimation);
            if (p.Clock < 4f) return false; // (three blows, then whole)
            if (piece.Repair())
            {
                Piece pc = piece.GetComponent<Piece>();
                if (pc != null) pc.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
                Npcs.Log(a.Id + ": mended " + Utils.GetPrefabName(piece.gameObject));
            }
            Hammer(a, false);
            p.Tries += 1f; p.Stage = 0;
            if (p.Tries == 1f && Random.value < 0.4f) NpcChatter.Mutter(a, "mend");
            return false;
        }

        // where to stand to mend it: on the ground below it, a step towards the base's middle
        private static Vector3 Stand(Vector3 at, Vector3 centre)
        {
            Vector3 dir = centre - at; dir.y = 0f;
            Vector3 p = at + (dir.sqrMagnitude > 0.01f ? dir.normalized * 1.6f : Vector3.zero);
            float h; if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out h)) p.y = h;
            return p;
        }

        // the game's hammer in its hand for the mending (and its own weapon back after)
        internal static void Hammer(NpcActor a, bool on) { Hold(a, on ? "Hammer" : null); }

        // a tool's look in its hand (a copy of the game's: the hammer, a lumberjack's axe, a miner's pickaxe — its own
        // stays in its cells), null: put away, its own weapon back
        internal static void Hold(NpcActor a, string prefabName)
        {
            Humanoid body = a.GetComponent<Humanoid>();
            if (body == null || ObjectDB.instance == null) return;
            ItemDrop.ItemData held = null;
            foreach (ItemDrop.ItemData it in body.GetInventory().GetAllItems()) if (it.m_customData != null && it.m_customData.ContainsKey(HammerKey)) { held = it; break; }
            if (held != null && prefabName != null && held.m_customData[HammerKey] != prefabName) { body.UnequipItem(held, false); body.GetInventory().RemoveItem(held); held = null; } // (another tool now)
            if (prefabName != null)
            {
                if (held != null) { if (!body.IsItemEquiped(held)) body.EquipItem(held, false); return; }
                GameObject prefab = ObjectDB.instance.GetItemPrefab(prefabName);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null) return;
                ItemDrop.ItemData h = drop.m_itemData.Clone(); h.m_dropPrefab = prefab; h.m_customData[HammerKey] = prefabName;
                if (body.GetInventory().AddItem(h)) body.EquipItem(h, false);
                return;
            }
            if (held == null) return;
            body.UnequipItem(held, false);
            body.GetInventory().RemoveItem(held);
            NpcGear.AtEase(a, body); // (a guard: its weapon back in hand; the others: hands free, weapons on the back)
        }
        private const string HammerKey = "RavenWatch_npcHammer";

        //   0 what needs feeding and from which chest · 1 to the chest, a few out · 2 to the beast / feeder · 3 given
        private static bool Tending(NpcMind m, NpcActor a, NpcResident r, NpcMind.Plan p, float dt)
        {
            Needs n = Of(r.Base);
            if (p.Stage == 0)
            {
                ItemDrop.ItemData it; Container from;
                foreach (Character beast in n.Hungry)
                {
                    if (beast == null || beast.IsDead() || !Food(r.Base, m.transform.position, beast, out it, out from)) continue;
                    p.Who = NpcKit.IdOf(from); p.Home = beast.transform.position; p.Food = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
                    p.Count = 2f; p.Stage = 1; Keep(); _beast[m] = beast; // (a couple for a beast)
                    m.NewGoal(); return false;
                }
                bool meat;
                foreach (FeederView f in n.Feeders)
                {
                    if (f == null || !FeederShort(f, out meat) || !RavenFood(r.Base, m.transform.position, f, meat, out it, out from)) continue;
                    p.Who = NpcKit.IdOf(from); p.Home = f.transform.position; p.Food = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
                    p.Count = 5f; p.Stage = 1; Keep(); _feeder[m] = f; // (a handful for the ravens)
                    m.NewGoal(); return false;
                }
                return true;
            }
            if (p.Stage == 1)
            {
                GameObject go = NpcKit.Find(p.Who);
                Container c = go != null ? go.GetComponentInChildren<Container>() : null;
                if (c == null || p.Food.Length == 0) return true;
                m.Status = Lang.K("бере їжу");
                if (!m.Walk(dt, c.transform.position, 1.8f)) return false;
                if (c.IsInUse() && !NpcLid.Mine(c, m.Actor)) return true; // (a player has it open: this chore let go)
                if (!m.Open(c, 1.2f)) return false; // (its lid up, facing it: NpcMind.Open, the one way)
                int take = 0;
                foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(c.GetInventory().GetAllItems()))
                {
                    if (take >= (int)p.Count || it.m_dropPrefab == null || it.m_dropPrefab.name != p.Food) continue;
                    int k = Mathf.Min((int)p.Count - take, it.m_stack);
                    c.GetInventory().RemoveItem(it, k); take += k;
                }
                if (take == 0) return true;
                p.Count = take; p.Stage = 2; m.Face(c.transform.position); m.NewGoal();
                return false;
            }
            // 2: to the beast (it may have moved) or the feeder
            Character beastNow; FeederView feeder;
            _beast.TryGetValue(m, out beastNow); _feeder.TryGetValue(m, out feeder);
            Vector3 to = beastNow != null ? beastNow.transform.position : feeder != null ? feeder.transform.position : p.Home;
            m.Status = beastNow != null ? Lang.K("несе їжу звірові") : Lang.K("несе їжу воронам");
            if (!m.Walk(dt, to, beastNow != null ? 2f : 1.6f)) return false;
            m.Face(to);
            Give(a, p, beastNow, feeder);
            _beast.Remove(m); _feeder.Remove(m);
            if (Random.value < 0.4f) NpcChatter.Mutter(a, beastNow != null ? "tend_beast" : "tend_ravens");
            return true;
        }

        private static readonly Dictionary<NpcMind, Character> _beast = new Dictionary<NpcMind, Character>();
        private static readonly Dictionary<NpcMind, FeederView> _feeder = new Dictionary<NpcMind, FeederView>();
        private static void Keep() { if (_beast.Count > 50) _beast.Clear(); if (_feeder.Count > 50) _feeder.Clear(); } // (a person gone mid-chore: not kept for ever)

        // what it carried for the chore put where it goes (into the feeder; down before the beast — or at its feet,
        // when the chore is cut off: nothing vanishes)
        private static void Give(NpcActor a, NpcMind.Plan p, Character beast, FeederView feeder)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(p.Food) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            int n = (int)p.Count;
            p.Stage = 3; p.Count = 0f;
            if (drop == null || n <= 0) return;
            if (feeder != null && feeder.Box != null)
            {
                ZNetView nv = feeder.Box.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && !feeder.Box.IsInUse())
                {
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    ItemDrop.ItemData it = drop.m_itemData.Clone(); it.m_dropPrefab = prefab; it.m_stack = n;
                    if (nv.IsOwner() && feeder.Box.GetInventory().AddItem(it)) { Npcs.Log(a.Id + ": put " + n + " " + p.Food + " into a feeder"); return; }
                }
            }
            Vector3 at = beast != null ? beast.transform.position + (a.transform.position - beast.transform.position).normalized * 1f : a.transform.position + a.transform.forward * 0.8f;
            ItemDrop.ItemData put = drop.m_itemData.Clone(); put.m_dropPrefab = prefab;
            ItemDrop.DropItem(put, n, at + Vector3.up * 0.4f, Quaternion.identity);
            Npcs.Log(a.Id + ": put down " + n + " " + p.Food + (beast != null ? " for " + beast.m_name : ""));
        }

        // cut off (a fight, an order): what it carried put down at its feet, the hammer away; the chore starts afresh
        internal static void Left(NpcMind m, NpcMind.Plan p)
        {
            NpcActor a = m.Actor;
            if (a == null) return;
            if (p.Pose == Mend) { Hammer(a, false); if (p.Stage == 2) p.Stage = 1; return; }
            if (p.Pose == Tend && p.Stage == 2 && p.Count > 0f) { Give(a, p, null, null); p.Stage = 0; }
            _beast.Remove(m); _feeder.Remove(m);
        }
    }
}
#endif

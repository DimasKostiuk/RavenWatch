#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // A person's own gear (npc-plan.md, fighters): ten cells of its own — the game's chest on its body, so the game's own
    // window opens with your bag beside it (Q → «Спорядження»): put gear in, take it out. It wears the best of what is
    // there by the stats: armour piece by piece (its template's clothes where it has nothing better), a weapon for near
    // and one for far — a bow or a crossbow only with arrows or bolts for it in its cells — and a shield with a one-handed
    // weapon. In a fight it holds the far one while the foe is far and the near one when it comes close (NpcMind).
    // The cells are kept by the game in its ZDO: every game sees the same, nothing is lost; what it shoots is taken from
    // them. Sent off (unhired, taken away), it puts them down at its feet. The cells are kept in its locker (NpcLocker),
    // not on the person: the game's chest window works as ever, its keeping goes there (NpcGearKeepPatch).
    internal sealed class NpcGear : MonoBehaviour
    {
        internal const int Width = 5, Height = 2;
        private Container _box;
        private NpcActor _a;
        private string _seen = "";
        private bool _changed = true;
        private float _next;
        private static readonly System.Reflection.FieldInfo OpenBox = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");
        private readonly Dictionary<string, int> _ammoSeen = new Dictionary<string, int>();

        internal ItemDrop.ItemData Near, Far, Shield; // its weapons and shield as worn now (the body's own copies)

        // ---------------------------------------------------------------- weapons: who, and in hand or on the back (the user, 2026-11-01)
        // one who can stand guard can carry weapons (a fighter by its template, any settler — Guard.Can); one who never fights
        // (the old elder: it runs) gets none — armour, clothes and its work's tools only. A guard walks with them in hand,
        // ready; the others keep them on the back, draw them for a fight (NpcMind) and put them back after
        internal static bool CanFight(NpcActor a)
        {
            if (a == null || a.Def == null) return false;
            if (Guard.Is(a)) return true;
            return (a.Def.Generated || a.Def.Fight == "defend" || a.Def.Fight == "hunt") && NpcLevers.NoJob(a, "guard") == null; // (the guard job's own rule — a fighter by its template or any settler — read from the template: its mind may not be loaded yet as it is first dressed)
        }
        internal static bool InHand(NpcActor a) { return Guard.Is(a); }

        private static readonly System.Reflection.FieldInfo HiddenR = AccessTools.Field(typeof(Humanoid), "m_hiddenRightItem"), HiddenL = AccessTools.Field(typeof(Humanoid), "m_hiddenLeftItem");

        // a fight: its weapons out (off its back as the game draws them; none there — its near one taken up)
        internal void Draw(Humanoid body)
        {
            _sheathe = false;
            if (body == null || (Near == null && Far == null) || body.InAttack()) return;
            if ((Near != null && body.IsItemEquiped(Near)) || (Far != null && body.IsItemEquiped(Far))) return; // (in hand already)
            body.EquipItem(Near ?? Far, false); // (the game's own: off its back)
            if (Near != null && Shield != null && !Near.IsTwoHanded()) body.EquipItem(Shield, false);
            Back(body, null, null);
        }

        // on its back where others see it (the game draws the back slots for players only: set here, synced by its ZDO)
        private static void Back(Humanoid body, ItemDrop.ItemData right, ItemDrop.ItemData left)
        {
            VisEquipment ve = body.GetComponent<VisEquipment>();
            if (ve == null) return;
            ve.SetRightBackItem(right != null && right.m_dropPrefab != null ? right.m_dropPrefab.name.GetStableHashCode() : 0, right != null ? right.m_quality : 0);
            ve.SetLeftBackItem(left != null && left.m_dropPrefab != null ? left.m_dropPrefab.name.GetStableHashCode() : 0, left != null ? left.m_variant : 0, left != null ? left.m_quality : 0);
        }

        // its hands emptied onto its back (no animation where nobody needs one: dressing)
        internal static void Sheathe(Humanoid body, bool animation)
        {
            if (body == null || body.InAttack()) return;
            body.HideHandItems(false, animation);
            Back(body, HiddenR != null ? HiddenR.GetValue(body) as ItemDrop.ItemData : null, HiddenL != null ? HiddenL.GetValue(body) as ItemDrop.ItemData : null);
        }
        private bool _sheathe, _fought; // (a fight over mid-swing: put away once the swing is done — Update; whether it might fight as last dressed)

        // the fight over (or its work tool put down): on its back again — a guard keeps them in hand
        internal static void AtEase(NpcActor a, Humanoid body)
        {
            NpcGear g = a != null ? a.Gear : null;
            if (g == null || body == null) return;
            if (InHand(a)) // (a guard: its near weapon and shield back in hand)
            {
                ItemDrop.ItemData main = g.Near ?? g.Far; // (a bow only: the bow)
                if (main != null && body.GetInventory().ContainsItem(main) && !body.IsItemEquiped(main)) body.EquipItem(main, false);
                if (g.Shield != null && body.GetInventory().ContainsItem(g.Shield) && (g.Near == null || !g.Near.IsTwoHanded()) && !body.IsItemEquiped(g.Shield)) body.EquipItem(g.Shield, false);
                return;
            }
            if (body.InAttack()) { g._sheathe = true; return; } // (the killing swing still going: put away once it is done)
            Sheathe(body, true);
        }

        // one who never fights: weapons, shields and what is shot put back in your bag (its work's axe or pickaxe stays)
        private void NoWeapons()
        {
            if (CanFight(_a) || Cells == null || !NpcLocker.Ready(_a) || (_kept == null && NpcLocker.Has(_a))) return; // (its cells read here first: never written back half-known)
            List<ItemDrop.ItemData> back = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData it in Cells.GetAllItems()) if (Fighting(it)) back.Add(it);
            if (back.Count == 0) return;
            Player p = InUse ? Player.m_localPlayer : null;
            foreach (ItemDrop.ItemData it in back)
            {
                Cells.RemoveItem(it);
                if (p == null || !p.GetInventory().AddItem(it)) ItemDrop.DropItem(it, it.m_stack, transform.position + transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            }
            if (p != null) p.Message(MessageHud.MessageType.Center, Lang.F("{0} не битиметься — зброя ні до чого.", _a.Name));
            Npcs.Log(_a.Id + ": no weapons for one who never fights — " + back.Count + " given back");
        }

        private static bool Fighting(ItemDrop.ItemData it)
        {
            if (IsAmmo(it) || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) return true;
            if (!it.IsWeapon() || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch) return false; // (a torch is a light)
            return it.m_shared.m_skillType != Skills.SkillType.Axes && it.m_shared.m_skillType != Skills.SkillType.Pickaxes; // (an axe, a pickaxe: its work's tools)
        }

        internal static readonly Dictionary<Container, NpcGear> Boxes = new Dictionary<Container, NpcGear>(); // (the persons' cells among the game's chests)
        private static readonly System.Reflection.FieldInfo LoadingF = AccessTools.Field(typeof(Container), "m_loading");
        private void Loading(bool on) { if (LoadingF != null) LoadingF.SetValue(_box, on); }
        private string _kept; // what its locker holds as last read or written here

        private void OnDestroy() { if (_box != null) Boxes.Remove(_box); }

        // the game's chest keeps its cells: into the locker (where it is owned — or where they were opened, which owns it then)
        internal void Keep()
        {
            if (_a == null || !_a.Owned || Cells == null || !NpcLocker.Ready(_a) || (_kept == null && NpcLocker.Has(_a))) return; // (never written before its locker was read here)
            string s = "";
            if (Cells.NrOfItems() > 0) { ZPackage p = new ZPackage(); Cells.Save(p); s = p.GetBase64(); }
            if (s == _kept) return;
            if (NpcLocker.Set(_a, ZDOVars.s_items, s)) _kept = s;
        }

        // …and reads them (every game, once a second: the game's own look for changes)
        internal void Read()
        {
            if (_a == null || Cells == null) return;
            string s = NpcLocker.Get(_a, ZDOVars.s_items);
            if (s == null || s == _kept) return; // (not here yet, or as it is)
            _kept = s;
            Loading(true); // (its own reading is no change to keep)
            try { if (s.Length == 0) Cells.RemoveAll(); else Cells.Load(new ZPackage(s)); }
            catch (System.Exception e) { Npcs.Log(_a.Id + ": its gear could not be read: " + e.Message); }
            finally { Loading(false); }
        }

        private void Awake()
        {
            _box = GetComponent<Container>(); _a = GetComponent<NpcActor>();
            if (_box != null) Boxes[_box] = this;
            Inventory cells = Cells;
            if (cells != null) cells.m_onChanged = (System.Action)System.Delegate.Combine(cells.m_onChanged, new System.Action(() => _changed = true)); // (looked at only when they changed)
        }

        // its cells open in a window: the game that opened them owns it now (the game's chest does so), so it is asked here —
        // not the ZDO's «in use», which stays set when that game drops off
        internal bool InUse { get { return _box != null && InventoryGui.IsVisible() && OpenBox != null && (Container)OpenBox.GetValue(InventoryGui.instance) == _box; } }
        internal Inventory Cells { get { return _box != null ? _box.GetInventory() : null; } }

        internal void Open(Player p) { if (_box != null && p != null) _box.Interact(p, false, false); }

        // where it is run: what is in its cells changed → dressed again; what it shot → taken from the cells
        private void Update()
        {
            if (_a == null || !_a.Owned || Cells == null || Time.time < _next) return;
            _next = Time.time + 1f;
            NpcLocker.Check(_a); // (its locker gone for good: let go of — NpcLocker)
            Humanoid me = GetComponent<Humanoid>();
            if (_sheathe && me != null && !me.InAttack() && (_a.Mind == null || _a.Mind.NowLayer != NpcMind.Layer.Fight)) { _sheathe = false; if (!InHand(_a)) Sheathe(me, true); }
            if (CanFight(_a) != _fought) { _fought = CanFight(_a); _a.Redress(); } // (a lever, its job: may fight now, or not — dressed again)
            NpcLocker.Keep(_a); // (its locker after it)
            if (_changed)
            {
                _changed = false;
                NoWeapons();
                string sig = Signature();
                if (sig != _seen) { _seen = sig; _a.Redress(); return; }
            }
            SyncAmmo();
        }

        // what is in its cells (arrows and bolts by their count: more put in, or taken out, dresses it again)
        private string Signature()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (ItemDrop.ItemData it in Cells.GetAllItems()) sb.Append(it.m_shared.m_name).Append(IsAmmo(it) ? it.m_stack : it.m_quality).Append(';');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- what it wears (NpcActor.Dress)
        // its template's clothes, piece by piece replaced by better from its cells; its weapons; its arrows/bolts
        internal List<ItemDrop.ItemData> Choose(List<ItemDrop.ItemData> template)
        {
            List<ItemDrop.ItemData> wear = new List<ItemDrop.ItemData>();
            List<ItemDrop.ItemData> cells = Cells != null ? new List<ItemDrop.ItemData>(Cells.GetAllItems()) : new List<ItemDrop.ItemData>();
            // armour: per slot, the best (its template's piece counts too)
            foreach (ItemDrop.ItemData.ItemType slot in new[] { ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs, ItemDrop.ItemData.ItemType.Shoulder, ItemDrop.ItemData.ItemType.Utility })
            {
                ItemDrop.ItemData best = null;
                foreach (ItemDrop.ItemData it in Concat(template, cells)) if (it.m_shared.m_itemType == slot && (best == null || it.GetArmor() > best.GetArmor())) best = it;
                if (best != null) wear.Add(best.Clone());
            }
            // weapons: the best near one; the best far one it has ammunition for; a shield for a one-handed near one
            NoWeapons(); // (before its dressing clears the change mark: one who never fights gives them back now)
            ItemDrop.ItemData near = null, far = null, shield = null;
            bool fights = CanFight(_a);
            _fought = fights;
            foreach (ItemDrop.ItemData it in Concat(template, cells))
            {
                if (!fights) break; // (one who never fights wields nothing)
                if (it.m_shared.m_useDurability && it.m_durability <= 0f) continue; // (broken: not taken up)
                if (!it.IsWeapon()) { if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && (shield == null || it.m_shared.m_blockPower > shield.m_shared.m_blockPower)) shield = it; continue; }
                if (Ranged(it)) { if (HasAmmo(cells, it) && (far == null || Power(it) > Power(far))) far = it; }
                else if (near == null || Power(it) > Power(near)) near = it;
            }
            if (near != null) wear.Add(near.Clone());
            if (far != null) wear.Add(far.Clone());
            if (shield != null && (near == null || !near.IsTwoHanded())) wear.Add(shield.Clone());
            if (fights) foreach (ItemDrop.ItemData it in cells) if (IsAmmo(it)) wear.Add(it.Clone()); // (what it shoots: all of it)
            _ammoSeen.Clear();
            if (fights) foreach (ItemDrop.ItemData it in cells) if (IsAmmo(it)) { int n; _ammoSeen.TryGetValue(it.m_shared.m_name, out n); _ammoSeen[it.m_shared.m_name] = n + it.m_stack; } // (only what it carries to shoot — else its cells' arrows were taken as shot)
            if (Cells != null) { _seen = Signature(); _changed = false; } // (dressed from these: no second dressing for them)
            return wear;
        }

        // what the body shot since: taken from the cells too (they are the ones kept)
        private void SyncAmmo()
        {
            Humanoid body = GetComponent<Humanoid>();
            if (body == null || _ammoSeen.Count == 0) return;
            foreach (string name in new List<string>(_ammoSeen.Keys))
            {
                int now = body.GetInventory().CountItems(name), was = _ammoSeen[name];
                if (now >= was) continue;
                Cells.RemoveItem(name, was - now);
                _ammoSeen[name] = now;
                _seen = Signature(); _changed = false; // (its own shots: no dressing again)
            }
        }

        internal static bool Ranged(ItemDrop.ItemData it) { return it != null && it.m_shared.m_attack != null && it.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile && !string.IsNullOrEmpty(it.m_shared.m_ammoType); }
        private static bool IsAmmo(ItemDrop.ItemData it) { return it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable; }
        private static bool HasAmmo(List<ItemDrop.ItemData> cells, ItemDrop.ItemData weapon) { foreach (ItemDrop.ItemData it in cells) if (IsAmmo(it) && it.m_shared.m_ammoType == weapon.m_shared.m_ammoType) return true; return false; }
        private static float Power(ItemDrop.ItemData it) { return it.GetDamage().GetTotalDamage(); }
        private static IEnumerable<ItemDrop.ItemData> Concat(List<ItemDrop.ItemData> a, List<ItemDrop.ItemData> b) { foreach (ItemDrop.ItemData x in a) yield return x; foreach (ItemDrop.ItemData x in b) yield return x; }

        // in a fight: the far weapon while the foe is far, the near one when it comes close (only when it has both)
        internal void Wield(Humanoid body, bool far)
        {
            if (body == null || Near == null || Far == null) return;
            ItemDrop.ItemData want = far ? Far : Near;
            if (body.IsItemEquiped(want) || body.InAttack()) return;
            body.EquipItem(want, false);
            if (!far && Shield != null && !Near.IsTwoHanded() && !body.IsItemEquiped(Shield)) body.EquipItem(Shield, false); // (the bow took its left hand: the shield back)
        }

        // sent off: its cells put down at its feet
        internal void DropAll()
        {
            if (Cells == null || _a == null || !_a.Owned) return;
            if (!NpcLocker.Ready(_a)) { Npcs.Log(_a.Id + ": its locker is not here yet — its gear stays in it"); return; }
            Read(); // (what the locker holds now, not what was last seen)
            if (Cells.NrOfItems() == 0) return;
            Vector3 at = transform.position + transform.forward * 0.8f + Vector3.up * 0.5f;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(Cells.GetAllItems())) ItemDrop.DropItem(it, it.m_stack, at, Quaternion.identity);
            Cells.RemoveAll();
            Npcs.Log((_a.Def != null ? _a.Id : "npc") + ": put down its gear");
        }

        // its button on the person's round menu
        internal static IEnumerable<NpcMenu.Button> MenuButtons()
        {
            yield return new NpcMenu.Button { Order = 85, Title = "Спорядження", Icon = "n-gear", AnyTime = true,
                Desc = a => !CanFight(a) ? Lang.T("Відкриває десять комірок поруч із твоєю торбою: броня, одяг, сокира чи кайло для роботи. Не боєць — зброї не візьме.") : Lang.T("Відкриває його десять комірок поруч із твоєю торбою: поклади зброю, щит, броню, стріли чи болти — він вдягне найкраще. Лук чи арбалет бере лише зі стрілами чи болтами. Лісорубу — сокиру, рудокопу — кайло; вони зношуються — забери й полагодь на верстаті або дай нові."),
                Now = a => { NpcGear g = a.Gear; return g != null && g.Cells != null && g.Cells.NrOfItems() > 0; },
                Do = (a, p) => { NpcGear g = a.Gear; if (g != null) g.Open(p); return null; } }; // (it stands still while its cells are open: NpcMind)
        }
    }

    // SeneaL UI shows a chest's cells under the hover text of anything with a Container in it — a person is no chest:
    // its cells are opened from its menu (Спорядження). Without SeneaL UI this patch is skipped.
    [HarmonyPatch]
    internal static class NpcGearNoSenealPreview
    {
        private static System.Reflection.MethodBase _target;
        private static bool Prepare()
        {
            System.Type t = AccessTools.TypeByName("SeneaLUI.Hud.HoverView");
            _target = t != null ? AccessTools.Method(t, "Previewable", new System.Type[] { typeof(Container) }) : null;
            return _target != null;
        }
        private static System.Reflection.MethodBase TargetMethod() { return _target; }
        private static void Postfix(Container __0, ref bool __result) { if (__result && __0 != null && NpcGear.Boxes.ContainsKey(__0)) __result = false; }
    }

    // a person's cells kept in its locker, not in its own ZDO (the game's chest: Save on a change, Load once a second)
    [HarmonyPatch(typeof(Container), "Save")]
    internal static class NpcGearKeepPatch
    {
        private static bool Prefix(Container __instance) { NpcGear g; if (NpcGear.Boxes.Count == 0 || !NpcGear.Boxes.TryGetValue(__instance, out g)) return true; g.Keep(); return false; }
    }

    [HarmonyPatch(typeof(Container), "Load")]
    internal static class NpcGearReadPatch
    {
        private static bool Prefix(Container __instance) { NpcGear g; if (NpcGear.Boxes.Count == 0 || !NpcGear.Boxes.TryGetValue(__instance, out g)) return true; g.Read(); return false; }
    }

    // a person never counts as «not loaded» (the game's own: only a player loads a crossbow): its reload is its pause
    // between shots (NpcMind), not a key held
    [HarmonyPatch(typeof(Humanoid), "IsWeaponLoaded")]
    internal static class NpcLoadedPatch
    {
        private static void Postfix(Humanoid __instance, ref bool __result)
        {
            try { if (!__result && NpcActor.Live.Count > 0 && NpcActor.Of(__instance) != null) __result = true; }
            catch (System.Exception e) { PatchGuard.Fail("NpcLoadedPatch", e); }
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Who gets what (RoleOf, the user 2026-11-06): one on guard duty all of what follows. Anyone else keeps here only what NpcTalkDress puts in, with no window of the game: its tool or one melee weapon
    // (Tool: it has work; Hand: it has none and can stand up for itself; None: no button at all) and its clothes, one a slot, worn for their look with no stats at all (Plain). Anything else in its cells goes back (NoWeapons).
    //
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
        // one who may fight can carry weapons (a fighter by its template, a settler by its nature: NpcNature.MayFight); one who only runs
        // (the old elder: it runs) gets none — armour, clothes and its work's tools only. A guard on duty walks and stands with them in hand,
        // ready, its pose its own calm one (NpcStance); off duty — a meal, its sleep, its pay, its ease — they go on its back (the user 2026-11-08, «вартовий це
        // вартовий… але коли йде їсти або спати, ховає зброю за спину»): InHand, put right in Update. The others keep them on the back, draw them for a fight
        // (NpcMind) and put them back after
        internal static bool CanFight(NpcActor a)
        {
            if (a == null || a.Def == null) return false;
            if (Guard.Is(a)) return true;
            return NpcNature.MayFight(a) && NpcLevers.NoJob(a, "guard") == null; // (read from what it is: its mind holds what this says, NpcNature.SyncFight)
        }
        // a guard on duty, or anyone of its kind on its guard (NpcAlert): its post, its rounds, an order, a fight, a scene, a short whim where it stands at its post
        internal static bool InHand(NpcActor a)
        {
            if (!Guard.Is(a)) return false;
            if (NpcAlert.On(a)) return true;
            NpcMind.Layer l = NpcMind.LayerNow(a);
            if (l == NpcMind.Layer.Job || l == NpcMind.Layer.Order || l == NpcMind.Layer.Scene) return true; // (a fight: NpcAlert above)
            if (l != NpcMind.Layer.Whim || a.Mind == null || a.Mind.SeatedNow || a.Mind.Asleep) return false; // (a meal, a rest, its pay, its ease: off duty)
            return Guard.Watch(a, a.transform.position) == true; // (a stretch at its post is still its watch)
        }

        // what its hands hold now, for a pose that suits them (NpcStance.Pose.Hands): the game's own stance for its weapon — the animator's state the game
        // plays under any clip of ours (measured 2026-11-08: Knud's knives crouch it, hips at 0.75 m, feet wide, a hand up before it) — «free», else the
        // weapon's animation state in small letters (onehanded, knives, greatsword, atgeir, bow…), «+shield» with a shield in its other hand
        internal const string HandsFree = "free";
        internal static string HandsOf(NpcActor a)
        {
            Humanoid h = a != null ? a.GetComponent<Character>() as Humanoid : null;
            ItemDrop.ItemData w = h != null ? h.GetCurrentWeapon() : null;
            if (h == null || w == null || (h.m_unarmedWeapon != null && w == h.m_unarmedWeapon.m_itemData)) return HandsFree;
            string kind = w.m_shared.m_animationState.ToString().ToLowerInvariant();
            foreach (ItemDrop.ItemData it in h.GetInventory().GetEquippedItems()) if (it != w && it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) return kind + "+shield";
            return kind;
        }

        // a weapon (and shield) put in a body's hands by their prefab names, not from its cells: its hands emptied first ("" — nothing). The one way for
        // a body not dressed from its cells — the stage's mannequin (raven stage wield), and a scene's when one asks it. Null: done; else why not
        internal static string Arm(NpcActor a, string weapon, string shield)
        {
            Humanoid body = a != null ? a.GetComponent<Character>() as Humanoid : null;
            if (body == null) return "Нема тіла людини.";
            body.UnequipAllItems();
            body.GetInventory().RemoveAll();
            Back(body, null, null);
            foreach (string name in new[] { weapon, shield })
            {
                if (string.IsNullOrEmpty(name)) continue;
                GameObject pf = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
                ItemDrop d = pf != null ? pf.GetComponent<ItemDrop>() : null;
                if (d == null || !d.m_itemData.IsWeapon() && d.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield) return "Нема зброї чи щита «" + name + "» (назва префаба гри: KnifeSkollAndHati, SwordIron, ShieldBanded…).";
                ItemDrop.ItemData it = d.m_itemData.Clone();
                it.m_dropPrefab = pf; it.m_stack = 1;
                if (!body.GetInventory().AddItem(it)) return "«" + name + "» не лягло в торбу.";
                body.EquipItem(it, false);
            }
            return null;
        }

        // where the weapon in its right hand points, measured (2026-11-08: a pole laid on the shoulder by a guessed wrist went nowhere near it): from its grip to its
        // farthest point, in the body's frame (x right, y up, z ahead) and in the hand's own (along its fingers · out of its palm · to its side) — what a clip's
        // @RightHand.f / .n must turn so it points where the pose wants it. Read when asked (the stage's wield, its words)
        private static readonly System.Reflection.FieldInfo RightInstance = AccessTools.Field(typeof(VisEquipment), "m_rightItemInstance");
        internal static string HeldAxis(NpcActor a)
        {
            Character c = a != null ? a.GetComponent<Character>() : null;
            VisEquipment ve = c != null ? c.GetComponent<VisEquipment>() : null;
            GameObject item = ve != null && RightInstance != null ? RightInstance.GetValue(ve) as GameObject : null;
            Transform hand = BodyClips.SkeletonBone(c, null, "RightHand");
            BodyAnimator an = BodyAnimator.Of(c);
            Vector3 along, palm;
            if (item == null || hand == null || an == null || !an.HandFacing(false, hand, out along, out palm)) return "у правій руці нічого не заміряти";
            Vector3 tip = hand.position; float far = 0f;
            foreach (MeshFilter mf in item.GetComponentsInChildren<MeshFilter>()) // heavy: once a command, one weapon's few meshes
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p = mf.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    float d = (p - hand.position).sqrMagnitude;
                    if (d > far) { far = d; tip = p; }
                }
            }
            Vector3 axis = (tip - hand.position).normalized, f = along.normalized, n = palm.normalized, side = Vector3.Cross(f, n).normalized;
            Vector3 body = c.transform.InverseTransformDirection(axis), fb = c.transform.InverseTransformDirection(f), nb = c.transform.InverseTransformDirection(n);
            System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InvariantCulture;
            System.Func<Vector3, string> V = v => v.x.ToString("0.00", ci) + " " + v.y.ToString("0.00", ci) + " " + v.z.ToString("0.00", ci);
            return "права: вістря від кисті " + Mathf.Sqrt(far).ToString("0.00", ci) + " м, у тілі " + V(body) + "; у кисті (пальці · долоня · бік) " + V(new Vector3(Vector3.Dot(axis, f), Vector3.Dot(axis, n), Vector3.Dot(axis, side))) + "; кисть зараз .f " + V(fb) + " .n " + V(nb);
        }

        // the game's stance for a weapon is the animator's «statei»/«statef», set by the game only as a weapon is taken up (measured 2026-11-08: a value set
        // stays). For some the game stands ready to strike even at its ease — knives crouched, a crossbow raised: at its ease (not on its guard: NpcAlert) a person
        // holding one stands as with a sword instead, and walks so; on its guard its own comes back and the game's animator blends into it. Every game draws
        // its own people: run on each (NpcModules, Everywhere), a look each StanceEvery s, the animator written only as it changes. A weapon carried on the
        // shoulder by its own stance (a greatsword, a sledge) and the atgeir keep theirs: their calm poses ride on it (NpcStance)
        internal static readonly HashSet<ItemDrop.ItemData.AnimationState> CalmAsSword = new HashSet<ItemDrop.ItemData.AnimationState> {
            ItemDrop.ItemData.AnimationState.Knives, ItemDrop.ItemData.AnimationState.DualAxes, ItemDrop.ItemData.AnimationState.Crossbow,
            ItemDrop.ItemData.AnimationState.Staves, ItemDrop.ItemData.AnimationState.TwoHandedAxe, ItemDrop.ItemData.AnimationState.Atgeir };

        // a weapon carried its own way at its ease, standing and walking alike (the user 2026-11-08: the atgeir on the shoulder in a walk too): a clip of the
        // arms only, «carry yes», over the calm stance (Stance). Where it is run, a look each CarryEvery s: at its ease with such a weapon — played (its term
        // renewed, never started over); on its guard, the weapon put away, another one in hand — let go. Another clip on its body (a gesture, a scene) is not cut
        internal static readonly Dictionary<ItemDrop.ItemData.AnimationState, string> Carries = new Dictionary<ItemDrop.ItemData.AnimationState, string> {
            { ItemDrop.ItemData.AnimationState.Atgeir, "guard_atgeir_carry" } };
        internal static float CarryEvery = 0.25f, CarryTerm = 30f;
        private float _carryNext, _carryRenew;
        internal static void Carry(NpcActor a)
        {
            NpcGear g = a != null ? a.Gear : null;
            if (g == null || a.Body == null || Time.time < g._carryNext) return;
            g._carryNext = Time.time + CarryEvery;
            Humanoid h = a.GetComponent<Character>() as Humanoid;
            ItemDrop.ItemData w = h != null ? h.GetCurrentWeapon() : null;
            string want = null;
            if (w != null && (h.m_unarmedWeapon == null || w != h.m_unarmedWeapon.m_itemData) && !NpcAlert.On(a)) Carries.TryGetValue(w.m_shared.m_animationState, out want);
            BodyAnimator an = BodyAnimator.Of(a.GetComponent<Character>());
            string now = an != null ? an.ClipIn(NpcBody.ClipSlot) : null;
            bool ours = now != null && Carries.ContainsValue(now);
            if (want == null) { if (ours) a.Body.Clip(""); return; }
            if (now != null && !ours && a.Body.ClipBusy) return; // (a gesture, a place's clip, a scene: theirs first — carried again after)
            if (now == want && Time.time < g._carryRenew) return; // (its term renewed only near its end: each renewal is a word in its ZDO)
            if (a.Body.Clip(want, CarryTerm)) g._carryRenew = Time.time + CarryTerm - 10f;
        }
        // the game blends nothing as the value changes (the user 2026-11-08: «анімація порвалась з бойової на звичайну»): the body's pose is held first
        // (stance_settle: a frozen base of every bone, on its own layer of this game — no word sent: each game changes the stance itself) and the stance is
        // changed once it holds in full, under it; the held pose lets go over its fade — from the old stance into the new one
        internal static float StanceEvery = 0.5f;
        internal const string Settle = "stance_settle";
        private float _stanceNext;
        private int _stanceWant = -1; private float _stanceSwap;
        private Animator _anim;
        internal static void Stance(NpcActor a)
        {
            NpcGear g = a != null ? a.Gear : null;
            if (g == null) return;
            if (g._stanceWant >= 0 && Time.time >= g._stanceSwap) // (its pose held in full now: the stance changed under it)
            {
                if (g._anim != null) { g._anim.SetInteger(StateI, g._stanceWant); g._anim.SetFloat(StateF, g._stanceWant); }
                g._stanceWant = -1;
                return;
            }
            if (g._stanceWant >= 0 || Time.time < g._stanceNext) return;
            g._stanceNext = Time.time + StanceEvery;
            Humanoid h = a.GetComponent<Character>() as Humanoid;
            ItemDrop.ItemData w = h != null ? h.GetCurrentWeapon() : null;
            if (w == null || (h.m_unarmedWeapon != null && w == h.m_unarmedWeapon.m_itemData)) return;
            if (g._anim == null) g._anim = a.GetComponentInChildren<Animator>();
            if (g._anim == null) return;
            ItemDrop.ItemData.AnimationState real = w.m_shared.m_animationState;
            int want = (int)(CalmAsSword.Contains(real) && !NpcAlert.On(a) ? ItemDrop.ItemData.AnimationState.OneHanded : real);
            if (g._anim.GetInteger(StateI) == want) return;
            if (BodyClips.Find(Settle) == null || !NpcClock.Seen(a)) { g._anim.SetInteger(StateI, want); g._anim.SetFloat(StateF, want); return; } // (nobody sees it: no easing to pay for)
            BodyClips.Play(h, BodyClips.SettleSlot, Settle, 0.01f, 1f); // (its pose now held; the stance changes next frame)
            g._stanceWant = want; g._stanceSwap = Time.time + 0.06f; // (the hold in full first: its fade in is 0.05 s)
        }
        private static readonly int StateI = Animator.StringToHash("statei"), StateF = Animator.StringToHash("statef");

        private bool Holding(Humanoid me) { return (Near != null && me.IsItemEquiped(Near)) || (Far != null && me.IsItemEquiped(Far)); }

        internal enum Role { None, Hand, Tool, Full }
        internal static Role RoleOf(NpcActor a)
        {
            if (Guard.Is(a)) return Role.Full;
            if (a != null && a.Worker != null && a.Worker.Works) return Role.Tool;
            return CanFight(a) ? Role.Hand : Role.None;
        }

        // what its one cell may hold
        internal static bool HandFits(Role role, ItemDrop.ItemData it)
        {
            if (it == null || !it.IsWeapon() || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch) return false;
            bool tool = it.m_shared.m_skillType == Skills.SkillType.Axes || it.m_shared.m_skillType == Skills.SkillType.Pickaxes;
            return role == Role.Tool ? tool : role == Role.Hand && (it.m_shared.m_attack == null || it.m_shared.m_attack.m_attackType != Attack.AttackType.Projectile); // (melee: a staff or a thrown thing shoots with no ammunition, Ranged would not see it)
        }

        internal static bool IsClothes(ItemDrop.ItemData it)
        {
            ItemDrop.ItemData.ItemType t = it.m_shared.m_itemType;
            return t == ItemDrop.ItemData.ItemType.Helmet || t == ItemDrop.ItemData.ItemType.Chest || t == ItemDrop.ItemData.ItemType.Legs || t == ItemDrop.ItemData.ItemType.Shoulder;
        }

        // for its look only: the same thing with nothing in it (the shared part is every item's of that name: a copy of it for this one)
        private static readonly System.Reflection.MethodInfo CopyShared = typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static ItemDrop.ItemData Plain(ItemDrop.ItemData it)
        {
            ItemDrop.ItemData c = it.Clone();
            ItemDrop.ItemData.SharedData s = (ItemDrop.ItemData.SharedData)CopyShared.Invoke(it.m_shared, null);
            s.m_armor = 0f; s.m_armorPerLevel = 0f; s.m_damageModifiers = new List<HitData.DamageModPair>();
            s.m_equipStatusEffect = null; s.m_setStatusEffect = null; s.m_setName = ""; s.m_setSize = 0; s.m_movementModifier = 0f;
            c.m_shared = s;
            return c;
        }

        // a thing a person can have on it (a template's gear, the look window's lists): the game's own items have an icon; a
        // creature's attack is an item too (an «item» of the game's list: a Writhan's blast put on Гейд by the look window), with none
        internal static bool Wearable(ItemDrop d)
        {
            ItemDrop.ItemData.SharedData s = d != null && d.m_itemData != null ? d.m_itemData.m_shared : null;
            return s != null && s.m_icons != null && s.m_icons.Length > 0 && s.m_itemType != ItemDrop.ItemData.ItemType.Customization;
        }

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
        private Role _role = Role.None; // (as last dressed)
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
                Back(body, null, null); // (back on duty from its back: off its back as Draw does — else in hand and on its back at once)
                return;
            }
            if (body.InAttack()) { g._sheathe = true; return; } // (the killing swing still going: put away once it is done)
            Sheathe(body, true);
        }

        // one not on guard: what its role has no place for put back in your bag (its job changed: a guard no more). One cell for its hand, one thing a slot
        private void NoWeapons()
        {
            Role role = RoleOf(_a);
            if (role == Role.Full || Cells == null || !NpcLocker.Ready(_a) || (_kept == null && NpcLocker.Has(_a))) return; // (its cells read here first: never written back half-known)
            List<ItemDrop.ItemData> back = new List<ItemDrop.ItemData>();
            HashSet<ItemDrop.ItemData.ItemType> worn = new HashSet<ItemDrop.ItemData.ItemType>();
            bool hand = false;
            foreach (ItemDrop.ItemData it in Cells.GetAllItems())
            {
                if (IsClothes(it)) { if (!worn.Add(it.m_shared.m_itemType)) back.Add(it); continue; }
                if (!hand && HandFits(role, it)) { hand = true; continue; }
                back.Add(it);
            }
            if (back.Count == 0) return;
            Player p = InUse ? Player.m_localPlayer : null;
            foreach (ItemDrop.ItemData it in back)
            {
                Cells.RemoveItem(it);
                if (p == null || !p.GetInventory().AddItem(it)) ItemDrop.DropItem(it, it.m_stack, transform.position + transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            }
            Npcs.Log(_a.Id + ": no place for " + back.Count + " of its things (" + role + "): given back");
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
            if (_sheathe && me != null && !me.InAttack() && !NpcAlert.On(_a)) { _sheathe = false; if (!InHand(_a)) Sheathe(me, true); }
            if (me != null && Guard.Is(_a) && !me.InAttack() && !NpcAlert.On(_a) && (Near != null || Far != null) && Holding(me) != InHand(_a)) AtEase(_a, me); // (a guard on duty or off it: its weapons out or on its back — the one place; on its guard its fight's own is Draw)
            if (CanFight(_a) != _fought || RoleOf(_a) != _role) { _fought = CanFight(_a); _role = RoleOf(_a); _a.Redress(); } // (a lever, its job: may fight now, or not; on guard no more: dressed again, what has no place now given back)
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
            NoWeapons(); // (first: what goes back is not worn; before its dressing clears the change mark)
            _role = RoleOf(_a);
            List<ItemDrop.ItemData> cells = Cells != null ? new List<ItemDrop.ItemData>(Cells.GetAllItems()) : new List<ItemDrop.ItemData>();
            bool forLook = RoleOf(_a) != Role.Full; // (one not on guard: what you put on it is worn over what it came in, for its look alone)
            // armour: per slot, the best (its template's piece counts too)
            foreach (ItemDrop.ItemData.ItemType slot in new[] { ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs, ItemDrop.ItemData.ItemType.Shoulder, ItemDrop.ItemData.ItemType.Utility })
            {
                ItemDrop.ItemData best = null;
                if (forLook)
                {
                    ItemDrop.ItemData mine = cells.Find(x => x.m_shared.m_itemType == slot);
                    if (mine != null) { wear.Add(Plain(mine)); continue; }
                }
                foreach (ItemDrop.ItemData it in forLook ? (IEnumerable<ItemDrop.ItemData>)template : Concat(template, cells)) if (it.m_shared.m_itemType == slot && (best == null || it.GetArmor() > best.GetArmor())) best = it;
                if (best != null) wear.Add(best.Clone());
            }
            // weapons: the best near one; the best far one it has ammunition for; a shield for a one-handed near one
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
            yield return new NpcMenu.Button { Order = 70, Title = Lang.K("Спорядження"), Icon = "n-gear", AnyTime = true,
                Shown = a => RoleOf(a) != Role.None,
                Not = a => RoleOf(a) != Role.Full && NpcVitals.IsDown(a) ? Lang.T("Без тями — спершу постав його на ноги (медовуха зцілення, E).") : null,
                Desc = a => { Role r = RoleOf(a); return r == Role.Tool ? Lang.T("Сокира чи кайло для роботи. Вони зношуються: забери й полагодь на верстаті або дай нові.") : r == Role.Hand ? Lang.T("Одна зброя ближнього бою, щоб міг відбитись.") : Lang.T("Відкриває його десять комірок поруч із твоєю торбою: поклади зброю, щит, броню, стріли чи болти — він вдягне найкраще. Лук чи арбалет бере лише зі стрілами чи болтами."); },
                Now = a => { NpcGear g = a.Gear; return g != null && g.Cells != null && g.Cells.NrOfItems() > 0; },
                Do = (a, p) => { NpcGear g = a.Gear; if (g == null) return null; if (RoleOf(a) == Role.Full) g.Open(p); else NpcTalk.OpenHand(a, p); return null; } }; // (it stands still while its cells are open: NpcMind)
        }
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

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // An NPC in the world (Npcs.cs): who it is (from its ZDO), how it looks, what you see pointing at it, what E does,
    // what it says. Its mind (NpcMind) moves it; orders reach the mind through here wherever they are given: on the
    // owner's game they go straight in, elsewhere they are sent to the owner.
    internal sealed class NpcActor : MonoBehaviour, Hoverable, Interactable
    {
        internal static readonly List<NpcActor> Live = new List<NpcActor>();
        private static readonly Dictionary<Character, NpcActor> ByBody = new Dictionary<Character, NpcActor>();
        private const string RpcSay = "RavenWatch_NpcSay", RpcOrder = "RavenWatch_NpcOrder", RpcTalk = "RavenWatch_NpcTalk", RpcAppear = "RavenWatch_NpcAppear";
        internal const string RpcAct = "RavenWatch_NpcAct";

        public EffectList m_vanish = new EffectList(); // (Odin's: his puff of smoke)

        private ZNetView _nv;
        private Humanoid _body;
        private NpcMind _mind;
        private NpcDef _def;
        private bool _dressed, _minded, _wasOwned;
        private float _nextSay, _nextRules, _nextHitRule, _nextBind, _nextStatus;
        internal System.Collections.Generic.HashSet<string> StoryFacts; internal string StoryFactsOf; // (what its story says of it: NpcBackstory.Fact)
        internal bool EyesOn; internal float EyesLook, EyesFar; // (a player near enough to see it: NpcClock.Seen)
        internal float NextRemark, NextLook; internal string LastRemark = ""; internal int Stared; // (a player standing by it saying nothing: looks in a row) // (its own words unasked: NpcVoice)
        internal static readonly int KStatus = "RavenWatch_npcStatus".GetStableHashCode(); // string: what it does now (its owner's words)

        // what it does now, on any game: its own mind where it is run, else as its owner last said
        internal string StatusNow { get { if (Owned && _mind != null) return _mind.Describe(); ZDO z = Zdo; return z != null ? z.GetString(KStatus, "") : ""; } }

        // ...as a player reads it: its words stay Ukrainian where they are kept (the ZDO, the log, what modules look for in
        // them), each part turned into the reader's language here — «шар: що робить (поза)» — then into its own form
        // («прийшов|прийшла»). The words are marked Lang.K where they are set (NpcMind, its modules' errands).
        internal string StatusText
        {
            get
            {
                string s = StatusNow;
                if (string.IsNullOrEmpty(s)) return s;
                s = NpcMind.WithoutLayer(s); // («сцена: стоїть»: the layer's word is for the log and the window)
                int colon = s.IndexOf(": ", StringComparison.Ordinal);
                string head = colon > 0 ? Lang.T(s.Substring(0, colon)) + ": " : "", rest = colon > 0 ? s.Substring(colon + 2) : s;
                int paren = rest.IndexOf(" (", StringComparison.Ordinal); // («стоїть (поза)»: the pose's own name stays)
                return Form(head + (paren > 0 ? Lang.T(rest.Substring(0, paren)) + rest.Substring(paren) : Lang.T(rest)));
            }
        }
        private int _daySeen = -1; // 0 day · 1 night (the habit for it given)
        private readonly List<ZDOID> _near = new List<ZDOID>();

        internal string Id { get { return _def != null ? _def.Id : ""; } }
        internal string Tag { get { ZDO z = Zdo; return z != null ? z.GetString(Npcs.KTag, "") : ""; } }
        internal ZDO Zdo { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; } }
        internal NpcDef Def { get { return _def; } }
        internal NpcMind Mind { get { return _mind; } }
        internal NpcBody Body { get { return _bodyL != null ? _bodyL : (_bodyL = GetComponent<NpcBody>()); } }
        private NpcBody _bodyL;
        private NpcResident _resident;
        internal NpcVitals Vitals { get { return _vitalsL != null ? _vitalsL : (_vitalsL = GetComponent<NpcVitals>()); } }
        private NpcVitals _vitalsL;
        internal static readonly int KArmor = "RavenWatch_npcArmor".GetStableHashCode(); // int: its armour as last dressed

        internal NpcGear Gear { get { return _gearL != null ? _gearL : (_gearL = GetComponent<NpcGear>()); } }
        private NpcGear _gearL;
        internal NpcHunger Hunger { get { return _hungerL != null ? _hungerL : (_hungerL = GetComponent<NpcHunger>()); } }
        private NpcHunger _hungerL;
        internal NpcWorker Worker { get { return _workerL != null ? _workerL : (_workerL = GetComponent<NpcWorker>()); } }
        private NpcWorker _workerL;
        internal bool Owned { get { return _nv != null && _nv.IsValid() && _nv.IsOwner(); } }
        internal NpcResident Resident { get { return _resident; } }
        internal NpcSettlers.Look LookKept; internal string LookRaw = ""; // (a settler's look as read: NpcSettlers.Of)

        // its name as shown: a settler's own, else its template's (in the player's language)
        // who it is in the log: its template, and its own name when that tells more (many settlers share «settler»)
        internal string Who { get { string n = Name; return n == Id ? Id : Id + " «" + n + "»"; } }
        internal string Name { get { string own = NpcSettlers.NameOf(this); return own.Length > 0 ? own : _def != null ? Lang.T(_def.Name) : Id; } }

        // a woman or a man (a settler's own, else its template's model) — and words in its form: «старанний|старанна»
        internal bool Female { get { NpcSettlers.Look l = NpcSettlers.Of(this); return l != null ? l.Model == 1 : _def != null && _def.Model == 1; } }
        private static readonly System.Text.RegularExpressions.Regex Forms = new System.Text.RegularExpressions.Regex(@"(\S+)\|(\S+)");
        internal string Form(string s) { if (string.IsNullOrEmpty(s) || s.IndexOf('|') < 0) return s; bool f = Female; return Forms.Replace(s, m => PickForm(m.Value, m.Groups[1].Value, m.Groups[2].Value, f)); }

        // «подарував|подарувала.»: the stop after the pair (and a quote before it) belongs to both forms; a player's {а|б} is not ours
        private static string PickForm(string all, string he, string she, bool female)
        {
            if (all.IndexOf('{') >= 0 || all.IndexOf('}') >= 0) return all;
            int lead = 0; while (lead < he.Length && !char.IsLetterOrDigit(he[lead])) lead++;
            int tail = she.Length; while (tail > 0 && !char.IsLetterOrDigit(she[tail - 1])) tail--;
            if (female) return (she.Length > 0 && char.IsLetterOrDigit(she[0]) ? he.Substring(0, lead) : "") + she;
            return he + (he.Length > 0 && char.IsLetterOrDigit(he[he.Length - 1]) ? she.Substring(tail) : "");
        }

        internal BodyGait.Style GaitKept; internal NpcPersona GaitFor; internal int GaitVer; internal string GaitForced = ""; // (its gait, made once per its ways: NpcGait.Of)
        internal NpcPersona PersonaKept; internal NpcDef PersonaFor; internal string PersonaStory = ""; internal uint PersonaSeed; internal string PersonaWritten; internal string PersonaLookRaw, PersonaTraitsRaw; // (its ways, made once: NpcPersona.Of)

        // the person a body belongs to (null: not one of ours)
        internal static NpcActor Of(Character c) { NpcActor a; return c != null && ByBody.TryGetValue(c, out a) ? a : null; }

        // the modules' words on it (NpcModules): an act run on the game that runs it; blows not reaching it now; it can be hurt
        internal static readonly Dictionary<string, Action<NpcActor, string[]>> Acts = new Dictionary<string, Action<NpcActor, string[]>>();
        internal static readonly List<Func<NpcActor, bool>> Unhurt = new List<Func<NpcActor, bool>>();
        internal static readonly List<Func<NpcActor, bool>> Hurtable = new List<Func<NpcActor, bool>>();

        internal static bool Immortal(Character c)
        {
            NpcActor a;
            if (c == null || !ByBody.TryGetValue(c, out a) || a == null) return false;
            for (int i = 0; i < Unhurt.Count; i++) if (Unhurt[i](a)) return true; // (a module says not now: NpcModules)
            if (a._def == null) return true;
            if (a._def.Mortal) return false; // (a story person meant to be hurt: as the game has it)
            for (int i = 0; i < Hurtable.Count; i++) if (Hurtable[i](a)) return false; // (hired, living at a base: it falls senseless, it does not die — NpcVitals)
            return true;
        }

        // The player's body gets up off the ground at its first moments, and only the game's Player ever tells it to stop («wakeup»):
        // a person has no Player (Npcs.BuildHuman), so it came into the world sitting (the ghost, 2026-11-04, seen through its fade). Before
        // its first frame: told, and its animator taken past the change, so nobody sees it. Once a body comes into the world or is loaded
        internal static float WakeSkip = 2f; // s
        private void Start()
        {
            if (_def != null && _def.Odin) return; // (not the player's body)
            Animator anim = GetComponentInChildren<Animator>();
            if (anim == null) return;
            anim.SetBool("wakeup", false);
            anim.Update(WakeSkip);
        }

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
            _body = GetComponent<Humanoid>();
            _mind = GetComponent<NpcMind>();
            _resident = GetComponent<NpcResident>();
            ZDO z = Zdo;
            if (z == null) return; // (a ghost with no ZDO)
            string id = z.GetString(Npcs.KId, "");
            if (id.Length == 0 && Npcs.NextId.Length > 0) { id = Npcs.NextId; z.Set(Npcs.KId, id); }
            _def = NpcDefs.Find(id);
            NpcKit.Born(z, _def); // (its lasting number and its seed from its first moment, where it is owned — born here, or an old save loaded here: what it is made of is seeded by it, NpcKit.Seed)
            if (_def == null) Npcs.Log("an npc «" + id + "» with no template here (another version?): it stands still");
            _nv.Register<string>(RpcSay, RPC_Say);
            _nv.Register<string>(RpcOrder, RPC_Order);
            _nv.Register<ZDOID>(RpcTalk, RPC_Talk);
            _nv.Register<string>(RpcAct, RPC_Act);
            _nv.Register<Vector3>(RpcAppear, RPC_Appear);
            if (_body != null)
            {
                ByBody[_body] = this;
                _walk0 = _body.m_walkSpeed; _speed0 = _body.m_speed; _run0 = _body.m_runSpeed; // (the body's own, before its template's)
                ApplyDef();
            }
            _seenId = id; _seenTraits = NpcLevers.Traits(this);
            float phase = NpcClock.Phase(this); // (its own step: the timers of all of them never ring in one frame)
            _nextBind = Time.time + phase * 0.5f; _nextRules = Time.time + phase * 0.5f; _nextStatus = Time.time + phase * 3f;
            Live.Add(this);
        }

        // its template on its body: name, health, its pace — its template's, and a little of its own (NpcPersona)
        private float _walk0, _speed0, _run0;
        private string _seenId = "", _seenTraits = "";
        internal void ApplyDef()
        {
            if (_body == null || _def == null) return;
            _body.m_name = Name;
            _body.m_health = _def.Health;
            float own = NpcPersona.Of(this).Walk * _pace;
            _body.m_walkSpeed = _walk0 * _def.Speed * _def.Walk * own; _body.m_speed = _speed0 * _def.Speed * (0.5f + own * 0.5f); _body.m_runSpeed = _run0 * _def.Speed * _def.Run;
        }

        // its gait's pace now (NpcGait: its kind, tired, glad): × its walk, where it is run
        private float _pace = 1f;
        internal void Pace(float mul)
        {
            mul = Mathf.Clamp(mul, 0.5f, 1.4f);
            if (Mathf.Abs(mul - _pace) < 0.01f) return;
            _pace = mul;
            ApplyDef();
        }

        // the story changed it (NpcLevers: another template, its traits): read anew on every game; where it is owned,
        // dressed again and its way of fighting the new template's
        internal void Rebind()
        {
            ZDO z = Zdo;
            if (z == null) return;
            string id = z.GetString(Npcs.KId, "");
            _seenId = id; _seenTraits = NpcLevers.Traits(this);
            NpcDef d = NpcDefs.Find(id);
            if (d == null) return;
            bool other = d != _def;
            _def = d;
            ApplyDef();
            if (other && Owned) { _dressed = false; if (_mind != null) _mind.Do("fight " + NpcNature.FightNow(this), NpcMind.Layer.Routine); Npcs.Log(Id + ": became " + id); }
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            Run(Unloaded); // (the modules' own: its map mark…)
            if (_body != null) ByBody.Remove(_body);
        }

        private bool _healed; private float _nextHeal;

        // the modules' parts in its life (NpcModules): steps before it is dressed, where it is run (in order; each true when
        // done — all of them again until all are), then each frame where it is run with a mind, and each frame on every game
        internal static readonly List<Func<NpcActor, bool>> BeforeDressed = new List<Func<NpcActor, bool>>();
        internal static readonly List<Action<NpcActor>> Removed = new List<Action<NpcActor>>();   // taken out of the world (where it is run, before its ZDO goes)
        internal static readonly List<Action<NpcActor>> Unloaded = new List<Action<NpcActor>>();  // gone from this game (out of range, removed)
        internal static readonly List<Func<NpcActor, float>> HeardFrom = new List<Func<NpcActor, float>>(); // metres its words are seen from (25 at least)
        internal static readonly List<Action<NpcActor>> Ticks = new List<Action<NpcActor>>();
        internal static readonly List<Action<NpcActor>> Everywhere = new List<Action<NpcActor>>();
        private bool _prepared;

        private bool Prepare()
        {
            for (int i = 0; i < BeforeDressed.Count; i++)
            {
                Func<NpcActor, bool> step = BeforeDressed[i];
                if (HookOff(step)) return false;
                try { if (!step(this)) return false; }
                catch (Exception e) { HookFailed(step, e); return false; }
            }
            return true;
        }

        // a module's part that fails: said in the log and left alone a while on this person — only it, the rest of its life
        // goes on (a throw in one used to end its whole frame, every frame). Nothing here unless one has failed.
        private const float HookPause = 30f;
        private Dictionary<Delegate, float> _hookOff;
        private bool HookOff(Delegate d) { float until; return _hookOff != null && _hookOff.TryGetValue(d, out until) && Time.time < until; }
        private void HookFailed(Delegate d, Exception e)
        {
            if (_hookOff == null) _hookOff = new Dictionary<Delegate, float>();
            _hookOff[d] = Time.time + HookPause;
            Npcs.Log(Id + ": part «" + d.Method.DeclaringType.Name + "." + d.Method.Name + "» failed, left alone " + HookPause + " s: " + e);
        }
        private void Run(List<Action<NpcActor>> parts)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                Action<NpcActor> part = parts[i];
                if (HookOff(part)) continue;
                try { part(this); }
                catch (Exception e) { HookFailed(part, e); }
            }
        }

        private static readonly int PerfActor = NpcPerf.Id("actor (кадр)"), PerfHabits = NpcPerf.Id("habits (звички, день мешканця)");

        private void Update()
        {
            long perf = NpcPerf.Now();
            Update0();
            NpcPerf.Add(PerfActor, perf);
        }

        private void Update0()
        {
            if (_waitText != null && Owned) SayWaiting();
            if (Time.time >= _nextBind)
            {
                _nextBind = Time.time + 0.5f; ZDO zb = Zdo;
                if (zb != null && (zb.GetString(Npcs.KId, "") != _seenId || NpcLevers.Traits(this) != _seenTraits)) Rebind(); // (the story's change, from any game)
                if (_body != null && _def != null && _body.m_name != Name) _body.m_name = Name; // (a settler's name come from its owner's game)
                if (!_healed && Owned && Time.time >= _nextHeal) { _nextHeal = Time.time + 5f; _healed = NpcKit.Heal(this); } // (links of an old save made lasting)
            }
            if (_def == null) return;
            if (_body != null)
            {
                if (!_prepared && Owned) _prepared = Prepare(); // (the modules' steps before it is dressed: NpcModules)
                if (Owned != _wasOwned) { _wasOwned = Owned; OwnerChanged(Owned); }
                if (!_dressed && Owned && _prepared) Dress(); // (dressed from its locker's cells, once they are there)
                if (!_minded && Owned && _mind != null) { _minded = _mind.Default(NpcNature.Manner(this)); if (_minded) NpcNature.SyncFight(this); } // (false: its kept plans not here yet — again next frame)
                if (Owned && Time.time >= _nextRules && NpcClock.Take()) { _nextRules = Time.time + 0.5f * NpcClock.Slow(this); long ph = NpcPerf.Now(); try { Habits(); } catch (Exception e) { _nextRules = Time.time + HookPause; Npcs.Log(Id + ": its habits failed, left alone " + HookPause + " s: " + e); } NpcPerf.Add(PerfHabits, ph); } // (its turn on the clock: fewer far from everyone)
                if (Owned && _mind != null) Run(Ticks); // (the modules' own, where it is run: its words…)
                if (Owned && _mind != null && Time.time >= _nextStatus) { _nextStatus = Time.time + 3f * NpcClock.Slow(this); ZDO zs = Zdo; string d = _mind.Describe(); if (zs != null && zs.GetString(KStatus, "") != d) zs.Set(KStatus, d); } // (what it does, for the other games: only when it changed, at most every 3 s)
                Run(Everywhere); // (the modules' own on every game: its mark on the map…)
            }
            else Face(); // (Odin: no mind, he only turns to you)
        }

        // run here from now on, or by another game from now on — the one place that hears of it: what it did goes by its ZDO,
        // nothing of the old owner's memory is taken for true (a body that was on a seat there, plans made there)
        private void OwnerChanged(bool here)
        {
            Npcs.Log(Id + " «" + Name + "»: run " + (here ? "here now" : "by another game now") + (Body != null && Body.Attached ? " (seated)" : "") + (_mind != null && _mind.Current != null ? ", doing " + _mind.Describe() : "")); // (seldom: a handover — the two-player test reads it)
            if (here) { _dressed = false; NpcKit.Born(Zdo, _def); } // (dressed afresh — its job may have changed meanwhile; its seed, if no owner gave it yet)
            if (_mind != null) _mind.OwnerChanged(here);
            if (Body != null) Body.OwnerChanged(here);
        }

        // ---------------------------------------------------------------- the look
        // where it is owned (and again when the ownership comes here: a new owner's copy has no gear of its own yet)
        private void Dress()
        {
            _dressed = true;
            ZDO z = Zdo;
            if (z == null || _body == null) return;
            try
            {
                NpcSettlers.Look own = NpcSettlers.Of(this); // (a settler's own look, else its template's)
                int model = own != null ? own.Model : _def.Model;
                Vector3 skin = own != null ? own.Skin : _def.Skin, hairColor = own != null ? own.HairColor : _def.HairColor;
                string hair = own != null ? own.Hair : _def.Hair, beard = own != null ? own.Beard : _def.Beard;
                z.Set(ZDOVars.s_modelIndex, model, false);
                z.Set(ZDOVars.s_skinColor, skin);
                z.Set(ZDOVars.s_hairColor, hairColor);
                z.Set(ZDOVars.s_hairItem, Hash(hair), false);
                z.Set(ZDOVars.s_beardItem, Hash(beard), false);
                z.Set(ZDOVars.s_noHair, hair.Length == 0);
                z.Set(ZDOVars.s_noBeard, beard.Length == 0);
                // what the other games draw in its hands and on it is kept in its ZDO and outlives a restart; the body made
                // anew knows nothing of it, so taking off what it does not hold changes nothing there — a bow lost with its
                // locker stayed drawn in its hands (2026-10-31). Wiped first: put on again below from what it really has
                foreach (int k in new[] { ZDOVars.s_rightItem, ZDOVars.s_leftItem, ZDOVars.s_rightBackItem, ZDOVars.s_leftBackItem, ZDOVars.s_chestItem, ZDOVars.s_legItem, ZDOVars.s_helmetItem, ZDOVars.s_shoulderItem, ZDOVars.s_utilityItem })
                    if (z.GetInt(k, 0) != 0) z.Set(k, 0);
                _body.UnequipAllItems();
                _body.GetInventory().RemoveAll();
                List<ItemDrop.ItemData> template = new List<ItemDrop.ItemData>();
                foreach (string g in own != null ? own.Gear : _def.Gear)
                {
                    GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g) : null;
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (drop == null) { Npcs.Log(_def.Id + ": no item «" + g + "»"); continue; }
                    if (!NpcGear.Wearable(drop)) { Npcs.Log(_def.Id + ": «" + g + "» is not a thing to wear (a creature's own, no icon): left off"); continue; }
                    ItemDrop.ItemData t = drop.m_itemData.Clone(); t.m_dropPrefab = prefab;
                    template.Add(t);
                }
                NpcGear gear = Gear;
                List<ItemDrop.ItemData> wear = gear != null ? gear.Choose(template) : template;
                ItemDrop.ItemData near = null, far = null, shield = null;
                foreach (ItemDrop.ItemData it in wear)
                {
                    if (!_body.GetInventory().AddItem(it)) continue;
                    if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) shield = it;
                    if (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable) continue; // (carried: what it shoots)
                    if (it.IsWeapon()) { if (NpcGear.Ranged(it)) far = it; else near = it; continue; }
                    _body.EquipItem(it, false);
                }
                if (gear != null) { gear.Near = near; gear.Far = far; gear.Shield = shield; }
                ItemDrop.ItemData hand = near ?? far;
                if (hand != null) _body.EquipItem(hand, false);
                if (hand != null && !NpcGear.InHand(this)) NpcGear.Sheathe(_body, false); // (not a guard: on its back at its ease — drawn for a fight)
                int armor = Mathf.RoundToInt(_body.GetBodyArmor());
                if (z.GetInt(KArmor, -1) != armor) z.Set(KArmor, armor); // (its armour for the hover on every game: only the owner dresses it)
                Npcs.Log(Id + ": dressed — " + Wearing() + (far != null ? "; far " + far.m_shared.m_name : "") + (gear != null && gear.Cells != null ? "; its cells " + gear.Cells.NrOfItems() : ""));
            }
            catch (Exception e) { Npcs.Log(_def.Id + " dress: " + e.Message); }
        }

        // what it has on and in hand now (the log, «raven npc gear»)
        internal string Wearing()
        {
            if (_body == null) return "";
            List<string> l = new List<string>();
            foreach (ItemDrop.ItemData it in _body.GetInventory().GetAllItems()) l.Add(it.m_shared.m_name + (it.m_equipped ? "*" : "") + (it.m_stack > 1 ? " x" + it.m_stack : ""));
            return l.Count > 0 ? string.Join(", ", l.ToArray()) : "нічого";
        }

        private int Hash(string item)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            if (ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(item) == null) { Npcs.Log(_def.Id + ": no hair/beard «" + item + "»"); return 0; }
            return item.GetStableHashCode();
        }

        // Odin turns to the nearest player (on every game: nothing to send)
        private void Face()
        {
            Player p = Player.GetClosestPlayer(transform.position, 30f);
            if (p == null) return;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 3f);
        }

        // hit (whether it can be hurt or not): its mind answers, the story hears
        internal static void Hit(Character body, Character attacker)
        {
            NpcActor a;
            if (body == null || !ByBody.TryGetValue(body, out a) || a == null) return;
            if (a._mind != null && a.Owned) a._mind.HurtBy(attacker);
            if (a.Owned && Time.time >= a._nextHitRule) { a._nextHitRule = Time.time + 3f; NpcRules.Fire(a, "hit", attacker as Player); }
            if (a.Owned) StoryBus.Raise("npc_hit", a.Id, attacker != null && attacker.IsPlayer() ? 1 : 0, a.transform.position);
        }

        // ---------------------------------------------------------------- coming and going
        // its puff of smoke where it stands now, on every game near (it was this game's alone: a second player saw a person jump with no smoke);
        // the spot goes with it, for the body is put elsewhere a moment later (NpcBody.Put: a puff at both ends)
        internal void Appear()
        {
            if (_nv != null && _nv.IsValid()) _nv.InvokeRPC(ZNetView.Everybody, RpcAppear, transform.position);
            else Puff(transform.position);
        }
        private void RPC_Appear(long sender, Vector3 at) { Puff(at); }
        private void Puff(Vector3 at) { if (m_vanish != null && m_vanish.HasEffects()) m_vanish.Create(at, transform.rotation); }

        // the look edited (the window «Вигляд») or its cells changed (NpcGear): dressed again, here (an edit takes the NPC over)
        internal void Redress()
        {
            if (_nv != null && _nv.IsValid() && !_nv.IsOwner()) _nv.ClaimOwnership();
            _dressed = false;
        }

        // gone from the world (its effect for Odin): the owner takes it away
        internal void Remove()
        {
            if (_nv == null || !_nv.IsValid()) { Destroy(gameObject); return; }
            if (!_nv.IsOwner()) _nv.ClaimOwnership();
            Run(Removed); // (the modules' own: what it carried and wore on the ground, its locker gone…)
            Appear();
            _nv.Destroy();
        }

        // ---------------------------------------------------------------- words
        // over its head, for everyone near (the game's own bubble); {name} is the one it says it to (here: you)
        internal void Say(string text) { Say(text, null); }

        // ---------------------------------------------------------------- one voice at a time (the user 2026-11-06: many people near, their
        // words over each other). A line over a head holds the floor in a small ring (FloorRadius) while its bubble is up, on every game (RPC_Say
        // marks it, so people run by different games hear each other). How a line takes it:
        //   Over     says it now (the default): a talk's answer, a scene, two people talking to each other, a call for help
        //   Event    something happened (a raid over, a meal missed, it is leaving): waits its turn up to WaitMost, then says it; a newer one replaces it
        //   Unasked  its own word in passing (a remark, a mutter, a place, the weather): said only while the ring is quiet, else not at all
        // Who speaks unasked asks FloorFree before it makes its line (NpcVoice, NpcChatter): nothing is made to be thrown away.
        internal enum Voice { Over, Event, Unasked }
        internal static float FloorRadius = 8f, FloorGap = 0.8f, WaitMost = 15f;
        private sealed class Floor { public NpcActor Who; public Vector3 At; public float Until; }
        private static readonly List<Floor> Floors = new List<Floor>(); // session: bubbles on screen now
        private string _waitText; private Player _waitTo; private float _waitUntil;

        // nobody but these two speaking in the ring round this point now
        internal static bool FloorFree(Vector3 at, NpcActor me, NpcActor with = null)
        {
            float now = Time.time, r2 = FloorRadius * FloorRadius;
            for (int i = Floors.Count - 1; i >= 0; i--)
            {
                Floor f = Floors[i];
                if (f.Until < now || f.Who == null) { Floors.RemoveAt(i); continue; }
                if (f.Who != me && f.Who != with && (f.At - at).sqrMagnitude < r2) return false;
            }
            return true;
        }

        private void Hold(string text)
        {
            Floor f = Floors.Find(x => x.Who == this);
            if (f == null) { if (Floors.Count > 64) Floors.RemoveAt(0); f = new Floor { Who = this }; Floors.Add(f); }
            f.At = transform.position; f.Until = Time.time + RavenSpeech.Seconds(text, RavenSpeech.Pace.Bubble) + FloorGap;
        }

        // an event's line waiting for the ring to be quiet (from Update, where it is run)
        private void SayWaiting()
        {
            if (_waitText == null) return;
            if (Time.time > _waitUntil) { _waitText = null; return; } // (too late: the moment is gone)
            if (!FloorFree(transform.position, this)) return;
            string t = _waitText; _waitText = null;
            Say(t, _waitTo, Voice.Over);
        }

        internal void Say(string text, Player to, Voice how)
        {
            if (how != Voice.Over && !FloorFree(transform.position, this))
            {
                if (how == Voice.Event && !string.IsNullOrEmpty(text)) { _waitText = text; _waitTo = to; _waitUntil = Time.time + WaitMost; }
                return;
            }
            Say(text, to);
        }

        // its line to someone: {name} and «ходив|ходила» of that one — the one it speaks to, else the player nearest it (not
        // the player of the game that runs it: a host's or a server's game — a friend was called by the host's name, 2026-11-01)
        internal void Say(string text, Player to)
        {
            if (_nv == null || !_nv.IsValid() || string.IsNullOrEmpty(text)) return;
            text = NpcBody.Spoken(this, text); // (a gesture in its words: played, the mark taken out)
            if (text.Length == 0) return;
            Player p = to ?? Player.GetClosestPlayer(transform.position, 20f) ?? Player.m_localPlayer;
            text = Lang.T(text).Replace("{name}", p != null ? p.GetPlayerName() : "");
            if (p != null) text = Grammar.Fix(text, p);
            Hold(text); // (at once here: the next one asking this frame already hears it)
            _nv.InvokeRPC(ZNetView.Everybody, RpcSay, text);
        }

        private void RPC_Say(long sender, string text)
        {
            Hold(text); // (every game knows who holds the floor)
            if (Chat.instance == null) return;
            float seen = 25f;
            for (int i = 0; i < HeardFrom.Count; i++) seen = Mathf.Max(seen, HeardFrom[i](this)); // (one lost and calling out: from far — a module's word)
            Chat.instance.SetNpcText(gameObject, Vector3.up * (_body != null ? 2.1f : 2.6f), seen, RavenSpeech.Seconds(text, RavenSpeech.Pace.Bubble), "", text, false);
        }

        // ---------------------------------------------------------------- orders (the mind's words: NpcMind.Do)
        // a player's order by default; habits give theirs for the Routine layer, a scene for the Scene layer
        internal void Order(string order) { Order(order, NpcMind.Layer.Order); }

        internal void Order(string order, NpcMind.Layer layer) { Order(order, layer, null); }

        // whose: the plan's owner on that layer (none: the layer's own — NpcMind.Owner); it may only let go of its own
        internal void Order(string order, NpcMind.Layer layer, string whose)
        {
            if (_nv == null || !_nv.IsValid() || _mind == null) return;
            if (_nv.IsOwner()) { _mind.Do(order, layer, whose); return; }
            long owner = _nv.GetZDO().GetOwner();
            if (owner == 0L) { Npcs.Log(Id + ": nobody runs it this moment (its owner changes) — order dropped: " + order); return; } // (a send to 0 goes to every game: each would run it)
            _nv.InvokeRPC(owner, RpcOrder, ((int)layer) + " " + (string.IsNullOrEmpty(whose) ? "-" : whose) + " " + order);
        }
        private const string SentOn = "^"; // (an order already sent on once: never again — no ring of games that each think another runs it)

        // an order that comes here as its owner changes (sent to the old one): on to the new one, once — not lost without a word
        private void RPC_Order(long sender, string sent)
        {
            if (_nv == null || !_nv.IsValid() || _mind == null) return;
            bool on = sent.StartsWith(SentOn, StringComparison.Ordinal);
            if (on) sent = sent.Substring(SentOn.Length);
            if (!_nv.IsOwner())
            {
                long now = _nv.GetZDO().GetOwner();
                if (!on && now != 0L && now != sender && now != ZDOMan.GetSessionID()) _nv.InvokeRPC(now, RpcOrder, SentOn + sent);
                else Npcs.Log(Id + ": an order came as its owner changed, nowhere to send it on — dropped: " + sent);
                return;
            }
            string[] w = sent.Split(new[] { ' ' }, 3);
            int l;
            if (w.Length == 3 && int.TryParse(w[0], out l) && l >= 0 && l <= (int)NpcMind.Layer.Scene) _mind.Do(w[2], (NpcMind.Layer)l, w[1] == "-" ? null : w[1]);
        }

        // ---------------------------------------------------------------- pointing at it, E
        public string GetHoverName() { return _def != null ? Name : ""; }

        public string GetHoverText()
        {
            if (_def == null || NpcMenu.IsOpen) return ""; // (its menu open: these words would show through the plate)
            string q = _mind != null && NpcMenu.MayOrder(this) ? RavenKeys.LabelOf(RavenKeys.GiftsId) : "—"; // (orders only for the hired ones; an admin: all) // (Q: its round menu, NpcMenu — the hint names the key, so the gifts circle gives way)
            for (int i = 0; i < HoverInstead.Count; i++) { string own = HoverInstead[i](this); if (own != null) return Localization.instance != null ? Localization.instance.Localize(own) : own; } // (dead, senseless…: a module's own)
            StringBuilder hover = new StringBuilder(Name);
            for (int i = 0; i < HoverMarks.Count; i++) hover.Append(HoverMarks[i](this)); // (after its name: «!» it asks for something…)
            for (int i = 0; i < HoverLines.Count; i++) hover.Append(HoverLines[i](this)); // (under it: a guard's health, its blows left…)
            string text = hover + "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Говорити") + (q != "—" ? "\n[<color=yellow><b>" + q + "</b></color>] " + Lang.T("Дії") : "");
            return Localization.instance != null ? Localization.instance.Localize(text) : text; // ($KEY_Use → the key, as every hover text of the game)
        }

        // the modules' words on pointing at it and E (NpcModules): a whole hover of its own instead (dead, senseless), a mark
        // after its name, a line under it; E — its own use instead of a talk (null: not theirs; true/false: done)
        internal static readonly List<Func<NpcActor, string>> HoverInstead = new List<Func<NpcActor, string>>();
        internal static readonly List<Func<NpcActor, string>> HoverMarks = new List<Func<NpcActor, string>>();
        internal static readonly List<Func<NpcActor, string>> HoverLines = new List<Func<NpcActor, string>>();
        internal static readonly List<Func<NpcActor, Player, bool?>> UseInstead = new List<Func<NpcActor, Player, bool?>>();

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _def == null || Time.time < _nextSay) return false;
            _nextSay = Time.time + 1.5f;
            if (user == null) return false;
            Player me = user as Player;
            for (int i = 0; i < UseInstead.Count; i++) { bool? done = UseInstead[i](this, me); if (done.HasValue) return done.Value; } // (dead, senseless…: a module's own use)
            // its talk opens here, on the game of the one who talks (NpcTalk); it turns to you there where it is run
            if (user == Player.m_localPlayer && NpcTalk.Open(this, Player.m_localPlayer)) return true; // (its look and «npc_talk» there: only when a talk really opens, not over another player's)
            if (Owned) Talk(user.GetZDOID());
            else _nv.InvokeRPC(_nv.GetZDO().GetOwner(), RpcTalk, user.GetZDOID());
            return true;
        }

        // E on it (here, or sent from another game to the owner): its «on talk» habit, else a greet line
        private void RPC_Talk(long sender, ZDOID who) { if (Owned) Talk(who); }

        // ---------------------------------------------------------------- what a talk changes in it (on the game that runs it)
        //   hire <player id> · unhire
        internal void Act(string act)
        {
            if (_nv == null || !_nv.IsValid()) return;
            if (_nv.IsOwner()) DoAct(act);
            else _nv.InvokeRPC(_nv.GetZDO().GetOwner(), RpcAct, act);
        }

        private void RPC_Act(long sender, string act)
        {
            if (!Owned) { Npcs.Log(Id + ": an act came as its owner changed — dropped (not sent on: who sent it counts, SentBy): " + act); return; }
            _actFrom = sender;
            try { DoAct(act); }
            finally { _actFrom = 0L; }
        }

        // an act done in a player's name (hired by it, its patience, its promise): sent by that player's own game? Asked by those acts where it is run. A plain word
        // kept out of a game with no mod to change it; a player not here to tell by: let through (a changed client can write a ZDO anyway: this is no wall, only no open door)
        private static long _actFrom; // (the game the act now running came from; 0: this one)
        internal static bool SentBy(long player)
        {
            if (_actFrom == 0L) return true;
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && p.GetPlayerID() == player && p.GetOwner() != _actFrom) { Npcs.Log("an act in the name of " + player + " came from another game (" + _actFrom + "): not done"); return false; }
            return true;
        }

        private void DoAct(string act)
        {
            ZDO z = Zdo;
            if (z == null) return;
            string[] w = act.Split(' ');
            if (w[0] == "unhold") { if (w.Length > 1 && _mind != null) _mind.Unhold(NpcKit.Parse(w[1])); return; }
            if (w[0] == "hold") { if (w.Length > 1 && _mind != null) _mind.Hold(NpcKit.Parse(w[1])); return; } // (the core's own: it stands for whoever gives it orders)
            Action<NpcActor, string[]> run;
            if (Acts.TryGetValue(w[0], out run)) { try { run(this, w); } catch (Exception e) { Npcs.Log(Id + ": act «" + act + "» failed: " + e); } } // (a module's: NpcModules)
            else Npcs.Log(Id + ": no such act «" + w[0] + "»");
        }

        private void Talk(ZDOID who)
        {
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(who) : null;
            Player p = go != null ? go.GetComponent<Player>() : null;
            if (_mind != null) Order("look " + NpcKit.Ref(who));
            if (!NpcRules.Fire(this, "talk", p) && _def.Greet.Count > 0) { string g = _def.Greet[UnityEngine.Random.Range(0, _def.Greet.Count)]; if (g.Contains("{@")) g = Form(NpcPhrases.Inline(this, g, p)); if (p != null) g = Grammar.Fix(Lang.T(g).Replace("{name}", p.GetPlayerName()), p); Say(g); } // (to the one who spoke to it: its name and forms here, not those of the game that runs it)
            StoryBus.Raise("npc_talk", _def.Id, 0, transform.position);
        }

        // ---------------------------------------------------------------- habits (NpcRules), where it is owned
        private void Habits()
        {
            ZDO z = Zdo;
            if (z == null) return;
            if (!z.GetBool(NpcRules.KBorn, false)) { z.Set(NpcRules.KBorn, true); NpcRules.Fire(this, "spawn", null); }
            // habits are its Routine: under any order, fight or scene they only set what it goes back to (NpcMind layers)
            bool quiet = _mind != null && (_mind.NowLayer == NpcMind.Layer.Fight || _mind.Asleep); // (no greetings mid-fight or in its sleep)
            bool lives = _resident != null && NpcResident.Is(this);
            if (lives) _resident.Tick(); // (a resident's day is its schedule: the template's day/night habits are for those who live nowhere)
            int day = EnvMan.instance != null && EnvMan.IsNight() ? 1 : 0;
            if (day != _daySeen) { _daySeen = day; if (!lives) NpcRules.Fire(this, day == 1 ? "night" : "day", null); }
            foreach (NpcRule r in _def.Rules)
            {
                if (r.When != "near") continue;
                foreach (Player p in Player.GetAllPlayers())
                {
                    if (p == null) continue;
                    ZDOID id = p.GetZDOID();
                    float d = Vector3.Distance(p.transform.position, transform.position);
                    if (d <= r.Arg && !_near.Contains(id)) { _near.Add(id); if (!quiet && UnityEngine.Random.value < NpcPersona.Of(this).Greets) NpcRules.Run(this, r.Do, p); } // (a chatty one greets nearly always, a quiet one now and then)
                    else if (d > r.Arg + 4f) _near.Remove(id);
                }
                break; // (one «near» distance per NPC: the first rule's)
            }
        }

        internal void FireRule(string when) { NpcRules.Fire(this, when, null); }

        internal void Habit(string order) { Order(order, NpcMind.Layer.Routine); }

        public float GetHoverOffset() { return 0f; }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        internal string Describe(Player p)
        {
            float d = p != null ? Vector3.Distance(p.transform.position, transform.position) : 0f;
            return Id + (Tag.Length > 0 ? " «" + Tag + "»" : "") + " · " + Mathf.RoundToInt(d) + " м" + (Owned ? " · мій" : " · чужий")
                + (_mind != null ? " · " + _mind.Describe() : "") + (_def != null && _def.Mortal ? " · смертний" : "") + (_resident != null && NpcResident.Is(this) ? " · " + _resident.Describe() : "") + (Worker != null && Worker.Works ? " · " + Worker.Describe() : "");
        }
    }
}
#endif

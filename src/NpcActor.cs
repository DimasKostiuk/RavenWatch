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
        private const string RpcReact = "RavenWatch_NpcReact2"; // (a wordless sound of its own to what was said to it, and after how long: NpcSpeech.Reacted. «2»: a game of before 3.0, which sent the sound alone, neither sends nor reads this one)
        private const string RpcNext = "RavenWatch_NpcNext"; // (who speaks next in a talk, and when: NpcBubble's thinking dots. An older game does not read it: a warning in its log, nothing else)
        private const string RpcSay = "RavenWatch_NpcSay", RpcSpeak = "RavenWatch_NpcSpeak", RpcOrder = "RavenWatch_NpcOrder", RpcTalk = "RavenWatch_NpcTalk", RpcAppear = "RavenWatch_NpcAppear";
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
        internal string DueRemark = ""; // session: a module's word — this theme at its next remark, over the ranking's chance (NpcVoice.Tick; StoryLife: what it came to an outing place to say)
        internal float NextRemark, NextLook, StareAgain; internal string LastRemark = ""; internal int Stared; // (a player standing by it saying nothing: looks in a row) // (its own words unasked: NpcVoice)
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
        internal NpcSpeech Speech { get { return _speechL != null ? _speechL : (_speechL = GetComponent<NpcSpeech>()); } } // (its head and chest as it speaks: NpcSpeech)
        private NpcSpeech _speechL;
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

        // its name as kept: a settler's own (its ZDO), else its template's — the Ukrainian source, the same on every game and in
        // every language. What is stored (a memory, a locker, a quarrel), sent (an act) or compared is this, never Name
        // No template here (another version, one taken out): the id it was made with — never an empty name in a list
        internal string Key { get { string own = NpcSettlers.NameOf(this); if (own.Length > 0) return own; if (_def != null) return _def.Name; ZDO z = Zdo; return z != null ? z.GetString(Npcs.KId, "") : ""; } }
        // its name as shown: the kept one in the reader's language (Lang.T: the names are rows of ui.txt)
        internal string Name { get { string k = Key; return _def == null || k == Id ? k : Lang.T(k); } }
        // a kept name read from somewhere (a memory, a bed, the heart's list of its folk) as shown
        internal static string Shown(string key) { return string.IsNullOrEmpty(key) ? "" : Lang.T(key); }
        // who it is in the log: its template, and its own name when that tells more (many settlers share «settler»)
        internal string Who { get { string n = Key; return n == Id ? Id : Id + " «" + n + "»"; } }

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
            _nv.Register<ZDOID, float>(RpcNext, RPC_Next);
            _nv.Register<string, ZDOID>(RpcSpeak, RPC_Speak);
            _nv.Register<string, float>(RpcReact, RPC_React);
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
            if (_waiting.Count > 0) Npcs.Log(Id + ": gone from this game with " + _waiting.Count + " words still waiting for its owner — dropped"); // (said, not lost without a word)
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

        private float _hatAt; private VisEquipment _visL; // (its raven's hat: NpcHat — looked at a second, on every game)
        private void Update0()
        {
            if (Time.time >= _hatAt && _def != null) { _hatAt = Time.time + 1f; if (_visL == null) _visL = GetComponent<VisEquipment>(); NpcHat.Wear(_visL, transform, _def); }
            if (_waitText != null && Owned) SayWaiting();
            if (_waiting.Count > 0) SendWaiting();
            if (_paid.Count > 0) PaidTick();
            ZDO zo = Zdo; long owner = zo != null ? zo.GetOwner() : 0L; // (one field read: who ran it before, for an act sent on — FromOwner)
            if (owner != _ownerNow) { if (_ownerNow != 0L) _ownerBefore = _ownerNow; _ownerNow = owner; _ownerAt = Time.time; }
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
            if (here && _body != null && _body.GetLevel() != 1) { Npcs.Log(Id + " «" + Name + "»: had " + (_body.GetLevel() - 1) + " star(s) from another mod — a person has none"); _body.SetLevel(1); } // (Path of Valheiman's from before it left our people alone: Npcs.LeftToUs)
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
                foreach (int k in new[] { ZDOVars.s_rightItem, ZDOVars.s_leftItem, ZDOVars.s_rightBackItem, ZDOVars.s_leftBackItem, ZDOVars.s_chestItem, ZDOVars.s_legItem, ZDOVars.s_helmetItem, ZDOVars.s_shoulderItem, ZDOVars.s_utilityItem, ZDOVars.s_trinketItem })
                    if (z.GetInt(k, 0) != 0) z.Set(k, 0);
                _body.UnequipAllItems();
                _body.GetInventory().RemoveAll();
                List<ItemDrop.ItemData> template = new List<ItemDrop.ItemData>();
                foreach (string g in own != null ? own.Gear : _def.Gear)
                {
                    GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g) : null;
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (drop == null) { Npcs.Log(_def.Id + ": no item «" + g + "»"); continue; }
                    if (!NpcGear.Wearable(drop) && !_def.Dvergr) { Npcs.Log(_def.Id + ": «" + g + "» is not a thing to wear (a creature's own, no icon): left off"); continue; }
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
                    if (!_body.EquipItem(it, false)) Npcs.Log(_def.Id + ": «" + it.m_shared.m_name + "» not put on: the game said no"); // (never silent: a thing the game refuses stays in the bag)
                }
                if (gear != null) { gear.Near = near; gear.Far = far; gear.Shield = shield; }
                ItemDrop.ItemData hand = near ?? far;
                if (hand != null) _body.EquipItem(hand, false);
                if (hand != null && !NpcGear.InHand(this)) NpcGear.Sheathe(_body, false); // (not a guard: on its back at its ease — drawn for a fight)
                float worn = 0f; HitData.DamageModifiers mods = new HitData.DamageModifiers();
                foreach (ItemDrop.ItemData it in _body.GetInventory().GetEquippedItems()) if (NpcGear.IsClothes(it)) { worn += it.GetArmor(); if (it.m_shared.m_damageModifiers != null) mods.Apply(it.m_shared.m_damageModifiers); }
                _armor = worn; _armorMods = mods; // (Armoured)
                int armor = Mathf.RoundToInt(worn);
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

        // what it wears protects it. The game counts armour for a player alone (Character.RPC_Damage: «if IsPlayer»; a body with no Player says 0 armour and none of
        // its pieces' resistances — the user 2026-11-11: «дав йому броню», the hover said 0 and a blow took all of it): a person's own, reckoned as it is dressed
        // (Dress: only where it is run, as the blow is) and taken off the blow before the game's own reckoning (NpcDamagePatch) — before its block and stagger, not after
        private float _armor;
        private HitData.DamageModifiers _armorMods;
        internal static void Armoured(Character body, HitData hit)
        {
            NpcActor a;
            if (hit == null || body == null || !ByBody.TryGetValue(body, out a) || a == null) return; // (being dressed again: as last dressed — never bare for a blow between)
            HitData.DamageModifier most;
            hit.ApplyResistance(a._armorMods, out most);
            hit.ApplyArmor(a._armor);
        }

        // hit (whether it can be hurt or not): its mind answers, the story hears. The blow goes into the log on the same 3 s as its rule — its kind,
        // its damage before armour, whose, its health then (2026-11-14: settlers fell senseless a minute after this game took them, and the log named no one)
        internal static void Hit(Character body, HitData hit)
        {
            NpcActor a;
            if (body == null || !ByBody.TryGetValue(body, out a) || a == null) return;
            Character attacker = hit != null ? hit.GetAttacker() : null;
            if (a._mind != null && a.Owned) a._mind.HurtBy(attacker);
            if (a.Owned && Time.time >= a._nextHitRule)
            {
                a._nextHitRule = Time.time + 3f;
                NpcRules.Fire(a, "hit", attacker as Player);
                string by = attacker != null ? Utils.GetPrefabName(attacker.gameObject) : hit != null && !hit.m_attacker.IsNone() ? "not loaded " + hit.m_attacker : "nobody";
                Npcs.Log(a.Id + ": hit — " + (hit != null ? hit.m_hitType + " " + hit.GetTotalDamage().ToString("0.#") : "?") + " by " + by + ", health " + body.GetHealth().ToString("0") + "/" + body.GetMaxHealth().ToString("0"));
            }
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
        // over its head, for everyone near (NpcBubble: one capsule per ring of voices); {name} is the one it says it to (here: you)
        internal void Say(string text) { Say(text, null); }

        // ---------------------------------------------------------------- one voice at a time (the user 2026-11-06: many people near, their
        // words over each other). A line over a head holds the floor in a small ring (FloorRadius) while its bubble is up, on every game (RPC_Say
        // marks it, so people run by different games hear each other). How a line takes it:
        //   Over     says it now (the default): a talk's answer, a scene, two people talking to each other, a call for help
        //   Event    something happened (a raid over, a meal missed, it is leaving): waits its turn up to WaitMost, then says it; a newer one replaces it
        //   Unasked  its own word in passing (a remark, a mutter, a place, the weather): said only while the ring is quiet, else not at all
        // Who speaks unasked asks FloorFree before it makes its line (NpcVoice, NpcChatter): nothing is made to be thrown away.
        // A weighty line (the user 2026-11-12: «щоб вони не губились серед звичайного трьопа»): something happened (Voice.Event), or what presses on it
        // (NpcVoice.Weighty: its own pressing theme — its hunger, its pay, what it asks of you, what you did to it; not a word on you). Every game draws it
        // the same: the mark goes with the words (Mark, a sign no font draws), and the bubble is in WeightyColor, seen WeightyFar times as far. Small talk,
        // a remark in passing, an answer in a talk stay as they were. Who decides what is weighty is the core here, not the module that speaks
        internal static string WeightyColor = "#FFB75E"; internal static float WeightyFar = 1.6f;
        private const string Mark = "\u200B";
        // A line that is a place of a talk (a true tale of Torvald's told by the fire: StoryMeets, the user 2026-11-16) says which with its words \u2014 \u00AB<talk> <place>\u00BB
        // between two ToldMark \u2014 and every game that gets it tells Told (the module there: its own player near enough has heard that place \u2014 the book's \u00AB\u0421\u0430\u0433\u0430\u00BB)
        private const string ToldMark = "\u2060";
        internal static Action<NpcActor, string> Told;
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
            SayLine(t, _waitTo, true); // (an event's line: weighty)
        }

        internal void Say(string text, Player to, Voice how, bool weighty = false)
        {
            if (how != Voice.Over && !FloorFree(transform.position, this))
            {
                if (how == Voice.Event && !string.IsNullOrEmpty(text)) { _waitText = text; _waitTo = to; _waitUntil = Time.time + WaitMost; }
                return;
            }
            SayLine(text, to, weighty || how == Voice.Event);
        }

        // its line to someone: {name} and «ходив|ходила» of that one — the one it speaks to, else the player nearest it (not
        // the player of the game that runs it: a host's or a server's game — a friend was called by the host's name, 2026-11-01)
        internal void Say(string text, Player to) { SayLine(text, to); }

        // a module that has it keep quiet now (the story: a scene plays near and it is not in it — StoryAside.Hushed; one not met yet — StoryStranger.Hushed): no line of its own at all
        internal static System.Func<NpcActor, bool> Hushed;

        // ...and the words as every game got them (null: nothing said) — the small talk times its next line by them (NpcSpeech.Seconds)
        internal string SayLine(string text, Player to, bool weighty = false, string told = null)
        {
            if (_nv == null || !_nv.IsValid() || string.IsNullOrEmpty(text)) return null;
            if (Hushed != null && Hushed(this)) return null;
            text = NpcBody.Spoken(this, text); // (a gesture in its words: played, the mark taken out)
            if (text.Length == 0) return null;
            Player p = to ?? Player.GetClosestPlayer(transform.position, 20f) ?? Player.m_localPlayer;
            text = Lang.T(text).Replace("{name}", p != null ? p.GetPlayerName() : "");
            if (p != null) text = Grammar.Fix(text, p);
            Hold(text); // (at once here: the next one asking this frame already hears it)
            _nv.InvokeRPC(ZNetView.Everybody, RpcSay, (weighty ? Mark : "") + (told != null ? ToldMark + told + ToldMark : "") + text);
            return text;
        }

        // its line in a player's talk window: no bubble (the window shows it), but every game near sees it speak to that player (NpcSpeech)
        internal void Speak(string text, Player to)
        {
            if (_nv == null || !_nv.IsValid() || string.IsNullOrEmpty(text) || to == null) return;
            _nv.InvokeRPC(ZNetView.Everybody, RpcSpeak, text, to.GetZDOID());
        }

        // its wordless sound to what was said to it (your answer in its talk): every game near hears it from the person (NpcSpeech),
        // after seconds — once the hero has said his answer (NpcTalk.Pick: he hears you out)
        internal void React(string sound, float after)
        {
            if (_nv == null || !_nv.IsValid() || string.IsNullOrEmpty(sound)) return;
            _nv.InvokeRPC(ZNetView.Everybody, RpcReact, sound, Mathf.Clamp(after, 0f, 30f));
        }

        private void RPC_React(long sender, string sound, float after) { NpcSpeech.Reacted(this, sound, after); }

        private void RPC_Speak(long sender, string text, ZDOID to)
        {
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(to) : null;
            NpcSpeech.Said(this, text, g != null ? g.GetComponent<Character>() : null);
        }

        private void RPC_Say(long sender, string text)
        {
            bool weighty = text.StartsWith(Mark, StringComparison.Ordinal);
            if (weighty) text = text.Substring(Mark.Length);
            string told = null;
            int end = text.StartsWith(ToldMark, StringComparison.Ordinal) ? text.IndexOf(ToldMark, ToldMark.Length, StringComparison.Ordinal) : -1;
            if (end > 0) { told = text.Substring(ToldMark.Length, end - ToldMark.Length); text = text.Substring(end + ToldMark.Length); }
            if (told != null && Told != null) Told(this, told); // (a place of a talk: heard by whom it reaches here)
            Hold(text); // (every game knows who holds the floor)
            NpcSpeech.Said(this, text); // (its head and chest speak it, those near turn to it: every game from the same words)
            if (Chat.instance == null) return; // (no screen here: a server)
            float seen = 25f;
            for (int i = 0; i < HeardFrom.Count; i++) seen = Mathf.Max(seen, HeardFrom[i](this)); // (one lost and calling out: from far — a module's word)
            NpcBubble.Say(this, text, RavenSpeech.Seconds(text, RavenSpeech.Pace.Bubble), weighty ? seen * WeightyFar : seen, weighty); // (one capsule per ring of voices, not the game's bubble per head)
        }

        // a talk's next line is next's, after so many seconds (NpcChatter, where the talk is run): every game shows its thinking dots
        // in the capsule this one's line is in (NpcBubble.Next)
        internal void SayNext(NpcActor next, float after)
        {
            if (_nv == null || !_nv.IsValid() || next == null || next.Zdo == null) return;
            _nv.InvokeRPC(ZNetView.Everybody, RpcNext, next.Zdo.m_uid, Mathf.Clamp(after, 0f, 60f));
        }

        private void RPC_Next(long sender, ZDOID next, float after)
        {
            if (Chat.instance == null) return; // (no screen here)
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(next) : null;
            NpcActor n = g != null ? g.GetComponent<NpcActor>() : null;
            if (n != null) NpcBubble.Next(this, n, after);
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
            ToOwner(RpcOrder, ((int)layer) + " " + (string.IsNullOrEmpty(whose) ? "-" : whose) + " " + order);
        }
        private const string SentOn = "^"; // (an order or act already sent on once: never again — no ring of games that each think another runs it)

        // ---------------------------------------------------------------- to the game that runs it (orders, acts): one way for both
        // Its owner changes now and then (a player walks off, leaves): for a moment nobody runs it (owner 0: a send to 0 goes to every
        // game, each would run it), or a word reaches the old owner. A word in a player's name may come after the player has already
        // given something (food, a keepsake, coins): lost, it is paid for nothing. So: nobody runs it — the word waits here, asked
        // again every WaitStep s up to WaitFor s (then dropped with a line); came to the old owner — sent on once to the new one,
        // an act with the game it first came from (SentBy asks that one, not the one that passed it on).
        internal static float WaitFor = 10f, WaitStep = 0.5f;
        private const int WordsMost = 16;
        private sealed class Waiting { public string Rpc, Payload; public float Until; }
        private readonly List<Waiting> _waiting = new List<Waiting>(); // session: words for an owner that is not there this moment
        private float _nextWaiting;

        // an act sent on is taken only from the game that ran it a moment ago (else any game could write another player's name before it)
        private long _ownerNow, _ownerBefore; private float _ownerAt;
        private bool FromOwner(long sender) { return sender != 0L && (sender == _ownerNow || (sender == _ownerBefore && Time.time - _ownerAt <= WaitFor + 10f)); }

        // an act for a person whose body may not be loaded here (the server's own passes over ZDOs: NpcRescue): straight to its owner; owned by nobody, or by this
        // game — the caller writes it itself (there is no body here to wait on)
        internal static void ActTo(ZDO z, string act)
        {
            long owner = z != null ? z.GetOwner() : 0L;
            if (owner == 0L || owner == ZDOMan.GetSessionID() || ZRoutedRpc.instance == null) { Npcs.Log("an act for a person nobody else runs — the caller keeps it: " + act); return; }
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, z.m_uid, RpcAct, act);
        }

        private void ToOwner(string rpc, string payload)
        {
            long owner = _nv.GetZDO().GetOwner();
            if (owner != 0L) { _nv.InvokeRPC(owner, rpc, payload); return; }
            if (_waiting.Count >= WordsMost) { Npcs.Log(Id + ": nobody runs it and " + WordsMost + " words already wait — dropped: " + payload); return; }
            _waiting.Add(new Waiting { Rpc = rpc, Payload = payload, Until = Time.time + WaitFor });
            Npcs.Log(Id + ": nobody runs it this moment (its owner changes) — waits for one: " + payload);
        }

        // in its Update: only while something waits (none: one count looked at)
        private void SendWaiting()
        {
            if (Time.time < _nextWaiting || _nv == null || !_nv.IsValid()) return;
            _nextWaiting = Time.time + WaitStep;
            long owner = _nv.GetZDO().GetOwner();
            for (int i = _waiting.Count - 1; i >= 0; i--)
            {
                Waiting w = _waiting[i];
                if (owner != 0L) { _waiting.RemoveAt(i); _nv.InvokeRPC(owner, w.Rpc, w.Payload); continue; } // (this game too, when it is the owner now: the same way in as from any other)
                if (Time.time >= w.Until) { _waiting.RemoveAt(i); Npcs.Log(Id + ": nobody took it over in " + WaitFor + " s — dropped: " + w.Payload); }
            }
        }

        // an order that comes here as its owner changes (sent to the old one): on to the new one, once — not lost without a word
        private void RPC_Order(long sender, string sent)
        {
            if (_nv == null || !_nv.IsValid() || _mind == null) return;
            bool on = sent.StartsWith(SentOn, StringComparison.Ordinal);
            if (on) sent = sent.Substring(SentOn.Length);
            if (!_nv.IsOwner())
            {
                if (!on) ToOwner(RpcOrder, SentOn + sent);
                else Npcs.Log(Id + ": an order sent on once came to a game that does not run it again — dropped: " + sent);
                return;
            }
            string[] w = sent.Split(new[] { ' ' }, 3);
            int l;
            if (w.Length == 3 && int.TryParse(w[0], out l) && l >= 0 && l <= (int)NpcMind.Layer.Scene) _mind.Do(w[2], (NpcMind.Layer)l, w[1] == "-" ? null : w[1]);
        }

        // ---------------------------------------------------------------- pointing at it, E
        public string GetHoverName() { return _def != null ? ShownName : ""; }

        // its name as the player sees it now — over it, on its lines (NpcBubble, a scene's subtitle), on the map (NpcMap): a module's own while it
        // keeps one nameless (one not met yet: «Чужинець» — StoryStranger), else Name. What is kept or compared stays Key
        internal string ShownName
        {
            get
            {
                for (int i = 0; i < NameInstead.Count; i++) { string own = NameInstead[i](this); if (own != null) return own; }
                return Name;
            }
        }

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
        internal static readonly List<Func<NpcActor, string>> NameInstead = new List<Func<NpcActor, string>>(); // (what the game shows as its name over it: the hover's head, the enemy plate — null: its own)
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
            else ToOwner(RpcAct, act);
        }

        // ---------------------------------------------------------------- a thing given with an act (a healing mead, a bite from your hands)
        // The thing leaves the giver's bag at once; what it does is done where the person is run. Till 2026-11-12 the two were not tied: the mead was
        // taken, and when its act was not done — nobody took the person over in WaitFor s, the word was dropped on its way, the person had come to by
        // itself a moment before, another player's mead got there first — it was gone for nothing. So a thing goes with its act as one: «paid <n> <act>».
        // The game that runs the person does the act and answers the game it came from: done or not (its module says «not done» with NotDone; an act
        // that failed or that nobody knows is not done either). Not done, or no answer in PaidWait s: the thing is back in the bag (at your feet when
        // the bag is full) with a word on the screen. An answer lost on its way gives the thing back though its act was done: a mead too many
        // rather than one lost. A new thing given for what happens on the person: Pay, never RemoveItem beside Act
        internal static float PaidWait = 25f; // (over the two waits a word may meet on its way: for an owner, and sent on once)
        private const string PaidWord = "paid", RpcPaid = "RavenWatch_NpcPaid";
        private sealed class Paid { public int N; public ItemDrop.ItemData Thing; public float Until; public string Act, Whom; }
        private static readonly List<Paid> _paid = new List<Paid>(); // session: things given on this game whose acts are not answered yet
        private static int _paidN;
        private static bool _notDone;
        private static ZRoutedRpc _paidRpcOn; // (registered with this one: once a session)
        internal static void NotDone() { _notDone = true; } // (an act's own «no»: said by its module where the person is run)

        internal void Pay(Player p, ItemDrop.ItemData thing, string act)
        {
            if (p == null || thing == null || _nv == null || !_nv.IsValid()) return;
            PaidRpcs();
            ItemDrop.ItemData one = thing.Clone(); one.m_stack = 1;
            ItemMarks.Take(p, thing, 1); // (given on purpose: a kept thing is no loss to undo)
            Paid paid = new Paid { N = ++_paidN, Thing = one, Until = Time.time + PaidWait, Act = act, Whom = Id };
            _paid.Add(paid);
            Act(PaidWord + " " + paid.N.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + act);
        }

        private static void PaidRpcs()
        {
            if (ZRoutedRpc.instance == null || _paidRpcOn == ZRoutedRpc.instance) return;
            _paidRpcOn = ZRoutedRpc.instance; _paid.Clear(); // (another world: what waited in the last one is not this one's)
            ZRoutedRpc.instance.Register<int, bool>(RpcPaid, (sender, n, done) => PaidAnswer(n, done));
        }

        // «paid <n> <act>» where it is run: the act, then its answer to the game the thing was given on
        private void RunPaid(string act, string[] w)
        {
            int n;
            long from = _actFrom;
            if (w.Length < 3 || !int.TryParse(w[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)) { Npcs.Log(Id + ": a paid act it cannot read — dropped: " + act); return; }
            _notDone = false;
            DoAct(act.Substring(PaidWord.Length + 1 + w[1].Length + 1));
            bool done = !_notDone;
            _notDone = false;
            if (!done) Npcs.Log(Id + ": a thing given for «" + w[2] + "» did nothing here — it goes back");
            if (from == 0L || ZRoutedRpc.instance == null) PaidAnswer(n, done);
            else { PaidRpcs(); ZRoutedRpc.instance.InvokeRoutedRPC(from, RpcPaid, n, done); }
        }

        private static void PaidAnswer(int n, bool done)
        {
            for (int i = 0; i < _paid.Count; i++)
            {
                if (_paid[i].N != n) continue;
                Paid paid = _paid[i];
                _paid.RemoveAt(i);
                if (!done) GiveBack(paid, "its act was not done");
                return;
            }
        }

        // in a person's Update: only while something waits (none: one count looked at)
        private static void PaidTick()
        {
            for (int i = _paid.Count - 1; i >= 0; i--)
            {
                if (Time.time < _paid[i].Until) continue;
                Paid paid = _paid[i];
                _paid.RemoveAt(i);
                GiveBack(paid, "no answer in " + PaidWait + " s");
            }
        }

        private static void GiveBack(Paid paid, string why)
        {
            Player me = Player.m_localPlayer;
            string name = paid.Thing.m_dropPrefab != null ? paid.Thing.m_dropPrefab.name : paid.Thing.m_shared.m_name;
            if (me == null) { Npcs.Log(paid.Whom + ": " + name + " given for «" + paid.Act + "» — " + why + ", and no player here to give it back to"); return; }
            if (!me.GetInventory().AddItem(paid.Thing)) ItemDrop.DropItem(paid.Thing, 1, me.transform.position + me.transform.forward * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            me.Message(MessageHud.MessageType.Center, Lang.T("Не вийшло. Річ знову в тебе."));
            Npcs.Log(paid.Whom + ": " + name + " given for «" + paid.Act + "» — " + why + ": given back");
        }

        // «^<game> <act>»: sent on by a game that no longer ran it; <game> is the one it first came from
        private void RPC_Act(long sender, string act)
        {
            bool on = act.StartsWith(SentOn, StringComparison.Ordinal);
            if (on)
            {
                int sp = act.IndexOf(' ');
                long first;
                if (sp < 0 || !long.TryParse(act.Substring(SentOn.Length, sp - SentOn.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out first)) { Npcs.Log(Id + ": a sent-on act it cannot read — dropped: " + act); return; }
                if (!FromOwner(sender)) { Npcs.Log(Id + ": an act «sent on» by a game that did not run it (" + sender + ") — dropped: " + act); return; }
                sender = first; act = act.Substring(sp + 1);
            }
            if (!Owned)
            {
                if (!on) ToOwner(RpcAct, SentOn + sender.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + act); // (who sent it still counts there: SentBy)
                else Npcs.Log(Id + ": an act sent on once came to a game that does not run it again — dropped: " + act);
                return;
            }
            _actFrom = sender;
            try { DoAct(act); }
            finally { _actFrom = 0L; }
        }

        // an act done in a player's name (hired by it, its patience, its promise): sent by that player's own game? Asked by those acts where it is run. A plain word
        // kept out of a game with no mod to change it; a player not here to tell by: let through (a changed client can write a ZDO anyway: this is no wall, only no open door)
        private static long _actFrom; // (the game the act now running came from; 0: this one)
        internal static long ActFrom { get { return _actFrom; } } // (an act's answer goes back there: NpcMenu.Deeds)
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
            if (z == null) { _notDone = true; return; }
            string[] w = act.Split(' ');
            if (w[0] == PaidWord) { RunPaid(act, w); return; } // (a thing given with it: done, and answered)
            if (w[0] == "unhold") { if (w.Length > 1 && _mind != null) _mind.Unhold(NpcKit.Parse(w[1])); return; }
            if (w[0] == "hold") { if (w.Length > 1 && _mind != null) _mind.Hold(NpcKit.Parse(w[1])); return; } // (the core's own: it stands for whoever gives it orders)
            Action<NpcActor, string[]> run;
            if (Acts.TryGetValue(w[0], out run)) { try { run(this, w); } catch (Exception e) { _notDone = true; Npcs.Log(Id + ": act «" + act + "» failed: " + e); } } // (a module's: NpcModules)
            else { _notDone = true; Npcs.Log(Id + ": no such act «" + w[0] + "»"); }
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

#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
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
        private const string RpcSay = "RavenWatch_NpcSay", RpcOrder = "RavenWatch_NpcOrder", RpcTalk = "RavenWatch_NpcTalk";

        public EffectList m_vanish = new EffectList(); // (Odin's: his puff of smoke)

        private ZNetView _nv;
        private Humanoid _body;
        private NpcMind _mind;
        private NpcDef _def;
        private bool _dressed, _minded;
        private Animator _anim;
        private int _emoteId = -1;
        private string _emoteLoop = "";
        private float _nextSay, _nextRules, _nextHitRule;
        private int _daySeen = -1; // 0 day · 1 night (the habit for it given)
        private readonly List<ZDOID> _near = new List<ZDOID>();

        internal string Id { get { return _def != null ? _def.Id : ""; } }
        internal string Tag { get { ZDO z = Zdo; return z != null ? z.GetString(Npcs.KTag, "") : ""; } }
        internal ZDO Zdo { get { return _nv != null && _nv.IsValid() ? _nv.GetZDO() : null; } }
        internal NpcDef Def { get { return _def; } }
        internal NpcMind Mind { get { return _mind; } }
        internal bool Owned { get { return _nv != null && _nv.IsValid() && _nv.IsOwner(); } }

        internal static bool Immortal(Character c)
        {
            NpcActor a;
            return c != null && ByBody.TryGetValue(c, out a) && a != null && (a._def == null || !a._def.Mortal);
        }

        private void Awake()
        {
            _nv = GetComponent<ZNetView>();
            _body = GetComponent<Humanoid>();
            _mind = GetComponent<NpcMind>();
            ZDO z = Zdo;
            if (z == null) return; // (a ghost with no ZDO)
            string id = z.GetString(Npcs.KId, "");
            if (id.Length == 0 && Npcs.NextId.Length > 0) { id = Npcs.NextId; z.Set(Npcs.KId, id); }
            _def = NpcDefs.Find(id);
            if (_def == null) Npcs.Log("an npc «" + id + "» with no template here (another version?): it stands still");
            _nv.Register<string>(RpcSay, RPC_Say);
            _nv.Register<string>(RpcOrder, RPC_Order);
            _nv.Register<ZDOID>(RpcTalk, RPC_Talk);
            if (_body != null)
            {
                ByBody[_body] = this;
                if (_def != null)
                {
                    _body.m_name = _def.Name;
                    _body.m_health = _def.Health;
                    _body.m_walkSpeed *= _def.Speed; _body.m_runSpeed *= _def.Speed;
                }
            }
            Live.Add(this);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_body != null) ByBody.Remove(_body);
        }

        private void Update()
        {
            if (_def == null) return;
            if (_body != null)
            {
                if (!_dressed && Owned) Dress();
                if (!_minded && Owned && _mind != null) { _minded = true; _mind.Do("fight " + _def.Fight); }
                UpdateEmote();
                if (Owned && Time.time >= _nextRules) { _nextRules = Time.time + 0.5f; Habits(); }
            }
            else Face(); // (Odin: no mind, he only turns to you)
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
                z.Set(ZDOVars.s_modelIndex, _def.Model, false);
                z.Set(ZDOVars.s_skinColor, _def.Skin);
                z.Set(ZDOVars.s_hairColor, _def.HairColor);
                z.Set(ZDOVars.s_hairItem, Hash(_def.Hair), false);
                z.Set(ZDOVars.s_beardItem, Hash(_def.Beard), false);
                z.Set(ZDOVars.s_noHair, _def.Hair.Length == 0);
                z.Set(ZDOVars.s_noBeard, _def.Beard.Length == 0);
                _body.UnequipAllItems();
                _body.GetInventory().RemoveAll();
                foreach (string g in _def.Gear)
                {
                    GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(g) : null;
                    if (prefab == null) { Npcs.Log(_def.Id + ": no item «" + g + "»"); continue; }
                    ItemDrop.ItemData it = _body.PickupPrefab(prefab, 0, false);
                    if (it != null) _body.EquipItem(it, false);
                }
            }
            catch (Exception e) { Npcs.Log(_def.Id + " dress: " + e.Message); }
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

        // ---------------------------------------------------------------- gestures and poses
        // As the game does it for players: the owner writes the gesture into the ZDO (its name, once or held, and a
        // number that goes up), and every game plays it on its own animator when the number changes.
        // name : back to standing. Held ones (loop): sit, relax, rest… until another order; once: wave, point, bow…
        internal void Emote(string name, bool loop)
        {
            ZDO z = Zdo;
            if (z == null || !Owned) return;
            if (string.IsNullOrEmpty(name) && z.GetString(ZDOVars.s_emote, "").Length == 0) return;
            z.Set(ZDOVars.s_emoteID, z.GetInt(ZDOVars.s_emoteID, 0) + 1);
            z.Set(ZDOVars.s_emote, name ?? "");
            z.Set(ZDOVars.s_emoteOneshot, !loop);
        }

        private void UpdateEmote()
        {
            ZDO z = Zdo;
            if (z == null) return;
            if (Owned && _emoteLoop.Length > 0 && _body.GetMoveDir() != Vector3.zero) Emote("", false); // (it walks off: the pose ends)
            int id = z.GetInt(ZDOVars.s_emoteID, 0);
            if (id == _emoteId) return;
            _emoteId = id;
            if (_anim == null) _anim = GetComponentInChildren<Animator>();
            if (_anim == null) return;
            if (_emoteLoop.Length > 0) _anim.SetBool("emote_" + _emoteLoop, false);
            _emoteLoop = "";
            _anim.SetTrigger("emote_stop");
            string e = z.GetString(ZDOVars.s_emote, "");
            if (e.Length == 0) return;
            _anim.ResetTrigger("emote_stop");
            if (z.GetBool(ZDOVars.s_emoteOneshot, true)) _anim.SetTrigger("emote_" + e);
            else { _emoteLoop = e; _anim.SetBool("emote_" + e, true); }
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
        internal void Appear() { if (m_vanish != null && m_vanish.HasEffects()) m_vanish.Create(transform.position, transform.rotation); }

        // gone from the world (its effect for Odin): the owner takes it away
        internal void Remove()
        {
            if (_nv == null || !_nv.IsValid()) { Destroy(gameObject); return; }
            if (!_nv.IsOwner()) _nv.ClaimOwnership();
            Appear();
            _nv.Destroy();
        }

        // ---------------------------------------------------------------- words
        // over its head, for everyone near (the game's own bubble); {name} is the one it says it to (here: you)
        internal void Say(string text)
        {
            if (_nv == null || !_nv.IsValid() || string.IsNullOrEmpty(text)) return;
            Player p = Player.m_localPlayer;
            text = Lang.T(text).Replace("{name}", p != null ? p.GetPlayerName() : "");
            if (p != null) text = Grammar.Fix(text, p);
            _nv.InvokeRPC(ZNetView.Everybody, RpcSay, text);
        }

        private void RPC_Say(long sender, string text)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(gameObject, Vector3.up * (_body != null ? 2.1f : 2.6f), 25f, Mathf.Clamp(2f + text.Length * 0.06f, 4f, 12f), "", text, false);
        }

        // ---------------------------------------------------------------- orders (the mind's words: NpcMind.Do)
        internal void Order(string order)
        {
            if (_nv == null || !_nv.IsValid() || _mind == null) return;
            if (_nv.IsOwner()) _mind.Do(order);
            else _nv.InvokeRPC(_nv.GetZDO().GetOwner(), RpcOrder, order);
        }

        private void RPC_Order(long sender, string order)
        {
            if (_nv != null && _nv.IsOwner() && _mind != null) _mind.Do(order);
        }

        // ---------------------------------------------------------------- pointing at it, E
        public string GetHoverName() { return _def != null ? Lang.T(_def.Name) : ""; }

        public string GetHoverText()
        {
            if (_def == null) return "";
            return Lang.T(_def.Name) + "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Говорити");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _def == null || Time.time < _nextSay) return false;
            _nextSay = Time.time + 1.5f;
            if (user == null) return false;
            if (Owned) Talk(user.GetZDOID());
            else _nv.InvokeRPC(_nv.GetZDO().GetOwner(), RpcTalk, user.GetZDOID());
            return true;
        }

        // E on it (here, or sent from another game to the owner): its «on talk» habit, else a greet line
        private void RPC_Talk(long sender, ZDOID who) { if (Owned) Talk(who); }

        private void Talk(ZDOID who)
        {
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(who) : null;
            Player p = go != null ? go.GetComponent<Player>() : null;
            if (_mind != null) Order("look " + NpcMind.IdOf(who));
            if (!NpcRules.Fire(this, "talk", p) && _def.Greet.Count > 0) Say(_def.Greet[UnityEngine.Random.Range(0, _def.Greet.Count)]);
            StoryBus.Raise("npc_talk", _def.Id, 0, transform.position);
        }

        // ---------------------------------------------------------------- habits (NpcRules), where it is owned
        private void Habits()
        {
            ZDO z = Zdo;
            if (z == null) return;
            if (!z.GetBool(NpcRules.KBorn, false)) { z.Set(NpcRules.KBorn, true); NpcRules.Fire(this, "spawn", null); }
            bool fighting = _mind != null && _mind.Now == NpcMind.Task.Attack;
            int day = EnvMan.instance != null && EnvMan.IsNight() ? 1 : 0;
            if (day != _daySeen && !fighting) { _daySeen = day; NpcRules.Fire(this, day == 1 ? "night" : "day", null); } // (a fight first: the habit after it)
            foreach (NpcRule r in _def.Rules)
            {
                if (r.When != "near") continue;
                foreach (Player p in Player.GetAllPlayers())
                {
                    if (p == null) continue;
                    ZDOID id = p.GetZDOID();
                    float d = Vector3.Distance(p.transform.position, transform.position);
                    if (d <= r.Arg && !_near.Contains(id)) { _near.Add(id); if (!fighting) NpcRules.Run(this, r.Do, p); }
                    else if (d > r.Arg + 4f) _near.Remove(id);
                }
                break; // (one «near» distance per NPC: the first rule's)
            }
        }

        internal void FireRule(string when) { NpcRules.Fire(this, when, null); }

        internal void OrderNow(string order) { Order(order); }

        public float GetHoverOffset() { return 0f; }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

        internal string Describe(Player p)
        {
            float d = p != null ? Vector3.Distance(p.transform.position, transform.position) : 0f;
            return Id + (Tag.Length > 0 ? " «" + Tag + "»" : "") + " · " + Mathf.RoundToInt(d) + " м" + (Owned ? " · мій" : " · чужий")
                + (_mind != null ? " · " + _mind.Status : "") + (_def != null && _def.Mortal ? " · смертний" : "");
        }
    }
}
#endif

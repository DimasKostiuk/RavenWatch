#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // An NPC's mind (Npcs.cs): our own BaseAI, so the game's path finding walks it (its navmesh, MoveTo, LookAt), and
    // nothing of a monster's own wants (hunting, fleeing, wandering) gets in the way. It does one task at a time:
    //   idle    stands; looks at whom (or where) it was told to, or at a player who comes near
    //   go      walks (or runs) to a point; arrived → idle
    //   follow  keeps near someone; far behind → runs; very far → steps in beside them
    //   wander  strolls about a point, now and then a few steps somewhere else in its circle
    //   sit     walks to a chair/bench and sits on it (or, with none, sits on the ground where it is)
    //   attack  fights one foe until it falls or gets away
    // And over every task, how it fights (Fight): off · defend (a foe that comes near it or the one it follows, or that
    // hit it) · hunt (any foe it senses). A fight puts the task aside and takes it up again after.
    //
    // Orders are words (Do), the same from the console, a scene, a rule:
    //   go x y z [run] · follow <user:id> [m] · stay · look <user:id>|none · lookat x y z · tp x y z · wander [r] [x y z]
    //   sit [x y z] · stand · emote <name> [loop] · fight off|defend|hunt · attack <user:id>
    // Runs where the NPC is owned (BaseAI.UpdateAI: only the owner).
    //
    // Never stuck: a walk that has not come closer for StuckSeconds steps the NPC near its goal (and says so in the
    // log and on the bus): a scene waiting for it never hangs.
    internal sealed class NpcMind : BaseAI
    {
        internal enum Task { Idle, Go, Follow, Wander, Sit, Attack }
        internal enum Fight { Off, Defend, Hunt }
        internal const float StuckSeconds = 8f, FarAway = 50f, DefendRange = 15f, Leash = 30f;

        private Task _task = Task.Idle, _before = Task.Idle;
        private Fight _fight = Fight.Defend;
        private Vector3 _to, _home, _lookAt;
        private bool _run, _hasLookAt;
        private float _near = 1.2f, _keep = 3f, _radius = 6f;
        private ZDOID _follow = ZDOID.None, _look = ZDOID.None, _foe = ZDOID.None;
        private float _best, _bestAt, _nextWander, _nextFoeCheck, _foeSince;
        private Chair _chair;
        private bool _seated, _told;
        private Character _hurtBy;
        private float _hurtAt = -100f;
        internal string Status = "стоїть";

        internal Task Now { get { return _task; } }
        internal Fight Fighting { get { return _fight; } }

        private NpcActor _actor;
        private NpcActor Actor { get { if (_actor == null) _actor = GetComponent<NpcActor>(); return _actor; } }

        public override bool UpdateAI(float dt)
        {
            if (!base.UpdateAI(dt)) return false;
            try { Think(dt); }
            catch (Exception e) { if (!_told) { _told = true; Npcs.Log("mind: " + e); } }
            return true;
        }

        private void Think(float dt)
        {
            if (m_character.IsDead()) return;
            if (_task != Task.Attack && _task != Task.Sit && Time.time >= _nextFoeCheck) { _nextFoeCheck = Time.time + 0.5f; LookForFoe(); }
            switch (_task)
            {
                case Task.Go: Going(dt); break;
                case Task.Follow: Following(dt); break;
                case Task.Wander: Wandering(dt); break;
                case Task.Sit: Sitting(dt); break;
                case Task.Attack: Attacking(dt); break;
                default: Idling(); break;
            }
        }

        // ---------------------------------------------------------------- the tasks
        private void Idling()
        {
            StopMoving();
            LookAround(6f);
        }

        // whom/where it was told to look at, else a player close by (the first bit of a mind: it notices you)
        private void LookAround(float near)
        {
            GameObject who = _look.IsNone() ? null : Find(_look);
            if (who != null) { LookAt(Eye(who)); return; }
            if (_hasLookAt) { LookAt(_lookAt); return; }
            Player p = Player.GetClosestPlayer(transform.position, near);
            if (p != null) LookAt(p.GetEyePoint());
        }

        private void Going(float dt)
        {
            float d = Utils.DistanceXZ(transform.position, _to);
            if (d <= _near + 0.6f) { Arrived(); return; }
            if (d < _best - 0.5f) { _best = d; _bestAt = Time.time; }
            if (Time.time - _bestAt > StuckSeconds) { Unstick(_to, "stuck " + Mathf.RoundToInt(d) + " m from its goal"); Arrived(); return; }
            MoveTo(dt, _to, _near, _run);
        }

        private void Following(float dt)
        {
            GameObject who = Find(_follow);
            if (who == null) { Status = "загубив, кого вів"; _task = Task.Idle; Npcs.Log(Name() + ": lost whom it followed"); return; }
            Vector3 at = who.transform.position;
            float d = Utils.DistanceXZ(transform.position, at);
            if (d > FarAway) { Unstick(at - who.transform.forward * 2f, "too far behind (" + Mathf.RoundToInt(d) + " m)"); return; }
            if (d > _keep)
            {
                if (d < _best - 0.5f || d < _keep + 2f) { _best = d; _bestAt = Time.time; }
                if (Time.time - _bestAt > StuckSeconds) { Unstick(at - who.transform.forward * 2f, "could not keep up"); return; }
                MoveTo(dt, at, _keep, d > 8f);
                Status = "іде слідом";
            }
            else { StopMoving(); LookAt(Eye(who)); _best = d; _bestAt = Time.time; Status = "поруч"; }
        }

        private void Wandering(float dt)
        {
            float d = Utils.DistanceXZ(transform.position, _to);
            bool walking = d > 1.2f && Time.time - _bestAt < StuckSeconds;
            if (walking) { if (d < _best - 0.5f) { _best = d; _bestAt = Time.time; } MoveTo(dt, _to, 1f, false); Status = "походжає"; return; }
            StopMoving();
            LookAround(8f);
            if (Time.time < _nextWander) return;
            _nextWander = Time.time + UnityEngine.Random.Range(6f, 15f);
            Vector2 r = UnityEngine.Random.insideUnitCircle * _radius;
            Vector3 p = _home + new Vector3(r.x, 0f, r.y);
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(p, out h)) p.y = h;
            if (Pathfinding.instance != null && !Pathfinding.instance.HavePath(transform.position, p, m_pathAgentType)) return; // (try another spot next time)
            _to = p; _best = float.MaxValue; _bestAt = Time.time;
        }

        private void Sitting(float dt)
        {
            if (_chair == null) // on the ground: walks to the spot (when given one), then the «sit» pose
            {
                float gd = Utils.DistanceXZ(transform.position, _to);
                if (!_seated && gd > 1.4f && Time.time - _bestAt < StuckSeconds)
                {
                    if (gd < _best - 0.5f) { _best = gd; _bestAt = Time.time; }
                    MoveTo(dt, _to, 1f, false); Status = "іде сісти"; return;
                }
                if (!_seated) { _seated = true; Actor.Emote("sit", true); Status = "сидить на землі"; StoryBus.Raise("npc_sat", Name(), 0, transform.position); }
                StopMoving();
                return;
            }
            Transform seat = _chair.m_attachPoint != null ? _chair.m_attachPoint : _chair.transform;
            if (!_seated)
            {
                float d = Utils.DistanceXZ(transform.position, seat.position);
                if (d > 1.4f)
                {
                    if (d < _best - 0.5f) { _best = d; _bestAt = Time.time; }
                    if (Time.time - _bestAt > StuckSeconds) Unstick(seat.position, "could not reach its seat");
                    else { MoveTo(dt, seat.position, 1f, false); Status = "іде сісти"; return; }
                }
                _seated = true;
                Actor.Emote("", false);
                m_animator.SetBool(_chair.m_attachAnimation, true);
                Status = "сидить";
                StoryBus.Raise("npc_sat", Name(), 0, seat.position);
            }
            // held on the seat as the game holds a player (no gravity, no drift)
            StopMoving();
            transform.position = seat.position;
            transform.rotation = seat.rotation;
            if (m_body != null) { m_body.useGravity = false; m_body.linearVelocity = Vector3.zero; m_body.angularVelocity = Vector3.zero; }
        }

        private void StandUp()
        {
            if (_task != Task.Sit) return;
            if (_chair != null && _seated)
            {
                m_animator.SetBool(_chair.m_attachAnimation, false);
                Transform seat = _chair.m_attachPoint != null ? _chair.m_attachPoint : _chair.transform;
                transform.position = seat.TransformPoint(_chair.m_detachOffset);
                if (m_body != null) { m_body.position = transform.position; m_body.useGravity = true; }
            }
            else if (_seated) Actor.Emote("", false);
            _seated = false; _chair = null;
        }

        // ---------------------------------------------------------------- fighting
        // a foe for its way of fighting (none while sitting: it stands up only when told, or when hit)
        private void LookForFoe()
        {
            if (_fight == Fight.Off) return;
            Character foe = null;
            if (_hurtBy != null && !_hurtBy.IsDead() && !_hurtBy.IsPlayer() && Time.time - _hurtAt < 20f) foe = _hurtBy;
            if (foe == null)
            {
                Vector3 guard = transform.position;
                GameObject leader = _task == Task.Follow ? Find(_follow) : null;
                float range = _fight == Fight.Hunt ? m_viewRange : DefendRange;
                float best = range;
                foreach (Character c in Character.GetAllCharacters())
                {
                    if (c == null || c == m_character || c.IsDead() || c.IsPlayer() || !IsEnemy(m_character, c)) continue;
                    float d = Vector3.Distance(c.transform.position, guard);
                    if (leader != null) d = Mathf.Min(d, Vector3.Distance(c.transform.position, leader.transform.position));
                    if (d >= best) continue;
                    if (_fight == Fight.Hunt && !CanSenseTarget(c)) continue;
                    best = d; foe = c;
                }
            }
            if (foe != null) StartFight(foe);
        }

        private void StartFight(Character foe)
        {
            ZNetView nv = foe.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            if (_task != Task.Attack)
            {
                _before = _task == Task.Sit ? Task.Idle : _task;
                if (_task != Task.Wander) _home = transform.position; // (a wanderer keeps its circle's middle)
            }
            StandUp();
            Actor.Emote("", false);
            _foe = nv.GetZDO().m_uid; _task = Task.Attack; _foeSince = Time.time;
            Status = "б'ється";
            Npcs.Log(Name() + ": fights " + foe.m_name);
        }

        private void Attacking(float dt)
        {
            GameObject go = Find(_foe);
            Character foe = go != null ? go.GetComponent<Character>() : null;
            Humanoid me = m_character as Humanoid;
            if (foe == null || foe.IsDead() || me == null) { EndFight(foe != null && foe.IsDead() ? "won" : "lost sight"); return; }
            float d = Vector3.Distance(transform.position, foe.transform.position);
            GameObject leader = _before == Task.Follow ? Find(_follow) : null;
            Vector3 anchor = leader != null ? leader.transform.position : _home;
            if (Vector3.Distance(foe.transform.position, anchor) > Leash && _fight != Fight.Hunt) { EndFight("it ran too far"); return; }
            ItemDrop.ItemData weapon = me.GetCurrentWeapon();
            float reach = weapon != null ? Mathf.Max(1.6f, weapon.m_shared.m_aiAttackRange) : 1.6f;
            if (d > reach)
            {
                MoveTo(dt, foe.transform.position, reach * 0.8f, true);
                if (Time.time - _foeSince > 40f) { EndFight("could not reach it"); return; }
                return;
            }
            StopMoving();
            LookAt(foe.GetCenterPoint());
            float interval = weapon != null ? Mathf.Max(0.8f, weapon.m_shared.m_aiAttackInterval) : 1.5f;
            if (weapon != null && !m_character.InAttack() && Time.time - weapon.m_lastAttackTime > interval && IsLookingAt(foe.GetCenterPoint(), 25f))
                m_character.StartAttack(foe, false);
        }

        private void EndFight(string why)
        {
            Npcs.Log(Name() + ": fight over (" + why + ")");
            StoryBus.Raise("npc_fight_over", Name(), why == "won" ? 1 : 0, transform.position);
            if (Actor != null) Actor.FireRule("fight_over");
            _foe = ZDOID.None; _hurtBy = null;
            _task = _before == Task.Attack ? Task.Idle : _before;
            _best = float.MaxValue; _bestAt = Time.time;
            Status = "стоїть";
        }

        // hit (even when it cannot be hurt: NpcDamagePatch tells it): a foe to answer, unless it is a player
        internal void HurtBy(Character attacker)
        {
            if (attacker == null || attacker == m_character) return;
            _hurtBy = attacker; _hurtAt = Time.time;
            if (!attacker.IsPlayer() && _fight != Fight.Off && _task != Task.Attack) StartFight(attacker);
        }

        // ---------------------------------------------------------------- small things
        private void Arrived()
        {
            StopMoving();
            _task = Task.Idle;
            Status = "прийшов";
            StoryBus.Raise("npc_arrived", Name(), 0, transform.position);
        }

        // stepped to the place (on the ground), not left standing in a hole: the scene goes on
        private void Unstick(Vector3 to, string why)
        {
            float h;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetSolidHeight(to, out h)) to.y = h + 0.1f;
            transform.position = to;
            if (m_body != null) { m_body.position = to; m_body.linearVelocity = Vector3.zero; }
            _best = float.MaxValue; _bestAt = Time.time;
            Npcs.Log(Name() + ": " + why + " — stepped to " + Mathf.RoundToInt(to.x) + "," + Mathf.RoundToInt(to.z));
            StoryBus.Raise("npc_stuck", Name(), 0, to);
        }

        private void Begin(Task t, string status)
        {
            StandUp();
            if (t != Task.Idle) Actor.Emote("", false);
            _task = t; Status = status;
            _best = float.MaxValue; _bestAt = Time.time;
        }

        // ---------------------------------------------------------------- orders
        internal void Do(string order)
        {
            if (string.IsNullOrEmpty(order)) return;
            string[] w = order.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            Vector3 v;
            switch (w[0])
            {
                case "go":
                    if (!Vec(w, 1, out v)) return;
                    _to = v; _run = Array.IndexOf(w, "run") > 0; _near = 1.2f;
                    Begin(Task.Go, _run ? "біжить" : "іде");
                    break;
                case "follow":
                    _follow = Id(w, 1); if (_follow.IsNone()) return;
                    _keep = w.Length > 2 ? Mathf.Clamp(Errands.F(w[2], 3f), 1.5f, 20f) : 3f;
                    Begin(Task.Follow, "іде слідом");
                    break;
                case "wander":
                    _radius = w.Length > 1 ? Mathf.Clamp(Errands.F(w[1], 6f), 2f, 40f) : 6f;
                    _home = Vec(w, 2, out v) ? v : transform.position;
                    _to = transform.position; _nextWander = Time.time + 1f;
                    Begin(Task.Wander, "походжає");
                    break;
                case "sit":
                    Vector3 near = Vec(w, 1, out v) ? v : transform.position;
                    Begin(Task.Sit, "сідає");
                    _chair = NearestChair(near, 4f); _seated = false; _to = near;
                    if (_chair == null) Npcs.Log(Name() + ": no seat near — sits on the ground");
                    break;
                case "stand": case "stay": Begin(Task.Idle, "стоїть"); StopMoving(); break;
                case "look": _look = w.Length > 1 && w[1] == "none" ? ZDOID.None : Id(w, 1); if (_look.IsNone()) _hasLookAt = false; break;
                case "lookat": if (Vec(w, 1, out v)) { _lookAt = v; _hasLookAt = true; _look = ZDOID.None; } break;
                case "tp": if (Vec(w, 1, out v)) Unstick(v, "told to step"); break;
                case "emote":
                    if (w.Length < 2) return;
                    if (_task != Task.Idle && _task != Task.Wander) Begin(Task.Idle, "стоїть");
                    StopMoving();
                    Actor.Emote(w[1] == "none" ? "" : w[1], Array.IndexOf(w, "loop") > 1);
                    break;
                case "fight":
                    _fight = w.Length > 1 && w[1] == "off" ? Fight.Off : w.Length > 1 && w[1] == "hunt" ? Fight.Hunt : Fight.Defend;
                    if (_fight == Fight.Off && _task == Task.Attack) EndFight("told to stop");
                    break;
                case "attack":
                    GameObject go = Find(Id(w, 1));
                    Character c = go != null ? go.GetComponent<Character>() : null;
                    if (c != null && !c.IsDead()) StartFight(c);
                    break;
                default: Npcs.Log(Name() + ": unknown order «" + order + "»"); return;
            }
            Npcs.Log(Name() + ": " + order);
        }

        private static Chair NearestChair(Vector3 at, float range)
        {
            Chair best = null;
            float bd = range;
            foreach (Collider col in Physics.OverlapSphere(at, range, LayerMask.GetMask("piece", "piece_nonsolid", "Default", "Default_small")))
            {
                Chair ch = col.GetComponentInParent<Chair>();
                if (ch == null || ch.m_inShip) continue;
                Transform s = ch.m_attachPoint != null ? ch.m_attachPoint : ch.transform;
                float d = Vector3.Distance(s.position, at);
                if (d < bd) { bd = d; best = ch; }
            }
            return best;
        }

        private static bool Vec(string[] w, int at, out Vector3 v)
        {
            v = Vector3.zero;
            if (w.Length < at + 3) return false;
            float x, y, z;
            if (!float.TryParse(w[at], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)) return false;
            if (!float.TryParse(w[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)) return false;
            if (!float.TryParse(w[at + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }

        // "user:id" → a ZDOID
        private static ZDOID Id(string[] w, int at)
        {
            if (w.Length <= at) return ZDOID.None;
            int c = w[at].IndexOf(':');
            long u; uint i;
            if (c <= 0 || !long.TryParse(w[at].Substring(0, c), out u) || !uint.TryParse(w[at].Substring(c + 1), out i)) return ZDOID.None;
            return new ZDOID(u, i);
        }

        internal static string IdOf(ZDOID id) { return id.UserID + ":" + id.ID; }

        private static GameObject Find(ZDOID id) { return ZNetScene.instance != null && !id.IsNone() ? ZNetScene.instance.FindInstance(id) : null; }

        private static Vector3 Eye(GameObject go)
        {
            Character c = go.GetComponent<Character>();
            return c != null ? c.GetEyePoint() : go.transform.position + Vector3.up * 1.6f;
        }

        private string Name() { NpcActor a = Actor; return a != null ? (a.Tag.Length > 0 ? a.Tag : a.Id) : name; }
    }
}
#endif

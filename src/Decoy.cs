#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Обманка», Hugin's gift: he flies to the enemy you look at (or the nearest one within reach), circles its head and
    // teases it. For a while it fights nobody but him: it turns after him, follows and swings at the air where he flies,
    // and its blows hurt no one (Hugin dodges, a few feathers fly; he cannot be hurt). Struck by players HitsToBreak
    // times, it sees through it: it turns on whoever struck last, and Hugin comes home. From GangOneAt trust one more
    // enemy close by joins in, from GangTwoAt two. Not on bosses. Pressed again: Hugin comes back at once.
    // A creature's mind runs on the game that owns it: the caster tells every game who is lured and from when; each
    // monster then fights the air on whichever game drives it, every game draws Hugin's circling the same way (the
    // circle is a formula of the world's clock), and the game that owns it counts the blows and calls it off.
    internal static class Decoy
    {
        internal static float Seconds = 8f, SecondsTop = 20f;
        internal static float Cooldown = 600f, CooldownTop = 300f; // seconds: 10 min at its first level, 5 at the top
        internal static float Range = 30f;
        internal static int HitsToBreak = 3;
        internal static int GangOneAt = 6, GangTwoAt = 7;
        internal static float GangReach = 5f;
        // the circle fits the creature: low and tight round a small one, wide and high round a big one
        internal static float OrbitSpeed = 7f, OrbitSpeedSmall = 4.5f;   // m/s round a big one / a small one
        internal static float OrbitOut = 0.6f, OrbitOutPer = 0.7f;       // m off its side: this, plus its own radius × (1 + OrbitOutPer)
        internal static float OrbitUp = 0.25f, OrbitUpPer = 0.22f;      // m over its head: this, plus its height × OrbitUpPer

        private const string RpcStart = "RavenWatch_Decoy", RpcEnd = "RavenWatch_DecoyEnd";
        internal static float Duration() { return Trust.RoundTime(Trust.LerpFrom(false, RavenAbilities.UnlockLevel(RavenAbilities.Find("hdecoy")), Seconds, SecondsTop) * GiftUp.Power("hdecoy")); } // (from the gift's own first level)

        // a creature lured, on every game (the owner of the creature acts on it; the others draw and wait)
        private sealed class Lure
        {
            public ZDOID Caster, Main;
            public double Start;
            public float Until, Seed, NextSwing;
            public int Hits;
            public bool Strikes; // «Обманка» III: its swings land on the creatures next to it
        }
        private static readonly Dictionary<ZDOID, Lure> Lured = new Dictionary<ZDOID, Lure>();

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(RpcStart, new Action<long, ZPackage>(OnStart));
            ZRoutedRpc.instance.Register<ZPackage>(RpcEnd, new Action<long, ZPackage>(OnEnd));
            if ((MoveToM == null || LookAtM == null || BestAttackM == null) && Plugin.Instance != null)
                Plugin.Instance.Log("Decoy: the game's creature AI has changed (MoveTo " + (MoveToM != null) + ", LookAt " + (LookAtM != null) + ", SelectBestAttack " + (BestAttackM != null) + "): lured creatures may not follow Hugin");
        }

        internal static void Reset() { Lured.Clear(); DecoyMark.CloseAll(); }

        // your Hugin is out luring now
        internal static bool Mine() { return DecoyFlight.Out(Player.m_localPlayer); }
        internal static float Left()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return 0f;
            ZDOID me = p.GetZDOID();
            float most = 0f;
            foreach (Lure l in Lured.Values) if (l.Caster == me) most = Mathf.Max(most, l.Until - Time.time);
            return Mathf.Max(0f, most);
        }

        internal static bool Any { get { return Lured.Count > 0; } }

        // ---------------------------------------------------------------- the caster's game

        // why not now, or null
        internal static string Refused(Player p)
        {
            if (Dome.Inside(p.transform.position)) return Lang.T("Під куполом ніхто не б'ється — нікого й дражнити");
            return null;
        }

        // who: the one in your sights (or the nearest), and from the higher trust one or two close by it; null with a
        // reason when there is nobody fit
        internal static List<Character> Targets(Player p, int level, out string why)
        {
            why = null;
            string note;
            List<Character> found = Strike.Targets(p, false, Range, out note);
            Character main = found.Count > 0 ? found[0] : null;
            if (main == null) { why = note ?? Lang.T("Нема кого обманювати — жодного ворога поблизу"); return null; }
            if (main.IsBoss()) { why = Lang.T("Цей на таке не купиться — боса не обдуриш"); return null; }
            if (main.GetComponent<MonsterAI>() == null) { why = Lang.T("Цей і так не б'ється — нема кого відволікати"); return null; }
            List<Character> list = new List<Character> { main };
            int more = level >= GangTwoAt ? 2 : level >= GangOneAt ? 1 : 0;
            if (more > 0)
            {
                List<Character> near = new List<Character>();
                foreach (Character c in Character.GetAllCharacters())
                    if (c != main && Strike.Prey(p, c) && !c.IsBoss() && c.GetComponent<MonsterAI>() != null && Vector3.Distance(c.transform.position, main.transform.position) < GangReach) near.Add(c);
                near.Sort((a, b) => Vector3.Distance(a.transform.position, main.transform.position).CompareTo(Vector3.Distance(b.transform.position, main.transform.position)));
                for (int i = 0; i < near.Count && i < more; i++) list.Add(near[i]);
            }
            return list;
        }

        // the gift: Hugin flies off, every game is told who is lured
        internal static void Begin(Player p, List<Character> who)
        {
            float seconds = Duration();
            float seed = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(seconds);
            pkg.Write(seed);
            pkg.Write(WorldTime.Now);
            pkg.Write(who.Count);
            foreach (Character c in who) pkg.Write(c.GetZDOID());
            pkg.Write(Facets.On("hdecoy")); // (read after the creatures; an older mod stops before it)
            pkg.SetPos(0);
            Started(pkg); // here at once (and the flight), then the others
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcStart, pkg);
        }

        // pressed again: Hugin gives it up and comes home
        internal static void Recall(Player p)
        {
            if (p == null) return;
            End(p.GetZDOID(), ZDOID.None, ZDOID.None);
        }

        private static void OnStart(long sender, ZPackage pkg) { if (sender != ZDOMan.GetSessionID()) Started(pkg); } // (our own: done already)

        private static void Started(ZPackage pkg)
        {
            try
            {
                ZDOID caster = pkg.ReadZDOID();
                float seconds = Mathf.Clamp(pkg.ReadSingle(), 0f, 30f);
                float seed = pkg.ReadSingle();
                double start = pkg.ReadDouble();
                int n = Mathf.Min(pkg.ReadInt(), 8);
                if (n <= 0) return;
                ZDOID main = ZDOID.None;
                for (int i = 0; i < n; i++)
                {
                    ZDOID id = pkg.ReadZDOID();
                    if (i == 0) main = id;
                    if (id == ZDOID.None) continue;
                    Lured[id] = new Lure { Caster = caster, Main = main, Start = start, Until = Time.time + seconds, Seed = seed, NextSwing = Time.time + 0.6f };
                    DecoyMark.On(Find(id), id); // the dizzy lights over its head
                }
                if (pkg.GetPos() < pkg.Size() && pkg.ReadBool())
                    foreach (Lure lu in Lured.Values) if (lu.Caster == caster && lu.Start == start) lu.Strikes = true;
                // the bird: the caster's own Hugin, drawn on every game near enough
                Character target = Find(main);
                Player me = Player.m_localPlayer;
                foreach (Player pl in Player.GetAllPlayers())
                {
                    if (pl == null || pl.GetZDOID() != caster) continue;
                    if (pl != me && me != null && Vector3.Distance(me.transform.position, pl.transform.position) > 120f) return;
                    if (target != null) DecoyFlight.Fly(pl, target, main, seed, start, Time.time + seconds);
                    return;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Decoy message unreadable: " + e.Message); }
        }

        // over for a caster's lures: time, pressed again, or seen through (broken: the creature; by: who struck it)
        private static void End(ZDOID caster, ZDOID broken, ZDOID by)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(caster);
            pkg.Write(broken);
            pkg.Write(by);
            pkg.SetPos(0);
            Ended(pkg);
            if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcEnd, pkg);
        }

        private static readonly List<ZDOID> Gone = new List<ZDOID>();

        private static void OnEnd(long sender, ZPackage pkg) { if (sender != ZDOMan.GetSessionID()) Ended(pkg); }

        private static void Ended(ZPackage pkg)
        {
            try
            {
                ZDOID caster = pkg.ReadZDOID(), broken = pkg.ReadZDOID(), by = pkg.ReadZDOID();
                Gone.Clear();
                foreach (KeyValuePair<ZDOID, Lure> kv in Lured) if (kv.Value.Caster == caster) Gone.Add(kv.Key);
                foreach (ZDOID id in Gone) Lured.Remove(id);
                DecoyFlight.Home(caster, broken != ZDOID.None);
                // the one that saw through it goes for whoever struck it (on the game that drives it)
                if (broken != ZDOID.None && by != ZDOID.None)
                {
                    Character c = Find(broken), attacker = Find(by);
                    ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
                    MonsterAI ai = c != null ? c.GetComponent<MonsterAI>() : null;
                    if (ai != null && attacker != null && nv != null && nv.IsValid() && nv.IsOwner() && SetTargetM != null) { try { SetTargetM.Invoke(ai, new object[] { attacker }); } catch { } }
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Decoy end unreadable: " + e.Message); }
        }

        internal static Character Find(ZDOID id)
        {
            if (id == ZDOID.None || ZNetScene.instance == null) return null;
            GameObject go = ZNetScene.instance.FindInstance(id);
            return go != null ? go.GetComponent<Character>() : null;
        }

        // ---------------------------------------------------------------- where Hugin circles (the same on every game)

        internal static Vector3 Orbit(Character c, float seed, double start)
        {
            float radius = Mathf.Clamp(Body.Radius(c), 0.15f, 6f);
            Vector3 head = Body.Head(c);
            float tall = Mathf.Clamp(head.y - c.transform.position.y, 0.2f, 12f);
            float big = Mathf.Clamp01((radius - 0.3f) / 1.7f); // 0: a neck, a greyling … 1: a troll and bigger
            float r = OrbitOut + radius * (1f + OrbitOutPer);
            float speed = Mathf.Lerp(OrbitSpeedSmall, OrbitSpeed, big);
            float a = seed + (float)(WorldTime.Now - start) * (speed / Mathf.Max(0.4f, r));
            float bob = Mathf.Lerp(0.12f, 0.4f, big);
            return head + Vector3.up * (OrbitUp + tall * OrbitUpPer) + new Vector3(Mathf.Cos(a) * r, bob * Mathf.Sin(a * 1.7f), Mathf.Sin(a) * r);
        }

        // lured now (the mark over its head): on every game
        internal static bool IsLured(ZDOID id) { Lure l; return Lured.TryGetValue(id, out l) && Time.time < l.Until; }

        // ---------------------------------------------------------------- the creature (only on the game that drives it)

        private static readonly MethodInfo MoveToM = AccessTools.Method(typeof(BaseAI), "MoveTo", new[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) });
        private static readonly MethodInfo LookAtM = AccessTools.Method(typeof(BaseAI), "LookAt", new[] { typeof(Vector3) });
        private static readonly MethodInfo StopM = AccessTools.Method(typeof(BaseAI), "StopMoving");
        private static readonly MethodInfo BestAttackM = AccessTools.Method(typeof(MonsterAI), "SelectBestAttack");
        private static readonly MethodInfo SetTargetM = AccessTools.Method(typeof(MonsterAI), "SetTarget", new[] { typeof(Character) });
        private static readonly MethodInfo AlertM = AccessTools.Method(typeof(BaseAI), "SetAlerted", new[] { typeof(bool) });
        private static readonly FieldInfo TargetF = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");

        // instead of its own thinking while lured: after Hugin, swinging at the air where he flies
        internal static bool Drives(MonsterAI ai, float dt)
        {
            Character c = ai.GetComponent<Character>();
            if (c == null) return false;
            ZDOID id = c.GetZDOID();
            Lure l;
            if (!Lured.TryGetValue(id, out l)) return false;
            if (Time.time >= l.Until || c.IsDead()) { Lured.Remove(id); return false; } // (time up: the caster's game calls Hugin home)
            Character main = l.Main == id ? c : Find(l.Main);
            if (main == null || main.IsDead()) { Lured.Remove(id); return false; }
            if (MoveToM == null || LookAtM == null) return false; // the game changed: it thinks for itself
            Vector3 at = Orbit(main, l.Seed, l.Start);
            try
            {
                if (TargetF != null) TargetF.SetValue(ai, null); // nobody but him
                if (AlertM != null) AlertM.Invoke(ai, new object[] { true });
                Humanoid h = c as Humanoid;
                ItemDrop.ItemData weapon = h != null && BestAttackM != null ? BestAttackM.Invoke(ai, new object[] { h, dt }) as ItemDrop.ItemData : null;
                float reach = weapon != null ? Mathf.Max(1.5f, weapon.m_shared.m_aiAttackRange) : 2f;
                Vector3 ground = new Vector3(at.x, c.transform.position.y, at.z);
                float off = Vector3.Distance(c.transform.position, ground);
                if (off > reach * 0.8f) MoveToM.Invoke(ai, new object[] { dt, ground, reach * 0.6f, off > 6f });
                else if (StopM != null) StopM.Invoke(ai, null);
                LookAtM.Invoke(ai, new object[] { at });
                if (h != null && weapon != null && Time.time >= l.NextSwing && off <= reach + 1f && !h.InAttack())
                {
                    l.NextSwing = Time.time + UnityEngine.Random.Range(1.4f, 2.4f);
                    h.StartAttack(null, false); // at the air (as at a wall: no creature to aim at)
                    if (l.Strikes) Neighbour(c, weapon, Mathf.Min(reach + 0.5f, NeighbourReach)); // (III: it lands on the one next to it)
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Decoy: a lured creature could not be driven: " + e.Message); Lured.Remove(id); return false; }
            return true;
        }

        internal static float NeighbourReach = 3f;

        // «Обманка» III: the nearest other creature in reach takes the swing (its weapon's damage, by its stars)
        // (a close blow only: a bow or a spit does not hit the one beside it; bosses and harmless animals are not hit)
        private static void Neighbour(Character c, ItemDrop.ItemData weapon, float reach)
        {
            if (weapon == null || weapon.m_shared.m_attack == null || weapon.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile) return;
            Character best = null; float bd = reach;
            foreach (Character o in Character.GetAllCharacters())
            {
                if (o == null || o == c || o.IsPlayer() || o.IsDead() || o.IsTamed() || o.IsBoss() || o.m_faction == Character.Faction.AnimalsVeg || o.m_faction == Character.Faction.Dverger || o.m_faction == Character.Faction.Players) continue;
                float dd = Vector3.Distance(o.transform.position, c.transform.position);
                if (dd < bd) { bd = dd; best = o; }
            }
            if (best == null || weapon == null) return;
            HitData h = new HitData();
            h.m_damage = weapon.GetDamage();
            h.m_damage.Modify(1f + Mathf.Max(0, c.GetLevel() - 1) * 0.5f);
            h.m_point = best.GetCenterPoint();
            h.m_dir = (best.transform.position - c.transform.position).normalized;
            h.m_skill = Skills.SkillType.None;
            h.m_skillRaiseAmount = Strike.Marker - Mathf.Max(1f, h.GetTotalDamage()); // (marked: no skill; the mark is also its cap, Strike.Cut)
            h.SetAttacker(null); // (nobody's: not the lured one's, its blows hurt nobody while lured)
            best.Damage(h);
        }

        // its blows while lured hurt nobody (it swings at a raven that is never there when it lands)
        internal static bool Harmless(HitData hit)
        {
            if (Lured.Count == 0 || hit == null) return false;
            Character a = hit.GetAttacker();
            return a != null && Lured.ContainsKey(a.GetZDOID());
        }

        // a player's blow on a lured one (counted on the game that owns it): the third and it sees through it
        internal static void Struck(Character c, HitData hit)
        {
            if (Lured.Count == 0 || c == null || hit == null) return;
            Lure l;
            if (!Lured.TryGetValue(c.GetZDOID(), out l)) return;
            Character a = hit.GetAttacker();
            if (!(a is Player) || hit.GetTotalDamage() <= 0f) return;
            // a blow, not a raven's (strike, bleeding) nor a wound ticking on its own (fire, poison): those are no players' blows
            if (hit.m_skillRaiseAmount <= Strike.Marker || !Marks.RealBlow(hit)) return;
            if (++l.Hits < HitsToBreak) return;
            End(l.Caster, c.GetZDOID(), a.GetZDOID());
        }

        // the caster's game: a creature lured by you is gone (time up, died, left): when none is left, Hugin comes home
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null || Lured.Count == 0) return;
            ZDOID me = p.GetZDOID();
            bool mine = false, live = false;
            foreach (KeyValuePair<ZDOID, Lure> kv in Lured)
            {
                if (kv.Value.Caster != me) continue;
                mine = true;
                Character c = Find(kv.Key);
                if (Time.time < kv.Value.Until && c != null && !c.IsDead()) live = true;
            }
            if (mine && !live) End(me, ZDOID.None, ZDOID.None);
        }
    }

    // Over a lured creature, on every game: three small lights of Hugin's blue circling over its head, like stars after a
    // blow — so everyone sees who is fooled. They come in quickly and fade when the lure is over.
    internal class DecoyMark : MonoBehaviour
    {
        private static readonly Dictionary<ZDOID, DecoyMark> Marks = new Dictionary<ZDOID, DecoyMark>();
        private static Mesh _quad;
        private static MaterialPropertyBlock _block;
        private static readonly Color Glow = new Color(1.1f, 1.8f, 2.6f); // past white: the game's glow takes it
        private const int Count = 3;
        private Character _c;
        private ZDOID _id;
        private float _alpha, _spin;
        private readonly Transform[] _dot = new Transform[Count];
        private readonly Renderer[] _r = new Renderer[Count];

        internal static void On(Character c, ZDOID id)
        {
            if (c == null) return;
            DecoyMark m;
            if (Marks.TryGetValue(id, out m) && m != null) return;
            Material mat = RavenAura.Dot();
            if (mat == null) return;
            GameObject go = new GameObject("RavenWatch_DecoyMark");
            m = go.AddComponent<DecoyMark>();
            m._c = c; m._id = id; m._spin = UnityEngine.Random.Range(0f, 6.3f);
            for (int i = 0; i < Count; i++)
            {
                GameObject d = new GameObject("dot");
                d.transform.SetParent(go.transform, false);
                d.AddComponent<MeshFilter>().sharedMesh = Quad();
                MeshRenderer r = d.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false; // (shown from its first frame, coloured: not a frame of the material's plain white)
                m._dot[i] = d.transform; m._r[i] = r;
            }
            Marks[id] = m;
        }

        private static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh();
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            _quad.hideFlags = HideFlags.HideAndDontSave;
            return _quad;
        }

        private void LateUpdate()
        {
            bool lured = _c != null && !_c.IsDead() && Decoy.IsLured(_id);
            _alpha = Mathf.MoveTowards(_alpha, lured ? 1f : 0f, Time.deltaTime * (lured ? 4f : 2f));
            if (_c == null || (!lured && _alpha <= 0f)) { Destroy(gameObject); return; }
            float radius = Mathf.Clamp(Body.Radius(_c), 0.15f, 6f);
            Vector3 head = Body.Head(_c);
            float ring = Mathf.Clamp(radius * 0.45f, 0.16f, 1.1f), size = Mathf.Clamp(radius * 0.18f, 0.07f, 0.35f);
            Vector3 centre = head + Vector3.up * (0.12f + ring * 0.5f);
            _spin += Time.deltaTime * 4.5f;
            Camera cam = Utils.GetMainCamera();
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < Count; i++)
            {
                if (_dot[i] == null) continue;
                float a = _spin + i * (Mathf.PI * 2f / Count);
                Vector3 at = centre + new Vector3(Mathf.Cos(a) * ring, 0.25f * ring * Mathf.Sin(a * 2f), Mathf.Sin(a) * ring);
                _dot[i].position = at;
                _dot[i].localScale = Vector3.one * size * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f + i));
                if (cam != null) _dot[i].rotation = Quaternion.LookRotation(at - cam.transform.position);
                Color c = Glow; c.a = _alpha;
                _block.Clear(); _block.SetColor("_Color", c);
                _r[i].SetPropertyBlock(_block);
                _r[i].enabled = _alpha > 0.01f;
            }
        }

        private void OnDestroy() { DecoyMark m; if (Marks.TryGetValue(_id, out m) && m == this) Marks.Remove(_id); }

        internal static void CloseAll() { foreach (DecoyMark m in Marks.Values) if (m != null) Destroy(m.gameObject); Marks.Clear(); }
    }

    // Hugin out luring: to the creature, round and round its head (dodging its swings), and home. Drawn on every game.
    internal class DecoyFlight : MonoBehaviour, IRavenUser
    {
        private bool _yielded;
        public void Yield() { _yielded = true; DestroyImmediate(this); }

        private Player _owner;
        private Character _target;
        private ZDOID _targetId;
        private float _seed, _until, _born, _t0, _nextCaw, _dodgeAt;
        private double _start;
        private int _phase; // 0 out to it, 1 circling, 2 home
        private bool _seen;
        private ShoulderPin _pin;
        private ShoulderLife _life;
        private Unhooked _hook;
        private RavenMotor _motor;
        private FlightBody _body;
        private ParticleSystem _trail;
        private bool _clone;

        private static readonly Dictionary<Player, DecoyFlight> Flying = new Dictionary<Player, DecoyFlight>();

        internal static bool Out(Player p) { DecoyFlight f; return p != null && Flying.TryGetValue(p, out f) && f != null; }

        internal static void Fly(Player owner, Character target, ZDOID targetId, float seed, double start, float until)
        {
            if (owner == null || target == null) return;
            GameObject model = Shoulder.ModelOf(owner, false);
            if (model != null && !AnimDirector.CanTake(model, RavenAct.Strike)) model = null;
            bool clone = false;
            if (model == null) { model = StrikeFlight.StandIn(owner, false); clone = true; if (model == null) return; }
            DecoyFlight old = model.GetComponent<DecoyFlight>();
            if (old != null) DestroyImmediate(old);
            DecoyFlight f = model.AddComponent<DecoyFlight>();
            f._owner = owner; f._target = target; f._targetId = targetId; f._seed = seed; f._start = start; f._until = until; f._clone = clone;
            f._pin = model.GetComponent<ShoulderPin>();
            f._life = model.GetComponent<ShoulderLife>();
            f._motor = RavenMotor.Of(model, false, owner);
            AnimDirector.Take(model, RavenAct.Strike, f, clone ? "обманка, замінник" : "обманка");
            if (f._pin != null) f._pin.enabled = false;
            if (!clone) f._hook = Unhooked.From(model.transform);
            f._born = Time.time; f._t0 = Time.time; f._phase = 0;
            f._nextCaw = Time.time + 1f;
            Vector3 to = Body.Center(target) - model.transform.position;
            f._motor.Launch((to.sqrMagnitude > 0.01f ? to.normalized : owner.transform.forward) * 3f + Vector3.up * 2.5f, "удар", 0.7f);
            f._body = f._motor.Body;
            Flying[owner] = f;
            if (owner == Player.m_localPlayer || !Shadow.Flagged(owner)) f._trail = Feathers.Trail(model.transform, false);
            RavenFx.Play("sfx_raven_flap", model.transform.position, Quaternion.identity);
        }

        // the lure is over for this caster: home (seen through: with an angry caw)
        internal static void Home(ZDOID caster, bool seenThrough)
        {
            foreach (KeyValuePair<Player, DecoyFlight> kv in Flying)
            {
                if (kv.Key == null || kv.Value == null || kv.Key.GetZDOID() != caster || kv.Value._phase == 2) continue;
                kv.Value._seen = seenThrough;
                kv.Value.GoHome();
                return;
            }
        }

        private void GoHome()
        {
            _phase = 2; _t0 = Time.time;
            if (_seen) RavenFx.Play("sfx_raven_kaw", transform.position, Quaternion.identity);
            if (ReferenceEquals(_owner, Player.m_localPlayer))
                RavenSkills.GiftSay(false, _seen ? "hdecoy_seen" : "hdecoy_end", _seen ? Lang.T("Розкусив, бевзь. Лечу назад.") : Lang.T("Набридло дражнити. Я вдома."));
        }

        private Vector3 Seat()
        {
            if (_pin != null && _pin.Bone != null) return _pin.Bone.position + Vector3.up * 0.1f;
            return _owner != null ? _owner.GetEyePoint() : transform.position;
        }

        private void LateUpdate()
        {
            Step();
            if (this != null && _life != null && (_pin == null || !_pin.enabled)) _life.Apply();
        }

        private void Step()
        {
            float dt = Time.deltaTime;
            if (_owner == null || _owner.IsDead() || Time.time - _born > 60f) { Land(); return; }
            if (_phase != 2 && (_target == null || _target.IsDead() || Time.time >= _until + 1.5f)) GoHome(); // (the caster's game says it first; a drawing game goes by itself too)
            if (_phase == 2)
            {
                Vector3 seat = Seat();
                if (_motor.Home(seat, _owner, _body.HomeSpeed(Strike.ReturnSpeed, _owner.GetVelocity(), Time.time - _t0), dt, true, _born, false, Vector3.zero) || _motor.Stuck) Land();
                return;
            }
            Vector3 at = Decoy.Orbit(_target, _seed, _start);
            if (_phase == 0)
            {
                float left = _motor.Steer(at, Strike.Speed * 1.1f, false, 0.4f, dt, Vector3.zero, 1f, true);
                if (left < 1.2f || Time.time - _t0 > 12f) { _phase = 1; _t0 = Time.time; }
            }
            else
            {
                _motor.Steer(at, Decoy.OrbitSpeed * 1.25f, false, 0.7f, dt, Vector3.zero, 0.3f, true);
                // its swing: Hugin flicks up and aside, feathers fly; now and then a taunting caw
                Humanoid h = _target as Humanoid;
                if (h != null && h.InAttack() && Time.time >= _dodgeAt)
                {
                    _dodgeAt = Time.time + 1f;
                    Vector3 side = Vector3.Cross(Vector3.up, (transform.position - _target.transform.position).normalized);
                    _body.Kick(_body.Vel * 0.6f + Vector3.up * 3f + side * UnityEngine.Random.Range(-2f, 2f));
                    if (_life != null) _life.Flap(0.4f);
                    if (UnityEngine.Random.value < 0.6f) { Raven t = Shoulder.Template(false); if (t != null && t.m_despawnEffect != null) RavenFx.Own(t.m_despawnEffect, transform.position, Quaternion.identity, null, 0.3f, -1); }
                }
                if (Time.time >= _nextCaw) { _nextCaw = Time.time + UnityEngine.Random.Range(1.8f, 3.2f); RavenFx.Play("sfx_raven_kaw", transform.position, Quaternion.identity); }
            }
            if (this == null) return;
            _motor.Finish(dt, 0f);
        }

        private void Land()
        {
            Flying.Remove(_owner);
            AnimDirector.Release(gameObject, this);
            Feathers.EndTrail(_trail); _trail = null;
            if (_clone) { Destroy(gameObject); return; }
            if (_pin != null)
            {
                Vector3 from = transform.position;
                if (_hook != null) { _hook.Back(); _hook = null; }
                _pin.enabled = true;
                _pin.ComeBack(from, transform.rotation);
            }
            Destroy(this);
        }

        private void OnDestroy()
        {
            DecoyFlight f;
            if (_owner != null && Flying.TryGetValue(_owner, out f) && f == this) Flying.Remove(_owner);
            if (_hook != null) { _hook.Back(); _hook = null; }
            if (_pin != null && !_clone && !_yielded) _pin.enabled = true;
            Feathers.EndTrail(_trail); _trail = null;
            if (!_yielded) AnimDirector.Release(gameObject, this);
        }
    }

    // ---------------------------------------------------------------- patches

    // a lured creature fights the air instead of thinking (on the game that drives it)
    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class DecoyPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        { try {
            if (!Decoy.Any || !Decoy.Drives(__instance, dt)) return true;
            __result = true;
            return false;
        } catch (Exception e) { PatchGuard.Fail("DecoyPatch", e); return true; } }
    }

    // its blows while lured hurt nobody (on the game where it strikes)
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class DecoyHarmlessPatch
    {
        private static bool Prefix(HitData hit) { try { return !Decoy.Harmless(hit); } catch { return true; } }
    }

    // the players' blows on it are counted where it is owned
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class DecoyStruckPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            try { ZNetView nv = __instance.GetComponent<ZNetView>(); if (nv != null && nv.IsValid() && nv.IsOwner()) Decoy.Struck(__instance, hit); } catch { }
        }
    }
}
#endif

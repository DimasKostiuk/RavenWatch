#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Під крилом», Hugin's dome. Every game keeps the same list of domes (sent to everyone, again every few seconds
    // while one stands, so a player who comes later sees it too) and acts on it by itself: draws it, keeps the
    // monsters it runs at the edge, refuses damage inside, keeps the rain off the ones inside.
    internal static class Dome
    {
        internal static float Radius = 6f;
        internal static float Seconds = 60f, SecondsTop = 90f;
        internal static int UnlockLevel = 3;
        internal static float Duration() { return Trust.RoundTime(Trust.Lerp(false, Seconds, SecondsTop) * GearFx.GuardMul(false) * GiftUp.Power("dome")); } // (Спіраль захисту: longer)

        // Against a boss the dome is brittle: it takes this many of a boss's blows, whatever their size (a boss's
        // damage differs tenfold between the first and the last one, and with stars, world settings and mods), and
        // breaks on the last of them. Every hit on anyone under it is a blow: a swing that catches two costs two.
        internal static int BossBlows = 3;

        private const string Rpc = "RavenWatch_Dome", HitRpc = "RavenWatch_DomeHit";
        private const float ResendEvery = 4f;

        internal sealed class State
        {
            public long Owner;
            public Vector3 Pos;
            public float Radius;
            public double Until;
            public int Level;
            public float Hp, MaxHp; // boss blows it can still take
            public float HitAt = -99f; // the last boss blow it took (the shell flashes)
            public DomeLook Look;
        }

        private static readonly Dictionary<long, State> Active = new Dictionary<long, State>();
        private static float _nextResend, _nextClean;

        private static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; } }
        private static long Me { get { return ZDOMan.GetSessionID(); } }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnDome));
            ZRoutedRpc.instance.Register<float>(HitRpc, new Action<long, float>(OnHit));
        }

        // ---------------------------------------------------------------- casting

        // why it cannot be raised here, or null
        internal static string Refused(Player p)
        {
            if (p.InInterior()) return Lang.T("Тут, під землею, Хугіну тісно розправити крила");
            return null;
        }

        internal static void Raise(Player p, int level)
        {
            float hp = Mathf.Max(1, BossBlows);
            State s = new State { Owner = Me, Pos = p.transform.position, Radius = Mathf.Max(2f, Radius), Until = Now + Duration(), Level = level, Hp = hp, MaxHp = hp };
            Apply(s);
            Send(s);
            _nextResend = Time.time + ResendEvery;
        }

        // pressed again: Hugin folds his wings and comes back to the shoulder
        internal static void Lower()
        {
            State s;
            if (!Active.TryGetValue(Me, out s)) return;
            s.Until = 0;
            Send(s);
            End(s, false);
        }

        internal static bool Mine() { State s; return Active.TryGetValue(Me, out s) && s.Until > Now; }

        // inside your own dome (not a friend's)
        internal static bool InsideMine(Vector3 pos)
        {
            State s;
            if (!Active.TryGetValue(Me, out s) || s.Until <= Now) return false;
            Vector3 d = pos - s.Pos;
            if (Mathf.Abs(d.y) > s.Radius + 2f) return false;
            d.y = 0f;
            return d.sqrMagnitude <= s.Radius * s.Radius;
        }

        // a boss struck someone under it: it bursts for everyone
        private static void Burst(State s)
        {
            s.Until = 0;
            Send(s);
            End(s, true);
        }

        private static void Send(State s)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(s.Owner);
            pkg.Write(s.Pos);
            pkg.Write(s.Radius);
            pkg.Write(s.Until);
            pkg.Write(s.Level);
            pkg.Write(s.Hp);
            pkg.Write(s.MaxHp);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnDome(long sender, ZPackage pkg)
        {
            try
            {
                State s = new State { Owner = pkg.ReadLong(), Pos = pkg.ReadVector3(), Radius = pkg.ReadSingle(), Until = pkg.ReadDouble(), Level = pkg.ReadInt() };
                s.Hp = pkg.ReadSingle(); s.MaxHp = pkg.ReadSingle();
                if (sender == Me && s.Owner == Me) return; // our own, already here
                State known;
                if (s.Until <= Now)
                {
                    if (Active.TryGetValue(s.Owner, out known)) End(known, true);
                    return;
                }
                if (Active.TryGetValue(s.Owner, out known) && Vector3.Distance(known.Pos, s.Pos) < 0.5f)
                {
                    known.Until = s.Until; // the same dome said again
                    if (s.Hp < known.Hp) { known.Hp = s.Hp; known.HitAt = Time.time; } // a blow taken elsewhere (it only goes down)
                    return;
                }
                Apply(s);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Dome message unreadable: " + e.Message); }
        }

        private static void Apply(State s)
        {
            State old;
            if (Active.TryGetValue(s.Owner, out old)) End(old, false);
            Active[s.Owner] = s;
            s.Look = DomeLook.Make(s);
        }

        private static void End(State s, bool burst)
        {
            State cur;
            if (Active.TryGetValue(s.Owner, out cur) && ReferenceEquals(cur, s)) Active.Remove(s.Owner);
            if (s.Look != null) s.Look.Close(burst);
            s.Look = null;
        }

        // from the skills' Update
        internal static void Tick()
        {
            if (Active.Count == 0) return;
            Wall(Time.deltaTime);
            double now = Now;
            if (Time.time >= _nextClean)
            {
                _nextClean = Time.time + 0.5f;
                List<State> over = null;
                foreach (State s in Active.Values) if (s.Until <= now) { if (over == null) over = new List<State>(); over.Add(s); }
                if (over != null) foreach (State s in over)
                {
                    End(s, false);
                    if (s.Owner == Me) RavenSkills.DomeSay("dome_end", Lang.T("Все, крила втомились. Тримайся.")); // its time ran out: Hugin comes back
                }
            }
            State mine;
            if (Time.time >= _nextResend && Active.TryGetValue(Me, out mine))
            {
                _nextResend = Time.time + ResendEvery;
                Send(mine);
            }
        }

        internal static void Reset()
        {
            foreach (State s in new List<State>(Active.Values)) End(s, false);
            Active.Clear();
        }

        internal static float LeftOf(State s) { return (float)(s.Until - Now); }

        // ---------------------------------------------------------------- inside

        internal static State At(Vector3 pos)
        {
            if (Active.Count == 0) return null;
            double now = Now;
            foreach (State s in Active.Values)
            {
                if (s.Until <= now) continue;
                Vector3 d = pos - s.Pos;
                if (Mathf.Abs(d.y) > s.Radius + 2f) continue;
                d.y = 0f;
                if (d.sqrMagnitude <= s.Radius * s.Radius) return s;
            }
            return null;
        }

        internal static bool Inside(Vector3 pos) { return At(pos) != null; }

        // within this many meters of a dome's edge (or inside it)
        internal static State Near(Vector3 pos, float margin)
        {
            if (Active.Count == 0) return null;
            double now = Now;
            foreach (State s in Active.Values)
            {
                if (s.Until <= now) continue;
                Vector3 d = pos - s.Pos;
                if (Mathf.Abs(d.y) > s.Radius + margin + 2f) continue;
                d.y = 0f;
                float r = s.Radius + margin;
                if (d.sqrMagnitude <= r * r) return s;
            }
            return null;
        }

        // ---------------------------------------------------------------- the wall
        // No monster stands inside, whatever brought it there (a lunge, a push, its own path): every game moves the
        // creatures it runs back out to the edge, smoothly, as if the dome pressed them out. Tamed ones, players'
        // own folk and bosses are left alone.
        private const float PushSpeed = 8f;

        private static void Wall(float dt)
        {
            if (Active.Count == 0) return;
            double now = Now;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.IsBoss()) continue;
                Character.Faction f = c.GetFaction();
                if (f == Character.Faction.Players || f == Character.Faction.PlayerSpawned) continue;
                ZNetView nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) continue; // only the game that moves it may move it
                foreach (State s in Active.Values)
                {
                    if (s.Until <= now) continue;
                    Vector3 d = c.transform.position - s.Pos;
                    if (Mathf.Abs(d.y) > s.Radius + 2f) continue;
                    d.y = 0f;
                    float edge = s.Radius + Body.Radius(c) * 0.5f;
                    float dist = d.magnitude;
                    if (dist >= edge) continue;
                    Vector3 dir = dist > 0.01f ? d / dist : c.transform.forward;
                    float step = Mathf.Min(edge - dist, PushSpeed * dt);
                    Rigidbody rb = c.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position += dir * step;
                        Vector3 v = rb.linearVelocity;
                        float inward = Vector3.Dot(v, -dir);
                        if (inward > 0f) rb.linearVelocity = v + dir * inward; // no more walking in
                    }
                    else c.transform.position += dir * step;
                }
            }
        }

        // ---------------------------------------------------------------- monsters at the edge

        // The game makes a monster whose target stands in a "no monsters" area run away (the traders' domes).
        // Here that question is answered "yes" for a target under the dome, and the running away is replaced
        // with prowling round the edge.
        [ThreadStatic] internal static MonsterAI Thinking;
        [ThreadStatic] internal static State ThinkingAbout;
        private static GameObject _areaHolder;
        private static EffectArea _area;

        // the stand-in area, put where the game should think it is (fleeing goes away from it)
        internal static EffectArea AreaAt(Vector3 pos)
        {
            EffectArea a = Area();
            if (_areaHolder != null) _areaHolder.transform.position = pos;
            return a;
        }

        internal static EffectArea Area()
        {
            if (_area != null) return _area;
            _areaHolder = new GameObject("RavenWatch_DomeArea");
            _areaHolder.SetActive(false); // never wakes: only stands for "yes, inside"
            UnityEngine.Object.DontDestroyOnLoad(_areaHolder);
            _area = _areaHolder.AddComponent<EffectArea>();
            return _area;
        }

        // the AI's own walking and looking (protected in the game), found once; a failure only turns the prowling off
        private static Func<BaseAI, float, Vector3, float, bool, bool> _moveTo;
        private static Action<BaseAI, Vector3> _lookAt;
        private static bool _looked;

        private static bool Hands()
        {
            if (_looked) return _moveTo != null;
            _looked = true;
            try
            {
                _moveTo = AccessTools.MethodDelegate<Func<BaseAI, float, Vector3, float, bool, bool>>(AccessTools.Method(typeof(BaseAI), "MoveTo", new Type[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) }));
                _lookAt = AccessTools.MethodDelegate<Action<BaseAI, Vector3>>(AccessTools.Method(typeof(BaseAI), "LookAt", new Type[] { typeof(Vector3) }));
            }
            catch (Exception e) { _moveTo = null; if (Plugin.Instance != null) Plugin.Instance.Log("Dome: monsters cannot be steered (" + e.Message + "), they will run off as from a trader's dome"); }
            return _moveTo != null;
        }

        private static void MoveTo(BaseAI ai, float dt, Vector3 point, float dist, bool run) { _moveTo(ai, dt, point, dist, run); }
        private static void LookAt(BaseAI ai, Vector3 point) { if (_lookAt != null) _lookAt(ai, point); }

        internal static bool CanProwl() { return Hands(); }

        private static readonly System.Reflection.FieldInfo SinceAttack = AccessTools.Field(typeof(MonsterAI), "m_timeSinceAttacking");

        internal static void Prowl(MonsterAI ai, State s, float dt)
        {
            // the game drops a target it could not strike for a minute; these see you and wait, they do not give up
            if (SinceAttack != null) SinceAttack.SetValue(ai, 0f);
            Transform t = ai.transform;
            Vector3 d = t.position - s.Pos;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = t.forward;
            float dist = d.magnitude;
            Vector3 dir = d / Mathf.Max(0.001f, dist);
            Character body = ai.GetComponent<Character>();
            float ring = s.Radius + 1.2f + Body.Radius(body);
            int id = ai.GetInstanceID();
            if (dist < ring - 0.8f)
            {
                MoveTo(ai, dt, s.Pos + dir * (ring + 0.6f), 0.4f, true); // inside or too close: out to the edge first
                return;
            }
            // now pacing along the edge, now standing and staring in: each monster in its own rhythm and direction
            float phase = Time.time * 0.11f + (id & 1023) * 0.0137f;
            Character target = TargetOf(ai);
            if (Mathf.Sin(phase * Mathf.PI * 2f) > 0.45f)
            {
                ai.StopMoving();
                if (target != null) LookAt(ai, target.transform.position);
                return;
            }
            float side = (id & 1) == 0 ? 1f : -1f;
            Vector3 next = Quaternion.AngleAxis(side * 30f, Vector3.up) * dir;
            MoveTo(ai, dt, s.Pos + next * ring, 0.5f, false);
        }

        private static readonly System.Reflection.FieldInfo TargetField = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static Character TargetOf(MonsterAI ai) { try { return TargetField != null ? TargetField.GetValue(ai) as Character : null; } catch { return null; } }

        // ---------------------------------------------------------------- no blows

        // true: this hit is refused. A boss's blow on someone under the dome is taken by the dome instead, until its
        // strength runs out: the blow that breaks it lands with what was left over, and the dome bursts.
        internal static bool Refuses(Character victim, HitData hit)
        {
            if (Active.Count == 0 || victim == null || hit == null) return false;
            Character attacker = hit.GetAttacker();
            State underVictim = At(victim.transform.position);
            if (attacker != null && attacker.IsBoss())
            {
                if (underVictim == null) return false;
                if (hit.GetTotalDamage() <= 0f) return true;
                if (underVictim.Hp < 0.5f) return false; // already broken here (its burst is on the way): the blow lands
                underVictim.Hp -= 1f; // here at once: the next blows before the owner answers count too
                underVictim.HitAt = Time.time;
                if (underVictim.Owner == Me) Struck(underVictim);
                else if (ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(underVictim.Owner, HitRpc, 1f);
                return true; // the dome took it
            }
            if (underVictim != null) return true;
            if (attacker != null && At(attacker.transform.position) != null) return true;
            TurnOn(victim, attacker);
            return false;
        }

        // a boss's blow taken under someone else's dome: its owner keeps the count and tells everyone
        private static void OnHit(long sender, float took)
        {
            State s;
            if (sender == Me || !Active.TryGetValue(Me, out s) || took <= 0f) return;
            s.Hp -= took;
            s.HitAt = Time.time;
            Struck(s);
        }

        // on the owner's game: broken, or everyone told how much is left
        private static void Struck(State s)
        {
            if (s.Hp <= 0.5f)
            {
                Burst(s);
                RavenSkills.DomeSay("dome_broken", Lang.T("Не втримав... Крило не камінь."));
                return;
            }
            Send(s);
            _nextResend = Time.time + ResendEvery;
        }

        // One player waits under the dome as bait while another, outside, cuts down the monsters pacing at its edge,
        // which never strike back (they wait for the one inside): a monster struck from outside turns on whoever
        // struck it, as it would anywhere else.
        private static void TurnOn(Character victim, Character attacker)
        {
            if (!(attacker is Player) || TargetField == null) return;
            MonsterAI ai = victim.GetComponent<MonsterAI>();
            if (ai == null) return;
            Character target = TargetOf(ai);
            if (target == null || target == attacker || At(target.transform.position) == null) return;
            TargetField.SetValue(ai, attacker);
        }

        // ---------------------------------------------------------------- a roof

        // The dome is a roof, not a stove: rain and wind stay out, the cold does not. The game counts it as shelter,
        // so a fire lit inside warms you as it does in a house (and sitting by it lets you rest, as by any fire).
        [ThreadStatic] internal static bool Sheltering; // inside the game's weather check for the local player under a dome

        internal static bool Roofed(Player p) { return p != null && p == Player.m_localPlayer && Active.Count > 0 && Inside(p.transform.position); }
    }

    // ---------------------------------------------------------------- patches

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class DomeThinkPatch
    {
        private static void Prefix(MonsterAI __instance) { try { Dome.Thinking = __instance; Dome.ThinkingAbout = null; } catch (System.Exception e) { PatchGuard.Fail("DomeThinkPatch", e); } }
        private static void Postfix() { try { Dome.Thinking = null; Dome.ThinkingAbout = null; } catch (System.Exception e) { PatchGuard.Fail("DomeThinkPatch", e); } }
    }

    [HarmonyPatch(typeof(EffectArea), "IsPointInsideNoMonsterArea")]
    internal static class DomeAreaPatch
    {
        private static void Postfix(Vector3 p, ref EffectArea __result)
        {
            MonsterAI ai = Dome.Thinking;
            if (__result != null || ai == null) return;
            Dome.State s = Dome.At(p);
            if (s == null) return;
            Character body = ai.GetComponent<Character>();
            if (body == null || body.IsBoss()) return; // bosses do not care
            Dome.ThinkingAbout = s;
            __result = Dome.Area();
        }
    }

    // a monster with no target near a dome (one that has not seen you, or one breaking things) keeps away from it:
    // the game's own fleeing from a "no monsters" area, away from the dome's centre
    [HarmonyPatch(typeof(EffectArea), "IsPointCloseToNoMonsterArea")]
    internal static class DomeNearPatch
    {
        private static void Postfix(Vector3 p, ref EffectArea __result)
        {
            MonsterAI ai = Dome.Thinking;
            if (__result != null || ai == null) return;
            Dome.State s = Dome.Near(p, 4f);
            if (s == null) return;
            Character body = ai.GetComponent<Character>();
            if (body == null || body.IsBoss()) return;
            __result = Dome.AreaAt(s.Pos);
        }
    }

    [HarmonyPatch(typeof(BaseAI), "Flee")]
    internal static class DomeFleePatch
    {
        private static bool Prefix(BaseAI __instance, float dt, ref bool __result)
        {
            Dome.State s = Dome.ThinkingAbout;
            MonsterAI ai = __instance as MonsterAI;
            if (s == null || ai == null || !ReferenceEquals(ai, Dome.Thinking) || !Dome.CanProwl()) return true;
            try { Dome.Prowl(ai, s, dt); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Dome prowl failed: " + e.Message); return true; }
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class DomeDamagePatch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            try { return !Dome.Refuses(__instance, hit); }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
    internal static class DomeShelterPatch
    {
        private static void Prefix(Player __instance)
        {
            Dome.Sheltering = false;
            try { Dome.Sheltering = __instance == Player.m_localPlayer && Dome.Inside(__instance.transform.position); } catch { }
        }

        private static void Postfix() { Dome.Sheltering = false; }
    }

    // under the dome the game finds a roof: no rain, and a fire inside counts as a fire in a house
    [HarmonyPatch(typeof(Player), "InShelter")]
    internal static class DomeRoofPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result) return;
            try { if (Dome.Roofed(__instance)) __result = true; } catch { }
        }
    }

    [HarmonyPatch(typeof(EnvMan), "IsWet")]
    internal static class DomeWetPatch { private static bool Prefix(ref bool __result) { if (!Dome.Sheltering) return true; __result = false; return false; } }

    // a fire looks for its own roof (rays up), not the player's: under the dome rain and storm wind do not reach it
    [HarmonyPatch(typeof(Fireplace), "CheckWet")]
    internal static class DomeFirePatch
    {
        private static void Postfix(Fireplace __instance, ref bool ___m_wet)
        {
            if (!___m_wet) return;
            try { if (Dome.Inside(__instance.transform.position)) ___m_wet = false; } catch { }
        }
    }

    // ---------------------------------------------------------------- the look

    // A faint blue sphere, runes rising from the ring on the ground and drifting inside, a soft light, Hugin gliding
    // round above. Drawn by every game for itself; nothing of it is a networked object.
    internal class DomeLook : MonoBehaviour
    {
        internal static Color Blue = new Color(0.35f, 0.62f, 1f);
        internal static float ShellOpacity = 0.35f; // the shell texture is mostly clear; this scales what shows
        private static Material _shell, _runes;
        private static Texture2D _runeTex;

        private Dome.State _s;
        private Renderer _shellRenderer;
        private Light _light;
        private ParticleSystem _ring, _air;
        private GameObject _raven;
        private float _born, _closing = -1f;
        private const float GrowSeconds = 0.6f, FadeSeconds = 1.2f;

        internal static DomeLook Make(Dome.State s)
        {
            try
            {
                GameObject root = new GameObject("RavenWatch_Dome");
                root.transform.position = s.Pos;
                DomeLook look = root.AddComponent<DomeLook>();
                look._s = s;
                look._born = Time.time;
                look.Build();
                RavenFx.Play("vfx_offering", s.Pos, Quaternion.identity);
                RavenFx.Play("sfx_offering", s.Pos, Quaternion.identity);
                return look;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Dome look failed: " + e.Message); return null; }
        }

        private void Build()
        {
            float r = _s.Radius;
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider c = shell.GetComponent<Collider>();
            if (c != null) Destroy(c);
            shell.name = "shell";
            shell.transform.SetParent(transform, false);
            shell.transform.localScale = Vector3.one * 0.01f;
            _shellRenderer = shell.GetComponent<Renderer>();
            _shellRenderer.sharedMaterial = Shell();
            _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shellRenderer.receiveShadows = false;

            GameObject lightGo = new GameObject("light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 2f;
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Blue;
            _light.range = r * 1.6f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;

            _ring = Runes("ring", r, true);
            _air = Runes("air", r, false);
            _raven = DomeRaven.Make(transform, _s);
        }

        // runes: from the ring on the ground they rise slowly; inside they float about and turn
        private ParticleSystem Runes(string name, float r, bool ring)
        {
            Material m = RuneMaterial();
            if (m == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // the circle shape lies flat
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = ring ? new ParticleSystem.MinMaxCurve(3f, 4.5f) : new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = ring ? new ParticleSystem.MinMaxCurve(0.05f, 0.2f) : new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = ring ? new ParticleSystem.MinMaxCurve(0.35f, 0.55f) : new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(Blue, Color.Lerp(Blue, Color.white, 0.45f));
            main.maxParticles = ring ? 40 : 24;
            main.gravityModifier = ring ? -0.03f : -0.01f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = ring ? 9f : 4f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            if (ring) { sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = r; sh.radiusThickness = 0.05f; }
            else { sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = r * 0.85f; sh.radiusThickness = 1f; }
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
            sheet.cycleCount = 1;
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private void Update()
        {
            float r = _s.Radius;
            float grow = Mathf.Clamp01((Time.time - _born) / GrowSeconds);
            float shown = 1f - (1f - grow) * (1f - grow);
            float fade = 1f;
            if (_closing >= 0f) fade = 1f - Mathf.Clamp01((Time.time - _closing) / FadeSeconds);
            else
            {
                float left = Dome.LeftOf(_s);
                // the last seconds: it flickers, so everyone sees it is about to go
                if (left < 5f) fade = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * (6f + (5f - left) * 2f)));
                // worn by a boss's blows: paler as it weakens, a flash at each blow
                if (_s.MaxHp > 0f) fade *= 0.45f + 0.55f * Mathf.Clamp01(_s.Hp / _s.MaxHp);
                float since = Time.time - _s.HitAt;
                if (since < 0.35f) fade += 1.2f * (1f - since / 0.35f);
            }
            if (_shellRenderer != null)
            {
                _shellRenderer.transform.localScale = Vector3.one * (2f * r * shown);
                Material m = _shellRenderer.sharedMaterial;
                if (m != null) m.mainTextureOffset = new Vector2(Time.time * 0.01f, Time.time * 0.025f);
                _shellRenderer.enabled = fade > 0.02f;
                MaterialPropertyBlock mpb = Block;
                mpb.Clear();
                mpb.SetColor("_Color", new Color(Blue.r, Blue.g, Blue.b, ShellOpacity * fade));
                _shellRenderer.SetPropertyBlock(mpb);
            }
            if (_light != null) _light.intensity = 0.9f * shown * fade;
            if (_closing >= 0f && Time.time - _closing > FadeSeconds + 6f) Destroy(gameObject);
        }

        private static MaterialPropertyBlock _block;
        private static MaterialPropertyBlock Block { get { if (_block == null) _block = new MaterialPropertyBlock(); return _block; } }

        // time is up (or it burst): no new runes, everything fades; the runes in the air finish their flight
        internal void Close(bool burst)
        {
            if (_closing >= 0f || this == null) return;
            _closing = Time.time;
            if (_ring != null) _ring.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_air != null) _air.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            bool standIn = _raven != null && _raven.transform.parent == transform;
            if (standIn)
            {
                Raven template = Shoulder.Template(false);
                if (template != null && template.m_despawnEffect != null) RavenFx.Own(template.m_despawnEffect, _raven.transform.position, Quaternion.identity, null, 1f, -1);
            }
            DomeRaven.Release(_raven);
            _raven = null;
            if (burst) RavenFx.Play("vfx_offering", _s.Pos + Vector3.up, Quaternion.identity);
            RavenFx.Play("sfx_raven_flap", _s.Pos, Quaternion.identity);
        }

        // ---------------------------------------------------------------- materials

        // see-through, coloured by _Color and the vertex colour (particles); the game has no legacy particle shaders
        private static Shader _shader;
        private static Shader Additive()
        {
            if (_shader != null) return _shader;
            _shader = Shader.Find("Sprites/Default");
            if (_shader == null) _shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (Plugin.Instance != null) Plugin.Instance.Log("Dome shader: " + (_shader != null ? _shader.name : "none"));
            return _shader;
        }

        // the shell: a faint glow, brighter near the ground, with slow drifting streaks
        private static Material Shell()
        {
            if (_shell != null) return _shell;
            const int w = 128, h = 64;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            System.Random rnd = new System.Random(7);
            float[] streak = new float[w];
            for (int x = 0; x < w; x++) streak[x] = (float)rnd.NextDouble();
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);                       // 0 at the bottom of the sphere, 1 at the top
                float rim = Mathf.Pow(1f - Mathf.Abs(v - 0.5f) * 2f, 6f); // bright along the ground line
                float top = Mathf.Lerp(0.35f, 0.15f, v);
                for (int x = 0; x < w; x++)
                {
                    float s = Mathf.Clamp01(streak[x] * 2.2f - 1.6f) * (0.5f + 0.5f * Mathf.Sin(v * 40f + x * 0.3f));
                    float a = Mathf.Clamp01(top * 0.5f + rim * 0.9f + s * 0.35f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a)); // white, the pattern in the alpha
                }
            }
            t.Apply(true, true);
            t.hideFlags = HideFlags.HideAndDontSave; // as its material: kept through the game's asset cleanup
            _shell = new Material(Additive());
            _shell.name = "RavenWatch dome shell";
            _shell.mainTexture = t;
            _shell.hideFlags = HideFlags.HideAndDontSave;
            _shell.color = new Color(Blue.r, Blue.g, Blue.b, ShellOpacity);
            _shell.renderQueue = 3100;
            return _shell;
        }

        internal static Material RuneMaterialPublic() { return RuneMaterial(); }

        // the runes: a 4×4 sheet of Elder Futhark (white, glowing), coloured in the particles
        private static Material RuneMaterial()
        {
            if (_runes != null) return _runes;
            if (_runeTex == null) _runeTex = LoadRunes();
            if (_runeTex == null) return null;
            _runes = new Material(Additive());
            _runes.name = "RavenWatch dome runes";
            _runes.mainTexture = _runeTex;
            _runes.hideFlags = HideFlags.HideAndDontSave;
            return _runes;
        }

        private static Texture2D LoadRunes()
        {
            try
            {
                using (Stream st = typeof(DomeLook).Assembly.GetManifestResourceStream("RavenWatch.runes.png"))
                {
                    if (st == null) return null;
                    byte[] data = new byte[st.Length];
                    int read = 0;
                    while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; }
                    Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (!AbilityIcons.Decode(t, data)) return null;
                    t.wrapMode = TextureWrapMode.Clamp;
                    t.hideFlags = HideFlags.HideAndDontSave;
                    return t;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Dome runes failed: " + e.Message); return null; }
        }
    }

    // Hugin himself round the dome: the very raven from the owner's shoulder (its size, its hat) lets go, flies up
    // to the circle, goes round while the dome stands, and flies back down to the shoulder when it falls. A game
    // that does not draw that shoulder (too far) flies a stand-in raven instead.
    internal class DomeRaven : MonoBehaviour, IRavenUser
    {
        private bool _yielded; // a stronger act (a strike) took the bird: let go without the landing

        public void Yield() { _yielded = true; DestroyImmediate(this); }

        private Dome.State _s;
        private ShoulderLife _life;
        private ShoulderPin _pin;
        private bool _borrowed;     // the shoulder's own raven: given back at the end, never destroyed
        private Unhooked _hook;     // let go of the body's bone while it flies
        private float _angle;
        private int _phase;         // 0 up from the shoulder to the circle, 1 round, 2 back down home
        private float _t0;
        private RavenMotor _motor;  // the raven's motor (keeps its speed when it was taken in the air)
        private FlightBody _body;   // the motor's body for this flight (RavenFlight)

        internal static GameObject Make(Transform parent, Dome.State s)
        {
            Player owner = OwnerPlayer(s);
            GameObject model = owner != null ? Shoulder.ModelOf(owner, false) : null;
            if (model != null && AnimDirector.CanTake(model, RavenAct.Dome))
            {
                DomeRaven old = model.GetComponent<DomeRaven>();
                if (old != null) DestroyImmediate(old);
                DomeRaven dr = model.AddComponent<DomeRaven>();
                dr._s = s;
                dr._borrowed = true;
                dr._pin = model.GetComponent<ShoulderPin>();
                dr._life = model.GetComponent<ShoulderLife>();
                dr._motor = RavenMotor.Of(model, false, owner);
                AnimDirector.Take(model, RavenAct.Dome, dr, "dome");
                if (dr._pin != null) dr._pin.enabled = false; // lets go of the shoulder
                dr._hook = Unhooked.From(model.transform);
                Vector3 d = model.transform.position - s.Pos;
                dr._angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; // enters the circle on its own side
                dr.Go(0);
                dr._motor.Launch(Vector3.up * 3.5f + new Vector3(d.x, 0f, d.z).normalized * 1.5f, "купол", 1f);
                dr._body = dr._motor.Body;
                RavenFx.Play("sfx_raven_flap", model.transform.position, Quaternion.identity);
                return model;
            }
            return StandIn(parent, s, owner);
        }

        private static GameObject StandIn(Transform parent, Dome.State s, Player owner)
        {
            Raven source = Shoulder.Template(false);
            if (source == null || source.m_visual == null) { if (Plugin.Instance != null) Plugin.Instance.Log("Dome: no Hugin model to circle it"); return null; }
            GameObject go = Instantiate(source.m_visual);
            go.name = "RavenWatch_DomeHugin";
            go.SetActive(true);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Shared.RemoveExclamation(source, go);
            go.transform.localScale = source.m_visual.transform.lossyScale * Shoulder.SizeFor(Mathf.Max(1, s.Level)) * Shoulder.Scale;
            go.transform.SetParent(parent, true);
            Shoulder.Perch(go.GetComponentInChildren<Animator>());
            DomeRaven dr = go.AddComponent<DomeRaven>();
            dr._s = s;
            dr._angle = UnityEngine.Random.Range(0f, 360f);
            dr._life = go.AddComponent<ShoulderLife>();
            dr._life.Init(owner != null ? owner : Player.m_localPlayer, false, Mathf.Max(1, s.Level));
            RavenWear.Dress(go, false, RavenWear.OnPlayer(owner != null ? owner : Player.m_localPlayer, false));
            AnimDirector.Take(go, RavenAct.Dome, dr, "dome, a stand-in");
            dr._phase = 1;
            dr.Place(true);
            dr._motor = RavenMotor.Of(go, false, owner);
            dr._motor.Launch(Quaternion.AngleAxis(dr._angle, Vector3.up) * Vector3.right * 7f, "купол", 1f);
            dr._body = dr._motor.Body;
            return go;
        }

        private static Player OwnerPlayer(Dome.State s)
        {
            if (s.Owner == ZDOMan.GetSessionID()) return Player.m_localPlayer;
            foreach (Player p in Player.GetAllPlayers())
                if (p != null && p.GetZDOID().UserID == s.Owner) return p;
            return null;
        }

        private void Go(int phase) { _phase = phase; _t0 = Time.time; }

        // the dome is gone: the shoulder's raven flies home, a stand-in just goes
        internal static void Release(GameObject g)
        {
            if (g == null) return;
            DomeRaven dr = g.GetComponent<DomeRaven>();
            // a shoulder raven is never destroyed here (a strike may have taken it from the dome meanwhile)
            if (dr == null) { if (g.GetComponent<ShoulderPin>() == null) Destroy(g, 0.1f); return; }
            if (!dr._borrowed) { Destroy(g, 0.1f); return; }
            dr.Go(2);
            if (dr._life != null) dr._life.Flap(1f);
        }

        private Vector3 Home()
        {
            if (_pin != null && _pin.Bone != null) return _pin.Bone.position + Vector3.up * 0.1f;
            Player owner = OwnerPlayer(_s);
            return owner != null ? owner.GetEyePoint() : transform.position;
        }

        // from LateUpdate, after the game's animation, right before the wings are sampled
        private void Step()
        {
            if (_borrowed && (_pin == null || _pin.Player == null || _pin.Player.IsDead())) { GiveBack(); return; }
            if (_body != null) StepNew();
        }

        // The new flight: it chases a point a little ahead of itself on the circle round the dome, so it swings round
        // by itself, banked, beating and gliding; the circle's own waves give it climbs and dives. Home: it brakes
        // with the wings, legs out, and lands.
        private void StepNew()
        {
            float dt = Time.deltaTime;
            if (_phase == 2)
            {
                // home: the motor's way home, the same for every flight
                Vector3 seatAt = Home();
                Player owner = _pin != null ? _pin.Player : null;
                Vector3 carry = owner != null ? owner.GetVelocity() : Vector3.zero;
                if (_motor.Home(seatAt, owner, _body.HomeSpeed(10f, carry, Time.time - _t0), dt, false, 0f, false, Vector3.zero) || _motor.Stuck || Time.time - _t0 > 40f)
                {
                    if (owner != null && ShoulderLife.LogLandings && ReferenceEquals(owner, Player.m_localPlayer) && Plugin.Instance != null) Plugin.Instance.Log("Landing (dome): facing " + Mathf.RoundToInt(_body.YawOff(owner.transform.forward)) + "° off the owner's way, " + Mathf.RoundToInt(Vector3.Distance(transform.position, seatAt) * 100f) + " cm from the seat, speed " + _body.Speed.ToString("0.0"));
                    GiveBack();
                }
                return;
            }
            Vector3 rel = transform.position - _s.Pos;
            float at = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
            _angle = at;
            float r = _s.Radius + 2f;
            _motor.Steer(Circle(at + 35f), _body.Style.Cruise * 0.75f, false, 0f, dt, Vector3.zero, 1f);
            if (_phase == 0 && Mathf.Abs(new Vector2(rel.x, rel.z).magnitude - r) < 1.5f) _phase = 1;
            _motor.Finish(dt, 0f);
        }

        private void GiveBack()
        {
            AnimDirector.Release(gameObject, this);
            if (_borrowed && _pin != null)
            {
                Vector3 from = transform.position;
                if (_hook != null) { _hook.Back(); _hook = null; }
                _pin.enabled = true;
                _pin.ComeBack(from, transform.rotation); // the game's own landing, onto the shoulder (from far: flown in again)
            }
            if (_borrowed) Destroy(this); else Destroy(gameObject);
        }

        private Vector3 Circle(float angle)
        {
            float r = _s.Radius + 2f; // round the outside of the dome, low enough to be seen from in it
            // up and down as it goes round: two long waves a circle, and a small bob of the wing beats
            float rad = angle * Mathf.Deg2Rad;
            float height = 4.6f + Mathf.Sin(rad * 2f) * 1.1f + Mathf.Sin(Time.time * 2.3f) * 0.15f;
            return _s.Pos + Vector3.up * height + Quaternion.AngleAxis(angle, Vector3.up) * (Vector3.forward * r);
        }

        private void Place(bool first)
        {
            Vector3 pos = Circle(_angle);
            Vector3 step = pos - transform.position;
            transform.position = pos;
            if (first || step.sqrMagnitude < 0.000001f) step = Quaternion.AngleAxis(_angle, Vector3.up) * Vector3.right;
            Face(step);
        }

        // facing where it flies (climbing and diving too), with a slight bank into the turn
        private void Face(Vector3 step)
        {
            if (step.sqrMagnitude < 0.000001f) return;
            if (_phase == 1) step.y *= 0.35f; // rides the waves up and down, nose only a little with them
            transform.rotation = Quaternion.LookRotation(step.normalized, Vector3.up) * Quaternion.AngleAxis(_phase == 1 ? -12f : 0f, Vector3.forward) * Quaternion.Euler(Shoulder.Rotation);
        }

        private void LateUpdate()
        {
            Step();
            if (this != null && _life != null && (_pin == null || !_pin.enabled)) _life.Apply();
        }

        private void OnDestroy()
        {
            if (_hook != null) { _hook.Back(); _hook = null; }
            if (_borrowed && _pin != null && !_pin.enabled) _pin.enabled = true;
            if (!_yielded) AnimDirector.Release(gameObject, this);
        }
    }
}
#endif

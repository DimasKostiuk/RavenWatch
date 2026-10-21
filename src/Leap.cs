#if RAVEN_ITEM
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's leap: for a while every jump from the ground throws you high up, and falls do not hurt.
    // When the spell runs out in the air, the landing hurts as usual (the game's own fall damage).
    internal static class Leap
    {
        internal const string EffectName = "RavenWatch_Leap";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        internal static float Seconds = 7.5f, SecondsTop = 30f; // how long the spell holds: at trust level 1 and at the top level
        internal static float Duration() { return Mathf.Floor(Trust.Lerp(false, Seconds, SecondsTop) * GiftUp.Power("leap") + 0.5f); }
        internal static float Height = 12.5f;
        internal static float HeightNow() { return Height * Trust.Mul(false, 0.05f); } // as high and as far as it throws you now (trust)
        internal static float ForwardNow() { return Forward * Trust.Mul(false, 0.05f); }
        internal static float Forward = 30f; // meters ahead when jumping on the run
        private const float TurnDegrees = 60f; // steering in the air, per second

        private static readonly FieldInfo Body = AccessTools.Field(typeof(Character), "m_body");
        private static readonly FieldInfo MoveDir = AccessTools.Field(typeof(Character), "m_moveDir");
        internal static bool FromGround; // the jump being made started on the ground
        private static SE_Stats _template;

        // the jump speed is worked out from gravity, then corrected by the heights really reached
        // (air drag and the game's own movement code take a little off)
        private static float _correction = 1f;
        private static bool _measuring;
        private static float _startY, _topY;

        // the flight on the run: the game lets the speed in the air fade, so it is held here till the landing
        private static bool _flying;
        private static Vector3 _dir;
        private static float _hSpeed, _flightStart;

        internal static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Злет Хугіна");
            se.m_tooltip = Lang.F("Кожен стрибок із землі — на {0} м угору. Падіння не шкодить, поки діє закляття", Mathf.RoundToInt(HeightNow()));
            se.m_icon = AbilityIcons.ForEffect("leap", false);
            se.m_ttl = Mathf.Max(1f, Duration());
            se.m_fallDamageModifier = -1f;
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        internal static void Reset() { _flying = false; _measuring = false; FromGround = false; }

        // the local player is up in a high jump: the shoulder ravens spread their wings
        private static float _jumpedAt = -100f;
        internal static bool Gliding(Player p) { return p != null && Time.time - _jumpedAt < 30f && Time.time - _jumpedAt > 0.15f && !p.IsOnGround() && On(p); }

        internal static bool On(Character c)
        {
            return c != null && c == Player.m_localPlayer && c.GetSEMan().HaveStatusEffect(Hash);
        }

        internal static float Speed()
        {
            float g = Mathf.Max(1f, -Physics.gravity.y);
            return Mathf.Sqrt(2f * g * Mathf.Max(1f, Height * Trust.Mul(false, 0.05f))) * _correction;
        }

        // up, and on the run also forward: a parabola that lands about Forward meters ahead
        internal static void Shape(ref Vector3 vel)
        {
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            vel.y = Speed();
            if (flat.magnitude < 1.5f || Forward <= 0f) { vel.x = 0f; vel.z = 0f; return; } // standing: straight up
            float g = Mathf.Max(1f, -Physics.gravity.y);
            float grow = Trust.Mul(false, 0.05f);
            float flight = 2f * Mathf.Sqrt(2f * Mathf.Max(1f, Height * grow) / g);
            Vector3 h = flat.normalized * (Forward * grow / flight);
            vel.x = h.x; vel.z = h.z;
        }

        internal static void Begin(Player p, Vector3 vel)
        {
            _measuring = true;
            _startY = _topY = p.transform.position.y;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            _flying = flat.magnitude > 0.1f;
            _dir = _flying ? flat.normalized : Vector3.zero;
            _hSpeed = flat.magnitude;
            _flightStart = Time.time;
            _jumpedAt = Time.time;
        }

        // every physics step of the local player: keep the forward speed, steer gently with the move keys
        internal static void Hold(Character c)
        {
            if (!_flying || c != Player.m_localPlayer) return;
            bool landed = Time.time - _flightStart > 0.3f && c.IsOnGround();
            if (landed || c.IsSwimming() || c.IsDead() || Time.time - _flightStart > 30f) { _flying = false; return; }
            Rigidbody rb = Body != null ? Body.GetValue(c) as Rigidbody : null;
            if (rb == null) { _flying = false; return; }
            object md = MoveDir != null ? MoveDir.GetValue(c) : null;
            Vector3 want = md is Vector3 ? (Vector3)md : Vector3.zero;
            want.y = 0f;
            if (want.sqrMagnitude > 0.01f)
                _dir = Vector3.RotateTowards(_dir, want.normalized, TurnDegrees * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            Vector3 v = rb.linearVelocity;
            Vector3 h = _dir * _hSpeed;
            rb.linearVelocity = new Vector3(h.x, v.y, h.z);
        }

        private static void Feathers(Player p) { if (p != null) LeapFeathers.Update(p, On(p)); }

        // every frame: the top of the flight, to correct the next one
        private static float _airSince = -1f, _landedAt = -10f;

        // under the spell, back on the ground after a real time in the air (not a step off a stair): the landing's feathers
        private static void Landing(Player p)
        {
            if (p == null || !On(p) || p.IsDead()) { _airSince = -1f; return; }
            if (!p.IsOnGround()) { if (_airSince < 0f) _airSince = Time.time; return; }
            if (_airSince >= 0f && Time.time - _airSince > 0.45f && Time.time - _landedAt > 0.6f) { _landedAt = Time.time; LeapFx.Land(p); }
            _airSince = -1f;
        }

        internal static void Tick(Player p)
        {
            Feathers(p);
            Landing(p);
            if (!_measuring || p == null) return;
            float y = p.transform.position.y;
            if (y > _topY) { _topY = y; return; }
            if (y > _topY - 0.5f) return; // still near the top
            _measuring = false;
            float reached = _topY - _startY;
            if (reached < 5f || p.IsOnGround()) return; // hit a ceiling or a branch: tells nothing
            _correction = Mathf.Clamp(_correction * Mathf.Sqrt(Height * Trust.Mul(false, 0.05f) / reached), 0.5f, 3f);
            if (Plugin.Instance != null) Plugin.Instance.Log("Hugin's leap reached " + reached.ToString("0.0") + " m of " + Height.ToString("0") + "; speed correction now " + _correction.ToString("0.00"));
        }
    }

    // ---------------------------------------------------------------- the game's own raven effects: feathers on use, a wing flap on every high jump
    internal static class LeapFx
    {
        private const string RpcFeathers = "RavenWatch_LeapFeathers", RpcFlap = "RavenWatch_LeapFlap", RpcLand = "RavenWatch_LeapLand";

        internal static void Register()
        {
            RavenFx.Register(RpcFeathers, SpawnFeathers);
            RavenFx.Register(RpcFlap, SpawnFlap);
            RavenFx.Register(RpcLand, SpawnLand);
        }

        internal static void Feathers(Player p) { RavenFx.Broadcast(RpcFeathers, p, SpawnFeathers); }
        internal static void Flap(Player p) { RavenFx.Broadcast(RpcFlap, p, SpawnFlap); }
        internal static void Land(Player p) { RavenFx.Broadcast(RpcLand, p, SpawnLand); }

        // our ring of feathers bursting out under your feet, with the sound of a raven's feather cloud
        private static void SpawnFeathers(Player p)
        {
            try
            {
                HuginWhispers.Feathers.VanishSound(p.transform.position + Vector3.up * 1.2f);
                HuginWhispers.Feathers.Ring(p.transform.position, HuginWhispers.Feathers.RingCount, HuginWhispers.Feathers.RingRadius);
                HuginWhispers.Feathers.Dust(p.transform.position, 14, 2.5f);
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Leap feathers failed: " + e.Message); }
        }

        // a landing under the spell: a smaller ring and a little smoke from under the feet
        private static void SpawnLand(Player p)
        {
            try
            {
                HuginWhispers.Feathers.Ring(p.transform.position, HuginWhispers.Feathers.LandCount, HuginWhispers.Feathers.LandRadius);
                HuginWhispers.Feathers.Dust(p.transform.position, 8, 1.6f);
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Leap landing failed: " + e.Message); }
        }

        // the raven's wing flap (a sound the game keeps inside the raven, found by name)
        private static void SpawnFlap(Player p) { RavenFx.Play("sfx_raven_flap", p.transform.position + Vector3.up * 1.2f, p.transform.rotation); }
    }

    // ---------------------------------------------------------------- the look: feathers off your feet while the spell holds
    internal static class LeapFeathers
    {
        private static ParticleSystem _fx;
        private static Material _mat;

        internal static void Update(Player p, bool on)
        {
            if (on && _fx == null) _fx = HuginWhispers.Feathers.FeetTrail(p);
            else if (!on && _fx != null) { HuginWhispers.Feathers.EndTrail(_fx); _fx = null; } // the last feathers settle where they are
        }

        // a soft particle look borrowed from the game (the Wisplight's), or a plain one
        internal static Material Material() { return Mat(); }

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            GameObject wisp = RavenLight.Wisp();
            if (wisp != null)
                foreach (ParticleSystemRenderer r in wisp.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    if (r.sharedMaterial != null) { _mat = r.sharedMaterial; return _mat; }
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) _mat = new Material(sh);
            return _mat;
        }
    }

    // after the game's own movement each physics step, so the held speed wins over its air control
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    internal static class LeapFlightPatch
    {
        private static void Postfix(Character __instance) { try { Leap.Hold(__instance); } catch (System.Exception e) { PatchGuard.Fail("LeapFlightPatch", e); } }
    }

    [HarmonyPatch(typeof(Character), "Jump")]
    internal static class LeapJumpPatch
    {
        private static void Prefix(Character __instance) { try { Leap.FromGround = Leap.On(__instance) && __instance.IsOnGround(); } catch (System.Exception e) { PatchGuard.Fail("LeapJumpPatch", e); } }
        private static void Postfix() { try { Leap.FromGround = false; } catch (System.Exception e) { PatchGuard.Fail("LeapJumpPatch", e); } }
    }

    // the game's jump goes through here with its own speed: a jump from the ground under the spell goes high
    [HarmonyPatch(typeof(Character), "ForceJump")]
    internal static class LeapForceJumpPatch
    {
        private static void Prefix(Character __instance, ref Vector3 vel)
        { try {
            if (!Leap.FromGround || __instance != Player.m_localPlayer) return;
            Leap.FromGround = false;
            Leap.Shape(ref vel);
            Leap.Begin((Player)__instance, vel);
            LeapFx.Flap((Player)__instance);
        } catch (System.Exception e) { PatchGuard.Fail("LeapForceJumpPatch", e); } }
    }
}
#endif

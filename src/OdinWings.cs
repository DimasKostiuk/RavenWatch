#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Крила Одіна», Hugin's great gift: you fly. A storm of feathers, Hugin goes up first (Munin a breath after), and
    // you are thrown up after him; then you fly where the camera looks (W, A/D aside), Space up, crouch down, always
    // at the same pace but with a pull on starting and a drift on stopping. Without a key you hang in the air with
    // slow beats. Both ravens fly at your back, Hugin leaving a trail of feathers. Nothing to strike with up there.
    // At most MaxHeight over the ground. While the gift lasts you may come down (crouch near the ground) and walk, and
    // Space on the ground leaps you up again instead of the game's jump. Time up, or the gift pressed again: you
    // float down like on a cloak of levitation (faster high up, slow near the ground), no fall hurts, and land in a
    // ring of feathers; on the ground it just ends.
    // The motion is the owner's (Character.UpdateMotion of the local player, like the game's debug fly but our own);
    // the look is every game's: the player's ZDO flag and the "odinwings" tag drive the clips (PlayerAnims rules,
    // built in below) and the ravens' trail; the bursts go to everyone as an effect message.
    internal static class OdinWings
    {
        internal static float Seconds = 60f, SecondsTop = 75f;
        internal static float Speed = 13f, ClimbSpeed = 7f, SinkSlow = 1.1f; // m/s: ahead, up/down, floating down at the end at the slowest
        internal static float SinkEase = 3f;                                     // m/s²: how gently the floating down sets in
        internal static float SinkSeconds = 10f, SinkLast = 1.5f, SinkFast = 8f; // the floating down: about this long from any height; the last moments' pace; at most m/s
        internal static float Accel = 9f, Decel = 6f, ClimbAccel = 12f;          // m/s²: the pull on starting and the drift on stopping
        internal static float LiftHeight = 4f, LiftSeconds = 1.1f, LiftAccel = 40f; // the leap up at the start: like a jump, smoother
        internal static float MaxHeight = 40f;                                   // over the ground (or the sea)
        internal static float TurnRate = 140f, TurnEase = 0.35f;                 // the body turns after the way it flies: at most °/s, and how long it eases
        internal static float MuninAfter = 0.4f, LiftAfter = 0.3f;               // Hugin first, then you (after the crouch of the take-off clip), then Munin
        internal static float FlourishMin = 7f, FlourishMax = 14f;              // a gentle stir this often (seconds)
        internal static float SafeAfter = 3f;                                    // no fall hurts this long after landing

        internal const string NetKey = "RavenWatch_OdinWings", Tag = "odinwings", SinkTag = "odinwings_sink";
        private const string RpcUp = "RavenWatch_WingsUp", RpcDown = "RavenWatch_WingsDown", RpcHop = "RavenWatch_WingsHop";
        internal static float Duration() { return Trust.RoundTime(Trust.LerpFrom(false, RavenAbilities.UnlockLevel(RavenAbilities.Find("hflight")), Seconds, SecondsTop) * GiftUp.Power("hflight")); } // (from the gift's own first level)

        private enum Phase { None, Lift, Fly, Sink, Ground } // (Ground: down on your feet, the gift still on)
        private static Phase _phase;
        private static double _until;
        private static float _since, _nextFlourish;
        private static bool _muninSent;
        private static Vector3 _vel;
        private static float _yawVel;

        private static double Now { get { return WorldTime.Now; } }

        internal static bool Flying() { return _phase != Phase.None; } // the gift is on (in the air or on the ground)
        internal static bool InAir() { return _phase == Phase.Lift || _phase == Phase.Fly || _phase == Phase.Sink; }
        internal static bool OnGround() { return _phase == Phase.Ground; }
        internal static float Left() { return Flying() ? Mathf.Max(0f, (float)(_until - Now)) : 0f; }
        internal static bool Sinking() { return _phase == Phase.Sink; }
        internal static string PhaseName() { return _phase == Phase.Lift ? Lang.T("зліт") : _phase == Phase.Fly ? Lang.T("політ") : _phase == Phase.Sink ? Lang.T("спуск") : _phase == Phase.Ground ? Lang.T("на землі") : "—"; }
        // any player in flight, as drawn by this game
        internal static bool Shown(Player p) { return p != null && (p == Player.m_localPlayer ? Flying() : NetFlag.Get(p, NetKey)); }
        // any player up in the air on the wings (not walking with the gift on): ours by the phase, the others' by their flight tag
        internal static bool AirShown(Player p)
        {
            if (p == null) return false;
            if (p == Player.m_localPlayer) return InAir();
            ZDO z = NetFlag.Zdo(p, false);
            return z != null && NetFlag.Get(p, NetKey) && z.GetString(PlayerAnims.TagKey, "").Contains(Tag);
        }

        internal static void Register()
        {
            RavenFx.Register(RpcUp, Burst);
            RavenFx.Register(RpcDown, Landing);
            RavenFx.Register(RpcHop, Hop);
        }

        // A gift out of place with the wings on (null: it may be used). In the air: no scream, no fury, no tailwind.
        // While the gift lasts at all (on your feet too): no dome, no scouting (Hugin holds your wings) and no leap of
        // his (Space is the wings' own leap). The strikes and the rest work.
        internal static string Blocks(RavenAbility a)
        {
            if (a == null || _phase == Phase.None) return null;
            switch (a.Id)
            {
                case "dome": return Lang.T("Під крилом: Хугін тримає для тебе крила — купол не розкриє");
                case "scout": return Lang.T("Розвідка: Хугін тримає для тебе крила — нікуди не полетить");
                case "leap": return Lang.T("Злет: у тебе вже крила Одіна");
                case "scream": return InAir() ? Lang.T("Крик: у польоті не до крику") : null;
                case "fury": return InAir() ? Lang.T("Лють: у польоті нема кого рубати") : null;
                case "wings": return InAir() ? Lang.T("Попутний вітер: у польоті крила Одіна вже несуть тебе") : null;
            }
            return null;
        }

        // why you cannot take off here, or null
        internal static string Refused(Player p)
        {
            if (p.InInterior() || Senses.Underground) return Lang.T("Під землею Хугіну нема де розправити крила");
            if (Senses.Indoors) return Lang.T("Під дахом не злетиш — вийди під небо");
            if (p.IsEncumbered()) return Lang.T("Занадто важко — Хугін тебе не підніме");
            if (p.InWater()) return Lang.T("З води не злетиш");
            if (p.IsAttached() || p.IsRiding()) return Lang.T("Спершу злізь");
            if (Beast.On()) return Lang.T("Звіра Хугін не підніме");
            return null;
        }

        internal static void Start(Player p)
        {
            _phase = Phase.Lift;
            _since = Time.time;
            _until = Now + Duration();
            _muninSent = false;
            _nextFlourish = Time.time + LiftSeconds + UnityEngine.Random.Range(FlourishMin, FlourishMax);
            Rigidbody rb = p.GetComponent<Rigidbody>();
            _vel = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
            if (_logUntil > 0f) { _logUntil = -1f; PAnimLog.End(Lang.T("новий політ")); } // the last landing still recording
            PAnimLog.Begin(Lang.T("Крила Одіна"));
            NetFlag.Set(p, NetKey, true);
            PlayerAnims.Tag(p, Tag, true);
            Safe(p, Duration() + 30f); // no fall hurts while you fly (ended at the landing, a few seconds after)
            p.HideHandItems();
            HoldCrouch(p);
            RavenFx.Broadcast(RpcUp, p, Burst);
            Orders.FlyWith(p, false, Duration() + 20f); // Hugin first
            PlayerAnims.Play(p, 2, "wings_takeoff", 0.08f, 1f);
        }

        // pressed again, or the time is up: down gently
        internal static void Sink(Player p)
        {
            if (_phase == Phase.Ground) { if (p != null) RavenSkills.GiftSay(false, "hflight_end", Lang.T("Досить неба на сьогодні.")); Land(p); return; }
            if (_phase == Phase.None || _phase == Phase.Sink) return;
            _phase = Phase.Sink;
            _since = Time.time;
            PAnimLog.Event(Lang.T("фаза: спуск"));
            if (p != null) PlayerAnims.Tag(p, SinkTag, true);
            if (p != null) RavenSkills.GiftSay(false, "hflight_end", Lang.T("Крила втомились. Тримайся, опускаю."));
        }

        private static void Land(Player p)
        {
            if (_phase == Phase.None) return;
            bool air = _phase != Phase.Ground; // (on your feet already: no landing again)
            _phase = Phase.None;
            if (p == null) { _flaring = false; return; }
            Flaring(p, false);
            HoldCrouch(p); // (the key held for "down": no sneaking after the landing)
            NetFlag.Set(p, NetKey, false);
            PlayerAnims.Tag(p, Tag, false);
            PlayerAnims.Tag(p, SinkTag, false);
            PlayerAnims.Stop(p, 2, 0.2f);
            Safe(p, SafeAfter);
            if (!p.IsDead() && air)
            {
                PlayerAnims.Play(p, 2, "wings_land", 0.05f, 1f);
                RavenFx.Broadcast(RpcDown, p, Landing);
            }
            Orders.FlyWithEnd(p, false);
            Orders.FlyWithEnd(p, true);
            if (p.IsDead()) PAnimLog.End(Lang.T("загинув")); else { PAnimLog.Event(Lang.T("посадка")); _logUntil = Time.time + 1.5f; }
        }

        private static float _logUntil = -1f, _refollowAt;

        // down on your feet while the gift lasts: walk, then Space leaps you up again
        private static void Touch(Player p)
        {
            _phase = Phase.Ground;
            _since = Time.time;
            _vel = Vector3.zero;
            PAnimLog.Event(Lang.T("на землю (дар ще діє)"));
            Flaring(p, false);
            HoldCrouch(p);
            PlayerAnims.Tag(p, Tag, false);
            PlayerAnims.Tag(p, SinkTag, false);
            PlayerAnims.Play(p, 2, "wings_land", 0.05f, 1f);
            RavenFx.Broadcast(RpcDown, p, Landing);
        }

        // Space on the ground while the gift lasts (instead of the game's jump): up again; true if it took the jump
        internal static bool Leap(Player p)
        {
            if (_phase != Phase.Ground || p == null) return false;
            string no = Refused(p);
            if (no != null && !p.InWater()) { RavenMsg.Center(p, no); return true; }
            _phase = Phase.Lift;
            _since = Time.time;
            _nextFlourish = Time.time + LiftSeconds + UnityEngine.Random.Range(FlourishMin, FlourishMax);
            Rigidbody rb = p.GetComponent<Rigidbody>();
            _vel = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
            PAnimLog.Event(Lang.T("знову злет із землі"));
            HoldCrouch(p);
            PlayerAnims.Tag(p, Tag, true);
            p.HideHandItems();
            PlayerAnims.Play(p, 2, "wings_takeoff", 0.08f, 1f);
            RavenFx.Broadcast(RpcHop, p, Hop);
            return true;
        }

        // from the skills' Update (the local player)
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            WingsLook.Tick();
            ReleaseCrouch();
            if (_logUntil > 0f && Time.time >= _logUntil) { _logUntil = -1f; PAnimLog.End(Lang.T("після посадки")); }
            if (_phase == Phase.None) return;
            if (p == null || p.IsDead() || p.IsTeleporting()) { Land(p); if (p == null) PAnimLog.End(Lang.T("гравця нема")); return; }
            if (!_muninSent && Time.time - _since >= MuninAfter && _phase != Phase.Sink)
            {
                _muninSent = true;
                if (Trust.Worn(true) != null) Orders.FlyWith(p, true, Left() + 20f);
            }
            if (Time.time >= _refollowAt && _phase != Phase.Sink)
            {
                _refollowAt = Time.time + 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    bool m = i == 1;
                    if (m && !_muninSent) continue;
                    if (Trust.Worn(m) != null && Orders.Of(p, m) == Orders.None && !Strike.Busy(p, m) && Shoulder.Arrived(p, m)) Orders.FlyWith(p, m, Left() + 20f);
                }
            }
            if (_phase == Phase.Ground) { if (Now >= _until) Land(p); return; } // on your feet: the time runs out quietly
            if (_phase != Phase.Sink && (Now >= _until || p.IsEncumbered() || p.IsAttached())) Sink(p);
            // a gentle stir now and then (over the clips the rules pick): a soft stroke, a small sag, a stretch
            if (_phase == Phase.Fly && Time.time >= _nextFlourish)
            {
                _nextFlourish = Time.time + UnityEngine.Random.Range(FlourishMin, FlourishMax);
                PlayerAnimator a = PlayerAnimator.Of(p);
                // only on the way (their arms are the flight's, out at the shoulders): hanging in place the arms stay low
                bool going = a != null && a.ValueOf("speed") > 3f;
                if (going && !a.Playing(2)) PlayerAnims.Play(p, 2, Flourishes[UnityEngine.Random.Range(0, Flourishes.Length)], 0.12f, 1f);
            }
        }

        private static readonly string[] Flourishes = { "wings_beat", "wings_dip", "wings_stretch" };

        internal static void Reset()
        {
            _phase = Phase.None;
            _vel = Vector3.zero;
            _flaring = false;
            _noCrouch = false;
            WingsLook.CloseAll();
        }

        // ---------------------------------------------------------------- the motion (FixedUpdate of the local player)

        private static readonly int FallingHash = ZSyncAnimation.GetHash("falling"), GroundHash = ZSyncAnimation.GetHash("onGround"),
            WaterHash = ZSyncAnimation.GetHash("inWater"), FlyingHash = ZSyncAnimation.GetHash("flying"), ForwardHash = ZSyncAnimation.GetHash("forward_speed"),
            SideHash = ZSyncAnimation.GetHash("sideway_speed");
        private static readonly System.Reflection.MethodInfo TakesInput = AccessTools.Method(typeof(Player), "TakeInput");
        private static readonly System.Reflection.MethodInfo EyeTurn = AccessTools.Method(typeof(Player), "UpdateEyeRotation");

        internal static void Motion(Player p, float dt, Rigidbody body, ZSyncAnimation zanim, Vector3 moveDir, Quaternion lookYaw, ref float maxAir, ref Vector3 currentVel, ref float lastGround)
        {
            bool input = false;
            try { input = TakesInput != null && (bool)TakesInput.Invoke(p, null); } catch { }
            bool up = input && (ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")) && !Hud.IsPieceSelectionVisible();
            bool down = input && (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch"));
            Vector3 pos = p.transform.position;
            float height = HeightAt(pos);

            Vector3 flat = new Vector3(moveDir.x, 0f, moveDir.z);
            if (flat.sqrMagnitude > 1f) flat.Normalize();
            Vector3 target = flat * Speed * (Facets.On("hflight") ? 1.15f : 1f); // («Крила Одіна» III: faster)
            float vy;
            bool crouching = _phase == Phase.Lift && Time.time - _since < LiftAfter; // still on the ground, gathering for the leap
            switch (_phase)
            {
                case Phase.Lift:
                    // a crouch, then a leap like a jump (strong at first, easing off at the top), then on into the flight
                    float t = Time.time - _since;
                    if (t < LiftAfter) { vy = 0f; target *= 0.3f; break; }
                    float u = Mathf.Clamp01((t - LiftAfter) / LiftSeconds);
                    vy = LiftHeight / LiftSeconds * 2f * (1f - u); // the area under it: LiftHeight
                    if (u >= 1f) { _phase = Phase.Fly; _since = Time.time; PAnimLog.Event(Lang.T("фаза: політ")); }
                    break;
                case Phase.Sink:
                    // like a cloak of levitation, down in about SinkSeconds from any height: quicker high up, the pace
                    // set by the height left over the time left, and slow and soft in the last metres
                    target *= 0.6f;
                    float left = Mathf.Max(SinkLast, SinkSeconds - (Time.time - _since));
                    vy = -Mathf.Clamp(height / left, SinkSlow, SinkFast);
                    break;
                default:
                    vy = up ? ClimbSpeed : down ? -ClimbSpeed : 0f;
                    break;
            }

            // the landing seen a few metres ahead: going down near the ground, or flying on into rising ground — the body
            // swings feet first, the pace drops, it settles and touches down with a small crouch (not at full speed into it)
            bool flare = false;
            if (_phase == Phase.Fly || _phase == Phase.Sink)
            {
                bool goingDown = _phase == Phase.Sink || down || _vel.y < -0.5f;
                if (_phase == Phase.Fly && (up || _overWater)) { } // (Space: away from the ground; over the sea: no landing, it pushes you up)
                else if (goingDown && height < FlareHeight) flare = true;
                else if (_phase == Phase.Fly && !up && new Vector2(_vel.x, _vel.z).sqrMagnitude > 4f && GroundAhead(pos, _vel)) flare = true;
            }
            Flaring(p, flare);
            if (flare)
            {
                target = Vector3.ClampMagnitude(target, FlareSpeed);
                if (_phase == Phase.Fly) vy = -Mathf.Clamp(height * 1.1f, FlareSink, 2.5f); // slower the nearer
                if (height < TouchHeight || p.IsOnGround())
                {
                    if (_phase == Phase.Sink) Land(p); else Touch(p); // (Touch: on your feet, the gift still on)
                    return;
                }
            }
            // not higher than MaxHeight over the ground; not into the sea
            if (height > MaxHeight && vy > 0f) vy = 0f;
            if (height > MaxHeight + 3f) vy = Mathf.Min(vy, -2f);
            float sea = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            if (pos.y < sea + 0.6f && _phase != Phase.Sink) vy = Mathf.Max(vy, 4f);

            // the pull and the drift: the pace changes over time, never at once
            Vector3 h = new Vector3(_vel.x, 0f, _vel.z);
            float rate = target.sqrMagnitude >= h.sqrMagnitude ? Accel * (Facets.On("hflight") ? 1.5f : 1f) : flare ? FlareDecel : Decel;
            h = Vector3.MoveTowards(h, target, rate * dt);
            float yv = Mathf.MoveTowards(_vel.y, vy, (_phase == Phase.Lift ? LiftAccel : _phase == Phase.Sink ? SinkEase : ClimbAccel) * dt);
            _vel = new Vector3(h.x, yv, h.z);

            // touched down while sinking, or into the water: the flight is over
            if (_phase == Phase.Sink && (p.IsOnGround() || height < 0.15f || pos.y <= sea + 0.1f)) { Land(p); return; }

            body.useGravity = false;
            body.linearVelocity = _vel;
            body.angularVelocity = Vector3.zero;
            currentVel = _vel;
            maxAir = pos.y;
            lastGround = 1f; // not "just touched the ground": else the game jumps on Space up here (IsOnGround) and the landing checks lie
            // the body turns after the way it flies (standing: where the camera looks)
            Quaternion want = h.sqrMagnitude > 1f ? Quaternion.LookRotation(new Vector3(h.x, 0f, h.z)) : lookYaw;
            // eased: it starts turning slowly, keeps a steady pace, and settles (no snap round to the camera)
            float yaw = p.transform.eulerAngles.y;
            yaw = Mathf.SmoothDampAngle(yaw, want.eulerAngles.y, ref _yawVel, TurnEase, TurnRate, dt);
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            try { if (EyeTurn != null) EyeTurn.Invoke(p, null); } catch { }
            // the game's own "in the air" animation under our clips (standing while you gather for the leap: its falling
            // pose has the knees pulled up, and would show through before the take-off clip has taken the legs)
            if (zanim != null)
            {
                zanim.SetBool(FallingHash, !crouching);
                zanim.SetBool(GroundHash, crouching);
                zanim.SetBool(WaterHash, false);
                zanim.SetBool(FlyingHash, false);
                zanim.SetFloat(ForwardHash, 0f);
                zanim.SetFloat(SideHash, 0f);
            }
        }

        // ---------------------------------------------------------------- the landing ahead

        internal static float FlareHeight = 2.6f;     // going down: the landing starts this high over the ground
        internal static float FlareSpeed = 4f;        // m/s ahead while landing
        internal static float FlareDecel = 16f;       // m/s²: how hard the pace drops then
        internal static float FlareSink = 0.9f;       // m/s down at the last moment
        internal static float TouchHeight = 0.3f;     // the feet down: on the ground
        internal const string LandTag = "odinwings_land";
        private static bool _flaring;

        private static void Flaring(Player p, bool on)
        {
            if (on == _flaring) return;
            _flaring = on;
            PlayerAnims.Tag(p, LandTag, on);
            if (on) PAnimLog.Event(Lang.T("підліт до посадки"));
        }

        // rising ground a little ahead on the way you fly (a slope you can stand on, not a wall: the wall just stops you)
        private static float _aheadAt = -1f;
        private static bool _ahead;
        private static bool GroundAhead(Vector3 pos, Vector3 vel)
        {
            if (Time.time - _aheadAt < 0.05f) return _ahead;
            _aheadAt = Time.time;
            Vector3 dir = vel.normalized;
            float dist = Mathf.Clamp(vel.magnitude * 0.3f, 1.5f, 4f);
            RaycastHit hit;
            _ahead = Physics.SphereCast(pos + Vector3.up * 0.5f, 0.3f, dir, out hit, dist, RavenMotor.Mask) && hit.normal.y > 0.6f;
            return _ahead;
        }

        // ---------------------------------------------------------------- the crouch key (it is "down" in the air)

        // held for "down" in flight, it would leave you sneaking on landing: the game's crouch waits until it is let go
        private static bool _noCrouch;
        private static readonly System.Reflection.MethodInfo SetCrouchM = AccessTools.Method(typeof(Character), "SetCrouch");
        private static readonly System.Reflection.FieldInfo CrouchToggled = AccessTools.Field(typeof(Player), "m_crouchToggled");

        internal static bool CrouchBlocked() { return _noCrouch; }

        private static void HoldCrouch(Player p)
        {
            _noCrouch = true;
            if (p == null) return;
            try { if (CrouchToggled != null) CrouchToggled.SetValue(p, false); } catch { }
            try { if (SetCrouchM != null) SetCrouchM.Invoke(p, new object[] { false }); } catch { }
        }

        // the key let go (and not in the air): the crouch is the game's again
        private static void ReleaseCrouch()
        {
            if (!_noCrouch || InAir()) return;
            if (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch")) return;
            _noCrouch = false;
        }

        private static float _heightAt = -1f, _height;
        private static bool _overWater; // the nearest under you is the sea, not the ground
        private static float HeightAt(Vector3 pos)
        {
            if (Time.time - _heightAt < 0.05f) return _height;
            _heightAt = Time.time;
            RaycastHit hit;
            float ground = Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 300f, RavenMotor.Mask) ? hit.distance - 0.3f : 300f;
            float sea = ZoneSystem.instance != null ? pos.y - ZoneSystem.instance.m_waterLevel : 300f;
            _height = Mathf.Max(0f, Mathf.Min(ground, sea));
            _overWater = sea < ground;
            return _height;
        }

        private const string SafeName = "RavenWatch_WingsSafe";
        private static SE_Stats _safe;
        private static void Safe(Player p, float seconds)
        {
            SE_Stats se = RavenFx.Template(ref _safe, SafeName);
            se.m_name = Lang.W("Крила Одіна");
            se.m_tooltip = Lang.T("Хугін тримає тебе в повітрі: падіння не шкодить");
            se.m_icon = AbilityIcons.ForEffect("hflight", false);
            se.m_ttl = seconds;
            se.m_fallDamageModifier = -1f;
            p.GetSEMan().RemoveStatusEffect(SafeName.GetStableHashCode(), true);
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- the feathers (every game)

        // the start: a storm of feathers round you, a gust back off you, a ring and dust from under your feet
        private static void Burst(Player p)
        {
            if (p == null) return;
            Vector3 feet = p.transform.position, chest = feet + Vector3.up * 1.2f;
            Feathers.Gust(p);
            Feathers.Ring(feet, 140, 4f);
            Feathers.Dust(feet, 14, 2.5f);
            for (int i = 0; i < 4; i++) Feathers.Shed(chest + UnityEngine.Random.insideUnitSphere * 0.6f, i % 2 == 1, 45);
            RavenFx.Play("sfx_raven_flap", chest, Quaternion.identity);
            RavenFx.Play("vfx_offering", feet + Vector3.up * 0.5f, Quaternion.identity);
        }

        // up again from the ground while the gift lasts: a gust and a smaller ring
        private static void Hop(Player p)
        {
            if (p == null) return;
            Feathers.Gust(p);
            Feathers.Ring(p.transform.position, 70, 3f);
            RavenFx.Play("sfx_raven_flap", p.transform.position + Vector3.up * 1.2f, Quaternion.identity);
        }

        // the landing: a ring of feathers and a little dust
        private static void Landing(Player p)
        {
            if (p == null) return;
            Vector3 feet = p.transform.position;
            Feathers.Ring(feet, 60, 2.5f);
            Feathers.Dust(feet, 8, 1.8f);
            RavenFx.Play("sfx_raven_flap", feet + Vector3.up, Quaternion.identity);
        }
    }

    // the flight is the owner's own motion: the game's walking, swimming and falling wait
    [HarmonyPatch(typeof(Character), "UpdateMotion")]
    internal static class WingsMotionPatch
    {
        private static bool Prefix(Character __instance, float dt, Rigidbody ___m_body, ZSyncAnimation ___m_zanim, Vector3 ___m_moveDir, Quaternion ___m_lookYaw,
            ref float ___m_maxAirAltitude, ref Vector3 ___m_currentVel, ref float ___m_lastGroundTouch)
        {
            if (!OdinWings.InAir()) return true;
            Player p = __instance as Player;
            if (p == null || p != Player.m_localPlayer || p.IsDead() || ___m_body == null) return true;
            try { OdinWings.Motion(p, dt, ___m_body, ___m_zanim, ___m_moveDir, ___m_lookYaw, ref ___m_maxAirAltitude, ref ___m_currentVel, ref ___m_lastGroundTouch); return false; }
            catch (Exception e) { PatchGuard.Fail("WingsMotionPatch", e); return true; }
        }
    }

    // no jump and no dodge of the game's own in flight: Space is "up" there
    [HarmonyPatch(typeof(Character), "Jump")]
    internal static class WingsJumpPatch
    {
        private static bool Prefix(Character __instance)
        {
            if (!OdinWings.Flying() || __instance != Player.m_localPlayer) return true;
            if (OdinWings.OnGround()) return !OdinWings.Leap((Player)__instance); // on your feet: up again instead of the game's jump
            return false; // in the air Space is "up"
        }
    }

    // the crouch key is "down" in the air, and after a landing it waits to be let go (no sneaking off at once)
    [HarmonyPatch(typeof(Player), "SetControls")]
    internal static class WingsCrouchPatch
    {
        private static void Prefix(Player __instance, ref bool crouch)
        {
            if (__instance == Player.m_localPlayer && (OdinWings.InAir() || OdinWings.CrouchBlocked())) crouch = false;
        }
    }

    [HarmonyPatch(typeof(Player), "Dodge")]
    internal static class WingsDodgePatch
    {
        private static bool Prefix(Player __instance) { return !(OdinWings.InAir() && __instance == Player.m_localPlayer); }
    }

    // nothing to strike with up there
    [HarmonyPatch(typeof(Humanoid), "StartAttack")]
    internal static class WingsAttackPatch
    {
        private static bool Prefix(Humanoid __instance, ref bool __result)
        {
            if (!OdinWings.InAir() || __instance != Player.m_localPlayer) return true;
            __result = false;
            return false;
        }
    }

    // Every game: the feathers trailing after Hugin (and lighter after you) while a player flies on the wings.
    internal static class WingsLook
    {
        private sealed class Look { public ParticleSystem Hugin, Self; public GameObject HuginModel; }
        private static readonly Dictionary<Player, Look> Looks = new Dictionary<Player, Look>();
        private static float _next;

        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null) continue;
                bool on = OdinWings.Shown(pl);
                Look l;
                Looks.TryGetValue(pl, out l);
                if (!on) { if (l != null) { End(l); Looks.Remove(pl); } continue; }
                if (l == null) { l = new Look(); Looks[pl] = l; l.Self = Feathers.Trail(pl.transform, false); }
                GameObject model = Shoulder.ModelOf(pl, false);
                if (model != l.HuginModel)
                {
                    Feathers.EndTrail(l.Hugin);
                    l.Hugin = model != null ? Feathers.Trail(model.transform, false) : null;
                    l.HuginModel = model;
                }
            }
            List<Player> gone = null;
            foreach (Player k in Looks.Keys) if (k == null) { if (gone == null) gone = new List<Player>(); gone.Add(k); }
            if (gone != null) foreach (Player k in gone) { End(Looks[k]); Looks.Remove(k); }
        }

        private static void End(Look l) { Feathers.EndTrail(l.Hugin); Feathers.EndTrail(l.Self); }

        internal static void CloseAll() { foreach (Look l in Looks.Values) End(l); Looks.Clear(); }
    }
}
#endif

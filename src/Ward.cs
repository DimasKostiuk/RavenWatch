#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Заслона Муніна»: a wall of runes in front of you that takes a monster's blow from the front, whatever its size
    // (a two-star wolf that would kill you does not), with all of it: no damage, no push, no poison, no stagger. A blow
    // that would have staggered you staggers the one who struck instead. One blow, two from trust level 6, three at 7;
    // two minutes. A perfect parry of your own keeps the charge: the game parries it, unless that parry would stagger you.
    // The blow lands in the game of the one struck (Character.RPC_Damage runs on the owner), so all of it is decided
    // here; the others only draw it: the charges in the player's ZDO, a message for each blow taken.
    internal static class Ward
    {
        internal static float Seconds = 120f;
        internal static int UnlockLevel = 4, TwoBlowsAt = 6, ThreeBlowsAt = 7;
        internal static float FrontDegrees = 180f;           // the whole front half
        internal static float PushForce = 40f, PushReach = 5f; // the one who struck is pushed back (not a boss, not from afar)
        internal static bool ShowNumber = true;              // the blow's damage over you in red, as the game shows a hit taken
        internal static float CrackTalkChance = 0.5f;
        internal static Color Red = new Color(1f, 0.3f, 0.24f); // red, not quite: a touch of ember in it
        internal static float NumberSeconds = 2f; // its damage number stays this many times longer than the game's own

        internal const string ChargesKey = "RavenWatch_Ward", UntilKey = "RavenWatch_WardUntil";
        private const string HitRpc = "RavenWatch_WardHit";

        internal static float Duration() { return Trust.RoundTime(Seconds * GearFx.GuardMul(true) * GiftUp.Power("ward")); } // (Спіраль захисту: longer)
        internal static int BlowsAt(int level) { return level >= ThreeBlowsAt ? 3 : level >= TwoBlowsAt ? 2 : 1; }

        private static int _charges, _max;
        private static double _until;
        private static float _nextScan;
        private static readonly System.Reflection.FieldInfo StaggerNow = AccessTools.Field(typeof(Character), "m_staggerDamage");

        private static double Now { get { return WorldTime.Now; } }
        private static long Me { get { return ZDOMan.GetSessionID(); } }

        // the blow the ward took, back into the shooter (its damage, from you: as if the arrow flew back)
        private static void Reflect(Player p, HitData hit, Character attacker)
        {
            HitData back = new HitData();
            back.m_damage = hit.m_damage.Clone();
            back.m_point = attacker.GetCenterPoint();
            back.m_dir = (attacker.GetCenterPoint() - p.GetCenterPoint()).normalized;
            back.m_ranged = true;
            back.m_skill = Skills.SkillType.None;
            back.m_skillRaiseAmount = Strike.Marker - Mathf.Max(1f, back.GetTotalDamage()); // (a raven's doing: no skill, no strike from the shadow; the mark also caps it at its own size: Strike.Cut)
            back.SetAttacker(p);
            attacker.Damage(back);
            if (Plugin.Instance != null) Plugin.Instance.Log("Munin ward sent a shot back into " + attacker.m_name);
        }

        internal static bool Mine() { return _charges > 0 && _until > Now; }
        internal static int Charges() { return Mine() ? _charges : 0; }
        internal static int MaxCharges() { return _max; }
        internal static float Left() { return Mine() ? (float)(_until - Now) : 0f; }

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(HitRpc, new Action<long, ZPackage>(OnHit)); }

        internal static void Raise(Player p, int level)
        {
            _max = _charges = BlowsAt(level);
            _until = Now + Duration();
            Publish(p);
            WardLook.For(p, true);
        }

        // what the others draw: the blows left and until when (world seconds)
        private static void Publish(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            zdo.Set(ChargesKey, Mine() ? _charges : 0);
            zdo.Set(UntilKey, Mine() ? (float)_until : 0f);
            zdo.Set(ChargesKey + "Max", _max);
        }

        // any player's ward as drawn: the blows it can still take (0: none)
        internal static int Shown(Player p, out int max)
        {
            max = 1;
            if (p == null || p.IsDead()) return 0;
            if (p == Player.m_localPlayer) { max = Mathf.Max(1, _max); return Charges(); }
            ZDO zdo = NetFlag.Zdo(p, false);
            if (zdo == null) return 0;
            int n = zdo.GetInt(ChargesKey, 0);
            if (n <= 0 || zdo.GetFloat(UntilKey, 0f) <= Now) return 0;
            max = Mathf.Max(n, zdo.GetInt(ChargesKey + "Max", n));
            return n;
        }

        // the last seconds of a ward (it flickers then): ours exactly, the others' by their message
        internal static float LeftOf(Player p)
        {
            if (p == Player.m_localPlayer) return Left();
            ZDO zdo = NetFlag.Zdo(p, false);
            return zdo != null ? (float)(zdo.GetFloat(UntilKey, 0f) - Now) : 0f;
        }

        // from the skills' Update
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (_charges > 0 && (p == null || p.IsDead() || _until <= Now))
            {
                bool timeUp = p != null && !p.IsDead();
                _charges = 0;
                if (p != null) Publish(p);
                if (timeUp) RavenSkills.GiftSay(true, "ward_end", Lang.T("Все, заслона здулася. Далі {сам|сама}."));
            }
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.25f;
            int max;
            foreach (Player pl in Player.GetAllPlayers())
                if (pl != null && Shown(pl, out max) > 0) WardLook.For(pl, false);
        }

        internal static void Reset()
        {
            _charges = 0; _max = 0; _until = 0;
            WardLook.CloseAll();
        }

        // ---------------------------------------------------------------- the blow

        // true: the ward took this blow, the game must not
        internal static bool TryBlock(Player p, HitData hit)
        {
            if (!Mine() || hit == null || p == null || p != Player.m_localPlayer) return false;
            if (hit.m_hitType != HitData.HitType.EnemyHit && hit.m_hitType != HitData.HitType.PlayerHit) return false; // not a fall, fire, poison, a tree
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker == p) return false;
            // the game would let it pass anyway: not a blow to spend the ward on
            if (p.IsDead() || p.GetHealth() <= 0f || p.IsDebugFlying() || p.IsTeleporting() || p.InCutscene()) return false;
            if (hit.m_dodgeable && p.IsDodgeInvincible()) return false;
            if (attacker.IsPlayer() && !p.IsPVPEnabled() && !hit.m_ignorePVP) return false;
            Vector3 from = Source(p, hit, attacker);
            if (Vector3.Angle(Flat(p.transform.forward, Vector3.forward), from) > FrontDegrees * 0.5f) return false; // from behind: it lands
            // your own perfect parry: the game parries it and the ward keeps its charge; a parry that would stagger you
            // (or leave you without stamina, so the blow lands after all) is the ward's: it takes the blow, the striker staggers
            bool parryFails;
            bool parry = Parry(p, hit, attacker, out parryFails);
            if (parry && !parryFails)
            {
                WardLook kept = WardLook.For(p, false);
                if (kept != null) kept.Glint();
                return false;
            }
            float damage;
            bool stagger;
            Measure(p, hit, attacker, out damage, out stagger);
            if (parry) stagger = true;
            if (damage <= 0.1f && !stagger && hit.m_statusEffectHash == 0) return false; // nothing in it to stop

            _charges--;
            Publish(p);
            if (hit.m_ranged && !attacker.IsPlayer() && Facets.On("ward")) Reflect(p, hit, attacker); // («Заслона Муніна» III)
            Vector3 away = Flat(attacker.transform.position - p.transform.position, from);
            if (stagger) attacker.Stagger(away);
            if (!attacker.IsBoss() && !attacker.IsPlayer() && PushForce > 0f && Vector3.Distance(attacker.transform.position, p.transform.position) <= PushReach) Push(attacker, away);
            if (ShowNumber) Number(hit.m_point != Vector3.zero ? hit.m_point : p.GetCenterPoint() + from * 0.6f, damage);
            WardLook look = WardLook.For(p, false);
            if (look != null) look.Hit(from, _charges);
            Send(p, from);
            if (Plugin.Instance != null) Plugin.Instance.Log("Munin ward took a blow of " + attacker.m_name + ": " + damage.ToString("0.#") + (stagger ? ", would stagger: the attacker staggers" : "") + ", left " + _charges);
            if (_charges <= 0) RavenSkills.GiftSay(true, "ward_broken", Lang.T("Тріснула, сука! Більше не прикрию!"));
            else if (UnityEngine.Random.value < CrackTalkChance) RavenSkills.GiftSay(true, "ward_crack", Lang.T("Тримається! Ще витримає!"));
            return true;
        }

        // The blow's damage in red over you, as the game shows a hit taken, but longer. The game draws its own copy at
        // once (a message to everybody is handled here first, in the same call), so the newest of its texts is this
        // one: only that one is held longer; every other number of the game keeps its time.
        private static readonly System.Reflection.FieldInfo TextsField = AccessTools.Field(typeof(DamageText), "m_worldTexts");
        private static System.Reflection.FieldInfo _durationField;

        private static void Number(Vector3 at, float damage)
        {
            DamageText dt = DamageText.instance;
            if (dt == null) return;
            System.Collections.IList texts = TextsField != null ? TextsField.GetValue(dt) as System.Collections.IList : null;
            int before = texts != null ? texts.Count : -1;
            dt.ShowText(DamageText.TextType.Normal, at, damage, true);
            if (texts == null || texts.Count <= before || NumberSeconds <= 1f) return; // too far, the HUD hidden: none was made
            try
            {
                object made = texts[texts.Count - 1];
                if (_durationField == null) _durationField = AccessTools.Field(made.GetType(), "m_duration");
                if (_durationField != null) _durationField.SetValue(made, (float)_durationField.GetValue(made) * NumberSeconds);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ward number time: " + e.Message); }
        }

        // The game's perfect parry, as Humanoid.BlockAttack decides it: blocking, facing the blow, the shield up less than
        // a quarter second ago, a shield or weapon that parries. fails: it would still stagger you, or leave you without
        // stamina (then the game lets the blow through).
        private static readonly System.Reflection.MethodInfo BlockerOf = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");
        private static readonly System.Reflection.FieldInfo BlockTimer = AccessTools.Field(typeof(Humanoid), "m_blockTimer");
        internal static float ParrySeconds = 0.25f;

        private static bool Parry(Player p, HitData hit, Character attacker, out bool fails)
        {
            fails = false;
            if (!hit.m_blockable || !p.IsBlocking() || Vector3.Dot(hit.m_dir, p.transform.forward) > 0f) return false;
            if (BlockerOf == null || BlockTimer == null) return false;
            ItemDrop.ItemData b = BlockerOf.Invoke(p, null) as ItemDrop.ItemData;
            if (b == null || b.m_shared == null || b.m_shared.m_timedBlockBonus <= 1f) return false;
            float timer = (float)BlockTimer.GetValue(p);
            if (timer == -1f || timer >= ParrySeconds) return false;
            float power = b.GetBlockPower(p.GetSkillFactor(Skills.SkillType.Blocking)) * b.m_shared.m_timedBlockBonus;
            p.GetSEMan().ModifyTimedBlockBonus(ref power);
            HitData h = hit.Clone();
            if (!attacker.IsPlayer() && Game.instance != null)
            {
                h.ApplyModifier(Game.instance.GetDifficultyDamageScalePlayer(p.transform.position));
                h.ApplyModifier(Game.m_enemyDamageRate);
            }
            if (b.m_shared.m_damageModifiers != null && b.m_shared.m_damageModifiers.Count > 0)
            {
                HitData.DamageModifiers mods = default(HitData.DamageModifiers);
                mods.Apply(b.m_shared.m_damageModifiers);
                HitData.DamageModifier dm;
                h.ApplyResistance(mods, out dm);
            }
            HitData.DamageTypes left = h.m_damage.Clone();
            left.ApplyArmor(power);
            float st = left.GetTotalStaggerDamage();
            p.GetSEMan().ModifyStagger(st, ref st);
            float now = 0f;
            try { if (StaggerNow != null) now = (float)StaggerNow.GetValue(p); } catch { }
            bool staggers = hit.m_staggerMultiplier >= 100f || (p.m_staggerDamageFactor > 0f && st > 0f && now + st >= p.GetMaxHealth() * p.m_staggerDamageFactor);
            bool tired = p.GetStamina() - p.m_perfectBlockStaminaDrain <= 0f;
            fails = staggers || tired;
            return true;
        }

        // where the blow came from, flat, from you: against its flight (a swing, an arrow, a blast), or the striker
        private static Vector3 Source(Player p, HitData hit, Character attacker)
        {
            Vector3 toAttacker = Flat(attacker.transform.position - p.transform.position, p.transform.forward);
            if (hit.m_dir.sqrMagnitude > 0.01f) return Flat(-hit.m_dir, toAttacker);
            return toAttacker;
        }

        private static Vector3 Flat(Vector3 v, Vector3 otherwise)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : otherwise;
        }

        // what the blow would have done, the way the game reckons it (Character.RPC_Damage, ApplyDamage, AddStaggerDamage)
        private static void Measure(Player p, HitData hit, Character attacker, out float damage, out bool stagger)
        {
            HitData h = hit.Clone();
            if (!attacker.IsPlayer() && Game.instance != null)
            {
                h.ApplyModifier(Game.instance.GetDifficultyDamageScalePlayer(p.transform.position));
                h.ApplyModifier(Game.m_enemyDamageRate);
            }
            HitData.DamageModifier mod;
            h.ApplyResistance(p.GetDamageModifiers(null), out mod);
            h.ApplyArmor(p.GetBodyArmor());
            h.ApplyModifier(Game.m_localDamgeTakenRate);
            damage = h.GetTotalDamage();
            stagger = hit.m_staggerMultiplier >= 100f;
            if (stagger || p.m_staggerDamageFactor <= 0f) return;
            float st = h.m_damage.GetTotalStaggerDamage() * hit.m_staggerMultiplier;
            p.GetSEMan().ModifyStagger(st, ref st);
            float now = 0f;
            try { if (StaggerNow != null) now = (float)StaggerNow.GetValue(p); } catch { }
            stagger = st > 0f && now + st >= p.GetMaxHealth() * p.m_staggerDamageFactor;
        }

        // a shove back: in the striker's own game when it is not ours (a blow with nothing in it but the push)
        private static void Push(Character c, Vector3 dir)
        {
            ZNetView nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            if (nv.IsOwner()) { c.ApplyPushback(dir, PushForce); return; }
            HitData push = new HitData();
            push.m_pushForce = PushForce;
            push.m_dir = dir;
            push.m_point = c.GetCenterPoint();
            push.m_blockable = false;
            push.m_dodgeable = false;
            c.Damage(push);
        }

        // ---------------------------------------------------------------- seen by the others

        private static void Send(Player p, Vector3 from)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(from);
            pkg.Write(_charges);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, HitRpc, pkg);
        }

        private static void OnHit(long sender, ZPackage pkg)
        {
            try
            {
                ZDOID id = pkg.ReadZDOID();
                Vector3 from = pkg.ReadVector3();
                int left = pkg.ReadInt();
                if (sender == Me) return; // ours, drawn already
                Player me = Player.m_localPlayer;
                foreach (Player pl in Player.GetAllPlayers())
                {
                    if (pl == null || pl.GetZDOID() != id) continue;
                    if (me != null && Vector3.Distance(me.transform.position, pl.transform.position) > 100f) return;
                    WardLook look = WardLook.For(pl, false);
                    if (look != null) look.Hit(from, left);
                    return;
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ward message unreadable: " + e.Message); }
        }
    }

    // after the other mods: a blow someone already refused (the dome, another mod) does not cost the ward
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class WardDamagePatch
    {
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(Character __instance, HitData hit, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            try
            {
                Player p = __instance as Player;
                if (p == null || p != Player.m_localPlayer) return true;
                return !Ward.TryBlock(p, hit);
            }
            catch (Exception e) { PatchGuard.Fail("WardDamagePatch", e); return true; }
        }
    }

    // ---------------------------------------------------------------- the look

    // A curved wall of red runes in front of the player, each on a spring: they lag as you run and sway when you
    // stop; now and then one comes loose, drifts off and fades, and a new one takes its place. A blow: a flash, the parry sound, the runes nearest the blow knocked; the last blow scatters them
    // all towards the striker. Drawn by every game for itself.
    internal class WardLook : MonoBehaviour
    {
        private static readonly Dictionary<Player, WardLook> Looks = new Dictionary<Player, WardLook>();

        internal static float Radius = 0.8f, Spring = 55f, Damping = 6.5f;
        private static readonly float[] Rows = { 0.6f, 1.08f, 1.56f };
        private static readonly int[] PerRow = { 7, 7, 7 }; // 21 runes
        private const float Span = 150f, GrowSeconds = 0.5f, FadeSeconds = 1.2f, LooseSeconds = 1.3f;

        private sealed class Rune
        {
            public Transform T;
            public Mesh M;
            public Color[] Colors = new Color[4];
            public float Angle, Height, Size, Spin, Phase;
            public Vector3 Pos, Vel;
            public float Loose = -1f;   // seconds left drifting off (−1: on its spring)
            public float Alpha;         // its own fade in after coming back
            public bool Odd;            // goes dark when the ward is cracked
        }

        private Player _p;
        private readonly List<Rune> _runes = new List<Rune>();
        private Light _light;
        private readonly List<Mark> _marks = new List<Mark>();
        private float _glintAt = -9f;
        private Transform _aura;
        private Mesh _auraMesh;
        private Renderer _auraRenderer;
        private float _auraYaw, _auraYawVel;
        private Vector3 _auraPosVel;
        private static Material _auraMat;
        private float _born, _closing = -1f, _flashAt = -9f;
        private int _shown = -1;

        internal static WardLook For(Player p, bool cast)
        {
            if (p == null) return null;
            WardLook l;
            if (Looks.TryGetValue(p, out l) && l != null && l._closing < 0f) return l;
            try
            {
                GameObject root = new GameObject("RavenWatch_Ward");
                l = root.AddComponent<WardLook>();
                l._p = p;
                l._born = Time.time;
                l.Build();
                Looks[p] = l;
                RavenFx.Play("sfx_raven_flap", p.transform.position + Vector3.up * 1.5f, Quaternion.identity);
                if (cast) RavenFx.Play("vfx_offering", p.transform.position + Vector3.up * 1.1f + p.transform.forward * 0.6f, Quaternion.identity);
                return l;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Ward look failed: " + e.Message); return null; }
        }

        internal static void CloseAll()
        {
            foreach (WardLook l in new List<WardLook>(Looks.Values)) if (l != null) Destroy(l.gameObject);
            Looks.Clear();
        }

        private void Build()
        {
            Material m = DomeLook.RuneMaterialPublic();
            Vector3 start = _p.transform.position + Vector3.up * 1.5f - _p.transform.right * 0.2f; // from Munin's shoulder
            int odd = 0;
            for (int row = 0; row < Rows.Length; row++)
                for (int i = 0; i < PerRow[row]; i++)
                {
                    float t = PerRow[row] == 1 ? 0.5f : i / (float)(PerRow[row] - 1);
                    Rune r = new Rune();
                    r.Angle = (t - 0.5f) * Span + UnityEngine.Random.Range(-6f, 6f);
                    r.Height = Rows[row] + UnityEngine.Random.Range(-0.06f, 0.06f);
                    r.Size = UnityEngine.Random.Range(0.2f, 0.28f);
                    r.Spin = UnityEngine.Random.Range(-25f, 25f);
                    r.Phase = UnityEngine.Random.Range(0f, 10f);
                    r.Pos = start;
                    r.Vel = UnityEngine.Random.insideUnitSphere * 1.5f;
                    r.Alpha = 1f;
                    r.Odd = (odd++ % 2) == 1;
                    GameObject go = new GameObject("rune");
                    go.transform.SetParent(transform, false);
                    r.T = go.transform;
                    r.M = Quad(UnityEngine.Random.Range(0, 16));
                    go.AddComponent<MeshFilter>().sharedMesh = r.M;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = m;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    _runes.Add(r);
                }
            BuildAura();
            BuildMarks();
            GameObject lightGo = new GameObject("light");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Ward.Red;
            _light.range = 3f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
        }

        // the aura: a faint curved veil of light behind the runes, brighter in the middle, soft at every edge, with
        // slow streaks drifting up it; it turns after you a moment late, as the runes do
        private const float AuraSpan = 165f, AuraLow = 0.2f, AuraHigh = 1.95f;
        private const int AuraSegments = 28;

        private void BuildAura()
        {
            GameObject go = new GameObject("aura");
            go.transform.SetParent(transform, false);
            _aura = go.transform;
            _auraMesh = new Mesh();
            int n = AuraSegments + 1;
            Vector3[] v = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            Color[] col = new Color[n * 2];
            int[] tri = new int[AuraSegments * 6];
            float r = Radius + 0.06f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)AuraSegments;
                float a = (t - 0.5f) * AuraSpan * Mathf.Deg2Rad;
                // fades out at both ends of the arc (sin(π) in floats is a hair below zero: Pow of it is NaN, drawn as a black stripe)
                float edge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.7f);
                v[i * 2] = new Vector3(Mathf.Sin(a) * r, AuraLow, Mathf.Cos(a) * r);
                v[i * 2 + 1] = new Vector3(Mathf.Sin(a) * r * 1.08f, AuraHigh, Mathf.Cos(a) * r * 1.08f); // leans out a little at the top
                uv[i * 2] = new Vector2(t * 3f, 0f);
                uv[i * 2 + 1] = new Vector2(t * 3f, 1f);
                col[i * 2] = col[i * 2 + 1] = new Color(1f, 1f, 1f, edge);
            }
            for (int i = 0; i < AuraSegments; i++)
            {
                int b = i * 2, k = i * 6;
                tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
                tri[k + 3] = b + 2; tri[k + 4] = b + 1; tri[k + 5] = b + 3;
            }
            _auraMesh.vertices = v; _auraMesh.uv = uv; _auraMesh.colors = col; _auraMesh.triangles = tri;
            _auraMesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = _auraMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AuraMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _auraRenderer = mr;
            if (_p != null)
            {
                _aura.position = _p.transform.position;
                _auraYaw = _p.transform.eulerAngles.y;
            }
        }

        private static Material AuraMaterial()
        {
            if (_auraMat != null) return _auraMat;
            const int w = 64, h = 64;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            System.Random rnd = new System.Random(11);
            float[] streak = new float[w];
            for (int x = 0; x < w; x++) streak[x] = (float)rnd.NextDouble();
            for (int y = 0; y < h; y++)
            {
                float vv = y / (float)(h - 1);
                float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(vv / 0.25f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((vv - 0.55f) / 0.45f)));
                for (int x = 0; x < w; x++)
                {
                    float s = Mathf.Clamp01(streak[x] * 2.4f - 1.5f) * (0.5f + 0.5f * Mathf.Sin(vv * 18f + x * 0.4f));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(body * (0.45f + s * 0.55f))));
                }
            }
            t.Apply(true, true);
            t.hideFlags = HideFlags.HideAndDontSave;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            _auraMat = new Material(sh);
            _auraMat.name = "RavenWatch ward aura";
            _auraMat.mainTexture = t;
            _auraMat.hideFlags = HideFlags.HideAndDontSave;
            _auraMat.renderQueue = 3090; // behind the runes
            return _auraMat;
        }

        private static MaterialPropertyBlock _block;
        // the veil and the light, quiet: a faint, muted red (not the runes' own glow)
        internal static float AuraAlpha = 0.12f, LightPower = 0.3f;
        internal static Color AuraTint = new Color(0.85f, 0.42f, 0.38f);

        private void TickAura(Vector3 basePos, float grow, float fade, float flash, bool cracked)
        {
            if (_aura == null) return;
            if (_p != null && _closing < 0f)
            {
                if ((_aura.position - basePos).sqrMagnitude > 25f) _aura.position = basePos; // a portal: no sweep across the world
                _aura.position = Vector3.SmoothDamp(_aura.position, basePos, ref _auraPosVel, 0.07f);
                _auraYaw = Mathf.SmoothDampAngle(_auraYaw, _p.transform.eulerAngles.y, ref _auraYawVel, 0.12f);
            }
            _aura.rotation = Quaternion.Euler(0f, _auraYaw, 0f);
            float big = _p != null ? _p.transform.localScale.x : 1f;
            _aura.localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1f, grow), 1f) * (1f + flash * 0.06f) * big;
            if (_auraMat != null) _auraMat.mainTextureOffset = new Vector2(Time.time * 0.03f, -Time.time * 0.12f);
            float alpha = AuraAlpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f)) * grow * fade;
            if (cracked) alpha *= 0.55f + 0.2f * Mathf.Sin(Time.time * 9f);
            alpha = Mathf.Clamp01(alpha + flash * 0.22f * fade);
            if (_auraRenderer == null) return;
            _auraRenderer.enabled = alpha > 0.005f;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            Color c = Color.Lerp(AuraTint, Color.white, flash * 0.3f);
            c.a = alpha;
            _block.SetColor("_Color", c);
            _auraRenderer.SetPropertyBlock(_block);
        }

        // ---------------------------------------------------------------- the charge marks

        // Over the wall, one mark per blow it can still take: Algiz, the rune of protection, drawn here (not one of the
        // wall's runes), bigger, warm gold with a glow round it, steady (no spring: they ride with the veil). A blow
        // spent: its mark flares and goes.
        private sealed class Mark
        {
            public Transform T;
            public Mesh M;
            public Color[] Colors = new Color[4];
            public float PopAt = -1f, Phase;
        }

        internal static Color MarkColor = new Color(1f, 0.72f, 0.42f);
        private const float MarkHeight = 2.2f, MarkGap = 0.36f, MarkSize = 0.36f, PopSeconds = 0.55f;
        private static Material _markMat;

        private void BuildMarks()
        {
            int max;
            Ward.Shown(_p, out max);
            max = Mathf.Clamp(max, 1, 3);
            for (int i = 0; i < max; i++)
            {
                GameObject go = new GameObject("charge");
                go.transform.SetParent(transform, false);
                Mark m = new Mark();
                m.T = go.transform;
                m.M = Quad(-1);
                m.Phase = i * 1.3f;
                go.AddComponent<MeshFilter>().sharedMesh = m.M;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MarkMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _marks.Add(m);
            }
        }

        // ᛉ with a soft halo, white (coloured in the vertices)
        private static Material MarkMaterial()
        {
            if (_markMat != null) return _markMat;
            const int n = 64;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            Vector2[,] seg =
            {
                { new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.84f) },   // the stem
                { new Vector2(0.5f, 0.5f), new Vector2(0.26f, 0.8f) },    // the arms up
                { new Vector2(0.5f, 0.5f), new Vector2(0.74f, 0.8f) },
            };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 q = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                    float d = 9f;
                    for (int s = 0; s < 3; s++) d = Mathf.Min(d, ToSegment(q, seg[s, 0], seg[s, 1]));
                    float core = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.028f) / 0.022f)); // (Mathf.SmoothStep eases between its first two by the third)
                    float r2 = (q - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                    float halo = Mathf.Exp(-r2 / 0.03f) * 0.22f + Mathf.Exp(-d * d / 0.003f) * 0.3f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, halo))));
                }
            t.Apply(true, true);
            t.hideFlags = HideFlags.HideAndDontSave;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            _markMat = new Material(sh);
            _markMat.name = "RavenWatch ward charges";
            _markMat.mainTexture = t;
            _markMat.hideFlags = HideFlags.HideAndDontSave;
            _markMat.renderQueue = 3110; // over the runes
            return _markMat;
        }

        private static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // a perfect parry of your own: the charge is kept, the marks light up for a moment
        internal void Glint() { _glintAt = Time.time; }

        private void TickMarks(int n, float grow, float fade, Camera cam)
        {
            if (_marks.Count == 0 || _aura == null) return;
            float big = _p != null ? _p.transform.localScale.x : 1f;
            Vector3 center = _aura.position + (Vector3.up * MarkHeight + _aura.forward * (Radius * 0.55f)) * big;
            Vector3 right = _aura.right;
            float glint = Mathf.Clamp01(1f - (Time.time - _glintAt) / 0.6f);
            for (int i = 0; i < _marks.Count; i++)
            {
                Mark m = _marks[i];
                if (i >= n && m.PopAt < 0f) m.PopAt = Time.time; // that blow is spent
                float pop = m.PopAt >= 0f ? Mathf.Clamp01((Time.time - m.PopAt) / PopSeconds) : 0f;
                float x = (i - (_marks.Count - 1) * 0.5f) * MarkGap * big;
                Vector3 pos = center + right * x + Vector3.up * (0.05f * Mathf.Sin(Time.time * 1.6f + m.Phase) + pop * 0.35f);
                float alpha = (0.95f * grow * fade) * (1f - pop) + (pop > 0f && pop < 1f ? (1f - pop) * 0.6f : 0f);
                alpha = Mathf.Clamp01(alpha + glint * 0.3f * (1f - pop));
                Color c = Color.Lerp(MarkColor, Color.white, 0.2f + 0.5f * Mathf.Max(glint, pop > 0f ? 1f - pop : 0f));
                c.a = pop >= 1f ? 0f : alpha;
                for (int k = 0; k < 4; k++) m.Colors[k] = c;
                m.M.colors = m.Colors;
                m.T.position = pos;
                m.T.localScale = Vector3.one * MarkSize * big * Mathf.Lerp(0.4f, 1f, grow) * (1f + pop * 1.4f + glint * 0.15f);
                if (cam != null) m.T.rotation = Quaternion.LookRotation(pos - cam.transform.position);
            }
        }

        // one rune of the 4×4 sheet on a quad (frame −1: the whole picture), its colour in the vertices (the dome's rune material takes them)
        private static Mesh Quad(int frame)
        {
            float u = (frame % 4) / 4f, v = 1f - (frame / 4 + 1) / 4f, s = 0.25f;
            if (frame < 0) { u = 0f; v = 0f; s = 1f; }
            Mesh m = new Mesh();
            m.vertices = new Vector3[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            m.uv = new Vector2[] { new Vector2(u, v), new Vector2(u + s, v), new Vector2(u + s, v + s), new Vector2(u, v + s) };
            m.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            m.colors = new Color[] { Color.clear, Color.clear, Color.clear, Color.clear };
            m.RecalculateBounds();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return m;
        }

        // a blow taken (here, or told by its owner's game)
        internal void Hit(Vector3 from, int left)
        {
            if (_closing >= 0f || _p == null) return;
            _flashAt = Time.time;
            _shown = left;
            float big = _p.transform.localScale.x;
            Vector3 chest = _p.transform.position + (Vector3.up * 1.1f + from * Radius) * big;
            try { if (_p.m_perfectBlockEffect != null) RavenFx.Own(_p.m_perfectBlockEffect, chest, Quaternion.identity, null, 1f, -1); } catch { }
            if (left <= 0) { Close(true, from); return; }
            // the runes nearest the blow are knocked off their springs
            foreach (Rune r in _runes)
            {
                Vector3 d = r.Pos - chest;
                if (d.magnitude < 0.75f) r.Vel += from * 3.5f + Vector3.up * 1.2f + UnityEngine.Random.insideUnitSphere;
            }
        }

        private void Close(bool burst, Vector3 from)
        {
            if (_closing >= 0f) return;
            _closing = Time.time;
            foreach (Rune r in _runes)
            {
                if (burst)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, from) * UnityEngine.Random.Range(-2.5f, 2.5f);
                    r.Vel = from * UnityEngine.Random.Range(3f, 6.5f) + side + Vector3.up * UnityEngine.Random.Range(0.5f, 2.5f);
                }
                else r.Vel = Vector3.up * UnityEngine.Random.Range(0.3f, 0.9f) + UnityEngine.Random.insideUnitSphere * 0.3f;
                r.Loose = -1f;
            }
            if (!burst && _p != null) RavenFx.Play("sfx_raven_flap", _p.transform.position + Vector3.up * 1.5f, Quaternion.identity);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (_p == null || _p.IsDead()) { if (_closing < 0f) Close(false, Vector3.forward); }
            else if (_closing < 0f)
            {
                int max;
                int n = Ward.Shown(_p, out max);
                if (n <= 0) Close(false, _p.transform.forward); // time up, or the last blow told only by the ZDO
                else
                {
                    if (_shown > n) { _flashAt = Time.time; }
                    _shown = n;
                }
            }

            float fade = 1f, grow = Mathf.Clamp01((Time.time - _born) / GrowSeconds);
            int maxNow;
            bool cracked = _closing < 0f && _p != null && Ward.Shown(_p, out maxNow) < maxNow;
            if (_closing >= 0f) fade = 1f - Mathf.Clamp01((Time.time - _closing) / FadeSeconds);
            else if (_p != null)
            {
                float left = Ward.LeftOf(_p);
                if (left < 5f) fade = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * (6f + (5f - left) * 2f)));
                if (cracked) fade *= 0.6f + 0.15f * Mathf.Sin(Time.time * 9f);
            }
            float flash = Mathf.Clamp01(1f - (Time.time - _flashAt) / 0.35f);

            Vector3 basePos = _p != null ? _p.transform.position : transform.position;
            Vector3 fwd = Vector3.forward;
            if (_p != null) { fwd = _p.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward; }
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Camera cam = Utils.GetMainCamera();
            float big = _p != null ? _p.transform.localScale.x : 1f; // (a grown body: «Звір Муніна»)

            foreach (Rune r in _runes)
            {
                float a = r.Angle * Mathf.Deg2Rad;
                Vector3 anchor = basePos + (fwd * Mathf.Cos(a) + right * Mathf.Sin(a)) * (Radius * big)
                    + Vector3.up * ((r.Height + 0.04f * Mathf.Sin(Time.time * 1.7f + r.Phase)) * big);
                if (_closing >= 0f) { r.Vel *= Mathf.Exp(-1.5f * dt); r.Pos += r.Vel * dt; }
                else if (r.Loose >= 0f)
                {
                    r.Loose -= dt;
                    r.Pos += r.Vel * dt;
                    if (r.Loose < 0f) { r.Pos = anchor; r.Vel = Vector3.zero; r.Alpha = 0f; } // back on its place, fading in
                }
                else
                {
                    if ((r.Pos - anchor).sqrMagnitude > 25f) r.Pos = anchor; // a teleport, a portal: no flight across the world
                    r.Vel += (anchor - r.Pos) * Spring * dt;
                    r.Vel *= Mathf.Exp(-Damping * dt);
                    r.Pos += r.Vel * dt;
                    r.Alpha = Mathf.MoveTowards(r.Alpha, 1f, dt * 1.5f);
                    // now and then one comes loose and drifts away, forward and up
                    if (grow >= 1f && UnityEngine.Random.value < 0.12f * dt)
                    {
                        r.Loose = LooseSeconds;
                        r.Vel = (fwd * Mathf.Cos(a) + right * Mathf.Sin(a)) * UnityEngine.Random.Range(0.4f, 0.9f) + Vector3.up * UnityEngine.Random.Range(0.3f, 0.7f);
                    }
                }
                float alpha = r.Alpha * fade * Mathf.Lerp(0.3f, 1f, grow);
                if (r.Loose >= 0f) alpha *= Mathf.Clamp01(r.Loose / LooseSeconds);
                if (cracked && r.Odd) alpha *= 0.35f;
                alpha = Mathf.Clamp01(alpha * 0.85f + flash * 0.6f);
                Color c = Color.Lerp(Ward.Red, Color.white, 0.1f * r.Alpha + flash * 0.5f);
                c.a = alpha;
                for (int i = 0; i < 4; i++) r.Colors[i] = c;
                r.M.colors = r.Colors;
                r.T.position = r.Pos;
                r.T.localScale = Vector3.one * r.Size * big * (1f + flash * 0.3f);
                if (cam != null) r.T.rotation = Quaternion.LookRotation(r.Pos - cam.transform.position) * Quaternion.Euler(0f, 0f, r.Spin + 8f * Mathf.Sin(Time.time + r.Phase));
            }

            TickAura(basePos, grow, fade, flash, cracked);
            int maxMarks;
            TickMarks(_closing >= 0f || _p == null ? 0 : Ward.Shown(_p, out maxMarks), grow, fade, cam);
            if (_light != null)
            {
                _light.transform.position = basePos + (Vector3.up * 1.1f + fwd * Radius) * big;
                _light.intensity = (LightPower * grow * fade + flash * 0.6f) * (_closing >= 0f ? fade : 1f);
            }
            if (_closing >= 0f && Time.time - _closing > FadeSeconds + 0.3f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            foreach (Rune r in _runes) if (r.M != null) Destroy(r.M);
            if (_auraMesh != null) Destroy(_auraMesh);
            foreach (Mark m in _marks) if (m.M != null) Destroy(m.M);
            _marks.Clear();
            _runes.Clear();
            if (_p != null)
            {
                WardLook cur;
                if (Looks.TryGetValue(_p, out cur) && ReferenceEquals(cur, this)) Looks.Remove(_p);
            }
            else
            {
                List<Player> gone = new List<Player>();
                foreach (KeyValuePair<Player, WardLook> kv in Looks) if (kv.Key == null || ReferenceEquals(kv.Value, this)) gone.Add(kv.Key);
                foreach (Player g in gone) Looks.Remove(g);
            }
        }
    }
}
#endif

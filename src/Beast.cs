#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // «Звір Муніна», Munin's great gift: he wakes the beast in you. You grow — smoothly, over a second and a half —
    // to Size times yourself, a real body (the game's own scale: it is hit as big, and it may get stuck where a big
    // body would), strike +DamagePercent%, armour +ArmorPercent%, cannot be staggered or pushed back; a dark red smoke
    // rises off your shoulders and you stand hunched (the player animation system, tag "beast"). Then you shrink back as
    // smoothly, and the beast's price comes: TiredSeconds of weariness, stamina coming back at TiredRegen of its pace.
    // Not in dungeons (and it ends going into one); not with the fury, not with Odin's wings. Munin keeps his own size
    // on the big shoulder. The melee reach grows with you (the blow is still struck at a man's height, where the
    // enemies are, and follows where you look).
    // The size is every game's own: the owner writes from-to-when on the player's ZDO and everyone eases it alike.
    internal static class Beast
    {
        internal static float Seconds = 30f, SecondsTop = 40f;
        internal static float Size = 2f, GrowSeconds = 1.6f, ShrinkSeconds = 1.6f;
        internal static float DamagePercent = 50f, ArmorPercent = 50f;
        internal static float KickPush = 1.5f; // the kick (no weapon, the wheel button) throws this many times as far
        internal static float SpeedPercent = 50f; // a body twice as big moves its legs as slowly: faster, so the steps fit the ground
        internal static float JumpHeight = 2f;    // the big body jumps this many times as high (the push: its square root)
        internal const string WakeClip = "beast_wake";
        private const string LogName = "Звір Муніна";
        internal static float TiredSeconds = 30f, TiredRegen = 0.5f;
        // the waking roar: the goblin brute's taunt, played forward at 0.6 of its pace (lower and longer)
        internal const string Sound = "sfx_goblinbrute_taunt";
        internal static float SoundSpeed = 0.6f;
        internal static float Duration() { return Trust.RoundTime(Trust.LerpFrom(true, RavenAbilities.UnlockLevel(RavenAbilities.Find("mberserk")), Seconds, SecondsTop) * GiftUp.Power("mberserk")); } // (from the gift's own first level)

        internal const string Tag = "beast";
        private const string FromKey = "RavenWatch_BeastFrom", ToKey = "RavenWatch_BeastTo", AtKey = "RavenWatch_BeastAt";
        private const string RpcUp = "RavenWatch_BeastUp", RpcDown = "RavenWatch_BeastDown";
        internal const string EffectName = "RavenWatch_Beast", TiredName = "RavenWatch_BeastTired";
        internal static readonly int Hash = EffectName.GetStableHashCode(), TiredHash = TiredName.GetStableHashCode();

        private static bool _on;
        private static float _nextFlourish;
        // now and then, over the state the rules pick (stand, walk, run, jump, strike): a roar, a head shake, the shoulders
        // rolled, the neck cracked, a huff, the arms flexed; never twice in a row, never in a swing
        internal static float FlourishMin = 6f, FlourishMax = 13f;
        private static readonly string[] Flourishes = { "beast_roar", "beast_shake", "beast_shoulders", "beast_crack", "beast_huff", "beast_flex" };
        private static int _lastFlourish = -1;
        private static double _until;
        private static SE_Stats _template, _tiredTemplate;
        private static double Now { get { return WorldTime.Now; } }

        internal static bool On() { return _on; }
        // any player's beast awake (growing or big), as this game sees it
        internal static bool Awake(Player p) { ZDO z = NetFlag.Zdo(p, false); return z != null && z.GetFloat(ToKey, 1f) > 1.01f; }
        internal static float Left() { return _on ? Mathf.Max(0f, (float)(_until - Now)) : 0f; }

        internal static void Register()
        {
            RavenFx.Register(RpcUp, Wake);
            RavenFx.Register(RpcDown, Settle);
        }

        // why the beast cannot wake here, or null
        internal static string Refused(Player p)
        {
            if (p.InInterior() || Senses.Underground) return Lang.T("Під землею звіра не будять — тут йому тісно");
            if (p.IsAttached() || p.IsRiding()) return Lang.T("Спершу злізь");
            if (Fury.On(p)) return Lang.T("Лють ще палає — звір прокинеться, коли вона згасне");
            if (OdinWings.Flying()) return Lang.T("З крилами Одіна звіра не розбудиш");
            return null;
        }

        // a gift out of place with the beast awake (null: it may be used)
        internal static string Blocks(RavenAbility a)
        {
            if (a == null || !_on) return null;
            if (a.Id == "fury") return Lang.T("Лють: звір і так лютує");
            if (a.Id == "hflight") return Lang.T("Крила Одіна: звіра Хугін не підніме");
            return null;
        }

        internal static void Start(Player p)
        {
            _on = true;
            _until = Now + Duration();
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Звір Муніна");
            se.m_tooltip = Facets.On("mberserk") // (III: no weariness after)
                ? Lang.F("+{0}% шкоди, +{1}% броні, +{2}% швидкості, без оглушення й відкидання. Втоми потім не буде", Mathf.RoundToInt(DamagePercent), Mathf.RoundToInt(ArmorPercent), Mathf.RoundToInt(SpeedPercent))
                : Lang.F("+{0}% шкоди, +{1}% броні, +{2}% швидкості, без оглушення й відкидання. Потім — втома: {3} с витривалість відновлюється вдвічі повільніше", Mathf.RoundToInt(DamagePercent), Mathf.RoundToInt(ArmorPercent), Mathf.RoundToInt(SpeedPercent), Mathf.RoundToInt(TiredSeconds));
            se.m_icon = AbilityIcons.ForEffect("mberserk", true);
            se.m_ttl = Duration();
            se.m_modifyAttackSkill = Skills.SkillType.All;
            se.m_damageModifier = 1f + DamagePercent / 100f;
            se.m_armorMultiplier = ArmorPercent / 100f;
            se.m_speedModifier = SpeedPercent / 100f;
            se.m_staggerModifier = -1f; // (SEMan.ModifyStagger: what comes in, taken off again)
            p.GetSEMan().RemoveStatusEffect(TiredHash, true); // a new beast: the old weariness forgotten
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            Grow(p, Size, GrowSeconds);
            PlayerAnims.Tag(p, Tag, true);
            _nextFlourish = Time.time + 6f; // (the waking roar first)
            _nextRage = Time.time + UnityEngine.Random.Range(5f, 7f); // (after the waking line)
            _reactAt = Time.time + 4f;
            _lastHp = p.GetHealth();
            if (RavenSkills.Animations) PlayerAnims.Play(p, 2, WakeClip, 0.15f, 1f);
            if (!PAnimLog.On) PAnimLog.Begin(LogName); // (a recording already running, by hand or a flight's tail, is left alone)
            RavenFx.Broadcast(RpcUp, p, Wake);
        }

        // time up, pressed again, or into a dungeon: back to yourself, and the price
        internal static void End(Player p, bool fast)
        {
            if (!_on) return;
            _on = false;
            if (p == null) { EndLog(Lang.T("гравця нема")); return; }
            p.GetSEMan().RemoveStatusEffect(Hash, true);
            Grow(p, 1f, fast ? 0.4f : ShrinkSeconds);
            PlayerAnims.Tag(p, Tag, false);
            StopFlourish(p, 0.3f);
            EndLog(fast ? Lang.T("звір обірвався") : Lang.T("звір пішов"));
            if (p.IsDead()) return;
            RavenFx.Broadcast(RpcDown, p, Settle);
            if (Facets.On("mberserk")) return; // («Звір Муніна» III: no weariness after)
            SE_Stats t = RavenFx.Template(ref _tiredTemplate, TiredName);
            t.m_name = Lang.W("Втома звіра");
            t.m_tooltip = Lang.T("Звір пішов і забрав сили: витривалість відновлюється вдвічі повільніше");
            t.m_icon = AbilityIcons.ForEffect("mberserk", true);
            t.m_ttl = TiredSeconds;
            t.m_staminaRegenMultiplier = TiredRegen;
            p.GetSEMan().AddStatusEffect(t, true, 0, 0f, 0);
            RavenSkills.GiftSay(true, "beast_end", Lang.T("Все, звір виліз. Тепер ти — ганчірка. Відсапуйся."));
        }

        // from the skills' Update (the local player; the sizes and the looks: every player)
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (_on && p != null)
            {
                if (p.IsDead()) End(p, true);
                else if (p.InInterior() || Senses.Underground) { End(p, true); RavenMsg.Center(p, Lang.T("Під землею звір не вміщається — і ти знову собою")); }
                else if (Now >= _until) End(p, false);
                else { Flourish(p); Rage(p); }
            }
            BeastLook.Tick();
        }

        // ---------------------------------------------------------------- Munin gone rabid (the beast's whole time)

        // While the beast is awake Munin is mad as never before: he yells on his own every RageMin–RageMax seconds
        // (the gift's own voice, not held back by the quiet between the ravens' lines), howls over every kill, screams
        // when you are hurt, roars at some of your blows — and whatever else he says then comes out louder and with a
        // curse on the end.
        internal static float RageMin = 7f, RageMax = 12f;   // seconds between his own yells
        internal static float ReactGap = 3f;                // at least this after a line ends before a reaction
        internal static float HitChance = 0.3f;             // a blow of yours he roars at
        internal static float HurtShare = 0.06f;            // a hit taking this much of your health makes him scream
        private static float _nextRage, _reactAt, _lastHp;
        private static bool _shouting;

        private static void Rage(Player p)
        {
            float hp = p.GetHealth();
            float lost = _lastHp - hp;
            _lastHp = hp;
            if (lost >= p.GetMaxHealth() * HurtShare && Time.time >= _reactAt) { Shout(p, "beast_hurt", Lang.T("ВОНО ТЕБЕ ВДАРИЛО?! РОЗІРВИ ЙОГО, КУРВА!")); return; }
            if (Time.time >= _nextRage && Time.time >= _reactAt && !p.IsDead()) Shout(p, "beast_rage", Lang.T("РВИ! ТОПЧИ! ЇБАШ ЇХ УСІХ!"));
        }

        internal static void OnKill(bool boss)
        {
            Player p = Player.m_localPlayer;
            if (!_on || p == null || Time.time < _reactAt - (boss ? 99f : 0f)) return; // (a boss's death always gets its howl)
            Shout(p, boss ? "beast_boss" : "beast_kill", Lang.T("ГОТОВИЙ! ДАВАЙ НАСТУПНОГО, СУКА!"));
        }

        internal static void OnHit()
        {
            Player p = Player.m_localPlayer;
            if (!_on || p == null || Time.time < _reactAt || UnityEngine.Random.value > HitChance) return;
            Shout(p, "beast_hit", Lang.T("ЩЕ! ГАТИ!"));
        }

        private static void Shout(Player p, string tag, string fallback)
        {
            _shouting = true;
            try { RavenSkills.GiftSay(true, tag, fallback); }
            finally { _shouting = false; }
            float said = 4f + fallback.Length / 25f; // (about how long it shows)
            _reactAt = Time.time + said * 0.6f + ReactGap;
            _nextRage = Time.time + UnityEngine.Random.Range(RageMin, RageMax);
        }

        // anything else Munin says while the beast is awake: louder, and a curse on the end
        internal static string Rabid(Player p, string line)
        {
            if (!_on || _shouting || string.IsNullOrEmpty(line) || p != Player.m_localPlayer) return line;
            string t = line.TrimEnd();
            if (t.EndsWith(".")) t = t.Substring(0, t.Length - 1) + "!";
            string curse = Plugin.Instance != null ? Plugin.Instance.ComposeTagged(p, true, "beast_swear") : null;
            if (!string.IsNullOrEmpty(curse) && t.Length < 110 && Gutter.Count(t) < 2) t = t + " " + Grammar.Fix(curse, p); // (not a third curse on a line that has two)
            if (UnityEngine.Random.value < 0.5f) t = t.ToUpperInvariant();
            return t;
        }

        private static void Flourish(Player p)
        {
            PlayerAnimator a = PlayerAnimator.Of(p);
            bool mine = a != null && a.Playing(2) && a.ClipIn(2) != null && a.ClipIn(2).StartsWith("beast_", StringComparison.Ordinal);
            if (mine && (p.InAttack() || p.IsBlocking() || p.InDodge())) { PAnimLog.Event(Lang.T("звір: рух обірвано (удар/блок/перекид)")); StopFlourish(p, 0.15f); return; } // the swing is the game's
            if (Time.time < _nextFlourish) return;
            if (a != null && a.Playing(2)) return;
            if (p.InAttack() || p.IsBlocking() || p.InDodge() || p.InEmote() || !p.IsOnGround() || p.IsSwimming()) { _nextFlourish = Time.time + 1f; return; }
            _nextFlourish = Time.time + UnityEngine.Random.Range(FlourishMin, FlourishMax);
            int i = UnityEngine.Random.Range(0, Flourishes.Length);
            if (i == _lastFlourish) i = (i + 1 + UnityEngine.Random.Range(0, Flourishes.Length - 1)) % Flourishes.Length;
            _lastFlourish = i;
            PAnimLog.Event(Lang.F("звір: рух «{0}»", Flourishes[i]));
            PlayerAnims.Play(p, 2, Flourishes[i], 0.2f, UnityEngine.Random.Range(0.9f, 1.1f));
        }

        private static void StopFlourish(Player p, float fade)
        {
            PlayerAnimator a = PlayerAnimator.Of(p);
            if (a != null && a.Playing(2) && a.ClipIn(2) != null && a.ClipIn(2).StartsWith("beast_", StringComparison.Ordinal)) PlayerAnims.Stop(p, 2, fade);
        }

        private static void EndLog(string why) { if (PAnimLog.What == LogName) PAnimLog.End(why); }

        internal static void Reset()
        {
            if (_on) EndLog(Lang.T("вихід"));
            _on = false;
            BeastLook.CloseAll();
        }

        // ---------------------------------------------------------------- the size (every game alike)

        private static void Grow(Player p, float to, float seconds)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            float from = SizeOf(p);
            zdo.Set(FromKey, from);
            zdo.Set(ToKey, to);
            zdo.Set(AtKey, (long)(Now * 1000.0)); // (milliseconds: a float of world seconds grows coarse in an old world, and the growth would step)
            zdo.Set(ToKey + "S", seconds);
        }

        // a player's size now: eased from where it was to where it goes (smoothstep, no jolts)
        internal static float SizeOf(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, false);
            if (zdo == null) return 1f;
            float to = zdo.GetFloat(ToKey, 1f);
            float from = zdo.GetFloat(FromKey, 1f);
            if (Mathf.Abs(to - from) < 0.001f) return to;
            float seconds = Mathf.Max(0.05f, zdo.GetFloat(ToKey + "S", GrowSeconds));
            float u = Mathf.Clamp01((float)(Now - zdo.GetLong(AtKey, 0L) / 1000.0) / seconds);
            u = u * u * (3f - 2f * u);
            return Mathf.Lerp(from, to, u);
        }

        // ---------------------------------------------------------------- the effects (every game)

        // the waking: Munin's feathers and a dark red smoke burst off you, and the ground shakes a little
        private static void Wake(Player p)
        {
            if (p == null) return;
            Vector3 feet = p.transform.position;
            Feathers.Gust(p);
            Feathers.Ring(feet, 90, 3.5f);
            Feathers.Dust(feet, 18, 3f);
            for (int i = 0; i < 3; i++) Feathers.Shed(feet + Vector3.up * (1.2f + i * 0.5f), true, 40);
            BeastLook.Aura(p);
            RavenFx.Play("sfx_raven_flap", feet + Vector3.up * 1.5f, Quaternion.identity);
            SoundBack.PlayForward(Sound, p.transform, feet + Vector3.up * 1.5f, 1f / SoundSpeed); // (follows you, whole to its end)
        }

        // back to yourself: a white haze round you and a few feathers
        private static void Settle(Player p)
        {
            if (p == null) return;
            // a white haze bursting out round the whole body (not under the feet), as the beast leaves it
            Material mat = Feathers.Smoke();
            Vector3 feet = p.transform.position;
            if (mat != null)
            {
                BeastLook.Puffs(mat, feet + Vector3.up * 1.3f, ParticleSystemShapeType.Sphere, 1f, 40, 0.9f, 1.6f, 1.6f, 2.3f, 1.5f, new Color(0.86f, 0.86f, 0.9f, 0.45f));
                BeastLook.Puffs(mat, feet + Vector3.up * 2.5f, ParticleSystemShapeType.Sphere, 0.9f, 25, 0.8f, 1.4f, 1.3f, 2.1f, 1.5f, new Color(0.9f, 0.9f, 0.93f, 0.4f));
            }
            Feathers.Shed(feet + Vector3.up * 1.4f, true, 25);
        }
    }

    // Every game: each player's size, put on his transform each frame (the game does not carry it between games),
    // the shoulder ravens kept at their own size on the grown shoulders, and the smoke off a beast's shoulders.
    [DefaultExecutionOrder(31000)] // after the shoulder ravens are placed for the frame
    internal class BeastLook : MonoBehaviour
    {
        private static BeastLook _inst;
        private static readonly Dictionary<Player, ParticleSystem> Smoke = new Dictionary<Player, ParticleSystem>();
        private static readonly Dictionary<GameObject, Vector3> RavenSize = new Dictionary<GameObject, Vector3>(); // a shoulder raven's own world size
        private static readonly Dictionary<Player, float> Scaled = new Dictionary<Player, float>(); // players we have scaled, and when they came back to 1
        private static float _next;

        internal static void Tick()
        {
            if (_inst == null) { GameObject go = new GameObject("RavenWatch_BeastLook"); DontDestroyOnLoad(go); _inst = go.AddComponent<BeastLook>(); }
        }

        internal static void CloseAll()
        {
            foreach (ParticleSystem ps in Smoke.Values) if (ps != null) Destroy(ps.transform.parent != null ? ps.transform.parent.gameObject : ps.gameObject);
            Smoke.Clear();
            foreach (BeastEyes e in Eyes.Values) if (e != null) Destroy(e.gameObject);
            Eyes.Clear();
            RavenSize.Clear();
            Scaled.Clear();
        }

        private static readonly List<Player> GonePlayers = new List<Player>();      // (kept: no new lists every second)
        private static readonly List<GameObject> GoneBirds = new List<GameObject>();

        private void LateUpdate()
        {
            List<Player> all = Player.GetAllPlayers();
            foreach (Player pl in all)
            {
                if (pl == null) continue;
                float s = Beast.SizeOf(pl);
                float since;
                bool known = Scaled.TryGetValue(pl, out since);
                if (Mathf.Abs(s - 1f) < 0.001f && (!known || Time.time - since > 3f)) { if (known) { Scaled.Remove(pl); ShoulderRavens(pl, 1f); } continue; }
                Scaled[pl] = Mathf.Abs(s - 1f) < 0.001f ? (known && since > 0f ? since : Time.time) : -1f;
                if (Mathf.Abs(pl.transform.localScale.x - s) > 0.0005f) pl.transform.localScale = Vector3.one * s;
                ShoulderRavens(pl, s);
                SmokeFor(pl, s > 1.05f && Beast.Awake(pl));
                EyesFor(pl, Beast.Awake(pl));
            }
            if (Time.time >= _next)
            {
                _next = Time.time + 1f;
                GonePlayers.Clear();
                foreach (Player k in Scaled.Keys) if (k == null) GonePlayers.Add(k);
                foreach (Player k in GonePlayers) Scaled.Remove(k);
                GoneBirds.Clear();
                foreach (GameObject g in RavenSize.Keys) if (g == null) GoneBirds.Add(g);
                foreach (GameObject g in GoneBirds) RavenSize.Remove(g);
                GonePlayers.Clear();
                foreach (KeyValuePair<Player, ParticleSystem> kv in Smoke) if (kv.Key == null) GonePlayers.Add(kv.Key);
                foreach (Player k in GonePlayers) { End(Smoke[k]); Smoke.Remove(k); }
                GonePlayers.Clear();
                foreach (KeyValuePair<Player, BeastEyes> kv in Eyes) if (kv.Key == null || kv.Value == null) GonePlayers.Add(kv.Key);
                foreach (Player k in GonePlayers) { if (Eyes[k] != null) Destroy(Eyes[k].gameObject); Eyes.Remove(k); }
            }
        }

        private static readonly Dictionary<Player, BeastEyes> Eyes = new Dictionary<Player, BeastEyes>();

        // the beast's eyes burn red (every game, on every beast)
        private static void EyesFor(Player pl, bool on)
        {
            BeastEyes e;
            Eyes.TryGetValue(pl, out e);
            if (!on) { if (e != null) { e.Close(); Eyes.Remove(pl); } return; }
            if (e != null) return;
            e = BeastEyes.Make(pl);
            if (e != null) Eyes[pl] = e;
        }

        // the ravens sit on bones of the grown body: their own world size is put back each frame
        private static void ShoulderRavens(Player pl, float s)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject m = Shoulder.ModelOf(pl, i == 1);
                if (m == null) continue;
                Transform t = m.transform, parent = t.parent;
                Vector3 want;
                if (!RavenSize.TryGetValue(m, out want))
                {
                    want = t.lossyScale / Mathf.Max(0.01f, s); // first seen on a grown body: its size as it would be on yours
                    RavenSize[m] = want;
                }
                if (parent == null) continue; // flying: its own size in the world already
                Vector3 ps = parent.lossyScale;
                t.localScale = new Vector3(want.x / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), want.y / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), want.z / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));
                if (Mathf.Abs(s - 1f) < 0.001f) RavenSize.Remove(m);
            }
        }

        // The waking's red aura: a burst of red haze off the whole body, a ring of it rolling out over the ground, and a
        // red flash of light fading in a second. A few dozen big puffs, gone in about three seconds.
        internal static void Aura(Player p)
        {
            Material mat = Feathers.Smoke();
            if (p == null || mat == null) return;
            Vector3 feet = p.transform.position;
            Puffs(mat, feet + Vector3.up * 1.6f, ParticleSystemShapeType.Sphere, 0.8f, 24, 1.7f, 3.1f, 3.2f, 2.8f, 1.2f, new Color(0.95f, 0.08f, 0.06f, 0.5f));
            Puffs(mat, feet + Vector3.up * 0.25f, ParticleSystemShapeType.Circle, 0.6f, 22, 1.2f, 1.8f, 6f, 2.8f, 1.2f, new Color(0.75f, 0.05f, 0.04f, 0.45f));
            GameObject lg = new GameObject("RavenWatch_BeastFlash");
            lg.transform.position = feet + Vector3.up * 1.6f;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.12f, 0.06f);
            l.range = 7f;
            l.shadows = LightShadows.None;
            lg.AddComponent<FadeLight>().Begin(l, 3.5f, 1.2f);
        }

        // a burst of haze (red at the waking, white at the end), out from a shape, slowing as it thins
        internal static void Puffs(Material mat, Vector3 at, ParticleSystemShapeType shape, float radius, int count, float sizeMin, float sizeMax, float speed, float life, float dragK, Color color)
        {
            GameObject go = new GameObject("RavenWatch_BeastAura");
            go.transform.position = at;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = count;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = shape;
            sh.radius = radius; sh.radiusThickness = shape == ParticleSystemShapeType.Circle ? 0f : 1f;
            if (shape == ParticleSystemShapeType.Circle) sh.rotation = new Vector3(90f, 0f, 0f); // (lying on the ground)
            ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = dragK;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2f)));
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.maxParticleSize = 0.6f;
            ps.Play();
            Destroy(go, life + 0.5f);
        }

        // a light that fades out and goes
        internal class FadeLight : MonoBehaviour
        {
            private Light _l;
            private float _from, _seconds, _at;
            internal void Begin(Light l, float from, float seconds) { _l = l; _from = from; _seconds = seconds; _at = Time.time; l.intensity = from; }
            private void Update()
            {
                float u = Mathf.Clamp01((Time.time - _at) / Mathf.Max(0.01f, _seconds));
                if (_l != null) _l.intensity = _from * (1f - u) * (1f - u);
                if (u >= 1f) Destroy(gameObject);
            }
        }

        // a dark red smoke rising off the beast's shoulders
        private static void SmokeFor(Player pl, bool on)
        {
            ParticleSystem ps;
            Smoke.TryGetValue(pl, out ps);
            if (!on) { if (ps != null) { End(ps); Smoke.Remove(pl); } return; }
            if (ps != null) return;
            Material mat = Feathers.Smoke();
            if (mat == null) return;
            GameObject holder = new GameObject("RavenWatch_BeastSmoke");
            holder.AddComponent<Feathers.Follow>().Target = pl.transform;
            GameObject go = new GameObject("smoke");
            go.transform.SetParent(holder.transform, false);
            go.transform.localPosition = Vector3.up * 2.9f;
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; main.duration = 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // few big soft puffs overlapping (a thick haze, cheap to draw), not many small ones
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f); // (out from the sphere: spreads a little round you)
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.05f, 0.04f, 0.42f), new Color(0.3f, 0.02f, 0.02f, 0.36f));
            main.gravityModifier = -0.04f;
            main.maxParticles = 60;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 20f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.7f;
            // one puff of the 2×2 sheet per particle (the whole sheet on one read as four small dots)
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2; sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.99f);
            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.8f)));
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.maxParticleSize = 0.6f; // (a puff never fills the whole screen when the camera is in it)
            ps.Play();
            Smoke[pl] = ps;
        }

        private static void End(ParticleSystem ps)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = false;
            Destroy(ps.transform.parent != null ? ps.transform.parent.gameObject : ps.gameObject, 2.5f);
        }
    }

    // The beast's eyes: two small glowing eyes set on the face itself, part of the body like a helmet piece — on the
    // head bone (they follow every turn of it), hidden by whatever is in front of them like the rest of the model, the
    // same from any side the camera looks. Bright past white so the game's glow takes them; a faint red light on the
    // face. Where the face is gets measured once from the body's own mesh at the waking (the front of the face at eye
    // height), so they sit on it, not inside the head. They flicker a little, fade in with the waking and out after.
    internal class BeastEyes : MonoBehaviour
    {
        // where the eyes sit from the head bone, at a man's size (metres; the body's axes when it wakes): up, apart, and
        // ahead — used only when the face cannot be measured
        internal static float Up = 0.115f, Ahead = 0.12f, Apart = 0.04f;
        internal static float Depth = 0f; // how far behind the face's front the eye sits (the player's face is nearly flat there: its eyes are painted on, so 0)
        internal static float Power = 1f; // the brightness (1: as made)
        private static readonly Color Glow = new Color(3.2f, 0.45f, 0.22f);
        private static readonly Color LightColor = new Color(1f, 0.14f, 0.07f);
        private const float FadeIn = 0.8f, FadeOut = 0.5f;
        private static readonly Vector3 EyeSize = new Vector3(0.024f, 0.013f, 0.01f); // wide, low, flat (at a man's size)
        private const float Sunk = -0.002f; // the eye's middle this far behind the face's surface (below 0: out in front of it, so most of it shows)

        private static Mesh _mesh;
        private static Material _mat;
        private static MaterialPropertyBlock _block;
        private static bool _logged;
        private Player _p;
        private Transform _head;
        private readonly Renderer[] _r = new Renderer[2];
        private Light _light;
        private float _born, _closing = -1f, _seed;

        internal static BeastEyes Make(Player p)
        {
            if (p == null || Mat() == null || EyeMesh() == null) return null;
            Animator an = p.GetComponentInChildren<Animator>();
            Transform head = an != null ? Utils.FindChild(an.transform, "Head", Utils.IterativeSearchType.DepthFirst) : null;
            if (head == null) return null;
            Transform b = p.transform;
            float s = b.localScale.x;
            // the face's own way (from the head bone, whatever way the head is turned as it wakes), else the body's
            Vector3 fw, fu, fr;
            FaceAxes(p, head, out fw, out fu, out fr);
            // the face's front at each eye (in the face's frame from the head bone, at a man's size), or the guess
            float[] ahead = { Ahead, Ahead };
            FaceFront(p, head, s, ahead, fw, fu, fr);
            GameObject root = new GameObject("RavenWatch_BeastEyes");
            root.transform.SetParent(head, false);
            BeastEyes e = root.AddComponent<BeastEyes>();
            e._p = p;
            e._head = head;
            e._born = Time.time;
            e._seed = UnityEngine.Random.Range(0f, 10f);
            Quaternion face = Quaternion.LookRotation(fw, fu);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                GameObject eye = new GameObject(i == 0 ? "left" : "right");
                eye.transform.SetParent(root.transform, false);
                eye.transform.position = head.position + (fu * Up + fw * (ahead[i] - Depth - Sunk) + fr * (side * Apart)) * s;
                eye.transform.rotation = face;
                Vector3 ls = head.lossyScale; // (the bone's own scale, whatever the model has): the eye at its size in the world
                Vector3 want = EyeSize * s;
                eye.transform.localScale = new Vector3(want.x / Mathf.Max(1e-5f, Mathf.Abs(ls.x)), want.y / Mathf.Max(1e-5f, Mathf.Abs(ls.y)), want.z / Mathf.Max(1e-5f, Mathf.Abs(ls.z)));
                eye.AddComponent<MeshFilter>().sharedMesh = _mesh;
                MeshRenderer mr = eye.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false; // (shown from its first LateUpdate, coloured: else one frame in the material's plain white)
                e._r[i] = mr;
            }
            GameObject lg = new GameObject("light");
            lg.transform.SetParent(root.transform, false);
            lg.transform.position = head.position + (fu * Up + fw * (Mathf.Max(ahead[0], ahead[1]) + 0.1f)) * s;
            e._light = lg.AddComponent<Light>();
            e._light.type = LightType.Point;
            e._light.color = LightColor;
            e._light.shadows = LightShadows.None;
            e._light.intensity = 0f;
            return e;
        }

        // The face's forward, up and right in the world now. The body's axes would do only with the head straight: as
        // the beast wakes the head may be turned or bowed (it was: the eyes sat off to one side, one on the hair). The
        // head bone's bind pose says how the face lay in it when the model stood straight (the body's way then), so the
        // face's axes follow the head as it is. No bind pose to read: the body's axes.
        private static void FaceAxes(Player p, Transform head, out Vector3 fw, out Vector3 fu, out Vector3 fr)
        {
            Transform b = p.transform;
            fw = b.forward; fu = b.up; fr = b.right;
            try
            {
                foreach (SkinnedMeshRenderer r in p.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (r == null || r.sharedMesh == null || r.bones == null) continue;
                    Transform[] bones = r.bones;
                    Matrix4x4[] binds = r.sharedMesh.bindposes;
                    for (int i = 0; i < bones.Length && i < binds.Length; i++)
                    {
                        if (bones[i] != head) continue;
                        // the body's axes in the mesh's own frame (the renderer itself is not animated), then into the head's
                        Vector3 mf = r.transform.InverseTransformDirection(b.forward), mu = r.transform.InverseTransformDirection(b.up);
                        Vector3 hf = binds[i].MultiplyVector(mf), hu = binds[i].MultiplyVector(mu);
                        if (hf.sqrMagnitude < 1e-8f || hu.sqrMagnitude < 1e-8f) return;
                        Vector3 wf = head.TransformDirection(hf).normalized, wu = head.TransformDirection(hu).normalized;
                        wu = Vector3.ProjectOnPlane(wu, wf).normalized;
                        if (wu.sqrMagnitude < 0.5f) return;
                        fw = wf; fu = wu; fr = Vector3.Cross(wu, wf); // (left-handed: up × forward = right)
                        if (!_loggedAxes && Plugin.Instance != null)
                        {
                            _loggedAxes = true;
                            Plugin.Instance.Log("Beast eyes: face turned " + Mathf.RoundToInt(Vector3.SignedAngle(b.forward, fw, b.up)) + "° from the body, bowed " + Mathf.RoundToInt(Vector3.Angle(b.up, fu)) + "°");
                        }
                        return;
                    }
                }
            }
            catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Beast eyes: face axes not read: " + ex.Message); }
        }
        private static bool _loggedAxes;

        // The front of the face at each eye, from the body mesh as it stands now: of the body's points at eye height and
        // over each eye, the one furthest ahead (along the face's own axes). Written into ahead[] (left, right); left alone
        // where nothing is found.
        private static void FaceFront(Player p, Transform head, float s, float[] ahead, Vector3 fw, Vector3 fu, Vector3 fr)
        {
            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer r in p.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r == null || r.sharedMesh == null) continue;
                if (r.name == "body") { body = r; break; }
                if (body == null || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount) body = r;
            }
            if (body == null) return;
            Mesh baked = new Mesh();
            try
            {
                body.BakeMesh(baked);
                Vector3[] v = baked.vertices;
                if (v.Length == 0) return;
                // the bake is in the renderer's frame with or without its scale (by Unity version): the one whose
                // box fits the renderer's own bounds in the world
                Matrix4x4 full = body.transform.localToWorldMatrix, plain = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                Matrix4x4 m = Fit(v, full, body.bounds) <= Fit(v, plain, body.bounds) ? full : plain;
                Vector3 h = head.position;
                float[] best = { -1f, -1f };
                int[] found = new int[2];
                float nose = -1f;
                float[] prof = new float[23]; // the face's front over the eyes by height (−0.02 .. 0.2 m), for the log
                for (int q = 0; q < prof.Length; q++) prof[q] = -1f;
                for (int k = 0; k < v.Length; k++)
                {
                    Vector3 d = m.MultiplyPoint3x4(v[k]) - h;
                    float x = Vector3.Dot(d, fr) / s, y = Vector3.Dot(d, fu) / s, z = Vector3.Dot(d, fw) / s;
                    if (z < -0.05f || z > 0.35f) continue;
                    if (Mathf.Abs(x) < 0.01f && y > Up - 0.06f && y < Up + 0.02f && z > nose) nose = z;
                    if (Mathf.Abs(Mathf.Abs(x) - Apart) < 0.014f) { int bin = Mathf.RoundToInt((y + 0.02f) / 0.01f); if (bin >= 0 && bin < prof.Length && z > prof[bin]) prof[bin] = z; }
                    if (Mathf.Abs(y - Up) > 0.02f) continue;
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        if (Mathf.Abs(x - side * Apart) > 0.014f) continue;
                        found[i]++;
                        if (z > best[i]) best[i] = z;
                    }
                }
                for (int i = 0; i < 2; i++) if (found[i] > 0) ahead[i] = best[i];
                if (!_logged && Plugin.Instance != null)
                {
                    _logged = true;
                    Plugin.Instance.Log("Beast eyes: face front " + best[0].ToString("0.000") + "/" + best[1].ToString("0.000") + " m (points " + found[0] + "/" + found[1] + "), nose " + nose.ToString("0.000") + " m, from body '" + body.name + "' " + v.Length + " points");
                    System.Text.StringBuilder sb = new System.Text.StringBuilder("Beast eyes: face front over the eyes by height (up: ahead):");
                    for (int q = 0; q < prof.Length; q++) if (prof[q] > -1f) sb.Append(" ").Append((q * 0.01f - 0.02f).ToString("0.00")).Append(":").Append(prof[q].ToString("0.000"));
                    Plugin.Instance.Log(sb.ToString());
                }
            }
            catch (Exception ex) { if (Plugin.Instance != null) Plugin.Instance.Log("Beast eyes: face not measured: " + ex.Message); }
            finally { Destroy(baked); }
        }

        // how far a set of points under a matrix is from filling a box (size difference)
        private static float Fit(Vector3[] v, Matrix4x4 m, Bounds want)
        {
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int k = 0; k < v.Length; k += 7) { Vector3 w = m.MultiplyPoint3x4(v[k]); lo = Vector3.Min(lo, w); hi = Vector3.Max(hi, w); }
            return (hi - lo - want.size).sqrMagnitude;
        }

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            Shader sh = Shader.Find("Sprites/Default"); // unlit: the colour as given, past white for the glow
            if (sh == null) return null;
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            _mat.renderQueue = 2460; // with the solid things (after the body): hidden by what is in front, like the model
            return _mat;
        }

        // a small sphere with white vertex colours (the shader takes them)
        private static Mesh EyeMesh()
        {
            if (_mesh != null) return _mesh;
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = tmp.GetComponent<MeshFilter>().sharedMesh;
            _mesh = UnityEngine.Object.Instantiate(src);
            DestroyImmediate(tmp); // (not at the frame end: it would show a frame as a white ball)
            Color[] c = new Color[_mesh.vertexCount];
            for (int i = 0; i < c.Length; i++) c[i] = Color.white;
            _mesh.colors = c;
            _mesh.hideFlags = HideFlags.HideAndDontSave;
            return _mesh;
        }

        internal void Close() { if (_closing < 0f) _closing = Time.time; }

        private void LateUpdate()
        {
            if (_p == null || _head == null) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01((Time.time - _born) / FadeIn);
            if (_closing >= 0f)
            {
                fade *= 1f - Mathf.Clamp01((Time.time - _closing) / FadeOut);
                if (Time.time - _closing >= FadeOut) { Destroy(gameObject); return; }
            }
            // a living burn: a slow breath and a quick small flicker
            float flick = 0.88f + 0.08f * Mathf.Sin(Time.time * 1.9f + _seed) + 0.04f * Mathf.Sin(Time.time * 13f + _seed * 3f);
            float a = fade * flick * Power;
            if (_block == null) _block = new MaterialPropertyBlock();
            // dark red when faint (fading in or out), burning when full
            Color c = Color.Lerp(new Color(0.25f, 0.02f, 0.02f), Glow, a);
            c.a = 1f;
            for (int i = 0; i < 2; i++)
            {
                Renderer r = _r[i];
                if (r == null) continue;
                _block.Clear();
                _block.SetColor("_Color", c);
                r.SetPropertyBlock(_block);
                r.enabled = fade > 0.01f;
            }
            if (_light != null)
            {
                _light.range = 1.2f * _p.transform.localScale.x;
                _light.intensity = 0.8f * a;
            }
        }
    }

    // the beast is not pushed back by blows
    [HarmonyPatch(typeof(Character), "ApplyPushback", new Type[] { typeof(Vector3), typeof(float) })]
    internal static class BeastPushbackPatch
    {
        private static bool Prefix(Character __instance) { return !(Beast.On() && __instance == Player.m_localPlayer); }
    }

    // the beast jumps as high for its body as a man for his: the height grows with the square of the push, so the push
    // is the square root of how many times higher (put back after the jump)
    [HarmonyPatch(typeof(Character), "Jump")]
    internal static class BeastJumpPatch
    {
        private static void Prefix(Character __instance, ref float __state)
        {
            __state = -1f;
            if (!Beast.On() || __instance != Player.m_localPlayer || Beast.JumpHeight <= 1f) return;
            __state = __instance.m_jumpForce;
            __instance.m_jumpForce *= Mathf.Sqrt(Beast.JumpHeight);
        }

        private static void Postfix(Character __instance, float __state)
        {
            if (__state >= 0f) __instance.m_jumpForce = __state;
        }
    }

    // the melee reach grows with the body (the blow is still struck at a man's height, and follows where you look)
    [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    internal static class BeastReachPatch
    {
        // (also the beast's kick: no weapon, the wheel button — it throws them KickPush times as far)
        private static void Prefix(Attack __instance, Humanoid ___m_character, ref Vector2 __state)
        {
            __state = new Vector2(-1f, -1f);
            if (___m_character == null || ___m_character != Player.m_localPlayer) return;
            if (Beast.On() && Kick(___m_character)) { __state.y = __instance.m_forceMultiplier; __instance.m_forceMultiplier *= Beast.KickPush; }
            float s = ___m_character.transform.localScale.x;
            if (s <= 1.01f) return;
            __state.x = __instance.m_attackRange;
            __instance.m_attackRange *= s;
        }

        private static void Postfix(Attack __instance, Vector2 __state)
        {
            if (__state.x >= 0f) __instance.m_attackRange = __state.x;
            if (__state.y >= 0f) __instance.m_forceMultiplier = __state.y;
        }

        private static System.Reflection.FieldInfo _secondary;
        private static bool _secondaryTried;
        private static bool Kick(Humanoid h)
        {
            ItemDrop.ItemData w = h.GetCurrentWeapon();
            if (w == null || w.m_shared == null || w.m_shared.m_skillType != Skills.SkillType.Unarmed) return false;
            if (!_secondaryTried) { _secondaryTried = true; _secondary = AccessTools.Field(typeof(Humanoid), "m_currentAttackIsSecondary"); } // (gone in some game version: no stronger kick, nothing breaks)
            try { return _secondary != null && (bool)_secondary.GetValue(h); } catch { return false; }
        }
    }

    // Path of Valheiman makes running cost more the faster your status effects make you (x(1 + k*bonus), k 3.33 by
    // default). The beast's speed is there so the big body's steps fit the ground, not a sprint to pay for: its share
    // is taken back out, and the beast runs on the stamina you spent before it woke (other speed gifts still count)
    [HarmonyPatch(typeof(SEMan), "ModifyRunStaminaDrain")]
    [HarmonyAfter("treextr.pathofvalheiman")]
    [HarmonyPriority(Priority.Last)]
    internal static class BeastRunStaminaPatch
    {
        private static System.Reflection.MethodInfo _povTunable;
        private static bool _looked;

        private static void Postfix(SEMan __instance, Character ___m_character, float baseDrain, ref float drain)
        {
            if (!Beast.On() || baseDrain <= 0f || drain <= 0f) return;
            Player p = ___m_character as Player;
            if (p == null || p != Player.m_localPlayer || p.IsSwimming()) return;
            float k = PovK();
            if (k <= 0f) return;
            float speed = 1f;
            __instance.ApplyStatusEffectSpeedMods(ref speed, p.transform.forward); // (the bonus as Path of Valheiman reads it)
            float all = Mathf.Max(0f, speed - 1f), other = Mathf.Max(0f, all - Beast.SpeedPercent / 100f);
            drain *= (1f + k * other) / (1f + k * all);
        }

        // its tunable "run.speed.drain" (0: Path of Valheiman not here, nothing to take back)
        private static float PovK()
        {
            if (!_looked)
            {
                _looked = true;
                Type t = AccessTools.TypeByName("PassiveTree.Tunables");
                _povTunable = t != null ? AccessTools.Method(t, "Of", new[] { typeof(string) }) : null;
            }
            if (_povTunable == null) return 0f;
            try { return Mathf.Max(0f, (float)_povTunable.Invoke(null, new object[] { "run.speed.drain" })); }
            catch { _povTunable = null; return 0f; }
        }
    }
}
#endif

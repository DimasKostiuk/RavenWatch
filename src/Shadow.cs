#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Hugin's shadow: for a while monsters neither see nor hear you (bosses do, unless the config says otherwise)
    // and other players do not see you. A swing, a shot or a cast ends it, and the first hit of that attack
    // is a strike from the shadow: the damage times StrikeMultiplier and the enemy staggered.
    // Path of Valheiman strips the game's own sneak attack and adds its own (the "backstab" passives) to hits on
    // enemies that are not alerted: for a strike the enemy counts as not alerted, so those passives add on top.
    internal static class Shadow
    {
        internal const string EffectName = "RavenWatch_Shadow";
        internal static readonly int Hash = EffectName.GetStableHashCode();
        private const string Key = "RavenWatch_Shadow"; // on the player's ZDO: every game hides that player
        private const string RpcFx = "RavenWatch_ShadowFx", RpcIn = "RavenWatch_ShadowIn";
        internal static float Seconds = 10f, SecondsTop = 20f; // at trust level 1 and at the top level
        internal static float StrikeMultiplier = 3f;
        internal static bool Stun = true;
        internal static bool FoolsBosses = false;
        internal static float SelfOpacity = 0.8f; // how you see yourself in the shadow (dark, a little see-through)
        private const float StrikeWindow = 3f;   // an arrow or a spell lands a little after the shot
        private const float CleaveWindow = 0.3f; // one swing may hit several enemies: all of them are struck

        private static SE_Stats _template;
        private static bool _wasOn, _revealed;
        private static float _strikeUntil, _firstHit = -1f;

        internal static float Duration() { return Mathf.Floor(Trust.Lerp(false, Seconds, SecondsTop) * GiftUp.Power("shadow") + 0.5f); }

        internal static bool Anyone { get { return InShadow.Count > 0; } }

        internal static bool On(Player p) { return p != null && p.GetSEMan().HaveStatusEffect(Hash); }

        internal static void Register() { RavenFx.Register(RpcFx, Spawn); RavenFx.Register(RpcIn, Feathers.In); }

        internal static void Start(Player p)
        {
            SE_Stats se = RavenFx.Template(ref _template, EffectName);
            se.m_name = Lang.W("Тінь");
            se.m_tooltip = Lang.F("Тебе не бачать і не чують. Удар, постріл чи закляття знімуть тінь, а перший удар — з тіні: ×{0} шкоди{1}", StrikeMultiplier.ToString("0.#"), (Stun ? Lang.T(" і ворог приголомшений") : ""));
            se.m_icon = AbilityIcons.ForEffect("shadow", false);
            se.m_ttl = Mathf.Max(1f, Duration());
            p.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
            _wasOn = true;
            _revealed = false;
            _strikeUntil = 0f;
            _firstHit = -1f;
            Publish(p);
            Mark(p, true); // now, not on the next frame: the monsters below must already count you hidden
            RavenFx.Broadcast(RpcIn, p, Feathers.In); // the feathers fly in and wrap you
            Calm(p);
        }

        // a swing, a shot or a cast: the shadow goes, the hits of this attack still come from it
        internal static void Reveal(Player p, bool strike)
        {
            if (!On(p)) return;
            _revealed = true;
            if (strike) { _strikeUntil = Time.time + StrikeWindow; _firstHit = -1f; }
            p.GetSEMan().RemoveStatusEffect(Hash, true);
            Mark(p, false);
            Publish(p);
            RavenFx.Broadcast(RpcFx, p, Spawn);
        }

        internal static void Reset() { InShadow.Clear(); _wasOn = false; _revealed = false; _strikeUntil = 0f; _firstHit = -1f; if (_orbit != null) UnityEngine.Object.Destroy(_orbit.transform.parent != null ? _orbit.transform.parent.gameObject : _orbit.gameObject); _orbit = null; }

        private static ParticleSystem _orbit; // your circling feathers while it lasts

        // every frame: the spell ran out by itself
        internal static void Tick(Player p)
        {
            bool on = On(p);
            Mark(p, on);
            TickSelf(p, on);
            if (on && _orbit == null && !p.IsDead()) _orbit = Feathers.Orbit(p);
            else if (!on && _orbit != null) { Feathers.Release(_orbit); _orbit = null; } // they fly off with the burst
            if (_wasOn && !on)
            {
                Publish(p);
                if (!_revealed && !p.IsDead())
                {
                    RavenFx.Broadcast(RpcFx, p, Spawn);
                    p.Message(MessageHud.MessageType.TopLeft, Lang.T("Тінь розвіялась"));
                }
            }
            _wasOn = on;
        }

        // twice a second and on every change: the others learn whether to hide you
        internal static void Publish(Player p)
        {
            if (p != null) NetFlag.Set(p, Key, On(p));
        }

        // the players in the shadow right now (the local one too): kept by TickOthers and Tick, read by every monster
        private static readonly HashSet<Character> InShadow = new HashSet<Character>();

        internal static bool Flagged(Character c) { return c != null && InShadow.Count > 0 && InShadow.Contains(c); }

        private static void Mark(Character c, bool on) { if (on) InShadow.Add(c); else InShadow.Remove(c); }

        // a monster at "me" should not notice the target
        internal static bool HiddenFrom(Character target, Transform me)
        {
            if (InShadow.Count == 0 || !InShadow.Contains(target)) return false;
            if (FoolsBosses || me == null) return true;
            Character m = me.GetComponent<Character>();
            return m == null || !m.IsBoss();
        }

        // the enemies that were after you lose you (the ones this game drives; the others on their owners' games)
        private static void Calm(Player p)
        {
            foreach (Character c in Character.GetAllCharacters())
            {
                MonsterAI ai = c != null ? c.GetBaseAI() as MonsterAI : null;
                if (ai != null) ShadowSensePatch.Forget(ai, p);
            }
        }

        // ---------------------------------------------------------------- the strike

        // the local player hits target: does it come from the shadow
        internal static bool Strikes(Character target)
        {
            Player p = Player.m_localPlayer;
            if (p == null || target == null || target == p || target.IsPlayer()) return false;
            if (target.IsBoss() && !FoolsBosses) return false;
            if (Time.time >= _strikeUntil) return false; // only the attack that ended the shadow, not a bleed or thorns meanwhile
            if (_firstHit < 0f) _firstHit = Time.time;
            else if (Time.time - _firstHit > CleaveWindow) { _strikeUntil = 0f; return false; }
            return true;
        }

        internal static void StrikeFx(Character target, HitData hit)
        {
            try { if (target.m_backstabHitEffects != null) RavenFx.Own(target.m_backstabHitEffects, hit.m_point, Quaternion.identity, target.transform, 1f, -1); }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- the look

        // dark feathers where you vanish or show up
        private static void Spawn(Player p)
        {
            try
            {
                Raven hugin = Shoulder.Template(false);
                Vector3 at = p.transform.position + Vector3.up * 1.1f;
                if (hugin != null && hugin.m_despawnEffect != null) RavenFx.Own(hugin.m_despawnEffect, at, p.transform.rotation, null, 1.4f, -1);
                LastChance.Ring(at, new Color(0.08f, 0.08f, 0.1f, 1f), new Color(0.25f, 0.25f, 0.3f, 1f));
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Shadow effect failed: " + e.Message); }
        }

        // the other players: one in the shadow is not drawn (the model, the gear, the ravens, the lights); Hugin's wisp
        // is its own and stays in sight, light and runes and all
        private static readonly Dictionary<Player, List<Behaviour>> HiddenLights = new Dictionary<Player, List<Behaviour>>();
        private static readonly Dictionary<Player, List<Renderer>> Hidden = new Dictionary<Player, List<Renderer>>();
        private static float _nextLook;

        internal static void TickOthers()
        {
            if (Time.time < _nextLook) return;
            _nextLook = Time.time + (Hidden.Count > 0 ? 0.1f : 0.25f);
            Player me = Player.m_localPlayer;
            InShadow.RemoveWhere(c => c == null);
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == me) continue;
                bool on = NetFlag.Get(o, Key);
                Mark(o, on);
                if (on) Hide(o);
                else if (Hidden.ContainsKey(o)) Show(o);
            }
            List<Player> gone = null;
            foreach (Player o in Hidden.Keys) if (o == null) { if (gone == null) gone = new List<Player>(); gone.Add(o); }
            if (gone != null) foreach (Player o in gone) { Hidden.Remove(o); HiddenLights.Remove(o); }
        }

        private static readonly List<Renderer> ScanR = new List<Renderer>();
        private static readonly List<Light> ScanL = new List<Light>();

        private static void Hide(Player o)
        {
            List<Renderer> rs;
            if (!Hidden.TryGetValue(o, out rs)) { rs = new List<Renderer>(); Hidden[o] = rs; HiddenLights[o] = new List<Behaviour>(); }
            o.GetComponentsInChildren<Renderer>(true, ScanR);
            foreach (Renderer r in ScanR)
                if (r != null && r.enabled && r.GetComponentInParent<WispLife>() == null) { r.enabled = false; rs.Add(r); } // gear put on meanwhile is caught next time
            List<Behaviour> ls = HiddenLights[o];
            o.GetComponentsInChildren<Light>(true, ScanL);
            foreach (Light l in ScanL)
                if (l != null && l.enabled && l.GetComponentInParent<WispLife>() == null) { l.enabled = false; ls.Add(l); }
        }

        private static void Show(Player o)
        {
            foreach (Renderer r in Hidden[o]) if (r != null) r.enabled = true;
            foreach (Behaviour l in HiddenLights[o]) if (l != null) l.enabled = true;
            Hidden.Remove(o);
            HiddenLights.Remove(o);
        }

        // ---------------------------------------------------------------- you, as you see yourself

        // The game's character shader draws nothing see-through, so for the shadow your model wears copies of its
        // materials on a transparent shader (the same textures, dark); the real ones come back after. The way in is
        // smooth, in two steps (and back out the same way, reversed):
        //   1. darkening: the real materials stay and the dark copy is laid over them, from clear to solid — the model
        //      stays solid, so nothing inside it shows through while it is still light;
        //   2. see-through: once it is dark, the copies take the real ones' place and go from solid to SelfOpacity.
        private static readonly Dictionary<Renderer, Material[]> Faded = new Dictionary<Renderer, Material[]>();
        private static readonly Dictionary<Material, Material> Ghosts = new Dictionary<Material, Material>();

        // a body some of whose renderers wear the shadow's materials now (their own come back after)
        internal static bool Fading(GameObject go)
        {
            if (go == null || Faded.Count == 0) return false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) if (Faded.ContainsKey(r)) return true;
            return false;
        }
        private static Shader _ghostShader;
        private static bool _shaderLooked, _standard;
        private static float _nextSelf;
        internal static float FadeInSeconds = 1.7f, FadeOutSeconds = 0.8f; // in: as long as the feathers take to land
        private const float DarkPart = 0.6f; // of the way: darkening first, then see-through
        private static float _k;             // 0 as usual .. 1 fully in the shadow
        private static bool _over = true;    // the copies laid over the real materials (step 1), or in their place (step 2)

        private static float Ease(float u) { return u * u * (3f - 2f * u); }

        private static void TickSelf(Player p, bool on)
        {
            if (SelfOpacity >= 0.999f || GhostShader() == null) { if (Faded.Count > 0) Unfade(); _k = 0f; return; }
            if (!on && _k <= 0f) { if (Faded.Count > 0) Unfade(); return; }
            float before = _k;
            _k = Mathf.MoveTowards(_k, on ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, on ? FadeInSeconds : FadeOutSeconds));
            if (_k <= 0f) { Unfade(); return; }
            if (before <= 0f) _nextSelf = 0f; // just started: everything at once
            bool over = _k < DarkPart;
            if (over != _over) { _over = over; foreach (KeyValuePair<Renderer, Material[]> kv in Faded) Dress(kv.Key, kv.Value); }
            if (Time.time >= _nextSelf)
            {
                _nextSelf = Time.time + 0.25f; // anything new meanwhile joins in (gear at once, through NewGear)
                p.GetComponentsInChildren<Renderer>(false, ScanR);
                foreach (Renderer r in ScanR) Fade(r);
            }
            // the copies' alpha: clear → solid while darkening, solid → SelfOpacity while going see-through
            float a = over ? Ease(_k / DarkPart) : Mathf.Lerp(1f, Mathf.Clamp01(SelfOpacity), Ease((_k - DarkPart) / (1f - DarkPart)));
            foreach (Material m in Ghosts.Values)
                if (m != null) { Color c = m.color; if (!Mathf.Approximately(c.a, a)) { c.a = a; m.color = c; } }
        }

        // the ravens darken with you; the wisp's light stays its own
        private static void Fade(Renderer r)
        {
            if (r == null || r is ParticleSystemRenderer || Faded.ContainsKey(r) || r.GetComponentInParent<WispLife>() != null) return;
            Material[] orig = r.sharedMaterials;
            Faded[r] = orig;
            Dress(r, orig);
        }

        // step 1: the real ones and the dark copy over them (an extra material draws the last part of the mesh again);
        // step 2: the copies alone
        private static void Dress(Renderer r, Material[] orig)
        {
            if (r == null || orig == null) return;
            Shader sh = GhostShader();
            if (_over)
            {
                if (orig.Length == 0) return;
                Material[] both = new Material[orig.Length + 1];
                for (int i = 0; i < orig.Length; i++) both[i] = orig[i];
                both[orig.Length] = Ghost(orig[orig.Length - 1], sh);
                r.sharedMaterials = both;
            }
            else
            {
                Material[] ghost = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++) ghost[i] = Ghost(orig[i], sh);
                r.sharedMaterials = ghost;
            }
        }

        // the game has just put a piece of gear on someone (VisEquipment): in your shadow it darkens now, on another
        // player in the shadow it is hidden now — not a quarter of a second later
        internal static void NewGear(VisEquipment ve, GameObject go)
        {
            if (go == null || ve == null || (InShadow.Count == 0 && _k <= 0f)) return;
            Player pl = ve.GetComponent<Player>();
            if (pl == null) return;
            if (pl == Player.m_localPlayer)
            {
                if (_k <= 0f || SelfOpacity >= 0.999f || GhostShader() == null) return;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) Fade(r);
                return;
            }
            List<Renderer> rs;
            if (!Hidden.TryGetValue(pl, out rs)) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) if (r != null && r.enabled) { r.enabled = false; rs.Add(r); }
        }

        private static void Unfade()
        {
            foreach (KeyValuePair<Renderer, Material[]> kv in Faded) if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            Faded.Clear();
            _k = 0f;
            _over = true;
        }

        private static Shader GhostShader()
        {
            if (_shaderLooked) return _ghostShader;
            _shaderLooked = true;
            string[] names = { "Legacy Shaders/Transparent/Diffuse", "Standard", "Sprites/Default" };
            foreach (string n in names)
            {
                Shader s = Shader.Find(n);
                if (s == null || !s.isSupported) continue;
                _ghostShader = s;
                _standard = n == "Standard";
                if (Plugin.Instance != null) Plugin.Instance.Log("Shadow: you look see-through with " + n);
                return s;
            }
            if (Plugin.Instance != null) Plugin.Instance.Log("Shadow: no transparent shader found, you look as usual");
            return null;
        }

        private static Material Ghost(Material src, Shader sh)
        {
            if (src == null) return null;
            Material m;
            if (Ghosts.TryGetValue(src, out m) && m != null) return m;
            m = new Material(sh);
            m.name = src.name + " (shadow)";
            m.hideFlags = HideFlags.HideAndDontSave;
            if (src.HasProperty("_MainTex")) { m.mainTexture = src.mainTexture; m.mainTextureScale = src.mainTextureScale; m.mainTextureOffset = src.mainTextureOffset; }
            // dark, with a cold blue: the texture shows only faintly through it, as a shape in the shadow
            Color c = src.HasProperty("_Color") ? src.color : Color.white;
            m.color = new Color(c.r * 0.14f + 0.02f, c.g * 0.15f + 0.025f, c.b * 0.2f + 0.045f, 0f); // its alpha: TickSelf, as it fades in and out
            if (_standard)
            {
                // the Standard shader's "Fade" mode
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 1); // the near side hides the far one: no arm showing through the body
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = 3000;
            Ghosts[src] = m;
            return m;
        }

        internal static void ResetLook()
        {
            Unfade();
            foreach (Material m in Ghosts.Values) if (m != null) UnityEngine.Object.Destroy(m);
            Ghosts.Clear();
            foreach (Player o in new List<Player>(Hidden.Keys)) if (o != null) Show(o);
            Hidden.Clear();
            HiddenLights.Clear();
        }
    }

    // ---------------------------------------------------------------- monsters: neither see nor hear

    [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new Type[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
    internal static class ShadowSeePatch
    {
        private static bool Prefix(Transform me, Character target, ref bool __result)
        { try {
            long t0 = Perf.On ? Perf.Now : 0;
            bool hidden = Shadow.HiddenFrom(target, me);
            if (Perf.On) Perf.Add(t0);
            if (!hidden) return true;
            __result = false;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("ShadowSeePatch", e); return true; } }
    }

    [HarmonyPatch(typeof(BaseAI), "CanHearTarget", new Type[] { typeof(Transform), typeof(float), typeof(Character) })]
    internal static class ShadowHearPatch
    {
        private static bool Prefix(Transform me, Character target, ref bool __result)
        { try {
            if (!Shadow.HiddenFrom(target, me)) return true;
            __result = false;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("ShadowHearPatch", e); return true; } }
    }

    // a monster already after you loses you and calms down (so the strike finds it unaware)
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class ShadowSensePatch
    {
        private static readonly FieldInfo Target = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly MethodInfo SetAlerted = AccessTools.Method(typeof(BaseAI), "SetAlerted");
        private static readonly System.Collections.Generic.Dictionary<int, float> Calmed = new System.Collections.Generic.Dictionary<int, float>();

        private static void Postfix(MonsterAI __instance, ref bool canHearTarget, ref bool canSeeTarget)
        { try {
            if (Target == null || !Shadow.Anyone) return; // nobody in the shadow: nothing to do for this monster
            Character t = Target.GetValue(__instance) as Character;
            if (t == null || !Shadow.HiddenFrom(t, __instance.transform)) return;
            canHearTarget = false;
            canSeeTarget = false;
            Forget(__instance, t);
        } catch (System.Exception e) { PatchGuard.Fail("ShadowSensePatch", e); } }

        internal static void Forget(MonsterAI ai, Character who)
        {
            if (Target == null || ai == null || Target.GetValue(ai) as Character != who) return;
            if (!Shadow.HiddenFrom(who, ai.transform)) return;
            ZNetView nv = ai.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            Target.SetValue(ai, null);
            // calmed at most once in a while: dropped again and again, the game raises the alert again and every
            // raise plays the creature's alert sound
            int key = ai.GetInstanceID();
            float last;
            if (Calmed.TryGetValue(key, out last) && Time.time - last < 5f) return;
            if (Calmed.Count > 200) Calmed.Clear();
            Calmed[key] = Time.time;
            if (SetAlerted != null) SetAlerted.Invoke(ai, new object[] { false });
        }
    }

    // ---------------------------------------------------------------- what ends it

    // the moment an attack does something: a swing hits, an arrow or a spell flies
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    internal static class ShadowAttackPatch
    {
        private static readonly FieldInfo Owner = AccessTools.Field(typeof(Attack), "m_character");

        private static void Prefix(Attack __instance)
        { try {
            Player p = Player.m_localPlayer;
            if (p == null || Owner == null || Owner.GetValue(__instance) as Player != p || !Shadow.On(p)) return;
            if (__instance.m_attackType == Attack.AttackType.None) return; // not an attack
            Shadow.Reveal(p, true);
        } catch (System.Exception e) { PatchGuard.Fail("ShadowAttackPatch", e); } }
    }

    // the strike: before Path of Valheiman counts its own bonuses, so they multiply ours
    [HarmonyPatch(typeof(Character), "Damage")]
    [HarmonyBefore("treextr.pathofvalheiman")]
    internal static class ShadowStrikePatch
    {
        private static readonly FieldInfo Alerted = AccessTools.Field(typeof(BaseAI), "m_alerted");

        private static void Prefix(Character __instance, HitData hit, out bool __state)
        { __state = default(bool); try {
            __state = false;
            Player me = Player.m_localPlayer;
            if (hit == null || me == null || hit.m_attacker != me.GetZDOID() || hit.m_skillRaiseAmount <= Strike.Marker || !Shadow.Strikes(__instance)) return; // by id: no lookup for every blow in the area; a raven's blow or bleed (marked) is not yours: it must not take your strike from the shadow
            __state = true;
            hit.ApplyModifier(Mathf.Max(1f, Shadow.StrikeMultiplier) * (Facets.On("shadow") ? 2f : 1f)); // («Тінь» III: twice again)
            hit.m_backstabBonus = 1f; // ours replaces the game's own sneak attack, they do not stack
            BaseAI ai = __instance.GetBaseAI();
            if (ai != null && Alerted != null) Alerted.SetValue(ai, false); // seen here as unaware; the owner's game sets it right again
            Shadow.StrikeFx(__instance, hit);
        } catch (System.Exception e) { PatchGuard.Fail("ShadowStrikePatch", e); } }

        private static void Postfix(Character __instance, HitData hit, bool __state)
        { try {
            if (!__state || __instance == null || __instance.IsDead() || !Shadow.Stun) return;
            __instance.Stagger(hit != null ? hit.m_dir : -__instance.transform.forward); // after the damage, so the stagger does not double it
        } catch (System.Exception e) { PatchGuard.Fail("ShadowStrikePatch", e); } }
    }

    // A player in the shadow shows no name and health bar over the head to the others (as the game does for a crouching one).
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    internal static class ShadowHudPatch
    {
        private static void Postfix(Character c, ref bool __result)
        {
            if (__result && c != null && c != Player.m_localPlayer && c.IsPlayer() && Shadow.Flagged(c)) __result = false;
        }
    }

    // a piece of gear the game has just put on someone: darkened or hidden at once while they are in the shadow
    [HarmonyPatch(typeof(VisEquipment), "AttachItem")]
    internal static class ShadowGearItemPatch
    {
        private static void Postfix(VisEquipment __instance, GameObject __result) { Shadow.NewGear(__instance, __result); }
    }

    [HarmonyPatch(typeof(VisEquipment), "AttachBackItem")]
    internal static class ShadowGearBackPatch
    {
        private static void Postfix(VisEquipment __instance, GameObject __result) { Shadow.NewGear(__instance, __result); }
    }

    [HarmonyPatch(typeof(VisEquipment), "AttachArmor")]
    internal static class ShadowGearArmorPatch
    {
        private static void Postfix(VisEquipment __instance, List<GameObject> __result)
        {
            if (__result != null) foreach (GameObject g in __result) Shadow.NewGear(__instance, g);
        }
    }
}
#endif

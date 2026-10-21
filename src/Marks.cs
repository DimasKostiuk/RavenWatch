#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // What a raven's strike leaves on its target for a few seconds.
    //  Hugin, «Осліплення»: pecked in the eyes, it neither sees nor hears anyone and drops whoever it was after.
    //  Munin, «Рвана рана»: it bleeds (the striker's game deals the drops, cut like any raven blow) and limps slower.
    // Sent to every game: the one that runs the creature acts on it, every one shows it (runes round the head,
    // falling drops of blood).
    internal static class Marks
    {
        internal static float BlindSeconds = 5f, BlindBossSeconds = 1.5f;
        internal static float WoundSeconds = 5f, WoundBossSeconds = 2.5f;
        internal static float BleedShare = 0.4f;  // of Munin's blow, over the wound's time
        internal static float Slow = 0.25f;       // the limp

        private const string Rpc = "RavenWatch_Mark";
        private const byte Blind = 1, Wound = 2, BlindFirm = 3; // (firm: «Удар в очі» III — a blow does not end it)
        private static readonly HashSet<Transform> Firm = new HashSet<Transform>();

        private static readonly Dictionary<Transform, float> Blinded = new Dictionary<Transform, float>();
        internal static bool AnyBlind { get { return Blinded.Count > 0; } }

        internal static void Register()
        {
            ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnMark));
        }

        internal static void Reset() { Blinded.Clear(); Bleeds.Clear(); Firm.Clear(); }

        // from the striker's game, at the first blow on a target
        internal static void Put(Character c, bool munin)
        {
            if (c == null || c.IsDead() || ZRoutedRpc.instance == null) return;
            float secs = munin ? (c.IsBoss() ? WoundBossSeconds : WoundSeconds) : (c.IsBoss() ? BlindBossSeconds : BlindSeconds);
            if (secs <= 0f) return;
            bool firm = !munin && Facets.On("hstrike");
            if (firm) secs *= 2f;
            ZPackage pkg = new ZPackage();
            pkg.Write(c.GetZDOID());
            pkg.Write(munin ? Wound : firm ? BlindFirm : Blind);
            pkg.Write(secs);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg); // every game, this one too
        }

        private static void OnMark(long sender, ZPackage pkg)
        {
            try
            {
                ZDOID id = pkg.ReadZDOID();
                byte kind = pkg.ReadByte();
                float secs = pkg.ReadSingle();
                GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                Character c = go != null ? go.GetComponent<Character>() : null;
                if (c == null || c.IsDead()) return;
                ZNetView nv = c.GetComponent<ZNetView>();
                bool mine = nv != null && nv.IsValid() && nv.IsOwner();
                if (kind == Blind || kind == BlindFirm)
                {
                    if (kind == BlindFirm) Firm.Add(c.transform); else Firm.Remove(c.transform);
                    Blinded[c.transform] = Time.time + secs; // every game keeps it: the creature may change hands meanwhile
                    if (mine) LoseTarget(c);
                    if (mine && Plugin.Instance != null) Plugin.Instance.Log("Blind: " + Utils.GetPrefabName(c.gameObject) + " for " + secs.ToString("0.#") + " s (run here)");
                    MarkLook.Show(c, false, secs);
                }
                else if (kind == Wound)
                {
                    if (mine) Limp(c, secs);
                    MarkLook.Show(c, true, secs);
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven mark unreadable: " + e.Message + " @ " + (e.StackTrace ?? "").Split(new[] { (char)10 })[0].Trim()); }
        }

        // ---------------------------------------------------------------- blind

        // hit by someone (not the raven's own blows): it feels where it came from, the blindness is over
        internal static void Unblind(Transform t, HitData hit)
        {
            if (t == null || Firm.Contains(t) || !Blinded.Remove(t)) return; // (a firm blindness: blows do not end it)
            MarkLook.Hide(t, false); // its runes go too, or it seemed still blind while it fought
            if (Plugin.Instance == null) return;
            Character a = hit != null ? hit.GetAttacker() : null;
            Plugin.Instance.Log("Blind over early: " + Utils.GetPrefabName(t.gameObject) + " hit by " + (a != null ? a.GetHoverName() : "?") + " (" + (hit != null ? hit.m_hitType.ToString() : "?") + ", " + (hit != null ? hit.GetTotalDamage().ToString("0") : "?") + ")");
        }

        // a blow, not a wound working on its own: burning, poison, frost and the like tick from the player as well,
        // and one tick of a poisoned blade was enough to end the blindness at once
        internal static bool RealBlow(HitData hit)
        {
            switch (hit.m_hitType)
            {
                case HitData.HitType.Burning: case HitData.HitType.Freezing: case HitData.HitType.Poisoned:
                case HitData.HitType.Smoke: case HitData.HitType.Water: case HitData.HitType.Fall: case HitData.HitType.Drowning:
                    return false;
            }
            return true;
        }

        // Munin's wounds from this player on any of these still bleeding
        internal static bool Bleeding(Player p, List<Character> on)
        {
            foreach (Bleed b in Bleeds) if (b.From == p && b.Left > 0 && b.Target != null && !b.Target.IsDead() && on.Contains(b.Target)) return true;
            return false;
        }

        internal static bool IsBlind(Transform t)
        {
            float until;
            if (t == null || !Blinded.TryGetValue(t, out until)) return false;
            if (Time.time < until) return true;
            Blinded.Remove(t); Firm.Remove(t);
            return false;
        }

        private static readonly FieldInfo TargetField = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");
        private static readonly FieldInfo StaticField = AccessTools.Field(typeof(MonsterAI), "m_targetStatic");
        private static readonly MethodInfo SetAlerted = AccessTools.Method(typeof(BaseAI), "SetAlerted");

        internal static void LoseTarget(Character c)
        {
            MonsterAI ai = c != null ? c.GetComponent<MonsterAI>() : null;
            if (ai == null) return;
            if (TargetField != null) TargetField.SetValue(ai, null);
            if (StaticField != null) StaticField.SetValue(ai, null);
            // its alert is left alone: dropped every frame, the game raised it again at once, and every raise
            // plays the creature's alert sound (a Greyling kept crying for the whole blindness)
        }

        // ---------------------------------------------------------------- wound: the limp (where the creature is run)

        private static SE_Stats _limp;
        private const string LimpName = "RavenWatch_Wound";

        private static void Limp(Character c, float secs)
        {
            SE_Stats se = RavenFx.Template(ref _limp, LimpName);
            se.m_name = Lang.W("Рвана рана");
            se.m_ttl = secs;
            se.m_speedModifier = -Mathf.Clamp01(Slow);
            se.m_icon = AbilityIcons.ForEffect("mstrike", true);
            c.GetSEMan().AddStatusEffect(se, true, 0, 0f, 0);
        }

        // ---------------------------------------------------------------- wound: the bleeding (the striker's game deals it)

        private sealed class Bleed
        {
            public Player From;
            public Character Target;
            public float Per;
            public int Left;
            public float Next;
            public Dictionary<Character, float> CapLeft;
            public Action<Character, float> Count;
        }

        private static readonly List<Bleed> Bleeds = new List<Bleed>();

        internal static void StartBleed(Player p, Character c, float blow, Dictionary<Character, float> capLeft, Action<Character, float> count)
        {
            float secs = c.IsBoss() ? WoundBossSeconds : WoundSeconds;
            int ticks = Mathf.Max(1, Mathf.RoundToInt(secs));
            float total = blow * Mathf.Max(0f, BleedShare) * (secs / Mathf.Max(0.1f, WoundSeconds)) * (Facets.On("mstrike") ? 2f : 1f); // («Кігті» III: twice the bleeding)
            if (total <= 0.5f) return;
            foreach (Bleed b in Bleeds) if (b.Target == c && b.From == p) { b.Left = ticks; b.Per = total / ticks; b.CapLeft = capLeft; b.Count = count; return; } // a fresh wound on an old one: this flight's now
            Bleeds.Add(new Bleed { From = p, Target = c, Per = total / ticks, Left = ticks, Next = Time.time + 1f, CapLeft = capLeft, Count = count });
        }

        // every frame from the skills' Update
        private static float _nextSweep;

        internal static void Tick()
        {
            if (Blinded.Count > 0 && Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + 1f;
                List<Transform> over = null;
                foreach (KeyValuePair<Transform, float> kv in Blinded)
                    if (kv.Key == null || Time.time >= kv.Value) { if (over == null) over = new List<Transform>(); over.Add(kv.Key); }
                if (over != null) foreach (Transform t in over) { Blinded.Remove(t); Firm.Remove(t); }
            }
            for (int i = Bleeds.Count - 1; i >= 0; i--)
            {
                Bleed b = Bleeds[i];
                if (b.Target == null || b.Target.IsDead() || b.From == null || b.Left <= 0) { Bleeds.RemoveAt(i); continue; }
                if (Time.time < b.Next) continue;
                b.Next = Time.time + 1f;
                b.Left--;
                float v = b.Per;
                if (b.Target.IsBoss() && b.CapLeft != null)
                {
                    float left;
                    if (!b.CapLeft.TryGetValue(b.Target, out left)) left = b.Target.GetMaxHealth() * Strike.BossCapMunin;
                    v = Mathf.Min(v, left);
                    b.CapLeft[b.Target] = left - v;
                }
                if (v <= 0.5f) continue;
                HitData hit = new HitData();
                hit.m_damage.m_slash = v;
                hit.m_point = Body.Center(b.Target);
                hit.m_dir = Vector3.down;
                hit.m_pushForce = 0f;
                hit.m_staggerMultiplier = 0f;
                hit.m_backstabBonus = 1f;
                hit.m_blockable = false;
                hit.m_dodgeable = false;
                hit.m_ranged = true;
                hit.m_skill = Skills.SkillType.None;
                hit.m_skillRaiseAmount = Strike.Marker - v;
                hit.SetAttacker(b.From);
                b.Target.Damage(hit);
                if (b.Count != null) b.Count(b.Target, v);
            }
        }
    }

    // ---------------------------------------------------------------- the look on the target

    // Blind: a few of Hugin's blue runes going round its head. Wounded: red drops falling from it.
    internal class MarkLook : MonoBehaviour
    {
        private ParticleSystem _ps;
        private float _until;
        private Character _c;
        private bool _blood;

        private static Material _dot;

        internal static void Show(Character c, bool blood, float secs)
        {
            if (c == null) return;
            string name = blood ? "RavenWatch_Wound" : "RavenWatch_Blind";
            Transform old = c.transform.Find(name);
            MarkLook look = old != null ? old.GetComponent<MarkLook>() : null;
            if (look == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(c.transform, false);
                look = go.AddComponent<MarkLook>();
                look._c = c;
                look._blood = blood;
                look.Build();
            }
            look._until = Mathf.Max(look._until, Time.time + secs);
            if (look._ps != null && !look._ps.isEmitting) look._ps.Play();
        }

        internal static void Hide(Transform on, bool blood)
        {
            Transform t = on != null ? on.Find(blood ? "RavenWatch_Wound" : "RavenWatch_Blind") : null;
            MarkLook look = t != null ? t.GetComponent<MarkLook>() : null;
            if (look != null) look._until = Mathf.Min(look._until, Time.time);
        }

        private void Build()
        {
            Material m = _blood ? Dot() : DomeLook.RuneMaterialPublic();
            if (m == null) return;
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = _blood ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            float r = _c != null ? Mathf.Max(0.3f, Body.Radius(_c)) : 0.5f;
            if (_blood)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.02f, 0.02f, 0.95f), new Color(0.8f, 0.05f, 0.05f, 0.95f));
                main.gravityModifier = 1.2f;
                main.maxParticles = 40;
                ParticleSystem.EmissionModule em = _ps.emission; em.rateOverTime = 14f;
                ParticleSystem.ShapeModule sh = _ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = r * 0.6f;
                transform.localPosition = Vector3.up * (_c != null ? (Body.Center(_c) - _c.transform.position).y : 1f);
            }
            else
            {
                // as big as the creature: a troll's ring is wider, its runes larger, so they show over it
                float big = Mathf.Clamp(r / 0.5f, 1f, 4f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.28f * big, 0.38f * big);
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(DomeLook.Blue, Color.white, 0.25f), Color.Lerp(DomeLook.Blue, Color.white, 0.6f));
                main.maxParticles = 24;
                ParticleSystem.EmissionModule em = _ps.emission; em.rateOverTime = 10f;
                ParticleSystem.ShapeModule sh = _ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = Mathf.Max(0.45f, r * 0.9f); sh.radiusThickness = 0f;
                ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(2.2f); // round the head (the ring lies flat: its own z is up)
                ParticleSystem.TextureSheetAnimationModule sheet = _ps.textureSheetAnimation;
                sheet.enabled = true; sheet.numTilesX = 4; sheet.numTilesY = 4; sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
                Vector3 head = _c != null ? Body.Head(_c) : transform.position + Vector3.up * 1.8f;
                transform.position = head + Vector3.up * (0.35f + r * 0.35f);
                transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }
            ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ParticleSystemRenderer pr = GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            _ps.Play();
        }

        private void Update()
        {
            if (_c == null || _c.IsDead() || Time.time >= _until)
            {
                if (_ps != null && _ps.isEmitting) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (Time.time >= _until + 1.5f || _c == null) Destroy(gameObject);
            }
        }

        // a soft round drop
        private static Material Dot()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f + 0.5f) / (n / 2f), dy = (y - n / 2f + 0.5f) / (n / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 1.6f));
                }
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            _dot = new Material(sh);
            _dot.name = "RavenWatch blood drop";
            _dot.mainTexture = t;
            _dot.hideFlags = HideFlags.HideAndDontSave;
            return _dot;
        }
    }

    // ---------------------------------------------------------------- patches: a blind creature senses nobody

    [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new Type[] { typeof(Transform), typeof(Vector3), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
    internal static class BlindSeePatch
    {
        private static bool Prefix(Transform me, ref bool __result)
        { try {
            if (!Marks.AnyBlind || !Marks.IsBlind(me)) return true;
            __result = false;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("BlindSeePatch", e); return true; } }
    }

    [HarmonyPatch(typeof(BaseAI), "CanHearTarget", new Type[] { typeof(Transform), typeof(float), typeof(Character) })]
    internal static class BlindHearPatch
    {
        private static bool Prefix(Transform me, ref bool __result)
        { try {
            if (!Marks.AnyBlind || !Marks.IsBlind(me)) return true;
            __result = false;
            return false;
        } catch (System.Exception e) { PatchGuard.Fail("BlindHearPatch", e); return true; } }
    }

    // a blow from a player (any but the raven's own, marked) ends the blindness: it knows where that came from
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    internal static class BlindHitPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            if (!Marks.AnyBlind || hit == null || hit.m_skillRaiseAmount <= Strike.Marker) return;
            if (Marks.RealBlow(hit) && hit.GetAttacker() is Player) Marks.Unblind(__instance.transform, hit);
        } catch (System.Exception e) { PatchGuard.Fail("BlindHitPatch", e); } }
    }

    // whoever it was after is lost while it is blind
    [HarmonyPatch(typeof(MonsterAI), "UpdateTarget")]
    internal static class BlindTargetPatch
    {
        private static void Postfix(MonsterAI __instance, ref bool canHearTarget, ref bool canSeeTarget)
        { try {
            if (!Marks.AnyBlind || !Marks.IsBlind(__instance.transform)) return;
            Character body = __instance.GetComponent<Character>();
            Marks.LoseTarget(body);
            canHearTarget = false;
            canSeeTarget = false;
        } catch (System.Exception e) { PatchGuard.Fail("BlindTargetPatch", e); } }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The gifts' third golden level (GiftUp: «III — its own facet»): each gift gets a thing of its own. The ones that
    // only change a number of the gift are written where that number is (Shadow, Marks, OdinWings, WeakPassives,
    // LastChance, Ward, Beast, Decoy, RavenSkills); the ones that act by themselves are here:
    //   light     — undead near your light flee it for a moment
    //   leap      — holding Jump while falling in the leap: you glide
    //   dome      — under your dome you heal
    //   bloodfury — every blow of yours in the fury heals you a little
    //   fury      — you also run faster
    //   wings     — you also swim faster
    //   heal      — the players near you heal as well
    internal static class Facets
    {
        internal static float LightReach = 10f, LightFearEvery = 2.5f, LightFearPause = 12f; // (each creature: a fright, then a pause)
        internal static float GlideFall = 3f;            // m/s: the slowest fall while gliding
        internal static float DomeHealPct = 2f;          // % of health a second
        internal static float FuryHealPct = 2f;          // % of health a blow
        internal static float FurySpeed = 1.15f, SwimSpeed = 1.3f;
        internal static float HealReach = 10f;
        private const string RpcHeal = "RavenWatch_HealFriend";

        internal static bool On(string id) { return GiftUp.Level(id) >= GiftUp.MaxLevel; }

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(RpcHeal, new Action<long, ZPackage>(OnHeal)); }

        private static float _nextLight, _nextDome, _lastFuryHeal = -99f;
        private static readonly System.Reflection.FieldInfo Body = AccessTools.Field(typeof(Character), "m_body");
        private static readonly System.Reflection.MethodInfo TakesInput = AccessTools.Method(typeof(Player), "TakeInput");
        private static readonly Dictionary<Character, float> ScaredUntil = new Dictionary<Character, float>(); // light III: when each may be frightened again

        // every frame (RavenSkills.Update)
        internal static void Frame()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            Glide(p);
            if (Time.time >= _nextLight) { _nextLight = Time.time + LightFearEvery; LightFear(p); }
            if (Time.time >= _nextDome) { _nextDome = Time.time + 1f; DomeHeal(p); }
        }

        // light III: the undead near your light run from it
        private static void LightFear(Player p)
        {
            if (ScaredUntil.Count > 32) { List<Character> gone = new List<Character>(); foreach (KeyValuePair<Character, float> kv in ScaredUntil) if (kv.Key == null || kv.Value < Time.time) gone.Add(kv.Key); foreach (Character c in gone) ScaredUntil.Remove(c); }
            if (!RavenLight.On(p) || !On("light")) return;
            List<Character> who = new List<Character>();
            float until;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.IsDead() || c.IsTamed() || c.IsBoss() || c.m_faction != Character.Faction.Undead) continue;
                if (Vector3.Distance(c.transform.position, p.transform.position) > LightReach) continue;
                if (ScaredUntil.TryGetValue(c, out until) && until > Time.time) continue; // flees for a moment, then dares again for a while
                ScaredUntil[c] = Time.time + Fear.Seconds + LightFearPause;
                who.Add(c);
            }
            if (who.Count > 0) Fear.Frighten(p.transform.position, who);
        }

        // leap III: falling in the leap with Jump held, you come down slowly
        private static void Glide(Player p)
        {
            if (!Leap.On(p) || p.IsOnGround() || p.InWater() || !On("leap")) return;
            bool input = false;
            try { input = TakesInput != null && (bool)TakesInput.Invoke(p, null); } catch { }
            if (!input || !(ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump"))) return;
            Rigidbody rb = Body != null ? Body.GetValue(p) as Rigidbody : null;
            if (rb == null || rb.linearVelocity.y >= -GlideFall) return;
            Vector3 v = rb.linearVelocity;
            v.y = Mathf.Lerp(v.y, -GlideFall, Mathf.Clamp01(Time.deltaTime * 8f));
            rb.linearVelocity = v;
        }

        // dome III: you heal under your own dome
        private static void DomeHeal(Player p)
        {
            if (!Dome.InsideMine(p.transform.position) || !On("dome")) return;
            float missing = p.GetMaxHealth() - p.GetHealth();
            if (missing > 0.05f) p.Heal(Mathf.Min(missing, p.GetMaxHealth() * DomeHealPct / 100f), false);
        }

        // bloodfury III: each blow of yours in the fury
        internal static void FuryBlow(Player p)
        {
            if (p == null || p.IsDead() || !BloodFury.On(p) || !On("bloodfury")) return;
            if (Time.time - _lastFuryHeal < 0.3f) return; // one swing through many foes heals once
            _lastFuryHeal = Time.time;
            float missing = p.GetMaxHealth() - p.GetHealth();
            if (missing > 0.05f) p.Heal(Mathf.Min(missing, p.GetMaxHealth() * FuryHealPct / 100f), true);
        }

        // heal III: the players near you, each healed on their own game as by their own raven
        internal static void HealFriends(Player p, float seconds)
        {
            if (p == null || !On("heal") || ZRoutedRpc.instance == null) return;
            int n = 0;
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null || pl == p || pl.IsDead() || Vector3.Distance(pl.transform.position, p.transform.position) > HealReach) continue;
                if (pl.GetHealth() >= pl.GetMaxHealth() - 0.5f || (p.IsPVPEnabled() && pl.IsPVPEnabled())) continue; // whole already, or a foe in PvP
                ZPackage pkg = new ZPackage();
                pkg.Write(pl.GetPlayerID());
                pkg.Write(seconds);
                pkg.Write(p.GetPlayerName());
                ZRoutedRpc.instance.InvokeRoutedRPC(pl.GetOwner(), RpcHeal, pkg);
                n++;
            }
            if (n > 0) p.Message(MessageHud.MessageType.TopLeft, Lang.F("Зцілення Муніна — ще й для {0} поруч", n));
        }

        private static void OnHeal(long sender, ZPackage pkg)
        {
            Player me = Player.m_localPlayer;
            long pid = pkg.ReadLong();
            float seconds = Mathf.Clamp(pkg.ReadSingle(), 1f, 60f);
            string from = pkg.ReadString();
            if (me == null || me.IsDead() || me.GetPlayerID() != pid) return;
            bool near = false; // only from a player who stands by you
            foreach (Player pl in Player.GetAllPlayers())
                if (pl != null && pl != me && pl.GetOwner() == sender && Vector3.Distance(pl.transform.position, me.transform.position) <= HealReach + 5f) { near = true; break; }
            if (!near) return;
            RavenHeal.Start(me, seconds);
            me.Message(MessageHud.MessageType.Center, Lang.F("Мунін гравця {0} лікує й тебе", from));
        }
    }

    // bloodfury III: a blow of yours (yours, not a raven's) heals you in the fury
    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class FacetFuryBlowPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || hit == null || hit.m_attacker != me.GetZDOID() || __instance == me || hit.m_skillRaiseAmount <= Strike.Marker || !BloodFury.On(me)) return;
            if (hit.GetTotalDamage() <= 0f || __instance.IsTamed() || !BaseAI.IsEnemy(me, __instance)) return; // a real blow at a foe
            Facets.FuryBlow(me);
        } catch (Exception e) { PatchGuard.Fail("FacetFuryBlowPatch", e); } }
    }

    // fury III: you run faster in it (called for every creature every frame: yours only, and the check once a frame)
    [HarmonyPatch(typeof(SEMan), "ApplyStatusEffectSpeedMods")]
    internal static class FacetFurySpeedPatch
    {
        private static int _frame = -1;
        private static bool _on;
        private static void Postfix(SEMan __instance, ref float speed)
        { try {
            Player me = Player.m_localPlayer;
            if (me == null || !ReferenceEquals(__instance, me.GetSEMan())) return;
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _on = Fury.On(me) && Facets.On("fury"); }
            if (_on) speed *= Facets.FurySpeed;
        } catch (Exception e) { PatchGuard.Fail("FacetFurySpeedPatch", e); } }
    }

    // wings III: you swim faster in the wind
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    internal static class FacetSwimPatch
    {
        private static void Prefix(Character __instance, out float __state)
        { __state = -1f; try {
            Player me = Player.m_localPlayer;
            if (me == null || __instance != me || !WingsFx.On(me) || !Facets.On("wings")) return;
            __state = me.m_swimSpeed;
            me.m_swimSpeed *= Facets.SwimSpeed;
        } catch (Exception e) { PatchGuard.Fail("FacetSwimPatch", e); } }

        private static void Postfix(Character __instance, float __state)
        { try { if (__state >= 0f && __instance != null) __instance.m_swimSpeed = __state; } catch { } }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's «Приборкання» (active): aim at a wild beast that can be tamed — Munin flies onto its head and breaks it in,
    // pecking between the horns and swearing. While he sits there (Seconds) its taming runs Speed times faster, and it
    // goes for nobody (it does not attack you, it is not alerted, so the taming does not stop). It still has to be fed,
    // as the game wants — but with the golden rune III it stays fed while he sits. Now and then it bucks and he swears.
    // Press again: Munin comes home. Tamed meanwhile, the beast is left and he comes home proud.
    //   - the seat: SitOn's «sit here» spot on the beast's head (wild allowed only here), unpaid there (paid here);
    //   - the beast is told to every game (RpcTame: who, till when, how much faster, fed): the game that runs it (its
    //     owner, the one taming it) speeds the taming and calms it.
    internal static class Tame
    {
        internal static float Cost = 40f;
        internal static float CooldownMinutes = 10f, CooldownMinutesTop = 5f;
        internal static float Seconds = 60f, SecondsTop = 120f;
        internal static float Speed = 2f, SpeedTop = 3.2f, SpeedMax = 4f; // ×2 at first, ×3.2 at the top trust, the golden rune I on top (×1.25): ×4 at most
        internal static float Range = 30f;
        private const string Rpc = "RavenWatch_Tame";

        private sealed class Boost { public double Until; public float Mul; public bool Fed; }
        private static readonly Dictionary<ZDOID, Boost> Boosted = new Dictionary<ZDOID, Boost>(); // every game
        internal static bool Any { get { return Boosted.Count > 0 || Held.Count > 0; } }
        // the beasts told to look for nobody (the game's own switch, Humanoid.m_aiCannotTargetOthers), with what it was before
        private static readonly Dictionary<Humanoid, bool> Held = new Dictionary<Humanoid, bool>();

        // the caster's own
        private static ZDOID _target = ZDOID.None;
        private static float _until, _buckAt, _seatedAt = -1f, _began, _paid;
        private static string _token = "";

        internal static bool Mine() { return _target != ZDOID.None; }
        internal static float Left() { return Mine() ? Mathf.Max(0f, _until - Time.time) : 0f; }

        internal static float Duration() { return Trust.RoundTime(Trust.LerpGift("mtame", Seconds, SecondsTop)); }
        internal static float SpeedNow() { return Mathf.Min(SpeedMax, Trust.LerpGift("mtame", Speed, SpeedTop) * GiftUp.Power("mtame")); }

        // «2 рази», «3 рази», «2,5 раза»
        internal static string Times()
        {
            float v = Mathf.Round(SpeedNow() * 10f) / 10f;
            return Mathf.Approximately(v, Mathf.Round(v)) ? Lang.N(Mathf.RoundToInt(v), "раз|рази|разів") : Lang.F("{0} раза", Lang.Num(v, "0.#"));
        }

        // ---------------------------------------------------------------- the gift

        // why not this one (null: it will do)
        private static string WhyNot(Character c)
        {
            if (c == null) return Lang.T("Наведи приціл на дику тварину, яку можна приручити.");
            string name = SitOn.NameOf(c);
            if (c is Player) return Lang.T("Людей Мунін не приборкує. Хоча хотів би.");
            if (c.IsDead()) return Lang.F("{0} вже нікого не слухає.", name);
            Tameable t = c.GetComponent<Tameable>();
            if (t == null || c.IsBoss()) return Lang.F("{0} не приручається.", name);
            if (c.IsTamed()) return Lang.F("{0}: це вже ручна тварина.", name);
            return null;
        }

        // From RavenSkills (its checks done: Munin on the shoulder, awake, his pause over)
        internal static void Begin(Player p, ItemDrop.ItemData munin)
        {
            Camera cam = Utils.GetMainCamera();
            Character c = cam != null ? SitOn.CharacterAt(cam.transform, p, Range) : null;
            string why = WhyNot(c);
            if (why != null) { RavenMsg.Center(p, why); return; }
            float cost = Cost * GiftUp.CostMul("mtame");
            ItemDrop.ItemData it;
            why = Perches.WhyNot(p, true, out it); // (sulking, still flying in, busy in the air: he would not go, and nothing is paid)
            if (why != null) { RavenMsg.Center(p, why); return; }
            if (Power.Current(true) < Mathf.Round(Power.Price(true, cost))) { Power.Spend(p, true, cost); return; } // (it says why itself)
            PerchView v;
            why = SitOn.Go(p, true, c, false, SitOn.BeastHead, true, out v);
            if (why != null) { RavenMsg.Center(p, why); return; }
            if (!Power.Spend(p, true, cost)) return;
            _paid = Power.Price(true, cost);
            _began = Time.time;
            RavenSkills.StartCooldown("mtame");
            _target = c.GetZDOID();
            _token = HitInfo.Token(c);
            _until = Time.time + Duration();
            _buckAt = Time.time + UnityEngine.Random.Range(15f, 25f);
            _seatedAt = -1f;
            Send(_target, WorldTime.Now + Duration(), SpeedNow(), Facets.On("mtame"));
            Say(p, "mtame", Lang.T("Ану стій, худобо! Зараз я тебе виховаю."));
            RavenMsg.Center(p, Lang.F("Мунін летить приборкувати: {0}.\nПоки він сидить, приручення йде в {1} швидше{2}.", SitOn.NameOf(c), Times(), Facets.On("mtame") ? Lang.T(", а звір ситий") : ""));
            if (Plugin.Instance != null) Plugin.Instance.Log("Munin tames " + Utils.GetPrefabName(c.gameObject) + " for " + Duration() + " s, x" + SpeedNow());
        }

        // pressed again: Munin comes home now (still on his way there: he lands first, then can be called)
        internal static void Recall(Player p)
        {
            if (_seatedAt < 0f) { RavenMsg.Center(p, Lang.T("Мунін ще летить до звіра — натисни, коли сяде.")); return; }
            End(p, "mtame_end", true);
        }

        // From RavenSkills' half-second tick (your game)
        internal static void Tick(Player p)
        {
            Forget();
            if (!Mine() || p == null) return;
            ItemDrop.ItemData m = RavenSeat.On(p, true);
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(_target) : null;
            Character c = go != null ? go.GetComponent<Character>() : null;
            if (m == null || p.IsDead()) { End(p, null, false); return; }
            if (!Perches.On(m))
            {
                if (_seatedAt >= 0f) { End(p, null, false); return; } // called home another way
                if (Time.time - _began > 6f) // never got there (the seat did not answer): the strength and the pause back
                {
                    Power.Refund(m, _paid);
                    RavenSkills.LetOff("mtame", 1f);
                    End(p, null, false);
                }
                return;
            }
            if (_seatedAt < 0f)
            {
                _seatedAt = Time.time;
                Satiety.Flew(true, SitHere.Hunger); // he got there: the flight makes him hungry, and it was done together
                Trust.Ordered(true);
            }
            if (c == null || c.IsDead()) { End(p, null, true); return; }
            if (c.IsTamed()) { Twists.MarkTamed(c); Nicks.OnTamed(p); End(p, "mtame_done", true); return; }
            if (Time.time >= _until) { End(p, "mtame_end", true); return; }
            if (Time.time >= _buckAt)
            {
                // it bucks: a caw, feathers, and Munin's mouth
                _buckAt = Time.time + UnityEngine.Random.Range(15f, 25f);
                if (UnityEngine.Random.value < 0.5f)
                {
                    Vector3 head = Body.Head(c) + Vector3.up * 0.3f;
                    ScreamFx.CawAt(c.transform, head, 0.8f, 0.9f, false);
                    Raven tpl = Shoulder.Template(true);
                    if (tpl != null && tpl.m_despawnEffect != null) RavenFx.Own(tpl.m_despawnEffect, head, Quaternion.identity, null, 0.4f, -1);
                    Say(p, "mtame_buck", null);
                }
            }
        }

        private static void End(Player p, string tag, bool callHome)
        {
            ZDOID id = _target;
            _target = ZDOID.None;
            if (id != ZDOID.None) Send(id, 0.0, 1f, false);
            if (p == null) return;
            ItemDrop.ItemData m = RavenSeat.On(p, true);
            if (callHome && m != null && Perches.On(m) && Perches.OnSpot(m)) Perches.CallSilent(p, m, true);
            if (tag != null) Say(p, tag, null);
            if (tag == "mtame_done") RavenMsg.Center(p, Lang.T("Приборкано! Мунін летить назад, страшенно гордий."));
        }

        // a line from Munin (wherever he sits: the bubble follows the bird), {beast} the one he tames
        private static void Say(Player p, string tag, string fallback)
        {
            string line = null;
            if (Plugin.Instance != null)
            {
                Composer.Extra.Clear();
                try { Composer.ExtraCreature("beast", _token); line = Plugin.Instance.ComposeTagged(p, true, tag); }
                finally { Composer.Extra.Clear(); }
            }
            if (string.IsNullOrEmpty(line)) line = fallback;
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, true, line);
        }

        // ---------------------------------------------------------------- every game: the beasts being broken in

        private static void Send(ZDOID id, double until, float mul, bool fed)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(id);
            pkg.Write(until);
            pkg.Write(mul);
            pkg.Write(fed);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnTame(long sender, ZPackage pkg)
        {
            ZDOID id = pkg.ReadZDOID();
            double until = pkg.ReadDouble();
            float mul = Mathf.Clamp(pkg.ReadSingle(), 1f, 50f);
            bool fed = pkg.ReadBool();
            if (until <= WorldTime.Now) { Boosted.Remove(id); return; }
            Boosted[id] = new Boost { Until = Mathf.Min((float)(until - WorldTime.Now), 600f) + WorldTime.Now, Mul = mul, Fed = fed }; // (never longer than a gift can last)
        }

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, new Action<long, ZPackage>(OnTame)); }

        private static float _forgetAt;
        private static readonly List<ZDOID> Old = new List<ZDOID>();
        private static void Forget()
        {
            if (Boosted.Count == 0 || Time.time < _forgetAt) return;
            _forgetAt = Time.time + 5f;
            Old.Clear();
            foreach (KeyValuePair<ZDOID, Boost> kv in Boosted) if (kv.Value.Until <= WorldTime.Now) Old.Add(kv.Key);
            foreach (ZDOID id in Old) Boosted.Remove(id);
            List<Humanoid> gone = null;
            foreach (Humanoid h in Held.Keys) if (h == null) { if (gone == null) gone = new List<Humanoid>(); gone.Add(h); }
            if (gone != null) foreach (Humanoid h in gone) Held.Remove(h); // (unloaded meanwhile: nothing to give back)
        }

        private static Boost Of(Component c)
        {
            if (Boosted.Count == 0 || c == null) return null;
            ZNetView nv = c.GetComponent<ZNetView>();
            Boost b;
            if (nv == null || !nv.IsValid() || !Boosted.TryGetValue(nv.GetZDO().m_uid, out b)) return null;
            return b.Until > WorldTime.Now ? b : null;
        }

        private static readonly MethodInfo SetAlerted = AccessTools.Method(typeof(BaseAI), "SetAlerted", new Type[] { typeof(bool) });

        // the game's taming step (its owner, every 3 s): not alerted, fed with the rune III; how far it is, in the log
        private static float _loggedAt;
        internal static void BeforeTaming(Tameable t)
        {
            Boost b = Of(t);
            if (b == null) return;
            MonsterAI ai = t.GetComponent<MonsterAI>();
            if (ai != null && SetAlerted != null) SetAlerted.Invoke(ai, new object[] { false });
            ZNetView nv = t.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            if (b.Fed) nv.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);
            if (Time.time >= _loggedAt && Plugin.Instance != null)
            {
                _loggedAt = Time.time + 15f;
                float left = nv.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, t.m_tamingTime);
                Plugin.Instance.Log("Taming " + Utils.GetPrefabName(t.gameObject) + ": " + Mathf.RoundToInt((1f - Mathf.Clamp01(left / Mathf.Max(1f, t.m_tamingTime))) * 100f) + "% tame, x" + b.Mul.ToString("0.##") + (t.IsHungry() ? ", HUNGRY (no progress)" : "") + (ai != null && ai.IsAlerted() ? ", alerted" : ""));
            }
        }

        internal static float Mul(Tameable t) { Boost b = Of(t); return b != null ? b.Mul : 1f; }

        // it goes for nobody while Munin sits on it: the game's own «may not target» switch (its target search is skipped
        // whole, so it never turns on you between two looks), the target dropped, the alert let down; off again after
        internal static void Calm(MonsterAI ai)
        {
            Humanoid h = ai.GetComponent<Humanoid>();
            bool was;
            if (Of(ai) == null)
            {
                if (h != null && Held.TryGetValue(h, out was)) { h.m_aiCannotTargetOthers = was; Held.Remove(h); }
                return;
            }
            if (h != null && !Held.ContainsKey(h)) { Held[h] = h.m_aiCannotTargetOthers; h.m_aiCannotTargetOthers = true; }
            if (AiTarget.Of(ai) != null) AiTarget.Drop(ai);
            if (ai.IsAlerted()) AiTarget.Calm(ai);
        }

        internal static void Reset()
        {
            foreach (KeyValuePair<Humanoid, bool> kv in Held) if (kv.Key != null) kv.Key.m_aiCannotTargetOthers = kv.Value;
            Held.Clear(); Boosted.Clear(); _target = ZDOID.None;
        }
    }

    [HarmonyPatch(typeof(Tameable), "TamingUpdate")]
    internal static class TameUpdatePatch
    {
        private static void Prefix(Tameable __instance) { try { if (Tame.Any) Tame.BeforeTaming(__instance); } catch (Exception e) { PatchGuard.Fail("TameUpdatePatch", e); } }
    }

    [HarmonyPatch(typeof(Tameable), "DecreaseRemainingTime")]
    internal static class TameSpeedPatch
    {
        private static void Prefix(Tameable __instance, ref float time) { try { if (Tame.Any) time *= Tame.Mul(__instance); } catch (Exception e) { PatchGuard.Fail("TameSpeedPatch", e); } }
    }

    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class TameCalmPatch
    {
        private static void Prefix(MonsterAI __instance) { try { if (Tame.Any) Tame.Calm(__instance); } catch (Exception e) { PatchGuard.Fail("TameCalmPatch", e); } }
    }
}
#endif

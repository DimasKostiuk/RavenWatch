#if RAVEN_ITEM
using System.Collections;
using UnityEngine;

namespace HuginWhispers
{
    // «Камінь долі» at work (the user, 2026-10-23): you raise the stone to the raven on your shoulder, it flares violet
    // and breaks into sparks that arc into the bird; the raven freezes, opens its wings and trembles in a swirl of violet
    // feathers, shakes itself and answers its new fate (glad, bristling, turning away; a golden one in a gold flash).
    // Only then the new blessing and burden are told and the raven says its word. The fate itself is rolled at the click
    // (FateStone.Use): this is how it is shown. Everyone near sees it: the gesture (PlayerAnims, by the player's data),
    // the stone and the sparks (an RPC), the raven (its sitting acts, sent as always).
    // «Поклик Одіна» (OdinCall) is shown the same way in Odin's gold, and ends with the raven flying off to him.
    internal static class FateRite
    {
        private const string Rpc = "RavenWatch_FateRite";
        internal static float FlashAt = 0.9f, SparkSeconds = 0.6f, SwirlSeconds = 1.3f;
        internal static float TurnAt = 0.95f, ShakeAt = 2.85f, AnswerAt = 3.6f;
        internal static float GoodLuck = 0.25f; // the roll's luck (Perks.Luck) above this: glad; below its minus: a bad one
        internal const string TurnName = "Доля перекидається";
        internal static float CallOffAt = 3.4f; // Odin's call: the raven takes off once it has shaken itself (OdinCall.Tick)
        private const float SeeRange = 60f;

        private static float _busyUntil;
        internal static bool Busy { get { return Time.time < _busyUntil; } } // (a second stone waits for this one; by the clock: a coroutine stopped on logout never ends a flag)
        private static readonly System.Collections.Generic.Dictionary<long, float> HeardAt = new System.Collections.Generic.Dictionary<long, float>();

        internal static void Register() { ZRoutedRpc.instance.Register<ZPackage>(Rpc, OnRpc); }

        // the owner's game, right after the new fate was rolled: 'told' is the line with it
        internal static void Play(Player p, bool munin, ItemDrop.ItemData raven, string told)
        {
            float luck = Perks.Luck(raven);
            bool gold = Perks.Golden(raven);
            if (Plugin.Instance == null) { Tell(p, munin, raven, told, luck, gold); return; }
            Plugin.Instance.StartCoroutine(Run(p, munin, raven, told, luck, gold));
        }

        private static IEnumerator Run(Player p, bool munin, ItemDrop.ItemData raven, string told, float luck, bool gold)
        {
            _busyUntil = Time.time + AnswerAt + 0.5f;
            bool right = RightSide(p, munin);
            if (RavenSkills.Animations) PlayerAnims.Play(p, 2, right ? "fate_offer_r" : "fate_offer_l", 0.25f, 1f);
            FateShow.Make(p, munin, right, gold, false);
            Send(p, munin, right, gold, false);
            yield return new WaitForSeconds(TurnAt);
            Act(p, munin, TurnName);
            yield return new WaitForSeconds(ShakeAt - TurnAt);
            Act(p, munin, "Струшується");
            yield return new WaitForSeconds(AnswerAt - ShakeAt);
            Act(p, munin, Answer(munin, luck, gold));
            Tell(p, munin, raven, told, luck, gold);
        }

        // Odin's call: the stone raised, gold into the raven, its wings opened and shaken (it takes off at CallOffAt:
        // OdinCall.Tick)
        internal static void PlayCall(Player p, bool munin)
        {
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(RunCall(p, munin));
        }

        private static IEnumerator RunCall(Player p, bool munin)
        {
            _busyUntil = Time.time + CallOffAt + 0.5f;
            bool right = RightSide(p, munin);
            if (RavenSkills.Animations) PlayerAnims.Play(p, 2, right ? "fate_offer_r" : "fate_offer_l", 0.25f, 1f);
            FateShow.Make(p, munin, right, false, true);
            Send(p, munin, right, false, true);
            yield return new WaitForSeconds(TurnAt);
            Act(p, munin, TurnName);
            yield return new WaitForSeconds(ShakeAt - TurnAt);
            Act(p, munin, "Струшується");
        }

        // the raven on the shoulder: on your right (the hand that raises the stone) or your left
        private static bool RightSide(Player p, bool munin)
        {
            GameObject m = Shoulder.ModelOf(p, munin);
            if (m == null) return !munin;
            return Vector3.Dot(m.transform.position - p.transform.position, p.transform.right) >= 0f;
        }

        // its act now, whatever it was doing (sent to the others as every act)
        private static void Act(Player p, bool munin, string name)
        {
            if (p == null) return;
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            SitTemplate t = SitTemplates.Find(name);
            if (life == null || t == null || !t.For(munin)) return;
            life.SitDirectorNow.PlayNow(life, t);
        }

        private static string Answer(bool munin, float luck, bool gold)
        {
            if (gold) return "Тріумфує";
            if (luck > GoodLuck) return "Радіє здобичі";
            if (luck < -GoodLuck) return munin ? "Наїжачився" : "Відвертається";
            return "Розпушився";
        }

        private static void Tell(Player p, bool munin, ItemDrop.ItemData raven, string told, float luck, bool gold)
        {
            if (p == null) return;
            RavenMsg.Center(p, told);
            if (gold) { PerkFx.GoldRolled(p, munin, raven); return; } // (its own gold line, caw and word)
            PerkFx.Say(munin, luck > GoodLuck ? "fate_good" : luck < -GoodLuck ? "fate_bad" : "fate_meh", 1f, 0f);
        }

        // ---------------------------------------------------------------- the others near

        private static void Send(Player p, bool munin, bool right, bool gold, bool odin)
        {
            if (ZRoutedRpc.instance == null || p == null || Shadow.On(p)) return; // (in Hugin's shadow nobody may find you by it)
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetZDOID());
            pkg.Write(munin); pkg.Write(right); pkg.Write(gold); pkg.Write(odin);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pkg);
        }

        private static void OnRpc(long sender, ZPackage pkg)
        {
            if (sender == ZDOMan.GetSessionID()) return;
            ZDOID id = pkg.ReadZDOID();
            bool munin = pkg.ReadBool(), right = pkg.ReadBool(), gold = pkg.ReadBool();
            bool odin = pkg.GetPos() < pkg.Size() && pkg.ReadBool(); // (Odin's call; a game from before it sends no such word)
            Player me = Player.m_localPlayer;
            GameObject g = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            Player who = g != null ? g.GetComponent<Player>() : null;
            if (who == null || me == null || who == me || id.UserID != sender) return; // (only the player's own game shows its stone)
            float at;
            if (HeardAt.TryGetValue(sender, out at) && Time.time - at < AnswerAt) return; // (one at a time from a game: no flood of flares)
            HeardAt[sender] = Time.time;
            if (HeardAt.Count > 64) HeardAt.Clear();
            if (Vector3.Distance(me.transform.position, who.transform.position) > SeeRange || Shadow.Flagged(who)) return;
            FateShow.Make(who, munin, right, gold, odin);
        }
    }

    // What every game near draws of it: the flare in the raised hand, the sparks into the raven, the violet feather
    // swirl round it, and the gold flash of a golden blessing (Odin's call: all of it in Odin's gold). Gone by itself.
    internal class FateShow : MonoBehaviour
    {
        internal static readonly Color Blue = new Color(0.45f, 0.65f, 1f), Red = new Color(1f, 0.3f, 0.25f), Gold = new Color(1f, 0.82f, 0.38f);
        private static readonly Color OdinGold = new Color(1f, 0.74f, 0.25f), Pale = new Color(1f, 0.95f, 0.75f);
        private Player _p;
        private bool _munin, _right, _gold, _odin, _flashed, _swirled, _goldDone;
        private Color Tint { get { return _odin ? OdinGold : FateStoneLook.Violet; } }
        private Color DotA { get { return _odin ? OdinGold : Blue; } }
        private Color DotB { get { return _odin ? Pale : Red; } }
        private float _t;
        private Transform _hand, _spine, _bird;
        private Light _light;
        private ParticleSystem _sparks;
        private Vector3 _sparkFrom;

        internal static void Make(Player p, bool munin, bool right, bool gold, bool odin)
        {
            if (p == null) return;
            GameObject go = new GameObject("RavenWatch_FateShow");
            FateShow s = go.AddComponent<FateShow>();
            s._p = p; s._munin = munin; s._right = right; s._gold = gold; s._odin = odin;
            Animator an = p.GetComponentInChildren<Animator>();
            s._hand = PlayerAnims.SkeletonBone(p, an != null ? an.transform : p.transform, right ? "RightHand" : "LeftHand"); // (the body's own hand: not another thing of that name on you)
        }

        private Vector3 HandAt { get { return _hand != null ? _hand.position : _p.transform.position + Vector3.up * 1.4f; } }

        // the raven's middle (its back bone), or the shoulder
        private Vector3 RavenAt(out Transform bird)
        {
            GameObject m = Shoulder.ModelOf(_p, _munin);
            bird = m != null ? m.transform : null;
            if (bird != _bird) { _bird = bird; _spine = bird != null ? Utils.FindChild(bird, "Spine1", Utils.IterativeSearchType.DepthFirst) : null; } // (looked up again only for another bird)
            return _spine != null ? _spine.position : bird != null ? bird.position + Vector3.up * 0.08f : _p.transform.position + Vector3.up * 1.6f;
        }

        private void Update()
        {
            if (_p == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _t += dt;
            if (!_flashed && _t >= FateRite.FlashAt) Flash();
            if (_light != null) { _light.intensity = Mathf.MoveTowards(_light.intensity, 0f, dt * 6f); _light.transform.position = HandAt; }
            if (_sparks != null)
            {
                Transform bird;
                Vector3 to = RavenAt(out bird);
                float k = Mathf.Clamp01((_t - FateRite.FlashAt) / FateRite.SparkSeconds), e = k * k * (3f - 2f * k);
                _sparks.transform.position = Vector3.Lerp(_sparkFrom, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.35f; // a little arc into the bird
                if (k >= 1f && _sparks.isEmitting) _sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!_swirled && _t >= FateRite.FlashAt + FateRite.SparkSeconds) Swirl();
            if (_gold && !_goldDone && _t >= FateRite.AnswerAt) { _goldDone = true; Transform b; Burst(RavenAt(out b), Gold, Gold, 50, 1.6f); }
            if (_t > FateRite.AnswerAt + 2.5f) Destroy(gameObject);
        }

        // a violet flash in the raised hand (no stone shown: the user, 2026-10-24), a burst, the sparks set off for the raven
        private void Flash()
        {
            _flashed = true;
            Vector3 at = HandAt;
            GameObject lg = new GameObject("light");
            lg.transform.SetParent(transform, false);
            lg.transform.position = at;
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point; _light.color = Tint; _light.range = 4f; _light.intensity = 3f; _light.shadows = LightShadows.None;
            Burst(at, DotA, DotB, 40, 1.2f);
            RavenFx.Play("sfx_offering", at, Quaternion.identity);
            _sparkFrom = at;
            _sparks = Dots("sparks", at, true);
            if (_sparks != null)
            {
                ParticleSystem.EmissionModule em = _sparks.emission;
                em.rateOverTime = 90f;
                _sparks.Play();
            }
        }

        // round the raven: violet feathers circling up (and a few motes)
        private void Swirl()
        {
            _swirled = true;
            Transform bird;
            Vector3 at = RavenAt(out bird);
            Transform holder = bird != null ? bird : transform;
            ParticleSystem f = Feathers.Make(holder, "RavenWatch_FateFeathers", holder.InverseTransformPoint(at));
            if (f != null)
            {
                f.transform.SetParent(transform, true); // (gone with this, wherever the bird goes)
                ParticleSystem.MainModule main = f.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.duration = FateRite.SwirlSeconds;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(Tint, DotA, 0.35f), Color.Lerp(Tint, DotB, 0.35f));
                main.maxParticles = 60;
                ParticleSystem.ShapeModule sh = f.shape;
                sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.22f; sh.rotation = new Vector3(90f, 0f, 0f);
                ParticleSystem.VelocityOverLifetimeModule v = f.velocityOverLifetime;
                v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
                v.orbitalY = new ParticleSystem.MinMaxCurve(5f, 7f);
                v.y = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                v.x = new ParticleSystem.MinMaxCurve(0f, 0f); v.z = new ParticleSystem.MinMaxCurve(0f, 0f); // (all three in one mode)
                v.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f); v.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
                ParticleSystem.EmissionModule em = f.emission;
                em.rateOverTime = 35f;
                f.Play();
            }
            Burst(at, Tint, Color.white, 20, 0.6f);
        }

        // a burst of soft motes from one point
        private void Burst(Vector3 at, Color a, Color b, int count, float speed)
        {
            ParticleSystem ps = Dots("burst", at, false);
            if (ps == null) return;
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            ps.Emit(count);
        }

        // our soft dots (RavenAura's mote), fading out; 'trail': a moving emitter leaving them behind
        private ParticleSystem Dots(string name, Vector3 at, bool trail)
        {
            Material m = RavenAura.Dot();
            if (m == null) return null;
            GameObject g = new GameObject(name);
            g.transform.SetParent(transform, false);
            g.transform.position = at;
            ParticleSystem ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = trail; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(DotA, DotB);
            main.maxParticles = 150;
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.04f;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Tint, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true; col.color = new ParticleSystem.MinMaxGradient(fade);
            ParticleSystemRenderer pr = g.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = m;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
            return ps;
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections;
using UnityEngine;

namespace HuginWhispers
{
    // You by the fire with your ravens (the user, 2026-10-28: «комплексно та гарно»):
    //   - a story (Cozy): your head goes to the teller and stays on it while it speaks, nodding now and then
    //     (story_listen, loops on the listen layer); then, as the story was, you take it: a laugh, a proud chin,
    //     a sheepish rub of the neck, a thoughtful nod with the hand at the chin (fire_<mood>_, seated: _sit_)
    //   - your two talking (TalkBubble): the head goes from one speaker to the other; by the fire, at the end, a laugh or a nod
    //   - standing in the warmth between stories: now and then both palms to the fire, a rub of the hands (fire_warm)
    //   - seated a while: now and then one of your ravens sits down on the ground by you, facing the flames (Down)
    // Your own game only (others see it by your data); the gestures through Gestures (an Answer; the warmth Ambient).
    internal static class Fireside
    {
        internal static float EndChance = 0.8f, TalkEndChance = 0.5f, WarmMin = 45f, WarmMax = 100f, HeadFade = 0.4f;

        private static float _headUntil = -1f, _endAt = -1f, _nextWarm;
        private static bool _endMunin;
        private static string _mood;

        internal static bool Telling { get { return Time.time < _headUntil; } } // a story or a talk going on: no palm or wave over it

        // Cozy.Tell, before the raven speaks: its memory's kind tells how you take it
        internal static void Story(Player p, bool munin, float seconds, string kind)
        {
            if (!Mine(p)) return;
            Head(p, munin, seconds);
            _mood = Mood(kind);
            _endMunin = munin;
            _endAt = Time.time + seconds + 0.4f;
        }

        // a line of your two's talk (TalkBubble.Say)
        internal static void Line(Player p, bool munin, float seconds, bool last)
        {
            if (!Mine(p)) return;
            Head(p, munin, seconds);
            _endAt = -1f;
            if (!last || !Cozy.Cosy || Random.value >= TalkEndChance) return;
            _mood = Random.value < 0.6f ? "laugh" : "nod";
            _endMunin = munin;
            _endAt = Time.time + seconds + 0.4f;
        }

        // from the skills' Update (your own game)
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            float now = Time.time;
            if (_headUntil > 0f && (now >= _headUntil || p.InAttack() || p.InWater() || OdinWings.Flying())) { _headUntil = -1f; BodyClips.Stop(p, BodyClips.ListenSlot, 0.6f); }
            if (_endAt > 0f && now >= _endAt)
            {
                _endAt = -1f;
                if (Random.value < EndChance) Gestures.Play(p, _endMunin, Gestures.Seated(p) ? "fire_" + _mood + "_sit_" : "fire_" + _mood + "_", Gestures.Pri.Answer, true, Gestures.Seated(p));
            }
            DownTick(p, now);
            if (now < _nextWarm) return;
            _nextWarm = now + 2f;
            if (!Cozy.Cosy || Telling || Gestures.Seated(p) || p.GetVelocity().magnitude > 0.3f || !RavenSkills.Animations) return;
            Vector3 f;
            if (!FireAt(p, out f)) return;
            if (Gestures.Play(p, false, "fire_warm", Gestures.Pri.Ambient, false)) _nextWarm = now + Random.Range(WarmMin, WarmMax);
        }

        // ---- a raven down by you at the fire (the user, 2026-10-28): you sit by the warmth a while, and now and then one
        // of yours flies off the shoulder and sits on the ground by you, facing the flames: warms its wings, dozes,
        // preens, now and then tosses a twig in (a spark). Back on you when you get up, or by itself after a while; one
        // at a time. The owner decides and writes it in the player's data (every game: Shoulder.Hovering for that raven
        // → HoverPlan's ground way, by the fire).
        internal static float DownAfter = 30f, DownCheck = 20f, DownChance = 0.3f, DownMin = 60f, DownMax = 150f, DownGapMin = 120f, DownGapMax = 240f, TwigChance = 0.2f;
        private static readonly int DownKey = "RavenWatch_FireDown".GetStableHashCode();
        private static float _sitSince = -1f, _downCheckAt, _downUntil = -1f, _downLookAt;
        private static int _down = -1; // the owner's: 0 Hugin, 1 Munin down by the fire, -1 none
        private static bool _twigDone;

        // that raven of this player is down by the fire now (any game: the owner's word in the player's data)
        internal static bool Down(Player p, bool munin)
        {
            ZDO z = p != null ? NetFlag.Zdo(p, false) : null;
            return z != null && z.GetInt(DownKey, 0) == (munin ? 2 : 1);
        }

        // the fire whose warmth you are in (its middle)
        internal static bool FireAt(Player p, out Vector3 at)
        {
            EffectArea a = p != null ? EffectArea.IsPointInsideArea(p.transform.position, EffectArea.Type.Heat, 1f) : null;
            at = a != null ? a.transform.position : Vector3.zero;
            return a != null;
        }

        private static void DownTick(Player p, float now)
        {
            if (now < _downLookAt) return;
            _downLookAt = now + 0.5f; // (the warmth is a physics look: twice a second)
            Vector3 f;
            bool seat = Gestures.Seated(p) && RavenSkills.Animations && !OdinWings.Flying() && FireAt(p, out f);
            if (!seat) { _sitSince = -1f; if (_down >= 0) SetDown(p, -1); return; }
            if (_sitSince < 0f) _sitSince = now;
            if (_down >= 0)
            {
                if (now >= _downUntil || Orders.Of(p, _down == 1) != Orders.None) { SetDown(p, -1); _downCheckAt = now + Random.Range(DownGapMin, DownGapMax); }
                return;
            }
            if (!Cozy.Cosy || now - _sitSince < DownAfter || now < _downCheckAt) return;
            _downCheckAt = now + DownCheck;
            if (Telling || Random.value >= DownChance) return;
            bool munin = Random.value < 0.5f;
            if (!Free(p, munin)) { munin = !munin; if (!Free(p, munin)) return; }
            GoDown(p, munin);
        }

        private static bool Free(Player p, bool munin)
        {
            return Shoulder.Arrived(p, munin) && Orders.Of(p, munin) == Orders.None && Outing.Of(p, munin) == Outing.None && !(munin && Beast.On()) && !Mead.Tipsy(munin);
        }

        private static void GoDown(Player p, bool munin)
        {
            _downUntil = Time.time + Random.Range(DownMin, DownMax);
            _twigDone = false;
            SetDown(p, munin ? 1 : 0);
        }

        private static void SetDown(Player p, int who)
        {
            _down = who;
            ZDO z = NetFlag.Zdo(p, true);
            if (z != null) z.Set(DownKey, who + 1);
        }

        // it came down by the fire (HoverPlan, the owner's game): you glance at it
        internal static void Landed(Player p, bool munin)
        {
            if (Mine(p)) BodyClips.Listen(p, munin);
        }

        // what it does there now and then (a sitting act, the owner's game; sent as those are)
        internal static string GroundAct(Player p, bool munin)
        {
            float r = Random.value;
            if (!_twigDone && r < TwigChance && Plugin.Instance != null) { _twigDone = true; Plugin.Instance.StartCoroutine(Twig(p)); return "Клює землю"; }
            r = Random.value;
            return r < 0.35f ? "Дрімає" : r < 0.6f ? "Потягується" : r < 0.85f ? "Чистить пір'я" : "Позіхає";
        }

        // a twig picked up and tossed in: the fire answers with a spark
        private static IEnumerator Twig(Player p)
        {
            yield return new WaitForSeconds(0.9f);
            Vector3 f;
            if (p == null || !FireAt(p, out f)) yield break;
            RavenFx.Play("vfx_FireAddFuel", f + Vector3.up * 0.2f, Quaternion.identity);
            RavenFx.Play("sfx_FireAddFuel", f, Quaternion.identity);
        }

        // studio: `react down [h|m]` — that raven down by the fire now (you seated by one), `react up` — back
        internal static string StudioDown(Player p, bool munin, bool down)
        {
            if (!down) { if (_down >= 0) SetDown(p, -1); return "back on you"; }
            Vector3 f;
            if (!Gestures.Seated(p) || !FireAt(p, out f)) return "sit by a fire first";
            if (!Free(p, munin)) return Plugin.RavenName(munin) + " is not free (not on you, an order, an outing, tipsy)";
            if (_down >= 0) SetDown(p, -1);
            _sitSince = Time.time - DownAfter;
            GoDown(p, munin);
            return Plugin.RavenName(munin) + " down by the fire for " + Mathf.RoundToInt(_downUntil - Time.time) + " s";
        }

        private static bool Mine(Player p) { return p != null && ReferenceEquals(p, Player.m_localPlayer) && RavenSkills.Animations && !OdinWings.Flying(); }

        // the head on the speaker for as long as it speaks (a story over a short Listen: it takes the layer)
        private static void Head(Player p, bool munin, float seconds)
        {
            if (p.InAttack() || p.InWater()) return;
            BodyClips.Play(p, BodyClips.ListenSlot, Gestures.RightSide(p, munin) ? "story_listen_r" : "story_listen_l", HeadFade, Random.Range(0.9f, 1.1f));
            _headUntil = Time.time + Mathf.Max(2f, seconds);
        }

        // a memory's kind (Memory.Add) → how you take it
        private static string Mood(string kind)
        {
            string m;
            switch (kind)
            {
                case "death": case "fdeath": case "fall": case "badday": case "grudge": case "tomb": m = "sheepish"; break;
                case "boss": case "slaughter": case "fight": case "tame": case "metal": m = "proud"; break;
                case "biome": case "dungeon": case "voyage": case "home": m = "nod"; break;
                default: m = "laugh"; break; // (friends, today, the numbers: ravens tell those for the laugh)
            }
            return m != "laugh" && Random.value < 0.2f ? "laugh" : m; // (a raven's way of telling it: now and then funny anyway)
        }
    }
}
#endif

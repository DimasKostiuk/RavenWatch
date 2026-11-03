#if RAVEN_ITEM
using System.Collections;
using UnityEngine;

namespace HuginWhispers
{
    // You answer what your raven does (the user, 2026-10-28): a peck by the ear — you flinch and rub it; food stolen
    // from your bag — a late grab and a shaken fist; the shake after its bath — an elbow up against the spray, the
    // shoulder brushed off; your mead sipped — a look into the horn and at the thief; a tipsy hiccup — a hand to steady
    // it; now and then, after its word, a palm to the face or a wave of the hand; your raven in a quarrel — you hush it
    // and shrug; a boss down — a fist to the sky, the ravens up circling. All through the gestures' director (Gestures:
    // your own game, others see it by your data).
    internal static class PlayerReact
    {
        internal static float SipDelay = 1.6f;     // after the mead's own drinking
        internal static float SteadyChance = 0.35f; // a hand to the tipsy bird at its hiccup
        internal static float WordChance = 0.12f, WordEvery = 90f; // a palm to the face / a wave after its word
        private static float _wordAt;

        internal static void Ear(Player p, bool munin) { Play(p, munin, "react_ear_"); }
        internal static void Stolen(Player p, bool munin) { Play(p, munin, "react_steal_"); }
        internal static void Splashed(Player p, bool munin) { Play(p, munin, "react_shake_"); }

        // a React for the gestures' director: over a stroke or an order, waits a moment for another reaction, plays running too
        // seated (a chair, the sitting emote): its seated copy when it has one (the sip), else nothing
        private static bool Play(Player p, bool munin, string clip)
        {
            bool sit = Gestures.Seated(p);
            if (sit) return BodyClips.Find(clip + "sit_r") != null && Gestures.Play(p, munin, clip + "sit_", Gestures.Pri.React, true, true);
            return Gestures.Play(p, munin, clip, Gestures.Pri.React);
        }

        internal static void Sipped(Player p, bool munin, float later)
        {
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(Later(p, munin, "react_sip_", SipDelay + later));
        }

        internal static void Hiccup(Player p, bool munin)
        {
            if (Random.value < SteadyChance) Play(p, munin, "react_steady_");
        }

        // after a word of the bird on you (RavenSkills.Speak): played, it takes the place of Listen's head turn
        internal static bool Word(Player p, bool munin)
        {
            if (Time.time < _wordAt || Fireside.Telling || Random.value >= WordChance) return false;
            GameObject m = Shoulder.ModelOf(p, munin);
            if (m == null || AnimDirector.Of(m) != RavenAct.Sit) return false; // (only a bird sitting on you)
            BodyAnimator a = BodyAnimator.For(p);
            if (a == null || a.Playing(2)) return false; // (an aside: now or never, not waiting for the layer)
            if (!Gestures.Play(p, munin, Random.value < 0.5f ? "react_palm_" : "react_wave_", Gestures.Pri.Answer)) return false; // (an aside: never over a reaction)
            _wordAt = Time.time + WordEvery;
            return true;
        }

        // your raven and someone else's quarrelling (Bonds.Squabble): you hush yours, then shrug at the world
        internal static float QuarrelDelay = 0.9f;
        internal static void Quarrel(Player p, bool munin)
        {
            if (Plugin.Instance != null && p == Player.m_localPlayer) Plugin.Instance.StartCoroutine(Later(p, munin, "react_quarrel_", QuarrelDelay));
        }

        // a boss down with you there (Strike.OnBossDown): a fist to the sky, your ravens up circling you with a caw
        internal static float BossDelay = 1.2f, BossCircle = 9f;
        internal static void BossDown(Player p)
        {
            if (Plugin.Instance != null && p == Player.m_localPlayer && !p.IsDead()) Plugin.Instance.StartCoroutine(Victory(p));
        }

        private static IEnumerator Victory(Player p)
        {
            yield return new WaitForSeconds(BossDelay);
            if (p == null || p.IsDead() || p != Player.m_localPlayer) yield break;
            Gestures.Play(p, false, "boss_win_", Gestures.Pri.Moment);
            bool any = false;
            foreach (bool munin in new[] { false, true })
            {
                if (!Shoulder.Arrived(p, munin) || Orders.Of(p, munin) != Orders.None || p.IsAttached() || p.IsRiding() || p.InWater() || Outing.Indoors()) continue;
                if (Beast.On() && munin) continue;
                Orders.FlyWith(p, munin, BossCircle + 5f, Orders.Circle);
                if (Orders.Of(p, munin) != Orders.Circle) continue;
                ScreamFx.CawAll(p, munin, munin ? 0.85f : 1.3f, 0.7f);
                any = true;
            }
            if (!any) yield break;
            yield return new WaitForSeconds(BossCircle);
            if (p == null) yield break;
            foreach (bool munin in new[] { false, true }) // (home quietly, unless you gave it an order of your own since)
                if (Orders.Of(p, munin) == Orders.Circle && Orders.TimeLeft(munin) <= 6f) Orders.FlyWithEnd(p, munin, Orders.Circle);
        }

        private static IEnumerator Later(Player p, bool munin, string clip, float sec)
        {
            yield return new WaitForSeconds(sec);
            Play(p, munin, clip);
        }

        // studio: `react <ear|steal|shake|sip|steady|palm|wave|quarrel> [h|m]` — the gesture at once (no chance, no gap);
        // `react boss` — the whole boss-down moment; `react fire_<laugh|proud|sheepish|nod> [h|m]` — a story's end (seated if you sit);
        // `react story [h|m]` — the head on the teller for 8 s, then its end; `react warm` — palms to the fire;
        // `react down [h|m]` / `react up` — that raven down by the fire by you (seated) / back
        internal static string Studio(string what, bool munin)
        {
            Player p = Player.m_localPlayer;
            if (p == null) return "no player";
            if (what == "boss") { BossDown(p); return "boss-down moment in " + BossDelay + " s"; }
            if (what == "story") { Fireside.Story(p, munin, 8f, "near"); return "head on " + Plugin.RavenName(munin) + " for 8 s, then an end (by chance)"; }
            if (what == "down" || what == "up") return Fireside.StudioDown(p, munin, what == "down");
            if (what == "warm") return Gestures.Play(p, false, "fire_warm", Gestures.Pri.Moment, false) ? "played fire_warm" : "not now";
            if (what.StartsWith("fire_")) { string mood = what.Substring(5); bool sit = Gestures.Seated(p); return Gestures.Play(p, munin, "fire_" + mood + (sit ? "_sit_" : "_"), Gestures.Pri.Moment, true, sit) ? "played fire_" + mood + (sit ? " (seated)" : "") : "not now"; }
            string clip = "react_" + what + "_";
            if (BodyClips.Find(clip + "r") == null) return "no clip " + clip + "r";
            return Gestures.Play(p, munin, clip, Gestures.Pri.Moment) ? "played " + clip + " (" + Plugin.RavenName(munin) + ")" : "not now (another gesture, flight, attack or block)";
        }
    }
}
#endif

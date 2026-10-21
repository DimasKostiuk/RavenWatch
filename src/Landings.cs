#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // One landing's own way (Landings.Pick, for every landing on a shoulder or a perch): where it comes in from, how far
    // out it starts braking, at what pace the game's landing clip plays, a flight template on the last way in, a
    // landing template laid over the clip (its wing pose, from the «Шаблони» tab, situation «Посадка»), a tilt (one
    // wing up, the other down), how far apart from the other raven it may touch down and what it does once it sits.
    // The clip itself (and its legs) stays as it is. The ranges come from the raven's character (FlightCharacter).
    internal sealed class LandStyle
    {
        public float Back, Up, Side;      // the approach point (FlightBody.HomeAim), m: behind you, above the shoulder, out to its side
        public float Flare;               // m off the point over the seat where it starts braking
        public float ClipRate;            // × the landing clip's pace
        public FlightTemplate Arrive;     // the flight template on the last way in (null: plain)
        public FlightTemplate Pose;       // the landing template over the clip (null: the game's own landing)
        public float Tilt;                // ° one wing up, the other down (signed)
        public float Apart;               // s at least between its touch-down and the other raven's (a few come nearly together)
        public int Settle;                // once it sits: 0 nothing, 1 a hop, 2 a beat of the wings, 3 a step aside, 4 a hop and a beat
        public float Phase;               // its own beat phase (two ravens never beat as one)
        internal static readonly string[] SettleNames = { "нічого", "підскок", "змах", "крок убік", "підскок і змах" };
        internal string Describe()
        {
            return (Pose != null ? Pose.Name : "як у грі") + (Mathf.Abs(Tilt) > 1f ? ", крен " + Mathf.RoundToInt(Tilt) + "°" : "") + ", темп ×" + ClipRate.ToString("0.00")
                + ", гальмує з " + Flare.ToString("0.0") + " м, підліт " + (Arrive != null ? Arrive.Name : "без шаблону") + ", потім " + SettleNames[Mathf.Clamp(Settle, 0, 4)];
        }
    }

    // One take-off's own way (Landings.PickTakeoff): it crouches a moment (its wings unfolding, the shoulder first,
    // then the elbow and the hand), then springs off into its first downstroke; how long, how deep, how hard, a little
    // to which side, and (beside the other raven leaving at once) a moment later or nearly together.
    internal sealed class TakeoffStyle
    {
        public float Hold;                // s on the shoulder before it springs off (the crouch and the unfold, plus its wait)
        public float Crouch;              // s of that which is the crouch itself
        public float Depth;               // m it crouches down
        public float Push;                // × the push-off speed
        public float Veer;                // ° it pushes off to one side of its way
        public float Unfold;              // s the elbow follows the shoulder in unfolding (the hand twice that)
        internal string Describe() { return "присідає " + Mathf.RoundToInt(Crouch * 1000f) + " мс на " + Mathf.RoundToInt(Depth * 100f) + " см, чекає " + Mathf.RoundToInt((Hold - Crouch) * 1000f) + " мс, поштовх ×" + Push.ToString("0.00") + ", убік " + Mathf.RoundToInt(Veer) + "°"; }
    }

    // «Диспетчер посадки й зльоту»: the two ravens of one player (on the shoulders or on a perch) never land or leave
    // as one. Each landing and take-off gets its own way, unlike the other raven's if that one is doing the same about
    // now; a raven ready to touch down waits a moment if the other is touching down just then (mostly a good part of a
    // second apart, now and then nearly together, never longer than MaxHold); one leaving right after the other waits
    // its own moment too. Everything goes through here: RavenMotor (ReadyToLand, MayTouchNow, Launch) and
    // ShoulderLife.BeginLand. The ranges are each raven's character (FlightCharacter), the poses are templates.
    internal static class Landings
    {
        internal static float MaxHold = 0.8f;                 // s a ready raven waits at most for its turn
        internal static float TogetherChance = 0.25f;         // a pair now and then comes down (or leaves) nearly together
        internal static float TogetherMin = 0.06f, TogetherMax = 0.15f, ApartMin = 0.25f, ApartMax = 0.7f;
        internal static float PairSeconds = 5f;               // the other raven's landing counts as "about now" this long
        internal static float LeaveApartMin = 0.12f, LeaveApartMax = 0.4f; // s between two leaving at once
        internal static float LandBeatHz = 5f;                // the landing templates' beat (× their beat when their motion is on)

        private sealed class Seat
        {
            public float TouchAt = -10f, ReadySince = -1f, PickedAt = -10f, LeftAt = -10f;
            public LandStyle Style; public TakeoffStyle Leave;
        }
        private static readonly Dictionary<Player, Seat[]> Pairs = new Dictionary<Player, Seat[]>();
        private static float _prune;

        private static Seat[] PairOf(Player p)
        {
            if (Time.time >= _prune) { _prune = Time.time + 60f; Prune(); }
            Seat[] s;
            if (!Pairs.TryGetValue(p, out s)) { s = new[] { new Seat(), new Seat() }; Pairs[p] = s; }
            return s;
        }

        private static void Prune()
        {
            List<Player> gone = null;
            foreach (Player p in Pairs.Keys) if (p == null) { if (gone == null) gone = new List<Player>(); gone.Add(p); }
            if (gone != null) foreach (Player p in gone) Pairs.Remove(p);
        }

        private static float R(float a, float b) { return Random.Range(Mathf.Min(a, b), Mathf.Max(a, b)); }

        // ---------------------------------------------------------------- landing
        // this landing's way: its own raven's character, and unlike the other raven's landing about now
        internal static LandStyle Pick(Player owner, bool munin, FlightCharacter ch)
        {
            if (ch == null) ch = RavenFlight.CharOf(munin);
            LandStyle other = null;
            Seat[] pair = owner != null ? PairOf(owner) : null;
            if (pair != null) { Seat o = pair[munin ? 0 : 1]; if (Time.time - o.PickedAt < PairSeconds) other = o.Style; }
            LandStyle s = new LandStyle();
            // where it comes in from: higher or lower, from farther behind or nearly over the shoulder, wide or close
            s.Back = FlightBody.ApproachBack + R(ch.LandBackMin, ch.LandBackMax);
            s.Up = FlightBody.ApproachUp + R(ch.LandUpMin, ch.LandUpMax);
            s.Side = FlightBody.ApproachSide * R(ch.LandSideMin, ch.LandSideMax);
            if (other != null && Mathf.Abs(s.Up - other.Up) < 0.4f) // the other one came in at that height: this one higher or lower
                s.Up = other.Up > FlightBody.ApproachUp + (ch.LandUpMin + ch.LandUpMax) * 0.5f ? FlightBody.ApproachUp + ch.LandUpMin : FlightBody.ApproachUp + ch.LandUpMax;
            s.Flare = R(ch.FlareMin, ch.FlareMax);
            s.ClipRate = R(ch.ClipMin, ch.ClipMax);
            if (other != null && Mathf.Abs(s.ClipRate - other.ClipRate) < 0.08f) s.ClipRate += other.ClipRate >= 1f ? -0.12f : 0.12f;
            s.Tilt = R(-ch.LandTilt, ch.LandTilt);
            if (other != null && Mathf.Sign(s.Tilt) == Mathf.Sign(other.Tilt)) s.Tilt = -s.Tilt;
            s.Settle = Weighted(new[] { ch.SettleNone, ch.SettleHop, ch.SettleFlap, ch.SettleStep, ch.SettleHopFlap }, other != null ? other.Settle : -1);
            s.Phase = Random.value * Mathf.PI * 2f;
            s.Apart = Random.value < TogetherChance ? R(TogetherMin, TogetherMax) : R(ApartMin, ApartMax);
            // the templates (each raven its own weights): the last way in, and the pose over the clip; not the other's
            s.Arrive = FlightTemplates.Pick((int)FlightSit.Arrive, munin, other != null ? other.Arrive : null);
            s.Pose = FlightTemplates.Pick((int)FlightSit.Land, munin, other != null ? other.Pose : null);
            if (pair != null) { Seat me = pair[munin ? 1 : 0]; me.Style = s; me.PickedAt = Time.time; me.ReadySince = -1f; }
            return s;
        }

        // Ready to start the landing clip: now, or a moment later if the other raven touches down about when this one
        // would (never longer than MaxHold). 'lead': s from the clip's start to its touch-down at this landing's pace.
        internal static bool MayTouch(Player owner, bool munin, LandStyle s, float lead)
        {
            if (owner == null) return true;
            Seat[] pair = PairOf(owner);
            Seat me = pair[munin ? 1 : 0], other = pair[munin ? 0 : 1];
            if (me.ReadySince < 0f) me.ReadySince = Time.time;
            float touch = Time.time + Mathf.Max(0f, lead), apart = s != null ? s.Apart : ApartMin;
            bool ok = Mathf.Abs(touch - other.TouchAt) >= apart || Time.time - me.ReadySince >= MaxHold;
            if (ok) { me.TouchAt = touch; me.ReadySince = -1f; }
            return ok;
        }

        // ---------------------------------------------------------------- take-off
        // this take-off's way: its character, a strike quicker, unlike (and a moment after) the other raven leaving now
        internal static TakeoffStyle PickTakeoff(Player owner, bool munin, FlightCharacter ch, string plan)
        {
            if (ch == null) ch = RavenFlight.CharOf(munin);
            Seat[] pair = owner != null ? PairOf(owner) : null;
            Seat me = pair != null ? pair[munin ? 1 : 0] : null, o = pair != null ? pair[munin ? 0 : 1] : null;
            TakeoffStyle other = o != null && Time.time - o.LeftAt < 0.6f ? o.Leave : null;
            float quick = plan == "удар" ? 0.45f : plan == "купол" ? 0.7f : 1f; // a strike springs off almost at once
            TakeoffStyle s = new TakeoffStyle();
            s.Crouch = R(ch.CrouchMin, ch.CrouchMax) * quick;
            if (other != null && Mathf.Abs(s.Crouch - other.Crouch) < 0.05f) s.Crouch = other.Crouch > (ch.CrouchMin + ch.CrouchMax) * 0.5f * quick ? ch.CrouchMin * quick : ch.CrouchMax * quick;
            s.Depth = ch.CrouchDepth * R(0.7f, 1.2f);
            s.Push = ch.PushMul * R(0.9f, 1.1f);
            s.Veer = R(0f, ch.VeerMax) * (Random.value < 0.5f ? -1f : 1f);
            if (other != null && Mathf.Sign(s.Veer) == Mathf.Sign(other.Veer)) s.Veer = -s.Veer;
            s.Unfold = ch.Unfold * R(0.8f, 1.2f);
            float wait = 0f;
            if (other != null) // the other one is just leaving: this one its own moment after it (now and then nearly with it)
            {
                float gap = Random.value < TogetherChance ? R(0f, 0.05f) : R(LeaveApartMin, LeaveApartMax);
                wait = Mathf.Max(0f, (o.LeftAt + other.Hold + gap) - (Time.time + s.Crouch));
                wait = Mathf.Min(wait, MaxHold);
            }
            s.Hold = s.Crouch + wait;
            if (me != null) { me.Leave = s; me.LeftAt = Time.time; }
            return s;
        }

        private static int Weighted(float[] w, int avoid)
        {
            float total = 0f;
            for (int i = 0; i < w.Length; i++) if (i != avoid) total += Mathf.Max(0f, w[i]);
            if (total <= 0f) return 0;
            float r = Random.value * total;
            for (int i = 0; i < w.Length; i++)
            {
                if (i == avoid) continue;
                r -= Mathf.Max(0f, w[i]);
                if (r <= 0f) return i;
            }
            return avoid == 0 ? 1 : 0;
        }
    }
}
#endif

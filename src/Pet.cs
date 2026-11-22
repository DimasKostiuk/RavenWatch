#if RAVEN_ITEM
using UnityEngine;

namespace HuginWhispers
{
    // Standing a while, now and then you stroke one of your ravens (the user, 2026-10-24): the hand on its side goes up to
    // it and smooths its back, the bird leans into it (Підставляє шию), and sometimes it says a word (PetPatterns:
    // petted). Everyone near sees it: the gesture (BodyClips, by the player's data), the bird (its sitting acts).
    internal static class Pet
    {
        internal static float StillAfter = 25f;            // standing this long before the first stroke
        internal static float EveryMin = 70f, EveryMax = 160f; // then one stroke in this many seconds
        internal static float SayChance = 0.4f;            // the bird says something after it
        internal static float StillSpeed = 0.25f;          // m/s: slower than this is standing

        private static float _stillSince = -1f, _next, _look, _lookedAt;
        private static Vector3 _lastPos;

        // from the skills' Update, looked at four times a second (your own game only)
        internal static void Tick()
        {
            if (Time.time < _look) return;
            _look = Time.time + 0.25f;
            Player p = Player.m_localPlayer;
            if (p == null || !RavenSkills.Animations) { _stillSince = -1f; return; }
            Vector3 pos = p.transform.position;
            float span = Time.time - _lookedAt;
            float moved = span > 0f && span < 2f ? (pos - _lastPos).magnitude / span : 0f;
            _lastPos = pos; _lookedAt = Time.time;
            if (!Calm(p) || moved > StillSpeed) { _stillSince = -1f; Moments.Moved(); return; }
            if (_stillSince < 0f) { _stillSince = Time.time; _next = Time.time + StillAfter + Random.Range(0f, 10f); return; }
            Moments.Still(p, Time.time - _stillSince); // (your own small moves by the moment: the same standing calm)
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(EveryMin, EveryMax);
            Stroke(p);
        }

        // nothing else going on: on your feet, hands free of a swing, no gift in the air, no window open
        private static bool Calm(Player p)
        {
            if (p.IsDead() || !p.IsOnGround() || p.InWater() || p.IsAttached() || p.IsRiding() || p.InAttack() || p.IsBlocking() || p.InDodge() || p.IsCrouching() || p.InEmote() || p.IsSitting()) return false; // (an emote, sitting on the ground: the hands are busy)
            return !OdinWings.Flying() && !Beast.On() && !FateRite.Busy && !InventoryGui.IsVisible() && !Minimap.IsOpen() && !RavenSkills.Silenced(); // (Silenced: a story scene plays — he stroked his raven as the dome closed over him, the ambush 2026-11-14)
        }

        private static void Stroke(Player p)
        {
            bool h = Sits(p, false), m = Sits(p, true);
            if (!h && !m) return;
            float ph = h ? Norov.PetW(RavenSeat.On(p, false)) : 0f, pm = m ? Norov.PetW(RavenSeat.On(p, true)) : 0f;
            bool munin = h && m ? Random.value * (ph + pm) < pm : m; // («Лагідний» is the one stroked more)
            if (!BodyClips.Gesture(p, munin, "pet_", Gestures.Pri.Ambient)) return; // (another gesture under way: not now, nor its leaning in and word)
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            if (life != null && life.SitNew) life.SitDirectorNow.React(life, "Підставляє шию", 0.5f);
            Habits.Stroked(p, munin); // («Мазунчик»)
            if (Random.value < SayChance) PerkFx.Say(munin, "petted", 1f, 4f);
        }

        // a raven of yours sitting on your shoulder now (not flying, not on a perch, not busy with a strike)
        private static bool Sits(Player p, bool munin)
        {
            ShoulderLife life = Shoulder.LifeOf(p, munin);
            return life != null && life.SitNew && AnimDirector.Of(Shoulder.ModelOf(p, munin)) == RavenAct.Sit;
        }
    }
}
#endif

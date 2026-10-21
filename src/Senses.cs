#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // «Ситуація»: what is going on around you, gathered once for everyone who needs it (the animation director, the
    // outings, the ravens' eyes on the shoulder), instead of each of them asking the game again on its own. Twice a
    // second, cheap: the game already keeps most of it (the roof, the dungeon, the ship); the creatures around are
    // looked over once a second in one pass.
    internal static class Senses
    {
        internal static float FoeRange = 25f;     // a fight: an enemy this close
        internal static float LookRange = 20f;    // the nearest enemy the ravens watch (as ShoulderLife.LookRange)
        internal static float BossRange = 40f;
        internal static float TravelMeters = 150f; // walked this far in the last 2 minutes: on the road

        internal static bool Underground, Indoors, OnShip, AtHelm, Sailing, Swimming, Sleeping, Night, Afk, Encumbered;
        internal static bool Ready; // gathered at least once for the player now in the world
        internal static bool Traveling { get { return Walked > TravelMeters && !Fight && !Indoors && !Underground && !OnShip; } }
        // a fight is with someone: an enemy near, or a creature hit by you or hitting you lately. A swing alone is not one
        // (chopping, mining), nor is a scratch (cold, poison, a campfire underfoot).
        internal static bool Fight { get { float t = Time.time; return FoeNear || t - HitFoeAt < 6f || t - HurtByFoeAt < 6f; } }
        internal static bool FoeNear, Attacking, Jumped;
        internal static float HurtAt = -100f, Health01 = 1f, Walked;
        internal static float HitFoeAt = -100f, HurtByFoeAt = -100f; // set by the hit patches in Diary.cs
        internal static Character Foe, Boss;      // the nearest enemy within LookRange, a boss within BossRange
        internal static Heightmap.Biome Biome;

        private static float _nextScan, _hpWas = -1f;
        private static Vector3 _at;
        private static bool _atSet;
        private static readonly Queue<KeyValuePair<float, float>> Steps = new Queue<KeyValuePair<float, float>>(); // (time, metres)

        // from Shoulder.Tick (every 0.5 s)
        internal static void Tick()
        {
            Player p = Player.m_localPlayer;
            if (p == null) { _atSet = false; _hpWas = -1f; Ready = false; return; }
            float now = Time.time;
            Underground = p.InInterior();
            Indoors = p.InShelter();
            Ship helm = p.GetControlledShip();
            AtHelm = helm != null;
            Ship ship = p.GetStandingOnShip();
            if (ship == null) ship = helm;
            OnShip = ship != null;
            Sailing = OnShip && ship.GetSpeed() > 1f;
            Swimming = p.IsSwimming();
            Sleeping = p.InBed() || p.IsSleeping();
            Night = EnvMan.IsNight();
            Attacking = p.InAttack();
            Biome = p.GetCurrentBiome();
            Encumbered = p.IsEncumbered();
            Ready = true;
            Afk = now - Diary.LastActive > 60f;

            float hp = p.GetHealth(), max = Mathf.Max(1f, p.GetMaxHealth());
            if (_hpWas >= 0f && hp < _hpWas - Mathf.Max(0.5f, max * 0.03f)) HurtAt = now; // a blow, not a trickle
            _hpWas = hp;
            Health01 = hp / max;

            Vector3 pos = p.transform.position;
            float moved = _atSet ? Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_at.x, 0f, _at.z)) : 0f;
            Jumped = _atSet && moved > 30f;                  // a portal or a teleport
            if (!Jumped && !OnShip && moved > 0.05f) { Steps.Enqueue(new KeyValuePair<float, float>(now, moved)); Walked += moved; }
            while (Steps.Count > 0 && now - Steps.Peek().Key > 120f) Walked -= Steps.Dequeue().Value;
            if (Walked < 0f) Walked = 0f;
            _at = pos; _atSet = true;

            if (now >= _nextScan) { _nextScan = now + 1f; Scan(p); }
        }

        // the creatures around, once: is there a fight, the nearest enemy, a boss
        private static void Scan(Player p)
        {
            Vector3 me = p.transform.position;
            Character foe = null, boss = null;
            float best = LookRange * LookRange;
            bool near = false;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c.IsPlayer()) continue;
                float d = (c.transform.position - me).sqrMagnitude;
                if (c.IsBoss()) { if (d <= BossRange * BossRange && boss == null) boss = c; }
                if (d > FoeRange * FoeRange || !BaseAI.IsEnemy(p, c)) continue;
                near = true;
                if (!c.IsBoss() && d < best) { best = d; foe = c; }
            }
            FoeNear = near; Foe = foe; Boss = boss;
        }

        // something ahead of you within this distance (for the raven's scouting): the nearest enemy in front
        internal static Character Ahead(Player p, float range)
        {
            Vector3 me = p.transform.position, f = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            Character best = null;
            float bd = range * range;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == p || c.IsDead() || c.IsPlayer()) continue;
                Vector3 to = c.transform.position - me;
                float d = to.sqrMagnitude;
                if (d > bd || Vector3.Dot(to.normalized, f) < 0.4f || !BaseAI.IsEnemy(p, c)) continue;
                bd = d; best = c;
            }
            return best;
        }

        internal static string Describe()
        {
            return Lang.F("Ситуація: {0}{1}{2}{3}{4}{5}{6}, здоров'я {7}%", (Underground ? Lang.T("під землею") : Indoors ? Lang.T("під дахом") : Lang.T("надворі")), (Sailing ? Lang.T(", пливе кораблем") : OnShip ? Lang.T(", на кораблі") : ""), (Traveling ? Lang.F(", в дорозі ({0} м за 2 хв)", Mathf.RoundToInt(Walked)) : Lang.F(", пройшов {0} м за 2 хв", Mathf.RoundToInt(Walked))), (Fight ? Lang.T(", БІЙ") : ""), (Swimming ? Lang.T(", пливе") : ""), (Sleeping ? Lang.T(", спить") : ""), (Afk ? Lang.T(", бездіяльний") : ""), Mathf.RoundToInt(Health01 * 100f));
        }
    }
}
#endif

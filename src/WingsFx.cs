#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // Munin's wings, heard and seen by everyone nearby. On use: a short higher caw and a gust of our feathers and
    // grey runes blown back off you (Feathers.Gust). While they last: a stream from each foot, Munin's feathers and
    // runes left behind as you run (Feathers.FootStream), the faster the thicker.
    internal static class WingsFx
    {
        internal const string RpcBurst = "RavenWatch_WingsFx";
        // the start's sound: a nature weapon's impact, twice as long (the game picks one of its variants each time)
        internal const string Sound = "sfx_weapons_nature_impact";
        internal static float SoundStretch = 2f, SoundLouder = 1.3f; // twice as long, 30% louder
        private const string Key = "RavenWatch_Wings";

        private static readonly Dictionary<Player, GameObject> Drawn = new Dictionary<Player, GameObject>();
        private static float _next;

        internal static void Register() { RavenFx.Register(RpcBurst, SpawnBurst); }

        // ---------------------------------------------------------------- on use

        internal static void Burst(Player p) { RavenFx.Broadcast(RpcBurst, p, SpawnBurst); }

        private static void SpawnBurst(Player p)
        {
            try
            {
                Feathers.Gust(p);
                RavenFx.PlayStretched(Sound, p.transform.position + Vector3.up * 1.2f, SoundStretch, SoundLouder);
                ScreamFx.Caw(p, true, 1.3f, 0.7f);
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Wings effect failed: " + e.Message); }
        }

        // ---------------------------------------------------------------- while they last

        // the wearer tells everyone the wings are on
        internal static void Publish(Player p)
        {
            NetFlag.Set(p, Key, p.GetSEMan().HaveStatusEffect(Wings.Hash));
        }

        internal static void Reset()
        {
            foreach (GameObject g in Drawn.Values) if (g != null) UnityEngine.Object.Destroy(g);
            Drawn.Clear();
        }

        // twice a second: a trail for every player nearby whose wings are on
        internal static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f;
            List<Player> gone = null;
            foreach (KeyValuePair<Player, GameObject> kv in Drawn)
            {
                if (kv.Key != null && Wants(kv.Key)) continue;
                Fade(kv.Value);
                if (gone == null) gone = new List<Player>();
                gone.Add(kv.Key);
            }
            if (gone != null) foreach (Player pl in gone) Drawn.Remove(pl);
            foreach (Player pl in Player.GetAllPlayers())
            {
                if (pl == null || !Wants(pl)) continue;
                GameObject g;
                if (Drawn.TryGetValue(pl, out g) && g != null) continue;
                Drawn[pl] = Make(pl);
            }
        }

        private static bool Wants(Player pl)
        {
            return !pl.IsDead() && NetFlag.Get(pl, Key);
        }

        // Munin's Wings are on for this player (any player: read from the net flag)
        internal static bool On(Player pl) { return pl != null && Wants(pl); }

        // the last feathers drift down where they are
        private static void Fade(GameObject g)
        {
            if (g == null) return;
            WingsStreams w = g.GetComponent<WingsStreams>();
            if (w != null) w.Stop();
            else UnityEngine.Object.Destroy(g);
        }

        private static GameObject Make(Player pl)
        {
            try
            {
                GameObject g = new GameObject("RavenWatch_WingsTrail");
                g.AddComponent<WingsStreams>().Init(pl);
                return g;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Wings trail failed: " + e.Message); return null; }
        }
    }

    // Two streams, one at each foot: the game's own foot bones (FootStep) when it has them, else two points beside
    // your feet. Not children of the body: they are put where the feet are each frame (World particles left behind).
    internal class WingsStreams : MonoBehaviour
    {
        private Player _pl;
        private Transform[] _feet;
        private readonly Transform[] _at = new Transform[2];
        private bool _stopped;

        internal void Init(Player pl)
        {
            _pl = pl;
            FootStep fs = pl != null ? pl.GetComponent<FootStep>() : null;
            if (fs != null && fs.m_feet != null && fs.m_feet.Length >= 2) _feet = fs.m_feet;
            for (int i = 0; i < 2; i++)
            {
                GameObject h = new GameObject(i == 0 ? "LeftFoot" : "RightFoot");
                h.transform.SetParent(transform, false);
                _at[i] = h.transform;
                Place(i);
                Feathers.FootStream(h.transform);
            }
        }

        private void Place(int i)
        {
            if (_pl == null || _at[i] == null) return;
            if (_feet != null && _feet[i] != null) { _at[i].position = _feet[i].position; return; }
            _at[i].position = _pl.transform.position + _pl.transform.right * (i == 0 ? -0.14f : 0.14f) + Vector3.up * 0.08f;
        }

        private void LateUpdate()
        {
            if (_stopped) return;
            if (_pl == null) { Stop(); return; }
            Place(0); Place(1);
        }

        // no new ones; the last settle where they are, then it goes
        internal void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true)) { ParticleSystem.EmissionModule em = ps.emission; em.enabled = false; }
            Destroy(gameObject, 3f);
        }
    }
}
#endif

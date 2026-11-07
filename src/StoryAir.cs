#if RAVEN_ITEM && RAVEN_STORY
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The scene's air (StoryScenes): weather, bolts, Thor, a shake (what is heard: StoryAudio). Each is local to a game (EnvMan,
    // the camera are each player's own); «all» sends it to every game (RPC), and each plays it round its own player —
    // the prologue's storm is seen by the whole world (story-lore.md «Пролог»).
    //   weather <env> [s] · weather off        a forced weather (the game's: ThunderStorm, Rain, Misty, Clear …) for s seconds
    //   bolt <x y z> [n] [spread]              bolts from the sky there (our jagged bolt + the obliterator's crack)
    //   bolt near [n]                          n bolts round the player, 25–70 m off
    //   bolt ahead [n]                         n bolts before the player's eyes (where the camera looks), 30–60 m off
    //   thor                                   Thor flies over the sky (the game's own ThorFly)
    //   shake <strength> <s>                   the camera shakes
    internal static class StoryAir
    {
        private const string Rpc = "RavenWatch_StoryAir";
        internal static float BoltHeight = 90f, ThorHeight = 110f, ThorAside = 160f, ThorAhead = 140f;
        private const int MaxBolts = 12;

        // where this player looks, flat (the camera's way: the scene's shot or their own)
        private static Vector3 Look()
        {
            Vector3 f = GameCamera.instance != null ? GameCamera.instance.transform.forward : (Player.m_localPlayer != null ? Player.m_localPlayer.transform.forward : Vector3.forward);
            f.y = 0f;
            return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
        }
        private static float _weatherUntil = -1f;
        private static Runner _run;

        internal static void RegisterRpcs() { ZRoutedRpc.instance.Register<string>(Rpc, (s, line) => { try { Do(line); } catch (System.Exception e) { Story.Log("air: " + e.Message); } }); }

        // here, or in every game ("all" sent along)
        internal static void Play(string line, bool all)
        {
            if (all && ZRoutedRpc.instance != null) ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, line); // (it comes back here too)
            else Do(line);
        }

        internal static void Do(string line)
        {
            Player p = Player.m_localPlayer;
            string[] w = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            switch (w[0])
            {
                case "weather":
                    if (EnvMan.instance == null || w.Length < 2) return;
                    if (w[1] == "off") { EnvMan.instance.SetForceEnvironment(""); _weatherUntil = -1f; return; }
                    EnvMan.instance.SetForceEnvironment(w[1]);
                    _weatherUntil = Time.time + Mathf.Clamp(w.Length > 2 ? F(w[2], 60f) : 60f, 1f, 600f);
                    Need();
                    break;
                case "bolt":
                    {
                        if (p == null || w.Length < 2) return;
                        bool near = w[1] == "near" || w[1] == "ahead", ahead = w[1] == "ahead";
                        int n = near ? (w.Length > 2 ? (int)F(w[2], 1f) : 1) : (w.Length > 4 ? (int)F(w[4], 1f) : 1);
                        n = Mathf.Clamp(n, 1, MaxBolts); // (it comes from any game too: never a flood of them)
                        float spread = !near && w.Length > 5 ? Mathf.Clamp(F(w[5], 0f), 0f, 200f) : 0f;
                        Vector3 at = near ? p.transform.position : new Vector3(F(w[1], 0f), F(w[2], 0f), F(w[3], 0f));
                        Need();
                        _run.StartCoroutine(Bolts(at, n, near, spread, ahead));
                        break;
                    }
                case "thor": if (Time.time - _thorAt < 3f) return; _thorAt = Time.time; Thor(p); break; // (one at a time across the sky)
                case "shake":
                    if (GameCamera.instance != null && p != null) GameCamera.instance.AddShake(p.transform.position, 50f, w.Length > 1 ? F(w[1], 0.5f) : 0.5f, false);
                    break;
            }
        }

        private static IEnumerator Bolts(Vector3 at, int n, bool near, float spread, bool ahead)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 hit = at;
                if (ahead)
                {
                    Player p = Player.m_localPlayer;
                    if (p != null) hit = p.transform.position; // (each bolt where the player is now, the way the camera looks now)
                    hit += Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * Look() * Random.Range(30f, 60f);
                }
                else if (near) { Vector2 r = Random.insideUnitCircle.normalized * Random.Range(25f, 70f); hit += new Vector3(r.x, 0f, r.y); }
                else if (spread > 0f) { Vector2 r = Random.insideUnitCircle * spread; hit += new Vector3(r.x, 0f, r.y); }
                hit = Ground(hit);
                Bolt(hit);
                yield return new WaitForSeconds(Random.Range(0.35f, 1.1f));
            }
        }

        // one bolt: our jagged line from the sky + the obliterator's crack and flash (its lightning effect, made harmless)
        internal static void Bolt(Vector3 ground)
        {
            BindFx.SkyBolt(ground, BoltHeight);
            GameObject crack = Crack();
            if (crack == null) return;
            GameObject holder = new GameObject("RavenWatch_StoryBolt");
            holder.SetActive(false); // (built asleep: what could hurt is taken off before anything wakes)
            holder.transform.position = ground;
            GameObject g = Object.Instantiate(crack, ground, Quaternion.identity, holder.transform);
            foreach (Aoe a in g.GetComponentsInChildren<Aoe>(true)) Object.DestroyImmediate(a);
            foreach (ZNetView nv in g.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(nv);
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            holder.SetActive(true);
            Object.Destroy(holder, 6f);
        }

        private static GameObject _crack;
        private static bool _crackLooked;
        private static GameObject Crack()
        {
            if (_crackLooked) return _crack;
            if (ZNetScene.instance == null) return null;
            _crackLooked = true;
            GameObject pre = ZNetScene.instance.GetPrefab("incinerator");
            Incinerator inc = pre != null ? pre.GetComponent<Incinerator>() : null;
            _crack = inc != null ? inc.m_lightingAOEs : null;
            if (_crack == null) Story.Log("air: the obliterator's lightning not found — bolts without its crack");
            return _crack;
        }

        // Thor across the sky: the game's own flier (found once among the loaded things), high over the player, side to side
        private static ThorFly _thor;
        private static float _thorAt = -100f;
        private static bool _thorSought;
        private static void Thor(Player p)
        {
            if (p == null) return;
            if (_thor == null && !_thorSought) { _thorSought = true; foreach (ThorFly t in Resources.FindObjectsOfTypeAll<ThorFly>()) { _thor = t; break; } } // heavy: once a session, at the first «thor»
            if (_thor == null) { Story.Log("air: no Thor among the game's things"); return; }
            // across the sky before the eyes: from one side to the other, far ahead and high
            Vector3 look = Look(), side = Vector3.Cross(Vector3.up, look) * (Random.value < 0.5f ? 1f : -1f);
            Vector3 from = p.transform.position + look * ThorAhead - side * ThorAside + Vector3.up * ThorHeight;
            GameObject g = Object.Instantiate(_thor.gameObject, from, Quaternion.LookRotation(side, Vector3.up));
            g.SetActive(true);
            ThorFly f = g.GetComponent<ThorFly>();
            if (f != null) f.m_ttl = Mathf.Max(f.m_ttl, ThorAside * 2f / Mathf.Max(1f, f.m_speed));
        }

        private static Vector3 Ground(Vector3 at)
        {
            if (ZoneSystem.instance != null) at.y = ZoneSystem.instance.GetGroundHeight(at);
            return at;
        }

        private static float F(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }

        // the weather's end and the coroutines' home
        private static void Need()
        {
            if (_run != null) return;
            GameObject g = new GameObject("RavenWatch_StoryAir");
            Object.DontDestroyOnLoad(g);
            _run = g.AddComponent<Runner>();
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                if (_weatherUntil > 0f && Time.time >= _weatherUntil && EnvMan.instance != null) { EnvMan.instance.SetForceEnvironment(""); _weatherUntil = -1f; }
            }
        }
    }
}
#endif

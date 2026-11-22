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
    // The forced weather has ONE owner here (Want): a scene's «weather» first; else a place's «weather: <env> <m> <conditions>» (locations.list)
    // while this player is within m of it, on the land (not inside), and its conditions hold (Places, twice a second) — the user 2026-11-18, the
    // night on Torvald's tower: «свою погоду… густий туман опустився на цю територію… аж тільки понадранок розсіявся після бою». The game's
    // weather has no territory (EnvMan forces one on the whole world this game draws): ours is the player's distance to the place — out of
    // it the world's own comes back. A place's weather comes in PlaceIn s and goes in PlaceOut s (the user 2026-11-20: at 6 the mist was gone at
    // once — «має помалу зникати»): the game's own blend (EnvMan.m_transitionDuration, 2 s) is made that long for that one change, and given back
    // after it (Blend); a scene's weather and the game's own keep the game's. Our own weather: «helmist» — Hel's mist, the game's Misty made denser and
    // blue-green (HelMist, added to the game's list once a world: EnvMan.AppendEnvironment)
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
        private static string _scene = "", _place = "", _applied = ""; // (the scene's weather · the place's round the player · what we force now)
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
                    if (_blendBackAt > 0f && _gameBlend > 0f) { EnvMan.instance.m_transitionDuration = _gameBlend; _blendBackAt = -1f; } // (a scene's weather: the game's blend, not a place's slow one)
                    if (w[1] == "off") { _scene = ""; _weatherUntil = -1f; Want(); return; }
                    _scene = EnvOf(w[1]);
                    _weatherUntil = Time.time + Mathf.Clamp(w.Length > 2 ? F(w[2], 60f) : 60f, 1f, 600f);
                    Need();
                    Want();
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

        // ---------------------------------------------------------------- the weather's one owner
        internal const string HelMist = "RavenWatch_HelMist";
        internal static float HelFog = 2.6f, HelTint = 0.45f; // (Hel's mist: × the fog of the game's Misty · how far its colour goes to Hel's blue-green)
        private static EnvMan _envFor;

        // a word of the data → the game's name of the weather («helmist»: ours, made in this world's list first)
        private static string EnvOf(string word)
        {
            if (word != "helmist") return word;
            MakeHelMist();
            return HelMist;
        }

        // Hel's mist in the game's list of weathers, once a world: the game's Misty, its fog HelFog times as thick, its colours towards HelEyes.Hel
        private static void MakeHelMist()
        {
            EnvMan em = EnvMan.instance;
            if (em == null || em == _envFor) return;
            _envFor = em;
            EnvSetup misty = em.m_environments.Find(e => e != null && e.m_name == "Misty");
            if (misty == null) { Story.Log("air: the game has no weather «Misty» — no Hel's mist"); return; }
            EnvSetup h = misty.Clone();
            h.m_name = HelMist; h.m_default = false;
            h.m_fogDensityNight *= HelFog; h.m_fogDensityMorning *= HelFog; h.m_fogDensityDay *= HelFog; h.m_fogDensityEvening *= HelFog;
            h.m_fogColorNight = Color.Lerp(h.m_fogColorNight, HelEyes.Hel * 0.35f, HelTint); h.m_fogColorMorning = Color.Lerp(h.m_fogColorMorning, HelEyes.Hel * 0.6f, HelTint);
            h.m_fogColorDay = Color.Lerp(h.m_fogColorDay, HelEyes.Hel * 0.8f, HelTint); h.m_fogColorEvening = Color.Lerp(h.m_fogColorEvening, HelEyes.Hel * 0.6f, HelTint);
            em.AppendEnvironment(h);
            Story.Log("air: Hel's mist made (Misty, fog ×" + HelFog.ToString("0.#", CultureInfo.InvariantCulture) + ")");
        }

        // twice a second (StoryQuests.Each): the place's weather round this player — the first place whose «weather:» holds within its reach
        private static EnvMan _airFor; // (the world this game's weather was reckoned in: another one — a relog — begins from none)
        internal static void Places(Player p)
        {
            if (EnvMan.instance != _airFor) { _airFor = EnvMan.instance; _place = _applied = ""; _gameBlend = _blendBackAt = -1f; } // (a new world: what we forced in the last one is not here)
            string want = "";
            if (p != null && !p.InInterior())
                foreach (Loc l in Locs.All)
                {
                    if (l.Weather.Length == 0) continue;
                    Vector3 at; Quaternion rot;
                    if (!l.Where(out at, out rot) || Utils.DistanceXZ(at, p.transform.position) > l.WeatherReach) continue; // flat: a round over the land
                    if (l.WeatherWhile.Length > 0 && !StoryCond.Holds(null, l.WeatherWhile, e => l.Log("weather: " + e), true)) continue;
                    want = EnvOf(l.Weather);
                    break;
                }
            if (want == _place) return;
            Story.Log("air: " + (want.Length > 0 ? "the place's weather «" + want + "» round you" : "the place's weather gone"));
            _place = want;
            if (_scene.Length == 0) Blend(want.Length > 0 ? PlaceIn : PlaceOut); // (a scene's weather over it: its own change, the game's blend)
            Want();
        }

        // the game's blend of one weather into the next made s long for the change about to be asked, and given back after it (Runner)
        internal static float PlaceIn = 20f, PlaceOut = 90f; // s a place's weather takes to come and to go (the game's own: 2) — tuned live: raven npc tune StoryAir.PlaceOut 60
        private static float _gameBlend = -1f, _blendBackAt = -1f;
        private static void Blend(float s)
        {
            EnvMan em = EnvMan.instance;
            if (em == null) return;
            if (_gameBlend < 0f) _gameBlend = em.m_transitionDuration;
            em.m_transitionDuration = Mathf.Max(0.1f, s);
            _blendBackAt = Time.time + s + 1f;
            Need();
        }

        // the weather now is the one we forced (not an inside's own, not the game's own)
        private static bool Ours() { EnvSetup e = EnvMan.instance != null ? EnvMan.instance.GetCurrentEnvironment() : null; return e != null && e.m_name == _applied; }

        // what is forced now: the scene's, else the place's, else none — ours let go only where we forced it (an inside of ours forces its own air: LocInside)
        private static void Want()
        {
            if (EnvMan.instance == null) return;
            string want = _scene.Length > 0 ? _scene : _place;
            if (want.Length > 0) EnvMan.instance.SetForceEnvironment(want); // (the game's: nothing when it is that one already)
            else if (_applied.Length > 0 && Ours()) EnvMan.instance.SetForceEnvironment(""); // (only ours: an inside forces its own air — LocInside)
            _applied = want;
        }

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
                if (_weatherUntil > 0f && Time.time >= _weatherUntil) { _scene = ""; _weatherUntil = -1f; Want(); }
                if (_blendBackAt > 0f && Time.time >= _blendBackAt) { _blendBackAt = -1f; if (EnvMan.instance != null && _gameBlend > 0f) EnvMan.instance.m_transitionDuration = _gameBlend; } // (the place's slow change over: the game's blend again)
            }
        }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // Pieces the raven abilities share: effects every player nearby sees, status effect templates,
    // and materials that have to go when their effect goes.
    internal static class RavenFx
    {
        private const float HearRange = 100f;

        // ---------------------------------------------------------------- seen by everyone nearby

        // Registers an effect that plays on the player who used it, on every game within reach.
        internal static void Register(string rpc, Action<Player> play)
        {
            ZRoutedRpc.instance.Register<ZPackage>(rpc, new Action<long, ZPackage>((sender, pkg) =>
            {
                Player who = Sender(sender, pkg);
                if (who != null) play(who);
            }));
        }

        // plays it here and sends it to the others
        internal static void Broadcast(string rpc, Player p, Action<Player> play)
        {
            play(p);
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = new ZPackage();
            pkg.Write(p.GetPlayerName());
            pkg.Write(p.transform.position);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, rpc, pkg);
        }

        // the player an effect came from, when it is someone else close enough to see it
        private static Player Sender(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            Vector3 pos = pkg.ReadVector3();
            Player me = Player.m_localPlayer;
            if (me == null || sender == ZDOMan.GetSessionID()) return null;
            if (Vector3.Distance(me.transform.position, pos) > HearRange) return null;
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != me && other.GetPlayerName() == name) return other;
            return null;
        }

        // A layer the game leaves unnamed, for a camera that should see nothing but our model. Unnamed is not surely
        // unused (the game draws its grass on such a layer): lights for that camera are switched on only while it shoots.
        internal static int FreeLayer()
        {
            for (int l = 31; l >= 8; l--) if (string.IsNullOrEmpty(LayerMask.LayerToName(l))) return l;
            return -1;
        }

        // ---------------------------------------------------------------- the game's effects by name
        // Many of them (sounds, puffs) are not in the game's list of prefabs, only inside other objects: those are
        // looked up once among everything loaded, and kept.
        private static readonly Dictionary<string, GameObject> Found = new Dictionary<string, GameObject>();
        private static Dictionary<string, GameObject> _loaded;

        internal static GameObject Prefab(string name)
        {
            GameObject g;
            if (Found.TryGetValue(name, out g)) return g;
            g = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (g == null)
            {
                if (_loaded == null)
                {
                    _loaded = new Dictionary<string, GameObject>();
                    // one name, several objects (a copy playing in the world now, another mod's copy on Jotunn mock
                    // shaders): the game's asset, on shaders that draw, wins
                    GameObject had;
                    foreach (GameObject o in Resources.FindObjectsOfTypeAll<GameObject>())
                        if (o != null && o.transform.parent == null && (!_loaded.TryGetValue(o.name, out had) || Better(o, had))) _loaded[o.name] = o;
                }
                _loaded.TryGetValue(name, out g);
                if (g == null && Plugin.Instance != null) Plugin.Instance.Log("Effect not found in the game: " + name);
            }
            Found[name] = g;
            return g;
        }

        // A game effect list played as the game plays it (EffectList.Create, step by step), but by us: other mods hook
        // the game's own (Path of Valheiman makes what is made during its blast swings bigger and reddens what comes in
        // a blighted blow, Seasons hangs its seasonal controller on everything made) — a raven's feather puff on a hit
        // came out huge or red. Ours goes past all of that.
        internal static GameObject[] Own(EffectList list, Vector3 pos, Quaternion rot, Transform parent = null, float scale = 1f, int variant = -1)
        {
            if (list == null || list.m_effectPrefabs == null) return new GameObject[0];
            List<GameObject> made = new List<GameObject>(list.m_effectPrefabs.Length);
            foreach (EffectList.EffectData d in list.m_effectPrefabs)
            {
                if (d == null || !d.m_enabled || d.m_prefab == null) continue;
                if (variant >= 0 && d.m_variant >= 0 && d.m_variant != variant) continue;
                Transform on = parent;
                Vector3 at = pos;
                Quaternion turn = rot;
                if (!string.IsNullOrEmpty(d.m_childTransform) && parent != null)
                {
                    Transform child = Utils.FindChild(parent, d.m_childTransform, Utils.IterativeSearchType.DepthFirst);
                    if (child != null) { on = child; at = child.position; }
                }
                if (on != null && d.m_inheritParentRotation) turn = on.rotation;
                if (d.m_randomRotation) turn = UnityEngine.Random.rotation;
                GameObject g = UnityEngine.Object.Instantiate(d.m_prefab, at, turn);
                if (d.m_scale)
                    g.transform.localScale = parent != null && d.m_inheritParentScale ? parent.localScale * scale : new Vector3(scale, scale, scale);
                else if (parent != null && d.m_multiplyParentVisualScale)
                {
                    Transform visual = parent.Find("Visual");
                    if (visual != null) g.transform.localScale = Vector3.Scale(g.transform.localScale, visual.localScale);
                }
                else if (parent != null && d.m_inheritParentScale) g.transform.localScale = parent.localScale;
                if (d.m_attach && on != null) g.transform.SetParent(on);
                if (d.m_follow && on != null)
                {
                    UnityEngine.Animations.ParentConstraint pc = g.AddComponent<UnityEngine.Animations.ParentConstraint>();
                    UnityEngine.Animations.ConstraintSource src = new UnityEngine.Animations.ConstraintSource();
                    src.sourceTransform = on; src.weight = 1f;
                    pc.AddSource(src);
                    pc.locked = true;
                    pc.SetTranslationOffset(0, d.m_prefab.transform.position);
                    pc.SetRotationOffset(0, d.m_prefab.transform.rotation.eulerAngles);
                    pc.constraintActive = true;
                }
                made.Add(g);
            }
            return made.ToArray();
        }

        private static int Worth(GameObject o) { return (o.scene.IsValid() ? 0 : 2) + (Shoulder.ShadersWork(o) ? 1 : 0); }
        private static bool Better(GameObject o, GameObject had) { return had == null || Worth(o) > Worth(had); }

        // the sounds in something just made, slower (stretch 1.5: half as long again, and lower); its own timer taken
        // off (it would cut the slower sound short) and it goes once the longest of them has played
        internal static void Stretch(GameObject made, float stretch)
        {
            if (made == null) return;
            foreach (TimedDestruction td in made.GetComponentsInChildren<TimedDestruction>(true)) UnityEngine.Object.DestroyImmediate(td);
            float pitch = 1f / Mathf.Max(0.1f, stretch), life = 2f;
            foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>(true))
            {
                ZSFX z = a.GetComponent<ZSFX>();
                if (z != null) z.SetPitchModifier(pitch); else a.pitch *= pitch;
                if (a.clip != null) life = Mathf.Max(life, a.clip.length * stretch + 0.5f);
                // the game picks its variant only when it starts playing (next frame): live as long as the longest of them
                if (z != null && z.m_audioClips != null) foreach (AudioClip c in z.m_audioClips) if (c != null) life = Mathf.Max(life, c.length * stretch + 0.5f);
            }
            foreach (ParticleSystem ps in made.GetComponentsInChildren<ParticleSystem>(true)) life = Mathf.Max(life, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
            UnityEngine.Object.Destroy(made, life);
        }

        internal static bool PlayStretched(string name, Vector3 at, float stretch) { return PlayStretched(Prefab(name), at, stretch) != null; }

        // a stretched sound, louder (1.3: 30% up). A player cannot go past full volume: what is left over plays as
        // a twin of the same clip, at the same moment and pace, beside it
        internal static GameObject PlayStretched(string name, Vector3 at, float stretch, float louder)
        {
            GameObject made = PlayStretched(Prefab(name), at, stretch);
            if (made == null || louder <= 1.001f) return made;
            float pitchMod = 1f / Mathf.Max(0.1f, stretch);
            foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>(true))
            {
                if (a.clip == null) continue;
                ZSFX z = a.GetComponent<ZSFX>();
                float want = a.volume * louder;
                if (want <= 1f) { if (z != null) z.SetVolumeModifier(louder); else a.volume = want; continue; }
                if (z == null) a.volume = 1f;
                AudioSource twin = a.gameObject.AddComponent<AudioSource>();
                twin.clip = a.clip;
                twin.outputAudioMixerGroup = a.outputAudioMixerGroup;
                twin.spatialBlend = a.spatialBlend; twin.rolloffMode = a.rolloffMode;
                twin.minDistance = a.minDistance; twin.maxDistance = a.maxDistance;
                twin.pitch = z != null ? a.pitch * pitchMod : a.pitch; // the game applies the stretch to the original on its next update
                twin.volume = Mathf.Clamp01(want - 1f);
                twin.loop = false;
                twin.time = a.time;
                twin.Play();
            }
            return made;
        }

        internal static GameObject PlayStretched(GameObject prefab, Vector3 at, float stretch)
        {
            try
            {
                if (prefab == null || prefab.GetComponent<ZNetView>() != null) return null;
                GameObject made = UnityEngine.Object.Instantiate(prefab, at, Quaternion.identity);
                made.SetActive(true);
                Stretch(made, stretch);
                return made;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Effect " + prefab.name + " failed: " + e.Message); return null; }
        }

        // plays it once here (not a networked object: each game plays its own copy); false when the game has none
        internal static bool Play(string name, Vector3 at, Quaternion rot)
        {
            try
            {
                GameObject prefab = Prefab(name);
                if (prefab == null || prefab.GetComponent<ZNetView>() != null) return false;
                GameObject made = UnityEngine.Object.Instantiate(prefab, at, rot);
                made.SetActive(true);
                if (made.GetComponent<TimedDestruction>() == null)
                {
                    float life = 4f;
                    foreach (AudioSource a in made.GetComponentsInChildren<AudioSource>(true)) if (a.clip != null) life = Mathf.Max(life, a.clip.length + 0.5f);
                    UnityEngine.Object.Destroy(made, life);
                }
                return true;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Effect " + name + " failed: " + e.Message); return false; }
        }

        internal static void Forget() { Found.Clear(); _loaded = null; } // a new world: other objects loaded

        // when the character appears: the one long look through everything loaded happens now, not mid-game
        internal static void Warm()
        {
            foreach (string n in new string[] { "sfx_raven_flap", "sfx_raven_kaw", "vfx_offering", "sfx_offering", "sfx_levelup", "vfx_skilllevelup", "fx_bossstone_attach", "sfx_bossstone_attach_done", ScreamFx.GateSound, WingsFx.Sound })
                Prefab(n);
        }

        // ---------------------------------------------------------------- status effects

        // The game copies an effect when it is added (a shallow copy that shares the Unity object), so the one
        // made here must stay alive: one template per effect, filled anew on every use, instead of a new one each time.
        internal static T Template<T>(ref T cache, string name) where T : StatusEffect
        {
            if (cache == null)
            {
                cache = ScriptableObject.CreateInstance<T>();
                cache.name = name;
                cache.hideFlags = HideFlags.HideAndDontSave;
            }
            return cache;
        }

        // ---------------------------------------------------------------- materials

        // r.materials makes copies the game never frees; they are kept here and freed with their object
        internal static Material[] OwnMaterials(Renderer r)
        {
            Material[] mats = r.materials;
            OwnedMaterials owner = r.gameObject.GetComponent<OwnedMaterials>();
            if (owner == null) owner = r.gameObject.AddComponent<OwnedMaterials>();
            owner.Add(mats);
            return mats;
        }
    }

    // "raven perf": for a few seconds the mod's own work each frame is timed (its main loop, the shoulder ravens,
    // and what the monsters ask about the shadow), then the average and the worst frame are told.
    internal static class Perf
    {
        internal static bool On;
        private static long _ticks, _frameTicks, _worst;
        private static int _frames, _lastFrame = -1;
        private static float _until;

        internal static string Start(float seconds)
        {
            _ticks = 0; _frameTicks = 0; _worst = 0; _frames = 0; _lastFrame = Time.frameCount;
            _until = Time.realtimeSinceStartup + seconds;
            On = true;
            return Lang.F("Міряю {0} с, грай як зазвичай…", Mathf.RoundToInt(seconds));
        }

        internal static long Now { get { return System.Diagnostics.Stopwatch.GetTimestamp(); } }

        internal static void Add(long since)
        {
            if (!On) return;
            long d = System.Diagnostics.Stopwatch.GetTimestamp() - since;
            if (Time.frameCount != _lastFrame) { if (_frameTicks > _worst) _worst = _frameTicks; _frameTicks = 0; _lastFrame = Time.frameCount; _frames++; }
            _ticks += d; _frameTicks += d;
        }

        // from the plugin's Update: the time is up
        internal static void Tick()
        {
            if (!On || Time.realtimeSinceStartup < _until) return;
            On = false;
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            string text = Lang.F("RavenWatch: у середньому {0} мс на кадр, найгірший кадр {1} мс ({2} кадрів)", (_ticks * ms / Mathf.Max(1, _frames)).ToString("0.000"), (_worst * ms).ToString("0.00"), _frames);
            if (Plugin.Instance != null) Plugin.Instance.Log(text);
            if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text);
            if (global::Console.instance != null) global::Console.instance.Print(text);
        }
    }

    // A moment kept in the character (or an item) in world time, so leaving the game does not reset a wait.
    internal static class WorldTime
    {
        internal static double Now { get { return ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : Time.time; } }

        // seconds until the moment kept under key; 0 or less when it has passed or was never set
        internal static float Left(Dictionary<string, string> data, string key)
        {
            string raw;
            double at;
            if (data == null || !data.TryGetValue(key, out raw) || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out at)) return 0f;
            return Sane(at - Now);
        }

        // Every timer kept is the world's clock, and each world has its own: carried to a world whose clock is behind,
        // a wait would last for ages. No wait of the mod is longer than MaxAhead, so anything further off is from
        // another world, and over.
        internal const float MaxAhead = 4f * 3600f;
        internal static float Sane(double left) { return left > MaxAhead ? 0f : (float)left; }

        internal static void Set(Dictionary<string, string> data, string key, double secondsFromNow)
        {
            if (data != null) data[key] = (Now + secondsFromNow).ToString("R", CultureInfo.InvariantCulture);
        }

        // A wait that another world must not cut short (one errand a day, a tree's sleep): kept with its world and the
        // real time it ends at; read in another world, what is left of it by the real clock goes on there.
        private static long World { get { return ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L; } }
        internal static void SetHere(Dictionary<string, string> data, string key, double seconds)
        {
            if (data == null) return;
            Set(data, key, seconds);
            data[key + "_w"] = World.ToString(CultureInfo.InvariantCulture) + "|" + DateTime.UtcNow.AddSeconds(seconds).Ticks.ToString(CultureInfo.InvariantCulture);
        }
        internal static float LeftHere(Dictionary<string, string> data, string key)
        {
            string raw;
            if (data == null || !data.TryGetValue(key + "_w", out raw)) return Left(data, key);
            int bar = raw.IndexOf('|');
            long w, ends;
            if (bar > 0 && long.TryParse(raw.Substring(0, bar), out w) && long.TryParse(raw.Substring(bar + 1), out ends) && w != 0L && World != 0L && w != World)
            {
                double left = (new DateTime(ends, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
                if (left > 0) SetHere(data, key, Math.Min(left, MaxAhead)); else ClearHere(data, key); // (set in another world: the rest of it, here)
            }
            return Left(data, key);
        }
        internal static void ClearHere(Dictionary<string, string> data, string key) { if (data == null) return; data.Remove(key); data.Remove(key + "_w"); }
    }

    // A yes/no the local player writes on its own ZDO for every game to read (a wisp is on, wings are out...).
    internal static class NetFlag
    {
        internal static ZDO Zdo(Character c, bool mine)
        {
            ZNetView nv = c != null ? c.GetComponent<ZNetView>() : null;
            if (nv == null || !nv.IsValid() || (mine && !nv.IsOwner())) return null;
            return nv.GetZDO();
        }

        internal static void Set(Character c, string key, bool on)
        {
            ZDO zdo = Zdo(c, true);
            if (zdo != null && zdo.GetBool(key, false) != on) zdo.Set(key, on);
        }

        internal static bool Get(Character c, string key)
        {
            ZDO zdo = Zdo(c, false);
            return zdo != null && zdo.GetBool(key, false);
        }
    }

    internal class OwnedMaterials : MonoBehaviour
    {
        private readonly List<Material> _mats = new List<Material>();
        internal void Add(Material[] mats) { foreach (Material m in mats) if (m != null && !_mats.Contains(m)) _mats.Add(m); }
        private void OnDestroy() { foreach (Material m in _mats) if (m != null) Destroy(m); _mats.Clear(); }
    }
}
#endif

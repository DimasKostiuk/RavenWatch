#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A game sound played backwards. The game keeps most of its clips compressed in memory: Unity can neither read
    // their samples nor play them with a negative pitch. So the clip is played once, silently, and caught as it comes
    // out of its player (OnAudioFilterRead hands over every buffer; it is copied and then wiped, so nothing is heard),
    // and the catch, turned round, is a clip of our own. Made once per clip, kept for the session. A clip the game
    // keeps decompressed is read directly instead.
    internal static class SoundBack
    {
        private static readonly Dictionary<AudioClip, AudioClip> Ready = new Dictionary<AudioClip, AudioClip>();
        private static readonly HashSet<AudioClip> Busy = new HashSet<AudioClip>();
        // the float[] forms by hand: this compiler cannot tell them from the newer Span ones
        private static readonly System.Reflection.MethodInfo GetDataM = typeof(AudioClip).GetMethod("GetData", new Type[] { typeof(float[]), typeof(int) });
        private static readonly System.Reflection.MethodInfo SetDataM = typeof(AudioClip).GetMethod("SetData", new Type[] { typeof(float[]), typeof(int) });

        // the clip turned round, or null while it is not made yet
        internal static AudioClip Get(AudioClip clip)
        {
            AudioClip r;
            return clip != null && Ready.TryGetValue(clip, out r) ? r : null;
        }

        // made (or tried and failed: nothing came out of it)
        internal static bool Made(AudioClip clip) { return clip != null && Ready.ContainsKey(clip); }

        // A backwards clip through a player like the given one (its mixer, so the game's sound volume counts), timed
        // so that it ENDS in endIn seconds: backwards a sound ends on its own onset (the click, the whoosh), and that is
        // what should meet the moment. Longer than that: only its last part, swelling in; shorter: it waits.
        internal static GameObject PlayEndingAt(AudioClip clip, AudioSource like, Vector3 at, Transform follow, float endIn)
        {
            if (clip == null) return null;
            if (clip.length > endIn + 0.05f)
            {
                GameObject go = PlayClip(clip, like, at, follow, 1f, clip.length - endIn, 0f);
                if (go != null) go.AddComponent<SoundEaseIn>().Begin(go.GetComponent<AudioSource>(), Mathf.Min(0.2f, endIn * 0.3f));
                return go;
            }
            return PlayClip(clip, like, at, follow, 1f, 0f, Mathf.Max(0f, endIn - clip.length));
        }

        // ---------------------------------------------------------------- a game sound by name, backwards
        // Every variant of the named sound object is caught in advance (PrepareNamed, when the character appears);
        // a request plays one ready variant at random, after a delay, stretched (stretch 1.4: 40% longer, and lower).
        // One not caught yet when asked is waited for a little while, then dropped.
        private static readonly Dictionary<string, List<AudioClip>> Named = new Dictionary<string, List<AudioClip>>();
        private static readonly Dictionary<string, AudioSource> NamedLike = new Dictionary<string, AudioSource>();

        private static List<AudioClip> ClipsOf(string name)
        {
            List<AudioClip> list;
            if (Named.TryGetValue(name, out list)) return list;
            GameObject prefab = RavenFx.Prefab(name);
            if (prefab == null) return null; // the game not loaded far enough yet: asked again next time
            list = new List<AudioClip>();
            AudioSource like = prefab.GetComponentInChildren<AudioSource>(true);
            ZSFX z = prefab.GetComponentInChildren<ZSFX>(true);
            if (z != null && z.m_audioClips != null) { foreach (AudioClip c in z.m_audioClips) if (c != null && !list.Contains(c)) list.Add(c); }
            else if (like != null && like.clip != null) list.Add(like.clip);
            Named[name] = list;
            NamedLike[name] = like;
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Sound backwards: " + name + " has " + list.Count + " variant(s)");
            return list;
        }

        internal static void PrepareNamed(string name)
        {
            try { List<AudioClip> list = ClipsOf(name); if (list != null) foreach (AudioClip c in list) Prepare(c); }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Sound backwards: " + name + " failed: " + e.Message); }
        }

        // a game sound played forward, stretch times slower, through a player of our own that lives exactly as long as the
        // slowed clip (the game's own copy picks its clip after we could measure it and was cut short)
        internal static bool PlayForward(string name, Transform follow, Vector3 at, float stretch)
        {
            List<AudioClip> list = ClipsOf(name);
            if (list == null || list.Count == 0) return false;
            AudioSource like;
            NamedLike.TryGetValue(name, out like);
            return PlayClip(list[UnityEngine.Random.Range(0, list.Count)], like, at, follow, stretch, 0f) != null;
        }

        // fadeIn: seconds it takes to swell from silence to full (0: at once)
        internal static void PlayNamed(string name, Transform follow, float delay, float stretch, float fadeIn = 0f)
        {
            PrepareNamed(name);
            Runner.Add(name, follow, Time.time + Mathf.Max(0f, delay), Mathf.Max(0.1f, stretch), Mathf.Max(0f, fadeIn));
        }

        private class Runner : MonoBehaviour
        {
            private struct Ask { public string Name; public Transform Follow; public float At, Stretch, FadeIn; }
            private static Runner _inst;
            private readonly List<Ask> _asks = new List<Ask>();
            private static readonly List<AudioClip> Ready_ = new List<AudioClip>();

            internal static void Add(string name, Transform follow, float at, float stretch, float fadeIn)
            {
                if (_inst == null)
                {
                    GameObject go = new GameObject("RavenWatch_SoundBackRunner");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<Runner>();
                }
                _inst._asks.Add(new Ask { Name = name, Follow = follow, At = at, Stretch = stretch, FadeIn = fadeIn });
            }

            private void Update()
            {
                for (int i = _asks.Count - 1; i >= 0; i--)
                {
                    Ask a = _asks[i];
                    if (Time.time < a.At) continue;
                    if (a.Follow == null || Time.time > a.At + 3f) { _asks.RemoveAt(i); continue; } // gone, or caught too late to fit
                    List<AudioClip> list = ClipsOf(a.Name);
                    Ready_.Clear();
                    if (list != null) foreach (AudioClip c in list) if (Get(c) != null) Ready_.Add(c);
                    if (Ready_.Count == 0) continue; // still being caught
                    _asks.RemoveAt(i);
                    AudioClip back = Get(Ready_[UnityEngine.Random.Range(0, Ready_.Count)]);
                    AudioSource like;
                    NamedLike.TryGetValue(a.Name, out like);
                    GameObject go = PlayClip(back, like, a.Follow.position + Vector3.up * 1.6f, a.Follow, a.Stretch, 0f); // slower is longer (and lower)
                    if (go != null && a.FadeIn > 0f) go.AddComponent<SoundEaseIn>().Begin(go.GetComponent<AudioSource>(), a.FadeIn); // from silence up to full, then on
                }
            }
        }

        // A clip through a player like the given one (its mixer, so the game's sound volume counts), stretch 2 twice
        // as long and lower, skip seconds left out at its start
        internal static GameObject PlayClip(AudioClip clip, AudioSource like, Vector3 at, Transform follow, float stretch, float skip, float delay = 0f)
        {
            if (clip == null) return null;
            GameObject go = new GameObject("RavenWatch_SoundPlay");
            if (follow != null) go.transform.SetParent(follow, false);
            go.transform.position = at;
            AudioSource s = go.AddComponent<AudioSource>();
            if (like != null)
            {
                s.outputAudioMixerGroup = like.outputAudioMixerGroup;
                s.spatialBlend = like.spatialBlend; s.rolloffMode = like.rolloffMode;
                s.minDistance = like.minDistance; s.maxDistance = like.maxDistance; s.volume = like.volume;
                s.dopplerLevel = like.dopplerLevel; s.spread = like.spread; s.priority = like.priority;
            }
            s.clip = clip;
            s.pitch = 1f / Mathf.Max(0.1f, stretch);
            s.loop = false;
            skip = Mathf.Clamp(skip, 0f, Mathf.Max(0f, clip.length - 0.05f));
            s.time = skip;
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
            UnityEngine.Object.Destroy(go, delay + (clip.length - skip) * Mathf.Max(0.1f, stretch) + 0.5f);
            return go;
        }

        private static readonly Dictionary<AudioClip, float> Quiet = new Dictionary<AudioClip, float>();

        // how long a clip of ours (a backwards one: readable) stays nearly silent at its start — a long fading tail
        // turned round is a long quiet lead-in
        internal static float QuietStart(AudioClip clip)
        {
            float q;
            if (clip == null) return 0f;
            if (Quiet.TryGetValue(clip, out q)) return q;
            q = 0f;
            try
            {
                int ch = Mathf.Max(1, clip.channels);
                float[] data = new float[clip.samples * ch];
                if (GetDataM != null && (bool)GetDataM.Invoke(clip, new object[] { data, 0 }))
                {
                    int i = 0;
                    while (i < data.Length && Mathf.Abs(data[i]) < 0.02f) i++;
                    q = Mathf.Max(0f, (i / ch) / (float)clip.frequency - 0.05f); // a moment before the sound itself
                }
            }
            catch (Exception) { q = 0f; }
            Quiet[clip] = q;
            return q;
        }

        // start making it (well before it is needed: the catch takes as long as the sound itself)
        internal static void Prepare(AudioClip clip)
        {
            if (clip == null || Ready.ContainsKey(clip) || Busy.Contains(clip) || SetDataM == null) return;
            if (clip.loadType == AudioClipLoadType.DecompressOnLoad && GetDataM != null && Read(clip)) return;
            Busy.Add(clip);
            GameObject go = new GameObject("RavenWatch_SoundBack");
            UnityEngine.Object.DontDestroyOnLoad(go);
            AudioSource a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.spatialBlend = 0f; a.volume = 1f; a.pitch = 1f; a.loop = false; a.playOnAwake = false;
            a.priority = 0; a.bypassListenerEffects = true; a.bypassReverbZones = true;
            go.AddComponent<Catcher>().Begin(clip, a);
        }

        private static bool Read(AudioClip clip)
        {
            try
            {
                int ch = Mathf.Max(1, clip.channels);
                float[] data = new float[clip.samples * ch];
                if (!(bool)GetDataM.Invoke(clip, new object[] { data, 0 })) return false;
                Done(clip, data, clip.samples, ch, clip.frequency, "read");
                return true;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Sound backwards: " + clip.name + " could not be read: " + e.Message); return false; }
        }

        // a catch that came out short (the game paused its sounds, a menu): not kept, tried again next time
        private static void Failed(AudioClip clip, string why)
        {
            Busy.Remove(clip);
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Sound backwards: " + clip.name + " not caught (" + why + "), will try again");
        }

        private static void Done(AudioClip clip, float[] data, int frames, int ch, int rate, string how)
        {
            Busy.Remove(clip);
            if (frames <= 0) { Ready[clip] = null; return; }
            float[] back = new float[frames * ch];
            for (int f = 0; f < frames; f++)
                for (int c = 0; c < ch; c++) back[(frames - 1 - f) * ch + c] = data[f * ch + c];
            AudioClip r = AudioClip.Create(clip.name + "_back", frames, ch, rate, false);
            SetDataM.Invoke(r, new object[] { back, 0 });
            r.hideFlags = HideFlags.HideAndDontSave;
            Ready[clip] = r;
            if (AnimDirector.LogOn && Plugin.Instance != null) Plugin.Instance.Log("Sound backwards: " + clip.name + " ready (" + how + ", " + (frames / (float)rate).ToString("0.00") + " s, " + ch + " ch)");
        }

        // plays the clip once, keeps what comes out, lets nothing through
        private class Catcher : MonoBehaviour
        {
            private AudioClip _clip;
            private AudioSource _a;
            private float[] _buf;
            private int _pos, _ch = 2, _rate;
            private volatile bool _full;
            private float _silentSince = -1f, _startedAt;

            internal void Begin(AudioClip clip, AudioSource a)
            {
                _clip = clip; _a = a;
                _rate = AudioSettings.outputSampleRate;
                _buf = new float[Mathf.CeilToInt((clip.length + 0.25f) * _rate) * Speakers()]; // room for the speaker layout in use
                _startedAt = Time.realtimeSinceStartup;
                a.Play();
            }

            // how many channels the game mixes into (the catch comes out in that many)
            private static int Speakers()
            {
                switch (AudioSettings.GetConfiguration().speakerMode)
                {
                    case AudioSpeakerMode.Mono: return 1;
                    case AudioSpeakerMode.Stereo: case AudioSpeakerMode.Prologic: return 2;
                    case AudioSpeakerMode.Quad: return 4;
                    case AudioSpeakerMode.Surround: return 5;
                    case AudioSpeakerMode.Mode5point1: return 6;
                    default: return 8;
                }
            }

            private void OnAudioFilterRead(float[] data, int channels)
            {
                if (_buf != null && !_full)
                {
                    _ch = channels;
                    int n = Math.Min(data.Length, _buf.Length - _pos);
                    Array.Copy(data, 0, _buf, _pos, n);
                    _pos += n;
                    if (_pos >= _buf.Length) _full = true;
                }
                Array.Clear(data, 0, data.Length); // heard by nobody
            }

            private void Update()
            {
                if (AudioListener.pause) { _silentSince = -1f; _startedAt += Time.unscaledDeltaTime; return; } // the game holds its sounds: wait
                bool playing = _a != null && _a.isPlaying && !_full;
                if (playing && Time.realtimeSinceStartup - _startedAt < _clip.length + 3f) { _silentSince = -1f; return; }
                // a moment after it stopped: the audio thread has handed over its last buffer
                if (_silentSince < 0f) { _silentSince = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - _silentSince < 0.2f) return;
                int ch = Mathf.Max(1, _ch);
                int frames = _pos / ch, caught = frames; // caught: before the quiet tail is trimmed
                // the catch may end in silence (the last buffer): trimmed, so the backwards sound starts at once
                while (frames > 0)
                {
                    bool quiet = true;
                    for (int c = 0; c < ch; c++) if (Mathf.Abs(_buf[(frames - 1) * ch + c]) > 0.0005f) { quiet = false; break; }
                    if (!quiet) break;
                    frames--;
                }
                // far shorter than the sound itself: cut off on the way, not worth keeping
                if (caught < _clip.length * _rate * 0.6f) Failed(_clip, "only " + (caught / (float)_rate).ToString("0.00") + " of " + _clip.length.ToString("0.00") + " s");
                else { long t0 = Slow.Start(); Done(_clip, _buf, frames, ch, _rate, "caught"); Slow.End(t0, "backwards sound made (" + _clip.name + ")"); }
                _buf = null;
                Destroy(gameObject);
            }
        }
    }

    // a sound started mid-way swells in over a moment instead of starting with a click
    internal class SoundEaseIn : MonoBehaviour
    {
        private AudioSource _a;
        private float _to, _len, _t0;

        internal void Begin(AudioSource a, float seconds)
        {
            _a = a; _to = a.volume; _len = Mathf.Max(0.01f, seconds); _t0 = Time.time;
            a.volume = 0f;
        }

        private void Update()
        {
            if (_a == null) { Destroy(this); return; }
            float u = Mathf.Clamp01((Time.time - _t0) / _len);
            _a.volume = _to * u;
            if (u >= 1f) Destroy(this);
        }
    }
}
#endif

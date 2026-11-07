#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The one place that decides what is heard (the user 2026-11-04: «predictable, and control as full as can be»). Whoever
    // wants a sound or a silence asks here and is written down as its owner; when the owner ends (End) all it held is let go,
    // softly, in one call. Nothing here runs a frame unless something is held or fading (Run is switched off).
    //   Music    layers: the game < fight < scene. The highest layer that wants something is what is heard; a layer that
    //            wants silence is real silence. While any layer holds, the game's MusicMan picks nothing (raids, locations,
    //            sailing, morning: one prefix on UpdateCurrentMusic); its source fades out on our time, and comes back by itself
    //            when the last one lets go. Our own tracks and the game's play alike, through sources of our own: a crossfade
    //            on the time asked, the game's music mixer and the player's music slider.
    //   Beds     long loops under a scene, any number, each with its owner.
    //   Sounds   one-shots on sources of our own, not ZSFX: the volume asked stays (ZSFX rewrites it each frame), two at once
    //            are two (AudioMan drops the second within 0.2 s), they can be stopped, follow something, have a range. The
    //            game's sound keeps its own variants, pitch and loudness. Dry by default, through the GUI group: the player's
    //            sound slider counts, «mix world» does not; «wet» goes through the game's own group, as the sound would.
    //   Mix      world (every game sound: the SfxVol of the mixer, the player's own value back at the end), ambient (wind, sea,
    //            ambient loop, shield hum: their sources; no new random ambience while below a half), music (whatever music
    //            is on, the game's too), voice (the ravens' spoken lines from then on). Owners may overlap: the lowest wins.
    //   Mute     a game sound by name (sfx_footstep*, *fire*): a new one does not start; a loop keeps running at zero, so it
    //            is back when let go. A loop AudioMan does not list (no concurrency limit) can only be quietened by «world».
    //            A clip's sounds (wet) obey it too; a scene's own never do: it asked for them.
    //   Ear      the listener: on the player's eyes (the game's) or on the scene's camera.
    // Every change is written in the log; «raven audio» says what is held and by whom.
    internal enum AudioLayer { Fight = 0, Scene = 1 }
    internal enum AudioMix { World = 0, Ambient = 1, Music = 2, Voice = 3 }

    internal sealed class SoundAsk
    {
        public float Volume = 1f, Pitch, Range; // (Pitch, Range 0: the sound's own)
        public bool Flat, Wet;                  // (Flat: no place, in the ears. Wet: the game's own mixer group)
        public Transform Follow;
        public string Owner = "";
    }

    internal static class Audio
    {
        internal const string Scene = "scene", Panel = "panel", Air = "air", Fight = "fight";
        private const string Kept = "kept";
        private const int MaxLines = 32;
        internal static float EndFade = 3f, BrokenFade = 1.5f, MuteFade = 0.5f, MusicFade = 3f, FightFade = 3f, MaxLoad = 15f;

        private sealed class Want { public string Owner, Name; public float Fade, Until, Vol = 1f, Keep; public bool Once; }

        private sealed class Line
        {
            public GameObject Go; public AudioSource Src;
            public string Owner = "", Name = "";
            public int Kind; // (KindSound, KindBed, KindMusic)
            public float Asked, Base, K, Target, Rate, Started;
            public bool Live, Waiting, Loop;
            public Transform Follow;
        }
        private const int KindSound = 0, KindBed = 1, KindMusic = 2;

        private static readonly Want[] Wants = new Want[2];
        private static readonly List<Line> Lines = new List<Line>();
        private static Line _cur;
        private static string _playing;
        private static bool _held, _trackDone;
        private static Run _run;

        // ---------------------------------------------------------------- music

        // name null: real silence. term: let go by itself that many seconds from now, unless asked again (0: only by End)
        internal static bool Music(string owner, AudioLayer layer, string name, float fade, float term, float vol, bool once, float keep)
        {
            if (name != null && !AudioBank.IsTrack(name)) { AudioBank.LogOnce("music " + name, "no music «" + name + "» (" + owner + ")"); return false; }
            Want w = Wants[(int)layer];
            if (owner == Air && w != null && (w.Owner == Scene || w.Owner == Panel)) { Log("music from another game left out: " + w.Owner + " holds it"); return true; } // (your own scene goes first)
            float until = term > 0f ? Time.unscaledTime + term : 0f;
            if (w != null && w.Owner == owner && w.Name == name) { w.Until = until; w.Vol = vol; w.Keep = keep; w.Once = once; if (_cur != null && _cur.Name == name) Louder(_cur, vol); return true; } // (asked again: the same track goes on)
            Wants[(int)layer] = new Want { Owner = owner, Name = name, Fade = Mathf.Max(0f, fade), Until = until, Vol = vol, Once = once, Keep = keep };
            Log("music " + layer + ": " + (name ?? "silence") + " (" + owner + ", " + fade.ToString("0.#") + " s)");
            Need();
            MusicNow();
            return true;
        }

        // this owner's music let go, mid-way (what is under it comes back; nothing under it: the game's)
        internal static void MusicOff(string owner, float fade)
        {
            bool any = false;
            for (int i = 0; i < Wants.Length; i++) if (Wants[i] != null && Wants[i].Owner == owner) { Wants[i] = null; any = true; }
            if (!any) return;
            _nextFade = fade;
            Log("music let go by " + owner);
            MusicNow();
        }

        internal static bool MusicWaiting { get { return _cur != null && _cur.Waiting; } }

        private static float _nextFade = -1f;

        // what should be heard now against what is
        private static void MusicNow()
        {
            Want top = null;
            for (int i = Wants.Length - 1; i >= 0 && top == null; i--) top = Wants[i];
            float fade = _nextFade >= 0f ? _nextFade : top != null ? top.Fade : EndFade;
            _nextFade = -1f;
            if (top != null && !_held) { _held = true; GameOut(fade); }
            string want = top != null ? top.Name : null;
            if (want != _playing)
            {
                if (_cur != null) { FadeTo(_cur, 0f, fade); _cur = null; }
                _playing = want;
                _trackDone = false;
                if (want != null) { _cur = Start(KindMusic, want, top.Owner, top.Vol, !top.Once, fade); if (_cur == null) _trackDone = true; } // (nothing to play: not tried again each frame; the layer stays a silence till it is let go)
            }
            if (top == null && _held) { _held = false; GameIn(); }
        }

        private static readonly FieldInfo FFade = AccessTools.Field(typeof(MusicMan), "m_musicFadeTime"), FTriggered = AccessTools.Field(typeof(MusicMan), "m_triggeredMusic"), FTrigger = AccessTools.Field(typeof(MusicMan), "m_triggerMusic"), FSource = AccessTools.Field(typeof(MusicMan), "m_musicSource");
        private static readonly MethodInfo MStop = AccessTools.Method(typeof(MusicMan), "StopMusic");

        // the game's music out, on our time
        private static void GameOut(float fade)
        {
            MusicMan m = MusicMan.instance;
            if (m == null) return;
            try
            {
                FFade.SetValue(m, Mathf.Max(0.05f, fade));
                FTriggered.SetValue(m, null);
                FTrigger.SetValue(m, null);
                MStop.Invoke(m, null);
                if (m.m_musicMixer != null) m.m_musicMixer.audioMixer.SetFloat("Music_ontop_ducking", 0f); // (a raid had it down; the prefix keeps it from moving now)
            }
            catch (Exception e) { Log("the game's music would not stop: " + e.Message); }
        }

        // the game picks again; a morning or evening call that came while we held is old news
        private static void GameIn()
        {
            MusicMan m = MusicMan.instance;
            if (m == null) return;
            try { FTrigger.SetValue(m, null); FTriggered.SetValue(m, null); }
            catch (Exception e) { Log("the game's music would not come back: " + e.Message); }
        }

        internal static bool MusicHeld { get { return _held; } }

        // the game's tracks by name (for the author's window)
        internal static List<string> GameTracks()
        {
            List<string> r = new List<string>();
            if (MusicMan.instance != null) foreach (MusicMan.NamedMusic m in MusicMan.instance.m_music) if (m != null && !string.IsNullOrEmpty(m.m_name) && m.m_enabled && m.m_clips != null && m.m_clips.Length > 0) r.Add(m.m_name);
            return r;
        }

        internal static MusicMan.NamedMusic GameTrack(string name)
        {
            if (MusicMan.instance == null) return null;
            foreach (MusicMan.NamedMusic m in MusicMan.instance.m_music) if (m != null && m.m_name == name && m.m_enabled && m.m_clips != null && m.m_clips.Length > 0) return m;
            return null;
        }

        // ---------------------------------------------------------------- beds

        internal static bool Bed(string owner, string name, float vol, float fade)
        {
            if (!AudioBank.IsLoop(name)) { AudioBank.LogOnce("bed " + name, "no bed «" + name + "» (" + owner + ")"); return false; }
            foreach (Line l in Lines) if (l.Live && l.Kind == KindBed && l.Owner == owner && l.Name == name) { Louder(l, vol); FadeTo(l, 1f, fade); return true; } // (asked again: its new loudness, softly up if it was going)
            Need();
            Log("bed: " + name + " (" + owner + ", " + fade.ToString("0.#") + " s)");
            return Start(KindBed, name, owner, vol, true, fade) != null;
        }

        // name null: all of this owner's
        internal static void BedOff(string owner, string name, float fade)
        {
            foreach (Line l in Lines) if (l.Live && l.Kind == KindBed && l.Owner == owner && (name == null || l.Name == name)) FadeTo(l, 0f, fade);
        }

        // ---------------------------------------------------------------- sounds

        internal static bool Sound(string name, Vector3 at, SoundAsk ask)
        {
            if (string.IsNullOrEmpty(name) || name == "-" || AudioMan.instance == null) return false;
            if (ask.Wet && MutedName(name)) return false; // (a sound of the world, a clip's: «mute» counts for it as for the game's own)
            List<AudioBank.Shot> shots = AudioBank.ShotsOf(name);
            if (shots == null) { AudioBank.LogOnce(name, "sound «" + name + "»: none of it in this game"); return false; }
            AudioBank.Entry e = AudioBank.Find(name);
            bool played = false;
            foreach (AudioBank.Shot s in shots)
            {
                AudioClip clip = s.Clips != null && s.Clips.Length > 0 ? s.Clips[UnityEngine.Random.Range(0, s.Clips.Length)] : s.Like != null ? s.Like.clip : null;
                if (clip == null) continue;
                float vol = (s.MaxVol > 0f ? UnityEngine.Random.Range(s.MinVol, s.MaxVol) : 1f) * e.Vol * ask.Volume;
                float pitch = ask.Pitch > 0f ? ask.Pitch : e.Pitch > 0f ? e.Pitch : s.MaxPitch > 0f ? UnityEngine.Random.Range(s.MinPitch, s.MaxPitch) : 1f;
                bool flat = ask.Flat || e.Flat, wet = (ask.Wet || e.Wet) && MixNow(AudioMix.World) >= 0.999f; // (the world down: it would go down with it)
                float range = ask.Range > 0f ? ask.Range : e.Range;
                // a player cannot go past full: what is left over plays as a twin, at the same moment and pitch
                played |= OneShot(name, clip, s.Like, Mathf.Min(1f, vol), pitch, flat, wet, range, at, ask);
                if (vol > 1f) OneShot(name, clip, s.Like, Mathf.Min(1f, vol - 1f), pitch, flat, wet, range, at, ask);
            }
            return played;
        }

        private static bool OneShot(string name, AudioClip clip, AudioSource like, float vol, float pitch, bool flat, bool wet, float range, Vector3 at, SoundAsk ask)
        {
            Line l = Take();
            if (l == null) { TooMany(name); return false; }
            l.Kind = KindSound; l.Name = name; l.Owner = ask.Owner; l.Loop = false; l.Waiting = false; l.Follow = ask.Follow;
            l.Base = vol; l.K = 1f; l.Target = 1f; l.Rate = 0f; l.Started = Time.unscaledTime;
            AudioSource a = l.Src;
            a.clip = clip; a.loop = false; a.pitch = pitch; a.panStereo = 0f;
            a.outputAudioMixerGroup = wet && like != null ? like.outputAudioMixerGroup : AudioMan.instance.m_guiMixer;
            a.bypassReverbZones = !wet;
            a.priority = like != null ? like.priority : 64;
            a.dopplerLevel = 0f;
            a.spatialBlend = flat ? 0f : like != null ? like.spatialBlend : 1f;
            a.spread = like != null ? like.spread : 0f;
            if (range > 0f) { a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = range * 0.3f; a.maxDistance = range; } // (full up to a third, gone at the range)
            else if (like != null)
            {
                a.rolloffMode = like.rolloffMode; a.minDistance = like.minDistance; a.maxDistance = like.maxDistance;
                if (like.rolloffMode == AudioRolloffMode.Custom) a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, like.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            }
            else { a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 5f; a.maxDistance = 100f; }
            l.Go.transform.position = at;
            a.volume = vol;
            a.Play();
            Need();
            return true;
        }

        // name null: all of this owner's
        internal static void StopSounds(string owner, string name, float fade)
        {
            foreach (Line l in Lines) if (l.Live && l.Kind == KindSound && l.Owner == owner && (name == null || l.Name == name)) FadeTo(l, 0f, fade);
        }

        internal static bool SoundOn(string owner, string name)
        {
            foreach (Line l in Lines) if (l.Live && l.Kind == KindSound && l.Owner == owner && l.Name == name && l.Target > 0f) return true;
            return false;
        }

        // ---------------------------------------------------------------- mix

        private sealed class MixClaim { public string Owner; public AudioMix What; public float K; }
        private static readonly List<MixClaim> Claims = new List<MixClaim>();
        private static readonly float[] MixK = { 1f, 1f, 1f, 1f }, MixRate = new float[4];
        private static float _sfxBase = 1f;
        private static bool _worldDown;

        internal static float MixNow(AudioMix m) { return MixK[(int)m]; }

        // k 1: this owner no longer asks
        internal static void Mix(string owner, AudioMix what, float k, float fade)
        {
            k = Mathf.Clamp01(k);
            MixClaim c = null;
            foreach (MixClaim x in Claims) if (x.Owner == owner && x.What == what) c = x;
            if (k >= 0.999f) { if (c != null) Claims.Remove(c); }
            else { if (c == null) { c = new MixClaim { Owner = owner, What = what }; Claims.Add(c); } c.K = k; }
            MixRate[(int)what] = fade > 0.01f ? 1f / fade : 0f;
            if (what == AudioMix.World && !_worldDown && k < 0.999f) { _sfxBase = AudioMan.GetSFXVolume(); _worldDown = true; }
            Log("mix " + what + ": " + k.ToString("0.##") + " (" + owner + ", " + fade.ToString("0.#") + " s)");
            Need();
        }

        private static float MixTarget(AudioMix what)
        {
            float t = 1f;
            foreach (MixClaim c in Claims) if (c.What == what && c.K < t) t = c.K;
            return t;
        }

        private static bool MixTick(float dt)
        {
            bool busy = false;
            for (int i = 0; i < MixK.Length; i++)
            {
                float t = MixTarget((AudioMix)i);
                if (Mathf.Abs(MixK[i] - t) < 0.0005f) { if (MixK[i] != t || (i == 0 && _worldDown && t >= 0.999f)) { MixK[i] = t; if (i == 0) World(); } continue; }
                MixK[i] = MixRate[i] > 0f ? Mathf.MoveTowards(MixK[i], t, MixRate[i] * dt) : t;
                if (i == 0) World();
                busy = true;
            }
            return busy;
        }

        // the mixer's value for the game's sounds: the player's own, times ours (the game writes it as log10 × 10)
        private static void World()
        {
            AudioMan m = AudioMan.instance;
            if (m == null || !_worldDown) return;
            float v = _sfxBase * MixK[0];
            m.m_masterMixer.SetFloat("SfxVol", v > 0f ? Mathf.Log10(Mathf.Clamp(v, 0.001f, 1f)) * 10f : -80f);
            if (MixK[0] >= 0.999f && MixTarget(AudioMix.World) >= 0.999f) _worldDown = false;
        }

        // the player moved the slider while the world is down: that is the new value to come back to
        internal static void SliderMoved(float vol) { if (_worldDown) { _sfxBase = vol; World(); } }

        // the ambient sources (AudioMan keeps writing them, some from their own last value): the true value is put back
        // before its Update and FixedUpdate, and scaled after
        private static AudioSource[] _amb;
        private static float[] _ambReal;
        private static AudioMan _ambOf;
        private static bool _ambScaled;

        private static AudioSource[] Ambient(AudioMan m)
        {
            if (_amb != null && _ambOf == m) return _amb;
            string[] names = { "m_oceanAmbientSource", "m_ambientLoopSource", "m_windLoopSource", "m_shieldHumSource" };
            _amb = new AudioSource[names.Length];
            _ambReal = new float[names.Length];
            _ambOf = m;
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo f = AccessTools.Field(typeof(AudioMan), names[i]);
                if (f != null) _amb[i] = f.GetValue(m) as AudioSource;
                else Log("the game has no " + names[i] + ": that one stays out of «mix ambient»");
            }
            return _amb;
        }

        internal static void AmbientBefore()
        {
            if (!_ambScaled || _amb == null) return;
            for (int i = 0; i < _amb.Length; i++) if (_amb[i] != null) _amb[i].volume = _ambReal[i];
            _ambScaled = false;
        }

        internal static void AmbientAfter(AudioMan m)
        {
            float k = MixK[(int)AudioMix.Ambient];
            if (k >= 0.999f) return;
            AudioSource[] a = Ambient(m);
            for (int i = 0; i < a.Length; i++) if (a[i] != null) { _ambReal[i] = a[i].volume; a[i].volume = _ambReal[i] * k; }
            _ambScaled = true;
        }

        internal static bool RandomAmbience { get { return MixK[(int)AudioMix.Ambient] >= 0.5f; } }

        // the music that is on, ours or the game's: the game writes its source from its own number, so this is safe each frame
        internal static void GameMusicAfter(MusicMan m)
        {
            float k = MixK[(int)AudioMix.Music];
            if (k >= 0.999f) return;
            AudioSource s = FSource != null ? FSource.GetValue(m) as AudioSource : null;
            if (s != null) s.volume *= k;
        }

        // ---------------------------------------------------------------- mute

        private sealed class MuteRule { public string Owner, Pattern; }
        private sealed class Muted { public ZSFX Z; public float Saved, K = 1f; public bool Back; }
        private static readonly List<MuteRule> Rules = new List<MuteRule>();
        private static readonly List<Muted> MutedNow = new List<Muted>();
        private static readonly FieldInfo FLoops = AccessTools.Field(typeof(AudioMan), "m_loopingSfx");

        internal static void Mute(string owner, string pattern)
        {
            string p = pattern.ToLowerInvariant();
            foreach (MuteRule r in Rules) if (r.Owner == owner && r.Pattern == p) return;
            Rules.Add(new MuteRule { Owner = owner, Pattern = p });
            Log("mute: " + p + " (" + owner + ")");
            // the loops already going
            IEnumerable loops = AudioMan.instance != null && FLoops != null ? FLoops.GetValue(AudioMan.instance) as IEnumerable : null;
            if (loops != null) foreach (object o in loops) { ZSFX z = o as ZSFX; if (z != null && Matches(z)) Silence(z); }
            RulesChanged();
            Need();
        }

        // pattern null: all of this owner's
        internal static void Unmute(string owner, string pattern)
        {
            string p = pattern != null ? pattern.ToLowerInvariant() : null;
            int n = Rules.RemoveAll(r => r.Owner == owner && (p == null || r.Pattern == p));
            if (n == 0) return;
            Log("unmute: " + (p ?? "all") + " (" + owner + ")");
            RulesChanged();
            Need();
        }

        private static void RulesChanged()
        {
            foreach (Muted m in MutedNow) m.Back = m.Z == null || !Matches(m.Z);
        }

        private static string BareName(ZSFX z)
        {
            string n = z.name.ToLowerInvariant();
            int c = n.IndexOf("(clone)", StringComparison.Ordinal);
            return (c >= 0 ? n.Substring(0, c) : n).Trim();
        }

        private static bool Matches(ZSFX z) { return Rules.Count > 0 && MutedName(BareName(z)); }

        private static bool MutedName(string name)
        {
            if (Rules.Count == 0) return false;
            string n = name.ToLowerInvariant();
            foreach (MuteRule r in Rules) if (Glob(n, r.Pattern)) return true;
            return false;
        }

        // «a», «a*», «*a», «*a*»
        internal static bool Glob(string name, string p)
        {
            bool head = p.EndsWith("*", StringComparison.Ordinal), tail = p.StartsWith("*", StringComparison.Ordinal);
            string core = p.Trim('*');
            if (core.Length == 0) return true;
            if (head && tail) return name.IndexOf(core, StringComparison.Ordinal) >= 0;
            if (head) return name.StartsWith(core, StringComparison.Ordinal);
            if (tail) return name.EndsWith(core, StringComparison.Ordinal);
            return name == core;
        }

        private static void Silence(ZSFX z)
        {
            foreach (Muted m in MutedNow) if (m.Z == z) { m.Back = false; return; }
            MutedNow.Add(new Muted { Z = z, Saved = z.GetVolumeModifier() });
        }

        // true: let it play. (Asked for every game sound that starts; with no rule it is one number's look)
        internal static bool MayPlay(ZSFX z)
        {
            if (!Matches(z)) return true;
            if (!z.IsLooping()) return false;
            Silence(z);
            Need();
            return true;
        }

        private static bool MuteTick(float dt)
        {
            for (int i = MutedNow.Count - 1; i >= 0; i--)
            {
                Muted m = MutedNow[i];
                if (m.Z == null) { MutedNow.RemoveAt(i); continue; }
                m.K = Mathf.MoveTowards(m.K, m.Back ? 1f : 0f, dt / Mathf.Max(0.05f, MuteFade));
                m.Z.SetVolumeModifier(m.Saved * m.K);
                if (m.Back && m.K >= 1f) MutedNow.RemoveAt(i);
            }
            foreach (Muted m in MutedNow) if (m.Back || m.K > 0f) return true;
            return false;
        }

        // ---------------------------------------------------------------- the ear

        private static string _earOwner;
        private static float _earK, _earTarget, _earRate;

        internal static void Ear(string owner, bool camera, float fade)
        {
            _earOwner = camera ? owner : null;
            _earTarget = camera ? 1f : 0f;
            _earRate = fade > 0.01f ? 1f / fade : 0f;
            if (_earRate == 0f) _earK = _earTarget;
            Log("ear: " + (camera ? "camera" : "player") + " (" + owner + ")");
            Need();
        }

        internal static void ListenerAfter(GameCamera cam)
        {
            if (_earK <= 0f || AudioMan.instance == null) return;
            Player p = Player.m_localPlayer;
            AudioListener l = AudioMan.instance.GetActiveAudioListener();
            if (p == null || l == null || p.m_eye == null) return;
            l.transform.position = Vector3.Lerp(p.m_eye.position, cam.transform.position, _earK);
        }

        // ---------------------------------------------------------------- an owner ends

        // everything this owner holds, let go softly. broken: the scene was cut, its one-shots too
        internal static void End(string owner, float fade, bool broken)
        {
            for (int i = 0; i < Wants.Length; i++)
            {
                Want w = Wants[i];
                if (w == null || w.Owner != owner) continue;
                if (w.Keep > 0f && !broken && w.Name != null) { w.Owner = Kept; w.Until = Time.unscaledTime + w.Keep; continue; } // («keep N»: it rings on past the scene)
                Wants[i] = null;
            }
            _nextFade = fade;
            MusicNow();
            _nextFade = -1f;
            BedOff(owner, null, fade);
            for (int i = 0; i < Claims.Count; i++) if (Claims[i].Owner == owner) MixRate[(int)Claims[i].What] = fade > 0.01f ? 1f / fade : 0f;
            Claims.RemoveAll(c => c.Owner == owner);
            Unmute(owner, null);
            if (_earOwner == owner) Ear(owner, false, Mathf.Min(fade, 1f));
            if (broken) StopSounds(owner, null, 0.5f);
            Need();
        }

        // what another game's scene put here is held at most this long (its «end» may never come: it left)
        private static float _airUntil;
        internal static void AirLease(float seconds) { _airUntil = Time.unscaledTime + seconds; }

        // the world left: nothing we hold means anything now
        internal static void EndAll()
        {
            for (int i = 0; i < Wants.Length; i++) Wants[i] = null;
            _nextFade = 0f;
            MusicNow();
            _nextFade = -1f;
            foreach (Line l in Lines) if (l.Live) Free(l);
            _cur = null; _playing = null;
            Claims.Clear();
            for (int i = 0; i < MixK.Length; i++) { MixRate[i] = 0f; }
            MixTick(0f);
            Rules.Clear();
            foreach (Muted m in MutedNow) if (m.Z != null) m.Z.SetVolumeModifier(m.Saved);
            MutedNow.Clear();
            _earOwner = null; _earK = 0f; _earTarget = 0f;
            AudioBank.Unload();
            Log("all let go (the world is gone)");
        }

        // ---------------------------------------------------------------- lines

        private static Line Take()
        {
            foreach (Line l in Lines) if (!l.Live) { l.Live = true; return l; }
            if (Lines.Count >= MaxLines) return null;
            Need();
            GameObject g = new GameObject("RavenWatch_Audio"); // heavy: a new one only when all the sources made so far are busy, MaxLines at most
            g.transform.SetParent(_run.transform, false);
            Line n = new Line { Go = g, Src = g.AddComponent<AudioSource>(), Live = true };
            n.Src.playOnAwake = false;
            Lines.Add(n);
            return n;
        }

        // music and beds: flat, on their own group; a clip still loading waits (the line's Waiting)
        private static Line Start(int kind, string name, string owner, float vol, bool loop, float fade)
        {
            Line l = Take();
            if (l == null) { TooMany(name); return null; }
            l.Kind = kind; l.Name = name; l.Owner = owner; l.Loop = loop; l.Follow = null; l.Started = Time.unscaledTime;
            l.Asked = vol; l.Base = vol; l.K = 0f; l.Target = 1f; l.Rate = fade > 0.01f ? 1f / fade : 0f; l.Waiting = true;
            if (l.Rate == 0f) l.K = 1f;
            AudioSource a = l.Src;
            a.clip = null; a.spatialBlend = 0f; a.pitch = 1f; a.priority = 0; a.bypassReverbZones = true; a.dopplerLevel = 0f; a.panStereo = 0f;
            a.outputAudioMixerGroup = kind == KindMusic && MusicMan.instance != null ? MusicMan.instance.m_musicMixer : AudioMan.instance != null ? AudioMan.instance.m_guiMixer : null;
            a.volume = 0f;
            Begin(l);
            return l.Live ? l : null;
        }

        private static void Begin(Line l)
        {
            AudioClip clip; float vol;
            int got = AudioBank.Clip(l.Name, out clip, out vol);
            if (got == 0) return;
            l.Waiting = false;
            if (got < 0) { Log("«" + l.Name + "»: nothing to play (" + l.Owner + ")"); Free(l); return; }
            l.Base *= vol;
            l.Started = Time.unscaledTime;
            l.Src.clip = clip; l.Src.loop = l.Loop; l.Src.time = 0f;
            l.Src.volume = Loud(l);
            l.Src.Play();
        }

        // its new asked loudness (what the catalogue adds stays)
        private static void Louder(Line l, float vol) { l.Base = l.Base / Mathf.Max(0.001f, l.Asked) * vol; l.Asked = vol; }

        private static float _toldMany = -100f;
        private static void TooMany(string name)
        {
            if (Time.unscaledTime - _toldMany < 10f) return;
            _toldMany = Time.unscaledTime;
            Log("too many sounds at once: «" + name + "» dropped (said once in 10 s)");
        }

        private static void FadeTo(Line l, float target, float fade)
        {
            l.Target = target;
            l.Rate = fade > 0.01f ? 1f / fade : 0f;
            if (l.Rate == 0f) l.K = target;
        }

        private static float Loud(Line l)
        {
            float k = l.K * l.K * (3f - 2f * l.K); // (an S-curve: no sudden start or stop)
            float v = l.Base * k;
            if (l.Kind == KindMusic) v *= MusicMan.m_masterMusicVolume * MixK[(int)AudioMix.Music];
            return Mathf.Clamp01(v);
        }

        private static void Free(Line l)
        {
            if (l == _cur) { _cur = null; _trackDone = true; }
            l.Live = false; l.Waiting = false; l.Follow = null;
            if (l.Src != null) { l.Src.Stop(); l.Src.clip = null; }
        }

        private static bool LinesTick(float dt)
        {
            bool any = false;
            foreach (Line l in Lines)
            {
                if (!l.Live) continue;
                any = true;
                if (l.Waiting)
                {
                    Begin(l);
                    if (l.Live && l.Waiting && Time.unscaledTime - l.Started > MaxLoad) { Log("«" + l.Name + "»: its file did not come in " + MaxLoad + " s"); Free(l); }
                    if (!l.Live || l.Waiting) continue;
                }
                if (l.K != l.Target) l.K = l.Rate > 0f ? Mathf.MoveTowards(l.K, l.Target, l.Rate * dt) : l.Target;
                if (l.Target <= 0f && l.K <= 0f) { Free(l); continue; }
                if (!l.Src.isPlaying && !AudioListener.pause && Time.unscaledTime - l.Started > 0.2f)
                {
                    if (!l.Loop || l.Src.clip == null) { Free(l); continue; }
                    l.Started = Time.unscaledTime; l.Src.Play(); // (a loop never ends by itself: the sound device changed, Unity stopped it)
                }
                l.Src.volume = Loud(l);
                if (l.Follow != null) l.Go.transform.position = l.Follow.position;
            }
            return any;
        }

        // ---------------------------------------------------------------- the frame (only while something is held or fading)

        private static void Tick()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool held = false;
            for (int i = 0; i < Wants.Length; i++)
            {
                Want w = Wants[i];
                if (w == null) continue;
                bool done = w.Owner == Kept && i == TopLayer() && _trackDone;
                if ((w.Until > 0f && Time.unscaledTime >= w.Until) || done) { Log("music " + (AudioLayer)i + ": " + (w.Name ?? "silence") + " ended"); Wants[i] = null; _nextFade = EndFade; }
                else held = true;
            }
            if (_held || held) MusicNow();
            bool busy = held || _held;
            if (ZNet.instance == null && (busy || Claims.Count > 0 || Rules.Count > 0 || _earOwner != null)) { EndAll(); return; }
            if (_airUntil > 0f && Time.unscaledTime >= _airUntil) { _airUntil = 0f; End(Air, EndFade, false); }
            if (_earK != _earTarget) { _earK = _earRate > 0f ? Mathf.MoveTowards(_earK, _earTarget, _earRate * dt) : _earTarget; busy = true; }
            if (_earOwner != null) busy = true;
            busy |= MixTick(dt);
            busy |= Claims.Count > 0;
            busy |= MuteTick(dt);
            busy |= Rules.Count > 0;
            busy |= LinesTick(dt);
            if (!busy) _run.enabled = false;
        }

        private static int TopLayer()
        {
            for (int i = Wants.Length - 1; i >= 0; i--) if (Wants[i] != null) return i;
            return -1;
        }

        private static void Need()
        {
            if (_run == null)
            {
                GameObject g = new GameObject("RavenWatch_AudioCore");
                UnityEngine.Object.DontDestroyOnLoad(g);
                _run = g.AddComponent<Run>();
            }
            _run.enabled = true;
        }

        // a load runs here (AudioBank)
        internal static Coroutine Host(IEnumerator job) { Need(); return _run.StartCoroutine(job); } // (a coroutine goes on when the component is switched off)

        private sealed class Run : MonoBehaviour
        {
            private void Update()
            {
                try { Tick(); }
                catch (Exception e) { Log("the frame failed: " + e); enabled = false; }
            }
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Audio: " + s); }

        // «raven audio»: what is held, and by whom
        internal static string Report()
        {
            StringBuilder b = new StringBuilder("Audio:\n");
            for (int i = Wants.Length - 1; i >= 0; i--) b.Append("  music ").Append((AudioLayer)i).Append(": ").Append(Wants[i] == null ? "-" : (Wants[i].Name ?? "silence") + " (" + Wants[i].Owner + ")").Append('\n');
            b.Append("  the game's music ").Append(_held ? "held off" : "free").Append(", on now: ").Append(_playing ?? "-").Append(_cur != null && _cur.Waiting ? " (loading)" : "").Append('\n');
            b.Append("  mix: world ").Append(MixK[0].ToString("0.##")).Append(", ambient ").Append(MixK[1].ToString("0.##")).Append(", music ").Append(MixK[2].ToString("0.##")).Append(", voice ").Append(MixK[3].ToString("0.##")).Append('\n');
            foreach (MixClaim c in Claims) b.Append("    ").Append(c.What).Append(' ').Append(c.K.ToString("0.##")).Append(" (").Append(c.Owner).Append(")\n");
            foreach (MuteRule r in Rules) b.Append("  mute ").Append(r.Pattern).Append(" (").Append(r.Owner).Append(")\n");
            if (MutedNow.Count > 0) b.Append("  loops at zero: ").Append(MutedNow.Count).Append('\n');
            b.Append("  ear: ").Append(_earK > 0f ? "camera" : "player").Append('\n');
            foreach (Line l in Lines) if (l.Live) b.Append("  ").Append(l.Kind == KindMusic ? "track " : l.Kind == KindBed ? "bed " : "sound ").Append(l.Name).Append(" (").Append(l.Owner.Length > 0 ? l.Owner : "-").Append(") ").Append(l.Waiting ? "loading" : l.Src.time.ToString("0.0") + " s, vol " + l.Src.volume.ToString("0.00")).Append('\n');
            return b.ToString();
        }
    }

    // ---------------------------------------------------------------- the game's side: each patch one line into the core
    [HarmonyPatch(typeof(MusicMan), "UpdateCurrentMusic")]
    internal static class AudioMusicPick
    {
        private static bool Prefix() { return !Audio.MusicHeld; } // (held: the game picks nothing; its source still fades out in UpdateMusic)
    }

    [HarmonyPatch(typeof(MusicMan), "Update")]
    internal static class AudioMusicMix
    {
        private static void Postfix(MusicMan __instance)
        {
            try { Audio.GameMusicAfter(__instance); }
            catch (Exception e) { PatchGuard.Fail("AudioMusicMix", e); }
        }
    }

    [HarmonyPatch(typeof(AudioMan), "Update")]
    internal static class AudioAmbientUpdate
    {
        private static void Prefix() { try { Audio.AmbientBefore(); } catch (Exception e) { PatchGuard.Fail("AudioAmbientUpdate", e); } }
        private static void Postfix(AudioMan __instance) { try { Audio.AmbientAfter(__instance); } catch (Exception e) { PatchGuard.Fail("AudioAmbientUpdate", e); } }
    }

    [HarmonyPatch(typeof(AudioMan), "FixedUpdate")]
    internal static class AudioAmbientFixed
    {
        private static void Prefix() { try { Audio.AmbientBefore(); } catch (Exception e) { PatchGuard.Fail("AudioAmbientFixed", e); } }
        private static void Postfix(AudioMan __instance) { try { Audio.AmbientAfter(__instance); } catch (Exception e) { PatchGuard.Fail("AudioAmbientFixed", e); } }
    }

    [HarmonyPatch(typeof(AudioMan), "UpdateRandomAmbient")]
    internal static class AudioRandomAmbient { private static bool Prefix() { return Audio.RandomAmbience; } }

    [HarmonyPatch(typeof(AudioMan), "UpdateLavaAmbient")]
    internal static class AudioLavaAmbient { private static bool Prefix() { return Audio.RandomAmbience; } }

    [HarmonyPatch(typeof(AudioMan), "RequestPlaySound")]
    internal static class AudioGate
    {
        private static bool Prefix(ZSFX sfx, ref bool __result)
        {
            try
            {
                if (Audio.MayPlay(sfx)) return true;
                __result = false;
                return false;
            }
            catch (Exception e) { PatchGuard.Fail("AudioGate", e); return true; }
        }
    }

    [HarmonyPatch(typeof(AudioMan), "SetSFXVolume")]
    internal static class AudioSlider
    {
        private static void Postfix(float vol) { try { Audio.SliderMoved(vol); } catch (Exception e) { PatchGuard.Fail("AudioSlider", e); } }
    }

    [HarmonyPatch(typeof(GameCamera), "UpdateListner")]
    internal static class AudioEar
    {
        private static void Postfix(GameCamera __instance) { try { Audio.ListenerAfter(__instance); } catch (Exception e) { PatchGuard.Fail("AudioEar", e); } }
    }
}
#endif

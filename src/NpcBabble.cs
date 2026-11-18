#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HuginWhispers
{
    // The people's wordless talk (the user 2026-11-15: «без войс пакету … бурчати, бельмекати, щось абстрактне, що символізувало б діалог …
    // на будь-якій мові»): every line a person says is hummed in its own voice — no letters, no syllables, only how a person speaks.
    // Found by ear over ten trials that day; what each rule answers is said beside it. Two things, both written by the author's tool
    // («node tools/sound.js babble» → assets/story/sounds/babble.txt, tools/sound_babble.py), so the game analyses no sound:
    //   the manner   phrases of real reading with none of their sound: every 10 ms — is there voice, how the pitch stands to the phrase's
    //                middle, how loud it is; how many syllables the phrase has and whether it ends rising. Nobody's voice: every voice uses all.
    //   the hums     where each file of a voice's wordless sounds (hm, hm_ask, uhu, em, chuckle, sigh — the files the voice has anyway, of
    //                every timbre) holds a stretch of steady voice, and its pitch.
    // A line (Render): its sentences and the parts between its commas, each said with a phrase of the manner that has about as many
    // syllables and the same kind of end (a question: a rising one, or a falling one with its end lifted). Where that phrase has voice, a
    // stretch of the person's own hum sounds — played a little faster or slower to follow the phrase's pitch (never far: a hum far from its
    // own speed is no longer that voice), as loud as the phrase is; where the phrase is silent (its consonants, its breaths), so is this.
    // The rules of the ear:
    //   · nothing filtered, nothing repeated or stretched: the hum as recorded («зроби чистий звук», «задвоєння»)
    //   · some phrases slow (Calm): longer, their near sounds run into one, their shortest left out, a stop or two inside, a longer pause after
    //     («розтягувати … щоб була пауза, а не потік незрозумілих звуків»)
    //   · no stretch of hum twice in a line; a recording the less likely the more of it the line has had; the ones known at once (uhu, em, a
    //     chuckle, a sigh) once a line («6 разів угу»)
    //   · each sound at a speed of its own, never the same twice running; some cut short («деякі замедлити, деякі пришвидшити»)
    //   · every sound comes in softly, dies away, and has a silence of its own length after it («після одного звуку різко йде другий»)
    // The same on every game: the choices are numbers of the line's own words and the person's seed, nothing is sent.
    // When: NpcSpeech.Said, for every line a person says near the eye (over its head and in your talk) — quieter between people than to you;
    // and the hero's own answers in a talk (NpcTalk.Pick: his «hero» timbre, NpcTimbre.OfHero; heard on his game only, as his window is his);
    // not while the voice package is on (RavenVoice.Available: his rule, kept though the package voices only the ravens yet), nor a line it really speaks (Voiced). While a line is hummed
    // the speaker's own short sounds (its breath, its «хм» written in the words, its sigh) are not played over it: NpcSpeech asks Say's answer.
    // Cost: once a voice — its files' samples copied (a few MB, the last VoicesKept kept); once a line — one pass over its samples (a line
    // of 8 s: some 350 000) and one clip made, let go when it has played.
    internal static class NpcBabble
    {
        // ---------------------------------------------------------------- numbers (raven npc tune NpcBabble.<name>)
        internal static bool On = true, Trace = false;
        internal static float Vol = 0.38f, VolToYou = 0.55f, Range = 9f;   // between people · in your talk · m
        internal static float Calm = 0.5f, SlowBy = 1.35f;                 // the share of slow phrases · how much longer one is
        internal static float Loud = 0.12f;                                // the line's own level before Vol
        internal static float LeastSound = 0.11f, ShortChance = 0.3f, ShortOver = 0.3f, ShortTo = 0.55f; // s · of sounds over ShortOver s, cut to this share
        internal static float FadeIn = 0.035f, FadeOut = 0.09f;            // s
        internal static float SpeedLow = 0.78f, SpeedHigh = 1.28f, Together = 0.6f; // a hum never played beyond these of its own speed · hums of different pitch brought this far together
        internal static float OverBy = 0.6f, MostTempo = 1.5f;             // s a hummed line may outlast its shown one · quickened at most by this to fit
        internal static int VoicesKept = 8;
        private static readonly float[] Speeds = { 0.84f, 0.92f, 1f, 1.1f, 1.19f };
        private static readonly float[] Rests = { 0.10f, 0.10f, 0.16f, 0.16f, 0.24f, 0.36f };
        private static readonly string[] Sounds = { "speech_hm", "speech_hm_ask", "speech_uhu", "speech_em", "speech_chuckle", "speech_sigh" };
        private static readonly string[] Rare = { "uhu", "em", "chu", "sig" };
        private const string MannerWord = "@manner", StepWord = "@mannerstep", HumWord = "@hum";
        private const string Vowels = "аеєиіїоуюяaeiouyАЕЄИІЇОУЮЯAEIOUY";

        private sealed class Phrase { public int Hills; public bool Rising; public bool[] V; public float[] F, E; }
        private sealed class Spot { public string Kind; public int First, Count; public float Hz; }
        private sealed class Hum { public float[] X; public float Hz; public string Kind; }
        private sealed class Voice { public readonly List<Hum> Hums = new List<Hum>(); public float Hz; public int Rate; public float At; }
        private sealed class Made { public AudioClip Clip; public float Until; }

        private static List<Phrase> _manner;
        private static Dictionary<string, List<Spot>> _spots;
        private static float _step = 0.01f;
        private static int _mostHills = 1;
        private static readonly Dictionary<string, Voice> Voices = new Dictionary<string, Voice>();
        // (by name: GetData has a Span overload the C# 5 compiler cannot resolve — as SoundBack reads its clips)
        private static readonly System.Reflection.MethodInfo GetDataM = typeof(AudioClip).GetMethod("GetData", new Type[] { typeof(float[]), typeof(int) });
        private static readonly List<Made> Clips = new List<Made>();

        internal static Func<string, bool> Voiced; // a person's line the voice package speaks aloud: not hummed (nobody sets it yet: the package voices the ravens only)

        internal static void Forget() { _manner = null; _spots = null; Voices.Clear(); } // (the sound files read anew: AudioBank.Again)

        // ---------------------------------------------------------------- the files
        private static void Read()
        {
            _manner = new List<Phrase>(); _spots = new Dictionary<string, List<Spot>>(StringComparer.OrdinalIgnoreCase); _mostHills = 1;
            foreach (KeyValuePair<string, string> f in StoryFiles.All(AudioBank.Kind, null, false))
                foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(f.Value))
                {
                    string l = ln.Value;
                    if (l.Length < 5 || l[0] != '@') continue;
                    string[] w = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w[0] == StepWord && w.Length >= 2) _step = Mathf.Clamp(Errands.F(w[1], 0.01f), 0.004f, 0.05f);
                    else if (w[0] == MannerWord && w.Length >= 4) { Phrase p = PhraseOf(w); if (p != null) { _manner.Add(p); _mostHills = Mathf.Max(_mostHills, p.Hills); } }
                    else if (w[0] == HumWord && w.Length >= 6)
                    {
                        Spot s = new Spot { Kind = w[2], First = Errands.I(w[3], -1), Count = Errands.I(w[4], 0), Hz = Errands.F(w[5], 0f) };
                        if (s.First < 0 || s.Count < 64 || s.Hz < 40f) continue;
                        List<Spot> list;
                        if (!_spots.TryGetValue(w[1], out list)) _spots[w[1]] = list = new List<Spot>();
                        list.Add(s);
                    }
                }
            if (_manner.Count == 0) AudioBank.LogOnce("babble none", "the people's wordless talk: no «" + MannerWord + "» line in the sound files (node tools/sound.js babble): they keep their short sounds only");
        }

        // a person's hums: the steady stretches of its voice's own files, copied once. Null: its files are still on their way (said again
        // at its next line); a voice with too few to talk with: none, told once
        private static Voice VoiceOf(NpcTimbre.Card card)
        {
            string key = card.Plain ? "" : card.Timbre;
            Voice v;
            if (Voices.TryGetValue(key, out v)) { v.At = Time.time; return v; }
            List<KeyValuePair<string, AudioClip>> files = new List<KeyValuePair<string, AudioClip>>();
            bool all = true;
            foreach (string sound in Sounds)
            {
                string name = NpcTimbre.Name(card, sound);
                if (!AudioBank.Has(name)) continue;
                all &= AudioBank.FilesOf(AudioBank.Find(name), files);
            }
            foreach (KeyValuePair<string, AudioClip> f in files)
            {
                if (f.Value.loadState == AudioDataLoadState.Unloaded) f.Value.LoadAudioData();
                if (f.Value.loadState == AudioDataLoadState.Loading) all = false;
            }
            if (!all) return null;
            v = new Voice { At = Time.time };
            List<float> hz = new List<float>();
            foreach (KeyValuePair<string, AudioClip> f in files)
            {
                List<Spot> spots;
                AudioClip c = f.Value;
                if (!_spots.TryGetValue(Path.GetFileName(f.Key), out spots) || c.loadState != AudioDataLoadState.Loaded) continue;
                int ch = Mathf.Max(1, c.channels);
                float[] buf = new float[c.samples * ch];
                if (GetDataM == null || !(bool)GetDataM.Invoke(c, new object[] { buf, 0 })) { AudioBank.LogOnce("babble data " + f.Key, "the people's wordless talk: the samples of «" + f.Key + "» cannot be read (kept compressed?): left out"); continue; }
                foreach (Spot s in spots)
                {
                    int n = Mathf.Min(s.Count, c.samples - s.First);
                    if (n < c.frequency / 8) continue;
                    v.Hums.Add(new Hum { X = Steady(buf, s.First, n, ch), Hz = s.Hz, Kind = s.Kind });
                    hz.Add(s.Hz);
                    v.Rate = c.frequency;
                }
            }
            if (v.Hums.Count < 4)
            {
                AudioBank.LogOnce("babble few " + key, "the people's wordless talk: the voice «" + (key.Length > 0 ? key : "own") + "» has " + v.Hums.Count + " stretch(es) of hum (node tools/sound.js babble): too few to talk with, it keeps its short sounds");
                v.Hums.Clear();
            }
            else { hz.Sort(); v.Hz = hz[hz.Count / 2]; }
            if (Voices.Count >= VoicesKept) // (the one not heard longest is let go)
            {
                string old = null; float at = float.MaxValue;
                foreach (KeyValuePair<string, Voice> kv in Voices) if (kv.Value.At < at) { at = kv.Value.At; old = kv.Key; }
                if (old != null) Voices.Remove(old);
            }
            Voices[key] = v;
            return v;
        }

        // ---------------------------------------------------------------- a line said
        private const string SpeechOwner = "speech";

        // a hummed line of that clip's name stopped where it is (faded out quickly)
        internal static void Hush(string clip) { Audio.StopSounds(SpeechOwner, clip, 0.12f); }

        // the seconds the line is hummed for, from now (0: not hummed); hummed, the speaker's own short sounds keep out of it. clip: the name its
        // sound goes by in the sound core — one that may be cut short later says its own (Hush: the hero's answer, NpcTalk)
        internal static float Say(NpcTimbre.Card card, Transform head, string text, uint seed, float seconds, bool toYou, string clip = "babble")
        {
            if (!On || card == null || head == null || string.IsNullOrEmpty(text) || AudioMan.instance == null) return 0f;
            if (RavenVoice.Available) return 0f; // (the voice package is on: no hum — the user 2026-11-15, asked again when the people fell silent with it: «це правило так і треба»; to hear the hum with the package installed, switch «Ravens speak aloud» off)
            if (Voiced != null && Voiced(text)) return 0f;
            for (int i = Clips.Count - 1; i >= 0; i--) if (Time.unscaledTime > Clips[i].Until) { if (Clips[i].Clip != null) UnityEngine.Object.Destroy(Clips[i].Clip); Clips.RemoveAt(i); }
            if (_manner == null) Read();
            if (_manner.Count == 0) return 0f;
            Voice v = VoiceOf(card);
            if (v == null || v.Hums.Count == 0 || v.Rate <= 0) return 0f;
            float[] y = Line(text, v, card.Pitch, seed, seconds);
            if (y == null) return 0f;
            // (filled through the reader callback: SetData has a Span overload the C# 5 compiler cannot resolve — as RavenVoice makes its clips)
            float[] line = y; int given = 0;
            AudioClip made = AudioClip.Create(clip, line.Length, 1, v.Rate, false, buf => { int k = Math.Min(buf.Length, line.Length - given); if (k > 0) Array.Copy(line, given, buf, 0, k); for (int i = Math.Max(0, k); i < buf.Length; i++) buf[i] = 0f; given += Math.Max(0, k); });
            bool heard = Audio.PlayClip(made, null, head.position, new SoundAsk { Volume = toYou ? VolToYou : Vol, Range = Range, Follow = head, Owner = SpeechOwner });
            Clips.Add(new Made { Clip = made, Until = Time.unscaledTime + y.Length / (float)v.Rate + 1f });
            if (Trace) Npcs.Log("babble: «" + (text.Length > 40 ? text.Substring(0, 40) + "…" : text) + "» " + (y.Length / (float)v.Rate).ToString("0.0") + " s for a line of " + seconds.ToString("0.0") + " s, voice «" + card.Timbre + "» (" + v.Hums.Count + " hums)" + (heard ? "" : " — NOT played by the sound core"));
            return heard ? y.Length / (float)v.Rate : 0f;
        }

        // ---------------------------------------------------------------- the line's rules
        // From here to «end of the line's rules» is what a hummed line IS: the author's tool plays the same outside the game (node tools/sound.js
        // talk: tools/sound_talk.py, a copy in Python that reads the numbers above from this file), so a new voice or a number is heard on the
        // listening page with no game run. A change here (not a comment, not a space) stops the build (tools/arch_check.js «babble-copy») till the
        // copy is made to play the same and its mark written anew.

        // the line as said: rendered, said once more quicker when much longer than it is shown, trailing off where it is longer still (null: nothing to say)
        private static float[] Line(string text, Voice v, float pitch, uint seed, float seconds)
        {
            pitch = Mathf.Clamp(pitch, 0.8f, 1.25f);
            float[] y = Render(text, v, pitch, seed, 1f);
            if (y == null) return null;
            float want = Mathf.Max(0.8f, seconds);
            if (y.Length / (float)v.Rate > want + OverBy) // (longer than the line is shown: said once more, quicker)
            {
                float[] quick = Render(text, v, pitch, seed, Mathf.Min(MostTempo, y.Length / (float)v.Rate / want));
                if (quick != null) y = quick;
            }
            int most = (int)((want + OverBy + 0.4f) * v.Rate);
            if (y.Length > most) // (still longer: it trails off there)
            {
                int f = Mathf.Min(most, (int)(0.35f * v.Rate));
                for (int i = 0; i < f; i++) y[most - f + i] *= 1f - i / (float)f;
                Array.Resize(ref y, most);
            }
            return y;
        }

        // a stretch of steady hum out of its file's samples (one channel), as loud as every other: the manner sets the loudness
        private static float[] Steady(float[] buf, int first, int n, int ch)
        {
            float[] x = new float[n];
            double sum = 0d;
            for (int i = 0; i < n; i++) { float y = buf[(first + i) * ch]; x[i] = y; sum += y * y; }
            float gain = 0.1f / Mathf.Max(1e-5f, Mathf.Sqrt((float)(sum / n)));
            for (int i = 0; i < n; i++) x[i] *= gain;
            return x;
        }

        // «@manner <syllables> <0|1> <two hex bytes a frame: pitch, loudness>»: the pitch of a frame with no voice is drawn between its neighbours
        private static Phrase PhraseOf(string[] w)
        {
            string h = w[3];
            int n = h.Length / 4;
            if (n < 4) return null;
            Phrase p = new Phrase { Hills = Mathf.Max(1, Errands.I(w[1], 1)), Rising = w[2] == "1", V = new bool[n], F = new float[n], E = new float[n] };
            int last = -1;
            for (int i = 0; i < n; i++)
            {
                int a, b;
                if (!int.TryParse(h.Substring(i * 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a) || !int.TryParse(h.Substring(i * 4 + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return null;
                p.V[i] = a > 0; p.E[i] = b / 255f * 1.6f;
                if (a == 0) continue;
                p.F[i] = 0.80f + (a - 1) / 254f * 0.45f;
                for (int k = last + 1; k < i; k++) p.F[k] = last < 0 ? p.F[i] : Mathf.Lerp(p.F[last], p.F[i], (k - last) / (float)(i - last));
                last = i;
            }
            if (last < 0) return null;
            for (int k = last + 1; k < n; k++) p.F[k] = p.F[last];
            return p;
        }

        // a number of the line's own (FNV-1a over the key, then its three small numbers): the same on every game
        private static uint H(uint seed, string key, int a, int b, int c)
        {
            uint h = 2166136261u ^ seed;
            for (int i = 0; i < key.Length; i++) { h ^= key[i]; h *= 16777619u; }
            h ^= (uint)a * 0x9E3779B1u; h *= 16777619u; h ^= (uint)b * 0x85EBCA6Bu; h *= 16777619u; h ^= (uint)c * 0xC2B2AE35u; h *= 16777619u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return h;
        }

        private sealed class Clause { public int Syllables; public char Mark; public string Key; }
        private static readonly char[] Edge = { '.', ',', '!', '?', '…', ':', ';', '«', '»', '"', '(', ')', '—', '-', '\'' };

        private static List<Clause> ClausesOf(string text)
        {
            List<Clause> list = new List<Clause>();
            int syl = 0; System.Text.StringBuilder key = new System.Text.StringBuilder();
            foreach (string w in text.Replace("—", " — ").Replace("...", "…").Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string core = w.Trim(Edge);
                char mark = w.IndexOf('?') >= 0 ? '?' : w.IndexOf('!') >= 0 ? '!' : w.IndexOf('…') >= 0 ? '~' : w.EndsWith(".") ? '.' : (w.EndsWith(",") || w.EndsWith(";") || w.EndsWith(":") || w == "—") ? ',' : ' ';
                if (core.Length > 0)
                {
                    int n = 0;
                    foreach (char ch in core) if (Vowels.IndexOf(ch) >= 0) n++;
                    syl += Mathf.Max(1, n);
                    key.Append(core.ToLowerInvariant()).Append(' ');
                }
                if (mark != ' ' && syl > 0) { list.Add(new Clause { Syllables = syl, Mark = mark, Key = key.ToString() }); syl = 0; key.Length = 0; }
            }
            if (syl > 0) list.Add(new Clause { Syllables = syl, Mark = '.', Key = key.ToString() });
            return list;
        }

        // the line as samples at the voice's rate (null: nothing to say in it)
        private static float[] Render(string text, Voice v, float pitch, uint seed, float tempo)
        {
            List<Clause> clauses = ClausesOf(text);
            if (clauses.Count == 0) return null;
            List<float[]> parts = new List<float[]>();
            List<int> used = new List<int>();
            string lastKind = "";
            float stepS = _step * v.Rate; // samples a frame
            int total = 0;
            foreach (Clause cl in clauses)
            {
                // a part longer than any phrase of the manner: said in two breaths
                int[] chunks = cl.Syllables <= _mostHills * 1.25f ? new[] { cl.Syllables } : new[] { cl.Syllables - cl.Syllables / 2, cl.Syllables / 2 };
                for (int ci = 0; ci < chunks.Length; ci++)
                {
                    int m = Mathf.Max(1, chunks[ci]);
                    bool last = ci == chunks.Length - 1, wantRise = last && cl.Mark == '?';
                    Phrase p = Pick(m, wantRise, seed, cl.Key, ci);
                    bool slow = H(seed, cl.Key, ci, 0, 2) % 100u < (uint)(Calm * 100f);
                    float scale = Mathf.Clamp(m / (float)p.Hills, 0.75f, 1.3f) / Mathf.Max(0.5f, tempo) * (slow ? SlowBy : 1f);
                    int frames = Mathf.Max(4, (int)(p.F.Length * scale));
                    bool[] on = new bool[frames]; float[] ff = new float[frames], ee = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float src = i * (p.F.Length - 1) / (float)(frames - 1);
                        int a = (int)src, b = Mathf.Min(p.F.Length - 1, a + 1); float t = src - a;
                        on[i] = p.V[Mathf.Min(p.F.Length - 1, Mathf.RoundToInt(src))];
                        ff[i] = Mathf.Lerp(p.F[a], p.F[b], t); ee[i] = Mathf.Lerp(p.E[a], p.E[b], t);
                    }
                    if (wantRise && !p.Rising) { int k = (int)(frames * 0.75f); for (int i = k; i < frames; i++) ff[i] *= Mathf.Lerp(1f, 1.12f, (i - k) / (float)Mathf.Max(1, frames - 1 - k)); } // (a question said with a statement's phrase: its end lifted)
                    if (cl.Mark == '!' && last) for (int i = 0; i < frames; i++) { ff[i] *= 1.03f; ee[i] *= 1.15f; }
                    Tidy(on, ee, slow ? 11 : 3, slow ? 10 : 4);
                    float[] y = Sounds_(on, ff, ee, frames, stepS, v, pitch, seed, cl.Key, ci, slow, used, ref lastKind);
                    parts.Add(y); total += y.Length;
                    float pause = (!last ? 0.14f : cl.Mark == '~' ? 0.75f : cl.Mark == ',' ? 0.24f : 0.5f) * (slow ? 1.6f : 1f);
                    float[] rest = new float[(int)(pause * v.Rate)];
                    parts.Add(rest); total += rest.Length;
                }
            }
            float[] line = new float[total];
            int at = 0; double sum = 0d; int sounding = 0;
            foreach (float[] part in parts) { Array.Copy(part, 0, line, at, part.Length); at += part.Length; }
            for (int i = 0; i < total; i++) { float s = line[i]; if (s > 1e-4f || s < -1e-4f) { sum += s * s; sounding++; } }
            if (sounding < v.Rate / 20) return null;
            float gain = Loud / Mathf.Max(1e-5f, Mathf.Sqrt((float)(sum / sounding)));
            for (int i = 0; i < total; i++) line[i] = Mathf.Clamp(line[i] * gain, -1f, 1f);
            return line;
        }

        // the phrase of the manner for a part of m syllables: one of the three that fit best (as many syllables, the same kind of end)
        private static Phrase Pick(int m, bool rise, uint seed, string key, int ci)
        {
            int a = -1, b = -1, c = -1; float sa = float.MaxValue, sb = float.MaxValue, sc = float.MaxValue;
            for (int r = 0; r < _manner.Count; r++)
            {
                Phrase p = _manner[r];
                float s = Mathf.Abs(p.Hills - m) + (p.Rising != rise ? 1.5f : 0f) + (H(seed, key, ci, r, 0) % 1000u) / 100000f;
                if (s < sa) { c = b; sc = sb; b = a; sb = sa; a = r; sa = s; }
                else if (s < sb) { c = b; sc = sb; b = r; sb = s; }
                else if (s < sc) { c = r; sc = s; }
            }
            int n = c >= 0 ? 3 : b >= 0 ? 2 : 1, k = (int)(H(seed, key, ci, 0, 1) % (uint)n);
            return _manner[k == 0 ? a : k == 1 ? b : c];
        }

        // the phrase's voice, frame by frame: a short silence between two sounds makes them one, a sound too short is left out, and inside a
        // sound the loudness sinks but never dies
        private static void Tidy(bool[] on, float[] ee, int join, int least)
        {
            int n = on.Length, i = 0, lastEnd = -1;
            while (i < n)
            {
                if (!on[i]) { i++; continue; }
                if (lastEnd >= 0 && i - lastEnd < join) for (int k = lastEnd; k < i; k++) on[k] = true;
                while (i < n && on[i]) i++;
                lastEnd = i;
            }
            i = 0;
            while (i < n)
            {
                if (!on[i]) { i++; continue; }
                int a = i; float top = 0f;
                while (i < n && on[i]) { top = Mathf.Max(top, ee[i]); i++; }
                if (i - a < least) { for (int k = a; k < i; k++) on[k] = false; continue; }
                for (int k = a; k < i; k++) ee[k] = Mathf.Max(ee[k], 0.45f * top);
            }
        }

        // the sounds of one phrase: each stretch of its voice hummed by a stretch of the person's own hum
        private static float[] Sounds_(bool[] on, float[] ff, float[] ee, int frames, float stepS, Voice v, float pitch, uint seed, string key, int ci, bool slow, List<int> used, ref string lastKind)
        {
            int n = (int)(frames * stepS);
            float[] y = new float[n];
            List<int> from = new List<int>(), to = new List<int>(); // (the sounds made: where each begins and ends)
            int f = 0, region = 0, lastSpeed = -1;
            float lo = SpeedLow * pitch, hi = SpeedHigh * pitch;
            while (f < frames)
            {
                if (!on[f]) { f++; continue; }
                int g = f;
                while (g < frames && on[g]) g++;
                int sp = (int)(H(seed, key, ci, region, 3) % (uint)Speeds.Length);
                if (sp == lastSpeed) sp = (sp + 2) % Speeds.Length;
                lastSpeed = sp;
                if ((g - f) * _step > ShortOver && H(seed, key, ci, region, 4) % 100u < (uint)(ShortChance * 100f)) { int cut = f + (int)((g - f) * ShortTo); for (int q = cut; q < g; q++) on[q] = false; g = cut; }
                if ((g - f) * _step >= LeastSound)
                {
                    int i0 = (int)(f * stepS), i1 = Mathf.Min(n, (int)(g * stepS));
                    // the stretch of hum: not one the line has used, not of the recording just heard, of the known ones once a line — and long enough
                    int best = -1; float bestScore = float.MaxValue; int longest = 0;
                    for (int r = 0; r < v.Hums.Count; r++)
                    {
                        Hum hm = v.Hums[r];
                        if (hm.X.Length > v.Hums[longest].X.Length) longest = r;
                        float need = Need(hm, v, ff, i0, i1, stepS, Speeds[sp] * pitch, lo, hi);
                        if (hm.X.Length < need + 8f) continue;
                        float score = (used.Contains(r) ? 8f : 0f) + (hm.Kind == lastKind ? 2f : 0f) + (H(seed, key, ci * 64 + region, r, 5) % 1000u) / 1000f;
                        bool rare = Array.IndexOf(Rare, hm.Kind) >= 0;
                        foreach (int u in used) if (v.Hums[u].Kind == hm.Kind) score += rare ? 12f : 1f;
                        if (score < bestScore) { bestScore = score; best = r; }
                    }
                    if (best < 0) // (no hum so long: the longest, and the sound ends where it does)
                    {
                        best = longest;
                        float room0 = v.Hums[best].X.Length - 8f, pos0 = 0f; int s0 = i0;
                        float k0 = Speeds[sp] * pitch * Mathf.Pow(v.Hz / v.Hums[best].Hz, Together);
                        while (s0 < i1 && pos0 < room0) { pos0 += Mathf.Clamp(k0 * At(ff, s0 / stepS), lo, hi); s0++; }
                        i1 = s0; g = Mathf.Max(f + 1, Mathf.CeilToInt(i1 / stepS));
                    }
                    Hum h = v.Hums[best];
                    float k = Speeds[sp] * pitch * Mathf.Pow(v.Hz / h.Hz, Together);
                    int room = (int)(h.X.Length - Need(h, v, ff, i0, i1, stepS, Speeds[sp] * pitch, lo, hi) - 8f);
                    float pos = room > 0 ? H(seed, key, ci * 64 + region, 0, 6) % (uint)(room + 1) : 0f;
                    int len = i1 - i0, fin = Mathf.Min(len / 3, (int)(FadeIn * v.Rate)), fout = Mathf.Min(len / 2, (int)(FadeOut * v.Rate));
                    for (int s = 0; s < len; s++)
                    {
                        float fr = (i0 + s) / stepS;
                        pos += Mathf.Clamp(k * At(ff, fr), lo, hi);
                        int a = (int)pos;
                        if (a >= h.X.Length - 1) break;
                        float x = Mathf.Lerp(h.X[a], h.X[a + 1], pos - a) * At(ee, fr);
                        if (s < fin) x *= s / (float)fin;
                        else if (s >= len - fout) { float d = (len - s) / (float)fout; x *= d * Mathf.Sqrt(d); }
                        y[i0 + s] = x;
                    }
                    used.Add(best); lastKind = h.Kind;
                    from.Add(i0); to.Add(i1);
                    // after every sound a silence of its own length: what of the next sound falls into it is not said
                    int rest = (int)(Rests[(int)(H(seed, key, ci * 64 + region, 0, 7) % (uint)Rests.Length)] * (slow ? 1.4f : 1f) / _step);
                    for (int q = g; q < g + rest && q < frames; q++) on[q] = false;
                }
                region++;
                f = Mathf.Max(g, f + 1);
            }
            if (!slow || from.Count < 2) return y;
            // a slow phrase stops inside itself: at its widest gap, and at its next widest
            int g1 = -1, g2 = -1;
            for (int i = 1; i < from.Count; i++)
            {
                int w = from[i] - to[i - 1];
                if (g1 < 0 || w > from[g1] - to[g1 - 1]) { g2 = g1; g1 = i; }
                else if (g2 < 0 || w > from[g2] - to[g2 - 1]) g2 = i;
            }
            int later = Mathf.Max(g1, g2), earlier = g2 < 0 ? -1 : Mathf.Min(g1, g2);
            int addLater = (int)(0.32f * v.Rate), addEarlier = earlier < 0 ? 0 : (int)(0.2f * v.Rate);
            int midLater = (to[later - 1] + from[later]) / 2, midEarlier = earlier < 0 ? 0 : (to[earlier - 1] + from[earlier]) / 2;
            float[] z = new float[n + addLater + addEarlier];
            Array.Copy(y, 0, z, 0, midEarlier);
            Array.Copy(y, midEarlier, z, midEarlier + addEarlier, midLater - midEarlier);
            Array.Copy(y, midLater, z, midLater + addEarlier + addLater, n - midLater);
            return z;
        }

        private static float At(float[] a, float frame)
        {
            int i = (int)frame;
            if (i >= a.Length - 1) return a[a.Length - 1];
            return Mathf.Lerp(a[i], a[i + 1], frame - i);
        }

        // how many samples of that hum a sound from i0 to i1 takes
        private static float Need(Hum h, Voice v, float[] ff, int i0, int i1, float stepS, float speed, float lo, float hi)
        {
            float k = speed * Mathf.Pow(v.Hz / h.Hz, Together), sum = 0f;
            int f0 = (int)(i0 / stepS), f1 = Mathf.Min(ff.Length - 1, (int)(i1 / stepS));
            for (int f = f0; f <= f1; f++) sum += Mathf.Clamp(k * ff[f], lo, hi);
            return sum * stepS * (i1 - i0) / Mathf.Max(1f, (f1 - f0 + 1) * stepS); // (by the frames' own values: a frame stands for stepS samples)
        }
        // ---------------------------------------------------------------- end of the line's rules
    }
}
#endif

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
    //   the hums     where each file of a voice's wordless sounds (hm, hm_ask, uhu, em, and the open «о» — the files the voice has anyway, of
    //                every timbre) holds a stretch of steady voice, and its pitch; only those within the voice's own pitch (the tool's FIT).
    // A line (Render): its sentences and the parts between its commas, each said with a phrase of the manner that has about as many
    // syllables and the same kind of end. The phrase gives the part its length, its loudness and the drift of its pitch; within that time
    // the person says «words» that are no words (Sounds_) — a word is two or three of its own hums run together, each a stretch of a
    // recorded hum played a little faster or slower (never far: a hum far from its own speed is no longer that voice), and after a word
    // there is silence till the next. A word has a melody of its own by what its part is: a statement's last word falls, a question's
    // climbs, a call begins high, one that trails off sinks a little, one before a comma hangs for more.
    // The rules of the ear:
    //   · nothing filtered, nothing repeated or stretched: the hum as recorded («зроби чистий звук», «задвоєння»)
    //   · words, not a row of sounds (the user 2026-11-23, on the page of trials: a hum a syllable was «краще, але надто часто»; fewer and
    //     fewer were each better, and of all he took «слова-вигуки, рідко»: «повинні бути вигуки, схожі на слова, а не самі слова»).
    //     One word for every WordEvery syllables of the part (one at least), and now and then a part of a line that is not its first
    //     says none (Skip: «рідші вигуки в загальному на 30%»)
    //   · no sound of real speech: syllables cut from a reading, or a reading turned back to front, are words to the ear («я просто чую слова»)
    //   · the hums of a word differ (never the same recording twice running), a recording the less likely the more of it the line has
    //     had; the ones known at once (uhu, em) rarely twice a line («6 разів угу»); the open «о» only as a word's first sound, once a line
    //   · some phrases slow (Calm): their words longer, the silence between them and after them longer
    //   · every hum comes in softly and dies away; inside a word the next begins before the last has died («після одного звуку різко йде другий»)
    // The same on every game: the choices are numbers of the line's own words and the person's seed, nothing is sent.
    // When: NpcSpeech.Said, for every line a person says near the eye (over its head and in your talk) — quieter between people than to you,
    // and in a scene quieter than a plain talk by SceneQuiet, the hero's lines there too (the user 2026-11-23: «під час катсцен … тихіше на 40%»: VolOf);
    // and the hero's own answers in a talk (NpcTalk.Pick: his «hero» timbre, NpcTimbre.OfHero; heard on his game only, as his window is his);
    // not while the voice package is on (RavenVoice.Available: his rule, kept though the package voices only the ravens yet), nor a line it really speaks (Voiced). While a line is hummed
    // the speaker's own short sounds (its breath, its «хм» written in the words, its sigh) are not played over it: NpcSpeech asks Say's answer.
    // Cost: once a voice — its files' samples copied (a few MB, the last VoicesKept kept); once a line — one pass over its samples (a line
    // of 8 s: some 350 000, and a word's hums are only a part of them) and one clip made, let go when it has played.
    internal static class NpcBabble
    {
        // ---------------------------------------------------------------- numbers (raven npc tune NpcBabble.<name>)
        internal static bool On = true, Trace = false;
        internal static float Vol = 0.38f, VolToYou = 0.55f, Range = 9f;   // between people · in your talk · m
        internal static float SceneQuiet = 0.6f;                           // a line said in a scene: this share of a plain talk's loudness (VolToYou)
        internal static float Calm = 0.5f, SlowBy = 1.35f;                 // the share of slow phrases · how much longer one is
        internal static float Loud = 0.12f;                                // the line's own level before Vol
        internal static float WordEvery = 11f, Third = 0.45f, Skip = 0.45f; // syllables of a part to a «word» · how often a word has a third hum · how often a part that is not the line's first says no word
                                                                           // (Skip 0.45: the user took the trial with every part saying its word and asked for 30% fewer — measured by the tool's copy, 22 voices: 1.22 → 0.84 hums a second)
        internal static float GapLeast = 0.5f, GapSpan = 0.4f;             // s of silence between two words of a part: the least · and up to this more
        internal static float HumLeast = 0.17f, HumMost = 0.26f, LastLonger = 1.3f; // s a hum of a word lasts · a part's last hum drawn out
        internal static float Run = 0.03f, FadeIn = 0.035f, FadeOut = 0.05f, FadeLast = 0.09f; // s: a word's next hum begins this long before the last ends · a hum comes in · dies away · a word's last one
        internal static float SpeedLow = 0.78f, SpeedHigh = 1.28f;         // a hum never played beyond these of its own speed
        internal static float OverBy = 0.6f, MostTempo = 1.5f;             // s a hummed line may outlast its shown one · quickened at most by this to fit
        internal static int VoicesKept = 8;
        // (not a chuckle, not a sigh: the user 2026-11-23 — a piece of laughter in a plain line was «рандомний звук … який взагалі не йде голосом
        // цього нпс»; measured that day, one stood in 33–94% of a voice's lines. What a person feels stays its short sounds: NpcSpeech)
        // The open «о» is among them since the words (2026-11-23): the one sound of a voice made with its mouth open — a word's first sound only
        private static readonly string[] Sounds = { "speech_hm", "speech_hm_ask", "speech_uhu", "speech_em", "speech_oh" };
        private const string Open = "oh";
        private static readonly string[] Level = { "hm", "em", "uhu" }; // the kinds a voice's middle pitch is taken from: said level, not asked or called out — the tool's FIT is measured against the same (MIDDLE)
        private static readonly string[] Rare = { "uhu", "em" };
        private const string MannerWord = "@manner", StepWord = "@mannerstep", HumWord = "@hum";
        private const string Vowels = "аеєиіїоуюяaeiouyАЕЄИІЇОУЮЯAEIOUY";

        private sealed class Phrase { public int Hills; public bool Rising; public float[] F, E; }
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
                    if (Array.IndexOf(Level, s.Kind) >= 0) hz.Add(s.Hz); // (its middle pitch is where it talks: a «хм?» is asked higher, an «о» called out higher)
                    v.Rate = c.frequency;
                }
            }
            if (v.Hums.Count < 4)
            {
                AudioBank.LogOnce("babble few " + key, "the people's wordless talk: the voice «" + (key.Length > 0 ? key : "own") + "» has " + v.Hums.Count + " stretch(es) of hum (node tools/sound.js babble): too few to talk with, it keeps its short sounds");
                v.Hums.Clear();
            }
            else if (hz.Count == 0) v.Hums.Clear(); // (nothing said level: no pitch to talk at)
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

        // how loud a line is hummed: in a scene (the people's lines and the hero's alike) quieter than a plain talk, else to you or between people
        internal static float VolOf(bool toYou, bool scene) { return scene ? VolToYou * SceneQuiet : toYou ? VolToYou : Vol; }

        // the seconds the line is hummed for, from now (0: not hummed); hummed, the speaker's own short sounds keep out of it. clip: the name its
        // sound goes by in the sound core — one that may be cut short later says its own (Hush: the hero's answer, NpcTalk)
        internal static float Say(NpcTimbre.Card card, Transform head, string text, uint seed, float seconds, float vol, string clip = "babble")
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
            bool heard = Audio.PlayClip(made, null, head.position, new SoundAsk { Volume = vol, Range = Range, Follow = head, Owner = SpeechOwner });
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
            Phrase p = new Phrase { Hills = Mathf.Max(1, Errands.I(w[1], 1)), Rising = w[2] == "1", F = new float[n], E = new float[n] };
            int last = -1;
            for (int i = 0; i < n; i++)
            {
                int a, b;
                if (!int.TryParse(h.Substring(i * 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a) || !int.TryParse(h.Substring(i * 4 + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return null;
                p.E[i] = b / 255f * 1.6f;
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
            Said said = new Said();
            int total = 0, nth = 0;
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
                    float[] ff = new float[frames], ee = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float src = i * (p.F.Length - 1) / (float)(frames - 1);
                        int a = (int)src, b = Mathf.Min(p.F.Length - 1, a + 1); float t = src - a;
                        ff[i] = Mathf.Lerp(p.F[a], p.F[b], t); ee[i] = Mathf.Lerp(p.E[a], p.E[b], t);
                    }
                    if (wantRise && !p.Rising) { int k = (int)(frames * 0.75f); for (int i = k; i < frames; i++) ff[i] *= Mathf.Lerp(1f, 1.12f, (i - k) / (float)Mathf.Max(1, frames - 1 - k)); } // (a question said with a statement's phrase: its end lifted)
                    if (cl.Mark == '!' && last) for (int i = 0; i < frames; i++) { ff[i] *= 1.03f; ee[i] *= 1.15f; }
                    bool silent = nth > 0 && H(seed, cl.Key, ci, 0, 8) % 100u < (uint)Mathf.RoundToInt(Skip * 100f); // (a part that is not the line's first, now and then: its time passes in silence)
                    float[] y = silent ? new float[(int)(frames * _step * v.Rate)] : Sounds_(ff, ee, frames, v, pitch, seed, cl.Key, ci, m, last ? cl.Mark : ',', slow, said);
                    parts.Add(y); total += y.Length; nth++;
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

        // what a line has said so far: where each hum was left off, how often each was used, the last one
        private sealed class Said
        {
            public readonly Dictionary<int, float> At = new Dictionary<int, float>();
            public readonly Dictionary<int, int> Times = new Dictionary<int, int>();
            public readonly List<string> Kinds = new List<string>();
            public int Prev = -1;
            public string LastKind = "";
        }

        // the «words» of one phrase (m syllables of the text, its part ending in mark): a word for every WordEvery syllables, each two or
        // three of the person's own hums run together, silence between the words through the phrase's own time
        private static float[] Sounds_(float[] ff, float[] ee, int frames, Voice v, float pitch, uint seed, string key, int ci, int m, char mark, bool slow, Said said)
        {
            int rate = v.Rate, words = Mathf.Max(1, Mathf.RoundToInt(m / WordEvery)), hums = 0;
            int[] groups = new int[words];
            for (int b = 0; b < words; b++) { groups[b] = Mathf.Min(Mathf.Max(1, m), 2 + (H(seed, key, ci, b, 36) % 100u < (uint)Mathf.RoundToInt(Third * 100f) ? 1 : 0)); hums += groups[b]; }
            float D = frames * _step, gapSum = 0f;
            float[] gaps = new float[words - 1];
            for (int g = 0; g < gaps.Length; g++) { gaps[g] = (GapLeast + (H(seed, key, ci, g, 32) % 1000u) / 1000f * GapSpan) * (slow ? 1.5f : 1f); gapSum += gaps[g]; }
            float syl = Mathf.Min(HumMost * (slow ? 1.25f : 1f), Mathf.Max(HumLeast, (D - gapSum) / hums));
            float[] len = new float[hums];
            float whole = gapSum + 0.05f;
            for (int gi = 0, at = 0; gi < words; gi++)
                for (int s = 0; s < groups[gi]; s++, at++)
                {
                    len[at] = syl * (0.85f + (H(seed, key, ci * 64 + gi, s, 33) % 30u) / 100f);
                    if (gi == words - 1 && s == groups[gi] - 1) len[at] *= LastLonger; // (a phrase's last hum is drawn out)
                    whole += len[at];
                }
            if (D > whole) // (the words stand through the phrase's own time: the silence between them is what is left of it)
            {
                for (int g = 0; g < gaps.Length; g++) gaps[g] += (D - whole) / gaps.Length;
                whole = D;
            }
            int n = (int)(whole * rate);
            float[] y = new float[n];
            float lo = SpeedLow * pitch, hi = SpeedHigh * pitch, top = 0f, t = 0f;
            for (int i = 0; i < frames; i++) top = Mathf.Max(top, ee[i]);
            float[] mel = null; // (a hum's melody, sample by sample: one array for the phrase, grown as needed)
            for (int gi = 0, idx = 0; gi < words; gi++)
            {
                int g = groups[gi];
                if (gi > 0) t += gaps[gi - 1];
                bool fin = gi == words - 1;
                // the word's own melody, by what its part is: it falls, asks, calls, sinks, or hangs for more
                float from = !fin ? 1.03f : mark == '.' ? 1.05f : mark == '?' ? 0.96f : mark == '!' ? 1.14f : mark == '~' ? 1.0f : 0.98f;
                float to = !fin ? 0.97f : mark == '.' ? 0.88f : mark == '?' ? 1.20f : mark == '!' ? 0.94f : mark == '~' ? 0.93f : 1.06f;
                float own = 1f + ((int)(H(seed, key, ci, gi, 37) % 60u) - 30) / 1000f;
                for (int s = 0; s < g; s++, idx++)
                {
                    float d = len[idx], t0 = t;
                    int cnt = (int)((d + (s < g - 1 ? Run : 0f)) * rate), i0 = (int)(t0 * rate);
                    t += d;
                    if (cnt < 8) continue;
                    if (mel == null || mel.Length < cnt) mel = new float[cnt];
                    float sum = 0f, least = float.MaxValue, most = 0f;
                    for (int i = 0; i < cnt; i++)
                    {
                        float f = Mathf.Sqrt(At(ff, (i / (float)rate + t0) / whole * (frames - 1))) * (from + (to - from) * ((s + i / (float)cnt) / g)) * own;
                        mel[i] = f; sum += f;
                        if (f < least) least = f;
                        if (f > most) most = f;
                    }
                    float e = Mathf.Max(At(ee, (t0 + d / 2f) / whole * (frames - 1)), 0.55f * top) * (g > 1 ? (s == 0 ? 1.18f : 0.9f) : 1f);
                    // the hum: one the line has used least, not the one just heard, with room left in it; the open «о» only to begin a word, once a line
                    int best = -1; float bestScore = float.MaxValue;
                    for (int r = 0; r < v.Hums.Count; r++)
                    {
                        Hum hm = v.Hums[r];
                        float need = Need(mel, cnt, pitch * v.Hz / hm.Hz, sum, least, most, lo, hi);
                        if (hm.X.Length < need) continue;
                        int times; float cur;
                        said.Times.TryGetValue(r, out times); said.At.TryGetValue(r, out cur);
                        float score = 3f * times + (r == said.Prev ? 6f : 0f) + (hm.Kind == said.LastKind ? 2f : 0f) + (H(seed, key, ci * 64 + idx, r, 34) % 1000u) / 1000f;
                        if (Array.IndexOf(Rare, hm.Kind) >= 0 && said.Kinds.Contains(hm.Kind)) score += 4f;
                        if (cur + need > hm.X.Length) score += 5f;
                        if (hm.Kind == Open && (s != 0 || said.Kinds.Contains(Open))) score += 100f;
                        if (score < bestScore) { bestScore = score; best = r; }
                    }
                    if (best < 0) // (no hum so long — a high-pitched person's drawn-out last one: its longest hum, and the sound as long as that allows)
                    {
                        for (int r = 0; r < v.Hums.Count; r++) if (best < 0 || v.Hums[r].X.Length > v.Hums[best].X.Length) best = r;
                        cnt = (int)(cnt * (v.Hums[best].X.Length - 16f) / Need(mel, cnt, pitch * v.Hz / v.Hums[best].Hz, sum, least, most, lo, hi));
                        if (cnt < 8) continue;
                        sum = 0f; least = float.MaxValue; most = 0f;
                        for (int i = 0; i < cnt; i++) { sum += mel[i]; if (mel[i] < least) least = mel[i]; if (mel[i] > most) most = mel[i]; }
                        said.At.Remove(best);
                    }
                    Hum h = v.Hums[best];
                    float k = pitch * v.Hz / h.Hz, room = Need(mel, cnt, k, sum, least, most, lo, hi), pos;
                    said.At.TryGetValue(best, out pos);
                    if (pos + room > h.X.Length) { int free = (int)(h.X.Length - room); pos = free > 0 ? H(seed, key, ci * 64 + idx, 0, 35) % (uint)(free + 1) : 0f; } // (no room left after where it was left off: from a place of its own)
                    int fadeIn = Mathf.Min(cnt / 3, (int)(FadeIn * rate)), fadeOut = Mathf.Min(cnt / 2, (int)((s < g - 1 ? FadeOut : FadeLast) * rate));
                    for (int i = 0; i < cnt && i0 + i < n; i++)
                    {
                        pos += Mathf.Clamp(k * mel[i], lo, hi);
                        int a = (int)pos;
                        if (a >= h.X.Length - 1) break;
                        float x = Mathf.Lerp(h.X[a], h.X[a + 1], pos - a) * e;
                        if (i < fadeIn) x *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / fadeIn);
                        else if (i >= cnt - fadeOut) x *= 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (i - (cnt - fadeOut)) / fadeOut);
                        y[i0 + i] += x;
                    }
                    int was;
                    said.Times.TryGetValue(best, out was);
                    said.At[best] = pos; said.Times[best] = was + 1; said.Prev = best; said.LastKind = h.Kind; said.Kinds.Add(h.Kind);
                }
            }
            return y;
        }

        private static float At(float[] a, float frame)
        {
            int i = (int)frame;
            if (i >= a.Length - 1) return a[a.Length - 1];
            return Mathf.Lerp(a[i], a[i + 1], frame - i);
        }

        // how many samples of a hum a sound of that melody takes at speed k (and eight to spare): the sum of its steps, each within the speed's limits
        private static float Need(float[] mel, int cnt, float k, float sum, float least, float most, float lo, float hi)
        {
            if (k * least >= lo && k * most <= hi) return k * sum + 8f; // (the usual one: no step at a limit)
            float s = 0f;
            for (int i = 0; i < cnt; i++) s += Mathf.Clamp(k * mel[i], lo, hi);
            return s + 8f;
        }
        // ---------------------------------------------------------------- end of the line's rules
    }
}
#endif

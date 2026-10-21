using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace HuginWhispers
{
    // The ravens speak aloud. A small program next to the mod (voice/RavenVoice.exe, the HolosTTS model with our rhythm
    // engine) turns a line into sound in a separate process, so the model never takes the game's memory or frame time.
    // The card is shown together with the sound: the line waits for its voice (about a second), and if the voice is late
    // the card goes out on its own. Each raven speaks with the voice of its character (Norov).
    internal static class RavenVoice
    {
        private static ConfigEntry<bool> _on;
        private static ConfigEntry<float> _volume, _maxWait;
        private static ConfigEntry<int> _threads;

        private static Process _proc;
        private static StreamWriter _in; // UTF-8 by hand: .NET Framework writes a child's stdin in the system code page
        private static bool _ready, _dead;
        private static readonly Queue<Msg> Inbox = new Queue<Msg>();
        private static readonly Dictionary<int, Pending> Waiting = new Dictionary<int, Pending>();
        private static int _nextId = 1;
        private static readonly Dictionary<string, float> Spoken = new Dictionary<string, float>(); // talker+line -> when voiced: not again within a minute
        private static VoiceDriver _driver;
        private static float _nextAlive;

        private sealed class Pending
        {
            public GameObject Talker; public bool Munin; public string Text; public float Asked; public Func<float, bool> Show; public bool Shown;
            public AudioClip Clip; public float Queued; // ready and waiting for its turn
            public string Kind; public float Wait; // the voice program's profile (h / m / n); how long the card waits for the sound (0 = Max wait)
        }

        internal static void Init(Plugin p)
        {
            // a package without the voice program keeps these two out of the menu (they would do nothing there)
            const string onDesc = "Hugin and Munin say their lines aloud with their own voices. The line's card appears together with the voice.";
            ConfigDescription volDesc = new ConfigDescription("Loudness of the ravens' voices.", new AcceptableValueRange<float>(0f, 1f));
            bool installed = File.Exists(Path.Combine(Dir, "RavenVoice.exe"));
            _on = installed ? p.Config.Bind("8. Voice", "Ravens speak aloud", true, onDesc) : p.Hide("8. Voice", "Ravens speak aloud", true, onDesc);
            _volume = installed ? p.Config.Bind("8. Voice", "Voice volume", 0.9f, volDesc) : p.Hide("8. Voice", "Voice volume", 0.9f, volDesc);
            _threads = p.Hide("8. Voice", "Voice threads", 0, new ConfigDescription("CPU threads the voice may use (it runs at low priority). 0 = by the computer: half its cores, 2 to 4.", new AcceptableValueRange<int>(0, 8)));
            _maxWait = p.Hide("8. Voice", "Max wait seconds", 4f, "If the voice is not ready this long after the line, the card is shown without it.");
            GameObject go = new GameObject("RavenWatch_Voice"); UnityEngine.Object.DontDestroyOnLoad(go); _driver = go.AddComponent<VoiceDriver>();
        }

        private static int _headless = -1;
        private static bool Headless()
        {
            if (_headless < 0) _headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ? 1 : 0;
            return _headless == 1;
        }
        // measured: 4 threads 0.24 s of work per second of voice, 8 only 13% faster; a 4-core computer keeps 2 for the game
        private static int Threads { get { return _threads.Value > 0 ? _threads.Value : Mathf.Clamp(SystemInfo.processorCount / 2, 2, 4); } }
        // the voice comes as its own package (RavenWatch_Voice: plugins/<its folder>/voice); a voice/ next to the dll wins,
        // then plugins/voice (a hand install), then voice/ in each plugin folder. Looked once.
        private static string _dir;
        private static string Dir
        {
            get
            {
                if (_dir != null) return _dir;
                _dir = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "voice");
                if (File.Exists(Path.Combine(_dir, "RavenVoice.exe"))) return _dir;
                try
                {
                    List<string> places = new List<string>();
                    places.Add(Path.Combine(BepInEx.Paths.PluginPath, "voice"));
                    foreach (string d in Directory.GetDirectories(BepInEx.Paths.PluginPath)) places.Add(Path.Combine(d, "voice"));
                    foreach (string d in places)
                        if (File.Exists(Path.Combine(d, "RavenVoice.exe"))) { _dir = d; break; }
                }
                catch (Exception e) { Plugin.Instance.Log("Voice: could not look for the voice package: " + e.Message); }
                return _dir;
            }
        }
        private static string ConfigDir { get { return Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"); } }
        // the sound files of the lines: in the system temp folder, not in config (mod managers export config with a profile)
        private static string WavDir { get { return Path.Combine(Path.GetTempPath(), "RavenWatch_voice"); } }

        // starts the voice program once (on the first line, or early from the driver); off on servers and when missing
        // the voice speaks one language (voice/language.txt, Ukrainian if missing): it is used only when the ravens'
        // phrases are in that language (an English voice pack will bring its own folder later)
        private static string _voiceLang;
        private static int _exe = -1;
        private static string VoiceLanguage
        {
            get
            {
                if (_voiceLang == null)
                {
                    try { string f = Path.Combine(Dir, "language.txt"); _voiceLang = File.Exists(f) ? File.ReadAllText(f).Trim() : Lang.Source; }
                    catch { _voiceLang = Lang.Source; }
                    if (_voiceLang.Length == 0) _voiceLang = Lang.Source;
                }
                return _voiceLang;
            }
        }

        // the ravens can speak aloud here and now: the voice is installed, switched on, and speaks the phrases' language
        internal static bool Available
        {
            get
            {
                if (_on == null || !_on.Value || _dead || Headless()) return false;
                if (_exe < 0) _exe = File.Exists(Path.Combine(Dir, "RavenVoice.exe")) ? 1 : 0; // looked once: this is read by the book every frame
                if (_exe == 0) return false;
                return string.Equals(VoiceLanguage, Lang.PhraseLanguage, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool Running()
        {
            if (_on == null || !_on.Value || _dead || Headless()) return false;
            if (_proc == null && !Available) return false; // another language: silent, and the program is not started
            if (_proc != null) return !_proc.HasExited || Fail("the voice program stopped");
            string exe = Path.Combine(Dir, "RavenVoice.exe");
            if (!File.Exists(exe)) { _dead = true; Plugin.Instance.Log("Voice: no voice/RavenVoice.exe, the ravens stay silent"); return false; }
            try
            {
                Directory.CreateDirectory(WavDir); Directory.CreateDirectory(ConfigDir);
                ProcessStartInfo si = new ProcessStartInfo(exe,
                    "--data \"" + Dir + "\" --threads " + Threads +
                    " --user \"" + Path.Combine(ConfigDir, "voice_stress.txt") + "\" --unknown \"" + Path.Combine(ConfigDir, "voice_unknown_words.txt") + "\"");
                si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardInput = true; si.RedirectStandardOutput = true;
                si.StandardOutputEncoding = new UTF8Encoding(false);
                _proc = Process.Start(si);
                _in = new StreamWriter(_proc.StandardInput.BaseStream, new UTF8Encoding(false)); _in.AutoFlush = true;
                Thread t = new Thread(ReadLoop); t.IsBackground = true; t.Start(_proc);
                return true;
            }
            catch (Exception e) { return Fail("cannot start the voice program: " + e.Message); }
        }

        private static bool Fail(string why)
        {
            _dead = true; Plugin.Instance.Log("Voice: " + why + ", the ravens stay silent");
            Flush(); return false;
        }

        // nothing more will come from the program: waiting cards go out alone, the sounds already made are freed
        private static void Flush()
        {
            _ready = false;
            foreach (Pending w in Waiting.Values) if (w.Show != null && !w.Shown) { w.Shown = true; w.Show(0f); }
            foreach (Pending t in Turns) { t.Show(0f); if (t.Clip != null) UnityEngine.Object.Destroy(t.Clip); }
            foreach (AudioClip c in Ready.Values) if (c != null) UnityEngine.Object.Destroy(c);
            Turns.Clear(); Waiting.Clear(); Ready.Clear(); ReadyAt.Clear();
            lock (Inbox) Inbox.Clear();
        }

        // an answer of the program; an OK line's sound is read from its file here, off the game's thread
        private sealed class Msg { public string Line; public float[] Samples; public int Rate; }

        private static void ReadLoop(object from)
        {
            Process proc = (Process)from;
            try
            {
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    Msg m = new Msg(); m.Line = line;
                    int id;
                    if (line.StartsWith("OK\t", StringComparison.Ordinal) && int.TryParse(line.Split('\t')[1], out id)) m.Samples = ReadWav(WavOf(id), out m.Rate);
                    lock (Inbox) { if (proc == _proc) Inbox.Enqueue(m); } // (a stopped program's last words are dropped)
                }
            }
            catch { }
        }

        // the sound files go round 16 names: a file is read the moment its OK comes, long before the name comes round again
        private static string WavOf(int id) { return Path.Combine(WavDir, "line" + (id % 16) + ".wav"); }

        // a raven line: true = the voice takes it and will call show(seconds of sound) when the sound is ready;
        // show answers whether the sound is still wanted (false: its card is gone, the raven stays silent)
        // kind: the delivery (null = the raven's own; "n" = plain, the Guide); wait: seconds the sound may take (0 = Max wait)
        internal static bool Say(GameObject talker, bool munin, int voice, string text, Func<float, bool> show, string kind = null, float wait = 0f)
        {
            if (talker == null || string.IsNullOrEmpty(text) || !Running()) return false;
            string plain = Plain(text);
            if (plain.Length < 2) return false;
            foreach (Pending w in Waiting.Values) if (w.Talker == talker && w.Text == plain && !w.Shown) return true; // the same line asked twice
            string key = talker.GetInstanceID() + "|" + plain; float when;
            if (Spoken.TryGetValue(key, out when) && Time.time - when < 60f) return false; // the game repeats a line: card only
            if (Spoken.Count > 200) Spoken.Clear();
            Spoken[key] = Time.time;
            Pending p = new Pending(); p.Talker = talker; p.Munin = munin; p.Text = plain; p.Show = show; p.Kind = kind; p.Wait = wait;
            return Ask(p, munin, voice) >= 0;
        }

        // the program skips a line nobody waits for any more (its card went out alone, its talk was cut short), so a
        // pile of asked lines never holds up the next one
        private static void Forget(int id)
        {
            if (_in == null) return;
            try { _in.WriteLine("DROP\t" + id); } catch { }
        }

        // a player's raven still has something to say: being made, waiting for its turn, or sounding now (a thought
        // asked meanwhile would only wait and then come without its voice). munin: which of the two
        internal static bool Busy(Player owner, out bool munin)
        {
            munin = false;
            if (owner == null || _proc == null) return false;
            GameObject go = owner.gameObject;
            foreach (Pending w in Waiting.Values) if (w.Talker == go && w.Show != null && !w.Shown) { munin = w.Munin; return true; }
            foreach (Pending t in Turns) if (t.Talker == go) { munin = t.Munin; return true; }
            // (a raven of a talk sounds from its model: on the shoulder bone, or just off it in flight)
            foreach (Speaking s in Speakers)
                if (s.At != null && Time.time < s.Until && (s.At.IsChildOf(owner.transform) || (s.At.position - owner.transform.position).sqrMagnitude < 9f)) { munin = s.Munin; return true; }
            return false;
        }

        private static int Ask(Pending p, bool munin, int voice)
        {
            int id = _nextId++;
            p.Asked = Time.time;
            Waiting[id] = p;
            try { _in.WriteLine("SAY\t" + id + "\t" + (p.Kind ?? (munin ? "m" : "h")) + "\t" + voice + "\t" + UnityEngine.Random.Range(1, int.MaxValue) + "\t" + WavOf(id) + "\t" + p.Text); }
            catch (Exception e) { Waiting.Remove(id); Fail("lost the voice program: " + e.Message); return -1; }
            return id;
        }

        // the card text without rich tags and line breaks
        internal static string Plain(string text)
        {
            string t = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
            return System.Text.RegularExpressions.Regex.Replace(t, "[\\r\\n\\t]+", " ").Trim();
        }

        // the visiting (big) Hugin and the Guide: voices.bin after the shoulder ravens' fourteen (voice-proto/scripts/voices_bin.mjs)
        internal const int BigHugin = 14, GuideVoice = 15;
        // where each raven's timbres start in voices.bin (seven characters each): Hugin his own 0, «Зає» 23; Munin Iago 7, «Голум» 16
        private static readonly int[] HuginTimbres = { 0, 23 }, MuninTimbres = { 7, 16 };

        // a big raven's words: the quick remark when it lands, the text on E. Big Munin has no voice of his own yet: silent.
        // The words are on screen already; the sound may come a while later (a long text takes the model a few seconds)
        internal static void SayBig(Raven raven, string text, bool guide)
        {
            if (raven == null || (raven.m_isMunin && !guide)) return;
            Say(raven.gameObject, false, guide ? GuideVoice : BigHugin, text, s => true, guide ? "n" : "h", 15f);
        }

        // a voice per character of Norov, in this order (voices.bin: Hugin's seven, then Munin's seven)
        private static readonly string[] VoiceChars = { "calm", "watchful", "fidget", "wanderer", "bold", "homebody", "devoted" };

        // the voice of a raven: by the character of the player's raven (Norov); without one, steady by name
        internal static int VoiceOf(Player owner, bool munin, GameObject talker)
        {
            Norov.Character c = owner != null ? Norov.OnPlayer(owner, munin) : null;
            int v = c != null ? Array.IndexOf(VoiceChars, c.Id) : -1;
            if (v < 0) v = Mathf.Abs((talker != null ? talker.name : "raven").GetHashCode() + (owner != null ? owner.GetPlayerName().GetHashCode() : 0)) % VoiceChars.Length;
            // each raven has two timbres: one per player, the same for everyone who hears that player's raven
            int[] timbres = munin ? MuninTimbres : HuginTimbres;
            int t = Mathf.Abs(((owner != null ? owner.GetPlayerName() : talker != null ? talker.name : "raven") + (munin ? "|munin voice" : "|hugin voice")).GetHashCode()) % timbres.Length;
            return timbres[t] + v;
        }

        // main thread: answers from the voice program, late lines, the sound itself
        internal static void Tick()
        {
            while (true)
            {
                Msg m;
                lock (Inbox) { if (Inbox.Count == 0) break; m = Inbox.Dequeue(); }
                string line = m.Line;
                string[] f = line.Split('\t');
                if (f[0] == "READY") { _ready = true; Plugin.Instance.Log("Voice ready in " + (f.Length > 1 ? f[1] : "?") + " ms" + (f.Length > 2 ? " (" + f[2] + ")" : "")); continue; }
                if ((f[0] == "OK" || f[0] == "ERR") && f.Length > 1)
                {
                    if (f[0] == "ERR") Plugin.Instance.Log("Voice: " + line);
                    int id; Pending p;
                    if (!int.TryParse(f[1], out id) || !Waiting.TryGetValue(id, out p)) continue; // dropped meanwhile
                    Waiting.Remove(id);
                    if (p.Show != null && p.Shown) continue; // too late: the card went out on its own
                    AudioClip clip = m.Samples != null ? MakeClip(m.Samples, m.Rate) : null;
                    if (p.Show == null) { if (clip != null) { Ready[id] = clip; ReadyAt[id] = Time.time; } continue; } // asked ahead (a talk): kept until the talk takes it
                    p.Shown = true;
                    if (clip == null) { p.Show(0f); continue; }
                    p.Clip = clip; p.Queued = Time.time; Turns.Add(p); // card and voice together, when it is this raven's turn
                }
            }
            // one raven at a time NEAR each other: a card with its voice waits while another raven is speaking within
            // NearRange of it (a while; then the card goes out alone, so a backlog never builds up). Ravens far apart speak
            // at once, and if they then meet mid-line the voices overlap: that is lively, not a fault.
            for (int i = 0; i < Turns.Count; i++)
            {
                Pending t = Turns[i];
                bool near = t.Talker != null && BusyNear(t.Talker.transform.position);
                if (near && Time.time - t.Queued < TurnWait) continue;
                Turns.RemoveAt(i--);
                if (t.Talker == null || near) { t.Show(0f); UnityEngine.Object.Destroy(t.Clip); continue; }
                if (t.Show(t.Clip.length)) Play(t.Talker, t.Clip, t.Munin); else UnityEngine.Object.Destroy(t.Clip);
            }
            // the voice is late (the first line also waits for the model to load): the card goes out without it
            float wait = _ready ? _maxWait.Value : _maxWait.Value + 12f;
            List<int> late = null;
            foreach (KeyValuePair<int, Pending> kv in Waiting)
                if (kv.Value.Show != null && !kv.Value.Shown && Time.time - kv.Value.Asked > (kv.Value.Wait > 0f ? kv.Value.Wait : wait)) { if (late == null) late = new List<int>(); late.Add(kv.Key); }
            // sounds asked ahead that nobody took (the talk was cut short): freed after two minutes
            if (ReadyAt.Count > 0 && Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + 10f; List<int> old = null;
                foreach (KeyValuePair<int, float> kv in ReadyAt) if (Time.time - kv.Value > 120f) { if (old == null) old = new List<int>(); old.Add(kv.Key); }
                if (old != null) foreach (int id in old) Drop(id);
            }
            if (late != null) foreach (int id in late) { Pending p = Waiting[id]; Waiting.Remove(id); Forget(id); p.Shown = true; p.Show(0f); }
            if (_proc != null && !Available) { Stop(); Plugin.Instance.Log("Voice: switched off (or another language)"); } // frees its memory; on again = starts again
            if (_proc != null && Time.time >= _nextAlive) { _nextAlive = Time.time + 1f; if (_proc.HasExited && !_dead) Fail("the voice program stopped"); }
        }

        // ---- a talk of the ravens (TalkBubble): all its lines are asked at once, each sound is taken when its line comes
        private static readonly Dictionary<int, AudioClip> Ready = new Dictionary<int, AudioClip>();
        private static readonly Dictionary<int, float> ReadyAt = new Dictionary<int, float>();
        private static float _nextSweep;
        internal static float MaxWait { get { return _maxWait != null ? _maxWait.Value : 4f; } }

        // ---- turns: one voice at a time
        private static readonly List<Pending> Turns = new List<Pending>();
        internal const float TurnWait = 6f;   // the longest a line waits for the one speaking near it to finish
        internal const float NearRange = 12f; // ravens closer than this take turns; farther apart they may speak at once
        private struct Speaking { public Transform At; public float Until; public bool Munin; }
        private static readonly List<Speaking> Speakers = new List<Speaking>();

        // is a raven speaking near this point right now
        internal static bool BusyNear(Vector3 pos)
        {
            for (int i = Speakers.Count - 1; i >= 0; i--)
            {
                Speaking s = Speakers[i];
                if (s.At == null || Time.time >= s.Until) { Speakers.RemoveAt(i); continue; }
                if ((s.At.position - pos).sqrMagnitude < NearRange * NearRange) return true;
            }
            return false;
        }

        // asks a line's sound ahead; -1 = no voice
        internal static int Ahead(bool munin, int voice, string text)
        {
            if (string.IsNullOrEmpty(text) || !Running()) return -1;
            string plain = Plain(text);
            if (plain.Length < 2) return -1;
            Pending p = new Pending(); p.Text = plain;
            return Ask(p, munin, voice);
        }

        // 1 = ready (the clip is the caller's now), 0 = still being made, -1 = none (failed, or no voice)
        internal static int Take(int id, out AudioClip clip)
        {
            clip = null;
            if (id < 0) return -1;
            if (Ready.TryGetValue(id, out clip)) { Ready.Remove(id); ReadyAt.Remove(id); return 1; }
            return Waiting.ContainsKey(id) && !_dead ? 0 : -1;
        }

        // a sound asked ahead and no longer wanted
        internal static void Drop(int id)
        {
            AudioClip c;
            if (Ready.TryGetValue(id, out c)) { if (c != null) UnityEngine.Object.Destroy(c); Ready.Remove(id); ReadyAt.Remove(id); }
            if (Waiting.Remove(id)) Forget(id);
        }

        internal const float Fade = 40f; // m: a raven's voice is gone by here
        // the curve's time is distance / Fade
        private static readonly AnimationCurve Rolloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(3f / Fade, 1f), new Keyframe(10f / Fade, 0.5f), new Keyframe(20f / Fade, 0.2f),
            new Keyframe(30f / Fade, 0.07f), new Keyframe(1f, 0f));

        internal static void Play(GameObject talker, AudioClip clip, bool munin)
        {
            GameObject go = new GameObject("RavenWatch_VoiceLine");
            go.transform.SetParent(talker.transform, false);
            go.transform.localPosition = Vector3.up * (talker.GetComponent<Player>() != null ? 1.7f : 0.5f); // a shoulder raven: by the head
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip; src.volume = _volume.Value; src.spatialBlend = 1f; src.dopplerLevel = 0f; src.priority = 64; src.spread = 0f;
            // a voice fades like one: full up to 3 m, half at 10, a fifth at 20, gone by 40 (the line keeps playing:
            // come back while it lasts and you hear the rest)
            src.rolloffMode = AudioRolloffMode.Custom; src.minDistance = 1f; src.maxDistance = Fade;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            // dry: the game's sfx group and the world's reverb zones put a strong echo on a voice
            src.reverbZoneMix = 0f; src.bypassReverbZones = true; src.bypassListenerEffects = true;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            go.AddComponent<VoiceMuffle>(); // far away it also goes dull (the highs go first), so the distance is heard
            src.Play();
            Speaking sp; sp.At = go.transform; sp.Until = Time.time + clip.length + 0.25f; sp.Munin = munin; Speakers.Add(sp);
            UnityEngine.Object.Destroy(go, clip.length + 0.3f);
            UnityEngine.Object.Destroy(clip, clip.length + 0.5f); // a clip is not freed with its object
        }

        // 16-bit mono PCM wav from the voice program (reader thread: no Unity here, no log either)
        private static float[] ReadWav(string path, out int rate)
        {
            rate = 0;
            try
            {
                byte[] b = File.ReadAllBytes(path);
                int pos = 12, data = -1, len = 0;
                rate = BitConverter.ToInt32(b, 24);
                while (pos + 8 <= b.Length)
                {
                    string id = Encoding.ASCII.GetString(b, pos, 4); int size = BitConverter.ToInt32(b, pos + 4);
                    if (id == "data") { data = pos + 8; len = Math.Min(size, b.Length - data); break; }
                    pos += 8 + size;
                }
                if (data < 0 || len < 2 || rate <= 0) return null;
                int n = len / 2; float[] s = new float[n];
                for (int i = 0; i < n; i++) s[i] = BitConverter.ToInt16(b, data + i * 2) / 32768f;
                return s;
            }
            catch { return null; }
        }

        private static AudioClip MakeClip(float[] s, int rate)
        {
            // filled through the reader callback: SetData has a Span overload the C# 5 compiler cannot resolve here
            int n = s.Length, at = 0;
            return AudioClip.Create("RavenVoice", n, 1, rate, false, buf => { int k = Math.Min(buf.Length, n - at); Array.Copy(s, at, buf, 0, k); for (int i = k; i < buf.Length; i++) buf[i] = 0f; at += k; });
        }

        // warm the model up as soon as a player is in the world, so the first line does not wait for the load
        internal static void Warm() { if (Player.m_localPlayer != null) Running(); }

        internal static void Stop()
        {
            Process proc = _proc;
            lock (Inbox) _proc = null;
            try { if (proc != null && !proc.HasExited) { _in.Close(); if (!proc.WaitForExit(1500)) proc.Kill(); } } catch { }
            _in = null;
            Flush();
        }
    }

    // the words of a line open one by one along its voice: each word gets a share of the sound by its syllables, plus a
    // pause after a comma or a full stop; the name before «:» is there from the start. The text keeps its full layout
    // (maxVisibleCharacters), so the capsule does not grow while it opens.
    internal sealed class WordReveal : MonoBehaviour
    {
        private TMP_Text _t;
        private int _head;
        private int[] _ends;
        private float[] _at;
        private float _start, _len;

        internal static void Begin(TMP_Text t, float seconds)
        {
            if (t == null || seconds <= 0f) return;
            WordReveal r = t.GetComponent<WordReveal>();
            if (r == null) r = t.gameObject.AddComponent<WordReveal>();
            r.Set(t, seconds);
        }

        private void Set(TMP_Text t, float seconds)
        {
            _t = t; _start = Time.time; _len = seconds;
            t.ForceMeshUpdate();
            TMP_TextInfo info = t.textInfo;
            int n = info.characterCount;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(n);
            for (int i = 0; i < n; i++) sb.Append(info.characterInfo[i].character);
            string s = sb.ToString();
            int colon = s.IndexOf(": ");
            _head = colon >= 0 && colon < 40 ? colon + 2 : 0;
            List<int> ends = new List<int>(); List<float> weight = new List<float>();
            int at = _head;
            while (at < n)
            {
                while (at < n && char.IsWhiteSpace(s[at])) at++;
                if (at >= n) break;
                int e = at, vowels = 0;
                while (e < n && !char.IsWhiteSpace(s[e])) { if ("аеєиіїоуюяАЕЄИІЇОУЮЯaeiouyAEIOUY".IndexOf(s[e]) >= 0) vowels++; e++; }
                char last = s[e - 1];
                float pause = last == ',' ? 1.2f : last == '…' ? 2.4f : ".!?".IndexOf(last) >= 0 ? 2f : last == '—' || last == ':' ? 1.5f : 0f;
                ends.Add(e); weight.Add(Mathf.Max(1, vowels) + pause);
                at = e;
            }
            _ends = ends.ToArray(); _at = new float[_ends.Length];
            float total = 0f; foreach (float w in weight) total += w;
            float acc = 0f, speak = seconds * 0.92f; // the voice ends a little before its sound does
            for (int i = 0; i < _at.Length; i++) { _at[i] = total > 0f ? acc / total * speak : 0f; acc += weight[i]; }
            t.maxVisibleCharacters = _head;
        }

        private void Update()
        {
            if (_t == null) { Destroy(this); return; }
            float e = Time.time - _start;
            int vis = _head;
            for (int i = 0; i < _at.Length; i++) if (e >= _at[i]) vis = _ends[i];
            _t.maxVisibleCharacters = vis;
            if (e > _len) { _t.maxVisibleCharacters = 99999; Destroy(this); }
        }
    }

    // a voice far away sounds dull: its highs are cut more the farther the listener (8 m: clear, 35 m: 2.2 kHz)
    internal sealed class VoiceMuffle : MonoBehaviour
    {
        private static AudioListener _ear;
        private AudioLowPassFilter _lp;
        private void Start() { _lp = GetComponent<AudioLowPassFilter>(); }
        private void Update()
        {
            if (_lp == null) return;
            if (_ear == null || !_ear.isActiveAndEnabled) _ear = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
            if (_ear == null) return;
            float d = Vector3.Distance(_ear.transform.position, transform.position);
            _lp.cutoffFrequency = Mathf.Lerp(22000f, 2200f, Mathf.InverseLerp(8f, 35f, d));
        }
    }

    internal sealed class VoiceDriver : MonoBehaviour
    {
        private bool _warmed;
        private void Update()
        {
            if (!_warmed && Player.m_localPlayer != null) { _warmed = true; RavenVoice.Warm(); }
            RavenVoice.Tick();
        }
        private void OnApplicationQuit() { RavenVoice.Stop(); }
        private void OnDestroy() { RavenVoice.Stop(); }
    }
}

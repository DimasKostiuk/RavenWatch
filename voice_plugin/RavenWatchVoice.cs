using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using HuginWhispers;

namespace RavenWatchVoice
{
    // RavenWatch_Voice: the ravens' voice as a plugin of its own. It starts and runs the voice program next to it
    // (voice/RavenVoice.exe: the HolosTTS model with our rhythm engine, in a separate process, so the model never takes
    // the game's memory or frame time) and connects it to RavenWatch through RavenWatch's voice door (RavenVoiceApi).
    // RavenWatch itself never touches the program: it hands the lines over as text and gets their sound back.
    // Built with STEW_COPY it is the copy carried by the Odins_Stew hand-out zip (tools/voice_plugin_build.ps1 -StewCopy):
    // a GUID of its own, loaded after the store package, and it stays off whenever that package is there — the store's
    // voice and its updates always win.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("ko4erga.ravenwatch", BepInDependency.DependencyFlags.HardDependency)]
#if STEW_COPY
    [BepInDependency(StoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
#endif
    public class VoicePlugin : BaseUnityPlugin
    {
        public const string StoreGuid = "ko4erga.ravenwatch.voice";
#if STEW_COPY
        public const string Guid = "ko4erga.ravenwatch.voice.stew", Name = "RavenWatch_Voice (Odins_Stew copy)";
#else
        public const string Guid = StoreGuid, Name = "RavenWatch_Voice";
#endif
        public const string Version = "1.3.0";

        private Engine _engine;

        private void Awake()
        {
#if STEW_COPY
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(StoreGuid)) { Logger.LogInfo("the RavenWatch_Voice package is installed: this copy stays off"); return; }
#endif
            string dir = Engine.FindDir(Path.GetDirectoryName(typeof(VoicePlugin).Assembly.Location));
            if (dir == null) { Logger.LogWarning("voice/RavenVoice.exe not found next to the plugin: the ravens stay silent"); return; }
            _engine = new Engine(dir, Logger);
            if (!RavenVoiceApi.Connect(_engine, Name + " " + Version)) { Logger.LogWarning("RavenWatch did not take the voice"); _engine = null; return; }
            Logger.LogInfo("voice connected from " + dir);
            int count = Engine.VoiceCount(dir);
            if (count <= 0) { Logger.LogWarning("voice/voices.bin unreadable: the ravens use only the older voices"); return; }
            try { TellVoices(count); Logger.LogInfo(count + " voices"); }
            catch (MissingMethodException) { Logger.LogWarning("this RavenWatch is older than 2.8.0: the ravens use only the older voices"); }
            catch (Exception e) { Logger.LogWarning("could not tell RavenWatch the voices (" + e.Message + "): the ravens use only the older voices"); }
        }

        // in a method of its own: on a RavenWatch without RavenVoiceApi.Voices only this call fails (when it is compiled)
        private static void TellVoices(int count) { RavenVoiceApi.Voices(count); }

        private void OnApplicationQuit() { if (_engine != null) _engine.Stop(); }
        private void OnDestroy() { if (_engine != null) _engine.Stop(); }
    }

    // the voice program: started on RavenWatch's first line, asked over its stdin, its answers read on a thread of
    // their own (a sound file read there too, off the game's thread) and handed to RavenVoiceApi
    internal sealed class Engine : IRavenVoiceEngine
    {
        private readonly string _dir;
        private readonly BepInEx.Logging.ManualLogSource _log;
        private readonly object _lock = new object();
        private Process _proc;
        private StreamWriter _in; // UTF-8 by hand: .NET Framework writes a child's stdin in the system code page
        private string _lang;

        internal Engine(string dir, BepInEx.Logging.ManualLogSource log) { _dir = dir; _log = log; }

        // how many voices voices.bin has (its first int: +n, −n, or −(100000 + n) — see voicehost Synth.cs); 0: unreadable
        internal static int VoiceCount(string dir)
        {
            try
            {
                using (var br = new BinaryReader(File.OpenRead(Path.Combine(dir, "voices.bin"))))
                {
                    int n = br.ReadInt32();
                    return n <= -100000 ? -n - 100000 : Math.Abs(n);
                }
            }
            catch { return 0; }
        }

        // the program next to this plugin (voice/), else a hand install (plugins/voice), else voice/ in another plugin folder
        internal static string FindDir(string here)
        {
            List<string> places = new List<string>();
            places.Add(Path.Combine(here, "voice"));
            try
            {
                places.Add(Path.Combine(Paths.PluginPath, "voice"));
                foreach (string d in Directory.GetDirectories(Paths.PluginPath)) places.Add(Path.Combine(d, "voice"));
            }
            catch { }
            foreach (string d in places) if (File.Exists(Path.Combine(d, "RavenVoice.exe"))) return d;
            return null;
        }

        private static string ConfigDir { get { return Path.Combine(Paths.ConfigPath, "RavenWatch"); } }
        // the sound files of the lines: in the system temp folder, not in config (mod managers export config with a profile)
        private static string WavDir { get { return Path.Combine(Path.GetTempPath(), "RavenWatch_voice"); } }
        // the sound files go round 16 names: a file is read the moment its OK comes, long before the name comes round again
        private static string WavOf(int id) { return Path.Combine(WavDir, "line" + (id % 16) + ".wav"); }

        // the voice speaks one language (voice/language.txt; Ukrainian if missing)
        public string Language
        {
            get
            {
                if (_lang == null)
                {
                    try { string f = Path.Combine(_dir, "language.txt"); _lang = File.Exists(f) ? File.ReadAllText(f).Trim() : ""; }
                    catch { _lang = ""; }
                    if (_lang.Length == 0) _lang = "Ukrainian";
                }
                return _lang;
            }
        }

        public bool Running { get { Process p = _proc; return p != null && !p.HasExited; } }

        public bool Start(int threads)
        {
            if (Running) return true;
            string exe = Path.Combine(_dir, "RavenVoice.exe");
            if (!File.Exists(exe)) { RavenVoiceApi.Lost("no voice/RavenVoice.exe"); return false; }
            try
            {
                Directory.CreateDirectory(WavDir); Directory.CreateDirectory(ConfigDir);
                ProcessStartInfo si = new ProcessStartInfo(exe,
                    "--data \"" + _dir + "\" --threads " + threads +
                    " --user \"" + Path.Combine(ConfigDir, "voice_stress.txt") + "\" --unknown \"" + Path.Combine(ConfigDir, "voice_unknown_words.txt") + "\"");
                si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardInput = true; si.RedirectStandardOutput = true;
                si.StandardOutputEncoding = new UTF8Encoding(false);
                Process p = Process.Start(si);
                StreamWriter w = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)); w.AutoFlush = true;
                lock (_lock) { _proc = p; _in = w; }
                Thread t = new Thread(ReadLoop); t.IsBackground = true; t.Start(p);
                return true;
            }
            catch (Exception e) { _log.LogWarning("cannot start the voice program: " + e.Message); return false; }
        }

        public bool Say(int id, string kind, int voice, int seed, string text)
        {
            StreamWriter w = _in;
            if (w == null || !Running) return false;
            try { lock (_lock) w.WriteLine("SAY\t" + id + "\t" + kind + "\t" + voice + "\t" + seed + "\t" + WavOf(id) + "\t" + text.Replace('\t', ' ').Replace('\n', ' ')); return true; }
            catch (Exception e) { _log.LogWarning("lost the voice program: " + e.Message); return false; }
        }

        public void Drop(int id)
        {
            StreamWriter w = _in;
            if (w == null) return;
            try { lock (_lock) w.WriteLine("DROP\t" + id); } catch { }
        }

        public void Stop()
        {
            Process p; StreamWriter w;
            lock (_lock) { p = _proc; w = _in; _proc = null; _in = null; }
            try { if (p != null && !p.HasExited) { if (w != null) w.Close(); if (!p.WaitForExit(1500)) p.Kill(); } } catch { }
        }

        // the program's answers: READY <ms> [<info>], OK <id>, ERR <id> <why>
        private void ReadLoop(object from)
        {
            Process proc = (Process)from;
            try
            {
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    if (proc != _proc) return; // (a stopped program's last words are dropped)
                    string[] f = line.Split('\t');
                    if (f[0] == "READY") { RavenVoiceApi.Ready((f.Length > 1 ? f[1] + " ms" : "") + (f.Length > 2 ? " (" + f[2] + ")" : "")); continue; }
                    int id;
                    if (f.Length < 2 || !int.TryParse(f[1], out id)) continue;
                    if (f[0] == "OK") { int rate; float[] s = ReadWav(WavOf(id), out rate); RavenVoiceApi.Result(id, s, rate); }
                    else if (f[0] == "ERR") { _log.LogInfo("voice: " + line); RavenVoiceApi.Result(id, null, 0); }
                }
                if (proc == _proc) RavenVoiceApi.Lost("the voice program stopped");
            }
            catch { }
        }

        // 16-bit mono PCM wav from the voice program (reader thread: no Unity here)
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
    }
}

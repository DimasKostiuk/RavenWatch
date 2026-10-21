#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace HuginWhispers
{
    // The player animations' black box (for tuning, the local player only): while it records, every frame's state
    // (the motion values, each layer's clips and weights, the body's turn, the bone that moved most) goes into a ring
    // of the last RingSeconds, the changes (a rule taking over, code playing a clip) into a list, and a bone or the
    // body jumping more than a threshold in one frame is marked as a glitch. Saved to config/RavenWatch/rec/
    // panim_<time>.txt when the recording ends (the landing of a flight on Odin's wings starts and ends one by itself)
    // or by hand from the test window; the five newest kept.
    internal static class PAnimLog
    {
        internal static float RingSeconds = 15f;
        internal static float BoneJump = 25f, BodyJump = 15f; // degrees in one frame (at 60 frames a second) counted as a glitch
        private const int MaxEvents = 3000, Keep = 5;

        private static bool _on;
        private static string _what = "";
        private static float _startedAt;
        private static readonly List<KeyValuePair<float, string>> Frames = new List<KeyValuePair<float, string>>();
        private static readonly List<string> Events = new List<string>();
        private static int _glitches;
        internal static string LastFile = "";

        internal static bool On { get { return _on; } }
        internal static string What { get { return _on ? _what : null; } } // whose recording runs now

        internal static void Begin(string what)
        {
            Frames.Clear(); Events.Clear(); _glitches = 0;
            _on = true; _what = what; _startedAt = Time.time;
            Event("початок запису: " + what);
        }

        // ends the recording and writes it (returns the file, or null)
        internal static string End(string why)
        {
            if (!_on) return null;
            Event("кінець запису: " + why);
            _on = false;
            return Save();
        }

        private static string T() { return (Time.time - _startedAt).ToString("0.000", CultureInfo.InvariantCulture); }

        internal static void Event(string text)
        {
            if (!_on) return;
            if (Events.Count >= MaxEvents) Events.RemoveAt(0);
            Events.Add(T() + "  " + text);
        }

        internal static void Glitch(string text) { if (!_on) return; _glitches++; Event("!!! РИВОК: " + text); }

        internal static void Frame(string line)
        {
            if (!_on) return;
            float now = Time.time;
            Frames.Add(new KeyValuePair<float, string>(now, T() + "  " + line));
            int drop = 0;
            while (drop < Frames.Count && now - Frames[drop].Key > RingSeconds) drop++;
            if (drop > 0) Frames.RemoveRange(0, drop);
        }

        private static string Save()
        {
            try
            {
                string dir = Path.Combine(Path.Combine(Paths.ConfigPath, "RavenWatch"), "rec");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "panim_" + DateTime.Now.ToString("HHmmss") + ".txt");
                StringBuilder sb = new StringBuilder();
                sb.Append("# RavenWatch: запис анімацій персонажа (").Append(_what).Append(") ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
                sb.Append("# ривків (кістка > ").Append(BoneJump).Append("° або тіло > ").Append(BodyJump).Append("° за кадр): ").Append(_glitches).Append('\n');
                sb.Append("# кадр: фаза | speed fwd vy turn air height | шари: слот=кліп:вага(<-старий:вага) | тіло pitch/yaw/roll | найбільший рух кістки за кадр | гра: найбільший рух кістки самою грою (під нашим)\n\n");
                sb.Append("== ПОДІЇ ==\n");
                foreach (string e in Events) sb.Append(e).Append('\n');
                sb.Append("\n== КАДРИ (останні ").Append(RingSeconds.ToString("0")).Append(" с) ==\n");
                foreach (KeyValuePair<float, string> f in Frames) sb.Append(f.Value).Append('\n');
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                List<string> old = new List<string>(Directory.GetFiles(dir, "panim_*.txt"));
                old.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                for (int i = Keep; i < old.Count; i++) { try { File.Delete(old[i]); } catch { } }
                LastFile = path;
                if (Plugin.Instance != null) Plugin.Instance.Log("Player animation log saved: " + path + " (" + _glitches + " glitches)");
                return path;
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Player animation log failed: " + e.Message); return null; }
        }

        internal static string N(float f) { return f.ToString("0.#", CultureInfo.InvariantCulture); }
    }
}
#endif

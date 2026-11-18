#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // What the player hears, written down while the studio plays a scene (StudioScene): the sound of the whole day's work of 2026-11-13 —
    // the audio core, people's sounds, a scene's sound pass — could be judged by the user's ear alone; whoever writes a scene's sound from
    // outside the game heard nothing of it, and said «not heard» of every line. A run now leaves the sound itself and its numbers.
    //   Begin(seconds)   a listener's filter (OnAudioFilterRead on the game's own AudioListener: what goes to the speakers, every source and
    //                    the mixer's groups in it) keeps the output as mono 16-bit, up to so many seconds
    //   End(path)        the wav beside the run's report (studio/out/<name>.wav: measured further by node tools/sound.js info <file>), and the
    //                    report's part (Last): the level of each second in dBFS, and every word the audio core said in the run with its
    //                    moment (Audio.Log → Heard: music begun and let go, a bed, the mix, the ear, a sound not found)
    //   Verdict          one line for the run's VERDICT when nothing at all was heard (the game muted, its master volume at zero): such a run
    //                    says nothing of the scene's sound, and must not read as «fine»
    // The moments are the run's own (seconds from Begin, as the report's steps). The filter runs on the audio thread: it only copies
    // samples into a buffer made beforehand; nothing is allocated or written there. It is on the listener only while a run records.
    internal static class AudioTap
    {
        internal static string Last = "", Verdict;
        private const float SilentDb = -70f;
        private static Ear _ear;
        private static float _t0;
        private static int _rate;
        private static readonly List<KeyValuePair<float, string>> Words = new List<KeyValuePair<float, string>>();

        private sealed class Ear : MonoBehaviour
        {
            internal volatile bool On;
            internal short[] Buf;
            internal int N;

            private void OnAudioFilterRead(float[] data, int channels)
            {
                if (!On || Buf == null || channels <= 0) return;
                int frames = data.Length / channels, n = N;
                for (int i = 0; i < frames && n < Buf.Length; i++)
                {
                    float s = 0f;
                    for (int c = 0; c < channels; c++) s += data[i * channels + c];
                    s = s / channels * 32767f;
                    Buf[n++] = (short)(s > 32767f ? 32767f : s < -32768f ? -32768f : s);
                }
                N = n;
            }
        }

        private static void Cut()
        {
            if (_ear != null) { _ear.On = false; UnityEngine.Object.Destroy(_ear); _ear = null; }
        }

        internal static void Begin(float seconds)
        {
            Cut();
            Last = ""; Verdict = null; Words.Clear();
            AudioListener l = AudioMan.instance != null ? AudioMan.instance.GetActiveAudioListener() : null;
            if (l == null) { Last = "sound: not recorded (the game has no listener now)\n"; return; }
            _rate = AudioSettings.outputSampleRate;
            _ear = l.gameObject.AddComponent<Ear>();
            _ear.Buf = new short[(int)(Mathf.Clamp(seconds, 5f, 400f) * _rate)];
            _t0 = Time.time;
            _ear.On = true;
        }

        // a word of the audio core while a run records (Audio.Log)
        internal static void Heard(string s)
        {
            if (_ear != null && _ear.On && Words.Count < 400) Words.Add(new KeyValuePair<float, string>(Time.time - _t0, s));
        }

        // the run is over: the wav written, the report's part made (Last, Verdict)
        internal static void End(string path, string shown)
        {
            Ear e = _ear;
            if (e == null) return;
            e.On = false;
            short[] buf = e.Buf; int n = Mathf.Min(e.N, buf.Length);
            Cut();
            StringBuilder b = new StringBuilder();
            try
            {
                using (BinaryWriter w = new BinaryWriter(System.IO.File.Create(path)))
                {
                    w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                    w.Write(_rate); w.Write(_rate * 2); w.Write((short)2); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                    byte[] bytes = new byte[n * 2];
                    Buffer.BlockCopy(buf, 0, bytes, 0, n * 2);
                    w.Write(bytes);
                }
            }
            catch (Exception ex)
            {
                if (Plugin.Instance != null) Plugin.Instance.Log("Audio: the run's wav «" + path + "» could not be written: " + ex);
                b.Append("sound: its wav could not be written: ").Append(ex.Message).Append('\n'); shown = "not written";
            }
            // the level of each second
            List<float> level = new List<float>();
            float top = -120f;
            for (int at = 0; at < n; at += _rate)
            {
                double sum = 0; int m = Mathf.Min(_rate, n - at);
                for (int i = 0; i < m; i++) { double v = buf[at + i] / 32768.0; sum += v * v; }
                float db = sum > 0 ? 10f * Mathf.Log10((float)(sum / m)) : -120f;
                level.Add(db);
                if (db > top) top = db;
            }
            b.Append("sound (the game's output at the listener: ").Append(shown).Append(", ").Append(_rate).Append(" Hz mono, ").Append((n / (float)_rate).ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" s; the game's master volume ").Append(AudioListener.volume.ToString("0.##", CultureInfo.InvariantCulture)).Append(")\n  level each second, dBFS (.. is under ").Append(SilentDb.ToString("0")).Append("):\n");
            for (int i = 0; i < level.Count; i++)
            {
                if (i % 10 == 0) b.Append(i > 0 ? "\n" : "").Append("  ").Append(i.ToString().PadLeft(4)).Append(" s ");
                b.Append(' ').Append(level[i] < SilentDb ? " .." : Mathf.RoundToInt(level[i]).ToString().PadLeft(3));
            }
            b.Append('\n');
            if (Words.Count > 0)
            {
                b.Append("  the audio core's words in the run:\n");
                foreach (KeyValuePair<float, string> wd in Words) b.Append("  ").Append(wd.Key.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6)).Append(" s  ").Append(wd.Value).Append('\n');
            }
            else b.Append("  the audio core said nothing in the run: the scene asked it for nothing\n");
            if (n > _rate && top < SilentDb) Verdict = "NOTHING WAS HEARD: the game's output stayed under " + SilentDb.ToString("0") + " dB the whole run (the game muted, its master volume at zero, its window in the background with the sound off there?) — this run says nothing of the scene's sound";
            Last = b.ToString();
            Words.Clear();
        }
    }
}
#endif

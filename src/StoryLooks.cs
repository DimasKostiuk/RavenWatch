#if RAVEN_ITEM && RAVEN_STORY
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // The story's ravens' colours are data: assets/story/looks/<id>.txt (StoryFiles), a RavenLooks text, with the story's own lines before it:
    //   default: yes     the look a scene's bird wears unless its step says «look <id>» (one file)
    //   streak: yes      a streak follows it in the air (seen against trees and sky)
    //   dim: 0.45        it is washed by at least this
    // A person's raven names one in its template (raven_look: <id>; game: the game's own colours). Read once, again with the story's files.
    internal static class StoryLooks
    {
        internal const string Kind = "looks";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class Look { public string File = "", Id = "", Text = ""; public bool Streak; public float Dim; }

        private static Dictionary<string, Look> _all;
        private static Look _default;

        internal static void Reload() { _all = null; _default = null; StoryBird.ForgetLooks(); }

        private static void Load()
        {
            _all = new Dictionary<string, Look>();
            foreach (KeyValuePair<string, string> f in StoryFiles.All(Kind, null, false))
            {
                Look l = new Look { File = f.Key.EndsWith(".txt") ? f.Key.Substring(0, f.Key.Length - 4) : f.Key };
                StringBuilder sb = new StringBuilder();
                bool isDefault = false;
                foreach (KeyValuePair<int, string> ln in StoryFiles.Lines(f.Value))
                {
                    string s = ln.Value;
                    if (s.StartsWith("default:")) { isDefault = s.EndsWith("yes"); continue; }
                    if (s.StartsWith("streak:")) { l.Streak = s.EndsWith("yes"); continue; }
                    if (s.StartsWith("dim:")) { float d; if (float.TryParse(s.Substring(4).Trim(), NumberStyles.Float, Inv, out d)) l.Dim = d; continue; }
                    if (s.StartsWith("id:")) l.Id = s.Substring(3).Trim();
                    sb.Append(s).Append('\n');
                }
                l.Text = sb.ToString();
                if (l.Id.Length == 0) { Story.Log("looks: " + f.Key + ": no «id:»"); continue; }
                _all[l.File] = l;
                if (isDefault) _default = l;
            }
            Story.Log("looks: " + _all.Count + " read");
        }

        internal static Look Get(string id)
        {
            if (_all == null) Load();
            Look l;
            return !string.IsNullOrEmpty(id) && _all.TryGetValue(id, out l) ? l : null;
        }

        internal static Look Default { get { if (_all == null) Load(); return _default; } }

        // its text washed towards a dull grey by dim (0..1), under a new id: each colour of its zones, its eyes' too and their strength less
        internal static string Dimmed(Look l, string id, float dim)
        {
            StringBuilder sb = new StringBuilder("id: " + id + "\nname: " + id + "\nfor: any\n");
            foreach (string line in l.Text.Split('\n'))
            {
                string[] w = line.Split(' ');
                if (w[0] == "zone" && w.Length >= 6) sb.Append("zone ").Append(w[1]).Append(' ').Append(Grey(w[2], dim)).Append(' ').Append(Grey(w[3], dim)).Append(' ').Append(w[4]).Append(' ').Append(w[5]).Append('\n');
                else if (w[0] == "eyes" && w.Length >= 3)
                {
                    float power;
                    if (!float.TryParse(w[2], NumberStyles.Float, Inv, out power)) power = 2.5f;
                    sb.Append("eyes ").Append(Grey(w[1], dim * 0.8f)).Append(' ').Append((power * (1f - dim * 0.85f)).ToString("0.##", Inv)).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static string Grey(string hex, float by)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out c)) return hex;
            c = Color.Lerp(c, new Color(0.42f, 0.42f, 0.43f), Mathf.Clamp01(by) * 0.7f);
            return ColorUtility.ToHtmlStringRGB(c);
        }
    }
}
#endif

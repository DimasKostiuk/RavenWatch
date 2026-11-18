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
    //   sparks: 8A5A2B C9A06A   a few sparks of these colours left behind it in the air, none while it sits (the Hall's raven: LocRaven)
    //   dim: 0.45        it is washed by at least this
    //   hat: hnizdo_kapeliukh   a piece of the ravens' gear on its head, worn wherever a bird of this look is drawn (assets/gear: its id;
    //                    the Hall's raven, the user 2026-11-17: «посади йому на голову гніздо-капелюх»; StoryBird.TakeLook)
    //   wear: kihti_komara kistiani_braslety   pieces of the ravens' gear, each on its own slot, several of one slot worn together (Bruni's raven,
    //                    the user 2026-11-18: «на лапи кігті з жал оси та ще браслети — щоб було ясно, що це бойовий птах»; Outfits.Dress «+»)
    //   level: 7         the size of a raven of that trust (Shoulder.SizeFor; past the top — the top): a scene's bird («bird come … look <id>»)
    //   when: <conditions of the world>   with «default: yes»: the default look while they hold, before the one with no «when:» (chapter 1: Eirik's
    //                    raven gave its feather — one colour from then on, in the prologue of those who come later too). Asked as a bird comes
    // A person's raven names one in its template (raven_look: <id>; game: the game's own colours). Read once, again with the story's files.
    internal static class StoryLooks
    {
        internal const string Kind = "looks";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal sealed class Look { public string File = "", Id = "", Text = "", When = ""; public bool Streak; public float Dim; public bool Sparks; public Color SparkA, SparkB; public string Hat = ""; public string[] Wear = new string[0]; public int Level; }

        private static Dictionary<string, Look> _all;
        private static readonly List<Look> _defaults = new List<Look>(); // (those with «when:» first)

        internal static void Reload() { _all = null; _defaults.Clear(); StoryBird.ForgetLooks(); }

        private static void Load()
        {
            _all = new Dictionary<string, Look>();
            _defaults.Clear();
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
                    if (s.StartsWith("sparks:")) { string[] c = s.Substring(7).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); l.Sparks = c.Length > 0 && ColorUtility.TryParseHtmlString("#" + c[0].TrimStart('#'), out l.SparkA); if (!(c.Length > 1 && ColorUtility.TryParseHtmlString("#" + c[1].TrimStart('#'), out l.SparkB))) l.SparkB = l.SparkA; if (!l.Sparks) Story.Log("looks: " + f.Key + ": sparks: no colour"); continue; }
                    if (s.StartsWith("when:")) { l.When = s.Substring(5).Trim(); continue; }
                    if (s.StartsWith("hat:")) { l.Hat = s.Substring(4).Trim(); continue; }
                    if (s.StartsWith("wear:")) { l.Wear = s.Substring(5).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); continue; }
                    if (s.StartsWith("level:")) { int lv; if (!int.TryParse(s.Substring(6).Trim(), NumberStyles.Integer, Inv, out lv) || lv < 1) Story.Log("looks: " + f.Key + ": level: a whole number from 1"); else l.Level = lv; continue; }
                    if (s.StartsWith("dim:")) { float d; if (float.TryParse(s.Substring(4).Trim(), NumberStyles.Float, Inv, out d)) l.Dim = d; continue; }
                    if (s.StartsWith("id:")) l.Id = s.Substring(3).Trim();
                    sb.Append(s).Append('\n');
                }
                l.Text = sb.ToString();
                if (l.Id.Length == 0) { Story.Log("looks: " + f.Key + ": no «id:»"); continue; }
                _all[l.File] = l;
                if (isDefault) { if (l.When.Length > 0) _defaults.Insert(0, l); else _defaults.Add(l); }
            }
            Story.Log("looks: " + _all.Count + " read");
        }

        internal static Look Get(string id)
        {
            if (_all == null) Load();
            Look l;
            return !string.IsNullOrEmpty(id) && _all.TryGetValue(id, out l) ? l : null;
        }

        // the default look now: the first whose «when:» holds in this world, else the one with none (null: none)
        internal static Look Default
        {
            get
            {
                if (_all == null) Load();
                foreach (Look l in _defaults) if (l.When.Length == 0 || StoryCond.Holds(Player.m_localPlayer, l.When, Story.Log, true)) return l;
                return null;
            }
        }

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

        // a look on the way from one text to another, under a new id: each zone's colours and numbers, the eyes' colour and strength, t of
        // the way (0 from, 1 to). A zone only one of them has keeps its own (StoryBird.Wear: Eirik's raven loses its ash-grey wings)
        internal static string Between(string from, string to, string id, float t)
        {
            Dictionary<string, string[]> other = new Dictionary<string, string[]>();
            foreach (string line in to.Split('\n')) { string[] w = line.Trim().Split(' '); if (w[0] == "zone" && w.Length >= 6) other["zone " + w[1]] = w; else if (w[0] == "eyes" && w.Length >= 3) other["eyes"] = w; }
            StringBuilder sb = new StringBuilder("id: " + id + "\nname: " + id + "\nfor: any\n");
            foreach (string line in from.Split('\n'))
            {
                string[] w = line.Trim().Split(' '), b;
                if (w[0] == "zone" && w.Length >= 6)
                {
                    if (!other.TryGetValue("zone " + w[1], out b)) b = w;
                    sb.Append("zone ").Append(w[1]).Append(' ').Append(Mix(w[2], b[2], t)).Append(' ').Append(Mix(w[3], b[3], t)).Append(' ').Append(Num(w[4], b[4], t)).Append(' ').Append(Num(w[5], b[5], t)).Append('\n');
                }
                else if (w[0] == "eyes" && w.Length >= 3)
                {
                    if (!other.TryGetValue("eyes", out b)) b = w;
                    sb.Append("eyes ").Append(Mix(w[1], b[1], t)).Append(' ').Append(Num(w[2], b[2], t)).Append('\n');
                }
                else if (w[0].Length > 0 && w[0] != "id:" && w[0] != "name:" && w[0] != "for:") sb.Append(line.Trim()).Append('\n'); // (what is not blended — glow, sheen… — as the start's, till the end look)
            }
            return sb.ToString();
        }
        private static string Mix(string a, string b, float t)
        {
            Color x, y;
            if (!ColorUtility.TryParseHtmlString("#" + a, out x) || !ColorUtility.TryParseHtmlString("#" + b, out y)) return t < 0.5f ? a : b;
            return ColorUtility.ToHtmlStringRGB(Color.Lerp(x, y, t));
        }
        private static string Num(string a, string b, float t)
        {
            float x, y;
            if (!float.TryParse(a, NumberStyles.Float, Inv, out x) || !float.TryParse(b, NumberStyles.Float, Inv, out y)) return t < 0.5f ? a : b;
            return Mathf.Lerp(x, y, t).ToString("0.##", Inv);
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

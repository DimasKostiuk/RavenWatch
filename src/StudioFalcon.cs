#if RAVEN_ITEM && RAVEN_STORY
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Студія» on the sisters' falcon, wherever it is and wherever you look (the user 2026-11-12: «ти мав це бачити» — I was
    // blind to it: its words went to your console only, and the raven's cameras stand half a metre to two from a small bird):
    //   falcon <name> [frames=1] [fps=4] [views=side,front,top,back]
    // The nearest falcon to you (Falcon) — from
    // cameras framed on its size (its wingspan fills a picture), turned by ITS way: side = its right, front = its face, back,
    // top (its way up the picture), three = front, right and above. One sheet: a row a moment, the views side by side; the
    // report: what it was doing at each moment.
    internal static partial class Studio
    {
        private sealed class Winged { public Vector3 Mid, Fwd; public float Span; public string Said; }

        // the nearest falcon now: its middle, its way, its span, and a line on it
        private static Winged NearestFalcon(Player p)
        {
            Winged best = null; float bd = float.MaxValue;
            foreach (Falcon x in Falcon.Live)
            {
                if (x == null) continue;
                float d = (x.Middle - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = new Winged { Mid = x.Middle, Fwd = x.Facing, Span = Mathf.Max(x.Span, x.Tall), Said = x.Describe() }; }
            }
            return best;
        }

        private static IEnumerator FalconShots(string name, int frames, float fps, string views)
        {
            frames = Mathf.Clamp(frames, 1, 24);
            Player p = Player.m_localPlayer;
            if (p == null || NearestFalcon(p) == null) { Note(name, "no falcon (raven story falcon)"); yield break; }
            Rig r = Make(views);
            if (r.Cams.Count == 0) { Drop(r); Note(name, "no views named (side, front, back, top, three)"); yield break; }
            Texture2D sheet = new Texture2D(Cell * r.Cams.Count, Cell * frames, TextureFormat.RGB24, false);
            StringBuilder rep = new StringBuilder("falcon from its own cameras; columns: " + string.Join(", ", r.Views.ToArray()) + "; rows: moments\n");
            try
            {
                for (int i = 0; i < frames; i++)
                {
                    if (i > 0) yield return new WaitForSeconds(1f / Mathf.Max(0.5f, fps));
                    yield return new WaitForEndOfFrame();
                    Winged w = p != null ? NearestFalcon(p) : null;
                    if (w == null) { rep.Append(i).Append(": gone\n"); break; }
                    PlaceOnFalcon(r, w);
                    ShootRow(r, sheet, 0, i, frames);
                    rep.Append(i).Append(": ").Append(w.Said).Append('\n');
                }
                Save(name, sheet);
                Note(name, rep.ToString());
            }
            finally { Object.Destroy(sheet); Drop(r); }
        }

        // the cameras round it at a distance its wings fit in (the views' 30° lens: a span s fits at about 2 s), lit from the camera
        private static void PlaceOnFalcon(Rig r, Winged f)
        {
            Vector3 mid = f.Mid, fwd = f.Fwd, right = Vector3.Cross(Vector3.up, fwd);
            float span = Mathf.Max(0.5f, f.Span);
            float d = span * 2.1f;
            r.Lamp.range = d * 2f;
            for (int i = 0; i < r.Cams.Count; i++)
            {
                Vector3 from;
                switch (r.Views[i])
                {
                    case "front": from = mid + fwd * d + Vector3.up * span * 0.15f; break;
                    case "back": from = mid - fwd * d + Vector3.up * span * 0.25f; break;
                    case "top": from = mid + Vector3.up * d; break;
                    case "three": from = mid + (fwd + right).normalized * d + Vector3.up * d * 0.45f; break;
                    default: from = mid + right * d + Vector3.up * span * 0.15f; break; // side: its right
                }
                r.Cams[i].farClipPlane = d * 3f;
                r.Cams[i].transform.position = from;
                r.Cams[i].transform.rotation = Quaternion.LookRotation(mid - from, r.Views[i] == "top" ? fwd : Vector3.up);
            }
            r.Mid = mid;
        }
    }
}
#endif

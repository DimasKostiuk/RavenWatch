#if RAVEN_ITEM
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The colour of one line in a capsule, with no mask: the image is the capsule's own rounded shape (soft-edged, the
    // whole capsule's size), and its mesh is cut here to the line's band (Top..Bottom from the capsule's top) and faded
    // out from the left over Fade. So the colour ends exactly where the capsule does: round where the band meets a round
    // corner, straight between the lines, never past the outline and never stair-stepped (a UI mask cuts hard).
    [RequireComponent(typeof(Graphic))]
    internal sealed class BandFill : BaseMeshEffect
    {
        private float _top, _bottom, _fade = 200f;
        private const int Steps = 12; // slices across the fade: the curve of the fade is kept
        private static readonly List<UIVertex> In = new List<UIVertex>();

        internal void Set(float top, float bottom, float fade)
        {
            if (Mathf.Abs(top - _top) < 0.01f && Mathf.Abs(bottom - _bottom) < 0.01f && Mathf.Abs(fade - _fade) < 0.01f) return;
            _top = top; _bottom = bottom; _fade = fade;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        // how strong the colour is at a distance from the left end (1 at the end, 0 at Fade)
        private float Strength(float fromLeft)
        {
            float t = Mathf.Clamp01(fromLeft / Mathf.Max(1f, _fade));
            return Mathf.Pow(1f - t, 1.8f);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            In.Clear();
            vh.GetUIVertexStream(In);
            vh.Clear();
            Rect r = ((RectTransform)transform).rect;
            float yHi = r.yMax - _top, yLo = r.yMax - _bottom;
            float left = r.xMin, right = r.xMin + Mathf.Max(1f, _fade), step = Mathf.Max(1f, _fade) / Steps;
            // the sliced shape comes as quads, each two triangles (6 vertices): straight-sided, with the picture laid on
            // them evenly, so any part of one can be cut out by plain interpolation
            for (int i = 0; i + 5 < In.Count; i += 6)
            {
                UIVertex lo = In[i], hi = In[i];
                for (int k = 1; k < 6; k++)
                {
                    UIVertex v = In[i + k];
                    if (v.position.x <= lo.position.x && v.position.y <= lo.position.y) lo = v;
                    if (v.position.x >= hi.position.x && v.position.y >= hi.position.y) hi = v;
                }
                float ax = lo.position.x, bx = hi.position.x, ay = lo.position.y, by = hi.position.y;
                if (bx - ax < 0.0001f || by - ay < 0.0001f) continue;
                float x0 = Mathf.Max(ax, left), x1 = Mathf.Min(bx, right), y0 = Mathf.Max(ay, yLo), y1 = Mathf.Min(by, yHi);
                if (x1 <= x0 || y1 <= y0) continue;
                float v0 = Mathf.Lerp(lo.uv0.y, hi.uv0.y, (y0 - ay) / (by - ay)), v1 = Mathf.Lerp(lo.uv0.y, hi.uv0.y, (y1 - ay) / (by - ay));
                float sa = x0;
                while (sa < x1 - 0.0001f)
                {
                    float sb = Mathf.Min(x1, left + (Mathf.Floor((sa - left) / step + 0.0001f) + 1f) * step);
                    float ua = Mathf.Lerp(lo.uv0.x, hi.uv0.x, (sa - ax) / (bx - ax)), ub = Mathf.Lerp(lo.uv0.x, hi.uv0.x, (sb - ax) / (bx - ax));
                    Color32 ca = Fade(lo.color, Strength(sa - left)), cb = Fade(lo.color, Strength(sb - left));
                    int n = vh.currentVertCount;
                    vh.AddVert(Vert(sa, y0, ua, v0, ca));
                    vh.AddVert(Vert(sa, y1, ua, v1, ca));
                    vh.AddVert(Vert(sb, y1, ub, v1, cb));
                    vh.AddVert(Vert(sb, y0, ub, v0, cb));
                    vh.AddTriangle(n, n + 1, n + 2);
                    vh.AddTriangle(n + 2, n + 3, n);
                    sa = sb;
                }
            }
        }

        private static Color32 Fade(Color32 c, float k) { c.a = (byte)Mathf.RoundToInt(c.a * k); return c; }

        private static UIVertex Vert(float x, float y, float u, float v, Color32 c)
        {
            UIVertex o = UIVertex.simpleVert;
            o.position = new Vector3(x, y, 0f);
            o.uv0 = new Vector2(u, v);
            o.color = c;
            return o;
        }
    }
}
#endif

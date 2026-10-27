using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Odin's feather (the errand's item, RavenItem.BuildFeather): one black feather with a gold edge and a gold quill, on a
// soft gold glow, a few sparks round it. Free-standing like the Fate stone's icon, no medallion.
// args: out.png
class FeatherIcon
{
    const float Len = 228f; // the feather's length, quill end to tip

    static void Main(string[] args)
    {
        Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // the glow behind it
            using (GraphicsPath halo = new GraphicsPath())
            {
                halo.AddEllipse(22f, 22f, 212f, 212f);
                using (PathGradientBrush pg = new PathGradientBrush(halo))
                {
                    pg.CenterColor = Color.FromArgb(110, 255, 196, 90);
                    pg.SurroundColors = new Color[] { Color.FromArgb(0, 255, 196, 90) };
                    g.FillPath(pg, halo);
                }
            }

            // the feather lies from the lower left to the upper right: drawn upright, then turned
            g.TranslateTransform(128f, 130f);
            g.RotateTransform(40f);
            float top = -Len / 2f, bottom = Len / 2f, vaneEnd = bottom - 46f; // the vane ends above the bare quill

            using (GraphicsPath vane = Vane(top, vaneEnd))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(-34f, 0f), new PointF(30f, 0f), Color.FromArgb(14, 13, 18), Color.FromArgb(54, 50, 62)))
                    g.FillPath(lg, vane);

                // the barbs: thin lighter lines from the quill out and up, inside the vane only
                Region keep = g.Clip;
                g.SetClip(vane, CombineMode.Intersect);
                using (Pen barb = new Pen(Color.FromArgb(80, 130, 122, 140), 1.5f))
                    for (float y = top + 10f; y < vaneEnd; y += 6.5f)
                    {
                        g.DrawLine(barb, 0f, y, -40f, y - 22f);
                        g.DrawLine(barb, 0f, y, 40f, y - 22f);
                    }
                g.Clip = keep;

                // the gold edge
                using (LinearGradientBrush rim = new LinearGradientBrush(new PointF(0f, top), new PointF(0f, vaneEnd), Color.FromArgb(255, 226, 140), Color.FromArgb(196, 132, 44)))
                using (Pen p = new Pen(rim, 4.6f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, vane); }
            }

            // the quill: gold, thin at the tip, thicker at its bare end, which is cut on a slant
            using (GraphicsPath quill = new GraphicsPath())
            {
                quill.AddPolygon(new PointF[] { new PointF(-0.8f, top + 6f), new PointF(0.8f, top + 6f), new PointF(3.6f, bottom - 5f), new PointF(-1.2f, bottom), new PointF(-3.6f, bottom - 7f) });
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(0f, top), new PointF(0f, bottom), Color.FromArgb(255, 238, 170), Color.FromArgb(214, 150, 54)))
                    g.FillPath(lg, quill);
            }

            // a little down at the vane's foot
            using (Pen down = new Pen(Color.FromArgb(170, 200, 150, 70), 1.8f))
            {
                down.StartCap = LineCap.Round; down.EndCap = LineCap.Round;
                float y0 = vaneEnd + 2f;
                g.DrawBezier(down, 0f, y0, -10f, y0 + 2f, -16f, y0 - 6f, -20f, y0 - 2f);
                g.DrawBezier(down, 0f, y0 + 5f, 9f, y0 + 7f, 15f, y0 - 1f, 19f, y0 + 3f);
                g.DrawBezier(down, 0f, y0 + 9f, -7f, y0 + 12f, -12f, y0 + 6f, -15f, y0 + 11f);
            }
            g.ResetTransform();

            // sparks
            Spark(g, 204f, 62f, 11f);
            Spark(g, 58f, 92f, 8f);
            Spark(g, 186f, 190f, 8f);
            Spark(g, 84f, 206f, 5.5f);
        }
        bmp.Save(args[0], ImageFormat.Png);
    }

    // the vane's outline: the left side wider than the right, a pointed tip, two splits on the left and one on the right
    static GraphicsPath Vane(float top, float end)
    {
        List<PointF> left = Side(top, end, -38f, new float[] { 0.36f, 0.63f });
        List<PointF> right = Side(top, end, 30f, new float[] { 0.5f });
        right.Reverse();
        List<PointF> all = new List<PointF>();
        all.Add(new PointF(0f, top));
        all.AddRange(left);
        all.Add(new PointF(0f, end));
        all.AddRange(right);
        GraphicsPath path = new GraphicsPath();
        path.AddPolygon(all.ToArray());
        return path;
    }

    // one side from the tip down to the foot; width w at its widest (sign = which side); a split at each share of the way
    static List<PointF> Side(float top, float end, float w, float[] splits)
    {
        List<PointF> l = new List<PointF>();
        const int n = 48;
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            float y = top + (end - top) * t;
            // full soon after the tip, narrowing slowly to the foot
            float k = (float)Math.Pow(Math.Sin(Math.PI * Math.Pow(t, 0.62)), 0.75);
            float x = w * k;
            foreach (float s in splits)
            {
                float d = Math.Abs(t - s);
                if (d < 0.035f) x *= 0.62f + d / 0.035f * 0.38f; // a V cut in towards the quill
            }
            l.Add(new PointF(x, y));
        }
        return l;
    }

    // a four-pointed gold spark
    static void Spark(Graphics g, float x, float y, float r)
    {
        using (GraphicsPath s = new GraphicsPath())
        {
            float q = r * 0.22f;
            s.AddPolygon(new PointF[] { new PointF(x, y - r), new PointF(x + q, y - q), new PointF(x + r, y), new PointF(x + q, y + q), new PointF(x, y + r), new PointF(x - q, y + q), new PointF(x - r, y), new PointF(x - q, y - q) });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 255, 222, 140))) g.FillPath(b, s);
        }
    }
}

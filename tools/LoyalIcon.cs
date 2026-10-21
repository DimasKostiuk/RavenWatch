using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// args: frameSource.png out.png munin(0/1)
class LoyalIcon
{
    const float C = 128f, Inner = 86.5f;

    static void Main(string[] args)
    {
        bool munin = args[2] == "1";
        Bitmap src = new Bitmap(args[0]);
        Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        Color centre = munin ? Color.FromArgb(176, 56, 46) : Color.FromArgb(118, 144, 150);
        Color edge = munin ? Color.FromArgb(26, 8, 8) : Color.FromArgb(11, 26, 41);
        Color accent = munin ? Color.FromArgb(255, 110, 80) : Color.FromArgb(130, 205, 255);

        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // the inner disk: lit from the upper left, like the other medallions
            using (GraphicsPath disk = new GraphicsPath())
            {
                disk.AddEllipse(C - Inner - 1, C - Inner - 1, (Inner + 1) * 2, (Inner + 1) * 2);
                using (PathGradientBrush pg = new PathGradientBrush(disk))
                {
                    pg.CenterPoint = new PointF(120f, 108f);
                    pg.CenterColor = centre;
                    pg.SurroundColors = new Color[] { edge };
                    Blend bl = new Blend();
                    bl.Factors = new float[] { 0f, 0.55f, 0.85f, 1f };
                    bl.Positions = new float[] { 0f, 0.3f, 0.6f, 1f };
                    pg.Blend = bl;
                    g.FillPath(pg, disk);
                }
            }

            using (GraphicsPath clip = new GraphicsPath()) { clip.AddEllipse(C - Inner, C - Inner, Inner * 2, Inner * 2); g.SetClip(clip); }
            g.TranslateTransform(128f, 140f); g.ScaleTransform(1.14f, 1.14f); g.TranslateTransform(-128f, -140f);
            float ox = 124f, oy = 116f; // the raven's feet on the top of the stone

            // the mound under the stone
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillEllipse(b, 78f, 184f, 100f, 18f);

            // the gravestone: gold, rounded top, a dark outline
            using (GraphicsPath stone = new GraphicsPath())
            {
                float l = 94f, r = 162f, top = oy - 2f, bottom = 190f, w = r - l;
                stone.AddArc(l, top, w, w * 0.62f, 180f, 180f);
                stone.AddLine(r, top + w * 0.31f, r + 4f, bottom);
                stone.AddLine(r + 4f, bottom, l - 4f, bottom);
                stone.CloseFigure();
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(l, top), new PointF(r, bottom), Color.FromArgb(252, 216, 138), Color.FromArgb(196, 140, 52)))
                    g.FillPath(lg, stone);
                using (Pen p = new Pen(Color.FromArgb(20, 14, 8), 5f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, stone); }
            }

            // ᛉ carved in it: protection
            using (Pen rune = new Pen(Color.FromArgb(110, 70, 24), 6f))
            {
                rune.StartCap = LineCap.Round; rune.EndCap = LineCap.Round;
                float x = 128f, y0 = 182f, y1 = 140f;
                g.DrawLine(rune, x, y0, x, y1);
                g.DrawLine(rune, x, 160f, x - 16f, 143f);
                g.DrawLine(rune, x, 160f, x + 16f, 143f);
            }

            // the raven, perched, facing right
            PointF[] body = Pts(ox, oy, new float[] {
                -44, 10,   -30, 2,   -16, -10,   -4, -21,   8, -29,   16, -39,   25, -41,   31, -37,
                31, -29,   27, -22,   25, -12,   18, -3,   8, 1,   -8, 2,   -24, 6,   -42, 14 });
            using (GraphicsPath raven = new GraphicsPath())
            {
                raven.AddClosedCurve(body, 0.35f);
                // the glow round him in his own colour
                for (int i = 11; i >= 1; i--)
                    using (Pen glow = new Pen(Color.FromArgb(26, accent), i * 2.6f)) { glow.LineJoin = LineJoin.Round; g.DrawPath(glow, raven); }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 24, 32))) g.FillPath(b, raven);
                using (Pen p = new Pen(Color.FromArgb(8, 8, 12), 3f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, raven); }
            }

            // the beak
            PointF[] beak = Pts(ox, oy, new float[] { 29, -38, 46, -32, 29, -29 });
            using (SolidBrush b = new SolidBrush(Color.FromArgb(52, 52, 62))) g.FillPolygon(b, beak);
            using (Pen p = new Pen(Color.FromArgb(8, 8, 12), 2.5f)) { p.LineJoin = LineJoin.Round; g.DrawPolygon(p, beak); }

            // the folded wing: a few lighter feather lines
            using (Pen f = new Pen(Color.FromArgb(70, 72, 88), 2.5f))
            {
                f.StartCap = LineCap.Round; f.EndCap = LineCap.Round;
                g.DrawCurve(f, Pts(ox, oy, new float[] { 12, -20, 0, -12, -16, -4, -34, 6 }), 0.4f);
                g.DrawCurve(f, Pts(ox, oy, new float[] { 10, -10, -2, -4, -18, 1 }), 0.4f);
            }

            // the legs gripping the stone
            using (Pen leg = new Pen(Color.FromArgb(14, 14, 18), 3f))
            {
                leg.StartCap = LineCap.Round; leg.EndCap = LineCap.Round;
                g.DrawLine(leg, ox + 4f, oy, ox + 2f, oy + 5f);
                g.DrawLine(leg, ox + 12f, oy - 1f, ox + 12f, oy + 5f);
            }

            // the eye, glowing
            PointF eye = new PointF(ox + 23f, oy - 34f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(90, accent))) g.FillEllipse(b, eye.X - 5f, eye.Y - 5f, 10f, 10f);
            using (SolidBrush b = new SolidBrush(accent)) g.FillEllipse(b, eye.X - 2.6f, eye.Y - 2.6f, 5.2f, 5.2f);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, eye.X - 1f, eye.Y - 1.8f, 1.8f, 1.8f);
        }

        // the frame (rune ring, gold rims, the raven's orb) from the source medallion
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                double d = Math.Sqrt((x + 0.5 - C) * (x + 0.5 - C) + (y + 0.5 - C) * (y + 0.5 - C));
                if (d < Inner - 1.5) continue;
                Color s = src.GetPixel(x, y);
                if (d >= Inner) { bmp.SetPixel(x, y, s); continue; }
                float t = (float)((d - (Inner - 1.5)) / 1.5); // a soft seam
                Color o = bmp.GetPixel(x, y);
                bmp.SetPixel(x, y, Color.FromArgb(
                    (int)(o.A + (s.A - o.A) * t), (int)(o.R + (s.R - o.R) * t), (int)(o.G + (s.G - o.G) * t), (int)(o.B + (s.B - o.B) * t)));
            }
        bmp.Save(args[1], ImageFormat.Png);
    }

    static PointF[] Pts(float ox, float oy, float[] xy)
    {
        PointF[] p = new PointF[xy.Length / 2];
        for (int i = 0; i < p.Length; i++) p[i] = new PointF(ox + xy[i * 2], oy + xy[i * 2 + 1]);
        return p;
    }
}

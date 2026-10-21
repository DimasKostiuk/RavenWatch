using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// The «Обманка» icon: Hugin's medallion (rim, runes, orb from frameSource) with a new field — a horned brute's head
// below, glaring up; Hugin circling over it on a dashed ring of light; the brute's swing whooshing past behind him.
// args: frameSource.png out.png
class DecoyIcon
{
    const float C = 128f, Inner = 86.5f;
    static readonly Color Accent = Color.FromArgb(130, 205, 255);

    static void Main(string[] args)
    {
        Bitmap src = new Bitmap(args[0]);
        Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // the inner disk, Hugin's blue, lit from the upper left like the other medallions
            using (GraphicsPath disk = new GraphicsPath())
            {
                disk.AddEllipse(C - Inner - 1, C - Inner - 1, (Inner + 1) * 2, (Inner + 1) * 2);
                using (PathGradientBrush pg = new PathGradientBrush(disk))
                {
                    pg.CenterPoint = new PointF(118f, 104f);
                    pg.CenterColor = Color.FromArgb(70, 118, 158);
                    pg.SurroundColors = new Color[] { Color.FromArgb(11, 26, 41) };
                    Blend bl = new Blend();
                    bl.Factors = new float[] { 0f, 0.55f, 0.85f, 1f };
                    bl.Positions = new float[] { 0f, 0.3f, 0.6f, 1f };
                    pg.Blend = bl;
                    g.FillPath(pg, disk);
                }
            }
            using (GraphicsPath clip = new GraphicsPath()) { clip.AddEllipse(C - Inner, C - Inner, Inner * 2, Inner * 2); g.SetClip(clip); }

            // the ring Hugin circles on: the back half first (behind the head)
            RectangleF ring = new RectangleF(58f, 74f, 140f, 50f);
            using (Pen back = new Pen(Color.FromArgb(110, Accent), 3f))
            {
                back.DashPattern = new float[] { 3f, 2.2f };
                g.DrawArc(back, ring, 180f, 180f);
            }

            // the brute's head, below, glaring up: dark, horned, a jagged mouth
            using (GraphicsPath head = new GraphicsPath())
            {
                head.AddClosedCurve(new PointF[] {
                    new PointF(82, 230), new PointF(80, 186), new PointF(92, 158), new PointF(112, 146), new PointF(128, 143),
                    new PointF(144, 146), new PointF(164, 158), new PointF(176, 186), new PointF(174, 230) }, 0.45f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(16, 20, 30))) g.FillPath(b, head);
                using (Pen p = new Pen(Color.FromArgb(70, Accent), 2f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, head); } // a rim of light from above
            }
            // horns, curving up and out
            Horn(g, new PointF[] { new PointF(96, 162), new PointF(78, 146), new PointF(68, 124), new PointF(74, 112) }, new PointF(104, 158));
            Horn(g, new PointF[] { new PointF(160, 162), new PointF(178, 146), new PointF(188, 124), new PointF(182, 112) }, new PointF(152, 158));
            // the eyes: slanted, amber, looking up at the raven
            Eye(g, new PointF(113, 172), -1f);
            Eye(g, new PointF(143, 172), 1f);
            // the jagged mouth
            using (Pen m = new Pen(Color.FromArgb(70, 80, 96), 2.6f))
            {
                m.LineJoin = LineJoin.Round; m.StartCap = LineCap.Round; m.EndCap = LineCap.Round;
                g.DrawLines(m, new PointF[] { new PointF(110, 198), new PointF(117, 193), new PointF(123, 199), new PointF(129, 193), new PointF(135, 199), new PointF(141, 193), new PointF(147, 198) });
            }

            // its swing, whooshing past (behind where Hugin was a moment ago)
            using (GraphicsPath whoosh = new GraphicsPath())
            {
                whoosh.AddArc(44f, 46f, 136f, 136f, 195f, 80f);
                whoosh.Reverse();
                whoosh.AddArc(56f, 60f, 112f, 112f, 200f, 70f);
                whoosh.CloseFigure();
                using (LinearGradientBrush lg = new LinearGradientBrush(new PointF(56, 100), new PointF(120, 56), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(220, 230, 245, 255)))
                    g.FillPath(lg, whoosh);
            }

            // the front half of the ring, over the head, with an arrow showing the way round
            using (Pen front = new Pen(Color.FromArgb(220, Accent), 3.4f))
            {
                front.DashPattern = new float[] { 3f, 2.2f };
                g.DrawArc(front, ring, 0f, 180f);
            }
            using (SolidBrush a = new SolidBrush(Accent))
                g.FillPolygon(a, new PointF[] { new PointF(70f, 113f), new PointF(80f, 118f), new PointF(70f, 124f) });

            // Hugin, wings up, on the ring's right side
            DrawRaven(g, 150f, 112f, 1.45f);

            // a couple of feathers drifting down where it missed him
            Feather(g, 112f, 128f, 30f);
            Feather(g, 96f, 116f, -25f);
            Feather(g, 124f, 138f, 60f);
        }

        // the frame (rune ring, gold rims, the raven's orb) from the source medallion
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++)
            {
                double d = Math.Sqrt((x + 0.5 - C) * (x + 0.5 - C) + (y + 0.5 - C) * (y + 0.5 - C));
                if (d < Inner - 1.5) continue;
                Color s = src.GetPixel(x, y);
                if (d >= Inner) { bmp.SetPixel(x, y, s); continue; }
                float t = (float)((d - (Inner - 1.5)) / 1.5);
                Color o = bmp.GetPixel(x, y);
                bmp.SetPixel(x, y, Color.FromArgb(
                    (int)(o.A + (s.A - o.A) * t), (int)(o.R + (s.R - o.R) * t), (int)(o.G + (s.G - o.G) * t), (int)(o.B + (s.B - o.B) * t)));
            }
        bmp.Save(args[1], ImageFormat.Png);
    }

    static void Horn(Graphics g, PointF[] outer, PointF baseIn)
    {
        using (GraphicsPath h = new GraphicsPath())
        {
            h.AddCurve(outer, 0.5f);
            h.AddLine(outer[outer.Length - 1], new PointF((outer[2].X + baseIn.X) / 2f + (baseIn.X > 128 ? 6f : -6f), outer[2].Y + 14f));
            h.AddLine(h.GetLastPoint(), baseIn);
            h.CloseFigure();
            using (LinearGradientBrush lg = new LinearGradientBrush(baseIn, outer[outer.Length - 1], Color.FromArgb(40, 46, 60), Color.FromArgb(150, 160, 176)))
                g.FillPath(lg, h);
            using (Pen p = new Pen(Color.FromArgb(10, 12, 18), 2.2f)) { p.LineJoin = LineJoin.Round; g.DrawPath(p, h); }
        }
    }

    static void Eye(Graphics g, PointF at, float side)
    {
        using (GraphicsPath e = new GraphicsPath())
        {
            // slanted: the inner corner lower (an angry brow), looking up
            e.AddPolygon(new PointF[] { new PointF(at.X - 9f, at.Y + side * -3f), new PointF(at.X + 9f, at.Y + side * 3f), new PointF(at.X + 4f, at.Y + 5f), new PointF(at.X - 4f, at.Y + 5f) });
            for (int i = 5; i >= 1; i--) using (Pen glow = new Pen(Color.FromArgb(30, 255, 170, 70), i * 2.4f)) g.DrawPath(glow, e);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 186, 90))) g.FillPath(b, e);
        }
        using (SolidBrush pupil = new SolidBrush(Color.FromArgb(40, 16, 6))) g.FillEllipse(pupil, at.X - 2f + side * 1f, at.Y - 1.5f, 4f, 4f);
    }

    // Hugin in flight, seen from the side, flying left, both wings up (scaled from a 0..1 box drawing)
    static void DrawRaven(Graphics g, float ox, float oy, float k)
    {
        Func<float, float, PointF> P = (x, y) => new PointF(ox + x * k, oy + y * k);
        using (GraphicsPath r = new GraphicsPath())
        {
            r.FillMode = FillMode.Winding;
            // body: head on the left, a wedge tail on the right
            r.AddClosedCurve(new PointF[] { P(-20, 2), P(-15, -3), P(-6, -4), P(6, -2), P(16, 0), P(26, -2), P(28, 3), P(18, 6), P(4, 8), P(-10, 7), P(-17, 6) }, 0.35f);
            // the far wing, up and back, its fingers spread
            r.AddClosedCurve(new PointF[] { P(-4, -2), P(-8, -14), P(-6, -26), P(0, -34), P(4, -31), P(3, -24), P(8, -27), P(9, -20), P(13, -21), P(11, -12), P(6, -3) }, 0.3f);
            // the near wing, bigger, raised high
            r.AddClosedCurve(new PointF[] { P(-2, 0), P(-14, -12), P(-20, -26), P(-16, -38), P(-10, -36), P(-9, -28), P(-4, -33), P(-1, -26), P(4, -29), P(4, -18), P(8, -8), P(6, 0) }, 0.3f);
            for (int i = 10; i >= 1; i--) using (Pen glow = new Pen(Color.FromArgb(22, Accent), i * 2.4f)) { glow.LineJoin = LineJoin.Round; g.DrawPath(glow, r); }
            using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 22, 30))) g.FillPath(b, r);
        }
        using (SolidBrush beak = new SolidBrush(Color.FromArgb(60, 60, 74))) g.FillPolygon(beak, new PointF[] { P(-19, -1), P(-28, 2), P(-19, 4) });
        using (SolidBrush eye = new SolidBrush(Accent)) g.FillEllipse(eye, ox - 15.5f * k, oy - 1.6f * k, 3f * k, 3f * k);
        using (Pen f = new Pen(Color.FromArgb(84, 88, 108), 1.4f * k))
        {
            f.StartCap = LineCap.Round; f.EndCap = LineCap.Round;
            g.DrawCurve(f, new PointF[] { P(-4, -4), P(-10, -16), P(-12, -30) }, 0.5f);
            g.DrawCurve(f, new PointF[] { P(0, -4), P(-3, -16), P(-2, -27) }, 0.5f);
        }
    }

    static void Feather(Graphics g, float x, float y, float deg)
    {
        GraphicsState s = g.Save();
        g.TranslateTransform(x, y); g.RotateTransform(deg);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 200, 225, 245))) g.FillEllipse(b, -2.6f, -7f, 5.2f, 14f);
        using (Pen p = new Pen(Color.FromArgb(160, 60, 90, 120), 1f)) g.DrawLine(p, 0f, -7f, 0f, 7f);
        g.Restore(s);
    }
}

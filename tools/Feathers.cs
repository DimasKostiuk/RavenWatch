using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// A 2x2 sheet of light grey feathers on a transparent ground (tinted in the particles). args: out.png [size]
class Feathers
{
    static Random rnd = new Random(7);

    static void Main(string[] args)
    {
        int size = args.Length > 1 ? int.Parse(args[1]) : 512;
        int cell = size / 2;
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            // length, width, bend, left share, splits, tilt
            Feather(g, 0, 0, cell, 0.86f, 0.30f, 0.10f, 0.42f, 2, -8f);
            Feather(g, cell, 0, cell, 0.80f, 0.36f, -0.08f, 0.48f, 3, 6f);
            Feather(g, 0, cell, cell, 0.70f, 0.40f, 0.16f, 0.38f, 1, -14f);
            Feather(g, cell, cell, cell, 0.88f, 0.24f, -0.04f, 0.45f, 4, 10f);
        }
        bmp.Save(args[0], ImageFormat.Png);
    }

    // spine point at t (0 base .. 1 tip) in cell space, before the tilt
    static PointF Spine(float t, float cx, float baseY, float len, float bend)
    {
        float y = baseY - t * len;
        float x = cx + bend * len * (float)Math.Sin(t * Math.PI * 0.9) * 0.5f;
        return new PointF(x, y);
    }

    static void Feather(Graphics g, int ox, int oy, int cell, float lenK, float widK, float bend, float leftShare, int splits, float tilt)
    {
        GraphicsState st = g.Save();
        g.TranslateTransform(ox + cell / 2f, oy + cell / 2f);
        g.RotateTransform(tilt);
        g.TranslateTransform(-cell / 2f, -cell / 2f);

        float len = cell * lenK, wid = cell * widK;
        float cx = cell / 2f, baseY = cell / 2f + len / 2f;
        float bare = 0.12f; // the bare quill at the base

        // where the vane is split (a few barbs part from the rest)
        float[] splitAt = new float[splits];
        int[] splitSide = new int[splits];
        for (int i = 0; i < splits; i++) { splitAt[i] = 0.3f + (float)rnd.NextDouble() * 0.6f; splitSide[i] = rnd.Next(2); }

        int barbs = (int)(len * 1.4f);
        for (int side = 0; side < 2; side++)
        {
            float sideW = wid * (side == 0 ? leftShare : 1f - leftShare);
            float sign = side == 0 ? -1f : 1f;
            for (int i = 0; i < barbs; i++)
            {
                float t = bare + (1f - bare) * i / (float)barbs;
                // the vane's outline: full in the middle, rounded to the tip, narrowing to the base
                float shape = (float)Math.Pow(Math.Sin(Math.PI * Math.Min(1.0, (t - bare) / (1f - bare) * 0.92 + 0.04)), 0.7);
                float w = sideW * shape * (0.92f + (float)rnd.NextDouble() * 0.12f);
                // a split: the barbs there lean a little more and leave a thin gap
                float lean = 0.55f;
                bool gap = false;
                for (int s = 0; s < splits; s++)
                    if (splitSide[s] == side && Math.Abs(t - splitAt[s]) < 0.03f) { lean += 0.1f; if (Math.Abs(t - splitAt[s]) < 0.004f) gap = true; }
                if (gap) continue;
                PointF a = Spine(t, cx, baseY, len, bend);
                PointF a2 = Spine(Math.Min(1f, t + 0.01f), cx, baseY, len, bend);
                float dx = a2.X - a.X, dy = a2.Y - a.Y, dl = (float)Math.Sqrt(dx * dx + dy * dy) + 1e-4f;
                dx /= dl; dy /= dl; // along the spine, to the tip
                // outwards, leaning toward the tip, slightly curved
                float nx = -dy * sign, ny = dx * sign;
                float bx = nx * (1f - lean) + dx * lean, by = ny * (1f - lean) + dy * lean;
                float bl = (float)Math.Sqrt(bx * bx + by * by); bx /= bl; by /= bl;
                PointF mid = new PointF(a.X + bx * w * 0.55f + nx * w * 0.06f, a.Y + by * w * 0.55f + ny * w * 0.06f);
                PointF end = new PointF(a.X + bx * w, a.Y + by * w);
                int tone = 205 + rnd.Next(50);
                bool down = t < bare + 0.12f; // the fluffy down near the base
                int alphaIn = down ? 90 : 230, alphaOut = down ? 40 : 150;
                using (Pen p1 = new Pen(Color.FromArgb(alphaIn, tone, tone, tone), down ? 1.2f : 1.6f))
                using (Pen p2 = new Pen(Color.FromArgb(alphaOut, tone, tone, tone), down ? 1f : 1.3f))
                {
                    p1.StartCap = LineCap.Round; p1.EndCap = LineCap.Round; p2.EndCap = LineCap.Round;
                    if (down) { end.X += (float)(rnd.NextDouble() - 0.5) * 6f; end.Y += (float)(rnd.NextDouble() - 0.5) * 6f; }
                    g.DrawLine(p1, a, mid);
                    g.DrawLine(p2, mid, end);
                }
            }
        }

        // the quill: from the bare base to the tip, tapering, a little lighter
        int steps = 40;
        for (int i = 0; i < steps; i++)
        {
            float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
            float w = 3.2f * (1f - t0) + 0.6f;
            using (Pen q = new Pen(Color.FromArgb(255, 248, 248, 248), w))
            {
                q.StartCap = LineCap.Round; q.EndCap = LineCap.Round;
                g.DrawLine(q, Spine(t0, cx, baseY, len, bend), Spine(t1, cx, baseY, len, bend));
            }
        }
        g.Restore(st);
    }
}


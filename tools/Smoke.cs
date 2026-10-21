using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// A 2x2 sheet of soft white smoke puffs on a transparent ground (tinted and faded in the particles).
// Build: csc /r:System.Drawing.dll Smoke.cs ; run: Smoke.exe out.png [size]
class Smoke
{
    static Random rnd = new Random(11);

    static void Main(string[] args)
    {
        int size = args.Length > 1 ? int.Parse(args[1]) : 512;
        int cell = size / 2;
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            for (int cy = 0; cy < 2; cy++)
                for (int cx = 0; cx < 2; cx++)
                    Puff(g, cx * cell, cy * cell, cell);
        }
        // a soft edge: nothing reaches the cell borders
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float lx = (x % cell) / (float)cell - 0.5f, ly = (y % cell) / (float)cell - 0.5f;
                float d = (float)Math.Sqrt(lx * lx + ly * ly) * 2f;
                if (d < 0.8f) continue;
                Color c = bmp.GetPixel(x, y);
                float k = Math.Max(0f, 1f - (d - 0.8f) / 0.2f);
                bmp.SetPixel(x, y, Color.FromArgb((int)(c.A * k), c.R, c.G, c.B));
            }
        bmp.Save(args[0], ImageFormat.Png);
    }

    // many soft round blobs, denser in the middle: a lumpy cloud
    static void Puff(Graphics g, int ox, int oy, int cell)
    {
        float c = cell / 2f;
        int blobs = 34;
        for (int i = 0; i < blobs; i++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2;
            double dist = Math.Pow(rnd.NextDouble(), 1.6) * cell * 0.26;
            float r = (float)(cell * (0.1 + rnd.NextDouble() * 0.16));
            float x = ox + c + (float)(Math.Cos(ang) * dist), y = oy + c + (float)(Math.Sin(ang) * dist);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(x - r, y - r, r * 2, r * 2);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    int a = 30 + rnd.Next(35);
                    b.CenterColor = Color.FromArgb(a, 255, 255, 255);
                    b.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillPath(b, path);
                }
            }
        }
    }
}

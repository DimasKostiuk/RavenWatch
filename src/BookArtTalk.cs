#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // The talk scroll's own pictures (the user 2026-11-06, docs/plans/mockups/talk-flow.html; the paper after the user's design «Сувій - доручення», archive 4A): made once
    // each and kept here as every picture of the book is (Make).
    //   TalkPaper(v)   the parchment, sliced: its colour across, its fibres, its edges shaded, its bottom torn — a wavy line with nicks and two bites, a darker rim (TornVariants of them,
    //                  each person its own by its name)
    //   TalkRod        the wooden rod, sliced across: grain, knots, rounded ends · TalkBand: an iron band with two rivets · TalkRope: a twisted rope, tiled down
    internal static partial class BookArt
    {
        internal const float TalkPaperTop = 30f, TalkPaperBottom = 84f, TalkPaperSide = 8f; // its slices, in pixels = canvas units

        internal static Sprite TalkPaper(int v)
        {
            v = (v & 0x7fffffff) % TornVariants;
            float[] tear = Tear(v, 512);
            return Make("talkpaper:" + v, 512, 512, (x, y) =>
            {
                int xi = Mathf.Clamp((int)x, 0, 511);
                float d = tear[xi]; // (px from the bottom up to the torn edge)
                if (y < d) return Color.clear;
                float u = x / 512f;
                Color c = Stops(u, new[] { 0f, 0.09f, 0.5f, 0.9f, 1f }, new[] { "#d3be92", "#e9dbb7", "#efe3c5", "#e7d7b1", "#ccb688" });
                if (y < d + 3f) return Color.Lerp(C("#6e4f26"), C("#9a7a48"), (y - d) / 3f); // (its rim)
                float shade = 0f;
                if (Mathf.Repeat(x + y * 0.123f, 7f) < 1f) shade += 0.05f;                    // (fibres, almost upright)
                if (Mathf.Repeat(y + x * 0.123f, 11f) < 1f) shade += 0.03f;
                shade += (Hash(xi, (int)y, v) - 0.5f) * 0.03f;                                 // (grain)
                float side = Mathf.Min(x, 512f - x);
                if (side < 40f) shade += Mathf.Pow(1f - side / 40f, 2f) * 0.22f;               // (its edges, as an inset shadow)
                float top = 512f - y;
                if (top < 18f) shade += Mathf.Pow(1f - top / 18f, 2f) * 0.35f;                 // (under the rod)
                float low = y - d;
                if (low < 70f) shade += Mathf.Pow(1f - low / 70f, 1.6f) * 0.3f;               // (towards the torn edge)
                return Color.Lerp(c, C("#5a3c1e"), Mathf.Clamp01(shade));
            }, new Vector4(TalkPaperSide, TalkPaperBottom, TalkPaperSide, TalkPaperTop));
        }

        // the torn edge: a wavy base, nicks, two deeper bites with a tongue of paper between, the same for a variant every time
        private static float[] Tear(int v, int w)
        {
            System.Random r = new System.Random(v * 977 + 13);
            const int N = 80;
            float ph = (float)r.NextDouble() * 6f;
            float b1 = 0.15f + (float)r.NextDouble() * 0.2f, d1 = 18f + (float)r.NextDouble() * 10f, b2 = 0.6f + (float)r.NextDouble() * 0.25f, d2 = 24f + (float)r.NextDouble() * 12f;
            float[] pts = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float x = i / (float)N;
                float d = 12f + Mathf.Sin(x * 9.3f + ph) * 5f + Mathf.Sin(x * 23.1f + ph * 2f) * 3f + ((float)r.NextDouble() - 0.5f) * 5f;
                foreach (Vector2 b in new[] { new Vector2(b1, d1), new Vector2(b2, d2) })
                {
                    float k = Mathf.Abs(x - b.x) / 0.055f;
                    if (k < 1f) d += b.y * (1f - k * k) * (Mathf.Abs(x - b.x) < 0.01f ? 0.45f : 1f);
                }
                if (r.NextDouble() < 0.12) d += 5f + (float)r.NextDouble() * 7f;
                if (r.NextDouble() < 0.06) d -= 5f;
                pts[i] = Mathf.Max(0f, d);
            }
            float[] o = new float[w];
            for (int x = 0; x < w; x++) { float f = x / (float)(w - 1) * N; int i = Mathf.Min(N - 1, (int)f); o[x] = Mathf.Lerp(pts[i], pts[i + 1], f - i); }
            return o;
        }

        internal static Sprite TalkRod
        {
            get
            {
                return Make("talkrod", 256, 32, (x, y) =>
                {
                    float out_ = BoxDistance(x, y, 256f, 32f, 16f, false);
                    if (out_ > 0.5f) return Color.clear;
                    float v = y / 32f;
                    Color c = Stops(1f - v, new[] { 0f, 0.4f, 0.7f, 1f }, new[] { "#8a5e36", "#5e3c20", "#3a2414", "#1e130a" });
                    float shade = 0f;
                    if (Mathf.Repeat(y, 4f) < 1f) shade -= 0.06f;          // (grain along it)
                    if (Mathf.Repeat(x, 23f) < 2f) shade += 0.07f;         // (knots)
                    if (y > 29f) shade -= 0.2f;                            // (its top lit)
                    c = shade < 0f ? Color.Lerp(c, C("#c99a64"), -shade) : Color.Lerp(c, Color.black, shade);
                    c.a = Mathf.Clamp01(0.5f - out_);
                    return c;
                }, new Vector4(16f, 0f, 16f, 0f));
            }
        }

        internal static Sprite TalkBand
        {
            get
            {
                return Make("talkband", 12, 36, (x, y) =>
                {
                    float out_ = BoxDistance(x, y, 12f, 36f, 3f, false);
                    if (out_ > 0.5f) return Color.clear;
                    Color c = Stops(x / 12f, new[] { 0f, 0.4f, 0.7f, 1f }, new[] { "#2a2c2e", "#7a7e80", "#3a3c3e", "#1a1b1c" });
                    foreach (float ry in new[] { 8f, 28f })
                    {
                        float dd = Mathf.Sqrt((x - 6f) * (x - 6f) + (y - ry) * (y - ry));
                        if (dd < 3f) c = Color.Lerp(C("#d9dcdd"), C("#55595b"), dd / 3f);
                    }
                    c.a = Mathf.Clamp01(0.5f - out_);
                    return c;
                }, Vector4.zero);
            }
        }

        internal static Sprite TalkRope
        {
            get
            {
                return Make("talkrope", 8, 14, (x, y) =>
                {
                    float t = Mathf.Repeat(x * 0.36f + y, 7f);
                    Color c = t < 3f ? C("#c9a46a") : t < 5f ? C("#8a6a3c") : C("#5e4524");
                    float edge = Mathf.Abs(x - 4f) / 4f;
                    return Color.Lerp(c, Color.black, edge * edge * 0.45f);
                }, Vector4.zero, false, TextureWrapMode.Repeat);
            }
        }

        // ---------------------------------------------------------------- a sheet's own pictures (the user's picture 2026-11-06, docs/plans/mockups/npc-card-2026-11-06.png)
        // how it is with you: the book's own brush stroke (its ragged ends, its bristles) painted rust through parchment to green. Made once
        internal static Sprite SheetScale
        {
            get
            {
                Sprite had;
                if (Sprites.TryGetValue("sheetscale", out had) && had != null) return had;
                Texture2D brush = Texture("brush-gold", true);
                if (brush == null) return Round(3f);
                Color[] from = brush.GetPixels();
                int bw = brush.width, bh = brush.height;
                return Make("sheetscale", bw, bh, (x, y) =>
                {
                    Color b = from[Mathf.Clamp((int)y, 0, bh - 1) * bw + Mathf.Clamp((int)x, 0, bw - 1)];
                    Color c = Stops(x / bw, new[] { 0f, 0.3f, 0.5f, 0.75f, 1f }, new[] { "#9a2216", "#c97a3a", "#c8b48a", "#8fae5a", "#4f6a2a" }) * Mathf.Clamp((b.r + b.g + b.b) / 1.5f, 0.82f, 1.12f); // (its bristles stay seen)
                    c.a = b.a;
                    return c;
                }, Vector4.zero);
            }
        }

        // the parchment's roll on top, stretched across: light along its middle, dark to its edges
        internal static Sprite SheetRoll { get { return Make("sheetroll", 4, 32, (x, y) => Stops(y / 32f, new[] { 0f, 0.22f, 0.55f, 0.85f, 1f }, new[] { "#5e4522", "#b89a62", "#f0e3bd", "#d9c48f", "#7a5c30" }), Vector4.zero); } }

        // the roll's end: the paper wound round, seen end on
        internal static Sprite SheetRollEnd
        {
            get
            {
                return Make("sheetrollend", 64, 64, (x, y) =>
                {
                    float d = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)) / 31f;
                    if (d > 1f) return Color.clear;
                    if (d > 0.9f) { Color rim = C("#6e4f26"); rim.a = Mathf.Clamp01((1f - d) * 31f); return rim; }
                    Color c = C("#ecdcb2");
                    return Mathf.Repeat(d * 4.2f, 1f) < 0.2f ? Color.Lerp(c, C("#8a6a38"), 0.85f) : c;
                }, Vector4.zero, true);
            }
        }

        // the roll's shadow on the paper under it
        internal static Sprite SheetRollShade { get { return Make("sheetrollshade", 4, 16, (x, y) => new Color(0.16f, 0.09f, 0.03f, 0.34f * (y / 16f) * (y / 16f)), Vector4.zero); } }

        // a diamond with a soft edge, white (tinted where it is used): a turned square has steps on its edges
        internal static Sprite Diamond
        {
            get { return Make("diamond", 64, 64, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(30.5f - Mathf.Abs(x - 32f) - Mathf.Abs(y - 32f))), Vector4.zero, true); }
        }

        // an arrow pointing left (turned for right): no font is asked for it
        internal static Sprite SheetArrow
        {
            get
            {
                return Make("sheetarrow", 48, 24, (x, y) =>
                {
                    float v = Mathf.Abs(y - 12f);
                    bool shaft = v < 1.3f && x > 5f && x < 46f, head = x < 18f && Mathf.Abs(v - (x - 4f) * 0.75f) < 1.4f;
                    return shaft || head ? Color.white : new Color(1f, 1f, 1f, 0f);
                }, Vector4.zero, true);
            }
        }

        // the round shield on the roll: an iron rim with rivets, red paint on the left, bare planks on the right, an iron boss
        internal static Sprite SheetShield
        {
            get
            {
                Color rivet = C("#b9b9b4"), rivetLow = C("#55565a"), rim = C("#2f3032"), rimHigh = C("#85878a"), seam = C("#1b1410"), boss = C("#3b3c3f"), bossHigh = C("#a3a4a2"), red = C("#6d2410"), redHigh = C("#a43d1b"), wood = C("#5d3d1c"), woodHigh = C("#94683a"), plank = C("#2a1a0c");
                return Make("sheetshield", 256, 256, (x, y) =>
                {
                    float px = (x - 128f) / 126f, py = (y - 128f) / 126f, d = Mathf.Sqrt(px * px + py * py);
                    if (d > 1f) return Color.clear;
                    float light = 0.5f + 0.5f * py; // (lit from above)
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = (i + 0.5f) * Mathf.PI / 4f, rx = Mathf.Cos(ang) * 0.92f, ry = Mathf.Sin(ang) * 0.92f, r = Mathf.Sqrt((px - rx) * (px - rx) + (py - ry) * (py - ry));
                        if (r < 0.05f) return Color.Lerp(rivet, rivetLow, r / 0.05f); // (a rivet)
                    }
                    if (d > 0.85f) { Color iron = Color.Lerp(rim, rimHigh, light * (1f - Mathf.Abs(d - 0.925f) * 6f)); iron.a = Mathf.Clamp01((1f - d) * 126f); return iron; }
                    if (d > 0.82f) return seam;
                    if (d < 0.27f) return Color.Lerp(boss, bossHigh, Mathf.Clamp01(light * (1f - d * 1.2f) + 0.15f)); // (its boss)
                    if (d < 0.31f) return seam;
                    Color c;
                    if (px < 0f) c = Color.Lerp(red, redHigh, 0.35f + 0.5f * light + 0.15f * Hash((int)(x / 7f), (int)(y / 21f), 3));
                    else
                    {
                        c = Color.Lerp(wood, woodHigh, 0.3f + 0.45f * light + 0.25f * Hash((int)(x / 3f), (int)(y / 40f), 5));
                        if (Mathf.Abs(px - 0.38f) < 0.012f) c = plank; // (a seam between planks)
                    }
                    if (Mathf.Abs(px) < 0.012f) c = plank;
                    return c;
                }, Vector4.zero, true);
            }
        }

        private static Color Stops(float t, float[] at, string[] hex)
        {
            for (int i = 1; i < at.Length; i++) if (t <= at[i]) return Color.Lerp(C(hex[i - 1]), C(hex[i]), Mathf.InverseLerp(at[i - 1], at[i], t));
            return C(hex[hex.Length - 1]);
        }

        private static float Hash(int x, int y, int s)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }
}
#endif

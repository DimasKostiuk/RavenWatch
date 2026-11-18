#if RAVEN_ITEM
using System;
using UnityEngine;

namespace HuginWhispers
{
    // A heart's board's own pictures (the user's design 2026-11-06, docs/plans/mockups/heart-board.html): made once each and kept here as every picture of the book is (Make), by the design's own colours.
    //   BoardPlank the head's wood, stretched across · BoardIron its iron ends (left: with rivets; foot: the foot beam's smaller ones) · BoardPlaque the name's plate, sliced: it grows with the name, its pointed ends, a rivet in each, kept
    //   BoardWood  the planks behind the notes, stretched · BoardPost a side post, tiled down: a joint and a rivet in every length · BoardBeam the foot beam's wood, stretched across
    //   BoardRail  the iron bar between the buttons' rows · BoardDash a dashed line, tiled across · BoardChain a hanging chain
    //   BoardAct   a wooden button with iron corners: normal, under the pointer, pressed; the main one with a gold rim
    internal static partial class BookArt
    {
        internal const float BoardPlaqueSide = 30f, BoardActCorner = 12f;

        private static Color Over(Color under, Color c) { return new Color(Mathf.Lerp(under.r, c.r, c.a), Mathf.Lerp(under.g, c.g, c.a), Mathf.Lerp(under.b, c.b, c.a), 1f); }

        internal static Sprite BoardPlank
        {
            get
            {
                return Make("boardplank", 64, 72, (x, y) =>
                {
                    float t = 1f - y / 72f;
                    Color c = Stops(t, new[] { 0f, 0.3f, 0.65f, 1f }, new[] { "#86603e", "#664628", "#4a3119", "#2a1a0c" });
                    float row = Mathf.Repeat(t * 72f, 12f);
                    if (row >= 4f && row < 5f) c = Over(c, new Color(0.08f, 0.05f, 0.02f, 0.16f));
                    if (row >= 11f) c = Over(c, new Color(1f, 0.86f, 0.63f, 0.05f));
                    if (t * 72f < 2f) c = Over(c, new Color(1f, 0.86f, 0.63f, 0.25f));
                    if (t * 72f > 69f) c = Over(c, new Color(0f, 0f, 0f, 0.45f));
                    float groove = Mathf.Min(Mathf.Abs(t * 72f - 11f), Mathf.Abs(t * 72f - 61f));
                    if (groove < 1f) c = Over(c, new Color(0f, 0f, 0f, 0.5f));
                    return c;
                }, Vector4.zero);
            }
        }

        private static Color Rivet(float x, float y, Vector2 at, float r) { return Color.Lerp(C("#d8d0c0"), C("#6e685c"), (new Vector2(x, y) - at + new Vector2(r * 0.32f, -r * 0.32f)).magnitude / r); }

        // an iron end, its point to the left (turned round for the right one); rivets: the left one's three. foot: the foot beam's, half as tall, one rivet
        internal static Sprite BoardIron(bool rivets, bool foot = false)
        {
            int w = foot ? 30 : 48, h = foot ? 36 : 72; float half = h * 0.5f, point = foot ? 18f : 30f;
            Vector2[] at = foot ? new[] { new Vector2(19.5f, 17.5f) } : new[] { new Vector2(32.5f, 59.5f), new Vector2(32.5f, 12.5f), new Vector2(15.5f, 36f) };
            return Make("boardiron:" + rivets + ":" + foot, w, h, (x, y) =>
            {
                float t = 1f - y / h, off = Mathf.Abs(y - half) / half * point;
                if (x <= off - 1f) return Color.clear;
                Color c = Stops(t, new[] { 0f, 0.4f, 1f }, new[] { "#8a857a", "#4e4a42", "#24211c" });
                c.a = Mathf.Clamp01(x - off + 1f);
                if (!rivets) return c;
                foreach (Vector2 r in at)
                    if ((new Vector2(x, y) - r).sqrMagnitude < 6.25f) return Rivet(x, y, r, 2.5f);
                return c;
            }, Vector4.zero);
        }

        // the distance inside a plate with pointed ends (px, below zero: outside)
        private static float Inside(float x, float y, float w, float h, float side)
        {
            float dy = h * 0.5f - Mathf.Abs(y - h * 0.5f), dx = Mathf.Min(x, w - x), slope = side / (h * 0.5f);
            return Mathf.Min(dy, (dx - (Mathf.Abs(y - h * 0.5f)) * slope) / Mathf.Sqrt(1f + slope * slope));
        }

        internal static Sprite BoardPlaque
        {
            get
            {
                const float W = 96f, H = 94f;
                return Make("boardplaque", (int)W, (int)H, (x, y) =>
                {
                    float d = Inside(x, y, W, H, 24f), t = 1f - y / H, row = Mathf.Repeat(t * H, 10f);
                    if (d <= 0f) return Color.clear;
                    Color c;
                    if (d < 3f) { c = Stops(t, new[] { 0f, 0.4f, 1f }, new[] { "#e0b660", "#b8893e", "#7f4e04" }); c.a = Mathf.Clamp01(d); }
                    else if (d < 9f) c = Stops(t, new[] { 0f, 0.55f, 1f }, new[] { "#5e3f22", "#3e2814", "#24160a" });
                    else if (d < 10.5f) return Over(Stops(t, new[] { 0f, 0.55f, 1f }, new[] { "#5e3f22", "#3e2814", "#24160a" }), new Color(0.72f, 0.54f, 0.24f, 0.55f));
                    else { c = Stops(t, new[] { 0f, 0.6f, 1f }, new[] { "#523720", "#36230f", "#22150a" }); if (d < 14f) c = Over(c, new Color(0f, 0f, 0f, 0.25f)); }
                    if (d >= 3f && row >= 4f && row < 5f) c = Over(c, new Color(0f, 0f, 0f, 0.13f));
                    foreach (Vector2 r in new[] { new Vector2(8.5f, 46.5f), new Vector2(W - 9.5f, 46.5f) }) // (a rivet in each point)
                        if ((new Vector2(x, y) - r).sqrMagnitude < 12.25f) return Rivet(x, y, r, 3.5f);
                    return c;
                }, new Vector4(BoardPlaqueSide, 0f, BoardPlaqueSide, 0f));
            }
        }

        // the planks: seams and each plank's own colour as the design's, grain, a few knots, dark round the edges
        internal static Sprite BoardWood
        {
            get
            {
                const int W = 472, H = 256;
                float[] seams = { 75f, 159f, 226f, 314f, 385f };
                Color[] planks = { C("#5c3f24"), C("#4c331d"), C("#614528"), C("#523821"), C("#5a3d22"), C("#48301b") };
                Vector3[] knots = { new Vector3(108f, 177f, 7f), new Vector3(349f, 95f, 6f), new Vector3(193f, 36f, 5f) };
                return Make("boardwood", W, H, (x, y) =>
                {
                    int p = 0; while (p < seams.Length && x >= seams[p]) p++;
                    Color c = planks[p];
                    float ty = 1f - y / H;
                    c = Color.Lerp(c, Color.black, Mathf.Max(0f, ty - 0.3f) * 0.3f);
                    if (Mathf.Repeat(x + y * 0.01f, 9f) < 0.7f) c = Over(c, new Color(0.08f, 0.04f, 0.02f, 0.13f));
                    if (Mathf.Repeat(x - y * 0.012f, 14.5f) < 0.6f) c = Over(c, new Color(0.08f, 0.04f, 0.02f, 0.1f));
                    foreach (Vector3 k in knots)
                    {
                        float dx = (x - k.x) / k.z, dy = (y - k.y) / (k.z * 2.4f), r = dx * dx + dy * dy;
                        if (r < 1f) c = Over(c, new Color(0.12f, 0.07f, 0.03f, Mathf.Lerp(0.85f, 0f, r)));
                        else if (r < 4f) c = Over(c, new Color(0.12f, 0.07f, 0.03f, 0.2f * Mathf.Sin(r)));
                    }
                    foreach (float s in seams) { if (x >= s - 1f && x < s) c = Over(c, new Color(0.04f, 0.02f, 0.01f, 0.7f)); else if (x >= s && x < s + 0.5f) c = Over(c, new Color(1f, 0.86f, 0.67f, 0.1f)); }
                    float edge = Mathf.Min(Mathf.Min(x, W - x) / 25f, Mathf.Min(y, H - y) / 25f);
                    if (edge < 1f) c = Color.Lerp(c, Color.black, (1f - edge) * 0.5f);
                    if (H - y < 9f) c = Color.Lerp(c, Color.black, (9f - (H - y)) / 9f * 0.45f);
                    return c;
                }, Vector4.zero);
            }
        }

        // a round post seen from the side: light along its middle, dark at both edges; every length (the tile) a joint at its foot and a rivet in its middle
        internal static Sprite BoardPost
        {
            get
            {
                const float W = 24f, H = 168f;
                Vector2 rivet = new Vector2(11.5f, 84f);
                return Make("boardpost", (int)W, (int)H, (x, y) =>
                {
                    Color c = Stops(x / (W - 1f), new[] { 0f, 0.3f, 0.62f, 1f }, new[] { "#3a2412", "#75522f", "#5a3d22", "#22140a" });
                    if (Mathf.Repeat(x + Mathf.Sin(y * Mathf.PI * 2f / H) * 0.8f, 5f) < 0.6f) c = Over(c, new Color(0.08f, 0.04f, 0.02f, 0.14f));
                    if (y < 2f) c = Over(c, new Color(0.04f, 0.02f, 0.01f, 0.7f)); else if (y < 3f) c = Over(c, new Color(1f, 0.86f, 0.67f, 0.12f));
                    if (x < 1f || x >= W - 1f) c = Over(c, new Color(0.04f, 0.02f, 0.01f, 0.7f));
                    float d = (new Vector2(x, y) - rivet).magnitude;
                    if (d < 2.5f) return Rivet(x, y, rivet, 2.5f);
                    if (d < 3.6f) c = Over(c, new Color(0.04f, 0.02f, 0.01f, 0.45f));
                    return c;
                }, Vector4.zero, false, TextureWrapMode.Repeat);
            }
        }

        internal static Sprite BoardBeam
        {
            get
            {
                return Make("boardbeam", 64, 36, (x, y) =>
                {
                    float at = (1f - y / 36f) * 36f, row = Mathf.Repeat(at, 9f);
                    Color c = Stops(at / 36f, new[] { 0f, 0.3f, 0.65f, 1f }, new[] { "#86603e", "#664628", "#4a3119", "#2a1a0c" });
                    if (row >= 3f && row < 4f) c = Over(c, new Color(0.08f, 0.05f, 0.02f, 0.16f));
                    if (at < 2f) c = Over(c, new Color(1f, 0.86f, 0.63f, 0.25f));
                    if (at > 33f) c = Over(c, new Color(0f, 0f, 0f, 0.45f));
                    return c;
                }, Vector4.zero);
            }
        }

        internal static Sprite BoardDash { get { return Make("boarddash", 6, 1, (x, y) => x < 4f ? Color.white : Color.clear, Vector4.zero, false, TextureWrapMode.Repeat); } }

        internal static Sprite BoardRail { get { return Make("boardrail", 4, 10, (x, y) => Stops(1f - y / 10f, new[] { 0f, 0.5f, 1f }, new[] { "#6e685c", "#3a362f", "#1e1c18" }), Vector4.zero); } }

        // three links down to a plate
        internal static Sprite BoardChain
        {
            get
            {
                return Make("boardchain", 18, 80, (x, y) =>
                {
                    float fromTop = 80f - y, cx = Mathf.Abs(x - 9f);
                    if (fromTop > 72f) return cx < 9f ? Stops((fromTop - 72f) / 8f, new[] { 0f, 1f }, new[] { "#7a756a", "#2e2b26" }) : Color.clear;
                    float link = Mathf.Repeat(fromTop, 24f);
                    if (link < 17f) { float rx = (x - 9f) / 5.5f, ry = (link - 8.5f) / 8.5f, r = rx * rx + ry * ry; return r <= 1f && r > 0.32f ? C("#5e594f") : Color.clear; }
                    return cx < 1.5f ? Color.Lerp(C("#9a948a"), C("#3a3630"), (x - 7.5f) / 3f) : Color.clear;
                }, Vector4.zero);
            }
        }

        // the iron ✕'s plate, the same on a talk, a card and a board (RavenBook.CloseButton): a square of dark iron, lit from above, no gold
        internal static Sprite IronClose
        {
            get
            {
                Color top = C("#5e6062"), low = C("#2b2c2e"), edge = C("#141517");
                return Make("ironclose", 92, 92, (x, y) =>
                {
                    float qx = Mathf.Max(Mathf.Abs(x - 46f) - 34f, 0f), qy = Mathf.Max(Mathf.Abs(y - 46f) - 34f, 0f), d = Mathf.Sqrt(qx * qx + qy * qy) - 11f; // (a rounded square: below zero inside)
                    if (d > 0f) return Color.clear;
                    if (d > -3f) { Color rim = edge; rim.a = Mathf.Clamp01(-d); return rim; }
                    Color c = Color.Lerp(low, top, y / 92f);
                    return d > -5f && y > 46f ? Color.Lerp(c, Color.white, 0.18f) : c;
                }, Vector4.zero, true);
            }
        }

        // a name in the old runes, letter by letter, for the head's carving: only for the eye
        private const string RuneFrom = "абвгґдеєжзиіїйклмнопрстуфхцчшщюяabcdefghijklmnopqrstuvwxyz";
        private const string RuneTo = "ᚨᛒᚹᚷᚷᛞᛖᛖᛉᛉᛁᛁᛁᛃᚲᛚᛗᚾᛟᛈᚱᛊᛏᚢᚠᚺᛏᚲᛊᛊᛃᛃᚨᛒᚲᛞᛖᚠᚷᚺᛁᛃᚲᛚᛗᚾᛟᛈᚲᚱᛊᛏᚢᚹᚹᚲᛃᛉ";
        internal static string RuneWords(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char ch in (name ?? "").ToLowerInvariant())
            {
                int at = RuneFrom.IndexOf(ch);
                if (at >= 0) sb.Append(RuneTo[at]); else if (ch == ' ' && sb.Length > 0) sb.Append(' ');
            }
            return sb.Length > 0 ? sb.ToString() : "ᚺᛖᛁᛗ";
        }

        // state 0 normal · 1 under the pointer · 2 pressed; main: the gold rim
        internal static Sprite BoardAct(int state, bool main)
        {
            string[] hex = state == 2 ? (main ? new[] { "#4a3018", "#5a3d1f", "#6a4a26" } : new[] { "#3a2616", "#47301c", "#553a22" })
                    : state == 1 ? (main ? new[] { "#9a7444", "#74522e", "#4e341c" } : new[] { "#8e6a42", "#6a4a2c", "#46301c" })
                    : (main ? new[] { "#8a6438", "#684826", "#46301a" } : new[] { "#7e5a38", "#5e4026", "#3e2818" });
            return Make("boardact:" + state + ":" + main, 64, 46, (x, y) =>
            {
                float t = 1f - y / 46f;
                float dx = Mathf.Min(x, 64f - x), dy = Mathf.Min(y, 46f - y), dt = t * 46f;
                if (state != 2 && dt > 43f) return C("#1e140a"); // (its thick lower edge)
                float h = state == 2 ? 46f : 43f;
                Color c = Stops(Mathf.Clamp01(dt / h), new[] { 0f, 0.45f, 1f }, hex);
                if (Mathf.Min(dx, dt < h ? Mathf.Min(dt, h - dt) : 0f) < 1.5f) return C("#1e140a");
                if (state != 2 && dt < 2.5f) c = Over(c, new Color(1f, 0.86f, 0.63f, 0.3f));
                if (state == 2 && dt < 5f) c = Over(c, new Color(0f, 0f, 0f, 0.5f));
                if (main && Mathf.Min(dx, Mathf.Min(dt, h - dt)) < 3.2f) c = C("#e8be64");
                else if (main && Mathf.Min(dx, Mathf.Min(dt, h - dt)) < 5f) c = Over(c, new Color(0.16f, 0.09f, 0.04f, 0.5f));
                if (dx + Mathf.Min(dt, h - dt) < 10f) { Color iron = Stops(t < 0.5f ? 0f : 1f, new[] { 0f, 1f }, new[] { "#9a948a", "#4e4a42" }); return Color.Lerp(iron, C("#3a3630"), (dx + Mathf.Min(dt, h - dt)) / 10f); } // (its iron corners)
                return c;
            }, new Vector4(BoardActCorner, BoardActCorner, BoardActCorner, BoardActCorner));
        }
    }
}
#endif

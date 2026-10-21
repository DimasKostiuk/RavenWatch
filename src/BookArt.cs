#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The Book of ravens' look, after the design «Книга воронів» (Desktop\book-export): its pictures built into the mod
    // (assets/book: the empty book rendered from the design by tools/book_chrome.ps1, the brush strokes, the portraits),
    // its fonts (assets/fonts, SIL OFL: Forum for headings, Alegreya for the words, Noto Sans Runic for the runes; the
    // game's own font when they cannot be made) and the shapes drawn here (seals, rings, rounded boxes).
    internal static class BookArt
    {
        // ---------------------------------------------------------------- colours of the design

        internal static Color C(string hex) { Color c; return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta; }
        internal static Color A(Color c, float a) { c.a = a; return c; }

        internal static readonly Color Ink = C("#3a2414"), InkSoft = C("#5a3c22"), Brown = C("#6a4a2a"), Caption = C("#7a5a32"),
            GoldInk = C("#7a5514"), Initial = C("#94681c"), Good = C("#2f5a2a"), Bad = C("#8E1E1E"),
            Gold = C("#E6C77C"), Paper = C("#f0e6cc"), PaperDim = C("#c9bc9e"), Muted = C("#8c7a5c"),
            Munin = C("#C2503A"), MuninDark = C("#7a2414"), Hugin = C("#6FA8D6"), HuginInk = C("#2a5a86"), HuginDark = C("#2a4a6a"),
            Card = new Color(0.98f, 0.93f, 0.81f, 0.8f), CardLine = new Color(0.227f, 0.141f, 0.078f, 0.55f), // (denser than the design's .35: on the game's paper the cards looked switched off)
            TipBg = C("#15110c"), TipLine = C("#6a5430"), TipInk = C("#e8dcc0");
        internal const string GoldHex = "#E6C77C", GoldInkHex = "#7a5514", GoodHex = "#2f5a2a", BadHex = "#8E1E1E", MutedHex = "#8c7a5c", InkSoftHex = "#5a3c22";

        internal static Color RavenInk(bool munin) { return munin ? Munin : HuginInk; }

        // ---------------------------------------------------------------- built-in pictures

        private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        private static byte[] Resource(string name)
        {
            using (Stream st = typeof(BookArt).Assembly.GetManifestResourceStream(name))
            {
                if (st == null) return null;
                byte[] data = new byte[st.Length];
                int read = 0;
                while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; }
                return data;
            }
        }

        // a picture of assets/book (name without .png); readable: kept in memory too, for the shapes made from it
        internal static Texture2D Texture(string name, bool readable = false)
        {
            Texture2D t;
            string key = readable ? name + ":r" : name; // a readable copy (kept in memory, for the shapes made from it) is its own
            if (Textures.TryGetValue(key, out t)) return t;
            t = null;
            try
            {
                byte[] data = Resource("RavenWatch.book." + name + ".png");
                if (data != null && LoadImage != null)
                {
                    t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear;
                    if (!(bool)LoadImage.Invoke(null, new object[] { t, data })) { UnityEngine.Object.Destroy(t); t = null; }
                    else t.Apply(true, !readable);
                }
            }
            catch (Exception e) { Log("picture " + name + ": " + e.Message); t = null; }
            Textures[key] = t;
            return t;
        }

        internal static Sprite Picture(string name)
        {
            Sprite s;
            if (Sprites.TryGetValue(name, out s)) return s;
            Texture2D t = Texture(name);
            s = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect) : null;
            if (s != null) s.hideFlags = HideFlags.HideAndDontSave;
            Sprites[name] = s;
            return s;
        }

        // a picture drawn at 2× (the popup cards' pieces, tools/card_art.html) cut for slicing: border in UI units (an
        // Image shows it with pixelsPerUnitMultiplier = 2)
        internal static Sprite Sliced(string name, float border)
        {
            string key = "sliced:" + name;
            Sprite s;
            if (Sprites.TryGetValue(key, out s)) return s;
            Texture2D t = Texture(name);
            float b = border * 2f;
            s = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b)) : null;
            if (s != null) s.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = s;
            return s;
        }

        // a brush stroke (512×28): its ragged ends kept, the middle stretched to the bar's length
        internal static Sprite Brush(string name)
        {
            string key = "brush:" + name;
            Sprite s;
            if (Sprites.TryGetValue(key, out s)) return s;
            Texture2D t = Texture(name);
            s = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(BrushEnd, 0f, BrushEnd, 0f)) : null;
            if (s != null) s.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = s;
            return s;
        }
        internal const float BrushEnd = 36f; // texture pixels at each end that are not stretched

        // a raven's portrait cut round (Munin's turned to face the same way as Hugin's, as in the design)
        internal static Sprite Portrait(bool munin)
        {
            string key = munin ? "portrait:m" : "portrait:h";
            Sprite s;
            if (Sprites.TryGetValue(key, out s)) return s;
            s = null;
            try
            {
                Texture2D src = Texture(munin ? "portrait-munin" : "portrait-huginn", true);
                if (src != null)
                {
                    int n = src.width;
                    Color32[] px = src.GetPixels32();
                    Color32[] o = new Color32[n * n];
                    float half = n / 2f;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            Color32 c = px[y * n + (munin ? n - 1 - x : x)];
                            float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                            c.a = (byte)(c.a * Mathf.Clamp01(half - d));
                            o[y * n + x] = c;
                        }
                    Texture2D t = NewTex(n, n, true);
                    t.SetPixels32(o); t.Apply(true, true);
                    s = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    s.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch (Exception e) { Log("portrait: " + e.Message); }
            Sprites[key] = s;
            return s;
        }

        // ---------------------------------------------------------------- fonts

        internal static TMP_FontAsset Heading, Body, Italic, Runic;
        private static bool _fontsTried;

        // made once: the TTFs go to BepInEx/cache and the game's TextMeshPro reads them from there (glyphs as needed);
        // anything missing (★, ●, arrows) falls back to the game's own font
        internal static void Fonts(TMP_FontAsset game)
        {
            if (_fontsTried) return;
            _fontsTried = true;
            try
            {
                FieldInfo sdf = AccessTools.Field(typeof(ShaderUtilities), "k_ShaderRef_MobileSDF");
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null) shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                if (shader == null && game != null && game.material != null) shader = game.material.shader;
                if (sdf != null && shader != null) sdf.SetValue(null, shader);
            }
            catch (Exception e) { Log("font shader: " + e.Message); }
            Heading = Make("Forum-Regular.ttf", game);
            Body = Make("Alegreya-Regular.ttf", game);
            Italic = Make("Alegreya-Italic.ttf", game);
            Runic = Make("NotoSansRunic-Regular.ttf", game);
            try
            {
                if (Body != null && Italic != null && Body.fontWeightTable != null && Body.fontWeightTable.Length > 4)
                    Body.fontWeightTable[4].italicTypeface = Italic; // <i> in the words: the real italic
            }
            catch { }
            // a rune inside ordinary words (ᚹ in a line of Alegreya): the runic font among their fallbacks,
            // last (the game's font, which has no runes, is still asked first for anything else they lack)
            if (Runic != null)
                foreach (TMP_FontAsset f in new[] { Heading, Body, Italic })
                {
                    if (f == null) continue;
                    if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (!f.fallbackFontAssetTable.Contains(Runic)) f.fallbackFontAssetTable.Add(Runic);
                }
            Log("fonts: " + (Heading != null ? "Forum " : "") + (Body != null ? "Alegreya " : "") + (Italic != null ? "Alegreya-Italic " : "") + (Runic != null ? "Runic" : "") + (Heading == null && Body == null ? "none, the game's own" : ""));
        }

        private static TMP_FontAsset Make(string file, TMP_FontAsset fallback)
        {
            try
            {
                byte[] data = Resource("RavenWatch.fonts." + file);
                if (data == null) return null;
                string dir = Path.Combine(BepInEx.Paths.CachePath, "RavenWatch-fonts");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, file);
                if (!File.Exists(path) || new FileInfo(path).Length != data.Length) File.WriteAllBytes(path, data);
                TMP_FontAsset f = TMP_FontAsset.CreateFontAsset(path, 0, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048);
                if (f == null) return null;
                f.name = "RavenWatch-" + Path.GetFileNameWithoutExtension(file); // no spaces: the name is used in <font=...> tags
                f.isMultiAtlasTexturesEnabled = true;
                if (fallback != null) f.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                f.hideFlags = HideFlags.HideAndDontSave; // the game's asset cleanup on a world load must not take it
                if (f.material != null) { f.material.hideFlags = HideFlags.HideAndDontSave; f.material.name = f.name + " Material"; }
                // every made font's material comes out with the same name, so the second one hit a duplicate key in the
                // manager and the font was lost (only Forum loaded, runes showed as squares): own names, hashes counted anew
                try
                {
                    FieldInfo h = AccessTools.Field(typeof(TMP_FontAsset), "m_MaterialHashCode");
                    if (h != null) h.SetValue(f, 0);
                    h = AccessTools.Field(typeof(TMP_FontAsset), "m_HashCode");
                    if (h != null) h.SetValue(f, 0);
                }
                catch { }
                try { MaterialReferenceManager.AddFontAsset(f); } // made at run time: without this a <font="..."> tag cannot find it and shows as text
                catch (Exception e) { Log("font " + file + " <font> tag: " + e.Message); } // the font itself still works
                return f;
            }
            catch (Exception e) { Log("font " + file + ": " + e.Message); return null; }
        }

        // ---------------------------------------------------------------- shapes drawn here

        private static Texture2D NewTex(int w, int h, bool mips = false)
        {
            Texture2D t = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, mips);
            t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
            return t;
        }

        private static Sprite Make(string key, int w, int h, Func<float, float, Color> pixel, Vector4 border, bool mips = false, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            Sprite s;
            if (Sprites.TryGetValue(key, out s) && s != null) return s;
            Texture2D t = NewTex(w, h, mips);
            t.wrapMode = wrap;
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x + 0.5f, y + 0.5f); // y up, as textures go
            t.SetPixels(px);
            t.Apply(mips, true);
            s = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = s;
            return s;
        }

        // the signed distance from a rounded box's edge (negative inside); top only: square corners at the bottom
        private static float BoxDistance(float x, float y, float w, float h, float r, bool topOnly)
        {
            float rr = topOnly && y < h / 2f ? 0f : r;
            float qx = Mathf.Abs(x - w / 2f) - (w / 2f - rr), qy = Mathf.Abs(y - h / 2f) - (h / 2f - rr);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - rr;
        }

        // a white rounded box (radius in canvas units), nine-sliced
        internal static Sprite Round(float r, bool topOnly = false)
        {
            int n = Mathf.CeilToInt(r) * 2 + 4;
            float b = Mathf.Ceil(r) + 1f;
            return Make("round:" + r + (topOnly ? "t" : ""), n, n, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - BoxDistance(x, y, n, n, r, topOnly))), new Vector4(b, b, b, b));
        }

        // its outline only, this thick
        internal static Sprite Frame(float r, float width)
        {
            int n = Mathf.CeilToInt(Mathf.Max(r, width)) * 2 + 4;
            float b = Mathf.Ceil(Mathf.Max(r, width)) + 1f;
            return Make("frame:" + r + ":" + width, n, n, (x, y) =>
            {
                float d = BoxDistance(x, y, n, n, r, false);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + width));
            }, new Vector4(b, b, b, b));
        }

        // the same two drawn Fine times larger and mipmapped: shown with pixelsPerUnitMultiplier = Fine they keep their size,
        // and on the tilted page a thin frame stays a smooth line (the 1-pixel ones stepped into ladders there)
        internal const float Fine = 4f;
        internal static Sprite RoundFine(float r, bool topOnly = false)
        {
            float R = r * Fine;
            int n = Mathf.CeilToInt(R) * 2 + 8;
            float b = Mathf.Ceil(R) + 2f;
            return Make("roundF:" + r + (topOnly ? "t" : ""), n, n, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01((0.5f - BoxDistance(x, y, n, n, R, topOnly)) / 2f)), new Vector4(b, b, b, b), true);
        }

        internal static Sprite FrameFine(float r, float width, bool topOnly = false)
        {
            float R = r * Fine, W = width * Fine;
            int n = Mathf.CeilToInt(Mathf.Max(R, W)) * 2 + 8;
            float b = Mathf.Ceil(Mathf.Max(R, W)) + 2f;
            return Make("frameF:" + r + ":" + width + (topOnly ? "t" : ""), n, n, (x, y) =>
            {
                float d = BoxDistance(x, y, n, n, R, topOnly);
                return new Color(1f, 1f, 1f, Mathf.Clamp01((0.5f - d) / 2f) * Mathf.Clamp01((0.5f + d + W) / 2f)); // soft by a quarter pixel each side
            }, new Vector4(b, b, b, b), true);
        }

        // ---------------------------------------------------------------- paper cards (the user's «Погризені краї», 2026-10-19)
        // A card with gnawed edges: each side goes in and out by its own noise, whose period is the tiled middle, so the
        // tiles meet without a seam; the corners chipped; warmer and darker by the edge and a faint grain, as old paper.
        // Drawn Fine times larger and shown Tiled with pixelsPerUnitMultiplier = Fine: the edge keeps its size on any card,
        // never stretched. torn false: the same paper with straight edges (the pinned sheet).
        internal const float TornBorder = 12f, TornTile = 64f; // canvas units: the edge part, the tiled middle
        private static readonly float[][] TornPhase = { new[] { 0.3f, 2.1f, 4.4f, 1.2f, 5.0f }, new[] { 3.9f, 0.7f, 2.8f, 5.5f, 1.9f }, new[] { 1.6f, 4.9f, 0.2f, 3.3f, 0.9f }, new[] { 5.2f, 3.1f, 1.7f, 0.4f, 4.1f } };
        private static float TornIn(float t, int side, bool torn, int v)
        {
            if (!torn) return Fine * 2f;
            float p = TornTile * Fine, k = t / p * Mathf.PI * 2f;
            float[] f = TornPhase[(side + v) & 3];
            float o = v * 2.39996f; // each variant: the sides' noises swapped round and shifted (the golden angle: never back in step)
            float d = 3f + 0.9f * Mathf.Sin(k + f[0] + o) + 0.55f * Mathf.Sin(3f * k + f[1] + o * 2f) + 0.22f * Mathf.Sin(7f * k + f[2] + o * 3f) + 0.1f * Mathf.Sin(17f * k + f[3] + o);
            float bite = Mathf.Max(0f, Mathf.Sin(k + f[4] + o * 1.5f)); // once a tile a deeper bite
            return Fine * (d + 2.8f * Mathf.Pow(bite, 16f));
        }
        // inside the paper: how far from its edge (texture px; negative: outside)
        private static float PaperDepth(float x, float y, int n, bool torn, int v)
        {
            float b = TornBorder * Fine;
            float d = Mathf.Min(Mathf.Min(x - TornIn(y - b, 0, torn, v), (n - x) - TornIn(y - b, 1, torn, v)), Mathf.Min(y - TornIn(x - b, 2, torn, v), (n - y) - TornIn(x - b, 3, torn, v)));
            float chip = Fine * (torn ? 9f : 5f), s2 = 0.7071f; // the corners: chipped off (torn) or just softened
            d = Mathf.Min(d, Mathf.Min((x + y - chip) * s2, ((n - x) + y - chip) * s2));
            return Mathf.Min(d, Mathf.Min((x + (n - y) - chip) * s2, ((n - x) + (n - y) - chip) * s2));
        }
        private static Sprite PaperSprite(string key, bool torn, float line, int v)
        {
            int n = Mathf.RoundToInt((TornBorder * 2f + TornTile) * Fine);
            float b = TornBorder * Fine, w = line * Fine;
            return Make(key, n, n, (x, y) =>
            {
                float d = PaperDepth(x, y, n, torn, v);
                float a = Mathf.Clamp01((d + 1f) / 2f);
                if (line > 0f) return new Color(1f, 1f, 1f, a * Mathf.Clamp01((w - d + 1f) / 2f));
                float e = Mathf.Clamp01(d / (Fine * (torn ? 6f : 4f)));
                e = e * e * (3f - 2f * e);
                float grain = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.05f;
                float k0 = torn ? 0.35f : 0.8f; // (the straight sheet: only a breath darker at its edge)
                Color c = Color.Lerp(new Color(0.84f, 0.72f, 0.55f), Color.white, k0 + (1f - k0) * e);
                return new Color(c.r + grain, c.g + grain, c.b + grain * 1.3f, a);
            }, new Vector4(b, b, b, b), true);
        }
        internal const int TornVariants = 6; // cards side by side gnawed each its own way (the user: as in the designs)
        internal static Sprite TornFill(int v) { v = (v & 0x7fffffff) % TornVariants; return PaperSprite("torn:" + v, true, 0f, v); }
        // its frame: thin (1) or thick (2) only — each width × variant is a texture of its own (~0.66 MB), so the widths
        // asked (1 … 2.5) come to these two
        internal static Sprite TornLine(float width, int v) { v = (v & 0x7fffffff) % TornVariants; float w = width < 1.25f ? 1f : 2f; return PaperSprite("tornline:" + w + ":" + v, true, w, v); }
        internal static Sprite SheetFill { get { return PaperSprite("sheet", false, 0f, 0); } }

        // a small padlock (a shut gift): its body and the shackle over it, white (tinted where it is used)
        internal static Sprite Lock
        {
            get
            {
                return Make("lock", 64, 64, (x, y) =>
                {
                    float body = BoxDistance(x - 10f, y - 4f, 44f, 32f, 5f, false); // (y up: the body low)
                    float d = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 36f) * (y - 36f));
                    float shackle = y >= 34f ? Mathf.Abs(d - 13f) - 3.5f : (Mathf.Abs(Mathf.Abs(x - 32f) - 13f) - 3.5f);
                    if (y < 30f) shackle = 99f;
                    float a = Mathf.Clamp01(0.5f - Mathf.Min(body, shackle));
                    float hole = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 22f) * (y - 22f)) - 4f; // the keyhole
                    if (body < 0f) a *= Mathf.Clamp01(hole + 0.5f);
                    return new Color(1f, 1f, 1f, a);
                }, Vector4.zero, true);
            }
        }

        // a photo corner holding a pinned sheet: 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left (its right angle
        // in that corner), a lighter fold line along its long side
        internal const float CornerSize = 22f;
        internal static Sprite PhotoCorner(int corner)
        {
            int n = Mathf.RoundToInt(CornerSize * Fine);
            Color dark = C("#6a4a26"), mid = C("#a07a48"), lit = C("#c9a36a");
            return Make("photocorner:" + corner, n, n, (x, y) =>
            {
                float u = corner == 0 || corner == 3 ? x : n - x, v = corner == 0 || corner == 1 ? n - y : y; // from the right angle
                float s = n - (u + v); // >0 inside the triangle, distance to the long side ×√2
                float a = Mathf.Clamp01((s + 1f) / 2f) * Mathf.Clamp01((Mathf.Min(u, v) + 1f) / 2f);
                Color c = Color.Lerp(mid, dark, Mathf.Clamp01(1f - Mathf.Min(u, v) / (Fine * 2.5f)) * 0.6f); // darker by the sheet's own edges
                c = Color.Lerp(c, lit, Mathf.Clamp01(1f - Mathf.Abs(s - Fine * 2.2f) / (Fine * 1.2f)) * 0.8f); // the fold catching the light
                c.a = a * 0.96f;
                return c;
            }, Vector4.zero, true);
        }

        // a game icon (from an atlas, no mipmaps) copied once into its own mipmapped texture: shrunk on the tilted page it
        // stays smooth instead of stepping into ladders; null or a failed copy: the icon as it is
        private static readonly Dictionary<Sprite, Sprite> Smoothed = new Dictionary<Sprite, Sprite>();
        internal static Sprite Smooth(Sprite s)
        {
            if (s == null || s.texture == null || (s.packed && s.packingRotation != SpritePackingRotation.None)) return s; // a turned atlas piece: as it is
            Sprite done;
            if (Smoothed.TryGetValue(s, out done) && done != null) return done;
            RenderTexture rt = null, was = RenderTexture.active;
            try
            {
                Texture tex = s.texture;
                Rect r = s.textureRect;
                int w = Mathf.Max(1, Mathf.RoundToInt(r.width)), h = Mathf.Max(1, Mathf.RoundToInt(r.height));
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
                RenderTexture.active = rt;
                Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
                t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear; t.anisoLevel = 4;
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                t.Apply(true, true);
                done = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                done.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception e) { Log("smooth icon " + s.name + ": " + e.Message); done = s; }
            finally
            {
                RenderTexture.active = was;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
            if (Smoothed.Count >= 128 && Smoothed.Count % 64 == 0) // the game's icons made anew (another world): drop the copies of the gone ones
            {
                List<Sprite> dead = new List<Sprite>();
                foreach (KeyValuePair<Sprite, Sprite> kv in Smoothed) if (kv.Key == null) dead.Add(kv.Key);
                foreach (Sprite k in dead)
                {
                    Sprite c = Smoothed[k];
                    Smoothed.Remove(k);
                    if (c != null && c.texture != null) UnityEngine.Object.Destroy(c.texture);
                    if (c != null) UnityEngine.Object.Destroy(c);
                }
            }
            Smoothed[s] = done;
            return done;
        }

        // an icon out of focus (a boss not beaten yet: you know there is one, not what it looks like): shrunk to a few
        // pixels and drawn back up smooth, twice, so its shape melts into soft colour
        private static readonly Dictionary<Sprite, Sprite> BlurredOf = new Dictionary<Sprite, Sprite>();
        internal static Sprite Blurred(Sprite s)
        {
            Sprite sharp = s; // (the book's icons come smoothed already: ItemIcon)
            if (sharp == null || sharp.texture == null) return s;
            Sprite done;
            if (BlurredOf.TryGetValue(sharp, out done) && done != null) return done;
            RenderTexture a = null, b = null, c = null, was = RenderTexture.active;
            try
            {
                Texture tex = sharp.texture;
                Rect r = sharp.textureRect;
                a = RenderTexture.GetTemporary(24, 24, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                b = RenderTexture.GetTemporary(12, 12, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                c = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                a.filterMode = b.filterMode = c.filterMode = FilterMode.Bilinear;
                Graphics.Blit(tex, a, new Vector2(r.width / tex.width, r.height / tex.height), new Vector2(r.x / tex.width, r.y / tex.height));
                Graphics.Blit(a, b);
                Graphics.Blit(b, c);
                RenderTexture.active = c;
                Texture2D t = new Texture2D(64, 64, TextureFormat.RGBA32, true, false);
                t.hideFlags = HideFlags.HideAndDontSave; t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Trilinear;
                t.ReadPixels(new Rect(0, 0, 64, 64), 0, 0, false);
                t.Apply(true, true);
                done = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                done.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception e) { Log("blur icon " + s.name + ": " + e.Message); done = sharp; }
            finally
            {
                RenderTexture.active = was;
                if (a != null) RenderTexture.ReleaseTemporary(a);
                if (b != null) RenderTexture.ReleaseTemporary(b);
                if (c != null) RenderTexture.ReleaseTemporary(c);
            }
            if (BlurredOf.Count >= 32) // (icons made anew in another world: their old blurred copies go)
            {
                List<Sprite> dead = new List<Sprite>();
                foreach (KeyValuePair<Sprite, Sprite> kv in BlurredOf) if (kv.Key == null) dead.Add(kv.Key);
                foreach (Sprite k in dead)
                {
                    Sprite c0 = BlurredOf[k];
                    BlurredOf.Remove(k);
                    if (c0 != null && c0.texture != null) UnityEngine.Object.Destroy(c0.texture);
                    if (c0 != null) UnityEngine.Object.Destroy(c0);
                }
            }
            BlurredOf[sharp] = done;
            return done;
        }

        internal static Sprite Circle { get { return Make("circle", 128, 128, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(63.5f - Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 64f) * (y - 64f)))), Vector4.zero, true); } }

        // a thin white ring (k: its width as a share of the diameter), for radial fills (the nest's favour arcs)
        internal static Sprite Ring(float k)
        {
            float inner = 255.5f - 512f * k;
            return Make("ring:" + k.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), 512, 512, (x, y) =>
            {
                float d = Mathf.Sqrt((x - 256f) * (x - 256f) + (y - 256f) * (y - 256f));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(255.5f - d) * Mathf.Clamp01(d - inner));
            }, Vector4.zero, true);
        }

        // a soft white glow, strongest in the middle (Odin's light over the nest window)
        internal static Sprite Glow { get { return Make("glow", 128, 128, (x, y) => { float d = Mathf.Clamp01(Mathf.Sqrt((x - 64f) * (x - 64f) + (y - 64f) * (y - 64f)) / 64f); return new Color(1f, 1f, 1f, (1f - d) * (1f - d)); }, Vector4.zero, true); } }

        // a soft round shadow (the seals' drop shadow)
        internal static Sprite SoftDisc { get { return Make("softdisc", 64, 64, (x, y) => { float d = Mathf.Sqrt((x - 32f) * (x - 32f) + (y - 32f) * (y - 32f)) / 32f; return new Color(0f, 0f, 0f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 1.6f); }, Vector4.zero); } }

        // a wax seal: the design's radial-gradient(circle at 38% 34%, light, dark 60%, #14100a) with its inner shade
        internal static Sprite Seal(Color light, Color dark)
        {
            string key = "seal:" + ColorUtility.ToHtmlStringRGBA(light) + ColorUtility.ToHtmlStringRGBA(dark);
            const int n = 192;
            Color edge = C("#14100a");
            float far = Mathf.Sqrt(0.62f * 0.62f + 0.66f * 0.66f) * n; // to the farthest corner, as CSS sizes it
            return Make(key, n, n, (x, y) =>
            {
                float cx = x - n / 2f, cy = y - n / 2f, r = Mathf.Sqrt(cx * cx + cy * cy);
                float gx = x - 0.38f * n, gy = (n - y) - 0.34f * n; // the light from the upper left (CSS y down)
                float t = Mathf.Sqrt(gx * gx + gy * gy) / far;
                Color c = t < 0.6f ? Color.Lerp(light, dark, t / 0.6f) : Color.Lerp(dark, edge, (t - 0.6f) / 0.4f);
                float rim = n / 2f - r; // an inner shade 5 of 86 deep
                if (rim < n * 5f / 86f) c = Color.Lerp(c, Color.black, 0.18f);
                c.a = Mathf.Clamp01(n / 2f - 0.5f - r + 0.5f);
                return c;
            }, Vector4.zero, true);
        }

        // the gear page's round window: radial-gradient(circle at 50% 40%, #2a3442, #121820 70%), shaded inside, with its
        // rings (#3a2414 3, #c8ae7a to 9, #3a2414 to 11) — 330 inside, 352 across
        internal static Sprite Porthole
        {
            get
            {
                const int n = 352;
                Color a = C("#2a3442"), b = C("#121820"), dark = C("#3a2414"), band = C("#c8ae7a");
                return Make("porthole", n, n, (x, y) =>
                {
                    float r = Mathf.Sqrt((x - n / 2f) * (x - n / 2f) + (y - n / 2f) * (y - n / 2f));
                    if (r > 176f) return new Color(0f, 0f, 0f, 0f);
                    Color c;
                    if (r > 165f) c = r > 174f || r < 168f ? dark : band;
                    else
                    {
                        float gx = x - n / 2f, gy = (n - y) - (11f + 330f * 0.4f);
                        c = Color.Lerp(a, b, Mathf.Clamp01(Mathf.Sqrt(gx * gx + gy * gy) / (165f * 1.4f)));
                        float inset = Mathf.Clamp01(1f - (165f - r) / 40f);
                        c = Color.Lerp(c, Color.black, 0.7f * inset * inset);
                    }
                    c.a = Mathf.Clamp01(176.5f - r);
                    return c;
                }, Vector4.zero, true);
            }
        }

        // a dashed line, tiled: across (dashes 6 on 4 off, 1.5 thick) or down
        internal static Sprite Dash(bool across)
        {
            return across
                ? Make("dash-h", 10, 4, (x, y) => new Color(1f, 1f, 1f, x < 6f && y > 1f && y < 3.5f ? 1f : 0f), Vector4.zero, false, TextureWrapMode.Repeat)
                : Make("dash-v", 4, 10, (x, y) => new Color(1f, 1f, 1f, y < 6f && x > 1f && x < 3.5f ? 1f : 0f), Vector4.zero, false, TextureWrapMode.Repeat);
        }

        // a thin line fading in (toward the middle): from nothing on the left (toRight) or on the right
        internal static Sprite Fade(bool toRight)
        {
            return Make(toRight ? "fade-r" : "fade-l", 64, 2, (x, y) => new Color(1f, 1f, 1f, toRight ? x / 64f : 1f - x / 64f), Vector4.zero);
        }

        // a thin line faded at both ends (transparent, full from 15% to 85%, transparent)
        internal static Sprite Fade2 { get { return Make("fade-2", 64, 2, (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(x, 64f - x) / 9.6f)), Vector4.zero); } }

        // the satiety's mark (at 15): an inked diamond on top and a thin stroke down through the bar, narrowing as a quill's
        // line does; soft-edged and mipmapped so it stays still on the tilted page (a hard 2-pixel line shimmered there)
        internal static Sprite Marker
        {
            get
            {
                const int w = 32, h = 96;
                return Make("marker", w, h, (x, y) =>
                {
                    float cx = w / 2f, top = h - 4f;          // y up: the diamond at the top
                    float dy = top - 12f - y;                 // from the diamond's middle down
                    float diamond = Mathf.Abs(x - cx) / 11f + Mathf.Abs(y - (top - 12f)) / 12f; // ≤ 1 inside
                    float a = Mathf.Clamp01((1f - diamond) * 9f);
                    if (dy > 0f && y > 2f)
                    {
                        float half = Mathf.Lerp(3.2f, 1.2f, Mathf.Clamp01(dy / (top - 14f))); // the stroke narrows downwards
                        a = Mathf.Max(a, Mathf.Clamp01(half - Mathf.Abs(x - cx) + 0.5f));
                    }
                    return new Color(1f, 1f, 1f, a);
                }, Vector4.zero, true);
            }
        }

        // an upright stripe (a card's colour edge): a clear pixel at each side so its edges stay soft on the tilted page
        // (a rounded box squeezed to 3 units overlapped its own corners and looked ragged), round at both ends; nine-sliced
        // along its length only
        internal static Sprite Stripe
        {
            get
            {
                const int w = 8, h = 16;
                return Make("stripe", w, h, (x, y) =>
                {
                    float across = Mathf.Clamp01(Mathf.Min(x - 1f, w - 1f - x)); // 0 at the clear sides, 1 inside
                    float ends = Mathf.Clamp01(Mathf.Min(y, h - y) / 2f);        // the round ends
                    return new Color(1f, 1f, 1f, across * ends);
                }, new Vector4(0f, 4f, 0f, 4f), true);
            }
        }

        // a check and a cross, strokes on 64
        internal static Sprite Check { get { return Strokes("check", new float[] { 12f, 34f, 26f, 18f, 26f, 18f, 52f, 48f }); } }
        internal static Sprite Cross { get { return Strokes("cross", new float[] { 16f, 16f, 48f, 48f, 16f, 48f, 48f, 16f }); } }

        private static Sprite Strokes(string key, float[] s)
        {
            return Make(key, 64, 64, (x, y) =>
            {
                float d = 1e9f;
                for (int i = 0; i + 3 < s.Length; i += 4) d = Mathf.Min(d, Segment(x, y, s[i], s[i + 1], s[i + 2], s[i + 3]));
                return new Color(1f, 1f, 1f, Mathf.Clamp01(4.5f - d));
            }, Vector4.zero, true);
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float k = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float dx = px - (ax + vx * k), dy = py - (ay + vy * k);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Book of ravens " + s); }
    }

    // A line of text sized to its room: the largest size up to Max at which it fits (down to Min, then «…»). Its room is
    // the layout's (Shrink gives it no width of its own), so the size never feeds back into the width; looked at again only
    // when the words or the room change.
    internal sealed class BookFit : MonoBehaviour
    {
        internal TMP_Text Text;
        internal float Max, Min;
        private string _words;
        private float _room = -1f;

        private void LateUpdate()
        {
            if (Text == null) return;
            float room = ((RectTransform)Text.transform).rect.width;
            string words = Text.text;
            if (words == _words && Mathf.Abs(room - _room) < 0.5f) return;
            _words = words; _room = room;
            if (room <= 1f) return;
            Text.fontSize = Max;
            float need = Text.GetPreferredValues(words, float.PositiveInfinity, float.PositiveInfinity).x;
            float size = need > room ? Mathf.Max(Min, Mathf.Floor(Max * room / need * 4f) / 4f) : Max;
            if (Mathf.Abs(Text.fontSize - size) > 0.01f) Text.fontSize = size;
        }
    }

    // A page's column scrolls only when its content is taller than the page; otherwise the scroll is off and the content
    // held exactly at the top (a scroll left on nudged the whole column a hair whenever a number changed)
    internal sealed class BookScrollGate : MonoBehaviour
    {
        internal UnityEngine.UI.ScrollRect Scroll;

        private void LateUpdate()
        {
            if (Scroll == null || Scroll.content == null || Scroll.viewport == null) return;
            bool needed = Scroll.content.rect.height > Scroll.viewport.rect.height + 1f;
            if (Scroll.enabled != needed) Scroll.enabled = needed;
            if (!needed && Scroll.content.anchoredPosition != Vector2.zero) Scroll.content.anchoredPosition = Vector2.zero;
        }
    }

    // A brush-stroke bar: a faint line along the bottom and over it the stroke, as long as the value. It paints itself
    // in when the page opens (from nothing to the value) and follows the value as it changes, smoothly.
    internal sealed class BookBar : MonoBehaviour
    {
        internal Func<float> Value;       // 0..1
        internal Func<Sprite> Stroke;     // which stroke now (the satiety's changes with its zone); null: the one it has
        internal RectTransform Fill;
        internal Image FillImage;
        internal string Key;              // the same bar on a page drawn anew goes on from where it was (Last), not from nothing
        internal bool AtOnce;             // no paint-in: it starts at its value (a page reached by another tab)
        internal static readonly Dictionary<string, float> Last = new Dictionary<string, float>(); // cleared when a page is opened
        internal static readonly Dictionary<string, float> LastWidth = new Dictionary<string, float>(); // the stroke's width then: a bar drawn anew starts at it, not at nothing for a frame

        // a bar drawn anew over an old one: the old one's stroke at once, before the first Update and the layout
        internal void Resume()
        {
            float w;
            if (Key == null || Fill == null || !LastWidth.TryGetValue(Key, out w)) return;
            Fill.sizeDelta = new Vector2(w, Fill.sizeDelta.y);
            if (FillImage != null) FillImage.enabled = w > 0f;
        }
        private float _shown, _born = -1f, _from, _target, _readAt = -1f;
        private bool _started;
        private Sprite _stroke;
        private const float PaintSeconds = 0.7f, ReadEvery = 0.2f; // the value (and the stroke) read five times a second, drawn every frame

        private void OnEnable() { _started = false; _readAt = -1f; }

        private void Update() { Paint(); }

        // the value and the stroke's picture: not every frame (they read the raven's data); the stroke moves every frame
        private void Read()
        {
            if (_readAt >= 0f && Time.unscaledTime - _readAt < ReadEvery) return;
            _readAt = Time.unscaledTime;
            try { _target = Mathf.Clamp01(Value()); } catch { _target = 0f; }
            if (Stroke != null) { try { _stroke = Stroke(); } catch { _stroke = null; } }
        }

        private void Paint()
        {
            if (Fill == null || Value == null) return;
            Read();
            float target = _target;
            if (!_started)
            {
                _started = true;
                _born = Time.unscaledTime;
                float was;
                _from = Key != null && Last.TryGetValue(Key, out was) ? was : AtOnce ? target : 0f;
            }
            float age = Time.unscaledTime - _born;
            if (age < PaintSeconds)
            {
                float k = age / PaintSeconds;
                _shown = Mathf.Lerp(_from, target, 1f - (1f - k) * (1f - k) * (1f - k)); // painted in, slowing at the end
            }
            else _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * Mathf.Max(0.25f, Mathf.Abs(target - _shown) * 4f));
            if (Key != null) Last[Key] = _shown;
            float room = ((RectTransform)transform).rect.width;
            if (room <= 1f) return; // not laid out yet: keep what Resume put there
            float full = room + 4f;
            float w = _shown * full;
            bool on = _shown > 0.004f;
            if (FillImage != null && FillImage.enabled != on) FillImage.enabled = on;
            // only when it really moved: setting the same size each frame would dirty the book's canvas each frame
            // in half-unit steps: a slowly sinking value (hunger, strength) does not creep its ragged end a hair each frame
            float want = Mathf.Round(Mathf.Max(w, 28f) * 2f) / 2f; // (at least its two ragged ends)
            if (Mathf.Abs(Fill.sizeDelta.x - want) > 0.25f) Fill.sizeDelta = new Vector2(want, Fill.sizeDelta.y);
            if (Key != null) LastWidth[Key] = on ? want : 0f;
            if (_stroke != null && FillImage != null && FillImage.sprite != _stroke) FillImage.sprite = _stroke;
        }
    }
}
#endif

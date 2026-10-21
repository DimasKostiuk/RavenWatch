#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuginWhispers
{
    // The ravens' eyes glow by trust: an unwoken raven's eyes barely smoulder, then they grow brighter level by level,
    // past the game's own at the top level. The glow of the raven's material covers the whole bird (a faint sheen
    // on the feathers, the eyes brightest), so the material is not dimmed as a whole: the eyes are found on its glow
    // picture as the brightest spots, and a copy of the picture is made with only them changed. The feathers keep
    // the game's own glow. Only our copies on the shoulders change (a material copy); the visiting ravens keep theirs.
    internal static class EyeGlow
    {
        // percent of the game's eye glow for trust level 0 (not woken), 1, 2 ... 7; past the end: the last one
        internal static float[] ByLevel = { 5f, 30f, 30f, 47.5f, 65f, 82.5f, 100f, 130f };
        internal const string DefaultList = "5, 30, 30, 47.5, 65, 82.5, 100, 130";
        internal static float Override = -1f; // raven eyes <percent>: the same for every raven, for trying
        internal static int Version = 1;      // bumped on every change: the ravens apply it again

        private const int MaxSide = 512;                      // the copies are kept this small at most: a shoulder raven is tiny
        private const float EyeFrom = 0.45f, EyeFull = 0.6f; // brightness, against the brightest spot, where the eyes begin / are surely eyes
        private const float MostEyes = 0.02f;                 // when this share of the whole picture counts as "eyes", there are no eyes to find (real eyes: under 0.1%)
        private static readonly Dictionary<Shader, string[]> Props = new Dictionary<Shader, string[]>();
        private static readonly Dictionary<Shader, string[]> TexProps = new Dictionary<Shader, string[]>();
        private static readonly Dictionary<Texture, Color32[]> Read = new Dictionary<Texture, Color32[]>(); // the glow picture, read once
        private static readonly Dictionary<Texture, Vector2Int> ReadSize = new Dictionary<Texture, Vector2Int>();
        private static readonly HashSet<Texture> NoEyes = new HashSet<Texture>();
        private static readonly Dictionary<string, Texture2D> Made = new Dictionary<string, Texture2D>(); // picture + share
        internal static string Last = "ще не пробував"; // what the last look at the glow found, for raven eyes

        internal static void SetList(string list)
        {
            List<float> l = new List<float>();
            foreach (string s in (list ?? "").Split(','))
            {
                float v;
                if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) l.Add(Mathf.Max(0f, v));
            }
            if (l.Count > 0) ByLevel = l.ToArray();
            Version++;
        }

        internal static float Share(int level)
        {
            if (Override >= 0f) return Override / 100f;
            if (ByLevel.Length == 0) return 1f;
            return ByLevel[Mathf.Clamp(level, 0, ByLevel.Length - 1)] / 100f;
        }

        // The eyes of a shoulder raven, for its level: its material is swapped for a copy with the eyes changed.
        // Not a property block: the game keeps its own property block on the raven (its material manager), and a block
        // of ours for one material replaced the game's whole block for it, so the raven lost part of its own look
        // (it came out paler, then greenish). The copies are shared by all ravens of a level and kept.
        private static readonly Dictionary<Material, Material> OriginalOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<string, Material> Copies = new Dictionary<string, Material>();

        // the game's own material behind one of our eye copies (any other: itself)
        internal static Material Original(Material m)
        {
            Material o;
            return m != null && OriginalOf.TryGetValue(m, out o) ? o : m;
        }

        // a material of ours is gone (a raven look freed): its eye copies go too
        internal static void Release(Material m)
        {
            if (m == null) return;
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, Material> kv in Copies)
            {
                Material o;
                if (kv.Value == null || (OriginalOf.TryGetValue(kv.Value, out o) && o == m)) keys.Add(kv.Key);
            }
            foreach (string k in keys)
            {
                Material c = Copies[k];
                Copies.Remove(k);
                if (c != null) { OriginalOf.Remove(c); Object.Destroy(c); }
            }
        }

        // and a glow picture of ours: the eye pictures made from it
        internal static void Release(Texture t)
        {
            if (t == null) return;
            string at = t.GetInstanceID() + ":";
            List<string> keys = new List<string>();
            foreach (string k in Made.Keys) if (k.StartsWith(at)) keys.Add(k);
            foreach (string k in keys) { if (Made[k] != null) Object.Destroy(Made[k]); Made.Remove(k); }
            Read.Remove(t); ReadSize.Remove(t); NoEyes.Remove(t);
        }

        internal static void Apply(GameObject go, int level)
        {
            if (go == null) return;
            float share = Share(level);
            bool same = Mathf.Abs(share - 1f) < 0.001f;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r.GetComponentInParent<FxOn>() != null || r.GetComponentInParent<Outfits.Piece>() != null || r.GetComponentInParent<RavenFetch.Carried>() != null) continue; // not what it wears or holds
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material cur = mats[i];
                    if (cur == null) continue;
                    Material orig;
                    if (!OriginalOf.TryGetValue(cur, out orig)) orig = cur;
                    Material want = same ? orig : CopyFor(orig, share);
                    if (want != cur) { mats[i] = want; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // the game's material with the eyes at this share, or the material itself when they cannot be told apart
        private static Material CopyFor(Material m, float share)
        {
            if (m.shader == null) return m;
            string[] cols = EmissionProps(m.shader);
            if (cols.Length == 0) return m;
            string tex = null;
            Texture src = null;
            foreach (string n in Textures(m.shader)) { src = m.GetTexture(n); if (src != null) { tex = n; break; } }
            if (src == null) { Last = "у матеріалі " + m.name + " світіння без картинки — очі не відділити"; return m; }
            string key = m.GetInstanceID() + ":" + share.ToString("0.###", CultureInfo.InvariantCulture);
            Material done;
            if (Copies.TryGetValue(key, out done) && done != null) return done;
            float boost;
            Texture2D eyes = EyesAt(src, share, out boost);
            if (eyes == null) return m;
            Material copy = new Material(m);
            copy.name = m.name + "_RavenEyes";
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.SetTexture(tex, eyes);
            foreach (string n in cols)
            {
                Color c = m.GetColor(n);
                copy.SetColor(n, new Color(c.r * boost, c.g * boost, c.b * boost, c.a));
            }
            Copies[key] = copy;
            OriginalOf[copy] = m;
            return copy;
        }

        // A copy of the glow picture with the eyes at this share. Above 100% the picture cannot hold more: the feathers
        // are dimmed in it by the boost instead, and the material's glow colour raised by it, so they come out as before.
        private static Texture2D EyesAt(Texture src, float share, out float boost)
        {
            boost = Mathf.Max(1f, share);
            if (NoEyes.Contains(src)) return null;
            string key = src.GetInstanceID() + ":" + share.ToString("0.###", CultureInfo.InvariantCulture);
            Texture2D done;
            if (Made.TryGetValue(key, out done) && done != null) return done;
            Color32[] px = Pixels(src);
            if (px == null) return null;
            Vector2Int size = ReadSize[src];
            float max = 0f;
            for (int i = 0; i < px.Length; i++) { float l = Lum(px[i]); if (l > max) max = l; }
            int lit = 0, eyes = 0;
            for (int i = 0; i < px.Length; i++) { float l = Lum(px[i]); if (l > 0.02f) lit++; if (l >= max * EyeFull) eyes++; }
            if (max < 0.05f || lit == 0 || eyes > px.Length * MostEyes)
            {
                NoEyes.Add(src);
                Last = "на картинці світіння " + src.name + " очі не виділяються (найяскравіше " + max.ToString("0.##", CultureInfo.InvariantCulture)
                    + ", «очей» " + eyes + " з " + lit + " світних точок) — ворон лишається рідним";
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven eye glow: " + Last);
                return null;
            }
            Color32[] o = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                float w = Mathf.InverseLerp(max * EyeFrom, max * EyeFull, Lum(px[i])); // 0 feathers, 1 eyes, soft between
                float k = Mathf.Lerp(1f, share, w) / boost;
                Color32 c = px[i];
                o[i] = new Color32(B(c.r * k), B(c.g * k), B(c.b * k), c.a);
            }
            // made just like the game's picture (colour space, smaller copies for far away, filtering): anything else
            // changes the faint glow of the feathers too
            bool mips = src.mipmapCount > 1;
            Texture2D t = new Texture2D(size.x, size.y, TextureFormat.RGBA32, mips, !Srgb(src));
            t.name = src.name + "_RavenEyes";
            t.hideFlags = HideFlags.HideAndDontSave;
            t.wrapMode = src.wrapMode;
            t.filterMode = src.filterMode;
            t.anisoLevel = src.anisoLevel;
            t.mipMapBias = src.mipMapBias;
            t.SetPixels32(o);
            t.Apply(mips, true); // kept on the card only
            Made[key] = t;
            Read.Remove(src); // the copy is made: the pixels can go (read again if another share is wanted)
            Last = "очі знайдено на " + src.name + ": " + eyes + " з " + lit + " світних точок, картинка " + size.x + "×" + size.y
                + " (" + src.graphicsFormat + ", " + (Srgb(src) ? "sRGB" : "лінійна") + ", рівнів " + src.mipmapCount + ", " + src.filterMode + ", гра в " + QualitySettings.activeColorSpace + ")";
            if (Plugin.Instance != null) Plugin.Instance.Log("Raven eye glow: " + Last + ", share " + share.ToString("0.###", CultureInfo.InvariantCulture));
            return t;
        }

        // the glow picture's pixels, read once from the card (the game's pictures cannot be read directly)
        private static Color32[] Pixels(Texture src)
        {
            Color32[] px;
            if (Read.TryGetValue(src, out px)) return px;
            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;
            try
            {
                float scale = Mathf.Min(1f, (float)MaxSide / Mathf.Max(src.width, src.height));
                int w = Mathf.Max(4, Mathf.RoundToInt(src.width * scale)), h = Mathf.Max(4, Mathf.RoundToInt(src.height * scale));
                bool srgb = Srgb(src); // read as it is stored, no colour conversion on the way
                rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D read = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                read.Apply();
                px = read.GetPixels32();
                Object.Destroy(read);
                ReadSize[src] = new Vector2Int(w, h);
            }
            catch (System.Exception e)
            {
                px = null;
                NoEyes.Add(src);
                Last = "не вдалося прочитати картинку світіння: " + e.Message;
                if (Plugin.Instance != null) Plugin.Instance.Log("Raven eye glow: " + Last);
            }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
            Read[src] = px;
            return px;
        }

        private static byte B(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255); }

        private static bool Srgb(Texture t)
        {
            try { return UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat); }
            catch (System.Exception) { return true; }
        }

        private static float Lum(Color32 c) { return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f; }

        // the colour properties of a shader that carry the glow ("_EmissionColor" and the like)
        private static string[] EmissionProps(Shader sh) { return Find(sh, Props, ShaderPropertyType.Color); }

        // and its glow pictures ("_EmissionMap" and the like)
        private static string[] Textures(Shader sh) { return Find(sh, TexProps, ShaderPropertyType.Texture); }

        private static string[] Find(Shader sh, Dictionary<Shader, string[]> cache, ShaderPropertyType type)
        {
            string[] names;
            if (cache.TryGetValue(sh, out names)) return names;
            List<string> l = new List<string>();
            for (int i = 0; i < sh.GetPropertyCount(); i++)
            {
                if (sh.GetPropertyType(i) != type) continue;
                string n = sh.GetPropertyName(i);
                if (n.ToLowerInvariant().Contains("emis")) l.Add(n);
            }
            names = l.ToArray();
            cache[sh] = names;
            return names;
        }

        // raven eyes: what the raven's materials are made of, in the log; a short answer for the console
        internal static string Describe()
        {
            GameObject go = Shoulder.LocalModel(false);
            if (go == null) go = Shoulder.LocalModel(true);
            Raven t = Shoulder.Template(false);
            if (go == null && t != null) go = t.m_visual;
            if (go == null) return "Не знайшов модель ворона: надягни ворона або підійди до Хугіна.";
            StringBuilder log = new StringBuilder("Raven materials (" + go.name + "):");
            List<string> found = new List<string>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    Shader sh = m.shader;
                    log.Append("\n  ").Append(r.name).Append(" / ").Append(m.name).Append(" / shader ").Append(sh != null ? sh.name : "none");
                    log.Append("\n    keywords: ").Append(string.Join(" ", m.shaderKeywords));
                    if (sh == null) continue;
                    for (int i = 0; i < sh.GetPropertyCount(); i++)
                    {
                        string n = sh.GetPropertyName(i);
                        ShaderPropertyType type = sh.GetPropertyType(i);
                        log.Append("\n    ").Append(n).Append(" (").Append(type).Append(")");
                        if (type == ShaderPropertyType.Color) log.Append(" = ").Append(m.GetColor(n));
                        else if (type == ShaderPropertyType.Float || type == ShaderPropertyType.Range) log.Append(" = ").Append(m.GetFloat(n).ToString(CultureInfo.InvariantCulture));
                        else if (type == ShaderPropertyType.Texture) { Texture tx = m.GetTexture(n); log.Append(" = ").Append(tx != null ? tx.name + " " + tx.width + "x" + tx.height : "none"); }
                    }
                    foreach (string n in EmissionProps(sh))
                    {
                        string s = m.name + ": " + n + " " + m.GetColor(n);
                        if (!found.Contains(s)) found.Add(s);
                    }
                }
            }
            if (Plugin.Instance != null) Plugin.Instance.Log(log.ToString() + "\n  eye glow: " + Last);
            string now = Override >= 0f ? "зараз очі всім " + Override.ToString("0.#", CultureInfo.InvariantCulture) + "% (raven eyes off — назад до рівнів)" : "зараз за рівнями: " + Levels();
            return found.Count == 0
                ? "Параметра світіння в матеріалі ворона не знайшов. Усе про матеріали — у лозі BepInEx (Raven materials)."
                : "Світіння: " + string.Join("; ", found.ToArray()) + ". Очі: " + Last + ". " + now + ". Подробиці — у лозі BepInEx (Raven materials).";
        }

        internal static string Levels()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ByLevel.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(i == 0 ? "сплячий " : i + ": ").Append(ByLevel[i].ToString("0.#", CultureInfo.InvariantCulture)).Append('%');
            }
            return sb.ToString();
        }
    }
}
#endif

#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The pictures of the gifts, of the book and of the Ravens group in the radial menu (main): built into the mod (assets/icons/<id>.png), and a picture of the
    // same name in config/RavenWatch/icons/ wins over it. Shown in the radial menu, the book and on the effects.
    internal static class AbilityIcons
    {
        private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), "LoadImage", new Type[] { typeof(Texture2D), typeof(byte[]) });
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // the gift's picture, or the raven's own when there is none
        internal static Sprite For(string id, bool munin)
        {
            Sprite s = Get(id);
            return s != null ? s : RavenItem.Icon(munin);
        }

        // the effects bar shows the whole round medallion, like the radial menu and the book
        internal static Sprite ForEffect(string id, bool munin) { return For(id, munin); }

        internal static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Sprite s;
            if (Cache.TryGetValue(id, out s)) return s;
            s = null;
            try
            {
                byte[] data = null;
                string own = Path.Combine(RavenItem.IconDir, id + ".png");
                if (File.Exists(own)) data = File.ReadAllBytes(own);
                else
                    using (Stream st = typeof(AbilityIcons).Assembly.GetManifestResourceStream("RavenWatch.icons." + id + ".png"))
                        if (st != null) { data = new byte[st.Length]; int read = 0; while (read < data.Length) { int n = st.Read(data, read, data.Length - read); if (n <= 0) break; read += n; } }
                if (data != null && LoadImage != null)
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    if ((bool)LoadImage.Invoke(null, new object[] { tex, data }))
                    {
                        tex.filterMode = FilterMode.Trilinear; // drawn small: mipmaps keep it smooth
                        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        s.name = "RavenWatch_" + id;
                        s.hideFlags = HideFlags.HideAndDontSave; // kept in the cache: the game's asset cleanup on world load must not take it
                    }
                }
            }
            catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Gift icon " + id + " failed: " + e.Message); }
            Cache[id] = s;
            return s;
        }

        // a picture from bytes (PNG) into a texture; false when the game cannot read it
        internal static bool Decode(Texture2D tex, byte[] data)
        {
            if (tex == null || data == null || LoadImage == null) return false;
            tex.hideFlags = HideFlags.HideAndDontSave;
            return (bool)LoadImage.Invoke(null, new object[] { tex, data });
        }

        // the console's "raven icons": pictures put in the folder show up without a restart
        internal static void Reload()
        {
            HashSet<Texture2D> textures = new HashSet<Texture2D>();
            foreach (Sprite s in Cache.Values) if (s != null) { if (s.texture != null) textures.Add(s.texture); UnityEngine.Object.Destroy(s); }
            foreach (Texture2D t in textures) UnityEngine.Object.Destroy(t);
            Cache.Clear();
        }
    }
}
#endif

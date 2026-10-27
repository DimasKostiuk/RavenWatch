#if RAVEN_ITEM
using System.Collections;
using UnityEngine;

namespace HuginWhispers
{
    // `photo <id> [yaw] [pitch] [zoom]`: a Workshop model's icon taken as the tab «Фото» takes it (its saved settings,
    // or the standard ones, with these turns), saved as its photo at once, and a copy in out/photo_<id>.png to look at.
    internal static partial class Studio
    {
        private static IEnumerator Photo(string id, float yaw, float pitch, float zoom)
        {
            WsModel m = null;
            foreach (string f in WsFiles.List())
            {
                WsModel x = WsFiles.Load(f);
                if (x != null && IconPhotos.KeyOf(x) == id) { m = x; break; }
            }
            if (m == null) { Note("photo_" + id, "no Workshop model with this id"); yield break; }
            int layer = RavenFx.FreeLayer();
            if (layer < 0) { Note("photo_" + id, "no free layer"); yield break; }
            PhotoStudio studio = new PhotoStudio("RavenWatch_StudioPhoto", new Vector3(0f, 9800f, 0f), layer);
            WsView view = new WsView(studio.Subject, layer, false);
            try
            {
                view.Sync(m);
                studio.Prepare();
                yield return null; yield return null; // (the parts' looks settle, as in the tab)
                studio.Prepare();
                if (!studio.Measure()) { Note("photo_" + id, "nothing seen"); yield break; }
                PhotoSet s = IconPhotos.SettingsOf(id);
                if (!float.IsNaN(yaw)) s.Yaw = yaw;
                if (!float.IsNaN(pitch)) s.Pitch = pitch;
                if (!float.IsNaN(zoom)) s.Zoom = zoom;
                int solid;
                Color32[] px = studio.Clean(s, out solid);
                Texture2D icon = IconPhotos.Finish(px, s.Trim);
                bool ok = IconPhotos.Save(id, icon, s, m.Name);
                Save("photo_" + id, icon);
                Object.Destroy(icon);
                Note("photo_" + id, (ok ? "saved" : "NOT saved") + ", solid " + solid + " px, " + s.ToLine());
            }
            finally
            {
                view.Clear();
                studio.Dispose();
            }
        }
    }
}
#endif

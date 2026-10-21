#if RAVEN_ITEM
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The errands' marks on the map in their own colour — a cold blue, Odin's — not the game's red of raids and events:
    // the game's own pictures (the event's «!», its ring) made grey once, so a colour can be laid on them; the map gives
    // every mark white on each redraw, so ours are coloured again right after it (UpdatePins).
    internal static class ErrandMap
    {
        internal static readonly Color Mark = new Color(0.5f, 0.75f, 1f, 1f);     // the «!» and the name
        internal static readonly Color Ring = new Color(0.45f, 0.7f, 1f, 0.85f);  // the zone's ring
        private static readonly Dictionary<Sprite, Sprite> Grey = new Dictionary<Sprite, Sprite>();
        internal static readonly List<Minimap.PinData> Ours = new List<Minimap.PinData>();
        internal static readonly List<Minimap.PinData> Trees = new List<Minimap.PinData>(); // the raven trees' marks (RavenTree.MapTick), in their gold

        // a grey copy of a map picture (once per picture); the picture itself when it cannot be read
        internal static Sprite GreyOf(Sprite s)
        {
            if (s == null) return null;
            Sprite g;
            if (Grey.TryGetValue(s, out g)) return g != null ? g : s;
            g = null;
            RenderTexture rt = null, prev = RenderTexture.active;
            try
            {
                Texture src = s.texture;
                Rect r = s.textureRect;
                rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D t = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                Color32[] px = t.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    Color32 c = px[i];
                    byte v = (byte)Mathf.Clamp(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 0, 255); // brightest channel: the red «!» stays bright
                    px[i] = new Color32(v, v, v, c.a);
                }
                t.SetPixels32(px);
                t.Apply(false, true);
                t.hideFlags = HideFlags.HideAndDontSave;
                g = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
                g.name = s.name + " (RavenWatch)";
                g.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (System.Exception e) { Errands.Log("map mark picture: " + e.Message); g = null; }
            finally
            {
                RenderTexture.active = prev;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
            Grey[s] = g;
            return g != null ? g : s;
        }

        // after the map's redraw: ours in our colour
        internal static void Tint()
        {
            for (int i = Trees.Count - 1; i >= 0; i--)
            {
                Minimap.PinData p = Trees[i];
                if (p == null) { Trees.RemoveAt(i); continue; }
                if (p.m_iconElement != null && p.m_iconElement.color != RavenTree.PinColor) p.m_iconElement.color = RavenTree.PinColor;
                if (p.m_NamePinData != null && p.m_NamePinData.PinNameText != null && p.m_NamePinData.PinNameText.color != RavenTree.PinColor) p.m_NamePinData.PinNameText.color = RavenTree.PinColor;
            }
            for (int i = Ours.Count - 1; i >= 0; i--)
            {
                Minimap.PinData p = Ours[i];
                if (p == null) { Ours.RemoveAt(i); continue; }
                Color c = p.m_type == Minimap.PinType.EventArea ? Ring : Mark;
                if (p.m_iconElement != null && p.m_iconElement.color != c) p.m_iconElement.color = c;
                if (p.m_NamePinData != null && p.m_NamePinData.PinNameText != null && p.m_NamePinData.PinNameText.color != c) p.m_NamePinData.PinNameText.color = c;
            }
        }

        // for testing (admins while tuning): you, at once, in the middle of the errand's zone
        internal static void Teleport(Player p, Vector3 at)
        {
            if (p == null) return;
            Remember(p);
            if (RavenBook.IsOpen) RavenBook.Close();
            // (a far place is not loaded yet: its height from the world generator, as the map knows it)
            float y = WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(at.x, at.z) : at.y;
            if (ZoneSystem.instance != null) y = Mathf.Max(y, ZoneSystem.instance.m_waterLevel);
            p.TeleportTo(new Vector3(at.x, y + 2f, at.z), p.transform.rotation, true);
            Errands.Log("teleported to the zone at " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + " (test)");
        }

        // ---------------------------------------------------------------- back where you were (testing)
        // every test teleport remembers where it took you from (player customData, so it lives through leaving the game)
        internal const string BackKey = "RavenWatch_tpBack";

        private static void Remember(Player p)
        {
            Vector3 v = p.transform.position;
            p.m_customData[BackKey] = Num(v.x) + "/" + Num(v.y) + "/" + Num(v.z);
        }

        private static string Num(float f) { return f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }

        // raven back: to where the last test teleport took you from (and that place is remembered in turn: back again)
        internal static string Back(Player p)
        {
            string raw;
            if (p == null || !p.m_customData.TryGetValue(BackKey, out raw)) return "Нема куди повертатися: ще не було тестового телепорту.";
            string[] w = raw.Split('/');
            if (w.Length != 3) return "Місце повернення зіпсоване: " + raw;
            Vector3 at = new Vector3(Errands.F(w[0], 0f), Errands.F(w[1], 0f), Errands.F(w[2], 0f));
            Remember(p);
            if (RavenBook.IsOpen) RavenBook.Close();
            p.TeleportTo(at + Vector3.up * 0.5f, p.transform.rotation, true);
            Errands.Log("teleported back to " + raw + " (test)");
            return "Назад: " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + ". Ще раз raven back — знову туди, звідки щойно.";
        }

        // raven home: to your bed (or the world's start, when you have no bed)
        internal static string Home(Player p)
        {
            if (p == null || Game.instance == null) return "Нема гравця.";
            PlayerProfile prof = Game.instance.GetPlayerProfile();
            bool bed = prof.HaveCustomSpawnPoint();
            Vector3 at = bed ? prof.GetCustomSpawnPoint() : prof.GetHomePoint();
            Remember(p);
            if (RavenBook.IsOpen) RavenBook.Close();
            p.TeleportTo(at + Vector3.up * 0.5f, p.transform.rotation, true);
            Errands.Log("teleported home (" + (bed ? "bed" : "world start") + ") (test)");
            return (bed ? "До ліжка" : "Ліжка нема — до початку світу") + ": " + Mathf.RoundToInt(at.x) + " " + Mathf.RoundToInt(at.z) + ". raven back — назад.";
        }

        // «Відстежити»: the big map opens on this place
        internal static void Show(Vector3 at)
        {
            if (Minimap.instance == null) return;
            if (RavenBook.IsOpen) RavenBook.Close();
            Minimap.instance.ShowPointOnMap(at);
        }
    }

    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    internal static class ErrandMapTintPatch
    {
        private static void Postfix() { try { if (ErrandMap.Ours.Count > 0 || ErrandMap.Trees.Count > 0) ErrandMap.Tint(); } catch { } }
    }
}
#endif

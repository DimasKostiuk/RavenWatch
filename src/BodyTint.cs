#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A dvergr's own colours (the user 2026-11-13: a dwarf of the story with a greyer skin, a greener suit, golden eyes): its template's lines
    //   tint_skin: r g b    its skin (the game's is one blue-green for every dvergr)
    //   tint_hair: r g b    its hair, beard and moustache (the game's: white-grey, or red — the …_Redbeard, …_Redhair things)
    //   tint_suit: r g b    its suit
    //   tint_eyes: r g b    its eyes' glow (the game's: blue)
    // none written: as the game made it. The colour is what that part comes out as on the whole (its mean); light, shade and drawing stay the
    // picture's own: each pixel of the part takes the colour by how light it was against the part's mean.
    // How: the game's pictures are small (128 × 128) and shared by every dvergr, so nothing of the game's is changed — a copy of the picture is
    // made with the part recoloured (once a picture and colour, kept for all who share them: Made) and given to this body's renderers by a
    // property block (no material of its own). The hair is drawn from the body's picture (a strip of it), by its own renderers: its copy is
    // another one. The eyes: their square in the body's picture and the glow's colour. Its boots are the body's second material: left as the game made them.
    // On every game from the person's template (nothing sent). Set when it comes and when its template's colours change (NpcBody.KeepOurs,
    // NpcPreview); a piece put on makes new renderers: painted again at once (SkinGuardPatch → Bind).
    //
    // A person on the player's body (the user 2026-11-14: the Falcon sisters' capes «світліший, щось до кофейного», and eyes that suit them):
    //   tint_eyes: r g b    its eyes. Measured in the game's files that day: both eyes are two small squares of the body's mesh (four corners each, mirrored,
    //                       at eye height) drawn from one flat blue patch of the body's picture (PlayerCharacter_01 and …_fem, 256 × 256: 38 pixels of
    //                       113,185,255) — so the eyes' colour is that patch in a copy of the picture. No glow: the player's material has no glow map,
    //                       its glow would light the whole body. The skin's own colour stays the game's (the template's «skin:»)
    //   tint_cape: r g b    its cape, whatever is on its shoulders (the game's own list of that slot's things): the cape's picture in that colour, as a
    //                       dvergr's suit is — every lit pixel by how light it was against the picture's mean — and the cape's sheen (the feather cape's
    //                       «_MetalColor» is a strong blue) in the same colour × CapeSheen
    //   tint_head: r g b    whatever is on its head (the game's helmet piece), the same way as the cape (the user 2026-11-14: the sisters' hood, the game's red
    //                       HelmetMage, in their capes' colour)
    // An echo (NpcFade) is not painted: its body's blocks are the fade's own.
    internal static class BodyTint
    {
        internal static float EyePower = 2.6f;   // the glow's strength (the game's own blue is 2.62 at its strongest channel); tuned live: studio «set BodyTint.EyePower 2»
        internal static float SuitFrom = 0.12f;  // a suit's pixel paler than this (metal, bone, stitching in grey) keeps its colour
        internal static float CapeSheen = 1.6f;  // a cape's sheen: its colour × this (NOT seen in the game yet: tune live, studio «set BodyTint.CapeSheen 1»)
        internal static float CapeFrom = 0.02f;  // a cape's pixel darker than this is the picture's empty black: left
        private const int Skin = 0, Hair = 1, Suit = 2, Person = 3, Cape = 4;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex"), Emission = Shader.PropertyToID("_EmissionColor"), MetalColor = Shader.PropertyToID("_MetalColor");
        private static readonly System.Reflection.FieldInfo ShoulderThings = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_shoulderItemInstances"); // (the game's own list of what hangs on the shoulders)
        private static readonly System.Reflection.FieldInfo HeadThing = HarmonyLib.AccessTools.Field(typeof(VisEquipment), "m_helmetItemInstance"); // (the game's own piece on the head)
        private static readonly Dictionary<string, Texture2D> Made = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Texture, Color32[]> Read = new Dictionary<Texture, Color32[]>();
        private static MaterialPropertyBlock _block;

        internal sealed class Mark : MonoBehaviour { internal string Stamp = ""; internal NpcDef Def; internal Texture Skin; internal bool Eyes; internal readonly List<Renderer> Painted = new List<Renderer>(); } // (Painted: the cape's and the head's renderers given our colour)

        private static string Stamp(NpcDef d) { return d.TintSkin.ToString("0.000") + d.TintHair.ToString("0.000") + d.TintSuit.ToString("0.000") + d.TintEyes.ToString("0.000") + d.TintCape.ToString("0.000") + d.TintHead.ToString("0.000") + EyePower.ToString("0.00") + SuitFrom.ToString("0.00") + CapeSheen.ToString("0.00") + CapeFrom.ToString("0.00"); }

        // the picture the body's skin is drawn from now (a person's: the game gives the man's or the woman's when it sets the model — after our first look, maybe)
        private static Texture SkinOf(VisEquipment vis) { Material m = vis.m_bodyModel.sharedMaterial; return m != null && m.HasProperty(MainTex) ? m.GetTexture(MainTex) : null; }

        // this body's colours as its template says (nothing unless they changed)
        internal static void Set(VisEquipment vis, NpcDef d)
        {
            if (vis == null || d == null || d.Odin || vis.m_bodyModel == null) return;
            if (!d.Dvergr && d.Echo > 0f) return; // (an echo's body: its blocks are the fade's own)
            Mark m = vis.GetComponent<Mark>();
            bool any = d.Dvergr ? NpcDef.HasTint(d.TintSkin) || NpcDef.HasTint(d.TintHair) || NpcDef.HasTint(d.TintSuit) || NpcDef.HasTint(d.TintEyes) : NpcDef.HasTint(d.TintEyes) || NpcDef.HasTint(d.TintCape) || NpcDef.HasTint(d.TintHead);
            if (m == null) { if (!any) return; m = vis.gameObject.AddComponent<Mark>(); } // (as the game made it: nothing made)
            string stamp = Stamp(d);
            Texture skin = d.Dvergr ? null : SkinOf(vis); // (a person: painted again when the game gives its body the other model's picture)
            if (m.Stamp == stamp && m.Def == d && m.Skin == skin) return;
            m.Stamp = stamp; m.Def = d; m.Skin = skin;
            if (d.Dvergr) Paint(vis, d); else PaintPerson(vis, d, m, true);
        }

        // a piece put on or taken off: its renderers are new ones (SkinGuardPatch)
        internal static void Bind(VisEquipment vis)
        {
            Mark m = vis != null ? vis.GetComponent<Mark>() : null;
            if (m == null || m.Def == null) return;
            if (m.Def.Dvergr) Paint(vis, m.Def); else PaintPerson(vis, m.Def, m, false); // (quiet: a dressing puts on six pieces, each a call here)
        }

        // a person on the player's body: its eyes (the blue patch of its skin's picture) and its cape, and the head's piece
        private static void PaintPerson(VisEquipment vis, NpcDef d, Mark m, bool say)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            SkinnedMeshRenderer body = vis.m_bodyModel;
            Texture src = SkinOf(vis);
            m.Skin = src;
            Texture2D pic = NpcDef.HasTint(d.TintEyes) && src != null ? Picture(src, Person, NpcDef.NoTint, d.TintEyes) : null;
            if (pic != null) { _block.Clear(); body.GetPropertyBlock(_block); _block.SetTexture(MainTex, pic); body.SetPropertyBlock(_block, 0); m.Eyes = true; } // (what the body's own block says stays: ours is laid over its first material alone)
            else if (m.Eyes) { body.SetPropertyBlock(null, 0); m.Eyes = false; }

            foreach (Renderer was in m.Painted) if (was != null) for (int i = 0; i < was.sharedMaterials.Length; i++) was.SetPropertyBlock(null, i); // (painted before: as the game made it, then anew — the cape or the head's piece may be another one now)
            m.Painted.Clear();
            if (NpcDef.HasTint(d.TintCape))
            {
                List<GameObject> things = ShoulderThings != null ? ShoulderThings.GetValue(vis) as List<GameObject> : null;
                if (say && Plugin.Instance != null) Plugin.Instance.Log("BodyTint: " + d.Id + ": eyes " + (pic != null ? "painted" : "not painted") + ", things on its shoulders " + (things != null ? things.Count.ToString() : "unknown (no such list)")); // (when its colours are laid: the cape did not turn in the first run, 2026-11-14)
                if (things != null) foreach (GameObject thing in things) PaintThing(thing, d.TintCape, "cape", d, m, say);
            }
            if (NpcDef.HasTint(d.TintHead))
            {
                GameObject head = HeadThing != null ? HeadThing.GetValue(vis) as GameObject : null;
                if (head == null && say && Plugin.Instance != null) Plugin.Instance.Log("BodyTint: " + d.Id + ": nothing on its head to paint");
                PaintThing(head, d.TintHead, "head", d, m, say);
            }
        }

        // a worn thing (the cape, the head's piece) in one colour: its picture as a dvergr's suit is, its sheen in the same colour × CapeSheen
        private static void PaintThing(GameObject thing, Vector3 c, string what, NpcDef d, Mark m, bool say)
        {
            if (thing == null) return;
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>(true)) // heavy: when its colours change and once a piece is put on
            {
                Material[] mats = r.sharedMaterials;
                bool painted = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    Texture from = mat != null && mat.HasProperty(MainTex) ? mat.GetTexture(MainTex) : null;
                    Texture2D t = from != null ? Picture(from, Cape, c, NpcDef.NoTint) : null;
                    if (t == null) continue;
                    _block.Clear(); r.GetPropertyBlock(_block);
                    _block.SetTexture(MainTex, t);
                    if (mat.HasProperty(MetalColor)) _block.SetColor(MetalColor, new Color(c.x * CapeSheen, c.y * CapeSheen, c.z * CapeSheen, 1f));
                    r.SetPropertyBlock(_block, i);
                    painted = true;
                }
                if (painted) m.Painted.Add(r);
                if (say && Plugin.Instance != null) Plugin.Instance.Log("BodyTint: " + d.Id + ": " + what + " renderer " + r.name + " (" + r.GetType().Name + ", " + mats.Length + " material(s)) " + (painted ? "painted" : "NOT painted"));
            }
        }

        private static void Paint(VisEquipment vis, NpcDef d)
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (SkinnedMeshRenderer r in vis.GetComponentsInChildren<SkinnedMeshRenderer>(true)) // heavy: when its colours change and once a piece is put on
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    Texture src = mat != null && mat.HasProperty(MainTex) ? mat.GetTexture(MainTex) : null;
                    if (src == null) continue;
                    bool bodys = src.name.StartsWith("DvergrBody", System.StringComparison.Ordinal); // (the body's own picture: its skin, and the strip its hair is drawn from)
                    if (r == vis.m_bodyModel && !bodys) continue; // (the body's second material is its boots: the game's own, whatever the suit — the user 2026-11-13: they took the eyes' glow and shone yellow)
                    int kind = r == vis.m_bodyModel ? Skin : bodys ? Hair : Suit;
                    Vector3 c = kind == Skin ? d.TintSkin : kind == Hair ? d.TintHair : d.TintSuit;
                    bool eyes = kind == Skin && NpcDef.HasTint(d.TintEyes) && mat.HasProperty(Emission) && mat.GetColor(Emission).maxColorComponent > 0.01f; // (only a material that glows already: the eyes')
                    if (!NpcDef.HasTint(c) && !eyes) { if (r.HasPropertyBlock()) r.SetPropertyBlock(null, i); continue; }
                    Texture2D t = Picture(src, kind, c, eyes ? d.TintEyes : NpcDef.NoTint);
                    if (t == null) continue;
                    _block.Clear();
                    _block.SetTexture(MainTex, t);
                    if (eyes) _block.SetColor(Emission, new Color(d.TintEyes.x * EyePower, d.TintEyes.y * EyePower, d.TintEyes.z * EyePower, 1f));
                    r.SetPropertyBlock(_block, i);
                }
            }
        }

        // the picture with its part in that colour (and the eyes' square in theirs), made once
        private static Texture2D Picture(Texture src, int kind, Vector3 c, Vector3 eyes)
        {
            string key = src.GetInstanceID() + "|" + kind + "|" + c.ToString("0.000") + "|" + eyes.ToString("0.000") + "|" + SuitFrom.ToString("0.00") + "|" + CapeFrom.ToString("0.00");
            Texture2D t;
            if (Made.TryGetValue(key, out t) && t != null) return t;
            Color32[] from = Pixels(src);
            if (from == null) return null;
            int w = src.width, h = src.height;
            Color32[] px = new Color32[from.Length];
            bool[] part = new bool[from.Length], eye = new bool[from.Length];
            double sum = 0; int n = 0;
            bool has = NpcDef.HasTint(c), hasEyes = NpcDef.HasTint(eyes);
            for (int i = 0; i < from.Length; i++)
            {
                Color32 p = from[i];
                float hu, s, v;
                Color.RGBToHSV(p, out hu, out s, out v);
                float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                bool blue = hu > 0.40f && hu < 0.60f; // (the dvergr's blue-green skin, and its eyes' square)
                if (kind == Skin) { eye[i] = blue && s > 0.85f && v > 0.85f; part[i] = !eye[i] && blue && s > 0.18f && v > 0.08f; }
                else if (kind == Person) eye[i] = hu > 0.50f && hu < 0.70f && s > 0.35f && v > 0.5f; // (the eyes' flat blue patch: 113,185,255 — hue 0.58, the one blue thing of a skin's picture)
                else if (kind == Cape) part[i] = lum > CapeFrom;
                else if (kind == Hair) part[i] = (i % w) > w * 0.70f && lum > 0.06f && !(blue && s > 0.18f); // (the hair's strip of the body's picture: its right side)
                else part[i] = s > SuitFrom && v > 0.05f;
                if (part[i]) { sum += lum; n++; }
            }
            float mean = n > 0 ? (float)(sum / n) : 0.5f;
            for (int i = 0; i < from.Length; i++)
            {
                Color32 p = from[i];
                if (hasEyes && eye[i]) { p = new Color32((byte)(eyes.x * 255f), (byte)(eyes.y * 255f), (byte)(eyes.z * 255f), p.a); }
                else if (has && part[i])
                {
                    float k = ((0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f) / Mathf.Max(0.02f, mean);
                    p = new Color32((byte)Mathf.Clamp(c.x * k * 255f, 0f, 255f), (byte)Mathf.Clamp(c.y * k * 255f, 0f, 255f), (byte)Mathf.Clamp(c.z * k * 255f, 0f, 255f), p.a);
                }
                px[i] = p;
            }
            t = new Texture2D(w, h, TextureFormat.RGBA32, true, !Srgb(src));
            t.name = src.name + "_rw_tint";
            t.filterMode = src.filterMode; t.wrapMode = src.wrapMode; t.anisoLevel = src.anisoLevel;
            t.SetPixels32(px);
            t.Apply(true, true);
            Made[key] = t;
            return t;
        }

        // the game's picture as it is stored (it cannot be read directly: drawn onto a card and read from there), once a picture
        private static Color32[] Pixels(Texture src)
        {
            Color32[] px;
            if (Read.TryGetValue(src, out px)) return px;
            RenderTexture prev = RenderTexture.active, rt = null;
            try
            {
                bool srgb = Srgb(src);
                rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                Texture2D read = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, !srgb);
                read.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                read.Apply();
                px = read.GetPixels32();
                Object.Destroy(read);
            }
            catch (System.Exception e) { px = null; if (Plugin.Instance != null) Plugin.Instance.Log("BodyTint: the picture «" + src.name + "» could not be read: " + e.Message); }
            finally { RenderTexture.active = prev; if (rt != null) RenderTexture.ReleaseTemporary(rt); }
            Read[src] = px; // (a picture that could not be read is not tried again)
            return px;
        }

        private static bool Srgb(Texture t)
        {
            try { return UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat); }
            catch (System.Exception) { return true; } // quiet: a picture that cannot say its format is a colour picture
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Фарби»: a raven's look = colours by zone + eyes + body glow + sheen + magic sparks. A look never tints the game's
    // picture (a near-black raven stays black under any tint): it paints a NEW picture. Every pixel keeps its own
    // lightness within its zone and takes its colour from that zone's gradient «тінь → світло»; the zone's «сила»
    // mixes it with the game's own colour, «рельєф» keeps more or less of the feathers' pattern.
    // Zones come from LookDiag.Zones (bones → zones). Left and right share one picture: both sides always match.
    //
    // Files: config/RavenWatch/looks/<id>.txt (made in the test window, tab «Фарби»):
    //   name: Провідник
    //   for: guide                       guide | hugin | munin | any
    //   zone body 1A0204 B01822 100 80   zone id, shadow colour, light colour, strength %, relief %
    //   eyes FFB040 4                    glow colour, power
    //   glow 000000 0                    the whole body's glow: colour, power
    //   sheen FF5A40 0.6 0.7 0.3         metal colour, metallic, metal gloss, gloss
    //   sparks FF4020 2 3 0.2            the shader's own glowing noise: colour, power, size, edge
    //   thing head 0 0.04 0 0 0 0 1 1 1 - - pair HelmetHat4
    //                                    a thing hung on a bone (LookThings): bone id, place, turn, size (the Workshop's
    //                                    frame on that bone), tint RRGGBB or -, fx or -, pair or - (mirrored on the
    //                                    other side too), then what it is: a game piece, fx/<piece>, asset/<set>/<file>
    //                                    or model:<Workshop file name>
    //   fx zhar_pirya                    an effect of our library it wears (FxLib, config/RavenWatch/fx/<id>.txt)
    // A line left out = the game's own. Files are read only with "Outfits all for tuning" (as the gear); the release
    // uses Builtin.
    internal class Look
    {
        internal class Part { public bool On; public Color Dark = Color.black, Light = Color.white; public float Mix = 1f, Relief = 0.8f; }

        public string Element = ""; // «element: fire» (Elements): its own, else the catalog's table
        public string Id = "", Name = "", For = "guide", Desc = ""; // for: rune = a rune may give it (WearCatalog: Фрея)
        public System.DateTime SavedAt; // a file's last write (the newest guide file wins)
        public Part[] Zones = new Part[LookDiag.ZoneNames.Length];
        public bool EyesOn; public Color Eyes = new Color(1f, 0.7f, 0.25f); public float EyePower = 4f;
        public bool GlowOn; public Color Glow = new Color(1f, 0.3f, 0.2f); public float GlowPower = 0.5f;
        public bool SheenOn; public Color Sheen = Color.white; public float Metal = 0.5f, MetalGloss = 0.7f, Gloss = 0.2f;
        public bool SparksOn; public Color Sparks = new Color(1f, 0.35f, 0.15f); public float SparkPower = 2f, SparkSize = 3f, SparkEdge = 0.2f;

        // a thing hung on the raven: on one bone (and mirrored on its pair), in the Workshop's frame on that bone
        internal class Thing
        {
            public string Src = "", Bone = "head";
            public Vector3 Pos, Rot, Scale = Vector3.one;
            public bool Tinted, Fx, Pair;
            public Color Tint = Color.white;

            internal string Line()
            {
                return "thing " + Bone + " " + F(Pos.x) + " " + F(Pos.y) + " " + F(Pos.z) + " " + F(Rot.x) + " " + F(Rot.y) + " " + F(Rot.z) + " "
                    + F(Scale.x) + " " + F(Scale.y) + " " + F(Scale.z) + " " + (Tinted ? Hex(Tint) : "-") + " " + (Fx ? "fx" : "-") + " " + (Pair ? "pair" : "-") + " " + Src;
            }

            // what makes it be built anew (the rest only moves it)
            internal string Kind() { return Src + "|" + Bone + "|" + (Tinted ? Hex(Tint) : "") + "|" + Fx + "|" + Pair; }

            internal Thing Clone() { return (Thing)MemberwiseClone(); }

            internal static Thing Parse(string[] a, string line)
            {
                if (a.Length < 15) return null;
                Thing t = new Thing();
                t.Bone = a[1].ToLowerInvariant();
                t.Pos = new Vector3(N(a[2], 0f), N(a[3], 0f), N(a[4], 0f));
                t.Rot = new Vector3(N(a[5], 0f), N(a[6], 0f), N(a[7], 0f));
                t.Scale = new Vector3(N(a[8], 1f), N(a[9], 1f), N(a[10], 1f));
                t.Tinted = a[11] != "-"; if (t.Tinted) t.Tint = C(a[11]);
                t.Fx = a[12] == "fx";
                t.Pair = a[13] == "pair";
                // the rest of the line (a name of a Workshop file may have spaces)
                int at = 0;
                for (int i = 0; i < 14; i++) { at = line.IndexOf(a[i], at, System.StringComparison.Ordinal) + a[i].Length; }
                t.Src = line.Substring(at).Trim();
                return t.Src.Length > 0 ? t : null;
            }
        }

        public List<Thing> Things = new List<Thing>();
        public List<string> Fx = new List<string>(); // the effects of our library it wears (FxLib ids)

        internal Look() { for (int i = 0; i < Zones.Length; i++) Zones[i] = new Part(); }

        internal static readonly string[] ZoneIds = { "none", "body", "head", "beak", "eyes", "wings", "tips", "tail", "legs" };

        internal string Code() { return Code(true); }

        internal string Code(bool things)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("name: ").Append(Name).Append('\n').Append("for: ").Append(For).Append('\n');
            if (Desc.Length > 0) sb.Append("desc: ").Append(Desc).Append('\n');
            if (Element.Length > 0) sb.Append("element: ").Append(Element).Append('\n');
            for (int i = 1; i < Zones.Length; i++)
            {
                Part p = Zones[i];
                if (!p.On) continue;
                sb.Append("zone ").Append(ZoneIds[i]).Append(' ').Append(Hex(p.Dark)).Append(' ').Append(Hex(p.Light)).Append(' ')
                    .Append(Mathf.RoundToInt(p.Mix * 100f)).Append(' ').Append(Mathf.RoundToInt(p.Relief * 100f)).Append('\n');
            }
            if (EyesOn) sb.Append("eyes ").Append(Hex(Eyes)).Append(' ').Append(F(EyePower)).Append('\n');
            if (GlowOn) sb.Append("glow ").Append(Hex(Glow)).Append(' ').Append(F(GlowPower)).Append('\n');
            if (SheenOn) sb.Append("sheen ").Append(Hex(Sheen)).Append(' ').Append(F(Metal)).Append(' ').Append(F(MetalGloss)).Append(' ').Append(F(Gloss)).Append('\n');
            if (SparksOn) sb.Append("sparks ").Append(Hex(Sparks)).Append(' ').Append(F(SparkPower)).Append(' ').Append(F(SparkSize)).Append(' ').Append(F(SparkEdge)).Append('\n');
            if (things) foreach (Thing t in Things) sb.Append(t.Line()).Append('\n');
            if (things) foreach (string id in Fx) sb.Append("fx ").Append(id).Append('\n');
            return sb.ToString();
        }

        internal static Look Parse(string text, string id)
        {
            Look l = new Look();
            l.Id = id ?? "";
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int hash = line.IndexOf('#');
                if (hash == 0) continue;
                if (line.Length == 0) continue;
                if (line.StartsWith("name:")) { l.Name = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("for:")) { l.For = line.Substring(4).Trim().ToLowerInvariant(); continue; }
                if (line.StartsWith("desc:")) { l.Desc = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("element:")) { l.Element = line.Substring(8).Trim().ToLowerInvariant(); continue; }
                string[] a = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                switch (a[0].ToLowerInvariant())
                {
                    case "zone":
                        int z = a.Length > 1 ? System.Array.IndexOf(ZoneIds, a[1].ToLowerInvariant()) : -1;
                        if (z <= 0 || a.Length < 4) break;
                        Part p = l.Zones[z];
                        p.On = true; p.Dark = C(a[2]); p.Light = C(a[3]);
                        p.Mix = a.Length > 4 ? N(a[4], 100f) / 100f : 1f;
                        p.Relief = a.Length > 5 ? N(a[5], 80f) / 100f : 0.8f;
                        break;
                    case "eyes": l.EyesOn = true; if (a.Length > 1) l.Eyes = C(a[1]); if (a.Length > 2) l.EyePower = N(a[2], 4f); break;
                    case "glow": l.GlowOn = true; if (a.Length > 1) l.Glow = C(a[1]); if (a.Length > 2) l.GlowPower = N(a[2], 0.5f); break;
                    case "sheen":
                        l.SheenOn = true; if (a.Length > 1) l.Sheen = C(a[1]);
                        if (a.Length > 2) l.Metal = N(a[2], 0.5f); if (a.Length > 3) l.MetalGloss = N(a[3], 0.7f); if (a.Length > 4) l.Gloss = N(a[4], 0.2f);
                        break;
                    case "sparks":
                        l.SparksOn = true; if (a.Length > 1) l.Sparks = C(a[1]);
                        if (a.Length > 2) l.SparkPower = N(a[2], 2f); if (a.Length > 3) l.SparkSize = N(a[3], 3f); if (a.Length > 4) l.SparkEdge = N(a[4], 0.2f);
                        break;
                    case "fx": if (a.Length > 1 && !l.Fx.Contains(a[1])) l.Fx.Add(a[1]); break;
                    case "thing":
                        Thing th = Thing.Parse(a, line);
                        if (th != null && System.Array.IndexOf(LookThings.BoneIds, th.Bone) >= 0) l.Things.Add(th);
                        break;
                }
            }
            return l;
        }

        internal Look Copy() { Look c = Parse(Code(), Id); return c; }

        internal static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }
        private static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        private static Color C(string s) { Color c; return ColorUtility.TryParseHtmlString("#" + s.TrimStart('#'), out c) ? c : Color.white; }
        private static float N(string s, float def) { float v; return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def; }
    }

    internal static class RavenLooks
    {
        // the looks that ship in the mod (the user's approved files get baked here)
        private static readonly string[] Builtin =
        {
            "id: provodnyk\nname: Провідник\nfor: guide\n" +
            "zone body 140204 9E1620 100 80\nzone head 140204 A8182A 100 80\nzone beak 1E140C 7A5A34 100 70\n" +
            "zone eyes 3A1A08 FFC060 100 60\nzone wings 120204 9A1420 100 80\nzone tips 080102 5E0A12 100 80\n" +
            "zone tail 0A0102 6A0C16 100 80\nzone legs 141010 4A3A34 100 70\n" +
            "eyes FFB040 4\nsheen FF6A50 0.55 0.7 0.25\n" +
            "thing head 0 -0.068 0.012 125 5 5 0.348 0.327 0.362 - - - GemstoneRed\n",
            // the rune looks (Фрея, «for: rune»): the first draft, to be tuned in «Фарби»
            "id: f_inii\nname: Іній\nfor: rune\ndesc: пір'я вкрите інеєм, очі крижано-сині\nzone body 455780 8DAFD0 83 80\nzone head 415578 B8CCE0 100 80\nzone beak 2A2A30 8A8A96 100 70\nzone eyes 0A1830 80C8FF 100 60\nzone wings 141C28 A8C4DC 100 80\nzone tips 0A0E18 E8F2FF 100 80\nzone tail 0A0E18 7A90A8 100 80\nzone legs 202428 6A7078 100 70\neyes 60B8FF 4\nsheen C0E0FF 0.5 0.7 0.3\n",
            "id: f_popil\nname: Попіл\nfor: rune\ndesc: сіре, як згасле вогнище, з жаринами в очах\nzone body 2D2D2D 9A9A96 100 80\nzone head 333333 B0B0AC 100 80\nzone beak 1A1A1A 5A5A5A 100 70\nzone eyes 131A00 F5F5F0 100 60\nzone wings 202020 8A8A86 100 80\nzone tips 919191 505050 100 80\nzone tail AAAAC6 6A6A66 100 80\nzone legs 70706D 4A4A4A 100 70\neyes FF7A20 3\n",
            "id: f_zoloto\nname: Золото Одіна\nfor: rune\ndesc: золоте пір'я з полиском, очі світлі, як мед\nzone body 2A1A04 D8A840 100 80\nzone head 2A1A04 E8C060 100 80\nzone beak 3A2A10 C09040 100 70\nzone eyes 2A1A00 FFF0A0 100 60\nzone wings 241604 C89830 100 80\nzone tips 140C02 806010 100 80\nzone tail 1A1004 A07820 100 80\nzone legs 3A2A10 8A6A30 100 70\neyes FFE080 4\nsheen FFD060 0.7 0.75 0.4\n",
            "id: f_krov\nname: Кров\nfor: rune\ndesc: темно-червоне пір'я й червоні очі\nzone body 140202 7A0A0E 100 80\nzone head 1A0204 8A1014 100 80\nzone beak 100808 4A2020 100 70\nzone eyes 200000 FF2020 100 60\nzone wings 100102 6A080C 100 80\nzone tips 080000 3A0406 100 80\nzone tail 0A0001 4A0608 100 80\nzone legs 140808 3A1A1A 100 70\neyes FF2020 5\nsheen 8A1010 0.4 0.6 0.3\n",
            "id: f_moh\nname: Мох\nfor: rune\ndesc: зелено-буре пір'я, як мох на старому дубі\nzone body 141A0A 5A7A30 100 80\nzone head 182010 6A8A3A 100 80\nzone beak 2A2010 7A6A40 100 70\nzone eyes 102000 A0FF60 100 60\nzone wings 101808 4A6A28 100 80\nzone tips 080C04 2A3A14 100 80\nzone tail 0A1006 3A5A20 100 80\nzone legs 1A1A10 4A4A30 100 70\neyes A0FF60 3\n",
            "id: f_nich\nname: Ніч\nfor: rune\ndesc: пір'я синє, як нічне небо, з фіолетовим полиском\nzone body 06060E 2A2A5A 100 80\nzone head 08081A 3A3A7A 100 80\nzone beak 0A0A14 2A2A40 100 70\nzone eyes 0A0020 C080FF 100 60\nzone wings 04040C 24245A 100 80\nzone tips 020208 14143A 100 80\nzone tail 040410 1A1A4A 100 80\nzone legs 0A0A14 202034 100 70\neyes B070FF 4\nsheen 7060FF 0.5 0.7 0.3\n",
            "id: f_burya\nname: Буря\nfor: rune\ndesc: пір'я кольору грозової хмари, в очах блискавка\nzone body 1A2026 6A7A8A 100 80\nzone head 1E242A 7A8A9A 100 80\nzone beak 1A1A20 4A5060 100 70\nzone eyes 001830 60D0FF 100 60\nzone wings 161C22 5A6A7A 100 80\nzone tips 0A1014 30404A 100 80\nzone tail 101418 405060 100 80\nzone legs 1A1E22 3A4048 100 70\neyes 70D8FF 5\nsparks 80C8FF 2 3 0.2\n",
            "id: f_sontse\nname: Сонце\nfor: rune\ndesc: пір'я теплого бурштину, очі як полудень\nzone body 2A1006 D06A20 100 80\nzone head 301408 E08030 100 80\nzone beak 3A2A10 C0A060 100 70\nzone eyes 301000 FFE060 100 60\nzone wings 280E04 C05A18 100 80\nzone tips 1A0802 803A10 100 80\nzone tail 200A04 A04A14 100 80\nzone legs 3A2010 806040 100 70\neyes FFD040 4\n",
        };

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "looks"); } }
        internal static bool FromFiles { get { return Outfits.TuningWanted; } }
        internal static int Version; // goes up when a file is saved: the guide re-dresses

        private static List<Look> _builtin;
        internal static List<Look> BuiltinLooks()
        {
            if (_builtin != null) return _builtin;
            _builtin = new List<Look>();
            foreach (string s in Builtin)
            {
                string id = "";
                foreach (string line in s.Split('\n')) if (line.StartsWith("id:")) id = line.Substring(3).Trim();
                _builtin.Add(Look.Parse(s, id));
            }
            return _builtin;
        }

        internal static List<Look> FileLooks()
        {
            List<Look> l = new List<Look>();
            try
            {
                if (!Directory.Exists(Dir)) return l;
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { Look k = Look.Parse(File.ReadAllText(f), Path.GetFileNameWithoutExtension(f)); k.SavedAt = File.GetLastWriteTimeUtc(f); l.Add(k); }
                    catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Raven look " + f + ": " + e.Message); }
                }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Raven looks folder: " + e.Message); }
            return l;
        }

        internal static void Save(Look l)
        {
            Directory.CreateDirectory(Dir);
            if (string.IsNullOrEmpty(l.Id))
            {
                // a new look never overwrites another one's file of the same name
                string id = Perches.IdFor(l.Name), at = id;
                for (int n = 2; File.Exists(Path.Combine(Dir, at + ".txt")); n++) at = id + "_" + n;
                l.Id = at;
            }
            File.WriteAllText(Path.Combine(Dir, l.Id + ".txt"), l.Code());
            Version++;
        }

        internal static void Delete(Look l)
        {
            string f = Path.Combine(Dir, l.Id + ".txt");
            if (File.Exists(f)) File.Delete(f);
            Version++;
        }

        // the guide's look: a saved file (while tuning) before the built-in one
        private static Look _guide;
        private static int _guideAt = -1;
        internal static Look ForGuide()
        {
            if (_guideAt == Version) return _guide;
            _guideAt = Version;
            _guide = null;
            if (FromFiles) foreach (Look l in FileLooks()) if (l.For == "guide" && (_guide == null || l.SavedAt > _guide.SavedAt)) _guide = l; // the last saved
            if (_guide == null) foreach (Look l in BuiltinLooks()) if (l.For == "guide") { _guide = l; break; }
            return _guide;
        }

        // ---------------------------------------------------------------- painting
        // What is painted for one look on one raven model: the new pictures and the material copies wearing them.
        // The test window keeps its own one and repaints it in place on every change; the game's looks are cached by code.
        internal class Painted
        {
            public Texture2D Main, Emi;
            public readonly Dictionary<Material, Material> Mats = new Dictionary<Material, Material>();
            public bool Munin;
            public Look Look;
            public int Users; // ravens wearing it now (RavenWear, the Guide): never freed while one does

            internal void Free()
            {
                foreach (Material m in Mats.Values) if (m != null) { EyeGlow.Release(m); Object.Destroy(m); }
                Mats.Clear();
                if (Main != null) Object.Destroy(Main);
                if (Emi != null) { EyeGlow.Release(Emi); Object.Destroy(Emi); }
                Main = Emi = null;
            }
        }

        private class Source { public Color32[] Main, Emi; public int W, H; public float EmiMax; public float[] Lo, Hi; }
        private static readonly Dictionary<bool, Source> Sources = new Dictionary<bool, Source>();
        private static readonly Dictionary<string, Painted> Cache = new Dictionary<string, Painted>();
        internal static string Problem = "";

        // the raven's creature material (the one with the colour picture) among a renderer's materials
        internal static bool IsBody(Material m) { return m != null && m.shader != null && m.shader.name == "Custom/Creature"; }

        // one look painted for a raven that wears it (RavenWear, the Guide); Drop when it takes it off. The things hung
        // on a look are no paint: the key leaves them out.
        internal static Painted Use(Look look, bool munin)
        {
            if (look == null) return null;
            string key = (munin ? "m|" : "h|") + look.Code(false);
            Painted p;
            if (!Cache.TryGetValue(key, out p))
            {
                Evict();
                p = new Painted();
                if (!Paint(p, look, munin)) { p.Free(); return null; }
                Cache[key] = p;
            }
            p.Users++;
            return p;
        }

        internal static void Drop(Painted p) { if (p != null && p.Users > 0) p.Users--; }

        // the ones nobody wears go (their pictures and copies, ~20 MB each) once more than IdleKept of them pile up
        internal static int IdleKept = 4;
        private static void Evict()
        {
            List<string> free = new List<string>();
            foreach (KeyValuePair<string, Painted> kv in Cache) if (kv.Value.Users <= 0) free.Add(kv.Key);
            if (free.Count < IdleKept) return;
            foreach (string k in free) { Cache[k].Free(); Cache.Remove(k); }
        }

        // one look being edited in the test window (tab «Руни»): worn as it is now by everything wearing its id, until
        // it is saved or dropped (null)
        private static Look _live;
        internal static Look Live { get { return _live; } set { _live = value; Version++; } }
        internal static bool HasFile(string id) { return !string.IsNullOrEmpty(id) && File.Exists(Path.Combine(Dir, id + ".txt")); }
        internal static bool IsBuiltin(string id) { foreach (Look k in BuiltinLooks()) if (k.Id == id) return true; return false; }

        // a look by id: the one being edited, the saved files (while tuning), the built-in ones
        internal static List<Look> AllLooks()
        {
            List<Look> l = new List<Look>();
            HashSet<string> ids = new HashSet<string>();
            if (_live != null && ids.Add(_live.Id)) l.Add(_live);
            if (FromFiles) foreach (Look k in FileLooks()) if (ids.Add(k.Id)) l.Add(k);
            foreach (Look k in BuiltinLooks()) if (ids.Add(k.Id)) l.Add(k);
            return l;
        }

        // the game's materials of one renderer → this look's copies (the others as they were)
        internal static Material[] Dress(Painted p, Material[] originals)
        {
            Material[] res = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++) res[i] = IsBody(originals[i]) ? MatOf(p, originals[i]) : originals[i];
            return res;
        }

        private static Material MatOf(Painted p, Material orig)
        {
            Material m;
            if (p.Mats.TryGetValue(orig, out m) && m != null) return m;
            m = new Material(orig);
            m.name = orig.name + " (look)";
            m.hideFlags = HideFlags.HideAndDontSave;
            p.Mats[orig] = m;
            Fit(m, orig, p);
            return m;
        }

        // the look's material settings, from the game's own each time (so a switch turned off really goes back)
        private static void Fit(Material m, Material orig, Painted p)
        {
            Look l = p.Look;
            if (l == null) return;
            m.mainTexture = p.Main != null ? p.Main : orig.mainTexture;
            if (m.HasProperty("_Color")) m.color = p.Main != null ? Color.white : orig.color;
            if (m.HasProperty("_EmissionMap"))
            {
                if (p.Emi != null)
                {
                    m.SetTexture("_EmissionMap", p.Emi);
                    float max = Mathf.Max(l.EyesOn ? l.EyePower : 0f, l.GlowOn ? l.GlowPower : 0f);
                    m.SetColor("_EmissionColor", Color.white * max);
                }
                else { m.SetTexture("_EmissionMap", orig.GetTexture("_EmissionMap")); m.SetColor("_EmissionColor", orig.GetColor("_EmissionColor")); }
            }
            if (m.HasProperty("_MetalColor"))
            {
                m.SetColor("_MetalColor", l.SheenOn ? l.Sheen : orig.GetColor("_MetalColor"));
                m.SetFloat("_Metallic", l.SheenOn ? l.Metal : orig.GetFloat("_Metallic"));
                m.SetFloat("_MetalGloss", l.SheenOn ? l.MetalGloss : orig.GetFloat("_MetalGloss"));
                m.SetFloat("_Glossiness", l.SheenOn ? l.Gloss : orig.GetFloat("_Glossiness"));
            }
            if (m.HasProperty("_NoiseGlowEnabled"))
            {
                Material t = SparkTemplate();
                bool on = l.SparksOn && t != null;
                m.SetFloat("_NoiseGlowEnabled", on ? 1f : orig.GetFloat("_NoiseGlowEnabled"));
                if (on)
                {
                    foreach (string k in SparkKeywords) m.EnableKeyword(k);
                    m.SetTexture("_NoiseGlowTex", t.GetTexture("_NoiseGlowTex"));
                    m.SetColor("_NoiseGlowColor", l.Sparks * l.SparkPower);
                    m.SetFloat("_NoiseGlowSize", l.SparkSize);
                    m.SetFloat("_NoiseGlowEdge", l.SparkEdge);
                }
                else
                {
                    foreach (string k in SparkKeywords) if (!orig.IsKeywordEnabled(k)) m.DisableKeyword(k);
                    m.SetTexture("_NoiseGlowTex", orig.GetTexture("_NoiseGlowTex"));
                    m.SetColor("_NoiseGlowColor", orig.GetColor("_NoiseGlowColor"));
                }
            }
        }

        // paints (or repaints in place) the pictures and refits every material copy
        internal static bool Paint(Painted p, Look l, bool munin)
        {
            Source s = SourceOf(munin);
            if (s == null) return false;
            int W, H; string why;
            byte[] zones = LookDiag.Zones(munin, out W, out H, out why);
            if (zones == null) { Problem = why; return false; }
            if (W != s.W || H != s.H) { Problem = Lang.T("карта зон і картинка різного розміру"); return false; }
            if (p.Main != null && (p.Main.width != W || p.Main.height != H)) { Object.Destroy(p.Main); p.Main = null; }
            if (p.Emi != null && (p.Emi.width != W || p.Emi.height != H)) { Object.Destroy(p.Emi); p.Emi = null; }
            p.Munin = munin;
            p.Look = l.Copy();

            bool anyZone = false;
            for (int i = 1; i < l.Zones.Length; i++) anyZone |= l.Zones[i].On;
            if (anyZone)
            {
                if (p.Main == null) p.Main = NewPic(W, H);
                Color32[] o = new Color32[W * H];
                for (int i = 0; i < o.Length; i++)
                {
                    int z = zones[i] == 0 ? (int)LookDiag.Zone.Body : zones[i];
                    Look.Part part = l.Zones[z];
                    Color32 c = s.Main[i];
                    if (!part.On) { o[i] = c; continue; }
                    float lum = Lum(c);
                    float range = Mathf.Max(0.02f, s.Hi[z] - s.Lo[z]);
                    float t = Mathf.Clamp01((lum - s.Lo[z]) / range);
                    t = Mathf.Lerp(0.5f, t, part.Relief);
                    Color g = Color.Lerp(part.Dark, part.Light, t);
                    Color res = Color.Lerp(c, g, part.Mix);
                    o[i] = new Color32(B(res.r * 255f), B(res.g * 255f), B(res.b * 255f), c.a);
                }
                p.Main.SetPixels32(o);
                p.Main.Apply(true);
            }
            else if (p.Main != null) { Object.Destroy(p.Main); p.Main = null; }

            if (l.EyesOn || l.GlowOn)
            {
                if (p.Emi == null) p.Emi = NewPic(W, H);
                float max = Mathf.Max(0.001f, Mathf.Max(l.EyesOn ? l.EyePower : 0f, l.GlowOn ? l.GlowPower : 0f));
                float eyeK = l.EyesOn ? l.EyePower / max : 0f, bodyK = l.GlowOn ? l.GlowPower / max : 0f;
                Color32[] e = new Color32[W * H];
                for (int i = 0; i < e.Length; i++)
                {
                    Color v;
                    if (zones[i] == (byte)LookDiag.Zone.Eyes)
                        v = l.EyesOn ? l.Eyes * eyeK * Mathf.Clamp01(Lum(s.Emi[i]) / Mathf.Max(0.01f, s.EmiMax) * 1.4f) : Color.black;
                    else
                    {
                        int z = zones[i] == 0 ? (int)LookDiag.Zone.Body : zones[i];
                        float t = Mathf.Clamp01((Lum(s.Main[i]) - s.Lo[z]) / Mathf.Max(0.02f, s.Hi[z] - s.Lo[z]));
                        v = l.GlowOn ? l.Glow * bodyK * (0.35f + 0.65f * t) : Color.black;
                    }
                    e[i] = new Color32(B(v.r * 255f), B(v.g * 255f), B(v.b * 255f), 255);
                }
                p.Emi.SetPixels32(e);
                p.Emi.Apply(true);
            }
            else if (p.Emi != null) { Object.Destroy(p.Emi); p.Emi = null; }

            foreach (KeyValuePair<Material, Material> kv in p.Mats) Fit(kv.Value, kv.Key, p);
            Problem = "";
            return true;
        }

        private static Source SourceOf(bool munin)
        {
            Source s;
            if (Sources.TryGetValue(munin, out s)) return s;
            Raven src = Shoulder.Template(munin);
            Material body = null;
            if (src != null && src.m_visual != null)
                foreach (Renderer r in src.m_visual.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials) if (IsBody(m)) body = m;
            if (body == null || body.mainTexture == null) { Problem = Lang.T("не знайшов матеріал ворона"); return null; }
            Texture2D main = LookDiag.Read(body.mainTexture, 1024);
            if (main == null) { Problem = Lang.T("картинку ворона не прочитано"); return null; }
            Texture2D emi = LookDiag.Read(body.HasProperty("_EmissionMap") ? body.GetTexture("_EmissionMap") : null, 1024);
            try
            {
                s = new Source { W = main.width, H = main.height, Main = main.GetPixels32() };
                s.Emi = new Color32[s.Main.Length];
                if (emi != null)
                {
                    Color32[] ep = emi.GetPixels32();
                    for (int y = 0; y < s.H; y++)
                        for (int x = 0; x < s.W; x++)
                        {
                            Color32 c = ep[(y * emi.height / s.H) * emi.width + x * emi.width / s.W];
                            s.Emi[y * s.W + x] = c;
                            s.EmiMax = Mathf.Max(s.EmiMax, Lum(c));
                        }
                }
                // each zone's lightness range (5th..95th share of its opaque pixels)
                int W, H; string why;
                byte[] zones = LookDiag.Zones(munin, out W, out H, out why);
                int nz = LookDiag.ZoneNames.Length;
                s.Lo = new float[nz]; s.Hi = new float[nz];
                int[][] hist = new int[nz][];
                for (int z = 0; z < nz; z++) hist[z] = new int[256];
                if (zones != null && zones.Length == s.Main.Length)
                    for (int i = 0; i < s.Main.Length; i++)
                    {
                        if (s.Main[i].a < 128) continue;
                        int z = zones[i] == 0 ? (int)LookDiag.Zone.Body : zones[i];
                        hist[z][Mathf.Clamp((int)(Lum(s.Main[i]) * 255f), 0, 255)]++;
                    }
                for (int z = 0; z < nz; z++)
                {
                    int total = 0;
                    foreach (int h in hist[z]) total += h;
                    if (total == 0) { s.Lo[z] = 0f; s.Hi[z] = 1f; continue; }
                    int acc = 0, lo = -1, hi = 255;
                    for (int b = 0; b < 256; b++)
                    {
                        acc += hist[z][b];
                        if (lo < 0 && acc >= total * 0.05f) lo = b;
                        if (acc >= total * 0.95f) { hi = b; break; }
                    }
                    s.Lo[z] = lo / 255f; s.Hi[z] = Mathf.Max(lo + 1, hi) / 255f;
                }
                Sources[munin] = s;
                return s;
            }
            finally
            {
                Object.Destroy(main);
                if (emi != null) Object.Destroy(emi);
            }
        }

        // the shader's glowing noise needs a noise picture: borrowed from a game creature that uses it
        // (looked for again now and then while not found: the creature that has it may not be loaded yet)
        private static Material _spark;
        private static float _sparkLookedAt = -100f;
        private static readonly List<string> SparkKeywords = new List<string>();
        internal static Material SparkTemplate()
        {
            if (_spark != null || Time.unscaledTime - _sparkLookedAt < 30f) return _spark;
            _sparkLookedAt = Time.unscaledTime;
            SparkKeywords.Clear();
            foreach (Material m in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (m == null || m.shader == null || m.shader.name != "Custom/Creature" || !m.HasProperty("_NoiseGlowTex")) continue;
                if (m.GetTexture("_NoiseGlowTex") == null || m.GetFloat("_NoiseGlowEnabled") < 0.5f) continue;
                _spark = m;
                break;
            }
            if (_spark != null)
                foreach (string k in _spark.shaderKeywords) if (k.ToUpperInvariant().Contains("NOISE")) SparkKeywords.Add(k);
            if (SparkKeywords.Count == 0) SparkKeywords.Add("_NOISEGLOWENABLED_ON");
            if (Plugin.Instance != null && _spark != null)
                Plugin.Instance.Log("Raven looks: sparks from " + _spark.name + " (" + _spark.GetTexture("_NoiseGlowTex").name + "), keywords " + string.Join(" ", _spark.shaderKeywords));
            return _spark;
        }

        internal static Texture2D NewPic(int w, int h)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // a picture of the zones themselves (one flat colour each), for «Показати зони»
        private static readonly Dictionary<bool, Texture2D> ZonePics = new Dictionary<bool, Texture2D>();
        internal static Texture2D ZonePic(bool munin)
        {
            Texture2D t;
            if (ZonePics.TryGetValue(munin, out t) && t != null) return t;
            int W, H; string why;
            byte[] z = LookDiag.Zones(munin, out W, out H, out why);
            if (z == null) return null;
            Source s = SourceOf(munin);
            t = NewPic(W, H);
            t.filterMode = FilterMode.Point;
            Color32[] px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) { Color32 c = LookDiag.ZoneColor(z[i] == 0 ? 1 : z[i]); c.a = s != null ? s.Main[i].a : (byte)255; px[i] = c; }
            t.SetPixels32(px);
            t.Apply(true);
            ZonePics[munin] = t;
            return t;
        }

        private static float Lum(Color32 c) { return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f; }
        private static byte B(float v) { return (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255); }
    }
}
#endif

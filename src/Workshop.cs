#if RAVEN_ITEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // «Майстерня» (console: raven workshop): own models put together from the game's pieces and items.
    // A model is a list of parts; each part is a prefab of the game (or of a mod) whose meshes are copied, moved,
    // turned, sized and tinted. Nothing of the game is copied out: the files keep only names and numbers, and the
    // meshes are taken from what the game has loaded. One special part, @seat, marks where a raven sits.
    // Files: config/RavenWatch/models/<name>.txt, readable by hand.

    internal class WsPart
    {
        internal string Src = "";
        internal Vector3 Pos, Rot, Scale = Vector3.one;
        internal bool Tinted;
        internal Color Tint = Color.white;
        internal string Mat = "";                     // the look of another piece of the game (its main material)
        internal bool Glow;
        internal Color GlowColor = new Color(1f, 0.55f, 0.2f);
        internal float GlowPower = 1f;
        internal bool Fx;                             // the piece's living effects too (a candle's flame, sparks, its light)

        internal string LookKey()
        {
            return (Tinted ? ColorUtility.ToHtmlStringRGBA(Tint) : "") + "|" + Mat + "|"
                + (Glow ? ColorUtility.ToHtmlStringRGB(GlowColor) + GlowPower.ToString("0.###", CultureInfo.InvariantCulture) : "");
        }

        internal WsPart Clone()
        {
            WsPart p = new WsPart();
            p.Src = Src; p.Pos = Pos; p.Rot = Rot; p.Scale = Scale; p.Tinted = Tinted; p.Tint = Tint;
            p.Mat = Mat; p.Glow = Glow; p.GlowColor = GlowColor; p.GlowPower = GlowPower; p.Fx = Fx;
            return p;
        }
    }

    internal class WsModel
    {
        internal string Name = "", Note = "", File = "";
        internal List<WsPart> Parts = new List<WsPart>();
        // marked for the mod: "perch" = a raven perch piece for the hammer (Perches.cs); its prefab id and its cost
        internal string Use = "", Id = "", Cost = "";
        internal float PerDay; // a perch's strength a game day for each raven (0: the config's default)
        internal string Station = ""; // the crafting station it is built at (a piece's prefab: piece_workbench, forge, piece_magetable…); empty: the workbench
        internal string Slot = "";  // a thing for a raven (use: gear): "back" or "legs" (RavenGear)
        // an item (use: item, WsItems): the item whose look it becomes (empty: a new item), a new one's stack and weight
        internal string Replaces = "";
        internal string Follow = "";
        internal string Ready = "";   // «✓ Готово для мода»: who marked it and when (packed by DevKit.Pack; empty: not yet)  // a thing on a raven's neck: "" moves with the neck, "body" carried by the body (RavenGear)
        internal int Stack;
        internal float Weight;
        internal string Mount = ""; // "wall": hung on a wall (origin = the point on the wall, the wall is the plane Z = 0, the model in front, +Z)
        internal bool Wall { get { return Mount == "wall"; } }
        // the raven's legs on the perch (PerchPose): left hip, knee, ankle, toes; right the same; the body's lean
        internal float[] Pose = new float[PerchPose.Count];
        internal float[] Stand = new float[PerchPose.Count]; // standing on the bar (after landing, now and then)
        internal int Seats { get { int n = 0; foreach (WsPart p in Parts) if (p.Src == WsFiles.Seat) n++; return n; } }
    }

    // ---------------------------------------------------------------- the files

    internal static class WsFiles
    {
        internal const string Seat = "@seat";
        internal const string Box = "@box"; // an unseen box to bump into (size = its scale); a model with any has no boxes of its own parts
        internal const string Light = "@light"; // a light of our own: tint = colour, scale X = reach (m), Y = brightness, Z = flicker (0 none)
        internal static bool Special(string src) { return src == Seat || src == Light || IsBox(src); }
        internal static bool IsBox(string src) { return src != null && src.StartsWith(Box); } // "@box", or "@box:<zone>" (a nest's top / bowl)
        internal static int Saved; // bumped on every save (RavenGear lists the saved things for a raven anew)
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "models"); } }

        internal static List<string> List()
        {
            List<string> l = new List<string>();
            try { if (Directory.Exists(Dir)) foreach (string f in Directory.GetFiles(Dir, "*.txt")) if (!Path.GetFileName(f).StartsWith("_")) l.Add(f); }
            catch (Exception e) { Log("list: " + e.Message); }
            l.Sort(StringComparer.OrdinalIgnoreCase);
            return l;
        }

        internal static string FileFor(string name)
        {
            string s = string.IsNullOrEmpty(name) ? "model" : name.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return Path.Combine(Dir, s + ".txt");
        }

        private static string F(float v) { return (Mathf.Abs(v) < 0.00005f ? 0f : v).ToString("0.####", C); }
        private static string V(Vector3 v) { return F(v.x) + " " + F(v.y) + " " + F(v.z); }

        internal static string ToText(WsModel m)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# RavenWatch — модель з Майстерні (raven workshop). Рядок part: деталь гри, місце (м), поворот (°), розмір, колір.\n");
            sb.Append("name: ").Append(m.Name.Replace("\n", " ")).Append('\n');
            sb.Append("note: ").Append(m.Note.Replace("\n", " ")).Append('\n');
            if (!string.IsNullOrEmpty(m.Use)) sb.Append("use: ").Append(m.Use).Append('\n');
            if (!string.IsNullOrEmpty(m.Id)) sb.Append("id: ").Append(m.Id).Append('\n');
            if (!string.IsNullOrEmpty(m.Cost)) sb.Append("cost: ").Append(m.Cost.Replace("\n", " ")).Append('\n');
            if (m.PerDay > 0f) sb.Append("perday: ").Append(F(m.PerDay)).Append('\n');
            if (!string.IsNullOrEmpty(m.Station)) sb.Append("station: ").Append(m.Station).Append('\n');
            if (!string.IsNullOrEmpty(m.Mount)) sb.Append("mount: ").Append(m.Mount).Append('\n');
            if (!string.IsNullOrEmpty(m.Slot)) sb.Append("slot: ").Append(m.Slot).Append('\n');
            if (!string.IsNullOrEmpty(m.Replaces)) sb.Append("replaces: ").Append(m.Replaces).Append('\n');
            if (!string.IsNullOrEmpty(m.Follow)) sb.Append("follow: ").Append(m.Follow).Append('\n');
            if (!string.IsNullOrEmpty(m.Ready)) sb.Append("ready: ").Append(m.Ready.Replace("\n", " ")).Append('\n');
            if (m.Stack > 0) sb.Append("stack: ").Append(m.Stack).Append('\n');
            if (m.Weight > 0f) sb.Append("weight: ").Append(F(m.Weight)).Append('\n');
            bool posed = false;
            foreach (float v in m.Pose) if (Mathf.Abs(v) > 0.001f) posed = true;
            if (posed)
            {
                sb.Append("pose:");
                foreach (float v in m.Pose) sb.Append(' ').Append(F(v));
                sb.Append('\n');
            }
            bool stood = false;
            foreach (float v in m.Stand) if (Mathf.Abs(v) > 0.001f) stood = true;
            if (stood)
            {
                sb.Append("stand:");
                foreach (float v in m.Stand) sb.Append(' ').Append(F(v));
                sb.Append('\n');
            }
            foreach (WsPart p in m.Parts)
            {
                sb.Append("part ").Append(p.Src).Append(" pos ").Append(V(p.Pos)).Append(" rot ").Append(V(p.Rot)).Append(" scale ").Append(V(p.Scale));
                if (p.Tinted) sb.Append(" tint ").Append(ColorUtility.ToHtmlStringRGB(p.Tint));
                if (!string.IsNullOrEmpty(p.Mat)) sb.Append(" mat ").Append(p.Mat);
                if (p.Glow) sb.Append(" glow ").Append(ColorUtility.ToHtmlStringRGB(p.GlowColor)).Append(' ').Append(F(p.GlowPower));
                if (p.Fx) sb.Append(" fx");
                string label = WsCatalog.Label(p.Src);
                if (!string.IsNullOrEmpty(label)) sb.Append("  # ").Append(label);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        internal static WsModel FromText(string text)
        {
            WsModel m = new WsModel();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("name:")) { m.Name = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("note:")) { m.Note = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("use:")) { m.Use = line.Substring(4).Trim(); continue; }
                if (line.StartsWith("id:")) { m.Id = line.Substring(3).Trim(); continue; }
                if (line.StartsWith("cost:")) { m.Cost = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("mount:")) { m.Mount = line.Substring(6).Trim(); continue; }
                if (line.StartsWith("slot:")) { m.Slot = line.Substring(5).Trim(); continue; }
                if (line.StartsWith("replaces:")) { m.Replaces = line.Substring(9).Trim(); continue; }
                if (line.StartsWith("follow:")) { m.Follow = line.Substring(7).Trim(); continue; }
                if (line.StartsWith("ready:")) { m.Ready = line.Substring(6).Trim(); continue; }
                if (line.StartsWith("stack:")) { int.TryParse(line.Substring(6).Trim(), out m.Stack); continue; }
                if (line.StartsWith("weight:")) { float.TryParse(line.Substring(7).Trim(), NumberStyles.Float, C, out m.Weight); continue; }
                if (line.StartsWith("station:")) { m.Station = line.Substring(8).Trim(); continue; }
                if (line.StartsWith("perday:")) { float.TryParse(line.Substring(7).Trim(), NumberStyles.Float, C, out m.PerDay); continue; }
                if (line.StartsWith("stand:"))
                {
                    string[] sw = line.Substring(6).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < sw.Length && i < m.Stand.Length; i++) float.TryParse(sw[i], NumberStyles.Float, C, out m.Stand[i]);
                    continue;
                }
                if (line.StartsWith("pose:"))
                {
                    string[] pw = line.Substring(5).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < pw.Length && i < m.Pose.Length; i++) float.TryParse(pw[i], NumberStyles.Float, C, out m.Pose[i]);
                    continue;
                }
                if (!line.StartsWith("part ")) continue;
                int hash = line.IndexOf(" #", StringComparison.Ordinal);
                if (hash > 0) line = line.Substring(0, hash);
                string[] w = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (w.Length < 2) continue;
                WsPart p = new WsPart();
                p.Src = w[1];
                for (int i = 2; i < w.Length; i++)
                {
                    if (w[i] == "pos" && i + 3 < w.Length) { p.Pos = Read(w, i + 1); i += 3; }
                    else if (w[i] == "rot" && i + 3 < w.Length) { p.Rot = Read(w, i + 1); i += 3; }
                    else if (w[i] == "scale" && i + 3 < w.Length) { p.Scale = Read(w, i + 1); i += 3; }
                    else if (w[i] == "tint" && i + 1 < w.Length) { Color c; if (ColorUtility.TryParseHtmlString("#" + w[i + 1], out c)) { p.Tint = c; p.Tinted = true; } i += 1; }
                    else if (w[i] == "mat" && i + 1 < w.Length) { p.Mat = w[i + 1]; i += 1; }
                    else if (w[i] == "glow" && i + 2 < w.Length)
                    {
                        Color c; float power;
                        if (ColorUtility.TryParseHtmlString("#" + w[i + 1], out c)) p.GlowColor = c;
                        if (float.TryParse(w[i + 2], NumberStyles.Float, C, out power)) p.GlowPower = power;
                        p.Glow = true; i += 2;
                    }
                    else if (w[i] == "fx") p.Fx = true;
                }
                m.Parts.Add(p);
            }
            return m;
        }

        private static Vector3 Read(string[] w, int i)
        {
            float x, y, z;
            float.TryParse(w[i], NumberStyles.Float, C, out x);
            float.TryParse(w[i + 1], NumberStyles.Float, C, out y);
            float.TryParse(w[i + 2], NumberStyles.Float, C, out z);
            return new Vector3(x, y, z);
        }

        internal static WsModel Load(string file)
        {
            try
            {
                WsModel m = FromText(System.IO.File.ReadAllText(file, Encoding.UTF8));
                m.File = file;
                if (string.IsNullOrEmpty(m.Name)) m.Name = Path.GetFileNameWithoutExtension(file);
                return m;
            }
            catch (Exception e) { Log("load " + file + ": " + e.Message); return null; }
        }

        internal static bool Save(WsModel m)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                // a thing for a raven marked before it had a name got the stand-in id ("perch"): its own from the name now
                // (perches keep theirs: a built one in the world goes by it)
                if ((m.Use == RavenGear.Use || m.Use == WsItems.Use) && !string.IsNullOrEmpty(m.Name) && (string.IsNullOrEmpty(m.Id) || m.Id == Perches.IdFor(""))) m.Id = Perches.IdFor(m.Name);
                string file = FileFor(m.Name);
                System.IO.File.WriteAllText(file, ToText(m), new UTF8Encoding(false));
                if (!string.IsNullOrEmpty(m.File) && !string.Equals(Path.GetFullPath(m.File), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(m.File))
                    System.IO.File.Delete(m.File); // renamed: the old file goes
                m.File = file;
                Saved++; // the book's list of things for a raven reads the files again
                return true;
            }
            catch (Exception e) { Log("save: " + e.Message); return false; }
        }

        internal static void Log(string s) { if (Plugin.Instance != null) Plugin.Instance.Log("Workshop " + s); }

        // what a saved model is for ("use:"), read from the top of its file (until its parts begin), kept by the file's time
        private sealed class Head { public DateTime At; public string Use = ""; public bool Ready; }
        private static readonly Dictionary<string, Head> Heads = new Dictionary<string, Head>();
        private static Head HeadOf(string file)
        {
            try
            {
                DateTime at = System.IO.File.GetLastWriteTimeUtc(file);
                Head h;
                if (Heads.TryGetValue(file, out h) && h.At == at) return h;
                h = new Head { At = at };
                foreach (string raw in System.IO.File.ReadAllLines(file, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("part ")) break;
                    if (line.StartsWith("use:")) h.Use = line.Substring(4).Trim();
                    else if (line.StartsWith("ready:")) h.Ready = line.Substring(6).Trim().Length > 0;
                }
                Heads[file] = h;
                return h;
            }
            catch { return new Head(); }
        }
        internal static string UseOf(string file) { return HeadOf(file).Use; }
        internal static bool ReadyOf(string file) { return HeadOf(file).Ready; }
    }

    // ---------------------------------------------------------------- what can be taken: every prefab with a mesh

    internal static class WsCatalog
    {
        internal const int CatAll = 0, CatBuild = 1, CatItem = 2, CatWorld = 3, CatCreature = 4, CatAsset = 5, CatFx = 6;
        internal static readonly string[] Cats = { "Усе", "Будівлі", "Речі", "Світ", "Істоти", "Ассети", "Ефекти" };

        // a part that is only a piece's living effect (its flame, sparks, light), without the piece: "fx/<prefab>"
        internal const string FxPrefix = "fx/";
        internal static bool FxOnly(string src) { return src != null && src.StartsWith(FxPrefix, StringComparison.Ordinal); }

        internal class Entry
        {
            internal string Name, Label, Mod, Search;
            internal int Cat;
            internal Sprite Icon;
            internal GameObject Prefab;
        }

        internal static readonly List<Entry> All = new List<Entry>();
        internal static readonly List<string> Mods = new List<string>(); // "Гра" first, then the mods found
        private static readonly Dictionary<string, Entry> ByName = new Dictionary<string, Entry>();
        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();
        internal static readonly HashSet<string> Vanilla = new HashSet<string>(); // the game's own, seen before the mods add theirs
        private static ZNetScene _builtFor;

        internal const string Game = "Гра", OtherMod = "Інший мод";

        internal static void SnapshotVanilla(ZNetScene s)
        {
            if (s == null || Vanilla.Count > 0) return;
            foreach (GameObject g in s.m_prefabs) if (g != null) Vanilla.Add(g.name);
            foreach (GameObject g in s.m_nonNetViewPrefabs) if (g != null) Vanilla.Add(g.name);
        }

        internal static bool Ready { get { return _builtFor != null && _builtFor == ZNetScene.instance; } }

        internal static void Build()
        {
            ZNetScene s = ZNetScene.instance;
            if (s == null || Ready) return;
            long t0 = Slow.Start();
            All.Clear(); ByName.Clear(); Mods.Clear();
            Dictionary<string, string> modOf = JotunnMods();
            HashSet<string> modNames = new HashSet<string>();
            int fxCount = 0;
            List<GameObject> prefabs = new List<GameObject>(s.m_prefabs);
            prefabs.AddRange(s.m_nonNetViewPrefabs);
            foreach (GameObject g in prefabs)
            {
                if (g == null || ByName.ContainsKey(g.name) || ByName.ContainsKey(FxPrefix + g.name)) continue;
                bool hasLook = HasLook(g), hasFx = g.GetComponent<Player>() == null && LivingFx(g);
                if (!hasLook && !hasFx) continue;
                Entry e = new Entry();
                e.Name = g.name; e.Prefab = g;
                Piece piece = g.GetComponent<Piece>();
                ItemDrop item = g.GetComponent<ItemDrop>();
                string token = null;
                if (piece != null) { e.Cat = CatBuild; token = piece.m_name; e.Icon = piece.m_icon; }
                else if (item != null)
                {
                    e.Cat = CatItem;
                    if (item.m_itemData != null && item.m_itemData.m_shared != null)
                    {
                        token = item.m_itemData.m_shared.m_name;
                        Sprite[] icons = item.m_itemData.m_shared.m_icons;
                        if (icons != null && icons.Length > 0) e.Icon = icons[0];
                    }
                }
                else if (g.GetComponent<Character>() != null) { e.Cat = CatCreature; Character c = g.GetComponent<Character>(); token = c.m_name; }
                else
                {
                    e.Cat = CatWorld;
                    HoverText ht = g.GetComponent<HoverText>(); if (ht != null) token = ht.m_text;
                }
                string local = "";
                if (!string.IsNullOrEmpty(token) && Localization.instance != null) local = Localization.instance.Localize(token);
                if (local == token || (local != null && local.StartsWith("["))) local = "";
                e.Label = local ?? "";
                string mod;
                if (modOf.TryGetValue(g.name, out mod)) e.Mod = mod;
                else e.Mod = Vanilla.Count == 0 || Vanilla.Contains(g.name) ? Game : OtherMod;
                if (e.Mod != Game) { modNames.Add(e.Mod); continue; } // the game's own pieces only: no model may lean on another mod
                e.Search = (e.Name + " " + e.Label + " " + e.Mod).ToLowerInvariant();
                if (hasLook) { All.Add(e); ByName[e.Name] = e; }
                if (hasFx)
                {
                    Entry f = new Entry();
                    f.Name = FxPrefix + g.name; f.Prefab = g; f.Cat = CatFx; f.Icon = e.Icon; f.Mod = e.Mod;
                    f.Label = e.Label.Length > 0 ? e.Label + " — ефект" : "";
                    f.Search = (f.Name + " " + f.Label + " ефект").ToLowerInvariant();
                    All.Add(f); ByName[f.Name] = f; fxCount++;
                }
            }
            All.Sort((a, b) => string.Compare(a.Label.Length > 0 ? a.Label : a.Name, b.Label.Length > 0 ? b.Label : b.Name, StringComparison.CurrentCultureIgnoreCase));
            Mods.Add(Game);
            List<string> ms = new List<string>(modNames);
            ms.Sort(StringComparer.OrdinalIgnoreCase);
            Mods.AddRange(ms);
            _builtFor = s;
            Slow.End(t0, "workshop catalog");
            int meshes = 0, readable = 0;
            foreach (Entry e in All) foreach (Renderer r in Looks(e.Prefab))
            {
                Mesh mesh = r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                meshes++; if (mesh != null && mesh.isReadable) readable++;
            }
            WsFiles.Log("catalog: " + fxCount + " living effects");
            WsFiles.Log("catalog: " + All.Count + " game pieces with a look (mods left out: " + string.Join(", ", ms.ToArray()) + "); meshes readable " + readable + " of " + meshes);
            Assets();
        }

        // the outside models (tab «Ассети»): read again from their folder, the game's pieces kept
        internal static void Assets()
        {
            All.RemoveAll(e => e.Cat == CatAsset);
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, Entry> kv in ByName) if (kv.Value.Cat == CatAsset) gone.Add(kv.Key);
            foreach (string k in gone) ByName.Remove(k);
            foreach (Entry e in WsAssets.Scan()) { All.Add(e); ByName[e.Name] = e; }
        }

        internal static Entry Find(string name) { Entry e; return name != null && ByName.TryGetValue(name, out e) ? e : null; }

        internal static string Label(string src)
        {
            if (src == WsFiles.Seat) return "місце ворона";
            if (src == WsFiles.Light) return "світло (розмір X — дальність, Y — яскравість, Z — мерехтіння; колір — відтінок)";
            if (WsFiles.IsBox(src)) return "колайдер (невидимий у грі)" + (src.Length > WsFiles.Box.Length + 1 ? ", зона " + src.Substring(WsFiles.Box.Length + 1) : "");
            Entry e = Find(src);
            if (e == null) return "";
            return (e.Label.Length > 0 ? e.Label : e.Name) + (e.Mod != Game ? " (" + e.Mod + ")" : "");
        }

        // Jotunn knows which mod added what (OdinArchitect and others); everything else a mod added is "інший мод"
        private static Dictionary<string, string> JotunnMods()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            Take(d, "Jotunn.Managers.PrefabManager", "Prefabs", "Prefab");
            Take(d, "Jotunn.Managers.PieceManager", "Pieces", "PiecePrefab");
            Take(d, "Jotunn.Managers.ItemManager", "Items", "ItemPrefab");
            return d;
        }

        private static void Take(Dictionary<string, string> d, string type, string member, string prefabProp)
        {
            try
            {
                Type t = AccessTools.TypeByName(type);
                if (t == null) return;
                PropertyInfo ip = AccessTools.Property(t, "Instance");
                object inst = ip != null ? ip.GetValue(null, null) : null;
                if (inst == null) return;
                object coll = null;
                FieldInfo f = AccessTools.Field(t, member);
                if (f != null) coll = f.GetValue(inst);
                else { PropertyInfo p = AccessTools.Property(t, member); if (p != null) coll = p.GetValue(inst, null); }
                IEnumerable items = coll is IDictionary ? ((IDictionary)coll).Values : coll as IEnumerable;
                if (items == null) return;
                foreach (object ce in items)
                {
                    if (ce == null) continue;
                    PropertyInfo pp = AccessTools.Property(ce.GetType(), prefabProp);
                    PropertyInfo sp = AccessTools.Property(ce.GetType(), "SourceMod");
                    GameObject g = pp != null ? pp.GetValue(ce, null) as GameObject : null;
                    object src = sp != null ? sp.GetValue(ce, null) : null;
                    if (g == null || src == null) continue;
                    PropertyInfo np = AccessTools.Property(src.GetType(), "Name");
                    string name = np != null ? np.GetValue(src, null) as string : null;
                    if (!string.IsNullOrEmpty(name) && name != "Jotunn") d[g.name] = name;
                }
            }
            catch (Exception e) { WsFiles.Log("mods from " + type + ": " + e.Message); }
        }

        // the renderers that make the look: the near model only (not the far LODs), the whole piece (not worn or broken)
        private static List<Renderer> Looks(GameObject g)
        {
            List<Renderer> list = new List<Renderer>();
            HashSet<Renderer> skip = new HashSet<Renderer>();
            foreach (LODGroup lg in g.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lg.GetLODs();
                for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (Renderer r in lods[i].renderers) if (r != null) skip.Add(r);
            }
            WearNTear wnt = g.GetComponent<WearNTear>();
            if (wnt != null)
                foreach (GameObject state in new GameObject[] { wnt.m_worn, wnt.m_broken })
                    if (state != null && state != wnt.m_new) foreach (Renderer r in state.GetComponentsInChildren<Renderer>(true)) skip.Add(r);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || skip.Contains(r) || !r.enabled || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;
                if (r is MeshRenderer) { MeshFilter mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; }
                else if (r is SkinnedMeshRenderer) { if (((SkinnedMeshRenderer)r).sharedMesh == null) continue; }
                else continue;
                list.Add(r);
            }
            return list;
        }

        private static bool HasLook(GameObject g)
        {
            try { return Looks(g).Count > 0; }
            catch { return false; }
        }

        private static readonly Dictionary<string, Material> MatCache = new Dictionary<string, Material>();

        // the main material of a piece: its biggest near renderer's first one (the wood of a pole, the iron of a band)
        internal static Material MaterialOf(string src)
        {
            Material m = null;
            if (string.IsNullOrEmpty(src)) return null;
            if (MatCache.TryGetValue(src, out m)) return m;
            if (WsAssets.Is(src)) { m = WsAssets.MainMaterial(src); if (m != null) MatCache[src] = m; return m; }
            Entry e = Find(src);
            // the catalog not made yet (a perch piece built as the world loads): straight from the game's prefabs
            GameObject g = e != null ? e.Prefab : (ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(src) : null);
            if (g == null && ZNetScene.instance == null) return null; // asked too early: not remembered as missing
            if (g != null)
            {
                float best = -1f;
                foreach (Renderer r in Looks(g))
                {
                    Mesh mesh = r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                    float size = mesh != null ? mesh.bounds.size.sqrMagnitude : 0f;
                    if (size > best && r.sharedMaterials[0] != null) { best = size; m = r.sharedMaterials[0]; }
                }
            }
            MatCache[src] = m;
            return m;
        }

        // a still copy of a prefab's look (meshes and materials only), kept switched off; parts are clones of it
        internal static GameObject Template(string src)
        {
            GameObject t;
            if (Templates.TryGetValue(src, out t) && t != null) return t;
            if (WsAssets.Is(src)) return WsAssets.Template(src);
            t = src == WsFiles.Seat ? SeatLook() : src == WsFiles.Light ? LightMarkLook() : WsFiles.IsBox(src) ? BoxLook() : FxOnly(src) ? EmptyLook(src) : CopyLook(src);
            if (t != null) Templates[src] = t;
            return t;
        }

        private static GameObject EmptyLook(string src)
        {
            if (FxTemplate(src) == null) return null; // no such piece (or it shows nothing living)
            GameObject root = new GameObject("WS_" + src);
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            return root;
        }

        private static GameObject CopyLook(string src)
        {
            Entry e = Find(src);
            GameObject g = e != null ? e.Prefab : (ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(src) : null);
            if (g == null) return null;
            GameObject root = new GameObject("WS_" + src);
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            foreach (Renderer r in Looks(g))
            {
                Mesh mesh = r is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)r).sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                GameObject part = new GameObject(r.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = g.transform.InverseTransformPoint(r.transform.position);
                part.transform.localRotation = Quaternion.Inverse(g.transform.rotation) * r.transform.rotation;
                Vector3 a = r.transform.lossyScale, b = g.transform.lossyScale;
                part.transform.localScale = new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z));
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return root;
        }

        // ---- the living effects of a piece (a candle's flame, a fire's sparks and light): its particle systems and lights
        // copied with their children, everything else taken off them (no fire damage, warmth, sounds or game logic).
        // A fire's flames are taken from what it shows when lit; the meshes already in the look are not copied twice.
        private static readonly Dictionary<string, GameObject> Fx = new Dictionary<string, GameObject>();

        internal static bool HasFx(string src) { return FxTemplate(src) != null; }

        internal static GameObject FxTemplate(string src)
        {
            GameObject t;
            if (FxOnly(src)) src = src.Substring(FxPrefix.Length);
            if (string.IsNullOrEmpty(src) || WsFiles.Special(src) || WsAssets.Is(src)) return null;
            if (Fx.TryGetValue(src, out t)) return t;
            t = null;
            try { t = CopyFx(src); }
            catch (Exception e) { WsFiles.Log("effects of " + src + ": " + e.Message); }
            if (t != null || ZNetScene.instance != null) Fx[src] = t; // (before the game is up: ask again later)
            return t;
        }

        private static GameObject CopyFx(string src)
        {
            Entry e = Find(src);
            GameObject g = e != null ? e.Prefab : (ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(src) : null);
            if (g == null) return null;
            HashSet<Renderer> look = new HashSet<Renderer>(Looks(g));
            // where to look: a fire's lit states (the game turns them on), else the whole piece
            List<Transform> roots = new List<Transform>();
            Fireplace fp = g.GetComponent<Fireplace>();
            if (fp != null)
                foreach (GameObject o in new[] { fp.m_enabledObject, fp.m_enabledObjectHigh })
                    if (o != null && !roots.Contains(o.transform)) roots.Add(o.transform);
            if (roots.Count == 0) roots.Add(g.transform);

            GameObject root = null;
            foreach (Transform top in roots)
                foreach (Transform n in top.GetComponentsInChildren<Transform>(true))
                {
                    if (n == g.transform || !IsFx(n) || !Shown(n, top)) continue;
                    bool under = false; // its parent is taken already (with it)
                    for (Transform up = n.parent; up != null && up != top.parent; up = up.parent) if (up != g.transform && IsFx(up) && Shown(up, top)) under = true;
                    if (under) continue;
                    if (root == null)
                    {
                        root = new GameObject("WSFX_" + src);
                        root.SetActive(false);
                        root.hideFlags = HideFlags.HideAndDontSave;
                        UnityEngine.Object.DontDestroyOnLoad(root);
                    }
                    GameObject c = UnityEngine.Object.Instantiate(n.gameObject, root.transform, false);
                    c.SetActive(true);
                    c.transform.localPosition = g.transform.InverseTransformPoint(n.position);
                    c.transform.localRotation = Quaternion.Inverse(g.transform.rotation) * n.rotation;
                    Vector3 a = n.lossyScale, b = g.transform.lossyScale;
                    c.transform.localScale = new Vector3(a.x / Mathf.Max(0.0001f, b.x), a.y / Mathf.Max(0.0001f, b.y), a.z / Mathf.Max(0.0001f, b.z));
                    Strip(n, c.transform, look);
                }
            if (root != null) WsFiles.Log("effects of " + src + ": " + root.GetComponentsInChildren<ParticleSystem>(true).Length + " particle system(s), " + root.GetComponentsInChildren<Light>(true).Length + " light(s)");
            return root;
        }

        private static bool IsFx(Transform t) { return t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null; }

        // for the tab «Ефекти»: a piece that shows something living all the time (a light, or particles that go on and
        // on) — not a flash of a blow or a drunk potion, which plays once and is gone
        private static bool LivingFx(GameObject g)
        {
            try
            {
                List<Transform> roots = new List<Transform>();
                Fireplace fp = g.GetComponent<Fireplace>();
                if (fp != null) foreach (GameObject o in new[] { fp.m_enabledObject, fp.m_enabledObjectHigh }) if (o != null) roots.Add(o.transform);
                if (roots.Count == 0) roots.Add(g.transform);
                foreach (Transform top in roots)
                {
                    foreach (Light l in top.GetComponentsInChildren<Light>(true)) if (l.enabled && l.transform != g.transform && Shown(l.transform, top)) return true;
                    foreach (ParticleSystem ps in top.GetComponentsInChildren<ParticleSystem>(true)) if (ps.main.loop && ps.transform != g.transform && Shown(ps.transform, top)) return true;
                }
            }
            catch { }
            return false;
        }

        // turned on in the piece (up to the lit state, which the game turns on itself)
        private static bool Shown(Transform t, Transform top)
        {
            for (Transform x = t; x != null && x != top; x = x.parent) if (!x.gameObject.activeSelf) return false;
            return true;
        }

        // only the look of the effect is kept: particles, lights (with the game's flicker and distance fade), and meshes
        // that are not in the piece's look already (a flame drawn as a mesh)
        private static void Strip(Transform from, Transform copy, HashSet<Renderer> look)
        {
            Transform[] a = from.GetComponentsInChildren<Transform>(true), b = copy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < b.Length; i++)
            {
                Renderer was = i < a.Length ? a[i].GetComponent<Renderer>() : null;
                bool keepMesh = was is MeshRenderer && !look.Contains(was);
                for (int pass = 0; pass < 3; pass++)
                    foreach (Component k in b[i].GetComponents<Component>())
                    {
                        if (k == null || k is Transform || k is ParticleSystem || k is ParticleSystemRenderer || k is Light || k is LightFlicker || k is LightLod) continue;
                        if (keepMesh && (k is MeshRenderer || k is MeshFilter)) continue;
                        try { UnityEngine.Object.DestroyImmediate(k); } catch { } // one that another needs goes in the next pass
                    }
                ParticleSystem ps = b[i].GetComponent<ParticleSystem>();
                if (ps != null) { ParticleSystem.MainModule m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; } // sized with the part
            }
        }

        // the effects of a part put under it (child "fx"); null when the piece has none
        internal static GameObject AddFx(Transform holder, string src) { return AddFx(holder, src, null); }

        // with a part: an effect-only part's tint colours its particles and light
        internal static GameObject AddFx(Transform holder, string src, WsPart p)
        {
            GameObject t = FxTemplate(src);
            if (t == null) return null;
            GameObject c = UnityEngine.Object.Instantiate(t, holder, false);
            c.name = "fx"; c.hideFlags = HideFlags.None;
            if (FxOnly(src)) Center(c.transform);
            WsFx w = c.AddComponent<WsFx>();
            if (p != null && FxOnly(p.Src) && p.Tinted) w.Recolor(true, p.Tint);
            c.SetActive(true);
            return c;
        }

        // the effect's own flame (its first particles, else its light) moved to the part's point
        private static void Center(Transform c)
        {
            Transform at = null;
            ParticleSystem ps = c.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null) at = ps.transform;
            else { Light l = c.GetComponentInChildren<Light>(true); if (l != null) at = l.transform; }
            if (at == null || at == c) return;
            Vector3 off = c.InverseTransformPoint(at.position);
            c.localPosition -= c.localRotation * Vector3.Scale(c.localScale, off);
        }

        private static Material _mark;
        internal static Material Mark
        {
            get
            {
                if (_mark == null) { _mark = new Material(Shader.Find("Sprites/Default")); _mark.hideFlags = HideFlags.HideAndDontSave; }
                return _mark;
            }
        }

        // the raven's seat: a small gold ball with a short beak-line showing where the raven faces
        // a light shown in the Workshop: a small warm ball (the light itself is made from the part: Perches.MakeLight)
        private static GameObject LightMarkLook()
        {
            GameObject root = new GameObject("WS_light");
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 0.08f;
            MeshRenderer mr = ball.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mark;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", new Color(1f, 0.85f, 0.4f, 1f));
            mr.SetPropertyBlock(pb);
            return root;
        }

        // a collider shown in the Workshop: a see-through cube of 1 m (its scale is the box's size)
        private static GameObject BoxLook()
        {
            GameObject root = new GameObject("WS_box");
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(root.transform, false);
            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mark;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", new Color(0.3f, 0.8f, 1f, 0.35f));
            mr.SetPropertyBlock(pb);
            return root;
        }

        private static GameObject SeatLook()
        {
            GameObject root = new GameObject("WS_seat");
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 0.05f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = Mark;
            ball.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            nose.transform.localScale = new Vector3(0.012f, 0.012f, 0.07f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = Mark;
            nose.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                MaterialPropertyBlock pb = new MaterialPropertyBlock();
                pb.SetColor("_Color", new Color(1f, 0.8f, 0.2f, 1f));
                mr.SetPropertyBlock(pb);
            }
            return root;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class WsVanillaPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ZNetScene __instance) { WsCatalog.SnapshotVanilla(__instance); }
    }

    // ---------------------------------------------------------------- a model made of copies

    internal class WsView
    {
        private readonly Transform _root;
        private readonly int _layer;
        private readonly bool _seats;
        private readonly List<GameObject> _parts = new List<GameObject>();
        private readonly List<string> _srcs = new List<string>();
        private readonly List<string> _looks = new List<string>();

        internal WsView(Transform root, int layer, bool seats) { _root = root; _layer = layer; _seats = seats; }

        internal int Count { get { return _parts.Count; } }
        internal GameObject Part(int i) { return i >= 0 && i < _parts.Count ? _parts[i] : null; }

        internal void Sync(WsModel m)
        {
            for (int i = 0; i < m.Parts.Count; i++)
            {
                WsPart p = m.Parts[i];
                if (i >= _parts.Count) { _parts.Add(null); _srcs.Add(null); _looks.Add(""); }
                if (i >= _fxLooks.Count) _fxLooks.Add("");
                if (_parts[i] == null || _srcs[i] != p.Src) { Kill(i); _parts[i] = Make(p.Src); _srcs[i] = p.Src; _looks[i] = ""; _fxLooks[i] = ""; }
                Transform t = _parts[i].transform;
                t.localPosition = p.Pos; t.localRotation = Quaternion.Euler(p.Rot); t.localScale = p.Scale;
                string look = p.LookKey();
                if (!WsFiles.Special(p.Src) && look != _looks[i])
                {
                    WsLook l = _parts[i].GetComponent<WsLook>();
                    if (l != null) l.Apply(p);
                    _looks[i] = look;
                }
                Transform fx = _parts[i].transform.Find("fx");
                bool only = WsCatalog.FxOnly(p.Src);
                if (fx != null && !only && fx.gameObject.activeSelf != p.Fx) fx.gameObject.SetActive(p.Fx);
                if (fx != null && only && look != _fxLooks[i]) { WsFx w = fx.GetComponent<WsFx>(); if (w != null) w.Recolor(p.Tinted, p.Tint); _fxLooks[i] = look; }
                _parts[i].SetActive(_seats || !WsFiles.Special(p.Src)); // (the seats and the boxes: shown with the marks)
            }
            for (int i = _parts.Count - 1; i >= m.Parts.Count; i--) { Kill(i); _parts.RemoveAt(i); _srcs.RemoveAt(i); _looks.RemoveAt(i); }
            while (_fxLooks.Count > _parts.Count) _fxLooks.RemoveAt(_fxLooks.Count - 1);
        }

        private readonly List<string> _fxLooks = new List<string>(); // the tint an effect-only part's effect has now

        private GameObject Make(string src)
        {
            GameObject holder = new GameObject("part " + src);
            holder.transform.SetParent(_root, false);
            GameObject t = WsCatalog.Template(src);
            if (t != null)
            {
                GameObject c = UnityEngine.Object.Instantiate(t, holder.transform, false);
                c.name = "look"; c.hideFlags = HideFlags.None;
                c.SetActive(true);
                GameObject fx = WsCatalog.AddFx(holder.transform, src); // shown when the part asks for it (always when it is only that)
                if (fx != null && !WsCatalog.FxOnly(src)) fx.SetActive(false);
                if (fx != null && WsCatalog.FxOnly(src)) FxMark(fx.transform);
            }
            foreach (Transform x in holder.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = _layer;
            if (!WsFiles.Special(src)) holder.AddComponent<WsLook>().Init();
            return holder;
        }

        // a small ball where an effect-only part is (sparks may be gone for a moment; it is clicked and moved by it)
        private static void FxMark(Transform fx)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.name = "mark";
            ball.transform.SetParent(fx, false);
            ball.transform.localScale = Vector3.one * 0.04f;
            MeshRenderer mr = ball.GetComponent<MeshRenderer>();
            mr.sharedMaterial = WsCatalog.Mark;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", new Color(0.4f, 0.85f, 1f, 1f));
            mr.SetPropertyBlock(pb);
        }

        private void Kill(int i) { if (_parts[i] != null) UnityEngine.Object.Destroy(_parts[i]); _parts[i] = null; }

        internal void Clear() { for (int i = 0; i < _parts.Count; i++) Kill(i); _parts.Clear(); _srcs.Clear(); _looks.Clear(); _fxLooks.Clear(); }

        internal Bounds? BoundsOf(int i)
        {
            GameObject g = Part(i);
            if (g == null) return null;
            Bounds? b = null;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer) continue; // a flame's sparks are no size of the part
                if (b == null) b = r.bounds;
                else { Bounds x = b.Value; x.Encapsulate(r.bounds); b = x; }
            }
            return b;
        }

        internal Bounds? BoundsAll()
        {
            Bounds? b = null;
            for (int i = 0; i < _parts.Count; i++)
            {
                Bounds? x = BoundsOf(i);
                if (x == null) continue;
                if (b == null) b = x;
                else { Bounds y = b.Value; y.Encapsulate(x.Value); b = y; }
            }
            return b;
        }

        // the part under a ray: the nearest one whose renderers it hits (by their boxes)
        internal int Pick(Ray ray)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < _parts.Count; i++)
            {
                GameObject g = _parts[i];
                if (g == null || !g.activeSelf) continue;
                foreach (Renderer r in g.GetComponentsInChildren<Renderer>(false))
                {
                    float d;
                    if (r.bounds.IntersectRay(ray, out d) && d < bestD) { bestD = d; best = i; }
                }
            }
            return best;
        }
    }

    // A part's living effects (WsCatalog.AddFx): a light reaches as far as the part is big (its range does not follow the
    // size by itself), measured again when the size changes.
    internal class WsFx : MonoBehaviour
    {
        private Light[] _l;
        private float[] _range;
        private Color[] _lightColor;
        private ParticleSystem[] _ps;
        private ParticleSystem.MinMaxGradient[] _start, _life;
        private float _k = -1f;

        internal static bool In(Transform t, Transform top)
        {
            for (Transform x = t; x != null && x != top; x = x.parent) if (x.GetComponent<WsFx>() != null) return true;
            return false;
        }

        // what it had from the game (the clone of a built piece reads it again: these are not kept in the prefab)
        private void Init()
        {
            if (_l != null) return;
            _l = GetComponentsInChildren<Light>(true);
            _range = new float[_l.Length]; _lightColor = new Color[_l.Length];
            for (int i = 0; i < _l.Length; i++) { _range[i] = _l[i].range; _lightColor[i] = _l[i].color; }
            _ps = GetComponentsInChildren<ParticleSystem>(true);
            _start = new ParticleSystem.MinMaxGradient[_ps.Length]; _life = new ParticleSystem.MinMaxGradient[_ps.Length];
            for (int i = 0; i < _ps.Length; i++) { _start[i] = _ps[i].main.startColor; _life[i] = _ps[i].colorOverLifetime.color; }
        }

        // the effect in another colour: each colour of it takes the tint's hue at its own brightness (a flame stays
        // a flame, only blue); the light takes the tint. Off: the game's colours back.
        internal void Recolor(bool tinted, Color tint)
        {
            Init();
            for (int i = 0; i < _l.Length; i++) if (_l[i] != null) _l[i].color = tinted ? tint : _lightColor[i];
            for (int i = 0; i < _ps.Length; i++)
            {
                if (_ps[i] == null) continue;
                ParticleSystem.MainModule m = _ps[i].main;
                m.startColor = tinted ? Tone(_start[i], tint) : _start[i];
                ParticleSystem.ColorOverLifetimeModule col = _ps[i].colorOverLifetime;
                if (col.enabled) col.color = tinted ? Tone(_life[i], tint) : _life[i];
            }
        }

        private static Color Tone(Color c, Color t)
        {
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return new Color(t.r * v, t.g * v, t.b * v, c.a);
        }

        private static Gradient Tone(Gradient g, Color t)
        {
            if (g == null) return null;
            GradientColorKey[] keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Tone(keys[i].color, t);
            Gradient n = new Gradient();
            n.mode = g.mode;
            n.SetKeys(keys, g.alphaKeys);
            return n;
        }

        private static ParticleSystem.MinMaxGradient Tone(ParticleSystem.MinMaxGradient g, Color t)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Tone(g.color, t));
                case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Tone(g.colorMin, t), Tone(g.colorMax, t));
                case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Tone(g.gradientMin, t), Tone(g.gradientMax, t));
                default:
                    ParticleSystem.MinMaxGradient r = new ParticleSystem.MinMaxGradient(Tone(g.gradient, t));
                    r.mode = g.mode; // a gradient, or a random colour from it
                    return r;
            }
        }

        private void LateUpdate()
        {
            Init();
            Vector3 s = transform.lossyScale;
            float k = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            if (Mathf.Abs(k - _k) < 0.001f) return;
            _k = k;
            for (int i = 0; i < _l.Length; i++) if (_l[i] != null) _l[i].range = _range[i] * k;
        }
    }

    // One part's colours: its own materials kept, another piece's material laid over them, glowing copies made when
    // asked (and freed with the part), the tint on top by property block.
    internal class WsLook : MonoBehaviour
    {
        private Renderer[] _r;
        private Material[][] _orig;
        private readonly List<Material> _own = new List<Material>();
        private static bool _toldNoGlow;

        internal void Init()
        {
            List<Renderer> own = new List<Renderer>(); // not its effects (they keep their own colours)
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (!WsFx.In(r.transform, transform)) own.Add(r);
            _r = own.ToArray();
            _orig = new Material[_r.Length][];
            for (int i = 0; i < _r.Length; i++) _orig[i] = _r[i].sharedMaterials;
        }

        internal void Apply(WsPart p)
        {
            Free();
            Material swap = WsCatalog.MaterialOf(p.Mat);
            for (int i = 0; i < _r.Length; i++)
            {
                if (_r[i] == null) continue;
                Material[] mats = (Material[])_orig[i].Clone();
                for (int k = 0; k < mats.Length; k++)
                {
                    if (swap != null) mats[k] = swap;
                    if (p.Glow && mats[k] != null) mats[k] = Glowing(mats[k], p.GlowColor * p.GlowPower);
                }
                _r[i].sharedMaterials = mats;
                if (!p.Tinted) { _r[i].SetPropertyBlock(null); continue; }
                MaterialPropertyBlock pb = new MaterialPropertyBlock();
                pb.SetColor("_Color", p.Tint);
                _r[i].SetPropertyBlock(pb);
            }
        }

        private Material Glowing(Material from, Color c)
        {
            Material m = new Material(from);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            bool any = false;
            foreach (string prop in new[] { "_EmissionColor", "_EmissiveColor" })
                if (m.HasProperty(prop)) { m.SetColor(prop, c); any = true; }
            if (m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") == null) m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            if (!any && !_toldNoGlow) { _toldNoGlow = true; WsFiles.Log("glow: shader " + from.shader.name + " has no emission colour"); }
            _own.Add(m);
            return m;
        }

        private void Free() { foreach (Material m in _own) if (m != null) Destroy(m); _own.Clear(); }

        private void OnDestroy() { Free(); }
    }

    // ---------------------------------------------------------------- the stage: the model alone, filmed by its own camera

    internal static class WsStage
    {
        private const int Size = 512;
        private static GameObject _stage, _modelRoot, _gizmo, _wallGrid;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static readonly List<Light> _lamps = new List<Light>();
        private static int _layer = -1;
        internal static WsView View;
        internal static float Yaw = 200f, Pitch = 18f, Zoom = 1f;
        internal static Vector3 Center = new Vector3(0f, 0.5f, 0f);
        internal static float Radius = 1f;
        internal static bool ShowRaven = true, ShowGrid = true, Spin;
        internal static string Problem = "";
        internal static RenderTexture Texture { get { return _rt; } }
        private static LineRenderer[] _box, _axes;
        private static readonly Color Back = new Color(0.17f, 0.19f, 0.22f, 1f);

        internal static bool Ready { get { return _stage != null; } }

        internal static bool Open()
        {
            if (_stage != null) return true;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; } // the world was unloaded under it
            _layer = RavenFx.FreeLayer();
            if (_layer < 0) { Problem = "Немає вільного шару для картинки."; return false; }
            Problem = "";
            _stage = new GameObject("RavenWatch_WorkshopStage");
            _stage.transform.position = new Vector3(0f, 9300f, 0f);
            _modelRoot = new GameObject("Model");
            _modelRoot.transform.SetParent(_stage.transform, false);
            View = new WsView(_modelRoot.transform, _layer, true);
            _cam = new GameObject("Cam").AddComponent<Camera>();
            _cam.transform.SetParent(_stage.transform, false);
            _cam.cullingMask = 1 << _layer; _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Back; _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.02f; _cam.farClipPlane = 200f; _cam.enabled = false;
            _cam.renderingPath = RenderingPath.Forward; _cam.allowHDR = false; _cam.allowMSAA = false;
            _lamps.Clear();
            Lamp("Key", 1.15f, Color.white, Quaternion.Euler(45f, 150f, 0f));
            Lamp("Fill", 0.55f, new Color(0.8f, 0.85f, 1f), Quaternion.Euler(25f, -40f, 0f));
            Lamp("Back", 0.4f, Color.white, Quaternion.Euler(-30f, 0f, 0f));
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); _rt.antiAliasing = 2; _rt.Create();
            _cam.targetTexture = _rt;
            Grid();
            WallGrid();
            _box = new LineRenderer[1]; _box[0] = Line("Box", new Color(1f, 0.85f, 0.25f), 16);
            _axes = new LineRenderer[3];
            _axes[0] = Line("X", new Color(1f, 0.3f, 0.3f), 2); _axes[1] = Line("Y", new Color(0.35f, 1f, 0.35f), 2); _axes[2] = Line("Z", new Color(0.35f, 0.6f, 1f), 2);
            return true;
        }

        private static void Lamp(string name, float power, Color c, Quaternion turn)
        {
            Light l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(_stage.transform, false); l.type = LightType.Directional; l.intensity = power; l.color = c;
            l.cullingMask = 1 << _layer; l.shadows = LightShadows.None; l.transform.rotation = turn; l.enabled = false;
            _lamps.Add(l);
        }

        private static LineRenderer Line(string name, Color c, int points)
        {
            LineRenderer lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.transform.SetParent(_stage.transform, false);
            lr.gameObject.layer = _layer;
            lr.sharedMaterial = WsCatalog.Mark;
            lr.startColor = lr.endColor = c;
            lr.positionCount = points; lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // the floor: lines every 25 cm, brighter every metre, 2 m each way
        private static void Grid()
        {
            _gizmo = new GameObject("Grid");
            _gizmo.transform.SetParent(_stage.transform, false);
            for (int i = -8; i <= 8; i++)
            {
                float v = i * 0.25f;
                Color c = i % 4 == 0 ? new Color(0.55f, 0.58f, 0.62f) : new Color(0.32f, 0.34f, 0.37f);
                GridLine(new Vector3(v, 0f, -2f), new Vector3(v, 0f, 2f), c);
                GridLine(new Vector3(-2f, 0f, v), new Vector3(2f, 0f, v), c);
            }
            // the model's front (+Z): a blue arrow on the floor, from the middle to the edge
            Color front = new Color(0.35f, 0.6f, 1f);
            GridLine(new Vector3(0f, 0.002f, 0f), new Vector3(0f, 0.002f, 2.3f), front, 0.025f);
            GridLine(new Vector3(0f, 0.002f, 2.3f), new Vector3(-0.25f, 0.002f, 1.95f), front, 0.025f);
            GridLine(new Vector3(0f, 0.002f, 2.3f), new Vector3(0.25f, 0.002f, 1.95f), front, 0.025f);
        }

        // a wall perch's wall: the plane Z = 0, lines every 25 cm, brighter every metre, the mount point marked
        private static void WallGrid()
        {
            GameObject floor = _gizmo;
            _wallGrid = new GameObject("WallGrid");
            _wallGrid.transform.SetParent(_stage.transform, false);
            _gizmo = _wallGrid; // GridLine draws into _gizmo
            for (int i = -8; i <= 8; i++)
            {
                float v = i * 0.25f;
                Color c = i % 4 == 0 ? new Color(0.55f, 0.58f, 0.62f) : new Color(0.32f, 0.34f, 0.37f);
                GridLine(new Vector3(v, -2f, 0f), new Vector3(v, 2f, 0f), c);
                GridLine(new Vector3(-2f, v, 0f), new Vector3(2f, v, 0f), c);
            }
            Color mark = new Color(1f, 0.8f, 0.2f);
            GridLine(new Vector3(-0.1f, 0f, 0.002f), new Vector3(0.1f, 0f, 0.002f), mark, 0.012f);
            GridLine(new Vector3(0f, -0.1f, 0.002f), new Vector3(0f, 0.1f, 0.002f), mark, 0.012f);
            Color front = new Color(0.35f, 0.6f, 1f);
            GridLine(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f), front, 0.02f);
            _wallGrid.SetActive(false);
            _gizmo = floor;
        }

        private static void GridLine(Vector3 a, Vector3 b, Color c) { GridLine(a, b, c, 0.006f); }

        private static void GridLine(Vector3 a, Vector3 b, Color c, float width)
        {
            LineRenderer lr = new GameObject("l").AddComponent<LineRenderer>();
            lr.transform.SetParent(_gizmo.transform, false);
            lr.gameObject.layer = _layer;
            lr.sharedMaterial = WsCatalog.Mark;
            lr.startColor = lr.endColor = c;
            lr.useWorldSpace = false; lr.positionCount = 2;
            lr.SetPosition(0, a); lr.SetPosition(1, b);
            lr.startWidth = lr.endWidth = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
        }

        internal static void Close()
        {
            if (View != null) View.Clear();
            View = null;
            if (_stage != null) UnityEngine.Object.Destroy(_stage);
            _stage = null; _modelRoot = null; _ravens.Clear(); _cam = null; _gizmo = null; _wallGrid = null;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; }
        }

        // the view framed on the whole model
        internal static void Frame()
        {
            Bounds? b = View != null ? View.BoundsAll() : null;
            if (_gearKey != null) { Bounds? rb = RavenBounds(); if (rb != null) { if (b == null) b = rb; else { Bounds x = b.Value; x.Encapsulate(rb.Value); b = x; } } }
            if (b == null) { Center = new Vector3(0f, 0.5f, 0f); Radius = 1f; Zoom = 1f; return; }
            Center = _stage.transform.InverseTransformPoint(b.Value.center);
            Radius = Mathf.Clamp(b.Value.extents.magnitude * 1.1f, 0.25f, 40f);
            Zoom = 1f;
        }

        internal static Ray RayAt(Vector2 viewport) { return _cam != null ? _cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f)) : new Ray(); }

        internal static void Render(WsModel m, int selected)
        {
            if (_stage == null || View == null) return;
            View.Sync(m);
            Raven(m);
            if (_gizmo != null) _gizmo.SetActive(ShowGrid && !m.Wall && m.Use != RavenGear.Use);
            if (_wallGrid != null) _wallGrid.SetActive(ShowGrid && m.Wall);
            Selection(selected);
            if (Spin) Yaw += Time.unscaledDeltaTime * 25f;
            Quaternion look = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 c = _stage.transform.TransformPoint(Center);
            _cam.transform.position = c - look * Vector3.forward * (Radius / Mathf.Tan(15f * Mathf.Deg2Rad)) / Zoom;
            _cam.transform.rotation = look;
            foreach (Light l in _lamps) if (l != null) l.enabled = true;
            try { _cam.Render(); }
            finally { foreach (Light l in _lamps) if (l != null) l.enabled = false; }
        }

        private static void Selection(int i)
        {
            Bounds? b = View.BoundsOf(i);
            GameObject part = View.Part(i);
            bool on = b != null && part != null;
            _box[0].enabled = on;
            foreach (LineRenderer a in _axes) a.enabled = on;
            if (!on) return;
            float w = Mathf.Clamp(Radius * 0.006f, 0.002f, 0.05f);
            Vector3 mn = b.Value.min, mx = b.Value.max;
            Vector3[] p =
            {
                new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mn.z),
                new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mx.y, mn.z),
                new Vector3(mx.x, mx.y, mx.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mx.x, mx.y, mx.z),
                new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mn.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mn.z)
            };
            for (int k = 0; k < p.Length; k++) _box[0].SetPosition(k, p[k]);
            _box[0].startWidth = _box[0].endWidth = w;
            Vector3 o = part.transform.position;
            float len = Mathf.Max(0.12f, Radius * 0.35f);
            Vector3[] dirs = { _stage.transform.right, _stage.transform.up, _stage.transform.forward };
            for (int k = 0; k < 3; k++)
            {
                _axes[k].SetPosition(0, o); _axes[k].SetPosition(1, o + dirs[k] * len);
                _axes[k].startWidth = _axes[k].endWidth = w * 1.6f;
            }
        }

        // a raven of shoulder size on each seat (Hugin on the first, Munin on the second), its legs as the model's
        // pose has them (PerchPose, the same as on the built perch); without a seat one stands beside the model
        private static readonly List<GameObject> _ravens = new List<GameObject>();
        private static void Raven(WsModel m)
        {
            if (m.Use == RavenGear.Use) { GearRaven(m); return; }
            if (_gearKey != null || (_ravens.Count > 0 && _ravens[0] != null && _ravens[0].name != "Raven")) RavenAgain(); // was dressed for a thing
            List<WsPart> seats = new List<WsPart>();
            foreach (WsPart p in m.Parts) if (p.Src == WsFiles.Seat) seats.Add(p);
            int want = ShowRaven ? Mathf.Max(1, seats.Count) : 0;
            while (_ravens.Count > want) { UnityEngine.Object.Destroy(_ravens[_ravens.Count - 1]); _ravens.RemoveAt(_ravens.Count - 1); }
            while (_ravens.Count < want)
            {
                GameObject made = MakeRaven(_ravens.Count == 1);
                if (made == null) return;
                _ravens.Add(made);
            }
            for (int i = 0; i < _ravens.Count; i++)
            {
                Transform t = _ravens[i].transform;
                if (i < seats.Count) { t.localPosition = seats[i].Pos; t.localRotation = Quaternion.Euler(seats[i].Rot); }
                else { t.localPosition = new Vector3(0.8f, 0f, 0f); t.localRotation = Quaternion.identity; }
                PerchPose pose = _ravens[i].GetComponentInChildren<PerchPose>();
                if (pose != null) { pose.Pose = m.Pose; pose.Stand = m.Stand; pose.Sit = Workshop.EditStand ? 0f : 1f; }
            }
        }

        private static GameObject MakeRaven(bool munin)
        {
            GameObject holder = new GameObject("Raven");
            holder.transform.SetParent(_modelRoot.transform, false);
            if (Perches.Sitter(holder.transform, munin, Shoulder.Scale, _layer, null, null) != null) return holder;
            UnityEngine.Object.Destroy(holder);
            return null;
        }

        internal static void RavenAgain() { foreach (GameObject g in _ravens) if (g != null) UnityEngine.Object.Destroy(g); _ravens.Clear(); _gearKey = null; }

        // a thing for a raven: one raven sitting (the top size: as the thing is made for), placed so the slot's bone is
        // at point 0; the model's parts (drawn by the view, to be picked and moved) are on it there. On the legs the
        // right one wears a mirrored copy, made again when the model changes.
        internal static bool GearMunin;
        private static string _gearKey;
        private static float _gearAt;
        private static void GearRaven(WsModel m)
        {
            int want = ShowRaven ? 1 : 0;
            if (_ravens.Count > want || (_ravens.Count == 1 && _ravens[0] != null && _ravens[0].name != (GearMunin ? "RavenM" : "RavenH"))) RavenAgain();
            if (want == 0) return;
            if (_ravens.Count == 0)
            {
                GameObject made = MakeRaven(GearMunin);
                if (made == null) return;
                made.name = GearMunin ? "RavenM" : "RavenH";
                _ravens.Add(made);
            }
            Transform holder = _ravens[0].transform;
            holder.localRotation = Quaternion.identity;
            string slot = RavenGear.SlotOf(m);
            Transform bone = RavenGear.Bone(_ravens[0], RavenGear.BoneName(slot, false));
            if (bone != null) holder.localPosition -= _modelRoot.transform.InverseTransformPoint(bone.position);
            if (Time.unscaledTime - _gearAt > 0.3f)
            {
                _gearAt = Time.unscaledTime;
                string key = slot + "|" + WsFiles.ToText(m);
                if (key != _gearKey)
                {
                    _gearKey = key;
                    GameObject bird = holder.childCount > 0 ? holder.GetChild(0).gameObject : null;
                    if (slot == RavenGear.Legs) RavenGear.Attach(bird, m, GearMunin, _layer, false);
                    else RavenGear.Detach(bird);
                }
            }
        }

        // the raven's bounds (a thing for a raven is framed with its wearer)
        internal static Bounds? RavenBounds()
        {
            Bounds? all = null;
            foreach (GameObject g in _ravens)
                if (g != null)
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
                    {
                        if (r is ParticleSystemRenderer) continue;
                        if (all == null) all = r.bounds; else { Bounds b = all.Value; b.Encapsulate(r.bounds); all = b; }
                    }
            return all;
        }
    }

    // ---------------------------------------------------------------- the window

    // The «Майстерня» tab of the test window (DevPanel; console: raven workshop). Enabled only while its tab is shown;
    // the model being made stays in memory between visits (named ones are saved on leaving).
    internal class Workshop : MonoBehaviour
    {
        private GUIStyle _on, _small, _rowL, _head;
        private Texture2D _rowSel;
        private bool _favLoaded;

        private WsModel _m = new WsModel();
        private int _sel = -1;
        private bool _dirty;
        private readonly List<string> _undo = new List<string>();
        private string _note = "";

        // the catalog
        private string _search = "", _searchWas = null;
        private int _cat, _mod = -1, _catWas = -2, _modWas = -2;
        private string _set = "", _setWas = null; // the asset set shown ("" = all)
        private Dictionary<string, int> _used = new Dictionary<string, int>();
        private readonly List<WsCatalog.Entry> _shown = new List<WsCatalog.Entry>();
        private Vector2 _catScroll, _partScroll, _fileScroll, _editScroll;
        private bool _favOnly, _favWas, _pickMat;
        private static Color? _clipTint;
        private float _softAt;
        private readonly HashSet<string> _fav = new HashSet<string>();

        // the steps
        private static readonly float[] MoveSteps = { 0.01f, 0.05f, 0.1f, 0.25f, 0.5f };
        private static readonly float[] TurnSteps = { 1f, 5f, 15f, 45f, 90f };
        private static readonly float[] SizeSteps = { 0.01f, 0.05f, 0.1f, 0.25f };
        private int _moveStep = 1, _turnStep = 2, _sizeStep = 1;

        // the picture
        private bool _dragged;
        private Vector2 _downAt;

        // the copy in the world
        private GameObject _worldRoot;
        private WsView _world;

        private List<string> _files = new List<string>();
        private string _deleteAsk = "";
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();

        // its tab is shown: the catalog, the picture, the list of models
        internal string Opened()
        {
            if (!_favLoaded) { _favLoaded = true; LoadFav(); }
            enabled = true;
            WsCatalog.Build();
            if (!WsStage.Open()) return _note = "Майстерня: " + WsStage.Problem;
            _files = WsFiles.List();
            _catWas = -2;
            if (_note.Length == 0) _note = "Моделі: " + WsFiles.Dir;
            return _note;
        }

        // the tab left (the copy in the world stays) or the window closed (it goes too); nothing named is lost
        internal void Closed(bool all)
        {
            if (_dirty && _m.Parts.Count > 0 && !string.IsNullOrEmpty(_m.Name)) { WsFiles.Save(_m); _dirty = false; }
            enabled = false;
            WsStage.Close();
            if (all) WorldOff();
        }

        private void LateUpdate()
        {
            if (WsStage.Ready) WsStage.Render(_m, _sel);
            if (_world != null) _world.Sync(_m);
        }

        private void OnDestroy() { WorldOff(); WsStage.Close(); }

        // ---------------------------------------------------------------- changes

        private void Change() { _undo.Add(WsFiles.ToText(_m)); if (_undo.Count > 200) _undo.RemoveAt(0); _dirty = true; }

        private void Undo()
        {
            if (_undo.Count == 0) { _note = "Нема що повертати."; return; }
            string text = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            string file = _m.File;
            _m = WsFiles.FromText(text);
            _m.File = file;
            if (_sel >= _m.Parts.Count) _sel = _m.Parts.Count - 1;
            _dirty = true;
            _note = "Повернуто на крок назад.";
        }

        private WsPart Sel { get { return _sel >= 0 && _sel < _m.Parts.Count ? _m.Parts[_sel] : null; } }

        private void Add(string src)
        {
            Change();
            WsPart p = new WsPart();
            p.Src = src;
            WsPart s = Sel;
            if (s != null) p.Pos = s.Pos + new Vector3(0f, 0f, 0f);
            _m.Parts.Add(p);
            _sel = _m.Parts.Count - 1;
            if (_m.Parts.Count == 1) StartCoroutine(FrameSoon());
            if (!WsFiles.Special(src) && _m.Use != RavenGear.Use) StartCoroutine(FloorSoon(p));
            _note = "Додано: " + WsCatalog.Label(src);
        }

        // a new piece that sank under the floor (most pieces of the game have their middle as the centre) stands on it
        private IEnumerator FloorSoon(WsPart p)
        {
            yield return null; // the stage takes the new part in LateUpdate: its look is there a frame later
            yield return null;
            int i = _m.Parts.IndexOf(p);
            Bounds? b = i >= 0 && WsStage.View != null ? WsStage.View.BoundsOf(i) : null;
            if (b == null) yield break;
            float bottom = b.Value.min.y - 9300f;
            if (bottom < -0.001f) p.Pos.y -= bottom;
        }

        private IEnumerator FrameSoon() { yield return null; yield return null; WsStage.Frame(); }

        private void NewModel()
        {
            if (_dirty && _m.Parts.Count > 0 && !string.IsNullOrEmpty(_m.Name)) WsFiles.Save(_m);
            _m = new WsModel(); _sel = -1; _undo.Clear(); _dirty = false; _fields.Clear();
            WsStage.Frame();
            _note = "Нова модель. Дай їй назву й опис — так я знатиму, що це.";
        }

        private void Open(string file)
        {
            if (_dirty && _m.Parts.Count > 0 && !string.IsNullOrEmpty(_m.Name)) WsFiles.Save(_m);
            WsModel m = WsFiles.Load(file);
            if (m == null) { _note = "Не вдалося прочитати файл."; return; }
            _m = m; _sel = m.Parts.Count > 0 ? 0 : -1; _undo.Clear(); _dirty = false; _fields.Clear();
            StartCoroutine(FrameSoon());
            int missing = 0;
            foreach (WsPart p in m.Parts) if (!WsFiles.Special(p.Src) && !WsAssets.Is(p.Src) && WsCatalog.Find(p.Src) == null) missing++;
            _note = "Відкрито: " + m.Name + (missing > 0 ? " — " + missing + " деталей нема в цій грі (мод не стоїть?)" : "");
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_m.Name.Trim())) { _note = "Спершу дай моделі назву."; return; }
            _m.Name = _m.Name.Trim();
            if (WsFiles.Save(_m)) { _dirty = false; _files = WsFiles.List(); _note = "Збережено: " + Path.GetFileName(_m.File); }
            else _note = "Не вдалося зберегти (див. лог).";
        }

        // ---------------------------------------------------------------- the world copy

        private void WorldHere()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (_worldRoot == null)
            {
                _worldRoot = new GameObject("RavenWatch_WorkshopWorld");
                _world = new WsView(_worldRoot.transform, 0, false);
            }
            Vector3 fwd = p.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 at = p.transform.position + fwd * 2.5f;
            RaycastHit hit;
            int mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small");
            if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out hit, 20f, mask)) at = hit.point;
            _worldRoot.transform.position = at;
            _worldRoot.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            _note = "Копію поставлено перед тобою (бачиш лише ти, F8 — піти подивитися).";
        }

        private void WorldOff()
        {
            if (_world != null) _world.Clear();
            _world = null;
            if (_worldRoot != null) Destroy(_worldRoot);
            _worldRoot = null;
        }

        // ---------------------------------------------------------------- favourites

        private string FavFile { get { return Path.Combine(WsFiles.Dir, "_улюблені.txt"); } }

        private void LoadFav()
        {
            try { if (File.Exists(FavFile)) foreach (string l in File.ReadAllLines(FavFile)) if (l.Trim().Length > 0) _fav.Add(l.Trim()); }
            catch { }
        }

        private void SaveFav()
        {
            try { Directory.CreateDirectory(WsFiles.Dir); File.WriteAllLines(FavFile, new List<string>(_fav).ToArray()); }
            catch { }
        }

        // ---------------------------------------------------------------- drawing

        private void Styles()
        {
            if (_on != null && _rowSel != null) return;
            _rowSel = Plugin.Solid(new Color(0.45f, 0.35f, 0.12f, 0.9f));
            _on = new GUIStyle(GUI.skin.button); _on.fontStyle = FontStyle.Bold; _on.normal.textColor = new Color(1f, 0.83f, 0.3f);
            _small = new GUIStyle(GUI.skin.label); _small.fontSize = 11; _small.wordWrap = true; _small.richText = true;
            _rowL = new GUIStyle(GUI.skin.button); _rowL.alignment = TextAnchor.MiddleLeft; _rowL.richText = true; _rowL.fontSize = 12; _rowL.wordWrap = false; _rowL.clipping = TextClipping.Clip;
            _head = new GUIStyle(GUI.skin.label); _head.fontStyle = FontStyle.Bold; _head.normal.textColor = new Color(0.95f, 0.82f, 0.55f);
        }

        // the tab's body: h = the height it has inside the test window
        internal void Body(float h)
        {
            Styles();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Майстерня воронів</b>" + (_dirty ? " *" : ""), _head, GUILayout.Width(200f));
            GUILayout.Label("<color=#C8C8C8>" + _note + "</color>", _small, GUILayout.Height(28f));
            GUILayout.EndHorizontal();

            h -= 34f;
            _pic = Mathf.Clamp(h - 380f, 220f, 512f); // the picture gives way so the model box below has room
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(330f), GUILayout.Height(h)); Catalog(h); GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(520f), GUILayout.Height(h)); Picture();
            // under the picture: scrolled when it does not fit (a thing for a raven has more rows), the saved list always reachable
            _midScroll = GUILayout.BeginScrollView(_midScroll, GUILayout.Height(Mathf.Max(120f, h - _pic - 92f))); ModelBox(); GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Height(h)); Parts(); EditBox(); GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // ---- left: the game's pieces

        private void Catalog(float h)
        {
            bool assets = _cat == WsCatalog.CatAsset;
            GUILayout.Label(assets ? "Ассети (тест, з теки)" : _cat == WsCatalog.CatFx ? "Ефекти з гри (без самої деталі)" : "Деталі з гри", _head);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Пошук", GUILayout.Width(48f));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("×", GUILayout.Width(24f))) _search = "";
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < WsCatalog.Cats.Length; i++) if (GUILayout.Button(WsCatalog.Cats[i], i == _cat ? _on : GUI.skin.button)) _cat = i;
            GUILayout.EndHorizontal();
            if (assets) AssetBar();
            GUILayout.BeginHorizontal();
            GUILayout.Label("<color=#9A9A9A>" + (assets ? "Не в моді: лише з цієї теки" : "Лише деталі самої гри") + "</color>", _small);
            if (GUILayout.Button(_favOnly ? "★ улюблені" : "☆ усі", _favOnly ? _on : GUI.skin.button, GUILayout.Width(100f))) _favOnly = !_favOnly;
            GUILayout.EndHorizontal();
            if (_pickMat)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("<color=#FFD24A>Клацни деталь, чий матеріал узяти</color>", _small);
                if (GUILayout.Button("Скасувати", GUILayout.Width(90f))) _pickMat = false;
                GUILayout.EndHorizontal();
            }
            if (!WsCatalog.Ready) { GUILayout.Label("Список ще не зібрано (світ вантажиться?)."); if (GUILayout.Button("Зібрати")) WsCatalog.Build(); return; }

            if (_search != _searchWas || _cat != _catWas || _mod != _modWas || _favOnly != _favWas || _set != _setWas) Filter();
            GUILayout.Label("<color=#9A9A9A>" + _shown.Count + " з " + WsCatalog.All.Count + ". Клік — додати; ↔ — замінити вибрану деталь; ☆ — в улюблені.</color>", _small);

            const float row = 34f;
            _catScroll = GUILayout.BeginScrollView(_catScroll, GUILayout.Height(Mathf.Max(200f, h - 150f)));
            Rect all = GUILayoutUtility.GetRect(300f, row * _shown.Count);
            int first = Mathf.Max(0, Mathf.FloorToInt(_catScroll.y / row) - 1);
            int last = Mathf.Min(_shown.Count - 1, first + Mathf.CeilToInt(h / row) + 2);
            for (int i = first; i <= last; i++)
            {
                WsCatalog.Entry e = _shown[i];
                Rect r = new Rect(all.x, all.y + i * row, all.width, row - 2f);
                Rect icon = new Rect(r.x + 2f, r.y + 1f, row - 4f, row - 4f);
                Rect main = new Rect(icon.xMax + 4f, r.y, r.width - icon.width - 70f, r.height);
                Rect swap = new Rect(main.xMax + 2f, r.y, 30f, r.height);
                Rect star = new Rect(swap.xMax + 2f, r.y, 28f, r.height);
                DrawIcon(icon, e.Icon);
                string title;
                if (e.Cat == WsCatalog.CatAsset)
                {
                    int n; _used.TryGetValue(e.Name, out n);
                    title = e.Label + (n > 0 ? "  <color=#7CDB7C>✓ у моделях: " + n + "</color>" : "") + "\n<size=10><color=#9A9A9A>набір " + e.Mod + "</color></size>";
                }
                else title = (e.Label.Length > 0 ? e.Label : e.Name) + "\n<size=10><color=#9A9A9A>" + e.Name + (e.Mod != WsCatalog.Game ? " · " + e.Mod : "") + "</color></size>";
                if (GUI.Button(main, title, _rowL))
                {
                    if (_pickMat)
                    {
                        WsPart s = Sel;
                        if (s != null && s.Src != WsFiles.Seat) { Change(); s.Mat = e.Name; _note = "Матеріал: як у " + WsCatalog.Label(e.Name); }
                        _pickMat = false;
                    }
                    else Add(e.Name);
                }
                if (GUI.Button(swap, "↔"))
                {
                    WsPart s = Sel;
                    if (s == null || s.Src == WsFiles.Seat) _note = "Спершу вибери деталь, яку замінити.";
                    else { Change(); s.Src = e.Name; _note = "Замінено на " + WsCatalog.Label(e.Name); }
                }
                bool fav = _fav.Contains(e.Name);
                if (GUI.Button(star, fav ? "★" : "☆")) { if (fav) _fav.Remove(e.Name); else _fav.Add(e.Name); SaveFav(); if (_favOnly) _catWas = -2; }
            }
            GUILayout.EndScrollView();
        }

        private void Filter()
        {
            _searchWas = _search; _catWas = _cat; _modWas = _mod; _favWas = _favOnly; _setWas = _set;
            if (_cat == WsCatalog.CatAsset) _used = WsAssets.Used();
            _shown.Clear();
            string[] words = (_search ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string mod = _mod >= 0 && _mod < WsCatalog.Mods.Count ? WsCatalog.Mods[_mod] : null;
            foreach (WsCatalog.Entry e in WsCatalog.All)
            {
                if (_cat == WsCatalog.CatAll ? (e.Cat == WsCatalog.CatAsset || e.Cat == WsCatalog.CatFx) : e.Cat != _cat) continue; // «Усе» = the game's own pieces; assets and effects only in their tabs
                if (_cat == WsCatalog.CatAsset && _set.Length > 0 && e.Mod != _set) continue;
                if (mod != null && e.Mod != mod) continue;
                if (_favOnly && !_fav.Contains(e.Name)) continue;
                bool ok = true;
                foreach (string w in words) if (e.Search.IndexOf(w, StringComparison.Ordinal) < 0) { ok = false; break; }
                if (ok) _shown.Add(e);
            }
            _catScroll = Vector2.zero;
        }

        // the asset sets as buttons, the folder read again (files added or taken away), the folder opened
        private void AssetBar()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Усі набори", _set.Length == 0 ? _on : GUI.skin.button)) _set = "";
            foreach (string set in WsAssets.Sets) if (GUILayout.Button(set, _set == set ? _on : GUI.skin.button)) _set = set;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Перечитати теку"))
            {
                WsCatalog.Assets();
                if (_set.Length > 0 && !WsAssets.Sets.Contains(_set)) _set = "";
                _catWas = -2;
                _note = "Ассетів: " + WsCatalog.All.FindAll(x => x.Cat == WsCatalog.CatAsset).Count + " у " + WsAssets.Sets.Count + " наборах.";
            }
            if (GUILayout.Button("Відкрити теку"))
            {
                try { Directory.CreateDirectory(WsAssets.Dir); Application.OpenURL("file:///" + WsAssets.Dir.Replace('\\', '/')); }
                catch (Exception ex) { _note = "Не вдалося: " + ex.Message; }
            }
            GUILayout.EndHorizontal();
            if (WsAssets.NoTexture.Count > 0)
                GUILayout.Label("<color=#FF9A6A>Нема текстур: " + string.Join(", ", WsAssets.NoTexture.ToArray()) + " — поклади їх у теку набору.</color>", _small);
        }

        private static void DrawIcon(Rect r, Sprite s)
        {
            if (s == null || s.texture == null) { GUI.Box(r, ""); return; }
            Rect tr = s.textureRect;
            Texture t = s.texture;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height));
        }

        // ---- middle: the turning picture

        private float _pic = 512f;
        private Vector2 _midScroll;

        private void Picture()
        {
            Rect pic = GUILayoutUtility.GetRect(_pic, _pic, GUILayout.Width(_pic), GUILayout.Height(_pic));
            if (WsStage.Texture != null) GUI.DrawTexture(pic, WsStage.Texture, ScaleMode.ScaleToFit, false);
            else GUI.Label(pic, WsStage.Problem);
            if (_m.Parts.Count == 0) GUI.Label(new Rect(pic.x + 12f, pic.y + 10f, pic.width - 24f, 60f), "Порожньо. Додай деталь зі списку ліворуч, а місце ворона — кнопкою праворуч.", _small);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && pic.Contains(e.mousePosition)) { _dragged = false; _downAt = e.mousePosition; }
            if (e.type == EventType.MouseDrag && pic.Contains(e.mousePosition))
            {
                if ((e.mousePosition - _downAt).sqrMagnitude > 16f) _dragged = true;
                if (e.button == 1 || e.shift)
                {
                    // right button or shift: slide the view
                    Quaternion look = Quaternion.Euler(WsStage.Pitch, WsStage.Yaw, 0f);
                    float k = WsStage.Radius / WsStage.Zoom * 0.004f;
                    WsStage.Center += look * new Vector3(-e.delta.x * k, e.delta.y * k, 0f);
                }
                else { WsStage.Yaw += e.delta.x * 0.6f; WsStage.Pitch = Mathf.Clamp(WsStage.Pitch + e.delta.y * 0.4f, -85f, 85f); }
                e.Use();
            }
            if (e.type == EventType.MouseUp && pic.Contains(e.mousePosition) && !_dragged && e.button == 0 && WsStage.View != null)
            {
                Vector2 v = new Vector2((e.mousePosition.x - pic.x) / pic.width, 1f - (e.mousePosition.y - pic.y) / pic.height);
                int hit = WsStage.View.Pick(WsStage.RayAt(v));
                if (hit >= 0) { _sel = hit; _note = "Вибрано: " + WsCatalog.Label(_m.Parts[hit].Src); }
                e.Use();
            }
            if (e.type == EventType.ScrollWheel && pic.Contains(e.mousePosition)) { WsStage.Zoom = Mathf.Clamp(WsStage.Zoom - e.delta.y * 0.05f, 0.2f, 8f); e.Use(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Спереду")) { WsStage.Yaw = 180f; WsStage.Pitch = 5f; }
            if (GUILayout.Button("Збоку")) { WsStage.Yaw = 270f; WsStage.Pitch = 5f; }
            if (GUILayout.Button("Ззаду")) { WsStage.Yaw = 0f; WsStage.Pitch = 5f; }
            if (GUILayout.Button("Згори")) { WsStage.Pitch = 85f; }
            if (GUILayout.Button("Усю модель")) WsStage.Frame();
            if (GUILayout.Button(WsStage.Spin ? "Обертати: так" : "Обертати: ні", WsStage.Spin ? _on : GUI.skin.button)) WsStage.Spin = !WsStage.Spin;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(WsStage.ShowRaven ? "Ворон: є" : "Ворон: нема", WsStage.ShowRaven ? _on : GUI.skin.button)) WsStage.ShowRaven = !WsStage.ShowRaven;
            if (GUILayout.Button(WsStage.ShowGrid ? "Сітка: є" : "Сітка: нема", WsStage.ShowGrid ? _on : GUI.skin.button)) WsStage.ShowGrid = !WsStage.ShowGrid;
            if (GUILayout.Button(_world != null ? "У світі: оновити місце" : "Показати у світі")) WorldHere();
            if (_world != null && GUILayout.Button("Прибрати зі світу")) WorldOff();
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Тягни — крутити, правою/Shift — зсунути, коліщатко — ближче. Клік по деталі — вибрати. Сітка: клітинка 25 см; синя стрілка на підлозі — перед моделі.</color>", _small);
        }

        // ---- middle, under the picture: the model itself

        private void ModelBox()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Назва", GUILayout.Width(60f));
            GUI.SetNextControlName("ws_name");
            string n = GUILayout.TextField(_m.Name ?? "");
            if (n != _m.Name) { _m.Name = n; _dirty = true; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Що це", GUILayout.Width(60f));
            GUI.SetNextControlName("ws_note");
            string d = GUILayout.TextField(_m.Note ?? "");
            if (d != _m.Note) { _m.Note = d; _dirty = true; }
            GUILayout.EndHorizontal();
            PerchBox();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Нова")) NewModel();
            if (GUILayout.Button(_dirty ? "Зберегти *" : "Зберегти", _dirty ? _on : GUI.skin.button)) Save();
            if (GUILayout.Button("Копія"))
            {
                if (string.IsNullOrEmpty((_m.Name ?? "").Trim())) _note = "Копію роблять зі збереженої моделі: спершу дай назву й збережи.";
                else { _m.Name = _m.Name.Trim() + " (копія)"; _m.File = ""; _dirty = true; _note = "Копія: зміни назву і збережи."; }
            }
            if (GUILayout.Button("Назад (крок)")) Undo();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool ready = !string.IsNullOrEmpty(_m.Ready);
            if (GUILayout.Button(ready ? "✓ Готово для мода (" + _m.Ready + ")" : "☐ Готово для мода", ready ? _on : GUI.skin.button))
            {
                if (string.IsNullOrEmpty((_m.Name ?? "").Trim()) || _m.Parts.Count == 0) _note = "Спершу дай моделі назву й деталі.";
                else
                {
                    Change();
                    _m.Ready = ready ? "" : DevKit.Author + " " + DateTime.Now.ToString("yyyy-MM-dd");
                    Save(); // marked: at once in the file
                    _note = ready ? "Знято позначку «Готово»." : "Позначено «Готово для мода». Коли все готове — «Запакувати готове».";
                }
            }
            int readyCount = 0;
            foreach (string f in _files) if (WsFiles.ReadyOf(f)) readyCount++;
            GUI.enabled = readyCount > 0;
            if (GUILayout.Button("Запакувати готове (" + readyCount + ")", GUILayout.Width(190f))) PackReady();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Збережені моделі", _head);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < FileTabs.Length; i++) if (GUILayout.Button(FileTabs[i] + " " + CountIn(i), _fileTab == i ? _on : GUI.skin.button)) { _fileTab = i; _fileScroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            _fileScroll = GUILayout.BeginScrollView(_fileScroll, GUILayout.Height(170f));
            int shownFiles = 0;
            foreach (string f in _files)
            {
                if (!InTab(_fileTab, WsFiles.UseOf(f))) continue;
                shownFiles++;
                string name = Path.GetFileNameWithoutExtension(f);
                bool cur = !string.IsNullOrEmpty(_m.File) && string.Equals(Path.GetFullPath(_m.File), Path.GetFullPath(f), StringComparison.OrdinalIgnoreCase);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((WsFiles.ReadyOf(f) ? "<color=#8FD18F>✓</color> " : "") + name, cur ? _on : _rowL)) Open(f);
                if (_deleteAsk == f)
                {
                    if (GUILayout.Button("Так, видалити", GUILayout.Width(110f))) { try { File.Delete(f); } catch { } if (cur) { _m.File = ""; _dirty = true; } _files = WsFiles.List(); _deleteAsk = ""; _note = "Видалено: " + name; }
                    if (GUILayout.Button("Ні", GUILayout.Width(36f))) _deleteAsk = "";
                }
                else if (GUILayout.Button("×", GUILayout.Width(30f))) _deleteAsk = f;
                GUILayout.EndHorizontal();
            }
            if (_files.Count == 0) GUILayout.Label("<color=#9A9A9A>Ще нічого не збережено.</color>", _small);
            else if (shownFiles == 0) GUILayout.Label("<color=#9A9A9A>Тут поки нічого.</color>", _small);
            GUILayout.EndScrollView();
        }

        // every model marked ready, with the book's fits, into one archive on the desktop (to send to the mod's author)
        private void PackReady()
        {
            if (_dirty && _m.Parts.Count > 0 && !string.IsNullOrEmpty(_m.Name)) Save();
            try
            {
                WsCatalog.Build(); // (to tell which parts are the game's own)
                int n; List<string> warn;
                string path = DevKit.Pack(out n, out warn);
                if (path == null) { _note = "Нічого не позначено «Готово для мода»."; return; }
                _note = "Запаковано " + n + ": " + Path.GetFileName(path) + " (на робочому столі) — надішли його." + (warn.Count > 0 ? " <color=#FF9A6A>Увага: " + warn.Count + " деталей не з гри — див. «список.txt» в архіві.</color>" : "");
                WsFiles.Log("packed " + n + " to " + path + (warn.Count > 0 ? "; warnings: " + string.Join("; ", warn.ToArray()) : ""));
                try { Application.OpenURL("file:///" + Path.GetDirectoryName(path).Replace('\\', '/')); } catch { }
            }
            catch (Exception e) { _note = "Не вдалося запакувати: " + e.Message; WsFiles.Log("pack: " + e); }
        }

        // the saved list's tabs: all; things for a raven; what is built (perches, raven tables); items; the rest
        private static readonly string[] FileTabs = { "Усі", "Для ворона", "Будується", "Предмети", "Інше" };
        private int _fileTab;
        private static bool InTab(int tab, string use)
        {
            switch (tab)
            {
                case 1: return use == RavenGear.Use;
                case 2: return use == Perches.Use || use == Perches.TableUse;
                case 3: return use == WsItems.Use;
                case 4: return use != RavenGear.Use && use != Perches.Use && use != Perches.TableUse && use != WsItems.Use;
                default: return true;
            }
        }
        private int CountIn(int tab) { int n = 0; foreach (string f in _files) if (InTab(tab, WsFiles.UseOf(f))) n++; return n; }

        // ---- right: the parts

        private void Parts()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Деталі моделі (" + _m.Parts.Count + ")", _head);
            GUILayout.FlexibleSpace();
            int seats = _m.Seats; // a perch holds one raven or both
            if (seats < 2 && (_m.Use ?? "") != RavenGear.Use && (_m.Use ?? "") != Perches.TableUse && GUILayout.Button(seats == 0 ? "+ Місце ворона" : "+ Друге місце"))
            {
                Add(WsFiles.Seat);
                _m.Parts[_sel].Pos = new Vector3(seats == 0 ? 0f : 0.3f, 1f, 0f);
            }
            // an unseen box to bump into (a tree's trunk, not its whole crown); with any, the parts get no boxes of their own
            if ((_m.Use ?? "") != RavenGear.Use && GUILayout.Button(new GUIContent("+ Колайдер", "Невидима коробка, об яку спотикаються й на яку наводять. Розмір — це її масштаб. Коли в моделі є хоч один колайдер, самі деталі коробок не мають.")))
            {
                Add(WsFiles.Box);
                _m.Parts[_sel].Pos = new Vector3(0f, 0.5f, 0f);
            }
            GUILayout.EndHorizontal();
            _partScroll = GUILayout.BeginScrollView(_partScroll, GUILayout.Height(150f));
            for (int i = 0; i < _m.Parts.Count; i++)
            {
                WsPart p = _m.Parts[i];
                bool missing = p.Src != WsFiles.Seat && WsCatalog.Find(p.Src) == null;
                string label = (i + 1) + ". " + (p.Src == WsFiles.Seat ? "<color=#FFD24A>◆ місце ворона</color>" : WsCatalog.Label(p.Src)) + (missing ? " <color=#FF7A7A>(нема в грі: " + p.Src + ")</color>" : "");
                if (GUILayout.Button(label, i == _sel ? _on : _rowL)) _sel = i;
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Уся модель:", GUILayout.Width(80f));
            GUI.enabled = _m.Parts.Count > 0;
            if (GUILayout.Button("×0.5")) Grow(0.5f);
            if (GUILayout.Button("−10%")) Grow(0.9f);
            if (GUILayout.Button("−1%")) Grow(0.99f);
            if (GUILayout.Button("+1%")) Grow(1.01f);
            if (GUILayout.Button("+10%")) Grow(1.1f);
            if (GUILayout.Button("×2")) Grow(2f);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        // the whole model bigger or smaller, as one: every part's place and size from the model's point 0 (the floor's
        // middle, or the point on the wall, so it still stands or hangs there); a raven's seat moves with it, the raven
        // itself keeps its size
        private void Grow(float k)
        {
            Change();
            foreach (WsPart p in _m.Parts) { p.Pos *= k; if (p.Src != WsFiles.Seat) p.Scale *= k; } // (a box grows with the model)
            _fields.Clear();
            _note = "Уся модель: ×" + k.ToString("0.##", CultureInfo.InvariantCulture) + ". «Назад (крок)» — скасувати.";
        }

        private void EditBox()
        {
            WsPart p = Sel;
            if (p == null) { GUILayout.Label("<color=#9A9A9A>Вибери деталь у списку чи клацни по ній на картинці.</color>", _small); return; }
            _editScroll = GUILayout.BeginScrollView(_editScroll);
            GUILayout.Label("Вибрана: " + (p.Src == WsFiles.Seat ? "місце ворона (куди він сяде; жовта риска — куди дивиться дзьоб)" : WsCatalog.Label(p.Src)), _small);

            Steps("Крок руху", MoveSteps, ref _moveStep, v => Mathf.RoundToInt(v * 100f) + " см");
            Vec("Місце", ref p.Pos, MoveSteps[_moveStep], "pos", "м", new[] { "X ←→", "Y ↕", "Z ↗" });
            Steps("Крок повороту", TurnSteps, ref _turnStep, v => v.ToString("0") + "°");
            Vec("Поворот", ref p.Rot, TurnSteps[_turnStep], "rot", "°", new[] { "X", "Y", "Z" });
            Steps("Крок розміру", SizeSteps, ref _sizeStep, v => Mathf.RoundToInt(v * 100f) + "%");
            Scale(p);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Дублювати")) { Change(); WsPart c = p.Clone(); _m.Parts.Insert(_sel + 1, c); _sel++; }
            bool drop = GUILayout.Button("Видалити");
            if (GUILayout.Button("Вище в списку") && _sel > 0) { Change(); _m.Parts.RemoveAt(_sel); _sel--; _m.Parts.Insert(_sel, p); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_m.Use == RavenGear.Use) { if (GUILayout.Button("До кістки (у точку 0)")) { Change(); p.Pos = Vector3.zero; } }
            else if (_m.Wall ? GUILayout.Button("До стіни") : GUILayout.Button("На підлогу")) { if (_m.Wall) ToWall(p); else Floor(p); }
            if (GUILayout.Button("По центру")) { Change(); p.Pos.x = 0f; p.Pos.z = 0f; }
            if (GUILayout.Button("Поворот 0")) { Change(); p.Rot = Vector3.zero; }
            if (GUILayout.Button("Розмір 1")) { Change(); p.Scale = Vector3.one; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Дзеркало X")) { Change(); p.Scale.x = -p.Scale.x; }
            if (GUILayout.Button("Дзеркало Z")) { Change(); p.Scale.z = -p.Scale.z; }
            if (GUILayout.Button("Копія дзеркалом X")) { Change(); WsPart c = p.Clone(); c.Pos.x = -c.Pos.x; c.Rot.y = -c.Rot.y; c.Rot.z = -c.Rot.z; _m.Parts.Insert(_sel + 1, c); _sel++; }
            GUILayout.EndHorizontal();
            if (p.Src == WsFiles.Seat) PoseBox();
            else if (!WsFiles.IsBox(p.Src)) TintBox(p);
            GUILayout.EndScrollView();
            if (drop) { Change(); _m.Parts.RemoveAt(_sel); if (_sel >= _m.Parts.Count) _sel = _m.Parts.Count - 1; }
        }

        // ---- marked for the mod: a raven perch for the hammer

        private static readonly string[] StationIds = { "piece_workbench", "forge", "piece_stonecutter", "piece_magetable", "blackforge" };
        private static readonly string[] StationNames = { "Верстак", "Кузня", "Каменетес", "Стіл ґалдра", "Чорна кузня" };
        // what the model is for in the mod
        private static readonly string[] UseIds = { "", Perches.Use, Perches.TableUse, RavenGear.Use, WsItems.Use };
        private static readonly string[] UseNames = { "Ні", "Жердина", "Стіл ворона", "Річ для ворона", "Предмет" };

        private void SetUse(string use)
        {
            Change();
            _m.Use = use;
            if (use.Length > 0 && string.IsNullOrEmpty(_m.Id)) _m.Id = Perches.IdFor(_m.Name);
            if (use.Length > 0 && string.IsNullOrEmpty(_m.Cost)) _m.Cost = Perches.DefaultCost;
            if (use == RavenGear.Use) { _m.Mount = ""; if (string.IsNullOrEmpty(_m.Slot)) _m.Slot = RavenGear.Back; }
            WsStage.RavenAgain();
            _note = use == Perches.Use ? (_m.Seats == 0 ? "Жердині потрібне місце ворона: «+ Місце ворона» праворуч." : "Жердина.")
                : use == Perches.TableUse ? "Стіл ворона: у грі E відкриває меню, де майструють речі для воронів."
                : use == RavenGear.Use ? "Річ для ворона: ворон на картинці, точка 0 — його кістка (спина, шия, ліва лапа чи голова для шапки). Зменш модель («Уся модель»), щоб пасувала."
                : use == WsItems.Use ? "Предмет: новий (з рецептом) або новий вигляд наявного (як-от Камінь долі). Модель лягає так, як на картинці; її середина — там, де предмет лежить."
                : "Просто модель.";
        }

        private void PerchBox()
        {
            bool perch = _m.Use == Perches.Use, table = _m.Use == Perches.TableUse, gear = _m.Use == RavenGear.Use, item = _m.Use == WsItems.Use;
            bool newItem = item && string.IsNullOrEmpty(_m.Replaces);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Для мода", GUILayout.Width(70f));
            for (int i = 0; i < UseIds.Length; i++)
            {
                bool on = (_m.Use ?? "") == UseIds[i];
                if (GUILayout.Button(UseNames[i], on ? _on : GUI.skin.button)) { if (!on) SetUse(UseIds[i]); }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (perch || table || gear || newItem)
            {
                GUILayout.Label("Ціна", GUILayout.Width(36f));
                GUI.SetNextControlName("ws_cost");
                string c = GUILayout.TextField(_m.Cost ?? "");
                if (c != _m.Cost) { _m.Cost = c; _dirty = true; }
                if (perch)
                {
                    GUILayout.Label("Сили/день", GUILayout.Width(70f));
                    float was = _m.PerDay > 0f ? _m.PerDay : Perches.DailyCap;
                    float pd = Num("perday", was);
                    if (Mathf.Abs(pd - was) > 0.001f) { _m.PerDay = Mathf.Clamp(Mathf.Round(pd), 1f, 1000f); _dirty = true; }
                }
            }
            GUILayout.EndHorizontal();
            if (gear)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Куди", GUILayout.Width(90f));
                string slot = RavenGear.SlotOf(_m);
                for (int i = 0; i < RavenGear.SlotIds.Length; i++)
                {
                    bool on = slot == RavenGear.SlotIds[i];
                    if (GUILayout.Button(RavenGear.SlotNames[i], on ? _on : GUI.skin.button)) { if (!on) { Change(); _m.Slot = RavenGear.SlotIds[i]; WsStage.RavenAgain(); } }
                }
                if (GUILayout.Button(WsStage.GearMunin ? "На картинці: Мунін" : "На картинці: Хугін", GUILayout.Width(150f))) WsStage.GearMunin = !WsStage.GearMunin;
                GUILayout.EndHorizontal();
                if (slot == RavenGear.Neck)
                {
                    bool withNeck = _m.Follow != RavenGear.FollowBody;
                    bool want = GUILayout.Toggle(withNeck, " Рухається з шиєю (вимкни для кулона на грудях: тоді річ тримається тулуба й шиї не слухається)");
                    if (want != withNeck) { Change(); _m.Follow = want ? "" : RavenGear.FollowBody; }
                }
                GUILayout.Label("<color=#9A9A9A>" + (slot == RavenGear.Head ? "Декоративна шапка: іде в пул шапок книги (відкривається довірою чи дарунком Одіна), не майструється. Точка 0 — голова ворона (череп із дзьобом), шапка повертається з нею" : "Майструється на столі ворона. Точка 0 — " + (slot == RavenGear.Legs ? "ліва лапа (на праву — дзеркально, сама)" : slot == RavenGear.Neck ? "основа шиї ворона" : "спина ворона")) + "; ворон — найбільшого розміру (найвища довіра), менші носять пропорційно менше.</color>", _small);
            }
            if (item) ItemBox(newItem);
            if (perch || table)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Де будувати", GUILayout.Width(90f));
                for (int i = 0; i < StationIds.Length; i++)
                {
                    bool on = (_m.Station ?? "") == StationIds[i] || (string.IsNullOrEmpty(_m.Station) && i == 0);
                    if (GUILayout.Button(StationNames[i], on ? _on : GUI.skin.button)) { if (!on) { Change(); _m.Station = i == 0 ? "" : StationIds[i]; } }
                }
                GUILayout.EndHorizontal();
            }
            if (!gear && !item)
            {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Стоїть на підлозі", !_m.Wall ? _on : GUI.skin.button)) { if (_m.Wall) { Change(); _m.Mount = ""; } }
            if (GUILayout.Button("Висить на стіні", _m.Wall ? _on : GUI.skin.button)) { if (!_m.Wall) { Change(); _m.Mount = "wall"; _note = "Настінна: точка 0 — місце на стіні, стіна — сітка Z = 0, модель перед нею (синя стрілка — від стіни)."; } }
            GUILayout.EndHorizontal();
            }
            if (perch && _m.Seats == 0) GUILayout.Label("<color=#FF7A7A>Нема місця ворона — у грі цієї жердини не буде. Додай «+ Місце ворона» праворуч.</color>", _small);
            if (perch || table) GUILayout.Label("<color=#9A9A9A>id " + _m.Id + " · ціна: предмет:кількість через пробіл (Wood:10 Iron:2) · у молотку з'явиться після перезаходу у світ (у конфігу «Perches from workshop files» = true)</color>", _small);
            if (gear) GUILayout.Label("<color=#9A9A9A>id " + _m.Id + " · ціна: предмет:кількість через пробіл (Feathers:5 Iron:2)</color>", _small);
        }

        // ---- an item: new (its stack, weight, where it is made) or a new look for one there is
        private void ItemBox(bool fresh)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Що це", GUILayout.Width(90f));
            if (GUILayout.Button("Новий предмет", fresh ? _on : GUI.skin.button)) { if (!fresh) { Change(); _m.Replaces = ""; } }
            for (int i = 0; i < WsItems.Replaceable.Length; i++)
            {
                bool on = _m.Replaces == WsItems.Replaceable[i];
                if (GUILayout.Button("Вигляд: " + WsItems.ReplaceableNames[i], on ? _on : GUI.skin.button)) { if (!on) { Change(); _m.Replaces = WsItems.Replaceable[i]; } }
            }
            GUILayout.EndHorizontal();
            if (fresh)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Стос", GUILayout.Width(36f));
                float st = Num("stack", _m.Stack > 0 ? _m.Stack : 20);
                if (Mathf.Abs(st - (_m.Stack > 0 ? _m.Stack : 20)) > 0.001f) { _m.Stack = Mathf.Clamp(Mathf.RoundToInt(st), 1, 999); _dirty = true; }
                GUILayout.Label("Вага", GUILayout.Width(36f));
                float wt = Num("weight", _m.Weight > 0f ? _m.Weight : 0.5f);
                if (Mathf.Abs(wt - (_m.Weight > 0f ? _m.Weight : 0.5f)) > 0.0001f) { _m.Weight = Mathf.Clamp(wt, 0.01f, 100f); _dirty = true; }
                GUILayout.EndHorizontal();
                // where it is made: the game's stations and the raven tables there are
                GUILayout.BeginHorizontal();
                GUILayout.Label("Де майструвати", GUILayout.Width(100f));
                for (int i = 0; i < StationIds.Length; i++)
                {
                    bool on = (_m.Station ?? "") == StationIds[i] || (string.IsNullOrEmpty(_m.Station) && i == 0);
                    if (GUILayout.Button(StationNames[i], on ? _on : GUI.skin.button)) { if (!on) { Change(); _m.Station = i == 0 ? "" : StationIds[i]; } }
                }
                GUILayout.EndHorizontal();
                List<Perches.Def> tables = Perches.Tables;
                if (tables.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", GUILayout.Width(100f));
                    foreach (Perches.Def d in tables)
                    {
                        bool on = _m.Station == d.Prefab;
                        if (GUILayout.Button(d.Name, on ? _on : GUI.skin.button)) { if (!on) { Change(); _m.Station = d.Prefab; } }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("<color=#9A9A9A>id " + _m.Id + " · новий предмет поки лише матеріал (без дії) · ціна: предмет:кількість через пробіл · з'явиться після перезаходу у світ (у конфігу «Perches from workshop files» = true)</color>", _small);
            }
            else GUILayout.Label("<color=#9A9A9A>Предмет робить те саме, що й раніше; міняються лише його вигляд та іконка (знімок моделі). Після перезаходу у світ (у конфігу «Perches from workshop files» = true).</color>", _small);
        }

        // ---- the raven's legs on the perch (both seats share it); the picture shows it live

        private static readonly string[] PoseNames = { "Ліва: стегно", "Ліва: коліно", "Ліва: гомілка", "Ліва: пальці", "Права: стегно", "Права: коліно", "Права: гомілка", "Права: пальці", "Нахил тіла", "Ліва: задній палець", "Права: задній палець" };

        internal static bool EditStand; // the pose being edited: standing (true) or sitting; the picture shows that one

        private void PoseBox()
        {
            GUILayout.Space(4);
            GUILayout.Label("Пози ворона на жердині (°)", _head);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Сидить (звична)", !EditStand ? _on : GUI.skin.button)) EditStand = false;
            if (GUILayout.Button("Стоїть", EditStand ? _on : GUI.skin.button)) EditStand = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("<color=#9A9A9A>Дві пози: прилетівши, ворон стає на жердину («стоїть»), потім сідає («сидить»); зрідка встає і знову сідає. Кути — над звичайною позою ворона з гри; лапи лишаються на місці ворона, тіло опускається саме. Пальці — три передні, задній палець — окремо. Спільні для обох місць.</color>", _small);
            float[] arr = EditStand ? _m.Stand : _m.Pose;
            string key = EditStand ? "stand" : "pose";
            Steps("Крок кута", TurnSteps, ref _turnStep, v => v.ToString("0") + "°");
            float step = TurnSteps[_turnStep];
            for (int k = 0; k < arr.Length; k++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(PoseNames[k], GUILayout.Width(110f));
                float x = arr[k];
                if (GUILayout.Button("−", GUILayout.Width(36f))) { Change(); x -= step; }
                x = Num(key + k, x);
                if (GUILayout.Button("+", GUILayout.Width(36f))) { Change(); x += step; }
                arr[k] = Mathf.Clamp(x, -180f, 180f);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Права = ліва")) { Change(); for (int k = 0; k < 4; k++) arr[k + 4] = arr[k]; arr[10] = arr[9]; }
            if (GUILayout.Button("Ліва = права")) { Change(); for (int k = 0; k < 4; k++) arr[k] = arr[k + 4]; arr[9] = arr[10]; }
            if (GUILayout.Button("Скинути")) { Change(); for (int k = 0; k < arr.Length; k++) arr[k] = 0f; }
            if (!EditStand && GUILayout.Button("Присісти")) { Change(); Array.Copy(PerchPose.Squat, arr, arr.Length); }
            GUILayout.EndHorizontal();
        }

        private void Steps(string title, float[] steps, ref int at, Func<float, string> text)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, GUILayout.Width(110f));
            for (int i = 0; i < steps.Length; i++) if (GUILayout.Button(text(steps[i]), i == at ? _on : GUI.skin.button)) at = i;
            GUILayout.EndHorizontal();
        }

        private void Vec(string title, ref Vector3 v, float step, string key, string unit, string[] names)
        {
            GUILayout.Label(title + " (" + unit + ")", _head);
            for (int k = 0; k < 3; k++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[k], GUILayout.Width(50f));
                float x = v[k];
                if (GUILayout.Button("−−", GUILayout.Width(36f))) { Change(); x -= step * 5f; }
                if (GUILayout.Button("−", GUILayout.Width(36f))) { Change(); x -= step; }
                x = Num(key + k, x);
                if (GUILayout.Button("+", GUILayout.Width(36f))) { Change(); x += step; }
                if (GUILayout.Button("++", GUILayout.Width(36f))) { Change(); x += step * 5f; }
                if (key == "rot") x = Mathf.Repeat(x + 180f, 360f) - 180f;
                v[k] = x;
                GUILayout.EndHorizontal();
            }
        }

        private void Scale(WsPart p)
        {
            GUILayout.Label("Розмір (1 = як у грі)", _head);
            float step = SizeSteps[_sizeStep];
            GUILayout.BeginHorizontal();
            GUILayout.Label("Увесь", GUILayout.Width(50f));
            if (GUILayout.Button("−−", GUILayout.Width(36f))) { Change(); p.Scale *= Mathf.Pow(1f - step, 5f); }
            if (GUILayout.Button("−", GUILayout.Width(36f))) { Change(); p.Scale *= 1f - step; }
            float avg = (Mathf.Abs(p.Scale.x) + Mathf.Abs(p.Scale.y) + Mathf.Abs(p.Scale.z)) / 3f;
            float typed = Num("scaleAll", avg);
            if (Mathf.Abs(typed - avg) > 0.0001f && typed > 0.001f) p.Scale *= typed / Mathf.Max(0.0001f, avg);
            if (GUILayout.Button("+", GUILayout.Width(36f))) { Change(); p.Scale *= 1f + step; }
            if (GUILayout.Button("++", GUILayout.Width(36f))) { Change(); p.Scale *= Mathf.Pow(1f + step, 5f); }
            GUILayout.EndHorizontal();
            string[] names = { "X ←→", "Y ↕", "Z ↗" };
            for (int k = 0; k < 3; k++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[k], GUILayout.Width(50f));
                float x = p.Scale[k];
                float sign = x < 0f ? -1f : 1f;
                if (GUILayout.Button("−−", GUILayout.Width(36f))) { Change(); x *= Mathf.Pow(1f - step, 5f); }
                if (GUILayout.Button("−", GUILayout.Width(36f))) { Change(); x *= 1f - step; }
                x = Num("scale" + k, x);
                if (GUILayout.Button("+", GUILayout.Width(36f))) { Change(); x *= 1f + step; }
                if (GUILayout.Button("++", GUILayout.Width(36f))) { Change(); x *= Mathf.Pow(1f + step, 5f); }
                if (Mathf.Abs(x) < 0.001f) x = 0.001f * sign;
                p.Scale[k] = x;
                GUILayout.EndHorizontal();
            }
        }

        // a number typed by hand: the text kept while it is being typed, the value taken whenever it reads
        private float Num(string key, float v)
        {
            string name = "ws_" + key + "_" + _sel;
            string text;
            bool focused = GUI.GetNameOfFocusedControl() == name;
            if (!focused || !_fields.TryGetValue(name, out text)) text = v.ToString("0.###", CultureInfo.InvariantCulture);
            GUI.SetNextControlName(name);
            string typed = GUILayout.TextField(text, GUILayout.Width(70f));
            _fields[name] = typed;
            float r;
            if (typed != text && float.TryParse(typed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out r) && !float.IsNaN(r) && !float.IsInfinity(r))
            {
                if (Mathf.Abs(r - v) > 0.00001f) { Change(); return r; }
            }
            return v;
        }

        // a wall perch: the part's back against the wall (its nearest point at Z = 0)
        private void ToWall(WsPart p)
        {
            Bounds? b = WsStage.View != null ? WsStage.View.BoundsOf(_sel) : null;
            if (b == null) return;
            Change();
            p.Pos.z -= b.Value.min.z; // the stage stands at z 0
        }

        private void Floor(WsPart p)
        {
            Bounds? b = WsStage.View != null ? WsStage.View.BoundsOf(_sel) : null;
            if (b == null) return;
            Change();
            float bottom = b.Value.min.y - 9300f; // the stage stands at 9300 m
            p.Pos.y -= bottom;
        }

        // wood, dark wood, metals, stone, paints
        private static readonly Color[] Swatches =
        {
            new Color(1f, 1f, 1f), new Color(0.85f, 0.72f, 0.55f), new Color(0.62f, 0.45f, 0.3f), new Color(0.42f, 0.29f, 0.19f),
            new Color(0.25f, 0.17f, 0.12f), new Color(0.12f, 0.1f, 0.09f), new Color(0.55f, 0.55f, 0.55f), new Color(0.78f, 0.8f, 0.84f),
            new Color(0.95f, 0.78f, 0.35f), new Color(0.72f, 0.45f, 0.25f), new Color(0.65f, 0.12f, 0.1f), new Color(0.4f, 0.08f, 0.1f),
            new Color(0.25f, 0.4f, 0.2f), new Color(0.2f, 0.32f, 0.6f), new Color(0.35f, 0.2f, 0.5f), new Color(0.3f, 0.6f, 0.65f)
        };
        private static readonly Color[] GlowSwatches =
        {
            new Color(1f, 0.55f, 0.2f), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.25f, 0.15f), new Color(0.35f, 0.7f, 1f),
            new Color(0.3f, 1f, 0.6f), new Color(0.75f, 0.4f, 1f), new Color(1f, 1f, 1f)
        };

        // a slider drag is one step for «Назад», not a hundred
        private void SoftChange() { if (Time.unscaledTime - _softAt > 0.8f) Change(); _softAt = Time.unscaledTime; _dirty = true; }

        private bool Swatch(Color c, bool picked)
        {
            Rect r = GUILayoutUtility.GetRect(24f, 20f, GUILayout.Width(24f), GUILayout.Height(20f));
            bool hit = GUI.Button(r, "");
            Color was = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), Texture2D.whiteTexture);
            GUI.color = was;
            if (picked) GUI.Label(new Rect(r.x + 7f, r.y + 1f, r.width, r.height), "<b>•</b>", _small);
            return hit;
        }

        private void TintBox(WsPart p)
        {
            GUILayout.Space(6);
            GUILayout.Label("Колір і матеріал", _head);

            // the tint
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(p.Tinted ? "Відтінок: є" : "Відтінок: нема", p.Tinted ? _on : GUI.skin.button, GUILayout.Width(120f))) { Change(); p.Tinted = !p.Tinted; }
            if (GUILayout.Button("Копіювати")) { _clipTint = p.Tinted ? (Color?)p.Tint : null; _note = p.Tinted ? "Колір скопійовано." : "Скопійовано «без відтінку»."; }
            if (GUILayout.Button("Вставити")) { Change(); p.Tinted = _clipTint != null; if (_clipTint != null) p.Tint = _clipTint.Value; }
            if (GUILayout.Button("На всі деталі")) { Change(); foreach (WsPart x in _m.Parts) if (x.Src != WsFiles.Seat) { x.Tinted = p.Tinted; x.Tint = p.Tint; } _note = "Відтінок цієї деталі — на всіх."; }
            GUILayout.EndHorizontal();
            if (p.Tinted)
            {
                for (int row = 0; row < 2; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int k = row * 8; k < row * 8 + 8; k++) if (Swatch(Swatches[k], p.Tint == Swatches[k])) { Change(); p.Tint = Swatches[k]; }
                    GUILayout.EndHorizontal();
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label("Код #", GUILayout.Width(44f));
                string hex = ColorUtility.ToHtmlStringRGB(p.Tint);
                string name = "ws_hex_" + _sel;
                string text;
                if (GUI.GetNameOfFocusedControl() != name || !_fields.TryGetValue(name, out text)) text = hex;
                GUI.SetNextControlName(name);
                string typed = GUILayout.TextField(text, GUILayout.Width(80f));
                _fields[name] = typed;
                Color parsed;
                if (typed != text && typed.Length == 6 && ColorUtility.TryParseHtmlString("#" + typed, out parsed) && ColorUtility.ToHtmlStringRGB(parsed) != hex) { Change(); p.Tint = parsed; }
                Rect show = GUILayoutUtility.GetRect(40f, 20f, GUILayout.Width(40f), GUILayout.Height(20f));
                Color was = GUI.color; GUI.color = p.Tint; GUI.DrawTexture(show, Texture2D.whiteTexture); GUI.color = was;
                GUILayout.EndHorizontal();
                p.Tint.r = Slider("R", p.Tint.r, 1.5f); p.Tint.g = Slider("G", p.Tint.g, 1.5f); p.Tint.b = Slider("B", p.Tint.b, 1.5f);
                float bright = Mathf.Max(0.001f, Mathf.Max(p.Tint.r, Mathf.Max(p.Tint.g, p.Tint.b)));
                float nb = Slider("Яскр.", bright, 1.5f);
                if (Mathf.Abs(nb - bright) > 0.0001f) { float k = Mathf.Max(0.001f, nb) / bright; p.Tint = new Color(p.Tint.r * k, p.Tint.g * k, p.Tint.b * k, 1f); }
            }

            if (WsCatalog.FxOnly(p.Src)) { GUILayout.Label("<color=#9A9A9A>Відтінок фарбує вогник, іскри й світло ефекту.</color>", _small); return; }

            // another piece's material
            GUILayout.Label("Матеріал: " + (string.IsNullOrEmpty(p.Mat) ? "свій" : WsCatalog.Label(p.Mat)), _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_pickMat ? "Вибери деталь ліворуч…" : "Узяти з іншої деталі…", _pickMat ? _on : GUI.skin.button))
            {
                _pickMat = !_pickMat;
                _note = _pickMat ? "Клацни у списку ліворуч деталь, з якої взяти матеріал (дерево, залізо, камінь…)." : "";
            }
            GUI.enabled = !string.IsNullOrEmpty(p.Mat);
            if (GUILayout.Button("Скинути матеріал", GUILayout.Width(130f))) { Change(); p.Mat = ""; _pickMat = false; _note = "Матеріал — знову свій."; }
            GUI.enabled = true;
            if (GUILayout.Button("На всі деталі", GUILayout.Width(110f))) { Change(); foreach (WsPart x in _m.Parts) if (x.Src != WsFiles.Seat) x.Mat = p.Mat; }
            GUILayout.EndHorizontal();

            // the glow
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(p.Glow ? "Світіння: є" : "Світіння: нема", p.Glow ? _on : GUI.skin.button, GUILayout.Width(120f))) { Change(); p.Glow = !p.Glow; }
            if (p.Glow) foreach (Color c in GlowSwatches) if (Swatch(c, p.GlowColor == c)) { Change(); p.GlowColor = c; }
            GUILayout.EndHorizontal();
            if (p.Glow) p.GlowPower = Slider("Сила", p.GlowPower, 5f);

            // the piece's living effects (only offered when it has any: a candle, a fire, a glowing stone…)
            if (p.Fx || WsCatalog.HasFx(p.Src))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.Fx ? "Живий ефект: є" : "Живий ефект: нема", p.Fx ? _on : GUI.skin.button, GUILayout.Width(150f)))
                { Change(); p.Fx = !p.Fx; _note = p.Fx ? "Деталь показує свій ефект, як у грі: вогник, іскри, світло." : "Ефект деталі вимкнено."; }
                GUILayout.Label("вогник, іскри, світло — як у грі", _small);
                GUILayout.EndHorizontal();
            }

            // everything of the look back to the piece's own (no tint, its own material, no glow); «Назад» undoes it
            bool changed = p.Tinted || !string.IsNullOrEmpty(p.Mat) || p.Glow;
            GUI.enabled = changed;
            if (GUILayout.Button("Скинути вигляд деталі (як у гри)")) { Change(); p.Tinted = false; p.Tint = Color.white; p.Mat = ""; p.Glow = false; _pickMat = false; _note = "Вигляд деталі скинуто: без відтінку, свій матеріал, без світіння."; }
            GUI.enabled = true;
        }

        private float Slider(string name, float v, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(40f));
            float n = GUILayout.HorizontalSlider(v, 0f, max);
            GUILayout.Label(n.ToString("0.00"), GUILayout.Width(36f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(n - v) > 0.0001f) SoftChange();
            return n;
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Мікс»: two or three effects of one layer laid on a raven together so they read as ONE effect, not a mush.
    //   Бальдр / Геймдаль (effects of the library): the amounts share one budget (two together are ~1.4 of one, not 2),
    //   only one of them keeps its light, and the mode decides how they sit:
    //     0 «Разом»            — on one spot, each a little smaller than the one before (they nest);
    //     1 «Ядро й оболонка»  — the first small and dense in the middle, the second wide and thin around it;
    //     2 «Рознести по тілу» — each on its own place (a trail: tail / both wing tips / back; an aura: back / both
    //                             wings / head), a pair halves its amount.
    //   Фрея (looks): one new look from the zones of the others:
    //     0 «Навпіл»           — every zone's colours mixed by the weights;
    //     1 «Тіло / крила»     — head, beak, eyes, body, legs from the first; wings, tips, tail from the second
    //                             (with three: tail and tips from the third);
    //     2 «Перехід»          — the first at the head, flowing into the second at the tail.
    //   «Одна барва»: the effects' own tints drawn 40% toward their common colour (for effects; looks mix anyway).
    // A mix is worn as any rune's wear (RavenWear.Dress, by its id in the wear code); the test window's mix is the
    // draft (Draft, id mx_draft, only on this computer); saved ones: config/RavenWatch/mixes/<id>.txt:
    //   name: Вогонь і сніг
    //   kind: h                       f | b | h (Фрея, Бальдр, Геймдаль)
    //   mode: 1
    //   desc: ...
    //   of h_vohon 1                  a source (a look id for Фрея, an effect id else) and its weight
    //   of h_snih 0.8
    //   harmony yes
    internal sealed class MixDef
    {
        public string Id = "", Name = "Новий мікс", Desc = "";
        public WearKind Kind = WearKind.Heimdall;
        public int Mode;
        public bool Harmony;
        public readonly List<string> Of = new List<string>();
        public readonly List<float> Weight = new List<float>();
        public System.DateTime SavedAt;

        internal float W(int i) { return i < Weight.Count ? Mathf.Clamp01(Weight[i]) : 1f; }

        internal string Code()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("name: ").Append(Name).Append('\n');
            sb.Append("kind: ").Append(WearCatalog.KindIds[(int)Kind]).Append('\n');
            sb.Append("mode: ").Append(Mode).Append('\n');
            if (Desc.Length > 0) sb.Append("desc: ").Append(Desc).Append('\n');
            for (int i = 0; i < Of.Count; i++) sb.Append("of ").Append(Of[i]).Append(' ').Append(W(i).ToString("0.##", CultureInfo.InvariantCulture)).Append('\n');
            if (Harmony) sb.Append("harmony yes\n");
            return sb.ToString();
        }

        internal static MixDef Parse(string text, string id)
        {
            MixDef m = new MixDef { Id = id ?? "" };
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int c = line.IndexOf(':');
                if (c > 0 && (line.IndexOf(' ') < 0 || line.IndexOf(' ') > c))
                {
                    string k = line.Substring(0, c).Trim().ToLowerInvariant(), v = line.Substring(c + 1).Trim();
                    if (k == "name") m.Name = v;
                    else if (k == "kind") { WearKind wk; if (WearCatalog.Kind(v, out wk)) m.Kind = wk; }
                    else if (k == "mode") { int n; if (int.TryParse(v, out n)) m.Mode = Mathf.Clamp(n, 0, 2); }
                    else if (k == "desc") m.Desc = v;
                    continue;
                }
                string[] a = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (a[0] == "of" && a.Length > 1 && m.Of.Count < FxMix.MaxSources)
                {
                    float w;
                    m.Of.Add(a[1]);
                    m.Weight.Add(a.Length > 2 && float.TryParse(a[2], NumberStyles.Float, CultureInfo.InvariantCulture, out w) ? Mathf.Clamp01(w) : 1f);
                }
                else if (a[0] == "harmony") m.Harmony = a.Length < 2 || a[1] == "yes";
            }
            return m;
        }

        internal MixDef Copy() { MixDef c = Parse(Code(), Id); c.SavedAt = SavedAt; return c; }
    }

    internal static class FxMix
    {
        internal const int MaxSources = 3;
        internal const string DraftId = "mx_draft";
        internal static int Version; // goes up when a mix changes: what wears it dresses again (WearCatalog.Stamp)

        internal static readonly string[] FxModes = { "Разом", "Ядро й оболонка", "Рознести по тілу" };
        internal static readonly string[] FxModeTips =
        {
            "в одному місці, кожен трохи менший за попередній — вкладаються один в одного",
            "перший малий і густий посередині, другий широкий і рідкий довкола",
            "кожен на своєму місці: слід — хвіст / обидва кінчики крил / спина; аура — спина / обидва крила / голова",
        };
        internal static readonly string[] LookModes = { "Навпіл", "Тіло / крила", "Перехід" };
        internal static readonly string[] LookModeTips =
        {
            "кольори кожної зони змішані за вагами",
            "голова, дзьоб, очі, тіло, лапи — від першого; крила, кінчики, хвіст — від другого (з трьома: хвіст і кінчики — від третього)",
            "перший на голові, плавно перетікає в другий на хвості",
        };

        // the test window's own, only on this computer: the mix being made (mx_draft) and one effect that no rune gives
        // shown alone (mx_solo_f/b/h); null takes it away
        private static readonly Dictionary<string, MixDef> Temp = new Dictionary<string, MixDef>();
        internal static MixDef Draft { get { return GetTemp(DraftId); } set { SetTemp(DraftId, value); } }
        internal static MixDef GetTemp(string id) { MixDef m; return Temp.TryGetValue(id, out m) ? m : null; }
        internal static void SetTemp(string id, MixDef m)
        {
            if (m == null) Temp.Remove(id); else { m.Id = id; Temp[id] = m; }
            Version++;
        }
        internal static bool IsTemp(string id) { return id == DraftId || (id ?? "").StartsWith("mx_solo_", System.StringComparison.Ordinal); }

        internal static string Dir { get { return Path.Combine(Path.Combine(BepInEx.Paths.ConfigPath, "RavenWatch"), "mixes"); } }

        // the mixes that ship in the mod (the user's approved ones get baked here)
        private static readonly string[] Builtin = { };

        private static List<MixDef> _all;
        private static int _allAt = -1;

        // built in, the saved files (while files are read), the draft
        internal static List<MixDef> All()
        {
            if (_all != null && _allAt == Version) return _all;
            _allAt = Version;
            Dictionary<string, MixDef> by = new Dictionary<string, MixDef>();
            foreach (string s in Builtin)
            {
                string id = "";
                foreach (string line in s.Split('\n')) if (line.StartsWith("id:")) id = line.Substring(3).Trim();
                by[id] = MixDef.Parse(s, id);
            }
            if (RavenLooks.FromFiles && Directory.Exists(Dir))
                foreach (string f in Directory.GetFiles(Dir, "*.txt"))
                {
                    try { MixDef d = MixDef.Parse(File.ReadAllText(f, Encoding.UTF8), Path.GetFileNameWithoutExtension(f)); d.SavedAt = File.GetLastWriteTimeUtc(f); by[d.Id] = d; }
                    catch (System.Exception e) { Perches.Log("mix " + f + ": " + e.Message); }
                }
            _all = new List<MixDef>(by.Values);
            _all.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.CurrentCulture));
            foreach (KeyValuePair<string, MixDef> kv in Temp) _all.Add(kv.Value);
            return _all;
        }

        internal static MixDef Find(string id) { foreach (MixDef m in All()) if (m.Id == id) return m; return null; }

        internal static void Save(MixDef m)
        {
            Directory.CreateDirectory(Dir);
            if (string.IsNullOrEmpty(m.Id) || IsTemp(m.Id))
            {
                string id = "mx_" + Perches.IdFor(m.Name), at = id;
                for (int n = 2; File.Exists(Path.Combine(Dir, at + ".txt")); n++) at = id + "_" + n;
                m.Id = at;
            }
            File.WriteAllText(Path.Combine(Dir, m.Id + ".txt"), m.Code(), Encoding.UTF8);
            Version++;
        }

        internal static void Delete(MixDef m)
        {
            string f = Path.Combine(Dir, m.Id + ".txt");
            if (File.Exists(f)) File.Delete(f);
            Version++;
        }

        // ---------------------------------------------------------------- Бальдр / Геймдаль: the parts to put on
        // (sources are any effects of the library, rune ones or not; a mix is never a source)
        private static readonly string[][] SpreadTrail = { new[] { "tail" }, new[] { "tipl", "tipr" }, new[] { "back" } };
        private static readonly string[][] SpreadAura = { new[] { "back" }, new[] { "wingl", "wingr" }, new[] { "head" } };

        internal static List<RavenFxDef> Parts(MixDef m)
        {
            List<RavenFxDef> src = new List<RavenFxDef>();
            List<float> w = new List<float>();
            for (int i = 0; i < m.Of.Count; i++)
            {
                RavenFxDef d = FxLib.Find(m.Of[i]);
                if (d != null && m.W(i) > 0.001f) { src.Add(d); w.Add(m.W(i)); }
            }
            List<RavenFxDef> parts = new List<RavenFxDef>();
            if (src.Count == 0) return parts;
            if (src.Count == 1) { RavenFxDef one = src[0].Copy(); one.Amount *= w[0]; parts.Add(one); return parts; }

            float budget = src.Count == 2 ? 0.7f : 0.55f; // each one's share: together ~1.4–1.65 of one alone
            Color mean = Color.black; int tinted = 0;
            foreach (RavenFxDef d in src) if (d.Tinted) { mean += d.Tint; tinted++; }
            if (tinted > 0) mean /= tinted;
            bool trail = m.Kind == WearKind.Heimdall;
            bool light = false;
            for (int i = 0; i < src.Count; i++)
            {
                RavenFxDef p = src[i].Copy();
                p.Kind = trail ? "trail" : "aura"; // (an aura mixed into the trails flies as a trail, and back)
                p.Amount *= budget * w[i];
                if (m.Harmony && p.Tinted && tinted > 1) p.Tint = Color.Lerp(p.Tint, mean, 0.4f);
                string[] bones = null;
                switch (m.Mode)
                {
                    case 0: // together: the first one's place, nesting
                        p.Bone = src[0].Bone; p.At = src[0].At;
                        p.Size *= 1f - 0.12f * i;
                        break;
                    case 1: // core and shell
                        if (i == 0) { p.Size *= 0.7f; p.Life *= 0.85f; p.Amount *= 1.15f; }
                        else if (i == 1) { p.Size *= 1.35f; p.Life *= 1.2f; p.Speed *= 0.85f; p.Amount *= 0.65f; }
                        p.Bone = src[0].Bone; p.At = src[0].At;
                        break;
                    default: // spread over the body
                        bones = (trail ? SpreadTrail : SpreadAura)[i % 3];
                        p.At = Vector3.zero;
                        break;
                }
                if (bones == null) bones = new[] { p.Bone };
                bool lit = false;
                for (int b = 0; b < bones.Length; b++)
                {
                    RavenFxDef q = b == 0 ? p : p.Copy();
                    q.Bone = bones[b];
                    if (bones.Length > 1) { q.Amount *= 0.5f; q.Size *= 0.8f; }
                    q.NoLight = light; // one light for the whole mix (two lights on a raven: too bright, twice the cost)
                    parts.Add(q);
                    if (!light && FxLib.HasLight(q.Base)) lit = true;
                }
                if (lit) light = true;
            }
            return parts;
        }

        // ---------------------------------------------------------------- Фрея: one look from the others
        // zone → which source (by mode 1 «Тіло / крила»): index into the sources (0, 1, 2; clipped to their count)
        private static readonly int[] SplitOf = { 0, 0, 0, 0, 0, 1, 2, 2, 0 };        // none body head beak eyes wings tips tail legs
        // zone → how far toward the last source (mode 2 «Перехід»)
        private static readonly float[] FlowOf = { 0f, 0.4f, 0f, 0f, 0f, 0.65f, 1f, 1f, 0.3f };

        internal static Look LookOf(MixDef m)
        {
            List<Look> src = new List<Look>();
            List<float> w = new List<float>();
            for (int i = 0; i < m.Of.Count; i++)
            {
                Look l = FindLook(m.Of[i]);
                if (l != null && m.W(i) > 0.001f) { src.Add(l); w.Add(m.W(i)); }
            }
            if (src.Count == 0) return null;
            Look r = Look.Parse(src[0].Code(false), m.Id);
            r.Name = m.Name; r.For = "mix"; r.Desc = m.Desc; r.Things.Clear(); r.Fx.Clear();
            if (src.Count == 1) return r;
            int n = src.Count;
            for (int z = 1; z < r.Zones.Length; z++)
            {
                if (m.Mode == 1) { CopyZone(r.Zones[z], Pick(src, Mathf.Min(SplitOf[z], n - 1), z)); continue; }
                float[] k = new float[n];
                if (m.Mode == 2)
                {
                    // the flow from the first (head) to the last (tail); a middle one sits at the middle of the body
                    float t = FlowOf[z] * (n - 1);
                    int lo = Mathf.Clamp(Mathf.FloorToInt(t), 0, n - 1), hi = Mathf.Min(lo + 1, n - 1);
                    k[lo] += 1f - (t - lo); k[hi] += t - lo;
                    for (int i = 0; i < n; i++) k[i] *= Mathf.Lerp(0.5f, 1f, w[i]); // (the weights still lean it)
                }
                else for (int i = 0; i < n; i++) k[i] = w[i];
                Blend(r.Zones[z], src, k, z);
            }
            // eyes, body glow, sheen, sparks: from whoever has them, mixed by weight (split: eyes and glow from the first)
            float[] all = new float[n];
            for (int i = 0; i < n; i++) all[i] = w[i];
            if (m.Mode == 1)
            {
                Look a = src[0], b = src[Mathf.Min(1, n - 1)];
                r.EyesOn = a.EyesOn || b.EyesOn; Look e = a.EyesOn ? a : b; r.Eyes = e.Eyes; r.EyePower = e.EyePower;
                r.GlowOn = a.GlowOn; r.Glow = a.Glow; r.GlowPower = a.GlowPower;
                r.SheenOn = b.SheenOn || a.SheenOn; Look s = b.SheenOn ? b : a; r.Sheen = s.Sheen; r.Metal = s.Metal; r.MetalGloss = s.MetalGloss; r.Gloss = s.Gloss;
                r.SparksOn = b.SparksOn || a.SparksOn; Look p = b.SparksOn ? b : a; r.Sparks = p.Sparks; r.SparkPower = p.SparkPower; r.SparkSize = p.SparkSize; r.SparkEdge = p.SparkEdge;
                return r;
            }
            float sum; Color c; float f1, f2, f3;
            // eyes
            sum = 0f; c = Color.black; f1 = 0f;
            for (int i = 0; i < n; i++) if (src[i].EyesOn) { sum += all[i]; c += src[i].Eyes * all[i]; f1 += src[i].EyePower * all[i]; }
            r.EyesOn = sum > 0f; if (sum > 0f) { r.Eyes = c / sum; r.EyePower = f1 / sum; }
            // glow (weaker when only some have it)
            sum = 0f; c = Color.black; f1 = 0f; float tot = 0f;
            for (int i = 0; i < n; i++) { tot += all[i]; if (src[i].GlowOn) { sum += all[i]; c += src[i].Glow * all[i]; f1 += src[i].GlowPower * all[i]; } }
            r.GlowOn = sum > 0f; if (sum > 0f) { r.Glow = c / sum; r.GlowPower = f1 / tot; }
            // sheen
            sum = 0f; c = Color.black; f1 = f2 = f3 = 0f;
            for (int i = 0; i < n; i++) if (src[i].SheenOn) { sum += all[i]; c += src[i].Sheen * all[i]; f1 += src[i].Metal * all[i]; f2 += src[i].MetalGloss * all[i]; f3 += src[i].Gloss * all[i]; }
            r.SheenOn = sum > 0f; if (sum > 0f) { r.Sheen = c / sum; r.Metal = f1 / sum; r.MetalGloss = f2 / sum; r.Gloss = f3 / sum; }
            // sparks
            sum = 0f; c = Color.black; f1 = f2 = f3 = 0f;
            for (int i = 0; i < n; i++) if (src[i].SparksOn) { sum += all[i]; c += src[i].Sparks * all[i]; f1 += src[i].SparkPower * all[i]; f2 += src[i].SparkSize * all[i]; f3 += src[i].SparkEdge * all[i]; }
            r.SparksOn = sum > 0f; if (sum > 0f) { r.Sparks = c / sum; r.SparkPower = f1 / tot; r.SparkSize = f2 / sum; r.SparkEdge = f3 / sum; }
            return r;
        }

        internal static Look FindLook(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (Look l in RavenLooks.AllLooks()) if (l.Id == id) return l;
            return null;
        }

        // that source's zone, or (it leaves the zone as in the game) the first source that paints it
        private static Look.Part Pick(List<Look> src, int i, int z)
        {
            if (src[i].Zones[z].On) return src[i].Zones[z];
            foreach (Look l in src) if (l.Zones[z].On) return l.Zones[z];
            return src[i].Zones[z];
        }

        private static void CopyZone(Look.Part to, Look.Part from) { to.On = from.On; to.Dark = from.Dark; to.Light = from.Light; to.Mix = from.Mix; to.Relief = from.Relief; }

        // a zone mixed by the weights; a source that leaves this zone as in the game counts as the game's colours at
        // strength 0 (it thins the others' strength, not their colour)
        private static void Blend(Look.Part to, List<Look> src, float[] k, int z)
        {
            float on = 0f, tot = 0f, mix = 0f, relief = 0f;
            Color dark = Color.black, light = Color.black;
            for (int i = 0; i < src.Count; i++)
            {
                tot += k[i];
                Look.Part p = src[i].Zones[z];
                if (!p.On || k[i] <= 0f) continue;
                on += k[i];
                dark += p.Dark * k[i]; light += p.Light * k[i];
                mix += p.Mix * k[i]; relief += p.Relief * k[i];
            }
            if (on <= 0f) { to.On = false; return; }
            to.On = true;
            to.Dark = dark / on; to.Light = light / on;
            to.Dark.a = 1f; to.Light.a = 1f;
            to.Mix = mix / Mathf.Max(on, tot);
            to.Relief = relief / on;
        }

        // the raven tree's way of weaving one layer of a в'язь (Bind.cs): green together, icy core and shell, charred over
        // the body (looks: half and half / body and wings / head to tail)
        internal static int ModeForTree(int treeKind) { return treeKind == 1 ? 0 : treeKind == 0 ? 1 : 2; } // (0 icy, 1 green, 2 charred)

        // ---------------------------------------------------------------- for the window: what a mix is made of, in words
        internal static string ModeName(MixDef m) { string[] n = m.Kind == WearKind.Freya ? LookModes : FxModes; return n[Mathf.Clamp(m.Mode, 0, n.Length - 1)]; }
    }
}
#endif

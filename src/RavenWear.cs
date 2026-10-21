#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // «Вбрання» ворона: what the runes give a raven to wear, three layers, each on its own:
    //   Фрея     — the feathers' colours, eyes and sheen (a look of RavenLooks with "for: rune");
    //   Бальдр   — a steady effect around the raven (an FxLib aura with "rune: yes");
    //   Геймдаль — a trail left in the air as it flies (an FxLib trail with "rune: yes").
    // The raven item keeps what it wears (customData RavenWatch_wear), so it goes with the raven wherever the raven
    // goes; the wearer sends it on the player's ZDO for the shoulders, a perch keeps it on its seat, a grave reads the
    // item. Every raven body is dressed by one call, RavenWear.Dress, from that one code.
    internal enum WearKind { Freya = 0, Baldr = 1, Heimdall = 2 }

    internal sealed class WearDef
    {
        public string Id = "", Name = "", Desc = "";
        public WearKind Kind;
        public Look Look;          // Фрея
        public RavenFxDef Fx;      // Бальдр, Геймдаль
        public MixDef Mix;         // a mix of several (FxMix): its parts for Бальдр / Геймдаль, its made look for Фрея
    }

    // what a rune can give: the looks and effects marked for runes, by layer
    internal static class WearCatalog
    {
        internal static readonly string[] KindIds = { "f", "b", "h" };
        internal static readonly string[] KindNames = { "Фрея", "Бальдр", "Геймдаль" };
        internal const int Kinds = 3;

        private static List<WearDef>[] _by;
        private static int _stamp = -1;

        // changes when a look or an effect is saved: what is worn is looked up again
        internal static int Stamp { get { return (RavenLooks.Version * 7919 + FxLib.Version) * 31 + FxMix.Version; } }

        // an id goes into the wear code and the rune record: none of their separators in it
        private static bool GoodId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(Bad) >= 0) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven wear catalog: «" + id + "» left out (no , : ; = | or spaces in a file name)"); return false; }
            return true;
        }
        private static readonly char[] Bad = { ',', ':', ';', '=', '|', '\n', ' ' };

        internal static List<WearDef> Of(WearKind k) { Build(); return _by[(int)k]; }

        internal static WearDef Find(WearKind k, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (WearDef d in Of(k)) if (d.Id == id) return d;
            foreach (WearDef d in _mixes) if (d.Kind == k && d.Id == id) return d;
            return null;
        }

        // the mixes (FxMix): worn by id like any other (saved ones, the test window's draft)
        private static List<WearDef> _mixes = new List<WearDef>();
        internal static List<WearDef> Mixes() { Build(); return _mixes; }

        internal static bool Kind(string s, out WearKind k)
        {
            k = WearKind.Freya;
            int i = System.Array.IndexOf(KindIds, (s ?? "").ToLowerInvariant());
            if (i < 0) return false;
            k = (WearKind)i;
            return true;
        }

        private static void Build()
        {
            int stamp = Stamp;
            if (_by != null && _stamp == stamp) return;
            _stamp = stamp;
            _by = new List<WearDef>[Kinds];
            for (int i = 0; i < Kinds; i++) _by[i] = new List<WearDef>();
            _mixes = new List<WearDef>();
            try
            {
                foreach (Look l in RavenLooks.AllLooks())
                {
                    if (l.For == "rune" && GoodId(l.Id)) _by[(int)WearKind.Freya].Add(new WearDef { Id = l.Id, Name = Lang.T(l.Name), Desc = Lang.T(l.Desc), Kind = WearKind.Freya, Look = l });
                }
                foreach (RavenFxDef d in FxLib.All())
                {
                    if (!d.Rune || !GoodId(d.Id)) continue;
                    WearKind k = d.Trail ? WearKind.Heimdall : WearKind.Baldr;
                    WearDef w = new WearDef { Id = d.Id, Name = Lang.T(d.Name), Desc = Lang.T(d.Desc), Kind = k, Fx = d };
                    _by[(int)k].Add(w);
                }
                foreach (MixDef m in FxMix.All())
                {
                    if (!GoodId(m.Id)) continue;
                    WearDef w = new WearDef { Id = m.Id, Name = Lang.T(m.Name), Desc = Lang.T(m.Desc), Kind = m.Kind, Mix = m };
                    if (m.Kind == WearKind.Freya) { w.Look = FxMix.LookOf(m); if (w.Look == null) continue; }
                    _mixes.Add(w);
                }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Raven wear catalog: " + e.Message); }
        }
    }

    // one raven's wear: the three layers by id, plus whatever a newer version wrote (kept as it is)
    internal sealed class Wear
    {
        internal const int Format = 1;
        public readonly string[] Ids = { "", "", "" };
        private readonly List<KeyValuePair<string, string>> _other = new List<KeyValuePair<string, string>>();

        internal string this[WearKind k] { get { return Ids[(int)k]; } set { Ids[(int)k] = Clean(value); } }

        // the в'язь this raven wears (Bind.cs; its id is its recipe): drawn over the runes of the layers it covers
        private string _bind = "";
        internal string Bind { get { return _bind; } set { _bind = Clean(value); } }

        internal bool Empty { get { if (_bind.Length > 0) return false; foreach (string s in Ids) if (s.Length > 0) return false; return true; } }

        internal static Wear Parse(string code)
        {
            Wear w = new Wear();
            if (string.IsNullOrEmpty(code)) return w;
            foreach (string part in code.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                if (k == "v") continue;
                if (k == "x") { w._bind = v; continue; }
                int i = System.Array.IndexOf(WearCatalog.KindIds, k);
                if (i >= 0) w.Ids[i] = v; else w._other.Add(new KeyValuePair<string, string>(k, v));
            }
            return w;
        }

        // "" when it wears nothing
        internal string Code()
        {
            if (Empty && _other.Count == 0) return "";
            StringBuilder sb = new StringBuilder("v=").Append(Format);
            for (int i = 0; i < Ids.Length; i++) if (Ids[i].Length > 0) sb.Append(';').Append(WearCatalog.KindIds[i]).Append('=').Append(Ids[i]);
            if (_bind.Length > 0) sb.Append(";x=").Append(_bind);
            foreach (KeyValuePair<string, string> kv in _other) sb.Append(';').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }

        private static string Clean(string s) { return (s ?? "").Replace(";", "").Replace("=", "").Replace("|", "").Replace("\n", "").Trim(); }
    }

    internal static class RavenWear
    {
        internal const string ItemKey = "RavenWatch_wear";                          // on the raven item
        internal const string KeyH = "RavenWatch_WearH", KeyM = "RavenWatch_WearM"; // on the player's ZDO: what the shoulders wear

        // ---------------------------------------------------------------- the raven item
        internal static string Code(ItemDrop.ItemData item)
        {
            string c;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(ItemKey, out c) ? c ?? "" : "";
        }

        internal static Wear Of(ItemDrop.ItemData item) { return Wear.Parse(Code(item)); }

        internal static void Set(ItemDrop.ItemData item, WearKind k, string id)
        {
            if (item == null) return;
            Wear w = Of(item);
            w[k] = id;
            string c = w.Code();
            if (c.Length == 0) item.m_customData.Remove(ItemKey); else item.m_customData[ItemKey] = c;
        }

        internal static void SetBind(ItemDrop.ItemData item, string id)
        {
            if (item == null) return;
            Wear w = Of(item);
            w.Bind = id;
            string c = w.Code();
            if (c.Length == 0) item.m_customData.Remove(ItemKey); else item.m_customData[ItemKey] = c;
        }

        // the local wearer tells everyone what the shoulders wear
        internal static void Publish(Player p, ItemDrop.ItemData hugin, ItemDrop.ItemData munin)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            RuneBook.KeepOwn(p, hugin); RuneBook.KeepOwn(p, munin); // only what your runes give
            if (hugin != null && munin != null) // each rune on one raven only (put on one while the other was off the shoulder): Munin gives it up
            {
                Wear wh = Of(hugin), wm = Of(munin);
                for (int k = 0; k < WearCatalog.Kinds; k++) if (wh.Ids[k].Length > 0 && wh.Ids[k] == wm.Ids[k]) Set(munin, (WearKind)k, "");
                if (wh.Bind.Length > 0 && wh.Bind == wm.Bind) SetBind(munin, "");
            }
            string h = Code(hugin), m = Code(munin);
            if (zdo.GetString(KeyH, "") != h) zdo.Set(KeyH, h);
            if (zdo.GetString(KeyM, "") != m) zdo.Set(KeyM, m);
        }

        // what any player's raven wears, as that player sent it
        internal static string OnPlayer(Player p, bool munin)
        {
            ZDO zdo = NetFlag.Zdo(p, false);
            return zdo != null ? zdo.GetString(munin ? KeyM : KeyH, "") : "";
        }

        // ---------------------------------------------------------------- a raven body
        // Dresses one raven body in this code (cheap when nothing changed: call it as often as the outfits).
        internal static void Dress(GameObject body, bool munin, string code)
        {
            if (body == null) return;
            if (code == null) code = "";
            WearState st = body.GetComponent<WearState>();
            if (st == null) { if (code.Length == 0) return; st = body.AddComponent<WearState>(); }
            int stamp = WearCatalog.Stamp;
            if (st.Code == code && st.Stamp == stamp && st.Munin == munin) return;
            if (Shadow.Fading(body)) return; // in Hugin's shadow its materials are the shadow's: dressed when it is over
            long t0 = Slow.Start();
            try
            {
                TakeOff(body, st);
                st.Code = code; st.Stamp = stamp; st.Munin = munin;
                Wear w = Wear.Parse(code);
                BindLook.Visual bv = w.Bind.Length > 0 ? BindLook.Of(w.Bind) : null;
                WearDef f = WearCatalog.Find(WearKind.Freya, w[WearKind.Freya]);
                Look look = bv != null && bv.Has(WearKind.Freya) ? bv.Look : f != null ? f.Look : null;
                if (look != null) Paint(body, st, look, munin);
                for (int k = (int)WearKind.Baldr; k <= (int)WearKind.Heimdall; k++)
                {
                    List<RavenFxDef> parts = null;
                    if (bv != null && bv.Has((WearKind)k)) parts = bv.Fx[k];
                    else
                    {
                        WearDef d = WearCatalog.Find((WearKind)k, w.Ids[k]);
                        if (d == null) continue;
                        parts = d.Mix != null ? FxMix.Parts(d.Mix) : d.Fx != null ? new List<RavenFxDef> { d.Fx } : null;
                    }
                    if (parts == null) continue;
                    foreach (RavenFxDef part in parts)
                    {
                        FxOn on = FxLib.Put(body, part, munin, body.layer);
                        if (on != null) { on.OnlyFlying = k == (int)WearKind.Heimdall && !st.Stage; on.Worn = true; st.Fx.Add(on); }
                    }
                }
            }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Raven wear: " + e); }
            ShoulderLife life = body.GetComponent<ShoulderLife>();
            if (life != null) life.EyesAgain(); // the eyes' glow made again on what it wears now
            Slow.End(t0, "raven wear");
        }

        // the raven's own renderers (not what it wears or holds, not effects)
        private static bool Own(Renderer r)
        {
            return r != null && !(r is ParticleSystemRenderer) && r.GetComponentInParent<FxOn>() == null
                && r.GetComponentInParent<Outfits.Piece>() == null && r.GetComponentInParent<RavenFetch.Carried>() == null;
        }

        private static void Paint(GameObject body, WearState st, Look look, bool munin)
        {
            RavenLooks.Painted p = RavenLooks.Use(look, munin);
            if (p == null) { if (Plugin.Instance != null) Plugin.Instance.Log("Raven wear: look " + look.Id + " not painted (" + RavenLooks.Problem + ")"); return; }
            st.Paint = p;
            List<Renderer> rs = new List<Renderer>();
            List<Material[]> orig = new List<Material[]>();
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!Own(r)) continue;
                Material[] cur = r.sharedMaterials;
                bool body0 = false;
                Material[] game = new Material[cur.Length];
                for (int i = 0; i < cur.Length; i++) { game[i] = EyeGlow.Original(cur[i]); body0 |= RavenLooks.IsBody(game[i]); }
                if (!body0) continue;
                rs.Add(r); orig.Add(game);
                r.sharedMaterials = RavenLooks.Dress(p, game);
            }
            st.Rs = rs.ToArray(); st.Orig = orig.ToArray();
        }

        private static void TakeOff(GameObject body, WearState st)
        {
            if (st.Rs != null)
                for (int i = 0; i < st.Rs.Length; i++) if (st.Rs[i] != null) st.Rs[i].sharedMaterials = st.Orig[i];
            st.Rs = null; st.Orig = null;
            RavenLooks.Drop(st.Paint);
            st.Paint = null;
            foreach (FxOn f in st.Fx) if (f != null) f.Off();
            st.Fx.Clear();
        }

        // ---------------------------------------------------------------- console (for testing, not in the docs)
        // raven wear                              what both ravens wear + the catalog
        // raven wear ui                           the test window's tab «Руни» (every effect with a code, the mix)
        // raven wear list                         every rune look and effect, by layer
        // raven wear [hugin|munin] f|b|h <id>|-   put it on (or take that layer off)
        internal static string Cmd(Player p, string[] a)
        {
            if (a.Length > 1 && a[1].ToLowerInvariant() == "ui") { DevPanel.Toggle(DevPanel.RunesTab); return DevPanel.Said(); }
            if (a.Length > 1 && a[1].ToLowerInvariant() == "list")
            {
                StringBuilder sb = new StringBuilder();
                for (int k = 0; k < WearCatalog.Kinds; k++)
                {
                    List<WearDef> l = WearCatalog.Of((WearKind)k);
                    sb.Append(WearCatalog.KindNames[k]).Append(" (").Append(WearCatalog.KindIds[k]).Append(", ").Append(l.Count).Append("): ");
                    for (int i = 0; i < l.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(l[i].Id); }
                    sb.Append('\n');
                }
                return sb.ToString().TrimEnd();
            }
            int at = 1;
            bool munin = false;
            if (a.Length > at && (a[at].ToLowerInvariant().StartsWith("h") && a[at].Length > 1 || a[at].ToLowerInvariant().StartsWith("m"))) { munin = a[at].ToLowerInvariant().StartsWith("m"); at++; }
            WearKind kind;
            if (a.Length > at + 1 && WearCatalog.Kind(a[at], out kind))
            {
                ItemDrop.ItemData item = Trust.Worn(munin);
                if (item == null) return Plugin.RavenName(munin) + " не на плечі.";
                string id = a[at + 1] == "-" ? "" : a[at + 1];
                if (id.Length > 0 && WearCatalog.Find(kind, id) == null) return "Нема «" + id + "» серед рун " + WearCatalog.KindNames[(int)kind] + " (raven wear list).";
                Set(item, kind, id);
                ItemDrop.ItemData hugin, mun;
                RavenItem.Worn(p, out hugin, out mun);
                Publish(p, hugin, mun);
                Dress(Shoulder.LocalModel(munin), munin, Code(item));
                return Plugin.RavenName(munin) + ": " + WearCatalog.KindNames[(int)kind] + " — " + (id.Length > 0 ? id : "зняв") + ".";
            }
            ItemDrop.ItemData h = Trust.Worn(false), m = Trust.Worn(true);
            int nf = WearCatalog.Of(WearKind.Freya).Count, nb = WearCatalog.Of(WearKind.Baldr).Count, nh = WearCatalog.Of(WearKind.Heimdall).Count;
            return "Хугін: " + Say(h) + "\nМунін: " + Say(m) + "\nУ каталозі: Фрея " + nf + ", Бальдр " + nb + ", Геймдаль " + nh
                + ".\nraven wear list · raven wear [hugin|munin] f|b|h <id>|-";
        }

        private static string Say(ItemDrop.ItemData item)
        {
            if (item == null) return "не на плечі";
            Wear w = Of(item);
            if (w.Empty) return "нічого";
            List<string> l = new List<string>();
            for (int k = 0; k < WearCatalog.Kinds; k++) if (w.Ids[k].Length > 0) l.Add(WearCatalog.KindNames[k] + " " + w.Ids[k] + (WearCatalog.Find((WearKind)k, w.Ids[k]) == null ? " (згасла)" : ""));
            return string.Join(", ", l.ToArray());
        }
    }

    // on a raven body: what it wears now, and the game's own materials to put back
    internal sealed class WearState : MonoBehaviour
    {
        internal string Code = "";
        internal int Stamp = -1;
        internal bool Munin;
        internal bool Stage;      // a test stage's raven: a trail is left all the time (no one says it flies)
        internal Renderer[] Rs;
        internal Material[][] Orig;
        internal RavenLooks.Painted Paint;
        internal readonly List<FxOn> Fx = new List<FxOn>();

        private void OnDestroy()
        {
            RavenLooks.Drop(Paint); Paint = null;
            foreach (FxOn f in Fx) if (f != null) f.Off();
            Fx.Clear();
        }
    }
}
#endif

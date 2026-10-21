#if RAVEN_ITEM
using System.Collections.Generic;

namespace HuginWhispers
{
    // The eight elements of the rune effects (binds-plan.md §3). Every look and effect of the catalog has one: what a
    // rune of Бальдр does in a в'язь, when a rune of Геймдаль fires it, how a rune of Фрея changes it — and «бонус
    // стихії» when one element comes again in a в'язь. A file may set its own («element: fire»), else the table below.
    internal enum Element { None = -1, Fire = 0, Ice = 1, Lightning = 2, Life = 3, Shadow = 4, Blood = 5, Runes = 6, Wind = 7 }

    internal static class Elements
    {
        internal const int Count = 8;
        internal static readonly string[] Ids = { "fire", "ice", "lightning", "life", "shadow", "blood", "runes", "wind" };
        internal static readonly string[] Names = Lang.Keys("Вогонь", "Лід", "Блискавка", "Життя", "Тінь", "Кров", "Руни", "Вітер");
        internal static readonly string[] Hex = { "FF7A30", "8FD4FF", "B49CFF", "8FD860", "9A96A8", "D23A3A", "E8C060", "D8E4E4" };

        // the catalog's own (the user's table, 2026-10-20)
        private static readonly Dictionary<string, Element> Default = new Dictionary<string, Element>
        {
            { "b_zharyny", Element.Fire }, { "b_inii", Element.Ice }, { "b_iskry", Element.Lightning }, { "b_svitliachky", Element.Life },
            { "b_spory", Element.Life }, { "b_dym", Element.Shadow }, { "b_tuman", Element.Blood }, { "b_runy", Element.Runes },
            { "h_vohon", Element.Fire }, { "h_snih", Element.Ice }, { "h_blyskavka", Element.Lightning }, { "h_svitliachky", Element.Life },
            { "h_dym", Element.Shadow }, { "h_bile_pirya", Element.Blood }, { "h_runy", Element.Runes }, { "h_pirya", Element.Wind },
            { "f_sontse", Element.Fire }, { "f_inii", Element.Ice }, { "f_burya", Element.Lightning }, { "f_moh", Element.Life },
            { "f_popil", Element.Shadow }, { "f_nich", Element.Shadow }, { "f_krov", Element.Blood }, { "f_zoloto", Element.Runes },
        };

        internal static Element Parse(string s)
        {
            int i = System.Array.IndexOf(Ids, (s ?? "").Trim().ToLowerInvariant());
            return i >= 0 ? (Element)i : Element.None;
        }

        internal static string IdOf(Element e) { return e == Element.None ? "" : Ids[(int)e]; }
        internal static string NameOf(Element e) { return e == Element.None ? Lang.T("без стихії") : Lang.T(Names[(int)e]); }
        internal static string Colored(Element e) { return e == Element.None ? NameOf(e) : "<color=#" + Hex[(int)e] + ">" + NameOf(e) + "</color>"; }

        // a catalog entry's element: its file's own, else the table (kept until the catalog changes: the windows ask it often)
        private static readonly Dictionary<string, Element>[] Known = { new Dictionary<string, Element>(), new Dictionary<string, Element>(), new Dictionary<string, Element>() };
        private static int _knownAt = -1;

        internal static Element Of(WearKind k, string id)
        {
            if (string.IsNullOrEmpty(id)) return Element.None;
            int stamp = WearCatalog.Stamp;
            if (stamp != _knownAt) { foreach (Dictionary<string, Element> d in Known) d.Clear(); _knownAt = stamp; }
            Dictionary<string, Element> of = Known[(int)k];
            Element e;
            if (!of.TryGetValue(id, out e)) { e = Find(k, id); of[id] = e; }
            return e;
        }

        private static Element Find(WearKind k, string id)
        {
            string own = "";
            if (k == WearKind.Freya) { Look l = FxMix.FindLook(id); if (l != null) own = l.Element; }
            else { RavenFxDef d = FxLib.Find(id); if (d != null) own = d.Element; }
            Element e = Parse(own);
            if (e != Element.None) return e;
            return Default.TryGetValue(id, out e) ? e : Element.None;
        }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HuginWhispers
{
    // A piece of a place the story wants touched (Loc.Touches, «touch: shchyt step prologue munin_shield»; «-» — always): the piece of
    // that name (LocParts names) glows (not with «quiet») while the story's conditions hold for you, and then E on it:
    //   · «read: <name> [<conditions> ::] <words>» — the first of its lines whose conditions hold, on the game's rune page (the Hall's old
    //     letter, Eirik's carving: «\n» a new line); which line was read is the character's for good (ReadFlag: the condition «read», the
    //     book's «quote:» — StorySaga), known by its conditions: a piece's lines may be put in any order and new ones added, but a line
    //     whose conditions are rewritten counts as not read;
    //   · the story's event «use <place>.<name>» — a quest's «on:» goes on from it (the shield of Munin's place starts its scene);
    //   · «elsedo: <name> <actions>» — when no stage of yours would pass on this very touch (StoryQuests.Awaits: its «on:» and its «wait:»;
    //     the letter read outside the prologue's moment: heartlearn at once — there is no scene to wait for).
    // Out of those conditions it is just a piece again. An item stand keeps its own look and rules: LocInteractPatch (E) and the stand's hover patches ask this first.
    internal sealed class LocTouch : MonoBehaviour, Hoverable, Interactable
    {
        internal string Place = "", Name = "";
        internal static float UsedAt = -100f;
        // (UsedAt: the last touch: a scene it starts and that must wait says why — StoryScenes; UsedKey: which: the cond «touched»)
        internal static string UsedKey = "";
        internal bool On, Quiet;
        private GameObject _glint;
        private const float Near = 60f; // m from the place's middle
        private const float TellNear = 25f; // m: this near its pieces are loaded for certain, so a piece not found is really not there

        private static readonly HashSet<string> _told = new HashSet<string>(); // (said once a game)

        // LocState's tick of a place near you: each named piece glows (or not) for you
        internal static void Tick(Loc l, Player me)
        {
            if (l.Touches.Count == 0) return;
            Vector3 at; Quaternion rot;
            if (!l.Anchor(out at, out rot)) return;
            Vector3 d = me.transform.position - at; d.y = 0f;
            if (d.sqrMagnitude > Near * Near) return; // (only near it: the pieces are not loaded farther anyway)
            foreach (KeyValuePair<string, string> t in l.Touches)
            {
                bool on = t.Value.Length == 0 || StoryCond.Holds(me, t.Value, e => l.Log("touch: " + e)); // (said once; a wrong line never glows)
                List<ZNetView> named = LocParts.Named(l.Id, t.Key);
                if (on && named.Count == 0 && d.sqrMagnitude < TellNear * TellNear && _told.Add(l.Id + "." + t.Key)) l.Log("touch «" + t.Key + "»: no piece of that name here, nothing can be touched (its name and its place in the blueprint differ?)");
                foreach (ZNetView nv in named)
                {
                    LocTouch lt = nv.GetComponent<LocTouch>();
                    if (lt == null) { if (!on) continue; lt = nv.gameObject.AddComponent<LocTouch>(); lt.Place = l.Id; lt.Name = t.Key; lt.Quiet = l.TouchQuiet.Contains(t.Key); }
                    lt.Set(on);
                }
            }
        }

        private void Set(bool on)
        {
            On = on;
            if (on && _glint == null && !Quiet)
            {
                ItemStand st = GetComponent<ItemStand>();
                _glint = WsItems.StoryGlint(st != null && st.m_attachOther != null ? st.m_attachOther.gameObject : gameObject); // (on the thing hung there: the shield)
            }
            if (_glint != null && _glint.activeSelf != on) _glint.SetActive(on);
        }

        private void OnDestroy() { if (_glint != null) Destroy(_glint); }

        internal string Title()
        {
            ItemStand st = GetComponent<ItemStand>();
            GameObject hung = st != null && st.HaveAttachment() && ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(st.GetAttachedItem()) : null;
            ItemDrop d = hung != null ? hung.GetComponent<ItemDrop>() : null;
            string item = d != null ? d.m_itemData.m_shared.m_name : ""; // (the thing hung there: «Щит з обідком»)
            if (item.Length == 0) { Piece pc = GetComponent<Piece>(); item = pc != null ? pc.m_name : Name; }
            return Localization.instance != null ? Localization.instance.Localize(item) : item;
        }

        public string GetHoverName() { return On ? Title() : ""; }
        public float GetHoverOffset() { return 0f; }

        public string GetHoverText()
        {
            if (!On) return "";
            Loc l = Locs.Get(Place);
            string t = Title() + "\n[<color=yellow><b>$KEY_Use</b></color>] " + (l != null && l.Reads.ContainsKey(Name) ? Lang.T("Прочитати") : Lang.T("Роздивитися"));
            return Localization.instance != null ? Localization.instance.Localize(t) : t;
        }

        // a touch, by E or by the author's test (raven story touch, the studio's scene check): one way for all
        internal static void Touch(string key, Vector3 at)
        {
            UsedAt = Time.time; UsedKey = key;
            StoryBus.Raise("use", key, 1, at);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player p = user as Player;
            if (!On || hold || p == null || p != Player.m_localPlayer) return false;
            Loc l = Locs.Get(Place);
            string key = Place + "." + Name;
            if (l != null) Read(l, p);
#if RAVEN_STORY
            string act;
            bool awaited = StoryQuests.Awaits(p, "use", key); // (asked before the event: what a stage waits for, it takes)
#endif
            Touch(key, transform.position);
#if RAVEN_STORY
            if (l != null && !awaited && l.ElseDo.TryGetValue(Name, out act))
                foreach (string a in act.Split(';')) if (a.Trim().Length > 0) StoryActions.Do(p, null, a.Trim());
#endif
            return l != null && l.TouchAnim.Contains(Name); // (the game plays its reach-and-touch only on true: none unless the place asks for it, the scene takes him at once)
        }

        // its words on the game's rune page: the first «read:» line whose conditions hold for you
        private void Read(Loc l, Player p)
        {
            List<KeyValuePair<string, string>> reads;
            if (!l.Reads.TryGetValue(Name, out reads) || TextViewer.instance == null) return;
            for (int i = 0; i < reads.Count; i++)
            {
                KeyValuePair<string, string> r = reads[i];
                if (r.Key.Length > 0 && !StoryCond.Holds(p, r.Key, e => l.Log("read: " + e))) continue;
                TextViewer.instance.ShowText(TextViewer.Style.Rune, Title(), Shown(r.Value), true);
                Story.SetFlag(p, ReadFlag(l.Id, Name, r.Key), true);
                return;
            }
        }

        // The words of a «read:» line as this game shows them (the rune page here, the book's «quote:»): translated, and each
        // «{pool <place>.<name> <N> [<in a row>]}» in them replaced by N names of that place's «pool:» line, <in a row> (3) to a row. Which N
        // is the world's: by its seed, so every player of a world reads the same names and another world has others; nothing is kept. They
        // are chosen by their numbers in the list, not by the words, so every language shows the same people; a list with names added later
        // changes some of the names in the worlds already begun.
        private static readonly Regex PoolWord = new Regex(@"\{pool (\w+)\.(\w+) (\d+)(?: (\d+))?\}",RegexOptions.CultureInvariant);
        internal static string Shown(string raw)
        {
            string t = Lang.T(raw ?? "");
            if (t.IndexOf("{pool ", StringComparison.Ordinal) < 0) return t;
            return PoolWord.Replace(t, m => Drawn(m.Groups[1].Value, m.Groups[2].Value, int.Parse(m.Groups[3].Value), m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 3));
        }

        private static string Drawn(string place, string pool, int n, int row)
        {
            Loc l = Locs.Get(place);
            string list;
            if (l == null || !l.Pools.TryGetValue(pool, out list))
            {
                if (_told.Add("pool." + place + "." + pool)) Story.Log("read: no «pool: " + pool + "» in the place «" + place + "», its names are not shown");
                return "";
            }
            string[] src = list.Split(','), names = Lang.T(list).Split(',');
            if (names.Length != src.Length)
            {
                if (_told.Add("poollang." + place + "." + pool)) l.Log("pool «" + pool + "»: " + src.Length + " names as written, " + names.Length + " in this language — shown as written");
                names = src;
            }
            uint seed = (uint)((WorldGenerator.instance != null ? WorldGenerator.instance.GetSeed() : 0) ^ (place + "." + pool).GetStableHashCode());
            uint[] keys = new uint[names.Length];
            int[] order = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                uint h = seed ^ ((uint)(i + 1) * 2654435761u); // (a name's own number with the world's seed: a name added to the list's end moves no other)
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                keys[i] = h; order[i] = i;
            }
            Array.Sort(keys, order);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < order.Length && i < n; i++)
                sb.Append(i == 0 ? "" : i % Mathf.Max(1, row) == 0 ? "\n" : " · ").Append(names[order[i]].Trim());
            return sb.ToString();
        }

        // a line read: the character's, written once («read.<place>.<piece>.<its conditions as a few letters>», «0» for the line with none; the
        // words are the same in every world). By its conditions, not its place among the lines: a line put in before it changes nothing
        private static string ReadFlag(string place, string name, string when) { return "read." + place + "." + name + "." + (when.Length == 0 ? "0" : ((uint)when.GetStableHashCode()).ToString("x")); }

        // what this player has read on a piece («<place>.<piece>»): of the lines read, the first as written (the one with conditions — the
        // fullest), untranslated; «»: nothing read, or no such piece
        internal static string ReadBy(Player p, string piece)
        {
            int dot = (piece ?? "").IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(piece.Substring(0, dot)) : null;
            List<KeyValuePair<string, string>> reads;
            if (p == null || l == null || !l.Reads.TryGetValue(piece.Substring(dot + 1), out reads)) return "";
            foreach (KeyValuePair<string, string> r in reads)
                if (Story.Flag(p, ReadFlag(l.Id, piece.Substring(dot + 1), r.Key))) return r.Value;
            return "";
        }

        // «read <place>.<piece> full»: he has read the piece's first «read:» line — the full words (Eirik's carving by the Watch's fire), not a
        // later line that only says they cannot be read
        internal static bool ReadFull(Player p, string piece)
        {
            int dot = (piece ?? "").IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(piece.Substring(0, dot)) : null;
            List<KeyValuePair<string, string>> reads;
            if (p == null || l == null || !l.Reads.TryGetValue(piece.Substring(dot + 1), out reads) || reads.Count == 0) return false;
            return Story.Flag(p, ReadFlag(l.Id, piece.Substring(dot + 1), reads[0].Key));
        }

        // «read <place>.<piece>»: what is wrong with these words (null: fine)
        internal static string ReadCheck(string piece)
        {
            int dot = (piece ?? "").IndexOf('.');
            Loc l = dot > 0 ? Locs.Get(piece.Substring(0, dot)) : null;
            if (l == null) return "read <місце>.<деталь> — нема місця «" + piece + "»";
            return l.Reads.ContainsKey(piece.Substring(dot + 1)) ? null : "у місця «" + l.Id + "» нема рядка read: для «" + piece.Substring(dot + 1) + "»";
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif

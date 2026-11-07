#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A piece of a place the story wants touched (Loc.Touches, «touch: shchyt step prologue munin_shield»; «-» — always): the piece of
    // that name (LocParts names) glows (not with «quiet») while the story's conditions hold for you, and then E on it:
    //   · «read: <name> [<conditions> ::] <words>» — the first of its lines whose conditions hold, on the game's rune page (the Hall's old
    //     letter, Eirik's carving: «\n» a new line);
    //   · the story's event «use <place>.<name>» — a quest's «on:» goes on from it (the shield of Munin's place starts its scene);
    //   · «elsedo: <name> <actions>» — when no stage of yours waits for this very touch (the letter read outside the prologue's moment:
    //     heartlearn at once — there is no scene to wait for).
    // Out of those conditions it is just a piece again. An item stand keeps its own look and rules: BlueprintStandPatch asks this first.
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
            foreach (KeyValuePair<string, string> r in reads)
            {
                if (r.Key.Length > 0 && !StoryCond.Holds(p, r.Key, e => l.Log("read: " + e))) continue;
                TextViewer.instance.ShowText(TextViewer.Style.Rune, Title(), Lang.T(r.Value), true);
                return;
            }
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif

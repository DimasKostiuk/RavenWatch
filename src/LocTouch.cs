#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A piece of a place the story wants touched (Loc.Touches, «touch: shchyt step prologue munin_shield»): the piece of
    // that name (HallParts names) glows like the Hall's old letter while the story's conditions hold for you, and then E
    // on it raises the story's event «use <place>.<name>» — a quest's «on:» goes on from it (the shield of Munin's place
    // starts its scene). Out of those conditions it is just a piece again. An item stand keeps its own look and rules:
    // BlueprintStandPatch asks this first.
    internal sealed class LocTouch : MonoBehaviour, Hoverable, Interactable
    {
        internal string Place = "", Name = "";
        internal bool On;
        private GameObject _glint;
        private const float Near = 60f; // m from the place's middle

        private static readonly Dictionary<string, List<Cond>> _conds = new Dictionary<string, List<Cond>>();

        // HallState's tick of a place near you: each named piece glows (or not) for you
        internal static void Tick(Loc l, Player me)
        {
            Vector3 at; Quaternion rot;
            if (!l.Anchor(out at, out rot)) return;
            Vector3 d = me.transform.position - at; d.y = 0f;
            if (d.sqrMagnitude > Near * Near) return; // (only near it: the pieces are not loaded farther anyway)
            foreach (KeyValuePair<string, string> t in l.Touches)
            {
                List<Cond> c;
                if (!_conds.TryGetValue(t.Value, out c))
                {
                    List<string> errs = new List<string>();
                    c = StoryCond.Parse(t.Value, errs);
                    foreach (string e in errs) l.Log("touch: " + e);
                    _conds[t.Value] = c = errs.Count == 0 ? c : null; // (said once; a wrong line never glows)
                }
                bool on = c != null && StoryCond.All(me, c);
                foreach (ZNetView nv in HallParts.Named(l.Id, t.Key))
                {
                    LocTouch lt = nv.GetComponent<LocTouch>();
                    if (lt == null) { if (!on) continue; lt = nv.gameObject.AddComponent<LocTouch>(); lt.Place = l.Id; lt.Name = t.Key; }
                    lt.Set(on);
                }
            }
        }

        private void Set(bool on)
        {
            On = on;
            if (on && _glint == null)
            {
                ItemStand st = GetComponent<ItemStand>();
                _glint = WsItems.Glint(st != null && st.m_attachOther != null ? st.m_attachOther.gameObject : gameObject, 3f); // (on the thing hung there: the shield)
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
            string t = Title() + "\n[<color=yellow><b>$KEY_Use</b></color>] " + Lang.T("Роздивитися");
            return Localization.instance != null ? Localization.instance.Localize(t) : t;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!On || hold || user != Player.m_localPlayer) return false;
            StoryBus.Raise("use", Place + "." + Name, 1, transform.position);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HuginWhispers
{
    // The author's look at the states on the Hall he builds by hand (its frame in this world: BlueprintFrame): only
    // drawn, only in his game — no strength written, nothing kept; off: everything as it was. A Hall a blueprint put
    // into the world is not touched (it shows the world's own state).
    internal static class LocPreview
    {
        internal static int State { get; private set; } // 0 — off
        internal static int Sub { get; private set; }   // its sub-state looked at (0 — the state itself)
        internal static Loc Of { get; private set; }    // the place looked at (the author's own, by its frame)
        private static readonly Dictionary<int, List<Component>> _hidden = new Dictionary<int, List<Component>>();
        private static readonly Dictionary<int, List<Component>> _dimmed = new Dictionary<int, List<Component>>();
        internal static readonly HashSet<int> Unlit = new HashSet<int>();
        private static readonly HashSet<ZNetView> _tinted = new HashSet<ZNetView>();
        private static readonly HashSet<WearNTear> _drawn = new HashSet<WearNTear>();
        private static readonly Dictionary<int, string> _layer = new Dictionary<int, string>(); // a piece's layer, till the author's files change
        private static int _layerVersion = -1;
        private static readonly string NotHere = new string('-', 2); // (its own object: compared by reference)
        private static float _next;

        internal static string Set(int s) { return Of != null ? Set(Of, s, 0) : ""; } // (off, or another state of the place looked at)
        internal static string Again() { return Of != null ? Set(Of, State, Sub) : ""; } // (the look again at once)

        internal static string Set(Loc l, int s, int sub = 0)
        {
            Vector3 c; Quaternion rot;
            if (l != Of && Of != null) { State = 0; Sub = 0; Off(); } // (another place: the one looked at before as it is)
            Of = l;
            if (s != 0 && !BlueprintFrame.Get(l.Id, out c, out rot)) return "Спершу рамка «" + l.Title + "» у цьому світі (збережи або «знайти рамку»).";
            int was = State * 100 + Sub;
            State = Mathf.Clamp(s, 0, l.Last);
            Sub = State == 0 ? 0 : Mathf.Clamp(sub, 0, l.SubsOf(State));
            if (was != State * 100 + Sub && BlueprintFrame.Get(l.Id, out c, out rot)) LocState.ClearSmoke(c, LocParts.AuthorReach); // (the look changed: no smoke of what it hid)
            if (State == 0) Off(); else _next = 0f;
            return State == 0 ? "Перегляд вимкнено: «" + l.Title + "» така, як є." : "Перегляд: «" + l.Title + "» як «" + l.WordOf(State, Sub) + "» (лише в тебе, у світі нічого не змінено).";
        }

        internal static void Tick()
        {
            if (State == 0 || Time.time < _next) return;
            _next = Time.time + 1f;
            Vector3 c; Quaternion rot;
            if (Of == null || !BlueprintFrame.Get(Of.Id, out c, out rot)) { Set(0); return; }
            bool ruined = LocState.Ruined(Of, State);
            if (_layerVersion != LocParts.Version || _layer.Count > 50000) { _layerVersion = LocParts.Version; _layer.Clear(); }
            foreach (Piece pc in Blueprint.Around(c, LocParts.AuthorReach))
            {
                ZNetView nv = pc.GetComponent<ZNetView>();
                if (nv.GetZDO().GetString(LocParts.KOf, "").Length > 0) continue; // (a placed copy: the world's state)
                int id = nv.GetInstanceID();
                string layer;
                if (!_layer.TryGetValue(id, out layer))
                {
                    string of; Vector3 at;
                    _layer[id] = layer = LocParts.Place(nv, out of, out at) && of == Of.Id ? LocParts.LayerAt(of, Utils.GetPrefabName(pc.gameObject), at) : NotHere;
                }
                if (ReferenceEquals(layer, NotHere)) continue; // (another place's, its frame nearer: not this look)
                bool on = LocState.Shows(layer, State, Sub);
                LocState.Show(_hidden, nv, on);
                LocState.Lights(_dimmed, Unlit, nv, on, ruined, Of, State, Sub);
                if (on && LocState.Tint(Of, nv)) _tinted.Add(nv);
                WearNTear w = on ? pc.GetComponent<WearNTear>() : null;
                if (w == null) continue;
                _drawn.Add(w);
                LocState.Draw(w, LocState.Broken(Of, nv, State, Sub) ? LocState.Ruin : 1f); // (a «worn:» step's patches too: LocState.Broken)
            }
        }

        internal static void StandMade(ZNetView nv) { if (_hidden.ContainsKey(nv.GetInstanceID())) LocState.Show(_hidden, nv, false); }

        private static void Off()
        {
            LocState.ShowAll(_hidden);
            LocState.UndimAll(_dimmed);
            Unlit.Clear();
            foreach (ZNetView nv in _tinted) LocState.Untint(nv);
            _tinted.Clear();
            foreach (WearNTear w in _drawn)
            {
                if (w == null) continue;
                ZNetView nv = w.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) LocState.Draw(w, nv.GetZDO().GetFloat(ZDOVars.s_health, w.m_health) / w.m_health);
            }
            _drawn.Clear();
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HuginWhispers
{
    // The author windows' one way to lay out what grows: the test window (DevPanel), the story window (StoryPanel, F9), the places window
    // (LocPanel, F10) and every tab in them. The user 2026-11-17 («не влазять всі елементи … шоб кожну табу не треба було правити»): each
    // row of buttons was its own BeginHorizontal with a loop, or «three a row» by count, and a row that got one more place, state or tab
    // ran past the window's edge — the «+ Нова локація» button fell off the places window so.
    //
    // AuthorRow — a row of buttons that never runs past its column:
    //     AuthorRow row = AuthorRow.Begin();            // (fill: the buttons of a row share its width; h: their height)
    //     if (row.Button("…", style)) …;                // as many as there are; row.Label(text, style, width) for a word among them
    //     row.End();                                    // row.Extra: how much taller it came than one row (a window head that grew); row.Tall: all its rows
    // A button is as wide as its words (measured once a text and style); the row breaks before the one that would not fit; a button wider
    // than the whole column takes it whole and wraps its words. The column's width is measured where the row stands (the width its own
    // vertical got when drawn last), so no caller says a width: a fixed column, a stretching one, a scroll view all fit themselves. The width
    // measured on a repaint is taken on the next layout event only, so a frame's passes always lay out the same rows (IMGUI needs that).
    // Until a row has been drawn once it takes the window's body (Wide).
    //
    // AuthorFrame — a window's frame: its size as the author dragged its lower right corner (kept: PlayerPrefs), on the screen, the × in the
    // title that closes it whatever a tab draws, the corner and the title to drag by.
    internal struct AuthorRow
    {
        internal static float Wide = 600f; // the body of the window drawn now (set by its frame): a row's width before it is measured

        private static readonly Dictionary<int, float> Seen = new Dictionary<int, float>(), Use = new Dictionary<int, float>();
        private static readonly Dictionary<GUIStyle, Dictionary<string, float>> Widths = new Dictionary<GUIStyle, Dictionary<string, float>>();
        private static readonly Dictionary<GUIStyle, GUIStyle> Wraps = new Dictionary<GUIStyle, GUIStyle>();
        private static readonly GUIContent Temp = new GUIContent();
        private static int _measured;
        private const float Narrowest = 80f, MeasureMax = 4000;

        private float _w, _h, _used, _rowH, _firstH, _tall;
        private bool _open, _fill;
        private int _rows;

        internal int Rows { get { return _rows; } }
        // how much taller the rows came than the first alone (a window's head that grew by them)
        internal float Extra { get { return _tall - _firstH; } }
        // how tall all its rows came (after End: what stands above a list that takes the rest — StoryPanel.Fill)
        internal float Tall { get { return _tall; } }

        // a row at its place in the code (the file and line are its key: the same line in a loop is the same column)
        internal static AuthorRow Begin(bool fill = true, float h = 0f, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            int key = file.GetHashCode() * 397 + line;
            Event e = Event.current;
            float seen;
            if (e.type == EventType.Layout && Seen.TryGetValue(key, out seen)) Use[key] = seen;
            GUILayout.BeginVertical();
            Rect at = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true)); // (nothing drawn: only where the column is)
            if (e.type == EventType.Repaint && at.width > 1f) Seen[key] = at.width;
            float w;
            if (!Use.TryGetValue(key, out w)) w = Wide;
            return new AuthorRow { _w = Mathf.Max(Narrowest, w), _fill = fill, _h = h };
        }

        internal bool Button(string text, GUIStyle st = null, float minW = 0f, float h = 0f)
        {
            Temp.text = text ?? ""; Temp.tooltip = "";
            return Button(Temp, st, minW, h);
        }

        internal bool Button(GUIContent c, GUIStyle st = null, float minW = 0f, float h = 0f)
        {
            if (st == null) st = GUI.skin.button;
            float gap = Mathf.Max(st.margin.left, st.margin.right);
            float w = Mathf.Max(minW, WidthOf(c, st));
            if (h <= 0f) h = _h;
            if (w + gap > _w) // (wider than the column: the whole of it, the words wrapped)
            {
                w = _w - gap;
                st = Wrap(st);
                h = Mathf.Max(h, st.CalcHeight(c, w));
            }
            Room(w, gap, (h > 0f ? h : st.CalcHeight(c, w)) + Mathf.Max(st.margin.top, st.margin.bottom));
            if (_fill) return h > 0f ? GUILayout.Button(c, st, GUILayout.MinWidth(w), GUILayout.ExpandWidth(true), GUILayout.Height(h)) : GUILayout.Button(c, st, GUILayout.MinWidth(w), GUILayout.ExpandWidth(true));
            return h > 0f ? GUILayout.Button(c, st, GUILayout.Width(w), GUILayout.Height(h)) : GUILayout.Button(c, st, GUILayout.Width(w));
        }

        // a word among the buttons (it keeps its width; a row that breaks before it starts with it)
        internal void Label(string text, GUIStyle st, float w)
        {
            Room(w, 4f, st.CalcHeight(new GUIContent(text), w));
            GUILayout.Label(text, st, GUILayout.Width(w));
        }

        internal void End()
        {
            if (_open) Close();
            GUILayout.EndVertical();
        }

        private void Room(float w, float gap, float tall)
        {
            if (_open && _used + w + gap > _w) Close();
            if (!_open) { GUILayout.BeginHorizontal(); _open = true; _used = 0f; _rowH = 0f; _rows++; }
            _used += w + gap;
            _rowH = Mathf.Max(_rowH, tall);
        }

        private void Close()
        {
            GUILayout.EndHorizontal();
            _open = false;
            if (_rows == 1) _firstH = _rowH;
            _tall += _rowH;
        }

        // a text's width in a style, measured once (texts with numbers in them come and go: the store is emptied past MeasureMax)
        private static float WidthOf(GUIContent c, GUIStyle st)
        {
            Dictionary<string, float> of;
            if (!Widths.TryGetValue(st, out of)) Widths[st] = of = new Dictionary<string, float>();
            string t = c.text ?? "";
            float w;
            if (of.TryGetValue(t, out w)) return w;
            if (++_measured > MeasureMax) { foreach (Dictionary<string, float> d in Widths.Values) d.Clear(); _measured = 1; }
            of[t] = w = Mathf.Ceil(st.CalcSize(c).x) + 2f;
            return w;
        }

        private static GUIStyle Wrap(GUIStyle st)
        {
            if (st.wordWrap) return st;
            GUIStyle w;
            if (!Wraps.TryGetValue(st, out w)) Wraps[st] = w = new GUIStyle(st) { wordWrap = true };
            return w;
        }
    }

    internal class AuthorFrame
    {
        internal const float Grip = 18f;
        private const float Pad = 24f, Bar = 22f; // (the windows' padding left and right; a scroll bar)
        private readonly string _key;
        private readonly float _firstW, _minW, _minH;
        private Vector2 _size;
        private bool _resizing;

        internal AuthorFrame(string key, float firstW, float minW, float minH) { _key = key; _firstW = firstW; _minW = minW; _minH = minH; }

        // in OnGUI, before GUI.Window: the corner being dragged (screen coordinates: the pointer may leave the window as it goes; the size
        // kept once let go), then the window as the author made it — at least `wide` (a tab that needs its width) — on the screen
        internal Rect Fit(Rect r, float wide)
        {
            if (_size == Vector2.zero) _size = new Vector2(PlayerPrefs.GetFloat(_key + "w", _firstW), PlayerPrefs.GetFloat(_key + "h", Screen.height - 100f)); // (first: as tall as the screen allows)
            Event e = Event.current;
            if (_resizing && e != null)
            {
                if (e.type == EventType.MouseDrag) { _size = new Vector2(Mathf.Max(_minW, e.mousePosition.x - r.x), Mathf.Max(_minH, e.mousePosition.y - r.y)); e.Use(); }
                else if (e.type == EventType.MouseUp || e.rawType == EventType.MouseUp) { _resizing = false; PlayerPrefs.SetFloat(_key + "w", _size.x); PlayerPrefs.SetFloat(_key + "h", _size.y); }
            }
            r.width = Mathf.Min(Mathf.Max(Mathf.Max(_size.x, wide), _minW), Screen.width - 10f);
            r.height = Mathf.Min(Mathf.Max(_size.y, _minH), Screen.height - 20f);
            r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, Screen.width - r.width));
            r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, Screen.height - 60f));
            AuthorRow.Wide = Body(r);
            return r;
        }

        // the width a whole row gets in a window's scrolling body
        internal static float Body(Rect r) { return r.width - Pad - Bar; }

        // in the window, first: the × in its title, always there (true: close)
        internal static bool Cross(Rect r) { return GUI.Button(new Rect(r.width - 34f, 4f, 26f, 20f), "×"); }

        // in the window, last: the corner to drag (a footer leaves Grip free at its right), and the title to move the window by
        internal void Corner(Rect r, GUIStyle st)
        {
            Rect grip = new Rect(r.width - Grip - 2f, r.height - Grip - 2f, Grip, Grip);
            GUI.Label(grip, "◢", st);
            if (Event.current.type == EventType.MouseDown && grip.Contains(Event.current.mousePosition)) { _resizing = true; Event.current.Use(); }
            GUI.DragWindow(new Rect(0f, 0f, r.width - 40f, 26f));
        }
    }
}
#endif

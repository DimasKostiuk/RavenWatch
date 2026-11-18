#if RAVEN_ITEM && RAVEN_NPC
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // People's words over their heads (the user 2026-11-16: «бесіди між нпс гарніше, приблизно у стилі розмови наших воронів; щоб
    // бульбашки не налазили і відрізнялись»; his choice: one capsule per talk, each person's own colour, the name before the words, for all
    // the people's words over heads). Instead of the game's small bubble over each head, which lay over each other where people sit
    // close: a capsule in the look of the ravens' talk (TalkBubble) — the lines one under another, each in a band of its speaker's own
    // colour (ColorOf) with the name before the words, the older ones smaller and dimmer, the last MaxRows kept; while the next one of a
    // talk thinks (NpcChatter → NpcActor.SayNext), a thin row with its name and three pulsing dots.
    // Where a line goes: into the capsule its speaker is already in (said a line there, or is named the next), else into one with a
    // speaker within Join m of it (the same ring of voices: NpcActor.FloorRadius), else a capsule of its own over its head. The capsule
    // hangs over the middle of those whose lines it shows and floats after them; capsules that would lie on each other on the screen are
    // pushed up apart (Spread, once a frame for all). Seen within the farthest reach of its lines (NpcActor.RPC_Say: 25 m, a weighty
    // line WeightyFar times, a module's HeardFrom), melting away towards it, only in view; hidden with the inventory, map and menus.
    // Every game draws it from the same words (RPC_Say, RPC_Next); nothing is kept or sent from here.
    internal sealed class NpcBubble : MonoBehaviour
    {
        internal static float Join = 8f;             // m: a line of one this near a capsule's speaker goes into it
        internal static float Head = 2.2f;           // m over the highest of them
        internal static float Size = 0.9f;           // the capsule's scale (the ravens' own talk is 1)
        internal static float ThinkMost = 5f;        // s the next one's dots wait for its line at most (a talk broken off)
        internal static int MaxRows = 4;             // the lines kept in a capsule; the oldest leaves
        private const float Width = 430f, MinWidth = 170f; // px at 1080p
        private const float PadL = 16f, PadR = 18f, PadV = 8f, FontSize = 17f, TypingFont = 14f;
        private const float OldScale = 0.86f, OlderScale = 0.8f;
        // the people's colours: bright on the dark capsule, none of them the ravens' blue and red, nor the weighty line's amber (NpcActor.WeightyColor)
        internal static readonly string[] Palette = { "#7FD6A8", "#C9A2FF", "#FF8FB1", "#5FD3C8", "#B5E06A", "#D7B98E", "#A7B6FF", "#E8D27A" };
        private static readonly List<NpcBubble> Live = new List<NpcBubble>(); // session: capsules on the screen now

        // a person's own colour: its template's «color:», else one of Palette by its seed (the same on every game)
        internal static string ColorOf(NpcActor a)
        {
            if (a == null) return Palette[0];
            if (a.Def != null && a.Def.Hue.Length > 0) return a.Def.Hue;
            uint seed = a.Zdo != null ? NpcKit.Seed(a.Zdo) : (uint)(a.Id ?? "").GetStableHashCode();
            return Palette[(int)(seed % (uint)Palette.Length)];
        }

        private static Color Rgb(string hex) { Color c; return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white; }

        // ---------------------------------------------------------------- asked from outside (NpcActor)

        // a line over a head (RPC_Say, every game): seconds it is up, metres it is seen from, weighty — its words in WeightyColor
        internal static void Say(NpcActor who, string text, float seconds, float seen, bool weighty)
        {
            if (who == null || string.IsNullOrEmpty(text) || RavenSpeech.Host == null) return;
            NpcBubble b = Find(who);
            if (b == null)
            {
                GameObject go = new GameObject("RavenWatch_NpcTalk", typeof(RectTransform)); // (one per ring of voices; the line's rows are its children)
                go.transform.SetParent(RavenSpeech.Host.transform, false);
                b = go.AddComponent<NpcBubble>();
                b.Build();
                Live.Add(b);
            }
            b.Line(who, text, seconds, seen, weighty);
        }

        // the next line of a talk is next's, after so many seconds (NpcChatter: who has just spoken knows): its dots show then, in the
        // capsule who's line is in, until its line comes (at most ThinkMost s)
        internal static void Next(NpcActor who, NpcActor next, float after)
        {
            if (who == null || next == null) return;
            foreach (NpcBubble b in Live)
                if (b != null && !b._ending && b.Speaks(who)) { b._nextWho = next; b._nextAt = Time.time + Mathf.Max(0f, after); return; }
        }

        // the capsule it is in already, else one whose speaker is near it
        private static NpcBubble Find(NpcActor who)
        {
            foreach (NpcBubble b in Live) if (b != null && !b._ending && (b.Speaks(who) || b._nextWho == who || (b._typing != null && b._typing.Who == who))) return b;
            float j2 = Join * Join;
            Vector3 at = who.transform.position;
            foreach (NpcBubble b in Live)
            {
                if (b == null || b._ending) continue;
                foreach (Row r in b._rows) if (!r.Leaving && r.Who != null && (r.Who.transform.position - at).sqrMagnitude < j2) return b;
            }
            return null;
        }

        // ---------------------------------------------------------------- building

        private sealed class Row
        {
            // Rt: the row's band, the capsule's full width; Content: the words, the only part that shrinks and dims as the row grows old
            public RectTransform Rt, Content; public CanvasGroup Cg; public Image Glow; public BandFill Band;
            public float H, Need, Scale = 1f, Alpha, GlowA, Keep = 1f, Until, Seen;
            public bool Leaving, Typing; public NpcActor Who; public Image Dot0, Dot1, Dot2;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<NpcActor, string> _tones = new Dictionary<NpcActor, string>(); // the colour each speaker has in this capsule
        private Row _typing;
        private NpcActor _nextWho; private float _nextAt;
        private RectTransform _rt, _glows, _rowsRt;
        private CanvasGroup _cg;
        private bool _ending, _placed;
        private float _height = 30f, _width = MinWidth;
        private Vector2 _want, _pos, _goal;          // the capsule's bottom middle on the screen: where it would stand, after Spread, where it is
        private bool _shown;                          // in view and readable this frame (takes part in Spread)

        private bool Speaks(NpcActor who) { foreach (Row r in _rows) if (r.Who == who && !r.Leaving) return true; return false; }

        // its colour here: its own, unless a speaker of this capsule has it already — then the next one free (two in one talk never alike)
        private string Tone(NpcActor who)
        {
            string c;
            if (_tones.TryGetValue(who, out c)) return c;
            c = ColorOf(who);
            if (_tones.ContainsValue(c))
            {
                int at = System.Array.IndexOf(Palette, c);
                for (int i = 1; i <= Palette.Length; i++) { string p = Palette[(Mathf.Max(0, at) + i) % Palette.Length]; if (!_tones.ContainsValue(p)) { c = p; break; } }
            }
            _tones[who] = c;
            return c;
        }

        private void Build()
        {
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = Vector2.zero;
            _rt.pivot = new Vector2(0.5f, 0f); // stands on its bottom middle, over the heads
            _rt.sizeDelta = new Vector2(_width, _height);
            _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.blocksRaycasts = false; _cg.interactable = false; _cg.alpha = 0f;

            RectTransform shadow = Child(transform, "shadow");
            Stretch(shadow, -14f, -16f, 14f, 8f);
            Image sh = shadow.gameObject.AddComponent<Image>(); sh.sprite = RavenSpeech.Glow(); sh.color = new Color(0f, 0f, 0f, 0.5f); sh.raycastTarget = false;
            RectTransform body = Child(transform, "body");
            Stretch(body, 0f, 0f, 0f, 0f);
            Image bi = body.gameObject.AddComponent<Image>();
            bi.sprite = RavenSpeech.Fill(); bi.type = Image.Type.Sliced; bi.color = new Color(0.035f, 0.045f, 0.06f, 0.9f); bi.raycastTarget = false;
            _glows = Child(body, "glows"); Stretch(_glows, 0f, 0f, 0f, 0f); // the speakers' colours, under the words (BandFill)
            _rowsRt = Child(body, "rows"); Stretch(_rowsRt, 0f, 0f, 0f, 0f);
            RectTransform frame = Child(transform, "frame");
            Stretch(frame, 0f, 0f, 0f, 0f);
            Image fr = frame.gameObject.AddComponent<Image>(); fr.sprite = RavenSpeech.Ring(); fr.type = Image.Type.Sliced; fr.color = RavenSpeech.Edge; fr.raycastTarget = false;
        }

        private static RectTransform Child(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform r, float l, float b, float rr, float t)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(rr, t);
        }

        // the band, its glow of the speaker's colour from the left end (the capsule's own shape cut to the band: BandFill), and the part
        // that holds the words (scaled from its left middle)
        private Row Band(string tone, float glow)
        {
            Row r = new Row { GlowA = glow };
            r.Rt = Child(_rowsRt, "row");
            r.Rt.anchorMin = r.Rt.anchorMax = new Vector2(0f, 1f); r.Rt.pivot = new Vector2(0f, 1f);
            Color c = Rgb(tone);
            RectTransform g = Child(_glows, "glow");
            Stretch(g, 0f, 0f, 0f, 0f);
            r.Glow = g.gameObject.AddComponent<Image>(); r.Glow.sprite = RavenSpeech.Fill(); r.Glow.type = Image.Type.Sliced; r.Glow.color = new Color(c.r, c.g, c.b, glow); r.Glow.raycastTarget = false;
            r.Band = g.gameObject.AddComponent<BandFill>(); // nothing shown until the line has its place
            r.Content = Child(r.Rt, "content");
            r.Content.anchorMin = r.Content.anchorMax = new Vector2(0f, 0.5f); r.Content.pivot = new Vector2(0f, 0.5f);
            r.Cg = r.Content.gameObject.AddComponent<CanvasGroup>(); r.Cg.alpha = 0f;
            return r;
        }

        private static void Drop(Row r) { if (r == null) return; if (r.Glow != null) Destroy(r.Glow.gameObject); if (r.Rt != null) Destroy(r.Rt.gameObject); }

        private static TextMeshProUGUI Text(Transform parent, float size)
        {
            RectTransform tr = Child(parent, "text");
            TextMeshProUGUI t = SpeechCard.NewText(tr.gameObject, RavenSpeech.Font());
            t.fontSize = size; t.richText = true; t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; t.outlineWidth = 0f;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = new Color(0.90f, 0.91f, 0.93f, 1f);
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f); tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(PadL, 0f);
            return t;
        }

        private void Line(NpcActor who, string text, float seconds, float seen, bool weighty)
        {
            if (_typing != null && _typing.Who == who) { Drop(_typing); _typing = null; }
            if (_nextWho == who) _nextWho = null;
            string tone = Tone(who);
            Row r = Band(tone, 0.34f);
            r.Who = who; r.Until = Time.time + seconds; r.Seen = seen;
            TextMeshProUGUI body = Text(r.Content, FontSize);
            string full = "<b><color=" + tone + ">" + who.ShownName + ":</color></b> " + (weighty ? "<color=" + NpcActor.WeightyColor + ">" + text + "</color>" : text);
            body.text = full;
            float most = Width - PadL - PadR;
            float tw = Mathf.Min(most, body.GetPreferredValues(full, most, 0f).x + 2f);
            float th = body.GetPreferredValues(full, tw, 0f).y;
            body.rectTransform.sizeDelta = new Vector2(tw, th);
            r.H = Mathf.Max(34f, th + 2f * PadV);
            r.Need = PadL + tw + PadR;
            r.Content.sizeDelta = new Vector2(r.Need, r.H);
            _rows.Add(r);
            int kept = 0; // the oldest leave (squeezed out, not cut)
            for (int i = _rows.Count - 1; i >= 0; i--) if (!_rows[i].Leaving && ++kept > MaxRows) _rows[i].Leaving = true;
        }

        // the thin bottom row: the next one's name and three pulsing dots
        private Row AddTyping(NpcActor who)
        {
            string tone = Tone(who);
            Row r = Band(tone, 0.22f);
            r.Typing = true; r.Who = who; r.Until = Time.time + ThinkMost;
            r.H = _rows.Count == 0 ? 32f : 26f;
            TextMeshProUGUI name = Text(r.Content, TypingFont);
            string head = "<color=" + tone + ">" + who.ShownName + "</color>";
            name.text = head; name.alpha = 0.85f;
            float nw = name.GetPreferredValues(head, Width, 0f).x + 2f;
            name.rectTransform.sizeDelta = new Vector2(nw, r.H);
            Color c = Rgb(tone);
            Image[] dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform d = Child(r.Content, "dot" + i);
                d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f); d.pivot = new Vector2(0.5f, 0.5f);
                d.anchoredPosition = new Vector2(PadL + nw + 9f + i * 11f, 0f); d.sizeDelta = new Vector2(6f, 6f);
                dots[i] = d.gameObject.AddComponent<Image>(); dots[i].sprite = BookArt.Circle; dots[i].color = c; dots[i].raycastTarget = false;
            }
            r.Dot0 = dots[0]; r.Dot1 = dots[1]; r.Dot2 = dots[2];
            r.Need = PadL + nw + 9f + 2f * 11f + 6f + PadR;
            r.Content.sizeDelta = new Vector2(r.Need, r.H);
            return r;
        }

        // ---------------------------------------------------------------- its life

        // one step a frame for all the capsules, from whichever is updated first: each its rows and where it would stand, then Spread
        // over them all, then each moves there (no LateUpdate of its own: the arch rule keeps those for a person's bones)
        private static int _tickFrame = -1;
        private static readonly List<NpcBubble> Ticking = new List<NpcBubble>();
        private void Update()
        {
            if (_tickFrame == Time.frameCount) return;
            _tickFrame = Time.frameCount;
            Ticking.Clear(); Ticking.AddRange(Live);
            foreach (NpcBubble b in Ticking) if (b != null) b.Step();
            Spread();
            foreach (NpcBubble b in Live) if (b != null) b.Move();
        }

        private void Step()
        {
            float now = Time.time;
            if (!_ending)
            {
                if (_nextWho != null && _typing == null && now >= _nextAt) { _typing = AddTyping(_nextWho); _nextWho = null; }
                if (_typing != null && (now >= _typing.Until || _typing.Who == null)) { Drop(_typing); _typing = null; } // (the talk broke off: its line never comes)
                bool up = _typing != null || _nextWho != null;
                foreach (Row r in _rows) if (!r.Leaving && r.Until > now && r.Who != null) { up = true; break; }
                if (!up) _ending = true;
            }

            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            // the older lines shrink and dim, the newest is full; the oldest past MaxRows squeeze out; the thinking row pulses
            int said = 0;
            foreach (Row r in _rows) if (!r.Leaving) said++;
            float y = 0f, wide = MinWidth;
            int back = said;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (!r.Leaving) back--;
                float wantScale = r.Leaving ? r.Scale : back == 0 ? 1f : back == 1 ? OldScale : OlderScale;
                float wantAlpha = r.Leaving ? 0f : back == 0 ? 1f : back == 1 ? 0.62f : 0.45f;
                float wantGlow = r.Leaving ? 0f : back == 0 ? 0.34f : 0.24f; // the colour stays, a little quieter
                r.Scale = Mathf.Lerp(r.Scale, wantScale, k);
                r.Alpha = Mathf.Lerp(r.Alpha, wantAlpha, k);
                r.GlowA = Mathf.Lerp(r.GlowA, wantGlow, k);
                if (r.Leaving) r.Keep = Mathf.Lerp(r.Keep, 0f, k);
                if (r.Leaving && r.Keep < 0.02f) { Drop(r); _rows.RemoveAt(i); i--; continue; }
                if (!r.Leaving) wide = Mathf.Max(wide, r.Need * r.Scale);
                float band = r.H * r.Scale * r.Keep;
                r.Rt.anchoredPosition = new Vector2(0f, y);
                r.Content.localScale = new Vector3(r.Scale, r.Scale, 1f);
                r.Cg.alpha = r.Alpha;
                Color gc = r.Glow.color; gc.a = r.GlowA; r.Glow.color = gc;
                r.Band.Set(-y, -y + band, Mathf.Min(_width * 0.6f, 260f));
                r.Rt.sizeDelta = new Vector2(_width, band);
                y -= band; // the next band starts right where this one ends: no gaps
            }
            if (_typing != null)
            {
                _typing.Alpha = Mathf.Lerp(_typing.Alpha, 1f, k);
                _typing.Cg.alpha = _typing.Alpha;
                _typing.Rt.anchoredPosition = new Vector2(0f, y);
                _typing.Rt.sizeDelta = new Vector2(_width, _typing.H);
                _typing.Band.Set(-y, -y + _typing.H, Mathf.Min(_width * 0.6f, 260f));
                float t = Time.unscaledTime * 6f;
                Pulse(_typing.Dot0, t); Pulse(_typing.Dot1, t - 0.9f); Pulse(_typing.Dot2, t - 1.8f);
                wide = Mathf.Max(wide, _typing.Need);
                y -= _typing.H;
            }
            _height = Mathf.Lerp(_height, Mathf.Max(20f, -y), k);
            _width = Mathf.Lerp(_width, Mathf.Min(Width, wide), k);
            _rt.sizeDelta = new Vector2(_width, _height);
            Aim(k);
        }

        private static void Pulse(Image dot, float t)
        {
            if (dot == null) return;
            Color c = dot.color;
            c.a = 0.35f + 0.65f * Mathf.Clamp01(Mathf.Sin(t) * 0.5f + 0.5f);
            dot.color = c;
        }

        // where it would stand this frame (over the middle of its speakers), and how clear it is; Move puts it there after Spread has set it apart from the others
        private void Aim(float k)
        {
            Camera cam = Utils.GetMainCamera();
            float want = _ending ? 0f : 1f;
            _shown = false;
            bool hidden = InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible() || Game.IsPaused();
            Vector3 mid = Vector3.zero; float top = float.MinValue, seen = 0f; int n = 0;
            foreach (Row r in _rows) Add(r, ref mid, ref top, ref seen, ref n);
            Add(_typing, ref mid, ref top, ref seen, ref n);
            if (n == 0 && !_ending) { _ending = true; want = 0f; } // (all of them gone: the words go with them)
            if (cam == null || hidden || n == 0) want = 0f;
            else
            {
                Vector3 at = mid / n; at.y = top + Head;
                Vector3 sp = cam.WorldToScreenPoint(at);
                bool inView = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
                Player me = Player.m_localPlayer;
                float dist = Vector3.Distance(me != null ? me.transform.position : cam.transform.position, mid / n);
                if (!inView || dist > seen) want = 0f;
                else
                {
                    want *= 1f - Mathf.InverseLerp(seen * 0.65f, seen, dist);
                    _want = new Vector2(sp.x, sp.y);
                    _shown = _cg.alpha > 0.02f || want > 0f;
                }
            }
            _cg.alpha = Mathf.Lerp(_cg.alpha, want, _ending ? 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime) : k);
            if (_ending && _cg.alpha < 0.02f) { Live.Remove(this); _shown = false; Destroy(gameObject); }
        }

        private static void Add(Row r, ref Vector3 mid, ref float top, ref float seen, ref int n)
        {
            if (r == null || r.Leaving || r.Who == null) return;
            Vector3 p = r.Who.transform.position;
            mid += p; top = Mathf.Max(top, p.y); n++;
            seen = Mathf.Max(seen, r.Typing ? 0f : r.Seen);
        }

        private void Move()
        {
            if (!_shown) return;
            float s = RavenSpeech.Host.Scale * Size;
            Vector2 target = _goalNow;
            // first shown, or back from nothing: straight there; small wobbles let be; whole pixels: no shiver
            if (!_placed || _cg.alpha < 0.05f) { _pos = target; _goal = target; _placed = true; }
            else
            {
                if ((target - _goal).sqrMagnitude > 36f * s * s) _goal = target;
                _pos = Vector2.Lerp(_pos, _goal, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            }
            transform.localScale = new Vector3(Size, Size, 1f);
            _rt.position = new Vector3(Mathf.Round(_pos.x), Mathf.Round(_pos.y), 0f);
        }

        // capsules that would lie on each other go up above the one under them (the lowest keep their places, so the nearest talk stays
        // where it is); kept on the screen
        private Vector2 _goalNow;
        private static readonly List<NpcBubble> Over = new List<NpcBubble>();
        private static void Spread()
        {
            Over.Clear();
            float s = RavenSpeech.Host.Scale * Size;
            foreach (NpcBubble b in Live)
            {
                if (b == null || !b._shown) continue;
                float half = b._width * 0.5f * s;
                b._goalNow = new Vector2(Mathf.Clamp(b._want.x, half + 4f * s, Screen.width - half - 4f * s), Mathf.Clamp(b._want.y, 4f * s, Screen.height - b._height * s - 4f * s));
                Over.Add(b);
            }
            if (Over.Count < 2) return;
            Over.Sort((a, b) => a._goalNow.y.CompareTo(b._goalNow.y));
            float gap = 6f * s;
            for (int i = 1; i < Over.Count; i++)
            {
                NpcBubble c = Over[i];
                for (int pass = 0; pass < Over.Count; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        NpcBubble o = Over[j];
                        float cw = c._width * 0.5f * s, ow = o._width * 0.5f * s;
                        bool side = Mathf.Abs(c._goalNow.x - o._goalNow.x) < cw + ow;
                        float oTop = o._goalNow.y + o._height * s;
                        bool up = c._goalNow.y < oTop + gap && o._goalNow.y < c._goalNow.y + c._height * s;
                        if (!side || !up) continue;
                        c._goalNow = new Vector2(c._goalNow.x, oTop + gap);
                        moved = true;
                    }
                    if (!moved) break;
                }
            }
        }

        private void OnDestroy() { Live.Remove(this); }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The bubble a talk between the ravens runs in, over the player's head: one capsule, the lines one under another,
    // each with its raven's round picture and a glow of its colour. While the next one is "thinking", a thin row at the
    // bottom shows its small picture and three pulsing dots. When a new line comes, the older ones shrink a little and
    // dim (by their scale: the text is not laid out again), so the whole talk stays in sight and readable without the
    // bubble growing much. Others' bubbles: only within 30 m, a little smaller, melting away towards the edge.
    internal sealed class TalkBubble : MonoBehaviour
    {
        internal struct Line { public bool Munin; public string Text; }

        private const float Width = 450f;           // px at 1080p
        private const float Pic = 34f, PicSmall = 22f, PadL = 11f, Gap = 11f, PadR = 18f, PadV = 9f;
        private const float OldScale = 0.86f, OlderScale = 0.8f;
        private const float GlowWidth = Width * 0.6f; // the colour fades out over this much from the left
        private static readonly Dictionary<Player, TalkBubble> Shown = new Dictionary<Player, TalkBubble>();

        // how long a line is the newest before the next one starts "thinking"
        internal static float Pace(string text) { return Mathf.Clamp(RavenSpeech.ReadSeconds(text) * 0.65f, 3f, 10f); }
        // after the last line the talk closes: that one is kept long enough to be read through, not just a pace
        internal static float After(string text, bool last, float mul) { float pace = Pace(text) * mul; return last ? Mathf.Max(pace + 2f, RavenSpeech.ReadSeconds(text) * mul) : pace; }
        private static float Think(string text) { return Mathf.Clamp(0.6f + (text != null ? text.Length : 0) / 90f, 0.8f, 1.8f); }

        internal static bool Playing(Player p) { TalkBubble b; return p != null && Shown.TryGetValue(p, out b) && b != null && !b._ending; }

        // ---- a talk of the two ravens on a perch: over the perch, not over a player
        private static readonly Dictionary<PerchView, TalkBubble> ShownPerch = new Dictionary<PerchView, TalkBubble>();
        private PerchView _perch;

        internal static bool PlayingAt(PerchView v) { TalkBubble b; return v != null && ShownPerch.TryGetValue(v, out b) && b != null && !b._ending; }

        internal static void PlayAt(PerchView v, List<Line> lines, bool mine)
        {
            if (v == null || lines == null || lines.Count == 0) return;
            TalkBubble old;
            if (ShownPerch.TryGetValue(v, out old) && old != null) Destroy(old.gameObject);
            GameObject go = new GameObject("RavenWatch_PerchTalk", typeof(RectTransform));
            go.transform.SetParent(RavenSpeech.Host.transform, false);
            TalkBubble b = go.AddComponent<TalkBubble>();
            b._perch = v;
            b.Build(null, lines, mine);
            ShownPerch[v] = b;
        }

        private Vector3 Anchor { get { return _perch != null ? _perch.TalkPoint() : _owner.transform.position + Vector3.up * 2.55f; } }
        private Vector3 Where { get { return _perch != null ? _perch.transform.position : _owner.transform.position; } }

        internal static void Play(Player owner, List<Line> lines, bool local)
        {
            if (owner == null || lines == null || lines.Count == 0) return;
            TalkBubble old;
            if (Shown.TryGetValue(owner, out old) && old != null) Destroy(old.gameObject);
            GameObject go = new GameObject("RavenWatch_Talk", typeof(RectTransform));
            go.transform.SetParent(RavenSpeech.Host.transform, false);
            TalkBubble b = go.AddComponent<TalkBubble>();
            b.Build(owner, lines, local);
            Shown[owner] = b;
        }

        // ---------------------------------------------------------------- building

        private sealed class Row
        {
            // Rt: the row's band, full width, nothing between the bands (its colour fills it top to bottom);
            // Content: the picture and the words, the only part that shrinks and dims as the row grows old
            public RectTransform Rt, Content; public CanvasGroup Cg; public Image Glow; public BandFill Band; public float H; public float Scale = 1f, Alpha, GlowA;
            public bool Munin; public bool Typing; public Image Dot0, Dot1, Dot2; public TextMeshProUGUI Body;
        }

        private Player _owner;
        private bool _local, _ending;
        private List<Line> _lines;
        private readonly List<Row> _rows = new List<Row>();
        private Row _typing;
        private RectTransform _rt, _body, _glows, _rowsRt, _frameRt;
        private CanvasGroup _cg;
        private int _next;            // the line to come
        private float _at;            // when the next step happens
        private bool _thinking;
        private float _height = 40f;
        private float _lookAt;        // when to look again whether both are still on the shoulder
        private int[] _voice;         // each line's sound, asked all at once when the talk starts (RavenVoice: the next is made while one plays)
        private AudioClip _clip;      // the sound of the line about to be said

        private void Build(Player owner, List<Line> lines, bool local)
        {
            _owner = owner; _local = local; _lines = lines;
            _voice = new int[lines.Count];
            for (int i = 0; i < lines.Count; i++) _voice[i] = RavenVoice.Ahead(lines[i].Munin, RavenVoice.VoiceOf(owner, lines[i].Munin, null), lines[i].Text);

            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = Vector2.zero;
            _rt.pivot = new Vector2(0.5f, 0f); // stands on its bottom middle, over the head
            _rt.sizeDelta = new Vector2(Width, _height);
            _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.blocksRaycasts = false; _cg.interactable = false; _cg.alpha = 0f;

            RectTransform shadow = Child(transform, "shadow");
            Stretch(shadow, -14f, -16f, 14f, 8f);
            Image sh = shadow.gameObject.AddComponent<Image>(); sh.sprite = RavenSpeech.Glow(); sh.color = new Color(0f, 0f, 0f, 0.55f); sh.raycastTarget = false;

            _body = Child(transform, "body");
            Stretch(_body, 0f, 0f, 0f, 0f);
            Image bi = _body.gameObject.AddComponent<Image>();
            bi.sprite = RavenSpeech.Fill(); bi.type = Image.Type.Sliced; bi.color = new Color(0.035f, 0.045f, 0.06f, 0.94f); bi.raycastTarget = false;
            _glows = Child(_body, "glows"); Stretch(_glows, 0f, 0f, 0f, 0f); // the lines' colours, under the words (BandFill: no mask)
            _rowsRt = Child(_body, "rows"); Stretch(_rowsRt, 0f, 0f, 0f, 0f);

            _frameRt = Child(transform, "frame");
            Stretch(_frameRt, 0f, 0f, 0f, 0f);
            Image fr = _frameRt.gameObject.AddComponent<Image>(); fr.sprite = RavenSpeech.Ring(); fr.type = Image.Type.Sliced; fr.color = RavenSpeech.Edge; fr.raycastTarget = false;

            _next = 0;
            StartThinking();
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

        // the raven's round picture with a ring of its colour
        private static void Avatar(Transform parent, bool munin, float size, float x)
        {
            Color c = munin ? RavenSpeech.MuninFrame : RavenSpeech.HuginFrame;
            RectTransform pr = Child(parent, "pic");
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f); pr.pivot = new Vector2(0f, 0.5f);
            pr.anchoredPosition = new Vector2(x, 0f); pr.sizeDelta = new Vector2(size, size);
            Image ring = pr.gameObject.AddComponent<Image>(); ring.sprite = SeneaLShoulders.Disc(); ring.color = c; ring.raycastTarget = false;
            RectTransform cut = Child(pr, "cut");
            Stretch(cut, 1.5f, 1.5f, -1.5f, -1.5f);
            Image ci = cut.gameObject.AddComponent<Image>(); ci.sprite = SeneaLShoulders.Disc(); ci.color = new Color(0.03f, 0.035f, 0.045f, 1f); ci.raycastTarget = false;
            cut.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform face = Child(cut, "face");
            Stretch(face, 2f, 2f, -2f, -2f);
            face.localScale = new Vector3(munin ? -1f : 1f, 1f, 1f);
            Image fi = face.gameObject.AddComponent<Image>(); fi.sprite = AbilityIcons.For(munin ? "munin" : "hugin", munin); fi.preserveAspect = true; fi.raycastTarget = false;
        }

        // the glow of the speaker's colour from the left end: the capsule's own shape, cut to the line's band (BandFill),
        // so it is round only where the band meets the capsule's round corners and never goes past the outline
        private Image Glow(Row r, bool munin, float alpha)
        {
            Color c = munin ? RavenSpeech.MuninFrame : RavenSpeech.HuginFrame;
            RectTransform g = Child(_glows, "glow");
            Stretch(g, 0f, 0f, 0f, 0f);
            Image gi = g.gameObject.AddComponent<Image>(); gi.sprite = RavenSpeech.Fill(); gi.type = Image.Type.Sliced; gi.color = new Color(c.r, c.g, c.b, alpha); gi.raycastTarget = false;
            r.Band = g.gameObject.AddComponent<BandFill>(); // nothing shown until the line has its place
            return gi;
        }

        private static void Drop(Row r) { if (r == null) return; if (r.Glow != null) Destroy(r.Glow.gameObject); if (r.Rt != null) Destroy(r.Rt.gameObject); }

        // the band and, inside it, the part that holds the picture and the words (scaled from its left middle)
        private Row Band(bool munin, string name, float glow)
        {
            Row r = new Row { Munin = munin, GlowA = glow };
            r.Rt = Child(_rowsRt, name);
            r.Rt.anchorMin = r.Rt.anchorMax = new Vector2(0f, 1f); r.Rt.pivot = new Vector2(0f, 1f);
            r.Glow = Glow(r, munin, glow);
            r.Content = Child(r.Rt, "content");
            r.Content.anchorMin = r.Content.anchorMax = new Vector2(0f, 0.5f); r.Content.pivot = new Vector2(0f, 0.5f);
            r.Content.anchoredPosition = Vector2.zero;
            r.Cg = r.Content.gameObject.AddComponent<CanvasGroup>(); r.Cg.alpha = 0f;
            return r;
        }

        private Row AddRow(bool munin, string text)
        {
            Row r = Band(munin, "row", 0.34f);
            float tw = Width - PadL - Pic - Gap - PadR;
            RectTransform tr = Child(r.Content, "text");
            TextMeshProUGUI body = SpeechCard.NewText(tr.gameObject, RavenSpeech.Font(false));
            body.fontSize = 18f; body.richText = true; body.textWrappingMode = TextWrappingModes.Normal; body.raycastTarget = false;
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.color = new Color(0.90f, 0.91f, 0.93f, 1f);
            string full = "<b><color=" + (munin ? RavenSpeech.MuninName : RavenSpeech.HuginName) + ">" + (_local || _owner == null ? Plugin.RavenName(munin) : Nicks.Name(_owner, munin)) + ":</color></b> " + RavenSpeech.MarkNames(text); // another player's two: by their nicknames
            body.text = full; r.Body = body;
            float th = body.GetPreferredValues(full, tw, 0f).y;
            r.H = Mathf.Max(Pic + 2f * PadV, th + 2f * PadV);
            r.Rt.sizeDelta = new Vector2(Width, r.H);
            r.Content.sizeDelta = new Vector2(Width, r.H);
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f); tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(PadL + Pic + Gap, 0f); tr.sizeDelta = new Vector2(tw, th);
            Avatar(r.Content, munin, Pic, PadL);
            _rows.Add(r);
            return r;
        }

        // the thin bottom row: the next raven's small picture and three pulsing dots
        private Row AddTyping(bool munin)
        {
            Row r = Band(munin, "typing", 0.22f);
            r.Typing = true;
            r.H = PicSmall + (_rows.Count == 0 ? 18f : 12f); // alone (the first line coming) a little taller, not a thin strip
            r.Rt.sizeDelta = new Vector2(Width, r.H);
            r.Content.sizeDelta = new Vector2(Width, r.H);
            Avatar(r.Content, munin, PicSmall, PadL + (Pic - PicSmall) * 0.5f);
            Color c = munin ? RavenSpeech.MuninFrame : RavenSpeech.HuginFrame;
            Image[] dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform d = Child(r.Content, "dot" + i);
                d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f); d.pivot = new Vector2(0.5f, 0.5f);
                d.anchoredPosition = new Vector2(PadL + Pic + Gap + 4f + i * 11f, 0f); d.sizeDelta = new Vector2(6f, 6f);
                dots[i] = d.gameObject.AddComponent<Image>(); dots[i].sprite = SeneaLShoulders.Disc(); dots[i].color = c; dots[i].raycastTarget = false;
            }
            r.Dot0 = dots[0]; r.Dot1 = dots[1]; r.Dot2 = dots[2];
            return r;
        }

        // ---------------------------------------------------------------- the talk going on

        private void StartThinking()
        {
            if (_next >= _lines.Count) return;
            Drop(_typing);
            _typing = AddTyping(_lines[_next].Munin);
            _thinking = true;
            _at = Time.time + Think(_lines[_next].Text);
        }

        private void Say()
        {
            Line l = _lines[_next];
            Drop(_typing); _typing = null;
            Row row = AddRow(l.Munin, l.Text);
            ToChat(l);
            _thinking = false;
            _next++;
            _at = Time.time + After(l.Text, _next >= _lines.Count, 1f);
            if (_clip != null) // its voice, from the raven saying it; the next line only after it is over
            {
                GameObject who = _perch != null ? _perch.Bird(l.Munin) : Shoulder.ModelOf(_owner, l.Munin);
                if (who == null && _owner != null) who = _owner.gameObject;
                if (who != null) { WordReveal.Begin(row.Body, _clip.length); RavenVoice.Play(who, _clip, l.Munin); _at = Mathf.Max(_at, Time.time + _clip.length + 0.4f); }
                else Destroy(_clip);
                _clip = null;
            }
            if (_perch != null) { _perch.Speaking(l.Munin, Pace(l.Text)); return; } // on a perch: the beak and the other one's look
            // the raven itself opens its beak (on whoever's shoulder it sits)
            GameObject model = Shoulder.ModelOf(_owner, l.Munin);
            Animator anim = model != null ? model.GetComponentInChildren<Animator>() : null;
            ShoulderLife.TalkOn(anim);
            ShoulderLife life = Shoulder.LifeOf(_owner, l.Munin);
            if (life != null) life.Wake(4f);
        }

        // "Хугін → Мунін: words" in the chat; another player's ravens with their owner's name
        private void ToChat(Line l)
        {
            Chat chat = Chat.instance;
            Player me = Player.m_localPlayer;
            if (chat == null || me == null || (_owner == null && _perch == null)) return;
            if ((!_local || _perch != null) && Vector3.Distance(me.transform.position, Where) > RavenTalk.HearRange) return;
            bool named = !_local && _perch == null && _owner != null; // another player's two: by their nicknames
            string who = "<color=" + (l.Munin ? RavenSpeech.MuninName : RavenSpeech.HuginName) + ">" + (named ? Nicks.Name(_owner, l.Munin) : Plugin.RavenName(l.Munin)) + "</color>"
                + "<color=#8A93A0> → " + (named ? Nicks.Name(_owner, !l.Munin) : Plugin.RavenName(!l.Munin)) + (_perch != null ? (_perch.GetComponent<NestView>() != null ? Lang.T(" · у гнізді") : Lang.T(" · на жердині")) : _local ? "" : " · " + _owner.GetPlayerName()) + "</color>";
            try
            {
                chat.AddString(who, l.Text, Talker.Type.Normal, false);
                HarmonyLib.Traverse.Create(chat).Field("m_hideTimer").SetValue(0f); // the chat shows for a moment
            }
            catch { }
        }

        private void Update()
        {
            if (_owner == null && _perch == null) { Destroy(gameObject); return; }
            // your own talk ends when one of the two leaves the shoulder (sent on a strike, off on an outing...);
            // a perch's when one of the two is gone from it
            if (!_ending && Time.time >= _lookAt && (_local || _perch != null))
            {
                _lookAt = Time.time + 0.25f;
                bool gone = _perch != null ? (_perch.Bird(false) == null || _perch.Bird(true) == null) : (!Shoulder.Present(_owner, false) || !Shoulder.Present(_owner, true));
                if (gone) { _ending = true; _next = _lines.Count; }
            }
            if (!_ending && Time.time >= _at)
            {
                if (_thinking) { if (!VoiceLate()) Say(); }
                else if (_next < _lines.Count) StartThinking();
                else _ending = true;
            }

            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            // the older lines shrink and dim, the newest is full; the thinking row pulses
            int said = _rows.Count;
            float y = 0f;
            for (int i = 0; i < said; i++)
            {
                Row r = _rows[i];
                int back = said - 1 - i; // 0 = the newest
                float wantScale = back == 0 ? 1f : back == 1 ? OldScale : OlderScale;
                float wantAlpha = back == 0 ? 1f : back == 1 ? 0.62f : 0.45f;
                float wantGlow = back == 0 ? 0.34f : 0.24f; // the colour stays, a little quieter
                r.Scale = Mathf.Lerp(r.Scale, wantScale, k);
                r.Alpha = Mathf.Lerp(r.Alpha, wantAlpha, k);
                r.GlowA = Mathf.Lerp(r.GlowA, wantGlow, k);
                float band = r.H * r.Scale;
                r.Rt.sizeDelta = new Vector2(Width, band);
                r.Rt.anchoredPosition = new Vector2(0f, y);
                r.Content.localScale = new Vector3(r.Scale, r.Scale, 1f);
                r.Cg.alpha = r.Alpha;
                Color gc = r.Glow.color; gc.a = r.GlowA; r.Glow.color = gc;
                r.Band.Set(-y, -y + band, GlowWidth);
                y -= band; // the next band starts right where this one ends: no gaps
            }
            if (_typing != null)
            {
                _typing.Alpha = Mathf.Lerp(_typing.Alpha, 1f, k);
                _typing.Cg.alpha = _typing.Alpha;
                _typing.Rt.anchoredPosition = new Vector2(0f, y);
                _typing.Band.Set(-y, -y + _typing.H, GlowWidth);
                float t = Time.unscaledTime * 6f;
                Pulse(_typing.Dot0, t); Pulse(_typing.Dot1, t - 0.9f); Pulse(_typing.Dot2, t - 1.8f);
                y -= _typing.H;
            }
            _height = Mathf.Lerp(_height, Mathf.Max(20f, -y), k);
            _rt.sizeDelta = new Vector2(Width, _height);

            Place(k);
        }

        private static void Pulse(Image dot, float t)
        {
            if (dot == null) return;
            Color c = dot.color;
            c.a = 0.35f + 0.65f * Mathf.Clamp01(Mathf.Sin(t) * 0.5f + 0.5f);
            dot.color = c;
        }

        // over the head; hidden with the inventory, map and menus; others' only near, melting away towards 30 m
        private void Place(float k)
        {
            Camera cam = Utils.GetMainCamera();
            float s = RavenSpeech.Host.Scale;
            float want = _ending ? 0f : 1f;
            bool hidden = InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible() || Game.IsPaused();
            if (cam == null || hidden) want = 0f;
            else
            {
                Vector3 at = Anchor;
                Vector3 sp = cam.WorldToScreenPoint(at);
                bool inView = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
                if (sp.z <= 0f || (!_local && !inView)) want = 0f; // someone else's talk: only while they are in view
                else
                {
                    float size = _local ? 1f : 0.85f;
                    transform.localScale = new Vector3(size, size, 1f);
                    float half = Width * 0.5f * s * size;
                    _rt.position = new Vector3(Mathf.Round(Mathf.Clamp(sp.x, half + 4f * s, Screen.width - half - 4f * s)), Mathf.Round(Mathf.Clamp(sp.y, 4f * s, Screen.height - _height * s * size - 4f * s)), 0f); // whole pixels: no shiver
                    if (!_local)
                    {
                        Player me = Player.m_localPlayer;
                        float dist = Vector3.Distance(me != null ? me.transform.position : cam.transform.position, Where);
                        want *= 1f - Mathf.InverseLerp(RavenTalk.HearRange * 0.65f, RavenTalk.HearRange, dist);
                    }
                }
            }
            _cg.alpha = Mathf.Lerp(_cg.alpha, want, _ending ? 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime) : k);
            if (_ending && _cg.alpha < 0.02f)
            {
                TalkBubble cur;
                if (_perch != null) { if (ShownPerch.TryGetValue(_perch, out cur) && cur == this) ShownPerch.Remove(_perch); }
                else if (Shown.TryGetValue(_owner, out cur) && cur == this) Shown.Remove(_owner);
                Destroy(gameObject);
            }
        }

        // the line waits for its sound after the thinking dots, at most the voice's max wait; then it goes without it
        private bool VoiceLate()
        {
            if (_clip == null && _voice != null && _next < _voice.Length && _voice[_next] >= 0)
            {
                AudioClip c; int st = RavenVoice.Take(_voice[_next], out c);
                if (st == 1) { _clip = c; _voice[_next] = -1; }
                else if (st == 0 && Time.time < _at + RavenVoice.MaxWait) return true;
                else { RavenVoice.Drop(_voice[_next]); _voice[_next] = -1; }
            }
            // another raven is speaking (a card, another talk): this line waits its turn a while, then goes without its voice
            if (_clip != null && RavenVoice.BusyNear(SpeakerAt(_lines[_next].Munin)))
            {
                if (Time.time < _at + RavenVoice.MaxWait + RavenVoice.TurnWait) return true;
                Destroy(_clip); _clip = null;
            }
            return false;
        }

        private Vector3 SpeakerAt(bool munin)
        {
            GameObject who = _perch != null ? _perch.Bird(munin) : Shoulder.ModelOf(_owner, munin);
            return who != null ? who.transform.position : Where;
        }

        private void OnDestroy()
        {
            if (_voice != null) foreach (int id in _voice) if (id >= 0) RavenVoice.Drop(id); // a talk cut short: its sounds are not wanted
            if (_clip != null) Destroy(_clip);
            if (_perch != null || ShownPerch.Count > 0) { List<PerchView> pg = null; foreach (KeyValuePair<PerchView, TalkBubble> kv in ShownPerch) if (kv.Value == this || kv.Key == null) { if (pg == null) pg = new List<PerchView>(); pg.Add(kv.Key); } if (pg != null) foreach (PerchView g in pg) ShownPerch.Remove(g); }
            if (_owner == null && _perch == null) { List<Player> gone = new List<Player>(); foreach (KeyValuePair<Player, TalkBubble> kv in Shown) if (kv.Value == this || kv.Key == null) gone.Add(kv.Key); foreach (Player g in gone) Shown.Remove(g); }
        }
    }
}
#endif

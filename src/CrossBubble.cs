#if RAVEN_ITEM
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // A talk of ravens of different players (CrossTalk) shown as a "bridge" over them, read like a group chat, unlike the
    // talk of your own two (TalkBubble, one column over your head):
    //   - your ravens' lines on the right, everybody else's on the left (a talk you only overhear: all on the left);
    //   - each line a bubble of its own, fitted to its words, the raven's round picture beside it (a ring of its owner's
    //     colour); on top, small: «Костя · Мудрагель → Сигурдів Кусака» (whose, who, to whom), the words under it;
    //   - the line being said: white, its bubble tinted with its owner's colour; the older ones smaller, grey and paler;
    //   - the last three lines only; under them «Кусака ●●●»: who speaks next;
    //   - a thin thread down to each player's head: dim and grey, lit in that player's colour while their raven speaks;
    //   - it hangs over the middle between the players and floats after them (eased).
    // A talk your ravens are in is shown in full, also kept on the screen's edge; one you overhear only in view and within
    // 30 m, faint from afar and clear when you come close. Every line also goes to the chat.
    internal sealed class CrossBubble : MonoBehaviour
    {
        private const float Width = 450f;
        private const float Pic = 30f, PicSmall = 22f, Gap = 7f, PadX = 11f, PadY = 6f, Side = 10f, RowGap = 6f, Top = 9f; // tight: more of the screen left free
        private const float TextMax = Width * 0.82f - Pic - Gap - Side - 2f * PadX; // the widest a line's words go before wrapping
        private static readonly Color BubbleBase = new Color(0.10f, 0.12f, 0.16f, 0.92f);
        private const float OldScale = 0.8f, OlderScale = 0.7f; // the lines said already: smaller, out of the way of the new one
        internal const float Slower = 1.2f; // a little longer on each line than the ravens' own talks: the bridge is read by several
        private static readonly Color Now = new Color(0.93f, 0.94f, 0.96f, 1f), Said = new Color(0.58f, 0.61f, 0.66f, 1f); // the words being said / said already
        private const string NickNow = "#FFFFFF", NickSaid = "#9AA0A9"; // the ravens' names in the words: a touch lighter than the rest
        private const int Shown = 3;                 // the last lines kept on the bridge; older ones go
        private const float Lift = 0.75f; // m over the heads' middle
        private static readonly List<CrossBubble> Live = new List<CrossBubble>();

        // ---------------------------------------------------------------- asked from outside

        internal static bool Busy(Player p)
        {
            foreach (CrossBubble b in Live) if (b != null && !b._ending && b.Has(p)) return true;
            return false;
        }

        // In a talk this raven looks at the one speaking; the speaker looks at whom it speaks to.
        internal static Transform LookFor(Player p, bool munin)
        {
            if (Live.Count == 0 || p == null) return null;
            foreach (CrossBubble b in Live)
            {
                if (b == null || b._ending || b._said < 0) continue;
                CrossTalk.Line l = b._lines[b._said];
                int me = -1;
                for (int i = 0; i < b._whos.Count; i++) if (b._whos[i].P == p && b._whos[i].Munin == munin) { me = i; break; }
                if (me < 0) continue;
                int at = me == l.Who ? l.To : l.Who;
                if (at == me) return null;
                GameObject g = Shoulder.ModelOf(b._whos[at].P, b._whos[at].Munin);
                return g != null ? g.transform : null;
            }
            return null;
        }

        internal static void Play(List<CrossTalk.Who> whos, List<CrossTalk.Line> lines, bool mine)
        {
            if (whos == null || lines == null || lines.Count == 0 || RavenSpeech.Host == null) return;
            for (int i = Live.Count - 1; i >= 0; i--) // a talk with any of the same players ends the old one at once
            {
                CrossBubble old = Live[i];
                if (old == null) { Live.RemoveAt(i); continue; }
                foreach (CrossTalk.Who w in whos) if (old.Has(w.P)) { Live.RemoveAt(i); Destroy(old.gameObject); break; }
            }
            GameObject go = new GameObject("RavenWatch_CrossTalk", typeof(RectTransform));
            go.transform.SetParent(RavenSpeech.Host.transform, false);
            CrossBubble b = go.AddComponent<CrossBubble>();
            b.Build(whos, lines, mine);
            Live.Add(b);
        }

        private bool Has(Player p)
        {
            foreach (CrossTalk.Who w in _whos) if (w.P == p) return true;
            return false;
        }

        // a colour of its own for every player, from the name (the same on every game), soft enough to sit beside the ravens'
        internal static Color OwnerColor(string name)
        {
            uint h = 2166136261u;
            if (name != null) foreach (char c in name) { h ^= c; h *= 16777619u; }
            float hue = (h % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.42f, 0.95f);
        }

        private static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

        // ---------------------------------------------------------------- building

        private sealed class Row
        {
            public RectTransform Rt, Content, Bubble; public CanvasGroup Cg; public Image Bg; public Color Hot;
            public TextMeshProUGUI Body; public string SaidText; public bool Old;
            public float H, Scale = 1f, Alpha; public Image Dot0, Dot1, Dot2;
        }

        private sealed class Owner { public Player P; public Color C; public float Foot; public bool Right; public RectTransform Thread; public Image ThreadImg; public float Lit; }

        // a thread at rest: its owner's colour, greyed and dark; while one of that player's ravens is speaking: bright
        private static Color ThreadRest(Color c) { Color g = Color.Lerp(c, new Color(0.42f, 0.44f, 0.48f), 0.65f) * 0.7f; g.a = 0.35f; return g; }

        private List<CrossTalk.Who> _whos;
        private List<CrossTalk.Line> _lines;
        private readonly List<Owner> _owners = new List<Owner>();
        private int[] _ownerOf;       // raven -> its owner's index
        private bool _mine, _ending, _thinking, _placed;
        private readonly List<Row> _rows = new List<Row>();
        private Row _typing;
        private RectTransform _rt, _rowsRt, _threads;
        private CanvasGroup _cg;
        private int _next, _said = -1;
        private float _at, _height = 40f;
        private int[] _voice;         // each line's sound, asked all at once (RavenVoice), taken when its line comes
        private AudioClip _clip;
        private Vector2 _pos;

        private void Build(List<CrossTalk.Who> whos, List<CrossTalk.Line> lines, bool mine)
        {
            _whos = whos; _lines = lines; _mine = mine;
            _voice = new int[lines.Count];
            for (int i = 0; i < lines.Count; i++) { CrossTalk.Who w = whos[lines[i].Who]; _voice[i] = RavenVoice.Ahead(w.Munin, RavenVoice.VoiceOf(w.P, w.Munin, null), lines[i].Text); }
            _ownerOf = new int[whos.Count];
            for (int i = 0; i < whos.Count; i++)
            {
                int o = _owners.FindIndex(x => x.P == whos[i].P);
                if (o < 0) { _owners.Add(new Owner { P = whos[i].P, C = OwnerColor(whos[i].P.GetPlayerName()) }); o = _owners.Count - 1; }
                _ownerOf[i] = o;
            }
            // the sides, as in a chat: your ravens on the right, everybody else's on the left (a talk you only overhear: all left)
            for (int o = 0; o < _owners.Count; o++) { _owners[o].Right = _owners[o].P == Player.m_localPlayer; }
            // where each player's thread leaves the bridge's foot: spread along their side, not all from one point
            int lefts = 0, leftAt = 0;
            foreach (Owner o in _owners) if (!o.Right) lefts++;
            foreach (Owner o in _owners) o.Foot = o.Right ? 0.85f : lefts <= 1 ? 0.15f : Mathf.Lerp(0.1f, 0.55f, leftAt++ / (float)(lefts - 1));

            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = Vector2.zero;
            _rt.pivot = new Vector2(0.5f, 0f);
            _rt.sizeDelta = new Vector2(Width, _height);
            _cg = gameObject.AddComponent<CanvasGroup>();
            _cg.blocksRaycasts = false; _cg.interactable = false; _cg.alpha = 0f;

            _threads = Child(transform, "threads"); // first: under everything
            Stretch(_threads, 0f, 0f, 0f, 0f);
            foreach (Owner o in _owners)
            {
                o.Thread = Child(_threads, "thread");
                o.Thread.anchorMin = o.Thread.anchorMax = new Vector2(0.5f, 0f);
                o.Thread.pivot = new Vector2(0f, 0.5f);
                o.ThreadImg = o.Thread.gameObject.AddComponent<Image>();
                o.ThreadImg.color = ThreadRest(o.C); o.ThreadImg.raycastTarget = false;
            }

            RectTransform shadow = Child(transform, "shadow");
            Stretch(shadow, -14f, -16f, 14f, 8f);
            Image sh = shadow.gameObject.AddComponent<Image>(); sh.sprite = RavenSpeech.Glow(); sh.color = new Color(0f, 0f, 0f, 0.55f); sh.raycastTarget = false;

            RectTransform body = Child(transform, "body");
            Stretch(body, 0f, 0f, 0f, 0f);
            Image bi = body.gameObject.AddComponent<Image>();
            bi.sprite = RavenSpeech.Fill(); bi.type = Image.Type.Sliced; bi.color = new Color(0.035f, 0.045f, 0.06f, 0.94f); bi.raycastTarget = false;
            _rowsRt = Child(body, "rows"); Stretch(_rowsRt, 0f, 0f, 0f, 0f);

            RectTransform fr = Child(transform, "frame");
            Stretch(fr, 0f, 0f, 0f, 0f);
            Image fi = fr.gameObject.AddComponent<Image>(); fi.sprite = RavenSpeech.Ring(); fi.type = Image.Type.Sliced; fi.color = RavenSpeech.Edge; fi.raycastTarget = false;

            Nicked();
            _next = 0;
            StartThinking();
        }

        private static string Esc(string s) { return s.Replace("<", "").Replace(">", ""); } // a name is never markup

        // the ravens of the talk by name and as called («Кривопер», «Кривопере»): found once, lit a touch in the words
        private Regex _nicks;
        private void Nicked()
        {
            List<string> forms = new List<string>();
            foreach (CrossTalk.Who w in _whos)
            {
                string a = Esc(Nicks.Name(w.P, w.Munin)), b = Esc(Nicks.Call(w.P, w.Munin));
                if (a.Length >= 2 && !forms.Contains(a)) forms.Add(a);
                if (b.Length >= 2 && !forms.Contains(b)) forms.Add(b);
            }
            if (forms.Count == 0) return;
            forms.Sort((x, y) => y.Length.CompareTo(x.Length)); // the longer first («Кривопере» before «Кривопер»)
            for (int i = 0; i < forms.Count; i++) forms[i] = Regex.Escape(forms[i]);
            try { _nicks = new Regex("(?<![\\p{L}\\p{N}>=#])(?:" + string.Join("|", forms.ToArray()) + ")(?![\\p{L}\\p{N}<])"); } catch { _nicks = null; }
        }

        private string Nick(string words, string hex)
        {
            return _nicks == null ? words : _nicks.Replace(words, "<color=" + hex + ">$0</color>");
        }

        // «Костя · Мудрагель → Сигурдів Кусака»: whose raven, which one, to whom (small, over the words)
        private static string Label(CrossTalk.Who w, CrossTalk.Who to)
        {
            string owner = w.P.GetPlayerName();
            return "<size=14><b><color=" + Hex(OwnerColor(owner)) + ">" + Esc(owner) + "</color></b><color=#5C6470>  ·  </color><b>"
                + Nicks.Plain(w.P, w.Munin, w.Munin ? RavenSpeech.MuninName : RavenSpeech.HuginName) + "</b><color=#7C8592>  <nobr>→  " + To(w, to) + "</nobr></color></size>"; // to whom: never torn over two lines
        }

        // to whom: its own partner by the name alone, another player's raven by its owner («Сигурдів Кусака»)
        private static string To(CrossTalk.Who w, CrossTalk.Who to)
        {
            if (to == null || to.P == null) return Lang.T("всім");
            string nick = Esc(Nicks.Name(to.P, to.Munin));
            if (to.P == w.P) return nick;
            string owner = to.P.GetPlayerName();
            if (!Lang.IsSource) return Lang.F("{0} гравця {1}", nick, Esc(owner)); // another language says whose in its own way («Sigurd's Biter»)
            string pos = Possessive(owner);
            return pos != null ? pos + " " + nick : nick + " (" + Esc(owner) + ")";
        }

        // A player's name as whose: Костя → Костин, Валентина → Валентинин, Марія → Маріїн, Сигурд → Сигурдів, Петро →
        // Петрів, Василь → Василів, Андрій → Андріїв. Null when it can't be told (not one Ukrainian word, all capitals,
        // an ending with no such form): then the name goes in brackets.
        private const string Cyr = "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя'ʼ’";
        private static string Possessive(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Trim();
            if (name.Length < 2 || name.Length > 20 || name == name.ToUpperInvariant()) return null;
            foreach (char c in name) if (Cyr.IndexOf(char.ToLowerInvariant(c)) < 0) return null;
            char last = char.ToLowerInvariant(name[name.Length - 1]);
            string stem = name.Substring(0, name.Length - 1);
            if (last == 'а') return stem + "ин";
            if (last == 'я') return "аеєиіїоуюя".IndexOf(char.ToLowerInvariant(stem[stem.Length - 1])) >= 0 ? stem + "їн" : stem + "ин";
            if (last == 'о' || last == 'ь') return stem + "ів";
            if (last == 'й') return stem + "їв";
            if ("еєиіїую'ʼ’".IndexOf(last) >= 0) return null;
            return name + "ів";
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

        // the raven's round picture: a ring of its owner's colour (the same colour as the name over its words and the thread)
        private static RectTransform Avatar(Transform parent, bool munin, Color owner, float size)
        {
            RectTransform pr = Child(parent, "pic");
            pr.sizeDelta = new Vector2(size, size);
            Image outer = pr.gameObject.AddComponent<Image>(); outer.sprite = SeneaLShoulders.Disc(); outer.color = owner; outer.raycastTarget = false;
            RectTransform cut = Child(pr, "cut");
            Stretch(cut, 2f, 2f, -2f, -2f);
            Image ci = cut.gameObject.AddComponent<Image>(); ci.sprite = SeneaLShoulders.Disc(); ci.color = new Color(0.03f, 0.035f, 0.045f, 1f); ci.raycastTarget = false;
            cut.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform face = Child(cut, "face");
            Stretch(face, 1.5f, 1.5f, -1.5f, -1.5f);
            face.localScale = new Vector3(munin ? -1f : 1f, 1f, 1f);
            Image fi = face.gameObject.AddComponent<Image>(); fi.sprite = AbilityIcons.For(munin ? "munin" : "hugin", munin); fi.preserveAspect = true; fi.raycastTarget = false;
            return pr;
        }

        // A row: on its side (yours right, the others left), the picture at the outer edge, low, the bubble beside it.
        // The sizes come after the words are measured (Fit).
        private Row Band(int who, string name, float pic)
        {
            Owner o = _owners[_ownerOf[who]];
            float t = o.Right ? 1f : 0f;
            Row r = new Row();
            r.Rt = Child(_rowsRt, name);
            r.Rt.anchorMin = r.Rt.anchorMax = new Vector2(0f, 1f); r.Rt.pivot = new Vector2(0f, 1f);
            r.Content = Child(r.Rt, "content");
            r.Content.anchorMin = r.Content.anchorMax = new Vector2(t, 1f); r.Content.pivot = new Vector2(t, 1f);
            r.Content.anchoredPosition = new Vector2(o.Right ? -Side : Side, 0f);
            r.Cg = r.Content.gameObject.AddComponent<CanvasGroup>(); r.Cg.alpha = 0f;
            RectTransform pr = Avatar(r.Content, _whos[who].Munin, o.C, pic);
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(t, 0f); pr.anchoredPosition = Vector2.zero;
            r.Bubble = Child(r.Content, "bubble");
            r.Bubble.anchorMin = r.Bubble.anchorMax = r.Bubble.pivot = new Vector2(t, 0f);
            r.Bubble.anchoredPosition = new Vector2(o.Right ? -(pic + Gap) : pic + Gap, 0f);
            r.Bg = r.Bubble.gameObject.AddComponent<Image>(); r.Bg.sprite = RavenSpeech.Fill(); r.Bg.type = Image.Type.Sliced; r.Bg.raycastTarget = false;
            r.Bg.color = BubbleBase;
            Color hot = Color.Lerp(BubbleBase, o.C, 0.24f); hot.a = 0.95f; r.Hot = hot; // the line being said: a tint of its owner
            r.Bubble.SetAsFirstSibling(); // under the picture
            return r;
        }

        private static void Fit(Row r, float bubbleW, float h, float pic)
        {
            r.H = h;
            r.Rt.sizeDelta = new Vector2(Width, h + RowGap);
            r.Content.sizeDelta = new Vector2(pic + Gap + bubbleW, h);
            r.Bubble.sizeDelta = new Vector2(bubbleW, h);
        }

        private Row AddRow(CrossTalk.Line l)
        {
            CrossTalk.Who w = _whos[l.Who];
            CrossTalk.Who to = l.To >= 0 && l.To < _whos.Count && l.To != l.Who ? _whos[l.To] : null;
            Row r = Band(l.Who, "row", Pic);
            RectTransform tr = Child(r.Bubble, "text");
            Stretch(tr, PadX, PadY, -PadX, -PadY);
            r.Body = SpeechCard.NewText(tr.gameObject, RavenSpeech.Font(false));
            r.Body.fontSize = 17f; r.Body.richText = true; r.Body.textWrappingMode = TextWrappingModes.Normal; r.Body.raycastTarget = false;
            r.Body.color = Now;
            r.Body.alignment = _owners[_ownerOf[l.Who]].Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            string words = RavenSpeech.MarkNames(l.Text);
            string full = Label(w, to) + "\n" + Nick(words, NickNow);
            r.SaidText = Label(w, to) + "\n" + Nick(words, NickSaid); // the same once it is said: the names stay a touch lighter in the grey
            r.Body.text = full;
            Vector2 pref = r.Body.GetPreferredValues(full, TextMax, 0f); // the bubble fits its words: short lines, short bubbles
            float tw = Mathf.Min(TextMax, Mathf.Ceil(pref.x) + 2f), th = Mathf.Ceil(pref.y);
            Fit(r, tw + 2f * PadX, Mathf.Max(Pic, th + 2f * PadY), Pic);
            _rows.Add(r);
            return r;
        }

        // «Кусака ●●●»: who speaks next
        private Row AddTyping(int who)
        {
            Row r = Band(who, "typing", PicSmall);
            RectTransform tr = Child(r.Bubble, "name");
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 0.5f);
            TextMeshProUGUI name = SpeechCard.NewText(tr.gameObject, RavenSpeech.Font(false));
            name.fontSize = 14f; name.richText = false; name.textWrappingMode = TextWrappingModes.NoWrap; name.raycastTarget = false;
            name.color = new Color(0.62f, 0.66f, 0.72f, 1f); name.alignment = TextAlignmentOptions.MidlineLeft;
            name.text = Esc(Nicks.Name(_whos[who].P, _whos[who].Munin));
            float nw = Mathf.Ceil(name.GetPreferredValues(name.text).x) + 1f;
            tr.sizeDelta = new Vector2(nw, 20f); tr.anchoredPosition = new Vector2(PadX, 0f);
            RectTransform holder = Child(r.Bubble, "dots");
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0f, 0.5f);
            holder.sizeDelta = new Vector2(28f, 8f); holder.anchoredPosition = new Vector2(PadX + nw + 7f, 0f);
            Image[] dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform d = Child(holder, "dot" + i);
                d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f); d.pivot = new Vector2(0.5f, 0.5f);
                d.anchoredPosition = new Vector2(3f + i * 10f, 0f); d.sizeDelta = new Vector2(5f, 5f);
                dots[i] = d.gameObject.AddComponent<Image>(); dots[i].sprite = SeneaLShoulders.Disc(); dots[i].color = new Color(0.7f, 0.74f, 0.8f, 1f); dots[i].raycastTarget = false;
            }
            r.Dot0 = dots[0]; r.Dot1 = dots[1]; r.Dot2 = dots[2];
            Fit(r, PadX + nw + 7f + 26f + PadX, PicSmall + 2f, PicSmall);
            return r;
        }

        private static void Drop(Row r) { if (r != null && r.Rt != null) Destroy(r.Rt.gameObject); }

        // ---------------------------------------------------------------- the talk going on

        private void StartThinking()
        {
            if (_next >= _lines.Count) return;
            Drop(_typing);
            _typing = AddTyping(_lines[_next].Who);
            _thinking = true;
            _at = Time.time + Mathf.Clamp(0.6f + _lines[_next].Text.Length / 90f, 0.8f, 1.8f);
        }

        private void Say()
        {
            CrossTalk.Line l = _lines[_next];
            Drop(_typing); _typing = null;
            Row row = AddRow(l);
            while (_rows.Count > Shown) { Drop(_rows[0]); _rows.RemoveAt(0); } // the oldest goes: the bridge keeps only the last few
            ToChat(l);
            _said = _next;
            _thinking = false;
            _next++;
            _at = Time.time + TalkBubble.After(l.Text, _next >= _lines.Count, Slower);
            CrossTalk.Who w = _whos[l.Who];
            GameObject model = Shoulder.ModelOf(w.P, w.Munin);
            if (_clip != null) // its voice, from the raven saying it; the next line only after it is over
            {
                GameObject from = model != null ? model : w.P != null ? w.P.gameObject : null;
                if (from != null) { WordReveal.Begin(row.Body, _clip.length); RavenVoice.Play(from, _clip, w.Munin); _at = Mathf.Max(_at, Time.time + _clip.length + 0.4f); }
                else Destroy(_clip);
                _clip = null;
            }
            Animator anim = model != null ? model.GetComponentInChildren<Animator>() : null;
            ShoulderLife.TalkOn(anim);
            foreach (CrossTalk.Who x in _whos) // everybody in it wakes to listen
            {
                ShoulderLife life = Shoulder.LifeOf(x.P, x.Munin);
                if (life != null) life.Wake(Mathf.Max(4f, TalkBubble.Pace(l.Text) * Slower + 1f));
            }
        }

        // the line in the chat too, for those in it or near
        private void ToChat(CrossTalk.Line l)
        {
            Chat chat = Chat.instance;
            Player me = Player.m_localPlayer;
            if (chat == null || me == null) return;
            if (!_mine && Vector3.Distance(me.transform.position, Middle()) > CrossTalk.HearRange) return;
            ChatLine(_whos[l.Who], _whos[l.To], l.Text);
        }

        // one raven's line to another's in the chat (a talk, a word in passing)
        internal static void ChatLine(CrossTalk.Who w, CrossTalk.Who to, string text)
        {
            Chat chat = Chat.instance;
            if (chat == null || w.P == null || to.P == null) return;
            string who = "<b><color=" + Hex(OwnerColor(w.P.GetPlayerName())) + ">" + Esc(w.P.GetPlayerName()) + "</color></b><color=#5C6470> · </color>"
                + Nicks.Plain(w.P, w.Munin, w.Munin ? RavenSpeech.MuninName : RavenSpeech.HuginName)
                + "<color=#8A93A0> → " + To(w, to) + "</color>";
            try
            {
                chat.AddString(who, text, Talker.Type.Normal, false);
                HarmonyLib.Traverse.Create(chat).Field("m_hideTimer").SetValue(0f);
            }
            catch { }
        }

        private Vector3 Middle()
        {
            Vector3 m = Vector3.zero;
            int n = 0;
            foreach (Owner o in _owners) if (o.P != null) { m += o.P.transform.position; n++; }
            return n > 0 ? m / n : Vector3.zero;
        }

        private static Vector3 HeadOf(Player p) { return p.transform.position + Vector3.up * 2.1f; }

        private void Update()
        {
            foreach (Owner o in _owners) if (o.P == null || o.P.IsDead()) { _ending = true; _next = _lines.Count; break; }
            if (!_ending && Time.time >= _at)
            {
                if (_thinking) { if (!VoiceLate()) Say(); }
                else if (_next < _lines.Count) StartThinking();
                else _ending = true;
            }

            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime), fast = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
            int said = _rows.Count;
            float y = -Top;
            for (int i = 0; i < said; i++)
            {
                Row r = _rows[i];
                int back = said - 1 - i;
                float wantScale = back == 0 ? 1f : back == 1 ? OldScale : OlderScale;
                float wantAlpha = back == 0 ? 1f : back == 1 ? 0.8f : 0.6f;
                r.Scale = Mathf.Lerp(r.Scale, wantScale, k);
                r.Alpha = Mathf.Lerp(r.Alpha, wantAlpha, back == 0 ? fast : k); // the line being said: clear at once
                if (back > 0 && !r.Old && r.Body != null) { r.Old = true; r.Body.text = r.SaidText; }
                if (r.Body != null) r.Body.color = Color.Lerp(r.Body.color, back == 0 ? Now : Said, k); // being said: white; said already: grey
                if (r.Bg != null) r.Bg.color = Color.Lerp(r.Bg.color, back == 0 ? r.Hot : BubbleBase, k); // its owner's tint while it is said
                float band = (r.H + RowGap) * r.Scale;
                r.Rt.sizeDelta = new Vector2(Width, band);
                r.Rt.anchoredPosition = new Vector2(0f, y);
                r.Content.localScale = new Vector3(r.Scale, r.Scale, 1f);
                r.Cg.alpha = r.Alpha;
                y -= band;
            }
            if (_typing != null)
            {
                _typing.Alpha = Mathf.Lerp(_typing.Alpha, 1f, k);
                _typing.Cg.alpha = _typing.Alpha;
                _typing.Rt.anchoredPosition = new Vector2(0f, y);
                float t = Time.unscaledTime * 6f;
                Pulse(_typing.Dot0, t); Pulse(_typing.Dot1, t - 0.9f); Pulse(_typing.Dot2, t - 1.8f);
                y -= _typing.H + RowGap;
            }
            float wantH = Mathf.Max(Top + 30f, -y + Top - RowGap);
            _height = Mathf.Lerp(_height, wantH, wantH > _height ? fast : k); // grows at once (nothing hangs out of it), shrinks softly
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

        // over the middle of the players, following them softly; the threads down to each head
        private void Place(float k)
        {
            Camera cam = Utils.GetMainCamera();
            float s = RavenSpeech.Host.Scale;
            float want = _ending ? 0f : 1f;
            bool hidden = InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible() || Game.IsPaused();
            float size = _mine ? 1f : 0.85f;
            if (cam == null || hidden) want = 0f;
            else
            {
                Vector3 top = Vector3.zero;
                int n = 0;
                foreach (Owner o in _owners) if (o.P != null) { top += HeadOf(o.P); n++; }
                if (n == 0) want = 0f;
                else
                {
                    top = top / n + Vector3.up * Lift;
                    Vector3 sp = cam.WorldToScreenPoint(top);
                    bool inView = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
                    if (sp.z <= 0f || (!_mine && !inView)) want = 0f;
                    else
                    {
                        transform.localScale = new Vector3(size, size, 1f);
                        float half = Width * 0.5f * s * size;
                        Vector2 target = new Vector2(Mathf.Clamp(sp.x, half + 4f * s, Screen.width - half - 4f * s), Mathf.Clamp(sp.y, 4f * s, Screen.height - _height * s * size - 4f * s));
                        _pos = _placed ? Vector2.Lerp(_pos, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime)) : target; // floats after them, a little behind
                        _placed = true;
                        _rt.position = new Vector3(Mathf.Round(_pos.x), Mathf.Round(_pos.y), 0f);
                        if (!_mine)
                        {
                            Player me = Player.m_localPlayer;
                            float dist = Vector3.Distance(me != null ? me.transform.position : cam.transform.position, Middle());
                            want *= dist > CrossTalk.HearRange ? 0f : Mathf.Lerp(0.95f, 0.3f, Mathf.InverseLerp(6f, 18f, dist)); // overheard: faint from afar, come closer to read it
                        }
                        Threads(cam, s * size, k);
                    }
                }
            }
            _cg.alpha = Mathf.Lerp(_cg.alpha, want, _ending ? 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime) : k);
            if (_ending && _cg.alpha < 0.02f) { Live.Remove(this); Destroy(gameObject); }
        }

        // a thin line from the bubble's bottom (towards that player's side) down to the player's head: dim and grey, lit up
        // bright (and a little thicker) while the line on the bridge is one of that player's ravens' (it stays lit while the
        // next one thinks, as that line's bubble keeps its tint)
        private void Threads(Camera cam, float scale, float k)
        {
            int speaking = !_ending && _said >= 0 ? _ownerOf[_lines[_said].Who] : -1;
            for (int i = 0; i < _owners.Count; i++)
            {
                Owner o = _owners[i];
                if (o.Thread == null) continue;
                o.Lit = Mathf.Lerp(o.Lit, i == speaking ? 1f : 0f, k);
                o.ThreadImg.color = Color.Lerp(ThreadRest(o.C), new Color(o.C.r, o.C.g, o.C.b, 1f), o.Lit);
                Vector3 sp = o.P != null ? cam.WorldToScreenPoint(HeadOf(o.P)) : Vector3.zero;
                if (o.P == null || sp.z <= 0f) { o.ThreadImg.enabled = false; continue; }
                Vector2 from = new Vector2(_rt.position.x + (o.Foot - 0.5f) * Width * scale, _rt.position.y + 2f * scale);
                Vector2 d = new Vector2(sp.x, sp.y) - from;
                float len = d.magnitude;
                if (len < 6f * scale || sp.y > from.y) { o.ThreadImg.enabled = false; continue; } // the head over the bubble's foot: no thread up
                o.ThreadImg.enabled = true;
                o.Thread.position = new Vector3(from.x, from.y, 0f);
                o.Thread.sizeDelta = new Vector2(len / scale, Mathf.Lerp(1.4f, 2.6f, o.Lit));
                o.Thread.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
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
            if (_clip != null && RavenVoice.BusyNear(SpeakerAt(_lines[_next])))
            {
                if (Time.time < _at + RavenVoice.MaxWait + RavenVoice.TurnWait) return true;
                Destroy(_clip); _clip = null;
            }
            return false;
        }

        private Vector3 SpeakerAt(CrossTalk.Line l)
        {
            CrossTalk.Who w = _whos[l.Who];
            GameObject model = Shoulder.ModelOf(w.P, w.Munin);
            return model != null ? model.transform.position : w.P != null ? w.P.transform.position : Middle();
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_voice != null) foreach (int id in _voice) if (id >= 0) RavenVoice.Drop(id); // a talk cut short: its sounds are not wanted
            if (_clip != null) Destroy(_clip);
        }
    }
}
#endif

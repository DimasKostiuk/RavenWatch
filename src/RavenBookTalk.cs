#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // A person's talk (NpcTalk): a scroll on the left of the screen, over every window (the user's mockup 2026-11-05,
    // docs/plans/mockups/talk-scroll.html) — its name on the plaque under the roll, its words in the dark block, the answers to them,
    // the story's sealed card, a request's pinned scrap, «ще на думці», «твоє» as chips, the grey ones' why under them, how it is with you
    // on the margins and the rope (RavenBookTalkRel, the user's design docs/plans/mockups/talk-relation-2026-11-08.html). The mouse only, no numbers: Esc, the ✕ or walking off closes it. While it is open the game is held as for
    // the book (the cursor shows, the hero stands).
    internal static partial class RavenBook
    {
        private static GameObject _talkRoot;
        private static Action<int> _talkPick;
        private static Action _talkClose;

        internal static bool TalkOpen { get { return _talkRoot != null && _talkRoot.activeSelf; } }

        // an answer's row: its words, its part of the scroll (0 under its words · 1 «ще на думці» · 2 «твоє» · 3 its request's scrap · 5 under its words too, an answer to your raven), grey (Off: nothing happens)
        // with its few words under the pointer (Tip: why it is grey), the story's (Story: the sealed card), its request's (Scrap: on the
        // pinned scrap), and how much of what it asks you carry (Have: «3 / 3»)
        internal sealed class TalkRow { public string Text = "", Tip, Have; public int Ring; public bool Off, Story, Scrap; }

        // a line said in the talk, in its order (NpcTalk builds them, the window shows them one by one): the person's, yours (the answer you picked, small, at the top), your raven's aloud
        // (its own block: the person hears it), your raven's aside (a thought in the margin: only you), another person near who butts in (the same block, its name on top: NpcTalkThird).
        // ToRaven: the person's word to your raven (raven_said) — in the raven's frame, not what its answers are to
        internal sealed class TalkSay { public int Kind; public string Text = "", Name = ""; public bool Munin, ToRaven; }
        internal const int SayPerson = 0, SayYou = 1, SayRaven = 2, SayAside = 3, SayOther = 4;
        private static readonly Color TCutHugin = C("#a9c7e8"), TCutMunin = C("#e8b86a"), TAsideHugin = C("#2f4a68"), TAsideMunin = C("#6a4510");

        internal static bool TalkInstant; // (config «Instant talks»: no fades, no waits)

        // a settler's request on its pinned scrap (NpcTalk: NpcRequests) — the thing, the pay in coins, how long it waits, whether you promised
        internal sealed class TalkScrap { public string Item = "", Words = "", Pay = "", Left = ""; public bool Promised; }

        // what your last answer moved (NpcTalkTone, NpcTalkRaven): a line under its words on the next showing, green up, red down; the talk
        // closed by that answer — said in the corner instead (FlushTalkNotes, from NpcTalk.Close)
        private static readonly List<KeyValuePair<string, bool>> TalkNotes = new List<KeyValuePair<string, bool>>();
        private static readonly Color TUp = C("#4f6a2a"), TDown = C("#9a2216");
        internal static void TalkNote(string text, bool up) { if (!string.IsNullOrEmpty(text) && TalkNotes.Count < 4) TalkNotes.Add(new KeyValuePair<string, bool>(text, up)); }
        internal static void FlushTalkNotes(Player p)
        {
            if (p != null) foreach (KeyValuePair<string, bool> n in TalkNotes) p.Message(MessageHud.MessageType.TopLeft, n.Key);
            TalkNotes.Clear();
        }

        internal static void ShowTalk(string who, string says, List<string> answers, Action<int> pick, Action close)
        {
            List<TalkRow> rows = new List<TalkRow>();
            foreach (string a in answers) rows.Add(new TalkRow { Text = a });
            ShowTalk(who, Says(says), rows, pick, close, null, null, false, null);
        }

        internal static void ShowTalk(string who, string says, List<TalkRow> answers, Action<int> pick, Action close, TalkRel rel, TalkScrap scrap)
        {
            ShowTalk(who, Says(says), answers, pick, close, rel, scrap, false, null);
        }

        // a person's words as its lines, one a line of its text (one line at a time, the user 2026-11-06)
        internal static List<TalkSay> Says(string says)
        {
            List<TalkSay> l = new List<TalkSay>();
            foreach (string part in (says ?? "").Split('\n')) if (part.Trim().Length > 0) l.Add(new TalkSay { Kind = SayPerson, Text = part.Trim() });
            if (l.Count == 0) l.Add(new TalkSay { Kind = SayPerson, Text = "…" });
            return l;
        }

        // ---------------------------------------------------------------- the scroll (the user 2026-11-06, docs/plans/mockups/talk-flow.html)
        // One window a talk, kept while you talk to that person; a click does not draw it anew but moves it on (TalkFlow): what was there folds away (the line you picked lit a moment),
        // then two parts come in, each at its own pace — on top the talk: your line, its lines one by one (before your raven butts in, a short while to read the line before it;
        // the raven's portrait and dots, then its words), on the bottom your answers one by one, once the last line said is whole (how long a line is read before the next: RavenSpeech, Pace.Talk / TalkCut — the one place; the lines after the first quicker, Later; no wait for a hum: the person's next line cuts its last, NpcSpeech.Babble); the answers to your raven in its frame, once it spoke.
        // A left click anywhere on the paper brings the next thing now (TalkSkip → Rush); Esc and the ✕ close it as ever. «Instant talks» (TalkInstant): no fades, no waits.
        // Your raven aloud (the user 2026-11-08, «каша з діалогів»: A + В): one frame of its colour where it butts in — its words, the person's word to it (named), your answers to it
        // (a must: NpcTalkRaven.Lock) — the talk goes on above and under it. Two speak in a turn (your raven aloud, another person): every block has its speaker's name.
        // The first screen (menu): its worry with its answers on top, the rest (its request, its story, «на думці», «твоє») below.
        // Its pictures: BookArt.TalkPaper (each person its own torn bottom); its top — assets/book/talk_roll, talk_raven, talk_ring, talk_token, talk_seal, talk_strap, talk_rivet, talk_hook, talk_chain (tools/talk_top_art.html).
        private const float TalkW = 540f, TalkLeft = 40f, TalkTop = 84f, TalkLeast = 300f, TalkPaperW = TalkW - 52f;
        private const float TalkHang = 14f, TalkPaperMin = 430f; // (a talk: its roll right under the screen's top, its hooks' bars running off it; its paper this high at the least; more words make it taller, but the margins' lines and rope never: they hang from its top at their own length, RelMargins — the user 2026-11-08, «половину менше … смужки настрою ніколи не розширяются і мотузка теж»)
        private static readonly Color TBlock = C("#4b1c0d"), TBlockInk = C("#f6e9cc"), TPaper = C("#efe3c4"), TStoryInk = C("#7f4e04"), TStoryFill = C("#f6e1ad"),
            TStoryEdge = C("#b8893e"), TSeal = C("#9a2216"), TScrap = C("#f4e8cb"), TTime = C("#8a3416"), THave = C("#4f6a2a"), TMind = C("#8a3416");
        private static string _talkWho;
        private static TalkFlow _talkFlow;
        private static RectTransform _talkSaid, _talkLow, _talkScroll;
        private static readonly Dictionary<int, Image> _talkFills = new Dictionary<int, Image>(); // (an answer's light: lit when picked)

        private sealed class TalkTurn { public List<TalkSay> Lines; public List<TalkRow> Rows; public TalkRel Rel; public string You; public TalkScrap Scrap; public bool Menu; }

        internal static void ShowTalk(string who, List<TalkSay> lines, List<TalkRow> answers, Action<int> pick, Action close, TalkRel rel, TalkScrap scrap, bool menu, string you)
        {
            EnsureGameFont();
            bool fresh = _talkRoot == null || _talkFlow == null || _talkWho != who;
            if (fresh) TalkShell(who);
            _talkPick = pick; _talkClose = close;
            _talkFlow.Next(new TalkTurn { Lines = lines ?? Says(""), Rows = answers, Rel = rel, Scrap = scrap, Menu = menu, You = you }, fresh);
        }

        // the scroll with nothing on it yet: its top (ScrollShell), its plaque, the paper with its two parts
        private static void TalkShell(string who)
        {
            if (_talkRoot != null) UnityEngine.Object.Destroy(_talkRoot);
            _talkWho = who;
            _talkRoot = OverlayCanvas("RavenWatch_Talk", 31000, true); // (over the game's own windows and ours)
            _talkRoot.AddComponent<TalkKeys>();
            _talkFlow = _talkRoot.AddComponent<TalkFlow>();
            RectTransform paper;
            _talkScroll = ScrollShell(_talkRoot.transform, who, TalkClose, out paper);
            _talkPaper = paper;
            RectTransform said = Rect("Said", paper); VBox(said, 9f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            Image si = said.gameObject.AddComponent<Image>(); si.color = new Color(1f, 1f, 1f, 0f);
            paper.gameObject.AddComponent<TalkSkip>(); // (a left click anywhere on the paper: the next line now — what is said, the margins, between the answers)
            RectTransform low = Rect("Yours", paper); VBox(low, 10f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            _talkSaid = said; _talkLow = low;
        }

        // ---------------------------------------------------------------- words in a row, in any language. A row's children share its width by what each would like: one wrapped text
        // would like it all, and then the rest are squeezed down to nothing and written over each other (the user 2026-11-06, «до тебе» under «рівно»). So in a row:
        //   Flex   takes what is left and no more, wraps in it
        //   Keep   a short word that is never squeezed
        //   Room   a word in a column of its own width: smaller down to least, then cut with «…»
        //   FitTabs tabs fill their row: too many — all a little smaller, together; too few — each the same bit wider, to the row's end
        private static void Flex(GameObject g)
        {
            LayoutElement le = g.GetComponent<LayoutElement>();
            if (le == null) le = g.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.minWidth = 0f; le.preferredWidth = 0f;
        }

        private static TMP_Text Keep(TMP_Text t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Fixed(t.gameObject, Mathf.Ceil(t.GetPreferredValues(t.text, float.PositiveInfinity, float.PositiveInfinity).x) + 1f, -1f);
            return t;
        }

        private static TMP_Text Room(TMP_Text t, float width, float least)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
            float need = t.GetPreferredValues(t.text, float.PositiveInfinity, float.PositiveInfinity).x;
            if (need > width && need > 0f) t.fontSize = Mathf.Max(least, Mathf.Floor(t.fontSize * width / need * 4f) / 4f);
            Fixed(t.gameObject, width, -1f);
            return t;
        }

        private static void FitTabs(RectTransform row, float room)
        {
            float sum = 0f, gaps = 0f;
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            if (h != null) gaps = h.spacing * Mathf.Max(0, row.childCount - 1);
            for (int i = 0; i < row.childCount; i++) sum += row.GetChild(i).GetComponent<LayoutElement>().preferredWidth;
            if (sum <= 0f || row.childCount == 0) return;
            if (sum + gaps <= room)
            {
                if (row.childCount < 2) return; // (one tab alone stays a tab: stretched over the row it read as a heading — the user's board 2026-11-08)
                // room to spare: each tab the same bit wider, so the row ends at the page's edge (the user 2026-11-08, «щоб в кінці не було пустого місця»)
                float more = (room - gaps - sum) / row.childCount;
                for (int i = 0; i < row.childCount; i++) { LayoutElement le = row.GetChild(i).GetComponent<LayoutElement>(); le.minWidth = le.preferredWidth = le.preferredWidth + more; }
                return;
            }
            float k = (room - gaps) / sum;
            for (int i = 0; i < row.childCount; i++)
            {
                Transform tab = row.GetChild(i);
                LayoutElement le = tab.GetComponent<LayoutElement>();
                le.minWidth = le.preferredWidth = le.preferredWidth * k;
                TMP_Text t = tab.GetComponentInChildren<TMP_Text>(); // heavy: one small tab, a window's draw
                if (t != null) { t.fontSize *= k; t.characterSpacing *= k; }
            }
        }

        // the ✕ of every window of ours about people (a talk, a card, a board): one look, its place the caller's
        private const float CloseSize = 44f;
        private static RectTransform CloseButton(Transform parent, Action close)
        {
            RectTransform x = Rect("Close", parent);
            x.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            x.sizeDelta = new Vector2(CloseSize, CloseSize);
            Image i = x.gameObject.AddComponent<Image>(); i.sprite = BookArt.IronClose;
            TMP_Text t = T(x, "×", 28f, C("#e9cf8e"), F.Body, TextAlignmentOptions.Center); Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Clickable(x.gameObject, close);
            Hover(x.gameObject, new Graphic[] { t }, t.color, null);
            return x;
        }

        // a picture of the scroll's top at its place: x right and y down from the scroll's corner, in canvas units (the pictures are twice that).
        // A piece with a drop shadow is drawn with a margin round it for its shadow (tools/talk_top_art.html: SHADE; the user 2026-11-14 — the shadows were cut at the pictures' edges):
        // a picture wider than its place is that margin all round — it stands out by it, its pivot on the piece's own corner (a turned head turns about the same edge
        // as before), and the pointer takes only the piece (raycastPadding: what is put on it reads its body from there, TopBody)
        private static readonly HashSet<string> _topMissing = new HashSet<string>(); // (said once a game)
        private static Image TopPiece(RectTransform top, string pic, float x, float y, float w, float h)
        {
            Sprite s = BookArt.Picture(pic);
            float pad = s != null ? Mathf.Max(0f, (s.rect.width / 2f - w) / 2f) : 0f; // (the roll is narrower than its place, a tile the same: none)
            if (pad > 0f && Mathf.Abs((s.rect.height / 2f - h) / 2f - pad) > 0.5f && _topMissing.Add(pic + ":size") && Plugin.Instance != null)
                Plugin.Instance.Log("talk scroll: picture «" + pic + "» is " + s.rect.width + "×" + s.rect.height + " px, not its place " + w + "×" + h + " with the same margin all round: drawn stretched (tools/talk_top_art.html)");
            RectTransform r = Rect(pic, top); r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.sizeDelta = new Vector2(w + 2f * pad, h + 2f * pad);
            r.pivot = new Vector2(pad / r.sizeDelta.x, 1f - pad / r.sizeDelta.y); r.anchoredPosition = new Vector2(x, -y);
            Image i = r.gameObject.AddComponent<Image>(); i.sprite = s; i.pixelsPerUnitMultiplier = 2f; i.raycastTarget = false; i.raycastPadding = new Vector4(pad, pad, pad, pad);
            if (i.sprite == null) { i.enabled = false; if (_topMissing.Add(pic) && Plugin.Instance != null) Plugin.Instance.Log("talk scroll: no picture «" + pic + "» in the build (assets/book): that piece of its top is not drawn"); } // (nothing there rather than a white box)
            return i;
        }

        // what is put on a top piece (the rune, the ✕) over its body, less its shadow's margin (TopPiece); in: its own inset from that body (left, bottom, right, top)
        private static void TopBody(Image piece, RectTransform r, float l, float b, float rt, float t)
        {
            Vector4 p = piece.raycastPadding;
            Stretch(r, Vector2.zero, Vector2.one, new Vector2(p.x + l, p.y + b), new Vector2(-(p.z + rt), -(p.w + t)));
        }

        // the scroll itself: its top (the roll on its hooks, the ravens' heads, the ✕ on its seal, the plaque), the paper
        private static RectTransform ScrollShell(Transform root, string who, Action close, out RectTransform paper)
        {
            RectTransform scroll = Rect("Scroll", root);
            scroll.anchorMin = scroll.anchorMax = scroll.pivot = new Vector2(0f, 1f);
            scroll.anchoredPosition = new Vector2(TalkLeft, -TalkHang);
            VBox(scroll, 0f, TextAnchor.UpperCenter, 0, 0, 0, 0).childForceExpandWidth = false;
            scroll.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fixed(scroll.gameObject, TalkW, -1f);
            scroll.sizeDelta = new Vector2(TalkW, scroll.sizeDelta.y);

            // its top's room in the column: what stands there is drawn after the paper, below (the roll's shadow falls on the paper)
            RectTransform rod = Rect("Rod", scroll);
            Fixed(rod.gameObject, TalkW, 32f);

            // the paper: its parts are the caller's
            paper = Rect("Paper", scroll);
            Image pi = paper.gameObject.AddComponent<Image>(); pi.sprite = BookArt.TalkPaper(who.GetStableHashCode()); pi.type = Image.Type.Sliced;
            VBox(paper, 12f, TextAnchor.UpperLeft).padding = PaperPlain(); // (with how it is with you: RelPaper's own)
            Fixed(paper.gameObject, TalkPaperW, -1f);
            paper.GetComponent<LayoutElement>().minHeight = TalkPaperMin;

            // the top (the user 2026-11-12, «А. Кований карниз»: the old rod looked poor, its iron did not read as iron, its ring hung on nothing). The parchment is wound on a roller
            // that lies in two forged hooks, their bars running up off the screen; a bronze raven's head on each end of the roller holds a ring in its beak — the left one's carries
            // the rune token, the right one's the iron seal with the ✕ (the whole seal is the button); the name's plaque hangs on two chains. The pictures and each one's place
            // (counted from the scroll's corner, as here): tools/talk_top_art.html
            RectTransform top = Rect("Top", scroll); top.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            top.anchorMin = top.anchorMax = top.pivot = new Vector2(0f, 1f); top.sizeDelta = new Vector2(TalkW, 32f); top.anchoredPosition = Vector2.zero;
            foreach (float at in new[] { 112f, TalkW - 112f }) { TopPiece(top, "talk_strap", at - 7f, -90f, 14f, 110f).type = Image.Type.Tiled; TopPiece(top, "talk_rivet", at - 7f, -12f, 14f, 14f); } // (behind the roll)
            TopPiece(top, "talk_ring", -34f, 17f, 20f, 22f); TopPiece(top, "talk_ring", TalkW + 14f, 17f, 20f, 22f); // (before the heads: the beak covers the ring's top)
            Image roll = TopPiece(top, "talk_roll", 18f, 0f, TalkW - 36f, 40f);
            Sprite across = BookArt.Sliced("talk_roll", 24f, 0f); if (across != null) { roll.sprite = across; roll.type = Image.Type.Sliced; }
            foreach (float at in new[] { 112f, TalkW - 112f }) TopPiece(top, "talk_hook", at - 9f, 6f, 18f, 32f); // (the hooks' lips before the roll)
            TopPiece(top, "talk_raven", -32f, -4f, 58f, 46f);
            TopPiece(top, "talk_raven", TalkW + 32f, -4f, 58f, 46f).rectTransform.localScale = new Vector3(-1f, 1f, 1f); // (the same head turned: it grows left from its place)
            Image token = TopPiece(top, "talk_token", -35f, 29f, 22f, 24f);
            TMP_Text tt = T(token.transform, "ᚱ", 10f, C("#3a2410"), F.Rune, TextAlignmentOptions.Center); TopBody(token, tt.rectTransform, 0f, 0f, 0f, 3f);
            Image seal = TopPiece(top, "talk_seal", TalkW + 5f, 31f, 38f, 40f); seal.gameObject.name = "Close";
            // the ✕ on the disc (r 16 round 19,22 of the seal: 3 in from its sides, 6 from its top, 2 from its bottom), centred by the sign's own shape, not by
            // the font's line (the user 2026-11-14: «збільши на 20% та відцентруй рівно» — it sat low and to the side; 26 before)
            TMP_Text xt = T(seal.transform, "×", 31f, C("#e9cf8e"), F.Body, TextAlignmentOptions.MidlineGeoAligned); TopBody(seal, xt.rectTransform, 3f, 2f, 3f, 6f);
            xt.textWrappingMode = TextWrappingModes.NoWrap; xt.overflowMode = TextOverflowModes.Overflow;
            Clickable(seal.gameObject, close);
            Hover(seal.gameObject, new Graphic[] { xt }, xt.color, null);
            foreach (float dx in new[] { -38f, 38f }) TopPiece(top, "talk_chain", TalkW / 2f + dx - 4f, 29f, 8f, 22f).type = Image.Type.Tiled;
            Plaque(scroll, who, 40f, false);
            RectTransform pl = (RectTransform)scroll.Find("Plaque"); if (pl != null) { pl.anchoredPosition = new Vector2(0f, -66f); pl.SetAsLastSibling(); }
            return scroll;
        }

        // a left click anywhere on the paper (the user 2026-11-18: «клік лівою кнопкою миші відразу відкриває наступний діалог по будь якому місцю на
        // пергаменті»; was: on the said part only, and it showed all of it at once): an answer, a chip, a card take their own clicks first
        internal sealed class TalkSkip : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e) { if (_talkFlow != null && e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) _talkFlow.Rush(); }
        }

        // ---------------------------------------------------------------- what is on the paper and when
        private sealed class TalkFlow : MonoBehaviour
        {
            private sealed class Item { public RectTransform R; public LayoutElement Le; public CanvasGroup Cg; public RectMask2D Mask; public float At, Dur, From, To = -1f, SwapAt = -1f; public bool Out, Done, Grow; public Action Swap, Shown; } // (Shown: once, as it starts to come in — the person speaks it: NpcTalk.LineShown)
            private readonly List<Item> _items = new List<Item>();
            private TalkTurn _next;
            private TalkRel _keptRel; // (this window's last: a new person, a new window)
            private float _buildAt;
            // (the user 2026-11-18 evening, «дуже довго»: the answers, a line's coming in and a raven's dots quick — LineIn, Dots under RavenSpeech.Hold;
            // Later: the lines after the first come at this share of their reading time — «перший … трохи швидше, всі наступні ще швидше». No wait
            // for a person's hum: its next line cuts it, NpcSpeech.Babble)
            private const float Lit = 0.14f, OutFor = 0.3f, InFor = 0.25f, AnsFor = 0.16f, AnsStart = 0.2f, AnsEvery = 0.06f, AnsAfter = 0.12f,
                LineIn = 0.2f, Dots = 0.5f, Later = 0.75f;

            internal bool Folding { get { return _next != null; } }
            private static float Now { get { return Time.unscaledTime; } }
            private static float S(float v) { return TalkInstant ? 0f : v; }

            internal void Next(TalkTurn t, bool fresh)
            {
                enabled = true; // (asleep while nothing moves: Update)
                if (fresh || _items.Count == 0 || TalkInstant) { Clear(); Build(t); return; }
                _next = t; _buildAt = Now + Lit + OutFor;
                foreach (Item i in _items) { i.Cg.blocksRaycasts = false; i.Out = true; i.Grow = false; i.Mask.enabled = true; i.From = Height(i); i.To = 0f; i.At = Now + Lit; i.Dur = OutFor; i.Done = false; i.SwapAt = -1f; i.Shown = null; } // (a line not come in yet is not spoken as it folds away)
            }

            // a click on the paper (TalkSkip): the next thing still to come — a line, your raven's words for its dots, the answers — comes now, and
            // what is after it keeps its pace from there (all of it moved as much earlier); the person speaks it, cutting its own hum before
            internal void Rush()
            {
                enabled = true;
                NpcTalk.YouCut(); // (the hero's line: the person speaks now)
                float now = Now, first = float.MaxValue;
                foreach (Item i in _items)
                {
                    if (i.Out) continue;
                    if (!i.Done && i.At > now) first = Mathf.Min(first, i.At);
                    if (i.SwapAt > now) first = Mathf.Min(first, i.SwapAt);
                }
                if (first < float.MaxValue)
                {
                    float early = first - now;
                    foreach (Item i in _items)
                    {
                        if (i.Out) continue;
                        if (!i.Done && i.At > now) i.At -= early;
                        if (i.SwapAt > now) i.SwapAt -= early;
                    }
                }
                NpcTalk.IdleStir();
            }

            private static float Height(Item i) { return i.Le.preferredHeight >= 0f ? i.Le.preferredHeight : i.R.rect.height; }

            private void Clear()
            {
                foreach (Item i in _items) if (i.R != null) { i.R.SetParent(null, false); UnityEngine.Object.Destroy(i.R.gameObject); } // (out of its part now: Destroy waits for the frame's end, and what is built next counts and measures that part)
                _items.Clear();
                _talkFills.Clear();
                if (_talkPaper != null) Drop(_talkPaper, RelName);
                if (_talkRoot != null) Drop(_talkRoot.transform, "Why");
            }

            // every child of that name out of its parent at once, then gone (Destroy waits for the frame's end: two turns built in one frame — Instant talks — found the same one twice and left the new one for good)
            private static void Drop(Transform parent, string name)
            {
                for (Transform t = parent.Find(name); t != null; t = parent.Find(name)) { t.SetParent(null, false); UnityEngine.Object.Destroy(t.gameObject); }
            }

            private Item Add(RectTransform parent)
            {
                RectTransform r = Rect("Item", parent);
                VBox(r, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0);
                RectMask2D mask = r.gameObject.AddComponent<RectMask2D>(); // (it grows from nothing: what does not fit yet is not drawn — only while it moves)
                mask.padding = new Vector4(0f, 0f, -SealOut, -SealOut); // (its frame reaches SealOut past its right and its top: a seal over its card's corner is whole while it grows and while it folds away too — the user 2026-11-12, «червоний круг після кліку стає на секунду обрізаним»; the game's Clipping takes z and w off the right and the top, so less than nothing adds)
                LayoutElement le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = -1f; le.minHeight = 0f; le.flexibleHeight = 0f; // (its own min is none: it folds to nothing)
                CanvasGroup cg = r.gameObject.AddComponent<CanvasGroup>();
                Item i = new Item { R = r, Le = le, Cg = cg, Mask = mask, Grow = true };
                _items.Add(i);
                return i;
            }

            private static void When(Item i, float at, float dur) { i.At = Now + S(at); i.Dur = S(dur); }

            private void Build(TalkTurn t)
            {
                List<TalkRow> rows = t.Rows ?? new List<TalkRow>();
                if (t.Rel == null) t.Rel = _keptRel; else _keptRel = t.Rel; // (a screen of the talk with no word on it — homes, clothes: the margins stay as they were, nothing jumps)
                RelPaper(t.Rel); // (its margins before anything is measured)
                TMP_Text hint = null; // (a grey answer's why: the line at the bottom, while the pointer is on it)
                Func<TMP_Text> Hint = () => hint;
                List<int> under = new List<int>(), story = new List<int>(), mind = new List<int>(), yours = new List<int>(), scrapRows = new List<int>(), raven = new List<int>();
                for (int i = 0; i < rows.Count; i++)
                {
                    TalkRow r = rows[i];
                    if (r.Scrap) scrapRows.Add(i); else if (r.Story) story.Add(i); else if (r.Ring == 1) mind.Add(i); else if (r.Ring == 2) yours.Add(i); else if (r.Ring == 5) raven.Add(i); else under.Add(i);
                }

                // on top, one at a time
                float at = 0f;
                if (!string.IsNullOrEmpty(t.You))
                {
                    Item y = Add(_talkSaid);
                    T(y.R, "<size=80%><b>" + Lang.T("ТИ") + "</b></size>   «" + t.You + "»", 15f, PSoft, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
                    When(y, 0f, InFor);
                    at = InFor;
                }
                at = Mathf.Max(at, NpcTalk.YouLeft); // (the hero is still saying his answer aloud: it hears him out — NpcTalk.Pick)
                bool worry = false;
                float whole = -1f; // (when the last line is whole: the answers come only after it — the user 2026-11-18)
                float inFor = RavenSpeech.Hold(LineIn), dots = RavenSpeech.Hold(Dots); // (how long a line is read: RavenSpeech, the one place)
                int itsLast = t.Lines.FindLastIndex(x => x.Kind == SayPerson && !x.ToRaven); // (a reproach or a lead comes before its worry: the answers are to what it said last, 2026-11-08; not its word to your raven)
                string named = t.Lines.Exists(x => x.Kind == SayRaven || x.Kind == SayOther) ? _talkWho : null; // (two speak: whose block is whose)
                for (int k = 0; k < t.Lines.Count; k++)
                {
                    TalkSay l = t.Lines[k];
                    if (k > 0)
                    {
                        at += RavenSpeech.Seconds(t.Lines[k - 1].Text, l.Kind == SayRaven || l.Kind == SayAside ? RavenSpeech.Pace.TalkCut : RavenSpeech.Pace.Talk) * (k > 1 ? Later : 1f); // (the first line read as long as it is, the ones after quicker)
                    }
                    Item it = Add(_talkSaid);
                    if (l.Kind == SayRaven && raven.Count > 0 && k + 1 < t.Lines.Count && t.Lines[k + 1].ToRaven) // (the person answered it: the raven's frame, your answers in it — they come with its words)
                    {
                        TalkSay reply = t.Lines[k + 1];
                        RectTransform inner = TalkRavenFrame(it.R, l);
                        TMP_Text said = TalkRavenBlock(inner, l, true);
                        RectTransform after = Rect("After", inner); VBox(after, 6f, TextAnchor.UpperLeft, 0, 0, 0, 0);
                        TalkBlock(after, reply.Text, _talkWho);
                        T(after, Lang.T("твоя відповідь ворону"), 13f, TMind, F.Italic, TextAlignmentOptions.TopLeft);
                        RectTransform list = Rect("Answers", after); VBox(list, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0);
                        Color ink = l.Munin ? C("#4e3312") : C("#24384f");
                        for (int q = 0; q < raven.Count; q++) TalkLine(list, rows[raven[q]], raven[q], q < raven.Count - 1, 17f, ink, false, Hint);
                        after.gameObject.SetActive(false); // (grows in with the raven's words: Swap)
                        When(it, at, inFor);
                        string w = l.Text;
                        string toRaven = reply.Text;
                        it.SwapAt = Now + S(at + dots); it.Swap = () => { said.text = "«" + w + "»"; after.gameObject.SetActive(true); NpcTalk.LineShown(toRaven); }; // (its word to your raven comes with it: it speaks it)
                        whole = Mathf.Max(whole, at + dots + inFor);
                        at += dots;
                        raven.Clear(); // (answered here, nowhere else)
                        k++; // (its reply is in the frame)
                        continue;
                    }
                    if (l.Kind == SayRaven)
                    {
                        TMP_Text words = TalkRavenBlock(it.R, l, false);
                        When(it, at, inFor);
                        string w = l.Text;
                        it.SwapAt = Now + S(at + dots); it.Swap = () => words.text = "«" + w + "»";
                        whole = Mathf.Max(whole, at + dots + inFor);
                        at += dots;
                        continue;
                    }
                    if (l.Kind == SayAside) { TalkAside(it.R, l); When(it, at, inFor * 2f); whole = Mathf.Max(whole, at + inFor * 2f); continue; }
                    if (l.Kind == SayYou) { T(it.R, "«" + l.Text + "»", 15f, PSoft, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal; When(it, at, inFor); whole = Mathf.Max(whole, at + inFor); continue; }
                    TalkBlock(it.R, l.Text, l.Kind == SayOther ? l.Name : named);
                    whole = Mathf.Max(whole, at + inFor);
                    if (l.Kind == SayOther) { When(it, at, inFor); continue; } // (not the one you talk to: its answers are not under these words; it says them over its head too)
                    string spoken = l.Text; it.Shown = () => NpcTalk.LineShown(spoken); // (its head and chest speak it as it comes in: NpcSpeech)
                    if (t.Menu && k == itsLast && under.Count > 0) // (the first screen: its worry's answers under its last words)
                    {
                        worry = true;
                        RectTransform list = Rect("Answers", it.R);
                        VBox(list, 0f, TextAnchor.UpperLeft, 8, 8, 0, 4);
                        for (int q = 0; q < under.Count; q++) TalkLine(list, rows[under[q]], under[q], q < under.Count - 1, 17f, PInk, false, Hint);
                    }
                    When(it, at, inFor);
                }
                if (TalkNotes.Count > 0) // (what your last answer moved)
                {
                    Item n = Add(_talkSaid);
                    RectTransform notes = Rect("Notes", n.R); // (an item has its own box already: Add)
                    VBox(notes, 2f, TextAnchor.UpperLeft, 8, 8, 2, 4);
                    foreach (KeyValuePair<string, bool> note in TalkNotes) T(notes, note.Key, 15f, note.Value ? TUp : TDown, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
                    TalkNotes.Clear();
                    When(n, at + inFor, inFor);
                }

                // below, at their own pace — from the moment the LAST line said is whole (the user 2026-11-17: «спочатку хоч одна репліка нпс, а потім
                // мої — ніби новий чистий аркуш»; 2026-11-18: after the first one only, your answer stood under Lit's first words while his second
                // was still to come — «дивно виглядає»); a turn with no line — from its start
                float b = whole >= 0f ? Mathf.Max(AnsStart, whole + AnsAfter) : AnsStart;
                if (!worry) for (int q = 0; q < under.Count; q++) { Item a = Add(_talkLow); TalkLine(a.R, rows[under[q]], under[q], q < under.Count - 1, 17f, PInk, false, Hint); When(a, b, AnsFor); b += AnsEvery; }
                if (story.Count > 0) { Item a = Add(_talkLow); TalkStory(a.R, rows, story, Hint); When(a, b, AnsFor); b += AnsEvery; }
                if (t.Scrap != null) { Item a = Add(_talkLow); TalkPinned(a.R, t.Scrap, rows, scrapRows, Hint); When(a, b, AnsFor); b += AnsEvery; }
                if (mind.Count > 0) { Item a = Add(_talkLow); TalkMind(a.R, rows, mind, Hint); When(a, b, AnsFor); b += AnsEvery; }
                if (yours.Count > 0)
                {
                    Item a = Add(_talkLow);
                    if (worry || _talkLow.childCount > 1) Divider(a.R, Lang.T("твоє"));
                    TalkChips(a.R, rows, yours, Hint);
                    When(a, b, AnsFor); b += AnsEvery;
                }
                if (raven.Count > 0) Npcs.Log("talk: answers to the raven with no raven's frame for them (" + raven.Count + ")"); // (NpcTalkRaven makes them only with its words and the reply: never seen)
                if (rows.Exists(r => !string.IsNullOrEmpty(r.Tip)))
                {
                    Item h = Add(_talkLow);
                    hint = T(h.R, "", 14f, PSoft, F.Italic, TextAlignmentOptions.TopLeft);
                    hint.gameObject.AddComponent<LayoutElement>().minHeight = 20f; // (room kept: the scroll does not jump)
                    When(h, 0f, 0f);
                }

                // every piece measured as it will be, then out of sight till its time; the scroll sized for all of it (a long one a little smaller, never off the screen)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_talkScroll);
                foreach (Item i in _items) { i.To = LayoutUtility.GetPreferredHeight(i.R); i.From = 0f; i.Le.preferredHeight = 0f; i.Cg.alpha = 0f; i.Cg.blocksRaycasts = false; }
                float sum = 0f; foreach (Item i in _items) sum += i.To;
                float h0 = LayoutUtility.GetPreferredHeight(_talkScroll), room = 1080f - TalkHang - 20f, fit = h0 > room ? room / h0 : 1f;
                _talkScroll.localScale = Vector3.one * fit;
                if (t.Rel != null) RelMargins(_talkPaper, t.Rel, TalkLeft + TalkW * fit + 8f); // (how it is with you: the margins, RavenBookTalkRel)
                if (TalkInstant) { Item last = null; foreach (Item i in _items) if (i.Shown != null) { if (last != null) last.Shown = null; last = i; } } // (all of it at once: only its last line spoken — not every hum in one frame)
                Update();
            }

            private void Update()
            {
                float now = Now;
                bool moving = false;
                foreach (Item i in _items)
                {
                    if (i.R == null) continue;
                    if (i.SwapAt >= 0f && now >= i.SwapAt && i.Done) // (its words in place of the dots: it grows to them)
                    {
                        i.SwapAt = -1f;
                        float was = Height(i);
                        if (i.Swap != null) i.Swap();
                        i.Le.preferredHeight = -1f;
                        LayoutRebuilder.ForceRebuildLayoutImmediate(i.R);
                        i.From = was; i.To = LayoutUtility.GetPreferredHeight(i.R); i.Le.preferredHeight = was;
                        i.At = now; i.Dur = S(0.2f); i.Done = false; i.Grow = false; i.Mask.enabled = true;
                    }
                    if (i.SwapAt >= 0f) moving = true; // (a raven's dots still to turn into its words)
                    if (i.Done) continue;
                    moving = true;
                    if (now < i.At) continue;
                    if (i.Shown != null) { Action shown = i.Shown; i.Shown = null; shown(); } // (once: the person begins to say it)
                    float k = i.Dur <= 0f ? 1f : Mathf.Clamp01((now - i.At) / i.Dur), e = k * k * (3f - 2f * k);
                    i.Le.preferredHeight = Mathf.Lerp(i.From, i.To, e);
                    i.Cg.alpha = i.Out ? 1f - e : i.Grow ? e : 1f;
                    if (k < 1f) continue;
                    i.Done = true;
                    if (!i.Out) { i.Le.preferredHeight = -1f; i.Cg.alpha = 1f; i.Cg.blocksRaycasts = true; i.Mask.enabled = false; } // (grown: nothing of it clipped any more — the story card's seal sits half over its corner, 2026-11-07)
                }
                if (_next != null && now >= _buildAt) { TalkTurn t = _next; _next = null; Clear(); Build(t); return; }
                if (moving && _talkScroll != null) LayoutRebuilder.MarkLayoutForRebuild(_talkScroll);
                if (!moving && _next == null) enabled = false; // (all of it in place: nothing to look at each frame till the next turn or a click — Next, Rush)
            }
        }

        // the person's words: the dark block, its tail
        private static void TalkBlock(RectTransform parent, string says, string who = null)
        {
            RectTransform block = Rect("Says", parent);
            Image bi = block.gameObject.AddComponent<Image>(); bi.sprite = BookArt.RoundFine(4f); bi.type = Image.Type.Sliced; bi.color = TBlock; bi.raycastTarget = false;
            VBox(block, 0f, TextAnchor.UpperLeft, 16, 16, 11, 12);
            if (!string.IsNullOrEmpty(who)) T(block, "<size=80%><b>" + who.ToUpperInvariant() + "</b></size>", 15f, TBlockInk, F.Body, TextAlignmentOptions.TopLeft); // (another person: its name, as «ТИ» over your words)
            string q = string.IsNullOrEmpty(says) ? "…" : says.TrimStart().StartsWith("«") ? says : "«" + says.Trim() + "»";
            T(block, q, 19f, TBlockInk, F.Body, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
            RectTransform tail = Rect("Tail", parent); Fixed(tail.gameObject, -1f, 9f);
            RectTransform tip = Rect("Tip", tail); tip.anchorMin = tip.anchorMax = new Vector2(0f, 1f); tip.sizeDelta = new Vector2(20f, 20f); tip.anchoredPosition = new Vector2(30f, 0f);
            Image ti = tip.gameObject.AddComponent<Image>(); ti.sprite = BookArt.Diamond; ti.color = TBlock; ti.raycastTarget = false;
        }

        // your raven's frame: its colour down the side, all of it inside (its words, the person's word to it, your answers to it); the inside returned
        private static RectTransform TalkRavenFrame(RectTransform parent, TalkSay l)
        {
            RectTransform frame = Rect("RavenFrame", parent); HBox(frame, 12f, TextAnchor.UpperLeft, 6, 0, 2, 4);
            RectTransform bar = Rect("Bar", frame); Fixed(bar.gameObject, 3f, -1f); bar.GetComponent<LayoutElement>().flexibleHeight = 1f;
            Image bi = bar.gameObject.AddComponent<Image>(); bi.color = l.Munin ? C("#b8853c") : C("#6f93bb"); bi.raycastTarget = false;
            RectTransform inner = Rect("Inner", frame); VBox(inner, 8f, TextAnchor.UpperLeft, 0, 0, 0, 0); Flex(inner.gameObject);
            return inner;
        }

        // your raven aloud: its own block (alone: a little to the right; in its frame: the frame is the indent), its portrait on its corner; its dots, then its words (the TMP returned)
        private static TMP_Text TalkRavenBlock(RectTransform parent, TalkSay l, bool framed)
        {
            Color ink = l.Munin ? TCutMunin : TCutHugin;
            RectTransform wrap = Rect("Raven", parent); VBox(wrap, 0f, TextAnchor.UpperLeft, framed ? 0 : 44, framed ? 0 : 22, framed ? 4 : 22, 2);
            RectTransform box = Rect("Box", wrap);
            Image b = box.gameObject.AddComponent<Image>(); b.sprite = BookArt.RoundFine(4f); b.type = Image.Type.Sliced; b.color = C("#1b212b"); b.raycastTarget = false;
            Outline(box, BookArt.FrameFine(4f, 1f), A(ink, 0.85f));
            VBox(box, 2f, TextAnchor.UpperLeft, 14, 50, 9, 11);
            TMP_Text name = T(box, l.Name.ToUpperInvariant(), 12f, ink, F.Head, TextAlignmentOptions.TopLeft); name.characterSpacing = 14f;
            TMP_Text words = T(box, "• • •", 17f, Color.Lerp(ink, Color.white, 0.45f), F.Italic, TextAlignmentOptions.TopLeft); words.textWrappingMode = TextWrappingModes.Normal;
            RectTransform pic = Rect("Pic", box); pic.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pic.anchorMin = pic.anchorMax = new Vector2(1f, 1f); pic.sizeDelta = new Vector2(46f, 46f); pic.anchoredPosition = new Vector2(-6f, -4f);
            Image pc = pic.gameObject.AddComponent<Image>(); pc.sprite = BookArt.Circle; pc.color = C("#141922"); pc.raycastTarget = false;
            Image rim = Img(pic, "Rim", BookArt.Ring(0.05f), ink);
            Sprite face = BookArt.Portrait(l.Munin);
            if (face != null) { Image f = Img(pic, "Face", face, Color.white); f.preserveAspect = true; Stretch(f.rectTransform, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f)); }
            return words;
        }

        // your raven aside: a thought in the margin, its small picture, «тихо»
        private static void TalkAside(RectTransform parent, TalkSay l)
        {
            Color ink = l.Munin ? TAsideMunin : TAsideHugin, rimc = l.Munin ? C("#a8752c") : C("#5f83ad");
            RectTransform row = Rect("Aside", parent); HBox(row, 9f, TextAnchor.UpperLeft, 36, 0, 2, 4);
            RectTransform bar = Rect("Bar", row); Fixed(bar.gameObject, 2f, -1f); bar.GetComponent<LayoutElement>().flexibleHeight = 1f; bar.gameObject.AddComponent<Image>().color = A(rimc, 0.7f);
            Sprite face = BookArt.Portrait(l.Munin);
            if (face != null) { RectTransform fr = Rect("Face", row); Fixed(fr.gameObject, 24f, 24f); Image f = fr.gameObject.AddComponent<Image>(); f.sprite = face; f.preserveAspect = true; f.raycastTarget = false; }
            RectTransform words = Rect("Words", row); VBox(words, 0f, TextAnchor.UpperLeft, 0, 0, 0, 0); Flex(words.gameObject);
            T(words, Lang.F("{0}, тихо", l.Name), 12f, A(ink, 0.65f), F.Body, TextAlignmentOptions.TopLeft);
            T(words, l.Text, 16.5f, ink, F.Italic, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
        }

        // «на думці»: its lesser worries, a line each
        private static void TalkMind(RectTransform parent, List<TalkRow> rows, List<int> mind, Func<TMP_Text> hint)
        {
            RectTransform m = Rect("Mind", parent);
            VBox(m, 3f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            T(m, Lang.T("на думці"), 13f, TMind, F.Italic, TextAlignmentOptions.TopLeft);
            foreach (int i in mind)
            {
                RectTransform row = Rect("Row", m);
                HBox(row, 8f, TextAnchor.UpperLeft, 0, 0, 2, 2);
                RectTransform bar = Rect("Bar", row); Fixed(bar.gameObject, 2f, -1f); bar.gameObject.AddComponent<Image>().color = A(TMind, 0.35f);
                bar.GetComponent<LayoutElement>().flexibleHeight = 1f;
                TalkLine(row, rows[i], i, false, 15.5f, PInk, true, hint);
            }
        }

        // ---------------------------------------------------------------- its words over the window: you said nothing for too long (NpcTalkIdle)
        // The user 2026-11-05: its words over the parchment, the parchment under them out of focus and its answers not to be clicked — for a few seconds and no more (so a click meant to
        // wave it away does not pick «Бувай»). Esc and the ✕ work as ever: the ✕ is on the roll's seal, not on the parchment.
        //   the limit    _alertUntil, a plain time asked at the click itself (TalkChoose): whatever becomes of the words, the answers come back by themselves
        //   its look     a canvas of its own with nothing in it to click (no raycaster), over the talk's, so the ✕ and the patience strip's tip still take the pointer. Its words: a block of the
        //                talk's own kind, in the middle of the parchment and narrower than it; the parchment under them darkened (AlertDim), its corners rounded as the parchment's
        //   the blur     the parchment's part of the screen, read once a silence (no click: it does not change; ReadPixels into an sRGB texture, so its colours stay as they are on the screen), shrunk and grown back
        private const float AlertInset = 28f, AlertFade = 0.35f, AlertMost = 8f, AlertBlur = 6f, AlertDim = 0.72f, AlertCorner = 10f;
        private static TalkAlertLook _alert;
        private static RectTransform _talkPaper;
        private static float _alertUntil;
        private static string AlertQuote(string says) { return "«" + (says ?? "").Trim() + "»"; }

        internal static void ShowTalkAlert(string who, string says)
        {
            EnsureGameFont();
            _alertUntil = Time.unscaledTime + Mathf.Min(AlertMost, RavenSpeech.Seconds(says, RavenSpeech.Pace.Bubble));
            if (_alert != null && !_alert.Gone) { _alert.Next = says; return; }
            if (_alert != null) UnityEngine.Object.Destroy(_alert.gameObject);
            GameObject root = OverlayCanvas("RavenWatch_TalkAlert", 31001, false);
            RectTransform frame = Rect("Soft", root.transform);
            Image round = frame.gameObject.AddComponent<Image>(); round.sprite = BookArt.RoundFine(AlertCorner); round.type = Image.Type.Sliced; round.raycastTarget = false;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RawImage soft = Rect("Picture", frame).gameObject.AddComponent<RawImage>(); soft.raycastTarget = false; soft.color = new Color(AlertDim, AlertDim, AlertDim, 1f);
            Stretch(soft.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            frame.gameObject.SetActive(false);
            RectTransform box = Rect("Alert", root.transform);
            box.anchorMin = box.anchorMax = Vector2.zero; box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(TalkW - 52f - AlertInset * 2f, 10f);
            Image bg = box.gameObject.AddComponent<Image>(); bg.sprite = BookArt.RoundFine(5f); bg.type = Image.Type.Sliced; bg.color = TBlock; bg.raycastTarget = false;
            UnityEngine.UI.Shadow sh = box.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(0f, -5f);
            VBox(box, 8f, TextAnchor.UpperCenter, 24, 24, 16, 20);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Outline(box, BookArt.FrameFine(5f, 1.5f), A(TStoryEdge, 0.9f));
            TMP_Text name = T(box, who, 15f, A(TStoryEdge, 1f), F.Head, TextAlignmentOptions.Center); name.characterSpacing = 14f; name.raycastTarget = false;
            TMP_Text words = T(box, AlertQuote(says), 28f, TBlockInk, F.Body, TextAlignmentOptions.Center); words.textWrappingMode = TextWrappingModes.Normal; words.raycastTarget = false;
            CanvasGroup group = root.AddComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            _alert = root.AddComponent<TalkAlertLook>();
            _alert.Group = group; _alert.Words = words; _alert.Box = box; _alert.Frame = frame; _alert.Soft = soft;
        }

        internal static void HideTalkAlert() { _alertUntil = 0f; if (_alert != null) _alert.Gone = true; }

        internal sealed class TalkAlertLook : MonoBehaviour
        {
            internal CanvasGroup Group; internal TMP_Text Words; internal RectTransform Box, Frame; internal RawImage Soft; internal string Next; internal bool Gone;
            private bool _ready;
            private RenderTexture _pic;
            private Texture2D _shot;

            private void Start() { StartCoroutine(Take()); }

            // the parchment, once the frame is drawn (its own words not on it yet: all of it still at nothing)
            private System.Collections.IEnumerator Take()
            {
                yield return new WaitForEndOfFrame();
                RenderTexture was = RenderTexture.active;
                try { if (!Gone) Lay(); }
                catch (Exception e) { Debug.LogWarning("[RavenWatch] the talk's blur: " + e.Message); } // (no blur: its words all the same)
                RenderTexture.active = was;
                _ready = true;
            }

            // its words in the middle of the parchment, narrower than it; under them the parchment out of focus
            private void Lay()
            {
                RectTransform paper = _talkPaper;
                if (paper == null) return;
                Vector3[] c = new Vector3[4];
                paper.GetWorldCorners(c); // (an overlay canvas: these are the screen's pixels)
                float scale = Mathf.Max(0.0001f, Box.lossyScale.x), fit = paper.lossyScale.x / scale;
                float x = Mathf.Max(0f, c[0].x), y = Mathf.Max(0f, c[0].y), w = Mathf.Min(c[2].x, Screen.width) - x, h = Mathf.Min(c[2].y, Screen.height) - y;
                if (w < 8f || h < 8f) return;
                Box.sizeDelta = new Vector2((w / scale) - AlertInset * 2f * fit, Box.sizeDelta.y);
                Box.position = new Vector3(x + w * 0.5f, y + h * 0.5f, 0f);
                Blur(x, y, w, h, scale);
            }

            private void Blur(float x, float y, float w, float h, float scale)
            {
                Texture2D shot = new Texture2D(Mathf.RoundToInt(w), Mathf.RoundToInt(h), TextureFormat.RGB24, false);
                RenderTexture.active = null;
                shot.ReadPixels(new Rect(x, y, shot.width, shot.height), 0, 0, false); // heavy: once a long silence in a talk (three minutes with no click at the least), the parchment's part only
                shot.Apply(false);
                shot.filterMode = FilterMode.Bilinear; shot.wrapMode = TextureWrapMode.Clamp;
                _shot = shot; // (let go of even if what follows fails: OnDestroy)
                int small = Mathf.Max(8, Mathf.RoundToInt(w / AlertBlur)), smallH = Mathf.Max(8, Mathf.RoundToInt(h / AlertBlur));
                RenderTexture cur = Temp(shot.width / 2, shot.height / 2);
                Graphics.Blit(shot, cur);
                UnityEngine.Object.Destroy(shot); _shot = null;
                while (cur.width > small) { RenderTexture n = Temp(Mathf.Max(small, cur.width / 2), Mathf.Max(smallH, cur.height / 2)); Graphics.Blit(cur, n); RenderTexture.ReleaseTemporary(cur); cur = n; }
                while (cur.width * 2 < w) { RenderTexture n = Temp(cur.width * 2, cur.height * 2); Graphics.Blit(cur, n); RenderTexture.ReleaseTemporary(cur); cur = n; }
                _pic = new RenderTexture(cur.width, cur.height, 0); _pic.filterMode = FilterMode.Bilinear;
                Graphics.Blit(cur, _pic);
                RenderTexture.ReleaseTemporary(cur);
                Frame.anchorMin = Frame.anchorMax = Frame.pivot = Vector2.zero;
                Frame.position = new Vector3(x, y, 0f);
                Frame.sizeDelta = new Vector2(w / scale, h / scale);
                Soft.texture = _pic;
                Frame.gameObject.SetActive(true);
                Frame.SetAsFirstSibling();
            }

            private static RenderTexture Temp(int w, int h) { RenderTexture t = RenderTexture.GetTemporary(Mathf.Max(8, w), Mathf.Max(8, h), 0); t.filterMode = FilterMode.Bilinear; return t; }

            private void OnDestroy() { if (_pic != null) { _pic.Release(); UnityEngine.Object.Destroy(_pic); } if (_shot != null) UnityEngine.Object.Destroy(_shot); }

            private void Update()
            {
                if (!_ready) return;
                float step = Time.unscaledDeltaTime / AlertFade;
                if (Gone || Next != null || Time.unscaledTime >= _alertUntil)
                {
                    Group.alpha = Mathf.MoveTowards(Group.alpha, 0f, step);
                    if (Group.alpha > 0f) return;
                    if (Gone) { if (_alert == this) _alert = null; UnityEngine.Object.Destroy(gameObject); return; }
                    if (Next != null) { Words.text = AlertQuote(Next); Next = null; LayoutRebuilder.ForceRebuildLayoutImmediate(Box); }
                    return;
                }
                Group.alpha = Mathf.MoveTowards(Group.alpha, 1f, step);
            }
        }

        // one answer as a line (under its words, «ще на думці»): gold under the pointer, grey with its why
        private static void TalkLine(Transform parent, TalkRow r, int at, bool rule, float size, Color ink, bool italic, Func<TMP_Text> hint)
        {
            RectTransform row = Rect("Answer", parent);
            HBox(row, 8f, TextAnchor.MiddleLeft, 4, 4, 6, 6);
            Flex(row.gameObject);
            Color c = r.Off ? A(ink, 0.4f) : ink;
            TMP_Text t = T(row, r.Text, size, c, italic ? F.Italic : F.Body, TextAlignmentOptions.MidlineLeft);
            Flex(t.gameObject);
            if (!string.IsNullOrEmpty(r.Have)) Keep(T(row, r.Have, size * 0.75f, THave, F.Body, TextAlignmentOptions.MidlineRight));
            Image fill = row.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 1f, 1f, 0f);
            if (rule) { RectTransform line = Rect("Rule", parent); Fixed(line.gameObject, -1f, 1f); line.gameObject.AddComponent<Image>().color = A(PRule, 0.45f); }
            TalkHook(row.gameObject, r, at, new Graphic[] { t }, c, fill, hint);
        }

        // what every answer does under the pointer and on a click (its why on the line under the chips; grey: no click, no light)
        private static void TalkHook(GameObject g, TalkRow r, int at, Graphic[] targets, Color normal, Image fill, Func<TMP_Text> hint)
        {
            if (!string.IsNullOrEmpty(r.Tip)) { TalkTipLater t = g.AddComponent<TalkTipLater>(); t.Hint = hint; t.Text = Grammar.Fix(r.Tip); }
            if (r.Off) return;
            if (fill != null) _talkFills[at] = fill;
            Clickable(g, () => TalkChoose(at));
            Hover(g, targets, normal, fill);
        }

        // the line under the chips is made last: its why is looked for when the pointer comes
        internal sealed class TalkTipLater : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal Func<TMP_Text> Hint; internal string Text;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { TMP_Text l = Hint != null ? Hint() : null; if (l != null) l.text = Text; }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { TMP_Text l = Hint != null ? Hint() : null; if (l != null && l.text == Text) l.text = ""; }
        }

        // the story's card: gold, its seal, a diamond by each answer, «веде далі історію»
        private static void TalkStory(Transform paper, List<TalkRow> answers, List<int> rows, Func<TMP_Text> hint)
        {
            RectTransform card = Rect("Story", paper);
            Image ci = card.gameObject.AddComponent<Image>(); ci.sprite = BookArt.RoundFine(4f); ci.type = Image.Type.Sliced; ci.color = A(TStoryFill, 0.85f); ci.raycastTarget = false;
            Outline(card, BookArt.FrameFine(4f, 1.5f), TStoryEdge);
            VBox(card, 4f, TextAnchor.UpperLeft, 16, 30, 12, 12);
            TMP_Text kind = T(card, Lang.T("ІСТОРІЯ"), 14f, TSeal, F.Head, TextAlignmentOptions.TopLeft); kind.characterSpacing = 18f;
            foreach (int i in rows)
            {
                RectTransform row = Rect("Answer", card);
                HBox(row, 10f, TextAnchor.MiddleLeft, 0, 0, 4, 4);
                RectTransform dia = Rect("Diamond", row); Fixed(dia.gameObject, 18f, 18f);
                Image di = Img(dia, "Edge", BookArt.FrameFine(1f, 1.5f), A(TStoryInk, 0.9f)); di.type = Image.Type.Sliced; di.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                TMP_Text t = T(row, answers[i].Text, 21f, TStoryInk, F.Body, TextAlignmentOptions.MidlineLeft);
                Flex(t.gameObject);
                if (!string.IsNullOrEmpty(answers[i].Have)) Keep(T(row, answers[i].Have, 15f, THave, F.Body, TextAlignmentOptions.MidlineRight));
                Image fill = row.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 1f, 1f, 0f);
                TalkHook(row.gameObject, answers[i], i, new Graphic[] { t }, TStoryInk, fill, hint);
            }
            T(card, Lang.T("веде далі історію"), 14f, A(TStoryInk, 0.75f), F.Italic, TextAlignmentOptions.TopLeft);
            Seal(card);
        }

        // a thin frame over a box that lays out its children: drawn on it, not one of its rows
        private static void Outline(RectTransform box, Sprite frame, Color c)
        {
            Image e = Img(box, "Edge", frame, c);
            e.type = Image.Type.Sliced;
            e.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // the story card's seal, half over its corner: red wax with the rune raido pressed in it (the road: «веде далі історію»). A picture
        // (assets/book/talk_wax, tools/talk_top_art.html: its body 46 across in the middle of 56); none in the build: the plain red round, as before
        private const float SealOut = 28f; // how far it reaches past its card's right and top (what a part's frame leaves whole: TalkFlow.Add)
        private static void Seal(RectTransform card)
        {
            RectTransform s = Rect("Seal", card);
            s.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            s.anchorMin = s.anchorMax = new Vector2(1f, 1f); s.sizeDelta = new Vector2(56f, 56f); s.anchoredPosition = new Vector2(-4f, -2f);
            Image si = s.gameObject.AddComponent<Image>(); si.raycastTarget = false;
            si.sprite = BookArt.Picture("talk_wax");
            if (si.sprite == null) { s.sizeDelta = new Vector2(46f, 46f); si.sprite = BookArt.Circle; si.color = TSeal; }
        }

        // a settler's request: a scrap pinned to the scroll, a little askew — the thing, its pay, how long it waits, «ОБІЦЯНО» once promised,
        // its answers (bring it, promise it) as words to click under it
        private static void TalkPinned(Transform paper, TalkScrap s, List<TalkRow> answers, List<int> rows, Func<TMP_Text> hint)
        {
            RectTransform wrap = Rect("Request", paper);
            VBox(wrap, 0f, TextAnchor.UpperLeft, 8, 30, 6, 2);
            RectTransform sc = Rect("Scrap", wrap);
            sc.localRotation = Quaternion.Euler(0f, 0f, 1.4f);
            Image si = sc.gameObject.AddComponent<Image>(); si.sprite = BookArt.TornFill(s.Item.GetStableHashCode()); si.type = Image.Type.Tiled; si.color = TScrap; si.raycastTarget = false;
            UnityEngine.UI.Shadow sh = sc.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0.24f, 0.14f, 0.04f, 0.35f); sh.effectDistance = new Vector2(3f, -5f);
            VBox(sc, 4f, TextAnchor.UpperLeft, 20, 20, 18, 14);
            RectTransform pin = Rect("Pin", sc); pin.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pin.anchorMin = pin.anchorMax = new Vector2(0.5f, 1f); pin.sizeDelta = new Vector2(14f, 14f); pin.anchoredPosition = new Vector2(0f, -6f);
            Image pi = pin.gameObject.AddComponent<Image>(); pi.sprite = BookArt.Circle; pi.color = C("#8c8578"); pi.raycastTarget = false;
            TMP_Text kind = T(sc, Lang.T("ПРОХАННЯ"), 13f, PSoft, F.Head, TextAlignmentOptions.TopLeft); kind.characterSpacing = 16f;
            RectTransform what = Rect("What", sc);
            HBox(what, 12f, TextAnchor.MiddleLeft, 0, 0, 2, 2);
            Sprite icon = ItemIcon(s.Item);
            if (icon != null) { RectTransform ic = Rect("Icon", what); Fixed(ic.gameObject, 38f, 38f); Image ii = ic.gameObject.AddComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false; }
            T(what, s.Words, 22f, PInk, F.Body, TextAlignmentOptions.MidlineLeft).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform foot = Rect("Foot", sc);
            HBox(foot, 10f, TextAnchor.MiddleLeft, 0, 0, 4, 0);
            T(foot, Lang.F("віддячить: {0}", s.Pay), 16f, PSoft, F.Italic, TextAlignmentOptions.MidlineLeft).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Keep(T(foot, Lang.F("ще {0}", s.Left), 16f, TTime, F.Italic, TextAlignmentOptions.MidlineRight));
            foreach (int i in rows) TalkLine(sc, answers[i], i, false, 19f, TStoryInk, false, hint);
            if (!s.Promised) return;
            RectTransform st = Rect("Stamp", sc); st.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            st.anchorMin = st.anchorMax = new Vector2(1f, 1f); st.sizeDelta = new Vector2(120f, 30f); st.anchoredPosition = new Vector2(-74f, -26f); st.localRotation = Quaternion.Euler(0f, 0f, -9f);
            Image sf = Img(st, "Edge", BookArt.FrameFine(2f, 2f), A(TSeal, 0.8f)); sf.type = Image.Type.Sliced;
            TMP_Text stamp = T(st, Lang.T("ОБІЦЯНО"), 15f, A(TSeal, 0.85f), F.Head, TextAlignmentOptions.Center); stamp.characterSpacing = 14f;
            Stretch(stamp.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        // «твоє»: chips that wrap into rows (no layout of Unity's wraps, so rows are laid by their widths here)
        private static void TalkChips(Transform paper, List<TalkRow> answers, List<int> rows, Func<TMP_Text> hint)
        {
            RectTransform box = Rect("Chips", paper);
            VBox(box, 8f, TextAnchor.UpperLeft, 0, 0, 0, 0);
            float room = TalkPaperW - 28f - 60f, used = room + 1f; // (the paper less its sides)
            RectTransform line = null;
            foreach (int i in rows)
            {
                TalkRow r = answers[i];
                Color ink = r.Off ? A(PInk, 0.4f) : PInk;
                Image fill;
                TMP_Text t = Chip(box, r.Text, 18f, ink, r.Off ? 0.35f : 0.8f, r.Off ? 0.05f : 0.3f, room, ref line, ref used, out fill);
                TalkHook(fill.gameObject, r, i, new Graphic[] { t }, ink, fill, hint);
            }
        }

        // a chip in rows that wrap (no layout of Unity's wraps: rows are laid by their widths here): the talk's «твоє», a sheet's words
        private static TMP_Text Chip(RectTransform box, string text, float size, Color ink, float edge, float back, float room, ref RectTransform line, ref float used, out Image fill)
        {
            RectTransform chip = Rect("Chip", box);
            fill = chip.gameObject.AddComponent<Image>(); fill.sprite = BookArt.RoundFine(3f); fill.type = Image.Type.Sliced; fill.color = new Color(1f, 0.98f, 0.92f, back);
            Outline(chip, BookArt.FrameFine(3f, 1f), A(PRule, edge));
            HBox(chip, 0f, TextAnchor.MiddleCenter, 11, 11, 6, 6);
            TMP_Text t = T(chip, text, size, ink, F.Body, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.Normal;
            float w = Mathf.Min(room, t.GetPreferredValues(t.text, room - 22f, 0f).x + 24f);
            if (line == null || used + 8f + w > room) { line = Rect("Line", box); HBox(line, 8f, TextAnchor.UpperLeft, 0, 0, 0, 0); used = -8f; }
            chip.SetParent(line, false);
            Fixed(chip.gameObject, w, -1f);
            used += 8f + w;
            return t;
        }

        // a part's name between the parts: a thin rule, small words in it
        private static void Divider(RectTransform paper, string words)
        {
            RectTransform d = Rect("Part", paper);
            HBox(d, 8f, TextAnchor.MiddleCenter, 0, 0, 2, 0);
            RectTransform l = Rect("Rule", d); Fixed(l.gameObject, -1f, 1f); l.gameObject.AddComponent<Image>().color = A(PRule, 0.5f); Flex(l.gameObject);
            TMP_Text t = T(d, words, 15f, A(PLabel, 0.9f), F.Italic, TextAlignmentOptions.Midline);
            Keep(t);
            RectTransform r = Rect("Rule", d); Fixed(r.gameObject, -1f, 1f); r.gameObject.AddComponent<Image>().color = A(PRule, 0.5f); Flex(r.gameObject);
        }

        internal sealed class ShowWhilePointed : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal GameObject Box;
            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (Box != null) { Box.SetActive(true); Box.transform.SetAsLastSibling(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Box.transform); } }
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (Box != null) Box.SetActive(false); }
        }

        internal static void HideTalk()
        {
            if (_talkRoot != null) UnityEngine.Object.Destroy(_talkRoot);
            _talkRoot = null; _talkPick = null; _talkClose = null; _talkFlow = null; _talkWho = null; _talkFills.Clear();
            _talkPaper = null; _talkSaid = null; _talkLow = null; _talkScroll = null; // (the gone window's parts: nothing keeps them, nothing builds into them)
        }

        private static void TalkChoose(int i)
        {
            if (_talkFlow != null && _talkFlow.Folding) return; // (the last pick still folding away)
            if (Time.unscaledTime < _alertUntil) { NpcTalk.IdleStir(); return; } // (its words are up: not to be clicked for this little while — you are here, though)
            Image lit; if (_talkFills.TryGetValue(i, out lit) && lit != null) lit.color = new Color(1f, 0.75f, 0.31f, 0.5f); // (the one you picked, a moment before the rest goes)
            Action<int> p = _talkPick;
            if (p != null) p(i);
        }

        private static void TalkClose() { Action c = _talkClose; if (c != null) c(); else HideTalk(); }

        // while it is open: Esc closes (no keys for the answers: the mouse only — the user 2026-11-05); it closes when you walk off (NpcTalk.Tick)
        internal sealed class TalkKeys : MonoBehaviour
        {
            private void Update()
            {
                NpcTalk.Tick();
                if (_talkRoot != null && Input.GetKeyDown(KeyCode.Escape)) TalkClose();
            }
        }
    }
}
#endif

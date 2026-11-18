#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Сага»: the story's progress (the user 2026-11-05) and what one knows of its world (2026-11-12). Its look is the user's design
    // «Сага, напрям B — рядок рун» (2026-11-16, Downloads\saga-design.zip: frames 1300×780 = the book's chrome; a book unit = a design px
    // less 50 each way). The page background is the book's own, untouched; on it only the book's pieces: drop caps, torn cards (Torn),
    // a sheet pinned with corners (Pinned), round medallions with a rune, rules with ᛟ, markers hanging in the margin.
    //   The row of sections over the left page (not scrolling): Літопис ᛚ · Люди ᛗ · Місця ᛟ · Знання ᚨ · Загадки ᛈ (StorySaga.Sections'
    //                        runes), the open one in ink with its drop cap and a gold brush under it, the others gold ink, ᛟ between them;
    //                        each in a place of its own that a click never moves, the name only grows in it (the user 2026-11-18).
    //   «Літопис»            the chronicle, a chapter at a time (the user 2026-11-17: three acts, chapters in them): the acts in a row over the
    //                        left page (the prologue by its name), the shown act's chapters in a row at its bottom — each row once there
    //                        are two in the book: with something in them, or their title shown on his screen (StorySaga.Pages;
    //                        SagaActs, SagaActChapters). The left page — the world: what
    //                        happened here for everyone (◆, the day and the names of the players who saw it); what this player neither
    //                        saw nor was told stays a rumour on a torn card (a person telling it lifts it); a row of runes under the
    //                        chapter's name says how much of it has happened. The right page — his own: what to do now on a pinned sheet
    //                        (the goal line, «Показати на мапі»; a sheet a quest he stands in, the chapter's own first) and what he has
    //                        passed in this chapter (◇), in the order lived;
    //   «Люди», «Місця», «Знання»   what one knows (StorySaga.Sections): on the left its entries by chapter, the open one on a torn card with
    //                        its section's medallion; on the right that one — its name as known now, a word about it, what is known, the
    //                        words read there in a double frame, the way to the map; under them, who may tell more and that something of it
    //                        nobody can be asked now (StorySaga.MoreOf);
    //   «Загадки»            the same spread for what is not understood yet: open ones (a dashed seal) under «Відкрито», answered ones
    //                        (a dark seal) under «Розгадано»; on the right its clues numbered in the author's order and, once known, its
    //                        answer on a pinned sheet.
    // Everything comes from StorySaga; nothing is put together here. A gold dot: a section with something new, an entry not read as it
    // is now. Read once drawn: the chronicle's chapter shown (StorySaga.Seen — a chapter not shown keeps its dot beside its name in the row),
    // the entry open on the right (StorySaga.SeenEntry).
    // Not taken from the design (nothing keeps it): the day a riddle was solved (its list says how many clues), a clue that is a rumour,
    // a «new» dot on a line of one's own way, the runes under a quote, the thin scrollbar (the book's columns have their own arrow).
    internal static partial class RavenBook
    {
        private static int _sagaPart;                   // session: 0 the chronicle, 1… a section of what one knows
        private static string _sagaChapter = "";        // session: the chapter the chronicle shows («»: one with something new, else the latest with an entry)
        private static bool _sagaClicked;               // session: _sagaChapter is the player's own pick (kept); else the book's, kept only while it is open (SagaClosed)
        private static readonly Dictionary<string, string> _sagaEntry = new Dictionary<string, string>(); // session: the entry open in each section

        // the design's measures, in book units: the row of sections, the columns (a margin of SagaPad on their left for the markers and
        // the cards that stand out of the words)
        private const float SagaRowX = 56f, SagaRowTop = 44f, SagaRowW = 500f, SagaRowH = 34f;
        private const float SagaLeftX = 30f, SagaRightX = 618f, SagaColW = 530f, SagaLeftTop = 84f, SagaRightTop = 40f;
        private const int SagaPad = 26;
        private const float SealK = 0.065f; // (the open seal's dashed rim as a share of its size: one picture for every size, ~1.5 px on 22–32)
        private const string SagaCapHex = "#94681c"; // (BookArt.Initial: the drop caps). The medallions' colours are the margin rail's (RavenBookRail.cs: MedalFace, MedalRim)
        private const string Futhark = "ᚠᚢᚦᚨᚱᚲᚷᚹᚺᚾᛁᛃᛇᛈᛉᛊᛏᛒᛖᛗᛚᛜᛞᛟ"; // (the chapter's moments, one rune each, in the row's order)
        private static readonly Color SealDark = C("#2e2014"), SealDarkRim = C("#a8843a"),
            SagaCard = new Color(0.98f, 0.929f, 0.812f, 0.8f), SagaCorner = C("#e9d5a6");

        private static void PageSaga()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return;
            if (_sagaPart < 0 || _sagaPart > StorySaga.Sections.Length) _sagaPart = 0;
            if (_sagaPart == 0)
            {
                List<StorySaga.Page> pages = StorySaga.Pages(p);
                StorySaga.Page pg = pages.Count == 0 ? null : pages.Find(x => x.Chapter.Id == _sagaChapter) ?? pages.Find(x => StorySaga.FreshChapter(p, x.Chapter.Id)) ?? pages[pages.Count - 1]; // (the one picked; else one with something new; else the latest)
                if (pg != null) { _sagaChapter = pg.Chapter.Id; StorySaga.Seen(p, pg.Chapter.Id); } // (the chapter shown, not all of them. Kept: the page does not jump to another chapter as it is drawn again)
                List<StorySaga.Page> ofAct = pg == null ? new List<StorySaga.Page>() : pages.FindAll(x => SagaActOf(x) == SagaActOf(pg));
                RectTransform left = SagaColumn("Left", false, 14f, ofAct.Count > 1 ? SagaChapRowH : 0f), right = SagaColumn("Right", true, 16f);
                SagaRibbons(p); // (after Seen: the dots of the row are as the page leaves them)
                if (ofAct.Count > 1) SagaActChapters(p, ofAct, pg);
                if (pg == null)
                {
                    SagaCaps(T(left, Lang.T("Літопис"), 12f, Caption), 12f);
                    T(left, Lang.T("У світі ще нічого не сталося."), 16f, Caption, F.Italic);
                    T(left, "ᛟ", 13f, GoldInk, F.Rune);
                }
                else
                {
                    SagaActs(left, p, pages, pg);
                    SagaWorld(left, pg);
                }
                SagaOwn(right, p, pg);
                return;
            }
            StorySaga.Section sec = StorySaga.Sections[_sagaPart - 1];
            List<StorySaga.Known> known = StorySaga.KnownOf(p, sec.Word);
            string open;
            _sagaEntry.TryGetValue(sec.Word, out open);
            StorySaga.Known shown = known.Find(k => k.Entry.Id == open) ?? known.Find(k => k.Fresh) ?? (known.Count > 0 ? known[0] : null); // (the one picked; else a new one; else the first)
            if (shown != null) { _sagaEntry[sec.Word] = shown.Entry.Id; StorySaga.SeenEntry(p, shown); shown.Fresh = false; }
            SagaRibbons(p); // (after SeenEntry: as above)
            SagaList(SagaColumn("Left", false, 8f), sec, known, shown);
            if (shown != null) SagaEntry(SagaColumn("Entry_" + shown.Entry.Id, true, 12f), sec, shown); // (a column of its own name: another entry opens at its top, the same one drawn again stays where it was scrolled)
        }

        // the book shut: a chapter it picked itself is picked anew next time (a new chapter begun meanwhile opens on its page); one the player clicked stays
        internal static void SagaClosed() { if (!_sagaClicked) _sagaChapter = ""; }

        // a column of the design: the book's own (it scrolls, its arrow), its margin on the left; spare: so much of the page's bottom left
        // for what stands under it (the row of an act's chapters)
        private static RectTransform SagaColumn(string name, bool right, float spacing, float spare = 0f)
        {
            RectTransform c = Column(name, right ? SagaRightX : SagaLeftX, right ? SagaRightTop : SagaLeftTop, SagaColW, spacing, PageBottom - spare);
            VerticalLayoutGroup v = c.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(SagaPad, 4, 6, v.padding.bottom);
            return c;
        }

        // ---- the row of sections: the chronicle and the sections of what one knows, ᛟ between them. Each has its own place that never moves
        // (the user 2026-11-18: the other tabs must not jump): its width is the widest of its looks — open, shut, the brush —, its name in its
        // middle, the «new» dot hangs out of it into the gap; a click only makes the name grow where it stands (SagaGrow), the one left shrinks back
        private static int _sagaPartDrawn = -1;         // session: the part the row was drawn with last (another now: a click, the names ease)
        private const float SagaPartOn = 19f, SagaPartOff = 17f, SagaPartCap = 24f, SagaBrushW = 84f, SagaGrowFor = 0.18f;

        private static void SagaRibbons(Player p)
        {
            RectTransform row = Rect("SagaParts", _pages);
            At(row, SagaRowX, SagaRowTop, SagaRowW, SagaRowH);
            HBox(row, 0f, TextAnchor.UpperLeft);
            SagaRibbon(row, Lang.T("Літопис"), 0, StorySaga.FreshChronicle(p));
            for (int i = 0; i < StorySaga.Sections.Length; i++)
            {
                Spring(row);
                TMP_Text rune = T(row, "ᛟ", 11f, A(GoldInk, 0.7f), F.Rune, TextAlignmentOptions.Bottom);
                rune.textWrappingMode = TextWrappingModes.NoWrap;
                Fixed(rune.gameObject, -1f, 26f);
                Spring(row);
                SagaRibbon(row, Lang.T(StorySaga.Sections[i].Name), i + 1, StorySaga.FreshIn(p, StorySaga.Sections[i].Word));
            }
            _sagaPartDrawn = _sagaPart;
        }

        private static void SagaRibbon(RectTransform row, string name, int part, bool fresh)
        {
            RectTransform b = Rect("Part" + part, row);
            bool on = part == _sagaPart, eased = _sagaPartDrawn >= 0 && _sagaPartDrawn != _sagaPart && (on || part == _sagaPartDrawn);
            VerticalLayoutGroup v = VBox(b, -2f, TextAnchor.UpperCenter);
            v.childForceExpandWidth = false;
            string open = SagaCap(name, SagaPartCap);
            TMP_Text t = T(b, on ? open : name, on ? SagaPartOn : SagaPartOff, on ? Ink : GoldInk, F.Head, TextAlignmentOptions.Bottom);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.pivot = new Vector2(0.5f, 0f); // (it grows from the middle of its foot: the line of names stays)
            Fixed(t.gameObject, -1f, on ? 28f : 26f);
            Fixed(b.gameObject, Mathf.Max(SagaBrushW, SagaWide(t, open, SagaPartOn), SagaWide(t, name, SagaPartOff)), -1f);
            if (on) // the open one: in ink, its drop cap, the gold brush under it
            {
                Image brush = Img(b, "Brush", BookArt.Brush("brush-gold"), new Color(1f, 1f, 1f, 0.75f));
                brush.type = Image.Type.Sliced; brush.pixelsPerUnitMultiplier = 4f; // (its ragged ends kept small: the stroke is short)
                Fixed(brush.gameObject, SagaBrushW, 7f);
                if (eased) t.gameObject.AddComponent<SagaGrow>().Go(SagaPartOff / SagaPartOn, brush.rectTransform);
                return;
            }
            if (eased) t.gameObject.AddComponent<SagaGrow>().Go(SagaPartOn / SagaPartOff, null);
            if (fresh) NewDot(t.rectTransform, 11f, 12f, -4f); // (beside the name, out of the layout: its place is the same with a dot and without)
            Clickable(b.gameObject, () => { _sagaPart = part; Show(_tab, true); });
            Hover(b.gameObject, new Graphic[] { t }, t.color, null);
        }

        // how wide a text is in this size (a measure only: the text is left as it was)
        private static float SagaWide(TMP_Text t, string s, float size)
        {
            float was = t.fontSize;
            t.fontSize = size;
            float w = t.GetPreferredValues(s).x;
            t.fontSize = was;
            return w;
        }

        // a section's name eases to its new size on a click (from: its old size as a share of the new one), the open one's brush spreads
        // from its middle; once, for SagaGrowFor seconds, then it stops
        internal sealed class SagaGrow : MonoBehaviour
        {
            private float _from, _at; private RectTransform _brush;
            internal void Go(float from, RectTransform brush) { _from = from; _brush = brush; _at = Time.unscaledTime; Update(); }
            private void Update()
            {
                float k = Mathf.Clamp01((Time.unscaledTime - _at) / SagaGrowFor), e = k * k * (3f - 2f * k);
                transform.localScale = Vector3.one * Mathf.Lerp(_from, 1f, e);
                if (_brush != null) _brush.localScale = new Vector3(e, 1f, 1f);
                if (k >= 1f) enabled = false;
            }
        }

        // ---- the chronicle (the user 2026-11-17: three acts, chapters in them): over the page a row of the acts — the prologue by its own name —,
        // at its bottom a row of the shown act's chapters; in each only what has something in it or whose title has come up (StorySaga.Pages; spoilers), and no row for one
        private const float SagaChapRowH = 34f; // (the row of chapters at the left page's bottom: the column ends over it)

        // the act a page is of — its group in the upper row («ch:<id>»: a chapter that stands alone, the prologue)
        private static string SagaActOf(StorySaga.Page pg) { return pg.Chapter.Act != null ? "act:" + pg.Chapter.Act.Id : "ch:" + pg.Chapter.Id; }

        // the acts with something in them, in a row: the one shown in ink, the others to click (it opens on its chapter with something new,
        // else its latest), one with something new in a chapter with its dot
        private static void SagaActs(RectTransform col, Player p, List<StorySaga.Page> pages, StorySaga.Page shown)
        {
            List<string> acts = new List<string>();
            foreach (StorySaga.Page pg in pages) if (!acts.Contains(SagaActOf(pg))) acts.Add(SagaActOf(pg));
            if (acts.Count < 2) return;
            RectTransform row = Rect("Acts", col);
            HBox(row, 8f, TextAnchor.LowerLeft);
            for (int i = 0; i < acts.Count; i++)
            {
                List<StorySaga.Page> of = pages.FindAll(x => SagaActOf(x) == acts[i]);
                StorySaga.Chapter first = of[0].Chapter;
                string name = Lang.T(first.Act == null ? first.Name : first.Act.Label.Length > 0 ? first.Act.Label : first.Act.Name);
                bool on = acts[i] == SagaActOf(shown);
                if (i > 0) T(row, "·", 14f, Caption).textWrappingMode = TextWrappingModes.NoWrap;
                TMP_Text t = T(row, on ? name : SagaLink(name), 14f, on ? Ink : GoldInk);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (on) continue;
                StorySaga.Page open = of.Find(x => StorySaga.FreshChapter(p, x.Chapter.Id)) ?? of[of.Count - 1];
                string id = open.Chapter.Id;
                Clickable(t.gameObject, () => { _sagaChapter = id; _sagaClicked = true; Show(_tab, true); });
                Hover(t.gameObject, new Graphic[] { t }, t.color, null);
                if (of.Exists(x => StorySaga.FreshChapter(p, x.Chapter.Id))) NewDotInline(row); // (something new in a chapter of an act not shown: the dot of the lists beside its name)
            }
            Spring(row);
        }

        // the shown act's chapters with something in them, in a row at the bottom of the left page (not scrolling): the one shown in ink,
        // the others to click, one with something new with its dot. By their names, never «Глава N» (the user 2026-11-17)
        private static void SagaActChapters(Player p, List<StorySaga.Page> of, StorySaga.Page shown)
        {
            RectTransform row = Rect("ActChapters", _pages);
            At(row, SagaLeftX + SagaPad, PageBottom - SagaChapRowH + 6f, SagaColW - SagaPad - 4f, SagaChapRowH - 10f);
            HBox(row, 8f, TextAnchor.MiddleLeft);
            T(row, "ᛟ", 12f, A(GoldInk, 0.7f), F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
            for (int i = 0; i < of.Count; i++)
            {
                StorySaga.Page pg = of[i];
                string id = pg.Chapter.Id;
                bool on = pg == shown;
                if (i > 0) T(row, "·", 14f, Caption).textWrappingMode = TextWrappingModes.NoWrap;
                TMP_Text t = T(row, on ? Lang.T(pg.Chapter.Name) : SagaLink(Lang.T(pg.Chapter.Name)), 14f, on ? Ink : GoldInk);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (on) continue;
                Clickable(t.gameObject, () => { _sagaChapter = id; _sagaClicked = true; Show(_tab, true); });
                Hover(t.gameObject, new Graphic[] { t }, t.color, null);
                if (StorySaga.FreshChapter(p, id)) NewDotInline(row); // (something new in a chapter not shown: the dot of the lists beside its name)
            }
            Spring(row);
        }

        // left: the world, in this chapter. Its head as its title on the screen (StorySaga.Title): the act's label small, the act's name big and
        // the chapter's under it 4 px smaller; an act with no name yet — the chapter's name big
        private static void SagaWorld(RectTransform left, StorySaga.Page pg)
        {
            RectTransform head = Rect("Head", left);
            VBox(head, 0f, TextAnchor.UpperLeft);
            StorySaga.Act act = pg.Chapter.Act;
            bool actNamed = act != null && act.Name.Length > 0;
            if (act != null && act.Label.Length > 0) { SagaCaps(T(head, Lang.T(act.Label), 12f, Caption), 12f); SagaGap(head, 2f); }
            T(head, SagaCap(Lang.T(actNamed ? act.Name : pg.Chapter.Name), 42f), 30f, Ink, F.Head);
            if (actNamed) T(head, Lang.T(pg.Chapter.Name), 26f, Ink, F.Head);
            if (pg.Chapter.Lands.Length > 0) { SagaGap(head, 6f); T(head, Lang.T(pg.Chapter.Lands), 14f, Brown); }
            if (pg.Of > 0) { SagaGap(head, 8f); SagaProgress(head, pg); }
            if (pg.Closed)
            {
                SagaGap(head, 10f);
                RectTransform done = Rect("Closed", head);
                HBox(done, 10f, TextAnchor.MiddleLeft);
                SagaMedal(done, 22f, "ᛟ", 0);
                SagaCaps(T(done, Lang.T("главу закінчено"), 13f, Caption, F.Head), 16f).textWrappingMode = TextWrappingModes.NoWrap;
                SagaLine(done, 0.3f);
                if (pg.Chapter.Closing.Length > 0) { SagaGap(head, 8f); T(head, Lang.T(pg.Chapter.Closing), 16f, InkSoft, F.Italic); }
            }
            if (pg.Chapter.Epigraph.Length > 0) { SagaGap(head, 10f); T(head, Lang.T(pg.Chapter.Epigraph), 16f, InkSoft, F.Italic); }
            SagaRule(left);
            if (pg.World.Count == 0) T(left, Lang.T("У світі ще нічого не сталося."), 16f, Caption, F.Italic);
            int rumours = 0;
            foreach (StoryTales.Told t in pg.World)
            {
                if (t.Known)
                {
                    RectTransform ev = Rect("Event", left);
                    VBox(ev, 3f, TextAnchor.UpperLeft);
                    SagaMark(ev, "◆");
                    T(ev, Lang.T(t.Saga), 16f, Ink);
                    string who = Witnesses(t), day = SagaDay(t.Moment);
                    T(ev, who.Length > 0 ? day + " · " + Lang.F("Свідки: {0}", who) : day, 13f, Caption, F.Italic);
                    continue;
                }
                // a rumour: a torn card, a little askew, its «Чутка» to hover
                RectTransform card = Rect("Rumor" + rumours, left);
                card.localRotation = Quaternion.Euler(0f, 0f, rumours++ % 2 == 0 ? -0.6f : 0.5f);
                Torn(card, SagaCard, BookArt.CardLine, 1f);
                VBox(card, 3f, TextAnchor.UpperLeft, 24, 24, 16, 17);
                RectTransform top = Rect("Top", card);
                HBox(top, 0f, TextAnchor.LowerLeft);
                T(top, "ᚹ ᛁ ᚾ ᛞ", 11f, A(GoldInk, 0.6f), F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
                Spring(top);
                TMP_Text hint = T(top, Lang.T("Чутка"), 13f, GoldInk, F.Head);
                hint.textWrappingMode = TextWrappingModes.NoWrap;
                Tip(hint.gameObject, () => Lang.T("Ти цього не бачи{в|ла}. Поговори з людьми: хтось, може, бачив сам і розповість."));
                T(card, Lang.T(t.Rumor.Length > 0 ? t.Rumor : "Кажуть, у світі щось сталося."), 15f, Brown, F.Italic);
            }
        }

        // «день 12»: the world's day of a moment kept in its seconds (as the game counts its days)
        private static string SagaDay(int moment)
        {
            float day = EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f;
            return Lang.F("день {0}", Mathf.FloorToInt(moment / day));
        }

        // the chapter's moments of the world: a rune each, gold ink once it happened, faint before; how many, beside them and under the cursor
        private static void SagaProgress(RectTransform col, StorySaga.Page pg)
        {
            RectTransform row = Rect("Progress", col);
            HBox(row, 10f, TextAnchor.MiddleLeft);
            RectTransform runes = Rect("Runes", row);
            HBox(runes, 7f, TextAnchor.MiddleLeft);
            for (int i = 0; i < pg.Of; i++)
            {
                TMP_Text n = T(runes, Futhark[i % Futhark.Length].ToString(), 15f, i < pg.Happened ? GoldInk : A(GoldInk, 0.28f), F.Rune);
                n.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Tip(runes.gameObject, () => Lang.F("<b>{0}</b>\nУ світі сталося {1} з {2} подій цієї глави.", Lang.T(pg.Chapter.Name), pg.Happened, pg.Of));
            T(row, Lang.F("сталося {0} з {1}", pg.Happened, Lang.N(pg.Of, "події|подій|подій")), 13f, Caption, F.Italic).textWrappingMode = TextWrappingModes.NoWrap;
            Spring(row);
        }

        // «ти, Оля, Торвін»: you first; the world keeps names in lower case (its keys)
        private static string Witnesses(StoryTales.Told t)
        {
            List<string> l = new List<string>();
            if (t.You) l.Add(Lang.T("ти"));
            foreach (string w in t.Witnesses) l.Add(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w));
            return l.Count > 0 ? Few(l) : "";
        }

        // right: your own way — what to do now (whatever chapter is shown) on a pinned sheet, then what you passed in this chapter
        private static void SagaOwn(RectTransform right, Player p, StorySaga.Page pg)
        {
            StoryQuests.Standing[] now = StoryQuests.NowAll(p).ToArray(); // (a card a quest he stands in: the chapter's own, then the one beside it — the user 2026-11-17)
            for (int i = 0; i < now.Length; i++)
            {
                StoryQuests.QuestDef q = now[i].Quest; StoryQuests.StageDef s = now[i].Stage;
                if (i > 0) SagaGap(right, 6f);
                RectTransform sheet = Rect("Now" + i, right);
                Pinned(sheet, SagaCard, SagaCorner, q.Id + "." + s.Id);
                VBox(sheet, 6f, TextAnchor.UpperLeft, 22, 22, 16, 14);
                RectTransform top = Rect("Top", sheet);
                HBox(top, 8f, TextAnchor.MiddleLeft);
                TMP_Text what = SagaCaps(T(top, Lang.T("Зараз") + " · " + Lang.T(q.Name), 12f, Caption), 12f);
                what.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                SagaMedal(top, 22f, StorySaga.ChronicleRune, 0);
                T(sheet, SagaCap(StoryQuests.GoalText(s), 34f), 26f, Ink, F.Head);
                if (s.More.Length > 0) T(sheet, Lang.T(s.More), 15f, InkSoft, F.Italic); // (its longer word: the goal is short on the screen, the book says why — stage «more:»)
                Vector3 at;
                if (StoryQuests.MarkAt(q, s, out at)) { SagaGap(sheet, 2f); SagaMap(sheet, at); }
            }
            if (now.Length == 0)
            {
                SagaHeading(right, Lang.T("Зараз"), false);
                T(right, Lang.T("Зараз сюжет нічого від тебе не чекає."), 16f, Caption, F.Italic);
            }
            SagaGap(right, 8f);
            SagaHeading(right, Lang.T("Мій шлях"), true);
            if (pg == null || pg.Own.Count == 0) { T(right, Lang.T("Ще нічого не записано."), 16f, Caption, F.Italic); return; }
            RectTransform list = Rect("Way", right);
            VBox(list, 10f, TextAnchor.UpperLeft);
            foreach (string e in pg.Own) SagaMark(T(list, Lang.T(e), 16f, Ink).rectTransform, "◇");
        }

        // «ᛟ Показати на мапі»: the book shut, the map open there
        private static void SagaMap(RectTransform col, Vector3 at)
        {
            RectTransform row = Rect("Map", col);
            HBox(row, 6f, TextAnchor.MiddleLeft);
            T(row, "ᛟ", 12f, GoldInk, F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text map = T(row, SagaLink(Lang.T("Показати на мапі")), 14f, GoldInk);
            map.textWrappingMode = TextWrappingModes.NoWrap;
            Spring(row);
            Clickable(map.gameObject, () => { Close(); if (Minimap.instance != null) Minimap.instance.ShowPointOnMap(at); });
            Tip(map.gameObject, () => Lang.T("Закриє книгу й відкриє мапу там, де це місце."));
            Hover(map.gameObject, new Graphic[] { map }, map.color, null);
        }

        // ---- what one knows
        // left: a section's entries — by chapter, or for riddles open ones over the answered; the open one on a torn card, a new one with its dot
        private static void SagaList(RectTransform col, StorySaga.Section sec, List<StorySaga.Known> known, StorySaga.Known shown)
        {
            if (known.Count == 0) { T(col, Lang.T(sec.None), 16f, Caption, F.Italic); return; }
            string chapter = null;
            int heads = 0; // (0 none yet, 1 «Відкрито», 2 «Розгадано»: KnownOf puts the answered after the open)
            foreach (StorySaga.Known k in known)
            {
                if (sec.Closes)
                {
                    int want = k.Closed ? 2 : 1;
                    if (heads != want) { heads = want; SagaSubhead(col, Lang.T(k.Closed ? "Розгадано" : "Відкрито")); }
                }
                else if (k.Entry.Chapter != chapter) { chapter = k.Entry.Chapter; SagaSubhead(col, Lang.T(StorySaga.ChapterName(chapter))); }
                SagaListRow(col, sec, k, k == shown);
            }
        }

        private static void SagaListRow(RectTransform col, StorySaga.Section sec, StorySaga.Known k, bool on)
        {
            string id = k.Entry.Id;
            RectTransform row = Rect("Entry", col);
            HBox(row, 12f, TextAnchor.UpperLeft, 0, 0, on ? 14 : 2, on ? 14 : 2);
            if (on) // the torn card behind it, standing out on the left, short of the right edge; its section's medallion at its right
            {
                RectTransform card = Rect("Card", row);
                card.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                Stretch(card, Vector2.zero, Vector2.one, new Vector2(-22f, 0f), new Vector2(-120f, 0f));
                Torn(card, SagaCard, BookArt.CardLine, 1f);
                if (!sec.Closes)
                {
                    RectTransform medal = SagaMedal(row, 20f, sec.Rune, 0);
                    medal.GetComponent<LayoutElement>().ignoreLayout = true;
                    medal.anchorMin = medal.anchorMax = new Vector2(1f, 0.5f); medal.pivot = new Vector2(0.5f, 0.5f);
                    medal.anchoredPosition = new Vector2(-146f, 0f);
                }
            }
            if (sec.Closes) SagaSeal(row, 24f, k.Closed);
            RectTransform words = Rect("Words", row);
            VBox(words, 0f, TextAnchor.UpperLeft);
            words.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform line = Rect("Name", words);
            HBox(line, 0f, TextAnchor.UpperLeft);
            string name = Lang.T(k.Name);
            TMP_Text n = T(line, on ? SagaCapColour(name) : name, 19f, sec.Closes && k.Closed ? Caption : Ink, F.Head);
            if (k.Fresh) NewDotInline(line);
            Spring(line);
            string under = sec.Closes ? Lang.N(k.Lines.Count, "зачіпка|зачіпки|зачіпок") : k.About.Length > 0 ? Lang.T(k.About) : "";
            if (under.Length > 0) T(words, under, 14f, Brown, F.Italic);
            if (on) return;
            Clickable(row.gameObject, () => { _sagaEntry[sec.Word] = id; Show(_tab, true); });
            Hover(row.gameObject, new Graphic[] { n }, n.color, null); // (the name turns gold under the pointer: the row itself has nothing to tint)
        }

        // right: the open entry, as known now
        private static void SagaEntry(RectTransform col, StorySaga.Section sec, StorySaga.Known k)
        {
            bool riddle = sec.Closes;
            RectTransform head = Rect("Head", col);
            HBox(head, 10f, TextAnchor.UpperLeft);
            RectTransform title = Rect("Title", head);
            VBox(title, 4f, TextAnchor.UpperLeft);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            T(title, SagaCap(Lang.T(k.Name), riddle ? 46f : 50f), riddle ? 32f : 36f, Ink, F.Head);
            if (k.About.Length > 0) T(title, Lang.T(k.About), 16f, riddle ? InkSoft : Brown, F.Italic);
            if (riddle) SagaSeal(head, 32f, k.Closed); else SagaMedal(head, 30f, sec.Rune, 0);
            if (riddle)
            {
                SagaSubhead(col, Lang.T("Зачіпки"));
                for (int i = 0; i < k.Lines.Count; i++)
                {
                    RectTransform clue = Rect("Clue", col);
                    HBox(clue, 12f, TextAnchor.UpperLeft);
                    SagaMedal(clue, 22f, (i + 1).ToString(CultureInfo.InvariantCulture), 0, F.Head);
                    T(clue, Lang.T(k.Lines[i]), 16f, Ink).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                }
                if (!k.Closed) // more to find: a faint empty ring
                {
                    RectTransform rest = Rect("Rest", col);
                    HBox(rest, 12f, TextAnchor.MiddleLeft);
                    SagaSeal(rest, 22f, false).GetComponent<CanvasGroup>().alpha = 0.55f;
                    T(rest, Lang.T("Ще не все знайдено."), 15f, A(Caption, 0.7f), F.Italic);
                }
                else
                {
                    RectTransform sheet = Rect("Answer", col);
                    Pinned(sheet, SagaCard, SagaCorner, k.Entry.Id);
                    VBox(sheet, 6f, TextAnchor.UpperLeft, 22, 22, 14, 14);
                    RectTransform top = Rect("Top", sheet);
                    HBox(top, 10f, TextAnchor.MiddleLeft);
                    SagaSeal(top, 22f, true);
                    SagaCaps(T(top, Lang.T("Відповідь"), 12f, Caption), 14f).textWrappingMode = TextWrappingModes.NoWrap;
                    Spring(top);
                    T(sheet, SagaCap(Lang.T(k.Answer), 26f), 19f, Ink, F.Head);
                }
            }
            else
            {
                SagaRule(col);
                foreach (string l in k.Lines) SagaMark(T(col, Lang.T(l), 16f, Ink).rectTransform, "◆");
                foreach (KeyValuePair<string, string> q in k.Quotes) SagaQuote(col, q.Key, LocTouch.Shown(q.Value)); // (as the piece itself shows them: translated, its «{pool …}» names the world's)
            }
            StorySaga.More more = StorySaga.MoreOf(Player.m_localPlayer, k); // (who may tell more, what nobody can be asked now)
            if (more.Who.Count > 0)
            {
                List<string> who = new List<string>();
                foreach (string n in more.Who) who.Add(Lang.T(n));
                T(col, who.Count == 1 ? Lang.F("{0} може розповісти більше.", who[0]) : Lang.F("Можуть розповісти більше: {0}.", Few(who)), 14f, Caption, F.Italic);
            }
            if (more.Lost) T(col, Lang.T("Про це вже нікого спитати."), 14f, Caption, F.Italic);
            Vector3 at;
            if (k.Map.Length > 0 && StoryWhere.MapAt(k.Map, out at)) SagaMap(col, at);
        }

        // words read on a piece of a place: a double frame, its caption between two ᛟ, the words in italics in the middle
        private static void SagaQuote(RectTransform col, string caption, string words)
        {
            RectTransform outer = Rect("Quote", col);
            Box(outer, 1f, new Color(0f, 0f, 0f, 0f), A(Ink, 0.55f), 1f);
            VBox(outer, 0f, TextAnchor.UpperLeft, 3, 3, 3, 3);
            RectTransform inner = Rect("In", outer);
            Box(inner, 1f, new Color(0.98f, 0.929f, 0.812f, 0.55f), A(Ink, 0.3f), 1f);
            VBox(inner, 6f, TextAnchor.UpperCenter, 20, 20, 12, 14);
            if (caption.Length > 0)
            {
                RectTransform cap = Rect("Caption", inner);
                HorizontalLayoutGroup h = HBox(cap, 10f, TextAnchor.MiddleCenter);
                h.childForceExpandWidth = false;
                T(cap, "ᛟ", 12f, GoldInk, F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
                SagaCaps(T(cap, Lang.T(caption), 12f, Caption), 14f).textWrappingMode = TextWrappingModes.NoWrap;
                T(cap, "ᛟ", 12f, GoldInk, F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
            }
            T(inner, words, 18f, InkSoft, F.Italic, TextAlignmentOptions.Top);
        }

        // ---- the design's small pieces
        // the first letter in the drop caps' gold, this much bigger (a text that starts with no letter — «…», «{» — as it is)
        private static string SagaCap(string s, float size)
        {
            if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0])) return s;
            return "<size=" + size.ToString("0", CultureInfo.InvariantCulture) + "><color=" + SagaCapHex + ">" + s[0] + "</color></size>" + s.Substring(1);
        }
        // the same, only its colour (the open name of a list)
        private static string SagaCapColour(string s) { return string.IsNullOrEmpty(s) || !char.IsLetter(s[0]) ? s : "<color=" + SagaCapHex + ">" + s[0] + "</color>" + s.Substring(1); }
        // a link on paper: gold ink, underlined at 45%
        private static string SagaLink(string s) { return "<u color=" + GoldInkHex + "73>" + s + "</u>"; }

        private static TMP_Text SagaCaps(TMP_Text t, float spacing) { t.fontStyle |= FontStyles.UpperCase; t.characterSpacing = spacing; return t; }

        private static void SagaGap(RectTransform col, float h) { Fixed(Rect("Gap", col).gameObject, -1f, h); }

        // a 1 px line taking the rest of a row
        private static void SagaLine(RectTransform row, float alpha)
        {
            Image l = Img(row, "Line", null, A(Ink, alpha));
            Fixed(l.gameObject, -1f, 1f);
            l.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        // a rule across the column with ᛟ in its middle
        private static void SagaRule(RectTransform col)
        {
            RectTransform row = Rect("Rule", col);
            HBox(row, 10f, TextAnchor.MiddleCenter);
            SagaLine(row, 0.35f);
            T(row, "ᛟ", 12f, GoldInk, F.Rune).textWrappingMode = TextWrappingModes.NoWrap;
            SagaLine(row, 0.35f);
        }

        // a small heading of a list in letter-spaced capitals, a line after it
        private static void SagaSubhead(RectTransform col, string text)
        {
            SagaGap(col, 6f);
            RectTransform row = Rect("Subhead", col);
            HBox(row, 10f, TextAnchor.MiddleLeft);
            SagaCaps(T(row, text, 14f, Caption, F.Head), 12f).textWrappingMode = TextWrappingModes.NoWrap;
            SagaLine(row, 0.3f);
        }

        // a page's heading with its drop cap («Мій шлях»), a line after it if asked
        private static void SagaHeading(RectTransform col, string text, bool rule)
        {
            RectTransform row = Rect("Heading", col);
            HBox(row, 10f, TextAnchor.MiddleLeft);
            T(row, SagaCap(text, 32f), 24f, Ink, F.Head).textWrappingMode = TextWrappingModes.NoWrap;
            if (rule) SagaLine(row, 0.35f); else Spring(row);
        }

        // ◆ / ◇ hanging in the margin, left of the words' first line
        private static void SagaMark(RectTransform owner, string glyph)
        {
            TMP_Text m = T(owner, glyph, 10f, GoldInk);
            m.textWrappingMode = TextWrappingModes.NoWrap;
            m.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform r = m.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(12f, 14f); r.anchoredPosition = new Vector2(-17f, -4f);
        }

        // a round medallion with a rune (or a figure: font): 0 — pale with a gold rim, 1 — dark with the light gold (an answered riddle)
        private static RectTransform SagaMedal(RectTransform parent, float size, string mark, int kind, F font = F.Rune)
        {
            RectTransform r = Rect("Medal", parent);
            Fixed(r.gameObject, size, size);
            Img(r, "Rim", BookArt.Circle, kind == 1 ? SealDarkRim : MedalRim); // (as the rail's medallions: a disc of the rim's colour, the face over it — no picture of its own)
            Image face = Img(r, "Face", BookArt.Circle, kind == 1 ? SealDark : MedalFace);
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
            TMP_Text t = T(r, mark, Mathf.Round(size * (font == F.Rune ? 0.5f : 0.55f)), kind == 1 ? Gold : GoldInk, font, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return r;
        }

        // a riddle's seal: open — a dashed gold ring round ᛈ; answered — the dark medallion. With a CanvasGroup: a caller may fade it
        private static RectTransform SagaSeal(RectTransform parent, float size, bool closed)
        {
            RectTransform r;
            if (closed) r = SagaMedal(parent, size, "ᛈ", 1);
            else
            {
                r = Rect("Seal", parent);
                Fixed(r.gameObject, size, size);
                Img(r, "Rim", BookArt.DashedRing(SealK, 12), MedalRim);
                TMP_Text t = T(r, "ᛈ", Mathf.Round(size * 0.5f), GoldInk, F.Rune, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            r.gameObject.AddComponent<CanvasGroup>();
            return r;
        }
    }
}
#endif

#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Слова ворона» in the book (Words): a wax seal with the rune ᚨ (Ansuz: Odin's rune of speech) beside the raven's
    // name on «Ворони», from trust 3; under it how many words of how many it can keep. It breathes gold while a word
    // caught from the chat waits for your «Лишити / Викинути». A click opens the card: each word, how clean it says it
    // (●○○ … ●●●), its moment (a click on it: the next one), ✕ to forget it; the free places; the caught word. The card
    // stays open over the page drawn anew (as the food list). The wish «Навчи мене» has its own field in the wish's card
    // (WordField, used by RavenBookRequest).
    internal static partial class RavenBook
    {
        private static readonly Color WordsWax = C("#2c4f4a");
        private static bool? _wordsFor;    // whose words' card is open (it stays open over a redraw)
        private static Vector2 _wordsAt;

        private static void WordsSeal(RectTransform top, bool m, ItemDrop.ItemData item)
        {
            List<Words.Word> words = Words.Of(item);
            int slots = Words.Slots(item);
            if (slots == 0 && words.Count == 0) return;
            string heard = Words.Heard(item);
            RectTransform box = Rect("Words", top);
            Fixed(box.gameObject, 54f, 66f);
            RectTransform seal = Rect("Seal", box);
            seal.anchorMin = seal.anchorMax = seal.pivot = new Vector2(0.5f, 1f);
            seal.sizeDelta = new Vector2(46f, 46f);
            seal.anchoredPosition = new Vector2(0f, -2f);
            if (heard.Length > 0) // a caught word waits: it breathes, as a wish waiting
            {
                Image glow = Img(seal, "Glow", BookArt.Glow, A(Gold, 0.7f));
                Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f));
                SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
                b.Low = 0.15f; b.High = 0.8f;
            }
            Image ring = Img(seal, "Ring", BookArt.Circle, Color.Lerp(WordsWax, Color.black, 0.35f));
            Image face = Img(seal, "Face", BookArt.Circle, WordsWax);
            Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            TMP_Text g = T(seal, "ᚨ", 22f, C("#e6f0dc"), F.Rune, TextAlignmentOptions.Center);
            Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text n = T(box, Mathf.Min(words.Count, slots) + "/" + slots, 13f, InkSoft, F.Body, TextAlignmentOptions.Bottom);
            Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (Image i in seal.GetComponentsInChildren<Image>(true)) i.raycastTarget = i == ring;
            ring.raycastTarget = true;
            Tip(ring.gameObject, () => WordsTip(m, item));
            Clickable(ring.gameObject, () => { _wordsEdit = -1; WordsCard(m, null); });
        }

        private static string WordsTip(bool m, ItemDrop.ItemData item)
        {
            List<Words.Word> words = Words.Of(item);
            int slots = Words.Slots(item);
            string s = Lang.F("<b>Слова {0}: {1} / {2}</b>", Plugin.RavenNameGen(m), Mathf.Min(words.Count, slots), slots);
            if (words.Count == 0) s += "\n" + Lang.T("Ще жодного. Він сам попросить навчити його слова — або підхопить щось із чату.");
            for (int i = 0; i < words.Count; i++)
                s += "\n<color=" + GoldHex + ">«" + words[i].Text + "»</color> · " + (i < slots ? Words.MomentName(words[i].When) : Lang.T("спить"));
            string heard = Words.Heard(item);
            if (heard.Length > 0) s += "\n\n<color=#FFD54F>" + Lang.F("Підхопив із чату: «{0}». Лишити?", heard) + "</color>";
            return s + "\n\n" + Lang.T("Клік — керувати словами.");
        }

        // the card of its words (keep: drawn again where it stood)
        private static void WordsCard(bool m, Vector2? keep)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(m);
            if (p == null || item == null || Trust.Stranger(item, p)) return;
            List<Words.Word> words = Words.Of(item);
            int slots = Words.Slots(item);
            if (_wordsEdit >= words.Count) _wordsEdit = -1; // (forgotten meanwhile)
            Card c = OpenCard("WordsCard", Lang.F("Слова {0}", Plugin.RavenNameGen(m)), 560f, true);
            T(c.Paper, m ? Lang.T("Мунін вставляє ці слова у свою мову, коли йому заманеться. Перекручує, звісно. Що частіше каже — то чистіше, але трохи псує навмисно.")
                         : Lang.T("Хугін вставляє ці слова у свою мову. Спершу плутає звуки; що частіше каже — то чистіше."), 15f, PSoft, F.Italic);

            string heard = Words.Heard(item);
            if (heard.Length > 0)
            {
                RectTransform hr = Rect("Heard", c.Paper);
                HBox(hr, 10f, TextAnchor.MiddleLeft, 0, 0, 4, 4);
                TMP_Text ht = T(hr, Lang.F("Підхопив із чату: <b>«{0}»</b>", heard), 17f, PInk, F.Body);
                ht.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                InkButton(hr, Lang.T("Лишити"), () => { string why = Words.KeepHeard(item, true); if (why != null) Tell(why); Again(m); });
                InkButton(hr, Lang.T("Викинути"), () => { Words.KeepHeard(item, false); Words.Forgot(p, m); Again(m); });
                if (words.Count >= slots) T(c.Paper, Lang.T("Місць немає: щоб лишити, спершу прибери одне слово."), 14f, C(BadHex), F.Italic);
                Rule(c.Paper);
            }

            for (int i = 0; i < words.Count; i++)
            {
                WordRow(c.Paper, p, m, item, words[i], i, i < slots);
                if (i == _wordsEdit && i < slots) { if (_wordsForget) ForgetAsk(c.Paper, p, m, item, words[i], i); else MomentPick(c.Paper, m, item, words[i], i); }
            }
            for (int i = words.Count; i < slots; i++)
                T(c.Paper, Lang.T("— вільне місце —"), 16f, A(PSoft, 0.7f), F.Italic, TextAlignmentOptions.Center);
            if (slots == 0) T(c.Paper, Lang.F("Слова відкриваються з {0}-го рівня довіри.", Words.FromLevel), 15f, PSoft, F.Italic);

            T(c.FootWords, Lang.F("Місць: {0}{1} · іскринок: {2}", slots, slots >= 8 ? "" : Lang.T(" (з довірою більше, до 8)"), Requests.Sparks(item)), 15f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            CardButton(c.Foot, Lang.T("Добре"), true, ClosePopup, "ᚨ");
            _wordsAt = PlaceCard(c, null, false, keep);
            _wordsFor = m;
        }

        private static int _wordsEdit = -1;   // the word whose moment is being chosen, or whose forgetting is asked
        private static bool _wordsForget;

        private static void WordRow(RectTransform paper, Player p, bool m, ItemDrop.ItemData item, Words.Word w, int index, bool awake)
        {
            RectTransform row = Rect("Word", paper);
            HBox(row, 12f, TextAnchor.MiddleLeft, 0, 0, 2, 2);
            Fixed(row.gameObject, -1f, 38f);
            Color ink = awake ? PInk : A(PSoft, 0.6f);
            TMP_Text t = T(row, "«" + w.Text + "»", 19f, ink, F.Head, TextAlignmentOptions.MidlineLeft);
            t.richText = false; // (what you typed stays words)
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            int stage = Words.Stage(w);
            string dots = "";
            for (int d = 1; d <= 3; d++) dots += d <= stage ? "●" : "○";
            TMP_Text dt = T(row, dots, 16f, awake ? GoldInk : A(PSoft, 0.6f), F.Body, TextAlignmentOptions.Center);
            dt.textWrappingMode = TextWrappingModes.NoWrap;
            dt.raycastTarget = true;
            Tip(dt.gameObject, () => stage >= 3 ? Lang.T("<b>Засвоєно</b>\nКаже чисто.")
                : Lang.F("<b>Засвоєння {0} / 3</b>\nЩе плутає звуки. Чисто — після {1} разів (сказано {2}).", stage, Words.CleanAt, w.Uses));
            if (awake)
            {
                bool open = _wordsEdit == index && !_wordsForget;
                RectTransform mb = InkButton(row, Words.MomentName(w.When), () => { _wordsForget = false; _wordsEdit = open ? -1 : index; Again(m); });
                Fixed(mb.gameObject, 190f, 34f);
                mb.GetComponent<HorizontalLayoutGroup>().spacing = 6f;
                Image[] chev = Chevron(mb, PInk); // (▾ ▴ are not in the fonts: drawn; open — turned up)
                if (open) chev[0].transform.parent.localRotation = Quaternion.Euler(0f, 0f, 180f);
                Hover(mb.gameObject, chev, PInk, null);
                Tip(mb.gameObject, () => Lang.F("<b>{0}</b>\nКаже: {1}.\n\n{2}", Words.MomentName(w.When), Words.MomentTip(w.When),
                    Words.MomentPrice(w) > 0 ? Lang.F("Клік — змінити момент: {0}.", Lang.N(Words.MomentPrice(w), "іскринка|іскринки|іскринок")) : Lang.T("Клік — вибрати момент. Перший вибір безкоштовний.")));
            }
            else
            {
                TMP_Text z = T(row, Lang.T("спить"), 15f, A(PSoft, 0.7f), F.Italic, TextAlignmentOptions.Center);
                Fixed(z.gameObject, 190f, 34f);
                z.raycastTarget = true;
                Tip(z.gameObject, () => Lang.T("Довіра впала, і місць стало менше: це слово він пам'ятає, але не каже, доки довіра не повернеться."));
            }
            RectTransform x = Rect("Forget", row); // (✕ is not in the fonts: the book's cross)
            Fixed(x.gameObject, 28f, 34f);
            Image xi = Img(x, "Cross", BookArt.Cross, A(PInk, 0.6f));
            xi.rectTransform.anchorMin = xi.rectTransform.anchorMax = xi.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            xi.rectTransform.sizeDelta = new Vector2(14f, 14f); xi.raycastTarget = false;
            Hover(x.gameObject, new Graphic[] { xi }, A(PInk, 0.6f), null);
            Tip(x.gameObject, () => Lang.F("Забути це слово: {0}.", Lang.N(Words.Price, "іскринка|іскринки|іскринок")));
            Clickable(x.gameObject, () =>
            {
                if (!awake) { string why = Words.Forget(item, index); if (why != null) Tell(why); else Words.Forgot(p, m); _wordsEdit = -1; Again(m); return; } // (a sleeping one: at once — it asks nothing more)
                bool asking = _wordsEdit == index && _wordsForget;
                _wordsForget = !asking; _wordsEdit = asking ? -1 : index;
                Again(m);
            });
        }

        // under a word: the seven moments to choose from (its own one dark); the price, if any
        private static void MomentPick(RectTransform paper, bool m, ItemDrop.ItemData item, Words.Word w, int index)
        {
            RectTransform box = Rect("Moments", paper);
            VBox(box, 6f, TextAnchor.UpperLeft, 14, 0, 0, 6);
            int price = Words.MomentPrice(w), have = Requests.Sparks(item);
            T(box, price > 0 ? Lang.F("Змінити момент — {0} (є {1}).", Lang.N(price, "іскринка|іскринки|іскринок"), have) : Lang.T("Коли йому казати це слово? Перший вибір безкоштовний, далі — іскринка за зміну."),
                14f, price > have ? C(BadHex) : PSoft, F.Italic);
            RectTransform line = null;
            for (int k = 0; k < Words.MomentCount; k++)
            {
                if (k % 4 == 0) { line = Rect("Line", box); HBox(line, 8f, TextAnchor.MiddleLeft); }
                Words.Moment mo = (Words.Moment)k;
                RectTransform b;
                if (mo == w.When)
                {
                    b = Rect("Here", line);
                    HBox(b, 0f, TextAnchor.MiddleCenter, 14, 14, 0, 0);
                    Fixed(b.gameObject, -1f, 32f);
                    Box(b, 5f, PDark, PGoldLine, 1.5f);
                    T(b, Words.MomentName(mo), 15f, PCream, F.Body, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.NoWrap;
                }
                else
                {
                    b = InkButton(line, Words.MomentName(mo), () =>
                    {
                        string why = Words.SetMoment(item, index, mo);
                        if (why != null) { Tell(why); return; }
                        _wordsEdit = -1;
                        Again(m);
                    });
                    Fixed(b.gameObject, -1f, 32f);
                }
                Tip(b.gameObject, () => "<b>" + Words.MomentName(mo) + "</b>\n" + Lang.F("Каже: {0}.", Words.MomentTip(mo)));
            }
        }

        // under a word: forget it for a sparklet? (so that a click by mistake costs nothing)
        private static void ForgetAsk(RectTransform paper, Player p, bool m, ItemDrop.ItemData item, Words.Word w, int index)
        {
            RectTransform row = Rect("Forget", paper);
            HBox(row, 10f, TextAnchor.MiddleLeft, 14, 0, 0, 6);
            int have = Requests.Sparks(item);
            TMP_Text q = T(row, Lang.F("Забути «{0}»? Це коштує {1} (є {2}).", w.Text, Lang.N(Words.Price, "іскринку|іскринки|іскринок"), have), 15f, have < Words.Price ? C(BadHex) : PInk, F.Italic);
            q.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            InkButton(row, Lang.T("Забути"), () =>
            {
                string why = Words.Forget(item, index);
                if (why != null) { Tell(why); return; }
                Words.Forgot(p, m);
                _wordsEdit = -1;
                Again(m);
            });
            InkButton(row, Lang.T("Ні"), () => { _wordsEdit = -1; Again(m); });
        }

        // a thin rule across the parchment
        private static void Rule(RectTransform paper)
        {
            RectTransform r = Rect("Rule", paper);
            Fixed(r.gameObject, -1f, 1.2f);
            r.gameObject.AddComponent<Image>().color = PRule;
        }

        // something changed: the page anew (the seal's count), and the card again where it stood
        private static void Again(bool m)
        {
            _wordsFor = m;
            if (IsOpen) Show(_tab, true); // (Show draws the card back: WordsAgain)
        }

        // after a page drawn anew (Show): the words' card back, if it was open
        private static void WordsAgain(bool? open, Vector2 at)
        {
            if (open.HasValue && _tab == RavensTab && Trust.Worn(open.Value) != null) WordsCard(open.Value, at);
        }

        // ---------------------------------------------------------------- «Навчи мене»: the field in the wish's card

        private static TMP_InputField _teachField;

        private static void WordField(RectTransform paper, bool m)
        {
            T(paper, Lang.F("Напиши слово чи коротку фразу (до {0} слів, {1} знаків). {2} повторюватиме — спершу криво.", Words.MaxWords, Words.MaxChars, Plugin.RavenName(m)), 14f, PSoft, F.Italic);
            RectTransform box = Rect("Field", paper);
            Fixed(box.gameObject, -1f, 46f);
            Box(box, 6f, new Color(1f, 1f, 1f, 0.28f), PGoldLine, 1.5f);
            RectTransform area = Rect("Area", box);
            Stretch(area, Vector2.zero, Vector2.one, new Vector2(14f, 4f), new Vector2(-14f, -4f));
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text t = T(area, "", 21f, PInk, F.Head, TextAlignmentOptions.MidlineLeft);
            t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text ph = T(area, m ? Lang.T("Ну? Кажи вже…") : Lang.T("Слово для ворона…"), 21f, A(PSoft, 0.7f), F.Italic, TextAlignmentOptions.MidlineLeft);
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(ph.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_InputField f = box.gameObject.AddComponent<TMP_InputField>();
            f.textViewport = area; f.textComponent = t; f.placeholder = ph;
            f.targetGraphic = box.GetComponent<Image>();
            f.transition = Selectable.Transition.None;
            f.lineType = TMP_InputField.LineType.SingleLine;
            f.characterLimit = Words.MaxChars;
            f.richText = false;
            f.customCaretColor = true; f.caretColor = PInk; f.caretWidth = 2;
            f.selectionColor = new Color(0.66f, 0.52f, 0.23f, 0.35f);
            Navigation nav = f.navigation; nav.mode = Navigation.Mode.None; f.navigation = nav;
            f.onValueChanged.AddListener(s =>
            {
                string kept = FontOnly(s, t.font); // (what the book's letters cannot draw is not typed: emoji, other scripts)
                if (kept != s) { f.SetTextWithoutNotify(kept); f.MoveTextEnd(false); }
            });
            f.onSubmit.AddListener(s => TeachNow(m));
            _teachField = f;
            f.ActivateInputField();
        }

        private static string FontOnly(string s, TMP_FontAsset font)
        {
            if (string.IsNullOrEmpty(s) || font == null) return s;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (c == ' ' || (!char.IsSurrogate(c) && font.HasCharacter(c, true))) sb.Append(c);
            return sb.ToString();
        }

        private static void TeachNow(bool m)
        {
            Player p = Player.m_localPlayer;
            if (p == null || _teachField == null) return;
            string why = Requests.Teach(p, m, _teachField.text);
            if (why != null) { Tell(why); return; }
            _teachField = null;
            ClosePopup();
            Refresh();
        }
    }
}
#endif

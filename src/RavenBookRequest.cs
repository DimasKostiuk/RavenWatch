#if RAVEN_ITEM
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Прохання ворона» in the book (Requests): the raven on its scroll (the user's icon, assets/book/request_scroll.png;
    // without it a drawn wax seal) to the right of the raven's name. No wish and no sparklets: nothing there. A wish
    // waiting: a glow in the raven's colour (Hugin blue, Munin red) breathes behind it. Done: a gold tick on its corner
    // until its card is opened. Only sparklets kept: faded. The sparklets as a tiny number under it. A click opens the post-style
    // card: its words, what and how far, the days left, the sparklets (at 5 — «Запалити руну»: «Шлях Одіна» opens with
    // the runes that can take a spark glowing; a click there lights one), «Показати на мапі» for the cache and «Покажи
    // мені», «Дати» for something shiny or feathers.
    internal static partial class RavenBook
    {
        // redraw the page if the book is open (a wish came, moved on, was done)
        internal static void Refresh() { if (IsOpen) Show(_tab, true); }

        // «Запалити руну»: whose sparklets light a rune now (-1: nobody; RavenBookRunes' cells glow while so)
        private static int _lightFor = -1;

        private static readonly Color SealHugin = C("#2f4f8a"), SealMunin = C("#8E1E1E"), SealGold = C("#c9962e");

        private static void RequestSeal(RectTransform top, bool m, ItemDrop.ItemData item)
        {
            _lightFor = -1; // (back on the ravens: a lighting not finished is given up)
            Requests.Req r = Requests.Of(item);
            int sparks = Requests.Sparks(item);
            bool waiting = Requests.Waiting(r), done = r != null && r.Done;
            if (!waiting && !done && sparks <= 0) return;
            RectTransform box = Rect("Request", top);
            Fixed(box.gameObject, 54f, 66f);
            RectTransform seal = Rect("Seal", box);
            seal.anchorMin = seal.anchorMax = seal.pivot = new Vector2(0.5f, 1f);
            seal.sizeDelta = new Vector2(50f, 50f);
            seal.anchoredPosition = Vector2.zero;
            Color wax = done ? SealGold : m ? SealMunin : SealHugin;
            if (!waiting && !done) wax = A(wax, 0.55f); // (only sparklets kept: a faded seal)
            if (waiting)
            {
                Image glow = Img(seal, "Glow", BookArt.Glow, A(wax, 0.6f));
                Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f));
                SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
                b.Low = 0.15f; b.High = 0.75f;
            }
            Image ring;
            Sprite scroll = BookArt.Picture("request_scroll");
            if (scroll != null)
            {
                ring = Img(seal, "Scroll", scroll, waiting || done ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                ring.preserveAspect = true;
                if (done)
                {
                    Image badge = Img(seal, "Done", BookArt.Circle, SealGold);
                    badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(1f, 0f);
                    badge.rectTransform.sizeDelta = new Vector2(18f, 18f); badge.rectTransform.anchoredPosition = new Vector2(2f, 0f);
                    TMP_Text tick = T(badge.transform, "✓", 13f, C("#fff4dc"), F.Body, TextAlignmentOptions.Center);
                    Stretch(tick.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                }
            }
            else
            {
                ring = Img(seal, "Ring", BookArt.Circle, Color.Lerp(wax, Color.black, 0.35f));
                Image face = Img(seal, "Face", BookArt.Circle, wax);
                Stretch(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                TMP_Text g = T(seal, done ? "✓" : "ᚱ", done ? 22f : 20f, C("#f6e2c0"), done ? F.Body : F.Rune, TextAlignmentOptions.Center);
                Stretch(g.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            if (sparks > 0)
            {
                TMP_Text n = T(box, sparks >= Requests.SparksForRune ? "<b>" + sparks + "</b>" : sparks.ToString(), 13f, sparks >= Requests.SparksForRune ? GoldInk : InkSoft, F.Body, TextAlignmentOptions.Bottom);
                Stretch(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            ring.raycastTarget = true;
            Tip(ring.gameObject, () => waiting ? Lang.F("<b>Прохання {0}</b>\n{1}\n\nКлік — подробиці.", Plugin.RavenNameGen(m), Requests.Goal(Requests.Of(item)))
                : done ? Lang.F("<b>Прохання {0} виконано!</b>\n\nКлік — подивитись.", Plugin.RavenNameGen(m))
                : Lang.F("<b>Іскринки {0}: {1} / {2}</b>\n\nКлік — подробиці.", Plugin.RavenNameGen(m), sparks, Requests.SparksForRune));
            Clickable(ring.gameObject, () => RequestCard(seal, m));
        }

        private static void RequestCard(RectTransform by, bool m)
        {
            ClosePopup();
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = Trust.Worn(m);
            if (p == null || item == null) return;
            Requests.Req r = Requests.Of(item);
            bool waiting = Requests.Waiting(r), done = r != null && r.Done;
            int sparks = Requests.Sparks(item);
            Card c = OpenCard("RequestCard", Lang.F("Прохання {0}", Plugin.RavenNameGen(m)), 460f, true);
            Color ink = m ? C("#8E1E1E") : C("#2f4f7a");
            string said;
            if (r != null && item.m_customData.TryGetValue(Requests.SaidKey, out said) && !string.IsNullOrEmpty(said))
                T(c.Paper, "«" + said + "»", 17f, PInk, F.Italic);
            if (waiting || done)
            {
                RectTransform row = Rect("Goal", c.Paper);
                HBox(row, 10f, TextAnchor.MiddleLeft);
                TMP_Text goal = T(row, "<b>" + Requests.Goal(r) + "</b>", 17f, ink, F.Body);
                goal.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                string prog = Requests.Progress(r);
                if (prog.Length > 0) T(row, prog, 17f, PInk, F.Body, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
                if (done) T(c.Paper, Lang.F("Виконано! +{0}", Lang.N(r.Reward, "іскринка|іскринки|іскринок")), 16f, C(PGoodHex), F.Italic);
                else
                {
                    if (r.Type == "teach") WordField(c.Paper, m); // the word to teach it (RavenBookWords)
                    int left = Requests.DaysLeft(r);
                    T(c.Paper, Lang.F("Лишилось: {0}", Lang.N(left, "день|дні|днів")), 15f, PSoft, F.Italic);
                }
            }
            else T(c.Paper, Lang.T("Зараз він нічого не просить. Прохання приходять самі, приблизно раз на 4–5 днів."), 15f, PSoft, F.Italic);

            // the sparklets: a row of five, or the lighting
            if (sparks >= Requests.SparksForRune)
            {
                bool can = RuneStars.CanSpark(p);
                InkButton(c.Paper, can ? Lang.F("Запалити руну ({0} іскринок)", Requests.SparksForRune) : Lang.T("Запалити руну: нема руни, що візьме іскру"), () =>
                {
                    if (!RuneStars.CanSpark(p)) return;
                    ClosePopup();
                    _lightFor = m ? 1 : 0;
                    _tab = RunesTab;
                    Show(_tab, true);
                    Tell(Lang.T("Обери руну, що світиться: їй дістанеться іскра."));
                });
            }
            else
            {
                string dots = "";
                for (int i = 0; i < Requests.SparksForRune; i++) dots += i < sparks ? "◆ " : "◇ "; // (the book font has no ✦: squares)
                RectTransform sr = Rect("Sparks", c.Paper);
                HBox(sr, 10f, TextAnchor.MiddleLeft);
                T(sr, Lang.T("Іскринки"), 16f, PLabel, F.Body).textWrappingMode = TextWrappingModes.NoWrap;
                TMP_Text d = T(sr, dots.TrimEnd(), 18f, GoldInk, F.Body);
                d.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                T(sr, sparks + " / " + Requests.SparksForRune, 16f, PInk, F.Body, TextAlignmentOptions.MidlineRight).textWrappingMode = TextWrappingModes.NoWrap;
            }
            int hoard = Requests.Hoard(item);
            if (hoard > 0) T(c.Paper, Lang.F("Скарбничка: {0}", Lang.N(hoard, "блискітка|блискітки|блискіток")), 15f, PSoft, F.Italic);

            T(c.FootWords, Lang.F("Виконано прохань: {0}", Requests.DoneCount(item)), 15f, FootSoft, F.Italic, TextAlignmentOptions.MidlineLeft);
            if (waiting && Requests.Gives(r))
                CardButton(c.Foot, Lang.F("Дати: {0}", r.Need > 1 ? r.Need + " × " + Requests.ItemName(r.Arg) : Requests.ItemName(r.Arg)), false, () => { string why = Requests.GiveShiny(p, m); ClosePopup(); if (why != null) Tell(why); Refresh(); });
            if (waiting && r.Type == "teach")
                CardButton(c.Foot, Lang.T("Навчити"), true, () => TeachNow(m), "ᚨ");
            if (waiting && (r.Type == "cache" || (r.Type == "show" && r.Arg != "dungeon")))
                CardButton(c.Foot, Lang.T("Показати на мапі"), false, () => { ClosePopup(); string why = Requests.ShowOnMap(p, m); if (why != null) Tell(why); });
            CardButton(c.Foot, waiting && r.Type == "teach" ? Lang.T("Пізніше") : Lang.T("Добре"), !(waiting && r.Type == "teach"), () => { ClosePopup(); if (done) Refresh(); });
            if (done) Requests.Seen(item); // (opened: the gold seal goes with the next draw)
            PlaceCard(c, by, true);
        }

        // RavenBookRunes' rune cell while a rune is being lit: the ones that can take a spark glow; a click lights it.
        // true when the cell took the click (the usual one is not added then)
        private static bool LightCell(RectTransform cell, Player p, string rune)
        {
            if (_lightFor < 0 || p == null || !RuneStars.CanSparkRune(p, rune)) return false;
            Image glow = Img(cell, "LightGlow", BookArt.Glow, A(Gold, 0.9f));
            Stretch(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-26f, -14f), new Vector2(26f, 26f));
            SparkBreath b = glow.gameObject.AddComponent<SparkBreath>();
            b.Low = 0.35f; b.High = 1f;
            glow.transform.SetAsFirstSibling();
            bool munin = _lightFor == 1;
            Clickable(cell.gameObject, () =>
            {
                string why = Requests.Light(p, munin, rune);
                _lightFor = -1;
                if (why != null) Tell(why);
                Show(_tab, true);
            });
            return true;
        }
    }
}
#endif

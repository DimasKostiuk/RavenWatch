#if RAVEN_ITEM
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // «Шлях Одіна», the left page: what the errand is, wherever you are (its words, each step with its state, the way to
    // its place on the map), or how to get one; under it the way to the golden runes. Giving a thing and giving it up are
    // in the nest's bowl (NestWindow).
    internal static partial class RavenBook
    {
        private static void ErrandSide(Player p)
        {
            RectTransform left = Column("Left", 56f, 40f, 500f, 10f);
            T(left, Lang.T("Доручення Одіна"), 26f, Ink, F.Head);
            ErrandRun r = Errands.Of(p);
            bool m; float away;
            bool flying = Nest.Flying(out m, out away);
            if (r != null) ErrandCard(left, p, r);
            else if (flying)
            {
                Ticking(T(left, Lang.F("{0} летить до Одіна. Повернеться за {1} — з дорученням.", Plugin.RavenName(m), RavenAbilities.Span(away)), 17f, InkSoft, F.Italic), () => { bool fm; float fl; return Nest.Flying(out fm, out fl) ? Lang.F("{0} летить до Одіна. Повернеться за {1} — з дорученням.", Plugin.RavenName(fm), RavenAbilities.Span(fl)) : null; });
            }
            else
            {
                T(left, Lang.T("Доручення нема. Посади воронів у Вороняче гніздо (E на гніздо вгорі), зазирни в чашу внизу й натисни «Призвати милість Одіна». Коли над гніздом засяє золото — Одін змилувався: прийми його волю в чаші. Може й не прийти — тоді клич наступного дня. За доручення Одін дає руни."), 16f, InkSoft, F.Body);
                string why = Nest.Blocked(p);
                if (why != null) Ticking(T(left, why, 15f, Bad, F.Italic), () => Nest.Blocked(p)); // («наступне — за …» counts down)
            }

            // ---- the way to the golden runes
            RectTransform gap = Rect("Gap", left); Fixed(gap.gameObject, -1f, 8f);
            RuneBook.Data d = RuneBook.Of(p);
            T(left, Lang.T("Золотий шлях"), 20f, GoldInk, F.Head);
            if (d.Opened < RuneBook.GoldFrom)
            {
                RectTransform path = Rect("Path", left);
                HBox(path, 6f, TextAnchor.MiddleLeft);
                for (int i = 0; i < RuneBook.GoldFrom; i++)
                {
                    TMP_Text g = T(path, i < d.Opened ? "◆" : "◇", 22f, i < d.Opened ? GoldInk : A(Ink, 0.35f), F.Body);
                    g.textWrappingMode = TextWrappingModes.NoWrap;
                }
                Spring(path);
                T(left, Lang.F("Коли загориться {0} руни, Одін може покликати й на золоте доручення.", RuneBook.GoldFrom), 15f, InkSoft, F.Italic);
            }
            else if (d.GoldEver >= 2 * GiftUp.MaxPerRaven) T(left, Lang.F("Золотих доручень більше не буде: ти вже отрима{{в|ла}} всі {0} золотих рун.", 2 * GiftUp.MaxPerRaven), 15f, InkSoft, F.Italic); // (Nest.Back: every one ever got counts)
            else T(left, Lang.T("Тепер Одін може покликати й на золоте доручення. Що більше рун горить, то частіше."), 15f, InkSoft, F.Italic);
        }

        // the errand: its name and words, each step, the way to its place; giving a thing and giving it up only in the
        // nest's bowl (the book just says so)
        private static void ErrandCard(RectTransform col, Player p, ErrandRun r)
        {
            RectTransform card = Rect("Errand", col);
            VBox(card, 6f, TextAnchor.UpperLeft, 18, 18, 16, 16); // (room for the photo corners)
            bool golden = r.Def != null && r.Def.Golden;
            Pinned(card, golden ? new Color(0.98f, 0.88f, 0.6f, 1f) : new Color(0.98f, 0.94f, 0.85f, 1f), golden ? C("#c9a24a") : Color.white, r.Id); // Odin's errand: a sheet pinned in
            if (r.Def == null)
            {
                T(card, r.Elsewhere ? Lang.T("Це доручення Одін дав в іншому світі: воно чекає, доки ти туди повернешся.") : Lang.F("Доручення «{0}» з іншої версії мода: його можна лише відкласти.", r.Id), 16f, InkSoft, F.Italic);
            }
            else
            {
                RectTransform head = Rect("Head", card);
                HBox(head, 8f, TextAnchor.LowerLeft);
                T(head, Lang.T(r.Def.Name), 21f, r.Def.Golden ? GoldInk : Ink, F.Head).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                TMP_Text who = T(head, Trial.From(r), 14f, Brown, F.Body, TextAlignmentOptions.BottomRight);
                who.textWrappingMode = TextWrappingModes.NoWrap;
                string text = Errands.TextOf(r);
                if (text.Length > 0) T(card, text, 15f, InkSoft, F.Italic);
                System.Collections.Generic.List<string> mods = ErrandMods.Lines(r);
                for (int i = 0; i < mods.Count; i++)
                {
                    int at = i; // («Поспіх» counts down)
                    Ticking(T(card, mods[i], 15f, new Color(0.62f, 0.16f, 0.12f), F.Head), () => { System.Collections.Generic.List<string> now = ErrandMods.Lines(r); return at < now.Count ? now[at] : null; });
                }
                for (int i = 0; i < r.Steps.Length; i++)
                {
                    bool done = i < r.Step, now = i == r.Step;
                    string mark = done ? "✓" : now ? "•" : "·";
                    TMP_Text line = T(card, mark + "  " + r.Steps[i].Describe(r, i, p), 16f, done ? Good : now ? Ink : A(Ink, 0.45f), now ? F.Head : F.Body);
                    if (now) { int at = i; Ticking(line, () => mark + "  " + r.Steps[at].Describe(r, at, Player.m_localPlayer)); } // (the way left, what the bag holds: live, the page not drawn again)
                }
            }
            Vector3 zone = Vector3.zero;
            bool marked = false;
            if (r.Current != null)
            {
                float radius;
                if (r.Current.Where(r, r.Step, out zone, out radius))
                {
                    marked = true;
                    Vector3 zc = zone; float rr = radius;
                    System.Func<string> way = () => { Player me = Player.m_localPlayer; if (me == null) return null; Vector3 d = zc - me.transform.position; d.y = 0f; return Lang.F("Місце позначене на мапі синім колом: до нього ~{0} м.", Mathf.RoundToInt(Mathf.Max(0f, d.magnitude - rr))); };
                    Ticking(T(col, way(), 14f, InkSoft, F.Italic), way); // (as you go: the words, not the page)
                }
            }
            string actWhy, act = Errands.ActLabel(p, out actWhy); // the step's own deed (Odin's feather…): grey where it can't be done
            if (marked || act.Length > 0)
            {
                RectTransform row = Rect("Buttons", col);
                HBox(row, 10f, TextAnchor.MiddleLeft, 0, 0, 4, 0);
                if (act.Length > 0)
                {
                    string w = actWhy;
                    RectTransform b = OutlineButton(row, act, () => { Errands.Act(Player.m_localPlayer); Refresh(); }, w == null);
                    Tip(b.gameObject, () => w == null ? Lang.T("Те саме, що E на місці чи наказ у колі J.") : Lang.F("Лише {0}.", w));
                }
                if (marked)
                {
                    Vector3 z = zone;
                    RectTransform track = OutlineButton(row, Lang.T("Відстежити"), () => ErrandMap.Show(z), true);
                    Tip(track.gameObject, () => Lang.T("Відкрити мапу на місці доручення."));
                    if (Outfits.Tuning) // a test button: admins while tuning only
                    {
                        RectTransform tp = OutlineButton(row, "Телепорт (тест)", () => ErrandMap.Teleport(p, z), true);
                        Tip(tp.gameObject, () => "Лише для тесту (адмін з «Outfits all for tuning»): одразу в зону доручення.");
                    }
                }
                Spring(row);
            }
            T(col, Nest.AsksToHandIn(p) ? Lang.T("Віддати річ і відмовитись можна в чаші воронячого гнізда.") : Lang.T("Відмовитись можна в чаші воронячого гнізда."), 14f, Brown, F.Italic);
        }

        private static string NestHelp(string h)
        {
            return Lang.F("{0}Тут видно твоє доручення, де б ти не бу{{в|ла}}.\n\n<b>Як отримати доручення.</b> Посади воронів у Вороняче гніздо (E на гніздо вгорі), зазирни в чашу внизу й натисни «Призвати милість Одіна»: ворони кличуть його, а він приходить за десять секунд — хвилину. Або й не приходить: тоді наступний поклик — лише наступного ігрового дня. Коли над гніздом засяє золото — Одін змилувався. Прийми його волю в чаші: чого він хоче, Одін не каже. Один із воронів злетить до нього в хмари й за кілька хвилин повернеться на плече вже з дорученням. Одне доручення приблизно на ігровий день, а що більше ворон довіряє — то частіше.\n\n<b>Милість Одіна.</b> Дари в чашу (трофеї й м'ясо, руда й метал, мед і їжа, монети й самоцвіти) схиляють, яке доручення він дасть.\n\n<b>Доручення</b> — убити, принести, дійти чи зробити щось. Кроки йдуть по черзі; річ для кроку віддають у чашу гнізда. Виконане доручення робить зарубки на різі, а {1} зарубок запалюють руну. Відмовитись можна в чаші, без кари, але нове — лише наступного дня.\n\n<b>Модифікатори.</b> Часом доручення має умову: «Без смерті» (помреш — доручення пропаде), «Без подряпин» (здоров'я жодного разу нижче 30% і без смерті), «Поспіх» (за ігровий день), «Пішки» (без порталів; що далі шлях, то щедріше), «Без відступу» (не тікати від ворога, що б'є), «Тихо» (не битися вночі й у бурю). Дотримаєшся — таємна нагорода: зарубка або Іскра Одіна, що лягає на руну й дає їй силу. Зрідка Одін дає своє, золотом: <b>Обітниця Одіна</b> (ні смерті, ні порталів — інакше доручення пропаде) чи <b>Випробування іскри</b> (встигни до кінця часу й не помри) — за них іскра Одіна напевно. Золоте доручення зрідка просить пожертву монетами в гнізді (до 300, лише на початку, поки нічого не зроблено) — Одін поверне втричі.\n\nКоли горить {2} руни, Одін може покликати й на <b>золоте</b> доручення.", h, RuneBook.NotchesPerRune, RuneBook.GoldFrom);
        }
    }
}
#endif

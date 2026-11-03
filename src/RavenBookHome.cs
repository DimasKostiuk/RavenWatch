#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The Ravens page's foot (the user's design, 2026-10-28): the ravens of this kind not on you, in ONE line under a
    // dashed rule — the page has no room for more. One at home (or on guard): where, its numbers, «журнал ›» (its card:
    // what it did since you were by); one lost: a red dot, its price, «Повернути ›» (the return's card); more than one:
    // «Удома 2 · ● пропав 1 · список ›», the list in a card. A raven at home is fetched in person only (E at its perch).
    internal static partial class RavenBook
    {
        private const string LostHex = "#9A3A2A";
        private static readonly Color LostDot = C("#A3261A"), FootRule = C("#7a5518");

        // lost: the lost ones too (a raven not on you: its empty page has the return)
        private static void FootZone(RectTransform col, bool m, bool lost)
        {
            List<Home.Note> homes;
            List<Backup.Lost> gone;
            NotWithYou(m, lost, out homes, out gone);
            if (homes.Count + gone.Count == 0) return;
            if (homes.Count + gone.Count == 1)
            {
                if (gone.Count == 1) LostLine(col, gone[0], null);
                else HomeLine(col, homes[0], null);
                return;
            }
            RectTransform[] at = new RectTransform[1];
            RectTransform row = FootLine(col, () => FootListCard(at[0], m, lost));
            at[0] = row;
            if (homes.Count > 0) T(row, "<b>" + Lang.F("Удома {0}", homes.Count) + "</b>", 13f, Ink, F.Body, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            if (homes.Count > 0 && gone.Count > 0) T(row, "·", 13f, RailDot, F.Body, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            if (gone.Count > 0)
            {
                LostMark(row);
                T(row, "<b><color=" + LostHex + ">" + Lang.F("пропав {0}", gone.Count) + "</color></b>", 13f, Ink, F.Body, TextAlignmentOptions.MidlineLeft).textWrappingMode = TextWrappingModes.NoWrap;
            }
            Spring(row);
            FootLink(row, Lang.T("список ›"));
        }

        // the ravens of this kind left at home (not gone to Odin) and, if asked, those lost
        private static void NotWithYou(bool m, bool lost, out List<Home.Note> homes, out List<Backup.Lost> gone)
        {
            Player p = Player.m_localPlayer;
            homes = new List<Home.Note>();
            gone = new List<Backup.Lost>();
            foreach (Home.Note n in Home.Notes(p)) if (n.Munin == m && !n.Odin) homes.Add(n); // (gone to Odin: the return has it)
            if (lost) foreach (Backup.Lost l in Backup.Ravens(p)) if (l.Munin == m && !l.WithYou) gone.Add(l);
        }

        // the line itself: the dashed rule over it, a click anywhere on it
        private static RectTransform FootLine(Transform parent, Action click)
        {
            RectTransform row = Rect("Foot", parent);
            HBox(row, 8f, TextAnchor.MiddleLeft, 0, 0, 5, 0);
            Fixed(row.gameObject, -1f, 26f);
            Image rule = Img(row, "Rule", BookArt.Dash(true), A(FootRule, 0.55f));
            rule.type = Image.Type.Tiled; rule.pixelsPerUnitMultiplier = 2f;
            rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            rule.rectTransform.anchorMin = new Vector2(0f, 1f); rule.rectTransform.anchorMax = new Vector2(1f, 1f);
            rule.rectTransform.offsetMin = new Vector2(0f, -2f); rule.rectTransform.offsetMax = Vector2.zero;
            Clickable(row.gameObject, click);
            return row;
        }

        // «журнал ›», «Повернути ›»: gold ink, brighter while the line is under the pointer
        private static void FootLink(RectTransform row, string text)
        {
            TMP_Text t = T(row, text, 13f, GoldInk, F.Body, TextAlignmentOptions.MidlineRight);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Hover(row.gameObject, new Graphic[] { t }, GoldInk, null);
        }

        private static void LostMark(RectTransform row)
        {
            RectTransform d = Rect("Dot", row);
            Fixed(d.gameObject, 9f, 9f);
            Img(d, "Dot", BookArt.Circle, LostDot);
        }

        // one at home: «Мунін вдома: «Жердина біля печі» · 120 м звідси · запас варти … · журнал ›»; anchor: where its
        // card opens (the list's card is gone by then: the page's line)
        private static void HomeLine(Transform parent, Home.Note n, RectTransform anchor)
        {
            bool guard = n.Mode == Home.OnGuard, m = n.Munin;
            string token = n.Token;
            RectTransform[] at = { anchor };
            RectTransform row = FootLine(parent, () => HomeCard(at[0], token));
            if (at[0] == null) at[0] = row;
            TMP_Text title = T(row, "<b>" + (guard ? Lang.F("{0} на варті", Plugin.RavenName(m)) : Lang.F("{0} вдома", Plugin.RavenName(m))) + ":</b>", 13f, Ink, F.Body, TextAlignmentOptions.MidlineLeft);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Tip(title.gameObject, () => HomeTip(guard));
            Func<string> text = () => HomeLineText(token);
            Ticking(Shrink(T(row, text() ?? "", 13f, InkSoft, F.Body, TextAlignmentOptions.MidlineLeft), 11f), text);
            FootLink(row, Lang.T("журнал ›"));
        }

        private static string HomeLineText(string token)
        {
            string where = HomeWhere(token), stats = HomeStats(token);
            return where == null ? null : where + (stats != null ? " · " + stats : "");
        }

        private static string HomeWhere(string token)
        {
            Player me = Player.m_localPlayer;
            Home.Note x = Home.NoteOf(me, token);
            if (x == null) return null;
            return !x.Here ? Lang.T("в іншому світі") : me != null ? Lang.F("«{0}» · {1} м звідси", x.Perch, Mathf.RoundToInt(Vector3.Distance(me.transform.position, x.Pos))) : "«" + x.Perch + "»";
        }

        private static string HomeStats(string token)
        {
            Home.Note x = Home.NoteOf(Player.m_localPlayer, token);
            return x == null ? null : Lang.F("запас варти {0}/{1} · сила {2}/{3} · ситість {4}", Mathf.FloorToInt(x.Store), Mathf.RoundToInt(x.Max * Home.StoreShare), Mathf.FloorToInt(x.Strength), Mathf.RoundToInt(x.Max), Mathf.RoundToInt(x.Sat));
        }

        // one lost: «● Хугін пропав · ціна: … · Повернути ›»
        private static void LostLine(Transform parent, Backup.Lost l, RectTransform anchor)
        {
            Player p = Player.m_localPlayer;
            string id = l.Id;
            RectTransform[] at = { anchor };
            RectTransform row = FootLine(parent, () => ReturnCard(at[0], id));
            if (at[0] == null) at[0] = row;
            LostMark(row);
            TMP_Text title = T(row, "<b><color=" + LostHex + ">" + Lang.F("{0} пропав", Plugin.RavenName(l.Munin)) + "</color></b>", 13f, Ink, F.Body, TextAlignmentOptions.MidlineLeft);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            string price = ReturnPrice(p, l);
            Tip(title.gameObject, () => ReturnTip(price));
            Shrink(T(row, l.DaysLeft > 0 ? Lang.F("лишилось {0} дн. до наступного повернення", l.DaysLeft) : Lang.F("ціна: {0}", price), 13f, InkSoft, F.Body, TextAlignmentOptions.MidlineLeft), 11f);
            FootLink(row, Lang.T("Повернути ›"));
        }

        private static string ReturnPrice(Player p, Backup.Lost l)
        {
            return l.Price != null ? Lang.F("{0} ×{1} (є {2})", l.Price.Name, l.Price.Count, Trust.Have(p, l.Price.Prefab)) : Lang.T("трофеї боса");
        }

        private static string ReturnTip(string price)
        {
            return Lang.F("<b><color={0}>Повернення ворона</color></b>\n\nВорон загинув у морі, зник чи лишився деінде? Його можна повернути обрядом біля Жертовних каменів.\n\nЦіна: {1}.\n\nВорон повернеться з тим самим рівнем довіри, вишколом і переможеними босами. Лише шкала до наступного рівня почнеться з нуля, а частину спогадів він забуде. Старий ворон, якщо десь лишився, розсиплеться пір'ям. Не частіше ніж раз на {2} днів.", GoldHex, price, Backup.RestoreDays);
        }

        private static string HomeTip(bool guard)
        {
            return guard
                ? Lang.F("<b><color={0}>На варті</color></b>\n\nПерші {1} хв ворон обживається, а тоді стереже вартову жердину: б'є розлючених ворогів, яких бачить, до {2} м. Б'є не своєю силою, а запасом варти: він набирається на жердині першим (до половини сили ворона), удар — {3}. Потім набирається й власна сила, повільніше, ніж на звичайній жердині, зате до повної. Вилітає лише ситий: їсть із годівниці поруч, а без неї полює сам, та лишається голодним і сидить, хоч би й запас був повний. Ситий раз на день кладе в годівницю знахідку, а за кілька днів без годівниці сумує й тратить довіру.\n\nПосеред бою на варту не сідає. Покликати його звідти не можна: прийди й забери (E на жердині). Зламають жердину без тебе — він полетить до Одіна.", GoldHex, Mathf.RoundToInt(Home.SettleSeconds / 60f), Mathf.RoundToInt(Home.GuardRadius), Mathf.RoundToInt(Home.GuardCost))
                : Lang.F("<b><color={0}>Вдома</color></b>\n\nВорон живе на жердині й не ображається, що його нема на плечі. Спершу набирає запас варти (до половини своєї сили), потім власну силу — повільніше, ніж на звичайній посадці, зате до повної. Обжившись ({1} хв), б'є розлючених ворогів, яких бачить, до {2} м: удар — {3} запасу, дорожче, ніж на вартовій жердині. Крізь стіни не бачить: у хаті стереже лише кімнату. Вилітає лише ситий: їсть із годівниці поруч, а без неї полює сам, та лишається голодним і сидить, хоч би й запас був повний. Ситий раз на день кладе в годівницю знахідку, а за кілька днів без годівниці сумує й тратить довіру.\n\nЗабрати його можна лише особисто: E на жердині. Зламають жердину без тебе — він полетить до Одіна.", GoldHex, Mathf.RoundToInt(Home.SettleSeconds / 60f), Mathf.RoundToInt(Home.HomeRadius), Mathf.RoundToInt(Home.HomeCost));
        }

        // ---------------------------------------------------------------- the cards

        // «Журнал»: the raven at home — where, who it is, its numbers, and what it did since you were by, a row each
        private static void HomeCard(RectTransform by, string token)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            Home.Note n = Home.NoteOf(p, token);
            if (n == null) return;
            bool guard = n.Mode == Home.OnGuard, m = n.Munin;
            string name = Plugin.RavenName(m);
            Card c = OpenCard("HomeCard", Lang.F("Журнал · {0}", guard ? Lang.F("{0} на варті", name) : Lang.F("{0} вдома", name)), 460f, false);
            Func<string> where = () => HomeWhere(token);
            Ticking(T(c.Paper, where() ?? "", 15f, PSoft, F.Italic), where);
            Backup.Lost copy = Backup.Ravens(p).Find(x => x.Id == n.Rid);
            if (copy != null) WhichRaven(c.Paper, copy.Copy, m, n.Level > 0 ? n.Level : copy.Level);
            CardRow(c.Paper, Lang.T("Запас варти"), () => { Home.Note x = Home.NoteOf(Player.m_localPlayer, token); return x == null ? null : Mathf.FloorToInt(x.Store) + " / " + Mathf.RoundToInt(x.Max * Home.StoreShare); });
            CardRow(c.Paper, Lang.T("Сила"), () => { Home.Note x = Home.NoteOf(Player.m_localPlayer, token); return x == null ? null : Mathf.FloorToInt(x.Strength) + " / " + Mathf.RoundToInt(x.Max); });
            CardRow(c.Paper, Lang.T("Ситість"), () => { Home.Note x = Home.NoteOf(Player.m_localPlayer, token); return x == null ? null : Mathf.RoundToInt(x.Sat) + " / 100"; });
            List<string> parts = Home.ReportParts(n.Log);
            T(c.Paper, "<b>" + Lang.T("Поки тебе не було") + "</b>", 15f, PLabel, F.Body);
            if (parts.Count == 0) T(c.Paper, Lang.T("нічого не сталося"), 14f, PSoft, F.Italic);
            foreach (string s in parts) { CardRule(c.Paper); T(c.Paper, Lang.UpperFirst(s), 14f, PInk, F.Body); }
            T(c.Paper, Lang.T("(як було, коли ти востаннє бу{в|ла} поруч)"), 12f, A(PSoft, 0.8f), F.Italic);
            TMP_Text how = T(c.Paper, "<b>" + (guard ? Lang.T("Як стереже") : Lang.T("Як живе вдома")) + "</b>", 14f, GoldInk, F.Body);
            Tip(how.gameObject, () => HomeTip(guard));
            PlaceCard(c, by);
        }

        // «Повернути»: a lost raven — who it was, the price, what it takes now; «Скасувати» / «Повернути»
        private static void ReturnCard(RectTransform by, string id)
        {
            ClosePopup();
            HideTip();
            Player p = Player.m_localPlayer;
            Backup.Lost l = Backup.Ravens(p).Find(x => x.Id == id);
            if (l == null) return;
            bool m = l.Munin;
            Card c = OpenCard("ReturnCard", Lang.F("Повернути {0}", (m ? Lang.T("Муніна") : Lang.T("Хугіна"))), 460f, true);
            RectTransform head = Rect("Head", c.Paper);
            HBox(head, 12f, TextAnchor.MiddleLeft);
            RectTransform pic = Rect("Portrait", head);
            Fixed(pic.gameObject, 64f, 64f);
            Img(pic, "Raven", BookArt.Portrait(m) ?? RavenItem.Icon(m), new Color(0.62f, 0.58f, 0.52f, 0.6f)).preserveAspect = true; // (gone: grey, faded)
            RectTransform who = Rect("Who", head);
            VBox(who, 2f, TextAnchor.MiddleCenter);
            who.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            WhichRaven(who, l.Copy, m, l.Level);
            string price = ReturnPrice(p, l);
            CardRow(c.Paper, Lang.T("Ціна"), () => "<b><color=" + GoldInkHex + ">" + price + "</color></b>");
            bool atStones = Trust.AtStones(p);
            bool can = atStones && l.DaysLeft == 0 && l.Price != null && Trust.Have(p, l.Price.Prefab) >= l.Price.Count;
            string why = l.DaysLeft > 0 ? Lang.F("лишилось {0} дн. до наступного повернення", l.DaysLeft) : !atStones ? Lang.T("підійди до Жертовних каменів") : can ? Lang.T("усе готово") : Lang.T("бракує трофеїв");
            CardRule(c.Paper);
            T(c.Paper, why, 14f, can ? C(PGoodHex) : Bad, F.Body); // what it takes: green when all is there
            T(c.Paper, Lang.F("Повернеться з тим самим рівнем довіри, вишколом і переможеними босами; шкала до наступного рівня почнеться з нуля, частину спогадів він забуде. Не частіше ніж раз на {0} днів.", Backup.RestoreDays), 13f, PSoft, F.Italic);
            CardButton(c.Foot, Lang.T("Скасувати"), false, ClosePopup);
            CardButton(c.Foot, Lang.T("Повернути"), true, () => { ClosePopup(); Act(() => Backup.Restore(p, id)); }, "ᚱ", can);
            PlaceCard(c, by);
        }

        // more than one not with you: the lines, each opening its own card (over the page's line: this card is gone then)
        private static void FootListCard(RectTransform by, bool m, bool lost)
        {
            ClosePopup();
            HideTip();
            List<Home.Note> homes;
            List<Backup.Lost> gone;
            NotWithYou(m, lost, out homes, out gone);
            Card c = OpenCard("FootCard", m ? Lang.T("Мунін: де вони") : Lang.T("Хугін: де вони"), 480f, false);
            foreach (Backup.Lost l in gone) LostLine(c.Paper, l, by); // (the lost first)
            foreach (Home.Note n in homes) HomeLine(c.Paper, n, by);
            PlaceCard(c, by);
        }

        // a row of a card: its name on the left, its number on the right, a dotted rule over it
        private static void CardRow(Transform paper, string label, Func<string> value)
        {
            CardRule(paper);
            RectTransform r = Rect("Row", paper);
            HBox(r, 10f, TextAnchor.MiddleLeft);
            T(r, label, 14f, PInk, F.Body, TextAlignmentOptions.MidlineLeft).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text v = T(r, value() ?? "", 14f, PInk, F.Body, TextAlignmentOptions.MidlineRight);
            v.textWrappingMode = TextWrappingModes.NoWrap;
            Ticking(v, value);
        }

        private static void CardRule(Transform paper)
        {
            Image d = Img(paper, "Rule", BookArt.Dash(true), A(PRule, 0.9f));
            d.type = Image.Type.Tiled; d.pixelsPerUnitMultiplier = 2f;
            Fixed(d.gameObject, -1f, 2f);
        }
    }
}
#endif

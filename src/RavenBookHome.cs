#if RAVEN_ITEM
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HuginWhispers
{
    // The book's page of a raven not on you: where each of that kind left at home sits (Home), what it is like there
    // and what it did since you were by. It is fetched in person only (E at its perch), at home as on guard.
    internal static partial class RavenBook
    {
        private static void HomeRows(RectTransform col, bool m)
        {
            Player p = Player.m_localPlayer;
            List<Backup.Lost> copies = null; // (who each one is: its copy in your character)
            foreach (Home.Note n in Home.Notes(p))
            {
                if (n.Munin != m || n.Odin) continue; // (gone to Odin: the return row below has it)
                DashedLine(col);
                RectTransform box = Rect("Home", col);
                VBox(box, 4f, TextAnchor.UpperCenter, 0, 0, 6, 0).childForceExpandWidth = false;
                bool guard = n.Mode == Home.OnGuard;
                TMP_Text head = T(box, guard ? Lang.F("{0} на варті", Plugin.RavenName(m)) : Lang.F("{0} вдома", Plugin.RavenName(m)), 19f, Ink, F.Head, TextAlignmentOptions.Center);
                Tip(head.gameObject, () => guard
                    ? Lang.F("<b><color={0}>На варті</color></b>\n\nПерші {1} хв ворон обживається, а тоді стереже вартову жердину: б'є розлючених ворогів, яких бачить, до {2} м. Б'є не своєю силою, а запасом варти: він набирається на жердині першим (до половини сили ворона), удар — {3}. Потім набирається й власна сила, повільніше, ніж на звичайній жердині, зате до повної. Вилітає лише ситий: їсть із годівниці поруч, а без неї полює сам, та лишається голодним і сидить, хоч би й запас був повний. Ситий раз на день кладе в годівницю знахідку, а за кілька днів без годівниці сумує й тратить довіру.\n\nПосеред бою на варту не сідає. Покликати його звідти не можна: прийди й забери (E на жердині). Зламають жердину без тебе — він полетить до Одіна.", GoldHex, Mathf.RoundToInt(Home.SettleSeconds / 60f), Mathf.RoundToInt(Home.GuardRadius), Mathf.RoundToInt(Home.GuardCost))
                    : Lang.F("<b><color={0}>Вдома</color></b>\n\nВорон живе на жердині й не ображається, що його нема на плечі. Спершу набирає запас варти (до половини своєї сили), потім власну силу — повільніше, ніж на звичайній посадці, зате до повної. Обжившись ({1} хв), б'є розлючених ворогів, яких бачить, до {2} м: удар — {3} запасу, дорожче, ніж на вартовій жердині. Крізь стіни не бачить: у хаті стереже лише кімнату. Вилітає лише ситий: їсть із годівниці поруч, а без неї полює сам, та лишається голодним і сидить, хоч би й запас був повний. Ситий раз на день кладе в годівницю знахідку, а за кілька днів без годівниці сумує й тратить довіру.\n\nЗабрати його можна лише особисто: E на жердині. Зламають жердину без тебе — він полетить до Одіна.", GoldHex, Mathf.RoundToInt(Home.SettleSeconds / 60f), Mathf.RoundToInt(Home.HomeRadius), Mathf.RoundToInt(Home.HomeCost)));
                if (copies == null) copies = Backup.Ravens(p);
                Backup.Lost copy = copies.Find(x => x.Id == n.Rid);
                if (copy != null) WhichRaven(box, copy.Copy, m, n.Level > 0 ? n.Level : copy.Level);
                // the way to it, its numbers and its tale: written again every second (the page itself only for news)
                string token = n.Token;
                System.Func<string> where = () => { Player me = Player.m_localPlayer; Home.Note x = Home.NoteOf(me, token); if (x == null) return null; return !x.Here ? Lang.T("в іншому світі") : me != null ? Lang.F("«{0}» · {1} м звідси", x.Perch, Mathf.RoundToInt(Vector3.Distance(me.transform.position, x.Pos))) : "«" + x.Perch + "»"; };
                Ticking(T(box, where(), 14f, InkSoft, F.Italic, TextAlignmentOptions.Center), where);
                System.Func<string> stats = () => { Home.Note x = Home.NoteOf(Player.m_localPlayer, token); return x == null ? null : Lang.F("запас варти {0}/{1} · сила {2}/{3} · ситість {4}", Mathf.FloorToInt(x.Store), Mathf.RoundToInt(x.Max * Home.StoreShare), Mathf.FloorToInt(x.Strength), Mathf.RoundToInt(x.Max), Mathf.RoundToInt(x.Sat)); };
                Ticking(T(box, stats(), 14f, InkSoft, F.Body, TextAlignmentOptions.Center), stats);
                if (n.Log.Length > 0)
                {
                    System.Func<string> tale = () => { Home.Note x = Home.NoteOf(Player.m_localPlayer, token); return x == null ? null : Home.Report(x.Log, m, x.Mode); };
                    Ticking(T(box, tale(), 13f, InkSoft, F.Body, TextAlignmentOptions.Center), tale);
                }
                T(box, Grammar.Fix(Lang.T("(як було, коли ти востаннє бу{в|ла} поруч)"), p), 12f, A(InkSoft, 0.7f), F.Italic, TextAlignmentOptions.Center);
            }
        }
    }
}
#endif

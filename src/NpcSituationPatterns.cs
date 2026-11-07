#if RAVEN_ITEM && RAVEN_NPC
namespace HuginWhispers
{
    // People's words by situation (the user 2026-11-05: as varied as the ravens' — the same themes, their details and nuances by what
    // goes on). Read after NpcPhrases' own and NpcTalkPatterns, into the same book: a theme's templates here are added to its own, so its
    // old words stay and these come in their turn. Two kinds of what is added:
    //   a situation   [theme ?cond, cond] — only while it holds: its own state and the world's (NpcPhrases.Situations: daypart, weather,
    //                 mood low|mid|high, hungry, tired, seated, working, feel <kind>, friendnear, raid…), heard by any player
    //   a build       [theme] {вигук?} {core} {tail?} — the theme's own list as its heart, a beginning and an end from the shared lists
    //                 (@вигук, @думка_кінець…) or a situation's own (@с_…)
    // Lists named @с_… are this file's. Seeded with a few lines each (the user: situations and templates first, their number of lines in the
    // next pass). «а|б» the speaker's form, «{а|б}» yours; a name as it is, never after «з», «у», «через».
    internal static class NpcSituationPatterns
    {
        internal const string Text = @"
// ---- its greeting (see_player: {привіт} its list, {про_тебе?} a question to you)
[see_player] {вигук?} {привіт} {про_тебе?}
[see_player ?daypart morning] {с_привіт_ранок} {про_тебе?}
[see_player ?daypart evening] {с_привіт_вечір} {про_тебе?}
[see_player ?daypart night] {с_привіт_ніч}
[see_player ?weather rain] {привіт} {с_привіт_дощ}
[see_player ?mood low] {с_привіт_похмуро}
[see_player ?mood high] {с_привіт_весело} {про_тебе?}
[see_player ?hungry] {привіт} {с_натяк_голод}
[see_player ?tired] {привіт} {с_натяк_втома}
[see_player ?working] {привіт} {робота_кінець}
@с_привіт_ранок
Доброго ранку.
О, ранок, а ти вже на ногах.
~бурч Ранок. Ще не прокинувся|прокинулася як слід.
~весел Ранок добрий! Принаймні поки що.
@с_привіт_вечір
Добрий вечір.
Вечір уже. Ти звідки так пізно?
~стар Сонце сідає. Сідай і ти, як маєш час.
@с_привіт_ніч
Ніч надворі, а ти ходиш.
Тихіше, люди сплять.
~боязк Ох, це ти… Я вже злякався|злякалася.
@с_привіт_дощ
{Мокрий|Мокра} {весь|вся}, як і я.
Заходь під дах, бо змокнеш.
~бурч Не капай на мене.
@с_привіт_похмуро
А, це ти.
Привіт. Якщо можна так сказати.
~бурч Чого тобі?
@с_привіт_весело
О, ось і ти!
Радий|Рада тебе бачити!
~весел Нарешті хтось веселий… тобто ти!
@с_натяк_голод
Поїсти б щось, до речі.
У тебе часом нема шматка хліба?
~бурч Шлунок бурчить голосніше за мене.
@с_натяк_втома
Ледве стою, не зважай.
Сили вже на денці.
~стар Кості гудуть.

// ---- what it thinks aloud (idle: {думка} its list)
[idle ?daypart evening] {вигук?} {с_думка_вечір} {думка_кінець?}
[idle ?daypart night] {с_думка_ніч}
[idle ?weather rain] {вигук?} {с_думка_дощ}
[idle ?seated] {вигук?} {с_думка_сидячи} {думка_кінець?}
[idle ?mood high] {вигук?} {с_думка_весело}
[idle ?mood low] {с_думка_сумно} {думка_кінець?}
[idle ?friendnear] {с_про_друга_поруч}
@с_думка_вечір
Ще один день минув.
Вечір — найкращий час. Робота позаду.
~стар Вечорами згадується всяке.
@с_думка_ніч
Не спиться щось.
Тихо як. Аж моторошно.
~боязк Що це там шурхотить?
@с_думка_дощ
Ллє і ллє.
Добре, що дах не тече. Поки що.
~весел Дощ — то й добре, грядки поллє.
@с_думка_сидячи
Добре отак посидіти.
Ноги кажуть дякую.
~бурч Сів|Сіла — і вже не хочеться вставати.
@с_думка_весело
Гарний день сьогодні.
А жити таки можна!
~весел Співати хочеться. Не буду. Поки що.
@с_думка_сумно
Щось на душі важко.
Не мій сьогодні день.
~бурч І чого я сюди прийшов|прийшла.
@с_про_друга_поруч
Добре, що {friend_near} поруч.
Поруч {friend_near} — то й не засумуєш. Ну, майже.
~весел {friend_near}, ти чуєш? Кажу, добре, що ти тут!

// ---- weather (weather: {погода} its list)
[weather ?weather rain] {с_дощ} {погода_кінець?}
[weather ?weather snow] {с_сніг} {погода_кінець?}
[weather ?weather storm] {с_буря} {погода_кінець?}
[weather ?weather fog] {с_туман}
@с_дощ
Знову дощ. Усе мокре.
Хмари низько. Ще поллє.
@с_сніг
Сніг. Ну, принаймні не дощ.
Білим-біло. Ноги мерзнуть.
@с_буря
Гримить так, що аж земля трясеться.
Ну й буря. Сидімо тихо.
~боязк Ой, як гримить…
@с_туман
Туман — хоч око виколи.
У такий туман краще далеко не ходити.

// ---- its work (work: {робота}; work_<job>: its own lists)
[work ?tired] {робота} {с_робота_втома}
[work ?mood high] {робота} {с_робота_весело}
[work ?mood low] {робота} {с_робота_похмуро}
[work_cook] {кухня} {робота_кінець?}
[work_stoker] {вогонь} {робота_кінець?}
[work_smelter] {піч} {робота_кінець?}
[work_gatherer] {збір} {робота_кінець?}
[work_cleaner] {прибирання} {робота_кінець?}
[work_keeper] {бджоли} {робота_кінець?}
[work_guard] {варта} {робота_кінець?}
[work_guard ?daypart night] {варта} {с_варта_ніч}
@с_робота_втома
Руки вже не слухаються.
Ще трохи — і впаду.
@с_робота_весело
Аж в руках горить!
Сьогодні робота йде.
@с_робота_похмуро
Робиш, робиш — а кому воно треба.
Абияк сьогодні йде.
@с_варта_ніч
Ніч довга. Очі злипаються.
Уночі кожен кущ — ворог.

// ---- hunger, tiredness, the hour (their own lists as the heart)
[hungry] {вигук?} {голод} {с_голод_кінець?}
[tired] {вигук?} {втома} {втома_кінець?}
[morning ?mood high] {ранок} {с_думка_весело}
[evening ?seated] {вечір} {с_думка_сидячи}
[night] {вигук?} {ніч}
[rain] {вигук?} {дощ} {погода_кінець?}
@с_голод_кінець
Хоч би хтось зварив щось.
~бурч А комора, звісно, порожня.
~весел Ну, нічого, вечеря ж буде. Буде ж?

// ---- «Як справи?», «Ще щось?» (its talk)
[how_work ?tired] {справи_робота} {с_робота_втома}
[how_work] {справи_робота} {робота_кінець?}
[how_good ?friendnear] {справи_добре} {с_про_друга_поруч}
[how_idle ?seated] {справи_без_діла} {с_думка_сидячи}
[talk_again] {ще_щось} {думка_кінець?}

// ---- two of them talking (chat_open / chat_reply; q_/a_ a topic's question and answer)
[chat_open ?daypart evening] {с_розмова_вечір}
[chat_open ?weather rain] {с_розмова_дощ}
[chat_reply] {вигук?} {відповідь}
[a_food] {вигук?} {в_їжа} {думка_кінець?}
[a_sleep] {вигук?} {в_сон}
[a_work] {вигук?} {в_робота} {думка_кінець?}
[a_weather] {вигук?} {в_погода}
[a_ravens] {вигук?} {в_ворони}
[a_home] {вигук?} {в_дім} {думка_кінець?}
@с_розмова_вечір
Ну, як день минув?
Посидимо трохи?
~стар Розкажи щось на ніч.
@с_розмова_дощ
Ну й ллє, правда?
Дах не тече у вас?

// ---- its talk's own (NpcTalk nodes: the words they were kept as one of them)
[who_intro] {хто_я}
@хто_я
Мене звати {npc}.
Я — {npc}.
{npc}. Так мене кличуть.
~бурч {npc}. А тобі навіщо?
~весел {npc}! Приємно, що питаєш.
~стар Звуть мене {npc}. Давно вже так звуть.
[rescue_wait] {чекаю_бій}
@чекаю_бій
Спершу прожени їх! Я… я тут, нікуди не дінусь.
Бий їх! Я не втечу, обіцяю.
~боязк Швидше, благаю… я тут, я чекаю.
~смілив Давай, я прикрию спину, як зможу!
[rescue_guard] {що_мені}
@що_мені
А мені що робити, поки тебе нема? Сам|Сама я тут довго не протримаюсь.
Ти підеш? А я? Сам|Сама я довго не вистою.
~боязк Не лишай мене тут самого|саму… Що мені робити?
";
    }
}
#endif

#if RAVEN_ITEM && RAVEN_NPC
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HuginWhispers
{
    // Residents' small talk (npc-plan.md, «Голод і їдальня»): two sitting side by side — at a table over a meal, by the
    // fire in the evening — now and then talk, a line each in turn over their heads (NpcBubble: one capsule for the talk). And lines of one
    // person alone: grumbling when hungry, a word at a meal, a word when it leaves. The words are plain text (built in
    // below; the author's own in config/RavenWatch/talk/chatter*.txt, read while tuning, are added to them). Each line is put through
    // Lang.TP where it is said: another language's are rows of its ui.txt (tools/l10n.js reads Builtin's lines for them):
    //   [chat table]          a talk at a table (fire: by the fire; any: either); talks part by an empty line,
    //   - Хто це смажив?      its lines «- » in turn: the one who starts, the other, the one who starts…
    //   - Кухар.
    //   ? folk cook, !raid    a line «? …» in a talk: only while these hold (StoryCond, «he» the one who starts: the moment
    //                         — day, night, daypart, weather, raid — and its home — folk, job, hired)
    //   [say hungry]          one person's lines, one per line (hungry · starving · nofood · meal · leave)
    //   [chat quarrel]        words between two at odds, or ill-matched and both low (Spat): it cools them, not warms (NpcAttitude.Quarrel)
    //   [meet evening]        a gathering's talks (the user 2026-11-16: the story's people together by the fire — StoryMeets says who is there
    //   heid: Тихо сьогодні.  and when): each line by the one named (its template's id), two or more of them; a talk is said only while all it
    //   torvald: Ага.         names are there. The texts come from a module (More: assets/story/meets), and these people's own words are theirs:
    //                         one that says nothing of the common book (NpcVoice.OwnWordsOnly) speaks here all the same
    //   [pair lit torvald]    a pair's short exchanges (the user 2026-11-16: two of the story's people near each other outside a gathering — StoryMeets says
    //                         when): written as a gathering's, its speakers only these two (the pair's key: PairKey, the two in any order).
    //                         A talk naming one that keeps quiet now (NpcActor.Hushed: not met yet, a scene near) is not picked: it would be heard by halves
    //   torvald: {told torvald story_}   a gathering's line that is a place of a talk the nearest player has not heard (Tells: a true tale by the fire —
    //                         StoryMeets), said with which it is, so those near have heard it (NpcActor.Told). Meet's «first»: a module's word
    //                         which talks go first (a player just sat down in the circle: «guest»)
    //   [road torvald_home]   a person's talks to a player who walks with it (the user 2026-11-18: StoryRoad says when and runs them — not here):
    //   torvald: …            written as a gathering's, «hero: …» a line of the player himself; read here (Road), said there
    // Who starts: the one of the two the game runs here with the lower id (never both); how often: their chattiness.
    // Light: a look round every few seconds per resident, none with no player in earshot; the lines are the actor's own Say
    // (one message each). A talk is not picked again soon (Lately); a talk the composer makes (its lines «{@q_food}»…) is picked first
    // more often than a written one (MadeFirst), so the written stories stay rare.
    internal static class NpcChatter
    {
        internal static readonly List<Func<NpcActor, NpcActor, bool>> Shuns = new List<Func<NpcActor, NpcActor, bool>>(); // a module's word that these two do not talk now (NpcNeighbours: kept apart)
        internal static readonly List<Func<NpcActor, NpcActor, string, bool>> Spats = new List<Func<NpcActor, NpcActor, string, bool>>(); // a module's word on a spat beginning (the two, where: meet · table · fire — NpcNeighbours: a scuffle); true: its own quarrel instead of the table's (a pair quarrels once a day: NpcAttitude.QuarrelAct)
        private static bool Shunned(NpcActor a, NpcActor o) { foreach (Func<NpcActor, NpcActor, bool> f in Shuns) if (f(a, o)) return true; return false; }
        internal sealed class Chat { public string[] Said; public string[] By; public List<Cond> When; } // (By: who says each line — a gathering's talk; null: the two in turn)
        // a road's talks («[road <name>]», in the order written; StoryRoad says them): none — empty
        private static readonly List<Chat> NoTalks = new List<Chat>();
        internal static List<Chat> Road(string name) { Lines l = Of("road " + name); return l != null ? l.Talks : NoTalks; }
        // a module's own texts in this format, read with the built-in ones (the story's gatherings: StoryModules)
        internal static readonly List<Func<IEnumerable<string>>> More = new List<Func<IEnumerable<string>>>();
        private sealed class Lines { public readonly List<Chat> Talks = new List<Chat>(); public readonly List<string> One = new List<string>(); }

        private static readonly string Builtin =
            "[chat table]\n" +
            "? folk cook\n- Хто це смажив?\n- Кухар.\n- Передай йому, що м'ясо ще пручалося.\n\n" +
            "- Знову риба.\n- Учора теж була риба.\n- То це та сама?\n\n" +
            "- Смачно.\n- Ти завжди так кажеш.\n- Бо мені завжди мало.\n\n" +
            "- Хліб сьогодні м'який.\n- Бо хтось нарешті дров у піч підклав.\n\n" +
            "- Їж повільніше, ніхто не забере.\n- У мене вже пів шматка кудись зникло.\n\n" +
            "- Як думаєш, Одін теж отак сидить і їсть?\n- Він тільки п'є. А м'ясо віддає вовкам.\n- Тоді нам ще пощастило.\n\n" +
            "- Добре, що є що їсти.\n- Добре, що є з ким.\n\n" +
            "- Завтра знову на роботу.\n- Не нагадуй, бо шматок у горлі стане.\n\n" +
            "? folk cook\n- Скажи кухарю, що солі мало.\n- От піди й скажи, у нього ніж.\n\n" +
            "- Мій дід колись цілого кабана з'їв.\n- Сам?\n- З друзями. Але найбільше — дід.\n\n" +
            "- Чуєш уночі вовків?\n- Чую. А мені здавалося, що то ти хропеш.\n\n" +
            "- Що там у казані?\n- Краще не питай. Їж, поки тепле.\n\n" +
            "[chat fire]\n" +
            "- Тепло.\n- Ага.\n- Отак би й сидіти до весни.\n\n" +
            "- Бачиш іскри? Кажуть, то душі полеглих летять до Вальгалли.\n- То чого вони гаснуть?\n- Не всі долітають.\n\n" +
            "- Розкажи щось.\n- Жив собі троль.\n- І що?\n- Досі живе. За отим пагорбом.\n\n" +
            "? night, weather clear\n- Зорі сьогодні ясні.\n- Отже, завтра буде холодно.\n\n" +
            "- Чуєш, як дрова тріщать?\n- Чую. Сирі хтось приніс.\n\n" +
            "- Драконицю колись бачити доводилось?\n- Раз. Більше не хочу.\n\n" +
            "- Добре, що в нас є дах.\n- І вогонь.\n- І ліжко. Ліжко — головне.\n\n" +
            "- Як твоя спина?\n- Як у старого віслюка. А твоя?\n- Як у віслюка, якого той віслюк возив.\n\n" +
            "- Знаєш, чому ворони кружляють над нами?\n- Чому?\n- Рахують нас. Про всяк випадок.\n\n" +
            "? !raid\n- Тихо сьогодні.\n- Не кажи вголос, бо почують.\n\n" +
            "[say hungry]\n" +
            "Шлунок бурчить, як троль у печері.\nПоїсти б…\nХоч би сухарик якийсь.\nКоли вже вечеря?\nНа порожній шлунок і сокира важча.\n" +
            "[say starving]\n" +
            "Ноги не тримають — їсти нема чого.\nЯ так довго не протягну.\nБез їжі не працюю. Пробач.\nЩе день без хліба — і піду звідси.\n" +
            "[say nofood]\n" +
            "Комора порожня… Хто з'їв усе готове?\nНема готової їжі. Розпаліть хтось піч!\nЇдальня є, а їсти нема чого.\n" +
            "[say meal]\n" +
            "О, оце діло.\nНу нарешті.\nСмачного всім.\n" +
            "[say need_surrounded]\n" +
            "Допоможіть! Хто-небудь!\nВідійдіть від мене, тварюки!\nЯ тут! Сюди!\n" +
            "[say need_captive]\n" +
            "Розв'яжіть мене, благаю!\nВони мене з'їсти хочуть, чи що?\nХто там? Допоможи!\n" +
            "[say need_hungry]\n" +
            "Хоч шматочок хліба…\nТри дні нічого не їв|їла.\nНоги не несуть…\n" +
            "[say need_wounded]\n" +
            "Ох… болить…\nМеду б… лікувального…\nНе підходьте… я ще живий|жива…\n" +
            "[say need_lost]\n" +
            "Де ж моя річ… без неї мені кінець.\nВони там, біля неї. Сам|Сама не піду.\nПоможи, га? Я віддячу.\n" +
            "[say need_freezing]\n" +
            "Х-холодно… пальців не чую.\nВогню б… хоч трішки вогню.\nЗуби цокочуть так, що вовки чують.\n" +
            "[say need_chased]\n" +
            "Рятуй! Вони за мною!\nБіжу до тебе — не кидай!\nШвидше, вони близько!\n" +
            "[say need_giant]\n" +
            "Не ворушись… воно дивиться.\nВоно мене стереже, як здобич.\nХто-небудь, допоможіть!\n" +
            "[say need_astray]\n" +
            "Ге-ей! Є тут хтось?\nЯ тут! Тут!\nДе я взагалі?..\nАго-ов!\n" +
            "[say need_ambush]\n" +
            "Допоможіть… я поранений|поранена…\nСюди… будь ласка…\nНе залишай мене тут…\n" +
            "[say saved]\n" +
            "Дякую! Я вже думав|думала — все.\nЖивий|Жива! Боги, живий|жива!\nЯ твій|твоя боржник|боржниця.\n" +
            "[say mend]\n" +
            "Хто ж так стіни б'є…\nЗалатаємо — буде як нове.\nДе два цвяхи, там і третій.\nОт і ціле. Ну, майже.\n" +
            "[say tend_beast]\n" +
            "На, їж, бо зовсім схуд.\nНе кусайся, я ж із добром.\nГодуєш їх, годуєш — а вони все одно дивляться, ніби ти винен.\n" +
            "[say tend_ravens]\n" +
            "Тримайте, чорні. Тільки не бийтеся.\nЦе Муніну м'ясо, а Хугіну — ягоди. Знаю, знаю.\nВорони в годівниці — ворони не на голові.\n" +
            "[say wash]\n" +
            "Брр, холодна!\nОт тепер можна й до людей.\nЗмити б іще й утому…\nОх, хороша вода.\n" +
            "[chat any]\n" +
            "- {@chat_open}\n- {@chat_reply}\n\n" +
            "- {@chat_open}\n- {@chat_reply}\n- {@chat_reply}\n\n" +
            // 2026-11-13: what their pay is for — they owe, swap, put by (the made ones: q_coins, NpcMoreCoinPatterns)
            "- З тебе ще дві монети.\n- Які дві? Я ж тобі рибою віддав|віддала.\n- Риба була вчорашня. Отже, одна.\n\n" +
            "- Позич монету.\n- А минулу коли віддаси?\n- От позичиш — тоді й віддам.\n\n" +
            "- Чого дзвениш, як коза з дзвіночком?\n- Платню в пояс зашив|зашила.\n- Тепер уся оселя знає, де твоя платня.\n\n" +
            "- Як хтось піде до торговця, передам монети на ніж.\n- І я передам. На два.\n- Нащо тобі два?\n- Один — щоб різати. Другий — щоб тобі не позичати.\n\n" +
            "? job cook\n- Добавки хочеш?\n- А почім?\n- Для тебе — як завжди.\n- Тобто дорожче, ніж для всіх.\n\n" +
            "[chat meet]\n" +
            "- Як воно?\n- Та живемо.\n- Ну й добре.\n\n" +
            "- Дим над пагорбом помітно?\n- Помітно. Хтось смолу варить.\n- Або троль обідає.\n\n" +
            "- Знову ти.\n- Знову я.\n- Ну то йди вже своєю дорогою.\n\n" +
            "- Що нового?\n- Курка втекла.\n- У нас нема курей.\n- От і я про те.\n\n" +
            "- А ворони сьогодні тихі.\n- Не наврочи.\n\n" +
            "? weather clear\n- Погода яка!\n- Яка?\n- Отака.\n\n" +
            "- Ти куди?\n- Туди.\n- А я звідти. Нічого там цікавого.\n\n" +
            "- Ложки моєї ніхто не бачив?\n- Бачили. Учора. У тебе в руці.\n\n" +
            "- Дерева ще рубати й рубати.\n- Дерева не закінчуються. На відміну від нас.\n\n" +
            "[say leave]\n" +
            "Не годуєте — піду туди, де годують. Бувайте.\nГолодним не наробишся. Прощавайте.\n" +
            "Без їжі я тут не житиму. Шукайте іншого дурня.\nПіду, поки ще ноги носять.\nТут, видно, людей не годують. Бувайте здорові.\n" +
            // 2026-11-01: more of everything (a line about the other one: no he|she forms — they would be the speaker's)
            "[chat table]\n" +
            "- Хто сьогодні мив миски?\n- Ніхто.\n- То чому вони чисті?\n- Ворони вилизали.\n\n" +
            "- Передай сіль.\n- Нема солі.\n- То передай хоч ложку, буду вдавати.\n\n" +
            "- Ти чого не їси?\n- Думаю.\n- Про що?\n- Чи буде добавка.\n\n" +
            "- Колись я їв|їла пиріг із журавлиною.\n- І що?\n- Нічого. Просто згадалось.\n\n" +
            "- Цей хліб твердий, як щит.\n- Зате до завтра не зіпсується.\n\n" +
            "- Добре, коли є стіл.\n- І лава.\n- І той, хто все це збудував.\n\n" +
            "- Мед є?\n- Є. Тільки його бджоли не віддають.\n\n" +
            "- Сьогодні м'ясо, завтра м'ясо…\n- Не скаржся. Позавчора не було нічого.\n\n" +
            "- Хто останній — той прибирає.\n- То я ще посиджу. Повільно.\n\n" +
            "- Їси, як троль.\n- Дякую.\n- То не комплімент.\n- Для троля — комплімент.\n\n" +
            "- Чуєш, а хто перший сів за цей стіл?\n- Не знаю. Але місце біля вогню — моє.\n\n" +
            "- Ти щось сьогодні все мовчиш.\n- Їм.\n- А, ну тоді добре.\n\n" +
            "[chat fire]\n" +
            "? night\n- Чуєш, як ліс дихає вночі?\n- Чую. Краще б він спав.\n\n" +
            "- Колись я бачив|бачила, як падає зоря.\n- І яке бажання?\n- Щоб не впала на мене.\n\n" +
            "- Вогонь сьогодні неспокійний.\n- Це вітер.\n- Або хтось думає про нас.\n\n" +
            "- Як гадаєш, звідки ми тут?\n- Валькірії принесли.\n- А звідки вони нас узяли?\n- Не питай того, на що нема відповіді.\n\n" +
            "- Заспівай щось.\n- Не вмію.\n- То тим смішніше буде.\n\n" +
            "- Я вчора знову чув|чула той голос уві сні.\n- Який?\n- Той, що каже: можна все забути й відпочити.\n- Не слухай. Тримайся за те, що маєш.\n\n" +
            "? weather clear\n- Дим іде прямо вгору.\n- Отже, завтра ясно.\n- Або що вітру нема.\n- Ну так, я ж і кажу.\n\n" +
            "- Мені тут подобається.\n- Мені теж.\n- Тільки не кажи нікому, а то ще більше роботи дадуть.\n\n" +
            "- Пам'ятаєш, як ми тут уперше сіли?\n- Ага. Тоді й вогню путнього не було.\n\n" +
            "- Подай поліно.\n- Та дотягнися вже.\n- Ти ближче.\n- А в тебе ноги молодші.\n\n" +
            "? night\n- Знаєш, що найкраще в ночі?\n- Що?\n- Що вона закінчується.\n\n" +
            "- Цікаво, чи ворони мерзнуть.\n- У них пір'я.\n- У мене теж шуба, а я мерзну.\n\n" +
            "[chat meet]\n" +
            "- Куди йдеш?\n- По дрова.\n- І я.\n- То чого ми стоїмо?\n\n" +
            "- Кабана біля паркану бачили?\n- Бачив|Бачила. Він мене теж.\n\n" +
            "? daypart morning\n- Ти сьогодні ранній птах.\n- Не спалося.\n- Мені теж. То хтось хропів.\n\n" +
            "- О, ти тут.\n- А де мені ще бути?\n- Та мало де. Світ великий.\n\n" +
            "- Новину чули?\n- Яку?\n- Нема новин.\n- Ну й добре.\n\n" +
            "? daypart day\n- Гарний ранок.\n- Вже обід.\n- То гарний обід.\n\n" +
            "- Мою сокиру ніхто не брав?\n- Ніхто. Вона там, де ти її завжди лишаєш.\n- А де я її лишаю?\n- Отож.\n\n" +
            "- Глянь, ворон на даху.\n- Рахує нас.\n- Всі на місці?\n- Здається, так.\n\n" +
            "- Що, втома?\n- Ні.\n- А чого сидиш?\n- Відпочиваю від того, що не втомився|втомилась.\n\n" +
            "- Що робиш?\n- Думаю.\n- Про що?\n- Про те, чи не піти мені робити щось.\n- І?\n- Ще думаю.\n\n" +
            "? day, weather clear\n- Небо сьогодні яке синє.\n- Як очі валькірії.\n- Звідки знаєш?\n- Так кажуть.\n\n" +
            "? weather clear\n- Чого ти сьогодні хмуришся?\n- Погода.\n- Та гарна ж погода.\n- От це й гнітить.\n\n" +
            // topics: a question and an answer on the same thing (made each time — NpcPhrases q_*/a_*)
            "[chat any]\n" +
            "- {@q_sleep}\n- {@a_sleep}\n\n" +
            "- {@q_food}\n- {@a_food}\n\n" +
            "- {@q_food}\n- {@a_food}\n- {@chat_reply}\n\n" +
            "- {@q_work}\n- {@a_work}\n\n" +
            "- {@q_weather}\n- {@a_weather}\n\n" +
            "- {@q_ravens}\n- {@a_ravens}\n\n" +
            "- {@q_ravens}\n- {@a_ravens}\n- {@chat_reply}\n\n" +
            "- {@q_home}\n- {@a_home}\n\n" +
            "- {@q_home}\n- {@a_home}\n- {@chat_reply}\n\n" +
            "? hasjob\n- {@q_coins}\n- {@a_coins}\n\n" + // (the one who starts is paid: NpcToil pays only a worker)
            "? hasjob\n- {@q_coins}\n- {@a_coins}\n- {@r_coins}\n\n" +
            "[say starving]\n" +
            "Перед очима темніє… їсти б.\nЯ скоро й кору гризтиму.\nНу хоч щось! Хоч кістку!\nНе працюється на голодний шлунок. Ніяк.\n" +
            "[say nofood]\n" +
            "Відкрив|Відкрила скриню — а там вітер.\nХто знову все поїв? Хоч крихту б лишили.\nКазан пустий, комора пуста. І шлунок теж.\nНаготуйте хтось їжі, бо ми тут ноги простягнемо.\n" +
            "[say meal]\n" +
            "Тепле! Нарешті тепле.\nОт за це я й люблю цю оселю.\nСмачно. Хто готував — тому шана.\nЇмо, поки не охололо.\n" +
            "[say saved]\n" +
            "Ти прийш{ов|ла}… а я вже й не чекав|чекала.\nДякую. Боги свідки — не забуду.\nЯ живий|жива завдяки тобі.\n" +
            "[say tend_beast]\n" +
            "Хороший, хороший… тільки не дихай на мене.\nЇж, поки дають. Я теж так живу.\nНу от, ситий — і вже не такий страшний.\n" +
            "[say tend_ravens]\n" +
            "Ось вам, чорні. Тільки не кажіть Одіну, що я жадібний|жадібна.\nЇжте, птахи. І за мною приглядайте.\nГодівниця повна — ворони щасливі. Наче.\n" +
            "[say wash]\n" +
            "Ух, аж дух перехопило!\nОт тепер я людина. Майже.\nВода холодна, а на душі тепло.\n" +
            "[say mend]\n" +
            "Тук-тук — і як нове.\nЩе одна латка — і хата вічна.\nЛагоджу, лагоджу… Хто ж її так б'є?\n" +
            // 2026-11-05: words between two at odds (Spat; a line about the other one: no he|she forms — they would be the speaker's)
            "[chat quarrel]\n" +
            "- Де мої дві монети?\n- Які монети? Я їх в очі не бачив|бачила.\n- Отож. Бо одразу на мед пішли.\n\n" +
            "- Знову рахуєш мої монети?\n- Свої. Твоїх уже давно нема.\n- Бо дехто позичає й не віддає!\n\n" +
            "- Хто з'їв останню рибу?\n- Я. А що?\n- Нічого. Я просто з ранку не їв|їла.\n\n" +
            "- Ти знову на моєму місці.\n- Тут не написано, чиє.\n- Тепер буде написано. Сокирою.\n\n" +
            "- Може, хоч раз до роботи візьмешся?\n- Я свою роботу знаю.\n- Знаєш. Тільки не робиш.\n\n" +
            "- Не дивись на мене так.\n- А як на тебе дивитися?\n- Ніяк. Дивись у свою миску.\n\n" +
            "- Знову хропіння всю ніч.\n- Це не я.\n- А хто? Троль під ліжком?\n\n" +
            "- Де моя ложка?\n- Звідки мені знати?\n- Бо вона в тебе в руці.\n\n" +
            "- Тобі заплатили, а мені — дулю.\n- Отже, краще працюю.\n- Отже, краще підлизуєшся.\n\n" +
            "- Коли лізли сірі, тебе ніде не було видно.\n- Я… стеріг|стерегла комору.\n- Під бочкою, ага.\n\n" +
            "- Ще слово — і я пересяду.\n- То пересідай.\n- …Не дочекаєшся.\n\n" +
            "- Ти завжди все знаєш краще.\n- Бо так воно і є.\n- От тому з тобою й важко.\n\n" +
            // 2026-11-05: talks the composer makes (a topic's question and answer, NpcPhrases q_/a_): the written ones above stay as the rarer
            // stories (Pick: a made one first more often)
            "[chat table]\n" +
            "- {@q_food}\n- {@a_food}\n\n" +
            "- {@q_food}\n- {@a_food}\n- {@chat_reply}\n\n" +
            "[chat fire]\n" +
            "- {@q_sleep}\n- {@a_sleep}\n\n" +
            "? daypart evening\n- {@chat_open}\n- {@chat_reply}\n- {@chat_reply}\n\n" +
            "[chat any]\n" +
            "- {@q_work}\n- {@a_work}\n\n" +
            "? !weather clear\n- {@q_weather}\n- {@a_weather}\n\n" +
            "- {@q_ravens}\n- {@a_ravens}\n\n" +
            "- {@q_home}\n- {@a_home}\n- {@chat_reply}\n\n" +
            // what the world's story did, between two of whom the one who starts knows it (StoryTales: its memory)
            "? npcremembers tale_gate\n- {@с_розмова_брама}\n- {@chat_reply}\n\n" +
            "? npcremembers tale_blue_fire\n- {@с_розмова_вогонь}\n- {@chat_reply}\n\n" +
            "? npcremembers tale_first_heart\n- {@с_розмова_серце}\n- {@chat_reply}\n\n";

        private static Dictionary<string, Lines> _all;
        internal static void Reload() { _all = null; Lately.Clear(); }

        private static Lines Of(string kind)
        {
            if (_all == null) Load();
            Lines l;
            return _all.TryGetValue(kind, out l) ? l : null;
        }

        private static void Load()
        {
            NpcTalk.Register(); // (the conditions known before its words are read)
            _all = new Dictionary<string, Lines>();
            Parse(Builtin);
            foreach (Func<IEnumerable<string>> more in More) foreach (string text in more()) Parse(text);
            if (RavenLooks.FromFiles && Directory.Exists(NpcTalk.Dir))
                foreach (string f in Directory.GetFiles(NpcTalk.Dir, "chatter*.txt"))
                {
                    try { Parse(File.ReadAllText(f, Encoding.UTF8)); }
                    catch (Exception e) { Npcs.Log("chatter file " + f + ": " + e.Message); }
                }
        }

        private static void Parse(string text)
        {
            Lines cur = null; bool talks = false, named = false;
            List<string> talk = new List<string>(), by = new List<string>();
            List<Cond> when = null;
            Action flush = () => { if (cur != null && talk.Count > 0) cur.Talks.Add(new Chat { Said = talk.ToArray(), By = named ? by.ToArray() : null, When = when }); talk.Clear(); by.Clear(); when = null; };
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    flush();
                    string[] w = line.Substring(1, line.Length - 2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length < 2 || (w[0] == "pair" && w.Length < 3)) { cur = null; continue; }
                    named = w[0] == "meet" || w[0] == "pair" || w[0] == "road";
                    talks = w[0] == "chat" || named;
                    string key = w[0] == "pair" ? PairKey(w[1], w[2]) : (w[0] == "road" ? "road " : named ? "meet " : talks ? "chat " : "say ") + w[1];
                    if (!_all.TryGetValue(key, out cur)) _all[key] = cur = new Lines();
                    continue;
                }
                if (cur == null) continue;
                if (line.Length == 0) { if (talks) flush(); continue; }
                if (talks && line.StartsWith("?"))
                {
                    List<string> errors = new List<string>();
                    when = StoryCond.Parse(line.Substring(1).Trim(), errors);
                    foreach (string e in errors) Npcs.Log("chatter «" + line + "»: " + e);
                }
                else if (named)
                {
                    int c = line.IndexOf(':'); // («<who>: <words>»)
                    int sp = line.IndexOf(' ');
                    if (c <= 0 || (sp >= 0 && sp < c)) { Npcs.Log("chatter «" + line + "»: a gathering's line is «<who>: <words>»"); continue; }
                    by.Add(line.Substring(0, c).Trim()); talk.Add(line.Substring(c + 1).Trim());
                }
                else if (talks) talk.Add(line.StartsWith("-") ? line.Substring(1).Trim() : line);
                else cur.One.Add(line);
            }
            flush();
        }

        // ---------------------------------------------------------------- one person's line
        // how: a mutter in passing (Unasked: only while the ring is quiet), or something it must say (Event: it waits its turn — leaving hungry)
        internal static void Mutter(NpcActor a, string kind) { Mutter(a, kind, NpcActor.Voice.Unasked); }
        internal static void Mutter(NpcActor a, string kind, NpcActor.Voice how)
        {
            if (a == null || Busy(a) || !NpcVoice.Free(a)) return; // (asleep, talked to, held quiet by a scene or the story)
            if (how == NpcActor.Voice.Unasked && !NpcActor.FloorFree(a.transform.position, a)) return; // (someone near is speaking)
            Lines l = Of("say " + kind);
            bool written = l != null && l.One.Count > 0, made = NpcPhrases.Has(a, kind);
            Player to = Player.GetClosestPlayer(a.transform.position, 20f);
            if (made && (!written || UnityEngine.Random.value < 0.5f)) { string s = NpcPhrases.Compose(a, kind, to); if (!string.IsNullOrEmpty(s)) { a.Say(s, null, how); return; } } // (written and made lines take turns)
            if (written && !NpcVoice.OwnWordsOnly(a)) a.Say(a.Form(NpcPhrases.Inline(a, Lang.TP(l.One[UnityEngine.Random.Range(0, l.One.Count)]), to)), null, how); // (in the phrases' language, as its made words: Lang.TP; its own form: «живий|жива»; the common people's lines, as the book's)
        }

        // the same, not again for a while (an empty pantry is said once in a few minutes, not at every look)
        private static readonly Dictionary<NpcActor, float> Said = new Dictionary<NpcActor, float>();
        internal static void MutterOnce(NpcActor a, string kind)
        {
            float at;
            if (a == null || (Said.TryGetValue(a, out at) && Time.time < at)) return;
            if (Said.Count > 64) Said.Clear();
            Said[a] = Time.time + 300f;
            Mutter(a, kind);
        }

        // ---------------------------------------------------------------- two talking
        private static readonly Dictionary<NpcActor, float> Talking = new Dictionary<NpcActor, float>(); // (until when)
        private static bool Busy(NpcActor a) { float until; return Talking.TryGetValue(a, out until) && Time.time < until; }
        // After a talk (the user 2026-11-12: those sitting talked almost without a pause — a look each 6–12 s at some 30%, a new talk half a minute
        // after the last, at a full table at once): the two who talked keep quiet RestFrom…RestTo s, and nobody starts a talk within the
        // floor's ring of where it was for RingQuiet s after it. A talk forced from the console minds neither
        internal static float RestFrom = 60f, RestTo = 120f, RingQuiet = 30f;
        private static readonly Dictionary<NpcActor, float> Rest = new Dictionary<NpcActor, float>(); // session: (until when)
        private static readonly List<KeyValuePair<Vector3, float>> Hush = new List<KeyValuePair<Vector3, float>>(); // session: (where a talk was, quiet till when)
        private static bool Resting(NpcActor a) { float until; return Rest.TryGetValue(a, out until) && Time.time < until; }
        private static bool Hushed(Vector3 at)
        {
            float r2 = NpcActor.FloorRadius * NpcActor.FloorRadius;
            for (int i = Hush.Count - 1; i >= 0; i--)
            {
                if (Hush[i].Value < Time.time) { Hush.RemoveAt(i); continue; }
                if ((Hush[i].Key - at).sqrMagnitude < r2) return true;
            }
            return false;
        }
        internal static bool IsTalking(NpcActor a) { return a != null && Busy(a); }

        // a resident's look round (NpcResident, every few seconds where it is owned): a talk with someone sitting by it, or
        // with someone met while both stroll about at their ease (then both stop, face each other and talk)
        internal static void Tick(NpcResident r) { Start(r, false); }

        // for testing (the console): a talk now with the nearest resident (6 m), whatever both are doing
        internal static string Force(NpcResident r)
        {
            if (r == null || r.Actor == null || !r.Actor.Owned) return "Його веде інша гра.";
            Talking.Remove(r.Actor);
            return Start(r, true) ? "Заговорили." : "Нема з ким (мешканець поруч, до 6 м, вільний від розмови).";
        }

        private static bool Start(NpcResident r, bool force)
        {
            NpcActor a = r.Actor;
            NpcMind m = r.Mind;
            if (a == null || m == null || Busy(a) || (!force && !NpcVoice.Free(a))) return false;
            if (!force && (Resting(a) || Hushed(a.transform.position))) return false; // (it talked a moment ago, or someone near did: a pause)
            bool seated = m.SeatedNow;
            if (!force && !seated && !AtEase(m)) return false;
            if (!force && Player.GetClosestPlayer(a.transform.position, Earshot) == null) return false; // (nobody would hear: no lines, no messages)
            float range = force ? 6f : seated ? 5f : 4f; // (round a fire they sit two steps off it: across it some four apart)
            NpcActor other = null; float best = range * range;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || o.Mind == null || Busy(o) || !NpcResident.Is(o) || (!force && (!NpcVoice.Free(o) || Resting(o)))) continue;
                if (!force && Shunned(a, o)) continue; // (kept apart: they pass each other by)
                if (!force && (seated ? !o.Mind.SeatedNow : (!AtEase(o.Mind) || !o.Owned))) continue; // (stopping the other one: only one run here)
                float d = (o.transform.position - a.transform.position).sqrMagnitude;
                if (d < best) { best = d; other = o; }
            }
            if (other == null) return false;
            if (!force && !NpcActor.FloorFree(a.transform.position, a, other)) return false; // (someone else near is speaking: not a third voice over it)
            if (force) seated = seated && other.Mind.SeatedNow;
            else
            {
                if (!First(a, other)) return false; // (the other one starts it)
                float chance = (NpcPersona.Of(a).Chatty + NpcPersona.Of(other).Chatty) * (seated ? 0.3f : 0.15f);
                if (NpcBackstory.Fact(a, "оповідач") || NpcBackstory.Fact(other, "оповідач")) chance *= 1.5f; // (one who sings or tells stories gets them talking)
                if (UnityEngine.Random.value > chance) return false;
            }
            string where = !seated ? "meet" : m.AtTable || other.Mind.AtTable ? "table" : "fire";
            List<Chat> pool = new List<Chat>();
            bool spat = Spat(a, other);
            if (spat) Fitting(pool, "chat quarrel", a); // (two at odds, or ill-matched and both low: words instead of a talk)
            if (pool.Count == 0) { spat = false; Fitting(pool, "chat " + where, a); Fitting(pool, "chat any", a); }
            if (pool.Count == 0) return false;
            string[] lines = Pick(pool).Said;
            if (Talking.Count > 64) Talking.Clear();
            float until = Time.time + 6f * lines.Length + 4f;
            Talking[a] = until; Talking[other] = until;
            if (Rest.Count > 64) Rest.Clear();
            float rest = until + UnityEngine.Random.Range(RestFrom, RestTo);
            Rest[a] = rest; Rest[other] = rest;
            if (Hush.Count < 32) Hush.Add(new KeyValuePair<Vector3, float>(a.transform.position, until + RingQuiet));
            if (!seated) { Stop(a, other, until); Stop(other, a, until); }
            r.StartCoroutine(Talk(new[] { a, other }, null, lines, new[] { seated, seated }, false));
            Npcs.Log(a.Id + ": " + (spat ? "quarrels" : "talks") + " with " + other.Id + " (" + where + ")");
            if (spat) { bool own = false; foreach (Func<NpcActor, NpcActor, string, bool> f in Spats) own |= f(a, other, where); if (!own) { NpcAttitude.Quarrel(a, other, "table"); NpcAttitude.Quarrel(other, a, "table"); } } // (a quarrel cools them both: NpcAttitude — a scuffle's own, else the table's)
            else { NpcAttitude.TellPerson(a, other, TalkWarms); NpcAttitude.TellPerson(other, a, TalkWarms); } // (they get to know each other: NpcAttitude, person to person)
            return true;
        }

        // ---------------------------------------------------------------- a gathering's talk (StoryMeets: who is there, and when)
        // may this one take part in a talk of its own written words now: not in another, not held quiet, not just out of one
        // a line «{told <talk> <place prefix>}» of a gathering's talk (the user 2026-11-16: Torvald's true tales by the fire, into the book's «Сага»): a module's
        // (StoryMeets) — the words of a place of that talk its listener (this game's player by the gathering) has not heard and may still, and «<talk> <place>»; null: none now (the talk ends there)
        internal const string ToldWord = "{told ";
        internal static Func<NpcActor, string, string[]> Tells;
        internal static float MeetApart = 14f; // m between two of a gathering's talk (Heid and Torvald across the fire of the Watch sat 5.8 m apart by their places: over the 6 m of two side by side, and their talk ended before its first line)
        internal static bool Ready(NpcActor o, bool force = false)
        {
            return o != null && o.Mind != null && !Busy(o) && NpcVoice.Free(o, true) && (force || !Resting(o));
        }

        // the first of the talk's kind that fits those there: its conditions hold (he: the one who starts) and everyone it names is among them.
        // there[0] starts it and is run here; one who stands (not seated) must be run here too: it is stopped for the talk. The same pauses
        // after it as any talk's (Rest, Hush). first: a condition's word (StoryMeets «guest» — a player just sat down in the circle) — of the talks
        // that fit, only those with it, while there is one. False: no talk now
        internal static bool Meet(string name, List<NpcActor> there, bool force, string first = null) { return Group(Of("meet " + name), name, there, force, first); }

        // two of the story's people's own short exchange («[pair <a> <b>]»; StoryMeets: near each other, outside a gathering); a starts it
        internal static string PairKey(string a, string b) { return "pair " + (string.CompareOrdinal(a, b) <= 0 ? a + " " + b : b + " " + a); }
        internal static bool HasPair(string a, string b) { Lines l = Of(PairKey(a, b)); return l != null && l.Talks.Count > 0; }
        private static readonly List<NpcActor> _pair = new List<NpcActor>(2);
        internal static bool Pair(NpcActor a, NpcActor o)
        {
            _pair.Clear(); _pair.Add(a); _pair.Add(o);
            string key = PairKey(a.Id, o.Id);
            return Group(Of(key), key, _pair, false, null);
        }

        private static bool Group(Lines l, string name, List<NpcActor> there, bool force, string first)
        {
            NpcActor a = there.Count > 0 ? there[0] : null;
            if (a == null || l == null || there.Count < 2 || !a.Owned || a.Resident == null) return false;
            if (!force && (Hushed(a.transform.position) || Player.GetClosestPlayer(a.transform.position, Earshot) == null || !NpcActor.FloorFree(a.transform.position, a))) return false;
            List<Chat> pool = new List<Chat>();
            NpcActor was = NpcTalk.Speaker;
            NpcTalk.Speaker = a;
            try
            {
                foreach (Chat c in l.Talks)
                {
                    if (c.By == null || !StoryCond.All(null, c.When)) continue;
                    bool all = true;
                    foreach (string id in c.By) { NpcActor o = Named(there, id); all &= o != null && (NpcActor.Hushed == null || !NpcActor.Hushed(o)); } // (one keeping quiet now — not met yet, a scene near — says no line: a talk naming it would be heard by halves, 2026-11-22)
                    if (all) pool.Add(c);
                }
            }
            finally { NpcTalk.Speaker = was; }
            if (first != null) { List<Chat> own = pool.FindAll(c => Names(c, first)); if (own.Count > 0) pool = own; }
            if (pool.Count == 0) return false;
            Chat talk = Pick(pool);
            List<NpcActor> who = new List<NpcActor>();
            int[] by = new int[talk.By.Length];
            for (int i = 0; i < talk.By.Length; i++) { NpcActor o = Named(there, talk.By[i]); int at = who.IndexOf(o); if (at < 0) { at = who.Count; who.Add(o); } by[i] = at; }
            if (who.Count < 2) return false; // (a talk of one voice is no talk: storycheck stops it in the data)
            bool[] seated = new bool[who.Count];
            for (int i = 0; i < who.Count; i++) { seated[i] = who[i].Mind.SeatedNow; if (!seated[i] && !who[i].Owned) return false; } // (one standing is stopped for the talk: only where it is run)
            if (Talking.Count > 64) Talking.Clear();
            if (Rest.Count > 64) Rest.Clear();
            float until = Time.time + 6f * talk.Said.Length + 4f, rest = until + UnityEngine.Random.Range(RestFrom, RestTo);
            for (int i = 0; i < who.Count; i++)
            {
                Talking[who[i]] = until; Rest[who[i]] = rest;
                if (!seated[i]) Stop(who[i], who[i == 0 ? 1 : 0], until);
            }
            if (Hush.Count < 32) Hush.Add(new KeyValuePair<Vector3, float>(a.transform.position, until + RingQuiet));
            a.Resident.StartCoroutine(Talk(who.ToArray(), by, talk.Said, seated, true));
            Npcs.Log(a.Id + ": a gathering's talk «" + name + "», " + who.Count + " of " + there.Count + " there, " + talk.Said.Length + " lines");
            return true;
        }
        private static NpcActor Named(List<NpcActor> there, string id) { foreach (NpcActor o in there) if (o != null && o.Id == id) return o; return null; }
        private static bool Names(Chat c, string kind) { if (c.When != null) foreach (Cond k in c.When) if (!k.Not && k.A.Length > 0 && k.A[0] == kind) return true; return false; } // (its «? …» asks that, not its opposite)

        // words instead of a talk: one of the two cold to the other (SpatCold of the times, hostile SpatHostile: the rest a talk warms them back —
        // a spat cools up to 15, a talk warms 3, so a cold pair may come out of it), or two who get on badly (Fit) and are both low
        internal static float SpatCold = 0.35f, SpatHostile = 0.6f, SpatLow = 0.35f, IllMatched = 25f, LowMood = 40f;
        private static bool Spat(NpcActor a, NpcActor o)
        {
            float worst = Mathf.Min(NpcAttitude.OfPerson(a, o), NpcAttitude.OfPerson(o, a));
            if (NpcAttitude.Cold(worst)) return UnityEngine.Random.value < (worst <= NpcAttitude.HostileAt ? SpatHostile : SpatCold);
            return NpcAttitude.Fit(a, o) < IllMatched && NpcToil.Mood(a) < LowMood && NpcToil.Mood(o) < LowMood && UnityEngine.Random.value < SpatLow;
        }

        // the talks of this kind whose conditions hold now («he»: the one who starts)
        private static void Fitting(List<Chat> into, string kind, NpcActor a)
        {
            Lines l = Of(kind);
            if (l == null) return;
            NpcActor was = NpcTalk.Speaker;
            NpcTalk.Speaker = a;
            try { foreach (Chat c in l.Talks) if (StoryCond.All(null, c.When)) into.Add(c); }
            finally { NpcTalk.Speaker = was; }
        }

        // one not heard lately (up to half of those that fit, of each kind of talk, are kept back; all the people of this game share it)
        internal static float TalkWarms = 3f; // points warmer to each other a talk
        private const float Earshot = 25f; // (as far as a line's bubble is seen: NpcActor.RPC_Say)
        private static readonly List<Chat> Lately = new List<Chat>();
        // a made talk (its lines from the composer, «{@…}»: new words each time) first MadeFirst of the times there is one; the written
        // stories are the rarer, so they are not heard word for word too often
        internal static float MadeFirst = 0.6f;
        private static bool Made(Chat c) { foreach (string l in c.Said) if (l.IndexOf("{@", StringComparison.Ordinal) >= 0) return true; return false; }

        private static Chat Pick(List<Chat> pool)
        {
            List<Chat> fresh = pool.FindAll(c => !Lately.Contains(c));
            if (fresh.Count == 0) fresh = pool;
            List<Chat> made = fresh.FindAll(Made);
            if (made.Count > 0 && made.Count < fresh.Count) fresh = UnityEngine.Random.value < MadeFirst ? made : fresh.FindAll(c => !Made(c));
            Chat c0 = fresh[UnityEngine.Random.Range(0, fresh.Count)];
            Lately.Add(c0);
            int mine = 0; // (of this pool only: a short list of quarrels must not forget what the table's long one has said)
            foreach (Chat c in Lately) if (pool.Contains(c)) mine++;
            for (int i = 0; i < Lately.Count && mine > pool.Count / 2;) { if (pool.Contains(Lately[i])) { Lately.RemoveAt(i); mine--; } else i++; }
            return c0;
        }

        // standing at its ease: nothing over its own day but a place it went to for its pleasure (NpcPlaces; no work, no duty, no order)
        private static bool AtEase(NpcMind m) { return m.AtLeisure && (m.Now == NpcMind.Task.Wander || m.Now == NpcMind.Task.Idle); }

        // stands for the talk (a whim of its own: over by itself), turned to the other
        private static void Stop(NpcActor a, NpcActor to, float until)
        {
            a.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = a.transform.position, Looks = true, Look = to.transform.position + Vector3.up * 1.6f }, until - Time.time, "talk");
        }

        // one of the two, the same on both games: the lower id
        internal static bool First(NpcActor a, NpcActor b)
        {
            ZDO za = a.Zdo, zb = b.Zdo;
            if (za == null || zb == null) return false;
            return za.m_uid.UserID != zb.m_uid.UserID ? za.m_uid.UserID < zb.m_uid.UserID : za.m_uid.ID < zb.m_uid.ID;
        }

        // the talk: its people (two in turn — by null; a gathering's — by: who says each line), each seated or stopped for it. own: their own
        // written words (a gathering's: one who says nothing of the common book speaks them too — NpcVoice.Free)
        private static IEnumerator Talk(NpcActor[] all, int[] by, string[] lines, bool[] seated, bool own)
        {
            NpcActor a = all[0], b = all[1];
            float apart = own ? MeetApart : 6f; // (two met or seated side by side: within 6 m; a gathering sits round its fire, each on its own side of it)
            bool fire = seated[0] && seated[1] && !a.Mind.AtTable && !b.Mind.AtTable, finished = true; // (by a fire: its gestures — NpcFireside; at a table its hands are at its meal)
            for (int i = 0; i < lines.Length; i++)
            {
                NpcActor who = all[by != null ? by[i] : i % 2];
                string broke = null; // (why the talk is over before its end: said in the log — a gathering's talk that ended before its first line was seen by nobody, 2026-11-16)
                for (int k = 0; k < all.Length && broke == null; k++)
                {
                    NpcActor o = all[k];
                    if (o == null || o.Mind == null) broke = "one of them is gone";
                    else if ((o.transform.position - all[0].transform.position).sqrMagnitude > apart * apart) broke = o.Id + " is " + Vector3.Distance(o.transform.position, all[0].transform.position).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m from " + all[0].Id;
                    else if (seated[k] ? !o.Mind.SeatedNow : o.Mind.NowLayer != NpcMind.Layer.Whim) broke = o.Id + (seated[k] ? " stood up" : " was called away"); // (one stood up, went, was called away: the talk is over)
                    else if (!NpcVoice.Free(o, own)) broke = o.Id + " may not speak now"; // (a scene took one of them, you spoke to one, the story hushed them)
                }
                Player hears = Player.GetClosestPlayer(who.transform.position, 20f);
                string line = lines[i], told = null;
                if (line.StartsWith(ToldWord, StringComparison.Ordinal) && line.EndsWith("}", StringComparison.Ordinal)) // («{told <talk> <place prefix>}»: a place of its talk, Tells)
                {
                    string[] tale = Tells != null ? Tells(who, line.Substring(ToldWord.Length, line.Length - ToldWord.Length - 1).Trim()) : null;
                    if (tale == null) broke = "nothing of «" + line + "» left to tell this game's player";
                    else { line = tale[0]; told = tale[1]; }
                }
                if (broke != null) { finished = false; Npcs.Log(all[0].Id + ": the talk is over at line " + (i + 1) + " of " + lines.Length + " — " + broke); break; }
                string said = who.Form(NpcPhrases.Inline(who, Lang.TP(line), hears)); // (in the phrases' language: Lang.TP; «{@theme}»: a made line in a written talk)
                string got = who.SayLine(said, null, false, told), sent = got ?? said; // (the words as every game got them: the plan of its speech is made of these; a place of a talk says which — those near have heard it: NpcActor.Told)
                float pause = Mathf.Max(RavenSpeech.Seconds(line, RavenSpeech.Pace.Chatter), NpcSpeech.Seconds(who, sent)); // (not over its own words: one speaks at a time)
                if (got != null && i + 1 < lines.Length) who.SayNext(all[by != null ? by[i + 1] : (i + 1) % 2], pause); // (the next one's thinking dots in the capsule, while it thinks: NpcBubble)
                yield return new WaitForSeconds(pause + (i + 1 < lines.Length ? NpcSpeech.Think(sent) : 0f)); // (the other thinks a moment before it answers: NpcSpeech, the same time on every game)
            }
            if (fire) NpcFireside.End(a, b, finished && a != null && b != null);
            for (int k = 0; k < all.Length; k++)
                if (all[k] != null) { Talking.Remove(all[k]); if (!seated[k] && all[k].Mind != null) Free(all[k].Mind); }
        }

        // the talk's stop over (its whim, if it is still that one): back to its stroll
        private static void Free(NpcMind m)
        {
            m.EndWhim("talk");
        }
    }
}
#endif

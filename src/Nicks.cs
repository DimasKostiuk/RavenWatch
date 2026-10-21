#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // A raven's nickname: one word, its own, chosen in the book («Образ») from the ones it has opened.
    //   - Прості (grey): three plain ones rolled when it is first seen;
    //   - За норовом (lavender): two from its character, one from its knack, one from its quirk, open at once;
    //   - Віхи (gold) and Легендарні (red gold): earned by deeds done with you, never shown before they are earned
    //     (a surprise); once open, the hover says what it was given for.
    // Everything lives in the raven itself (customData: the chosen one, the open ones, the deed counters), so it goes
    // with the raven and comes back with it (Backup). The chosen name and its call form are published on the player
    // for the others (ZDO RavenWatch_NickH / _NickM = "name|call|kind"): their speech cards, the chat, the talks.
    // On your own screen your ravens stay «Хугін» / «Мунін» with the nickname quietly beside them.
    internal static class Nicks
    {
        internal enum Kind { Simple = 0, Norov = 1, Deed = 2, Legend = 3 }

        internal sealed class Nick
        {
            public string Id; // Call: how it is called («Кривдзьобе»); Why: what it was given for
            public string NameSrc, CallSrc, WhySrc; // the source texts (Ukrainian)
            public string Name { get { return Lang.T(NameSrc); } set { NameSrc = value; } }
            public string Call { get { return Lang.T(CallSrc); } set { CallSrc = value; } }
            public string Why { get { return Lang.T(WhySrc); } set { WhySrc = value; } }
            public Kind K;
            public int For;                    // 0 both, 1 Hugin only, 2 Munin only
            public Func<ItemDrop.ItemData, bool, bool> Earned; // deeds: is it earned (the raven, munin)
            public bool Fits(bool munin) { return For == 0 || (For == 2) == munin; }
        }

        private const string ChosenKey = "RavenWatch_nick", OpenKey = "RavenWatch_nicks", SeenKey = "RavenWatch_nickSeen";
        private const string DaysKey = "RavenWatch_nickDays"; // id:day of each deed earned (-1: earned before the record began)
        private const string ZdoH = "RavenWatch_NickH", ZdoM = "RavenWatch_NickM";
        private const int SimpleCount = 3;

        // ---------------------------------------------------------------- the names

        private static Nick N(string id, string name, string call, Kind k, int forWho, string why) { return new Nick { Id = id, Name = name, Call = call, K = k, For = forWho, Why = why }; }
        private static Nick D(string id, string name, string call, Kind k, int forWho, string why, Func<ItemDrop.ItemData, bool, bool> earned)
        {
            Nick n = N(id, name, call, k, forWho, why); n.Earned = earned; return n;
        }

        // the plain ones: mostly funny (Hugin the know-all, Munin the hooligan), a few poetic ones among them
        private static readonly Nick[] Simple =
        {
            N("s_syvoper", Lang.K("Сивопер"), Lang.K("Сивопере"), Kind.Simple, 1, null),
            N("s_tykhokryl", Lang.K("Розумака"), Lang.K("Розумако"), Kind.Simple, 1, null),
            N("s_vitrolet", Lang.K("Всезнайко"), Lang.K("Всезнайку"), Kind.Simple, 1, null),
            N("s_yasnovyd", Lang.K("Мудрагель"), Lang.K("Мудрагелю"), Kind.Simple, 1, null),
            N("s_sribnoper", Lang.K("Дзьобик"), Lang.K("Дзьобику"), Kind.Simple, 1, null),
            N("s_mudrodziob", Lang.K("Філософ"), Lang.K("Філософе"), Kind.Simple, 1, null),
            N("s_lehkokryl", Lang.K("Пухнастик"), Lang.K("Пухнастику"), Kind.Simple, 1, null),
            N("s_khmarnyk", Lang.K("Зануда"), Lang.K("Занудо"), Kind.Simple, 1, null),
            N("s_shepotun", Lang.K("Книжник"), Lang.K("Книжнику"), Kind.Simple, 1, null),
            N("s_zoreper", Lang.K("Зорепер"), Lang.K("Зорепере"), Kind.Simple, 1, null),
            N("s_synoper", Lang.K("Каркуша"), Lang.K("Каркушо"), Kind.Simple, 1, null),
            N("s_tumanovyd", Lang.K("Тумановид"), Lang.K("Тумановиде"), Kind.Simple, 1, null),
            N("s_kryvdziob", Lang.K("Шибеник"), Lang.K("Шибенику"), Kind.Simple, 2, null),
            N("s_chornokryl", Lang.K("Чорнокрил"), Lang.K("Чорнокриле"), Kind.Simple, 2, null),
            N("s_rvanoper", Lang.K("Хуліган"), Lang.K("Хулігане"), Kind.Simple, 2, null),
            N("s_chornodziob", Lang.K("Бандюга"), Lang.K("Бандюго"), Kind.Simple, 2, null),
            N("s_burevii", Lang.K("Буревій"), Lang.K("Буревію"), Kind.Simple, 2, null),
            N("s_sazhoper", Lang.K("Головоріз"), Lang.K("Головорізе"), Kind.Simple, 2, null),
            N("s_hromokryl", Lang.K("Розбишака"), Lang.K("Розбишако"), Kind.Simple, 2, null),
            N("s_shkrebun", Lang.K("Злюка"), Lang.K("Злюко"), Kind.Simple, 2, null),
            N("s_karkun", Lang.K("Каркало"), Lang.K("Каркало"), Kind.Simple, 2, null),
            N("s_nichokryl", Lang.K("Нічокрил"), Lang.K("Нічокриле"), Kind.Simple, 2, null),
            N("s_chortoper", Lang.K("Кусака"), Lang.K("Кусако"), Kind.Simple, 2, null),
            N("s_h_professor", Lang.K("Професор"), Lang.K("Професоре"), Kind.Simple, 1, null),
            N("s_h_mudryk", Lang.K("Мудрик"), Lang.K("Мудрику"), Kind.Simple, 1, null),
            N("s_h_pidkazka", Lang.K("Підказка"), Lang.K("Підказко"), Kind.Simple, 1, null),
            N("s_h_poradnyk", Lang.K("Порадник"), Lang.K("Порадниче"), Kind.Simple, 1, null),
            N("s_h_vchenyi", Lang.K("Вчений"), Lang.K("Вчений"), Kind.Simple, 1, null),
            N("s_h_mriinyk", Lang.K("Мрійник"), Lang.K("Мрійнику"), Kind.Simple, 1, null),
            N("s_h_skromniaha", Lang.K("Скромняга"), Lang.K("Скромняго"), Kind.Simple, 1, null),
            N("s_h_chystiulia", Lang.K("Чистюля"), Lang.K("Чистюле"), Kind.Simple, 1, null),
            N("s_h_pushok", Lang.K("Пушок"), Lang.K("Пушку"), Kind.Simple, 1, null),
            N("s_h_sychyk", Lang.K("Сичик"), Lang.K("Сичику"), Kind.Simple, 1, null),
            N("s_h_lektor", Lang.K("Лектор"), Lang.K("Лекторе"), Kind.Simple, 1, null),
            N("s_h_tsytatnyk", Lang.K("Цитатник"), Lang.K("Цитатнику"), Kind.Simple, 1, null),
            N("s_h_zadumlyvyi", Lang.K("Задумливий"), Lang.K("Задумливий"), Kind.Simple, 1, null),
            N("s_h_didus", Lang.K("Дідусь"), Lang.K("Дідусю"), Kind.Simple, 1, null),
            N("s_h_erudyt", Lang.K("Ерудит"), Lang.K("Ерудите"), Kind.Simple, 1, null),
            N("s_h_poet", Lang.K("Поет"), Lang.K("Поете"), Kind.Simple, 1, null),
            N("s_h_movchun", Lang.K("Мовчун"), Lang.K("Мовчуне"), Kind.Simple, 1, null),
            N("s_h_akuratyst", Lang.K("Акуратист"), Lang.K("Акуратисте"), Kind.Simple, 1, null),
            N("s_h_misiatseper", Lang.K("Місяцепер"), Lang.K("Місяцепере"), Kind.Simple, 1, null),
            N("s_h_svitanok", Lang.K("Світанок"), Lang.K("Світанку"), Kind.Simple, 1, null),
            N("s_h_sribnokryl", Lang.K("Сріблокрил"), Lang.K("Сріблокриле"), Kind.Simple, 1, null),
            N("s_m_beshketnyk", Lang.K("Бешкетник"), Lang.K("Бешкетнику"), Kind.Simple, 2, null),
            N("s_m_shkidnyk", Lang.K("Шкідник"), Lang.K("Шкіднику"), Kind.Simple, 2, null),
            N("s_m_proidysvit", Lang.K("Пройдисвіт"), Lang.K("Пройдисвіте"), Kind.Simple, 2, null),
            N("s_m_khalamydnyk", Lang.K("Халамидник"), Lang.K("Халамиднику"), Kind.Simple, 2, null),
            N("s_m_shmarkach", Lang.K("Шмаркач"), Lang.K("Шмаркачу"), Kind.Simple, 2, null),
            N("s_m_burchalo", Lang.K("Бурчало"), Lang.K("Бурчало"), Kind.Simple, 2, null),
            N("s_m_harchun", Lang.K("Гарчун"), Lang.K("Гарчуне"), Kind.Simple, 2, null),
            N("s_m_dziobach", Lang.K("Дзьобач"), Lang.K("Дзьобачу"), Kind.Simple, 2, null),
            N("s_m_svarlyvets", Lang.K("Сварливець"), Lang.K("Сварливцю"), Kind.Simple, 2, null),
            N("s_m_skandalist", Lang.K("Скандаліст"), Lang.K("Скандалісте"), Kind.Simple, 2, null),
            N("s_m_batiar", Lang.K("Батяр"), Lang.K("Батяре"), Kind.Simple, 2, null),
            N("s_m_khartsyziaka", Lang.K("Харцизяка"), Lang.K("Харцизяко"), Kind.Simple, 2, null),
            N("s_m_zabiiaka", Lang.K("Забіяка"), Lang.K("Забіяко"), Kind.Simple, 2, null),
            N("s_m_buian", Lang.K("Буян"), Lang.K("Буяне"), Kind.Simple, 2, null),
            N("s_m_mordobii", Lang.K("Мордобій"), Lang.K("Мордобою"), Kind.Simple, 2, null),
            N("s_m_zlyden", Lang.K("Злидень"), Lang.K("Злидню"), Kind.Simple, 2, null),
            N("s_m_kryvavokryl", Lang.K("Кривавокрил"), Lang.K("Кривавокриле"), Kind.Simple, 2, null),
            N("s_m_popelokryl", Lang.K("Попелокрил"), Lang.K("Попелокриле"), Kind.Simple, 2, null),
            N("s_m_temnoper", Lang.K("Темнопер"), Lang.K("Темнопере"), Kind.Simple, 2, null),
        };

        // by its norov: the character gives two, the knack one, the quirk one (the id after "c_", "k_", "q_" is the norov's)
        private static readonly Nick[] ByNorov =
        {
            N("c_homebody_1", Lang.K("Пічкур"), Lang.K("Пічкуре"), Kind.Norov, 0, Lang.K("Характер: Домосід")),
            N("c_homebody_2", Lang.K("Хатник"), Lang.K("Хатнику"), Kind.Norov, 0, Lang.K("Характер: Домосід")),
            N("c_wanderer_1", Lang.K("Вітролов"), Lang.K("Вітролове"), Kind.Norov, 0, Lang.K("Характер: Мандрівник")),
            N("c_wanderer_2", Lang.K("Бурлака"), Lang.K("Бурлако"), Kind.Norov, 0, Lang.K("Характер: Мандрівник")),
            N("c_fidget_1", Lang.K("Вертихвіст"), Lang.K("Вертихвосте"), Kind.Norov, 0, Lang.K("Характер: Непосида")),
            N("c_fidget_2", Lang.K("Дзиґа"), Lang.K("Дзиґо"), Kind.Norov, 0, Lang.K("Характер: Непосида")),
            N("c_calm_1", Lang.K("Тюлень"), Lang.K("Тюленю"), Kind.Norov, 0, Lang.K("Характер: Спокійний")),
            N("c_calm_2", Lang.K("Флегма"), Lang.K("Флегмо"), Kind.Norov, 0, Lang.K("Характер: Спокійний")),
            N("c_watchful_1", Lang.K("Пильнозор"), Lang.K("Пильнозоре"), Kind.Norov, 0, Lang.K("Характер: Сторожкий")),
            N("c_watchful_2", Lang.K("Параноїк"), Lang.K("Параноїку"), Kind.Norov, 0, Lang.K("Характер: Сторожкий")),
            N("c_bold_1", Lang.K("Задирака"), Lang.K("Задирако"), Kind.Norov, 0, Lang.K("Характер: Зухвалий")),
            N("c_bold_2", Lang.K("Шибайголова"), Lang.K("Шибайголово"), Kind.Norov, 0, Lang.K("Характер: Зухвалий")),
            N("c_devoted_1", Lang.K("Вірнопер"), Lang.K("Вірнопере"), Kind.Norov, 0, Lang.K("Характер: Відданий")),
            N("c_devoted_2", Lang.K("Хвостик"), Lang.K("Хвостику"), Kind.Norov, 0, Lang.K("Характер: Відданий")),
            N("k_heavy", Lang.K("Кувалда"), Lang.K("Кувалдо"), Kind.Norov, 0, Lang.K("Хист: важкий удар")),
            N("k_far", Lang.K("Ракета"), Lang.K("Ракето"), Kind.Norov, 0, Lang.K("Хист: Далекий виліт")),
            N("k_quick", Lang.K("Метеор"), Lang.K("Метеоре"), Kind.Norov, 0, Lang.K("Хист: Швидке крило")),
            N("k_thrifty", Lang.K("Скнара"), Lang.K("Скнаро"), Kind.Norov, 0, Lang.K("Хист: Ощадний")),
            N("k_keen", Lang.K("Влучко"), Lang.K("Влучку"), Kind.Norov, 0, Lang.K("Хист: Влучний")),
            N("k_eye", Lang.K("Підглядач"), Lang.K("Підглядачу"), Kind.Norov, 0, Lang.K("Хист: Пильне око")),
            N("k_frugal", Lang.K("Аскет"), Lang.K("Аскете"), Kind.Norov, 0, Lang.K("Хист: Невибагливий")),
            N("k_light", Lang.K("Лінивець"), Lang.K("Лінивцю"), Kind.Norov, 0, Lang.K("Хист: Легке крило")),
            N("q_thief", Lang.K("Хапко"), Lang.K("Хапку"), Kind.Norov, 0, Lang.K("Примха: Злодюжка")),
            N("q_picky", Lang.K("Вередун"), Lang.K("Вередуне"), Kind.Norov, 0, Lang.K("Примха: Перебірливий")),
            N("q_grumbler", Lang.K("Буркун"), Lang.K("Буркуне"), Kind.Norov, 0, Lang.K("Примха: Бурчун")),
            N("q_wet", Lang.K("Мокрохвіст"), Lang.K("Мокрохвосте"), Kind.Norov, 0, Lang.K("Примха: Мокрий")),
            N("q_loud", Lang.K("Горлань"), Lang.K("Горланю"), Kind.Norov, 0, Lang.K("Примха: Галасливий")),
            N("q_showoff", Lang.K("Хизун"), Lang.K("Хизуне"), Kind.Norov, 0, Lang.K("Примха: Задавака")),
            N("q_jealous", Lang.K("Ревнивець"), Lang.K("Ревнивцю"), Kind.Norov, 0, Lang.K("Примха: Ревнивий")),
            N("q_sleepy", Lang.K("Дрімко"), Lang.K("Дрімку"), Kind.Norov, 0, Lang.K("Примха: Сонько")),
            N("q_chilly", Lang.K("Тремтун"), Lang.K("Тремтуне"), Kind.Norov, 0, Lang.K("Примха: Мерзляк")),
            N("q_magpie", Lang.K("Барахольник"), Lang.K("Барахольнику"), Kind.Norov, 0, Lang.K("Примха: Сорока")),
            N("q_willful", Lang.K("Упертюх"), Lang.K("Упертюху"), Kind.Norov, 0, Lang.K("Примха: Свавільний")),
            N("q_timid", Lang.K("Боягуз"), Lang.K("Боягузе"), Kind.Norov, 0, Lang.K("Примха: Боязкий")),
        };

        // The deeds (gold): long work together. The counters are the raven's own (Count); a boss counts each time it
        // falls with the raven on you (Bosses: the old ravens' "seen once" counts as one).
        private static readonly Nick[] Deeds =
        {
            D("d_eikthyr", Lang.K("Оленеїд"), Lang.K("Оленеїде"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Ейктюр"), (i, m) => Bosses(i, "Eikthyr") >= BossTimes),
            D("d_elder", Lang.K("Лісоруб"), Lang.K("Лісорубе"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Прадавній"), (i, m) => Bosses(i, "gd_king") >= BossTimes),
            D("d_bonemass", Lang.K("Смердюк"), Lang.K("Смердюче"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Кістянка"), (i, m) => Bosses(i, "Bonemass") >= BossTimes),
            D("d_moder", Lang.K("Ящірколов"), Lang.K("Ящірколове"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Модер"), (i, m) => Bosses(i, "Dragon") >= BossTimes),
            D("d_yagluth", Lang.K("Кістогриз"), Lang.K("Кістогризе"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Яглут"), (i, m) => Bosses(i, "GoblinKing") >= BossTimes),
            D("d_queen", Lang.K("Тарганобій"), Lang.K("Тарганобою"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Королева"), (i, m) => Bosses(i, "SeekerQueen") >= BossTimes),
            D("d_fader", Lang.K("Вогнегасник"), Lang.K("Вогнегаснику"), Kind.Deed, 0, Lang.K("Тричі бачив, як падає Фейдер"), (i, m) => Bosses(i, "Fader") >= BossTimes),
            D("d_trust4", Lang.K("Підлиза"), Lang.K("Підлизо"), Kind.Deed, 0, Lang.K("Довіра 5-го рівня"), (i, m) => Trust.Level(i) >= 5),
            D("d_strikes_m", Lang.K("Мордоклюй"), Lang.K("Мордоклюю"), Kind.Deed, 2, Lang.K("Двісті влучних ударів"), (i, m) => Strike.Flights(i) >= 200),
            D("d_strikes_h", Lang.K("Снайпер"), Lang.K("Снайпере"), Kind.Deed, 1, Lang.K("Двісті влучних ударів"), (i, m) => Strike.Flights(i) >= 200),
            D("d_deaths", Lang.K("Вдівець"), Lang.K("Вдівцю"), Kind.Deed, 0, Lang.K("Тридцять разів бачив твою смерть"), (i, m) => Count(i, "deaths") >= 30),
            D("d_days", Lang.K("Старпер"), Lang.K("Старпере"), Kind.Deed, 0, Lang.K("Сто двадцять днів разом"), (i, m) => Count(i, "days") >= 120),
            D("d_outings", Lang.K("Гуляка"), Lang.K("Гуляко"), Kind.Deed, 0, Lang.K("Шістдесят прогулянок у небі"), (i, m) => Count(i, "outs") >= 60),
            D("d_sail", Lang.K("Шкіпер"), Lang.K("Шкіпере"), Kind.Deed, 0, Lang.K("Сто кілометрів під вітрилом"), (i, m) => Count(i, "sail") >= 100000),
            D("d_thief", Lang.K("Клептоман"), Lang.K("Клептомане"), Kind.Deed, 0, Lang.K("Тридцять крадіжок"), (i, m) => Count(i, "thefts") >= 30),
            D("d_perch", Lang.K("Сидень"), Lang.K("Сидню"), Kind.Deed, 0, Lang.K("Десять годин на жердині"), (i, m) => Count(i, "perch") >= 36000), // seconds of your play while it sits there
            D("d_talker", Lang.K("Пліткар"), Lang.K("Пліткарю"), Kind.Deed, 0, Lang.K("Говорив із воронами п'ятьох різних людей"), (i, m) => Talkers(i, 1) >= 5),
            D("d_brother", Lang.K("Кум"), Lang.K("Куме"), Kind.Deed, 0, Lang.K("П'ятнадцять розмов із воронами однієї людини"), (i, m) => Talkers(i, 15) >= 1),
            // the little ones (grey): plain work done together, opened quietly (a line in the corner, a dot in the book)
            D("e_trees", Lang.K("Лісник"), Lang.K("Ліснику"), Kind.Simple, 0, Lang.K("Сто зрубаних дерев"), (i, m) => Count(i, "trees") >= 100),
            D("e_logs", Lang.K("Колун"), Lang.K("Колуне"), Kind.Simple, 0, Lang.K("Сто розколотих колод"), (i, m) => Count(i, "logs") >= 100),
            D("e_rocks", Lang.K("Каменяр"), Lang.K("Каменярю"), Kind.Simple, 0, Lang.K("Двісті розбитих каменів і жил"), (i, m) => Count(i, "rocks") >= 200),
            D("e_pickups", Lang.K("Підбирайло"), Lang.K("Підбирайло"), Kind.Simple, 0, Lang.K("П'ятсот разів ти щось підняв із землі"), (i, m) => Count(i, "pickups") >= 500),
            D("e_berries", Lang.K("Чорничник"), Lang.K("Чорничнику"), Kind.Simple, 0, Lang.K("Сто зібраних ягід"), (i, m) => Count(i, "berries") >= 100),
            D("e_mushrooms", Lang.K("Грибник"), Lang.K("Грибнику"), Kind.Simple, 0, Lang.K("Сто зібраних грибів"), (i, m) => Count(i, "mushrooms") >= 100),
            D("e_crops", Lang.K("Городник"), Lang.K("Городнику"), Kind.Simple, 0, Lang.K("Сто зібраних з грядки"), (i, m) => Count(i, "crops") >= 100),
            D("e_bees", Lang.K("Бджоляр"), Lang.K("Бджолярю"), Kind.Simple, 0, Lang.K("Двадцять разів брав мед у бджіл"), (i, m) => Count(i, "bees") >= 20),
            D("e_sap", Lang.K("Смоляк"), Lang.K("Смоляче"), Kind.Simple, 0, Lang.K("Двадцять разів збирав сік"), (i, m) => Count(i, "sap") >= 20),
            D("e_jumps", Lang.K("Стрибунець"), Lang.K("Стрибунцю"), Kind.Simple, 0, Lang.K("Тисяча стрибків. Йому на плечі — кожен"), (i, m) => Count(i, "jumps") >= 1000),
            D("e_arrows", Lang.K("Лучник"), Lang.K("Лучнику"), Kind.Simple, 0, Lang.K("П'ятсот пущених стріл"), (i, m) => Count(i, "arrows") >= 500),
            D("e_doors", Lang.K("Воротар"), Lang.K("Воротарю"), Kind.Simple, 0, Lang.K("Триста відчинених дверей"), (i, m) => Count(i, "doors") >= 300),
            D("e_tamed", Lang.K("Пастух"), Lang.K("Пастуше"), Kind.Simple, 0, Lang.K("П'ятеро приручених звірів"), (i, m) => Count(i, "tamed") >= 5),
            D("e_crafts", Lang.K("Майстер"), Lang.K("Майстре"), Kind.Simple, 0, Lang.K("Сто зроблених і покращених речей"), (i, m) => Count(i, "crafts") >= 100),
            D("e_grill", Lang.K("Шашличник"), Lang.K("Шашличнику"), Kind.Simple, 0, Lang.K("П'ятдесят шматків м'яса з вогню"), (i, m) => Count(i, "grill") >= 50),
            D("e_graves", Lang.K("Гробар"), Lang.K("Гробарю"), Kind.Simple, 0, Lang.K("П'ять разів забирав свої речі з могили"), (i, m) => Count(i, "graves") >= 5),
            D("e_build", Lang.K("Тесля"), Lang.K("Теслю"), Kind.Simple, 0, Lang.K("Триста збудованих шматків"), (i, m) => Count(i, "build") >= 300),
            D("e_fish", Lang.K("Вудкар"), Lang.K("Вудкарю"), Kind.Simple, 0, Lang.K("Десять спійманих риб"), (i, m) => Count(i, "fish") >= 10),
            D("e_portal", Lang.K("Порталник"), Lang.K("Порталнику"), Kind.Simple, 0, Lang.K("Тридцять порталів"), (i, m) => Count(i, "portal") >= 30),
            D("e_sleep", Lang.K("Подушка"), Lang.K("Подушко"), Kind.Simple, 0, Lang.K("Десять ночей у ліжку"), (i, m) => Count(i, "sleep") >= 10),
            D("e_eat", Lang.K("Їдок"), Lang.K("Їдоку"), Kind.Simple, 0, Lang.K("Сто разів ти поїв"), (i, m) => Count(i, "eat") >= 100),
            D("e_falls", Lang.K("Падало"), Lang.K("Падало"), Kind.Simple, 0, Lang.K("Десять падінь"), (i, m) => Count(i, "fall") >= 10),
            D("e_swim", Lang.K("Плавунець"), Lang.K("Плавунцю"), Kind.Simple, 0, Lang.K("П'ять хвилин у воді"), (i, m) => Count(i, "swim") >= 300),
            D("e_dungeon", Lang.K("Печерник"), Lang.K("Печернику"), Kind.Simple, 0, Lang.K("П'ять разів під землею"), (i, m) => Count(i, "dungeon") >= 5),
            D("e_fed", Lang.K("Ласунчик"), Lang.K("Ласунчику"), Kind.Simple, 0, Lang.K("Двадцять разів ти його нагодував"), (i, m) => Count(i, "fed") >= 20),
            D("e_days", Lang.K("Приятель"), Lang.K("Приятелю"), Kind.Simple, 0, Lang.K("Двадцять днів разом"), (i, m) => Count(i, "days") >= 20),
            D("e_level", Lang.K("Учень"), Lang.K("Учню"), Kind.Simple, 0, Lang.K("Двадцять разів ти чогось навчився"), (i, m) => Count(i, "level") >= 20),
            D("e_cook", Lang.K("Кухарчук"), Lang.K("Кухарчуку"), Kind.Simple, 0, Lang.K("Тридцять приготованих страв"), (i, m) => Count(i, "cook") >= 30),
            // what you do every day, with the raven watching (Diary's events, OnEvent; the times in seconds: Tick)
            D("d_falls", Lang.K("Гепало"), Lang.K("Гепало"), Kind.Deed, 0, Lang.K("П'ятдесят разів бачив, як ти гепаєшся"), (i, m) => Count(i, "fall") >= 50),
            D("d_neardeath", Lang.K("Везунчик"), Lang.K("Везунчику"), Kind.Deed, 0, Lang.K("Тридцять разів ти ледь не вмер — і вижив"), (i, m) => Count(i, "near") >= 30),
            D("d_flee", Lang.K("Драпака"), Lang.K("Драпако"), Kind.Deed, 0, Lang.K("Тридцять разів ви разом драпали від ворога"), (i, m) => Count(i, "flee") >= 30),
            D("d_escaped", Lang.K("Мазило"), Lang.K("Мазило"), Kind.Deed, 0, Lang.K("П'ятдесят разів поранений звір утік від тебе"), (i, m) => Count(i, "escaped") >= 50),
            D("d_eat", Lang.K("Ненажера"), Lang.K("Ненажеро"), Kind.Deed, 0, Lang.K("Чотириста разів дивився, як ти їси. Сам не їв"), (i, m) => Count(i, "eat") >= 400),
            D("d_sleep", Lang.K("Будильник"), Lang.K("Будильнику"), Kind.Deed, 0, Lang.K("П'ятдесят ночей ти спав, а він стеріг"), (i, m) => Count(i, "sleep") >= 50),
            D("d_portal", Lang.K("Фокусник"), Lang.K("Фокуснику"), Kind.Deed, 0, Lang.K("Сто п'ятдесят разів зникав і з'являвся з тобою в порталі"), (i, m) => Count(i, "portal") >= 150),
            D("d_fish", Lang.K("Рибалка"), Lang.K("Рибалко"), Kind.Deed, 0, Lang.K("Сто риб — і жодної йому"), (i, m) => Count(i, "fish") >= 100),
            D("d_build", Lang.K("Виконроб"), Lang.K("Виконробе"), Kind.Deed, 0, Lang.K("Три тисячі збудованих тобою шматків під його наглядом"), (i, m) => Count(i, "build") >= 3000),
            D("d_cook", Lang.K("Кухар"), Lang.K("Кухарю"), Kind.Deed, 0, Lang.K("Двісті страв, приготованих при ньому"), (i, m) => Count(i, "cook") >= 200),
            D("d_pet", Lang.K("Цуцик"), Lang.K("Цуцику"), Kind.Deed, 0, Lang.K("П'ятдесят разів ти гладив тварин. Не його"), (i, m) => Count(i, "pet") >= 50),
            D("d_pick", Lang.K("Ягодоїд"), Lang.K("Ягодоїде"), Kind.Deed, 0, Lang.K("Тисяча ягід, грибів і всякого зібраного"), (i, m) => Count(i, "pick") >= 1000),
            D("d_dungeon", Lang.K("Підвальник"), Lang.K("Підвальнику"), Kind.Deed, 0, Lang.K("Тридцять разів лазив з тобою під землю"), (i, m) => Count(i, "dungeon") >= 30),
            D("d_chop", Lang.K("Бобер"), Lang.K("Бобре"), Kind.Deed, 0, Lang.K("Три тисячі ударів сокирою при ньому"), (i, m) => Count(i, "chop") >= 3000),
            D("d_mine", Lang.K("Шахтар"), Lang.K("Шахтарю"), Kind.Deed, 0, Lang.K("Три тисячі ударів кайлом при ньому"), (i, m) => Count(i, "mine") >= 3000),
            D("d_level", Lang.K("Ботан"), Lang.K("Ботане"), Kind.Deed, 0, Lang.K("Сто п'ятдесят разів ти чогось навчився"), (i, m) => Count(i, "level") >= 150),
            D("d_heavy", Lang.K("Віслюк"), Lang.K("Віслюче"), Kind.Deed, 0, Lang.K("Годину ви разом тягли більше, ніж можна"), (i, m) => Count(i, "heavy") >= 3600),
            D("d_swim", Lang.K("Качка"), Lang.K("Качко"), Kind.Deed, 0, Lang.K("Півгодини плавав з тобою. На плечі. Мокрий"), (i, m) => Count(i, "swim") >= 1800),
            D("d_fed", Lang.K("Годованець"), Lang.K("Годованцю"), Kind.Deed, 0, Lang.K("Сто п'ятдесят разів ти його нагодував"), (i, m) => Count(i, "fed") >= 150),
            D("d_offs", Lang.K("Перекотиполе"), Lang.K("Перекотиполе"), Kind.Deed, 0, Lang.K("П'ятдесят разів ти знімав його з плеча"), (i, m) => Count(i, "offs") >= 50),
            D("d_crits", Lang.K("Критикан"), Lang.K("Критикане"), Kind.Deed, 0, Lang.K("Сто критичних ударів"), (i, m) => Count(i, "crits") >= 100),
            D("d_raids", Lang.K("Партизан"), Lang.K("Партизане"), Kind.Deed, 0, Lang.K("Десять набігів ви вистояли разом"), (i, m) => Count(i, "raids") >= 10),
            D("d_mead", Lang.K("Бражник"), Lang.K("Бражнику"), Kind.Deed, 0, Lang.K("Вісімдесят медів, випитих при ньому"), (i, m) => Count(i, "mead") >= 80),
            D("d_stuffed", Lang.K("Пузань"), Lang.K("Пузаню"), Kind.Deed, 0, Lang.K("Тридцять разів наївся донесхочу"), (i, m) => Count(i, "stuffed") >= 30),
            D("d_starve", Lang.K("Кощій"), Lang.K("Кощію"), Kind.Deed, 0, Lang.K("П'ять днів голодував. Пам'ятає кожен"), (i, m) => Count(i, "starveday") >= 5),
            // the legends: a feat, or hundreds of hours together
            D("l_angel", Lang.K("Ангел"), Lang.K("Ангеле"), Kind.Legend, 1, Lang.K("П'ятдесят разів витягнув тебе з того світу («Останній шанс»)"), (i, m) => Count(i, "saved") >= 50),
            D("l_berserk", Lang.K("Берсерк"), Lang.K("Берсерку"), Kind.Legend, 2, Lang.K("Сто п'ятдесят разів впадав у криваву лють"), (i, m) => Count(i, "fury") >= 150),
            D("l_godslayer", Lang.K("Богоклюй"), Lang.K("Богоклюю"), Kind.Legend, 0, Lang.K("Власним ударом добив трьох різних босів"), (i, m) => List(i, "RavenWatch_n_felled").Count >= 3),
            D("l_deathless", Lang.K("Талісман"), Lang.K("Талісмане"), Kind.Legend, 0, Lang.K("Сто днів поспіль поруч із тобою — і жодної твоєї смерті"), (i, m) => Count(i, "alive") >= 100),
            D("l_lands", Lang.K("Турист"), Lang.K("Туристе"), Kind.Legend, 0, Lang.K("Побував з тобою в усіх семи краях і пройшов тисячу кілометрів"), (i, m) => Lands(i) >= Realms.Length && Count(i, "dist") >= 1000000),
            D("l_reaper", Lang.K("М'ясник"), Lang.K("М'ясниче"), Kind.Legend, 2, Lang.K("Три тисячі вбитих при ньому, з них сотня зіркових"), (i, m) => Count(i, "kills") >= 3000 && Count(i, "stars") >= 100),
            D("l_allseer", Lang.K("Шпигун"), Lang.K("Шпигуне"), Kind.Legend, 1, Lang.K("Сто розвідок і двадцять п'ять нових рунних каменів"), (i, m) => Count(i, "scouts") >= 100 && Count(i, "lore") >= 25),
            D("l_eternal", Lang.K("Дідуган"), Lang.K("Дідугане"), Kind.Legend, 0, Lang.K("Чотириста днів разом"), (i, m) => Count(i, "days") >= 400),
            D("l_envoy", Lang.K("Улюбленець"), Lang.K("Улюбленцю"), Kind.Legend, 0, Lang.K("Сто днів поспіль на найвищій довірі — і жодного дня голоду"), (i, m) => Count(i, "envoy") >= 100),
            D("l_skald", Lang.K("Тамада"), Lang.K("Тамадо"), Kind.Legend, 0, Lang.K("Говорив із воронами шістьох різних людей, і двоє з тих воронів мали заслужене ім'я"), (i, m) => Talkers(i, 1) >= 6 && List(i, "RavenWatch_n_famed").Count >= 2),
        };

        // The same deed in Munin's own word (his manner): Hugin keeps the word in Deeds, Munin gets "<id>_m" with this one —
        // the same condition, the same "what for", the same kind. The deeds for one raven only keep theirs.
        private static readonly Dictionary<string, string[]> MuninWord = new Dictionary<string, string[]>
        {
            { "d_eikthyr", Lang.Keys("Рогоклюй", "Рогоклюю") },
            { "d_elder", Lang.Keys("Дуборіз", "Дуборізе") },
            { "d_bonemass", Lang.Keys("Гнилоклюв", "Гнилоклюве") },
            { "d_moder", Lang.Keys("Драконоїд", "Драконоїде") },
            { "d_yagluth", Lang.Keys("Черепоклюй", "Черепоклюю") },
            { "d_queen", Lang.Keys("Жукодав", "Жукодаве") },
            { "d_fader", Lang.Keys("Попелюх", "Попелюху") },
            { "d_trust4", Lang.Keys("Підлабузник", "Підлабузнику") },
            { "d_deaths", Lang.Keys("Гробокоп", "Гробокопе") },
            { "d_days", Lang.Keys("Хрич", "Хричу") },
            { "d_outings", Lang.Keys("Волоцюга", "Волоцюго") },
            { "d_sail", Lang.Keys("Боцман", "Боцмане") },
            { "d_thief", Lang.Keys("Злодюга", "Злодюго") },
            { "d_perch", Lang.Keys("Нероба", "Неробо") },
            { "d_talker", Lang.Keys("Базікало", "Базікало") },
            { "d_brother", Lang.Keys("Сват", "Свате") },
            { "d_falls", Lang.Keys("Реготун", "Реготуне") },
            { "d_neardeath", Lang.Keys("Живчик", "Живчику") },
            { "d_flee", Lang.Keys("Дезертир", "Дезертире") },
            { "d_escaped", Lang.Keys("Криворук", "Криворуче") },
            { "d_eat", Lang.Keys("Обжора", "Обжоро") },
            { "d_sleep", Lang.Keys("Вартовий", "Вартовий") },
            { "d_portal", Lang.Keys("Шахрай", "Шахраю") },
            { "d_fish", Lang.Keys("Рибожер", "Рибожере") },
            { "d_build", Lang.Keys("Бригадир", "Бригадире") },
            { "d_cook", Lang.Keys("Дегустатор", "Дегустаторе") },
            { "d_pet", Lang.Keys("Ревнюк", "Ревнюче") },
            { "d_pick", Lang.Keys("Обдирайло", "Обдирайло") },
            { "d_dungeon", Lang.Keys("Кротяра", "Кротяро") },
            { "d_chop", Lang.Keys("Дроворуб", "Дроворубе") },
            { "d_mine", Lang.Keys("Довбня", "Довбне") },
            { "d_level", Lang.Keys("Зубрила", "Зубрило") },
            { "d_heavy", Lang.Keys("Ішак", "Ішаче") },
            { "d_swim", Lang.Keys("Щур", "Щуре") },
            { "d_fed", Lang.Keys("Ненаситний", "Ненаситний") },
            { "d_offs", Lang.Keys("Скривджений", "Скривджений") },
            { "d_crits", Lang.Keys("Різник", "Різнику") },
            { "d_raids", Lang.Keys("Рубака", "Рубако") },
            { "d_mead", Lang.Keys("П'яниця", "П'янице") },
            { "d_stuffed", Lang.Keys("Черевань", "Череваню") },
            { "d_starve", Lang.Keys("Скелет", "Скелете") },
            { "e_trees", Lang.Keys("Сокирник", "Сокирнику") },
            { "e_logs", Lang.Keys("Тріскун", "Тріскуне") },
            { "e_rocks", Lang.Keys("Каменелом", "Каменоломе") },
            { "e_pickups", Lang.Keys("Хапун", "Хапуне") },
            { "e_berries", Lang.Keys("Чорнорот", "Чорнороте") },
            { "e_mushrooms", Lang.Keys("Мухомор", "Мухоморе") },
            { "e_crops", Lang.Keys("Бур'ян", "Бур'яне") },
            { "e_bees", Lang.Keys("Покусаний", "Покусаний") },
            { "e_sap", Lang.Keys("Липкий", "Липкий") },
            { "e_jumps", Lang.Keys("Блоха", "Блохо") },
            { "e_arrows", Lang.Keys("Стріляка", "Стріляко") },
            { "e_doors", Lang.Keys("Скрипун", "Скрипуне") },
            { "e_tamed", Lang.Keys("Псар", "Псарю") },
            { "e_crafts", Lang.Keys("Халтурник", "Халтурнику") },
            { "e_grill", Lang.Keys("Шкварка", "Шкварко") },
            { "e_graves", Lang.Keys("Мародер", "Мародере") },
            { "e_build", Lang.Keys("Цвяхоїд", "Цвяхоїде") },
            { "e_fish", Lang.Keys("Рибоклюй", "Рибоклюю") },
            { "e_portal", Lang.Keys("Зникайло", "Зникайло") },
            { "e_sleep", Lang.Keys("Хропун", "Хропуне") },
            { "e_eat", Lang.Keys("Жерун", "Жеруне") },
            { "e_falls", Lang.Keys("Сміхун", "Сміхуне") },
            { "e_swim", Lang.Keys("Утопленик", "Утопленику") },
            { "e_dungeon", Lang.Keys("Кріт", "Кроте") },
            { "e_fed", Lang.Keys("Ласун", "Ласуне") },
            { "e_days", Lang.Keys("Сусіда", "Сусідо") },
            { "e_level", Lang.Keys("Недоук", "Недоуку") },
            { "e_cook", Lang.Keys("Нюхач", "Нюхачу") },
            { "l_godslayer", Lang.Keys("Богоїд", "Богоїде") },
            { "l_deathless", Lang.Keys("Безсмертник", "Безсмертнику") },
            { "l_lands", Lang.Keys("Бродяга", "Бродяго") },
            { "l_eternal", Lang.Keys("Пращур", "Пращуре") },
            { "l_envoy", Lang.Keys("Пестунчик", "Пестунчику") },
            { "l_skald", Lang.Keys("Горлодер", "Горлодере") },
        };

        private static List<Nick> _deeds;
        private static List<Nick> AllDeeds()
        {
            if (_deeds != null) return _deeds;
            List<Nick> l = new List<Nick>();
            foreach (Nick n in Deeds)
            {
                string[] w;
                if (n.For != 0 || !MuninWord.TryGetValue(n.Id, out w)) { l.Add(n); continue; }
                l.Add(new Nick { Id = n.Id, NameSrc = n.NameSrc, CallSrc = n.CallSrc, K = n.K, For = 1, WhySrc = n.WhySrc, Earned = n.Earned });
                l.Add(new Nick { Id = n.Id + "_m", NameSrc = w[0], CallSrc = w[1], K = n.K, For = 2, WhySrc = n.WhySrc, Earned = n.Earned });
            }
            _deeds = l;
            return l;
        }

        private static Dictionary<string, Nick> _byId;
        internal static Nick Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, Nick>();
                foreach (Nick n in Simple) _byId[n.Id] = n;
                foreach (Nick n in ByNorov) _byId[n.Id] = n;
                foreach (Nick n in AllDeeds()) _byId[n.Id] = n;
            }
            Nick found;
            return _byId.TryGetValue(id, out found) ? found : null;
        }

        // ---------------------------------------------------------------- the raven's data

        private static string Get(ItemDrop.ItemData item, string key)
        {
            string v;
            return item != null && item.m_customData != null && item.m_customData.TryGetValue(key, out v) ? v : null;
        }

        private static List<string> List(ItemDrop.ItemData item, string key)
        {
            List<string> l = new List<string>();
            string raw = Get(item, key);
            if (!string.IsNullOrEmpty(raw)) foreach (string s in raw.Split(',')) if (s.Length > 0 && !l.Contains(s)) l.Add(s);
            return l;
        }

        private static void PutList(ItemDrop.ItemData item, string key, List<string> l) { item.m_customData[key] = string.Join(",", l.ToArray()); }

        internal static List<Nick> Open(ItemDrop.ItemData item)
        {
            List<Nick> l = new List<Nick>();
            foreach (string id in List(item, OpenKey)) { Nick n = Find(id); if (n != null) l.Add(n); }
            return l;
        }

        internal static Nick Current(ItemDrop.ItemData item)
        {
            Nick n = Find(Get(item, ChosenKey));
            if (n != null) return n;
            List<Nick> open = Open(item);
            return open.Count > 0 ? open[0] : null;
        }

        internal static bool IsNew(ItemDrop.ItemData item, Nick n) { return !List(item, SeenKey).Contains(n.Id); }

        internal static void MarkSeen(ItemDrop.ItemData item)
        {
            if (item == null) return;
            string open = Get(item, OpenKey) ?? "";
            if (Get(item, SeenKey) != open) item.m_customData[SeenKey] = open;
        }

        // ---------------------------------------------------------------- when each deed was earned (the raven remembers it)

        private static void Dated(ItemDrop.ItemData r, Nick n, int day)
        {
            List<string> l = List(r, DaysKey);
            l.RemoveAll(e => e.StartsWith(n.Id + ":"));
            l.Add(n.Id + ":" + day);
            PutList(r, DaysKey, l);
        }

        // for the book's "has anything changed" look: the chosen name, how many are open, and whether renaming is free / waits
        internal static string Snapshot(ItemDrop.ItemData item)
        {
            return (Get(item, ChosenKey) ?? "") + "/" + List(item, OpenKey).Count + "/" + (FreeRename(item) ? "f" : RenameWait(item) > 0f ? "w" : "p");
        }

        internal static List<Nick> NewOnes(ItemDrop.ItemData item)
        {
            List<Nick> l = new List<Nick>();
            if (item == null) return l;
            List<string> seen = List(item, SeenKey);
            foreach (Nick n in Open(item)) if (!seen.Contains(n.Id)) l.Add(n);
            return l;
        }

        internal static bool HasNew(ItemDrop.ItemData item)
        {
            List<string> seen = List(item, SeenKey);
            foreach (string id in List(item, OpenKey)) if (!seen.Contains(id)) return true;
            return false;
        }

        // the raven's own counters for the deeds
        internal static int Count(ItemDrop.ItemData item, string what)
        {
            int n;
            return int.TryParse(Get(item, "RavenWatch_n_" + what), out n) ? n : 0;
        }

        internal static void Add(ItemDrop.ItemData item, string what, int amount)
        {
            if (item == null || amount <= 0) return;
            item.m_customData["RavenWatch_n_" + what] = Mathf.Min(Count(item, what) + amount, 99999999).ToString();
        }

        internal const int BossTimes = 3;

        // how many times this boss fell with the raven on you (before the count began: once, if it saw it)
        private static int Bosses(ItemDrop.ItemData item, string prefab)
        {
            return Mathf.Max(Count(item, "boss_" + prefab), Strike.BossesSeen(item).Contains(prefab) ? 1 : 0);
        }

        // a boss fell with this raven on you (Strike sees the end of the fight)
        internal static void OnBoss(Player p, ItemDrop.ItemData item, string prefab)
        {
            if (item == null || string.IsNullOrEmpty(prefab) || Trust.Stranger(item, p)) return;
            int was = Bosses(item, prefab);
            item.m_customData["RavenWatch_n_boss_" + prefab] = (was + 1).ToString();
        }

        // the lands it flew over with you
        private static readonly string[] Realms = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands" };
        private static int Lands(ItemDrop.ItemData item)
        {
            int n = 0;
            List<string> l = List(item, "RavenWatch_n_lands");
            foreach (string r in Realms) if (l.Contains(r)) n++;
            return n;
        }

        // the players whose ravens it talked with: how many have at least this many talks
        private static int Talkers(ItemDrop.ItemData item, int atLeast)
        {
            int n = 0;
            foreach (string e in List(item, "RavenWatch_n_talks"))
            {
                int c, at = e.IndexOf(':');
                if (at > 0 && int.TryParse(e.Substring(at + 1), out c) && c >= atLeast) n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- what the others did (hooks)

        // you died: the ravens on you saw it (before the tombstone takes them); the days without a death start over
        internal static void OnDeath(Player p)
        {
            foreach (ItemDrop.ItemData r in Mine(p)) Add(r, "deaths", 1);
            foreach (bool m in new[] { false, true }) { ItemDrop.ItemData r = Own(p, m); if (r != null) r.m_customData["RavenWatch_n_alive"] = "0"; }
        }

        // a creature you killed (not a tame one): Munin on you counts it, and the starred ones too
        internal static void OnKill(Player p, int stars)
        {
            ItemDrop.ItemData r = Own(p, true);
            if (r == null || Trust.Away(r)) return;
            Add(r, "kills", 1);
            if (stars > 0) Add(r, "stars", 1);
        }

        // a rune stone (or the like) read for the first time: Hugin on you counts it
        internal static void OnLore(Player p) { ItemDrop.ItemData r = Own(p, false); if (r != null && !Trust.Away(r)) Add(r, "lore", 1); }

        // the diary's events: the ravens on you (and here) saw it
        internal static void OnEvent(Ev type, int value)
        {
            string what;
            switch (type)
            {
                case Ev.Fall: what = "fall"; break;
                case Ev.NearDeath: what = "near"; break;
                case Ev.Flee: what = "flee"; break;
                case Ev.Escaped: what = "escaped"; break;
                case Ev.Eat: what = "eat"; break;
                case Ev.Sleep: what = "sleep"; break;
                case Ev.Portal: what = "portal"; break;
                case Ev.Fish: what = "fish"; break;
                case Ev.Build: what = "build"; break;
                case Ev.Cook: what = "cook"; break;
                case Ev.Pet: what = "pet"; break;
                case Ev.Pick: what = "pick"; break;
                case Ev.Dungeon: what = "dungeon"; break;
                case Ev.Chop: what = "chop"; break;
                case Ev.Mine: what = "mine"; break;
                default: return;
            }
            foreach (ItemDrop.ItemData r in Mine(Player.m_localPlayer)) Add(r, what, 1);
        }

        internal static void OnStat(PlayerStatType stat, float amount, bool cheated)
        {
            if (cheated) return;
            string what;
            switch (stat)
            {
                case PlayerStatType.Tree: what = "trees"; break;
                case PlayerStatType.Logs: what = "logs"; break;
                case PlayerStatType.Mines: what = "rocks"; break;
                case PlayerStatType.ItemsPickedUp: what = "pickups"; break;
                case PlayerStatType.HarvestBerry: what = "berries"; break;
                case PlayerStatType.HarvestMushroom: what = "mushrooms"; break;
                case PlayerStatType.HarvestCrop: what = "crops"; break;
                case PlayerStatType.BeesHarvested: what = "bees"; break;
                case PlayerStatType.SapHarvested: what = "sap"; break;
                case PlayerStatType.Jumps: what = "jumps"; break;
                case PlayerStatType.ArrowsShot: what = "arrows"; break;
                case PlayerStatType.DoorsOpened: what = "doors"; break;
                case PlayerStatType.CreatureTamed: what = "tamed"; break;
                case PlayerStatType.CraftsOrUpgrades: what = "crafts"; break;
                case PlayerStatType.CraftGrill: what = "grill"; break;
                case PlayerStatType.TombstonesOpenedOwn: what = "graves"; break;
                default: return;
            }
            int n = Mathf.Max(1, Mathf.RoundToInt(amount));
            foreach (ItemDrop.ItemData r in Mine(Player.m_localPlayer)) Add(r, what, n);
        }

        internal static void OnFed(ItemDrop.ItemData r) { Add(r, "fed", 1); }
        internal static void OnSkillUp(Player p) { foreach (ItemDrop.ItemData r in Mine(p)) Add(r, "level", 1); } // a skill one level up (vanilla; the diary's Level is PoV's own)
        internal static void OnTakenOff(Player p, ItemDrop.ItemData r) { if (r != null && !Trust.Stranger(r, p)) Add(r, "offs", 1); }
        internal static void OnCrit(Player p, bool munin) { ItemDrop.ItemData r = Own(p, munin); if (r != null) Add(r, "crits", 1); }
        internal static void OnRaid(Player p) { foreach (ItemDrop.ItemData r in Mine(p)) Add(r, "raids", 1); }
        internal static void OnMead(Player p) { foreach (ItemDrop.ItemData r in Mine(p)) Add(r, "mead", 1); }
        internal static void OnSaved(Player p) { ItemDrop.ItemData r = Own(p, false); if (r != null) Add(r, "saved", 1); }
        internal static void OnFury(Player p) { ItemDrop.ItemData r = Own(p, true); if (r != null) Add(r, "fury", 1); }

        internal static void OnScout(Player p, bool munin) { ItemDrop.ItemData r = Own(p, munin); if (r != null) Add(r, "scouts", 1); }

        // the last blow on a boss was this raven's: each boss once
        internal static void OnFelled(Player p, ItemDrop.ItemData r, string prefab)
        {
            if (r == null || Trust.Stranger(r, p) || string.IsNullOrEmpty(prefab)) return;
            List<string> l = List(r, "RavenWatch_n_felled");
            if (!l.Contains(prefab)) { l.Add(prefab); PutList(r, "RavenWatch_n_felled", l); }
        }

        internal static void OnOuting(Player p, bool munin) { ItemDrop.ItemData r = Own(p, munin); if (r != null) Add(r, "outs", 1); }

        internal static void OnTheft(Player p, bool munin) { ItemDrop.ItemData r = Own(p, munin); if (r != null) Add(r, "thefts", 1); }

        // a talk with other players' ravens: each of yours in it counts each of those players once
        internal static void OnTalk(Player p, bool munin, List<long> players, List<long> famed)
        {
            ItemDrop.ItemData r = Own(p, munin);
            if (r == null) return;
            if (famed != null && famed.Count > 0)
            {
                List<string> f = List(r, "RavenWatch_n_famed");
                foreach (long id in famed) if (!f.Contains(id.ToString())) f.Add(id.ToString());
                while (f.Count > 40) f.RemoveAt(0);
                PutList(r, "RavenWatch_n_famed", f);
            }
            List<string> l = List(r, "RavenWatch_n_talks");
            foreach (long id in players)
            {
                string key = id + ":";
                int i = l.FindIndex(e => e.StartsWith(key)), c = 0;
                if (i >= 0) { int.TryParse(l[i].Substring(key.Length), out c); l.RemoveAt(i); }
                l.Insert(0, key + (c + 1));
            }
            while (l.Count > 40) l.RemoveAt(l.Count - 1);
            PutList(r, "RavenWatch_n_talks", l);
        }

        private static ItemDrop.ItemData Own(Player p, bool munin)
        {
            ItemDrop.ItemData r = p != null ? RavenSeat.On(p, munin) : null;
            return r != null && !Trust.Stranger(r, p) ? r : null;
        }

        private static List<ItemDrop.ItemData> Mine(Player p)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>(2);
            foreach (bool m in new[] { false, true }) { ItemDrop.ItemData r = Own(p, m); if (r != null && !Trust.Away(r)) l.Add(r); }
            return l;
        }

        // ---------------------------------------------------------------- the tick (from the twice-a-second sync)

        private static float _next, _checkAt;
        private static Vector3 _sailFrom, _walkFrom;
        private static bool _sailing;
        private static readonly int[] ZoneWas = { -1, -1 };
        private static readonly string[] LandWas = new string[2];                     // the land last written down, per raven slot
        private static readonly ItemDrop.ItemData[] LandOf = new ItemDrop.ItemData[2];
        private static readonly ItemDrop.ItemData[] ZoneOf = new ItemDrop.ItemData[2];

        internal static void Tick(Player p)
        {
            if (p == null) return;
            float now = Time.time;
            if (now < _next) return;
            _next = now + 1f;
            bool check = now >= _checkAt;
            if (check) _checkAt = now + 5f;
            int today = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            bool sailing = Senses.Sailing;
            Vector3 pos = p.transform.position;
            float sailed = sailing && _sailing ? Mathf.Min(Vector3.Distance(pos, _sailFrom), 40f) : 0f; // a jump (a portal): not sailed
            _sailing = sailing; _sailFrom = pos;
            float step = _walkFrom != Vector3.zero ? Vector3.Distance(pos, _walkFrom) : 0f;
            float moved = step <= 40f ? step : 0f; // a second's way; more is a portal or a respawn
            _walkFrom = pos;
            string land = p.GetCurrentBiome().ToString();
            bool swim = p.IsSwimming(), heavy = p.IsEncumbered();

            foreach (bool munin in new[] { false, true })
            {
                ItemDrop.ItemData r = Own(p, munin);
                if (r == null || !Perks.Rolled(r)) continue;
                if (Get(r, OpenKey) == null)
                {
                    if (!NameDue(r)) continue; // asleep, or not long awake with you yet: no name (and no deeds) before it
                    First(p, r, munin, true);
                }
                if (Get(r, FreeGivenKey) == null) { GiveFree(r); r.m_customData[FreeGivenKey] = "1"; } // its first own choice is free (new ravens and ones from before)
                if (Trust.Away(r) && !Perches.On(r)) continue;
                if (Perches.On(r)) Add(r, "perch", 1); // a second on a perch
                else
                {
                    if (sailed > 0f) Add(r, "sail", Mathf.RoundToInt(sailed));
                    if (moved > 0.5f) Add(r, "dist", Mathf.RoundToInt(moved));
                    if (swim) Add(r, "swim", 1);
                    if (heavy) Add(r, "heavy", 1);
                    int zone = Satiety.Zone(Satiety.Value(r)), zi = munin ? 1 : 0;
                    if (!ReferenceEquals(ZoneOf[zi], r)) { ZoneOf[zi] = r; ZoneWas[zi] = -1; }
                    if (zone == Satiety.Stuffed && ZoneWas[zi] >= 0 && ZoneWas[zi] < Satiety.Stuffed) Add(r, "stuffed", 1); // filled to the brim just now
                    ZoneWas[zi] = zone;
                    if (Satiety.IsStarving(r)) r.m_customData["RavenWatch_n_envoy"] = "0"; // a day of hunger: the envoy's days start over
                    int li = munin ? 1 : 0;
                    if ((land != LandWas[li] || !ReferenceEquals(LandOf[li], r)) && Array.IndexOf(Realms, land) >= 0) // only when the land (or the raven) changes
                    {
                        LandWas[li] = land; LandOf[li] = r;
                        List<string> lands = List(r, "RavenWatch_n_lands");
                        if (!lands.Contains(land)) { lands.Add(land); PutList(r, "RavenWatch_n_lands", lands); }
                    }
                }
                int day;
                if (!int.TryParse(Get(r, "RavenWatch_n_day"), out day) || day != today)
                {
                    if (day > 0 && today > day)
                    {
                        Add(r, "days", 1); // a new day with you (not counted on the first look, nor going back)
                        Add(r, "alive", 1); // and one more without your death (OnDeath starts it over)
                        if (Satiety.IsStarving(r)) Add(r, "starveday", 1);
                        if (Trust.Level(r) >= Trust.MaxLevel && !Satiety.IsStarving(r)) Add(r, "envoy", 1); else r.m_customData["RavenWatch_n_envoy"] = "0";
                    }
                    r.m_customData["RavenWatch_n_day"] = today.ToString();
                }
                if (check) Earn(p, r, munin);
            }
            Publish(p);
            SayProud(p);
            TellNext(p, now);
        }

        // Its first name comes once it is awake (trust above 0) and has been back with you for a while: not the moment an
        // asleep raven is put on the shoulder. The clock is kept in the raven (world time), so it survives leaving the game.
        internal static float NameAfter = 120f; // s
        private const string AwakeKey = "RavenWatch_nickAwake";

        private static bool NameDue(ItemDrop.ItemData r)
        {
            if (!Trust.Active(r) || Trust.AwayLeft(r) > 0f) return false; // asleep, or still at Odin's for the waking
            double now = WorldTime.Now, at;
            string raw = Get(r, AwakeKey);
            if (raw == null || !double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out at) || at - now > WorldTime.MaxAhead)
            {
                r.m_customData[AwakeKey] = now.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }
            if (now - at < NameAfter) return false;
            r.m_customData.Remove(AwakeKey);
            return true;
        }

        // the first look at a raven: its plain names, and all it has already earned (old ravens too); told in the middle
        private static void First(Player p, ItemDrop.ItemData r, bool munin, bool announce)
        {
            // the names other players' ravens already go by in this world are left aside while there are others to give,
            // so ravens that play together start with different names
            HashSet<string> taken = new HashSet<string>();
            foreach (Player o in Player.GetAllPlayers())
                if (o != null && o != p)
                    foreach (bool m in new[] { false, true }) { string tn, tc; Kind tk; if (Of(o, m, out tn, out tc, out tk)) taken.Add(tn); }
            List<Nick> pool = new List<Nick>();
            foreach (Nick n in Simple) if (n.Fits(munin) && !taken.Contains(n.Name)) pool.Add(n);
            if (pool.Count < SimpleCount) foreach (Nick n in Simple) if (n.Fits(munin) && !pool.Contains(n)) pool.Add(n);
            List<string> open = new List<string>();
            for (int i = 0; i < SimpleCount && pool.Count > 0; i++) { int k = UnityEngine.Random.Range(0, pool.Count); open.Add(pool[k].Id); pool.RemoveAt(k); }
            r.m_customData[ChosenKey] = open.Count > 0 ? open[0] : "";
            List<Nick> earned = new List<Nick>();
            foreach (Nick n in Due(r, munin)) if (!open.Contains(n.Id)) { open.Add(n.Id); if (n.Earned != null) { earned.Add(n); Dated(r, n, -1); } }
            PutList(r, OpenKey, open);
            if (!announce) return;
            Nick first = Find(open.Count > 0 ? open[0] : null);
            if (first != null)
                Tell(p, Lang.F("<color={0}>{1} має прізвисько — «{2}»</color>\n<size=75%>Ще {3} на вибір: книга воронів, «Образ»</size>", BookArt.GoldHex, Plugin.RavenName(munin), first.Name, (open.Count - 1)));
            foreach (Nick n in earned) Tell(p, Line(munin, n)); // already earned before (an old raven): each in its turn
        }

        // what it has earned and does not have yet
        private static List<Nick> Due(ItemDrop.ItemData r, bool munin)
        {
            List<Nick> due = new List<Nick>();
            List<string> open = List(r, OpenKey);
            if (Norov.Rolled(r))
            {
                Norov.Character c = Norov.CharOf(r);
                Norov.Knack k = Norov.KnackOf(r);
                Norov.Quirk q = Norov.QuirkOf(r);
                foreach (Nick n in ByNorov)
                    if (!open.Contains(n.Id) && ((c != null && n.Id.StartsWith("c_" + c.Id + "_")) || (k != null && n.Id == "k_" + k.Id) || (q != null && n.Id == "q_" + q.Id))) due.Add(n);
            }
            foreach (Nick n in AllDeeds())
            {
                if (open.Contains(n.Id) || !n.Fits(munin)) continue;
                bool got = false;
                try { got = n.Earned(r, munin); } catch { }
                if (got) due.Add(n);
            }
            return due;
        }

        // new names earned: opened, and said (the deeds with a word from the raven and a line in the middle)
        private static void Earn(Player p, ItemDrop.ItemData r, bool munin)
        {
            List<Nick> due = Due(r, munin);
            if (due.Count == 0) return;
            List<string> open = List(r, OpenKey);
            Nick loud = null;
            foreach (Nick n in due)
            {
                open.Add(n.Id);
                if (n.Earned != null) Dated(r, n, Memory.Day());
                if (n.K >= Kind.Deed && (loud == null || n.K > loud.K)) loud = n;
            }
            PutList(r, OpenKey, open);
            if (loud != null) GiveFree(r); // a gold or legendary name earned: one free renaming (a flag — never more than one)
            Backup.Save(p, r);
            due.Sort((a, b) => b.K.CompareTo(a.K)); // the greatest first
            foreach (Nick n in due) Tell(p, Line(munin, n));
            if (loud != null) Proud[munin ? 1 : 0] = loud; // the gold and the legends: the raven says it itself, once nobody is talking (SayProud)
        }

        private static readonly Nick[] Proud = new Nick[2]; // a name it is to say itself, waiting for a quiet moment

        // from the tick: not over a talk (a deed often comes from one: the talks with someone's ravens)
        private static void SayProud(Player p)
        {
            if (p != Player.m_localPlayer || (Proud[0] == null && Proud[1] == null) || RavenSkills.Hushed(p)) return;
            int i = Proud[0] != null ? 0 : 1;
            Nick n = Proud[i]; Proud[i] = null;
            Say(p, i == 1, "nick_new", n); // one at a time: the other one waits until this one is said
        }

        // ---------------------------------------------------------------- the middle of the screen, one at a time

        // "Хугін заслужив прізвисько «Лісник»" in the kind's colour, what for under it
        private static string Line(bool munin, Nick n)
        {
            string verb = n.Earned != null ? Lang.T(" заслужив прізвисько «") : Lang.T(" має нове прізвисько «");
            string why = n.Why ?? ""; // what for, as the book's hover says it
            return "<color=" + Hex(n.K) + ">" + Plugin.RavenName(munin) + verb + n.Name + "»</color>"
                + (why.Length > 0 ? "\n<size=75%>" + why + "</size>" : "");
        }

        internal static float TellGap = 5f; // s between two such lines
        private static readonly List<string> Told = new List<string>();
        private static float _tellAt;

        private static Player _toldFor;
        private static void Tell(Player p, string line)
        {
            if (p != Player.m_localPlayer) return;
            if (p != _toldFor) { Told.Clear(); _toldFor = p; } // another character (or this one again after a relog): nothing left from before
            if (Told.Count < 20) Told.Add(line);
        }

        // from the tick: the next line, once both ravens have flown in (not over the arrival) and the last one was read
        private static void TellNext(Player p, float now)
        {
            if (Told.Count == 0 || now < _tellAt || p != _toldFor || !Shoulder.Arrived(p, false) || !Shoulder.Arrived(p, true) || RavenSkills.Hushed(p)) return; // not over a talk
            RavenMsg.Center(p, Told[0], 1);
            Told.RemoveAt(0);
            _tellAt = now + TellGap;
        }

        private static void Say(Player p, bool munin, string tag, Nick n)
        {
            if (Plugin.Instance == null) return;
            Composer.Extra.Clear();
            Composer.Extra["nick"] = n.Name;
            string line;
            try { line = Plugin.Instance.ComposeTagged(p, munin, tag); }
            finally { Composer.Extra.Clear(); }
            if (!string.IsNullOrEmpty(line)) RavenSkills.SayFromShoulder(p, munin, line);
        }

        // ---------------------------------------------------------------- renaming has a price
        // A new name costs 20% of the trust bar (the raven takes it to heart) and then waits 3 game days. Free, without
        // the price and without starting the wait: the first choice of a raven (a new one, or one from before this rule),
        // and one choice after it earns a gold or legendary name — one, however many it earns meanwhile (no stacking).

        internal const float RenameLoss = 0.20f; // of the trust bar to the next level
        internal const int RenameDays = 3;
        private const string FreeKey = "RavenWatch_nickFree", FreeGivenKey = "RavenWatch_nickFree0", NextKey = "RavenWatch_nickNext";

        internal static bool FreeRename(ItemDrop.ItemData r) { return Get(r, FreeKey) == "1"; }
        private static void GiveFree(ItemDrop.ItemData r) { if (r != null) r.m_customData[FreeKey] = "1"; }
        internal static float RenameWait(ItemDrop.ItemData r) { return r != null ? Mathf.Max(0f, WorldTime.Left(r.m_customData, NextKey)) : 0f; }

        // the trust a paid renaming takes now (0: the bar is full and frozen, or the top level has none)
        internal static int RenameCost(ItemDrop.ItemData r)
        {
            if (r == null || Trust.Full(r) || Trust.Need(r) <= 0f) return 0;
            return Mathf.Min(Mathf.CeilToInt(Trust.Need(r) * RenameLoss), Mathf.FloorToInt(Trust.Points(r)));
        }

        // "2 год 10 хв", "35 хв", "менше хвилини"
        internal static string WaitText(float seconds)
        {
            if (seconds < 60f) return Lang.T("менше хвилини");
            int m = Mathf.CeilToInt(seconds / 60f);
            return m >= 60 ? Lang.F("{0} год{1}", (m / 60), (m % 60 > 0 ? Lang.F(" {0} хв", (m % 60)) : "")) : Lang.F("{0} хв", m);
        }

        // why a new name can't be given now (null: it can)
        internal static string RenameBlock(Player p, ItemDrop.ItemData r, bool munin)
        {
            if (r == null || p == null) return "";
            if (Trust.Stranger(r, p)) return Lang.T("Це чужий ворон: ім'я йому дав власник.");
            if (FreeRename(r)) return null;
            if (Trust.Away(r)) return Lang.F("{0} зараз не з тобою. Нове ім'я скажеш, коли повернеться.", Plugin.RavenName(munin));
            float wait = RenameWait(r);
            if (wait > 0f) return Lang.F("{0} ще не звик до свого імені. Нове — через {1}.", Plugin.RavenName(munin), WaitText(wait));
            return null;
        }

        // the book: you pick one of the open names
        internal static string Choose(Player p, ItemDrop.ItemData r, bool munin, string id)
        {
            if (p == null || r == null) return null;
            Nick n = Find(id);
            if (n == null || !List(r, OpenKey).Contains(id)) return null;
            if (Get(r, ChosenKey) == id) return null;
            string why = RenameBlock(p, r, munin);
            if (why != null) return why;
            bool free = FreeRename(r);
            int cost = free ? 0 : RenameCost(r);
            r.m_customData[ChosenKey] = id;
            if (free) r.m_customData.Remove(FreeKey); // the free one is spent; the wait does not start from it
            else
            {
                Trust.OnRenamed(r, cost);
                // (with very long game days 3 of them would pass WorldTime.MaxAhead and read as "from another world" = over)
                WorldTime.Set(r.m_customData, NextKey, Mathf.Min(RenameDays * Power.GameDaySeconds(), WorldTime.MaxAhead - 60f));
            }
            Backup.Save(p, r);
            Publish(p);
            p.Message(MessageHud.MessageType.TopLeft, Lang.F("{0} тепер «{1}»{2}", Plugin.RavenName(munin), n.Name, (free ? Lang.T(" (безкоштовно)") : cost > 0 ? Lang.F(" (довіра −{0})", cost) : "")));
            if (Trust.Active(r) && !Trust.Away(r)) Say(p, munin, "nick_change", n);
            return null;
        }

        // ---------------------------------------------------------------- for the others

        private static void Publish(Player p)
        {
            ZDO zdo = NetFlag.Zdo(p, true);
            if (zdo == null) return;
            string h = Code(RavenSeat.On(p, false)), m = Code(RavenSeat.On(p, true));
            if (zdo.GetString(ZdoH, "") != h) zdo.Set(ZdoH, h);
            if (zdo.GetString(ZdoM, "") != m) zdo.Set(ZdoM, m);
        }

        private static string Code(ItemDrop.ItemData r)
        {
            Nick n = r != null ? Current(r) : null;
            return n != null ? n.Name + "|" + n.Call + "|" + (int)n.K : "";
        }

        // this player's raven's nickname, its call form and kind (yours from the raven, others' as published); false: none
        internal static bool Of(Player p, bool munin, out string name, out string call, out Kind kind)
        {
            name = call = null; kind = Kind.Simple;
            if (p == null) return false;
            if (p == Player.m_localPlayer)
            {
                ItemDrop.ItemData r = RavenSeat.On(p, munin);
                Nick n = r != null ? Current(r) : null;
                if (n == null) return false;
                name = n.Name; call = n.Call; kind = n.K;
                return true;
            }
            ZDO zdo = NetFlag.Zdo(p, false);
            string raw = zdo != null ? zdo.GetString(munin ? ZdoM : ZdoH, "") : "";
            if (string.IsNullOrEmpty(raw)) return false;
            string[] f = raw.Split('|');
            if (f.Length < 3 || f[0].Length == 0 || f[0].Length > 24 || f[1].Length > 24) return false;
            name = f[0].Replace("<", "").Replace(">", "");
            call = f[1].Length > 0 ? f[1].Replace("<", "").Replace(">", "") : name;
            int k;
            kind = int.TryParse(f[2], out k) && k >= 0 && k <= 3 ? (Kind)k : Kind.Simple;
            return true;
        }

        internal static string Name(Player p, bool munin)
        {
            string name, call; Kind k;
            return Of(p, munin, out name, out call, out k) ? name : Plugin.RavenName(munin);
        }

        internal static string Call(Player p, bool munin)
        {
            string name, call; Kind k;
            return Of(p, munin, out name, out call, out k) ? call : munin ? Lang.T("Муніне") : Lang.T("Хугіне");
        }

        // the colour of a kind: on the dark cards and in the chat, and on the book's paper
        internal static string Hex(Kind k) { return k == Kind.Legend ? "#FF9D5C" : k == Kind.Deed ? "#E8C35A" : k == Kind.Norov ? "#B48CE6" : "#9AA3AD"; }
        internal static string PaperHex(Kind k) { return k == Kind.Legend ? "#A8401C" : k == Kind.Deed ? "#8A6420" : k == Kind.Norov ? "#6A3F9E" : "#5A4A3A"; }
        internal static string KindName(Kind k) { return k == Kind.Legend ? Lang.T("Легендарне") : k == Kind.Deed ? Lang.T("Віха") : k == Kind.Norov ? Lang.T("За норовом") : Lang.T("Просте"); }

        // How a raven is named on this screen (plain text: the speech cards take no markup). Yours: «Хугін»;
        // another player's: its nickname · its owner.
        internal static string Title(Player p, bool munin)
        {
            if (p == null || p == Player.m_localPlayer) return Plugin.RavenName(munin);
            return Name(p, munin) + " · " + p.GetPlayerName();
        }

        // just the name, one word, yours too (the talks between players' ravens: everybody by the name the others call it),
        // in the raven's colour, an earned one in its own
        internal static string Plain(Player p, bool munin, string ravenHex)
        {
            string name, call; Kind k;
            if (!Of(p, munin, out name, out call, out k)) return "<color=" + ravenHex + ">" + Plugin.RavenName(munin) + "</color>";
            return "<color=" + (k >= Kind.Deed ? Hex(k) : ravenHex) + ">" + name + "</color>";
        }

        // the console: the names of a raven, and opening one (raven nick h|m [id])
        internal static string Cmd(Player p, bool munin, string id)
        {
            ItemDrop.ItemData r = RavenSeat.On(p, munin);
            if (r == null) return Lang.F("Нема {0} на плечі.", (munin ? Lang.T("Муніна") : Lang.T("Хугіна")));
            if (Get(r, OpenKey) == null) First(p, r, munin, false);
            if (!string.IsNullOrEmpty(id))
            {
                Nick n = Find(id);
                if (n == null) return Lang.F("Нема такого: {0}", id);
                List<string> open = List(r, OpenKey);
                if (!open.Contains(id)) { open.Add(id); PutList(r, OpenKey, open); if (n.Earned != null) Dated(r, n, Memory.Day()); }
                Tell(p, Line(munin, n));
                if (n.K >= Kind.Deed) { GiveFree(r); Say(p, munin, "nick_new", n); } // as when earned: one free renaming
            }
            List<string> rows = new List<string>();
            Nick cur = Current(r);
            foreach (Nick n in Open(r)) rows.Add((cur == n ? "● " : "  ") + n.Name + " (" + n.Id + ", " + KindName(n.K) + ")");
            return string.Join("\n", rows.ToArray());
        }
    }
}
#endif

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
    // fire in the evening — now and then talk, a line each in turn over their heads (the game's bubble). And lines of one
    // person alone: grumbling when hungry, a word at a meal, a word when it leaves. The words are plain text (built in
    // below; the author's own in config/RavenWatch/talk/chatter*.txt, read while tuning, are added to them):
    //   [chat table]          a talk at a table (fire: by the fire; any: either); talks part by an empty line,
    //   - Хто це смажив?      its lines «- » in turn: the one who starts, the other, the one who starts…
    //   - Кухар.
    //   [say hungry]          one person's lines, one per line (hungry · starving · nofood · meal · leave)
    // Who starts: the one of the two the game runs here with the lower id (never both); how often: their chattiness.
    // Light: a look round every few seconds per resident, the lines are the actor's own Say (one message each).
    internal static class NpcChatter
    {
        private sealed class Lines { public readonly List<string[]> Talks = new List<string[]>(); public readonly List<string> One = new List<string>(); }

        private static readonly string Builtin =
            "[chat table]\n" +
            "- Хто це смажив?\n- Кухар.\n- Передай йому, що м'ясо ще пручалося.\n\n" +
            "- Знову риба.\n- Учора теж була риба.\n- То це та сама?\n\n" +
            "- Смачно.\n- Ти завжди так кажеш.\n- Бо мені завжди мало.\n\n" +
            "- Хліб сьогодні м'який.\n- Бо хтось нарешті дров у піч підклав.\n\n" +
            "- Їж повільніше, ніхто не забере.\n- У мене вже пів шматка кудись зникло.\n\n" +
            "- Як думаєш, Одін теж отак сидить і їсть?\n- Він тільки п'є. А м'ясо віддає вовкам.\n- Тоді нам ще пощастило.\n\n" +
            "- Добре, що є що їсти.\n- Добре, що є з ким.\n\n" +
            "- Завтра знову на роботу.\n- Не нагадуй, бо шматок у горлі стане.\n\n" +
            "- Скажи кухарю, що солі мало.\n- От піди й скажи, у нього ніж.\n\n" +
            "- Мій дід колись цілого кабана з'їв.\n- Сам?\n- З друзями. Але найбільше — дід.\n\n" +
            "- Чуєш уночі вовків?\n- Чую. А мені здавалося, що то ти хропеш.\n\n" +
            "- Що там у казані?\n- Краще не питай. Їж, поки тепле.\n\n" +
            "[chat fire]\n" +
            "- Тепло.\n- Ага.\n- Отак би й сидіти до весни.\n\n" +
            "- Бачиш іскри? Кажуть, то душі полеглих летять до Вальгалли.\n- То чого вони гаснуть?\n- Не всі долітають.\n\n" +
            "- Розкажи щось.\n- Жив собі троль.\n- І що?\n- Досі живе. За отим пагорбом.\n\n" +
            "- Зорі сьогодні ясні.\n- Отже, завтра буде холодно.\n\n" +
            "- Чуєш, як дрова тріщать?\n- Чую. Сирі хтось приніс.\n\n" +
            "- Драконицю колись бачити доводилось?\n- Раз. Більше не хочу.\n\n" +
            "- Добре, що в нас є дах.\n- І вогонь.\n- І ліжко. Ліжко — головне.\n\n" +
            "- Як твоя спина?\n- Як у старого віслюка. А твоя?\n- Як у віслюка, якого той віслюк возив.\n\n" +
            "- Знаєш, чому ворони кружляють над базою?\n- Чому?\n- Рахують нас. Про всяк випадок.\n\n" +
            "- Тихо сьогодні.\n- Не кажи вголос, бо почують.\n\n" +
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
            "[chat meet]\n" +
            "- Як воно?\n- Та живемо.\n- Ну й добре.\n\n" +
            "- Дим над пагорбом помітно?\n- Помітно. Хтось смолу варить.\n- Або троль обідає.\n\n" +
            "- Знову ти.\n- Знову я.\n- Ну то йди вже своєю дорогою.\n\n" +
            "- Що нового?\n- Курка втекла.\n- У нас нема курей.\n- От і я про те.\n\n" +
            "- А ворони сьогодні тихі.\n- Не наврочи.\n\n" +
            "- Погода яка!\n- Яка?\n- Отака.\n\n" +
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
            "- Чуєш, як ліс дихає вночі?\n- Чую. Краще б він спав.\n\n" +
            "- Колись я бачив|бачила, як падає зоря.\n- І яке бажання?\n- Щоб не впала на мене.\n\n" +
            "- Вогонь сьогодні неспокійний.\n- Це вітер.\n- Або хтось думає про нас.\n\n" +
            "- Як гадаєш, звідки ми тут?\n- Валькірії принесли.\n- А звідки вони нас узяли?\n- Не питай того, на що нема відповіді.\n\n" +
            "- Заспівай щось.\n- Не вмію.\n- То тим смішніше буде.\n\n" +
            "- Я вчора знову чув|чула той голос уві сні.\n- Який?\n- Той, що каже: можна все забути й відпочити.\n- Не слухай. Тримайся за те, що маєш.\n\n" +
            "- Дим іде прямо вгору.\n- Отже, завтра ясно.\n- Або що вітру нема.\n- Ну так, я ж і кажу.\n\n" +
            "- Мені тут подобається.\n- Мені теж.\n- Тільки не кажи нікому, а то ще більше роботи дадуть.\n\n" +
            "- Пам'ятаєш, як ми тут уперше сіли?\n- Ага. Тоді й вогню путнього не було.\n\n" +
            "- Подай поліно.\n- Та дотягнися вже.\n- Ти ближче.\n- А в тебе ноги молодші.\n\n" +
            "- Знаєш, що найкраще в ночі?\n- Що?\n- Що вона закінчується.\n\n" +
            "- Цікаво, чи ворони мерзнуть.\n- У них пір'я.\n- У мене теж шуба, а я мерзну.\n\n" +
            "[chat meet]\n" +
            "- Куди йдеш?\n- По дрова.\n- І я.\n- То чого ми стоїмо?\n\n" +
            "- Кабана біля паркану бачили?\n- Бачив|Бачила. Він мене теж.\n\n" +
            "- Ти сьогодні ранній птах.\n- Не спалося.\n- Мені теж. То хтось хропів.\n\n" +
            "- О, ти тут.\n- А де мені ще бути?\n- Та мало де. Світ великий.\n\n" +
            "- Новину чули?\n- Яку?\n- Нема новин.\n- Ну й добре.\n\n" +
            "- Гарний ранок.\n- Вже обід.\n- То гарний обід.\n\n" +
            "- Мою сокиру ніхто не брав?\n- Ніхто. Вона там, де ти її завжди лишаєш.\n- А де я її лишаю?\n- Отож.\n\n" +
            "- Глянь, ворон на даху.\n- Рахує нас.\n- Всі на місці?\n- Здається, так.\n\n" +
            "- Що, втома?\n- Ні.\n- А чого сидиш?\n- Відпочиваю від того, що не втомився|втомилась.\n\n" +
            "- Що робиш?\n- Думаю.\n- Про що?\n- Про те, чи не піти мені робити щось.\n- І?\n- Ще думаю.\n\n" +
            "- Небо сьогодні яке синє.\n- Як очі валькірії.\n- Звідки знаєш?\n- Так кажуть.\n\n" +
            "- Чого ти сьогодні хмуришся?\n- Погода.\n- Та гарна ж погода.\n- От це й гнітить.\n\n" +
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
            "Тук-тук — і як нове.\nЩе одна латка — і хата вічна.\nЛагоджу, лагоджу… Хто ж її так б'є?\n";

        private static Dictionary<string, Lines> _all;
        internal static void Reload() { _all = null; }

        private static Lines Of(string kind)
        {
            if (_all == null) Load();
            Lines l;
            return _all.TryGetValue(kind, out l) ? l : null;
        }

        private static void Load()
        {
            _all = new Dictionary<string, Lines>();
            Parse(Builtin);
            if (RavenLooks.FromFiles && Directory.Exists(NpcTalk.Dir))
                foreach (string f in Directory.GetFiles(NpcTalk.Dir, "chatter*.txt"))
                {
                    try { Parse(File.ReadAllText(f, Encoding.UTF8)); }
                    catch (Exception e) { Npcs.Log("chatter file " + f + ": " + e.Message); }
                }
        }

        private static void Parse(string text)
        {
            Lines cur = null; bool talks = false;
            List<string> talk = new List<string>();
            Action flush = () => { if (cur != null && talk.Count > 0) cur.Talks.Add(talk.ToArray()); talk.Clear(); };
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    flush();
                    string[] w = line.Substring(1, line.Length - 2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (w.Length < 2) { cur = null; continue; }
                    talks = w[0] == "chat";
                    string key = (talks ? "chat " : "say ") + w[1];
                    if (!_all.TryGetValue(key, out cur)) _all[key] = cur = new Lines();
                    continue;
                }
                if (cur == null) continue;
                if (line.Length == 0) { if (talks) flush(); continue; }
                if (talks) talk.Add(line.StartsWith("-") ? line.Substring(1).Trim() : line);
                else cur.One.Add(line);
            }
            flush();
        }

        // ---------------------------------------------------------------- one person's line
        internal static void Mutter(NpcActor a, string kind)
        {
            if (a == null || Busy(a) || !NpcVoice.Free(a)) return; // (asleep, talked to, held quiet by a scene or the story)
            Lines l = Of("say " + kind);
            bool written = l != null && l.One.Count > 0, made = NpcPhrases.Has(a, kind);
            Player to = Player.GetClosestPlayer(a.transform.position, 20f);
            if (made && (!written || UnityEngine.Random.value < 0.5f)) { string s = NpcPhrases.Compose(a, kind, to); if (!string.IsNullOrEmpty(s)) { a.Say(s); return; } } // (written and made lines take turns)
            if (written) a.Say(a.Form(NpcPhrases.Inline(a, l.One[UnityEngine.Random.Range(0, l.One.Count)], to))); // (its own form: «живий|жива»)
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
            bool seated = m.SeatedNow;
            if (!force && !seated && !AtEase(m)) return false;
            float range = force ? 6f : seated ? 5f : 4f; // (round a fire they sit two steps off it: across it some four apart)
            NpcActor other = null; float best = range * range;
            foreach (NpcActor o in NpcActor.Live)
            {
                if (o == null || o == a || o.Mind == null || Busy(o) || !NpcResident.Is(o) || (!force && !NpcVoice.Free(o))) continue;
                if (!force && (seated ? !o.Mind.SeatedNow : (!AtEase(o.Mind) || !o.Owned))) continue; // (stopping the other one: only one run here)
                float d = (o.transform.position - a.transform.position).sqrMagnitude;
                if (d < best) { best = d; other = o; }
            }
            if (other == null) return false;
            if (force) seated = seated && other.Mind.SeatedNow;
            else
            {
                if (!First(a, other)) return false; // (the other one starts it)
                float chance = (NpcPersona.Of(a).Chatty + NpcPersona.Of(other).Chatty) * (seated ? 0.3f : 0.15f);
                if (NpcBackstory.Fact(a, "оповідач") || NpcBackstory.Fact(other, "оповідач")) chance *= 1.5f; // (one who sings or tells stories gets them talking)
                if (UnityEngine.Random.value > chance) return false;
            }
            string where = !seated ? "meet" : m.AtTable || other.Mind.AtTable ? "table" : "fire";
            List<string[]> pool = new List<string[]>();
            Lines l = Of("chat " + where); if (l != null) pool.AddRange(l.Talks);
            l = Of("chat any"); if (l != null) pool.AddRange(l.Talks);
            if (pool.Count == 0) return false;
            string[] lines = pool[UnityEngine.Random.Range(0, pool.Count)];
            if (Talking.Count > 64) Talking.Clear();
            float until = Time.time + 6f * lines.Length + 4f;
            Talking[a] = until; Talking[other] = until;
            if (!seated) { Stop(a, other, until); Stop(other, a, until); }
            r.StartCoroutine(Talk(a, other, lines, seated));
            Npcs.Log(a.Id + ": talks with " + other.Id + " (" + where + ")");
            return true;
        }

        // strolling at its ease: nothing over its own day (no work, no whim, no order)
        private static bool AtEase(NpcMind m) { return m.NowLayer <= NpcMind.Layer.Routine && (m.Now == NpcMind.Task.Wander || m.Now == NpcMind.Task.Idle); }

        // stands for the talk (a whim of its own: over by itself), turned to the other
        private static void Stop(NpcActor a, NpcActor to, float until)
        {
            a.Mind.TakeWhim(new NpcMind.Plan { Kind = NpcMind.Task.Idle, To = a.transform.position, Looks = true, Look = to.transform.position + Vector3.up * 1.6f }, until - Time.time, "talk");
        }

        // one of the two, the same on both games: the lower id
        private static bool First(NpcActor a, NpcActor b)
        {
            ZDO za = a.Zdo, zb = b.Zdo;
            if (za == null || zb == null) return false;
            return za.m_uid.UserID != zb.m_uid.UserID ? za.m_uid.UserID < zb.m_uid.UserID : za.m_uid.ID < zb.m_uid.ID;
        }

        private static IEnumerator Talk(NpcActor a, NpcActor b, string[] lines, bool seated)
        {
            bool fire = seated && !a.Mind.AtTable && !b.Mind.AtTable, finished = true; // (by a fire: its gestures — NpcFireside; at a table its hands are at its meal)
            for (int i = 0; i < lines.Length; i++)
            {
                NpcActor who = i % 2 == 0 ? a : b;
                if (a == null || b == null || a.Mind == null || b.Mind == null || (a.transform.position - b.transform.position).sqrMagnitude > 36f) { finished = false; break; }
                if (seated ? (!a.Mind.SeatedNow || !b.Mind.SeatedNow) : (a.Mind.NowLayer != NpcMind.Layer.Whim || b.Mind.NowLayer != NpcMind.Layer.Whim)) { finished = false; break; } // (one stood up, went, was called away: the talk is over)
                if (!NpcVoice.Free(a) || !NpcVoice.Free(b)) { finished = false; break; } // (a scene took one of them, you spoke to one, the story hushed them)
                who.Say(who.Form(NpcPhrases.Inline(who, lines[i], Player.GetClosestPlayer(who.transform.position, 20f)))); // («{@theme}»: a made line in a written talk)
                if (fire) NpcFireside.Line(who, who == a ? b : a);
                yield return new WaitForSeconds(Mathf.Clamp(2.5f + lines[i].Length * 0.06f, 3f, 6f));
            }
            if (fire) NpcFireside.End(a, b, finished && a != null && b != null);
            if (a != null) { Talking.Remove(a); if (!seated && a.Mind != null) Free(a.Mind); }
            if (b != null) { Talking.Remove(b); if (!seated && b.Mind != null) Free(b.Mind); }
        }

        // the talk's stop over (its whim, if it is still that one): back to its stroll
        private static void Free(NpcMind m)
        {
            m.EndWhim("talk");
        }
    }
}
#endif

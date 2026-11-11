#if RAVEN_ITEM
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuginWhispers
{
    // The Guide («Провідник»): a red raven, calm and plain, the one raven you talk to (E). He tells a new player about
    // each part of the ravens at the moment it becomes theirs: one topic a visit, short. A topic is done only once the
    // player has talked to him; until then he comes back after RetrySeconds. Each topic once per character; the minor
    // ones (Minor) a game day apart. The book lists every topic he has brought (Told) and calls him to tell one again (Replay).
    // Most topics are noticed by the Guide himself (When, checked on the plugin's tick); a few are sent from the place
    // they happen (Queue: bought, woke, death). Several waiting: the most needed first (the list order).
    // With the story the book comes from the prologue (RavenBook.Given): till then only the Bookless topics come (the
    // people's, and NoBook: a raven bought before the book, sent the moment it is — Urgent, even on the quiet first day).
    internal static class Guide
    {
        internal static bool Enabled = true;
        internal static float RetrySeconds = 720f; // world seconds between tries while he is not talked to
        internal static float GapSeconds = 1200f;  // world seconds after a talk before the next topic
        internal static float QuietSeconds = 1800f; // a new character: a game day of quiet first (its own ravens talk enough; the user 2026-11-02)
        private const string QueueKey = "RavenWatch_guideQueue", DoneKey = "RavenWatch_guideDone", AtKey = "RavenWatch_guideAt", VerKey = "RavenWatch_guideV", FromKey = "RavenWatch_guideFrom";
        internal const string Who = "guide", Name = "Провідник";

        internal const int Bought = 1, Woke = 2, Gear = 3, Gifts = 6, Food = 7, Orders = 8, Perches = 9,
            Strong = 10, Rune = 11, Gold = 13, Death = 14, Lost = 15, Mail = 16, NestErrands = 17, Binds = 18, Habit = 19, // (12: the old tree topic, gone)
            Wish = 20, Nature = 21, Home = 22, Bath = 23, Words = 24, Judge = 25, Arm = 26,
            PeopleHeart = 27, PeopleRescue = 28, PeopleJobs = 29, PeopleUpkeep = 30, PeopleDown = 31, PeopleAsk = 32, // (the people: 2.9+)
            NoBook = 33; // (the story: a raven with no book yet)

        // A minor topic (the little things of a raven's life) comes no sooner than a game day after the last talk, so a
        // player who reaches many of them at once (a veteran, a new version) hears one a day, not one every ten minutes.
        private const string LastKey = "RavenWatch_guideLast";
        // the topics he has flown in with (talked to or not): the book lists them to hear again
        private const string ShownKey = "RavenWatch_guideShown";

        private sealed class Topic
        {
            public int Id; public string Title, Text;
            public Func<Player, bool> When; // null: sent from elsewhere (Queue)
            public bool Known;              // a player who already had a woken raven before this Guide knows it (told silently)
            public bool Minor;              // waits a game day after the last talk (LastKey)
            public bool Bookless;           // may come while he has no book of ravens (the story: the rest are about the book's keys and wait for it)
            public bool Urgent;             // comes in a new character's quiet first day too
        }

        // in the order of need: when several wait, the first of them comes
        private static readonly List<Topic> Topics = new List<Topic>
        {
#if RAVEN_STORY
            // a raven bought before the book (Perks: sent at once, not to the prologue's own raven): the book comes only
            // through the prologue — and the prologue hurries for him (quests/prologue.txt: «ownraven any, !book»)
            new Topic { Id = NoBook, Known = true, Bookless = true, Urgent = true, Title = Lang.K("Ворон без книги"),
                Text = Lang.K("Ворон тепер твій, але ще спить і тебе не знає. Посади його на плече (ПКМ по ворону в сумці): хай звикає до тебе.\n\nГоворити з ним, будити й водити за собою вчить Книга воронів. Її в тебе ще нема, і жоден торговець її не продасть: вона давно чекає на того, кого кличуть.\n\nСлухай небо. Чужий ворон знає дорогу до неї: іди на його поклик.") },
#endif
            new Topic { Id = Bought, Known = true, Title = Lang.K("Ворон спить"),
                Text = Lang.K("Ворон ще спить і тебе не знає.\n\nЩоб збудити: посади його на плече (ПКМ по ворону в сумці), іди до Жертовних каменів, повісь трофей Ейктюра на його камінь, май у сумці ще {wake} — і проведи обряд: B → «Розвиток».\n\nНе до вподоби благо чи тягар — Камінь долі ворона (у Хальдора) їх перекине.") },
            new Topic { Id = Woke, Known = true, Title = Lang.K("Ворон твій"),
                Text = Lang.K("Ворон прокинувся і тепер твій. Чужих він клює.\n\nКнига воронів — B: «Ворони» (стан і норов), «Дари», «Розвиток» (довіра й обряди).\n\nСпершу він дичиться й інколи відлітає. Звикне.") },
            new Topic { Id = Gifts, Known = true, Title = Lang.K("Дари і сила"),
                When = p => Done(p, Woke) && TopLevel(p) >= 1,
                Text = Lang.K("Дари — у колі Q. Кожен коштує сили.\n\nОбидва ворони щодня набирають трохи самі. Хугін — ще в мирі й роботі, Мунін — у бою.\n\nУдари ворона ростуть із вишколом: B → «Розвиток».") },
            new Topic { Id = Food, Known = true, Title = Lang.K("Ворон голодний"),
                When = p => Hungry(p),
                Text = Lang.K("Ворон голодний. Хугін їсть ягоди, мед, гриби й рибу, Мунін — сире м'ясо. Підходить лише їжа його рівня — яка, видно в книзі.\n\nГодуй з кола J («Погодувати»). Їжа — головний шлях до довіри. Улюблену він має свою й сам натякне на неї.\n\nГолодний ворон краде їжу із сумки, а на нулі слабне й клює тебе.") },
            new Topic { Id = Death, Known = true, Title = Lang.K("Смерть"),
                Text = Lang.K("Смерть забирає у ворона довіру (на 1–2 рівнях — менше).\n\nВорони лягають у могилу з речами й стережуть її. Ворон із плеча, щойно забереш його, сяде назад на плече. Прийдеш по нього за 10 хвилин — він поверне половину втраченої довіри.\n\nЗагуби{в|ла} ворона зовсім — поверни його обрядом: B → «Ворони», сторінка ворона. Раз на 5 днів.") },
            new Topic { Id = Lost, Title = Lang.K("Чужий ворон"),
                When = p => HoldsStranger(p),
                Text = Lang.K("Цей ворон не твій: дарів не дає, не росте, нічого не пам'ятає й клює тебе.\n\nСвого загубленого ворона поверни обрядом: B → «Ворони», сторінка ворона. Старий тоді розсиплеться пір'ям.") },
            new Topic { Id = Orders, Title = Lang.K("Накази"),
                When = p => Done(p, Gifts) && TopLevel(p) >= 1,
                Text = Lang.K("Коло J — накази ворону: «Поруч», «Кружляй», «Облетіти», трюки й «Усіх назад».\n\n«Туди»: наведи приціл на гравця, тварину, річ чи будову, і ворон полетить туди.\n\nГолодний ворон (ситість до 15) наказів не слухає.") },
            new Topic { Id = Wish, Title = Lang.K("Прохання ворона"),
                When = p => HasWish(),
                Text = Lang.K("Ворон про щось попросив. Це його власне бажання, не доручення Одіна: приймати не треба, просто зроби.\n\nЩо саме — у книзі: печатка біля його імені на «Ворони». За виконане — довіра й іскринки. Не встигнеш — прохання мине саме.") },
            new Topic { Id = Gear, Title = Lang.K("Спорядження"),
                When = p => TopLevel(p) >= 3,
                Text = Lang.K("Ворон дасть себе вбрати: B → «Образ». Чотири місця — голова, шия, спина, лапи. Кожна річ дає свою силу.\n\nРечі роблять на столах ворона (молоток → RavenWatch): простий — біля кузні, просунутий — біля столу ґалдра. Речі з просунутого надіваються з 5-го рівня.\n\nТорба на спині годує ворона сама.") },
            new Topic { Id = Perches, Title = Lang.K("Жердини"),
                When = p => Done(p, Gear),
                Text = Lang.K("Молоток → RavenWatch: жердини для воронів.\n\nE біля жердини садить Хугіна, Shift+E — Муніна. Там ворон відпочиває й набирається сили. Під дахом і в затишку — швидше.\n\nПокликати його назад можна звідусіль.") },
            new Topic { Id = NestErrands, Title = Lang.K("Доручення Одіна"),
                When = p => TopLevel(p) >= 1 && Stage.Has(p, Trial.BossKey),
                Text = Lang.K("Посади воронів у Вороняче гніздо (E угорі), тоді E на чашу — вікно гнізда. Там можна дати Одінові дари (з ними він охочіше відповідає) і покликати його.\n\nОдін приходить не завжди: не прийшов — клич наступного дня. Прийняв — ворон полетить і повернеться з дорученням. З довірою доручення частішають.\n\nГнізда нема — молоток → RavenWatch, треба ще 2 роги Ейктюра. Виконані доручення запалюють руни: «Шлях Одіна» в книзі.") },
            new Topic { Id = Strong, Title = Lang.K("Ворон береже тебе"),
                When = p => TopLevel(p) >= 4,
                Text = Lang.K("Тепер ворон береже тебе в скруті: Хугін рятує від смертельного удару, Мунін люто б'ється, коли тобі погано.\n\nДалі — більші дари: з 5-го рівня Хугін віщує ранками, а Мунін мститься твоєму вбивці; з 6-го — Крила Одіна і Звір Муніна.") },
            new Topic { Id = Rune, Title = Lang.K("Перша руна"),
                When = p => RuneBook.Of(p).Opened >= 1,
                Text = Lang.K("Загорілася руна. Руни Фреї фарбують ворона, руни Бальдра дають ефект, руни Геймдаля — слід у польоті.\n\nПовісити руну: B → «Образ», ромби довкола ворона. Усі руни — у «Шляху Одіна».\n\nРуна твоя, не ворона: віддаси ворона — він її не понесе.") },
            new Topic { Id = Binds, Title = Lang.K("Воронячі в'язі"),
                When = p => FoundTree(p) && RuneBook.Of(p).Opened >= 2,
                Text = Lang.K("Ти знайш{ов|ла} Вороняче дерево. Біля коріння руни сплітаються у в'язь — силу, що діє на тобі: Бальдр — що станеться, Геймдаль — коли, Фрея підсилює.\n\nЗелене дерево бере 2 руни (ворон із довірою від 2), крижане — 3 (від 4), горіле — 4 (від 6).\n\nПосади воронів на жердину на верхівці й встій на арені. В'язь одна; вмикає її коло Q чи книга, розплітають — біля дерева.") },
            new Topic { Id = Gold, Title = Lang.K("Золото Одіна"),
                When = p => Golden(p),
                Text = Lang.K("Одін кличе на золоті доручення. За кожне — золота руна.\n\nЇї вкладають у дар ворона назавжди: I — дар сильнішає, II — дістає свій прийом, III — свою грань.\n\nДо п'яти золотих рун на ворона, усього десять. Коли отримаєш десяту, золоті доручення скінчаться.") },
            new Topic { Id = Mail, Title = Lang.K("Пошта воронів"),
                When = p => OtherRavenNear(p) && Post.LockedWhy(p) == null && (Trust.Worn(false) != null || Trust.Worn(true) != null), // (a raven of yours old enough to carry a letter)
                Text = Lang.K("Поруч гравець із воронами. Йому можна слати листи й посилки.\n\nКонверт у кутку книги (B). Лист — 20 сили й пергамент (у Хальдора), з 2-го рівня довіри ворона; посилка — 30 сили й одна річ до 2 кг, з 3-го.") },

#if RAVEN_NPC
            // ---- the people (NPC): their heart, the ones in need, their work, their keep
            new Topic { Bookless = true, Id = PeopleDown, Title = Lang.K("Без тями"),
                When = p => PeopleNear(p, 40f, a => NpcVitals.IsDown(a)),
                Text = Lang.K("Мешканця побили, і він лежить без тями. Не помре.\n\nМедовуха зцілення одразу поставить його на ноги. Без неї він згодом підведеться сам і поплентається додому відлежуватись.") },
            new Topic { Bookless = true, Id = PeopleRescue, Title = Lang.K("Люди в біді"),
                When = p => RumourNear(p),
                Text = Lang.K("На дошці Серця оселі з'явилась чутка про людину в біді, а на мапі — позначка.\n\nНе барись: допоможи їй і поговори з нею. Врятованих можна покликати жити у свою оселю.") },
            new Topic { Bookless = true, Id = PeopleHeart, Title = Lang.K("Серце оселі"),
                When = p => HeartNear(p),
                Text = Lang.K("Серце оселі — домівка для людей, яких ти врятуєш.\n\nДай йому назву, інакше сюди нікого не покличеш. На дошці серця видно, хто тут живе, чого бракує і які ходять чутки.\n\nУ скарбничку внизу клади монети — з неї робітники беруть платню.") },
            new Topic { Bookless = true, Id = PeopleJobs, Title = Lang.K("Робота мешканців"),
                When = p => PeopleNear(p, 20f, a => NpcResident.Is(a) && NpcTalk.Hired(a)), // (your own workers: not a story person who lives by its fire)
                Text = Lang.K("Кожному мешканцю можна дати роботу, ящик і ліжко. Більшості робіт потрібен ящик: звідти беруть і туди складають.\n\nДеяким роботам треба ще щось: лісорубу — сокира й ділянка, вартовому — пост, слузі — дім.") },
#endif
            // ---- minor: one a game day at most (Minor)
            new Topic { Id = Nature, Minor = true, Title = Lang.K("Норов"),
                When = p => Done(p, Gifts) && TopLevel(p) >= 1,
                Text = Lang.K("У кожного ворона свій норов: характер (як поводиться й куди літає), хист (його сильна сторона) і примха (маленька вада).\n\nУсе це — у книзі: B → «Ворони». Наведи на картку — там пояснення. Примху довіра не змінить, до неї просто звикають.") },
            new Topic { Id = Home, Minor = true, Title = Lang.K("Ворон удома"),
                When = p => Done(p, Perches),
                Text = Lang.K("Ворона можна лишити вдома: глянь на жердину й натисни {home} (із Shift — Муніна). Він житиме там і стерегтиме, що бачить, а плече звільниться для іншого.\n\nНа вартовій жердині він лише вартує й б'є ворогів довкола. Б'є тільки ситий — не забувай його годувати.\n\nЗабрати — E біля тієї жердини: звідусіль його не покличеш.") },
            new Topic { Id = Habit, Minor = true, Title = Lang.K("Звички ворона"),
                When = p => HasHabit(),
                Text = Lang.K("Твій ворон набув звички — від того, як ти граєш останні дні.\n\nКожна дає і плюс, і мінус. Тримається щонайменше п'ять днів, а тоді минає, якщо граєш інакше. Не більше двох разом.\n\nУ книзі (B → «Ворони») — картка «Звичка» біля норову. Наведи на неї: там написано, як її позбутися або зберегти.") },
            new Topic { Id = Bath, Minor = true, Title = Lang.K("Брудний ворон"),
                When = p => Dirty(),
                Text = Lang.K("Ворон забруднився: болото, попіл, кров чи земля. Часом він сам злітає митися до води поруч.\n\nПомити можна й самому: стань у воду — у колі J з'явиться «Помити». Гаряча купіль іще краща.\n\nВідмитий ворон якийсь час б'є сильніше.") },
            new Topic { Id = Words, Minor = true, Title = Lang.K("Слова ворона"),
                When = p => LearnsWords(),
                Text = Lang.K("Ворон учиться слів: підхоплює їх із чату поруч або просить навчити.\n\nНове слово чекає в книзі (печатка ᚨ біля імені): його можна лишити чи викинути й обрати, коли казати. Спершу ворон слово перекручує, а що частіше каже — то чистіше.") },
            new Topic { Id = Judge, Minor = true, Title = Lang.K("Суддя бою"),
                Text = Lang.K("Після доброго бою ворон оцінює його — від однієї до п'яти зірок. Хугін підкаже, що було не так, Мунін покепкує.\n\nБій як у сагах дає трохи довіри, а після доброго бою ворон часом покаже свою схованку.") },
            new Topic { Id = Arm, Minor = true, Title = Lang.K("На руку"),
                When = p => TopLevel(p) >= ArmPerch.MinTrust,
                Text = Lang.K("Тепер ворон сідає на руку. У колі J біля його крила — кнопки: на руку й назад, погладити, нагодувати з руки, глянути в очі, підкинути.\n\nЗброю з тієї руки буде прибрано. Рушиш у бій — ворон сам стрибне на плече.") },
#if RAVEN_NPC
            new Topic { Bookless = true, Id = PeopleUpkeep, Minor = true, Title = Lang.K("Ціна праці"),
                When = p => Done(p, PeopleJobs) && PeopleNear(p, 30f, a => a.Worker != null && a.Worker.Works),
                Text = Lang.K("Мешканцям треба їсти, відпочивати й отримувати платню.\n\nЇдять вони готову їжу з комори — ящика кухаря чи ящика біля столу. Ситі, відпочилі й вчасно оплачені працюють краще, а довго невдоволені колись підуть.") },
            new Topic { Bookless = true, Id = PeopleAsk, Minor = true, Title = Lang.K("Прохання мешканців"),
                When = p => PeopleNear(p, 30f, a => NpcRequests.Of(a) != null && !NpcLevers.IsProtected(a)),
                Text = Lang.K("Мешканець про щось просить. Спитай його сам або глянь на дошку серця.\n\nПринесеш — віддячить.") },
#endif
        };

        private static Topic Find(int id) { foreach (Topic t in Topics) if (t.Id == id) return t; return null; }

        private static int _current; // the topic on its way now, 0 for none

        // ---------------------------------------------------------------- what he notices
        private static List<ItemDrop.ItemData> Owned(Player p)
        {
            List<ItemDrop.ItemData> l = new List<ItemDrop.ItemData>();
            if (p == null) return l;
            bool m;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) if (RavenItem.IsRaven(item, out m) && !Trust.Stranger(item, p)) l.Add(item);
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (!l.Contains(item) && RavenItem.IsRaven(item, out m) && !Trust.Stranger(item, p)) l.Add(item);
            return l;
        }

        // the trust level of your best raven (0: none woken)
        private static int TopLevel(Player p)
        {
            int best = 0;
            foreach (ItemDrop.ItemData item in Owned(p)) best = Mathf.Max(best, Trust.SettledLevel(item));
            return best;
        }

        private static bool Hungry(Player p)
        {
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item != null && Trust.Active(item) && !Trust.Stranger(item, p) && Satiety.Zone(item) <= Satiety.Hungry) return true;
            }
            return false;
        }

        private static bool HasHabit()
        {
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData item = Trust.Worn(m); if (item != null && Trust.Active(item) && Habits.Now(item).Count > 0) return true; }
            return false;
        }

        // a worn, woken raven of yours (each of the two), for the topics about its own life
        private static IEnumerable<ItemDrop.ItemData> WornAwake()
        {
            Player p = Player.m_localPlayer;
            foreach (bool m in new bool[] { false, true })
            {
                ItemDrop.ItemData item = Trust.Worn(m);
                if (item != null && Trust.Active(item) && !Trust.Stranger(item, p)) yield return item;
            }
        }

        private static bool HasWish() { foreach (ItemDrop.ItemData item in WornAwake()) if (Requests.Of(item) != null) return true; return false; }
        private static bool Dirty() { foreach (ItemDrop.ItemData item in WornAwake()) if (global::HuginWhispers.Bath.Dirty(item)) return true; return false; }
        private static bool LearnsWords()
        {
            foreach (ItemDrop.ItemData item in WornAwake()) if (global::HuginWhispers.Words.Open(item) && (global::HuginWhispers.Words.Of(item).Count > 0 || global::HuginWhispers.Words.Heard(item).Length > 0)) return true;
            return false;
        }

        // the keys and numbers in a text, as they are now
        private static string Fill(string text) { return text.Replace("{wake}", Trust.WakeText()).Replace("{home}", RavenKeys.LabelOf(RavenKeys.HomeId)).Replace("{q}", RavenKeys.LabelOf(RavenKeys.GiftsId)); }

        // a minor topic may come: a game day since the last talk
        private static bool MinorOk(Player p)
        {
            string raw; double last;
            if (!p.m_customData.TryGetValue(LastKey, out raw) || !double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out last)) return true;
            double day = EnvMan.instance != null ? Mathf.Max(60f, EnvMan.instance.m_dayLengthSec) : 1800f;
            double since = WorldTime.Now - last;
            return since < 0 || since >= day; // (a time from another world: no wait)
        }

        private static bool HoldsStranger(Player p)
        {
            bool m;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems()) if (RavenItem.IsRaven(item, out m) && Trust.Stranger(item, p)) return true;
            foreach (ItemDrop.ItemData item in RavenSeat.Shelved(p)) if (RavenItem.IsRaven(item, out m) && Trust.Stranger(item, p)) return true;
            return false;
        }

#if RAVEN_NPC
        // the people near you (NpcActor), one that ok lets through
        private static bool PeopleNear(Player p, float range, Func<NpcActor, bool> ok)
        {
            foreach (NpcActor a in NpcActor.Live) if (a != null && Vector3.Distance(a.transform.position, p.transform.position) < range && ok(a)) return true;
            return false;
        }
        private static bool HeartNear(Player p) { foreach (global::HuginWhispers.NpcHeart h in global::HuginWhispers.NpcHeart.Loaded) if (h != null && Vector3.Distance(h.transform.position, p.transform.position) < 25f) return true; return false; }
        private static bool RumourNear(Player p)
        {
            foreach (global::HuginWhispers.NpcHeart h in global::HuginWhispers.NpcHeart.Loaded)
            {
                if (h == null || Vector3.Distance(h.transform.position, p.transform.position) > 40f) continue;
                ZNetView nv = h.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO().GetString(global::HuginWhispers.NpcRescue.KInfo, "").Length > 0) return true;
            }
            return false;
        }
#endif

        private static bool FoundTree(Player p) { string raw; return p.m_customData.TryGetValue(RavenTree.KnownKey, out raw) && raw.Replace("v2|", "").Trim('|').Length > 0; }

        private static bool Golden(Player p)
        {
            RuneBook.Data d = RuneBook.Of(p);
            if (d.Gold > 0 || d.GoldEver > 0) return true;
            ErrandRun r = Errands.Of(p);
            return r != null && r.Def != null && r.Def.Golden;
        }

        // another player near with a raven on the shoulder: he has the mod (the post is for him)
        private static bool OtherRavenNear(Player p)
        {
            foreach (Player o in Player.GetAllPlayers())
            {
                if (o == null || o == p || Vector3.Distance(o.transform.position, p.transform.position) > 40f) continue;
                ZNetView nv = o.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid() && nv.GetZDO().GetInt("RavenWatch_Shoulder", 0) != 0) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- the lists in the character
        private static List<int> Read(Player p, string key)
        {
            List<int> list = new List<int>();
            string raw;
            if (p == null || !p.m_customData.TryGetValue(key, out raw) || string.IsNullOrEmpty(raw)) return list;
            foreach (string part in raw.Split(','))
            {
                int n;
                if (int.TryParse(part, out n)) list.Add(n);
            }
            return list;
        }

        private static void Write(Player p, string key, List<int> list)
        {
            List<string> parts = new List<string>();
            foreach (int n in list) parts.Add(n.ToString());
            p.m_customData[key] = string.Join(",", parts.ToArray());
        }

        private static bool Done(Player p, int id) { return Read(p, DoneKey).Contains(id); }

        internal static bool IsGuide(string key) { return key == Plugin.KeyPrefix + Who; }

        // a topic to tell, not before delaySeconds (world time; a longer wait already set stays), once per character
        internal static void Queue(Player p, int id, float delaySeconds)
        {
            if (!Enabled || p == null || p != Player.m_localPlayer || Find(id) == null) return;
            if (Read(p, DoneKey).Contains(id)) return;
            List<int> q = Read(p, QueueKey);
            if (q.Contains(id)) return;
            q.Add(id);
            Write(p, QueueKey, q);
            if (WorldTime.Left(p.m_customData, AtKey) < delaySeconds) WorldTime.Set(p.m_customData, AtKey, delaySeconds);
        }

        // the player died: with a raven of his on the shoulder, the Guide tells about it after
        internal static void Died(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return;
            foreach (bool m in new bool[] { false, true }) { ItemDrop.ItemData item = Trust.Worn(m); if (item != null && Trust.Active(item) && !Trust.Stranger(item, p)) { Queue(p, Death, 60f); return; } }
        }

        // A player who had woken ravens before this Guide (an older version) knows the start and what is already his:
        // once, those topics are marked told without a word. A new player gets everything.
        private static void CatchUp(Player p)
        {
            string v;
            if (p.m_customData.TryGetValue(VerKey, out v) && v == "2") return;
            p.m_customData[VerKey] = "2";
            bool veteran = TopLevel(p) >= 1;
            if (!veteran) foreach (string k in p.m_customData.Keys) if (k.StartsWith("RavenWatch_copy_")) { veteran = true; break; } // (a woken raven of his, now lost)
            if (!veteran) return;
            List<int> done = Read(p, DoneKey), q = Read(p, QueueKey);
            foreach (Topic t in Topics)
            {
                bool now = false;
                try { now = t.When != null && t.When(p); } catch (Exception) { }
                if ((t.Known || now) && !done.Contains(t.Id)) done.Add(t.Id);
                q.Remove(t.Id);
            }
            Write(p, DoneKey, done);
            Write(p, QueueKey, q);
        }

        private static float _nextLook;

        // a new character's first game day: he does not come yet (what he would tell waits in the queue); one who had
        // woken ravens already when this came is no newcomer
        private static bool Quiet(Player p)
        {
            string raw; double from;
            if (!p.m_customData.TryGetValue(FromKey, out raw) || !double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out from))
            {
                from = WorldTime.Now - (TopLevel(p) >= 1 ? QuietSeconds : 0f);
                p.m_customData[FromKey] = from.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
            return WorldTime.Now - from < QuietSeconds;
        }

        // from the plugin's tick: the Guide comes with the next topic when the way is clear; true when he was sent.
        // The topic stays in the queue until talked, so if he is ignored he comes again after RetrySeconds.
        internal static bool Tick(Plugin plugin, Player p)
        {
            if (!Enabled || plugin == null || p == null || p.IsDead() || Mute.SelfMuted) return false;
            CatchUp(p);
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + 5f;
                List<int> done = Read(p, DoneKey), q = Read(p, QueueKey);
                foreach (Topic t in Topics)
                {
                    if (t.When == null || done.Contains(t.Id) || q.Contains(t.Id)) continue;
                    bool now = false;
                    try { now = t.When(p); } catch (Exception e) { if (Plugin.Instance != null) Plugin.Instance.Log("Guide " + t.Id + ": " + e.Message); }
                    if (now) Queue(p, t.Id, 20f);
                }
            }
            if (plugin.RavenBusy || p.InInterior() || p.IsSleeping() || Senses.Fight) return false;
            bool quiet = Quiet(p);
            bool book = true;
#if RAVEN_STORY
            if (StoryScenes.Playing.Length > 0) return false; // (a scene of the story: never into it)
            if (StoryQuests.WantsScene(p) || Locs.Holding(p.transform.position)) return false; // (nor when one is about to start, nor in a place of the story: a raven outside the Hall held the next scene up, 2026-11-05)
            book = RavenBook.Given(p); // (no book yet: only what needs none — the rest is about its keys)
#endif
            List<int> queue = Read(p, QueueKey);
            if (queue.Count == 0 || WorldTime.Left(p.m_customData, AtKey) > 0f) return false;
            List<int> told = Read(p, DoneKey);
            // "it only sleeps" is no news once the raven has woken
            if (!told.Contains(Bought) && (queue.Contains(Woke) || told.Contains(Woke))) { told.Add(Bought); Write(p, DoneKey, told); }
            // "no book yet" is no news once the book is his
            if (book && queue.Contains(NoBook) && !told.Contains(NoBook)) { told.Add(NoBook); Write(p, DoneKey, told); }
            Topic next = null;
            bool minorOk = MinorOk(p), waiting = false;
            foreach (Topic t in Topics)
            {
                if (!queue.Contains(t.Id) || told.Contains(t.Id)) continue;
                if (quiet && !t.Urgent) { waiting = true; continue; } // (a new character's first game day)
                if (!book && !t.Bookless) { waiting = true; continue; } // (till the prologue gives the book)
                if (t.Minor && !minorOk) { waiting = true; continue; } // (a game day after the last talk)
                next = t; break;
            }
            if (next == null)
            {
                if (waiting) return false;
                queue.RemoveAll(id => Find(id) == null || told.Contains(id)); // a topic from an older version, or already told
                Write(p, QueueKey, queue);
                return false;
            }
            WorldTime.Set(p.m_customData, AtKey, Mathf.Max(60f, RetrySeconds));
            _current = next.Id; _replay = false;
            List<int> shown = Read(p, ShownKey);
            if (!shown.Contains(next.Id)) { shown.Add(next.Id); Write(p, ShownKey, shown); }
            plugin.Visit(false, Lang.T(next.Title), Fill(Lang.T(next.Text)), Who);
            return true;
        }

        // what he has told or brought (the player may have let him fly off): id and title, in the topics' order, for the
        // book's list (RavenBookGuide) — a click there calls him to tell it again (Replay)
        internal static List<KeyValuePair<int, string>> Told(Player p)
        {
            List<KeyValuePair<int, string>> l = new List<KeyValuePair<int, string>>();
            if (p == null) return l;
            List<int> done = Read(p, DoneKey), shown = Read(p, ShownKey);
            foreach (Topic t in Topics)
                if (done.Contains(t.Id) || shown.Contains(t.Id)) l.Add(new KeyValuePair<int, string>(t.Id, Grammar.Fix(Lang.T(t.Title))));
            return l;
        }

        private static bool _replay; // the topic on its way is one asked for again from the book

        // from the book: he flies in and tells this topic again; null when sent, else why not now
        internal static string Replay(Player p, int id)
        {
            Plugin plugin = Plugin.Instance;
            Topic t = Find(id);
            if (plugin == null || p == null || t == null) return Lang.T("Провідника зараз нема.");
            if (p.IsDead() || p.IsSleeping()) return Lang.T("Не зараз.");
            if (p.InInterior()) return Lang.T("Під дах Провідник не залітає. Вийди надвір.");
            if (Senses.Fight) return Lang.T("Посеред бою Провідник не прилетить.");
            if (plugin.RavenBusy) return plugin.GuideOnWay ? Lang.T("Провідник уже летить до тебе: вислухай його спершу.") : Lang.T("Зараз говорить інший ворон. Спробуй за хвилину.");
            _current = id; _replay = true;
            plugin.Visit(false, Lang.T(t.Title), Fill(Lang.T(t.Text)), Who);
            return null;
        }

        // the player has talked to him (E): this topic is told
        internal static void Talked()
        {
            Player p = Player.m_localPlayer;
            if (_current == 0 || p == null) return;
            List<int> q = Read(p, QueueKey);
            q.Remove(_current);
            Write(p, QueueKey, q);
            List<int> done = Read(p, DoneKey);
            if (!done.Contains(_current)) done.Add(_current);
            Write(p, DoneKey, done);
            _current = 0;
            bool replay = _replay; _replay = false;
            WorldTime.Set(p.m_customData, AtKey, Mathf.Max(60f, GapSeconds)); // the next topic not right after this one
            if (!replay) p.m_customData[LastKey] = WorldTime.Now.ToString("R", System.Globalization.CultureInfo.InvariantCulture); // (the minor ones: a game day)
        }

        // console "raven guide": everything anew, as for a new player
        internal static string Reset(Player p)
        {
            if (p == null) return Lang.T("персонажа нема");
            p.m_customData.Remove(DoneKey);
            p.m_customData.Remove(QueueKey);
            p.m_customData.Remove(AtKey);
            p.m_customData.Remove(LastKey);
            p.m_customData.Remove(ShownKey);
            p.m_customData[VerKey] = "2";
            _current = 0;
            foreach (ItemDrop.ItemData item in Owned(p)) { if (!Trust.Active(item)) Queue(p, Bought, 0f); else Queue(p, Woke, 0f); }
            return Lang.F("Провідник розповість усе знову ({0} тем, коли до них дійде).", Topics.Count);
        }
    }

    // While the Guide's text is on a raven, that raven is crimson (redder than Munin) and named «Провідник» — both in
    // the hover and in the talk window. Material copies, made once per original; the originals come back after.
    internal static class GuideLook
    {
        internal static Color Crimson = new Color(0.85f, 0.06f, 0.1f);
        private static readonly Dictionary<Material, Material> Made = new Dictionary<Material, Material>();
        private class Saved { public string Name; public Renderer[] Renderers; public Material[][] Mats; public int Version; public RavenLooks.Painted Paint; }
        private static readonly Dictionary<Raven, Saved> Worn = new Dictionary<Raven, Saved>();

        internal static void Update(Raven r)
        {
            Raven.RavenText cur = Plugin.CurrentText(r);
            bool guide = cur != null && Guide.IsGuide(cur.m_key);
            Saved s;
            bool on = Worn.TryGetValue(r, out s);
            if (guide && on && s.Version != RavenLooks.Version) { Take(r, s); on = false; } // a look saved in «Фарби»: re-dressed at once
            if (guide && !on) Put(r);
            else if (!guide && on) Take(r, s);
        }

        private static void Put(Raven r)
        {
            Saved s = new Saved { Name = r.m_name, Renderers = r.GetComponentsInChildren<Renderer>(true), Version = RavenLooks.Version };
            s.Mats = new Material[s.Renderers.Length][];
            Look look = null;
            try { look = RavenLooks.ForGuide(); if (look != null) s.Paint = RavenLooks.Use(look, r.m_isMunin); }
            catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Guide look: " + e.Message); }
            for (int i = 0; i < s.Renderers.Length; i++)
            {
                Material[] list = s.Renderers[i].sharedMaterials;
                s.Mats[i] = list;
                // the raven itself wears its look (painted feathers, eyes, sheen); the smoke and the rest keep the crimson tint
                Material[] painted = null;
                if (s.Paint != null && !(s.Renderers[i] is ParticleSystemRenderer)) painted = RavenLooks.Dress(s.Paint, list);
                Material[] tinted = new Material[list.Length];
                for (int j = 0; j < list.Length; j++) tinted[j] = painted != null && RavenLooks.IsBody(list[j]) ? painted[j] : Tint(list[j]);
                s.Renderers[i].sharedMaterials = tinted;
            }
            // the things its look hangs on it (after the renderers above were noted: those are its own)
            if (look != null && (look.Things.Count > 0 || look.Fx.Count > 0))
            {
                int layer = r.m_visual != null ? r.m_visual.layer : -1;
                try { LookThings.Hang(r.gameObject, look, r.m_isMunin, layer); FxLib.Hang(r.gameObject, look.Fx, r.m_isMunin, layer); }
                catch (System.Exception e) { if (Plugin.Instance != null) Plugin.Instance.LogError("Guide look things: " + e); }
            }
            r.m_name = Lang.W(Guide.Name);
            Worn[r] = s;
        }

        private static void Take(Raven r, Saved s)
        {
            LookThings.Unhang(r.gameObject);
            FxLib.Unhang(r.gameObject);
            for (int i = 0; i < s.Renderers.Length; i++) if (s.Renderers[i] != null) s.Renderers[i].sharedMaterials = s.Mats[i];
            RavenLooks.Drop(s.Paint);
            r.m_name = s.Name;
            Worn.Remove(r);
        }

        // a copy cloned from a raven that wears the look right now (another player's visiting raven is cloned from ours):
        // its own materials back, the things and smoke the look hung on it off
        internal static void Undress(Raven r, GameObject copy)
        {
            Saved s;
            if (r == null || copy == null || r.m_visual == null || !Worn.TryGetValue(r, out s)) return;
            Dictionary<Renderer, Material[]> own = new Dictionary<Renderer, Material[]>();
            for (int i = 0; i < s.Renderers.Length; i++) if (s.Renderers[i] != null) own[s.Renderers[i]] = s.Mats[i];
            Renderer[] from = r.m_visual.GetComponentsInChildren<Renderer>(true), to = copy.GetComponentsInChildren<Renderer>(true);
            if (from.Length != to.Length) return;
            for (int i = 0; i < from.Length; i++)
            {
                Material[] m;
                if (own.TryGetValue(from[i], out m)) to[i].sharedMaterials = m;
                else UnityEngine.Object.Destroy(to[i].gameObject); // (not there when the look was put on: hung by it)
            }
        }

        internal static void Forget(Raven r) { Saved s; if (Worn.TryGetValue(r, out s)) RavenLooks.Drop(s.Paint); Worn.Remove(r); }

        private static Material Tint(Material m)
        {
            if (m == null) return null;
            Material c;
            if (Made.TryGetValue(m, out c) && c != null) return c;
            c = new Material(m);
            c.name = m.name + " (guide)";
            c.hideFlags = HideFlags.HideAndDontSave;
            if (c.HasProperty("_Color"))
            {
                Color col = m.color;
                Color tinted = Color.Lerp(col, Crimson, 0.55f);
                tinted.a = col.a;
                c.color = tinted;
            }
            if (c.HasProperty("_EmissionColor"))
            {
                Color e = m.GetColor("_EmissionColor");
                float k = Mathf.Max(e.r, Mathf.Max(e.g, e.b));
                if (k > 0.01f) c.SetColor("_EmissionColor", Crimson * k * 1.3f);
            }
            Made[m] = c;
            return c;
        }
    }
}
#endif

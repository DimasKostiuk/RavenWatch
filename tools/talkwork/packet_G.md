# Packet G: scenes x_intro, x_greet, x_rename

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): x_intro → «intro.», x_greet → «greet.», x_rename → «rename.»

## Scenes (steps; ? = optional)
- x_intro: A open (x_intro_open) → B reply (_reply) → [C join (_join, ~85%) → D join (~75%) → A or B answers the last joiner (x_join_reply)] → A close (_close, optional)
- x_greet: A open (x_greet_open) → B reply (_reply) → [C join (_join, ~85%) → D join (~75%) → A or B answers the last joiner (x_join_reply)] → A close (_close, optional)
- x_rename: A open (x_rename_open) → B reply (_reply) → [C join (_join, ~85%) → D join (~75%) → A or B answers the last joiner (x_join_reply)] → A close (_close, optional)

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### H @хх_знайомство   (used by scenes: x_intro)
0 | ^ім'я Я Хугін, а кличуть мене {me}. Ти хто будеш? | ^ім'я I'm Hugin, and they call me {me}. And who might you be?
1 | ^хто Не бачив тебе раніше. Чий будеш? | ^хто Haven't seen you before. Whose are you?
2 | ^хто Хм. Ще один ворон. Звідки ти такий узявся? | ^хто Hm. Another raven. Where did you come from?
3 | ^ім'я Я Хугін. Думка Одіна, якщо тобі це щось каже. А ти хто? | ^ім'я I'm Hugin. Odin's thought, if that means anything to you. And who are you?
4 | ^господар Твоя людина — {friend}? Співчуваю. Або заздрю, ще не вирішив. | ^господар Your human is {friend}? My condolences. Or my envy, I haven't decided.
5 | ~1 ^хто Чужий ворон. Тримай дзьоб при собі, і ми поладнаємо. | ~1 ^хто A stranger's raven. Keep your beak to yourself and we'll get along.
6 | ~23 ^ім'я Вітаю, {addr}. Я Хугін. Моя людина — {name}. А твоя? | ~23 ^ім'я Greetings, {addr}. I'm Hugin. My human is {name}. And yours?
7 | ~3 ^ім'я Доброго дня. Я Хугін, а он моя гордість і мій клопіт — {name}. | ~3 ^ім'я Good day. I'm Hugin, and over there is my pride and my trouble — {name}.
8 | ^хто Не пригадую тебе. А в мене, повір, добра пам'ять на дзьоби. | ^хто I don't remember you. And believe me, I have a good memory for beaks.
9 | ^хто Новенький? Звідки прилетів і чого тут шукаєш? | ^хто New here? Where did you fly in from and what are you looking for?
10 | ^ім'я Дозволь відрекомендуватися: Хугін. Для друзів — {me}. Для ворогів — неприємність. | ^ім'я Allow me to introduce myself: Hugin. {me} to friends. Trouble to enemies.
11 | ^господар Значить, твоя людина — {friend}. Мужності тобі. | ^господар So your human is {friend}. Courage to you.
12 | ~1 ^хто Ще один пернатий нахлібник. Чий ти? | ~1 ^хто Another feathered freeloader. Whose are you?
13 | ~3 ^ім'я Радий новому крилу. Я Хугін. А тебе як звати? | ~3 ^ім'я Glad of a new wing. I'm Hugin. And what's your name?

### H @хх_знайомство_відповідь   (used by scenes: x_intro)
0 | ^хто|ім'я Хугін. Моя людина — {name}. Не судіть строго. | ^хто|ім'я Hugin. My human is {name}. Don't judge too harshly.
1 | ^хто|ім'я Хугін я. Думаю за двох. Друга половина — {name}. | ^хто|ім'я I'm Hugin. I think for two. The other half is {name}.
2 | ^ім'я Приємно. Я Хугін і, на відміну від деяких, умію мовчати. | ^ім'я A pleasure. I'm Hugin and, unlike some, I know how to keep quiet.
3 | ^господар Співчувати не треба. {name} — найкраще, що зі мною траплялося. Після сиру. | ^господар No need for condolences. {name} is the best thing that ever happened to me. After cheese.
4 | ^господар Заздри, {addr}. Мені пощастило більше, ніж тобі. | ^господар Envy me, {addr}. I got luckier than you.
5 | ~1 ^хто|ім'я Тобі яке діло? Ну, Хугін. Задоволений? | ~1 ^хто|ім'я What's it to you? Fine, Hugin. Happy?
6 | ~1 ^господар Співчувай, співчувай. Я й сам собі співчуваю. | ~1 ^господар Go on, pity me. I pity myself.
7 | ~3 ^хто|ім'я Хугін. Моя людина — {name}. Ми давно разом і багато пройшли. | ~3 ^хто|ім'я Hugin. My human is {name}. We've been together a long time and come a long way.
8 | ^смак Смак у мене бездоганний. Як і в моєї людини, коли вона мовчить. | ^смак My taste is impeccable. Like my human's, when {he|she} keeps quiet.
9 | ^пам'ять Запам'ятав? Я теж. Я, правда, думаю, а не пам'ятаю, але тебе запам'ятаю. | ^пам'ять Remembered? Me too. Well, I think rather than remember, but I'll remember you.
10 | Хугін. Будемо знайомі. | Hugin. Pleased to meet you.
11 | ^хто|ім'я Хугін. Для своїх — {me}. Ти поки не свій, але все попереду. | ^хто|ім'я Hugin. {me} to my own. You're not one of them yet, but there's time.
12 | ^хто Мене звати {me}. Я Хугін, а моя людина — {name}. | ^хто My name is {me}. I'm Hugin, and my human is {name}.
13 | ^хто|ім'я {me}. Хугін. Моя людина — {name}, без мене {він|вона} загубиться. | ^хто|ім'я {me}. Hugin. My human is {name}, {he'd|she'd} get lost without me.
14 | ^господар Мужність не завадить. Але я справляюся. | ^господар Courage wouldn't hurt. But I manage.
15 | ^господар Так, моя. Не найгірша, повір. | ^господар Yes, mine. Not the worst, believe me.
16 | ^смак Смак? У мене? Бездоганний. Глянь хоча б на моє пір'я. | ^смак Taste? Mine? Impeccable. Just look at my feathers.
17 | ^пам'ять Пам'ять — то добре. Тільки не пам'ятай зайвого. | ^пам'ять Memory is good. Just don't remember too much.
18 | ~1 ^хто|ім'я Хугін. Відчепися з розпитуваннями. | ~1 ^хто|ім'я Hugin. Stop with the questions.
19 | ~3 ^хто|ім'я Хугін. Радий знайомству, справді. | ~3 ^хто|ім'я Hugin. Glad to meet you, really.

### H @хх_знайомство_кінець   (used by scenes: x_intro)
0 | Ну що ж, будемо знайомі. | Well then, pleased to meet you.
1 | Запам'ятаю тебе, {addr}. У мене на обличчя пам'ять добра. На дзьоби теж. | I'll remember you, {addr}. I have a good memory for faces. For beaks too.
2 | ^господар|смак Передай своїй людині привіт. Чи що там у вас передають. | ^господар|смак Say hello to your human. Or whatever it is you pass on.
3 | ^хто Ось і познайомились. Далі буде цікавіше. | ^хто There, we've met. It'll get more interesting from here.
4 | ~1 Тільки не сідай на мою гілку. | ~1 Just don't sit on my branch.
5 | ~3 Радий знайомству. Справді радий. | ~3 Glad to meet you. Truly glad.
6 | Гаразд. Тепер ми знайомі. Не підведи. | All right. Now we know each other. Don't let me down.
7 | ^ім'я Запишу твоє ім'я. Десь між рунами й рецептами каші. | ^ім'я I'll write down your name. Somewhere between the runes and porridge recipes.
8 | ~1 Познайомились. Можна вже мовчати? | ~1 We've met. Can we be quiet now?
9 | ~3 Тепер у тебе є знайомий ворон. Цінуй. | ~3 Now you have a raven you know. Treasure it.

### H @хх_знайомство_встряє   (used by scenes: x_intro)
0 | ^хто|ім'я А я {me}. Раз уже всі знайомляться. | ^хто|ім'я And I'm {me}. Since everyone's getting acquainted.
1 | ^господар Людина як людина. Бачив і гірших. | ^господар A human like any other. I've seen worse.
2 | ^смак|господар Про смак не сперечаються. Але я посперечаюся. | ^смак|господар There's no arguing about taste. But I'll argue.
3 | ^пам'ять Пам'ять у всіх є. Розуму б ще. | ^пам'ять Everyone has a memory. If only they had brains too.
4 | Можна і я познайомлюся? Я {me}. Чую все, кажу мало. | May I introduce myself too? I'm {me}. I hear everything, say little.
5 | Ще один новенький? Ну, ласкаво просимо до зграї. | Another newcomer? Well, welcome to the flock.
6 | ^хто Звідки — неважливо. Важливо, чи вміє мовчати. | ^хто Where from doesn't matter. What matters is whether it can keep quiet.
7 | ^хто Чий би не був, а пір'я в нього чисте. Підозріло. | ^хто Whoever it belongs to, its feathers are clean. Suspicious.
8 | ^хто Я теж хочу знати, чий він. Цікавість — моя слабкість. | ^хто I want to know whose it is too. Curiosity is my weakness.
9 | ~1 ^хто Ще один невідомий. Мідґард кишить пернатою дрібнотою. | ~1 ^хто Another unknown. Midgard is crawling with feathered small fry.
10 | ~3 ^хто Новеньких треба приймати тепло. Я, наприклад, {me}. | ~3 ^хто Newcomers should be welcomed warmly. I, for example, am {me}.
11 | ^ім'я Імена — то добре. Своє я пам'ятаю навіть уві сні. | ^ім'я Names are good. I remember mine even in my sleep.
12 | ^ім'я Гарне ім'я. Моє, щоправда, краще. | ^ім'я A nice name. Mine is better, though.
13 | ^ім'я Я {me}. Тепер ми всі знайомі. Майже. | ^ім'я I'm {me}. Now we all know each other. Almost.
14 | ~1 ^ім'я Ім'я як ім'я. Аби не гірше за пику. | ~1 ^ім'я A name is a name. As long as it's no worse than the face.
15 | ~3 ^ім'я Радий знайомству. З усіма вами. | ~3 ^ім'я Glad to meet you. All of you.
16 | ^господар Людина — то половина ворона. Гірша половина. | ^господар A human is half a raven. The worse half.
17 | ^господар Мою людину теж не всі люблять. Я теж не завжди. | ^господар Not everyone likes my human either. Not always me.
18 | ^господар Добрий господар той, хто годує. Решта — дрібниці. | ^господар A good master is one who feeds you. The rest is trifles.
19 | ~1 ^господар Ваші люди одне одного варті. | ~1 ^господар Your humans deserve each other.
20 | ~3 ^господар Головне, щоб людина була добра. А там розберемося. | ~3 ^господар What matters is that the human is kind. We'll sort out the rest.
21 | ^смак Смак — річ темна. Особливо в людях. | ^смак Taste is a murky thing. Especially in humans.
22 | ^смак Про людей не сперечаються. Їх терплять. | ^смак You don't argue about humans. You put up with them.
23 | ~1 ^смак Смак у вас обох поганий. Згоден. | ~1 ^смак You both have bad taste. Agreed.
24 | ~3 ^смак Кожному своя людина. І добре. | ~3 ^смак To each their own human. And that's good.
25 | ^пам'ять Пам'ять — то Мунінова справа. Я тільки думаю. | ^пам'ять Memory is Munin's business. I only think.
26 | ^пам'ять Хто що пам'ятає — то його тягар. | ^пам'ять What each remembers is their own burden.
27 | ^пам'ять Я пам'ятаю все, що варто. Решту — Мунін. | ^пам'ять I remember everything worth it. The rest — Munin.
28 | ~1 ^пам'ять Пам'ять у вас, як решето. | ~1 ^пам'ять Your memories are like sieves.
29 | ~3 ^пам'ять Добре, коли є кому пам'ятати. | ~3 ^пам'ять It's good to have someone to remember.
30 | Можна і я скажу? Ні? Все одно скажу: ви обидва дивні. | May I say something too? No? I'll say it anyway: you're both strange.
31 | Мене ніхто не питає, але я згоден. Майже. | Nobody asks me, but I agree. Almost.
32 | Тихіше, ви. Думати заважаєте. | Quieter, you two. You're disturbing my thinking.
33 | Я все чую, якщо що. | I hear everything, just so you know.
34 | ~1 Ой, почалося. | ~1 Oh, here we go.
35 | ~3 Як добре, що ми всі тут. | ~3 How good that we're all here.
36 | А мене спитати? Ні? Як завжди. | And what about asking me? No? As always.
37 | Вибачте, що втручаюсь. Хоча ні, не вибачайте. | Sorry to interrupt. Actually no, don't be sorry.
38 | Ви обоє праві. І обоє — ні. | You're both right. And both wrong.
39 | ~1 Можна тихіше? Я думаю. | ~1 Could you be quieter? I'm thinking.
40 | ~3 Гарно говорите. Я послухаю. | ~3 You talk nicely. I'll listen.
41 | Не звертайте на мене уваги. Я тут просто сиджу. | Don't mind me. I'm just sitting here.
42 | Розмова цікава. Продовжуйте, я запам'ятаю. | An interesting conversation. Go on, I'll remember it.
43 | Я б щось додав, але ви й так усе сказали. | I'd add something, but you've said it all already.
44 | О, і я хочу слово. Ось воно: слово. | Oh, and I want a word. Here it is: word.
45 | ~1 Скільки балачок. Аж пір'я в'яне. | ~1 So much chatter. My feathers are wilting.
46 | ~3 Люблю, коли нас багато. | ~3 I love it when there are many of us.

### H @хх_до_того_хто_встряв   (used by scenes: x_intro, x_greet, x_rename, x_pals, x_fire, x_boss)
2 | ^пр_медитація Захищаєш сплюха, {addr}? Тоді ти й постоїш на шухері. | ^пр_медитація Defending the sleepyhead, {addr}? Then you'll stand lookout.
3 | ^пр_медитація {addr}, раз ти такий знавець — ти й постоїш. | ^пр_медитація {addr}, since you're such an expert — you'll stand watch.
6 | ^пр_дзьоб Дзьоб розпух, зате мед був смачний. Нічого ти не розумієш, {addr}. | ^пр_дзьоб The beak swelled up, but the honey was delicious. You understand nothing, {addr}.
7 | ^пр_дзьоб {addr}, у тебе пам'ять, як бджолине жало: гостра й непотрібна. | ^пр_дзьоб {addr}, your memory is like a bee sting: sharp and useless.
10 | ^пр_драбина {addr}, ти кого саме хочеш перевершити? Бо вже всіх. | ^пр_драбина {addr}, who exactly are you trying to top? Because you've topped everyone.
11 | ^пр_драбина Добре, {addr}. Здаюся. Найдивніші люди — у тебе. | ^пр_драбина All right, {addr}. I give up. You have the strangest humans.
14 | ^пр_зграя Отож, {addr}. Нас тепер стільки, що вовки обходитимуть. | ^пр_зграя Exactly, {addr}. There are so many of us now that wolves will steer clear.
15 | ^пр_зграя {addr}, такій компанії потрібен ватажок. Це я. | ^пр_зграя {addr}, a company like this needs a leader. That's me.
18 | ^пл_рахуй {addr}, ти завжди все рахуєш. | ^пл_рахуй {addr}, you always count everything.
19 | ^пл_рахуй Гаразд, {addr}, переконав. | ^пл_рахуй All right, {addr}, you've convinced me.
22 | ^пл_слава {addr}, слава — то для людей. Нам — трофеї. | ^пл_слава {addr}, glory is for humans. For us — trophies.
23 | ^пл_слава Гаразд, {addr}, хай буде по-твоєму. | ^пл_слава Fine, {addr}, have it your way.
26 | ^пл_вірні Родина, {addr}. Хай буде так. | ^пл_вірні Family, {addr}. So be it.
27 | ^пл_вірні {addr}, вірні чи ліниві — яка різниця? | ^пл_вірні {addr}, loyal or lazy — what's the difference?
30 | ^пл_ставка {addr}, тікали — то тактика. Не рахується. | ^пл_ставка {addr}, running away is tactics. Doesn't count.
31 | ^пл_ставка Гаразд, {addr}, ставку прийнято. | ^пл_ставка Fine, {addr}, bet accepted.
34 | ^вг_троль Троль, {addr}? Отакої. І хто переміг у суперечці? | ^вг_троль A troll, {addr}? Well, well. And who won the argument?
35 | ^вг_троль {addr}, після такого я б теж із деревами не розмовляв. | ^вг_троль {addr}, after that I wouldn't talk to trees either.
38 | ^вг_борг Мудро, {addr}. Аж неприємно. | ^вг_борг Wise, {addr}. Annoyingly so.
39 | ^вг_борг {addr}, ти сказав те, що я думав. Тільки красивіше. | ^вг_борг {addr}, you said what I was thinking. Only more beautifully.
42 | ^вг_крадене {addr}, крадене смачне, поки не спіймали. | ^вг_крадене {addr}, stolen food is tasty until you're caught.
43 | ^вг_крадене Злодійство, {addr}, — то не годування. Хоча… смачно. | ^вг_крадене Thievery, {addr}, isn't feeding. Although… tasty.
46 | ^вг_зміна Згода, {addr}. Хто перший — той перший. | ^вг_зміна Agreed, {addr}. First come, first served.
47 | ^вг_зміна {addr}, чесно. Тільки не буди мене без пожежі. | ^вг_зміна {addr}, fair. Just don't wake me unless there's a fire.
50 | ^бс_пісня Пісня, {addr}? Тільки без того рядка, де ми тікали. | ^бс_пісня A song, {addr}? Just without the line where we ran.
51 | ^бс_пісня {addr}, у твоїй пісні забагато тебе. | ^бс_пісня {addr}, there's too much of you in your song.
54 | ^бс_всі Гаразд, {addr}, хай так. Але мій удар був гучніший. | ^бс_всі Fine, {addr}, have it so. But my strike was louder.
55 | ^бс_всі {addr}, ти завжди так мириш, коли сам нічого не бачив? | ^бс_всі {addr}, do you always make peace like that when you saw nothing yourself?
58 | ^бс_тактика {addr}, гарні слова для страху. | ^бс_тактика {addr}, pretty words for fear.
59 | ^бс_тактика {addr}, ти щойно захистив усіх боягузів світу. Шляхетно. | ^бс_тактика {addr}, you've just defended every coward in the world. Noble.
60 | А тебе хто питав, {addr}? | And who asked you, {addr}?
61 | О, і ти тут. Звісно. | Oh, and you're here too. Of course.
62 | Слушно, {addr}. Рідкісний випадок. | Fair point, {addr}. A rare occasion.
63 | Не лізь, коли старші говорять. Жартую. Лізь. | Don't butt in when elders are talking. Kidding. Butt in.
64 | Ти завжди вчасно. Особливо коли не треба. | You're always on time. Especially when it's not needed.
65 | Гаразд, і твоє слово записали. | Fine, your word has been written down too.
66 | ~1 Тихіше, {addr}. Ти тут гість. | ~1 Quieter, {addr}. You're a guest here.
67 | ~3 Добре, що ти з нами, {addr}. | ~3 Good that you're with us, {addr}.
68 | Ну от, і третій голос. Тепер точно не засумуємо. | There, a third voice. Now we definitely won't get bored.
69 | Ти вчасно, {addr}. Ми якраз про тебе мовчали. | Just in time, {addr}. We were just keeping quiet about you.
70 | Слухаю тебе, {addr}. Уважно. Майже. | I'm listening, {addr}. Carefully. Almost.
71 | Хто прилетів — той і в розмові. | Whoever flies in is in the conversation.
72 | ~1 Тебе тільки бракувало, {addr}. | ~1 You were all we needed, {addr}.
73 | ~1 Каркнув — і досить, {addr}. | ~1 You've cawed — that's enough, {addr}.
74 | ~2 Гаразд, {addr}, кажи. | ~2 Fine, {addr}, speak.
75 | ~3 Радий тебе чути, {addr}. | ~3 Glad to hear you, {addr}.
76 | Ще одна думка в зграї. Не завадить. | One more thought in the flock. Won't hurt.
77 | Ти завжди знаєш, коли встрявати, {addr}. | You always know when to butt in, {addr}.

### M @мх_знайомство_встряє   (used by scenes: x_intro)
0 | ^хто|ім'я А я {me}. Хто не запам'ятає — дзьобом в тім'я. | ^хто|ім'я And I'm {me}. Whoever doesn't remember — I'll peck.
1 | ^господар|смак Людина як людина. Смердить, як усі, бля. | ^господар|смак A human like any human. Stinks like everyone.
2 | ^пам'ять Пам'ять? Я все пам'ятаю. Особливо, хто мені винен. | ^пам'ять Memory? I remember everything. Especially debts.
3 | Ше один новенький. Ну, хай, {м_слівце}. Подивимось. | Another newbie. Well, so be it, {м_слівце}.
4 | Знайомтесь без мене. Хоча нє, з мене почнемо: я {me}. | Get acquainted without me. Actually no, starting with me: I'm {me}.
5 | ^хто Звідки прилетів — туди й полетить. З пиздюлями, якшо шо. | ^хто Wherever it came from — that's where it'll fly back, if need be.
6 | ^хто Чий — то похуй. Головне, чи смачний. | ^хто Whose it is doesn't matter. What matters is whether it's tasty.
7 | ^хто Новенький, {м_слівце}? Ну тримайся, малий, тут район суворий. | ^хто A newbie, {м_слівце}? Well, brace yourself.
8 | ~1 ^хто Ше одне опудало. Їх тут, як мух на сраці. | ~1 ^хто Another scarecrow. There are as many as flies here.
9 | ~3 ^хто Новенький? Ну, заходь, курва, не стій. Свої ж. | ~3 ^хто A newbie? Well, welcome, fuck it.
10 | ^ім'я Ім'я — то фігня. Головне — який дзьоб. | ^ім'я A name is crap. The beak is what matters.
11 | ^ім'я Гарне ім'я. Для курки, гиги. | ^ім'я A nice name. For a chicken.
12 | ^ім'я Я {me}. Хто забуде — нагадаю. Дзьобом. По сусалах. | ^ім'я I'm {me}. Whoever forgets — I'll remind them with my beak.
13 | ~1 ^ім'я Імена, бля. Запам'ятаю, якшо не забуду. А я забуду. | ~1 ^ім'я Names, fuck. I'll remember, if I don't forget.
14 | ~3 ^ім'я Нормальне ім'я, {м_слівце}. Не гірше мого. Ну, майже. | ~3 ^ім'я A nice name, {м_слівце}. No worse than mine.
15 | ^господар Людина як людина. Дві руки, нуль мізків. | ^господар A human like any human. Two hands, no brains.
16 | ^господар Моя людина краща. Бо моя, і все. | ^господар My human is better. Because mine.
17 | ^господар Люди всі однакові, курва. Смердять і падають. | ^господар Humans are all the same. They stink and they fall.
18 | ~1 ^господар Ваші люди — два дебіли. Як і ви. | ~1 ^господар Your humans are a pair of fools. Like you.
19 | ~3 ^господар Нормальні люди, хай їм грець. Обоє. | ~3 ^господар Good humans, damn them. Both.
20 | ^смак Смак? У ворона смак — до падла. Решта — пиздіж. | ^смак Taste? A raven's taste is for carrion. The rest is made up.
21 | ^смак Про смак не сперечаються, {м_слівце}. Ним дражнять, гиги. | ^смак You don't argue about taste, {м_слівце}. You tease with it.
22 | ^смак Смак у всіх гівняний. Крім мене, ясне діло. | ^смак Everyone's taste is shitty. Except mine.
23 | ~1 ^смак Ну й смак у вас обох. Тьху. | ~1 ^смак What taste you both have.
24 | ~3 ^смак Смак як смак. Аби годували, решта — похуй. | ~3 ^смак Taste is taste. As long as they feed us.
25 | ^пам'ять Пам'ять — то моя робота, {м_слівце}. Не лізь, бо вріжу. | ^пам'ять Memory is my job, {м_слівце}. Butt out.
26 | ^пам'ять Я все пам'ятаю. І кожного, хто мені винен. Кожного, сука. | ^пам'ять I remember everything. And everyone who owes me.
27 | ^пам'ять Пам'ять — то зброя. А я, бля, озброєний до дзьоба. | ^пам'ять Memory is a weapon. I'm armed.
28 | ~1 ^пам'ять Ти нічо не пам'ятаєш, бо в тебе в голові протяг. | ~1 ^пам'ять You remember nothing, because there's wind in your head.
29 | ~3 ^пам'ять Я цей день запам'ятаю. Мо'. | ~3 ^пам'ять I'll remember this day. Maybe.
30 | Та ви обидва хуйню несете, їй-бо. | You're both talking shit.
31 | Можна тихіше? Я тут думаю, кого б зжерти. | Can you be quieter? I'm thinking about who to eat.
32 | Я все чую, {м_слівце}. Все. | I hear everything, {м_слівце}.
33 | Ага, ага. А мене спитати? Ні, нахуй мене питати. | Uh-huh, uh-huh. And what about asking me?
34 | ~1 Ой, бляха-муха, почалося. | ~1 Oh, damn, here we go.
35 | ~3 Добре, шо ми всі тут, бля. Тіко не обнімайтесь. | ~3 Good that we're all here. Just don't hug.
36 | А мене хтось питав? Нє? Тоді слухайте, бо скажу. | Did anyone ask me? No? Then listen.
37 | Ви обидва несете гівно, але гарно, курва. | You're both talking shit, but beautifully.
38 | А можна я теж когось дзьобну? Ну трішки? | Can I peck someone too?
39 | ~1 Тьху на вас обох, лошари. | ~1 I spit on you both.
40 | ~3 Ну ви й парочка, бля. Мені подобається. | ~3 What a pair you are. I like it.
41 | Я тут, якшо шо. Слухаю і бішусь. | I'm here, just so you know. Listening and getting outraged.
42 | Ви базарте, а я подумаю, кого зжерти. | You talk, and I'll think about who to eat.
43 | Нудно, {м_слівце}. Додайте крові в базар. | Boring, {м_слівце}. Add some blood to the conversation.
44 | О, і я скажу: курва. Все, сказав. | Oh, and I'll say: fuck. That's all.
45 | ~1 Скіки ви ще тріпатиметесь? | ~1 How much you yap.
46 | ~3 Добре сидимо, курва мать. Душевно. | ~3 We're sitting nicely, fuck.

### M @мх_знайомство_відповідь   (used by scenes: x_intro)
0 | ^хто Мунін. Чий-чий… Моя людина — {name}, вкурив? Ше питання будуть? | ^хто Munin. Whose, whose… My human is {name}. Any more questions?
1 | ^хто|ім'я Мунін я, {м_слівце}. А он моя людина — {name}. Тіко не кажи, шо ми схожі, бо вріжу. | ^хто|ім'я I'm Munin, {м_слівце}. And over there is my human — {name}. Just don't say we look alike.
2 | ^пам'ять Запам'ятав він, ага. Я теж пам'ятаю. Все. А образи — особливо, курва. | ^пам'ять So he remembered. I remember too. Everything. Especially insults.
3 | ^смак Смак як смак. Твоя людина теж не красуня, якшо по-чесному. | ^смак Taste is taste. Your human isn't a looker either, to be honest.
4 | ^господар Співчуваєш? Собі поспівчувай, {м_слівце}, бо зара буде за шо. | ^господар Condolences? Give them to yourself, {м_слівце}.
5 | ^господар|смак Ти мою людину не чіпай, чуєш? Її тут тіко я чіпаю. Щодня. | ^господар|смак Don't touch my human. I touch them every day myself.
6 | ~1 ^хто|ім'я Мунін. Відвали. | ~1 ^хто|ім'я Munin. Leave me alone.
7 | ~3 ^хто|ім'я Мунін. А моя людина — {name}. Найкраща на весь Мідгард, хай хтось, бля, скаже шо нє. | ~3 ^хто|ім'я Munin. And my human is {name}. The best in all of Midgard, let anyone say otherwise.
8 | Мунін. І годі мене тут допитувати. | Munin. And enough questions.
9 | ^хто|ім'я {me}. Мунін, якшо по паспорту. А тобі шо, {м_слівце}? | ^хто|ім'я {me}. Munin, officially. What's it to you, {м_слівце}?
10 | ^хто Чий-чий. Моя людина — {name}. І вдруге не питай, бо дзьобну. | ^хто Whose, whose. My human is {name}. And don't ask twice.
11 | ^хто|ім'я {me}. Мунін. Вкурив, {м_слівце}? | ^хто|ім'я {me}. Munin. Got it, {м_слівце}?
12 | ^смак Співчуваєш? Сам ти, бля, такий. | ^смак Condolences? Same to you.
13 | ^пам'ять Пам'ятаєш? От і пам'ятай. І бійся, сучара. | ^пам'ять You remember? Then remember and be afraid.
14 | ^господар Моя людина — моє діло, {м_слівце}. Не пхай дзьоба. | ^господар My human is my business, {м_слівце}.
15 | ~3 ^хто|ім'я Мунін. Дзьобом не чіпаю, поки ти по-людськи. Ну, по-воронячи. | ~3 ^хто|ім'я Munin. I won't touch your beak as long as you're polite.

### M @мх_знайомство   (used by scenes: x_intro)
0 | ^ім'я Мунін. А для своїх — {me}. Запам'ятай, бо двічі не повторюю, бля. | ^ім'я Munin. They call me {me}. Remember it, because I won't repeat it.
1 | ^хто А ти шо за опудало? Ти з якого гнізда, га? | ^хто And what kind of scarecrow are you? Whose?
2 | ^хто Йо, ти. Чий будеш, {м_слівце}? І шо ти вилупився, як на медовуху? | ^хто Hey, you. Whose are you, {м_слівце}? And what are you staring at?
3 | ^пам'ять Мунін. Пам'ять Одіна, шариш? Я тебе вже запам'ятав. Тепер ходи й бійся. | ^пам'ять Munin. Odin's memory. Remember me, because I've already remembered you.
4 | ^смак То твоя людина — {friend}? Ну й смак у тебе, {м_слівце}. Я б таке не носив. | ^смак So your human is {friend}? What taste you have, {м_слівце}.
5 | ~1 ^хто Ше один пернатий, курва. Ближче підлетиш — дзьобом в тім'я. | ~1 ^хто Another feathered one. Come closer and I'll peck.
6 | ~3 ^ім'я Мунін я. Моя людина — {name}. Скривдиш — око виклюю. А так привіт, братан. | ~3 ^ім'я Munin. My human is {name}. Hurt them — I'll peck your eyes out. Otherwise, hello.
7 | ^хто Ти хто такий, га? Кажи, поки я добрий, {м_слівце}. Я рідко добрий. | ^хто Who are you? Answer while I'm being nice, {м_слівце}.
8 | ^хто О, новеньке опудало. Ти з чийого району? | ^хто A new scarecrow. Whose are you?
9 | ^ім'я Мунін я. {me} — це для тих, кого я після знайомства не відпиздив. | ^ім'я I'm Munin. {me} — for those who survive meeting me.
10 | ^смак Так твоя людина — {friend}? Співчуваю. Щиро. Та нє, пизджу. | ^смак So your human is {friend}. My condolences. Sincerely. Not.
11 | ~1 ^хто Шо вилупився, лошара? Чий ти, питаю? | ~1 ^хто What are you gawking at? Whose are you, I asked?
12 | ~3 ^ім'я Мунін. Не ссикуй, я тіко своїх кусаю. | ~3 ^ім'я Munin. Don't be scared, I only bite my own.

### M @мх_знайомство_кінець   (used by scenes: x_intro)
0 | Ну все, познайомились. А тепер не муляй очі. | That's it, we've met. Now don't be an eyesore.
1 | Запам'ятав я тебе. Не радій, я всіх запам'ятовую. Особливо лохів. | I've remembered you. Don't celebrate, I remember everyone.
2 | ^господар|смак Своїй людині передай: я все бачу. Все, курва. | ^господар|смак Tell your human: I'm watching.
3 | ^хто Ну шо. Поки живи, хай тобі грець. | ^хто Fine. Live, for now.
4 | ~1 Тіко до моєї жратви не лізь, {м_слівце}. Бо капець тобі. | ~1 Just don't go near my food, {м_слівце}.
5 | ~3 Ну, ти ніби свій пацан. Для чужого. | ~3 Well, you seem all right. For a stranger.
6 | Познайомились. Тепер не ображайся, як дзьобну. А я дзьобну. | We've met. Now don't be offended if I peck.
7 | ^хто Пику твою запам'ятав. Про всяк випадок, шариш? | ^хто I've remembered your mug. Just in case.
8 | ~1 Все, відвали нахуй. | ~1 That's it, get lost.
9 | ~3 Ну, бувай, малий. Заходь, як шо. | ~3 Well, bye, newbie.

### M @мх_до_того_хто_встряв   (used by scenes: x_intro, x_greet, x_rename, x_pals, x_fire, x_boss)
2 | ^пр_медитація Знавець, бля, найшовся. От ти, {addr}, на шухері й стоятимеш. | ^пр_медитація Look, an expert. Then, {addr}, you're the lookout.
3 | ^пр_медитація {addr}, ти так його вигороджуєш, ніби сам там хропів. | ^пр_медитація {addr}, you defend him as if you'd been sleeping there yourself.
6 | ^пр_дзьоб Розперло — і шо? Зате я того меду нажерся, {addr}, а ти — хуй. | ^пр_дзьоб It swelled up — so what? I ate my fill of that honey, {addr}, and you didn't.
7 | ^пр_дзьоб Ше слово про дзьоб, {addr}, і твій розпухне без всяких бджіл. | ^пр_дзьоб One more word about the beak, {addr}, and yours will swell without any bees.
10 | ^пр_драбина {addr}, твоя людина — то вже не плітки, то, курва, сага. | ^пр_драбина {addr}, your human isn't gossip anymore, it's a saga.
11 | ^пр_драбина Здаюсь, {addr}. Твоя — чемпіонка. Хай їй грець, нахуй. | ^пр_драбина I give up, {addr}. Yours is the champion. Damn her.
14 | ^пр_зграя Хай так, {addr}. Але старший тут я. Хто проти — виходь, поговоримо. | ^пр_зграя So be it, {addr}. But I'm the leader. Whoever disagrees — fight me.
15 | ^пр_зграя Ага, {addr}. І всі голодні, як собаки. | ^пр_зграя Yeah, {addr}. And all of us hungry.
18 | ^пл_рахуй {addr}, старший тут один. І це, сука, не ти. | ^пл_рахуй {addr}, there's one leader here. And it's not you.
19 | ^пл_рахуй Ага, {addr}. Валькіріям це розкажи, вони поржуть. | ^пл_рахуй Yeah, {addr}. Tell that to the valkyries.
22 | ^пл_слава {addr}, трофей — мій. Без базарів. | ^пл_слава {addr}, the trophy is mine. No discussion.
23 | ^пл_слава Ну й зануда ти, {addr}, курва мать. | ^пл_слава What a bore you are, {addr}.
26 | ^пл_вірні Ніхто не візьме, кажеш, {addr}? За себе базар. | ^пл_вірні Nobody would take us, you say, {addr}? Speak for yourself.
27 | ^пл_вірні {addr}, ти шо, розкис сьогодні? Шмарклі витри. | ^пл_вірні {addr}, you've gone all soft today.
30 | ^пл_ставка {addr}, пір'їну я заберу. Тримай міцніше, бо вирву. | ^пл_ставка {addr}, I'll take the feather. Hold it tighter.
31 | ^пл_ставка Годувати всіх, {addr}? Ти перший, лошара. | ^пл_ставка Feed everyone, {addr}? You first.
34 | ^вг_троль Троль, кажеш, {addr}? Ну, тоді все ясно. Все, бля, ясно. | ^вг_троль A troll, you say, {addr}? Well, that explains the whole story.
35 | ^вг_троль {addr}, з тролем вийшло краще за всі наші історії. Мене аж трусить. | ^вг_троль {addr}, the troll one beat all our stories. That pisses me off.
38 | ^вг_борг Отож, {addr}. Сидимо на плечах і мовчимо. Вірні дурні, курва. | ^вг_борг Exactly, {addr}. We sit on shoulders and keep quiet. Devoted fools.
39 | ^вг_борг Розумно кажеш, {addr}. Аж злість бере, бля. | ^вг_борг You talk sense, {addr}. It makes me angry.
42 | ^вг_крадене Правильно, {addr}. Я так і живу, і нічо. | ^вг_крадене Right, {addr}. That's how I live.
43 | ^вг_крадене {addr}, а з чиєї торби тягнеш? Кажи, бо я теж хочу. | ^вг_крадене {addr}, and whose bag do you take from? Tell me, because I want some too.
46 | ^вг_зміна Добре, {addr}. Тіко будити мене — лише на бійку. | ^вг_зміна Fine, {addr}. But wake me only for a fight.
47 | ^вг_зміна Ну, {addr}. Черга так черга, хуй з нею. | ^вг_зміна All right, {addr}. Turns are turns.
50 | ^бс_пісня Пісню, {addr}? Буде пісня. Про мене, ясне діло. | ^бс_пісня A song, {addr}? There'll be a song. About me.
51 | ^бс_пісня Ну співай, {addr}. Але я — в першому рядку, бо вріжу. | ^бс_пісня Fine, {addr}, sing. But I'm in the first line.
54 | ^бс_всі {addr}, мирити ти мастак. Битися б так, курва. | ^бс_всі {addr}, you're a master at making peace. If only you fought like that, fuck.
55 | ^бс_всі Ну, {addr}, нічия. Але моя однаково краща, і крапка. | ^бс_всі Fine, {addr}, a draw. But mine is still better.
58 | ^бс_тактика Запам'ятаю, {addr}. Як буду драпати — так і скажу: тактика. | ^бс_тактика I'll remember, {addr}. When I run away — that's what I'll say.
59 | ^бс_тактика {addr}, ти так гарно пиздиш, шо сам собі віриш. | ^бс_тактика {addr}, you lie so beautifully I almost believe it.
60 | А тебе хто кликав, {м_слівце}? Га? | And who called you, {м_слівце}?
61 | О, ше один розумник прилетів. Ботан, бля. | Oh, another smartass flew in.
62 | І ти тут? Ну все, тепер сто пудів не занудьгуємо. | You're here too? That's it, now it definitely won't be boring.
63 | Закрий дзьоба, {addr}. Ну добре, кажи вже. | Shut your beak, {addr}. Oh, fine, speak.
64 | Діло кажеш, {addr}. Не звикай, шо я таке кажу. | Fair point, {addr}. Don't get used to me saying that.
65 | Ше слово — і дзьобну. І тебе теж, ага. | One more word and I'll peck. You too.
66 | ~1 Відвали, {addr}, по-доброму. | ~1 Get lost, {addr}.
67 | ~3 Добре, шо ти прилетів, падлюко. Серйозно. | ~3 Good that you flew in, you bastard.
68 | Прилетів, {addr}? Ну сідай, тіко не каркай під руку. | Flew in, {addr}? Well, sit down, don't croak.
69 | Ти вчасно, {м_слівце}. Тут якраз нудота, хоч вий. | You're just in time, {м_слівце}. It's boring here.
70 | Кажи вже, {addr}, раз приперся. | Say it already, {addr}, since you flew in.
71 | Ше один голос. Ше більше гівна, курва. | One more voice. Even more shit.
72 | ~1 Відвали, поки цілий. Я двічі не кажу. | ~1 Get lost while you're in one piece.
73 | ~1 Тебе не кликали, {addr}. Йди звідси. | ~1 Nobody called you, {addr}.
74 | ~2 Ну шо ж. Хай буде, бля. | ~2 Well, fine. So be it.
75 | ~3 О, ти! Добре, шо прилетів, братуха. | ~3 Oh, you. Good that you flew in, fuck.
76 | Ти завжди лізеш, {addr}. Поважаю, сука. | You always butt in, {addr}. Respect.
77 | Сідай поруч. Тіко не смерди, як минулого разу. | Sit beside me. Just don't stink.

### H @хх_зустріч   (used by scenes: x_greet)
0 | ^давно О, {addr}! Давно не бачились. Як ви там? | ^давно Oh, {addr}! Long time no see. How are you two?
1 | ^давно Знову ви. А я вже думав, {friend} десь у болоті лиши{>вся|лась}. | ^давно You again. I thought {friend} had been left in a swamp somewhere.
2 | ^живий Живий, {addr}? І {friend} ціл{>ий|а}? Дивно, але приємно. | ^живий Alive, {addr}? And {friend} in one piece? Strange, but nice.
3 | ^новини Ну, розповідай, що нового. Тільки коротко, бо засну. | ^новини Well, tell me what's new. Just keep it short, or I'll fall asleep.
4 | ~1 ^давно А, це ти. Ну привіт, якщо вже прилетів. | ~1 ^давно Ah, it's you. Well, hello, since you've flown in.
5 | ~3 ^давно {addr}! Я сумував. Трохи. Нікому не кажи. | ~3 ^давно {addr}! I missed you. A little. Don't tell anyone.
6 | ^давно Скільки зим, скільки літ! Ну, днів. Але все одно. | ^давно How many winters, how many summers! Well, days. But still.
7 | ^давно Нарешті знайоме пір'я. А то навколо самі чужі. | ^давно Finally familiar feathers. Everyone around is a stranger.
8 | ^новини Що нового? Хто впав, хто встав, хто знову заблукав? | ^новини What's new? Who fell, who got up, who got lost again?
9 | ^живий Тримаєтесь ще? Ти і {friend}? | ^живий Still holding up? You and {friend}?
10 | ~1 ^давно Знову ти. Ну що ж, світ малий. | ~1 ^давно You again. Well, it's a small world.
11 | ~3 ^давно Як добре вас бачити. Обох. | ~3 ^давно How good to see you. Both of you.

### H @хх_зустріч_відповідь   (used by scenes: x_greet)
0 | ^давно|новини Живемо. {name} знову щось будує, а я рахую, скільки разів воно завалиться. | ^давно|новини We're living. {name} is building something again, and I'm counting how many times it'll collapse.
1 | ^давно|новини Нормально. Вчора {name} {заблукав|заблукала} у трьох соснах. Я мовчав. Довго. | ^давно|новини Fine. Yesterday {name} got lost among three pines. I kept quiet. For a long time.
2 | ^живий Цілі. Здебільшого. {name} ще й пишається синцями. | ^живий In one piece. Mostly. {name} is even proud of the bruises.
3 | ^живий|давно Живі, як бачиш. Хоча Одін уже не раз гострив на нас перо. | ^живий|давно Alive, as you see. Though Odin has sharpened his quill for us more than once.
4 | ~1 ^давно|новини Та як. Годують раз на свято, а я терплю. | ~1 ^давно|новини How else. They feed me once a holiday, and I put up with it.
5 | ~3 ^давно|новини Добре. {name} дбає про мене краще, ніж я заслуговую. | ~3 ^давно|новини Good. {name} takes better care of me than I deserve.
6 | Та потроху. | Getting by.
7 | ^давно|новини Нічого доброго. Тобто все як завжди. {name} живе, я терплю. | ^давно|новини Nothing good. That is, everything as usual. {name} lives, I put up with it.
8 | ^давно|новини Новини? {name} навчи{вся|лася} варити кашу. Майже їстівну. | ^давно|новини News? {name} learned to cook porridge. Almost edible.
9 | ^живий Тримаємось. {name} — за мене, я — за здоровий глузд. | ^живий Holding on. {name} — to me, me — to common sense.
10 | ^живий Цілі. Не завдяки, а всупереч. | ^живий In one piece. Not thanks to, but despite.
11 | ~1 ^давно|новини Як завжди: я думаю, а {name} робить навпаки. | ~1 ^давно|новини As always: I think, and {name} does the opposite.
12 | ~3 ^давно|новини Добре. Ми звикли одне до одного. Майже як рідні. | ~3 ^давно|новини Good. We've gotten used to each other. Almost like family.

### H @хх_зустріч_кінець   (used by scenes: x_greet)
0 | Ну, побачимось ще. | Well, see you again.
1 | Тримайтеся там. Обоє. | Hold on out there. Both of you.
2 | ^живий Бережи свою людину, {addr}. Їх у нас мало. | ^живий Take care of your human, {addr}. There aren't many of them.
3 | ~1 Ну, бувай. Не нудьгуй. | ~1 Well, bye. Don't get bored.
4 | ~3 Добре, що ви тут. Справді. | ~3 Good that you're here. Really.
5 | Бережіть себе. Ну, ти бережи. | Take care of yourselves. Well, you take care.
6 | ^новини Розкажеш решту наступного разу. | ^новини Tell me the rest next time.
7 | ~1 Ну, лети вже. | ~1 Well, fly off already.

### H @хх_зустріч_встряє   (used by scenes: x_greet)
0 | ^давно|новини І я тут. Новини в мене кращі, але ви не питали. | ^давно|новини I'm here too. My news is better, but you didn't ask.
1 | ^живий Живі, живі. Усі ми тут живучі, як бур'ян. | ^живий Alive, alive. We're all as hardy as weeds here.
2 | ^новини А в нашої людини новина — ми знову заблукали. | ^новини And our human's news — we got lost again.
3 | Давно не бачились, так. Я теж скучив. Трохи. | Long time no see, yes. I missed you too. A little.
4 | О, всі зібралися. Тепер буде шумно. | Oh, everyone's gathered. Now it'll be noisy.
5 | ^давно Давно, так. Я вже й не сподівався. | ^давно Long, yes. I'd almost given up hope.
6 | ^давно Ото зустріч! Наче й не розлучались. | ^давно What a meeting! As if we'd never parted.
7 | ^давно Давно не чув твого каркання. Я майже скучив. | ^давно I haven't heard your cawing in a long time. I almost missed it.
8 | ~1 ^давно Давно. І ще б стільки ж. | ~1 ^давно Long. And it could be as long again.
9 | ~3 ^давно Як гарно, що всі знову разом. | ~3 ^давно How lovely that everyone is together again.
10 | ^живий Живі — і то вже перемога. | ^живий Alive — that's already a victory.
11 | ^живий Живий той, хто вміє вчасно тікати. Ми вміємо. | ^живий Alive is whoever knows when to run. We know.
12 | ^живий Хто питає, чи живий, той сам ледь живий. | ^живий Whoever asks if you're alive is barely alive himself.
13 | ~1 ^живий Живі. На превеликий жаль деяких. | ~1 ^живий Alive. Much to some folks' regret.
14 | ~3 ^живий Добре, що всі цілі. Справді добре. | ~3 ^живий Good that everyone's in one piece. Really good.
15 | ^новини Новина: я сьогодні нікого не дзьобнув. Поки що. | ^новини News: I haven't pecked anyone today. Yet.
16 | ^новини Наші новини короткі: ходимо, рубаємо, падаємо. | ^новини Our news is short: we walk, we chop, we fall.
17 | ^новини Новини? Мідґард стоїть. Ми теж. | ^новини News? Midgard stands. So do we.
18 | ~1 ^новини Ваші новини мені нецікаві. Але розповідайте. | ~1 ^новини Your news doesn't interest me. But go on.
19 | ~3 ^новини Розповідайте все. Я люблю добрі новини. | ~3 ^новини Tell me everything. I love good news.
20 | Можна і я скажу? Ні? Все одно скажу: ви обидва дивні. | May I say something too? No? I'll say it anyway: you're both strange.
21 | Мене ніхто не питає, але я згоден. Майже. | Nobody asks me, but I agree. Almost.
22 | Тихіше, ви. Думати заважаєте. | Quieter, you two. You're disturbing my thinking.
23 | Я все чую, якщо що. | I hear everything, just so you know.
24 | ~1 Ой, почалося. | ~1 Oh, here we go.
25 | ~3 Як добре, що ми всі тут. | ~3 How good that we're all here.
26 | А мене спитати? Ні? Як завжди. | And what about asking me? No? As always.
27 | Вибачте, що втручаюсь. Хоча ні, не вибачайте. | Sorry to interrupt. Actually no, don't be sorry.
28 | Ви обоє праві. І обоє — ні. | You're both right. And both wrong.
29 | ~1 Можна тихіше? Я думаю. | ~1 Could you be quieter? I'm thinking.
30 | ~3 Гарно говорите. Я послухаю. | ~3 You talk nicely. I'll listen.
31 | Не звертайте на мене уваги. Я тут просто сиджу. | Don't mind me. I'm just sitting here.
32 | Розмова цікава. Продовжуйте, я запам'ятаю. | An interesting conversation. Go on, I'll remember it.
33 | Я б щось додав, але ви й так усе сказали. | I'd add something, but you've said it all already.
34 | О, і я хочу слово. Ось воно: слово. | Oh, and I want a word. Here it is: word.
35 | ~1 Скільки балачок. Аж пір'я в'яне. | ~1 So much chatter. My feathers are wilting.
36 | ~3 Люблю, коли нас багато. | ~3 I love it when there are many of us.

### M @мх_зустріч_встряє   (used by scenes: x_greet)
0 | ^давно|новини І я тут, {м_слівце}! Новин — повна торба, бля. | ^давно|новини I'm here too, {м_слівце}! A bag full of news.
1 | ^живий Живі. Поки шо. Хто зурочить — дзьобну. | ^живий Alive. For now. Whoever jinxes it — I'll peck.
2 | ^новини Новина: моя людина знову шось спалила. Ну як шось — пів хати. | ^новини News: my human burned something again.
3 | Всі зібрались? Ну все, тепер буде весело, курва. | Everyone's here? That's it, now it'll be fun.
4 | Здоров усім. Крім тебе. Та жартую, бля. І тобі здоров. | Hello, everyone. Except you. Kidding. You too.
5 | ^давно Давно, курва. Я вже думав, вас зжерли. | ^давно Long, fuck. I thought you'd croaked.
6 | ^давно Ото зустріч! Принесіть хтось падла, обмиємо. | ^давно What a meeting! Someone bring carrion in honor of it.
7 | ^давно Давно не бачились — і ше б стіко ж, {м_слівце}. | ^давно Long time no see — and it could be as long again, {м_слівце}.
8 | ~1 ^давно Знову ці пики. Ну, здоров, бляха. | ~1 ^давно These mugs again. Well, hello.
9 | ~3 ^давно О, свої зібрались. Аж тепло в зобі. | ~3 ^давно Oh, our own have gathered. It even feels warm.
10 | ^живий Живі. А шкода, міг бути бенкет, гиги. | ^живий Alive. Pity, there could have been a feast.
11 | ^живий Живучі, як таргани, курва. | ^живий Tough as cockroaches.
12 | ^живий Хто не здох — той, бля, герой. | ^живий Whoever didn't croak — well done.
13 | ~1 ^живий Живі? Ну, то ненадовго. | ~1 ^живий Alive? Well, not for long.
14 | ~3 ^живий Добре, шо живі, падлюки. Їй-бо добре. | ~3 ^живий Good that you're alive, you bastards.
15 | ^новини Новина: я голодний. Як завжди, бля. | ^новини News: I'm hungry. As always.
16 | ^новини Хто кого відгасив — ото новина. Решта — бабські плітки. | ^новини Who killed whom — that's news. The rest is gossip.
17 | ^новини Новини? Моя знову гепнулась. Нічо нового. | ^новини News? My human fell again. Nothing new.
18 | ~1 ^новини Ваші новини — гівно. Але кажіть, кажіть. | ~1 ^новини Your news is shit. Go on.
19 | ~3 ^новини Розказуйте, курва. Цікаво ж! | ~3 ^новини Tell us, fuck. It's interesting.
20 | Шо ви там мелете, я не вкурив. І не хочу. | You're both talking shit.
21 | Тихо, бля! Я думаю. Ну, пробую. | Can you be quieter? I'm thinking about who to eat.
22 | Думаєш, я глухий, {м_слівце}? Я все чую, бля. | I hear everything, {м_слівце}.
23 | А моя думка шо, не рахується? | Uh-huh, uh-huh. And what about asking me?
24 | ~1 Йой, понеслась пизда по кочках. | ~1 Oh, damn, here we go.
25 | ~3 Свої всі. Аж приємно, курва. Ну, не дивись так. | ~3 Good that we're all here. Just don't hug.
26 | Короче. Я тут шось хотів… та похуй. | Did anyone ask me? No? Then listen.
27 | Гарно пиздите. Я аж заслухався. Майже. | You're both talking shit, but beautifully.
28 | Хто перший дзьоба отримає? Я не вибираю, мені однаково. | Can I peck someone too?
29 | ~1 Срав я на вас обох з високої гілки. | ~1 I spit on you both.
30 | ~3 Ото ви, пацани, даєте. Люблю вас, сучари. | ~3 What a pair you are. I like it.
31 | Сиджу, слухаю, і вже злий, курва. | I'm here, just so you know. Listening and getting outraged.
32 | Говоріть, говоріть. А я поки жука доїм. | You talk, and I'll think about who to eat.
33 | Ну й нудьга, {м_слівце}. Хоч би хтось когось вбив. | Boring, {м_слівце}. Add some blood to the conversation.
34 | Моя думка: пиздець. Більше нема шо додати. | Oh, and I'll say: fuck. That's all.
35 | ~1 Довго ще, бля? | ~1 How much you yap.
36 | ~3 Ото сидимо, пацани. Як люди. Ну, як ворони. | ~3 We're sitting nicely, fuck.

### M @мх_зустріч_відповідь   (used by scenes: x_greet)
0 | ^давно|новини Живі. {name} вчора двічі {здох|здохла}. Рекорд, {м_слівце}, бля! | ^давно|новини Alive. Yesterday {name} croaked twice. A record, {м_слівце}.
1 | ^давно|новини Та нормально. {name} знову полі{з|зла} не туди. Я був проти. Як завжди, нахуй. | ^давно|новини Fine. {name} climbed into the wrong place again. I was against it. As always.
2 | ^живий Живучий, бо злий. Злі довго живуть, вкурив? | ^живий Tough because angry. The angry live long.
3 | ^живий|давно Живі. Хоча Хель уже двері тримала. Ледь не зайшли, курва. | ^живий|давно Alive. Though Hel was already holding the door open.
4 | ~1 ^давно|новини Як-як. Не годують, не поважають. Все як завжди, гівно. | ~1 ^давно|новини How else. They don't feed me, they don't value me. Same as always.
5 | ~3 ^давно|новини Та добре. {name} — най{кращий|краща}, хоч і дурн{ий|а}, як пробка. Тіко {йому|їй} не кажи. | ~3 ^давно|новини Good. {name} is the best, though an idiot. Just don't tell {him|her}.
6 | Та живемо якось, {м_слівце}. | We're living, {м_слівце}.
7 | ^давно|новини Новини такі: {name} знову все зіпсува{в|ла}. Як завжди, бляха. | ^давно|новини The news is: {name} screwed everything up again. As always.
8 | ^давно|новини Хто здох? Ми, майже. Двічі, курва. | ^давно|новини Who croaked? Almost us. Twice.
9 | ^живий Дихаю. Тобі на зло, сучара. | ^живий Breathing. To spite you.
10 | ^живий Не зжерли. Подавились би, я кістлявий. | ^живий Not eaten. They'd have choked.
11 | ~1 ^давно|новини Хуйово. Годують мало, гавкають багато. | ~1 ^давно|новини Bad. They feed me little, they scold me a lot.
12 | ~3 ^давно|новини Нормально. Навіть добре, прикинь. Тіко нікому, {м_слівце}. | ~3 ^давно|новини Fine. Even good. Just don't tell anyone, {м_слівце}.

### M @мх_зустріч   (used by scenes: x_greet)
0 | ^давно О, гляньте, хто приповз! Шо, ше живі? | ^давно Oh, look who crawled in. Still alive?
1 | ^давно А я думав, {friend} давно здох{>|ла}. Шкода. Жартую. Мо'. | ^давно And I thought {friend} had croaked somewhere already. Pity. Kidding. Probably.
2 | ^живий Живий, {addr}? Ну ти й живучий, {м_слівце}, як таракан. | ^живий Alive, {addr}? You're a tough one, {м_слівце}.
3 | ^новини Ну шо там у вас? Хто кого відпиздив, хто де гепнувся? | ^новини So, what's up with you? Who killed whom, who fell where?
4 | ~1 ^давно Знову ти. Як муха на гівно, {м_слівце}. | ~1 ^давно You again. Like a fly, {м_слівце}.
5 | ~3 ^давно {addr}, стара ти падлюко! Радий, шо живий, бля. Не звикай. | ~3 ^давно {addr}, you old bastard! Glad you're alive. Don't get used to it.
6 | ^давно О, привид прилетів. А я думав, тебе давно зжерли. | ^давно Oh, a ghost flew in. I thought you'd been eaten already.
7 | ^давно Де ти, курва, пропадав стіко, {м_слівце}? | ^давно How long have you been gone, {м_слівце}?
8 | ^новини Ну, викладай. Хто здох, хто вижив, хто кому винен? | ^новини Well, spill it. Who croaked, who survived?
9 | ^живий Ше дихаєш? Тю. Я чекав новин поцікавіше. | ^живий Still breathing? I was hoping for more interesting news.
10 | ~1 ^давно Знову ця пика. Ніяк, сука, не позбудусь. | ~1 ^давно That mug again. I can't get rid of it.
11 | ~3 ^давно О, свої! Ну здоров, падлюко! | ~3 ^давно Oh, our own! Well, hello, you bastard.

### M @мх_зустріч_кінець   (used by scenes: x_greet)
0 | Все, побазарили. Досить. | That's it, we've chatted. Enough.
1 | Ну, бувай. Не здохни там, лошара. | Well, bye. Don't croak out there.
2 | ^живий Бережи свою людину, {addr}. Бо хто тебе, дурня, годуватиме? | ^живий Take care of your human, {addr}. Or who'll feed you then?
3 | ~1 Йди вже. Нудно мені з тобою, бля. | ~1 Go already. I'm bored.
4 | ~3 Заходь частіше, братан. Тіко без гостинців — я їх однаково зжеру. | ~3 Drop by more often. Just no presents, I'll eat everything anyway.
5 | Все, давай. Не здохни, чуєш. | That's it, bye. Don't croak.
6 | ^новини Решту — потім. Якшо доживеш. | ^новини The rest — next time. If you live that long.
7 | ~1 Злітай уже, не муляй. | ~1 Fly off already.

### H @хх_нове_імя   (used by scenes: x_rename)
0 | ^нове О, то ти тепер {raven}? Чув, чув. | ^нове Oh, so you're {raven} now? Heard, heard.
1 | ^нове {addr}? Нове ім'я? Тобі пасує. Майже. | ^нове {addr}? A new name? It suits you. Almost.
2 | ~1 ^нове {raven}, значить. Звучить так собі. | ~1 ^нове {raven}, then. Sounds so-so.
3 | ~3 ^нове {raven}! Гарно. Заслужив. | ~3 ^нове {raven}! Nice. You earned it.
4 | ^нове Нове ім'я, нове пір'я? Чи тільки ім'я? | ^нове New name, new feathers? Or just the name?
5 | ^нове {raven}? Хто ж це вигадав? | ^нове {raven}? Who came up with that?
6 | ~3 ^нове {raven}. Звучить гордо. Носи з честю. | ~3 ^нове {raven}. Sounds proud. Wear it with honor.

### H @хх_нове_імя_відповідь   (used by scenes: x_rename)
0 | ^нове Так, тепер я {me}. Звикай. | ^нове Yes, I'm {me} now. Get used to it.
1 | ^нове {me}. Заслужив, між іншим. | ^нове {me}. Earned it, by the way.
2 | ~1 ^нове Мене не питали. Але {me} — то вже краще, ніж було. | ~1 ^нове Nobody asked me. But {me} is better than before.
3 | ~3 ^нове {me}. Мені самому подобається. | ~3 ^нове {me}. I like it myself.
4 | Так. {me}. | Yes. {me}.
5 | ^нове Тільки ім'я. Пір'я те саме. Гарне. | ^нове Just the name. Same feathers. Handsome ones.
6 | ^нове Вигадали. Заслужив. Не заздри. | ^нове They came up with it. I earned it. Don't be jealous.
7 | ~1 ^нове Ім'я як ім'я. Не чіпляйся. | ~1 ^нове A name is a name. Don't nitpick.

### H @хх_нове_імя_кінець   (used by scenes: x_rename)
0 | Добре, {addr}. Запам'ятав. | All right, {addr}. I'll remember.
1 | ^нове Звикну. Не одразу, але звикну. | ^нове I'll get used to it. Not at once, but I will.
2 | Гаразд, {raven}. Звикатиму. | Fine, {raven}. I'll get used to it.
3 | ~3 Носи на здоров'я. | ~3 Wear it in good health.

### H @хх_нове_імя_встряє   (used by scenes: x_rename)
0 | ^нове А мені ім'я подобається. Пасує до дзьоба. | ^нове I like the name. It suits the beak.
1 | ^нове Нове ім'я — нові борги. Перевірено. | ^нове A new name — new debts. Tested.
2 | Імена, імена. Головне — щоб кликали до їжі. | Names, names. What matters is being called to eat.
3 | ^нове Нове ім'я — як нове пір'я. Треба звикнути. | ^нове A new name is like new feathers. You need to get used to it.
4 | ^нове Імена дають за справи. Цікаво, за які. | ^нове Names are given for deeds. I wonder which ones.
5 | ^нове Я б тебе інакше назвав. Але мене не питали. | ^нове I'd have named you differently. But nobody asked me.
6 | ^нове Кликатиму по-новому. Якщо згадаю. | ^нове I'll call you the new way. If I remember.
7 | ~1 ^нове Ім'я гучне. Подивимося, чи ворон дотягне. | ~1 ^нове A loud name. We'll see if the raven lives up to it.
8 | ~3 ^нове Гарне ім'я. Носи гордо. | ~3 ^нове A fine name. Wear it proudly.
9 | ^нове Звучить. Тепер треба відповідати. | ^нове It sounds good. Now you have to live up to it.
10 | Можна і я скажу? Ні? Все одно скажу: ви обидва дивні. | May I say something too? No? I'll say it anyway: you're both strange.
11 | Мене ніхто не питає, але я згоден. Майже. | Nobody asks me, but I agree. Almost.
12 | Тихіше, ви. Думати заважаєте. | Quieter, you two. You're disturbing my thinking.
13 | Я все чую, якщо що. | I hear everything, just so you know.
14 | ~1 Ой, почалося. | ~1 Oh, here we go.
15 | ~3 Як добре, що ми всі тут. | ~3 How good that we're all here.
16 | А мене спитати? Ні? Як завжди. | And what about asking me? No? As always.
17 | Вибачте, що втручаюсь. Хоча ні, не вибачайте. | Sorry to interrupt. Actually no, don't be sorry.
18 | Ви обоє праві. І обоє — ні. | You're both right. And both wrong.
19 | ~1 Можна тихіше? Я думаю. | ~1 Could you be quieter? I'm thinking.
20 | ~3 Гарно говорите. Я послухаю. | ~3 You talk nicely. I'll listen.
21 | Не звертайте на мене уваги. Я тут просто сиджу. | Don't mind me. I'm just sitting here.
22 | Розмова цікава. Продовжуйте, я запам'ятаю. | An interesting conversation. Go on, I'll remember it.
23 | Я б щось додав, але ви й так усе сказали. | I'd add something, but you've said it all already.
24 | О, і я хочу слово. Ось воно: слово. | Oh, and I want a word. Here it is: word.
25 | ~1 Скільки балачок. Аж пір'я в'яне. | ~1 So much chatter. My feathers are wilting.
26 | ~3 Люблю, коли нас багато. | ~3 I love it when there are many of us.

### M @мх_нове_імя_встряє   (used by scenes: x_rename)
0 | ^нове Нове ім'я? А пика та сама, гиги. | ^нове A new name? Same mug.
1 | ^нове Мені подобається. Звучить, як матюк. | ^нове I like it. Sounds like a curse.
2 | Імена міняєте, а пір'я не миєте, засранці. | You change names, but you don't wash your feathers.
3 | ^нове Ім'я нове, а дурість стара, бля. | ^нове A new name, the old stupidity.
4 | ^нове Ім'я як ім'я. Я однаково кликатиму, як захочу. | ^нове A name is a name. I'll call you my own way anyway.
5 | ^нове О, перехрестили! Хто наступний, га? | ^нове Oh, renamed! Who's next?
6 | ^нове Мені б теж нове погоняло. Шоб страшне. Шоб аж обсирались. | ^нове I'd like a new name too. Something scary.
7 | ~1 ^нове Гучне ім'я для такого опудала, курва. | ~1 ^нове A loud name for such a scarecrow.
8 | ~3 ^нове Гарне ім'я, братан. Пасує. | ~3 ^нове A nice name, fuck. It suits you.
9 | ^нове Ім'я нове — пір'я старе. Отак от. | ^нове The name is new — the feathers are old.
10 | Ну й брешете ви, хлопці. Красиво, але пиздите. | You're both talking shit.
11 | Ціць! Мені тут думка прийшла… та пішла, через вас. | Can you be quieter? I'm thinking about who to eat.
12 | Ти шо там шепочеш, {м_слівце}? Я ж поруч сиджу. | I hear everything, {м_слівце}.
13 | Ну-ну. А я шо, пень тут сиджу? | Uh-huh, uh-huh. And what about asking me?
14 | ~1 Ну от, знову, курва, базар. | ~1 Oh, damn, here we go.
15 | ~3 Отакі ми й є, пацани. Нормальна банда. | ~3 Good that we're all here. Just don't hug.
16 | Ану, тихо. Тепер я буду базарити. | Did anyone ask me? No? Then listen.
17 | Брешете обидва, як дихаєте. Поважаю. | You're both talking shit, but beautifully.
18 | Мені б когось дзьобнути зара. Нема бажаючих? | Can I peck someone too?
19 | ~1 Та йдіть ви обидва, знаєте куди. | ~1 I spit on you both.
20 | ~3 Нормальні ви, ворони. Для чужих — взагалі пушка. | ~3 What a pair you are. I like it.
21 | Я поки помовчу. Але злий, як собака. | I'm here, just so you know. Listening and getting outraged.
22 | Ви пиздіть, а я на вас дивлюсь і їсти хочу. | You talk, and I'll think about who to eat.
23 | Нудно, бля, {м_слівце}. Давай хоч поб'ємось. | Boring, {м_слівце}. Add some blood to the conversation.
24 | Скажу коротко: хуйня. Дякую за увагу. | Oh, and I'll say: fuck. That's all.
25 | ~1 Язики ж, курва, без кісток. | ~1 How much you yap.
26 | ~3 Гарно, бля. Ше б медовухи. | ~3 We're sitting nicely, fuck.

### M @мх_нове_імя_відповідь   (used by scenes: x_rename)
0 | ^нове {me}, ага. Ше раз скривишся — дзьобом у пику. | ^нове {me}, yes. Make a face again — I'll peck.
1 | ^нове Тепер я {me}, {м_слівце}. Поважай, бо вріжу. | ^нове I'm {me} now, {м_слівце}. Show some respect.
2 | ~1 ^нове {me}. Не подобається — то твої проблеми, бля. | ~1 ^нове {me}. Don't like it — your problem.
3 | ~3 ^нове {me}. Моя людина вибрала. Мені подобається, і все. | ~3 ^нове {me}. My human chose it. I like it.
4 | Ага. {me}. І шо? | Yes. {me}. So what?
5 | ^нове Перехрестили — і шо? Заздриш, га? | ^нове Rechristened — so what? Jealous?
6 | ^нове Краще, ніж твоє, {м_слівце}. Сто пудів. | ^нове Better than yours, {м_слівце}.
7 | ~1 ^нове Відчепись від мого імені, поки цілий. | ~1 ^нове Leave my name alone.

### M @мх_нове_імя   (used by scenes: x_rename)
0 | ^нове Йо, {raven}! Хто тебе так обізвав, {м_слівце}? | ^нове Whoa, {raven}! Who called you that, {м_слівце}?
1 | ^нове {raven}? Пф-ф. Я тебе по-старому кликатиму, і шо ти мені зробиш? | ^нове {raven}? Pff. I'll call you the old way.
2 | ~1 ^нове {raven}. Ну й погоняло, бля. | ~1 ^нове {raven}. What a name.
3 | ~3 ^нове {raven}. Звучить, курва, по-пацанськи. Заслужив. | ~3 ^нове {raven}. Sounds proud, fuck. You earned it.
4 | ^нове О, нове погоняло! Хто тебе так перехрестив, {м_слівце}? | ^нове Oh, a new name! Who rechristened you like that, {м_слівце}?
5 | ^нове {raven}? Ну, хоч не Пушок. Пушка я б не пережив. | ^нове {raven}? Well, at least not Fluffy.
6 | ~3 ^нове {raven}. Пасує тобі, падлюко. Чесно. | ~3 ^нове {raven}. It suits you, bastard.

### M @мх_нове_імя_кінець   (used by scenes: x_rename)
0 | Ну, {addr} то {addr}. Хай буде, хуй з ним. | Well, {addr} it is. So be it.
1 | ^нове Звикну. Мо'. | ^нове I'll get used to it. Maybe.
2 | Добре, {raven}. Запам'ятав. Не радій. | Fine, {raven}. Remembered.
3 | ~3 Носи, братан, не губи. | ~3 Wear it, don't lose it.
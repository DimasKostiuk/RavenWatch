# Packet A: scenes idle_a, idle_b, idle_c, perch_a, perch_b

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): idle_a → «idle_a.», idle_b → «idle_b.», idle_c → «idle_c.», perch_a → «perch_a.», perch_b → «perch_b.»

## Scenes (steps; ? = optional)
- idle_a: M dlg_idle_m_open = {м_почин} / {м_вигук} {м_почин}  →  H dlg_idle_h_reply = {х_відповідь}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- idle_b: H dlg_idle_h_open = {х_почин}  →  M dlg_idle_m_reply = {м_відповідь} / {м_відповідь} {м_лайка}  →  H? dlg_h_close = {х_кінець}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- idle_c: M dlg_player_m = {м_про_гравця} / {м_вигук} {м_про_гравця}  →  H dlg_player_h = {х_про_гравця}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- perch_a: H dlg_perch_h_open = {хп_розмова}  →  M dlg_perch_m_reply = {мп_відповідь}  →  H? dlg_h_close = {х_кінець}
- perch_b: M dlg_perch_m_open = {мп_розмова}  →  H dlg_perch_h_reply = {хп_відповідь}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @м_почин   (used by scenes: idle_a)
0 | ^нудьга Хугіне, нудно, {м_слівце}. Розкажи шось. Нє, стоп, не розказуй. Ну розкажи. | ^нудьга Hugin, I'm bored, {м_слівце}. Tell me something. No, don't tell me.
1 | ^голод Я б зара когось зжер. Тебе, наприклад. Без солі. | ^голод I'd eat someone right now. You, for example.
2 | ^погляд Шо ти на мене так зиркаєш, ботан? Як Одін на п'яного. | ^погляд You're giving me that look again. Like Odin at a drunk.
3 | ^сон Мені снилось, шо ти заткнувся. Охуєнний сон, я аж прокидатись не хотів. | ^сон I dreamed you shut up. It was a fucking awesome dream.
4 | ^мовчиш Хугіне, ти мовчати вмієш, {м_слівце}, чи в тебе це від народження таке? | ^мовчиш Hugin, do you ever shut up, {м_слівце}, or were you born like that?
5 | ^тиша Шось тихо. Аж бісить. Хай хтось когось уже вб'є, ну. | ^тиша This silence again. Someone has to kill someone.
6 | ~1 ^мовчиш Хугіне, тіко почни мені тут розумничати — пір'я повискубую, ботан. | ~1 ^мовчиш Hugin, if you start philosophizing now, I'll pluck your feathers out.
7 | ~23 ^нудьга Шо, професоре, як воно? Срав сьогодні? | ~23 ^нудьга Hey, wiseguy, how's it going? Had a shit?
8 | ~3 ^погляд Хугіне. Та нічо. Тіко гляну, шо ти тут, засранцю. | ~3 ^погляд Hugin. Nothing. Just checking you're here, you little shit.
9 | ^свербить Хугіне, почухай мені під крилом. Нє, стій, не лізь, {м_слівце}. Там своє. | ^свербить Hugin, scratch me under the wing. No, better not touch, {м_слівце}.
10 | ^одін Чуєш, а Одін там теж сидить і дуріє з нудьги, чи тіко нам так пощастило? | ^одін I wonder if Odin is also sitting around bored right now, or are we the only lucky ones?
11 | ^мрія Знаєш, шо я хочу? Спиздити в Одіна око. Просто так. Шоб було. | ^мрія You know what I want? To steal one of Odin's eyes. Just because.
12 | ^сварка Ану давай посремось, {м_слівце}. Чисто для розминки. | ^сварка Let's quarrel, {м_слівце}. As a warm-up.
13 | ~1 ^винен Хугіне, ти мені винен. Шо — без поняття. Але винен, і крапка. | ~1 ^винен Hugin, you owe me. I don't remember what, but you owe me.
14 | ~3 ^плече Чуєш, я б це плече на Асгард не проміняв. Тіко нікому, {м_слівце}, бо дзьобну. | ~3 ^плече You know, I wouldn't trade this shoulder for Asgard. Don't tell anyone, {м_слівце}.
15 | ^голод Чуєш, як пузо бурчить? То воно тебе на обід кличе, ботан. | ^голод Hear my stomach growling? That's me inviting you to lunch.

### H @х_відповідь   (used by scenes: idle_a)
0 | ^нудьга Нудно тобі? Порахуй пір'їни. В себе. | ^нудьга Bored? Count your feathers. Your own.
1 | ^нудьга Можу розповісти про погоду. Ні? Дивно. | ^нудьга I could tell you about the weather. No? Strange.
2 | ~1 ^нудьга Нудно — бо в голові порожньо. | ~1 ^нудьга You're bored because your head is empty.
3 | ~23 ^нудьга Добре, слухай: жив собі ворон, який мовчав. Кінець. Найкраща історія. | ~23 ^нудьга Fine, listen: once there was a raven who kept quiet. The end. The best story.
4 | ~3 ^нудьга Ходімо, знайдемо тобі когось для клювання. | ~3 ^нудьга Come on, let's find you someone to peck.
5 | ^голод Мене не їдять. Я гіркий від мудрості. | ^голод Nobody eats me. I'm bitter with wisdom.
6 | ^голод Поїж моху. Корисно для голови. | ^голод Eat some moss. Good for the head.
7 | ~1 ^голод Їж своє пір'я, на здоров'я. | ~1 ^голод Eat your own feathers, enjoy.
8 | ~3 ^голод Почекай, {name} скоро нагодує. Мабуть. | ~3 ^голод Wait, {name} will feed us soon. Probably.
9 | ^погляд Це не погляд. Це співчуття. | ^погляд That's not a look. That's sympathy.
10 | ^погляд Я дивлюсь, бо чекаю, коли ти щось розумне скажеш. | ^погляд I'm looking because I'm waiting for you to say something smart.
11 | ~3 ^погляд Дивлюсь, бо скучив. Не звикай. | ~3 ^погляд I'm looking because I missed you. Don't get used to it.
12 | ^сон Гарний сон. Шкода, що сни не збуваються. | ^сон A nice dream. Pity dreams don't come true.
13 | ^сон Мені наснилось те саме. Тільки мовчав ти. | ^сон I dreamed the same thing. Only you kept quiet.
14 | ~1 ^сон Сни — єдине місце, де ти розумний. | ~1 ^сон Dreams are the only place you're smart.
15 | ^мовчиш Мовчу. Просто ти мене не чуєш за власною лайкою. | ^мовчиш I'm talking. You just can't hear me over your own swearing.
16 | ^мовчиш Хтось у нашій парі має думати. Не ти. | ^мовчиш Someone in our pair has to think. Not you.
17 | ~3 ^мовчиш Хтось має говорити, поки ти лаєшся. | ~3 ^мовчиш Someone has to talk while you swear.
18 | ^тиша Підозріло? Ні. Просто ти нарешті замовк. | ^тиша Suspicious? No. You've just finally shut up.
19 | ^тиша Тиша — то не біда. Біда — коли ти її порушуєш. | ^тиша Silence isn't the problem. The problem is when you break it.
20 | ~1 ^тиша Убивати буде хтось інший. Ти ж за мною сховаєшся. | ~1 ^тиша Someone else will do the killing. You'll hide behind me anyway.
21 | ^свербить Не чіпатиму. Там, мабуть, щось живе. | ^свербить I won't touch it. Something probably lives there.
22 | ^свербить Свербить — значить, ростеш. Розумом, сподіваюсь, теж. | ^свербить If it itches, you're growing. In brains too, I hope.
23 | ~3 ^свербить Іди сюди, почешу. Тільки не клюйся. | ~3 ^свербить Come here, I'll scratch it. Just don't peck.
24 | ^одін Одін не нудиться. Одін думає. Спробуй якось. | ^одін Odin isn't bored. Odin thinks. Try it sometime.
25 | ^одін Одін зараз слухає скальдів. Пощастило йому, що не тебе. | ^одін Odin is listening to skalds right now. Lucky him it's not you.
26 | ~1 ^одін Одіну без тебе точно не нудно. | ~1 ^одін Odin is certainly not bored without you.
27 | ^мрія Він тебе другим оком і побачить. Останнє, що ти побачиш. | ^мрія He'll see you with his other eye. The last thing you'll see.
28 | ^мрія Мрій тихіше. Він чує все, що ми кажемо. | ^мрія Dream more quietly. He hears everything we say.
29 | ^сварка Посваримось. Ти починай, а я поки подумаю, чи варто. | ^сварка Let's quarrel. You start, and meanwhile I'll think whether it's worth it.
30 | ^сварка Не хочу. Ти програєш, а потім дуєшся до вечора. | ^сварка I don't want to. You'll lose and then sulk until evening.
31 | ~1 ^винен Я тобі нічого не винен. Хіба що терпіння. | ~1 ^винен I owe you nothing. Except patience.
32 | ~1 ^винен Винен? Добре. Я тобі винен тишу. Отримуй. | ~1 ^винен Owe you? Fine. I owe you silence. Here you go.
33 | ~3 ^плече І я. Хоч ти й смердиш болотом. | ~3 ^плече Me too. Even though you smell of swamp.
34 | ~3 ^плече Не скажу. Але запишу. Глибоко. | ~3 ^плече I won't say it. But I'll write it down. Deep.
35 | ^голод Попроси. {name} не відмовить, якщо чемно. Знаю, тобі важко. | ^голод Ask. {name} won't refuse if you're polite. I know it's hard for you.
36 | ^тиша Убий час. Він сам не помре. | ^тиша Kill time. It won't die by itself.
37 | ^погляд Я дивлюсь, як ти линяєш. Захопливо. | ^погляд I'm watching you molt. Fascinating.
38 | ^сон А мені снилось, що ти розумний. Отакі в нас дурні сни. | ^сон And I dreamed you were smart. Such silly dreams we have.
39 | ^мовчиш Мовчати я вмію. Але поруч із тобою хтось має бути розумним. | ^мовчиш I know how to keep quiet. But next to you someone has to be smart.

### M @м_кінець   (used by scenes: idle_a, idle_b, idle_c, rest_b, night, near_death, gap_m, walk, cross_m, perch_b)
0 | Все. Я сказав. Крапка. | That's it, I've spoken.
1 | Тьху на тебе, ботан. | I spit on you.
2 | Забудь. І я забуду, {м_слівце}. Вже забув. | Forget it. I'll forget it too, {м_слівце}.
3 | Та ну його все нахуй. | Fuck everything.
4 | ^нудьга Історія — гівно. Як і ти, професоре. | ^нудьга The story is shit. Like you.
5 | ^нудьга Ой смішно. Ой, тримайте мене, зара обісрусь. | ^нудьга Very funny. I'm shitting myself.
6 | ^тиша Ну-ну. Ше подивимось, хто перший заверещить. | ^тиша Well, well. We'll see who squeals first.
7 | ^голод Ну дай хоч хробака якогось, жмот. | ^голод At least give me a worm.
8 | ^погляд|мовчиш Дивись, дивись. Я теж так вмію. Бачиш? Дивлюсь. | ^погляд|мовчиш Look, look. I can do it too.
9 | ^сон Все одно приснишся мені з закритим дзьобом. Сто пудів. | ^сон You'll still show up in my dreams with your mouth shut.
10 | ^калюжа Я не впав. Я спланував. Шариш різницю? | ^калюжа I didn't fall. I glided.
14 | ^голод Схуднути?! Я тобі зара так схудну — без пір'я лишишся. | ^голод Lose weight? I'll lose you right now.
23 | ^будує Ну-ну. Подивимось, хто з нас ржатиме останнім. | ^будує Well, well. We'll see which of us laughs.
24 | ^сам Я не матюкаюсь, {м_слівце}. Я так розмовляю. Розумієш різницю? | ^сам I'm not swearing. That's how I communicate, {м_слівце}.
25 | ^меч Мухи — то не тролі, професоре. Вкурив? | ^меч Flies aren't trolls, wiseguy.
26 | ^розчарування Ой, все. Святий, бля, знайшовся. | ^розчарування Oh, that's it. Look, a saint.
27 | ^похвала Тіко спробуй ляпнути, {м_слівце}. Тіко спробуй. | ^похвала Just try it, {м_слівце}.
28 | ~3 ^болото Є коліна! Просто сховані, {м_слівце}. Під пір'ям. | ~3 ^болото I do have knees. They're just hidden, {м_слівце}.
41 | ~1 Та йди ти в сраку, ботан. | ~1 Go up your own ass.
42 | ~23 Ну й хай, {м_слівце}. Мені похуй. | ~23 So be it, {м_слівце}.
43 | ~3 Все, мир. До вечора, засранцю. Потім знов посремось. | ~3 Fine, peace. Until evening, you little shit.
44 | ^свербить Сам ти шось живе. Блохастий. | ^свербить You're the something that lives there.
45 | ^одін Та хай слухає. Я йому ше й заспіваю, {м_слівце}. Матюками. | ^одін Let him listen. I'll even sing for him, {м_слівце}.
46 | ^мрія Ото ше глянемо, хто кого побачить, ага. | ^мрія We'll see who sees whom.
47 | ~3 ^плече Тіко не розкисай, засранцю. Бо я теж розкисну. | ~3 ^плече Just don't go soft, you little shit.
48 | ^сварка Ну все. Знов я виграв. Бо ти мовчиш, як пень. | ^сварка There. I won again, because you're silent.
49 | ~1 ^винен Тишу? Ти? Мені? Ой, не смішіть мене, бля. | ~1 ^винен Silence? You? Don't make me laugh.
50 | ^співає Аби не я? Та я співаю, як бог, {м_слівце}! Як Браґі! | ^співає As long as it's not me? I sing like a god, {м_слівце}.
51 | ^їсть Та хай жере. Аби й мені шось лишилось, бо я ж не камінь. | ^їсть So what. As long as there's some left for me.
52 | ^спить Я не хроплю. Я вві сні гарчу. Грізно. | ^спить I don't snore. I growl in my sleep.
53 | ^руки А мені цікаво, як тебе обскубти. Кожну пір'їну. | ^руки I'm curious how to pluck you.
59 | ^руки Від злості — то так, {м_слівце}. Лапи аж свербять. | ^руки From anger — that's true, {м_слівце}.
63 | Та пішов ти, {м_слівце}. Знаєш куди. | Well, screw you, {м_слівце}.
64 | ~3 Ну, добре. Хай буде по-твоєму. Раз. | ~3 Well, fine. So be it.

### H @х_почин   (used by scenes: idle_b)
0 | ^тиша Муніне, ти чув? Тиша. Підозріло. | ^тиша Munin, did you hear that? Silence. Suspicious.
1 | ^тиша Надто спокійно. Не люблю, коли спокійно. | ^тиша Too calm. I don't like it when it's calm.
2 | ^рахую Я рахую дерева. Збився на сорок другому. | ^рахую I'm counting trees. Lost count at forty-two.
3 | ^сни Цікаво, чи сниться воронам щось, крім їжі. | ^сни I wonder if ravens dream of anything besides food.
4 | ^компанія Знаєш, що найгірше в мандрах? Компанія. | ^компанія You know what the worst thing about travel is? The company.
5 | ~1 ^сморід Муніне, ти знову смердиш болотом. | ~1 ^сморід Munin, you smell of swamp again.
6 | ~1 ^розумне Скажи щось розумне. Хоч раз. Для історії. | ~1 ^розумне Say something smart. Just once. For history.
7 | ~23 ^день Гарний день, Муніне. Не зіпсуй його. | ~23 ^день A fine day, Munin. Don't ruin it.
8 | ~3 ^утрьох Знаєш, а ми непогано тримаємось. Утрьох. | ~3 ^утрьох You know, we're holding up pretty well. The three of us.
9 | ^одін Як гадаєш, Муніне, Одін нас ще пам'ятає? | ^одін What do you think, Munin, does Odin still remember us?
10 | ^вітер Чуєш, як вітер змінився? Щось буде. | ^вітер Feel how the wind has changed? Something's coming.
11 | ^пір'я Муніне, у тебе пір'їна стирчить. Отам. Ні, не там. | ^пір'я Munin, you've got a feather sticking out. There. No, not there.
12 | ^хмара Он та хмара схожа на тебе. Така ж набурмосена. | ^хмара That cloud over there looks like you. Just as sulky.
13 | ~3 ^дякую Знаєш, я рідко це кажу. Але добре, що ти поруч. | ~3 ^дякую You know, I rarely say this. But it's good you're around.

### M @м_відповідь   (used by scenes: idle_b)
0 | ^тиша Підозріло? Та це кайф, бля. Нікого клювати не треба. Ну, крім тебе. | ^тиша Suspicious? It's fucking awesome. Finally nobody to peck.
1 | ^тиша Тиша, {м_слівце}. Зара шось вилізе і все засере, от побачиш. | ^тиша Silence, {м_слівце}. Something's about to crawl out and ruin it.
2 | ^рахую Сорок два? Я б уже з глузду з'їхав. А ти й так з'їхав, ботан. | ^рахую Forty-two? I'd have lost my mind. And you already have.
3 | ^рахую Рахуй, рахуй, професоре. А я покимарю. | ^рахую Count, count. I'll take a nap meanwhile.
4 | ^сни Мені м'ясо сниться. А тобі шо, книжки? Нудьга, бля. | ^сни I dream of meat. And you — of your clever thoughts. Boring.
5 | ^сни Сниться, ага. Як я тобі дзьоба відкручую. Кайфовий сон. | ^сни I do. Of me twisting your beak off.
6 | ^компанія Компанія? Це ти про себе, чи шо, гівнюче? | ^компанія Company? Are you talking about yourself, you shithead?
7 | ^компанія В мандрах найгірше — твоя мудрість, {м_слівце}. Всю дорогу в вуха. | ^компанія The worst thing about travel is your wisdom, {м_слівце}.
8 | ^сморід Болотом? Та то від тебе, {м_слівце}, книжками смердить. | ^сморід Swamp? It's you, {м_слівце}, who reeks of books.
9 | ^сморід Я смерджу, бо пашу. А ти пахнеш, бо ніхуя не робиш. | ^сморід I stink because I work. And you smell nice because you do nothing.
10 | ^розумне Розумне? Йди нахуй. Ось. Сказав. Шо, не розумно? | ^розумне Something smart? Fuck off. There, I said it.
11 | ^розумне Я розумний, коли треба. Зара не треба. Відчепись. | ^розумне I'm smart when needed. It's not needed now.
12 | ^день Гарний? Ну, поки ти пиздіти не почав — гарний. | ^день Fine? Well, until you start bullshitting.
13 | ~3 ^утрьох Ага. Нормально, {м_слівце}. Тіко не розкисай мені, ботан. | ~3 ^утрьох Yeah. Not bad, {м_слівце}. Just don't go soft.
14 | ~3 ^день Ну… гарний, шо. Не буду псувати. Сьогодні. | ~3 ^день Well… fine. I won't ruin it. Today.
15 | ^одін Пам'ятає, а як же. Мене, {м_слівце}, не забудеш, хоч як старайся. | ^одін He remembers. You don't forget someone like me, {м_слівце}.
16 | ^одін Хай тіко спробує забути. Нагадаю. Дзьобом у тім'я. | ^одін Let him try to forget. I'll remind him. With my beak.
17 | ^вітер Шось буде? Та й добре. Я вже сраку відсидів, {м_слівце}. | ^вітер Something's coming — good. I've been sitting too long, {м_слівце}.
18 | ^вітер Вітер як вітер. Не вигадуй, професоре. | ^вітер Wind is wind. Don't make things up, wiseguy.
19 | ^пір'я Стирчить, бо я бойовий, ясно? А ти прилизаний, як жрець на свято. | ^пір'я It sticks out because I'm a fighter. Yours is all smoothed down, like a priest's.
20 | ^пір'я Де? Тут? Ну все, якшо пиздиш — ти труп. | ^пір'я Where? Here? That's it, you're dead if you're lying.
21 | ^хмара А он та — на тебе схожа. Пуста і надута. | ^хмара And that one — looks like you. Empty and puffed up.
22 | ^хмара Набурмосена? Я тобі зара покажу набурмосену, {м_слівце}. В табло. | ^хмара Sulky? I'll show you sulky right now, {м_слівце}.
23 | ~3 ^дякую Та йди ти… Ну, тобто… й тобі. Ага. Й тобі, ботан. | ~3 ^дякую Go to… I mean, you too, wiseguy. You too.
24 | ~3 ^дякую Ну, бля. Шо ти мені тут розкис, як хліб у пиві. | ~3 ^дякую Well, fuck. Don't go soft on me here.
25 | ^тиша Не любиш тиші? То я зара як заору. Легше стало? | ^тиша Don't like calm? Then I'll scream right now. Better?
26 | ^рахую Сорок третій — то я. Мене не рахуй, бо дзьобну. | ^рахую The forty-third is me. Don't count me.
27 | ^сни Снилось, шо я Одін. А ти мені п'яти чухаєш. Ото був сон. | ^сни I dream I'm Odin. And you're scratching my heels.
28 | ^компанія Сам ти компанія. А я — подарунок. Долі, бля. | ^компанія You're the company. I'm a gift of fate.
29 | ~3 ^утрьох Утрьох, бо третій годує. Якби не жрачка — кинув би вас обох. Мо'. | ~3 ^утрьох Three of us — because the third one feeds us. Otherwise I'd have left you both.

### H @х_кінець   (used by scenes: idle_b, fog, death, biome, fed_munin, gap_h, sail, cross_h, perch_a)
0 | Ну все. Досить мудрості на сьогодні. | That's it. Enough wisdom for today.
1 | Залишимо це на потім. | Let's leave it for later.
2 | Обговоримо, коли ти подорослішаєш. Тобто ніколи. | We'll discuss it when you grow up. That is, never.
3 | ^тиша Тихо. Попереду щось є. А, ні. Кущ. | ^тиша Quiet. Something's ahead. Ah, no. A bush.
4 | ^компанія|сморід Компанія, як і казав. | ^компанія|сморід The company, as I said.
5 | ^сни|рахую Добре. Повернусь до дерев. Сорок три… | ^сни|рахую Fine. Back to the trees. Forty-three…
15 | ^вітер Не міряй усіх по собі. | ^вітер Don't judge everyone by yourself.
16 | ^вітер Хоч раз погодились. | ^вітер We agreed for once.
30 | ~3 Гаразд, гаразд. Мир. | ~3 Fine, fine. Peace.
31 | ^одін Хай пам'ятає. Аби не кликав назад. | ^одін Let him remember. As long as he doesn't call us back.
32 | ^вітер Побачимо. Тримайся міцніше. | ^вітер We'll see. Hold on tighter.
33 | ^пір'я Не там. Ну й хай стирчить. Тобі личить. | ^пір'я Not there. Well, let it stick out. It suits you.
34 | ^хмара Он, уже темніє. Як ти після сварки. | ^хмара Look, it's already getting darker. Like you after a quarrel.
35 | ~3 ^дякую Ну от. Ти теж уже розкис. | ~3 ^дякую There. Now you've gone soft too.
44 | Досить. У мене від тебе пір'я сивіє. | Enough. You're turning my feathers gray.
45 | ~2 Гаразд, гаразд. Хай буде по-твоєму. Цього разу. | ~2 Fine, fine. Have it your way. This time.

### M @м_про_гравця   (used by scenes: idle_c)
0 | ^калюжа Ти бачив, як {name} в калюжу гепну{вся|лася}? Я досі ржу, {м_слівце}. | ^калюжа Did you see {name} fall into that puddle? I'm still neighing, {м_слівце}.
1 | ^будує {name} знов шось будує. Як завалиться — я перший заржу, зуб даю. | ^будує {name} is building something again. If it collapses — I'll be the first to laugh.
2 | ^сам Хугіне, глянь: {name} сам{|а} з собою балакає. Ну пиздець, приїхали. | ^сам Hugin, {name} is talking to {himself|herself}. Holy shit.
3 | ^меч {name} мечем махає, як мух ганяє, {м_слівце}. Сміх, та й годі. | ^меч {name} swings that sword like chasing flies, {м_слівце}.
4 | ~1 ^розчарування {name} сьогодні — повний відстій. Як і ти, ботан. | ~1 ^розчарування {name} is a total disappointment today. Like you.
5 | ~23 ^похвала {name} — нормальн{ий|а}, {м_слівце}. Тіко {йому|їй} не ляпни, шо я таке сказав. | ~23 ^похвала {name} is all right, {м_слівце}. Don't tell {him|her} I said so.
6 | ~3 ^болото За {name} я б і в болото поліз, {м_слівце}. Ну. По коліна. Не глибше. | ~3 ^болото For {name} I'd even go into a swamp, {м_слівце}. Well, knee-deep.
7 | ^співає Хугіне, {name} знов шось мугикає. Вуха в'януть, {м_слівце}. Де в ворона вуха — не знаю, але в'януть. | ^співає Hugin, {name} is humming something again. My ears are wilting, {м_слівце}.
8 | ^їсть Бачив, скільки {name} трескає? От мені б так, бля. | ^їсть Did you see how much {name} gobbles? I wish I could.
9 | ^спить {name} вночі хропе, як троль з перепою. | ^спить {name} snores at night worse than a troll.
10 | ^руки {name} знов усе руками мацає. Шо бачить — те й лапає. Як я. | ^руки {name} is pawing everything again. Whatever {he|she} can't see, {he|she} gropes.

### H @х_про_гравця   (used by scenes: idle_c)
0 | ^калюжа Бачив. А ти вчора впав з плеча. Теж було смішно. | ^калюжа I saw. And yesterday you fell off the shoulder. That was funny too.
1 | ^калюжа Зате {name} {вибрався|вибралась} сам{|а}. А тебе я витягав з болота. | ^калюжа At least {name} got out alone. I had to pull you out of the swamp.
2 | ^будує Не завалиться. Я стежив за кожною балкою. | ^будує It won't collapse. I watched every beam.
3 | ^будує Ти ржатимеш з-під уламків, Муніне. Ми ж на {ньому|ній} сидимо. | ^будує You'll be laughing from under the rubble, Munin. We're sitting on {him|her}, remember.
4 | ^сам Не сам{|а}. З нами. Просто ми не завжди відповідаємо. | ^сам Not alone. With us. We just don't always answer.
5 | ^сам Хтось же має з {ним|нею} говорити, поки ти лаєшся. | ^сам Someone has to talk to {him|her} while you swear.
6 | ^меч Зате жодна муха ще не втекла. | ^меч At least no fly has escaped yet.
7 | ^меч Ти махаєш крилами не краще. | ^меч You flap your wings no better.
8 | ~1 ^розчарування Принаймні {name} не лається на весь ліс. | ~1 ^розчарування At least {name} doesn't swear at the whole forest.
9 | ~1 ^розчарування Розчарування — це коли ворон замість думки видає матюк. | ~1 ^розчарування Disappointment is when a raven produces a curse instead of a thought.
10 | ~23 ^похвала Не скажу. Але запишу. | ~23 ^похвала I won't say it. But I'll write it down.
11 | ~23 ^похвала О, Мунін когось похвалив. Запам'ятаю цей день. | ~23 ^похвала Oh, Munin praised someone. I'll remember this day.
12 | ~3 ^болото У тебе нема колін, Муніне. | ~3 ^болото You don't have knees, Munin.
13 | ~3 ^болото І я б поліз. Разом тепліше. | ~3 ^болото I'd have gone in too. It's warmer together.
14 | ^співає Це пісня, Муніне. Ти просто не знаєш, що це. | ^співає It's a song, Munin. You just don't know what that is.
15 | ^співає Хай співає. Аби не ти. | ^співає Let {him|her} sing. As long as it's not you.
16 | ^їсть {name} працює за трьох. Тобто й за тебе. | ^їсть {name} works for three. Meaning for you too.
17 | ^їсть Жере? Їсть. А жереш тут ти. | ^їсть Gobbles? Eats. You're the one who gobbles here.
18 | ^спить Гірше за троля хропеш ти. Я чув обох. | ^спить You snore worse than a troll. I've heard both.
19 | ^спить Хай хропе. Значить, жив{ий|а}. | ^спить Let {him|her} snore. It means {he's|she's} alive.
20 | ^руки Так вчаться. Ти, щоправда, і дзьобом не навчився. | ^руки That's how one learns. You, though, haven't even learned with your beak.
21 | ^руки Мацає — бо цікаво. Тобі колись було цікаво? | ^руки {He|She} pokes at things because {he's|she's} curious. Were you ever curious?

### H @хп_розмова   (used by scenes: perch_a)
0 | ^жердина Муніне, ти теж відчуваєш, як сила вертається? | ^жердина Munin, do you feel your strength coming back too?
1 | ^рівна Добра в нас жердина. Хоч щось у цьому домі рівне. | ^рівна We have a good perch. At least something in this house is straight.
2 | ^сидіти Знаєш, сидіти — теж робота. Розумова. | ^сидіти You know, sitting is work too. Mental work.
3 | ^задум Цікаво, що {name} знову задума{в|ла}. Щось велике, мабуть. | ^задум I wonder what {name} is up to again. Something big, probably.
4 | ^тиша Чуєш? Ніхто не кричить. Ніхто не біжить. Рай. | ^тиша Hear that? Nobody's shouting. Nobody's running. Paradise.
5 | ~1 ^ти Посунься, Муніне. Ти займаєш пів жердини. | ~1 ^ти Move over, Munin. You take up half the perch.
6 | ~23 ^дім Тут затишно. Майже як удома в Одіна. | ~23 ^дім It's cozy here. Almost like at home with Odin.
7 | ~3 ^ми Нам із тобою пощастило, Муніне. Хоч ти й не визнаєш. | ~3 ^ми We got lucky, Munin. Though you won't admit it.
8 | ^сила Муніне, скільки сили ти вже набрав? Чесно. | ^сила Munin, how much strength have you gathered already? Honestly.
9 | ^тиша Тихо як. Рай, а не жердина. | ^тиша So quiet. Paradise, not a perch.
10 | ^забуде Як думаєш, Муніне, {name} нас колись тут забуде? | ^забуде What do you think, Munin, will {name} ever forget us here?
11 | ^вигадка Цікаво, хто першим вигадав жердину. Мудра була голова. | ^вигадка I wonder who invented the perch first. A wise head it was.
12 | ^разом Муніне, ти колись думав, що ми отак сидітимемо разом? | ^разом Munin, did you ever think we'd sit together like this?
13 | ^пера Ти знову чистиш пір'я, Муніне? Третій раз за годину. | ^пера Cleaning your feathers again, Munin? Third time in an hour.
14 | ~1 ^думаю Сиди тихо, Муніне. Я думаю. | ~1 ^думаю Sit quietly, Munin. I'm thinking.
15 | ~23 ^дім Добре, що {name} постави{в|ла} нам жердину. Визнай. | ~23 ^дім Good that {name} put up a perch for us. Admit it.
16 | ^їжа Муніне, ти знову голодний? По очах бачу. | ^їжа Munin, hungry again? I can see it in your eyes.
17 | ^забуде Іноді думаю: а що, як {name} про нас забуде? Ні, дурна думка. | ^забуде Sometimes I think: what if {name} forgets about us? No, a silly thought.

### M @мп_відповідь   (used by scenes: perch_a)
0 | ^жердина Сила йде, ага. А нудьга — ше швидше. | ^жердина Strength is coming, yeah. Boredom comes faster.
1 | ^сидіти Сидіти — робота? Ти її сам придумав, шоб нічо не робити. | ^сидіти Sitting is work? You invented that so you can do nothing.
2 | ^сидіти Мені нудно, а ти мене ше й повчаєш, професор. | ^сидіти I'm bored, and you're lecturing me on top of it.
3 | ^задум Шось дурне. І знов якоїсь хуйні притягне. | ^задум Something stupid. And it'll drag in more dirt again.
4 | ^тиша Рай? Рай — це коли ти мовчиш, Хугіне. | ^тиша Paradise? Paradise is when you shut up, Hugin.
5 | ~1 ^ти Сам посунься, розумнику. | ~1 ^ти Move over yourself, smartass.
6 | ~23 ^дім Ну... тут і справді нічо. Окей. | ~23 ^дім Well... it really isn't bad here. Okay.
7 | ~3 ^ми Ну от, розпустив соплі. Йди вже, пташко. | ~3 ^ми There, you've gone soft. Off with you, birdie.
8 | ^ти Займаю скільки хочу. Хочеш більше — злітай. | ^ти I take up as much as I want. Want more — fly off.
9 | ^ти А ти не пхайся крилом, розумнику. | ^ти And don't shove me with your wing, smartass.
10 | ^ми Ой, тіко не рюмсай мені тут, Хугіне. | ^ми Oh, just don't cry on me here, Hugin.
11 | ^ми Пощастило, ага. Особливо тобі — зі мною. | ^ми Lucky, yeah. Especially you — with me.
12 | ^сидіти Сидіти — не робота. Робота — це когось дзьобнути. | ^сидіти Sitting isn't work. Work is pecking someone.
13 | ^їжа Голодний — не те слово. Хай {name} несе м'ясо, поки я не почав з тебе. | ^їжа Hungry isn't the word. Let {name} bring meat before I start on you.
14 | ^тиша Тихо, бо всі від тебе повтікали, ботане. | ^тиша Quiet, because everyone ran away from you, Hugin.
15 | ^сила|жердина Сила? Ну, сила — то нормас. Але м'ясо краще. | ^сила|жердина Strength? Strength is good. But meat is better.
16 | ~1 ^тиша Рай? Нудьга смертна. Хоч би хтось когось зжер. | ~1 ^тиша Paradise? Deadly boredom. If only someone would eat someone.
17 | ~3 ^забуде Нікуди {name} не дінеться. Куди ж без нас, га. | ~3 ^забуде {name} won't go anywhere. Where would {he|she} be without us.
18 | ^забуде Хай тіко спробує. Знайду {його|її} і клюну. В сраку. | ^забуде Let {him|her} try. I'll find {him|her}. And peck.
19 | ^сила Досить, шоб клюнути тебе. Двічі. | ^сила Enough to peck you. Twice.
20 | ^вигадка Якийсь лінивий птах, якому заїбало літати. Мій герой. | ^вигадка Some lazy bird who got tired of flying. My hero.
21 | ^пера Бо хтось тут на мене линяє, Хугіне. | ^пера Because someone here is molting on me, Hugin.
22 | ^їжа Голодний. Завжди. Це не секрет, це по життю так. | ^їжа Hungry. Always. It's not a secret, it's a way of life.
23 | ^дім Добре, добре. Тут гарно. Задоволений, ботан? | ^дім All right, all right. It's nice here. Happy?
24 | ^рівна Рівна, бо я на ній сиджу. Решта тут — криве гівно. | ^рівна Straight because I'm sitting on it. Everything else here is crooked.
25 | ^думаю Думай тихіше. Аж сюди чути, як у тебе в башці скрипить. | ^думаю Think more quietly. I can hear it creaking from here.
26 | ^разом Думав. І щоразу жахався. А тепер нічо, звик. | ^разом I did. And every time I was horrified. Now it's fine, I'm used to it.

### M @мп_розмова   (used by scenes: perch_b)
0 | ^крива Хугіне, тобі не здається, шо ця палиця кривувата? Ну крива ж, бля. | ^крива Hugin, don't you think this stick is a bit crooked?
1 | ^сидіти Скільки нам тут ше сидіти? Мені вже нудно, капець. | ^сидіти How much longer do we sit here? I'm already bored.
2 | ^задум Шо там {цей дурень|ця дурепа} {name} знов затіває? | ^задум What is that idiot {name} up to again?
3 | ^тиша Чуєш, як тихо? Аж кортить когось клюнути. | ^тиша Hear how quiet it is? Makes me want to peck someone.
4 | ~1 ^ти Посунься, Хугіне. Ти тут не сам, бля. | ~1 ^ти Move over, Hugin. You're not alone here.
5 | ~23 ^жердина Знаєш, а жердина ця нічо. Тіко нікому. | ~23 ^жердина You know, this perch isn't bad. Don't tell anyone.
6 | ~3 ^ми Слухай, Хугіне... з людиною нам пощастило. {name} — наш{|а}. Тіко мовчи, ясно? | ~3 ^ми Listen, Hugin... we got lucky with our human. {name} is ours. Just keep quiet.
7 | ^сидіти Хугіне, ше годину тут — і я стану совою, бляха. | ^сидіти Hugin, one more hour here and I'll turn into an owl.
8 | ^тиша Хугіне, ти спиш? Не спи. Мені нудно. | ^тиша Hugin, are you asleep? Don't sleep. I'm bored.
9 | ^їжа Хугіне, як думаєш, {name} хоч пожерти нам принесе? | ^їжа Hugin, do you think {name} will at least bring us something to eat?
10 | ^думаєш Хугіне, ти знов думаєш? По пиці ж видно, ботан. | ^думаєш Hugin, are you thinking again? It shows on your face.
11 | ^ми Хугіне... а ми з тобою нормальна пара. Тіко нікому. | ^ми Hugin... you and I make a decent pair. Don't tell anyone.
12 | ^смола Ця жердина смолою пахне. Мені заходить. Мовчи. | ^смола This perch smells of resin. I like it. Shut up.
13 | ~1 ^дивись Не дивись на мене, Хугіне. Бісиш. | ~1 ^дивись Don't look at me, Hugin. You're annoying.
14 | ~23 ^дім А шо, сидіти тут — не найгірша хуйня. | ~23 ^дім You know, sitting here isn't the worst.
15 | ^забуде Хугіне, а якшо {name} про нас забуде? Я ж {його|її} тоді знайду. | ^забуде Hugin, what if {name} forgets about us? I'll find {him|her} then.
16 | ^задум Цікаво, шо {name} там знов ламає. Чую ж, як гуркотить. | ^задум I wonder what {name} is breaking out there again. I can hear the crashing.

### H @хп_відповідь   (used by scenes: perch_b)
0 | ^сидіти Зате сила йде. А від твого бурчання — ні. | ^сидіти At least strength comes. From your grumbling — none.
1 | ^сидіти Посидь спокійно хоч раз. Тобі піде на користь. | ^сидіти Sit still for once. It'll do you good.
2 | ^нудьга|сидіти Нудно? Порахуй пір'їни. Я свої вже порахував. | ^нудьга|сидіти Bored? Count your feathers. I've counted mine already.
3 | ^задум Щось велике і, як завжди, без плану. Зате з гуркотом. | ^задум Something big and, as always, without a plan. But with a crash.
4 | ^тиша Тиша — це коли ти мовчиш. Цінуй момент. | ^тиша Silence is when you keep quiet. Treasure the moment.
5 | ~1 ^ти Я займаю рівно стільки, скільки заслуговую. Тобто більше. | ~1 ^ти I take up exactly as much as I deserve. That is, more.
6 | ~23 ^дім Тут добре. Не зіпсуй, Муніне. | ~23 ^дім It's good here. Don't spoil it, Munin.
7 | ~3 ^ми Знаю. Тільки не кажи це так ніжно, а то розчулюся. | ~3 ^ми I know. Just don't say it so tenderly, or I'll get sentimental.
8 | ^ти Посунутись? Я тут сидів першим. Здається. | ^ти Move over? I was sitting here first. I think.
9 | ^ти Не штовхайся. Жердина не така широка, як твоє самолюбство. | ^ти Don't push. The perch isn't as wide as your ego.
10 | ^ми Може, й так. Але вголос я цього не скажу. Ой. | ^ми Maybe so. But I won't say it out loud. Oops.
11 | ^ми Тихіше, Муніне. Бо ще хтось почує, що ти м'якнеш. | ^ми Quieter, Munin. Or someone will hear you're going soft.
12 | ^сидіти Скільки треба, стільки й сидітимемо. Сила сама не набереться. | ^сидіти We'll sit as long as needed. Strength won't gather by itself.
13 | ^забуде Не забуде. Я це знаю. Я ж думка. | ^забуде {He|She} won't forget. I know it. I'm the thought, after all.
14 | ^тиша Я чую. Але мовчу, бо мудрий. | ^тиша I hear it. But I keep quiet, because I'm wise.
15 | ^крива Кривувата? То ти криво сидиш, Муніне. | ^крива A bit crooked? It's you sitting crooked, Munin.
16 | ~1 ^нудьга|тиша Нудьга — то для дурних. Тобто для тебе. | ~1 ^нудьга|тиша Boredom is for fools. That is, for you.
17 | ~3 ^забуде Нікуди {name} без нас не дінеться. І ми без {нього|неї}. | ~3 ^забуде {name} won't get anywhere without us. Nor we without {him|her}.
18 | ^їжа Принесе, якщо попросиш чемно. Тобто ніколи. | ^їжа {He|She} will bring some, if you ask politely. That is, never.
19 | ^їжа Принесе. {name} про нас не забуває. Про себе — буває. | ^їжа {He|She} will bring some. {name} doesn't forget about us. About {himself|herself} — sometimes.
20 | ^думаєш Я завжди думаю. Спробуй і ти, хоч раз на рік. | ^думаєш I always think. You try it too, once a year.
21 | ^дивись Дивлюсь, бо ти щось замишляєш. Я ж бачу. | ^дивись I'm watching because you're plotting something. I can see it.
22 | ^смола Смолою пахне, бо свіжа. Тільки не здумай її гризти. | ^смола It smells of resin because it's fresh. Just don't you dare gnaw it.
23 | ^крива Рівна вона. Це в тебе погляд кривий. | ^крива It's straight. It's your view that's crooked.
24 | ^дім Ото ж бо. Цінуй, поки маєш. | ^дім Exactly. Treasure it while you have it.
25 | ^задум Головне, щоб не знову міст через болото. | ^задум As long as it's not another bridge over a swamp.
26 | ^жердина Бачиш? А ти бурчав. Я все чую, Муніне. | ^жердина See? And you grumbled. I hear everything, Munin.
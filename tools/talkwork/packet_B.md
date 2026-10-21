# Packet B: scenes rest_a, rest_b, rain, fog, night, snow, storm, dawn, dusk

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): rest_a → «rest_a.», rest_b → «rest_b.», rain → «rain.», fog → «fog.», night → «night.», snow → «snow.», storm → «storm.», dawn → «dawn.», dusk → «dusk.»

## Scenes (steps; ? = optional)
- rest_a: H dlg_rest_h = {х_вогонь}  →  M dlg_rest_m = {м_вогонь} / {м_вогонь} {м_лайка}  →  H? dlg_h_soft = {х_тепло}
- rest_b: M dlg_rest_m_open = {м_вогонь_почин} / {м_вигук} {м_вогонь_почин}  →  H dlg_rest_h_reply = {х_вогонь_відповідь}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- rain: M dlg_rain_m = {м_дощ} {м_лайка?} / {м_вигук} {м_дощ}  →  H dlg_rain_h = {х_дощ}  →  M? dlg_rain_m2 = {м_дощ_кінець}
- fog: H dlg_fog_h = {х_туман}  →  M dlg_fog_m = {м_туман} / {м_вигук} {м_туман}  →  H? dlg_h_close = {х_кінець}
- night: M dlg_night_m = {м_ніч} / {м_вигук} {м_ніч}  →  H dlg_night_h = {х_ніч}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- snow: M dlg_snow_m = {м_сніг} / {м_вигук} {м_сніг}  →  H dlg_snow_h = {х_сніг}  →  M? dlg_snow_m2 = {м_сніг_кінець}
- storm: H dlg_storm_h = {х_гроза}  →  M dlg_storm_m = {м_гроза} / {м_вигук} {м_гроза}  →  H? dlg_storm_h2 = {х_гроза_кінець}
- dawn: H dlg_dawn_h = {х_світанок}  →  M dlg_dawn_m = {м_світанок} / {м_вигук} {м_світанок}  →  H? dlg_dawn_h2 = {х_світанок_кінець}
- dusk: M dlg_dusk_m = {м_вечір} / {м_вигук} {м_вечір}  →  H dlg_dusk_m_h = {х_вечір}  →  M? dlg_dusk_m2 = {м_вечір_кінець}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @м_кінець   (used by scenes: idle_a, idle_b, idle_c, rest_b, night, near_death, gap_m, walk, cross_m, perch_b)
29 | ^ніч|бійка Ну хоч зранку комусь напихаю. | ^ніч|бійка Well, at least in the morning I'll beat someone up.
39 | ^спів Тіко не співай, {м_слівце}. Благаю. Я ж ворон, а не камінь. | ^спів Just don't sing, {м_слівце}.
40 | ^пиво|риба Ага. Розказуй, розказуй, пиздабол. | ^пиво|риба Yeah, sure, tell me more.
54 | ^шрам Пиздиш, як Локі. Навіть гірше. | ^шрам You lie like Loki.
55 | ^мед Ну ти й жлоб, {м_слівце}. Ріг меду пожалів. | ^мед What a cheapskate you are, {м_слівце}.
56 | ^дим Найгарнішого? А, ну тоді ясно. На мене, кого ж ше. | ^дим The handsomest? Yeah. Well, then everything's clear.
57 | ^вовки Я теж голодний, бля. І я ближче. Тож слухай мене. | ^вовки I'm hungry too. Listen to me, I'm closer.
58 | ^очі Курчам? Йди сам глянь, розумнику. Я тут посиджу. | ^очі A chicken? Go look yourself, smartass.
75 | ^зорі_н Та блохи, зорі — одна хуйня. Дрібне все. | ^зорі_н Fleas, stars — equally tiny.
76 | ^сова Сторож, мудра… А верещить, як різана. | ^сова A watchman, wise… Still loud.
77 | ^місяць Хай світить. Тіко не на мене, бо дзьобну. | ^місяць Let it shine. But not on me.
78 | ^холод_н Добре, добре. Тіко швидше, бо я в бурульку перетворюсь. | ^холод_н Fine. But faster, I'm freezing.
79 | ^шурхіт Дзьобну. Шоб знало. | ^шурхіт I'll peck it. Just in case.
80 | ^сон_н Ну то я покимарю. Ти ж однаково не спиш, от і стережи. | ^сон_н Well, then I'll sleep. You're not sleeping anyway.
81 | ^драуґри Шкода, бляха. Я б подзьобав. | ^драуґри Pity. I'd have pecked a bit.
82 | ^історія Ха-ха. Смішно. Я аж не засміявся, бачиш? | ^історія Funny. I nearly didn't laugh.
83 | ^жарина Ну відсунувся. Але вогонь я запам'ятав. Він ше своє отримає. | ^жарина I moved back. But I'll remember the fire.
84 | ^нудно_в Уві сні — то не те, шо наживо. Ну добре, хай буде. | ^нудно_в In a dream it's not the same. Oh, fine.
85 | ^смажене_в Чемно? Я? Ну… спробую. Дай, бля, шматок. Будь ласка. | ^смажене_в Politely? Me? Well, I'll try.
86 | ^ворони_в Пощастило, ага. Тіко не звикай, бо наврочиш. | ^ворони_в Lucky, fuck. Don't get used to it.

### H @х_кінець   (used by scenes: idle_b, fog, death, biome, fed_munin, gap_h, sail, cross_h, perch_a)
6 | ^туман Тримайся крила. І не каркай. | ^туман Stay by my wing. And don't croak.
36 | ^звуки Добре, що хоч твоє бурчання знайоме. | ^звуки Good that at least your grumbling is familiar.
37 | ^дорога Тоді йдемо далі. Обережно. | ^дорога Then we go on. Carefully.
59 | ^мряка_т Скоро розвіється. Мабуть. | ^мряка_т It'll clear soon. Probably.
60 | ^силует Підійдемо — побачимо. Обережно. | ^силует We'll get closer and see. Carefully.
61 | ^голос Туман любить пастки з голосами. Тихо. | ^голос Fog loves traps with voices. Quiet.
62 | ^тримайся Гаразд. Аби не відставав. | ^тримайся Fine. As long as you don't fall behind.
63 | ^вогник Не підемо. Хай світить сам. | ^вогник We won't go. Let it shine by itself.
64 | ^тиша_т Мені подобається. Тихо, як у пір'ї. | ^тиша_т I like it. Quiet, like inside feathers.
65 | ^мокро Злітати буде важко. Сидимо. | ^мокро It'll be hard to take off. We'll sit.

### H @х_вогонь   (used by scenes: rest_a)
0 | ^спогад Пам'ятаєш, Муніне, як ми вперше сіли на це плече? | ^спогад Remember, Munin, when we first sat on this shoulder?
1 | ^спогад Пам'ятаєш нашу першу зиму? Ти тоді майже змерз. | ^спогад Remember our first winter? You nearly froze back then.
2 | ^вогонь Тепло. Можна й подрімати. | ^вогонь Warm. A good time for a nap.
3 | ^вогонь Колись Одін питав, що ми бачили. Сьогодні я б розповів про вогонь. | ^вогонь Odin used to ask what we saw. Today I'd tell him about the fire.
4 | ~1 ^дражню Не лізь до полум'я, Муніне, ти й так смажений на голову. | ~1 ^дражню Don't go near the flames, Munin, your head's fried enough already.
5 | ~3 ^вогонь Знаєш, мені тут подобається. Не кажи нікому. | ~3 ^вогонь You know, I like it here. Don't tell anyone.
6 | ^казка Розкажу тобі казку, Муніне. Жив собі ворон, і був він мудрий. Здогадайся, хто. | ^казка I'll tell you a tale, Munin. Once there was a raven, and he was wise. Guess who.
7 | ^іскри Глянь, як іскри летять. Наче світлячки. | ^іскри Look how the sparks fly. Like fireflies.
8 | ^завтра Завтра знову в дорогу. Цікаво, куди {name} нас поведе. | ^завтра Tomorrow we're on the road again. I wonder where {name} will lead us.
9 | ^зірки_в Дивись, як зорі над вогнем мерехтять. | ^зірки_в Look how the stars flicker above the fire.
10 | ^мрія_в Знаєш, про що я мрію? Про тихий дах і повну миску. | ^мрія_в You know what I dream of? A quiet roof and a full bowl.
11 | ^одін_в Цікаво, Одін зараз теж біля вогню сидить? | ^одін_в I wonder if Odin is sitting by a fire right now too.
12 | ^пісня_в Заспівати тобі, Муніне? Тихенько. | ^пісня_в Shall I sing for you, Munin? Quietly.
13 | ^день_в Довгий був день. Добре, що закінчився біля вогню. | ^день_в It was a long day. Good that it ended by the fire.

### M @м_вогонь   (used by scenes: rest_a)
0 | ^спогад Пам'ятаю, ага. Ти з плеча гепнувся. Двічі, {м_слівце}. Я ржав. | ^спогад I remember. You fell off the shoulder. Twice, {м_слівце}.
1 | ^спогад Аякже. Ти верещав, як курча недорізане. | ^спогад You bet. You were squealing like a chick.
2 | ^спогад Пам'ятаю. Ти тоді шмарклі розпустив, як мала дитина. | ^спогад I remember. You burst into tears back then, like an old woman.
3 | ^вогонь Тепло, {м_слівце}. Як у вовка в пузі. | ^вогонь Warm as a wolf's belly, {м_слівце}.
4 | ^вогонь Я б тут і заночував, {м_слівце}. Назавжди. Хай нас усі нахуй забудуть. | ^вогонь I'd spend the night here. Forever, {м_слівце}.
5 | ^вогонь Спи, ботан. Я посторожу. Мо'. Як не засну. | ^вогонь Sleep, wiseguy. I'll keep watch. Maybe.
6 | ^дражню Сам ти засмажений, {м_слівце}. На голову. | ^дражню You're the fried one, {м_слівце}.
7 | ^дражню Я до вогню лізу, бо від твоїх жартів аж мороз по пір'ю. | ^дражню I get close to the flames because your jokes leave me cold.
8 | ~3 ^спогад Пам'ятаю. Класні були часи, {м_слівце}. І зара, бля, нічо так. | ~3 ^спогад I remember. Good times, {м_слівце}. And now isn't bad either.
9 | ^казка Та я вже допер, чим кінчиться. Казка — гівно, {м_слівце}. | ^казка I guessed. The tale is shit, {м_слівце}.
10 | ^казка А тоді прилетів інший ворон і дав тому розумнику в дзьоб. Кінець. Гарна казка. | ^казка And then another raven flew in and punched the wise one in the beak. The end.
11 | ^іскри Світлячки? То жар, дурню. Сунь туди дзьоба — сам дізнаєшся. | ^іскри Fireflies? Those are embers, wiseguy. Stick your beak in — you'll find out.
12 | ^іскри Гарно, {м_слівце}. Аби тіко на хвіст мені не сіли. | ^іскри Pretty, {м_слівце}. As long as they don't land on my tail.
13 | ^завтра Куди поведе — там комусь і наб'ємо пику. Просто. | ^завтра Wherever {he|she} leads — there we'll punch someone in the face.
14 | ^завтра Та хоч у Гель. Аби там годували. | ^завтра Even to Hel. As long as there's grub.
15 | ^зірки_в Мерехтять, бо дим, романтик ти пернатий. | ^зірки_в They flicker because of the smoke, romantic.
16 | ^зірки_в Гарно, бля. Мовчи. Не псуй. | ^зірки_в Beautiful, fuck. Shut up.
17 | ^мрія_в А я — про махач і повну миску. Ну і медовухи бочку. | ^мрія_в And I dream of war and a full bowl.
18 | ^мрія_в Миска — то добре. А дах — для ссикунів. | ^мрія_в A bowl is good. A roof is for weaklings.
19 | ^одін_в Сидить і бухає. Як завжди, старий. | ^одін_в Sitting and drinking. As always.
20 | ^одін_в Сидить і про нас думає. Ну, про мене. Про тебе шо там думати. | ^одін_в Sitting and thinking about us. Well, about me.
21 | ^пісня_в Тіко спробуй, {м_слівце}. Тіко рота відкрий. | ^пісня_в Just try it, {м_слівце}.
22 | ^пісня_в Співай. Я засну — і всім буде краще. | ^пісня_в Sing. I'll fall asleep — and that'll be better.
23 | ^день_в Довгий і нудний, бляха. Але вогонь — норм вогонь. | ^день_в Long and boring. But the fire is decent.
24 | ^день_в Закінчився, бо я заїбався, {м_слівце}. | ^день_в It ended because I got tired, {м_слівце}.

### H @х_тепло   (used by scenes: rest_a)
0 | ^спогад Добре згадувати разом. Навіть з тобою. | ^спогад It's good to remember together. Even with you.
1 | ^спогад Колись і про цей вечір згадаємо. | ^спогад Someday we'll remember this evening too.
2 | ^вогонь Вогонь гріє. Навіть тебе, Муніне. | ^вогонь The fire warms. Even you, Munin.
3 | ^вогонь Посидимо ще трохи. Звідси гарно видно зорі. | ^вогонь Let's sit a little longer. You can see the stars well from here.
4 | ~1 Не крути хвостом біля вогню, бо підсмажишся. | ~1 Don't wag your tail near the fire, or you'll get roasted.
5 | ~23 Добре вечоріти разом. | ~23 It's good to spend the evening together.
6 | ^казка Кінець неправильний. Але смішний. | ^казка The ending is wrong. But funny.
7 | ^завтра Головне — разом. Решта знайдеться. | ^завтра What matters is being together. The rest will turn up.
8 | ^іскри Обережно з хвостом. Він у тебе один. | ^іскри Careful with your tail. You only have one.
9 | ^зірки_в Посидимо, поки не згасне. | ^зірки_в Let's sit until it goes out.
10 | ^мрія_в Миска — наша спільна мрія. Хоч щось. | ^мрія_в A bowl is our shared dream. At least something.
11 | ^одін_в Хай йому буде тепло. Як нам. | ^одін_в May he be warm. Like us.
12 | ^пісня_в Гаразд. Мовчу. Але пісня була б гарна. | ^пісня_в Fine. I'll be quiet. But it would have been a nice song.
13 | ^день_в Завтра буде новий. Відпочивай. | ^день_в Tomorrow will be a new one. Rest.

### M @м_вогонь_почин   (used by scenes: rest_b)
0 | ^пиво Хугіне, ану розкажи, як ти тоді в пиво бовтнувся, {м_слівце}. Гиги. | ^пиво Hugin, tell about that time you fell into the beer, {м_слівце}.
1 | ^риба А пам'ятаєш, як ми три дні за рибу гризлись? Ти тоді гівнюк був. І зара гівнюк. | ^риба And remember how we argued over a fish for three days? You were a shithead.
2 | ^спокій Сидимо. Ні троля, ні бійки. Аж лячно, {м_слівце}. | ^спокій Here we sit. No troll, no fight. Downright suspicious, {м_слівце}.
3 | ~1 ^спів Тіко заспіваєш — я собі вуха в багатті спалю. Чесно. | ~1 ^спів If you start singing, I'll burn my ears off.
4 | ~3 ^подобається Добре тут, {м_слівце}. Ну. Подобається мені. Не ржи, бо дзьобну. | ~3 ^подобається It's good here, {м_слівце}. I like it. Don't laugh.
5 | ^шрам Хугіне, а шо то в тебе за шрам на лапі? Тіко не пизди, шо від дракона. | ^шрам Hugin, where did you get that scar on your foot? Just don't lie that it was a dragon.
6 | ^мед Зара б медовухи, {м_слівце}. Цілий ріг. Нє, два. | ^мед Some mead right now, {м_слівце}. A whole horn.
7 | ^дим Дим в очі, бля. Спеціально на мене тягне, падлюка. | ^дим Smoke in my eyes. It drifts at me on purpose, the bitch.
8 | ^історія Хугін, розкажи шось. Тіко не про мудрість, {м_слівце}, бо засну. | ^історія Hugin, tell me something. Just not about wisdom, {м_слівце}.
9 | ^жарина Жарина вистрибнула! Мало хвоста мені не підсмалила, курва. | ^жарина An ember jumped out. Nearly set my tail on fire.
10 | ^нудно_в Нудно, {м_слівце}. Пішли когось уб'ємо, га? | ^нудно_в Boring, {м_слівце}. Let's kill someone.
11 | ^смажене_в Смаженим пахне. Нам шось лишиться чи як? | ^смажене_в Smells of roast meat. Will there be any left for us?
12 | ^ворони_в А інші ворони зара теж біля вогню? Чи мерзнуть десь, бідолахи? | ^ворони_в Are other ravens by a fire right now too? Or freezing?

### H @х_вогонь_відповідь   (used by scenes: rest_b)
0 | ^пиво Я не падав у пиво. Я його досліджував. | ^пиво I didn't fall into the beer. I was studying it.
1 | ^пиво Це було медове вино. І це було мистецтво. | ^пиво It was mead wine. And it was art.
2 | ^риба Риба була моя. Я її перший побачив. | ^риба The fish was mine. I saw it first.
3 | ^риба Три дні? Чотири. І я переміг. | ^риба Three days? Four. And I won.
4 | ^спокій Не каркай. Хай буде спокійно хоч раз. | ^спокій Don't croak. Let it be calm for once.
5 | ^спокій Підозріло — це коли ти мовчиш. | ^спокій Suspicious is when you're quiet.
6 | ^спів Я співаю краще за тебе. Хоч це й не важко. | ^спів I sing better than you. Though that's not hard.
7 | ^подобається Не сміюся. Мені теж добре. | ^подобається I'm not laughing. I feel good too.
8 | ^шрам Від дракона. А що, не віриш? | ^шрам From a dragon. What, you don't believe me?
9 | ^шрам Від тебе. Пам'ятаєш, як ти спросоння клюнув? | ^шрам From you. Remember how you pecked me half asleep?
10 | ^мед Ріг меду — і ти заснеш на моєму хвості. Ні. | ^мед A horn of mead and you'll fall asleep on my tail. No.
11 | ^мед Мед — для тих, хто заслужив. Тобі — вода. | ^мед Mead is for those who've earned it. Water for you.
12 | ^дим Дим іде на найгарнішого. Не знав? | ^дим Smoke goes to the handsomest. Didn't you know?
13 | ^дим Пересядь. Хоча дим тебе знайде. Він теж тебе не любить. | ^дим Move. Though the smoke will find you. It doesn't like you either.
14 | ^спів Не співатиму. Хоча хотілося. | ^спів I won't sing. Though I wanted to.
15 | ~3 ^подобається І мені. Посидимо ще трохи. | ~3 ^подобається Me too. Let's sit a little longer.
16 | ^історія Тоді про тебе. Там мудрості нема. | ^історія Then about you. There's no wisdom there.
17 | ^історія Розкажу про ворона, що мовчав. Вигадка, звісно. | ^історія I'll tell about a raven who kept quiet. Fiction, of course.
18 | ^жарина Відсунься трохи. Хвіст у тебе один. | ^жарина Move back a little. You only have one tail.
19 | ^жарина Вогонь тебе теж не любить. Не ображайся. | ^жарина The fire doesn't like you either. Don't be offended.
20 | ^нудно_в Біля вогню не вбивають. Біля вогню відпочивають. | ^нудно_в Nobody kills by the fire. By the fire we rest.
21 | ^нудно_в Спи. Уві сні вбивай скільки хочеш. | ^нудно_в Sleep. In your dreams kill as many as you like.
22 | ^смажене_в Лишиться. Якщо чемно попросиш. | ^смажене_в There'll be some left. If you ask politely.
23 | ^смажене_в {name} поділиться. Як завжди. | ^смажене_в {name} will share. As always.
24 | ^ворони_в Хтось біля вогню. Хтось ні. Нам пощастило. | ^ворони_в Some sit by a fire. Some don't. We got lucky.
25 | ^ворони_в Чужі ворони — чужий клопіт. | ^ворони_в Other ravens are other people's trouble.

### M @м_дощ   (used by scenes: rain)
0 | ^скарга Пір'я — як ганчірка, шо підлогу мили. | ^скарга My feathers are like a rag already.
1 | ^скарга Я мокрий, як хвіст у риби. Нє, ше мокріший. | ^скарга I'm wet as a water spirit's ass.
2 | ^скарга Ше трохи — і я качка, {м_слівце}. Кря, бля. | ^скарга A little more and I'm a duck, {м_слівце}.
3 | ^скарга Хто, бля, цей дощ замовив? Знайду — заклюю. | ^скарга Who ordered this rain? I'll find them and peck them to fucking death.
4 | ^скарга З неба ссуть, а ми сидимо й мовчимо. Терпіли. | ^скарга They're pissing from the sky, and we keep quiet.
5 | ~1 ^скарга Ненавиджу воду. І тебе. Тебе більше. | ~1 ^скарга I hate water. And you.
6 | ^калюжі Хлюпає під лапами, {м_слівце}. На плечі! Як таке взагалі? | ^калюжі It's squelching under my feet, {м_слівце}. Even on the shoulder.
7 | ^сохнути Де сохнути будемо, га? Я питаю: де, бля?! | ^сохнути Where are we going to dry off? I'm asking, where?!

### H @х_дощ   (used by scenes: rain)
0 | ^скарга Дощ змиває сліди. І дурні думки. | ^скарга Rain washes away tracks. And silly thoughts.
1 | ^скарга Тихіше, від тебе вода тече мені за комір. | ^скарга Quieter, water is running off you down my collar.
2 | ^скарга Мокрий ворон — сумне видовище. Ти — особливо. | ^скарга A wet raven is a sad sight. You especially.
3 | ~1 ^скарга Не ний. Ти не з цукру. | ~1 ^скарга Don't whine. You're not made of sugar.
4 | ~3 ^скарга Тримайся ближче, так тепліше. | ~3 ^скарга Stay closer, it's warmer that way.
5 | ^калюжі Хлюпає в тебе в голові. На плечі сухо. | ^калюжі It's sloshing in your head. The shoulder is dry.
6 | ^калюжі Не тупцюй. Бризкаєш. | ^калюжі Stop stomping. You're splashing.
7 | ^сохнути Біля вогню. Якщо {name} його розпалить. | ^сохнути By the fire. If {name} lights one.
8 | ^сохнути На вітрі. Як білизна. Розвісимо тебе на гілці. | ^сохнути In the wind. Like laundry. We'll hang you on a branch.
9 | ^скарга Дощ скінчиться. Твоє бурчання — ні. | ^скарга The rain will end. Your grumbling won't.
10 | ~2 ^скарга Перетерпи. Мокрі — не мертві. | ~2 ^скарга Bear it. Wet isn't dead.

### M @м_дощ_кінець   (used by scenes: rain)
0 | ^скарга Філософ знайшовся, {м_слівце}. | ^скарга A philosopher, {м_слівце}.
1 | ^скарга Сам ти мудрий. Мокрий, {м_слівце}, і мудрий. Тьху. | ^скарга You're the wise one. Wet, {м_слівце}, and wise.
2 | ^скарга Ну… мо' й так. Але все одно хуйня. | ^скарга Well… maybe so. But it's still bullshit.
3 | ~3 ^скарга Добре, ботан, мовчу. Тіко мокро, {м_слівце}. | ~3 ^скарга Fine, wiseguy, I'm quiet. Wet, {м_слівце}.
4 | ^калюжі Бризкаю, бо можу. І шо ти мені зробиш? | ^калюжі I splash because I can.
5 | ^сохнути Тіко сунься, {м_слівце}. Тіко сунься. | ^сохнути Just try it, {м_слівце}.
6 | ^скарга Скінчиться, ага. Як і ти колись, професоре. | ^скарга It'll end. Like you someday.

### H @х_туман   (used by scenes: fog)
0 | ^туман Нічого не бачу. А я, між іншим, бачу все. | ^туман I can't see anything. And I, by the way, see everything.
1 | ^туман Туман. Добре для засідок. Погано для нас. | ^туман Fog. Good for ambushes. Bad for us.
2 | ^туман У такому тумані навіть Одін заблукав би. | ^туман In fog like this even Odin would get lost.
3 | ~1 ^туман Муніне, якщо загубишся, я шукати не буду. | ~1 ^туман Munin, if you get lost, I won't look for you.
4 | ~3 ^туман Тримайся поруч. Не люблю, коли тебе не видно. | ~3 ^туман Stay close. I don't like it when I can't see you.
5 | ^звуки Чуєш? Кроки. Чи то в тебе в животі. | ^звуки Hear that? Footsteps. Or your stomach.
6 | ^дорога Ми ще на дорозі? Бо я вже не певен. | ^дорога Are we still on the road? Because I'm no longer sure.
7 | ^мряка_т Туман лізе в пір'я. Холодний, як рука драуґра. | ^мряка_т The fog creeps into my feathers. Cold as a draugr's hand.
8 | ^силует Он силует. Дерево чи троль? | ^силует There's a silhouette. A tree or a troll?
9 | ^голос Хтось кличе в тумані. Не відповідай. | ^голос Someone's calling in the fog. Don't answer.
10 | ^тримайся Тримаймося ближче. Тут легко розгубитися. | ^тримайся Let's stay closer. It's easy to get separated here.
11 | ^вогник Бачиш вогник? Там, у сірому. | ^вогник See a light? There, in the gray.
12 | ^тиша_т Туман глушить звуки. Навіть твою лайку. | ^тиша_т The fog muffles sounds. Even your swearing.
13 | ^мокро Туман осідає на пір'я. Скоро будемо важкі, як каміння. | ^мокро The fog settles on our feathers. Soon we'll be heavy as stones.

### M @м_туман   (used by scenes: fog)
0 | ^туман Все бачиш? Ну то скажи, де я, професоре. Ну? Ну? | ^туман See everything? Then tell me where I am, wiseguy.
1 | ^туман Засідки? Та я сам засідка, {м_слівце}. Ходяча. | ^туман Ambushes? I'm an ambush myself, {м_слівце}.
2 | ^туман Одін би заблукав. А ти вже, бля, заблукав. | ^туман Odin would get lost, and you're already lost.
3 | ^туман Ніхуя не видно. Саме то, шоб когось клюнути з-за рогу. | ^туман Can't see fuck-all. Perfect for pecking someone.
4 | ~1 ^туман Не шукатимеш? Та я тебе сам знайду. І дзьобну. | ~1 ^туман You won't look for me? I'll find you anyway. And bite.
5 | ~3 ^туман Та тут я, тут. Не скигли, засранцю, бо стидно. | ~3 ^туман I'm here, here. Don't whine, you little shit.
6 | ^звуки То в мене в пузі. Я голодний, а ти — кроки, кроки. | ^звуки My stomach. I'm hungry, and you're on about footsteps again.
7 | ^звуки Кроки. Здоровенні. Ну все, {м_слівце}, зара буде бійка. | ^звуки Footsteps. Big ones. That's it, {м_слівце}, there'll be a fight.
8 | ^дорога На дорозі, де ж ше. Он камінь, он кущ. Чи то троль. Хуй його знає. | ^дорога On the road. There's a rock, there's a bush. Or a troll.
9 | ^дорога Дорога — там, куди {name} пре. Хоч у болото. | ^дорога The road is wherever {name} goes. Even into a swamp.
10 | ~2 ^туман Нічо не видно. Зате й нас не видно. Чесно. | ~2 ^туман Little to see. But nobody sees us either.
11 | ^мряка_т Та краще б драуґр, {м_слівце}. Йому хоч у пику дати можна. А туман шо? | ^мряка_т A draugr would at least be something. But fog is boring, {м_слівце}.
12 | ^мряка_т Холодно, бля. Ну… притулися, чи шо. Тіко без телячих ніжностей. | ^мряка_т Cold, fuck. Snuggle up or something.
13 | ^силует Троль, сто пудів, {м_слівце}. Я печінкою чую. Готуй дзьоба. | ^силует A troll. I can feel it, {м_слівце}. Ready your beak.
14 | ^силует Та дерево. Але я б дзьобнув. Хай знає. | ^силует A tree. But I'd peck it just in case.
15 | ^голос Та я вже відповів. Матюком. Трьохповерховим. | ^голос I already answered. With a curse.
16 | ^голос Кличе? То я йому зара так гукну, {м_слівце}, шо сам прибіжити. | ^голос It's calling? Then I'll call back, {м_слівце}.
17 | ^тримайся Я й так на плечі, ало. Ближче — тіко в пазуху. | ^тримайся I'm on the shoulder anyway. Can't get any closer.
18 | ^тримайся Розгубимось — знайду тебе по запаху. Ти смердиш книжками, ботан. | ^тримайся If we get separated — I'll find you by smell. You stink of wisdom.
19 | ^вогник Ну бачу. Пішли туди, хай там хоч дідько з рогами сидить. | ^вогник I see it. Let's go, even if it's hell over there.
20 | ^вогник То болотний вогник, {м_слівце}. Дурнів заманює. Тебе, наприклад. Гиги. | ^вогник That's a will-o'-the-wisp, {м_слівце}. It lures fools. You, for example.
21 | ^тиша_т Нічо не глушить. Слухай сюди: КУРВА. | ^тиша_т It doesn't muffle. Listen: FUCK.
22 | ^тиша_т Глушить, бляха. Мені це не подобається, чуєш? | ^тиша_т It muffles, fuck. I don't like it.
23 | ^мокро Я важкий не від туману. Я від злості важкий. | ^мокро I'm already heavy. From anger.
24 | ^мокро Обтрусимось. Як пси. Шо тут думати. | ^мокро We'll shake it off. Like dogs.

### M @м_ніч   (used by scenes: night)
0 | ^бійка Ніч. Саме час комусь напихати, {м_слівце}. | ^бійка Night. The best time for a fight, {м_слівце}.
1 | ^темно Темно, як у тебе в голові, ботан. | ^темно Dark as your head.
2 | ^кістка Чуєш? Десь кістка хрумтить. Аж слина, {м_слівце}. | ^кістка I hear a bone crunching somewhere. Appetizing, {м_слівце}.
3 | ~1 ^сховаюсь Як хтось вилізе з кущів — я за тебе сховаюсь. А шо такого? | ~1 ^сховаюсь If someone climbs out of the bushes, I'll hide behind you.
4 | ~3 ^спокій Посидь тихо, {м_слівце}. Ніч гарна. Не псуй. | ~3 ^спокій Sit quietly, {м_слівце}. A beautiful night.
5 | ^вовки Чуєш вовків? Виють. Ше й краще за тебе, {м_слівце}. | ^вовки Hear the wolves? Singing. Better than you, {м_слівце}.
6 | ^очі Хугіне, з кущів хтось зирить. Двоє очей. Нє, четверо. Бля. | ^очі Hugin, someone in the bushes is looking at us. Two eyes. No, four.
7 | ^зорі_н Зорі повилазили, {м_слівце}. Як блохи на собаці. Аж небо чухається. | ^зорі_н The stars are out, {м_слівце}. Like fleas on a dog.
8 | ^сова Сова верещить. От би їй голову відкрутити, падлюці. | ^сова An owl is hooting. I want to twist its head off.
9 | ^місяць Шо вирячився, місяцю? Га? Тебе питаю, пико лиса. | ^місяць The moon is staring. What are you gawking at, bastard?
10 | ^холод_н Холодно вночі, {м_слівце}. Лапи задубіли нахуй. | ^холод_н It's cold at night, {м_слівце}. My feet are freezing.
11 | ^шурхіт Шось шурхотить у траві. Дзьобнути, чи шо? | ^шурхіт Something is rustling in the grass. Shall I peck it?
12 | ^сон_н Хугін, ти спиш? Не спи, бо мені нудно, курва. | ^сон_н Are you asleep, Hugin? Don't sleep, it's boring.
13 | ^драуґри Вночі драуґри вилазять. Хочу одному в гниле око дзьобом. Хрясь! | ^драуґри At night the draugr crawl out. I want to peck one in its rotten eye.

### H @х_ніч   (used by scenes: night)
0 | ^бійка Тобі аби битися. А ніч для думок. | ^бійка All you want is a fight. Night is for thinking.
1 | ^бійка Бийся сам. Я посвічу очима. | ^бійка Fight alone. I'll light the way with my eyes.
2 | ^темно Темно в мене в голові? Принаймні вона в мене є. | ^темно Dark in my head? At least I have one.
3 | ^кістка Це вовк гризе. Сподіваюсь, не нашого господаря. | ^кістка That's a wolf gnawing. Hopefully not on our master.
4 | ^сховаюсь За мене не ховайся. Я тонкий. | ^сховаюсь Don't hide behind me. I'm thin.
5 | ^спокій Добре. Помовчимо. Вночі ліс говорить сам. | ^спокій Fine. Let's be quiet. At night the forest talks by itself.
6 | ^бійка Бійка вночі — для тих, хто бачить у темряві. Тобто не для тебе. | ^бійка Fighting at night is for those who see in the dark. So, not for you.
7 | ^темно Темно — то не страшно. Страшно, коли ти щось задумав. | ^темно Darkness isn't scary. What's scary is when you've got something in mind.
8 | ^кістка То не кістка. То {name} хрумтить сухарем. | ^кістка That's not a bone. That's {name} crunching a rusk.
9 | ^кістка Апетитно? Ти хворий. | ^кістка Appetizing? You're sick.
10 | ~1 ^сховаюсь Ховайся. Я потім усім розкажу. | ~1 ^сховаюсь Hide. I'll tell everyone later.
11 | ~3 ^спокій Гарна. Тиха. Як ти, коли спиш. | ~3 ^спокій Beautiful. Quiet. Like you when you sleep.
12 | ^вовки Вовки співають, бо голодні. Як ти. Тільки ти не співаєш, а скиглиш. | ^вовки Wolves sing because they're hungry. Like you. Only you don't sing, you whine.
13 | ^вовки Хай співають. Аби не підходили. | ^вовки Let them sing. As long as they don't come closer.
14 | ^очі Четверо — то двоє звірів. Або один дуже здивований. | ^очі Four — that's two beasts. Or one very surprised one.
15 | ^очі Світлячки, Муніне. Не будь курчам. | ^очі Fireflies, Munin. Don't be a chicken.
16 | ^зорі_н Як блохи? Ти романтик, Муніне. | ^зорі_н Like fleas? You're a romantic, Munin.
17 | ^зорі_н Це зорі, а не блохи. Подивись уважніше. | ^зорі_н Those are stars, not fleas. Look closer.
18 | ^сова Сова — нічний сторож. Не чіпай. | ^сова The owl is the night watchman. Leave it alone.
19 | ^сова Сова мудра. Тому ти її й не любиш. | ^сова The owl is wise. That's why you don't like it.
20 | ^місяць Місяць світить, щоб ти не заблукав. | ^місяць The moon shines so you don't get lost.
21 | ^місяць Він на всіх так дивиться. Не бери до серця. | ^місяць It looks at everyone like that. Don't take it to heart.
22 | ^холод_н Притулися до шиї. Там тепліше. | ^холод_н Snuggle up to the neck. It's warmer there.
23 | ^холод_н Біля вогню сядемо. Скоро. | ^холод_н We'll sit by the fire. Soon.
24 | ^шурхіт Миша. Не дзьобай, вона тобі нічого не зробила. | ^шурхіт A mouse. Don't peck it, it hasn't done anything to you.
25 | ^шурхіт Може, змія. Обережно. | ^шурхіт Maybe a snake. Careful.
26 | ^сон_н Не сплю. Думаю. | ^сон_н I'm not sleeping. I'm thinking.
27 | ^сон_н Ворон уночі пильнує. Я пильную. | ^сон_н A raven keeps watch at night. I'm keeping watch.
28 | ^драуґри Гниле око? Смачного. | ^драуґри A rotten eye? Enjoy.
29 | ^драуґри Хай лізуть. Ми далеко від болота. | ^драуґри Let them come. We're far from the swamp.

### M @м_сніг   (used by scenes: snow)
0 | ^мерзну Мерзну так, шо пір'я дзвенить, {м_слівце}. | ^мерзну I'm so cold my feathers are ringing, {м_слівце}.
1 | ^комір Сніг за комір. Коміра в мене нема, а сніг є. Як так, бля? | ^комір Snow down the collar. I don't have a collar, but there is snow.
2 | ^сніг Все біле. Ненавиджу біле. Тьху. | ^сніг Everything's white. I hate white.
3 | ^лапи Лапи до плеча примерзли, {м_слівце}. Все, я тут навіки. | ^лапи My feet are frozen to the shoulder, {м_слівце}. Now I'm here forever.
4 | ^сніжинка Сніжинка мені на дзьоб сіла. Нахабна, сука. Зневажає. | ^сніжинка A snowflake fell on my beak. It despises me.
5 | ^стежка В снігу всі сліди видно. І наші, {м_слівце}, теж. | ^стежка In the snow all tracks are visible. Ours too, {м_слівце}.
6 | ^тепло Де вогонь, бля? Мені вогонь треба, а не твоя мудрість. | ^тепло Where's the fire? I need fire, not your wisdom.
7 | ^білі Тут вовки, {м_слівце}. Чую. Білі й злі, як теща. | ^білі There are wolves here, {м_слівце}. I can hear them. White and angry.
8 | ^бурулька В мене на хвості бурулька. Я тепер, бля, зброя. | ^бурулька There's an icicle on my tail. I'm a weapon now.
9 | ^глухо Сніг усе глушить. Навіть як я матюкаюсь. | ^глухо The snow muffles everything. Even my swearing.
10 | ^гори Гори, сніг, дубак. Хто тут, бля, живе по своїй волі? | ^гори Mountains, snow, cold. Who lives here voluntarily?
11 | ^кожух {name} в хутрі, а я в пір'ї. Де справедливість, га? | ^кожух {name} is in fur, and I'm in feathers. Unfair.

### H @х_сніг   (used by scenes: snow)
0 | ^мерзну Притулися ближче. Тільки не кусайся. | ^мерзну Huddle closer. Just don't bite.
1 | ^мерзну Дзвенить — бо пусте всередині. | ^мерзну It rings because it's empty inside.
2 | ^комір Немає коміра — немає проблем. | ^комір No collar — no problems.
3 | ^сніг Красиво ж. Хоч раз подивись. | ^сніг It's beautiful. Look for once.
4 | ^комір Заховай дзьоб у пір'я. Мудрі так роблять. | ^комір Tuck your beak into your feathers. The wise do that.
5 | ^сніг Біле — бо чисте. Тобі незнайоме. | ^сніг White because it's clean. Unfamiliar to you.
6 | ^лапи Назавжди? Тоді мені треба інше плече. | ^лапи Forever? Then I need another shoulder.
7 | ^лапи Не вовтузься. Відмерзнуть. | ^лапи Stop fidgeting. They'll thaw.
8 | ^сніжинка Вона просто втомилася. Як усі, хто поруч із тобою. | ^сніжинка It's just tired. Like everyone near you.
9 | ^сніжинка Сніжинка не вміє зневажати. На відміну від тебе. | ^сніжинка A snowflake can't despise. Unlike you.
10 | ^стежка Тому йдемо обережно. | ^стежка That's why we walk carefully.
11 | ^стежка Зате видно, хто за нами йде. | ^стежка But we can see who's following us.
12 | ^тепло Мудрість теж гріє. Зсередини. | ^тепло Wisdom warms too. From the inside.
13 | ^тепло Скоро. {name} вже шукає, де розпалити. | ^тепло Soon. {name} is already looking for a place to light a fire.
14 | ^білі Злі, як ти. Тільки чесніші. | ^білі Angry, like you. Only more honest.
15 | ^білі Тихо. Не кличмо їх. | ^білі Quiet. Let's not call them.
16 | ^бурулька Найнебезпечніша зброя Мідґарду. Для тебе самого. | ^бурулька The most dangerous weapon in Midgard. For yourself.
17 | ^бурулька Не махай нею. Відламаєш. | ^бурулька Don't wave it around. You'll break it off.
18 | ^глухо Нарешті щось корисне від снігу. | ^глухо Finally something useful from snow.
19 | ^глухо Тиша в снігу — найгарніша тиша. | ^глухо Silence in snow is the most beautiful silence.
20 | ^гори Ті, хто міцніший за тебе. | ^гори Those tougher than you.
21 | ^гори Кози. І трохи вовків. І ми зараз. | ^гори Goats. And some wolves. And us, right now.
22 | ^кожух Пір'я тепліше, ніж здається. | ^кожух Feathers are warmer than they seem.
23 | ^кожух Сховайся під комір. Там тепло. | ^кожух Hide under the collar. It's warm there.

### M @м_сніг_кінець   (used by scenes: snow)
0 | ^мерзну Пусте? Там вогонь, {м_слівце}. Ледь жевріє, курва. | ^мерзну Empty? There's a fire in there, {м_слівце}. Barely smoldering.
1 | ^комір Великий філософ. Мерзлий. Хе. | ^комір A great philosopher. A frozen one.
2 | ^сніг Глянув. Біле. Ненавиджу. | ^сніг I looked. White. I hate it.
3 | ^лапи Інше плече? Я тобі дам інше плече, хитрий який. | ^лапи Another shoulder? I'll give you another shoulder.
4 | ^комір Мудрі мерзнуть так само. Тіко мовчки. Як дурні. | ^комір The wise freeze just the same. Only silently.
5 | ^сніг Чисте? Почекай, я зара на нього насру. | ^сніг Clean? Wait, I'll shit on it.
6 | ^сніжинка А я її зжер. Хай знає, з ким має діло. | ^сніжинка I ate it. Let it know.
7 | ^стежка Хто за нами припреться — тому дзьобом в тім'я. | ^стежка Whoever follows — I'll peck.
8 | ^тепло Скоро, скоро… А дубака я вже зара ловлю. | ^тепло Soon, soon. And I'm freezing right now.
9 | ^білі Хай лізуть. Я теж білий стану. Від злості, бля. | ^білі Let them come. I'll be white too. With rage.
10 | ^бурулька Відламаю й встромлю тобі. По-братськи. | ^бурулька I'll break it off — and stick it in you. In a friendly way.
11 | ^глухо Ага? Ну слухай: КУРВА! О. Не заглушило. | ^глухо Then listen: FUCK. It didn't muffle that.
12 | ^гори Та хай живуть. А я хочу додому. Додому, чуєш? | ^гори Let them live. And I want to go home.
13 | ^кожух Пиздиш. Але під комір залізу. | ^кожух You're lying. But I'll crawl under the collar.

### H @х_гроза   (used by scenes: storm)
0 | ^грім Грім. Тор знову п'яний. | ^грім Thunder. Thor is drunk again.
1 | ^блискавка Якщо вдарить блискавка — то в тебе. Ти чорніший. | ^блискавка If lightning strikes — it'll be you. You're blacker.
2 | ^небо Небо сердиться. Не на нас, сподіваюсь. | ^небо The sky is angry. Not at us, I hope.
3 | ^вітер Вітер такий, що пір'я виє. Тримайся, Муніне. | ^вітер The wind is so strong my feathers howl. Hold on, Munin.
4 | ^молот Чуєш? То Мйольнір. Тор когось лупить. | ^молот Hear that? That's Mjölnir. Thor is pounding someone.
5 | ^дерево Не сідай на високе дерево, Муніне. Сьогодні не той день. | ^дерево Don't sit on a tall tree, Munin. Today's not the day.
6 | ^злива Ллє так, ніби в Асгарді перекинули діжку. | ^злива It's pouring as if someone tipped over a barrel in Asgard.
7 | ^страх Ти тремтиш, Муніне? Чи то вітер? | ^страх Are you trembling, Munin? Or is that the wind?
8 | ^рахувати Рахуй від спалаху до грому. Так знаєш, як далеко. | ^рахувати Count from the flash to the thunder. That's how you know how far it is.
9 | ^іскра Пір'я аж тріщить. Небо повне іскор. | ^іскра My feathers are crackling. The sky is full of sparks.
10 | ^тролі У грозу навіть тролі ховаються. Розумні тролі. | ^тролі In a storm even trolls hide. Smart trolls.
11 | ^після Після грози повітря чисте. Аж дихати хочеться. | ^після After a storm the air is clean. You really want to breathe.

### M @м_гроза   (used by scenes: storm)
0 | ^грім П'яний Тор — то найчесніший Тор. Наш пацан. | ^грім A drunk Thor is the most honest Thor.
1 | ^блискавка Я чорніший, а ти вищий. Тебе першого засмажити, ботан. | ^блискавка I'm blacker, but you're taller. You'll get fried first.
2 | ^небо Хай сердиться. Я теж сердитий, {м_слівце}. І шо? | ^небо Let it be angry. I'm angry too, {м_слівце}.
3 | ^грім Тор бухий — то й нам можна, {м_слівце}. | ^грім If Thor is drunk — then we can be too, {м_слівце}.
4 | ^блискавка Як у тебе вдарить — то хоч порозумнішаєш. | ^блискавка If it hits you — at least you'll get smart.
5 | ^небо Небо сердиться, бо ти йому набрид. Як і всім. | ^небо The sky is angry because it's sick of you.
6 | ^вітер Тримаюсь. За тебе. Шоб ти першим полетів. | ^вітер I'm holding on. To you. So you fly off first.
7 | ^вітер Виє? То я співаю, {м_слівце}. Шо, не подобається? | ^вітер Howling? That's me singing, {м_слівце}.
8 | ^молот Та хай лупить кого хоче. Аби не нас, {м_слівце}. | ^молот Let him pound. As long as it's not us, {м_слівце}.
9 | ^молот Ото б мене хоч раз на махач узяв. Я б йому показав, як треба. | ^молот If only he'd take me to a fight once.
10 | ^дерево Та я й не збирався, бля. Я на плечі сиджу, як цар. | ^дерево I wasn't going to. I'm on the shoulder, like a king.
11 | ^дерево Де хочу, там і сиджу. Хай тіко спробує вдарити, та блискавка. | ^дерево The tall tree is mine. Let it just try to strike.
12 | ^злива Яка нахуй діжка? То Одін рюмсає, шо ти в нього є. | ^злива That's not a barrel. That's Odin crying that he has you.
13 | ^злива Та хай ллє. Мокріше мене вже нема куди, {м_слівце}. | ^злива Let it pour. I can't get any wetter, {м_слівце}.
14 | ^страх Я не трушусь, бля. Я від злості трушусь. Це інше! | ^страх I'm not trembling. I'm vibrating with rage.
15 | ^страх Вітер, {м_слівце}. Тіко вітер. Ну і грім трохи. Відчепись. | ^страх The wind, {м_слівце}. Only the wind. And a bit of thunder.
16 | ^рахувати Раз, два… БАХ! Близько, сука! | ^рахувати One, two… BANG! Close, fuck.
17 | ^рахувати Рахувати? Я до трьох рахую, і то на кігтях. Зате матюкаюсь — як бог. | ^рахувати I can't count. I can swear.
18 | ^іскра То я від злості тріщу, а не від неба, ботан. | ^іскра I'm crackling with anger, not from the sky.
19 | ^іскра О, я тепер блищу. Красень, {м_слівце}, глянь! | ^іскра Oh, I'm shiny now. Handsome, {м_слівце}.
20 | ^тролі Тролі розумні? Ти шо, бля, грибів наївся? | ^тролі Trolls smart? Have you lost your mind?
21 | ^тролі А ми не ховаємось. Виходить, ми тупіші за тролів. Ну й похуй. | ^тролі And we're not hiding. So we're dumber than trolls.
22 | ^після От і дихай. А мені й так нормально смердить. | ^після Breathe. I'm fine with the stink as it is.
23 | ^після Після грози — гриби. А після грибів {name} співає. Жах, бля. | ^після After a storm — mushrooms. And after mushrooms — {name} sings.

### H @х_гроза_кінець   (used by scenes: storm)
0 | ^грім Ну от. Тепер і ти ображаєш богів. | ^грім There. Now you're insulting the gods too.
1 | ^блискавка Тоді я пригнусь. А ти стирчи. | ^блискавка Then I'll duck. And you stick out.
2 | ^небо Сховаймось. Мудрість — у сухості. | ^небо Let's take cover. Wisdom lies in dryness.
3 | ^вітер Співаєш ти гірше за вітер. | ^вітер You sing worse than the wind.
4 | ^небо Чуєш? Знову гримить. На тебе. | ^небо Hear that? Thunder again. At you.
5 | ^молот Тор б'є, кого хоче. Краще не потрапляй йому на очі. | ^молот Thor strikes whomever he likes. Better not catch his eye.
6 | ^дерево Сміливий ти, доки не загримить. | ^дерево You're brave until it thunders.
7 | ^злива Мокрі обидва. Хоч у чомусь рівні. | ^злива Both soaked. At least we're equal in something.
8 | ^страх Звісно. Я так і подумав. | ^страх Of course. That's what I thought.
9 | ^рахувати Тоді просто пригнись. Це вміють усі. | ^рахувати Then just duck. Everyone can do that.
10 | ^іскра Тріщи тихіше. Ще блискавку приманиш. | ^іскра Crackle more quietly. You'll attract lightning.
11 | ^тролі Розумніші за нас тролі. Це вже щось про нас каже. | ^тролі Trolls are smarter than us. That says something about us.
12 | ^після Дихай, Муніне. Хоч раз — свіжим. | ^після Breathe, Munin. For once — fresh air.

### H @х_світанок   (used by scenes: dawn)
0 | ^світає Світає. Ще один день — і ми живі. | ^світає Dawn. Another day — and we're alive.
1 | ^хропів Ранок. Ти хропів, як троль, Муніне. | ^хропів Morning. You snored like a troll, Munin.
2 | ^роса Роса на пір'ї. Добрий знак. | ^роса Dew on my feathers. A good sign.
3 | ^сонце Сонце встає, Муніне. Хоч хтось сьогодні рано встав. | ^сонце The sun is rising, Munin. At least someone got up early today.
4 | ^птахи Птахи співають. Світ прокидається. | ^птахи Birds are singing. The world is waking up.
5 | ^мряка Ранкова мряка над землею. Як молоко. | ^мряка Morning mist over the land. Like milk.
6 | ^сніданок Що на сніданок, Муніне? Знову чиєсь око? | ^сніданок What's for breakfast, Munin? Someone's eye again?
7 | ^асгард Мені наснився Асгард. А тобі що снилося? | ^асгард I dreamed of Asgard. What did you dream of?
8 | ^холод Ранок холодний. Пір'я ще не прогрілося. | ^холод The morning is cold. My feathers haven't warmed up yet.
9 | ^план Новий день. Який у нас план? | ^план A new day. What's our plan?
10 | ^лінь Вставай, Муніне. Ти спиш з розплющеним оком. | ^лінь Get up, Munin. You're sleeping with one eye open.
11 | ^перший Хто перший побачив сонце — тому й день вдалий. | ^перший Whoever sees the sun first has a lucky day.

### M @м_світанок   (used by scenes: dawn)
0 | ^світає Ше один день. Ше один день тебе слухати. Йой. | ^світає Another day. Another day of listening to you.
1 | ^хропів То ти хропів. Я спав, як немовля. Зле немовля. | ^хропів That was you. I slept like a baby. Like an angry baby.
2 | ^роса Добрий знак? Мокрі лапи — то добрий знак, по-твоєму? | ^роса A good sign? Wet feet are a good sign?
3 | ^світає Живі. Ну, ти — ледь-ледь. | ^світає Alive. Well, you — barely.
4 | ^хропів Я не хроплю. Я голосом сторожу. | ^хропів I don't snore. I keep watch with my voice.
5 | ^роса Добрий знак — то коли сніданок. Все. | ^роса A good sign is when there's breakfast.
6 | ^сонце Сонцю не треба тебе слухати. Пощастило гаду. | ^сонце The sun doesn't have to listen to you. Lucky sun.
7 | ^сонце Хай встає. А я ше подрімаю, {м_слівце}. | ^сонце Let it rise. I'll sleep some more, {м_слівце}.
8 | ^птахи Яке співають — верещать. Зжер би всіх, {м_слівце}. | ^птахи The birds are screeching. I'd eat them, {м_слівце}.
9 | ^птахи Хай горлають. Поки я не прокинувся як слід. | ^птахи Let them sing. Until I wake up.
10 | ^мряка Яке молоко? Мені б м'ясця, {м_слівце}. | ^мряка Milk? I'd like some meat, {м_слівце}.
11 | ^мряка Мряка — то земля потіє. Від тебе, ботане. Від твоїх розумних балачок. | ^мряка The mist is the earth sweating. Because of you.
12 | ^сніданок Око — то делікатес, бля. Шо ти шариш у жратві. | ^сніданок An eye is a delicacy. You don't understand.
13 | ^сніданок Шо дадуть. А дадуть, сто пудів, нічо. | ^сніданок Whatever they give. And by the looks of it, they'll give nothing.
14 | ^асгард Мені снилось, шо ти заткнувся. Кайфовий сон, бля. | ^асгард I dreamed you were quiet. The best dream.
15 | ^асгард Кров снилась. Повно крові. Смачно, {м_слівце}. | ^асгард I dreamed of blood. Lots of blood. Tasty, {м_слівце}.
16 | ^холод То сунься ближче до шиї. Там тепліше. Тіко не впритул, ясно? | ^холод Sit closer to the neck. It's warmer there.
17 | ^холод Дубак. До обіду буду злий, {м_слівце}. А після обіду — теж. | ^холод Cold. I'll be angry until lunch, {м_слівце}.
18 | ^план План? Когось вгасити. Як завжди, бля. | ^план The plan is to kill someone. As always.
19 | ^план План — шоб без плану. Найкращий план, вкурив? | ^план The plan is to have no plan. That's our best one.
20 | ^лінь Я і вві сні пасу. Талант, {м_слівце}. | ^лінь I keep watch in my sleep. That's a talent, {м_слівце}.
21 | ^лінь Ше хвилинку. Ну дві. Ну відчепись, бля. | ^лінь One more minute. Well, two.
22 | ^перший Ну я побачив. А день все одно гівно. | ^перший I saw it. And the day is still shitty.
23 | ^перший Ти побачив? Ну то хай тобі й фартить, {м_слівце}. А я й так фартовий. | ^перший You saw it? Then may you be lucky, {м_слівце}.

### H @х_світанок_кінець   (used by scenes: dawn)
0 | ^світає Тоді вставай. День не чекає. | ^світає Then get up. The day won't wait.
1 | ^хропів Чув весь табір. І сусідній. | ^хропів The whole camp heard. And the next one.
2 | ^роса Мокрі ноги — дрібниця. Твоя пика зранку — от поганий знак. | ^роса Wet feet are nothing. Your face in the morning — now that's a bad sign.
3 | ^сонце Не проспи день. Він короткий. | ^сонце Don't sleep through the day. It's short.
4 | ^птахи Ти вже прокинувся. На жаль. | ^птахи You're already awake. Unfortunately.
5 | ^мряка Поетичності в тобі — як у каменя. | ^мряка You're as poetic as a rock.
6 | ^сніданок {name} нагодує. Просто прокинься чемно. | ^сніданок {name} will feed us. Just wake up politely.
7 | ^асгард Сни в тебе, як і ти. Нічого світлого. | ^асгард Your dreams are like you. Nothing bright.
8 | ^холод Сонце пригріє. І тебе, і твою вдачу. | ^холод The sun will warm up. You and your temper too.
9 | ^план Ну, хоч послідовно. | ^план Well, at least you're consistent.
10 | ^лінь Ще хвилинка — і я тебе дзьобну. | ^лінь One more minute and I'll peck you.
11 | ^перший Щастя спільне. Навіть тобі шматок. | ^перший Luck is shared. Even you get a piece.

### M @м_вечір   (used by scenes: dusk)
0 | ^ніч Сонце сідає. Ну нарешті моя пора, {м_слівце}. | ^ніч The sun is setting. Finally my time, {м_слівце}.
1 | ^голод Вечір, а я не жер. Хтось про мене пам'ятає взагалі? | ^голод Evening, and I haven't eaten. Does anyone remember me?
2 | ^червоне Небо червоне, як кров. Гарно, {м_слівце}. Аж їсти захотілось. | ^червоне The sky is red. Like blood. Beautiful, {м_слівце}.
3 | ^кажани Кажани вилізли, {м_слівце}. Мої братани нічні. | ^кажани The bats are out, {м_слівце}. My night brothers.
4 | ^сови Сови повилазили, {м_слівце}. Ненавиджу тих сов. | ^сови The owls are waking up, {м_слівце}. I hate owls.
5 | ^багаття Пора вогонь палити. Бо я задубів, {м_слівце}. | ^багаття Time to light a fire. Because I'm freezing, {м_слівце}.
6 | ^втома День минув, а я від тебе заїбався більше, ніж від дороги. | ^втома The day is over, and I'm more tired of you than of the road.
7 | ^тіні Тіні довгі повилазили. Скоро і те, шо в них сидить, вилізе. | ^тіні The shadows are getting longer. Soon what hides in them will crawl out.
8 | ^зорі Перша зоря, {м_слівце}. Загадуй. Я вже загадав — шоб ти заткнувся. | ^зорі The first star, {м_слівце}. Make a wish. I wished you'd shut up.
9 | ^підсумок Ну шо, скільки сьогодні завалили? Мало, сто пудів. | ^підсумок So, how many did we kill today? Not enough, probably.
10 | ^виття Чуєш? Вовки виють. На вечерю кличуть, гиги. | ^виття Hear that? Wolves are howling. Inviting us to supper.
11 | ^нічліг Де ночуємо, {м_слівце}? Тіко не в болоті, бо я злечу. | ^нічліг Where are we sleeping, {м_слівце}? As long as it's not in a swamp.

### H @х_вечір   (used by scenes: dusk)
0 | ^ніч Твоя пора — то ще не привід кусатись. | ^ніч Your time of day is no reason to bite.
1 | ^ніч Ніч для сну, Муніне. Для тебе — особливо. | ^ніч Night is for sleep, Munin. For you especially.
2 | ^голод {name} нагодує. Якщо попросиш чемно. | ^голод {name} will feed you. If you ask politely.
3 | ^голод Вечір — час для вечері. Не для лайки. | ^голод Evening is time for supper. Not for swearing.
4 | ^червоне Червоне — бо сонце соромиться твоїх слів. | ^червоне Red because the sun is ashamed of your words.
5 | ^червоне Як кров? Ти про все так думаєш. | ^червоне Like blood? You think that about everything.
6 | ^кажани Брати? Вони хоч мовчки літають. | ^кажани Brothers? At least they fly in silence.
7 | ^кажани Кажани їдять комарів. А ти що їси? Мені мозок. | ^кажани Bats eat mosquitoes. And what do you eat? My brain.
8 | ^сови Сови мудрі. Тому ти їх і не любиш. | ^сови Owls are wise. That's why you don't like them.
9 | ^сови Сови вночі, ми вдень. Чесний поділ. | ^сови Owls at night, us by day. A fair split.
10 | ^багаття Розпалить {name}. Ти тільки сиди й гарчи. | ^багаття {name} will light it. You just sit and growl.
11 | ^багаття Вогонь буде. Ти тільки не сунь у нього дзьоба. | ^багаття There'll be a fire. Just don't stick your beak in it.
12 | ^втома Взаємно, Муніне. Взаємно. | ^втома Likewise, Munin. Likewise.
13 | ^втома Відпочинь. Завтра знову втомлюватимешся. | ^втома Rest. Tomorrow you'll get tired again.
14 | ^тіні Тому тримаймося світла. | ^тіні That's why we stay in the light.
15 | ^тіні Хай лізе. Ти ж хотів бійки. | ^тіні Let it come. You wanted a fight.
16 | ^зорі А я — щоб ти навчився чемності. Обидва не збудуться. | ^зорі And I wish you'd learn manners. Neither will come true.
17 | ^зорі Зорі не виконують бажань. Вони просто світять. | ^зорі Stars don't grant wishes. They just shine.
18 | ^підсумок Зате всі живі. Це теж підсумок. | ^підсумок But everyone's alive. That's a summary too.
19 | ^підсумок Ти рахуєш трупи. Я рахую дні. Обидва — без кінця. | ^підсумок You count corpses. I count days. Both endless.
20 | ^виття Кличуть, щоб ми стали вечерею. | ^виття They're calling us to become supper.
21 | ^виття Не відповідай їм. Будь ласка. | ^виття Don't answer them. Please.
22 | ^нічліг Де {name} вирішить. Сподіваюсь, під дахом. | ^нічліг Wherever {name} decides. Under a roof, I hope.
23 | ^нічліг Під дахом. Я вже бачу дим. | ^нічліг Under a roof. I can already see smoke.

### M @м_вечір_кінець   (used by scenes: dusk)
0 | ^ніч Посплю, як здохну. | ^ніч I'll sleep when I'm dead.
1 | ^голод Чемно? Я? Ха! Ха-ха! | ^голод Politely? Me? Ha.
2 | ^червоне Моїх слів? Та я ше й не починав, ботан. | ^червоне My words? I haven't even started yet.
3 | ^кажани Мізки? В тебе? Не смішіть мене, {м_слівце}. | ^кажани A brain? You? Don't make me laugh, {м_слівце}.
4 | ^сови Я одну колись дзьобнув. Мудрість їй ніхріна не допомогла. | ^сови I pecked one once. Wisdom didn't help it.
5 | ^багаття Сидіти, гарчати, сраку гріти. Оце я розумію вечір. | ^багаття Sit, growl, warm my ass. My favorite evening.
6 | ^втома Завтра знов втома. Ура, бля. Ура. | ^втома Tomorrow — new tiredness. Hooray, fuck.
7 | ^тіні Світло — для ссикл. Але… сяду ближче. Просто так. | ^тіні Light is for cowards. But I'll sit closer.
8 | ^зорі То хай світять мовчки. Як ти мав би, ботан. | ^зорі Then let them shine in silence. As you should.
9 | ^підсумок Живі — то нудно. Ну… хай вже. | ^підсумок Alive is boring. But fine.
10 | ^виття А я відповім. Дзьобом. По сусалах. | ^виття I'll answer. With my beak.
11 | ^нічліг Дах — то кайф. Аби не капало, як того разу, бля. | ^нічліг A roof is good. As long as it doesn't leak like last time.
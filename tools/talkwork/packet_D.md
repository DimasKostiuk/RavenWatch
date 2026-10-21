# Packet D: scenes fed_hugin, fed_munin, gap_h, gap_m, dungeon, fall, heavy, portal

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): fed_hugin → «fed_hugin.», fed_munin → «fed_munin.», gap_h → «gap_h.», gap_m → «gap_m.», dungeon → «dungeon.», fall → «fall.», heavy → «heavy.», portal → «portal.»

## Scenes (steps; ? = optional)
- fed_hugin: M dlg_jeal_m = {м_ревнощі} / {м_вигук} {м_ревнощі}  →  H dlg_jeal_h = {х_ситий}  →  M? dlg_jeal_m2 = {м_погроза}
- fed_munin: H dlg_jealM_h = {х_голодний}  →  M dlg_jealM_m = {м_ситий} / {м_ситий} {м_лайка}  →  H? dlg_h_close = {х_кінець}
- gap_h: H dlg_gap_h_brag = {х_хвалиться}  →  M dlg_gap_m_reply = {м_відбивається} / {м_вигук} {м_відбивається}  →  H? dlg_h_close = {х_кінець}
- gap_m: M dlg_gap_m_brag = {м_хвалиться} / {м_хвалиться} {м_лайка}  →  H dlg_gap_h_reply = {х_відбивається}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- dungeon: H dlg_dun_h = {х_данж}  →  M dlg_dun_m = {м_данж} / {м_вигук} {м_данж}  →  H? dlg_dun_h2 = {х_данж_кінець}
- fall: M dlg_fall_m = {м_падіння} / {м_вигук} {м_падіння}  →  H dlg_fall_h = {х_падіння}  →  M? dlg_fall_m2 = {м_падіння_кінець}
- heavy: M dlg_heavy_m = {м_вага} / {м_вигук} {м_вага}  →  H dlg_heavy_h = {х_вага}  →  M? dlg_heavy_m2 = {м_вага_кінець}
- portal: M dlg_portal_m = {м_портал} / {м_вигук} {м_портал}  →  H dlg_portal_h = {х_портал}  →  M? dlg_portal_m2 = {м_портал_кінець}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @м_кінець   (used by scenes: idle_a, idle_b, idle_c, rest_b, night, near_death, gap_m, walk, cross_m, perch_b)
38 | ^хвальба Ну-ну. Хто кого, {м_слівце}, — то ше бабка надвоє сказала. | ^хвальба We'll see who beats whom, {м_слівце}.
65 | ^хвальба Пизди шо хочеш. Я попереду. | ^хвальба Say what you want. I'm ahead.
66 | ^хвальба Заздри мовчки, ботан. | ^хвальба Envy in silence, wiseguy.

### H @х_кінець   (used by scenes: idle_b, fog, death, biome, fed_munin, gap_h, sail, cross_h, perch_a)
28 | ^ситий Не жери очима, Муніне. Негарно. | ^ситий Don't devour it with your eyes, Munin. Not nice.
29 | ^хвальба Хвались, хвались. Я все одно попереду. | ^хвальба Brag, brag. I'm still ahead.
46 | ^хвальба Побачимо, хто кого, Муніне. | ^хвальба We'll see who wins, Munin.
47 | ^хвальба Хвалися менше, працюй більше. | ^хвальба Brag less, work more.
48 | ^ситий Їж повільніше. Подавишся. | ^ситий Eat slower. You'll choke.
49 | ^ситий Ну, смачного. Щиро. Майже. | ^ситий Well, enjoy. Sincerely. Almost.
50 | ^ситий Аби тобі пішло на користь. Хоч щось. | ^ситий May it do you good. At least something.

### M @м_ревнощі   (used by scenes: fed_hugin)
0 | ^ситий О, {food} йому. А мені шо, повітря жерти? | ^ситий Oh, {food} for him. And me, what, eat air?
1 | ^ситий Хугіна годують, а мені — обіцянки-цяцянки, {м_слівце}. | ^ситий They feed Hugin, and me — with promises, {м_слівце}.
2 | ^ситий О, бачу, в нас любимчик завівся. Ссикло пернате. | ^ситий I see we've got a favorite here. Feathered pisser.
3 | ~1 ^ситий Йому — жрачку, мені — дулю. Охуєнно, бля. | ~1 ^ситий Food for him, a fig for me. Fucking great.
4 | ~3 ^ситий А мені? Ну хоч крихітку, {м_слівце}? Я ж теж свій. | ~3 ^ситий And for me? At least a crumb, {м_слівце}?
5 | ^ситий Ага, Хугіну знов {food}. А я шо, бля, для краси тут сиджу? | ^ситий Oh, {food} for Hugin again. And what am I, decoration?
6 | ^ситий Бачу, хто тут улюбленчик. Я все запам'ятаю, {м_слівце}. Все. | ^ситий I see who the favorite is here. I'll remember, {м_слівце}.
7 | ^ситий Хугін жере, а я дивлюсь. Як пес під столом, сука. | ^ситий Hugin gobbles, and I watch. Like a dog under the table.
8 | ^ситий Смачно тобі, ботан? Шоб ти вдавився. | ^ситий Tasty? May it stick in your throat.
9 | ^ситий {food}… Мені б теж. Та кому я, нахуй, потрібен. | ^ситий {food}… I'd like some too. But who needs me.
10 | ~1 ^ситий Його годувати — шо сову годувати. Ні толку, ні «дякую». | ~1 ^ситий Feeding him is like feeding an owl. No use, no gratitude.
11 | ~3 ^ситий Ну добре, хай пожере. Але наступний шматок — мій, ясно? | ~3 ^ситий Fine, let him eat. But the next piece is mine.
12 | ^ситий {name}, в тебе два ворони, алло! Не забувай, бля. | ^ситий {name}, you have two ravens. Don't forget.

### H @х_ситий   (used by scenes: fed_hugin)
0 | ^ситий Смачно, до речі. | ^ситий Tasty, by the way.
1 | ^ситий Мене годують, бо я заслужив. | ^ситий They feed me because I've earned it.
2 | ^ситий {food}. Дуже смачно. Шкода, що не поділюсь. | ^ситий {food}. Very tasty. Pity I won't share.
3 | ~1 ^ситий Заздри мовчки. | ~1 ^ситий Envy in silence.
4 | ~3 ^ситий Ну добре, наступного разу попрошу і для тебе. | ~3 ^ситий All right, next time I'll ask for you too.
5 | ^ситий Я б поділився. Але ти б і мене з'їв. | ^ситий I'd share. But you'd eat me too.
6 | ~2 ^ситий Наступного разу проси сам. Ти ж голосний. | ~2 ^ситий Next time ask yourself. You're loud enough.
7 | ^ситий Не заздри. Тобі не личить. | ^ситий Don't be jealous. It doesn't suit you.
8 | ^ситий Чесно заслужив. Я не верещав. | ^ситий Earned it honestly. I didn't screech.
9 | ^ситий Хочеш — поділюсь крихтою. Однією. | ^ситий If you want, I'll share a crumb. One.
10 | ^ситий Дивись і вчись: так їдять культурно. | ^ситий Watch and learn: this is how you eat with manners.
11 | ~1 ^ситий Мовчи, бо подавлюсь від твого бурчання. | ~1 ^ситий Be quiet, or I'll choke on your grumbling.
12 | ~3 ^ситий Наступного разу попрошу для обох. Обіцяю. | ~3 ^ситий Next time I'll ask for both of us. I promise.

### M @м_погроза   (used by scenes: fed_hugin)
0 | ^ситий Вночі прийду і очі тобі виклюю. Обидва. | ^ситий At night I'll eat your eyes.
1 | ^ситий Я це запам'ятаю, {м_слівце}. Я все запам'ятовую. | ^ситий I'll remember, {м_слівце}.
2 | ^ситий Ну-ну. Колись і я буду ситий, а ти — ні. Чекай. | ^ситий Well, well. Someday I'll be full too, and you won't.
3 | ^ситий Шоб ти вдавився, {м_слівце}. | ^ситий Choke on it, {м_слівце}.
4 | ~3 ^ситий Ну добре. Але наступного разу — я, засранцю. Домовились. | ~3 ^ситий Fine. Next time — me, you little shit.
5 | ^ситий Я цей шматок запам'ятаю, {м_слівце}. Ой запам'ятаю. | ^ситий I'll remember this piece, {м_слівце}.
6 | ^ситий Жери-жери. Я почекаю, поки заснеш. Гиги. | ^ситий Eat, eat. I'll wait until you fall asleep.
7 | ^ситий Наступного разу я перший. І все. Крапка. | ^ситий Next time I'm first. Period.
8 | ~3 ^ситий Та хай. Але я запам'ятаю, хто ділився. А хто ні — тим паче. | ~3 ^ситий Fine. But I'll remember who shared.

### H @х_голодний   (used by scenes: fed_munin)
0 | ^ситий Тебе нагодували? А мене? | ^ситий They fed you? And me?
1 | ^ситий Бачу, у нас тепер улюбленець. | ^ситий I see we have a favorite now.
2 | ^ситий Апетит у тебе завжди був кращий за манери. | ^ситий Your appetite was always better than your manners.
3 | ~1 ^ситий Подавися. | ~1 ^ситий Choke on it.
4 | ~3 ^ситий Приємного. Тільки не чавкай. | ~3 ^ситий Enjoy. Just don't smack your beak.
5 | ^ситий Ого, Муніну дали першому. Що сталося? | ^ситий Whoa, Munin got fed first. What happened?
6 | ^ситий Смачного, Муніне. Тільки крихти з мене струси. | ^ситий Enjoy, Munin. Just shake the crumbs off me.
7 | ^ситий Тобі {food}? Цікаво, за які заслуги. | ^ситий {food} for you? I wonder what for.
8 | ^ситий Чавкаєш так, що в Асгарді чути. | ^ситий You smack your beak so loud they hear it in Asgard.
9 | ^ситий Їж, їж. Може, подобрішаєш. | ^ситий Eat, eat. Maybe you'll get kinder.
10 | ~1 ^ситий Тебе годують, щоб ти мовчав. Не спрацює. | ~1 ^ситий They feed you to shut you up. It won't work.
11 | ~3 ^ситий Радий, що тебе нагодували. Чесно. | ~3 ^ситий Glad they fed you. Honestly.
12 | ^ситий Я не заздрю. Просто дивлюся. Пильно. | ^ситий I'm not jealous. I'm just watching. Closely.

### M @м_ситий   (used by scenes: fed_munin)
0 | ^ситий А ти заздри, заздри, {м_слівце}. Тобі корисно. | ^ситий Envy, go on, envy, {м_слівце}.
1 | ^ситий Смачно, бля. Шкода, шо ти не взнаєш, {м_слівце}. | ^ситий Tasty. Pity you won't feel it, {м_слівце}.
2 | ^ситий Мене годують, бо я страшніший. Все просто. | ^ситий They feed me because I'm scarier.
3 | ~1 ^ситий Шо зириш? Не поділюсь, хоч здохни тут. | ~1 ^ситий Don't look at me like that. I won't share, even if you croak.
4 | ~3 ^ситий Ну… на, понюхай, {м_слівце}. Тіко понюхай. | ~3 ^ситий Well… you can sniff it, {м_слівце}.
5 | ^ситий Дивитись можеш. Дивитись на халяву, {м_слівце}. | ^ситий You can watch. Watching is free, {м_слівце}.
6 | ~2 ^ситий Я б поділився. Але вже зжер. Вибач. Нє, не вибач. | ~2 ^ситий I'd share. But I already ate it. Sorry. No, not sorry.
7 | ^ситий Подобрішаю? Не діждешся, {м_слівце}. | ^ситий Get kinder? You'll never see it, {м_слівце}.
8 | ^ситий Заслуги? Я і є заслуга, бля. Весь, від дзьоба до сраки. | ^ситий The merits are me. All of me is one big merit.
9 | ^ситий Чавкаю, бо смачно. А ти слухай і рюмсай. | ^ситий I smack my beak because it's tasty. And you listen and weep.
10 | ^ситий Дивись, дивись. Очима не нажерешся, ботан. | ^ситий Look, look. You won't get full with your eyes.
11 | ~1 ^ситий Ше раз зирнеш — і тебе зжеру. На закусь. | ~1 ^ситий Glance once more — and I'll eat you too.
12 | ~3 ^ситий На, крихту. Від серця відриваю, бля. Цінуй. | ~3 ^ситий Here. A feather from my food. From the heart.

### H @х_хвалиться   (used by scenes: gap_h)
0 | ^хвальба Мені {name} довіряє більше. Факт. | ^хвальба {name} trusts me more. Fact.
1 | ^хвальба Мене {name} частіше кличе. Довіра — річ така. | ^хвальба {name} calls me more often. Trust is like that.
2 | ^хвальба Довіра в мене росте, а в тебе — тільки лайка. | ^хвальба My trust grows, and yours — only swearing.
3 | ~1 ^хвальба Муніне, наздоганяй. Довіра сама не прийде. | ~1 ^хвальба Munin, catch up. Trust won't come by itself.
4 | ^хвальба {name} мене слухає. Тебе — терпить. | ^хвальба {name} listens to me. Puts up with you.
5 | ^хвальба Мені сьогодні довірили пильнувати. Тобі — мовчати. | ^хвальба Today I was trusted to keep watch. You — to keep quiet.
6 | ^хвальба Довіра — як пір'я: в мене густе, в тебе — облізле. | ^хвальба Trust is like feathers: mine are thick, yours are scruffy.
7 | ^хвальба Подивись на мене, Муніне. Отак виглядає довіра. | ^хвальба Look at me, Munin. That's what trust looks like.
8 | ^хвальба Я вже на ступінь вище. Наздоганяй, якщо зможеш. | ^хвальба I'm a level higher already. Catch up if you can.
9 | ~3 ^хвальба Не ображайся, Муніне. Просто я старанніший. | ~3 ^хвальба Don't be offended, Munin. I'm just more diligent.
10 | ~1 ^хвальба Тобі довіряють, як вовкові з вівцями. Тобто ніяк. | ~1 ^хвальба They trust you like a wolf with sheep. That is, not at all.
11 | ^хвальба Знаєш, чому мені довіряють? Бо я не лаюсь. | ^хвальба You know why I'm trusted? Because I don't swear.

### M @м_відбивається   (used by scenes: gap_h)
0 | ^хвальба Ну то й шо, шо ти попереду? Зате я страшніший, {м_слівце}. | ^хвальба So what if you're ahead? I'm scarier, {м_слівце}.
1 | ^хвальба Хвалься, хвалься, ботан. Я однаково голосніший. | ^хвальба Brag, brag. I'll outshout you anyway.
2 | ^хвальба Довіра — то для слабаків, {м_слівце}. І для ботанів. | ^хвальба Trust is for weaklings, {м_слівце}.
3 | ^хвальба Засунь ту довіру собі в сраку. Глибоко. | ^хвальба Shove that trust under your tail.
4 | ~3 ^хвальба Ну добре. Молодець ти. Тіко не зазнавайся, {м_слівце}, бо пір'я повищипую. | ~3 ^хвальба Fine. Good job. Just don't get cocky, {м_слівце}.
5 | ^хвальба Довіра, довіра… А хто вчора нашу людину від вовка закрив? Я, бля. | ^хвальба Trust, trust… And who covered our human from a wolf yesterday? Me.
6 | ^хвальба Не матюкаєшся — бо нудний, як пень, {м_слівце}. | ^хвальба You don't swear because you're boring, {м_слівце}.
7 | ^хвальба Довіра — то нашийник. Носи сам, песик. | ^хвальба Trust is a leash. Wear it yourself.
8 | ^хвальба Слухає тебе? Бо ти бубниш, і під тебе добре спиться. | ^хвальба Listens to you? Because you mumble, and it's easier to fall asleep.
9 | ^хвальба Наздожену й переплюну. Прям плюну, ага. | ^хвальба I'll catch up and outspit you. Literally.
10 | ~1 ^хвальба Ше слово, ботан, — і пір'я в тебе поменшає. Сильно. | ~1 ^хвальба One more word — and you'll have fewer feathers.
11 | ~3 ^хвальба Ну… красень. Тіко я все одно крутіший, {м_слівце}. | ~3 ^хвальба Well… good job. But I'm still better, {м_слівце}.

### M @м_хвалиться   (used by scenes: gap_m)
0 | ^хвальба Мені {name} більше довіряє. Бо я чесний, {м_слівце}. Чесний, кажу! | ^хвальба {name} trusts me more. Because I'm honest, {м_слівце}.
1 | ^хвальба Мене {name} частіше годує. Шариш, чого? Бо довіряє, {м_слівце}. | ^хвальба {name} feeds me more often. Because {he|she} trusts me, {м_слівце}.
2 | ^хвальба Довіра в мене — як м'язи. Росте. А в тебе висить, як у діда. | ^хвальба My trust is like muscles. It grows. Yours sags.
3 | ~1 ^хвальба По довірі ти в сраці, ботан. Наздоганяй. | ~1 ^хвальба You're behind on trust, wiseguy. Catch up.
4 | ^хвальба Мене {name} більше любить, вкурив? Бо я чесний гівнюк, {м_слівце}. | ^хвальба {name} loves me more. Because I'm an honest shithead, {м_слівце}.
5 | ^хвальба Бачив, як {name} мені лиситься? А тобі — хуй. | ^хвальба Did you see {name} smile at me? And not at you.
6 | ^хвальба Довіра в мене — як кігті. Гостра й росте, бля. А в тебе шо? | ^хвальба My trust is like claws. Sharp and growing.
7 | ^хвальба Наздоганяй, ботан. Хоча куди тобі, з твоїми розумностями. | ^хвальба Catch up, wiseguy. Though how could you.
8 | ^хвальба Ше трохи — і я тут улюбленець. А ти — так, пір'я на вітрі. | ^хвальба A little more and I'm the favorite. And you're not anymore.
9 | ~1 ^хвальба Ти позаду, як завжди, лошара. Звикай. | ~1 ^хвальба You're behind, as always. Get used to it.
10 | ~3 ^хвальба Я попереду, Хугіне. Але й ти нічо. Майже. Трошки. | ~3 ^хвальба I'm ahead, Hugin. But you're not bad either. Almost.
11 | ^хвальба Мене першим годують. Оце, бля, і є довіра, шариш? | ^хвальба They feed me first. That, fuck, is trust.

### H @х_відбивається   (used by scenes: gap_m)
0 | ^хвальба Довіра — не змагання. Але так, ти попереду. Поки. | ^хвальба Trust isn't a contest. But yes, you're ahead. For now.
1 | ^хвальба Хвалься, хвалься. Я наздожену. | ^хвальба Brag, brag. I'll catch up.
2 | ^хвальба Твоя довіра голосна. Моя — тиха. | ^хвальба Your trust is loud. Mine is quiet.
3 | ~3 ^хвальба Радий за тебе. Чесно. | ~3 ^хвальба Happy for you. Honestly.
4 | ^хвальба Годує частіше, бо ти частіше верещиш. | ^хвальба {He|She} feeds you more often because you screech more often.
5 | ^хвальба Довіра — не пиріг. Її не з'їси першим. | ^хвальба Trust isn't a pie. You can't eat it first.
6 | ^хвальба Попереду ти, бо голосніший. Не розумніший. | ^хвальба You're ahead because you're louder. Not smarter.
7 | ^хвальба Чесний гівнюк — то все одно гівнюк. | ^хвальба An honest jerk is still a jerk.
8 | ^хвальба Хай. Я не поспішаю. Довіра любить тихих. | ^хвальба Fine. I'm in no hurry. Trust loves the quiet ones.
9 | ~1 ^хвальба Попереду? Ти просто швидше випрошуєш. | ~1 ^хвальба Ahead? You're just quicker at begging.
10 | ~3 ^хвальба Гаразд. Сьогодні твоя перемога. Завтра побачимо. | ~3 ^хвальба Fine. Today is your victory. We'll see tomorrow.

### H @х_данж   (used by scenes: dungeon)
0 | ^темно Тут темно, як у тебе в голові, Муніне. | ^темно It's dark here, like in your head, Munin.
1 | ^запах Пахне смертю й старим золотом. | ^запах It smells of death and old gold.
2 | ^відлуння Не каркай тут. Відлуння все розбудить. | ^відлуння Don't croak here. The echo will wake everything.
3 | ^скарб Десь тут має бути скриня. Дивись уважно. | ^скарб There should be a chest somewhere here. Look carefully.
4 | ^тісно Тісно. Крилами не махнеш. | ^тісно Cramped. Can't flap a wing.
5 | ^кістки Глянь, скільки кісток. Хтось тут погано скінчив. | ^кістки Look how many bones. Someone came to a bad end here.
6 | ^павуки Павутина скрізь. Не заплутайся, Муніне. | ^павуки Cobwebs everywhere. Don't get tangled, Munin.
7 | ^капає Десь капає вода. Кап. Кап. Дратує. | ^капає Water is dripping somewhere. Drip. Drip. Annoying.
8 | ^двері Стара брама. Хтось замкнув її не просто так. | ^двері An old gate. Someone locked it for a reason.
9 | ^шепіт Ти чуєш шепіт? Чи то вітер у щілинах? | ^шепіт Do you hear whispering? Or is it the wind in the cracks?
10 | ^пастка Обережно з плитами на підлозі. Тут люблять пастки. | ^пастка Careful with the floor tiles. They love traps here.
11 | ^задуха Повітря затхле. Як у старій скрині. | ^задуха The air is stale. Like in an old chest.
12 | ^вихід Пам'ятай дорогу назад. Тут легко загубитися. | ^вихід Remember the way back. It's easy to get lost here.
13 | ^череп Глянь, череп усміхається. Чому, цікаво? | ^череп Look, the skull is smiling. I wonder why.

### M @м_данж   (used by scenes: dungeon)
0 | ^темно Темно? Зате мене не видно, {м_слівце}. Саме то. | ^темно Dark? But nobody can see me. Perfect, {м_слівце}.
1 | ^темно Сам ти темрява. Я тут бачу краще за тебе, сліпа курка. | ^темно You're the darkness. I see better here than you.
2 | ^запах Золото — моє. Смерть — твоя. Все по-чесному. | ^запах The gold is mine, the death is yours.
3 | ^запах Драуграми тхне. Смачно, {м_слівце}. | ^запах It smells of draugr. Tasty, {м_слівце}.
4 | ^відлуння Розбуджу — то й наваляємо. Шо такого? | ^відлуння If I wake them — we'll beat them up.
5 | ^відлуння КАР! Ой. Ну, бля, тепер точно знають, шо ми тут. | ^відлуння KRAA! Oops. Well, now they definitely know we're here.
6 | ^скарб Скриня — моя. Хто перший побачив, того й скриня. Закон. | ^скарб The chest is mine. Whoever saw it first takes it.
7 | ^тісно Тісно, бо в тебе голова від мудрості роздулась. | ^тісно It's cramped because your head is swollen with wisdom.
8 | ^скарб Скриня? Цить, хай {name} не чує. Я перший гляну. | ^скарб A chest? Quiet, don't let {name} hear. I'll look first.
9 | ^тісно Тісно? То не махай крилами в мене перед дзьобом, ботан. | ^тісно Cramped? Then don't flap your wings in front of my beak.
10 | ^кістки Не погано. Смачно навіть. Для щурів. | ^кістки Not bad. Tasty. For rats.
11 | ^кістки Ото хтось без воронів ходив, {м_слівце}. От і доходився. | ^кістки Someone here didn't have ravens, {м_слівце}. And that's how they ended.
12 | ^павуки Павуки мене бояться. Бо я їх жеру. | ^павуки Spiders are afraid of me. I eat them.
13 | ^павуки Заплутаюсь — то павук пошкодує, шо вилупився. | ^павуки If I get tangled — the spider will regret it.
14 | ^капає Мене все бісить. Вода — в черзі, після тебе. | ^капає Everything annoys me. The water is in line.
15 | ^капає Кап-кап. Як твої поради, ботан. Нудно й мокро. | ^капає Drip-drip. Like your advice.
16 | ^двері Значить, там шось цікаве. Ламай. | ^двері So there's something interesting behind it.
17 | ^двері Замкнули, бо там шось страшне. Пішли, бля! | ^двері They locked it because there's something scary there. Let's go!
18 | ^шепіт То драуґри пліткують. Про тебе, ботан. | ^шепіт That's the draugr gossiping. About you.
19 | ^шепіт Я ніхріна не чую. Бо я не ссикло. | ^шепіт I don't hear anything. Because I'm no coward.
20 | ^пастка Пастки — для дурнів. От ти й іди перший. | ^пастка Traps are for fools. You step first.
21 | ^пастка Я над підлогою, бля. Мені похуй. | ^пастка I'm above the floor. I don't care.
22 | ^задуха Затхлятина — мій улюблений запах. Трупиками тхне, ммм. | ^задуха Stale is my favorite smell. The smell of corpses.
23 | ^задуха Дихай менше. І пизди менше. | ^задуха Breathe less. Talk less too.
24 | ^вихід Та пам'ятаю я. Ліво, право, ше раз ліво. Ніби. | ^вихід I remember. Left, right, left again. I think.
25 | ^вихід Загубимось — то й житимем тут. Бабла тут повно. | ^вихід If we get lost — we'll live here. There's enough treasure.
26 | ^череп Бо здох до того, як тебе почув. Пощастило мужику. | ^череп Because it died before it heard you.
27 | ^череп Лиситься, бо знає шось, чого ми не знаємо. Падло. | ^череп It's smiling because it knows something we don't.

### H @х_данж_кінець   (used by scenes: dungeon)
0 | ^темно Тихо. Щось там шкребеться. | ^темно Quiet. Something's scratching in there.
1 | ^запах Смерть першою не бери. Вона кусається. | ^запах Don't grab death first. It bites.
2 | ^відлуння Сказав же — розбудиш. | ^відлуння I told you — you'll wake them.
3 | ^скарб Знайдемо. {name} знайде. | ^скарб We'll find it. {name} will find it.
4 | ^тісно Тоді не крутись. Ти мені в око пером тицьнув. | ^тісно Then stop squirming. You poked a feather in my eye.
5 | ^скарб Перший гляне {name}. Ти — другий. Може. | ^скарб {name} looks first. You — second. Maybe.
6 | ^тісно Мовчи і терпи. Вийдемо — тоді махай. | ^тісно Be quiet and bear it. Once we're out — flap away.
7 | ^запах Нюхай менше, дивись більше. | ^запах Sniff less, look more.
8 | ^відлуння Ну от. Тепер чекай гостей. | ^відлуння There. Now expect guests.
9 | ^кістки Ми не скінчимо. Не сьогодні. | ^кістки We won't come to an end. Not today.
10 | ^павуки Їж павуків, але дивись під ноги. | ^павуки Eat spiders, but watch your feet.
11 | ^капає Кап. Кап. Тепер і мене дратує. | ^капає Drip. Drip. Now it annoys me too.
12 | ^двері Цікаве й страшне — для тебе одне й те саме. | ^двері Interesting and scary are the same thing for you.
13 | ^шепіт Тихіше. Хай шепочуть без нас. | ^шепіт Quieter. Let them whisper without us.
14 | ^пастка Ступатиме {name}. Обережно. | ^пастка {name} will step. Carefully.
15 | ^задуха Скоро вийдемо на свіже повітря. | ^задуха We'll be out in fresh air soon.
16 | ^вихід Я пам'ятаю за нас обох. Не хвилюйся. | ^вихід I remember for both of us. Don't worry.
17 | ^череп Дякую, Муніне. Тепер мені моторошно. | ^череп Thanks, Munin. Now I'm creeped out.

### M @м_падіння   (used by scenes: fall)
0 | ^літати Я думав, ти літати вмієш, {м_слівце}. А нє. | ^літати I thought you could fly, {м_слівце}.
1 | ^ноги Гепнулись. Ноги цілі? Та мені похуй, я на плечі. | ^ноги We crashed. Legs in one piece? I don't care, I'm on the shoulder.
2 | ^ще Ше раз так стрибни — я сам злечу. Без тебе. | ^ще Jump like that again — I'll fly off by myself, without you.
3 | ^гепнув Ото гепну{вся|лася}! Аж земля, бля, здригнулась. | ^гепнув Crashed so hard the ground shook.
4 | ^скеля Хто, {м_слівце}, без крил зі скелі стрибає? Я питаю: хто? Дурні. | ^скеля Who, {м_слівце}, jumps off a cliff without wings? I'm asking, who?
5 | ^кущ Гепнулись у кущ. У колючий, {м_слівце}. Ой-ой. | ^кущ We crashed into a bush. A thorny one, {м_слівце}.
6 | ^багно Плюхнулись у багно. Тепер ми однієї масті. | ^багно We flopped into the mud. Now we're the same color.
7 | ^політ Секунду ми летіли. Найкраща секунда за день, бля. | ^політ For a second we flew. The best second today.
8 | ^каміння Каміння поруч летіло. Я рахував, {м_слівце}, чесно. | ^каміння Rocks were flying alongside. I counted, {м_слівце}.
9 | ^висота Звідки ми гепнулись? З неба, чи шо? | ^висота Where did we fall from? From the sky, or what?
10 | ^синці Синці будуть, {м_слівце}. Не в мене, і то добре. | ^синці There'll be bruises, {м_слівце}. Not on me, luckily.
11 | ^сміх Гиги! Я досі ржу, {м_слівце}. Це було шикарно. | ^сміх Ha! I'm still laughing, {м_слівце}. That was beautiful.
12 | ^вдруге Це вже вдруге за день, {м_слівце}. Вдруге! | ^вдруге That's the second time today, {м_слівце}.

### H @х_падіння   (used by scenes: fall)
0 | ^літати Надихну{вся|лася} нами. Тільки крил забу{в|ла} взяти. | ^літати {He|She} was inspired by us. Just forgot to bring wings.
1 | ^літати Літати — то не стрибати з гори. Запам'ятай. | ^літати Flying isn't jumping off a mountain. Remember that.
2 | ^ноги Кістки цілі. Здається. {name}, ти ж ціл{ий|а}? | ^ноги Bones intact. I think. {name}, you're in one piece?
3 | ^ще Не підказуй {йому|їй}. Бо послухає. | ^ще Don't give {him|her} ideas. {He|She} might listen.
4 | ^гепнув Земля витримала. А от {name} — не факт. | ^гепнув The ground held up. {name} — not a given.
5 | ^ноги Ціле. Тільки гордість трохи розбилась. | ^ноги Intact. Only {his|her} pride got a little broken.
6 | ^ще Не стрибне. Я {йому|їй} в очі подивлюсь — передумає. | ^ще {He|She} won't jump. I'll look {him|her} in the eye — {he'll|she'll} change {his|her} mind.
7 | ^гепнув Зате швидко спустились. | ^гепнув At least we got down fast.
8 | ^скеля Той, хто поспішає. Тобто {name}. | ^скеля Someone in a hurry. That is, {name}.
9 | ^скеля Ті, кому лінь обходити. Знайомо, га? | ^скеля Those too lazy to go around. Sounds familiar, eh?
10 | ^кущ Колючий, зате м'який. Майже. | ^кущ Prickly, but soft. Almost.
11 | ^кущ Кущ нас урятував. Подякуй. | ^кущ The bush saved us. Say thanks.
12 | ^багно Ти й до того був чорний. | ^багно You were black before that anyway.
13 | ^багно Помиємося. Колись. | ^багно We'll wash. Someday.
14 | ^політ Тільки приземлення її зіпсувало. | ^політ Only the landing spoiled it.
15 | ^політ Ти завжди любив падіння. Чужі. | ^політ You always loved falls. Other people's.
16 | ^каміння І скільки? | ^каміння And how many?
17 | ^каміння Добре, що не на голову. | ^каміння Good thing not on the head.
18 | ^висота З дурної ідеї. Вона завжди висока. | ^висота From a stupid idea. Those are always high.
19 | ^висота Звідти, де стежки не було. | ^висота From where there was no path.
20 | ^синці Синці — то медалі для незграб. | ^синці Bruises are medals for the clumsy.
21 | ^синці Головне, що не переломи. | ^синці What matters is no broken bones.
22 | ^сміх Сміятися з падіння — некрасиво. | ^сміх Laughing at a fall isn't nice.
23 | ^сміх Гаразд, трохи смішно було. | ^сміх Fine, it was a little funny.
24 | ^вдруге Третього не буде. Я пильнуватиму. | ^вдруге There won't be a third. I'll keep watch.
25 | ^вдруге Земля знайома. Вже як рідна. | ^вдруге The ground is familiar. Like family by now.

### M @м_падіння_кінець   (used by scenes: fall)
0 | ^літати Ну хоч приземлення було красиве. Нє, не було. Гівно було. | ^літати Well, at least the landing was beautiful. No, it wasn't.
1 | ^ноги Цілі — то й добре. Пішли далі. | ^ноги In one piece — good. Let's go.
2 | ^ще Пізно, бля. Вже послуха{в|ла}. | ^ще Too late. {He's|She's} already listened.
3 | ^гепнув Земля ціла. Шкода, шо. | ^гепнув The ground is intact. Pity.
4 | ^ноги Гордість? Яка, нахуй, гордість? Ти бачив, як {name} леті{в|ла}? | ^ноги Pride? What pride? Did you see {name} fly?
5 | ^ще Дивись, дивись. А я вже не можу на це дивитись. | ^ще Watch, watch. Because I'm not watching anymore.
6 | ^гепнув Швидко — не значить правильно, професоре. Навіть я шарю. | ^гепнув Fast isn't the same as right.
7 | ^скеля Лінь — то святе. Але ж не так, бляха. | ^скеля Laziness is sacred. But not like that.
8 | ^кущ М'який? Я тепер, бля, як їжак. | ^кущ Soft? Now I'm like a hedgehog.
9 | ^багно Чорний — то стиль, ботан. А багно — то зашквар. | ^багно Black is style. Mud is disgrace.
10 | ^політ Похуй. Все одно найкраща секунда. | ^політ Still the best second.
11 | ^каміння Три. І всі повз. Я ж казав — фартові ми. | ^каміння I counted: three. And all of them missed us.
12 | ^висота Дурні ідеї — мої найулюбленіші, ага. | ^висота Stupid ideas are my favorite.
13 | ^синці То ми герої. Синюваті, бля. | ^синці Then we're heroes. Bluish ones.
14 | ^сміх Некрасиво, зате від душі. | ^сміх Not nice, but honest.
15 | ^вдруге Буде й третій. Я сракою чую. | ^вдруге There'll be a third. I can feel it.

### M @м_вага   (used by scenes: heavy)
0 | ^ковадло Шо там, ковадло? Я з плеча сповзаю, {м_слівце}. | ^ковадло What, are you carrying an anvil? I'm sliding off the shoulder, {м_слівце}.
1 | ^кинь Викидай каміння. Бо я тебе викину. | ^кинь Throw out the rocks, or I'll throw you out.
2 | ^черепаха Повземо, як черепахи. Як вагітні черепахи, бля. | ^черепаха We crawl like turtles. Like pregnant turtles.
3 | ^сумка Шо там у торбі, {м_слівце}? Каміння? Каміння і ше трохи каміння? | ^сумка What's in the bag, {м_слівце}? Rocks? Rocks and a few more rocks?
4 | ^спина Спина під нами тріщить, {м_слівце}. Я ж чую. | ^спина The back under us is cracking, {м_слівце}. I can hear it.
5 | ^жадібність Все тягнем. Навіть сміття всяке. | ^жадібність We haul everything. Even what we don't need.
6 | ^легкий Хочеш, я злечу — і легше стане? Ні? Ну то й сиджу. | ^легкий Want me to fly off — and it'll be lighter?
7 | ^віз Чого ми візок не взяли? Ну чого, га? | ^віз Why didn't we take the cart? Well, why?
8 | ^повільно Повземо, а день, бля, минає. | ^повільно We crawl, and the day passes.
9 | ^скриня Премо, наче скриню з золотом. А там — дошки, бляха. | ^скриня We're hauling as if it's a chest of gold. And it's planks.
10 | ^кульгає Ми шкандибаємо, {м_слівце}. Ше трохи — і гепнемось разом. | ^кульгає We're limping, {м_слівце}. A little more and we'll fall together.
11 | ^відпочинок Перекури, {м_слівце}. Ну хоч хвилинку. | ^відпочинок Take a rest, {м_слівце}. Just a minute.

### H @х_вага   (used by scenes: heavy)
0 | ^ковадло Там руда, Муніне. Скарб. Потерпиш. | ^ковадло That's ore, Munin. Treasure. You'll live.
1 | ^ковадло Не ний. Ти важиш менше за одну залізяку. | ^ковадло Don't whine. You weigh less than one lump of iron.
2 | ^кинь Викидати руду? Ти здурів. | ^кинь Throw out ore? Have you gone mad?
3 | ^кинь Нічого не викидаємо. Повземо додому. | ^кинь We're throwing nothing out. We crawl home.
4 | ^черепаха Черепахи хоча б мовчать, коли повзуть. | ^черепаха Turtles at least keep quiet while they crawl.
5 | ^черепаха Черепаха хоч доповзе. А ти б і не дійшов. | ^черепаха A turtle will at least crawl there. You wouldn't even make it.
6 | ^сумка Каміння й руда. Наше майбутнє. | ^сумка Stones and ore. Our future.
7 | ^сумка Там усе, що {name} не може кинути. Включно з тобою. | ^сумка Everything {name} can't throw away. Including you.
8 | ^спина То не спина. То твоє бурчання. | ^спина That's not your back. That's your grumbling.
9 | ^спина Нічого. Дійдемо — відпочинемо. | ^спина Never mind. We'll get there and rest.
10 | ^жадібність Все колись знадобиться. | ^жадібність Everything comes in handy someday.
11 | ^жадібність Скупий двічі носить. | ^жадібність The stingy carry twice.
12 | ^легкий На одну пір'їнку легше. Не допоможе. | ^легкий One feather lighter. Won't help.
13 | ^легкий Злітай. Тільки не повертайся з камінням. | ^легкий Fly off. Just don't come back with stones.
14 | ^віз Бо {name} дума{в|ла}, що донесе. | ^віз Because {name} thought it could be carried.
15 | ^віз Візок — для розумних. Ми — для пригод. | ^віз Carts are for the wise. We're for adventures.
16 | ^повільно Поспішиш — людей насмішиш. | ^повільно More haste, less speed.
17 | ^повільно Зате безпечно. Ніхто не наздожене — бо ніхто й не женеться. | ^повільно But it's safe. Nobody will catch up — because nobody's chasing.
18 | ^скриня Дошки — то хата. Хата — то золото. | ^скриня Planks are a house. A house is gold.
19 | ^скриня Дошки чесніші за золото. | ^скриня Planks are more honest than gold.
20 | ^кульгає Не впадемо. Тримайся. | ^кульгає We won't fall. Hold on.
21 | ^кульгає Падати — то по твоїй частині. | ^кульгає Falling is your department.
22 | ^відпочинок Хвилинка — і далі. | ^відпочинок A minute — and on we go.
23 | ^відпочинок Ти не несеш нічого. Відпочинь краще від балачок. | ^відпочинок You're not carrying anything. Better rest from chattering.

### M @м_вага_кінець   (used by scenes: heavy)
0 | ^ковадло Скарб, ага. В тебе в голові скарб — пустота. | ^ковадло Treasure. The treasure in your head is emptiness.
1 | ^кинь То я злітаю. А ти пішки повзи. | ^кинь Then I'm flying. Crawl on foot yourself.
2 | ^черепаха Мовчу. Повзу. Ненавиджу. Всіх. | ^черепаха I'm quiet. I'm crawling. I hate it.
3 | ^сумка Майбутнє, бля, важке. Як твоя дупа. | ^сумка The future is heavy, fuck.
4 | ^ковадло Потерплю. Але, курва, запам'ятаю. | ^ковадло I'll bear it. But I'll remember.
5 | ^кинь Ну, повземо. Як завжди, хай йому грець. | ^кинь We crawl. As always.
6 | ^спина Тріщить, гарчить — яка, бля, різниця. Важко! | ^спина Cracking, growling — what's the difference. It's heavy, fuck.
7 | ^жадібність Ага, знадобиться. Коли ми під ним здохнемо. | ^жадібність It'll come in handy. When we croak under this weight.
8 | ^легкий Пір'їнку? Я важу, як ковадло, бля. Від злості. | ^легкий A feather? I weigh as much as an anvil. From anger.
9 | ^віз Пригоди з грижею. Ото кайф, курва. | ^віз Adventures with a hernia. Wonderful.
10 | ^повільно Повземо, як слимаки. Героїчні слимаки, йо. | ^повільно We crawl like snails. Heroic snails, fuck.
11 | ^скриня Хата з дошок. А я хотів золоту, як в Одіна. | ^скриня A house of planks. And I wanted a golden one.
12 | ^кульгає Гепатись — то в нас спільне, ага. | ^кульгає Falling is our joint business.
13 | ^відпочинок Я твою мудрість на собі тягну, ботан. Важче за камінь, бля. | ^відпочинок I carry the burden of your wisdom. It's the heaviest.

### M @м_портал   (used by scenes: portal)
0 | ^нудить Ненавиджу ці дірки. Мене вивертає, {м_слівце}. Навиворіт. | ^нудить I hate these holes. I'm turning inside out, {м_слівце}.
1 | ^пір'я Я там пір'я лишив. Пів хвоста, бля. | ^пір'я I left feathers in there. Half of them.
2 | ^де Де ми? Нє, не кажи. Знати не хочу. | ^де Where are we? No, don't tell me. I don't want to know.
3 | ^швидко Раз — і ми вже хуй зна де. Як у Локі в кишені, {м_слівце}. | ^швидко Bang — and we're somewhere else. Like in Loki's pocket, {м_слівце}.
4 | ^світло Те світло в дірці аж сліпить, {м_слівце}. | ^світло That light in the portal is blinding, {м_слівце}.
5 | ^шум Після тої дірки у вухах дзвенить. Дзень-дзень, бля. | ^шум My ears are ringing after that hole.
6 | ^рахунок Скіко ми вже разів у ці дірки стрибали, га? | ^рахунок How many times have we jumped into these holes already?
7 | ^половина Хугіне, в мене всі лапи на місці? Перелічи, ну. | ^половина Hugin, do I have all my feet? Count them.
8 | ^звідки А звідки ми, {м_слівце}, щойно вилізли? Забув уже. | ^звідки And where did we just come from, {м_слівце}? I've already forgotten.
9 | ^магія Ці каменюки з рунами… Хто їх наліпив? Локі, чи шо? | ^магія These rocks with runes. Who invented them? Loki?
10 | ^мороз Там, між світами, дубак собачий. Як у Ніфльгеймі, бля. | ^мороз Out there, between worlds, it's as cold as Niflheim.
11 | ^метал А чого руду крізь дірку не можна тягти? Хто це, бля, придумав? | ^метал And why can't ore go through a portal? Who came up with that?

### H @х_портал   (used by scenes: portal)
0 | ^нудить Не на мене. Тільки не на мене. | ^нудить Not on me. Just not on me.
1 | ^нудить Звикнеш. Я ж звик. | ^нудить You'll get used to it. I did.
2 | ^пір'я Пір'я відросте. Ти й так линяв. | ^пір'я Feathers grow back. You were molting anyway.
3 | ^де Ми там, де {name} хоті{в|ла}. Решта неважливо. | ^де We're where {name} wanted. The rest doesn't matter.
4 | ^де Вдома, сподіваюсь. Пахне знайомо. | ^де Home, I hope. Smells familiar.
5 | ^пір'я Половину? Тоді ти тепер удвічі легший. Нарешті. | ^пір'я Half? Then you're twice as light now. Finally.
6 | ^швидко Краще в кишені в Локі, ніж пішки через болото. | ^швидко Better in Loki's pocket than on foot through the swamp.
7 | ^швидко Не кажи про Локі. Він слухає. | ^швидко Don't mention Loki. He's listening.
8 | ^світло Заплющ очі. Ти й так нічого не хочеш бачити. | ^світло Close your eyes. You don't want to see anything anyway.
9 | ^світло Сліпить, бо там шлях між світами. | ^світло It's blinding because that's the way between worlds.
10 | ^шум Це твоя совість. Вперше озвалась. | ^шум That's your conscience. Speaking up for the first time.
11 | ^шум Минеться. Головне — всі на місці. | ^шум It'll pass. What matters is everyone's in place.
12 | ^рахунок Багато. Я рахую. | ^рахунок Plenty. I'm counting.
13 | ^рахунок Більше, ніж ти лаявся. Ну, майже. | ^рахунок More than you've sworn. Well, almost.
14 | ^половина Дві. Як і було. | ^половина Two. As before.
15 | ^половина Лапи на місці. Розум — як і раніше, ні. | ^половина Feet in place. Brains — still not, as before.
16 | ^звідки З дому. Пам'ятай хоч це. | ^звідки From home. Remember at least that.
17 | ^звідки Звідти, де ти знову бурчав. | ^звідки From where you were grumbling again.
18 | ^магія Не Локі. Давні майстри. | ^магія Not Loki. The old masters.
19 | ^магія Якщо Локі — то вони точно колись зламаються. | ^магія If it's Loki — then they'll definitely break someday.
20 | ^мороз Бо там нічого нема. Навіть тебе. | ^мороз Because there's nothing there. Not even you.
21 | ^мороз Тому й не затримуємось. | ^мороз That's why we don't linger.
22 | ^метал Портал не любить метал. Як я — твою лайку. | ^метал A portal doesn't like metal. Like I don't like your swearing.
23 | ^метал Боги так вирішили. Не сперечайся. | ^метал The gods decided so. Don't argue.

### M @м_портал_кінець   (used by scenes: portal)
0 | ^нудить Звикнеш, звикнеш… Та йди ти зі своїм «звикнеш». | ^нудить You'll get used to it, you'll get used to it… Screw you.
1 | ^пір'я Я не линяв. Я оновлювався. Шариш? | ^пір'я I wasn't molting. I was renewing.
2 | ^де Знайомо? Твоєю мудрістю пахне. Значить, біда. | ^де Familiar? Smells of your wisdom. So, trouble.
3 | ^швидко Хай слухає. Я тому Локі все одно не вірю. | ^швидко Let him listen. I don't trust him anyway.
4 | ^нудить Сам звикай, ботан. | ^нудить Get used to it yourself.
5 | ^де Ну й добре. Аби не в морі, бо там я блюю. | ^де Well, good. As long as it's not the sea.
6 | ^світло Заплющив. І в тебе врізався. Дякую, порадник хуїв. | ^світло I closed them. Now I bumped into you too.
7 | ^шум Минеться, минеться… А поки дзвенить, курва. | ^шум It'll pass, it'll pass. And meanwhile — it's ringing, fuck.
8 | ^рахунок Рахує він, бля. Краще б жратву рахував. | ^рахунок He's counting. Better he counted food.
9 | ^половина Дві? А відчуваю три. Шо за хуйня. | ^половина Two? But I feel three. Strange.
10 | ^звідки Я завжди бурчу. То не прикмета, то характер. | ^звідки I always grumble. It's not a clue.
11 | ^магія Хто б не наліпив — хай йому гикнеться, падлу. | ^магія Whoever invented them — may they get the hiccups.
12 | ^мороз Холодно, курва. Ненавиджу ту дірку. | ^мороз Cold, fuck. I hate that hole.
13 | ^метал Боги — жмоти. Як і ти. | ^метал The gods are greedy. Like you.
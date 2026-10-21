# Packet F: scenes potion, sit, cozy, trophy, trophy_hang, cart, plant

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): potion → «potion.», sit → «sit.», cozy → «cozy.», trophy → «trophy.», trophy_hang → «trophy_hang.», cart → «cart.», plant → «plant.»

## Scenes (steps; ? = optional)
- potion: M dlg_potion_m = {мд_зілля} / {м_вигук} {мд_зілля}  →  H dlg_potion_h = {хд_зілля}  →  M? dlg_potion_m2 = {мд_зілля_кінець}
- sit: H dlg_sit_h = {хд_стілець}  →  M dlg_sit_m = {мд_стілець} / {м_вигук} {мд_стілець}  →  H? dlg_sit_h2 = {хд_стілець_кінець}
- cozy: H dlg_cozy_h = {хд_затишок}  →  M dlg_cozy_m = {мд_затишок} / {м_вигук} {мд_затишок}  →  H? dlg_cozy_h2 = {хд_затишок_кінець}
- trophy: M dlg_trophy_m = {мд_трофей} / {м_вигук} {мд_трофей}  →  H dlg_trophy_h = {хд_трофей}  →  M? dlg_trophy_m2 = {мд_трофей_кінець}
- trophy_hang: H dlg_hang_h = {хд_стіна}  →  M dlg_hang_m = {мд_стіна} / {м_вигук} {мд_стіна}  →  H? dlg_hang_h2 = {хд_стіна_кінець}
- cart: M dlg_cart_m = {мд_візок} / {м_вигук} {мд_візок}  →  H dlg_cart_h = {хд_візок}  →  M? dlg_cart_m2 = {мд_візок_кінець}
- plant: H dlg_plant_h = {хд_город}  →  M dlg_plant_m = {мд_город} / {м_вигук} {мд_город}  →  H? dlg_plant_h2 = {хд_город_кінець}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @мд_зілля   (used by scenes: potion)
0 | ^ковток Хильнули! Ну шо, як воно, га? | ^ковток Down it went! Well, how is it?
1 | ^більше Ше ковток! За мене, ну! | ^більше Another sip! For me.
2 | ^сила Чуєш силу? Я чую. Аж через плече пре, бля. | ^сила Feel the strength? I do. Through the shoulder.
3 | ^смак Яке воно на смак? Кажи, бо мені, сука, не дали. | ^смак What does it taste like? Tell me, because they didn't give me any.
4 | ^запас Скільки там ше тих пляшечок лишилось? | ^запас How many of those little bottles are left?
5 | ^одін_п Як думаєш, Одін теж таку бурду п'є? | ^одін_п Do you think Odin drinks stuff like this too?

### H @хд_зілля   (used by scenes: potion)
0 | ^ковток Гірке. Але діє. | ^ковток Bitter. But it works.
1 | ^ковток Солодке. Підозріло солодке. | ^ковток Sweet. Suspiciously sweet.
2 | ^більше Зілля — не для забави. | ^більше Potions aren't for fun.
3 | ^більше За тебе? Тоді вже краще води. | ^більше For you? Then better water.
4 | ^сила Відчуваю. Тепер можна й у бій. | ^сила I feel it. Now we can go into battle.
5 | ^сила Відчуваю, що ти перебільшуєш. | ^сила I feel you're exaggerating.
6 | ^смак Як мед і трави. І трохи як ліки. | ^смак Like honey and herbs. And a bit like medicine.
7 | ^смак Як перемога. Перемога гірка. | ^смак Like victory. Victory is bitter.
8 | ^запас Небагато. Варто наварити. | ^запас Not much. We should brew more.
9 | ^запас Досить на кілька боїв. | ^запас Enough for a few fights.
10 | ^одін_п Одін п'є мед поезії. Нам не дадуть. | ^одін_п Odin drinks the mead of poetry. We won't get any.
11 | ^одін_п Одін п'є все. Він же бог. | ^одін_п Odin drinks everything. He's a god.

### M @мд_зілля_кінець   (used by scenes: potion)
0 | ^ковток Та хоч яке. Аби вставило. | ^ковток Whatever it is — as long as it works.
1 | ^більше Для забави — то інша бочка, ясно. Там я перший. | ^більше For fun — that's another barrel.
2 | ^сила У бій! Нарешті, бля! Хто перший? | ^сила Into battle! Finally!
3 | ^смак Мені б хоч понюхати, жмоти. | ^смак If only I could sniff it.
4 | ^запас Кілька — то мало. Варимо, нахуй! | ^запас A few is not enough. Let's brew!
5 | ^одін_п То й я буду бог. Наливай, не жмоться. | ^одін_п Then I'll be a god too. Pour.

### H @хд_стілець   (used by scenes: sit)
0 | ^трон Стілець — як трон. А {name} на ньому — як ярл. | ^трон A chair like a throne. And {name} on it like a jarl.
1 | ^ноги Ноги відпочивають. Видно ж, як {name} розслаби{вся|лася}. | ^ноги The legs are resting. You can see how {name} relaxed.
2 | ^тиша_с Тихо, коли сидимо. Чуєш, як дерево поскрипує? | ^тиша_с It's quiet when we sit. Hear the wood creaking?
3 | ^думки_с Сидячи, краще думається. | ^думки_с Thinking is easier sitting down.
4 | ^довго_с Довго сидітимемо, як думаєш? | ^довго_с Will we sit long, do you think?
5 | ^стілець Хто цей стілець змайстрував? Гарна робота. | ^стілець Who made this chair? Nice work.

### M @мд_стілець   (used by scenes: sit)
0 | ^трон Ярл, бля. Ярл у засраних чоботах. | ^трон A jarl, fuck. A jarl with dirty boots.
1 | ^трон Ярл, а я — його радник. Головний, ясно? | ^трон A jarl, and I'm his adviser. The chief one.
2 | ^ноги Ага. А ми тут сиди й нудьгуй, як дурні. | ^ноги Yeah. And we sit here bored.
3 | ^ноги Хай сидить. Мені й на плечі нормально. | ^ноги Let them sit. I'm fine on the shoulder anyway.
4 | ^тиша_с Чую. Стілець кривий, от і скрипить. | ^тиша_с I hear it. The chair is crooked.
5 | ^тиша_с Чую. Нудьга, бляха. | ^тиша_с I hear it. Boring, fuck.
6 | ^думки_с Думай-думай, професоре. А я подрімаю. | ^думки_с Think. And I'll take a nap.
7 | ^думки_с А мені краще думається, коли когось гашу. | ^думки_с I think better when I'm hitting someone.
8 | ^довго_с Поки сраку не відсидимо, по-любому. | ^довго_с Until our asses go numb.
9 | ^довго_с Поки жрати не принесуть. | ^довго_с Until they bring food.
10 | ^стілець Та {name} ж. Криво, але, бля, нормально. | ^стілець {name}. Crooked, but nice.
11 | ^стілець Без поняття. Але сидиться ж. | ^стілець Don't know. But it's sittable.

### H @хд_стілець_кінець   (used by scenes: sit)
0 | ^трон Двір як двір. Ярл, радник і стілець. | ^трон A court like any court. A jarl, an adviser and a chair.
1 | ^ноги Посидимо всі разом. | ^ноги Let's all sit together.
2 | ^тиша_с Тихо — то вже подарунок. | ^тиша_с Quiet is already a gift.
3 | ^думки_с Кожному своє. | ^думки_с To each their own.
4 | ^довго_с Тоді — недовго. Сподіваюся. | ^довго_с Then — not for long. I hope.
5 | ^стілець Добре, що сидиться. | ^стілець Good that it's comfortable.

### H @хд_затишок   (used by scenes: cozy)
0 | ^тепло_д Тепло, Муніне. Вдома завжди тепліше. | ^тепло_д It's warm, Munin. It's always warmer at home.
1 | ^дах_д Над нами дах. Ніякий дощ не страшний. | ^дах_д There's a roof over us. No rain is scary.
2 | ^кубло Знаєш, Муніне, це вже наше гніздо. | ^кубло You know, Munin, this is our nest now.
3 | ^спокій_д Спокійно тут. Навіть ти тихіший. | ^спокій_д It's calm here. Even you are quieter.
4 | ^вечеря Пахне вечерею. Чуєш? | ^вечеря It smells of supper. Smell it?
5 | ^пам'ять_д Пам'ятаєш, якою ця хата була спочатку? | ^пам'ять_д Remember what this house was like at first?

### M @мд_затишок   (used by scenes: cozy)
0 | ^тепло_д Тепло — бо вогонь горить. А не бо вдома, ботане. | ^тепло_д Warm because of the fire. Not because it's home.
1 | ^тепло_д Тепло, курва. Аж розкис весь. | ^тепло_д Warm, fuck. I've gone all soft.
2 | ^дах_д Дах — то нормально. Аби не тік. | ^дах_д A roof is good. As long as it doesn't leak.
3 | ^дах_д Дах є, а жрачки нема. Шо за порядки? | ^дах_д There's a roof, but no food.
4 | ^кубло Гніздо, бля. А яйця мої де? | ^кубло A nest, fuck. And where are my eggs?
5 | ^кубло Наше. Хто сунеться — в тім'я дзьобну. | ^кубло Ours. Whoever comes in — I'll peck.
6 | ^спокій_д Тихіший, бо спати хочу. Не звикай. | ^спокій_д Quieter because I want to sleep.
7 | ^спокій_д Нє, не тихіший. Я злість накопичую. Ше побачиш. | ^спокій_д Not quieter. I'm storing up anger.
8 | ^вечеря Чую. Мені шматок, бо дзьобну, вкурив? | ^вечеря I smell it. A piece for me, or I'll peck.
9 | ^вечеря Чую. Вже слина капає, йой. | ^вечеря I smell it. And I'm already drooling.
10 | ^пам'ять_д Курником вона була. І досі трохи курник, чесно. | ^пам'ять_д A chicken coop. And it's still a bit of a chicken coop.
11 | ^пам'ять_д Пам'ятаю. Дірява була, як твоя голова. | ^пам'ять_д I remember. Full of holes, like your memory.

### H @хд_затишок_кінець   (used by scenes: cozy)
0 | ^тепло_д Хай так. Але вдома. | ^тепло_д So be it. But at home.
1 | ^дах_д Їжа буде. Дах — уже є. | ^дах_д There'll be food. There's a roof already.
2 | ^кубло Отже, наше. | ^кубло So it's ours.
3 | ^спокій_д Накопичуй тихо. | ^спокій_д Gather it quietly.
4 | ^вечеря Почекай. Вечеря для всіх. | ^вечеря Wait. Supper is for everyone.
5 | ^пам'ять_д Тепер вона добра. Як і ми. | ^пам'ять_д Now it's good. Like us.

### M @мд_трофей   (used by scenes: trophy)
0 | ^голова Голова, курва! Дай я понесу! | ^голова A head, fuck! I want to carry it.
1 | ^смердить Смердить дохлятиною. Мій улюблений запах, йо. | ^смердить It stinks of death. My favorite smell.
2 | ^рідкість Такий трофей не кожен день падає, шариш? | ^рідкість A trophy like that doesn't drop every day.
3 | ^бій_т Пам'ятаєш, як воно ревло? Аж кайфово було. | ^бій_т Remember how it roared? Downright pleasant.
4 | ^продати А його продати можна? За блискітки якісь? | ^продати Can we sell it? For sparkles?
5 | ^колекція Скільки там у нас уже тих голів, га? | ^колекція How many of those heads do we have now?

### H @хд_трофей   (used by scenes: trophy)
0 | ^голова Вона важча за тебе. | ^голова It's heavier than you.
1 | ^голова Понесе {name}. Ти — подивишся. | ^голова {name} will carry it. You'll look.
2 | ^смердить Твій улюблений запах — будь-який, аби з м'ясом. | ^смердить Your favorite smell is any smell, as long as there's meat.
3 | ^смердить Смердить. Тому й несемо додому, а не тримаємо в сумці. | ^смердить It stinks. That's why we're taking it home and not keeping it in the bag.
4 | ^рідкість Тому його треба берегти. | ^рідкість That's why it has to be kept safe.
5 | ^рідкість Не кожен. Тому ми й пишаємося. | ^рідкість Not everyone. That's why we're proud.
6 | ^бій_т Пам'ятаю. Ти ревів голосніше. | ^бій_т I remember. You roared louder.
7 | ^бій_т Пам'ятаю. Тепер тихо. | ^бій_т I remember. Now it's quiet.
8 | ^продати Трофеї не продають. Їх вішають. | ^продати Trophies aren't sold. They're hung.
9 | ^продати Можна. Але соромно. | ^продати You could. But it's shameful.
10 | ^колекція Достатньо, щоб гості боялися. | ^колекція Enough to frighten guests.
11 | ^колекція Не рахував. Але стіна наповнюється. | ^колекція I haven't counted. But the wall is filling up.

### M @мд_трофей_кінець   (used by scenes: trophy)
0 | ^голова Тоді хоч очі видзьобаю. Шо, теж не можна? | ^голова Then at least I'll peck out the eyes.
1 | ^смердить То несім додому — і на стіну, нахуй. | ^смердить Home — and onto the wall.
2 | ^рідкість Берегти — то я вмію. Дзьобом по рилу, хто підійде. | ^рідкість Keeping things safe — that I can do. With my beak.
3 | ^бій_т Ревло гарно. Шкода, шо заткнулось. | ^бій_т It roared nicely. Pity it went quiet.
4 | ^продати Соромно — то для лохів. | ^продати Shame is for weaklings.
5 | ^колекція Мало. Треба ше. Дохуя треба. | ^колекція Not enough. We need more.

### H @хд_стіна   (used by scenes: trophy_hang)
0 | ^стіна_т Ще одна голова на стіні. Зала слави росте. | ^стіна_т Another head on the wall. The hall of glory grows.
1 | ^погляд_т Він дивиться на нас мертвими очима. | ^погляд_т It's looking at us with dead eyes.
2 | ^криво Висить трохи криво. Чи то мені здається? | ^криво It hangs a little crooked. Or is it just me?
3 | ^гості Гості побачать — злякаються. | ^гості Guests will see it — and get scared.
4 | ^сісти Можна було б сісти на той трофей. Як на жердину. | ^сісти One could sit on that trophy. Like a perch.
5 | ^історія_т Кожен трофей — історія. Цю я розповідатиму довго. | ^історія_т Every trophy is a story. I'll be telling this one for a long time.

### M @мд_стіна   (used by scenes: trophy_hang)
0 | ^стіна_т Росте, бля. Скоро й мене тут повісять. | ^стіна_т It's growing, fuck. Soon they'll hang me too.
1 | ^стіна_т Висить як треба. Бойовий інтер'єр, ги. | ^стіна_т It hangs nicely. Battle decor.
2 | ^погляд_т Хай дивиться. Я теж дивлюсь. Хто перший кліпне, га? | ^погляд_т Let it look. I'm looking too. Who'll win?
3 | ^погляд_т Мертві очі — найкращі. Не кліпають, не пиздять. | ^погляд_т Dead eyes are the best eyes. They don't blink.
4 | ^криво Криво. Як і все тут, курва. | ^криво Crooked. Like everything here.
5 | ^криво Тобі здається. Око в тебе вже того, втомилось. | ^криво It seems. Your eye is tired.
6 | ^гості І правильно. Хай знають, з ким мають діло. | ^гості Rightly so. Let them know whom they're dealing with.
7 | ^гості Які гості? До нас ніхто не ходить, бо ми — це ми. | ^гості What guests? Nobody visits us.
8 | ^сісти Сідай-сідай. Подивлюсь, як ти гепнешся. | ^сісти Sit. I'll watch you fall.
9 | ^сісти Я перший сяду. Я ж його валив. | ^сісти I'll sit first. I did the killing.
10 | ^історія_т Тіко коротко. Я все одно засну на другому слові. | ^історія_т Keep it short. I'll fall asleep anyway.
11 | ^історія_т Розповідай. Але головний там я, ясно? | ^історія_т Tell it. But I'm the main hero.

### H @хд_стіна_кінець   (used by scenes: trophy_hang)
0 | ^стіна_т Хай росте. Місця на стінах ще досить. | ^стіна_т Let it grow. There's still plenty of room on the walls.
1 | ^погляд_т Моторошно, але гарно. | ^погляд_т Eerie, but beautiful.
2 | ^криво Хай так. Головне — висить. | ^криво So be it. What matters is it hangs.
3 | ^гості Може, ще прийдуть. Побачать — запам'ятають. | ^гості Maybe more will come. They'll see — and remember.
4 | ^сісти Сяду пізніше. Коли ти не бачитимеш. | ^сісти I'll sit later. When you're not looking.
5 | ^історія_т Розповім. По-своєму. | ^історія_т I'll tell it. My way.

### M @мд_візок   (used by scenes: cart)
0 | ^рип Рип-рип, рип-рип. Цей візок мене доконає, бля. | ^рип Creak-creak. This cart will drive me mad.
1 | ^важко_в Шо ми там везем? Пів гори, нахуй? | ^важко_в What are we hauling there? Half a mountain?
2 | ^колесо Колесо хитається. Відвалиться, от побачиш. Сто пудів. | ^колесо The wheel is wobbling. It'll fall off, you'll see.
3 | ^дорога_в Дорога — ніби троль по ній срав. Ями, каміння. | ^дорога_в The road is like after a troll. Holes, rocks.
4 | ^кінь Глянь на {name} — чистий кінь. Тіко без хвоста. | ^кінь {name} is like a horse. Honest.
5 | ^сісти_в А можна я на візок сяду? Він же, бля, їде. | ^сісти_в Can I sit on the cart? It's moving, after all.

### H @хд_візок   (used by scenes: cart)
0 | ^рип Змастити б колеса. | ^рип The wheels could use some grease.
1 | ^рип Терпи. Він везе, а не співає. | ^рип Bear it. It carries, it doesn't sing.
2 | ^важко_в Руду. Наше майбутнє. | ^важко_в Ore. Our future.
3 | ^важко_в Все, що {name} не захоті{в|ла} лишити. | ^важко_в Everything {name} didn't want to leave behind.
4 | ^колесо Не накаркай. | ^колесо Don't jinx it.
5 | ^колесо Не відвалиться. {name} його добре стави{в|ла}. | ^колесо It won't fall off. {name} fitted it well.
6 | ^дорога_в Тому й повільно. | ^дорога_в That's why it's slow.
7 | ^дорога_в Іншої дороги нема. | ^дорога_в There's no other road.
8 | ^кінь Не ображай. Працює ж. | ^кінь Don't insult it. It works, doesn't it.
9 | ^кінь Кінь би краще впорався. | ^кінь A horse would do better.
10 | ^сісти_в Сідай. Тільки не бурчи. | ^сісти_в Sit. Just don't grumble.
11 | ^сісти_в Ти й так на плечі. Яка різниця? | ^сісти_в You're on the shoulder anyway. What's the difference?

### M @мд_візок_кінець   (used by scenes: cart)
0 | ^рип Змастити? Чим? Твоїми порадами, розумнику? | ^рип Grease it? With what? Your advice?
1 | ^важко_в Важке в нас майбутнє, курва. Аж візок стогне. | ^важко_в The future is heavy, fuck.
2 | ^колесо Побачим. Я пильную, мені ж не похуй. | ^колесо We'll see. I'm keeping watch.
3 | ^дорога_в Ну то я полечу. А ви собі повзіть. | ^дорога_в Then I'll fly. And you crawl.
4 | ^кінь Та я не ображаю. Я хвалю. Як умію, бля. | ^кінь I'm not insulting. I'm praising. As best I can.
5 | ^сісти_в Різниця — я на руді сиджу. Я багатий, ясно? | ^сісти_в The difference — I'm sitting on ore. I'm rich.

### H @хд_город   (used by scenes: plant)
0 | ^насіння Насіння в землі. Тепер — чекати. | ^насіння The seeds are in the ground. Now — wait.
1 | ^врожай Будемо з урожаєм. Якщо кабани не знайдуть. | ^врожай We'll have a harvest. If the boars don't find it.
2 | ^руки_с Руки в землі — і {name} спокійн{ий|а}. Дивно. | ^руки_с Hands in the dirt — and {name} is calm. Strange.
3 | ^ягоди_с Посадили б ще ягід. Для нас. | ^ягоди_с We should plant more berries. For us.
4 | ^дощ_с Добре б дощ пройшов. Поливати не треба. | ^дощ_с Rain would be nice. No need to water.
5 | ^кріт {name} у землі, як кріт. Тільки старанніший. | ^кріт {name} is in the dirt like a mole. Only more diligent.

### M @мд_город   (used by scenes: plant)
0 | ^насіння Чекати, бля. Знову чекати. Заїбало чекати. | ^насіння Waiting, fuck. Waiting again.
1 | ^насіння А я те насіння подзьобаю, поки ніхто не бачить. Ги. | ^насіння And I'll peck the seeds while nobody's looking.
2 | ^врожай Кабанів я сам пожену. Пиздюлів їм дам. | ^врожай I'll chase off the boars myself.
3 | ^врожай Кабани знайдуть. Ці свині завжди знаходять, сука. | ^врожай The boars will find it. They always do.
4 | ^руки_с Спокійн{ий|а}, бо нікого не валить. Отака хуйня. | ^руки_с Calm because {he's|she's} not killing anyone.
5 | ^руки_с Землю колупати заспокоює? Мене — нє, нахуй. | ^руки_с Earth calms people down. Not me.
6 | ^ягоди_с Для мене. А ти обійдешся, ботан. | ^ягоди_с For me. You'll do without.
7 | ^ягоди_с Ягоди — то тема. Хай чорниці садить. | ^ягоди_с Berries are good. Plant blueberries.
8 | ^дощ_с Дощ — то мокро. Нахуя мені той дощ? | ^дощ_с Rain is wet. I don't want rain.
9 | ^дощ_с Та хай іде. Я під дах шмигну. | ^дощ_с Let it rain. I'll hide under a roof.
10 | ^кріт Кріт хоч смачний. Я пробував. | ^кріт At least a mole is tasty.
11 | ^кріт Кріт — то я малим. Не питай. | ^кріт The mole was me as a child.

### H @хд_город_кінець   (used by scenes: plant)
0 | ^насіння Чекатимемо. І пильнуватимемо. | ^насіння We'll wait. And keep watch.
1 | ^врожай Тоді пильнуймо разом. | ^врожай Then let's keep watch together.
2 | ^руки_с Хай буде спокій. Хоч на грядці. | ^руки_с Let there be peace. At least in the garden.
3 | ^ягоди_с Попросимо. Чемно. | ^ягоди_с We'll ask. Politely.
4 | ^дощ_с Дощ — для грядки, не для тебе. | ^дощ_с Rain is for the garden, not for you.
5 | ^кріт Кроти, ворони… Головне, що грядки є. | ^кріт Moles, ravens… What matters is there's a garden.
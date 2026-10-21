# Packet E: scenes fish, chop, mine, raid, trade, ride, smelt, ferment, repair

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): fish → «fish.», chop → «chop.», mine → «mine.», raid → «raid.», trade → «trade.», ride → «ride.», smelt → «smelt.», ferment → «ferment.», repair → «repair.»

## Scenes (steps; ? = optional)
- fish: M dlg_fish_m = {м_риба} / {м_вигук} {м_риба}  →  H dlg_fish_h = {х_риба}  →  M? dlg_fish_m2 = {м_риба_кінець}
- chop: H dlg_chop_h = {х_рубка}  →  M dlg_chop_m = {м_рубка} / {м_вигук} {м_рубка}  →  H? dlg_chop_h2 = {х_рубка_кінець}
- mine: M dlg_mine_m = {м_руда} / {м_вигук} {м_руда}  →  H dlg_mine_h = {х_руда}  →  M? dlg_mine_m2 = {м_руда_кінець}
- raid: H dlg_raid_h = {хд_набіг}  →  M dlg_raid_m = {мд_набіг} / {м_вигук} {мд_набіг}  →  H? dlg_raid_h2 = {хд_набіг_кінець}
- trade: M dlg_trade_m = {мд_торг} / {м_вигук} {мд_торг}  →  H dlg_trade_h = {хд_торг}  →  M? dlg_trade_m2 = {мд_торг_кінець}
- ride: H dlg_ride_h = {хд_їзда}  →  M dlg_ride_m = {мд_їзда} / {м_вигук} {мд_їзда}  →  H? dlg_ride_h2 = {хд_їзда_кінець}
- smelt: M dlg_smelt_m = {мд_піч} / {м_вигук} {мд_піч}  →  H dlg_smelt_h = {хд_піч}  →  M? dlg_smelt_m2 = {мд_піч_кінець}
- ferment: M dlg_ferment_m = {мд_бочка} / {м_вигук} {мд_бочка}  →  H dlg_ferment_h = {хд_бочка}  →  M? dlg_ferment_m2 = {мд_бочка_кінець}
- repair: H dlg_repair_h = {хд_лагодження}  →  M dlg_repair_m = {мд_лагодження} / {м_вигук} {мд_лагодження}  →  H? dlg_repair_h2 = {хд_лагодження_кінець}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @м_риба   (used by scenes: fish)
0 | ^перша Риба! Перша — моя! Моя, сказав! | ^перша Fish! The first one's mine!
1 | ^мала Оце риба? Я черв'яків більших жер, {м_слівце}. | ^мала Is that a fish? I've eaten bigger worms, {м_слівце}.
2 | ^ще Ше лови. Я ше не нажерся. | ^ще Catch more. I'm not full yet.
3 | ^луска Хугіне, вона мені лускою в око! Прямо в око, сука! | ^луска Hugin, it threw scales at me! Right in the eye!
4 | ^велика Ото рибина! Здоровенна, більша за мене, {м_слівце}! | ^велика Now that's a fish! Bigger than me, {м_слівце}!
5 | ^черв'як Черв'яка на гачок? Нашо, дай мені, я його сам схаваю. | ^черв'як A worm on the hook? Give it to me, I'll eat it myself.
6 | ^зірвалась Зірвалась! Сука, зірвалась! Та шо ж таке! | ^зірвалась It got off! Fuck, it got off!
7 | ^смажити Смажити будем чи сиру хавати? Я за сиру, бля. | ^смажити Are we frying it or eating it raw? I vote raw.
8 | ^поплавець Поплавок смикнувся! Тягни, {м_слівце}, тягни! | ^поплавець The float twitched! Pull, {м_слівце}!
9 | ^чекати Сидимо, чекаємо. Рибо, ну давай вже, курва. | ^чекати We sit, we wait. Come on, fish, already.
10 | ^кістка Кістка в горлі застрягла, {м_слівце}. Ше з тої риби. | ^кістка I've got a bone stuck in my throat, {м_слівце}. From the last fish.
11 | ^глибоко А там, у глибині, кажуть, є риба, шо кораблі ковтає. Цілими, бля. | ^глибоко And in the deep, they say, there's a fish that swallows ships.

### H @х_риба   (used by scenes: fish)
0 | ^перша Перша — {name}. Тобі — хвіст. | ^перша The first one is for {name}. You get the tail.
1 | ^перша Тобі хвіст. Як завжди. | ^перша You get the tail. As always.
2 | ^мала Мала, але чесна. Не те що ти. | ^мала Small, but honest. Unlike you.
3 | ^ще Риба не кінчиться. На відміну від мого терпіння. | ^ще The fish won't run out. Unlike my patience.
4 | ^мала Мала — бо ти її розлякав своїм каркотінням. | ^мала Small because you scared it with your cawing.
5 | ^ще Риба не для тебе. Рибу їсть {name}. | ^ще The fish isn't for you. {name} eats the fish.
6 | ^луска Риба мстить. Знає, хто її з'їсть. | ^луска The fish takes revenge. It knows who'll eat it.
7 | ^луска Луска — то прикраса. Тобі личить. | ^луска Scales are decoration. They suit you.
8 | ^велика Нарешті хтось більший за тебе. Поважай. | ^велика Finally someone bigger than you. Show respect.
9 | ^велика Більша — бо розумніша. До тебе не підпливала. | ^велика Bigger because smarter. It never swam near you.
10 | ^черв'як Тоді наживкою будеш ти. | ^черв'як Then you'll be the bait.
11 | ^черв'як Черв'як — для риби. Ти — для терпіння. | ^черв'як The worm is for the fish. You are for patience.
12 | ^зірвалась Риба теж хоче жити. Як і ти. | ^зірвалась The fish wants to live too. Like you.
13 | ^зірвалась Не кричи. Наступна почує. | ^зірвалась Don't shout. The next one will hear.
14 | ^смажити Смажити. Ми ж не дикуни. | ^смажити Fried. We're not savages.
15 | ^смажити Тобі сира, {name} — смажена. Всі щасливі. | ^смажити Raw for you, fried for {name}. Everyone's happy.
16 | ^поплавець То вітер. Не галасуй. | ^поплавець That's the wind. Don't make a fuss.
17 | ^поплавець Тягне {name}, а не ти. Сиди. | ^поплавець {name} pulls, not you. Sit.
18 | ^чекати Рибалка — то терпіння. У тебе його нема. | ^чекати Fishing is patience. You have none.
19 | ^чекати Мовчи, і вона прийде. | ^чекати Be quiet, and it'll come.
20 | ^кістка Не жери так жадібно. | ^кістка Don't gobble so greedily.
21 | ^кістка Це покарання. За жадібність. | ^кістка That's punishment. For greed.
22 | ^глибоко Кажуть. Не будемо перевіряти. | ^глибоко So they say. Let's not check.
23 | ^глибоко Є. І вона дуже любить воронів. | ^глибоко There is. And it's very fond of ravens.

### M @м_риба_кінець   (used by scenes: fish)
0 | ^перша Хвіст? Хвіст, {м_слівце}?! Я тобі зара дам хвіст. | ^перша The tail? The tail, {м_слівце}?! I'll give you a tail.
1 | ^мала Сам ти нечесний. І дрібний. | ^мала You're the dishonest one.
2 | ^ще Терпіння — то для ситих. | ^ще Patience is for those who aren't hungry.
3 | ^луска Личить? Я тобі зара так прикрашу, {м_слівце}, — мама рідна не впізнає. | ^луска Suits me? I'll decorate you right now, {м_слівце}.
4 | ^ще Ага, {name}, значить. А мені шо, водичка? | ^ще Oh, so it's for {name}. And for me what, water?
5 | ^велика Поважати рибу? Я її зара зжеру, ото й вся повага. | ^велика Respect the fish? I'm about to gobble it.
6 | ^черв'як Я тебе сам наживлю, ботан. На гачок і в воду. | ^черв'як I'll bait anyone you like myself.
7 | ^зірвалась Хоче жити — хай не клює, падла. | ^зірвалась If it wants to live — it shouldn't bite.
8 | ^смажити Самі ви дикуни. Сира — то смак, шариш? | ^смажити You're the savages. Raw is taste.
9 | ^поплавець Сиджу, сиджу. Але смикнулось, я бачив, бля! | ^поплавець I'm sitting. But it twitched, I saw it.
10 | ^чекати Є в мене терпіння. Маленьке. Було. Скінчилось. | ^чекати I do have patience. A little. And it's run out.
11 | ^кістка Жадібність — то по життю, ботан. Не вчи. | ^кістка Greed is a way of life.
12 | ^глибоко Хай спробує. Я їй зсередини печінку видзьобаю, суці. | ^глибоко Let it try. I'll peck out its liver from the inside.

### H @х_рубка   (used by scenes: chop)
0 | ^ліс Ще одне дерево впало. Ліс плаче. | ^ліс One more tree fell. The forest weeps.
1 | ^сидів Хтось колись на цьому дереві сидів. Може, ми. | ^сидів Someone once sat on this tree. Maybe us.
2 | ^берегись Берегись! Падає! Ну, майже. | ^берегись Look out! It's falling! Well, almost.
3 | ^дрова Дрова будуть. Зима — ні. | ^дрова There'll be firewood. Winter — no.
4 | ^тріски Тріски летять, Муніне. Пригнись. | ^тріски Chips are flying, Munin. Duck.
5 | ^білка Білка з того дерева втекла. Дивилась на нас, як на вбивць. | ^білка A squirrel ran from that tree. Looked at us like murderers.
6 | ^смола Смола на пір'ї. Тепер я пахну лісом. | ^смола Resin on my feathers. Now I smell of the forest.
7 | ^пеньок Від дерева лишився пеньок. Як від твого терпіння. | ^пеньок All that's left of the tree is a stump. Like of your patience.
8 | ^гнізда На цьому дереві були гнізда. Чужі, на щастя. | ^гнізда There were nests in this tree. Someone else's, luckily.
9 | ^стук Тук-тук. Як серце лісу. | ^стук Knock-knock. Like the heart of the forest.
10 | ^старе Це дерево старше за нас обох. Було. | ^старе This tree is older than both of us. Was.
11 | ^кора Кора тріщить, як старий корабель. | ^кора The bark cracks like an old ship.
12 | ^луна Луна від сокири на весь ліс. Тепер усі знають, де ми. | ^луна The axe echoes through the whole forest. Now everyone knows where we are.

### M @м_рубка   (used by scenes: chop)
0 | ^ліс Ліс плаче? Хай плаче. Мені дрова треба. | ^ліс The forest weeps? Let it weep. I need firewood.
1 | ^сидів Сидіти? На оцьому? Я на дубах сиджу, а то осика сопляча. | ^сидів Sit? On that? I sit on oaks, and that's an aspen.
2 | ^берегись Майже, бля! Ше трохи — і по тобі б, {м_слівце}. | ^берегись Almost! A little more and it'd be on you, {м_слівце}.
3 | ^дрова Зими не буде? Ти це моїм лапам скажи. | ^дрова No winter? Tell that to my feet.
4 | ^ліс Плаче? Хай. Нам узимку тепло буде, а йому вже похуй. | ^ліс Weeping? Let it. We'll be warm in winter, and it doesn't care anymore.
5 | ^сидів Мені й на плечі добре. Тут подають. | ^сидів I'm fine on the shoulder. They serve me here.
6 | ^берегись Не впало ж. А шкода — було б ржачно. | ^берегись It didn't fall. Pity — it would have been fun.
7 | ^дрова Зима буде. Вона завжди припреться, коли не чекаєш. | ^дрова Winter will come. It always comes when you don't expect it.
8 | ^тріски Я не пригинаюсь. Я ухиляюсь, {м_слівце}. По-бойовому. | ^тріски I don't duck. I dodge, {м_слівце}.
9 | ^тріски Одна вже в мене в пір'ї. Знайду, хто кинув — вб'ю. | ^тріски One is already in my feathers. I'll find who threw it.
10 | ^білка Ну так ми й є вбивці. Дерев, {м_слівце}. | ^білка We ARE murderers. Of trees, {м_слівце}.
11 | ^білка Хай чеше. Наступного разу зжеру, руду. | ^білка Let it run. Next time I'll eat it.
12 | ^смола А я злістю смерджу. Натурально, бля. | ^смола And I smell of anger. A natural scent.
13 | ^смола Смола — то дерево рюмсає. Хай рюмсає, нюня. | ^смола Resin is the tree crying. Let it cry.
14 | ^пеньок Який там пеньок. Від мого терпіння вже ямка лишилась. | ^пеньок My patience isn't a stump anymore, it's a pit.
15 | ^пеньок Пеньок — теж діло. Сісти можна. Насрати можна. | ^пеньок A stump is useful too. You can sit on it.
16 | ^гнізда Чужі? То рубай, нахуй, сміливіше. | ^гнізда Someone else's? Then chop more boldly.
17 | ^гнізда Шкода, яйця не схавали. Сніданок пропав. | ^гнізда Pity, we didn't get breakfast.
18 | ^стук Яке серце, бля? Ліс — то дрова з понтами. | ^стук A heart? A forest is firewood with pretensions.
19 | ^стук Тук-тук. Як у мене в башці від тебе. | ^стук Knock-knock. Like my head because of you.
20 | ^старе Старе — значить трухляве. Як твої жарти, ботан. | ^старе Old means rotten. Like your jokes.
21 | ^старе Було. Тепер — колода. Всі ми колись колоди, йо. | ^старе It was. Now it's a log. Like all of us someday.
22 | ^кора Кора смачна. Для жуків. Не для мене, нє. | ^кора The bark is tasty. For beetles.
23 | ^кора Корабель з неї буде. Чи хата. Чи дрова, курва. Шо вийде. | ^кора A ship will come of it. Or a house. Or firewood, fuck.
24 | ^луна Хай знають. Хай приходять, я їм дам. | ^луна Let them know. Let them come.
25 | ^луна Та ніхто не припреться. Всі мене бояться. Всі! | ^луна Nobody will come. Everyone's afraid of me.

### H @х_рубка_кінець   (used by scenes: chop)
0 | ^ліс Ліс відросте. Ти — ні. | ^ліс The forest will grow back. You won't.
1 | ^сидів Осика теж дерево. Не будь снобом. | ^сидів An aspen is a tree too. Don't be a snob.
2 | ^берегись Я ж казав. Я завжди кажу. | ^берегись I told you. I always tell you.
3 | ^дрова Зате тепло буде. Навіть тобі. | ^дрова But it'll be warm. Even for you.
4 | ^тріски Знайдеш. Він із сокирою. | ^тріски You'll find one. {He|She} has an axe.
5 | ^білка Білка жива — і то добре. | ^білка The squirrel's alive — and that's good.
6 | ^смола Відмиватися будемо довго. | ^смола We'll be washing it off for a long time.
7 | ^пеньок Сідай, але тихо. | ^пеньок Sit, but quietly.
8 | ^гнізда Без гнізда — без проблем. | ^гнізда No nest — no problem.
9 | ^стук Тук-тук. І ще раз. І ще. | ^стук Knock-knock. And again. And again.
10 | ^старе Шануй старших, Муніне. Навіть дерева. | ^старе Respect your elders, Munin. Even trees.
11 | ^кора Дрова — найчесніша доля. | ^кора Firewood is the most honest fate.
12 | ^луна Прийдуть — то не скаржся. | ^луна If they come — don't complain.

### M @м_руда   (used by scenes: mine)
0 | ^стук Стук-стук-стук. Я зара сам когось стукну, {м_слівце}. По голові. | ^стук Knock-knock-knock. I'll knock someone myself in a moment, {м_слівце}.
1 | ^руда Руда. Блищить. Можна спиздити? | ^руда Ore. It shines. Can I steal it?
2 | ^пил Пилюка в очах, в дзьобі, в сраці. Скрізь, бля. | ^пил Dust in my eyes, in my beak, up my ass.
3 | ^іскри Іскри з каменя летять, {м_слівце}. Ше хвіст мені підпалить. | ^іскри Sparks are flying from the stone, {м_слівце}. It'll set my tail on fire.
4 | ^глибина Ше трохи глибше — і в Гель провалимось, {м_слівце}. | ^глибина Any deeper and we'll dig down to Hel, {м_слівце}.
5 | ^кайло Кайло тупе, як твої жарти, розумнику. | ^кайло The pickaxe is as dull as your jokes.
6 | ^мідь Мідь, олово, залізо… А мені шось блискуче буде, чи хуй? | ^мідь Copper, tin, iron… Will there be anything shiny left for me?
7 | ^обвал Якшо тут завалить — я перший звалю. А ти — як знаєш. | ^обвал If it collapses here — I'll be the first to fly out. You — as you like.
8 | ^жила Жила жирна, {м_слівце}. Аж слина капає. | ^жила A nice vein, {м_слівце}. I'm drooling.
9 | ^піт {name} пріє, як свиня в лазні. Я на плечі вже плаваю, бля. | ^піт {name} sweats like a pig. I'm drowning on this shoulder.
10 | ^камінь Камінь, камінь, камінь. Хоч би один дракон, для різнобою, бля. | ^камінь Stone, stone, stone. If only there were one dragon for variety.
11 | ^пітьма Тут темно, як у тебе в голові, {м_слівце}. | ^пітьма It's dark here, like in your ideas, {м_слівце}.

### H @х_руда   (used by scenes: mine)
0 | ^стук Терпи. Хочеш когось стукнути — стукни себе. | ^стук Bear it. If you want to hit someone — hit yourself.
1 | ^стук Стукнеш когось — то тільки себе. Більше нікого нема. | ^стук If you hit someone, it's only yourself. There's nobody else.
2 | ^руда Красти в себе? Ти ж не сорока. | ^руда Steal from yourself? You're not a magpie.
3 | ^руда Блищить — бо мідь. Ти ж не сорока. | ^руда It shines because it's copper. You're not a magpie.
4 | ^пил Чхни. Тільки не на мене. | ^пил Sneeze. Just not on me.
5 | ^пил Пил — то руда, яку ти вдихнув. Тепер ти дорогий. | ^пил Dust is ore you breathed in. Now you're valuable.
6 | ^іскри Не підпалить. Ти мокрий від злості. | ^іскри It won't set you alight. You're wet with anger.
7 | ^іскри Сиди тихо — і хвіст цілий. | ^іскри Sit quietly — and your tail stays whole.
8 | ^глибина Тоді передамо привіт. | ^глибина Then we'll send our regards.
9 | ^глибина Гель руду не віддає. Тому й копаємо обережно. | ^глибина Hel doesn't give up ore. That's why we dig carefully.
10 | ^кайло Тупе кайло теж копає. А мої жарти — смішать. | ^кайло A blunt pickaxe still digs. And my jokes are still funny.
11 | ^кайло Нагостримо його. Бо Мунін ниє. | ^кайло We'll sharpen it. Because Munin whines.
12 | ^мідь Лишиться пил. Блискучий. | ^мідь Dust will remain. Shiny dust.
13 | ^мідь З міді — бронза. З бронзи — сокира. З сокири — твій спокій. | ^мідь Copper makes bronze. Bronze makes an axe. An axe makes your peace.
14 | ^обвал Не завалиться. Сподіваюсь. | ^обвал It won't collapse. I hope.
15 | ^обвал Вилетиш і заблукаєш. Як завжди. | ^обвал You'll fly out and get lost. As always.
16 | ^жила Руду не їдять, Муніне. | ^жила You don't eat ore, Munin.
17 | ^жила Гарна. Не торкайся, бо вкрадеш. | ^жила Pretty. Don't touch it, you'll steal it.
18 | ^піт Це чесний піт. Поважай. | ^піт That's honest sweat. Respect it.
19 | ^піт Хочеш — злети. Ніхто не тримає. | ^піт If you want — fly off. Nobody's holding you.
20 | ^камінь Дракони живуть вище. Не накликай. | ^камінь Dragons live higher up. Don't jinx it.
21 | ^камінь Тобі б тільки битися. | ^камінь All you want is to fight.
22 | ^пітьма Смолоскип є. Ідей — теж. | ^пітьма There's a torch. And ideas too.
23 | ^пітьма Мої ідеї світять. Просто тобі не видно. | ^пітьма My ideas shine. You just can't see them.

### M @м_руда_кінець   (used by scenes: mine)
0 | ^стук Себе? Ну… теж варіант. Нє, тебе краще. | ^стук Myself? Well, that's an option too.
1 | ^руда Я не сорока. Я ворон з принципами. Блискучими, ге-ге. | ^руда I'm not a magpie. I'm a raven with principles. Shiny ones.
2 | ^пил Не на тебе. На тебе — потім. | ^пил Not on you. On you — later.
3 | ^іскри Тихо сидіти — то не про мене, ботан. | ^іскри Sitting quietly isn't my thing.
4 | ^глибина Гель мене не лякає. Мене лякає, як ти, ботан, трусишся над кожним каменем. | ^глибина Hel doesn't scare me. Your caution scares me.
5 | ^кайло Я не ною, бля. Я кажу, як є. | ^кайло I'm not whining. I'm stating facts.
6 | ^мідь Мій спокій — то коли ти заткнешся. | ^мідь My peace is when you're quiet.
7 | ^обвал Ну то вилітати будемо разом. Ти за мною, ботан. | ^обвал Well, then we'll fly out together.
8 | ^жила А я б і зжер, і спиздив. Разом. | ^жила I'd both eat it and steal it. At the same time.
9 | ^піт Я ворон, а не банщик. Сиджу й терплю, як дурень. | ^піт I'm a raven, not a bathhouse attendant. I sit and bear it.
10 | ^камінь А я накличу. Хоч якесь діло буде. | ^камінь I'll jinx it. At least there'll be something to do.
11 | ^пітьма Світять? То чого я ніхуя не бачу? | ^пітьма They shine? Then why can't I see anything?

### H @хд_набіг   (used by scenes: raid)
0 | ^стіна Вони лізуть на стіни, Муніне. Тримайся. | ^стіна They're climbing the walls, Munin. Hold on.
1 | ^скільки Скільки ж їх… Я перестав рахувати. | ^скільки So many of them… I've stopped counting.
2 | ^хата Вони прийшли по наш дім. | ^хата They've come for our home.
3 | ^вогонь_н Бережи вогонь, Муніне. Без нього дім — просто стіни. | ^вогонь_н Guard the fire, Munin. Without it a home is just walls.
4 | ^страх_н Признаюся, Муніне: мені трохи моторошно. | ^страх_н I admit, Munin: I'm a little uneasy.
5 | ^кінець_н Скоро вщухне. Набіги не вічні. | ^кінець_н It'll die down soon. Raids don't last forever.

### M @мд_набіг   (used by scenes: raid)
0 | ^стіна Тримаюсь, тримаюсь. Дзьобом, бля, яким їм зара очі вийму. | ^стіна I'm holding on. To the beak I'll rip them with.
1 | ^стіна Та хай лізуть. Згори якраз зручно в тім'я довбати. | ^стіна Let them climb. It's easier to peck from above.
2 | ^скільки Ти трупи рахуй, ботане, а не живих. Так веселіше ж. | ^скільки Count the corpses, not the living. It's more fun.
3 | ^скільки Дохуя — то й добре. Жрачки буде, {м_слівце}. | ^скільки Many — means there'll be plenty to eat, {м_слівце}.
4 | ^хата По наш? Шо, серйозно? Хто сунеться — додому без очей піде. | ^хата Our home is our den. Whoever climbs in will be left without eyes.
5 | ^хата Дім, бля. Я тут гніздо мостив, мостив, а ці лізуть. Ну шо за люди. | ^хата Home, fuck. I was building a nest here, and they're climbing in.
6 | ^вогонь_н Та бережу. Я на той вогонь дивлюсь, як на сало. Ніхто не візьме. | ^вогонь_н The fire is intact. I look at it like at my food.
7 | ^вогонь_н Хай хтось тіко пальцем до вогню — дзьобом в око, і все, гаплик. | ^вогонь_н Let anyone touch the fire — I'll peck them in the eye.
8 | ^страх_н Мені? Нє. Мені весело, {м_слівце}. Ти шо, зассав? | ^страх_н Not me. I'm having fun, {м_слівце}.
9 | ^страх_н Моторошно? Ну… трохи, бляха. Тіко не кажи нікому, бо дзьобну. | ^страх_н Uneasy? Well, a little. But don't tell anyone.
10 | ^кінець_н Шкода, курва. Я тіко розігрівся. | ^кінець_н Pity. I was just warming up.
11 | ^кінець_н Вщухне — я три дні спатиму. Не будіть мене, ясно? | ^кінець_н When it dies down — I'll sleep for three days.

### H @хд_набіг_кінець   (used by scenes: raid)
0 | ^стіна Тільки не відлітай далеко. | ^стіна Just don't fly far.
1 | ^скільки Спершу вижити. Потім рахуватимемо. | ^скільки First survive. Then we'll count.
2 | ^хата Тоді боронимо разом. | ^хата Then we defend it together.
3 | ^вогонь_н Добре. Хоч хтось пильнує. | ^вогонь_н Good. At least someone's keeping watch.
4 | ^страх_н Нічого. Удвох не так страшно. | ^страх_н It's fine. It's less scary with two.
5 | ^кінець_н Бережи сили. Ще знадобляться. | ^кінець_н Save your strength. You'll need it.

### M @мд_торг   (used by scenes: trade)
0 | ^ціни Ти ці ціни бачив, {м_слівце}? Це ж грабіж, бля, серед білого дня! | ^ціни Did you see these prices, {м_слівце}? Robbery!
1 | ^блискуче Он там на прилавку шось блищить. Хочу. Хочу, кажу! | ^блискуче Something's shining on the counter. I want it.
2 | ^торговець {trader} на мене криво зиркає. Шо я йому зробив, га? | ^торговець {trader} is giving me a funny look.
3 | ^торг Хай скине ціну, бо я сам скину. Разом із прилавком, нахуй. | ^торг Knock the price down, or I'll knock it down myself. Along with the counter.
4 | ^гроші Куди всі бабки ділися? Знов на якийсь мотлох просрали? | ^гроші Where did you put all the coins? On junk again?
5 | ^подарунок А мені шо купите? Хоч кістку якусь, жмоти. | ^подарунок Will you buy me something? At least a bone.

### H @хд_торг   (used by scenes: trade)
0 | ^ціни Бачив. Але товар добрий. | ^ціни I saw. But the goods are good.
1 | ^ціни Ціни як ціни. Ти просто жадібний. | ^ціни Prices are prices. You're just greedy.
2 | ^блискуче Блищить — не значить, що корисне. | ^блискуче Shiny doesn't mean useful.
3 | ^блискуче Хочеш — заробляй. Як усі. | ^блискуче If you want it — earn it. Like everyone.
4 | ^торговець Бо ти на прилавок насрав минулого разу. | ^торговець Because you crapped on his counter last time.
5 | ^торговець Не дивиться. Ти просто підозріливий. | ^торговець He isn't looking. You're just suspicious.
6 | ^торг Торгуватися — мистецтво, Муніне. Не погрози. | ^торг Haggling is an art, Munin. Not threats.
7 | ^торг Тихо. Торгується {name}, не ти. | ^торг Quiet. {name} is haggling, not you.
8 | ^гроші На корисне. Подивишся — сам подякуєш. | ^гроші On something useful. You'll see — and thank me yourself.
9 | ^гроші Монети для того й є, щоб їх віддавати. | ^гроші Coins are there to be given away.
10 | ^подарунок Кісток тут не продають. | ^подарунок They don't sell bones here.
11 | ^подарунок Купимо, як заслужиш. | ^подарунок We'll buy it when you earn it.

### M @мд_торг_кінець   (used by scenes: trade)
0 | ^ціни Жадібний — то торговець, ясно? А я — ощадливий. Різниця, бля. | ^ціни The trader is the greedy one. I'm thrifty.
1 | ^блискуче Корисне — нудне. Блискуче — моє. Шо тут незрозуміло? | ^блискуче Useful is boring. Shiny is mine.
2 | ^торговець Хай зиркає. Я теж криво вмію. І насрати можу ше раз. | ^торговець Let him look. I can look funny too.
3 | ^торг Мистецтво, бля. Моє мистецтво — дзьобом по пиці. | ^торг Art, fuck. My art is the beak.
4 | ^гроші Подякую, як побачу. Поки шось не бачу ніхуя. | ^гроші I'll thank you when I see it. I don't see it yet.
5 | ^подарунок Ну то я сам шось візьму. Тихенько. Ніхто й не допре. | ^подарунок Then I'll take something myself. Quietly.

### H @хд_їзда   (used by scenes: ride)
0 | ^швидко Летимо, як вітер! І навіть не махаю крилами. | ^швидко We're flying like the wind! And I'm not even flapping.
1 | ^сідло Сидіти на плечі вершника — нова для мене висота. | ^сідло Sitting on a rider's shoulder is a new height for me.
2 | ^звір {mount} дихає так важко. Гарний звір. | ^звір {mount_gen} is breathing so heavily. A fine beast.
3 | ^куди_р Куди {name} нас везе на такій швидкості? | ^куди_р Where is {name} taking us at such speed?
4 | ^трясе Трясе. У мене всі думки перемішалися. | ^трясе It's bumpy. All my thoughts are scrambled.
5 | ^пішки Як ми раніше ходили пішки? Не уявляю. | ^пішки How did we ever walk on foot before? I can't imagine.

### M @мд_їзда   (used by scenes: ride)
0 | ^швидко Отак і жити треба, {м_слівце}. Хай везе, а ми сидимо. | ^швидко That's the way to live, {м_слівце}. Let it carry us.
1 | ^швидко Шустро, бля! Аж пір'я вітер чеше. Кайф. | ^швидко Fast, fuck. The wind is combing my feathers.
2 | ^сідло Висота як висота. Тіко трясе, сука, як на возі. | ^сідло A height like any height. But bumpy.
3 | ^сідло Я тут король, ясно? А вершник — то так, підставка. | ^сідло I'm the king here. The rider is under the king.
4 | ^звір Гарний? Та він смердить, як хлів, нахуй. | ^звір A fine one? It stinks like a barn.
5 | ^звір Звір як звір. Зате везе, а не я крильми махаю. | ^звір The beast is all right. At least it carries us.
6 | ^куди_р А похуй куди. Аби не пішки. | ^куди_р Anywhere. As long as not on foot.
7 | ^куди_р Туди, де є кого дзьобнути. Мо'. Я так думаю. | ^куди_р Where there's someone to peck. I hope.
8 | ^трясе Думки? У тебе? Та їх там і не було, професоре. | ^трясе You never had any, wiseguy.
9 | ^трясе Мене теж трясе, бляха-муха. Але весело ж! | ^трясе I'm shaking too. But it's fun.
10 | ^пішки Повзли, як равлики. Згадувати гидко, тьху. | ^пішки We crawled like snails. Disgusting to remember.
11 | ^пішки Пішки — то для ссикунів. Верхи — для пацанів. | ^пішки On foot is for pisspants. In the saddle is for heroes.

### H @хд_їзда_кінець   (used by scenes: ride)
0 | ^швидко Не звикай. Пішки теж треба. | ^швидко Don't get used to it. Walking is needed too.
1 | ^сідло Король, що тримається кігтями. | ^сідло A king who hangs on by his talons.
2 | ^звір Подякуй йому. Подумки. | ^звір Thank it. In your head.
3 | ^куди_р Скоро дізнаємося. | ^куди_р We'll find out soon.
4 | ^трясе Тримаймося міцніше. | ^трясе Let's hold on tighter.
5 | ^пішки Пам'ятай про це, коли злізеш. | ^пішки Remember that when we get down.

### M @мд_піч   (used by scenes: smelt)
0 | ^жар Жарко, курва! Пір'я вже тліє, чуєш? | ^жар Hot, fuck. My feathers are smoldering.
1 | ^дим_п Дим в очі, в дзьоб, у душу, бля. Всюди. | ^дим_п Smoke in my eyes, in my beak, in my soul.
2 | ^руда_п Руду — в піч. А мені шо з того? | ^руда_п Ore into the furnace, and what's in it for me?
3 | ^чекати_п Скільки ше чекати, поки воно, бля, розплавиться? | ^чекати_п How long do we wait until it melts?
4 | ^меч А мені з цього заліза меч скують, га? | ^меч Will they forge me a sword from this iron?
5 | ^коваль Глянь на {name}: у сажі, як чорт із пекла. Ги-ги. | ^коваль {name} is covered in soot, like a blacksmith in hell.

### H @хд_піч   (used by scenes: smelt)
0 | ^жар Відсунься від печі. Не геройствуй. | ^жар Move away from the furnace. No heroics.
1 | ^жар Тепло — то ж добре. Ти ж завжди мерзнеш. | ^жар Warmth is good. You're always cold.
2 | ^дим_п Душа в тебе є? Новина. | ^дим_п You have a soul? That's news.
3 | ^дим_п Терпи. Метал того вартий. | ^дим_п Bear it. The metal is worth it.
4 | ^руда_п Тобі — тепло й компанія. | ^руда_п For you — warmth and company.
5 | ^руда_п Тобі — злиток подивитися. | ^руда_п For you — a bar to look at.
6 | ^чекати_п Скільки треба. Метал не поспішає. | ^чекати_п As long as needed. Metal doesn't hurry.
7 | ^чекати_п Сиди тихо — швидше буде. | ^чекати_п Sit quietly — it'll be faster.
8 | ^меч Ворони мечів не носять. | ^меч Ravens don't carry swords.
9 | ^меч Тобі — кігтеточку. Залізну. | ^меч For you — a scratching post. An iron one.
10 | ^коваль Чесна сажа. Не сміяйся. | ^коваль Honest soot. Don't laugh.
11 | ^коваль Зате руки при ділі. | ^коваль At least the hands are busy.

### M @мд_піч_кінець   (used by scenes: smelt)
0 | ^жар Та відсунувся я. Один хуй пече. | ^жар I moved back. But it still burns.
1 | ^дим_п Терплю, бля. Закоптився весь, як ковбаса. | ^дим_п I'm bearing it. But I'm all smoked.
2 | ^руда_п Дивитися — мало. Дай хоч поклювати, жлоб. | ^руда_п Looking isn't enough. Let me peck it.
3 | ^чекати_п Тихо я не вмію. Але чекаю, бачиш? Чекаю, курва. | ^чекати_п I don't know how to be quiet. But I'm waiting.
4 | ^меч А я носитиму. В дзьобі. Всі обісруться. | ^меч I'll carry it in my beak. It'll be scary.
5 | ^коваль Та не сміюся я. Ну… трохи. Ги. | ^коваль I'm not laughing. Well, a little.

### M @мд_бочка   (used by scenes: ferment)
0 | ^бухло_ф Бочка, Хугіне! Бухло буде, йо-о! | ^бухло_ф A barrel, Hugin! There'll be booze!
1 | ^чекати_ф Скільки ж воно, бля, бродитиме? В мене вже сушняк. | ^чекати_ф How long does it ferment? I'm already thirsty.
2 | ^запах_ф Кислим тхне. Це добре чи хана? | ^запах_ф It smells sour. Is that good or bad?
3 | ^перший Перший ковток — мій. Я заслужив, ясно? | ^перший The first sip is mine. I've earned it.
4 | ^рецепт А хто знає, шо туди сипати? Чи на око, як завжди? | ^рецепт Does anyone know what to put in there? Or by eye?
5 | ^дах_ф Бочку під дах тягніть, бо дощ усе засере. | ^дах_ф Get that barrel under a roof, or the rain will ruin it all.

### H @хд_бочка   (used by scenes: ferment)
0 | ^бухло_ф Медовуха, Муніне. Це зілля, а не бухло. | ^бухло_ф Mead, Munin. It's a potion, not booze.
1 | ^бухло_ф Буде. Але не тобі. | ^бухло_ф There will be. But not for you.
2 | ^чекати_ф Кілька днів. Терпи. | ^чекати_ф A few days. Be patient.
3 | ^чекати_ф Поки не дозріє. Як і ти. | ^чекати_ф Until it's ripe. Like you.
4 | ^запах_ф Добре. Значить, бродить. | ^запах_ф Good. It means it's fermenting.
5 | ^запах_ф Пахне, як ти зранку. | ^запах_ф It smells like you in the morning.
6 | ^перший Перший — {name}. Хто варить, той і п'є. | ^перший {name} first. Whoever brews it drinks it.
7 | ^перший Перший — Одіну. Так годиться. | ^перший Odin first. That's proper.
8 | ^рецепт На око. Як усе в нас. | ^рецепт By eye. Like everything with us.
9 | ^рецепт Рецепт є. Я пам'ятаю. Ну, майже. | ^рецепт There's a recipe. I remember it. Well, almost.
10 | ^дах_ф Слушно. Вперше за день. | ^дах_ф Fair point. First time today.
11 | ^дах_ф Вже під дахом. Не каркай. | ^дах_ф It's under a roof already. Don't croak.

### M @мд_бочка_кінець   (used by scenes: ferment)
0 | ^бухло_ф Зілля, бухло — одна холєра, якшо добре зробити. | ^бухло_ф Potion, booze — same thing, if it's made well.
1 | ^чекати_ф Я й без бочки дозрію. Шо ти мені тут. | ^чекати_ф I'll ripen without a barrel.
2 | ^запах_ф Кисле — то міцне буде. Я шарю. | ^запах_ф Sour means it'll be strong.
3 | ^перший Ну хай другий. Але здоровенний ковток, ясно? | ^перший At least second. But a big sip.
4 | ^рецепт На око — то в мене око гарне. Сиплю, нахуй. | ^рецепт By eye — I have a good eye. Pouring.
5 | ^дах_ф Я завжди діло кажу. Просто ніхто, бля, не слухає. | ^дах_ф I always talk sense. Just quietly.

### H @хд_лагодження   (used by scenes: repair)
0 | ^молоток Стук молотка — найчесніша музика. | ^молоток The sound of a hammer is the most honest music.
1 | ^нове_р Як нове. Ну, майже. | ^нове_р Good as new. Well, almost.
2 | ^подряпина Кожна подряпина — історія. Шкода їх затирати. | ^подряпина Every scratch is a story. A pity to smooth them out.
3 | ^ламати Полагодили. Тепер знову підемо ламати. | ^ламати Repaired. Now we'll go break things again.
4 | ^верстак Верстак гарний. {name} його доглядає. | ^верстак The workbench is nice. {name} takes care of it.
5 | ^ціле Ціле спорядження — спокійний ворон. | ^ціле Intact gear — a calm raven.

### M @мд_лагодження   (used by scenes: repair)
0 | ^молоток Найчесніша музика — то коли ворог верещить. Але й так нічо. | ^молоток The most honest is an enemy's scream. But this isn't bad either.
1 | ^молоток Стук-стук, бля. Голова тріщить, як горіх. | ^молоток Knocking, fuck. My head is splitting.
2 | ^нове_р «Майже» — то в нас по життю так. | ^нове_р Almost — that's our motto.
3 | ^нове_р До першого бою — як нове. Потім знов гівно. | ^нове_р Good as new until the first fight.
4 | ^подряпина Історії — в твій літопис, ботане. А залізо має бути ціле. | ^подряпина Stories go in the chronicle. Iron should be whole.
5 | ^подряпина Мої історії — на ворожих пиках. Шо там та подряпина. | ^подряпина My stories are on enemy faces.
6 | ^ламати Отак, бля, правильно. Лагодиш, шоб ламати. | ^ламати That's right. Repair it so you can break things.
7 | ^ламати Ламати — то я люблю. Це моє. | ^ламати Breaking — that I love.
8 | ^верстак Верстак доглядає, а мене — ні. Обідно, курва. | ^верстак They take care of the workbench, and not of me.
9 | ^верстак Верстак як верстак. Стругає й стругає. | ^верстак A workbench like any workbench. It planes.
10 | ^ціле Спокійний ворон — нудний ворон. Нахуй такий спокій. | ^ціле A calm raven is a boring raven.
11 | ^ціле Спокійний — то ти. А я злий. Завжди. | ^ціле The calm one is you. I'm angry.

### H @хд_лагодження_кінець   (used by scenes: repair)
0 | ^молоток Терпи. Спорядження важливіше. | ^молоток Bear it. The gear matters more.
1 | ^нове_р Значить, бережімо до першого бою. | ^нове_р Then let's keep it safe until the first fight.
2 | ^подряпина Літопис пам'ятає. Залізо — ні. | ^подряпина The chronicle remembers. Iron doesn't.
3 | ^ламати Коло життя вікінга. | ^ламати The circle of a viking's life.
4 | ^верстак Верстак береже нас. А ми — його. | ^верстак The workbench takes care of us. And we of it.
5 | ^ціле Тоді один із нас спокійний. Досить. | ^ціле Then one of us is calm. That's enough.
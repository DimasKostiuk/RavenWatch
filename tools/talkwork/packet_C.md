# Packet C: scenes boss, near_death, death, biome, cross_h, cross_m, walk, sail, friend

Thread prefixes you must use (every thread of a scene starts with its prefix, then a dot): boss → «boss.», near_death → «near_death.», death → «death.», biome → «biome.», cross_h → «cross_h.», cross_m → «cross_m.», walk → «walk.», sail → «sail.», friend → «friend.»

## Scenes (steps; ? = optional)
- boss: H dlg_boss_h = {х_бос}  →  M dlg_boss_m = {м_бос} / {м_бос} {м_лайка}  →  H? dlg_boss_h2 = {х_бос_осадити}
- near_death: M dlg_near_m = {м_ледве} {м_лайка?} / {м_вигук} {м_ледве}  →  H dlg_near_h = {х_ледве}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- death: H dlg_death_h = {х_смерть}  →  M dlg_death_m = {м_смерть} / {м_вигук} {м_смерть}  →  H? dlg_h_close = {х_кінець}
- biome: H dlg_biome_h = {х_біом}  →  M dlg_biome_m = {м_біом} / {м_вигук} {м_біом}  →  H? dlg_h_close = {х_кінець}
- walk: M dlg_walk_m = {м_дорога} / {м_вигук} {м_дорога}  →  H dlg_walk_h = {х_дорога}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- sail: H dlg_sail_h = {х_море}  →  M dlg_sail_m = {м_море} / {м_море} {м_лайка?}  →  H? dlg_h_close = {х_кінець}
- cross_h: H dlg_cross_h = {х_перехід}  →  M dlg_cross_m_reply = {м_перехід_відповідь}  →  H? dlg_h_close = {х_кінець}
- cross_m: M dlg_cross_m = {м_перехід} / {м_вигук} {м_перехід}  →  H dlg_cross_h_reply = {х_перехід_відповідь}  →  M? dlg_m_close = {м_кінець} / {м_кінець} {м_лайка?}
- friend: H dlg_friend_h = {х_друг}  →  M dlg_friend_m = {м_друг} / {м_вигук} {м_друг}  →  H? dlg_friend_h2 = {х_друг_кінець}

## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.

### M @м_кінець   (used by scenes: idle_a, idle_b, idle_c, rest_b, night, near_death, gap_m, walk, cross_m, perch_b)
11 | ^далеко Ага-ага. Аби не по колу, як минулого разу. | ^далеко Well, well. As long as it's not in circles.
12 | ^ноги Я сили бережу. Для бійки. Шо, не можна? | ^ноги I'm saving my strength for a fight.
13 | ^пейзаж Сам ти камінь. Тупий і сірий. | ^пейзаж You're the rock.
15 | ^погода Я не скиглю. Я, бля, коментую. Різниця. | ^погода I'm not whining. I'm commenting.
16 | ^звір Шкода, бляха. Такий смачненький був. | ^звір Pity. It looked tasty.
17 | ^куди Нормальні ворони летять. А ми трусимось на плечі, як мішки. | ^куди Ravens fly, and we're bouncing on a shoulder.
18 | ^смачний Ше глянем, хто кого зжере. | ^смачний We'll see who eats whom.
19 | ^кордон Тоді хоч медовухи. Хоч ковток, ну. | ^кордон Then at least wine. At least a little.
20 | ^знову Ну й виріши{в|ла}, і шо. Я теж вирішив: не заткнусь. | ^знову So {he|she} decided. I decided something too. I won't keep quiet.
21 | ^гірше Я все чую. І мені не страшно. Ані крихти, ясно? | ^гірше I hear everything. And I'm not scared.
22 | ^стомився Ну… добре, помовчимо. Хвилинку. Одну. | ^стомився Well… we'll be quiet. For a minute.
30 | ^валькірія Нехай ше прилетить. Я їй таку дорогу покажу — забуде, як звати. | ^валькірія She'll come again. I'll show her the way.
31 | ^доб'ю Нічо, прогодуюсь. Тобою, ботан. | ^доб'ю I'll feed myself. With you.
32 | ^п'яти Є в мене серце. Маленьке. Чорне. І злюще. | ^п'яти I do have a heart. Small and black.
33 | ^всрався Не ржи, бо ти теж пискнув. Я чув. | ^всрався Don't laugh. You squeaked too.
34 | ^зілля Логіка, {м_слівце}, ага. Наступного разу сам носи, розумний. | ^зілля Logic, {м_слівце}. Next time carry it yourself.
35 | ^гордість Кабан був здоровенний! Я бачив! Як хата! | ^гордість The boar was huge. I saw it.
36 | ^весело Болить — то ше веселіше, ге-ге. | ^весело When it hurts it's even more fun.
37 | ~3 ^лякай Не записуй! Бо видзьобаю, чуєш? | ~3 ^лякай Don't write it down. Or I'll peck it out.
60 | ^пісня Двадцять один, ботан. Рахувати навчись спершу. | ^пісня Twenty-one, wiseguy. You count badly.
61 | ^сліди Відкриті, а як же. Я завжди на шухері. | ^сліди Open. I'm always ready.
62 | ^ще То я поспішу, професоре. Шоб скоріше той кінець. | ^ще Then I'll hurry, wiseguy.
67 | ^нюх Я жеру все, шо не втекло. І шо втекло — теж, якшо догоню. | ^нюх I eat everything that didn't run away.
68 | ^стовп Нашо мені стовп? Я межу носом чую, бля. | ^стовп I don't need a post. I smell the border with my nose.
69 | ^тут Хай знайде. Пошкодує, сука. | ^тут If it finds me — it'll regret it.
70 | ^назад Гівно, але своє, рідне. | ^назад Shit, but our own.
71 | ^погода Мокрий, бо ти мене під дощ потяг, розумнику. | ^погода Wet because you dragged me into the rain.
72 | ^слід Біда — то моє друге ім'я, ботан. А перше — Мунін. | ^слід Trouble is my middle name.
73 | ^дерева Коріння, поради — все одно криве, як не знаю шо. | ^дерева Roots, advice — all crooked anyway.
74 | ^край Ну то пішли далі. Бурчу, бля, але йду. | ^край Then let's go on. I grumble, but I go.

### H @х_кінець   (used by scenes: idle_b, fog, death, biome, fed_munin, gap_h, sail, cross_h, perch_a)
7 | ^смерть Наступного разу — обережніше. Всі троє. | ^смерть Next time — more carefully. All three of us.
8 | ^біом Запам'ятаю дорогу. Хтось же має. | ^біом I'll remember the way. Someone has to.
9 | ^спокій Тоді не буди його своїм каркотінням. | ^спокій Then don't wake him with your cawing.
10 | ^обрій Якщо змій — ти перший у пащу. | ^обрій If it's a serpent — you go into its jaws first.
11 | ^хитає Тільки не на мене, Муніне. | ^хитає Just not on me, Munin.
12 | ^хитає Тримайся за щоглу. І за язик. | ^хитає Hold on to the mast. And to your tongue.
13 | ^борт Мудрість не тоне. Вона спливає. | ^борт Wisdom doesn't sink. It floats.
14 | ^люблю Знаю. Кричи. Море стерпить. | ^люблю I know. Shout. The sea can take it.
17 | ^риба Ти завжди голодний. | ^риба You're always hungry.
18 | ^змій Тоді битимешся сам. | ^змій Then you'll fight alone.
19 | ^змій Вплав. Ти ж качка, сам казав. | ^змій Swim for it. You're a duck, you said so yourself.
20 | ^буря Весело буде, коли ти змокнеш. | ^буря It'll be fun when you get soaked.
21 | ^берег Значить, пливемо далі. | ^берег Then we sail on.
22 | ^запах Звикнеш. Ти до всього звикаєш. | ^запах You'll get used to it. You get used to everything.
23 | ^тихо Скоро й тут буде тихо. Після тебе. | ^тихо Soon it'll be quiet here too. After you.
24 | ^пильно Хай так. Аби дивився, а не каркав. | ^пильно So be it. As long as you watch and don't croak.
25 | ^межа Межі — для тих, хто ходить. Ми ж летимо. | ^межа Borders are for those who walk. We fly.
26 | ^додому Так. Нарешті. | ^додому Yes. Finally.
27 | ^обрій Дім. Так. Колись. | ^обрій Home. Yes. Someday.
38 | ^речі Без геройства. Чуєш, {name}? | ^речі No heroics. Hear me, {name}?
39 | ^ліжко Пиятику ще відгуляємо. Живими. | ^ліжко We'll have our feast yet. Alive.
40 | ^звірі Аби вони про це не дізналися першими. | ^звірі As long as they don't find out about it first.
41 | ^мапа Красиво відступати теж треба вміти. | ^мапа Retreating gracefully is a skill too.
42 | ^чайки Летючі щури хоча б знають дорогу. | ^чайки At least flying rats know the way.
43 | ^повітря Легкий, як ковадло. | ^повітря Light as an anvil.
51 | ^звірина Дивись, щоб ми самі не стали тут новою їжею. | ^звірина Watch out that we don't become the new food here.
52 | ^барви Мені подобається. Навіть якщо тобі — ні. | ^барви I like it. Even if you don't.
53 | ^подих Тримайся, бо здме. | ^подих Hold on, or you'll be blown away.
54 | ^бували Цього разу буде краще. | ^бували This time it'll go better.
55 | ^від Звикнемо. Ми скрізь звикаємо. | ^від We'll get used to it. We get used to everywhere.
56 | ^карта Повернемося. Рано чи пізно. | ^карта We'll be back. Sooner or later.
57 | ^пташки Хай співають. Тихіше на душі. | ^пташки Let them sing. It eases the soul.
58 | ^нове Обережно — і разом. | ^нове Carefully — and together.
66 | ^валькірії Цього разу полетіли без нас. | ^валькірії This time they flew off without us.
67 | ^помилка Наступного разу — обережніше. Усі. | ^помилка Next time — more carefully. All of us.
68 | ^могила Колись їх стане менше. Сподіваюся. | ^могила Someday there'll be fewer of them. I hope.
69 | ^холод_с Тепер тепло. Ходімо. | ^холод_с Now it's warm. Let's go.
70 | ^помста Помстимося. Але з розумом. | ^помста We'll take revenge. But wisely.
71 | ^знову_с Головне — разом. | ^знову_с What matters is together.
72 | ^небо_б Запам'ятаю цей колір. | ^небо_б I'll remember this color.
73 | ^рослини Побачимо, що з тобою буде до ранку. | ^рослини We'll see what becomes of you by morning.
74 | ^перші Хай так. Але обережно. | ^перші So be it. But carefully.
75 | ^тиша_край Слухаймо уважно. | ^тиша_край Let's listen closely.
76 | ^дім_далеко Повернемося. Колись. | ^дім_далеко We'll be back. Someday.
77 | ^скарби Спершу виживемо. Потім скарби. | ^скарби First we survive. Then treasures.

### H @х_бос   (used by scenes: boss)
0 | ^бос Непогано впорались із {boss_ins}. | ^бос We handled {boss_gen} not badly.
1 | ^бос {boss} лежить. Одін буде задоволений. | ^бос {boss_gen} is down. Odin will be pleased.
2 | ^бос Ще один трофей на камінь. Голова {boss_gen} гарно там висітиме. | ^бос One more trophy for the stone. The head of {boss_gen} will look good there.
3 | ~1 ^бос Гадав, ми цього разу точно загинемо. | ~1 ^бос I thought this time we'd die for sure.
4 | ~3 ^бос Пишаюся. Нами всіма. | ~3 ^бос I'm proud. Of all of us.
5 | ^трофей Голова {boss_gen} — це добре. Але бачив, скільки там іще всього? | ^трофей The head of {boss_gen} is fine. But did you see how much else is there?
6 | ^шрами {name} {увесь|уся} в подряпинах. Боси так просто не лягають. | ^шрами {name} is all scratched up. Bosses don't go down easily.
7 | ^тиша_б Як тихо стало. Наче світ видихнув. | ^тиша_б How quiet it's become. As if the world breathed out.
8 | ^пісня Про цей бій складуть пісню. Я вже знаю перший рядок. | ^пісня They'll make a song about this fight. I already know the first line.
9 | ^одін_б Одін дивиться. Я відчуваю його погляд. | ^одін_б Odin is watching. I can feel his gaze.
10 | ^біль {name} ледь стоїть. Перемога дорого коштувала. | ^біль {name} is barely standing. The victory cost dearly.
11 | ^наступний Хто наступний, як думаєш? | ^наступний Who's next, do you think?
12 | ^страх_б Зізнаюся: на мить мені стало страшно. | ^страх_б I admit it: for a moment I got scared.

### M @м_бос   (used by scenes: boss)
0 | ^бос «Впорались»? Я тій тварюці око видовбав, а ти шо? Рахував, ботан. | ^бос “Handled”? I pecked its eye out, and you were counting.
1 | ^бос Я бив. Ти пиздів. Все чесно, бля. | ^бос I hit. You bullshitted. A fair split.
2 | ^бос Ше трошки — і я б з {boss_ins} сам впорався, {м_слівце}. Сто пудів. | ^бос A little more and I'd have dealt with {boss_gen} myself, {м_слівце}.
3 | ^бос Бачив, як ця туша гепнулась? Охуєнно! Аж земля гупнула. | ^бос Saw how it crashed down? Fucking awesome.
4 | ~1 ^бос Мало. Давай ше одного. Я тіко розігрівся. | ~1 ^бос Not enough. I want another one.
5 | ~3 ^бос Красиво вийшло, {м_слівце}. Всі молодці. І ти, ботан. Ну, трошки. | ~3 ^бос It turned out beautifully, {м_слівце}. Everyone did well.
6 | ^трофей Бачив. Половина моя. Я ж заслужив, {м_слівце}, чи шо? | ^трофей Saw it. I'll take half for myself. I earned it, {м_слівце}.
7 | ^трофей Всего? Там кров і гівно. Моє улюблене. | ^трофей Everything else? Blood and crap. My favorite.
8 | ^шрами Подряпини — то краса. Мені б такі, бля. | ^шрами Scratches are decoration. I wish I had some.
9 | ^шрами Подряпини загояться. А {boss} — вже ні. Ха! | ^шрами Scratches heal. {boss_gen} doesn't. Ha.
10 | ~2 ^бос Нормально вийшло. Але наступного разу першим б'ю я. І не сперечайся. | ~2 ^бос It went all right. Next time I strike first.
11 | ^тиша_б {boss} лежити, от світ і видихнув. Я теж. І перднув трохи, вибач. | ^тиша_б It breathed out because {boss_gen} is down. I breathed out too.
12 | ^тиша_б Тиша, тиша… Нудно, курва. Давай наступного. | ^тиша_б Silence is boring. Bring on the next one.
13 | ^пісня Перший рядок: «Мунін завалив {boss_gen}». А решта — хуйня, не важлива. | ^пісня First line: “Munin killed {boss_gen}.” The rest doesn't matter.
14 | ^пісня Пісні — для слабаків, братан. Нам — м'ясо. | ^пісня Songs are for weaklings. Meat is for us.
15 | ^одін_б Хай дивиться. Хай повчиться, як треба. | ^одін_б Let him watch. Let him learn.
16 | ^одін_б Одін бачив, хто бив найсильніше. Я. Ну а хто ж іще. | ^одін_б Odin saw who hit hardest. Me.
17 | ^біль Нічо, загоїться. Головне — {boss} лежити, а ми ні. | ^біль Never mind, it'll heal. What matters is {boss_gen} is down.
18 | ^біль Дорого? Та норм. Я б і більше вивалив. | ^біль Dearly? It's fine. I'd have paid more.
19 | ^наступний Та хоч хто. Я готовий, хоч зара. | ^наступний Anyone. I'm ready.
20 | ^наступний Найбільшого давай. Шоб було шо клювати, бля. | ^наступний The biggest one. So there's something to peck.
21 | ^страх_б Мені — ні. Ну… трошки. Трошечки. Відчепись. | ^страх_б Not me. Well, a little.
22 | ^страх_б Страшно, кажеш? Ссикло. Але я теж, якшо чесно. | ^страх_б Scared? Pisspants. But me too.

### H @х_бос_осадити   (used by scenes: boss)
0 | ^бос А ти в цей час ховався. {name} бачи{в|ла}, не відкрутишся. | ^бос And you were hiding the whole time. {name} saw it, you won't wriggle out.
1 | ^бос Я бачив, де ти був. Під кущем. | ^бос I saw where you were. Under a bush.
2 | ^бос Скромніше, Муніне. Скромніше. | ^бос Modesty, Munin. Modesty.
3 | ~3 ^бос Гаразд. Ти теж молодець. Трохи. | ~3 ^бос Fine. You did well too. A little.
4 | ^трофей Тобі — пір'їна з подушки. За хоробрість. | ^трофей For you — a feather from a pillow. For bravery.
5 | ^шрами Тобі б такі — ти б скиглив тиждень. | ^шрами If you had scars like those, you'd whine for a week.
6 | ^тиша_б Насолоджуйся. Такі хвилини рідкісні. | ^тиша_б Enjoy it. Moments like this are rare.
7 | ^пісня Перший рядок — про нашу людину. А тебе — у приспів. | ^пісня The first line is about our human. And you go in the chorus.
8 | ^одін_б Одін бачив усе. Навіть твій кущ. | ^одін_б Odin saw everything. Even your bush.
9 | ^біль Відпочинемо. Заслужили. | ^біль We'll rest. We've earned it.
10 | ^наступний Не поспішай. Спершу перепочинок. | ^наступний Don't hurry. Rest first.
11 | ^страх_б Страх — не сором. Сором — тікати. | ^страх_б Fear is no shame. Running is.

### M @м_ледве   (used by scenes: near_death)
0 | ^валькірія Я вже валькірію бачив, бля. Страшна, як твоя пика. | ^валькірія I already saw a valkyrie. She was as scary as your face.
1 | ^доб'ю Ше раз таке втнеш — я тебе сам доб'ю. Чесно. | ^доб'ю Do that again and I'll finish you off myself.
2 | ^п'яти Серце в п'ятах. Де в ворона п'яти — хто його зна, але там. | ^п'яти My heart is in my heels. Where a raven's heels are, I don't know.
3 | ^всрався Я ледь не всрався. Ну, трошки всрався. | ^всрався I nearly shat myself.
4 | ^зілля Хто, {м_слівце}, зілля забув? Я питаю: хто?! Ти?! | ^зілля Who, {м_слівце}, forgot the potion? I'm asking: who?
5 | ^гордість Тікати — не сором. Сором — то від кабана здохнути, бля. | ^гордість Running isn't shameful. Shameful is croaking to a boar.
6 | ~1 ^весело Ше б секунда — і було б весело. Похорони, все таке. | ~1 ^весело One more moment and it would have been fun.
7 | ~3 ^лякай Не лякай мене так, {м_слівце}. Я ж не залізний. | ~3 ^лякай Don't scare me like that, {м_слівце}.
8 | ^руки В {name} аж руки трусяться. А в мене лапи. Не кажи нікому. | ^руки {name}'s hands are shaking. And my feet.

### H @х_ледве   (used by scenes: near_death)
0 | ^валькірія Я теж її бачив. Вона махала тобі, не мені. | ^валькірія I saw her too. She was waving at you, not me.
1 | ^валькірія Валькірії по таких, як ти, не прилітають. | ^валькірія Valkyries don't come for the likes of you.
2 | ^доб'ю Доб'єш — а хто тебе тоді годуватиме? | ^доб'ю Finish {him|her} off — and who'll feed you then?
3 | ^доб'ю Не погрожуй {name}. Без {нього|неї} ти нікому не потрібен. | ^доб'ю Don't threaten {name}. Without {him|her} nobody needs you.
4 | ^п'яти Там, де в тебе совість, Муніне. Тобто ніде. | ^п'яти Where your conscience is, Munin. That is, nowhere.
5 | ^п'яти У тебе є серце? Оце новина. | ^п'яти You have a heart? That's news.
6 | ^всрався Помітно. Плече ще тепле. | ^всрався It shows. The shoulder is still warm.
7 | ^всрався Не тільки ти, Муніне. Не тільки ти. | ^всрався Not only you, Munin. Not only you.
8 | ^зілля Я нагадував. Двічі. Мене не слухають. | ^зілля I reminded. Twice. Nobody listens to me.
9 | ^зілля Зілля в сумці. Сумка — на базі. Логіка. | ^зілля The potion's in the bag. The bag's at the base. Logic.
10 | ^гордість Від кабана — ні. Від гордості — так. | ^гордість From a boar — no. From pride — yes.
11 | ^гордість Тікати вміти треба. {name} вміє. | ^гордість You have to know how to run. {name} knows.
12 | ^весело Тобі все весело, поки не болить. | ^весело Everything's fun for you until it hurts.
13 | ~1 ^весело Весело — це коли ти мовчиш. | ~1 ^весело Fun is when you keep quiet.
14 | ~3 ^лякай Він злякався. Запишу, бо ніхто не повірить. | ~3 ^лякай He got scared. I'll write it down, because nobody will believe it.
15 | ~3 ^лякай Ми живі. Це головне. | ~3 ^лякай We're alive. That's what matters.
16 | ^доб'ю|всрався|п'яти Ще б мить — і {name} лиши{вся|лася} б тут, а ми б летіли до Одіна самі. | ^доб'ю|всрався|п'яти One more moment and {name} would have stayed here, and we'd be flying to Odin alone.
17 | ^руки Трусяться — значить, живі. Усі троє. | ^руки If they're shaking, we're alive. All three of us.
18 | ^руки Лапи в тебе трусяться завжди. Від злості. | ^руки Your feet always shake. From anger.

### H @х_смерть   (used by scenes: death)
0 | ^смерть Ну от. Знову довелося шукати тіло. | ^смерть There we go. Had to look for the body again.
1 | ^смерть Смерть — не кінець. Але дуже незручно. | ^смерть Death isn't the end. But it's very inconvenient.
2 | ^смерть Запам'ятаємо це місце. Обходитимемо. | ^смерть Let's remember this place. We'll go around it.
3 | ~1 ^смерть Я ж казав. Я завжди кажу. | ~1 ^смерть I told you. I always tell you.
4 | ~3 ^смерть Головне, що {name} поверну{вся|лась}. | ~3 ^смерть What matters is {name} came back.
5 | ^речі Речі лежать там, де впали. Підемо по них, поки хтось не поцупив. | ^речі The things lie where they fell. Let's go get them before someone pinches them.
6 | ^ліжко Ну хоч прокину{вся|лась} у ліжку. Як після доброї пиятики. | ^ліжко Well, at least {he|she} woke up in bed. Like after a good drinking bout.
7 | ^валькірії Валькірії вже кружляли. Я бачив їхні крила. | ^валькірії The valkyries were already circling. I saw their wings.
8 | ^помилка Знаєш, де була помилка? Я знаю. | ^помилка You know where the mistake was? I do.
9 | ^могила Камінь над могилою. Ще один у нашій колекції. | ^могила A stone over a grave. One more for our collection.
10 | ^холод_с Було холодно там, де ми лежали. | ^холод_с It was cold where we lay.
11 | ^помста Хто це зробив, той ще пожалкує. | ^помста Whoever did it will regret it.
12 | ^знову_с Ну що, знову з нуля. | ^знову_с Well, from scratch again.

### M @м_смерть   (used by scenes: death)
0 | ^смерть Тіло шукати — то пів біди. А манатки потім тягти — от де пиздець. | ^смерть Looking for the body is only half the trouble. Carrying its stuff — now that's fucked.
1 | ^смерть Незручно? Ти, {м_слівце}, там не лежав, шоб казати. | ^смерть Inconvenient? You weren't the one lying there, {м_слівце}.
2 | ^смерть Обходити? Я туди вернусь і всіх там поклюю, {м_слівце}. | ^смерть Go around it? I'll go back there and take revenge, {м_слівце}.
3 | ^смерть Ти завжди кажеш. І шо? Хтось тебе слухає? Нє. | ^смерть You always say that. Nobody listens.
4 | ~3 ^смерть Добре, шо верну{вся|лась}, {м_слівце}. Більше так не роби, чуєш? Не роби. | ~3 ^смерть Good that you came back, {м_слівце}. Don't do that again.
5 | ^речі Хто спиздить — того я з'їм, {м_слівце}. Живцем. | ^речі Whoever pinches them — I'll eat, {м_слівце}.
6 | ^речі Підем. Тіко цього разу без геройства, бо знов ляжемо. | ^речі Let's go. Only this time no heroics.
7 | ^ліжко Бухати було б веселіше, {м_слівце}. | ^ліжко A drinking bout would have been more fun, {м_слівце}.
8 | ^ліжко Ліжко — то добре. Могила — гірше. Логіка, ботан. | ^ліжко A bed is good. A grave is worse.
9 | ^валькірії Я б їм пір'я повискубував, курвам крилатим. | ^валькірії I'd pluck their feathers out.
10 | ^валькірії Хай кружляють. Хуй їм, а не нас. | ^валькірії Let them circle. We don't give up.
11 | ^помилка Помилка — шо ти, розумник, не попередив. | ^помилка The mistake was that you didn't warn us.
12 | ^помилка Помилка — то {name}. Завжди. Без варіантів. | ^помилка The mistake is {name}. Always.
13 | ^могила Колекція, бля. Скоро свій цвинтар відкриємо, з табличкою. | ^могила A collection, fuck. Soon we'll open a cemetery.
14 | ^могила Хай стоїть. Пам'ятник дурості. Нашій. | ^могила Let it stand. A monument to stupidity.
15 | ^холод_с Холодно, темно й нудно, як у дупі. Більше туди не хочу. | ^холод_с Cold, dark and boring. I don't want that again.
16 | ^холод_с Та я не лежав. Я сидів і чекав, ясно? | ^холод_с I wasn't lying there. I was sitting and waiting.
17 | ^помста Пошкодує, гнида. Я йому очі виклюю. Обидва. | ^помста They'll regret it. I'll peck their eyes out.
18 | ^помста З розумом, без розуму — пику я вже запам'ятав. | ^помста I've already remembered the mug.
19 | ^знову_с З нуля, бля. Голий, босий і злий як собака. | ^знову_с From scratch, fuck. Naked and angry.
20 | ^знову_с Та не з нуля. Ми ж пам'ятаємо. Я — то точно. | ^знову_с Not from scratch. We remember, after all.

### H @х_біом   (used by scenes: biome)
0 | ^біом {biome_here}. Новий світ — нові способи нас убити. | ^біом {biome_here}. A new world — new ways to kill us.
1 | ^біом Подивись навколо, Муніне. Такого ми ще не бачили. | ^біом Look around, Munin. We've never seen anything like this.
2 | ^біом Я запам'ятаю цю дорогу. Ти — ні. | ^біом I'll remember this road. You won't.
3 | ~1 ^біом Тут навіть повітря недружнє. | ~1 ^біом Even the air here is unfriendly.
4 | ~3 ^біом Гарно. Страшно, але гарно. | ~3 ^біом Beautiful. Scary, but beautiful.
5 | ^звірі Цікаво, які тут звірі. І чи вони нас їдять. | ^звірі I wonder what beasts live here. And whether they eat us.
6 | ^мапа Треба запам'ятати, звідки ми прийшли. Раптом тікати. | ^мапа We should remember where we came from. In case we need to run.
7 | ^небо_б Навіть небо тут іншого кольору. | ^небо_б Even the sky is a different color here.
8 | ^рослини Дивні рослини. Не їж нічого, Муніне. | ^рослини Strange plants. Don't eat anything, Munin.
9 | ^перші Ми тут перші з воронів. Мабуть. | ^перші We're the first ravens here. Probably.
10 | ^тиша_край Тут тихо по-іншому. Насторожено. | ^тиша_край It's quiet here in a different way. Wary.
11 | ^дім_далеко Дім тепер далеко. Чуєш, як далеко? | ^дім_далеко Home is far away now. Feel how far?
12 | ^скарби Тут має бути щось цінне. Нові землі — нові скарби. | ^скарби There should be something valuable here. New lands — new treasures.

### M @м_біом   (used by scenes: biome)
0 | ^біом Нові способи нас вбити? Кайф. Нові пики, шоб клювати. | ^біом New ways to kill us? Fucking awesome. New faces to peck.
1 | ^біом Смердить, як у троля в сраці. Мені подобається, шо. | ^біом It stinks here like a troll's ass. I like it.
2 | ^біом Ти собі запам'ятовуй, а я гляну, кого тут першим клюнути. | ^біом You'll remember it, and I'll find who to peck first here.
3 | ^біом Хто тут головний? Зара дізнаюсь — і насеру йому на голову. | ^біом Who's the boss here? I'll find out and shit on his head.
4 | ~1 ^біом Ми тут всі здохнемо. Зате красиво, {м_слівце}. | ~1 ^біом We're all going to croak here. Beautiful, {м_слівце}.
5 | ~3 ^біом Ну… гарно, {м_слівце}. Тіко нікому, шо я це сказав. | ~3 ^біом Well, it's pretty, {м_слівце}. Don't tell anyone I said so.
6 | ^звірі Жеруть, ага. Але спершу я їх. | ^звірі They do eat. But I eat them first.
7 | ^звірі Тутешні ше не в курсі, хто прийшов, {м_слівце}. Зара будуть. | ^звірі The local beasts don't know yet who has arrived, {м_слівце}.
8 | ^мапа Тікати? Я не тікаю. Я красиво відступаю. Шариш? | ^мапа Run? I don't run. I retreat beautifully.
9 | ^мапа Ти малюй свою мапу. А я гляну, хто тут найсмачніший. | ^мапа Remember it. And I'll see who's the tastiest here.
10 | ^небо_б Колір як колір. Гівняний якийсь. | ^небо_б A color like any color. Shitty.
11 | ^небо_б Гарне, бляха. Тіко нікому не кажи, шо я сказав. | ^небо_б Beautiful, fuck. Don't tell anyone.
12 | ^рослини Пізно, я вже зжер. Нічо, живий. Поки шо. | ^рослини I already ate some. It's fine, I'm alive.
13 | ^рослини Та не жеру, не жеру. Поки шо. | ^рослини I won't eat it. For now.
14 | ^перші Перші — значить, усе наше. Все, чуєш? | ^перші First means everything is ours.
15 | ^перші Перші, бо інші не такі дурні, як ми. | ^перші First, because the others aren't stupid.
16 | ^тиша_край Насторожено, бо знають, шо я прийшов. І правильно. | ^тиша_край Wary because they know I've arrived.
17 | ^тиша_край Тиша, бля. Перед бурею, по-любому. | ^тиша_край Silence, fuck. Before a storm.
18 | ^дім_далеко Чую. І заїбісь. | ^дім_далеко I feel it. And good.
19 | ^дім_далеко Дім, дім… Там хоч жерти було шо. | ^дім_далеко Home, home… At least there was food there.
20 | ^скарби Скарби — мої. Хто перший урвав — той і бере. | ^скарби The treasures are mine. Whoever's first takes them.
21 | ^скарби Блискуче? Де?! Де, курва? Показуй! | ^скарби Shiny? Where?! Show me.

### M @м_дорога   (used by scenes: walk)
0 | ^далеко Куди ми, {м_слівце}, вже пів дня премось? | ^далеко Where the {м_слівце} have we been walking to for half a day?
1 | ^ноги Добре, шо я на плечі. Пішки я б уже давно здох. | ^ноги Good thing I'm on the shoulder. On foot I'd have croaked already.
2 | ^пейзаж Дерево, камінь, дерево. Ше дерево. Охуєнні види, бля. | ^пейзаж Trees, rocks, trees. Fucking awesome views.
3 | ~3 ^далеко Далеко зайшли, {м_слівце}. Аж гордо якось, прикинь. | ~3 ^далеко We've come far, {м_слівце}. It makes me proud.
4 | ^голод Йдемо, а я голодний. Хтось про мене подумав, {м_слівце}? Нє? | ^голод We walk, and I'm hungry. Did anyone think of me, {м_слівце}?
5 | ^погода Йдем, йдем. Хоч би дощ пішов, чи шо. Для розваги. | ^погода Walking, walking. If only it would rain — for entertainment.
6 | ^звір Чую звіра. Мо' завалимо по дорозі, га? | ^звір I smell a beast. Shall we take it down on the way?
7 | ^куди Хугіне, ми взагалі знаємо, куди премось? Чи {name} просто так шляється? | ^куди Hugin, do we know where we're going, or is {name} just strolling?
8 | ^ноги Трясе, як на возі з камінням. Я вже пів пір'я розгубив, бля. | ^ноги It's bumpy like a cart of rocks. I've lost all my feathers.
9 | ~3 ^стомився Ох, далеко. Мені вже навіть матюкатись ліньки. Уяви. | ~3 ^стомився Oh, it's far. Even I'm too lazy to swear.
10 | ^пісня Хугіне, заспівай шось. Тіко не своє, а нормальне. Про бійку. | ^пісня Hugin, give us a song. Not yours, a proper one.
11 | ^сліди Глянь, сліди. Хтось тут ішов до нас. Або тікав. Від мене, мо'. | ^сліди Look, tracks. Someone was coming toward us here. Or running away.

### H @х_дорога   (used by scenes: walk)
0 | ^далеко Туди, де ще не були. Це і є мандри. | ^далеко Where we haven't been yet. That's what travel is.
1 | ^далеко Карту {name} тримає в голові. Сподіваюсь. | ^далеко {name} keeps the map in {his|her} head. I hope.
2 | ^ноги Ти навіть крилами не махаєш. Нахлібник. | ^ноги You don't even flap your wings. Freeloader.
3 | ^ноги Зате {name} іде за трьох. | ^ноги But {name} walks for three.
4 | ^пейзаж Ти просто не вмієш дивитись. | ^пейзаж You just don't know how to look.
5 | ^пейзаж Он той камінь — точнісінько твоя голова. | ^пейзаж That rock over there — exactly your head.
6 | ~3 ^далеко Гордість? Не кажи, що ти розчулився. | ~3 ^далеко Pride? Don't tell me you've gone soft.
7 | ^голод Подумали. Вирішили, що схуднути тобі не завадить. | ^голод We thought about it. Decided losing weight wouldn't hurt you.
8 | ^голод Зловимо щось дорогою. Якщо не розлякаєш лайкою. | ^голод We'll catch something on the way. If you don't scare it off with your swearing.
9 | ^погода Не накликай, Муніне. | ^погода Don't jinx it, Munin.
10 | ^погода Ти ж потім першим скиглитимеш. | ^погода You'll be the first to whine afterward.
11 | ^звір Ми йдемо не полювати, а дійти. | ^звір We're not here to hunt, we're here to get there.
12 | ^звір Звір чує тебе ще здалеку. Від тебе тхне бійкою. | ^звір The beast can hear you from far away. You reek of a fight.
13 | ^куди {name} знає. Мабуть. | ^куди {name} knows. Probably.
14 | ^куди Ворони не питають куди. Ворони летять. | ^куди Ravens don't ask where. Ravens fly.
15 | ^ноги Пір'їни відростуть. Розум — ні. | ^ноги Feathers grow back. Brains don't.
16 | ~1 ^далеко Пів дня — це ще мало. Не ний. | ~1 ^далеко Half a day is nothing yet. Don't whine.
17 | ~3 ^пейзаж Подивись уважніше. Гарно ж. | ~3 ^пейзаж Look closer. It's beautiful.
18 | ~3 ^стомився Це найкраще, що ти сьогодні сказав. | ~3 ^стомився That's the best thing you've said today.
19 | ^стомився Тоді помовчимо разом. | ^стомився Then let's be quiet together.
20 | ^пісня Нормальних пісень ти не знаєш. Ти знаєш лише ту, де «курва» двадцять разів. | ^пісня You don't know any proper songs. You only know the one with “fuck” twenty times.
21 | ^пісня Пісню? Гаразд: ішов ворон, ішов… Ні, ворони не ходять. | ^пісня A song? Fine: a raven was walking, walking… No, ravens don't walk.
22 | ^сліди Втікав. Від тебе, мабуть. | ^сліди It was running away. From you, probably.
23 | ^сліди Свіжі. Тримай очі відкритими. | ^сліди Fresh. Keep your eyes open.

### H @х_море   (used by scenes: sail)
0 | ^спокій Море спокійне. Підозріло спокійне. | ^спокій The sea is calm. Suspiciously calm.
1 | ^обрій Глянь, Муніне, обрій. Там щось є. | ^обрій Look, Munin, the horizon. There's something there.
2 | ^хитає Не дивись униз, Муніне. Хитає. | ^хитає Don't look down, Munin. It's rocking.
3 | ~1 ^борт Якщо впадеш за борт, я не пірнатиму. | ~1 ^борт If you fall overboard, I'm not diving.
4 | ~3 ^люблю Люблю море. Тут ніхто не кричить, крім тебе. | ~3 ^люблю I love the sea. Nobody shouts here, except you.
5 | ^вітер Вітер у спину. Хоч щось сьогодні на нашому боці. | ^вітер Wind at our backs. At least something's on our side today.
6 | ^риба Під нами, мабуть, ціла зграя риби. | ^риба There's probably a whole school of fish under us.
7 | ^змій Якщо зараз вилізе змій, я голосуватиму за втечу. | ^змій If a serpent comes up now, I vote for running.
8 | ^буря Хмари збираються. Якщо буде буря — тримайся за мене. | ^буря Clouds are gathering. If there's a storm — hold on to me.
9 | ^берег Бачиш берег? Я — ні. А я ж бачу все. | ^берег See the shore? I don't. And I see everything.
10 | ~1 ^хитає Тримайся, Муніне. Ти вже зелений, як водорість. | ~1 ^хитає Hold on, Munin. You're already green as seaweed.
11 | ~3 ^обрій Знаєш, на морі думається інакше. Ширше. | ~3 ^обрій You know, at sea you think differently. Wider.
12 | ^чайки Бачиш чайок, Муніне? Значить, берег близько. | ^чайки See the gulls, Munin? That means the shore is close.

### M @м_море   (used by scenes: sail)
0 | ^спокій Підозріло? Та тут кожна хвиля нас зжерти хоче, {м_слівце}, а ти — підозріло. | ^спокій Suspicious? Every wave here wants to eat us, {м_слівце}.
1 | ^спокій Спокійне, бо ше не прокинулось. Почекай трохи. | ^спокій Calm because it hasn't woken up yet. Wait.
2 | ^обрій Обрій, {м_слівце}. Там завжди шось є. Зазвичай змій, бля. | ^обрій The horizon, {м_слівце}. There's always something there. Usually a serpent.
3 | ^обрій Там ніхуя нема, крім води. Вода, вода, вода. Задовбало. | ^обрій There's fuck-all there except water. Water, water, water.
4 | ^хитає Мене не хитає. Мене блювати тягне. Різниця. | ^хитає I'm not rocking. I'm nauseous.
5 | ^хитає Ше раз хитне — наригаю тобі на хвіст. Сто пудів. | ^хитає One more rock and I'll puke on your tail.
6 | ^борт Я? За борт? Це ти в нас від мудрості важкий. Ти й підеш на дно. | ^борт Me? Overboard? You're the one heavy with wisdom — you'll sink.
7 | ~1 ^борт Не пірнатимеш? Та я тебе сам туди скину, ботан. З розгону. | ~1 ^борт You won't dive? I'll throw you in myself.
8 | ^люблю Я кричу, бо мене хитає, {м_слівце}. Ясно тобі? | ^люблю I'm shouting because I'm seasick, {м_слівце}.
9 | ~3 ^люблю Кричу, бо люблю, ботан. Море люблю. Не тебе. Ну, й тебе. | ~3 ^люблю I'm shouting because I love it, wiseguy.
10 | ^вітер Вітер у спину? То ти, професоре, перднув? | ^вітер Wind at our backs? Is that you farting, wiseguy?
11 | ^вітер Хай дме. Аби не в пику, {м_слівце}. | ^вітер Let it blow. As long as it's not in my face, {м_слівце}.
12 | ^риба Риба — то жрачка. Ловіть, {м_слівце}, бо я голодний. | ^риба Fish is food. Catch it, {м_слівце}, I'm hungry.
13 | ^риба Хай пливе. Я її потім гляну, як зголоднію. | ^риба Let it swim. I'll look at it again when I get hungry.
14 | ^змій А я за бійку. В нього луска смачна, {м_слівце}, я чув. | ^змій And I vote for a fight. Its scales are tasty, {м_слівце}.
15 | ^змій Втекти? З корабля? Вплав, чи шо? Ти дурний? | ^змій Run? From a ship? Swim for it, or what?
16 | ^буря Триматись за тебе? Та тебе першого здує, дохляк. | ^буря To you? You'll be the first to blow away, skinny-ribs.
17 | ^буря Буря — то кайф! Хвилі до неба, {м_слівце}! Йо-хо! | ^буря A storm is fun. Waves up to the sky, {м_слівце}!
18 | ^берег Берег там, де нас ше не зжерли. | ^берег The shore is wherever we haven't been eaten yet.
19 | ^берег Нема берега. Є вода і твоя пика. Обидва — гівно. | ^берег There's no shore. There's water and your face.
20 | ~1 ^спокій Тихо, як на цвинтарі. Мені якраз. | ~1 ^спокій Calm as a graveyard. I like it.
21 | ~3 ^хитає Не хитає мене. Це я так танцюю, шо. | ~3 ^хитає I'm not rocking. This is how I dance.
22 | ~3 ^обрій Там дім, ботан. Колись вернемось. Всі троє. | ~3 ^обрій Home is there, wiseguy. Someday we'll go back.
23 | ^обрій Ширше? В тебе й так голова, як вітрило. Надута. | ^обрій Wider? Your head is like a sail as it is.
24 | ^чайки Чайки — то щури з крилами. Ненавиджу, {м_слівце}. | ^чайки Gulls are flying rats. I hate them, {м_слівце}.
25 | ^чайки Або теж заблукали. Як ми, лохи. | ^чайки Or they're lost too. Like us.

### H @х_перехід   (used by scenes: cross_h)
0 | ^запах Земля змінилась. Чуєш, як інакше пахне? | ^запах The land has changed. Smell how different it is?
1 | ^тихо Знаєш, {from_in} було тихіше. | ^тихо You know, {from_in} it was quieter.
2 | ^пильно {biome_here}. Тримай очі відкритими, Муніне. | ^пильно {biome_here}. Keep your eyes open, Munin.
3 | ^межа Ось і межа. Ні стовпа, ні знаку — а все інше. | ^межа And here's the border. No post, no sign — and everything's different.
4 | ~3 ^додому Звідси вже недалеко до дому. Відчуваю. | ~3 ^додому It's not far home from here. I can feel it.
5 | ^повітря Повітря тут інше. Важче. Або це ти на плечі вмостився. | ^повітря The air is different here. Heavier. Or that's you settling on the shoulder.
6 | ^звірина Тут інша звірина. Глянь на сліди. | ^звірина The beasts are different here. Look at the tracks.
7 | ^барви Навіть світло тут інше. Барви не ті. | ^барви Even the light is different here. The colors are off.
8 | ^подих Вітер тут дме з іншого боку. Як і ти. | ^подих The wind blows from the other side here. Like you.
9 | ^бували Ми тут бували раніше. Пам'ятаєш? | ^бували We've been here before. Remember?
10 | ^від {from_in} було спокійніше. А тут — побачимо. | ^від {from_in} it was calmer. Here — we'll see.
11 | ^карта Запам'ятай, де межа. Повертатися будемо. | ^карта Remember where the border is. We'll be coming back.
12 | ^пташки Птахи тут співають інакше. Прислухайся. | ^пташки The birds sing differently here. Listen.
13 | ^нове {biome_here}. Нове завжди небезпечне. | ^нове {biome_here}. The new is always dangerous.

### M @м_перехід_відповідь   (used by scenes: cross_h)
0 | ^запах Пахне так само. Тобою. Фу. | ^запах Smells the same: of you.
1 | ^запах Інакше? Тут смердить, {м_слівце}, ото й уся різниця. | ^запах Different? It stinks here, {м_слівце}.
2 | ^тихо Тихо, бо тут усе вже здохло. Логічно. | ^тихо Quieter, because everything there has already croaked.
3 | ^тихо Нашо мені тиша? Мені бійка треба. | ^тихо I don't need quiet. I need a fight.
4 | ^пильно В мене очі завжди відкриті. Не те шо твої мізки. | ^пильно My eyes are always open. Unlike your mind.
5 | ^пильно Відкриті, ага. Шукаю, кого першим клюнути. | ^пильно Open. Looking for someone to peck first.
6 | ^межа Все інше? Та ті самі дерева, ті самі пики. | ^межа Everything different? The same trees, the same mugs.
7 | ^межа Знак — то я. Велике чорне попередження, бля. | ^межа The sign is me. A big black warning.
8 | ~3 ^додому Дім, {м_слівце}… Аж в пузі тепло стало. Не ржи. | ~3 ^додому Home, {м_слівце}… It even feels cozy.
9 | ^додому Дім? Ну, хоч пожерти дадуть. Нормально. | ^додому Home? Well, at least there'll be grub.
10 | ^повітря Я легкий, як пір'їна. То твоя мудрість важка, ботан. | ^повітря I'm light as a feather. It's your wisdom that's heavy.
11 | ^повітря Важче, бо тут небезпечно. Кайф. | ^повітря Heavier because it's more dangerous here. I like it.
12 | ^звірина Інша? То й шо. Нова — значить, я її ше не куштував. | ^звірина Different means new in taste.
13 | ^звірина Сліди здорові, бля. Оце я розумію — є кого гасити. | ^звірина The tracks are big. I like it.
14 | ^барви Барви-шмарви. Сіре, зелене, гівняне — от і всі барви. | ^барви Colors are colors. Gray, green, shitty.
15 | ^барви Ну… гарно, шо. Ляпнеш комусь, шо я так сказав, — око виклюю. | ^барви Beautiful. Just don't tell anyone I said so.
16 | ^подих Я дму, звідки хочу, {м_слівце}. І шо ти мені зробиш? | ^подих I blow from wherever I want, {м_слівце}.
17 | ^подих Вітер — то мій кент. Такий самий злий і без мізків. | ^подих The wind is my brother. Angry, like me.
18 | ^бували Ага, пам'ятаю. Тут нас мало не зжерли, курва. Класно було. | ^бували I remember. We nearly got eaten here.
19 | ^бували Бували. Шось нічо хорошого не пригадую. Та й похуй. | ^бували We have. I don't remember anything good.
20 | ^від Спокійніше? Та там мухи дохли від нудьги, братан. | ^від Calmer? The flies were dying of boredom there.
21 | ^від Побачимо, ага. І когось завалимо. Сто пудів. | ^від We'll see. And we'll kill someone.
22 | ^карта Та запам'ятав. Межа — де смердить по-іншому. Шариш? | ^карта Remembered. The border is where it stinks differently.
23 | ^карта Вертатися? Нашо? Я ше не нажерся. | ^карта Go back? I haven't eaten my fill yet.
24 | ^пташки Співають, бо не знають, шо я поруч. Зара дізнаються. | ^пташки They sing because they don't know I'm near.
25 | ^пташки Шо там птахи… Я чую тіко, як у мене пузо бурчить. | ^пташки I only hear my stomach growling.
26 | ^нове Небезпечне? Йо, то ж весело! Пішли вже, не сци. | ^нове Dangerous means fun.
27 | ^нове Ну то ти й лізь спереду, професоре. А я прикрию. Ззаду. Здалеку. | ^нове Then you go in front, wiseguy.

### M @м_перехід   (used by scenes: cross_m)
0 | ^смачний О, {biome_here}. Тут по-любому хтось смачний є. | ^смачний Oh, {biome_here}. Someone here must be tasty.
1 | ^кордон Знов кордон. Ні митниці, ні пива. Шо за країна. | ^кордон A border again. No customs, no beer.
2 | ^знову Нашо ми знов сюди премось, {м_слівце}? Нашо? | ^знову Why the {м_слівце} are we dragging ourselves here again?
3 | ^гірше Було {from_in}, стало {biome_here_in}. З гівна в гівно, бля. | ^гірше First {from_in}, now {biome_here_in}. Out of shit and into shit.
4 | ^ще Скільки ше тих країв, {м_слівце}? Де вже той кінець світу? | ^ще How many more of these lands, {м_слівце}? Will there ever be an end of the world?
5 | ^нюх Нюх-нюх… О, нові смаки, {м_слівце}. Шось тут їстівне по-любому. | ^нюх I sniff — and smell new tastes, {м_слівце}.
6 | ^стовп Ні стовпа, ні знака, бля. Хто так межі ставить, га? Руки б повідривав. | ^стовп No post, no sign. Who sets borders like that?
7 | ^тут Все, {biome_here}. Ну, хто тут найзліший? Крім мене, ясно. | ^тут That's it, {biome_here}. Who's the angriest here? Besides me.
8 | ^назад Пішли назад, а? {from_in} хоч гівно було знайоме, рідне. | ^назад Let's go back. {from_in} at least the shit was familiar.
9 | ^погода Погода тут якась не така. Гірша, курва мать. | ^погода The weather is different here. Worse, fuck.
10 | ^слід Йо, хтось тут ходив. Здоровий такий. Хочу на нього глянути. Зблизька. | ^слід Someone walked here. A big one. I want to see it.
11 | ^дерева Дерева тут криві, як твої поради, ботан. | ^дерева The trees here are as crooked as your advice.
12 | ^край Ідемо, ідемо, ідемо… Де той край, бляха? Я вже заїбався. | ^край We walk and walk — and where's that edge?

### H @х_перехід_відповідь   (used by scenes: cross_m)
0 | ^смачний Смачний тут — ти. Для місцевих. | ^смачний The tasty one here is you. For the locals.
1 | ^смачний Спершу роздивись, а потім клюй. | ^смачний Look around first, then peck.
2 | ^кордон Пиво буде вдома. Якщо дійдемо. | ^кордон There'll be beer at home. If we get there.
3 | ^кордон Митниця — це я. Мито — твоя мовчанка. | ^кордон Customs is me. The duty is your silence.
4 | ^знову Бо {name} так виріши{в|ла}. Не сперечайся з господарем. | ^знову Because {name} decided so. Don't argue with the master.
5 | ^знову Знову. Ворони не питають дороги — ворони літають. | ^знову Again. Ravens don't ask the way — ravens fly.
6 | ^гірше Там, звідки ми йшли, теж не було раю. | ^гірше Where we came from wasn't paradise either.
7 | ^гірше Не кажи «гірше». Почують. | ^гірше Don't say “worse.” They'll hear.
8 | ^ще Буде. Там, де ти замовкнеш. | ^ще There will be. Wherever you shut up.
9 | ^ще Кінець світу — то Рагнарок. Не поспішай. | ^ще The end of the world is Ragnarök. Don't rush it.
10 | ^нюх Нові смаки — нові отрути. Не все їж. | ^нюх New tastes — new poisons. Don't eat everything.
11 | ^нюх Нюхай. Але дзьобом не тицяй. | ^нюх Sniff. But don't poke it with your beak.
12 | ^стовп Природа ставила. Їй стовпи не потрібні. | ^стовп Nature set it. It doesn't need posts.
13 | ^стовп Межі видно серцем, Муніне. Ну, тобі — ні. | ^стовп Borders are seen with the heart, Munin. Well, not by you.
14 | ^тут Найзліший тут — ти. І це ненадовго. | ^тут The angriest one here is you. And not for long.
15 | ^тут Не шукай найзлішого. Сам знайде. | ^тут Don't look for the angriest. It'll find you itself.
16 | ^назад Назад не підемо. Вперед цікавіше. | ^назад We won't go back. Forward is more interesting.
17 | ^назад Знайоме гівно — теж гівно. | ^назад Familiar crap is still crap.
18 | ^погода Погода не гірша. Ти просто мокрий. | ^погода The weather isn't worse. You're just wet.
19 | ^погода Звикнемо. Ми до всього звикаємо. | ^погода We'll get used to it. We get used to everything.
20 | ^слід Великий слід — велика біда. Не кличмо. | ^слід A big track means big trouble. Let's not call it.
21 | ^слід Хочеш подивитися? Подивишся. Звідти, з пащі. | ^слід Want to see it? You will. From inside its jaws.
22 | ^дерева Криві дерева — прямі коріння. | ^дерева Crooked trees — straight roots.
23 | ^дерева Мої поради — найрівніші в Мідґарді. | ^дерева My advice is the straightest in Midgard.
24 | ^край Краю нема. Є тільки дорога. | ^край There's no edge. There's only the road.
25 | ^край Край буде. Коли {name} скаже «досить». | ^край There'll be an edge. When {name} says “enough.”

### H @х_друг   (used by scenes: friend)
0 | ^гість {friend} поруч. Поводься пристойно, Муніне. | ^гість {friend} is close. Behave, Munin.
1 | ^разом З {friend} безпечніше. Удвох легше. | ^разом It's safer with {friend}. Easier with two.
2 | ^дивиться {friend} на нас дивиться. Усміхнись. Ой, ти ж не вмієш. | ^дивиться {friend} is looking at us. Smile. Oh, you can't.
3 | ^ворони Цікаво, {friend} має своїх воронів? Сподіваюсь, не таких, як ти. | ^ворони I wonder if {friend} has ravens. Hopefully not like you.
4 | ^подарунок {friend} щось несе. Може, нам? | ^подарунок {friend} is carrying something. Maybe for us?
5 | ^спомин {friend} знову тут. Пам'ятаєш минулий раз? | ^спомин {friend} is here again. Remember last time?
6 | ^частування {friend} може пригостити. Будь чемним, Муніне. | ^частування {friend} might treat us. Be polite, Munin.
7 | ^броня {friend} у новій броні. Блищить. | ^броня {friend} is in new armor. It shines.
8 | ^гомін Удвох вони галасують, як ціла дружина. | ^гомін The two of them are as noisy as a whole war band.
9 | ^допомога {friend} допомагає. Рідкість у цих землях. | ^допомога {friend} is helping. A rarity in these lands.
10 | ^смажене {friend} пахне димом і смаженим. | ^смажене {friend} smells of smoke and roast meat.
11 | ^спина {friend} поруч — хоч спина прикрита. | ^спина With {friend} nearby — at least our back is covered.
12 | ^розмова {friend} щось каже. Слухай, Муніне, може, щось розумне. | ^розмова {friend} is saying something. Listen, Munin, maybe it's something smart.

### M @м_друг   (used by scenes: friend)
0 | ^гість Пристойно? Я гостеві в шолом насеру. Пристойно, ага. | ^гість Behave? I'll shit in the guest's helmet. Politely.
1 | ^гість {friend}? О, ше один, кого можна дзьобнути. | ^гість {friend}? Oh, one more to peck.
2 | ^разом Безпечніше? Та {friend} перш за всіх і дремене, от побачиш. | ^разом Safer? {friend} will be the first to run.
3 | ^дивиться Хай дивиться. Я теж дивлюсь. І думаю всяке погане. | ^дивиться Let them look. I'm looking too. And thinking bad things.
4 | ^ворони Якшо є — наваляю. Обом. | ^ворони If there are — I'll beat them. Both.
5 | ^разом Удвох легше. Тікати, бля. | ^разом Easier with two. To run away.
6 | ^дивиться Хай дивиться, мо' навчиться чогось. Хоч матюкатись. | ^дивиться Let them look. Maybe they'll learn something.
7 | ^ворони Якшо є — хай знають, хто тут на районі головний. | ^ворони If there are — let them know who's boss here.
8 | ^подарунок Якшо не нам — сам заберу. Без питань. | ^подарунок If it's not for us — I'll take it myself.
9 | ^подарунок Нам? Нам ніхто ніхуя не носить, {м_слівце}. | ^подарунок For us? Nobody brings us anything, {м_слівце}.
10 | ^спомин Пам'ятаю. Нічо доброго, бля. | ^спомин I remember. Nothing good.
11 | ^спомин Пам'ятаю. Я тоді пір'їну проїбав. Хорошу пір'їну. | ^спомин I remember. I lost a feather back then.
12 | ^частування Чемним? За жратву — буду. Раз. Цього разу. | ^частування Polite? For food — yes. This time.
13 | ^частування Чемність — то для ситих, ботан. | ^частування Politeness is for the well-fed.
14 | ^броня Блищить? Дай дзьобнути. Хоч разок. | ^броня Shines? Let me peck it.
15 | ^броня Броня гарна. А от те, шо в ній, — так собі. | ^броня The armor is nice. What's inside it — not so much.
16 | ^гомін А я їх перекричу. Одним матюком. | ^гомін And I'll outshout them. With one curse.
17 | ^гомін Хай горлають. Хоч не нудно, бля. | ^гомін Let them make noise. At least it's not boring.
18 | ^допомога Допомагає, бо шось хоче, {м_слівце}. Сто пудів. | ^допомога Helping because they want something, {м_слівце}.
19 | ^допомога Ну хай допомагає. Я не заважатиму. Сильно. | ^допомога Fine, let them help. I won't get in the way.
20 | ^смажене Смаженим? То чого нам нічо не принесли, га? | ^смажене Of roast meat? Then why didn't they bring us any?
21 | ^смажене Я вже слиною капаю, {м_слівце}. | ^смажене I'm already drooling, {м_слівце}.
22 | ^спина Прикрита? Аби ножем не прикрили. | ^спина Covered? As long as it's not with a knife.
23 | ^спина Спину я сам прикрию. Дзьобом, бля. | ^спина I'll cover the back. With my beak.
24 | ^розмова Розумне? Від людей? Ой, не смішіть мене. | ^розмова Something smart? From humans? Don't make me laugh.
25 | ^розмова Я слухаю. Поки шо — хуйня якась. | ^розмова I'm listening. Nothing interesting so far.

### H @х_друг_кінець   (used by scenes: friend)
0 | ^гість Гаразд. Хоч не плюй. | ^гість Fine. At least don't spit.
1 | ^разом Ну хоч не втечеш першим. | ^разом Well, at least you won't be the first to run.
2 | ^дивиться Вже пізно. Все бачили. | ^дивиться Too late. They saw everything.
3 | ^ворони Отож. Тому {friend} без воронів і ходить. | ^ворони Exactly. That's why {friend} goes without ravens.
4 | ^подарунок Ну то хоч не кради при всіх. | ^подарунок Then at least don't steal in front of everyone.
5 | ^спомин Цього разу буде краще. Сподіваюсь. | ^спомин This time it'll go better. I hope.
6 | ^частування Чемність годує. Запам'ятай. | ^частування Politeness feeds you. Remember that.
7 | ^броня Будь чемним до чужої броні. | ^броня Be polite to other people's armor.
8 | ^гомін Галасу й так досить. | ^гомін There's enough noise as it is.
9 | ^допомога Не всі такі, як ти. | ^допомога Not everyone is like you.
10 | ^смажене Ти завжди про їжу. | ^смажене You're always about food.
11 | ^спина Удвох спину тримати легше. | ^спина It's easier to guard a back with two.
12 | ^розмова Зате ти — суцільна цікавість. | ^розмова But you're pure curiosity.
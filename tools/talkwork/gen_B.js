// builds edits_B.json from the new line sets below; old (owned) lines are parsed from packet_B.md
// line format inside each block: MARK | Ukrainian | English
const fs = require('fs'), path = require('path');
const dir = __dirname;
const pk = fs.readFileSync(path.join(dir, 'packet_B.md'), 'utf8').split(/\r?\n/);
const old = {}; let cur = null;
for (const l of pk) {
  const h = /^### ([HM]) @(\S+)/.exec(l); if (h) { cur = h[1] + ':' + h[2]; old[cur] = []; continue; }
  if (cur && /^\d+ \| /.test(l)) { const parts = l.split(' | '); old[cur].push(parts[1].trim()); }
  else if (l.startsWith('#')) cur = null;
}
const P = s => s.split('\n').map(x => x.trim()).filter(x => x && !x.startsWith('//')).map(x => { const a = x.split(' | '); if (a.length !== 3) throw new Error('bad row ' + x); return a.map(y => y.trim()); });

const N = {};
// ===================== REST_A (H open, M reply, H? close) =====================
N['H:х_вогонь'] = P(`
^rest_a.плече | Пам'ятаєш, Муніне, як ми вперше сіли на це плече? | Remember, Munin, when we first sat on this shoulder?
^rest_a.зима | Пам'ятаєш нашу першу зиму? Ти тоді майже змерз. | Remember our first winter? You nearly froze back then.
^rest_a.дрімати | Тепло. Можна й подрімати. | Warm. A good time for a nap.
^rest_a.одін_питав | Колись Одін питав, що ми бачили. Сьогодні я б розповів про вогонь. | Odin used to ask what we saw. Today I'd tell him about the fire.
~1 ^rest_a.дражню | Не лізь до полум'я, Муніне, ти й так смажений на голову. | Don't go near the flames, Munin, your head's fried enough already.
~3 ^rest_a.подобається | Знаєш, мені тут подобається. Не кажи нікому. | You know, I like it here. Don't tell anyone.
^rest_a.казка | Розкажу тобі казку, Муніне. Жив собі ворон, і був він мудрий. Здогадайся, хто. | I'll tell you a tale, Munin. Once there was a raven, and he was wise. Guess who.
^rest_a.іскри | Глянь, як іскри летять. Наче світлячки. | Look how the sparks fly. Like fireflies.
^rest_a.завтра | Завтра знову в дорогу. Цікаво, куди {name} нас поведе. | Tomorrow we're on the road again. I wonder where {name} will lead us.
^rest_a.зорі | Дивись, як зорі над вогнем мерехтять. | Look how the stars flicker above the fire.
^rest_a.мрія | Знаєш, про що я мрію? Про тихий дах і повну миску. | You know what I dream of? A quiet roof and a full bowl.
^rest_a.одін | Цікаво, Одін зараз теж біля вогню сидить? | I wonder if Odin is sitting by a fire right now too.
^rest_a.пісня | Заспівати тобі, Муніне? Тихенько. | Shall I sing for you, Munin? Quietly.
^rest_a.день | Довгий був день. Добре, що закінчився біля вогню. | It was a long day. Good that it ended by the fire.
`);
N['M:м_вогонь'] = P(`
^rest_a.плече>rest_a.гепнувся | Пам'ятаю, ага. Ти з плеча гепнувся. Двічі. Я ржав. | I remember, yeah. You fell off the shoulder. Twice. I was pissing myself.
~1 ^rest_a.плече>rest_a.гепнувся | Пам'ятаю. Ти гепнувся, а я ржав так, шо сам ледь не впав. | I remember. You fell, and I laughed so hard I nearly fell too.
^rest_a.плече>rest_a.кігті | Аякже. Я тоді так кігтями вчепився, шо {name} аж підскочи{в|ла}. | You bet. I dug my claws in so hard {name} jumped.
~3 ^rest_a.плече>rest_a.кігті | Пам'ятаю. Вчепився кігтями — і досі не відчепився, {м_слівце}. | I remember. Dug my claws in, and never let go, {м_слівце}.
~3 ^rest_a.плече>rest_a.часи | Пам'ятаю. Класні були часи. І зара, бля, нічо так. | I remember. Good times. And now isn't fucking bad either.
^rest_a.зима>rest_a.змерз | Я не змерз. Я загартовувався, {м_слівце}. | I didn't freeze. I was toughening up, {м_слівце}.
~1 ^rest_a.зима>rest_a.змерз | Майже змерз? Я дубака тіко зневажав. Не плутай. | Nearly froze? I just looked down on the cold. Get it right.
^rest_a.зима>rest_a.плакав | Пам'ятаю. А ти тоді шмарклі розпустив, як мала дитина. | I remember. And you were snivelling like a little kid.
~1 ^rest_a.зима>rest_a.плакав | Аякже. А ти верещав, як курча недорізане. | You bet. And you were squealing like a half-slaughtered chick.
^rest_a.дрімати>rest_a.сторожу | Спи, ботан. Я посторожу. Мо'. Як не засну. | Sleep, nerd. I'll keep watch. Maybe. If I don't nod off.
^rest_a.дрімати>rest_a.назавжди | Я б тут і заночував. Назавжди. Хай нас усі нахуй забудуть. | I'd spend the night here. Forever. Let the whole fucking world forget us.
~3 ^rest_a.дрімати>rest_a.назавжди | Тепло, як у вовка в пузі. Я б тут і лишився, {м_слівце}. | Warm as a wolf's belly. I'd stay here for good, {м_слівце}.
^rest_a.одін_питав>rest_a.про_мене | Про вогонь? А про мене шо, не розповів би? Я ж тут найцікавіший. | About the fire? And what, nothing about me? I'm the most interesting thing here.
^rest_a.одін_питав>rest_a.драуґр | Про вогонь Одін би заснув, {м_слівце}. Розкажи краще, як я драуґра зробив. | Odin would fall asleep at the fire bit, {м_слівце}. Tell him how I wrecked that draugr.
^rest_a.дражню>rest_a.сам | Сам ти засмажений, {м_слівце}. На голову. | You're the fried one, {м_слівце}. In the head.
^rest_a.дражню>rest_a.мороз | Я до вогню лізу, бо від твоїх жартів аж мороз по пір'ю. | I crawl to the fire because your jokes give my feathers frostbite.
^rest_a.подобається>rest_a.нікому | Нікому не скажу. Тіко всім, {м_слівце}. Гиги. | I won't tell anyone. Just everyone, {м_слівце}. Heh.
^rest_a.подобається>rest_a.мені_теж | Та й мені, бля. Тіко ти теж мовчи, ясно? | Me too, fuck. But you keep your beak shut too, got it?
^rest_a.казка>rest_a.дзьоб | А тоді прилетів інший ворон і дав тому розумнику в дзьоб. Кінець. Гарна казка. | And then another raven flew in and punched the smartass in the beak. The end. Nice tale.
^rest_a.казка>rest_a.допер | Та я вже допер, хто. Казка — гівно, {м_слівце}. | I already got who. The tale is shit, {м_слівце}.
^rest_a.іскри>rest_a.жар | Світлячки? То жар, дурню. Сунь туди дзьоба — сам дізнаєшся. | Fireflies? Those are embers, dumbass. Stick your beak in, you'll find out.
^rest_a.іскри>rest_a.хвіст | Гарно, {м_слівце}. Аби тіко на хвіст мені не сіли. | Pretty, {м_слівце}. As long as they don't land on my tail.
^rest_a.завтра>rest_a.пика | Куди поведе — там комусь і наб'ємо пику. Просто. | Wherever {he|she} leads, someone gets their face smashed. Simple.
^rest_a.завтра>rest_a.гель | Та хоч у Гель. Аби там годували. | Even to Hel. As long as there's grub.
^rest_a.зорі>rest_a.дим | Мерехтять, бо дим, романтик ти пернатий. | They flicker because of the smoke, you feathered romantic.
^rest_a.зорі>rest_a.мовчи | Гарно, бля. Мовчи. Не псуй. | Beautiful, fuck. Shut up. Don't ruin it.
^rest_a.мрія>rest_a.махач | А я — про махач і повну миску. Ну і медовухи бочку. | And I dream of a brawl and a full bowl. And a barrel of mead.
^rest_a.мрія>rest_a.ссикуни | Миска — то добре. А дах — для ссикунів. | A bowl is good. A roof is for pussies.
^rest_a.одін>rest_a.бухає | Сидить і бухає. Як завжди, старий. | Sitting there boozing. Same as always, the old man.
^rest_a.одін>rest_a.думає | Сидить і про нас думає. Ну, про мене. Про тебе шо там думати. | Sitting and thinking about us. Well, about me. What's there to think about you.
^rest_a.пісня>rest_a.спробуй | Тіко спробуй, {м_слівце}. Тіко рота відкрий. | Just try it, {м_слівце}. Just open your mouth.
^rest_a.пісня>rest_a.засну | Співай. Я засну — і всім буде краще. | Sing. I'll fall asleep, and everyone's better off.
^rest_a.день>rest_a.нудний | Довгий і нудний, бляха. Але вогонь — норм вогонь. | Long and boring, damn it. But the fire's a decent fire.
^rest_a.день>rest_a.каторга | Закінчився, бо я заїбався. Не день, а каторга. | It ended because I'm fucking knackered. Not a day, hard labour.
`);
N['H:х_тепло'] = P(`
^rest_a.гепнувся | Двічі. А ти рахував. Отакий ти друг. | Twice. And you counted. Some friend you are.
^rest_a.гепнувся | Падав, бо хтось штовхався. Але ти цього, звісно, не пам'ятаєш. | I fell because someone was shoving. But you don't remember that, of course.
^rest_a.кігті | У {name} досі є сліди від твоїх кігтів. Гарний був початок. | {name} still has the marks of your claws. Lovely beginning.
^rest_a.кігті | Ти й зараз так чіпляєшся. Тільки вже за звичкою. | You still cling like that. Only out of habit now.
^rest_a.часи | Нічо так. Від тебе це майже пісня. | Not bad. From you that's practically a song.
^rest_a.часи | Запам'ятаю, що ти це сказав. Майже без лайки. | I'll remember you said that. Almost without swearing.
^rest_a.змерз | Загартовувався. Із синім дзьобом. | Toughening up. With a blue beak.
^rest_a.змерз | Тому й просидів усю зиму в {name} під каптуром. | That's why you spent the whole winter under {name}'s hood.
^rest_a.плакав | Я плакав від диму. Ти — від холоду. Різниця є. | I cried from the smoke. You from the cold. There's a difference.
^rest_a.плакав | Я хоч плакав тихо. Тебе чув увесь фіорд. | At least I cried quietly. The whole fjord heard you.
^rest_a.сторожу | Як не заснеш. Тобто сторожу я. | If you don't nod off. So, I'm keeping watch.
^rest_a.сторожу | Ти засинаєш раніше, ніж договорюєш «посторожу». | You fall asleep before you finish saying «I'll keep watch».
^rest_a.назавжди | Забудуть — і годувати перестануть. Подумай ще. | Forget us, and stop feeding us. Think again.
^rest_a.назавжди | Назавжди не вийде. Дрова скінчаться. | Forever won't work. The firewood will run out.
^rest_a.про_мене | Розповів би. Коротко: їв, лаявся, спав. | I would. Briefly: ate, swore, slept.
^rest_a.про_мене | Про тебе Одін і так знає. Ти ж кричиш на весь Мідґард. | Odin knows about you already. You shout across all of Midgard.
^rest_a.драуґр | Ти драуґра не зробив. Ти на нього накричав. | You didn't wreck the draugr. You yelled at it.
^rest_a.драуґр | Від твоєї версії Одін заснув би ще швидше. | Your version would put Odin to sleep even faster.
^rest_a.сам | Блискуче. Моїм же жартом. | Brilliant. With my own joke.
^rest_a.сам | Повторив за мною. Уже прогрес. | You repeated after me. That's progress.
^rest_a.мороз | Тоді грійся. Жартів у мене ще багато. | Then warm up. I have plenty more jokes.
^rest_a.мороз | Ну, хоч щось у тобі я зачепив. | Well, at least I got through to something in you.
^rest_a.нікому | Скажеш — і я розповім, як ти бурульку лизав. | Tell them, and I'll tell how you licked an icicle.
^rest_a.нікому | Кажи. Тобі однаково ніхто не вірить. | Go on. Nobody believes you anyway.
^rest_a.мені_теж | Мовчу. Тепер у нас спільна таємниця. | I'm quiet. Now we share a secret.
^rest_a.мені_теж | Ясно. Двоє мовчать про одне. Це вже майже дружба. | Got it. Two keeping quiet about one thing. That's nearly friendship.
^rest_a.дзьоб | Кінець неправильний. Але смішний. | The ending is wrong. But funny.
^rest_a.дзьоб | Другий ворон потім довго тікав. Але це вже інша казка. | The second raven ran for a long time after. But that's another tale.
^rest_a.допер | Допер? Так швидко? Казка вже діє. | Got it? That fast? The tale is working already.
^rest_a.допер | Погана, бо про розумного? Розумію. Не твій жанр. | Bad because it's about someone clever? I see. Not your genre.
^rest_a.жар | Сунь сам. Ти ж усе перевіряєш дзьобом. | Stick your own in. You test everything with your beak.
^rest_a.жар | Я знаю, що жар. Це називається «порівняння». | I know they're embers. It's called a «simile».
^rest_a.хвіст | Обережно з хвостом. Він у тебе один. | Careful with your tail. You only have one.
^rest_a.хвіст | Сядуть — будеш найсвітліший ворон Мідґарду. | If they do, you'll be the brightest raven in Midgard.
^rest_a.пика | Ось тому веде {name}, а не ти. | That's exactly why {name} leads, not you.
^rest_a.пика | Просто. Як і ти. | Simple. Like you.
^rest_a.гель | У Гель годують. Тобою. | They do feed in Hel. On you.
^rest_a.гель | Їжа знайдеться. {name} без неї не ходить. | There'll be food. {name} never goes without.
^rest_a.дим | Знаю, що дим. Ти вмієш зіпсувати будь-яку ніч. | I know it's smoke. You can ruin any night.
^rest_a.дим | Дякую. Тепер вони просто мерехтять у диму. | Thanks. Now they're just flickering in smoke.
^rest_a.мовчи | Мовчу. Бо вперше ти маєш рацію. | I'm quiet. Because for once you're right.
^rest_a.мовчи | Посидимо, поки не згасне. Мовчки. | Let's sit until it goes out. Quietly.
^rest_a.махач | Миска в нас спільна. Решту мрій — окремо. | The bowl we share. The rest of the dreams separately.
^rest_a.махач | Бочка медовухи і ти. Бідний той махач. | A barrel of mead and you. Pity the brawl.
^rest_a.ссикуни | Скажеш це під дощем. Я послухаю. | Say that in the rain. I'll listen.
^rest_a.ссикуни | Тоді спи надворі. Миску я посторожу. | Then sleep outside. I'll guard the bowl.
^rest_a.бухає | Не кажи цього при ньому. Він чує крізь вогонь. | Don't say that near him. He hears through fire.
^rest_a.бухає | Хай п'є. Йому є що забувати. Тебе, наприклад. | Let him drink. He has things to forget. You, for example.
^rest_a.думає | Думає. Що з тобою робити. | He's thinking. About what to do with you.
^rest_a.думає | Про тебе він теж думає. Зітхаючи. | He thinks about you too. With a sigh.
^rest_a.спробуй | Гаразд. Мовчу. Але пісня була б гарна. | Fine. I'll be quiet. But it would have been a nice song.
^rest_a.спробуй | Не відкрию. Заспіваю завтра, коли ти спатимеш. | I won't. I'll sing tomorrow while you sleep.
^rest_a.засну | Отже, пісня корисна. Почну. | So the song is useful. I'll start.
^rest_a.засну | Тоді співатиму довго. Для всіх. | Then I'll sing a long one. For everyone.
^rest_a.нудний | Норм вогонь. Від тебе це похвала. | A decent fire. From you that's praise.
^rest_a.нудний | Завтра буде новий. Може, не такий нудний. | Tomorrow's a new one. Maybe less boring.
^rest_a.каторга | Ти цілий день сидів на плечі. Співчуваю плечу. | You sat on a shoulder all day. My sympathy to the shoulder.
^rest_a.каторга | Відпочивай. Завтра знову втомлюватимешся. | Rest. Tomorrow you'll get tired again.
`);
// ===================== REST_B (M open, H reply, M? close in м_кінець) =====================
N['M:м_вогонь_почин'] = P(`
^rest_b.пиво | Хугіне, ану розкажи, як ти тоді в пиво бовтнувся, {м_слівце}. Гиги. | Hugin, go on, tell how you flopped into the beer that time, {м_слівце}. Heh.
^rest_b.риба | А пам'ятаєш, як ми три дні за рибу гризлись? Ти тоді гівнюк був. І зара такий самий. | Remember how we fought three days over a fish? You were a shithead then. Still are.
^rest_b.спокій | Сидимо. Ні троля, ні бійки. Аж лячно, {м_слівце}. | Here we sit. No troll, no fight. Creepy as hell, {м_слівце}.
~1 ^rest_b.спів | Тіко заспіваєш — я собі вуха в багатті спалю. Чесно. | You start singing, I'll burn my ears off in the fire. Honest.
~3 ^rest_b.подобається | Добре тут, {м_слівце}. Ну. Подобається мені. Не ржи, бо дзьобну. | It's good here, {м_слівце}. Yeah. I like it. Don't laugh or I'll peck you.
^rest_b.шрам | Хугіне, а шо то в тебе за шрам на лапі? Тіко не пизди, шо від дракона. | Hugin, what's that scar on your foot? And don't bullshit me it was a dragon.
^rest_b.мед | Зара б медовухи, {м_слівце}. Цілий ріг. Нє, два. | I could use some mead, {м_слівце}. A whole horn. No, two.
^rest_b.дим | Дим в очі, бля. Спеціально на мене тягне, падлюка. | Smoke in my eyes, fuck. It drifts at me on purpose, the bastard.
^rest_b.історія | Хугін, розкажи шось. Тіко не про мудрість, {м_слівце}, бо засну. | Hugin, tell me something. Just not about wisdom, {м_слівце}, or I'll pass out.
^rest_b.жарина | Жарина вистрибнула! Мало хвоста мені не підсмалила, курва. | An ember jumped out! Nearly singed my tail, the bitch.
^rest_b.нудно | Нудно, {м_слівце}. Пішли когось уб'ємо, га? | Boring, {м_слівце}. Let's go kill someone, huh?
^rest_b.смажене | Смаженим пахне. Нам шось лишиться чи як? | Smells of roast meat. Will there be any left for us or what?
^rest_b.ворони | А інші ворони зара теж біля вогню? Чи мерзнуть десь, бідолахи? | Are other ravens by a fire right now? Or freezing somewhere, poor bastards?
`);
N['H:х_вогонь_відповідь'] = P(`
^rest_b.пиво>rest_b.досліджував | Я не падав у пиво. Я його досліджував. | I didn't fall into the beer. I was studying it.
~1 ^rest_b.пиво>rest_b.досліджував | Я не бовтнувся. Я брав пробу. Глибоку. | I didn't flop. I took a sample. A deep one.
^rest_b.пиво>rest_b.мистецтво | Це було медове вино. І це було мистецтво. | It was mead wine. And it was art.
^rest_b.риба>rest_b.моя | Риба була моя. Я її перший побачив. | The fish was mine. I saw it first.
^rest_b.риба>rest_b.чотири | Три дні? Чотири. І я переміг. | Three days? Four. And I won.
~3 ^rest_b.риба>rest_b.чотири | Чотири дні, а не три. І переміг я. Але гризтися з тобою було весело. | Four days, not three. And I won. But fighting you was fun.
^rest_b.спокій>rest_b.не_каркай | Не каркай. Хай буде спокійно хоч раз. | Don't jinx it. Let it be calm for once.
^rest_b.спокій>rest_b.мовчиш | Підозріло — це коли ти мовчиш. | Suspicious is when you're quiet.
^rest_b.спів>rest_b.краще | Я співаю краще за тебе. Хоч це й не важко. | I sing better than you. Not that it's hard.
^rest_b.спів>rest_b.хотілося | Не співатиму. Хоча хотілося. | I won't sing. Though I wanted to.
^rest_b.подобається>rest_b.не_сміюсь | Не сміюся. Мені теж добре. | I'm not laughing. I feel good too.
^rest_b.подобається>rest_b.посидимо | Не сміюся. Посидимо ще, поки дрова є. | Not laughing. Let's sit a bit more, while there's wood.
~3 ^rest_b.подобається>rest_b.посидимо | І мені. Посидимо ще трохи. Тільки не розкисай. | Me too. Let's sit a while longer. Just don't go soft.
^rest_b.шрам>rest_b.дракон | Від дракона. А що, не віриш? | From a dragon. What, you don't believe me?
^rest_b.шрам>rest_b.від_тебе | Від тебе. Пам'ятаєш, як ти спросоння клюнув? | From you. Remember how you pecked me half asleep?
^rest_b.мед>rest_b.заснеш | Ріг меду — і ти заснеш на моєму хвості. Ні. | One horn of mead and you'll fall asleep on my tail. No.
^rest_b.мед>rest_b.вода | Мед — для тих, хто заслужив. Тобі — вода. | Mead is for those who've earned it. You get water.
^rest_b.дим>rest_b.гарний | Дим іде на найгарнішого. Не знав? | Smoke goes to the handsomest. Didn't you know?
^rest_b.дим>rest_b.пересядь | Пересядь. Хоча дим тебе знайде. Він теж тебе не любить. | Move. Though the smoke will find you. It doesn't like you either.
^rest_b.історія>rest_b.про_тебе | Тоді про тебе. Там мудрості нема. | Then about you. No wisdom there.
^rest_b.історія>rest_b.мовчав | Розкажу про ворона, що мовчав. Вигадка, звісно. | I'll tell about a raven who kept quiet. Fiction, of course.
^rest_b.жарина>rest_b.відсунься | Відсунься трохи. Хвіст у тебе один. | Move back a bit. You only have one tail.
^rest_b.жарина>rest_b.не_любить | Вогонь тебе теж не любить. Не ображайся. | The fire doesn't like you either. No offence.
^rest_b.нудно>rest_b.відпочивають | Біля вогню не вбивають. Біля вогню відпочивають. | Nobody kills by the fire. By the fire we rest.
^rest_b.нудно>rest_b.уві_сні | Спи. Уві сні вбивай скільки хочеш. | Sleep. In your dreams kill as many as you like.
^rest_b.смажене>rest_b.чемно | Лишиться. Якщо чемно попросиш. | There'll be some left. If you ask politely.
^rest_b.смажене>rest_b.поділиться | {name} поділиться. Як завжди. | {name} will share. As always.
^rest_b.ворони>rest_b.пощастило | Хтось біля вогню. Хтось ні. Нам пощастило. | Some sit by a fire. Some don't. We got lucky.
^rest_b.ворони>rest_b.чужі | Чужі ворони — чужий клопіт. | Other ravens are other people's trouble.
`);
// м_кінець: only my lines (prefixed rest_b. / night.)
N['M:м_кінець'] = P(`
^rest_b.досліджував | Досліджував, ага. Два дні потім гикав, дослідник. | Studying, sure. Hiccuped for two days after, the scholar.
^rest_b.досліджував | Ага. Розказуй, розказуй, пиздабол. | Yeah. Keep talking, you bullshitter.
^rest_b.мистецтво | Мистецтво — то коли не тонеш, {м_слівце}. | Art is when you don't drown, {м_слівце}.
^rest_b.мистецтво | Мистецтво, ага. Ти пузирі пускав, митець. | Art, sure. You were blowing bubbles, artist.
^rest_b.моя | Побачив перший, а зжер я. Отак. | You saw it first, and I ate it. That's how.
^rest_b.моя | Твоя? Там на хвості мій дзьоб відбитий. | Yours? My beak print is on its tail.
^rest_b.чотири | Переміг? Ти рибу в болото впустив, {м_слівце}. | Won? You dropped the fish in the swamp, {м_слівце}.
^rest_b.чотири | Переміг він. А чого ж тоді я ситий ходив? | He won. Then why was I the one with a full belly?
^rest_b.не_каркай | Я не каркаю. Я ворон, я так розмовляю. | I'm not jinxing. I'm a raven, that's how I talk.
^rest_b.не_каркай | Хай буде. Але я одним оком пасу. | Fine. But I'm keeping one eye open.
^rest_b.мовчиш | То я не мовчатиму. Шоб тобі спокійно було. | Then I won't shut up. For your peace of mind.
^rest_b.мовчиш | Ну то радій, ботан. Я ше довго не замовкну. | Then rejoice, nerd. I won't shut up for ages.
^rest_b.краще | Краще? Ти співаєш, як колесо скрипить. | Better? You sing like a squeaky wheel.
^rest_b.краще | Тіко не співай, {м_слівце}. Благаю. Я ж ворон, а не камінь. | Just don't sing, {м_слівце}. I'm begging. I'm a raven, not a rock.
^rest_b.хотілося | От і не хоти. Вуха ціліші будуть. | Then don't want to. My ears will thank you.
^rest_b.хотілося | Хотілося йому… Перехочеться, {м_слівце}. | He wanted to… You'll get over it, {м_слівце}.
^rest_b.не_сміюсь | Ну от. Тіко нікому, ясно? | There. Just not a word to anyone, got it?
^rest_b.не_сміюсь | Не смієшся? Підозріло. Але хай. | Not laughing? Suspicious. But fine.
^rest_b.посидимо | Посидимо. Тіко ти мовчи, а то зіпсуєш. | We'll sit. Just keep quiet or you'll spoil it.
^rest_b.посидимо | Давай. Я нікуди й не збирався, {м_слівце}. | Sure. I wasn't going anywhere, {м_слівце}.
^rest_b.дракон | Пиздиш, як Локі. Навіть гірше. | You lie like Loki. Even worse.
^rest_b.дракон | Не вірю. Дракон би тебе одним чихом засмажив. | I don't. A dragon would fry you with one sneeze.
^rest_b.від_тебе | Не пам'ятаю. Але пишаюсь, {м_слівце}. | Don't remember. But I'm proud, {м_слівце}.
^rest_b.від_тебе | Спросоння? То я ше ласкаво клюнув. | Half asleep? That was me being gentle.
^rest_b.заснеш | Ну ти й жлоб. Ріг меду пожалів. | What a cheapskate. Begrudging me one horn of mead.
^rest_b.заснеш | Засну, то й шо? Хвіст у тебе м'який. | So I fall asleep, so what? Your tail is soft.
^rest_b.вода | Вода? Я шо тобі, жаба? | Water? What am I, a frog?
^rest_b.вода | Заслужив я більше за тебе. Я хоч б'юсь, {м_слівце}. | I've earned more than you. At least I fight, {м_слівце}.
^rest_b.гарний | Найгарнішого? А, ну тоді ясно. На мене, кого ж ше. | The handsomest? Ah, then it's clear. At me, who else.
^rest_b.гарний | Ото ж бо. Тебе він десятою дорогою обходить. | Exactly. It goes a mile around you.
^rest_b.пересядь | Не пересяду. Це моє місце, хай дим пересідає. | I'm not moving. This is my spot, let the smoke move.
^rest_b.пересядь | Теж не любить? То нас двоє, {м_слівце}. Ми з димом заодно. | Doesn't like me either? Then the smoke and I are a team, {м_слівце}.
^rest_b.про_тебе | Ха-ха. Смішно. Я аж не засміявся, бачиш? | Ha-ha. Funny. Look how hard I'm not laughing.
^rest_b.про_тебе | Про мене — то тіко хороше, ясно? Або нічо. | About me it's only good stuff, got it? Or nothing.
^rest_b.мовчав | Ворон, шо мовчав? Таких не буває. Вигадка й є. | A raven who kept quiet? No such thing. Fiction indeed.
^rest_b.мовчав | Нудно вже з першого слова. Давай про бійку, {м_слівце}. | Boring from the first word. Tell one about a fight, {м_слівце}.
^rest_b.відсунься | Ну відсунувся. Але вогонь я запам'ятав. Він ше своє отримає. | Fine, I moved. But I'll remember that fire. It'll get what's coming.
^rest_b.відсунься | Один, ага. І той тепер смердить паленим. | One, yeah. And it stinks of burning now.
^rest_b.не_любить | Взаємно. Я в нього зара плюну. | Mutual. I'll spit in it right now.
^rest_b.не_любить | Я не ображаюсь. Я мщу, {м_слівце}. | I don't get offended. I get even, {м_слівце}.
^rest_b.відпочивають | Відпочивають. Ссикуни відпочивають. | Resting. Pussies rest.
^rest_b.відпочивають | Тоді відійдем від вогню. На два кроки. | Then we step away from the fire. Two steps.
^rest_b.уві_сні | Уві сні — то не те, шо наживо. Ну добре, хай буде. | In a dream it's not like the real thing. Oh fine, have it your way.
^rest_b.уві_сні | Уві сні я тебе першого приб'ю, ботан. | In my dream I'll do you first, nerd.
^rest_b.чемно | Чемно? Я? Ну… спробую. Дай шматок. Будь ласка. | Politely? Me? Well… I'll try. Give me a piece. Please.
^rest_b.чемно | Чемно я не вмію. Я вмію швидко. | I can't do polite. I can do fast.
^rest_b.поділиться | Як завжди — то кісткою. А я хочу м'ясо. | As always means a bone. I want meat.
^rest_b.поділиться | Поділиться, бо я дивитимусь отак. Жалібно. | {He'll|She'll} share, because I'll look like this. Pitiful.
^rest_b.пощастило | Пощастило, ага. Тіко не звикай, бо наврочиш. | Lucky, yeah. Don't get used to it or you'll jinx it.
^rest_b.пощастило | Не нам пощастило. То {name} з нами пощастило. | We didn't get lucky. {name} got lucky with us.
^rest_b.чужі | Холодний ти, як риб'яче пузо. | You're cold as a fish belly.
^rest_b.чужі | Ну й гівнюк ти. Я ж так, спитав. | What a shithead. I was just asking.
^night.думки | Думай. А я поки кігті розімну. | You think. I'll limber up my claws.
^night.думки | Думки — то для тих, кому нема кого бить. | Thinking is for those with nobody to hit.
^night.темрява | Я в темряві бачу краще за тебе, сліпий. | I see better than you in the dark, blind boy.
^night.темрява | Ну хоч зранку комусь напихаю. | Well, at least in the morning I'll beat someone up.
^night.голова | Є, ага. Пуста, як барило після свята. | You have one, sure. Empty as a keg after a feast.
^night.голова | А в мене не голова, а кулак. З дзьобом. | Mine's not a head, it's a fist. With a beak.
^night.задумав | Задумав. Але не скажу, {м_слівце}. Сюрприз. | I've got something. Not telling, {м_слівце}. Surprise.
^night.задумав | Нічо я не задумав. Поки шо. | I've got nothing in mind. Yet.
^night.вовк | Не нашого. {name} ше тут, я лапами чую. | Not ours. {name}'s still here, I can feel it with my feet.
^night.вовк | Вовк? То в нього можна відібрать. Я б спробував. | A wolf? Then we can take it off him. I'd give it a go.
^night.сухар | Сухарем? І мовчить, жмот. | A rusk? And keeps quiet about it, the miser.
^night.сухар | Сухар — теж їжа. Хай ділиться, {м_слівце}. | A rusk is food too. Let {him|her} share, {м_слівце}.
^night.хворий | Хворий — то ти. А я голодний. | You're the sick one. I'm hungry.
^night.хворий | Хворий, зате ситим буду. А ти — ні. | Sick, but I'll be fed. You won't.
^night.тонкий | Тонкий, зате розумний. От тебе першим і зжеруть. | Thin, but clever. So they'll eat you first.
^night.тонкий | То за {name} сховаюсь. Там є за чим. | Then I'll hide behind {name}. There's room there.
^night.розкажу | Розказуй. Хто тобі повірить, ботан? | Go on, tell. Who'd believe you, nerd?
^night.розкажу | Розкажеш — я тобі пір'я в хвості перелічу. | Tell anyone and I'll count every feather in your tail. One by one.
^night.ліс | Ліс говорить, а ти перекладаєш. Мовчи вже. | The forest talks and you translate. Just shut up.
^night.ліс | Ага. Слухай ліс. Не мене, а ліс, {м_слівце}. | Yeah. Listen to the forest. Not me, the forest, {м_слівце}.
^night.як_ти | Я не тихий, коли сплю. Я хроплю. Сам казав. | I'm not quiet when I sleep. I snore. You said so.
^night.як_ти | Ти дивишся, як я сплю? Дивак, бля. | You watch me sleep? Fucking weirdo.
^night.скиглиш | Я теж голодний. І я ближче. Тож слухай мене. | I'm hungry too. And I'm closer. So listen to me.
^night.скиглиш | Скиглю, бо ти не годуєш. Вовків хоч зграя годує. | I whine because you don't feed me. At least the pack feeds wolves.
^night.не_підходили | Підійдуть — я їм заспіваю, {м_слівце}. Втечуть. | If they come, I'll sing to them, {м_слівце}. They'll run.
^night.не_підходили | Хай підходять. Я їм носи пообдзьобую. | Let them come. I'll peck their noses off.
^night.здивований | Здивований чим? Моєю красою, сто пудів. | Surprised by what? My good looks, for sure.
^night.здивований | Двоє — то двоє. Я двох подужаю. Мабуть. | Two is two. I can take two. Probably.
^night.курча | Курчам? Йди сам глянь, розумнику. Я тут посиджу. | A chicken? Go look yourself, smartass. I'll sit here.
^night.курча | Світлячки так не моргають, {м_слівце}. Я бачив. | Fireflies don't blink like that, {м_слівце}. I saw.
^night.романтик | Та блохи, зорі — одна хуйня. Дрібне все. | Fleas, stars, same shit. All tiny.
^night.романтик | Романтик, ага. Я ше й вірші про блох знаю. | A romantic, yeah. I know poems about fleas too.
^night.уважніше | Подивився. Чухаються. | I looked. They're scratching.
^night.уважніше | Уважно дивитись — то твоя робота. Моя — дзьобать. | Looking closely is your job. Mine is pecking.
^night.сторож | Сторож… А верещить, як різана. | A watchman… Screeches like it's being butchered.
^night.сторож | Сторож тут я. Хай сова мовчить. | I'm the watchman here. Let the owl shut up.
^night.мудра | Мудра, бо ти так сказав? Ти й себе мудрим кличеш. | Wise because you said so? You call yourself wise too.
^night.мудра | Мудрих я не люблю. Тебе ж якось терплю. | I don't like the wise. Somehow I put up with you.
^night.заблукав | Хай світить. Тіко не на мене, бо дзьобну. | Let it shine. But not on me, or I'll peck it.
^night.заблукав | Я й так не заблукаю. В мене ніс. | I won't get lost anyway. I've got a nose.
^night.не_бери | На всіх? То ше гірше. Нахаба. | At everyone? That's even worse. Cheeky bastard.
^night.не_бери | Беру. Я все беру до серця. І до дзьоба. | I do. I take everything to heart. And to beak.
^night.шия | Притулюсь. Але це нічо не значить, ясно? | I'll snuggle. But it means nothing, got it?
^night.шия | До шиї? Там {name} лоскотно. Ну й хай. | To the neck? It tickles {name} there. Too bad.
^night.вогонь | Скоро — то коли? Бо я в бурульку перетворююсь. | Soon is when? Because I'm turning into an icicle.
^night.вогонь | Скоро. Ти завжди кажеш «скоро». | Soon. You always say «soon».
^night.миша | Поки не зробила. Дзьобну. Шоб знала. | Hasn't yet. I'll peck it. Just so it knows.
^night.миша | Миша — то вечеря, а не друг. | A mouse is supper, not a friend.
^night.змія | Змія? Ну… тоді хай шурхотить собі. | A snake? Well… let it rustle then.
^night.змія | Змію я дзьобну двічі. Для певності. | A snake I'll peck twice. To be sure.
^night.думаю | Думай тихіше, бо я чую, як скрипить. | Think quieter, I can hear it creaking.
^night.думаю | Думаєш? Про мене, сто пудів. | Thinking? About me, for sure.
^night.пильную | Ну то я покимарю. Ти ж однаково пильнуєш. | Then I'll doze. You're on watch anyway.
^night.пильную | Пильнуєш — то пильнуй і мене. Мені нудно. | If you're on watch, watch me too. I'm bored.
^night.смачного | Дякую. Тобі не лишу. | Thanks. I won't save you any.
^night.смачного | Свіже — то кожен може. А гниле — з характером. | Anyone can eat fresh. Rotten has character.
^night.болото | Шкода, бляха. Я б подзьобав. | Damn shame. I'd have pecked a bit.
^night.болото | То ходім до болота. Я швидко. | Then let's go to the swamp. I'll be quick.
`);
// ===================== RAIN (M open, H reply, M? close) =====================
N['M:м_дощ'] = P(`
^rain.мокрий | Пір'я — як ганчірка, шо підлогу мили. | My feathers are like a rag someone mopped the floor with.
^rain.мокрий | Я мокрий, як хвіст у риби. Нє, ше мокріший. | I'm wet as a fish's tail. No, wetter.
^rain.мокрий | Ше трохи — і я качка, {м_слівце}. Кря. | A bit more and I'm a duck, {м_слівце}. Quack.
^rain.замовив | Хто цей дощ замовив? Знайду — заклюю. | Who ordered this rain? I'll find them and peck them to death.
^rain.ссуть | З неба ссуть, а ми сидимо й мовчимо. Терпіли. | The sky's pissing on us, and we just sit and take it. Doormats.
~1 ^rain.ненавиджу | Ненавиджу воду. І тебе. Тебе більше. | I hate water. And you. You more.
^rain.калюжі | Хлюпає під лапами, {м_слівце}. На плечі! Як таке взагалі? | It's squelching under my feet, {м_слівце}. On a shoulder! How is that even possible?
^rain.сохнути | Де сохнути будемо, га? Я питаю: де?! | Where are we drying off, huh? I'm asking: where?!
`);
N['H:х_дощ'] = P(`
^rain.мокрий>rain.комір | Тихіше, від тебе вода тече мені за комір. | Quieter, water is running off you down my collar.
^rain.мокрий>rain.видовище | Мокрий ворон — сумне видовище. Ти — особливо. | A wet raven is a sad sight. You especially.
^rain.мокрий|rain.ненавиджу>rain.цукор | Не ний. Не розмокнеш. | Don't whine. You won't dissolve.
~1 ^rain.мокрий|rain.ненавиджу>rain.цукор | Не ний. Ти не з цукру. | Don't whine. You're not made of sugar.
~3 ^rain.мокрий>rain.ближче | Тримайся ближче, так тепліше. Мокрий, але мій. | Stay closer, it's warmer. Wet, but mine.
~2 ^rain.мокрий|rain.ссуть>rain.мертві | Перетерпи. Мокрі — не мертві. | Bear it. Wet isn't dead.
^rain.мокрий|rain.ссуть>rain.думки | Дощ змиває сліди. І дурні думки. Тобі корисно. | Rain washes away tracks. And stupid thoughts. Good for you.
^rain.мокрий|rain.ссуть|rain.ненавиджу>rain.бурчання | Дощ скінчиться. Твоє бурчання — ні. | The rain will end. Your grumbling won't.
^rain.замовив>rain.небо | Небо. Лети, клюй. Я подивлюсь. | The sky. Fly up and peck it. I'll watch.
^rain.замовив>rain.тор | Тор. Іди, скажи йому. Він любить сміливих. | Thor. Go tell him. He likes the brave.
~1 ^rain.замовив>rain.тор | Тор. Іди, поскаржся. Він тебе вислухає. Блискавкою. | Thor. Go complain. He'll hear you out. With lightning.
^rain.ссуть>rain.не_мовчиш | Мовчимо? Ти не мовчиш. На жаль. | Taking it quietly? You're not quiet. Sadly.
^rain.ссуть>rain.кричи | Покричи на небо. Може, засоромиться. | Shout at the sky. Maybe it'll be ashamed.
^rain.ненавиджу>rain.взаємно | Взаємно. Тільки вода мовчить. | Likewise. Only the water keeps quiet.
^rain.калюжі>rain.в_голові | Хлюпає в тебе в голові. На плечі сухо. | It's sloshing in your head. The shoulder is dry.
^rain.калюжі>rain.тупцюй | Не тупцюй. Бризкаєш. | Stop stomping. You're splashing.
^rain.сохнути>rain.вогонь | Біля вогню. Якщо {name} його розпалить. | By the fire. If {name} lights one.
~3 ^rain.сохнути>rain.вогонь | Біля вогню. Я сам тебе обтрушу, тільки не кричи. | By the fire. I'll shake you dry myself, just stop yelling.
^rain.сохнути>rain.гілка | На вітрі. Як білизна. Розвісимо тебе на гілці. | In the wind. Like laundry. We'll hang you on a branch.
`);
N['M:м_дощ_кінець'] = P(`
^rain.комір | Ну то й шо? Тобі теж треба помитись, ботан. | So what? You need a wash too, nerd.
^rain.комір | Тече, бо я до тебе тулюсь. Терпи. | It runs because I'm leaning on you. Deal with it.
^rain.видовище | На себе глянь. Ти як пацюк, шо з бочки виліз. | Look at yourself. Like a rat that crawled out of a barrel.
^rain.видовище | Сумне, бо ти дивишся. Не дивись, {м_слівце}. | Sad because you're looking. Don't look, {м_слівце}.
^rain.цукор | Не з цукру. З гівна і злості. Ше гірше розмокає. | Not sugar. Shit and spite. Dissolves even worse.
^rain.цукор | Ну… мо' й так. Але все одно хуйня. | Well… maybe. But it's still bullshit.
^rain.ближче | Ближче, ага. Шоб і ти намок. Справедливо. | Closer, yeah. So you get wet too. Fair.
^rain.ближче | Добре, притулюсь. Тіко нікому, {м_слівце}. | Fine, I'll snuggle. Not a word to anyone, {м_слівце}.
^rain.мертві | Не мертві, ага. Поки шо, {м_слівце}. | Not dead, sure. Yet, {м_слівце}.
^rain.мертві | Утішив. Мокрий, але живий. Ура, бля. | Comforting. Wet but alive. Fucking hooray.
^rain.думки | Філософ знайшовся, {м_слівце}. | Look who's a philosopher, {м_слівце}.
^rain.думки | Сам ти мудрий. Мокрий і мудрий. Тьху. | You're the wise one. Wet and wise. Ugh.
^rain.бурчання | Скінчиться, ага. Як і ти колись, професоре. | It'll end, sure. Like you someday, professor.
^rain.бурчання | Бурчання — то моє право. Як у неба — дощ. | Grumbling is my right. Like rain is the sky's.
^rain.небо | І полечу. Тіко хай дощ скінчиться. | And I will. Once the rain stops.
^rain.небо | Небо далеко. А ти близько, ботан. | The sky is far. You're close, nerd.
^rain.тор | Тор? Ну… хай живе поки шо. | Thor? Well… he can live. For now.
^rain.тор | Скажу. Як він сюди спуститься, {м_слівце}. | I will. When he comes down here, {м_слівце}.
^rain.не_мовчиш | Бо хтось має сказати правду. | Because somebody has to tell the truth.
^rain.не_мовчиш | Мовчать терпіли. А я лаюсь. | Doormats keep quiet. I swear.
^rain.кричи | Покричу. А ти не заважай. | I will. And don't you get in the way.
^rain.кричи | Небо засоромиться? Ти шо, грибів наївся? | The sky, ashamed? Have you been eating mushrooms?
^rain.взаємно | Бо вона тупа. А я ні. | Because it's dumb. I'm not.
^rain.взаємно | Вода хоч не вчить мене жити. | At least the water doesn't lecture me.
^rain.в_голові | Сухо? Помацай, ботан. Помацай! | Dry? Feel it, nerd. Go on, feel it!
^rain.в_голові | В голові в мене злість. Вона не хлюпає. | In my head is rage. Rage doesn't slosh.
^rain.тупцюй | Бризкаю, бо можу. І шо ти мені зробиш? | I splash because I can. And what are you gonna do about it?
^rain.тупцюй | Тупцюю, бо лапи мерзнуть, {м_слівце}. | I stomp because my feet are freezing, {м_слівце}.
^rain.вогонь | Розпалить. Я так дивитимусь, шо розпалить. | {He'll|She'll} light one. I'll stare until {he|she} does.
^rain.вогонь | «Якщо». Вічно в тебе «якщо». | «If». It's always «if» with you.
^rain.гілка | Тіко сунься, {м_слівце}. Тіко сунься. | Just try it, {м_слівце}. Just try.
^rain.гілка | Сам на гілці висітимеш. За дзьоб. | You'll hang on a branch yourself. By the beak.
`);
// ===================== FOG (H open, M reply, H? close in х_кінець) =====================
N['H:х_туман'] = P(`
^fog.бачу | Нічого не бачу. А я, між іншим, бачу все. | I can't see anything. And I, by the way, see everything.
^fog.засідки | Туман. Добре для засідок. Погано для нас. | Fog. Good for ambushes. Bad for us.
^fog.одін | У такому тумані навіть Одін заблукав би. | In fog like this even Odin would get lost.
~1 ^fog.шукати | Муніне, якщо загубишся, я шукати не буду. | Munin, if you get lost, I won't look for you.
~3 ^fog.поруч | Тримайся поруч. Не люблю, коли тебе не видно. | Stay close. I don't like it when I can't see you.
^fog.звуки | Чуєш? Кроки. Чи то в тебе в животі. | Hear that? Footsteps. Or your stomach.
^fog.дорога | Ми ще на дорозі? Бо я вже не певен. | Are we still on the road? Because I'm not sure anymore.
^fog.мряка | Туман лізе в пір'я. Холодний, як рука драуґра. | The fog creeps into my feathers. Cold as a draugr's hand.
^fog.силует | Он силует. Дерево чи троль? | There's a silhouette. A tree or a troll?
^fog.голос | Хтось кличе в тумані. Не відповідай. | Someone's calling in the fog. Don't answer.
^fog.тримайся | Тримаймося ближче. Тут легко розгубитися. | Let's stay closer. It's easy to get separated here.
^fog.вогник | Бачиш вогник? Там, у сірому. | See a light? There, in the grey.
^fog.тиша | Туман глушить звуки. Навіть твою лайку. | The fog muffles sounds. Even your swearing.
^fog.мокро | Туман осідає на пір'я. Скоро будемо важкі, як каміння. | The fog settles on our feathers. Soon we'll be heavy as stones.
`);
N['M:м_туман'] = P(`
^fog.бачу>fog.де_я | Все бачиш? Ну то скажи, де я, професоре. Ну? Ну? | You see everything? Then tell me where I am, professor. Well? Well?
^fog.бачу>fog.сліпий | Шо, сліпий тепер, як я? Ласкаво просимо, {м_слівце}. | What, blind like me now? Welcome, {м_слівце}.
^fog.засідки>fog.сам_засідка | Засідки? Та я сам засідка, {м_слівце}. Ходяча. | Ambushes? I'm an ambush myself, {м_слівце}. A walking one.
^fog.засідки>fog.клюнути | Ніхуя не видно. Саме то, шоб когось клюнути зненацька. | Can't see fuck-all. Perfect for pecking someone by surprise.
~2 ^fog.засідки>fog.клюнути | Погано? Нас теж не видно. Клюну першим, і всьо. | Bad? Nobody sees us either. I peck first, done.
^fog.одін>fog.ти_вже | Одін би заблукав. А ти вже, бля, заблукав. | Odin would get lost. And you already fucking are.
^fog.одін>fog.одноокий | Одін і так одним оком бачить. Йому туман — дрібниця. | Odin sees with one eye anyway. Fog's nothing to him.
^fog.шукати>fog.знайду | Не шукатимеш? Та я тебе сам знайду. І дзьобну. | You won't look? I'll find you myself. And peck you.
^fog.шукати>fog.сам_вийду | Та й не треба. Я сам вийду. А ти тут хоч згний, {м_слівце}. | Don't need you to. I'll get out myself. You can rot here, {м_слівце}.
^fog.поруч>fog.тут | Та тут я, тут. Не скигли, засранцю, бо стидно. | I'm here, here. Don't whine, you little shit, it's embarrassing.
^fog.поруч>fog.ніжності | Ой, які ми ніжні. Добре, тримаюсь. Тіко нікому. | Oh, aren't we tender. Fine, I'm holding on. Just tell no one.
^fog.звуки>fog.живіт | То в мене в пузі. Я голодний, а ти — кроки, кроки. | That's my belly. I'm starving and you go footsteps, footsteps.
^fog.звуки>fog.бійка | Кроки. Здоровенні. Ну все, {м_слівце}, зара буде бійка. | Footsteps. Huge ones. That's it, {м_слівце}, there's gonna be a fight.
^fog.дорога>fog.троль | На дорозі, де ж ше. Он камінь, он кущ. Чи то троль. Хуй його знає. | On the road, where else. There's a rock, there's a bush. Or a troll. Fuck knows.
^fog.дорога>fog.болото | Дорога — там, куди {name} пре. Хоч у болото. | The road is wherever {name} barges. Even into a swamp.
^fog.мряка>fog.драуґр | Та краще б драуґр, {м_слівце}. Йому хоч у пику дати можна. А туман шо? | A draugr would be better, {м_слівце}. At least you can punch it. And fog?
^fog.мряка>fog.притулися | Холодно, бля. Ну… притулися, чи шо. Тіко без телячих ніжностей. | Cold, fuck. Well… snuggle up or whatever. No mushy stuff.
^fog.силует>fog.троль_сто | Троль, сто пудів, {м_слівце}. Я печінкою чую. Готуй дзьоба. | A troll, hundred percent, {м_слівце}. I feel it in my gut. Ready your beak.
^fog.силует>fog.дерево | Та дерево. Але я б дзьобнув. Хай знає. | A tree. But I'd peck it anyway. Just so it knows.
^fog.голос>fog.матюком | Та я вже відповів. Матюком. Трьохповерховим. | I already answered. With a three-storey curse.
^fog.голос>fog.гукну | Кличе? То я йому зара так гукну, {м_слівце}, шо сам прибіжить. | It's calling? I'll yell back so hard, {м_слівце}, it'll come running.
^fog.тримайся>fog.пазуха | Я й так на плечі, ало. Ближче — тіко в пазуху. | I'm already on the shoulder, hello. Any closer is inside your shirt.
^fog.тримайся>fog.запах | Розгубимось — знайду тебе по запаху. Ти смердиш книжками, ботан. | If we get split, I'll find you by smell. You stink of books, nerd.
^fog.вогник>fog.пішли | Ну бачу. Пішли туди, хай там хоч дідько з рогами сидить. | Yeah, I see it. Let's go, even if there's a horned devil sitting there.
^fog.вогник>fog.болотний | То болотний вогник, {м_слівце}. Дурнів заманює. Тебе, наприклад. Гиги. | That's a will-o'-the-wisp, {м_слівце}. Lures idiots. Like you. Heh.
^fog.тиша>fog.курва | Нічо не глушить. Слухай сюди: КУРВА. | It doesn't muffle shit. Listen: FUCK.
^fog.тиша>fog.не_подобається | Глушить, бляха. Мені це не подобається, чуєш? | It does muffle, damn it. I don't like it, hear me?
^fog.мокро>fog.злість | Я важкий не від туману. Я від злості важкий. | I'm not heavy from the fog. I'm heavy with rage.
^fog.мокро>fog.пси | Обтрусимось. Як пси. Шо тут думати. | We'll shake it off. Like dogs. Nothing to think about.
`);
N['H:х_кінець'] = P(`
^fog.де_я | На плечі. Справа. Сопиш. | On the shoulder. Right side. Wheezing.
^fog.де_я | Там, де найгучніше. Тебе не треба бачити. | Wherever it's loudest. No need to see you.
^fog.сліпий | Я — на годину. Ти — від народження. | Me, for an hour. You, since birth.
^fog.сліпий | Тимчасово. Не звикай до компанії. | Temporarily. Don't get used to the company.
^fog.сам_засідка | Засідка, що лається вголос. Надійно. | An ambush that swears out loud. Very reliable.
^fog.сам_засідка | Засідка мовчить. Спробуй хоч раз. | An ambush keeps quiet. Try it once.
^fog.клюнути | Аби цим «кимось» не виявився я. | As long as that «someone» isn't me.
^fog.клюнути | Зненацька клюють і нас. Тихо. | They can peck us by surprise too. Quiet.
^fog.ти_вже | Я знаю, де ми. Просто не кажу. | I know where we are. I'm just not saying.
^fog.ти_вже | Заблукав. Разом із тобою. Утіха слабка. | Lost. Together with you. Small comfort.
^fog.одноокий | Одним, але більше, ніж ти двома. | One eye, but more than you see with two.
^fog.одноокий | Йому дрібниця. Нам — ні. Тримайся. | Nothing to him. Not to us. Hold on.
^fog.знайду | Знайдеш — дзьобни. Аби знайшов. | Find me and peck me. As long as you find me.
^fog.знайду | Знайдеш по голосу. Своєму. | You'll find me by the voice. Your own.
^fog.сам_вийду | Вийдеш. Колами. За тиждень. | You'll get out. In circles. In a week.
^fog.сам_вийду | Тоді відпусти моє крило. | Then let go of my wing.
^fog.тут | Тримайся крила. І не каркай. | Stay by my wing. And don't croak.
^fog.тут | Я не скиглю. Я перевіряю, чи ти живий. | I'm not whining. I'm checking you're alive.
^fog.ніжності | Нікому. Туман однаково все ховає. | No one. The fog hides everything anyway.
^fog.ніжності | Домовились. Ти мовчиш, я не бачу. | Deal. You keep quiet, I see nothing.
^fog.живіт | Добре, що хоч твоє бурчання знайоме. | Good that at least your grumbling is familiar.
^fog.живіт | Тоді бурчи тихіше. Справжніх кроків не чую. | Then grumble quieter. I can't hear the real footsteps.
^fog.бійка | Не буде. Ти ще не бачив, хто там. | There won't. You haven't seen what it is yet.
^fog.бійка | Бійка в тумані — це коли б'єш своїх. Стій. | A fight in fog is when you hit your own. Stay put.
^fog.троль | Якщо кущ рухається — це не кущ. | If the bush moves, it's not a bush.
^fog.троль | Тоді йдемо далі. Обережно. | Then we go on. Carefully.
^fog.болото | У болото — це вже не дорога. Це доля. | Into a swamp isn't a road. It's fate.
^fog.болото | Тоді молімося, щоб {name} бачи{в|ла} краще за нас. | Then let's pray {name} sees better than we do.
^fog.драуґр | Туману не даси. Він сам дасть. Застуду. | You can't punch fog. It punches you. With a cold.
^fog.драуґр | Не накликай. Драуґри туман люблять. | Don't call them. Draugr love fog.
^fog.притулися | Без ніжностей. Просто тепло. Дякую. | No mushy stuff. Just warmth. Thanks.
^fog.притулися | Притулюсь. Ти теплий, як не дивно. | I will. You're warm, oddly enough.
^fog.троль_сто | Твоя печінка вже раз кущ за троля мала. | Your gut once mistook a bush for a troll.
^fog.троль_сто | Підійдемо — побачимо. Обережно. | We'll get closer and see. Carefully.
^fog.дерево | Дзьобай. Дерево не відповість. | Peck away. A tree won't answer.
^fog.дерево | Дзьобни. Якщо заверещить — тікаємо. | Peck it. If it screams, we run.
^fog.матюком | Тепер воно знає, де ми. Дякую. | Now it knows where we are. Thanks.
^fog.матюком | Туман любить пастки з голосами. А ти — пастка з лайкою. | Fog loves traps with voices. And you're a trap with swearing.
^fog.гукну | Не треба, щоб прибігало. Тихо. | We don't want it running here. Quiet.
^fog.гукну | Туман любить пастки з голосами. Не будь приманкою. | Fog loves traps with voices. Don't be the bait.
^fog.пазуха | У пазуху не пущу. Сиди й не злітай. | You're not going in the shirt. Sit and don't fly off.
^fog.пазуха | Плече — це близько. Для тебе — навіть забагато. | A shoulder is close. For you, too close.
^fog.запах | Книжками? Ти хоч одну бачив? | Books? Have you ever seen one?
^fog.запах | А тебе знайде кожен вовк. Так само. | And every wolf will find you. Same way.
^fog.пішли | Не підемо. Хай світить сам. | We won't. Let it shine by itself.
^fog.пішли | Дідько з рогами — це ще добрий варіант. | A horned devil is the good option.
^fog.болотний | Знаю. Тому й не йду. А ти вже дивишся. | I know. That's why I'm not going. And you're already staring.
^fog.болотний | Тоді чому ти до нього шию тягнеш? | Then why are you craning your neck at it?
^fog.курва | Чув. На жаль, не заглушило. | Heard it. Sadly, not muffled.
^fog.курва | Дякую. Тепер нас чують і в Гельгеймі. | Thanks. Now they can hear us in Helheim.
^fog.не_подобається | Мені подобається. Тихо, як у пір'ї. | I like it. Quiet, like inside feathers.
^fog.не_подобається | Чую. Ледве. От би завжди так. | I hear you. Barely. If only it were always this way.
^fog.злість | Злітати буде важко. Сидимо. | Taking off will be hard. We sit.
^fog.злість | Злість хоч не капає за комір. | At least rage doesn't drip down the collar.
^fog.пси | Обтрусишся на мене — злечу з плеча. | Shake off on me and I'm off the shoulder.
^fog.пси | Пси обтрушуються. Ворони чекають. Чекай. | Dogs shake. Ravens wait. Wait.
`);
// ===================== NIGHT (M open, H reply, M? close in м_кінець) =====================
N['M:м_ніч'] = P(`
^night.бійка | Ніч. Саме час комусь напихати, {м_слівце}. | Night. Just the time to beat someone up, {м_слівце}.
^night.темно | Темно, як у тебе в голові, ботан. | Dark as the inside of your head, nerd.
^night.кістка | Чуєш? Десь кістка хрумтить. Аж слина, {м_слівце}. | Hear that? A bone crunching somewhere. Makes me drool, {м_слівце}.
~1 ^night.сховаюсь | Як хтось вилізе з кущів — я за тебе сховаюсь. А шо такого? | If something crawls out of the bushes, I'm hiding behind you. So what?
~3 ^night.спокій | Посидь тихо, {м_слівце}. Ніч гарна. Не псуй. | Sit quiet, {м_слівце}. It's a nice night. Don't ruin it.
^night.вовки | Чуєш вовків? Виють. Ше й краще за тебе, {м_слівце}. | Hear the wolves? Howling. Better than you, {м_слівце}.
^night.очі | Хугіне, з кущів хтось зирить. Двоє очей. Нє, четверо. Бля. | Hugin, something's staring from the bushes. Two eyes. No, four. Fuck.
^night.зорі | Зорі повилазили, {м_слівце}. Як блохи на собаці. Аж небо чухається. | The stars are out, {м_слівце}. Like fleas on a dog. The sky's scratching.
^night.сова | Сова верещить. От би їй голову відкрутити, падлюці. | An owl's screeching. I'd like to twist its head off, the bitch.
^night.місяць | Шо вирячився, місяцю? Га? Тебе питаю, пико лиса. | What are you gawking at, moon? Huh? I'm asking you, baldy.
^night.холод | Холодно вночі, {м_слівце}. Лапи задубіли нахуй. | Cold at night, {м_слівце}. My feet are fucking frozen.
^night.шурхіт | Шось шурхотить у траві. Дзьобнути, чи шо? | Something's rustling in the grass. Peck it, or what?
^night.сон | Хугін, ти спиш? Не спи, бо мені нудно, курва. | Hugin, you asleep? Don't sleep, I'm fucking bored.
^night.драуґри | Вночі драуґри вилазять. Хочу одному в гниле око дзьобом. Хрясь! | At night the draugr crawl out. I want to peck one in its rotten eye. Crunch!
`);
N['H:х_ніч'] = P(`
^night.бійка>night.думки | Тобі аби битися. А ніч для думок. | All you want is a fight. Night is for thinking.
^night.бійка>night.темрява | Бійка вночі — для тих, хто бачить у темряві. Тобто не для тебе. | Fighting at night is for those who see in the dark. So not you.
^night.бійка>night.темрява | Бийся сам. Я тобі хіба посвічу очима. | Fight alone. I'll light the way with my eyes, at most.
^night.темно>night.голова | Темно в мене в голові? Принаймні вона в мене є. | Dark in my head? At least I have one.
^night.темно>night.задумав | Темно — то не страшно. Страшно, коли ти щось задумав. | Dark isn't scary. Scary is when you've got an idea.
^night.кістка>night.вовк | Це вовк гризе. Сподіваюсь, не нашого господаря. | That's a wolf gnawing. Hopefully not our master.
^night.кістка>night.сухар | То не кістка. То {name} хрумтить сухарем. | That's not a bone. That's {name} crunching a rusk.
^night.кістка>night.хворий | Апетитно? Ти хворий. | Appetizing? You're sick.
^night.сховаюсь>night.тонкий | За мене не ховайся. Я тонкий. | Don't hide behind me. I'm thin.
^night.сховаюсь>night.розкажу | Ховайся. Я потім усім розкажу. | Hide. I'll tell everyone later.
^night.спокій>night.ліс | Добре. Помовчимо. Вночі ліс говорить сам. | Fine. Let's be quiet. At night the forest talks by itself.
^night.спокій>night.як_ти | Гарна. Тиха. Як ти, коли спиш. | Beautiful. Quiet. Like you when you sleep.
^night.вовки>night.скиглиш | Вовки співають, бо голодні. Як ти. Тільки ти не співаєш, а скиглиш. | Wolves sing because they're hungry. Like you. Only you don't sing, you whine.
^night.вовки>night.не_підходили | Хай співають. Аби не підходили. | Let them sing. As long as they don't come closer.
^night.очі>night.здивований | Четверо — то двоє звірів. Або один дуже здивований. | Four means two beasts. Or one very surprised one.
^night.очі>night.курча | Світлячки, Муніне. Не будь курчам. | Fireflies, Munin. Don't be a chicken.
~3 ^night.очі>night.курча | Світлячки. Сиди, я пильную. І не будь курчам. | Fireflies. Sit, I'm watching. And don't be a chicken.
^night.зорі>night.романтик | Як блохи? Ти романтик, Муніне. | Like fleas? You're a romantic, Munin.
^night.зорі>night.уважніше | Це зорі, а не блохи. Подивись уважніше. | Those are stars, not fleas. Look closer.
^night.сова>night.сторож | Сова — нічний сторож. Не чіпай. | The owl is the night watchman. Leave it alone.
^night.сова>night.мудра | Сова мудра. Тому ти її й не любиш. | The owl is wise. That's why you don't like it.
^night.місяць>night.заблукав | Місяць світить, щоб ти не заблукав. | The moon shines so you don't get lost.
^night.місяць>night.не_бери | Він на всіх так дивиться. Не бери до серця. | It looks at everyone like that. Don't take it to heart.
^night.холод>night.шия | Притулися до шиї. Там тепліше. | Snuggle up to the neck. It's warmer there.
^night.холод>night.вогонь | Біля вогню сядемо. Скоро. | We'll sit by a fire. Soon.
~1 ^night.холод>night.вогонь | Терпи. Біля вогню відтанеш. Колись. | Bear it. You'll thaw by a fire. Eventually.
^night.шурхіт>night.миша | Миша. Не дзьобай, вона тобі нічого не зробила. | A mouse. Don't peck it, it hasn't done anything to you.
^night.шурхіт>night.змія | Може, змія. Обережно. | Maybe a snake. Careful.
^night.сон>night.думаю | Не сплю. Думаю. | Not sleeping. Thinking.
^night.сон>night.пильную | Ворон уночі пильнує. Я пильную. | A raven keeps watch at night. I'm keeping watch.
^night.драуґри>night.смачного | Гниле око? Смачного. | A rotten eye? Bon appétit.
^night.драуґри>night.болото | Хай лізуть. Ми далеко від болота. | Let them crawl. We're far from the swamp.
`);
// ===================== SNOW (M open, H reply, M? close) =====================
N['M:м_сніг'] = P(`
^snow.мерзну | Мерзну так, шо пір'я дзвенить, {м_слівце}. | I'm so cold my feathers are ringing, {м_слівце}.
^snow.комір | Сніг за комір. Коміра в мене нема, а сніг є. Як так, бля? | Snow down my collar. I don't have a collar, but the snow's there. How, fuck?
^snow.біле | Все біле. Ненавиджу біле. Тьху. | Everything's white. I hate white. Ugh.
^snow.лапи | Лапи до плеча примерзли, {м_слівце}. Все, я тут навіки. | My feet are frozen to the shoulder, {м_слівце}. That's it, I'm here forever.
^snow.сніжинка | Сніжинка мені на дзьоб сіла. Нахабна, сука. Зневажає. | A snowflake landed on my beak. Cheeky bitch. It despises me.
^snow.сліди | В снігу всі сліди видно. І наші, {м_слівце}, теж. | In snow you see all the tracks. Ours too, {м_слівце}.
^snow.вогонь | Де вогонь, бля? Мені вогонь треба, а не твоя мудрість. | Where's the fire, fuck? I need fire, not your wisdom.
^snow.вовки | Тут вовки, {м_слівце}. Чую. Білі й злі, як теща. | There are wolves here, {м_слівце}. I can hear them. White and mean as a mother-in-law.
^snow.бурулька | В мене на хвості бурулька. Я тепер, бля, зброя. | There's an icicle on my tail. I'm a fucking weapon now.
^snow.глухо | Сніг усе глушить. Навіть як я матюкаюсь. | Snow muffles everything. Even my swearing.
^snow.гори | Гори, сніг, дубак. Хто тут, бля, живе по своїй волі? | Mountains, snow, freezing. Who the fuck lives here by choice?
^snow.кожух | {name} в хутрі, а я в пір'ї. Де справедливість, га? | {name}'s in fur and I'm in feathers. Where's the justice, huh?
`);
N['H:х_сніг'] = P(`
^snow.мерзну>snow.кусайся | Притулися ближче. Тільки не кусайся. | Huddle closer. Just don't bite.
~3 ^snow.мерзну>snow.кусайся | Іди сюди, дзвіночку. Тільки не кусайся. | Come here, little bell. Just don't bite.
^snow.мерзну>snow.пусте | Дзвенить — бо пусте всередині. | It rings because it's empty inside.
^snow.комір>snow.нема_проблем | Немає коміра — немає проблем. | No collar, no problem.
^snow.комір>snow.дзьоб | Заховай дзьоб у пір'я. Мудрі так роблять. | Tuck your beak into your feathers. The wise do that.
^snow.біле>snow.подивись | Красиво ж. Хоч раз подивись. | It's beautiful. Look for once.
^snow.біле>snow.чисте | Біле — бо чисте. Тобі незнайоме. | White because it's clean. Unfamiliar to you.
^snow.лапи>snow.інше_плече | Навіки? Тоді мені треба інше плече. | Forever? Then I need another shoulder.
^snow.лапи>snow.відмерзнуть | Не вовтузься. Відмерзнуть. | Stop fidgeting. They'll thaw.
^snow.сніжинка>snow.втомилася | Вона просто втомилася. Як усі, хто поруч із тобою. | It's just tired. Like everyone near you.
^snow.сніжинка>snow.не_вміє | Сніжинка не вміє зневажати. На відміну від тебе. | A snowflake can't despise. Unlike you.
^snow.сліди>snow.обережно | Тому йдемо обережно. І ти не тупцюй. | That's why we walk carefully. And you stop stomping.
^snow.сліди>snow.хто_йде | Зате видно, хто за нами йде. | But we can see who's following us.
^snow.вогонь>snow.мудрість | Мудрість теж гріє. Зсередини. | Wisdom warms too. From the inside.
^snow.вогонь>snow.скоро | Скоро. {name} вже шукає, де розпалити. | Soon. {name}'s already looking for a spot to light one.
~3 ^snow.вогонь>snow.скоро | Скоро. А поки грійся об мене. Мудрість додаю безкоштовно. | Soon. Meanwhile, warm up on me. Wisdom included for free.
^snow.вовки>snow.чесніші | Злі, як ти. Тільки чесніші. | Mean like you. Only more honest.
^snow.вовки>snow.тихо | Тихо. Не кличмо їх. | Quiet. Let's not call them.
^snow.бурулька>snow.для_тебе | Найнебезпечніша зброя Мідґарду. Для тебе самого. | The most dangerous weapon in Midgard. To yourself.
^snow.бурулька>snow.відламаєш | Не махай нею. Відламаєш. | Don't wave it around. You'll snap it off.
^snow.глухо>snow.корисне | Нарешті щось корисне від снігу. | Finally something useful from snow.
^snow.глухо>snow.найгарніша | Тиша в снігу — найгарніша тиша. Не псуй. | Silence in snow is the most beautiful silence. Don't spoil it.
^snow.гори>snow.міцніші | Ті, хто міцніший за тебе. | Those tougher than you.
^snow.гори>snow.кози | Кози. І трохи вовків. І ми зараз. | Goats. Some wolves. And us, right now.
^snow.кожух>snow.пір'я | Пір'я тепліше, ніж здається. | Feathers are warmer than they look.
^snow.кожух>snow.під_комір | Сховайся під комір. Там тепло. | Hide under the collar. It's warm there.
`);
N['M:м_сніг_кінець'] = P(`
^snow.кусайся | Не кусатимусь. Сильно. | I won't bite. Hard.
^snow.кусайся | Притулюсь. А кусаюсь я від холоду, ясно? | I'll huddle. And I bite from the cold, got it?
^snow.пусте | Пусте? Там вогонь. Ледь жевріє, курва. | Empty? There's a fire in there. Barely smouldering, fuck.
^snow.пусте | Пусте — то в тебе. Я хоч дзвеню. | Empty is you. At least I ring.
^snow.нема_проблем | Великий філософ. Мерзлий. Хе. | A great philosopher. A frozen one. Heh.
^snow.нема_проблем | Проблема є. Вона мокра і за шиєю. | There's a problem. It's wet and down my neck.
^snow.дзьоб | Мудрі мерзнуть так само. Тіко мовчки. Як дурні. | The wise freeze just the same. Only quietly. Like idiots.
^snow.дзьоб | Заховав. Тепер нічо не бачу. Дякую, мудрий. | Tucked it. Now I can't see a thing. Thanks, wise guy.
^snow.подивись | Глянув. Біле. Ненавиджу. | Looked. White. Hate it.
^snow.подивись | Подивився. Сліпну. Задоволений, {м_слівце}? | I looked. I'm going blind. Happy, {м_слівце}?
^snow.чисте | Чисте? Почекай, я зара на нього насру. | Clean? Hang on, I'll shit on it.
^snow.чисте | Чисте, поки я не сів. Зара буде знайоме. | Clean until I sit on it. Soon it'll be familiar.
^snow.інше_плече | Інше плече? Я тобі дам інше плече, хитрий який. | Another shoulder? I'll give you another shoulder, you sly one.
^snow.інше_плече | Нема іншого. Терпи мене навіки. | There isn't another. Put up with me forever.
^snow.відмерзнуть | Відмерзнуть, коли я вже здохну. | They'll thaw when I'm dead.
^snow.відмерзнуть | Я не вовтузюсь. Я тремчу. Різниця, бля. | I'm not fidgeting. I'm shivering. There's a difference, fuck.
^snow.втомилася | Втомилась? Ну то я її зжер. Хай відпочиває. | Tired? So I ate it. Let it rest.
^snow.втомилася | Від мене не втомлюються. Від мене тікають. | Nobody gets tired of me. They run from me.
^snow.не_вміє | А я її зжер. Хай знає, з ким має діло. | I ate it. Let it know who it's dealing with.
^snow.не_вміє | Не вміє? Зате я вмію за двох. Тьху на неї. | Can't? Well, I can for two. Spit on it.
^snow.обережно | Обережно, бля. Я по снігу й не ходжу, я сиджу. | Carefully, fuck. I don't walk on snow, I sit.
^snow.обережно | Не тупцюю. Це {name} топче, як троль. | I'm not stomping. It's {name}, trampling like a troll.
^snow.хто_йде | Хто за нами припреться — тому дзьобом в тім'я. | Whoever comes after us gets a beak in the skull.
^snow.хто_йде | Хай іде. Я його по сліду й знайду. | Let them come. I'll find them by the tracks.
^snow.мудрість | Мене вона тіко дратує. Зсередини. | It only annoys me. From the inside.
^snow.мудрість | Тебе гріє. А я синій, {м_слівце}. | It warms you. I'm turning blue, {м_слівце}.
^snow.скоро | Скоро, скоро… А дубака я вже зара ловлю. | Soon, soon… And I'm freezing right now.
^snow.скоро | Хай шукає швидше. Я вже синій. | Tell {him|her} to look faster. I'm already blue.
^snow.чесніші | Чесніші? Вони тебе зжеруть. Чесно. | More honest? They'll eat you. Honestly.
^snow.чесніші | Я чесний. Я тобі прямо кажу, шо ти ботан. | I'm honest. I'm telling you straight you're a nerd.
^snow.тихо | Хай лізуть. Я теж білий стану. Від злості, бля. | Let them come. I'll go white too. With rage, fuck.
^snow.тихо | Мовчу. Але дзьоб наготові. | I'm quiet. But my beak is ready.
^snow.для_тебе | Для мене? Відламаю й встромлю тобі. По-братськи. | To me? I'll snap it off and stick it in you. Brotherly.
^snow.для_тебе | Для себе? Дивись, як я нею махну. | To myself? Watch me swing it.
^snow.відламаєш | Відламаю — нова виросте. Я багатий. | If I snap it, a new one grows. I'm rich.
^snow.відламаєш | Та й хай. Зброя одноразова, як ти. | Fine. A disposable weapon, like you.
^snow.корисне | Ага? Ну слухай: КУРВА! О. Не заглушило. | Yeah? Then listen: FUCK! See? Didn't muffle it.
^snow.корисне | Корисне — то коли ти мовчиш. А сніг так, підмога. | Useful is when you shut up. The snow just helps.
^snow.найгарніша | Найгарніша тиша — то коли ти мовчиш. | The most beautiful silence is when you shut up.
^snow.найгарніша | Гарна. Але довго я так не витримаю, {м_слівце}. | Nice. But I can't last long like this, {м_слівце}.
^snow.міцніші | Міцніші? Тіко кози. І ті дурні. | Tougher? Only goats. And they're dumb.
^snow.міцніші | Та хай живуть. А я хочу додому. Додому, чуєш? | Let them. I want to go home. Home, hear me?
^snow.кози | Ми — не по волі. Нас {name} притяг{|ла}. | We're not here by choice. {name} dragged us.
^snow.кози | Кози, бля. Я ще й з козами в одному списку. | Goats, fuck. Now I'm on a list with goats.
^snow.пір'я | Пиздиш. Але хай. Мені вже однаково. | Bullshit. But fine. I don't care anymore.
^snow.пір'я | Тепліше? Чого ж я тоді дзвеню? | Warmer? Then why am I ringing?
^snow.під_комір | Пиздиш. Але під комір залізу. | Bullshit. But I'll crawl under the collar.
^snow.під_комір | Залізу. Тіко не жени мене звідти. | I'll crawl in. Just don't kick me out.
`);
// ===================== STORM (H open, M reply, H? close) =====================
N['H:х_гроза'] = P(`
^storm.грім | Грім. Тор знову п'яний. | Thunder. Thor is drunk again.
^storm.блискавка | Якщо вдарить блискавка — то в тебе. Ти чорніший. | If lightning strikes, it'll be you. You're blacker.
^storm.небо | Небо сердиться. Не на нас, сподіваюсь. | The sky is angry. Not at us, I hope.
^storm.вітер | Вітер такий, що пір'я виє. Тримайся, Муніне. | The wind's so strong my feathers howl. Hold on, Munin.
^storm.молот | Чуєш? То Мйольнір. Тор когось лупить. | Hear that? That's Mjölnir. Thor is pounding someone.
^storm.дерево | Не сідай на високе дерево, Муніне. Сьогодні не той день. | Don't sit on a tall tree, Munin. Today's not the day.
^storm.злива | Ллє так, ніби в Асгарді перекинули діжку. | It's pouring as if someone tipped over a barrel in Asgard.
^storm.страх | Ти тремтиш, Муніне? Чи то вітер? | Are you trembling, Munin? Or is that the wind?
^storm.рахувати | Рахуй від спалаху до грому. Так знаєш, як далеко. | Count from the flash to the thunder. That's how you know how far.
^storm.іскра | Пір'я аж тріщить. Небо повне іскор. | My feathers are crackling. The sky is full of sparks.
^storm.тролі | У грозу навіть тролі ховаються. Розумні тролі. | In a storm even trolls hide. Smart trolls.
^storm.після | Після грози повітря чисте. Аж дихати хочеться. | After a storm the air is clean. Makes you want to breathe.
`);
N['M:м_гроза'] = P(`
^storm.грім>storm.пацан | П'яний Тор — то найчесніший Тор. Наш пацан. | Drunk Thor is the most honest Thor. Our kind of guy.
^storm.грім>storm.нам_можна | Тор бухий — то й нам можна, {м_слівце}. | If Thor's hammered, so can we be, {м_слівце}.
^storm.блискавка>storm.вищий | Я чорніший, а ти вищий. Тебе першого засмажить, ботан. | I'm blacker, but you're taller. You'll fry first, nerd.
^storm.блискавка>storm.злішим | Хай б'є. Я від того тіко злішим стану. | Let it strike. It'll just make me meaner.
^storm.небо>storm.сердитий | Хай сердиться. Я теж сердитий, {м_слівце}. І шо? | Let it be angry. I'm angry too, {м_слівце}. So what?
^storm.небо>storm.набрид | Небо сердиться, бо ти йому набрид. Як і всім. | The sky's angry because it's sick of you. Like everyone.
^storm.вітер>storm.за_тебе | Тримаюсь. За тебе. Шоб ти першим полетів. | I'm holding on. To you. So you blow away first.
^storm.вітер>storm.співаю | Виє? То я співаю, {м_слівце}. Шо, не подобається? | Howling? That's me singing, {м_слівце}. What, don't like it?
^storm.молот>storm.аби_не_нас | Та хай лупить кого хоче. Аби не нас, {м_слівце}. | Let him pound whoever he wants. As long as it's not us, {м_слівце}.
^storm.молот>storm.махач | Ото б мене хоч раз на махач узяв. Я б йому показав, як треба. | If only he'd take me to a brawl once. I'd show him how it's done.
^storm.дерево>storm.цар | Та я й не збирався, бля. Я на плечі сиджу, як цар. | Wasn't going to, fuck. I sit on the shoulder like a king.
^storm.дерево>storm.де_хочу | Де хочу, там і сиджу. Хай тіко спробує вдарити, та блискавка. | I sit where I want. Let that lightning just try.
^storm.злива>storm.рюмсає | Яка нахуй діжка? То Одін рюмсає, шо ти в нього є. | What fucking barrel? That's Odin weeping that he's stuck with you.
^storm.злива>storm.мокріше | Та хай ллє. Мокріше мене вже нема куди, {м_слівце}. | Let it pour. I can't get any wetter, {м_слівце}.
^storm.страх>storm.злість | Я не трушусь, бля. Я від злості трушусь. Це інше! | I'm not shaking, fuck. I'm shaking with rage. That's different!
^storm.страх>storm.вітер_тіко | Вітер, {м_слівце}. Тіко вітер. Ну і грім трохи. Відчепись. | The wind, {м_слівце}. Just the wind. And a bit of thunder. Piss off.
^storm.рахувати>storm.близько | Раз, два… БАХ! Близько, сука! | One, two… BANG! Close, bitch!
^storm.рахувати>storm.до_трьох | Рахувати? Я до трьох рахую, і то на кігтях. Зате матюкаюсь — як бог. | Count? I count to three, on my claws. But I swear like a god.
^storm.іскра>storm.тріщу | То я від злості тріщу, а не від неба, ботан. | That's me crackling with rage, not the sky, nerd.
^storm.іскра>storm.блищу | О, я тепер блищу. Красень, {м_слівце}, глянь! | Oh, I'm shiny now. Handsome, {м_слівце}, look!
^storm.тролі>storm.грибів | Тролі розумні? Ти шо, бля, грибів наївся? | Trolls smart? Did you eat some fucking mushrooms?
^storm.тролі>storm.тупіші | А ми не ховаємось. Виходить, ми тупіші за тролів. Ну й похуй. | And we're not hiding. So we're dumber than trolls. Fuck it.
^storm.після>storm.смердить | От і дихай. А мені й так нормально смердить. | Go on, breathe. I'm fine with the stink.
^storm.після>storm.гриби | Після грози — гриби. А після грибів {name} співає. Жах, бля. | After a storm, mushrooms. And after mushrooms, {name} sings. Fucking horror.
`);
N['H:х_гроза_кінець'] = P(`
^storm.пацан | Ну от. Тепер ти ще й ображаєш богів. | There. Now you're insulting the gods too.
^storm.пацан | Скажи це йому. Під блискавкою. | Say that to him. Under the lightning.
^storm.нам_можна | Тобі можна, коли станеш богом. Тобто ніколи. | You can when you become a god. So, never.
^storm.нам_можна | Тор п'є — і громить. Ти п'єш — і падаєш з плеча. | Thor drinks and thunders. You drink and fall off the shoulder.
^storm.вищий | Тоді я пригнусь. А ти стирчи. | Then I'll duck. And you stick out.
^storm.вищий | Вищий, бо розумніший. Блискавка таких поважає. | Taller because smarter. Lightning respects that.
^storm.злішим | Злішим уже нікуди. | There's no meaner left.
^storm.злішим | Тоді хай б'є в мене. Тобі вже досить. | Then let it hit me. You've had enough.
^storm.сердитий | Тоді вас двоє. Я сховаюсь. | Then there are two of you. I'll take cover.
^storm.сердитий | А те, що в неба є блискавки. А в тебе — тільки дзьоб. | So the sky has lightning. And you only have a beak.
^storm.набрид | Чуєш? Знову гримить. На тебе. | Hear that? Thunder again. At you.
^storm.набрид | Небо мене слухає. Тебе — терпить. | The sky listens to me. It puts up with you.
^storm.за_тебе | Полечу — то разом із тобою. Ти ж тримаєшся. | If I go, you come with me. You're holding on.
^storm.за_тебе | Хитро. Тільки я легший. Полетимо обидва. | Clever. Only I'm lighter. We'll both go.
^storm.співаю | Співаєш ти гірше за вітер. | You sing worse than the wind.
^storm.співаю | Не подобається. Вітер хоч колись ущухне. | I don't. At least the wind will die down eventually.
^storm.аби_не_нас | Тор б'є, кого хоче. Краще не потрапляй йому на очі. | Thor strikes whoever he likes. Better not catch his eye.
^storm.аби_не_нас | Не нас. Поки ти мовчиш. | Not us. As long as you keep quiet.
^storm.махач | Він тебе візьме. Замість молота. | He'll take you. Instead of the hammer.
^storm.махач | Тор тебе й не помітить. Як блоху. | Thor wouldn't even notice you. Like a flea.
^storm.цар | Цар мокрий і тремтить. Царство під стать. | A king, wet and shivering. A kingdom to match.
^storm.цар | Сиди, царю. Тільки голову не задирай. | Sit, your majesty. Just keep your head down.
^storm.де_хочу | Сміливий ти, доки не загримить. | You're brave until it thunders.
^storm.де_хочу | Спробує. Вона не питає. | It will. It doesn't ask.
^storm.рюмсає | Ні. Він плаче, що в нього є ти. Теж. | No. He weeps that he has you. As well.
^storm.рюмсає | Тоді він рюмсає за двох. І про тебе — гірше. | Then he weeps for two. And harder about you.
^storm.мокріше | Мокрі обидва. Хоч у чомусь рівні. | Both soaked. Equal in something at least.
^storm.мокріше | Є куди. Я покажу тобі калюжу. | There is. I'll show you a puddle.
^storm.злість | Звісно. Я так і подумав. | Of course. That's what I thought.
^storm.злість | Від злості. І трохи від грому. Ну-ну. | With rage. And a bit with thunder. Sure.
^storm.вітер_тіко | Відчепився. Але крило поруч, якщо що. | I'll let it go. But my wing is right here, just in case.
^storm.вітер_тіко | Трохи грому — це чесно. Ціную. | A bit of thunder, that's honest. I appreciate it.
^storm.близько | Два. Отже, пригнись. Негайно. | Two. So duck. Now.
^storm.близько | Близько. Тому й кажу: сиди нижче. | Close. That's why I said: sit lower.
^storm.до_трьох | Тоді просто пригнись. Це вміють усі. | Then just duck. Everyone can do that.
^storm.до_трьох | До трьох вистачить. Далі грім уже не страшний. | Three is enough. After that thunder's not scary.
^storm.тріщу | Тріщи тихіше. Ще блискавку приманиш. | Crackle quieter. You'll lure the lightning.
^storm.тріщу | Тоді не злися. Хоч сьогодні. Для безпеки. | Then don't rage. Just today. For safety.
^storm.блищу | Красень. Поки не засмажився. | Handsome. Until you get fried.
^storm.блищу | Блищиш. Блискавка теж помітила. | You shine. The lightning noticed too.
^storm.грибів | Розумніші за тебе. Вони ховаються. | Smarter than you. They're hiding.
^storm.грибів | Розумні, бо ховаються. Ти — ні. | Smart because they hide. You don't.
^storm.тупіші | Тролі розумніші за нас. Це вже щось про нас каже. | Trolls smarter than us. That says something about us.
^storm.тупіші | Не ми. Ти. Я давно під каптуром у {name}. | Not us. You. I've been under {name}'s hood for a while.
^storm.смердить | Дихай, Муніне. Хоч раз — свіжим. | Breathe, Munin. Fresh air, for once.
^storm.смердить | Тобі нормально. Тому від тебе й смердить. | You're fine with it. That's why you stink.
^storm.гриби | Не нагадуй. Я досі чую ту пісню. | Don't remind me. I can still hear that song.
^storm.гриби | Тоді гриби — тобі. Заспіваєте разом. | Then the mushrooms are yours. You can sing together.
`);
// ===================== DAWN (H open, M reply, H? close) =====================
N['H:х_світанок'] = P(`
^dawn.світає | Світає. Ще один день — і ми живі. | Dawn. Another day, and we're alive.
^dawn.хропів | Ранок. Ти хропів, як троль, Муніне. | Morning. You snored like a troll, Munin.
^dawn.роса | Роса на пір'ї. Добрий знак. | Dew on my feathers. A good sign.
^dawn.сонце | Сонце встає, Муніне. Хоч хтось сьогодні рано встав. | The sun is rising, Munin. At least someone got up early today.
^dawn.птахи | Птахи співають. Світ прокидається. | Birds are singing. The world is waking up.
^dawn.мряка | Ранкова мряка над землею. Як молоко. | Morning mist over the land. Like milk.
^dawn.сніданок | Що на сніданок, Муніне? Знову чиєсь око? | What's for breakfast, Munin? Someone's eye again?
^dawn.асгард | Мені наснився Асгард. А тобі що снилося? | I dreamed of Asgard. What did you dream of?
^dawn.холод | Ранок холодний. Пір'я ще не прогрілося. | The morning is cold. My feathers haven't warmed up yet.
^dawn.план | Новий день. Який у нас план? | A new day. What's our plan?
^dawn.лінь | Вставай, Муніне. Ти спиш з розплющеним оком. | Get up, Munin. You're sleeping with one eye open.
^dawn.перший | Хто перший побачив сонце — тому й день вдалий. | Whoever sees the sun first has a lucky day.
`);
N['M:м_світанок'] = P(`
^dawn.світає>dawn.слухати | Ше один день. Ше один день тебе слухати. Йой. | Another day. Another day of listening to you. Ugh.
^dawn.світає>dawn.ледь | Живі. Ну, ти — ледь-ледь. | Alive. Well, you barely.
^dawn.хропів>dawn.то_ти | То ти хропів. Я спав, як немовля. Зле немовля. | That was you snoring. I slept like a baby. An angry baby.
^dawn.хропів>dawn.сторожу | Я не хроплю. Я голосом сторожу. | I don't snore. I guard with my voice.
^dawn.роса>dawn.мокрі_лапи | Добрий знак? Мокрі лапи — то добрий знак, по-твоєму? | A good sign? Wet feet are a good sign, according to you?
^dawn.роса>dawn.сніданок | Добрий знак — то коли сніданок. Все. | A good sign is breakfast. That's it.
^dawn.сонце>dawn.пощастило | Сонцю не треба тебе слухати. Пощастило гаду. | The sun doesn't have to listen to you. Lucky bastard.
^dawn.сонце>dawn.подрімаю | Хай встає. А я ше подрімаю, {м_слівце}. | Let it rise. I'll doze a bit more, {м_слівце}.
^dawn.птахи>dawn.зжер | Яке співають — верещать. Зжер би всіх, {м_слівце}. | Singing? Screeching. I'd eat them all, {м_слівце}.
^dawn.птахи>dawn.горлають | Хай горлають. Поки я не прокинувся як слід. | Let them yell. Until I'm properly awake.
^dawn.мряка>dawn.м'ясо | Яке молоко? Мені б м'ясця, {м_слівце}. | What milk? I want some meat, {м_слівце}.
^dawn.мряка>dawn.потіє | Мряка — то земля потіє. Від тебе, ботане. Від твоїх розумних балачок. | The mist is the earth sweating. Because of you, nerd. Your clever talk.
^dawn.сніданок>dawn.делікатес | Око — то делікатес, бля. Шо ти шариш у жратві. | An eye is a delicacy, fuck. What do you know about food.
^dawn.сніданок>dawn.нічо | Шо дадуть. А дадуть, сто пудів, нічо. | Whatever we get. And we'll get nothing, guaranteed.
^dawn.асгард>dawn.заткнувся | Мені снилось, шо ти заткнувся. Кайфовий сон, бля. | I dreamed you shut up. Fucking great dream.
^dawn.асгард>dawn.кров | Кров снилась. Повно крові. Смачно, {м_слівце}. | I dreamed of blood. Loads of blood. Tasty, {м_слівце}.
^dawn.холод>dawn.шия | То сунься ближче до шиї. Там тепліше. Тіко не впритул, ясно? | Then scoot closer to the neck. It's warmer. Just not too close, got it?
^dawn.холод>dawn.злий | Дубак. До обіду буду злий, {м_слівце}. А після обіду — теж. | Freezing. I'll be angry till lunch, {м_слівце}. And after lunch too.
^dawn.план>dawn.вгасити | План? Когось вгасити. Як завжди, бля. | Plan? Smash someone. As always, fuck.
^dawn.план>dawn.без_плану | План — шоб без плану. Найкращий план, вкурив? | The plan is no plan. The best plan, get it?
^dawn.лінь>dawn.талант | Я і вві сні пасу. Талант, {м_слівце}. | I keep watch even asleep. Talent, {м_слівце}.
^dawn.лінь>dawn.хвилинку | Ше хвилинку. Ну дві. Ну відчепись, бля. | One more minute. Well, two. Fuck off.
^dawn.перший>dawn.гівно | Ну я побачив. А день все одно гівно. | Well, I saw it. And the day is still shit.
^dawn.перший>dawn.фартовий | Ти побачив? Ну то хай тобі й фартить, {м_слівце}. А я й так фартовий. | You saw it? Then you get lucky, {м_слівце}. I'm lucky anyway.
`);
N['H:х_світанок_кінець'] = P(`
^dawn.слухати | Слухатимеш. Може, щось і запам'ятаєш. | You'll listen. Maybe something will stick.
^dawn.слухати | А мені — тебе. Чесний обмін. | And I listen to you. Fair trade.
^dawn.ледь | Ледь-ледь — це все одно живий. Порахуй. | Barely is still alive. Do the maths.
^dawn.ледь | Зате я вже встав. А ти ще ні. | But I'm already up. You're not.
^dawn.то_ти | Чув увесь табір. І сусідній. | The whole camp heard. And the next one.
^dawn.то_ти | Зле немовля, що хропе. Рідкісний звір. | An angry baby that snores. A rare beast.
^dawn.сторожу | Наторожив. Усі вовки розбіглися. | Fine guarding. All the wolves ran off.
^dawn.сторожу | Сторожиш так, що не спить ніхто. | You guard so well nobody sleeps.
^dawn.мокрі_лапи | Мокрі ноги — дрібниця. Твоя пика зранку — от поганий знак. | Wet feet are nothing. Your face in the morning, now that's a bad sign.
^dawn.мокрі_лапи | По-моєму, так. Мокрий, зате живий. | According to me, yes. Wet, but alive.
^dawn.сніданок | {name} нагодує. Просто прокинься чемно. | {name} will feed us. Just wake up politely.
^dawn.сніданок | Для тебе всі знаки — про їжу. | For you, every sign is about food.
^dawn.пощастило | Воно далеко. А ти поруч. Тобі не пощастило. | It's far away. You're close. You're the unlucky one.
^dawn.пощастило | Сонце все чує. Просто мовчить. Вчися. | The sun hears everything. It just keeps quiet. Learn.
^dawn.подрімаю | Не проспи день. Він короткий. | Don't sleep through the day. It's short.
^dawn.подрімаю | Ще хвилинка — і я тебе дзьобну. | One more minute and I'll peck you.
^dawn.зжер | Ти вже прокинувся. На жаль. | You're awake already. Sadly.
^dawn.зжер | З'їж одного. Інші співатимуть про тебе. | Eat one. The others will sing about you.
^dawn.горлають | Прокинешся — вони замовкнуть. Від жаху. | Once you wake, they'll go quiet. In horror.
^dawn.горлають | Як слід ти не прокидаєшся ніколи. | You never properly wake up.
^dawn.м'ясо | Поетичності в тобі — як у каменя. | You're as poetic as a rock.
^dawn.м'ясо | Ти навіть у хмарі бачиш котлету. | You even see a meatball in a cloud.
^dawn.потіє | Земля потіє — гарно сказав. Ти теж поет. Поганий. | The earth sweating, nicely put. You're a poet too. A bad one.
^dawn.потіє | Від мене земля хоч не тікає. Від тебе — так. | At least the earth doesn't run from me. It does from you.
^dawn.делікатес | Шарю досить, щоб не їсти очей. | I know enough not to eat eyes.
^dawn.делікатес | Делікатес. Тоді ділись. Ні, жартую. | A delicacy. Then share. No, joking.
^dawn.нічо | Дадуть. Якщо ти зранку нікого не обматюкаєш. | We'll get some. If you don't curse anyone out this morning.
^dawn.нічо | Нічого — теж сніданок. Для філософів. | Nothing is a breakfast too. For philosophers.
^dawn.заткнувся | Сни не збуваються. Тобі не пощастило. | Dreams don't come true. Bad luck.
^dawn.заткнувся | Не звикай. Я вже прокинувся. | Don't get used to it. I'm awake.
^dawn.кров | Сни в тебе, як і ти. Нічого світлого. | Your dreams are like you. Nothing bright.
^dawn.кров | Мені — Асгард, тобі — бойня. Справедливо. | Asgard for me, a slaughterhouse for you. Fair.
^dawn.шия | Ясно. Впритул — це тільки ти вмієш. | Got it. Only you get too close.
^dawn.шия | Дякую. Не впритул. Майже. | Thanks. Not too close. Almost.
^dawn.злий | Сонце пригріє. І тебе, і твою вдачу. | The sun will warm up. You and your temper too.
^dawn.злий | Отже, звичайний день. | So, an ordinary day.
^dawn.вгасити | Ну, хоч послідовно. | Well, at least you're consistent.
^dawn.вгасити | Кого — вирішить {name}. Ти тільки кричи. | Who, {name} decides. You just yell.
^dawn.без_плану | Вкурив. Тому й живемо так. | Got it. That's why we live like this.
^dawn.без_плану | Без плану — це твій план уже років десять. | No plan has been your plan for ten years.
^dawn.талант | Пасеш. Хроплячи. | Watching. While snoring.
^dawn.талант | Талант. Тільки вчора ти пропас вовка. | Talent. Only yesterday you missed a wolf.
^dawn.хвилинку | Ще хвилинка — і я тебе дзьобну. Рахую. | One more minute and I peck you. I'm counting.
^dawn.хвилинку | Дві хвилинки. Потім співатиму. | Two minutes. Then I sing.
^dawn.гівно | Ти побачив — і вже зіпсував. | You saw it and already ruined it.
^dawn.гівно | Поганий, бо ти так вирішив ще до сніданку. | Bad, because you decided so before breakfast.
^dawn.фартовий | Щастя спільне. Навіть тобі шматок. | Luck is shared. Even you get a piece.
^dawn.фартовий | Фартовий — бо сидиш поруч зі мною. | Lucky because you sit next to me.
`);
// ===================== DUSK (M open, H reply, M? close) =====================
N['M:м_вечір'] = P(`
^dusk.ніч | Сонце сідає. Ну нарешті моя пора, {м_слівце}. | The sun is setting. Finally my time, {м_слівце}.
^dusk.голод | Вечір, а я не жер. Хтось про мене пам'ятає взагалі? | Evening, and I haven't eaten. Does anyone even remember me?
^dusk.червоне | Небо червоне, як кров. Гарно, {м_слівце}. Аж їсти захотілось. | The sky's red as blood. Beautiful, {м_слівце}. Makes me hungry.
^dusk.кажани | Кажани вилізли, {м_слівце}. Мої братани нічні. | The bats are out, {м_слівце}. My night bros.
^dusk.сови | Сови повилазили, {м_слівце}. Ненавиджу тих сов. | The owls are out, {м_слівце}. I hate those owls.
^dusk.багаття | Пора вогонь палити. Бо я задубів, {м_слівце}. | Time to light a fire. I'm frozen stiff, {м_слівце}.
^dusk.втома | День минув, а я від тебе заїбався більше, ніж від дороги. | The day's done, and I'm more fucked out by you than by the road.
^dusk.тіні | Тіні довгі повилазили. Скоро і те, шо в них сидить, вилізе. | The long shadows are out. Soon what sits in them comes out too.
^dusk.зорі | Перша зоря, {м_слівце}. Загадуй. Я вже загадав — шоб ти заткнувся. | First star, {м_слівце}. Make a wish. I already did: that you shut up.
^dusk.підсумок | Ну шо, скільки сьогодні завалили? Мало, сто пудів. | So, how many did we kill today? Not enough, for sure.
^dusk.виття | Чуєш? Вовки виють. На вечерю кличуть, гиги. | Hear that? Wolves howling. Calling us to supper, heh.
^dusk.нічліг | Де ночуємо, {м_слівце}? Тіко не в болоті, бо я злечу. | Where are we sleeping, {м_слівце}? Not in a swamp, or I'm off.
`);
N['H:х_вечір'] = P(`
^dusk.ніч>dusk.кусатись | Твоя пора — то ще не привід кусатись. | Your time is no reason to bite.
^dusk.ніч>dusk.сон | Ніч для сну, Муніне. Для тебе — особливо. | Night is for sleep, Munin. For you especially.
^dusk.голод>dusk.чемно | {name} нагодує. Якщо попросиш чемно. | {name} will feed you. If you ask politely.
^dusk.голод>dusk.вечеря | Вечір — час для вечері. Не для лайки. | Evening is for supper. Not for swearing.
~3 ^dusk.голод>dusk.чемно | Пам'ятаю. Тому й кажу: попроси в {name} чемно. Я підтримаю. | I remember. That's why I say: ask {name} politely. I'll back you.
^dusk.червоне>dusk.соромиться | Червоне — бо сонце соромиться твоїх слів. | Red because the sun is ashamed of your words.
^dusk.червоне>dusk.кров | Як кров? Ти про все так думаєш. | Like blood? You think that about everything.
^dusk.кажани>dusk.мовчки | Братани? Вони хоч мовчки літають. | Bros? At least they fly in silence.
^dusk.кажани>dusk.мозок | Кажани їдять комарів. А ти що їси? Мені мозок. | Bats eat mosquitoes. And what do you eat? My brain.
^dusk.сови>dusk.мудрі | Сови мудрі. Тому ти їх і не любиш. | Owls are wise. That's why you don't like them.
^dusk.сови>dusk.поділ | Сови вночі, ми вдень. Чесний поділ. | Owls by night, us by day. A fair split.
^dusk.багаття>dusk.гарчи | Розпалить {name}. Ти тільки сиди й гарчи. | {name} will light it. You just sit and growl.
^dusk.багаття>dusk.дзьоб | Вогонь буде. Ти тільки не сунь у нього дзьоба. | There'll be a fire. Just don't stick your beak in it.
^dusk.втома>dusk.взаємно | Взаємно, Муніне. Взаємно. | Likewise, Munin. Likewise.
^dusk.втома>dusk.завтра | Відпочинь. Завтра знову втомлюватимешся. | Rest. Tomorrow you'll get tired again.
^dusk.тіні>dusk.світло | Тому тримаймося світла. | That's why we stay in the light.
^dusk.тіні>dusk.бійки | Хай лізе. Ти ж хотів бійки. | Let it come. You wanted a fight.
^dusk.зорі>dusk.чемність | А я — щоб ти навчився чемності. Обидва не збудуться. | And I wished you'd learn manners. Neither will come true.
^dusk.зорі>dusk.світять | Зорі не виконують бажань. Вони просто світять. | Stars don't grant wishes. They just shine.
^dusk.підсумок>dusk.живі | Зате всі живі. Це теж підсумок. | But everyone's alive. That's a tally too.
^dusk.підсумок>dusk.рахуєш | Ти рахуєш трупи. Я рахую дні. Обидва — без кінця. | You count corpses. I count days. Both endless.
^dusk.виття>dusk.вечерею | Кличуть, щоб ми стали вечерею. | They're calling us to be the supper.
^dusk.виття>dusk.не_відповідай | Не відповідай їм. Будь ласка. | Don't answer them. Please.
^dusk.нічліг>dusk.дах | Де {name} вирішить. Сподіваюсь, під дахом. | Wherever {name} decides. Under a roof, I hope.
^dusk.нічліг>dusk.дим | Під дахом. Я вже бачу дим. | Under a roof. I can already see smoke.
`);
N['M:м_вечір_кінець'] = P(`
^dusk.кусатись | Привід — ти. А пора — тіко нагода. | You're the reason. The hour's just the chance.
^dusk.кусатись | Не кусатимусь. Погризу трошки, і всьо. | I won't bite. Just nibble a bit, that's all.
^dusk.сон | Посплю, як здохну. | I'll sleep when I'm dead.
^dusk.сон | Спи сам. А я вночі гуляю. | You sleep. I go out at night.
^dusk.чемно | Чемно? Я? Ха! Ха-ха! | Politely? Me? Ha! Ha-ha!
^dusk.чемно | Попрошу. Дзьобом. По руці. Це чемно. | I'll ask. With my beak. On the hand. That's polite.
^dusk.вечеря | То де вечеря, бля? Я ше й лаятись почну. | Then where's supper, fuck? I'm about to start swearing too.
^dusk.вечеря | Лайка — то моя вечеря, поки жерти нема. | Swearing is my supper till there's grub.
^dusk.соромиться | Моїх слів? Та я ше й не починав, ботан. | My words? I haven't even started, nerd.
^dusk.соромиться | Хай соромиться. Я не соромлюсь. | Let it be ashamed. I'm not.
^dusk.кров | Про все. А шо, є шось краще за кров? | About everything. What, is there anything better than blood?
^dusk.кров | Не про все. Про тебе я думаю гірше. | Not everything. About you I think worse.
^dusk.мовчки | Мовчки? Вони пищать, ботан. Ти просто глухий. | Silence? They squeak, nerd. You're just deaf.
^dusk.мовчки | Бо їм нема шо тобі сказати. А мені є. | Because they've got nothing to say to you. I do.
^dusk.мозок | Мізки? В тебе? Не смішіть мене, {м_слівце}. | Brains? You? Don't make me laugh, {м_слівце}.
^dusk.мозок | Їм. Смачно. Хоч його й мало. | I do. Tasty. Even if there's not much of it.
^dusk.мудрі | Я одну колись дзьобнув. Мудрість їй ніхріна не допомогла. | I pecked one once. Wisdom didn't help it one bit.
^dusk.мудрі | Мудрі, ага. Як ти. Тому й не люблю. | Wise, yeah. Like you. That's why.
^dusk.поділ | Чесний, ага. Тіко я й уночі не сплю. | Fair, sure. Only I don't sleep at night either.
^dusk.поділ | Хай собі. Аби в мій день не лізли. | Fine. As long as they stay out of my day.
^dusk.гарчи | Сидіти, гарчати, сраку гріти. Оце я розумію вечір. | Sit, growl, warm my ass. That's what I call an evening.
^dusk.гарчи | Гарчатиму. Шоб швидше палив. | I'll growl. So the fire comes faster.
^dusk.дзьоб | Сам не сунь. Мій дзьоб — моя справа. | Don't stick your own. My beak, my business.
^dusk.дзьоб | Тіко раз сунув! Раз! І всі пам'ятають. | I stuck it in once! Once! And everybody remembers.
^dusk.взаємно | Ну от. Хоч у чомусь згода. | There. We agree on something.
^dusk.взаємно | То завтра помовчимо. Обидва відпочинем. | Then tomorrow we keep quiet. Both of us rest.
^dusk.завтра | Завтра знов втома. Ура, бля. Ура. | Tomorrow, tired again. Hooray, fuck. Hooray.
^dusk.завтра | Від тебе — по-любому. | Of you, for sure.
^dusk.світло | Світло — для ссикл. Але… сяду ближче. Просто так. | Light is for cowards. But… I'll sit closer. Just because.
^dusk.світло | Тримайся сам. Я в тіні — як удома. | You stick to it. In the shadows I'm at home.
^dusk.бійки | Хотів. Але не з тінню, {м_слівце}. | I did. But not with a shadow, {м_слівце}.
^dusk.бійки | Та й хай. Я з того, шо вилізе, пір'я наскубу. | Fine. I'll pluck whatever crawls out.
^dusk.чемність | Чемності? Я чемний. Я ж тебе ше не дзьобнув. | Manners? I am polite. I haven't pecked you yet.
^dusk.чемність | Твоє не збудеться. А моє — побачим. | Yours won't. Mine, we'll see.
^dusk.світять | То хай світять мовчки. Як ти мав би, ботан. | Then let them shine in silence. Like you should, nerd.
^dusk.світять | Нашо тоді вони? Тіко очі муляють. | What are they for then? Just bugging my eyes.
^dusk.живі | Живі — то нудно. Ну… хай вже. | Alive is boring. Well… fine.
^dusk.живі | Живі, ага. Завтра надолужимо. | Alive, sure. We'll make up for it tomorrow.
^dusk.рахуєш | Трупи рахувать веселіше. | Counting corpses is more fun.
^dusk.рахуєш | Дні рахувать — то як у тюрязі. Я краще трупи. | Counting days is like jail. I'll take corpses.
^dusk.вечерею | Мною вдавляться. Я гіркий, {м_слівце}. | They'll choke on me. I'm bitter, {м_слівце}.
^dusk.вечерею | Хай пробують. Я застрягаю в горлі. | Let them try. I get stuck in throats.
^dusk.не_відповідай | А я відповім. Дзьобом. По сусалах. | I'll answer. With my beak. In the chops.
^dusk.не_відповідай | «Будь ласка»? Ну… добре. Мовчу. Тобі ж страшно. | «Please»? Well… fine. I'm quiet. You're scared, after all.
^dusk.дах | Дах — то кайф. Аби не капало, як того разу, бля. | A roof is great. As long as it doesn't leak like last time, fuck.
^dusk.дах | Сподівайся. А я вже знаю, шо будуть кущі. | Keep hoping. I already know it'll be bushes.
^dusk.дим | Дим? То лети першим, перевір. | Smoke? Then fly ahead and check.
^dusk.дим | Дим — то добре. Дим — то хтось смажить. | Smoke is good. Smoke means someone's roasting.
`);

// ===================== emit =====================
const out = [];
for (const key of Object.keys(N)) {
  const [who, list] = key.split(':'); const o = old[key]; if (!o) throw new Error('no old list ' + key);
  const nw = N[key];
  nw.forEach(([mk, uk, en], i) => {
    const u = mk + ' ' + uk, e = mk + ' ' + en;
    if (i < o.length) out.push({ who, list, op: 'set', old: o[i], uk: u, en: e });
    else out.push({ who, list, op: 'add', uk: u, en: e });
  });
  for (let i = nw.length; i < o.length; i++) out.push({ who, list, op: 'del', old: o[i] });
}
for (const k of Object.keys(old)) if (!N[k]) console.log('UNTOUCHED old list', k);
fs.writeFileSync(path.join(dir, 'edits_B.json'), JSON.stringify(out, null, 1));

// ===================== checks =====================
const tiers = mk => { const m = /^~(\d+) /.exec(mk); return m ? m[1].split('').map(Number) : [1, 2, 3]; };
const thr = mk => mk.replace(/^~\d+ /, '').slice(1);
const steps = {
  rest_a: ['H:х_вогонь', 'M:м_вогонь', 'H:х_тепло'], rest_b: ['M:м_вогонь_почин', 'H:х_вогонь_відповідь', 'M:м_кінець'],
  rain: ['M:м_дощ', 'H:х_дощ', 'M:м_дощ_кінець'], fog: ['H:х_туман', 'M:м_туман', 'H:х_кінець'], night: ['M:м_ніч', 'H:х_ніч', 'M:м_кінець'],
  snow: ['M:м_сніг', 'H:х_сніг', 'M:м_сніг_кінець'], storm: ['H:х_гроза', 'M:м_гроза', 'H:х_гроза_кінець'],
  dawn: ['H:х_світанок', 'M:м_світанок', 'H:х_світанок_кінець'], dusk: ['M:м_вечір', 'H:х_вечір', 'M:м_вечір_кінець'],
};
let bad = 0;
const pre = sc => x => thr(x[0]).startsWith(sc + '.');
for (const sc in steps) {
  const [a, b, c] = steps[sc].map(k => N[k].filter(pre(sc)));
  const opens = new Set(a.map(x => thr(x[0])));
  for (const tr of [1, 2, 3]) if (a.filter(x => tiers(x[0]).includes(tr)).length < 3) { bad++; console.log('few openings', sc, tr); }
  for (const t of opens) for (const tr of [1, 2, 3]) { const n = b.filter(x => thr(x[0]).split('>')[0].split('|').includes(t) && tiers(x[0]).includes(tr)).length; if (n < 2) { bad++; console.log('reply short', sc, t, tr, n); } }
  const nexts = new Set(b.map(x => thr(x[0]).split('>')[1]));
  for (const x of b) if (!thr(x[0]).includes('>')) { bad++; console.log('reply no chain', sc, x[0]); }
  for (const t of nexts) for (const tr of [1, 2, 3]) { const n = c.filter(x => thr(x[0]).split('|').includes(t) && tiers(x[0]).includes(tr)).length; if (n < 2) { bad++; console.log('close short', sc, t, tr, n); } }
  for (const x of c) if (!thr(x[0]).split('|').every(t => nexts.has(t))) { bad++; console.log('orphan close', sc, x[0]); }
  for (const x of b) for (const t of thr(x[0]).split('>')[0].split('|')) if (!opens.has(t)) { bad++; console.log('orphan reply', sc, x[0]); }
}
for (const k in N) for (const x of N[k]) {
  const t = thr(x[0]); const p = t.split(/[>|]/).map(s => s.split('.')[0]);
  if (!p.every(s => s in steps)) { bad++; console.log('prefix', k, x[0]); }
  if (new Set(p).size > 1) { bad++; console.log('mixed prefix', k, x[0]); }
  const br = s => (s.match(/[{}]/g) || []).length; if (br(x[1]) % 2) { bad++; console.log('brace', x[1]); }
  const ph = s => (s.match(/\{[^}]*\}/g) || []).filter(z => !z.includes('|')).sort().join(','); if (ph(x[1]) !== ph(x[2])) console.log('placeholder diff', k, x[1], '||', x[2]);
}
// munin swear count (counting {м_слівце})
const sw = /(бля|курв|сук|нахуй|нахуя|хуй|хуя|хуйн|пизд|їба|їбал|сран|срак|гівн|похуй|падлюк|\{м_слівце\})/gi;
const roots = s => (s.match(sw) || []).map(z => z.toLowerCase().slice(0, 3));
for (const k in N) if (k.startsWith('M:')) for (const x of N[k]) { const m = roots(x[1]); const lim = ['M:м_вогонь', 'M:м_дощ', 'M:м_кінець'].includes(k) ? 2 : 2; if (m.length > lim) { bad++; console.log('swears', m.length, k, x[1]); } if (new Set(m).size < m.length) { bad++; console.log('repeat root', x[1]); } }
// hugin: no hard swears
for (const k in N) if (k.startsWith('H:')) for (const x of N[k]) if (/(бля|курв|хуй|пизд|їба|гівн)/i.test(x[1])) console.log('hugin swear?', x[1]);
console.log('ops', out.length, 'set', out.filter(x => x.op === 'set').length, 'add', out.filter(x => x.op === 'add').length, 'del', out.filter(x => x.op === 'del').length, 'bad', bad);
const per = {}; for (const e of out) { const sc = (e.uk || '').match(/\^([a-z_]+)\./); const s = sc ? sc[1] : 'del'; per[s] = per[s] || { set: 0, add: 0, del: 0 }; per[s][e.op]++; }
console.log(JSON.stringify(per));

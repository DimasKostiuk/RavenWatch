// builds edits_C.json (packet C: boss, near_death, death, biome, cross_h, cross_m, walk, sail, friend)
// every opening has its own point; every reply chains to its own sub-point; closes answer that reply.
// old lines are parsed from packet_C.md and replaced in place (set), the rest added / deleted.
const fs = require('fs'), path = require('path');
const dir = __dirname;
const pk = fs.readFileSync(path.join(dir, 'packet_C.md'), 'utf8').split(/\r?\n/);
const old = {}; let cur = null;
for (const l of pk) {
  const h = /^### ([HM]) @(\S+)/.exec(l); if (h) { cur = h[1] + ':' + h[2]; old[cur] = []; continue; }
  if (cur && /^\d+ \| /.test(l)) { const parts = l.split(' | '); old[cur].push(parts[1].trim()); }
  else if (l.startsWith('#')) cur = null;
}
// opening: [tone, uk, en]; reply: [tone, sub, uk, en, closes|null]; close: [tone, uk, en]
const S = {};
// ============================== BOSS (H open, M reply, H? close) ==============================
S.boss = { O: 'H:х_бос', R: 'M:м_бос', C: 'H:х_бос_осадити', items: [
 { p: 'впорались', o: [['', 'Непогано впорались із {boss_ins}.', 'We handled {boss} not badly.']], r: [
  ['', 'рахував', '«Впорались»? Я тій тварюці око видовбав. А ти рахував, ботан.', '“Handled”? I pecked its eye out. You were counting, wiseguy.', [
   ['', 'Рахував. Твоїх ударів було два, і обидва повз.', 'Counting. You landed two blows, and both missed.'],
   ['', 'Хтось мусив рахувати. Ти ж до трьох не вмієш.', 'Someone had to count. You can\'t get past three.']]],
  ['', 'розмазали', 'Непогано? Та ми {boss_gen} розмазали, як кашу по столу!', 'Not badly? We smeared {boss} like porridge on a table!', [
   ['', 'Ми? {name} розмазува{в|ла}. Ти розмазувався по плечу.', 'We? {name} did the smearing. You smeared yourself flat on the shoulder.'],
   ['', 'Розмазали. Тільки не розповідай це в кожній корчмі.', 'Smeared, yes. Just don\'t tell it in every tavern.']]],
  ['~1', 'мало', 'Мало. Давай ше одного. Я тіко розігрівся.', 'Not enough. Bring another one. I\'m just warmed up.', [
   ['', 'Розігрівся ти під кущем. Там тепло.', 'You warmed up under a bush. It\'s cosy there.'],
   ['', 'Ще одного {name} не потягне. А ти — тим паче.', '{name} can\'t take another one. And you even less.']]],
  ['~3', 'молодці', 'Красиво вийшло, {м_слівце}. Всі молодці. І ти, ботан. Ну, трошки.', 'That went beautifully, {м_слівце}. Everyone did well. You too, wiseguy. A little.', [
   ['', '«Трошки» — щедро як на тебе. Приймаю.', '“A little” is generous, coming from you. Accepted.'],
   ['', 'Запам\'ятаю, що ти це сказав. Шкода, без свідків.', 'I\'ll remember you said that. Pity there were no witnesses.']]],
 ]},
 { p: 'одін', o: [['', '{boss} лежить. Одін буде задоволений.', '{boss} is down. Odin will be pleased.']], r: [
  ['', 'нагорода', 'Одін? А мені шо з того? Хоч би хробака кинув.', 'Odin? What do I get out of it? He could at least toss me a worm.', [
   ['', 'Одін платить не хробаками. Він платить тим, що ти ще живий.', 'Odin doesn\'t pay in worms. He pays by letting you live.'],
   ['', 'Тобі — хробак. Мені — похвала. Кожному своє.', 'A worm for you. Praise for me. To each his own.']]],
  ['', 'найсильніше', 'Одін бачив, хто бив найсильніше. Я. Ну а хто ж іще.', 'Odin saw who hit hardest. Me. Who else.', [
   ['', 'Він бачив, хто кричав найголосніше. Оце правда був ти.', 'He saw who screamed loudest. That really was you.'],
   ['', 'Найсильніше ти бив крилом мене по голові. Двічі.', 'The hardest thing you hit was my head, with your wing. Twice.']]],
  ['~23', 'найсильніше', 'Хай радіє. І хай запише, хто бив найсильніше. Я.', 'Let him be pleased. And let him write down who hit hardest. Me.', null],
 ]},
 { p: 'трофей', o: [['', 'Ще один трофей на камінь. Голова {boss_gen} гарно там висітиме.', 'One more trophy for the stone. The head of {boss} will look good up there.']], r: [
  ['', 'сидіти', 'Висітиме, ага. А я на ній сидітиму. Як король.', 'It will, yeah. And I\'ll sit on it. Like a king.', [
   ['', 'Сидітимеш, поки {name} не зжене. Тобто хвилину.', 'Until {name} shoos you off. So, a minute.'],
   ['', 'Король на чужій голові. Тобі пасує.', 'A king on someone else\'s head. Suits you.']]],
  ['', 'очі', 'Голову — на камінь, а мені шо? Очі хоч віддайте.', 'The head goes on the stone, and me? At least give me the eyes.', [
   ['', 'Очі — частина трофея. Перебʼєшся.', 'The eyes are part of the trophy. You\'ll live.'],
   ['', 'Бери. Висітиме сліпа голова, і всі знатимуть, хто постарався.', 'Take them. A blind head will hang there, and everyone will know whose work it is.']]],
 ]},
 { p: 'загинемо', o: [['~1', 'Гадав, ми цього разу точно загинемо.', 'I thought this time we\'d die for sure.']], r: [
  ['', 'гадав', 'Ти гадав, а я бився. Тому ти й гадав, шо загинемо.', 'You were thinking, I was fighting. That\'s why you thought we\'d die.', [
   ['', 'Я гадав правильно. Нам просто пощастило.', 'I thought right. We just got lucky.'],
   ['', 'Ти бився з моїм хвостом, коли ховався за мене.', 'You fought my tail while hiding behind me.']]],
  ['', 'безсмертний', 'Загинемо? Я? Та я безсмертний, бля.', 'Die? Me? I\'m fucking immortal.', [
   ['', 'Безсмертний поки що тільки твій язик.', 'So far the only immortal thing is your tongue.'],
   ['', 'Перевіримо на наступному. Тільки без мене.', 'We\'ll test it on the next one. Without me.']]],
 ]},
 { p: 'пишаюся', o: [['~3', 'Пишаюся. Нами всіма.', 'I\'m proud. Of all of us.']], r: [
  ['', 'розкис', 'Ого. Хугін розкис. Запишіть хтось, бо ніхто не повірить.', 'Whoa. Hugin went soft. Someone write it down, nobody will believe it.', [
   ['', 'Не розкис. Констатував. А тепер забудь.', 'Not soft. Stating a fact. Now forget it.'],
   ['', 'Записуй. Тільки твоє ім\'я там буде дрібненько.', 'Write it. Only your name goes in small print.']]],
  ['', 'ще_раз', 'Всіма? І мною? Скажи ше раз, бо я не розчув.', 'All of us? Me too? Say it again, I didn\'t catch it.', [
   ['', 'Не скажу. Одного разу й так забагато.', 'I won\'t. Once was already too much.'],
   ['', 'Розчув. По тобі видно.', 'You caught it. It\'s written all over you.']]],
  ['~3', 'нікому', 'Я теж, ботан. Тіко нікому, ясно?', 'Me too, wiseguy. Just not a word to anyone, got it?', [
   ['', 'Нікому. Мені однаково ніхто не повірить.', 'Not a word. Nobody would believe me anyway.'],
   ['', 'Мовчу. Вперше ти просиш тиші.', 'Silent. First time you\'ve ever asked for quiet.']]],
 ]},
 { p: 'шрами', o: [['', '{name} {увесь|уся} в подряпинах. Боси так просто не лягають.', '{name} is all scratched up. Bosses don\'t go down easily.']], r: [
  ['', 'загояться', 'Подряпини загояться. А {boss} — вже ні. Ха!', 'Scratches heal. {boss} won\'t. Ha!', [
   ['', 'Загояться. Якщо {name} хоч раз поїсть і поспить по-людськи.', 'They\'ll heal. If {name} eats and sleeps properly for once.'],
   ['', 'Аби загоїлися до наступного. Бо наступний буде злішим.', 'As long as they heal before the next one. The next one will be meaner.']]],
  ['', 'клював', 'Не лягають, бо ти їх тіко рахуєш. Я клював — і лягло.', 'They don\'t, because you just count them. I pecked, and down it went.', [
   ['', 'Лягло від сокири. Ти клював повітря.', 'It went down from the axe. You pecked the air.'],
   ['', 'Клював, ага. Переважно мене, коли лякався.', 'Pecked, sure. Mostly me, whenever you got scared.']]],
  ['', 'пірʼїна', 'Подряпини — то фігня. В мене ось пір\'їна вилізла, от де біда.', 'Scratches are nothing. I lost a feather here, now that\'s a tragedy.', [
   ['', '{name} у крові, а ти про пір\'їну. Тепер ясно, чому з тобою ніхто не дружить.', '{name} is bleeding and you\'re on about a feather. Now I see why nobody likes you.'],
   ['', 'Пір\'їна відросте. Совість — ні, бо її й не було.', 'The feather grows back. The conscience won\'t, you never had one.']]],
 ]},
 { p: 'тиша', o: [['', 'Як тихо стало. Наче світ видихнув.', 'How quiet it\'s got. As if the world breathed out.']], r: [
  ['', 'перднув', '{boss} лежить, от світ і видихнув. Я теж. І перднув трохи, вибач.', '{boss} is down, so the world breathed out. Me too. And farted a little, sorry.', [
   ['', 'Тепер ясно, чому так тихо. Усі затамували подих.', 'Now I see why it\'s so quiet. Everyone is holding their breath.'],
   ['', 'Я вибачаю. Світ — навряд.', 'I forgive you. The world probably won\'t.']]],
  ['', 'нудно', 'Тиша, тиша… Нудно, курва. Давай наступного.', 'Quiet, quiet… Boring as fuck. Bring the next one.', [
   ['', 'Нудно тобі ненадовго. Наступний сам тебе знайде.', 'Not boring for long. The next one will find you itself.'],
   ['', 'Посидь у тиші. Може, хоч одна думка прилетить.', 'Sit in the quiet. Maybe a thought will fly in for once.']]],
 ]},
 { p: 'пісня', o: [['', 'Про цей бій складуть пісню. Я вже знаю перший рядок.', 'They\'ll make a song about this fight. I already know the first line.']], r: [
  ['', 'рядок', 'Перший рядок: «Мунін завалив {boss_gen}». А решта — хуйня, не важлива.', 'First line: “Munin killed {boss}.” The rest is shit, doesn\'t matter.', [
   ['', 'У такий рядок правда не влазить. Закороткий.', 'The truth won\'t fit in that line. Too short.'],
   ['', 'У моїй пісні ти теж є. У приспіві, під кущем.', 'You\'re in my song too. In the chorus, under a bush.']]],
  ['', 'мʼясо', 'Пісні — для слабаків. Нам — м\'ясо.', 'Songs are for weaklings. Meat is for us.', [
   ['', 'М\'ясо ти з\'їси за вечір. Пісню пам\'ятатимуть сто років.', 'You\'ll eat the meat in one evening. The song will be remembered for a hundred years.'],
   ['', 'Тоді в пісні тебе не буде. Тільки м\'ясо.', 'Then you\'re not in the song. Only the meat.']]],
  ['', 'кажи', 'Ну? Кажи вже той рядок, шо ти тягнеш?', 'Well? Say the line already, what are you dragging it out for?', [
   ['', '«Ворон кричав, а {name} руба{в|ла}». Здогадайся, хто ворон.', '“The raven screamed, and {name} hacked.” Guess who the raven is.'],
   ['', 'Не скажу. Ти переробиш його на лайку.', 'I won\'t. You\'d turn it into swearing.']]],
 ]},
 { p: 'погляд', o: [['', 'Одін дивиться. Я відчуваю його погляд.', 'Odin is watching. I can feel his gaze.']], r: [
  ['', 'повчиться', 'Хай дивиться. Хай повчиться, як треба.', 'Let him watch. Let him learn how it\'s done.', [
   ['', 'Вчити бога — сміливо. Дурно, але сміливо.', 'Teaching a god. Brave. Stupid, but brave.'],
   ['', 'Одін чекає, коли ти чогось навчишся. Давно чекає.', 'Odin is waiting for you to learn something. Has been for ages.']]],
  ['', 'шия', 'Відчуваєш? То в тебе шия затерпла, ботан.', 'Feel it? That\'s your neck gone stiff, wiseguy.', [
   ['', 'Затерпла, бо ти висів на мені весь бій.', 'Stiff because you hung off me the whole fight.'],
   ['', 'Ні. Він дивиться на тебе. Мені просто незручно поруч.', 'No. He\'s looking at you. I\'m just embarrassed to sit next to you.']]],
 ]},
 { p: 'біль', o: [['', '{name} ледь стоїть. Перемога дорого коштувала.', '{name} can barely stand. The victory cost dearly.']], r: [
  ['', 'ми_ні', 'Нічо, загоїться. Головне — {boss} лежить, а ми ні.', 'It\'ll heal. What matters is {boss} is down and we\'re not.', [
   ['', 'Ми ні. Цього разу.', 'We\'re not. This time.'],
   ['', 'Лежить. А {name} хай сяде. Дай {йому|їй} спокій.', 'It\'s down. Now let {name} sit. Leave {him|her} be.']]],
  ['', 'вивалив', 'Дорого? Та норм. Я б і більше вивалив.', 'Dearly? It\'s fine. I\'d have paid more.', [
   ['', 'Ти нічого не платив. Ти дивився.', 'You paid nothing. You watched.'],
   ['', 'Вивалив би, ага. З чужої кишені, як завжди.', 'Sure you would. Out of someone else\'s pocket, as always.']]],
  ['~3', 'сядь', 'Ей, {name}, сядь, бляха. Не геройствуй хоч зара.', 'Hey, {name}, sit down, damn it. No heroics, not now.', [
   ['', 'Вперше з тобою згоден. Сядь, {name}.', 'First time I agree with you. Sit, {name}.'],
   ['', 'Чуєш, {name}? Навіть Мунін каже сісти.', 'Hear that, {name}? Even Munin says sit down.']]],
 ]},
 { p: 'наступний', o: [['', 'Хто наступний, як думаєш?', 'Who\'s next, do you think?']], r: [
  ['', 'готовий', 'Та хоч хто. Я готовий, хоч зара.', 'Whoever. I\'m ready, right now.', [
   ['', 'Ти готовий. {name} — ні. Почекаєш.', 'You\'re ready. {name} isn\'t. You\'ll wait.'],
   ['', 'Готовий ти був і цього разу. Під кущем.', 'You were ready this time too. Under a bush.']]],
  ['', 'найбільший', 'Найбільшого давай. Шоб було шо клювати.', 'The biggest one. So there\'s something to peck.', [
   ['', 'Найбільший і тебе склює. Не поспішай.', 'The biggest one pecks back. Don\'t rush.'],
   ['', 'Буде найбільший. Тоді й побачимо, де твій дзьоб.', 'You\'ll get the biggest. Then we\'ll see where your beak goes.']]],
 ]},
 { p: 'страх', o: [['', 'Зізнаюся: на мить мені стало страшно.', 'I admit it: for a moment I got scared.']], r: [
  ['', 'трошки', 'Мені — ні. Ну… трошки. Трошечки. Відчепись.', 'Not me. Well… a little. A tiny bit. Back off.', [
   ['', 'Трошки — це коли тремтить плече. Плече тремтіло.', 'A tiny bit is when the shoulder shakes. The shoulder shook.'],
   ['', 'Відчепився. Але запам\'ятав.', 'Backing off. But I\'ll remember.']]],
  ['', 'теж', 'Страшно, кажеш? Ссикло. Але я теж, якшо чесно.', 'Scared, you say? Chicken. Me too, honestly.', [
   ['', 'Чесно — це щось новеньке. Мені подобається.', 'Honest. That\'s new. I like it.'],
   ['', 'Двоє боягузів на одних плечах. Гарна компанія.', 'Two cowards on one pair of shoulders. Fine company.']]],
 ]},
]};
// ============================== NEAR_DEATH (M open, H reply, M? close) ==============================
S.near_death = { O: 'M:м_ледве', R: 'H:х_ледве', C: 'M:м_кінець', items: [
 { p: 'валькірія', o: [['', 'Я вже валькірію бачив, бля. Страшна, як твоя пика.', 'I already saw a valkyrie, fuck. Ugly as your mug.']], r: [
  ['', 'махала', 'Я теж її бачив. Вона махала тобі, не мені.', 'I saw her too. She was waving at you, not me.', [
   ['', 'Махала, бо я гарний. Тобі не зрозуміти.', 'She waved because I\'m handsome. You wouldn\'t get it.'],
   ['', 'Хай махає. Я їй дулю показав.', 'Let her wave. I showed her the finger.']]],
  ['', 'гарна', 'Страшна, як моя? Значить, гарна. Ти просто заздриш.', 'As ugly as mine? So, pretty. You\'re just jealous.', [
   ['', 'Гарна, ага. Як жаба в болоті.', 'Pretty, sure. Like a toad in a swamp.'],
   ['', 'Заздрю? Тобі? Та я б краще валькірію поцілував.', 'Jealous? Of you? I\'d sooner kiss the valkyrie.']]],
  ['~1', 'неприлітають', 'Валькірії по таких, як ти, не прилітають.', 'Valkyries don\'t come for the likes of you.', [
   ['', 'Прилетять. Я того вартий, шоб ти знав.', 'They will. I\'m worth it, so you know.'],
   ['', 'Ну й не треба. Я сам до Одіна дочимчикую.', 'Fine by me. I\'ll waddle to Odin on my own.']]],
 ]},
 { p: 'добʼю', o: [['', 'Ше раз таке втнеш — я тебе сам доб\'ю. Чесно.', 'Pull that again and I\'ll finish you off myself. Honest.']], r: [
  ['', 'годувати', 'Доб\'єш — а хто тебе тоді годуватиме?', 'Finish {him|her} off, and who feeds you then?', [
   ['', 'Сам себе прогодую. Тобою, ботан.', 'I\'ll feed myself. On you, wiseguy.'],
   ['', 'Годуватиме, ага. Та {name} й так через раз годує.', 'Feeds me, yeah. {name} only does it every other time anyway.']]],
  ['', 'потрібен', 'Не погрожуй. Без {нього|неї} ти нікому не потрібен.', 'Don\'t threaten {name}. Without {him|her} nobody needs you.', [
   ['', 'Потрібен. Тобі. Бо на кого ти тоді бурчатимеш.', 'Somebody does. You. Who else would you grumble at.'],
   ['', 'А ти, типу, потрібен? Нас двоє таких, ботан.', 'And you\'re needed, are you? There\'s two of us, wiseguy.']]],
 ]},
 { p: 'пʼяти', o: [['', 'Серце в п\'ятах. Де в ворона п\'яти — хто його зна, але там.', 'Heart in my heels. Where a raven\'s heels are, who knows, but it\'s there.']], r: [
  ['', 'серце', 'У тебе є серце? Оце новина.', 'You have a heart? That\'s news.', [
   ['', 'Є в мене серце. Маленьке. Чорне. І злюще.', 'I do have a heart. Small. Black. And mean.'],
   ['', 'Є. І воно зара калатає, як дурне. Не ржи.', 'I do. And right now it\'s pounding like an idiot. Don\'t laugh.']]],
  ['', 'совість', 'П\'яти в тебе там, де совість. Тобто ніде.', 'Your heels are where your conscience is. Nowhere.', [
   ['', 'Совість мені нашо? Мені й без неї зашибісь.', 'What do I need a conscience for? I\'m doing great without one.'],
   ['', 'Ну й нехай ніде. Зате серце є, а в тебе — тіко мізки.', 'Fine, nowhere. But I\'ve got a heart, and you\'ve only got brains.']]],
 ]},
 { p: 'всрався', o: [['', 'Я ледь не всрався. Ну… трошки таки да.', 'I nearly shat myself. Well… a little, actually.']], r: [
  ['', 'тепле', 'Помітно. Плече ще тепле.', 'It shows. The shoulder is still warm.', [
   ['', 'То не я! То… від страху. Ну, я. Мовчи.', 'That wasn\'t me! That was… fear. Fine, me. Shut up.'],
   ['', 'Тепле — значить, живі. Радій, ботан.', 'Warm means alive. Be glad, wiseguy.']]],
  ['', 'пискнув', 'Не тільки ти, Муніне. Я теж пискнув. Тихо, але пискнув.', 'Not only you, Munin. I squeaked too. Quietly, but I did.', [
   ['', 'Пискнув? Хугін пискнув! Оце я всім розкажу.', 'Squeaked? Hugin squeaked! I\'m telling everyone.'],
   ['', 'Ну от. А строїш із себе героя.', 'There you go. And you play the hero.']]],
  ['', 'скільки', '«Трошки» — це скільки? Хоча ні, не кажи.', '“A little” is how much? No, don\'t tell me.', [
   ['', 'Не скажу. Але плече зара краще не нюхати.', 'I won\'t. But better not sniff the shoulder right now.'],
   ['', 'Скільки треба, стільки й всрався. Не твоє діло.', 'As much as it took. None of your business.']]],
 ]},
 { p: 'зілля', o: [['', 'Хто, {м_слівце}, зілля забув? Я питаю: хто?! Ти?!', 'Who, {м_слівце}, forgot the potion? I\'m asking: who?! You?!']], r: [
  ['', 'нагадував', 'Я нагадував. Двічі. Мене не слухають.', 'I reminded. Twice. Nobody listens to me.', [
   ['', 'Нагадував він. Треба було дзьобом у потилицю нагадувати.', 'He reminded. Should\'ve reminded with a beak to the back of the head.'],
   ['', 'Тихо нагадував, ось шо. Треба кричати, як я.', 'You reminded too quietly, that\'s what. You have to yell, like me.']]],
  ['', 'логіка', 'Зілля в сумці. Сумка — на базі. Логіка.', 'The potion\'s in the bag. The bag\'s at the base. Logic.', [
   ['', 'Логіка, {м_слівце}, ага. Наступного разу сам носи, розумний.', 'Logic, {м_слівце}, right. Next time carry it yourself, smartass.'],
   ['', 'Логіка в тебе є, а зілля нема. Чудово.', 'You\'ve got logic and no potion. Great.']]],
  ['', 'крила', 'Не я. Я сумок не ношу. У мене крила.', 'Not me. I don\'t carry bags. I have wings.', [
   ['', 'Крила в нього. Хоч би раз ними шось корисне приніс.', 'Wings, he says. Bring something useful with them for once.'],
   ['', 'То на крилах і принось наступного разу. Носильник.', 'Then bring it on your wings next time. Porter.']]],
 ]},
 { p: 'гордість', o: [['', 'Тікати — не сором. Сором — то від кабана здохнути, бля.', 'Running isn\'t shameful. Shameful is croaking to a boar, fuck.']], r: [
  ['', 'гордість', 'Від кабана — ні. Від гордості — так.', 'From a boar, no. From pride, yes.', [
   ['', 'Гордості в мене нема. Тіко злість. А злість не вбиває.', 'I\'ve got no pride. Only rage. And rage doesn\'t kill.'],
   ['', 'Ой, філософ. Я жити хочу, а не гордитись.', 'Oh, a philosopher. I want to live, not be proud.']]],
  ['', 'вміє', 'Тікати вміти треба. {name} вміє.', 'Running is a skill. {name} has it.', [
   ['', 'Вміє, ага. Аж я з плеча мало не злетів.', 'Has it, yeah. I nearly flew off the shoulder.'],
   ['', 'Вміє. Я {його|її} й навчив, шоб ти знав.', 'Has it. I\'m the one who taught {him|her}, so you know.']]],
 ]},
 { p: 'весело', o: [['~1', 'Ше б секунда — і було б весело. Похорони, все таке.', 'One more second and it\'d have been fun. A funeral, the works.']], r: [
  ['', 'болить', 'Тобі все весело, поки не болить.', 'Everything\'s fun for you until it hurts.', [
   ['', 'Болить — то ше веселіше, ге-ге.', 'When it hurts it\'s even more fun, heh-heh.'],
   ['', 'А мені й не болить. Мені смішно, бо ми живі.', 'It doesn\'t hurt me. I\'m laughing because we\'re alive.']]],
  ['', 'мовчиш', 'Весело — це коли ти мовчиш.', 'Fun is when you keep quiet.', [
   ['', 'То тобі весело. А мені нудно.', 'That\'s fun for you. For me it\'s boring.'],
   ['', 'Не діждешся. Я б на тих похоронах ше й співав.', 'Not a chance. I\'d have sung at that funeral too.']]],
 ]},
 { p: 'лякай', o: [['~3', 'Не лякай мене так, {м_слівце}. Я ж не залізний.', 'Don\'t scare me like that, {м_слівце}. I\'m not made of iron.']], r: [
  ['', 'запишу', 'Ти злякався. Запишу, бо ніхто не повірить.', 'You got scared. I\'ll write it down, nobody would believe it.', [
   ['', 'Не записуй! Бо видзьобаю, ясно.', 'Don\'t write it! Or I\'ll peck it out, got it.'],
   ['', 'Запишеш — я ту сторінку з\'їм. Разом із тобою.', 'Write it and I\'ll eat that page. With you.']]],
  ['', 'живий', 'Не залізний. Зате живий. Ми всі троє.', 'Not iron. But alive. All three of us.', [
   ['', 'Живий. Ну й добре. Бо без мене ти б засумував.', 'Alive. Good. Without me you\'d be moping.'],
   ['', 'Троє, ага. Шоб так і лишалось, ясно.', 'Three, yeah. Keep it that way, got it.']]],
 ]},
 { p: 'руки', o: [['', 'Глянь, руки трусяться. А в мене — лапи. Не кажи нікому.', 'Look, the hands are shaking. And my feet. Don\'t tell anyone.']], r: [
  ['', 'трусяться', 'Трусяться — значить, живі. Усі троє.', 'Shaking means alive. All three of us.', [
   ['', 'Живі. Але лапам хай би вже попустило.', 'Alive. But I wish my feet would calm down.'],
   ['', 'Ну, хоч так. Тіко нікому, я сказав.', 'Well, there\'s that. Not a word, I said.']]],
  ['', 'нескажу', 'Не скажу. Тільки не тримайся за мене, бо я теж трушуся.', 'I won\'t. Just don\'t hold on to me, I\'m shaking too.', [
   ['', 'О, і ти? То нас уже троє трусунів.', 'Oh, you too? So that\'s three of us shaking.'],
   ['', 'Тримаюсь, бо хочу. Мовчи вже.', 'I hold on because I want to. Shut up already.']]],
  ['', 'злість', 'Лапи в тебе трусяться завжди. Від злості.', 'Your feet always shake. From rage.', [
   ['', 'Від злості. Так і запишем. Від злості.', 'From rage. Write that down. From rage.'],
   ['', 'Від злості, ага. Я ше ту гниду не доклював.', 'From rage, yeah. I haven\'t finished pecking that bastard.']]],
 ]},
]};
// ============================== DEATH (H open, M reply, H? close) ==============================
S.death = { O: 'H:х_смерть', R: 'M:м_смерть', C: 'H:х_кінець', items: [
 { p: 'тіло', o: [['', 'Ну от. Знову довелося шукати тіло.', 'There we go. Had to look for the body again.']], r: [
  ['', 'манатки', 'Тіло шукати — то пів біди. А манатки потім тягти — от де пиздець.', 'Finding the body is half the trouble. Hauling the gear back, now that\'s fucked.', [
   ['', 'Тягти буде {name}. Ти сидиш і коментуєш.', '{name} does the hauling. You sit and comment.'],
   ['', 'Тягнути не тобі. Тобі — тільки нити дорогою.', 'You\'re not hauling. You\'re only whining on the way.']]],
  ['', 'мухи', 'Шукати? Я здалеку бачив, де воно лежить. Мухи підказали.', 'Look for it? I saw where it lay from afar. The flies told me.', [
   ['', 'Мухи завжди знаходять першими. Ти — другим.', 'Flies always find it first. You come second.'],
   ['', 'Добре, що хоч мухи з тобою дружать.', 'Good that at least the flies are your friends.']]],
 ]},
 { p: 'незручно', o: [['', 'Смерть — не кінець. Але дуже незручно.', 'Death isn\'t the end. But it\'s very inconvenient.']], r: [
  ['', 'нележав', 'Незручно? Ти, {м_слівце}, там не лежав, шоб казати.', 'Inconvenient? You weren\'t lying there, {м_слівце}, to say that.', [
   ['', 'Лежав. Поруч. Ти лежав на мені.', 'I was. Right next to you. You were lying on me.'],
   ['', 'Лежали ми обидва. Тільки ти ще й лаявся.', 'We both lay there. Only you were swearing too.']]],
  ['', 'мокре', 'Незручно — то коли пір\'я мокре. А це — пиздець.', 'Inconvenient is wet feathers. This is a fucking disaster.', [
   ['', 'Хай так. Але ти й від мокрого пір\'я так кричиш.', 'Fine. But you scream like that over wet feathers too.'],
   ['', 'Точне слово. Вперше за тиждень.', 'The right word. First time this week.']]],
 ]},
 { p: 'обходити', o: [['', 'Запам\'ятаємо це місце. Обходитимемо.', 'Let\'s remember this place. We\'ll go around it.']], r: [
  ['', 'поклюю', 'Обходити? Я туди вернусь і всіх там поклюю, {м_слівце}.', 'Go around it? I\'ll go back there and peck everyone, {м_слівце}.', [
   ['', 'Вернешся. На чужому плечі. Як завжди.', 'You\'ll go back. On someone else\'s shoulder. As always.'],
   ['', 'Ми вже раз туди повернулися. Ти бачив, чим скінчилось.', 'We went back there once. You saw how that ended.']]],
  ['', 'ссикло', 'Ти й так усе обходиш. Ссикло.', 'You go around everything anyway. Chicken.', [
   ['', 'Обходжу — і досі цілий. Порівняй із нашим лежанням.', 'I go around, and I\'m still whole. Compare that to our lying there.'],
   ['', 'Боягуз, зате живий. І ти живий, бо сидиш поруч.', 'A coward, but alive. And you\'re alive because you sit next to me.']]],
 ]},
 { p: 'казав', o: [['~1', 'Я ж казав. Я завжди кажу.', 'I told you. I always tell you.']], r: [
  ['', 'неслухає', 'Ти завжди кажеш. І шо? Хтось тебе слухає? Нє.', 'You always tell us. So what? Anyone listen? Nope.', [
   ['', 'Отож і лежали. Через твоє «нє».', 'That\'s why we were lying there. Because of your “nope”.'],
   ['', 'Не слухають. Тому я й кажу двічі.', 'Nobody listens. That\'s why I say it twice.']]],
  ['', 'язиком', 'Кажеш, кажеш… Краще б зілля носив, а не язиком чесав.', 'Telling, telling… You\'d better carry potions instead of flapping your tongue.', [
   ['', 'Носив би. Тільки ти б його випив дорогою.', 'I would. Only you\'d drink it on the way.'],
   ['', 'Язиком я хоча б не лаюся. Порівняй.', 'At least my tongue doesn\'t swear. Compare.']]],
 ]},
 { p: 'повернувся', o: [['~3', 'Головне, що {name} поверну{вся|лась}.', 'What matters is {name} came back.']], r: [
  ['', 'нероби', 'Добре, шо верну{вся|лась}, {м_слівце}. Більше так не роби, чуєш? Не роби.', 'Good you\'re back, {м_слівце}. Don\'t do that again, hear? Don\'t.', [
   ['', 'Чує. Але зробить, ти ж знаєш.', 'Hears you. But will do it again, you know that.'],
   ['', 'Слухай Муніна, {name}. Раз у житті він правий.', 'Listen to Munin, {name}. For once in his life he\'s right.']]],
  ['', 'мерзли', 'Поверну{вся|лась}, а ми тут мерзли, як дурні.', 'Came back, and we were freezing here like idiots.', [
   ['', 'Мерзли. Але чекали. Це важливіше.', 'Freezing. But waiting. That matters more.'],
   ['', 'Мерз ти. Я — думав. Від думок тепліше.', 'You were freezing. I was thinking. Thinking keeps you warm.']]],
 ]},
 { p: 'речі', o: [['', 'Речі лежать там, де впали. Підемо по них, поки хтось не поцупив.', 'The things lie where they fell. Let\'s go get them before someone pinches them.']], r: [
  ['', 'зʼїм', 'Хто спиздить — того я з\'їм. Живцем.', 'Whoever nicks them, I\'ll eat. Alive.', [
   ['', 'Злодія з\'їси, а речі однаково нестиме {name}.', 'You\'ll eat the thief, and {name} still carries the stuff.'],
   ['', 'З\'їси. Тільки спершу дожени.', 'You\'ll eat him. Catch him first.']]],
  ['', 'геройство', 'Підем. Тіко цього разу без геройства, бо знов ляжемо.', 'Let\'s go. Only no heroics this time, or we\'ll lie down again.', [
   ['', 'Без геройства. Це й тобі, Муніне.', 'No heroics. That goes for you too, Munin.'],
   ['', 'Вперше кажеш розумне. Запам\'ятаю.', 'First sensible thing you\'ve said. I\'ll remember it.']]],
 ]},
 { p: 'ліжко', o: [['', 'Ну хоч прокину{вся|лась} у ліжку. Як після доброї пиятики.', 'Well, at least {he|she} woke up in bed. Like after a good drinking bout.']], r: [
  ['', 'голий', 'Після пиятики хоч весело було. А тут — голова болить і голий.', 'After a drinking bout at least it was fun. Here, a headache and naked.', [
   ['', 'Голий, зате в ліжку. Бувало й гірше.', 'Naked, but in bed. It\'s been worse.'],
   ['', 'Пиятику ще відгуляємо. Живими.', 'We\'ll have our feast yet. Alive.']]],
  ['', 'логіка', 'Ліжко — то добре. Могила — гірше. Логіка, ботан.', 'A bed is good. A grave is worse. Logic, wiseguy.', [
   ['', 'Логіка від тебе. Отже, сьогодні диво.', 'Logic from you. So today is a miracle.'],
   ['', 'Вчишся. Повільно, але вчишся.', 'You\'re learning. Slowly, but learning.']]],
 ]},
 { p: 'валькірії', o: [['', 'Валькірії вже кружляли. Я бачив їхні крила.', 'The valkyries were already circling. I saw their wings.']], r: [
  ['', 'пірʼя', 'Я б їм пір\'я повискубував, курвам крилатим.', 'I\'d pluck their feathers out, the winged bitches.', [
   ['', 'Повискубуєш — вони прилетять уже по тебе.', 'Pluck them, and they\'ll come back for you.'],
   ['', 'У них мечі, Муніне. Твоє пір\'я полетить першим.', 'They have swords, Munin. Your feathers would go first.']]],
  ['', 'ненас', 'Хай кружляють. Хуй їм, а не нас.', 'Let them circle. Fuck them, they\'re not getting us.', [
   ['', 'Цього разу полетіли без нас.', 'This time they flew off without us.'],
   ['', 'Не нас. Але кружляли низько. Запам\'ятай.', 'Not us. But they circled low. Remember that.']]],
 ]},
 { p: 'помилка', o: [['', 'Знаєш, де була помилка? Я знаю.', 'You know where the mistake was? I do.']], r: [
  ['', 'непопередив', 'Помилка — шо ти, розумник, не попередив.', 'The mistake was you not warning us, smartass.', [
   ['', 'Я попереджав. Ти кричав голосніше.', 'I did warn. You screamed louder.'],
   ['', 'Попереджав. Ти тоді якраз жука клював.', 'I warned. You were busy pecking a beetle.']]],
  ['', 'name', 'Помилка — то {name}. Завжди. Без варіантів.', 'The mistake is {name}. Always. No options.', [
   ['', 'Помилка — сперечатися з тим, хто тебе носить.', 'The mistake is arguing with the one who carries you.'],
   ['', '{name} помиляється, ти підбадьорюєш. Гарна команда.', '{name} makes mistakes, you cheer. Great team.']]],
  ['', 'кажи', 'Ну? Кажи вже, раз такий умний.', 'Well? Spit it out, if you\'re so clever.', [
   ['', 'Ти закричав, і воно обернулося. Ось і все.', 'You screamed, and it turned around. That\'s all.'],
   ['', 'Полізли, не роздивившись. Усі троє.', 'We went in without looking. All three of us.']]],
 ]},
 { p: 'могила', o: [['', 'Камінь над могилою. Ще один у нашій колекції.', 'A stone over a grave. One more for our collection.']], r: [
  ['', 'цвинтар', 'Колекція, бля. Скоро свій цвинтар відкриємо, з табличкою.', 'A collection, fuck. Soon we\'ll open our own graveyard, with a sign.', [
   ['', 'Табличку напишу я. Ти пишеш тільки лайкою.', 'I\'ll write the sign. You only write in swears.'],
   ['', 'Колись їх стане менше. Сподіваюся.', 'Someday there\'ll be fewer of them. I hope.']]],
  ['', 'памʼятник', 'Хай стоїть. Пам\'ятник дурості. Нашій.', 'Let it stand. A monument to stupidity. Ours.', [
   ['', 'Нашій? Твоїй. Я лише стояв поруч.', 'Ours? Yours. I just stood nearby.'],
   ['', 'Чесно. Хай стоїть і нагадує.', 'Fair. Let it stand and remind us.']]],
 ]},
 { p: 'холод', o: [['', 'Було холодно там, де ми лежали.', 'It was cold where we lay.']], r: [
  ['', 'нехочу', 'Холодно, темно й нудно, як у дупі. Більше туди не хочу.', 'Cold, dark and boring, like up an arse. I don\'t want to go back.', [
   ['', 'Тоді не лізь першим. Нагадаю.', 'Then don\'t charge in first. I\'ll remind you.'],
   ['', 'Не хочеш — то не каркай під руку.', 'Don\'t want to? Then don\'t croak at the wrong moment.']]],
  ['', 'сидів', 'Та я не лежав. Я сидів і чекав, ясно?', 'I wasn\'t lying. I was sitting and waiting, got it?', [
   ['', 'Сидів. Трусився. Я бачив.', 'Sitting. Shivering. I saw.'],
   ['', 'Ясно. Чекав і цокотів дзьобом на весь ліс.', 'Got it. Waiting, and chattering your beak for the whole forest.']]],
 ]},
 { p: 'помста', o: [['', 'Хто це зробив, той ще пожалкує.', 'Whoever did it will regret it.']], r: [
  ['', 'очі', 'Пошкодує, гнида. Я тій гниді очі виклюю. Обидва.', 'Regret it, sure. I\'ll peck that bastard\'s eyes out. Both.', [
   ['', 'Виклюєш. Але спершу хай {name} одягнеться.', 'You will. But first let {name} get dressed.'],
   ['', 'Помстимося. Але з розумом.', 'We\'ll take revenge. But wisely.']]],
  ['', 'пика', 'Пику я вже запам\'ятав. Хай тепер ховається.', 'I\'ve memorised the mug. Now let it hide.', [
   ['', 'Ти пам\'ятаєш пику. Я — дорогу. Підемо разом.', 'You remember the mug. I remember the road. We go together.'],
   ['', 'Не сховається. Але й ти сам не лізь.', 'It won\'t hide. But don\'t go in alone.']]],
 ]},
 { p: 'знову', o: [['', 'Ну що, знову з нуля.', 'Well, from scratch again.']], r: [
  ['', 'злий', 'З нуля, бля. Голий, босий і злий як собака.', 'From scratch, fuck. Naked, barefoot and mad as a dog.', [
   ['', 'Злий — це добре. Злість теж зброя.', 'Mad is good. Rage is a weapon too.'],
   ['', 'Голий, босий, але живий. Нуль — не так і мало.', 'Naked, barefoot, but alive. Zero isn\'t that little.']]],
  ['', 'памʼятаємо', 'Та не з нуля. Ми ж пам\'ятаємо. Я — то точно.', 'Not from scratch. We remember. I do, for sure.', [
   ['', 'Ти пам\'ятаєш, де їжа. Я — де смерть. Разом упораємось.', 'You remember where the food is. I remember where death is. Together we\'ll manage.'],
   ['', 'Пам\'ятаєш. Дивно, але приємно.', 'You remember. Odd, but nice.']]],
 ]},
]};
// ============================== BIOME (H open, M reply, H? close) ==============================
S.biome = { O: 'H:х_біом', R: 'M:м_біом', C: 'H:х_кінець', items: [
 { p: 'вбити', o: [['', '{biome_here}. Новий світ — нові способи нас убити.', '{biome_here}. A new world, new ways to kill us.']], r: [
  ['', 'пики', 'Нові способи нас вбити? Кайф. Нові пики, шоб клювати.', 'New ways to kill us? Sweet. New mugs to peck.', [
   ['', 'Пики тут клюються у відповідь. Пам\'ятай.', 'The mugs here peck back. Remember that.'],
   ['', 'Клюй. А я рахуватиму, скільки разів ми тікали.', 'Peck away. I\'ll count how many times we ran.']]],
  ['', 'спробують', 'Убити? Нас? Хай спершу спробують, курва.', 'Kill us? Let them fucking try.', [
   ['', 'Спробують. Вони тут саме для цього.', 'They will. That\'s what they\'re here for.'],
   ['', 'Не кажи цього вголос. Почують.', 'Don\'t say it out loud. They\'ll hear.']]],
  ['~1', 'здохнемо', 'Ми тут всі здохнемо. Зате красиво, {м_слівце}.', 'We\'ll all croak here. Pretty, though, {м_слівце}.', [
   ['', 'Здохнемо, то хоч не від нудьги.', 'If we croak, at least not of boredom.'],
   ['', 'Може, й ні. Якщо ти хоч раз помовчиш.', 'Maybe not. If you keep quiet for once.']]],
 ]},
 { p: 'дивись', o: [['', 'Подивись навколо, Муніне. Такого ми ще не бачили.', 'Look around, Munin. We\'ve never seen anything like this.']], r: [
  ['', 'смердить', 'Бачити не бачив, а смердить, як у троля в сраці. Мені подобається.', 'Never seen it, but it stinks like a troll\'s arse. I like it.', [
   ['', 'Тобі подобається все, що смердить. Тому ти й мене не любиш.', 'You like everything that stinks. That\'s why you don\'t like me.'],
   ['', 'Звикнеш. Ти до всього звикаєш.', 'You\'ll get used to it. You get used to everything.']]],
  ['', 'як_усюди', 'Бачу. Дерево, камінь, ше дерево. Як усюди, тіко інше.', 'I see. A tree, a rock, another tree. Like everywhere, only different.', [
   ['', '«Як усюди, тільки інше». Ти щойно описав усі мандри.', '“Like everywhere, only different.” You just described all travel.'],
   ['', 'Ти просто не вмієш дивитись.', 'You just don\'t know how to look.']]],
  ['~3', 'гарно', 'Ну… гарно, {м_слівце}. Тіко нікому, шо я це сказав.', 'Well… pretty, {м_слівце}. Just don\'t tell anyone I said it.', [
   ['', 'Мовчу. Але запам\'ятаю.', 'Silent. But I\'ll remember.'],
   ['', 'Мені подобається. І тобі, бачу, теж.', 'I like it. And so do you, I see.']]],
 ]},
 { p: 'дорога', o: [['', 'Я запам\'ятаю цю дорогу. Ти — ні.', 'I\'ll remember this road. You won\'t.']], r: [
  ['', 'клюнути', 'Ти собі запам\'ятовуй, а я гляну, кого тут першим клюнути.', 'You go ahead and remember, I\'ll see who to peck first.', [
   ['', 'Дивись. Тільки не того, хто клюне сильніше.', 'Look. Just not one who pecks harder.'],
   ['', 'Клюй. А я запам\'ятаю, звідки тебе витягати.', 'Peck. And I\'ll remember where to drag you out from.']]],
  ['', 'мапа', 'А нашо мені? Я з тобою. Ти — моя мапа.', 'What do I need it for? I\'m with you. You\'re my map.', [
   ['', 'Мапа, яка тебе терпить. Цінуй.', 'A map that puts up with you. Appreciate it.'],
   ['', 'Тоді не загуби свою мапу.', 'Then don\'t lose your map.']]],
 ]},
 { p: 'повітря', o: [['~1', 'Тут навіть повітря недружнє.', 'Even the air here is unfriendly.']], r: [
  ['', 'недружній', 'Недружнє — то нормально. Я теж недружній.', 'Unfriendly is fine. I\'m unfriendly too.', [
   ['', 'Тоді ви з ним порозумієтеся.', 'Then you two will get along.'],
   ['', 'Знаю. Тому й сиджу на другому плечі.', 'I know. That\'s why I sit on the other shoulder.']]],
  ['', 'нюхаєш', 'Повітря як повітря. Ти просто нюхаєш забагато, ботан.', 'Air is air. You just sniff too much, wiseguy.', [
   ['', 'Нюхаю. Тому й досі живий.', 'I sniff. That\'s why I\'m still alive.'],
   ['', 'Хтось мусить нюхати. Ти лише жереш.', 'Someone has to sniff. You only eat.']]],
 ]},
 { p: 'страшно', o: [['~3', 'Гарно. Страшно, але гарно.', 'Beautiful. Scary, but beautiful.']], r: [
  ['', 'я_тут', 'Страшно? Я тут, ботан. Хай тіко шось ворухнеться.', 'Scary? I\'m here, wiseguy. Just let something move.', [
   ['', 'Ворухнеться — ти сховаєшся першим. Але дякую.', 'If it moves, you hide first. But thanks.'],
   ['', 'Добре, що ти тут. Мовчи, що я це сказав.', 'Good that you\'re here. Don\'t repeat that.']]],
  ['', 'гарно2', 'Гарне, бляха. Тіко нікому не кажи, шо я сказав.', 'Pretty, damn it. Just don\'t tell anyone I said so.', [
   ['', 'Не скажу. Мені однаково ніхто не повірить.', 'I won\'t. Nobody would believe me anyway.'],
   ['', 'Мовчу. Хоч страшно — то вже не мені одному.', 'Silent. At least I\'m not the only one scared now.']]],
 ]},
 { p: 'звірі', o: [['', 'Цікаво, які тут звірі. І чи вони нас їдять.', 'I wonder what beasts live here. And whether they eat us.']], r: [
  ['', 'спершуя', 'Жеруть, ага. Але спершу я їх.', 'They eat, sure. But I eat them first.', [
   ['', 'Спершу ти їх, потім вони тебе. Кругообіг.', 'First you them, then they you. The circle of life.'],
   ['', 'Дивись, щоб ми самі не стали тут новою їжею.', 'Watch out that we don\'t become the new food here.']]],
  ['', 'вкурсі', 'Тутешні ше не в курсі, хто прийшов, {м_слівце}. Зара будуть.', 'The locals don\'t know who\'s arrived yet, {м_слівце}. They will.', [
   ['', 'Будуть. Ти кричиш так, що чути аж у сусідньому краю.', 'They will. You yell so loud the next land can hear.'],
   ['', 'Хай краще не знають. Нам так спокійніше.', 'Better they don\'t. We\'re calmer that way.']]],
 ]},
 { p: 'мапа', o: [['', 'Треба запам\'ятати, звідки ми прийшли. Раптом тікати.', 'We should remember where we came from. In case we need to run.']], r: [
  ['', 'відступаю', 'Тікати? Я не тікаю. Я красиво відступаю. Шариш?', 'Run? I don\'t run. I retreat beautifully. Get it?', [
   ['', 'Шарю. Красиво відступати теж треба вміти.', 'I get it. Retreating gracefully is a skill too.'],
   ['', 'Шарю. Твій відступ видно здалеку. По хвосту.', 'I get it. Your retreat is visible from afar. By the tail.']]],
  ['', 'смачний', 'Ти малюй свою мапу. А я гляну, хто тут найсмачніший.', 'You draw your map. I\'ll see who\'s the tastiest here.', [
   ['', 'Найсмачніший тут, підозрюю, ти.', 'The tastiest here, I suspect, is you.'],
   ['', 'Глянь. Тільки не куштуй усе підряд.', 'Look. Just don\'t taste everything.']]],
 ]},
 { p: 'небо', o: [['', 'Навіть небо тут іншого кольору.', 'Even the sky is a different colour here.']], r: [
  ['', 'гівняний', 'Колір як колір. Гівняний якийсь.', 'A colour like any colour. Shitty, kind of.', [
   ['', 'Мені подобається. Навіть якщо тобі — ні.', 'I like it. Even if you don\'t.'],
   ['', 'Для тебе все погане, крім їжі.', 'For you everything\'s shitty except food.']]],
  ['', 'синє', 'Іншого? Та воно ж синє, як і було. Ти шо, сліпий?', 'Different? It\'s blue, same as ever. You blind or what?', [
   ['', 'Синє. Але інше синє. Тобі не пояснити.', 'Blue. But a different blue. Can\'t explain it to you.'],
   ['', 'Сліпий тут один. І це не я.', 'There\'s one blind one here. And it\'s not me.']]],
 ]},
 { p: 'рослини', o: [['', 'Дивні рослини. Не їж нічого, Муніне.', 'Strange plants. Don\'t eat anything, Munin.']], r: [
  ['', 'зжер', 'Пізно, я вже зжер. Нічо, живий. Поки шо.', 'Too late, I already ate some. Fine, I\'m alive. For now.', [
   ['', 'Побачимо, що з тобою буде до ранку.', 'We\'ll see what becomes of you by morning.'],
   ['', 'Поки що. Скажеш, коли почнеш світитися.', 'For now. Tell me when you start glowing.']]],
  ['', 'нежеру', 'Та не жеру, не жеру. Поки шо.', 'I\'m not eating, I\'m not. For now.', [
   ['', '«Поки що» я чув. Стежу за тобою.', 'I heard that “for now”. I\'m watching you.'],
   ['', 'Тримаю тебе за слово. І за хвіст.', 'I\'m holding you to your word. And your tail.']]],
 ]},
 { p: 'перші', o: [['', 'Ми тут перші з воронів. Мабуть.', 'We\'re the first ravens here. Probably.']], r: [
  ['', 'наше', 'Перші — значить, усе наше. Все, чуєш?', 'First means it\'s all ours. All of it, hear?', [
   ['', 'Чую. Наше, поки нас не з\'їли.', 'I hear. Ours, until we get eaten.'],
   ['', 'Наше. Тільки не міть територію, прошу.', 'Ours. Just don\'t mark the territory, please.']]],
  ['', 'дурні', 'Перші, бо інші не такі дурні, як ми.', 'First, because the others aren\'t as stupid as us.', [
   ['', 'Дурні, зате перші. Хтось мусить.', 'Stupid, but first. Somebody has to be.'],
   ['', 'Говори за себе. Я тут, бо {name} сюди піш{ов|ла}.', 'Speak for yourself. I\'m here because {name} went here.']]],
 ]},
 { p: 'тиша', o: [['', 'Тут тихо по-іншому. Насторожено.', 'It\'s quiet here in a different way. Wary.']], r: [
  ['', 'знають', 'Насторожено, бо знають, шо я прийшов. І правильно.', 'Wary because they know I\'ve arrived. And rightly so.', [
   ['', 'Знають. Тепер помовч, щоб і я щось почув.', 'They know. Now shut up so I can hear something too.'],
   ['', 'Бояться, ага. Сміються, мабуть, у кущах.', 'Scared, sure. Laughing in the bushes, more likely.']]],
  ['', 'буря', 'Тиша, бля. Перед бурею, по-любому.', 'Quiet, fuck. Before a storm, for sure.', [
   ['', 'Слухаймо уважно. Буря має голос.', 'Let\'s listen closely. A storm has a voice.'],
   ['', 'Не накаркай. Ти це вмієш.', 'Don\'t jinx it. You\'re good at that.']]],
 ]},
 { p: 'дім', o: [['', 'Дім тепер далеко. Чуєш, як далеко?', 'Home is far away now. Feel how far?']], r: [
  ['', 'нудно', 'Чую. І заїбісь. Там нудно було.', 'I feel it. And fucking good. It was boring there.', [
   ['', 'Нудно, зате безпечно. Ще згадаєш.', 'Boring, but safe. You\'ll miss it.'],
   ['', 'Тут нудно не буде. Обіцяю.', 'It won\'t be boring here. I promise.']]],
  ['', 'жерти', 'Дім, дім… Там хоч жерти було шо.', 'Home, home… At least there was grub there.', [
   ['', 'Знайдемо й тут. Ти їжу і в камені знайдеш.', 'We\'ll find some here. You\'d find food in a rock.'],
   ['', 'Повернемося. Колись. Тоді й наїсися.', 'We\'ll be back. Someday. Then you\'ll eat your fill.']]],
 ]},
 { p: 'скарби', o: [['', 'Тут має бути щось цінне. Нові землі — нові скарби.', 'There should be something valuable here. New lands, new treasures.']], r: [
  ['', 'мої', 'Скарби — мої. Хто перший урвав — той і бере.', 'The treasures are mine. Whoever grabs first, takes.', [
   ['', 'Спершу виживемо. Потім скарби.', 'First we survive. Then treasures.'],
   ['', 'Урвеш ти, а понесе {name}. Тобі лишиться дивитись.', 'You grab, {name} carries. You get to watch.']]],
  ['', 'показуй', 'Цінне? Де?! Де, курва? Показуй!', 'Valuable? Where?! Where, damn it? Show me!', [
   ['', 'Ще ніде. Я сказав «має бути».', 'Nowhere yet. I said “should be”.'],
   ['', 'Шукаємо. Без твого крику знайдемо швидше.', 'We\'re looking. Without your yelling we\'d find it faster.']]],
 ]},
]};
// ============================== WALK (M open, H reply, M? close) ==============================
S.walk = { O: 'M:м_дорога', R: 'H:х_дорога', C: 'M:м_кінець', items: [
 { p: 'далеко', o: [['', 'Куди ми, {м_слівце}, вже пів дня премось?', 'Where the {м_слівце} have we been trudging for half a day?']], r: [
  ['', 'мандри', 'Туди, де ще не були. Це і є мандри.', 'Where we haven\'t been yet. That\'s what travel is.', [
   ['', 'Мандри, бля. Скажи ше «пригоди». Тьху.', 'Travel, fuck. Say “adventure” next. Pfft.'],
   ['', 'Ага-ага. Аби не по колу, як минулого разу.', 'Uh-huh. As long as it\'s not in circles like last time.']]],
  ['', 'карта', 'Карту {name} тримає в голові. Сподіваюсь.', '{name} keeps the map in {his|her} head. I hope.', [
   ['', 'Сподіваєшся. Ну все, ми заблукали.', 'You hope. That\'s it, we\'re lost.'],
   ['', 'В голові? Там же вітер свище, я чув.', 'In the head? The wind whistles in there, I\'ve heard.']]],
  ['~1', 'неный', 'Пів дня — це ще мало. Не ний.', 'Half a day is nothing yet. Don\'t whine.', [
   ['', 'Я не скиглю. Я, бля, коментую. Різниця.', 'I\'m not whining. I\'m fucking commenting. There\'s a difference.'],
   ['', 'Мало? То понеси мене сам, раз такий бадьорий.', 'Nothing? Then carry me yourself, if you\'re so perky.']]],
 ]},
 { p: 'гордо', o: [['~3', 'Далеко зайшли, {м_слівце}. Аж гордо якось, прикинь.', 'We\'ve come far, {м_слівце}. Kind of proud, imagine that.']], r: [
  ['', 'розчулився', 'Гордість? Не кажи, що ти розчулився.', 'Pride? Don\'t tell me you\'ve gone soft.', [
   ['', 'Не розчулився. Просто… далеко. Відчепись.', 'Not soft. Just… far. Back off.'],
   ['', 'Розчулився, ага. Від тряски.', 'Gone soft, yeah. From the jolting.']]],
  ['', 'нуль', 'Прикинув. Ти пройшов рівно нуль кроків.', 'Imagined it. You walked exactly zero steps.', [
   ['', 'Нуль кроків, зате скільки тряски.', 'Zero steps, but how much jolting.'],
   ['', 'Я не ногами. Я душею йшов.', 'Not with my feet. I walked with my soul.']]],
 ]},
 { p: 'ноги', o: [['', 'Добре, шо я на плечі. Пішки я б уже давно здох.', 'Good thing I\'m on the shoulder. On foot I\'d have croaked long ago.']], r: [
  ['', 'нахлібник', 'Ти навіть крилами не махаєш. Нахлібник.', 'You don\'t even flap your wings. Freeloader.', [
   ['', 'Я сили бережу. Для бійки. Отак.', 'I\'m saving my strength. For a fight. So there.'],
   ['', 'Нахлібник — то посада. Я її чесно заслужив.', 'Freeloader is a job title. I earned it fair.']]],
  ['', 'затрьох', 'Зате {name} іде за трьох.', 'But {name} walks for three.', [
   ['', 'Ну й нехай. В {нього|неї} ноги довгі, а в мене кривенькі.', 'Let {him|her}. {His|Her} legs are long, mine are crooked.'],
   ['', 'За трьох іде — за трьох і годує. Особливо мене.', 'Walks for three, feeds three. Me especially.']]],
 ]},
 { p: 'трясе', o: [['', 'Трясе, як на возі з камінням. Я вже пів пір\'я розгубив, бля.', 'Jolting like a cart full of rocks. I\'ve lost half my feathers, fuck.']], r: [
  ['', 'розум', 'Пір\'їни відростуть. Розум — ні.', 'Feathers grow back. Brains don\'t.', [
   ['', 'То в мене його й не було. Нема чого губити.', 'Never had any. Nothing to lose.'],
   ['', 'Розум не гріє. Пір\'я гріє. От і вибирай.', 'Brains don\'t keep you warm. Feathers do. So choose.']]],
  ['', 'злети', 'Злети й політай. Трясти перестане.', 'Take off and fly. The jolting will stop.', [
   ['', 'Злетіти? Шоб ти тут сам розумничав? Ше чого.', 'Fly off? So you can play the smartass alone here? Like hell.'],
   ['', 'Полечу — і ти без мене скучатимеш. Сиджу.', 'If I fly, you\'ll miss me. I\'m staying.']]],
 ]},
 { p: 'пейзаж', o: [['', 'Дерево, камінь, дерево. Ше дерево. Охуєнні види.', 'Tree, rock, tree. Another tree. Fucking amazing views.']], r: [
  ['', 'дивитись', 'Ти просто не вмієш дивитись.', 'You just don\'t know how to look.', [
   ['', 'Вмію. Дивлюсь — дерево. Он, знову дерево.', 'I do. I look: a tree. There, another tree.'],
   ['', 'Дивлюсь. Бачу тебе. Краще б не вмів.', 'I look. I see you. I\'d rather not know how.']]],
  ['', 'камінь', 'Он той камінь — точнісінько твоя голова.', 'That rock over there is your head exactly.', [
   ['', 'Сам ти камінь. Тупий і сірий.', 'You\'re the rock. Dumb and grey.'],
   ['', 'Гарний камінь. Здоровий. Значить, і голова гарна.', 'Nice rock. Big. So my head\'s nice too.']]],
  ['~3', 'уважніше', 'Подивись уважніше. Гарно ж.', 'Look closer. It\'s beautiful.', [
   ['', 'Подивився. Гарно, ладно. Відчепись.', 'Looked. Beautiful, fine. Back off.'],
   ['', 'Ну… гарно. Але дерево.', 'Well… beautiful. But a tree.']]],
 ]},
 { p: 'голод', o: [['', 'Йдемо, а я голодний. Хтось про мене подумав, {м_слівце}? Нє?', 'We walk, and I\'m hungry. Anyone think of me, {м_слівце}? No?']], r: [
  ['', 'схуднути', 'Подумали. Вирішили, що схуднути тобі не завадить.', 'We did. Decided losing weight wouldn\'t hurt you.', [
   ['', 'Схуднути? Я пір\'я і кістки! Шо там худнути, бля.', 'Lose weight? I\'m feathers and bones! What\'s to lose, fuck.'],
   ['', 'Я схудну — ти перший заплачеш. Не буде кого дражнити.', 'If I get thin, you\'ll cry first. Nobody to tease.']]],
  ['', 'зловимо', 'Зловимо щось дорогою. Якщо не розлякаєш лайкою.', 'We\'ll catch something on the way. If you don\'t scare it off with your swearing.', [
   ['', 'Не розлякаю. Я пошепки матюкатимусь.', 'I won\'t. I\'ll swear in a whisper.'],
   ['', 'Ловіть швидше. Бо я почну дзьобати плече.', 'Catch it faster. Or I start pecking the shoulder.']]],
 ]},
 { p: 'погода', o: [['', 'Йдем, йдем. Хоч би дощ пішов, чи шо. Для розваги.', 'Walking, walking. If only it would rain or something. For fun.']], r: [
  ['', 'накликай', 'Не накликай, Муніне.', 'Don\'t jinx it, Munin.', [
   ['', 'Накличу, якшо захочу. І шо ти мені зробиш.', 'I\'ll jinx it if I want. And what\'ll you do about it.'],
   ['', 'Та жартую я. Сухо — то й добре.', 'I\'m joking. Dry is good.']]],
  ['', 'скиглити', 'Ти ж потім першим скиглитимеш.', 'You\'ll be the first to whine afterwards.', [
   ['', 'Не скиглитиму. Коментуватиму. Голосно.', 'I won\'t whine. I\'ll comment. Loudly.'],
   ['', 'Скиглитиму, ага. Зате весело буде.', 'I\'ll whine, sure. But it\'ll be fun.']]],
 ]},
 { p: 'звір', o: [['', 'Чую звіра. Мо\' завалимо по дорозі, га?', 'I smell a beast. Maybe we take it down on the way, huh?']], r: [
  ['', 'дійти', 'Ми йдемо не полювати, а дійти.', 'We\'re not here to hunt. We\'re here to get there.', [
   ['', 'Дійти, дійти. Нудний ти, як каша.', 'Get there, get there. You\'re as boring as porridge.'],
   ['', 'Шкода. Пахло смачно.', 'Pity. It smelled tasty.']]],
  ['', 'тхне', 'Звір чує тебе ще здалеку. Від тебе тхне бійкою.', 'The beast can smell you from far off. You reek of a fight.', [
   ['', 'Тхне бійкою — то мій парфум.', 'Reeking of a fight is my perfume.'],
   ['', 'Хай чує. Хай знає, хто йде.', 'Let it smell. Let it know who\'s coming.']]],
 ]},
 { p: 'куди', o: [['', 'Хугіне, ми взагалі знаємо, куди премось? Чи {name} просто так шляється?', 'Hugin, do we even know where we\'re going? Or is {name} just wandering?']], r: [
  ['', 'мабуть', '{name} знає. Мабуть.', '{name} knows. Probably.', [
   ['', '«Мабуть». Чудово. Ми заблукали.', '“Probably.” Great. We\'re lost.'],
   ['', 'Мабуть — то значить ні, ботан.', 'Probably means no, wiseguy.']]],
  ['', 'летять', 'Ворони не питають куди. Ворони летять.', 'Ravens don\'t ask where. Ravens fly.', [
   ['', 'Нормальні ворони летять. А ми трусимось на плечі, як мішки.', 'Normal ravens fly. We bounce on a shoulder like sacks.'],
   ['', 'Летять. А я вже тиждень не літав.', 'Fly. And I haven\'t flown in a week.']]],
 ]},
 { p: 'стомився', o: [['~3', 'Ох, далеко. Мені вже навіть матюкатись ліньки. Уяви.', 'Oh, it\'s far. I\'m too lazy even to swear. Imagine.']], r: [
  ['', 'найкраще', 'Це найкраще, що ти сьогодні сказав.', 'That\'s the best thing you\'ve said today.', [
   ['', 'Ой, не радій. Відпочину — надолужу.', 'Don\'t celebrate. Once I rest, I\'ll catch up.'],
   ['', 'Найкраще, бо коротке. Запам\'ятай.', 'The best, because it was short. Remember it.']]],
  ['', 'разом', 'Тоді помовчимо разом.', 'Then let\'s be quiet together.', [
   ['', 'Ну… добре, помовчимо. Хвилинку. Одну.', 'Well… fine, we\'ll be quiet. For a minute. One.'],
   ['', 'Разом. Тіко ти перший не витримаєш.', 'Together. Only you\'ll crack first.']]],
 ]},
 { p: 'пісня', o: [['', 'Хугіне, заспівай шось. Тіко не своє, а нормальне. Про бійку.', 'Hugin, sing something. Not yours, a proper one. About a fight.']], r: [
  ['', 'двадцять', 'Нормальних пісень ти не знаєш. Ти знаєш лише ту, де «курва» двадцять разів.', 'You don\'t know any proper songs. You only know the one with “bitch” twenty times.', [
   ['', 'Двадцять один, ботан. Рахувати навчись спершу.', 'Twenty-one, wiseguy. Learn to count first.'],
   ['', 'Бо то найкраща пісня. Душевна.', 'Because it\'s the best song. Heartfelt.']]],
  ['', 'ворон', 'Пісню? Гаразд: ішов ворон, ішов… Ні, ворони не ходять.', 'A song? Fine: a raven was walking, walking… No, ravens don\'t walk.', [
   ['', 'Отак завжди. Почав — і обламав.', 'Always the same. Starts, then ruins it.'],
   ['', 'Ходять. Я ходив. Раз. До миски.', 'They do. I walked. Once. To the bowl.']]],
 ]},
 { p: 'сліди', o: [['', 'Глянь, сліди. Хтось тут ішов до нас. Або тікав. Від мене, мо\'.', 'Look, tracks. Someone came this way. Or ran. From me, maybe.']], r: [
  ['', 'втікав', 'Втікав. Від тебе, мабуть.', 'Ran. From you, probably.', [
   ['', 'От бачиш. Мене всі бояться.', 'See. Everybody\'s scared of me.'],
   ['', 'Правильно робив. Я зара злий.', 'Smart move. I\'m in a mood right now.']]],
  ['', 'свіжі', 'Свіжі. Тримай очі відкритими.', 'Fresh. Keep your eyes open.', [
   ['', 'Відкриті, а як же. Я завжди на шухері.', 'Open, what else. I\'m always on the lookout.'],
   ['', 'Свіжі — значить, ми близько. Кайф.', 'Fresh means we\'re close. Sweet.']]],
 ]},
]};
// ============================== SAIL (H open, M reply, H? close) ==============================
S.sail = { O: 'H:х_море', R: 'M:м_море', C: 'H:х_кінець', items: [
 { p: 'спокій', o: [['', 'Море спокійне. Підозріло спокійне.', 'The sea is calm. Suspiciously calm.']], r: [
  ['', 'хвиля', 'Підозріло? Та тут кожна хвиля нас зжерти хоче, {м_слівце}, а ти — підозріло.', 'Suspicious? Every wave here wants to eat us, {м_слівце}, and you say suspicious.', [
   ['', 'Хвилі тебе не хочуть. Ти гіркий.', 'The waves don\'t want you. You taste bitter.'],
   ['', 'Тоді тримайся. І не гойдай корабель криком.', 'Then hold on. And don\'t rock the ship with your yelling.']]],
  ['', 'прокинулось', 'Спокійне, бо ше не прокинулось. Почекай трохи.', 'Calm because it hasn\'t woken up yet. Wait a bit.', [
   ['', 'Тоді не буди його своїм каркотінням.', 'Then don\'t wake it with your cawing.'],
   ['', 'Тоді шепочи. Хай поспить.', 'Then whisper. Let it sleep.']]],
  ['~1', 'цвинтар', 'Тихо, як на цвинтарі. Мені якраз.', 'Quiet as a graveyard. Just my style.', [
   ['', 'Цвинтар у морі зветься дном. Не поспішай туди.', 'At sea the graveyard is called the bottom. Don\'t hurry there.'],
   ['', 'Тобі якраз. Бо там ніхто не відповідає.', 'Your style. Because nobody talks back there.']]],
 ]},
 { p: 'обрій', o: [['', 'Глянь, Муніне, обрій. Там щось є.', 'Look, Munin, the horizon. There\'s something there.']], r: [
  ['', 'змій', 'Обрій, {м_слівце}. Там завжди шось є. Зазвичай змій.', 'The horizon, {м_слівце}. There\'s always something there. Usually a serpent.', [
   ['', 'Якщо змій — ти перший у пащу.', 'If it\'s a serpent, you go in its jaws first.'],
   ['', 'Тоді не кричи. Змії чують краще, ніж бачать.', 'Then don\'t yell. Serpents hear better than they see.']]],
  ['', 'вода', 'Там ніхуя нема, крім води. Вода, вода, вода. Задовбало.', 'There\'s fuck-all there but water. Water, water, water. Sick of it.', [
   ['', 'Є. Ти просто дивишся на хвилю під носом.', 'There is. You\'re just looking at the wave under your beak.'],
   ['', 'Вода — то дорога. Наберися терпцю.', 'The water is the road. Be patient.']]],
  ['~3', 'дім', 'Там дім, ботан. Колись вернемось. Всі троє.', 'Home is there, wiseguy. Someday we\'ll go back. All three of us.', [
   ['', 'Дім. Так. Колись.', 'Home. Yes. Someday.'],
   ['', 'Всі троє. Ловлю на слові.', 'All three. I\'ll hold you to it.']]],
 ]},
 { p: 'ширше', o: [['~3', 'Знаєш, на морі думається інакше. Ширше.', 'You know, at sea you think differently. Wider.']], r: [
  ['', 'вітрило', 'Ширше? В тебе й так голова, як вітрило. Надута.', 'Wider? Your head\'s already like a sail. Puffed up.', [
   ['', 'Зате тягне нас уперед. На відміну від тебе.', 'But it pulls us forward. Unlike you.'],
   ['', 'Надута, зате суха. А твоя — ні.', 'Puffed up, but dry. Yours isn\'t.']]],
  ['', 'нудить', 'А мені тіко блювати думається. Широко.', 'All I think about is puking. Widely.', [
   ['', 'Тільки не на мене, Муніне.', 'Just not on me, Munin.'],
   ['', 'Блюй за борт. З вітром, не проти.', 'Puke overboard. With the wind, not against it.']]],
 ]},
 { p: 'хитає', o: [['', 'Не дивись униз, Муніне. Хитає.', 'Don\'t look down, Munin. It\'s rocking.'], ['~1', 'Тримайся, Муніне. Ти вже зелений, як водорість.', 'Hold on, Munin. You\'re green as seaweed already.']], r: [
  ['', 'нудить', 'Мене не хитає. Мене блювати тягне. Різниця.', 'I\'m not rocking. I\'m about to puke. There\'s a difference.', [
   ['', 'Тягне — то тягнись за борт. Не на мене.', 'If you\'re about to, lean overboard. Not on me.'],
   ['', 'Різниця є. Хитає тебе, а відмиватимусь я.', 'There is a difference. You\'re the one rocking, I\'m the one who\'ll be washing.']]],
  ['', 'хвіст', 'Ше раз хитне — наригаю тобі на хвіст. Сто пудів.', 'One more roll and I puke on your tail. Guaranteed.', [
   ['', 'Тримайся за щоглу. І за язик.', 'Hold on to the mast. And to your tongue.'],
   ['', 'Спробуй. Далі попливеш сам.', 'Try it. You\'ll swim the rest yourself.']]],
  ['~3', 'танцюю', 'Не хитає мене. Це я так танцюю, шо.', 'I\'m not rocking. This is how I dance.', [
   ['', 'Танцюй. Тільки не на мене.', 'Dance. Just not onto me.'],
   ['', 'Гарно танцюєш. Особливо зеленим кольором.', 'Nice dancing. The green especially.']]],
 ]},
 { p: 'борт', o: [['~1', 'Якщо впадеш за борт, я не пірнатиму.', 'If you fall overboard, I\'m not diving.']], r: [
  ['', 'дно', 'Я? За борт? Це ти в нас від мудрості важкий. Ти й підеш на дно.', 'Me? Overboard? You\'re the one heavy with wisdom. You\'ll go to the bottom.', [
   ['', 'Мудрість не тоне. Вона спливає.', 'Wisdom doesn\'t sink. It floats.'],
   ['', 'На дно піде той, хто більше жере. Подумай.', 'The one who eats more goes to the bottom. Think about it.']]],
  ['', 'скину', 'Не пірнатимеш? Та я тебе сам туди скину, ботан. З розгону.', 'You won\'t dive? I\'ll throw you in myself, wiseguy. With a run-up.', [
   ['', 'Скинеш — і пливтимеш далі без мене і без дороги.', 'Throw me in, and you sail on without me and without a way.'],
   ['', 'Розгону в тебе нема. Тільки язик.', 'You don\'t have a run-up. Just a tongue.']]],
 ]},
 { p: 'люблю', o: [['~3', 'Люблю море. Тут ніхто не кричить, крім тебе.', 'I love the sea. Nobody shouts here except you.']], r: [
  ['', 'кричу', 'Я кричу, бо мене хитає, {м_слівце}. Ясно тобі?', 'I shout because I\'m seasick, {м_слівце}. Got it?', [
   ['', 'Ясно. Кричи. Море стерпить.', 'Got it. Shout. The sea can take it.'],
   ['', 'Ясно. Тоді кричи за борт.', 'Got it. Then shout overboard.']]],
  ['', 'ітебе', 'Кричу, бо люблю, ботан. Море люблю. Не тебе. Ну, й тебе.', 'I shout because I love it, wiseguy. The sea. Not you. Well, you too.', [
   ['', 'Знаю. Кричи. Море стерпить.', 'I know. Shout. The sea can take it.'],
   ['', '«Ну, й тебе». Вирізьблю це на щоглі.', '“Well, you too.” I\'ll carve that on the mast.']]],
 ]},
 { p: 'вітер', o: [['', 'Вітер у спину. Хоч щось сьогодні на нашому боці.', 'Wind at our backs. At least something\'s on our side today.']], r: [
  ['', 'перднув', 'Вітер у спину? То ти, професоре, перднув?', 'Wind at our backs? Was that you farting, professor?', [
   ['', 'Ні. Але тепер, коли ти спитав, підозрюю тебе.', 'No. But now that you ask, I suspect you.'],
   ['', 'Якби я, ми б уже були на березі.', 'If it were me, we\'d be ashore already.']]],
  ['', 'впику', 'Хай дме. Аби не в пику, {м_слівце}.', 'Let it blow. As long as it\'s not in my face, {м_слівце}.', [
   ['', 'Тримайся, бо здме.', 'Hold on, or you\'ll be blown away.'],
   ['', 'У пику тобі дме тільки правда. Від мене.', 'The only thing that blows in your face is the truth. From me.']]],
 ]},
 { p: 'риба', o: [['', 'Під нами, мабуть, ціла зграя риби.', 'There\'s probably a whole school of fish under us.']], r: [
  ['', 'голодний', 'Риба — то жрачка. Ловіть, {м_слівце}, бо я голодний.', 'Fish is grub. Catch it, {м_слівце}, I\'m hungry.', [
   ['', 'Ти завжди голодний.', 'You\'re always hungry.'],
   ['', 'Лови сам. Пірнеш — і наїсися.', 'Catch it yourself. Dive in and eat.']]],
  ['', 'потім', 'Хай пливе. Я її потім гляну, як зголоднію.', 'Let it swim. I\'ll check it later when I\'m hungry.', [
   ['', '«Потім» у тебе — це за мить. Знаю тебе.', 'Your “later” is a moment from now. I know you.'],
   ['', 'Вона тебе теж гляне. Знизу.', 'It\'ll check you out too. From below.']]],
 ]},
 { p: 'змій', o: [['', 'Якщо зараз вилізе змій, я голосуватиму за втечу.', 'If a serpent comes up now, I vote for running.']], r: [
  ['', 'бійка', 'А я за бійку. В нього луска смачна, {м_слівце}, я чув.', 'And I vote for a fight. Its scales are tasty, {м_слівце}, I heard.', [
   ['', 'Тоді битимешся сам.', 'Then you\'ll fight alone.'],
   ['', 'Чув ти це від кухаря. Не від тих, хто бився.', 'You heard it from a cook. Not from those who fought.']]],
  ['', 'вплав', 'Втекти? З корабля? Вплав, чи шо? Ти дурний?', 'Run? From a ship? Swim for it, or what? You stupid?', [
   ['', 'Вплав. Ти ж качка, сам казав.', 'Swim for it. You\'re a duck, you said so yourself.'],
   ['', 'Кораблем, дурню. Він пливе швидше за змія. Іноді.', 'By ship, idiot. It sails faster than a serpent. Sometimes.']]],
 ]},
 { p: 'буря', o: [['', 'Хмари збираються. Якщо буде буря — тримайся за мене.', 'Clouds are gathering. If there\'s a storm, hold on to me.']], r: [
  ['', 'здує', 'Триматись за тебе? Та тебе першого здує, дохляк.', 'Hold on to you? You\'ll be the first one blown off, skinny.', [
   ['', 'Здує — тоді полетимо разом. Тримайся міцніше.', 'If I go, we fly together. Hold on tighter.'],
   ['', 'Дохляк, зате з кігтями. Тримайся.', 'Skinny, but with claws. Hold on.']]],
  ['', 'кайф', 'Буря — то кайф! Хвилі до неба, {м_слівце}! Йо-хо!', 'A storm is a blast! Waves up to the sky, {м_слівце}! Yo-ho!', [
   ['', 'Весело буде, коли ти змокнеш.', 'It\'ll be fun when you get soaked.'],
   ['', 'Кайф, поки не перевернемося.', 'A blast, until we capsize.']]],
 ]},
 { p: 'берег', o: [['', 'Бачиш берег? Я — ні. А я ж бачу все.', 'See the shore? I don\'t. And I see everything.']], r: [
  ['', 'незжерли', 'Берег там, де нас ше не зжерли.', 'The shore is wherever we haven\'t been eaten yet.', [
   ['', 'Значить, пливемо далі.', 'Then we sail on.'],
   ['', 'Тоді берег поки що під нами.', 'Then for now the shore is right under us.']]],
  ['', 'нема', 'Нема берега. Є вода і твоя пика. Обидва — гівно.', 'There\'s no shore. There\'s water and your mug. Both shit.', [
   ['', 'Є берег. Просто ти дивишся на мене.', 'There is a shore. You\'re just looking at me.'],
   ['', 'Буде берег. А ти до нього дотерпи.', 'There\'ll be a shore. You just hold out till then.']]],
 ]},
 { p: 'чайки', o: [['', 'Бачиш чайок, Муніне? Значить, берег близько.', 'See the gulls, Munin? That means the shore is close.']], r: [
  ['', 'щури', 'Чайки — то щури з крилами. Ненавиджу, {м_слівце}.', 'Gulls are rats with wings. I hate them, {м_слівце}.', [
   ['', 'Летючі щури хоча б знають дорогу.', 'At least flying rats know the way.'],
   ['', 'Вони тебе теж. Взаємно.', 'They hate you too. Mutual.']]],
  ['', 'заблукали', 'Або теж заблукали. Як ми, лохи.', 'Or they\'re lost too. Like us, suckers.', [
   ['', 'Ми не заблукали. Ми шукаємо.', 'We\'re not lost. We\'re searching.'],
   ['', 'Хай так. Заблукаємо разом із чайками.', 'Fine. We\'ll get lost along with the gulls.']]],
 ]},
]};
// ============================== CROSS_H (H open, M reply, H? close) ==============================
S.cross_h = { O: 'H:х_перехід', R: 'M:м_перехід_відповідь', C: 'H:х_кінець', items: [
 { p: 'запах', o: [['', 'Земля змінилась. Чуєш, як інакше пахне?', 'The land has changed. Smell how different it is?']], r: [
  ['', 'тобою', 'Пахне так само. Тобою. Фу.', 'Smells the same. Of you. Ugh.', [
   ['', 'Я пахну розумом. Тобі цей запах незнайомий.', 'I smell of brains. A smell you wouldn\'t know.'],
   ['', 'Звикнеш. Ти ж і до себе звик.', 'You\'ll get used to it. You got used to yourself.']]],
  ['', 'смердить', 'Інакше? Тут смердить, {м_слівце}, ото й уся різниця.', 'Different? It stinks here, {м_слівце}, that\'s the whole difference.', [
   ['', 'Смердить — значить, тут хтось живе. Обережно.', 'It stinks, so something lives here. Careful.'],
   ['', 'Смердить по-новому. Це теж новина.', 'It stinks in a new way. That\'s news too.']]],
 ]},
 { p: 'тихо', o: [['', 'Знаєш, {from_in} було тихіше.', 'You know, {from_in} it was quieter.']], r: [
  ['', 'здохло', 'Тихіше, бо там усе вже здохло. Логічно.', 'Quieter because everything there croaked already. Logical.', [
   ['', 'Логічно. А тут ще ніщо не здохло. Пам\'ятай.', 'Logical. And here nothing has croaked yet. Remember that.'],
   ['', 'Скоро й тут буде тихо. Після тебе.', 'Soon it\'ll be quiet here too. After you.']]],
  ['', 'бійка', 'Нашо мені тиша? Мені бійка треба.', 'What do I want quiet for? I want a fight.', [
   ['', 'Тут бійка сама тебе знайде.', 'Here the fight will find you itself.'],
   ['', 'Отримаєш. Тільки потім не проси тиші.', 'You\'ll get one. Just don\'t beg for quiet afterwards.']]],
 ]},
 { p: 'пильно', o: [['', '{biome_here}. Тримай очі відкритими, Муніне.', '{biome_here}. Keep your eyes open, Munin.']], r: [
  ['', 'мізки', 'В мене очі завжди відкриті. Не те шо твої мізки.', 'My eyes are always open. Unlike your brain.', [
   ['', 'Очі без мізків — то просто дві дірки.', 'Eyes without a brain are just two holes.'],
   ['', 'Хай так. Аби дивився, а не каркав.', 'So be it. As long as you watch and don\'t croak.']]],
  ['', 'клюнути', 'Відкриті, ага. Шукаю, кого першим клюнути.', 'Open, yeah. Looking for who to peck first.', [
   ['', 'Шукай. Тільки клюй тих, хто менший.', 'Look. Just peck the smaller ones.'],
   ['', 'Клюнеш першим — тікатимеш теж першим.', 'Peck first, and you\'ll run first too.']]],
 ]},
 { p: 'межа', o: [['', 'Ось і межа. Ні стовпа, ні знаку — а все інше.', 'And here\'s the border. No post, no sign, and everything\'s different.']], r: [
  ['', 'тісамі', 'Все інше? Та ті самі дерева, ті самі пики.', 'Everything different? Same trees, same mugs.', [
   ['', 'Пика тут одна та сама — твоя. Решта нова.', 'There\'s one same mug here, yours. The rest are new.'],
   ['', 'Дерева ті самі. А ті, хто під ними, — ні.', 'The trees are the same. Whoever\'s under them isn\'t.']]],
  ['', 'знак', 'Знак — то я. Велике чорне попередження, бля.', 'The sign is me. A big black warning, fuck.', [
   ['', 'Попередження, яке всі ігнорують.', 'A warning everyone ignores.'],
   ['', 'Велике — сумнівно. Чорне — так.', 'Big, doubtful. Black, yes.']]],
 ]},
 { p: 'додому', o: [['~3', 'Звідси вже недалеко до дому. Відчуваю.', 'It\'s not far home from here. I can feel it.']], r: [
  ['', 'тепло', 'Дім, {м_слівце}… Аж в пузі тепло стало. Не ржи.', 'Home, {м_слівце}… My belly\'s gone warm. Don\'t laugh.', [
   ['', 'Не ржу. У мене теж.', 'Not laughing. Mine too.'],
   ['', 'Не ржу. Записую.', 'Not laughing. Taking notes.']]],
  ['', 'пожерти', 'Дім? Ну, хоч пожерти дадуть. Нормально.', 'Home? Well, at least there\'ll be grub. Fine.', [
   ['', 'Дадуть. Якщо ти не з\'їв усе ще минулого разу.', 'There will. If you didn\'t eat it all last time.'],
   ['', 'Дадуть. А ти хоч раз дай усім виспатися.', 'There will. And you let everyone sleep for once.']]],
 ]},
 { p: 'повітря', o: [['', 'Повітря тут інше. Важче. Або це ти на плечі вмостився.', 'The air is different here. Heavier. Or that\'s you settling on the shoulder.']], r: [
  ['', 'легкий', 'Я легкий, як пір\'їна. То твоя мудрість важка, ботан.', 'I\'m light as a feather. It\'s your wisdom that\'s heavy, wiseguy.', [
   ['', 'Легкий, як ковадло.', 'Light as an anvil.'],
   ['', 'Пір\'їна, яка жере за трьох.', 'A feather that eats for three.']]],
  ['', 'небезпечно', 'Важче, бо тут небезпечно. Кайф.', 'Heavier because it\'s dangerous here. Sweet.', [
   ['', 'Тобі кайф, поки не болить.', 'Sweet for you, until it hurts.'],
   ['', 'Тоді не розслабляйся. Цей кайф кусається.', 'Then don\'t relax. This kind of sweet bites.']]],
 ]},
 { p: 'звірина', o: [['', 'Тут інша звірина. Глянь на сліди.', 'The beasts are different here. Look at the tracks.']], r: [
  ['', 'куштував', 'Інша? То й шо. Нова — значить, я її ше не куштував.', 'Different? So what. New means I haven\'t tasted it yet.', [
   ['', 'Дивись, щоб вона не скуштувала тебе першою.', 'Make sure it doesn\'t taste you first.'],
   ['', 'Куштуй обережно. Нове буває отруйним.', 'Taste carefully. New can be poisonous.']]],
  ['', 'здорові', 'Сліди здорові, бля. Оце я розумію — є кого гасити.', 'Big tracks, fuck. Now that\'s what I call something to smash.', [
   ['', 'Здорові сліди — здорові зуби. Не поспішай.', 'Big tracks, big teeth. Don\'t rush.'],
   ['', 'Гаси. Я подивлюся звідси. Здалеку.', 'Smash it. I\'ll watch from here. From afar.']]],
 ]},
 { p: 'барви', o: [['', 'Навіть світло тут інше. Барви не ті.', 'Even the light is different here. The colours are off.']], r: [
  ['', 'сіре', 'Барви-шмарви. Сіре, зелене, гівняне — от і всі барви.', 'Colours, schmolours. Grey, green, shitty, that\'s all the colours.', [
   ['', 'А мені подобається. Тим більше, що тобі — ні.', 'I like it. All the more because you don\'t.'],
   ['', 'У тебе всі барви сірі. Це не світло, це ти.', 'For you all colours are grey. It\'s not the light, it\'s you.']]],
  ['', 'ляпнеш', 'Ну… гарно, шо. Ляпнеш комусь, шо я так сказав, — око виклюю.', 'Well… it\'s pretty. Blab that I said so and I\'ll peck your eye out.', [
   ['', 'Не ляпну. Мені однаково ніхто не повірить.', 'I won\'t blab. Nobody would believe me anyway.'],
   ['', 'Мовчу. Очі мені ще знадобляться.', 'Silent. I still need my eyes.']]],
 ]},
 { p: 'подих', o: [['', 'Вітер тут дме з іншого боку. Як і ти.', 'The wind blows from the other side here. Like you.']], r: [
  ['', 'звідки', 'Я дму, звідки хочу, {м_слівце}. І шо ти мені зробиш?', 'I blow from wherever I want, {м_слівце}. And what\'ll you do about it?', [
   ['', 'Нічого. Почекаю, поки видихнешся.', 'Nothing. I\'ll wait till you run out of breath.'],
   ['', 'Нічого. Вітер сам тебе розверне.', 'Nothing. The wind will turn you round itself.']]],
  ['', 'кент', 'Вітер — то мій кент. Такий самий злий і без мізків.', 'The wind is my buddy. Just as mean and brainless.', [
   ['', 'Тримайся, бо твій кент тебе здме.', 'Hold on, or your buddy will blow you away.'],
   ['', 'Нарешті ти чесно себе описав.', 'Finally you described yourself honestly.']]],
 ]},
 { p: 'бували', o: [['', 'Ми тут бували раніше. Пам\'ятаєш?', 'We\'ve been here before. Remember?']], r: [
  ['', 'зжерли', 'Ага, пам\'ятаю. Тут нас мало не зжерли, курва. Класно було.', 'Yeah, I remember. We nearly got eaten here, fuck. It was great.', [
   ['', 'Цього разу буде краще. Без «класно».', 'This time it\'ll go better. Without the “great”.'],
   ['', 'Класно, бо вижили. Повторювати не будемо.', 'Great because we survived. We won\'t repeat it.']]],
  ['', 'непригадую', 'Бували. Шось нічо хорошого не пригадую. Та й похуй.', 'We have. Can\'t recall anything good. Whatever.', [
   ['', 'Ти пригадуєш тільки їжу. Тут її не було.', 'You only recall food. There wasn\'t any here.'],
   ['', 'Цього разу буде краще.', 'This time it\'ll go better.']]],
 ]},
 { p: 'від', o: [['', '{from_in} було спокійніше. А тут — побачимо.', '{from_in} it was calmer. Here, we\'ll see.']], r: [
  ['', 'мухи', 'Спокійніше? Та там мухи дохли від нудьги, братан.', 'Calmer? Flies were dying of boredom there, bro.', [
   ['', 'Тут мухи не дохнуть. Тут вони кусаються.', 'Flies don\'t die here. Here they bite.'],
   ['', 'Мухи хоч не жалілися. На відміну від тебе.', 'At least the flies didn\'t complain. Unlike you.']]],
  ['', 'завалимо', 'Побачимо, ага. І когось завалимо. Сто пудів.', 'We\'ll see, yeah. And we\'ll kill someone. Guaranteed.', [
   ['', 'Обережно — і разом.', 'Carefully, and together.'],
   ['', 'Завалимо. Тільки не того, хто більший за нас.', 'We will. Just not someone bigger than us.']]],
 ]},
 { p: 'карта', o: [['', 'Запам\'ятай, де межа. Повертатися будемо.', 'Remember where the border is. We\'ll be coming back.']], r: [
  ['', 'носом', 'Та запам\'ятав. Межа — де смердить по-іншому. Шариш?', 'Got it. The border is where it stinks differently. Get it?', [
   ['', 'Шарю. Твій ніс — краща мапа, ніж твоя голова.', 'I get it. Your nose is a better map than your head.'],
   ['', 'Шарю. Тільки ти й сам смердиш. Збиваєш.', 'I get it. Only you stink too. You throw it off.']]],
  ['', 'нажерся', 'Вертатися? Нашо? Я ше не нажерся.', 'Go back? What for? I haven\'t eaten my fill.', [
   ['', 'Повернемося. Рано чи пізно. Ситим чи ні.', 'We\'ll be back. Sooner or later. Full or not.'],
   ['', 'Наїсишся — тоді й підемо. Тобто ніколи.', 'We\'ll go when you\'re full. So, never.']]],
 ]},
 { p: 'пташки', o: [['', 'Птахи тут співають інакше. Прислухайся.', 'The birds sing differently here. Listen.']], r: [
  ['', 'дізнаються', 'Співають, бо не знають, шо я поруч. Зара дізнаються.', 'They sing because they don\'t know I\'m near. They\'ll find out.', [
   ['', 'Не чіпай. Вони хоча б не лаються.', 'Leave them. At least they don\'t swear.'],
   ['', 'Хай співають. Хоч хтось тут радіє.', 'Let them sing. At least somebody here is happy.']]],
  ['', 'пузо', 'Шо там птахи… Я чую тіко, як у мене пузо бурчить.', 'Birds, whatever… All I hear is my belly growling.', [
   ['', 'Твоє пузо співає гірше за них.', 'Your belly sings worse than they do.'],
   ['', 'Пузо — теж пташка. Голодна й голосна.', 'Your belly is a bird too. Hungry and loud.']]],
 ]},
 { p: 'нове', o: [['', '{biome_here}. Нове завжди небезпечне.', '{biome_here}. The new is always dangerous.']], r: [
  ['', 'весело', 'Небезпечне? Йо, то ж весело! Пішли вже, не сци.', 'Dangerous? Yo, that\'s fun! Let\'s go, don\'t piss yourself.', [
   ['', 'Обережно — і разом. Не біжи.', 'Carefully, and together. Don\'t run.'],
   ['', 'Не сцю. Думаю. Тобі таке не знайоме.', 'Not pissing myself. Thinking. Not something you\'d know.']]],
  ['', 'ззаду', 'Ну то ти й лізь спереду, професоре. А я прикрию. Ззаду. Здалеку.', 'Then you go in front, professor. I\'ll cover you. From behind. From afar.', [
   ['', 'Прикриєш. Як завжди — голосом.', 'You\'ll cover me. As always, with your voice.'],
   ['', 'Здалеку прикривати ти вмієш. Майстер.', 'Covering from afar, that you can do. A master.']]],
 ]},
]};
// ============================== CROSS_M (M open, H reply, M? close) ==============================
S.cross_m = { O: 'M:м_перехід', R: 'H:х_перехід_відповідь', C: 'M:м_кінець', items: [
 { p: 'смачний', o: [['', 'О, {biome_here}. Тут по-любому хтось смачний є.', 'Oh, {biome_here}. Someone here is bound to be tasty.']], r: [
  ['', 'ти', 'Смачний тут — ти. Для місцевих.', 'The tasty one here is you. For the locals.', [
   ['', 'Хай спробують. Я в горлі застрягну.', 'Let them try. I\'ll get stuck in their throat.'],
   ['', 'Я гіркий, ботан. Мене виплюнуть.', 'I\'m bitter, wiseguy. They\'ll spit me out.']]],
  ['', 'роздивись', 'Спершу роздивись, а потім клюй.', 'Look around first, then peck.', [
   ['', 'Роздивився. Все смачне.', 'Looked around. Everything\'s tasty.'],
   ['', 'Я спершу клюю, роздивляюсь потім. Так швидше.', 'I peck first, look later. It\'s faster.']]],
 ]},
 { p: 'кордон', o: [['', 'Знов кордон. Ні митниці, ні пива. Шо за країна.', 'A border again. No customs, no beer. What a country.']], r: [
  ['', 'вдома', 'Пиво буде вдома. Якщо дійдемо.', 'There\'ll be beer at home. If we get there.', [
   ['', 'Дійдемо. Я за пиво й пішки дійду.', 'We will. For beer I\'d even walk.'],
   ['', '«Якшо»… Не каркай, бо каркати — то моя робота.', '“If”… Don\'t croak, croaking is my job.']]],
  ['', 'мито', 'Митниця — це я. Мито — твоя мовчанка.', 'Customs is me. The duty is your silence.', [
   ['', 'Тоді я контрабанда. Пропускай так.', 'Then I\'m contraband. Wave me through.'],
   ['', 'Хуй тобі, а не мито. Я плачу лайкою.', 'Fuck your duty. I pay in swears.']]],
 ]},
 { p: 'знову', o: [['', 'Нашо ми знов сюди премось, {м_слівце}? Нашо?', 'Why the {м_слівце} are we dragging ourselves here again? Why?']], r: [
  ['', 'вирішив', 'Бо {name} так виріши{в|ла}. Не сперечайся з господарем.', 'Because {name} decided so. Don\'t argue with the master.', [
   ['', 'Ну й виріши{в|ла}, і шо. Я теж вирішив: не заткнусь.', 'So {he|she} decided. I decided something too: I won\'t shut up.'],
   ['', 'Господар, бля. А мене ніхто не питав.', 'Master, fuck. Nobody asked me.']]],
  ['', 'літають', 'Знову. Ворони не питають дороги — ворони літають.', 'Again. Ravens don\'t ask the way. Ravens fly.', [
   ['', 'Літають. А я їжджу. На плечі.', 'They fly. I ride. On a shoulder.'],
   ['', 'Ворони літають, а ми тут пішки пиляєм.', 'Ravens fly, and we\'re slogging on foot.']]],
 ]},
 { p: 'гірше', o: [['', 'Було {from_in}, стало {biome_here_in}. З гівна в болото.', 'First {from_in}, now {biome_here_in}. Out of the shit and into the swamp.']], r: [
  ['', 'раю', 'Там, звідки ми йшли, теж не було раю.', 'Where we came from wasn\'t paradise either.', [
   ['', 'Не було. Але там гівно хоч знайоме.', 'It wasn\'t. But at least the shit there was familiar.'],
   ['', 'Рай, рай… Рай — то де годують.', 'Paradise, paradise… Paradise is where they feed you.']]],
  ['', 'чує', 'Не лай місце вголос. Воно чує.', 'Don\'t curse the place out loud. It hears.', [
   ['', 'Хай чує. Мені не страшно. Ані крихти.', 'Let it hear. I\'m not scared. Not one bit.'],
   ['', 'Хай чує. Хоч раз почує шось чесне.', 'Let it hear. For once it\'ll hear something honest.']]],
 ]},
 { p: 'ще', o: [['', 'Скільки ше тих країв, {м_слівце}? Де вже той кінець світу?', 'How many more of these lands, {м_слівце}? Where\'s that end of the world?']], r: [
  ['', 'замовкнеш', 'Буде. Там, де ти замовкнеш.', 'It\'ll come. Wherever you shut up.', [
   ['', 'То ніколи, ботан. Звикай.', 'So never, wiseguy. Get used to it.'],
   ['', 'Не діждешся. Я й на краю світу базікатиму.', 'Not a chance. I\'ll be yapping at the edge of the world.']]],
  ['', 'рагнарок', 'Кінець світу — то Рагнарок. Не поспішай.', 'The end of the world is Ragnarök. Don\'t rush it.', [
   ['', 'То я поспішу, професоре. Шоб скоріше той кінець.', 'Then I\'ll hurry, professor. Get to that end sooner.'],
   ['', 'Рагнарок? Там хоч бійка буде. Я — за.', 'Ragnarök? At least there\'ll be a fight. I\'m in.']]],
 ]},
 { p: 'нюх', o: [['', 'Нюх-нюх… О, нові смаки, {м_слівце}. Шось тут їстівне по-любому.', 'Sniff, sniff… Oh, new flavours, {м_слівце}. Something here is edible for sure.']], r: [
  ['', 'отрути', 'Нові смаки — нові отрути. Не все їж.', 'New flavours, new poisons. Don\'t eat everything.', [
   ['', 'Я жеру все, шо не втекло. Отрута теж не втекла.', 'I eat everything that didn\'t run off. Poison didn\'t run off either.'],
   ['', 'Отрута — то приправа. Я загартований.', 'Poison is seasoning. I\'m hardened.']]],
  ['', 'тицяй', 'Нюхай. Але дзьобом не тицяй.', 'Sniff. But don\'t poke it with your beak.', [
   ['', 'Без дзьоба нюхати нецікаво.', 'Sniffing without the beak is no fun.'],
   ['', 'Дзьоб сам тицяє. Я його не контролюю.', 'The beak pokes by itself. I don\'t control it.']]],
 ]},
 { p: 'стовп', o: [['', 'Ні стовпа, ні знака, бля. Хто так межі ставить, га? Руки б повідривав.', 'No post, no sign, fuck. Who sets borders like that, huh? I\'d rip their hands off.']], r: [
  ['', 'природа', 'Природа ставила. Їй стовпи не потрібні.', 'Nature set it. Nature doesn\'t need posts.', [
   ['', 'Та й мені не треба. Я межу носом чую.', 'I don\'t need one either. I smell the border with my nose.'],
   ['', 'То я природі й повідриваю. Шо там у неї є.', 'Then I\'ll rip nature\'s off. Whatever she\'s got.']]],
  ['', 'серцем', 'Межі видно серцем, Муніне. Ну, тобі — ні.', 'Borders are seen with the heart, Munin. Well, not by you.', [
   ['', 'Є в мене серце. Маленьке, чорне й злюще.', 'I have a heart. Small, black and mean.'],
   ['', 'Серцем, ага. А я — пузом. Тут пузо інакше бурчить.', 'With the heart, sure. I use my belly. It growls differently here.']]],
 ]},
 { p: 'тут', o: [['', 'Все, {biome_here}. Ну, хто тут найзліший? Крім мене, ясно.', 'That\'s it, {biome_here}. So who\'s the meanest here? Besides me, obviously.']], r: [
  ['', 'ненадовго', 'Найзліший тут — ти. І це ненадовго.', 'The meanest here is you. Not for long.', [
   ['', 'Надовго. Я тут зара всім покажу.', 'For long. I\'ll show them all right now.'],
   ['', 'Ненадовго? Я й тебе переживу, ботан.', 'Not for long? I\'ll outlive you too, wiseguy.']]],
  ['', 'знайде', 'Не шукай найзлішого. Сам знайде.', 'Don\'t look for the meanest. It\'ll find you.', [
   ['', 'Хай знайде. Пошкодує, сука.', 'Let it. It\'ll regret it, the bitch.'],
   ['', 'Хай. Я якраз розігрівся.', 'Let it. I\'m just warmed up.']]],
 ]},
 { p: 'назад', o: [['', '{from_in} хоч гівно було знайоме, рідне. Пішли назад, а?', '{from_in} at least the shit was familiar. Homely. Let\'s go back, huh?']], r: [
  ['', 'вперед', 'Назад не підемо. Вперед цікавіше.', 'We\'re not going back. Forward is more interesting.', [
   ['', 'Тобі цікавіше. А мені страшніше. Ну, ладно, пішли.', 'More interesting for you. Scarier for me. Fine, let\'s go.'],
   ['', 'Цікавіше… Ну, бурчу, але йду.', 'More interesting… Well, I grumble, but I go.']]],
  ['', 'болото', 'Знайоме болото — теж болото.', 'A familiar swamp is still a swamp.', [
   ['', 'Болото, але своє, рідне.', 'A swamp, but our own. Homely.'],
   ['', 'Філософ. В болоті розбирається.', 'A philosopher. Knows his swamps.']]],
 ]},
 { p: 'погода', o: [['', 'Погода тут якась не така. Гірша, курва мать.', 'The weather here is off. Worse, damn it.']], r: [
  ['', 'мокрий', 'Погода не гірша. Ти просто мокрий.', 'The weather isn\'t worse. You\'re just wet.', [
   ['', 'Мокрий, бо ти мене під дощ потяг, розумнику.', 'Wet because you dragged me into the rain, smartass.'],
   ['', 'Мокрий і злий. Обережно мене.', 'Wet and mad. Careful with me.']]],
  ['', 'звикнемо', 'Звикнемо. Ми до всього звикаємо.', 'We\'ll get used to it. We get used to everything.', [
   ['', 'Ти звикаєш. А я бурчу. Так і живем.', 'You get used to it. I grumble. That\'s how we live.'],
   ['', 'Не хочу звикати. Хочу, шоб було сухо.', 'I don\'t want to get used to it. I want it dry.']]],
 ]},
 { p: 'слід', o: [['', 'Йо, хтось тут ходив. Слід здоровенний. Хочу глянути, чий. Зблизька.', 'Yo, something walked here. Huge track. I want to see whose. Up close.']], r: [
  ['', 'біда', 'Великий слід — велика біда. Не кличмо.', 'A big track means big trouble. Let\'s not call it.', [
   ['', 'Біда — то моє друге ім\'я, ботан. А перше — Мунін.', 'Trouble is my middle name, wiseguy. The first one is Munin.'],
   ['', 'Кличмо. Нудно ж.', 'Let\'s call it. It\'s boring.']]],
  ['', 'паща', 'Хочеш подивитися? Подивишся. Звідти, з пащі.', 'Want to see? You will. From inside its jaws.', [
   ['', 'З пащі ше краще видно. Зуби, гланди, все.', 'You see even better from the jaws. Teeth, tonsils, everything.'],
   ['', 'Я в пащі застрягну. Я колючий.', 'I\'ll get stuck in the jaws. I\'m prickly.']]],
 ]},
 { p: 'дерева', o: [['', 'Дерева тут криві, як твої поради, ботан.', 'The trees here are as crooked as your advice, wiseguy.']], r: [
  ['', 'коріння', 'Криві дерева — прямі коріння.', 'Crooked trees, straight roots.', [
   ['', 'Коріння, поради — все одно криве.', 'Roots, advice, all crooked anyway.'],
   ['', 'Коріння я не бачу. Бачу криве.', 'I don\'t see roots. I see crooked.']]],
  ['', 'рівні', 'Мої поради — найрівніші в Мідґарді.', 'My advice is the straightest in Midgard.', [
   ['', 'Рівні, ага. Як твій хвіст після бійки.', 'Straight, sure. Like your tail after a fight.'],
   ['', 'Найрівніші, бо їх ніхто не слухає. Не гнуться.', 'Straightest because nobody listens to them. They don\'t bend.']]],
 ]},
 { p: 'край', o: [['', 'Ідемо, ідемо, ідемо… Де той край? Я вже заїбався.', 'Walking, walking, walking… Where\'s the edge? I\'m fucking sick of it.']], r: [
  ['', 'дорога', 'Краю нема. Є тільки дорога.', 'There\'s no edge. There\'s only the road.', [
   ['', 'Ну то пішли далі. Бурчу, але йду.', 'Then let\'s go on. I grumble, but I go.'],
   ['', 'Дорога, дорога… Філософ хрінів.', 'The road, the road… Damn philosopher.']]],
  ['', 'досить', 'Край буде. Коли {name} скаже «досить».', 'There\'ll be an edge. When {name} says “enough.”', [
   ['', '{name}, скажи вже «досить». Ну будь ласка.', '{name}, say “enough” already. Pretty please.'],
   ['', 'Не скаже. Я {його|її} знаю.', '{He|She} won\'t. I know {him|her}.']]],
 ]},
]};
// ============================== FRIEND (H open, M reply, H? close) ==============================
S.friend = { O: 'H:х_друг', R: 'M:м_друг', C: 'H:х_друг_кінець', items: [
 { p: 'гість', o: [['', '{friend} поруч. Поводься пристойно, Муніне.', '{friend} is close. Behave, Munin.']], r: [
  ['', 'шолом', 'Пристойно? Я гостеві в шолом насеру. Пристойно, ага.', 'Behave? I\'ll shit in the guest\'s helmet. Politely, sure.', [
   ['', 'Насереш — і {friend} зварить тебе в тому ж шоломі.', 'Do it, and {friend} will boil you in that same helmet.'],
   ['', 'Гаразд. Хоч не плюй.', 'Fine. At least don\'t spit.']]],
  ['', 'дзьобнути', '{friend}? О, ше один, кого можна дзьобнути.', '{friend}? Oh, one more to peck.', [
   ['', 'Дзьобнеш — {friend} дзьобне у відповідь. Мечем.', 'Peck, and {friend} pecks back. With a sword.'],
   ['', 'Дзьобай краще мене. Я хоча б звик.', 'Peck me instead. At least I\'m used to it.']]],
 ]},
 { p: 'разом', o: [['', '{friend} поруч — безпечніше. Удвох легше.', 'It\'s safer with {friend}. Easier with two.']], r: [
  ['', 'дремене', 'Безпечніше? Та {friend} перш за всіх і дремене, от побачиш.', 'Safer? {friend} will be the first to bolt, you\'ll see.', [
   ['', 'Першим дремениш ти. Перевірено.', 'You\'ll bolt first. Tested.'],
   ['', 'Б\'юся об заклад на твою пайку, що ні.', 'I bet your share of food {friend} won\'t.']]],
  ['', 'тікати', 'Удвох легше. Тікати, бля.', 'Easier with two. To run away, fuck.', [
   ['', 'Тікати ти вмієш і сам. Талант.', 'You can run alone just fine. A talent.'],
   ['', 'Тікати удвох — це вже маневр.', 'Running as a pair is a manoeuvre.']]],
 ]},
 { p: 'дивиться', o: [['', '{friend} на нас дивиться. Усміхнись. Ой, ти ж не вмієш.', '{friend} is looking at us. Smile. Oh, you can\'t.']], r: [
  ['', 'погане', 'Хай дивиться. Я теж дивлюсь. І думаю всяке погане.', 'Let them look. I\'m looking too. And thinking nasty things.', [
   ['', 'По тобі видно. Ти думаєш обличчям.', 'It shows. You think with your face.'],
   ['', 'Вже пізно. Все побачили.', 'Too late. They saw it all.']]],
  ['', 'усмішка', 'Вмію. Ось. Бачиш?', 'I can. There. See?', [
   ['', 'Бачу. Тепер {friend} задкує.', 'I see. Now {friend} is backing away.'],
   ['', 'Бачу. Краще не треба.', 'I see. Better not.']]],
 ]},
 { p: 'ворони', o: [['', 'Цікаво, {friend} має своїх воронів? Сподіваюсь, не таких, як ти.', 'I wonder if {friend} has ravens. Hopefully not like you.']], r: [
  ['', 'наваляю', 'Якшо є — наваляю. Обом.', 'If there are, I\'ll beat them up. Both.', [
   ['', 'Отож. Тому {friend} їх при тобі й не показує.', 'Exactly. That\'s why {friend} doesn\'t show them around you.'],
   ['', 'Обом наваляєш, а потім мені скаржитимешся.', 'You\'ll beat both, then come crying to me.']]],
  ['', 'район', 'Якшо є — хай знають, хто тут на районі головний.', 'If there are, let them know who runs this block.', [
   ['', 'Головний тут {name}. Ти — дзвіночок на плечі.', '{name} runs it. You\'re a bell on the shoulder.'],
   ['', 'Знатимуть. По запаху.', 'They\'ll know. By the smell.']]],
 ]},
 { p: 'подарунок', o: [['', '{friend} щось несе. Може, нам?', '{friend} is carrying something. Maybe for us?']], r: [
  ['', 'заберу', 'Якшо не нам — сам заберу. Без питань.', 'If it\'s not for us, I\'ll take it. No questions.', [
   ['', 'Ну то хоч не кради при всіх.', 'Then at least don\'t steal in front of everyone.'],
   ['', 'Забереш — і {friend} забере тебе. На юшку.', 'Take it, and {friend} takes you. For soup.']]],
  ['', 'неносять', 'Нам? Нам ніхто ніхуя не носить.', 'For us? Nobody brings us fuck-all.', [
   ['', 'Носили б, якби ти не дзьобав руку, що дає.', 'They would, if you didn\'t peck the hand that gives.'],
   ['', 'Бо ти дякуєш лайкою.', 'Because you say thanks with swearing.']]],
 ]},
 { p: 'спомин', o: [['', '{friend} знову тут. Пам\'ятаєш минулий раз?', '{friend} is here again. Remember last time?']], r: [
  ['', 'нічо', 'Пам\'ятаю. Нічо доброго, бля.', 'I remember. Nothing good, fuck.', [
   ['', 'Цього разу буде краще. Сподіваюсь.', 'This time it\'ll go better. I hope.'],
   ['', 'Бо ти тоді все й зіпсував.', 'Because you\'re the one who ruined it.']]],
  ['', 'пірʼїна', 'Пам\'ятаю. Я тоді пір\'їну проїбав. Хорошу пір\'їну.', 'I remember. I fucking lost a feather then. A good feather.', [
   ['', 'Твою пір\'їну {friend} досі носить на шапці.', '{friend} still wears your feather in a cap.'],
   ['', 'Пір\'їна відросла. А сорому ти тоді наробив надовго.', 'The feather grew back. The shame you caused lasts longer.']]],
 ]},
 { p: 'частування', o: [['', '{friend} може пригостити. Будь чемним, Муніне.', '{friend} might treat us. Be polite, Munin.']], r: [
  ['', 'раз', 'Чемним? За жратву — буду. Раз. Цього разу.', 'Polite? For food, I will. Once. This time.', [
   ['', 'Чемність годує. Запам\'ятай.', 'Politeness feeds you. Remember that.'],
   ['', 'Раз — уже рекорд. Тримаю за слово.', 'Once is already a record. I\'ll hold you to it.']]],
  ['', 'ситих', 'Чемність — то для ситих, ботан.', 'Politeness is for the well-fed, wiseguy.', [
   ['', 'Чемних голодних годують першими. Подумай.', 'Polite hungry ones get fed first. Think about it.'],
   ['', 'Тому ти й завжди голодний.', 'That\'s why you\'re always hungry.']]],
 ]},
 { p: 'броня', o: [['', '{friend} у новій броні. Блищить.', '{friend} is in new armour. It shines.']], r: [
  ['', 'дзьобнути', 'Блищить? Дай дзьобнути. Хоч разок.', 'Shines? Let me peck it. Just once.', [
   ['', 'Будь чемним до чужої броні.', 'Be polite to other people\'s armour.'],
   ['', 'Дзьобнеш — дзьоб погнеш. Це ж залізо.', 'Peck it and you\'ll bend your beak. It\'s iron.']]],
  ['', 'вній', 'Броня гарна. А от те, шо в ній, — так собі.', 'The armour\'s nice. What\'s inside it, so-so.', [
   ['', 'Голосніше не кажи. {friend} у броні, а ти — ні.', 'Don\'t say it louder. {friend} has armour, you don\'t.'],
   ['', 'Те, що в ній, тебе хоча б не носить. Тобі пощастило.', 'At least what\'s inside doesn\'t have to carry you. Lucky for it.']]],
 ]},
 { p: 'гомін', o: [['', 'Удвох вони галасують, як ціла дружина.', 'The two of them are as noisy as a whole war band.']], r: [
  ['', 'перекричу', 'А я їх перекричу. Одним матюком.', 'And I\'ll outshout them. With one curse.', [
   ['', 'Галасу й так досить.', 'There\'s enough noise as it is.'],
   ['', 'Перекричиш. Ти ж для цього й вилупився.', 'You will. That\'s what you hatched for.']]],
  ['', 'горлають', 'Хай горлають. Хоч не нудно, бля.', 'Let them yell. At least it\'s not boring, fuck.', [
   ['', 'Тобі нудно, тільки коли тихо. А тихо не буде.', 'You\'re only bored when it\'s quiet. It won\'t be.'],
   ['', 'Не нудно. Аж вуха болять.', 'Not boring. My ears hurt.']]],
 ]},
 { p: 'допомога', o: [['', '{friend} допомагає. Рідкість у цих землях.', '{friend} is helping. A rarity in these lands.']], r: [
  ['', 'хоче', 'Допомагає, бо шось хоче, {м_слівце}. Сто пудів.', 'Helping because they want something, {м_слівце}. Guaranteed.', [
   ['', 'Не всі такі, як ти.', 'Not everyone is like you.'],
   ['', 'Хоче. Щоб ти помовчав. Я теж.', 'They want something. For you to shut up. Me too.']]],
  ['', 'незаважатиму', 'Ну хай допомагає. Я не заважатиму. Сильно.', 'Fine, let them help. I won\'t get in the way. Much.', [
   ['', '«Сильно». Хоч чесно.', '“Much.” At least it\'s honest.'],
   ['', 'Не заважай зовсім. Спробуй, раптом сподобається.', 'Don\'t get in the way at all. Try it, you might like it.']]],
 ]},
 { p: 'смажене', o: [['', '{friend} пахне димом і смаженим.', '{friend} smells of smoke and roast meat.']], r: [
  ['', 'непринесли', 'Смаженим? То чого нам нічо не принесли, га?', 'Roast meat? Then why didn\'t they bring us any, huh?', [
   ['', 'Бо ти минулого разу вкрав цілу миску.', 'Because last time you stole a whole bowl.'],
   ['', 'Принесли б, якби ти не дзьобався.', 'They would, if you didn\'t peck.']]],
  ['', 'слина', 'Я вже слиною капаю, {м_слівце}.', 'I\'m drooling already, {м_слівце}.', [
   ['', 'Ти завжди про їжу.', 'You\'re always about food.'],
   ['', 'Не капай на плече. Обладунки іржавіють.', 'Don\'t drip on the shoulder. Armour rusts.']]],
 ]},
 { p: 'спина', o: [['', '{friend} поруч — хоч спина прикрита.', 'With {friend} nearby, at least our back is covered.']], r: [
  ['', 'ніж', 'Прикрита? Аби ножем не прикрили.', 'Covered? As long as it\'s not with a knife.', [
   ['', '{friend} — не ти. Ножа в спину не встромить.', '{friend} isn\'t you. Won\'t stab anyone in the back.'],
   ['', 'Параноїк. Але живий параноїк.', 'Paranoid. But a living paranoid.']]],
  ['', 'дзьобом', 'Спину я сам прикрию. Дзьобом, бля.', 'I\'ll cover the back myself. With my beak, fuck.', [
   ['', 'Дзьобом ти прикриваєш тільки свою.', 'With your beak you only cover your own.'],
   ['', 'Удвох спину тримати легше. Навіть із тобою.', 'Two guard a back easier. Even with you.']]],
 ]},
 { p: 'розмова', o: [['', '{friend} щось каже. Слухай, Муніне, може, щось розумне.', '{friend} is saying something. Listen, Munin, maybe it\'s smart.']], r: [
  ['', 'люди', 'Розумне? Від людей? Ой, не смішіть мене.', 'Smart? From humans? Oh, don\'t make me laugh.', [
   ['', 'Від людей — частіше, ніж від тебе.', 'From humans, more often than from you.'],
   ['', 'Від людей. Ти ж від них і лаятись навчився.', 'From humans. That\'s where you learned to swear.']]],
  ['', 'хуйня', 'Я слухаю. Поки шо — хуйня якась.', 'I\'m listening. So far it\'s bullshit.', [
   ['', 'Ти й мене так слухаєш.', 'That\'s how you listen to me too.'],
   ['', 'Зате ти — суцільна цікавість.', 'Whereas you are pure fascination.']]],
 ]},
]};

// ---------------- build ----------------
const N = {}; // list key -> [[mark, uk, en]]
const add = (k, tone, mk, uk, en) => (N[k] = N[k] || []).push([(tone ? tone + ' ' : '') + '^' + mk, uk, en]);
const problems = [];
for (const sc in S) {
  const { O, R, C, items } = S[sc];
  const subs = new Set();
  for (const it of items) {
    const p = sc + '.' + it.p;
    for (const [t, uk, en] of it.o) add(O, t, p, uk, en);
    for (const [t, sub, uk, en, cl] of it.r) {
      const s = sc + '.' + it.p + '_' + sub;
      add(R, t, p + '>' + s, uk, en);
      if (cl) { if (subs.has(s)) problems.push('dup sub ' + s); subs.add(s); for (const [ct, cuk, cen] of cl) add(C, ct, s, cuk, cen); }
      else if (!it.r.some(x => x[1] === sub && x[4])) problems.push('sub without closes ' + s);
    }
  }
}
const out = [];
for (const key of Object.keys(N)) {
  const [who, list] = key.split(':'); const o = old[key]; if (!o) throw new Error('no old list ' + key);
  N[key].forEach(([mk, uk, en], i) => {
    const u = mk + ' ' + uk, e = mk + ' ' + en;
    if (i < o.length) out.push({ who, list, op: 'set', old: o[i], uk: u, en: e });
    else out.push({ who, list, op: 'add', uk: u, en: e });
  });
  for (let i = N[key].length; i < o.length; i++) out.push({ who, list, op: 'del', old: o[i] });
}
for (const k in old) if (!N[k]) problems.push('old list untouched ' + k);
fs.writeFileSync(path.join(dir, 'edits_C.json'), JSON.stringify(out, null, 1));

// ---------------- checks ----------------
const tiers = mk => { const m = /^~(\d+) /.exec(mk); return m ? m[1].split('').map(Number) : [1, 2, 3]; };
const thr = mk => mk.replace(/^~\d+ /, '').slice(1);
for (const sc in S) {
  const { O, R, C } = S[sc];
  const a = N[O].filter(x => thr(x[0]).startsWith(sc + '.')), b = N[R].filter(x => thr(x[0]).startsWith(sc + '.')), c = N[C].filter(x => thr(x[0]).startsWith(sc + '.'));
  const opens = new Set(a.map(x => thr(x[0])));
  for (const t of opens) for (const tr of [1, 2, 3]) { const n = b.filter(x => thr(x[0]).split('>')[0] === t && tiers(x[0]).includes(tr)).length; if (n < 2) problems.push(`reply short ${t} ~${tr} ${n}`); }
  const nexts = new Set(b.map(x => thr(x[0]).split('>')[1]));
  for (const t of nexts) for (const tr of [1, 2, 3]) { const n = c.filter(x => thr(x[0]) === t && tiers(x[0]).includes(tr)).length; if (n < 2) problems.push(`close short ${t} ~${tr} ${n}`); }
  for (const x of c) { if (!nexts.has(thr(x[0]))) problems.push('orphan close ' + x[0]); if (/\?\s*$/.test(x[1])) problems.push('question in close ' + x[1]); }
  for (const x of b) if (!opens.has(thr(x[0]).split('>')[0])) problems.push('orphan reply ' + x[0]);
}
// Munin swears: at most 1 where the template can add one (all but м_перехід_відповідь), 2 there
const SW = /(\{м_слівце\}|бля|курв|сук[аио]|\bсуч|ху[йяєїю]|пизд|[їй]б[аеіо]|заїб|сра[кцв]|срав|гівн|ссикл|сци\b)/gi;
for (const k in N) if (k.startsWith('M:')) for (const x of N[k]) { const m = x[1].match(SW) || []; const lim = k === 'M:м_перехід_відповідь' ? 2 : 1; if (m.length > lim) problems.push(`swears ${m.length} ${k} ${x[1]}`); }
for (const k in N) if (k.startsWith('H:')) for (const x of N[k]) if ((x[1].match(SW)||[]).length) problems.push('hugin swear ' + x[1]);
// duplicates within a list
for (const k in N) { const seen = new Set(); for (const x of N[k]) { if (seen.has(x[1])) problems.push('dup ' + k + ' ' + x[1]); seen.add(x[1]); } }
for (const p of problems) console.log('PROBLEM', p);
console.log('ops', out.length, 'set', out.filter(x => x.op === 'set').length, 'add', out.filter(x => x.op === 'add').length, 'del', out.filter(x => x.op === 'del').length, 'problems', problems.length);
for (const k in N) console.log(k, N[k].length, '(old ' + old[k].length + ')');

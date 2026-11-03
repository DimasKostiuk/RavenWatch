// The ravens' lines as a player hears them: every tag of every *Patterns list (in Patterns.All order, read from
// Patterns.cs itself), put together the way the game does it (template, parts and parts inside parts, trust tier, the
// player's forms, a raven's manner and the street mouth of Gutter) and checked for what a machine can see.
//   node tools/phrasesample.js [tagFilter] [-n N] [-plain]
//     tagFilter  a regexp over the tags (default: all)
//     -n N       lines per tag, raven and tier (default 4)
//     -plain     no ⟦…⟧ marks around what Manner/Gutter added
// Writes build/samples/<file>.txt (the lines by tag) and build/samples/_flags.txt (the problems found).
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), src = path.join(root, 'src'), out = path.join(root, 'build', 'samples');

const args = process.argv.slice(2);
let filter = null, N = 4, marks = true;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '-n') N = +args[++i];
  else if (args[i] === '-plain') marks = false;
  else filter = new RegExp(args[i]);
}

// ---------------------------------------------------------------- the books
function arrays(file, name) {
  const s = fs.readFileSync(path.join(src, file), 'utf8');
  const i = s.indexOf('string[] ' + name + ' =');
  if (i < 0) return [];
  const j = s.indexOf('\n        };', i);
  const res = [], re = /^\s*"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?$/gm, body = s.slice(i, j);
  let m;
  while ((m = re.exec(body))) res.push(JSON.parse('"' + m[1] + '"'));
  return res;
}
const allLine = fs.readFileSync(path.join(src, 'Patterns.cs'), 'utf8').split('\n').find(l => l.includes('_allH = l.ToArray()'));
const files = ['Patterns.cs'].concat([...allLine.matchAll(/AddRange\((\w+)\.Hugin\)/g)].map(m => m[1] + '.cs'));

function book(name) {
  const t = {}, p = {}, fileOf = {}, order = [];
  for (const f of files) {
    let cur = null;
    for (const raw of arrays(f, name)) {
      const l = raw.trim();
      if (!l || l.startsWith('#')) continue;
      if (l.startsWith('[')) { const e = l.indexOf(']'), tag = l.slice(1, e); if (!t[tag]) { t[tag] = []; fileOf[tag] = f; order.push(tag); } t[tag].push(l.slice(e + 1).trim()); cur = null; continue; }
      if (l.startsWith('@')) { cur = l.slice(1).trim(); p[cur] = p[cur] || []; continue; }
      if (cur) p[cur].push(l);
    }
  }
  return { t, p, fileOf, order };
}

// ---------------------------------------------------------------- the game's rules, in small
const rnd = () => Math.random(), pick = a => a[Math.floor(rnd() * a.length)];
function tone(line, tier) {
  const s = line.trimStart();
  if (s.length < 2 || s[0] !== '~') return line;
  let i = 1, fits = false;
  while (i < s.length && /\d/.test(s[i])) { if (+s[i] === tier) fits = true; i++; }
  if (i === 1) return line;
  return fits ? s.slice(i).trimStart() : null;
}
function chance(line) { const t = line.trimStart(); const m = t.match(/^%(\d+)\s*/); return m ? (rnd() * 100 < +m[1] ? t.slice(m[0].length) : null) : line; }
function thread(line) { return line[0] === '^' ? line.slice(line.indexOf(' ') + 1 || line.length).trimStart() : line; }
const swearList = n => n === 'матюк' || n === 'м_лайка' || n === 'м_вигук' || n === 'м_слівце';

let missing = new Set();
function expand(b, s, tier, wordy, depth) {
  return s.replace(/\{([^{}|]+?)(\?)?\}/g, (whole, name, opt) => {
    if (!b.p[name]) { if (/[а-яіїєґ]/.test(name)) missing.add(name); return whole; } // a placeholder
    if (opt && rnd() >= (swearList(name) ? 0.75 : wordy)) return '';
    const fit = b.p[name].map(x => tone(x, tier)).filter(x => x != null).map(thread);
    if (!fit.length) return opt ? '' : '⟪no ' + name + ' for ~' + tier + '⟫';
    let x = pick(fit);
    if (depth < 1) x = expand(b, x, tier, wordy, depth + 1);
    return x;
  });
}
const tidy = s => s.replace(/\s+/g, ' ').replace(/ ([,.!?:;…])/g, '$1').trim();

// LineMood (src/LineMood.cs), the same rules
const S=(t,...h)=>h.some(x=>t.startsWith(x)), I=(t,...x)=>x.includes(t);
function mood(t){
  if(S(t,"mem_","grave_","fate_","guest_","nick_","breed_reveal_")||I(t,"loyal","petted","awake","heal","bond_friend_died","bond_friend_hurt","people_respect_died","people_respect_back","stranger_death","errand_failed","habit_lost"))return 'sincere';
  if(S(t,"dlg_","x_","about_","bond_","pair_","perch_","chase_","joint_","victim_","people_")||I(t,"cut_in","stranger_talk","again_lead"))return 'talk';
  if(S(t,"death","fall","judge_tip_")||I(t,"near_death","sh_near_death","sh_fall","encumbered","escaped","flee","broke","freezing","poisoned","burning","portal_ore","hstrike_miss","mstrike_miss","stranger_peck","judge_1","judge_2","lastchance","hungry","hungry_bad","hungry_ask_again","starve_peck","home_forgotten","home_broken","revenge_late","revenge_gone"))return 'fail';
  if(S(t,"kill","sh_kill","build","craft","cook","smelt","ferment","repair","chop","mine","pick","plant","loot","trade","fav_","fed","trophy","hstrike","mstrike")||I(t,"boss","sh_boss","strike_boss","strike_stronger","sh_build","sh_smelt","sh_plant","sh_trade","sh_trophy_hang","fish","fish_again","tame","mtame_done","level","levelup","judge_4","judge_5","errand_done","revenge_paid","parry","dodge","beast_kill"))return 'win';
  return 'neutral';
}

// Manner (src/Manner.cs): an end only where the line's mood fits it (the cooldown of minutes is left out here)
const SND = ['fail', 'win', 'neutral', 'talk'];
const M = {
  hOpen: ['Хм.', 'Ну.', 'Ну-ну.', 'Слухай.', 'Ага.', 'Так.', 'От.'],
  mOpen: ['Чуєш.', 'Йо.', 'Ну.', 'Е.', 'Бля.', 'Тьху.', 'Слухай сюди.', 'Короче.', 'Та бля.'],
  hEnd: [['Кра.', SND], ['Ну-ну.', SND], ['Отак от.', SND], ['Логічно.', ['fail']], ['Хто б сумнівався.', ['fail', 'win']], ['Браво.', ['fail', 'win']], ['Ага.', SND]],
  mEnd: [['Тьху.', ['fail', 'neutral', 'talk']], ['Я сказав.', SND], ['Кар!', SND], ['І все, нахуй.', ['fail', 'neutral']], ['Бля.', ['fail', 'neutral', 'talk']], ['Вкурив?', SND], ['Шо, нє?', SND], ['Ото так, курва.', ['fail', 'win']]],
};
function dress(s, munin, md) {
  if (md === 'sincere') return s;
  const open = pick(munin ? M.mOpen : M.hOpen), [end, fits] = pick(munin ? M.mEnd : M.hEnd), r = rnd();
  if (r < 0.1 * 0.85) return '⟦' + open + '⟧ ' + s.charAt(0).toUpperCase() + s.slice(1);
  const w = end.replace(/[.!?]/g, '').split(',')[0].toLowerCase();
  if (r >= 0.1 && r < 0.27 && fits.includes(md) && !new RegExp('(^|[^а-яіїєґ])' + w + '($|[^а-яіїєґ])', 'i').test(s)) return s + ' ⟦' + end + '⟧';
  return s;
}
// Gutter (src/Gutter.cs): only what adds to a line (the word swaps are left out: they are read in Gutter itself)
const G = {
  hJab: ['Незграба.', 'Роззява.', 'Недотепа.', 'Генію.', 'Ну ти й {бовдур|бовдурка}.', 'Ну ти й {йолоп|йолопка}.', 'Ну ти й диво.', 'Ну й руки.', 'Талант.', 'Майстер.', 'Ну хто так робить?'],
  hJabWarm: ['Ех ти.', 'Горе моє.', 'Ну-ну, генію.', 'Недотепа {мій|моя}.', 'Що б ти без мене роби{в|ла}.', 'Чудо ти моє.'],
  hGrunt: ['Ну,', 'Слухай,', 'Ага,', 'Хм,', 'Так,', 'Ну от,', 'Знаєш,', 'Ну-ну,', 'От'],
  mGrunt: ['Чуєш,', 'Йо,', 'Та бля,', 'Ну шо,', 'Ото', 'Короче,', 'Нє, ну', 'Тю,', 'Йой,', 'Бля,', 'Е,', 'Ге,'],
  mCurse: ['Лошара.', 'От чмо.', 'Тьху на тебе.', 'Насеру тобі в шолом, побачиш.', 'Ото мудило.', 'Пиздець, а не воїн.', 'Позорище, бля.', 'Хрін тобі, а не слава.'],
  mCurseCry: ['Ну шо за пиздець.', 'Ну його в пизду.', 'Та ну нахуй.', 'До сраки таке.', 'Їбать-копать.', 'Йо-майо.', 'Охрініти.', 'Ото халепа.'],
  mCurseWarm: ['Ех ти, дурне моє.', 'Йой, братан.', 'Дурне, але моє.', 'Свій, хоч і довбень.', 'Горе моє, курва.'],
};
const ends = s => /[.!?…]$/.test(s);
function street(s, munin, tier, md) {
  if (munin) {
    const k = tier >= 3 ? 0.6 : tier === 2 ? 0.8 : 1;
    if (rnd() < 0.16 * k && /^[А-ЯІЇЄҐ]/.test(s)) s = '⟦' + pick(G.mGrunt) + '⟧ ' + s[0].toLowerCase() + s.slice(1);
    if ((md === 'fail' || md === 'neutral') && rnd() < 0.13 * k * (md === 'fail' ? 1 : 0.5) && ends(s) && s.length < 120) s += ' ⟦' + pick(tier >= 3 ? G.mCurseWarm : md === 'fail' ? G.mCurse : G.mCurseCry) + '⟧';
    return s;
  }
  if (md === 'sincere') return s;
  const r = rnd();
  if (r < 0.1 && /^[А-ЯІЇЄҐ]/.test(s)) return '⟦' + pick(G.hGrunt) + '⟧ ' + s[0].toLowerCase() + s.slice(1);
  if (md === 'fail' && r >= 0.1 && r < 0.1 + (tier >= 3 ? 0.08 : 0.15) && ends(s) && !s.endsWith('?') && s.length < 110) return s + ' ⟦' + pick(tier >= 3 ? G.hJabWarm : G.hJab) + '⟧';
  return s;
}

// the values a line is filled with
const V = {
  name: 'Ейнар', friend: 'Сіґрід', raven: 'Мунін', fraven: 'Хугін', nick: 'Сомельє', biome: 'Чорний ліс', biome_in: 'у Чорному лісі',
  biome_here: 'Болота', biome_here_in: 'на Болотах', seen: 'троль', seen_gen: 'троля', seen_ins: 'тролем', seen_genpl: 'тролів', seen_enemy: 'троль', seen_count: '3',
  boss: 'Старійшина', boss_gen: 'Старійшини', boss_ins: 'Старійшиною', killer: 'драугр', killer_gen: 'драугра', killer_ins: 'драугром', killer_genpl: 'драугрів',
  top: 'ґрейдворф', top_gen: 'ґрейдворфа', top_ins: 'ґрейдворфом', top_genpl: 'ґрейдворфів', foe: 'вовк', foe_gen: 'вовка', foe_genpl: 'вовків', prey: 'олень',
  attacker: 'кістяк', attacker_gen: 'кістяка', attacker_ins: 'кістяком', food: 'ягідний пиріг', coins: '100 монет', count: '7', n: '3', times: '5', km: '12,4',
  item: 'бронзова сокира', broke: 'бронзова сокира', fish: 'окунь', tamed: 'вовк', tamed_gen: 'вовка', tamed_ins: 'вовком', mount: 'лоукс', weapon: 'булава',
  trader: 'Гальдор', trader_gen: 'Гальдора', trader_ins: 'Гальдором', owner: 'Сіґрід', me: 'Хугін', stranger: 'Торвальд', kin: 'брат', breed: 'Попелястий',
};
function fill(s, female, femaleOther) {
  s = s.replace(/\{>([^{}|]*)\|([^{}|]*)\}/g, (_, a, c) => femaleOther ? c : a);
  s = s.replace(/\{([^{}|]*)\|([^{}|]*)\}/g, (_, a, c) => female ? c : a);
  s = s.replace(/\{(\w+)\}/g, (_, n) => V[n] !== undefined ? V[n] : /_(all|times|kills|km|hours|max|lost)$/.test(n) ? '42' : '‹' + n + '›');
  s = s.length ? s[0].toUpperCase() + s.slice(1) : s;
  return s.replace(/([^.][.!?]|^[.!?]) ([а-яіїєґa-z])/g, (_, a, c) => a + ' ' + c.toUpperCase()); // Lang.UpperSentences
}

// ---------------------------------------------------------------- the checks
const ROOTS = ['курв', 'бля', 'сук', 'хуй', 'пизд', 'пізд', 'йоб', 'гівн', 'срак', 'дуп', 'трясц', 'хрін'];
function flags(line, tpl, raw) {
  const f = [], bare = line.replace(/[⟦⟧]/g, ''), dots = bare.replace(/\.\.\./g, '…');
  if (/\{[^}]*\}|⟪/.test(bare)) f.push('unfilled');
  if (/[.!?]\s+[а-яіїєґ]/.test(dots)) f.push('lowercase after end');
  if (/[,:—]\s*[.!?]|[.!?]\s*[,:]|,,|\.\.(?!\.)|!\.|\?\./.test(dots)) f.push('punct clash');
  if (bare.length && !/[.!?…»)"*]$/.test(bare)) f.push('no end mark');
  if (/\bОдин\b(?!\s+(з|із|раз|день|на|ворон|удар|крок))/.test(bare)) f.push('Один?');
  if (/[a-z]/i.test(bare.replace(/‹\w+›/g, ''))) f.push('latin');
  if (/[ыэъё]/i.test(bare)) f.push('russian letter');
  const words = bare.toLowerCase().replace(/‹\w+›/g, '').match(/[а-яіїєґ']+/g) || [];
  for (let i = 1; i < words.length; i++) if (words[i] === words[i - 1] && words[i].length > 2 && !/^(ну|так|шоб|ото|тьху|кра|ха|ге)$/.test(words[i])) { f.push('word twice: ' + words[i]); break; }
  const stems = {};
  for (const w of words) if (w.length >= 6) { const st = w.slice(0, 5); if (stems[st] && stems[st] !== w) { f.push('stem twice: ' + stems[st] + '/' + w); break; } if (stems[st] === w) { f.push('word repeats: ' + w); break; } stems[st] = w; }
  let sw = 0; const low = bare.toLowerCase(); for (const r of ROOTS) { const n = low.split(r).length - 1; if (n >= 2) f.push('swear root twice: ' + r); sw += n; }
  const tails = (line.match(/⟦[^⟧]*⟧$/) ? 1 : 0) + (/хвіст|_кін|кінець|tail/.test(tpl) && /\{[^}]*(хвіст|кін|кінець)[^}]*\?\}\s*$/.test(tpl) && raw.trim() !== tidy(raw.replace(/\{[^}]*\?\}\s*$/, '')) ? 1 : 0);
  if (tails >= 2) f.push('two tails');
  const short = bare.split(/(?<=[.!?…])\s+/).filter(x => (x.match(/[а-яіїєґ']+/gi) || []).length <= 2);
  if (short.length >= 3) f.push('many short bits');
  return f;
}
function formSplits(b, who, res) {
  const seen = new Set();
  const look = (l, where) => {
    for (const m of l.matchAll(/([а-яіїєґ']*)\{>?([^{}|]*)\|([^{}|]*)\}([а-яіїєґ']*)/gi)) {
      const [all, pre, a, c] = m;
      if (seen.has(all)) continue;
      const mw = pre + a + m[4], fw = pre + c + m[4];
      if (/вла(ся|сь)?$|[вг]ся(ла|лася)$|вва$|(ий|ій)а$|переміг/i.test(fw) || /вла$|(ал|ил|ял)(ся|сь)$/i.test(mw)) { seen.add(all); res.push(`${who} ${where}: broken form «${all}» → ${mw} / ${fw}`); }
    }
  };
  for (const tag in b.t) for (const x of b.t[tag]) look(x, '[' + tag + ']');
  for (const n in b.p) for (const x of b.p[n]) look(x, '@' + n);
  // a list of whole sentences with bits in it (or the other way round): two lists under one name
  for (const n in b.p) {
    const ls = b.p[n].map(x => thread(tone(x, 1) ?? tone(x, 2) ?? tone(x, 3) ?? x).trim()).filter(Boolean);
    const whole = ls.filter(x => /[.!?…»]$/.test(x)), bits = ls.filter(x => !/[.!?…»,:]$/.test(x));
    const few = Math.min(whole.length, bits.length);
    if (few >= 1 && few >= ls.length * 0.08) res.push(`${who} @${n}: mixed list (${whole.length} sentences / ${bits.length} bits), e.g. «${whole[0]}» vs «${bits[0]}»`);
  }
}

// ---------------------------------------------------------------- the run
fs.mkdirSync(out, { recursive: true });
for (const f of fs.readdirSync(out)) fs.unlinkSync(path.join(out, f));
const byFile = {}, flagged = [], splits = [];
let made = 0;
for (const [who, munin] of [['Hugin', false], ['Munin', true]]) {
  const b = book(who);
  formSplits(b, who, splits);
  for (const tag of b.order) {
    if (filter && !filter.test(tag)) continue;
    const file = b.fileOf[tag];
    const lines = (byFile[file] = byFile[file] || []);
    lines.push(`\n[${tag}] ${who}`);
    for (const tier of [1, 2, 3]) {
      const temps = b.t[tag].map(x => tone(x, tier)).filter(x => x != null);
      if (!temps.length) continue;
      const got = new Set();
      for (let k = 0; k < N * 4 && got.size < N; k++) {
        const tpl0 = chance(pick(temps)); if (tpl0 == null) continue;
        const wordy = pick([0.3, 0.5, 0.72]);
        const raw = expand(b, thread(tpl0), tier, wordy, 0);
        let s = tidy(raw);
        const md = mood(tag);
        s = street(dress(s, munin, md), munin, tier, md);
        const female = rnd() < 0.5;
        const line = fill(tidy(s), female, rnd() < 0.5);
        if (got.has(line)) continue;
        got.add(line); made++;
        const shown = marks ? line : line.replace(/[⟦⟧]/g, '');
        lines.push(`  ~${tier}${female ? 'ж' : ' '} ${shown}`);
        const fl = flags(line, tpl0, raw);
        if (fl.length) flagged.push(`${fl.join('; ')}  ::  ${who} [${tag}] ~${tier}  ${line}`);
      }
    }
  }
}
for (const f in byFile) fs.writeFileSync(path.join(out, f.replace('.cs', '.txt')), byFile[f].join('\n') + '\n');
const kinds = {};
for (const l of flagged) for (const k of l.split('  ::  ')[0].split('; ')) { const key = k.split(':')[0]; kinds[key] = (kinds[key] || 0) + 1; }
fs.writeFileSync(path.join(out, '_flags.txt'), Object.entries(kinds).map(([k, n]) => `# ${k}: ${n}`).join('\n') + '\n\n' + flagged.sort().join('\n') + '\n\n# broken {m|f} forms\n' + splits.join('\n') + '\n\n# lists named but missing\n' + [...missing].join('\n') + '\n');
console.log('files', files.length, '| lines made', made, '| flagged', flagged.length, '| late splits', splits.length, '| missing lists', missing.size);
console.log(Object.entries(kinds).map(([k, n]) => k + ' ' + n).join(', '));

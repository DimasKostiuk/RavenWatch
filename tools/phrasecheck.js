// Emulates Patterns.Compose for the satiety themes: every tier, both genders, every combination of parts.
const fs = require('fs');
const src = fs.readFileSync('C:/Users/kosti/Desktop/RavensWatch/src/Patterns.cs', 'utf8');
function arr(name) {
  const start = src.indexOf('internal static readonly string[] ' + name + ' =');
  const end = src.indexOf('\n        };', start);
  const body = src.slice(start, end);
  const out = [];
  const re = /^\s*"((?:[^"\\]|\\.)*)",\s*$/gm; let m;
  while ((m = re.exec(body))) out.push(m[1].replace(/\\"/g, '"').replace(/\\\\/g, '\\'));
  return out;
}
function book(lines) {
  const t = {}, p = {}; let part = null;
  for (let raw of lines) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    if (line.startsWith('@')) { const n = line.slice(1).trim(); part = p[n] = p[n] || []; continue; }
    if (line.startsWith('[')) { const e = line.indexOf(']'); const tag = line.slice(1, e); (t[tag] = t[tag] || []).push(line.slice(e + 1).trim()); part = null; continue; }
    if (part) part.push(line);
  }
  return { t, p };
}
function tone(line, tier) {
  const s = line.trimStart();
  if (s[0] !== '~') return line;
  let i = 1, fits = false;
  while (i < s.length && /\d/.test(s[i])) { if (+s[i] === tier) fits = true; i++; }
  if (i === 1) return line;
  return fits ? s.slice(i).trimStart() : null;
}
const tags = ['hungry_bad','starve_peck','steal_food','steal_coins','steal_coins_all','steal_warn','fav_hint_kind','fav_hint_place','fav_hint_letter','fav_first','fav_again','stuffed','fed_full','corpse','hungry_ask','fed'];
const values = { name: 'Костя', food: 'Хмарка', coins: '100 монет', kind: 'ягідного', place: 'щось із далеких земель', letter: 'Х' };
const values2 = { ...values, coins: '21 монету' };
const seen = new Set(); let problems = 0, built = 0; const samples = {};
for (const who of ['Hugin', 'Munin']) {
  const b = book(arr(who));
  for (const tag of tags) {
    const temps = b.t[tag];
    if (!temps) { if (!(who === 'Hugin' && tag === 'corpse') && !(who === 'Munin' && tag === 'fav_hint_kind')) console.log(`${who} [${tag}]: NO TEMPLATE`); continue; }
    for (const tier of [1, 2, 3]) {
      let anyForTier = false;
      for (const tpl0 of temps) {
        const tpl = tone(tpl0, tier); if (tpl == null) continue;
        const slots = [...tpl.matchAll(/\{([^}|]+?)(\?)?\}/g)];
        // choices per slot
        const choices = slots.map(s => {
          const name = s[1], opt = !!s[2];
          const list = b.p[name];
          if (!list) { console.log(`${who} [${tag}] missing part @${name}`); problems++; return [''] }
          const fit = list.map(l => tone(l, tier)).filter(l => l != null);
          if (fit.length === 0 && !opt) { console.log(`${who} [${tag}] tier ${tier}: @${name} has no line for this tier`); problems++; }
          return opt ? ['', ...fit] : fit;
        });
        // cartesian
        let combos = [[]];
        for (const c of choices) { const n = []; for (const a of combos) for (const x of c) n.push([...a, x]); combos = n; }
        for (const combo of combos) {
          let s = tpl, k = 0;
          s = s.replace(/\{([^}|]+?)(\?)?\}/g, () => combo[k++]);
          s = s.replace(/\s+/g, ' ').trim();
          for (const v of [values, values2]) for (const fem of [false, true]) {
            let o = s.replace(/\{([^{}|]*)\|([^{}|]*)\}/g, (_, a, c) => fem ? c : a);
            o = o.replace(/\{(\w+)\}/g, (_, n) => v[n] !== undefined ? v[n] : '{' + n + '}');
            if (o.length) o = o[0].toUpperCase() + o.slice(1);
            built++; anyForTier = true;
            const errs = [];
            if (/\{[^}]*\}/.test(o)) errs.push('unfilled');
            if (/[.!?]\s+[а-яіїєґ]/.test(o.replace(/\.\.\./g, '…'))) errs.push('lowercase after end');
            if (/[,:—]\s*[.!?]|[.!?]\s*[,:]|,,|\.\.(?!\.)/.test(o.replace(/\.\.\./g, '…'))) errs.push('punct clash');
            if (/\s[,.!?:]/.test(o)) errs.push('space before punct');
            if (!/[.!?…»)]$/.test(o)) errs.push('no end mark');
            if (/[:—]$/.test(o)) errs.push('dangling');
            if (errs.length) { problems++; if (!seen.has(errs.join()+who+tag+s)) { seen.add(errs.join()+who+tag+s); console.log(`${who} [${tag}] t${tier}${fem ? ' ж' : ''}: ${errs.join(', ')} :: ${o}`); } }
            const key = who + ' [' + tag + '] ~' + tier;
            (samples[key] = samples[key] || new Set()).add(o);
          }
        }
      }
      if (!anyForTier) { console.log(`${who} [${tag}] tier ${tier}: NOTHING can be said`); problems++; }
    }
  }
}
console.log('built', built, 'problems', problems);
fs.writeFileSync(process.argv[2] || 'samples.txt', Object.entries(samples).map(([k, s]) => k + ' (' + s.size + ')\n  ' + [...s].slice(0, 4).join('\n  ')).join('\n'));

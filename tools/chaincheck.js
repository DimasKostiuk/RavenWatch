// Checks the chained talks of players' ravens (src/CrossPatterns.cs): every way a talk can go (2, 3 or 4 ravens,
// a join or an answer left out by chance), every speaker (Hugin or Munin) and every trust tier must find a line on
// the thread at each step. Usage: node tools/chaincheck.js [-v]  (-v: also the plain kinds' spare fallbacks)
const fs = require('fs'), path = require('path');
const src = fs.readFileSync(path.join(__dirname, '..', 'src', 'CrossPatterns.cs'), 'utf8');
function arr(name) {
  const i = src.indexOf('string[] ' + name + ' =');
  const j = src.indexOf('};', i);
  const out = []; const re = /"((?:[^"\\]|\\.)*)"/g; let m;
  while ((m = re.exec(src.slice(i, j)))) out.push(m[1].replace(/\\"/g, '"'));
  return out;
}
function book(a) {
  const b = { t: {}, p: {} }; let part = null;
  for (const raw of a) {
    const l = raw.trim();
    if (!l || l.startsWith('#')) continue;
    if (l.startsWith('@')) { part = b.p[l.slice(1)] = b.p[l.slice(1)] || []; continue; }
    if (l.startsWith('[')) { const e = l.indexOf(']'); (b.t[l.slice(1, e)] = b.t[l.slice(1, e)] || []).push(l.slice(e + 1).trim()); part = null; continue; }
    if (part) part.push(l);
  }
  return b;
}
const B = { H: book(arr('Hugin')), M: book(arr('Munin')) };
const NOISE = /_(вступ|хвіст|відгук)$/;
function take(s, t) { s = s.trimStart(); if (s[0] !== '~') return s; let i = 1, f = false; while (/\d/.test(s[i])) { if (+s[i] === t) f = true; i++; } return i === 1 ? s : f ? s.slice(i).trimStart() : null; }
// the lines a step can say: [{answers:[...], next, loose}]
function options(who, tag, t) {
  const b = B[who], out = [];
  for (const tpl of b.t[tag] || []) {
    const x = take(tpl, t); if (x == null) continue;
    for (const m of x.matchAll(/\{([^}?]+)\??\}/g)) {
      if (NOISE.test(m[1]) || !b.p[m[1]]) continue;
      for (const p of b.p[m[1]]) {
        const y = take(p, t); if (y == null) continue;
        if (y[0] !== '^') { out.push({ loose: true }); continue; }
        const mk = y.slice(1, y.indexOf(' ')), a = mk.indexOf('>');
        out.push({ answers: (a < 0 ? mk : mk.slice(0, a)).split('|'), next: a < 0 ? null : mk.slice(a + 1) });
      }
    }
  }
  return out;
}
const KINDS = ['pals', 'fire', 'boss', 'intro', 'greet', 'rename'];
const CHAINED = new Set(['pals', 'fire', 'boss']);
// the plans as CrossTalk.Plan makes them, with the chance steps in or out
function plans(kind) {
  const o = 'x_' + kind + '_open', r = 'x_' + kind + '_reply', j = 'x_' + kind + '_join', a = 'x_join_reply', c = 'x_' + kind + '_close';
  return [[o, r], [o, r, c], [o, r, j], [o, r, j, c], [o, r, j, a], [o, r, j, a, c], [o, r, j, j], [o, r, j, j, c], [o, r, j, j, a], [o, r, j, j, a, c]];
}
let problems = 0, spare = 0; const seen = new Set();
for (const kind of KINDS) {
  for (const t of [1, 2, 3]) {
    // every thread an opener can set
    const starts = new Set();
    for (const w of ['H', 'M']) for (const op of options(w, 'x_' + kind + '_open', t)) if (op.answers) starts.add(op.answers[0]);
    for (const start of starts) for (const plan of plans(kind)) {
      // walk the plan with every mix of speakers
      const walk = (i, thread, trail) => {
        if (i >= plan.length) return;
        for (const w of ['H', 'M']) {
          const opts = options(w, plan[i], t);
          const fit = opts.filter(x => x.answers && x.answers.includes(thread));
          if (fit.length === 0) {
            const key = `${kind} t${t} ${w} ${plan[i]} ^${thread}`;
            if (seen.has(key)) continue; seen.add(key);
            const hasLoose = opts.some(x => x.loose);
            if (CHAINED.has(kind) && !/^тепло$/.test(thread)) { console.log(`MISSING ${key}  (after ${trail})${hasLoose ? ' — spare line' : ' — NOTHING'}`); problems++; }
            else if (!hasLoose) { console.log(`NOTHING ${key}`); problems++; }
            else { spare++; if (process.argv.includes('-v')) console.log(`spare   ${key}`); }
            continue;
          }
          const nexts = new Set(fit.map(x => x.next || thread));
          for (const n of nexts) walk(i + 1, n, trail + ' ' + w + ':' + plan[i].replace(/^x_\w+?_/, '') + '>' + n);
        }
      };
      walk(1, start, 'open^' + start);
    }
  }
}
console.log(`problems ${problems}, spare fallbacks ${spare}`);

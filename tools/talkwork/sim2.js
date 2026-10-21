// Plays the talks as the game does now (two-level threads, chains in pair talks, no unmarked fallback, a question
// always answered, a join answered and closed). node sim2.js [N] [scene,scene|x] [seed]
const T = require('../talkcheck.js');
const { UK: B, scenes } = T;
const SW = ['матюк', 'м_лайка', 'м_вигук', 'м_слівце'];
let tier = 1, want = null, got = null, next = null;
const pick = a => a[Math.floor(Math.random() * a.length)];
const toneOk = x => { const m = /^~(\d+) /.exec(x); return !m || m[1].includes(String(tier)); };
const strip = x => x.replace(/^~\d+ /, '');
function takeMark(x) { if (!x.startsWith('^')) return [null, x]; const sp = x.indexOf(' '); return [x.slice(1, sp), x.slice(sp + 1)]; }
function answers(mk, w) { return mk.split('>')[0].split('|').some(a => a === w || (w.includes('.') && a === w.slice(0, w.indexOf('.')))); }
function nested(b, part) {
  return part.replace(/\{([^{}]+)\}/g, (m, name) => { const opt = name.endsWith('?'), bare = opt ? name.slice(0, -1) : name; const l = b.p[bare]; if (!l) return m;
    if (opt && Math.random() >= (SW.includes(bare) ? 0.75 : 0.5)) return '';
    const fit = l.filter(toneOk).map(strip).filter(x => x.indexOf('{') < 0); return fit.length ? pick(fit) : m; });
}
function expand(b, t) {
  let fail = false;
  const s = t.replace(/\{([^{}]+)\}/g, (m, name) => {
    const opt = name.endsWith('?'), bare = opt ? name.slice(0, -1) : name; const l = b.p[bare]; if (!l) return m;
    if (opt && Math.random() >= (SW.includes(bare) ? 0.75 : 0.5)) return '';
    const fit = [], loose = [], marks = new Map(); let threaded = false;
    for (const raw of l) { if (!toneOk(raw)) continue; let [mk, x] = takeMark(strip(raw)); if (mk) threaded = true;
      if (mk && want != null && !answers(mk, want)) continue; x = nested(b, x); if (!mk) { loose.push(x); continue; } fit.push(x); marks.set(x, mk); }
    let use = want == null ? fit.concat(loose) : (fit.length ? fit : (threaded ? [] : loose));
    if (!use.length) { if (!opt) fail = true; return ''; }
    const p = pick(use);
    if (got == null && marks.has(p)) { const mk = marks.get(p), a = mk.indexOf('>'); got = (a < 0 ? mk : mk.slice(0, a)).split('|')[0]; if (a >= 0) next = mk.slice(a + 1); }
    return p;
  });
  return fail ? null : s;
}
function compose(who, tag) {
  const ts = (B[who].t[tag] || []).filter(toneOk).map(strip).map(x => x.replace(/^%\d+ /, ''));
  for (let a = 0; a < 8 && ts.length; a++) { got = null; next = null; const s = expand(B[who], pick(ts)); if (s != null) return s; }
  return null;
}
const V = { name: 'Костя', friend: 'Валя', boss: 'Ейктюр', boss_gen: 'Ейктюра', boss_ins: 'Ейктюром', food: 'Мед', from: 'Луки', from_in: 'на Луках', biome_here: 'Чорнолісся', trader: 'Гальдор' };
function fill(t, v, fSelf, fOther) {
  t = t.replace(/\{>([^{}|]*)\|([^{}|]*)\}/g, (m, a, b) => fOther ? b : a).replace(/\{([^{}|]*)\|([^{}|]*)\}/g, (m, a, b) => fSelf ? b : a);
  t = t.replace(/\{([\wЀ-ӿ_]+)\}/g, (m, k) => v[k] != null ? v[k] : m).replace(/\s+/g, ' ').replace(/\s+([,.!?…:;])/g, '$1').trim();
  return t ? t[0].toUpperCase() + t.slice(1) : t;
}
function pair(s, tH, tM, chatty) {
  for (let tries = 0; tries < 3; tries++) {
    const out = []; let thread = null, ok = true;
    for (const st of s.steps) {
      const asked = out.length && out[out.length - 1].text.includes('?');
      if (st.opt && Math.random() > chatty && !asked) break;
      tier = st.who === 'M' ? tM : tH; want = thread;
      const line = compose(st.who, st.tag);
      if (line == null) { if (st.opt) break; ok = false; break; }
      thread = next != null ? next : (thread == null ? got : thread);
      out.push({ who: st.who === 'M' ? 'Мунін' : 'Хугін', text: fill(line, V, false, false), thread });
    }
    if (ok && out.length >= 2) return out;
  }
  return null;
}
const NN = { H: ['Сивопер', 'Всезнайко', 'Мудрагель', 'Пушок'], M: ['Шибеник', 'Смердюк', 'Кусака', 'Батяр'] };
const CALL = { 'Сивопер': 'Сивопере', 'Всезнайко': 'Всезнайку', 'Мудрагель': 'Мудрагелю', 'Пушок': 'Пушку', 'Шибеник': 'Шибенику', 'Смердюк': 'Смердюче', 'Кусака': 'Кусако', 'Батяр': 'Батяре' };
const P = [{ o: 'Костя', f: false }, { o: 'Валя', f: true }, { o: 'Оля', f: true }, { o: 'Тарас', f: false }];
function cross(kind, n) {
  const R = []; for (let i = 0; i < n; i++) { const k = Math.random() < .5 ? 'H' : 'M'; R.push(Object.assign({ k, nick: NN[k][i], t: 1 + Math.floor(Math.random() * 3) }, P[i])); }
  const ch = kind === 'fire' ? .75 : (kind === 'intro' || kind === 'pals') ? .65 : .55;
  for (let tries = 0; tries < 3; tries++) {
    const plan = [{ w: 0, to: 1, tag: `x_${kind}_open`, c: 1 }, { w: 1, to: 0, tag: `x_${kind}_reply`, c: 1 }];
    if (n >= 3) { plan.push({ w: 2, to: Math.random() < .5 ? 0 : 1, tag: `x_${kind}_join`, c: .85 }); if (n >= 4) plan.push({ w: 3, to: Math.random() < .5 ? 2 : (Math.random() < .5 ? 0 : 1), tag: `x_${kind}_join`, c: .75 }); plan.push({ w: Math.random() < .5 ? 0 : 1, to: -1, tag: 'x_join_reply', c: ch }); }
    plan.push({ w: 0, to: 1, tag: `x_${kind}_close`, c: ch });
    let thread = null, joined = -1, ok = true; const out = [];
    for (const st of plan) {
      const join = st.tag.endsWith('_join'), ans = st.tag === 'x_join_reply';
      if (ans && joined < 0) continue;
      const must = !join && ((out.length && out[out.length - 1].text.includes('?')) || (joined >= 0 && (ans || st.tag.endsWith('_close'))));
      if (!must && Math.random() > st.c) { if (join || ans) continue; break; }
      const to = ans ? joined : st.to; if (to === st.w) continue;
      const sp = R[st.w], ad = R[to];
      tier = sp.t; want = thread;
      const line = compose(sp.k, st.tag);
      if (line == null) { if (st.c >= 1) { ok = false; break; } continue; }
      if (next != null) thread = next; else if (thread == null && got != null) thread = got;
      out.push({ who: `${sp.nick} (${sp.k === 'H' ? 'Хугін' : 'Мунін'}, ${sp.o}) → ${ad.nick}`, text: fill(line, Object.assign({}, V, { name: sp.o, friend: ad.o, raven: ad.nick, addr: CALL[ad.nick], me: sp.nick, kin: ad.k === 'H' ? 'Хугін' : 'Мунін' }), sp.f, ad.f), thread });
      if (join) joined = st.w;
    }
    if (ok && out.length >= 2) return out;
  }
  return null;
}
module.exports = { pair, cross, scenes };
if (require.main === module) {
  const n = +process.argv[2] || 10, only = process.argv[3], show = process.argv.includes('-t');
  if (only === 'x') {
    for (let i = 0; i < n; i++) { const kind = pick(['intro', 'greet', 'rename', 'pals', 'fire', 'boss']), k = 2 + Math.floor(Math.random() * 3); const r = cross(kind, k);
      console.log(`## x_${kind} ×${k}`); if (!r) { console.log('(не склалося)\n'); continue; } for (const l of r) console.log(`${l.who}: ${l.text}${show ? '   [' + l.thread + ']' : ''}`); console.log(); }
  } else {
    const list = scenes.filter(s => !s.cross && (!only || only.split(',').includes(s.id)));
    for (let i = 0; i < n; i++) { const s = list[i % list.length]; const tH = 1 + Math.floor(Math.random() * 3), tM = 1 + Math.floor(Math.random() * 3); const r = pair(s, tH, tM, 0.55);
      console.log(`## [${s.id}] довіра Х${tH} М${tM}`); if (!r) { console.log('(не склалося)\n'); continue; } for (const l of r) console.log(`${l.who}: ${l.text}${show ? '   [' + l.thread + ']' : ''}`); console.log(); }
  }
}

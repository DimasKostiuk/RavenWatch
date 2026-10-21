// The ravens' talks hold together: every way a talk can go (the pair's scenes of RavenTalk, the players' ravens of
// CrossTalk), every speaker and every trust tier, Ukrainian (src/*Patterns.cs) and English (assets/lang/English).
//   node tools/talkcheck.js            - the problems; exit code 1 when there are errors (rw_build stops)
//   node tools/talkcheck.js matrix X   - scenes whose id starts with X (or x_<kind>): every opening, its answers, the
//                                        last words, as text to read (for the sense no machine can check)
// Rules (errors):
//   loose      a talk list mixing marked (^thread) and unmarked lines: an unmarked one is said on any thread
//   bare       a talk template with no marked list in it: its line carries no thread
//   dead       a required step finds nothing on the thread (the talk falls apart), or a question is left unanswered
//   shared     one list answers one thread for two scenes (one name, two meanings: «ситий» for the one fed and the one hungry)
//   parity     the English list differs from the Ukrainian one in length or in its marks
// Warnings: short (an optional step that never comes on that thread), orphan (an answer to a thread nobody opens).
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), src = path.join(root, 'src');
const files = ['Patterns.cs', 'TalkPatterns.cs', 'PerchPatterns.cs', 'CrossPatterns.cs', 'AboutPatterns.cs', 'StrikePatterns.cs', 'DeathPatterns.cs', 'EmotePatterns.cs', 'PassPatterns.cs', 'LifePatterns.cs', 'VarietyPatterns.cs', 'QuipPatterns.cs', 'PlacePatterns.cs', 'LifeTalkPatterns.cs', 'AgainPatterns.cs', 'WorldPatterns.cs', 'WardPatterns.cs', 'ErrandPatterns.cs', 'MorePatterns.cs', 'MomentPatterns.cs'];

function arrays(file, name) {
  const s = fs.readFileSync(path.join(src, file), 'utf8');
  const i = s.indexOf('string[] ' + name + ' =');
  if (i < 0) return [];
  const j = s.indexOf('\n        };', i);
  const out = [], re = /^\s*"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?$/gm, body = s.slice(i, j);
  let m;
  while ((m = re.exec(body))) out.push(JSON.parse('"' + m[1] + '"'));
  return out;
}
function book(lines) {
  const b = { t: {}, p: {} };
  let cur = null;
  for (const raw of lines) {
    const l = raw.trim();
    if (!l || l.startsWith('#')) continue;
    if (l.startsWith('[')) { const e = l.indexOf(']'); (b.t[l.slice(1, e)] = b.t[l.slice(1, e)] || []).push(l.slice(e + 1).trim()); cur = null; continue; }
    if (l.startsWith('@')) { cur = b.p[l.slice(1)] = b.p[l.slice(1)] || []; continue; }
    if (cur) cur.push(l);
  }
  return b;
}
const UK = { H: book([].concat(...files.map(f => arrays(f, 'Hugin')))), M: book([].concat(...files.map(f => arrays(f, 'Munin')))) };
const enDir = path.join(root, 'assets', 'lang', 'English');
const EN = fs.existsSync(enDir) ? {
  H: book(fs.readFileSync(path.join(enDir, 'hugin_patterns.txt'), 'utf8').split(/\r?\n/)),
  M: book(fs.readFileSync(path.join(enDir, 'munin_patterns.txt'), 'utf8').split(/\r?\n/)),
} : null;

// the scenes
const rt = fs.readFileSync(path.join(src, 'RavenTalk.cs'), 'utf8');
const d0 = rt.indexOf('string[] Defaults'), d1 = rt.indexOf('};', d0);
const scenes = [];
{
  let cur = null;
  for (const m of rt.slice(d0, d1).matchAll(/"((?:[^"\\]|\\.)*)"/g)) {
    const t = m[1];
    if (!t) continue;
    if (t.startsWith('[')) { cur = { id: t.slice(1, t.indexOf(']')), steps: [] }; scenes.push(cur); continue; }
    const [w, tag] = t.split(' ');
    cur.steps.push({ who: w[0], tag, opt: w.endsWith('?') });
  }
}
// the players' ravens: open (A), reply (B), joins (C, D), the answer to who joined (A or B), close (A)
const KINDS = ['intro', 'greet', 'rename', 'pals', 'fire', 'boss'];
for (const k of KINDS) for (const a of ['H', 'M']) for (const b of ['H', 'M']) for (const c of ['', 'H', 'M']) {
  const steps = [{ who: a, tag: 'x_' + k + '_open' }, { who: b, tag: 'x_' + k + '_reply' }];
  if (c) steps.push({ who: c, tag: 'x_' + k + '_join', join: true }, { who: a, tag: 'x_join_reply', opt: true, answer: true });
  steps.push({ who: a, tag: 'x_' + k + '_close', opt: true, close: true });
  scenes.push({ id: 'x_' + k + ':' + a + b + c, cross: true, steps });
}

// ---- the composer's rules, as sets
function tone(x, t) { const m = /^~(\d+) /.exec(x); if (!m) return x; return m[1].includes(String(t)) ? x.slice(m[0].length) : null; }
function mark(x) { if (!x.startsWith('^')) return [null, x]; const sp = x.indexOf(' '); return [x.slice(1, sp), x.slice(sp + 1)]; }
// the line ends on a question (RavenTalk.Asks): its last sentence mark is '?'
const asks = t => { const m = t.match(/[.!?…][^.!?…]*$/); return !!m && m[0][0] === '?'; };
const answersOf = mk => mk.split('>')[0].split('|');
// a thread topic.point is answered by a line marked with that point or with the bare topic (Patterns.Answers)
const answers = (mk, want) => answersOf(mk).some(a => a === want || (want.includes('.') && a === want.slice(0, want.indexOf('.'))));
const nextOf = mk => mk.includes('>') ? mk.slice(mk.indexOf('>') + 1) : null;
function listsIn(tpl) { return [...tpl.matchAll(/\{([^}]+)\}/g)].map(m => ({ name: m[1].replace(/\?$/, ''), opt: m[1].endsWith('?') })); }
function marked(B, list) { return (B.p[list] || []).some(l => /^(~\d+ )?\^/.test(l)); }
// the lines a step can say on this thread at this tier: [{text, list, mk, thread after}]
function options(B, who, tag, t, thread) {
  const out = [];
  for (const raw of B[who].t[tag] || []) {
    let tpl = tone(raw, t); if (tpl == null) continue;
    tpl = tpl.replace(/^%\d+ /, '');
    const main = listsIn(tpl).filter(x => B[who].p[x.name] && marked(B[who], x.name));
    if (!main.length) continue;
    const first = main[0];
    for (const l of B[who].p[first.name]) {
      const x = tone(l, t); if (x == null) continue;
      const [mk, text] = mark(x);
      if (mk == null) { if (thread == null) out.push({ text, list: first.name, mk: null, after: thread }); continue; }
      if (thread != null && !answers(mk, thread)) continue;
      out.push({ text, list: first.name, mk, after: nextOf(mk) || (thread == null ? answersOf(mk)[0] : thread) });
    }
  }
  return out;
}

const errors = [], warns = [];
const err = (kind, s) => errors.push(kind.padEnd(7) + s), warn = (kind, s) => warns.push(kind.padEnd(7) + s);

function lint(B, lang) {
  // the lists the talks use
  const talkTags = new Set();
  for (const s of scenes) for (const st of s.steps) talkTags.add(st.who + ':' + st.tag);
  const lists = new Set();
  for (const k of talkTags) {
    const [who, tag] = k.split(':');
    for (const raw of B[who].t[tag] || []) {
      const ls = listsIn(raw).filter(x => B[who].p[x.name]);
      if (!ls.some(x => marked(B[who], x.name))) err('bare', `${lang} ${who} [${tag}] ${raw}`);
      for (const x of ls) lists.add(who + ':' + x.name);
    }
  }
  for (const k of lists) {
    const [who, name] = k.split(':');
    const l = B[who].p[name];
    if (!marked(B[who], name)) continue; // a list of noise (an exclamation, a swear): fits any thread
    const loose = l.filter(x => !/^(~\d+ )?\^/.test(x));
    for (const x of loose) err('loose', `${lang} ${who} @${name}: ${x}`);
  }
  // every path
  const opened = {}; // thread -> scenes that open it
  const heard = {};  // thread|list -> scenes whose talk reaches that list on that thread
  for (const s of scenes) {
    for (let t = 1; t <= 3; t++) {
      const seen = new Set();
      // joined: someone joined in (cross talks): then the answer to them and the close are always said (CrossTalk.Make)
      (function walk(i, thread, lastQ, said, joined) {
        const key = i + '|' + thread + '|' + lastQ + '|' + joined;
        if (seen.has(key)) return; seen.add(key);
        if (i >= s.steps.length) return;
        const st0 = s.steps[i];
        if (st0.answer && !joined) { walk(i + 1, thread, lastQ, said, joined); return; } // nobody joined: nobody to answer
        const st = (st0.answer || st0.close) && joined ? Object.assign({}, st0, { opt: false }) : st0;
        const opts = options(B, st.who, st.tag, t, thread);
        const sid = s.cross ? s.id.split(':')[0] : s.id;
        if (i === 0) for (const o of opts) if (o.mk) (opened[answersOf(o.mk)[0]] = opened[answersOf(o.mk)[0]] || new Set()).add(sid);
        if (i > 0 && thread != null) for (const o of opts) { const k = thread + '|' + st.who + ':' + o.list; (heard[k] = heard[k] || new Set()).add(sid); }
        if (!opts.length) {
          if (!st.opt) err('dead', `${lang} ${s.id} ~${t} ${st.who} ${st.tag} on «${thread}» (after: ${said})`);
          else if (lastQ) err('dead', `${lang} ${s.id} ~${t} the question «${said}» left unanswered: ${st.who} ${st.tag} has nothing on «${thread}»`);
          else if (!st.answer && i > 0) warn('short', `${lang} ${s.id} ~${t} ${st.who} ${st.tag} never comes on «${thread}»`);
          if (st.join || (st.opt && s.cross)) walk(i + 1, thread, st.join ? lastQ : false, said, joined); // a step left out: a join that did not come (in a pair talk the first one left out ends it)
          return;
        }
        // a line with a question mark anywhere makes the next step come (RavenTalk.Asked, CrossTalk.Make)
        for (const o of opts) walk(i + 1, o.after, asks(o.text), o.text, joined || !!st.join);
        if ((st.opt && !lastQ && s.cross) || st.join) walk(i + 1, thread, st.join ? lastQ : false, said, joined);
      })(0, null, false, '', false);
    }
  }
  for (const k in heard) if (heard[k].size > 1) { const [th, l] = k.split('|'); err('shared', `${lang} «${th}» in ${l} answers ${[...heard[k]].join(', ')}`); }
  // answers nobody asks for
  for (const k of lists) {
    const [who, name] = k.split(':');
    for (const l of B[who].p[name]) {
      const [mk] = mark(l.replace(/^~\d+ /, '')); if (!mk) continue;
      if (!answersOf(mk).some(a => opened[a] || chainTargets.has(a) || Object.keys(opened).concat([...chainTargets]).some(o => o.startsWith(a + '.')))) warn('orphan', `${lang} ${who} @${name}: ${l}`);
    }
  }
}
// the threads a chain moves on to (^a>b: b is asked for even if no scene opens it)
const chainTargets = new Set();
for (const B of [UK, EN].filter(Boolean)) for (const who of ['H', 'M']) for (const n in B[who].p) for (const l of B[who].p[n]) { const [mk] = mark(l.replace(/^~\d+ /, '')); if (mk && nextOf(mk)) chainTargets.add(nextOf(mk)); }

function parity() {
  if (!EN) return;
  for (const who of ['H', 'M']) {
    const names = new Set();
    for (const s of scenes) for (const st of s.steps) if (st.who === who) for (const raw of UK[who].t[st.tag] || []) for (const x of listsIn(raw)) if (UK[who].p[x.name]) names.add(x.name);
    for (const n of names) {
      const u = UK[who].p[n], e = EN[who].p[n];
      if (!e) { err('parity', `EN ${who} @${n} missing`); continue; }
      if (u.length !== e.length) { err('parity', `EN ${who} @${n}: ${e.length} lines, uk ${u.length}`); continue; }
      const mk = l => { const x = l.replace(/^~\d+ /, ''); return (/^~\d+ /.exec(l) || [''])[0] + (mark(x)[0] || ''); };
      for (let i = 0; i < u.length; i++) if (mk(u[i]) !== mk(e[i])) err('parity', `EN ${who} @${n} #${i + 1}: «${mk(e[i])}» vs uk «${mk(u[i])}»: ${u[i]}`);
    }
  }
}

function matrix(prefix) {
  const B = UK, out = [];
  for (const s of scenes) {
    if (!s.id.startsWith(prefix)) continue;
    if (s.cross && !/:..$/.test(s.id)) continue; // the cross kinds: the two-raven ways are enough to read
    out.push(`\n=== ${s.id}: ${s.steps.map(st => st.who + (st.opt ? '?' : '') + ' ' + st.tag).join(' → ')}`);
    const st0 = s.steps[0];
    const byThread = {};
    for (let t = 1; t <= 3; t++) for (const o of options(B, st0.who, st0.tag, t, null)) {
      const th = o.mk ? answersOf(o.mk)[0] : '(без мітки)';
      (byThread[th] = byThread[th] || new Map()).set(o.text, o);
    }
    for (const th in byThread) {
      out.push(`\n--- ^${th}`);
      for (const [text, o] of byThread[th]) out.push(`  ${st0.who} ВІДКРИТТЯ ${o.mk && nextOf(o.mk) ? '[>' + nextOf(o.mk) + '] ' : ''}${text}`);
      // the next steps on this thread (and on what a chain moves on to)
      const follow = (i, thread, depth) => {
        if (i >= s.steps.length || depth > 3) return;
        const st = s.steps[i], seen = new Map();
        for (let t = 1; t <= 3; t++) for (const o of options(B, st.who, st.tag, t, thread)) seen.set(o.text, o);
        const pad = '  '.repeat(depth + 1);
        if (!seen.size) { out.push(`${pad}${st.who} ${st.tag}: (нічого)`); return; }
        const nexts = new Set();
        for (const [text, o] of seen) { out.push(`${pad}${st.who} ${st.tag.replace(/^dlg_|^x_/, '')} ${o.mk && nextOf(o.mk) ? '[>' + nextOf(o.mk) + '] ' : ''}${text}`); nexts.add(o.after); }
        for (const n of nexts) { if (n !== thread) out.push(`${pad}  >> ${n}`); follow(i + 1, n, depth + 1); if (nexts.size > 4) break; }
      };
      follow(1, th === '(без мітки)' ? null : th, 0);
    }
  }
  console.log(out.join('\n'));
}

module.exports = { UK, EN, scenes, options, listsIn, marked, tone, mark, answersOf, nextOf };
if (require.main !== module) return;
if (process.argv[2] === 'matrix') { matrix(process.argv[3] || ''); return; }
lint(UK, 'uk');
if (EN) lint(EN, 'en');
parity();
const verbose = process.argv.includes('-v');
const count = a => { const c = {}; for (const x of a) { const k = x.slice(0, 7).trim(); c[k] = (c[k] || 0) + 1; } return Object.entries(c).map(([k, v]) => k + ' ' + v).join(', '); };
for (const e of errors) console.log('ERROR ' + e);
if (verbose) for (const w of warns) console.log('warn  ' + w);
console.log(`talkcheck: ${errors.length} errors (${count(errors)}), ${warns.length} warnings (${count(warns)})${verbose ? '' : ' (-v shows them)'}`);
process.exitCode = errors.length ? 1 : 0;

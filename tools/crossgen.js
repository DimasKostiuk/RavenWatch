// Builds src/CrossPatterns.cs from tools/cross_phrases.json (the talks between players' ravens, and the nickname
// words) and tools/cross_chains.json (the chained talks). Edit the json, run: node tools/crossgen.js, then bump
// Patterns.Revision. `node tools/crossgen.js --old` prints the Grammar.Old pairs that turn the old templates in the
// players' phrase files into the new ones.
//
// Plain kinds (intro, greet, rename): one thread from the opener on, as before.
// Chained kinds (pals, fire, boss, from cross_chains.json): each branch is a little story, every step picks up what
// the one before said. A branch t = [t0, t1, t2, t3]:
//   open ^t0 · reply ^t0>t1 · join ^t1>t2 (the first who joins) / ^t2>t3 (the second) · answer to who joined ^t2 / ^t3
//   · last word ^t1 (nobody joined) / ^t2|t3. A join that does not come leaves the thread where it was, so every step
//   fits whatever came before it. Unmarked lines are the spare ones, said only when nothing on the thread fits.
// Intros and tails only on the opener (and only sometimes): in the middle of a talk they sounded like random noises.
const fs = require('fs'), path = require('path');
const d = JSON.parse(fs.readFileSync(path.join(__dirname, 'cross_phrases.json'), 'utf8'));
const ch = JSON.parse(fs.readFileSync(path.join(__dirname, 'cross_chains.json'), 'utf8'));
const PLAIN = { intro: 'знайомство', greet: 'зустріч', rename: 'нове_імя' };
const OLD = { pals: 'друзі', fire: 'вогонь', boss: 'бос' };   // the old lists of the chained kinds (left unused)
const CHAINED = { pals: ch.pals.code, fire: ch.fire.code, boss: ch.boss.code };
const ALL = Object.assign({}, PLAIN, CHAINED);

function templates(p, n) { // p: хх / мх, n: хн / мн
  const t = [];
  for (const [k, ua] of Object.entries(ALL)) {
    t.push(`[x_${k}_open] {${p}_${ua}}`);
    t.push(`[x_${k}_open] {${p}_вступ?} {${p}_${ua}}`);
    t.push(`[x_${k}_open] {${p}_${ua}} {${p}_хвіст?}`);
    t.push(`[x_${k}_reply] {${p}_${ua}_відповідь}`);
    t.push(`[x_${k}_close] {${p}_${ua}_кінець}`);
    t.push(`[x_${k}_join] {${p}_${ua}_встряє}`);
  }
  t.push(`[x_join_reply] {${p}_до_того_хто_встряв}`);
  t.push(`[nick_new] {${n}_заслужив}`);
  t.push(`[nick_change] {${n}_змінив}`);
  return t;
}

// the old templates (up to rev 36) -> the new ones, for Grammar.Old
function oldPairs(p) {
  const out = [];
  for (const [k, ua] of Object.entries(ALL)) {
    const was = OLD[k] || ua;
    out.push([`[x_${k}_open] {${p}_вступ?} {${p}_${was}} {${p}_хвіст?}`, `[x_${k}_open] {${p}_${ua}}`]);
    out.push([`[x_${k}_reply] {${p}_відгук?} {${p}_${was}_відповідь} {${p}_хвіст?}`, `[x_${k}_reply] {${p}_${ua}_відповідь}`]);
    if (OLD[k]) out.push([`[x_${k}_close] {${p}_${was}_кінець}`, `[x_${k}_close] {${p}_${ua}_кінець}`]);
    out.push([`[x_${k}_join] {${p}_${was}_встряє} {${p}_хвіст?}`, `[x_${k}_join] {${p}_${ua}_встряє}`]);
    out.push([`[x_${k}_join] {${p}_встряє}`, `[x_${k}_join] {${p}_${ua}_встряє}`]);
  }
  out.push([`[x_join_reply] {${p}_відгук?} {${p}_до_того_хто_встряв}`, `[x_join_reply] {${p}_до_того_хто_встряв}`]);
  return out;
}

// a tone mark stays in front of the thread mark: "~3 ^t text"
function mark(line, m) { const t = /^(~\d+ )(.*)$/.exec(line); return t ? t[1] + '^' + m + ' ' + t[2] : '^' + m + ' ' + line; }

function chainLists(r, p) {
  const k = r === 'hugin' ? 'h' : 'm', lists = {}, order = [];
  const add = (name, lines) => { if (!lists[name]) { lists[name] = []; order.push(name); } lists[name].push(...lines); };
  const answers = [];
  for (const kind of Object.keys(CHAINED)) {
    const c = ch[kind], base = p + '_' + c.code;
    const open = [], reply = [], join = [], close = [];
    for (const b of c.branches) {
      const [t0, t1, t2, t3] = b.t;
      for (const l of b.open[k]) open.push(mark(l, t0));
      for (const l of b.reply[k]) reply.push(mark(l, t0 + '>' + t1));
      for (const l of b.join1[k]) join.push(mark(l, t1 + '>' + t2));
      for (const l of b.join2[k]) join.push(mark(l, t2 + '>' + t3));
      for (const l of b.jr2[k]) answers.push(mark(l, t2));
      for (const l of b.jr3[k]) answers.push(mark(l, t3));
      for (const l of b.close1[k]) close.push(mark(l, t1));
      for (const l of b.close23[k]) close.push(mark(l, t2 + '|' + t3));
    }
    const keep = c.keep && c.keep[k];
    if (keep) { open.push(...(keep.open || [])); reply.push(...(keep.reply || [])); join.push(...(keep.join || [])); close.push(...(keep.close || [])); }
    const loose = c.loose || {};
    if (loose.reply) reply.push(...loose.reply[k]);
    if (loose.join) join.push(...loose.join[k]);
    if (loose.close) close.push(...loose.close[k]);
    add(base, open); add(base + '_відповідь', reply); add(base + '_встряє', join); add(base + '_кінець', close);
  }
  return { lists, order, answers };
}

function block(r, p, n) {
  const out = templates(p, n).concat(['']);
  const c = chainLists(r, p);
  const generic = d[r].lists[p + '_встряє'] || []; // a chime-in that fits anything: now only the spare of each kind's list
  const skip = new Set([p + '_встряє']);
  for (const ua of Object.values(OLD)) for (const s of ['', '_відповідь', '_встряє', '_кінець']) skip.add(p + '_' + ua + s);
  for (const list of d[r].order) {
    if (skip.has(list)) continue;
    let lines = d[r].lists[list].slice();
    if (list === p + '_до_того_хто_встряв') lines = c.answers.concat(lines);              // the chained answers first
    if (/_встряє$/.test(list)) for (const g of generic) if (!lines.includes(g)) lines.push(g); // spare chime-ins
    out.push('@' + list); for (const l of lines) out.push(l); out.push('');
  }
  for (const list of c.order) {
    const lines = c.lists[list].slice();
    if (/_встряє$/.test(list)) for (const g of generic) if (!lines.includes(g)) lines.push(g);
    out.push('@' + list); for (const l of lines) out.push(l); out.push('');
  }
  return out.map(l => '            "' + l.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '",').join('\n');
}

if (process.argv.includes('--old')) {
  const esc = s => s.replace(/\\/g, '\\\\').replace(/"/g, '\\"');
  for (const p of ['хх', 'мх']) for (const [a, b] of oldPairs(p)) console.log(`            { "${esc(a)}", "${esc(b)}" }, // 1.7.0: the players' ravens' talks: chained, no noises mid-talk`);
  process.exit(0);
}

const cs = `#if RAVEN_ITEM
namespace HuginWhispers
{
    // The ravens of different players talking with each other (CrossTalk), and the nickname words (Nicks).
    // GENERATED by tools/crossgen.js from tools/cross_phrases.json and tools/cross_chains.json — edit there and run it again.
    // Each line is said by a raven to another player's raven, in the tone of its own trust (~1 levels 1-3, ~2 levels
    // 4-5, ~3 levels 6-7):
    //   {name}   the speaker's own player; their forms {він|вона}
    //   {friend} the player of the raven it speaks to; their forms {>він|вона}
    //   {raven}  that raven's nickname; {addr} how it is called («Кривдзьобе»); {kin} its kind (Хугін / Мунін)
    //   {me}     the speaker's own nickname
    // Threads (^mark) tie an answer to what it answers; ^this>next also moves the talk on (a chain: pals, fire, boss),
    // so whoever speaks next picks up what was just said. Unmarked lines are the spare ones. Intros and tails: on the
    // opener only, and only sometimes.
    // Kinds: intro, greet, pals, boss, fire, rename; x_<kind>_join: a third (fourth) raven joins in on the thread,
    // x_join_reply: one of the first two answers it. nick_new / nick_change: {nick}.
    // Added to hugin_patterns.txt / munin_patterns.txt once (Patterns.Revision).
    internal static class CrossPatterns
    {
        internal static readonly string[] Hugin =
        {
${block('hugin', 'хх', 'хн')}
        };

        internal static readonly string[] Munin =
        {
${block('munin', 'мх', 'мн')}
        };
    }
}
#endif
`;
fs.writeFileSync(path.join(__dirname, '..', 'src', 'CrossPatterns.cs'), cs.replace(/\n/g, '\r\n'));
console.log('CrossPatterns.cs written');

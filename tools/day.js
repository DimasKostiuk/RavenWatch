// A story person's day read with no game (the user 2026-11-22: the days of act 1 looked over, later acts add conditions): which lines of
// its day (assets/story/people: «at:», «spot:» — StoryLife) act at a stage of the story, hour by hour, and whether the quest holds it then.
//   node tools/day.js                         everyone with a day, the story over (every stage reached, none on)
//   node tools/day.js <who>                   one person
//   node tools/day.js <who> <quest> <stage>   …while that stage of that quest is on (as «raven story jump <quest> <stage>» would set the world)
// It reads the same words as the game (StoryLife.Read, StoryPeople.Parse) but judges the world roughly, and says where it does not know:
//   reached / step    by the files' order: the prologue, ch1, ch2 …, a quest's stages as written (a fork's side way counts as reached). A quest that
//                     runs beside another («starts: reached <quest> <stage>»: Eyvind's mushrooms beside the chapter) is neither before nor after it:
//                     once it may have begun, where it stands is not known — asked by its own name it is, and then the other one is known only up to that stage
//   state <place>     by the place's «becomes:» lines (blueprints/locations.list.txt), a hearth taken as lit
//   world <fact>      true once a stage at or before this one says «world <fact>» (its «start:»; «done:» and its scene's «do» — from the next stage on)
//   phase <name>      any «when:» line of its block
//   night, day, weather, anything else: not known — the line is shown with «?»
// What the game alone knows is not here: whether a place stands in the world, how far it is (a way out and back), what the day's dice gave.
// The live answer: «raven npc day <who>» in the game.
const fs = require('fs'), path = require('path');
const story = path.join(__dirname, '..', 'assets', 'story');
const clean = l => l.replace(/\s+#.*$/, '').replace(/^#.*$/, '').trim(); // (as the game cuts a note: after a space)
const files = dir => fs.existsSync(path.join(story, dir)) ? fs.readdirSync(path.join(story, dir)).filter(f => f.endsWith('.txt') && !f.startsWith('_')) : [];
const linesOf = (dir, f) => fs.readFileSync(path.join(story, dir, f), 'utf8').split(/\r?\n/).map(clean).filter(Boolean);
const [who, quest, stage] = process.argv.slice(2).filter(a => !a.startsWith('--'));

// ---------------------------------------------------------------- the story's order: quests by file (prologue, ch1, ch2 …), stages as written
const blockOf = f => { const m = /^ch(\d+)/.exec(f); return m ? +m[1] : 0; };
const quests = []; // { id, stages: [{ id, start, done, scene }] }
for (const f of files('quests').sort((a, b) => blockOf(a) - blockOf(b) || a.localeCompare(b))) {
  let q = null, st = null;
  for (const l of linesOf('quests', f)) {
    let m = /^\[quest\s+(\S+)\]/.exec(l);
    if (m) { q = { id: m[1], stages: [] }; quests.push(q); st = null; continue; }
    m = /^\[stage\s+(\S+)\]/.exec(l);
    if (m && q) { st = { id: m[1], start: '', done: '', scene: '' }; q.stages.push(st); continue; }
    m = /^starts:\s*reached\s+(\S+)\s+(\S+)/.exec(l);
    if (m && q && !st) { q.beside = [m[1], m[2]]; continue; } // (it begins at that stage of another quest and runs beside it)
    if (!st) continue;
    m = /^(start|done|scene):\s*(.*)$/.exec(l);
    if (m) st[m[1]] += (st[m[1]] ? '; ' : '') + m[2];
  }
}
const sceneDo = {}; // a scene → its «do …» lines joined (what it leaves in the world)
for (const f of files('scenes')) {
  let sc = null;
  for (const l of linesOf('scenes', f)) {
    const m = /^\[scene\s+(\S+)\]/.exec(l);
    if (m) { sc = m[1]; sceneDo[sc] = ''; continue; }
    const d = /(?:^|\s)do\s+(.*)$/.exec(l);
    if (sc && d) sceneDo[sc] += '; ' + d[1];
  }
}
// where the story stands: [quest index, stage index]; none given — past everything
const qi = quest ? quests.findIndex(q => q.id === quest) : quests.length;
if (quest && qi < 0) { console.error('нема квесту «' + quest + '» (є: ' + quests.map(q => q.id).join(', ') + ')'); process.exit(2); }
const si = quest ? quests[qi].stages.findIndex(s => s.id === stage) : -1;
if (quest && si < 0) { console.error('нема етапу «' + stage + '» у квесті ' + quest); process.exit(2); }
// a stage's place in the order: -1 before now, 0 now, 1 after, null — not known (a quest beside the one asked)
const mainRel = (a, b) => a < qi ? -1 : a > qi ? 1 : b < si ? -1 : b > si ? 1 : 0;
const asked = quest ? quests[qi] : null;
const rel = (a, b) => {
  if (!asked) return -1;                    // (the story over: all of it behind)
  if (a === qi) return b < si ? -1 : b > si ? 1 : 0;
  const x = quests[a];
  if (asked.beside) {                       // (the one asked runs beside another: that one is known up to the stage this began at)
    const ba = quests.findIndex(y => y.id === asked.beside[0]), bb = ba >= 0 ? quests[ba].stages.findIndex(y => y.id === asked.beside[1]) : -1;
    if (x.beside || ba < 0) return null;
    return a < ba ? -1 : a === ba && b <= bb ? -1 : null;
  }
  if (x.beside) {                           // (one that runs beside: not begun before its stage; after — somewhere, or over)
    const ba = quests.findIndex(y => y.id === x.beside[0]), bb = ba >= 0 ? quests[ba].stages.findIndex(y => y.id === x.beside[1]) : -1;
    if (ba < 0 || bb < 0) return null;
    return mainRel(ba, bb) > 0 ? 1 : null;
  }
  return mainRel(a, b);
};
const whenIs = (q, s) => { const a = quests.findIndex(x => x.id === q); if (a < 0) return null; const b = quests[a].stages.findIndex(x => x.id === s); return b < 0 ? null : rel(a, b); };
const unknown = quests.filter((x, a) => x.stages.length > 0 && rel(a, 0) === null || x.stages.some((_, b) => rel(a, b) === null)).map(x => x.id); // (said in the head of the answer)
// every stage in order with what it leaves: «start:» as the stage begins, «done:» and its scene's «do» as it ends (a stage not known: nothing of it)
const passed = []; // the actions done by now, in order
quests.forEach((q, a) => q.stages.forEach((s, b) => {
  const r = rel(a, b);
  if (r === null) return;
  if (r <= 0) passed.push(...s.start.split(';'));
  if (r < 0) passed.push(...s.done.split(';'), ...(sceneDo[s.scene.split(/\s+/)[0]] || '').split(';'));
}));
const acts = passed.map(a => a.trim().split(/\s+/)).filter(w => w[0]);
const facts = new Set(); // the world's facts that stand now («world x»; «unworld x» takes one off)
const everSet = new Set(quests.flatMap(q => q.stages.flatMap(s => (s.start + ';' + s.done + ';' + (sceneDo[s.scene.split(/\s+/)[0]] || '')).split(';').map(a => a.trim().split(/\s+/)).filter(w => w[0] === 'world' && w[1]).map(w => w[1]))));
for (const w of acts) { if (w[0] === 'world' && w[1]) facts.add(w[1]); if (w[0] === 'unworld' && w[1]) facts.delete(w[1]); }
const holds = {}; // a person → the quest's word that holds it now («npc <who> hold …»; «free» takes it off)
for (const w of acts) if (w[0] === 'npc' && w[2] === 'hold') holds[w[1]] = w.slice(3).join(' '); else if (w[0] === 'npc' && w[2] === 'free') delete holds[w[1]];

// ---------------------------------------------------------------- the places' states («becomes: <state> <world conditions>»)
const becomes = {}; // place → [[state, conditions]]
{
  let loc = null;
  for (const l of linesOf('blueprints', 'locations.list.txt')) {
    let m = /^loc\s+(\S+)/.exec(l);
    if (m) { loc = m[1]; becomes[loc] = []; continue; }
    m = /^becomes:\s*(\S+)\s+(.*)$/.exec(l);
    if (m && loc) becomes[loc].push([parseFloat(m[1]), m[2]]);
  }
}
// a place named by a word of the people's files («hall.x») may be another id in the list («zala»): the list's «loc <id> … as <word>» is not
// read here — a state is asked by the list's own id («state zala >= 1.3»)

// ---------------------------------------------------------------- conditions: true, false, null (not known here)
const phases = {}, ones = {}, people = [];
const cmp = (have, w, at) => { let op = '>=', t = w[at] || ''; for (const o of ['>=', '<=', '>', '<', '=']) if (t.startsWith(o)) { op = o; t = t.slice(o.length); break; } if (!t) t = w[at + 1] || ''; const n = parseFloat(t); return op === '>' ? have > n : op === '<' ? have < n : op === '<=' ? have <= n : op === '=' ? have === n : have >= n; };
let deep = 0;
const one = text => {
  let t = text.trim(), not = false;
  if (t.startsWith('!')) { not = true; t = t.slice(1).trim(); }
  const w = t.split(/\s+/);
  let v = null;
  if (w[0] === 'reached') { const r = whenIs(w[1], w[2]); v = r === null ? null : r <= 0; }
  else if (w[0] === 'step') { const r = whenIs(w[1], w[2]); v = r === null ? null : r === 0; }
  else if (w[0] === 'world') v = facts.has(w[1]) ? true : everSet.has(w[1]) ? false : null; // (a fact no stage sets: the game's own, or a module's)
  else if (w[0] === 'hearth') v = true; // (taken as lit)
  else if (w[0] === 'state' && becomes[w[1]]) {
    let lo = 0, hi = 0; // (the state with only what is known to hold · with what may hold too)
    for (const [s, c] of becomes[w[1]]) { const r = all(c); if (r === true) { lo = Math.max(lo, s); hi = Math.max(hi, s); } else if (r === null) hi = Math.max(hi, s); }
    const a = cmp(lo, w, 2), b = cmp(hi, w, 2);
    v = a === b ? a : null;
  }
  else if (w[0] === 'phase' && phases[w[1]] && deep < 4) { deep++; const rs = phases[w[1]].map(all); deep--; v = rs.includes(true) ? true : rs.includes(null) ? null : false; }
  return v === null ? null : not ? !v : v;
};
function all(text) { const rs = (text || '').split(',').filter(x => x.trim()).map(one); return rs.includes(false) ? false : rs.includes(null) ? null : true; }

// ---------------------------------------------------------------- the people's files
for (const f of files('people')) {
  let cur = null, ph = null, on = null, doings = {};
  for (const l of linesOf('people', f)) {
    let m = /^\[(\w+)\s+(\S+)\]/.exec(l);
    if (m) { cur = ph = on = null; if (m[1] === 'person') { cur = { id: m[2], lives: [], days: [], lines: [], after: '' }; people.push(cur); doings = {}; } else if (m[1] === 'phase') ph = phases[m[2]] = phases[m[2]] || []; else if (m[1] === 'one') on = ones[m[2]] = ones[m[2]] || []; continue; }
    const c = l.indexOf(':'); if (c <= 0) continue;
    const k = l.slice(0, c).trim(); let v = l.slice(c + 1).trim();
    if (ph) { if (k === 'when') ph.push(v); continue; }
    if (on) { if (k === 'place') { const wi = v.indexOf(' when '); on.push([wi >= 0 ? v.slice(0, wi).trim() : v, wi >= 0 ? v.slice(wi + 6) : '']); } continue; }
    if (!cur) continue;
    const wi = v.indexOf(' when '), when = wi >= 0 ? v.slice(wi + 6).trim() : '', head = wi >= 0 ? v.slice(0, wi) : v;
    if (k === 'do') { const d = /^(\w+)\s*=\s*(.*)$/.exec(v); if (d) doings[d[1]] = d[2]; }
    else if (k === 'after') cur.after = v;
    else if (k === 'live') cur.lives.push([head.trim().split(/\s+/)[0], when]);
    else if (k === 'day') cur.days.push([head.trim(), when]);
    else if (k === 'at' || k === 'spot') {
      const w = head.trim().split(/\s+/), at = k === 'at';
      const L = { at, set: w[0], hours: at ? w[1] : '', place: w[at ? 2 : 1], when, doing: '', words: [] };
      let i = at ? 3 : 2;
      if (w[i] === 'by') i += 2;
      if (doings[w[i]]) { L.doing = w[i]; w.splice(i, 1, ...doings[w[i]].split(/\s+/)); }
      L.words = w.slice(i);
      const val = x => { const j = L.words.indexOf(x); return j >= 0 ? L.words[j + 1] : ''; };
      if (!at && val('hours')) L.hours = val('hours');
      Object.assign(L, { kind: L.doing || L.words[0], sit: L.words.includes('sit'), clip: val('clip'), chance: val('chance'), days: val('days'), every: val('every'), daily: L.words.includes('daily'), group: val('group'), meet: val('meet'), one: val('one'), over: L.words.includes('over'), say: val('say') });
      cur.lines.push(L);
    }
  }
}

// ---------------------------------------------------------------- telling
const mark = r => r === true ? '✓' : r === false ? '·' : '?';
const inHours = (L, h) => { if (!L.hours) return true; const [a, b] = L.hours.split('-').map(Number); return a <= b ? h >= a && h < b : h >= a || h < b; };
const tell = x => {
  const out = [];
  const born = all(x.after);
  const home = x.lives.find(l => all(l[1]) !== false);
  const sure = x.days.find(d => all(d[1]) === true), maybeSet = sure ? null : x.days.find(d => all(d[1]) === null);
  const set = (sure || maybeSet || ['home'])[0]; // (none known to hold but one may: that one's lines are shown, said so)
  out.push('── ' + x.id + (quest ? '   (' + quest + ' ' + stage + ')' : '   (сюжет пройдено)'));
  if (born === false) { out.push('   ще не у світі (after: ' + x.after + ')'); return out; }
  out.push('   дім: ' + (home ? home[0] + (all(home[1]) === null ? ' ?' : '') : '—') + '   набір дня: ' + set + (maybeSet ? '   (не певно: ' + maybeSet[1] + ' — тут не відомо; інакше «home»)' : ''));
  if (home && home[0] === 'away') out.push('   ПОЗА СВІТОМ на цьому етапі (live: away)');
  if (holds[x.id]) out.push('   КВЕСТ ТРИМАЄ: npc ' + x.id + ' hold ' + holds[x.id] + '   — день нижче не діє, поки квест не скаже free');
  const mine = x.lines.filter(l => l.set === set);
  if (mine.length === 0) { out.push('   свого дня в цьому наборі нема: місця бази, або ним веде квест'); return out; }
  const live = mine.map(l => ({ l, r: all(l.when) }));
  const overOn = live.filter(o => o.l.over && o.r !== false);
  const start = l => l.hours ? +l.hours.split('-')[0] : 99;
  for (const o of live.slice().sort((a, b) => (b.l.at - a.l.at) || start(a.l) - start(b.l))) {
    const l = o.l, notes = [];
    if (l.chance) notes.push('іноді ' + Math.round(+l.chance * 100) + '% днів');
    else if (l.days) notes.push('раз на ' + l.days + ' дні');
    else if (l.one) notes.push('одне з місць дня «' + l.one + '»: ' + (ones[l.one] || []).map(p => p[0] + (p[1] ? (all(p[1]) === false ? ' (зараз нема)' : all(p[1]) === null ? ' (?)' : '') : '')).join(' / '));
    else if (l.at && !l.daily) notes.push('раз на ' + (l.every || '1-3') + ' дні' + (l.group ? ', разом з «' + l.group + '»' : l.meet ? ', разом зі зборами' : ''));
    else if (l.daily) notes.push('щодня');
    if (l.over) notes.push('OVER: решта рядків цих годин мовчить');
    if (l.meet) notes.push('збори «' + l.meet + '»');
    if (l.say) notes.push('каже «' + l.say + '»');
    { const j = l.words.indexOf('haul'); if (j >= 0) notes.push('несе дошку від ' + l.words[j + 1]); }
    let hid = '';
    if (o.r !== false && !l.over && l.at) { const h = overOn.find(v => { for (let t = 0.5; t < 24; t++) if (inHours(l, t) && inHours(v.l, t)) return true; return false; }); if (h) hid = '   ← у день рядка OVER (' + h.l.hours + ' ' + h.l.place + ') мовчить'; }
    out.push('   ' + mark(o.r) + ' ' + (l.at ? 'at  ' : 'spot') + ' ' + (l.hours || 'весь день').padEnd(9) + ' ' + l.place.padEnd(22) + ' ' + (l.kind + (l.sit ? ' sit' : '')).padEnd(12) + (notes.length ? '  ' + notes.join('; ') : '') + (o.r === false ? '   (не тепер: when ' + l.when + ')' : o.r === null ? '   (тут не відомо: when ' + l.when + ')' : '') + hid);
  }
  // the hours no «at:» line may take at this stage (an outing aside): its free time, the «spot:» places
  const free = [];
  for (let h = 0; h < 24; h++) if (!live.some(o => o.l.at && o.r !== false && !o.l.chance && inHours(o.l, h))) free.push(h);
  const runs = []; for (const h of free) { const last = runs[runs.length - 1]; if (last && last[1] === h) last[1] = h + 1; else runs.push([h, h + 1]); }
  out.push('   години без жодного at: (вільний час, місця spot): ' + (runs.length ? runs.map(r => r[0] + '–' + r[1]).join(', ') : 'нема'));
  return out;
};
const list = who ? people.filter(x => x.id === who) : people.filter(x => x.lines.length > 0);
if (who && list.length === 0) { console.error('нема людини «' + who + '» (є: ' + people.map(x => x.id).join(', ') + ')'); process.exit(2); }
console.log('✓ діє на цьому етапі   · не тепер   ? тут не відомо (гра знає: raven npc day <хто>)');
if (unknown.length) console.log('квест поруч, де він зараз — тут не відомо: ' + unknown.join(', ') + ' (його етапи в умовах — «?», його hold/free не враховано; спитай його самого: node tools/day.js <хто> <цей квест> <етап>)');
for (const x of list) console.log(tell(x).join('\n'));

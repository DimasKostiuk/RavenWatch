// A quest walked dry, on node, before the game: stage after stage, what each waits for, who makes that true, and where a player would
// stand for ever. storycheck holds the words together; this walks the way (docs/jobs/story-data.md: «пройди в голові кожен новий етап
// удвох і з перезаходом» — done here, for every stage, each time).
//   node tools/quest_dry.js              every quest of assets/story/quests
//   node tools/quest_dry.js <quest>      one
//   --strict                             exit code 1 when a STUCK is found (not in the build: the walk judges, the user decides)
// What a condition stands on and what an action leaves is said by the kinds themselves, in the code: the «Dry» line of each
// (StoryCond.Kind.Dry, StoryActions.Kind.Dry — their grammar is there). A kind with none is NOT JUDGED and said so; nothing passes in silence.
//
// The walk is the game's own order (StoryQuests.Enter0, Step): on entering «start:», then the forks (a server quest's own first), then
// the stage waits — its ways, or its scene, or «on:» with «wait:», or «wait:» — then the scene's «do» lines, «done:», and on to «next:».
// A fork that stands on what the player may or may not do (free conditions) is walked both ways.
// Two worlds are walked:
//   fresh   a new world and a new character: only what the walk itself makes stands
//   late    a new character in a world whose story is over: every fact of the world the first walk made, or that the game makes by itself,
//           already stands (the second player; the one who comes to the server later; an old world). Said only where it differs.
// Per stage it says:
//   STUCK   nothing can make what it waits for: no action of the data, no part of the game; a thing to touch that is not lit or is hidden
//           on that stage; a reply of a talk closed on it; a person not yet in the world; a place of the player's own never given
//   LEFT    «start:» gave a person «follow» or «stay» and a fork leaves at once to a stage that does not take the word off
//   RELOG   the stage passes by an event alone: one that comes while the player is elsewhere or out of the game is lost (said with
//           whether it can come again)
// What it cannot know: how far anything is and whether a person gets there; time; numbers (a number once set is taken to be enough);
// the inside of a scene beyond its «needs:» and «do» lines; two players in the same pass (the server's claims); whether a talk's reply
// was already used up.
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), story = path.join(root, 'assets', 'story'), srcDir = path.join(root, 'src');
const args = process.argv.slice(2), strict = args.includes('--strict'), only = args.find(a => !a.startsWith('--'));
const clean = l => l.replace(/\s+#.*$/, '').replace(/^#.*$/, '').trim(); // (as the game cuts a note: after a space)
const files = dir => fs.existsSync(path.join(story, dir)) ? fs.readdirSync(path.join(story, dir)).filter(f => f.endsWith('.txt') && !f.startsWith('_')) : [];
const linesOf = (dir, f) => fs.readFileSync(path.join(story, dir, f), 'utf8').split(/\r?\n/).map(clean);
const splitConds = t => (t || '').split(',').map(x => x.trim()).filter(Boolean);
const splitActs = t => (t || '').split(';').map(x => x.trim()).filter(Boolean);

// ---------------------------------------------------------------- what the code says of its kinds
const condDry = {}, actDry = {}, raisedByCode = {}, madeByCode = {};
for (const f of fs.readdirSync(srcDir).filter(x => x.endsWith('.cs'))) {
  const s = fs.readFileSync(path.join(srcDir, f), 'utf8');
  for (const m of s.matchAll(/"(\w+)",\s*new (StoryCond\.|StoryActions\.)?Kind\s*\{\s*Dry = "([^"]*)"/g)) {
    const into = m[2] === 'StoryCond.' || (!m[2] && f === 'StoryCond.cs') ? condDry : m[2] === 'StoryActions.' || (!m[2] && f === 'StoryActions.cs') ? actDry : null;
    if (into) into[m[1]] = m[3];
  }
  for (const m of s.matchAll(/StoryBus\.Raise\("(\w+)"/g)) raisedByCode[m[1]] = f;
  // a word of the world the game sets by itself: StoryWorld.SetFlag(<a const of this file>, true)
  const consts = {};
  for (const m of s.matchAll(/const string ([^;]+);/g)) for (const c of m[1].matchAll(/(\w+)\s*=\s*"(\w+)"/g)) consts[c[1]] = c[2];
  for (const m of s.matchAll(/StoryWorld\.SetFlag\((\w+|"\w+"),\s*true\)/g)) { const name = m[1].startsWith('"') ? m[1].slice(1, -1) : consts[m[1]]; if (name) madeByCode['world:' + name] = f; }
}
if (Object.keys(condDry).length < 10 || Object.keys(actDry).length < 10) { console.log('QUEST tools/quest_dry.js: found ' + Object.keys(condDry).length + ' condition and ' + Object.keys(actDry).length + ' action kinds with a «Dry» line in src: its pattern is out of date, nothing can be judged'); process.exit(1); }

// ---------------------------------------------------------------- the data
// places: their other names, what is touched, hidden, done on a touch nobody waits for, when the hearth may not be lit
const alias = {}, places = {};
{
  let at = null;
  for (const l of linesOf('blueprints', 'locations.list.txt')) {
    let m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = places[m[1]] = { id: m[1], touch: {}, hide: {}, elsedo: {}, hearthwait: null }; continue; }
    if (!at) continue;
    if ((m = /^alias:\s*(.+)$/.exec(l))) { for (const a of m[1].split(/\s+/)) alias[a] = at.id; continue; }
    if ((m = /^touch:\s*(\S+)\s+(.*)$/.exec(l))) { const w = m[2].replace(/^(?:(?:anim|quiet)\s+)+/, '').trim(); at.touch[m[1]] = w === '-' ? [] : splitConds(w); continue; }
    if ((m = /^hidewhile:\s*(\S+)\s+(.*)$/.exec(l))) { (at.hide[m[1]] = at.hide[m[1]] || []).push(splitConds(m[2])); continue; }
    if ((m = /^elsedo:\s*(\S+)\s+(.*)$/.exec(l))) { at.elsedo[m[1]] = splitActs(m[2]); continue; }
    if ((m = /^hearthwait:\s*(.*?)\s*::/.exec(l))) { at.hearthwait = splitConds(m[1]); continue; }
  }
}
const placeOf = w => { const dot = w.indexOf('.'), head = dot < 0 ? w : w.slice(0, dot); return (alias[head] || head) + (dot < 0 ? '' : w.slice(dot)); }; // (hall.x → zala.x)

// quests
const quests = [];
for (const f of files('quests')) {
  let q = null, s = null, w = null;
  linesOf('quests', f).forEach((l, i) => {
    if (!l) return;
    let m = /^\[(quest|stage|way)\s+(\S+)\]$/.exec(l);
    if (m) {
      if (m[1] === 'quest') { q = { id: m[2], file: 'quests/' + f, server: false, starts: [], who: [], forks: [], stages: [], first: '' }; quests.push(q); s = w = null; }
      else if (m[1] === 'stage' && q) { s = { id: m[2], line: i + 1, forks: [], start: [], done: [], wait: [], on: '', scene: '', next: '', ways: [], saga: '', choice: '' }; q.stages.push(s); w = null; }
      else if (m[1] === 'way' && s) { w = { id: m[2], line: i + 1, forks: [], start: [], done: [], wait: [], on: '', scene: '', next: '', ways: [], saga: '' }; s.ways.push(w); }
      return;
    }
    const c = l.indexOf(':');
    if (c <= 0 || !q) return;
    const key = l.slice(0, c).trim().toLowerCase(), val = l.slice(c + 1).trim();
    const fork = () => { const a = val.lastIndexOf('->'); return a < 0 ? null : { when: splitConds(val.slice(0, a)), to: val.slice(a + 2).trim(), text: val }; };
    if (!s) {
      if (key === 'owner') q.server = val === 'server';
      else if (key === 'starts') q.starts = splitConds(val);
      else if (key === 'who') q.who = splitConds(val);
      else if (key === 'first') q.first = val;
      else if (key === 'fork') { const k = fork(); if (k) q.forks.push(k); }
      return;
    }
    const d = w || s;
    if (key === 'wait') d.wait = splitConds(val);
    else if (key === 'on') d.on = val;
    else if (key === 'scene') d.scene = val;
    else if (key === 'next') d.next = val;
    else if (key === 'saga') d.saga = val;
    else if (key === 'choice') s.choice = val;
    else if (key === 'start') d.start.push(...splitActs(val));
    else if (key === 'done') d.done.push(...splitActs(val));
    else if (key === 'fork') { const k = fork(); if (k) d.forks.push(k); }
  });
}
for (const q of quests) {
  q.stages.forEach((s, i) => { if (!s.next) s.next = i + 1 < q.stages.length ? q.stages[i + 1].id : 'end'; for (const w of s.ways) if (!w.next) w.next = s.next; }); // (StoryQuestsRead: none = the next one in the file)
  if (!q.first && q.stages.length) q.first = q.stages[0].id;
  q.stage = id => q.stages.find(s => s.id === id);
}

// scenes: what each needs and does
const scenes = {};
for (const f of files('scenes')) {
  let sc = null;
  for (const l of linesOf('scenes', f)) {
    let m = /^\[scene\s+(\S+)\]$/.exec(l);
    if (m) { sc = scenes[m[1]] = { id: m[1], needs: [], does: [] }; continue; }
    if (!sc) continue;
    if ((m = /^needs:\s*(.*)$/.exec(l))) sc.needs = splitConds(m[1]);
    else if ((m = /^(?:if\s+.*?\s+)?do\s+(.+)$/.exec(l))) sc.does.push(m[1].trim());
  }
}

// people: until when one is not in the world
const people = {};
for (const f of files('people')) {
  let id = null;
  for (const l of linesOf('people', f)) {
    let m = /^\[person\s+(\S+)\]$/.exec(l);
    if (m) { id = m[1]; people[id] = { after: [] }; continue; }
    if (id && (m = /^after:\s*(.*)$/.exec(l))) people[id].after = splitConds(m[1]);
  }
}

// talks: the events their replies raise, each with the gates on the way to it from the talk's first page
const talkEvents = {}; // event → [{ file, line, gates: [[conds] …] one list a way }]
for (const f of files('talk')) {
  const who = f.replace(/\.txt$/, '');
  const pages = {}; let page = null, first = null, gate = null; // (a «??» line gates every reply under it, up to the next «??» or page)
  linesOf('talk', f).forEach((l, i) => {
    let m;
    if ((m = /^@(\S+)/.exec(l))) { page = m[1]; first = first || page; pages[page] = pages[page] || []; gate = null; return; }
    if (page === null) return;
    if ((m = /^\?\?\s*(.*)$/.exec(l))) { gate = splitConds(m[1]); return; }
    if ((m = /^>\s*.*?=>\s*(\S+)(.*)$/.exec(l))) {
      const ev = /!raise\s+(\w+)/.exec(m[2]);
      pages[page].push({ to: m[1].split('|').map(x => x.replace(/\*\d+$/, '')), raise: ev ? ev[1] : null, gate: gate || [], line: i + 1 }); // (a draw «a|b*2»: each of its places)
    }
  });
  // every way from the first page to a reply that raises something (a page seen on a way is not walked again)
  const walk = (pg, gates, seen) => {
    for (const r of pages[pg] || []) {
      const g = r.gate.length ? gates.concat([r.gate]) : gates;
      if (r.raise) { const e = talkEvents[r.raise] = talkEvents[r.raise] || []; let one = e.find(x => x.file === f && x.line === r.line); if (!one) e.push(one = { file: 'talk/' + f, who, line: r.line, ways: [] }); one.ways.push(g); }
      for (const to of r.to) if (to !== 'end' && pages[to] && !seen.has(to)) walk(to, g, new Set(seen).add(to));
    }
  };
  if (first) walk(first, [], new Set([first]));
}

// tales: a moment of the world, known once it came
const tales = {};
for (const f of files('tales')) {
  let id = null;
  for (const l of linesOf('tales', f)) {
    let m = /^\[tale\s+(\S+)\]$/.exec(l);
    if (m) { id = m[1]; tales[id] = { when: [] }; continue; }
    if (id && (m = /^when:\s*(.*)$/.exec(l))) tales[id].when = splitConds(m[1]);
  }
}

// ---------------------------------------------------------------- facts
// a state: what the character has, what the world has, where the walked quest stands, the words to people
const newState = () => ({ me: {}, world: {}, quests: {}, at: {}, orders: {}, lived: {} });
const copy = st => JSON.parse(JSON.stringify(st));
const fill = (tpl, w) => tpl.replace(/\{(\d)\}/g, (_, n) => { const x = w[+n] || ''; return /^(gate|hearth|state|placed|been|read):/.test(tpl) || tpl.startsWith('at ') ? placeOf(x) : x; });
// a fact's key and the value asked of it («gate:zala=open» → key gate:zala, value open)
const keyOf = (tpl, w) => { const t = fill(tpl, w).trim(), eq = t.indexOf('='); return eq < 0 ? [t, true] : [t.slice(0, eq), t.slice(eq + 1)]; };
const parseCond = text => { const not = text.startsWith('!'), w = (not ? text.slice(1) : text).trim().split(/\s+/); return { not, w, kind: w[0], dry: condDry[w[0]] }; };
const parseDry = dry => { const colon = dry.indexOf(' : '); const head = (colon < 0 ? dry : dry.slice(0, colon)).trim(), how = colon < 0 ? null : dry.slice(colon + 3).trim(); const sp = head.indexOf(' '); return { word: sp < 0 ? head : head.slice(0, sp), rest: sp < 0 ? '' : head.slice(sp + 1), how }; };

// every action of the data that sets or clears a fact, with where it stands (to say who could make a fact a stage waits on)
const setters = {}; // key → [{ where, value, quest, stage }]
const noteAct = (act, where, quest, stage) => {
  const w = act.split(/\s+/), dry = w[0] === 'npc' ? actDry[w[2]] : actDry[w[0]];
  if (!dry) return;
  const m = /^(set|clear|later) (me|world) (.+)$/.exec(dry);
  if (!m) return;
  const [key, value] = keyOf(m[3], w);
  (setters[key] = setters[key] || []).push({ where, value: m[1] === 'clear' ? false : value, quest, stage });
};
for (const q of quests) for (const s of q.stages) {
  for (const a of s.start.concat(s.done)) noteAct(a, q.id + '.' + s.id, q.id, s.id);
  for (const w of s.ways) for (const a of w.done) noteAct(a, q.id + '.' + s.id + ' (way ' + w.id + ')', q.id, s.id);
  if (s.scene && scenes[s.scene]) for (const a of scenes[s.scene].does) noteAct(a, 'scene ' + s.scene + ' of ' + q.id + '.' + s.id, q.id, s.id);
}
const stageScenes = new Set(quests.flatMap(q => q.stages.map(s => s.scene).filter(Boolean)));
for (const id of Object.keys(scenes)) if (!stageScenes.has(id)) for (const a of scenes[id].does) noteAct(a, 'scene ' + id, null, null);
for (const p of Object.values(places)) for (const n of Object.keys(p.elsedo)) for (const a of p.elsedo[n]) noteAct(a, 'a touch of ' + p.id + '.' + n + ' (elsedo)', null, null);

// ---------------------------------------------------------------- one walk
function walkQuest(q, world, label) {
  const rows = {}, problems = [], seen = new Set(), ends = [];
  const say = (kind, stage, text) => { const line = kind + ' ' + q.id + '.' + stage + ': ' + text; if (!problems.includes(line)) problems.push(line); };
  const order = q.stages.map(s => s.id);
  const laterOf = (from, to) => order.indexOf(to) > order.indexOf(from);

  // yes | no | maybe, now
  const now = (text, st, stage) => {
    const c = parseCond(text);
    if (!c.dry) return 'maybe';
    const d = parseDry(c.dry);
    let v = 'maybe';
    if (d.word === 'step') v = c.w[1] === q.id ? (stage === c.w[2] ? 'yes' : 'no') : (st.at[c.w[1]] === c.w[2] ? 'yes' : st.quests[c.w[1]] === 'done' || !st.quests[c.w[1]] ? 'no' : 'maybe');
    else if (d.word === 'quest') { const want = c.w[2] || 'done', has = c.w[1] === q.id ? 'active' : st.quests[c.w[1]] || ''; v = want === 'started' ? (has ? 'yes' : 'no') : has === want ? 'yes' : 'no'; }
    else if (d.word === 'lived') v = st.lived[c.w[1]] ? 'yes' : 'no';
    else if (d.word === 'reached') { // (got to that stage or past it, by the file's order: StoryQuests.Reached)
      const qq = quests.find(x => x.id === c.w[1]), ord = qq ? qq.stages.map(x => x.id) : [], want = ord.indexOf(c.w[2]);
      const at = c.w[1] === q.id ? stage : st.at[c.w[1]], has = c.w[1] === q.id ? 'active' : st.quests[c.w[1]];
      v = want < 0 || !has ? 'no' : has === 'done' || has === 'failed' ? 'yes' : at ? (ord.indexOf(at) >= want ? 'yes' : 'no') : 'maybe';
    }
    else if (d.word === 'me' || d.word === 'world') { const [key, value] = keyOf(d.rest, c.w), has = st[d.word][key]; v = has === undefined || has === false ? (value === 'shut' ? 'yes' : 'no') : value === true || has === value ? 'yes' : 'no'; } // (a gate never opened is shut)
    else if (d.word === 'after') v = st.me[fill(d.rest, c.w)] ? 'maybe' : 'no';
    else if (d.word === 'at') { const pl = fill(d.rest, c.w); v = pl.startsWith('me.') && !st.me['spot:' + pl.slice(3).split('.')[0]] ? 'no' : 'maybe'; }
    return c.not ? (v === 'yes' ? 'no' : v === 'no' ? 'yes' : 'maybe') : v;
  };
  const all = (conds, st, stage) => { let v = 'yes'; for (const c of conds) { const one = now(c, st, stage); if (one === 'no') return 'no'; if (one === 'maybe') v = 'maybe'; } return v; };

  // can this condition come to hold while the player stands on this stage? → { ok, by }
  const comes = (text, st, s) => {
    const c = parseCond(text), v = now(text, st, s.id);
    if (!c.dry) return { ok: true, by: 'NOT JUDGED: the kind «' + c.kind + '» has no Dry line (src)' };
    if (v === 'yes') return { ok: true, by: 'stands already' };
    const d = parseDry(c.dry);
    if (d.word === 'free' || d.word === 'moment') return { ok: true, by: 'the player' };
    if (d.word === 'at') { const pl = fill(d.rest, c.w); return v === 'no' && !c.not ? { ok: false, by: 'the place «' + pl + '» is the player\'s own and no «spot ' + pl.slice(3).split('.')[0] + '» was done before this stage' } : { ok: true, by: 'the player' }; }
    if (d.word === 'after') return v === 'no' && !c.not ? { ok: false, by: 'no «' + fill(d.rest, c.w).replace(':', ' ') + '» was done before this stage: it never counts' } : { ok: true, by: 'time' };
    if (d.word === 'step' || d.word === 'quest' || d.word === 'lived' || d.word === 'reached') return v === 'no' ? { ok: c.w[1] !== q.id, by: c.w[1] !== q.id ? 'the quest «' + c.w[1] + '» (walked apart)' : 'it does not hold on this stage and the stage cannot change it' } : { ok: true, by: 'the quests' };
    const [key, value] = keyOf(d.rest, c.w);
    if (c.not) { // it stands and must go
      const off = (setters[key] || []).filter(x => x.value === false || (value !== true && x.value !== value));
      const free = off.filter(x => x.quest !== q.id || !laterOf(s.id, x.stage));
      return free.length ? { ok: true, by: free[0].where } : { ok: false, by: '«' + key + '» stands and ' + (off.length ? 'only a later stage of this quest takes it away (' + off[0].where + ')' : 'nothing takes it away') };
    }
    if (d.how) {
      if (key.startsWith('hearth:')) { const p = places[key.slice(7)]; if (p && p.hearthwait && all(p.hearthwait, st, s.id) === 'yes') return { ok: false, by: 'the hearth of ' + p.id + ' may not be lit on this stage (hearthwait: ' + p.hearthwait.join(', ') + ')' }; }
      if (key.startsWith('knows:')) { const t = tales[key.slice(6)]; if (!t) return { ok: false, by: 'no tale «' + key.slice(6) + '»' }; for (const w of t.when) { const r = comes(w, st, s); if (!r.ok) return { ok: false, by: 'the tale «' + key.slice(6) + '» never comes: ' + r.by }; } }
      return { ok: true, by: d.how };
    }
    if (madeByCode[key]) return { ok: true, by: 'the game itself (' + madeByCode[key] + ')' };
    const on = (setters[key] || []).filter(x => x.value !== false && (value === true || x.value === value));
    const free = on.filter(x => x.quest !== q.id || !laterOf(s.id, x.stage));
    if (free.length) return { ok: true, by: free[0].where + (free[0].quest === q.id ? '' : free[0].quest ? ' (another quest)' : '') };
    return { ok: false, by: on.length ? 'only a later stage of this quest makes «' + key + '» (' + on[0].where + ')' : 'nothing makes «' + key + '»: no action of the quests, scenes or places, no part of the game' };
  };

  // who raises this event, and may it come on this stage?
  const eventComes = (on, st, s) => {
    const w = on.split(/\s+/), name = w[0].toLowerCase(), arg = w.slice(1).join(' ');
    if (name === 'use') {
      const pl = placeOf(arg), dot = pl.indexOf('.'), p = places[pl.slice(0, dot)], thing = pl.slice(dot + 1);
      if (!p || !(thing in p.touch)) return { ok: false, by: 'no «touch: ' + thing + '» in the place: nothing to press E on' };
      for (const h of p.hide[thing] || []) if (all(h, st, s.id) === 'yes') return { ok: false, by: 'the thing is hidden on this stage (hidewhile: ' + h.join(', ') + ')' };
      const lit = p.touch[thing];
      for (const c of lit) { const r = now(c, st, s.id) === 'no' ? comes(c, st, s) : { ok: true }; if (!r.ok) return { ok: false, by: 'the thing is not lit on this stage (touch: ' + c + ' — ' + r.by + ')' }; }
      return { ok: true, by: 'E on ' + pl, again: 'E again while it is lit' };
    }
    const talks = talkEvents[name];
    if (talks) {
      let why = '';
      for (const t of talks) {
        const who = people[t.who];
        if (who && all(who.after, st, s.id) === 'no') { const r = who.after.map(c => comes(c, st, s)).find(x => !x.ok); if (r) { why = t.who + ' is not in the world yet (after: ' + who.after.join(', ') + ' — ' + r.by + ')'; continue; } }
        const open = t.ways.find(g => g.every(gate => all(gate, st, s.id) !== 'no'));
        if (open) return { ok: true, by: 'a reply of ' + t.file + ':' + t.line, again: 'the reply stands while its gate holds (whether it was used up is the talk\'s own matter)' };
        why = 'its reply (' + t.file + ':' + t.line + ') is closed on this stage: ' + t.ways.map(g => '?? ' + g.map(x => x.join(', ')).join(' · ')).join(' | ');
      }
      return { ok: false, by: why };
    }
    for (const qq of quests) for (const ss of qq.stages) for (const a of ss.start.concat(ss.done)) if (new RegExp('^raise\\s+' + name + '\\b').test(a)) return { ok: true, by: 'raised by ' + qq.id + '.' + ss.id, again: 'once' };
    if (raisedByCode[name]) return { ok: true, by: 'the game (' + raisedByCode[name] + ')', again: 'when it happens again' };
    return { ok: false, by: 'nobody raises «' + name + '»: no reply of a talk, no action, no part of the game' };
  };

  const apply = (act, st, stage) => { // → a stage to jump to, or null
    const w = act.split(/\s+/), dry = w[0] === 'npc' ? actDry[w[2]] : actDry[w[0]];
    if (!dry) { say('NOT JUDGED', stage, 'the action «' + w[0] + (w[0] === 'npc' ? ' … ' + w[2] : '') + '» has no Dry line (src): what it leaves is unknown'); return null; }
    if (dry === 'go') return w[0] === 'end' ? 'end' : w[1];
    if (dry === 'scene' && scenes[w[1]]) { for (const a of scenes[w[1]].does) apply(a, st, stage); return null; }
    let m = /^(set|clear|later) (me|world) (.+)$/.exec(dry);
    if (m) { const [key, value] = keyOf(m[3], w); st[m[2]][key] = m[1] === 'clear' ? false : value; return null; }
    if ((m = /^order (\S+) (\w+)$/.exec(dry))) { const who = fill(m[1], w); if (m[2] === 'free') delete st.orders[who]; else st.orders[who] = { verb: m[2], text: act, from: stage }; }
    return null;
  };
  const row = (s, text) => { (rows[s.id] = rows[s.id] || []).includes(text) || rows[s.id].push(text); };

  function enter(id, st, from, forked) {
    if (id === 'end' || id === '') { ends.push(st); return; }
    const s = q.stage(id);
    if (!s) { say('STUCK', from || '?', 'goes to «' + id + '», a stage that is not there'); return; }
    // a word left by the stage a fork came from at once
    if (forked) for (const who of Object.keys(st.orders)) {
      const o = st.orders[who];
      if (o.from !== from || (o.verb !== 'follow' && o.verb !== 'stay')) continue;
      const first = s.start.find(a => a.startsWith('npc ' + who + ' '));
      if (!first) say('LEFT', from, '«start: ' + o.text + '» stands when its fork leaves at once to «' + s.id + '», and that stage\'s «start:» says nothing to ' + who + ' (npc ' + who + ' free)');
    }
    st.at[q.id] = s.id;
    for (const a of s.start) { const j = apply(a, st, s.id); if (j) { enter(j, st, s.id, false); return; } }
    const key = s.id + '|' + JSON.stringify([st.me, st.world, st.orders]);
    if (seen.has(key)) return;
    seen.add(key);
    if (seen.size > 4000) { say('NOT JUDGED', s.id, 'the walk grew past 4000 states: stopped'); return; }
    // the forks: a server quest's own first; one that surely holds goes, one that may is walked both ways
    for (const f of q.forks.filter(x => x.to !== s.id).concat(s.forks)) { // (StoryQuests.ServerStep: its own fork to the stage it stands on is not asked)
      const v = all(f.when, st, s.id);
      if (v === 'no') continue;
      row(s, 'fork «' + f.text + '»' + (v === 'yes' ? '' : ' (may)'));
      enter(f.to, copy(st), s.id, true);
      if (v === 'yes') return;
    }
    const pass = (d, way) => {
      const st2 = copy(st), name = way ? s.id + ' (way ' + way.id + ')' : s.id;
      const said = [];
      let ok = true;
      if (d.scene) {
        const sc = scenes[d.scene];
        said.push('scene ' + d.scene);
        if (!sc) { ok = false; say('STUCK', name, 'the scene «' + d.scene + '» is not there'); }
        else for (const n of sc.needs) { const r = comes(n, st2, s); if (!r.ok) { ok = false; say('STUCK', name, 'its scene «' + d.scene + '» never plays — needs «' + n + '»: ' + r.by); } else if (r.by.startsWith('NOT JUDGED')) say('NOT JUDGED', name, 'the scene\'s need «' + n + '»: ' + r.by); }
      }
      else {
        if (d.on) {
          const r = eventComes(d.on, st2, s);
          said.push('on ' + d.on + ' ← ' + r.by);
          if (!r.ok) { ok = false; say('STUCK', name, 'waits for the event «' + d.on + '»: ' + r.by); }
          else say('RELOG', name, 'passes by the event «' + d.on + '» alone (' + r.by + '): one that comes while the player is not on this stage is lost; again: ' + r.again);
        }
        for (const c of d.wait) {
          const r = comes(c, st2, s);
          said.push(c + ' ← ' + r.by);
          if (!r.ok) { ok = false; say('STUCK', name, 'waits for «' + c + '»: ' + r.by); }
          else if (r.by.startsWith('NOT JUDGED')) say('NOT JUDGED', name, '«' + c + '»: ' + r.by);
          else { const pc = parseCond(c), pd = parseDry(pc.dry); if (pd.word === 'moment') say('RELOG', name, 'waits for «' + c + '», which holds only for a moment: gone after a relog'); if ((pd.word === 'me' || pd.word === 'world') && !pc.not && now(c, st2, s.id) !== 'yes') { const [k, v] = keyOf(pd.rest, pc.w); st2[pd.word][k] = v; } } // (what it waited for now stands)
        }
        if (!d.on && !d.wait.length && !way) said.push('nothing: passes at once');
      }
      row(s, (way ? 'way ' + way.id + ': ' : '') + said.join(' · ') + (ok ? '' : '   ← STUCK'));
      if (!ok) return;
      if (d.scene && scenes[d.scene]) for (const a of scenes[d.scene].does) apply(a, st2, s.id);
      if (s.saga) st2.lived[q.id + '.' + s.id] = true;
      if (way) { st2.me['chose:' + (s.choice || q.id)] = way.id; if (way.saga) st2.lived[q.id + '.' + s.id + '.' + way.id] = true; }
      let jump = null;
      for (const a of (way ? way.done : []).concat(s.done)) { const j = apply(a, st2, s.id); if (j && !jump) jump = j; }
      enter(jump || d.next || s.next, st2, s.id, false);
    };
    if (s.ways.length) for (const w of s.ways) pass(w, w); else pass(s, null);
  }

  const st0 = newState();
  st0.world = copy(world);
  for (const c of q.starts.concat(q.who)) { const pc = parseCond(c); if (!pc.not && pc.kind === 'quest') st0.quests[pc.w[1]] = pc.w[2] || 'done'; } // (what it starts on is taken as so)
  for (const c of q.starts) { const pc = parseCond(c); if (pc.dry && !pc.not) { const pd = parseDry(pc.dry); if (pd.word === 'me' || pd.word === 'world') { const [k, v] = keyOf(pd.rest, pc.w); st0[pd.word][k] = v; } } }
  enter(q.first, st0, '', false);
  return { rows, problems, ends };
}

// ---------------------------------------------------------------- the report
const picked = quests.filter(q => !only || q.id === only);
if (only && !picked.length) { console.log('QUEST no quest «' + only + '» (' + quests.map(q => q.id).join(', ') + ')'); process.exit(1); }
// the world after the story: every fact of the world a fresh walk of any quest made, and what the game makes by itself
const fresh = {}, lateWorld = {};
for (const q of quests) { fresh[q.id] = walkQuest(q, {}, 'fresh'); for (const e of fresh[q.id].ends) Object.assign(lateWorld, e.world); }
for (const k of Object.keys(madeByCode)) lateWorld[k] = true;
for (const k of Object.keys(condDry)) { const d = parseDry(condDry[k]); if (d.word !== 'world' || !d.how) continue; } // (a world fact the player makes — a hearth lit — stands in the late world only where a walk waited for it: taken from the ends above)
let stuck = 0;
for (const q of picked) {
  const a = fresh[q.id], b = walkQuest(q, lateWorld, 'late');
  console.log('\n== ' + q.id + (q.server ? '  (the server\'s' + (q.who.length ? '; leads: ' + q.who.join(', ') : '') + ')' : '  (each player\'s own)') + '  ' + q.file + '  starts: ' + (q.starts.join(', ') || '-'));
  for (const s of q.stages) {
    const fr = a.rows[s.id] || ['(never reached)'], lt = b.rows[s.id] || ['(never reached)'];
    console.log('  ' + s.id.padEnd(16) + fr[0]);
    for (const x of fr.slice(1)) console.log('  ' + ''.padEnd(16) + x);
    if (JSON.stringify(fr) !== JSON.stringify(lt)) for (const x of lt) console.log('  ' + '  late:'.padEnd(16) + x);
  }
  const order = ['STUCK', 'LEFT', 'NEVER', 'NOT JUDGED', 'RELOG'];
  const lines = a.problems.map(p => [p, 'fresh']);
  for (const p of b.problems) { const had = lines.find(x => x[0] === p || x[0] === p.replace('(late)', '(fresh)')); if (had) had[1] = 'both'; else lines.push([p, 'late']); }
  for (const s of q.stages) if (!a.rows[s.id] && !b.rows[s.id]) lines.push(['NEVER ' + q.id + '.' + s.id + ': neither walk comes to it (only a world in between — part of the facts standing, part not — may: check its forks by hand)', 'both']);
  lines.sort((x, y) => order.findIndex(o => x[0].startsWith(o)) - order.findIndex(o => y[0].startsWith(o)));
  for (const [p, where] of lines) { if (p.startsWith('STUCK')) stuck++; console.log('  ' + p + (where === 'both' ? '' : '   [' + where + ' world only]')); }
  const left = {};
  for (const e of a.ends.concat(b.ends)) for (const who of Object.keys(e.orders)) left[who + ': ' + e.orders[who].text + ' (from ' + e.orders[who].from + ')'] = true;
  if (a.ends.length + b.ends.length === 0) console.log('  the walk never reaches the quest\'s end');
  else console.log('  at its end words to people still stand: ' + (Object.keys(left).join(' | ') || 'none'));
}
console.log('\nquest_dry: ' + picked.length + ' quest(s), ' + stuck + ' STUCK. Not known here: distances, time, numbers, the inside of scenes, two players in one pass (the head of tools/quest_dry.js).');
process.exit(strict && stuck ? 1 : 0);

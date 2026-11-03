// The story's data holds together, checked at the build (the game checks the same on load, but only says so in its log):
// every stage a quest goes to, every quest and scene it names, every place and named piece, every person. A typo there
// would play a scene «at once» or put a mark nowhere. The words of steps and conditions are left to the game's own check.
// Run: node tools/storycheck.js  (exit code 1 on a problem: rw_build stops)
const fs = require('fs'), path = require('path');
const story = path.join(__dirname, '..', 'assets', 'story');
const problems = [];
const say = (file, what) => problems.push('STORY ' + file + ': ' + what);
const txt = dir => fs.existsSync(path.join(story, dir)) ? fs.readdirSync(path.join(story, dir)).filter(f => f.endsWith('.txt')) : [];
const lines = (dir, f) => fs.readFileSync(path.join(story, dir, f), 'utf8').split(/\r?\n/).map(l => l.replace(/\s+#.*$/, '').replace(/^#.*$/, '').trim());

// places: blueprints/locations.list.txt, their pieces: blueprints/<place>.names.txt
const places = {};
for (const l of lines('blueprints', 'locations.list.txt')) { const m = /^loc\s+(\S+)/.exec(l); if (m) places[m[1]] = new Set(); }
for (const id of Object.keys(places)) {
  const f = id + '.names.txt';
  if (fs.existsSync(path.join(story, 'blueprints', f))) for (const l of lines('blueprints', f)) { const w = l.split(/\s+/)[0]; if (w) places[id].add(w); }
}
// a name must still fall on a piece of its blueprint: a piece replaced or moved there leaves the name on nothing, and every scene and
// quest that uses it goes dead with no word (the game finds a name by its prefab and place)
for (const id of Object.keys(places)) {
  const nf = path.join(story, 'blueprints', id + '.names.txt'), bf = path.join(story, 'blueprints', id + '.txt');
  if (!fs.existsSync(nf) || !fs.existsSync(bf)) continue;
  const pieces = lines('blueprints', id + '.txt').map(l => l.split(/\s+/));
  const found = {};
  for (const l of lines('blueprints', id + '.names.txt')) {
    const w = l.split(/\s+/);
    if (w.length < 5) continue;
    found[w[0]] = found[w[0]] || pieces.some(p => p[0] === w[1] && Math.abs(+p[1] - +w[2]) < 0.06 && Math.abs(+p[2] - +w[3]) < 0.06 && Math.abs(+p[3] - +w[4]) < 0.06);
  }
  for (const n of Object.keys(found)) if (!found[n]) say('blueprints/' + id + '.names.txt', 'name «' + n + '» falls on no piece of ' + id + '.txt: the piece was replaced or moved');
}
places.hall = places.zala; // (the Hall's other word: StoryWhere.HallWord)
const placeWhy = word => {
  if (word === 'bird' || word === 'player' || word === 'sky' || word.startsWith('npc.')) return null;
  const [id, piece] = word.split('.');
  if (!places[id]) return 'no place «' + id + '» in locations.list.txt';
  return piece && !places[id].has(piece) ? 'no piece «' + piece + '» in ' + (id === 'hall' ? 'zala' : id) + '.names.txt' : null;
};
// a word naming a piece of a known place, anywhere in a line of steps
const checkDotted = (file, words) => { for (const w of words) { const m = /^(\w+)\.(\w+)$/.exec(w); if (m && places[m[1]]) { const why = placeWhy(w); if (why) say(file, why); } } };

// people's templates
const npcs = new Set();
for (const f of txt('npc')) for (const l of lines('npc', f)) { const m = /^\[npc\s+(\S+)\]/.exec(l); if (m) npcs.add(m[1]); }

// the word of a scene's step that names a place (the same steps as StoryScenes.CheckPlaces in the game)
const after = (w, word) => { const i = w.indexOf(word); return i > 0 && i + 1 < w.length ? w[i + 1] : null; };
function stepPlace(w) {
  if (w[0] === 'bird' && (w[1] === 'land' || w[1] === 'leave')) return w[2];
  if (w[0] === 'bird' && w[1] === 'come') return after(w, 'at');
  if (w[0] === 'npc' && ['go', 'sit', 'spawn', 'face'].includes(w[2])) return w[3];
  if ((w[0] === 'door' || w[0] === 'bolt') && w[1] !== 'near' && w[1] !== 'ahead') return w[1];
  if (w[0] === 'drop' || w[0] === 'sound') return w[2];
  if (w[0] === 'prop' || (w[0] === 'npc' && w[2] === 'prop')) return after(w, 'from');
  if (w[0] === 'camera') return after(w, 'look');
  return null;
}

// scenes
const scenes = new Set(), used = new Set();
for (const f of txt('scenes')) {
  let at = f;
  for (const l of lines('scenes', f)) {
    if (!l) continue;
    const head = /^\[scene\s+(\S+)\]/.exec(l);
    if (head) { if (scenes.has(head[1])) say(f, 'scene «' + head[1] + '» twice'); scenes.add(head[1]); at = f + ' [' + head[1] + ']'; continue; }
    const step = l.replace(/^if [^:]*:\s*/, '');
    const w = step.split(/\s+/);
    if (w[0] === 'place:') { const why = placeWhy(w[1]); if (why) say(at, why); continue; }
    if (w[0] === 'say' || (w[0] === 'bird' && w[1] === 'say')) continue; // (a line of speech is free text)
    if (w[0] === 'npc' && w.length > 1 && !npcs.has(w[1])) say(at, 'no person «' + w[1] + '» in assets/story/npc');
    const place = stepPlace(w);
    if (place) { const why = placeWhy(place); if (why) say(at, why); }
    checkDotted(at, w);
  }
}

// quests
const questIds = new Set();
for (const f of txt('quests')) for (const l of lines('quests', f)) { const m = /^\[quest\s+(\S+)\]/.exec(l); if (m) questIds.add(m[1]); }
for (const f of txt('quests')) {
  const qs = []; let q = null, stage = null;
  for (const l of lines('quests', f)) {
    if (!l) continue;
    let m;
    if ((m = /^\[quest\s+(\S+)\]/.exec(l))) { q = { id: m[1], first: '', stages: new Set(), refs: [] }; qs.push(q); stage = null; continue; }
    if ((m = /^\[stage\s+(\S+)\]/.exec(l))) { if (q.stages.has(m[1])) say(f, q.id + ': stage «' + m[1] + '» twice'); q.stages.add(m[1]); stage = m[1]; continue; }
    if ((m = /^\[way\s+(\S+)\]/.exec(l))) continue;
    const c = l.indexOf(':'); if (c < 0 || !q) continue;
    const key = l.slice(0, c).trim(), val = l.slice(c + 1).trim();
    const at = f + ' [' + q.id + (stage ? '.' + stage : '') + ']';
    if (key !== 'goal' && key !== 'name' && !(key === 'start' && val.startsWith('msg '))) for (const named of val.matchAll(/\bquest\s+(\w+)/g)) if (!questIds.has(named[1])) say(at, 'no quest «' + named[1] + '»');
    if (key === 'first') q.first = val;
    else if (key === 'next') q.refs.push([at, val]);
    else if (key === 'fork') { const i = val.lastIndexOf('->'); if (i >= 0) q.refs.push([at, val.slice(i + 2).trim()]); }
    else if (key === 'scene') { used.add(val); if (!scenes.has(val)) say(at, 'no scene «' + val + '»'); }
    else if (key === 'mark') { const why = placeWhy(val.split(/\s+/)[0]); if (why) say(at, why); }
    else if (key === 'wait' || key === 'on' || key === 'start' || key === 'done') {
      if (key === 'start' && val.startsWith('msg ')) continue; // (free text)
      const near = /\bnear\s+(\S+)/.exec(val);
      if (near) { const why = placeWhy(near[1].replace(/,$/, '')); if (why) say(at, why); }
      checkDotted(at, val.split(/[\s,]+/));
      const npc = /^npc\s+(\S+)/.exec(val);
      if (npc && !npcs.has(npc[1])) say(at, 'no person «' + npc[1] + '» in assets/story/npc');
    }
  }
  for (const qu of qs) {
    if (qu.first && !qu.stages.has(qu.first)) say(f, qu.id + ': first stage «' + qu.first + '» is not there');
    for (const [at, to] of qu.refs) if (to !== 'end' && !qu.stages.has(to)) say(at, 'no stage «' + to + '»');
  }
}

// people
for (const f of txt('people')) {
  let at = f;
  for (const l of lines('people', f)) {
    const head = /^\[person\s+(\S+)\]/.exec(l);
    if (head) { at = f + ' [' + head[1] + ']'; continue; }
    const m = /^(npc|live|kindled):\s*(.*)$/.exec(l);
    if (!m) continue;
    if (m[1] === 'npc') { if (!npcs.has(m[2])) say(at, 'no person «' + m[2] + '» in assets/story/npc'); continue; }
    checkDotted(at, m[2].split(/\s+/));
  }
}

if (problems.length) { console.log(problems.join('\n') + '\n\n' + problems.length + ' broken in the story\'s data: assets/story.'); process.exit(1); }
console.log('storycheck: ok (' + scenes.size + ' scenes, ' + used.size + ' played by quests, ' + npcs.size + ' people, ' + Object.keys(places).length + ' places)');

// The story's data holds together, checked at the build (the game checks the same on load, but only says so in its log):
// every stage a quest goes to, every quest and scene it names, every place and named piece, every person. A typo there
// would play a scene «at once» or put a mark nowhere. The first words of steps, conditions and actions are checked against the code
// (what the registries know); their own words are left to the game's check at load.
// Run: node tools/storycheck.js  (exit code 1 on a problem: rw_build stops)
const fs = require('fs'), path = require('path');
const story = path.join(__dirname, '..', 'assets', 'story');
const problems = [];
const say = (file, what) => problems.push('STORY ' + file + ': ' + what);
const txt = dir => fs.existsSync(path.join(story, dir)) ? fs.readdirSync(path.join(story, dir)).filter(f => f.endsWith('.txt')) : [];
const lines = (dir, f) => fs.readFileSync(path.join(story, dir, f), 'utf8').split(/\r?\n/).map((l, i) => { if (/\S\t#/.test(l) && !/ #/.test(l.slice(0, l.indexOf('\t#')))) say(dir + '/' + f + ':' + (i + 1), 'a tab before «#»: the game cuts a note only after a space, so the note is read as words'); return l.replace(/\s+#.*$/, '').replace(/^#.*$/, '').trim(); });
// the mod's own items (RavenWatch_item_<id>): assets/items and assets/story/items, by their «id:» (here: any line may name one)
const ownItems = new Set();
for (const d of [path.join(story, '..', 'items'), path.join(story, 'items')]) for (const f of fs.existsSync(d) ? fs.readdirSync(d).filter(x => x.endsWith('.txt')) : []) { const m = /^id:\s*(\S+)/m.exec(fs.readFileSync(path.join(d, f), 'utf8')); if (m) ownItems.add(m[1]); }
const itemWhy = item => item && item.startsWith('RavenWatch_item_') && !ownItems.has(item.slice(16)) ? 'no item «' + item + '» in assets/items or assets/story/items (by its «id:»)' : null;

// places: blueprints/locations.list.txt, their pieces: blueprints/<place>.names.txt
const places = {};
for (const l of lines('blueprints', 'locations.list.txt')) { const m = /^loc\s+(\S+)/.exec(l); if (m) places[m[1]] = new Set(); }
for (const id of Object.keys(places)) {
  const f = id + '.names.txt';
  if (fs.existsSync(path.join(story, 'blueprints', f))) for (const l of lines('blueprints', f)) { const w = l.split(/\s+/)[0]; if (w) places[id].add(w); }
}
// a name must still fall on a piece of its blueprint: a piece replaced or moved there leaves the name on nothing, and every scene and
// quest that uses it goes dead with no word. The game finds a name by its prefab and place within 5 cm (LocParts.Near). The author's game
// reads and writes these same files (Blueprint.Data), so there is no other copy to hold to them.
for (const id of Object.keys(places)) {
  const nf = 'blueprints/' + id + '.names.txt', bf = id + '.txt';
  if (!fs.existsSync(path.join(story, nf)) || !fs.existsSync(path.join(story, 'blueprints', bf))) continue;
  const pieces = lines('blueprints', bf).map(l => l.split(/\s+/));
  const found = {};
  for (const l of lines('blueprints', id + '.names.txt')) {
    const w = l.split(/\s+/);
    if (w.length < 5) continue;
    found[w[0]] = found[w[0]] || pieces.some(p => p[0] === w[1] && Math.hypot(+p[1] - +w[2], +p[2] - +w[3], +p[3] - +w[4]) < 0.05);
  }
  for (const n of Object.keys(found)) if (!found[n]) say(nf, 'name «' + n + '» falls on no piece of ' + bf + ': the piece was replaced or moved. It can no longer be touched or found: move the name with it');
}
// a line of names or layers that went into a build and is gone now: a piece moved or renamed. A world built before still has the
// piece where the old line said, and finds it no more (LocParts keeps several lines a name for that): keep the old line beside the new.
// What every build had: tools/names.seen.txt, kept with the code (a line once seen stays; take it out there only when no world has the old place).
const warnings = []; let writeLedger = null;
{
  const seenFile = path.join(__dirname, 'names.seen.txt'), now = new Set(), held = [];
  for (const f of fs.readdirSync(path.join(story, 'blueprints')).filter(x => /\.(names|layers)\.txt$/.test(x))) for (const l of lines('blueprints', f)) { const w = l.split(/\s+/); if (w.length >= 5) { now.add(f + '\t' + w.join(' ')); held.push({ f, name: w[0], prefab: w[1], at: w.slice(2, 5).map(Number) }); } }
  const seen = fs.existsSync(seenFile) ? fs.readFileSync(seenFile, 'utf8').split(/\r?\n/).filter(Boolean) : [];
  const still = s => { const [f, l] = s.split('\t'), w = (l || '').split(' '), at = w.slice(2, 5).map(Number); return held.some(h => h.f === f && (h.name === w[0] || /.layers.txt$/.test(f)) && h.prefab === w[1] && Math.hypot(h.at[0] - at[0], h.at[1] - at[1], h.at[2] - at[2]) < 0.05); }; // (a layers line: its first word is the piece's layer, not its name — the same piece at the same spot with another layer is still found: the user 2026-11-15, the camp's poison stands gone in its new sub-state too) // (LocParts.Near: the game finds it within 5 cm)
  for (const s of seen) if (!still(s)) { const [f, l] = s.split('\t'); warnings.push('blueprints/' + f + ': «' + l + '» is gone: a world built before has that piece there and finds it no more. Keep the old line beside the new one (or, when no world has that place, take it out of tools/names.seen.txt)'); }
  if (process.argv.includes('--ledger')) writeLedger = () => { try { fs.writeFileSync(seenFile, [...new Set(seen.concat([...now]))].sort().join('\n') + '\n'); } catch (e) { console.log('STORY WARNING tools/names.seen.txt not written: ' + e.message); } }; // (only the build writes it, and only when the story holds: a trial run leaves no line behind)
}
// A sub-state added to a state after its pieces were marked (the camp's «казан» 2026-11-15, the Hall's «обжита_гейд» 2026-11-16: Heid's things gone
// and the rubble back in the Hall she had moved into). A mark speaks of one step only (LocLayers.Shows): «+1.2» there only in 1.2, «-1.2» gone only
// in 1.2 — so the new step shows such a piece as the state's start. When a state has more steps than the last build had (tools/substates.seen.txt),
// every piece marked for what was its last step must say what it is in each new one: «+s.k» / «-s.k» for it, or «>s.k» (there from that step on).
{
  const subsFile = path.join(__dirname, 'substates.seen.txt'), subsNow = {}, subsSeen = {};
  let at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l); if (m) { at = m[1]; continue; }
    const st = /^states:\s*(.+)$/.exec(l); if (!st || !at) continue;
    st[1].split(',').forEach((part, i) => { const p = /\(([^)]*)\)/.exec(part); subsNow[at + ' ' + (i + 1)] = p ? p[1].split('/').length : 0; });
  }
  // «worn: 1.4 0.5 on wall roof» (LocState.Broken): a step its place does not have, or a part not 0..1, would do nothing with no word in the game
  at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l); if (m) { at = m[1]; continue; }
    const wn = /^worn:\s*(.*)$/.exec(l); if (!wn) continue;
    const w = wn[1].split(/\s+/).filter(Boolean), on = w.indexOf('on');
    if (on < 2 || on % 2 !== 0 || on + 1 >= w.length) { say('blueprints/locations.list.txt', at + ': worn: <step> <part> [<step> <part> …] on <prefab words>'); continue; }
    for (let i = 0; i < on; i += 2) {
      const sm = /^(\d+)(?:\.(\d+))?$/.exec(w[i]), part = Number(w[i + 1]);
      if (!sm || subsNow[at + ' ' + sm[1]] === undefined || (sm[2] && +sm[2] > subsNow[at + ' ' + sm[1]])) say('blueprints/locations.list.txt', at + ': worn: «' + w[i] + '» is no step of its «states:»');
      if (!(part >= 0 && part <= 1)) say('blueprints/locations.list.txt', at + ': worn: «' + w[i + 1] + '» is no part 0..1');
    }
  }
  if (fs.existsSync(subsFile)) for (const l of fs.readFileSync(subsFile, 'utf8').split(/\r?\n/)) { const w = l.trim().split(/\s+/); if (w.length === 3) subsSeen[w[0] + ' ' + w[1]] = +w[2]; }
  for (const key of Object.keys(subsNow)) {
    const [place, s] = key.split(' '), was = subsSeen[key], nowN = subsNow[key];
    if (!(was >= 1) || nowN <= was || !fs.existsSync(path.join(story, 'blueprints', place + '.layers.txt'))) continue;
    for (const l of lines('blueprints', place + '.layers.txt')) {
      const marks = (l.split(/\s+/)[0] || '').split(',');
      if (!marks.some(x => x === '+' + s + '.' + was || x === '-' + s + '.' + was) || marks.some(x => x.startsWith('>' + s + '.'))) continue;
      const missing = []; for (let k = was + 1; k <= nowN; k++) if (!marks.includes('+' + s + '.' + k) && !marks.includes('-' + s + '.' + k)) missing.push(s + '.' + k);
      if (missing.length) say('blueprints/' + place + '.layers.txt', '«' + l + '»: marked for ' + s + '.' + was + ', silent on the new ' + missing.join(', ') + ' — there it shows as the state\'s start. Say it: «+' + missing[0] + '», «-' + missing[0] + '» or «>' + s + '.' + was + '» (from ' + s + '.' + was + ' on)');
    }
  }
  const prevLedger = writeLedger;
  if (process.argv.includes('--ledger')) writeLedger = () => { if (prevLedger) prevLedger(); try { fs.writeFileSync(subsFile, '# the sub-states each place\'s state had at the last build (storycheck: a new one asks its marked pieces again)\n' + Object.keys(subsNow).sort().map(k => k + ' ' + subsNow[k]).join('\n') + '\n'); } catch (e) { console.log('STORY WARNING tools/substates.seen.txt not written: ' + e.message); } };
}
// a thing the story wants touched, read or opened must have its name; a place's other words (alias:) are its own
const aliasOf = {}, placeRules = [], lastStep = {}; // (lastStep: the last «becomes:» step of each place, in the file's order)
// the insides made of the game's rooms (assets/story/rooms/<name>.txt, built by tools/rooms.js: LocInside): each with its named points
const insides = {};
const roomFiles = fs.existsSync(path.join(story, 'rooms')) ? fs.readdirSync(path.join(story, 'rooms')).filter(f => f.endsWith('.txt')) : [];
for (const f of roomFiles) {
  if (f.endsWith('.own.txt')) continue;
  const pts = insides[f.slice(0, -4)] = new Set();
  for (const l of lines('rooms', f)) { const m = /^point\s+(\S+)/.exec(l); if (m) pts.add(m[1]); }
}
for (const f of roomFiles) if (f.endsWith('.own.txt') && !insides[f.slice(0, -8)]) say('rooms/' + f, 'the pieces of the author for a layout that is not there (' + f.slice(0, -8) + '.txt)');
{
  let at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    const al = /^alias:\s*(.+)$/.exec(l);
    if (al) { for (const a of al[1].split(/\s+/)) { if (places[a]) say('blueprints/locations.list.txt', at + ': alias «' + a + '» is a place of its own'); aliasOf[a] = at; } continue; }
    const tc = /^(touch|read|elsedo|gate):\s*(\S+)/.exec(l);
    if (tc && places[at] && !places[at].has(tc[2])) say('blueprints/locations.list.txt', at + ': ' + tc[1] + ' «' + tc[2] + '» has no such name in ' + at + '.names.txt');
    const ins = /^inside:\s*(\S+)(?:\s+door\s+(\S+))?\s*$/.exec(l);
    if (/^inside:/.test(l) && !ins) say('blueprints/locations.list.txt', at + ': inside: <layout> [door <piece>]');
    if (ins && !insides[ins[1]]) say('blueprints/locations.list.txt', at + ': inside «' + ins[1] + '» has no layout assets/story/rooms/' + ins[1] + '.txt (node tools/rooms.js build ' + ins[1] + ')');
    if (ins && ins[2] && places[at] && !places[at].has(ins[2])) say('blueprints/locations.list.txt', at + ': inside: door «' + ins[2] + '» has no such name in ' + at + '.names.txt');
    const rule = /^(lockdoors|shutdoors|litwhile|hidewhile|statewhile|touch|read|mapmark|becomes|follows|cold|fog|hearthwait|hearthgivewait|insideshut):\s*(.+)$/.exec(l);
    if (rule) placeRules.push([at, rule[1], rule[2]]);
    // «becomes:» steps are walked in the file's order and the walk stops at the first that does not hold (LocState.Becomes): a later step written
    // above an earlier one stops it for ever (2026-11-16: chapter 2's «becomes: 2 …» above «1.3» — the Hall would never have become 1.3)
    const bc = /^becomes:\s*(\d+)(?:\.(\d+))?\s/.exec(l);
    if (bc) { const code = +bc[1] * 100 + (bc[2] ? +bc[2] : 0); if (lastStep[at] !== undefined && code <= lastStep[at]) say('blueprints/locations.list.txt', at + ': becomes: ' + bc[1] + (bc[2] ? '.' + bc[2] : '') + ' stands after a later or the same step — the steps go in order, the walk stops at the first that does not hold'); lastStep[at] = code; }
    const ed = /^elsedo:\s*\S+\s+(.+)$/.exec(l);
    if (ed) placeRules.push([at, 'elsedo', ed[1]]);
  }
}
// «{pool <place>.<name> N}» in a «read:» line (LocTouch.Shown): the place has that «pool:» with at least N names, and every language's row
// of the list has as many names (they are drawn by their numbers, so a name missing in one language shows other people there)
{
  const pools = {}; let at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    const p = /^pool:\s*(\S+)\s+(.+)$/.exec(l);
    if (p) pools[at + '.' + p[1]] = p[2].trim();
  }
  at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    if (!/^read:/.test(l)) continue;
    const good = [...l.matchAll(/\{pool (\w+)\.(\w+) (\d+)(?: (\d+))?\}/g)];
    if ((l.match(/\{pool/g) || []).length !== good.length) say('blueprints/locations.list.txt', at + ': read: «{pool <місце>.<назва> <скільки> [<у рядку>]}» written another way');
    for (const g of good) {
      const list = pools[(aliasOf[g[1]] || g[1]) + '.' + g[2]];
      if (!list) say('blueprints/locations.list.txt', at + ': read: no «pool: ' + g[2] + '» in the place «' + g[1] + '»');
      else if (list.split(',').length < +g[3]) say('blueprints/locations.list.txt', at + ': read: {pool ' + g[1] + '.' + g[2] + ' ' + g[3] + '} asks more names than the list has (' + list.split(',').length + ')');
    }
  }
  const langDir = path.join(story, '..', 'lang');
  if (fs.existsSync(langDir)) for (const lang of fs.readdirSync(langDir)) {
    const ui = path.join(langDir, lang, 'ui.txt');
    if (!fs.existsSync(ui)) continue;
    const rows = fs.readFileSync(ui, 'utf8').replace(/\r/g, '').split('\n');
    for (const [key, list] of Object.entries(pools)) {
      const row = rows.find(r => r.startsWith(list + '\t'));
      const there = row ? row.slice(list.length + 1).trim() : '';
      if (there && there.split(',').length !== list.split(',').length) say('lang/' + lang + '/ui.txt', 'pool ' + key + ': ' + there.split(',').length + ' names, the list has ' + list.split(',').length);
    }
  }
}
for (const a of Object.keys(aliasOf)) places[a] = places[aliasOf[a]];
// the author's camera shots of a place (StoryShots: blueprints/<place>.shots.txt): «camera shot|move <place>.<shot>»
const shots = {};
for (const id of Object.keys(places)) {
  shots[id] = new Set();
  const f = id + '.shots.txt';
  if (fs.existsSync(path.join(story, 'blueprints', f))) for (const l of lines('blueprints', f)) { const w = l.split(/\s+/)[0]; if (w) shots[id].add(w); }
}
for (const a of Object.keys(aliasOf)) shots[a] = shots[aliasOf[a]];
// the places of a «hold» word: the first, and those after «by» and «then»
const holdPlaces = w => w.filter((x, i) => i === 0 || w[i - 1] === 'by' || w[i - 1] === 'then');
const placeWhy = word => {
  if (word.startsWith('in.')) { // (an inside and a point of it: LocInside.Place)
    const [, lay, pt] = word.split('.');
    return !insides[lay] ? 'no inside «' + lay + '» in assets/story/rooms' : pt && !insides[lay].has(pt) ? 'no point «' + pt + '» in the inside «' + lay + '» (its «point» lines: tools/rooms/' + lay + '.rooms.txt)' : null;
  }
  if (word === 'bird' || word === 'player' || word === 'player.thing' || word === 'sky' || word.startsWith('npc.') || prefixes.some(p => word.startsWith(p))) return null; // (a module's own places: StoryWhere.Prefixed, read from the code; «player.thing», «npc.<who>.thing»: what that one holds — StoryWhere.IsThing)
  const [id, piece] = word.split('.');
  if (!places[id]) return 'no place «' + id + '» in locations.list.txt';
  return piece && !places[id].has(piece) ? 'no piece «' + piece + '» in ' + (aliasOf[id] || id) + '.names.txt' : null;
};
// a word naming a piece of a known place, anywhere in a line of steps
const checkDotted = (file, words) => { for (const w of words) { const m = /^(\w+)\.(\w+)$/.exec(w); if (m && places[m[1]]) { if (words[0] === 'camera' && shots[m[1]].has(m[2])) continue; const why = placeWhy(w); if (why) say(file, why); } } };

// people's templates
const npcs = new Set();
const npcTimbres = []; // («timbre: <name>» of a person's file: held to the timbres below, once those are read)
for (const f of txt('npc')) { for (const l of lines('npc', f)) { const tm = /^timbre:\s*(\S+)/.exec(l); if (tm) npcTimbres.push([f, tm[1].toLowerCase()]); } }
const authorNpcs = new Set(); // (the author's own templates, «_» files: only his own «_» scenes may name them — 2026-11-15, the vision's stand-ins)
for (const f of txt('npc')) { for (const l of lines('npc', f)) { const m = /^\[npc\s+(\S+)\]/.exec(l); if (m) (f.startsWith('_') ? authorNpcs : npcs).add(m[1]); } } // («_» files: the author's own, not in the mod — the story may not name them)

// the sound catalogue: assets/story/sounds (AudioBank): every word of a line is a sound the game can look for or an option it reads, a name
// is written once, a file it names is there. What the game itself has: tools/game_sounds.txt (the game writes it: the «Звуки» tab)
const gameList = path.join(__dirname, 'game_sounds.txt'), gameSfx = new Set(), gameTracks = new Set();
const haveList = fs.existsSync(gameList);
if (haveList) for (const l of fs.readFileSync(gameList, 'utf8').split(/\r?\n/)) { const [k, n] = l.split(' '); if (k === 'sfx') gameSfx.add(n); else if (k === 'track') gameTracks.add(n); }
const bank = new Map();
const soundFile = /\.(ogg|wav|mp3)$/i, sceneWords = ['off', 'game', 'stop', 'all'];
// a person's timbre (NpcTimbre): a line «@timbre <name> …» of the same files — whose voice it is, and the author's tool's words for making it
const notMade = new Set();
const timbres = new Map(), timbreWords = ['for', 'age', 'pitch', 'only', 'hero', 'rec', 'creak', 'of', 'like', 'tone', 'throat', 'air', 'tilt', 'tremor'];
let pools = 0;
for (const f of txt('sounds')) for (const l of lines('sounds', f)) {
  if (!l) continue;
  if (l.startsWith('@')) {
    const tw = l.split(/\s+/), tat = 'sounds/' + f + ' «' + l.slice(0, 40) + '»';
    if (tw[0] === '@none') { if (/^speech_\w+__\w+$/.test(tw[1] || '')) notMade.add(tw[1]); else say(tat, '@none <voice sound>__<timbre>'); continue; } // (the tool's word: that sound cannot be made in that timbre, the person is silent there)
    if (tw[0] === '@pool') { // (the pool's generation, NpcTimbre: raised by the author, every settler picks its voice anew once)
      if (!/^[1-9]\d{0,5}$/.test(tw[1] || '') || tw.length > 2) say(tat, '@pool <a whole number from 1>');
      if (++pools > 1) say(tat, '«@pool» is written twice: one line says the pool\'s generation');
      continue;
    }
    // the people's wordless talk (NpcBabble; made by «node tools/sound.js babble»): the manner of real reading and where each voice's files hum
    if (tw[0] === '@mannerstep') { if (!(Number(tw[1]) >= 0.004 && Number(tw[1]) <= 0.05)) say(tat, '@mannerstep <seconds a frame, 0.004 … 0.05>'); continue; }
    if (tw[0] === '@manner') { if (!/^\d+$/.test(tw[1] || '') || !/^[01]$/.test(tw[2] || '') || !/^([0-9a-f]{4}){4,}$/.test(tw[3] || '')) say(tat, '@manner <syllables> <0|1> <four hex digits a frame> (made by the tool: node tools/sound.js babble)'); continue; }
    if (tw[0] === '@hum') {
      if (!/^\d+$/.test(tw[3] || '') || !/^\d+$/.test(tw[4] || '') || !(Number(tw[5]) > 40)) say(tat, '@hum <file> <kind> <first sample> <samples> <Hz>');
      else if (!fs.existsSync(path.join(story, 'sounds', tw[1] || ''))) say(tat, 'no file «' + tw[1] + '» in assets/story/sounds: its hums are of a file that is gone (node tools/sound.js babble again)');
      continue;
    }
    if (tw[0] !== '@timbre') { say(tat, 'a line on «@» is «@timbre <name> …», «@pool <number>», the tool\'s «@none <sound>__<timbre>» or the babble\'s «@manner», «@mannerstep», «@hum»'); continue; }
    if (!/^[a-z][a-z0-9]*(_[a-z0-9]+)*$/.test(tw[1] || '')) { say(tat, 'a timbre\'s name: small latin letters, digits, single «_»'); continue; }
    if (timbres.has(tw[1])) say(tat, 'written twice');
    const t = { shaped: false, whose: false };
    for (const x of tw.slice(2)) {
      const [k, v] = x.split('=');
      if (!timbreWords.includes(k)) say(tat, 'no word «' + x + '» (' + timbreWords.join(' ') + ')');
      else if (k === 'only') t.whose = true;
      else if (k === 'hero') { t.hero = true; if (!tw.some(x => x.startsWith('for='))) say(tat, 'hero: the voice of the hero says for=man|woman (it goes to a player of that sex)'); if (tw.includes('only')) say(tat, 'hero and only: a voice of the hero is given by name to nobody'); }
      else if (k === 'for') { t.whose = true; if (v !== 'man' && v !== 'woman') say(tat, 'for=man or for=woman'); }
      else if (k === 'age') { for (const a of (v || '').split(',')) if (!['young', 'grown', 'old'].includes(a)) say(tat, 'age: young, grown, old — not «' + a + '»'); }
      else if (k === 'pitch') { if (!/^\d*\.?\d+(\.\.\d*\.?\d+)?$/.test(v || '')) say(tat, 'pitch=0.95..1.05'); }
      else if (k === 'rec') t.rec = true; // (a voice recorded by another person: its own files, nothing made of it)
      else if (k === 'of') t.of = v || ''; // (made of that recorded voice's takes, not of the first recording's: held to it below)
      else if (k === 'creak') { if (!(Number(v) >= 40 && Number(v) <= 120)) say(tat, 'creak=<Hz, 40 … 120>: under it this recorded voice creaks'); if (!tw.includes('rec')) say(tat, 'creak= belongs to a recorded voice\'s «rec» line: its timbres (of=) are made by it'); }
      else if (k === 'like') { t.shaped = true; if (!/^\w+$/.test(v || '')) say(tat, 'like=<a voice of the speech model: Speaker_53>'); }
      else { t.shaped = true; if (isNaN(Number(v))) say(tat, x + ': not a number'); }
    }
    if (!t.whose) say(tat, 'neither for=man|woman nor «only»: nobody would get it');
    timbres.set(tw[1], t);
    continue;
  }
  const w = l.split(/\s+/), at = 'sounds/' + f + ' «' + w[0] + '»', e = { tokens: [], flags: [] };
  if (!/^\w+$/.test(w[0])) say(at, 'a name: latin letters, digits, «_» only');
  if (/^sfx_/i.test(w[0])) say(at, 'a name on «sfx_» hides the game\'s own sound');
  if (sceneWords.includes(w[0])) say(at, 'a word of the scenes cannot be a name');
  if (bank.has(w[0])) say(at, 'written twice: the last would win without a word');
  for (const t of w.slice(1)) {
    const opt = /^(\w+)=(.*)$/.exec(t);
    if (opt) {
      if (!['vol', 'pitch', 'spread', 'range'].includes(opt[1])) say(at, 'no option «' + t + '» (vol= pitch= spread= range= 2d wet)');
      else if (!(+opt[2] >= 0)) say(at, t + ': not a number');
      else e.flags.push(opt[1]);
    } else if (t === '2d' || t === 'wet') e.flags.push(t);
    else if (t.startsWith('file:')) { if (!soundFile.test(t)) say(at, t + ': .ogg, .wav or .mp3'); else if (!fs.existsSync(path.join(story, 'sounds', t.slice(5)))) say(at, 'no file ' + t.slice(5) + ' in assets/story/sounds'); e.tokens.push(t); }
    else e.tokens.push(t);
  }
  if (!e.tokens.length) say(at, 'no sound in it');
  // a game's sound under a wrong name is silence with no word: at least one of the line must be there (the others stand in on another version)
  if (haveList && e.tokens.length && !e.tokens.some(t => !t.startsWith('sfx_') || gameSfx.has(t))) say(at, 'none of ' + e.tokens.join(', ') + ' is in the game (tools/game_sounds.txt)');
  bank.set(w[0], e);
}
for (const [name, t] of timbres) if (t.of !== undefined) {
  const of = timbres.get(t.of);
  if (!of || !of.rec) say('sounds «@timbre ' + name + '»', 'of=' + t.of + ': no recorded voice (a «@timbre … rec» line) is named so');
  if (t.rec) say('sounds «@timbre ' + name + '»', '«rec» is a recording as it is: a voice made of another (of=) is not one');
  else if (!t.shaped) say('sounds «@timbre ' + name + '»', 'of=' + t.of + ' and no word that makes it another voice (tone= throat= air= tilt= tremor= like=)');
}
for (const [f, name] of npcTimbres) if (!timbres.has(name)) say('npc/' + f, 'timbre «' + name + '»: no such «@timbre» line in assets/story/sounds');
  else if (timbres.get(name).hero) say('npc/' + f, 'timbre «' + name + '»: a voice of the hero (hero) — a person is not given it');
// every wordless voice sound is made in every made timbre, or the tool has said it cannot be («@none»): a sound or a timbre added and the
// tool not run again would leave one person silent where another is heard («node tools/sound.js timbres»)
for (const [name] of bank) if (name.startsWith('speech_') && !name.includes('__')) for (const [t, d] of timbres) if (d.shaped && !bank.has(name + '__' + t) && !notMade.has(name + '__' + t)) say('sounds «' + name + '»', 'not made in the timbre «' + t + '»: run «node tools/sound.js timbres»');
for (const [name] of bank) { const m = /^(speech_.+?)__(.+)$/.exec(name); if (m && (!timbres.has(m[2]) || !bank.has(m[1]))) say('sounds «' + name + '»', 'a sound of a timbre or of a voice sound that is not written any more: run «node tools/sound.js timbres»'); }
const fileWhy = name => fs.existsSync(path.join(story, 'sounds', name.slice(5))) ? null : 'no file ' + name.slice(5) + ' in assets/story/sounds';
const sfxWhy = name => haveList && name.startsWith('sfx_') && !gameSfx.has(name) ? 'no sound «' + name + '» in the game (tools/game_sounds.txt; written by the game\'s «Звуки» tab)' : null;
const soundWhy = name => name.startsWith('file:') ? fileWhy(name) : name === '-' || bank.has(name) || name.includes('/') ? null : name.startsWith('sfx_') ? sfxWhy(name) : 'no sound «' + name + '» in assets/story/sounds (sfx_… or Creature/kind for the game\'s own)';
// a track: of the catalogue, or the game's (its list known, it is held to it)
const trackWhy = name => {
  if (bank.has(name)) {
    const e = bank.get(name), bad = e.flags.filter(x => x === 'range' || x === '2d' || x === 'wet');
    if (bad.length) return 'music «' + name + '»: ' + bad.join(', ') + ' mean nothing for music';
    const track = t => t.startsWith('file:') || t.startsWith('game:') || (haveList ? gameTracks.has(t) : !t.startsWith('sfx_') && !t.includes('/'));
    return e.tokens.some(track) ? null : 'music «' + name + '»: none of ' + e.tokens.join(', ') + ' is a track or a file (the game would stay silent)';
  }
  if (name.startsWith('file:')) return fileWhy(name);
  const n = name.startsWith('game:') ? name.slice(5) : name;
  return haveList ? (gameTracks.has(n) ? null : 'no music «' + name + '» in the game or assets/story/sounds') : (name.startsWith('sfx_') || name.includes('/') ? 'music «' + name + '»: not a track' : null);
};
const bedWhy = name => bank.has(name) || name.startsWith('game:') || gameTracks.has(name) ? null : soundWhy(name);

// the words a scene's step may begin with, as the game knows them (else the game leaves the line out: said only in its log)
const srcDir = path.join(__dirname, '..', 'src');
const code = fs.readdirSync(srcDir).filter(f => f.endsWith('.cs') && !f.startsWith('BodyClips')).map(f => fs.readFileSync(path.join(srcDir, f), 'utf8')).join('\n'); // (any module may register a step)
// the conditions' and actions' first words as the code registers them (StoryCond, StoryActions — built in and modules')
const condWords = new Set([...((/Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>\s*\{([\s\S]*?)\n        \};/.exec(code.slice(code.indexOf('internal static class StoryCond'))) || [])[1] || '').matchAll(/\{ "(\w+)", new Kind/g)].map(m => m[1]).concat([...code.matchAll(/StoryCond\.Register\("(\w+)"/g)].map(m => m[1])));
const actWords = new Set([...((/Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>\s*\{([\s\S]*?)\n        \};/.exec(code.slice(code.indexOf('internal static class StoryActions'))) || [])[1] || '').matchAll(/\{ "(\w+)", new Kind/g)].map(m => m[1]).concat([...code.matchAll(/StoryActions\.Register\("(\w+)"/g)].map(m => m[1])));
const prefixes = []; // (StoryWhere.Prefixed, read from the code)
for (const f of fs.readdirSync(srcDir).filter(f => f.endsWith('.cs'))) { // (a word, or a const string of the same file naming it)
  const t = fs.readFileSync(path.join(srcDir, f), 'utf8');
  for (const m of t.matchAll(/StoryWhere\.Prefixed\[(?:"([\w.]+)"|(\w+))\]/g)) prefixes.push(m[1] || (new RegExp('const string ' + m[2] + ' = "([^"]+)"').exec(t) || [])[1] || '');
  if (f === 'StoryWhere.cs') for (const m of t.matchAll(/\{ "([\w]+\.)", \(string rest, out Vector3 at\)/g)) prefixes.push(m[1]); // (its own, in the table's initializer)
}
// the places the place conditions name (placed, gate, hearth, state, been, near, inside, npcat)
const condPlace = { placed: 1, gate: 1, hearth: 1, state: 1, been: 1, near: 1, inside: 1, npcat: 1 };
// the kinds that ask only the world (Kind.World): the server's rules may use nothing else — it has no player, and there the rule would never hold
const worldWords = new Set();
// «step|reached|quest <quest> …» of a quest of the server: the world's too (its stage is kept in the world; it stays reached when the quest is over)
const questWorldWords = new Set(), worldQuests = new Set();
for (const f of txt('quests')) { let q = ''; for (const l of lines('quests', f)) { const m = /^\[quest\s+(\S+)\]/.exec(l); if (m) q = m[1]; else if (q && /^owner:\s*server/.test(l)) worldQuests.add(q); } }
{
  const regs = [...code.matchAll(/StoryCond\.Register\("(\w+)"/g)];
  regs.forEach((m, i) => {
    const body = code.slice(m.index, i + 1 < regs.length ? regs[i + 1].index : m.index + 800).split(/\n\s*\n/)[0].slice(0, 900);
    if (/\bWorld = true\b/.test(body)) worldWords.add(m[1]);
    if (/\bWorldIf = OfServer\b/.test(body)) questWorldWords.add(m[1]); // (Kind.WorldIf: the world's when its quest is the server's — StoryTrackConds.OfServer)
  });
}
// how a word is written, from its own «Usage» in the code: how many words it needs, and the words of a place that takes only a few («gate <місце> open|shut»)
const usages = (text, cls) => {
  const out = {};
  for (const m of text.matchAll(new RegExp('"(\\w+)",\\s*new (?:' + cls + '\\.)?Kind\\s*\\{[^"]*?Usage\\s*=\\s*"((?:[^"\\\\]|\\\\.)*)"', 'g'))) {
    const min = /\bMin\s*=\s*(\d+)/.exec(text.slice(m.index, m.index + m[0].length + 200).split(/\bCheck\s*=|\bTest\s*=|\bDo\s*=/)[0]);
    const tokens = m[2].split(/\s{2,}|\(/)[0].trim().split(/\s+/).slice(1), enums = {};
    for (let i = 0; i < tokens.length; i++) {
      const optional = /^\[.*\]$/.test(tokens[i]), t = tokens[i].replace(/^\[|\]$/g, '');
      if (/^[\w.-]+(\|[\w.-]+)+$/.test(t)) enums[i + 1] = t.split('|');
      else if (optional) break; // (what follows an optional word stands at no fixed place)
    }
    out[m[1]] = { min: min ? +min[1] : 0, enums, usage: m[2].split(/\s{2,}/)[0] };
  }
  return out;
};
const sectionOf = (cls) => code.slice(code.indexOf('internal static class ' + cls));
const condUsage = Object.assign(usages((/Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>\s*\{([\s\S]*?)\n        \};/.exec(sectionOf('StoryCond')) || [])[1] || '', 'StoryCond'), usages(code, 'StoryCond'));
const actTable = (/Dictionary<string, Kind> Kinds = new Dictionary<string, Kind>\s*\{([\s\S]*?)\n        \};/.exec(sectionOf('StoryActions')) || [])[1] || '';
const actVerbs = new Set([...(actTable.slice(actTable.indexOf('Verbs = new Dictionary', actTable.indexOf('{ "npc", new Kind')), actTable.indexOf('{ "gate", new Kind')).matchAll(/\{ "(\w+)", new Kind/g))].map(m => m[1]));
const actUsage = Object.assign(usages(actTable, 'StoryActions'), usages(code, 'StoryActions'));
delete actUsage.npc; // (its verbs have their own check: actVerbs)
const wordsWhy = (at, kind, u, w) => {
  if (!u) return;
  if (w.length - 1 < u.min) { say(at, '«' + w.join(' ') + '»: too few words (' + u.usage + ')'); return; }
  for (const i of Object.keys(u.enums)) if (w[i] !== undefined && !u.enums[i].includes(w[i])) say(at, kind + ' «' + w.join(' ') + '»: «' + w[i] + '» is none of ' + u.enums[i].join(', '));
};
// the story's own words: what is asked anywhere, and what sets it anywhere (a typo in either: a stage waits for what never comes)
const knowAsked = []; // every condition asked anywhere, with its place: «asked», «knows», «lived», «read» are checked once the talks, tales and quests are read
const asked = { flag: [], world: [], var: [], wvar: [], stamp: [] }, setWords = { flag: new Set(), world: new Set(), var: new Set(), wvar: new Set(), stamp: new Set() };
// the actions that make a fact of their own (StoryActions.Kind «Dry = "set world world:{N}"», as quest_dry reads it: its N-th word is the fact —
// «waves <name> …»; «set me flag:{N}» the character's)
const drySets = {};
for (const m of code.matchAll(/"(\w+)",\s*new (?:StoryActions\.)?Kind\s*\{\s*Dry = "set (world world|me flag):\{(\d)\}"/g)) drySets[m[1]] = { kind: m[2] === 'me flag' ? 'flag' : 'world', at: +m[3] };
const noteAsked = (at, w) => { if (w[0] === 'since' && w[1]) asked.stamp.push([at, w[1]]); else if (asked[w[0]] && w[0] !== 'stamp' && w[1]) asked[w[0]].push([at, w[1]]); };
const raisedInData = new Set();
const condsWhy = (at, text, worldOnly) => {
  if (!text) return;
  for (const c of text.split(',')) {
    const w = c.trim().replace(/^!/, '').split(/\s+/);
    if (!w[0]) continue;
    if (!condWords.has(w[0])) { say(at, 'no condition «' + w[0] + '» (StoryCond)'); continue; }
    if (worldOnly && !worldWords.has(w[0]) && !(questWorldWords.has(w[0]) && worldQuests.has(w[1]))) say(at, '«' + w[0] + '» asks a player, and the server asks this with none: only ' + [...worldWords].join(', ') + '; ' + [...questWorldWords].join(', ') + ' of a quest of the server (' + [...worldQuests].join(', ') + ')');
    if (condPlace[w[0]] && w[1]) { const why = placeWhy(w[1]); if (why) say(at, w[0] + ': ' + why); }
    wordsWhy(at, 'condition', condUsage[w[0]], w);
    noteAsked(at, w);
    knowAsked.push([at, w]);
  }
};
const actsWhy = (at, text) => {
  if (!text) return;
  for (const a of text.split(';')) {
    const w = a.trim().split(/\s+/);
    if (!w[0]) continue;
    if (!actWords.has(w[0]) || actVerbs.has(w[0])) { say(at, 'no action «' + w[0] + '» (StoryActions)'); continue; }
    if (w[0] === 'npc' && !actVerbs.has(w[2])) { say(at, 'npc: no word «' + w[2] + '» (' + [...actVerbs].join(', ') + ')'); continue; }
    wordsWhy(at, 'action', actUsage[w[0]], w);
    if (w[0] === 'give') { const why = itemWhy(w[1]); if (why) say(at, why); }
    if (setWords[w[0]] && w[1]) setWords[w[0]].add(w[1]);
    const ds = drySets[w[0]]; if (ds && w[ds.at]) setWords[ds.kind].add(w[ds.at]); // (an action that makes a fact of its own: its «Dry = "set world {N}"» in the code — waves)
    if (w[0] === 'raise' && w[1]) raisedInData.add(w[1].toLowerCase());
  }
};
{ // a line of the list the code has no word for (the game says it only in its log)
  const table = (/Dictionary<string, LineFn> Lines = new Dictionary<string, LineFn>\s*\{([\s\S]*?)\n        \};/.exec(code) || [])[1] || '';
  const words = new Set([...table.matchAll(/^\s*\{ "(\w+)", \(d, v, w\)/gm)].map(m => m[1]));
  let at = '';
  if (words.size > 0) for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    const k = /^(\w+):/.exec(l);
    if (k && !words.has(k[1])) say('blueprints/locations.list.txt [' + at + ']', 'no line «' + k[1] + ':» in Locs.Lines');
  }
}
for (const [at, kind, val] of placeRules) {
  const where = 'blueprints/locations.list.txt [' + at + '] ' + kind;
  if (kind === 'elsedo') { actsWhy(where, val); continue; }
  if (kind === 'fog' && /^(yes|no|так|ні|0|1)$/.test(val)) continue;
  const text = kind === 'litwhile' || kind === 'hidewhile' || kind === 'touch' ? val.split(/\s+/).slice(1).join(' ').replace(/^(anim |quiet )+/, '') :
               kind === 'statewhile' || kind === 'becomes' || kind === 'follows' ? val.split(/\s+/).slice(1).join(' ') :
               kind === 'read' ? (val.includes('::') ? val.slice(val.split(/\s+/)[0].length, val.indexOf('::')) : '') :
               kind === 'hearthwait' || kind === 'hearthgivewait' || kind === 'insideshut' ? (val.includes('::') ? val.slice(0, val.indexOf('::')) : val) : val;
  if (text.trim() !== '-') condsWhy(where, text.trim(), kind === 'becomes' || kind === 'follows' || kind === 'cold' || kind === 'fog');
}
// «raven: <look> <piece> <conditions>» (LocRaven, the Hall's raven): its look is a file of looks, its piece a name of the place's blueprint, its
// conditions words of the game; and a «ravenroof:» says where it goes out and in (without one it would go through the roof over its home piece)
{
  let at = '', raven = false, roof = false;
  const close = () => { if (raven && !roof) say('blueprints/locations.list.txt [' + at + ']', 'raven: without «ravenroof: x y z» — the point under its roof it goes out and in through'); };
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { close(); at = m[1]; raven = roof = false; continue; }
    if (/^ravenroof:\s*\S+\s+\S+\s+\S+/.test(l)) roof = true;
    const r = /^raven:\s*(\S+)\s+(\S+)\s*(.*)$/.exec(l);
    if (!r) continue;
    raven = true;
    const where = 'blueprints/locations.list.txt [' + at + '] raven';
    if (!fs.existsSync(path.join(story, 'looks', r[1] + '.txt'))) say(where, 'no look «' + r[1] + '» in assets/story/looks');
    const names = path.join(story, 'blueprints', at + '.names.txt');
    if (!fs.existsSync(names) || !fs.readFileSync(names, 'utf8').split(/\r?\n/).some(n => n.split(/\s+/)[0] === r[2])) say(where, 'no piece named «' + r[2] + '» in blueprints/' + at + '.names.txt: its word has nowhere to live');
    if (r[3].trim()) condsWhy(where, r[3].trim(), false); else say(where, 'no conditions: when does it live?');
  }
  close();
}
// «sitsay: <place> <tag> [conditions]» (LocSitSay): the place is there, the conditions are words of the game, and both ravens have that tag
// (a tag the books lack says nothing, and nothing tells why)
{
  let at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    const s = /^sitsay:\s*(\S+)(?:\s+(\S+))?(.*)$/.exec(l);
    if (!s) continue;
    const where = 'blueprints/locations.list.txt [' + at + '] sitsay';
    if (!s[2]) { say(where, 'sitsay: <place> <tag> [conditions]'); continue; }
    const why = placeWhy(s[1]); if (why) say(where, why);
    if (s[3].trim()) condsWhy(where, s[3].trim());
    for (const who of ['Hugin', 'Munin']) if (!new RegExp('internal static readonly string\\[\\] ' + who + ' =[^;]*"\\[' + s[2] + '\\]').test(code)) say(where, 'no tag «' + s[2] + '» in ' + who + '\'s words (src/*Patterns.cs)');
  }
}
// a step's second word, where its «Usage» allows only a few in every form («bars on|off», «watchers take … · watchers free»)
const stepSecond = {};
for (const m of code.matchAll(/"(\w+)",\s*new (?:StoryScenes\.)?StepKind\([^)]*\)\s*\{[^"]*?Usage\s*=\s*"((?:[^"\\]|\\.)*)"/g)) {
  const words = new Set();
  let fixed = true;
  for (const form of m[2].split(/\s{2,}|\(/)[0].split(' · ')) {
    const t = form.trim().split(/\s+/);
    if (t[0] !== m[1] || !t[1] || !/^[\w.-]+(\|[\w.-]+)*$/.test(t[1])) { fixed = false; break; }
    t[1].split('|').forEach(x => words.add(x));
  }
  if (fixed && words.size > 0) stepSecond[m[1]] = words;
}
// the steps and their verbs, as the director's own entries say them (StoryScenes: StepKind, StepVerb): the words, and which word names a place
const entriesIn = (text, type) => {
  const out = {}, ms = [...text.matchAll(new RegExp('\\{ "(\\w+)", new ' + type + '\\b', 'g'))];
  ms.forEach((m, i) => {
    const body = text.slice(m.index, i + 1 < ms.length ? ms[i + 1].index : text.length);
    const at = /\bPlaceAt = (\d+)/.exec(body), after = /\bPlaceAfter = "(\w+)"/.exec(body), not = /\bPlaceNot = new\[\] \{([^}]*)\}/.exec(body);
    out[m[1]] = { at: at ? +at[1] : 0, after: after ? after[1] : null, not: not ? not[1].match(/\w+/g) : [] };
  });
  return out;
};
const stepWords = new Set([...code.matchAll(/\{ "(\w+)", (?:new StepKind|air|\w+Kind\(\))/g)].map(m => m[1]).concat([...code.matchAll(/StoryScenes\.Register\("(\w+)"/g)].map(m => m[1])));
// a step registered with its word not written out in that line («Register(word, …)») cannot be read above: every scene using it was stopped as
// «no step» while the game had it («unmute», 2026-11-13: registered through a helper)
for (const r of code.matchAll(/StoryScenes\.Register\(\s*([A-Za-z_]\w*)\s*,/g)) say('src', 'StoryScenes.Register(' + r[1] + ', …): write the word of the step out in that line ("word"), this check reads the words from there');
const builtin = (/private static Dictionary<string, StepKind> Builtin\(\)([\s\S]*?)private static string Check\(Step st\)/.exec(code) || [])[1] || '';
const stepPlaces = entriesIn(builtin, 'StepKind');
const verbsOf = {}; // step → { at: which word is the verb, verbs: its entries }
for (const m of code.matchAll(/\{ "(\w+)", (\w+Kind)\(\) \}/g)) {
  const fn = new RegExp('private static StepKind ' + m[2] + '\\(\\)([\\s\\S]*?)\\n        \\}\\n').exec(code);
  if (!fn) { say('src', 'no ' + m[2] + '()'); continue; }
  verbsOf[m[1]] = { at: +(/VerbAt = (\d+)/.exec(fn[1]) || [0, 1])[1], verbs: entriesIn(fn[1], 'StepVerb') };
}
// a step whose module lists its own words (`const string XStep = "prop take"`, `XFlags = { … }, XValued = { … }`): after its
// words and one argument, every word is one of them (a value after the valued ones). A typo there the game only says in a scene
const stepOptions = [...code.matchAll(/const string (\w+)Step = "([^"]+)";[\s\S]*?\b\1Flags = \{([^}]*)\}, \1Valued = \{([^}]*)\}/g)]
  .map(m => ({ step: m[2].split(' '), flags: (m[3].match(/"[^"]+"/g) || []).map(x => x.slice(1, -1)), valued: (m[4].match(/"[^"]+"/g) || []).map(x => x.slice(1, -1)) }));
function optionsWhy(at, all) {
  const w = all[0] === 'npc' ? all.slice(2) : all; // (a person's own: «npc <who> prop take …», read from the same word on: StoryScenesCast)
  for (const o of stepOptions) {
    if (o.step.some((x, i) => w[i] !== x)) continue;
    const arg = o.step.length;
    if (!w[arg]) { say(at, o.step.join(' ') + ': what? no word after it (the thing it is about)'); continue; }
    { const why = itemWhy(w[arg]); if (why) say(at, why); }
    for (let i = arg + 1; i < w.length; i++) {
      if (o.flags.includes(w[i])) continue;
      if (o.valued.includes(w[i]) && i + 1 < w.length) { if (w[i] === 'size' && !(+w[i + 1] >= 0)) say(at, o.step.join(' ') + ': size «' + w[i + 1] + '» is not a number of metres'); i++; continue; }
      say(at, o.step.join(' ') + ': «' + w[i] + '» is none of ' + o.flags.concat(o.valued.map(x => x + ' <…>')).join(', ') + ': the game stops the step in the scene');
    }
  }
}
// a held thing's own numbers in its item file («hang:», «lies:», «falls:»: StoryProps.OwnOf — a word and its number, in pairs): the words as
// the code lists them. The game reads such a line no further than a word it does not know, and says so only in its log
{
  const m = /HangWords = \{([^}]*)\}, LiesWords = \{([^}]*)\}, FallsWords = \{([^}]*)\}/.exec(code);
  const own = m ? { hang: m[1].match(/\w+/g) || [], lies: m[2].match(/\w+/g) || [], falls: m[3].match(/\w+/g) || [] } : null;
  if (!own) say('src', 'no HangWords, LiesWords, FallsWords (StoryProps): the own lines of items — hang:, lies:, falls: — are not checked');
  else for (const [d, name] of [[path.join(story, '..', 'items'), 'items'], [path.join(story, 'items'), 'story/items']]) for (const f of fs.existsSync(d) ? fs.readdirSync(d).filter(x => x.endsWith('.txt')) : []) {
    fs.readFileSync(path.join(d, f), 'utf8').split(/\r?\n/).forEach((raw, n) => {
      const k = /^(hang|lies|falls):\s*(.*)$/.exec(raw.replace(/\s+#.*$/, '').trim());
      if (!k) return;
      const w = k[2].split(/\s+/).filter(Boolean), at = name + '/' + f + ':' + (n + 1);
      for (let i = 0; i < w.length; i += 2) {
        if (!own[k[1]].includes(w[i])) { say(at, k[1] + ': «' + w[i] + '» is none of ' + own[k[1]].join(', ') + ': the game reads the line no further'); break; }
        if (w[i + 1] === undefined || isNaN(Number(w[i + 1]))) { say(at, k[1] + ': ' + w[i] + ': a number after it'); break; }
      }
    });
  }
}
// the words after a sound's name and place (StoryAudio reads these and passes over any other without a word)
const audioWords = {
  sound: { flags: ['2d', 'wet', 'follow', 'wait'], valued: ['vol', 'range', 'pitch', 'delay', 'fadein', 'skip'] },
  bed: { flags: ['2d', 'wet', 'follow', 'all'], valued: ['vol', 'range', 'pitch'], fade: true },
  music: { flags: ['once', 'wait', 'all'], valued: ['keep', 'rest', 'vol'], fade: true },
};
function audioWordsWhy(at, w) {
  const o = audioWords[w[0]];
  if (!o || !w[1] || w[1] === 'stop' || w[1] === 'off' || w[1] === 'game') return;
  for (let i = w[0] !== 'music' && stepPlace(w) ? 3 : 2; i < w.length; i++) {
    if (o.flags.includes(w[i])) continue;
    if (o.valued.includes(w[i])) { if (isNaN(Number(w[i + 1]))) say(at, w[0] + ': ' + w[i] + ': a number after it'); i++; continue; }
    if (o.fade && !isNaN(Number(w[i]))) continue;
    say(at, w[0] + ': «' + w[i] + '» is none of ' + o.flags.concat(o.valued.map(x => x + ' <…>')).join(', ') + ': the game would pass over it');
  }
}
function stepPlace(w) {
  if (w[0] === 'sound') return w[1] === 'stop' || ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'wait', 'delay', 'fadein', 'skip'].includes(w[2]) ? null : w[2]; // (a module's own: StoryAudio.PlaceOf)
  if (w[0] === 'bed') return w.length < 3 || w[1] === 'off' || !isNaN(Number(w[2])) || ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'all'].includes(w[2]) ? null : w[2]; // (StoryAudio.BedPlaceOf)
  if (w[0] === 'prop' && w[1] === 'throw') return w[2] || null; // (where a thing is thrown: StoryProps.PlaceOf, the step's own)
  if (w[0] === 'npc' && w[2] === 'prop' && w[3] === 'throw') return w[4] || null;
  const v = verbsOf[w[0]];
  const e = v ? v.verbs[w[v.at]] : stepPlaces[w[0]];
  if (!e) return null;
  let i = e.at;
  if (e.after) { i = w.indexOf(e.after); i = i > 0 ? i + 1 : 0; }
  return i > 0 && i < w.length && !e.not.includes(w[i]) ? w[i] : null;
}

// scenes
const scenes = new Set(), used = new Set();
const sceneOwn = new Set(); // (a scene's own names for people it makes of another template: «falcon c become sestra_3 as sestra_2»)
for (const f of txt('scenes')) {
  let at = f;
  for (const l of lines('scenes', f)) {
    if (!l) continue;
    const head = /^\[scene\s+(\S+)\]/.exec(l);
    if (head) { if (scenes.has(head[1])) say(f, 'scene «' + head[1] + '» twice'); scenes.add(head[1]); at = f + ' [' + head[1] + ']'; sceneOwn.clear(); if (f.startsWith('_')) for (const n of authorNpcs) sceneOwn.add(n); continue; }
    const step = l.replace(/^if [^:]*:\s*/, '');
    const w = step.split(/\s+/);
    if (w[0] === 'place:' || w[0] === 'check:') { const why = placeWhy(w[1]); if (why) say(at, w[0] + ' ' + why); continue; }
    if (w[0] === 'needs:') { condsWhy(at, step.slice(6).trim()); continue; }
    if (w[0] === 'test:') { for (const a of step.slice(5).split(';')) { const t = a.trim(); if (t && !t.startsWith('npc ') && !actWords.has(t.split(/\s+/)[0])) say(at, 'test: no action «' + t.split(/\s+/)[0] + '»'); } continue; }
    { const iff = /^if ([^:]*):/.exec(l); if (iff) condsWhy(at, iff[1]); }
    if (w[0] === 'do') { actsWhy(at, step.slice(3)); if (w[1] === 'npc' && w[2] && !npcs.has(w[2])) say(at, 'do npc: no person «' + w[2] + '» in assets/story/npc (a word kept in the world goes by its template id, not a tag)'); }
    if (w[0] === 'until') condsWhy(at, step.slice(6).split('|')[0]);
    if (w[0].endsWith(':')) continue; // (needs:, alone:, guard:)
    if (stepWords.size > 0 && !stepWords.has(w[0])) say(at, 'no step «' + w[0] + '»: the game would leave the line out');
    if (stepSecond[w[0]] && !stepSecond[w[0]].has(w[1])) say(at, w[0] + ': «' + w[1] + '» is none of ' + [...stepSecond[w[0]]].join(', ') + ': the game would leave the line out');
    { const v = verbsOf[w[0]]; if (v && w[v.at] && !v.verbs[w[v.at]]) say(at, w[0] + ': no word «' + w[v.at] + '» (' + Object.keys(v.verbs).join(', ') + ')' + (w[0] === 'npc' ? '; a standing word: do npc … hold' : '')); }
    if (w[0] === 'do' && w[1] === 'npc' && w[3] === 'hold') for (const pl of holdPlaces(w.slice(4))) { const why = placeWhy(pl); if (why) say(at, 'hold ' + why); }
    if (w[0] === 'say' || (w[0] === 'bird' && w[1] === 'say')) continue; // (a line of speech is free text)
    if (w[0] === 'falcon' && w[2] === 'become') { const asAt = w.indexOf('as'); if (asAt > 3) { if (!npcs.has(w[asAt + 1])) say(at, 'falcon become … as: no person «' + w[asAt + 1] + '» in assets/story/npc'); sceneOwn.add(w[3]); } else if (w[3] && !npcs.has(w[3])) say(at, 'falcon become: no person «' + w[3] + '» in assets/story/npc'); }
    if (w[0] === 'npc' && w[2] === 'ready') { const asAt = w.indexOf('as'); if (asAt > 3) { if (!npcs.has(w[asAt + 1])) say(at, 'npc … ready … as: no person «' + w[asAt + 1] + '» in assets/story/npc'); sceneOwn.add(w[1]); } } // (made beforehand, unseen: its own name of another template, as «falcon become … as»)
    if (w[0] === 'music' && w.includes('rest') && !w.includes('keep')) say(at, 'music … rest: works only with «keep N» (a scene\'s music ends with the scene: its silence would never come)');
    // a vision's picture (StoryScreen.Memory): the file is there, or the game drops the scene at its own check (2026-11-15)
    if (w[0] === 'memory' && w[1] && !['.png', '.jpg'].some(x => fs.existsSync(path.join(story, 'memories', w[1] + x)))) say(at, 'memory: no picture «' + w[1] + '» (assets/story/memories/<name>.png|.jpg)');
    if (w[0] === 'npc' && w.length > 1 && !npcs.has(w[1]) && !sceneOwn.has(w[1])) say(at, 'no person «' + w[1] + '» in assets/story/npc');
    const place = stepPlace(w);
    if (place) { const why = placeWhy(place); if (why) say(at, why); }
    const heard = w[0] === 'sound' && w[1] !== 'stop' ? w[1] : w[0] === 'npc' && w[2] === 'fade' ? w.find((x, i) => i > 2 && isNaN(Number(x))) : null;
    if (heard) { const why = soundWhy(heard); if (why) say(at, why); }
    if (w[0] === 'music' && w[1] && w[1] !== 'off' && w[1] !== 'game') { const why = trackWhy(w[1]); if (why) say(at, why); }
    if (w[0] === 'bed' && w[1] && w[1] !== 'off') { const why = bedWhy(w[1]); if (why) say(at, why); }
    audioWordsWhy(at, w);
    checkDotted(at, w);
    if (w[0] === 'camera') { const why = require('./scene_dry.js').cameraWhy(w); if (why) say(at, 'camera: ' + why + ' — the game leaves the whole step out, and nothing on the screen says so'); } // (the words of camera on|shoulder|two, by the game's own lists: a typo was seen only as a missing shot, 2026-11-11)
    optionsWhy(at, w);
  }
}

// the saga's chapters (assets/story/saga): a quest or tale with saga lines names one that is there
const chapters = new Set();
const sagaLived = new Set(); // «quest.stage[.way]» with a «saga:» line (a quest of the server's: kept in those who were there): what the condition «lived» may name
for (const f of txt('saga')) for (const l of lines('saga', f)) { const m = /^\[chapter\s+(\S+)\]/.exec(l); if (m) { if (chapters.has(m[1])) say('saga/' + f, 'chapter «' + m[1] + '» twice'); chapters.add(m[1]); } }
const chapterWhy = (at, chapter, saga) => {
  if (saga && !chapter) say(at, 'has saga lines but no «chapter:» (the book would not know where to write them)');
  else if (chapter && !chapters.has(chapter)) say(at, 'no chapter «' + chapter + '» in assets/story/saga');
};

// quests
const questIds = new Set();
for (const f of txt('quests')) for (const l of lines('quests', f)) { const m = /^\[quest\s+(\S+)\]/.exec(l); if (m) questIds.add(m[1]); }
const personalActs = ['flag', 'unflag', 'var', 'stamp', 'spot', 'choose', 'goto', 'end', 'book', 'give'], serverScenes = new Set(), serverQuests = new Set();
// the fields a quest file may have, as its reader knows them (StoryQuestsRead: an unknown one is only a line in the game's log)
const questRead = fs.readFileSync(path.join(srcDir, 'StoryQuestsRead.cs'), 'utf8');
const switches = questRead.split('switch (key)');
const casesOf = t => new Set([...(t || '').split('default:')[0].matchAll(/case "(\w+)":/g)].map(m => m[1]));
const questFields = casesOf(switches[1]), stageFields = casesOf(switches[2]);
const wayFields = new Set([...((/if \(w != null && (key != [^\n]*?)\)\s*\n/.exec(questRead) || [])[1] || '').matchAll(/key != "(\w+)"/g)].map(m => m[1]));
// the things the story puts on the ground or at the feet (a scene's «drop», «do give» — no room in the bag: at the feet; a quest's «lay», «give»): a server's
// cleaning or another mod may take them away, so a stage that waits for one has «lost:» (StoryLost) — the user 2026-11-16: «щоб не було софт локу»
// Only one laid by that stage or one before it counts (Ketil's necklace waits in a chest at «house»; it is laid on his bed only at «night», later)
const sceneLays = new Map(); // scene → [[item, where]]
for (const f of txt('scenes')) { let sc = ''; lines('scenes', f).forEach((l, i) => { const h = /^\[scene\s+(\S+)\]/.exec(l); if (h) { sc = h[1]; return; } const m = /^(?:if [^:]*:\s*)?(?:drop|do\s+give)\s+(\S+)/.exec(l); if (m && sc) { if (!sceneLays.has(sc)) sceneLays.set(sc, []); sceneLays.get(sc).push([m[1], 'scenes/' + f + ':' + (i + 1)]); } }); }
const laysIn = val => [...val.split(';')].map(a => /^\s*(?:lay|give)\s+(\S+)/.exec(a)).filter(Boolean).map(m => m[1]);
for (const f of txt('quests')) {
  const qs = []; let q = null, stage = null, way = null;
  for (const l of lines('quests', f)) {
    if (!l) continue;
    let m;
    if ((m = /^\[quest\s+(\S+)\]/.exec(l))) { q = { id: m[1], first: '', stages: new Set(), refs: [], chapter: '', saga: false, order: [], info: {}, server: false, forks: [], said: [] }; qs.push(q); stage = null; way = null; continue; }
    if ((m = /^\[stage\s+(\S+)\]/.exec(l))) { if (q.stages.has(m[1])) say(f, q.id + ': stage «' + m[1] + '» twice'); q.stages.add(m[1]); stage = m[1]; way = null; q.order.push(stage); q.info[stage] = { fields: new Set(), next: '', to: [], ways: [] }; continue; }
    if ((m = /^\[way\s+(\S+)\]/.exec(l))) { way = { id: m[1], fields: new Set(), next: '' }; q.info[stage].ways.push(way); continue; }
    const c = l.indexOf(':'); if (c < 0 || !q) continue;
    const key = l.slice(0, c).trim(), val = l.slice(c + 1).trim();
    const at = f + ' [' + q.id + (stage ? '.' + stage : '') + (way ? '.' + way.id : '') + ']';
    if (!(way ? wayFields : stage ? stageFields : questFields).has(key)) { say(at, 'no field «' + key + '»: the game would leave the line out' + (way ? ' (a way knows: ' + [...wayFields].join(', ') + ')' : '')); continue; }
    q.said.push([at, key, val, !!way]);
    if (!stage && key === 'owner') q.server = val === 'server';
    if (!stage && key === 'who') condsWhy(at, val);
    if (!stage && key === 'fork') q.forks.push(val.slice(val.lastIndexOf('->') + 2).trim());
    if (stage) {
      const info = way || q.info[stage];
      info.fields.add(key);
      if (key === 'next') info.next = val;
      if (key === 'wait') info.wait = val;
      if (key === 'start' || key === 'done') for (const it of laysIn(val)) (info.lays = info.lays || []).push([it, at]);
      if (key === 'scene') for (const [it, where] of sceneLays.get(val.split(/\s+/)[0]) || []) (info.lays = info.lays || []).push([it, where]);
      if (key === 'fork') q.info[stage].to.push(val.slice(val.lastIndexOf('->') + 2).trim());
      if ((key === 'start' || key === 'done') && !val.startsWith('msg ')) for (const a of val.split(';')) { const w = a.trim().split(/\s+/); if (w[0] === 'goto' && w[1]) { q.refs.push([at, w[1]]); q.info[stage].to.push(w[1]); } if (w[0] === 'end') q.info[stage].to.push('end'); }
    }
    if (key !== 'goal' && key !== 'name' && key !== 'saga' && !(key === 'start' && val.startsWith('msg '))) for (const named of val.matchAll(/\bquest\s+(\w+)/g)) if (!questIds.has(named[1])) say(at, 'no quest «' + named[1] + '»');
    if (key === 'starts' || key === 'wait') condsWhy(at, val);
    else if (key === 'fork') condsWhy(at, val.slice(0, Math.max(0, val.lastIndexOf('->'))));
    if ((key === 'start' || key === 'done') && !val.startsWith('msg ')) actsWhy(at, val);
    if (key === 'first') q.first = val;
    else if (key === 'chapter') q.chapter = val;
    else if (key === 'saga') q.saga = true;
    else if (key === 'next') q.refs.push([at, val]);
    else if (key === 'fork') { const i = val.lastIndexOf('->'); if (i >= 0) q.refs.push([at, val.slice(i + 2).trim()]); }
    else if (key === 'call') { if (!npcs.has(val)) say(at, 'no person «' + val + '» in assets/story/npc'); }
    else if (key === 'scene' || key === 'nudge') { const sc = val.split(/\s+/)[0]; used.add(sc); if (!scenes.has(sc)) say(at, 'no scene «' + sc + '»'); }
    else if (key === 'mark') { const why = placeWhy(val.split(/\s+/)[0]); if (why) say(at, why); }
    else if (key === 'seek') { const sw = val.split(/\s+/); const why = placeWhy(sw[0]); if (why) say(at, why); if (sw.length < 2) say(at, 'seek: <місце> <слова пошуку>'); }
    else if (key === 'stock') { const sw = val.split(/\s+/); if (sw.length < 4 || sw.length % 2 !== 0 || !(+sw[1] > 0) || sw.slice(2).some((x, i) => i % 2 === 1 && !(/^\d+$/.test(x) ? +x >= 1 : /^(\d+)-(\d+)$/.test(x) && +x.split('-')[1] >= Math.max(1, +x.split('-')[0])))) say(at, 'stock: <місце> <м> <Prefab> <N|A-B> [<Prefab> <N|A-B> …]'); else { const why = placeWhy(sw[0]); if (why) say(at, 'stock: ' + why); } }
    else if (key === 'lost') { const lw = val.split(/\s+/); if (lw.length !== 2) say(at, 'lost: <предмет> <місце>'); else { const why = placeWhy(lw[1]) || itemWhy(lw[0]); if (why) say(at, 'lost: ' + why); if (stage && !way) (q.info[stage].lost = q.info[stage].lost || new Set()).add(lw[0]); } }
    else if (key === 'wait' || key === 'on' || key === 'start' || key === 'done') {
      if (key === 'start' && val.startsWith('msg ')) continue; // (free text)
      const near = /\bnear\s+(\S+)/.exec(val);
      if (near) { const why = placeWhy(near[1].replace(/,$/, '')); if (why) say(at, why); }
      checkDotted(at, val.split(/[\s,]+/));
      const holds = /^npc\s+\S+\s+hold\s+(.+)/.exec(val);
      if (holds) for (const pl of holdPlaces(holds[1].split(/\s+/))) { const why = placeWhy(pl); if (why) say(at, 'hold ' + why); }
      const npc = /^npc\s+(\S+)/.exec(val);
      if (npc && !npcs.has(npc[1])) say(at, 'no person «' + npc[1] + '» in assets/story/npc');
    }
  }
  for (const qu of qs) {
    chapterWhy(f + ' [' + qu.id + ']', qu.chapter, qu.saga);
    for (const st of qu.order) { if (qu.info[st].fields.has('saga')) sagaLived.add(qu.id + '.' + st); for (const w of qu.info[st].ways) if (w.fields.has('saga')) sagaLived.add(qu.id + '.' + st + '.' + w.id); }
    if (qu.first && !qu.stages.has(qu.first)) say(f, qu.id + ': first stage «' + qu.first + '» is not there');
    for (const [at, to] of qu.refs) if (to !== 'end' && !qu.stages.has(to)) say(at, 'no stage «' + to + '»');
    // what a stage is, and where it leads: no stage nobody comes to, none that never comes to the end (no soft lock by its shape)
    const edges = {};
    qu.order.forEach((id, i) => {
      const s = qu.info[id], at = f + ' [' + qu.id + '.' + id + ']', own = s.next || (i + 1 < qu.order.length ? qu.order[i + 1] : 'end');
      edges[id] = new Set(s.to);
      qu.forks.forEach(to => { if (to !== id) edges[id].add(to); });
      if (s.ways.length === 0) {
        edges[id].add(own);
        if (!s.fields.has('goal')) say(at, 'no goal: the player would not know what to do');
        if (s.fields.has('scene') && (s.fields.has('wait') || s.fields.has('on'))) say(at, 'a stage with a scene passes when it ends: its «wait»/«on» are never asked');
      } else {
        if (s.fields.has('scene') || s.fields.has('wait') || s.fields.has('on')) say(at, 'a stage with ways waits only for its ways: put wait/on/scene in a way');
        for (const w of s.ways) {
          edges[id].add(w.next || own);
          if (!w.fields.has('goal')) say(at + ' way ' + w.id, 'no goal');
          if (!w.fields.has('wait') && !w.fields.has('on')) say(at + ' way ' + w.id, 'no wait/on: it would be chosen at once');
        }
      }
    });
    const reached = new Set(), todo = [qu.first || qu.order[0]];
    while (todo.length) { const a = todo.pop(); if (a === 'end' || reached.has(a) || !edges[a]) continue; reached.add(a); edges[a].forEach(b => todo.push(b)); }
    const ends = new Set(['end']);
    for (let again = true; again;) { again = false; for (const a of qu.order) if (!ends.has(a) && [...edges[a]].some(b => ends.has(b))) { ends.add(a); again = true; } }
    const laidBy = new Map(); // item → where the story first puts it on the ground in this quest, as the stages go
    qu.order.forEach(a => {
      for (const [it, where] of qu.info[a].lays || []) if (!laidBy.has(it)) laidBy.set(it, where);
      for (const m of (qu.info[a].wait || '').matchAll(/(?:^|,)\s*item\s+(\S+)/g))
        if (laidBy.has(m[1]) && !(qu.info[a].lost && qu.info[a].lost.has(m[1]))) say(f + ' [' + qu.id + '.' + a + ']', 'waits for «' + m[1] + '», which the story puts on the ground (' + laidBy.get(m[1]) + '): add «lost: ' + m[1] + ' <місце>» — taken away by a cleaning, it is laid there again (StoryLost), no soft lock');
    });
    for (const a of qu.order) {
      if (!reached.has(a)) say(f + ' [' + qu.id + '.' + a + ']', 'no stage leads here');
      if (!ends.has(a)) say(f + ' [' + qu.id + '.' + a + ']', 'never comes to the end: a player here is stuck for good');
    }
    // a quest of the server keeps nothing in a character, and its stage is one for the world (StoryQuestsServer)
    if (qu.server) {
      serverQuests.add(qu.id);
      for (const [at, key, val, inWay] of qu.said) {
        if (key === 'nudge' || key === 'choice' || inWay) say(at, 'a quest of the server has no «' + (inWay ? 'way' : key) + '»: nothing of it is kept in a player; what happened goes into the saga through assets/story/tales');
        if (key === 'start' || key === 'done') for (const a of val.split(';')) { const w = a.trim().split(/\s+/)[0]; if (personalActs.includes(w)) say(at, 'a quest of the server may not «' + w + '»: it writes into a character, or moves one player only'); }
      }
      for (const a of qu.order) { const s = qu.info[a], i = qu.order.indexOf(a), own = s.next || (i + 1 < qu.order.length ? qu.order[i + 1] : 'end'); if (own === 'end' && s.fields.has('done')) say(f + ' [' + qu.id + '.' + a + ']', 'the last stage of a quest of the server: «done» could be lost with its player; put it in «start:» of this stage'); }
      // «saga:» goes to those who were by the stage's mark when it passed (StoryQuestsServer.cs, Witnessed): a stage with no mark has nobody there but the one who passed it
      for (const a of qu.order) if (qu.info[a].fields.has('saga') && !qu.info[a].fields.has('mark')) say(f + ' [' + qu.id + '.' + a + ']', '«saga:» on a stage of a quest of the server goes to those who stood by its mark when it passed: give the stage a «mark:» (none: only the one whose game passed it, wherever he was)');
      for (const a of qu.order) if (a !== a.toLowerCase()) say(f + ' [' + qu.id + '.' + a + ']', 'a stage of a quest of the server in lower case: the world keeps its keys so');
      for (const id of qu.order) { const sc = (qu.said.find(x => x[0].endsWith('.' + id + ']') && x[1] === 'scene') || [])[2]; if (sc) serverScenes.add(sc.split(/\s+/)[0]); }
    }
  }
}

// people
const placeKinds = new Set([...fs.readFileSync(path.join(srcDir, 'NpcPlaces.cs'), 'utf8').matchAll(/\bK\(new Kind\s*\{\s*Id = "(\w+)"/g)].map(m => m[1]));
// a person's day (StoryLife): «day: <set> [when <world conditions>]» says which set of its places is its now, «at: <set> <a>-<b> …» its day by
// the hour, «spot: <set> …» its free hours. A set nobody's «day:» names is never its (but «home», the one with no word); a «day:» of a set
// with no place is an empty day; a «day:» after one with no «when» is never reached
const meetOf = {}; // a gathering's name → the people whose days name it («… meet <name>»)
const personIds = new Set(); // the story's people («[person x]»: its template's id)
const dayOf = { at: '', sets: new Set(), days: [] };
const dayEnd = () => {
  let open = false;
  for (const d of dayOf.days) {
    if (open) say(dayOf.at, 'day: «' + d.set + '» stands after a «day:» with no «when»: never reached');
    if (!dayOf.sets.has(d.set)) say(dayOf.at, 'day: no place of the set «' + d.set + '» (its «at:» and «spot:» lines)');
    if (!d.when) open = true;
  }
  for (const s of dayOf.sets) if (s !== 'home' && !dayOf.days.some(d => d.set === s)) say(dayOf.at, 'the set «' + s + '» is nobody\'s day: give it «day: ' + s + ' when <world conditions>» (StoryLife)');
  dayOf.sets = new Set(); dayOf.days = [];
};
for (const f of txt('people')) {
  let at = f;
  for (const l of lines('people', f)) {
    const head = /^\[person\s+(\S+)\]/.exec(l);
    if (head) { dayEnd(); personIds.add(head[1]); at = dayOf.at = f + ' [' + head[1] + ']'; continue; }
    const st = /^stranger:\s*(.*)$/.exec(l); // («stranger: <world conditions> [:: <name>]», «strangersay: hugin|munin <words> [/ …]»: StoryStranger)
    if (st) { const sep = st[1].indexOf('::'), c = (sep >= 0 ? st[1].slice(0, sep) : st[1]).trim(); if (!c) say(at, 'stranger: <world conditions> [:: <name>]'); else condsWhy(at, c, true); continue; }
    const ss = /^strangersay:\s*(.*)$/.exec(l);
    if (ss) { for (const part of ss[1].split('/')) if (!/^(hugin|munin)\s+\S/.test(part.trim())) say(at, 'strangersay: hugin|munin <words> [/ hugin|munin <words> …] (StoryStranger)'); continue; }
    const day = /^day:\s*(.*)$/.exec(l);
    if (day) {
      const when = day[1].indexOf(' when '), set = (when >= 0 ? day[1].slice(0, when) : day[1]).trim();
      if (!/^\w+$/.test(set)) say(at, 'day: <set> [when <world conditions>]');
      if (when >= 0) condsWhy(at, day[1].slice(when + 6), true);
      dayOf.days.push({ set, when: when >= 0 });
      continue;
    }
    let lw = l; // («at:»/«spot:» … when <world conditions>: StoryLife — the conditions checked, the rest read as before)
    { const wi = l.indexOf(' when '); if (/^(at|spot):/.test(l) && wi >= 0) { condsWhy(at, l.slice(wi + 6), true); lw = l.slice(0, wi); } }
    let sp = /^spot:\s*(\S+)\s+(\S+)(.*)$/.exec(lw);
    const planned = /^at:\s*(\S+)\s+(\S+)\s+(\S+)(.*)$/.exec(lw); // «at: <set> <a>-<b> <place> …» — a spot whose hours are said first; «at: <set> <a>-<b> sleep» the hook of its night
    if (/^at:/.test(l) && !planned) { say(at, 'at: <set> <from>-<to> <place> <kind> … | at: <set> <from>-<to> sleep'); continue; }
    if (planned) {
      const h = /^(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)$/.exec(planned[2]);
      if (!h || +h[1] > 24 || +h[2] > 24 || +h[1] === +h[2]) say(at, 'at: hours «' + planned[2] + '» — <from>-<to>, 0…24, not the same (over midnight: 22-6)');
      dayOf.sets.add(planned[1]);
      if (planned[3] === 'sleep') { if (planned[4].trim()) say(at, 'at … sleep: no words after it'); continue; }
      if (/\shours\s/.test(planned[4] + ' ')) say(at, 'at: its hours are said already, no «hours»');
      sp = [lw, planned[1], planned[3], planned[4]];
    }
    if (sp) {
      dayOf.sets.add(sp[1]);
      { const w = sp[3].trim().split(/\s+/), mi = w.indexOf('meet'); if (mi >= 0) { if (!/^\w+$/.test(w[mi + 1] || '')) say(at, 'meet: <name> (a gathering: assets/story/meets)'); else { const who = (/\[(\S+)\]$/.exec(at) || [])[1]; (meetOf[w[mi + 1]] = meetOf[w[mi + 1]] || new Set()).add(who); } } }
      const why = placeWhy(sp[2]); if (why) say(at, 'spot ' + why);
      { const w = sp[3].trim().split(/\s+/), di = w.indexOf('days'); if (di >= 0 && !/^[1-9]\d*$/.test(w[di + 1] || '')) say(at, 'days <n>: a whole number from 1 (every n-th day of the game: StoryLife)'); }
      { // (its days: none of days, chance, daily — at random intervals, «every <a>-<b>» their gap, «group <name>» the same days as others': StoryLife.Acts)
        const w = sp[3].trim().split(/\s+/), has = x => w.includes(x), ei = w.indexOf('every'), gi = w.indexOf('group');
        if (ei >= 0) { const m = /^(\d+)-(\d+)$/.exec(w[ei + 1] || ''); if (!m || +m[1] < 1 || +m[2] < +m[1]) say(at, 'every <a>-<b>: whole days from 1, a ≤ b (StoryLife)'); }
        if (gi >= 0 && !/^\w+$/.test(w[gi + 1] || '')) say(at, 'group <name>: one word (lines of that name act on the same days)');
        if (has('daily') && (has('days') || has('chance'))) say(at, 'daily — or days, or chance, not together');
        if ((ei >= 0 || has('daily') || gi >= 0) && (has('days') || has('chance'))) say(at, 'every/daily/group are for a line at random intervals: not with days or chance');
        if ((ei >= 0 || has('daily') || gi >= 0) && !planned) say(at, 'every/daily/group are words of an «at:» line (its day by the hour), not of a «spot:»');
      }
      const rest = sp[3].trim().split(/\s+/);
      { const kind = rest[0] === 'by' ? rest[2] : rest[0]; if (!placeKinds.has(kind)) say(at, 'spot: no kind «' + kind + '» (NpcPlaces.Kinds)'); }
      rest.forEach((x, i) => { if (rest[i - 1] === 'by') { const w2 = placeWhy(x); if (w2) say(at, 'spot by ' + w2); } });
      rest.forEach((x, i) => { if (rest[i - 1] === 'moves') for (const c of x.split('|')) if (!fs.existsSync(path.join(story, 'clips', c.split('.')[0] + '.txt'))) say(at, 'spot moves: no clip «' + c + '» (assets/story/clips)'); }); // (StoryLife «spot … moves»: the game would only log it)
      continue;
    }
    const carry = /^carry:\s*(\S+)(.*)$/.exec(l); // «carry: <item> [arm|left|right] [clip <clip>] [set <clip>]» (StoryProps.CarryLine)
    if (carry) {
      const w = itemWhy(carry[1]); if (w) say(at, 'carry: ' + w); else if (!carry[1].startsWith('RavenWatch_item_')) say(at, 'carry: «' + carry[1] + '» is not the mod\'s own item (RavenWatch_item_<id>)');
      const rest = carry[2].trim().split(/\s+/).filter(Boolean);
      for (let i = 0; i < rest.length; i++) {
        if (rest[i] === 'arm' || rest[i] === 'left' || rest[i] === 'right') continue;
        if ((rest[i] === 'clip' || rest[i] === 'set') && i + 1 < rest.length) { i++; continue; }
        say(at, 'carry: no such word «' + rest[i] + '» (' + 'carry: <item> [arm|left|right] [clip <clip>] [set <clip>]' + ')');
      }
      continue;
    }
    const m = /^(npc|live|after|until):\s*(.*)$/.exec(l);
    if (!m) continue;
    if (m[1] === 'npc') { if (!npcs.has(m[2])) say(at, 'no person «' + m[2] + '» in assets/story/npc'); continue; }
    if (m[1] === 'after' || m[1] === 'until') { condsWhy(at, m[2], true); continue; }
    const when = m[2].indexOf(' when ');
    if (when >= 0) condsWhy(at, m[2].slice(when + 6), true);
    checkDotted(at, (when >= 0 ? m[2].slice(0, when) : m[2]).split(/\s+/));
    { const home = (when >= 0 ? m[2].slice(0, when) : m[2]).split(/\s+/);
      if (home[0] === 'away') { if (home.length > 1) say(at, 'live away: no spawn or bed — it is out of the world (StoryPeople.Away)'); if (when < 0) say(at, 'live away: with «when» only — out of the world for good is «until:»'); }
      else for (let i = 0; i < home.length; i++) if (i === 0 || home[i - 1] === 'spawn' || home[i - 1] === 'bed') { const why = placeWhy(home[i]); if (why) say(at, 'live ' + why); } }
  }
}
dayEnd();
// the gatherings' talks (assets/story/meets, NpcChatter «[meet <name>]»): every gathering a day names has talks, every talk's speakers are people of
// that gathering (a person's id is its template's: «[person x]» with «npc: x»), its conditions are known, and there are enough talks for it not to
// say the same thing every evening (MeetFew; a talk of made phrases «{@theme}» is new each time and counts as three)
const meetTold = []; // «{told <talk> <place prefix>}» lines of the gatherings: checked once the talks are read (below)
{
  const MeetFew = 6, talks = {};
  for (const f of txt('meets')) {
    let name = null, cur = null, pair = null;
    const end = () => { if (name && cur && cur.by.length) talks[name].push(cur); cur = null; };
    lines('meets', f).forEach((raw, i) => {
      const at = 'meets/' + f + ':' + (i + 1), l = raw.replace(/^#.*$/, '').trim();
      const ph = /^\[pair\s+(\w+)\s+(\w+)\]$/.exec(l); // («[pair <a> <b>]»: two people's short exchanges near each other outside a gathering — StoryMeets.PairTick)
      if (ph) {
        end(); pair = new Set([ph[1], ph[2]]); name = 'pair ' + [ph[1], ph[2]].sort().join(' '); talks[name] = talks[name] || [];
        for (const p of pair) if (!personIds.has(p)) say(at, '[pair]: «' + p + '» is none of the story\'s people («[person ' + p + ']» in assets/story/people)');
        if (pair.size < 2) say(at, '[pair]: two different people');
        return;
      }
      const head = /^\[(\w+)\s+(\w+)\]$/.exec(l);
      if (head) { end(); pair = null; if (head[1] !== 'meet') { say(at, 'only «[meet <name>]» and «[pair <a> <b>]» blocks here'); name = null; return; } name = head[2]; talks[name] = talks[name] || []; return; }
      if (!l) { end(); return; }
      if (!name) { if (!raw.trim().startsWith('#')) say(at, 'a line before any «[meet <name>]»'); return; }
      cur = cur || { by: [], made: false, at };
      if (l.startsWith('?')) { condsWhy(at, l.slice(1).trim(), false); cur.when = (cur.when ? cur.when + ', ' : '') + l.slice(1).trim(); return; }
      { const told = /\{told/.test(l) ? /^\w+:\s*(\{told.*)$/.exec(l) || [l, ''] : null; if (told) { const t = /^\{told\s+(\w+)\s+(\w+)\}$/.exec(told[1]); if (!t) say(at, '{told <talk> <place prefix>}: the whole line, nothing else in it'); else meetTold.push({ at, talk: t[1], prefix: t[2], cur }); } }
      const m = /^(\w+):\s*(.+)$/.exec(l);
      if (!m) { say(at, 'a gathering\'s line is «<who>: <words>»'); return; }
      cur.by.push(m[1]);
      if (/\{@\w+\}/.test(m[2])) cur.made = true;
      if (pair) { if (!pair.has(m[1])) say(at, '«' + m[1] + '» is not of the pair «' + name + '»'); return; }
      if (!meetOf[name]) return;
      if (!meetOf[name].has(m[1])) say(at, '«' + m[1] + '» is not of the gathering «' + name + '»: its day has no «meet ' + name + '» (assets/story/people)');
    });
    end();
  }
  for (const name of Object.keys(meetOf)) {
    if (!talks[name]) { say('people', 'the gathering «' + name + '» has no talks: «[meet ' + name + ']» in assets/story/meets'); continue; }
    if (meetOf[name].size < 2) say('people', 'the gathering «' + name + '» is named by one person only: nobody to talk to');
    const worth = talks[name].reduce((n, t) => n + (t.made ? 3 : 1), 0);
    if (worth < MeetFew) say('meets', 'the gathering «' + name + '» has few talks (' + talks[name].length + '; at least ' + MeetFew + ', a made one counts as three): they would say the same every time');
    for (const t of talks[name]) if (new Set(t.by).size < 2) say(t.at, 'a talk of one speaker: a gathering\'s talk is between two or more');
  }
  for (const name of Object.keys(talks)) {
    if (name.startsWith('pair ')) {
      const worth = talks[name].reduce((n, t) => n + (t.made ? 3 : 1), 0);
      if (worth < MeetFew) say('meets', 'the pair «' + name + '» has few talks (' + talks[name].length + '; at least ' + MeetFew + ', a made one counts as three)');
      for (const t of talks[name]) if (new Set(t.by).size < 2) say(t.at, 'a talk of one speaker: a pair\'s talk is between the two');
      continue;
    }
    if (!meetOf[name]) say('meets', '«[meet ' + name + ']»: no person\'s day names it («… meet ' + name + '» in assets/story/people)');
  }
}

// talks: every answer leads to a place that is there, a person's «talk:» names a talk (common: in the code); «step» names a stage that
// exists, «npcat» a place; a stage waiting for what a talk raises must be offered in that talk («step <its quest> <its stage>»): else a window
// closed half way leaves the player on a stage nothing can end (Heid's letter, 2026-11-04)
const talks = new Set(['common']);
const stagesOf = {}, stageOn = [];
for (const f of txt('quests')) {
  let quest = '', stage = '';
  for (const l of lines('quests', f)) {
    let m;
    if ((m = /^\[quest\s+(\S+)\]/.exec(l))) { quest = m[1]; stagesOf[quest] = stagesOf[quest] || new Set(); stage = ''; continue; }
    if ((m = /^\[stage\s+(\S+)\]/.exec(l))) { stage = m[1]; if (quest) stagesOf[quest].add(stage); continue; }
    if ((m = /^on:\s*(\S+)/.exec(l)) && quest && stage) stageOn.push({ quest, stage, event: m[1].toLowerCase() });
  }
}
const talkRaised = new Set(), talkSteps = new Set();
const talkLed = {}; // a talk → the places its answers lead to (the saga's «asked <talk> <place>»)
const taleIds = new Set(); // (the saga's «knows <tale>»)
const talkConds = (at, s) => {
  for (const c of s.split(',')) {
    const w = c.trim().replace(/^!/, '').split(/\s+/);
    noteAsked(at, w);
    knowAsked.push([at, w]);
    if (w[0] === 'through' && (c.trim().startsWith('!') || !isThrough(w.join(' ')))) say(at, '«' + c.trim() + '»: «through» or «through N» (N — the chapter it was judged at); anything else the game cannot read, and the question would never show');
    if (w[0] === 'step') { if (!stagesOf[w[1]] || !stagesOf[w[1]].has(w[2])) say(at, 'no stage «' + w[2] + '» in quest «' + w[1] + '»'); else talkSteps.add(w[1] + ' ' + w[2]); }
    if (w[0] === 'reached' && (!stagesOf[w[1]] || !stagesOf[w[1]].has(w[2]))) say(at, 'no stage «' + w[2] + '» in quest «' + w[1] + '»');
    if (w[0] === 'npcat' && w[1]) { const why = placeWhy(w[1]); if (why) say(at, why); }
  }
};
// A story person's question lives in the story's time (the user 2026-11-15: a question out of its time, or one not asked in it, is gone for good).
// The game keeps each question of a first screen inside its file's chapter (NpcTalk.Parse: «chnow»); inside the chapter its end is the data's own —
// a condition that stops holding for good as the story goes on — or «wholechapter» said on purpose («always»: lifts the chapter's bounds too, only for
// «taleuntold», the retelling of what happened). A place («npcat»), an item, «once» are no end: Heid's «why is the fire blue?» stood on in the
// Hall lit gold (2026-11-15). «!quest x active» holds again once the quest is over: no end either. A closing «{@say_…} => end» needs none
const talkEnds = c => {
  const not = c.startsWith('!'), w = c.replace(/^!/, '').trim().split(/\s+/);
  if (w[0] === 'quest') return not ? !w[2] || w[2] === 'done' || w[2] === 'started' : w[2] === 'active';
  return not ? ['reached', 'world', 'knows', 'lived', 'flag', 'chose'].includes(w[0]) : ['step', 'taleuntold', 'wholechapter', 'always', 'through'].includes(w[0]); // («through»: its own rules below — throughQ)
};
// A question that lives past its chapter («through», the user 2026-11-16: a question goes only when it no longer belongs — NpcTalk's header): no chapter's
// end is put on it, so its end must last — a lasting fact in its own line, a later chapter's «?- <place> : <condition>», or «through N»: no end yet,
// judged at chapter N — once a later chapter's files are there, each such one is ended or judged anew (N raised by the author, never by a tool)
const throughQ = [], throughEnds = [];
const isThrough = c => /^through(\s+\d+)?$/.test(c);
// «lived <quest>.<stage>» of a quest of the server is theirs only who were there when it passed (StoryQuestsServer.cs, Witnessed): another never has it,
// so for him it is no end that comes and no answer that comes — as a talk's place
const losable = w => (w[0] === 'lived' && w[1] && serverQuests.has(w[1].split('.')[0])) || w[0] === 'asked';
const lastingEnd = c => { const not = c.startsWith('!'), w = c.replace(/^!/, '').trim().split(/\s+/); return not && !losable(w) && (['world', 'knows', 'lived', 'flag', 'chose'].includes(w[0]) || (w[0] === 'quest' && (!w[2] || w[2] === 'done' || w[2] === 'started'))); };
const lastingFact = c => { const w = c.trim().split(/\s+/); return !c.startsWith('!') && !losable(w) && (['reached', 'world', 'knows', 'lived', 'flag', 'chose'].includes(w[0]) || (w[0] === 'quest' && (!w[2] || w[2] === 'done' || w[2] === 'started'))); };
// your words to a story person ({@say_*} in its talk) are its own — [<who>:say_*] in assets/story/words/<who>.txt (NpcPhrases: they win over the common
// ones); none: the settler's «Гаразд, не буду заважати» in a story talk (the user 2026-11-17). Checked in the loop below, line by line.
const ownSay = new Set(); { const wordsDir = path.join(story, 'words'); for (const f of fs.existsSync(wordsDir) ? fs.readdirSync(wordsDir).filter(x => x.endsWith('.txt')) : []) for (const m of fs.readFileSync(path.join(wordsDir, f), 'utf8').matchAll(/^\[(\w+):(say_\w+)/gm)) ownSay.add(m[1] + ':' + m[2]); }
// a talk's places are all its files' together, as the game reads them: a chapter's «[talk heid +]» may lead to a place of the prologue's file (checked after all are read)
const talkPlaces = {}, talkTo = [], placeLines = {}, firstQs = []; // (placeLines «talk:place» → its lines; firstQs: the first screen's questions — «heard, it comes back» below)
for (const f of txt('talk')) {
  let talk = '', places = new Set(), to = [], prevAnswer = false, page = '', ctx = [];
  const done = () => { if (!talk) return; talkLed[talk] = new Set([...(talkLed[talk] || []), ...to.map(x => x[1])]); // (a «[talk x +]» adds to the same talk)
    talkPlaces[talk] = new Set([...(talkPlaces[talk] || []), ...places]); for (const [at, t] of to) talkTo.push([at, t, talk, f]); };
  lines('talk', f).forEach((l, i) => {
    if (l.startsWith('~') && !/^~(hugin|munin|any)\s+(aside|aloud)\b[^:]*:\s*\S/.test(l)) say(f + ':' + (i + 1), 'a raven line: ~hugin|munin|any aside|aloud: words');
    const head = /^\[talk\s+([^\s:\]]+)/.exec(l);
    if (head) { done(); talk = head[1]; talks.add(talk); places = new Set(); to = []; prevAnswer = false; page = ''; ctx = []; return; }
    if (l.startsWith('@')) { page = l.slice(1).replace(/\+$/, '').trim(); places.add(page); prevAnswer = false; ctx = []; return; }
    if (talk && page) (placeLines[talk + ':' + page] = placeLines[talk + ':' + page] || []).push(l);
    if (talk && talk !== 'common') for (const m of l.matchAll(/\{@(say_\w+)\}/g)) if (!ownSay.has(talk + ':' + m[1])) say(f + ':' + (i + 1), '{@' + m[1] + '} in the talk of «' + talk + '»: no [' + talk + ':' + m[1] + '] of its own in assets/story/words/' + talk + '.txt — the common ones are a settler\'s (at least 6 lines in its way; English in npc_patterns.txt)');
    if (talk && page === 'start' && l.startsWith('>')) firstQs.push({ at: f + ':' + (i + 1), talk, l });
    if (l.startsWith('?-')) { // (the end of a «through» question: NpcTalk.ParseEnd)
      const c = l.indexOf(':'), where = c > 0 ? l.slice(2, c).trim().split(/\s+/).filter(Boolean) : [], cond = c > 0 ? l.slice(c + 1).trim() : '';
      if (!talk) say(f + ':' + (i + 1), '«?-» outside a talk');
      else if (!where.length || !cond || cond.includes(',')) say(f + ':' + (i + 1), '«?-»: ?- <place> [place…] : <one condition>');
      else if (!lastingFact(cond)) say(f + ':' + (i + 1), '«?- … : ' + cond + '»: the end of a «through» question is one lasting fact, not turned round, that comes for every player — «reached <quest> <stage>», «world <fact>», «knows», «lived» of a personal quest, «flag», «chose», «quest <id> done» (a condition of the moment would bring the question back; «lived» of a quest of the server is theirs only who were there)');
      else { talkConds(f + ':' + (i + 1), cond); throughEnds.push({ at: f + ':' + (i + 1), talk, places: where }); }
      return; }
    if (l.startsWith('??')) { talkConds(f + ':' + (i + 1), l.slice(2)); ctx = l.slice(2).split(',').map(x => x.trim()).filter(Boolean); return; }
    if (page === 'start' && l.startsWith('>')) { // (read as NpcTalk.ParseAnswer: no «=>» — it goes to «end»; a «?…» part split on its commas, as StoryCond.Parse)
      const arrow = l.indexOf('=>'), text = l.slice(l.startsWith('>|') ? 2 : 1, arrow < 0 ? l.length : arrow).trim(), rest = arrow < 0 ? '' : l.slice(arrow + 2).trim();
      const target = rest && !/^[?!]/.test(rest) ? rest.split(/\s+/)[0] : 'end';
      const own = rest.split(/\s(?=[?!])/).filter(x => x.startsWith('?')).flatMap(x => x.slice(1).split(',')).map(x => x.trim()).filter(Boolean), all = ctx.concat(own);
      if (!(target === 'end' && /^\{@say_\w+\}$/.test(text)) && !all.some(talkEnds))
        say(f + ':' + (i + 1), '«' + text + '»: a story person\'s question with no end in the story — it would stand on out of its time. Give it one: «?step <quest> <stage>», «?!reached <quest> <stage>», «?!world <fact>» … in the line or its «??»; or «?? wholechapter» if it lives its whole chapter on purpose (docs/jobs/words-people.md «Скільки живе питання»)');
      const thr = all.find(isThrough);
      if (thr) throughQ.push({ at: f + ':' + (i + 1), talk, text, all, judged: /\d/.test(thr) ? Number(thr.split(/\s+/)[1]) : -1, to: target.split('|').map(x => x.replace(/\*\d$/, '')) });
      if (all.includes('always') && !all.includes('taleuntold')) say(f + ':' + (i + 1), '«' + text + '»: «always» lifts a question out of its chapter\'s bounds — only for the retelling of what happened («taleuntold»); a question of the chapter is «wholechapter»');
    }
    if (l.startsWith('>') && page !== 'start' && ctx.concat(l.includes('=>') ? l.slice(l.indexOf('=>') + 2).split(/\s(?=[?!])/).filter(x => x.startsWith('?')).flatMap(x => x.slice(1).split(',')).map(x => x.trim()) : []).some(isThrough))
      say(f + ':' + (i + 1), '«through» is for a question of the first screen («@start»): the game puts a chapter\'s bounds only there');
    if (l.startsWith('>|') && !prevAnswer) say(f + ':' + (i + 1), '«>|» is a way of the answer right above it, and there is none: the game shows it always');
    prevAnswer = l.startsWith('>') || (prevAnswer && (l === '' || l.startsWith('~') || l.startsWith('#')));
    const after = l.startsWith('>') && l.includes('=>') ? l.slice(l.indexOf('=>') + 2) : '';
    for (const part of after.split(/\s(?=[?!])/)) { if (part.startsWith('?')) talkConds(f + ':' + (i + 1), part.slice(1)); const r = /^!raise\s+(\S+)/.exec(part); if (r) talkRaised.add(r[1].toLowerCase()); const st = /^!(flag|world|var|wvar|stamp)\s+(\S+)/.exec(part); if (st) setWords[st[1]].add(st[2]); const sq = /^!quest\s+(\S+)/.exec(part); if (sq && serverQuests.has(sq[1])) say(f + ':' + (i + 1), '«!quest ' + sq[1] + '»: a quest of the server is never kept in a character (Story.SetQuest refuses it): move its stage through the quest itself (an «on:» on «!raise»)'); }
    const m = /^>.*=>\s*([^\s?!]+)(\s+once)?/.exec(l);
    if (m) { // (a draw «a|b*2|c»: each of its places, as NpcTalk.PoolOf reads it)
      const ways = m[1].includes('|') ? m[1].split('|').map(x => { const d = /^([^*]+)(?:\*([1-9]))?$/.exec(x); if (!d || d[1] === 'end') say(f + ':' + (i + 1), '«' + x + '» in a draw is no place (place or place*1…9)'); return d ? d[1] : x; }) : [m[1]];
      for (const w of ways) to.push([f + ':' + (i + 1), w]);
      if (m[2] && ways.length > 1) say(f + ':' + (i + 1), 'once on a draw: a place heard is not drawn already (NpcTalk.Draw)');
      else if (m[2] && m[1] === 'end') say(f + ':' + (i + 1), 'once needs a place to lead to'); }
  });
  done();
}
for (const [at, t, talk] of talkTo) if (t !== 'end' && t !== 'homes' && !talkPlaces[talk].has(t)) say(at, 'no place «' + t + '» in talk «' + talk + '»');
// A question of the first screen that only tells (its place and every place it leads on to do nothing: no «!» deed anywhere down the line) and
// has no «once» comes back after it was heard, the same words over and over (the user 2026-11-17, Eyvind's «Хто ти такий?» — «не раз такий баг
// вже є»; Ketil's «Тобі є де спати?», Lit's «Зала — туди.» were the same). Heard once is enough: «once». One meant to be asked again (a reminder,
// a tale drawn at random) says so: variants «a || b», or a draw «=> a|b».
{
  const deedDown = (talk, place, seen) => { if (seen.has(place)) return false; seen.add(place);
    for (const l of placeLines[talk + ':' + place] || []) {
      if (/(^|\s)!(?!tone\b)\w/.test(l.includes('=>') ? l.slice(l.indexOf('=>')) : '') || /\{(tale|told)\b/.test(l)) return true; // (a deed on an answer; a retelling: it ends itself)
      const m = /^>.*=>\s*([^\s?!]+)/.exec(l); if (m && !['start', 'end', 'homes'].includes(m[1])) for (const t of m[1].split('|')) if (deedDown(talk, t.replace(/\*\d$/, ''), seen)) return true; }
    return false; };
  for (const q of firstQs) {
    const arrow = q.l.indexOf('=>'); if (arrow < 0) continue;
    const text = q.l.slice(q.l.startsWith('>|') ? 2 : 1, arrow).trim(), rest = q.l.slice(arrow + 2).trim(), target = /^[?!]/.test(rest) ? 'end' : rest.split(/\s+/)[0];
    if (['start', 'end', 'homes'].includes(target) || target.includes('|') || text.includes('||') || /\bonce\b/.test(rest) || /(^|\s)!(?!tone\b)\w/.test(rest)) continue;
    if (!deedDown(q.talk, target, new Set())) say(q.at, '«' + text + '»: heard, it comes back — its answer only tells (no deed down its line) and it has no «once». Once is enough: «=> ' + target + ' once». Meant to be asked again: say so — variants «a || b» or a draw «=> a|b»');
  }
}
for (const talk of Object.keys(talkPlaces)) if (!talkPlaces[talk].has('start')) say('talk «' + talk + '»', 'no @start');
// a gathering's «{told <talk> <prefix>}» (StoryMeets: a true tale by the fire, into the book): the talk has such places, each one an answer leads to (so the
// book can ask it), and the talk asks «untold» of the same — else, all told, the line has nothing to say and the talk breaks off mid-way
for (const t of meetTold) {
  const places = talkPlaces[t.talk] ? [...talkPlaces[t.talk]].filter(p => p.startsWith(t.prefix)) : [];
  if (!talkPlaces[t.talk]) say(t.at, '{told}: no talk «' + t.talk + '» in assets/story/talk');
  else if (!places.length) say(t.at, '{told}: no place of talk «' + t.talk + '» starts with «' + t.prefix + '»');
  else for (const p of places) if (!(talkLed[t.talk] || new Set()).has(p)) say(t.at, '{told}: no answer of talk «' + t.talk + '» leads to «' + p + '» — nobody could be asked it, and the fire would tell it out of its time');
  if (!(t.cur.when || '').split(',').some(c => c.trim() === 'untold ' + t.talk + ' ' + t.prefix)) say(t.at, '{told ' + t.talk + ' ' + t.prefix + '}: its talk needs «? untold ' + t.talk + ' ' + t.prefix + '» (StoryMeets)');
}
{
  const latest = Math.max(0, ...['quests', 'scenes', 'talk', 'saga'].flatMap(d => txt(d)).map(file => { const m = /^ch(\d+)/.exec(file); return m ? Number(m[1]) : 0; })); // (the latest chapter there is: StoryFiles.Block)
  for (const q of throughQ) {
    const act = q.all.find(c => /^step\s/.test(c) || /^!\s*reached\s/.test(c));
    if (act) say(q.at, '«' + q.text + '»: «through» beside «' + act + '» — a stage is its own chapter\'s, so the question ends inside its chapter anyway: drop «through», or end it by a lasting fact');
    const ended = q.all.some(lastingEnd) || throughEnds.some(e => e.talk === q.talk && e.places.some(p => q.to.includes(p)));
    if (ended && q.judged >= 0) say(q.at, '«' + q.text + '»: «through ' + q.judged + '» says it has no end yet, and it has one: write plain «through»');
    if (!ended && q.judged < 0) say(q.at, '«' + q.text + '»: a «through» question with no end — give it a lasting one («?!world <fact>» in its line, or a later chapter\'s «?- ' + q.to[0] + ' : <condition>»), or say it has none yet: «through ' + latest + '»');
    if (!ended && q.judged >= 0 && q.judged !== latest) say(q.at, '«' + q.text + '»: «through ' + q.judged + '» was judged at chapter ' + q.judged + ', and the story has chapter ' + latest + ' now: does the question still belong there? End it («?- ' + q.to[0] + ' : <condition>» in talk/ch' + latest + '_<who>.txt) or write «through ' + latest + '»');
  }
  for (const e of throughEnds) for (const p of e.places) if (!throughQ.some(q => q.talk === e.talk && q.to.includes(p))) say(e.at, '«?- ' + p + '»: no «through» question of the first screen of talk «' + e.talk + '» leads to «' + p + '»: the line ends nothing');
}
// tales: a place that is there, and the three lines each needs; a rumour only beside a saga entry, both in a chapter
for (const f of txt('tales')) {
  let id = '', has = new Set(), chapter = '';
  const done = () => {
    if (!id) return;
    const at = 'tales/' + f + ' [' + id + ']';
    for (const k of ['when', 'at', 'saw']) if (!has.has(k)) say(at, 'no «' + k + ':»');
    if (has.has('rumor') && !has.has('saga')) say(at, 'a rumor with no saga line: nobody could ever lift it');
    chapterWhy(at, chapter, has.has('saga') || has.has('rumor'));
  };
  lines('tales', f).forEach(l => {
    const h = /^\[tale\s+(\S+)\]/.exec(l);
    if (h) { done(); id = h[1]; taleIds.add(id); has = new Set(); chapter = ''; return; }
    const m = /^(\w+):\s*(.*)$/.exec(l); if (!m) return;
    has.add(m[1]);
    if (m[1] === 'chapter') chapter = m[2].trim();
    if (m[1] === 'at') { const why = placeWhy(m[2].split(/\s+/)[0]); if (why) say('tales/' + f + ' [' + id + ']', why); }
    if (m[1] === 'when') condsWhy('tales/' + f + ' [' + id + ']', m[2]);
  });
  done();
}
for (const s of stageOn) if (talkRaised.has(s.event) && !talkSteps.has(s.quest + ' ' + s.stage)) say('quests [' + s.quest + ' ' + s.stage + ']', 'waits for «' + s.event + '» from a talk, and no talk offers anything at this stage («?? step ' + s.quest + ' ' + s.stage + '»): closed half way, it is stuck');
for (const f of txt('npc')) for (const l of lines('npc', f)) { const m = /^talk:\s*(\S+)/.exec(l); if (m && !talks.has(m[1])) say('npc/' + f, 'no talk «' + m[1] + '» in assets/story/talk'); }
// the ravens' looks (assets/story/looks): one default for a scene's bird, what a template or a step names is there
{
  const looks = new Set(txt('looks').map(f => f.replace(/\.txt$/, '')));
  let defaults = 0;
  for (const f of txt('looks')) { const ls = lines('looks', f); const when = ls.some(l => l.startsWith('when:')); if (ls.includes('default: yes') && !when) defaults++; if (when && !ls.includes('default: yes')) say('looks/' + f, '«when:» goes only with «default: yes»'); if (!ls.some(l => l.startsWith('id:'))) say('looks/' + f, 'no «id:»'); }
  // a line of a look the game no longer reads (another bird's body on a raven: gone 2026-11-13, the falcon has its own skeleton)
  for (const f of txt('looks')) for (const l of lines('looks', f)) if (/^(body|frame|size):/.test(l)) say('looks/' + f, '«' + l.split(':')[0] + ':» is read no more: the falcon is its own creature (Falcon)');
  if (defaults !== 1) say('looks', defaults + ' files say «default: yes»: one look is a scene\'s bird by default (those with «when:» not counted)');
  for (const f of txt('npc')) for (const l of lines('npc', f)) { const m = /^raven_look:\s*(\S+)/.exec(l); if (m && m[1] !== 'game' && !looks.has(m[1])) say('npc/' + f, 'no look «' + m[1] + '» in assets/story/looks'); }
  for (const f of txt('scenes')) for (const l of lines('scenes', f)) { const m = /^(?:if [^:]*:\s*)?bird (?:come\b.*\blook|wear)\s+(\S+)/.exec(l); if (m && !looks.has(m[1])) say('scenes/' + f, 'no look «' + m[1] + '» in assets/story/looks'); }
}

// a scene of a quest of the server: its «do» writes nothing into a character either. But «give»: a scene plays on the one game of its player (its claim),
// so the thing goes to the one standing there — Torvald's scroll into the finder's bag, no room: at his feet (the user 2026-11-16: on the ground it was missed);
// the stage then waits for it in anyone's bag, and «lost:» keeps one at the feet from being lost
const sceneGives = new Set(['give']);
for (const f of txt('scenes')) {
  let sc = '';
  for (const l of lines('scenes', f)) {
    const h = /^\[scene\s+(\S+)\]/.exec(l);
    if (h) { sc = h[1]; continue; }
    const m = /^(?:if [^:]*:\s*)?do\s+(\S+)/.exec(l);
    if (m && serverScenes.has(sc) && personalActs.includes(m[1]) && !sceneGives.has(m[1])) say(f + ' [' + sc + ']', 'a scene of a quest of the server may not «do ' + m[1] + '»: it writes into a character');
    const own = /^(?:if [^:]*:\s*)?(own|raven)\s/.exec(l);
    if (own && serverScenes.has(sc)) say(f + ' [' + sc + ']', 'a scene of a quest of the server may not «' + own[1] + '»: it acts on one character');
  }
}
// the saga's entries of what one knows (assets/story/saga: person, place, lore — StorySaga.Sections): its words as the game reads them, and
// what its conditions name is there — a talk's place an answer leads to («asked»), a tale («knows»), an entry of one's own chronicle
// («lived»), a piece with words to read («read», «quote:»). A line shut for ever by a typo would say nothing in the game
{
  const sagaCode = fs.readFileSync(path.join(srcDir, 'StorySaga.cs'), 'utf8');
  const sections = new Set([...sagaCode.matchAll(/new Section \{ Word = "(\w+)"/g)].map(m => m[1]));
  const closing = new Set([...sagaCode.matchAll(/new Section \{ Word = "(\w+)"[^}]*Closes = true/g)].map(m => m[1])); // (riddles: their entries have an «answer:»)
  if (sections.size === 0) say('tools/storycheck.js', 'found no sections in StorySaga.cs: its pattern is out of date, and this check would pass every block');
  const readPieces = new Set(placeRules.filter(r => r[1] === 'read').map(r => r[0] + '.' + r[2].split(/\s+/)[0]));
  const pieceWhy = word => { const [id, piece] = word.split('.'); const loc = aliasOf[id] || id; return !places[id] ? 'no place «' + id + '» in locations.list.txt' : readPieces.has(loc + '.' + piece) ? null : 'place «' + loc + '» has no «read: ' + piece + '» line: nothing to read there'; };
  // a book line stays once learnt: a condition of the moment (standing at a stage, where one is, what one carries) would take it out again —
  // Ketil's entry asked «asked ketil, step ch1 bed, quest ch1 done» (a comma is «and»: never all three) and was never in the book (2026-11-16)
  const momentKinds = new Set(['step', 'npcat', 'near', 'before', 'item', 'raven', 'calm', 'night', 'day', 'biome', 'chnow', 'chfrom', 'through', 'inside', 'sky']);
  const sagaConds = (at, text) => {
    for (const group of text.split('|')) {
      if (!group.trim()) { say(at, 'an empty condition beside «|» or before «::»'); continue; }
      condsWhy(at, group);
      for (const c of group.split(',')) { const w = c.trim().replace(/^!/, '').split(/\s+/)[0]; if (momentKinds.has(w)) say(at, '«' + c.trim() + '»: a condition of the moment in the book — the line would go again once it passes (a stage: «reached <quest> <stage>»)'); }
    }
  };
  const knowWhy = (at, w) => {
    if (w[0] === 'asked' && w[1]) { if (!talkLed[w[1]]) say(at, 'asked: no talk «' + w[1] + '» in assets/story/talk'); else if (w[2] && !talkLed[w[1]].has(w[2])) say(at, 'asked: no answer of talk «' + w[1] + '» leads to «' + w[2] + '»: it could never be heard'); }
    if (w[0] === 'knows' && w[1] && !taleIds.has(w[1])) say(at, 'knows: no tale «' + w[1] + '» in assets/story/tales');
    if (w[0] === 'lived' && w[1] && !sagaLived.has(w[1])) say(at, 'lived: «' + w[1] + '» is no stage or way of a quest with a «saga:» line');
    if (w[0] === 'read' && w[1]) { const why = pieceWhy(w[1]); if (why) say(at, 'read: ' + why); }
  };
  const chapterKeys = new Set(['name', 'act', 'after', 'when', 'lands', 'epigraph', 'closing', 'done']), actKeys = new Set(['label', 'name']), sagaActs = new Set(), chapterActs = [], actWords = {}, actNamed = new Set(), entryKeys = new Set(['chapter', 'shown', 'name', 'about', 'line', 'quote', 'map', 'answer']);
  const sagaAsked = [], riddleAt = {}, riddleAnswered = new Set(), riddleSafe = new Set(); // (the talks the book's lines ask: each has its person's entry — StorySaga.MoreOf names who may tell more by it)
  const ids = new Set(), sectionOf = {}, sagaAdds = [];
  for (const f of txt('saga')) {
    let e = null, inChapter = false, inAct = false;
    const done = () => {
      if (!e) return;
      if (e.plus) { if (e.chapter) say(e.at, 'an adding block «[… +]» has no «chapter:» of its own: its lines go into the entry\'s chapter'); return; }
      if (!e.names.length || e.names[e.names.length - 1].includes(' :: ')) say(e.at, 'its last «name:» must have no conditions (what it is called till more is known)');
      for (const list of [e.names, e.abouts]) list.forEach((t, n) => { if (n < list.length - 1 && !t.includes(' :: ')) say(e.at, '«' + t + '» has no conditions and stands above other lines of its kind: those below it could never show'); });
      chapterWhy(e.at, e.chapter, true);
      if (!e.shown && !e.lines) say(e.at, 'neither «shown:» nor «line:» nor «quote:»: it would never be in the book');
      if (closing.has(e.sec)) riddleAt[e.id] = e.at;
    };
    lines('saga', f).forEach((l, i) => {
      if (!l) return;
      const at = 'saga/' + f + ':' + (i + 1);
      const h = /^\[(\w+)\s+(\S+?)(\s*\+)?\]$/.exec(l);
      if (h) {
        done(); e = null; inChapter = h[1] === 'chapter'; inAct = h[1] === 'act';
        if (inAct) { if (f !== 'chapters.txt') say(at, 'an act outside saga/chapters.txt: the acts and chapters stand in that file'); if (sagaActs.has(h[2])) say(at, 'act «' + h[2] + '» twice'); sagaActs.add(h[2]); actWords[h[2]] = at; return; }
        if (inChapter) return;
        if (!sections.has(h[1])) { say(at, 'no block «' + h[1] + '» (act, chapter, ' + [...sections].join(', ') + ')'); return; }
        if (h[3]) { sagaAdds.push([at, h[1], h[2]]); e = { id: h[2], at: 'saga/' + f + ' [' + h[2] + ' +]', names: [], abouts: [], chapter: '', shown: true, lines: 1, plus: true, sec: h[1] }; return; } // («[person heid +]»: more for an entry of any saga file — StorySaga reads them after all the entries)
        if (!/^\w+$/.test(h[2]) || ids.has(h[2])) say(at, 'entry «' + h[2] + '»: a name of letters, digits and _, used once in all the saga files');
        ids.add(h[2]); sectionOf[h[2]] = h[1];
        e = { id: h[2], at: 'saga/' + f + ' [' + h[2] + ']', names: [], abouts: [], chapter: '', shown: false, lines: 0, sec: h[1] };
        return;
      }
      const m = /^(\w+):\s*(.*)$/.exec(l);
      if (!m) { say(at, 'a line with no «key:»'); return; }
      if (inAct) { if (!actKeys.has(m[1])) say(at, 'an act has no «' + m[1] + ':» (' + [...actKeys].join(', ') + ')'); else if (m[2].trim()) actNamed.add([...sagaActs].pop()); return; }
      if (inChapter) { if (!chapterKeys.has(m[1])) say(at, 'a chapter has no «' + m[1] + ':» (' + [...chapterKeys].join(', ') + (m[1] === 'label' ? '; «label:» is its act\'s now: «[act <n>]»' : '') + ')'); else if (m[1] === 'done' || m[1] === 'when') condsWhy(at, m[2]); else if (m[1] === 'act') chapterActs.push([at, m[2].trim()]); return; }
      if (!e) return;
      if (!entryKeys.has(m[1])) { say(at, 'an entry has no «' + m[1] + ':» (' + [...entryKeys].join(', ') + ')'); return; }
      const sep = m[2].indexOf(' :: ');
      if (m[1] === 'chapter') e.chapter = m[2].trim();
      else if (m[1] === 'shown') { e.shown = true; sagaConds(at, m[2]); }
      else if (m[1] === 'quote') { e.lines++; const why = pieceWhy(m[2].split(/\s+/)[0]); if (why) say(at, 'quote: ' + why); }
      else {
        if (m[1] === 'answer') { // (a riddle's answer: StorySaga's header)
          if (!closing.has(e.sec)) { say(at, '«answer:» is a riddle\'s (' + [...closing].join(', ') + '): an entry of «' + e.sec + '» has none'); return; }
          riddleAnswered.add(e.id);
          if (sep < 0) say(at, 'answer: <conditions> :: <words> — with no conditions the riddle would be answered at once');
          else if (m[2].slice(0, sep).split('|').some(g => !g.split(',').some(c => losable(c.trim().split(/\s+/))))) riddleSafe.add(e.id); // (a way that comes for every player)
        }
        if (sep >= 0) for (const c of m[2].slice(0, sep).split(/[|,]/)) { const w = c.trim().split(/\s+/); if (w[0] === 'asked' && w[1]) sagaAsked.push([at, w[1]]); }
        if (m[1] === 'name') e.names.push(m[2]);
        if (m[1] === 'about') e.abouts.push(m[2]);
        if (m[1] === 'line') e.lines++;
        if (sep >= 0) { sagaConds(at, m[2].slice(0, sep)); if (!m[2].slice(sep + 4).trim()) say(at, 'no words after «::»'); }
        else if (m[2].includes('::')) say(at, '«::» stands with a space on each side («<conditions> :: <words>»)');
        if (m[1] === 'map') { const why = placeWhy((sep >= 0 ? m[2].slice(sep + 4) : m[2]).trim()); if (why) say(at, 'map: ' + why); }
      }
    });
    done();
  }
  for (const id of Object.keys(riddleAt)) if (riddleAnswered.has(id) && !riddleSafe.has(id)) say(riddleAt[id], 'every way of its answers can be lost — a talk («asked») or a stage of a quest of the server («lived»: theirs only who were there) — and the riddle might never close. A main clue cannot be lost: give one answer line a way that comes for every player («reached», «knows», «world», «lived» of a personal quest); a fuller answer for those who were there may stand above it');
  for (const id of Object.keys(riddleAt)) if (!riddleAnswered.has(id)) warnings.push(riddleAt[id] + ': a riddle with no «answer:» yet in any saga file — it stays open for ever (answer: <conditions> :: <words>)');
  for (const act of sagaActs) if (!actNamed.has(act)) say(actWords[act], '[act ' + act + '] has neither «label:» nor «name:»: the book\'s row of acts would show an empty link');
  for (const [where, act] of chapterActs) if (!sagaActs.has(act)) say(where, 'act: no «[act ' + act + ']» in saga/chapters.txt');
  for (const [at, sec, id] of sagaAdds) if (sectionOf[id] !== sec) say(at, '[' + sec + ' ' + id + ' +]: no entry «' + id + '» in «' + sec + '» in any saga file');
  for (const [at, talk] of sagaAsked) if (sectionOf[talk] !== 'person') say(at, 'asked ' + talk + ': no «[person ' + talk + ']» entry in the saga — the book names who may tell more by the entry called as the talk (StorySaga.MoreOf)');
  for (const [at, w] of knowAsked) knowWhy(at, w); // (wherever they stand: the saga, a quest, a talk, a place's rule)
}
// a word asked that nothing sets, an event waited for that nothing raises: the stage stands for ever and no check in the game says so
const inCode = word => new RegExp('const string [^;]*"' + word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '"').test(code); // (a module's own word, as a constant: any string in the code would let a typo through, «trial» is a console word)
for (const kind of Object.keys(asked)) for (const [at, word] of asked[kind]) if (!setWords[kind].has(word) && !inCode(word)) say(at, kind + ' «' + word + '» is asked, and nothing sets it (no action in the data, no such word in the code)');
{
  const raised = new Set([...raisedInData, ...talkRaised, ...[...code.matchAll(/StoryBus\.Raise\("(\w+)"\s*[,)]/g)].map(m => m[1].toLowerCase())]);
  const raisedPrefix = [...code.matchAll(/StoryBus\.Raise\("(\w+)"\s*\+/g)].map(m => m[1].toLowerCase());
  for (const m of code.matchAll(/\bRaise\((\w+)\s*,/g)) { const c = new RegExp('\\b' + m[1] + '\\s*=\\s*"(\\w+)"').exec(code); if (c) raised.add(c[1].toLowerCase()); }
  for (const m of ((/internal enum Ev\s*\{([\s\S]*?)\}/.exec(fs.readFileSync(path.join(srcDir, 'Diary.cs'), 'utf8')) || [])[1] || '').replace(/\/\/.*$/gm, '').matchAll(/\b[A-Z]\w+/g)) raised.add(m[0].toLowerCase());
  for (const s of stageOn) if (!raised.has(s.event) && !raisedPrefix.some(p => s.event.startsWith(p))) say('quests [' + s.quest + ' ' + s.stage + ']', 'on: «' + s.event + '»: nothing raises it (no StoryBus.Raise in the code, no raise in the data): the stage would stand for ever');
}
// this check reads the code by patterns: a pattern that finds nothing means it has gone out of date, and it must not pass everything in silence
for (const [what, found] of [['step option words (const string XStep, XFlags, XValued)', new Set(stepOptions.map(o => o.step.join(' ')))], ['conditions', condWords], ['actions', actWords], ['action verbs', actVerbs], ['steps', stepWords], ['world conditions', worldWords], ['stage fields', stageFields], ['quest fields', questFields], ['way fields', wayFields], ['place kinds', placeKinds], ['condition usages', new Set(Object.keys(condUsage))], ['action usages', new Set(Object.keys(actUsage))]])
  if (found.size === 0) say('tools/storycheck.js', 'found no ' + what + ' in the code: its pattern is out of date, and this check would pass everything');

// the code never names the story: its quests, places, people (the words the story's data owns; a scene's or a piece's name only when it could mean nothing else — with a «_»)
{
  const owned = new Map();
  for (const q of questIds) owned.set(q, 'quest');
  for (const id of Object.keys(places)) if (!aliasOf[id]) owned.set(id, 'place');
  for (const f of txt('people')) for (const l of lines('people', f)) { const m = /^\[person\s+(\S+)\]/.exec(l); if (m) owned.set(m[1], 'person'); }
  for (const sc of scenes) if (sc.includes('_')) owned.set(sc, 'scene');
  for (const id of Object.keys(places)) for (const n of places[id]) if (n.includes('_')) owned.set(n, 'named piece');
  for (const f of fs.readdirSync(srcDir).filter(f => f.endsWith('.cs') && !f.startsWith('BodyClips'))) {
    fs.readFileSync(path.join(srcDir, f), 'utf8').split('\n').forEach((raw, i) => {
      const line = raw.replace(/\/\/.*$/, '');
      for (const m of line.matchAll(/"([a-z_][a-z0-9_]*)"/g)) if (owned.has(m[1])) say('src/' + f + ':' + (i + 1), 'names the story\'s ' + owned.get(m[1]) + ' «' + m[1] + '»: the code knows only mechanics, the story lives in assets/story');
    });
  }
}

// The story's blocks (docs/jobs/story-data.md «Блоки сюжету»): a chapter is a block of its own files — quests/chN.txt, scenes/chN.txt,
// talk/chN_<who>.txt, saga/chN.txt (any other name: the prologue's block) — begun once the one before it is done, and an earlier block is
// hardly asked after. What keeps them apart, so chapter 4 is no tangle of chapter 1's threads:
//   1. a file asks the stage of a quest of its own block only («step|reached <quest> <stage>»: an earlier chapter's question ends with its chapter by itself): another block's talk with the same person is a file of
//      that block adding to it («[talk heid +]»), not a «?? step» in the person's first file
//   2. a quest's chapter, and a saga entry's, is its own block's; the chapters themselves all in saga/chapters.txt (their order is that file's), a chapter's own lines there of its chapter's block
//   3. a quest's «scratch:» keys go when it ends (StoryWorld.Forget): no saga line, no person's «after:»/«until:», no place's rule, no other quest may ask one,
//      and a person keeps one «live:» that holds without them («reached <quest> <stage>» stays)
{
  const blockOf = file => { const m = /^ch(\d+)/.exec(file); return m ? 'ch' + Number(m[1]) : 'prologue'; }; // (as the game's StoryFiles.Block: «ch01» is chapter 1)
  const questBlock = {}, questFile = {}, scratch = [], serverQuest = new Set();
  for (const file of txt('quests')) {
    let q = '';
    for (const l of lines('quests', file)) {
      let m;
      if ((m = /^\[quest\s+(\S+)\]/.exec(l))) { q = m[1]; questBlock[q] = blockOf(file); questFile[q] = file; continue; }
      if (q && (m = /^scratch:\s*(.+)$/.exec(l))) for (const p of m[1].trim().split(/\s+/)) scratch.push({ quest: q, file, p: p.toLowerCase() });
      if (q && /^owner:\s*server/.test(l)) serverQuest.add(q);
      if (q && (m = /^chapter:\s*(\S+)/.exec(l)) && m[1] !== blockOf(file)) say('quests/' + file + ' [' + q + ']', 'chapter «' + m[1] + '» in a file of block ' + blockOf(file) + ': a quest of chapter N lives in quests/chN.txt');
    }
  }
  for (const s of scratch) if (!serverQuest.has(s.quest)) say('quests/' + s.file + ' [' + s.quest + ']', '«scratch:» is for a quest of the server (owner: server): a personal quest keeps nothing in the world to forget');
  for (const dir of ['quests', 'scenes', 'talk', 'saga']) for (const file of txt(dir)) { let own = blockOf(file); lines(dir, file).forEach((l, i) => {
    const ch = dir === 'saga' && file === 'chapters.txt' ? /^\[chapter\s+(\w+)\]/.exec(l) : null;
    if (ch) own = blockOf(ch[1]); // (a chapter's lines are its own chapter's: all the chapters stand in this one file — rule 2 — so its name says nothing of theirs)
    for (const m of l.matchAll(/\b(?:step|reached)\s+(\w+)\s+\w+/g))
      if (questBlock[m[1]] && questBlock[m[1]] !== own)
        say(dir + '/' + file + ':' + (i + 1), 'asks the stage of «' + m[1] + '» (block ' + questBlock[m[1]] + ') from a file of block ' + own + ': a block asks only its own stages' + (dir === 'talk' ? ' — put it in talk/' + questBlock[m[1]] + '_<who>.txt as «[talk <who> +]»' : ''));
  }); }
  for (const file of txt('saga')) lines('saga', file).forEach((l, i) => {
    const at = 'saga/' + file + ':' + (i + 1);
    if (/^\[chapter\s/.test(l) && file !== 'chapters.txt') say(at, 'a chapter outside saga/chapters.txt: the chapters stand in that file\'s order');
    const m = /^chapter:\s*(\S+)/.exec(l);
    if (m && m[1] !== blockOf(file)) say(at, 'an entry of chapter «' + m[1] + '» in a file of block ' + blockOf(file) + ': a chapter\'s entries live in saga/' + m[1] + '.txt (more for one of another chapter: «[person <id> +]» there)');
  });
  const named = l => [...l.matchAll(/\b(?:world|unworld|wvar|days|wstamp)\s+(\w+)/g)].map(m => m[1].toLowerCase()).concat([...l.matchAll(/\bfound\.(\w+)/g)].map(m => m[1].toLowerCase()));
  const kinds = ['t_', 'found_', 'fw_', 'stash_', 'stashed_']; // (StoryWorld.ScratchKinds as the modules fill it: StoryTrackConds «t_», StoryFound)
  const isOf = (name, p) => { let b = name; for (let cut = true; cut; ) { cut = false; for (const k of kinds) if (b.startsWith(k)) { b = b.slice(k.length); cut = true; break; } } return b.startsWith(p); };
  const lasting = (at, l, own) => { for (const n of named(l)) for (const s of scratch) if (s.file !== own && isOf(n, s.p)) say(at, '«' + n + '» is a scratch key of quest «' + s.quest + '» («scratch: ' + s.p + '»): it goes when that quest ends — a lasting fact needs a name of its own, or ask the stage: «reached ' + s.quest + ' <stage>»'); };
  for (const file of txt('saga')) lines('saga', file).forEach((l, i) => lasting('saga/' + file + ':' + (i + 1), l, null));
  for (const file of txt('quests')) lines('quests', file).forEach((l, i) => lasting('quests/' + file + ':' + (i + 1), l, file));
  // a person's «after:»/«until:» and a place's rules last past any chapter: a scratch key there is gone with its quest — Lit's «after: world ch1_home»
  // would have taken him out of the world, the Hall's «becomes: 1.3 … world ch1_home» its state, «hearthgivewait: !world ch1_heid_seen» the Ember for good (2026-11-16);
  // a stage of a quest of the server says the same and stays: «reached ch1 home_hall»
  for (const file of txt('people')) lines('people', file).forEach((l, i) => { if (/^(until|after):/.test(l)) lasting('people/' + file + ':' + (i + 1), l.replace(/\s+#.*$/, ''), null); });
  lines('blueprints', 'locations.list.txt').forEach((l, i) => { if (/^\w+:/.test(l) && !/^(id|name|inside|shell|insidemusic|pool|read):/.test(l)) lasting('blueprints/locations.list.txt:' + (i + 1), l.replace(/\s+#.*$/, ''), null); });
  // its homes: once the quests are over at least one «live:» must still hold — one with no «when», or whose «when» asks no scratch key
  for (const file of txt('people')) {
    const ls = lines('people', file).map(l => l.replace(/\s+#.*$/, ''));
    for (let i = 0; i < ls.length; i++) {
      const h = /^\[person\s+(\S+)\]/.exec(ls[i]);
      if (!h) continue;
      const homes = [];
      for (let j = i + 1; j < ls.length && !/^\[person\s/.test(ls[j]); j++) if (/^live:/.test(ls[j])) homes.push(ls[j]);
      const lastsOn = homes.some(l => { const w = / when (.*)$/.exec(l); return !w || !named('when ' + w[1]).some(n => scratch.some(s => isOf(n, s.p)) && !new RegExp('!\\s*(?:world|wvar)\\s+' + n + '\\b').test(w[1])); });
      if (homes.length && !lastsOn) say('people/' + file + ' [' + h[1] + ']', 'every «live:» asks a scratch key: once its quest is over it has no home (a stage stays: «live: … when reached <quest> <stage>»)');
    }
  }
  // a talk's question ended by «!world <key>»: a scratch key goes with its quest's end and the question would come back (its chapter still goes on till the next begins)
  for (const file of txt('talk')) lines('talk', file).forEach((l, i) => { for (const m of l.matchAll(/!world\s+(\w+)/g)) lasting('talk/' + file + ':' + (i + 1), 'world ' + m[1], null); if (l.startsWith('?-')) lasting('talk/' + file + ':' + (i + 1), l, null); }); // («?-»: the end of a «through» question — a scratch key gone, the question would come back)
  // «chnow» is the game's own (NpcTalk.Parse puts it on a story person's questions): written in data it would say one chapter twice
  for (const dir of ['quests', 'scenes', 'talk', 'saga', 'people', 'tales']) for (const file of txt(dir)) lines(dir, file).forEach((l, i) => { const m = /\b(chnow|chfrom)\b/.exec(l); if (m) say(dir + '/' + file + ':' + (i + 1), '«' + m[1] + '» is put by the game itself (NpcTalk.Parse): a question\'s chapter is its file\'s' + (m[1] === 'chfrom' ? ' — one that lives past it says «through»' : '')); });
}

for (const w of warnings) console.log('STORY WARNING ' + w);
if (problems.length) { console.log(problems.join('\n') + '\n\n' + problems.length + ' broken in the story\'s data: assets/story.'); process.exit(1); }
if (writeLedger) writeLedger();
console.log('storycheck: ok (' + scenes.size + ' scenes, ' + used.size + ' played by quests, ' + npcs.size + ' people, ' + Object.keys(places).length + ' places)');

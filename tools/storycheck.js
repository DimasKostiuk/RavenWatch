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
  const still = s => { const [f, l] = s.split('\t'), w = (l || '').split(' '), at = w.slice(2, 5).map(Number); return held.some(h => h.f === f && h.name === w[0] && h.prefab === w[1] && Math.hypot(h.at[0] - at[0], h.at[1] - at[1], h.at[2] - at[2]) < 0.05); }; // (LocParts.Near: the game finds it within 5 cm)
  for (const s of seen) if (!still(s)) { const [f, l] = s.split('\t'); warnings.push('blueprints/' + f + ': «' + l + '» is gone: a world built before has that piece there and finds it no more. Keep the old line beside the new one (or, when no world has that place, take it out of tools/names.seen.txt)'); }
  if (process.argv.includes('--ledger')) writeLedger = () => { try { fs.writeFileSync(seenFile, [...new Set(seen.concat([...now]))].sort().join('\n') + '\n'); } catch (e) { console.log('STORY WARNING tools/names.seen.txt not written: ' + e.message); } }; // (only the build writes it, and only when the story holds: a trial run leaves no line behind)
}
// a thing the story wants touched, read or opened must have its name; a place's other words (alias:) are its own
const aliasOf = {}, placeRules = [];
{
  let at = '';
  for (const l of lines('blueprints', 'locations.list.txt')) {
    const m = /^loc\s+(\S+)/.exec(l);
    if (m) { at = m[1]; continue; }
    const al = /^alias:\s*(.+)$/.exec(l);
    if (al) { for (const a of al[1].split(/\s+/)) { if (places[a]) say('blueprints/locations.list.txt', at + ': alias «' + a + '» is a place of its own'); aliasOf[a] = at; } continue; }
    const tc = /^(touch|read|elsedo|gate):\s*(\S+)/.exec(l);
    if (tc && places[at] && !places[at].has(tc[2])) say('blueprints/locations.list.txt', at + ': ' + tc[1] + ' «' + tc[2] + '» has no such name in ' + at + '.names.txt');
    const rule = /^(lockdoors|shutdoors|litwhile|hidewhile|statewhile|touch|read|mapmark|becomes|follows|cold|fog):\s*(.+)$/.exec(l);
    if (rule) placeRules.push([at, rule[1], rule[2]]);
    const ed = /^elsedo:\s*\S+\s+(.+)$/.exec(l);
    if (ed) placeRules.push([at, 'elsedo', ed[1]]);
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
  if (word === 'bird' || word === 'player' || word === 'sky' || word.startsWith('npc.') || prefixes.some(p => word.startsWith(p))) return null; // (a module's own places: StoryWhere.Prefixed, read from the code)
  const [id, piece] = word.split('.');
  if (!places[id]) return 'no place «' + id + '» in locations.list.txt';
  return piece && !places[id].has(piece) ? 'no piece «' + piece + '» in ' + (aliasOf[id] || id) + '.names.txt' : null;
};
// a word naming a piece of a known place, anywhere in a line of steps
const checkDotted = (file, words) => { for (const w of words) { const m = /^(\w+)\.(\w+)$/.exec(w); if (m && places[m[1]]) { if (words[0] === 'camera' && shots[m[1]].has(m[2])) continue; const why = placeWhy(w); if (why) say(file, why); } } };

// people's templates
const npcs = new Set();
for (const f of txt('npc')) { if (f.startsWith('_')) continue; for (const l of lines('npc', f)) { const m = /^\[npc\s+(\S+)\]/.exec(l); if (m) npcs.add(m[1]); } } // («_» files: the author's own, not in the mod — the story may not name them)

// the sound catalogue: assets/story/sounds (AudioBank): every word of a line is a sound the game can look for or an option it reads, a name
// is written once, a file it names is there. What the game itself has: tools/game_sounds.txt (the game writes it: the «Звуки» tab)
const gameList = path.join(__dirname, 'game_sounds.txt'), gameSfx = new Set(), gameTracks = new Set();
const haveList = fs.existsSync(gameList);
if (haveList) for (const l of fs.readFileSync(gameList, 'utf8').split(/\r?\n/)) { const [k, n] = l.split(' '); if (k === 'sfx') gameSfx.add(n); else if (k === 'track') gameTracks.add(n); }
const bank = new Map();
const soundFile = /\.(ogg|wav|mp3)$/i, sceneWords = ['off', 'game', 'stop', 'all'];
for (const f of txt('sounds')) for (const l of lines('sounds', f)) {
  if (!l) continue;
  const w = l.split(/\s+/), at = 'sounds/' + f + ' «' + w[0] + '»', e = { tokens: [], flags: [] };
  if (!/^\w+$/.test(w[0])) say(at, 'a name: latin letters, digits, «_» only');
  if (/^sfx_/i.test(w[0])) say(at, 'a name on «sfx_» hides the game\'s own sound');
  if (sceneWords.includes(w[0])) say(at, 'a word of the scenes cannot be a name');
  if (bank.has(w[0])) say(at, 'written twice: the last would win without a word');
  for (const t of w.slice(1)) {
    const opt = /^(\w+)=(.*)$/.exec(t);
    if (opt) {
      if (!['vol', 'pitch', 'range'].includes(opt[1])) say(at, 'no option «' + t + '» (vol= pitch= range= 2d wet)');
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
}
// the places the place conditions name (placed, gate, hearth, state, been, near, inside, npcat)
const condPlace = { placed: 1, gate: 1, hearth: 1, state: 1, been: 1, near: 1, inside: 1, npcat: 1 };
// the kinds that ask only the world (Kind.World): the server's rules may use nothing else — it has no player, and there the rule would never hold
const worldWords = new Set();
{
  const regs = [...code.matchAll(/StoryCond\.Register\("(\w+)"/g)];
  regs.forEach((m, i) => { if (/\bWorld = true\b/.test(code.slice(m.index, i + 1 < regs.length ? regs[i + 1].index : m.index + 800).split(/\n\s*\n/)[0].slice(0, 900))) worldWords.add(m[1]); });
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
const asked = { flag: [], world: [], var: [], wvar: [], stamp: [] }, setWords = { flag: new Set(), world: new Set(), var: new Set(), wvar: new Set(), stamp: new Set() };
const noteAsked = (at, w) => { if (w[0] === 'since' && w[1]) asked.stamp.push([at, w[1]]); else if (asked[w[0]] && w[0] !== 'stamp' && w[1]) asked[w[0]].push([at, w[1]]); };
const raisedInData = new Set();
const condsWhy = (at, text, worldOnly) => {
  if (!text) return;
  for (const c of text.split(',')) {
    const w = c.trim().replace(/^!/, '').split(/\s+/);
    if (!w[0]) continue;
    if (!condWords.has(w[0])) { say(at, 'no condition «' + w[0] + '» (StoryCond)'); continue; }
    if (worldOnly && !worldWords.has(w[0])) say(at, '«' + w[0] + '» asks a player, and the server asks this with none: only ' + [...worldWords].join(', '));
    if (condPlace[w[0]] && w[1]) { const why = placeWhy(w[1]); if (why) say(at, w[0] + ': ' + why); }
    wordsWhy(at, 'condition', condUsage[w[0]], w);
    noteAsked(at, w);
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
               kind === 'read' ? (val.includes('::') ? val.slice(val.split(/\s+/)[0].length, val.indexOf('::')) : '') : val;
  if (text.trim() !== '-') condsWhy(where, text.trim(), kind === 'becomes' || kind === 'follows' || kind === 'cold' || kind === 'fog');
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
// the words after a sound's name and place (StoryAudio reads these and passes over any other without a word)
const audioWords = {
  sound: { flags: ['2d', 'wet', 'follow', 'wait'], valued: ['vol', 'range', 'pitch'] },
  bed: { flags: ['2d', 'wet', 'follow', 'all'], valued: ['vol', 'range', 'pitch'], fade: true },
  music: { flags: ['once', 'wait', 'all'], valued: ['keep', 'vol'], fade: true },
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
  if (w[0] === 'sound') return w[1] === 'stop' || ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'wait'].includes(w[2]) ? null : w[2]; // (a module's own: StoryAudio.PlaceOf)
  if (w[0] === 'bed') return w.length < 3 || w[1] === 'off' || !isNaN(Number(w[2])) || ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'all'].includes(w[2]) ? null : w[2]; // (StoryAudio.BedPlaceOf)
  const v = verbsOf[w[0]];
  const e = v ? v.verbs[w[v.at]] : stepPlaces[w[0]];
  if (!e) return null;
  let i = e.at;
  if (e.after) { i = w.indexOf(e.after); i = i > 0 ? i + 1 : 0; }
  return i > 0 && i < w.length && !e.not.includes(w[i]) ? w[i] : null;
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
    if (w[0] === 'npc' && w.length > 1 && !npcs.has(w[1])) say(at, 'no person «' + w[1] + '» in assets/story/npc');
    const place = stepPlace(w);
    if (place) { const why = placeWhy(place); if (why) say(at, why); }
    const heard = w[0] === 'sound' && w[1] !== 'stop' ? w[1] : w[0] === 'npc' && w[2] === 'fade' ? w.find((x, i) => i > 2 && isNaN(Number(x))) : null;
    if (heard) { const why = soundWhy(heard); if (why) say(at, why); }
    if (w[0] === 'music' && w[1] && w[1] !== 'off' && w[1] !== 'game') { const why = trackWhy(w[1]); if (why) say(at, why); }
    if (w[0] === 'bed' && w[1] && w[1] !== 'off') { const why = bedWhy(w[1]); if (why) say(at, why); }
    audioWordsWhy(at, w);
    checkDotted(at, w);
    optionsWhy(at, w);
  }
}

// the saga's chapters (assets/story/saga): a quest or tale with saga lines names one that is there
const chapters = new Set();
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
    for (const a of qu.order) {
      if (!reached.has(a)) say(f + ' [' + qu.id + '.' + a + ']', 'no stage leads here');
      if (!ends.has(a)) say(f + ' [' + qu.id + '.' + a + ']', 'never comes to the end: a player here is stuck for good');
    }
    // a quest of the server keeps nothing in a character, and its stage is one for the world (StoryQuestsServer)
    if (qu.server) {
      serverQuests.add(qu.id);
      for (const [at, key, val, inWay] of qu.said) {
        if (key === 'saga' || key === 'nudge' || key === 'choice' || inWay) say(at, 'a quest of the server has no «' + (inWay ? 'way' : key) + '»: nothing of it is kept in a player; what happened goes into the saga through assets/story/tales');
        if (key === 'start' || key === 'done') for (const a of val.split(';')) { const w = a.trim().split(/\s+/)[0]; if (personalActs.includes(w)) say(at, 'a quest of the server may not «' + w + '»: it writes into a character, or moves one player only'); }
      }
      for (const a of qu.order) { const s = qu.info[a], i = qu.order.indexOf(a), own = s.next || (i + 1 < qu.order.length ? qu.order[i + 1] : 'end'); if (own === 'end' && s.fields.has('done')) say(f + ' [' + qu.id + '.' + a + ']', 'the last stage of a quest of the server: «done» could be lost with its player; put it in «start:» of this stage'); }
      for (const a of qu.order) if (a !== a.toLowerCase()) say(f + ' [' + qu.id + '.' + a + ']', 'a stage of a quest of the server in lower case: the world keeps its keys so');
      for (const id of qu.order) { const sc = (qu.said.find(x => x[0].endsWith('.' + id + ']') && x[1] === 'scene') || [])[2]; if (sc) serverScenes.add(sc.split(/\s+/)[0]); }
    }
  }
}

// people
const placeKinds = new Set([...fs.readFileSync(path.join(srcDir, 'NpcPlaces.cs'), 'utf8').matchAll(/\bK\(new Kind\s*\{\s*Id = "(\w+)"/g)].map(m => m[1]));
for (const f of txt('people')) {
  let at = f;
  for (const l of lines('people', f)) {
    const head = /^\[person\s+(\S+)\]/.exec(l);
    if (head) { at = f + ' [' + head[1] + ']'; continue; }
    const sp = /^spot:\s*(\S+)\s+(\S+)(.*)$/.exec(l);
    if (sp) {
      const why = placeWhy(sp[2]); if (why) say(at, 'spot ' + why);
      const rest = sp[3].trim().split(/\s+/);
      { const kind = rest[0] === 'by' ? rest[2] : rest[0]; if (!placeKinds.has(kind)) say(at, 'spot: no kind «' + kind + '» (NpcPlaces.Kinds)'); }
      rest.forEach((x, i) => { if (rest[i - 1] === 'by') { const w2 = placeWhy(x); if (w2) say(at, 'spot by ' + w2); } });
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
    const m = /^(npc|live|after):\s*(.*)$/.exec(l);
    if (!m) continue;
    if (m[1] === 'npc') { if (!npcs.has(m[2])) say(at, 'no person «' + m[2] + '» in assets/story/npc'); continue; }
    if (m[1] === 'after') { condsWhy(at, m[2], true); continue; }
    const when = m[2].indexOf(' when ');
    if (when >= 0) condsWhy(at, m[2].slice(when + 6), true);
    checkDotted(at, (when >= 0 ? m[2].slice(0, when) : m[2]).split(/\s+/));
    { const home = (when >= 0 ? m[2].slice(0, when) : m[2]).split(/\s+/); for (let i = 0; i < home.length; i++) if (i === 0 || home[i - 1] === 'spawn' || home[i - 1] === 'bed') { const why = placeWhy(home[i]); if (why) say(at, 'live ' + why); } }
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
const talkConds = (at, s) => {
  for (const c of s.split(',')) {
    const w = c.trim().replace(/^!/, '').split(/\s+/);
    noteAsked(at, w);
    if (w[0] === 'step') { if (!stagesOf[w[1]] || !stagesOf[w[1]].has(w[2])) say(at, 'no stage «' + w[2] + '» in quest «' + w[1] + '»'); else talkSteps.add(w[1] + ' ' + w[2]); }
    if (w[0] === 'npcat' && w[1]) { const why = placeWhy(w[1]); if (why) say(at, why); }
  }
};
for (const f of txt('talk')) {
  let talk = '', places = new Set(), to = [], prevAnswer = false;
  const done = () => { for (const [at, t] of to) if (t !== 'end' && t !== 'homes' && !places.has(t)) say(at, 'no place «' + t + '» in talk «' + talk + '»'); if (talk && !places.has('start')) say(f, 'talk «' + talk + '»: no @start'); };
  lines('talk', f).forEach((l, i) => {
    if (l.startsWith('~') && !/^~(hugin|munin|any)\s+(aside|aloud)\b[^:]*:\s*\S/.test(l)) say(f + ':' + (i + 1), 'a raven line: ~hugin|munin|any aside|aloud: words');
    const head = /^\[talk\s+([^\s:\]]+)/.exec(l);
    if (head) { done(); talk = head[1]; talks.add(talk); places = new Set(); to = []; prevAnswer = false; return; }
    if (l.startsWith('@')) { places.add(l.slice(1).replace(/\+$/, '').trim()); prevAnswer = false; return; }
    if (l.startsWith('??')) { talkConds(f + ':' + (i + 1), l.slice(2)); return; }
    if (l.startsWith('>|') && !prevAnswer) say(f + ':' + (i + 1), '«>|» is a way of the answer right above it, and there is none: the game shows it always');
    prevAnswer = l.startsWith('>') || (prevAnswer && (l === '' || l.startsWith('~') || l.startsWith('#')));
    const after = l.startsWith('>') && l.includes('=>') ? l.slice(l.indexOf('=>') + 2) : '';
    for (const part of after.split(/\s(?=[?!])/)) { if (part.startsWith('?')) talkConds(f + ':' + (i + 1), part.slice(1)); const r = /^!raise\s+(\S+)/.exec(part); if (r) talkRaised.add(r[1].toLowerCase()); const st = /^!(flag|world|var|wvar|stamp)\s+(\S+)/.exec(part); if (st) setWords[st[1]].add(st[2]); const sq = /^!quest\s+(\S+)/.exec(part); if (sq && serverQuests.has(sq[1])) say(f + ':' + (i + 1), '«!quest ' + sq[1] + '»: a quest of the server is never kept in a character (Story.SetQuest refuses it): move its stage through the quest itself (an «on:» on «!raise»)'); }
    const m = /^>.*=>\s*([^\s?!]+)(\s+once)?/.exec(l);
    if (m) { to.push([f + ':' + (i + 1), m[1]]); if (m[2] && m[1] === 'end') say(f + ':' + (i + 1), 'once needs a place to lead to'); }
  });
  done();
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
    if (h) { done(); id = h[1]; has = new Set(); chapter = ''; return; }
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
  for (const f of txt('looks')) { const ls = lines('looks', f); if (ls.includes('default: yes')) defaults++; if (!ls.some(l => l.startsWith('id:'))) say('looks/' + f, 'no «id:»'); }
  if (defaults !== 1) say('looks', defaults + ' files say «default: yes»: one look is a scene\'s bird by default');
  for (const f of txt('npc')) for (const l of lines('npc', f)) { const m = /^raven_look:\s*(\S+)/.exec(l); if (m && m[1] !== 'game' && !looks.has(m[1])) say('npc/' + f, 'no look «' + m[1] + '» in assets/story/looks'); }
  for (const f of txt('scenes')) for (const l of lines('scenes', f)) { const m = /^(?:if [^:]*:\s*)?bird come\b.*\blook\s+(\S+)/.exec(l); if (m && !looks.has(m[1])) say('scenes/' + f, 'no look «' + m[1] + '» in assets/story/looks'); }
}

// a scene of a quest of the server: its «do» writes nothing into a character either
for (const f of txt('scenes')) {
  let sc = '';
  for (const l of lines('scenes', f)) {
    const h = /^\[scene\s+(\S+)\]/.exec(l);
    if (h) { sc = h[1]; continue; }
    const m = /^(?:if [^:]*:\s*)?do\s+(\S+)/.exec(l);
    if (m && serverScenes.has(sc) && personalActs.includes(m[1])) say(f + ' [' + sc + ']', 'a scene of a quest of the server may not «do ' + m[1] + '»: it writes into a character');
    const own = /^(?:if [^:]*:\s*)?(own|raven)\s/.exec(l);
    if (own && serverScenes.has(sc)) say(f + ' [' + sc + ']', 'a scene of a quest of the server may not «' + own[1] + '»: it acts on one character');
  }
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

for (const w of warnings) console.log('STORY WARNING ' + w);
if (problems.length) { console.log(problems.join('\n') + '\n\n' + problems.length + ' broken in the story\'s data: assets/story.'); process.exit(1); }
if (writeLedger) writeLedger();
console.log('storycheck: ok (' + scenes.size + ' scenes, ' + used.size + ' played by quests, ' + npcs.size + ' people, ' + Object.keys(places).length + ' places)');

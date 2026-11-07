// People's words hold together and stay varied (the user 2026-11-05: as varied as the ravens', and sewn into the build, so that a story written
// later is held to it without anyone remembering). Reads every NPC phrase file (src/NpcPhrases.cs, src/Npc*Patterns.cs) and the story's retellings
// (assets/story/tales). Ravens' words: talkcheck, bookcheck.
//   node tools/npcwordcheck.js                 the problems; exit code 1 stops rw_build
//   node tools/npcwordcheck.js stats           the numbers: themes, lines, variants a theme, the thinnest
//   node tools/npcwordcheck.js --tighten       themes that have grown leave the baseline (it only shrinks)
//   node tools/npcwordcheck.js --also <file>   checks a phrase file not in src yet too (write it, check it, then put it in: a broken one stops every chat's build)
// Rules (errors):
//   thin      a theme with fewer than MinVariants different lines. Old thin ones stand in tools/npcword_baseline.json and may only leave it; a new
//             theme — a story person's own [heid:idle] too — is born varied. One line that must be exact belongs in a scene or a talk's place, not in a theme
//   tale      a retelling with fewer than MinTale variants of saw, heard or hearsay
//   unknown   a {name} that is no list of parts and no value (a line with one is silently dropped in the game)
//   split     «бачив{|ла}»: the engine only pastes, that gives «бачивла»; write «бачи{в|ла}»
//   name      a name after a preposition: names stay in the nominative (no «з {neighbour}»)
//   who       «ти … а|б» (the speaker's form said of you: {а|б}) or «я … {а|б}» (yours said of the speaker: а|б)
//   listener  a {а|б} form in the lines two people say to each other: it would be the player's, not the listener's
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), src = path.join(root, 'src');
const MinVariants = 6, MinTale = 3;
const baseFile = path.join(__dirname, 'npcword_baseline.json');

const Values = new Set(['name', 'npc', 'job', 'base', 'other', 'neighbour', 'alone', 'weather', 'daypart', 'food', 'wage', 'item', 'why', 'friend', 'foe', 'feud_a', 'feud_b', 'irked_by', 'friend_near', 'goal_what', 'arc_other']);
const TwoPeople = new Set(['розмова', 'відповідь', 'п_сон', 'в_сон', 'п_їжа', 'в_їжа', 'п_робота', 'в_робота', 'п_погода', 'в_погода', 'п_ворони', 'в_ворони', 'п_дім', 'в_дім', 'с_розмова_вечір', 'с_розмова_дощ', 'с_розмова_брама', 'с_розмова_вогонь', 'с_розмова_серце']);
const Names = 'name|other|neighbour|friend|foe|friend_near|feud_a|feud_b|irked_by|arc_other|m_[a-z_]+';
const NameAfter = new RegExp('(^|[\\s«(])(з|із|зі|у|в|через|до|для|від|про|за|крім|без|на|біля|проти|після|перед|над|під)\\s+\\{(' + Names + ')\\}', 'iu');

const files = fs.readdirSync(src).filter(f => f === 'NpcPhrases.cs' || /^Npc.*Patterns\.cs$/.test(f));
const T = {}, P = {}, lines = [];
const alsoAt = process.argv.indexOf('--also');
if (alsoAt > 0) for (const x of process.argv.slice(alsoAt + 1)) files.push(x);
for (const f of files) {
  const s = fs.readFileSync(path.isAbsolute(f) ? f : path.join(src, f), 'utf8');
  const re = /@"((?:[^"]|"")*)"/g; let m;
  while ((m = re.exec(s))) {
    const text = m[1].split('""').join('"');
    if (!/^\s*@\S/m.test(text) && !/^\s*\[[^\]]+\]/m.test(text)) continue;
    let cur = null;
    for (const raw of text.split('\n')) {
      const l = raw.trim();
      if (!l || l.startsWith('#') || l.startsWith('//')) continue;
      if (l.startsWith('@')) { cur = l.slice(1).trim(); P[cur] = P[cur] || []; continue; }
      if (l.startsWith('[')) {
        const c = l.indexOf(']'); if (c < 0) continue;
        let head = l.slice(1, c).trim(); const q = head.indexOf(' ?'); if (q >= 0) head = head.slice(0, q).trim();
        const body = l.slice(c + 1).trim();
        (T[head] = T[head] || []).push(body); cur = null;
        lines.push({ f, list: '[' + head + ']', l: body });
        continue;
      }
      if (cur) { P[cur].push(l); lines.push({ f, list: cur, l }); }
    }
  }
}

const strip = l => l.replace(/^~\S+\s+/, '');
const memo = {};
function listCount(name, depth) {
  if (depth > 6) return 1;
  if (memo[name] !== undefined) return memo[name];
  let sum = 0; for (const l of P[name] || []) sum += lineCount(strip(l), depth + 1);
  return (memo[name] = Math.max(1, sum));
}
function lineCount(line, depth) {
  let n = 1;
  for (const m of line.matchAll(/\{([^}|]+)\}/g)) {
    let k = m[1]; const opt = k.endsWith('?'); if (opt) k = k.slice(0, -1);
    if (P[k]) n *= listCount(k, depth) + (opt ? 1 : 0);
  }
  return n;
}
const variants = {};
for (const t in T) { let v = 0; for (const tp of T[t]) v += lineCount(strip(tp), 0); variants[t] = v; }

const args = process.argv.slice(2);
if (args[0] === 'stats') {
  const v = Object.keys(variants).sort((a, b) => variants[a] - variants[b]);
  let partLines = 0; for (const k in P) partLines += P[k].length;
  const med = variants[v[Math.floor(v.length / 2)]];
  console.log('themes ' + v.length + ', templates ' + Object.values(T).reduce((a, x) => a + x.length, 0) + ', lists ' + Object.keys(P).length + ', part lines ' + partLines + ', median variants ' + med);
  console.log('thinnest: ' + v.slice(0, 25).map(t => t + '(' + variants[t] + ')').join(' '));
  process.exit(0);
}

let baseline = [];
try { baseline = JSON.parse(fs.readFileSync(baseFile, 'utf8')).thin || []; } catch (e) { /* quiet: no baseline yet — every thin theme is told */ }
if (args[0] === '--tighten' || args[0] === '--new-baseline') {
  const thin = Object.keys(variants).filter(t => variants[t] < MinVariants && (args[0] === '--new-baseline' || baseline.includes(t))).sort();
  fs.writeFileSync(baseFile, JSON.stringify({ note: 'old themes with too few variants: this list only shrinks (npcwordcheck --tighten); a new theme is born varied', thin }, null, 1) + '\n');
  console.log('npcwordcheck: baseline ' + thin.length + ' (was ' + baseline.length + ')');
  process.exit(0);
}

const errors = [];
const say = (rule, x) => errors.push(rule + ' [' + x.f + ' ' + (x.list || '') + '] ' + x.l);
for (const t in variants) if (variants[t] < MinVariants && !baseline.includes(t)) errors.push('thin [' + t + '] ' + variants[t] + ' variant(s), at least ' + MinVariants + ': more lines or parts');

for (const x of lines) {
  const body = strip(x.l);
  for (const m of body.matchAll(/\{([^}]*)\}/g)) {
    const k = m[1].replace(/\?$/, '');
    if (k.includes('|')) { if (k.startsWith('|') && /[вл]$/u.test(body.slice(0, m.index))) say('split', x); continue; }
    if (!(P[k] || Values.has(k) || /^(m|ago|n)_/.test(k) || /^(story_|goal_)/.test(k))) say('unknown {' + k + '}', x);
  }
  if (NameAfter.test(body)) say('name', x);
  if (TwoPeople.has(x.list) && /\{[^}|]*\|[^}]*\}/.test(body)) say('listener', x);
  for (const clause of body.split(/[.!?…:;]|,\s+(?:а|і|й|та|що|бо|коли|поки|як)\s+|\s—\s/)) {
    const plain = clause.replace(/\{[^}]*\}/g, ' ');
    const you = /(^|[\s,])[Тт]и(\s|$)/u.test(clause), me = /(^|[\s,])[Яя](\s|$)/u.test(clause);
    if (you && !me && /\S\|\S/.test(plain)) say('who (ти + форма людини)', x);
    if (me && !you && /\{[^}|]*\|[^}]*\}/.test(clause)) say('who (я + твоя форма)', x);
  }
}

// the story's retellings
const talesDir = path.join(root, 'assets', 'story', 'tales');
if (fs.existsSync(talesDir)) for (const f of fs.readdirSync(talesDir).filter(n => n.endsWith('.txt'))) {
  let id = null, n = null;
  const flush = () => { if (id) for (const k of ['saw', 'heard', 'hearsay']) if (n[k] < MinTale) errors.push('tale [' + f + ' ' + id + '] ' + k + ': ' + n[k] + ' variant(s), at least ' + MinTale); };
  for (const raw of fs.readFileSync(path.join(talesDir, f), 'utf8').split(/\r?\n/)) {
    const l = raw.trim();
    const h = /^\[tale\s+(\S+)\]/.exec(l);
    if (h) { flush(); id = h[1]; n = { saw: 0, heard: 0, hearsay: 0 }; continue; }
    const m = /^(saw|heard|hearsay):/.exec(l);
    if (m && id) n[m[1]]++;
  }
  flush();
}

for (const e of errors.slice(0, 40)) console.log(e);
if (errors.length > 40) console.log('… and ' + (errors.length - 40) + ' more');
console.log('npcwordcheck: ' + errors.length + ' problem(s)');
process.exit(errors.length ? 1 : 0);

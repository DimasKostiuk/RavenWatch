// One run from outside the game, the game open and in a world (studio/request.txt, studio/out). The game half of tools/anim.js (the one
// entry, review 2 2026-11-10): anim.js <verb> …; the studio's folder is tools/studio.path (one line). What to read comes back at once.
//   clip <clip> [views]        the clip on the stage, on the body (the dummy, Heid, the dvergr's dummy: tools/body.js ofCell reads the row's body cell)
//                              and with the thing its row in docs/CLIPS.md names: measured and filmed in one run
//                              (the short report says each thing once: one VERDICT line an event, pose rows «Δ» — only what changed; all of it whole in .full.txt);
//                              prints the short report (VERDICT with a «fix:» command where the core could reckon one, what was asked, the tempo, each
//                              key frame's pose in words) and the sheets: stage_<clip>_key.png (a row a moment of the VERDICT, the wants, the marks;
//                              its two views; labelled), stage_<clip>_diff.png (this take | the take before, or what «ab» plays) — and only with
//                              views asked, stage_<clip>_<view>.png of the whole run (side,chest,… : AutoViews names what the clip would take)
//   views <clip> <views>       the same as clip <clip> <views>
//                              Under the report: what this take changed against the one before — the VERDICT lines gone, the new ones, how many stay
//                              (by tag and subject: verdictLines), so the last change is judged without reading two reports
//   fix <clip> [n …|list] [--take]   the «fix:» commands of the last report run as they stand (all, or those numbered; «list» only shows them): the core
//                              reckoned them, nobody retypes them. Only of the file as it is now; a command that fails stops the rest. A command that would
//                              key a channel the clip's movements make («do») is NOT run: it names the movement to change instead; --take runs it anyway
//                              (the channel is the author's from then on). A take of such a clip ends with the movements that play at each line's moment
//   report <clip>              the last report of the clip again — only if it is of the file as it is now (the file's hash in its head, AnimStage.Hash);
//                              of another version it refuses: «take it again»
//   scene --dry <scene>        the scene run dry, on node, no game (tools/scene_dry.js): a row a step — when it starts, how long it takes, who is in
//                              the frame — and the warnings the file alone shows (words over a bird in flight, a person with no «test: npc … at», a
//                              shot held long, a run of waits, a mark the clip has not got)
//   scene --cover <scene>      camera lines proposed for the scene's talks that have no shots of their own (scene_dry.js cover): both first, over the
//                              listener's shoulder at who speaks, close on a short line; nothing is written — take what fits
//   scene <scene> [fps] [from] the same table first, then the scene at its place in the game: prints its VERDICT, the steps with what each came to,
//                              each body's numbers, the sheets
//   done <clip>                the user approved it: its frames and report go (each take is ~5 MB)
//   approved [clip …] [--save] every clip the user has seen and accepted (docs/CLIPS.md, its state «бачив користувач», not «не затверджено»), or the ones
//                              named, each taken TWICE: a list for the USER to judge — each rule that fires, its moment, its words, «in both takes» or
//                              «only in one» (a rule that gives two answers on one clip is not to be trusted yet), and the key sheet to look at — written
//                              to studio/out/approved_review.txt. Nothing is hidden or loosened by it: what he accepted may be wrong (the user
//                              2026-11-10); he says over the frame whether the clip or the rule is wrong. Also its numbers against
//                              tools/clip_numbers.json (--save writes them): after a change in the core, what moved in what was already approved
//   status                     one line each: the game answers the studio or not (raven stage status), a scene playing, the stage (up, on what,
//                              playing what), the studio's last jobs (another chat may be at it)
// A clip's take ends with «BY EYE» when its last three saved versions each changed the same part of the body by hand (byEye below): numbers turned a
// take a guess — it names the tool that reckons that part.
// It refuses while a scene plays in the game (a run cut off the blue_fire the user was watching, 2026-11-04). Whether the user is at the keys, or
// another chat uses the studio, it cannot see: «status» shows the last jobs, ask first. A clip's run takes its mannequin and its timer off the screen
// when it is done.
// After itself (the user 2026-11-07, 80 files left behind): a clip's run first takes off every other clip's stage files older than StaleMin
// minutes (a take still being looked at by another chat is fresher); «done» takes off the clip's own. A scene's run (the user 2026-11-14: 258 MB lay in
// studio/out) takes off every other scene's files older than StaleMin, and any other loose shot of the studio older than StaleDays days; its own
// last run stays till the next run of it replaces it.
// A report is of one version of the clip's file (review 2 2026-11-10: a report from before an edit was read as the edit's): its head carries the
// file's hash, and «clip» warns, «report» refuses, when the file is not that version any more.
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const root = path.join(__dirname, '..');
const numbersFile = path.join(root, 'tools', 'clip_numbers.json');
// the studio's folder, from tools/studio.path (the game's profile: config/RavenWatch/studio)
function studioDir() {
  const p = path.join(root, 'tools', 'studio.path');
  if (!fs.existsSync(p)) throw new Error('no tools/studio.path: one line, the studio folder of the test profile (…/BepInEx/config/RavenWatch/studio)');
  const d = fs.readFileSync(p, 'utf8').trim().replace(/\\/g, '/');
  if (!d) throw new Error('tools/studio.path is empty');
  return d;
}
let studio, out;
const inStudio = () => { if (!studio) { studio = studioDir(); out = path.join(studio, 'out'); if (!fs.existsSync(out)) fs.mkdirSync(out, { recursive: true }); } };
const sleep = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const until = (test, seconds, what) => { const end = Date.now() + seconds * 1000; while (Date.now() < end) { const r = test(); if (r) return r; sleep(400); } throw new Error('no ' + what + ' in ' + seconds + ' s: is the game open and in a world?'); };
const ask = (line, seconds) => { fs.writeFileSync(path.join(studio, 'request.txt'), line + '\n'); until(() => !fs.existsSync(path.join(studio, 'request.txt')), seconds || 20, 'answer to «' + line + '»'); };
const sheets = prefix => fs.readdirSync(out).filter(f => f.startsWith(prefix) && f.endsWith('.png')).map(f => path.join(out, f).replace(/\\/g, '/'));
const clear = prefix => { for (const f of fs.readdirSync(out)) if ((f === prefix + '.txt') || (f === prefix + '.full.txt') || (f === prefix + '.wav') || (f.startsWith(prefix + '_') && (f.endsWith('.png') || f.endsWith('.txt')))) fs.unlinkSync(path.join(out, f)); };
// the studio's log (studio/out/log.txt): its last lines; the answer the game wrote to a «raven …» line
const logLines = () => { const p = path.join(out, 'log.txt'); return fs.existsSync(p) ? fs.readFileSync(p, 'utf8').split('\n').filter(Boolean) : []; };
const answerOf = line => { const l = logLines().reverse().find(x => x.includes('  ' + line + '  →  ')); return l ? l.slice(l.indexOf('→') + 1).trim() : null; };

// the clip file's version as the stage writes it into a report's head (AnimStage.Hash: SHA-1 of the text, no «\r», no byte-order mark, 8 hex digits)
const hashOf = clip => { const p = path.join(root, 'assets', 'story', 'clips', clip.split('.')[0] + '.txt'); if (!fs.existsSync(p)) return null; let t = fs.readFileSync(p, 'utf8'); if (t.charCodeAt(0) === 0xFEFF) t = t.slice(1); return crypto.createHash('sha1').update(t.replace(/\r/g, ''), 'utf8').digest('hex').slice(0, 8); };
// why a report is not of the file as it is now (null: it is, or the clip has no file — built in)
const stale = (clip, text) => {
  const m = /, file ([0-9a-f]{8}) saved ([^\n]+)/.exec(text.split('\n')[0] || ''), now = hashOf(clip);
  if (now === null) return null;
  if (!m) return 'the report has no file version in its head (a stage built before 2026-11-10): take it again';
  return m[1] === now ? null : 'the report is of another version of the file (the report\'s ' + m[1] + ', saved ' + m[2] + '; the file now ' + now + '): take it again';
};

// the last scene the log tells of has not ended: its name
const scenePlaying = () => {
  const p = path.join(studio, '..', '..', '..', 'LogOutput.log');
  if (!fs.existsSync(p)) return null;
  const text = fs.readFileSync(p, 'utf8');
  const all = text.match(/Story: track: сцена «[^»]+» (почалась|скінчилась|перервана)/g) || [];
  const last = all[all.length - 1] || '';
  return last.endsWith('почалась') ? last.slice(last.indexOf('«')) : null;
};
// the scene's own file and when it was saved (what the game has just read: a run reloads before it)
const sceneFile = id => { const d = path.join(root, 'assets', 'story', 'scenes'); for (const n of fs.readdirSync(d)) { const p = path.join(d, n); if (fs.readFileSync(p, 'utf8').includes('[scene ' + id + ']')) return n + ', saved ' + fs.statSync(p).mtime.toLocaleString('uk-UA'); } return '(no file names it)'; };
const StaleMin = 30;
const StaleDays = 1;
const dropStaleScenes = keep => { let n = 0; for (const f of fs.readdirSync(out)) { if (f === 'log.txt' || f === 'request.txt' || f.startsWith('stage_') || f.startsWith(keep + '.') || f.startsWith(keep + '_')) continue; const p = path.join(out, f); const age = Date.now() - fs.statSync(p).mtimeMs; if (fs.statSync(p).isFile() && (f.startsWith('sc_') ? age > StaleMin * 60000 : age > StaleDays * 86400000)) { fs.unlinkSync(p); n++; } } return n; };
const dropStale = keep => { let n = 0; for (const f of fs.readdirSync(out)) { if (!f.startsWith('stage_') || f.startsWith('stage_' + keep + '.') || f.startsWith('stage_' + keep + '_')) continue; const p = path.join(out, f); if (Date.now() - fs.statSync(p).mtimeMs > StaleMin * 60000) { fs.unlinkSync(p); n++; } } return n; };
const rowOf = clip => fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').find(l => l.startsWith('| `' + clip.split('.')[0] + '` |')) || ''; // (a part: its whole clip's row — the body, the thing)
// (the state cell, the row's last: «бачив користувач» there and no «не затверджено» — shield_kneel's «бачив користувач; … новіша версія … не затверджено» was counted, 2026-11-10)
const approvedNames = () => fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').filter(l => { if (!l.startsWith('| `')) return false; const c = l.split('|').map(x => x.trim()).filter(Boolean); const state = c[c.length - 1] || ''; return state.includes('бачив користувач') && !state.includes('не затверджено'); }).map(l => /^\| `([\w-]+)`/.exec(l)).filter(Boolean).map(m => m[1]);
const verdictOf = text => text.slice(text.indexOf('VERDICT'), text.indexOf('pose: ')).trim();
// The words a scene takes a thing with («prop take <item> … square», «size <m>»: StoryProps.Read) — the stage takes it with the same ones, so a take
// plays what the scene will (2026-11-12: the stage knew none of them, and a thing its scene takes «square» was never taken so on the mannequin).
// «from <piece>» is the scene's own (the mannequin stands elsewhere), «free» the stage's anyway. Scenes that differ: the first, and the others said.
const sceneTake = item => {
  const d = path.join(root, 'assets', 'story', 'scenes'), found = [];
  for (const n of fs.readdirSync(d).sort()) fs.readFileSync(path.join(d, n), 'utf8').split('\n').forEach((raw, i) => {
    const w = raw.split('#')[0].trim().split(/\s+/), at = w.indexOf('prop');
    if (at < 0 || w[at + 1] !== 'take' || w[at + 2] !== item) return;
    const keep = [];
    for (let k = at + 3; k < w.length; k++) { if (w[k] === 'square') keep.push(w[k]); else if (w[k] === 'size' && w[k + 1]) keep.push(w[k], w[++k]); else if (w[k] === 'from' || w[k] === 'pose') k++; }
    found.push({ words: keep.join(' '), where: n + ':' + (i + 1) });
  });
  return found;
};

// A take's numbers kept with the keys they measured: stage_<clip>.full.txt copied to config/RavenWatch/player_anims_measures/<clip>/<NNN>.full.txt, NNN the
// saved version of the keys (player_anims_takes/<clip>/<NNN>.txt) whose text the report's own file hash names. The body's model is held against these
// (node tools/body.js: lengths, the elbow, the wrist, the head, the bones' speeds), and the report in studio/out lives only till the next take of another
// clip (dropStale): on 2026-11-12 the model's proof went from 15 takes to 1 that way. NOT in the takes folder itself: the stage counts every *.txt there
// as a take (AnimStage.Takes), and reports kept there made its take numbers wrong the same day.
function keepMeasure(clip) {
  const name = clip.split('.')[0], full = path.join(out, 'stage_' + clip + '.full.txt'), dir = path.join(studio, '..', 'player_anims_takes', name);
  if (!fs.existsSync(full) || !fs.existsSync(dir)) return;
  const text = fs.readFileSync(full, 'utf8'), h = (/, file ([0-9a-f]{8}) saved/.exec(text) || [])[1];
  const sha = t => { if (t.charCodeAt(0) === 0xFEFF) t = t.slice(1); return crypto.createHash('sha1').update(t.replace(/\r/g, ''), 'utf8').digest('hex').slice(0, 8); };
  const of = fs.readdirSync(dir).filter(f => /^\d+\.txt$/.test(f)).sort().filter(f => sha(fs.readFileSync(path.join(dir, f), 'utf8')) === h).pop();
  if (!h || !of) return; // (no saved keys of that file: numbers with nothing to hold them against)
  const to = path.join(studio, '..', 'player_anims_measures', name);
  fs.mkdirSync(to, { recursive: true });
  fs.writeFileSync(path.join(to, of.replace('.txt', (clip === name ? '' : '.' + clip.slice(name.length + 1)) + '.full.txt')), text);
}
function runClip(clip, views, keepOthers) {
  const report = path.join(out, 'stage_' + clip + '.txt');
  const before = fs.existsSync(report) ? measuredOf(clip, fs.readFileSync(report, 'utf8')) : null; // (the take before, while its report is still there)
  clear('stage_' + clip);
  const stale0 = keepOthers ? 0 : dropStale(clip); // (approved: the sheets of the clips before stay, the user looks at them after)
  if (stale0 > 0) console.log('older takes of other clips taken off: ' + stale0 + ' file(s)');
  const row = rowOf(clip), cells = row.split('|').map(c => c.trim());
  ask('raven stage down'); // (a clean stage each run: a speed, a game emote, a walk, fixtures, a second mannequin left by the last one changed the measure — review 2026-11-07)
  ask('raven stage up ' + require('./body.js').ofCell(cells[3]).stage); // (the body it was made on — the dummy, Heid, the dvergr's dummy: a clip on another is another clip; the row's body cell is read in one place, body.js)
  const thing = /реквізит `(\w+)`( на руці)?/.exec(row); // («на руці»: on its right forearm, as a person carries its own — Heid's basket)
  const arm = /зброя `(\w+)`(?: і `(\w+)`)?/.exec(row); // (a pose for a weapon in hand: the game's own stance for it under the clip, as on a guard)
  ask('raven stage wield ' + (arm ? arm[1] + (arm[2] ? ' ' + arm[2] : '') : 'none'));
  ask('raven stage play ' + clip);
  sleep(800);
  const from = cells[4] || ''; // (where the clip begins: «сидить на лаві …» — on the game's own bench, as a person sits; «сидить на землі …» — the game's sit; after «play»: the mannequin has come into the world by then)
  if (/^сидить на (лаві|стільці)/.test(from)) { ask('raven stage chair'); sleep(800); }
  else if (/^сидить на землі/.test(from)) { ask('raven stage emote sit'); sleep(800); }
  const taken = thing && !thing[2] ? sceneTake(thing[1]) : [];
  if (taken.length) console.log('the thing as its scene takes it (' + taken[0].where + '): ' + (taken[0].words || 'no words of its own') + taken.filter(t => t.words !== taken[0].words).map(t => ' · ANOTHER scene takes it otherwise (' + t.where + '): ' + (t.words || 'no words')).join(''));
  ask('raven stage prop ' + (thing ? thing[1] + (thing[2] ? ' arm' : taken.length && taken[0].words ? ' ' + taken[0].words : '') : 'none')); // (its .on channels do nothing with no thing)
  sleep(800);
  ask('raven stage check' + (views ? ' ' + views : ''));
  const text = until(() => { if (!fs.existsSync(report)) return null; const t = fs.readFileSync(report, 'utf8'); return t.includes('key sheet: coming') ? null : t; }, 180, 'report of ' + clip);
  sleep(500);
  const why = stale(clip, text);
  if (why) console.log('WARNING: the file changed during the take — ' + why);
  runClip.since = before ? since(before, measuredOf(clip, text)) : '';
  keepMeasure(clip);
  return text;
}

// A clip written by movements (tools/motion.js: «# do» lines, «# scripted:» channels): which of its movements make a channel, which play at a moment.
// A report's line and its «fix:» speak of keys; in such a clip the keys are the script's, so the mend belongs in a movement (redo) — a key set by hand
// takes its channel from the script for good (the review 2026-11-11: «fix» did that in silence, and the next «do» of that hand did nothing)
function scriptOf(name) {
  const clipTool = require('./clip.js'), motion = require('./motion.js');
  let c; try { c = clipTool.load(name.split('.')[0]); } catch (e) { return null; }
  const lines = motion.scriptOf(c).map((l, i) => { const w = l.split(/\s+/), m = /^([0-9.]+)-([0-9.]+)$/.exec(w[0]) || [0, 0, 0]; return { n: i + 1, line: l, t0: +m[1], t1: +m[2], verb: w[1], side: w[2] }; });
  return lines.length ? { lines, made: motion.listOf(c, 'scripted'), family: clipTool.family } : null;
}
const makes = (s, ch) => s.lines.filter(l => { // the movements that write a channel
  const side = /Left/.test(ch) ? 'left' : /Right/.test(ch) ? 'right' : null, mine = !side || l.side === side || l.side === 'both';
  if (/^@(Left|Right)Hand\.on/.test(ch)) return mine && (l.verb === 'grip' || l.verb === 'release');
  if (/^@(Left|Right)Hand/.test(ch)) return mine && (l.verb === 'reach' || l.verb === 'rest' || l.verb === 'grip');
  if (/Hand(Thumb|Index|Middle|Ring|Pinky)/.test(ch)) return mine && (l.verb === 'fingers' || l.verb === 'grip' || l.verb === 'release');
  if (/^@(Left|Right)Foot/.test(ch)) return mine && l.verb === 'step';
  if (/^Spine[12]$/.test(ch)) return ['lean', 'turn', 'sigh'].includes(l.verb);
  if (ch === 'move') return ['sink', 'weight', 'step'].includes(l.verb);
  if (/^@Look/.test(ch)) return ['look', 'nod', 'shake'].includes(l.verb);
  return false;
});
// the channels a command of clip.js keys (set, del, move, hold; a batch: each of its changes)
const keysOf = w => w[0] === 'batch' ? (w[2] || '').split(';').map(x => x.trim().split(/\s+/)).filter(o => ['set', 'del', 'move', 'hold'].includes(o[0])).map(o => o[1]) : ['set', 'del', 'move', 'hold'].includes(w[0]) ? [w[2]] : [];

// two takes of one clip, the one before and this one: what went, what came, how many stay
function since(was, now) {
  const a = [...new Set(was.map(x => x.key))], b = [...new Set(now.map(x => x.key))];
  const gone = a.filter(k => !b.includes(k)), came = b.filter(k => !a.includes(k)), stay = b.length - came.length;
  if (!a.length && !b.length) return 'against the take before: clean then, clean now';
  return 'against the take before: ' + gone.length + ' gone' + (gone.length ? ' (' + gone.join('; ') + ')' : '') + ', ' + came.length + ' new' + (came.length ? ' (' + came.join('; ') + ')' : '') + ', ' + stay + ' still there';
}

// the «fix:» commands of a report, as it wrote them (each: the words after «node tools/anim.js»)
const fixesOf = text => text.split('\n').map(l => /^\s*fix: node tools\/anim\.js (.+)$/.exec(l)).filter(Boolean).map(m => { const w = m[1].match(/"[^"]*"|\S+/g) || [], end = w.findIndex(x => x.startsWith('(') || x === '—' || x === '–'); return (end < 0 ? w : w.slice(0, end)).join(' '); });
// a command line's words, a quoted stretch one word (batch <clip> "set …; set …")
const wordsOf = line => (line.match(/"[^"]*"|\S+/g) || []).map(w => w.startsWith('"') ? w.slice(1, -1) : w);

// a take's VERDICT lines, each by what it is about — its tag and the part it names («[jerk] RightArm», «[miss] @RightHand», «[palm] right») —
// so two takes of one clip can be laid side by side: the same mistake twice, or a rule that answers once yes, once no
// a take's lines as measured, a line a rule and a bone: from stage_<clip>.full.txt (the short report folds one event into one line — BodyCheck.Fold —
// so a jerk that outlives its mended flight would read as «new»); a report from before the folding: its own lines
const measuredOf = (clip, text) => { const p = path.join(out, 'stage_' + clip + '.full.txt'); const full = fs.existsSync(p) ? fs.readFileSync(p, 'utf8') : ''; return verdictLines(/^VERDICT( as measured|: OK)/m.test(full) ? full : text); };
const verdictLines = text => {
  const v = text.slice(text.indexOf('VERDICT'), text.search(/^asked|^pose: /m) > 0 ? text.search(/^asked|^pose: /m) : undefined);
  const lines = [];
  for (const l of v.split('\n')) {
    const m = /^  - (\[[\w-]+\])\s+(\S+)?(.*)$/.exec(l);
    const whole = m ? m[1] + ' ' + (m[2] || '') + m[3] : '';
    if (m) lines.push({ key: m[1] === '[want]' ? whole.split(' (')[0] : (m[1] + ' ' + ((m[2] || '') + m[3]).split(':')[0].replace(/-?\d+(\.\d+)?/g, '#')).slice(0, 80), text: whole.slice(0, 220) }); // (a want: its own words tell it from the others; any other: what it is about up to its «:», the numbers out — the left foot's line is not the right's)
    else if (/^  - /.test(l)) lines.push({ key: l.slice(4, 40), text: l.slice(4, 220) });
  }
  return lines;
};

function status() {
  let game;
  try { ask('raven stage status', 6); sleep(300); game = answerOf('raven stage status') || 'the game took the request but wrote no answer (a build before 2026-11-10?)'; }
  catch (e) { game = 'the game does not answer the studio in 6 s: not open, not in a world, or another request waits'; try { fs.unlinkSync(path.join(studio, 'request.txt')); } catch (x) { /* quiet: the game took it meanwhile */ } }
  const playing = scenePlaying();
  const last = logLines().slice(-3);
  console.log('game:   ' + game + '\nscene:  ' + (playing ? playing + ' is playing — no runs till it ends' : 'none playing') + '\nstudio: ' + (last.length ? 'last jobs (another chat may be at it):\n  ' + last.join('\n  ') : 'no jobs logged'));
}

// Numbers turned by eye (the user 2026-11-11: «чому наосліп, ми що дарма правили всі ці інструменти» — five saved versions in a row of one clip, each changing
// the same hand's point on the thing and its fingers by hand, each followed by a take to see how it looks): the clip's saved versions (the stage keeps one
// a save, config/RavenWatch/player_anims_takes/<clip>/), the last ByEye changes in a row within ByEyeHours — when each of them changed, by hand, keys of
// the same part of the body, the take ends by saying so and naming what reckons that part; with no such tool: stop and tell the user which is missing.
// A channel the clip's movements make («# scripted:») is the script's, not a hand's: not counted.
const ByEye = 3, ByEyeHours = 3;
const PARTS_OF = [
  [/^@(Left|Right)Hand\.on|^(Left|Right)Hand(Thumb|Index|Middle|Ring|Pinky)[123]$/, m => 'the ' + (m[1] || m[2]).toLowerCase() + ' hand\'s hold (its point on the thing, its fingers)', c => 'node tools/anim.js grip ' + c + ' reckons the wrist\'s point and each finger\'s bend from the take\'s sheet (its fingers alone, on no thing: node tools/anim.js grip presets)'],
  [/^@(Left|Right)Hand/, m => 'the ' + m[1].toLowerCase() + ' arm (its hand\'s point, turn, elbow)', c => 'say it as a movement — node tools/anim.js do ' + c + ' "… reach …" — and the model of the arm (tools/body.js) chooses the way, the elbow and the turn; node tools/anim.js check ' + c + ' says the room to each limit before a take'],
  [/^@(Left|Right)Foot/, m => 'the ' + m[1].toLowerCase() + ' foot', c => 'say it as a movement — node tools/anim.js do ' + c + ' "… step …"'],
  [/^(Spine1|Spine2|move|body|Hips)$/, () => 'the trunk (lean, turn, weight, height)', c => 'say it as a movement — node tools/anim.js do ' + c + ' "… lean|turn|sink|weight …" —, or run the report\'s own «fix:» lines (node tools/anim.js fix ' + c + ')'],
  [/^@Look/, () => 'the look', c => 'say it as a movement — node tools/anim.js do ' + c + ' "… look …"'],
];
function byEye(clip, hours) {
  inStudio();
  const within = hours || ByEyeHours, name = clip.split('.')[0], dir = path.join(studio, '..', 'player_anims_takes', name);
  if (!fs.existsSync(dir)) return null;
  const files = fs.readdirSync(dir).filter(f => /^\d+\.txt$/.test(f)).sort((a, b) => parseInt(a) - parseInt(b)).slice(-(ByEye + 1)).filter(f => Date.now() - fs.statSync(path.join(dir, f)).mtimeMs < within * 3600000);
  if (files.length < ByEye + 1) return null;
  const clipTool = require('./clip.js'), motion = require('./motion.js'), parsed = files.map(f => clipTool.parse(fs.readFileSync(path.join(dir, f), 'utf8'), name));
  const partOf = ch => { for (const [re, word, tool] of PARTS_OF) { const m = re.exec(ch); if (m) return { word: word(m), tool }; } return null; };
  let common = null;
  for (let i = 1; i < parsed.length; i++) {
    const a = parsed[i - 1], b = parsed[i], made = new Set(motion.listOf(b, 'scripted')), changed = new Map();
    for (const ch of new Set([...a.order, ...b.order])) { if (made.has(ch) || JSON.stringify(a.keys[ch] || []) === JSON.stringify(b.keys[ch] || [])) continue; const p = partOf(ch); if (p) changed.set(p.word, p.tool); }
    common = common === null ? changed : new Map([...common].filter(([w]) => changed.has(w)));
    if (!common.size) return null;
  }
  return [...common].map(([word, tool]) => '! BY EYE: ' + word + ' was changed by hand in each of the last ' + ByEye + ' saved versions of ' + name + ' (' + files.slice(1).map(f => parseInt(f)).join(', ') + '). Numbers turned a take a guess are what the tools are there to end: ' + tool(name) + '. If that does not reckon what is wrong here — or these versions ARE that tool\'s own, written one after another and still wrong — STOP and tell the user what the tool does not see: do not take a fourth guess.').join('\n');
}

// a verb and its words (anim.js): throws on a mistake
function run(cmd, a) {
  if (cmd === 'scene' && a[0] === '--dry') { if (!a[1]) throw new Error('scene --dry <scene>'); console.log(require('./scene_dry.js').dry(a[1])); return; } // (the file alone: the time table and the warnings, no game, no studio)
  if (cmd === 'scene' && a[0] === '--sound') { if (!a[1]) throw new Error('scene --sound <scene>'); console.log(require('./scene_dry.js').sound(a[1])); return; } // (a first sound pass proposed: bed, mix, ear, what falls, mute clips; nothing written)
  if (cmd === 'scene' && a[0] === '--cover') { if (!a[1]) throw new Error('scene --cover <scene>'); console.log(require('./scene_dry.js').cover(a[1])); return; } // (camera lines proposed for its talk; nothing written)
  inStudio();
  if (cmd === 'status') { status(); return; }
  const playing = cmd === 'done' || cmd === 'report' || cmd === 'fix' ? null : scenePlaying(); // (no game needed for these)
  if (playing) throw new Error('a scene plays in the game: ' + playing + ' — wait for its end');
  if ((cmd === 'clip' || cmd === 'views') && a[0]) {
    if (cmd === 'views' && !a[1]) throw new Error('views <clip> <view,view…> (front,side,back,three,top,chest,chestside,chestleft,chesttop,contact,handl,handr,holdl,holdr,fingerl,fingerr,head,headside,headtop,feet)');
    const text = runClip(a[0], a[1]);
    ask('raven stage down');
    console.log(text.trimEnd());
    if (runClip.since) console.log(runClip.since);
    { const s = scriptOf(a[0]); if (s) {
      const at = []; for (const l of verdictLines(text)) { const m = /at ([0-9.]+)(?:–([0-9.]+))? s/.exec(l.text); if (!m) continue; const t0 = +m[1], t1 = m[2] ? +m[2] : t0, side = /\bLeft|\bleft/.test(l.text) ? 'left' : /\bRight|\bright/.test(l.text) ? 'right' : null; const on = s.lines.filter(x => x.t0 - 0.3 <= t1 && t0 <= x.t1 + 0.3 && (!side || !['left', 'right'].includes(x.side) || x.side === side)); if (on.length) at.push('  ' + l.key.slice(0, 46) + ' (' + m[1] + ' s) → ' + on.map(x => 'do ' + x.n + ': ' + x.line).join(' · ')); }
      console.log('this clip is written by movements (node tools/anim.js show ' + a[0] + '): mend the movement (redo <n> …), not its keys' + (at.length ? ' — what plays at each line\'s moment:\n' + at.join('\n') : ''));
    } }
    console.log('sheets:\n  ' + sheets('stage_' + a[0]).join('\n  '));
    { const eye = byEye(a[0]); if (eye) console.log(eye); }
  } else if (cmd === 'report' && a[0]) {
    const report = path.join(out, 'stage_' + a[0] + '.txt');
    if (!fs.existsSync(report)) throw new Error('no report of ' + a[0] + ': node tools/anim.js clip ' + a[0]);
    const text = fs.readFileSync(report, 'utf8');
    const why = stale(a[0], text);
    if (why) throw new Error('old report of ' + a[0] + ': ' + why + ' (node tools/anim.js clip ' + a[0] + ')');
    console.log(text.trimEnd());
    console.log('sheets:\n  ' + sheets('stage_' + a[0]).join('\n  '));
  } else if (cmd === 'fix' && a[0]) {
    const report = path.join(out, 'stage_' + a[0] + '.txt');
    if (!fs.existsSync(report)) throw new Error('no report of ' + a[0] + ': node tools/anim.js clip ' + a[0]);
    const text = fs.readFileSync(report, 'utf8'), why = stale(a[0], text);
    if (why) throw new Error('old report of ' + a[0] + ': ' + why + ' — its commands are of another file (node tools/anim.js clip ' + a[0] + ')');
    const all = fixesOf(text);
    if (!all.length) { console.log(a[0] + ': the last report has no «fix:» command (what is left is the pose\'s shape, the user\'s to judge, or a line that says why it has none)'); return; }
    all.forEach((c, i) => console.log((i + 1) + ': ' + c));
    if (a[1] === 'list') return;
    const takeIt = a.includes('--take'); a = a.filter(x => x !== '--take');
    const pick = a.slice(1).map(x => Math.round(parseFloat(x)) - 1);
    if (pick.some(i => !(i >= 0 && i < all.length))) throw new Error('fix <clip> [n …|list]: the report has ' + all.length + ' command(s)');
    const clipTool = require('./clip.js');
    const chosen = pick.length ? pick : all.map((_, k) => k), moves = i => ['stretch', 'shift'].includes(wordsOf(all[i])[0]), when = i => parseFloat(wordsOf(all[i])[2]) || 0;
    const order = chosen.filter(i => !moves(i)).concat(chosen.filter(moves).sort((x, y) => when(y) - when(x))); // (the commands were reckoned for the file as the take saw it: what moves time last, the latest first)
    let done = 0;
    for (const i of order) {
      const w = wordsOf(all[i]); console.log('— ' + (i + 1) + ': ' + all[i]);
      const s = takeIt ? null : scriptOf(a[0]), theirs = s ? [...new Set(keysOf(w).flatMap(s.family))].filter(ch => s.made.includes(ch)) : [];
      if (theirs.length) { // (the clip's movements make that channel: a key set by hand would take it from them for good)
        const all0 = [...new Set(theirs.flatMap(ch => makes(s, ch)))], when = ['set', 'del'].includes(w[0]) ? parseFloat(w[3]) : NaN, near = all0.filter(x => x.t0 - 0.3 <= when && when <= x.t1 + 0.3), by = near.length ? near : all0; // (the movement that plays at the key's moment, when the command has one)
        console.log('  NOT RUN: it keys ' + theirs.join(', ') + ' by hand, and the clip\'s movements make ' + (theirs.length > 1 ? 'them' : 'it') + (by.length ? ' (' + by.map(x => 'do ' + x.n + ': ' + x.line).join(' · ') + ')' : ' (the accompaniment: node tools/anim.js alive ' + a[0] + ' <0..2>)') + '. Say it in the movement: node tools/anim.js redo ' + a[0] + ' <n> <t0>-<t1> … — or take the channel from the script for good: node tools/anim.js fix ' + a[0] + ' ' + (i + 1) + ' --take');
        continue;
      }
      try { clipTool.run(w[0], w.slice(1)); } catch (e) { throw new Error('command ' + (i + 1) + ' failed: ' + e.message + '\n' + (done ? done + ' command(s) before it are applied, so the report is of an older file now — take it again: node tools/anim.js clip ' + a[0] : 'nothing was changed')); }
      done++;
    }
    console.log('done; the report is of the file before them: node tools/anim.js clip ' + a[0]);
  } else if (cmd === 'scene' && a[0]) {
    console.log(require('./scene_dry.js').dry(a[0]) + '\n'); // (before the run: what the file says it will be; the run's report then says what it was)
    const name = 'sc_' + a[0], report = path.join(out, name + '.txt');
    clear(name);
    const gone = dropStaleScenes(name); if (gone) console.log('older studio files of other scenes and shots taken off: ' + gone);
    ask('raven stage down'); // (the stage's mannequin stood in the scene's frames with its line over the picture, zz_heid_meet 2026-11-11)
    ask('raven story scene reload'); // (the files as they are now: 2026-11-06 a run measured the scene from before an edit; the clips with them — StoryFiles.ReloadAll: 2026-11-11 a run played a clip's keys from before an edit)
    console.log('scene file: ' + sceneFile(a[0]) + ' — read again just now, the clips too');
    ask('scene ' + name + ' ' + a[0] + ' ' + (a[1] || 2) + ' ' + (a[2] || 0));
    const text = until(() => fs.existsSync(report) ? fs.readFileSync(report, 'utf8') : null, 420, 'report of ' + a[0]);
    console.log(text);
    const listed = (/^files: (.*)$/m.exec(text) || [])[1]; // (the run's own files, by its report: never an older sheet with a like name)
    console.log('sheets:\n  ' + (listed ? listed.split(' ').filter(Boolean).map(f => path.join(out, f).replace(/\\/g, '/')) : sheets(name)).join('\n  '));
    const wav = path.join(out, name + '.wav'); // (what was heard in the run: AudioTap — its levels and the core's words stand in the report; the sound itself is measured further by node tools/sound.js info)
    if (fs.existsSync(wav)) console.log('sound of the run:\n  ' + wav.replace(/\\/g, '/'));
  } else if (cmd === 'done' && a[0]) {
    clear('stage_' + a[0]);
    console.log(a[0] + ': its frames and report taken off');
  } else if (cmd === 'approved') {
    const save = a.includes('--save'), asked = a.filter(x => x !== '--save'), all = approvedNames();
    const unknown = asked.filter(x => !all.includes(x));
    if (unknown.length) throw new Error('not approved clips (docs/CLIPS.md): ' + unknown.join(', '));
    const names = asked.length ? asked : all;
    const old = fs.existsSync(numbersFile) ? JSON.parse(fs.readFileSync(numbersFile, 'utf8')) : {}, now = {};
    const review = ['# node tools/anim.js approved, ' + new Date().toLocaleString('uk-UA') + ': ' + names.length + ' approved clip(s), each taken twice. For the user to judge over the key sheet: the clip wrong, or the rule.', ''];
    let moved = 0, clean = 0, shaky = 0;
    for (const clip of names) {
      const one = measuredOf(clip, runClip(clip, null, true)), text = runClip(clip, null, true), two = measuredOf(clip, text);
      const keys = [...new Set([...one, ...two].map(x => x.key))];
      if (!keys.length) { clean++; review.push(clip + ': OK in both takes'); console.log(clip + ': OK in both takes'); }
      else {
        review.push(clip + ':  sheet ' + path.join(out, 'stage_' + clip + '_key.png').replace(/\\/g, '/'));
        for (const k of keys) {
          const a1 = one.find(x => x.key === k), a2 = two.find(x => x.key === k), both = a1 && a2;
          if (!both) shaky++;
          review.push('  ' + (both ? 'both takes   ' : 'ONLY take ' + (a1 ? 1 : 2) + '  ') + (a2 || a1).text);
        }
        console.log(clip + ': ' + keys.length + ' line(s)' + (keys.some(k => !(one.find(x => x.key === k) && two.find(x => x.key === k))) ? ', some only in one take' : ''));
      }
      const verdict = verdictOf(text), pose = (/^pose: (.*)$/m.exec(text) || [])[1] || '';
      now[clip] = { verdict, pose };
      const was = old[clip];
      if (was && (was.verdict !== verdict || was.pose !== pose)) { moved++; review.push('  numbers MOVED since tools/clip_numbers.json: was ' + was.pose + ' | now ' + pose); }
    }
    ask('raven stage down');
    if (save) { fs.writeFileSync(numbersFile, JSON.stringify(now, null, 1) + '\n'); console.log('kept in tools/clip_numbers.json'); }
    review.push('', names.length + ' clip(s): ' + clean + ' clean in both takes; ' + shaky + ' line(s) only in one take (that rule is not steady on that clip); ' + moved + ' moved against tools/clip_numbers.json');
    fs.writeFileSync(path.join(out, 'approved_review.txt'), review.join('\n') + '\n');
    console.log('\n' + review.join('\n') + '\n\nwritten: ' + path.join(out, 'approved_review.txt').replace(/\\/g, '/'));
  } else throw new Error('node tools/anim.js clip <clip> [views] | views <clip> <views> | report <clip> | fix <clip> [n …|list] | done <clip> | scene [--dry] <scene> [fps] [from] | approved [clip …] [--save] | status');
}

module.exports = { run, studioDir, since, fixesOf, wordsOf, verdictLines, byEye, sceneTake };
if (require.main === module) { console.error('take.js is the game half of the one tool: node tools/anim.js ' + (process.argv[2] || '<verb>') + ' …'); process.exit(1); }

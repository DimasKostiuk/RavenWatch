// One run from outside the game, the game open and in a world (studio/request.txt, studio/out). The game half of tools/anim.js (the one
// entry, review 2 2026-11-10): anim.js <verb> …; the studio's folder is tools/studio.path (one line). What to read comes back at once.
//   clip <clip> [views]        the clip on the stage, on the body and with the thing its row in docs/CLIPS.md names: measured and filmed in one run;
//                              prints the short report (VERDICT with a «fix:» command where the core could reckon one, what was asked, the tempo, each
//                              key frame's pose in words) and the sheets: stage_<clip>_key.png (a row a moment of the VERDICT, the wants, the marks;
//                              its two views; labelled), stage_<clip>_diff.png (this take | the take before, or what «ab» plays) — and only with
//                              views asked, stage_<clip>_<view>.png of the whole run (side,chest,… : AutoViews names what the clip would take)
//   views <clip> <views>       the same as clip <clip> <views>
//   report <clip>              the last report of the clip again — only if it is of the file as it is now (the file's hash in its head, AnimStage.Hash);
//                              of another version it refuses: «take it again»
//   scene --dry <scene>        the scene run dry, on node, no game (tools/scene_dry.js): a row a step — when it starts, how long it takes, who is in
//                              the frame — and the warnings the file alone shows (words over a bird in flight, a person with no «test: npc … at», a
//                              shot held long, a run of waits, a mark the clip has not got)
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
// It refuses while a scene plays in the game (a run cut off the blue_fire the user was watching, 2026-11-04). Whether the user is at the keys, or
// another chat uses the studio, it cannot see: «status» shows the last jobs, ask first. A clip's run takes its mannequin and its timer off the screen
// when it is done.
// After itself (the user 2026-11-07, 80 files left behind): a clip's run first takes off every other clip's stage files older than StaleMin
// minutes (a take still being looked at by another chat is fresher); «done» takes off the clip's own. Scenes' files are never touched here.
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
const clear = prefix => { for (const f of fs.readdirSync(out)) if ((f === prefix + '.txt') || (f === prefix + '.full.txt') || (f.startsWith(prefix + '_') && (f.endsWith('.png') || f.endsWith('.txt')))) fs.unlinkSync(path.join(out, f)); };
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
const dropStale = keep => { let n = 0; for (const f of fs.readdirSync(out)) { if (!f.startsWith('stage_') || f.startsWith('stage_' + keep + '.') || f.startsWith('stage_' + keep + '_')) continue; const p = path.join(out, f); if (Date.now() - fs.statSync(p).mtimeMs > StaleMin * 60000) { fs.unlinkSync(p); n++; } } return n; };
const rowOf = clip => fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').find(l => l.startsWith('| `' + clip.split('.')[0] + '` |')) || ''; // (a part: its whole clip's row — the body, the thing)
// (the state cell, the row's last: «бачив користувач» there and no «не затверджено» — shield_kneel's «бачив користувач; … новіша версія … не затверджено» was counted, 2026-11-10)
const approvedNames = () => fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').filter(l => { if (!l.startsWith('| `')) return false; const c = l.split('|').map(x => x.trim()).filter(Boolean); const state = c[c.length - 1] || ''; return state.includes('бачив користувач') && !state.includes('не затверджено'); }).map(l => /^\| `([\w-]+)`/.exec(l)).filter(Boolean).map(m => m[1]);
const verdictOf = text => text.slice(text.indexOf('VERDICT'), text.indexOf('pose: ')).trim();

function runClip(clip, views, keepOthers) {
  const report = path.join(out, 'stage_' + clip + '.txt');
  clear('stage_' + clip);
  const stale0 = keepOthers ? 0 : dropStale(clip); // (approved: the sheets of the clips before stay, the user looks at them after)
  if (stale0 > 0) console.log('older takes of other clips taken off: ' + stale0 + ' file(s)');
  const row = rowOf(clip), cells = row.split('|').map(c => c.trim());
  ask('raven stage down'); // (a clean stage each run: a speed, a game emote, a walk, fixtures, a second mannequin left by the last one changed the measure — review 2026-11-07)
  ask('raven stage up ' + (cells[3] === 'Гейд' ? 'heid' : 'dummy')); // (the body it was made on: a clip on another is another clip)
  const thing = /реквізит `(\w+)`( на руці)?/.exec(row); // («на руці»: on its right forearm, as a person carries its own — Heid's basket)
  const arm = /зброя `(\w+)`(?: і `(\w+)`)?/.exec(row); // (a pose for a weapon in hand: the game's own stance for it under the clip, as on a guard)
  ask('raven stage wield ' + (arm ? arm[1] + (arm[2] ? ' ' + arm[2] : '') : 'none'));
  ask('raven stage play ' + clip);
  sleep(800);
  ask('raven stage prop ' + (thing ? thing[1] + (thing[2] ? ' arm' : '') : 'none')); // (its .on channels do nothing with no thing)
  sleep(800);
  ask('raven stage check' + (views ? ' ' + views : ''));
  const text = until(() => { if (!fs.existsSync(report)) return null; const t = fs.readFileSync(report, 'utf8'); return t.includes('key sheet: coming') ? null : t; }, 180, 'report of ' + clip);
  sleep(500);
  const why = stale(clip, text);
  if (why) console.log('WARNING: the file changed during the take — ' + why);
  return text;
}

// a take's VERDICT lines, each by what it is about — its tag and the part it names («[jerk] RightArm», «[miss] @RightHand», «[palm] right») —
// so two takes of one clip can be laid side by side: the same mistake twice, or a rule that answers once yes, once no
const verdictLines = text => {
  const v = text.slice(text.indexOf('VERDICT'), text.search(/^asked|^pose: /m) > 0 ? text.search(/^asked|^pose: /m) : undefined);
  const lines = [];
  for (const l of v.split('\n')) {
    const m = /^  - (\[[\w-]+\])\s+(\S+)?(.*)$/.exec(l);
    const whole = m ? m[1] + ' ' + (m[2] || '') + m[3] : '';
    if (m) lines.push({ key: m[1] === '[want]' ? whole.split(' (')[0] : m[1] + ' ' + (m[2] || ''), text: whole.slice(0, 220) }); // (a want: its own words tell it from the others)
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

// a verb and its words (anim.js): throws on a mistake
function run(cmd, a) {
  if (cmd === 'scene' && a[0] === '--dry') { if (!a[1]) throw new Error('scene --dry <scene>'); console.log(require('./scene_dry.js').dry(a[1])); return; } // (the file alone: the time table and the warnings, no game, no studio)
  inStudio();
  if (cmd === 'status') { status(); return; }
  const playing = cmd === 'done' || cmd === 'report' ? null : scenePlaying(); // (no game needed for these)
  if (playing) throw new Error('a scene plays in the game: ' + playing + ' — wait for its end');
  if ((cmd === 'clip' || cmd === 'views') && a[0]) {
    if (cmd === 'views' && !a[1]) throw new Error('views <clip> <view,view…> (front,side,back,three,top,chest,chestside,chestleft,chesttop,contact,handl,handr,holdl,holdr,fingerl,fingerr,head,headside,headtop,feet)');
    const text = runClip(a[0], a[1]);
    ask('raven stage down');
    console.log(text.trimEnd());
    console.log('sheets:\n  ' + sheets('stage_' + a[0]).join('\n  '));
  } else if (cmd === 'report' && a[0]) {
    const report = path.join(out, 'stage_' + a[0] + '.txt');
    if (!fs.existsSync(report)) throw new Error('no report of ' + a[0] + ': node tools/anim.js clip ' + a[0]);
    const text = fs.readFileSync(report, 'utf8');
    const why = stale(a[0], text);
    if (why) throw new Error('old report of ' + a[0] + ': ' + why + ' (node tools/anim.js clip ' + a[0] + ')');
    console.log(text.trimEnd());
    console.log('sheets:\n  ' + sheets('stage_' + a[0]).join('\n  '));
  } else if (cmd === 'scene' && a[0]) {
    console.log(require('./scene_dry.js').dry(a[0]) + '\n'); // (before the run: what the file says it will be; the run's report then says what it was)
    const name = 'sc_' + a[0], report = path.join(out, name + '.txt');
    clear(name);
    ask('raven story scene reload'); // (the files as they are now: 2026-11-06 a run measured the scene from before an edit)
    console.log('scene file: ' + sceneFile(a[0]) + ' — read again just now');
    ask('scene ' + name + ' ' + a[0] + ' ' + (a[1] || 2) + ' ' + (a[2] || 0));
    const text = until(() => fs.existsSync(report) ? fs.readFileSync(report, 'utf8') : null, 420, 'report of ' + a[0]);
    console.log(text);
    const listed = (/^files: (.*)$/m.exec(text) || [])[1]; // (the run's own files, by its report: never an older sheet with a like name)
    console.log('sheets:\n  ' + (listed ? listed.split(' ').filter(Boolean).map(f => path.join(out, f).replace(/\\/g, '/')) : sheets(name)).join('\n  '));
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
      const one = verdictLines(runClip(clip, null, true)), text = runClip(clip, null, true), two = verdictLines(text);
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
  } else throw new Error('node tools/anim.js clip <clip> [views] | views <clip> <views> | report <clip> | done <clip> | scene [--dry] <scene> [fps] [from] | approved [clip …] [--save] | status');
}

module.exports = { run, studioDir };
if (require.main === module) { console.error('take.js is the game half of the one tool: node tools/anim.js ' + (process.argv[2] || '<verb>') + ' …'); process.exit(1); }

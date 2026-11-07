// One run from outside the game, the game open and in a world (studio/request.txt, studio/out). What to read comes back at once.
//   node tools/take.js clip <clip> [views]        the clip on the stage, on the body and with the thing its row in docs/CLIPS.md names: measured and
//                                                 filmed in one run; prints VERDICT, what was asked, the tempo and each key frame's pose in words, and the sheets
//   node tools/take.js scene <scene> [fps] [from]  a scene at its place: prints its VERDICT, each body's numbers, the sheets
//   node tools/take.js done <clip>                the user approved it: its frames and report go (each take is ~5 MB)
//   node tools/take.js approved [--save]          every clip the user has seen and accepted (docs/CLIPS.md «бачив користувач»), one after the
//                                                 other: their numbers against tools/clip_numbers.json; --save writes them. After a change in the
//                                                 core: what moved in what was already approved
// It refuses while a scene plays in the game (a run cut off the blue_fire the user was watching, 2026-11-04). Whether the user is at the keys, or
// another chat uses the studio, it cannot see: ask first. A clip's run takes its mannequin and its timer off the screen when it is done.
// After itself (the user 2026-11-07, 80 files left behind): a clip's run first takes off every other clip's stage files older than StaleMin
// minutes (a take still being looked at by another chat is fresher); «done» takes off the clip's own. Scenes' files are never touched here.
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const studio = 'D:/Thunderstore/Valheim/profiles/RavenTest/BepInEx/config/RavenWatch/studio', out = path.join(studio, 'out');
const numbersFile = path.join(root, 'tools', 'clip_numbers.json');
const sleep = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const until = (test, seconds, what) => { const end = Date.now() + seconds * 1000; while (Date.now() < end) { const r = test(); if (r) return r; sleep(400); } throw new Error('no ' + what + ' in ' + seconds + ' s: is the game open and in a world?'); };
const ask = line => { fs.writeFileSync(path.join(studio, 'request.txt'), line + '\n'); until(() => !fs.existsSync(path.join(studio, 'request.txt')), 20, 'answer to «' + line + '»'); };
const sheets = prefix => fs.readdirSync(out).filter(f => f.startsWith(prefix) && f.endsWith('.png')).map(f => path.join(out, f).replace(/\\/g, '/'));
const clear = prefix => { for (const f of fs.readdirSync(out)) if ((f === prefix + '.txt') || (f.startsWith(prefix + '_') && (f.endsWith('.png') || f.endsWith('.txt')))) fs.unlinkSync(path.join(out, f)); };

// the last scene the log tells of has not ended: its name
const scenePlaying = () => {
  const text = fs.readFileSync(path.join(studio, '..', '..', '..', 'LogOutput.log'), 'utf8');
  const all = text.match(/Story: track: сцена «[^»]+» (почалась|скінчилась|перервана)/g) || [];
  const last = all[all.length - 1] || '';
  return last.endsWith('почалась') ? last.slice(last.indexOf('«')) : null;
};
// the scene's own file and when it was saved (what the game has just read: take.js reloads before a run)
const sceneFile = id => { const d = path.join(root, 'assets', 'story', 'scenes'); for (const n of fs.readdirSync(d)) { const p = path.join(d, n); if (fs.readFileSync(p, 'utf8').includes('[scene ' + id + ']')) return n + ', saved ' + fs.statSync(p).mtime.toLocaleString('uk-UA'); } return '(no file names it)'; };
const StaleMin = 30;
const dropStale = keep => { let n = 0; for (const f of fs.readdirSync(out)) { if (!f.startsWith('stage_') || f.startsWith('stage_' + keep + '.') || f.startsWith('stage_' + keep + '_')) continue; const p = path.join(out, f); if (Date.now() - fs.statSync(p).mtimeMs > StaleMin * 60000) { fs.unlinkSync(p); n++; } } return n; };
const rowOf = clip => fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').find(l => l.startsWith('| `' + clip + '` |')) || '';

function runClip(clip, views) {
  const report = path.join(out, 'stage_' + clip + '.txt');
  clear('stage_' + clip);
  const stale = dropStale(clip);
  if (stale > 0) console.log('older takes of other clips taken off: ' + stale + ' file(s)');
  const row = rowOf(clip), cells = row.split('|').map(c => c.trim());
  ask('raven stage down'); // (a clean stage each run: a speed, a game emote, a walk, fixtures, a second mannequin left by the last one changed the measure — review 2026-11-07)
  ask('raven stage up ' + (cells[3] === 'Гейд' ? 'heid' : 'dummy')); // (the body it was made on: a clip on another is another clip)
  const thing = /реквізит `(\w+)`/.exec(row);
  ask('raven stage play ' + clip);
  sleep(800);
  ask('raven stage prop ' + (thing ? thing[1] : 'none')); // (its .on channels do nothing with no thing)
  sleep(800);
  ask('raven stage check' + (views ? ' ' + views : ''));
  const text = until(() => { if (!fs.existsSync(report)) return null; const t = fs.readFileSync(report, 'utf8'); return t.includes('frames of the mistakes: coming') ? null : t; }, 180, 'report of ' + clip);
  sleep(500);
  return text;
}

const [cmd, ...a] = process.argv.slice(2);
try {
  const playing = cmd === 'done' ? null : scenePlaying();
  if (playing) throw new Error('a scene plays in the game: ' + playing + ' — wait for its end');
  if (cmd === 'clip' && a[0]) {
    const text = runClip(a[0], a[1]);
    ask('raven stage down');
    console.log(text.slice(0, text.indexOf('fastest:') >= 0 ? text.indexOf('fastest:') : text.length).trimEnd());
    console.log('sheets:\n  ' + sheets('stage_' + a[0]).join('\n  '));
  } else if (cmd === 'scene' && a[0]) {
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
    const rows = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').filter(l => l.startsWith('| `') && l.includes('бачив користувач'));
    const names = rows.map(l => /^\| `([\w-]+)`/.exec(l)).filter(Boolean).map(m => m[1]);
    const old = fs.existsSync(numbersFile) ? JSON.parse(fs.readFileSync(numbersFile, 'utf8')) : {}, now = {};
    let moved = 0;
    for (const clip of names) {
      const text = runClip(clip);
      const verdict = text.slice(text.indexOf('VERDICT'), text.indexOf('pose: ')).trim();
      const pose = (/^pose: (.*)$/m.exec(text) || [])[1] || '';
      now[clip] = { verdict, pose };
      const was = old[clip];
      if (!was) console.log(clip + ': no numbers kept yet\n  ' + verdict.replace(/\n/g, '\n  '));
      else if (was.verdict === verdict && was.pose === pose) console.log(clip + ': the same');
      else { moved++; console.log(clip + ': MOVED\n  was ' + was.verdict.replace(/\n/g, '\n  ') + '\n  now ' + verdict.replace(/\n/g, '\n  ') + '\n  was ' + was.pose + '\n  now ' + pose); }
    }
    if (a[0] === '--save') { fs.writeFileSync(numbersFile, JSON.stringify(now, null, 1) + '\n'); console.log('kept in tools/clip_numbers.json'); }
    ask('raven stage down');
    console.log(names.length + ' approved clip(s), ' + moved + ' moved');
  } else throw new Error('node tools/take.js clip <clip> [views] | done <clip> | scene <scene> [fps] [from] | approved [--save]');
} catch (e) { console.error('take: ' + e.message); process.exit(1); }

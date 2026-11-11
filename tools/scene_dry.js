// A scene run dry, on node, before the game (review 2 2026-11-10: the mistakes of the past weeks were mostly of time — 23 s of nothing, a bird
// flying 50 m in the frame, a subtitle over a bird in flight — and each was seen only after a run of up to seven minutes in the game).
// From the file alone: a table, a row a step — when it starts, how long it takes (the game's own numbers: RavenSpeech.Seconds for a line read,
// a clip's length from its file, a bird's cruise, a person's walk and run, the step limits), who is in the frame — and the warnings:
//   a word the game does not know (the same tables as storycheck, run here on this scene alone);
//   a person the scene takes from the world with no «test: npc <who> at …» (the check waits CastWait s for one that is far);
//   a subtitle or a line while the bird is in flight (unreadable over it);
//   one shot held longer than StepLong s; a run of nothing but waits and words longer than that;
//   a bird coming from farther than BirdFar m (that long in the frame); a walk with no end (its limit);
//   a mark waited for that the clip on that body has not got, or has passed.
// What it cannot know: how far a walk is (the places' positions are the world's) — a walk is «~» and its limit; whether an «if» holds (counted in).
//   node tools/scene_dry.js <scene>          or  node tools/anim.js scene --dry <scene>
const fs = require('fs'), path = require('path'), cp = require('child_process');
const root = path.join(__dirname, '..');
const scenesDir = path.join(root, 'assets', 'story', 'scenes'), clipsDir = path.join(root, 'assets', 'story', 'clips');
// the game's numbers (where they live in the code)
const StepLong = 8, StepMax = 30, CastWait = 180, BirdFar = 40, Cruise = 9, LandSec = 3, WalkSpeed = 1.3, RunSpeed = 3.5, BackSlowest = 0.35, FadePerson = 3; // StudioScene.StepLong · StoryScenes.StepMax · CastWait · (a bird far off) · RavenFlight Hugin.Cruise · its landing · StoryScenesCast (npc go) · StoryWalk.BackSlowest · Fades.Person
const readSec = text => Math.min(8, Math.max(2, 1.2 + text.length * 0.06)) * 1.1; // RavenSpeech.Seconds, Pace.Scene, ReadScale 1.1
const f = v => (Math.round(v * 10) / 10).toString();

// a clip's file: its length, parts, marks, loop
const clips = {};
function clipOf(name) {
  const [whole, part] = name.split('.');
  if (!(whole in clips)) {
    const p = path.join(clipsDir, whole + '.txt');
    if (!fs.existsSync(p)) clips[whole] = null;
    else {
      const c = { length: 0, loop: false, parts: {}, marks: [] };
      for (const raw of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
        const w = raw.trim().split(/\s+/);
        if (w[0] === 'length') c.length = +w[1];
        else if (w[0] === 'loop') c.loop = w[1] === '1';
        else if (w[0] === 'part') c.parts[w[1]] = { from: +w[2], to: +w[3], how: w[4] || 'hold' };
        else if (w[0] === 'mark') c.marks.push({ t: +w[1], name: w[2] });
      }
      clips[whole] = c;
    }
  }
  const c = clips[whole];
  if (!c) return null;
  if (!part) return { length: c.length, loop: c.loop, marks: c.marks, from: 0, known: true };
  const pt = c.parts[part];
  if (!pt) return null;
  return { length: pt.to - pt.from, loop: pt.how === 'loop', marks: c.marks.filter(m => m.t >= pt.from && m.t <= pt.to).map(m => ({ t: m.t - pt.from, name: m.name })), from: pt.from, known: true };
}

function sceneLines(id) {
  for (const n of fs.readdirSync(scenesDir).filter(x => x.endsWith('.txt'))) {
    const all = fs.readFileSync(path.join(scenesDir, n), 'utf8').split(/\r?\n/);
    const start = all.findIndex(l => new RegExp('^\\[scene\\s+' + id + '\\]').test(l.trim()));
    if (start < 0) continue;
    const out = [];
    for (let i = start + 1; i < all.length; i++) {
      let l = all[i];
      if (/^\s*\[scene\s/.test(l)) break;
      l = l.replace(/\s+#.*$/, '').trim(); // (a note after the line: StoryFiles cuts it)
      if (!l || l.startsWith('#')) continue;
      out.push({ num: i + 1, line: l });
    }
    return { file: n, lines: out };
  }
  return null;
}

function dry(id) {
  const sc = sceneLines(id);
  if (!sc) return 'scene_dry: no scene «' + id + '» in assets/story/scenes';
  // what storycheck says of this scene (the words, the places, the people: its own tables)
  const sr = cp.spawnSync('node', [path.join(root, 'tools', 'storycheck.js')], { encoding: 'utf8' });
  const said = ((sr.stdout || '') + (sr.stderr || '')).split('\n').filter(l => l.includes('[' + id + ']') || (l.startsWith('STORY ') && l.includes(sc.file)));
  const head = {}, steps = [];
  for (const { num, line } of sc.lines) {
    const m = /^(roams|alone|needs|check|test|place|guard):\s*(.*)$/.exec(line);
    if (m) { head[m[1]] = head[m[1]] ? head[m[1]] + '; ' + m[2] : m[2]; continue; }
    let cond = null, text = line;
    if (line.startsWith('if ')) { const c = line.indexOf(':'); cond = line.slice(3, c).trim(); text = line.slice(c + 1).trim(); }
    steps.push({ num, line, cond, w: text.split(/\s+/), text });
  }
  const warn = [];
  const put = new Set((head.test || '').split(';').map(s => s.trim()).filter(Boolean).filter(a => /^npc \S+ at /.test(a)).map(a => a.split(/\s+/)[1]));
  if (!head.check && !head.test) warn.push('no «check:» and no «test:» line: the check starts it in the middle of its place, in whatever world the game is in');
  // the walk through the steps
  let t = 0, cam = false, camSince = 0, camStep = null, bird = null, birdFlying = false, nothing = 0, nothingFrom = null, nothingSteps = 0;
  const cast = new Set(), rows = [], bodyClip = {}; // (who: {clip, at} — the clip started on him and when)
  const words = s => s.w.slice(s.w[0] === 'npc' ? 3 : 2).join(' ');
  for (const s of steps) {
    const w = s.w, kind = w[0];
    let took = 0, note = '', shown = null;
    const sub = text => { took = readSec(text); if (bird && birdFlying) { note += 'words over the bird in flight: unreadable; '; warn.push('line ' + s.num + ': words while the bird is in flight («' + text.slice(0, 30) + '»): nobody reads them over it — land it first, or say them before «bird come … nowait»'); } };
    const walk = (who, limitSec) => { took = 0; shown = '~walk'; note += who + ' walks: how far the world says (limit ' + f(limitSec) + ' s); '; };
    const markOn = (who, mark, limit) => {
      const on = bodyClip[who];
      if (!on) { took = limit; note += 'no clip of ours started on ' + who + ' before this: waits the whole ' + f(limit) + ' s; '; return; }
      const c = clipOf(on.clip);
      const m = c && c.marks.find(x => x.name === mark);
      if (!m) { took = limit; note += 'no «mark ' + mark + '» in ' + on.clip + ': waits the whole ' + f(limit) + ' s; '; return; }
      const left = m.t - (t - on.at);
      if (left < 0) { took = 0; note += 'mark ' + mark + ' of ' + on.clip + ' passed ' + f(-left) + ' s ago: no wait (the sound comes late); '; }
      else took = Math.min(left, limit);
    };
    switch (kind) {
      case 'wait': took = +w[1] || 1; break;
      case 'until': { const lim = +(s.text.slice(s.text.lastIndexOf('|') + 1).trim()) || 10; took = lim; shown = '≤' + f(lim); if (lim > StepLong) note += 'waits up to ' + f(lim) + ' s for its condition; '; break; }
      case 'sub': sub(w.slice(1).join(' ')); break;
      case 'say': sub(w.slice(2).join(' ')); note += '(only with ' + w[1] + ' on the shoulder) '; break;
      case 'msg': took = 0; break;
      case 'fade': took = w[2] ? +w[2] : 1.5; break;
      case 'camera':
        if (w[1] === 'off') { cam = false; camStep = null; }
        else { if (!cam) camSince = t; cam = true; camStep = s; }
        if (w[1] === 'move' && w.includes('wait')) took = +w[4] || 0;
        if (w[1] === 'orbit' && w.includes('wait')) took = +w[6] || 0;
        break;
      case 'player':
        if (w[1] === 'clip') {
          if (w[2] !== 'none') {
            const c = clipOf(w[2]);
            if (!c) { note += 'clip ' + w[2] + ' not a file of assets/story/clips (built in, or a part that is not there): its length unknown; '; shown = '?'; }
            else { bodyClip.player = { clip: w[2], at: t }; if (c.loop) note += 'loops: on him till the next clip; '; else if (!w.includes('nowait')) took = c.length; }
          }
        }
        else if (w[1] === 'mark') markOn('player', w[2], +w[3] || 30);
        else if (w[1] === 'go') { if (!w.includes('nowait')) walk('the player', 7); }
        else if (w[1] === 'back') { took = (+w[2] || 1) / BackSlowest; shown = '≤' + f(took); }
        else if (w[1] === 'stop') took = +w[2] || 0.8;
        else if (w[1] === 'arrive') walk('the player (arrive)', 7);
        break;
      case 'bird':
        if (w[1] === 'come') {
          bird = 'bird';
          const d = +w[2] || 60, h = +w[3] || 18, l = w[4] === 'land' ? (+w[5] || 5) : 0;
          const flight = Math.hypot(d, h) / Cruise + LandSec;
          if (w.includes('nowait')) { birdFlying = true; took = 0; note += 'on its way ~' + f(flight) + ' s; '; }
          else { took = flight; birdFlying = false; shown = '~' + f(flight); }
          if (d > BirdFar) note += 'from ' + d + ' m: ~' + f(Math.hypot(d, h) / Cruise) + ' s of flying in the frame; ';
        }
        else if (w[1] === 'circle') { took = +w[2] || 30; birdFlying = true; }
        else if (w[1] === 'over') { took = StepMax; shown = '≤' + StepMax; }
        else if (w[1] === 'land') { birdFlying = false; took = LandSec; shown = '~' + LandSec; }
        else if (w[1] === 'shoulder') { birdFlying = false; took = LandSec; shown = '~' + LandSec; }
        else if (w[1] === 'leave' || w[1] === 'gone') { bird = null; birdFlying = false; }
        else if (w[1] === 'say') sub(w.slice(2).join(' '));
        break;
      case 'npc': {
        const who = w[1], verb = w[2];
        if (verb !== 'spawn' && verb !== 'free' && !cast.has(who)) {
          cast.add(who);
          if (!put.has(who) && !steps.some(x => x.w[0] === 'npc' && x.w[1] === who && x.w[2] === 'spawn')) warn.push('«' + who + '» is taken from the world and nothing says where: the check waits up to ' + CastWait + ' s for one that is far — add «test: npc ' + who + ' at <place>»');
        }
        if (verb === 'go') walk(who, 8 + (w.includes('run') ? 10 : 20));
        else if (verb === 'say') sub(w.slice(3).join(' '));
        else if (verb === 'fade') { took = w[3] ? +w[3] : FadePerson; cast.delete(who); }
        else if (verb === 'free') cast.delete(who);
        else if (verb === 'mark') markOn(who, w[3], +w[4] || 30);
        else if (verb === 'clip' && w[3] !== 'none') { const c = clipOf(w[3]); if (c) bodyClip[who] = { clip: w[3], at: t }; }
        break;
      }
      case 'bars': case 'weather': case 'thor': case 'shake': case 'bolt': case 'do': case 'drop': case 'door': case 'prop': case 'watchers':
      case 'music': case 'bed': case 'mix': case 'mute': case 'unmute': case 'ear': case 'sound': case 'raven': case 'own':
        break;
      default: note += 'a word the game may not know (storycheck says); ';
    }
    // nothing on screen: waits and words only, one after the other
    const idle = kind === 'wait' || kind === 'sub' || kind === 'say' || kind === 'msg' || (kind === 'bird' && w[1] === 'say') || (kind === 'npc' && w[2] === 'say') || kind === 'sound';
    if (idle) { if (nothingFrom === null) nothingFrom = s; nothing += took; nothingSteps++; }
    else { if (nothing > StepLong && nothingSteps > 1) warn.push('from step ' + nothingFrom.num + ' («' + nothingFrom.line.slice(0, 40) + '»): ' + f(nothing) + ' s of nothing but waits and words — is anything happening on screen?'); nothing = 0; nothingFrom = null; nothingSteps = 0; }
    if (cam && took > StepLong) note += 'holds the shot ' + f(took) + ' s (StepLong ' + StepLong + '); ';
    const inFrame = [cam ? 'cam' : 'free', bird ? (birdFlying ? 'bird↑' : 'bird') : null, ...cast].filter(Boolean).join(' ');
    rows.push({ num: s.num, at: t, took, shown: shown || f(took), cond: s.cond, text: s.text, who: inFrame, note: note.trim() });
    t += took;
  }
  if (nothing > StepLong && nothingSteps > 1) warn.push('from step ' + nothingFrom.num + ': ' + f(nothing) + ' s of nothing but waits and words at the end');
  if (cam) warn.push('the camera is still on at the end (camera off comes with the end of the scene: fine if meant)');
  // the table
  const out = [];
  out.push('scene ' + id + ' (' + sc.file + '), dry: ~' + f(t) + ' s in all, ' + steps.length + ' step(s)' + (head.place ? ', place ' + head.place : '') + (head.needs ? ', needs ' + head.needs : '') + (head.test ? '\n  test: ' + head.test : '') + (head.check ? '\n  check: ' + head.check : ''));
  out.push('line | at s | takes | in frame | step | note');
  for (const r of rows) out.push(String(r.num).padStart(4) + ' | ' + f(r.at).padStart(5) + ' | ' + r.shown.padStart(6) + ' | ' + r.who.padEnd(14) + ' | ' + (r.cond ? 'if ' + r.cond + ': ' : '') + r.text + (r.note ? '  — ' + r.note : ''));
  if (warn.length) { out.push('warnings:'); for (const x of warn) out.push('  - ' + x); }
  if (said.length) { out.push('storycheck on it:'); for (const x of said) out.push('  - ' + x); }
  if (!warn.length && !said.length) out.push('warnings: none from the file (the run still judges the poses, the walks, the frame)');
  return out.join('\n');
}

module.exports = { dry };
if (require.main === module) {
  const id = process.argv[2];
  if (!id) { console.error('node tools/scene_dry.js <scene>'); process.exit(1); }
  console.log(dry(id));
}

// A scene run dry, on node, before the game (review 2 2026-11-10: the mistakes of the past weeks were mostly of time — 23 s of nothing, a bird
// flying 50 m in the frame, a subtitle over a bird in flight — and each was seen only after a run of up to seven minutes in the game).
// From the file alone: a table, a row a step — when it starts, how long it takes (the game's own numbers: RavenSpeech.Seconds for a line read,
// a clip's length from its file, a bird's cruise, a person's walk and run, the step limits), who is in the frame — and the warnings:
//   a word the game does not know (the same tables as storycheck, run here on this scene alone);
//   a person the scene takes from the world with no «test: npc <who> at …» (the check waits CastWait s for one that is far);
//   a subtitle or a line while the bird is in flight (unreadable over it);
//   one shot held longer than StepLong s; a run of nothing but waits and words longer than that;
//   a bird coming from farther than BirdFar m (that long in the frame); a walk with no end (its limit);
//   a mark waited for that the clip on that body has not got, or has passed;
//   the shots (review 3 2026-11-11: a scene is filmed by words now — camera on / shoulder / two — and the mistakes of filming are of time and order
//   too): a shot cut away sooner than ShotShort s; a cut between two like shots of one subject (it jumps); TalkLines lines or more and over
//   TalkLong s of words under one shot; the side of the line between two said one way and then the other (the 180° rule); a push cut off
//   before its end. The shots' count, mean and longest stand under the table.
// cover(id): a first coverage of a scene's talks that have no camera of their own, by the common grammar — both first, then over the listener's
// shoulder at who speaks, close on a short line, the whole body for a clip, a slow push on a long line. Lines to put in, each with the line it
// goes before; nothing is written to the file (the author's own shots are never overwritten: the key scenes he films himself).
// What is heard: a column of the table says what the scene holds at each step (♪ music, ~ a bed, the mix, the ear), every one-shot is listed
// under it with its moment and length, and the sound has warnings of its own — tools/scene_sound.js (its head says each); sound(id): a first
// sound pass proposed for a scene, as cover(id) proposes its shots.
// What it cannot know: how far a walk is (the places' positions are the world's) — a walk is «~» and its limit; whether an «if» holds (counted in).
//   node tools/scene_dry.js <scene>          or  node tools/anim.js scene --dry <scene>
const fs = require('fs'), path = require('path'), cp = require('child_process');
const root = path.join(__dirname, '..');
const scenesDir = path.join(root, 'assets', 'story', 'scenes'), clipsDir = path.join(root, 'assets', 'story', 'clips');
// the game's numbers (where they live in the code)
// the sisters' falcons (Falcon, StoryScenesFalcon): from so far, at its cruise, down at its approach, then the flare and braking
const FalconFrom = 90, FalconUp = 35, FalconCruise = 11, FalconApproach = 7, FalconBrake = 3.5, FalconLand = 8; // StoryScenes.FalconFrom/Up · Falcon.Cruise/Approach · its braking (BrakeAt, PostureSeconds) · a landing from its circle
const FalconSkim = 6.5, SkimWay = 56; // Falcon.SkimSpeed · m of a low run as the ambush lays it (the places are the world's: a lane's words are not reckoned here)
const StepLong = 8, StepMax = 30, CastWait = 180, BirdFar = 40, Cruise = 9, LandSec = 3, WalkSpeed = 1.3, RunSpeed = 3.5, BackSlowest = 0.35, FadePerson = 3; // StudioScene.StepLong · StoryScenes.StepMax · CastWait · (a bird far off) · RavenFlight Hugin.Cruise · its landing · StoryScenesCast (npc go) · StoryWalk.BackSlowest · Fades.Person
const readSec = text => Math.min(8, Math.max(2, 1.2 + text.length * 0.06)) * 1.1; // RavenSpeech.Seconds, Pace.Scene, ReadScale 1.1
const ShotShort = 1.2, TalkLines = 4, TalkLong = 14, ShortLine = 28, LongRead = 5, PushSecs = 3; // s a shot is read in · lines and s of talk one shot carries · letters of a line that is a look, not a speech · s of reading that asks for a push · a push's seconds when none are said (StoryScenes.PushSecs)
const SIZES = ['close', 'medium', 'full', 'wide'];
// the words of «camera on|shoulder|two», from where the game keeps them (src/StoryScenesCamera.cs: OnSizes, OnAngles, ReadFraming): a line with a word
// that is not there is left out of the scene by the game when it loads, and nothing on the screen says so (the review 2026-11-11: «clsoe»)
let _cam = null;
function camWords() {
  if (_cam) return _cam;
  const src = fs.readFileSync(path.join(root, 'src', 'StoryScenesCamera.cs'), 'utf8'), list = n => { const m = new RegExp(n + ' = \\{([^}]*)\\}').exec(src); return m ? m[1].split(',').map(x => x.trim().replace(/"/g, '')).filter(Boolean) : []; };
  return _cam = { sizes: list('OnSizes'), sides: list('OnAngles') };
}
// why the game would leave this camera step out (null: its words are known)
function cameraWhy(w) {
  const kind = w[1], cw = camWords();
  if (kind !== 'on' && kind !== 'shoulder' && kind !== 'two') return null; // (the older kinds take places and numbers: the game checks them as it plays)
  const pair = kind !== 'on', isNum = x => /^-?\d+(\.\d+)?$/.test(x), from = pair ? 4 : 3;
  if (w.length < from) return 'camera ' + kind + (pair ? ' <a> <b>' : ' <who>') + ': who is filmed is missing';
  if (pair && w[2] === w[3]) return 'camera ' + kind + ' ' + w[2] + ' ' + w[3] + ': the same one twice';
  let push = false;
  for (let i = from; i < w.length; i++) {
    const x = w[i];
    if (x === 'cut') continue;
    if (x === 'wait') { if (!push && !w.includes('push')) return '«wait» with no «push»: it would hold nothing'; continue; }
    if (x === 'blend' || x === 'fov' || x === 'live') { if (!isNum(w[i + 1] || '')) return '«' + x + '» needs a number after it'; i++; continue; }
    if (x === 'push') { if (!isNum(w[i + 1] || '')) return '«push» needs metres after it (push 0.3 4)'; push = true; i += isNum(w[i + 2] || '') ? 2 : 1; continue; }
    if (x === 'look') { if (pair) return '«look» is a word of «camera on» only: ' + kind + ' looks at <b> itself'; if (!w[i + 1]) return '«look» needs what to look at'; i++; continue; }
    if (pair) { if (x === 'left' || x === 'right') continue; return '«' + x + '» is not a word of camera ' + kind + ' (left right · push <m> <s> · blend <s> | cut · fov · live · wait)'; }
    if (cw.sizes.includes(x) || cw.sides.includes(x) || x === 'low' || x === 'high' || x === 'track' || isNum(x)) continue;
    return '«' + x + '» is not a word of camera on (' + cw.sizes.join(' ') + ' · ' + cw.sides.join(' ') + ' or degrees · low high · track · push <m> <s> · look <what> · blend <s> | cut · fov · live · wait)';
  }
  return null;
}
// a camera step as a shot: what it is of, how near, which side of the line
function shotOf(w) {
  const kind = w[1], pair = kind === 'shoulder' || kind === 'two', own = []; // (own: the words that make the shot what it is — not how it comes in, moves, or is held)
  let push = null;
  for (let i = 2; i < w.length; i++) {
    const x = w[i];
    if (x === 'cut' || x === 'wait' || x === 'track') continue;
    if (['blend', 'fov', 'live', 'look', 'ease'].includes(x)) { i++; continue; } // (a word and its value: «blend 2» is not an angle of 2)
    if (x === 'push') { const s = +w[i + 2]; push = isNaN(s) ? PushSecs : s; i += isNaN(s) ? 1 : 2; continue; }
    own.push(x);
  }
  const who = pair ? [w[2], w[3]].sort().join(' and ') : kind === 'on' || kind === 'orbit' || kind === 'shot' || kind === 'move' ? w[2] : ['face', 'follow', 'behind', 'over'].includes(kind) ? 'player' : kind;
  const size = kind === 'on' ? (own.find(x => SIZES.includes(x)) || 'medium') : kind === 'face' ? 'close' : kind;
  const side = pair ? own.slice(2).find(x => x === 'left' || x === 'right') : null;
  const sig = (kind === 'two' ? own.slice(2) : own).filter(x => x !== 'medium').join(' '); // (what two shots must share to be the same shot: «face 0 3» is not «face -55 9»; over a's shoulder is not over b's; two a b is two b a)
  return { kind, who, size, sig, push, cut: w.includes('cut'), world: side ? (side === 'left' ? -1 : 1) * (w[2] < w[3] ? 1 : -1) : 0, words: 0, wordSec: 0 }; // (left of a→b is right of b→a)
}
const f = v => (Math.round(v * 10) / 10).toString();

// a clip's file: its length, parts, marks, loop
const clips = {};
function clipOf(name) {
  const [whole, part] = name.split('.');
  if (!(whole in clips)) {
    const p = path.join(clipsDir, whole + '.txt');
    if (!fs.existsSync(p)) clips[whole] = null;
    else {
      const c = { length: 0, loop: false, parts: {}, marks: [], events: [] };
      for (const raw of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
        const w = raw.trim().split(/\s+/);
        if (w[0] === 'length') c.length = +w[1];
        else if (w[0] === 'loop') c.loop = w[1] === '1';
        else if (w[0] === 'part') c.parts[w[1]] = { from: +w[2], to: +w[3], how: w[4] || 'hold' };
        else if (w[0] === 'mark') c.marks.push({ t: +w[1], name: w[2] });
        else if (w[0] === 'event') c.events.push({ t: +w[1], name: w[2] }); // (a sound the clip carries: «event <time> <sound> [volume]»)
      }
      clips[whole] = c;
    }
  }
  const c = clips[whole];
  if (!c) return null;
  if (!part) return { length: c.length, loop: c.loop, marks: c.marks, events: c.events, from: 0, known: true };
  const pt = c.parts[part];
  if (!pt) return null;
  return { length: pt.to - pt.from, loop: pt.how === 'loop', marks: c.marks.filter(m => m.t >= pt.from && m.t <= pt.to).map(m => ({ t: m.t - pt.from, name: m.name })), events: c.events.filter(m => m.t >= pt.from && m.t <= pt.to).map(m => ({ t: m.t - pt.from, name: m.name })), from: pt.from, known: true };
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
    const m = /^(roams|alone|aside|needs|check|test|place|guard):\s*(.*)$/.exec(line);
    if (m) { head[m[1]] = head[m[1]] ? head[m[1]] + '; ' + m[2] : m[2]; continue; }
    let cond = null, text = line;
    if (line.startsWith('if ')) { const c = line.indexOf(':'); cond = line.slice(3, c).trim(); text = line.slice(c + 1).trim(); }
    steps.push({ num, line, cond, w: text.split(/\s+/), text });
  }
  const warn = [];
  const lane = require('./scene_sound.js').lane(warn); // (what is heard: its own file)
  let filmed = false;
  const put = new Set((head.test || '').split(';').map(s => s.trim()).filter(Boolean).filter(a => /^npc \S+ at /.test(a)).map(a => a.split(/\s+/)[1]));
  const bare = !head.check && !head.test; // (said in the table's head: a fact for whoever runs the check, not a mistake of the scene)
  // the walk through the steps
  const falcons = new Map(); // (name → flying: the scene's falcons)
  const skims = new Map();   // (name → when its low run began)
  let t = 0, cam = false, camSince = 0, camStep = null, bird = null, birdFlying = false, nothing = 0, nothingFrom = null, nothingSteps = 0, saidUntil = 0; // (saidUntil: the end of the last «npc <who> says»)
  const cast = new Set(), rows = [], bodyClip = {}; // (who: {clip, at} — the clip started on him and when)
  const shots = []; let shot = null;
  const endShot = () => { if (shot) { shot.end = t; shots.push(shot); shot = null; } };
  const words = s => s.w.slice(s.w[0] === 'npc' ? 3 : 2).join(' ');
  for (const s of steps) {
    const w = s.w, kind = w[0];
    let took = 0, note = '', shown = null;
    const sub = (text, spoken, overBird) => { took = readSec(text); if (shot && spoken) { shot.words++; shot.wordSec += took; } if (overBird && bird && birdFlying) { note += 'words over the bird in flight: unreadable; '; warn.push('line ' + s.num + ': words while the bird is in flight («' + text.slice(0, 30) + '»): nobody reads them over it — land it first, or say them before «bird come … nowait»'); } };
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
      case 'wait': took = w[1] === 'said' ? Math.max(0, saidUntil - t) : (+w[1] || 1); break;
      case 'until': { const lim = +(s.text.slice(s.text.lastIndexOf('|') + 1).trim()) || 10; took = lim; shown = '≤' + f(lim); if (lim > StepLong) note += 'waits up to ' + f(lim) + ' s for its condition; '; break; }
      case 'sub': sub(w.slice(1).join(' ')); break;
      case 'say': sub(w.slice(2).join(' '), true); note += '(only with ' + w[1] + ' on the shoulder) '; break;
      case 'msg': took = 0; break;
      case 'fade': took = w[2] ? +w[2] : 1.5; break;
      case 'glow': break; // (the light of a memory: does not wait)
      case 'camera':
        { const ended = shot; endShot(); if (ended && w[1] === 'off') ended.off = true; }
        { const why = cameraWhy(w); if (why) { note += 'THE GAME WILL LEAVE THIS STEP OUT; '; warn.push('line ' + s.num + ': ' + why); } }
        if (w[1] === 'off') { cam = false; camStep = null; }
        else { if (!cam) camSince = t; cam = true; camStep = s; shot = Object.assign(shotOf(w), { num: s.num, at: t, line: s.text, cond: !!s.cond }); }
        if (shot && shot.push !== null) { if (w.includes('wait')) took = shot.push; else shot.pushTill = t + shot.push; } // (no seconds said: the game's own PushSecs)
        if (w[1] === 'move' && w.includes('wait')) took = +w[4] || 0;
        if (w[1] === 'orbit' && w.includes('wait')) took = +w[6] || 0;
        break;
      case 'player':
        if (w[1] === 'clip') {
          if (w[2] !== 'none') {
            const c = clipOf(w[2]);
            if (!c) { note += 'clip ' + w[2] + ' not a file of assets/story/clips (built in, or a part that is not there): its length unknown; '; shown = '?'; }
            else { bodyClip.player = { clip: w[2], at: t }; note += lane.clip('player', c, t); if (c.loop) note += 'loops: on him till the next clip; '; else if (!w.includes('nowait')) took = c.length; }
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
        else if (w[1] === 'say') sub(w.slice(2).join(' '), true, true);
        break;
      case 'falcon': {
        const name = w[1], verb = w[2];
        if (verb === 'come') {
          const d = +w[3] || FalconFrom, h = +w[4] || FalconUp, flight = Math.hypot(d, h) / ((FalconCruise + FalconApproach) / 2) + FalconBrake;
          if (w.includes('circle') || w.includes('nowait')) { falcons.set(name, true); took = 0; note += 'on its way ~' + f(flight) + ' s; '; }
          else { falcons.set(name, false); took = flight; shown = '~' + f(flight); }
          if (d > BirdFar) note += 'from ' + d + ' m: ~' + f(Math.hypot(d, h) / FalconCruise) + ' s of flying in the frame; ';
        }
        else if (verb === 'ready') { /* made beforehand, unseen: nothing on the screen */ }
        else if (verb === 'skim') { falcons.set(name, true); skims.set(name, t); note += 'the low run: over its place in about ' + f(SkimWay / FalconSkim) + ' s if it has ' + SkimWay + ' m to go (the scene does not wait); '; } // (Falcon.Skim: one pace all the way)
        else if (verb === 'from') { falcons.set(name, false); if (w[3]) cast.delete(w[3]); } // (a person of the scene turns into a falcon: it makes it)
        else if (!falcons.has(name)) note += 'no falcon «' + name + '» yet (falcon ' + name + ' come first): the game skips it; ';
        else if (verb === 'land') { falcons.set(name, false); took = FalconLand; shown = '~' + FalconLand; }
        else if (verb === 'circle') falcons.set(name, true);
        else if (verb === 'become') {
          if (falcons.get(name) && !w.includes('stride')) note += 'it becomes a person in the air: it should land first, or say «stride»; ';
          // (out of a low run the step waits till the falcon is over its place — unless «nowait»: counted from when its run began, at the way guessed above)
          if (skims.has(name) && w.includes('stride') && !w.includes('nowait')) { took = Math.max(0, skims.get(name) + SkimWay / FalconSkim - t); shown = '~' + f(took); }
          skims.delete(name); falcons.delete(name); if (w[3]) cast.add(w[3]);
        }
        else if (verb === 'leave') falcons.set(name, true);
        else if (verb === 'gone') falcons.delete(name);
        break;
      }
      case 'npc': {
        const who = w[1], verb = w[2];
        if (verb !== 'spawn' && verb !== 'ready' && verb !== 'free' && !cast.has(who)) {
          cast.add(who);
          if (!put.has(who) && !steps.some(x => x.w[0] === 'npc' && x.w[1] === who && (x.w[2] === 'spawn' || x.w[2] === 'ready'))) warn.push('«' + who + '» is taken from the world and nothing says where: the check waits up to ' + CastWait + ' s for one that is far — add «test: npc ' + who + ' at <place>»');
        }
        if (verb === 'go') { if (w.includes('nowait')) note += who + ' walks on as the scene goes on; '; else walk(who, 8 + (w.includes('run') ? 10 : 20)); }
        else if (verb === 'arrive') walk(who + ' (arrive)', +w[3] || 12);
        else if (verb === 'ready') cast.delete(who); // (made beforehand, unseen: not in the picture till a falcon turns into it)
        else if (verb === 'say') sub(w.slice(3).join(' '), true);
        else if (verb === 'says') { sub(w.slice(3).join(' '), true); saidUntil = t + took; took = 0; note += 'the scene goes on as it is said (till ' + f(saidUntil) + ' s: «wait said»); '; }
        else if (verb === 'fade') { took = w[3] ? +w[3] : FadePerson; cast.delete(who); }
        else if (verb === 'sparks') { took = w[3] ? +w[3] : 3.5; cast.delete(who); } // (gone in sparks: NpcSparks)
        else if (verb === 'free') cast.delete(who);
        else if (verb === 'mark') markOn(who, w[3], +w[4] || 30);
        else if (verb === 'clip' && w[3] !== 'none') { const c = clipOf(w[3]); if (c) { bodyClip[who] = { clip: w[3], at: t }; note += lane.clip('npc.' + who, c, t); } }
        break;
      }
      case 'lane': case 'bars': case 'weather': case 'thor': case 'shake': case 'bolt': case 'do': case 'drop': case 'door': case 'prop': case 'watchers':
      case 'raven': case 'own':
        break;
      case 'music': case 'bed': case 'mix': case 'mute': case 'unmute': case 'ear': case 'sound':
        { const r = lane.step(s, t, cam); took = r.took; if (r.shown) shown = r.shown; note += r.note; }
        break;
      default: note += 'a word the game may not know (storycheck says); ';
    }
    // nothing on screen: waits and words only, one after the other
    const idle = kind === 'wait' || kind === 'msg' || kind === 'sound';
    if (idle) { if (nothingFrom === null) nothingFrom = s; nothing += took; nothingSteps++; }
    else { if (nothing > StepLong && nothingSteps > 1) warn.push('from step ' + nothingFrom.num + ' («' + nothingFrom.line.slice(0, 40) + '»): ' + f(nothing) + ' s of nothing but waits — is anything happening on screen?'); nothing = 0; nothingFrom = null; nothingSteps = 0; }
    if (cam && took > StepLong) note += 'holds the shot ' + f(took) + ' s (StepLong ' + StepLong + '); ';
    if (shot && shown && /^[~≤?]/.test(shown)) shot.unknown = true; // (a walk, an «until»: how long the shot is held the file does not say)
    const inFrame = [cam ? 'cam' : 'free', bird ? (birdFlying ? 'bird↑' : 'bird') : null, ...[...falcons].map(([n, fl]) => 'falcon.' + n + (fl ? '↑' : '')), ...cast].filter(Boolean).join(' ');
    if (cam || (kind === 'bars' && w[1] === 'on')) filmed = true;
    rows.push({ num: s.num, at: t, took, shown: shown || f(took), cond: s.cond, text: s.text, who: inFrame, heard: lane.heard(), note: note.trim() });
    t += took;
  }
  if (nothing > StepLong && nothingSteps > 1) warn.push('from step ' + nothingFrom.num + ': ' + f(nothing) + ' s of nothing but waits at the end');
  endShot();
  const soundLines = lane.end(t, filmed, id);
  if (cam) warn.push('the camera is still on at the end (camera off comes with the end of the scene: fine if meant)');
  for (let i = 0; i < shots.length; i++) {
    const a = shots[i], b = shots[i + 1], held = a.end - a.at, either = b && a.cond && b.cond && held === 0; // (two «if» camera lines one after the other: one or the other plays, not both)
    if ((b || a.off) && !either && !a.unknown && held < ShotShort) warn.push('line ' + a.num + ': the shot «' + a.line.slice(0, 40) + '» is cut away after ' + f(held) + ' s: the eye does not read it under ' + ShotShort + ' s — hold it, or leave it out');
    if (b && b.cut && !either && a.kind === b.kind && a.who === b.who && a.sig === b.sig && a.kind !== 'shot' && a.kind !== 'move') warn.push('line ' + b.num + ': a cut between two like shots of ' + b.who + ' jumps: change how near it is (close ↔ medium ↔ full) or the side (front ↔ left|right), or blend');
    if (a.words >= TalkLines && a.wordSec > TalkLong && a.kind !== 'shot' && a.kind !== 'move' && a.kind !== 'orbit') warn.push( // (not under the author's own shot: his to judge)
      'line ' + a.num + ': ' + a.words + ' lines, ' + f(a.wordSec) + ' s of words under one shot: cut to who speaks (camera on npc.<who> close cut · camera shoulder player npc.<who> cut) — node tools/anim.js scene --cover ' + id + ' proposes the cuts');
    if (a.pushTill && a.pushTill > a.end + 0.1) warn.push('line ' + a.num + ': its push would take ' + f(a.pushTill - a.at) + ' s, the shot is cut after ' + f(held) + ' s: a shorter push, or «wait»');
  }
  const lineSide = {};
  for (const a of shots) if (a.world) { if (lineSide[a.who] && lineSide[a.who] !== a.world) warn.push('line ' + a.num + ': the camera crosses the line between ' + a.who + ' (their sides swap in the picture): keep one side — drop left|right here, the camera keeps the scene\'s own'); else lineSide[a.who] = a.world; }
  // the table
  const out = [];
  out.push('scene ' + id + ' (' + sc.file + '), dry: ~' + f(t) + ' s in all, ' + steps.length + ' step(s)' + (head.place ? ', place ' + head.place : '') + (head.needs ? ', needs ' + head.needs : '') + (head.test ? '\n  test: ' + head.test : '') + (head.check ? '\n  check: ' + head.check : '') + (bare ? '\n  (no «check:» or «test:» line: a run in the game starts it in the middle of its place, in whatever world the game is in)' : ''));
  const heardWide = Math.max(5, ...rows.map(r => r.heard.length));
  out.push('line | at s | takes | camera, who the scene holds | ' + 'heard'.padEnd(heardWide) + ' | step | note'); // (the trial 2026-11-11 read «in frame» as who is filmed: it is whether a shot is on and whom the scene has taken)
  for (const r of rows) out.push(String(r.num).padStart(4) + ' | ' + f(r.at).padStart(5) + ' | ' + r.shown.padStart(6) + ' | ' + r.who.padEnd(14) + ' | ' + r.heard.padEnd(heardWide) + ' | ' + (r.cond ? 'if ' + r.cond + ': ' : '') + r.text + (r.note ? '  — ' + r.note : ''));
  if (shots.length) { const known = shots.filter(x => !x.unknown), more = shots.length - known.length; if (known.length) { const long = known.reduce((a, b) => b.end - b.at > a.end - a.at ? b : a); out.push('shots: ' + shots.length + (more ? ' (' + more + ' with a walk or a wait in ' + (more > 1 ? 'them' : 'it') + ': how long, the file does not say); the others ' : ', ') + f(known.reduce((s, x) => s + x.end - x.at, 0) / known.length) + ' s each on average, the longest ' + f(long.end - long.at) + ' s (line ' + long.num + ')'); } else out.push('shots: ' + shots.length + ', each with a walk or a wait in it: how long, the file does not say'); }
  for (const x of soundLines) out.push(x);
  if (warn.length) { out.push('warnings:'); for (const x of warn) out.push('  - ' + x); }
  if (said.length) { out.push('storycheck on it:'); for (const x of said) out.push('  - ' + x); }
  if (!warn.length && !said.length) out.push('warnings: none from the file (the run still judges the poses, the walks, the frame)');
  return out.join('\n');
}

// A first coverage of the talks of a scene (the head of this file): lines to put in, nothing written.
function cover(id) {
  const sc = sceneLines(id);
  if (!sc) return 'scene_dry: no scene «' + id + '» in assets/story/scenes';
  const steps = sc.lines.filter(x => !/^(roams|alone|aside|needs|check|test|place|guard):/.test(x.line)).map(x => { let text = x.line, cond = null; if (text.startsWith('if ')) { const c = text.indexOf(':'); cond = text.slice(3, c).trim(); text = text.slice(c + 1).trim(); } return { num: x.num, text, cond, w: text.split(/\s+/) }; });
  // who speaks at a step, and the words (null: no speech)
  const speech = s => s.w[0] === 'npc' && (s.w[2] === 'say' || s.w[2] === 'says') ? { who: 'npc.' + s.w[1], text: s.w.slice(3).join(' ') } : s.w[0] === 'bird' && s.w[1] === 'say' ? { who: 'bird', text: s.w.slice(2).join(' ') } : s.w[0] === 'say' ? { who: 'player', text: s.w.slice(2).join(' ') } : null; // (say hugin|munin: the raven on his shoulder)
  const quiet = s => ['wait', 'sub', 'sound', 'msg', 'music', 'bed', 'mix'].includes(s.w[0]) || (s.w[0] === 'npc' && ['face', 'emote', 'clip', 'mark', 'prop', 'take'].includes(s.w[2])) || (s.w[0] === 'player' && ['look', 'face', 'emote', 'clip', 'mark'].includes(s.w[1])) || (s.w[0] === 'bird' && s.w[1] === 'look');
  // «if» lines of speech one after the other are ONE line, said one way or the other (the raven on the shoulder: hugin's words or munin's): only the first
  // of them is a line of the talk, and only when whoever speaks is the same in each (else nothing is proposed before them: which plays, the file does not say)
  const alt = k => k > 0 && !!steps[k].cond && !!steps[k - 1].cond && !!speech(steps[k]) && !!speech(steps[k - 1]);
  const mixed = k => { let j = k; while (j + 1 < steps.length && alt(j + 1)) j++; return new Set(steps.slice(k, j + 1).map(s => speech(s).who)).size > 1; };
  const out = [], hasBars = steps.some(s => s.w[0] === 'bars' && s.w[1] === 'on');
  let camOn = false, authors = false, i = 0, talks = 0, bars = false;
  const AUTHOR = ['shot', 'move', 'orbit'];
  while (i < steps.length) {
    if (steps[i].w[0] === 'camera') { camOn = steps[i].w[1] !== 'off'; authors = AUTHOR.includes(steps[i].w[1]); i++; continue; }
    if (!speech(steps[i])) { i++; continue; }
    let j = i, said = 0; // (a talk: speech and the quiet steps between, to the first step that is neither)
    while (j < steps.length && (speech(steps[j]) || quiet(steps[j]) || steps[j].w[0] === 'camera')) { if (speech(steps[j]) && !alt(j)) said++; j++; }
    while (j > i && !speech(steps[j - 1])) j--; // (it ends with its last line)
    const own = authors || steps.slice(i, j).some(s => s.w[0] === 'camera');
    if (said < 2 || own) { if (own && said >= 2) out.push('lines ' + steps[i].num + '–' + steps[j - 1].num + ': this talk has shots of its own — left as it is'); i = Math.max(j, i + 1); continue; }
    talks++;
    const first = speech(steps[i]), other = first.who === 'player' ? (steps.slice(i, j).map(speech).find(x => x && x.who !== 'player') || { who: 'player' }).who : 'player';
    const put = (s, line) => out.push('before line ' + s.num + ' («' + s.text.slice(0, 34) + '»): ' + line);
    if (!hasBars && !bars) { put(steps[i], 'bars on'); bars = true; }
    let now = null, size = ''; const heard = [], wasOn = camOn;
    if (first.who !== other && !camOn && !mixed(i)) { put(steps[i], 'camera two ' + first.who + ' ' + other + ' blend 1.5'); now = 'two'; size = 'two'; camOn = true; }
    for (let k = i; k < j; k++) {
      const s = steps[k], sp = speech(s);
      if (!sp) { if (s.w[0] === 'npc' && s.w[2] === 'clip' && s.w[3] !== 'none' && size !== 'full' && size !== 'two') { put(s, 'camera on npc.' + s.w[1] + ' full ' + (camOn ? 'cut' : 'blend 1.5')); now = 'npc.' + s.w[1]; size = 'full'; camOn = true; } continue; }
      if (alt(k)) continue;                 // (the other way of the line before: under the same shot)
      if (mixed(k)) continue;               // (ways by different speakers: no shot proposed)
      const others = heard.filter(x => x !== sp.who), listener = others.length ? others[others.length - 1] : sp.who === 'player' ? other : 'player'; // (who spoke last before him: the one he answers)
      const secs = readSec(sp.text), short = sp.text.replace(/[….?!\s]/g, '').length < ShortLine, push = secs > LongRead ? ' push 0.3 ' + f(secs) : '';
      const want = short ? 'close' : listener === sp.who ? 'medium' : 'shoulder'; // (nobody to look over the shoulder of — the raven on his own shoulder speaks: he alone)
      if (k === i && now === 'two') { heard.push(sp.who); continue; } // (the first line under the shot of both)
      if (now !== sp.who || size !== want) {
        const how = camOn ? ' cut' : ' blend 1.5';
        put(s, want === 'shoulder' ? 'camera shoulder ' + listener + ' ' + sp.who + how + push : 'camera on ' + sp.who + ' ' + want + how + push);
        now = sp.who; size = want; camOn = true;
      }
      heard.push(sp.who);
    }
    if (!wasOn && !steps.slice(j).some(s => s.w[0] === 'camera')) { const at = steps[j]; out.push((at ? 'before line ' + at.num + ' («' + at.text.slice(0, 34) + '»)' : 'at the end') + ': camera off blend 1.5' + (bars ? ' · bars off' : '')); camOn = false; bars = false; }
    i = j;
  }
  if (bars) out.push('at the end: bars off');
  if (!out.length) return 'scene ' + id + ': no talk of two lines or more to cover';
  return 'scene ' + id + ' (' + sc.file + '): a first coverage of its talk by the common grammar — both first, over the listener\'s shoulder at who speaks, close on a short line,\n' +
    'the whole body for a clip, a slow push on a long line. Take what fits; then node tools/anim.js scene --dry ' + id + '. The key scenes the user films himself (window «Режисер»).\n  ' + out.join('\n  ');
}

// a first sound pass proposed for a scene (tools/scene_sound.js): lines to put in, nothing written
const sound = id => require('./scene_sound.js').propose(id, sceneLines, clipOf);

module.exports = { dry, cover, sound, cameraWhy };
if (require.main === module) {
  const id = process.argv[2];
  if (!id) { console.error('node tools/scene_dry.js <scene>'); process.exit(1); }
  console.log(dry(id));
}

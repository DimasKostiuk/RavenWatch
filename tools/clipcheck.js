// The clips hold together, checked at the build: assets/story/clips and every clip a scene names. In the game a mistake here
// says nothing: a bone with a wrong name moves nothing, a channel over the limit is left out, a clip that is only in the
// author's config plays on his machine and nowhere else. Each rule below comes from a mistake already made (docs/ANIMATION.md).
// Run: node tools/clipcheck.js  (exit code 1 on a problem: rw_build stops)
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const dir = path.join(root, 'assets', 'story', 'clips'), scenes = path.join(root, 'assets', 'story', 'scenes');
const MAX_CHANNELS = 40, MAX_KEYS = 200; // (BodyClips.MaxBones, MaxKeys)
const FOOT_UP = 0.14; // m: a foot target higher than this is off the ground (the game's stance: 0.12)
const PROP_OFF = 0.05, HELD = 0.9; // @Prop.w past this: the thing has left where it lay · a hand's .on.w this high: it is on the thing
const HAND_FULL = 0.95; // a hand target's .w this high: the hand is held there (BodyCheck.Full), and its palm is judged — if anything says where it faces
const problems = [];
// what the game can play by name (tools/game_sounds.txt, the game writes it: the «Звуки» tab) — a clip's event of any other sfx is silent in the game
// (2026-11-15: sfx_knife_swing and sfx_land are in the game's files, but inside other things' effects: no prefab of their own, nothing heard)
const gameList = path.join(__dirname, 'game_sounds.txt'), gameSfx = new Set();
if (fs.existsSync(gameList)) for (const l of fs.readFileSync(gameList, 'utf8').split(/\r?\n/)) { const [k, n] = l.split(' '); if (k === 'sfx') gameSfx.add(n); }
const say = (file, what) => problems.push('CLIP ' + file + ': ' + what);

const bones = new Set(['body', 'move', 'Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head']);
for (const s of ['Left', 'Right']) {
  for (const b of ['Shoulder', 'Arm', 'ForeArm', 'Hand', 'UpLeg', 'Leg', 'Foot', 'ToeBase']) bones.add(s + b);
  for (const f of ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']) for (const j of [1, 2, 3]) bones.add(s + 'Hand' + f + j);
}
// the clips made before «say» (the user's words) was there; a new one has them, or how it came to be is lost
const BEFORE_SAY = new Set(['arms_cross', 'door_push', 'heid_rummage', 'key_turn', 'look_caw', 'look_search', 'point_at', 'rub_hands', 'shield_kneel', 'shrug_small', 'sign_touch', 'step_back']);
// the clips made before the palm had to be said (2026-11-10), accepted by the user's eye: a new take of one of these says its palm, and its name goes
const BEFORE_PALM = new Set([]);
const FIXTURES = ['wall', 'table', 'seat', 'step', 'point', 'piece'];
const SIDES = ['left', 'right', 'both'];
// a «want» line: what BodyCheck.Holds reads; null: fine
function wantWhy(line, length) {
  const w = line.trim().split(/\s+/);
  const m = /^(\d+(?:\.\d+)?)(?:-(\d+(?:\.\d+)?))?$/.exec(w[0] || '');
  if (!m) return 'its time «' + w[0] + '» is not <t> or <t0-t1>';
  if (+(m[2] || m[1]) > length + 1e-6) return 'its time is past the length ' + length;
  if (m[2] && +m[2] <= +m[1]) return 'its stretch ends before it begins';
  const num = i => w.length > i && !isNaN(+w[i]);
  const dirs = ['up', 'down', 'left', 'right'], lean = ['forward', 'back', 'left', 'right'], palm = ['forward', 'back', 'up', 'down', 'left', 'right'];
  switch (w[1]) {
    case 'head': return dirs.includes(w[2]) && num(3) ? null : 'want … head up|down|left|right <deg>';
    case 'lean': return lean.includes(w[2]) && num(3) ? null : 'want … lean forward|back|left|right <deg>';
    case 'hips': return w[2] === 'low' && num(3) ? null : 'want … hips low <m>';
    case 'hand': return SIDES.includes(w[2]) && ((w[3] === 'on' && (w.length < 5 || (w[4] === 'body' && (w.length < 6 || num(5))))) || w[3] === 'holds' || (w[3] === 'near' && num(4) && num(5) && num(6)) || (w[3] === 'touches' && w[4] === 'prop') || (w[3] === 'below' && w[4] === 'shoulder')) ? null : 'want … hand left|right|both on [body [m]] | holds | near <x y z> [m] | touches prop | below shoulder';
    case 'palm': return SIDES.includes(w[2]) && (palm.includes(w[3]) || (w[3] === 'to' && w[4] === 'prop')) ? null : 'want … palm left|right|both forward|back|up|down|left|right | to prop';
    case 'elbow': return ['left', 'right'].includes(w[2]) && w[3] === 'bent' && num(4) ? null : 'want … elbow left|right bent <deg>';
    case 'look': return w[2] === 'prop' || (num(2) && num(3) && num(4)) ? null : 'want … look prop | <x y z>';
    case 'smooth': return !m[2] ? 'want <t0-t1> smooth: a stretch' : null;
    case 'foot': return ['left', 'right'].includes(w[2]) && (w[3] === 'down' || (w[3] === 'up' && num(4))) ? null : 'want … foot left|right up <m> | down';
    case 'knee': return ['left', 'right'].includes(w[2]) && w[3] === 'bent' && num(4) ? null : 'want … knee left|right bent <deg>';
    case 'weight': return ['left', 'right'].includes(w[2]) ? null : 'want … weight left|right';
    case 'still': return !m[2] ? 'want <t0-t1> still [deg/s]: a stretch' : null;
    case 'slow': return !m[2] || !num(2) ? 'want <t0-t1> slow <m/s>: a stretch' : null;
    case 'finger': return SIDES.includes(w[2]) && ['index', 'middle', 'ring', 'pinky'].includes(w[3]) && w[4] === 'straight' && (w.length < 6 || num(5)) ? null : 'want … finger left|right|both index|middle|ring|pinky straight [deg]';
  }
  return 'do not know «' + w[1] + '»: head, lean, hips, hand, palm, foot, knee, weight, still, slow, finger';
}

// a channel's value at t from its keys, straight between them (exact at keys; the game's curve stays between them)
const valueAt = (k, t) => {
  const s = k.slice().sort((a, b) => a.t - b.t);
  if (t <= s[0].t) return s[0].v;
  for (let i = 1; i < s.length; i++) if (t <= s[i].t) { const u = (t - s[i - 1].t) / Math.max(1e-6, s[i].t - s[i - 1].t); return s[i - 1].v.map((x, j) => x + (s[i].v[j] - x) * u); }
  return s[s.length - 1].v;
};
const known = ch => {
  if (bones.has(ch)) return true;
  if (/^@Look(\.w)?$/.test(ch) || /^@Prop(\.r|\.w)?$/.test(ch)) return true;
  return /^@(Left|Right)(Hand|Foot)(\.w|\.p)?$/.test(ch) || /^@(Left|Right)Hand\.(f|n)$/.test(ch) || /^@(Left|Right)Hand\.on(\.w|\.f|\.n)?$/.test(ch);
};

const clips = {};
for (const f of fs.existsSync(dir) ? fs.readdirSync(dir).filter(x => x.endsWith('.txt')) : []) {
  const name = f.slice(0, -4);
  if (name.includes('.')) say(f, 'a dot in a clip\'s name: the dot divides a clip from its part (shield_kneel.lifts)');
  const c = clips[name] = { parts: new Set(), partAt: {}, marks: [] };
  const channels = new Set(), times = new Set();
  let length = 0, eased = false;
  const parts = [], rows = [], wants = [], keys = {};
  let said = false, touches = false, toned = false;
  for (const raw of fs.readFileSync(path.join(dir, f), 'utf8').split(/\r?\n/)) {
    const l = raw.trim();
    if (!l || l.startsWith('#')) continue;
    if (/\s#/.test(l) && !/^(say|want)\s/.test(l)) say(f, l + ': a note after a line: the game cuts only whole-line notes, so it reads the «#» as a word. Put it on its own line');
    const w = l.split(/\s+/);
    if (w[0] === 'length') length = +w[1];
    else if (w[0] === 'curve') eased = w[1] === 'ease';
    else if (w[0] === 'part') parts.push({ name: w[1], from: +w[2], to: +w[3], how: w[4] || 'hold' });
    else if (w[0] === 'mark') { rows.push({ t: +w[1], what: 'mark ' + w[2] }); c.marks.push({ t: +w[1], name: w[2] }); }
    else if (w[0] === 'event') { rows.push({ t: +w[1], what: 'event ' + w[2] }); if (w.length < 3 || isNaN(+w[1])) say(f, l + ': event <time> <sound> [volume]'); else if (gameSfx.size && w[2].startsWith('sfx_') && !gameSfx.has(w[2])) say(f, l + ': no sound «' + w[2] + '» the game can play by name (tools/game_sounds.txt): silent in the game'); }
    else if (w[0] === 'say') said = true;
    else if (w[0] === 'want') wants.push(l.slice(4).trim());
    else if (w[0] === 'touch') { touches = true; if (!FIXTURES.includes(w[1])) say(f, '«touch» is a fixture\'s words: ' + FIXTURES.join(', ') + ' (AnimStageFixtures)'); else if (w[1] === 'piece' ? isNaN(+w[3]) : w[1] !== 'seat' && w[1] !== 'point' && isNaN(+w[2])) say(f, l + ': how far it is is missing'); }
    else if (w[0] === 'fade') { if (w.length !== 3 || isNaN(+w[1]) || isNaN(+w[2])) say(f, l + ': fade <in> <out>; with one number the game leaves the line out'); }
    else if (w[0] === 'mode') { if (w.length < 3 || !['body', 'aim', 'bone'].includes(w[2])) say(f, l + ': mode <bone> body|aim|bone; another word the game leaves out, and the bone\'s numbers then mean something else'); }
    else if (w[0] === 'base') { if (!['game', 'frozen'].includes(w[1])) say(f, l + ': base game|frozen'); }
    else if (w[0] === 'loop') { if (!['0', '1'].includes(w[1])) say(f, l + ': loop 0|1'); }
    else if (w[0] === 'inertia') { if (isNaN(+w[1]) || +w[1] < 0 || +w[1] > 1) say(f, l + ': inertia 0..1'); }
    else if (w[0] === 'vary') { if (isNaN(+w[1]) || +w[1] < 0 || +w[1] > 1) say(f, l + ': vary 0..1'); }
    else if (w[0] === 'prop') { if (!w[1] || (w[2] && w[2] !== 'r' && w[2] !== 'l') || (w[3] && !(+w[3] > 0))) say(f, l + ': prop <item> [r|l] [scale]'); }
    else if (w[0] === 'tone') { toned = true; if (!['plain', 'loose', 'merry', 'drinks'].includes(w[1])) say(f, l + ': tone plain|loose|merry|drinks (NpcMoves.Tones)'); }
    else if (w[0] === 'carry') { if (w[1] !== 'yes' && w[1] !== 'no') say(f, l + ': carry yes|no'); }
    else if (w[0] === 'follow') { if (!/^@(Left|Right)(Hand|Foot)$/.test(w[1] || '') || !w[2]) say(f, l + ': follow @LeftHand|@RightHand|@LeftFoot|@RightFoot <bone> (Spine2, RightShoulder…)'); }
    else if (w[0] === 'cloth') { if (w[1] !== 'off' && w[1] !== 'on') say(f, l + ': cloth off|on'); }
    else if (w[0] === 'strength') { if (isNaN(+w[1]) || +w[1] < 0 || +w[1] > 1) say(f, l + ': strength 0..1'); }
    else if (!isNaN(+w[0]) && w.length >= 5) {
      channels.add(w[1]); times.add(+w[0]); rows.push({ t: +w[0], what: w[1], y: +w[3] });
      if (w.slice(2, 5).some(v => isNaN(+v))) say(f, l + ': not a number');
      const k = keys[w[1]] = keys[w[1]] || [];
      if (k.some(x => Math.round(x.t * 1000) === Math.round(+w[0] * 1000))) say(f, w[1] + ' twice at ' + w[0] + ' (the game keeps a key\'s time to the ms): it keeps one of them, which is not said');
      k.push({ t: +w[0], v: w.slice(2, 5).map(Number) });
    }
    else if (!['clip', 'pivot', 'drive', 'end', 'feet'].includes(w[0])) say(f, 'do not know the line «' + l + '»');
  }
  if (!eased) say(f, 'no «curve ease»: what a key leaves out would be zero');
  if (!(length > 0)) say(f, 'no length');
  if (length > 60) say(f, 'length ' + length + ': the game cuts a clip at 60 s');
  for (const line of wants) { const why = wantWhy(line, length); if (why) say(f, 'want ' + line + ': ' + why); else if (!touches && /\bhand\s+\w+\s+on\b(?!\s+body)/.test(line)) say(f, 'want ' + line + ': «on» what? no «touch» line, so the stage puts nothing there and a scene measures nothing: it can never pass'); else if (/\b(hand\s+\w+\s+(holds|touches prop)|palm\s+\w+\s+to prop|look prop)\b/.test(line) && !channels.has('@Prop')) say(f, 'want ' + line + ': what thing? no @Prop in the clip, so nothing is in the hands: it can never pass'); }
  if (!toned) say(f, 'no «tone»: the kind of move it is — plain|loose|merry|drinks (NpcMoves.Tones). Go over every person in docs/jobs/people-moves.md first: whom it fits, whom not (the user 2026-11-17)');
  if (!said && !BEFORE_SAY.has(name)) say(f, 'no «say»: the words it was asked in, and «want» lines to measure them by (docs/ANIMATION.md)');
  for (const ch of channels) if (!known(ch)) say(f, '«' + ch + '» is no bone or channel: it would move nothing, silently');
  if (channels.size > MAX_CHANNELS) say(f, channels.size + ' channels, the game keeps ' + MAX_CHANNELS + ': the rest would be left out');
  if (times.size > MAX_KEYS) say(f, times.size + ' key times, the game keeps ' + MAX_KEYS);
  for (const r of rows) if (r.t > length + 1e-6) say(f, r.what + ' at ' + r.t + ' is past the length ' + length);
  // a target with no weight comes in at once and jumps; a grip by bone angles does not hold (ANIMATION.md)
  for (const s of ['Left', 'Right']) {
    const h = '@' + s + 'Hand';
    if (channels.has(h) && !channels.has(h + '.w')) say(f, h + ' has no ' + h + '.w: the hand would jump to it');
    // a hand sent somewhere with its full weight: where its palm faces must be SAID — by .f + .n, by .on.f + .on.n on a thing, or by «want … palm» —
    // or nothing judges it, and a fist stood where a palm was meant through every rule (door_push 2026-11-04, take_book's left hand 2026-11-07)
    const held = channels.has(h) && (!keys[h + '.w'] || keys[h + '.w'].some(k => k.v[0] >= HAND_FULL)); // (no .w: the core holds it at full weight all along)
    const side = s.toLowerCase(), palmSaid = (channels.has(h + '.f') && channels.has(h + '.n')) || (channels.has(h + '.on.f') && channels.has(h + '.on.n')) || wants.some(l => new RegExp('\\bpalm\\s+(' + side + '|both)\\b').test(l));
    if (held && !palmSaid && !BEFORE_PALM.has(name)) say(f, h + ' is sent somewhere with its full weight, and nothing says where its palm faces: give it ' + h + '.f + ' + h + '.n (directions in the body\'s frame), or «want <t> palm ' + side + ' up|down|forward|back|left|right» — a palm no one said is a palm no one judges');
    if (channels.has(h + '.f') !== channels.has(h + '.n')) say(f, h + '.f and ' + h + '.n go together (fingers and palm): one alone does nothing');
    if ((channels.has(h + '.f') || channels.has(h + '.n')) && !channels.has(h)) say(f, h + '.f/.n turn the hand by its target\'s weight: no ' + h + ', they do nothing');
    if (channels.has(h + '.on')) {
      if (!channels.has(h + '.on.w')) say(f, h + '.on has no ' + h + '.on.w: the hand would jump to the thing');
      if (!channels.has(h + '.on.f') || !channels.has(h + '.on.n')) say(f, h + '.on needs ' + h + '.on.f and ' + h + '.on.n: without them the hand keeps the game\'s pose on the thing');
    }
  }
  // a weight is 0..1 (its first number); a hand's fingers and palm are directions: never zero, never the same line
  for (const ch of Object.keys(keys)) {
    if (/\.w$/.test(ch)) for (const k of keys[ch]) if (k.v[0] < -1e-6 || k.v[0] > 1 + 1e-6) say(f, ch + ' ' + k.v[0] + ' at ' + k.t + ': a weight is 0..1, the game clamps it, so the key says what it does not do');
    if (/Hand(\.on)?\.(f|n)$/.test(ch)) for (const k of keys[ch]) if (Math.hypot(...k.v) < 1e-3) say(f, ch + ' at ' + k.t + ' is 0 0 0: no direction, the hand turns anywhere');
  }
  for (const s of ['@LeftHand', '@RightHand']) for (const on of ['', '.on']) {
    const fk = keys[s + on + '.f'], nk = keys[s + on + '.n'];
    if (!fk || !nk) continue;
    for (const k of fk.concat(nk)) {
      const a = valueAt(fk, k.t), b = valueAt(nk, k.t), la = Math.hypot(...a), lb = Math.hypot(...b);
      if (la > 1e-3 && lb > 1e-3 && Math.abs(a[0] * b[0] + a[1] * b[1] + a[2] * b[2]) / (la * lb) > 0.98) { say(f, s + on + '.f and .n on one line at ' + k.t + ': fingers and palm the same way say no turn'); break; }
    }
  }
  // a loop's part ends as it began: else the body jumps each time it starts over (a weight 0.05, a position 0.03 m, an angle 4°)
  for (const p of parts) if (p.how === 'loop') for (const ch of Object.keys(keys)) {
    const a = valueAt(keys[ch], p.from), b = valueAt(keys[ch], p.to);
    const off = Math.max(...a.map((x, i) => Math.abs(x - b[i]))), most = /\.w$/.test(ch) ? 0.05 : /\.(f|n)$/.test(ch) ? 0.07 : /\.r$/.test(ch) || !(ch.startsWith('@') || ch === 'move') ? 4 : 0.03; // (.f .n: directions, 0.07 of a unit is about 4°)
    if (off > most) say(f, 'part ' + p.name + ' loops but ' + ch + ' is ' + a.map(x => +x.toFixed(3)).join(' ') + ' at ' + p.from + ' and ' + b.map(x => +x.toFixed(3)).join(' ') + ' at ' + p.to + ': it jumps each time round');
  }
  if (channels.has('@Prop') && !channels.has('@Prop.w')) say(f, '@Prop has no @Prop.w: the thing would leave where it lay at once');
  // the hand is on the thing before the thing moves, and stays till it lies again: else it flies to the hand (take_book, 2026-11-07)
  if (keys['@Prop.w']) {
    const on = t => Math.max(...['@LeftHand', '@RightHand'].map(s => !keys[s + '.on'] || !keys[s + '.on.w'] ? 0 : valueAt(keys[s + '.on.w'], t)[0] * (keys[s] && keys[s + '.w'] ? valueAt(keys[s + '.w'], t)[0] : 1)));
    for (let t = 0; t <= length + 1e-6; t += 0.02) {
      const w = valueAt(keys['@Prop.w'], t)[0];
      if (w > PROP_OFF && on(t) < HELD) { say(f, 'at ' + t.toFixed(2) + ' s @Prop.w is ' + w.toFixed(2) + ' (off where it lay) and no hand is on it (.on.w × the hand\'s .w ' + on(t).toFixed(2) + ', needs ' + HELD + '): it moves by itself'); break; }
    }
  }
  if (channels.has('@Look') && !channels.has('@Look.w')) say(f, '@Look has no @Look.w: the head would snap to it');
  // the feet go in turn: the next leaves the ground only when the one before stands on it (the user 2026-11-04)
  const footKeys = {};
  for (const ch of ['@LeftFoot', '@RightFoot']) footKeys[ch] = rows.filter(r => r.what === ch).sort((a, b) => a.t - b.t);
  const footY = (ch, t) => {
    const k = footKeys[ch];
    if (!k.length) return null;
    if (t <= k[0].t) return k[0].y;
    for (let i = 1; i < k.length; i++) if (t <= k[i].t) return k[i - 1].y + (k[i].y - k[i - 1].y) * (t - k[i - 1].t) / Math.max(1e-6, k[i].t - k[i - 1].t);
    return k[k.length - 1].y;
  };
  for (let t = 0; t <= length; t += 0.02) {
    const l = footY('@LeftFoot', t), r = footY('@RightFoot', t);
    if (l !== null && r !== null && l > FOOT_UP && r > FOOT_UP) { say(f, 'both feet off the ground at ' + t.toFixed(2) + ' s: the next foot leaves only when the one before stands'); break; }
  }
  let end = null;
  for (const p of parts) {
    c.parts.add(p.name); c.partAt[p.name] = p;
    c.lastPart = p; c.length = length;
    if (!(p.from < p.to) || p.to > length + 1e-6) say(f, 'part ' + p.name + ': ' + p.from + '..' + p.to + ' is not within the clip');
    if (!['hold', 'loop', 'end'].includes(p.how)) say(f, 'part ' + p.name + ': «' + p.how + '» is not hold, loop or end');
    if (end !== null && p.from < end) say(f, 'part ' + p.name + ' starts inside the one before');
    end = p.to;
  }
}

// the built-in ones, in the code: their names
const code = fs.readFileSync(path.join(root, 'src', 'BodyClips.cs'), 'utf8');
const builtin = new Set();
for (const m of code.matchAll(/"clip ([\w.-]+)\\n/g)) builtin.add(m[1]);

// every clip a scene names is in the mod, not only in someone's config
let named = 0;
const clipHeld = (at, name) => {
  named++;
  const [clip, part] = name.split('.');
  if (clips[clip]) {
    if (part && !clips[clip].parts.has(part)) say(at, 'clip ' + clip + ' has no part «' + part + '»');
    const last = clips[clip].lastPart; // (a clip that ends held, played whole: it plays to its end and lets the body go — Ketil rose from his knees in the sparks, 2026-11-14)
    if (!part && last && last.how === 'hold' && last.to >= clips[clip].length - 1e-6) say(at, 'clip ' + clip + ' ends in its part «' + last.name + '» that holds, but is played whole: whole, it lets the body go at its end — play «' + clip + '.' + last.name + '»');
  }
  else if (!builtin.has(name)) say(at, 'no clip «' + name + '» in assets/story/clips or the built-in ones: it would play only where a config has it');
};
// a standing word (StoryHold), a scene's «do» or a quest's start:/done:
const holdClips = (at, raw) => {
  const m = /\bnpc \S+ hold \S+ clip (\S+)(?: from (\S+))?/.exec(raw);
  if (!m) return;
  clipHeld(at, m[1]);
  if (m[2]) clipHeld(at, m[2]);
};
for (const f of fs.existsSync(scenes) ? fs.readdirSync(scenes).filter(x => x.endsWith('.txt')) : []) {
  const on = {}; // (the clip on each body now, within a scene: what its «mark» is looked for in)
  fs.readFileSync(path.join(scenes, f), 'utf8').split(/\r?\n/).forEach((raw, i) => {
    const at = f + ':' + (i + 1);
    if (/^\s*\[scene /.test(raw)) { for (const k of Object.keys(on)) delete on[k]; return; }
    holdClips(at, raw);
    const m = /^\s*(?:if [^:]*:\s*)?(player|npc \S+) clip\s+(\S+)/.exec(raw);
    if (m) { on[m[1]] = m[2]; if (m[2] !== 'none') clipHeld(at, m[2]); return; }
    const k = /^\s*(?:if [^:]*:\s*)?(player|npc \S+) mark\s+(\S+)/.exec(raw);
    if (!k) return;
    const name = on[k[1]];
    if (!name || name === 'none') { say(at, k[1] + ' mark ' + k[2] + ': no clip of ours on it before this line in the scene'); return; }
    const [clip, part] = name.split('.');
    if (!clips[clip]) return; // (a built-in one: its marks are in the code)
    const p = part ? clips[clip].partAt[part] : null;
    if (!clips[clip].marks.some(x => x.name === k[2] && (!p || (x.t >= p.from && x.t <= p.to)))) say(at, 'no «mark ' + k[2] + '» in ' + name + ': the step would be skipped');
  });
}
const quests = path.join(root, 'assets', 'story', 'quests');
for (const f of fs.existsSync(quests) ? fs.readdirSync(quests).filter(x => x.endsWith('.txt')) : []) fs.readFileSync(path.join(quests, f), 'utf8').split(/\r?\n/).forEach((raw, i) => holdClips(f + ':' + (i + 1), raw));

// every clip of the mod has its row in docs/CLIPS.md (what it shows, its body, from → to, its parts, where it is used, its state):
// the catalog is how a pose like the one wanted is found before one is made again (the user 2026-11-04)
const catalog = path.join(__dirname, '..', 'docs', 'CLIPS.md');
const rows = fs.existsSync(catalog) ? fs.readFileSync(catalog, 'utf8') : '';
for (const name of Object.keys(clips)) if (rows.indexOf('| `' + name + '` |') < 0) say(name + '.txt', 'no row in docs/CLIPS.md: say what it shows, on which body, from → to, its parts, where it is used, its state');

// the people of the story and their moves (the user 2026-11-17: every new or changed person, every new clip — all of them gone over): a person
// written by hand says what kinds of moves are not its own («shuns: …», «shuns: none») and how lively it is («persona: … lively=…»), and stands in
// the table of docs/jobs/people-moves.md, which is the list gone over for each new clip
{
  const npcDir = path.join(root, 'assets', 'story', 'npc'), page = path.join(root, 'docs', 'jobs', 'people-moves.md');
  const table = fs.existsSync(page) ? fs.readFileSync(page, 'utf8') : '';
  if (!table) problems.push('PEOPLE docs/jobs/people-moves.md is missing: the table of the story\'s people and their moves');
  for (const f of fs.existsSync(npcDir) ? fs.readdirSync(npcDir).filter(x => x.endsWith('.txt') && !x.startsWith('_') && x !== 'settler.txt') : []) {
    const lines = fs.readFileSync(path.join(npcDir, f), 'utf8').split(/\r?\n/).map(x => x.replace(/#.*/, '').trim()), id = f.replace(/\.txt$/, '');
    const shuns = lines.find(x => /^shuns\s*:/.test(x)), persona = lines.find(x => /^persona\s*:/.test(x)) || '';
    if (!shuns) problems.push('PEOPLE npc/' + f + ': no «shuns:» line — which kinds of moves are not this person\'s (loose merry drinks, or «shuns: none»); the user says so, docs/jobs/people-moves.md');
    else for (const t of shuns.split(':')[1].trim().split(/\s+/)) if (!['none', 'loose', 'merry', 'drinks'].includes(t)) problems.push('PEOPLE npc/' + f + ': shuns «' + t + '» — loose|merry|drinks|none');
    if (!/\blively=/.test(persona)) problems.push('PEOPLE npc/' + f + ': no «lively=» in its «persona:» line — how much its body does of itself (1 as anyone, 0.45 Heid); docs/jobs/people-moves.md');
    if (table && !new RegExp('^\\| `' + id + '` \\|', 'm').test(table)) problems.push('PEOPLE npc/' + f + ': no row in docs/jobs/people-moves.md — the list gone over for every new clip');
  }
}
if (problems.length) { for (const p of problems) console.log(p); console.log('clipcheck: ' + problems.length + ' problem(s)'); process.exit(1); }
console.log('clipcheck: ok (' + Object.keys(clips).length + ' clip file(s), ' + named + ' named by scenes)');

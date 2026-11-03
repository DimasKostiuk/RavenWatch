// The clips hold together, checked at the build: assets/story/clips and every clip a scene names. In the game a mistake here
// says nothing: a bone with a wrong name moves nothing, a channel over the limit is left out, a clip that is only in the
// author's config plays on his machine and nowhere else. Each rule below comes from a mistake already made (docs/ANIMATION.md).
// Run: node tools/clipcheck.js  (exit code 1 on a problem: rw_build stops)
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const dir = path.join(root, 'assets', 'story', 'clips'), scenes = path.join(root, 'assets', 'story', 'scenes');
const MAX_CHANNELS = 40, MAX_KEYS = 200; // (BodyClips.MaxBones, MaxKeys)
const problems = [];
const say = (file, what) => problems.push('CLIP ' + file + ': ' + what);

const bones = new Set(['body', 'move', 'Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head']);
for (const s of ['Left', 'Right']) {
  for (const b of ['Shoulder', 'Arm', 'ForeArm', 'Hand', 'UpLeg', 'Leg', 'Foot', 'ToeBase']) bones.add(s + b);
  for (const f of ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky']) for (const j of [1, 2, 3]) bones.add(s + 'Hand' + f + j);
}
const known = ch => {
  if (bones.has(ch)) return true;
  if (/^@Look(\.w)?$/.test(ch) || /^@Prop(\.r|\.w)?$/.test(ch)) return true;
  return /^@(Left|Right)(Hand|Foot)(\.w|\.p)?$/.test(ch) || /^@(Left|Right)Hand\.on(\.w|\.f|\.n)?$/.test(ch);
};

const clips = {};
for (const f of fs.existsSync(dir) ? fs.readdirSync(dir).filter(x => x.endsWith('.txt')) : []) {
  const name = f.slice(0, -4);
  if (name.includes('.')) say(f, 'a dot in a clip\'s name: the dot divides a clip from its part (shield_kneel.lifts)');
  const c = clips[name] = { parts: new Set() };
  const channels = new Set(), times = new Set();
  let length = 0, eased = false;
  const parts = [], rows = [];
  for (const raw of fs.readFileSync(path.join(dir, f), 'utf8').split(/\r?\n/)) {
    const l = raw.trim();
    if (!l || l.startsWith('#')) continue;
    const w = l.split(/\s+/);
    if (w[0] === 'length') length = +w[1];
    else if (w[0] === 'curve') eased = w[1] === 'ease';
    else if (w[0] === 'part') parts.push({ name: w[1], from: +w[2], to: +w[3], how: w[4] || 'hold' });
    else if (w[0] === 'mark') rows.push({ t: +w[1], what: 'mark ' + w[2] });
    else if (!isNaN(+w[0]) && w.length >= 5) { channels.add(w[1]); times.add(+w[0]); rows.push({ t: +w[0], what: w[1] }); if (w.slice(2, 5).some(v => isNaN(+v))) say(f, l + ': not a number'); }
    else if (!['clip', 'loop', 'pivot', 'base', 'mode', 'drive', 'event', 'end', 'feet', 'fade'].includes(w[0])) say(f, 'do not know the line «' + l + '»');
  }
  if (!eased) say(f, 'no «curve ease»: what a key leaves out would be zero');
  if (!(length > 0)) say(f, 'no length');
  for (const ch of channels) if (!known(ch)) say(f, '«' + ch + '» is no bone or channel: it would move nothing, silently');
  if (channels.size > MAX_CHANNELS) say(f, channels.size + ' channels, the game keeps ' + MAX_CHANNELS + ': the rest would be left out');
  if (times.size > MAX_KEYS) say(f, times.size + ' key times, the game keeps ' + MAX_KEYS);
  for (const r of rows) if (r.t > length + 1e-6) say(f, r.what + ' at ' + r.t + ' is past the length ' + length);
  // a target with no weight comes in at once and jumps; a grip by bone angles does not hold (ANIMATION.md)
  for (const s of ['Left', 'Right']) {
    const h = '@' + s + 'Hand';
    if (channels.has(h) && !channels.has(h + '.w')) say(f, h + ' has no ' + h + '.w: the hand would jump to it');
    if (channels.has(h + '.on')) {
      if (!channels.has(h + '.on.w')) say(f, h + '.on has no ' + h + '.on.w: the hand would jump to the thing');
      if (!channels.has(h + '.on.f') || !channels.has(h + '.on.n')) say(f, h + '.on needs ' + h + '.on.f and ' + h + '.on.n: without them the hand keeps the game\'s pose on the thing');
    }
  }
  if (channels.has('@Prop') && !channels.has('@Prop.w')) say(f, '@Prop has no @Prop.w: the thing would leave where it lay at once');
  if (channels.has('@Look') && !channels.has('@Look.w')) say(f, '@Look has no @Look.w: the head would snap to it');
  let end = null;
  for (const p of parts) {
    c.parts.add(p.name);
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
for (const f of fs.existsSync(scenes) ? fs.readdirSync(scenes).filter(x => x.endsWith('.txt')) : []) {
  fs.readFileSync(path.join(scenes, f), 'utf8').split(/\r?\n/).forEach((raw, i) => {
    const m = /^\s*(?:if [^:]*:\s*)?(?:player clip|npc \S+ clip)\s+(\S+)/.exec(raw);
    if (!m || m[1] === 'none') return;
    named++;
    const [clip, part] = m[1].split('.');
    if (clips[clip]) { if (part && !clips[clip].parts.has(part)) say(f + ':' + (i + 1), 'clip ' + clip + ' has no part «' + part + '»'); }
    else if (!builtin.has(m[1])) say(f + ':' + (i + 1), 'no clip «' + m[1] + '» in assets/story/clips or the built-in ones: it would play only where a config has it');
  });
}

if (problems.length) { for (const p of problems) console.log(p); console.log('clipcheck: ' + problems.length + ' problem(s)'); process.exit(1); }
console.log('clipcheck: ok (' + Object.keys(clips).length + ' clip file(s), ' + named + ' named by scenes)');

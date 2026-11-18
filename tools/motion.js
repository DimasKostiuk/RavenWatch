// A clip written by movements, not by keys: the «do» half of tools/anim.js (review 3, 2026-11-11). The user's aim: a few words give a living
// clip, and a weaker model writes it without holding eighty numbers in its head. A clip made this way is a short script in its own head —
// «# do <t0>-<t1> <movement> …» lines — and the keys of its «# scripted:» channels are made from them again on every change:
//   reach left|right|both <place>                                … to a place on the body by its name — nape, brow, eyes, chin, belt — the hand, its turn
//                                                                and its elbow as a clip the user has seen holds them (PLACES)
//   reach left|right|both <x y z> [palm <dir>] [fingers <dir>]   the hand goes there and stays: a pull back before it, an arc on the way (not a
//                                                                straight rail), its weight taken up softly, fingers and palm said (dir: forward back
//                                                                up down left right in out; none said — a hand already out stays turned as it
//                                                                was; from rest: fingers along the forearm, the palm down). [elbow down|out|up|back|in]:
//                                                                which side the elbow points (none said: as it was, else the first the arm takes)
//   rest left|right|both                                         the hand let go, back to where it hangs — no faster than Back s a metre
//   look <x y z> | left|right|up|down <deg> […] | ahead          the head: a point, a way (from straight ahead), or let go
//   nod [deg] [times] · shake [deg] [times]                      the head's yes and no (through the look, on top of where it looks)
//   lean forward|back|left|right <deg> | none                    the trunk, shared over Spine1 and Spine2 (BodyCheck's own limits: past them it says so)
//   turn left|right <deg> | none                                 the chest turned
//   sink <m>                                                     the hips that much lower (0: up again), the feet staying where they stand
//   weight left|right|centre [m]                                 the weight over that foot
//   step left|right <dx> <dz>                                    that foot steps: the weight goes to the other one first, the foot rises, comes down
//                                                                from above (never slides in), the body follows half the way
//   sigh                                                         a breath in and out: the chest up, then down a little lower than it was
//   fingers left|right|both fist|flat|point|relaxed              the fingers (the numbers of fist_palm, door_push, point_at — the same sign on both hands)
//   grip left|right|both <face> [fingers <dir>]                  the hand laid on the thing the clip moves (its row's «реквізит», «@Prop» keyed by hand): the palm on
//                                                                that face of it — left right top bottom front back, the thing as it lies square before the body —, the
//                                                                fingers that way along it (down a side face, forward over the top). The wrist's point, the hand's turn
//                                                                and every finger's bend are reckoned (tools/grip.js) from the hand and the thing as a take measured
//                                                                them; not measured yet: a note says what to take first. release left|right|both: off it again
// Whole examples (the user's words → want → do):
//   «махає правою рукою на прощання»:
//     node tools/anim.js new wave_bye --length 4.6 "махає правою рукою на прощання"
//     node tools/anim.js header wave_bye want 1.2-2.3 palm right forward
//     node tools/anim.js do wave_bye "0.4-1.2 reach right 0.5 1.75 0.25 palm forward fingers up; 0.5-1.1 fingers right flat; 1.25-1.6 reach right 0.36 1.8 0.25 palm forward fingers up; 1.6-1.95 reach right 0.58 1.72 0.25 palm forward fingers up; 2.2-3 nod; 3.1-4.1 rest right; 3.2-4 fingers right relaxed"
//   «показує лівою рукою вбік і дивиться туди»:
//     node tools/anim.js new point_side --length 4 "показує лівою рукою вбік і дивиться туди"
//     node tools/anim.js header point_side want 1-2.8 finger left index straight
//     node tools/anim.js do point_side "0.3-1 reach left -0.74 1.42 0.15 palm down fingers left; 0.4-1 fingers left point; 0.2-0.9 look left 60; 2.8-3.6 rest left; 2.8-3.4 fingers left relaxed; 2.9-3.7 look ahead"
// and the rest of the body is reckoned from what the hands do, by one pass (accompany, «# alive <0..2>», 1 when a script is there): the chest turns
// and bends after the hands a moment BEFORE them and settles with a little overshoot (a spring), the weight goes over, the knees give at a fast
// move, the head looks at what a hand reaches for (when the script says no look of its own), the other hand answers. Its sizes stay within what
// the clips the user accepted use (trunk 6–19° a bone, weight 3–5 cm, look 0.5–0.6: measured 2026-11-11 on 14 of them); the law itself is not
// fitted to them (they follow none: the fit left 80% over) — it is the user's eye that tunes «alive <clip> <amount>».
// A channel the author keys by hand (set, move, hold, del, copy) is his from then on, with the channels that only mean something with it (a hand's target
// with its weight, pole, fingers and palm; the look with its weight; both spine bones — clip.js family): the script leaves them («auto <clip> <channel>»
// gives them back). One movement of a hand, of the head, of a foot at a time: one that begins before the last one of it ended is refused, not blended.
// The arm itself is in tools/body.js (its head says what is measured and how near): the shoulder carried by the trunk, the elbow by the game's own solve,
// the wrist's bend, the trunk's column. regen asks it for every hand movement: of the ways a hand can go (straight with a small arc, then wider and wider
// round), of the sides its elbow can point (where the script says none) and of how long its weight is held on the way back, the first is kept on which
// no limit of BodyCheck is passed (ways, choose); the hand's turn on the way follows the forearm wherever the turn asked would bend the wrist too far
// (turnHand). So no way round the head, no elbow's side, no middle turn is a rule of thumb here: each is what the arm allows.
// Whose body: the one its row in docs/CLIPS.md names (a person's, Heid's, the game's Dvergr — tools/body.js BODIES). Its stance, its eyes, its arm and
// the heights the accompaniment goes by are that body's. On the dvergr the named places of the head are derived, not poses it has stood in (a note
// says so each time one is used); everything else is said and reckoned as on a person.
// The accompaniment and the model are a STANDING body's (standing: no «body»/«Hips» keys, its row in docs/CLIPS.md begins «стійка»).
// predict: what the keys alone already say before a take — a hand faster than BodyCheck.HandFast, a weight taken or let go in a snap, a way back faster
// than Back, and by the model: [miss] a target past the arm, [fold] the elbow folded, [bend] the wrist bent, [inside] a hand or forearm in the trunk,
// [jerk] the arm's bones swinging too fast. Limits are read from src/BodyCheck.cs, never copied here.
// mirror: the same clip for the other side — targets, directions, body-frame and aim bones, fingers' bend; a bone in its own axes or a thing: refused.
const fs = require('fs'), path = require('path'), body = require('./body.js');
const root = path.join(__dirname, '..'), { eased, limit } = body;
const num = s => { if (!/^-?(\d+\.?\d*|\.\d+)$/.test(String(s))) throw new Error('not a number: «' + s + '»'); return parseFloat(s); };
const r3 = v => Math.round(v * 1000) / 1000;
const add = (a, b) => a.map((x, i) => x + b[i]), sub = (a, b) => a.map((x, i) => x - b[i]), mul = (a, k) => a.map(x => x * k);
const len = a => Math.hypot(...a), dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const unit = a => { const l = len(a); return l < 1e-6 ? [0, 0, 0] : mul(a, 1 / l); };
const mix = (a, b, k) => a.map((x, i) => x + (b[i] - x) * k);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const rotY = (v, deg) => { const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r); return [v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c]; }; // (+: to the right)
const rotX = (v, deg) => { const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r); return [v[0], v[1] * c - v[2] * s, v[1] * s + v[2] * c]; }; // (+: down)

// the body's own numbers (shoulders, where a hand hangs, the arm's lengths, the feet of its stance, where its eyes are, the heights the accompaniment
// goes by) are tools/body.js's, by the clip's body — a person's, Heid's, a dvergr's: none of them is a constant here
const aheadOf = B => [0, B.eyes[1], 2];                                       // where the head looks when nothing is said: straight ahead of its eyes
const RestFingers = [0, -1, 0.1];                                             // where a hanging hand's fingers go (wave_off's keys at rest; its palm: to the body)
const SIGN = { Left: -1, Right: 1 };
// the script's own measures (seconds, metres, degrees)
const Room = 0.08, Settle = 0.3;                                              // the share of each limit of BodyCheck a chosen way keeps free (a take's body is not the model's to the degree: it reads the wrist 1.4–2.8° low) · s after a movement its way is still judged (the core's spring overshoots)
const RampMin = 0.3, Back = 0.5, LookW = 0.6, NodW = 0.7, LookIn = 0.4, StepUp = 0.12, StepMost = 0.5, StepBefore = 0.3, StepAfter = 0.35, WeightMost = 0.12, LeanShare = 1.7; // (LeanShare: spine degrees a degree of lean — BodyCheck.WantFix, basket_set 2026-11-10; aside it is unmeasured)
const FINGERS = { // (x bends: + to the palm, the same on both hands — fist_palm, door_push, point_at)
  relaxed: {},
  flat: { Index1: -25, Index2: -25, Middle1: -25, Middle2: -25, Ring1: -25, Ring2: -25, Pinky1: -25, Pinky2: -25 },
  fist: { Index1: 70, Index2: 70, Middle1: 70, Middle2: 70, Ring1: 70, Ring2: 70, Pinky1: 70, Pinky2: 70 },
  point: { Index1: -11, Index2: -30, Index3: -30, Middle1: 78, Middle2: 85, Ring1: 65, Ring2: 70, Pinky1: 70 },
};
// the left hand's own numbers where they differ: the game holds its fingers otherwise. Measured 2026-11-11 on zz_move by «want … finger left index straight»
// (BodyCheck.FingerBend: the three joints' sum off the bind pose): the right hand's numbers read 38° on the left; a joint changed a take gave each joint's
// own straight: -37, -40, -32
// a body's own shapes where its hand is not a person's (the dvergr's fingers bend the other way: «-25» closed his fist, 2026-11-13). Measured, never
// guessed: «node tools/anim.js grip presets» on a kept hand (tools/grip_hands.json) — dvergr.left in dv_show_lot take 2, 2026-11-16 (flat bends 0°,
// fist 80°/90°; point = the index flat, the rest a fist, as the presets say); dvergr.right in dv_count take 2 the same day. A side not here is not
// measured: «fingers» on it is refused, not guessed
const FINGERS_BODY = {
  dvergr: {
    Left: {
      relaxed: {},
      flat: { Index1: 29, Index2: 44, Middle1: 39, Middle2: 55, Ring1: 50, Ring2: 55, Pinky1: 66, Pinky2: 49 },
      fist: { Index1: -51, Index2: -46, Middle1: -41, Middle2: -35, Ring1: -30, Ring2: -35, Pinky1: -14, Pinky2: -41 },
      point: { Index1: 29, Index2: 44, Middle1: -41, Middle2: -35, Ring1: -30, Ring2: -35, Pinky1: -14, Pinky2: -41 },
    },
    Right: { // (dvergr.right in dv_count take 2, 2026-11-16)
      relaxed: {},
      flat: { Index1: 8, Index2: 45, Middle1: 27, Middle2: 47, Ring1: 37, Ring2: 53, Pinky1: 31, Pinky2: 60 },
      fist: { Index1: -72, Index2: -45, Middle1: -53, Middle2: -43, Ring1: -43, Ring2: -37, Pinky1: -48, Pinky2: -30 },
      point: { Index1: 8, Index2: 45, Middle1: -53, Middle2: -43, Ring1: -43, Ring2: -37, Pinky1: -48, Pinky2: -30 },
    },
  },
};
const FINGERS_LEFT = { point: { Index1: -37, Index2: -40, Index3: -32 }, flat: { Index1: -45, Index2: -45, Middle1: -45, Middle2: -45, Ring1: -45, Ring2: -45, Pinky1: -45, Pinky2: -45 } }; // (flat: a first step past the right hand's -25, which read a fist of 61° on the left in basket_drop — to be measured as the point was)
// Places on the body by name: where the hand is, how it is turned and where its elbow points — each taken whole from a clip that holds it (the right
// hand's numbers; the left: mirrored). A point of the head guessed in metres sits inside the head, and the arm folds to reach it; these are poses a
// body has stood in. The way there and back is not kept here: the arm's model chooses it (ways). nape: scratch_head · brow: wipe_brow · eyes:
// shade_eyes · chin: think_chin · belt: hand_belt
const PLACES = {
  nape: { p: [0.13, 1.66, 0.06], f: [-0.42, 0.88, 0.2], n: [-1, 0, 0], e: [1, 0, 0.1] },
  brow: { p: [0.17, 1.72, 0.3], f: [-0.95, -0.3, 0], n: [0, 0, -1], e: [1, -0.6, 0] },
  eyes: { p: [0.04, 1.8, 0.22], f: [-0.9, 0, 0.44], n: [0, -1, 0], e: [1, 0.3, 0] },
  chin: { p: [0.03, 1.4, 0.15], f: [0, 1, 0.2], n: [-0.6, 0, -0.8], e: [0.2, -1, 0.5] },
  belt: { p: [0.23, 1.12, 0.03], f: [0, -0.5, 0.85], n: [-1, 0, 0], e: [1, 0, -0.4] },
};
// … and on a body that is not a person's. The dvergr's belt and chin are its own clips', seen and accepted by the user (dv_hand_belt 2026-11-13, dv_think_chin
// 2026-11-17). The other three places of the head are DERIVED, not poses it has stood in: a person's place moved with the head's ball and
// as far from its middle as the dvergr's ball is wider (tools/body.js, «head» — a ball the game guesses for this body) — so the hand comes to the
// surface the game judges [head] by, which may not be the head one sees. The script says so in a note each time one is used: the take judges it
const PLACES_OF = { dvergr: {
  belt: { p: [0.22, 0.73, -0.03], f: [0, -0.5, 0.85], n: [-1, 0, 0], e: [1, 0, -0.4] },
  chin: { p: [0.05, 1.0, 0.27], f: [0, 1, 0.2], n: [-0.6, 0, -0.8], e: [0.8, -0.6, 0] }, // (dv_think_chin, dv_listen_chin, accepted 2026-11-17: the derived chin went into his chest)
} };
function placeOf(B, name) {
  const own = (PLACES_OF[B.name] || {})[name], man = PLACES[name];
  if (own || !man || !PLACES_OF[B.name]) return own || man; // (a person's, Heid's: the places as they are)
  const from = body.BODIES.player.head, k = B.head.r / from.r;
  return Object.assign({}, man, { p: add(B.head.c, mul(sub(man.p, from.c), k)).map(r3), note: '«' + name + '» on the ' + B.name + ' is DERIVED from a person\'s (moved with the head\'s ball, which the game guesses for this body), not a pose it has stood in: look at the take, then say the point itself (reach <side> <x y z>)' });
}
const sided = (v, s) => [v[0] * SIGN[s], v[1], v[2]];
const ELBOW = { down: [0.8, -0.6, 0], out: [1, 0, 0], up: [0.8, 0.5, 0.2], back: [0.5, -0.3, -0.8], in: [-0.3, -0.9, 0.2] }; // (x for the right arm: mirrored for the left; in this order the model tries them where the script says none)
const TRUNK = ['lean', 'turn', 'sink', 'weight', 'step', 'sigh'];             // the movements of the trunk: made first, so a hand's way is chosen on the body as it will be leant and lowered
const DIRS = { forward: [0, 0, 1], back: [0, 0, -1], up: [0, 1, 0], down: [0, -1, 0], left: [-1, 0, 0], right: [1, 0, 0] };
const VERBS = ['reach', 'rest', 'look', 'nod', 'shake', 'lean', 'turn', 'sink', 'weight', 'step', 'sigh', 'fingers', 'grip', 'release'];

// ---------------------------------------------------------------- the clip's head: its script and what the script owns
const headWord = (c, word) => { for (const h of c.head) { const w = h.trim().split(/\s+/); if (w[0] === word) return w.slice(1); } return null; };
const lengthOf = c => { const l = headWord(c, 'length'); if (!l) throw new Error('the clip has no length'); return parseFloat(l[0]); };
const scriptOf = c => c.head.map(h => h.trim()).filter(h => h.startsWith('# do ')).map(h => h.slice(5).trim());
const listOf = (c, tag) => { const h = c.head.map(x => x.trim()).find(x => x.startsWith('# ' + tag + ':')); return h ? h.slice(tag.length + 3).trim().split(/\s+/).filter(Boolean) : []; };
function setLine(c, start, line) { // a «# …» line of the head set, or taken out (line null)
  const i = c.head.findIndex(h => h.trim().startsWith(start));
  if (i >= 0) { if (line === null) c.head.splice(i, 1); else c.head[i] = line; }
  else if (line !== null) { let at = c.head.length; while (at > 0 && c.head[at - 1].trim() === '') at--; c.head.splice(at, 0, line); }
}
const aliveOf = c => { const h = c.head.map(x => x.trim()).find(x => x.startsWith('# alive ')); return h ? parseFloat(h.split(/\s+/)[2]) : (scriptOf(c).length ? 1 : 0); };
function setScript(c, lines) {
  c.head = c.head.filter(h => !h.trim().startsWith('# do '));
  let at = c.head.length; while (at > 0 && c.head[at - 1].trim() === '') at--;
  c.head.splice(at, 0, ...lines.map(l => '# do ' + l));
}
const hasMode = (c, bone, mode) => c.head.some(h => h.trim().replace(/\s+/g, ' ') === 'mode ' + bone + ' ' + mode);

function parseLine(line, length) {
  const w = line.trim().split(/\s+/), m = /^(\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)$/.exec(w[0] || '');
  if (!m) throw new Error('«' + line + '»: a movement begins with its time, <t0>-<t1> (seconds): 0.4-1.2 reach right 0.3 1.3 0.4');
  const t0 = +m[1], t1 = +m[2];
  if (!(t1 > t0)) throw new Error('«' + line + '»: it ends before it begins');
  if (t1 > length + 1e-6) throw new Error('«' + line + '»: past the clip\'s length ' + length + ' (node tools/anim.js header <clip> length ' + r3(t1) + ')');
  if (!VERBS.includes(w[1])) throw new Error('«' + line + '»: do not know «' + (w[1] || '') + '» — ' + VERBS.join(', '));
  return { t0, t1, verb: w[1], a: w.slice(2), line };
}
const sides = word => { if (word === 'left') return ['Left']; if (word === 'right') return ['Right']; if (word === 'both') return ['Left', 'Right']; throw new Error('left, right or both — not «' + word + '»'); };
function dirOf(word, side) {
  if (word === 'in') return [-SIGN[side], 0, 0];
  if (word === 'out') return [SIGN[side], 0, 0];
  if (!DIRS[word]) throw new Error('a way is forward, back, up, down, left, right, in or out — not «' + word + '»');
  return DIRS[word];
}

// ---------------------------------------------------------------- tracks: a value that the movements set one after another, and bumps on top
function Track(zero) { this.keys = [{ t: 0, v: zero }]; this.bumps = []; }
Track.prototype.raw = function (t) {
  const k = this.keys;
  if (t <= k[0].t) return k[0].v;
  for (let i = 1; i < k.length; i++) if (t <= k[i].t) { const u = (t - k[i - 1].t) / Math.max(1e-6, k[i].t - k[i - 1].t), s = u * u * (3 - 2 * u); return k[i - 1].v + (k[i].v - k[i - 1].v) * s; } // (eased, as the game's curve between two keys)
  return k[k.length - 1].v;
};
Track.prototype.at = function (t) {
  let v = this.raw(t);
  for (const b of this.bumps) if (t > b[0].t && t < b[b.length - 1].t) for (let i = 1; i < b.length; i++) if (t <= b[i].t) { const u = (t - b[i - 1].t) / (b[i].t - b[i - 1].t), s = u * u * (3 - 2 * u); v += b[i - 1].v + (b[i].v - b[i - 1].v) * s; break; }
  return v;
};
Track.prototype.to = function (t0, t1, v) { const was = this.raw(t0); this.keys = this.keys.filter(k => k.t < t0 - 1e-6); this.keys.push({ t: t0, v: was }, { t: t1, v }); };
Track.prototype.bump = function (pts) { this.bumps.push(pts.map(p => ({ t: p[0], v: p[1] }))); };
Track.prototype.times = function () { const s = new Set(this.keys.map(k => r3(k.t))); for (const b of this.bumps) for (const p of b) s.add(r3(p.t)); return [...s]; };
Track.prototype.used = function () { return this.keys.some(k => Math.abs(k.v) > 1e-9) || this.bumps.length > 0; };

// ---------------------------------------------------------------- the script made into keys
function regen(c) {
  const length = lengthOf(c), notes = [], lines = scriptOf(c).map(l => parseLine(l, length)).sort((a, b) => a.t0 - b.t0);
  const alive = aliveOf(c), was = new Set(listOf(c, 'scripted'));
  for (const ch of was) { delete c.keys[ch]; c.order = c.order.filter(x => x !== ch); } // (what the script made last time: made again)
  const mine = new Set(), skipped = new Set();
  const free = ch => { if (mine.has(ch)) return true; if (c.keys[ch] && c.keys[ch].length) { skipped.add(ch); return false; } return true; }; // (keys the author put there: his)
  const K = (ch, t, v) => {
    if (!free(ch)) return;
    t = r3(clamp(t, 0, length));
    if (!c.keys[ch]) { c.keys[ch] = []; if (!c.order.includes(ch)) c.order.push(ch); }
    mine.add(ch);
    const k = c.keys[ch].find(k => Math.abs(k.t - t) < 0.0005);
    const val = [0, 1, 2].map(i => r3(v[i] || 0));
    if (k) k.v = val; else c.keys[ch].push({ t, v: val });
  };
  const cut = (ch, t) => { if (mine.has(ch) && c.keys[ch]) c.keys[ch] = c.keys[ch].filter(k => k.t <= t + 1e-6); }; // (a later movement takes the channel over from its start)
  const lastOf = (ch, def) => { const k = c.keys[ch]; if (!k || !k.length) return def; return k.reduce((a, b) => b.t > a.t ? b : a).v; };
  const T = { pitch: new Track(0), yaw: new Track(0), roll: new Track(0), mx: new Track(0), my: new Track(0), mz: new Track(0) };
  const B = body.bodyOf(c), REST = B.rest, SHOULDER = B.shoulder, STANCE = B.stance, HEAD = B.eyes, AHEAD = aheadOf(B);
  const hand = { Left: { out: false, at: REST.Left }, Right: { out: false, at: REST.Right } };
  const foot = { Left: STANCE.Left.slice(), Right: STANCE.Right.slice() };
  const look = { on: false, at: AHEAD, said: false };
  const reaches = []; // (for the look of the accompaniment)
  const wasFeet = was.has('@LeftFoot') || was.has('@RightFoot'), wasLook = was.has('@Look');
  const needFeet = () => { for (const s of ['Left', 'Right']) if (free('@' + s + 'Foot') && !(c.keys['@' + s + 'Foot'] || []).length) K('@' + s + 'Foot', 0, STANCE[s]); };
  if (wasFeet) needFeet();
  if (wasLook) { K('@Look', 0, AHEAD); K('@Look.w', 0, [0, 0, 0]); }
  const lookW = (t, w) => K('@Look.w', t, [w, 0, 0]);
  // a channel's value at t by the keys it has now (straight between them)
  const keyAt = (ch, t, def) => { if (!c.keys[ch] || !c.keys[ch].length) return def; c.keys[ch].sort((a, b) => a.t - b.t); return require('./clip.js').valueAt(c, ch, t).v; }; // (clip.valueAt reads keys in time order: put so first)
  // one movement of a hand, of the head, of a foot at a time (the review 2026-11-11: two that overlapped were both kept, and the keys were of neither —
  // a hand that never left rest, a rest cut to half a second)
  const busy = {};
  const takes = (L, key, what) => { const b = busy[key]; if (b && L.t0 < b.till - 1e-6) throw new Error('«' + L.line + '»: it begins while «' + b.line + '» still moves ' + what + ' (till ' + r3(b.till) + ' s) — one movement of it at a time: begin at ' + r3(b.till) + ' or later, or change that line (redo)'); busy[key] = { till: L.t1, line: L.line }; };
  // the head taken to a point: from where it looks and with the weight it has at that moment (a look let go a moment ago, a nod fading), nothing later left over
  const lookTo = (t0, t1, p) => {
    const w = keyAt('@Look.w', t0, [0])[0], from = keyAt('@Look', t0, AHEAD);
    cut('@Look', t0); cut('@Look.w', t0);
    if (w < 0.05) K('@Look', t0, p); else { K('@Look', t0, from); K('@Look', Math.min(t1, t0 + LookIn + 0.1), p); }
    lookW(t0, w); lookW(Math.min(t1, t0 + LookIn), LookW);
    look.on = true; look.at = p;
  };
  // … and let go: its point held till the weight is off (the next look's point would pull the head across on its way out)
  const lookOff = (t0, t1) => { if (!look.on) return; const w = keyAt('@Look.w', t0, [LookW])[0], end = Math.min(t1, t0 + 0.6); cut('@Look', t0); cut('@Look.w', t0); K('@Look', t0, look.at); K('@Look', end, look.at); lookW(t0, w); lookW(end, 0); look.on = false; };

  // the trunk as the script's own movements have it at t (lean, turn, sink, weight, step — made before the hands; what the accompaniment adds comes after,
  // and predict sees the whole): what a hand's way is chosen on. Spine or «move» keyed by the author: his keys
  const authors = ch => !mine.has(ch) && (c.keys[ch] || []).length > 0, keysOf = ch => c.keys[ch].slice().sort((a, b) => a.t - b.t);
  const trunkAt = t => {
    const v = [T.pitch.at(t), T.yaw.at(t), T.roll.at(t)], own = b => authors(b) ? (hasMode(c, b, 'body') ? eased(keysOf(b), t, [0, 0, 0]) : [0, 0, 0]) : null;
    return { s1: own('Spine1') || mul(v, 0.45), s2: own('Spine2') || mul(v, 0.55), mv: authors('move') ? eased(keysOf('move'), t, [0, 0, 0]) : [T.mx.at(t), T.my.at(t), T.mz.at(t)] };
  };
  // The ways a hand can go from a to b, the plainest first: straight with a small arc away from the body (never a rail), then round — wider and wider, out,
  // ahead, between them, low and high; with each, the sides its elbow may point (poles; [null]: said, or not the script's)
  const ways = (s, a, b, poles) => {
    const d = sub(b, a), far = len(d), u = unit(d), vias = [], list = [];
    const square = v => { const q = sub(v, mul(u, dot(v, u))); return len(q) > 0.2 ? unit(q) : null; };
    if (far > 0.15) {
      const out = square([SIGN[s] * 0.6, 0.3, 0.5]);
      vias.push({ rank: 0, via: add(add(a, mul(d, 0.52)), mul(out || [0, 0, 0], Math.min(0.06, far * 0.08))) });
      if (far > 0.3) for (const [rank, size] of [[1, 0.12], [2, 0.2], [3, 0.3]]) for (const dir of [[SIGN[s], 0, 0], [0, 0, 1], [SIGN[s], 0, 1], [SIGN[s], -0.5, 0.5], [0, 1, 0.5]]) { const q = square(dir); if (q) vias.push({ rank, via: add(add(a, mul(d, 0.5)), mul(q, size)) }); }
    } else vias.push({ rank: 0, via: null });
    for (const v of vias) poles.forEach((pole, i) => list.push({ via: v.via, pole, rank: v.rank + i * 1.5 }));
    return list.sort((x, y) => x.rank - y.rank);
  };
  // … and the one kept: each way written in turn (write), the arm of the model swept over the movement and a little after — the first way on which every
  // limit holds with Room to spare, else the one least over (predict then says what is left: the script asks what no arm does)
  const blind = new Set(); // (the arms the model does not cover in this clip: said once each)
  const choose = (s, chs, t0, t1, list, write) => {
    const keep = () => ({ k: JSON.stringify(chs.map(x => c.keys[x] || null)), m: chs.map(x => mine.has(x)), o: c.order.slice() });
    const back = z => { const k = JSON.parse(z.k); chs.forEach((x, i) => { if (k[i]) c.keys[x] = k[i]; else delete c.keys[x]; if (z.m[i]) mine.add(x); else mine.delete(x); }); c.order = z.o.slice(); };
    const from = keep(); let top = null;
    for (const way of list) {
      back(from); write(way);
      const sw = body.sweep(c, s, t0, Math.min(length, t1 + Settle), trunkAt), cost = body.over(sw, Room);
      if (!sw && !blind.has(s)) { blind.add(s); notes.push('the ' + s.toLowerCase() + ' arm is not the model\'s in this clip (its body does not stand by its keys — «body», «Hips» —, or the hand\'s target rides a bone): its way, its elbow and its turn on the way are the first tried, not chosen — nothing is foretold of it; the take says [fold], [bend], [inside], [head]'); }
      if (!top || cost < top.cost - 1e-6) top = { cost, made: keep() };
      if (cost < 1e-6) break;
    }
    back(top.made);
  };
  // A hand's fingers and palm over a movement: from how they are at its start (a hand at rest: as it hangs — fingers down, the palm to the body) to how they
  // are asked at its end; where the script says neither, the fingers end along the forearm (a straight wrist). On the way the hand is the forearm's: where
  // the turn between two keys would bend the wrist past BodyCheck.WristBend (less Room), or the two are over a third of a turn apart (no steady way between
  // them: the hand flips), a key is put there — the fingers turned toward the forearm's way by as much as it takes (body.unbent). «soft»: the end's own turn
  // is a place's, not the script's words — it too gives way to the wrist. (The takes that asked for this, zz_greet and zz_w_scratch 2026-11-11: «fingers up»
  // from the first moment bent the wrist 140° on the way up; a middle way along the hand's path left 102° and a flip of 2794 deg/s on the way down.)
  const turnHand = (ch, s, t0, t1, wasOut, P, fSaid, nSaid, soft) => {
    const most = limit('WristBend', 80) * (1 - Room), squared = (n, f) => sub(n, mul(f, dot(n, f)));
    const f0 = wasOut ? lastOf(ch + '.f', RestFingers) : RestFingers, n0 = wasOut ? lastOf(ch + '.n', [-SIGN[s], 0, 0]) : [-SIGN[s], 0, 0];
    cut(ch + '.f', t0); cut(ch + '.n', t0);
    K(ch + '.f', t0, f0); K(ch + '.n', t0, n0);
    const arm = () => body.sweep(c, s, t0, t1, trunkAt) || [], end = arm().pop(), fore = end ? unit(end.fore) : unit(sub(P, SHOULDER[s])); // (a body the model does not stand for: the arm's own line)
    let f1 = fSaid ? unit(fSaid) : fore, n1 = nSaid ? unit(nSaid) : null;
    if (!n1) for (const cand of [DIRS.down, [-SIGN[s], 0, 0], DIRS.forward]) if (Math.abs(dot(f1, cand)) < 0.75) { n1 = cand; break; }
    if (Math.abs(dot(f1, n1)) > 0.9) f1 = unit(sub(f1, mul(n1, dot(f1, n1) * 0.9))); // (fingers no one said, along the palm's own way: turned off it)
    if ((soft || !fSaid) && end && body.wristBend(f1, n1, fore, s, B) > most) f1 = body.unbent(f1, n1, fore, s, most - 3, B);
    n1 = unit(squared(n1, f1));
    K(ch + '.f', t1, f1); K(ch + '.n', t1, n1);
    for (let i = 0; i < 4; i++) {
      const sw = arm(), fk = keysOf(ch + '.f').filter(k => k.t >= t0 - 1e-6 && k.t <= t1 + 1e-6), near = t => fk.some(k => Math.abs(k.t - t) < 0.07);
      if (!sw.length) break;
      let at = null, flips = false;
      for (let j = 1; j < fk.length && !at; j++) if (dot(unit(fk[j - 1].v), unit(fk[j].v)) < -0.5 && fk[j].t - fk[j - 1].t > 0.15) { const mid = (fk[j - 1].t + fk[j].t) / 2; at = sw.reduce((a, b) => Math.abs(b.t - mid) < Math.abs(a.t - mid) ? b : a); flips = true; }
      if (!at) for (const x of sw) if (x.w >= 0.5 && x.turned !== null && x.turned > most && !near(x.t) && (!at || x.turned > at.turned)) at = x;
      if (!at) break;
      const put = x => { // (the hand's turn at the sample x: along the forearm for a flip, else as the keys have it there — then no more bent than the wrist gives)
        const way = unit(x.fore);
        let f = flips ? way : unit(eased(fk, x.t, f1)), n = squared(eased(keysOf(ch + '.n'), x.t, n1), f);
        if (len(n) < 0.3) n = squared(n1, f);
        if (len(n) < 0.3) n = squared(n0, f);
        if (len(n) < 0.3) return false;
        f = body.unbent(f, n, way, s, most - 3, B);
        K(ch + '.f', x.t, f); K(ch + '.n', x.t, unit(squared(n, f)));
        return true;
      };
      if (!flips) { if (!put(at)) break; continue; }
      // (a flip's middle key: at the moment that leaves the hand turning slowest over the whole movement — in the middle of the time is not in the middle of the turn)
      const fWas = JSON.stringify(c.keys[ch + '.f']), nWas = JSON.stringify(c.keys[ch + '.n']); let best = null;
      for (const x of sw) {
        if (near(x.t)) continue;
        c.keys[ch + '.f'] = JSON.parse(fWas); c.keys[ch + '.n'] = JSON.parse(nWas);
        if (!put(x)) continue;
        const peak = Math.max(...arm().map(q => q.spin));
        if (!best || peak < best.peak) best = { peak, x };
      }
      c.keys[ch + '.f'] = JSON.parse(fWas); c.keys[ch + '.n'] = JSON.parse(nWas);
      if (!best || !put(best.x)) break;
    }
  };

  for (const L of [...lines.filter(l => TRUNK.includes(l.verb)), ...lines.filter(l => !TRUNK.includes(l.verb))]) {
    const { t0, t1, a } = L, span = t1 - t0, bad = why => { throw new Error('«' + L.line + '»: ' + why); };
    switch (L.verb) {
      case 'reach': {
        const place = placeOf(B, a[1]);
        if (!place && (a.length < 4 || !/^-?[0-9.]/.test(a[1]))) bad('reach left|right|both <x y z> | ' + Object.keys(PLACES).join('|') + ' [palm <dir>] [fingers <dir>] [elbow <way>] — a place on the body by its name, or a point in metres');
        const P0 = place ? place.p : [num(a[1]), num(a[2]), num(a[3])], pi = a.indexOf('palm'), fi = a.indexOf('fingers'), ei = a.indexOf('elbow');
        if (ei >= 0 && !ELBOW[a[ei + 1]]) bad('elbow ' + Object.keys(ELBOW).join('|') + ' — which way the elbow points (none said: the first of these the arm takes without passing a limit)');
        for (const s of sides(a[0])) {
          const ch = '@' + s + 'Hand', P = place ? sided(place.p, s) : a[0] === 'both' ? [Math.abs(P0[0]) * SIGN[s], P0[1], P0[2]] : P0, h = hand[s];
          if (!free(ch)) { notes.push(ch + ' is keyed by hand: «' + L.line + '» does not write it (node tools/anim.js auto ' + c.name + ' ' + ch + ' gives it back to the script)'); continue; }
          takes(L, ch, 'the ' + s.toLowerCase() + ' hand');
          if (place && place.note && !notes.includes(place.note)) notes.push(place.note);
          const start = h.at, d = sub(P, start), far = len(d), wasOut = h.out;
          if (!wasOut && span < RampMin + 0.05) bad('a hand taken from rest in ' + r3(span) + ' s is a snap (the take would say [fast]): give it at least ' + (RampMin + 0.05) + ' s');
          // fingers and palm: always said (a palm no one said is a palm no one judges — clipcheck); the elbow's side: said, a place's, or the one the arm takes best
          // (a hand already out, nothing said of it: it stays turned as it was, its elbow where it was — a small move beside, a scratch, is the same hand)
          const keeps = wasOut && !place && fi < 0 && pi < 0 && (c.keys[ch + '.f'] || []).length > 0 && (c.keys[ch + '.n'] || []).length > 0;
          const fSaid = fi >= 0 ? dirOf(a[fi + 1], s) : place ? unit(sided(place.f, s)) : keeps ? unit(lastOf(ch + '.f')) : null, nSaid = pi >= 0 ? dirOf(a[pi + 1], s) : place ? unit(sided(place.n, s)) : keeps ? unit(lastOf(ch + '.n')) : null;
          if (fi >= 0 && nSaid && Math.abs(dot(fSaid, nSaid)) > 0.9) bad( // (a palm nobody said is chosen square to the fingers: turnHand)
            'fingers ' + a[fi + 1] + ' and palm ' + (pi >= 0 ? a[pi + 1] : 'down') + ' are one line: a hand is said by two ways square to each other — palm down: fingers forward|back|left|right; palm forward: fingers up|down|left|right');
          const poleFree = free(ch + '.p'), poleWas = lastOf(ch + '.p', sided(ELBOW.down, s)), eSaid = ei >= 0 ? ELBOW[a[ei + 1]] : place ? place.e : null;
          const poles = !poleFree ? [null] : eSaid ? [sided(eSaid, s)] : [...(wasOut ? [poleWas] : []), ...Object.values(ELBOW).map(e => sided(e, s))];
          choose(s, [ch, ch + '.w', ch + '.p', ch + '.f', ch + '.n'], t0, t1, ways(s, start, P, poles), way => {
            cut(ch, t0); cut(ch + '.w', t0);
            K(ch, t0, start);
            if (!wasOut) { K(ch + '.w', t0, [0, 0, 0]); K(ch + '.w', t0 + Math.max(RampMin, span * 0.6), [1, 0, 0]); }
            else if (far > 0.25 && span >= 0.5) { const back = add(sub(start, mul(unit(d), Math.min(0.04, far * 0.1))), [0, -0.01, 0]); back[1] = Math.max(back[1], Math.min(start[1], P[1])); K(ch, t0 + span * 0.18, back); } // (the pull back before the move — never under the lower end of the move: not into the ground)
            if (way.via) K(ch, t0 + span * 0.5, way.via);
            K(ch, t1, P);
            if (way.pole) { cut(ch + '.p', t0); K(ch + '.p', t0, poleWas); K(ch + '.p', t1, way.pole); }
            turnHand(ch, s, t0, t1, wasOut, P, fSaid, nSaid, (!!place || keeps) && fi < 0);
          });
          h.out = true; h.at = P;
          reaches.push({ side: s, t0, t1, P });
        }
        break;
      }
      case 'rest':
        for (const s of sides(a[0] || '')) {
          const ch = '@' + s + 'Hand', h = hand[s];
          if (!free(ch)) { notes.push(ch + ' is keyed by hand: «' + L.line + '» does not write it'); continue; }
          if (!h.out) { if (a[0] !== 'both') bad('the ' + s.toLowerCase() + ' hand is at rest then: nothing to let go (a reach of it comes first)'); continue; }
          takes(L, ch, 'the ' + s.toLowerCase() + ' hand');
          const far = len(sub(h.at, REST[s])), need = Math.max(0.4, Back * far);
          if (span < need - 1e-6) bad('the hand comes ' + r3(far) + ' m back in ' + r3(span) + ' s — a snap; no faster than ' + Back + ' s a metre: at least ' + r3(need) + ' s');
          // (back to how it hangs, as it comes down. Its weight let go at once drags the hand straight to where it hangs — from the head that is through the
          //  shoulder: so it is held in full for a share of the way, the least share on which the arm passes no limit; and the elbow comes down with it, or stays)
          const from = h.at, poleFree = free(ch + '.p') && (c.keys[ch + '.p'] || []).length > 0, poleWas = lastOf(ch + '.p', sided(ELBOW.down, s)), list = [];
          for (const hold of [0, 0.25, 0.5, 0.75]) for (const w of ways(s, from, REST[s], poleFree ? [sided(ELBOW.down, s), null] : [null])) list.push({ via: w.via, pole: w.pole, hold, rank: w.rank + hold * 4 });
          choose(s, [ch, ch + '.w', ch + '.p', ch + '.f', ch + '.n'], t0, t1, list.sort((x, y) => x.rank - y.rank), way => {
            cut(ch, t0); cut(ch + '.w', t0);
            K(ch, t0, from); if (way.via) K(ch, t0 + span * 0.5, way.via); K(ch, t1, REST[s]);
            K(ch + '.w', t0, [1, 0, 0]); if (way.hold > 0) K(ch + '.w', t0 + span * way.hold, [1, 0, 0]); K(ch + '.w', t1, [0, 0, 0]);
            if (way.pole) { cut(ch + '.p', t0); K(ch + '.p', t0, poleWas); K(ch + '.p', t1, way.pole); }
            turnHand(ch, s, t0, t1, true, REST[s], RestFingers, [-SIGN[s], 0, 0], false);
          });
          h.out = false; h.at = REST[s];
          reaches.push({ side: s, t0, t1, P: null });
        }
        break;
      case 'look': {
        look.said = true;
        if (free('@Look')) takes(L, '@Look', 'the head');
        if (a[0] === 'ahead') { lookOff(t0, t1); break; }
        let p;
        if (DIRS[a[0]] && a[0] !== 'forward' && a[0] !== 'back') {
          let yaw = 0, pitch = 0;
          for (let i = 0; i < a.length; i += 2) { const deg = a[i + 1] === undefined ? 35 : num(a[i + 1]); if (deg < 0 || deg > 90) bad('a look ' + deg + '° aside: 0..90 (the head itself turns no farther than 80: BodyClips.LookMost)'); if (a[i] === 'left') yaw = -deg; else if (a[i] === 'right') yaw = deg; else if (a[i] === 'up') pitch = -deg; else if (a[i] === 'down') pitch = deg; else bad('look left|right|up|down <deg> […]'); }
          p = add(HEAD, rotY(rotX([0, 0, 2], pitch), yaw));
        } else { if (a.length < 3) bad('look <x y z> | left|right|up|down <deg> | ahead'); p = [num(a[0]), num(a[1]), num(a[2])]; }
        if (!free('@Look')) { notes.push('@Look is keyed by hand: «' + L.line + '» does not write it'); break; }
        lookTo(t0, t1, p);
        break;
      }
      case 'nod': case 'shake': {
        look.said = true;
        if (!free('@Look')) { notes.push('@Look is keyed by hand: «' + L.line + '» does not write it'); break; }
        takes(L, '@Look', 'the head');
        // (the weight and the point the head has at that moment: a look that is on, a nod just before still fading; under half of it the weight is taken up first)
        const w0 = keyAt('@Look.w', t0, [0])[0], base = w0 > 0.05 ? keyAt('@Look', t0, AHEAD) : AHEAD, wn = w0 >= 0.5 ? w0 : NodW, ts = w0 >= 0.5 ? t0 : t0 + 0.15;
        const deg = (a[0] !== undefined ? num(a[0]) : L.verb === 'nod' ? 12 : 15) / wn, times = a[1] !== undefined ? Math.max(1, Math.round(num(a[1]))) : L.verb === 'nod' ? 1 : 2, each = (t1 - ts) / times;
        if (each < 0.25) bad('a ' + L.verb + ' in ' + r3(each) + ' s is a twitch: at least 0.25 s each');
        cut('@Look', t0); cut('@Look.w', t0);
        K('@Look', t0, base); lookW(t0, w0); if (ts > t0) lookW(ts, wn);
        const v0 = sub(base, HEAD), yaw0 = Math.atan2(v0[0], v0[2]) * 180 / Math.PI;
        const turned = d => add(HEAD, L.verb === 'nod' ? rotY(rotX(rotY(v0, -yaw0), d), yaw0) : rotY(v0, d)); // (a nod round the head's own right, wherever it looks: round the body's it was no nod at all on a head turned aside)
        for (let i = 0; i < times; i++) {
          const k = Math.pow(0.7, i); // (each one smaller than the last)
          if (L.verb === 'nod') { K('@Look', ts + (i + 0.4) * each, turned(deg * k)); K('@Look', ts + (i + 0.8) * each, turned(-deg * 0.15 * k)); }
          else { K('@Look', ts + (i + 0.3) * each, turned(deg * k)); K('@Look', ts + (i + 0.75) * each, turned(-deg * k)); }
        }
        K('@Look', t1, base); lookW(t1, wn);
        if (!look.on && t1 + 0.3 <= length + 1e-6) { lookW(t1 + 0.3, 0); K('@Look', t1 + 0.3, base); } // (at the clip's very end its own fade carries the head off: a key there would fall on the nod's last one)
        break;
      }
      case 'lean': {
        if (a[0] === 'none') { T.pitch.to(t0, t1, 0); T.roll.to(t0, t1, 0); break; }
        if (!['forward', 'back', 'left', 'right'].includes(a[0]) || a[1] === undefined) bad('lean forward|back|left|right <deg> | none');
        const deg = num(a[1]), spine = deg * LeanShare, most = limit('SpineBent', 58);
        if (spine > most) bad('a lean of ' + deg + '° is past what the spine gives (' + Math.floor(most / LeanShare) + '°, BodyCheck.SpineBent): the rest is the hips\' and the knees\' — «sink» with it, or take a kneel that is there (node tools/anim.js find коліно)');
        if (a[0] === 'forward') T.pitch.to(t0, t1, spine); else if (a[0] === 'back') T.pitch.to(t0, t1, -spine); else T.roll.to(t0, t1, a[0] === 'left' ? spine : -spine);
        break;
      }
      case 'turn': {
        if (a[0] === 'none') { T.yaw.to(t0, t1, 0); break; }
        if (!['left', 'right'].includes(a[0]) || a[1] === undefined) bad('turn left|right <deg> | none');
        const deg = num(a[1]); if (deg > limit('SpineBent', 58)) bad('the chest turns no farther than ' + limit('SpineBent', 58) + '° on the hips (BodyCheck.SpineBent): the feet must turn the rest (a step), or the look does it (look ' + a[0] + ' ' + deg + ')');
        T.yaw.to(t0, t1, a[0] === 'right' ? deg : -deg);
        break;
      }
      case 'sink': {
        if (a[0] === undefined) bad('sink <m> (0: up again)');
        const m = num(a[0]), most = limit('MoveMost', 0.3);
        if (m < 0) bad('sink <m>: how far DOWN, from 0 (a body does not rise over its own legs)');
        if (m > most) bad('the hips ' + m + ' m down is past what standing legs give (' + most + ' m, BodyCheck.MoveMost): that is a kneel — take one that is there (node tools/anim.js find коліно)');
        needFeet(); T.my.to(t0, t1, -m);
        break;
      }
      case 'weight': {
        if (!['left', 'right', 'centre'].includes(a[0])) bad('weight left|right|centre [m]');
        const m = a[1] !== undefined ? num(a[1]) : limit('WeightSide', 0.04) + 0.01;
        if (m < 0 || m > WeightMost) bad('the weight goes ' + m + ' m aside: 0..' + WeightMost + ' (the hips stay between the feet)');
        needFeet(); T.mx.to(t0, t1, (foot.Left[0] + foot.Right[0]) / 2 - (STANCE.Left[0] + STANCE.Right[0]) / 2 + (a[0] === 'left' ? -m : a[0] === 'right' ? m : 0));
        T.my.bump([[t0, 0], [t0 + span * 0.5, -0.008], [t1, 0]]); // (the knees give as the weight goes over)
        break;
      }
      case 'step': {
        if (a.length < 3) bad('step left|right <dx> <dz> (metres: right, ahead)');
        const s = sides(a[0])[0], o = s === 'Left' ? 'Right' : 'Left', ch = '@' + s + 'Foot', d = [num(a[1]), 0, num(a[2])], far = len(d);
        if (far > StepMost) bad('a step of ' + r3(far) + ' m: no longer than ' + StepMost + ' m (two steps)');
        if (span < 0.45) bad('a step in ' + r3(span) + ' s is a jump: at least 0.45 s');
        if (t0 < StepBefore - 1e-6) bad('a step needs ' + StepBefore + ' s before it, for the weight to go over to the other foot: begin at ' + StepBefore + ' or later');
        if (t1 + StepAfter > length + 1e-6) bad('a step needs ' + StepAfter + ' s after it within the clip, for the body to settle: end by ' + r3(length - StepAfter) + ', or a longer clip');
        needFeet();
        if (!free(ch)) { notes.push(ch + ' is keyed by hand: «' + L.line + '» does not write it'); break; }
        takes(L, ch, 'the ' + s.toLowerCase() + ' foot');
        const from = foot[s], to = add(from, d), pre = t0 - StepBefore, side = limit('WeightSide', 0.04) + 0.01;
        const mid = f => (f.Left[0] + f.Right[0]) / 2 - (STANCE.Left[0] + STANCE.Right[0]) / 2, midZ = f => (f.Left[2] + f.Right[2]) / 2 - (STANCE.Left[2] + STANCE.Right[2]) / 2;
        T.mx.to(pre, t0, mid(foot) + SIGN[o] * side); // (the weight over the foot that stays, before this one leaves the ground)
        cut(ch, t0);
        K(ch, t0, from); K(ch, t0 + span * 0.4, add(add(from, mul(d, 0.45)), [0, StepUp, 0])); K(ch, t0 + span * 0.8, add(to, [0, 0.05, 0])); K(ch, t1, to); // (down from above: never slid in)
        foot[s] = to;
        const settle = t1 + StepAfter;
        T.mx.to(t0 + span * 0.5, settle, mid(foot)); T.mz.to(t0 + span * 0.4, settle, midZ(foot)); // (the body follows half the way)
        T.my.bump([[t0 + span * 0.7, 0], [t1 + 0.05, -0.018], [settle, 0]]); // (the knee takes the landing)
        break;
      }
      case 'sigh': {
        if (span < 0.8) bad('a breath in ' + r3(span) + ' s is a gasp: at least 0.8 s');
        T.pitch.bump([[t0, 0], [t0 + span * 0.4, -3.5], [t0 + span * 0.8, 2], [t1, 0]]);
        T.my.bump([[t0, 0], [t0 + span * 0.4, 0.006], [t0 + span * 0.8, -0.012], [t1, 0]]);
        break;
      }
      case 'grip': { // (a hand laid on the thing the clip moves: the wrist's point, the hand's turn and each finger's bend are tools/grip.js's reckoning)
        const fi = a.indexOf('fingers');
        for (const s of sides(a[0] || '')) {
          const h = '@' + s + 'Hand', own = [h + '.on', h + '.on.f', h + '.on.n', h + '.on.w'];
          if (own.some(ch => !free(ch))) { notes.push(h + '.on is keyed by hand: «' + L.line + '» does not write it (node tools/anim.js auto <clip> ' + own.join(' ') + ' gives it to the script)'); continue; }
          let r; try { r = require('./grip.js').forScript(c, s.toLowerCase(), a[1], fi >= 0 ? a[fi + 1] : null, t1); } catch (e) { bad(e.message); }
          for (const n of r.notes) notes.push(n);
          for (const ch of own) cut(ch, t0);
          K(h + '.on', t0, r.on); K(h + '.on.f', t0, r.f); K(h + '.on.n', t0, r.n);
          K(h + '.on.w', t0, lastOf(h + '.on.w', [0, 0, 0])); K(h + '.on.w', t1, [1, 0, 0]);
          if ((c.keys[h] || []).length && free(h + '.w')) { cut(h + '.w', t0); K(h + '.w', t0, lastOf(h + '.w', [0, 0, 0])); K(h + '.w', t1, [1, 0, 0]); } // (a hand with a target of its own goes by that target's weight: BodyAnimator.Targets)
          if (r.keys) for (const name of Object.keys(r.keys)) for (const i of [0, 1]) {
            const v = r.keys[name][i], ch = s + 'Hand' + name + (i + 1);
            if (!v || !free(ch) || (!(c.keys[ch] || []).length && v.every(x => Math.abs(x) < 0.5))) continue;
            cut(ch, t0); K(ch, t0, lastOf(ch, [0, 0, 0])); K(ch, t0 + span * 0.6, lastOf(ch, [0, 0, 0])); K(ch, t1, v); // (open on the way: the fingers close as the palm comes on)
          }
        }
        break;
      }
      case 'release': { // (the hand off the thing: its hold let go, the fingers back to how the game holds them)
        for (const s of sides(a[0] || '')) {
          const h = '@' + s + 'Hand';
          if (!free(h + '.on.w')) { notes.push(h + '.on.w is keyed by hand: «' + L.line + '» does not write it'); continue; }
          if (!(c.keys[h + '.on.w'] || []).length) bad('the ' + s.toLowerCase() + ' hand holds nothing then: a grip of it comes first');
          cut(h + '.on.w', t0); K(h + '.on.w', t0, lastOf(h + '.on.w', [0, 0, 0])); K(h + '.on.w', t1, [0, 0, 0]);
          for (const ch of c.order.slice()) if (new RegExp('^' + s + 'Hand(Thumb|Index|Middle|Ring|Pinky)[123]$').test(ch) && mine.has(ch)) { cut(ch, t0); K(ch, t0, lastOf(ch, [0, 0, 0])); K(ch, t0 + span * 0.5, [0, 0, 0]); }
        }
        break;
      }
      case 'fingers': {
        if (!FINGERS[a[1]]) bad('fingers left|right|both ' + Object.keys(FINGERS).join('|'));
        const own = FINGERS_BODY[body.bodyOf(c).name]; // (a body of its own hand: its measured shapes only)
        for (const s of sides(a[0] || '')) {
          if (own && !own[s]) bad('fingers ' + s.toLowerCase() + ': the ' + body.bodyOf(c).name + '\'s ' + s.toLowerCase() + ' hand is not measured yet — one take of a clip of this body with that hand on a thing (grip), then node tools/anim.js grip presets, then its numbers into FINGERS_BODY');
          const set = own ? Object.assign({}, own[s][a[1]]) : Object.assign({}, FINGERS[a[1]], s === 'Left' ? FINGERS_LEFT[a[1]] : null), all = new Set(Object.keys(set));
          for (const ch of c.order) { const m = new RegExp('^' + s + 'Hand(Index|Middle|Ring|Pinky)([123])$').exec(ch); if (m && mine.has(ch)) all.add(m[1] + m[2]); }
          for (const j of all) { const ch = s + 'Hand' + j; if (!free(ch)) continue; cut(ch, t0); K(ch, t0, lastOf(ch, [0, 0, 0])); K(ch, t1, [set[j] || 0, 0, 0]); }
        }
        break;
      }
    }
  }
  for (const ch of c.order) c.keys[ch].sort((a, b) => a.t - b.t); // (the accompaniment reads the hands by clip.valueAt: keys in time order)
  const before = JSON.stringify([c.keys, c.order, [...mine], c.head]);
  for (let coarse = 1; ; coarse *= 2) {
    const n0 = notes.length;
    accompany(c, { alive, T, K, free, mine, notes, reaches, look, length, coarse });
    const times = new Set(); for (const ch of c.order) for (const k of c.keys[ch]) times.add(Math.round(k.t * 1000));
    if (times.size <= KeyTimes || coarse >= 4) break;
    const b = JSON.parse(before); c.keys = b[0]; c.order = b[1]; mine.clear(); for (const ch of b[2]) mine.add(ch); c.head = b[3]; notes.length = n0;
  }
  { const times = new Set(); for (const ch of c.order) for (const k of c.keys[ch]) times.add(Math.round(k.t * 1000)); if (times.size > 200) throw new Error(times.size + ' key times, the game keeps 200 (BodyClips.MaxKeys): fewer movements in one clip, node tools/anim.js alive ' + c.name + ' 0 (no accompaniment), or two clips'); }
  for (const ch of skipped) if (!notes.some(x => x.includes(ch))) notes.push(ch + ' is keyed by hand: the script leaves it');
  for (const ch of [...mine]) if (!(c.keys[ch] || []).length) { mine.delete(ch); delete c.keys[ch]; c.order = c.order.filter(x => x !== ch); }
  setLine(c, '# scripted:', mine.size ? '# scripted: ' + [...mine].join(' ') : null);
  if (c.order.length > 40) throw new Error(c.order.length + ' channels, the game keeps 40 (fingers take eight a hand): fewer finger movements, or the fingers of one hand only');
  return { notes };
}

// a spring that follows u (sampled every dt): it lags, overshoots a little, settles — what the core's Inertia does for a hand
function spring(u, dt, hz, damp) {
  const out = [], om = 2 * Math.PI * hz; let x = u[0], v = 0;
  for (let i = 0; i < u.length; i++) { for (let k = 0; k < 5; k++) { const h = dt / 5, a = om * om * (u[i] - x) - 2 * damp * om * v; v += a * h; x += v * h; } out.push(x); }
  return out;
}
// keys of a sampled line: as few as keep it within tol; the times in «keep» always (the movements' own moments)
function simplify(ts, vs, tol, keep) {
  const idx = new Set([0, ts.length - 1]);
  for (const t of keep) { let b = 0; for (let i = 1; i < ts.length; i++) if (Math.abs(ts[i] - t) < Math.abs(ts[b] - t)) b = i; idx.add(b); }
  const split = (a, b) => {
    let worst = 0, at = -1;
    for (let i = a + 1; i < b; i++) { const u = (ts[i] - ts[a]) / (ts[b] - ts[a]); let e = 0; for (let j = 0; j < vs[i].length; j++) e = Math.max(e, Math.abs(vs[i][j] - (vs[a][j] + (vs[b][j] - vs[a][j]) * u)) / tol[j]); if (e > worst) { worst = e; at = i; } }
    if (worst > 1) { idx.add(at); split(a, at); split(at, b); }
  };
  const fixed = [...idx].sort((a, b) => a - b);
  for (let i = 1; i < fixed.length; i++) split(fixed[i - 1], fixed[i]);
  return [...idx].sort((a, b) => a - b);
}

// The rest of the body after the hands (the head of this file). Writes Spine1, Spine2, move, and — when the script is silent on them — the look and
// the other hand; only channels that are the script's or nobody's yet.
const Lead = 0.22, Hz = 1.6, Damp = 0.7; // s the trunk begins before the hand · the spring that carries it
const KeyTimes = 180;                    // key times a clip may have before the accompaniment is made coarser (the game keeps 200: BodyClips.MaxKeys)
// does the clip play on a standing body? Not one turned or laid by «body»/«Hips» keys, nor one whose row in docs/CLIPS.md begins anything but «стійка»
// (сидить, лежить): the shoulder, the rest of the hands and the trunk's law here are a standing body's (the review 2026-11-11: fall_back got 25° of
// forward bend lying on its back; six sitting clips were told their hands miss)
function standing(c, plain) { // (plain: the game's own stance and no other — «стійка зброї» stands too, but its shoulders are not the ones tools/body.js measured)
  if (c.keys.body || c.keys.Hips) return false;
  const row = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').find(l => l.startsWith('| `' + c.name + '` |'));
  return !row || (plain ? /^\s*стійка\s*(→|$)/ : /^\s*стійка/).test(row.split('|')[4] || '');
}
function accompany(c, o) {
  const { T, K, free, notes, length } = o, clip = require('./clip.js'), coarse = o.coarse || 1, B = body.bodyOf(c), REST = B.rest, SHOULDER = B.shoulder, HEAD = B.eyes, AHEAD = aheadOf(B), H = B.heights;
  let alive = o.alive;
  if (alive > 0 && !standing(c)) { alive = 0; if (c.keys['@LeftHand'] || c.keys['@RightHand']) notes.push('the body does not stand in this clip («body»/«Hips» keys, or its row in docs/CLIPS.md does not begin «стійка»): the accompaniment is a standing body\'s — left out (lean, turn, look still do what they say)'); }
  const dt = length > 8 || coarse > 1 ? 0.1 : 0.05, N = Math.round(length / dt), ts = Array.from({ length: N + 1 }, (_, i) => r3(i * dt));
  const val = (ch, t, def) => { const r = clip.valueAt(c, ch, t); return r ? r.v : def; };
  // a hand at t: its weight, where it is, and what it asks of the trunk — how far ahead of its shoulder (z), out from it (o), across the body (a), all × its
  // weight, so a hand at rest asks nothing. A hand on a thing: the thing's law, not this one
  const handAt = (s, t) => {
    const ch = '@' + s + 'Hand';
    if (!c.keys[ch]) return { w: 0, p: REST[s], d: [0, 0, 0], z: 0, o: 0, a: 0 };
    const w = val(ch + '.on.w', t, [0])[0] > 0.5 ? 0 : clamp(val(ch + '.w', t, [1])[0], 0, 1), tg = val(ch, t, REST[s]), side = SIGN[s] * (tg[0] - SHOULDER[s][0]);
    return { w, p: mix(REST[s], tg, w), d: mul(sub(tg, REST[s]), w), z: w * (tg[2] - SHOULDER[s][2]), o: w * Math.max(0, side - 0.1), a: w * Math.max(0, -side - 0.1) };
  };
  const raw = { pitch: [], yaw: [], roll: [], mx: [], my: [], mz: [], dip: [] };
  let prev = null;
  for (const t of ts) {
    const L = handAt('Left', t), R = handAt('Right', t);
    const fwd = h => Math.max(0, h.z - 0.35), low = h => h.w * Math.max(0, H.low - h.p[1]), high = h => h.w * Math.max(0, h.p[1] - H.high), up = h => h.w * Math.max(0, h.p[1] - H.up);
    raw.yaw.push(clamp(18 * (L.z - R.z) + 20 * (R.o - R.a + L.a - L.o), -20, 20));           // (a hand ahead brings its shoulder forward; a hand out or across turns the chest after it)
    raw.pitch.push(clamp(28 * (fwd(L) + fwd(R)) + 30 * (low(L) + low(R)) - 8 * (high(L) + high(R)), -8, 25)); // (a far or a low reach bends it; a hand overhead arches it back)
    raw.roll.push(clamp(18 * (up(R) - up(L)) - 8 * (Math.max(0, R.o - 0.15) - Math.max(0, L.o - 0.15)), -10, 10)); // (a hand up lifts its shoulder; a hand far aside leans that way)
    raw.mx.push(clamp(0.1 * (R.o - R.a + L.a - L.o), -0.05, 0.05));
    raw.mz.push(clamp(0.05 * (Math.max(0, L.z - 0.2) + Math.max(0, R.z - 0.2)), 0, 0.03));
    raw.my.push(-clamp(0.2 * (L.w * Math.max(0, H.knee - L.p[1]) + R.w * Math.max(0, H.knee - R.p[1])), 0, 0.1));
    const speed = prev ? Math.max(len(sub(L.p, prev.L)), len(sub(R.p, prev.R))) / dt : 0;
    raw.dip.push(-0.02 * clamp(speed / 1.5, 0, 1)); // (the knees give at a fast move)
    prev = { L: L.p, R: R.p };
  }
  const lead = Math.round(Lead / dt), ahead = a => a.map((_, i) => a[Math.min(a.length - 1, i + lead)]); // (the trunk begins before the hand: what comes is read ahead)
  const acc = {};
  for (const k of ['pitch', 'yaw', 'roll', 'mx', 'mz']) acc[k] = spring(ahead(raw[k]), dt, Hz, Damp).map(v => v * alive);
  const my = spring(ahead(raw.my), dt, Hz, Damp), dip = spring(ahead(raw.dip), dt, 3, 0.8);
  acc.my = my.map((v, i) => (v + dip[i]) * alive);
  // a loop's stretch ends as it began (clipcheck): what the spring is still off by at its end is taken out over it
  const loops = [];
  for (const h of c.head) { const w = h.trim().split(/\s+/); if (w[0] === 'part' && w[4] === 'loop') loops.push([+w[2], +w[3]]); }
  const whole = headWord(c, 'loop'); if (whole && whole[0] === '1') loops.push([0, length]);
  const closed = arr => { for (const [a, b] of loops) { const ia = Math.round(a / dt), ib = Math.min(N, Math.round(b / dt)), off = arr[ib] - arr[ia]; for (let i = ia; i <= ib; i++) { const u = (i - ia) / Math.max(1, ib - ia); arr[i] -= off * u * u * (3 - 2 * u); } } return arr; };
  for (const k of Object.keys(acc)) closed(acc[k]);
  // the trunk: what the movements said (lean, turn, sigh) plus what the hands ask of it
  const sum = k => ts.map((t, i) => T[k].at(t) + (alive > 0 ? acc[k][i] : 0));
  const spineFree = ['Spine1', 'Spine2'].every(b => free(b));
  const wantsSpine = alive > 0 || T.pitch.used() || T.yaw.used() || T.roll.used();
  if (wantsSpine && spineFree) {
    for (const b of ['Spine1', 'Spine2']) if (!hasMode(c, b, 'body')) { let at = c.head.findIndex(h => h.trim().startsWith('#') || h.trim() === ''); if (at < 0) at = c.head.length; c.head.splice(at, 0, 'mode ' + b + ' body'); }
    const most = limit('SpineBent', 58), cap = v => clamp(v, -most, most);
    const p = sum('pitch').map(cap), y = sum('yaw').map(cap), r = sum('roll').map(cap), vs = ts.map((_, i) => [p[i], y[i], r[i]]); // (a lean said plus what the hands ask: together no more than the spine gives — BodyCheck.SpineBent)
    if (vs.some(v => v.some(x => Math.abs(x) > 0.3))) {
      const keep = [...T.pitch.times(), ...T.yaw.times(), ...T.roll.times()];
      const tenth = v => v.map(x => Math.round(x * 10) / 10);
      for (const i of simplify(ts, vs, [0.4 * coarse, 0.4 * coarse, 0.4 * coarse], keep)) { K('Spine1', ts[i], tenth(mul(vs[i], 0.45))); K('Spine2', ts[i], tenth(mul(vs[i], 0.55))); }
    }
  } else if (wantsSpine && (T.pitch.used() || T.yaw.used() || T.roll.used())) notes.push('Spine1/Spine2 are keyed by hand: the script\'s lean, turn and sigh do not write them (node tools/anim.js auto ' + c.name + ' Spine1 Spine2)');
  // the weight: only on feet that stand by their own targets (else the body would carry them along the ground)
  const feet = (c.keys['@LeftFoot'] || []).length && (c.keys['@RightFoot'] || []).length;
  const wantsMove = T.mx.used() || T.my.used() || T.mz.used() || alive > 0;
  if (wantsMove && free('move')) {
    if (!feet) { if (T.mx.used() || T.my.used() || T.mz.used() || (alive > 0 && (c.keys['@LeftHand'] || c.keys['@RightHand']))) notes.push('no @LeftFoot/@RightFoot targets: the weight is left out of the accompaniment (the body would drag the feet)'); }
    else {
      const x = sum('mx'), y = sum('my'), z = sum('mz'), vs = ts.map((_, i) => [x[i], y[i], z[i]]);
      if (vs.some(v => v.some(q => Math.abs(q) > 0.002))) for (const i of simplify(ts, vs, [0.003 * coarse, 0.003 * coarse, 0.003 * coarse], [...T.mx.times(), ...T.my.times(), ...T.mz.times()])) K('move', ts[i], vs[i]);
    }
  } else if (wantsMove && (T.mx.used() || T.my.used() || T.mz.used())) notes.push('move is keyed by hand: the script\'s sink, weight and step do not write it (node tools/anim.js auto ' + c.name + ' move)');
  if (!(alive > 0)) return;
  // the head: at what a hand reaches for before the body, the eyes first — unless the script says the look itself, or the hand goes to its own body
  if (!o.look.said && free('@Look') && o.reaches.some(r => r.P)) {
    let on = false, at = AHEAD, whose = null;
    const W = (t, w) => K('@Look.w', t, [w, 0, 0]);
    K('@Look', 0, AHEAD); W(0, 0);
    const one = []; // (both hands sent at once: one look, at the middle between them)
    for (const r of o.reaches.slice().sort((a, b) => a.t0 - b.t0)) { const p = one[one.length - 1]; if (p && r.P && p.P && Math.abs(p.t0 - r.t0) < 0.05 && p.side !== r.side) p.P = mix(p.P, r.P, 0.5); else one.push(Object.assign({}, r)); }
    for (const r of one) {
      const ts0 = Math.max(0, r.t0 - 0.2), seen = r.P && r.P[2] > 0.3 && r.P[1] < H.seen && len(sub(r.P, HEAD)) > 0.45;
      if (seen) { if (!on) { K('@Look', ts0, r.P); W(ts0, 0); W(ts0 + 0.35, 0.5 * Math.min(1, alive)); } else { K('@Look', ts0, at); K('@Look', ts0 + 0.4, r.P); } on = true; at = r.P; whose = r.side; }
      else if (on && r.side === whose) { W(ts0, 0.5 * Math.min(1, alive)); W(Math.min(length, ts0 + 0.5), 0); K('@Look', ts0, at); K('@Look', Math.min(length, ts0 + 0.5), at); on = false; } // (its point held till the weight is off)
    }
  }
  // the other hand answers: a hand nothing speaks of, while the working one is out — a small target, a small weight, later than the trunk
  for (const s of ['Left', 'Right']) {
    const o2 = s === 'Left' ? 'Right' : 'Left', ch = '@' + s + 'Hand';
    if ((c.keys[ch] || []).length || !free(ch) || !(c.keys['@' + o2 + 'Hand'] || []).length) continue;
    const d = ts.map(t => handAt(o2, t).d), far = d.map(len);
    if (Math.max(...far) < 0.2) continue;
    const w = closed(spring(far.map(f => 0.4 * clamp((f - 0.1) / 0.25, 0, 1)), dt, 1.2, 0.9)).map(v => clamp(v * Math.min(1, alive), 0, 0.45));
    const off = [0, 1, 2].map(j => closed(spring(d.map((v, i) => [SIGN[s] * 0.03 * far[i], 0.04 * far[i], -0.14 * v[2]][j]), dt, 1.2, 0.8)));
    const vs = ts.map((_, i) => [REST[s][0] + off[0][i], REST[s][1] + off[1][i], REST[s][2] + off[2][i], w[i]]);
    for (const i of simplify(ts, vs, [0.004 * coarse, 0.004 * coarse, 0.004 * coarse, 0.03 * coarse], [])) { K(ch, ts[i], vs[i]); K(ch + '.w', ts[i], [vs[i][3], 0, 0]); }
    K(ch + '.p', 0, sided(ELBOW.down, s));
  }
}

// ---------------------------------------------------------------- what the keys alone say before a take
// a hand's speed by its keys, m/s: the fastest moment within [a, b] (its target eased in by its weight from where it hangs; not while it is on a thing)
function handSpeed(c, s, a, b) {
  const clip = require('./clip.js'), ch = '@' + s + 'Hand', REST = body.bodyOf(c).rest, sorted = n => c.keys[n] && c.keys[n].length ? c.keys[n].slice().sort((x, y) => x.t - y.t) : null;
  const ks = sorted(ch), ws = sorted(ch + '.w'), on = sorted(ch + '.on.w');
  if (!ks) return { worst: 0, at: 0 };
  const pos = t => { const w = clamp(eased(ws, t, [1])[0], 0, 1); return mix(REST[s], eased(ks, t, REST[s]), w); };
  let worst = 0, at = 0;
  for (let t = Math.max(0, a); t + 0.05 <= b + 1e-6; t += 0.05) { if (on && eased(on, t, [0])[0] > 0.5) continue; const v = len(sub(pos(t + 0.05), pos(t))) / 0.05; if (v > worst) { worst = v; at = t; } }
  return { worst, at };
}
// A fault of haste — a hand too fast («speed»), a bone of the arm turning too fast («spin») at «at» — and the cure reckoned, not left to takes (the
// user 2026-11-12: «інструмент сам каже, скільки часу дати рухові»; zz_scratch the day before went 0.9 → 1.3 s by my own hand, a take a guess): the
// stretch of the clip that fault belongs to — in a scripted clip the hand's movement playing then (and Settle after it), else the two keys of the hand's
// target round it — is made longer IN MEMORY by the very code of the verb «stretch» (clip.stretchTo; a scripted clip made again from its script), and the
// fault measured again, till it is under its limit with Room to spare. Says the seconds that do it and the ready command; null: no stretch found.
// ok false: more time does not cure it (a hand's turn that flips: another turn, not a longer one). Costs a making of the clip a try, at most Tries.
// Only for a body that stands (standing): a fall's hands fly, and «stretch the fall to 0.85 s» would be a command that leads into nonsense. A hand both
// too fast and jerking in one stretch gets one command, the longer of the two (predict).
const Tries = 4, Most = 4; // (tries at a longer way; the longest tried is Most × the way it has: slower())
function slower(c, s, at, kind) {
  if (!standing(c)) return null;
  const clip = require('./clip.js'), ch = '@' + s + 'Hand', length = lengthOf(c), side = s.toLowerCase();
  const lim = (kind === 'spin' ? limit('BoneJerk', 600) : limit('HandFast', 3)) * (1 - Room), up = v => Math.ceil(v * 20 - 1e-6) / 20;
  const measure = (x, a, b) => kind === 'spin' ? Math.max(0, ...(body.sweep(x, s, a, Math.min(lengthOf(x), b)) || []).map(q => q.spin)) : handSpeed(x, s, a, Math.min(lengthOf(x), b)).worst;
  let span = null;
  const scripted = listOf(c, 'scripted').includes(ch);
  if (scripted) {
    const lines = scriptOf(c).map((l, i) => Object.assign(parseLine(l, length), { n: i + 1 })).filter(L => ['reach', 'rest', 'grip', 'release'].includes(L.verb) && (L.a[0] === side || L.a[0] === 'both') && at >= L.t0 - 1e-6 && at <= L.t1 + Settle + 1e-6);
    if (lines.length) { const L = lines.reduce((p, q) => q.t0 > p.t0 ? q : p); span = { t0: L.t0, t1: L.t1, n: L.n, line: L.line }; }
  } else {
    const ks = (c.keys[ch] || []).slice().sort((x, y) => x.t - y.t);
    for (let i = 0; i + 1 < ks.length; i++) if (at >= ks[i].t - 1e-6 && at < ks[i + 1].t + 1e-6) { span = { t0: ks[i].t, t1: ks[i + 1].t }; break; }
    // (… and the whole way of its weight, where the weight is on its way then: a hand let go drops for as long as its weight falls, and two keys of the
    //  target are only a part of that — shade_eyes 2026-11-12, the way 3–3.4 s stretched alone cured nothing, the weight fell till 3.8)
    const ws = (c.keys[ch + '.w'] || []).slice().sort((x, y) => x.t - y.t);
    if (span) for (let i = 0; i + 1 < ws.length; i++) if (at >= ws[i].t - 1e-6 && at < ws[i + 1].t + 1e-6 && Math.abs(ws[i].v[0] - ws[i + 1].v[0]) > 0.05) { span = { t0: Math.min(span.t0, ws[i].t), t1: Math.max(span.t1, ws[i + 1].t) }; break; }
  }
  if (!span || !(span.t1 > span.t0)) return null;
  const has = span.t1 - span.t0, now = measure(c, span.t0, span.t1 + Settle);
  if (now <= lim) return null; // (the fault is not this stretch's own)
  // (the longest way tried: Most × what it has, at least +1 s. Uncapped, a hand turned over on the spot — 4320 deg/s, dv_wave_off 2026-11-16 —
  //  made each try ×7 longer, 0.3 → 2.3 → 18 → 140 s, each regen and sweep over the whole of it: «check» and «do» hung for minutes. Past this
  //  length time is not the cure: the line says «more time alone does not clear it»)
  const most = up(Math.max(has * Most, has + 1));
  let need = Math.min(most, up(has * now / lim)), peak = now, ok = false;
  for (let i = 0; i < Tries && !ok; i++) {
    const x = JSON.parse(JSON.stringify(c));
    try { clip.stretchTo(x, span.t0, span.t1, need); if (scripted) regen(x); } catch (e) { return null; } // quiet: the stretched clip is refused (a part's edge, the key limit) — no number to give, the line stays as it was
    peak = measure(x, span.t0, span.t0 + need + Settle);
    if (peak <= lim) ok = true; else if (need >= most) break; else need = Math.min(most, up(need * Math.max(1.08, peak / lim)));
  }
  return Object.assign(span, { has, need, peak, ok, lim, fix: '\n      fix: node tools/anim.js stretch ' + c.name + ' ' + r3(span.t0) + ' ' + r3(span.t1) + ' ' + r3(need) });
}
const slowerSaid = (x, unit) => !x ? '' : x.ok
  ? ' — ' + (x.n ? 'do ' + x.n + ' («' + x.line + '»)' : 'the way ' + r3(x.t0) + '–' + r3(x.t1) + ' s') + ' has ' + r3(x.has) + ' s: ' + r3(x.need) + ' s clears it by the model (' + (unit === 'm/s' ? r3(x.peak) : Math.round(x.peak)) + ' ' + unit + ' then; what comes after it moves later with it)' + (x.also ? ' — the same stretch has another fault that needs ' + r3(x.also) + ' s: that one\'s command clears both' : '') + x.fix
  : ' — more time alone does not clear it (' + (x.n ? 'do ' + x.n : 'the way ' + r3(x.t0) + '–' + r3(x.t1) + ' s') + ', ' + r3(x.has) + ' s now, made ' + r3(x.need) + ' s long still gives ' + (unit === 'm/s' ? r3(x.peak) : Math.round(x.peak)) + ' ' + unit + ', wanted under ' + (unit === 'm/s' ? r3(x.lim) : Math.round(x.lim)) + '): the hand\'s turn or its way is the cause, not the time';
// two faults of one stretch: the shorter cure gives way to the longer one's command (said once)
const sameStretch = (a, b) => a && b && a.ok && b.ok && Math.abs(a.t0 - b.t0) < 1e-6 && Math.abs(a.t1 - b.t1) < 1e-6;
function predict(c) {
  const clip = require('./clip.js'), out = [], length = lengthOf(c), fast = limit('HandFast', 3), turn = limit('WeightTurn', 0.5);
  const val = (ch, t, def) => { const r = clip.valueAt(c, ch, t); return r ? r.v : def; };
  const touches = c.head.some(h => /^touch\s/.test(h.trim())), B = body.bodyOf(c), REST = B.rest, deg = v => Math.round(v) + '°';
  for (const s of ['Left', 'Right']) {
    const ch = '@' + s + 'Hand', ks = c.keys[ch] || [], ws = c.keys[ch + '.w'] || [];
    if (!ks.length) continue;
    const onThing = t => val(ch + '.on.w', t, [0])[0] > 0.5;
    const { worst, at } = handSpeed(c, s, 0, length);
    const jerks = standing(c, true) ? body.judge(body.sweep(c, s)).spin : null, xs = jerks ? slower(c, s, jerks.t, 'spin') : null; // (reckoned here, before the [fast] line: one stretch, one command)
    if (worst > fast) { let x = slower(c, s, at, 'speed'); if (sameStretch(x, xs)) { if (x.need > xs.need) { xs.fix = ''; xs.also = x.need; } else { x = Object.assign({}, x, { fix: '', also: xs.need }); } } out.push('[fast] the ' + s.toLowerCase() + ' hand goes ' + r3(worst) + ' m/s by its keys at ' + r3(at) + ' s (BodyCheck.HandFast ' + fast + ')' + (x ? slowerSaid(x, 'm/s') : ': more time for that way (node tools/anim.js stretch ' + c.name + ' …), or a nearer point')); }
    for (let i = 1; i < ws.length; i++) { // (a hand let go: no faster than Back s a metre of the way it really comes — its distance × the weight it loses; a take too fast shows in its speed above)
      const a = ws[i - 1], b = ws[i], drop = a.v[0] - b.v[0];
      if (drop < turn) continue;
      const far = len(sub(val(ch, a.t, REST[s]), REST[s])) * drop, span = b.t - a.t, need = Back * far;
      if (far >= 0.2 && span < need - 1e-6) out.push('[fast] the ' + s.toLowerCase() + ' hand is let go and comes ' + r3(far) + ' m back in ' + r3(span) + ' s (' + r3(a.t) + '–' + r3(b.t) + '): a snap — no faster than ' + Back + ' s a metre: at least ' + r3(need) + ' s for that way of its weight (' + ch + '.w)');
    }
    // the arm itself, by the body's model (tools/body.js): only a body in the game's own stance
    if (!standing(c, true)) continue;
    const j = body.judge(body.sweep(c, s)), side = s.toLowerCase();
    if (j.past && !touches) out.push('[miss] ' + ch + ' at ' + r3(j.past.t) + ' s is ' + r3(j.past.v) + ' m past the arm (its shoulder then about ' + j.past.sh.map(r3).join(' ') + ', the arm ' + r3(B.upper + B.fore) + ' m): the body must come to it — lean, sink, a step — or the point ' + r3(j.past.v) + ' m nearer');
    if (j.fold) out.push('[fold] the ' + side + ' elbow folds to ' + deg(j.fold.v) + ' at ' + r3(j.fold.t) + ' s (BodyCheck.ElbowFold ' + j.fold.lim + '): the hand passes too near its own shoulder — a way farther out (a reach to a point between), or a point farther from the shoulder');
    if (j.bend) out.push('[bend] the ' + side + ' wrist bends ' + deg(j.bend.v) + ' off the forearm at ' + r3(j.bend.t) + ' s' + (j.bend.span > 0.15 ? ', over the limit ' + r3(j.bend.span) + ' s in all between ' + r3(j.bend.from) + ' and ' + r3(j.bend.to) + ' s — the pose it holds, not a moment' : '') + ' (BodyCheck.WristBend ' + j.bend.lim + '): the forearm goes ' + j.bend.fore.map(r3).join(' ') + ' there — the fingers nearer that way, the palm turned, or the elbow another way (elbow ' + Object.keys(ELBOW).join('|') + ')');
    if (j.deep) out.push('[inside] the ' + side + ' ' + j.deep.what + ' is ' + Math.round(j.deep.v * 100) + '% of the way to the spine at ' + r3(j.deep.t) + ' s (BodyCheck.InsideDeep ' + Math.round(j.deep.lim * 100) + '%): out in front of the chest, or round by the side');
    if (j.head) out.push('[head] the ' + side + ' ' + j.head.what + ' goes through the head: ' + Math.round(-j.head.gap * 1000) + ' mm inside it by the model at ' + r3(j.head.t) + ' s (its ball is ' + Math.round(j.head.off * 1000) + ' mm off on average, so this is no near miss) — ' + Math.round(j.head.v * 100) + '% of the way to the head\'s middle (BodyCheck.HeadDeep ' + Math.round(j.head.lim * 100) + '%): out onto its surface — in front of the brow, beside the temple');
    if (j.spin) {
      // (every stretch of this arm that jerks, not the worst alone: with one line a time the next showed only after the first was mended — a take a
      //  line. The latest first: its command leaves the earlier ones' times as they are)
      const said = [{ at: j.spin, x: xs }], covered = q => said.some(y => y.x ? q.t >= y.x.t0 - 1e-6 && q.t <= y.x.t1 + Settle + 1e-6 : Math.abs(q.t - y.at.t) < 0.5);
      for (const q of (body.sweep(c, s) || []).filter(q => q.spin > j.spin.lim).sort((a, b) => b.spin - a.spin)) { if (said.length >= 4) break; if (covered(q)) continue; said.push({ at: { v: q.spin, t: q.t, what: q.spins, lim: j.spin.lim }, x: slower(c, s, q.t, 'spin') }); }
      said.sort((a, b) => b.at.t - a.at.t);
      const many = said.filter(y => y.x && y.x.ok && y.x.fix).length > 1;
      for (const y of said) out.push('[jerk] the ' + side + ' ' + y.at.what + ' about ' + Math.round(y.at.v) + ' deg/s at ' + r3(y.at.t) + ' s (BodyCheck.BoneJerk ' + y.at.lim + '; the model\'s speed × what takes measured over it)' + (y.x ? slowerSaid(y.x, 'deg/s').replace('\n      fix:', (many ? ' — run these commands in the order given, the latest stretch first' : '') + '\n      fix:') : ': more time for that movement, or a turn of the hand nearer the one it comes from'));
    }
  }
  return out;
}

// the arms by the model in one line, over or not (anim.js check): how near each limit the clip comes
function arms(c) {
  if (!standing(c, true)) return null;
  const out = [];
  for (const s of ['Left', 'Right']) {
    const sw = body.sweep(c, s); if (!sw) continue;
    const held = sw.filter(x => x.bend !== null), top = (k, f) => sw.reduce((a, b) => f(b[k], a[k]) ? b : a);
    const fold = top('fold', (x, y) => x < y), spin = top('spin', (x, y) => x > y), deep = top('deep', (x, y) => x > y), near = top('headGap', (x, y) => x < y), bend = held.length ? held.reduce((a, b) => b.bend > a.bend ? b : a) : null;
    out.push(s.toLowerCase() + ': tightest elbow ' + Math.round(fold.fold) + '° @' + r3(fold.t) + (bend ? ', wrist ' + Math.round(bend.bend) + '° @' + r3(bend.t) : '') + ', ' + spin.spins + ' ' + Math.round(spin.spin) + ' deg/s @' + r3(spin.t) + (deep.deep > 0 ? ', ' + Math.round(deep.deep * 100) + '% into the trunk @' + r3(deep.t) : '') + (near.headGap < 0.15 ? ', nearest the head ' + Math.round(near.headGap * 1000) + ' mm @' + r3(near.t) : ''));
  }
  return out.length ? 'the arms by the model (tools/body.js; limits: elbow ' + limit('ElbowFold', 25) + '°, wrist ' + limit('WristBend', 80) + '°, a bone\'s turn ' + limit('BoneJerk', 600) + ' deg/s, trunk ' + Math.round(limit('InsideDeep', 0.2) * 100) + '%; the head\'s gap is the model\'s, ±' + Math.round(body.HeadOff * 1000) + ' mm on average and up to 59: under that a take judges) — ' + out.join(' · ') : null;
}

// ---------------------------------------------------------------- the same clip for the other side
function mirror(src, name) {
  const swap = s => s.replace(/Left|Right/g, m => m === 'Left' ? 'Right' : 'Left'), lr = w => w === 'left' ? 'right' : w === 'right' ? 'left' : w;
  const modes = {};
  for (const h of src.head) { const w = h.trim().split(/\s+/); if (w[0] === 'mode') modes[w[1]] = w[2]; }
  const B = body.bodyOf(src), STANCE = B.stance;
  const out = { name, head: [], keys: {}, order: [], body: B.name }; // (body: the new clip has no row in the catalogue yet — it is made on its source's body)
  for (const ch of src.order) {
    const to = swap(ch), ks = src.keys[ch];
    let f;
    if (/^@Prop|\.on(\.|$)/.test(ch)) throw new Error('mirror: ' + ch + ' — a thing\'s own frame is not measured: a clip with a thing is not mirrored');
    const ft = /^@(Left|Right)Foot$/.exec(ch);
    if (/\.w$/.test(ch)) f = v => v;
    else if (ft) { const from = STANCE[ft[1]], onto = STANCE[ft[1] === 'Left' ? 'Right' : 'Left']; f = v => [onto[0] - (v[0] - from[0]), onto[1] + (v[1] - from[1]), onto[2] + (v[2] - from[2])]; } // (the game's stance is not symmetric — the left foot ahead: mirrored outright, each foot began 0.58 m from where it stands and they swapped on the way in)
    else if (ch.startsWith('@') || ch === 'move') f = v => [-v[0], v[1], v[2]];                      // a point or a way in the body's frame
    else if (ch === 'body' || modes[ch] === 'body') f = v => [v[0], -v[1], -v[2]];                    // pitch stays, yaw and roll the other way
    else if (modes[ch] === 'aim') f = v => [v[0], -v[1], -v[2]];                                      // up stays, aside and the twist the other way
    else if (/Hand(Thumb|Index|Middle|Ring|Pinky)[123]$/.test(ch)) { if (ks.some(k => Math.abs(k.v[1]) > 1e-6 || Math.abs(k.v[2]) > 1e-6)) throw new Error('mirror: ' + ch + ' spreads (its y or z is not 0): which way a finger spreads on the other hand is not measured — only its bend (x) is the same'); f = v => v; }
    else throw new Error('mirror: ' + ch + ' turns in the bone\'s own axes, and they differ left and right (not measured): give it «mode ' + ch + ' body» keys first, or mirror without it');
    out.keys[to] = ks.map(k => ({ t: k.t, v: f(k.v) })); out.order.push(to);
  }
  for (const h of src.head) {
    const w = h.trim().split(/\s+/);
    if (w[0] === 'clip') out.head.push('clip ' + name);
    else if (w[0] === 'mode' || w[0] === 'follow') out.head.push(swap(h));
    else if (w[0] === 'prop' && w[2]) out.head.push(['prop', w[1], w[2] === 'r' ? 'l' : 'r', ...w.slice(3)].join(' '));
    else if (w[0] === 'say') out.head.push(h + ' (дзеркально)');
    else if (w[0] === 'want') { const m = w.slice(); for (let i = 2; i < m.length; i++) m[i] = lr(m[i]); const ni = m.indexOf('near'); if (ni > 0) m[ni + 1] = String(-parseFloat(m[ni + 1])); if (m[2] === 'look' && m[3] !== 'prop') m[3] = String(-parseFloat(m[3])); out.head.push(m.join(' ')); }
    else if (h.trim().startsWith('# do ')) { const m = h.trim().split(/\s+/); for (let i = 3; i < m.length; i++) m[i] = lr(m[i]); if (m[3] === 'reach' && m[4] !== 'both') m[5] = String(-parseFloat(m[5])); if (m[3] === 'look' && !isNaN(parseFloat(m[4]))) m[4] = String(-parseFloat(m[4])); if (m[3] === 'step') m[5] = String(-parseFloat(m[5])); out.head.push(m.join(' ')); }
    else if (h.trim().startsWith('# scripted:')) out.head.push(swap(h));
    else if (w[0] === 'touch' && w[1] === 'point' && w[2] !== undefined) out.head.push(['touch', 'point', String(-parseFloat(w[2])), ...w.slice(3)].join(' ')); // (the point the hand touches: over with the hand)
    else out.head.push(h);
  }
  if (!out.head.some(h => /^say\s/.test(h.trim()))) out.head.splice(out.head.findIndex(h => /^clip\s/.test(h.trim())) + 1, 0, 'say дзеркало кліпу ' + src.name); // (a clip from before «say»: the new one is asked in these words)
  return out;
}

const FINGERS_SAID = { Right: FINGERS, Left: Object.fromEntries(Object.keys(FINGERS).map(k => [k, Object.assign({}, FINGERS[k], FINGERS_LEFT[k])])) }; // (the shapes as each hand gets them: tools/grip.js holds them against a measured hand)
module.exports = { regen, predict, arms, slower, handSpeed, mirror, standing, scriptOf, setScript, setLine, listOf, aliveOf, parseLine, lengthOf, VERBS, FINGERS_SAID, FINGERS_BODY };
if (require.main === module) { console.error('motion.js is the movement half of the one tool: node tools/anim.js do <clip> "<t0>-<t1> <movement> …"'); process.exit(1); }

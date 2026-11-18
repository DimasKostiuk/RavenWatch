// The body the clip tools reckon with before a take (2026-11-11, the user: «ти не пози прав, а рушій сам» — the generator placed points and
// directions but had no arm in it, so a folded elbow or a twisted wrist was learnt only from a take). Here is the arm as the game plays it:
//   the shoulder     where the upper arm begins, carried by the trunk: «move», then Spine1 and Spine2 («mode … body») turned about two points of the back
//   the elbow        by the game's own two-bone solve (BodyAnimator.Solve in src/BodyClips.cs: the law of cosines, the elbow on the pole's side), the
//                    hand's point eased in by its weight from where it hangs, its target followed by the core's spring (BodyAnimator.Inertia)
//   the wrist        how far the hand is bent off the forearm, as BodyCheck measures it: off the skeleton's own rest, not off a straight line
//   the trunk        the column hips → chest → neck that BodyCheck's «inside» measures a hand and a forearm against
//   the hand's turn  the core's own blend (BodyAnimator.Facing): from how the hand lies on the forearm to what «.f» and «.n» say, by the target's weight
// and from them, for every moment of a clip (sweep): the elbow's angle, the wrist's bend, how far the target is past the arm, how deep the hand or
// the forearm is in the trunk, how fast the arm's bones turn. motion.js uses it twice: predict says [fold], [bend], [miss], [inside] before a take; regen chooses the way of a hand,
// its elbow's side and its turn on the way so that the limits hold. The limits are BodyCheck's own, read from src/BodyCheck.cs — never copied here.
//
// Every number below is MEASURED in takes, none is taken from a clip someone accepted (rows of stage_<clip>.full.txt, a row each 0.25 s, 2 decimals):
//   node tools/body.js        fits them again from the takes on disk and says how far the model is from what the game measured
// As fitted 2026-11-11 on zz_w_scratch, zz_w_pick, zz_w_point (the player's body) and basket_carry (Heid's):
//   arm lengths       0.318 ± 0.005 and 0.260 ± 0.004 m, both arms, both bodies (83 rows)
//   the elbow         9 mm off the measured one on average, 23 mm at most (32 rows with the hand's weight on; the rows are rounded to 10 mm)
//   the shoulder      20 mm rms, 46 mm at most in the middle of a fast lean (54 rows; the reckoning before it — one pivot at the hips — 121 mm rms, 411 at most)
//   the wrist         fitted on two [bend] lines (85.08°, 83.31°) with the measured forearm, then checked on two takes it had not seen (73.8° for a measured
//                     73.54°, 52.9° for 54.06°); from the keys alone, through the whole model, it reads 1.4–2.8° under the take on all four
//   the trunk         the palm's distance off the body in 51 standing poses: 15 mm rms
// The head: a ball (BodyCheck.InHead: a hand or a forearm inside it), carried by the back. Its place is measured (2026-11-12, 180 rows of 15 takes of
// the player's body, the rows in a clip's fade left out; a row's «headball x,y,z r» and «head up,right»):
//   a ball carried by the back, as sweep takes it            32 mm off the rows on average, 83 mm at most
//   a pivot the back carries (-0.005 1.567 0.061) + the head's own MEASURED turn about it (the ball 0.013 0.137 0.082 from the pivot, the face its +z)
//                                                             14 mm, 69 mm at most (held out, 8 takes against 7: 17 mm, 50) — as near as the shoulder is
//   the same with the head's turn said from the KEYS (the rest face carried by the back; «@Look» to its point by its weight, LookMost)
//                                                             26 mm, 68 mm at most: the face is 11 deg off on average, 29 at most
// The head's turn is not the clip's to say: under a look of part weight (the scripts' 0.6) and with no look the game turns the head itself. So the ball
// is the plain carried one, and what it is good for is measured too — the gap between the hand or the forearm and the head's surface, the model's
// against the rows' own (2026-11-12 evening, 344 rows of the takes on disk, 55 of them within 0.15 m of the head: drink, scratch_head, shade_eyes,
// stretch_up, toast, wipe_brow, yawn): 26 mm off on average, 90% under 50, 59 at most; never said «inside» where the game had it outside (0 of 344);
// the three rows the game had inside were 1, 1 and 14 mm deep, and the model had them 19–25 mm outside. Hence three uses, each within that:
//   judge       [head] only where the MODEL's ball has the hand or the forearm inside past BodyCheck.HeadDeep AND deeper than the model's own average
//               error (HeadOff): a gross fault (an arm through the head). 15 mm inside by the model is no finding (shade_eyes 2026-11-12, said so at first).
//               A shallow one — the 16–20 mm of shade_eyes and wipe_brow — is under the model's error: a take's to say. Never read «no [head]» as clear
//   over        a way of a hand keeps HeadClear from the ball, save as near as the movement's own ends are (a reach to the nape ends at the head: that is
//               asked) — regen's choice of a way round the head; of 335 rows the model had 30 mm clear none was inside in the game
//   headGap     in every sample (m off the ball's surface, below zero inside): anim.js check says the nearest, with the model's error beside it
// node tools/body.js says the ball from the takes on disk each run, and how near the carried one is.
// Three bodies (BODIES; a clip's is picked by the body cell of its row in docs/CLIPS.md, ofCell): the player's (the dummy's), Heid's, and the game's
// Dvergr (2026-11-13, the user: a dwarf's clips were keyed by eye on a person's measures — «руки криві»). Everything of a body is its own there: the
// arm's two lengths, the shoulders, where the hands and elbows hang, the back's two points, the trunk's column, the head's ball, the wrist's rest line,
// the feet of its stance, its eyes, the heights the accompaniment goes by. The two human bodies share what was fitted on both (HUMAN). What is measured
// of the dvergr and what is only derived stands over its numbers; against its three takes the model is: the shoulder 6–10 mm off on average (16 at
// most; fitted on these same rows), the elbow by the solve 6–8 mm (19 at most), the wrist 70.4° for a measured 69.76° and 70.96°, 21.5° for 25.26°,
// the tightest elbow 67° for 66.81° and 67.85°, 72° for 70.2°. Its back and its head are NOT proven (no take has turned the one or reached the other).
// node tools/body.js says, a body at a time, how each stands with no clip on a limb («at rest, <body>: …» — what shoulder, restElbow, rest and stance
// are fitted to) and its arm's lengths («in all, <body>: …»).
// NOT in the model:
// a stance other than the game's own (a weapon's: other shoulders), the legs, a body that does not stand («body»/«Hips» keys), a target that rides a bone («follow»).
// A hand on the thing the clip moves («.on», «@Prop»): in the model since 2026-11-11 late (the thing's place and turn from the keys, the hand's point and
// turn on it) — reasoned from BodyAnimator.Targets and Grip, NOT yet held against a take: tools/grip.js says the arm's numbers for a reckoned grip by it.
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const add = (a, b) => a.map((x, i) => x + b[i]), sub = (a, b) => a.map((x, i) => x - b[i]), mul = (a, k) => a.map(x => x * k);
const len = a => Math.hypot(...a), dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const unit = a => { const l = len(a); return l < 1e-9 ? [0, 0, 0] : mul(a, 1 / l); };
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const mix = (a, b, k) => a.map((x, i) => x + (b[i] - x) * k);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const angle = (a, b) => Math.acos(clamp(dot(unit(a), unit(b)), -1, 1)) * 180 / Math.PI;
const SIGN = { Left: -1, Right: 1 };

// What the two human bodies share — fitted on both together (the head of this file):
//   upper, fore   m: shoulder → elbow, elbow → wrist
//   wrist         the forearm's way in the hand's own frame as the skeleton rests (fingers, palm, thumb's side — the left hand: the side mirrored):
//                 BodyCheck measures the wrist's bend off this; on a person it is 10.6° off the fingers' line toward the palm
//   j1, j2        the back: the two points Spine1 and Spine2 turn the shoulders about — fitted to the measured shoulders, so they stand for the bones'
//                 joint work, they are not the bones' own places
//   trunk         the trunk's column (BodyCheck.CheckIn: hips → chest → neck; its radius TrunkRound × the shoulders' width)
//   stance        the game's stance, the feet (what «@LeftFoot», «@RightFoot» of a clip that stands begin with)
//   eyes          where a look said by its way starts from («look left 40»); ahead of it the head looks when nothing is said
//   pivot         a new clip's «pivot»: m up from the feet, what the body turns round
//   heights       m up from the feet where the accompaniment of tools/motion.js changes its mind: a hand under «low» bends the trunk, under «knee»
//                 lowers the hips, over «up» lifts its shoulder, over «high» arches the back; a point under «seen» (and ahead) is looked at
const HUMAN = {
  upper: 0.318, fore: 0.26,
  wrist: [0.983, 0.167, -0.079],
  j1: [0, 1.2, -0.1], j2: [0, 1.44, 0.02],
  trunk: { hips: [-0.04, 0.84, -0.1], chest: [-0.04, 1.1, -0.06], neck: [-0.04, 1.5, 0.08] },
  stance: { Left: [-0.09, 0.12, 0.28], Right: [0.05, 0.12, -0.3] },
  eyes: [0, 1.6, 0], pivot: 1.1,
  heights: { low: 0.8, knee: 0.75, up: 1.45, high: 1.6, seen: 1.35 },
};
const TrunkRound = 0.32; // (BodyCheck's own share of the shoulders' width, the same for every body: the game measures a dvergr's coat by its bones too)
// a body's own numbers, in its frame (x right, y up, z ahead), in the game's stance (the left foot ahead: the body is not symmetric in it).
// name: the word of «anim.js new --body» and of a kept hand (tools/grip_hands.json) · stage: the mannequin a take stands it on («raven stage up …»)
const BODIES = {
  player: Object.assign({ name: 'player', stage: 'dummy' }, HUMAN, { // (the dummy and the player: zz_w_scratch, zz_w_pick, zz_w_point)
    shoulder: { Left: [-0.226, 1.472, 0.069], Right: [0.179, 1.472, -0.054] },
    rest: { Left: [-0.34, 0.98, 0.22], Right: [0.32, 0.99, -0.25] },          // where a hand hangs
    restElbow: { Left: [-0.35, 1.18, 0.05], Right: [0.23, 1.23, -0.26] },
    head: { c: [0.001, 1.702, 0.135], r: 0.108 }, // the ball's middle as the body stands, its radius (277 rows standing straight; the head of this file says how near it is and what it is used for)
  }),
  heid: Object.assign({ name: 'heid', stage: 'heid' }, HUMAN, { // (basket_carry: narrower shoulders)
    shoulder: { Left: [-0.2, 1.47, 0.05], Right: [0.155, 1.47, -0.05] },
    rest: { Left: [-0.31, 0.98, 0.21], Right: [0.28, 0.98, -0.23] },
    restElbow: { Left: [-0.31, 1.18, 0.04], Right: [0.2, 1.22, -0.24] },
    head: { c: [-0.007, 1.693, 0.143], r: 0.108 }, // (10 rows standing straight: few)
  }),
  // The game's Dvergr, as its mannequin stands on the stage (2026-11-13; it stands on the player's animations retargeted to its own skeleton: hips
  // 0.49 m, a lean of 6° forward). MEASURED — in the rows of three takes (dv_hand_belt twice, dv_arms_cross; 88 rows, the limbs no clip held then:
  // «at rest, dvergr» of node tools/body.js says each again with its spread, 4–14 mm), and in the game's prefab (the bones of its rest pose). The
  // mannequin stood on a slope in all three (the verdicts: «the ground … is not flat», 0.1–0.16 m): arms and shoulders are measured in the body's
  // frame and came out the same to 13 mm as in two takes whose verdict had no such line; the feet of «stance» are the ones to measure again on flat ground.
  //   upper, fore       the prefab's bones 0.386 and 0.253; the takes' rows the same: 0.386 ± 0.004, 0.253 ± 0.004 (176 rows)
  //   shoulder, rest, restElbow, stance      the rows
  //   head              the rows' ball — but the game GUESSES it for this body (its skin could not be read: BodyCheck.HeadUp over the head's bone,
  //                     BodyCheck.HeadRound × the shoulders' width). It is the ball the game judges [head] by, so the model keeps it; the head one
  //                     sees is not measured, and 0.2 m is wide for it (the ball comes down to the shoulders' height)
  //   wrist             fitted on the four bends those takes measured — 70.96° and 69.76° (one pose twice), 25.26°, 23.55° — with the measured
  //                     forearms: it says them back within 3° (1.8 rms) and lies 6° off the fingers' own way, as a person's lies 11° off; a
  //                     person's line read those bends 10° rms off. Three poses for two numbers: a near fit, not a proven one. (Takes before
  //                     11:00 that day are of a core that turned no dvergr hand at all — BodyAnimator.HandFacing found no fingers on its
  //                     skeleton — and read 80–134° there: none of them is in the fit, and none is kept on disk)
  //   trunk.hips        the stage's own row of its bones («raven npc gait stage»)
  // DERIVED, not measured — no take has turned this body's back or sent a hand to its head yet:
  //   trunk.chest, .neck  the prefab's Spine2 and Neck (0.872, 1.168 m at rest) lowered as the hips are in the stance (0.549 → 0.49) and leant 6°
  //   j1, j2            a person's two points at the same shares of the way from the hips to the shoulders (57% and 95%), on the trunk's own line
  //   eyes              the head's bone as it stands: the game's guessed ball less BodyCheck.HeadUp
  //   pivot             what the dv_ clips made by hand have
  //   heights           a person's × the shoulders' height (1.07 / 1.47)
  // Its trunk is far wider and deeper than its bones say (a coat, a belly, a beard down to the chest): the column here is BodyCheck's own (bones,
  // TrunkRound × the shoulders), so [inside] says what the game will say, NOT that a hand is clear of the coat — the user accepted a hand on its belt
  // at the wrist ∓0.22 0.73 -0.03 (dv_hand_belt), 0.2 m from the column's middle.
  dvergr: { name: 'dvergr', stage: 'dummy_dvergr',
    upper: 0.386, fore: 0.253,
    shoulder: { Left: [-0.25, 1.073, 0.11], Right: [0.224, 1.058, -0.028] },
    rest: { Left: [-0.422, 0.562, 0.295], Right: [0.418, 0.555, -0.253] },
    restElbow: { Left: [-0.418, 0.726, 0.102], Right: [0.3, 0.776, -0.28] },
    head: { c: [-0.007, 1.256, 0.056], r: 0.198 },
    wrist: [0.994, -0.035, -0.105],
    j1: [0, 0.82, -0.03], j2: [0, 1.04, 0.03],
    trunk: { hips: [-0.02, 0.49, -0.03], chest: [-0.02, 0.81, 0], neck: [-0.01, 1.11, 0.04] },
    stance: { Left: [-0.06, 0.13, 0.22], Right: [0.1, 0.1, -0.18] },
    eyes: [0, 1.16, 0], pivot: 0.75,
    heights: { low: 0.58, knee: 0.54, up: 1.05, high: 1.16, seen: 0.98 },
  },
};
const Warm = 1;                                     // s of the clip before a stretch asked that are reckoned too: the spring (2 Hz, damped 0.65) is settled to a millimetre in that
const HeadClear = 0.05, HeadOff = 0.026;            // m a hand's way keeps off the head's ball (90% of the model's gaps are nearer the game's than this) · m the model's gap is off on average: said with the number
const HangF = [0, -1, 0.1];                          // where a hanging hand's fingers go in the game's stance (its palm: to the body)
const Dt = 0.05, InertiaHz = 2, InertiaDamp = 0.65; // s a sample · the core's spring (BodyAnimator.InertiaHz, InertiaDamp: read below where they can be)
// BodyCheck's «jerk» is a bone's whole turning speed, its twist too; the model has the arm's swing and the hand's blended turn. The two factors are
// FITTED (2026-11-12, a take of each of 40 standing clips, 71 arms; the fastest of an arm's three bones in the take against the model's raw speeds):
//   the arm's bones ÷ the model's swing     median ×1.25, three quarters under ×1.53, nine tenths under ×1.78
//   the hand's bone ÷ the model's hand turn median ×1.16, three quarters under ×1.32, nine tenths under ×1.45
//   … and apart by what the hand's weight does at that moment (65 bones over 250 deg/s): coming on — median ×1.25, three quarters under ×1.37;
//   going off — median ×1.49, three quarters under ×1.78 (zz_t9 the same day: a way back the one factor called clean, 528, measured 692). Hence
//   TwistFall for the moments the weight falls.
// With ×1.4 and ×1.2 the model is right about «over BoneJerk or not» for 67 of the 71 arms: 1 said where the game had none (listen_in), 3 missed
// (stretch_up, key_turn, stir: quick turns of a bone round itself, which a swing does not show). Before the fit (×2 and ×1.35, the most the first three
// takes had measured) it was right for 51: 18 said where the game had none. Two things the same takes mended in the hand's turn: under a clip the hand
// keeps the turn it HANGS with (the solve leaves the end bone's turn in the world as it was), it does not lie along the forearm — arms_cross read
// 1622 deg/s for the game's 194; and a hand on a thing is turned by the thing's frame (Grip), which this blend is not: no hand turn is said of it.
// node tools/body.js says the fit against every take kept on disk («the bones' speed»): look there before changing either number.
const Twist = 1.4, TwistFall = 1.8, HandTwist = 1.2, FoldRoom = 5, ShoulderOff = 0.05; // · deg the model's elbow may be off (measured: 1–5°) · m its shoulder may be off (46 mm at most, in a fast deep lean): a target is said past the arm only beyond this

let _src = {};
const _num = {}, _pole = {};
function number(file, name, def) { // a number of the core, read where it lives (once a run: the file is searched for it one time)
  const key = file + " " + name; if (key in _num) return _num[key] === null ? def : _num[key];
  if (!_src[file]) _src[file] = fs.readFileSync(path.join(root, 'src', file), 'utf8');
  const m = new RegExp('\\b' + name + ' = (-?[0-9.]+)f').exec(_src[file]);
  _num[key] = m ? parseFloat(m[1]) : null;
  return m ? _num[key] : def;
}
const limit = (name, def) => number('BodyCheck.cs', name, def);
function poleOf(side) { // a limb's own pole when its clip says none (BodyClips.Limbs)
  if (_pole[side]) return _pole[side];
  if (!_src['BodyClips.cs']) _src['BodyClips.cs'] = fs.readFileSync(path.join(root, 'src', 'BodyClips.cs'), 'utf8');
  const m = new RegExp('Name = "@' + side + 'Hand"[^}]*Pole = new Vector3\\((-?[0-9.]+)f, (-?[0-9.]+)f, (-?[0-9.]+)f\\)').exec(_src['BodyClips.cs']);
  return _pole[side] = m ? [+m[1], +m[2], +m[3]] : [SIGN[side] * 0.5, -0.7, -0.5];
}

// Quaternion.Euler(x, y, z) on a vector: round z, then x, then y (x +: the front goes down · y +: to the right · z +: the right side goes up)
function turn(e, v) {
  const [x, y, z] = e.map(d => d * Math.PI / 180);
  let [a, b, c] = v;
  [a, b] = [a * Math.cos(z) - b * Math.sin(z), a * Math.sin(z) + b * Math.cos(z)];
  [b, c] = [b * Math.cos(x) - c * Math.sin(x), b * Math.sin(x) + c * Math.cos(x)];
  [a, c] = [a * Math.cos(y) + c * Math.sin(y), -a * Math.sin(y) + c * Math.cos(y)];
  return [a, b, c];
}
// a point of the upper body carried by the trunk: Spine1 turns all above the body's j1, Spine2 all above its j2, «move» all of it
function carried(p, s1, s2, mv, hangs, B) { // (hangs: a point of the arm — it hangs from the shoulder, so the whole back carries it however low it is · B: whose back, the player's when none is said)
  const J1 = (B || BODIES.player).j1, J2 = (B || BODIES.player).j2;
  let q = p;
  if (hangs || p[1] >= J2[1]) q = add(add(J1, turn(s1, sub(J2, J1))), turn(s2, turn(s1, sub(p, J2))));
  else if (p[1] >= J1[1]) q = add(J1, turn(s1, sub(p, J1)));
  return add(q, mv);
}
// BodyAnimator.Solve: where the elbow is and where the hand gets to (a target past the arm: as far as the arm goes toward it)
function solve(sh, target, pole, B) { // (B: whose arm — its two lengths; the player's when none is said)
  const UPPER = (B || BODIES.player).upper, FORE = (B || BODIES.player).fore;
  const to = sub(target, sh), l = len(to);
  if (l < 0.001) return { elbow: add(sh, [0, -UPPER, 0]), hand: target, past: 0 };
  const d = clamp(l, Math.abs(UPPER - FORE) + 0.001, UPPER + FORE - 0.001), dir = mul(to, 1 / l);
  const cos = clamp((UPPER * UPPER + d * d - FORE * FORE) / (2 * UPPER * d), -1, 1), sin = Math.sqrt(1 - cos * cos);
  let side = sub(pole, mul(dir, dot(pole, dir)));
  if (dot(side, side) < 0.0001) side = cross(dir, [1, 0, 0]);
  return { elbow: add(add(sh, mul(dir, UPPER * cos)), mul(unit(side), UPPER * sin)), hand: add(sh, mul(dir, d)), past: Math.max(0, l - (UPPER + FORE)) };
}
// the wrist's bend, deg: the hand turned as BodyAnimator.Facing turns it (fingers f, palm n made square to them) against the forearm's way
function wristBend(f, n, fore, side, B) { // (B: whose wrist — the line its bend is measured off; the player's when none is said)
  const WRIST = (B || BODIES.player).wrist;
  f = unit(f); const p = unit(sub(n, mul(f, dot(n, f)))), s = cross(p, f);
  return angle(add(add(mul(f, WRIST[0]), mul(p, WRIST[1])), mul(s, WRIST[2] * SIGN[side])), fore);
}
// … and the fingers' way that bends it no more than «most»: f turned toward the way of no bend at all, by as little as it takes
function unbent(f, n, fore, side, most, B) {
  const b = wristBend(f, n, fore, side, B), WRIST = (B || BODIES.player).wrist;
  if (b <= most) return unit(f);
  // (the way of no bend: the hand's frame turned whole so that its rest line lies on the forearm — near enough: f turned by the same turn that takes the rest line to the forearm)
  const fu = unit(f), p = unit(sub(n, mul(fu, dot(n, fu)))), s = cross(p, fu);
  const line = unit(add(add(mul(fu, WRIST[0]), mul(p, WRIST[1])), mul(s, WRIST[2] * SIGN[side]))), to = unit(fore);
  const axis = unit(cross(line, to)), k = (b - most) * Math.PI / 180;
  if (len(axis) < 0.5) return fu;
  return unit(add(add(mul(fu, Math.cos(k)), mul(cross(axis, fu), Math.sin(k))), mul(axis, dot(axis, fu) * (1 - Math.cos(k))))); // (Rodrigues)
}
// turns as quaternions [x, y, z, w]: the hand's own turn is a blend of two (BodyAnimator.Facing: Slerp by the target's weight from the pose under it)
const qMul = (a, b) => [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0], a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]];
const qInv = q => [-q[0], -q[1], -q[2], q[3]];
const qAngle = (a, b) => 2 * Math.acos(clamp(Math.abs(a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3]), 0, 1)) * 180 / Math.PI;
function qSlerp(a, b, k) { // (the short way, as Unity's)
  let d = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3]; if (d < 0) { b = b.map(x => -x); d = -d; }
  if (d > 0.9995) { const q = a.map((x, i) => x + (b[i] - x) * k), l = Math.hypot(...q); return q.map(x => x / l); }
  const th = Math.acos(d), sa = Math.sin((1 - k) * th) / Math.sin(th), sb = Math.sin(k * th) / Math.sin(th);
  return a.map((x, i) => x * sa + b[i] * sb);
}
function qFrame(f, n) { // the turn whose forward is f and whose up is n made square to it (Quaternion.LookRotation)
  f = unit(f); const u = unit(sub(n, mul(f, dot(n, f)))), r = cross(u, f), m = [r[0], u[0], f[0], r[1], u[1], f[1], r[2], u[2], f[2]], tr = m[0] + m[4] + m[8];
  if (tr > 0) { const k = Math.sqrt(tr + 1) * 2; return [(m[7] - m[5]) / k, (m[2] - m[6]) / k, (m[3] - m[1]) / k, k / 4]; }
  if (m[0] > m[4] && m[0] > m[8]) { const k = Math.sqrt(1 + m[0] - m[4] - m[8]) * 2; return [k / 4, (m[1] + m[3]) / k, (m[2] + m[6]) / k, (m[7] - m[5]) / k]; }
  if (m[4] > m[8]) { const k = Math.sqrt(1 + m[4] - m[0] - m[8]) * 2; return [(m[1] + m[3]) / k, k / 4, (m[5] + m[7]) / k, (m[2] - m[6]) / k]; }
  const k = Math.sqrt(1 + m[8] - m[0] - m[4]) * 2; return [(m[2] + m[6]) / k, (m[5] + m[7]) / k, k / 4, (m[3] - m[1]) / k];
}
const toSegment = (p, a, b) => { const ab = sub(b, a), k = clamp(dot(sub(p, a), ab) / Math.max(1e-9, dot(ab, ab)), 0, 1); return len(sub(p, add(a, mul(ab, k)))); };

// a channel's value at t by the curve the game plays («curve ease»: each key's speed from its neighbours, none at a turn or a hold) — a Hermite curve
// with those speeds. The game's own is in BodyClips; this one is near it, and a take still measures the truth
function eased(keys, t, def) {
  if (!keys || !keys.length) return def;
  const k = keys;
  if (t <= k[0].t) return k[0].v;
  if (t >= k[k.length - 1].t) return k[k.length - 1].v;
  let i = 0; while (i + 1 < k.length && k[i + 1].t < t) i++;
  const a = k[i], b = k[i + 1], h = b.t - a.t, u = (t - a.t) / h;
  const slope = (j, n) => { if (j <= 0 || j >= k.length - 1) return 0; const d0 = k[j].v[n] - k[j - 1].v[n], d1 = k[j + 1].v[n] - k[j].v[n]; return d0 * d1 <= 0 ? 0 : (k[j + 1].v[n] - k[j - 1].v[n]) / (k[j + 1].t - k[j - 1].t); };
  return a.v.map((_, n) => { const m0 = slope(i, n) * h, m1 = slope(i + 1, n) * h, u2 = u * u, u3 = u2 * u; return (2 * u3 - 3 * u2 + 1) * a.v[n] + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * b.v[n] + (u3 - u2) * m1; });
}

// whose body a clip is made on, by the body cell of its row in docs/CLIPS.md: it begins «двергр» → the dvergr's, it names «Гейд» → hers, else the
// player's, which is the dummy's. The one place that reads the cell: the model (bodyOf), the take's mannequin (take.js), the kept hand (grip.js).
// A clip not in the catalogue yet (a mirror being made, a take's saved keys) says its body itself: c.body, a name of BODIES
const ofCell = cell => /^\s*двергр/.test(cell || '') ? BODIES.dvergr : /Гейд/.test(cell || '') ? BODIES.heid : BODIES.player;
let _rows = null;
function bodyOf(c) {
  if (c.body && BODIES[c.body]) return BODIES[c.body];
  if (_rows === null) { try { _rows = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n'); } catch (e) { _rows = []; } } // quiet: no catalogue — the player's body
  const row = _rows.find(l => l.startsWith('| `' + c.name + '` |'));
  return ofCell(row ? row.split('|')[3] : '');
}

// One arm through the clip, a sample each Dt: what the keys make of it in the game. Each sample:
//   t · w (the target's weight) · held (w ≥ BodyCheck.Full, not on a thing: the wrist and the miss are judged) · sh, elbow, hand · target
//   fold (the elbow's inner angle, 180 straight) · past (m the target is past the arm) · bend (the wrist, deg; null: no .f/.n, or not held)
//   turned (the wrist's bend were the hand's turn fully on: the way in and out, where the take does not judge it) · spin (deg/s a take is reckoned to
//   measure on the arm's fastest bone) · spins (which: the arm swings, or the hand turns)
//   deep (how deep in the trunk's column, 0 its surface, 1 the spine: the worse of the hand and the forearm's middle) · what (which of the two)
// null: the arm is not the model's (no target of it, the body does not stand by the trunk's keys — «body», «Hips» —, its target rides a bone: «follow»)
function sweep(c, side, from, to, trunkAt) {
  const ch = '@' + side + 'Hand', K = c.keys, B = bodyOf(c);
  const aimed = !!(K[ch] && K[ch].length); // (a target of its own; with none, a hand on the thing goes by «.on.w» alone: BodyAnimator.Targets)
  if ((!aimed && !(K[ch + '.on'] && K[ch + '.on'].length && K['@Prop'] && K['@Prop'].length)) || K.body || K.Hips || c.head.some(h => h.trim().split(/\s+/)[0] === 'follow' && h.includes(ch))) return null;
  const length = parseFloat((c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'length') || [0, 0])[1]);
  const sorted = n => K[n] && K[n].length ? K[n].slice().sort((a, b) => a.t - b.t) : null;
  const tg = sorted(ch), wk = sorted(ch + '.w'), pk = sorted(ch + '.p'), fk = sorted(ch + '.f'), nk = sorted(ch + '.n'), on = sorted(ch + '.on.w');
  // a hand on the thing the clip moves (BodyAnimator.Targets, Grip, PropAt): its target goes over to «.on» — a point of the thing, which is at «@Prop»,
  // turned by «@Prop.r», by «@Prop.w» from where it lay (on the stage: where «@Prop» says at the clip's start) — and its turn to «.on.f», «.on.n»
  const onP = sorted(ch + '.on'), onF = sorted(ch + '.on.f'), onN = sorted(ch + '.on.n'), pAt = sorted('@Prop'), pR = sorted('@Prop.r'), pW = sorted('@Prop.w');
  // (where it lay: where «@Prop» says at the clip's start, as the stage lays it; its turn then: square before the body — none —, BodyAnimator.PropRest)
  const thingAt = (t, shown) => { const k = (pW ? clamp(eased(pW, t, [1])[0], 0, 1) : 1) * shown; return { at: mix(pAt[0].v, eased(pAt, t), k), r: pR ? mul(eased(pR, t, [0, 0, 0]), k) : [0, 0, 0] }; };
  const bodyMode = b => c.head.some(h => h.trim().replace(/\s+/g, ' ') === 'mode ' + b + ' body');
  const s1k = bodyMode('Spine1') ? sorted('Spine1') : null, s2k = bodyMode('Spine2') ? sorted('Spine2') : null, mk = sorted('move');
  const inertia = c.head.some(h => /^touch\s/.test(h.trim())) ? 0 : parseFloat((c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'inertia') || [0, 1])[1]);
  const fade = c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'fade'), fadeIn = fade ? parseFloat(fade[1]) : number('BodyClips.cs', 'EasedFade', 0.7); // (the clip's way in: every target's weight × how far the clip is shown, on its S-curve — its way out comes after its length)
  const full = limit('Full', 0.95), own = poleOf(side), r = TrunkRound * len(sub(B.shoulder.Left, B.shoulder.Right)), TRUNK = B.trunk, reach = B.upper + B.fore;
  const out = [], om = 2 * Math.PI * number('BodyClips.cs', 'InertiaHz', InertiaHz), damp = number('BodyClips.cs', 'InertiaDamp', InertiaDamp);
  let x = null, v = [0, 0, 0], was = null, w = 0;
  const first = from === undefined ? 0 : Math.max(0, Math.floor((from - Warm) / Dt)), last = to === undefined ? length : Math.min(length, to); // (a stretch asked: only it, and Warm s before it for the spring to be where it would be)
  for (let i = first; i * Dt <= last + 1e-6; i++) {
    const t = Math.round(i * Dt * 1000) / 1000;
    const shown = fadeIn > 0 && t < fadeIn ? (t / fadeIn) * (t / fadeIn) * (3 - 2 * t / fadeIn) : 1;
    w = clamp(eased(wk, t, [1])[0], 0, 1) * shown;
    const ow = on && onP && pAt ? clamp(eased(on, t, [0])[0], 0, 1) * shown : 0, th = ow > 0.001 ? thingAt(t, shown) : null;
    const onIt = th ? add(th.at, turn(th.r, eased(onP, t))) : null, at = th ? (aimed ? mix(eased(tg, t, B.rest[side]), onIt, ow) : onIt) : eased(tg, t, B.rest[side]);
    if (!aimed) w = ow;
    const gripped = th && onF && onN, fNow = gripped ? turn(th.r, eased(onF, t)) : fk && nk ? eased(fk, t) : null, nNow = gripped ? turn(th.r, eased(onN, t)) : fk && nk ? eased(nk, t) : null;
    // (the core's spring: the target followed, a little overshoot, settled — it starts where it is asked)
    if (x === null || inertia <= 0 || w < 0.01 || th) { x = at; v = [0, 0, 0]; } // (on a thing the core's spring is off: the hand stays on its rim)
    else for (let k = 0; k < 3; k++) { const h = Dt / 3, a = sub(mul(sub(at, x), om * om), mul(v, 2 * damp * om)); v = add(v, mul(a, h)); x = add(x, mul(v, h)); }
    const target = mix(at, x, inertia);
    const said = trunkAt ? trunkAt(t) : null, s1 = said ? said.s1 : eased(s1k, t, [0, 0, 0]), s2 = said ? said.s2 : eased(s2k, t, [0, 0, 0]), mv = said ? said.mv : eased(mk, t, [0, 0, 0]);
    const sh = carried(B.shoulder[side], s1, s2, mv, false, B), under = carried(B.rest[side], s1, s2, mv, true, B), underElbow = carried(B.restElbow[side], s1, s2, mv, true, B);
    // (the pole: the clip's, from the way the elbow points in the pose under it, by the target's weight — BodyAnimator.Targets)
    let pole = unit(pk ? eased(pk, t, own) : own);
    { const chord = unit(sub(under, sh)); let bent = sub(underElbow, sh); bent = sub(bent, mul(chord, dot(bent, chord))); if (len(bent) > 0.02) pole = unit(mix(unit(bent), pole, w)); }
    const point = mix(under, target, w), arm = solve(sh, point, pole, B), thing = on && eased(on, t, [0])[0] > 0.5, held = w >= full && (!thing || !!th); // (on a thing whose place the clip does not say — no «@Prop», no «.on» point — the hand is not the model's)
    const fore = sub(arm.hand, arm.elbow), up = sub(arm.elbow, sh);
    // (deg/s the upper arm or the forearm swings, or the hand turns: BodyCheck's «jerk» counts the bones' twist too, hence the fitted factors. The hand's
    //  turn is the core's own blend (BodyAnimator.Facing) — from the turn the hand hangs with, which the solve leaves it, to what .f and .n say, by the
    //  target's weight: where the two are far apart, the blend swings round as the weight comes and goes — zz_scratch 2026-11-11, 761 deg/s on the way back)
    const lies = qFrame(HangF, [-SIGN[side], 0, 0.001]), asked = fNow && !th ? qFrame(fNow, nNow) : null; // (on a thing: not this blend — the head of this file)
    const hq = asked && w > 0 ? qMul(qSlerp([0, 0, 0, 1], qMul(asked, qInv(lies)), w), lies) : lies;
    const falls = was && w < was.w - 0.002, twist = falls ? TwistFall : Twist; // (the hand let go: the arm drops into the pose under it, and its bones turn round themselves more than on the way out)
    const swing = was ? Math.max(angle(fore, was.fore), angle(up, was.up)) / Dt * twist : 0, turns = was ? qAngle(hq, was.hq) / Dt * HandTwist : 0, spin = Math.max(swing, turns);
    const swingRaw = swing / twist, turnRaw = turns / HandTwist;
    was = { fore, up, hq, w };
    if (t < (from === undefined ? 0 : from) - 1e-6 || t > (to === undefined ? length : to) + 1e-6) continue;
    const turned = fNow && w > 0 ? wristBend(fNow, nNow, fore, side, B) : null;
    const trunk = [add(TRUNK.hips, mv), carried(TRUNK.chest, s1, s2, mv, false, B), carried(TRUNK.neck, s1, s2, mv, false, B)];
    const deepOf = p => 1 - Math.min(toSegment(p, trunk[0], trunk[1]), toSegment(p, trunk[1], trunk[2])) / r;
    const dh = deepOf(arm.hand), df = deepOf(mix(arm.elbow, arm.hand, 0.5));
    const hc = B.head ? carried(B.head.c, s1, s2, mv, false, B) : null; // (the head's ball, carried by the back: how far the hand and the forearm are off its surface, m — below zero inside; how deep, 0 its surface, 1 its middle)
    const gh = hc ? len(sub(arm.hand, hc)) - B.head.r : 9, gf = hc ? toSegment(hc, arm.elbow, arm.hand) - B.head.r : 9, hh = hc ? -gh / B.head.r : 0, hf = hc ? -gf / B.head.r : 0;
    out.push({
      t, w, held, sh, elbow: arm.elbow, hand: arm.hand, target: at, fore,
      fold: angle(sub(sh, arm.elbow), fore), past: held ? Math.max(0, len(sub(at, sh)) - reach) : 0,
      bend: held ? turned : null, turned, spin, spins: turns > swing ? 'hand turns' : 'arm swings', swingRaw, turnRaw,
      deep: Math.max(dh, df), what: dh >= df ? 'hand' : 'forearm', head: Math.max(hh, hf), headWhat: hh >= hf ? 'hand' : 'forearm', headGap: Math.min(gh, gf),
    });
  }
  return out;
}

// the worst of a sweep against BodyCheck's limits: { fold, bend, past, deep, spin } each { v, t, … } or null where the limit holds
function judge(samples) {
  const worst = { fold: null, bend: null, past: null, deep: null, spin: null, head: null };
  if (!samples) return worst;
  const lim = { fold: limit('ElbowFold', 25), bend: limit('WristBend', 80), past: limit('MissFar', 0.05) + ShoulderOff, deep: limit('InsideDeep', 0.2), spin: limit('BoneJerk', 600), head: limit('HeadDeep', 0.05) };
  for (const s of samples) {
    if (s.fold < lim.fold && (!worst.fold || s.fold < worst.fold.v)) worst.fold = { v: s.fold, t: s.t, lim: lim.fold };
    if (s.bend !== null && s.bend > lim.bend && (!worst.bend || s.bend > worst.bend.v)) worst.bend = { v: s.bend, t: s.t, lim: lim.bend, fore: unit(s.fore) };
    if (s.past > lim.past && (!worst.past || s.past > worst.past.v)) worst.past = { v: s.past, t: s.t, lim: lim.past, sh: s.sh, target: s.target };
    if (s.deep > lim.deep && (!worst.deep || s.deep > worst.deep.v)) worst.deep = { v: s.deep, t: s.t, lim: lim.deep, what: s.what };
    if (s.spin > lim.spin && (!worst.spin || s.spin > worst.spin.v)) worst.spin = { v: s.spin, t: s.t, lim: lim.spin, what: s.spins };
    if (-s.headGap > HeadOff && s.head > lim.head && (!worst.head || s.head > worst.head.v)) worst.head = { v: s.head, t: s.t, lim: lim.head, what: s.headWhat, gap: s.headGap, off: HeadOff };
  }
  // (the wrist: how long it is over, not only its worst moment — a wrist bent a whole hold long is the pose, and one key mended leaves the rest: scratch_head 2026-11-11)
  if (worst.bend) { const over = samples.filter(s => s.bend !== null && s.bend > lim.bend); worst.bend.from = over[0].t; worst.bend.to = over[over.length - 1].t; worst.bend.span = over.length * Dt; }
  return worst;
}
// how far a sweep is over the limits, as one number (0: all hold) — what regen chooses a way by: degrees, with a metre past the arm or a whole depth
// of the trunk counted as 100°. «room» keeps that much of each limit free (a take's body is not the model's to the degree)
function over(samples, room) {
  if (!samples) return 0;
  const k = room === undefined ? 0 : room, lf = limit('ElbowFold', 25) + (k > 0 ? Math.max(limit('ElbowFold', 25) * k, FoldRoom) : 0), lb = limit('WristBend', 80) * (1 - k), ld = limit('InsideDeep', 0.2) * (1 - k), lp = limit('MissFar', 0.05), ls = limit('BoneJerk', 600) * (1 - k);
  // (the head: the way keeps HeadClear off its ball — a centimetre nearer counts as 10° — but no farther than the movement's own ends are: a reach to the
  //  nape ends at the head and a rest begins there, the script asks that; what is on the way must not come nearer than they)
  const need = s => Math.max(0, HeadClear - s.headGap), nearEnds = samples.length ? Math.max(need(samples[0]), need(samples[samples.length - 1])) : 0;
  let sum = 0;
  for (const s of samples) sum += Math.max(0, lf - s.fold) + (s.bend !== null ? Math.max(0, s.bend - lb) : 0) + 100 * Math.max(0, s.deep - ld) + 100 * Math.max(0, s.past - lp) + 0.1 * Math.max(0, s.spin - ls) + 1000 * Math.max(0, HeadClear - s.headGap - nearEnds);
  return sum * Dt;
}

module.exports = { BODIES, SIGN, Dt, HeadClear, HeadOff, eased, limit, bodyOf, ofCell, carried, solve, wristBend, unbent, sweep, judge, over, turn };

// ---------------------------------------------------------------- node tools/body.js: the model against what the takes on disk measured
if (require.main === module) {
  const clip = require('./clip.js'), studio = fs.readFileSync(path.join(__dirname, 'studio.path'), 'utf8').trim();
  const out = path.join(studio, 'out'), takes = path.join(studio, '..', 'player_anims_takes');
  const mean = a => a.reduce((x, y) => x + y, 0) / Math.max(1, a.length), mm = v => Math.round(v * 1000) + ' mm';
  const spins = [], all = {}, heads = {}, rests = {}; // (a body each: its arm's lengths and how near the model is, its head's ball, how it stands with no clip on a limb)
  for (const who of Object.keys(BODIES)) { all[who] = { upper: [], fore: [], elbow: [], shoulder: [] }; heads[who] = []; rests[who] = {}; }
  // (every take whose numbers are kept — player_anims_measures/<clip>/<NNN>.full.txt, the keys it measured in player_anims_takes/<clip>/<NNN>.txt:
  //  tools/take.js keepMeasure — and the reports still in studio/out; one take once)
  const kept = [], seenTake = new Set();
  const measures = path.join(studio, '..', 'player_anims_measures');
  if (fs.existsSync(measures)) for (const d of fs.readdirSync(measures)) { const dir = path.join(measures, d); if (!fs.statSync(dir).isDirectory()) continue; for (const f of fs.readdirSync(dir)) if (/^\d+\.full\.txt$/.test(f)) kept.push({ name: d, file: path.join(dir, f), keys: path.join(takes, d, f.replace('.full.txt', '.txt')) }); }
  for (const file of fs.readdirSync(out).filter(f => /^stage_.*\.full\.txt$/.test(f)).sort()) kept.push({ name: file.slice(6, -9), file: path.join(out, file) });
  for (const one of kept.sort((x, y) => x.file < y.file ? -1 : 1)) {
    const name = one.name, text = fs.readFileSync(one.file, 'utf8'), take = /, take (\d+),/.exec(text);
    const version = (/, file ([0-9a-f]{8}) saved/.exec(text) || [])[1] || (take ? 'take ' + take[1] : one.file); // (one version of the keys once: a kept report and the same one still in studio/out)
    if (seenTake.has(name + ' ' + version)) continue;
    seenTake.add(name + ' ' + version);
    const saved = one.keys || (take ? path.join(takes, name, String(take[1]).padStart(3, '0') + '.txt') : null); // (a kept report: its keys by name; one in studio/out: by its take's number — right only while no other file sits in the takes folder)
    if (!saved || !fs.existsSync(saved)) { console.log(name + ': the clip of its take is not kept (' + (saved || 'no take number') + ') — left out'); continue; }
    const c = clip.parse(fs.readFileSync(saved, 'utf8'), name), rows = [];
    { const at = text.split('\n').find(l => /^0\.[57]\d?\s+turned/.test(l)) || '', high = /Rshoulder -?[0-9.]+,(-?[0-9.]+),/.exec(at); // (the take's own body, by its shoulders — how high, then how wide: the catalogue may name another)
      c.body = high && +high[1] < 1.3 ? 'dvergr' : /Rshoulder 0\.1[0-6]/.test(at) ? 'heid' : 'player'; }
    const B = BODIES[c.body], A = all[c.body], R = rests[c.body];
    for (const l of text.split('\n')) {
      const m = /^([0-9.]+)\s+turned/.exec(l); if (!m) continue;
      const r = { t: +m[1] };
      for (const k of ['Lhand', 'Rhand', 'Lelbow', 'Relbow', 'Lshoulder', 'Rshoulder', 'Lfoot', 'Rfoot']) { const q = new RegExp(k + ' (-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+)').exec(l); if (q) r[k] = q.slice(1).map(Number); }
      if (r.Rshoulder) rows.push(r);
      const hb = /headball (-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+) r ([0-9.]+)( guessed)?/.exec(l), ln = /lean (-?[0-9.]+),(-?[0-9.]+)/.exec(l);
      if (hb && r.Rshoulder) heads[c.body].push({ c: hb.slice(1, 4).map(Number), r: +hb[4], guessed: !!hb[5], lean: ln ? Math.hypot(+ln[1], +ln[2]) : 99, hips: +((/hips (-?[0-9.]+)/.exec(l) || [0, 0])[1]), t: r.t, clip: c, name });
    }
    const length = parseFloat((c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'length') || [0, 0])[1]);
    const fade = (c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'fade') || [0, 0.4, 0.6]).slice(1).map(Number);
    const e = { elbow: [], shoulder: [] }; let bend = null;
    for (const side of ['Left', 'Right']) {
      const S = side[0], sw = sweep(c, side);
      for (const r of rows) {
        A.upper.push(len(sub(r[S + 'elbow'], r[S + 'shoulder']))); A.fore.push(len(sub(r[S + 'hand'], r[S + 'elbow'])));
        // (a limb the clip does not hold at that row — no target of it, or its weight off — under a trunk the clip leaves be then: the game's own
        //  stance, which is what a body's «shoulder», «restElbow», «rest» and «stance» are. Not the take's first frame: the mannequin is still coming
        //  into its stance there — dv_hand_belt 2026-11-13, its row at 0 s 6 cm off all the others)
        if (r.t > 0.1 && (!sw || (sw[Math.round(r.t / Dt)] || { w: 1 }).w < 0.005) && !c.keys.body && !c.keys.Hips && ['Spine1', 'Spine2', 'move'].every(k => !c.keys[k] || len(eased(c.keys[k].slice().sort((a, b) => a.t - b.t), r.t, [0, 0, 0])) < (k === 'move' ? 0.01 : 2))) for (const [k, v] of [['shoulder', r[S + 'shoulder']], ['elbow', r[S + 'elbow']], ['hand', r[S + 'hand']], ['foot', c.keys['@' + side + 'Foot'] ? null : r[S + 'foot']]]) if (v) (R[side + ' ' + k] = R[side + ' ' + k] || []).push(v);
        if (!sw || r.t < fade[0] + 0.05 || r.t > length - fade[1] - 0.05) continue; // (in the clip's own fade the pose under it still shows)
        const s = sw[Math.round(r.t / Dt)]; if (!s) continue;
        e.shoulder.push(len(sub(s.sh, r[S + 'shoulder'])));
        if (s.held) { e.elbow.push(len(sub(solve(r[S + 'shoulder'], r[S + 'hand'], unit(eased(c.keys['@' + side + 'Hand.p'] && c.keys['@' + side + 'Hand.p'].slice().sort((a, b) => a.t - b.t), r.t, poleOf(side))), B).elbow, r[S + 'elbow']))); }
      }
      for (const s of sw || []) if (s.bend !== null && s.t >= fade[0] && s.t <= length - fade[1] && (bend === null || s.bend > bend)) bend = s.bend;
    }
    for (const side of ['Left', 'Right']) { // (the bones' speed: the model's «spin» against the take's «fastest» of that arm's three bones)
      const sw = sweep(c, side), g = b => { const m = new RegExp('^\\s+' + side + b + '\\s+(\\d+) deg/s', 'm').exec(text); return m ? +m[1] : null; }, real = [g('Arm'), g('ForeArm'), g('Hand')];
      if (sw && real.every(v => v !== null)) spins.push({ name, side, real: Math.max(...real), model: Math.max(...sw.map(x => x.spin)) });
    }
    const said = /bend L ([0-9.]+) R ([0-9.]+) deg/.exec(text), measured = said ? Math.max(+said[1], +said[2]) : null, tight = /tightest elbow ([0-9.]+) deg/.exec(text);
    let fold = 180; for (const side of ['Left', 'Right']) for (const s of sweep(c, side) || []) fold = Math.min(fold, s.fold);
    console.log(name + ' (take ' + take[1] + ', ' + c.body + ', ' + rows.length + ' rows): the shoulder ' + mm(mean(e.shoulder)) + ' off on average, ' + mm(Math.max(0, ...e.shoulder)) + ' at most'
      + (e.elbow.length ? ' · the elbow by the solve ' + mm(mean(e.elbow)) + ', ' + mm(Math.max(...e.elbow)) + ' at most (' + e.elbow.length + ' rows held)' : '')
      + (bend !== null && measured !== null ? ' · the wrist ' + bend.toFixed(1) + '° for a measured ' + measured + '°' : '')
      + (tight ? ' · the tightest elbow ' + fold.toFixed(0) + '° for a measured ' + tight[1] + '°' : ''));
    A.elbow.push(...e.elbow); A.shoulder.push(...e.shoulder);
  }
  const sd = a => { const m = mean(a); return Math.sqrt(mean(a.map(x => (x - m) ** 2))); };
  // the head: its ball as the body stands straight (rows that lean under 3°), and how near the ball carried by the back is to every row's
  const StandLean = 9, StandHips = 0.8; // (a row stands straight: the game's own stance reads a lean of 5–7 deg and hips 0.84 m — take_book, wave_off 2026-11-11)
  for (const who of Object.keys(heads)) {
    const h = heads[who]; if (!h.length) continue;
    const up = h.filter(x => x.lean < StandLean && x.hips > StandHips * BODIES[who].trunk.hips[1] / HUMAN.trunk.hips[1]), from = up.length ? up : h, c0 = [0, 1, 2].map(i => mean(from.map(x => x.c[i]))), r0 = mean(h.map(x => x.r));
    const errs = h.map(x => { const K = x.clip.keys, at = n => K[n] && K[n].length ? eased(K[n].slice().sort((a, b) => a.t - b.t), x.t, [0, 0, 0]) : [0, 0, 0], bodyMode = b => x.clip.head.some(l => l.trim().replace(/\s+/g, ' ') === 'mode ' + b + ' body'); return len(sub(carried(c0, bodyMode('Spine1') ? at('Spine1') : [0, 0, 0], bodyMode('Spine2') ? at('Spine2') : [0, 0, 0], at('move'), false, BODIES[who]), x.c)); });
    console.log('the head, ' + who + ': its ball ' + c0.map(v => v.toFixed(3)).join(' ') + ', r ' + r0.toFixed(3) + ' m' + (h.some(x => x.guessed) ? ' (GUESSED from the shoulders in the game: its skin could not be read)' : ' (read from the skin)') + ' — from ' + from.length + ' row(s)' + (up.length ? ' standing straight' : ', none standing straight: a mean of all') + '; carried by the back it is ' + mm(mean(errs)) + ' off the rows on average, ' + mm(Math.max(...errs)) + ' at most (' + h.length + ' rows). The model has ' + (BODIES[who].head ? BODIES[who].head.c.join(' ') + ', r ' + BODIES[who].head.r + ' (what it is used for, and within what error: the head of this file)' : 'no head for this body'));
  }
  if (spins.length) { // (what [jerk] before a take and the seconds of «stretch» stand on: Twist and HandTwist are the most the first takes measured over the model, so it errs to the careful side — this line says by how much, and it is what to fit them by once there are takes enough)
    const lim = limit('BoneJerk', 600), q = spins.map(x => x.model / x.real).sort((a, b) => a - b), n = k => spins.filter(k).length;
    console.log('the bones\' speed, ' + spins.length + ' arm(s): the model says ×' + q[Math.floor(q.length / 2)].toFixed(2) + ' of what the take measured (median; ×' + q[0].toFixed(2) + ' … ×' + q[q.length - 1].toFixed(2) + '). Over ' + lim + ' deg/s — the game ' + n(x => x.real > lim) + ', the model ' + n(x => x.model > lim) + ': said where the game had none ' + n(x => x.model > lim && x.real <= lim) + ', missed ' + n(x => x.model <= lim && x.real > lim));
  }
  // how a body stands with no clip on it, from the rows where a limb is the game's own: what its «shoulder», «restElbow», «rest», «stance» are fitted to
  for (const who of Object.keys(rests)) for (const k of Object.keys(rests[who]).sort()) {
    const a = rests[who][k], m = [0, 1, 2].map(i => mean(a.map(v => v[i]))), [side, what] = k.split(' '), has = (what === 'foot' ? BODIES[who].stance : BODIES[who][{ shoulder: 'shoulder', elbow: 'restElbow', hand: 'rest' }[what]])[side];
    console.log('at rest, ' + who + ': ' + k.toLowerCase() + ' ' + m.map(v => v.toFixed(3)).join(' ') + ' ± ' + mm(Math.sqrt(mean(a.map(v => len(sub(v, m)) ** 2)))) + ' (' + a.length + ' rows) — the model has ' + has.join(' ') + ', ' + mm(len(sub(has, m))) + ' off');
  }
  if (!Object.keys(all).some(who => all[who].upper.length)) console.log('no takes with rows on disk (' + out + '): the model stands on the numbers in its head');
  else for (const who of Object.keys(all)) { const a = all[who]; if (a.upper.length) console.log('in all, ' + who + ': upper arm ' + mean(a.upper).toFixed(3) + ' ± ' + sd(a.upper).toFixed(3) + ' m (the model: ' + BODIES[who].upper + '), forearm ' + mean(a.fore).toFixed(3) + ' ± ' + sd(a.fore).toFixed(3) + ' m (' + BODIES[who].fore + '), ' + a.upper.length + ' rows · the elbow ' + mm(mean(a.elbow)) + ' · the shoulder ' + mm(mean(a.shoulder))); }
}

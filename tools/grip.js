// A hand on a thing, reckoned (the user 2026-11-11: «чому наосліп, ми що дарма правили всі ці інструменти» — a grip's numbers were turned by eye take
// after take, because the body model, tools/body.js, ends at the wrist). Here are the fingers and the thing's shape:
//   node tools/anim.js grip <clip> [left|right] [--at <s>] [--search] [--write] [--sheet <file>]
//   node tools/anim.js grip presets                the fingers' ready shapes (flat, fist) as keys, measured on each kept hand, against tools/motion.js's numbers
// What it reads: the grip sheet of the clip's last take (studio/out/stage_<clip>.full.txt, BodyCheck.GripSheet): each digit's three joints — place and
// turn — and its tip in the hand's own frame, the keys the clip had on those bones then, the thing's box. A finger key turns its bone over the pose the
// game has under it (BodyClips.Turn), so nothing here is a number of the skeleton guessed: the take measured the hand as it was at known keys, and
// another key is that pose turned on by the difference, joint after joint (fk). The hand so measured is kept in tools/grip_hands.json (a body, a side),
// so the reckoning needs no fresh take; a new take's sheet is first held against what the kept hand foretold for its keys (how near the model is).
// What it says, a hand at a time:
//   as the file is     every digit against the thing's surface by the model, in BodyCheck's own words (lies, pokes, air, IN) and by its own limits
//                      (GripPalm, GripTip, GripSteep, FingerDeep, GripLie — read from src/BodyCheck.cs, never copied)
//   reckoned           the wrist's place («.on») moved straight to or from the surface till the palm's face lies on it; each finger's two bends
//                      (…Index1, …Index2) searched so that its tip lies on the surface — the pad, not the end —, no joint in the thing, no joint bent
//                      past what a finger bends; the thumb's (…Thumb1 x y z, …Thumb2 x) so that its tip is on the thing, by as little a turn as does it
//   --search           … and, when a finger still cannot lie (or asked), the places near — the wrist slid along the surface, the hand turned about its
//                      palm's way — where more of them do: the nearest such place, with what it moves
//   the command        one «node tools/anim.js batch …» with those keys (the held keys of each channel: a key that was 0 — the hand still open on its
//                      way — stays); --write runs it. Then a take: the game measures every digit again, and the two are held together.
// NOT reckoned here: the arm (does it reach that wrist, how the wrist bends: the take's [miss], [bend]); the turn of the hand, which is the author's —
// «.on.f», «.on.n» say which face the palm is on; a thing that is not its box (Distance's box or round: a book is a box, a basket is not).
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const body = require('./body.js');
const add = (a, b) => a.map((x, i) => x + b[i]), sub = (a, b) => a.map((x, i) => x - b[i]), mul = (a, k) => a.map(x => x * k);
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2], len = a => Math.hypot(a[0], a[1], a[2]);
const unit = a => { const l = len(a); return l < 1e-9 ? [0, 0, 0] : mul(a, 1 / l); };
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const clamp = (v, a, b) => Math.max(a, Math.min(b, v)), deg = r => r * 180 / Math.PI, rad = d => d * Math.PI / 180;
// turns as quaternions [x, y, z, w], Unity's own algebra: q · v, and Euler(x, y, z) = round y · round x · round z
const I = [0, 0, 0, 1];
const qMul = (a, b) => [a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0], a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]];
const qInv = q => [-q[0], -q[1], -q[2], q[3]];
const qRot = (q, v) => { const u = [q[0], q[1], q[2]], t = mul(cross(u, v), 2); return add(add(v, mul(t, q[3])), cross(u, t)); };
const qAxis = (ax, d) => { const h = rad(d) / 2, s = Math.sin(h); return [ax[0] * s, ax[1] * s, ax[2] * s, Math.cos(h)]; };
const qEuler = v => qMul(qMul(qAxis([0, 1, 0], v[1]), qAxis([1, 0, 0], v[0])), qAxis([0, 0, 1], v[2]));
const qPart = (q, k) => { if (k >= 0.999) return q; const w = clamp(q[3], -1, 1), a = 2 * Math.acos(Math.abs(w)), s = Math.sqrt(1 - w * w); if (s < 1e-6) return I; const ax = mul([q[0], q[1], q[2]], (w < 0 ? -1 : 1) / s); return qAxis(ax, deg(a) * k); }; // (Slerp from no turn)
const r1 = v => Math.round(v * 10) / 10, mm = v => (v < 0 ? '−' : '') + Math.abs(Math.round(v * 1000)) + ' mm', f3 = v => (Math.round(v * 1000) / 1000).toString();

const DIGITS = ['Thumb', 'Index', 'Middle', 'Ring', 'Pinky'], WORD = { Thumb: 'thumb', Index: 'index', Middle: 'middle', Ring: 'ring', Pinky: 'pinky' };
const whole = (name, def) => { const m = new RegExp('\\b' + name + ' = (\\d+);').exec(fs.readFileSync(path.join(root, 'src', 'BodyCheck.cs'), 'utf8')); return m ? +m[1] : def; }; // (a whole number of BodyCheck's: body.limit reads the ones written «…f»)
const lim = () => ({ palm: body.limit('GripPalm', 0.045), tip: body.limit('GripTip', 0.025), steep: body.limit('GripSteep', 50), deep: body.limit('FingerDeep', 0.02), shut: body.limit('GripShut', 0.045), under: body.limit('GripUnder', 0.7), lie: whole('GripLie', 3), full: body.limit('Full', 0.95) });
// what the reckoning aims at, inside those limits: the palm's face and a fingertip's bone this far off the surface (the skin between), a finger's last
// bone no steeper into it than this; a joint no deeper in the thing than Skin. A finger's joints bend from Back (a little past straight) to Most.
const PalmOn = 0.003, Pad = 0.004, Lies = 25, Skin = 0.002, Back = -12, Most = 100;

// ---------------------------------------------------------------- the sheet of a take
function readSheet(text) {
  const lines = text.split('\n').filter(l => l.startsWith('grip '));
  if (!lines.length) return null;
  const nums = s => s.trim().split(/\s+/).map(Number);
  const out = { hands: {} };
  for (const l of lines) {
    let m = /^grip thing: centre (.+?) size (.+?) (box|round) off ([0-9.]+) deg/.exec(l);
    if (m) { out.thing = { centre: nums(m[1]), size: nums(m[2]), round: m[3] === 'round', off: +m[4] }; continue; }
    m = /^grip part: centre (.+?) size (.+)$/.exec(l);
    if (m && out.thing) { (out.thing.parts = out.thing.parts || []).push({ centre: nums(m[1]), size: nums(m[2]) }); continue; }
    m = /^grip (left|right) at ([0-9.]+) s \(on the thing ([0-9.]+)–([0-9.]+) s(?:, the thing then ([0-9.]+) deg off its said turn(?:, its middle [^)]*? off its said place(?:, the clip \S+ at [0-9.]+ s)?)?)?\): wrist (.+?) asked (.+?) f (.+?) n (.+?) palm (-?[0-9.]+) tips (bones|reckoned)/.exec(l);
    if (m) { out.hands[m[1]] = { side: m[1], t: +m[2], from: +m[3], to: +m[4], off: +(m[5] || 0), wrist: nums(m[6]), asked: nums(m[7]), f: nums(m[8]), n: nums(m[9]), palm: +m[10], tips: m[11], digits: {} }; continue; }
    m = /^grip (left|right) (Thumb|Index|Middle|Ring|Pinky): (.+)$/.exec(l);
    if (m && out.hands[m[1]]) {
      const seg = m[3].split('|').map(s => s.trim()).filter(Boolean), d = { P: [], Q: [], key: [] };
      for (const s of seg) {
        const j = /^p (.+?) q (.+?) key (.+)$/.exec(s), t = /^tip (.+?)( bones| reckoned)?$/.exec(s);
        if (j) { d.P.push(nums(j[1])); d.Q.push(nums(j[2])); const k = /^(.+?) w ([0-9.]+)$/.exec(j[3].trim()); d.key.push(k ? { v: nums(k[1]), w: +k[2] } : null); }
        else if (t) { d.tip = nums(t[1]); if (t[2]) d.tips = t[2].trim(); } // (which this finger's tip is: the skeleton's own end, or reckoned)
      }
      if (d.P.length === 3 && d.tip) out.hands[m[1]].digits[m[2]] = d;
    }
  }
  return out.thing ? out : null; // (a thing with no hand on it yet is a sheet too: its box)
}

// ---------------------------------------------------------------- a digit at other keys: the measured pose turned on, joint after joint
// d: a digit of a sheet (P, Q, key as measured, tip) · K: its three keys now, each [x, y, z] or null (not keyed: no turn of ours on that bone)
function fk(d, K, tips) {
  const was = i => d.key[i] ? qPart(qEuler(d.key[i].v), d.key[i].w) : I, now = i => K[i] ? qEuler(K[i]) : I;
  const Q = [], P = [d.P[0]];
  Q[0] = qMul(qMul(d.Q[0], qInv(was(0))), now(0));
  for (let i = 1; i < 3; i++) {
    P[i] = add(P[i - 1], qRot(Q[i - 1], qRot(qInv(d.Q[i - 1]), sub(d.P[i], d.P[i - 1])))); // (the bone before carries it)
    Q[i] = qMul(qMul(qMul(Q[i - 1], qMul(qInv(d.Q[i - 1]), d.Q[i])), qInv(was(i))), now(i));
  }
  const tip = (d.tips || tips) === 'bones' ? add(P[2], qRot(Q[2], qRot(qInv(d.Q[2]), sub(d.tip, d.P[2])))) : add(P[2], mul(sub(P[2], P[1]), 0.8)); // (BodyCheck.Tip: the skeleton's own end, or 0.8 of the bone before)
  return { P, tip };
}
// how far a joint is bent toward the palm, deg (below zero: bent back): from the way in to the way out, about the hand's own side (x = palm × fingers)
const curl = (a, b) => { const u = unit(a), v = unit(b); return deg(Math.acos(clamp(dot(u, v), -1, 1))) * (cross(u, v)[0] <= 0 ? 1 : -1); };

// ---------------------------------------------------------------- the thing: StoryPropView.Distance, its box or its round, in the thing's frame
function thingOf(t) {
  const e = mul(t.size, 0.5), thin = e[0] <= e[1] && e[0] <= e[2] ? 0 : e[1] <= e[2] ? 1 : 2;
  const ia = thin === 0 ? 1 : 0, ib = thin === 2 ? 1 : 2;
  const box = (q, h) => { const d = [Math.abs(q[0]) - h[0], Math.abs(q[1]) - h[1], Math.abs(q[2]) - h[2]]; return d[0] > 0 || d[1] > 0 || d[2] > 0 ? Math.hypot(Math.max(d[0], 0), Math.max(d[1], 0), Math.max(d[2], 0)) : Math.max(d[0], d[1], d[2]); };
  const dist = p => {
    if (t.parts && t.parts.length > 1) return Math.min(...t.parts.map(x => box(sub(p, x.centre), mul(x.size, 0.5)))); // (a thing of several boxes: the nearest — StoryPropView.Distance)
    const q = sub(p, t.centre);
    if (t.round) { const radial = Math.hypot(q[ia], q[ib]) - Math.max(e[ia], e[ib]), o = Math.abs(q[thin]) - e[thin]; return radial > 0 || o > 0 ? Math.hypot(Math.max(radial, 0), Math.max(o, 0)) : Math.max(radial, o); }
    return box(q, e);
  };
  const E = 0.004; // (StoryPropView.SlopeStep)
  const normal = p => { const g = [0, 1, 2].map(i => { const a = p.slice(), b = p.slice(); a[i] += E; b[i] -= E; return dist(a) - dist(b); }); return len(g) > 1e-9 ? unit(g) : [0, 1, 0]; };
  return { dist, normal };
}

// the hand's frame set on the thing: «.on» the wrist, «.on.f» its fingers' way, «.on.n» its palm's (made square to them: Quaternion.LookRotation)
function frame(W, f, n) {
  const z = unit(f), y = unit(sub(n, mul(z, dot(n, z)))), x = cross(y, z);
  return { W, x, y, z, at: p => add(W, add(add(mul(x, p[0]), mul(y, p[1])), mul(z, p[2]))) };
}

// ---------------------------------------------------------------- a hand on the thing, as BodyCheck.GripOf reads it
// keys: { Index: [k1, k2, k3], … } (each [x, y, z] or null) · up: the world's up in the thing's frame (null: not known)
function read(hand, thing, fr, keys, up) {
  const L = lim(), along = len(hand.digits.Middle.P[0]), face = fr.at([0, 0.015, 0.45 * along]), palmGap = thing.dist(face);
  const turned = palmGap < 0.005 || dot(fr.y, mul(thing.normal(face), -1)) > 0.2;
  const digits = {}; let lie = 0, shut = 0, fingers = 0, thumb = false; const off = [];
  for (const name of DIGITS) {
    const d = hand.digits[name]; if (!d) continue;
    const k = fk(d, keys[name] || [null, null, null], hand.tips), tip = fr.at(k.tip), j2 = fr.at(k.P[1]), j3 = fr.at(k.P[2]);
    const dt = thing.dist(tip), mid = Math.min(thing.dist(j2), thing.dist(j3));
    const steep = deg(Math.asin(clamp(dot(unit(sub(tip, j3)), mul(thing.normal(tip), -1)), -1, 1)));
    const state = dt < -L.deep || mid < -L.deep ? 'IN' : dt > L.tip ? 'air' : name === 'Thumb' ? 'on' : steep > L.steep ? 'pokes' : steep < -L.steep ? 'stands' : 'lies';
    digits[name] = { tip: dt, mid, steep, state, curls: [curl([0, 0, 1], sub(k.P[1], k.P[0])), curl(sub(k.P[1], k.P[0]), sub(k.P[2], k.P[1])), curl(sub(k.P[2], k.P[1]), sub(k.tip, k.P[2]))] };
    if (name === 'Thumb') { thumb = state === 'on'; continue; }
    fingers++;
    if (state === 'lies') lie++;
    else { off.push(WORD[name] + (state === 'IN' ? ' ' + mm(-Math.min(dt, mid)) + ' inside it' : state === 'pokes' ? ' pokes at it end on (' + Math.round(steep) + '°)' : state === 'stands' ? ' stands off it on its tip (' + Math.round(-steep) + '°)' : ' ' + mm(dt) + ' in the air')); if (state === 'air' && len(sub(tip, face)) < L.shut) shut++; }
  }
  const under = up ? dot(fr.y, up) > L.under && palmGap <= L.palm : false;
  const no = [];
  if (palmGap > L.palm) no.push('palm ' + mm(palmGap) + ' off it');
  if (!turned) no.push('palm turned away from it');
  if (lie < Math.min(L.lie, fingers)) no.push('fingers lying on it ' + lie + ' of ' + fingers + ' (' + off.join(', ') + ')');
  if (!thumb && !under && digits.Thumb) no.push('the thumb ' + (digits.Thumb.state === 'IN' ? 'inside it' : mm(digits.Thumb.tip) + ' off it'));
  if (shut >= 3) no.push('shut to a fist before it');
  return { palmGap, turned, digits, lie, fingers, thumb, under, why: no.length ? no.join(', ') : null, wrist: thing.dist(fr.W) };
}
const said = r => 'palm ' + mm(r.palmGap) + DIGITS.filter(n => r.digits[n]).map(n => { const d = r.digits[n]; return ' · ' + WORD[n] + ' ' + mm(d.tip) + ' ' + d.state + (d.state === 'IN' && d.mid < d.tip ? ' (a joint ' + mm(d.mid) + ')' : n !== 'Thumb' && d.state !== 'air' ? ' ' + Math.round(d.steep) + '°' : ''); }).join('');

// ---------------------------------------------------------------- the reckoning
// the wrist moved straight to or from the surface till the palm's face is PalmOn off it (null: the palm does not face the thing from there)
function palmOn(hand, thing, W, f, n) {
  const along = len(hand.digits.Middle.P[0]);
  for (let i = 0; i < 6; i++) {
    const fr = frame(W, f, n), face = fr.at([0, 0.015, 0.45 * along]), g = thing.dist(face), nrm = thing.normal(face);
    if (dot(fr.y, mul(nrm, -1)) < 0.3 && g > 0.005) return null;
    if (Math.abs(g - PalmOn) < 0.0005) break;
    W = sub(W, mul(nrm, g - PalmOn));
  }
  return W;
}
// one finger: the two bends that lay its tip on the surface (k3: its third key as the file has it). cost: 0 a finger that lies as it should
function finger(d, tips, thing, fr, k3, start, like) { // (like: the middle finger's two bends, already found — the fingers of one hand on one face bend alike)
  const cost = (a, b) => {
    const k = fk(d, [[a, 0, 0], [b, 0, 0], k3], tips), c = [curl([0, 0, 1], sub(k.P[1], k.P[0])), curl(sub(k.P[1], k.P[0]), sub(k.P[2], k.P[1])), curl(sub(k.P[2], k.P[1]), sub(k.tip, k.P[2]))];
    let v = 0;
    for (const x of c.slice(0, 2)) v += 40 * (Math.max(0, Back - x) + Math.max(0, x - Most)); // (no joint past what a finger bends)
    const pts = [k.P[1], k.P[2], k.tip].map(fr.at), all = pts.concat([fr.at(mul(add(k.P[0], k.P[1]), 0.5)), fr.at(mul(add(k.P[1], k.P[2]), 0.5)), fr.at(mul(add(k.P[2], k.tip), 0.5))]);
    for (const p of all) v += 8000 * Math.max(0, -thing.dist(p) - Skin); // (nothing of it in the thing)
    const dt = thing.dist(pts[2]);
    v += 1000 * Math.abs(dt - Pad); // (mm the tip is off where a pad lies)
    const steep = deg(Math.asin(clamp(dot(unit(sub(pts[2], pts[1])), mul(thing.normal(pts[2]), -1)), -1, 1)));
    v += 0.6 * Math.max(0, steep - Lies) + 0.6 * Math.max(0, -steep - Lies); // (the pad: not the end into it, not the tip alone with the finger standing off it)
    v += 150 * Math.max(0, thing.dist(pts[1]) - 0.025); // (its last joint near the surface too: it lies, it does not stand on its tip)
    v += 0.01 * (Math.abs(c[0]) + Math.abs(c[1])) + 0.02 * Math.abs(steep); // (of two bends as good: the smaller, the last bone flatter on it)
    if (like) v += 0.05 * (Math.abs(c[0] - like[0]) + Math.abs(c[1] - like[1])); // (20° off its neighbour weighs as a millimetre of the tip)
    return { v, c };
  };
  let best = null;
  const tryAt = (a, b) => { const r = cost(a, b); if (!best || r.v < best.v) best = { a, b, v: r.v, c: r.c }; };
  for (let a = -90; a <= 130; a += 5) for (let b = -90; b <= 130; b += 5) tryAt(a, b);
  { const c = best; for (let a = c.a - 5; a <= c.a + 5; a++) for (let b = c.b - 5; b <= c.b + 5; b++) tryAt(a, b); } // (then to a degree round the best)
  if (start) tryAt(start[0], start[1]);
  return best;
}
// the thumb: its first joint's three turns and its second's bend, so that its tip is on the thing, by as little a turn off «from» as does it
function thumbOf(d, tips, thing, fr, from) {
  const cost = (v1, x2) => {
    const k = fk(d, [v1, [x2, 0, 0], from[2]], tips), tip = fr.at(k.tip), dt = thing.dist(tip);
    let v = 1000 * Math.abs(dt - Pad);
    for (const p of [k.P[1], k.P[2], mul(add(k.P[1], k.P[2]), 0.5), mul(add(k.P[2], k.tip), 0.5)]) v += 8000 * Math.max(0, -thing.dist(fr.at(p)) - Skin);
    v += 0.08 * (Math.abs(v1[0] - from[0][0]) + Math.abs(v1[1] - from[0][1]) + Math.abs(v1[2] - from[0][2]) + Math.abs(x2 - from[1][0])); // (a thumb turns no more than it must: its joints' own limits are not measured)
    return v;
  };
  let best = null;
  const tryAt = (v1, x2) => { const v = cost(v1, x2); if (!best || v < best.v) best = { v1, x2, v }; };
  const o = from[0], o2 = from[1][0], R = 48;
  for (let x = -R; x <= R; x += 12) for (let y = -R; y <= R; y += 12) for (let z = -R; z <= R; z += 12) for (let b = -36; b <= 48; b += 12) tryAt([o[0] + x, o[1] + y, o[2] + z], o2 + b);
  for (const step of [4, 1]) { const c = best; for (let x = -2; x <= 2; x++) for (let y = -2; y <= 2; y++) for (let z = -2; z <= 2; z++) for (let b = -2; b <= 2; b++) tryAt([c.v1[0] + x * step, c.v1[1] + y * step, c.v1[2] + z * step], c.x2 + b * step); }
  return best;
}
// a hand at a place: the palm laid on, every finger searched (the thumb only when asked: it is the slow one)
function lay(hand, thing, W0, f, n, keys, up, withThumb) {
  const W = palmOn(hand, thing, W0, f, n);
  if (!W) return null;
  const fr = frame(W, f, n), out = {}; let cost = 0;
  let like = null;
  for (const name of ['Middle', 'Index', 'Ring', 'Pinky']) {
    const d = hand.digits[name]; if (!d) continue;
    const k = keys[name] || [null, null, null], b = finger(d, hand.tips, thing, fr, k[2], k[0] && k[1] ? [k[0][0], k[1][0]] : null, like);
    out[name] = [[b.a, 0, 0], [b.b, 0, 0], k[2]]; cost += b.v;
    if (name === 'Middle') like = b.c;
  }
  if (hand.digits.Thumb) {
    const k = keys.Thumb || [null, null, null], from = [k[0] || [0, 0, 0], k[1] || [0, 0, 0], k[2]];
    if (withThumb) { const b = thumbOf(hand.digits.Thumb, hand.tips, thing, fr, from); out.Thumb = [b.v1, [b.x2, 0, 0], k[2]]; } else out.Thumb = k;
  }
  const r = read(hand, thing, fr, out, up);
  return { W, f, n, keys: out, r, cost };
}

// … and, when it does not hold there (or asked), the places near: the wrist slid along the palm's own plane, the hand turned about the palm's way — the one
// where most fingers lie, then the nearest. { best, moved } — moved: what the place near changed, in words ('' the place as said); null: no palm on it
function settle(hand, thing, W0, f, n, keys, up, search) {
  let best = lay(hand, thing, W0, f, n, keys, up, true), moved = '';
  if (!best) return null;
  if (search || best.r.why) {
    const fr0 = frame(best.W, f, n); let top = null;
    for (let yaw = -30; yaw <= 30; yaw += 15) for (let u = -0.06; u <= 0.0601; u += 0.02) for (let v = -0.06; v <= 0.0601; v += 0.02) {
      const f2 = qRot(qAxis(fr0.y, yaw), f), W2 = add(best.W, add(mul(fr0.z, u), mul(fr0.x, v))), x = lay(hand, thing, W2, f2, n, keys, up, false);
      if (!x || x.r.wrist < 0.005) continue; // (the wrist stays outside the thing)
      const score = x.r.lie * 1000 - x.cost - 400 * (Math.abs(u) + Math.abs(v)) - 0.5 * Math.abs(yaw);
      if (!top || score > top.score) top = { score, yaw, u, v, f2, W2 };
    }
    if (top && (top.yaw !== 0 || Math.abs(top.u) > 1e-6 || Math.abs(top.v) > 1e-6)) {
      const x = lay(hand, thing, top.W2, top.f2, n, keys, up, true);
      if (x && (x.r.lie > best.r.lie || (!x.r.why && best.r.why))) { best = x; moved = 'slid ' + mm(top.u) + ' along the fingers, ' + mm(top.v) + ' across' + (top.yaw ? ', turned ' + top.yaw + '° about the palm\'s way' : ''); }
    }
  }
  return { best, moved };
}

// ---------------------------------------------------------------- for a movement of the script (tools/motion.js «grip <side> <face>»)
// A face of the thing by its name, in the thing's own frame as it lies square before the body (x right, y up, z ahead): the palm goes on that face, the
// fingers the way said (or down a side face, forward over the top or the bottom), and from the kept hand and the thing's kept box: the wrist's point, the
// fingers' keys. { on, f, n, keys (null: no hand of this body measured yet), notes }
const FACES = { left: [-1, 0, 0], right: [1, 0, 0], top: [0, 1, 0], bottom: [0, -1, 0], front: [0, 0, 1], back: [0, 0, -1] };
const WAYS = { forward: [0, 0, 1], back: [0, 0, -1], up: [0, 1, 0], down: [0, -1, 0], left: [-1, 0, 0], right: [1, 0, 0] };
const propOf = c => { let rows = []; try { rows = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n'); } catch (e) { /* quiet: no catalogue — no thing named */ } const m = /реквізит `(\w+)`/.exec(rows.find(l => l.startsWith('| `' + c.name + '` |')) || ''); return m ? m[1] : null; };
function forScript(c, side, face, way, t) { // (t: the moment the hand is on it — how the thing is turned then says where up is)
  const N = FACES[face];
  if (!N) throw new Error('grip left|right|both <face> [fingers <way>]: the thing\'s face the palm lies on — ' + Object.keys(FACES).join('|') + ' (the thing as it lies square before the body: x right, y up, z ahead)');
  const f = way ? WAYS[way] : Math.abs(N[1]) > 0.5 ? WAYS.forward : WAYS.down;
  if (!f) throw new Error('fingers ' + Object.keys(WAYS).join('|') + ' — not «' + way + '»');
  if (Math.abs(dot(f, N)) > 0.9) throw new Error('fingers ' + way + ' on the ' + face + ' face: the fingers go along the face the palm is on, not into it');
  const K = kept(), prop = propOf(c), T = prop ? K['thing.' + prop] : null, H = K[bodyName(c) + '.' + side], notes = [];
  if (!prop) throw new Error('grip: the clip\'s row in docs/CLIPS.md names no thing (реквізит `…`): which thing is it on?');
  if (!T) throw new Error('grip: the thing ' + prop + ' is not measured yet — one take of this clip with it on the stage (node tools/anim.js clip ' + c.name + '), then node tools/anim.js grip ' + c.name + ' reads its box');
  const n = mul(N, -1), e = mul(T.size, 0.5), faceAt = add(T.centre, [N[0] * e[0], N[1] * e[1], N[2] * e[2]]);
  if (H && !H.hand.digits.Middle) throw new Error('grip: the kept ' + side + ' hand has no middle finger: nothing to reckon by');
  if (T.off > 2) notes.push('grip: the thing ' + prop + ' was measured standing ' + T.off + '° off the turn «.on» is said in: its box may be turned so against the hand — take the clip again (the stage lays a thing square since 2026-11-11)');
  const along = H ? len(H.hand.digits.Middle.P[0]) : 0.09, W0 = sub(add(faceAt, mul(N, 0.02)), mul(unit(f), 0.45 * along));
  const turnThen = t !== undefined && c.keys['@Prop.r'] && c.keys['@Prop.r'].length ? require('./clip.js').valueAt(c, '@Prop.r', t).v : [0, 0, 0], up = qRot(qInv(qEuler(turnThen)), [0, 1, 0]);
  if (!H) { notes.push('the ' + side + ' hand of this body is not measured yet (no take with a hand on a thing): the wrist is put by a hand 0.09 m long and no finger is keyed — after one take, node tools/anim.js grip ' + c.name + ', then this movement said again lays the fingers'); return { on: W0, f, n, keys: null, notes }; }
  const s = settle(H.hand, thingOf(T), W0, f, n, {}, up, false);
  if (!s) throw new Error('grip: the palm does not come onto the ' + face + ' face of ' + prop + ' (' + T.size.map(f3).join(' × ') + ' m)');
  if (s.moved) notes.push('grip ' + side + ' ' + face + ': the hand lies better a little off the face\'s middle — ' + s.moved);
  if (s.best.r.why) notes.push('grip ' + side + ' ' + face + ': by the reckoning it does NOT hold yet — ' + s.best.r.why + ': another face, or another way for the fingers');
  return { on: s.best.W, f: unit(s.best.f), n, keys: s.best.keys, notes };
}

// ---------------------------------------------------------------- the fingers' ready shapes, measured (node tools/anim.js grip presets)
// tools/motion.js keeps «flat», «fist», «point» as key numbers taken from clips, the left hand's partly a guess («to be measured»). From a kept hand:
// the keys that bend each joint as the shape means — flat: both joints straight on the palm's line; fist: Fist° each; point: the index straight,
// the rest a fist — beside what the numbers in motion.js give on that hand. Nothing is written: the numbers go into motion.js by hand, once looked at.
const Fist = [80, 90];
function presets() {
  const K = kept(), hands = Object.keys(K).filter(k => !k.startsWith('thing.'));
  if (!hands.length) { console.log('no hand measured yet: a take of a clip with a hand on a thing first (its full report carries the grip sheet), then node tools/anim.js grip <that clip>'); return; }
  let M = {}, MB = {}; try { const mo = require('./motion.js'); M = mo.FINGERS_SAID || {}; MB = mo.FINGERS_BODY || {}; } catch (e) { /* quiet: said below as unknown */ }
  for (const key of hands) {
    const H = K[key].hand, side = key.split('.')[1], said0 = MB[key.split('.')[0]] || M; // (a body of its own hand: its own shapes, motion.js FINGERS_BODY)
    console.log(key + ' (measured in «' + K[key].take.split(', file')[0] + '»): the keys that bend a finger\'s two joints to a shape, and what motion.js\'s own numbers give on this hand');
    for (const name of DIGITS.slice(1)) {
      const d = H.digits[name]; if (!d) continue;
      const bends = (a, b) => { const k = fk(d, [[a, 0, 0], [b, 0, 0], d.key[2] ? d.key[2].v : null], H.tips); return [curl([0, 0, 1], sub(k.P[1], k.P[0])), curl(sub(k.P[1], k.P[0]), sub(k.P[2], k.P[1]))]; };
      const keysFor = want => { let best = null; for (let a = -100; a <= 140; a += 1) { const c0 = bends(a, 0)[0]; if (!best || Math.abs(c0 - want[0]) < best.e) best = { a, e: Math.abs(c0 - want[0]) }; } let b2 = null; for (let b = -100; b <= 140; b += 1) { const c1 = bends(best.a, b)[1]; if (!b2 || Math.abs(c1 - want[1]) < b2.e) b2 = { b, e: Math.abs(c1 - want[1]) }; } return [best.a, b2.b]; };
      const row = [];
      for (const [shape, want] of [['flat', [0, 0]], ['fist', Fist]]) {
        const k = keysFor(want), said = said0[side === 'left' ? 'Left' : 'Right'] && said0[side === 'left' ? 'Left' : 'Right'][shape], now = said ? bends(said[name + '1'] || 0, said[name + '2'] || 0).map(Math.round) : null;
        row.push(shape + ': ' + name + '1 ' + k[0] + ', ' + name + '2 ' + k[1] + (now ? ' (motion.js ' + (said[name + '1'] || 0) + ', ' + (said[name + '2'] || 0) + ' bends it ' + now.join('°, ') + '° — meant ' + want.join('°, ') + '°)' : ''));
      }
      console.log('  ' + WORD[name].padEnd(7) + row.join(' · ') + ' · as the game holds it with no key: ' + bends(0, 0).map(Math.round).join('°, ') + '°');
    }
  }
  console.log('point: the index as «flat», the rest as «fist». Nothing written: put the numbers into FINGERS / FINGERS_LEFT of tools/motion.js once looked at (a clip made with the old numbers changes at its next «do»).');
}

// ---------------------------------------------------------------- the clip: its keys for a hand at a moment, the channels to write
const Side = s => s === 'left' ? 'Left' : 'Right';
function clipAt(c, side, t) {
  const clip = require('./clip.js'), val = ch => { const v = clip.valueAt(c, ch, t); return v ? v.v : null; }, h = '@' + Side(side) + 'Hand';
  const keys = {};
  for (const name of DIGITS) keys[name] = [1, 2, 3].map(i => val(Side(side) + 'Hand' + name + i));
  return { on: val(h + '.on'), f: val(h + '.on.f'), n: val(h + '.on.n'), keys, propTurn: val('@Prop.r') };
}
// the stretch a hand is on the thing by the file («.on.w» at BodyCheck.Full or more): [from, to], null — never
function onStretch(c, side) {
  const ks = c.keys['@' + Side(side) + 'Hand.on.w']; if (!ks || !ks.length) return null;
  const full = lim().full, length = parseFloat((c.head.map(h => h.trim().split(/\s+/)).find(w => w[0] === 'length') || [0, 0])[1]) || ks[ks.length - 1].t;
  let from = null, to = null;
  for (let t = 0; t <= length + 1e-6; t += 0.05) { const w = body.eased(ks, t, [0])[0]; if (w >= full) { if (from === null) from = t; to = t; } }
  return from === null ? null : [from, to];
}
// the changes as clip.js words: a channel's held keys (the ones with the value it has at t) take the new value; a channel the clip has not got yet
// takes the times of «like» — 0 where that one is 0, the new value where it holds
function opsFor(c, ch, t, value, like, like2) { // (like2: the channel to take the times of when «like» says nothing at t — the hand's «.on.w»)
  const clip = require('./clip.js'), same = (a, b) => a.every((x, i) => Math.abs(x - b[i]) < 1e-4), line = (k, v) => 'set ' + ch + ' ' + f3(k) + ' ' + v.map(f3).join(' ');
  const ks = c.keys[ch];
  if (ks && ks.length) { const now = clip.valueAt(c, ch, t).v; if (same(now, value)) return []; const held = ks.filter(k => same(k.v, now)); return held.length ? held.map(k => line(k.t, value)) : ks.map(k => line(k.t, add(k.v, sub(value, now)))); } // (no key has the value it has at t — it moves there: every key moved by the difference)
  let ref = c.keys[like];
  if (ref && ref.length && len(clip.valueAt(c, like, t).v) < 1e-4 && like2 && c.keys[like2] && c.keys[like2].length) { like = like2; ref = c.keys[like2]; } // (that one is 0 at t: all its zero keys would read as «held» — the finger shut the whole clip)
  if (!ref || !ref.length) return [line(t, value)];
  const held = clip.valueAt(c, like, t).v;
  if (len(held) < 1e-4) return [line(t, value)];
  return ref.map(k => line(k.t, same(k.v, held) ? value : k.v.every(x => Math.abs(x) < 1e-4) ? [0, 0, 0] : mul(value, clamp(len(k.v) / Math.max(1e-6, len(held)), 0, 1))));
}

// the arm that brings the wrist there, by the model of the arm (tools/body.js: a hand on the thing is in it), over the whole stretch the hand is on the
// thing, with those keys put into a copy of the clip: does it reach, how tight the elbow, how far the wrist is bent — against BodyCheck's own limits
function armSaid(c, side, stretch, ops) {
  const c2 = { name: c.name, head: c.head, order: c.order.slice(), keys: {} };
  for (const ch of Object.keys(c.keys)) c2.keys[ch] = c.keys[ch].map(k => ({ t: k.t, v: k.v.slice() }));
  for (const o of ops) { const w = o.split(' '), ch = w[1], t = +w[2], v = w.slice(3).map(Number), ks = c2.keys[ch] || (c2.keys[ch] = []), k = ks.find(x => Math.abs(x.t - t) < 1e-4); if (k) k.v = v; else { ks.push({ t, v }); ks.sort((a, b) => a.t - b.t); } }
  const sw = body.sweep(c2, Side(side), stretch[0], stretch[1]);
  if (!sw || !sw.length) return 'the arm: not the model\'s here (the body does not stand by its keys, or the thing\'s place is not said)';
  const L = { fold: body.limit('ElbowFold', 25), bend: body.limit('WristBend', 80), miss: body.limit('MissFar', 0.05) };
  let fold = sw[0], bend = null, past = sw[0];
  for (const s of sw) { if (s.fold < fold.fold) fold = s; if (s.turned !== null && (!bend || s.turned > bend.turned)) bend = s; if (s.past > past.past) past = s; }
  const bad = [];
  if (past.past > L.miss) bad.push('does NOT reach: the wrist\'s point is ' + mm(past.past) + ' past the arm at ' + f3(past.t) + ' s ([miss]) — the thing nearer the body (@Prop), or the point nearer the shoulder');
  if (fold.fold < L.fold) bad.push('the elbow folds to ' + Math.round(fold.fold) + '° at ' + f3(fold.t) + ' s ([fold], the limit ' + L.fold + '°)');
  if (bend && bend.turned > L.bend) bad.push('the wrist bends ' + Math.round(bend.turned) + '° at ' + f3(bend.t) + ' s ([bend], the limit ' + L.bend + '°): the fingers\' way (.on.f) nearer the forearm\'s, which goes ' + unit(bend.fore).map(f3).join(' ') + ' there (body frame)');
  return 'the arm by the model (not yet held against a take with a thing): ' + (bad.length ? '! ' + bad.join(' · ') : 'reaches; the tightest elbow ' + Math.round(fold.fold) + '° at ' + f3(fold.t) + ' s' + (bend ? ', the wrist bent ' + Math.round(bend.turned) + '° at most (' + f3(bend.t) + ' s)' : ''));
}

// ---------------------------------------------------------------- the kept hands (tools/grip_hands.json) and a take's sheet
const keptFile = path.join(__dirname, 'grip_hands.json');
const kept = () => fs.existsSync(keptFile) ? JSON.parse(fs.readFileSync(keptFile, 'utf8')) : {};
const bodyName = c => body.bodyOf(c).name; // (a kept hand is one body's: a dvergr's is not a person's, and none is kept till a take measures it)
function sheetOf(c, file) {
  const out = [], K = kept(), who = bodyName(c);
  let sheet = null, head = '';
  if (file) { const text = fs.readFileSync(file, 'utf8'); sheet = readSheet(text); if (!sheet) throw new Error('no grip sheet in ' + file); return { sheet, K, who, head: (text.split(/\r?\n/)[0] || '').trim(), notes: ['the sheet of ' + file + ' (not kept)'] }; } // (--sheet: a report laid aside, or a made-up hand for the tool's own test)
  try {
    const studio = fs.readFileSync(path.join(__dirname, 'studio.path'), 'utf8').trim(), p = path.join(studio, 'out', 'stage_' + c.name + '.full.txt');
    if (fs.existsSync(p)) { const text = fs.readFileSync(p, 'utf8'); sheet = readSheet(text); head = (text.split('\n')[0] || '').trim(); }
  } catch (e) { /* quiet: no studio folder on this machine — the kept hands serve */ }
  if (sheet) {
    let changed = false;
    for (const side of Object.keys(sheet.hands)) {
      const key = who + '.' + side, was = K[key], h = sheet.hands[side];
      if (was && was.take !== head) { // (how near the model is: the kept hand, turned to this take's keys, against what this take measured)
        let worst = 0, of = '', other = 0;
        const differs = (a, b) => (a ? a.v : [0, 0, 0]).some((x, i) => Math.abs(x - (b ? b.v : [0, 0, 0])[i]) > 0.5);
        for (const name of DIGITS) { const d = was.hand.digits[name], m = h.digits[name]; if (!d || !m) continue; other += [0, 1, 2].filter(i => differs(d.key[i], m.key[i])).length; const e = len(sub(fk(d, m.key.map(k => k ? k.v : null), was.hand.tips).tip, m.tip)); if (e > worst) { worst = e; of = WORD[name]; } }
        out.push('the model against this take, ' + side + ' hand: the hand kept from «' + was.take.split(',').slice(0, 2).join(',') + '» foretold this take\'s fingertips within ' + mm(worst) + ' (the worst: ' + of + ') — ' + (other ? other + ' joint key(s) differ between the two takes: that far the reckoning is proven' : 'but the two takes have the SAME finger keys: this proves only that the game holds the hand the same twice, nothing of the reckoning'));
      }
      if (!was || was.take !== head) { K[key] = { take: head, hand: h, thing: sheet.thing }; changed = true; }
    }
    const prop = propOf(c); // (the thing by its name: a movement of the script lays a hand on it with no take — forScript)
    if (prop && JSON.stringify(K['thing.' + prop]) !== JSON.stringify(sheet.thing)) { K['thing.' + prop] = sheet.thing; changed = true; }
    if (changed) fs.writeFileSync(keptFile, JSON.stringify(K, null, 1) + '\n');
  }
  return { sheet, K, who, head, notes: out };
}

function run(a) {
  const clip = require('./clip.js');
  const flags = a.filter(x => x.startsWith('--')), words = a.filter((x, i) => !x.startsWith('--') && !(i > 0 && (a[i - 1] === '--at' || a[i - 1] === '--sheet')));
  const name = words[0], only = words[1];
  if (name === 'presets') { presets(); return; }
  if (!name || (only && !['left', 'right', 'both'].includes(only))) throw new Error('grip <clip> [left|right] [--at <s>] [--search] [--write]');
  const c = clip.load(name.split('.')[0]), got = sheetOf(c, a.includes('--sheet') ? a[a.indexOf('--sheet') + 1] : null), at = a.includes('--at') ? parseFloat(a[a.indexOf('--at') + 1]) : null;
  for (const n of got.notes) console.log(n);
  const ops = [];
  for (const side of ['right', 'left']) {
    if (only && only !== 'both' && only !== side) continue;
    const stretch = onStretch(c, side);
    if (!stretch) continue;
    const from = got.sheet && got.sheet.hands[side] ? { hand: got.sheet.hands[side], thing: got.sheet.thing, take: got.head } : got.K[got.who + '.' + side];
    if (!from) { console.log(side + ' hand: no take has measured this body\'s ' + side + ' hand on a thing yet — node tools/anim.js clip ' + c.name + ' (its full report carries the grip sheet), then this again'); continue; }
    if (!from.hand.digits.Middle) { console.log(side + ' hand: its sheet has no middle finger (a body without finger bones?): nothing to reckon by'); continue; }
    const fresh = got.sheet && got.sheet.hands[side];
    const t = at !== null ? at : (stretch[0] + stretch[1]) / 2, now = clipAt(c, side, t), hand = from.hand;
    // (the thing's box: this take's own; with no report on disk, the one kept with the hand — of whatever thing that take had: the line below says its size)
    const T = fresh ? got.sheet.thing : from.thing, thing = thingOf(T);
    console.log('\n' + side + ' hand on the thing ' + f3(stretch[0]) + '–' + f3(stretch[1]) + ' s, reckoned at ' + f3(t) + ' s. The hand: measured in «' + from.take.split(', file')[0] + '»' + (fresh ? '' : ' (kept: no report of this clip on disk — the THING\'s box is that take\'s too: ' + T.size.map(f3).join(' × ') + ' m)') + ', fingertips ' + (hand.tips === 'bones' ? 'the skeleton\'s own' : 'reckoned 0.8 of the bone before') + '; the thing ' + T.size.map(f3).join(' × ') + ' m' + (T.round ? ', read as a round' : T.parts && T.parts.length > 1 ? ', read as ' + T.parts.length + ' boxes' : ''));
    if (Math.max(T.off, hand.off || 0) > 2) console.log('  ! the thing stood off the turn «.on» is said in — ' + (hand.off || 0) + '° as the hand was measured, ' + T.off + '° at the take\'s end: the numbers below are of a thing square to it, so they are NOT this take\'s (on the stage a thing lies square since 2026-11-11: take it again)');
    if (!now.on) { console.log('  the clip has no @' + Side(side) + 'Hand.on at ' + f3(t) + ' s'); continue; }
    const f = now.f || hand.f, n = now.n || hand.n;
    if (!now.f || !now.n) console.log('  the clip says no «.on.f»/«.on.n» for it: the hand\'s turn is the game\'s pose (as measured: f ' + hand.f.map(f3).join(' ') + ', n ' + hand.n.map(f3).join(' ') + ') — say it, or the grip is not the clip\'s');
    const up = now.propTurn ? qRot(qInv(qEuler(now.propTurn)), [0, 1, 0]) : null;
    if (fresh && len(sub(hand.wrist, hand.asked)) > 0.02) console.log('  ! in the take the wrist was ' + mm(len(sub(hand.wrist, hand.asked))) + ' off the point «.on» asks (the arm did not bring it there: the take\'s [miss]) — below it is reckoned where it is asked');
    const r0 = read(hand, thing, frame(now.on, f, n), now.keys, up);
    console.log('  as the file is:  ' + said(r0) + '\n                   ' + (r0.why ? 'does NOT hold: ' + r0.why : 'holds') + (r0.wrist < 0 ? ' · the wrist ' + mm(-r0.wrist) + ' INSIDE the thing' : ''));
    const got2 = settle(hand, thing, now.on, f, n, now.keys, up, flags.includes('--search'));
    if (!got2) { console.log('  the palm does not face the thing from «.on» ' + now.on.map(f3).join(' ') + ' with «.on.n» ' + n.map(f3).join(' ') + ': nothing to lay — turn the palm to a face of it (.on.n), or move the point'); continue; }
    const best = got2.best, moved = got2.moved ? ' — the place near where more of the hand lies: ' + got2.moved : '';
    console.log('  reckoned' + moved + ':\n                   ' + said(best.r) + '\n                   ' + (best.r.why ? 'still does NOT hold: ' + best.r.why + ' — this turn of the hand cannot lay it on this thing: another face for the palm (.on.n), or another way for the fingers (.on.f)' : 'holds') + (best.r.wrist < 0 ? ' · ! the wrist ' + mm(-best.r.wrist) + ' INSIDE the thing: the point is on it, not past its edge' : ''));
    const h = '@' + Side(side) + 'Hand', rnd = v => v.map(x => Math.round(x * 1000) / 1000);
    console.log('    ' + h + '.on ' + rnd(best.W).join(' ') + '  (was ' + now.on.map(f3).join(' ') + ': ' + mm(len(sub(best.W, now.on))) + ')' + (moved && now.f ? '\n    ' + h + '.on.f ' + rnd(unit(best.f)).join(' ') + '  (was ' + now.f.map(f3).join(' ') + ')' : ''));
    for (const nm of DIGITS) { const k = best.keys[nm]; if (!k || !hand.digits[nm]) continue; const was = now.keys[nm], d = best.r.digits[nm]; console.log('    ' + WORD[nm].padEnd(7) + (nm === 'Thumb' ? 'Thumb1 ' + (k[0] ? k[0].map(r1).join(' ') : '-') + ' · Thumb2 ' + (k[1] ? r1(k[1][0]) : '-') : '1: ' + r1(k[0][0]) + ' · 2: ' + r1(k[1][0])) + '  (was ' + (nm === 'Thumb' ? (was[0] ? was[0].map(r1).join(' ') : '-') + ' · ' + (was[1] ? r1(was[1][0]) : '-') : (was[0] ? r1(was[0][0]) : '-') + ' · ' + (was[1] ? r1(was[1][0]) : '-')) + ') — joints bent ' + d.curls.slice(0, 2).map(Math.round).join('°, ') + '°'); }
    // the keys
    const way = (ch, v, had) => had ? opsFor(c, ch, t, v, h + '.on.w') : ['set ' + ch + ' 0 ' + v.map(f3).join(' ')]; // (a way the clip has not said yet: one key — it does not grow with a weight)
    const mine = [...opsFor(c, h + '.on', t, rnd(best.W), h + '.on.w'), ...(moved || !now.f || !now.n ? [...way(h + '.on.f', rnd(unit(best.f)), !!now.f), ...(now.n ? [] : way(h + '.on.n', rnd(unit(n)), false))] : [])];
    ops.push(...mine);
    console.log('    ' + armSaid(c, side, stretch, mine));
    const like = DIGITS.slice(1).map(nm => Side(side) + 'Hand' + nm + '1').find(ch => c.keys[ch] && c.keys[ch].length) || h + '.on.w';
    for (const nm of DIGITS) {
      const k = best.keys[nm]; if (!k || !hand.digits[nm]) continue;
      for (const i of [0, 1]) { if (!k[i]) continue; const ch = Side(side) + 'Hand' + nm + (i + 1), had = c.keys[ch] && c.keys[ch].length; if (!had && k[i].every(x => Math.abs(x) < 0.5)) continue; ops.push(...opsFor(c, ch, t, k[i].map(r1), like, h + '.on.w')); }
    }
  }
  if (!ops.length) { console.log('\nnothing to change'); return; }
  const fresh = list => new Set(list.map(o => o.split(' ')[1]).filter(ch => !c.keys[ch] || !c.keys[ch].length));
  if (c.order.length + fresh(ops).size > 40) { // (the game keeps 40 channels a clip: the thumbs' new ones go first — a thumb off the thing is the lesser loss)
    const thumbs = [...fresh(ops)].filter(ch => /HandThumb[12]$/.test(ch));
    for (let i = ops.length - 1; i >= 0; i--) if (thumbs.includes(ops[i].split(' ')[1])) ops.splice(i, 1);
    console.log('\n! with every key the clip would have ' + (c.order.length + fresh(ops).size + thumbs.length) + ' channels and the game keeps 40: the thumbs\' new channels (' + thumbs.join(', ') + ') are left out — the thumb stays as the game holds it; free ' + thumbs.length + ' channel(s) of the clip for them');
    if (c.order.length + fresh(ops).size > 40) throw new Error('still ' + (c.order.length + fresh(ops).size) + ' channels without the thumbs: nothing written — one hand at a time (grip ' + c.name + ' left), or fewer channels in the clip');
  }
  const line = 'batch ' + c.name + ' "' + ops.join('; ') + '"';
  if (flags.includes('--write')) { clip.run('batch', [c.name, ops.join('; ')]); console.log('written. Now the take measures it: node tools/anim.js clip ' + c.name + ' chest,holdr,holdl'); }
  else console.log('\nthe keys (nothing written; --write runs it):\n  node tools/anim.js ' + line + '\nthen the take measures every digit again: node tools/anim.js clip ' + c.name + ' chest,holdr,holdl');
}

module.exports = { run, readSheet, fk, thingOf, frame, read, lay, settle, forScript, presets };
if (require.main === module) { console.error('grip.js is a part of the one tool: node tools/anim.js grip <clip>'); process.exit(1); }

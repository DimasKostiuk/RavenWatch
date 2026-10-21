// Looks into a binary FBX the way our loader (WsAssets.FbxLook) will build it: Unity axes (X mirrored), metres.
// Prints its size, polygons, pictures; draws three views (front X-Y, side Z-Y, top X-Z) into one PNG with a metre
// grid, so seats can be placed on the model without opening the game.
//   node tools/fbx_probe.js <model.fbx> [out.png] [--scale k]
// (k: the part's scale in the model file, so the picture shows it as the game will)
//   --tex <picture.png>   the views in the model's own colours (its diffuse PNG): where candles, lanterns, runes are
//   --px <n>              each view n pixels wide (default 360)
//   --uvmask "<js x y z>" <out.png> [size] · --only "<js x y z>" (see below)
const fs = require("fs"), zlib = require("zlib");
const args = process.argv.slice(2);
const inPath = args[0];
if (!inPath) { console.log("node tools/fbx_probe.js <model.fbx> [out.png] [--scale k]"); process.exit(1); }
const si = args.indexOf("--scale"), K = si >= 0 ? parseFloat(args[si + 1]) : 1;
const outPng = args[1] && !args[1].startsWith("--") && args[1].endsWith(".png") ? args[1] : null;

const b = fs.readFileSync(inPath);
if (b.toString("ascii", 0, 18) !== "Kaydara FBX Binary") throw new Error("not a binary FBX");
const ver = b.readUInt32LE(23), big = ver >= 7500, head = big ? 25 : 13;

function props(at, n) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const t = String.fromCharCode(b[at]); at++;
    switch (t) {
      case "Y": out.push(b.readInt16LE(at)); at += 2; break;
      case "C": out.push(!!b[at]); at += 1; break;
      case "I": out.push(b.readInt32LE(at)); at += 4; break;
      case "F": out.push(b.readFloatLE(at)); at += 4; break;
      case "D": out.push(b.readDoubleLE(at)); at += 8; break;
      case "L": out.push(b.readBigInt64LE(at)); at += 8; break;
      case "S": case "R": { const l = b.readUInt32LE(at); out.push(t === "S" ? b.toString("utf8", at + 4, at + 4 + l) : { raw: l }); at += 4 + l; break; }
      case "d": case "f": case "i": case "l": case "b": {
        const len = b.readUInt32LE(at), enc = b.readUInt32LE(at + 4), cl = b.readUInt32LE(at + 8);
        let data = b.subarray(at + 12, at + 12 + cl);
        if (enc === 1) data = zlib.inflateSync(data);
        const arr = [];
        for (let k = 0; k < len; k++)
          arr.push(t === "d" ? data.readDoubleLE(k * 8) : t === "f" ? data.readFloatLE(k * 4) : t === "i" ? data.readInt32LE(k * 4) : t === "l" ? Number(data.readBigInt64LE(k * 8)) : data[k]);
        out.push(arr); at += 12 + cl; break;
      }
      default: throw new Error("prop type " + t);
    }
  }
  return out;
}

function read(pos) {
  const end = big ? Number(b.readBigUInt64LE(pos)) : b.readUInt32LE(pos);
  const count = big ? Number(b.readBigUInt64LE(pos + 8)) : b.readUInt32LE(pos + 4);
  const listLen = big ? Number(b.readBigUInt64LE(pos + 16)) : b.readUInt32LE(pos + 8);
  const nameLen = b[pos + head - 1];
  if (end === 0) return null;
  const name = b.toString("ascii", pos + head, pos + head + nameLen);
  const pAt = pos + head + nameLen;
  const n = { name, p: name === "Content" ? [] : props(pAt, count), kids: [], end, size: listLen };
  let p = pAt + listLen;
  while (p < end) { const k = read(p); if (!k) { p += head; break; } n.kids.push(k); p = k.end; }
  return n;
}
const top = []; { let p = 27; while (p < b.length - head) { const n = read(p); if (!n) break; top.push(n); p = n.end; } }
const kid = (n, name) => n && n.kids.find(k => k.name === name);
function props70(n) { const m = {}; const P = kid(n, "Properties70"); if (P) for (const k of P.kids) m[k.p[0]] = k.p; return m; }

const global = props70(top.find(n => n.name === "GlobalSettings"));
const gi = (k, d) => global[k] ? Number(global[k][4]) : d;
const up = gi("UpAxis", 1), upS = gi("UpAxisSign", 1), front = gi("FrontAxis", 2), frontS = gi("FrontAxisSign", 1), coord = gi("CoordAxis", 0), coordS = gi("CoordAxisSign", 1);
const unit = gi("UnitScaleFactor", 1), toM = unit / 100;

// 4x4 row-major matrices
const I = () => [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
const mul = (a, c) => { const r = new Array(16).fill(0); for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) for (let k = 0; k < 4; k++) r[i*4+j] += a[i*4+k] * c[k*4+j]; return r; };
const T = v => { const m = I(); m[3] = v[0]; m[7] = v[1]; m[11] = v[2]; return m; };
const S = v => { const m = I(); m[0] = v[0]; m[5] = v[1]; m[10] = v[2]; return m; };
function E(d) {
  const [x, y, z] = d.map(v => v * Math.PI / 180);
  const rx = [1,0,0,0, 0,Math.cos(x),-Math.sin(x),0, 0,Math.sin(x),Math.cos(x),0, 0,0,0,1];
  const ry = [Math.cos(y),0,Math.sin(y),0, 0,1,0,0, -Math.sin(y),0,Math.cos(y),0, 0,0,0,1];
  const rz = [Math.cos(z),-Math.sin(z),0,0, Math.sin(z),Math.cos(z),0,0, 0,0,1,0, 0,0,0,1];
  return mul(rz, mul(ry, rx));
}
const tr = m => { const r = I(); for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) r[i*4+j] = m[j*4+i]; return r; };
const vec = (p, k, d) => p[k] ? [Number(p[k][4]), Number(p[k][5]), Number(p[k][6])] : d;
const neg = v => v.map(x => -x);
function local(n) {
  const p = props70(n), rp = vec(p, "RotationPivot", [0,0,0]), sp = vec(p, "ScalingPivot", [0,0,0]);
  let m = mul(T(vec(p, "Lcl Translation", [0,0,0])), T(vec(p, "RotationOffset", [0,0,0])));
  m = mul(m, T(rp)); m = mul(m, E(vec(p, "PreRotation", [0,0,0]))); m = mul(m, E(vec(p, "Lcl Rotation", [0,0,0])));
  m = mul(m, tr(E(vec(p, "PostRotation", [0,0,0])))); // (a rotation's inverse is its transpose)
  m = mul(m, T(neg(rp))); m = mul(m, T(vec(p, "ScalingOffset", [0,0,0]))); m = mul(m, T(sp)); m = mul(m, S(vec(p, "Lcl Scaling", [1,1,1]))); m = mul(m, T(neg(sp)));
  return m;
}
function geoOff(n) { const p = props70(n); return mul(mul(T(vec(p, "GeometricTranslation", [0,0,0])), E(vec(p, "GeometricRotation", [0,0,0]))), S(vec(p, "GeometricScaling", [1,1,1]))); }
const pt = (m, v) => [m[0]*v[0]+m[1]*v[1]+m[2]*v[2]+m[3], m[4]*v[0]+m[5]*v[1]+m[6]*v[2]+m[7], m[8]*v[0]+m[9]*v[1]+m[10]*v[2]+m[11]];
const axes = v => [-(v[coord] * coordS), v[up] * upS, v[front] * frontS];

const objects = top.find(n => n.name === "Objects"), conns = top.find(n => n.name === "Connections");
const byId = new Map(); for (const k of objects.kids) byId.set(String(k.p[0]), k);
const parent = new Map(), geomOf = new Map();
const videos = [];
for (const c of conns.kids) {
  if (c.name !== "C") continue;
  const ch = byId.get(String(c.p[1])), pa = byId.get(String(c.p[2]));
  if (!ch) continue;
  if (ch.name === "Model" && pa && pa.name === "Model") parent.set(String(c.p[1]), String(c.p[2]));
  else if (ch.name === "Geometry" && pa && pa.name === "Model") geomOf.set(String(c.p[2]), ch);
}
for (const k of objects.kids) if (k.name === "Video") { const c = kid(k, "Content"); videos.push({ name: k.p[1], file: (kid(k, "RelativeFilename") || { p: [""] }).p[0], bytes: c ? c.size : 0 }); }
const worldOf = id => { let m = local(byId.get(id)); let p = parent.get(id); while (p && byId.has(p)) { m = mul(local(byId.get(p)), m); p = parent.get(p); } return m; };

const V = [], TR = [], TUV = []; let polys = 0, tris = 0;
for (const [id, geo] of geomOf) {
  const m = mul(worldOf(id), geoOff(byId.get(id)));
  const cp = kid(geo, "Vertices").p[0], pvi = kid(geo, "PolygonVertexIndex").p[0];
  const base = V.length;
  // the UVs by polygon corner (ByPolygonVertex + IndexToDirect, Meshy's way; else none)
  const uvl = kid(geo, "LayerElementUV"), uvd = uvl ? kid(uvl, "UV") : null, uvi = uvl ? kid(uvl, "UVIndex") : null;
  const uvAt = pv => { if (!uvd) return [0, 0]; const k = uvi ? uvi.p[0][pv] : pv; return [uvd.p[0][k * 2], uvd.p[0][k * 2 + 1]]; };
  for (let i = 0; i < cp.length; i += 3) { const w = axes(pt(m, [cp[i], cp[i+1], cp[i+2]])); V.push(w.map(x => x * toM * K)); }
  let start = 0;
  for (let i = 0; i < pvi.length; i++) if (pvi[i] < 0) {
    polys++; tris += i - start - 1;
    const ix = k => base + (pvi[k] < 0 ? ~pvi[k] : pvi[k]);
    for (let k = start + 1; k + 1 <= i; k++) { TR.push([ix(start), ix(k), ix(k + 1)]); TUV.push([uvAt(start), uvAt(k), uvAt(k + 1)]); }
    start = i + 1;
  }
}
const lo = [1e9,1e9,1e9], hi = [-1e9,-1e9,-1e9];
for (const v of V) for (let a = 0; a < 3; a++) { lo[a] = Math.min(lo[a], v[a]); hi[a] = Math.max(hi[a], v[a]); }
const f = x => x.toFixed(3);
console.log(inPath.split(/[\\/]/).pop());
console.log("  FBX " + ver + ", unit " + unit + " cm, up " + up + (upS < 0 ? "-" : "") + ", models " + geomOf.size + ", vertices " + V.length + ", polygons " + polys + ", triangles " + tris);
console.log("  size (m, x" + K + "): x " + f(lo[0]) + ".." + f(hi[0]) + "  y " + f(lo[1]) + ".." + f(hi[1]) + "  z " + f(lo[2]) + ".." + f(hi[2]) + "  → " + f(hi[0]-lo[0]) + " × " + f(hi[1]-lo[1]) + " × " + f(hi[2]-lo[2]));
for (const v of videos) console.log("  picture " + v.name + " " + v.file + " " + Math.round(v.bytes / 1024) + " KB");
// the highest points in slices of height: where something sticks out (bars, rims)
const slices = 20, hist = new Array(slices).fill(0);
for (const v of V) hist[Math.min(slices - 1, Math.floor((v[1] - lo[1]) / Math.max(1e-6, hi[1] - lo[1]) * slices))]++;
console.log("  vertices by height (bottom→top): " + hist.join(" "));

// --where "<js with x y z>": the box of the points there (e.g. "Math.abs(x) > 0.4": the arms of a cross)
for (let i = 0; i < args.length; i++) if (args[i] === "--where") {
  const test = new Function("x", "y", "z", "return (" + args[i + 1] + ");");
  const l = [1e9,1e9,1e9], h = [-1e9,-1e9,-1e9]; let n = 0;
  for (const v of V) if (test(v[0], v[1], v[2])) { n++; for (let a = 0; a < 3; a++) { l[a] = Math.min(l[a], v[a]); h[a] = Math.max(h[a], v[a]); } }
  console.log("  where " + args[i + 1] + ": " + n + " points" + (n ? "  x " + f(l[0]) + ".." + f(h[0]) + "  y " + f(l[1]) + ".." + f(h[1]) + "  z " + f(l[2]) + ".." + f(h[2]) : ""));
}
// --top x z [below]: the surfaces over the point (x, z) from above, highest first (only those under "below"):
// where a raven's feet go on a bar or a branch
for (let i = 0; i < args.length; i++) if (args[i] === "--top") {
  const x = parseFloat(args[i + 1]), z = parseFloat(args[i + 2]), cap = args[i + 3] && !args[i + 3].startsWith("--") ? parseFloat(args[i + 3]) : 1e9;
  const ys = [];
  for (const t of TR) {
    const [A, B, C] = t.map(k => V[k]);
    const den = (B[2] - C[2]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[2] - C[2]);
    if (Math.abs(den) < 1e-12) continue;
    const w0 = ((B[2] - C[2]) * (x - C[0]) + (C[0] - B[0]) * (z - C[2])) / den, w1 = ((C[2] - A[2]) * (x - C[0]) + (A[0] - C[0]) * (z - C[2])) / den, w2 = 1 - w0 - w1;
    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
    const y = w0 * A[1] + w1 * B[1] + w2 * C[1];
    if (y < cap) ys.push(y);
  }
  ys.sort((p, q) => q - p);
  console.log("  top at x " + x + " z " + z + (cap < 1e8 ? " below " + cap : "") + ": " + (ys.length ? ys.slice(0, 6).map(f).join(", ") : "nothing"));
}
// --uvmask "<js with x y z>" <out.png> [size]: a picture in the model's UV space, white where the triangles whose
// middle passes the test lie (e.g. "y > 1.3": what is on top) — for tools/glow_map.js --cut
// --only "<js with x y z>": with --tex, the triangles failing the test drawn grey (what a mask would take)
const centre = t => [0, 1, 2].map(a => (V[t[0]][a] + V[t[1]][a] + V[t[2]][a]) / 3);
const oi = args.indexOf("--only"), ONLY = oi >= 0 ? new Function("x", "y", "z", "return (" + args[oi + 1] + ");") : null;
for (let i = 0; i < args.length; i++) if (args[i] === "--uvmask") {
  const test = new Function("x", "y", "z", "return (" + args[i + 1] + ");"), out = args[i + 2];
  const sz = args[i + 3] && !args[i + 3].startsWith("--") ? parseInt(args[i + 3]) : 1024, m = Buffer.alloc(sz * sz * 3);
  let n = 0;
  for (let ti = 0; ti < TR.length; ti++) {
    if (!test(...centre(TR[ti]))) continue;
    n++;
    const q = TUV[ti].map(uv => [(uv[0] - Math.floor(uv[0])) * sz, (1 - (uv[1] - Math.floor(uv[1]))) * sz]);
    const den = (q[1][1] - q[2][1]) * (q[0][0] - q[2][0]) + (q[2][0] - q[1][0]) * (q[0][1] - q[2][1]);
    if (Math.abs(den) < 1e-9) continue;
    const y0 = Math.max(0, Math.floor(Math.min(q[0][1], q[1][1], q[2][1])) - 1), y1 = Math.min(sz - 1, Math.ceil(Math.max(q[0][1], q[1][1], q[2][1])) + 1);
    const x0 = Math.max(0, Math.floor(Math.min(q[0][0], q[1][0], q[2][0])) - 1), x1 = Math.min(sz - 1, Math.ceil(Math.max(q[0][0], q[1][0], q[2][0])) + 1);
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      // inside, or within a pixel and a half of an edge (the seam)
      let inside = true;
      for (let e = 0; e < 3; e++) {
        const A = q[e], B = q[(e + 1) % 3], C = q[(e + 2) % 3];
        const ex = B[0] - A[0], ey = B[1] - A[1], len = Math.hypot(ex, ey) || 1e-9;
        const side = (ex * (C[1] - A[1]) - ey * (C[0] - A[0])) > 0 ? 1 : -1;
        const d = side * (ex * (y + 0.5 - A[1]) - ey * (x + 0.5 - A[0])) / len;
        if (d < -1.5) { inside = false; break; }
      }
      if (inside) m.fill(255, (y * sz + x) * 3, (y * sz + x) * 3 + 3);
    }
  }
  writePng(out, sz, sz, m);
  console.log("  uv mask " + args[i + 1] + ": " + n + " triangles → " + out);
}
if (!outPng) process.exit(0);
// ---------------------------------------------------------------- a PNG read (8-bit RGB / RGBA), for --tex
function readPng(file) {
  const b = fs.readFileSync(file);
  let p = 8, w = 0, h = 0, ct = 0; const idat = [];
  while (p < b.length) {
    const len = b.readUInt32BE(p), type = b.toString('ascii', p + 4, p + 8), d = b.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = d.readUInt32BE(0); h = d.readUInt32BE(4); ct = d[9]; if (d[8] !== 8) throw new Error('PNG: 8-bit only'); }
    else if (type === 'IDAT') idat.push(d);
    else if (type === 'IEND') break;
    p += 12 + len;
  }
  const bpp = ct === 6 ? 4 : ct === 2 ? 3 : ct === 4 ? 2 : 1, stride = w * bpp;
  const raw = zlib.inflateSync(Buffer.concat(idat)), out = Buffer.alloc(w * h * 3);
  let prev = Buffer.alloc(stride), cur = Buffer.alloc(stride);
  for (let y = 0; y < h; y++) {
    const ft = raw[y * (stride + 1)], row = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? cur[x - bpp] : 0, up = prev[x], c = x >= bpp ? prev[x - bpp] : 0; let v = row[x];
      if (ft === 1) v += a; else if (ft === 2) v += up; else if (ft === 3) v += (a + up) >> 1;
      else if (ft === 4) { const pp = a + up - c, pa = Math.abs(pp - a), pb = Math.abs(pp - up), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? up : c; }
      cur[x] = v & 255;
    }
    for (let x = 0; x < w; x++) { const o = (y * w + x) * 3; if (bpp >= 3) { out[o] = cur[x * bpp]; out[o + 1] = cur[x * bpp + 1]; out[o + 2] = cur[x * bpp + 2]; } else { out[o] = out[o + 1] = out[o + 2] = cur[x * bpp]; } }
    const t = prev; prev = cur; cur = t;
  }
  return { w, h, px: out };
}
const texi = args.indexOf("--tex"), TEX = texi >= 0 ? readPng(args[texi + 1]) : null;
const texAt = (u, v) => { const x = Math.min(TEX.w - 1, Math.max(0, Math.floor((u - Math.floor(u)) * TEX.w))), y = Math.min(TEX.h - 1, Math.max(0, Math.floor((1 - (v - Math.floor(v))) * TEX.h))); const o = (y * TEX.w + x) * 3; return [TEX.px[o], TEX.px[o + 1], TEX.px[o + 2]]; };
// ---------------------------------------------------------------- three views
const pxi = args.indexOf("--px"), P = pxi >= 0 ? parseInt(args[pxi + 1]) : 360, pad = 24, W = P * 3 + pad * 4, H = P + pad * 2;
const img = Buffer.alloc(W * H * 3, 255);
const span = Math.max(hi[0]-lo[0], hi[1]-lo[1], hi[2]-lo[2]) * 1.05;
const views = [ { a: 0, bAx: 1, d: 2, flipA: false }, { a: 2, bAx: 1, d: 0, flipA: true }, { a: 0, bAx: 2, d: 1, flipA: false } ];
const set = (x, y, r, g, bl) => { if (x < 0 || y < 0 || x >= W || y >= H) return; const i = (y * W + x) * 3; img[i] = r; img[i+1] = g; img[i+2] = bl; };
views.forEach((vw, n) => {
  const ox = pad + n * (P + pad), oy = pad;
  const cA = (lo[vw.a] + hi[vw.a]) / 2, cB = (lo[vw.bAx] + hi[vw.bAx]) / 2;
  const toX = v => Math.round(ox + P / 2 + ((vw.flipA ? -1 : 1) * (v - cA)) / span * P);
  const toY = v => Math.round(oy + P / 2 - (v - cB) / span * P);
  // grid: every 0.25 m light, every metre dark, the axes (0) red
  for (let g = Math.floor((cA - span) * 4) / 4; g <= cA + span; g += 0.25) { const x = toX(g); const m = Math.abs(g - Math.round(g)) < 1e-6; for (let y = oy; y < oy + P; y++) set(x, y, ...(Math.abs(g) < 1e-6 ? [230,80,80] : m ? [170,170,170] : [225,225,225])); }
  for (let g = Math.floor((cB - span) * 4) / 4; g <= cB + span; g += 0.25) { const y = toY(g); const m = Math.abs(g - Math.round(g)) < 1e-6; for (let x = ox; x < ox + P; x++) set(x, y, ...(Math.abs(g) < 1e-6 ? [230,80,80] : m ? [170,170,170] : [225,225,225])); }
  const depth = new Float32Array(P * P).fill(-1e9), col = TEX ? new Uint8Array(P * P * 3) : null;
  const dSign = n === 2 ? 1 : n === 1 ? -1 : -1; // nearest to the viewer: front looks from -Z… drawn as the darkest
  for (let ti = 0; ti < TR.length; ti++) {
    const t = TR[ti], uv = TUV[ti];
    const q = t.map(i => [toX(V[i][vw.a]) - ox, toY(V[i][vw.bAx]) - oy, V[i][vw.d] * dSign]);
    const minX = Math.max(0, Math.floor(Math.min(q[0][0], q[1][0], q[2][0]))), maxX = Math.min(P - 1, Math.ceil(Math.max(q[0][0], q[1][0], q[2][0])));
    const minY = Math.max(0, Math.floor(Math.min(q[0][1], q[1][1], q[2][1]))), maxY = Math.min(P - 1, Math.ceil(Math.max(q[0][1], q[1][1], q[2][1])));
    const den = (q[1][1] - q[2][1]) * (q[0][0] - q[2][0]) + (q[2][0] - q[1][0]) * (q[0][1] - q[2][1]);
    for (let y = minY; y <= maxY; y++) for (let x = minX; x <= maxX; x++) {
      let d, b0 = 1, b1 = 0, b2 = 0;
      if (Math.abs(den) < 1e-9) { d = Math.max(q[0][2], q[1][2], q[2][2]); if (!(x === Math.round(q[0][0]) && y === Math.round(q[0][1]))) continue; }
      else {
        const w0 = ((q[1][1] - q[2][1]) * (x - q[2][0]) + (q[2][0] - q[1][0]) * (y - q[2][1])) / den;
        const w1 = ((q[2][1] - q[0][1]) * (x - q[2][0]) + (q[0][0] - q[2][0]) * (y - q[2][1])) / den;
        const w2 = 1 - w0 - w1;
        if (w0 < -0.02 || w1 < -0.02 || w2 < -0.02) continue;
        d = w0 * q[0][2] + w1 * q[1][2] + w2 * q[2][2];
        b0 = w0; b1 = w1; b2 = w2;
      }
      if (d > depth[y * P + x]) {
        depth[y * P + x] = d;
        if (col) { const c = ONLY && !ONLY(...centre(t)) ? [150, 150, 150] : texAt(b0 * uv[0][0] + b1 * uv[1][0] + b2 * uv[2][0], b0 * uv[0][1] + b1 * uv[1][1] + b2 * uv[2][1]); const o = (y * P + x) * 3; col[o] = c[0]; col[o + 1] = c[1]; col[o + 2] = c[2]; }
      }
    }
  }
  const dl = lo[vw.d], dh = hi[vw.d];
  for (let y = 0; y < P; y++) for (let x = 0; x < P; x++) {
    const d = depth[y * P + x]; if (d < -1e8) continue;
    const t = Math.max(0, Math.min(1, (d * dSign - dl) / Math.max(1e-6, dh - dl))); const k = dSign > 0 ? t : 1 - t;
    const c = Math.round(30 + 170 * (1 - k));
    if (col) { const o = (y * P + x) * 3; set(ox + x, oy + y, col[o], col[o + 1], col[o + 2]); }
    else set(ox + x, oy + y, c, Math.round(c * 0.8), 40);
  }
});
// PNG
function writePng(file, w, h, px) { const r = Buffer.alloc((w * 3 + 1) * h); for (let y = 0; y < h; y++) { r[y * (w * 3 + 1)] = 0; px.copy(r, y * (w * 3 + 1) + 1, y * w * 3, (y + 1) * w * 3); } const ih = Buffer.alloc(13); ih.writeUInt32BE(w, 0); ih.writeUInt32BE(h, 4); ih[8] = 8; ih[9] = 2; fs.writeFileSync(file, Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk("IHDR", ih), chunk("IDAT", zlib.deflateSync(r)), chunk("IEND", Buffer.alloc(0))])); }
function crc(buf) { let c, t = []; for (let n = 0; n < 256; n++) { c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } let r = 0xFFFFFFFF; for (const x of buf) r = t[(r ^ x) & 255] ^ (r >>> 8); return (r ^ 0xFFFFFFFF) >>> 0; }
function chunk(type, data) { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); }
const raw = Buffer.alloc((W * 3 + 1) * H); for (let y = 0; y < H; y++) { raw[y * (W * 3 + 1)] = 0; img.copy(raw, y * (W * 3 + 1) + 1, y * W * 3, (y + 1) * W * 3); }
const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(W, 0); ihdr.writeUInt32BE(H, 4); ihdr[8] = 8; ihdr[9] = 2;
fs.writeFileSync(outPng, Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]));
console.log("  views → " + outPng + " (front X-Y | side Z-Y, +Z to the left | top X-Z, +Z down; grid 0.25 m, metres darker, 0 red)");

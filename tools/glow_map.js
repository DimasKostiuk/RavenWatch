// A glow map from a model's own picture: the pixels of one colour (runes painted gold, a lantern's glass, blue veins)
// kept as they are, brighter, everything else black. Put next to the model as <Name>_glow.png: the mod's loader
// (WsAssets) lays it on as the glow of every material that has none of its own.
//   node tools/glow_map.js <diffuse.png> <out.png> --hue 20-45 [--sat 0.55] [--val 0.6] [--size 512] [--gain 1.4]
//   [--cut <mask.png>] [--dim k]: black where the mask is white (tools/fbx_probe.js --uvmask: twigs on top), all × k
//   (hue in degrees: 0 red, 30 orange, 60 yellow, 120 green, 190 cyan, 230 blue, 280 violet; a range may wrap: 340-20)
const fs = require("fs"), zlib = require("zlib");
const a = process.argv.slice(2);
if (a.length < 2) { console.log("node tools/glow_map.js <diffuse.png> <out.png> --hue 20-45 [--sat 0.55] [--val 0.6] [--size 512] [--gain 1.4]"); process.exit(1); }
const opt = (k, d) => { const i = a.indexOf(k); return i >= 0 ? a[i + 1] : d; };
const [h0, h1] = opt("--hue", "20-45").split("-").map(Number);
const cutPath = opt("--cut", null), dim = parseFloat(opt("--dim", "1"));
const sat = parseFloat(opt("--sat", "0.55")), val = parseFloat(opt("--val", "0.6")), size = parseInt(opt("--size", "512")), gain = parseFloat(opt("--gain", "1.4"));

function readPng(file) {
  const b = fs.readFileSync(file);
  let p = 8, w = 0, h = 0, ct = 0; const idat = [];
  while (p < b.length) {
    const len = b.readUInt32BE(p), type = b.toString("ascii", p + 4, p + 8), d = b.subarray(p + 8, p + 8 + len);
    if (type === "IHDR") { w = d.readUInt32BE(0); h = d.readUInt32BE(4); ct = d[9]; if (d[8] !== 8) throw new Error("PNG: 8-bit only"); }
    else if (type === "IDAT") idat.push(d);
    else if (type === "IEND") break;
    p += 12 + len;
  }
  const bpp = ct === 6 ? 4 : ct === 2 ? 3 : ct === 4 ? 2 : 1, stride = w * bpp;
  const raw = zlib.inflateSync(Buffer.concat(idat)), out = Buffer.alloc(w * h * 3);
  let prev = Buffer.alloc(stride), cur = Buffer.alloc(stride);
  for (let y = 0; y < h; y++) {
    const ft = raw[y * (stride + 1)], row = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    for (let x = 0; x < stride; x++) {
      const l = x >= bpp ? cur[x - bpp] : 0, up = prev[x], c = x >= bpp ? prev[x - bpp] : 0; let v = row[x];
      if (ft === 1) v += l; else if (ft === 2) v += up; else if (ft === 3) v += (l + up) >> 1;
      else if (ft === 4) { const pp = l + up - c, pa = Math.abs(pp - l), pb = Math.abs(pp - up), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? l : pb <= pc ? up : c; }
      cur[x] = v & 255;
    }
    for (let x = 0; x < w; x++) { const o = (y * w + x) * 3; if (bpp >= 3) { out[o] = cur[x * bpp]; out[o + 1] = cur[x * bpp + 1]; out[o + 2] = cur[x * bpp + 2]; } else out[o] = out[o + 1] = out[o + 2] = cur[x * bpp]; }
    const t = prev; prev = cur; cur = t;
  }
  return { w, h, px: out };
}

function hsv(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn;
  let hh = 0;
  if (d > 0) hh = mx === r ? 60 * (((g - b) / d) % 6) : mx === g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
  if (hh < 0) hh += 360;
  return [hh, mx > 0 ? d / mx : 0, mx];
}
const inHue = hh => h0 <= h1 ? hh >= h0 && hh <= h1 : hh >= h0 || hh <= h1;

const src = readPng(a[0]), cut = cutPath ? readPng(cutPath) : null;
const cutAt = (x, y) => { const cx = Math.min(cut.w - 1, Math.floor((x + 0.5) * cut.w / size)), cy = Math.min(cut.h - 1, Math.floor((y + 0.5) * cut.h / size)); return cut.px[(cy * cut.w + cx) * 3] > 127; };
const img = Buffer.alloc(size * size * 3);
let lit = 0;
for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
  // a box of source pixels per output pixel: lit when most of it is the colour
  const sx0 = Math.floor(x * src.w / size), sx1 = Math.max(sx0 + 1, Math.floor((x + 1) * src.w / size));
  const sy0 = Math.floor(y * src.h / size), sy1 = Math.max(sy0 + 1, Math.floor((y + 1) * src.h / size));
  let n = 0, hit = 0, r = 0, g = 0, bl = 0;
  for (let sy = sy0; sy < sy1; sy++) for (let sx = sx0; sx < sx1; sx++) {
    const o = (sy * src.w + sx) * 3; n++;
    const [hh, ss, vv] = hsv(src.px[o], src.px[o + 1], src.px[o + 2]);
    if (inHue(hh) && ss >= sat && vv >= val) { hit++; r += src.px[o]; g += src.px[o + 1]; bl += src.px[o + 2]; }
  }
  if (hit === 0 || (cut && cutAt(x, y))) continue;
  const k = hit / n * dim, o = (y * size + x) * 3;
  img[o] = Math.min(255, Math.round(r / hit * gain * k)); img[o + 1] = Math.min(255, Math.round(g / hit * gain * k)); img[o + 2] = Math.min(255, Math.round(bl / hit * gain * k));
  lit++;
}
function crc(buf) { let c, t = []; for (let n = 0; n < 256; n++) { c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } let r = 0xFFFFFFFF; for (const x of buf) r = t[(r ^ x) & 255] ^ (r >>> 8); return (r ^ 0xFFFFFFFF) >>> 0; }
function chunk(type, data) { const l = Buffer.alloc(4); l.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc(td)); return Buffer.concat([l, td, c]); }
const raw = Buffer.alloc((size * 3 + 1) * size); for (let y = 0; y < size; y++) { raw[y * (size * 3 + 1)] = 0; img.copy(raw, y * (size * 3 + 1) + 1, y * size * 3, (y + 1) * size * 3); }
const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4); ihdr[8] = 8; ihdr[9] = 2;
fs.writeFileSync(a[1], Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0))]));
console.log(a[1].split(/[\\/]/).pop() + ": " + lit + " of " + size * size + " pixels glow (" + (100 * lit / size / size).toFixed(2) + "%), " + Math.round(fs.statSync(a[1]).size / 1024) + " KB");

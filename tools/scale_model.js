// Scales a Workshop model file (assets/perches/<id>.txt) as a whole about its ground point (0,0,0): every part's place
// and size × k, so the parts stay where they were on it. A light keeps its brightness and flicker (only its reach
// grows); a raven's seat keeps its size (the raven is not bigger).
//   node tools/scale_model.js <model.txt> <k>
const fs = require('fs');
const [file, kArg] = process.argv.slice(2);
const k = parseFloat(kArg);
if (!file || !(k > 0)) { console.error('node tools/scale_model.js <model.txt> <k>'); process.exit(1); }
const n = (v) => { const r = Math.round(parseFloat(v) * k * 1000) / 1000; return String(r); };
let changed = 0;
const out = fs.readFileSync(file, 'utf8').split('\n').map(line => {
  if (!line.startsWith('part ')) return line;
  const hash = line.indexOf('#');
  const body = hash >= 0 ? line.slice(0, hash).trimEnd() : line, rest = hash >= 0 ? '  ' + line.slice(hash) : '';
  const t = body.split(/\s+/);
  const what = t[1];
  const p = t.indexOf('pos'), s = t.indexOf('scale');
  if (p > 0) for (let i = 1; i <= 3; i++) t[p + i] = n(t[p + i]);
  if (s > 0 && what !== '@seat') {
    if (what === '@light') t[s + 1] = n(t[s + 1]); // (reach only)
    else for (let i = 1; i <= 3; i++) t[s + i] = n(t[s + i]);
  }
  changed++;
  return t.join(' ') + rest;
});
fs.writeFileSync(file, out.join('\n'));
console.log(file + ': ' + changed + ' parts × ' + k);

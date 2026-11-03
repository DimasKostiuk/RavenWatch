// The phrase books hold together (both ravens, Ukrainian src/*Patterns.cs and English assets/lang/English): every list a
// template names exists, every list is used by a template, every tag can say something at every trust tier, and the
// English book has the same tags and lists. Run: node tools/bookcheck.js  (exit code 1 on a problem: rw_build stops)
const fs = require('fs');
const path = require('path');
const root = path.join(__dirname, '..') + '/', src = root + 'src/';
let problems = 0;
function arrays(file, name) {
  const s = fs.readFileSync(src + file, 'utf8'); const i = s.indexOf('string[] ' + name + ' ='); if (i < 0) return [];
  const j = s.indexOf('\n        };', i); const out = []; const re = /^\s*"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?$/gm; let m;
  while ((m = re.exec(s.slice(i, j)))) out.push(JSON.parse('"' + m[1] + '"')); return out;
}
const P = fs.readFileSync(src + 'Patterns.cs', 'utf8').split('\n');
const fH = ['Patterns.cs'].concat([...P.find(l => l.includes('_allH = l.ToArray()')).matchAll(/AddRange\((\w+)\.Hugin\)/g)].map(m => m[1] + '.cs'));
const fM = ['Patterns.cs'].concat([...P.find(l => l.includes('_allM = l.ToArray()')).matchAll(/AddRange\((\w+)\.Munin\)/g)].map(m => m[1] + '.cs'));
function book(lines) {
  const t = {}, p = {}; let cur = null;
  for (const raw of lines) { const l = raw.trim(); if (!l || l[0] === '#') continue;
    if (/^(~\d+\s*)?\[/.test(l) && !/^(~\d+\s*)?\[[^\]]*\]\s*$/.test('')) { const m = l.match(/^(~\d+\s*)?\[([A-Za-z0-9_]+)]\s*(.*)$/); if (m) { (t[m[2]] = t[m[2]] || []).push((m[1] || '') + m[3]); cur = null; continue; } }
    if (l[0] === '@') { cur = l.slice(1).trim(); p[cur] = p[cur] || []; continue; } if (cur) p[cur].push(l); }
  return { t, p };
}
const tone = (line, tier) => { const s = line.trimStart(); const m = s.match(/^~(\d+)\s*/); if (!m) return line; return m[1].includes(String(tier)) ? s.slice(m[0].length) : null; };
const strip = l => l.replace(/^%\d+\s*/, '').replace(/^\^\S*\s*/, '');
function audit(name, b) {
  const used = new Set(), missing = new Set(), holes = [];
  const refs = s => [...strip(s).matchAll(/\{([^{}|]+?)(\?)?\}/g)];
  // lists named inside lists count too (one level)
  for (const tag in b.t) for (const tpl of b.t[tag]) for (const m of refs(tpl.replace(/^~\d+\s*/, ''))) { if (b.p[m[1]]) used.add(m[1]); else if (/[а-яіїєґ]/.test(m[1])) missing.add(m[1] + ' (in [' + tag + '])'); }
  for (const n in b.p) for (const l of b.p[n]) for (const m of refs(l.replace(/^~\d+\s*/, ''))) if (b.p[m[1]]) used.add(m[1]);
  for (const tag in b.t) for (const tier of [1, 2, 3]) {
    const ok = b.t[tag].some(tpl0 => { const tpl = tone(tpl0, tier); if (tpl == null) return false;
      return refs(tpl).every(m => m[2] || !b.p[m[1]] || b.p[m[1]].some(l => tone(l, tier) != null)); });
    if (!ok) holes.push(tag + ' ~' + tier);
  }
  const orphan = Object.keys(b.p).filter(n => !used.has(n));
  problems += missing.size + orphan.length + holes.length;
  console.log(`\n== ${name}: tags ${Object.keys(b.t).length}, lists ${Object.keys(b.p).length}`);
  console.log(`  lists named but missing (${missing.size}): ${[...missing].slice(0, 20).join(', ')}`);
  console.log(`  lists nobody uses (${orphan.length}): ${orphan.slice(0, 40).join(', ')}`);
  console.log(`  tag+tier with nothing to say (${holes.length}): ${holes.slice(0, 40).join(', ')}`);
}
const books = {};
for (const [k, files, n] of [['H', fH, 'Hugin'], ['M', fM, 'Munin']]) {
  books[k] = book([].concat(...files.map(f => arrays(f, n))));
  audit('UK ' + n, books[k]);
  const en = book(fs.readFileSync(root + 'assets/lang/English/' + n.toLowerCase() + '_patterns.txt', 'utf8').split(/\r?\n/));
  audit('EN ' + n, en);
  const ukT = new Set(Object.keys(books[k].t)), enT = new Set(Object.keys(en.t));
  const tm = [...ukT].filter(t => !enT.has(t)).length + [...enT].filter(t => !ukT.has(t)).length; problems += tm;
  console.log(`  EN tags missing vs UK: ${[...ukT].filter(t => !enT.has(t)).join(', ') || 'none'}`);
  console.log(`  EN tags not in UK: ${[...enT].filter(t => !ukT.has(t)).join(', ') || 'none'}`);
  const ukL = new Set(Object.keys(books[k].p)), enL = new Set(Object.keys(en.p));
  console.log(`  EN lists missing vs UK: ${[...ukL].filter(t => !enL.has(t)).slice(0, 30).join(', ') || 'none'}`);
}

console.log('bookcheck: ' + problems + ' problem(s)');
if (problems) process.exitCode = 1;

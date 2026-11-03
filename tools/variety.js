// How many different things a raven can say on each tag: per raven and trust tier, the «heart» (the biggest list of every
// template, summed: the lines that differ in sense, not just in their bits) and all the combinations.
//   node tools/variety.js [minHeart]   → build/samples/_variety.txt (thinnest first) and a summary
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), src = path.join(root, 'src');
const minHeart = +(process.argv[2] || 8);

function arrays(file, name) {
  const s = fs.readFileSync(path.join(src, file), 'utf8');
  const i = s.indexOf('string[] ' + name + ' =');
  if (i < 0) return [];
  const j = s.indexOf('\n        };', i);
  const res = [], re = /^\s*"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?$/gm, body = s.slice(i, j);
  let m;
  while ((m = re.exec(body))) res.push(JSON.parse('"' + m[1] + '"'));
  return res;
}
const allLine = fs.readFileSync(path.join(src, 'Patterns.cs'), 'utf8').split('\n').find(l => l.includes('_allH = l.ToArray()'));
const files = ['Patterns.cs'].concat([...allLine.matchAll(/AddRange\((\w+)\.Hugin\)/g)].map(m => m[1] + '.cs'));
function book(name) {
  const t = {}, p = {}, fileOf = {};
  for (const f of files) {
    let cur = null;
    for (const raw of arrays(f, name)) {
      const l = raw.trim();
      if (!l || l.startsWith('#')) continue;
      if (l.startsWith('[')) { const e = l.indexOf(']'), tag = l.slice(1, e); (t[tag] = t[tag] || []).push(l.slice(e + 1).trim()); fileOf[tag] = fileOf[tag] || f; cur = null; continue; }
      if (l.startsWith('@')) { cur = l.slice(1).trim(); p[cur] = p[cur] || []; continue; }
      if (cur) p[cur].push(l);
    }
  }
  return { t, p, fileOf };
}
// the whole lines of Quips.cs / Defaults*.cs ([tag] line), a spare beside the assembled ones
function whole(file, name) {
  const out = {};
  for (const l of arrays(file, name)) { const m = l.match(/^\[([a-z0-9_]+)\]\s*(.*)$/); if (m) (out[m[1]] = out[m[1]] || []).push(m[2]); }
  return out;
}
function tone(line, tier) {
  const s = line.trimStart();
  if (s.length < 2 || s[0] !== '~') return line;
  let i = 1, fits = false;
  while (i < s.length && /\d/.test(s[i])) { if (+s[i] === tier) fits = true; i++; }
  if (i === 1) return line;
  return fits ? s.slice(i).trimStart() : null;
}
const strip = l => l.replace(/^%\d+\s*/, '').replace(/^\^\S*\s*/, '');
function measure(b, tag, tier) {
  let heart = 0, combos = 0, temps = 0;
  for (const t0 of b.t[tag]) {
    const t = tone(t0, tier); if (t == null) continue;
    temps++;
    let big = 1, prod = 1, any = false;
    for (const m of strip(t).matchAll(/\{([^{}|]+?)(\?)?\}/g)) {
      const list = b.p[m[1]]; if (!list) continue;
      const n = list.map(x => tone(x, tier)).filter(x => x != null).length;
      prod *= m[2] ? n + 1 : Math.max(n, 1);
      if (!m[2] || !any) { if (!m[2]) { big = any ? Math.max(big, n) : n; any = true; } }
    }
    heart += any ? big : 1;
    combos += prod;
  }
  return { heart, combos, temps };
}
const rows = [];
const quips = { Hugin: Object.assign({}, whole('Quips.cs', 'Hugin')), Munin: Object.assign({}, whole('Quips.cs', 'Munin')) };
for (const who of ['Hugin', 'Munin']) {
  const b = book(who);
  for (const tag of Object.keys(b.t)) {
    const r = { who, tag, file: b.fileOf[tag].replace('.cs', ''), h: [], c: [] };
    for (const tier of [1, 2, 3]) { const m = measure(b, tag, tier); r.h.push(m.heart); r.c.push(m.combos); }
    r.min = Math.min(...r.h);
    r.spare = (quips[who][tag] || []).length;
    rows.push(r);
  }
}
rows.sort((a, b) => a.min - b.min || a.tag.localeCompare(b.tag));
const thin = rows.filter(r => r.min < minHeart);
const line = r => `${r.who[0]} ${r.tag.padEnd(28)} серце ~1/~2/~3: ${r.h.join('/').padEnd(10)} комбінацій: ${r.c.join('/').padEnd(16)} ${r.spare ? 'цілих: ' + r.spare + '  ' : ''}(${r.file})`;
fs.mkdirSync(path.join(root, 'build', 'samples'), { recursive: true });
fs.writeFileSync(path.join(root, 'build', 'samples', '_variety.txt'), `# tags with a heart under ${minHeart} at some tier: ${thin.length} of ${rows.length}\n` + rows.map(line).join('\n') + '\n');
const byFile = {};
for (const r of thin) byFile[r.file] = (byFile[r.file] || 0) + 1;
console.log(`tags ${rows.length} | thin (<${minHeart}) ${thin.length}`);
console.log(Object.entries(byFile).sort((a, b) => b[1] - a[1]).map(([f, n]) => f + ' ' + n).join(', '));

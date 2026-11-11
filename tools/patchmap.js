// What of the game the mod leans on, by the game's class: the answer to «the game was updated — what do we look at first». Read from src/
// as it stands, so it is never stale (a list kept in a document would be). The build's patchcheck says whether each patched method still
// EXISTS on its type; this says where the mod is, and what breaks without a word: a transpiler (it rewrites the method's body), a private
// field or method named in a string or as a «___field» parameter (renamed in the game: null, or a throw at the first call).
//   node tools/patchmap.js               every class of the game we patch, the most patched first: patches, methods, transpilers,
//                                        private members named, our files
//   node tools/patchmap.js <Class>       that class in full: each method, the kind of each patch (prefix, postfix, transpiler,
//                                        finalizer), our file and line, the private members it names
// Which patches must be guarded (PatchGuard) is arch_check's rule, not shown here.
//   node tools/patchmap.js --fragile     only what breaks silently: the transpilers and every private member named, by class
const fs = require('fs'), path = require('path');
const src = path.join(__dirname, '..', 'src');
const files = fs.readdirSync(src).filter(f => f.endsWith('.cs')).sort();
const ours = new Set();
const text = {};
for (const f of files) { text[f] = fs.readFileSync(path.join(src, f), 'utf8').split(/\r?\n/); for (const l of text[f]) { const m = /\b(?:class|struct|enum|interface)\s+([A-Z]\w*)/.exec(l); if (m) ours.add(m[1]); } }

const classes = {};   // game class → { patches: [], named: Map(member → Set(file)) }
const cls = n => (classes[n] = classes[n] || { patches: [], named: new Map() });
const name = (c, member, file) => { const m = cls(c).named; if (!m.has(member)) m.set(member, new Set()); m.get(member).add(file); };
const loose = [];     // patches whose target is not written in the attribute (TargetMethod, harmony.Patch)

for (const f of files) {
  const L = text[f];
  for (let i = 0; i < L.length; i++) {
    const line = L[i];
    if (/^\s*\/\//.test(line)) continue;
    const heads = [...line.matchAll(/\[HarmonyPatch\(typeof\(([\w.]+)\),\s*"(\w+)"/g)];
    const bare = /\[HarmonyPatch\]/.test(line);
    if (heads.length || bare) {
      // the patch's own lines: to the next patch attribute on a later line, or 120 lines
      let end = i + 1; while (end < L.length && end < i + 120 && !/\[HarmonyPatch[\](]/.test(L[end])) end++;
      const body = L.slice(i, end).join('\n');
      const kinds = [...new Set([...body.matchAll(/\b(Prefix|Postfix|Transpiler|Finalizer)\s*\(/g)].map(m => m[1].toLowerCase()))];
      const patchClass = (/\bclass\s+(\w+)/.exec(body) || [])[1] || '?';
      const fields = [...new Set([...body.matchAll(/\b___(\w+)/g)].map(m => m[1]))];
      if (bare && !heads.length) loose.push({ file: f, line: i + 1, patchClass, what: 'TargetMethod', kinds });
      for (const h of heads) {
        const game = h[1].split('.').pop();
        cls(game).patches.push({ method: h[2], kinds, file: f, line: i + 1, patchClass, fields });
        for (const fl of fields) name(game, fl, f);
      }
    }
    if (/\bharmony\w*\.Patch\(/i.test(line)) loose.push({ file: f, line: i + 1, patchClass: '', what: 'harmony.Patch', kinds: [] });
    // a private member of a game's class named in a string, outside the attributes
    for (const m of line.matchAll(/AccessTools\.(?:Field|Method|Property|PropertyGetter|PropertySetter|FieldRefAccess<[^>]*>)\(typeof\((\w+)\),\s*"(\w+)"/g)) if (!ours.has(m[1])) name(m[1], m[2], f);
    for (const m of line.matchAll(/typeof\((\w+)\)\.Get(?:Field|Method|Property)\("(\w+)"/g)) if (!ours.has(m[1])) name(m[1], m[2], f);
  }
}

const arg = process.argv[2];
const pad = (s, n) => String(s).padEnd(n), num = (s, n) => String(s).padStart(n);
const names = Object.keys(classes).filter(c => classes[c].patches.length || classes[c].named.size).sort((a, b) => classes[b].patches.length - classes[a].patches.length || a.localeCompare(b));
const trans = c => classes[c].patches.filter(p => p.kinds.includes('transpiler'));

if (arg && arg !== '--fragile') {
  const c = classes[arg];
  if (!c) { console.log('nothing of ours on ' + arg + ' (a class of the game, as written in typeof(…))'); process.exit(1); }
  console.log(arg + ': ' + c.patches.length + ' patch(es)');
  const by = {}; for (const p of c.patches) (by[p.method] = by[p.method] || []).push(p);
  for (const m of Object.keys(by).sort()) {
    console.log('  ' + m);
    for (const p of by[m]) console.log('    ' + pad(p.kinds.join('+') || '?', 20) + ' ' + pad(p.file + ':' + p.line, 30) + ' ' + p.patchClass + (p.fields.length ? '  names: ' + p.fields.join(', ') : ''));
  }
  if (c.named.size) { console.log('  private members named (a rename in the game breaks these silently):'); for (const [m, fs2] of [...c.named].sort()) console.log('    ' + pad(m, 28) + [...fs2].join(', ')); }
} else if (arg === '--fragile') {
  console.log('What breaks without a word after a game update (the build\'s patchcheck does not see these):\n');
  for (const c of names) {
    const t = trans(c), n = classes[c].named;
    if (!t.length && !n.size) continue;
    console.log(c);
    for (const p of t) console.log('  transpiler  ' + pad(p.method, 26) + p.file + ':' + p.line);
    if (n.size) console.log('  named       ' + [...n.keys()].sort().join(', '));
  }
} else {
  console.log(pad('game class', 26) + num('patches', 8) + num('methods', 9) + num('transp.', 9) + num('named', 7) + '  our files');
  let total = 0, tr = 0, nm = 0;
  for (const c of names) {
    const p = classes[c].patches, fl = new Set(p.map(x => x.file));
    total += p.length; tr += trans(c).length; nm += classes[c].named.size;
    if (!p.length) continue;
    console.log(pad(c, 26) + num(p.length, 8) + num(new Set(p.map(x => x.method)).size, 9) + num(trans(c).length || '', 9) + num(classes[c].named.size || '', 7) + '  ' + [...fl].slice(0, 5).join(', ') + (fl.size > 5 ? ' +' + (fl.size - 5) : ''));
  }
  const onlyNamed = names.filter(c => !classes[c].patches.length);
  console.log('\n' + total + ' patches on ' + names.filter(c => classes[c].patches.length).length + ' classes of the game; ' + tr + ' transpilers; ' + nm + ' private members named' +
    (onlyNamed.length ? ' (' + onlyNamed.length + ' more classes are only named, not patched: --fragile)' : ''));
  if (loose.length) { console.log('\ntarget not written in the attribute (' + loose.length + '): ' + loose.map(l => l.file + ':' + l.line + ' ' + l.what).join(', ')); }
  console.log('\none class in full: node tools/patchmap.js <Class>;  what breaks silently: node tools/patchmap.js --fragile');
}

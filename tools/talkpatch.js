// Applies line edits to the phrase lists in both languages at once: the Ukrainian ones in src/*Patterns.cs and the same
// line (by its place in the list) in assets/lang/English/<who>_patterns.txt, so the two never drift apart.
//   node tools/talkpatch.js edits.json [--dry]
// edits.json: [{ "who": "H"|"M", "list": "х_кінець", "op": "set"|"del"|"add", "old": "the Ukrainian line as written
//   (marks included)", "uk": "the new line", "en": "its English" }]  (set: old -> uk/en; del: old; add: uk/en at the
//   end of the list, in the file where the list's last block is)
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..'), src = path.join(root, 'src'), enDir = path.join(root, 'assets', 'lang', 'English');
const files = ['Patterns.cs', 'TalkPatterns.cs', 'PerchPatterns.cs', 'CrossPatterns.cs', 'AboutPatterns.cs', 'StrikePatterns.cs', 'DeathPatterns.cs', 'EmotePatterns.cs', 'PassPatterns.cs', 'LifePatterns.cs', 'VarietyPatterns.cs', 'QuipPatterns.cs', 'PlacePatterns.cs', 'LifeTalkPatterns.cs', 'AgainPatterns.cs', 'WorldPatterns.cs', 'WardPatterns.cs', 'ErrandPatterns.cs', 'MorePatterns.cs', 'MomentPatterns.cs'];
const NAME = { H: 'Hugin', M: 'Munin' };
const text = {}; // file -> lines
const read = f => text[f] || (text[f] = fs.readFileSync(f, 'utf8').split('\n'));

// where every item of every list is: [{file, line, value}]
function ukIndex(who) {
  const idx = {};
  for (const f of files) {
    const full = path.join(src, f), L = read(full);
    let inArr = false, cur = null;
    for (let i = 0; i < L.length; i++) {
      const l = L[i];
      if (!inArr) { if (l.includes('string[] ' + NAME[who] + ' =')) { inArr = true; cur = null; } continue; }
      if (/^\s{8}};/.test(l)) { inArr = false; continue; }
      const m = /^(\s*)"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?\r?$/.exec(l);
      if (!m) continue;
      const v = JSON.parse('"' + m[2] + '"').trim();
      if (!v || v.startsWith('#')) continue;
      if (v.startsWith('[')) { cur = null; continue; }
      if (v.startsWith('@')) { cur = v.slice(1); (idx[cur] = idx[cur] || []); continue; }
      if (cur) idx[cur].push({ file: full, line: i, value: v, indent: m[1] });
    }
  }
  return idx;
}
function enIndex(who) {
  const full = path.join(enDir, who === 'H' ? 'hugin_patterns.txt' : 'munin_patterns.txt'), L = read(full), idx = {};
  let cur = null;
  for (let i = 0; i < L.length; i++) {
    const v = L[i].trim();
    if (!v || v.startsWith('#')) continue;
    if (v.startsWith('[')) { cur = null; continue; }
    if (v.startsWith('@')) { cur = v.slice(1); (idx[cur] = idx[cur] || []); continue; }
    if (cur) idx[cur].push({ file: full, line: i, value: v, indent: '' });
  }
  return idx;
}
const cs = s => '"' + s.replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '",';

const edits = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const dry = process.argv.includes('--dry');
const changes = {}; // file -> [{line, kind: set|del|after, text}]
const push = (f, c) => (changes[f] = changes[f] || []).push(c);
const I = { H: { uk: ukIndex('H'), en: enIndex('H') }, M: { uk: ukIndex('M'), en: enIndex('M') } };
let bad = 0;
for (const e of edits) {
  const uk = I[e.who].uk[e.list], en = I[e.who].en[e.list];
  if (!uk || !en) { console.log('NO LIST', e.who, e.list); bad++; continue; }
  if (e.op === 'add') {
    const a = uk[uk.length - 1], b = en[en.length - 1];
    push(a.file, { line: a.line, kind: 'after', text: a.indent + cs(e.uk) });
    push(b.file, { line: b.line, kind: 'after', text: e.en });
    continue;
  }
  const i = uk.findIndex(x => x.value === e.old.trim() && !x.used);
  if (i < 0) { console.log('NOT FOUND', e.who, e.list, e.old); bad++; continue; }
  uk[i].used = true;
  if (!en[i]) { console.log('NO ENGLISH AT', i, e.list); bad++; continue; }
  if (e.op === 'del') { push(uk[i].file, { line: uk[i].line, kind: 'del' }); push(en[i].file, { line: en[i].line, kind: 'del' }); continue; }
  push(uk[i].file, { line: uk[i].line, kind: 'set', text: uk[i].indent + cs(e.uk) });
  push(en[i].file, { line: en[i].line, kind: 'set', text: e.en });
}
for (const f in changes) {
  const L = text[f], eol = L[0].endsWith('\r') ? '\r' : '';
  // from the bottom up, the adds after a line in their order
  const byLine = {};
  for (const c of changes[f]) (byLine[c.line] = byLine[c.line] || []).push(c);
  for (const ln of Object.keys(byLine).map(Number).sort((a, b) => b - a)) {
    const cs_ = byLine[ln], set = cs_.find(c => c.kind === 'set'), del = cs_.find(c => c.kind === 'del'), after = cs_.filter(c => c.kind === 'after');
    const add = after.map(c => c.text + eol);
    if (del) L.splice(ln, 1, ...add);
    else { if (set) L[ln] = set.text + eol; L.splice(ln + 1, 0, ...add); }
  }
  if (!dry) fs.writeFileSync(f, L.join('\n'));
}
console.log(`${edits.length} edits, ${bad} failed, files: ${Object.keys(changes).map(f => path.basename(f)).join(', ')}${dry ? ' (dry run)' : ''}`);
process.exitCode = bad ? 1 : 0;

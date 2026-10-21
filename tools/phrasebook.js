// The raven phrasebook: reads every phrase list from the mod's source (Patterns, TalkPatterns, PerchPatterns,
// CrossPatterns, AboutPatterns and the rest of the *Patterns files, in Patterns.All order) and writes one page, tools/phrasebook/raven-phrases.html, to read, try and edit them.
// Run: node tools/phrasebook.js   (then open the page in a browser; edits stay in that browser, «Копіювати зміни»
// gives them in the phrase files' format to bring back into the code)
const fs = require('fs'), path = require('path');
const src = path.join(__dirname, '..', 'src');
const files = ['Patterns.cs', 'TalkPatterns.cs', 'PerchPatterns.cs', 'CrossPatterns.cs', 'AboutPatterns.cs', 'StrikePatterns.cs', 'DeathPatterns.cs', 'EmotePatterns.cs', 'PassPatterns.cs', 'LifePatterns.cs', 'VarietyPatterns.cs', 'QuipPatterns.cs', 'PlacePatterns.cs', 'LifeTalkPatterns.cs', 'AgainPatterns.cs', 'WorldPatterns.cs', 'WardPatterns.cs', 'ErrandPatterns.cs', 'MorePatterns.cs', 'MomentPatterns.cs'];

function arrays(file, name) {
  const s = fs.readFileSync(path.join(src, file), 'utf8');
  const i = s.indexOf('string[] ' + name + ' =');
  if (i < 0) return [];
  const j = s.indexOf('\n        };', i);
  const out = [], re = /^\s*"((?:[^"\\]|\\.)*)",\s*(?:\/\/.*)?$/gm, body = s.slice(i, j);
  let m;
  while ((m = re.exec(body))) out.push(JSON.parse('"' + m[1] + '"'));
  return out;
}

function book(name) {
  const tpl = {}, lists = {}, order = [], fileOf = {};
  for (const f of files) {
    let cur = null;
    for (const raw of arrays(f, name)) {
      const l = raw.trim();
      if (!l || l.startsWith('#')) continue;
      if (l.startsWith('[')) {
        const e = l.indexOf(']');
        const tag = l.slice(1, e), body = l.slice(e + 1).trim();
        if (!tpl[tag]) { tpl[tag] = []; order.push(tag); fileOf[tag] = f; }
        tpl[tag].push(body);
        cur = null;
        continue;
      }
      if (l.startsWith('@')) { cur = l.slice(1); if (!lists[cur]) lists[cur] = []; continue; }
      if (cur) lists[cur].push(l);
    }
  }
  return { tpl, lists, order, fileOf };
}

const data = { hugin: book('Hugin'), munin: book('Munin'), built: new Date().toISOString().slice(0, 16).replace('T', ' ') };
const tplPage = fs.readFileSync(path.join(__dirname, 'phrasebook.tpl.html'), 'utf8');
const outDir = path.join(__dirname, 'phrasebook');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir);
const out = path.join(outDir, 'raven-phrases.html');
fs.writeFileSync(out, tplPage.replace('/*DATA*/null', JSON.stringify(data).replace(/</g, '\\u003c')));
let t = 0, l = 0;
for (const r of [data.hugin, data.munin]) { t += Object.keys(r.tpl).length; for (const k in r.lists) l += r.lists[k].length; }
console.log('written', out, '| tags', t, '| list lines', l);

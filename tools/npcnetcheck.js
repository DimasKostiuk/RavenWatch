// npcnetcheck: a person's ZDO stays light (the two-player test 2026-11-05: 25 people of 1–1.5 KB each starved the net — the second player
// never got the base; a person's ZDO is sent whole each time anything in it changes, twenty times a second while it walks, before pieces).
// Two rules, read from the code (src/Npc*, Story*, Body*, Guard*):
//   1. a value that lives in a person's locker (NpcLocker.Lives) is written only through NpcLocker.Put/PutRef, never with ZDO.Set
//   2. a key written with ZDO.Set there that this list has not seen before stops the build: decide where it lives —
//        on the person (every game must see it the very moment it changes: a pose, a clip, a seat, who talks to it…) → add it to tools/npcnet_keys.json "person"
//        in its locker (seldom changes, read now and then)                                  → NpcLocker.Lives, written with NpcLocker.Put
//        on another thing (a heart, a raven, a locker, a prop, a chest)                    → tools/npcnet_keys.json "other"
//   node tools/npcnetcheck.js          the problems; exit code 1 stops rw_build
const fs = require('fs'), path = require('path');
const src = path.join(__dirname, '..', 'src');
const listFile = path.join(__dirname, 'npcnet_keys.json');
const list = fs.existsSync(listFile) ? JSON.parse(fs.readFileSync(listFile, 'utf8')) : { person: [], other: [] };
const known = new Set([...list.person, ...list.other]);
const lockerSrc = fs.readFileSync(path.join(src, 'NpcLocker.cs'), 'utf8');
const livesBlock = (lockerSrc.match(/_lives = new Dictionary<int, Kind>\s*\{([\s\S]*?)\}\);/) || [, ''])[1];
const lives = new Set([...livesBlock.matchAll(/\{\s*\w+\.(K\w+),/g)].map(m => m[1]));
const problems = [];
const seen = new Set();
for (const f of fs.readdirSync(src)) {
  if (!/^(Npc|Story|Body|Guard)\w*\.cs$/.test(f) || f === 'NpcLocker.cs') continue;
  const lines = fs.readFileSync(path.join(src, f), 'utf8').split(/\r?\n/);
  lines.forEach((line, i) => {
    if (/^\s*\/\//.test(line)) return;
    for (const m of line.matchAll(/\.Set\(\s*(?:[A-Za-z]+\.)?(K[A-Za-z0-9]*)\s*,/g)) {
      const k = m[1];
      if (line.includes('NpcLocker.Put')) continue;
      seen.add(k);
      if (lives.has(k)) problems.push(`${f}:${i + 1}  ${k} lives in the locker (NpcLocker.Lives): write it with NpcLocker.Put, not ZDO.Set`);
      else if (!known.has(k)) problems.push(`${f}:${i + 1}  new ZDO key ${k}: on the person (every game sees it at once) → tools/npcnet_keys.json "person"; seldom read → NpcLocker.Lives + NpcLocker.Put; another thing's → "other"`);
    }
  });
}
if (process.argv[2] === '--new-list') {
  const person = [...seen].filter(k => !lives.has(k)).sort();
  fs.writeFileSync(listFile, JSON.stringify({ person, other: [] }, null, 1) + '\n');
  console.log('npcnetcheck: list written,', person.length, 'keys — sort them into person/other by hand');
  process.exit(0);
}
for (const p of problems) console.log(p);
console.log(`npcnetcheck: ${problems.length} problem(s) (${lives.size} in the locker, ${list.person.length} on the person, ${list.other.length} on other things)`);
process.exit(problems.length ? 1 : 0);

// RavenWatch: a model maker's archive (DevKit «Запакувати готове») into the mod.
//   node tools/import_models.js <archive.zip>
// Models go by their "use:" to assets/perches (perch, station), assets/gear (gear), assets/items (item), their photos to
// assets/photos; the fits approved
// in the book ("ok" lines of outfits.txt) for what came in are printed, to be written into Outfits.Builtin.
const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

const zip = process.argv[2];
if (!zip || !fs.existsSync(zip)) { console.error('usage: node tools/import_models.js <archive.zip>'); process.exit(1); }
const root = path.join(__dirname, '..');
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'rw_import_'));
const ps = spawnSync('powershell', ['-NoProfile', '-Command', `Expand-Archive -LiteralPath '${path.resolve(zip).replace(/'/g, "''")}' -DestinationPath '${tmp.replace(/'/g, "''")}' -Force`], { encoding: 'utf8' });
if (ps.status !== 0) { console.error('unzip failed:', ps.stderr); process.exit(1); }

const head = (t, k) => { const m = t.match(new RegExp('^' + k + ':\\s*(.*)$', 'm')); return m ? m[1].trim() : ''; };
const dirs = { perch: 'perches', station: 'perches', gear: 'gear', item: 'items' };
const ids = {}; // id → file, of what the mod has already
for (const d of new Set(Object.values(dirs))) {
  const full = path.join(root, 'assets', d);
  if (!fs.existsSync(full)) continue;
  for (const f of fs.readdirSync(full)) if (f.endsWith('.txt')) { const id = head(fs.readFileSync(path.join(full, f), 'utf8'), 'id'); if (id) ids[d + '|' + id] = f; }
}
const list = path.join(tmp, 'список.txt');
if (fs.existsSync(list)) console.log(fs.readFileSync(list, 'utf8'));

const keys = [];
const mdir = path.join(tmp, 'models');
for (const f of fs.existsSync(mdir) ? fs.readdirSync(mdir) : []) {
  const text = fs.readFileSync(path.join(mdir, f), 'utf8');
  const use = head(text, 'use'), id = head(text, 'id'), slot = head(text, 'slot');
  const d = dirs[use];
  if (!d) { console.log('SKIP (не для мода):', f); continue; }
  const destDir = path.join(root, 'assets', d);
  fs.mkdirSync(destDir, { recursive: true });
  let dest = path.join(destDir, f);
  if (ids[d + '|' + id] && ids[d + '|' + id] !== f) { console.log('УВАГА: id «' + id + '» уже має ' + ids[d + '|' + id] + ' — ' + f + ' не скопійовано, дай йому інший id'); continue; }
  if (fs.existsSync(dest) && fs.readFileSync(dest, 'utf8') !== text) console.log('ЗАМІНА:', path.relative(root, dest));
  fs.writeFileSync(dest, text, 'utf8');
  console.log('OK', use + (slot ? '/' + slot : ''), '→', path.relative(root, dest));
  if (use === 'gear') keys.push('gear_' + id);
}

// the photos taken in the tab «Фото» (photos/<id>.png): into assets/photos, built into the DLL
const pdir = path.join(tmp, 'photos');
for (const f of fs.existsSync(pdir) ? fs.readdirSync(pdir) : []) {
  if (!f.endsWith('.png')) continue;
  const destDir = path.join(root, 'assets', 'photos');
  fs.mkdirSync(destDir, { recursive: true });
  fs.copyFileSync(path.join(pdir, f), path.join(destDir, f));
  console.log('OK фото →', path.join('assets', 'photos', f));
}

const fits = path.join(tmp, 'outfits.txt');
if (fs.existsSync(fits)) {
  const lines = fs.readFileSync(fits, 'utf8').split(/\r?\n/).filter(l => / ok\s*$/.test(l) && !l.startsWith('#'));
  const mine = lines.filter(l => keys.some(k => l.startsWith(k + ' ') || l.startsWith(k + '@')) || /^Helmet/.test(l));
  console.log('\n# посадки «Готово» для Outfits.Builtin (' + mine.length + '):');
  for (const l of mine) console.log(l);
}
fs.rmSync(tmp, { recursive: true, force: true });

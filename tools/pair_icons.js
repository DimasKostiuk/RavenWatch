// Makes the paired gifts' medallions assets/icons/pload.png, pclear.png, pwhirl.png from tools/pair_icons.html with headless Chrome.
const { execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const page = 'file:///' + path.resolve(__dirname, 'pair_icons.html').replace(/\\/g, '/');
const dom = execFileSync(chrome, ['--headless=new', '--disable-gpu', '--allow-file-access-from-files', '--virtual-time-budget=5000', '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
for (const id of ['pload', 'pclear', 'pwhirl']) {
  const m = dom.match(new RegExp(id + ' data:image/png;base64,([A-Za-z0-9+/=]+)'));
  if (!m) { console.error('no picture made: ' + id); process.exit(1); }
  fs.writeFileSync(path.resolve(__dirname, '..', 'assets', 'icons', id + '.png'), Buffer.from(m[1], 'base64'));
  console.log('assets/icons/' + id + '.png written');
}

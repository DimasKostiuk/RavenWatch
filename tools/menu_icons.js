// Makes the orders circle's icons and button frames (assets/menu/o-*.png, f-*.png) from tools/menu_icons.html with headless Chrome.
const { execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const page = 'file:///' + path.resolve(__dirname, 'menu_icons.html').replace(/\\/g, '/');
const dom = execFileSync(chrome, ['--headless=new', '--disable-gpu', '--virtual-time-budget=5000', '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
const out = path.resolve(__dirname, '..', 'assets', 'menu');
let n = 0;
for (const line of dom.split('\n')) {
  const m = line.match(/((?:o|f)-[a-z-]+) data:image\/png;base64,([A-Za-z0-9+/=]+)/);
  if (!m) continue;
  fs.writeFileSync(path.join(out, m[1] + '.png'), Buffer.from(m[2], 'base64'));
  n++;
}
console.log('icons written:', n);

// Makes the raven tree window's pictures (assets/book/tree_*.png) from tools/tree_art.html with headless Chrome.
const { execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const page = 'file:///' + path.resolve(__dirname, 'tree_art.html').replace(/\\/g, '/');
const dom = execFileSync(chrome, ['--headless=new', '--disable-gpu', '--virtual-time-budget=10000', '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
const dir = path.resolve(__dirname, '..', 'assets', 'book');
let n = 0;
for (const line of dom.split('\n')) {
  const m = line.match(/(tree_[a-z_]+) data:image\/png;base64,([A-Za-z0-9+/=]+)/);
  if (!m) continue;
  fs.writeFileSync(path.join(dir, m[1] + '.png'), Buffer.from(m[2], 'base64'));
  n++;
}
console.log('pictures written:', n);

// Makes the paired gifts' leaf assets/book/pair_leaf.png from tools/pair_leaf.html with headless Chrome.
const { execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const page = 'file:///' + path.resolve(__dirname, 'pair_leaf.html').replace(/\\/g, '/');
const dom = execFileSync(chrome, ['--headless=new', '--disable-gpu', '--allow-file-access-from-files', '--virtual-time-budget=5000', '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
const m = dom.match(/leaf data:image\/png;base64,([A-Za-z0-9+/=]+)/);
if (!m) { console.error('no picture made'); process.exit(1); }
fs.writeFileSync(path.resolve(__dirname, '..', 'assets', 'book', 'pair_leaf.png'), Buffer.from(m[1], 'base64'));
console.log('assets/book/pair_leaf.png written');

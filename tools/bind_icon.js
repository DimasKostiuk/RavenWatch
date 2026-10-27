// Makes the в'язь's medallion assets/icons/bind.png from tools/bind_icon.html with headless Chrome.
const { execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const page = 'file:///' + path.resolve(__dirname, 'bind_icon.html').replace(/\\/g, '/');
const dom = execFileSync(chrome, ['--headless=new', '--disable-gpu', '--allow-file-access-from-files', '--virtual-time-budget=5000', '--dump-dom', page], { encoding: 'utf8', maxBuffer: 64 << 20 });
const m = dom.match(/bind data:image\/png;base64,([A-Za-z0-9+/=]+)/);
if (!m) { console.error('no picture made'); process.exit(1); }
fs.writeFileSync(path.resolve(__dirname, '..', 'assets', 'icons', 'bind.png'), Buffer.from(m[1], 'base64'));
console.log('assets/icons/bind.png written');

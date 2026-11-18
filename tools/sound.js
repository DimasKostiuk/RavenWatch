#!/usr/bin/env node
// A raw sound file made ready for the mod: node tools/sound.js info|add|list … — the work and every word of it: the header of
// tools/sound.py. This file only finds a Python that has numpy, scipy, soundfile and av (the one of voice-proto/pytools; else
// the «python» of the PATH) and hands the words on.
const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const own = path.join(root, 'voice-proto', 'pytools', 'py', 'python.exe');
const python = fs.existsSync(own) ? own : 'python';
const r = spawnSync(python, ['-X', 'utf8', '-B', path.join(__dirname, 'sound.py'), ...process.argv.slice(2)], { stdio: 'inherit', env: { ...process.env, PYTHONIOENCODING: 'utf-8' } });
if (r.error) { console.log('sound: no Python to run it (' + r.error.message + '): it needs numpy, scipy, soundfile, av — voice-proto/pytools/py had them'); process.exit(1); }
process.exit(r.status === null ? 1 : r.status);

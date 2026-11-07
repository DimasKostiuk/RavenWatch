// RavenWatch clips edited by words, not by hand-written scripts (the user 2026-11-04: «it takes too long»; the scripts broke on
// quotes and «\n» again and again). Each change is written to assets/story/clips: the one copy (the game on the author's machine
// reads it there, the stage plays it at once, the build ships it — BodyClips.Dir, 2026-11-06), then tools/clipcheck.js is run.
//   node tools/clip.js show <clip> [channel]                       its keys, a channel a line (or one channel's)
//   node tools/clip.js set <clip> <channel> <t> <x> [y z]           a key put (or replaced) at t
//   node tools/clip.js del <clip> <channel> <t>                     the key at t taken out
//   node tools/clip.js move <clip> <channel> <from> <to> <dx> [dy dz]   every key of it in [from, to] moved by that much
//   node tools/clip.js stretch <clip> <from> <to> <seconds>         the span [from, to] made that long: its keys spread, all
//                                                                  later keys (every channel), parts, marks and the length go on
//   node tools/clip.js copy <to> <from> <t0> <t1> <at> [ch,ch…]     the keys of a checked clip in [t0, t1] put into another at «at»
//                                                                  (its moves reused, not made again: a kneel, a step back)
//   node tools/clip.js header <clip> <line>                         a header line set («length 6», «fade 0.6 0.8», «part loop 1 4 loop», «mark 3.4 unlocked», «say …», «touch wall 0.55»); «want …» adds one
//   node tools/clip.js unwant <clip> <words>                        the «want» lines with those words taken out
//   node tools/clip.js unline <clip> <start of the line>            a head line taken out («part lowers», a note)
//   node tools/clip.js batch <clip> "<op>; <op>; …"                 several of the above (set, del, move, header, unwant, unline — without the clip's
//                                                                  name) made together, checked once: a rework passes through states that
//                                                                  are wrong on the way (a hand's weight moved before the prop's), 2026-11-07
// A change after which clipcheck says something new, about any clip or scene, or falls over, is not kept: the file goes back, exit code 1.
const fs = require('fs'), path = require('path'), cp = require('child_process');
const root = path.join(__dirname, '..');
const assets = path.join(root, 'assets', 'story', 'clips');
const n = s => { const v = parseFloat(s); if (isNaN(v)) throw new Error('not a number: ' + s); return v; };
const f = v => (Math.round(v * 1000) / 1000).toString();
const three = v => { const o = v.slice(0, 3); while (o.length < 3) o.push(0); return o; };

function load(name) {
  const file = path.join(assets, name + '.txt');
  if (!fs.existsSync(file)) throw new Error('no clip ' + name);
  const head = [], keys = {}, order = [];
  // (what is not said of a key's three numbers is zero: a line with fewer is one the game does not read)
  for (const raw of fs.readFileSync(file, 'utf8').replace(/\r/g, '').split('\n')) {
    const line = raw.trim();
    const m = line.match(/^(-?[0-9.]+)\s+(\S+)\s+(.*)$/);
    if (m && !line.startsWith('#')) {
      const ch = m[2];
      if (!keys[ch]) { keys[ch] = []; order.push(ch); }
      keys[ch].push({ t: n(m[1]), v: three(m[3].trim().split(/\s+/).map(n)) });
    } else if (order.length === 0 || line.length) head.push(raw); // (the head, and a note among the keys: kept with the head)
  }
  return { name, head, keys, order };
}

function save(c) {
  for (const ch of c.order) c.keys[ch].sort((a, b) => a.t - b.t);
  const out = [];
  for (const h of c.head) out.push(h);
  while (out.length && out[out.length - 1].trim() === '') out.pop();
  out.push('');
  for (const ch of c.order) { if (!c.keys[ch].length) continue; for (const k of c.keys[ch]) out.push(f(k.t) + ' ' + ch + ' ' + k.v.map(f).join(' ')); out.push(''); }
  const text = out.join('\n');
  const file = path.join(assets, c.name + '.txt'), was = fs.existsSync(file) ? fs.readFileSync(file) : null;
  const problems = () => { const r = cp.spawnSync('node', [path.join(root, 'tools', 'clipcheck.js')], { encoding: 'utf8' }); return { failed: r.status !== 0, text: (r.stdout || '') + (r.stderr || ''), lines: new Set(((r.stdout || '') + (r.stderr || '')).split('\n').filter(l => l.startsWith('CLIP '))) }; };
  const before = problems().lines; // (what was wrong already is not this change's — another clip's, an old one)
  fs.writeFileSync(file, text);
  const after = problems();
  process.stdout.write(after.text);
  const added = [...after.lines].filter(l => !before.has(l)); // (anywhere: a scene that names a part or a mark this change took away too — 2026-11-06 review)
  if (added.length || (after.failed && after.lines.size === 0)) {
    if (was !== null) fs.writeFileSync(file, was); else fs.unlinkSync(file);
    console.error('clip: not kept, ' + c.name + ' is as it was — it made new problems:\n  ' + added.join('\n  '));
    process.exit(1);
  }
}

function headerValue(c, word) { for (const h of c.head) { const w = h.trim().split(/\s+/); if (w[0] === word) return w.slice(1); } return null; }
function setHeader(c, line) {
  const w = line.trim().split(/\s+/);
  const same = w[0] === 'part' || w[0] === 'mode' ? h => h.trim().split(/\s+/)[0] === w[0] && h.trim().split(/\s+/)[1] === w[1]
    : w[0] === 'mark' ? h => h.trim().split(/\s+/)[0] === 'mark' && h.trim().split(/\s+/)[2] === w[2] // (a mark is its name: its time is what changes)
    : h => h.trim().split(/\s+/)[0] === w[0];
  const i = w[0] === 'want' ? -1 : c.head.findIndex(same);
  if (i >= 0) c.head[i] = line; else { let at = c.head.findIndex(h => h.trim().startsWith('#') || h.trim() === ''); if (at < 0) at = c.head.length; c.head.splice(at, 0, line); }
}

const [cmd, name, ...a] = process.argv.slice(2);
try {
  if (!cmd || !name) throw new Error('node tools/clip.js find <words> | show|set|del|move|stretch|copy|header|unwant <clip> …');
  if (cmd === 'find') { // (the catalog's rows with every word: a pose like the one wanted, before making one — docs/CLIPS.md)
    const words = [name, ...a].map(w => w.toLowerCase());
    const rows = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').filter(l => l.startsWith('| `'));
    const hit = rows.filter(l => words.every(w => l.toLowerCase().includes(w)));
    for (const l of (hit.length ? hit : rows.filter(l => words.some(w => l.toLowerCase().includes(w))))) console.log(l.replace(/\s*\|\s*/g, ' | ').trim());
    if (!hit.length) console.log('(no row has all the words; rows with any of them above)');
    process.exit(0);
  }
  const c = load(name);
  if (cmd === 'batch') {
    const ops = a.join(' ').split(';').map(s => s.trim()).filter(Boolean);
    for (const op of ops) { const w = op.split(/\s+/); if (['show', 'copy', 'stretch', 'batch'].includes(w[0])) throw new Error('batch: «' + w[0] + '» goes alone'); apply(c, w[0], w.slice(1)); }
    console.log(ops.length + ' change(s) together'); save(c);
  } else if (cmd === 'show' || cmd === 'stretch' || cmd === 'copy') apply(c, cmd, a);
  else { apply(c, cmd, a); save(c); }
} catch (e) { console.error('clip: ' + e.message); process.exit(1); }

function apply(c, cmd, a) {
  switch (cmd) {
    case 'show':
      for (const ch of c.order) if (!a[0] || ch === a[0]) console.log(ch.padEnd(20) + c.keys[ch].map(k => f(k.t) + ':' + k.v.map(f).join(',')).join('  '));
      for (const h of c.head) if (/^(length|part|mark|fade|say|want|touch)\b/.test(h.trim())) console.log('  ' + h.trim());
      break;
    case 'set': {
      const ch = a[0], t = n(a[1]), v = three(a.slice(2).map(n));
      if (!c.keys[ch]) { c.keys[ch] = []; c.order.push(ch); }
      const k = c.keys[ch].find(k => Math.abs(k.t - t) < 0.0005);
      if (k) k.v = v; else c.keys[ch].push({ t, v });
      break;
    }
    case 'del': { const ch = a[0], t = n(a[1]); c.keys[ch] = (c.keys[ch] || []).filter(k => Math.abs(k.t - t) >= 0.0005); break; }
    case 'move': {
      const ch = a[0], t0 = n(a[1]), t1 = n(a[2]), d = a.slice(3).map(n);
      let moved = 0;
      for (const k of c.keys[ch] || []) if (k.t >= t0 - 1e-4 && k.t <= t1 + 1e-4) { k.v = k.v.map((x, i) => x + (d[i] || 0)); moved++; }
      console.log('moved ' + moved + ' key(s)'); break;
    }
    case 'stretch': {
      const t0 = n(a[0]), t1 = n(a[1]), len = n(a[2]), add = len - (t1 - t0);
      const map = t => t <= t0 ? t : t >= t1 ? t + add : t0 + (t - t0) * len / (t1 - t0);
      for (const ch of c.order) for (const k of c.keys[ch]) k.t = map(k.t);
      c.head = c.head.map(h => {
        const w = h.trim().split(/\s+/);
        if (w[0] === 'length') return 'length ' + f(n(w[1]) + add);
        if (w[0] === 'part') return 'part ' + w[1] + ' ' + f(map(n(w[2]))) + ' ' + f(map(n(w[3]))) + ' ' + w.slice(4).join(' ');
        if (w[0] === 'mark' || w[0] === 'event') return w[0] + ' ' + f(map(n(w[1]))) + ' ' + w.slice(2).join(' ');
        if (w[0] === 'want') { const d = w[1].indexOf('-'); return 'want ' + (d > 0 ? f(map(n(w[1].slice(0, d)))) + '-' + f(map(n(w[1].slice(d + 1)))) : f(map(n(w[1])))) + ' ' + w.slice(2).join(' '); }
        return h;
      });
      console.log('span ' + f(t0) + '-' + f(t1) + ' now ' + f(len) + ' s; the clip ' + (add >= 0 ? '+' : '') + f(add) + ' s'); save(c); break;
    }
    case 'copy': {
      const src = load(a[0]), t0 = n(a[1]), t1 = n(a[2]), at = n(a[3]), only = a[4] ? a[4].split(',') : null;
      let put = 0;
      for (const ch of src.order) {
        if (only && only.indexOf(ch) < 0) continue;
        if (!c.keys[ch]) { c.keys[ch] = []; c.order.push(ch); }
        c.keys[ch] = c.keys[ch].filter(k => k.t < at - 1e-4 || k.t > at + (t1 - t0) + 1e-4);
        for (const k of src.keys[ch]) if (k.t >= t0 - 1e-4 && k.t <= t1 + 1e-4) { c.keys[ch].push({ t: at + k.t - t0, v: k.v.slice() }); put++; }
      }
      { const end = Math.max(...c.order.map(ch => Math.max(0, ...c.keys[ch].map(k => k.t)))); const l = c.head.findIndex(h => /^length\s/.test(h.trim())); if (l >= 0 && end > n(c.head[l].trim().split(/\s+/)[1])) { c.head[l] = 'length ' + f(end); console.log('length now ' + f(end)); } } // (its keys past the end: the clip goes on)
      console.log('copied ' + put + ' key(s) from ' + a[0]); save(c); break;
    }
    case 'header': setHeader(c, a.join(' ')); break;
    case 'unline': { const start = a.join(' '); const had = c.head.length; c.head = c.head.filter(h => !h.trim().startsWith(start)); if (c.head.length === had) throw new Error('no head line starts: ' + start); break; }
    case 'unwant': { const words = a.join(' '); c.head = c.head.filter(h => !(h.trim().startsWith('want ') && h.includes(words))); break; }
    default: throw new Error('unknown: ' + cmd);
  }
}

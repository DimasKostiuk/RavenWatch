// RavenWatch clips edited by words, not by hand-written scripts (the user 2026-11-04: «it takes too long»; the scripts broke on
// quotes and «\n» again and again). The clip half of tools/anim.js (the one entry, review 2 2026-11-10): anim.js <verb> <clip> …
// Each change is written to assets/story/clips: the one copy (the game on the author's machine reads it there, the stage plays it
// at once, the build ships it — BodyClips.Dir, 2026-11-06), then tools/clipcheck.js is run.
//   new <clip> [--body heid|player|any] [--length 4] [--prop <item>] "<say>"
//                                                               a new clip's frame (newClip): head, say, the game's stance at 0 and at the end, the look
//                                                               ahead, a draft row in docs/CLIPS.md — it passes clipcheck as it is. The row's body and
//                                                               thing («реквізит `<item>`», a prefab: RavenWatch_item_…) are what a take puts on the
//                                                               stage (take.js reads the row): a clip with a thing and none in its row is taken without it
//   do <clip> "<t0>-<t1> <movement> …; …"                       movements added to the clip's script (tools/motion.js, its head says each: reach, rest, look,
//                                                               nod, shake, lean, turn, sink, weight, step, sigh, fingers): the keys are made from the
//                                                               script again, the rest of the body with them — a few words, not eighty keys
//   undo <clip> <n|all> · redo <clip> <n> <movement>            a script line taken out, or said anew (its number: «show»)
//   alive <clip> [0..2]                                         how much the rest of the body goes with the hands (1 by default; 0: none); with no
//                                                               script too: a clip keyed by hand gets its trunk and weight from its hands
//   auto <clip> <channel…>                                      channels keyed by hand given back to the script (their keys go)
//   mirror <to> <from>                                          the same clip for the other side, a new one (a thing or a bone in its own axes: refused)
//   check <clip>                                                what the keys alone say before a take (also after every change of it)
//   find <words>                                                the catalog's rows with every word (docs/CLIPS.md): a pose like the one wanted
//   show <clip> [channel]                                       its keys, a channel a line (or one channel's)
//   at <clip> <t>                                               every channel's value at that moment (straight between its keys; the game eases), the wants then
//   set <clip> <channel> <t> <x> [y z]                          a key put (or replaced) at t
//   del <clip> <channel> <t>                                    the key at t taken out
//   move <clip> <channel> <from> <to> <dx> [dy dz]              every key of it in [from, to] moved by that much
//   stretch <clip> <from> <to> <seconds>                        the span [from, to] made that long: its keys spread, all later keys (every channel),
//                                                               parts, marks and the length go on
//   shift <clip> <from> <to|end> <dt>                           every key of every channel in [from, to] moved dt s in time (parts, marks, wants in it too;
//                                                               the length grows if a key goes past it); a moved key landing on one that stays replaces it
//   hold <clip> <channel> <t0> <t1>                             the channel's value at t0 held till t1: a key at t1 with it, the keys between taken out
//   copy <to> <from> <t0> <t1> <at> [ch,ch…]                    the keys of a checked clip in [t0, t1] put into another at «at» (its moves reused: a kneel, a step back)
//   header <clip> <line>                                        a header line set («length 6», «fade 0.6 0.8», «part loop 1 4 loop», «mark 3.4 unlocked», «say …», «touch wall 0.55»); «want …» adds one
//   unwant <clip> <words>                                       the «want» lines with those words taken out
//   unline <clip> <start of the line>                           a head line taken out («part lowers», a note)
//   batch <clip> "<op>; <op>; …"                                several of the above (set, del, move, hold, header, unwant, unline — without the clip's name) made
//                                                               together, checked once: a rework passes through states that are wrong on the way, 2026-11-07
//   diff <clip>                                                 the file against its last take on disk (config/RavenWatch/player_anims_takes/<clip>/, the stage keeps one a
//                                                               save), key by key and head line by line: what the last change did
// A change after which clipcheck says something new, about any clip or scene, or falls over, is not kept: the file goes back, exit code 1.
const fs = require('fs'), path = require('path'), cp = require('child_process');
const motion = require('./motion.js');
const root = path.join(__dirname, '..');
const assets = path.join(root, 'assets', 'story', 'clips');
const n = s => { const v = parseFloat(s); if (isNaN(v)) throw new Error('not a number: ' + s); return v; };
const f = v => (Math.round(v * 1000) / 1000).toString();
const three = v => { const o = v.slice(0, 3); while (o.length < 3) o.push(0); return o; };
const STANCE = { '@LeftFoot': motion.STANCE.Left, '@RightFoot': motion.STANCE.Right }; // (the game's stance, the feet in the body's frame: docs/ANIMATION.md «Числа»)

function parse(text, name) {
  const head = [], keys = {}, order = [];
  // (what is not said of a key's three numbers is zero: a line with fewer is one the game does not read)
  for (const raw of text.replace(/\r/g, '').split('\n')) {
    const line = raw.trim();
    const m = line.match(/^(-?[0-9.]+)\s+(\S+)\s+(.*)$/);
    if (m && !line.startsWith('#')) {
      const ch = m[2];
      if (!keys[ch]) { keys[ch] = []; order.push(ch); }
      keys[ch].push({ t: n(m[1]), v: three(m[3].trim().split(/\s+/).map(n)) });
    } else if (order.length === 0 || line.length) head.push(raw); // (the head, and a note among the keys: kept with the head)
  }
  for (const ch of order) keys[ch].sort((a, b) => a.t - b.t);
  return { name, head, keys, order };
}

function load(name) {
  const file = path.join(assets, name + '.txt');
  if (!fs.existsSync(file)) throw new Error('no clip ' + name);
  return parse(fs.readFileSync(file, 'utf8'), name);
}

function text(c) {
  for (const ch of c.order) c.keys[ch].sort((a, b) => a.t - b.t);
  const out = [];
  for (const h of c.head) out.push(h);
  while (out.length && out[out.length - 1].trim() === '') out.pop();
  out.push('');
  for (const ch of c.order) { if (!c.keys[ch].length) continue; for (const k of c.keys[ch]) out.push(f(k.t) + ' ' + ch + ' ' + k.v.map(f).join(' ')); out.push(''); }
  return out.join('\n');
}

function save(c) {
  const file = path.join(assets, c.name + '.txt'), was = fs.existsSync(file) ? fs.readFileSync(file) : null;
  const problems = () => { const r = cp.spawnSync('node', [path.join(root, 'tools', 'clipcheck.js')], { encoding: 'utf8' }); return { failed: r.status !== 0, text: (r.stdout || '') + (r.stderr || ''), lines: new Set(((r.stdout || '') + (r.stderr || '')).split('\n').filter(l => l.startsWith('CLIP '))) }; };
  const before = problems().lines; // (what was wrong already is not this change's — another clip's, an old one)
  fs.writeFileSync(file, text(c));
  const after = problems();
  process.stdout.write(after.text);
  const added = [...after.lines].filter(l => !before.has(l)); // (anywhere: a scene that names a part or a mark this change took away too — 2026-11-06 review)
  if (added.length || (after.failed && after.lines.size === 0)) {
    if (was !== null) fs.writeFileSync(file, was); else fs.unlinkSync(file);
    throw new Error('not kept, ' + c.name + ' is as it was — ' + (added.length ? 'it made new problems:\n  ' + added.join('\n  ') : 'clipcheck fell over (its words above)'));
  }
  said(c);
}
// what the keys alone say before a take (motion.predict): a take is not spent on it
function said(c) { const p = motion.predict(c); if (p.length) console.log('before a take, from the keys alone (the take measures the body):\n  ' + p.join('\n  ')); }
// a channel the author keys by hand is his from then on: the script does not make it again
// … and with it the channels that only mean something together (the review 2026-11-11: one key set on a scripted @RightHand, and the script made its weight,
// fingers and palm no more — every later change was refused «@RightHand has no @RightHand.w»; one key on Spine1 and Spine2's share of the lean was gone)
const family = ch => { const m = /^(@(?:Left|Right)Hand)(\.w|\.p|\.f|\.n)?$/.exec(ch); if (m) return m[2] === '.p' ? [ch] : [m[1], m[1] + '.w', m[1] + '.f', m[1] + '.n']; /* (the elbow's way is its own: keyed by hand, the hand stays the script's) */ if (/^@Look(\.w)?$/.test(ch)) return ['@Look', '@Look.w']; if (ch === 'Spine1' || ch === 'Spine2') return ['Spine1', 'Spine2']; return [ch]; };
function mineNow(c, chs) {
  const list = motion.listOf(c, 'scripted'), took = [...new Set(chs.flatMap(family))].filter(ch => list.includes(ch));
  if (!took.length) return;
  const left = list.filter(ch => !took.includes(ch));
  motion.setLine(c, '# scripted:', left.length ? '# scripted: ' + left.join(' ') : null);
  console.log(took.join(', ') + ': keyed by hand now, as ' + (took.length > 1 ? 'they are (they go together)' : 'it is') + ' — the script\'s movements leave ' + (took.length > 1 ? 'them' : 'it') + ' (node tools/anim.js auto ' + c.name + ' ' + took[0] + ' gives back: the keys set by hand go)');
}
// the script made into keys again; its notes said
function remake(c) { const r = motion.regen(c); for (const x of r.notes) console.log('note: ' + x); }

function headerValue(c, word) { for (const h of c.head) { const w = h.trim().split(/\s+/); if (w[0] === word) return w.slice(1); } return null; }
function setHeader(c, line) {
  const w = line.trim().split(/\s+/);
  const same = w[0] === 'part' || w[0] === 'mode' ? h => h.trim().split(/\s+/)[0] === w[0] && h.trim().split(/\s+/)[1] === w[1]
    : w[0] === 'mark' ? h => h.trim().split(/\s+/)[0] === 'mark' && h.trim().split(/\s+/)[2] === w[2] // (a mark is its name: its time is what changes)
    : h => h.trim().split(/\s+/)[0] === w[0];
  const i = w[0] === 'want' ? -1 : c.head.findIndex(same);
  if (i >= 0) c.head[i] = line; else { let at = c.head.findIndex(h => h.trim().startsWith('#') || h.trim() === ''); if (at < 0) at = c.head.length; c.head.splice(at, 0, line); }
}

// a channel's value at t: straight between the two keys round it (the game eases: near, not exact); null — no keys
function valueAt(c, ch, t) {
  const ks = c.keys[ch] || [];
  if (!ks.length) return null;
  if (t <= ks[0].t) return { v: ks[0].v, how: t === ks[0].t ? 'key' : 'before its first key ' + f(ks[0].t) };
  const last = ks[ks.length - 1];
  if (t >= last.t) return { v: last.v, how: t === last.t ? 'key' : 'after its last key ' + f(last.t) };
  for (let i = 0; i + 1 < ks.length; i++) {
    if (Math.abs(ks[i].t - t) < 0.0005) return { v: ks[i].v, how: 'key' };
    if (ks[i].t < t && t < ks[i + 1].t) { const k = (t - ks[i].t) / (ks[i + 1].t - ks[i].t); return { v: ks[i].v.map((x, j) => x + (ks[i + 1].v[j] - x) * k), how: 'between ' + f(ks[i].t) + ' and ' + f(ks[i + 1].t) }; }
  }
  return { v: last.v, how: 'key' };
}

// the head's time words moved by a map t → t' (stretch, shift)
function mapHead(c, map, add) {
  c.head = c.head.map(h => {
    const w = h.trim().split(/\s+/);
    if (w[0] === 'length') return 'length ' + f(n(w[1]) + add);
    if (w[0] === 'part') return 'part ' + w[1] + ' ' + f(map(n(w[2]))) + ' ' + f(map(n(w[3]))) + ' ' + w.slice(4).join(' ');
    if (w[0] === 'mark' || w[0] === 'event') return w[0] + ' ' + f(map(n(w[1]))) + ' ' + w.slice(2).join(' ');
    if (w[0] === '#' && w[1] === 'do' && w[2]) { const d = w[2].indexOf('-'); return '# do ' + f(map(n(w[2].slice(0, d)))) + '-' + f(map(n(w[2].slice(d + 1)))) + ' ' + w.slice(3).join(' '); }
    if (w[0] === 'want') { const d = w[1].indexOf('-'); return 'want ' + (d > 0 ? f(map(n(w[1].slice(0, d)))) + '-' + f(map(n(w[1].slice(d + 1)))) : f(map(n(w[1])))) + ' ' + w.slice(2).join(' '); }
    return h;
  });
}

// a new clip's frame (Г11): the game's stance at 0 and at the end, the look ahead, the user's words; a draft row in the catalog first (clipcheck wants it)
function newClip(name, a) {
  if (!/^[a-z][a-z0-9_]*$/.test(name || '')) throw new Error('new <clip>: a name of small letters, digits and _');
  const file = path.join(assets, name + '.txt');
  if (fs.existsSync(file)) throw new Error('clip ' + name + ' exists: edit it, or pick another name');
  let body = 'any', length = 4, prop = null; const words = [];
  for (let i = 0; i < a.length; i++) { if (a[i] === '--body') body = a[++i]; else if (a[i] === '--length') length = n(a[++i]); else if (a[i] === '--prop') prop = a[++i]; else words.push(a[i]); }
  if (prop !== null && !/^\w+$/.test(prop || '')) throw new Error('--prop <item>: the prefab of the thing (RavenWatch_item_…): letters, digits and _');
  const say = words.join(' ').trim();
  if (!say) throw new Error('new <clip> [--body heid|player|any] [--length s] "<say>": the user\'s words, what the clip is measured by (docs/jobs/clip.md)');
  if (!['heid', 'player', 'any'].includes(body)) throw new Error('--body heid|player|any (the catalog\'s body: the stage plays it on that one)');
  if (!(length > 0.5)) throw new Error('--length: seconds, more than 0.5');
  const L = f(length);
  const head = ['clip ' + name, 'length ' + L, 'loop 0', 'pivot 1.1', 'curve ease', 'fade 0.4 0.6', 'mode Spine1 body', 'mode Spine2 body', 'say ' + say, '',
    '# a new clip\'s frame (anim.js new): the game\'s stance at 0 and at ' + L + ' (the feet below), the look ahead, nothing else yet.',
    '# Add «want <t> …» lines as the user speaks (docs/jobs/clip.md), then its movements: node tools/anim.js do ' + name + ' "<t0>-<t1> reach right <x y z>; …".',
    '# scripted: @LeftFoot @RightFoot @Look @Look.w'];
  const keys = {}, order = [];
  for (const ch of Object.keys(STANCE)) { keys[ch] = [{ t: 0, v: STANCE[ch] }, { t: length, v: STANCE[ch] }]; order.push(ch); }
  keys['@Look'] = [{ t: 0, v: [0, 1.6, 2] }]; order.push('@Look');
  keys['@Look.w'] = [{ t: 0, v: [0, 0, 0] }]; order.push('@Look.w');
  const cat = path.join(root, 'docs', 'CLIPS.md'), was = fs.readFileSync(cat, 'utf8');
  const lines = was.split('\n');
  let at = lines.findIndex(l => l.startsWith('## Сюжетні кліпи'));
  at = at < 0 ? -1 : lines.findIndex((l, i) => i > at && l.startsWith('|---'));
  if (at < 0) throw new Error('docs/CLIPS.md: no table under «## Сюжетні кліпи» to put the row in');
  const row = '| `' + name + '` | ' + say + ' | ' + (body === 'heid' ? 'Гейд' : body === 'player' ? 'гравець' : 'будь-хто (мешканці)') + ' | стійка → стійка | — (' + L + ' с) | ' + (prop ? 'реквізит `' + prop + '`' : '—') + ' | ніде ще | чернетка ' + new Date().toISOString().slice(0, 10) + ': каркас, ключів нема |';
  lines.splice(at + 1, 0, row);
  fs.writeFileSync(cat, lines.join('\n'));
  try { save({ name, head, keys, order }); }
  catch (e) { fs.writeFileSync(cat, was); throw e; }
  console.log('new clip assets/story/clips/' + name + '.txt (' + L + ' s, body ' + body + (prop ? ', thing ' + prop : '') + ') and its draft row in docs/CLIPS.md; next: want lines, then its movements (node tools/anim.js do ' + name + ' "<t0>-<t1> reach right <x y z>; …")');
}

// the same clip for the other side (motion.mirror): a new clip, its draft row in the catalog beside its source's
function mirrorClip(to, from) {
  if (!/^[a-z][a-z0-9_]*$/.test(to || '') || !from) throw new Error('mirror <to> <from>: the new clip\'s name (small letters, digits, _), then the clip it mirrors');
  if (fs.existsSync(path.join(assets, to + '.txt'))) throw new Error('clip ' + to + ' exists: pick another name');
  const c = motion.mirror(load(from), to);
  if (motion.scriptOf(c).length) remake(c);
  const cat = path.join(root, 'docs', 'CLIPS.md'), was = fs.readFileSync(cat, 'utf8'), lines = was.split('\n');
  const at = lines.findIndex(l => l.startsWith('| `' + from + '` |'));
  if (at < 0) throw new Error('docs/CLIPS.md: no row of ' + from + ' to put the mirrored one after');
  const cells = lines[at].split('|').map(x => x.trim()); // ('', name, shows, body, from → to, parts, touches, used, state, '')
  cells[1] = '`' + to + '`'; cells[2] += ' (дзеркально до `' + from + '`)'; cells[cells.length - 3] = 'ніде ще'; cells[cells.length - 2] = 'чернетка ' + new Date().toISOString().slice(0, 10) + ': дзеркало `' + from + '`, у грі не бачено';
  lines.splice(at + 1, 0, '| ' + cells.slice(1, -1).join(' | ') + ' |');
  fs.writeFileSync(cat, lines.join('\n'));
  try { save(c); } catch (e) { fs.writeFileSync(cat, was); throw e; }
  console.log('new clip assets/story/clips/' + to + '.txt: ' + from + ' for the other side, and its draft row in docs/CLIPS.md');
}

// the takes the stage keeps (one a save): the last one that is not the file as it is now
function lastTake(name) {
  const dir = path.join(require('./take.js').studioDir(), '..', 'player_anims_takes', name);
  if (!fs.existsSync(dir)) return null;
  const now = fs.readFileSync(path.join(assets, name + '.txt'), 'utf8').replace(/\r/g, '');
  for (const file of fs.readdirSync(dir).filter(x => x.endsWith('.txt')).sort().reverse()) {
    const t = fs.readFileSync(path.join(dir, file), 'utf8').replace(/\r/g, '');
    if (t !== now) return { file: path.join(dir, file), text: t };
  }
  return null;
}

function diff(name) {
  const now = load(name), take = lastTake(name);
  if (!take) { console.log(name + ': no earlier take on disk differs from the file (the stage keeps one a save, with the game open)'); return; }
  const was = parse(take.text, name);
  console.log(name + ': the file against ' + take.file + ' (' + fs.statSync(take.file).mtime.toLocaleString('uk-UA') + ')');
  let said = 0;
  for (const h of was.head) if (!now.head.includes(h) && h.trim()) { console.log('  head gone:  ' + h.trim()); said++; }
  for (const h of now.head) if (!was.head.includes(h) && h.trim()) { console.log('  head new:   ' + h.trim()); said++; }
  for (const ch of [...new Set([...was.order, ...now.order])]) {
    const a = was.keys[ch] || [], b = now.keys[ch] || [];
    const at = (ks, t) => ks.find(k => Math.abs(k.t - t) < 0.0005);
    for (const k of a) { const o = at(b, k.t); if (!o) { console.log('  ' + ch.padEnd(20) + f(k.t) + ': key gone (' + k.v.map(f).join(' ') + ')'); said++; } else if (o.v.some((x, i) => Math.abs(x - k.v[i]) > 0.0005)) { console.log('  ' + ch.padEnd(20) + f(k.t) + ': ' + k.v.map(f).join(' ') + ' → ' + o.v.map(f).join(' ')); said++; } }
    for (const k of b) if (!at(a, k.t)) { console.log('  ' + ch.padEnd(20) + f(k.t) + ': new key ' + k.v.map(f).join(' ')); said++; }
  }
  if (!said) console.log('  (only notes or spacing differ)');
}

function apply(c, cmd, a) {
  switch (cmd) {
    case 'show': {
      const script = motion.scriptOf(c), made = motion.listOf(c, 'scripted');
      const one = a[0] && a[0] !== 'all';
      if (one && !c.keys[a[0]]) throw new Error('show: no channel ' + a[0] + ' in ' + c.name + ' (it has: ' + c.order.join(' ') + ')');
      if (!one) script.forEach((l, i) => { const w = l.split(/\s+/), side = w[2] === 'left' ? ['Left'] : w[2] === 'right' ? ['Right'] : ['Left', 'Right'], dead = (w[1] === 'reach' || w[1] === 'rest') && side.every(s => c.keys['@' + s + 'Hand'] && !made.includes('@' + s + 'Hand')); console.log('do ' + (i + 1) + ': ' + l + (dead ? '   (not written: that hand is keyed by hand — auto ' + c.name + ' @' + side[0] + 'Hand gives it back)' : '')); });
      if (!one && (script.length || made.length)) console.log('alive ' + motion.aliveOf(c) + '; made by the script (their keys: show ' + c.name + ' <channel> | all): ' + (made.join(' ') || '—'));
      for (const ch of c.order) if (a[0] === 'all' || ch === a[0] || (!a[0] && !made.includes(ch))) console.log(ch.padEnd(20) + c.keys[ch].map(k => f(k.t) + ':' + k.v.map(f).join(',')).join('  '));
      if (one) break;
      for (const h of c.head) if (/^(length|part|mark|fade|say|want|touch)\b/.test(h.trim())) console.log('  ' + h.trim());
      break;
    }
    case 'at': {
      const t = n(a[0]);
      console.log(c.name + ' at ' + f(t) + ' s (straight between keys; the game eases):');
      for (const ch of c.order) { const r = valueAt(c, ch, t); if (r) console.log('  ' + ch.padEnd(20) + r.v.map(f).join(' ').padEnd(22) + (r.how === 'key' ? '(key)' : '(' + r.how + ')')); }
      for (const h of c.head) { const w = h.trim().split(/\s+/); if (w[0] !== 'want') continue; const d = w[1].indexOf('-'); const t0 = n(d > 0 ? w[1].slice(0, d) : w[1]), t1 = d > 0 ? n(w[1].slice(d + 1)) : t0; if (t0 - 0.0005 <= t && t <= t1 + 0.0005) console.log('  ' + h.trim()); }
      break;
    }
    case 'set': {
      const ch = a[0], t = n(a[1]), v = three(a.slice(2).map(n));
      mineNow(c, [ch]);
      if (!c.keys[ch]) { c.keys[ch] = []; c.order.push(ch); }
      const k = c.keys[ch].find(k => Math.abs(k.t - t) < 0.0005);
      if (k) k.v = v; else c.keys[ch].push({ t, v });
      break;
    }
    case 'del': { const ch = a[0], t = n(a[1]); mineNow(c, [ch]); c.keys[ch] = (c.keys[ch] || []).filter(k => Math.abs(k.t - t) >= 0.0005); break; }
    case 'move': {
      const ch = a[0], t0 = n(a[1]), t1 = n(a[2]), d = a.slice(3).map(n);
      mineNow(c, [ch]);
      let moved = 0;
      for (const k of c.keys[ch] || []) if (k.t >= t0 - 1e-4 && k.t <= t1 + 1e-4) { k.v = k.v.map((x, i) => x + (d[i] || 0)); moved++; }
      console.log('moved ' + moved + ' key(s)'); break;
    }
    case 'hold': { // the value at t0 held till t1: a key at t1 with it, the keys between gone
      const ch = a[0], t0 = n(a[1]), t1 = n(a[2]);
      if (!(t1 > t0)) throw new Error('hold: t1 after t0');
      const r = valueAt(c, ch, t0);
      if (!r) throw new Error('hold: no channel ' + ch);
      mineNow(c, [ch]);
      c.keys[ch] = c.keys[ch].filter(k => k.t <= t0 + 1e-4 || k.t >= t1 - 1e-4);
      for (const t of [t0, t1]) { const k = c.keys[ch].find(k => Math.abs(k.t - t) < 0.0005); if (k) k.v = r.v.slice(); else c.keys[ch].push({ t, v: r.v.slice() }); }
      console.log(ch + ' held ' + f(t0) + '-' + f(t1) + ' at ' + r.v.map(f).join(' ')); break;
    }
    case 'stretch': {
      const t0 = n(a[0]), t1 = n(a[1]), len = n(a[2]), add = len - (t1 - t0);
      if (!(t1 > t0) || len < 0) throw new Error('stretch <clip> <from> <to> <seconds>: «to» after «from», the seconds not below 0');
      const map = t => t <= t0 ? t : t >= t1 ? t + add : t0 + (t - t0) * len / (t1 - t0);
      for (const ch of c.order) for (const k of c.keys[ch]) k.t = map(k.t);
      mapHead(c, map, add);
      console.log('span ' + f(t0) + '-' + f(t1) + ' now ' + f(len) + ' s; the clip ' + (add >= 0 ? '+' : '') + f(add) + ' s');
      if (motion.scriptOf(c).length) remake(c); // (the script's times went with it: its keys made again from them)
      save(c); break;
    }
    case 'shift': { // every key in [from, to] moved dt in time; the head's times in it too; a moved key on a staying one replaces it
      const t0 = n(a[0]), t1 = a[1] === 'end' ? 1e9 : n(a[1]), dt = n(a[2]);
      const inside = t => t >= t0 - 1e-4 && t <= t1 + 1e-4, map = t => inside(t) ? t + dt : t;
      let moved = 0, replaced = 0;
      for (const ch of c.order) {
        const stay = c.keys[ch].filter(k => !inside(k.t)), go = c.keys[ch].filter(k => inside(k.t));
        for (const k of go) { k.t += dt; moved++; }
        c.keys[ch] = stay.filter(k => { const hit = go.some(g => Math.abs(g.t - k.t) < 0.0005); if (hit) replaced++; return !hit; }).concat(go);
        if (c.keys[ch].some(k => k.t < -1e-6)) throw new Error('shift: a key of ' + ch + ' would go before 0');
      }
      const length = headerValue(c, 'length'), end = Math.max(...c.order.map(ch => Math.max(0, ...c.keys[ch].map(k => k.t))));
      mapHead(c, map, length && end > n(length[0]) ? end - n(length[0]) : 0);
      console.log('shifted ' + moved + ' key(s) by ' + f(dt) + ' s' + (replaced ? ', ' + replaced + ' staying key(s) replaced' : ''));
      if (motion.scriptOf(c).length) remake(c);
      save(c); break;
    }
    case 'copy': {
      const src = load(a[0]), t0 = n(a[1]), t1 = n(a[2]), at = n(a[3]), only = a[4] ? a[4].split(',') : null;
      let put = 0;
      mineNow(c, src.order.filter(ch => !only || only.indexOf(ch) >= 0));
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

// a verb and its words (anim.js): throws on a mistake
function run(cmd, args) {
  const [name, ...a] = args;
  if (!name) throw new Error('node tools/anim.js ' + cmd + ' <clip> … (node tools/anim.js help keys says each)');
  if (cmd === 'find') { // (the catalog's rows with every word: a pose like the one wanted, before making one — docs/CLIPS.md)
    const words = [name, ...a].map(w => w.toLowerCase());
    const rows = fs.readFileSync(path.join(root, 'docs', 'CLIPS.md'), 'utf8').split('\n').filter(l => l.startsWith('| `'));
    const hit = rows.filter(l => words.every(w => l.toLowerCase().includes(w)));
    for (const l of (hit.length ? hit : rows.filter(l => words.some(w => l.toLowerCase().includes(w))))) console.log(l.replace(/\s*\|\s*/g, ' | ').trim());
    if (!hit.length) console.log('(no row has all the words; rows with any of them above)');
    return;
  }
  if (cmd === 'new') { newClip(name, a); return; }
  if (cmd === 'diff') { diff(name); return; }
  if (cmd === 'mirror') { mirrorClip(name, a[0]); return; }
  const c = load(name);
  if (cmd === 'check') { const p = motion.predict(c); console.log(p.length ? p.join('\n') : name + ': the keys alone say nothing against it (the take measures the body)'); return; }
  if (cmd === 'do' || cmd === 'undo' || cmd === 'redo' || cmd === 'alive' || cmd === 'auto') {
    const script = motion.scriptOf(c), length = motion.lengthOf(c);
    if (cmd === 'do') {
      const lines = a.join(' ').split(';').map(s => s.trim()).filter(Boolean);
      if (!lines.length) throw new Error('do <clip> "<t0>-<t1> <movement> …; …" — ' + motion.VERBS.join(', ') + ' (the head of tools/motion.js says each)');
      for (const l of lines) motion.parseLine(l, length);
      motion.setScript(c, script.concat(lines));
    } else if (cmd === 'undo') {
      if (a[0] === 'all') motion.setScript(c, []);
      else { const i = Math.round(n(a[0] || '')) - 1; if (!(i >= 0 && i < script.length)) throw new Error('undo <clip> <n|all>: the script has ' + script.length + ' line(s) (node tools/anim.js show ' + name + ')'); script.splice(i, 1); motion.setScript(c, script); }
    } else if (cmd === 'redo') {
      const i = Math.round(n(a[0] || '')) - 1, line = a.slice(1).join(' ').trim();
      if (!(i >= 0 && i < script.length) || !line) throw new Error('redo <clip> <n> <t0>-<t1> <movement> …: the script has ' + script.length + ' line(s)');
      motion.parseLine(line, length); script[i] = line; motion.setScript(c, script);
    } else if (cmd === 'alive') {
      const k = a[0] === undefined ? 1 : n(a[0]);
      if (!(k >= 0 && k <= 2)) throw new Error('alive <clip> [0..2]');
      motion.setLine(c, '# alive ', '# alive ' + f(k));
    } else {
      if (!a.length) throw new Error('auto <clip> <channel…>: the channels to give back to the script');
      for (const ch of a) if (!/^@(Left|Right)(Hand|Foot)(\.[wpfn])?$|^@Look(\.w)?$|^Spine[12]$|^move$|^(Left|Right)Hand(Index|Middle|Ring|Pinky)[123]$/.test(ch)) throw new Error('auto: ' + ch + ' is not a channel the script makes (hands, feet, @Look, Spine1/Spine2, move, fingers): its keys would only be lost');
      for (const ch of [...new Set(a.flatMap(family))]) { if (!c.keys[ch]) continue; delete c.keys[ch]; c.order = c.order.filter(x => x !== ch); }
    }
    remake(c);
    const now = motion.scriptOf(c); now.forEach((l, i) => console.log('do ' + (i + 1) + ': ' + l));
    console.log(c.name + ': ' + now.length + ' movement(s), alive ' + motion.aliveOf(c) + ', ' + c.order.reduce((s, ch) => s + c.keys[ch].length, 0) + ' key(s) on ' + c.order.length + ' channel(s)');
    save(c);
    return;
  }
  if (cmd === 'batch') {
    const ops = a.join(' ').split(';').map(s => s.trim()).filter(Boolean);
    for (const op of ops) { const w = op.split(/\s+/); if (['show', 'at', 'copy', 'stretch', 'shift', 'batch'].includes(w[0])) throw new Error('batch: «' + w[0] + '» goes alone'); apply(c, w[0], w.slice(1)); }
    console.log(ops.length + ' change(s) together'); save(c);
  } else if (cmd === 'show' || cmd === 'at' || cmd === 'stretch' || cmd === 'shift' || cmd === 'copy') apply(c, cmd, a);
  else { apply(c, cmd, a); if (cmd === 'header' && a[0] === 'length' && (motion.scriptOf(c).length || motion.listOf(c, 'scripted').length)) remake(c); save(c); }
}

module.exports = { run, load, parse, valueAt, family };
if (require.main === module) { console.error('clip.js is the clip half of the one tool: node tools/anim.js ' + (process.argv[2] || '<verb>') + ' …'); process.exit(1); }

// What a scene sounds like, from its file alone — the sound's part of the dry run (tools/scene_dry.js) and a first sound pass proposed for a
// scene (node tools/anim.js scene --sound <scene>), as «--cover» proposes its shots. The scenes were filmed and stayed mute: of 19 scenes
// 23 «sound» lines, 20 of them one raven's caw, no music, no bed, no mix (the review of 2026-11-13) — nothing showed the sound while a
// scene was written, so nothing asked for it.
// lane(warn): the dry run's sound lane. It walks the steps with the run and keeps what is held — the music (♪name; ♪— is a silence asked
//   for), the beds (~name), the mix (world 0.4), the mutes, the ear — for a column of the table; lists every one-shot with its moment, where
//   it is heard and how long it rings (our own files: their length; a game's sound: not known here); the sounds a clip carries («event» in
//   its file) at their moments. Its warnings:
//     «ear camera» with no shot on; a «sound … wait» on a game's sound (how long it waits, the file does not say);
//     a one-shot or its «delay» running past the end of the scene; a «music … once» shorter than what it is held for;
//     music begun so close to the end that its fade-in is most of it; a wordless voice sound at a woman who has none of it;
//     a filmed scene (bars or a shot) with not one word of sound.
// propose(id): lines to put in, each with the line it goes before; nothing is written. By the grammar of docs/jobs/scene.md «Звук сцени»:
//   the place's bed in with the first shot; the world down under a talk and back after it; the ear at the camera for a wide shot; a sound for
//   a thing that falls; the clips used that carry no sound (their marks are where a sound belongs); a long stretch with no line and no sound.
//   What it cannot propose: which music, and where the scene turns — that is the scene's point, said by its author.
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..');
const soundsDir = path.join(root, 'assets', 'story', 'sounds'), npcDir = path.join(root, 'assets', 'story', 'npc');
const EndFade = 3, WideSizes = ['wide'], TalkQuiet = 0.4, LongQuiet = 12; // Audio.EndFade · a shot the ear goes to the camera for · the world under a talk · s with no line and no sound
const f = v => (Math.round(v * 10) / 10).toString();

// an Ogg Vorbis file's length: its last page's sample count over its rate (null: not such a file)
function oggSeconds(file) {
  try {
    const b = fs.readFileSync(file);
    const id = b.indexOf(Buffer.from('\x01vorbis', 'latin1'));
    if (id < 0) return null;
    const rate = b.readUInt32LE(id + 12);
    const last = b.lastIndexOf(Buffer.from('OggS', 'latin1'));
    if (last < 0 || rate <= 0) return null;
    const n = Number(b.readBigUInt64LE(last + 6));
    return n > 0 ? n / rate : null;
  } catch (e) { return null; }
}

// the catalogue: name → { files, game, sec (the longest of its own files; null: a game's sound) }; the sounds a timbre has none of; the timbres
let _bank = null;
function bank() {
  if (_bank) return _bank;
  const names = new Map(), none = new Set(), timbres = new Map();
  if (fs.existsSync(soundsDir)) for (const file of fs.readdirSync(soundsDir).filter(x => x.endsWith('.txt')).sort()) {
    for (const raw of fs.readFileSync(path.join(soundsDir, file), 'utf8').split(/\r?\n/)) {
      const w = raw.split('#')[0].trim().split(/\s+/).filter(Boolean);
      if (w.length < 2) continue;
      if (w[0] === '@none') { none.add(w[1]); continue; }
      if (w[0] === '@timbre') { timbres.set(w[1], { woman: w.includes('for=woman'), only: w.includes('only') }); continue; }
      if (w[0].startsWith('@')) continue;
      const e = { files: [], game: [], sec: null };
      for (const t of w.slice(1)) {
        if (t.startsWith('file:')) e.files.push(t.slice(5));
        else if (!t.includes('=') && t !== '2d' && t !== 'wet' && t !== 'any') e.game.push(t);
      }
      const secs = e.files.map(x => oggSeconds(path.join(soundsDir, x))).filter(x => x !== null);
      if (secs.length) e.sec = Math.max(...secs);
      names.set(w[0], e);
    }
  }
  return _bank = { names, none, timbres };
}

// is this person of the story a woman (its template: «model: 1»); null: no such template here
function isWoman(who) {
  if (!fs.existsSync(npcDir)) return null;
  for (const file of fs.readdirSync(npcDir).filter(x => x.endsWith('.txt'))) {
    const text = fs.readFileSync(path.join(npcDir, file), 'utf8');
    const at = text.search(new RegExp('^\\[npc\\s+' + who.replace(/[^\w]/g, '') + '\\]', 'm'));
    if (at < 0) continue;
    const rest = text.slice(at + 1), next = rest.search(/^\[npc\s/m), own = next < 0 ? rest : rest.slice(0, next);
    const m = /^model:\s*(\d)/m.exec(own);
    return !!m && m[1] === '1';
  }
  return null;
}

// the word of a «sound» or a «bed» that names a place (StoryAudio.PlaceOf, BedPlaceOf)
const SoundWords = ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'wait', 'delay', 'fadein', 'skip'], BedWords = ['vol', 'range', 'pitch', '2d', 'wet', 'follow', 'all'];
const isNum = x => x !== undefined && x !== '' && !isNaN(Number(x));
function placeOf(w) {
  if (w[0] === 'sound') return w.length < 3 || w[1] === 'stop' || SoundWords.includes(w[2]) ? null : w[2];
  if (w[0] === 'bed') return w.length < 3 || w[1] === 'off' || isNum(w[2]) || BedWords.includes(w[2]) ? null : w[2];
  return null;
}
const after = (w, word, def) => { const i = w.indexOf(word); return i >= 0 && isNum(w[i + 1]) ? +w[i + 1] : def; };
const fadeOf = (w, from, def) => { for (let i = from; i < w.length; i++) { if (['keep', 'vol', 'range', 'pitch'].includes(w[i])) { i++; continue; } if (isNum(w[i])) return +w[i]; } return def; };

function lane(warn) {
  let music, musicAt = 0, musicFade = 0, musicOnce = null, ear = 'player', words = 0, earNum = 0;
  const beds = new Map(), mix = {}, mutes = new Set(), rings = [];
  const ring = (num, at, name, sec, where, from) => rings.push({ num, at, name, sec, where, from });
  return {
    // a step of sound: how long the scene waits on it, and what to say of it
    step(s, t, cam) {
      const w = s.w.filter(x => x !== 'all'), b = bank();
      let took = 0, shown = null, note = '';
      words++;
      switch (w[0]) {
        case 'music':
          if (w[1] === 'game') music = undefined;
          else if (w[1] === 'off') { music = null; musicOnce = null; }
          else { music = w[1]; musicAt = t; musicFade = fadeOf(w, 2, 3); const e = b.names.get(w[1]); musicOnce = w.includes('once') ? { num: s.num, at: t, sec: e ? e.sec : null, name: w[1] } : null; }
          break;
        case 'bed':
          if (w[1] === 'off') { const which = w[2] && !isNum(w[2]) ? w[2] : null; if (which) beds.delete(which); else beds.clear(); }
          else beds.set(w[1], placeOf(w));
          break;
        case 'mix':
          if (w[1] === 'reset') for (const k of Object.keys(mix)) delete mix[k];
          else if (isNum(w[2])) { if (+w[2] >= 1) delete mix[w[1]]; else mix[w[1]] = +w[2]; }
          break;
        case 'mute': mutes.add(w[1]); break;
        case 'unmute': if (w[1] === 'all') mutes.clear(); else mutes.delete(w[1]); break;
        case 'ear':
          ear = w[1]; earNum = s.num;
          if (w[1] === 'camera' && !cam) { note += 'no shot is on: the ear goes to the game\'s own camera; '; warn.push('line ' + s.num + ': «ear camera» with no shot on — put it after the «camera …» line it belongs to'); }
          break;
        case 'sound': {
          if (w[1] === 'stop') { note += 'stops ' + (w[2] || 'all') + '; '; break; }
          const place = placeOf(w), delay = after(w, 'delay', 0);
          let e = b.names.get(w[1]);
          const person = place && place.startsWith('npc.') && !place.endsWith('.thing') && /^speech_/.test(w[1]) ? place.slice(4) : null;
          if (person) {
            note += 'in ' + person + '\'s own timbre; ';
            if (isWoman(person) === true) {
              const hers = [...b.timbres].filter(([n, d]) => d.woman && !d.only).map(([n]) => n);
              if (hers.length && hers.every(n => b.none.has(w[1] + '__' + n) || !b.names.has(w[1] + '__' + n))) { note += 'SILENT: no woman\'s timbre has this sound; '; warn.push('line ' + s.num + ': «' + w[1] + '» at ' + person + ' — a woman, and no woman\'s timbre has that sound (assets/story/sounds: «@none»): she will be silent'); }
            }
          }
          const sec = e ? e.sec : null;
          ring(s.num, t + delay, w[1], sec, place || 'player');
          if (w.includes('wait')) {
            if (sec !== null) took = sec + delay;
            else { shown = '?'; note += 'waits for a game\'s sound: how long, the file does not say; '; warn.push('line ' + s.num + ': «sound ' + w[1] + ' … wait» waits out a sound of the game — its length is not known here: time it in the game, or wait a number of seconds'); }
          }
          else if (sec !== null) note += 'rings ' + f(sec) + ' s' + (delay ? ' from +' + f(delay) : '') + '; ';
          break;
        }
      }
      return { took, shown, note };
    },
    // a clip of ours starts on a body: the sounds its file carries, at their moments
    clip(who, c, t) {
      if (!c.events || !c.events.length) return '';
      for (const ev of c.events) ring(null, t + ev.t, ev.name, (bank().names.get(ev.name) || {}).sec || null, who, 'clip');
      return 'its sounds: ' + c.events.map(ev => ev.name + ' +' + f(ev.t)).join(', ') + '; ';
    },
    // what is held right now, for the table's column
    heard() {
      return [music === undefined ? null : music === null ? '♪—' : '♪' + music, ...[...beds.keys()].map(x => '~' + x), ...Object.entries(mix).map(([k, v]) => k + ' ' + v),
        mutes.size ? 'mute ' + mutes.size : null, ear === 'camera' ? 'ear cam' : null].filter(Boolean).join(' ') || '-';
    },
    // the end of the scene: the list of what rings, and the warnings that need its whole length
    end(total, filmed, id) {
      const out = [];
      if (rings.length) {
        out.push('one-shots: ' + rings.length + ' (line | at s | sound, where | rings)');
        for (const r of rings) out.push('  ' + (r.num === null ? 'clip' : String(r.num)).padStart(4) + ' | ' + f(r.at).padStart(5) + ' | ' + r.name + ' at ' + r.where + ' | ' + (r.sec !== null ? f(r.sec) + ' s' : 'a game\'s sound'));
      }
      for (const r of rings) {
        if (r.at > total) warn.push((r.num === null ? 'a clip\'s sound' : 'line ' + r.num) + ': «' + r.name + '» would start at ' + f(r.at) + ' s, after the scene has ended (' + f(total) + ' s): it is never heard');
        else if (r.sec !== null && r.at + r.sec > total + 1) warn.push((r.num === null ? 'a clip\'s sound' : 'line ' + r.num) + ': «' + r.name + '» rings to ' + f(r.at + r.sec) + ' s, the scene ends at ' + f(total) + ' s: its end is faded away with the scene (' + EndFade + ' s) — a «wait» after it, or an earlier line');
      }
      if (musicOnce && musicOnce.sec !== null && music === musicOnce.name && total - musicOnce.at > musicOnce.sec + 2) warn.push('line ' + musicOnce.num + ': «music ' + musicOnce.name + ' once» is ' + f(musicOnce.sec) + ' s long and nothing follows it for ' + f(total - musicOnce.at - musicOnce.sec) + ' s more: silence there — if not meant, drop «once» or put the next music');
      if (typeof music === 'string' && total - musicAt < musicFade + 2) warn.push('music «' + music + '» begins ' + f(total - musicAt) + ' s before the end with a fade-in of ' + f(musicFade) + ' s: it is barely up when the scene lets it go — begin it earlier, or «keep N» to let it ring on');
      if (ear === 'camera') out.push('  (the ear is at the camera to the end: line ' + earNum + ' — the scene\'s end gives it back)');
      if (filmed && !words && !rings.length) warn.push('a filmed scene with no sound of its own (no bed, no music, no one-shot): node tools/anim.js scene --sound ' + id + ' proposes a first pass');
      return out;
    },
  };
}

// a first sound pass for a scene (the head of this file): lines to put in, nothing written
function propose(id, sceneLines, clipOf) {
  const sc = sceneLines(id);
  if (!sc) return 'scene_sound: no scene «' + id + '» in assets/story/scenes';
  const head = {};
  const steps = [];
  for (const x of sc.lines) {
    const m = /^(roams|alone|aside|needs|check|test|place|guard|camera):\s*(.*)$/.exec(x.line);
    if (m) { head[m[1]] = m[2]; continue; }
    let text = x.line; if (text.startsWith('if ')) text = text.slice(text.indexOf(':') + 1).trim();
    steps.push({ num: x.num, text, w: text.split(/\s+/) });
  }
  const b = bank(), out = [], has = word => steps.some(s => s.w[0] === word);
  const put = (s, line, why) => out.push((s ? 'before line ' + s.num + ' («' + s.text.slice(0, 34) + '»)' : 'at the end') + ': ' + line + (why ? '      — ' + why : ''));
  const speech = s => (s.w[0] === 'npc' && s.w[2] === 'say') || (s.w[0] === 'bird' && s.w[1] === 'say') || s.w[0] === 'say';
  const filmedAt = steps.find(s => (s.w[0] === 'bars' && s.w[1] === 'on') || (s.w[0] === 'camera' && s.w[1] !== 'off'));
  // 1. the place's bed, in with the first shot
  if (!has('bed')) {
    const place = (head.place || '').split(/[.\s]/)[0], name = place ? 'bed_' + place : null;
    if (name && b.names.has(name)) put(filmedAt || steps[0], 'bed ' + name + ' 2', 'the place\'s own sound under the whole scene');
    else out.push('the place\'s bed: none in the catalogue' + (name ? ' («' + name + '»)' : '') + ' — what is heard there with nobody speaking (wind, fire, a hall\'s hum)? A game\'s loop (the «Звуки» tab) or a file through node tools/sound.js add <file> ' + (name || 'bed_<place>') + ' bed; then «bed ' + (name || 'bed_<place>') + ' 2» before the first shot');
  }
  // 2. the world down under a talk, back after it
  if (!has('mix')) {
    let i = 0;
    while (i < steps.length) {
      if (!speech(steps[i])) { i++; continue; }
      let j = i, said = 0, last = i;
      while (j < steps.length && (speech(steps[j]) || ['wait', 'camera', 'sub', 'sound'].includes(steps[j].w[0]) || (steps[j].w[0] === 'npc' && ['face', 'emote', 'clip', 'mark'].includes(steps[j].w[2])) || (steps[j].w[0] === 'player' && ['look', 'face', 'emote'].includes(steps[j].w[1])))) { if (speech(steps[j])) { said++; last = j; } j++; }
      if (said >= 2) { put(steps[i], 'mix world ' + TalkQuiet + ' 1', said + ' lines: the world a little down while they speak'); put(steps[last + 1], 'mix reset 1.5'); }
      i = Math.max(j, i + 1);
    }
  }
  // 3. the ear at the camera for a wide shot
  if (!has('ear')) {
    let at = null;
    for (const s of steps) {
      if (s.w[0] !== 'camera') continue;
      const wide = s.w[1] === 'on' && s.w.some(x => WideSizes.includes(x));
      if (wide && !at) { out.push('after line ' + s.num + ' («' + s.text.slice(0, 34) + '»): ear camera 1      — a wide shot: heard from where it is seen'); at = s; }
      else if (!wide && at) { put(s, 'ear player 1'); at = null; }
    }
  }
  // 4. a thing that falls or is thrown; a person that fades
  for (const s of steps) {
    const drop = (s.w[0] === 'prop' && (s.w[1] === 'drop' || s.w[1] === 'throw')) || (s.w[0] === 'npc' && s.w[2] === 'prop' && (s.w[3] === 'drop' || s.w[3] === 'throw'));
    const k = steps.indexOf(s), near = steps.slice(k, k + 3).some(x => x.w[0] === 'sound');
    if (drop && !near) out.push('after line ' + s.num + ' («' + s.text.slice(0, 34) + '»): sound sfx_drop ' + (s.w[0] === 'npc' ? 'npc.' + s.w[1] + '.thing' : 'player.thing') + '      — the thing lands (the step waits its fall): what it is made of says which sound');
    if (s.w[0] === 'npc' && s.w[2] === 'fade' && !s.w.slice(3).some(x => isNaN(Number(x)))) out.push('line ' + s.num + ' («' + s.text.slice(0, 34) + '»): a sound with the fading — «npc ' + s.w[1] + ' fade ' + (s.w[3] || 3) + ' whisper»');
  }
  // 5. the clips used here that carry no sound
  const mute = new Map();
  for (const s of steps) {
    const name = s.w[0] === 'player' && s.w[1] === 'clip' ? s.w[2] : s.w[0] === 'npc' && s.w[2] === 'clip' ? s.w[3] : null;
    if (!name || name === 'none' || mute.has(name)) continue;
    const c = clipOf(name);
    if (c && (!c.events || !c.events.length)) mute.set(name, c.marks.map(m => m.name + ' ' + f(m.t)).join(', '));
  }
  for (const [name, marks] of mute) if (marks) out.push('clip ' + name + ' carries no sound — its marks: ' + marks + ' (a sound belongs where the body touches: «event <time> <sound> [volume]» in its file)'); // (a clip with no mark touches nothing: a look, a turn — it needs none)
  // 6. a long stretch with no line and no sound
  {
    let quiet = 0, from = null;
    const flush = () => { if (quiet > LongQuiet && from) out.push('from line ' + from.num + ' («' + from.text.slice(0, 34) + '»): about ' + f(quiet) + ' s with no line and no sound — a music cue or the bed carries such a stretch'); quiet = 0; from = null; };
    for (const s of steps) {
      if (speech(s) || ['sound', 'music', 'bed'].includes(s.w[0])) { flush(); continue; }
      if (s.w[0] === 'wait') { if (!from) from = s; quiet += +s.w[1] || 1; }
      else if (s.w[0] === 'bird' && s.w[1] === 'circle') { if (!from) from = s; quiet += +s.w[2] || 30; }
    }
    flush();
  }
  // 7. the music: the scene's own point
  if (!has('music')) out.push('music: none. Say in one sentence what the scene is about, and give that moment its cue («music <track> 4»); a silence before it is a sound too («music off 3»). Tracks: the game\'s (tools/game_sounds.txt, «track …») or a file (node tools/sound.js add <file> <name> music)');
  if (!out.length) return 'scene ' + id + ': nothing to propose — it has its bed, its mix, its ear and its music';
  return 'scene ' + id + ' (' + sc.file + '): a first sound pass, by docs/jobs/scene.md «Звук сцени». Take what fits; then node tools/anim.js scene --dry ' + id + ' shows what is heard under each step.\n  ' + out.join('\n  ');
}

module.exports = { lane, propose, bank, oggSeconds };

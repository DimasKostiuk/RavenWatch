// The core's rules as a check (docs/ARCHITECTURE.md), run by rw_build before it compiles: what goes against them stops
// the build, with the place to use instead. Run alone: node tools/arch_check.js
// Two kinds of rule:
//   RULES    never: a line that does this stops the build. `allow` lists the files that own the thing.
//   RATCHET  old code as it stands is written down per file (tools/arch_baseline.json), and those counts may only fall.
//            A new case goes through the core or, where the rule has a mark and it is really needed, says why on its
//            own line. After mending old ones: --tighten (it only ever lowers).
// With the user's clear consent only: an entry in ALLOW, a raised count, --new-baseline (for a rule that changed).
const fs = require('fs'), path = require('path');
const src = path.join(__dirname, '..', 'src');
const BASELINE = path.join(__dirname, 'arch_baseline.json');

const npc = f => /^Npc.*\.cs$/.test(f);
const any = () => true;
const code = line => line.replace(/\/\/.*$/, ''); // (comments may name anything)

const RULES = [
  { id: 'game-move', files: npc, bad: /(^|[^.\w])(MoveTo|MoveTowards)\(|\.MoveTo\(/,
    why: 'the game\'s walk halts on a bend over 10°; walk through NpcMind.Move → NpcWalk.Step → NpcMind.Steer' },
  { id: 'path-ask', files: npc, bad: /\.(GetPath|HavePath)\(/,
    why: 'a person\'s way asked of the game\'s way finder: NpcWalk.Have / NpcWalk.Way (kept answers, the frame\'s limit, measured); a walk\'s own way: NpcWalk.Ask',
    allow: ['NpcWalk.cs', 'NpcWalkAsk.cs', 'NpcWalkRoom.cs', 'NpcNav.cs'] },
  { id: 'door-rpc', files: any, bad: /"UseDoor"/,
    why: 'the door command is a switch; use NpcDoors.Open / Shut', allow: ['NpcDoors.cs'] },
  // BodyClips owns the search; FateRite looks inside the raven itself; RavenFx is the game's own effect-child lookup
  { id: 'find-bone', files: any, bad: /Utils\.FindChild\(/,
    why: 'on a body it finds same-named bones of worn items and of the ravens sitting on it; use BodyClips.SkeletonBone',
    allow: ['BodyClips.cs', 'FateRite.cs', 'RavenFx.cs'] },
  { id: 'bone-layer', files: npc, bad: /void LateUpdate\s*\(/,
    why: 'bones of a person: a layer in NpcPose, in its order, not a LateUpdate of its own',
    allow: ['NpcFade.cs', 'NpcPose.cs', 'NpcPoseWatch.cs', 'NpcPreview.cs', 'NpcRaven.cs'] },
  // one way a body is moved by us (2026-11-07: five places wrote it themselves, only the seat's glide was drawn every frame and the rest ran
  // at the mind's twenty steps a second): NpcBody.Put / Slide. The files allowed own no body: a food in a hand, a preview, map dots
  { id: 'body-put', files: npc, bad: /(^|[\s(_])(?!dot\b)(\w+\.)?transform\.position\s*=[^=]|\b(rb|_rb|m_body)\.position\s*=[^=]|\.MovePosition\(/,
    why: 'a person\'s body is walked (NpcWalk) or seated (NpcBody); put or slid somewhere by us only through NpcBody.Put / NpcBody.Slide (held, drawn every frame, counted as a jump)',
    allow: ['NpcBody.cs', 'NpcEat.cs', 'NpcPreview.cs', 'NpcHouse.cs', 'NpcBase.cs'] },
  // the mind's clock (2026-11-07): the game runs a creature's mind twenty times a second from its physics step and hands it 0.05 s; a frame's
  // time there is the physics step's 0.02 s, and the turn on a bend, a slide, the walk report all ran 2.5 times slow
  { id: 'mind-time', files: f => /^(NpcMind|NpcWalk)\w*\.cs$/.test(f), bad: /Time\.(deltaTime|fixedDeltaTime)\b/,
    why: 'inside a person\'s mind and feet time is the mind\'s step: NpcMind.Dt (or the dt handed to a task); a timer of its own reads Time.time' },
  { id: 'npc-seed', files: npc, bad: /m_uid\.(ID|UserID)\s*\*|m_uid\.GetHashCode\(\)|\(u?int\)\s*\w+\.m_uid\.(ID|UserID)\b/,
    why: 'a ZDOID is new at every world load: seeded by it a person is another after a restart; seed by NpcKit.Seed(zdo)', allow: ['NpcKit.cs'] },
  { id: 'one-animator', files: any, bad: /new\s+PAnimClip\s*\(/,
    why: 'one player of clips: BodyAnimator; clips live in BodyClips', allow: ['BodyClips.cs', 'PlayerAnimPanel.cs'] },
  { id: 'one-music', files: any, bad: /MusicMan\.instance|\.TriggerMusic\(/,
    why: 'music has one owner: ask Audio.Music / MusicOff (layers, owner, lease), or the game and a scene fight over it', allow: ['Audio.cs'] },
  // the sources that were there before the core: voice lines, wings, backwards sounds, a pair's fx (the user 2026-11-04)
  { id: 'audio-source', files: any, bad: /AddComponent<AudioSource>\(/,
    why: 'a sound of your own: Audio.Sound / Bed / Music: a source of your own has no owner and nobody lets it go',
    allow: ['Audio.cs', 'OdinWings.cs', 'PairFx.cs', 'RavenFx.cs', 'RavenVoice.cs', 'SoundBack.cs'] },
  // a step with verbs («player go») is found by its verb's entry: asked by its first word it has no Begin, and the step does nothing, without a word (2026-11-06)
  { id: 'step-kind', files: f => f.startsWith('StoryScenes'), bad: /Kinds\.TryGetValue\(\w+\.W\[/,
    why: 'a step\'s kind is st.Kind (its verb\'s own entry, found once): looked up by its first word, a step with verbs does nothing' },
  // one limit for a raven's head (it went round behind its back: the user 2026-11-04): every turn put on it through RavenHead.Limit
  { id: 'raven-head', files: f => f === 'ShoulderLife.cs' || f === 'PerchLife.cs', bad: /(_head|_neck\.T)\.rotation\s*=(?!.*RavenHead\.Limit)/,
    why: 'a raven\'s head turns only through RavenHead.Limit (one limit for the bird, under 90°: past it the shorter way goes round its back)' },
  // the key is the second argument of Bind or of our Hide (tuning.cfg); a literal section is checked as well
  { id: 'config-key', files: any, bad: /(\.Bind|\bHide)(<[^>]*>)?\(\s*("[^"]*[='\[\]\\][^"]*"|[^,"]+|"[^"]*")\s*,\s*"[^"]*[='\[\]\\][^"]*"|(\.Bind|\bHide)(<[^>]*>)?\(\s*"[^"]*[='\[\]\\][^"]*"\s*,/,
    why: 'no = \' [ ] \\ " in a config section or key: one bad key kills the whole mod at its start' },
  // who reads config (2026-11-06: a config copy of a clip won over the mod's own, and the ghost's arm stayed as it was after a fix).
  // Config holds what is the machine's own; what the mod ships has ONE copy, in assets, read there on the author's machine
  // (StoryFiles.Source: Blueprint.Data, BodyClips.Dir, BodyGait). A new reader of config is a decision: here, with the user, and why.
  //   the machine's own:  the author's tools and their output (AnimStage, BlackBox, FlightRec, JoltWatch, LookDiag, NpcSelf, NpcWalkBox,
  //                       PlayerAnimLog, SoundPanel, Studio, TakeoffKeys, WingPanel, Mute, Motor flight.txt, PerchTurn, DevKit),
  //                       a player's own (HuginWhispers/Lang phrase files, RavenJournal, RavenLooks, Outfits, RavenItem icons,
  //                       Errands, FlightTemplates, FxLib, FxMix, SitSystem/SitTemplates, Workshop/WsAssets models)
  //   one copy, by Source: Blueprint, BodyClips, BodyGait, Photos, StoryFiles, StoryQuests, NpcDefs, NpcTalk, AudioBank
  { id: 'config-read', files: any, bad: /Paths.ConfigPath/,
    why: 'a new reader of config: is it the machine\x27s own, or a copy of what the mod ships? The mod\x27s data has one copy in assets (StoryFiles.Source); say which in tools/arch_check.js (config-read), with the user',
    allow: ['AnimStage.cs', 'AudioBank.cs', 'BlackBox.cs', 'Blueprint.cs', 'BodyClips.cs', 'BodyGait.cs', 'DevKit.cs', 'Errands.cs', 'FlightRec.cs', 'FlightTemplates.cs', 'FxLib.cs', 'FxMix.cs', 'HuginWhispers.cs', 'JoltWatch.cs', 'Lang.cs', 'LookDiag.cs', 'Motor.cs', 'Mute.cs', 'NpcDefs.cs', 'NpcSelf.cs', 'NpcTalk.cs', 'NpcWalkBox.cs', 'Outfits.cs', 'PerchTurn.cs', 'Photos.cs', 'PlayerAnimLog.cs', 'RavenItem.cs', 'RavenJournal.cs', 'RavenLooks.cs', 'SitSystem.cs', 'SitTemplates.cs', 'SoundPanel.cs', 'StoryFiles.cs', 'StoryQuests.cs', 'Studio.cs', 'TakeoffKeys.cs', 'WingPanel.cs', 'Workshop.cs', 'WsAssets.cs'] },
];

// the { … } that opens at or after `from`; null if none closes (braces in strings are rare enough to ignore here)
function block(text, from) {
  const start = text.indexOf('{', from);
  if (start < 0) return null;
  let depth = 0;
  for (let i = start; i < text.length; i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}' && --depth === 0) return { start, end: i + 1, body: text.slice(start, i + 1) };
  }
  return null;
}
// the whole line holding `at`
function lineAt(text, at) {
  const a = text.lastIndexOf('\n', at) + 1;
  let b = text.indexOf('\n', at);
  if (b < 0) b = text.length;
  return text.slice(a, b);
}
const inComment = (text, at) => { const l = lineAt(text, at), off = at - (text.lastIndexOf('\n', at) + 1); const c = l.indexOf('//'); return c >= 0 && c < off; };
const stripComments = s => s.split('\n').map(code).join('\n');

// a catch that writes nothing anywhere: the cause is lost
function quietCatches(text) {
  const re = /\bcatch\b/g; let m, n = 0;
  while ((m = re.exec(text))) {
    if (inComment(text, m.index)) continue;
    const b = block(text, m.index);
    if (!b) continue;
    if (lineAt(text, m.index).includes('// quiet:') || b.body.includes('// quiet:')) continue;
    if (!/Log|PatchGuard|throw|Errors\.Add|Warn|Fail|Error/.test(stripComments(b.body))) n++;
  }
  return n;
}

// a patch on the game's creature, fight, AI and equipment code that can throw into the game's own method
const GUARDED_KINDS = /HarmonyPatch\(typeof\((Character|Humanoid|BaseAI|MonsterAI|AnimalAI|VisEquipment|Attack)\b/g;
function bareCount(text) {
  const re = new RegExp(GUARDED_KINDS.source, 'g'); let m, n = 0;
  while ((m = re.exec(text))) {
    if (inComment(text, m.index)) continue;
    let end = text.lastIndexOf('[', m.index), depth = 0; // (past the attribute: a «new Type[] { … }» in it is no body)
    for (; end < text.length; end++) { if (text[end] === '[') depth++; else if (text[end] === ']' && --depth === 0) break; }
    const owner = block(text, end);
    if (!owner) continue;
    const isClass = /\bclass\b/.test(text.slice(end, owner.start));
    if (!isClass) { if (!owner.body.includes('PatchGuard')) n++; continue; } // (the attribute stands on the method itself)
    const methods = /\b(Prefix|Postfix|Finalizer)\s*\(/g; let f;
    while ((f = methods.exec(owner.body))) {
      const body = block(owner.body, f.index);
      if (body && !body.body.includes('PatchGuard')) n++;
    }
  }
  return n;
}

const perLine = (re, mark) => text => text.split('\n').filter(l => re.test(code(l)) && !l.includes('// ' + mark)).length;

const RATCHET = [
  { id: 'quiet-catch', count: quietCatches,
    why: 'a catch that says nothing hides the cause: log it once; if silence is right, say why: // quiet: <why>' },
  { id: 'lasting-id', skip: ['NpcKit.cs'], count: perLine(/UserID\s*\+\s*":"|UserID\)\.Append\(':'\)|(GetZDOID\(\)|m_uid)\.ToString\(\)/, 'session:'),
    why: 'a ZDOID changes on every world load, so a kept "user:id" points nowhere after a restart: NpcKit.Save / Resolve; if it lives one session only: // session: <why>' },
  { id: 'heavy-call', count: perLine(/FindObjects?OfType|GetAllCharacters\(\)|Physics\.(Overlap|RaycastAll|SphereCastAll)|GetComponentsInChildren/, 'heavy:'),
    why: 'a heavy call: use what is gathered already (Senses, NpcCensus, a cache) or run it on an event or a rare timer; where it is needed, say when and how often: // heavy: <when>' },
  { id: 'patch-guard', count: bareCount,
    why: 'a patch on the game\'s creature code needs try + PatchGuard.Fail: one that throws must not break the game\'s method' },
];

// exceptions the user agreed to: { rule: 'id', file: 'X.cs', why: '…' }
const ALLOW = [];

const sources = {};
for (const f of fs.readdirSync(src)) if (f.endsWith('.cs')) sources[f] = fs.readFileSync(path.join(src, f), 'utf8');

function ratchetCounts() {
  const out = {};
  for (const r of RATCHET) {
    out[r.id] = {};
    for (const f of Object.keys(sources).sort()) {
      if ((r.skip || []).includes(f)) continue;
      const n = r.count(sources[f]);
      if (n) out[r.id][f] = n;
    }
  }
  return out;
}
const total = counts => RATCHET.map(r => r.id + ' ' + Object.values(counts[r.id] || {}).reduce((a, b) => a + b, 0)).join(', ');
const save = counts => fs.writeFileSync(BASELINE, JSON.stringify(counts, null, 1) + '\n');

if (process.argv.includes('--new-baseline')) { const now = ratchetCounts(); save(now); console.log('new baseline: ' + total(now)); process.exit(0); }
if (process.argv.includes('--tighten')) {
  if (!fs.existsSync(BASELINE)) { console.log('tools/arch_baseline.json is missing: --tighten only lowers it'); process.exit(1); }
  const now = ratchetCounts(), old = JSON.parse(fs.readFileSync(BASELINE, 'utf8')), out = {};
  for (const r of RATCHET) {
    out[r.id] = {};
    for (const f of Object.keys(now[r.id])) { const n = Math.min(now[r.id][f], (old[r.id] || {})[f] || 0); if (n) out[r.id][f] = n; }
  }
  save(out); console.log('tightened: ' + total(out));
  process.exit(0);
}

let found = 0;
for (const f of Object.keys(sources)) {
  const rules = RULES.filter(r => r.files(f) && !(r.allow || []).includes(f) && !ALLOW.some(a => a.rule === r.id && a.file === f));
  if (!rules.length) continue;
  sources[f].split('\n').forEach((line, i) => {
    const c = code(line);
    for (const r of rules) if (r.bad.test(c)) {
      found++;
      console.log('ARCH ' + r.id + ': src/' + f + ':' + (i + 1) + '\n  ' + line.trim().slice(0, 120) + '\n  → ' + r.why);
    }
  });
}

let base = null;
if (!fs.existsSync(BASELINE)) { found++; console.log('ARCH baseline: tools/arch_baseline.json is missing'); }
else {
  base = JSON.parse(fs.readFileSync(BASELINE, 'utf8'));
  const now = ratchetCounts();
  for (const r of RATCHET) for (const f of Object.keys(now[r.id])) {
    const had = (base[r.id] || {})[f] || 0;
    if (now[r.id][f] > had) { found++; console.log('ARCH ' + r.id + ': src/' + f + ' has ' + now[r.id][f] + ', allowed ' + had + '\n  → ' + r.why); }
  }
}

if (found) { console.log('\n' + found + ' against the core\'s rules: docs/ARCHITECTURE.md. Fix it in the core; an exception only with the user\'s consent.'); process.exit(1); }
console.log('arch_check: ok (old debt: ' + total(base) + ')');

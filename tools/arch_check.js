// The core's rules as a check (docs/ARCHITECTURE.md, the map; its tables in docs/arch/*.md), run by rw_build before it compiles:
// what goes against them stops the build, with the place to use instead. Run alone: node tools/arch_check.js
// The documents are checked too (at the end): every code name they give in back-ticks is in the code (arch-doc: the map, its sections,
// the job pages docs/jobs/*.md), and every file of src/ is named in the map or a section (arch-file), so its section is one search away.
// Two kinds of rule:
//   RULES    never: a line that does this stops the build. `allow` lists the files that own the thing. A rule reads the line's code,
//            its comment taken off; one marked `whole` reads the whole line (code-in-comment: the comment is what it judges).
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
  // Thunderstore's automatic check flagged how the mod reached into another UI mod (2.9.0, 2026-11-08). The mod knows no UI mod by name: what it
  // needs it finds by the game's own objects (FeederUi.WindowOf) and guards at the game's own doors (GraveRavens.Keeps)
  { id: 'no-ui-mod-names', files: any, bad: /seneal/i,
    why: 'no other UI mod is named or reached into in the code: find it through the game\'s own objects (FeederUi.WindowOf), guard at the game\'s own doors (GraveRavens.Keeps)' },
  // a quest's ring round the Hall came red: a map mod draws every EventArea pin as a raid, whatever colour was laid on it (2026-11-18)
  { id: 'map-zone', files: any, bad: /PinType\.EventArea/,
    why: 'a zone\'s ring on the map through ErrandMap.Zone (no fight: a ring on a «!» pin, coloured by its owner; a fight: the game\'s raid ring)', allow: ['ErrandMap.cs'] },
  { id: 'game-move', files: npc, bad: /(^|[^.\w])(MoveTo|MoveTowards)\(|\.MoveTo\(/,
    why: 'the game\'s walk halts on a bend over 10°; walk through NpcMind.Move → NpcWalk.Step → NpcMind.Steer' },
  // Shader.Find("Standard") is null in this game: the echo stayed flat, the heart's papers never showed, the shadow looked flat (2026-11-15)
  { id: 'standard-shader', files: any, bad: /Shader\.Find\(\s*"Standard"\s*\)/,
    why: 'the standard shader through FbxLook.Standard (the loaded shaders\' list: Shader.Find does not see it here)', allow: ['WsAssets.cs'] },
  { id: 'path-ask', files: npc, bad: /\.(GetPath|HavePath)\(/,
    why: 'a person\'s way asked of the game\'s way finder: NpcWalk.Have / NpcWalk.Way (kept answers, the frame\'s limit, measured); a walk\'s own way: NpcWalk.Ask',
    allow: ['NpcWalk.cs', 'NpcWalkAsk.cs', 'NpcWalkRoom.cs', 'NpcNav.cs'] },
  // a person's bed changed past the one door left the bed still signed with its old person (2026-11-09)
  { id: 'bed-door', files: f => /^(Npc|Story)\w*\.cs$/.test(f) && f !== 'NpcResident.cs', bad: /NpcLocker\.Put(Ref)?\(\s*[\w.]+\s*,\s*(NpcResident\.)?KBed\b/,
    why: 'a person\'s bed changes only through NpcResident.SetBed: the bed itself says whose it is, and the one it leaves must say nobody\'s' },
  // «a settler who lives here» was spelled out in nine places, five of them with no look at the template itself: a resident whose template
  // was gone (a file taken away, another version) threw every second in its hunger and its toil (2026-11-14)
  { id: 'settled-one', files: f => /^Npc\w*\.cs$/.test(f) && f !== 'NpcResident.cs', bad: /NpcResident\.Is\(\s*\w+\s*\)\s*(&&|\|\|)\s*!?\w+\.Def\.Settler\b|\.Def\.Settler\s*(&&|\|\|)\s*!?NpcResident\.Is\(/,
    why: '«a resident who is a settler» is one question: NpcResident.Settled (it holds for a person whose template is not here too)' },
  { id: 'door-rpc', files: any, bad: /"UseDoor"/,
    why: 'the door command is a switch; use NpcDoors.Open / Shut', allow: ['NpcDoors.cs'] },
  // BodyClips owns the search; FateRite looks inside the raven itself; RavenFx is the game's own effect-child lookup
  { id: 'find-bone', files: any, bad: /Utils\.FindChild\(/,
    why: 'on a body it finds same-named bones of worn items and of the ravens sitting on it; use BodyClips.SkeletonBone',
    allow: ['BodyClips.cs', 'FateRite.cs', 'RavenFx.cs'] },
  // the game's head call throws for a body without a humanoid head (Lit, the dvergr: 2026-11-17 the Hall's raven on his head stopped its place's
  // raven 30 s each time); a person, a story body, a place's bird: Body.Head (asked, never thrown) or Body.HeadBone (kept)
  { id: 'head-point', files: f => /^(Npc|Story|Loc)/.test(f), bad: /\.GetHeadPoint\(\)/,
    why: 'a person or a story body may have no head the game knows (a dvergr): the call throws; use Body.Head or Body.HeadBone' },
  { id: 'bone-layer', files: npc, bad: /void LateUpdate\s*\(/,
    why: 'bones of a person: a layer in NpcPose, in its order, not a LateUpdate of its own',
    allow: ['NpcFade.cs', 'NpcPose.cs', 'NpcPoseWatch.cs', 'NpcPreview.cs', 'NpcRaven.cs'] },
  // one way a body is moved by us (2026-11-07: five places wrote it themselves, only the seat's glide was drawn every frame and the rest ran
  // at the mind's twenty steps a second): NpcBody.Put / Slide. The files allowed own no body: a food in a hand, a preview, map dots
  { id: 'body-put', files: npc, bad: /(^|[\s(_])(?!dot\b)(\w+\.)?transform\.position\s*=[^=]|\b(rb|_rb|m_body)\.position\s*=[^=]|\.MovePosition\(/,
    why: 'a person\'s body is walked (NpcWalk) or seated (NpcBody); put or slid somewhere by us only through NpcBody.Put / NpcBody.Slide (held, drawn every frame, counted as a jump)',
    allow: ['NpcBody.cs', 'NpcEat.cs', 'NpcPreview.cs', 'NpcHouse.cs', 'NpcBase.cs'] },
  // a synced animator flag on a person (2026-11-11): the game's ZSyncAnimation.SetBool writes the ZDO only when this game's animator differs,
  // and an animator made anew says «off» whatever the ZDO says — «in bed» stayed in a person's ZDO and it walked about lying
  { id: 'anim-flag', files: npc, bad: /\b(za|\w*zanim|m_animator)\.Set(Bool|Int|Float)\((?!anim, on\)|name, v\))|GetComponent<ZSyncAnimation>\(\)\.Set(Bool|Int|Float)\(/,
    why: 'a person\'s synced animator flag or value goes through NpcBody.AnimFlag / AnimValue (it writes the ZDO itself: the game\'s SetBool skips it when this game\'s animator already says so)' },
  // a key taken off a ZDO (2026-11-12, read in the game's code): ZDO.RemoveString… takes it off this game's copy only — no new revision, and
  // ZDO.Deserialize only adds and overwrites keys — so the server and every other game keep the old value. NpcLocker took a person's values
  // off it so, and the old ones came back over its locker at the next owner (one saved was «in need» again after a relog on a dedicated server)
  { id: 'zdo-remove', files: any, bad: /\.Remove(String|Int|Bool|Long|Float|Vec3|Quaternion|ByteArray)\(/,
    why: 'the game\'s ZDO.Remove… takes a key off this game\'s copy only (not sent: the server and the other games keep the old value); write a value every game gets — "" or a mark (a person\'s value into its locker: NpcLocker.Put, its mark NpcLocker.Off)' },
  // the mind's clock (2026-11-07): the game runs a creature's mind twenty times a second from its physics step and hands it 0.05 s; a frame's
  // time there is the physics step's 0.02 s, and the turn on a bend, a slide, the walk report all ran 2.5 times slow
  { id: 'mind-time', files: f => /^(NpcMind|NpcWalk)\w*\.cs$/.test(f), bad: /Time\.(deltaTime|fixedDeltaTime)\b/,
    why: 'inside a person\'s mind and feet time is the mind\'s step: NpcMind.Dt (or the dt handed to a task); a timer of its own reads Time.time' },
  // the map has roofs and upper floors too: a settler who could not reach his work was put on the roof over it, past a height check that compared the spot with itself (2026-11-09)
  { id: 'same-floor', files: f => /^(NpcBody|NpcMind\w*|NpcWalkStuck)\.cs$/.test(f), bad: /NpcKit\.Valid\(/,
    why: 'what puts a body somewhere takes a spot on its own floor: NpcKit.SameFloor (the map has roofs and upper floors too)' },
  // the map's nearest spot with a growing reach is one spot every ring: on a workbench's top, the top itself — Eyvind stayed up there a whole evening, «no floor free beside it» 60 times (2026-11-20)
  { id: 'floor-beside', files: f => /^(Npc|Story)\w*\.cs$/.test(f), bad: /NpcKit\.SameFloor\(out \w+, [^,]+, r\b/,
    why: 'a spot for a body by a spot, ring by ring: NpcKit.FloorBeside (spots round it, each judged) — not NpcKit.SameFloor with a growing reach (the same nearest spot every time)' },
  // a person's name as shown is in the reader's language (2026-11-11: the names became translated): what is kept or compared is its source
  { id: 'kept-name', files: f => /^(Npc|Story)\w*\.cs$/.test(f), bad: /NpcMemory\.(Tell|Remember|TellHome)\([^;]*\.Name\)|\.What\s*==\s*[\w.]+\.Name\b|\b\w+\.Name\s*==\s*[\w.]+\.What\b/,
    why: 'a person\'s Name is shown (Lang.T, the reader\'s language): a memory keeps and a check compares its NpcActor.Key; a kept name is shown through NpcActor.Shown' },
  { id: 'npc-seed', files: npc, bad: /m_uid\.(ID|UserID)\s*\*|m_uid\.GetHashCode\(\)|\(u?int\)\s*\w+\.m_uid\.(ID|UserID)\b/,
    why: 'a ZDOID is new at every world load: seeded by it a person is another after a restart; seed by NpcKit.Seed(zdo)', allow: ['NpcKit.cs'] },
  // a person's own life played clips by name in eleven places, none asking whether they fit it (2026-11-15: Heid drank from a tankard at every fire, a dwarf
  // would have got a man's hand points): a move of its own life goes through its gate, NpcBody.Move (NpcMoves: its body's clips, its template's never/moves)
  { id: 'own-move', files: npc, bad: /\bBody\.Clip\(\s*"(?!"|none")/,
    why: 'a short move of a person\'s own life goes through NpcBody.Move (NpcMoves: a clip of its body, never one its template forbids, its own moves); Clip plays exactly what a scene, a story word or a carried thing names' },
  // «so in the story's places» asked as «a blueprint's copy» (2026-11-16): the game's Hugin was kept off the Hall's map table, and sat on the barrow's,
  // whose inside is put by LocInside, not by a blueprint — the user: «чому це правило не поширюється на всі створені мною або доповнені локації»
  { id: 'story-piece', files: any, whole: true, bad: /^(?!.*\/\/ blueprint only:)(?:(?!\/\/).)*Blueprint\.Copy\(/,
    why: 'a rule for the story\'s places asks Blueprint.StoryPiece (a blueprint\'s copy or a piece of an inside of ours); Blueprint.Copy only for what is the blueprint\'s own — its place\'s state, layers, «lock…» rules by its stamp — said on its line: // blueprint only: <why>',
    allow: ['Blueprint.cs'] },
  { id: 'one-animator', files: any, bad: /new\s+PAnimClip\s*\(/,
    why: 'one player of clips: BodyAnimator; clips live in BodyClips', allow: ['BodyClips.cs', 'PlayerAnimPanel.cs'] },
  { id: 'one-music', files: any, bad: /MusicMan\.instance|\.TriggerMusic\(/,
    why: 'music has one owner: ask Audio.Music / MusicOff (layers, owner, lease), or the game and a scene fight over it', allow: ['Audio.cs'] },
  // who may hold a source of its own: the core; a raven's voice (filters of its own on each line); the gifts' effects (RavenFx: left there on purpose, the user 2026-11-09);
  // SoundBack's silent catch (nothing heard comes out of it). Wings, a pair's fx and backwards sounds moved into the core (2026-11-09)
  { id: 'audio-source', files: any, bad: /AddComponent<AudioSource>\(/,
    why: 'a sound of your own: Audio.Sound / Bed / Music: a source of your own has no owner and nobody lets it go',
    allow: ['Audio.cs', 'RavenFx.cs', 'RavenVoice.cs', 'SoundBack.cs'] },
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
  //   one copy, by Source: Blueprint, BodyClips, BodyGait, Photos, StoryFiles, StoryQuests, NpcDefs, NpcTalk
  { id: 'config-read', files: any, bad: /Paths.ConfigPath/,
    why: 'a new reader of config: is it the machine\x27s own, or a copy of what the mod ships? The mod\x27s data has one copy in assets (StoryFiles.Source); say which in tools/arch_check.js (config-read), with the user',
    allow: ['AnimStage.cs', 'BlackBox.cs', 'Blueprint.cs', 'BodyClips.cs', 'BodyGait.cs', 'DevKit.cs', 'Errands.cs', 'FlightRec.cs', 'FlightTemplates.cs', 'FxLib.cs', 'FxMix.cs', 'HuginWhispers.cs', 'JoltWatch.cs', 'Lang.cs', 'LookDiag.cs', 'Motor.cs', 'Mute.cs', 'NpcDefs.cs', 'NpcSelf.cs', 'NpcTalk.cs', 'NpcWalkBox.cs', 'Outfits.cs', 'PerchTurn.cs', 'Photos.cs', 'PlayerAnimLog.cs', 'RavenItem.cs', 'RavenJournal.cs', 'RavenLooks.cs', 'SitSystem.cs', 'SitTemplates.cs', 'SoundPanel.cs', 'StoryFiles.cs', 'StoryQuests.cs', 'Studio.cs', 'TakeoffKeys.cs', 'WingPanel.cs', 'Workshop.cs', 'WsAssets.cs'] },
  // a base's pieces looked over again beside its own look (2026-11-08: mending, tables, tubs and work each had a «not made yet» look over
  // every piece in Reach — thousands on a big base): its look is NpcBase.Things / Wears / Tables / Tubs; not made yet: none till it is
  { id: 'base-pieces', files: any, bad: /GetAllPiecesInRadius\([^;]*NpcBase\.Reach/,
    why: 'a base\x27s things are its own look: NpcBase.Things(type), Wears, Tables, Tubs, Round (null: not made yet — wait, never look over its pieces)', allow: ['NpcBase.cs'] },
  // a raven gift's refusals were checked in five places in five orders (2026-11-07): the circle showed one reason, the press said
  // another, a dot was green and the gift would not go. One list, RavenSkills.Why; a gift's own refusal of the place is asked there
  { id: 'gift-why', files: any, bad: /\b(Strike|Dome|Beast|Decoy|OdinWings)\.Refused\(|\bHabits\.Blocks\(/,
    why: 'why a raven gift cannot go now is one list: RavenSkills.Why (Use, Status, UsableNow, Dot, FreeStrike read it); add a new refusal there', allow: ['RavenSkills.cs'] },
  // a word to the game that runs a person (2026-11-07): an act sent to its old owner, or to owner 0, was lost after the player had already paid for it
  { id: 'npc-owner-rpc', files: any, bad: /\bRpc(Act|Order)\b/,
    why: 'orders and acts to a person go through NpcActor.Order / Act (waits for an owner, sends on once, keeps who sent it); a body not loaded here: NpcActor.ActTo', allow: ['NpcActor.cs'] },
  // a talk, its patience and its repeats are reckoned on the game of the one who talks; a person's mind runs only on its owner's (2026-11-08: «at work» was
  // always false on a second player's game)
  // whether a person is on its guard is one answer (2026-11-08: a guard's pose and weapon by «a fight»): its pose, its hands, its gait ask NpcAlert
  { id: 'alert-core', files: f => /^(NpcStance|NpcGear|NpcGait|BodyGait).cs$/.test(f), bad: /Layer.Fight|NpcFeel.Fright/,
    why: 'on its guard or not: NpcAlert.On(person) — a fight, a fright, just fought, on every game' },
  // the Fine pictures carry their own scale (BookArt.FinePpu): a multiplier at the Image makes them Fine times thinner — and with it Unity took their least size Fine
  // times too large (a pill of radius 11 asked for 92 units of height, 2026-11-08)
  { id: 'fine-multiplier', files: any, bad: /pixelsPerUnitMultiplier\s*=\s*BookArt\.Fine\b/,
    why: 'RoundFine, FrameFine, TornFill, TornLine, SheetFill: set the sprite and type only — their scale is in the sprite (BookArt.FinePpu)' },
  // what is wild was asked by the prefab in two places with two lists (2026-11-08): the ground under a place let every Piece pass, and
  // PlantEverything makes the game's own bushes pieces — bushes stood inside the Hall; the game's plain bushes (only StaticPhysics) were missed too
  { id: 'wild-core', files: any, bad: /GetComponent<(TreeLog|MineRock5?|Destructible|StaticPhysics)>\(\)/,
    why: 'the world\'s own growth and stone, and nobody\'s: Wild.Is(zdo | view) (its kind by prefab: Wild.Kind)', allow: ['Wild.cs'] },
  // (its card and its heart's board too: read on anyone's game — its card said «у бою тікає» of everyone on a second player's, 2026-11-08)
  { id: 'mind-elsewhere', files: f => /^(NpcTalk\w*|NpcPatience|NpcFavours|NpcAway|NpcProfile|NpcBoard|NpcMenu)\.cs$/.test(f), bad: /\.Mind\.(NowLayer|SeatedNow|Now|Style)\b|\.Mind\.Of\(|\.Vitals\.Down\b/,
    why: 'what it does now, on any game: NpcMind.LayerNow(person) / NpcMind.SeatedOf(person) / NpcMind.EatingOf(person), its order: NpcMind.OrderOf(person), down: NpcVitals.IsDown(person), how it fights: NpcNature.FightNow(person); its Mind and Vitals stand still where it is not run' },
  // its last fight was read from the mind's own fields by the themes a talk asks on the talker's game (2026-11-08): there they stand still, «after a fight» never came
  { id: 'fought-mind', files: any, bad: /\.Fought(At|Ran|Won)\b/,
    why: 'its last fight on any game: NpcMind.FoughtOf(person, out ran, out won)', allow: ['NpcMind.cs'] },
  // a note put into the middle of a line took the rest of the line into itself (2026-11-11, NpcTalkAbout: «…; // (note) r.Count = tone; r.At = NowSec;» —
  // two assignments in a comment compile, the build was green and the retelling of words broke). Judged on the whole line, its comment too (`whole`)
  { id: 'code-in-comment', files: any, whole: true, bad: { test: swallowed },
    why: 'the line\'s comment ends with a statement (an assignment, a call, a return): code after «//» does not run — put the note at the end of the line, or on the line above' },
];

// where a line's comment begins: the first «//» outside a string or a character (-1: none)
function commentAt(line) {
  let q = null;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (q) { if (c === '\\') i++; else if (c === q) q = null; continue; }
    if (c === '"' || c === '\'') q = c;
    else if (c === '/' && line[i + 1] === '/') return i;
  }
  return -1;
}
// code-in-comment: code stands before the comment, and the comment's last words are a statement closed by «;». A line that is all comment is not
// judged (prose there may say «x = y;»), nor a statement elsewhere in a note than at its end (measured on src 2026-11-12: 9 notes of 233 244 lines
// hold «word = words;» somewhere, none after code and at its end)
const SWALLOWED = /(^|[\s;)}])(?:[A-Za-z_][\w.\[\]]*\s(?:=|\+=|-=|\*=|\|=|&=)\s[^=;][^;]*|[A-Za-z_][\w.]*\([^;]*\)|(?:return|break|continue)\b[^;]*);\s*$/;
function swallowed(line) {
  const at = commentAt(line);
  return at > 0 && line.slice(0, at).trim().length > 0 && SWALLOWED.test(line.slice(at + 2));
}

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
  { id: 'flat-distance', only: /^(Story|Loc)/, count: perLine(/Utils\.DistanceXZ\(/, 'flat:'),
    why: 'a distance by the map alone sees a crypt of ours (it hangs 5120 m over its door) and the grass over it as one spot — the user 2026-11-15: a story\'s quiet round on the grass left the whole crypt without foes. Measure from a story place with StoryWhere.Apart; where the map alone is right (a log\'s number, a step on one floor), say why: // flat: <why>' },
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
      if ((r.skip || []).includes(f) || (r.only && !r.only.test(f))) continue;
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
    for (const r of rules) if (r.bad.test(r.whole ? line : c)) {
      found++;
      console.log('ARCH ' + r.id + ': src/' + f + ':' + (i + 1) + '\n  ' + line.trim().slice(0, 120) + '\n  → ' + r.why);
    }
  });
}

// arch-doc: the map (docs/ARCHITECTURE.md), its sections (docs/arch/*.md) and the job pages (docs/jobs/*.md) name code in back-ticks, and a reader goes where they point.
// Every such name must be in the code; a top-level class that lives in a file of another name is written with that file once per
// document: `NpcClock` (NpcPerf.cs); a file that is a part of another class is written with its extension: `NpcWalkLinks.cs`.
// Checked: `X.cs` is a file of src/; `Our.Member` has that member in a file declaring Our; a CamelCase name is a class, a file or a
// word of the code. A game's or Unity's name (no class of ours) is let be. Else the document rots and sends the reader to nothing.
{
  const docs = path.join(__dirname, '..', 'docs');
  const pages = dir => fs.existsSync(path.join(docs, dir)) ? fs.readdirSync(path.join(docs, dir)).filter(f => f.endsWith('.md')).map(f => path.join(docs, dir, f)) : [];
  const mapFiles = [path.join(docs, 'ARCHITECTURE.md'), ...pages('arch')], docFiles = [...mapFiles, ...pages('jobs')];
  const all = Object.values(sources).join('\n');
  const word = w => new RegExp('\\b' + w + '\\b');
  const top = {}, declared = {};
  for (const f of Object.keys(sources)) {
    for (const m of sources[f].matchAll(/^ {0,4}(?:\w+\s+)*(?:class|struct)\s+([A-Z]\w*)/gm)) (top[m[1]] = top[m[1]] || new Set()).add(f);
    for (const m of sources[f].matchAll(/\b(?:class|struct|enum)\s+([A-Z]\w*)/g)) (declared[m[1]] = declared[m[1]] || new Set()).add(f);
  }
  const fileOf = new Set(Object.keys(sources).map(f => f.slice(0, -3)));
  const bad = (doc, what) => { found++; console.log('ARCH arch-doc: docs/' + path.relative(docs, doc).replace(/\\/g, '/') + ': ' + what); };
  for (const doc of docFiles) {
    if (!fs.existsSync(doc)) { bad(doc, 'is missing'); continue; }
    const text = fs.readFileSync(doc, 'utf8'), seen = new Set();
    for (const m of text.matchAll(/`([^`\n]+)`/g)) {
      const t = m[1];
      let x = /^(\w+)\.cs$/.exec(t);
      if (x) { if (!sources[t]) bad(doc, '`' + t + '` is no file of src/'); continue; }
      x = /^([A-Z][A-Za-z0-9_]*)(?:\.([A-Za-z0-9_]+))?$/.exec(t);
      if (!x) continue;
      const head = x[1], mem = x[2], key = head + (mem ? '.' + mem : '');
      if (seen.has(key)) continue;
      seen.add(key);
      // the third column of a table row («Не роби») names what must NOT be written, so a name missing from the code is right there
      const lineStart = text.lastIndexOf('\n', m.index) + 1, before = text.slice(lineStart, m.index);
      const dontColumn = before.startsWith('| ') && before.split(' | ').length >= 3;
      const camel = /[a-z]/.test(head) && /^[A-Z].*[A-Z]/.test(head); // NpcClock, StudioScene: a name the reader will look for
      if (!declared[head]) {
        if (fileOf.has(head)) { bad(doc, '`' + t + '`: ' + head + ' is a file, not a class: write `' + head + '.cs`'); continue; }
        if ((camel || mem) && !dontColumn && !word(head).test(all)) bad(doc, '`' + t + '`: no ' + head + ' in src/');
        continue;
      }
      if (mem && !dontColumn && !word(head + '\\.' + mem).test(all) && ![...declared[head]].some(f => word(mem).test(sources[f]))) bad(doc, '`' + t + '`: no ' + mem + ' on ' + head);
      if (top[head] && declared[head].size === 1 && !fileOf.has(head) && !new RegExp('`' + head + '(\\.\\w+)?` (\\(`?\\w+\\.cs`?\\)|у `\\w+\\.cs`)').test(text)) {
        const home = [...top[head]][0];
        bad(doc, '`' + t + '`: ' + head + ' lives in ' + home + ': write it once here as `' + head + '` (' + home + ')');
      }
    }
  }

  // arch-file: the other way round. Every file of src/ is named in the map or one of its sections, so «which section is my file's» is
  // one search for its name (a file with no row of its own stands in docs/arch/files.md, on its section's line). Named: its name inside
  // back-ticks (alone, with .cs, or as the class of a member), as the home of a class — `NpcClock` (NpcPerf.cs) — or by a back-ticked
  // pattern with * that fits it (`RavenBook*`, `*Patterns.cs`; a pattern of data ids — `you_*` — starts small and is not one of these).
  // A pattern that fits no file is a pointer to nothing.
  const named = new Set(), patterns = [];
  for (const doc of mapFiles) {
    if (!fs.existsSync(doc)) continue;
    const text = fs.readFileSync(doc, 'utf8');
    for (const m of text.matchAll(/\((\w+)\.cs\)/g)) named.add(m[1]);
    for (const m of text.matchAll(/`([^`\n]+)`/g)) {
      const t = m[1];
      if (/^\*?[A-Z][\w*]*(\.cs)?$/.test(t) && t.includes('*') && t.replace(/\.cs$/, '').replace(/\*/g, '').length >= 4) {
        const p = new RegExp('^' + t.replace(/\.cs$/, '').replace(/\*/g, '\\w*') + '$');
        if (![...fileOf].some(f => p.test(f))) bad(doc, '`' + t + '` fits no file of src/');
        patterns.push(p);
      }
      for (const w of t.match(/[A-Za-z_]\w*/g) || []) named.add(w);
    }
  }
  for (const f of fileOf) if (!named.has(f) && !patterns.some(p => p.test(f))) {
    found++;
    console.log('ARCH arch-file: src/' + f + '.cs is named in no section of docs/arch: give it a row in its section, or a place on its section\'s line in docs/arch/files.md');
  }
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

// babble-copy: the author's tool plays the people's wordless talk outside the game (node tools/sound.js talk: tools/sound_talk.py), a copy
// of NpcBabble's «line's rules» in Python. A copy that drifts makes the page lie about the game (the user 2026-11-16: «інструмент +
// перевірку»): the rules as they act (comments and spaces out) must have the mark the copy says it plays (its RULES_OF)
{
  const cs = fs.readFileSync(path.join(src, 'NpcBabble.cs'), 'utf8');
  const a = cs.indexOf('// ---------------------------------------------------------------- the line\'s rules');
  const b = cs.indexOf('// ---------------------------------------------------------------- end of the line\'s rules');
  const py = fs.readFileSync(path.join(__dirname, 'sound_talk.py'), 'utf8').match(/^RULES_OF = '([0-9a-f]*)'/m);
  if (a < 0 || b < a) { found++; console.log('ARCH babble-copy: src/NpcBabble.cs has lost its marks «the line\'s rules» … «end of the line\'s rules» (tools/sound_talk.py copies what is between them)'); }
  else {
    const mark = require('crypto').createHash('sha1').update(cs.slice(a, b).replace(/\/\/[^\n]*/g, '').replace(/\s+/g, ''), 'utf8').digest('hex').slice(0, 16);
    if (!py || py[1] !== mark) { found++; console.log('ARCH babble-copy: the rules of a hummed line in src/NpcBabble.cs changed (' + mark + '), tools/sound_talk.py plays ' + (py ? py[1] : 'none') + '\n  → make the copy (class Rules) do what the game now does, then node tools/sound.js talk --mark'); }
  }
}

if (found) { console.log('\n' + found + ' against the core\'s rules: docs/ARCHITECTURE.md. Fix it in the core; an exception only with the user\'s consent.'); process.exit(1); }
console.log('arch_check: ok (old debt: ' + total(base) + ')');

// The one tool for our clips and scenes (review 2 2026-11-10, Г10: four entries with four languages — raven stage, request.txt, take.js, clip.js —
// were too many for a model to hold). Its words below; the stage's console words and studio/request.txt stay underneath (take.js speaks them).
//   node tools/anim.js new <clip> --tone plain|loose|merry|drinks [--body heid|player|dvergr|any] [--length 4] [--prop <item>] "<say: the user's words>"
//                                                  a new clip's frame: head (curve ease, fade, pivot), say, that body's stance at 0 and at the end (feet),
//                                                  @Look ahead, a draft row in docs/CLIPS.md with the body and the thing a take puts on the stage;
//                                                  it passes clipcheck as it is (Г11)
//   node tools/anim.js do <clip> "<t0>-<t1> <movement> …; …"   the clip written by movements, not keys (tools/motion.js): reach, rest, look, nod, shake,
//                                                  lean, turn, sink, weight, step, sigh, fingers — the keys and the rest of the body are made from them;
//                                                  undo <clip> <n|all>, redo <clip> <n> <movement>, alive <clip> [0..2] (how much the body goes with the
//                                                  hands), auto <clip> <channel…> (a channel keyed by hand back to the script)
//   node tools/anim.js mirror <to> <from>          the same clip for the other side · check <clip>: what the keys alone say before a take
//   node tools/anim.js find <words>                the catalog's rows with those words: a pose like the one wanted, before making one
//   node tools/anim.js show <clip> [channel]       its keys, a channel a line
//   node tools/anim.js at <clip> <t>               the pose at that moment: every channel's value (straight between its keys; the game eases), the wants that hold then
//   node tools/anim.js set|del|move|stretch|copy|header|unwant|unline|batch|shift|hold <clip> …   a change (clip.js: checked, a bad one put back)
//   node tools/anim.js grip <clip> [left|right] [--search] [--write]   a hand on a thing reckoned from the last take's grip sheet (tools/grip.js): every digit
//                                                  against the thing's surface as the file is, then the wrist's place and each finger's bend that lay the hand on it
//   node tools/anim.js diff <clip>                 the file against its last take on disk (player_anims_takes), key by key: what the last change did
//   node tools/anim.js clip <clip> [views]         a take on the stage: measured and filmed in one run (take.js: the report, the key sheet, the diff sheet)
//   node tools/anim.js views <clip> <views>        … and sheets of the whole run from those views
//   node tools/anim.js report <clip>               the last report again (only of the file as it is now)
//   node tools/anim.js fix <clip> [n …|list]       the report's «fix:» commands run as they stand (all, or those numbered)
//   node tools/anim.js done <clip>                 the user approved it: its frames and report go
//   node tools/anim.js scene <scene> [fps] [from]  the scene dry (the time table from the file), then played and checked at its place in the game
//   node tools/anim.js scene --dry <scene>         the dry run alone, no game (the time table, the shots, the warnings)
//   node tools/anim.js scene --cover <scene>       camera lines proposed for its talk (both, over the shoulder, close): nothing written
//   node tools/anim.js scene --sound <scene>       sound lines proposed for it (the place's bed, the world down under a talk, the ear for a wide
//                                                  shot, a thing that falls, the clips with no sound of their own): nothing written
//   node tools/anim.js approved [clip …] [--save]  every accepted clip (or those) taken twice: what fires on each, steady or not, its key sheet —
//                                                  studio/out/approved_review.txt, for the user to judge (nothing hidden); numbers against tools/clip_numbers.json
//   node tools/anim.js status                      one line: the game answers or not, a scene playing, the stage (up, on what, playing what), the studio's last jobs
// The studio's folder: tools/studio.path (one line; the game's profile config/RavenWatch/studio).
const fs = require('fs'), path = require('path');
const clip = require('./clip.js'), take = require('./take.js');
const CLIP = ['new', 'find', 'show', 'at', 'do', 'undo', 'redo', 'alive', 'auto', 'mirror', 'check', 'set', 'del', 'move', 'stretch', 'copy', 'header', 'unwant', 'unline', 'batch', 'shift', 'hold', 'diff'];
const TAKE = ['clip', 'views', 'report', 'fix', 'done', 'scene', 'approved', 'status'];
const [cmd, ...a] = process.argv.slice(2);
// help [do|keys|take]: the heads of the tool's own files, as they stand (one place for what a verb does: the head; never a second list here)
function help(what) {
  const head = file => { const out = []; for (const l of fs.readFileSync(path.join(__dirname, file), 'utf8').split('\n')) { if (!l.startsWith('//')) break; out.push(l.replace(/^\/\/ ?/, '')); } return out.join('\n'); };
  console.log(what === 'do' ? head('motion.js') : what === 'keys' ? head('clip.js') : what === 'take' ? head('take.js') : head('anim.js') + '\n\nmore: node tools/anim.js help do (the movements) | help keys (set, move, copy, batch …) | help take (clip, fix, scene, status)');
}
try {
  if (cmd === 'help' || cmd === '--help' || cmd === '-h') { help(a[0]); process.exit(0); }
  if (cmd === 'grip') require('./grip.js').run(a);
  else if (CLIP.includes(cmd)) clip.run(cmd, a);
  else if (TAKE.includes(cmd)) take.run(cmd, a);
  else throw new Error('node tools/anim.js new|find|show|at|do|undo|redo|alive|auto|mirror|check|set|del|move|stretch|copy|header|unwant|unline|batch|shift|hold|diff|grip <clip> … | clip|views|report|fix|done <clip> | scene [--dry|--cover|--sound] <scene> | approved [clip …] | status — node tools/anim.js help says each, with an example');
} catch (e) { console.error('anim: ' + e.message); process.exit(1); }

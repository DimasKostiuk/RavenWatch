// A dungeon of ours put together on node, before the game, from the game's own rooms (the crypt of chapter 1 first; any theme the same way).
// The game builds a dungeon of whole rooms joined way to way (DungeonGenerator); a dungeon once made is never generated again: every game
// reads the list «room, place, turn» off its object and sets the rooms' walls itself, with none of the rooms' own things (chests, spawners,
// doors). So a list written by us is the layout, the same in every world, and what stands in it is only what we put (docs/plans/story-lore.md,
// «Склеп Чорного лісу»). This tool makes that list from a short file and says what is wrong with it.
//   node tools/rooms.js kinds [theme] [room]        the room kinds of a theme (tools/rooms/kinds_<theme>.txt): size, ways by number, floor
//   node tools/rooms.js build <name>                tools/rooms/<name>.rooms.txt → assets/story/rooms/<name>.txt (every place in the dungeon's
//                                                   measure: what the mod reads, LocInside), tools/rooms/<name>.svg (the plan from above),
//                                                   and the report
//   node tools/rooms.js take <dump> <n> <name>      dungeon number n (from 1) of a «raven story rooms» file → <name>.rooms.txt with every
//                                                   room at its place: a real dungeon of the game as the start of ours
//   node tools/rooms.js verify <dump>               the tool's own sums against real dungeons: every joined pair of rooms set anew through
//                                                   its way must land where the game put it, and the print must be the game's
// Where the kinds come from: in game «raven story rooms kinds <theme>» (src/LocRooms.cs) writes config/RavenWatch/blueprints/rooms/
// kinds_<theme>.txt; its copy lies in tools/rooms. A new version of the game: take it again.
//
// The file <name>.rooms.txt, a line a thing (# a note):
//   theme: ForestCrypt
//   air: Crypt                                      the game's environment inside (light, fog, sound); Crypt when not said
//   room <id> <kind>                                the first one: the way in (an entrance kind), set as the game sets it — its way in at
//                                                   the dungeon's own point, looking along its forward
//   room <id> <kind> from <id>.<n> [by <m>]         joined to way n of a room already there by its own way m (by may be left out when the
//                                                   kind has one way); the numbers are those of «kinds»
//   room <id> <kind> at <x> <y> <z> turn <deg>      at a place of the dungeon's measure (what «take» writes)
//   cap <kind>                                      the endcap that shuts every way left open (none said: an open way is an error)
//   cap <id>.<n> <kind>                             this one for that way — the way in too: the wall behind the way out (without it the way
//                                                   in is a hole into nothing; the game's own crypts shut it with their gate)
//   space <id> from <id>.<n> <wide> <high> <long> [down <m>]   an empty box of ours beyond that way, for the author to build in with the
//                                                   hammer (no walls, no floor: the game puts nothing there): so wide, so high, so long
//                                                   out of the way, its bottom <down> (3) m under the way's floor. The way stays open into it.
//                                                   Building is allowed and «raven story rooms keep» takes pieces only in the rooms and spaces
//   door <id>.<n> <prefab>                          a door of the game in that way (the game puts its own there by chance)
//   thing <prefab> <id> <x> <y> <z> [turn <deg>]    a thing of the game in a room, in the room's measure (x right, y up, z forward)
//   fill <id> <word>[!] …                           the game's own things of that room's kind, each at the place (and turn) the game keeps
//                                                   it: every thing whose prefab has one of the words in its name («torch», «chest»,
//                                                   «mushroom», «root», «random», «surtling», «skull», «remains», «spawner», «bonepile»…).
//                                                   The game puts most of them by chance: so does this, by a draw of its own that is the same
//                                                   at every build (the layout's name, the room, the thing's number) — «word!» puts every one.
//                                                   A kinds file written before things had «facing» gives no turn: said, and they stand unturned
//   point <name> <id> <x> <y> <z> [turn <deg>]      a named point of ours there: a scene's place, a camera's aim
//   point <name> at <x> <y> <z> [turn <deg>]        the same by the dungeon's own measure (in a space, where no room is: the numbers of
//                                                   a piece's line in <name>.own.txt are these)
// The measure: metres; the dungeon's x right, y up, z forward of its own point; a turn is degrees clockwise from above, as the game's.
// What it checks: a kind that is not there; a way joined twice or to nothing; two rooms through one another (their boxes, as the game
// tests a new room: endcaps are not tested); a way left open with no cap; a thing or a point outside its room's box; how far the whole
// reaches (over 64 m it leaves the game's own inside box: said, not forbidden — our dungeon brings its own air).
// «print» is the same number «raven story rooms» says of a dungeon standing in a world: the same print, the same layout there.
const fs = require('fs'), path = require('path');
const dir = path.join(__dirname, 'rooms'), outDir = path.join(__dirname, '..', 'assets', 'story', 'rooms');
const die = m => { console.error('rooms: ' + m); process.exit(1); };
const num = s => { const v = Number(s); if (!isFinite(v)) die('not a number: «' + s + '»'); return v; };
const F = v => { let r = Math.round(v * 10) / 10; if (r === 0) r = 0; return String(r); };            // (as LocRooms.F: to 0.1, no «-0»)
const V = p => F(p[0]) + ' ' + F(p[1]) + ' ' + F(p[2]);
const deg = y => { const d = Math.round(((y % 360) + 360) % 360); return d === 360 ? 0 : d; };
// a turn round y, the game's way (clockwise from above): forward (0 0 1) turned 90 looks along x
const rot = (yaw, p) => { const a = yaw * Math.PI / 180, c = Math.cos(a), s = Math.sin(a); return [p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c]; };
const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]], sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const dist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
const Joined = 0.1;   // m: two ways this near are one way through (the game's RoomConnection.TestContact; LocRooms.Joined)
const Inside = 0.2;   // m: boxes may go this far into one another and still only touch
const InRoom = 0.5;   // m: a thing this far outside its room's box is still its own (LocRooms.InRoom)
const Box = 64;       // m: the game's own inside box of a dungeon

// the game's string hash (StringExtensionMethods.GetStableHashCode), for the print
function stableHash(s) {
  let a = 5381, b = 5381;
  for (let i = 0; i < s.length; i += 2) {
    a = (((a << 5) + a) ^ s.charCodeAt(i)) | 0;
    if (i === s.length - 1) break;
    b = (((b << 5) + b) ^ s.charCodeAt(i + 1)) | 0;
  }
  return (a + Math.imul(b, 1566083941)) | 0;
}
const printOf = rooms => (stableHash(rooms.map(r => r.kind + '|' + V(r.at) + '|' + deg(r.turn)).sort().join('\n')) >>> 0).toString(16).padStart(8, '0');

// ---------------------------------------------------------------- the kinds, as the game wrote them
function readKinds(theme) {
  const f = path.join(dir, 'kinds_' + theme + '.txt');
  if (!fs.existsSync(f)) die('no ' + f + ' (in game: raven story rooms kinds ' + theme + ', then copy the file here)');
  const kinds = new Map(); let k = null;
  for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
    let m = line.match(/^\[room (\S+)\]\s+(\S+).*?size ([\d.]+)x([\d.]+)x([\d.]+)/);
    if (m) { k = { name: m[1], is: m[2], size: [num(m[3]), num(m[4]), num(m[5])], ways: [], things: [] }; kinds.set(k.name.toLowerCase(), k); continue; }
    if (!k) continue;
    m = line.match(/^\s+way: \S+( \(the way in\))?.* at (\S+) (\S+) (\S+) facing (\d+)/);
    if (m) { k.ways.push({ in: !!m[1], at: [num(m[2]), num(m[3]), num(m[4])], facing: num(m[5]) }); continue; }
    m = line.match(/^\s+thing: (\S+)( \([^)]*\))? at (\S+) (\S+) (\S+)(?: facing (\d+)°)?(?:\s+chance (\d+)%)?/);
    if (m) k.things.push({ prefab: m[1], is: (m[2] || '').trim(), at: [num(m[3]), num(m[4]), num(m[5])], facing: m[6] === undefined ? null : num(m[6]), chance: m[7] ? num(m[7]) : 100 });
  }
  if (kinds.size === 0) die('no room read from ' + f);
  return kinds;
}
const floorOf = k => k.ways.length ? Math.min(...k.ways.map(w => w.at[1])) : -k.size[1] / 2; // (a way stands on the floor: the lowest is the floor)

function cmdKinds(theme, one) {
  const kinds = readKinds(theme || 'ForestCrypt');
  for (const k of kinds.values()) {
    if (one && k.name.toLowerCase() !== one.toLowerCase()) continue;
    console.log(k.name + '  ' + k.is + '  ' + k.size.join('x') + ' m (x, y, z)  floor at y ' + F(floorOf(k)));
    k.ways.forEach((w, i) => console.log('   way ' + (i + 1) + (w.in ? ' (the way in)' : '') + ': at ' + V(w.at) + ', looks ' + w.facing + '°'));
    if (one) for (const t of k.things) console.log('   the game puts: ' + t.prefab + (t.is ? ' ' + t.is : '') + ' at ' + V(t.at) + (t.chance < 100 ? '  ' + t.chance + '%' : ''));
  }
}

// ---------------------------------------------------------------- one layout
// a room set so that its way m lies on a way of the dungeon's measure that looks <yaw> (DungeonGenerator.CalculateRoomPosRot)
function through(kind, m, at, yaw) {
  const w = kind.ways[m], turn = yaw + 180 - w.facing;
  return { at: sub(at, rot(turn, w.at)), turn: deg(turn) };
}
const wayAt = (r, i) => add(r.at, rot(r.turn, r.k.ways[i].at));
const wayYaw = (r, i) => deg(r.turn + r.k.ways[i].facing);

function readLayout(name) {
  const f = path.join(dir, name + '.rooms.txt');
  if (!fs.existsSync(f)) die('no ' + f);
  const L = { name, theme: 'ForestCrypt', air: 'Crypt', rooms: [], capAll: null, caps: [], spaces: [], doors: [], things: [], points: [], errors: [], warns: [] };
  const lines = fs.readFileSync(f, 'utf8').split(/\r?\n/).map((l, i) => ({ n: i + 1, w: l.replace(/\s+#.*$/, '').replace(/^#.*$/, '').trim().split(/\s+/) })).filter(l => l.w[0]);
  const th = lines.find(l => l.w[0] === 'theme:'); if (th) L.theme = th.w[1];
  const air = lines.find(l => l.w[0] === 'air:'); if (air) L.air = air.w[1];
  const kinds = L.kinds = readKinds(L.theme);
  const err = (l, m) => L.errors.push('line ' + l.n + ': ' + m);
  const kindOf = (l, s) => { const k = kinds.get((s || '').toLowerCase()); if (!k) err(l, 'no room kind «' + s + '» in ' + L.theme + ' (node tools/rooms.js kinds)'); return k; };
  const byId = id => L.rooms.find(r => r.id === id);
  const wayOf = (l, s) => { // «id.n» → { r, i }
    const m = /^([^.]+)\.(\d+)$/.exec(s || ''); if (!m) { err(l, 'a way is written «room.number»: «' + s + '»'); return null; }
    const r = byId(m[1]); if (!r) { err(l, 'no room «' + m[1] + '» above this line'); return null; }
    const i = +m[2] - 1; if (!r.k.ways[i]) { err(l, r.id + ' (' + r.k.name + ') has ' + r.k.ways.length + ' ways, not ' + m[2]); return null; }
    return { r, i };
  };
  const spot = (l, a, from) => { // «<id> x y z [turn d]» → a place in the dungeon's measure
    const r = byId(a[from]); if (!r) { err(l, 'no room «' + a[from] + '»'); return null; }
    if (a.length < from + 4) { err(l, 'a place is «room x y z»'); return null; }
    const p = [num(a[from + 1]), num(a[from + 2]), num(a[from + 3])], t = a[from + 4] === 'turn' ? num(a[from + 5]) : 0, s = r.k.size;
    if (Math.abs(p[0]) > s[0] / 2 + InRoom || Math.abs(p[1]) > s[1] / 2 + InRoom || Math.abs(p[2]) > s[2] / 2 + InRoom) err(l, V(p) + ' is outside ' + r.id + ' (' + s.join('x') + ' m round its middle)');
    if (p[1] < floorOf(r.k) - 0.3) L.warns.push('line ' + l.n + ': ' + V(p) + ' is under the floor of ' + r.id + ' (its floor by the ways: y ' + F(floorOf(r.k)) + ')');
    return { room: r.id, at: add(r.at, rot(r.turn, p)), turn: deg(r.turn + t) };
  };
  for (const l of lines) {
    const a = l.w;
    if (a[0] === 'theme:' || a[0] === 'air:') continue;
    if (a[0] === 'room') {
      const k = kindOf(l, a[2]); if (!k) continue;
      if (byId(a[1])) { err(l, 'a second room «' + a[1] + '»'); continue; }
      const r = { id: a[1], kind: k.name, k, cap: false };
      if (a[3] === 'at') { r.at = [num(a[4]), num(a[5]), num(a[6])]; r.turn = deg(a[7] === 'turn' ? num(a[8]) : 0); if (r.turn % 90) err(l, 'a turn is 0, 90, 180 or 270'); }
      else if (a[3] === 'from') {
        const w = wayOf(l, a[4]); if (!w) continue;
        let m = a[5] === 'by' ? +a[6] - 1 : (k.ways.length === 1 ? 0 : -1);
        if (!k.ways[m]) { err(l, k.name + ' has ' + k.ways.length + ' ways: say which joins («by <number>»)'); continue; }
        Object.assign(r, through(k, m, wayAt(w.r, w.i), wayYaw(w.r, w.i)));
      }
      else if (a.length === 3 && L.rooms.length === 0) {
        const m = k.ways.findIndex(w => w.in); if (m < 0) { err(l, 'the first room is the way in: ' + k.name + ' is no entrance'); continue; }
        r.turn = deg(-k.ways[m].facing); r.at = sub([0, 0, 0], rot(r.turn, k.ways[m].at)); // (PlaceStartRoom: its way in on the dungeon's point)
      }
      else { err(l, 'room <id> <kind> from <id>.<n> [by <m>]  |  at <x> <y> <z> turn <deg>'); continue; }
      L.rooms.push(r);
    }
    else if (a[0] === 'cap') {
      if (a.length === 2) { if (kindOf(l, a[1])) L.capAll = kinds.get(a[1].toLowerCase()); }
      else { const w = wayOf(l, a[1]), k = kindOf(l, a[2]); if (w && k) L.caps.push({ w, k, l }); }
    }
    else if (a[0] === 'space') {
      const w = a[2] === 'from' ? wayOf(l, a[3]) : null;
      if (!w || a.length < 7) { err(l, 'space <id> from <id>.<n> <wide> <high> <long> [down <m>]'); continue; }
      const wide = num(a[4]), high = num(a[5]), long = num(a[6]), down = a[7] === 'down' ? num(a[8]) : 3;
      const out = deg(wayYaw(w.r, w.i) + 180), at = add(wayAt(w.r, w.i), rot(out, [0, high / 2 - down, long / 2]));
      const h = out % 180 !== 0 ? [long / 2, high / 2, wide / 2] : [wide / 2, high / 2, long / 2];
      L.spaces.push({ id: a[1], w, at, turn: out, size: [wide, high, long], lo: sub(at, h), hi: add(at, h) });
    }
    else if (a[0] === 'door') { const w = wayOf(l, a[1]); if (w && a[2]) L.doors.push({ prefab: a[2], room: w.r.id, at: wayAt(w.r, w.i), turn: wayYaw(w.r, w.i) }); else if (w) err(l, 'door <id>.<n> <prefab>'); }
    else if (a[0] === 'fill') {
      const r = byId(a[1]); if (!r || a.length < 3) { err(l, r ? 'fill <id> <word>[!] …' : 'no room «' + a[1] + '» above this line'); continue; }
      const words = a.slice(2).map(w => ({ w: w.replace(/!$/, '').toLowerCase(), all: w.endsWith('!'), n: 0 }));
      r.k.things.forEach((t, i) => {
        const hit = words.find(w => t.prefab.toLowerCase().includes(w.w)); if (!hit) return;
        if (!hit.all && t.chance < 100 && (stableHash(name + '|' + r.id + '|' + i) >>> 0) % 100 >= t.chance) { hit.n += 0.001; return; }
        hit.n++;
        if (t.facing === null) L.unturned = (L.unturned || 0) + 1;
        L.things.push({ prefab: t.prefab, room: r.id, at: add(r.at, rot(r.turn, t.at)), turn: deg(r.turn + (t.facing || 0)) });
      });
      for (const w of words) if (w.n === 0) L.warns.push('line ' + l.n + ': ' + r.k.name + ' keeps nothing named «' + w.w + '» (node tools/rooms.js kinds ' + L.theme + ' ' + r.k.name + ')');
    }
    else if (a[0] === 'thing') { const s = spot(l, a, 2); if (s) L.things.push(Object.assign({ prefab: a[1] }, s)); }
    else if (a[0] === 'point' && a[2] === 'at') {
      if (a.length < 6) { err(l, 'point <name> at <x> <y> <z> [turn <deg>]'); continue; }
      if (L.points.some(p => p.name === a[1])) err(l, 'a second point «' + a[1] + '»');
      L.points.push({ name: a[1], room: '-', at: [num(a[3]), num(a[4]), num(a[5])], turn: deg(a[6] === 'turn' ? num(a[7]) : 0) });
    }
    else if (a[0] === 'point') { const s = spot(l, a, 2); if (s) { if (L.points.some(p => p.name === a[1])) err(l, 'a second point «' + a[1] + '»'); L.points.push(Object.assign({ name: a[1] }, s)); } }
    else err(l, 'unknown word «' + a[0] + '»');
  }
  return L;
}

// every way of every room: what it meets
function ways(rooms) {
  const all = [];
  for (const r of rooms) r.k.ways.forEach((w, i) => all.push({ r, i, at: wayAt(r, i), yaw: wayYaw(r, i), in: w.in, to: [] }));
  for (const a of all) for (const b of all) if (a.r !== b.r && dist(a.at, b.at) < Joined) a.to.push(b);
  return all;
}
function boxOf(r) { // its box in the dungeon's measure (turns are quarter turns)
  const q = deg(r.turn) % 180 !== 0, s = r.k.size, h = [q ? s[2] / 2 : s[0] / 2, s[1] / 2, q ? s[0] / 2 : s[2] / 2];
  return { lo: sub(r.at, h), hi: add(r.at, h) };
}
function overlaps(rooms) {
  const out = [], solid = rooms.filter(r => r.k.is !== 'endcap');
  for (let i = 0; i < solid.length; i++) for (let j = i + 1; j < solid.length; j++) {
    const a = boxOf(solid[i]), b = boxOf(solid[j]);
    const d = [0, 1, 2].map(n => Math.min(a.hi[n], b.hi[n]) - Math.max(a.lo[n], b.lo[n]));
    if (d.every(x => x > Inside)) out.push(solid[i].id + ' and ' + solid[j].id + ' go through one another by ' + d.map(F).join(' x ') + ' m');
  }
  return out;
}

function finish(L) {
  // the caps: the said ones, then the common one on every way still open
  let n = 0;
  const capOn = (w, k) => { const p = through(k, 0, wayAt(w.r, w.i), wayYaw(w.r, w.i)); L.rooms.push({ id: 'cap' + (++n), kind: k.name, k, cap: true, at: p.at, turn: p.turn }); };
  for (const c of L.caps) { if (c.k.is !== 'endcap') L.errors.push('line ' + c.l.n + ': ' + c.k.name + ' is no endcap'); capOn(c.w, c.k); }
  const intoSpace = w => L.spaces.some(s => s.w.r === w.r && s.w.i === w.i);
  if (L.capAll) for (const w of ways(L.rooms)) if (!w.in && w.to.length === 0 && !intoSpace(w)) capOn(w, L.capAll);
  for (const w of ways(L.rooms)) {
    const what = w.r.id + '.' + (w.i + 1);
    if (intoSpace(w)) { if (w.to.length) L.errors.push(what + ' opens into a space and a room stands in it'); continue; }
    if (w.in) { if (w.to.some(t => !t.r.cap)) L.errors.push(what + ' is the way in and a room stands in it (a cap said for it may: «cap ' + what + ' <kind>» — the wall behind the way out)'); continue; }
    if (w.to.length === 0) L.errors.push(what + ' is left open (a hole into nothing): join a room or say «cap <kind>»');
    else if (w.to.length > 1) L.errors.push(what + ' meets ' + w.to.length + ' ways at once');
    else if (deg(w.yaw - w.to[0].yaw) !== 180) L.errors.push(what + ' and ' + w.to[0].r.id + '.' + (w.to[0].i + 1) + ' meet but do not look at one another');
  }
  if (L.rooms.filter(r => r.k.ways.some(w => w.in)).length !== 1) L.errors.push('a dungeon has one way in (one entrance room)');
  for (const o of overlaps(L.rooms)) L.errors.push(o);
  for (const s of L.spaces) for (const r of L.rooms.filter(r => r.k.is !== 'endcap')) {
    const b = boxOf(r), d = [0, 1, 2].map(n => Math.min(s.hi[n], b.hi[n]) - Math.max(s.lo[n], b.lo[n]));
    if (d.every(x => x > Inside)) L.errors.push('the space ' + s.id + ' and ' + r.id + ' go through one another by ' + d.map(F).join(' x ') + ' m');
  }
  const bs = L.rooms.map(boxOf).concat(L.spaces), lo = [0, 1, 2].map(i => Math.min(...bs.map(b => b.lo[i]))), hi = [0, 1, 2].map(i => Math.max(...bs.map(b => b.hi[i])));
  L.lo = lo; L.hi = hi;
  if (hi[0] - lo[0] > Box || hi[2] - lo[2] > Box) L.warns.push('the whole is ' + F(hi[0] - lo[0]) + ' x ' + F(hi[2] - lo[2]) + ' m: wider than the game\'s own inside box (' + Box + ' m)');
}

function builtText(L) {
  const o = ['# ' + L.name + ' — made by «node tools/rooms.js build ' + L.name + '» from ' + L.name + '.rooms.txt: do not edit, build again.',
    '# Every place is in the dungeon\'s own measure (x right, y up, z forward of its point; a turn clockwise from above).',
    'theme: ' + L.theme, 'air: ' + L.air, 'print: ' + printOf(L.rooms)];
  for (const r of L.rooms) o.push('room ' + r.id + ' ' + r.kind + ' ' + V(r.at) + ' ' + deg(r.turn));
  for (const s of L.spaces) o.push('space ' + s.id + ' ' + V(s.at) + ' ' + s.turn + ' ' + s.size.join(' '));   // (its middle, its turn, wide high long)
  for (const d of L.doors) o.push('door ' + d.prefab + ' ' + V(d.at) + ' ' + d.turn);
  for (const t of L.things) o.push('thing ' + t.prefab + ' ' + V(t.at) + ' ' + t.turn + ' ' + t.room);
  for (const p of L.points) o.push('point ' + p.name + ' ' + V(p.at) + ' ' + p.turn + ' ' + p.room);
  return o.join('\n') + '\n';
}

// the plan from above: z up the page, x to the right
function svg(L) {
  const S = 14, pad = 3, X = x => (x - L.lo[0] + pad) * S, Y = z => (L.hi[2] - z + pad) * S;
  const W = (L.hi[0] - L.lo[0] + pad * 2) * S, H = (L.hi[2] - L.lo[2] + pad * 2) * S;
  const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;');
  const o = ['<svg xmlns="http://www.w3.org/2000/svg" width="' + W + '" height="' + (H + 30) + '" font-family="sans-serif" font-size="11">',
    '<rect width="100%" height="100%" fill="#1d1f21"/>',
    '<text x="8" y="16" fill="#ddd">' + esc(L.name) + ' · ' + L.theme + ' · ' + L.rooms.filter(r => !r.cap).length + ' rooms + ' + L.rooms.filter(r => r.cap).length + ' caps · ' + F(L.hi[0] - L.lo[0]) + ' x ' + F(L.hi[2] - L.lo[2]) + ' m · 1 square = 1 m</text>'];
  for (const r of L.rooms) {
    const b = boxOf(r), ent = r.k.ways.some(w => w.in);
    const d = Math.max(b.hi[2] - b.lo[2], 0.4), x = X(b.lo[0]), y = Y(b.lo[2] + d) + 24;
    o.push('<rect x="' + x + '" y="' + y + '" width="' + (b.hi[0] - b.lo[0]) * S + '" height="' + d * S + '" fill="' + (r.cap ? '#555' : ent ? '#3b5a3b' : '#39424e') + '" stroke="#9aa" stroke-width="1"/>');
    if (!r.cap) o.push('<text x="' + (X(r.at[0])) + '" y="' + (Y(r.at[2]) + 24) + '" fill="#fff" text-anchor="middle">' + esc(r.id) + '</text>',
      '<text x="' + (X(r.at[0])) + '" y="' + (Y(r.at[2]) + 37) + '" fill="#9aa" text-anchor="middle" font-size="9">' + esc(r.kind.replace(/^forestcrypt_(new_)?/i, '')) + ' · floor ' + F(r.at[1] + floorOf(r.k)) + '</text>');
  }
  for (const s of L.spaces) o.push('<rect x="' + X(s.lo[0]) + '" y="' + (Y(s.hi[2]) + 24) + '" width="' + (s.hi[0] - s.lo[0]) * S + '" height="' + (s.hi[2] - s.lo[2]) * S + '" fill="none" stroke="#6cf" stroke-dasharray="6 4"/>',
    '<text x="' + X(s.at[0]) + '" y="' + (Y(s.at[2]) + 24) + '" fill="#6cf" text-anchor="middle">' + esc(s.id) + ' (empty, ' + s.size.join('x') + ')</text>');
  for (const w of ways(L.rooms)) if (!w.r.cap) o.push('<circle cx="' + X(w.at[0]) + '" cy="' + (Y(w.at[2]) + 24) + '" r="4" fill="' + (w.in ? '#6f6' : w.to.length ? '#fc6' : '#f55') + '"/>',
    '<text x="' + (X(w.at[0]) + 6) + '" y="' + (Y(w.at[2]) + 20) + '" fill="#fc6" font-size="9">' + (w.in ? 'in' : w.i + 1) + '</text>');
  for (const d of L.doors) o.push('<rect x="' + (X(d.at[0]) - 6) + '" y="' + (Y(d.at[2]) + 21) + '" width="12" height="6" fill="#a63"/>');
  for (const t of L.things) o.push('<rect x="' + (X(t.at[0]) - 3) + '" y="' + (Y(t.at[2]) + 21) + '" width="6" height="6" fill="#6cf"/>', '<text x="' + (X(t.at[0]) + 6) + '" y="' + (Y(t.at[2]) + 28) + '" fill="#6cf" font-size="9">' + esc(t.prefab) + '</text>');
  for (const p of L.points) o.push('<path d="M' + (X(p.at[0]) - 5) + ' ' + (Y(p.at[2]) + 24) + 'h10M' + X(p.at[0]) + ' ' + (Y(p.at[2]) + 19) + 'v10" stroke="#f6c" stroke-width="2"/>', '<text x="' + (X(p.at[0]) + 7) + '" y="' + (Y(p.at[2]) + 20) + '" fill="#f6c" font-size="10">' + esc(p.name) + '</text>');
  return o.join('\n') + '\n</svg>\n';
}

function cmdBuild(name) {
  if (!name) die('build <name>   (tools/rooms/<name>.rooms.txt)');
  const L = readLayout(name);
  if (L.errors.length === 0) finish(L);
  if (L.unturned) L.warns.push(L.unturned + ' things of «fill» stand unturned: the kinds file has no «facing» for things (in game: raven story rooms kinds ' + L.theme + ', then build again)');
  for (const w of L.warns) console.log('  note: ' + w);
  if (L.errors.length) { for (const e of L.errors) console.log('  WRONG: ' + e); console.log('rooms: ' + name + ' NOT built: ' + L.errors.length + ' wrong'); process.exit(1); }
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(path.join(outDir, name + '.txt'), builtText(L));
  fs.writeFileSync(path.join(dir, name + '.svg'), svg(L));
  for (const r of L.rooms.filter(r => !r.cap)) console.log('  ' + r.id.padEnd(10) + r.kind.padEnd(36) + 'at ' + V(r.at).padEnd(16) + 'turn ' + String(deg(r.turn)).padEnd(4) + 'floor y ' + F(r.at[1] + floorOf(r.k)));
  console.log('rooms: ' + name + ' built: ' + L.rooms.filter(r => !r.cap).length + ' rooms, ' + L.rooms.filter(r => r.cap).length + ' caps, ' + L.spaces.length + ' spaces, ' + L.doors.length + ' doors, ' + L.things.length + ' things, ' + L.points.length + ' points; '
    + F(L.hi[0] - L.lo[0]) + ' x ' + F(L.hi[2] - L.lo[2]) + ' m, ' + F(L.hi[1] - L.lo[1]) + ' m high; print ' + printOf(L.rooms));
  console.log('  → ' + path.join(outDir, name + '.txt') + '\n  → ' + path.join(dir, name + '.svg'));
}

// ---------------------------------------------------------------- a real dungeon out of «raven story rooms»
function readDump(file) {
  if (!fs.existsSync(file)) die('no ' + file);
  const out = []; let d = null;
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    let m = line.match(/^\[dungeon (\S+)\]/); if (m) { d = { prefab: m[1], theme: '', rooms: [], print: '', where: '' }; out.push(d); continue; }
    if (!d) continue;
    if ((m = line.match(/^themes: ([^;]+)/))) d.theme = m[1].trim();
    else if ((m = line.match(/^at: (.*)/))) d.where = m[1];
    else if ((m = line.match(/^print: (\S+)/))) d.print = m[1];
    else if ((m = line.match(/^  (\d+) (\S+)\s+\S+\s+at (\S+) (\S+) (\S+)\s+turned (\d+)/))) d.rooms.push({ id: 'r' + m[1], kind: m[2], at: [num(m[3]), num(m[4]), num(m[5])], turn: num(m[6]) });
  }
  return out;
}

function cmdTake(file, n, name) {
  if (!file || !n || !name) die('take <dump file> <n> <name>');
  const d = readDump(file)[+n - 1]; if (!d) die('the file has no dungeon number ' + n);
  const o = ['# ' + name + ' — a dungeon of the game taken as it stood (' + d.prefab + ', ' + d.where + '), print ' + d.print + '.', '# Change it, then: node tools/rooms.js build ' + name, 'theme: ' + d.theme];
  for (const r of d.rooms) o.push('room ' + r.id + ' ' + r.kind + ' at ' + V(r.at) + ' turn ' + r.turn);
  const f = path.join(dir, name + '.rooms.txt'); if (fs.existsSync(f)) die(f + ' is there already');
  fs.writeFileSync(f, o.join('\n') + '\n'); console.log('rooms: ' + d.rooms.length + ' rooms → ' + f);
}

function cmdVerify(file) {
  if (!file) die('verify <dump file>');
  let bad = 0;
  for (const d of readDump(file)) {
    const kinds = readKinds(d.theme);
    for (const r of d.rooms) { r.k = kinds.get(r.kind.toLowerCase()); if (!r.k) die('the dump has a room the kinds file does not: ' + r.kind); }
    const all = ways(d.rooms); let pairs = 0, off = 0, open = 0;
    for (const w of all) {
      if (w.in) continue;
      if (w.to.length !== 1) { open++; continue; }
      const t = w.to[0], p = through(t.r.k, t.i, w.at, w.yaw); pairs++;
      if (dist(p.at, t.r.at) > 0.05 || deg(p.turn) !== deg(t.r.turn)) { off++; console.log('  OFF: ' + t.r.id + ' set through ' + w.r.id + '.' + (w.i + 1) + ' lands at ' + V(p.at) + ' turn ' + p.turn + ', the game has ' + V(t.r.at) + ' turn ' + t.r.turn); }
    }
    const print = printOf(d.rooms), ov = overlaps(d.rooms);
    for (const o of ov) console.log('  boxes: ' + o);
    console.log(d.prefab + ': ' + d.rooms.length + ' rooms, ' + pairs + ' joined ways set anew, ' + off + ' off; ways not joined one to one: ' + open + '; boxes through one another: ' + ov.length + '; print ' + print + (print === d.print ? ' = the game\'s' : ' NOT the game\'s ' + d.print));
    if (off || open || ov.length || print !== d.print) bad++;
  }
  console.log(bad ? 'rooms: verify FAILED for ' + bad + ' dungeon(s)' : 'rooms: verify passed');
  process.exit(bad ? 1 : 0);
}

const a = process.argv.slice(2);
if (a[0] === 'kinds') cmdKinds(a[1], a[2]);
else if (a[0] === 'build') cmdBuild(a[1]);
else if (a[0] === 'take') cmdTake(a[1], a[2], a[3]);
else if (a[0] === 'verify') cmdVerify(a[1]);
else console.log('node tools/rooms.js kinds [theme] [room] · build <name> · take <dump> <n> <name> · verify <dump>   (the head of tools/rooms.js says each)');

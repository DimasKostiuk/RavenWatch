// Swaps the pictures embedded in a binary FBX (Video → Content) for smaller ones, so a model built into the mod
// does not swell the DLL. The rest of the file is copied byte for byte; only the node sizes are counted anew.
//   node tools/fbx_retex.js <in.fbx> <out.fbx> <video name part>=<picture> [...]
//   e.g. node tools/fbx_retex.js big.fbx assets/fbx/RavenNest.fbx Image_0=diffuse.jpg Image_2=normal.jpg
// A video is matched by its name or RelativeFilename containing the part. Videos left unnamed lose nothing, unless
// --drop-others: their pictures go (roughness and metallic maps, which our loader does not use).
// (Our loader, WsAssets.FbxLook, reads PNG and JPG alike.)
const fs = require("fs");
const [, , inPath, outPath, ...swaps] = process.argv;
if (!inPath || !outPath) { console.log("node tools/fbx_retex.js <in.fbx> <out.fbx> Image_0=diffuse.jpg ..."); process.exit(1); }
const dropOthers = swaps.includes("--drop-others");
const want = swaps.filter(s => s !== "--drop-others").map(s => { const i = s.indexOf("="); return { part: s.slice(0, i), file: s.slice(i + 1), used: false }; });

const b = fs.readFileSync(inPath);
if (b.toString("ascii", 0, 18) !== "Kaydara FBX Binary") throw new Error("not a binary FBX");
const ver = b.readUInt32LE(23), big = ver >= 7500, head = big ? 25 : 13;

// one node: its name, its properties as raw bytes (copied as they are), its children
function read(pos) {
  const end = big ? Number(b.readBigUInt64LE(pos)) : b.readUInt32LE(pos);
  const count = big ? Number(b.readBigUInt64LE(pos + 8)) : b.readUInt32LE(pos + 4);
  const listLen = big ? Number(b.readBigUInt64LE(pos + 16)) : b.readUInt32LE(pos + 8);
  const nameLen = b[pos + head - 1];
  if (end === 0) return null;
  const name = b.toString("ascii", pos + head, pos + head + nameLen);
  const propsAt = pos + head + nameLen;
  const props = b.subarray(propsAt, propsAt + listLen);
  const n = { name, count, props, kids: [], end };
  let p = propsAt + listLen;
  while (p < end) { const k = read(p); if (!k) { p += head; break; } n.kids.push(k); p = k.end; }
  n.hadNull = n.kids.length > 0 || p < end; // a node with children closes with a null record
  return n;
}

// the first string property (a video's name), and a child's string (RelativeFilename)
function str(props, at) { if (props[at] !== 0x53) return ""; const l = props.readInt32LE(at + 1); return props.toString("utf8", at + 5, at + 5 + l); }
function kidStr(n, name) { const k = n.kids.find(x => x.name === name); return k ? str(k.props, 0) : ""; }
function strAfterFirst(props) { // props[0] is the id (L, 9 bytes), then the name (S)
  return props[0] === 0x4C ? str(props, 9) : str(props, 0);
}

const top = [];
let pos = 27;
while (pos < b.length - head) { const n = read(pos); if (!n) break; top.push(n); pos = n.end; }

// the swaps: a Video whose name or file has the part gets the new picture as its Content (a raw 'R' property)
function swap(n) {
  if (n.name === "Video") {
    const label = strAfterFirst(n.props) + " " + kidStr(n, "RelativeFilename") + " " + kidStr(n, "Filename");
    const w = want.find(x => label.includes(x.part));
    const c = n.kids.find(k => k.name === "Content");
    if (w && c) {
      const data = fs.readFileSync(w.file);
      const raw = Buffer.alloc(5 + data.length);
      raw[0] = 0x52; raw.writeInt32LE(data.length, 1); data.copy(raw, 5);
      console.log("  " + label.trim() + ": " + c.props.length + " → " + raw.length + " bytes (" + w.file + ")");
      c.props = raw; c.count = 1; w.used = true;
    }
    else if (c && dropOthers) {
      const raw = Buffer.alloc(5); raw[0] = 0x52; raw.writeInt32LE(0, 1);
      console.log("  " + label.trim() + ": " + c.props.length + " → dropped");
      c.props = raw; c.count = 1;
    }
  }
  n.kids.forEach(swap);
}
top.forEach(swap);
want.filter(w => !w.used).forEach(w => console.log("  NOT FOUND: " + w.part));

// written anew: each node's end offset counted from where it lands
const parts = [];
let at = 27;
function put(buf) { parts.push(buf); at += buf.length; }
function write(n) {
  const hdr = Buffer.alloc(head + n.name.length);
  const start = at;
  put(hdr);
  put(n.props);
  n.kids.forEach(write);
  if (n.hadNull) put(Buffer.alloc(head));
  const end = at;
  if (big) { hdr.writeBigUInt64LE(BigInt(end), 0); hdr.writeBigUInt64LE(BigInt(n.count), 8); hdr.writeBigUInt64LE(BigInt(n.props.length), 16); }
  else { hdr.writeUInt32LE(end, 0); hdr.writeUInt32LE(n.count, 4); hdr.writeUInt32LE(n.props.length, 8); }
  hdr[head - 1] = n.name.length;
  hdr.write(n.name, head, "ascii");
  void start;
}
top.forEach(write);
put(Buffer.alloc(head)); // the top level's null record
const out = Buffer.concat([b.subarray(0, 27), ...parts, Buffer.alloc(160)]); // (a zeroed tail in place of the footer)
fs.writeFileSync(outPath, out);
console.log(inPath + " " + b.length + " → " + outPath + " " + out.length + " bytes");

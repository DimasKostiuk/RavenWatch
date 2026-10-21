// RavenWatch: a translated phrase file checked against its Ukrainian original (build/l10n_source/, tools/langdump.ps1).
//   node tools/phrasecheck_lang.js <Language> [file]     — the files in lang_wip/<Language>/ (or assets/lang/<Language>/)
// It looks for: themes [tag] lost, list refs {list} that do not exist or are empty, unknown placeholders (typos),
// broken {a|b} forms, thread marks ^id that the original does not know, Cyrillic left outside ids, creature names.
const fs = require("fs"), path = require("path");
const root = path.join(__dirname, "..");
const language = process.argv[2] || "English";
const only = process.argv[3];
const srcDir = path.join(root, "build", "l10n_source");
const dirs = [path.join(root, "lang_wip", language), path.join(root, "assets", "lang", language)];
const cyr = /[А-Яа-яІіЇїЄєҐґ]/;
let problems = 0, warnings = 0;
const err = (f, n, s) => { problems++; console.log("  ERR  " + f + ":" + n + "  " + s); };
const warn = (f, n, s) => { warnings++; console.log("  warn " + f + ":" + n + "  " + s); };

function read(file) { return fs.readFileSync(file, "utf8").split(/\r?\n/); }

// what a file of the original knows: tags, lists, placeholders, thread ids
function scan(lines) {
  const k = { tags: new Map(), lists: new Map(), names: new Set(), threads: new Set() };
  let list = null;
  lines.forEach((raw, i) => {
    let l = raw.trim();
    if (!l.length || l.startsWith("#")) return;
    if (l.startsWith("@")) { list = l.slice(1).trim(); if (!k.lists.has(list)) k.lists.set(list, 0); return; }
    let m = l.match(/^\[([^\]]+)\]/);
    if (m) { list = null; k.tags.set(m[1], (k.tags.get(m[1]) || 0) + 1); l = l.slice(m[0].length); }
    else if (list) k.lists.set(list, k.lists.get(list) + 1);
    l = l.replace(/^~\d+\s*/, "");
    m = l.match(/^\^([^\s>]+)(?:>(\S+))?\s/);
    if (m) { k.threads.add(m[1]); if (m[2]) m[2].split(/[,|]/).forEach(t => k.threads.add(t)); }
    for (const b of l.match(/\{[^{}]*\}/g) || []) {
      const t = b.slice(1, -1);
      if (t.includes("|")) continue;
      k.names.add(t.replace(/\?$/, ""));
    }
  });
  return k;
}

// every placeholder any original file uses; a creature's forms come together ({x} {x_gen} {x_ins} {x_genpl})
const allNames = new Set();
for (const f of fs.readdirSync(srcDir).filter(f => f.endsWith(".txt"))) for (const n of scan(read(path.join(srcDir, f))).names) allNames.add(n);
const base = n => n.replace(/_(gen|ins|genpl|acc|in)$/, "");
const bases = new Set([...allNames].map(base));
function knownName(name, o) { return o.names.has(name) || allNames.has(name) || bases.has(base(name)); }

function checkPatterns(file, lines, orig) {
  const o = scan(orig), t = scan(lines);
  for (const [tag] of o.tags) if (!t.tags.has(tag)) warn(file, "-", "theme [" + tag + "] is missing (its moments go silent)");
  for (const [tag] of t.tags) if (!o.tags.has(tag)) err(file, "-", "theme [" + tag + "] is not in the original (a typo?)");
  let list = null;
  lines.forEach((raw, i) => {
    const n = i + 1;
    let l = raw.trim();
    if (!l.length || l.startsWith("#")) return;
    if (l.startsWith("@")) { list = l.slice(1).trim(); return; }
    const tagM = l.match(/^\[([^\]]+)\]/);
    if (tagM) { list = null; l = l.slice(tagM[0].length); }
    if (/~\d/.test(l.replace(/^\s*~\d+\s*/, ""))) err(file, n, "a tone mark ~N in the middle of a line");
    l = l.replace(/^~\d+\s*/, "");
    const th = l.match(/^\^([^\s>]+)(?:>(\S+))?\s/);
    if (th) {
      if (!o.threads.has(th[1])) warn(file, n, "thread ^" + th[1] + " is not in the original");
      l = l.slice(th[0].length);
    }
    let bare = l;
    for (const b of l.match(/\{[^{}]*\}/g) || []) {
      const tok = b.slice(1, -1);
      if (tok.includes("|")) {
        const parts = tok.replace(/^>/, "").split("|");
        if (parts.length !== 2) err(file, n, "a form " + b + " needs exactly two sides");
        bare = bare.split(b).join(" ");
        continue;
      }
      const name = tok.replace(/\?$/, "");
      if (t.lists.has(name)) { if (t.lists.get(name) === 0) err(file, n, "list {" + name + "} is empty"); }
      else if (o.lists.has(name)) { if (!tok.endsWith("?")) err(file, n, "list {" + name + "} is not in this file (the template will never be used)"); else warn(file, n, "optional list {" + name + "?} is not in this file"); }
      else if (!knownName(name, o)) err(file, n, "unknown placeholder {" + name + "} (a typo?)");
      bare = bare.split(b).join(" ");
    }
    if (/[{}]/.test(bare)) err(file, n, "a lone brace");
    if (cyr.test(bare)) err(file, n, "Ukrainian left: " + bare.trim().slice(0, 70));
  });
  for (const [name, c] of t.lists) if (c === 0) err(file, "-", "list @" + name + " has no lines");
  for (const [name] of o.lists) if (!t.lists.has(name)) warn(file, "-", "list @" + name + " is missing");
}

function checkNames(file, lines, orig) {
  const keys = l => { const m = l.match(/^([^=#][^=]*)=(.*)$/); return m ? [m[1].trim(), m[2].split("|").map(x => x.trim())] : null; };
  const o = new Set(orig.map(keys).filter(x => x).map(x => x[0]));
  const seen = new Set();
  lines.forEach((l, i) => {
    const k = keys(l.trim());
    if (!k) return;
    seen.add(k[0]);
    if (!o.has(k[0])) warn(file, i + 1, "key " + k[0] + " is not in the original");
    if (k[1].length !== 4 || k[1].some(x => !x.length)) err(file, i + 1, "needs four forms: name | of whom | with whom | of many");
    if (k[1].some(x => cyr.test(x))) err(file, i + 1, "Ukrainian left");
  });
  for (const k of o) if (!seen.has(k)) warn(file, "-", "creature " + k + " is missing (the game's own name is used)");
}

const files = fs.readdirSync(srcDir).filter(f => f.endsWith(".txt") && f !== "dialogues.txt" && (!only || f === only));
for (const f of files) {
  const target = dirs.map(d => path.join(d, f)).find(p => fs.existsSync(p));
  if (!target) { console.log("-- " + f + ": not translated yet"); continue; }
  console.log("== " + f + "  (" + path.relative(root, target) + ")");
  const lines = read(target), orig = read(path.join(srcDir, f));
  if (f === "creature_names.txt") checkNames(f, lines, orig); else checkPatterns(f, lines, orig);
}
console.log(problems + " error(s), " + warnings + " warning(s)");
process.exitCode = problems ? 1 : 0;

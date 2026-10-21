// RavenWatch languages: the texts the code asks for (Lang.T / F / P / N / Word) against the tables in assets/lang.
//   node tools/l10n.js check [Language]  - what is missing, unused, or broken in a table (default English)
//   node tools/l10n.js sync [Language]   - adds every new text to the table with an empty side (to translate), keeps the rest
//   node tools/l10n.js todo              - the Ukrainian literals in the code not yet put through Lang (per file)
const fs = require("fs"), path = require("path");
const root = path.join(__dirname, "..");
const src = path.join(root, "src");
const cyr = /[А-Яа-яІіЇїЄєҐґ]/;
// files whose Ukrainian stays Ukrainian: the grammar tables and the author's own test windows (never seen by players)
const skip = new Set(["Grammar.cs", "Lang.cs",
  // the phrase data (its own files per language, not this table)
  "Patterns.cs", "VarietyPatterns.cs", "CrossPatterns.cs", "TalkPatterns.cs", "AboutPatterns.cs", "Quips.cs", "QuipPatterns.cs", "LifePatterns.cs",
  "LifeTalkPatterns.cs", "DefaultsEvents.cs", "PerchPatterns.cs", "PlacePatterns.cs", "AgainPatterns.cs", "EmotePatterns.cs", "PassPatterns.cs",
  "WorldPatterns.cs", "Defaults.cs", "StrikePatterns.cs", "DeathPatterns.cs", "WardPatterns.cs", "ErrandPatterns.cs", "MorePatterns.cs", "MomentPatterns.cs",
  // the author's own tools (test windows, console, workshop, flight internals): stay Ukrainian
  "Workshop.cs", "Commands.cs", "FlightPanel.cs", "PlayerAnimPanel.cs", "SitPanel.cs", "TemplatePanel.cs", "LooksPanel.cs", "LandPanel.cs",
  "WingPanel.cs", "PhotoPanel.cs", "TakeoffPanel.cs", "DevPanel.cs", "SoundPanel.cs", "LookDiag.cs", "Dummy.cs", "EyeGlow.cs", "PerchTurn.cs",
  "PerchTurnStage.cs", "SitTemplates.cs", "TakeoffKeys.cs", "FlightStage.cs", "FlightTemplates.cs", "Landings.cs", "Motor.cs", "RavenFlight.cs",
  "AnimDirector.cs", "BlackBox.cs", "PlayerAnimLog.cs", "DevKit.cs", "FlightPlans.cs", "FlightRec.cs", "SitDirector.cs"]);

function unC(s) { // a C# regular literal's body to its text
  return s.replace(/\\(u[0-9a-fA-F]{4}|.)/g, (m, c) => c[0] === "u" ? String.fromCharCode(parseInt(c.slice(1), 16)) : c === "n" ? "\n" : c === "t" ? "\t" : c === "r" ? "\r" : c === "0" ? "\0" : c);
}
const esc = s => s.replace(/\\/g, "\\\\").replace(/\n/g, "\\n").replace(/\t/g, "\\t").replace(/\r/g, "\\r").replace(/^#/, "\\#"); // (a text starting with # is not a comment)
const unesc = s => s.replace(/\\(.)/g, (m, c) => c === "n" ? "\n" : c === "t" ? "\t" : c === "r" ? "\r" : c);

// every text the code asks for, with where
const plural = new Set(), formats = new Set();
// a string.Format pattern: every { is {n}, {n,w}, {n:fmt} or the escape {{ (and } likewise)
function badFormat(s) {
  for (let i = 0; i < s.length; i++) {
    if (s[i] === "{") { if (s[i + 1] === "{") { i++; continue; } const m = s.slice(i).match(/^\{\d+(,-?\d+)?(:[^{}]*)?\}/); if (!m) return "a lone { at " + i; i += m[0].length - 1; continue; }
    if (s[i] === "}") { if (s[i + 1] === "}") { i++; continue; } return "a lone } at " + i; }
  }
  return null;
}
const named = s => (s.match(/\{[a-z_]+\??\}/g) || []).sort().join(",");
function asked() {
  const out = new Map(), bad = [];
  const call = /Lang\.(T|TP|F|P|N|K|W|Word)\(/g;
  // constants translated where they are used: Lang.T(Guide.Name) → the text of «const string Name = "…"»
  const consts = new Map();
  for (const f of fs.readdirSync(src).filter(n => n.endsWith(".cs"))) {
    const text = fs.readFileSync(path.join(src, f), "utf8");
    const re = /const string ([^;]*);/g; let m;
    while ((m = re.exec(text))) { const re2 = /(\w+)\s*=\s*"((?:[^"\\]|\\.)*)"/g; let q; while ((q = re2.exec(m[1]))) consts.set(q[1], { s: unC(q[2]), at: f }); }
  }
  for (const f of fs.readdirSync(src).filter(n => n.endsWith(".cs"))) {
    const text = fs.readFileSync(path.join(src, f), "utf8");
    const re = /Lang\.(?:T|TP|W)\((?:\w+\.)?(\w+)\)/g; let m;
    while ((m = re.exec(text))) {
      const c = consts.get(m[1]);
      if (!c || !cyr.test(c.s)) continue;
      if (!out.has(c.s)) out.set(c.s, []);
      out.get(c.s).push(f + " (const " + m[1] + " of " + c.at + ")");
    }
  }
  for (const f of fs.readdirSync(src).filter(n => n.endsWith(".cs"))) {
    const text = fs.readFileSync(path.join(src, f), "utf8");
    let m;
    // lists kept in fields: Lang.Keys("…", "…")
    const keys = /Lang\.Keys\(((?:[^()"]|"(?:[^"\\]|\\.)*")*)\)/g;
    while ((m = keys.exec(text))) {
      const line = text.slice(0, m.index).split("\n").length;
      const lit = /"((?:[^"\\]|\\.)*)"/g; let q;
      while ((q = lit.exec(m[1]))) { const s = unC(q[1]); if (!s.length) continue; if (!out.has(s)) out.set(s, []); out.get(s).push(f + ":" + line); }
    }
    while ((m = call.exec(text))) {
      let i = m.index + m[0].length;
      // P and N: the count first; Word: the key first
      if (m[1] === "P" || m[1] === "N" || m[1] === "Word") { let depth = 0; for (; i < text.length; i++) { const c = text[i]; if (c === "(") depth++; else if (c === ")") depth--; else if (c === "," && depth === 0) { i++; break; } } }
      while (/\s/.test(text[i])) i++;
      const line = text.slice(0, m.index).split("\n").length;
      if (text[i] !== '"') continue; // a value made elsewhere (its texts are marked there: Lang.K / Lang.Keys)
      let j = i + 1, body = "";
      for (; j < text.length; j++) { if (text[j] === "\\") { body += text[j] + text[j + 1]; j++; continue; } if (text[j] === '"') break; body += text[j]; }
      let k = j + 1; while (/\s/.test(text[k])) k++;
      if (text[k] === "+") bad.push(f + ":" + line + "  Lang." + m[1] + "(\"…\" + …): the text must be one literal (use Lang.F with {0})");
      const s = unC(body);
      if (!out.has(s)) out.set(s, []);
      out.get(s).push(f + ":" + line);
      if (m[1] === "P" || m[1] === "N") plural.add(s);
      if (m[1] === "F") formats.add(s);
    }
  }
  // texts made from others in the code: a gift's name without «(пасивний ефект)» (Plain) and without the raven too (Bare)
  {
    const t = fs.readFileSync(path.join(src, "Abilities.cs"), "utf8");
    const re = /new RavenAbility\("[^"]*", (?:true|false), \d+, Lang\.K\("((?:[^"\\]|\\.)*)"\)/g; let m;
    while ((m = re.exec(t))) {
      const name = unC(m[1]);
      const plain = name.replace(" (пасивний ефект)", ""), bare = plain.replace(" Хугіна", "").replace(" Муніна", "");
      for (const k of [plain, bare]) { if (!out.has(k)) out.set(k, []); out.get(k).push("Abilities.cs (Plain/Bare of «" + name + "»)"); }
    }
  }
  // the built-in models' own words (assets/gear, perches, items: «name:» and «note:» lines), shown through Lang
  for (const sub of ["gear", "perches", "items"]) {
    const d = path.join(root, "assets", sub);
    if (!fs.existsSync(d)) continue;
    for (const f of fs.readdirSync(d).filter(n => n.endsWith(".txt"))) {
      fs.readFileSync(path.join(d, f), "utf8").split(/\r?\n/).forEach((l, i) => {
        const m = l.match(/^(name|note):\s*(.*\S)\s*$/);
        if (!m) return;
        if (!out.has(m[2])) out.set(m[2], []);
        out.get(m[2]).push("assets/" + sub + "/" + f + ":" + (i + 1));
      });
    }
  }
  return { out, bad };
}

function readTable(file) {
  const t = new Map(), order = [];
  if (!fs.existsSync(file)) return { t, order, head: "" };
  const lines = fs.readFileSync(file, "utf8").split(/\r?\n/);
  let head = "";
  for (const l of lines) {
    if (l.startsWith("#")) { if (!order.length) head += l + "\n"; continue; }
    if (!l.length) continue;
    const tab = l.indexOf("\t");
    if (tab <= 0) continue;
    const k = unesc(l.slice(0, tab)), v = unesc(l.slice(tab + 1));
    if (!t.has(k)) order.push(k);
    t.set(k, v);
  }
  return { t, order, head };
}

const blanks = s => (s.match(/\{\d+[^}]*\}/g) || []).map(x => x.replace(/:.*\}/, "}")).sort().join(",");
const tags = s => (s.match(/<\/?[a-z]+[^>]*>/gi) || []).map(x => x.replace(/=[^>]*/, "")).sort().join(",");

const cmd = process.argv[2] || "check", language = process.argv[3] || "English";
const tableFile = path.join(root, "assets", "lang", language, "ui.txt");

if (cmd === "todo") {
  let total = 0; const rows = [];
  for (const f of fs.readdirSync(src).filter(n => n.endsWith(".cs"))) {
    if (skip.has(f)) continue;
    const text = fs.readFileSync(path.join(src, f), "utf8");
    // the spans of Lang.Keys(…) lists: everything in them is marked
    const spans = []; { const re = /Lang\.Keys\((?:[^()"]|"(?:[^"\\]|\\.)*")*\)/g; let m; while ((m = re.exec(text))) spans.push([m.index, m.index + m[0].length]); }
    const lines = text.split(/\r?\n/);
    let n = 0, off = 0;
    const show = process.argv.includes("--show") && (!process.argv[3] || process.argv[3] === f || process.argv[3] === "--show");
    lines.forEach((l, li) => {
      const code = l.replace(/\/\/.*$/, "");
      const re = /"((?:[^"\\]|\\.)*)"/g; let m;
      while ((m = re.exec(code))) {
        if (!cyr.test(m[1])) continue;
        const at = off + m.index;
        if (spans.some(s => at >= s[0] && at < s[1])) continue;
        const before = code.slice(0, m.index);
        if (/Lang\.(T|TP|F|P|N|K|W)\(\s*$/.test(before) || /Lang\.(P|N|Word)\([^()]*,\s*$/.test(before)) continue;
        n++;
        if (show) console.log("   " + f + ":" + (li + 1) + "  " + m[1].slice(0, 90));
      }
      off += l.length + 1;
    });
    if (n) { rows.push([n, f]); total += n; }
  }
  rows.sort((a, b) => b[0] - a[0]);
  rows.forEach(r => console.log(String(r[0]).padStart(6) + "  " + r[1]));
  console.log("left: " + total + " Ukrainian literals outside Lang");
  return;
}

const { out, bad } = asked();
const { t, order, head } = readTable(tableFile);

// a batch to translate: the untranslated texts asked for in the given source files (all of a file's texts in order)
//   node tools/l10n.js export English batch1 Abilities.cs Orders.cs …   → lang_wip/English/ui_batch1.tsv
//   node tools/l10n.js merge English batch1                             → its filled lines into the table
if (cmd === "export" || cmd === "merge") {
  const batch = process.argv[4];
  const wip = path.join(root, "lang_wip", language, "ui_" + batch + ".tsv");
  if (cmd === "export") {
    const files = new Set(process.argv.slice(5));
    const keys = [];
    for (const [k, at] of out) {
      if (t.has(k) && t.get(k).length) continue;
      if (files.size && !at.some(a => files.has(a.split(/[:\s]/)[0]))) continue;
      keys.push(k);
    }
    fs.mkdirSync(path.dirname(wip), { recursive: true });
    fs.writeFileSync(wip, "# " + language + " batch " + batch + ": source<TAB>translation\n" + keys.map(k => esc(k) + "\t").join("\n") + "\n");
    console.log(keys.length + " texts → " + path.relative(root, wip));
  } else {
    const { t: done } = readTable(wip);
    let n = 0, skipped = 0;
    for (const [k, v] of done) {
      if (!v.length) { skipped++; continue; }
      if (!out.has(k)) { console.log("  not in the code: «" + k.slice(0, 60) + "»"); continue; }
      t.set(k, v); n++;
    }
    let text = head || "# RavenWatch: " + language + ".\n";
    const keysAll = [...order.filter(k => out.has(k)), ...[...out.keys()].filter(k => !order.includes(k))];
    for (const k of keysAll) text += esc(k) + "\t" + esc(t.get(k) || "") + "\n";
    fs.writeFileSync(tableFile, text);
    console.log(n + " merged into " + path.relative(root, tableFile) + (skipped ? ", " + skipped + " left empty" : ""));
  }
  return;
}

if (cmd === "sync") {
  const keep = order.filter(k => out.has(k)), dropped = order.filter(k => !out.has(k));
  const add = [...out.keys()].filter(k => !t.has(k));
  let text = head || "# RavenWatch: " + language + ".\n";
  for (const k of keep) text += esc(k) + "\t" + esc(t.get(k)) + "\n";
  for (const k of add) text += esc(k) + "\t\n";
  if (dropped.length) { text += "# no longer in the code (kept for a while):\n"; for (const k of dropped) text += "#" + esc(k) + "\t" + esc(t.get(k)) + "\n"; }
  fs.mkdirSync(path.dirname(tableFile), { recursive: true });
  fs.writeFileSync(tableFile, text);
  console.log(language + ": " + keep.length + " kept, " + add.length + " new (to translate), " + dropped.length + " no longer used");
  return;
}

// check
let problems = 0;
const say = s => { problems++; console.log("  " + s); };
console.log("== " + language + ": " + out.size + " texts in the code, " + t.size + " in the table");
if (bad.length) { console.log("-- calls the tool cannot read:"); bad.forEach(say); }
for (const k of formats) { const b = badFormat(k); if (b) say("source format broken (" + b + "): «" + k.slice(0, 80) + "» " + out.get(k)[0]); }
const untranslated = [...out.keys()].filter(k => !t.has(k) || !t.get(k).length);
console.log("-- not translated: " + untranslated.length);
const notInTable = [...out.keys()].filter(k => !t.has(k));
if (notInTable.length) { console.log("-- not in the table at all (run sync): " + notInTable.length); notInTable.slice(0, 10).forEach(k => console.log("   «" + k.replace(/\n/g, "\\n").slice(0, 90) + "» " + out.get(k)[0])); }
for (const [k, v] of t) {
  if (!v.length || !out.has(k) || v === "-") continue; // («-»: no such word in this language — habits and word pools)
  if (blanks(k) !== blanks(v)) say("blanks differ: «" + k.slice(0, 70) + "» → «" + v.slice(0, 70) + "»");
  if (tags(k) !== tags(v)) say("rich-text tags differ: «" + k.slice(0, 70) + "»");
  if (named(k) !== named(v)) say("named blanks ({name}…) differ: «" + k.slice(0, 70) + "»");
  if (formats.has(k)) { const b = badFormat(v); if (b) say("translation format broken (" + b + "): «" + v.slice(0, 70) + "»"); }
  if (plural.has(k) && v.split("|").length > k.split("|").length) say("more plural forms than the source: «" + k + "»");
  if (language !== "Ukrainian" && language !== "Russian" && cyr.test(v.replace(/«[^»]*»/g, "").replace(/\{[^{}|]*\}/g, ""))) say("Cyrillic left in the translation: «" + v.slice(0, 70) + "»");
}
const unused = [...t.keys()].filter(k => !out.has(k));
if (unused.length) console.log("-- in the table but not in the code: " + unused.length + " (sync moves them to the end as comments)");
console.log(problems ? problems + " problem(s)" : "no problems");

// RavenWatch: puts the Ukrainian texts of a C# file through Lang (one-off codemod for the translation work).
//   node tools/l10n_wrap.js <File.cs> [--dry] [--show]
// A lone text becomes Lang.T("…"); a sentence glued from texts and values ("Ще " + n + " с") becomes
// Lang.F("Ще {0} с", n), so a translation may put the values where its own word order wants them.
// A static array of texts (string[] Names = { "…", … }) becomes a property translated on every read (Lang.Arr over
// Lang.Keys), so the places that use it stay as they are.
// Left alone (and listed in build/l10n_wrap_report.txt for a hand fix): const strings, other texts in static fields
// (read once, in the language of that moment), comparisons and lookups (== "…", case "…", Contains("…"), keys),
// Replace, logs, and glued sentences it cannot read safely.
const fs = require("fs"), path = require("path");
const file = process.argv[2], dry = process.argv.includes("--dry");
const cyr = /[А-Яа-яІіЇїЄєҐґ]/;

// ---------------------------------------------------------------- static string arrays → translated properties
// arrays of ids (the flight plans): never translated
const IdArrays = new Set(["Plans"]);
const names = [];
function arrays(src) {
  let n = 0;
  const re = /^([ \t]*)(internal|private|public)\s+static\s+(?:readonly\s+)?string\[\]\s+(\w+)\s*=\s*(?:new\s+string\s*\[\s*\]\s*|new\s*\[\s*\]\s*)?\{((?:[^{};"]|"(?:[^"\\]|\\.)*")*)\}\s*;/gm;
  src = src.replace(re, (all, ind, vis, name, body) => {
    if (!cyr.test(body) || IdArrays.has(name)) return all;
    const items = (body.replace(/\/\/[^\n]*/g, "").match(/"(?:[^"\\]|\\.)*"|[^,\s"]+/g) || []);
    if (!items.every(s => /^"(?:[^"\\]|\\.)*"$/.test(s))) return all; // only plain texts
    n++; names.push(name);
    body = items.join(", ");
    return ind + vis + " static string[] " + name + " { get { return Lang.Arr(" + name + "Src); } } // (translated on every read)\n" +
      ind + "private static readonly string[] " + name + "Src = Lang.Keys(" + body + ");";
  });
  return { src, n };
}

// ---------------------------------------------------------------- one pass over the glued sentences
function pass(src, final) {
  const toks = [];
  {
    let i = 0;
    const n = src.length;
    while (i < n) {
      const c = src[i], s = i;
      if (/\s/.test(c)) { while (i < n && /\s/.test(src[i])) i++; toks.push({ t: "ws", s, e: i }); continue; }
      if (c === "/" && src[i + 1] === "/") { while (i < n && src[i] !== "\n") i++; toks.push({ t: "cm", s, e: i }); continue; }
      if (c === "/" && src[i + 1] === "*") { i = src.indexOf("*/", i + 2) + 2; toks.push({ t: "cm", s, e: i }); continue; }
      if (c === "#" && /^\s*$/.test(src.slice(src.lastIndexOf("\n", s - 1) + 1, s))) { while (i < n && src[i] !== "\n") i++; toks.push({ t: "cm", s, e: i }); continue; }
      if (c === "@" && src[i + 1] === '"') { i += 2; while (i < n) { if (src[i] === '"' && src[i + 1] === '"') { i += 2; continue; } if (src[i] === '"') { i++; break; } i++; } toks.push({ t: "vstr", s, e: i }); continue; }
      if (c === '"') { i++; while (i < n && src[i] !== '"') { if (src[i] === "\\") i++; i++; } i++; toks.push({ t: "str", s, e: i }); continue; }
      if (c === "'") { i++; while (i < n && src[i] !== "'") { if (src[i] === "\\") i++; i++; } i++; toks.push({ t: "chr", s, e: i }); continue; }
      if (/[A-Za-z_]/.test(c)) { while (i < n && /[A-Za-z0-9_]/.test(src[i])) i++; toks.push({ t: "id", s, e: i }); continue; }
      if (/[0-9]/.test(c)) { while (i < n && /[0-9A-Za-z_.]/.test(src[i])) { if (src[i] === "." && !/[0-9]/.test(src[i + 1] || "")) break; i++; } toks.push({ t: "num", s, e: i }); continue; }
      const three = src.substr(i, 3), two = src.substr(i, 2);
      if (["<<=", ">>="].includes(three)) { i += 3; toks.push({ t: "op", s, e: i }); continue; }
      if (["==", "!=", "<=", ">=", "&&", "||", "??", "=>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "++", "--", "<<", "->", "::"].includes(two)) { i += 2; toks.push({ t: "op", s, e: i }); continue; }
      i++; toks.push({ t: "op", s, e: i });
    }
  }
  const txt = k => (k ? src.slice(k.s, k.e) : "");
  const sig = toks.filter(k => k.t !== "ws" && k.t !== "cm");

  // inside code (a method, a lambda), or a field
  const runtime = new Array(sig.length).fill(false);
  {
    const stack = [{ kind: "file" }];
    const lambdas = [];
    let paren = 0;
    for (let j = 0; j < sig.length; j++) {
      const v = txt(sig[j]);
      const top = stack[stack.length - 1];
      const inCode = top.kind === "code" || lambdas.length > 0;
      if (v === "{") {
        const p = txt(sig[j - 1]);
        let kind;
        if ([")", "else", "try", "do", "get", "set", "finally", "=>", "add", "remove", "unsafe", "checked", "unchecked"].includes(p)) kind = "code";
        else if (["=", ",", "{", "]", "(", "return"].includes(p) || txt(sig[j - 2]) === "new") kind = inCode ? "code" : "init";
        else {
          let b = j - 1, isType = false;
          while (b >= 0 && ![";", "{", "}"].includes(txt(sig[b]))) { if (["class", "struct", "enum", "namespace", "interface"].includes(txt(sig[b]))) isType = true; b--; }
          kind = isType ? "type" : (inCode ? "code" : "init");
        }
        stack.push({ kind });
      } else if (v === "}") { if (stack.length > 1) stack.pop(); }
      else if (v === "(") paren++;
      else if (v === ")") { paren--; while (lambdas.length && paren < lambdas[lambdas.length - 1]) lambdas.pop(); }
      else if (v === "=>" && top.kind !== "code") lambdas.push(paren);
      else if ((v === "," || v === ";") && lambdas.length && paren <= lambdas[lambdas.length - 1]) lambdas.pop();
      runtime[j] = stack[stack.length - 1].kind === "code" || lambdas.length > 0;
    }
  }

  const stopOps = new Set([",", ";", ")", "]", "}", "(", "[", "{", "?", ":", "=", "=>", "&&", "||", "??", "==", "!=", "<", ">", "<=", ">=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "&", "|", "^", "<<", ">>", "return", "throw", "yield", "case", "in", "else", "new"]);
  function operandEnd(j) {
    let d = 0, x = j;
    for (; x < sig.length; x++) {
      const v = txt(sig[x]);
      if (v === "(" || v === "[") { d++; continue; }
      if (v === ")" || v === "]") { if (d === 0) return x; d--; continue; }
      if (d > 0) continue;
      if (v === "+" || v === "-" || stopOps.has(v)) return x;
    }
    return x;
  }
  function operandStart(j) {
    let d = 0, x = j;
    for (; x >= 0; x--) {
      const v = txt(sig[x]);
      if (v === ")" || v === "]") { d++; continue; }
      if (v === "(" || v === "[") { if (d === 0) return x + 1; d--; continue; }
      if (d > 0) continue;
      if (v === "+" || v === "-" || stopOps.has(v)) return x + 1;
    }
    return 0;
  }
  const isCyrStr = k => (k.t === "str" || k.t === "vstr") && cyr.test(txt(k));
  const keyRanges = [];
  { const re = /Lang\.Keys\((?:[^()"]|"(?:[^"\\]|\\.)*")*\)/g; let m; while ((m = re.exec(src))) keyRanges.push([m.index, m.index + m[0].length]); }
  const inKeys = k => keyRanges.some(r => k.s >= r[0] && k.e <= r[1]);
  const throughLang = k => inKeys(k) || /Lang\.(T|F|P|N|K|Keys|Word)\s*\(\s*$/.test(src.slice(Math.max(0, k.s - 30), k.s)) || /Lang\.(P|N|Word)\s*\([^()]{0,60},\s*$/.test(src.slice(Math.max(0, k.s - 90), k.s));
  const lineOf = k => src.slice(0, k.s).split("\n").length;
  const skips = [];
  const skip = (j, why) => { if (final) skips.push(lineOf(sig[j]) + "  " + why + ":  " + txt(sig[j]).slice(0, 100)); };
  const edits = [], used = new Set();
  let nT = 0, nF = 0, deferred = 0;

  for (let j = 0; j < sig.length; j++) {
    const k = sig[j];
    if (!isCyrStr(k) || used.has(j) || throughLang(k)) continue;
    const p = txt(sig[j - 1]), pp = txt(sig[j - 2]), next = txt(sig[j + 1]);
    if (p === "case") { skip(j, "case label"); continue; }
    if (p === "==" || p === "!=" || next === "==" || next === "!=") { skip(j, "comparison"); continue; }
    if (p === "(" && /^(StartsWith|EndsWith|Contains|IndexOf|LastIndexOf|Equals|ContainsKey|TryGetValue|Remove|Log|LogInfo|LogWarning|LogError|GetPrefab|Find|Split|Replace|Trim|TrimEnd|TrimStart|Bind|Hide|HFloat|Fault)$/.test(pp)) { skip(j, pp + "(…)"); continue; }
    if (p === "," && /\.Replace\s*\(\s*"(?:[^"\\]|\\.)*"\s*,\s*$/.test(src.slice(Math.max(0, k.s - 300), k.s))) { skip(j, "Replace(…, text)"); continue; }
    if (p === "[") { skip(j, "index/key"); continue; }
    { let b = j - 1, isConst = false; while (b >= 0 && ![";", "{", "}"].includes(txt(sig[b]))) { if (txt(sig[b]) === "const") isConst = true; b--; } if (isConst) { skip(j, "const"); continue; } }
    if (!runtime[j]) { skip(j, "field (read once): translate where it is shown"); continue; }

    const parts = [[j, j + 1]];
    let x = j - 1, bad = false;
    while (x >= 0 && txt(sig[x]) === "+") { const st = operandStart(x - 1); if (st > x - 1) { bad = true; break; } parts.unshift([st, x]); x = st - 1; }
    if (x >= 0 && txt(sig[x]) === "-") bad = true;
    let y = j + 1;
    while (y < sig.length && txt(sig[y]) === "+") { const en = operandEnd(y + 1); if (en === y + 1) { bad = true; break; } parts.push([y + 1, en]); y = en; }
    if (y < sig.length && txt(sig[y]) === "-") bad = true;
    const before = x >= 0 ? txt(sig[x]) : ";", after = y < sig.length ? txt(sig[y]) : ";";
    if (["*", "/", "%", ".", "!"].includes(before) || ["*", "/", "%", "."].includes(after)) bad = true;
    if (bad) { skip(j, "glued sentence too tricky"); continue; }
    const isLit = pr => pr[1] - pr[0] === 1 && sig[pr[0]].t === "str";
    if (parts.some(pr => pr[1] - pr[0] === 1 && sig[pr[0]].t === "vstr")) { skip(j, "verbatim string"); continue; }
    // a value holding a text of its own not yet done: this sentence waits for the next pass
    let inner = false;
    for (const pr of parts) if (!isLit(pr)) for (let q = pr[0]; q < pr[1]; q++) if (isCyrStr(sig[q]) && !throughLang(sig[q])) inner = true;
    if (inner) { deferred++; continue; }
    const firstLit = parts.findIndex(isLit);
    let fmt = "", args = [];
    if (firstLit > 0) {
      const a0 = sig[parts[0][0]].s, a1 = sig[parts[firstLit - 1][1] - 1].e;
      args.push(firstLit === 1 ? src.slice(a0, a1) : "(" + src.slice(a0, a1) + ")");
      fmt += "{0}";
    }
    for (let q = Math.max(0, firstLit); q < parts.length; q++) {
      const pr = parts[q];
      if (isLit(pr)) fmt += txt(sig[pr[0]]).slice(1, -1).replace(/\{/g, "{{").replace(/\}/g, "}}");
      else { fmt += "{" + args.length + "}"; args.push(src.slice(sig[pr[0]].s, sig[pr[1] - 1].e)); }
    }
    for (const pr of parts) for (let q = pr[0]; q < pr[1]; q++) used.add(q);
    const start = sig[parts[0][0]].s, end = sig[parts[parts.length - 1][1] - 1].e;
    let rep;
    if (!args.length) { rep = 'Lang.T("' + fmt.replace(/\{\{/g, "{").replace(/\}\}/g, "}") + '")'; nT++; }
    else { rep = 'Lang.F("' + fmt + '", ' + args.join(", ") + ")"; nF++; }
    edits.push([start, end, rep]);
  }
  edits.sort((a, b) => b[0] - a[0]);
  for (const [s, e, r] of edits) src = src.slice(0, s) + r + src.slice(e);
  return { src, nT, nF, deferred, skips, changed: edits.length > 0 };
}

let src = fs.readFileSync(file, "utf8");
const arr = arrays(src); src = arr.src;
let T = 0, F = 0, r;
for (let i = 0; i < 8; i++) { r = pass(src, false); src = r.src; T += r.nT; F += r.nF; if (!r.changed) break; }
r = pass(src, true);
if (!dry) fs.writeFileSync(file, src);
console.log(path.basename(file) + ": arrays " + arr.n + (names.length ? " (" + names.join(", ") + ")" : "") + ", T " + T + ", F " + F + ", left " + r.skips.length + (r.deferred ? ", stuck " + r.deferred : ""));
if (process.argv.includes("--show")) r.skips.forEach(s => console.log("   " + s));
fs.mkdirSync(path.join(__dirname, "..", "build"), { recursive: true });
fs.appendFileSync(path.join(__dirname, "..", "build", "l10n_wrap_report.txt"), "== " + path.basename(file) + "\n" + r.skips.map(s => "   " + s).join("\n") + "\n");

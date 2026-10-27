// The built-in errands' words (name, text, text2 in Errands.cs Builtin) into src/ContentWords.cs, so tools/l10n.js
// knows them. Adds only the ones not there yet, after the errands' block.
//   node tools/contentwords.js
const fs = require("fs"), path = require("path");
const src = path.join(__dirname, "..", "src");
const er = fs.readFileSync(path.join(src, "Errands.cs"), "utf8"), cwf = path.join(src, "ContentWords.cs");
let cw = fs.readFileSync(cwf, "utf8");
const want = [], re = /\\n(name|text|text2): ([^\\"]+)/g;
let m;
while ((m = re.exec(er))) { const t = m[2].trim(); if (!cw.includes(`"${t}"`) && !want.includes(t)) want.push(t); }
const anchor = `            "Мунін бачив ватажка далеко звідси. Дорога довга, бій — ще довший.",\n`; // (the errands' block)
if (!cw.includes(anchor)) throw new Error("ContentWords.cs: the errands' block not found");
cw = cw.replace(anchor, anchor + want.map(t => `            "${t}",\n`).join(""));
fs.writeFileSync(cwf, cw);
console.log(want.length + " added");

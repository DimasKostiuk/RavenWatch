// RavenWatch: puts translations written by line number next to their texts in an exported batch.
//   lang_wip/<Language>/ui_<batch>.tsv        (exported: source<TAB> per line, tools/l10n.js export)
//   lang_wip/<Language>/ui_<batch>.<n>.txt    (translations: «line<TAB>translation», \n escaped; any number of parts)
//   node tools/l10n_zip.js <Language> <batch>  → fills the batch file; then: node tools/l10n.js merge <Language> <batch>
const fs = require("fs"), path = require("path");
const [language, batch] = process.argv.slice(2);
const dir = path.join(__dirname, "..", "lang_wip", language);
const file = path.join(dir, "ui_" + batch + ".tsv");
const lines = fs.readFileSync(file, "utf8").split("\n");
const parts = fs.readdirSync(dir).filter(f => f.startsWith("ui_" + batch + ".") && f.endsWith(".txt")).sort();
let n = 0, bad = 0;
for (const p of parts) {
  fs.readFileSync(path.join(dir, p), "utf8").split(/\r?\n/).forEach((l, i) => {
    if (!l.trim() || l.startsWith("#")) return;
    const tab = l.indexOf("\t");
    const no = parseInt(l.slice(0, tab), 10);
    if (tab < 0 || !(no >= 1) || no > lines.length) { bad++; console.log("  " + p + ":" + (i + 1) + " cannot read: " + l.slice(0, 60)); return; }
    const row = lines[no - 1];
    const t = row.indexOf("\t");
    if (t < 0 || row.startsWith("#")) { bad++; console.log("  " + p + ":" + (i + 1) + " line " + no + " is no text"); return; }
    lines[no - 1] = row.slice(0, t) + "\t" + l.slice(tab + 1);
    n++;
  });
}
fs.writeFileSync(file, lines.join("\n"));
const left = lines.filter(l => l && !l.startsWith("#") && l.endsWith("\t")).length;
console.log(n + " filled from " + parts.length + " part(s), " + left + " still empty" + (bad ? ", " + bad + " bad line(s)" : ""));

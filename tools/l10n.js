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
  "WorldPatterns.cs", "Defaults.cs", "StrikePatterns.cs", "DeathPatterns.cs", "WardPatterns.cs", "ErrandPatterns.cs", "MorePatterns.cs", "MomentPatterns.cs", "HomePatterns.cs", "FillPatterns.cs",
  // the author's own tools (test windows, console, workshop, flight internals): stay Ukrainian
  "Workshop.cs", "Commands.cs", "AnimStage.cs", "FlightPanel.cs", "PlayerAnimPanel.cs", "SitPanel.cs", "TemplatePanel.cs", "LooksPanel.cs", "LandPanel.cs",
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
      if (text[i] !== '"') {
        // a value made elsewhere (its texts are marked there: Lang.K / Lang.Keys) — but the branches of a choice right in it,
        // «Lang.T(young ? "молодий|молода" : "старий|стара")», are its texts: a literal right after «?» or «:» of the argument
        let d = 0, e = i, lastSig = "";
        for (; e < text.length; e++) {
          const c = text[e];
          if (c === '"') {
            let q = e + 1, b = ""; for (; q < text.length; q++) { if (text[q] === "\\") { b += text[q] + text[q + 1]; q++; continue; } if (text[q] === '"') break; b += text[q]; }
            if ((lastSig === "?" || lastSig === ":") && cyr.test(b)) { const s = unC(b); if (!out.has(s)) out.set(s, []); out.get(s).push(f + ":" + line); if (m[1] === "F") formats.add(s); if (m[1] === "P" || m[1] === "N") plural.add(s); }
            e = q; lastSig = '"'; continue;
          }
          if (c === "(") d++; else if (c === ")") { if (d === 0) break; d--; } else if (c === "," && d === 0) break;
          if (!/\s/.test(c)) lastSig = c;
        }
        continue;
      }
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
  // the built-in models' own words (assets/gear, perches, items: «name:», «note:» and «story:» lines), shown through Lang
  for (const sub of ["gear", "perches", "items", "story/perches", "story/items"]) {
    const d = path.join(root, "assets", sub);
    if (!fs.existsSync(d)) continue;
    for (const f of fs.readdirSync(d).filter(n => n.endsWith(".txt"))) {
      fs.readFileSync(path.join(d, f), "utf8").split(/\r?\n/).forEach((l, i) => {
        const m = l.match(/^(name|note|story):\s*(.*\S)\s*$/);
        if (!m) return;
        // («story: для Кетіля until world x   # …»: the text is the words before «until» and the note — WsItems.StoryOf; «story: yes»: a word, not a text)
        const text = m[1] === "story" ? m[2].replace(/\s+#.*$/, "").replace(/(^|\s+)until\s.*$/, "").trim() : m[2];
        if (m[1] === "story" && (text === "yes" || text === "")) return;
        if (!out.has(text)) out.set(text, []);
        out.get(text).push("assets/" + sub + "/" + f + ":" + (i + 1));
      });
    }
  }
  // the story's own texts (assets/story quests + scenes: goals, mark labels, lines said and shown), as StoryQuests and
  // StoryScenes hand them to Lang.T — the same cut (StoryFiles.Lines: « #» starts a note)
  const put = (text, where) => { text = (text || "").trim(); if (!text || !cyr.test(text)) return; if (!out.has(text)) out.set(text, []); out.get(text).push(where); };
  const rest = (line, n) => { const w = line.split(/[ \t]+/).filter(Boolean); if (w.length <= n) return ""; let pos = 0; for (let i = 0; i < n; i++) pos = line.indexOf(w[i], pos) + w[i].length; return line.slice(pos).trim(); };
  for (const kind of ["quests", "scenes"]) {
    const d = path.join(root, "assets", "story", kind);
    if (!fs.existsSync(d)) continue;
    for (const f of fs.readdirSync(d).filter(n => n.endsWith(".txt"))) {
      fs.readFileSync(path.join(d, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
        let line = raw; const note = line.indexOf(" #"); if (note >= 0) line = line.slice(0, note);
        line = line.trim();
        if (!line || line.startsWith("#") || line.startsWith("[")) return;
        const where = "assets/story/" + kind + "/" + f + ":" + (i + 1);
        if (kind === "quests") {
          const c = line.indexOf(":"); if (c <= 0) return;
          const key = line.slice(0, c).trim().toLowerCase(), val = line.slice(c + 1).trim();
          if (key === "goal" || key === "more" || key === "name" || key === "saga") put(val, where);
          else if (key === "mark") { const m = val.split(/ +/); const r = parseInt(m[1], 10); put(r > 0 ? m.slice(2).join(" ") : m.slice(1).join(" "), where); }
          else if (key === "start" || key === "done") val.split(";").forEach(x => { x = x.trim(); if (x.startsWith("msg ")) put(rest(x, 1), where); });
          return;
        }
        if (line.startsWith("if ")) { const c = line.indexOf(":"); if (c < 0) return; line = line.slice(c + 1).trim(); }
        const w = line.split(/[ \t]+/);
        if (w[0] === "say") put(rest(line, 2), where);
        else if (w[0] === "msg" || w[0] === "sub") put(rest(line, 1), where);
        else if ((w[0] === "bird" || w[0] === "player") && w[1] === "say") put(rest(line, 2), where);
        else if (w[0] === "npc" && (w[2] === "say" || w[2] === "says")) put(rest(line, 3), where); // («says»: the same line, the scene going on)
        else if (w[0] === "do" && w[1] === "msg") put(rest(line, 2), where);
        else if (w[0] === "letter") { const r = rest(line, 1), sep = r.indexOf("::"); if (sep > 0) { put(r.slice(0, sep).replace(/ seal \S+\s*$/, "").trim(), where); put(r.slice(sep + 2).trim(), where); } } // (a letter: who from — without «seal <id>» — and its words: StoryScenes «letter»)
      });
    }
  }
  // the story's places (assets/story/blueprints/locations.list.txt): their names (the map) and what a touched piece reads (LocTouch: «read:»,
  // its words after «::» when it has conditions; «\n» kept as written, as the keys are)
  {
    const lf = path.join(root, "assets", "story", "blueprints", "locations.list.txt");
    if (fs.existsSync(lf)) fs.readFileSync(lf, "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
      let line = raw; const note = line.indexOf(" #"); if (note >= 0) line = line.slice(0, note);
      line = line.trim();
      const where = "assets/story/blueprints/locations.list.txt:" + (i + 1);
      let m;
      if ((m = /^(?:name|hearthshut):\s*(.+)$/.exec(line))) put(m[1], where);
      else if ((m = /^(?:hearthwait|hearthgivewait|insideshut):\s*(.+)$/.exec(line))) { const v = m[1], sep = v.indexOf("::"); if (sep >= 0 && v.slice(sep + 2).trim()) put(v.slice(sep + 2).trim(), where); } // (what the hearth says while it waits for the story)
      else if ((m = /^pool:\s*\S+\s+(.+)$/.exec(line))) put(m[1], where); // (a list of names: as many and in the same order in every language — storycheck)
      else if ((m = /^read:\s*\S+\s+(.+)$/.exec(line))) { const v = m[1], sep = v.indexOf("::"); put((sep >= 0 ? v.slice(sep + 2) : v).replace(/\\n/g, "\n"), where); } // (a written «\n» is a new line, as in the code's keys)
    });
  }
  // a story person's greetings and the words it cuts into your talk with (assets/story/npc: «greet:», «interrupt:» lines, said through Lang.T — NpcTalk «{greet}»; a made one, «{@see_player}», has no text of its own).
  // Missed till 2026-11-14: Ketil's greetings stood in no table and the build said nothing
  const npcDir = path.join(root, "assets", "story", "npc");
  if (fs.existsSync(npcDir)) for (const f of fs.readdirSync(npcDir).filter(n => n.endsWith(".txt")))
    fs.readFileSync(path.join(npcDir, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
      const m = /^(?:greet|interrupt|cry|raven_name):\s*(.+?)\s*$/.exec(raw.trim()); if (m) put(m[1], "assets/story/npc/" + f + ":" + (i + 1)); // (and the words it cuts into your talk with: NpcTalkCutIn.cs; its raven's name over its line in your two's talk: NpcRaven)
    });
  // a story person not met yet (assets/story/people, StoryStranger): its shown name after «stranger: … ::» and your ravens' lines of «strangersay:»
  // («hugin <words> / munin <words>», each part's words through Lang.T)
  const peopleDir = path.join(root, "assets", "story", "people");
  if (fs.existsSync(peopleDir)) for (const f of fs.readdirSync(peopleDir).filter(n => n.endsWith(".txt")))
    fs.readFileSync(path.join(peopleDir, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
      let line = raw; const note = line.indexOf(" #"); if (note >= 0) line = line.slice(0, note); line = line.trim();
      const where = "assets/story/people/" + f + ":" + (i + 1);
      let m;
      if ((m = /^stranger:\s*(.+)$/.exec(line))) { const sep = m[1].indexOf("::"); if (sep >= 0) put(m[1].slice(sep + 2).trim(), where); }
      else if ((m = /^strangersay:\s*(.+)$/.exec(line))) for (const part of m[1].split("/")) { const t = part.trim(), sp = t.indexOf(" "); if (sp > 0) put(t.slice(sp + 1).trim(), where); }
    });
  // the story's tales (assets/story/tales): what a witness and a hearer say
  const talesDir = path.join(root, "assets", "story", "tales");
  if (fs.existsSync(talesDir)) for (const f of fs.readdirSync(talesDir).filter(n => n.endsWith(".txt")))
    fs.readFileSync(path.join(talesDir, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
      let line = raw; const note = line.indexOf(" #"); if (note >= 0) line = line.slice(0, note); line = line.trim();
      const m = /^(saw|heard|hearsay|saga|rumor|as \w+):\s*(.+)$/.exec(line); if (m) put(m[2], "assets/story/tales/" + f + ":" + (i + 1));
    });
  // the saga (assets/story/saga): a chapter's words, an entry's names and lines (after their «<conditions> :: »), a quote's caption
  const sagaDir = path.join(root, "assets", "story", "saga");
  if (fs.existsSync(sagaDir)) for (const f of fs.readdirSync(sagaDir).filter(n => n.endsWith(".txt")))
    fs.readFileSync(path.join(sagaDir, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
      let line = raw; const note = line.indexOf(" #"); if (note >= 0) line = line.slice(0, note); line = line.trim();
      const m = /^(name|label|lands|epigraph|closing|about|line|answer):\s*(.+)$/.exec(line);
      if (m) { const sep = m[2].indexOf(" :: "); put((sep >= 0 ? m[2].slice(sep + 4) : m[2]).trim(), "assets/story/saga/" + f + ":" + (i + 1)); }
      const q = /^quote:\s*\S+\s+(.+)$/.exec(line); if (q) put(q[1].trim(), "assets/story/saga/" + f + ":" + (i + 1));
    });
  // a talk's texts, cut as NpcTalk.Parse cuts them: what a place says (all its lines joined, answers between or not, till the
  // next «@» or «[»), each answer's words (every «a || b» its own text: NpcTalk.OneOf) and a raven's word in it
  const talkTexts = (text, whereOf) => {
    let says = [], at = "";
    const end = () => { if (says.length) put(says.join("\n"), at); says = []; };
    text.replace(/\r/g, "").split("\n").forEach((raw, i) => {
      const line = raw.trim(), where = whereOf(i);
      if (!line || line.startsWith("#") || line.startsWith("??") || line.startsWith("?-")) return; // («?-»: the end of a «through» question — NpcTalk.ParseEnd, no words)
      if (line.startsWith("[") || line.startsWith("@")) { end(); return; }
      if (line.startsWith("~")) { const c = line.indexOf(":"); if (c > 0) put(line.slice(c + 1).trim(), where); return; } // (a raven's word in a talk: its own text, never part of what the person says)
      if (line.startsWith(">")) { const a = line.indexOf("=>"), from = line.startsWith(">|") ? 2 : 1; (a < 0 ? line.slice(from) : line.slice(from, a)).split("||").forEach(w => put(w, where)); return; }
      if (!says.length) at = where;
      says.push(line);
    });
    end();
  };
  // the story's gatherings (assets/story/meets, read as NpcChatter.Parse reads «[meet <name>]»): each line without its «<who>: »
  {
    const meetsDir = path.join(root, "assets", "story", "meets");
    if (fs.existsSync(meetsDir)) for (const f of fs.readdirSync(meetsDir).filter(n => n.endsWith(".txt")))
      fs.readFileSync(path.join(meetsDir, f), "utf8").replace(/\r/g, "").split("\n").forEach((raw, i) => {
        const l = raw.trim();
        if (!l || l.startsWith("#") || l.startsWith("[") || l.startsWith("?")) return;
        const m = /^(\w+):\s*(.+)$/.exec(l); if (m) put(m[2], "assets/story/meets/" + f + ":" + (i + 1));
      });
  }
  // the story's talks (assets/story/talk)
  const talkDir = path.join(root, "assets", "story", "talk");
  if (fs.existsSync(talkDir)) for (const f of fs.readdirSync(talkDir).filter(n => n.endsWith(".txt")))
    talkTexts(fs.readFileSync(path.join(talkDir, f), "utf8"), i => "assets/story/talk/" + f + ":" + (i + 1));
  // the talks built into the code (NpcTalk's own common, the modules' «[talk common +]», NpcTasks' answers kept in fields): a file
  // that holds «[talk », every literal of it read as talk lines (a literal is whole lines there; the place's words of the
  // built-in talks are made, «{greet}» «{concern}», so only answers and ravens' words come of them)
  for (const f of fs.readdirSync(src).filter(n => n.endsWith(".cs"))) {
    const text = fs.readFileSync(path.join(src, f), "utf8");
    if (text.indexOf("[talk ") < 0) continue;
    const lit = /"((?:[^"\\]|\\.)*)"/g; let m;
    while ((m = lit.exec(text))) {
      const s = unC(m[1]);
      if (!/^\s*(>|~\w+ \w+:)/m.test(s)) continue;
      const line = text.slice(0, m.index).split("\n").length;
      talkTexts(s.split("\n").filter(l => /^\s*(>|~)/.test(l)).join("\n"), () => "src/" + f + ":" + line);
    }
  }
  // the residents' small talk built into NpcChatter (Builtin, read as NpcChatter.Parse reads it): each line of a talk without
  // its «- », each line of one person's; NpcChatter puts each through Lang.T where it is said
  {
    const text = fs.readFileSync(path.join(src, "NpcChatter.cs"), "utf8");
    let i = text.indexOf("string Builtin =");
    if (i < 0) bad.push("NpcChatter.cs: no «string Builtin =» (tools/l10n.js reads its lines from there)");
    else {
      i += "string Builtin =".length; let body = "";
      for (;;) {
        while (i < text.length && /[\s+]/.test(text[i])) i++;
        if (text.startsWith("//", i)) { i = text.indexOf("\n", i); continue; }
        if (text[i] !== '"') break;
        let j = i + 1; for (; j < text.length; j++) { if (text[j] === "\\") { j++; continue; } if (text[j] === '"') break; }
        body += unC(text.slice(i + 1, j)); i = j + 1;
      }
      if (text[i] !== ";") bad.push("NpcChatter.cs: Builtin is not literals joined by + (tools/l10n.js cannot read it)");
      let talks = false;
      body.split("\n").forEach(raw => {
        const l = raw.trim();
        if (!l || l.startsWith("#")) return;
        if (l.startsWith("[") && l.endsWith("]")) { talks = l.slice(1).startsWith("chat "); return; }
        if (talks && l.startsWith("?")) return;
        put(talks && l.startsWith("-") ? l.slice(1).trim() : l, "src/NpcChatter.cs (Builtin)");
      });
    }
  }
  // a settler's name is a row of its own (NpcActor.Name: Lang.T of the kept name): the same text used for anything else would
  // give the name that other thing's translation («Руна» — a rune — named a woman «Rune»)
  {
    const t = fs.readFileSync(path.join(src, "NpcSettlers.cs"), "utf8"), names = new Set();
    const re = /string\[\] (?:Men|Women) = Lang\.Keys\(([^)]*)\)/g; let m;
    while ((m = re.exec(t))) for (const q of m[1].match(/"((?:[^"\\]|\\.)*)"/g) || []) names.add(unC(q.slice(1, -1)));
    if (!names.size) bad.push("NpcSettlers.cs: no «string[] Men|Women = Lang.Keys(…)» (tools/l10n.js checks the names there)");
    for (const k of names) { const other = (out.get(k) || []).find(w => !w.startsWith("NpcSettlers.cs")); if (other) bad.push("NpcSettlers.cs: the name «" + k + "» is also a text of " + other + " — one row cannot be both: pick another name"); }
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
  if (k.split("\n").length !== v.split("\n").length) say("lines differ (" + k.split("\n").length + " → " + v.split("\n").length + "): «" + k.replace(/\n/g, "\\n").slice(0, 70) + "» — a translation keeps every line (2026-11-18: Lit's whole plan was one line in English)");
  if (formats.has(k)) { const b = badFormat(v); if (b) say("translation format broken (" + b + "): «" + v.slice(0, 70) + "»"); }
  if (plural.has(k) && v.split("|").length > k.split("|").length) say("more plural forms than the source: «" + k + "»");
  if (language !== "Ukrainian" && language !== "Russian" && cyr.test(v.replace(/«[^»]*»/g, "").replace(/\{[^{}|]*\}/g, ""))) say("Cyrillic left in the translation: «" + v.slice(0, 70) + "»");
}
const unused = [...t.keys()].filter(k => !out.has(k));
if (unused.length) console.log("-- in the table but not in the code: " + unused.length + " (sync moves them to the end as comments)");
// --strict (the build, for English — the user 2026-11-11: every text changed is translated at once): a text not translated is a
// problem, and so is a people's theme English has not or a people's list it has shorter (npc_patterns.txt, cut as NpcPhrases.Parse)
if (process.argv.includes("--strict")) {
  untranslated.slice(0, 15).forEach(k => say("not translated: «" + k.replace(/\n/g, "\\n").slice(0, 80) + "» " + (out.get(k) || [""])[0]));
  if (untranslated.length > 15) { problems += untranslated.length - 15; console.log("  … and " + (untranslated.length - 15) + " more not translated"); }
  const enFile = path.join(root, "assets", "lang", language, "npc_patterns.txt");
  const read = lines => { const T = new Set(), L = new Map(); let cur = null; for (let l of lines) { l = l.trim(); if (!l || l.startsWith("#") || l.startsWith("//")) continue; if (l.startsWith("@")) { cur = l.slice(1).trim(); if (!L.has(cur)) L.set(cur, 0); continue; } const m = l.match(/^\[([^\]]+)\]/); if (m) { cur = null; const h = m[1].trim(), q = h.indexOf(" ?"); T.add(q < 0 ? h : h.slice(0, q).trim()); continue; } if (cur) L.set(cur, L.get(cur) + 1); } return { T, L }; };
  const verb = (file, name) => { const text = fs.readFileSync(path.join(src, file), "utf8"), re = /@"((?:[^"]|"")*)"/g, got = []; let m; if (name) { re.lastIndex = text.indexOf("const string " + name + " = @\""); m = re.exec(text); return m ? [m[1].replace(/""/g, '"')] : []; } while ((m = re.exec(text))) { const s = m[1].replace(/""/g, '"'); if (/^\s*(\[|@)/m.test(s) && s.length > 200) got.push(s); } return got; };
  const all = /All = \{([^}]*)\}/.exec(fs.readFileSync(path.join(src, "NpcMorePatterns.cs"), "utf8"));
  const where = new Map(); for (const f of fs.readdirSync(src).filter(n => /^Npc.*Patterns\.cs$/.test(n))) { const re = /const string (\w+) = @"/g, text = fs.readFileSync(path.join(src, f), "utf8"); let m; while ((m = re.exec(text))) where.set(m[1], f); }
  let uk = []; for (const f of ["NpcPhrases.cs", "NpcTalkPatterns.cs", "NpcSituationPatterns.cs"]) uk = uk.concat(verb(f));
  const wordsDir = path.join(root, "assets", "story", "words"); // (a story person's own words: data, the same format)
  if (fs.existsSync(wordsDir)) for (const w of fs.readdirSync(wordsDir).filter(n => n.endsWith(".txt"))) uk.push(fs.readFileSync(path.join(wordsDir, w), "utf8").replace(/\r/g, ""));
  if (!all) say("NpcMorePatterns.cs: no «All = { … }» (tools/l10n.js reads the people's words in its order)");
  else for (const n of all[1].split(",").map(s => s.trim()).filter(Boolean)) { if (!where.has(n)) { say("NpcMorePatterns.All: «" + n + "» is no const string of an Npc*Patterns.cs"); continue; } uk = uk.concat(verb(where.get(n), n)); }
  const U = read(uk.join("\n").split("\n")), E = read(fs.existsSync(enFile) ? fs.readFileSync(enFile, "utf8").split(/\r?\n/) : []);
  const noTheme = [...U.T].filter(x => !E.T.has(x)), short = [...U.L].filter(([n, c]) => (E.L.get(n) || 0) < c);
  noTheme.slice(0, 10).forEach(x => say("npc_patterns.txt: theme [" + x + "] has no " + language + " (its Ukrainian is said instead)"));
  short.slice(0, 10).forEach(([n, c]) => say("npc_patterns.txt: list @" + n + " has " + (E.L.get(n) || 0) + " " + language + " lines, the Ukrainian " + c + " (add the new ones)"));
  const more = Math.max(0, noTheme.length - 10) + Math.max(0, short.length - 10);
  if (more) { problems += more; console.log("  … and " + more + " more of the people's words"); }
  console.log("-- people's words: " + noTheme.length + " theme(s) missing, " + short.length + " list(s) short");
}
console.log(problems ? problems + " problem(s)" : "no problems");
if (problems) process.exitCode = 1;

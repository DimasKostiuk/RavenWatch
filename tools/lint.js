const fs = require("fs");
const dir = "C:/Users/kosti/Desktop/RavensWatch/src/";
const known = new Set(["name","killer","killer_gen","killer_ins","attacker","attacker_gen","attacker_ins","boss","boss_gen","boss_ins","top","top_gen","top_ins","top_genpl","prey","prey_gen","prey_ins","foe","foe_gen","foe_ins","count","fish","times","level","biome","biome_in","biome_here","biome_here_in","km","deaths_all","cause_times","tombs_times","bosses_all","kills_all","trees_all","mines_all","fish_all","fish_lost","built_all","portals_times","sleep_all","food_all","berries_all","jumps_all","streak_max","base_hours","km_all","air_km","sail_km","killer_kills","top_kills","killer_genpl","seen_enemy","seen_count","seen","seen_gen","seen_ins","seen_genpl","friend"]);
let issues = 0, total = 0;
for (const f of ["Defaults.cs", "DefaultsEvents.cs", "Quips.cs"]) {
  const lines = fs.readFileSync(dir + f, "utf8").split(/\r?\n/);
  for (const raw of lines) {
    const m = raw.match(/^\s*"(.*)",\s*$/);
    if (!m) continue;
    const t = m[1]; total++;
    const p = [];
    if (/[а-яіїєґ][a-z]|[a-z][а-яіїєґ]/i.test(t)) p.push("latin-mix");
    if (/[ыэъё]/i.test(t)) p.push("russian letter");
    if (/  /.test(t.replace(/^\[[^\]]+\] /, "")) && !/ = /.test(t)) p.push("double space");
    if (/ [,.!?]/.test(t)) p.push("space before punct");
    for (const ph of (t.match(/\{[^}]*\}/g) || [])) if (!ph.includes("|") && !known.has(ph.slice(1, -1))) p.push("unknown " + ph);
    if ((t.match(/\{/g) || []).length !== (t.match(/\}/g) || []).length) p.push("brace");
    if (/\{count\} (ворогів|істот|деталей)|\{fish\} риб/.test(t)) p.push("numeral agreement");
    if (p.length) { issues++; console.log(f + ": " + p.join(", ") + " :: " + t.slice(0, 120)); }
  }
}
console.log("lines", total, "with issues", issues);

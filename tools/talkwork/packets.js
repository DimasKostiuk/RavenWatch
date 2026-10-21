const T = require('../talkcheck.js');
const fs = require('fs');
const { UK, EN, scenes, options, listsIn, marked, tone, mark } = T;
const GROUPS = {
  A: ['idle_a','idle_b','idle_c','perch_a','perch_b'],
  B: ['rest_a','rest_b','rain','fog','night','snow','storm','dawn','dusk'],
  C: ['boss','near_death','death','biome','cross_h','cross_m','walk','sail','friend'],
  D: ['fed_hugin','fed_munin','gap_h','gap_m','dungeon','fall','heavy','portal'],
  E: ['fish','chop','mine','raid','trade','ride','smelt','ferment','repair'],
  F: ['potion','sit','cozy','trophy','trophy_hang','cart','plant'],
  G: ['x_intro','x_greet','x_rename'],
  H: ['x_pals','x_fire','x_boss'],
};
const PREFIX = { x_intro: 'intro', x_greet: 'greet', x_rename: 'rename', x_pals: 'pals', x_fire: 'fire', x_boss: 'xboss' };
const groupOf = id => Object.keys(GROUPS).find(g => GROUPS[g].includes(id));
const sid = s => s.cross ? s.id.split(':')[0] : s.id;
// lists per scene, and lines reached per scene
const reach = {}; // who:list:index -> Set(scene)
const usesList = {}; // who:list -> [scene]
for (const s of scenes) {
  const id = sid(s);
  for (const st of s.steps) for (const raw of UK[st.who].t[st.tag] || []) for (const x of listsIn(raw)) if (UK[st.who].p[x.name] && marked(UK[st.who], x.name)) { const k = st.who + ':' + x.name; (usesList[k] = usesList[k] || []); if (!usesList[k].includes(id)) usesList[k].push(id); }
  for (let t = 1; t <= 3; t++) {
    const seen = new Set();
    (function walk(i, thread) {
      const key = i + '|' + thread; if (seen.has(key) || i >= s.steps.length) return; seen.add(key);
      const st = s.steps[i]; const opts = options(UK, st.who, st.tag, t, thread);
      for (const o of opts) {
        const arr = UK[st.who].p[o.list];
        arr.forEach((l, j) => { const x = tone(l, t); if (x == null) return; const [mk, text] = mark(x); if (text === o.text && mk === o.mk) { const k = st.who + ':' + o.list + ':' + j; (reach[k] = reach[k] || new Set()).add(id); } });
        walk(i + 1, o.after);
      }
      if (st.opt || st.join) walk(i + 1, thread);
    })(0, null);
  }
}
// owner of every line in a talk list
const own = {}; // group -> who:list -> [j]
for (const k in usesList) {
  const [who, list] = k.split(':');
  UK[who].p[list].forEach((l, j) => {
    const r = reach[k + ':' + j];
    let sc = r ? [...r] : usesList[k]; // unreached (unmarked fallback, orphans): the first scene using the list
    const g = groupOf(sc[0]);
    ((own[g] = own[g] || {})[k] = own[g][k] || []).push(j);
  });
}
for (const g in GROUPS) {
  const out = [];
  out.push(`# Packet ${g}: scenes ${GROUPS[g].join(', ')}`);
  out.push(`\nThread prefixes you must use (every thread of a scene starts with its prefix, then a dot): ` + GROUPS[g].map(id => `${id} → «${PREFIX[id] || id}.»`).join(', '));
  out.push('\n## Scenes (steps; ? = optional)');
  const shown = new Set();
  for (const s of scenes) { const id = sid(s); if (!GROUPS[g].includes(id) || shown.has(id)) continue; shown.add(id);
    if (s.cross) out.push(`- ${id}: A open (x_${id.slice(2)}_open) → B reply (_reply) → [C join (_join, ~85%) → D join (~75%) → A or B answers the last joiner (x_join_reply)] → A close (_close, optional)`);
    else out.push(`- ${id}: ` + s.steps.map(st => `${st.who}${st.opt ? '?' : ''} ${st.tag} = ${(UK[st.who].t[st.tag] || []).join(' / ')}`).join('  →  '));
  }
  out.push('\n## Your lines (index in list | Ukrainian | English). You own exactly these; edit/delete them, and add new lines to any list your scenes use.');
  for (const k in own[g] || {}) {
    const [who, list] = k.split(':');
    const users = usesList[k];
    out.push(`\n### ${who} @${list}   (used by scenes: ${users.join(', ')})`);
    for (const j of own[g][k]) out.push(`${j} | ${UK[who].p[list][j]} | ${(EN[who].p[list] || [])[j]}`);
  }
  fs.writeFileSync(`packet_${g}.md`, out.join('\n'));
  console.log(g, out.length, 'lines');
}

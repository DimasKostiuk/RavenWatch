# RavenWatch

Pick your job, read its one page in full, and open more only where that page sends you. Work that spans two rows: read both pages.

| The job | Read |
|---|---|
| People's words: themes, talks, retellings | `docs/jobs/words-people.md` |
| The ravens' words (Hugin, Munin) | `docs/jobs/words-ravens.md` |
| A clip (a pose, an animation) | `docs/jobs/clip.md` |
| People's moves: a new or changed person, who may make a new clip | `docs/jobs/people-moves.md` |
| A scene | `docs/jobs/scene.md` |
| Story data: quests, stages, places, story people | `docs/jobs/story-data.md` |
| Code: a mechanic, a fix, the interface, a tool | `docs/ARCHITECTURE.md` (the map: the rules, «Перед словом «готово»») and, in full, the section of `docs/arch/` for the files you touch (grep the file's name in `docs/arch/`) |
| Building, installing, seeing it in game | `docs/jobs/build.md` |

Always:

- Before the first edit of a file read its header in full: lines found by grep are not a header read. If another chat changed a page, a section or a file you edit, read it again in full.
- Fix things in the core, not beside it; delete what a new system replaces. No needless heavy calls: where one is needed, make it predictable and as light as possible.
- The checks (`tools/arch_check.js`, `tools/storycheck.js`, `tools/npcwordcheck.js` and the rest) run in every build and stop what goes against the rules. Add an exception or raise a count there only with the user's clear consent.
- A green build proves that words match, not that anything runs: say «built, not seen in game» until it is seen, and add a line to its part in `docs/untested/` (the index and the rules: `docs/UNTESTED.md`; a part is searched and added to at the top of its section, never read whole).
- After changing how something is called (a registry, a split, a rename), grep every old call and walk one real data line from reading to running.
- Changed the core, a data format or a check: update the map, its section or the job page in the same change.
- A new or changed story person, and every new clip of people's life: go over ALL the people in `docs/jobs/people-moves.md` and decide for each whether the move fits them (the user, 2026-11-17). The build stops a clip with no `tone` and a person with no `shuns:` or `lively=`.
- People's and the story's words are born varied: what a person may say more than once is never one line (the numbers: `docs/jobs/words-people.md`).
- Every text written or changed is translated into English in the same change (the user, 2026-11-11): a menu or story text — `node tools/l10n.js sync English`, then its row in `assets/lang/English/ui.txt`; a people's theme or list line — `assets/lang/English/npc_patterns.txt`, as long as the Ukrainian; a raven's phrase — its English phrase file. The build stops on what is left (`node tools/l10n.js check English --strict`, `tools/bookcheck.js`).

# RavenWatch

Before changing any code, read `docs/ARCHITECTURE.md`: who owns what in the core, and the rules. Fix things in the core, not beside it; delete what a new system replaces. No needless heavy calls: where one is needed, make it predictable and as light as possible.

`tools/arch_check.js` and `tools/storycheck.js` run in every build and stop what goes against the rules. Add an exception or raise a count there only with the user's clear consent. `docs/UNTESTED.md` lists what nobody has seen in game yet.

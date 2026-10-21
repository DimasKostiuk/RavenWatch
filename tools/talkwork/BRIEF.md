# Brief: make the ravens' talks hold together

RavenWatch is a Valheim mod. Two ravens, Hugin and Munin, sit on the player's shoulders and talk to each other in short scenes. These talks are now voiced aloud, and many sound fake because an answer does not answer what was just said. Your job: rework the lines of your scenes so that EVERY possible chain makes sense.

## How a talk is put together (read carefully)
- A scene is a set of steps, for example `H open → M reply → H? close` (`?` marks an optional step, said ~50% of the time). Each step picks a random template for its tag, and a template is made of random lines from @lists.
- A line can start with a tone mark: `~1` (low trust, levels 0–3), `~2` (4–5), `~3` (6–7), or combined marks like `~23` and `~12`. A line without a tone mark fits every tier. Each raven speaks in the tier of its own trust, and the two ravens may be on different tiers.
- **Threads.** A line carries a thread mark after its tone mark: `~1 ^thread text`.
  - The opening line sets the talk's thread. Each later step only picks lines whose mark answers the current thread.
  - **Two levels** (new): a thread is `prefix.point`, e.g. `gap_h.слухає`. A line marked `^gap_h.слухає` answers only openings marked `gap_h.слухає`. A line marked with the bare prefix `^gap_h` answers ANY point of that scene, so it must make sense after EVERY opening of the scene.
  - **Chains**: `^a>b` means the line answers `a` and moves the talk on to `b`, so the next step must answer `b`. Use chains so that the close answers the REPLY and not only the opening. Example: an opening `^gap_h.слухає`, then a reply `^gap_h.слухає>gap_h.бубниш`, then a close `^gap_h.бубниш`.
  - Several answers: `^a|b` answers a or b. For example, `^gap_h.слухає|gap_h.пильнує>gap_h.страшний`.
  - Unmarked lines are no longer allowed in these lists. Every line must carry a mark with your scene's prefix.
- Placeholders are filled by the game:
  - `{name}` is the player and `{friend}` another player near.
  - `{food}`, `{boss}`, `{boss_gen}` and `{boss_ins}` are filled when the scene sets them.
  - `{м_слівце}` is a swear word picked in the middle of a line.
  - Player gender forms: `{він|вона}`, `упа{в|ла}`, `сам{|а}`. Split the form exactly where the two words start to differ: `вибра{вся|лась}` is right, `вибрався{|ась}` is wrong. Expand both forms in your head and check them.
  - In cross talks (players' ravens): `{me}` is the speaker's nickname, `{raven}` the nickname of the raven spoken to, `{addr}` its vocative, `{kin}` its kind (Хугін/Мунін), `{name}` the speaker's owner, `{friend}` the other raven's owner, `{>він|вона}` the other owner's gender forms. `{g_my_*}` / `{g_their_*}` are gossip facts. Keep any placeholder you find in a line exactly as it is.
- Monsters and bosses have unknown gender. Never use a gendered past verb or adjective for them ("воно впало" is fine, "він упав" about a troll is not). The player's gender always goes through `{m|f}` forms.
- A line that ends with a question forces the next step to be said. So a question must have answers on its thread at the next step, for every tier.

## What makes a chain "real" (the goal)
Each line must react to the concrete words of the previous line: its content, its claim, its question. A line that is merely on the same topic is not enough.

The user found these failures:
- "Я вже на ступінь вище" → «Слухає тебе? Бо ти бубниш…». Nobody said anything about listening.
- A close «Побачимо, хто кого» that has nothing to do with the reply before it.
- Hugin says «Не жери очима» to Munin, who is the one being fed.
- A talk of three ends right after someone joins, with an unrelated «Ну, бувай».
- An opening about honey, then «гілка», when the talk should have gone to the beehive.

The user wants dialogue that sounds like two characters actually talking to each other.

## Voices (strict)
- **Hugin**: a cold, clever smartass. Short, precise jabs, at least as biting as Munin but through wit. Plain spoken modern Ukrainian, never bookish ("утім", "як і належить" are out). Almost no swearing (a rare «дідько/трясця/чорт»). At ~3 he is warm, but only through the thorns, never with greeting-card sentiment.
- **Munin**: a lout (бидло). He swears hard and speaks in street surzhyk: «шо», «тіко», «ше», «бля», russisms like «ржать», «по-любому», «короче». These are intentional, so never "correct" them. No slurs about sexual orientation or nationality. Never repeat the same swear root twice in one line, and keep at most 2 swears per line (lines with 3+ get dropped).
- Length: like the existing lines (one or two short sentences). Shorter is better. These are spoken aloud.
- Tiers: ~1 is rude and cold, ~2 is in between, ~3 is fond (still rude for Munin). Each step must have enough lines for every tier: at least 2 that answer each thread at each tier. An unmarked-tier line counts for all tiers.

## Your task
Your packet `packet_<X>.md` lists your scenes (steps and templates) and the lines you OWN (the list index, the Ukrainian, the English). For each of your scenes:
1. Read every opening. Decide the points: group openings whose answers can be shared, and give the others their own point.
2. Rework the marks and the text of the replies so that each reply answers its opening(s). Chain each reply to a point the close answers (`>`), and make the closes answer the replies. Keep good lines and only re-mark them. Rewrite or delete lines that cannot fit. Add new lines where a thread lacks answers.
3. Rename every thread to your scene's prefix (given in the packet), e.g. `^хвальба` → `^gap_h.слухає`. Lines you own that sit in lists shared with other scenes (х_кінець, м_кінець…) must also get your prefix.
4. Unmarked lines you own: mark them for one of your threads if they fit, otherwise delete them.
5. Every line needs its English twin with EXACTLY the same tone mark and thread mark (only the text after the mark is translated). Munin in English is crude, Hugin dry and cutting.
6. Self-check: for every opening, read every reply that can follow it, and for every reply, read every close that can follow it, at each tier. Each pair must sound like a real exchange. Write 3 sample full chains per scene in your final message.

## Output
Write a JSON array to `edits_<X>.json` in the scratchpad talks folder (the same folder as your packet). Do NOT edit any project files.

```json
[{"who":"H","list":"х_кінець","op":"set","old":"<the Ukrainian line exactly as in the packet, marks included>","uk":"<new Ukrainian line>","en":"<new English line>"},
 {"who":"M","list":"м_ревнощі","op":"del","old":"<exact old line>"},
 {"who":"M","list":"м_ревнощі","op":"add","uk":"~1 ^fed_hugin.мало Мені — дуля, {м_слівце}?","en":"~1 ^fed_hugin.мало A fig for me, {м_слівце}?"}]
```

- `who` is H or M (whose list it is).
- `set` and `del` only for lines you own, with `old` copied exactly from the packet (between the `|` separators, trimmed).
- `add` for new lines, in any list your scenes use.
- Valid JSON with double quotes. Escape inner double quotes; better to avoid them and use «».

When finished, reply with a short summary: per scene the points you made, the counts of set/del/add, and 3 sample chains per scene. Write in English, with the samples in Ukrainian.

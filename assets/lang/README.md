# RavenWatch translations

The mod is written in Ukrainian; every other language is a set of text files. A language folder is named exactly as
the game names the language (`English`, `Polish`, `German`, `Portuguese_Brazilian`, …).

- **Built in** (shipped in the dll): `assets/lang/<Language>/` in the mod's sources.
- **Your own** (no rebuild): `BepInEx/config/RavenWatch/lang/<Language>/` — the same files; a line there wins over a built-in one.
- The language used is the game's, or the one set in the config: `[0. Language] Language = Auto | English | …`.
  A language without a line for some text shows the English one, and English without one shows the Ukrainian source.

## 1. The menus: `ui.txt`

One text per line: `Ukrainian source<TAB>translation`. Leave the right side empty for "not translated yet".

```
Хугін	Hugin
ще {0} с	{0} s left
день|дні|днів	day|days
```

- `\n` is a new line inside a text, `\t` a tab, `\\` a backslash; a line starting with `#` is a comment
  (a text that itself starts with `#` is written `\#…`).
- `{0}`, `{1}` … are values put in by the mod: keep every one, in any order your language needs.
  `{{` and `}}` are a plain `{` `}` (needed for `{he|she}` forms inside a text that has `{0}`).
- `{name}`, `{friend}` … (named) are filled later: keep them as they are.
- `{male|female}` pairs pick the player's form: `{him|her}`. No pair is needed when both are the same.
- Plural forms are one entry joined by `|`: two forms (`day|days`) or three (Slavic: `день|дні|днів`).
- Rich text tags (`<b>`, `<color=…>`, `<size=…>`) must stay balanced, the same as in the source.
- Names in «guillemets» that point at the mod's own things (gifts, tabs) should match their own translations.
- A raven's little words (its habits «Хм.», «Курва.», how it calls you, a few spare lines) may have no match in
  your language: write `-` as the translation and that word is simply not there (no Ukrainian word slips in).

Tools (in the mod's sources): `node tools/l10n.js sync <Language>` adds every text the code asks for (empty right
side), `node tools/l10n.js check <Language>` lists what is missing or broken (blanks, tags, formats).

## 2. The ravens' words (phrase files)

`hugin_patterns.txt`, `munin_patterns.txt`, `hugin_titles.txt`, `hugin_texts.txt`, `munin_titles.txt`,
`munin_texts.txt`, `hugin_event_titles.txt`, `hugin_event_texts.txt`, `hugin_tails.txt`, `hugin_quips.txt`,
`munin_quips.txt`, `creature_names.txt` (`dialogues.txt` holds no words: it is the same for every language).

The Ukrainian originals, line for line, come out of the built dll with `tools/langdump.ps1` (→ `build/l10n_source/`).
A language counts as having its own phrases once its folder has `hugin_patterns.txt`; until then it speaks English
ones (or Ukrainian, while English has none).

- Lists may be any length: 50 Ukrainian swears and 20 English ones are fine — a raven picks from what its language has.
  A template whose list is missing gives way to another; an optional part `{…?}` just drops out.
- Keep every `[theme]` tag and every `@list` name exactly as they are (`@матюк`, `{звертання}`…): they are ids,
  never shown. Translate only the words.
- Placeholders `{name}`, `{seen}`, `{seen_gen}`… keep their names. A language without cases may simply use `{seen}`
  where Ukrainian says `{seen_gen}` / `{seen_ins}`; `{…_genpl}` is the plural.
- `~1` / `~2` / `~3` at a line's start is the tone by trust (cold / warm / close): keep it.
- `creature_names.txt`: `key = name | of whom | with whom | of many` — in a language without cases write the same
  name three times and the plural last.
- The `# rev N` / `# patterns rev N` lines are written by the mod: leave them.

---

# Переклади RavenWatch (українською)

Мод написано українською, а кожна інша мова — це набір текстових файлів. Тека мови називається точно так,
як мову називає гра (`English`, `Polish`, `German`, `Portuguese_Brazilian`…).

- **Вбудовані** (їдуть у dll): `assets/lang/<Мова>/` у вихідних кодах мода.
- **Свої** (без перезбирання): `BepInEx/config/RavenWatch/lang/<Мова>/` — ті самі файли; рядок звідти важить більше за вбудований.
- Мова береться з гри або з налаштування `[0. Language] Language = Auto | English | …`.
  Якщо в мові нема рядка для якогось тексту, показується англійський, а без англійського — українське джерело.

## 1. Меню й підказки: `ui.txt`

Один текст на рядок: `українське джерело<TAB>переклад`. Права частина порожня — «ще не перекладено».

- `\n` — новий рядок усередині тексту, `\t` — табуляція, `\\` — зворотна риска; рядок із `#` на початку — коментар
  (текст, що сам починається з `#`, пишеться `\#…`).
- `{0}`, `{1}` … — значення, які підставляє мод: зберегти кожне, у будь-якому порядку, як треба мові.
  `{{` і `}}` — звичайні дужки `{` `}` (потрібні для форм `{він|вона}` у тексті, де є `{0}`).
- `{name}`, `{friend}` … (іменовані) заповнюються пізніше: лишити як є.
- Пари `{чоловіча|жіноча}` обирають форму за гравцем. Якщо форми однакові, пара не потрібна.
- Форми множини — один запис через `|`: дві (`day|days`) або три (слов'янські: `день|дні|днів`).
- Теги (`<b>`, `<color=…>`, `<size=…>`) мають бути ті самі й закриті, як у джерелі.
- Назви в «лапках», що вказують на речі мода (дари, вкладки), мають збігатися з їхнім власним перекладом.
- Дрібні слова ворона (примовки «Хм.», «Курва.», як він тебе кличе, кілька запасних реплік) можуть не мати пари у твоїй
  мові: переклад `-` означає, що такого слова просто нема (українське не вскочить).

Інструменти (у вихідних кодах): `node tools/l10n.js sync <Мова>` додає всі тексти, яких просить код (з порожньою
правою частиною), `node tools/l10n.js check <Мова>` показує, чого бракує чи що зламано (підстановки, теги, формати).

## 2. Слова воронів (файли фраз)

`hugin_patterns.txt`, `munin_patterns.txt`, `hugin_titles.txt`, `hugin_texts.txt`, `munin_titles.txt`,
`munin_texts.txt`, `hugin_event_titles.txt`, `hugin_event_texts.txt`, `hugin_tails.txt`, `hugin_quips.txt`,
`munin_quips.txt`, `creature_names.txt` (`dialogues.txt` слів не має: він однаковий для всіх мов).

Українські оригінали рядок у рядок дістає із зібраної dll `tools/langdump.ps1` (→ `build/l10n_source/`).
Мова має свої фрази, щойно в її теці є `hugin_patterns.txt`; доти вона говорить англійськими
(або українськими, поки англійських нема).

- Списки бувають будь-якої довжини: 50 українських матюків і 20 англійських — нормально, ворон бере з того, що є
  в його мові. Шаблон, чийого списку нема, поступається іншому; необов'язкова частина `{…?}` просто випадає.
- Кожен тег `[тема]` і кожну назву списку `@…` лишити точно як є (`@матюк`, `{звертання}`…): це ідентифікатори,
  їх ніхто не бачить. Перекладати тільки слова.
- Підстановки `{name}`, `{seen}`, `{seen_gen}`… зберігають свої назви. Мова без відмінків може просто писати
  `{seen}` там, де в українській `{seen_gen}` / `{seen_ins}`; `{…_genpl}` — множина.
- `~1` / `~2` / `~3` на початку рядка — тон за довірою (холодно / тепло / близько): зберегти.
- `creature_names.txt`: `ключ = хто | кого | ким | кого (багато)` — у мові без відмінків тричі та сама назва,
  а в кінці множина.
- Рядки `# rev N` / `# patterns rev N` пише сам мод: не чіпати.

# RavenWatch flight template test set

These are starter presets for `BepInEx\config\RavenWatch\templates` in the
`D:\Thunderstore\Valheim\profiles\RavenTest` profile. They are also included in
the RavenTest test-build archive. Open `raven test` → `Шаблони` and press
`Перечитати файли` after installation.

## Suggested first test

1. In `Шаблони`, choose Hugin or Munin, select `Коло — живий птах`, and leave
   `Цей шаблон` selected. Set the route to `Коло`; try camera `Навколо` or
   `Загальний`. Use `Швидкість перегляду` to slow down or pause the picture.
   The selection is forced in the preview, so the template's circle-only
   checkbox does not stop it from showing.
2. For an in-world outing, switch to `Як вирішить режисер` and set the preview
   situation to `Кружляння`. The director rolls among templates allowed
   for the selected situation, with `Вага «без шаблону»` also in the draw. If
   no template appears, check the situation and its allowed checkbox.
3. To guarantee it on your raven during a real circle, select the template and
   use `Грати на твоїх воронах у грі`, then start a circle from the flight tab.
   This override is global for your ravens and ignores allowed situations;
   switch it off after the test.

## Take-off

Take-off now aims toward the active flight plan instead of using one fixed
push direction. Follow, circle, scout, hover, tricks, and departures to a perch
each provide their first useful route point; the shared motor adds a smooth
upward push and some of the player's movement. Calm follow/circle flights gather
speed more gradually, while attacks and tricks use a shorter ramp. When the
Every launch from the shoulder uses the same brief shoulder-specific push-off,
including scouting, following, circles, hovering, orders, strikes, and the dome
gift: the bird compresses and extends its legs, pitches its body, and starts a
deep, synchronized wing stroke while the motor carries it upward and toward its
route. The procedural launch pose takes priority over flight-mode poses, then
blends into the sampled flight clip. Airborne handoffs do not restart the
push-off. Its state ends by itself and is also cleared when the bird lands.
Calm follow/circle flights gather speed more gradually, while attacks and
tricks use a shorter ramp. The game's landing clip is unchanged.

## Glide wingbeat

Level glides slow the wingbeat instead of pausing the flight clip. There is a
minimum clip rate, so the wings keep moving through the stroke rather than
holding a possibly low pose for the length of a glide.

## Jump wingbeat

Shoulder ravens use a short, eased wing wave when the player jumps or lands.
The shoulder starts the stroke before the elbow and wrist follow, with a
per-raven phase, amplitude, delay, and beat count. This replaces the former
single-angle flap; it does not change the separate powered leap flight.

## Tuning a dive template

In `raven test` → `Шаблони`, select the template you want to tune and press
`Пікірувати з «... »`. This activates that template only for the commanded dive
and sends the normal in-game dive order. The selected template's `Поза` is
enabled for the test if it was off; the template itself is not saved until you
press `Зберегти шаблон`. The 1–10 buttons under `Поза` are points across one
wingbeat (top to bottom, then back up), not the ten flight situations. Select
`Плече`, `Лікоть`, or `Кисть` and adjust each point; the right wing mirrors the
left. Changes are visible immediately in the preview and in a running test.

`Ізолювати позу` is on by default for the dive test. It suppresses the motor's
automatic dive fold, adaptive wing correction, and speed/effort changes to the
beat, so the authored ten-point pose can be judged consistently. The commanded
dive path and body orientation remain motor-driven. Turn isolation off to
compare the same template with the normal adaptive dive animation. The test
uses the ordinary order, including its strength cost and cooldown; `Вимкнути`
removes the dive-only override.

## Situations and dive test

The director uses one of the ten situation checkboxes. The new `Ниряння`
situation is selected for a commanded dive and for fast, steep descents
(vertical speed below −2.5 m/s at over 7 m/s). The dive preset folds the wings
back; the shared wing pose suppresses most of the normal up/down stroke and
sweeps the wings rearward, with a small residual movement for life. This applies
even when the director does not select a template. During a dive, the director
chooses an eligible dive template instead of the usual chance to fly plain.
The motor also tucks the legs a little tighter. The same leg pose has slight,
individual, out-of-step motion during flight, with both legs drawn farther back
toward the tail; the in-flight leg motion is deliberately slow and subtle so it
does not twitch. Near a landing the motor eases the legs forward to reach for
the shoulder.

The wing panel includes a `Ниряння` adaptive preview so the tucked pose can be
checked from above without relying on a live dive.

During `dance`, `relax`, `rest`, sitting, or sleeping, a raven leaves the
shoulder, climbs farther clear, and hovers with its flight animation until the
pose ends. The emote list is configurable; sitting and sleeping are detected
directly as well.

To test the dive preset in the world, select `Ниряння — складені крила` and
enable `Грати на твоїх воронах у грі`, then choose the dive trick in the
`Розворот` tab/radial orders. For the director, turn forced play off: the
`Ниряння` checkbox is chosen automatically during the dive. The final landing
clip remains game-controlled; the dive pose is for the steep approach before
the motor marks its final landing stage.

An attack sent into empty space now changes from its initial climb to the
`Ниряння` situation for at least two seconds before returning home. The motor
holds the wings folded during this segment even if the flight does not reach
the automatic steep-descent threshold.

## Preset assignments

- `Планер — спокійний`: return, follow, scout.
- `Ривок — стрімкий`: attack, scout, take-off.
- `Віраж — широкий`: scout and dome (kept out of circle so it does not compete
  with the dedicated circle flight).
- `Зависання — активні крила`: hover/dance.
- `Захід — м'яке гальмування`: home and arrival before the final landing clip.
- `Коло — живий птах`: circle.
- `Ниряння — складені крила`: commanded or detected fast descent.
- `Висхідний пошук`: circle and scout. A slow vertical wave and stronger
  stroke pace give the raven a searching, climbing rhythm; the body, neck,
  head, and tail shift gently with the wingbeat.
- `Перемінний супровід`: follow. The existing motor alternates short powered
  strokes with glides; this preset increases glide opportunities while keeping
  each flight's phase timing independent, with a light body roll and tail
  counter-motion.

`Коло — живий птах` is restricted to the circle situation. It combines the
motor's curved, inertial steering and turn-driven body bank with a slight
vertical drift, stroke-synced body/head/tail motion, slower wing tempo and
occasional slower glide beats. The wings never pause mid-stroke. Templates do
not define flight paths or dynamic physics;
the flight plan and motor still provide those. Every motor-driven flight also
shares a subtle body pitch, head counter-motion, tail balance, and wing effort
response to climbing, acceleration and braking. The remaining presets are
starting points; the motor adds global leg motion and descent/landing responses.

The final shoulder landing keeps the vanilla landing clip and its authored
wing poses. Each landing gets a small independent clip-speed variation (±6%),
while the motor continues to map the clip's progress onto the approach path.
This gently varies timing between ravens without changing the landing pose or
adding per-frame random checks.
The motor also caps turn speed across flight plans, including template turn
multipliers; only close-in precision maneuvers receive a limited higher cap.
Tune any value in the preview before saving a copy.

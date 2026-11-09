# Umbreon

Reference: https://unite-db.com/pokemon/umbreon (current detailed formulas),
`Assets/Moves/Dumps/umbreon_skills.json`, and `age_197_*` declarations under
`Assets/Moves/Dumps/age-named-v2/3c44b23`. AGE times are provisional evidence,
not a verified execution trace; primary Foul Play movement scripts are missing.

## Agreed behavior

- Eevee and Umbreon use the same physical three-attack sequence. Boosted attacks
  animate a lunge without moving the gameplay root, hit a small area, and heal
  10% missing HP once on a successful attack, not once per enemy. Only basics crit.
- Swift fires four homing stars. The fourth applies a cleansable four-second
  mark; proximity grants 15% speed and subsequent damage readies the next boost.
- Anticipation/Inner Focus negates Espeon's condition set, including slows,
  burn/poison, plus physical displacement. Bound bypasses this passive, including
  its own unstoppable state. Inner Focus does not reflect damage. Forty-second
  cooldown resets on respawn; evolution preserves the running cooldown.
- Fake Tears is a full four-unit circle, not a speculative semicircle.
- Mean Look uses a four-unit cast range and a stationary two-unit confinement
  radius. Pending application can be cleansed during 0.5s; formed confinement
  cannot be cleansed or escaped via walking, dashes, teleport/Eject or knockback.
  Only the original target is confined. Umbreon inside gains empowered basics
  against any enemy. Victim/caster death and despawn remove the zone.
- Foul Play has two piercing wall-crossing dashes, with a six-second follow-up
  window only after a first-stage hit. The 7.5s cooldown starts when the follow-up
  finishes or the window expires (immediately after the initial dash on a miss).
  The first stage snapshots the highest victim Attack and own Attack at first
  hit. If the victim snapshot was higher, the follow-up uses that snapshot;
  otherwise it uses current own Attack. Each upgraded activation readies a boost.
  Move 1's secondary cooldown ring shows the remaining follow-up window, using
  its server deadline, and clears on follow-up use, expiry or effect cleanup.
- Wish targets an ally or falls back to self. Death transfers to the lowest HP
  percentage living allied player within ten units, including Umbreon, without
  restarting the two-second deadline. A committed Wish survives caster death.
- Snarl locks basics/other moves but leaves walking and battle items available.
  Eight pulses, capped six small Attack/Sp. Atk stacks, plus the separate final
  shout. Each small stack retains its own 1.5s duration; final reduction lasts 3s.
- Moonlight Prance leaves movement at 50% during its leap and permits battle items.
  Initial theft removes half of eligible existing shields without dealing damage;
  a following five-second aura intercepts half of new grants. Rayquaza and Assault
  Vest are excluded. All stolen contributions share one absolute expiry, three
  seconds after the aura ends; repeated theft never adds duration. Transferred
  shields do not recursively create new shield-grant events.
- Learning Unite enables 1% max-HP healing each second while the existing combat
  timer reports out of combat. Four allied Eeveelution licenses give +1% max HP.

## Integration and tuning

`UmbreonMoveRules` centralizes mutable geometry/speed/timing. Current provisional
defaults: basic range 2.5, small-AoE radius 1; dashes 4.5/3.5 units over 0.25s,
hit radius 0.75; follow-up sector 2.5 units/120 degrees and 1.5-unit shove over
0.6s. Dump-derived ranges: Swift 6, proximity 3, Mean Look 4/2, Wish 7/10,
Fake Tears/Snarl 4, Unite landing/theft 4/4.5. Times and formulas are seconds
and zero-based `CurrentLevel`, respectively.

The existing move/basic/passive factories and server-owned prefab framework are
used, not a replacement combat engine. New enum values are appended. Optional
ally/self targeting, displacement interception, shield interception/extraction,
and cast-owned movement constraints are shared opt-in hooks. Existing movement
entry points constrain their destinations, with a final server/owner guard for
external transform-driven movement. Other characters' normal mechanics retain
their previous behavior. All peers need matching scripts/enum values.

`Tools/Characters/Umbreon/Create Missing Move Assets` authors seven network move
prefabs, reusable gold/violet/dark editable effects, materials, icons, previews,
learnset, registry and Addressables/network registrations. It preserves existing
prefab edits, stats, models, selection data and animator. No runtime primitive,
mesh or LineRenderer generation. Eevee Swift maps to its existing `spell1a` state.

Rebuild Addressables before testing built players or existing-content play mode.
Presentation follows the project's fixed-roster RPC model, not late-join recovery.

## Validation

`Unite.Umbreon.Tests`: factories, art, registration, zero-based progression,
formulas, tunable ranges, passive classification and animator-state availability.
`Unite.Umbreon.PlayMode.Tests`: host and dedicated-server fixtures covering
passive exceptions, Swift/boost interaction, stationary basics, Mean Look cleanse
and confinement, Foul Play hit-confirmed recasts, Wish death transfer, Snarl ticks,
shield extraction/exclusions/deadlines and live grant interception cleanup.

Manual follow-up: all four move combinations with a real remote client; aim/self
selection, Eject during leap and confinement, wall traversal, animation polish,
fog-of-war effect visibility and built-player Addressables loading.

Validation: 23 Umbreon EditMode tests and 22 Umbreon host/server PlayMode tests
passed. The earlier combined Umbreon/Espeon/melee PlayMode run passed 81/81.
The broader six-assembly EditMode run passed 199/210; its remaining 11 failures
are three battle-item cooldown expectations, six obsolete Glaceon-area reflection
tests and two reserve-UI prefab assertions, outside this character implementation.

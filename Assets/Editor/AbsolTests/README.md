# Absol moveset: reference fidelity

Runtime tuning lives in `Assets/Scripts/Moves/Absol/AbsolMoveRules.cs`. Reference
files are `Assets/Moves/Dumps/absol_skills.json` and the Absol declarations under
`Assets/Moves/Dumps/age-named-v2/3c44b23/`.

## Geometry and movement

The implementation uses the project's existing distance conversion (1000 dump
units per Unity unit), treats AGE `MSize` as full box dimensions, and keeps
target-acquisition/indicator ranges separate from collision geometry.

| Behaviour | AGE declaration | Implemented geometry |
| --- | --- | --- |
| Basic A1/A2, Slash | `age_359_A1`, `age_359_A2`, `age_359_S2` | Box 3 × 10 × 4, forward offset 1.5 |
| Boosted A3 | `age_359_A3` | Box 3 × 10 × 4.5, forward offset 1.5 |
| Boosted follow-up | `age_new_359_A3B1` | Independent box 3.5 × 10 × 7, forward offset 3 |
| Pursuit dash | `age_359_S12` | Five-unit travel cap; three-second empowerment |
| Pursuit basic A4 | `age_359_A4` | Box 3 × 10 × 4, forward offset 1 |
| Psycho Cut projectile | `age_test_359_S11B1` | Swept box 3 × 10 × 3, travel cap 7, speed 18 |
| Sucker Punch retreat | `age_359_S22` | Backward travel cap 2; bounded wall-crossing traversal |
| Unite flurry | `age_359_U0` | Box 5 × 10 × 8, forward offset 4.5 |
| Unite finale | `age_new_359_U1B1` | Four expanding 70-degree sectors, radii 2.5/5/7.5/10, origin offset -0.5 |

Psycho Cut includes its final travel segment even if a frame passes its maximum
flight time. The boosted follow-up retains the existing isolated-main-hit gate,
but checks its own larger volume instead of repeating damage on the original
victim. Unite rechecks current contacts for each expanding pulse. Its existing
full-damage first contact, reduced repeat hits and once-per-victim shove are
preserved, including for enemies entering after the first pulse.

Aim art remains the active-skill dump's art, not a replacement collision shape:
Slash's sector indicator does not imply a sector hitbox, and Unite's eight-unit
indicator does not cap its finale collision radius.

## Remaining uncertainty

The export is a declaration reader, **not a verified execution trace**. See its
`README.md`. Time units, conditions, defaults, dependency handling, and some
nested parameter interpretations have not been recovered. Consequently:

- Existing attack/cast timings are retained, including evenly spaced Unite
  flurry hits and its existing finale spacing. Raw AGE times are not presented
  as proven milliseconds. The newly restored retreat uses a provisional 0.15s
  duration from S22's raw `Length=150`, within the existing one-second stance.
- AGE collision sizes/offsets are a closer reference-backed model, not a claim
  that all native collision semantics (including A3B1's rotation field) have
  been recovered.
- Damage coefficients, critical-hit bonuses, isolation conditions, healing,
  cooldown reductions, and buff magnitudes retain their existing values. Their
  external effect tables are not included in these dumps.
- Feint and Night Slash AGE scripts, Pursuit+/Psycho Cut+ scripts, and Sucker
  Punch impact scripts `age_test_359_S22B1/B2` are absent. Those missing scripts
  cannot establish exact upgrade or impact behaviour.

## Regression coverage

`Assets/Editor/AbsolTests/AbsolTests.cs` compares collision sizes, movement
parameters and finale radii directly with the AGE export, as well as checking
active-skill cooldowns, indicators, energy, camera settings and icons.

`Assets/Editor/AbsolTests/PlayMode/AbsolServerTests.cs` exercises both server and
host, including box-versus-sector boundaries, the boosted follow-up's independent
contacts, Pursuit wall crossing and cleave, Psycho Cut endpoint contacts and
deduplication, Sucker Punch retreat/death cleanup, and live expanding Unite
contacts.

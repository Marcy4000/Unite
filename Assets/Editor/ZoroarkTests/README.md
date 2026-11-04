# Zoroark moveset: dump fidelity

References: `Assets/Moves/Dumps/zorua_skills.json` and the `age_570_*`
declarations in `Assets/Moves/Dumps/age-named-v2/3c44bc3/`.

## Spatial changes

As in the project's other movesets, distances use 1000 dump units per Unity
unit and AGE `MSize` is treated as full box dimensions, not half extents.
Indicators, movement maximums and collision volumes are separate parameters.

| Behaviour | Declaration | Implemented geometry |
| --- | --- | --- |
| Basic A1/A2/A3 | `age_570_A1/A2/A3` | Box 3 × 10 × 4, forward offset 1.5 |
| Slash | `age_570_S1` | Swept box 2.5 × 2.5 × 3, forward offset 0.5; travel 4 |
| Fury Swipes | `age_570_S2` | Swept box 2 × 10 × 3.5, forward offset 1; travel 3.5 |
| Shadow Claw path | `age_570_S11` | Swept box 2.5 × 10 × 5.4, forward offset 0.8; travel 4.4 (indicator 4.5) |
| Shadow Claw endpoint | `age_570_S11` | Separate box 2.5 × 2.5 × 3, forward offset 2; shares the path's damage gate |
| Cut | `age_570_S12` | Swept box 2.5 × 10 × 6, forward offset 1; travel 5 (indicator 4.5) |
| Initial Feint Attack | `age_570_S22` | Swept box 2.5 × 10 × 5, forward offset -0.8 |
| Night Slash cross | `age_570_S21B2B/S21B3B` | Two perpendicular diagonal boxes 8 × 4 × 2.5; resized cross art |

Queries retain the project's combat-height offset. Feint's negative offset uses
the export's signed-int32 interpretation of the serialized translation. Sweeps
cover the actual traversed segment, including both endpoints, and resolve damage
only once per victim per dash. Shadow Claw checks current endpoint contacts;
interruption prevents its endpoint phase. Night Slash retains one damage instance
per victim across both arms and one healing trigger per contacted arm.

Feint's indicator switches from 7 initially to 5 during return recasts and back
to 7 for its targeted finisher, including on remote owners. Returning to the
actual mark is deliberately not clamped to the indicator radius: the corresponding
return-movement script is missing. Cleanup clears the replicated phase state.

## Limits and retained behaviour

The AGE export is a declaration reader, **not a verified execution trace**.
Its README states that timing units, defaults, conditions and dependency handlers
are not recovered. These spatial changes are a closer reference-backed model,
not a claim that native collision or execution semantics are fully recovered.

- Attack/cast timings, Shadow Claw's existing path-stun/throw durations, cooldown
  start semantics and Illusion's existing selection logic are unchanged.
- Shadow Claw's separate endpoint box still executes at dash completion; the raw
  AGE activation times are not converted into an asserted runtime schedule.
- Night Slash's special basic/movement collision and Feint's return/finisher
  collision retain their previous shapes where their scripts are absent. The
  normal basic box is not extrapolated to the missing special-basic script.
- Damage/heal coefficients, heal multiplicity, crit flags, missing-HP scaling,
  wild caps and Unite buffs retain existing values: their effect tables are absent.
- `age_570_G`, many upgraded/movement scripts, and Feint's targeted finisher are
  absent. The supplied files cannot establish their exact runtime behaviour.
- The authoring preview order now agrees with the skill dump (Cut S2A, Night
  Slash S1B); the already-correct existing preview references are preserved.

## Regression coverage

`ZoroarkTests.cs` compares movement maxima and full box dimensions directly with
the readable AGE declarations, and checks initial/return/finisher indicator ranges
against the active-skill JSON. It also checks the persisted cross artwork.

`PlayMode/ZoroarkServerTests.cs` runs on both host and dedicated server: it covers
movement distances, box-versus-cone/capsule boundaries, single-frame swept
contacts, multiple-collider deduplication, allied filtering, Shadow Claw's
independent endpoint, outer cross arms, healing gates and Feint phase cleanup.

# Zoroark

Reference: https://unite-db.com/pokemon/zoroark, detailed RSB data retrieved
October 29, 2026; local targeting dump: `Assets/Moves/Dumps/zorua_skills.json`.
Detailed notes/formulas take precedence over the short descriptions. Spatial
values below were agreed with the project owner, not measured original hitboxes.

## Integration and tuning

- `ZoroarkMoveRules.cs` centralizes geometry, phase timing, damage/healing formulas,
  status IDs and animation names. Distances are Unity units; damage formulas use
  zero-based `CurrentLevel`. Cooldowns are declared in the move definitions.
- Melee: 2.5 range / 120 degrees. Slash 4; Fury Swipes 3.5; Shadow Claw/Cut 4.5;
  Night Slash movement dash 4 and special-basic range 4.5; Feint initial dash and
  finisher targeting 7. Dashes have 0.75 hit radius, 0.2s travel, 0.035s windup,
  and 0.06s recovery. All use bounded wall-crossing traversal.
- Shadow Claw's final 1-unit region throws. Night Slash's cross is two centered
  6-by-1.5 strips rotated +/-45 degrees. Unite radii are 2.8, 3.5 and 4.2.
- Learn levels are 1/3, branches at 5/7, Unite 9, upgrades 11/13; evolution at 5.
  Existing enum values and supplied models/controllers were preserved.
- Moves use `PrefabMove` / server-owned `PrefabMoveEffect`; basics use the shared
  server attack path and input buffer. Combo state is owned by the active cast,
  not by the HP bar. No alternative cooldown/damage/movement framework is added.
- Seven effect prefabs, MoveAssets, registry and Addressables/network entries are
  authored. Imported Zorua/Zoroark Animator clips supply all character animation.
  Baked slash/ring meshes, colored materials and particles follow Duraludon's
  editable Graphics-child pattern; no runtime meshes/primitives are constructed.
- `Tools > Zoroark > Create Missing Move Assets` creates missing assets only and
  preserves later prefab edits. Rebuild Addressables before using an existing
  content build, and deploy updated clients and servers together.

## Combo rules

- Every third ordinary basic is boosted (130% Attack). Only basic-classified
  attacks can crit. Normal basic charge is separate from the combo gauge.
- Fury Swipes offers exactly one recast within four seconds after hitting.
- Night Slash activates a 2.5s mode. Movement input, including held input, uses
  the available dash. A successful dash grants a special basic; a successful
  special basic or slot-A move refreshes the window and re-enables the dash.
  Holding/changing movement direction while awaiting that special basic does
  not cancel the chain or require a neutral stick. The next dash waits until the
  attack recovery ends. Movement can end a failed chain with neither a dash nor
  special basic available; expiration and hindrances also end it. Six successful
  attack/cast steps guarantee the finisher, including when the sixth step is a
  dash. Multiple victims never grant extra steps.
- The finisher cleanses conditions and grants 1.4s invincibility. It lands after
  1.2s. Movement can steer the jump at 1.5 units/s, up to 1.5 units of total travel,
  respecting walls. Steering stops on landing/death and pauses during slot A's
  explicit reposition so the two movements cannot fight each other. The cross
  hits at the final steered position. Slot A can perform one damage-free 4-unit aerial reposition, even while
  its normal move is cooling down; this neither resets nor extends that cooldown.
  Other actions remain locked. Each victim takes cross damage once, even in the overlap. Each diagonal
  which contacts an eligible victim grants one heal (maximum two, even if the
  single damage instance KOs a victim). Damage classification follows the sixth
  step: move damage or basic damage. Wild damage is capped at 1000.
- Feint hits reset only slot A's cooldown. Recasts return to the actual origin
  mark; slot A moves that mark to its cast origin. Each successful move grants
  one step and refreshes its three-second window. Four steps offer a targeted
  finisher: normal target aiming chooses the victim, movement chooses the bounce
  direction, and no movement falls back to continuing forward through the target.
  After selection, a stationary 0.15s anticipation animation precedes the lunge;
  the target is rechecked afterward, and the 0.5s unstoppable phase starts at launch.
  The leap and bounce cannot damage the same victim twice; wild damage caps at 1200.
- Cut heals per enemy hit. All recast/chain cooldowns start when the chain ends,
  misses, expires, or is interrupted. Feint/Night upgrades reduce that cooldown
  by one second. Death/despawn cleans up cast locks, traversal and presentation.
- Unite roots movement/basics/normal moves for 1.33s, but permits battle items.
  Pulses sample the caster's position, so Eject Button repositions later pulses.

## Reusable UI

- `AuxiliaryAction` is a disposable local binding with icon, availability,
  cooldown and activation callback. `AuxiliaryActionUI` is an optional generic
  HUD/mobile button, wired into both existing layouts and hidden without a
  provider. Illusion binds the existing input asset's new **AuxiliaryAction**:
  **C / left-stick click**. Saved bindings use the normal InputManager flow.
  Its frame, cooldown artwork, lock and font match the current `MoveUI` prefab.
  `Tools > Zoroark > Update Auxiliary Action Style` refreshes only that button's
  style; create-missing authoring preserves later style edits once migrated.
- The existing MoveUI secondary ring shows Fury Swipes' 4s second-use window,
  Night Slash's 2.5s chain window and Feint's 3s mark/finisher window. Hits refresh
  the deadline; committing a recast/finisher, expiry, death or cleanup clears it.
  These are recast timers, not the full move cooldown which starts after the chain.
- `SegmentedGaugeModule` extends `GaugeModule` through the existing HP-bar module
  API. `SetSteps(count, capacity)` clamps state and pools thin black separator
  Images. Its prefab is `Assets/Resources/HPBarModules/SegmentedGauge.prefab`.
  Night uses six segments and Feint four; full/ready changes color. It coexists
  with the normal boosted-attack counter and does not own combat/network state.

## Illusion

- Server selects the nearest living opponent, prioritizing opposing players
  without requiring vision; falls back to wild models. Fifteen-second cooldown
  and duration, a condition/debuff cleanse and 0.33s unstoppable.
- Availability is independent of scoring/goal-zone eligibility; match-wide
  action disabling, active scoring/recall and cast locks are still respected.
- Copies the model, enemy-facing name, level, HP/shield/energy snapshot, bar
  dimensions/style, portrait and minimap styling. Wild disguises use the wild
  minimap dot. Copied HP is not subscribed to the original's live HP.
- The original team, stats, collider, targeting, match roster and actual vision
  eligibility never change. Allies retain real HP/name information; enemies see
  the copied ally presentation. The clone follows Zoroark's own visibility, not
  the source's visibility; sampling a hidden opponent does not reveal its location.
- Appearance breaks on damage, basics, move commitment (including automatic
  Night dashes), score attempts, recall, evolution, timeout or death. Presentation
  overrides do not rebind gameplay objects. Hidden HP modules retain their live
  state rather than restoring stale combo visibility when the disguise ends.
- Like existing effect presentation, RPC state assumes the project's fixed roster;
  reconstructing an in-progress disguise/chain for late joiners is not supported.

## Validation

EditMode: `Unite.Zoroark.Tests` covers definitions, formulas/caps, learnset,
asset registrations, segmented gauges, generic auxiliary-action lifecycle and
view-only HP overrides. PlayMode: `Unite.Zoroark.PlayMode.Tests` runs both NGO
dedicated-server and host fixtures for multi-target healing/counts, recasts,
finishers, invulnerability, basic attacks, Illusion selection/snapshots and death.
Regression suites: shared moves, HP bars, Duraludon, Absol and melee targeting.

Manual remote-client checks: learn both branches; controller/mobile aiming;
Night movement/basic handoff and aerial reposition; Feint mark relocation and
bounce direction; enemy/ally disguise appearance and restoration; Eject during
Unite; death/disconnect during Addressables loads; readability of authored effects.

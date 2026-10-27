# Absol

Reference: https://unite-db.com/pokemon/absol, detailed RSB data retrieved
October 27, 2026. Detailed formulas take precedence over the shorter summaries.

## Tuning

**Edit `AbsolMoveRules.cs`.** No tuning ScriptableObject or Inspector configuration
is required. The top of the file groups melee reach/angle, dash distance/bow/
radius/duration, recast timing, projectile dimensions/speed, counter ranges,
Unite rectangle/cone dimensions, distance bands and animation phase timings.
Damage formulas and status IDs are centralized in the same file. Levels passed
to damage formulas use the project's zero-based `CurrentLevel`.

Unspecified Unite-DB dimensions use the agreed project-scale approximations:
2.5-unit melee; 4-unit curved/straight dashes with 1.5-unit bow and 0.75-unit
hit radius; 4-unit Pursuit hop and 5-unit empowered reach; 7-unit Psycho Cut;
6-unit counter acquisition / 10-unit break range; 4-by-6 Unite rectangle and
8-unit, 60-degree final cone. Final hit-count bands are 2/4/6/8 units. These
bands approximate the documented close-to-far 4/3/2/1 behavior, not measured
original-game boundaries.

### Combat feel

All pauses are seconds and live in `AbsolMoveRules`:

- `BasicWindup` / `BasicRecovery`: 0.20 / 0.26. Recovery now means time **after**
  damage, not total animation length. Boosted basics use `BoostedWindup` (0.22),
  `BoostedHitDelay` (0.08), and `BoostedRecovery` (0.26). The animation spans the
  whole sequence; these attack timings scale down with attack-speed cooldown.
- `BasicInputBuffer`: 0.18. Move A/B and Unite presses/releases near the end of
  a basic are buffered without greying the move slots. The latest press wins;
  held basic attacks cannot steal a buffered cast. Cancel, death, expiry and
  move replacement discard the input. Legacy characters retain their own buffers.
- `FeintWindup` / `FeintRecovery`: 0.04 / 0.06.
- `NightFirstWindup` / `NightFirstRecovery`: 0.04 / 0.06.
- `NightSecondWindup` / `NightSecondRecovery`: 0.09 / 0.08. The second dash
  stays stationary during its windup; this is separate from `NightRecastDelay`.
- `PursuitWindup` / `PursuitRecovery`: 0.035 / 0.065; the empowered attack has
  separate `PursuitAttackWindup` / `PursuitAttackRecovery` (0.04 / 0.14).
- `PsychoWindup` delays the blade by 0.10. Movement/actions are locked through
  `PsychoAnimationDuration` (0.35) plus `PsychoRecovery` (0.08), independently
  of projectile flight and the five-second mark lifetime.
- `SlashWindup` / `SlashRecovery` remain independently tunable; `SuckerRecovery`
  adds 0.08 after impact without extending its unstoppable state.
- `BasicAnimationBlend` / `MoveAnimationBlend`: 0.06-second visual crossfades.
  Basic blends scale down with attack speed along with their animation timing.
  `RecoveryAnimationBlend` / `UniteAnimationBlend`: 0.10 seconds for returning
  to idle and transitioning into/between Unite phases. Zero restores instant
  playback. Blends overlap the existing animations and never add cast time,
  delay damage or extend movement/action locks. Other characters opt out by default.

Zero disables an individual windup/recovery. Your configured animation names
and spatial tuning are not reset by these timing controls.

Curved aiming uses the imported `pref_Indicator_Arc_R_2m5_lv3` (mirrored for
left arcs); Unite uses `pref_Indicator_Arrow_2dir_lv3`, with their authored
materials. Each imported child has Y scale `-1`, below a normalized sizing
holder so runtime range/width scaling cannot overwrite the face correction.
Slash uses `pref_Indicator_Sector_120_lv3` to match `SlashAngle`, normalized
uniformly to its configured range without distorting the sector angle. Its
supplied texture atlas is assigned on `SlashAimSector.mat`, leaving the FBX
import settings untouched. When authoring new sector art, the filename is
selected from `SlashAngle` (use one of the available authored angles).
`AbsolAssetAuthoring.UpdateAimIndicators()` migrates only the old procedural
footprints and preserves subsequent layout/material edits.

## Kit and agreed behavior

- Super Luck is an idempotent permanent +15-point crit modifier. Cleansing and
  death do not remove it; passive disposal releases it.
- Every third basic is boosted, cleaves, lowers Defense by 15% for two seconds,
  and gains the single-target bonus only when exactly one eligible victim is hit.
  Its main hit and isolation bonus are separate damage ticks. The delayed bonus
  rechecks the original victim's life, range and angle; it cannot hit a departed
  target or retarget a different one.
- Feint alternates curved paths, crosses walls, and bypasses Defense/shields.
  Feint and both Night Slash hits grant the 60-point attack-speed buff on hit.
- Night Slash offers its second, straight dash only after a confirmed first hit.
  Recast is delayed 0.15s and remains available for six seconds. Its full 5.5s
  cooldown starts after the second dash completes, a miss, or window expiry.
  Upgraded second-hit healing includes shields and wild targets, but not overkill.
- Pursuit replaces the next attack regardless of charge and resets charge to
  zero when used. The leap can cleave clustered enemies. Each victim's backstab
  is checked against its current facing at impact; any confirmed backstab
  reduces remaining cooldown by 70%. Owner-only rear highlights expire with
  the empowerment. Upgraded speed ends on attack or after two seconds.
- Psycho Cut pierces, applies the 90/65/40/15% decaying slow, and marks each
  victim three times for five seconds. Direct Absol damage, including Unite
  ticks, consumes marks; item damage and secondary mark damage do not. Marks
  are source-specific, cleansable, expire independently, and grant upgraded
  movement speed irrespective of distance. Multiple marked enemies/casts do
  not stack that movement-speed buff.
- Sucker Punch reacts to incoming direct attacks or its target committing an
  offensive action. Calm Mind, recovery/buff-labelled legacy moves and Pursuit's
  preparatory hop do not count. The first triggering damage instance is rejected
  before shields/HP are touched. Existing stat/status interceptors also reject
  same-frame follow-up debuffs from that hit. Legacy debuffs have no attacker
  metadata, so unrelated same-frame debuffs cannot be distinguished there.
  The counter cancels on target KO/untargetability or excessive separation.
- Midnight Slash locks every action, including battle items and recall, from commitment
  through the second animation. The gameplay root never moves: imported clips
  supply the apparent forward/backward movement. Five rapid ticks hit the whole
  fixed rectangle, splitting each tick's damage among eligible targets. The
  final cone hits once at maximum range and up to four times close up; repeat
  hits deal 30% damage. It shoves and grants the reference six-second speed/shield
  buffs. Invulnerability/untargetability cover the flurry; documented unstoppable
  phases cover the opening and finale. No other dashes gain unstoppable.

## Shared integration

There is no Absol-specific player controller or alternative cooldown/status
system. Small opt-in additions to the existing systems provide:

- `DamageInfo.bonusCritRate` and `DamageSource` (legacy defaults are unchanged).
- Synchronous server `Pokemon.ResolveDamage`, a post-resolution damage event,
  offensive-action notifications and permanent-stat ownership.
- Optional server-resolved basic attacks, server/owner attack presentation and
  opt-in `BufferedMoveInput` handling instead of disabling move slots during basics.
- `PrefabMove.CooldownOnEffectEnd`, effect completion/recast notifications,
  pending-load cleanup and optional immediate commitment locks.
- Disposable `CombatActionLock`, existing `WallCrossingTraversal`, reusable
  displacement, sector/swept targeting, stepped debuff decay, custom aim prefab
  presentation, opt-in animation duration matching and conditional recovery.

The damage network payload changed: rebuild/redeploy clients and servers
together. Existing enum values are preserved; new moves/passive are appended.

## Assets and validation

Seven effect prefabs live under `Assets/Prefabs/Objects/Moves/Absol`, with baked
meshes/materials and editable Graphics children like Duraludon. Melee, mark and
rear-highlight presentation lives in `Assets/Resources/Effects/Absol`; custom
aim footprints live in `Assets/Resources/AimIndicators`. No runtime primitives,
meshes or line renderers are constructed. `AbsolAssetAuthoring.Build()` is an
editor-only, create-missing authoring helper; it preserves existing prefab
layouts and the imported model/controller. Aim radius/length scales follow
rules at runtime. After changing the baked sector angle, run **Tools → Absol →
Create Missing Move Assets** to refresh the generated indicator mesh without
overwriting prefab layouts or your learnset.

Moves have icons/previews, registry entries, and network/Addressables entries.
Learn levels are 1/3, 5/7, Unite 9, upgrades 11/13. Rebuild Addressables before
testing an existing content build.

Tests: `Unite.Absol.Tests` and `Unite.Absol.PlayMode.Tests`. The latter runs real
NGO dedicated-server and host fixtures. Also regression-test Sylveon, Duraludon,
shared moves and melee targeting after shared combat changes.

Manual host + remote-client checks: aiming/cancellation on each input device,
Night Slash cooldown/recast HUD, Pursuit charge display/back highlights, cross-
wall landings/interruption, countering another license's attacks and offensive
moves, all Unite animation phases, simultaneous Absol marks, and death/disconnect
during pending Addressables loads. Presentation follows the project's existing
fixed-roster RPC model rather than reconstructing effects for late joiners.

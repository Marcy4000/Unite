# Cinderace

Reference: https://unite-db.com/pokemon/cinderace, detailed RSB data retrieved
October 23, 2026 (the page fetches its data from `/pokemon.json`).

## Implemented

- Full seven-move learnset, both level-7/8 branches, Unite at 9, upgrades at
  11/13. Project asset levels are zero-based. `CinderBlazeKick` was appended to
  the enum; the legacy `BlazingBycicleKick` spelling/value is preserved.
- Server-owned basic/boosted attack cycle (100%/140% Attack), persistent boosted
  readiness after successful moves, 1000/1300 wild damage caps, and critical hits.
- Source-specific, cleansable cinders: normal attacks add one; boosted attacks
  and direct moves add two. Five stacks consume all stacks and flare for
  90% Attack + 25 + 0.7% target max HP per zero-based level (500 wild cap).
- Blaze's 50%-HP threshold, +10 critical-rate points, +20 attack-speed points,
  5s duration, 30s cooldown. Server evaluation also works for remote players.
- Ember/Pyro Ball/Blaze Kick burns: three 20%-Attack ticks, 5% Attack/Sp. Attack
  reductions for 2s. Mark and burn IDs are leased from 15000–19999. The narrowly
  scoped `PlayerManager` exception prevents its legacy generic DoT from dealing
  additional, self-attributed damage for these burns. Other burns are unchanged.
- Low Sweep/Flame Charge query the actual travelled path once per target, with
  explicit movement-phase acknowledgements. Flame Charge+ readies a one-use
  30%/2s slow on the next boosted projectile.
- Pyro Ball pierces, burns, and reduces mobility cooldown by 4s once per upgraded
  cast that actually damages an opponent (not once per collider or burn tick).
- Blaze Kick approaches the target, guarantees a critical hit, burns, shoves for
  0.5s, rebounds using movement input or backward by default, and remains
  unstoppable until landing. The upgrade adds 30 attack-speed points for 3s.
- Feint cleanses removable speed reductions, grants 0.73s invincibility and
  40% movement speed for 2s. Its upgrade readies three non-critical attacks,
  each healing 30% of actual HP damage (in addition to normal lifesteal).
- Blazing Bicycle Kick: unstoppable windup, a giant projectile toward the target
  location at launch, an AoE explosion with 14% missing-HP damage, 10% movement
  speed for 5s on hit, and 6s Unite buffs (30% movement speed, 35 attack-speed
  points, 20%-max-HP shield). Damage is tagged as Unite damage.

## Architecture and assets

`MovesController.Cinderace` owns server cooldown validation, attack state,
marks/burns, and per-request async loading. `CinderMoveBase` owns local aiming,
action-status leases, animations and traversal. `CinderMoveObject` owns each
cast's server hitboxes, replicated graphics, lifetime and Addressables handle.
Casts commit their cooldown/energy only after the prefab is loaded. Death and
cancelled pending requests invalidate async callbacks even across respawns.
Periodic and secondary damage use `Unused_2` to prevent cinder recursion;
`Unused_1` identifies direct moves and boosted basics that apply two cinders.

Art follows Duraludon's convention: run **Tools > Cinderace > Create Missing Move
Prefabs** to author missing editable prefabs and register Addressables/NGO.
Existing layouts are preserved. All rings are baked into `Cinderace/Art`;
materials live in `Assets/Materials/Cinderace`. No match-time geometry is built.
Attached effects follow the target's visibility; cinder pips are caster-only and
do not expose hidden enemies through fog or bushes.
The old generic CinderPyroball/basic-attack prefabs remain untouched because
other characters may reference them. Rebuild Addressables before built-player
testing or using an editor configuration backed by a previous content build.

## Approximations and checks

Unite-DB does not specify world-space range, projectile speeds, dash distances,
or frame timings. These use the project's scale: basic range 7, Ember/Pyro Ball
range 8, Kick range 7, Unite range 11 / blast radius 3, 5-unit movement dashes,
3-unit rebound. Kick approaches in 0.25s and rebounds in 0.25s. Pyro Ball retains
the existing 0.4667s release / 1.1s animation. These are centralized in the rules
or serialized hitboxes. Effects are authored placeholders, not extracted VFX.
Feint healing uses real HP lost, excluding shields and overkill. Marshadow's
pre-existing approximate copied Unite is unchanged.

Run EditMode assembly `Unite.Cinderace.Tests` for mechanics/formulas, stack
expiry/consumption, factories, icons, learnset, upgrade thresholds, serialized
hitboxes/art, animation states, and network/addressable registrations.

Manual host + remote client checklist:
1. Learn each branch and upgrade; check basic/move/boosted cinder sequences.
2. Burn players and wild Pokémon, cleanse/refresh burns, and check three ticks
   credited to the caster without triggering more cinders.
3. Dash through several opponents and over walls. Check one hit each, then
   Flame Charge+'s next-boosted slow and Pyro Ball+'s cooldown reduction.
4. Kick a moving opponent, choose rebound directions, and test immunity,
   guaranteed criticals and attack-speed upgrade.
5. Dodge during incoming damage; verify the 0.73s window, slow cleanse, three
   upgraded heals and disabled critical hits on those projectiles.
6. Unite into several wounded enemies; check explosion damage and buffs.
7. Die, cancel aiming, get stunned, or level up during casts/pending loads.
   Check no leftover action locks, immunity, marks, particles, or ghost hits.

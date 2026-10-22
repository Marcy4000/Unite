# Vaporeon rework

Custom ranged sustain/control Supporter. Base stats and Unite energy are unchanged.
`VaporWhirlpool` **replaces the unused `VaporHaze` enum name at serialized ID 26**;
all subsequent move IDs retain their original values.

## Kit and tuning

Player-facing levels: Swift/Helping Hand at 1/2; evolve and choose Aqua Ring or
Rain Dance at 4; Dive or Whirlpool at 6; Turbo Tempest at 8; upgrades at 10/12.
The asset and `Pokemon.CurrentLevel` use zero-based levels.

- **Basic attacks:** 6-unit range, water projectiles, every third attack boosted;
  charges expire after 4.5s without attacking. Gauge and owner-only ready halo.
  Server tracks charge, attack interval and hits. Correct Eevee/Vaporeon clips.
- **Swift:** exactly four 1-unit-radius volleys, 0.7s apart. 7.5s cooldown.
  Uses the same camera-facing yellow star artwork as Jolteon/Flareon, with falling
  stars, a warm-gold boundary and a sparkle chime: no water spray while still Eevee.
- **Helping Hand:** +35% Attack/Sp. Attack for 3.5s, ally only. 9s cooldown.
  No valid ally means no cast/cooldown. Support spells do not root Vaporeon.
- **Aqua Ring:** target ally or self fallback, independent 20%-target-max-HP
  shield, eight original-strength heal pulses (first at 0.15s), 6s lifetime;
  +25% Attack/Sp. Attack for at most 4.5s. Shield break ends the ring, its buffs
  and pending heals. Recasting replaces this caster's ring on that recipient;
  different casters own separate shields/regeneration. 12s cooldown.
  Upgrade: 25% shield.
- **Rain Dance:** grounded, wall-respecting 4-unit hop over 0.35s and 0.1s recovery.
  3-unit-radius rain lasts 10s; +55% allied defenses, healing every 1.2s, 20%
  enemy slow. 12.5s cooldown. Upgrade: +65% defenses.
  Cooldown is committed before hopping, so cancelling/interruption cannot grant
  free movement. Hard hindrances during the hop cancel the pending rain field.
- **Dive:** 3s mobile invincibility, recast after 0.25s, then 0.25s resurfacing
  warning, damage/0.25s incapacitation/short 0.6-unit shove within 2 units.
  Other actions are locked, except the recast. Cooldown starts on emergence:
  10s, upgraded 9s. While submerged, uses the no-walls walkmesh and direct movement
  to cross both terrain and physical wall colliders, like the wall-crossing dashes.
  Substeps prevent leaving map boundaries or skipping gaps in the no-walls mesh.
  Only the model sinks; root height, flight, controller enabled state and shared
  dash state are not changed. On emergence (manual or automatic), resolve a clear
  footprint within 2 units, falling back to the last grounded point from this Dive.
  The server validates the exit and uses that point for both the warning and hit.
  Reset/death/despawn releases traversal and restores normal wall collision.
- **Whirlpool:** 7-unit placement, 2.5-unit radius, 3s duration, 11s cooldown.
  Six pulses total **0.6 SpA + 6 x level + 198** before defense/rounding;
  this is the whole-cast budget. 25% slow; one inward pull at 0.9s, capped at
  1.25 units. Target must still be inside. No additional stun or explicit heal.
  A shared 1.5s per-target pull immunity prevents overlapping Whirlpool chains.
  Objectives/hindrance-immune enemies cannot be displaced. Upgrade: 35% slow.
  Normal special-damage spell vamp still applies.
- **Turbo Tempest:** fixed 6-unit-radius center; 0.6s interruptible anticipation,
  hit/heal, 0.15s recovery. Both enemy defenses reduced by 35% for 5s. Living
  player allies (including self) are the only healing recipients; an invalid
  enemy never falls through to healing. Damage has the Unite property. Marshadow's
  copied Tempest retains Attack scaling and now matches the 35% reductions.
- **Hydration:** after evolution, successfully shielding/buffing or actually
  healing another ally grants 10% personal movement speed for 1.5s, once per 6s.
  Self-casts, overhealing and every individual regeneration tick cannot spam it.

Tune `VaporMoveRules.cs` first. Ground-target and recipient previews use authored
effect prefabs. A 0.18s recovery-window input buffer lets follow-up moves flow
without bypassing cooldowns, hindrances or Dive's underwater restrictions.

## Gameplay and lifecycle

`MovesController.Vaporeon.cs` receives owner-only cast RPCs, validates the selected
branch/level/range/cooldown, and runs damage, shields, healing and field membership
on the server. Timed effects never depend on Addressable loading. Queries resolve
child hitboxes and deduplicate Pokémon before applying an effect.

`VaporActionLock` owns scoped status leases and one movement restriction, rather
than clearing shared flags or unlocking all actions. Move resets are idempotent;
there are no root-position tweens or untracked move coroutines. Server cast scopes
cancel on death/despawn and dispose even work delivered after cancellation.

`VaporModifierLease` combines Vaporeon contributions by **strongest wins** per
target/stat/buff direction. Rain and Whirlpool slows never add together. Removing
one field leaves the other field's contribution intact. Shield/modifier IDs are
leased from 60000–61999, outside Jolteon's mark pool and below Marshadow's IDs,
not shared constants or truncated network IDs. Occupancy
is reconciled every 0.1s, including death, despawn and immunity changes. Caster
death/despawn ends its active fields, protection and owned debuffs.

The shared owner-driven cooldown/Unite-energy system is retained. Server regular
move acceptance has a small latency tolerance; this is not a replacement of the
project's entire cooldown/energy authority model. Target displacement uses the
existing owner-movement architecture, with wall-respecting explicit movement.

## Authored presentation

Run **Tools > Vaporeon > Create Missing Rework Prefabs** to create missing assets.
The editor-only builder preserves existing prefab layouts and creates baked
ring/spiral/ribbon meshes, turquoise/deep-water/foam URP materials, particles,
short placeholder water audio and a Whirlpool icon/preview. All 18 effect prefabs
live in `Assets/Prefabs/Objects/Moves/Vaporeon/Rework/`; materials live in
`Assets/Materials/Vaporeon/`. Tune them directly in Prefab Mode.

Effects are local cosmetics driven by server presentation RPCs, **not network
hitbox objects**, so they require Addressables registration but no entries in
`DefaultNetworkPrefabs`. No runtime geometry is generated. Delayed loads use the
server effect age; cancelled/expired requests release their handles without
showing stale effects. Persistent attached effects respect target visibility.
Hostile area boundaries use both a contrasting color and perimeter teeth.
All clients prewarm the effects when a Vaporeon is initialized, including enemy
Vaporeons, rather than waiting for that client's first hostile telegraph.

The original Vaporeon network prefabs/scripts remain for serialized compatibility;
new casts do not spawn them. The legacy Tempest healing filter is corrected too.
Presentation follows the existing fixed-roster flow, not late-join restoration.
**Rebuild Addressables content before testing a built player or Use Existing Build.**

## Validation

Run EditMode assembly `Unite.Vaporeon.Tests` in `Assets/Editor/VaporeonTests/`.
It covers serialized compatibility, factory/asset/registry/learnset wiring,
upgrades, protection/hostile filtering (including invincible enemies), source
leases, late cancellation, aim clamping, deduplication, damage budgets, authored
prefabs, Addressables and animation bindings. These do not simulate a remote client.

Manual host + remote-client acceptance:

1. Learn each branch and upgrade. Exercise all four combinations; verify
   attack–support–attack weaving and buffered Whirlpool/Tempest follow-ups.
2. Cast Helping Hand without allies and Aqua Ring on empty ground. Verify costs,
   self fallback, recipient preview, eight heal pulses, shield break, replacement,
   target KO/disconnect and independent protection from two Vaporeons.
3. Enter/leave overlapping rain/whirlpools. Verify strongest slow, no lingering
   defenses, six damage pulses, one pull per field and shared pull immunity.
4. Escape Whirlpool before collapse; test Unstoppable, Hindrance Resistance,
   invincibility and objectives. Pull/shove must stop at walls/map boundaries.
5. Cross walls with Dive; test manual and automatic emergence inside thin and thick
   terrain, map edges, interruptions and repeated casts. Verify a safe exit with
   no permanent wall phasing. Rain/pulls/shoves must still stop at walls.
   Confirm action locks,
   warning timing, short shove, cooldown and correct model/root restoration.
6. Interrupt Rain during its hop and Tempest during anticipation; KO/despawn at
   every cast phase. Verify no ghost healing/damage, stale shields, move locks,
   movement restrictions or shifted models. Tempest's spent Unite energy is not
   refunded after a committed interrupted windup.
7. With slow/missing cosmetic loads, confirm gameplay is unchanged and expired
   effects never appear late. Check both clients' visuals, audio, visibility,
   shield break, boosted feedback and freshly built Addressables.

Balance values and generated audio/VFX are a playable first pass, not final art
or validated competitive tuning.

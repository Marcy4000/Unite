# Emboar rework

## Identity and controls

Physical melee All-Rounder. Displayed evolution levels are 5/7, branches 5/7,
Unite 9, upgrades 11/13; serialized levels are zero-based. Original asset GUIDs,
move enum entries and the `EmboarReversal` internal Flamethrower identity remain.
Basic attacks intentionally have **no animation integration**: the current model
has no usable basic-attack animations. The server still resolves their forward
area, natural third-hit boost and Fire Pledge's confirmed-swing charges.
Vision-controller colliders are excluded from combat queries. Every basic-attack
victim is range-checked at its current root position, including secondary cleave
targets, so stale physics poses after death/teleport cannot damage a remote player.

- **Fire Pledge:** 5s of +25% movement speed and +20 attack-speed points. Three
  connected swings are boosted, with a 0.55s third-swing launcher on all targets.
  Empty attacks do not advance it; expiry clears the launcher. Upgrade: 7s CD.
- **Flamethrower:** 0.18s release / 0.5s recovery, point-blank-inclusive volume,
  0.35s stun, 0.5s Unstoppable. Upgrade grants a 10%-max-HP shield for 3s.
- **Heat Crash:** wall-safe 0.45s leap, immediate landing hit and two widening
  waves. Reckless launches only on landing. Upgrade: 0.2s rather than 0.28s wave
  interval and 30% rather than 25% wave slow. No six-wave duplicated damage/CC.
- **Flame Charge:** 1.15s steerable sprint, 0.4s acceleration. Recast to brake;
  after 0.08s recovery another move/attack may follow. Hits each target only once.
  Reckless starts at full speed. Upgrade grants +20% speed for 2s after the run.
- **Inferno Ringbreaker:** ground-aim up to 5 units, 0.2s brace, 0.3s drive, radial
  slam, control returned at 0.78s. Rush + slam total 340% Attack + 650 physical
  damage before defenses, without crit. Inner 2.4-unit area: 0.8s launch; outer
  3.5-unit area: 35% slow for 1.5s. Grants a 15%-max-HP shield for 4s and brief
  Unstoppable. An opposing-player slam grants one ready Reckless use. No steroid,
  homing grab, invulnerability or repeated hard CC. Energy cost stays 100000.

Reckless gains 8/2 points per player/non-player damage event and 5 when damaged,
with per-target outgoing and global incoming 0.25s throttles. Ready lasts 15s;
only Heat Crash/Flame Charge consume it, with a 5s rearm. Snapshot at commitment
is passed into the effect; delayed landing never reads a spent passive. Death
clears the meter and Pledge. The early 20% Defense bonus is reapplied on respawn.

## Ownership and authoring

`MovesController.Emboar.cs` validates learned branches, serials, action windows,
cooldowns, targets, finite coordinates and bounded movement reports. As with the
other custom licenses, character movement is owner-driven. Every move loads its
own Addressable and initializes its NetworkObject directly on the server; no
shared spawn callback. A failed or cancelled load cannot spend resources or
resurrect an old cast. `EmboarMoveBase` uses disposable action leases and a 150ms
follow-up buffer. Cooldowns/energy are paid at accepted commitment, not expiry.

`EmboarMoveObject` owns server queries, hit deduplication, phase synchronization,
death cleanup and Addressables handles. Graphics never determine damage.
Marshadow uses the same Ringbreaker prefab through an explicit adapter, its own
movement and Attack, and receives no Reckless or foreign animation requests.

Run **Tools > Emboar > Create Missing Move Prefabs** to author missing assets and
register their exact runtime addresses/network hashes. Existing layouts are
preserved. All meshes/materials, flame shapes, warnings, Pledge pips and short
original procedural sound clips are baked assets, not generated during matches.
The old Heat Crash/Smog/Reversal prefabs remain for legacy references; rewritten
move factories use the new `*Object.prefab` assets.

**Tools > Emboar > Render Ringbreaker Preview** renders the actual model/effect in
an isolated preview scene and links the resulting sprite to the Unite asset.
It does not replace or save the user's open scene.

## Verification

EditMode suite: `EmboarTests`, plus Duraludon, Marshadow and Cresselia regressions.
PlayMode suite: `EmboarLifecycleTests` verifies scoped-lock and visual-lift cleanup.
`EmboarBasicDedicatedServerTests` and `EmboarBasicHostTests` exercise the real basic
RPC/damage pipeline with overlapping vision sensors, stale post-respawn colliders,
out-of-range primary IDs, legitimate melee cleaves and Pledge charge consumption.
Before balance sign-off, host + remote-client playtests should cover:

1. Pledge → Charge → early brake → three connected attacks; no charge loss on air.
2. Reckless Heat Crash → buffered Pledge/Flamethrower; landing launches exactly once.
3. Walls and map edges during every driven move, including high-latency reports.
4. Death/interruption/evolution during pending loads, leap, sprint and Unite.
5. Two Emboars casting concurrently; no mixed spawns, stacked slows or orphan VFX.
6. Ringbreaker misses, inner/outer hits, shield expiry and one-time meter refill.
7. Marshadow borrowing Ringbreaker, including cancellation and death.

Live multiplayer balance and visual readability still require human playtesting.

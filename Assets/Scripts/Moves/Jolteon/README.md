# Jolteon rework

## Scope

Jolteon remains a physical melee Speedster. `JoltBasicAtk`, `JoltSwift`,
`JoltSwiftProjectile`, and their prefabs/assets are intentionally unchanged.
Learnset and serialized move/passive enum values are preserved: evolution and
first branch at 4, second branch at 6, Unite at 8, upgrades at 10/12.

## Flow and tuning

- Charge preserves the full Volt Absorb meter for an offensive follow-up. It
  applies a net 75% movement-speed reduction for **0.3s**, before the shared
  movement-speed efficacy tax, instead of a movement restriction/root. Its own
  speed buff is compensated during preparation, but other buffs still apply.
  Base/empowered attack and defense durations are 5/6s; speed lasts 2.5/5s.
  Charge+ adds 10 percentage points to both defensive buffs.
- Fang has a 0.18s startup and a 4-unit, 0.2s collision-aware lunge. Empowerment
  is spent only on the first bite and retained for its one 4s follow-up. A hit
  grants 60% movement speed for 1.5s (2s upgraded). No tween/coroutine survives
  death. Hard movement-control effects interrupt the pass without erasing the
  cooldown. The second bite cannot reopen a third, including during overcharge.
- Discharge has 8/10-unit normal/empowered travel. A direct hit earns one 3s
  follow-up; its travel and electric-field radius shrink by 30%, and its speed
  multiplier rises from 2.5 to 3.5. It measures actual displacement, stops when
  blocked, and times out after 2s. Speed is evaluated during travel so Charge's
  preparation slow does not persist for the entire rush. The field ticks on
  entry and every 0.45s; each second-pass contact slows by 20% for 1s (35% upgraded).
  Direct contact retains the existing 0.35s incapacitation, not a physical knock-up.
- Electroweb travels 9 units at 12 units/s. Its 3.5-unit net widens to 5 units
  when empowered. Server sweeps deduplicate targets; marks last 4s (5s upgraded),
  dealing damage every 0.6s. A direct move hit grants a refreshing, non-stacking
  30% speed boost for 1s (1.5s upgraded). Basic attacks and periodic/secondary
  damage cannot trigger this boost. Marks are source-specific; reapplication by
  the same source replaces its mark, never another Jolteon's. Each mark owns a
  distinct timed Scriptable status; removal of that status stops its damage.
  Mark IDs use 40000..59999, outside Duraludon's existing mark ranges.
  `Unused_1` explicitly tags Jolteon's direct move damage and `Unused_2` its
  periodic damage, so unflagged held-item procs cannot accidentally grant speed.
- Lightning Blitz **retains wall bounces**, using a pre-movement sphere sweep
  and normalized reflection. Movement input steers it on keyboard/controller.
  It lasts 3.5s, costs Unite energy on activation, and rewards only normal
  completion with the existing shield/speed plus 5s of protected full meter.
  Death forcibly clears overcharge even during that protection.
- Volt Absorb keeps its 2/s idle, 6/s moving, 8/4 player/non-player hit gain and
  20/player-KO gain. Damage gain is limited to 24/s; a target can grant it once
  per 0.25s, or once per 1s for periodic damage. All additions clamp immediately.

`JoltMoveRules` centralizes ranges, startup, windows, effect durations and IDs.
`JoltChargeState` and `JoltRecastState` isolate the resource rules for testing.
Recasts are temporary earned windows, not `StoredUses` reserves. Full cooldown
begins on second-pass completion, window expiry, or interruption after commitment.
Cancelling aim spends nothing and does not erase an earned follow-up.

## Integration and presentation

The project retains owner-controlled movement and dash damage requests; this is
not a full server-authoritative player-combat migration. `MoveWithWalls` applies
the existing wall walkmesh validation to driven motion, without changing other
characters' dashes. Dash queries sweep each frame to avoid skipping targets.

`MovesController.Jolteon` adds an owner-only server RPC for cast-scoped effects.
It does not touch `onObjectSpawned`, so overlapping Charge/Web effects cannot
consume Swift's legacy spawn notification. `JoltEffectRequest` captures each
cast, invalidates pending loads on source/marked-target death, and retains its
Addressables handle until destruction. Spawn failures release handles and log
an error rather than leaving movement/action restrictions waiting for an object.

`JoltEffectObject` owns server-side Web/mark gameplay and replicated presentation.
Other effects follow the actor's local replicated transform on each client.
Attached auras and marks follow the actor/target's local Vision visibility,
so persistent electricity cannot reveal an enemy hidden in a bush or invisible.
Presentation follows the existing fixed-roster RPC flow, not late-join restoration.
The legacy Web/Discharge/Charge/Trail prefabs/scripts remain for compatibility,
but reworked casts use only `Jolteon/Rework` assets. Swift still uses its original
prefab and spawn path.

`Tools/Jolteon/Create Missing Rework Prefabs` authors baked ring/bolt meshes,
yellow-white/cyan materials, lattice nets, marks, jaw arcs, speed trails and sparks.
It preserves existing assets and hand edits. Graphics are editable in Prefab
Mode and hidden until initialization. The builder registers Addressables and
persists distinct NGO spawn hashes. Rebuild Addressables for built-player tests
or editor setups using existing content builds.

## Validation

EditMode: `Unite.Jolteon.Tests` covers meter/overcharge/death rules, recast limits
and deadlines, reflections, factories, cooldowns, mark upgrade duration, baked
graphics, hitbox wiring, network identity and Addressables registration.
Also run `Unite.Duraludon.Tests` and `Unite.Moves.Tests` for regressions.

Manual host + remote-client checklist:

1. Check all four branch combinations at 4/6 and upgrades at 10/12.
2. At full meter use Charge immediately into Fang/Discharge: Charge must permit
   movement during its slow and leave the next offensive cast empowered.
3. Miss/hit Fang, cancel recast aiming, and release a second cast just before
   expiry. Repeat during Unite overcharge; never permit a third bite.
4. Hit/miss Discharge, change second-pass direction, and brush targets with only
   the smaller field. Check walls, corners, low FPS and the 3s window boundary.
5. Web multiple colliders/enemies, reapply it and use two opposing Jolteons.
   Only direct move hits should grant speed; ticks must not self-trigger it.
   Remove a mark status and kill/respawn its target while the prefab is loading.
6. Bounce Lightning Blitz off straight walls and corners, steer using movement
   input, and die mid-rush or during overcharge. Energy must already be spent.
7. Repeat casts with delayed/failed Addressables loads. Die during Fang startup,
   Charge, Discharge, Web and Blitz; check no late movement, locks or orphan VFX.
8. Confirm Swift and the three-hit basic attack behave exactly as before.

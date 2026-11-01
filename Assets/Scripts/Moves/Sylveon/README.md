# Sylveon

Reference: https://unite-db.com/pokemon/sylveon, including the site's detailed
RSB formulas/notes (retrieved October 22, 2026). Where the old summary and RSB
disagree, the detailed RSB takes precedence, as in Duraludon's implementation.

## Moves

- Mystical Fire: the shared dash, four orbiting homing flames (five upgraded),
  enemy-player targeting priority, 28% repeat-target damage, a refreshing Sp.
  Attack debuff capped at 60%, and 12% remaining-cooldown reduction per hit.
- Draining Kiss: three projectile round trips with damage on arrival at the
  enemy and healing on arrival at Sylveon. Its 50% slow decays to 35% halfway
  through its one-second duration. Upgrading increases healing by 50%.
- Baby-Doll Eyes: sure-hit targeting, a fairy projectile, and the reference
  Attack/speed debuffs instead of the old Pyro Ball placeholder.
- Calm Mind: three-second stat buffs, reference cooldown/speed, and upgraded
  one-move damage blocking before shields/HP are consumed. Blocking grants a
  three-second shield; it does not prevent separately applied hindrances.
- Fairy Frolic: cleanse/invincibility, landing damage and 60% HP-damage healing,
  then ten seconds of 50% HP-damage healing. The detailed RSB includes basic
  attacks. Shield absorption and overkill do not contribute to this healing.
  Landing damage against wild Pokemon is capped at 2000. The six-second Unite
  movement/CDR buffs and max-HP shield use ordinary stats/shields.
  A ground warning reuses the legacy Unite circle during the airborne wind-up,
  follows Sylveon, matches the five-unit damage radius, and hides on landing.
- Adaptability/Pixilate: damage dealt/taken refreshes a 1.5-second window with
  up to four 5% stacks; evolved Sylveon also gains Sp. Defense.
- Basic attacks retain their damage/three-hit sequence, with authored fairy
  projectiles. Evolved boosted attacks grant the speed buff on impact, not cast.
- Swift's existing implementation is retained, with its projectile hitbox radius
  enlarged from 0.2 to 0.5 world units without changing the graphics or damage.

**Hyper Voice retains its intentional mechanics:** its prefab, damage (`0.32f, 6, 110` close / `0.54f, 8, 184` far), range (`5f`),
cooldown (`7f`), six-to-seven-wave upgrade and existing displayed level-10 upgrade
remain as before, now routed through a thin `CombatMove` bridge and server-authoritative `SylveonExecution` / `HypervoiceHitbox`.

## Shared integration

Sylveon runs on the shared `CombatActor` architecture:
- All seven moves (`SylvSwift`, `SylvBabyDollEyes`, `SylvHyperVoice`, `SylvMysticalFire`, `SylvDrainingKiss`, `SylvCalmMind`, `SylvUnite`) are thin `CombatMove` input/aim bridges (`CooldownOnFinish => false`, `PrefabPath => null`).
- Swift, Hyper Voice, and Fairy Frolic retain action restrictions but allow movement both while awaiting server acceptance (`LockMovementDuringLoad => false`) and during execution. Other moves retain their existing movement-lock policies.
- `SylveonCharacterRuntime` owns the server-authoritative 3-hit basic attack cycle (including Eevee vs. Sylveon animations and boosted attack speed buff), `Adaptability` / `Pixilate` passive stacks, Mystical Fire dash traversal, Hyper Voice channel state/gauge, and presentation snapshots (`SylveonSnapshotRPC`).
- `SylveonExecution` executes all seven moves on the server via `CombatEffectSpawner` and retains independent `Addressables` handles (`HoldAsset`) on long-lived projectiles and effects so cast recovery completion never despawns projectiles in flight or releases their assets prematurely.

The enum name `SylvMysticalCringe` is retained for serialized compatibility;
its runtime/UI name is Mystical Fire. Learnset rows keep their existing levels
and now contain both branches, including the appropriate upgrades.

## Art and tuning

`Tools/Sylveon/Create Missing Move Prefabs` creates missing assets and preserves
hand-edited prefab layouts, following `DuraludonPrefabBuilder`. The prefabs have
editable `Graphics` children, baked meshes, materials, particles and serialized
hitboxes. Runtime scripts do not construct geometry. Network prefab hashes and
Addressables registrations are persisted. Existing legacy prefabs are retained.

The source does not specify Unity-scale ranges, projectile speeds, flame
lifetime or all animation timings. These approximations are centralized in
`SylvMoveRules`: a 3-unit leap, 5-unit flame acquisition, 5-second flame window,
6-unit Kiss cast range, 9-unit break range, and 12-unit/second projectile travel.

## Validation

- EditMode suite: `Unite.Sylveon.Tests` in `Assets/Editor/SylveonTests`.
- PlayMode suite: `Unite.Sylveon.PlayMode.Tests` in `Assets/Editor/SylveonTests/PlayMode`.

Manual host + remote-client checks still required:

1. Learn each branch at levels 4/6, Unite at 8, and upgrades at 10/12.
2. Fire against one/multiple targets, with and without enemy players present;
   verify flight, repeat damage, stacking reduction and cooldown reduction.
3. Move toward/away from Kiss; cross its break range and kill/despawn the target.
4. Test Calm Mind+ against normal/boosted attacks, multi-hit moves and a move
   applying crowd control. Only the first move-damage instance is blocked.
5. Test Frolic against shields, low-HP targets and wild Pokemon; verify landing
   healing, subsequent healing and cleanup on caster death/disconnect.
6. Die during delayed Addressables loads and while effects are active. Overlap
   Fire/Kiss/Calm Mind and verify they do not consume legacy spawn callbacks.
7. Regression-check the retained Hyper Voice variant and boosted-attack speed.

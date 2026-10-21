# Duraludon moves

Reference: https://unite-db.com/pokemon/duraludon (retrieved October 17, 2026).
The detailed RSB notes/formulas take precedence over the site's older summaries:
Dragon Pulse has 30% KO cooldown reduction, Dragon Tail has a 2.5s inter-use
cooldown and a consumable upgrade mark, and Stealth Rock roots on its second
hit and grants a shield when triggered.

## Integration

- `MoveDatabase` maps all seven existing enum values. The serialized misspelling
  `DuraStelthRock` is intentionally preserved to avoid breaking saved assets.
- `Assets/Moves/Duraludon` holds the icons, previews, descriptions, and energy
  cost. All seven moves are in `MoveAssetRegistry.asset`.
- Learnset: basic moves at 1/2, Flash Cannon or Dragon Pulse at 5, Dragon Tail or
  Stealth Rock at 7, Revolving Ruin at 9, upgrades at 11/13. Asset levels are
  zero-based, like the other characters.
- Every move has a prefab in `Assets/Prefabs/Objects/Moves/Duraludon`. The five
  new cast objects inherit `DuraMoveObject` and own their server-side hitboxes,
  damage, presentation phases, and lifetime. Flash Cannon and Metal Claw keep
  their original prefabs. `DuraBoostedObj` retains persistent mark/attack state
  and coordinates Addressables -> NetworkObject -> InitializeRPC spawning.
  Each async cast captures its parameters and is cancelled if the caster dies
  before loading completes. Addressable handles are retained until destruction.
- `DuraDragonPulseMarks.prefab` is an owner-only decoration referenced by the
  boosted-attack prefab. Five authored orbs and a final ring replace procedural
  line geometry. No match-time meshes, primitives, or LineRenderers are created.
- Damage flag `Unused_2` identifies Duraludon secondary damage, preventing bonus
  damage from recursively triggering itself or counting boosted multi-hits as
  multiple Dragon Pulse marks. `Unused_1` identifies the Pulse detonation, which
  consumes Tail's mark but does not create a fresh Pulse mark.
- Stored moves use the shared `MoveBase` / `MoveUseState` implementation. They
  declare only capacity and cast interval, recover sequentially with CDR, and
  continue recovering while dead. `MoveUI`'s stored-use panel shows recovery
  and reserve count separately from cast availability. See `../README.md`.
- Death clears persistent fields, marks, visuals, and Unite action restrictions.

## Approximation / remaining polish

The site does not specify world-space ranges, exact charge thresholds, rotation
duration, or trap tick spacing. These use project-scale values: Pulse radius
3–7.5 units / 1.5s full charge; Tail 4-unit hit area / 2.5-unit retreat; Rock
7-unit throw / 2-unit field / 0.5s ticks; Ruin 6-unit ring / 2s sweep. They are
centralized in `DuraMoveRules` or adjacent move constants for tuning.

Visuals use authored placeholder meshes, colored URP materials and particles,
inspired by the existing move prefabs. The editor-only `DuraludonPrefabBuilder`
creates missing assets and registers network/addressable prefabs. It does not
overwrite existing prefab layouts. Geometry is baked into `Duraludon/Art` and
materials live in `Assets/Materials/Duraludon`; these are editable in Prefab Mode.
The builder imports new prefab assets before registration so NGO generates
nonzero, unique spawn hashes. Rebuild Addressables content before testing a
built player or an editor configuration that uses an existing content build.
Presentation RPCs follow the existing fixed-roster flow, not late-join state
restoration. Pulse marks
are source-specific and cleansable; the niche opposing-Duraludon mark-steal
interaction is not reproduced. The separate Heavy Metal passive is unchanged.

## Validation

Run EditMode tests in `Unite.Duraludon.Tests` (`Assets/Editor/DuraludonTests`). They cover factories/assets/
registry/learnset, charge recovery/upgrades, Pulse formulas/stages, prefab
registrations, serialized hitboxes/VFX, baked meshes, and the mark prefab.

Manual host + remote-client checks:

1. Learn both branches at 5/7, Unite at 9, upgrades at 11/13.
2. Stack Pulse marks with normal/boosted attacks and Rock ticks. Cancel charging,
   cleanse marks, expire them, then test partial/full release and KO bonuses.
3. Spend both Tail charges, retreat through a wall, and repeat during Cannon
   Mode. Confirm the stance remains rooted after the retreat and ends normally.
4. Throw Rocks onto enemies and empty ground. Confirm interception, enemy-hidden
   fields, reveal + shield, second-hit root, replacement of the oldest field,
   60s expiry, and the third reserve only after upgrading.
5. Sweep Ruin across several enemies. Check capped buffs/shields, the final shove,
   and damage only on the burning circumference (not the interior).
6. Die during Pulse/Cannon/Ruin and with active traps. Verify movement, aim,
   action locks, and fields are cleaned up, including on the remote client.

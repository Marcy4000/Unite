# Single-use and stored-use moves

`MoveBase` defaults to `MoveUseType.SingleUse`. Existing normal moves retain
their cooldown and `Finish`/`onMoveOver` flow without any changes.
Unite moves retain their existing, separate energy-based charging system.

To define a stored-use move, inherit directly from `MoveBase` and declare:

```csharp
public override MoveUseType UseType => MoveUseType.StoredUses;
public override int MaxStoredUses => IsUpgraded ? 3 : 2;
public override float StoredUseInterval => 1f;

// In the constructor:
// Cooldown = 8.5f; // Time to recover ONE stored use, not the cast interval.
```

The move still sets `wasMoveSuccessful = true` when its effect succeeds and
calls `base.Finish()`, just like a normal move. It must not spend reserves,
rewrite `Cooldown`, manage recovery in `Update`, or call UI methods itself.

## Ownership of state

- `MoveBase.Finish` spends a use only after a successful cast. A failed finish
  or cancellation does not spend anything.
- `MoveUseState` recovers uses **sequentially**. Casting another reserve does
  not restart the pending recovery. Increasing capacity grants only the new
  slots, preserving spent uses and recovery progress.
- `MovesController` advances stored resources before input, including while
  dead. Its existing cooldown status reflects the greater of the cast interval
  and (only with empty reserves) the wait for the next recovery.
- CDR affects reserve recovery once, not the cast interval. Changing CDR
  preserves the fraction of recovery completed. `ReduceMoveCooldown` reduces
  reserve recovery for stored moves without bypassing the cast interval.
- State follows the existing owner-controlled move flow. Death/reset and move
  upgrades do not refill reserves. Replacing a move creates a fresh resource.

## UI

`MovesController` supplies the actual learned move to `MoveUI.Initialize` via
`BattleUIManager` / `MovesHolderUI`. Upgrades rebind the same runtime object.
Learn-panel previews may omit that argument and use a fresh definition.

The main UI shows cast availability. `SecondaryBG` is enabled **only for stored
use moves** and shows reserve recovery plus a count. Its copied icon is disabled.
The desktop prefab owns the references; the mobile variant inherits them. Both
timers read the live resource state, so they stay synchronized after recovery,
CDR changes, reductions, upgrades, death, and hiding/re-enabling the HUD.

The older `SecondaryCooldown` ring and `ShowMoveSecondaryCooldown` API remain
available for temporary modes such as Flash Cannon; they are not reserve timers.

Tests: `Assets/Editor/MoveTests` (`Unite.Moves.Tests`), plus the existing
`Unite.Duraludon.Tests` regression tests.

## Authored server-owned effects

`PrefabMove` is an optional `MoveBase` adapter for moves whose gameplay lives in
an authored network prefab. Declare `MoveId`, `PrefabPath`, `AimStyle`, and
`Range`; normal aiming, cancellation, successful completion and cooldown UI
still use the existing move flow. `MovesController.PrefabMoves` validates the
learned move and target, handles independent asynchronous loads, and spawns a
`PrefabMoveEffect`. Effects supply `Execute()` and optional presentation/cleanup
hooks, retaining the asset handle and cleaning up on caster death/despawn.

`HomingMoveEffect` is a configurable sure-hit damage/stat projectile. The older
`HomingProjectile` also supports optional attacker-on-impact stat changes for
basic attacks. See `Sylveon/README.md` for examples and prefab-authoring details.

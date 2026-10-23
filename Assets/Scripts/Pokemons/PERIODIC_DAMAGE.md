# Shared periodic status damage

Periodic damage belongs to `Pokemon`, not `PlayerManager` or a character's private timer.
It works for players, wild Pokemon and objectives, including a dedicated server with no
victim input controller. Presentation is local and uses the shared `Resources/Combat/BurnStatus`
prefab when a visible Pokemon is burned.

Create a status with `StatusEffect.DamageOverTime(type, damage, duration, interval, id,
targetMaxHpRatio, nonPlayerDamageCap)` and pass it through the existing `AddStatusEffect` API.
The `DamageInfo` supplies the real attacker, scaling and damage type. A source plus status
type plus ID identifies one periodic effect. Different sources coexist; reapplying the same
effect refreshes its expiry rather than adding duration, without delaying its next tick.

- The first tick occurs after one interval. The final tick exactly at expiry is included.
- Absolute schedules catch up missed frames; damage is computed from current source stats.
- Maximum-HP contributions and optional non-player caps are applied before normal mitigation.
- Periodic ticks carry only the existing secondary-damage flag (`Unused_2`), not basic/crit/
  Unite/item flags. Characters can exclude secondary damage from on-hit stack generation.
- Removing/cleansing the status stops its damage. Changing its remaining time updates its
  schedule. Death of the victim clears it. Despawning the source invalidates it. Source death
  alone does not invalidate a still-spawned source; a character may explicitly remove its
  own statuses during death cleanup if its design requires that behavior.
- `HasDamageOverTime`, `RefreshDamageOverTime` and `RemoveDamageOverTime` query or maintain
  only one source's periodic statuses. Maintenance never creates a new effect.
- A plain `new StatusEffect(StatusType.Burned, ...)` is only a marker. It does **not** invent
  an attacker or silently start the removed victim-attributed legacy damage coroutine.

Flareon and Cinderace both use this path. Cinderace retains 20% Attack per tick for three
seconds and its separate two-second Attack/Sp. Attack debuff. Its cinder marks still belong
to its own combat runtime, but it has no private burn tick scheduler or burn-ID classification.
Marshadow's copied Flareon Unite uses the same status profile with Marshadow as the source.

`StatusEffect` now serializes the periodic payload. Rebuild both server and clients together;
old and new builds are not wire-compatible.

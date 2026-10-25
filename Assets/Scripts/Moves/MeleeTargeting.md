# Melee targeting safety

`CombatTargeting` treats physics overlaps as candidates. It rejects vision sensors
and revalidates current hurtbox geometry from local collider data and the current
transform. It deliberately does not use cached `Collider.bounds` or `ClosestPoint`
to validate teleports. Sphere, capsule, CharacterController and box hurtboxes are
supported; mesh hurtboxes use their transformed mesh bounds as a conservative guard.
Box sweeps retain the original physics narrow phase plus a current-pose bounds guard.
Real trigger hurtboxes remain valid, and multiple hurtboxes resolve to one Pokemon.

- `AimInCircle` and `AimInCircleAtPosition` reject stale poses and dead targets.
  The latter is shared by Jolteon, Flygon, Marshadow, Serperior and wild melee attacks.
- Each of those basic attacks additionally checks every victim's current root
  against its attack range (with the existing half-unit contact allowance).
- Flareon's delayed basic attack performs the same checks at contact, not just
  against its original primary target when input was received.
- Flygon's area effects and Supersonic use the shared sphere/box filtering, so an
  overlapping vision trigger cannot cause remote damage, stuns or slows.
- Serperior's boosted basic dash is range-capped, wall-safe and owns disposable
  action/movement restrictions. Death, reset, replacement and network teardown
  cancel it. It never runs a detached transform tween or unlocks unrelated stuns.

`BasicAttackBase.ResetAttack` and `Dispose` default to no-op for unaffected licenses.
`MovesController` calls them on death/reset, replacement and teardown. No basic
attack animations were added or changed by this safety pass.

Tests: `Unite.MeleeTargeting.Tests` (current geometry and wiring) and
`Unite.MeleeTargeting.PlayMode.Tests` (shared Aim queries, real Flareon damage on
host/dedicated server, stale colliders and Serperior's frame-by-frame cleanup).

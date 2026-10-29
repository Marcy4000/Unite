# Psyduck racing move audit

All six moves are constructed through `MoveDatabase`. Dash is a stored-use move;
the other five are consumable race items. This audit covers their runtime logic,
network ownership, prefab physics configuration, status interactions, and cleanup.

## Findings and fixes

| Move | Verified issues | Resulting behavior |
| --- | --- | --- |
| Barrier | Initialization disabled the collider on its owner, including the host. The solid collider used `OnCollisionEnter`, which is not the correct interaction path for racers moved by a `CharacterController`. | A server-owned trigger with a kinematic body ignores its caster, incapacitates an opponent for 0.5 seconds, and disappears after a hit or 5 seconds. |
| Bubbles | Shared asynchronous spawn callbacks could be consumed by another item. Two root-position tweens competed, and an infinite tween was nested in a sequence. Despawn did not kill tweens, and an uninitialized bubble never expired. | Three independent server-initialized bubbles travel in a forward fan. Bobbing animates only the visual child. A hit incapacitates for 1.2 seconds; each bubble expires after 10 seconds and kills its tweens on removal. |
| Confuse Ray | Target selection used a larger place number for a racer supposedly ahead. Any Scriptable status made a racer immune. Lost targets and uninitialized projectiles could leave permanent objects; a zero direction could reach `LookRotation`. | The server selects the nearest eligible racer ahead, with the existing leader fallback retained. The ray incapacitates for 1 second, ignores unrelated Scriptable statuses, and expires after 10 seconds or target despawn. |
| Freeze Ray | Shared the Confuse Ray targeting and lifecycle defects. The freeze-specific Scriptable status required explicit shield/immunity handling. | Uses the same targeting and lifetime rules, applying the existing 1-second sliding freeze (Scriptable status 20). Shields and unstoppable/invincible states block it consistently. |
| Shield | Initialization and subscriptions ran on the spawning client while status removal/despawn ran on the server. It removed beneficial speed changes as well as slows. Expiry could leave hindrance resistance behind, and removing a just-published freeze risked event ordering problems. | The server follows the assigned racer and blocks one incoming racing hit or qualifying negative status/speed debuff before publication. Speed buffs remain intact. Expiry, replacement, and despawn remove the shield's own interception hooks; no persistent resistance status is added. Lifetime remains 12 seconds. |
| Dash | The dash branch bypassed movement restrictions and freeze. Failed/redundant dashes could spend a stored use. | Restricted, frozen, already-dashing, or disposed racers cannot start a new dash. Restrictions interrupt an active dash; only successful starts spend a use. The existing two stored uses and recharge settings remain unchanged. |

## Shared safeguards

- Racing item requests use a dedicated owner-invoked server RPC rather than the
  general `onObjectSpawned` event. Overlapping loads cannot steal initialization
  callbacks from another item.
- Spawn requests capture placement and targeting on the server and discard pending
  loads after the actor despawns or finishes racing. Addressable handles remain
  alive for the spawned object's lifetime and are released on destruction.
- Barrier and shield prefabs use server-authoritative `NetworkTransform`s.
- Hits resolve `PlayerManager` through parent colliders, ignore their caster, and
  cannot execute twice after a network despawn.
- Finishing an inactive/cancelled consumable does not spawn an object or replace
  a newly acquired item with the empty item.

## Regression coverage

- Validation on October 29, 2026: all 20 racing PlayMode tests and all 10 racing
  EditMode rules tests pass. The broader move EditMode assembly passes 60/62;
  its two failures concern duplicated reserve icons in the unchanged `MoveUI`
  and `MoveUIMobile` prefabs, not racing move logic.
- EditMode: inactive/cancelled consumables; nearest-ahead targeting; leader
  fallback; missing casters; finished-racer exclusion.
- PlayMode: real barrier/CharacterController trigger physics; caster immunity;
  bubble tween cleanup; shield one-hit behavior, buffs versus debuffs, replacement
  and expiry; unrelated Scriptable statuses; lost targets; uninitialized expiry;
  rejected dash charges; movement/freeze interruption.
- Host/client: server-owned barrier and shield placement replication, plus the
  pre-existing racing animation tests for host-owned and client-owned racers.

These are focused automated regressions, not a claim that every multiplayer race,
latency condition, or balance scenario has been exhaustively tested. Item balance
and a full human-played race remain separate validation tasks.

# Shared gameplay stability pass

This pass changes shared systems, not bot strategy or early lane-farm policy.

## Lifecycle and vision

- Vision range is checked against current positions, including controller enable,
  blind and owner-alive state. Trigger membership is no longer authoritative after
  a teleport. Bush checks use the observers' current bush rather than stale IDs.
- Death/respawn clears transient reveal/bush state. Visibility changes notify and
  toggle graphics only when the state changes; newly loaded models still receive
  the current state immediately.
- Player minimap icons are idempotent, react to model/team readiness, and unbind
  named event handlers on teardown. There is no fixed initialization delay.
- Avatar despawn removes its match roster entry and icon. Passive experience uses
  the current roster rather than delegates retaining destroyed Pokemon.
- Wild Pokemon capture their subscribed match and invalidate asynchronous loads
  on despawn. Objective icons cancel their tweens and subscriptions on teardown.
- Results-screen teardown releases only valid scoreboard handles, including when
  leaving before the second asset load starts. Cleanup is safe to repeat.

## Goals and lobby capacity

- Only living, spawned defenders in an active, non-weakened goal receive defense.
  Weakening immediately removes goal-sourced speed/shield benefits and hides the
  defensive graphic. Recovery restores benefits without requiring re-entry.
- Goal speed/shield IDs are `64991`/`64990`, separate from flux speed and other
  shield sources. Goal shields refresh instead of accumulating duration forever.
- Occupancy is deduplicated and dead/despawned occupants are removed even if a
  trigger exit was missed. Disabling/despawning a goal cleans up its occupants.
- Map capacity counts humans and the actual number of teams. Bots are cleared by
  the successful map-change roster update rather than preventing smaller modes.

## Placement and movement

- AEOS drops are grounded before NGO serializes their initial position. Terrain
  queries ignore triggers, rigidbody actors, networked hitboxes, walls and goals.
  An invalid scatter point retries the original drop location; no floating pickup
  is spawned if neither has ground. Async drops cannot outlive their match.
- Soldiers sample a compatible agent-type NavMesh before spawning. Navigation
  queries require a bound agent; binding recovery is throttled and bounded.
- `WalkmeshBoundary` welds imported vertices and caches real boundary edges for
  tangential sliding and nearby position recovery. Sub-centimeter tangential
  movement is retained. Physical collision still belongs to CharacterController;
  special traversal still chooses the normal/no-walls walkmesh as before.
- The small collision meshes under `Assets/Model/Navmeshes` retain CPU-readable
  vertices in builds. Boundary data is warmed during avatar setup and weakly
  cached so old scene meshes are not retained by a static registry.

## Presentation and allocation work

- Hidden animators retain continuous parameters and the latest explicit state,
  without inactive playback/state queries or a backlog of one-shot triggers.
- Bot selection scans retain their existing ordering/cost policy without LINQ
  sorting/closure allocations. Navigation corner arrays are reused.
- Vision scale checks avoid the allocating three-argument `Mathf.Max` overload.
  Hot status/shield lookups index NetworkLists instead of boxing enumerators.
  Periodic damage reuses its callback-safe snapshot buffer, and wild-Pokemon
  notice queries reuse physics buffers and deduplication sets while retaining
  current-transform validation after teleports.
- HUD countdowns and minimap energy text update only when their values change.
- Goal shield materials are cached and explicitly destroyed with their owner.

## Validation limits

The intermittent MissingReferenceException had no supplied stack trace. These
changes address identified stale-reference paths, but cannot establish that every
possible occurrence of that exception has the same cause. Editor measurements
also include Editor/rendering/tool overhead and do not reproduce livestream load.
They are not a standalone-build FPS guarantee.

## Automated regression results

- All 108 focused EditMode checks passed (bots/shared gameplay, minimap,
  multiplayer, network resilience and lane selection).
- All 171 selected PlayMode checks passed, including server/client combat,
  periodic damage, supported bot Pokemon, wall navigation and soldier binding.
- The broader EditMode sweep passed 554/558. Four unrelated, untouched asset
  checks fail: Flygon and Vaporeon tests read a missing serialized `description`
  property; two stored-move UI prefab tests find an enabled duplicate icon.

Live controlled checks verified smaller-map switching with nine bots, out-of-sight
enemy respawn, weakened/restored goal healing and defenses, defensive-graphic
cleanup on death, ground-level AEOS drops under goal geometry, and a Regieleki
soldier spawning on a bound NavMeshAgent.

The final lifecycle run completed three shortened match/results/lobby cycles
(Theia, Remoat, Theia) in one Play session, including immediate results exit.
Each match had 10 players and 10 minimap icons; each lobby cleanup had zero
remaining player/Pokemon objects or player icons. No runtime errors or inactive-
animator warnings were recorded. Existing main-menu missing-state and non-root
DontDestroyOnLoad warnings remain outside this pass. These were not full-length
ten-minute matches, and remote-client visibility still needs multi-instance coverage.

## Short Editor profiling samples

Private Theia host, one human and nine bots, 15-second samples; counters include
Editor overhead. Gameplay was not deterministically replayed. The final sample
was taken without concurrent tool calls during its capture window.

| Counter | Before | After |
| --- | ---: | ---: |
| Main-thread median | 21.24 ms | 19.04 ms |
| Main-thread p95 | 25.97 ms | 24.39 ms |
| BehaviourUpdate median | 0.94 ms | 1.09 ms |
| Managed bytes/frame median | 40,411 | 20,625 |
| Managed bytes/frame p95 | 106,167 | 280,145 |

Median allocation fell by about 49%, but tail allocations did **not** improve in
these samples. Startup/spawn/combat mixes differ, so these are diagnostic samples,
not proof of a frame-rate improvement or elimination of GC spikes. GPU/render-thread
timing was unavailable; streaming load was not reproduced. A longer standalone and
Editor profiling pass is still needed to attribute remaining frame spikes.

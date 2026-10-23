# Flareon: burn-and-brawl rework

## Combat and integration

- Existing serialized move/passive enum values, asset GUIDs and learnset are unchanged.
  Displayed levels: starters 1/2, evolution and Heat Wave/Takedown 4, Flamethrower/Flare Blitz 6,
  Unite 8, upgrades 10/12. Rules use the project's zero-based level index.
- `FlareonMoveBase` owns aiming, scoped action restrictions, a short input buffer and wall-safe
  owner-driven movement. `MovesController.Flareon` validates casts, learned branches, targets,
  cooldowns, movement reports and asynchronous loads. Damage runs on the server.
- Cooldowns are paid once on accepted commitment. Takedown's second hit is an explicit,
  source-owned recast window; it does not pay or restart the cooldown. A dead, despawned,
  out-of-range or protected target cannot be slammed. Failed initial targeting costs nothing.
- Third **connected** basics reduce both move cooldowns by 0.5s once, even against a group.
  They refresh this Flareon's existing burns to at least 2s remaining, without creating a new
  burn, extending another caster's burn or shortening a longer remaining duration.
- Passive: direct damage against burned opposing players grants up to three 5% Attack stacks,
  expiring together after 5s without a qualifying hit. Secondary/periodic and item damage do
  not generate stacks. Eevee receives 10% Attack for 5s after taking damage below half HP.
  The replicated combat state and stat changes reset together on death.
- Swift has ten 0.4s ticks. Heat Wave has eight pulses (ten upgraded) with a 0.35s first pulse,
  plus 20% speed for 6s. Flare Blitz lasts a fixed travel time regardless of speed modifiers,
  stops at walls, burns for 2/3s and grants its 3s hit refund only once. Takedown launches for
  1.2/1.4s rather than the previous 1.6/1.8s. Other direct-damage formulas are retained.
- Each burn tick deals 20% Attack plus 1% target max HP, once per second, capped at 200
  pre-mitigation damage against non-player targets. This deliberately replaces the old,
  victim-attributed 5%-max-HP-every-0.75s legacy loop. Full-combo tuning needs playtesting.

## Presentation

**The Flareon model does not have ready basic-attack clips.** No Flareon basic animation
is requested. The placeholder uses a short visual-root nudge and an authored contact crescent
at the 0.1s hit beat; Eevee keeps its existing clips. The player collision root is never
scaled, lifted or tweened for presentation. Adding real basic clips later should preserve
that server hit beat and replace only the placeholder presentation.

Presentation updates in `LateUpdate`. Swift and Heat Wave attach only their graphics to the
actor at zero local offset; their server hitboxes remain on the network effect root. This
prevents movement, pulse positioning or network interpolation from shifting an aura for one
frame after its graphics were positioned. Despawn/destruction cleans up the attached graphics.

`FlareonMoveObject` separates server hitboxes from replicated visual phases. Swift stars,
Heat Wave boundary/pulses, the Flamethrower sweep, charge flames, launch/slam and Unite
landing effects are authored prefabs. Owner-only pips show the basic cycle and Attack stacks;
the maximum-heat aura is visible to observers who can see the actor. Recast markers do not
reveal hidden targets. The shared burn decoration respects victim visibility.

Use **Tools/Flareon/Create Missing Move Prefabs**. Like Duraludon's builder, it bakes meshes,
materials and sounds to assets, creates missing prefabs, and registers exact Addressables
keys and unique persisted NGO hashes. Existing layouts are not overwritten. Old Flareon
area prefabs remain in the project to preserve references, but the new move factories do
not use them. Rebuild Addressables content before testing a built player.

## Validation

EditMode: `Unite.Flareon.Tests`, plus Cinderace, Cresselia and Marshadow regression suites.
PlayMode: `Unite.Flareon.PlayMode.Tests` uses a real isolated dedicated server to verify
source attribution, independent player/wild/objective burns, final ticks, cleansing,
refresh and source despawn, plus movement/action lease and visual-lift cleanup tests.
Coverage includes combo-state expiry/reset, source-aware periodic damage scheduling,
boundary ticks, reapplication, refresh, non-player caps, upgrade thresholds, factory
integration, authored hitboxes, materials and prefab registrations.

Manual host + remote-client matrix:

1. Test all four move pairs at their unlock and upgrade levels. Weave a basic between
   Takedown stages and queue a follow-up during another move's final 0.2s.
2. Third-hit a group, whiff, become stunned before contact, and let the basic cycle expire.
   Only a confirmed third hit reduces cooldowns, once; no missing animation warnings.
3. Recast Takedown, let it expire, kill/despawn/cleanse/protect the target, and die in either
   jump. Check cooldown payment, indicators, action locks and movement restoration.
4. Drive Tackle/Blitz/Unite into thin walls and map edges. Test root/stun during travel and
   disconnect/despawn. No transform tween may move a dead player after reset.
5. Apply Flareon and Cinderace burns simultaneously to players, wilds and objectives. Confirm
   independent sources, correct damage/KO attribution, refresh rather than duration addition,
   cleanse stopping ticks, no duplicate generic/private damage, and no victim-owner dependency.
6. Verify sweep direction, pulse counts, visibility, jump lift and cleanup from a remote client.
   Test latency and delayed/failed Addressables loads, including death then respawn while loading.
7. Copy Flareon's Unite with Marshadow; its burn must credit Marshadow through the shared system.

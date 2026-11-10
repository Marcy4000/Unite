# Trevenant

Reference: https://unite-db.com/pokemon/trevenant (November 9, 2026), including detailed RSB formulas.
Geometry also references `Assets/Moves/Dumps/{trevenant,phantump}_skills.json` and `age-named-v2/3c44bc3/age_708_*`.
AGE timing declarations are not verified runtime schedules; animation-aligned timing remains editable project tuning.

## Integration

- All seven moves use server-accepted `PrefabMove` effects. The passive and basic attacks run on the server, including for remote owners and bots.
- Learnset: 1/3 starting moves, evolution/first branch 5, second branch 7, Unite 9, upgrades 11/13 (serialized zero-based).
- `TrevenantMoveRules` holds formulas and tunable geometry/timing. Curse uses actual AGE radii 3.5/4.3 rather than its oversized indicators; Unite uses 4.5 radius, 8 cast range. Pain Split+ uses Unite-DB's 8.5s rather than the older AGE duration.
- `Tools/Characters/Trevenant/Create Missing Move Assets` creates missing assets only. Existing model/animator/stat/prefab work is preserved. Prefabs, materials, mesh assets, icons, previews, move registry and network/Addressables entries are authored, not generated during matches.
- Rebuild Addressables content before testing a player build or an editor configured to use existing content builds.

## Agreed rules

- Curse fills after 20 outgoing/incoming damage events, independent of damage amount. Each enemy hit/tick is an event. A self-cost contributes exactly once, not once for each side of the transaction.
- Health costs are nonlethal true HP loss, bypassing shields and reductions; they do not trigger Natural Cure, items, or Pain Split reflection.
- Basic boosted recovery and Branch Poke/Horn Leech healing scale per distinct enemy hit. Each target grants one three-tick recovery sequence per dash.
- Branch Poke crosses internal walls using bounded traversal. Horn Leech uses wall-respecting movement; its victims are displaced with wall collision.
- Pain Split permits wild targets, ignores walls/vision after acquisition, and breaks on distance, death, expiry, or replacement. Redirected true damage cannot be redirected again. One active tether per caster.
- Natural Cure cooldown uses absolute server time and survives death. Active recovery ends on death and begins its cooldown. Move cooldowns start when their complete execution/recast window ends. Full Curse can detach a still-active Pain Split cooldown so the tether can be replaced.
- Forest bonus damage is source-specific, cleansable, nonrecursive, and limited to one proc per attacking ally per 0.5s. Healing amplification requires the specific forest hit-scaled shield, not merely any shield.

## Visuals and validation

Editable green spectral branches/hammers, violet flames/aura/tether, phantom trees, marks, particles and custom hammer outlines follow Duraludon's baked-asset approach. Existing Phantump/Trevenant animator states drive all attacks/moves. Physics queries deduplicate Pokemon and use current transforms; authored disabled hitboxes are inspection guides, while rule values drive gameplay.

Wood Hammer presentation now follows `Bip001 L Hand` / `Bip001 R Hand` in LateUpdate: two spectral log hammers for the sweep and one larger, two-hand-anchored hammer for the slam. Authored growth rings, vines, leaves, motion trails and separate ground-impact bursts replace the stationary placeholder hammers. `TrevenantWoodHammerAuthoring` performs the targeted one-time upgrade and preserves later artist edits. Grip rotation/offset and weapon scales live in `TrevenantMoveRules`.

The first Wood Hammer aim indicator uses the supplied `pref_Indicator_Sector_180_lv3` FBX and its matching texture, wrapped at the gameplay radius. Its transparent material is a Trevenant-owned copy; the user's source FBX/import settings/material are unchanged. The follow-up retains its rectangular indicator. Curse displays its active-zone duration on the move's secondary cooldown ring, using a replicated server expiry and clearing immediately on detonation, death or cleanup; its HP-bar gauge still shows charge.

Both Wood Hammer hits use the same 0.35s windup and 0.2s recovery. Opt-in contact-timed animation playback maps each clip's actual strike pose (`HammerSweepContact` / `HammerSlamContact`) onto that shared windup, then plays the remainder during recovery without restarting the clip. Its second-hit window also uses the secondary cooldown ring: it opens only after a successful opener and clears on follow-up acceptance, expiry, death or cleanup. Curse and Wood Hammer share the slot-A duration display without independently overwriting it.

Will-o-Wisp now travels in a `\|/` fan, with side flames angled 25 degrees outward and a forward center flame. `WispSpreadAngle` and initial `WispSpacing` (0.25) are editable. Server swept collision queries and client flame positions/rotations use the same trajectory helpers and synchronized launch time; each flame still stops at its own first target and repeat hits retain half damage.

EditMode suite: `Unite.Trevenant.Tests` tests factories, learnset, art, network/Addressables registrations, animator states, tunability, gauge count/cap, formulas, HP thresholds, nonlethal costs and recursive-redirection guards.

PlayMode suite: `Unite.Trevenant.PlayMode.Tests` runs actual NGO dedicated-server and host fixtures with remote-owned actors. It exercises effect coroutines, multi-target recovery, true-damage redirection (including opposing tethers), passive/death timers, marks/shields, dash wall behavior, cooldown dispatch, and link replacement. A connected remote-client visual/input smoke test remains a manual check.

Validation on November 9, 2026: 30/30 Trevenant PlayMode cases passed. The broader selected EditMode run passed 579/592, including all Trevenant cases. Its 13 failures are in untouched older tests: three battle-item cooldown expectations, bot roster count, BootScene held-item catalog count, six obsolete GlaceUniteArea field/reflection checks, and two stored-use UI duplicate-icon checks. These were left outside this implementation. Unity compilation completed without errors.

Presentation follow-up validation: 115/115 targeted EditMode cases (Trevenant, Umbreon, Duraludon) and 32/32 Trevenant host/server PlayMode cases passed. New checks sample both Wood Hammer animations at four poses, verify exact moving hand anchors and trail cleanup, validate the supplied sector mesh/radius, and exercise Curse's actual secondary cooldown ring through countdown, detonation and death. A rendered editor preview was also checked for hammer placement.

Cast/recast/fan follow-up validation: 183/183 targeted EditMode cases (Trevenant, Umbreon, Duraludon, Absol) and 40/40 Trevenant host/server PlayMode cases passed. Added checks cover animation contact/recovery and hidden-model resumption, equal server hit/lock timing, Hammer's actual secondary ring and window lifecycle, rotated/tunable fan trajectories, three independently hit lanes and untouched gaps.

Manual host + remote-client checklist:

1. Learn all branches and upgrades; cast at walls and at maximum range.
2. Hit multiple enemies with each recovery move. Verify three delayed ticks per victim and boosted healing per victim.
3. Trigger Natural Cure at 35% HP, die during recovery/cooldown, respawn and verify remaining cooldown.
4. Aim/reaim Wood Hammer; compare both strike timings. Miss the opener, use the follow-up, let the 5s secondary ring expire, and die while holding a recast. Verify the ring clears in each case. Check Will-o-Wisp's fan on host and remote client while aiming in different directions.
5. Fill Curse with hits, burn ticks, enemy attacks and self-costs. Detonate early/full/automatically and verify other-slot cooldown resets.
6. Link wild/enemy targets; move behind walls, lose vision, exceed 7.5 units, replace a link after Curse, and link two opposing Trevenants. Verify no reflection recursion.
7. Unite onto several enemies; trigger marks using two allies, cleanse marks, break/expire the forest shield, and verify +50% healing stops even if the standard Unite shield remains.
8. Interrupt windups, die while Addressables load, and despawn actors. Confirm no action locks, movement overrides, markers, or persistent tethers remain.

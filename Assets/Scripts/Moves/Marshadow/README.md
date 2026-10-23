# Marshadow rework

Basic-attack animations are intentionally omitted: the model has no suitable
combat chain. Attacks retain their three-hit counter and use authored impact
effects instead. No new animator states or clips are required. A per-action
speed parameter retimes the existing punch/kick/cast clips to contact, and
recovery explicitly returns to locomotion without overriding stun animations.

## Combat

- Fighting Spirit: a committed move sequence or a boosted basic hit grants a
  stack (maximum three), refreshing a four-second window. Per stack: 5% Attack,
  5 attack-speed points and 5 lifesteal percentage points. Death clears both the
  counter and stats. Recasts do not farm extra stacks.
- Drain Punch: 0.25s windup, 0.18s recovery. Server healing uses actual HP lost:
  25% against players, 10% against wilds, with aggregate wild healing capped at
  4% max HP. Shields and rejected damage cannot generate healing.
- Shadow Sneak: 3.5s protected traversal; manual early emergence. The gameplay
  root stays grounded. Exit searches the grounded walkmesh within two units,
  then uses a known valid position. Every restriction/stat/status is released.
- Aura Sphere: server-swept projectile with explicit hit/miss messages correlated
  to its cast. Five-second marked recast; pursuit respects walls and requires
  contact. The follow-up reduces the other move's base cooldown by 30% only on a
  valid hit. Upgrade increases missing-HP scaling from 20% to 25%; wild execute
  damage is capped at 700. Target-death subscriptions are always removed.
- Close Combat: three manually redirected 3.5-unit strikes, each taking up to
  0.23s, with a 1.25s recast window after each of the first two. Other moves and
  basics can be woven between strikes. Server swept hits are deduplicated per
  strike. Finisher knocks up and slows; upgrade increases finisher damage and
  gives 20% movement speed for two seconds after hitting.
- Spectral Thief: up to four units over 0.28s, then 0.18s recovery. Outer radius
  1.5; theft radius 0.8. Theft is an atomic server operation for at most eight
  removable, timed positive stat changes, preserving remaining lifetime (capped
  at six seconds, eight upgraded). Shields and permanent effects are excluded.
- Spectral Thief and every Close Combat strike cross walls using the same
  no-walls walkmesh as the shared dash system. They retain their original range
  and speed, stop at map boundaries, and shorten the dash if its endpoint would
  be inside terrain. Interruption returns to the last safe point before
  releasing movement locks. Flight, controller and shared dash state are not
  changed; Aura Sphere pursuit and Phantom Force knockback still respect walls.
- Phantom Force: four-second stealth with only a 0.22s invincible entry dodge.
  Offense breaks stealth. The aimed kick has a 0.28s windup/0.2s recovery
  (0.22/0.13 upgraded), shoves up to 2.5 units and wall-stuns for 0.7s (1s
  upgraded). The owner resolves movement against the walkmesh; the server
  validates the outstanding wall report, extends the stun and broadcasts impact.

Recast moves begin cooldown when their sequence ends. Cancellation of aim does
not erase a committed sequence. Death/interruption resets committed execution
and spends its cooldown. A 0.18s input buffer applies only to Marshadow's short
execution locks. `BattleActionStatus.AcquireStatus` leases prevent a cleanup
from clearing another caller's action restrictions; legacy flag APIs are intact.

## Mirror Shadow: explicit compatibility boundary

Random selection is preserved, but only enemies with supported adapters enter
the pool. Another Marshadow, unknown/unimplemented Unites and unsupported
character-dependent states are excluded. No eligible enemy means no energy is
spent. Channeling spends Mirror's energy immediately; two seconds later the
borrowed move receives one use, expiring after 15 seconds. Death/expiry discard
it without refunding a full Mirror. The energy denominator never changes.

Current adapters:

| Source | Shadow copy |
| --- | --- |
| Cinderace | Homing physical Unite strike |
| Glaceon | Short grounded leap, physical landing hit and 5s slowing field; no icicles |
| Sylveon | Physical burst and six seconds of 50-point lifesteal/spell vamp |
| Vaporeon | Physical tempest, defense reductions and ally healing scaling with Attack |
| Emboar | Inferno Ringbreaker rush, radial slam and shield using Marshadow's Attack; no Reckless or foreign basic attacks |
| Flareon | Collision-safe targeted rush, missing-HP burst, burn and shield |
| Duraludon | Existing standalone Revolving Ruin effect object, with Marshadow animation fallback |

These are **adapted copies**, not exact replicas of foreign passives/form changes.
The borrowed HUD description explicitly explains the adapted behavior. Flygon,
Cresselia, Meowstic, Jolteon and Serperior are not selected yet. Add an explicit
adapter and compatibility tests before enabling them; never install their raw
`MoveBase` implementations on Marshadow.

## Presentation and networking

`MarshadowPrefabBuilder` authors missing meshes, materials, particles and short
placeholder WAV sounds into `Assets/Prefabs/Objects/Moves/Marshadow/Rework` and
`Assets/Materials/Marshadow`. Existing hand edits and the three legacy network
prefabs are preserved. Run **Tools / Marshadow / Create Missing Rework Prefabs**
after importing this change, then rebuild Addressables for built players.

Damage/projectiles and buff transfers run on the server. Cosmetic RPCs load
local authored prefabs with captured parameters and per-request cancellation;
visual loading cannot delay or cancel gameplay. Addressable handles live until
their presentation is destroyed. No runtime meshes/primitives are generated.
Owner/allied shadow markers do not reveal stealth to enemies. Fixed-roster
presentation follows the project's networking model; late-join restoration is
not provided. Legacy `AuraSphereProjectile` is retained for prefab compatibility
but is no longer used by Marshadow's moves.

## Validation

EditMode suite: `Unite.Marshadow.Tests` under `Assets/Editor/MarshadowTests`.
Also run the existing move, Jolteon and Duraludon regression suites after changes
to scoped action statuses or shared controller integration.

Host + remote client manual checklist (not replaced by EditMode tests):

1. Test all four level-5/7 builds and upgrades at 11/13; attack between rushes.
2. Kill/despawn Aura's target before impact, while marked and during pursuit;
   miss, expire, cancel aim, block pursuit with a wall and KO with the follow-up.
3. Enter/exit Sneak in walls and map edges. Interrupt/kill/reset every cast phase.
4. Check Phantom's dodge expiry, offensive stealth break and wall impact under
   latency; verify no stale speed, invisibility, action or movement restriction.
5. Steal multiple timed buffs, protected buffs and nearly expired buffs with two
   Marshadows. Confirm no original-duration resets or duplicate transfers.
6. Test every supported copy, an all-unsupported roster, another Marshadow,
   interruption during channel, borrowed-use expiry and death before/after use.
7. Repeat with keyboard, controller/mobile aiming and 100-200ms latency. Ensure
   neither passive pips nor pursuit/stealth markers reveal hidden opponents.

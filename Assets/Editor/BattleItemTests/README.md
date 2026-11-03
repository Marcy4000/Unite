# Battle item audit

Reference: https://unite-db.com/items and its battle-only data at
https://unite-db.com/battle_items.json, retrieved November 3, 2026.
Held items are not part of this change. Brick is a pre-existing custom item.

| Battle item | Cooldown | Effect |
| --- | ---: | --- |
| Eject Button | 80s | Directional, walkmesh-validated teleport |
| Fluffy Tail | 22s | Three-second wild target disable; bonus damage with a 0.5s proc cooldown |
| Full Heal | 60s | Cleanse negative conditions and removable debuffs, preserving buffs; 1.5s unstoppable |
| Goal Getter | 60s | Double scoring speed for 10s |
| Goal Hacker | 80s | Four-second goal modifier; allied shields doubled and healing every 0.5s, or opposing shielding/healing disabled |
| Potion | 30s | Heal 160 + 20% maximum HP, clamped to missing HP |
| Slow Smoke | 35s | Stationary 3.5s cloud: 80% movement slow and 50 attack-speed points |
| X Attack | 40s | 20% Attack/Sp. Attack and 25 attack-speed points for 6.5s |
| X Speed | 50s | 45% movement speed and movement-slow immunity for 6s |
| Shedinja Doll | 45s | Two seconds invincible, untargetable, and action-locked |
| Brick (custom) | 55s | Server-spawned, ranged brick impact with damage and stun |

The reference does not supply Unity-world distances. Existing Eject/Brick ranges
remain five units; Slow Smoke uses a three-unit radius and targeted items have a
six-unit range. These constants are in `BattleItemRules`.

## Authority and lifecycle

- The owner submits an activation, not a heal amount, stat boost, duration, or cooldown.
- The server checks the equipped item, owner permission, cooldown, life state,
  hindrances, match state, targets, ranges, and finite input before applying effects.
- The owner starts UI cooldown and use events only after server acceptance.
- Smoke modifiers are tracked per source and removed on exit, expiry, or despawn.
  Overlapping clouds do not stack their penalties. Replicated cloud position and
  expiry also drive visuals on late-joining clients.
- X Speed masks both existing and subsequently applied movement slows. Slow
  calculations refresh from committed NetworkList changes, including on remote
  owners, rather than relying on status RPCs arriving after list replication.
- Full Heal preserves positive effects instead of clearing every status.
- Shedinja uses independent movement/action leases and a server action lock;
  ending it does not release another effect's restrictions. Scoring protection
  cannot bypass its scoring/recall locks.
- Existing enum IDs remain stable; new IDs are appended after custom Brick.
  Both authored selection catalogs include all ten official battle items.
- Brick no longer uses the shared spawn notification or global local-player aim
  singleton. Initialization is server-only and idempotent; tweens and asset
  handles are released on teardown.

## Automated checks

Run `Unite.BattleItems.Tests` in EditMode and
`Unite.BattleItems.PlayMode.Tests` in PlayMode. The latter starts an isolated
dedicated NGO server and a host with a minimal authored network prefab. Existing combat
test assemblies provide regression coverage for shared stat/status plumbing.

Validation on November 3, 2026:

- Battle-item coverage: 21 EditMode checks and 18 host/dedicated-server checks pass.
- Extended EditMode run: 327/328 pass across battle items, Flareon, Rayquaza,
  Cinderace, and bots. The remaining bot-catalog test expects 13 entries but the
  already-modified roster has 16; those existing roster changes were left intact.
- Extended PlayMode run: 41/41 pass across battle items, Rayquaza, and Flareon.
- Unity compilation and `git diff --check` pass.

These tests do not replace a two-device match with latency, packet loss, and
reconnection. In particular, manually exercise directional/targeted input,
Goal Hacker with defenders entering/leaving and competing casts, Fluffy Tail
against ordinary wild Pokémon and objectives, and Shedinja during active moves,
recall, protected scoring, death, and disconnect. Do not interpret passing
automated checks as a guarantee that every network edge case has been excluded.

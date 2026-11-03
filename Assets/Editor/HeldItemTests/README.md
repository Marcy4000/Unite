# Held-item audit and regression coverage

## Reference and scope

Reference retrieved on 2026-11-03: https://unite-db.com/items and its held-only data feed, https://unite-db.com/held_items.json.

The site's item pages currently default to **level 40**. The assets and `Level30Items.json` use the site's progression calculation at **level 30**, not the `bonus1` / `bonus2` summaries. At level 30, each ordinary stat has 30 increments; alternating stats have 15 increments; `initial_diff` is added where present. Passive effects use their highest tier, attained at item level 20. Fractional percentage points are preserved separately from percentage multipliers.

The catalog has 32 nonempty items: the existing 18 plus Assault Vest, Weakness Policy, Exp. Share, Score Shield, Rescue Hood, Slick Spoon, Curse Bangle, Curse Incense, Charging Charm, Accel Bracer, Drive Lens, Big Root, Vanguard Bell, and Rusted Sword. All reference icons already exist under `Assets/Sprites/Items/HeldItems`. The generic Material icon is an upgrade resource, not Amulet Coin. Items lacking their own icons, including Choice Scarf and Tenacity Belt, are not added. Battle items are outside this change.

Rusted Sword has no invented stats or independent passive: it is restricted to Zacian in selection and server loadout validation. This does not add Zacian or its ability/move mechanics.

## Runtime invariants

- Item stats and effects initialize and run on the server, including for remote-owned players and bots. The equipped-item bitset and precise stat changes replicate to peers and late joiners.
- Existing item IDs and catalog positions are retained; new IDs are appended. Invalid Base64, invalid indices, duplicate items, missing slots, and overlong loadouts are normalized before effects are created.
- Damage procs use committed, resolved server damage. Item-generated damage is marked as item damage and cannot recursively proc additional damage/heal items. Lifesteal uses real HP loss rather than shields or overkill.
- Modern moves notify items at an accepted server cast, before their damage; their owner-side completion events do not notify items again. Legacy completion reports go through MovesController's accepted-action path, validate the selected learned move, and spend server-tracked cooldowns/reserves or Unite energy. There is no standalone held-item activation RPC. Legacy targeting and execution remain client-authored: this is not a migration of every legacy move to authoritative execution or a complete anti-cheat solution.
- Permanent score/KO stacks survive ordinary death. Temporary recovery, armed attacks, charge, and transient item shields are cleared on death; cooldowns are not reset by death. Despawn disposes handlers and item stat modifiers.
- Buddy Barrier and Resonant Guard choose one nearby, living, other ally by current HP and refresh their own shield family rather than stacking it. Rescue Hood tracks the source of ally healing and shields, including support-move shield leases.
- Score Shield is consumed before other shields, is removed on scoring completion/cancellation, protects through hindrances while intact, and sends the post-hit interruption result with damage. Assault Vest only absorbs Sp. Attack-based damage, including scaling inversion.
- Curse effects refresh one 30% anti-heal window instead of stacking. Big Root excludes environmental healing and passive HP regeneration. Slick Spoon's penetration follows Sp. Attack-based damage, including scaling inversion. Vanguard Bell excludes non-hindering marks; periodic damage can trigger a charged Charging Charm or Resonant Guard, but item damage cannot.

The feed does not define an assist attribution timeout. This implementation explicitly uses a **10-second recent-damage window**, excludes same-team damage and proximity-only EXP recipients, and clears contributors after each KO. Charging Charm measures ordinary movement in project world units and rejects single-frame displacements over five units to avoid charging teleports.

Changing `StatChange` and `ShieldInfo` serialization requires all peers to run the same game build.

## Validation

EditMode assembly: `Unite.HeldItems.Tests`.

PlayMode assembly: `Unite.HeldItems.PlayMode.Tests`.

The EditMode suite checks every eligible asset, factory, pre-existing icon, level-30 stat, stable scene catalog index, malformed selection, formula, and fractional stat identity. PlayMode tests use actual NGO host and dedicated-server sessions and a minimal, UI-free network prefab. Coverage includes damage recursion, shield/overkill exclusion, critical and move procs, score shield ordering, ally shield selection, healing attribution, anti-heal expiration, stat stacking, proc cooldowns, charge movement, out-of-combat activation, passive EXP, KO/assist attribution, and a connected late-joining remote owner receiving equipment and fractional stats without duplicate procs.

These automated checks do not replace a full production-scene multiplayer soak test under packet loss, reconnects, or migration. Do not interpret a passing suite as a guarantee that every possible gameplay interaction is bug-free.

### Recorded validation, 2026-11-03

- Final focused PlayMode run: **62/62 passed**, split between host and dedicated-server fixtures, including connected remote ownership and pooled despawn/respawn.
- Focused EditMode suite: **16/16 passed**.
- Full-project EditMode run: **1723/1738 passed**. The 15 remaining failures concern the bot-roster count, Chinese lobby materials, trainer death positioning, character metadata, reserve UI, shared error UI, and city NPC counts; these unrelated expectations/assets were not changed for this task.
- The last broad combat/network PlayMode run passed **296/298**. Its two failures were the newly added legacy cooldown-reduction checks; the double/float clock mismatch was corrected and those checks pass in the final focused run. An earlier broad run passed 298/298 before the final precision/initial-health additions. The broad suite was not repeated after those final changes.

Fractional critical-hit chance now uses a floating-point roll rather than integer buckets. Full-health initialization includes held-item HP bonuses without granting healing credit, healing injured holders, or reviving defeated holders. Legacy item validation mirrors trusted cooldown reductions and upgraded reserve capacity.

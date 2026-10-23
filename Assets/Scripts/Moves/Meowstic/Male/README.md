# Meowstic Male rework

## Gameplay

The existing learnset and Unite energy cost are unchanged (displayed levels
1/2, 4, 6, 8, 10, 12). The four move combinations remain ally escort/control
builds; there is no additional resource system.

- Basics are ranged psychic projectiles. Every third attack uses special damage
  and applies a non-stacking 15% slow for 0.6 seconds. The sequence expires after
  five seconds without attacking; the owner sees boosted-ready pips.
  Normal bolts launch from the active model's hand socket and boosted bolts from
  its head socket, including while levitating. Projectile assets are kept warm;
  visual loading/network age never fast-forwards a bolt away from its first
  visible launch point, and movement uses time elapsed since launch rather than
  consuming a full pre-launch frame delta.
- Espurr blocks one timed removable stat debuff, weakens subsequent stat debuffs
  by 60% for three seconds, and recovers after a 23-second cycle. Buffs, persistent
  area modifiers, and unremovable effects do not consume this protection.
- Evolved Meowstic gains **5 CDR points** per nearby, living, non-invisible enemy
  player (7m range; maximum 25), not a percentage increase to its base CDR.
- Scratch retains its two-hit budget. Leer uses an actual 90-degree cone.
- Reflect protects against **physical damage only**. It stores 35% of damage
  received after mitigation, including shield absorption but excluding overkill,
  up to 2500 (3000 upgraded). A second use detonates early without restarting the
  original cooldown. The four-second protection ends with the detonation. A
  recipient KO also detonates at the last combat position, before respawn hiding
  can move the effect off-map; lethal damage contributes without counting overkill.
- Magic Coat intercepts eligible stat debuffs and hindrances *before* application.
  It reflects to the nearest visible enemy player within 8m, with NetworkObjectId
  as a deterministic tie-breaker. The current effect records do not carry a
  reliable original applier, so reflection does not pretend to target that source.
  No enemy is required to prevent the effect. Reflections cannot bounce between
  coats. Bound/suppression, periodic damage, and persistent area effects are not
  reflected. Upgrade: five seconds instead of four.
- Psychic gives +15% Defense and Sp. Defense, without the copied generic damage
  reduction. Its five 0.8-second pulses retain the damage/healing budget; upgrade
  increases the healing ratio from 0.35 to 0.45 Sp. Attack. Attack and Sp. Attack
  reductions are tracked separately from unrelated buffs/debuffs and cap at 60%.
- Wonder Room has a square 6.5m footprint. Enemy physical damage scales with
  Sp. Attack, and special damage with Attack, **without converting damage type**.
  Allied defenses increase 15% (20% upgraded). Overlaps are source-owned.
- Mystic Harmony has a 0.35-second startup and six-second active field. Regular
  attacks, moves, and battle items remain available after startup. Scoring stays
  disabled. Unstoppable covers the field lifetime. Each ally gets one 25%-max-HP
  shield per cast, expiring when that field ends; crossing the boundary or breaking
  the shield cannot replenish it. Speed and CDR buffs exist only within the field.

Cast commitment is 0.2 seconds for regular support moves, 0.3 for Scratch, and
0.35 for the Unite. Short input buffering lets another regular move follow the
recovery. Scoped action/movement restrictions cannot unlock another cast's locks.

## Authority and lifetime

`MovesController.Meowstic.cs` owns server gameplay, target/range validation,
cooldown gates, field membership, damage storage, and passive state. No new move
uses the legacy shared `onObjectSpawned` callback. Cosmetic loading never delays
gameplay. The project's Unite charge meter remains owner-managed, as for the
other licenses; this rework does not introduce a global energy-authority migration.

Each cast owns its effects, subscriptions, leased modifiers, and shield IDs.
Death/despawn releases them idempotently. Meowstic reserves effect IDs
**10000–13999**, independently of the other characters' pools. Duplicate copies
of one defensive aura use the strongest contribution; leaving one does not erase
another. Different moves can combine their explicitly documented protections.
Wonder Room source membership is a set, not a shared boolean toggle.

The small `Pokemon.Meowstic.cs` extension supplies pre-application filters,
resolved-damage reporting, physical-only reduction, and source-owned inversion.
`Stat.PhysicalDamageReduction` is appended, preserving existing serialized values.

## Presentation

Run **Tools / Meowstic / Create Missing Rework Prefabs** to author missing assets.
The builder follows Duraludon's baked-prefab workflow, preserves existing edits,
and registers local cosmetic prefabs in the `MovesAssets` Addressables group.
No meshes or primitives are constructed during a match. Legacy network prefabs
and area scripts are preserved for serialized compatibility but are no longer
spawned by Meowstic's move implementations.

Rework assets live in `Assets/Prefabs/Objects/Moves/Meowstic/Male/Rework/` and
`Assets/Materials/Meowstic/`. These include psychic bolts, claw/cone impacts,
segmented Reflect panels with storage pips, a reflection bolt, orbiting aura eyes,
square Room previews/boundaries, inversion markers, an open Unite canopy, and
short placeholder sounds. Owner-only passive/boost markers do not reveal stealth;
attached cosmetics respect their recipient's visibility. Fixed-roster cosmetic
RPCs follow the project model; late-join visual restoration is not provided.

**Rebuild Addressables before testing packaged clients.**

## Validation

EditMode suite: `Unite.Meowstic.Tests`. Tests exercise production move factories,
asset/rule consistency, passive filtering, typed damage and inversion formulas,
stack expiration, modifier ownership, reflection context, upgrade benefits,
cone geometry, and authored Addressable references.

PlayMode suite: `Unite.Meowstic.PlayMode.Tests`. It starts an isolated NGO host
without lobby/UI singletons and checks real RPC damage through shields,
pre-application hindrance rejection, independent timed shields, cleared-modifier
recovery, and overlapping Wonder Room ownership. It does not replace the
two-client combo/latency checks below.

Manual multiplayer acceptance checks:

1. Try all four move pairs on host and remote client; chain attacks and moves into
   and out of Mystic Harmony, including a battle-item escape.
2. Detonate Reflect at low/high storage, after shield absorption, and on recipient
   death. Special/true damage must not fill its meter.
3. Apply simultaneous stat reductions and hindrances inside opposing Magic Coats;
   no loops, delayed interruption, or beneficial-effect removal should occur.
4. Cross overlapping Rooms/Unites, become Unstoppable inside a Unite, break a
   shield, and re-enter. Verify only the appropriate source effects are removed.
5. Kill/disconnect the caster during every effect and during an outstanding visual
   load. Respawn in the same enemy-count configuration; passive CDR must return.
6. Verify square/radius boundaries, animations, audio and readability at the normal
   gameplay camera distance and on the intended mobile quality settings.

Numbers are a first-pass tuning baseline, not a claim of completed live balance.
